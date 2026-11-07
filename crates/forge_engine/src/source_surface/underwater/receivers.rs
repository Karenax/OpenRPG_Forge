//! Existing material-side water-light receivers, shared by all scene consumers.
// SPDX-License-Identifier: MIT
// Transferred from tidewater_caustics.rs; upstream 1438b1ab, copyright 2026 DRG Software Solutions LLC.
// See third_party/tidewater/LICENSE. Shader declarations and profile guards are preserved.
const SAMPLE_SHADER: &str = include_str!("receivers/terrain_sample.wgsl");
const VILLAGE_LITE_SHADER: &str = include_str!("receivers/surface_lite.wgsl");
const LIGHT_MAP_LOOKUP: &str = include_str!("receivers/light_map_lookup.wgsl");
const UNDERWATER_SHADOW: &str = include_str!("receivers/shadow_position.wgsl");

/// Extend a verified source-lighting terrain shader with the material-side
/// underwater direct and ambient terms. Earlier diagnostic shaders without
/// source lighting are intentionally left untouched.
pub fn terrain_shader_source(source: &str) -> Result<String, String> {
    const MARKER: &str = "+sourceEnvironmentLight(s,N,V)+s.emissive;";
    if !source.contains("fn sourceSceneLight(") {
        return Ok(source.to_owned());
    }
    if source.matches(MARKER).count() != 1 || source.contains("fn terrainUnderwaterDirect(") {
        return Err("Unsupported source terrain lighting shader".into());
    }
    let source = refract_shadow_receiver(source)?.replace(
        MARKER,
        "*terrainUnderwaterDirect(P,N)+sourceEnvironmentLight(s,N,V)*terrainUnderwaterAmbient(P)+s.emissive;",
    );
    // Tidewater's Shader.js prepends this directive to its WGSL modules. Its
    // materials sample textures and take derivatives in data-dependent surface
    // branches; without it browser WebGPU rejects the shader and Studio's
    // rendering app exits before terrain picking can run.
    let sample = terrain_sample_source();
    Ok(format!(
        "diagnostic( off, derivative_uniformity );\n{source}\n{sample}"
    ))
}

/// The pinned scene gives buildings the `lite` underwater mode: attenuate
/// direct and ambient light without photon-map caustics. Keep emissive separate.
pub fn surface_shader_source(source: &str) -> Result<String, String> {
    const MARKER: &str = "+sourceEnvironmentLight(s,N,V)+s.emissive;";
    if !source.contains("fn sourceSceneLight(") {
        return Ok(source.to_owned());
    }
    if source.matches(MARKER).count() != 1 || source.contains("fn villageUnderwaterDirect(") {
        return Err("Unsupported source surface lighting shader".into());
    }
    let source = refract_shadow_receiver(source)?.replace(
        MARKER,
        "*villageUnderwaterDirect(P)+sourceEnvironmentLight(s,N,V)*villageUnderwaterAmbient(P)+s.emissive;",
    );
    Ok(format!("{source}\n{}", surface_sample_source()))
}

/// Adapt only the scene-lighting lookup, never the shadow helper declaration,
/// hill/cloud modulation or the original receiver used for surface lighting.
fn refract_shadow_receiver(source: &str) -> Result<String, String> {
    const CALL: &str = "*sunShadow(P,N,pixel)";
    if source.matches(CALL).count() != 1 {
        return Err("Source scene shadow-position call drift".into());
    }
    Ok(source.replace(
        CALL,
        "*sunShadow(underwaterShadowPosition(P,pixel),N,pixel)",
    ))
}

fn terrain_sample_source() -> String {
    let lookup = LIGHT_MAP_LOOKUP
        .replace("UW_SEA_LEVEL", "causticsParams.tiles_m.z")
        .replace("uwWaves", "causticsWaves")
        .replace("uwLevel", "causticsLevel")
        .replace("uwLinear", "causticsRepeat");
    let shadow = UNDERWATER_SHADOW
        .replace("UW_SHADOW_PARAMS", "causticsParams")
        .replace("UW_SHADOW_WAVES", "true");
    format!("{SAMPLE_SHADER}\n{lookup}\n{shadow}")
}
fn surface_sample_source() -> String {
    let lookup = LIGHT_MAP_LOOKUP
        .replace(
            "        if (waves) { a = textureSampleLevel(uwWaves, uwLinear, uv, layer, 0.0); }",
            "",
        )
        .replace("UW_SEA_LEVEL", "villageUnderwaterParams.tiles_m.z")
        .replace("uwLevel", "villageOceanLevel")
        .replace("uwLinear", "villageOceanRepeat");
    let shadow = UNDERWATER_SHADOW
        .replace("UW_SHADOW_PARAMS", "villageUnderwaterParams")
        .replace("UW_SHADOW_WAVES", "false");
    format!("{VILLAGE_LITE_SHADER}\n{lookup}\n{shadow}")
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn material_sampler_parses_and_validates_for_gpu() {
        let source = terrain_sample_source().replace("#{MATERIAL_BIND_GROUP}", "0");
        let source = format!(
            "{source}\n@fragment fn fragment() -> @location(0) vec4f {{\n\
             return vec4f(causticsSampleBaked(vec3f(0.0, -2.0, 0.0), 2.0, \
             vec2f(0.0), 0.0, vec2f(0.01), vec2f(0.01), 1.0), 1.0);\n}}"
        );
        let shader = naga::front::wgsl::parse_str(&source).unwrap();
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::all(),
        )
        .validate(&shader)
        .unwrap();
    }

    #[test]
    fn terrain_lighting_patch_keeps_emissive_outside_water_attenuation() {
        let shader = "fn sourceSceneLight(s:LightingSurface,V:vec3f,P:vec3f,pixel:vec2f)->vec3f { return sourceDirectLight(s)*sunShadow(P,N,pixel)+sourceEnvironmentLight(s,N,V)+s.emissive; }";
        let patched = terrain_shader_source(shader).unwrap();
        assert!(patched.starts_with("diagnostic( off, derivative_uniformity );\n"));
        assert!(patched.contains(
            "*sunShadow(underwaterShadowPosition(P,pixel),N,pixel)*terrainUnderwaterDirect(P,N)"
        ));
        assert!(
            patched
                .contains("sourceEnvironmentLight(s,N,V)*terrainUnderwaterAmbient(P)+s.emissive")
        );
        assert!(terrain_shader_source(&patched).is_err());
    }

    #[test]
    fn village_lite_shader_parses_and_keeps_emissive_unattenuated() {
        let source = surface_sample_source().replace("#{MATERIAL_BIND_GROUP}", "0");
        let source = format!(
            "{source}\n@fragment fn fragment() -> @location(0) vec4f {{\n\
             let P = vec3f(0.0, -2.0, 0.0);\n\
             return vec4f(villageUnderwaterDirect(P) + villageUnderwaterAmbient(P), 1.0);\n}}"
        );
        let shader = naga::front::wgsl::parse_str(&source).unwrap();
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::all(),
        )
        .validate(&shader)
        .unwrap();
        let scene = "fn sourceSceneLight(s:LightingSurface,V:vec3f,P:vec3f,pixel:vec2f)->vec3f { return sourceDirectLight(s)*sunShadow(P,N,pixel)+sourceEnvironmentLight(s,N,V)+s.emissive; }";
        let patched = surface_shader_source(scene).unwrap();
        assert!(patched.contains(
            "*sunShadow(underwaterShadowPosition(P,pixel),N,pixel)*villageUnderwaterDirect(P)"
        ));
        assert!(
            patched
                .contains("sourceEnvironmentLight(s,N,V)*villageUnderwaterAmbient(P)+s.emissive")
        );
        assert!(surface_shader_source(&patched).is_err());
    }

    #[test]
    fn shadow_entry_patch_rejects_missing_or_duplicate_calls() {
        assert!(refract_shadow_receiver("fn sourceSceneLight() {}").is_err());
        assert!(refract_shadow_receiver("*sunShadow(P,N,pixel)*sunShadow(P,N,pixel)").is_err());
        let source = "terrainSunShadowAt(P)*cloudsShadow(P.xz)*sunShadow(P,N,pixel)";
        let patched = refract_shadow_receiver(source).unwrap();
        assert!(patched.starts_with("terrainSunShadowAt(P)*cloudsShadow(P.xz)"));
        assert!(patched.ends_with("*sunShadow(underwaterShadowPosition(P,pixel),N,pixel)"));
    }
}
