// SPDX-License-Identifier: MIT
// Reprojection follows Tidewater MeshShader.js at 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
//! Preserve the installed fish geometry, skin ABI and source LOD coverage.
use super::{closing, fragments, replace, require, through};

pub(super) fn derive(vertex: &str, fragment: &str) -> Result<(String, String), &'static str> {
    for source in [vertex, fragment] {
        require(
            source,
            "@location(2) rest:vec3f,@location(3) data:vec4f,",
            2,
        )?;
        require(
            source,
            "@binding(19) var<storage,read> fishSkin:FishSkin;",
            1,
        )?;
        require(source, "@location(4) length_m:f32,", 1)?;
        if source.contains("tidewater_previous_P")
            || source.contains("pbr_functions::")
            || source.contains("pbr_types::")
        {
            return Err("already derived or incompatible fish lighting imports");
        }
    }
    require(vertex, "output.P=(world*vec4f(input.position,1.0)).xyz;", 1)?;
    let extend = |source: &str| -> Result<String, &'static str> {
        require(source, "struct Output {", 1)?;
        let start = source.find("struct Output {").ok_or("fish Output")?;
        let end = closing(source, start)?;
        if source[start..end].contains("@location(5)") {
            return Err("fish varying location 5 occupied");
        }
        let mut extended = source.to_owned();
        extended.insert_str(end, "\n @location(5) tidewater_previous_P:vec3f,\n");
        Ok(extended)
    };
    let vertex = replace(
        &extend(vertex)?,
        "return output;",
        "output.tidewater_previous_P=(mesh_functions::get_previous_world_from_local(input.instance)*vec4f(input.position,1.0)).xyz;\n return output;",
        1,
    )?;
    let fragment = fragments(
        &extend(fragment)?,
        "fn fragment(vs:Output)->@location(0) vec4f {",
        1,
        |body| {
            require(body, "frame.frameIndex=settings.frameIndex;", 1)?;
            require(body, "let surface=fishSurface(input);", 1)?;
            let prefix = through(
                body,
                "if (!lodFadeVisible(input.pixel,settings.lod.x,settings.lod.y>0.5)) {discard;}",
            )?;
            if body[prefix.len()..].contains("discard") {
                return Err("unrecognized fish post-surface coverage");
            }
            Ok(format!(
                "{prefix}\nreturn tidewater_auxiliary_world(vs.P,vs.tidewater_previous_P,1.0,1.0,vec4f(0.0));\n"
            ))
        },
    )?;
    Ok((vertex, fragment))
}
