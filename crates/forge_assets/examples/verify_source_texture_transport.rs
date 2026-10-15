//! Verify original source-image/native-mip transports before material assembly.
use forge_assets::{
    source_graph_bundle::SourceGraphTexture, source_particle_transport::SourceParticleBlob,
};
use forge_core::{ContentHash, bytes_sha256};
use serde::Deserialize;
use std::{
    error::Error,
    fs::File,
    io::{Read, Write},
    path::Path,
};

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Manifest {
    schema_version: u32,
    licence_ref: String,
    resources: Vec<SourceParticleBlob>,
    textures: Vec<SourceGraphTexture>,
}

fn bounded(path: &Path, limit: usize) -> Result<Vec<u8>, Box<dyn Error>> {
    let mut bytes = Vec::new();
    File::open(path)?
        .take(u64::try_from(limit)? + 1)
        .read_to_end(&mut bytes)?;
    if bytes.len() > limit {
        return Err("source texture file exceeds budget".into());
    }
    Ok(bytes)
}

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 3 {
        return Err(
            "Usage: verify_source_texture_transport PACKAGE_ROOT MANIFEST_SHA256 FRESH_REPORT"
                .into(),
        );
    }
    let root = Path::new(&args[0]).canonicalize()?;
    let pin: ContentHash = args[1].parse()?;
    let manifest_path = root.join("manifest.json").canonicalize()?;
    if !manifest_path.starts_with(&root) {
        return Err("manifest outside package root".into());
    }
    let wire = bounded(&manifest_path, 1024 * 1024)?;
    if bytes_sha256(&wire) != pin {
        return Err("texture manifest SHA differs".into());
    }
    let manifest: Manifest = serde_json::from_slice(&wire)?;
    if manifest.schema_version != 1
        || manifest.licence_ref.trim().is_empty()
        || manifest.textures.is_empty()
        || manifest.textures.len() > 10
        || manifest.resources.len() > 128
    {
        return Err("texture manifest schema, licence or budget invalid".into());
    }
    let mut resources = Vec::new();
    let mut total = 0usize;
    for resource in &manifest.resources {
        if resource.path.contains(['\\', ':'])
            || resource.path.starts_with('/')
            || resource
                .path
                .split('/')
                .any(|p| p.is_empty() || p == "." || p == "..")
            || resource.byte_length == 0
            || resource.byte_length > 64 * 1024 * 1024
        {
            return Err("texture resource path or size invalid".into());
        }
        total = total
            .checked_add(resource.byte_length)
            .ok_or("texture budget overflow")?;
        if total > 128 * 1024 * 1024 {
            return Err("texture total resource budget exceeded".into());
        }
        let path = root.join(&resource.path).canonicalize()?;
        if !path.starts_with(&root) {
            return Err("texture resource outside package root".into());
        }
        let bytes = bounded(&path, resource.byte_length)?;
        if bytes.len() != resource.byte_length || bytes_sha256(&bytes) != resource.sha256 {
            return Err(format!("texture resource pin differs: {}", resource.path).into());
        }
        resources.push(bytes);
    }
    let views: Vec<_> = resources.iter().map(Vec::as_slice).collect();
    for texture in &manifest.textures {
        texture
            .validate_integrity(&views)
            .map_err(|e| format!("texture integrity: {e:?}"))?;
    }
    let output = serde_json::json!({
        "schema":"forge.source-texture.transport-verification","version":1,
        "manifest_sha256":pin,"textures_verified":manifest.textures.len(),
        "resources_verified":resources.len(),"resource_bytes_verified":total,
        "native_mips_verified":manifest.textures.iter().filter_map(|t| t.imported_storage.as_ref())
            .map(|t| t.mips.len()).sum::<usize>(),
        "gpu_upload_executed":false,"material_assembled":false,"Studio_integrated":false
    });
    File::create_new(&args[2])?.write_all(&serde_json::to_vec_pretty(&output)?)?;
    println!("{}", serde_json::to_string(&output)?);
    Ok(())
}
