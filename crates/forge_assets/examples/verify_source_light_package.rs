//! Verify a portable light package after extraction, without the source clone.
use forge_assets::source_local_lights::{
    SourceLocalLightsPackage, ownership::SourceLightOwnership,
};
use forge_core::ContentHash;
use std::{error::Error, fs::File, io::Read, path::Path};

fn bounded(path: &Path, maximum: usize) -> Result<Vec<u8>, Box<dyn Error>> {
    let limit = u64::try_from(maximum)?
        .checked_add(1)
        .ok_or("byte limit overflow")?;
    let mut bytes = Vec::new();
    File::open(path)?.take(limit).read_to_end(&mut bytes)?;
    if bytes.len() > maximum {
        return Err(format!("input exceeds byte limit: {}", path.display()).into());
    }
    Ok(bytes)
}

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 3 {
        return Err(
            "Usage: verify_source_light_package PACKAGE_ROOT MANIFEST_SHA256 FRESH_REPORT".into(),
        );
    }
    let root = Path::new(&args[0]).canonicalize()?;
    let report = Path::new(&args[2]);
    if report.exists() {
        return Err("report exists; use a fresh output path".into());
    }
    let expected: ContentHash = args[1].parse()?;
    let manifest_path = root.join("manifest.json").canonicalize()?;
    if !manifest_path.starts_with(&root) {
        return Err("manifest resolves outside the selected package root".into());
    }
    let manifest = bounded(&manifest_path, 2 * 1024 * 1024)?;
    let package = SourceLocalLightsPackage::load(&manifest, expected, |name, maximum| {
        let path = root
            .join(name)
            .canonicalize()
            .map_err(|error| error.to_string())?;
        if !path.starts_with(&root) {
            return Err(format!("package member resolves outside its root: {name}"));
        }
        bounded(&path, maximum).map_err(|error| error.to_string())
    })?;
    let descriptor = package.descriptor();
    let document = descriptor.document();
    let phases: Vec<_> = document
        .lights
        .iter()
        .map(|light| format!("{:016x}", light.phase.to_bits()))
        .collect();
    let ownership = package
        .acquisition()
        .get("editable_ownership")
        .map(|_| SourceLightOwnership::from_package(&package))
        .transpose()?;
    let ownership_evidence = ownership.as_ref().map(|index| {
        let source_ids: std::collections::BTreeSet<_> = index
            .records()
            .iter()
            .filter_map(|row| row.source_binding.source_object_id.as_deref())
            .collect();
        let selections: std::collections::BTreeMap<_, _> = source_ids
            .into_iter()
            .map(|source| (source, index.selection_for_source_object(source)))
            .collect();
        serde_json::json!({
            "validated_source_records":index.records().len(),
            "unresolved_light_ids":index.unresolved_light_ids(),
            "source_identity_selections":selections,
            "catalogue_binding_executed":false,"studio_binding_executed":false
        })
    });
    let output = serde_json::json!({
        "schema":"forge.source-local-lights.package-verification","version":1,
        "manifest_sha256":package.receipt_hash(),"descriptor_sha256":descriptor.content_hash(),
        "source_commit":document.source_commit,"emitters":document.lights.len(),
        "registration_sets":document.sets,"source_files_verified":document.source_inputs.len(),
        "payload_files_verified":package.file_count(),"phase_f64_bits_hex":phases,
        "original_license_verified":true,"acquisition_envelope_verified":true,
        "declaration_is_execution_attestation":false,
        "source_ownership":ownership_evidence,
        "runtime_import_executed":false,"gpu_executed":false,
        "renderer_parity":false,"studio_qualified":false
    });
    let bytes = serde_json::to_vec_pretty(&output)?;
    std::io::Write::write_all(&mut File::create_new(report)?, &bytes)?;
    println!(
        "{}",
        serde_json::to_string(&serde_json::json!({
            "emitters":document.lights.len(),"payload_files_verified":package.file_count(),
            "report":report,"runtime_import_executed":false
        }))?
    );
    Ok(())
}
