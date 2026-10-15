//! Admission of standalone source compute WGSL before shader/device mutation.
use bevy::{prelude::*, shader::ValidateShader};
use forge_core::{ContentHash, bytes_sha256};
use wgpu::naga;

/// Admit a pinned, standalone WGSL compute program. Bevy imports, shader defs,
/// overrides and optional device capabilities are outside this portable profile.
/// Rejection leaves shader assets unchanged; no device pipeline is requested.
/// Callers must still qualify the source binding ABI and actual device resources.
pub fn register_source_particle_compute_program(
    assets: &mut Assets<Shader>,
    source: &str,
    expected_sha256: ContentHash,
    entry_point: &str,
) -> Result<Handle<Shader>, String> {
    if bytes_sha256(source.as_bytes()) != expected_sha256 {
        return Err("Source compute program SHA-256 mismatch".into());
    }
    let module = naga::front::wgsl::parse_str(source)
        .map_err(|error| format!("Source compute WGSL parse refusal: {error}"))?;
    naga::valid::Validator::new(
        naga::valid::ValidationFlags::all(),
        naga::valid::Capabilities::empty(),
    )
    .validate(&module)
    .map_err(|error| format!("Source compute WGSL validation refusal: {error}"))?;
    let entry = module
        .entry_points
        .iter()
        .find(|entry| entry.name == entry_point && entry.stage == naga::ShaderStage::Compute)
        .ok_or_else(|| format!("Source compute entry point absent: {entry_point}"))?;
    let [x, y, z] = entry.workgroup_size;
    if !module.overrides.is_empty()
        || x == 0
        || y == 0
        || z == 0
        || x > 256
        || y > 256
        || z > 64
        || u64::from(x) * u64::from(y) * u64::from(z) > 256
    {
        return Err("Source compute workgroup exceeds the portable static profile".into());
    }
    let mut shader = Shader::from_wgsl(source.to_owned(), "admitted-source-compute");
    shader.validate_shader = ValidateShader::Enabled;
    Ok(assets.add(shader))
}

#[cfg(test)]
mod tests {
    use super::*;

    const VALID: &str = "@compute @workgroup_size(64) fn advance() {}";

    #[test]
    fn refusal_keeps_existing_shader_assets_and_entry_stage_is_checked() {
        let mut assets = Assets::<Shader>::default();
        let accepted = register_source_particle_compute_program(
            &mut assets,
            VALID,
            bytes_sha256(VALID.as_bytes()),
            "advance",
        )
        .unwrap();
        let fragment =
            "@fragment fn advance() -> @location(0) vec4<f32> { return vec4<f32>(1.0); }";
        let invalid = "@group(0) @binding(0) var<storage,read> x: u32; @compute @workgroup_size(1) fn advance() { x = 1u; }";
        let oversized = "@compute @workgroup_size(257) fn advance() {}";
        let imported = "#import some::library\n@compute @workgroup_size(1) fn advance() {}";
        let cases = [
            (VALID, bytes_sha256(b"changed"), "advance", "SHA-256"),
            (VALID, bytes_sha256(VALID.as_bytes()), "absent", "absent"),
            (
                fragment,
                bytes_sha256(fragment.as_bytes()),
                "advance",
                "absent",
            ),
            ("not WGSL", bytes_sha256(b"not WGSL"), "advance", "parse"),
            (
                invalid,
                bytes_sha256(invalid.as_bytes()),
                "advance",
                "validation",
            ),
            (
                oversized,
                bytes_sha256(oversized.as_bytes()),
                "advance",
                "workgroup",
            ),
            (
                imported,
                bytes_sha256(imported.as_bytes()),
                "advance",
                "parse",
            ),
        ];
        for (source, hash, entry, reason) in cases {
            let error = register_source_particle_compute_program(&mut assets, source, hash, entry)
                .unwrap_err();
            assert!(error.contains(reason), "{error}");
            assert_eq!(assets.len(), 1);
            assert!(assets.get(&accepted).is_some());
        }
        assert!(matches!(
            assets.get(&accepted).unwrap().validate_shader,
            ValidateShader::Enabled
        ));
    }
}
