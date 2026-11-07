// SPDX-License-Identifier: MIT
// Reprojection follows Tidewater MeshShader.js at 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
//! Preserve the installed fish geometry, skin ABI and source LOD coverage.
use super::{closing, fragments, replace, require, through};

const LEGACY_FRAME: &str = "frame.frameIndex=settings.frameIndex;";
const SHARED_FRAME: &str =
    "frame.frameIndex=select(settings.frameIndex,fishClock.y,fishClock.z!=0u);";
const SHARED_TIME: &str =
    "frame.time=select(settings.info.w,bitcast<f32>(fishClock.x),fishClock.z!=0u);";
const CLOCK_BINDING: &str = "@binding(56) var<storage,read> fishClock:vec4u;";

fn frame_clock(source: &str, body: &str) -> Result<(), &'static str> {
    match (
        body.matches(LEGACY_FRAME).count(),
        body.matches(SHARED_FRAME).count(),
    ) {
        (1, 0) if !body.contains("fishClock") => Ok(()),
        (0, 1) => {
            require(source, CLOCK_BINDING, 1)?;
            require(body, SHARED_TIME, 1)
        }
        _ => Err("unrecognized or duplicate fish frame clock"),
    }
}

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
        if source.contains("source_previous_P")
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
        extended.insert_str(end, "\n @location(5) source_previous_P:vec3f,\n");
        Ok(extended)
    };
    let vertex = replace(
        &extend(vertex)?,
        "return output;",
        "output.source_previous_P=(mesh_functions::get_previous_world_from_local(input.instance)*vec4f(input.position,1.0)).xyz;\n return output;",
        1,
    )?;
    let fragment = fragments(
        &extend(fragment)?,
        "fn fragment(vs:Output)->@location(0) vec4f {",
        1,
        |body| {
            frame_clock(fragment, body)?;
            require(body, "let surface=fishSurface(input);", 1)?;
            let prefix = through(
                body,
                "if (!lodFadeVisible(input.pixel,settings.lod.x,settings.lod.y>0.5)) {discard;}",
            )?;
            if body[prefix.len()..].contains("discard") {
                return Err("unrecognized fish post-surface coverage");
            }
            Ok(format!(
                "{prefix}\nreturn source_auxiliary_world(vs.P,vs.source_previous_P,1.0,1.0,vec4f(0.0));\n"
            ))
        },
    )?;
    Ok((vertex, fragment))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn shared_clock_retains_full_uint_frame_and_exact_time_bits_or_legacy_settings() {
        let body = format!("{SHARED_TIME}\n{SHARED_FRAME}");
        let source = format!("{CLOCK_BINDING}\n{body}");
        assert!(frame_clock(&source, &body).is_ok());
        assert!(frame_clock(LEGACY_FRAME, LEGACY_FRAME).is_ok());
        assert!(frame_clock(&source.replace("fishClock:vec4u", "fishClock:vec4f"), &body).is_err());
        assert!(frame_clock(&source, &body.replace("fishClock.z!=0u", "true")).is_err());
        assert!(frame_clock(&source, &body.replace("bitcast<f32>", "f32")).is_err());
    }

    #[test]
    fn missing_duplicate_or_mixed_clock_never_accepts_an_ambiguous_mask_frame() {
        let body = format!("{SHARED_TIME}\n{SHARED_FRAME}");
        let source = format!("{CLOCK_BINDING}\n{body}");
        assert!(frame_clock(&source.replace(CLOCK_BINDING, ""), &body).is_err());
        assert!(frame_clock(&format!("{CLOCK_BINDING}\n{source}"), &body).is_err());
        for extra in [SHARED_FRAME, LEGACY_FRAME, SHARED_TIME] {
            assert!(frame_clock(&source, &format!("{body}\n{extra}")).is_err());
        }
        assert!(frame_clock(&source, &format!("{LEGACY_FRAME}\n{SHARED_TIME}")).is_err());
    }
}
