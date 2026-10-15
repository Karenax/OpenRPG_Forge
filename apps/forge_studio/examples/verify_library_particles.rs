//! Uses a governed external fixture Vault; never embeds purchased data.
use forge_core::{ContentHash, bytes_sha256};
use forge_studio_frontend::{
    asset_library_native::Library,
    asset_library_protocol::{
        LibraryParticleRequest, LibraryQuery, LibraryRequest, LibraryResponse,
    },
};
fn run() -> Result<(), String> {
    let args: Vec<String> = std::env::args().skip(1).collect();
    if args.len() != 5 {
        return Err("vault_root id carrier_sha256 manifest_sha256 package_sha256".into());
    }
    let mut library = Library::new(std::path::Path::new(&args[0]))?;
    let LibraryResponse::Page { page } = library.execute(LibraryRequest::Search {
        query: LibraryQuery::default(),
    })?
    else {
        return Err("Library page absent".into());
    };
    let hash = |index: usize| ContentHash::parse(args[index].as_str()).map_err(|e| e.to_string());
    let request = LibraryParticleRequest {
        schema_version: 1,
        id: args[1].clone(),
        revision: page.revision,
        carrier_sha256: hash(2)?,
        manifest_sha256: hash(3)?,
        package_sha256: hash(4)?,
    };
    let wire = library.read_particle_package(request.clone())?;
    let mut stale = request.clone();
    stale.revision = "stale".into();
    if library.read_particle_package(stale).is_ok() {
        return Err("Stale revision accepted".into());
    }
    for key in 0..3 {
        let mut wrong = request.clone();
        let other = bytes_sha256(b"wrong package pin");
        match key {
            0 => wrong.carrier_sha256 = other,
            1 => wrong.manifest_sha256 = other,
            _ => wrong.package_sha256 = other,
        }
        if library.read_particle_package(wrong).is_ok() {
            return Err("Wrong pin accepted".into());
        }
    }
    println!(
        "{}",
        serde_json::json!({"read_bytes":wire.len(),"wire_sha256":bytes_sha256(&wire),
        "stale_revision_and_three_wrong_pins_refused":true,"renderer_executed":false})
    );
    Ok(())
}
fn main() {
    if let Err(error) = run() {
        eprintln!("{error}");
        std::process::exit(1);
    }
}
