// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 DRG Software Solutions
//! Explicit derivative of the closed source plant profile; see `third_party/tidewater/LICENSE`.
//! Geometry and prepasses are shared by the explicit Bevy and source-lighting
//! specializations. Source lighting retains the original surface/BRDF functions.
use std::fmt::Write;

const IMPORT: &str = "#import bevy_pbr::{mesh_functions, view_transformations::position_world_to_clip,\n    mesh_view_bindings::view, pbr_types, pbr_functions::{apply_pbr_lighting,main_pass_post_lighting_processing}}";
const NATIVE_IMPORT: &str = "#import bevy_pbr::{mesh_functions, view_transformations::position_world_to_clip, mesh_view_bindings::view}";
const FRAME: &str = "struct WorldPlantFrame { camera:vec4f, wind:vec4f, gust:vec4f, previous_camera:vec4f, previous_wind:vec4f, previous_gust:vec4f, clouds:CloudsParams };\n@group(#{MATERIAL_BIND_GROUP}) @binding(18) var<storage,read> plantFrame:WorldPlantFrame;\n";

/// Replace only a named, unique-per-kind source function, refusing missing or
/// extra branches. These anchors contain no braces inside strings/comments.
fn functions(
    source: &str,
    name: &str,
    count: usize,
    mut replacement: impl FnMut(&str) -> String,
) -> Result<String, String> {
    let anchor = format!("fn {name}(");
    if source.matches(&anchor).count() != count {
        return Err(format!("World plant function drift: {name}"));
    }
    let mut rest = source;
    let mut result = String::new();
    while let Some(start) = rest.find(&anchor) {
        let body = rest[start..]
            .find('{')
            .ok_or("Missing plant function body")?
            + start;
        let mut depth = 1_usize;
        let mut end = None;
        for (index, byte) in rest.as_bytes().iter().enumerate().skip(body + 1) {
            match byte {
                b'{' => depth += 1,
                b'}' => {
                    depth -= 1;
                    if depth == 0 {
                        end = Some(index + 1);
                        break;
                    }
                }
                _ => {}
            }
        }
        let end = end.ok_or("Unclosed plant function")?;
        result.push_str(&rest[..start]);
        result.push_str(&replacement(&rest[start..end]));
        rest = &rest[end..];
    }
    result.push_str(rest);
    Ok(result)
}

pub fn derive(source: &str) -> Result<String, String> {
    // This is a source-profile adaptation, not an arbitrary WGSL importer.
    // Pinning also closes the exact meaning of the removed captured-light path.
    forge_assets::source_vegetation_package::profile::validate_world_shader(source)?;
    let mut result = super::super::with_valid_bark_power_domain(&source.replace("\r\n", "\n"))?;
    for (anchor, replacement) in [
        (IMPORT, NATIVE_IMPORT),
        (
            "    frame.cameraPos=view.world_position;",
            "    frame.cameraPos=plantFrame.camera.xyz;",
        ),
        (
            "    vegParams.camPos=view.world_position;",
            "    vegParams.camPos=plantFrame.camera.xyz;",
        ),
        (
            "    frame.time=probe.wind.w;",
            "    frame.time=plantFrame.wind.w;",
        ),
        (
            "    frame.windDir=probe.wind.xy;",
            "    frame.windDir=plantFrame.wind.xy;",
        ),
        (
            "    frame.windSpeed=probe.wind.z;",
            "    frame.windSpeed=plantFrame.wind.z;",
        ),
        (
            "    frame.frameIndex=u32(probe.lodEndFrame.y);",
            "    frame.frameIndex=u32(plantFrame.gust.z);",
        ),
        (
            "    vegParams.gustOffset=probe.gustLod.xy;",
            "    vegParams.gustOffset=plantFrame.gust.xy;",
        ),
    ] {
        if result.matches(anchor).count() != 4 {
            return Err(format!("World plant setup drift: {anchor}"));
        }
        result = result.replace(anchor, replacement);
    }
    result = functions(&result, "vegTranslucency", 4, |original| {
        format!(
            "#ifdef FORGE_SOURCE_LIGHTING\n{original}\n#else\nfn vegTranslucency(albedo:vec3f,N:vec3f,strength:f32,P:vec3f)->vec3f {{ return vec3f(strength); }}\n#endif"
        )
    })?;
    result = functions(&result, "vertex", 4, |_| String::new())?.replace("@vertex\n", "");
    result = functions(&result, "fragment", 4, |original| {
        original
            .replacen("fn fragment(", "fn forgeSourceFragment(", 1)
            .replace("@builtin(front_facing) front:bool", "front:bool")
            .replace("->@location(0) vec4f", "->vec4f")
            .replace(
                "normalize(view.world_position-vs.P)",
                "normalize(plantFrame.camera.xyz-vs.P)",
            )
    })?
    .replace("@fragment\n", "");
    let (plants, grass) = result
        .split_once("#ifdef PLANT_KIND_3")
        .ok_or("World plant branch boundary drift")?;
    // Add per-instance flags and previous geometry to the exact source varyings.
    let output = "    @location(10) vIPos4:vec4f,\n";
    if plants.matches(output).count() != 3 || grass.matches(output).count() != 1 {
        return Err("World plant output drift".into());
    }
    let plants = plants.replace(output, &format!("{output}    @location(11) @interpolate(flat) mesh_flags:u32,\n#ifdef MOTION_VECTOR_PREPASS\n    @location(12) previous_position:vec3f,\n#endif\n#ifdef UNCLIPPED_DEPTH_ORTHO_EMULATION\n    @location(13) unclipped_depth:f32,\n#endif\n"));
    // The source grass already uses 15 locations. Reuse its unused trunk
    // tangent/height fields for previous position/depth; only flags add a slot.
    let grass = grass.replace(
        output,
        &format!("{output}    @location(15) @interpolate(flat) mesh_flags:u32,\n"),
    );
    let grass = grass_height(&grass)?;
    result = format!(
        "{FRAME}{plants}#ifdef PLANT_KIND_3{grass}{}",
        include_str!("surface.wgsl")
    );
    let assignment = result
        .lines()
        .find(|line| line.trim_start().starts_with("frame.sunDir=vec3f("))
        .ok_or("Source sun assignment absent")?
        .trim()
        .to_owned();
    if result.matches(&assignment).count() != 4 {
        return Err("Source sun assignments differ across plant kinds".into());
    }
    let prepared = crate::source_lighting::scene_uniform::prepare(&result)?;
    let shader = crate::source_lighting::surface_library::reuse_plant_receivers(&prepared.shader)?;
    let shader = heightfield_domain(&shader)?;
    let result = shader.replace("frame.sunDir=sceneLighting.sunDirection.xyz;",
        &format!("\n#ifdef FORGE_SOURCE_LIGHTING\n    frame.sunDir=sceneLighting.sunDirection.xyz;\n#else\n    {assignment}\n#endif\n"));
    let result = crate::source_shadows::receiver::shader_source(&result)?;
    // Camera-dependent cloud uniforms share the existing frame upload. Mutating
    // every material would respecialize all its resident plant entities.
    let mut result =
        crate::source_lighting::local_lights::receiver::plant_shader_source(&result, 56)?
            .replace("cloudsParams.", "forgeCloudParams().");
    result.push_str("\nfn forgeCloudParams()->CloudsParams {\n#ifdef FORGE_SOURCE_LIGHTING\n    return plantFrame.clouds;\n#else\n    return cloudsParams;\n#endif\n}\n");
    Ok(crate::source_vegetation::with_source_derivative_directive(
        result,
    ))
}

fn heightfield_domain(source: &str) -> Result<String, String> {
    let coordinates = "((P.xz+1024.0)/2048.0)";
    if source.matches(coordinates).count() != 1 {
        return Err("World plant heightfield receiver domain drift".into());
    }
    let mut result = source.replace(coordinates, "((P.xz-forgeHillDomain.xy)/forgeHillDomain.z)");
    result.push_str(
        "\n@group(#{MATERIAL_BIND_GROUP}) @binding(55) var<uniform> forgeHillDomain:vec4f;\n",
    );
    Ok(result)
}

fn grass_height(source: &str) -> Result<String, String> {
    let start = source
        .find("\t// terrain height (float texels")
        .ok_or("Missing grass terrain evaluator")?;
    let end = source[start..]
        .find("\n\tlet base = vec3f")
        .map(|value| start + value)
        .ok_or("Missing grass terrain base")?;
    let original = &source[start..end];
    if original.matches("let ground = ").count() != 1 {
        return Err("Grass height return drift".into());
    }
    let original = original.replace("( xz - -1024.0 )", "( xz - grassField.zw )")
        .replace("let ground = ", "return ")
        .replace("\tlet hfi = floor( hfp );", "\tif any(hfp < vec2f(0.0)) || any(hfp >= vec2f(2047.0)) { return grassField.y; }\n\tlet hfi = floor( hfp );");
    let mut result = source
        .replacen(
            &source[start..end],
            "\tlet ground = terrainHeightAt(xz);",
            1,
        )
        .replace(
            "( xz - -1024.0 ) / 2048.0",
            "( xz - grassField.zw ) / 2048.0",
        );
    let corrections = crate::sampled_terrain_corrections::FUNCTIONS
        .replace("forgeBaseElevation", "grassField.x")
        .replace("xz+vec2f(1024.0)", "xz-grassField.zw");
    write!(result, "\n#ifdef PLANT_KIND_3\n@group(#{{MATERIAL_BIND_GROUP}}) @binding(19) var<uniform> grassField:vec4f;\n@group(#{{MATERIAL_BIND_GROUP}}) @binding(20) var forgeTerrainDeltas:texture_2d<f32>;\nfn forgeOriginalTerrainHeightAt(xz:vec2f)->f32 {{\n{original}\n}}\n{corrections}\n#endif\n").map_err(|error| error.to_string())?;
    Ok(result)
}
