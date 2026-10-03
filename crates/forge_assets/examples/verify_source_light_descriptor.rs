//! Validate an acquired descriptor and its original source bytes, without GPU.
use forge_assets::source_local_lights::SourceLocalLights;
use forge_core::ContentHash;
use std::{collections::BTreeMap, error::Error, fs::File, io::Read, path::Path};

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
    if args.len() != 4 {
        return Err("Usage: verify_source_light_descriptor DESCRIPTOR SHA256 ORIGINAL_SOURCE_ROOT FRESH_REPORT".into());
    }
    let report = Path::new(&args[3]);
    if report.exists() {
        return Err("report exists; use a fresh output path".into());
    }
    let expected: ContentHash = args[1].parse()?;
    let descriptor =
        SourceLocalLights::load(&bounded(Path::new(&args[0]), 8 * 1024 * 1024)?, expected)?;
    let source_root = Path::new(&args[2]).canonicalize()?;
    let mut originals = BTreeMap::new();
    for input in &descriptor.document().source_inputs {
        let path = source_root.join(&input.path).canonicalize()?;
        if !path.starts_with(&source_root) || input.bytes > 32 * 1024 * 1024 {
            return Err(format!("source outside closure or size limit: {}", input.path).into());
        }
        originals.insert(input.path.clone(), bounded(&path, input.bytes)?);
    }
    descriptor.verify_source_inputs(|path| originals.get(path).map(Vec::as_slice))?;
    let document = descriptor.document();
    let phase_bits: Vec<_> = document
        .lights
        .iter()
        .map(|light| format!("{:016x}", light.phase.to_bits()))
        .collect();
    let output = serde_json::json!({
        "schema":"forge.source-local-lights.descriptor-verification", "version":1,
        "descriptor_sha256":descriptor.content_hash(), "source_commit":document.source_commit,
        "emitters":document.lights.len(), "registration_sets":document.sets,
        "source_files_verified":originals.len(), "phase_f64_bits_hex":phase_bits,
        "declared_full_registered_inventory":document.full_application_inventory,
        "declaration_is_execution_attestation":false,
        "runtime_import_executed":false,"gpu_executed":false,
        "renderer_parity":false,"studio_qualified":false
    });
    let bytes = serde_json::to_vec_pretty(&output)?;
    let mut file = File::create_new(report)?;
    std::io::Write::write_all(&mut file, &bytes)?;
    println!(
        "{}",
        serde_json::to_string(&serde_json::json!({
            "emitters":document.lights.len(),"source_files_verified":originals.len(),
            "report":report,"gpu_executed":false,"runtime_import_executed":false
        }))?
    );
    Ok(())
}
