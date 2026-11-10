//! Verify a pinned decal carrier without admitting it to the renderer.
use forge_assets::source_decal_glb::read_source_decal_glb;
use forge_core::{ContentHash, bytes_sha256};
use std::{
    error::Error,
    fs::File,
    io::{Read, Write},
    path::Path,
};

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 3 {
        return Err("Usage: verify_source_decal_carrier GLB SHA256 FRESH_REPORT".into());
    }
    let expected: ContentHash = args[1].parse()?;
    let mut raw = Vec::new();
    File::open(Path::new(&args[0]))?
        .take(512 * 1024 * 1024 + 1)
        .read_to_end(&mut raw)?;
    if raw.len() > 512 * 1024 * 1024 || bytes_sha256(&raw) != expected {
        return Err("carrier budget or SHA differs".into());
    }
    let carrier = read_source_decal_glb(&raw)?.ok_or("source decal declaration absent")?;
    let report = serde_json::json!({"schema":"forge.source-decal.carrier-reopen","version":1,"carrier_sha256":expected,"materials":carrier.materials.len(),"buffer_views":carrier.views.len(),"renderer_registration_executed":false,"GPU_executed":false,"Studio_qualified":false});
    File::create_new(&args[2])?.write_all(&serde_json::to_vec_pretty(&report)?)?;
    println!("{}", serde_json::to_string(&report)?);
    Ok(())
}
