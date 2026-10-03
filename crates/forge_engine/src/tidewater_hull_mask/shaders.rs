// SPDX-License-Identifier: MIT
// Tidewater WaterMaterial.js 1438b1ab, WATER_HULL branch.
//! A strict seam inside shared shadeWater keeps beauty and auxiliary coverage equal.
pub fn water_shader_source(source: &str) -> Result<String, String> {
    const SEAM: &str = "let screenUV = in.pixel * frame.invResolution;";
    if source.matches(SEAM).count() != 1 || source.contains("var waterHullMask:") {
        return Err(
            "Verified Tidewater hull water seam is missing, duplicated or already patched".into(),
        );
    }
    let discard = r"
    // RenderWorld publishes active only after the current dedicated mask draw.
    // Same thresholds and front-face rule as the pinned WATER_HULL branch.
    if (hullReadiness.x > 0.5 && in.front) {
        let mSize = vec2f(textureDimensions(waterHullMask));
        let hullDist = textureLoad(waterHullMask,
            vec2i(clamp(screenUV, vec2f(0.0), vec2f(0.9999)) * mSize), 0).x;
        if (hullDist > 0.01 && length(pos - frame.cameraPos) > hullDist - 0.02) {
            discard;
        }
    }
";
    Ok(format!(
        "{}\n@group(#{{MATERIAL_BIND_GROUP}}) @binding(54) var waterHullMask: texture_2d<f32>;\n@group(#{{MATERIAL_BIND_GROUP}}) @binding(55) var<storage,read> hullReadiness: vec4f;\n",
        source.replacen(SEAM, &format!("{SEAM}\n{discard}"), 1)
    ))
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn missing_duplicate_and_prepatched_water_are_rejected() {
        let seam = "let screenUV = in.pixel * frame.invResolution;";
        assert!(water_shader_source("").is_err());
        assert!(water_shader_source(&seam.repeat(2)).is_err());
        let patched = water_shader_source(seam).unwrap();
        assert!(water_shader_source(&patched).is_err());
    }
    #[test]
    fn source_discard_precedes_water_depth_and_preserves_front_face_rule() {
        let source = "let screenUV = in.pixel * frame.invResolution;\nlet sceneDepthC = depth;";
        let patched = water_shader_source(source).unwrap();
        assert!(patched.find("discard;").unwrap() < patched.find("sceneDepthC").unwrap());
        assert!(patched.contains("if (hullReadiness.x > 0.5 && in.front)"));
        assert!(
            patched.contains("hullDist > 0.01 && length(pos - frame.cameraPos) > hullDist - 0.02")
        );
        assert!(patched.contains("@binding(54) var waterHullMask: texture_2d<f32>"));
    }
}
