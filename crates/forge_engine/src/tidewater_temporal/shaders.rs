// SPDX-License-Identifier: MIT
// Adapted from Tidewater MeshShader.js / WaterMaterial.js / Breakers.js / Spray.js at 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
//! Strict auxiliary shader derivation. Main shader strings and layouts remain caller-owned.
//!
//! The caller must retain the original vertex buffers, defines, culling and depth coverage.
//! Group 1 replaces Bevy's lighting binding array with the 336-byte per-view packet.
//! Unsupported materials return an error; they must be counted, never given camera fallback.
//! Plants, terrain and water deliberately reproject the current deformed world position,
//! matching the pinned source's staticVelocity contract. Village uses its real previous
//! mesh model and the current local deformation. Sky excludes camera translation (W=0).

use std::{error::Error as StdError, fmt, ops::Range};

pub const PACKET: &str = include_str!("packet.wgsl");

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Family {
    TerrainLod,
    Plant,
    Village,
    Ocean,
    /// Source late premultiplied lip, static world reprojection and real opacity.
    BreakerLip,
    /// Source late spray, including alive guards, depth fades and fragment discard.
    Spray,
    /// Current bounded Forge `StallKit` adapter, not source beauty qualification.
    FishStand,
}

#[derive(Debug)]
pub struct Derived {
    /// Entry name `vertex`, with the original material specialization defines.
    pub vertex: String,
    /// Entry name `fragment`; location 0 RGBA16F velocity, location 1 RGBA8 mask.
    pub fragment: String,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Error {
    pub family: Family,
    pub seam: &'static str,
}

impl fmt::Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(
            f,
            "Unsupported Tidewater {:?} auxiliary shader seam: {}",
            self.family, self.seam
        )
    }
}
impl StdError for Error {}

/// Derive from the actual installed shaders, including authored terrain/vegetation changes.
/// A known seam is mandatory; this function does not classify unknown shaders by appearance.
pub fn derive(family: Family, vertex: &str, fragment: &str) -> Result<Derived, Error> {
    let error = |seam| Error { family, seam };
    if [vertex, fragment].iter().any(|source| {
        source.contains("tidewater_temporal_packet")
            || source.contains("var<uniform> refractionCapture:")
    }) {
        return Err(error("already derived or refraction-only shader"));
    }
    let mut vertex = vertex.to_owned();
    let mut fragment = fragment.to_owned();
    let count = entries(family, &vertex, &fragment).map_err(&error)?;
    match family {
        Family::TerrainLod => {
            require(&vertex, "terrainLodMorph(", 2).map_err(&error)?;
            require(&vertex, "o.world_position=vec4f(P,1.0);", 1).map_err(&error)?;
            if fragment.contains("discard") {
                return Err(error("terrain coverage is no longer opaque"));
            }
            fragment = lighting_imports(&fragment, 1).map_err(&error)?;
            fragment = fragments(&fragment,
                "fn fragment(vs:VertexOutput)->@location(0) vec4f {", 1,
                |body| {
                    require(body, "sourceSurface(input,&surface);", 1)?;
                    Ok("\nreturn tidewater_auxiliary_world(vs.world_position.xyz,vs.world_position.xyz,1.0,1.0,vec4f(0.0));\n".into())
                }).map_err(&error)?;
        }
        Family::Plant => {
            require(&vertex, "sourceVertex(&source,&o);", 4).map_err(&error)?;
            require(&vertex, "o.P=source.worldPos;", 4).map_err(&error)?;
            require(&fragment, "#ifdef PLANT_KIND_3", 1).map_err(&error)?;
            fragment = lighting_imports(&fragment, 4).map_err(&error)?;
            // Do not add varyings: grass already uses locations 0..14 on WebGPU.
            fragment = fragments(&fragment,
                "fn fragment(vs:ProbeOut,@builtin(front_facing) front:bool)->@location(0) vec4f {", 4,
                |body| {
                    let prefix = through(body, "sourceSurface(input,&surface);")?;
                    if body[prefix.len()..].contains("discard") { return Err("unrecognized plant post-surface coverage"); }
                    Ok(format!("{prefix}\nreturn tidewater_auxiliary_world(vs.P,vs.P,1.0,1.0,vec4f(0.0));\n"))
                }).map_err(&error)?;
        }
        Family::Village => {
            require(&vertex, "sourceVertex(&v,&o);", 1).map_err(&error)?;
            let start = vertex
                .find("struct Output {")
                .ok_or(error("village Output"))?;
            let end = closing(&vertex, start).map_err(&error)?;
            let output = &vertex[start..end];
            if output.contains("@location(5)") {
                return Err(error("village varying location 5 occupied"));
            }
            vertex.insert_str(end, "\n @location(5) tidewater_previous_P:vec3f,\n");
            vertex = replace(&vertex, "o.uv=v.uv;return o;",
                "o.uv=v.uv;\n o.tidewater_previous_P=(mesh_functions::get_previous_world_from_local(input.instance)*vec4f(v.position,1.0)).xyz;\n return o;", 1).map_err(&error)?;
            // Fragment Input must use the same extended Output struct.
            let start = fragment
                .find("struct Output {")
                .ok_or(error("village fragment Output"))?;
            let end = closing(&fragment, start).map_err(&error)?;
            if fragment[start..end].contains("@location(5)") {
                return Err(error("village fragment varying location 5 occupied"));
            }
            fragment.insert_str(end, "\n @location(5) tidewater_previous_P:vec3f,\n");
            fragment = lighting_imports(&fragment, 1).map_err(&error)?;
            fragment = fragments(&fragment,
                "fn fragment(vs:Output,@builtin(front_facing) front:bool)->@location(0) vec4f {", 1,
                |body| {
                    let prefix = through(body, "if s.alpha < mat.alphaTest { discard; }")?;
                    if body[prefix.len()..].contains("discard") { return Err("unrecognized village post-surface coverage"); }
                    // The only transparent village material is source net (kind 6).
                    // Village glass is inside opaque wood kind 0, unlike boat glass weight 0.
                    Ok(format!("{prefix}\nvar alpha=1.0;\n#ifdef VILLAGE_KIND_6\nalpha=s.alpha;\n#endif\nreturn tidewater_auxiliary_world(vs.P,vs.tidewater_previous_P,alpha,1.0,vec4f(0.0));\n"))
                }).map_err(&error)?;
        }
        Family::Ocean => {
            require(&vertex, "let s=waterSurfaceVertex(node,grid.xz);", 1).map_err(&error)?;
            require(
                &fragment,
                "r.mask = vec4f( seenFromBelow, 1.0, 0.0, 1.0 );",
                1,
            )
            .map_err(&error)?;
            fragment = fragments(&fragment,
                "fn fragment(input:WaterVertex,@builtin(front_facing) front:bool)->@location(0) vec4f {", 1,
                ocean_body
            ).map_err(&error)?;
        }
        Family::FishStand => {
            (vertex, fragment) = fish_stand(vertex, fragment).map_err(&error)?;
        }
        Family::BreakerLip | Family::Spray => {
            fragment = late_world(family, &vertex, &fragment).map_err(&error)?;
        }
    }
    // Remove unused lighting imports in both modules: Naga validates globals too.
    if matches!(family, Family::Plant | Family::Village) {
        vertex = lighting_imports(&vertex, count).map_err(&error)?;
    }
    Ok(Derived {
        vertex: format!("{vertex}\n{PACKET}"),
        fragment: format!("{fragment}\n{PACKET}"),
    })
}

/// Fullscreen auxiliary sky; encode first at reversed depth 0 with no depth write.
#[must_use]
pub fn sky() -> String {
    format!("{PACKET}\n{}", include_str!("sky.wgsl"))
}

fn ocean_body(body: &str) -> Result<String, &'static str> {
    const ORIGINAL: &str = "return shadeWater(i).color;";
    // Actual visible_water_mask_shader_source encodes the source water mask in
    // main HDR alpha for the underwater pass. It is not velocity coverage alpha.
    const NATIVE: &str = "return vec4f(result.color.rgb, 0.25 + 0.5 * result.mask.x);";
    require(body, "shadeWater(i)", 1)?;
    match (body.matches(ORIGINAL).count(), body.matches(NATIVE).count()) {
        (1, 0) => replace(
            body,
            ORIGINAL,
            "let water=shadeWater(i);\n return tidewater_auxiliary_world(input.P,input.P,1.0,1.0,water.mask);",
            1,
        ),
        (0, 1) => {
            require(body, "let result = shadeWater(i);", 1)?;
            replace(
                body,
                NATIVE,
                "return tidewater_auxiliary_world(input.P,input.P,1.0,1.0,result.mask);",
                1,
            )
        }
        _ => Err("unknown or ambiguous water fragment output variant"),
    }
}

fn late_world(family: Family, vertex: &str, fragment: &str) -> Result<String, &'static str> {
    let signature = match family {
        Family::BreakerLip => {
            require(vertex, "o.P=select(vec3f(0.0,-1e5,0.0),P,valid);", 1)?;
            require(fragment, "fn shade(in:LipInput)->LipResult{", 1)?;
            require(fragment, "r.color = vec4f( col * alpha, aOut );", 1)?;
            "fn fragment(v:LipVertex,@builtin(front_facing) front:bool)->@location(0) vec4f{"
        }
        Family::Spray => {
            require(
                vertex,
                "o.P=select(vec3f(0.0,-1e5,0.0),world,alive && a > 1e-4);",
                1,
            )?;
            require(fragment, "fn shade(in:SpriteInput)->SpriteResult{", 1)?;
            require(fragment, "if ( aOut < 0.002 ) { discard; }", 1)?;
            require(
                fragment,
                "let fadeScene = sat( ( posViewZ - sceneZ ) / soft );",
                1,
            )?;
            "fn fragment(v:SpriteVertex)->@location(0) vec4f{"
        }
        _ => return Err("not a source late world material"),
    };
    // Native shade is executed in full, retaining all alpha/coverage and discards.
    // Source useWorld/staticVelocity reprojects the SAME current deformed P.
    // The renderer must use premultiplied velocity blend, normal mask blend,
    // and depth_write=false. Mask alpha 0 preserves the previously rendered mask.
    fragments(fragment, signature, 1, |body| {
        require(body, "setupFrame();", 1)?;
        require(body, "i.P=v.P;", 1)?;
        replace(
            body,
            "return shade(i).color;",
            "let result=shade(i);return tidewater_auxiliary_world(v.P,v.P,result.color.a,1.0,vec4f(0.0));",
            1,
        )
    })
}

fn fish_stand(mut vertex: String, mut fragment: String) -> Result<(String, String), &'static str> {
    if fragment.contains("discard") {
        return Err("stand coverage is no longer opaque");
    }
    require(
        &vertex,
        "out.world = (mesh_functions::get_world_from_local(input.instance) * vec4f(input.position, 1.0)).xyz;",
        1,
    )?;
    for source in [&mut vertex, &mut fragment] {
        let start = source.find("struct Output {").ok_or("stand Output")?;
        let end = closing(source, start)?;
        if source[start..end].contains("@location(6)") {
            return Err("stand varying location 6 occupied");
        }
        source.insert_str(end, "\n @location(6) tidewater_previous_P:vec3f,\n");
    }
    vertex = replace(
        &vertex,
        "out.uv2 = input.uv2; return out;",
        "out.uv2 = input.uv2;\n out.tidewater_previous_P = (mesh_functions::get_previous_world_from_local(input.instance) * vec4f(input.position, 1.0)).xyz;\n return out;",
        1,
    )?;
    fragment = fragments(
        &fragment,
        "fn fragment(input: Output) -> @location(0) vec4f {",
        1,
        |body| {
            require(
                body,
                "return vec4f(albedo * (direct + indirect), input.color.a);",
                1,
            )?;
            Ok("\nreturn tidewater_auxiliary_world(input.world,input.tidewater_previous_P,1.0,1.0,vec4f(0.0));\n".into())
        },
    )?;
    Ok((vertex, fragment))
}

fn entries(family: Family, vertex: &str, fragment: &str) -> Result<usize, &'static str> {
    let count = if family == Family::Plant { 4 } else { 1 };
    require(vertex, "@vertex", count)?;
    require(fragment, "@fragment", count)?;
    let entry = match family {
        Family::TerrainLod => "fn vertex(v:LodInput)->VertexOutput {",
        Family::Plant => "fn vertex(v:ProbeVertex)->ProbeOut {",
        Family::Village => "fn vertex(input:Input)->Output {",
        Family::FishStand => "fn vertex(input: Input) -> Output {",
        Family::BreakerLip => {
            "fn vertex(@location(0) position:vec3f,@location(1) sheetId:vec4f)->LipVertex{"
        }
        Family::Spray => {
            "fn vertex(@location(0) position:vec3f,@location(1) slot:u32)->SpriteVertex{"
        }
        Family::Ocean => {
            "fn vertex(@location(0) grid:vec3f,@location(1) node:vec4f)->WaterVertex {"
        }
    };
    require(vertex, entry, count)?;
    Ok(count)
}

fn require(source: &str, seam: &'static str, count: usize) -> Result<(), &'static str> {
    if source.matches(seam).count() == count {
        Ok(())
    } else {
        Err(seam)
    }
}
fn replace(
    source: &str,
    seam: &'static str,
    replacement: &str,
    count: usize,
) -> Result<String, &'static str> {
    require(source, seam, count)?;
    Ok(source.replace(seam, replacement))
}
fn through<'a>(body: &'a str, seam: &'static str) -> Result<&'a str, &'static str> {
    require(body, seam, 1)?;
    let end = body.find(seam).ok_or(seam)? + seam.len();
    Ok(&body[..end])
}

fn lighting_imports(source: &str, count: usize) -> Result<String, &'static str> {
    let mut source = source.to_owned();
    let mut ranges = Vec::new();
    for (start, _) in source.match_indices("#import bevy_pbr::{") {
        let end = closing(&source, start)? + 1;
        if source[start..end]
            .contains("pbr_functions::{apply_pbr_lighting,main_pass_post_lighting_processing}")
        {
            ranges.push(start..end);
        }
    }
    if ranges.len() != count {
        return Err("known unused PBR lighting import");
    }
    for range in ranges.into_iter().rev() {
        let import = if source[range.clone()].contains("forward_io::VertexOutput") {
            "#import bevy_pbr::{forward_io::VertexOutput,mesh_view_bindings::view}"
        } else {
            "#import bevy_pbr::{mesh_functions,mesh_view_bindings::view,view_transformations::position_world_to_clip}"
        };
        source.replace_range(range, import);
    }
    // Other references would require a broader adapter, not a hidden missing binding.
    if source.contains("pbr_types::")
        || source.contains("apply_pbr_lighting(")
        || source.contains("main_pass_post_lighting_processing(")
    {
        return Err("lighting function still reachable or present");
    }
    Ok(source)
}

fn fragments(
    source: &str,
    entry: &'static str,
    count: usize,
    body: impl Fn(&str) -> Result<String, &'static str>,
) -> Result<String, &'static str> {
    require(source, entry, count)?;
    let mut replacements = Vec::<(Range<usize>, String)>::new();
    for (start, _) in source.match_indices(entry) {
        let open = start + entry.len() - 1;
        let end = closing(source, open)?;
        let contents = body(&source[open + 1..end])?;
        let signature = entry.replace("@location(0) vec4f", "TidewaterAuxiliaryOutput");
        replacements.push((start..end + 1, format!("{signature}{contents}}}")));
    }
    let mut result = source.to_owned();
    for (range, replacement) in replacements.into_iter().rev() {
        result.replace_range(range, &replacement);
    }
    Ok(result)
}

// Source functions contain nested blocks and comments; braces inside comments are not syntax.
fn closing(source: &str, from: usize) -> Result<usize, &'static str> {
    let bytes = source.as_bytes();
    let mut index = from;
    let mut depth = 0usize;
    let mut begun = false;
    while index < bytes.len() {
        if bytes[index..].starts_with(b"//") {
            index += bytes[index..]
                .iter()
                .position(|b| *b == b'\n')
                .unwrap_or(bytes.len() - index);
            continue;
        }
        if bytes[index..].starts_with(b"/*") {
            let mut comments = 1usize;
            index += 2;
            while index < bytes.len() && comments > 0 {
                if bytes[index..].starts_with(b"/*") {
                    comments += 1;
                    index += 2;
                } else if bytes[index..].starts_with(b"*/") {
                    comments -= 1;
                    index += 2;
                } else {
                    index += 1;
                }
            }
            if comments != 0 {
                return Err("unterminated shader comment");
            }
            continue;
        }
        match bytes[index] {
            b'{' => {
                depth += 1;
                begun = true;
            }
            b'}' if begun => {
                depth -= 1;
                if depth == 0 {
                    return Ok(index);
                }
            }
            _ => {}
        }
        index += 1;
    }
    Err("unterminated shader block")
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{
        asset::Assets,
        math::{Mat4, Vec2, Vec3, Vec4},
        shader::{
            Shader, ShaderCache, ShaderCacheError, ShaderCacheSource, ShaderDefVal, ValidateShader,
        },
    };
    use std::{
        fs,
        path::{Path, PathBuf},
    };

    fn validate(source: &str) -> naga::Module {
        let module = naga::front::wgsl::parse_str(source).unwrap();
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::all(),
        )
        .validate(&module)
        .unwrap();
        module
    }

    #[test]
    fn packet_and_sky_validate_with_exact_336_byte_abi() {
        let module = validate(&sky());
        let packet = module
            .types
            .iter()
            .find(|(_, ty)| ty.name.as_deref() == Some("TidewaterTemporalPacket"))
            .unwrap()
            .1;
        let naga::TypeInner::Struct { members, span } = &packet.inner else {
            panic!("packet must be a struct")
        };
        assert_eq!(*span, 336);
        assert_eq!(
            members.iter().map(|m| m.offset).collect::<Vec<_>>(),
            [0, 64, 128, 192, 256, 272, 288, 304, 320]
        );
        assert_eq!(module.entry_points.len(), 2);
        assert!(PACKET.contains("* vec2<f32>(0.5, -0.5)"));
        assert!(PACKET.contains("velocity * coverage, 0.0, coverage"));
    }

    fn uv(current: Vec4, previous: Vec4) -> Vec2 {
        (current.truncate().truncate() / current.w - previous.truncate().truncate() / previous.w)
            * Vec2::new(0.5, -0.5)
    }

    // Inspect the actual WGSL/Naga branch, then exercise its projection contract
    // on CPU. This does not execute a GPU shader or qualify compiler arithmetic.
    fn recovery_model_mask() -> u32 {
        let module = validate(PACKET);
        let world = module
            .functions
            .iter()
            .find(|(_, f)| f.name.as_deref() == Some("tidewater_auxiliary_world"))
            .unwrap()
            .1;
        let (condition, accept, reject) = world
            .body
            .iter()
            .find_map(|s| match s {
                naga::Statement::If {
                    condition,
                    accept,
                    reject,
                } => Some((*condition, accept, reject)),
                _ => None,
            })
            .unwrap();
        assert!(
            reject.is_empty(),
            "legacy previous model must remain intact"
        );
        let naga::Expression::Binary {
            op: naga::BinaryOperator::NotEqual,
            left,
            right,
        } = world.expressions[condition]
        else {
            panic!("recovery must test a flag against zero")
        };
        assert!(matches!(
            world.expressions[right],
            naga::Expression::Literal(naga::Literal::U32(0))
        ));
        let naga::Expression::Binary {
            op: naga::BinaryOperator::And,
            right: flag,
            ..
        } = world.expressions[left]
        else {
            panic!("recovery must use a bit mask, not the seed boolean")
        };
        let naga::Expression::Literal(naga::Literal::U32(mask)) = world.expressions[flag] else {
            panic!("recovery flag must be a u32 literal")
        };
        assert_eq!(mask, 2);
        let (previous_world, _) = world
            .local_variables
            .iter()
            .find(|(_, v)| v.name.as_deref() == Some("previous_world"))
            .unwrap();
        let stores_argument = |s: &naga::Statement, argument| match s {
            naga::Statement::Store { pointer, value } => {
                matches!(world.expressions[*pointer], naga::Expression::LocalVariable(v) if v == previous_world)
                    && matches!(world.expressions[*value], naga::Expression::FunctionArgument(i) if i == argument)
            }
            _ => false,
        };
        assert!(world.body.iter().any(|s| stores_argument(s, 1)));
        assert!(accept.iter().any(|s| stores_argument(s, 0)));
        assert!(PACKET.contains("phase_epoch_seed.w & 2u"));
        assert!(PACKET.contains("vec4<f32>(previous_world, 1.0)"));
        mask
    }

    fn projected_world_velocity(
        matrices: [Mat4; 2],
        positions: [Vec3; 2],
        flags: u32,
        recovery_mask: u32,
    ) -> Vec2 {
        let previous = if flags & recovery_mask != 0 {
            positions[0]
        } else {
            positions[1]
        };
        uv(
            matrices[0] * positions[0].extend(1.0),
            matrices[1] * previous.extend(1.0),
        )
    }

    #[test]
    fn explicit_model_recovery_resets_object_motion_only_for_flag_two() {
        let mask = recovery_model_mask();
        let projection = Mat4::perspective_rh(1.0, 1.7, 0.1, 1000.0);
        let local = Vec3::new(0.0, 0.0, -4.0);
        let positions = [
            Mat4::from_translation(Vec3::X).transform_point3(local),
            local,
        ];
        let matrices = [projection, projection];
        let real_motion = uv(
            projection * positions[0].extend(1.0),
            projection * positions[1].extend(1.0),
        );
        assert!(real_motion.x > 0.1);
        // w=1 can mean resize/history seed, which must retain model movement.
        for flags in [0, 1, 4, 5] {
            assert_eq!(
                projected_world_velocity(matrices, positions, flags, mask),
                real_motion
            );
        }
        for flags in [2, 3] {
            assert_eq!(
                projected_world_velocity(matrices, positions, flags, mask),
                Vec2::ZERO
            );
        }
    }

    #[test]
    fn model_recovery_keeps_camera_motion_until_camera_history_is_also_seeded() {
        let mask = recovery_model_mask();
        let projection = Mat4::perspective_rh(1.0, 1.7, 0.1, 1000.0);
        let moved_camera = projection * Mat4::from_translation(Vec3::new(-0.2, 0.1, 0.0));
        let positions = [Vec3::new(1.0, 0.0, -4.0), Vec3::new(0.0, 0.0, -4.0)];
        let camera_only = uv(
            moved_camera * positions[0].extend(1.0),
            projection * positions[0].extend(1.0),
        );
        assert!(camera_only.length() > 0.01);
        assert_eq!(
            projected_world_velocity([moved_camera, projection], positions, 3, mask),
            camera_only
        );
        assert_eq!(
            projected_world_velocity([moved_camera, moved_camera], positions, 3, mask),
            Vec2::ZERO
        );
    }

    #[test]
    fn static_world_recovery_preserves_source_motion_coverage_and_mask() {
        let mask = recovery_model_mask();
        let projection = Mat4::perspective_rh(1.0, 1.7, 0.1, 1000.0);
        let matrices = [
            projection * Mat4::from_translation(Vec3::new(-0.2, 0.1, 0.0)),
            projection,
        ];
        let point = Vec3::new(0.3, 0.2, -4.0);
        let positions = [point, point];
        let source = projected_world_velocity(matrices, positions, 0, mask);
        assert!(source.length() > 0.01);
        for flags in [0, 1, 2, 3] {
            assert_eq!(
                projected_world_velocity(matrices, positions, flags, mask),
                source
            );
        }
        let start = PACKET.find("fn tidewater_auxiliary_clips(").unwrap();
        let open = start + PACKET[start..].find('{').unwrap();
        let end = closing(PACKET, open).unwrap();
        let clips = &PACKET[open + 1..end];
        assert!(!clips.contains("phase_epoch_seed"));
        assert!(clips.contains("let coverage = alpha * weight;"));
        assert!(clips.contains("velocity * coverage, 0.0, coverage"));
        assert!(clips.contains("output.mask = mask;"));
    }

    #[test]
    fn world_interpolation_equals_source_clip_interpolation_without_extra_varyings() {
        let current = Mat4::perspective_rh(1.0, 1.7, 0.1, 1000.0);
        let previous = current * Mat4::from_translation(Vec3::new(-0.2, 0.1, 0.0));
        let points = [
            Vec3::new(-1.0, 0.0, -2.0),
            Vec3::new(1.0, 0.8, -4.0),
            Vec3::new(0.0, -1.0, -3.0),
        ];
        let barycentric = [0.2, 0.3, 0.5];
        let mut denominator = 0.0;
        let mut world = Vec3::ZERO;
        let mut interpolated_current = Vec4::ZERO;
        let mut interpolated_previous = Vec4::ZERO;
        for (point, barycentric) in points.into_iter().zip(barycentric) {
            let clip = current * point.extend(1.0);
            let coefficient = barycentric / clip.w;
            denominator += coefficient;
            world += point * coefficient;
            interpolated_current += clip * coefficient;
            interpolated_previous += (previous * point.extend(1.0)) * coefficient;
        }
        world /= denominator;
        let direct = uv(current * world.extend(1.0), previous * world.extend(1.0));
        let source = uv(
            interpolated_current / denominator,
            interpolated_previous / denominator,
        );
        assert!(direct.abs_diff_eq(source, 1e-6));
        assert!(direct.x > 0.0 && direct.y > 0.0);
        assert_eq!(
            uv(current * world.extend(1.0), current * world.extend(1.0)),
            Vec2::ZERO
        );
    }

    #[test]
    fn sky_excludes_translation_but_preserves_rotation_and_village_preserves_model_motion() {
        let projection = Mat4::perspective_rh(1.0, 1.7, 0.1, 1000.0);
        let direction = Vec3::new(0.2, 0.1, -1.0).extend(0.0);
        assert_eq!(
            uv(
                projection * direction,
                (projection * Mat4::from_translation(Vec3::X)) * direction
            ),
            Vec2::ZERO
        );
        assert!(
            uv(
                projection * direction,
                (projection * Mat4::from_rotation_y(0.1)) * direction
            )
            .length()
                > 0.01
        );
        let local = Vec3::new(0.0, 0.0, -4.0).extend(1.0);
        assert!(
            uv(
                projection * (Mat4::from_translation(Vec3::X) * local),
                projection * local
            )
            .x > 0.1
        );
    }

    #[test]
    fn unknown_or_already_derived_sources_fail_without_camera_fallback() {
        for family in [
            Family::TerrainLod,
            Family::Plant,
            Family::Village,
            Family::Ocean,
            Family::FishStand,
        ] {
            assert!(
                derive(
                    family,
                    "@vertex fn vertex() {}",
                    "@fragment fn fragment() {}"
                )
                .is_err()
            );
            assert!(derive(family, PACKET, PACKET).is_err());
        }
        let comments = "{ /* } { /* } */ */ // }\n { } }";
        assert_eq!(closing(comments, 0).unwrap(), comments.len() - 1);
        assert!(closing("{ /* never closed", 0).is_err());
    }

    #[test]
    fn recognizes_actual_native_water_mask_rewrite_without_dropping_coverage() {
        let original = "setupWaterFrame();var i:WaterInput;i.P=input.P;i.pixel=input.clip.xy;i.front=front;i.vs=input;\n if input.P.y < -100.0 { discard; }\n return shadeWater(i).color;";
        let rewritten =
            crate::tidewater_underwater::visible_water_mask_shader_source(original).unwrap();
        for source in [original, &rewritten] {
            let derived = ocean_body(source).unwrap();
            assert!(derived.contains("if input.P.y < -100.0 { discard; }"));
            assert!(derived.contains("setupWaterFrame();var i:WaterInput;i.P=input.P;"));
            assert_eq!(derived.matches("shadeWater(i)").count(), 1);
            assert!(derived.contains("tidewater_auxiliary_world(input.P,input.P,1.0,1.0,"));
        }
        let derived = ocean_body(&rewritten).unwrap();
        assert!(derived.contains("let result = shadeWater(i);"));
        assert!(derived.contains("1.0,1.0,result.mask)"));
        assert!(!derived.contains("0.25 + 0.5 * result.mask.x"));
        assert!(
            ocean_body(&format!(
                "{original}\n return vec4f(result.color.rgb, 0.25 + 0.5 * result.mask.x);"
            ))
            .is_err()
        );
        assert!(ocean_body(&rewritten.replace("0.25 + 0.5", "0.3 + 0.4")).is_err());
        assert!(ocean_body(&rewritten.replace("let result =", "let other =")).is_err());
    }

    #[allow(clippy::trivially_copy_pass_by_ref)] // ShaderCache imposes fn(&RenderDevice, ..).
    fn load_cpu_shader(
        _: &(),
        source: ShaderCacheSource<'_>,
        _: &ValidateShader,
    ) -> Result<naga::Module, ShaderCacheError> {
        let ShaderCacheSource::Naga(module) = source else {
            return Err(ShaderCacheError::CreateShaderModule(
                "expected real Naga composition".into(),
            ));
        };
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::all(),
        )
        .validate(&module)
        .map_err(|e| ShaderCacheError::CreateShaderModule(format!("{e:?}")))?;
        // Imports must not inject the original lighting binding array into group 1.
        let bindings = module
            .global_variables
            .iter()
            .filter_map(|(_, var)| var.binding.as_ref())
            .filter(|binding| binding.group == 1)
            .collect::<Vec<_>>();
        assert_eq!(
            bindings.len(),
            1,
            "group 1 is reserved exclusively for the temporal packet"
        );
        assert_eq!(bindings[0].binding, 0);
        Ok(module)
    }

    fn register_libraries(
        path: &Path,
        assets: &mut Assets<Shader>,
        cache: &mut ShaderCache<naga::Module, ()>,
    ) {
        for entry in fs::read_dir(path).unwrap() {
            let path = entry.unwrap().path();
            if path.is_dir() {
                register_libraries(&path, assets, cache);
            } else if path
                .extension()
                .is_some_and(|extension| extension == "wgsl")
            {
                let source = fs::read_to_string(&path).unwrap();
                if source.contains("#define_import_path") {
                    let shader = Shader::from_wgsl(source, path.to_string_lossy());
                    let handle = assets.add(shader.clone());
                    cache.set_shader(handle.id(), shader);
                }
            }
        }
    }

    fn definitions(family: Family, kind: u32) -> Vec<ShaderDefVal> {
        let mut definitions = vec![
            ShaderDefVal::UInt("MATERIAL_BIND_GROUP".into(), 3),
            ShaderDefVal::UInt("PER_OBJECT_BUFFER_BATCH_SIZE".into(), 64),
            ShaderDefVal::UInt("AVAILABLE_STORAGE_BUFFER_BINDINGS".into(), 8),
            ShaderDefVal::UInt(
                "MAX_CASCADES_PER_LIGHT".into(),
                u32::try_from(bevy::pbr::MAX_CASCADES_PER_LIGHT).unwrap(),
            ),
            ShaderDefVal::UInt(
                "MAX_DIRECTIONAL_LIGHTS".into(),
                u32::try_from(bevy::pbr::MAX_DIRECTIONAL_LIGHTS).unwrap(),
            ),
            ShaderDefVal::UInt(
                "MAX_RECT_LIGHTS".into(),
                u32::try_from(bevy::pbr::MAX_RECT_LIGHTS).unwrap(),
            ),
            ShaderDefVal::Bool("VERTEX_OUTPUT_INSTANCE_INDEX".into(), true),
            ShaderDefVal::Bool("ENVIRONMENT_MAP".into(), true),
        ];
        match family {
            Family::Plant => definitions.push(format!("PLANT_KIND_{kind}").into()),
            Family::Village => {
                definitions.push(format!("VILLAGE_KIND_{kind}").into());
                if kind >= 5 {
                    definitions.push("VILLAGE_DOUBLE_SIDED".into());
                }
            }
            _ => {}
        }
        definitions
    }

    fn assert_source_functions_unchanged(family: Family, source: &str, derived: &Derived) {
        for function in [
            "sourceVertex(",
            "sourceSurface(",
            "waterSurfaceVertex(",
            "shadeWater(",
            "terrainLodMorph(",
            "terrainHeightAt(",
            "setupFrame(",
            "shade(",
        ] {
            for (start, _) in source.match_indices(&format!("fn {function}")) {
                let end = closing(source, start).unwrap() + 1;
                assert!(
                    derived.vertex.contains(&source[start..end])
                        || derived.fragment.contains(&source[start..end]),
                    "{family:?} changed {function}"
                );
            }
        }
    }

    fn validate_native_ocean(
        fixture: &Path,
        assets: &mut Assets<Shader>,
        cache: &mut ShaderCache<naga::Module, ()>,
    ) {
        let original = fs::read_to_string(fixture.join("scene/water-optics.wgsl")).unwrap();
        // Exact production chain in native ocean_geometry::install_shaders;
        // these are the real shared adapters, not a copied synthetic rewrite.
        let receiver = crate::tidewater_object_shadow::receiver::shader_source(&original).unwrap();
        let masked =
            crate::tidewater_underwater::visible_water_mask_shader_source(&receiver).unwrap();
        let runtime = crate::tidewater_refraction::water_shader_source(&masked).unwrap();
        let derived = derive(Family::Ocean, &runtime, &runtime).unwrap();
        assert_source_functions_unchanged(Family::Ocean, &runtime, &derived);
        assert!(derived.fragment.contains("1.0,1.0,result.mask)"));
        for source in [derived.vertex, derived.fragment] {
            let shader = Shader::from_wgsl(source, "aux-native-ocean-runtime.wgsl");
            let handle = assets.add(shader.clone());
            cache.set_shader(handle.id(), shader);
            cache
                .get(0, handle.id(), &definitions(Family::Ocean, 0))
                .unwrap();
        }
    }

    fn assert_late_world(family: Family, vertex: &str, fragment: &str, derived: &Derived) {
        if !matches!(family, Family::BreakerLip | Family::Spray) {
            return;
        }
        assert_eq!(derived.vertex, format!("{vertex}\n{PACKET}"));
        assert!(
            derived
                .fragment
                .contains("v.P,v.P,result.color.a,1.0,vec4f(0.0)")
        );
        assert_eq!(
            derived.fragment.matches("discard").count(),
            fragment.matches("discard").count()
        );
        assert!(
            derive(
                family,
                &vertex.replace("o.P=select(", "o.P=unknown("),
                fragment
            )
            .is_err()
        );
        assert!(
            derive(
                family,
                vertex,
                &fragment.replace("return shade(i).color;", "return vec4f(1.0);")
            )
            .is_err()
        );
        if family == Family::Spray {
            assert!(
                derive(
                    family,
                    vertex,
                    &fragment.replace("aOut < 0.002", "aOut < 0.1")
                )
                .is_err()
            );
        }
    }

    const SOURCE_CASES: [(Family, &str, &str, u32); 6] = [
        (
            Family::TerrainLod,
            "scene/terrain-lod.wgsl",
            "scene/terrain.wgsl",
            1,
        ),
        (Family::Plant, "scene/plant.wgsl", "scene/plant.wgsl", 4),
        (
            Family::Village,
            "buildings/village.wgsl",
            "buildings/village.wgsl",
            7,
        ),
        (
            Family::Ocean,
            "scene/water-optics.wgsl",
            "scene/water-optics.wgsl",
            1,
        ),
        (
            Family::BreakerLip,
            "scene/breaker-lip.wgsl",
            "scene/breaker-lip.wgsl",
            1,
        ),
        (
            Family::Spray,
            "scene/spray-sprites.wgsl",
            "scene/spray-sprites.wgsl",
            1,
        ),
    ];

    /// Explicit local qualification fixture, never silently skipped or synthesized.
    /// Run with `FORGE_TIDEWATER_SHADER_FIXTURE=bundle004` and `--ignored`.
    #[test]
    #[ignore = "requires the pinned source bundle and installed Bevy 0.19.1 WGSL libraries"]
    fn derives_actual_source_shaders_with_real_bevy_imports_and_preserved_coverage() {
        let fixture = PathBuf::from(
            std::env::var("FORGE_TIDEWATER_SHADER_FIXTURE")
                .expect("set FORGE_TIDEWATER_SHADER_FIXTURE to the pinned bundle004"),
        );
        let cargo_home = std::env::var_os("CARGO_HOME").map_or_else(
            || PathBuf::from(std::env::var_os("USERPROFILE").unwrap()).join(".cargo"),
            PathBuf::from,
        );
        let registry = cargo_home.join("registry/src");
        let mut assets = Assets::<Shader>::default();
        let mut cache = ShaderCache::new(
            (),
            wgpu::Features::empty(),
            wgpu::DownlevelFlags::all(),
            load_cpu_shader,
        );
        for registry in fs::read_dir(registry).unwrap() {
            for entry in fs::read_dir(registry.unwrap().path()).unwrap() {
                let path = entry.unwrap().path();
                let name = path.file_name().unwrap().to_string_lossy();
                if name.starts_with("bevy_") && name.ends_with("-0.19.1") {
                    register_libraries(&path.join("src"), &mut assets, &mut cache);
                }
            }
        }
        for (family, vertex_path, fragment_path, kinds) in SOURCE_CASES {
            let vertex = fs::read_to_string(fixture.join(vertex_path)).unwrap();
            let fragment = fs::read_to_string(fixture.join(fragment_path)).unwrap();
            let derived = derive(family, &vertex, &fragment).unwrap();
            // Every source coverage/deformation function remains byte-exact.
            for source in [&vertex, &fragment] {
                assert_source_functions_unchanged(family, source, &derived);
            }
            assert_eq!(
                vertex,
                fs::read_to_string(fixture.join(vertex_path)).unwrap()
            );
            assert_eq!(
                fragment,
                fs::read_to_string(fixture.join(fragment_path)).unwrap()
            );
            if family == Family::Plant {
                assert_eq!(
                    derived.vertex,
                    format!("{}\n{PACKET}", lighting_imports(&vertex, 4).unwrap())
                );
            }
            assert_late_world(family, &vertex, &fragment, &derived);
            for kind in 0..kinds {
                let definitions = definitions(family, kind);
                for source in [&derived.vertex, &derived.fragment] {
                    let shader =
                        Shader::from_wgsl(source.clone(), format!("aux-{family:?}-{kind}.wgsl"));
                    let handle = assets.add(shader.clone());
                    cache.set_shader(handle.id(), shader);
                    cache
                        .get(0, handle.id(), &definitions)
                        .unwrap_or_else(|e| panic!("{family:?} kind {kind}: {e:?}"));
                }
            }
        }
        let stand = include_str!("../tidewater_stall.wgsl");
        let derived = derive(Family::FishStand, stand, stand).unwrap();
        assert!(
            derived
                .vertex
                .contains("get_previous_world_from_local(input.instance)")
        );
        for source in [derived.vertex, derived.fragment] {
            let shader = Shader::from_wgsl(source, "aux-fish-stand.wgsl");
            let handle = assets.add(shader.clone());
            cache.set_shader(handle.id(), shader);
            cache
                .get(0, handle.id(), &definitions(Family::FishStand, 0))
                .unwrap();
        }
        validate_native_ocean(&fixture, &mut assets, &mut cache);
    }
}
