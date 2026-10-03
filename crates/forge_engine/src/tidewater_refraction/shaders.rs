// SPDX-License-Identifier: MIT
// Adapted from Tidewater RefractionPass.js / WaterMaterial.js at 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
//! Strict seams in the verified source shaders, preserving their lighting.

const DECLARATION: &str =
    "\n@group(#{MATERIAL_BIND_GROUP}) @binding(200) var<uniform> refractionCapture: vec4f;\n";

pub(super) fn terrain(source: &str) -> Result<String, String> {
    let source = patch(
        source,
        "fn fragment(vs:VertexOutput)->@location(0) vec4f {",
        "vs.world_position.y",
    )?;
    // The half-resolution capture still contains lit RGB. Only the photon
    // sampling loses dispersion, as in UnderwaterLighting.js REFRACTION_CLIP.
    replace_once(
        &source,
        "let caustics = causticsSampleBaked(P, depth, waves.slope, waves.foam, dx, dy, waves.detailK);",
        "let caustics = causticsSampleBakedMono(P, depth, waves.slope, waves.foam, dx, dy, waves.detailK);",
    )
}

pub(super) fn village(source: &str) -> Result<String, String> {
    patch(
        source,
        "fn fragment(vs:Output,@builtin(front_facing) front:bool)->@location(0) vec4f {",
        "vs.P.y",
    )
}

fn patch(source: &str, entry: &str, height: &str) -> Result<String, String> {
    if source.contains("var<uniform> refractionCapture:") {
        return Err("Refraction capture shader already patched".into());
    }
    let clipped = format!("{entry}\n    if ({height} > refractionCapture.x + 0.4) {{ discard; }}");
    Ok(format!(
        "{}{DECLARATION}",
        replace_once(source, entry, &clipped)?
    ))
}

pub fn water(source: &str) -> Result<String, String> {
    const ENTRY: &str = "\t\tvar found = false;";
    if source.contains("var waterRefrColor:") {
        return Err("Refraction water shader already patched".into());
    }
    let capture = r"
        if (refractionReady.x > 0.5) {
            let uvRc = clamp(uvR, vec2f(0.001), vec2f(0.999));
            let rc = textureSampleLevel(waterRefrColor, smpLinearClamp, uvRc, 0.0);
            let rSize = vec2f(textureDimensions(waterRefrDepth));
            let rd = textureLoad(waterRefrDepth, vec2i(min(uvRc * rSize, rSize - 1.0)), 0);
            if (rc.a > 0.5 && rd > 0.0) {
                sceneCol = rc.rgb / rc.a;
                uvF = uvRc;
                dR = rd;
                found = true;
            }
        }
";
    let source = replace_once(source, ENTRY, &format!("{ENTRY}\n{capture}"))?;
    Ok(format!(
        "{source}\n@group(#{{MATERIAL_BIND_GROUP}}) @binding(51) var waterRefrColor: texture_2d<f32>;\n\
         @group(#{{MATERIAL_BIND_GROUP}}) @binding(52) var waterRefrDepth: texture_depth_2d;\n\
         @group(#{{MATERIAL_BIND_GROUP}}) @binding(53) var<uniform> refractionReady: vec4f;\n"
    ))
}

fn replace_once(source: &str, seam: &str, replacement: &str) -> Result<String, String> {
    if source.matches(seam).count() != 1 {
        return Err("Verified Tidewater refraction shader seam missing or duplicated".into());
    }
    Ok(source.replacen(seam, replacement, 1))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn missing_duplicate_or_prepatched_capture_fails_closed() {
        let entry =
            "fn fragment(vs:Output,@builtin(front_facing) front:bool)->@location(0) vec4f {";
        assert!(village("").is_err());
        assert!(village(&format!("{entry}{entry}")).is_err());
        let shader = village(entry).unwrap();
        assert!(village(&shader).is_err());
        assert!(shader.contains("vs.P.y > refractionCapture.x + 0.4"));
    }

    #[test]
    fn capture_preserves_lit_color_and_opaque_fallback() {
        let source = "\t\tvar found = false;\nif (!found) { sceneCol = opaqueColor; }";
        let patched = water(source).unwrap();
        assert!(patched.contains("sceneCol = rc.rgb / rc.a"));
        assert!(patched.contains("rc.a > 0.5 && rd > 0.0"));
        assert!(patched.contains("if (!found) { sceneCol = opaqueColor; }"));
        assert!(water(&patched).is_err());
    }
}
