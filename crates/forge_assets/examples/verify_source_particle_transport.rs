//! Verify carrier-bound particle transport without registering renderer assets.
use forge_assets::source_particle_transport::SourceParticleTransport;
use forge_core::{ContentHash, bytes_sha256};
use std::{
    error::Error,
    fs::File,
    io::{Read, Write},
    path::Path,
};

fn bounded(path: &Path, limit: usize) -> Result<Vec<u8>, Box<dyn Error>> {
    let mut bytes = Vec::new();
    File::open(path)?
        .take(u64::try_from(limit)? + 1)
        .read_to_end(&mut bytes)?;
    if bytes.len() > limit {
        return Err("source particle file exceeds budget".into());
    }
    Ok(bytes)
}

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 4 {
        return Err("Usage: verify_source_particle_transport PACKAGE_ROOT MANIFEST_SHA256 CARRIER_GLB FRESH_REPORT".into());
    }
    let root = Path::new(&args[0]).canonicalize()?;
    let report = Path::new(&args[3]);
    if report.exists() {
        return Err("report exists; use fresh output".into());
    }
    let pin: ContentHash = args[1].parse()?;
    let manifest_path = root.join("manifest.json").canonicalize()?;
    if !manifest_path.starts_with(&root) {
        return Err("manifest outside package root".into());
    }
    let manifest = bounded(&manifest_path, 1024 * 1024)?;
    let carrier = bounded(Path::new(&args[2]), 512 * 1024 * 1024)?;
    let mut resolved = 0;
    let result = SourceParticleTransport::decode_for_carrier(&manifest, pin, &carrier, |name| {
        resolved += 1;
        let path = root.join(name).canonicalize().map_err(|e| e.to_string())?;
        if !path.starts_with(&root) {
            return Err("particle resource outside package root".into());
        }
        bounded(&path, 64 * 1024 * 1024).map_err(|e| e.to_string())
    });
    let error = result.as_ref().err().cloned();
    let output = serde_json::json!({
        "schema": "forge.source-particle.transport-verification", "version": 1,
        "manifest_sha256": bytes_sha256(&manifest), "expected_manifest_sha256": pin,
        "carrier_sha256": bytes_sha256(&carrier),
        "transport_admitted": result.is_ok(), "refusal": error,
        "resource_resolver_calls": resolved,
        "resource_files_verified": result.as_ref().ok().map(|v| v.resources.len()),
        "owners_verified": result.as_ref().ok().map(|v| v.contract.owners.len()),
        "renderer_registration_executed": false, "gpu_executed": false,
        "studio_qualified": false, "vertex_order_qualified": false
    });
    File::create_new(report)?.write_all(&serde_json::to_vec_pretty(&output)?)?;
    println!("{}", serde_json::to_string(&output)?);
    if let Some(error) = error {
        return Err(error.into());
    }
    Ok(())
}
