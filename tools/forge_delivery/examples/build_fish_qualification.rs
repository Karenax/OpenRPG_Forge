//! Private, isolated fish qualification fork of a previously admitted module.
//! It never changes the source package or claims visual/distribution approval.
use forge_assets::{
    AssetCatalog, AssetKind, AssetRecord, AssetSource, source_fish_package::resource_index,
};
use forge_core::{
    AreaId, AssetId, CommandId, ContentHash, EntityId, Transform, Vec3, bytes_sha256,
};
use forge_delivery::{
    manifest::{self, Descriptor, Kind, Rights},
    module_runtime, orpmod,
};
use forge_project::{
    module_package::{
        MODULE_FISH_API_VERSION, ModuleFile, ModuleFilePin, ModuleFileRole, ModuleFishBinding,
        ModuleObjectAsset, ModuleObjectFiles, ModuleRights,
    },
    zone_authoring::{
        placements::{ManualPlacement, PlacementEdit},
        session::ZoneEditSession,
    },
};
use std::{collections::BTreeMap, fs, path::PathBuf};

const FISH_HASH: &str = "e578b44e93d18d79ec06c875b76dae005f71c17ecf1d0ff09aa3ecddd651330a";
const PREFIX: &str = "_forge-export/fish-qualification/profile";

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let arguments: Vec<_> = std::env::args_os().skip(1).collect();
    let [installed, area, transport, fish_root, output] = arguments.as_slice() else {
        return Err("INSTALLED_DIR AREA_ID TRANSPORT_SHA256 FISH_SOURCE_DIR NEW_OUTPUT_DIR".into());
    };
    let installed = PathBuf::from(installed);
    let fish_root = PathBuf::from(fish_root);
    let output = PathBuf::from(output);
    if !installed.is_absolute()
        || !fish_root.is_absolute()
        || !output.is_absolute()
        || output.exists()
    {
        return Err("Inputs must be absolute and the output must not exist".into());
    }
    for path in [&installed, &fish_root, &output] {
        manifest::safe_chain(path)?;
    }
    let area: AreaId = area.to_str().ok_or("Area is not Unicode")?.parse()?;
    let transport: ContentHash = transport
        .to_str()
        .ok_or("Transport pin is not Unicode")?
        .parse()?;
    let fish_hash: ContentHash = FISH_HASH.parse()?;
    let loaded = module_runtime::load(&installed, area, None)?;
    module_runtime::verify_transport_revision(&loaded, transport)?;
    if loaded.manifest.provenance.rights != ModuleRights::PrivateLocalOnly
        || !loaded.manifest.fish_profiles.is_empty()
        || loaded.environment()?.is_none()
    {
        return Err(
            "Expected a private module with an environment and no existing fish profiles".into(),
        );
    }
    let index_bytes = fs::read(manifest::confined(&fish_root, "package.json")?)?;
    let index = resource_index(&index_bytes, fish_hash)?;
    let excluded: Vec<_> = index
        .files
        .keys()
        .filter(|name| name.ends_with(".js"))
        .cloned()
        .collect();
    if excluded.is_empty() {
        return Err("Expected the pinned reference package's historical source scripts".into());
    }
    let runtime = index.runtime_resources(&excluded)?;
    let mut payloads = BTreeMap::new();
    let mut resources = Vec::new();
    let mut new_files = Vec::new();
    for (name, pin) in &runtime {
        let bytes = fs::read(manifest::confined(&fish_root, name)?)?;
        if bytes.len() != pin.bytes || bytes_sha256(&bytes) != pin.sha256 {
            return Err(format!("Fish source differs: {name}").into());
        }
        let path = format!("{PREFIX}/{name}");
        new_files.push(ModuleFile {
            path: path.clone(),
            bytes: u64::try_from(bytes.len())?,
            sha256: pin.sha256,
            role: if name == &index.license {
                ModuleFileRole::License
            } else {
                ModuleFileRole::ObjectSource
            },
        });
        resources.push(ModuleFilePin {
            path: path.clone(),
            sha256: pin.sha256,
        });
        payloads.insert(path, bytes);
    }
    let description_name = "geometry/fish-props.json";
    let description: serde_json::Value = serde_json::from_slice(
        payloads
            .get(&format!("{PREFIX}/{description_name}"))
            .ok_or("Fish description absent")?,
    )?;
    let instances = description["instances"]
        .as_array()
        .ok_or("Fish instances absent")?;
    let prototypes = description["prototypes"]
        .as_array()
        .ok_or("Fish prototypes absent")?;
    if instances.len() != 78 || prototypes.len() != 18 {
        return Err("Pinned fish inventory differs".into());
    }
    let mut zone = ZoneEditSession::from_archive_bytes(&loaded.archive)?;
    let gameplay = zone
        .head()
        .corrections
        .gameplay
        .as_deref()
        .ok_or("Gameplay absent")?;
    let player = gameplay
        .actors
        .iter()
        .find(|actor| actor.id == gameplay.player)
        .ok_or("Player absent")?
        .position;
    let mut catalog = AssetCatalog {
        schema_version: 1,
        assets: Vec::new(),
    };
    let mut declared = Vec::new();
    let mut selected_prototypes = Vec::new();
    // The source inventory also contains severed parts and large ice props.
    // Pick five distinct whole fish so the visual check has an unambiguous subject.
    for (ordinal, index) in [2_usize, 3, 5, 7, 14].into_iter().enumerate() {
        let instance = instances.get(index).ok_or("Fish source row absent")?;
        if instance["source"]["kind"].as_str() != Some("whole") {
            return Err("Fish visual qualification requires whole specimens".into());
        }
        let prototype = instance["prototype"]
            .as_str()
            .ok_or("Fish prototype absent")?;
        if selected_prototypes.contains(&prototype) {
            return Err("Fish visual qualification requires distinct prototypes".into());
        }
        selected_prototypes.push(prototype);
        let source_id = instance["source_id"]
            .as_str()
            .ok_or("Fish source identity absent")?;
        let asset_id = AssetId::deterministic(source_id);
        catalog.assets.push(AssetRecord {
            id: asset_id,
            logical_name: source_id.into(),
            kind: AssetKind::Mesh,
            source: AssetSource::External {
                uri: "https://github.com/dgreenheck/tidewater".into(),
                declared_license: Some("MIT".into()),
            },
            content_hash: fish_hash,
        });
        declared.push(ModuleObjectAsset {
            asset_id,
            sha256: fish_hash,
        });
        let offset = [-3.0, -1.5, 0.0, 1.5, 3.0][ordinal];
        let placement = ManualPlacement {
            id: EntityId::deterministic(format!("gate7/fish-qualification/{ordinal}")),
            name: format!("Poisson de qualification {}", ordinal + 1),
            asset_id,
            asset_hash: fish_hash,
            transform: Transform {
                translation_m: Vec3::new(player.x + offset, player.y + 2.3, player.z - 4.0),
                scale: Vec3::new(0.7, 0.7, 0.7),
                ..Transform::IDENTITY
            },
            locked: false,
            physics: None,
            particles: None,
            local_lights: None,
        };
        let request = zone.head().prepare_placement(
            CommandId::deterministic(format!("gate7/fish-qualification/insert/{ordinal}")),
            PlacementEdit::Insert { placement },
        )?;
        zone.execute(&request)?;
    }
    declared.sort_by_key(|asset| asset.asset_id);
    let catalog_bytes = serde_json::to_vec(&catalog)?;
    let catalog_path = format!("{PREFIX}/catalog.json");
    let catalog_hash = bytes_sha256(&catalog_bytes);
    new_files.push(ModuleFile {
        path: catalog_path.clone(),
        bytes: u64::try_from(catalog_bytes.len())?,
        sha256: catalog_hash,
        role: ModuleFileRole::ObjectSource,
    });
    payloads.insert(catalog_path.clone(), catalog_bytes);
    let index_path = format!("{PREFIX}/package.json");
    new_files.push(ModuleFile {
        path: index_path.clone(),
        bytes: u64::try_from(index_bytes.len())?,
        sha256: fish_hash,
        role: ModuleFileRole::ObjectSource,
    });
    payloads.insert(index_path.clone(), index_bytes);

    let archive = zone.archive_bytes()?;
    let mut module = loaded.manifest.clone();
    let selected = module
        .zones
        .iter_mut()
        .find(|candidate| candidate.area_id == area)
        .ok_or("Zone absent")?;
    selected.archive_sha256 = bytes_sha256(&archive);
    selected.head_hash = zone.head().content_hash()?;
    let archive_path = selected.archive_path.clone();
    let head = selected.head_hash;
    let archive_hash = selected.archive_sha256;
    let file = module
        .files
        .iter_mut()
        .find(|file| file.path == archive_path)
        .ok_or("Zone file absent")?;
    file.bytes = u64::try_from(archive.len())?;
    file.sha256 = archive_hash;
    module.compatibility.module_api_version = MODULE_FISH_API_VERSION;
    module.module_version = "1.0.99".into();
    module.name = "Qualification privée des poissons".into();
    module.provenance.source_revision = format!("gate7/fish-qualification/{head}");
    module.provenance.source = format!(
        "Private qualification fork of admitted transport {transport}; original module and source fish package remain unchanged."
    );
    if let Some(vegetation) = module
        .vegetation
        .iter_mut()
        .find(|binding| binding.area_id == area)
    {
        vegetation.vegetation_sha256 = zone
            .head()
            .effective()?
            .1
            .ok_or("Authored vegetation disappeared from qualification zone")?
            .content_hash()?;
    }
    module
        .provenance
        .license_paths
        .push(format!("{PREFIX}/{}", index.license));
    module.fish_profiles.push(ModuleFishBinding {
        area_id: area,
        package: ModuleObjectFiles {
            manifest: ModuleFilePin {
                path: index_path,
                sha256: fish_hash,
            },
            resources,
        },
        catalog: ModuleFilePin {
            path: catalog_path,
            sha256: catalog_hash,
        },
        excluded_source_scripts: excluded.clone(),
        description: description_name.into(),
        prototypes: u32::try_from(prototypes.len())?,
        instances: u32::try_from(instances.len())?,
        assets: declared,
    });
    for file in new_files {
        if module
            .files
            .iter()
            .any(|existing| existing.path.eq_ignore_ascii_case(&file.path))
        {
            return Err("Fish qualification path conflicts with input".into());
        }
        module.files.push(file);
    }
    module.validate()?;
    module.validate_zone_archive(area, &archive)?;

    fs::create_dir(&output)?;
    let source = output.join("source");
    fs::create_dir(&source)?;
    for file in &module.files {
        let target = source.join(&file.path);
        if let Some(parent) = target.parent() {
            fs::create_dir_all(parent)?;
        }
        if file.path == archive_path {
            fs::write(target, &archive)?;
        } else if let Some(bytes) = payloads.get(&file.path) {
            fs::write(target, bytes)?;
        } else {
            let bytes = loaded.read_payload(&file.path, usize::try_from(file.bytes)?)?;
            fs::write(target, bytes)?;
        }
    }
    fs::write(source.join("module.json"), module.to_bytes()?)?;
    let package = output.join("fish-qualification.orpmod");
    let (_, revision) = orpmod::pack(
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
            files: Vec::new(),
            product: None,
        },
        &package,
        false,
    )?;
    let installed_copy = module_runtime::load(&package, area, Some(&output.join("cache")))?;
    if installed_copy.fish_profiles()?.len() != 1
        || installed_copy.zone.head().content_hash()? != head
    {
        return Err("Installed fish qualification module differs".into());
    }
    module_runtime::verify_transport_revision(&loaded, transport)?;
    if bytes_sha256(&fs::read(manifest::confined(&fish_root, "package.json")?)?) != fish_hash {
        return Err("Fish source index changed during qualification".into());
    }
    let proof = serde_json::json!({
        "schema":"forge.gate7.private-fish-qualification.v1", "package":package,
        "package_sha256":forge_delivery::file_hash(&package)?, "transport_manifest_sha256":revision,
        "area":area, "head":head, "archive_sha256":archive_hash, "fish_package_sha256":fish_hash,
        "fish_placements":5, "fish_prototypes":selected_prototypes,
        "excluded_source_scripts":excluded, "input_transport_sha256":transport,
        "private_only":true, "native_qualified":false, "gate_closed":false
    });
    fs::write(
        output.join("proof.json"),
        serde_json::to_vec_pretty(&proof)?,
    )?;
    println!("{}", serde_json::to_string(&proof)?);
    Ok(())
}
