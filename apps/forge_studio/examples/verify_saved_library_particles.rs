//! Exact saved-package lookup in an external governed Vault fixture.
use forge_core::{ContentHash, bytes_sha256};
use forge_studio_frontend::{
    asset_library_native::Library,
    asset_library_protocol::{LibrarySavedParticleRequest, vault_asset_id},
};
fn run() -> Result<(), String> {
    let args: Vec<String> = std::env::args().skip(1).collect();
    if args.len() != 5 {
        return Err("vault_root id carrier manifest package".into());
    }
    let mut library = Library::new(std::path::Path::new(&args[0]))?;
    let hash = |i: usize| ContentHash::parse(args[i].as_str()).map_err(|e| e.to_string());
    let request = LibrarySavedParticleRequest {
        schema_version: 1,
        asset_id: vault_asset_id(&args[1]),
        carrier_sha256: hash(2)?,
        manifest_sha256: hash(3)?,
        package_sha256: hash(4)?,
    };
    let wire = library.read_saved_particle_package(&request)?;
    for lane in 0..3 {
        let mut bad = request.clone();
        let wrong = bytes_sha256(b"wrong saved particle pin");
        match lane {
            0 => bad.carrier_sha256 = wrong,
            1 => bad.manifest_sha256 = wrong,
            _ => bad.package_sha256 = wrong,
        }
        if library.read_saved_particle_package(&bad).is_ok() {
            return Err("Wrong saved pin accepted".into());
        }
    }
    println!(
        "{}",
        serde_json::json!({"saved_package_sha256":bytes_sha256(&wire),"bytes":wire.len(),
        "three_wrong_saved_pins_refused":true,"GPU_qualified":false})
    );
    Ok(())
}
fn main() {
    if let Err(error) = run() {
        eprintln!("{error}");
        std::process::exit(1);
    }
}
