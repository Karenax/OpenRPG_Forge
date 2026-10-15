//! Private external resources only; no purchased programs are embedded here.
use forge_assets::{
    source_particle_package::SourceParticlePackage,
    source_particle_transport::SourceParticleTransport,
};
use forge_core::{ContentHash, bytes_sha256};
use std::{collections::BTreeMap, path::PathBuf};

fn run() -> Result<(), String> {
    let args: Vec<String> = std::env::args().skip(1).collect();
    if args.len() != 5 {
        return Err("manifest manifest_sha256 carrier resources_root output_package".into());
    }
    let read = |p: &std::path::Path, max: u64| -> Result<Vec<u8>, String> {
        if std::fs::metadata(p).map_err(|e| e.to_string())?.len() > max {
            return Err("Resource budget exceeded".into());
        }
        std::fs::read(p).map_err(|e| e.to_string())
    };
    let manifest = read(&PathBuf::from(&args[0]), 1024 * 1024)?;
    let expected = ContentHash::parse(&args[1]).map_err(|e| e.to_string())?;
    let carrier = read(&PathBuf::from(&args[2]), 512 * 1024 * 1024)?;
    let root = std::fs::canonicalize(&args[3]).map_err(|e| e.to_string())?;
    let resolved =
        SourceParticleTransport::decode_for_carrier(&manifest, expected, &carrier, |path| {
            let file = std::fs::canonicalize(root.join(path)).map_err(|e| e.to_string())?;
            if !file.starts_with(&root) {
                return Err("Resource leaves package root".into());
            }
            read(&file, 64 * 1024 * 1024)
        })?;
    let resources: BTreeMap<_, _> = resolved.resources;
    let package = SourceParticlePackage {
        carrier_sha256: bytes_sha256(&carrier),
        manifest_sha256: expected,
        manifest,
        resources,
    };
    let wire = package.encode()?;
    let decoded = SourceParticlePackage::decode_pinned(
        &wire,
        package.carrier_sha256,
        expected,
        bytes_sha256(&wire),
    )?;
    decoded.resolve_for_carrier(&carrier)?;
    std::fs::write(&args[4], &wire).map_err(|e| e.to_string())?;
    println!(
        "{}",
        serde_json::json!({"package_sha256":bytes_sha256(&wire),"package_bytes":wire.len(),
        "resources":decoded.resources.len(),"owners":resolved.contract.owners.len(),
        "carrier_sha256":package.carrier_sha256,"manifest_sha256":expected,"GPU_qualified":false})
    );
    Ok(())
}
fn main() {
    if let Err(error) = run() {
        eprintln!("{error}");
        std::process::exit(1);
    }
}
