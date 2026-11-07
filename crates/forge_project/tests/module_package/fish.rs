use super::*;
use forge_project::module_package::{
    MODULE_FISH_API_VERSION, ModuleFilePin, ModuleFishBinding, ModuleObjectAsset, ModuleObjectFiles,
};

fn with_fish(key: &str) -> (ModulePackageManifest, Vec<u8>) {
    let (mut module, archive) = fixture(key);
    module.compatibility.module_api_version = MODULE_FISH_API_VERSION;
    let manifest = ModuleFilePin {
        path: "fish/profile/package.json".into(),
        sha256: bytes_sha256(b"fish index"),
    };
    let catalog = ModuleFilePin {
        path: "fish/profile/catalog.json".into(),
        sha256: bytes_sha256(b"fish catalog"),
    };
    let description = ModuleFilePin {
        path: "fish/profile/geometry/fish.json".into(),
        sha256: bytes_sha256(b"fish description"),
    };
    for pin in [&manifest, &catalog, &description] {
        module.files.push(ModuleFile {
            path: pin.path.clone(),
            sha256: pin.sha256,
            bytes: 16,
            role: ModuleFileRole::ObjectSource,
        });
    }
    module.fish_profiles.push(ModuleFishBinding {
        area_id: module.zones[0].area_id,
        assets: vec![ModuleObjectAsset {
            asset_id: AssetId::deterministic("independent fish"),
            sha256: manifest.sha256,
        }],
        package: ModuleObjectFiles {
            manifest,
            resources: vec![description],
        },
        catalog,
        excluded_source_scripts: Vec::new(),
        description: "geometry/fish.json".into(),
        prototypes: 1,
        instances: 1,
    });
    (module, archive)
}

#[test]
fn api_thirteen_retains_the_existing_fish_contract() {
    let (mut module, archive) = with_fish("fish-with-placement-companions");
    module.compatibility.module_api_version =
        forge_project::module_package::MODULE_PLACEMENT_COMPANIONS_API_VERSION;
    module.validate().unwrap();
    module
        .validate_zone_archive(module.zones[0].area_id, &archive)
        .unwrap();
}

#[test]
fn legacy_wire_shape_is_preserved_and_old_versions_refuse_fish_fields() {
    let (legacy, _) = fixture("legacy-fish-wire");
    let bytes = legacy.to_bytes().unwrap();
    let mut value: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
    assert!(value.get("fish_profiles").is_none());
    assert_eq!(
        ModulePackageManifest::from_bytes(&bytes)
            .unwrap()
            .to_bytes()
            .unwrap(),
        bytes
    );
    value["fish_profiles"] = serde_json::json!([]);
    assert!(ModulePackageManifest::from_bytes(&serde_json::to_vec(&value).unwrap()).is_err());
}

#[test]
fn fish_script_exclusions_are_explicit_sorted_and_do_not_change_legacy_bytes() {
    let (mut module, _) = with_fish("fish-script-exclusions");
    let bytes = module.to_bytes().unwrap();
    let value: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
    assert!(
        value["fish_profiles"][0]
            .get("excluded_source_scripts")
            .is_none()
    );
    assert_eq!(
        ModulePackageManifest::from_bytes(&bytes)
            .unwrap()
            .to_bytes()
            .unwrap(),
        bytes
    );
    module.fish_profiles[0].excluded_source_scripts =
        vec!["geometry/source.js".into(), "material/source.js".into()];
    let bytes = module.to_bytes().unwrap();
    let restored = ModulePackageManifest::from_bytes(&bytes).unwrap();
    assert_eq!(
        restored.fish_profiles[0].excluded_source_scripts,
        module.fish_profiles[0].excluded_source_scripts
    );
    for invalid in [
        vec!["geometry/fish.bin".into()],
        vec!["../source.js".into()],
        vec!["material/source.js".into(), "geometry/source.js".into()],
        vec!["geometry/source.js".into(), "geometry/source.js".into()],
    ] {
        module.fish_profiles[0].excluded_source_scripts = invalid;
        assert!(module.validate().is_err());
    }
}

#[test]
fn new_api_preserves_existing_object_terrain_ocean_and_vegetation_contracts() {
    fn attach_fish(module: &mut ModulePackageManifest) {
        let (mut donor, _) = with_fish("fish-api-donor");
        let mut binding = donor.fish_profiles.remove(0);
        binding.area_id = module.zones[0].area_id;
        module.fish_profiles.push(binding);
        module.files.extend(
            donor
                .files
                .into_iter()
                .filter(|file| file.path.starts_with("fish/profile/")),
        );
        module.compatibility.module_api_version = MODULE_FISH_API_VERSION;
    }
    let (object, _) = super::object_contracts::with_objects("fish-api-object");
    let (terrain, _) = super::terrain_contracts::with_terrain("fish-api-terrain");
    let ocean = super::ocean_contracts::with_ocean();
    let (species, species_archive) = super::api3_vegetation(false);
    let (imported, imported_archive) = super::api3_vegetation(true);
    for mut module in [object, terrain, ocean] {
        attach_fish(&mut module);
        module.validate().unwrap();
    }
    for (mut module, archive) in [(species, species_archive), (imported, imported_archive)] {
        attach_fish(&mut module);
        module
            .validate_zone_archive(module.zones[0].area_id, &archive)
            .unwrap();
    }
}

#[test]
fn fish_binding_requires_area_closure_exact_pins_and_unambiguous_ids() {
    let (module, _) = with_fish("fish-contract");
    module.validate().unwrap();
    assert_eq!(
        ModulePackageManifest::from_bytes(&module.to_bytes().unwrap()).unwrap(),
        module
    );
    let mut old = module.clone();
    old.compatibility.module_api_version = MODULE_FISH_API_VERSION - 1;
    assert!(old.validate().is_err());
    let mut foreign = module.clone();
    foreign.fish_profiles[0].area_id = AreaId::deterministic("other area");
    assert!(foreign.validate().is_err());
    let mut wrong_hash = module.clone();
    wrong_hash.fish_profiles[0].assets[0].sha256 = bytes_sha256(b"other package");
    assert!(wrong_hash.validate().is_err());
    let mut collision = module.clone();
    collision.fish_profiles[0].assets[0].asset_id = collision.assets[0].asset_id;
    assert!(collision.validate().is_err());
    let mut repeated = module.clone();
    repeated.fish_profiles.push(module.fish_profiles[0].clone());
    assert!(repeated.validate().is_err());
    let mut missing = module.clone();
    missing.fish_profiles[0].description = "geometry/missing.json".into();
    assert!(missing.validate().is_err());
    let mut escape = module.clone();
    escape.fish_profiles[0].description = "../outside.json".into();
    assert!(escape.validate().is_err());
    let mut invalid_role = module.clone();
    invalid_role.files.last_mut().unwrap().role = ModuleFileRole::AssetAuxiliary;
    assert!(invalid_role.validate().is_err());
    let mut too_many_assets = module.clone();
    let binding = &mut too_many_assets.fish_profiles[0];
    binding.instances = 4097;
    let package_hash = binding.package.manifest.sha256;
    binding.assets = (0..4097)
        .map(|i| ModuleObjectAsset {
            asset_id: AssetId::deterministic(format!("fish-{i}")),
            sha256: package_hash,
        })
        .collect();
    binding.assets.sort_by_key(|asset| asset.asset_id);
    assert!(too_many_assets.validate().is_err());
    let mut oversized = module;
    oversized.files.last_mut().unwrap().bytes =
        forge_project::module_package::fish::MAX_FISH_BYTES + 1;
    assert!(oversized.validate().is_err());
}

#[test]
fn fish_resource_inventory_rejects_ambiguous_or_foreign_files() {
    let (module, _) = with_fish("fish-resource-inventory");
    let binding = &module.fish_profiles[0];
    let resources = binding.resource_inventory(&module.files).unwrap();
    assert_eq!(
        resources["geometry/fish.json"],
        (16, binding.package.resources[0].sha256)
    );
    let mut duplicate = module.files.clone();
    duplicate.push(duplicate.last().unwrap().clone());
    assert!(binding.resource_inventory(&duplicate).is_err());
    let mut missing = module.files.clone();
    missing.retain(|file| file.path != binding.package.resources[0].path);
    assert!(binding.resource_inventory(&missing).is_err());
    let mut foreign = binding.clone();
    foreign.package.resources[0].path = "elsewhere/fish.json".into();
    assert!(foreign.resource_inventory(&module.files).is_err());
}

#[test]
fn only_the_declared_fish_revision_authorizes_a_zone_placement() {
    let (mut module, archive) = with_fish("fish-placement");
    let mut zone = ZoneEditSession::from_archive_bytes(&archive).unwrap();
    let fish = module.fish_profiles[0].assets[0];
    let mut corrections = zone.head().corrections.clone();
    corrections.placements = Some(vec![
        forge_project::zone_authoring::placements::ManualPlacement {
            id: forge_core::EntityId::deterministic("fish placement"),
            name: "fish".into(),
            asset_id: fish.asset_id,
            asset_hash: fish.sha256,
            transform: forge_core::Transform::IDENTITY,
            locked: false,
            local_lights: None,
            physics: None,
            particles: None,
        },
    ]);
    zone.execute(&forge_project::zone_authoring::session::ZoneRequest {
        command_id: forge_core::CommandId::deterministic("place fish"),
        expected_hash: zone.head().content_hash().unwrap(),
        expected_revision: zone.head().revision,
        action: forge_project::zone_authoring::session::ZoneAction::edit(corrections),
    })
    .unwrap();
    let archive = repin_archive(&mut module, &zone);
    module
        .validate_zone_archive(module.zones[0].area_id, &archive)
        .unwrap();
    let mut wrong = module.clone();
    wrong.fish_profiles[0].assets[0].sha256 = bytes_sha256(b"other revision");
    assert!(
        wrong
            .validate_zone_archive(wrong.zones[0].area_id, &archive)
            .is_err()
    );
}

#[test]
fn removed_fish_still_requires_its_exact_profile_for_undo_and_redo() {
    let (module, archive) = with_fish("fish-history");
    let mut zone = ZoneEditSession::from_archive_bytes(&archive).unwrap();
    let fish = module.fish_profiles[0].assets[0];
    let mut corrections = zone.head().corrections.clone();
    corrections.placements = Some(vec![
        forge_project::zone_authoring::placements::ManualPlacement {
            id: forge_core::EntityId::deterministic("historical fish"),
            name: "fish".into(),
            asset_id: fish.asset_id,
            asset_hash: fish.sha256,
            transform: forge_core::Transform::IDENTITY,
            locked: false,
            local_lights: None,
            physics: None,
            particles: None,
        },
    ]);
    zone.execute(&forge_project::zone_authoring::session::ZoneRequest {
        command_id: forge_core::CommandId::deterministic("place historical fish"),
        expected_hash: zone.head().content_hash().unwrap(),
        expected_revision: zone.head().revision,
        action: forge_project::zone_authoring::session::ZoneAction::edit(corrections),
    })
    .unwrap();
    zone.execute(&forge_project::zone_authoring::session::ZoneRequest {
        command_id: forge_core::CommandId::deterministic("undo historical fish"),
        expected_hash: zone.head().content_hash().unwrap(),
        expected_revision: zone.head().revision,
        action: forge_project::zone_authoring::session::ZoneAction::Undo,
    })
    .unwrap();
    assert!(
        zone.head()
            .corrections
            .placements
            .as_deref()
            .unwrap_or_default()
            .is_empty()
    );
    for archive in [
        zone.archive_bytes().unwrap(),
        zone.compact_archive_bytes().unwrap(),
        zone.delta_archive_bytes().unwrap(),
    ] {
        let mut current = module.clone();
        current.zones[0].archive_sha256 = bytes_sha256(&archive);
        current.zones[0].head_hash = zone.head().content_hash().unwrap();
        let file = current
            .files
            .iter_mut()
            .find(|file| file.role == ModuleFileRole::ZoneArchive)
            .unwrap();
        file.bytes = archive.len() as u64;
        file.sha256 = bytes_sha256(&archive);
        current
            .validate_zone_archive(current.zones[0].area_id, &archive)
            .unwrap();
        let mut absent = current.clone();
        absent.fish_profiles.clear();
        absent
            .files
            .retain(|file| !file.path.starts_with("fish/profile/"));
        absent.validate().unwrap();
        assert!(
            absent
                .validate_zone_archive(absent.zones[0].area_id, &archive)
                .is_err()
        );
        let mut reopened = ZoneEditSession::from_archive_bytes(&archive).unwrap();
        reopened
            .execute(&forge_project::zone_authoring::session::ZoneRequest {
                command_id: forge_core::CommandId::deterministic("redo historical fish"),
                expected_hash: reopened.head().content_hash().unwrap(),
                expected_revision: reopened.head().revision,
                action: forge_project::zone_authoring::session::ZoneAction::Redo,
            })
            .unwrap();
        assert_eq!(
            reopened
                .head()
                .corrections
                .placements
                .as_deref()
                .unwrap()
                .len(),
            1
        );
        let restored = repin_archive(&mut current, &reopened);
        current
            .validate_zone_archive(current.zones[0].area_id, &restored)
            .unwrap();
    }
}
