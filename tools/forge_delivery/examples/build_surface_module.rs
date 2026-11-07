//! Private no-vegetation qualification fork using the ordinary zone and package codecs.
//! The full-population module remains immutable and independently qualified.
use forge_core::{CommandId, Revision, bytes_sha256};
use forge_delivery::{
    manifest::{Descriptor, Kind, Rights},
    module_runtime, orpmod,
};
use forge_project::{
    module_package::ModuleFileRole,
    zone_authoring::session::{ZoneAction, ZoneEditSession, ZoneRequest},
};
use std::{fs, path::PathBuf};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let args: Vec<_> = std::env::args_os().skip(1).map(PathBuf::from).collect();
    let [input, output] = args.as_slice() else {
        return Err("INPUT_INSTALLED_MODULE OUTPUT_NEW_DIRECTORY".into());
    };
    if !input.is_absolute() || !output.is_absolute() || output.exists() {
        return Err("Qualification inputs must be absolute and output must be new".into());
    }
    forge_delivery::manifest::safe_chain(input)?;
    forge_delivery::manifest::safe_chain(output)?;
    let admitted = orpmod::validate_directory(input, false)?;
    if admitted.zones.len() != 1 || admitted.environments.len() != 1 {
        return Err("This qualification fork requires one admitted area and environment".into());
    }
    let loaded = module_runtime::load(input, admitted.zones[0].area_id, None)?;
    if loaded.environment()?.is_none() || loaded.zone.head().base.vegetation.is_none() {
        return Err("Input must prove its original environment and vegetation".into());
    }
    let mut terrain = loaded.zone.head().base.terrain.clone();
    terrain.revision = Revision::INITIAL;
    let mut zone = ZoneEditSession::from_initial_terrain(terrain)?;
    let mut corrections = loaded.zone.head().corrections.clone();
    if !corrections.density.is_empty()
        || corrections.species.is_some()
        || corrections.promotions.is_some()
        || corrections.imported_plant_offsets.is_some()
        || corrections.imported_plant_placements.is_some()
        || corrections.vegetation_exclusions.is_some()
    {
        return Err("Vegetation corrections need explicit review before this fork".into());
    }
    corrections.name = Some("Qualification des surfaces sans végétation".into());
    zone.execute(&ZoneRequest {
        command_id: CommandId::deterministic("gate7/surfaces/no-vegetation/v1"),
        expected_revision: zone.head().revision,
        expected_hash: zone.head().content_hash()?,
        action: ZoneAction::edit(corrections),
    })?;
    let archive = zone.archive_bytes()?;
    let mut module = admitted;
    module.module_version = "1.0.7".into();
    module.name = "Qualification des surfaces sans végétation".into();
    module.vegetation.clear();
    module.zones[0].archive_sha256 = bytes_sha256(&archive);
    module.zones[0].head_hash = zone.head().content_hash()?;
    module.zones[0].gameplay_sha256 = zone
        .head()
        .corrections
        .gameplay
        .as_deref()
        .map(forge_core::canonical_json_sha256)
        .transpose()?;
    let sources = zone.required_source_blobs()?;
    module.zones[0].source_blobs = sources.keys().copied().collect();
    module.files.retain(|file| {
        file.role != ModuleFileRole::Vegetation
            && !file.path.starts_with("plants/")
            && (file.role != ModuleFileRole::SourceBlob || sources.contains_key(&file.sha256))
    });
    module
        .provenance
        .license_paths
        .retain(|path| !path.starts_with("plants/"));
    module.provenance.source_revision =
        format!("gate7/no-vegetation/{}", module.zones[0].head_hash);
    module.provenance.source = format!(
        "Explicit private no-vegetation qualification fork of head {}; original terrain, environment and RPG retained; no original archive or timeline modified.",
        loaded.zone.head().content_hash()?
    );
    let source = output.join("source");
    fs::create_dir(output)?;
    fs::create_dir(&source)?;
    for file in &mut module.files {
        let target = source.join(&file.path);
        if let Some(parent) = target.parent() {
            fs::create_dir_all(parent)?;
        }
        if file.path == module.zones[0].archive_path {
            fs::write(&target, &archive)?;
            file.sha256 = bytes_sha256(&archive);
            file.bytes = u64::try_from(archive.len())?;
        } else {
            // Verify the ordinary admitted bytes before linking an immutable input.
            loaded.read_payload(&file.path, 64 * 1024 * 1024)?;
            fs::hard_link(loaded.package_root.join(&file.path), &target)?;
        }
    }
    fs::write(source.join("module.json"), module.to_bytes()?)?;
    let package = output.join("surfaces.orpmod");
    let (_, transport) = orpmod::pack(
        &source,
        Descriptor {
            schema: "forge.delivery.package.v1".into(),
            id: module.module_id.to_string(),
            label: module.name.clone(),
            version: module.module_version.clone(),
            kind: Kind::Module,
            platform: "any".into(),
            source_revision: module.provenance.source_revision.clone(),
            rights: Rights::PrivateLocalOnly,
            license_paths: module.provenance.license_paths.clone(),
            files: vec![],
            product: None,
        },
        &package,
        false,
    )?;
    let installed = module_runtime::load(
        &package,
        module.zones[0].area_id,
        Some(&output.join("cache")),
    )?;
    if installed.vegetation_imported()?.is_some()
        || installed.zone.head().effective()?.1.is_some()
        || installed.environment()?.is_none()
    {
        return Err("No-vegetation module failed ordinary admission".into());
    }
    println!(
        "{}",
        serde_json::json!({"package":package,"package_sha256":forge_delivery::file_hash(&package)?,
        "transport_manifest_sha256":transport,"area_id":module.zones[0].area_id,
        "head_hash":module.zones[0].head_hash,"archive_sha256":module.zones[0].archive_sha256,
        "payloads":module.files.len()+1,"source_blobs":sources.len(),"vegetation":false,
        "environment":true,"private_only":true,"native_qualified":false,"gate_closed":false})
    );
    Ok(())
}
