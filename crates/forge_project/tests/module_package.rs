use forge_core::{AreaId, AssetId, ContentHash, ModuleId, bytes_sha256};
use forge_project::{
    authoring::new_terrain,
    module_package::{
        MAX_MODULE_FILE_BYTES, MAX_MODULE_MANIFEST_BYTES, MODULE_PACKAGE_SCHEMA,
        MODULE_PACKAGE_VERSION, ModuleAsset, ModuleCompatibility, ModuleFile, ModuleFileRole,
        ModulePackageError, ModulePackageManifest, ModuleProvenance, ModuleRights, ModuleZone,
    },
    terrain_edit::TerrainEditSession,
    zone_authoring::{GeneratorStamp, migration::ZoneMigrationProposal, session::ZoneEditSession},
};

fn fixture(key: &str) -> (ModulePackageManifest, Vec<u8>) {
    let terrain = new_terrain(&format!("project-{key}"), key, "Test zone").unwrap();
    let project_id = terrain.project_id;
    let area_id = terrain.area_id;
    let terrain_hash = terrain.content_hash().unwrap();
    let legacy = TerrainEditSession::new(terrain).unwrap();
    let zone = ZoneEditSession::from_migration(
        ZoneMigrationProposal::prepare(
            &legacy.archive_bytes().unwrap(),
            terrain_hash,
            GeneratorStamp {
                name: "package-test".into(),
                version: "1".into(),
                inputs_hash: terrain_hash,
            },
            None,
        )
        .unwrap(),
    )
    .unwrap();
    let archive = zone.archive_bytes().unwrap();
    let archive_sha256 = bytes_sha256(&archive);
    let asset_id = AssetId::deterministic(format!("asset-{key}"));
    let asset_hash = bytes_sha256(b"asset payload");
    (
        ModulePackageManifest {
            schema: MODULE_PACKAGE_SCHEMA.into(),
            version: MODULE_PACKAGE_VERSION,
            project_id,
            module_id: ModuleId::deterministic(key),
            name: "Île de test".into(),
            module_version: "1.0.0".into(),
            compatibility: ModuleCompatibility {
                engine_version: "0.1.0".into(),
                module_api_version: 1,
            },
            provenance: ModuleProvenance {
                publisher: "Forge test author".into(),
                source: "authored fixture".into(),
                source_revision: "fixture-1".into(),
                rights: ModuleRights::PrivateLocalOnly,
                license_paths: vec!["licenses/module.txt".into()],
                proof_paths: vec!["provenance/source.json".into()],
                credits_path: "credits.txt".into(),
            },
            asset_catalog_path: "assets/catalog.json".into(),
            files: vec![
                ModuleFile {
                    path: "zones/start.json".into(),
                    bytes: archive.len() as u64,
                    sha256: archive_sha256,
                    role: ModuleFileRole::ZoneArchive,
                },
                file("assets/catalog.json", ModuleFileRole::AssetCatalog),
                ModuleFile {
                    path: "assets/object.glb".into(),
                    bytes: 13,
                    sha256: asset_hash,
                    role: ModuleFileRole::Asset,
                },
                file("licenses/module.txt", ModuleFileRole::License),
                file("provenance/source.json", ModuleFileRole::Provenance),
                file("credits.txt", ModuleFileRole::Credits),
            ],
            zones: vec![ModuleZone {
                area_id,
                archive_path: "zones/start.json".into(),
                archive_sha256,
                head_hash: zone.head().content_hash().unwrap(),
                gameplay_sha256: None,
                source_blobs: Vec::new(),
            }],
            assets: vec![ModuleAsset {
                asset_id,
                file_path: "assets/object.glb".into(),
                sha256: asset_hash,
                dependencies: Vec::new(),
            }],
            gameplay_visuals: Vec::new(),
        },
        archive,
    )
}

fn file(path: &str, role: ModuleFileRole) -> ModuleFile {
    ModuleFile {
        path: path.into(),
        bytes: 1,
        sha256: bytes_sha256(b"x"),
        role,
    }
}

#[test]
fn two_independent_modules_roundtrip_without_an_engine_or_filesystem() {
    let (first, archive) = fixture("harbor");
    let (second, second_archive) = fixture("mountain");
    assert_ne!(first.module_id, second.module_id);
    assert_ne!(first.zones[0].area_id, second.zones[0].area_id);
    for (manifest, bytes) in [(first, archive), (second, second_archive)] {
        manifest.validate().unwrap();
        manifest
            .validate_zone_archive(manifest.zones[0].area_id, &bytes)
            .unwrap();
        let encoded = manifest.to_bytes().unwrap();
        let decoded = ModulePackageManifest::from_bytes(&encoded).unwrap();
        assert_eq!(decoded, manifest);
        assert_eq!(
            decoded.content_hash().unwrap(),
            manifest.content_hash().unwrap()
        );
    }
}

#[test]
fn old_future_and_unknown_field_manifests_are_refused() {
    let (manifest, _) = fixture("versions");
    for version in [1, 3, u32::MAX] {
        let mut changed = manifest.clone();
        changed.version = version;
        assert!(matches!(
            changed.validate(),
            Err(ModulePackageError::Schema)
        ));
    }
    let mut changed = serde_json::to_value(&manifest).unwrap();
    changed["native_entrypoint"] = serde_json::json!("evil.dll");
    assert!(matches!(
        ModulePackageManifest::from_bytes(&serde_json::to_vec(&changed).unwrap()),
        Err(ModulePackageError::Json(_))
    ));
}

#[test]
fn paths_are_portable_and_cannot_alias_the_transport_manifest() {
    let (manifest, _) = fixture("paths");
    for path in [
        "../zone.json",
        "/zone.json",
        "C:/zone.json",
        "a\\b.json",
        "a//b.json",
        "a/./b.json",
        "a/NUL.json",
        "a/COM1.txt",
        "a/file. ",
        "MODULE.JSON",
        "a/evil.dll",
        "a/run.exe",
        "a/script.ps1",
        "a/script.cmd",
    ] {
        let mut changed = manifest.clone();
        changed.files[0].path = path.into();
        assert!(
            matches!(changed.validate(), Err(ModulePackageError::Path(_))),
            "{path}"
        );
    }
}

#[test]
fn duplicate_case_paths_parent_conflicts_and_zone_ids_are_refused() {
    let (manifest, _) = fixture("duplicates");
    let mut changed = manifest.clone();
    changed
        .files
        .push(file("ASSETS/OBJECT.GLB", ModuleFileRole::Asset));
    assert!(matches!(
        changed.validate(),
        Err(ModulePackageError::Duplicate)
    ));
    let mut changed = manifest.clone();
    changed.files.push(file("assets", ModuleFileRole::Asset));
    assert!(matches!(
        changed.validate(),
        Err(ModulePackageError::Duplicate)
    ));
    let mut changed = manifest;
    changed.zones.push(changed.zones[0].clone());
    assert!(matches!(
        changed.validate(),
        Err(ModulePackageError::Duplicate)
    ));
}

#[test]
fn quotas_are_checked_before_an_archive_or_asset_is_read() {
    let (manifest, _) = fixture("quotas");
    let mut changed = manifest;
    changed.files[0].bytes = MAX_MODULE_FILE_BYTES + 1;
    assert!(matches!(
        changed.validate(),
        Err(ModulePackageError::Limit(_))
    ));
    assert!(matches!(
        ModulePackageManifest::from_bytes(&vec![b' '; MAX_MODULE_MANIFEST_BYTES + 1]),
        Err(ModulePackageError::Limit(_))
    ));
}

#[test]
fn compatibility_requires_both_exact_engine_and_module_api() {
    let (manifest, _) = fixture("compatibility");
    manifest.check_compatibility("0.1.0", 1).unwrap();
    for (engine, api) in [("0.2.0", 1), ("0.1.0", 2), ("", 0)] {
        assert!(matches!(
            manifest.check_compatibility(engine, api),
            Err(ModulePackageError::Compatibility)
        ));
    }
}

#[test]
fn asset_dependencies_and_payload_pins_are_closed() {
    let (manifest, _) = fixture("dependencies");
    let mut changed = manifest.clone();
    changed.assets[0]
        .dependencies
        .push(AssetId::deterministic("absent"));
    assert!(matches!(
        changed.validate(),
        Err(ModulePackageError::Reference(_))
    ));
    let mut changed = manifest.clone();
    let same_id = changed.assets[0].asset_id;
    changed.assets[0].dependencies.push(same_id);
    assert!(matches!(
        changed.validate(),
        Err(ModulePackageError::Reference(_))
    ));
    let mut changed = manifest.clone();
    changed.assets[0].sha256 = ContentHash::from_bytes([0; 32]);
    assert!(matches!(
        changed.validate(),
        Err(ModulePackageError::Reference(_))
    ));
    let mut changed = manifest;
    changed.asset_catalog_path = "assets/absent.json".into();
    assert!(matches!(
        changed.validate(),
        Err(ModulePackageError::Reference(_))
    ));
}

#[test]
fn actual_inventory_rejects_missing_extra_changed_or_duplicate_payloads() {
    let (manifest, _) = fixture("inventory");
    let mut reordered = manifest.files.clone();
    reordered.reverse();
    manifest.validate_inventory(&reordered).unwrap();
    let mut variants = Vec::new();
    variants.push(manifest.files[1..].to_vec());
    let mut extra = manifest.files.clone();
    extra.push(file("unlisted.txt", ModuleFileRole::Provenance));
    variants.push(extra);
    let mut changed = manifest.files.clone();
    changed[0].sha256 = bytes_sha256(b"changed");
    variants.push(changed);
    let mut duplicate = manifest.files.clone();
    duplicate.push(duplicate[0].clone());
    variants.push(duplicate);
    for actual in variants {
        assert!(matches!(
            manifest.validate_inventory(&actual),
            Err(ModulePackageError::Inventory)
        ));
    }
}

#[test]
fn zone_archive_bytes_and_domain_identity_must_match_the_manifest() {
    let (manifest, archive) = fixture("identity");
    let area = manifest.zones[0].area_id;
    let mut changed = manifest.clone();
    changed.project_id = forge_core::ProjectId::deterministic("wrong-project");
    assert!(matches!(
        changed.validate_zone_archive(area, &archive),
        Err(ModulePackageError::Archive(_))
    ));
    let mut changed = manifest.clone();
    changed.zones[0].head_hash = bytes_sha256(b"wrong-head");
    assert!(matches!(
        changed.validate_zone_archive(area, &archive),
        Err(ModulePackageError::Archive(_))
    ));
    assert!(matches!(
        manifest.validate_zone_archive(AreaId::deterministic("absent"), &archive),
        Err(ModulePackageError::Archive(_))
    ));
    let mut tampered = archive;
    tampered.push(b' ');
    assert!(matches!(
        manifest.validate_zone_archive(area, &tampered),
        Err(ModulePackageError::Archive(_))
    ));
}

#[test]
fn provenance_and_credits_are_required_payload_references() {
    let (manifest, _) = fixture("provenance");
    let mut changed = manifest.clone();
    changed.provenance.license_paths.clear();
    assert!(matches!(
        changed.validate(),
        Err(ModulePackageError::Reference(_))
    ));
    let mut changed = manifest.clone();
    changed.provenance.credits_path = "missing.txt".into();
    assert!(matches!(
        changed.validate(),
        Err(ModulePackageError::Reference(_))
    ));
    let mut changed = manifest;
    changed.provenance.source_revision = " ".into();
    assert!(matches!(
        changed.validate(),
        Err(ModulePackageError::Text(_))
    ));
}

fn repin_archive(manifest: &mut ModulePackageManifest, zone: &ZoneEditSession) -> Vec<u8> {
    let archive = zone.archive_bytes().unwrap();
    let hash = bytes_sha256(&archive);
    manifest.zones[0].archive_sha256 = hash;
    manifest.zones[0].head_hash = zone.head().content_hash().unwrap();
    manifest.zones[0].gameplay_sha256 = zone
        .head()
        .corrections
        .gameplay
        .as_deref()
        .map(forge_core::canonical_json_sha256)
        .transpose()
        .unwrap();
    let file = manifest
        .files
        .iter_mut()
        .find(|file| file.role == ModuleFileRole::ZoneArchive)
        .unwrap();
    file.bytes = archive.len() as u64;
    file.sha256 = hash;
    archive
}

#[test]
fn gameplay_module_identity_is_checked_inside_the_canonical_archive() {
    let (mut manifest, archive) = fixture("gameplay");
    let mut zone = ZoneEditSession::from_archive_bytes(&archive).unwrap();
    let player = forge_core::EntityId::deterministic("package-player");
    let gameplay: forge_gameplay::GameplayDocument = serde_json::from_value(serde_json::json!({
        "schema": "forge.gameplay", "version": 1,
        "module_id": manifest.module_id, "area_id": manifest.zones[0].area_id,
        "profile": {
            "id": "forge.gate3-minimal", "version": 1,
            "interaction_range_m": 3.0, "attack_range_m": 2.0,
            "attack_damage": 20, "attack_stamina_cost": 5, "max_stamina": 100,
            "max_action_receipts": 512, "max_events_per_tick": 128
        },
        "player": player,
        "actors": [{
            "id": player, "name": "Player", "role": "player",
            "position": {"x": 0.0, "y": 0.0, "z": 0.0},
            "max_vitality": 100, "protection": 0, "inventory_capacity": 12,
            "dialogue": null, "patrol": [], "flees_danger": false
        }],
        "items": [], "objects": [], "places": [], "dialogues": [], "quests": []
    }))
    .unwrap();
    let request = zone
        .head()
        .prepare_gameplay(
            Some(gameplay),
            forge_core::CommandId::deterministic("package-gameplay"),
        )
        .unwrap();
    zone.execute(&request).unwrap();
    let archive = repin_archive(&mut manifest, &zone);
    manifest
        .validate_zone_archive(manifest.zones[0].area_id, &archive)
        .unwrap();
    manifest.module_id = ModuleId::deterministic("another-module");
    assert!(matches!(
        manifest.validate_zone_archive(manifest.zones[0].area_id, &archive),
        Err(ModulePackageError::Archive(_))
    ));
}

#[test]
fn authored_placement_requires_the_same_asset_id_and_payload_pin() {
    let (mut manifest, archive) = fixture("placement");
    let mut zone = ZoneEditSession::from_archive_bytes(&archive).unwrap();
    let mut corrections = zone.head().corrections.clone();
    corrections.placements = Some(vec![
        forge_project::zone_authoring::placements::ManualPlacement {
            id: forge_core::EntityId::deterministic("package-placement"),
            name: "Authored object".into(),
            asset_id: manifest.assets[0].asset_id,
            asset_hash: manifest.assets[0].sha256,
            physics: None,
            particles: None,
            transform: forge_core::Transform::IDENTITY,
            locked: true,
        },
    ]);
    zone.execute(&forge_project::zone_authoring::session::ZoneRequest {
        command_id: forge_core::CommandId::deterministic("insert-placement"),
        expected_hash: zone.head().content_hash().unwrap(),
        expected_revision: zone.head().revision,
        action: forge_project::zone_authoring::session::ZoneAction::edit(corrections),
    })
    .unwrap();
    let archive = repin_archive(&mut manifest, &zone);
    manifest
        .validate_zone_archive(manifest.zones[0].area_id, &archive)
        .unwrap();
    manifest.assets[0].asset_id = AssetId::deterministic("different-asset");
    assert!(matches!(
        manifest.validate_zone_archive(manifest.zones[0].area_id, &archive),
        Err(ModulePackageError::Archive(_))
    ));
}

#[test]
fn cyclic_dependencies_and_orphan_payload_files_are_refused() {
    let (mut manifest, _) = fixture("cycle");
    let first_id = manifest.assets[0].asset_id;
    let second_id = AssetId::deterministic("cycle-second");
    let second_file = file("assets/second.png", ModuleFileRole::Asset);
    manifest.assets[0].dependencies = vec![second_id];
    manifest.assets.push(ModuleAsset {
        asset_id: second_id,
        file_path: second_file.path.clone(),
        sha256: second_file.sha256,
        dependencies: vec![first_id],
    });
    manifest.files.push(second_file);
    assert!(matches!(
        manifest.validate(),
        Err(ModulePackageError::Reference(_))
    ));
    manifest.assets[1].dependencies.clear();
    manifest.validate().unwrap();
    manifest
        .files
        .push(file("orphan.bin", ModuleFileRole::SourceBlob));
    assert!(matches!(
        manifest.validate(),
        Err(ModulePackageError::Reference(_))
    ));
}

#[test]
fn zone_blob_references_must_match_its_canonical_source_documents() {
    let (mut manifest, archive) = fixture("blob-closure");
    let extra_blob = file("sources/not-used.bin", ModuleFileRole::SourceBlob);
    manifest.zones[0].source_blobs.push(extra_blob.sha256);
    manifest.files.push(extra_blob);
    manifest.validate().unwrap();
    assert!(matches!(
        manifest.validate_zone_archive(manifest.zones[0].area_id, &archive),
        Err(ModulePackageError::Archive(_))
    ));
}

#[test]
fn unicode_filename_aliases_are_refused_but_display_names_remain_unicode() {
    let (manifest, _) = fixture("portable-names");
    assert_eq!(manifest.name, "Île de test");
    manifest.validate().unwrap();
    for path in ["assets/σ.glb", "assets/ς.glb", "assets/maison-é.glb"] {
        let mut changed = manifest.clone();
        changed.assets[0].file_path = path.into();
        changed
            .files
            .iter_mut()
            .find(|file| file.role == ModuleFileRole::Asset)
            .unwrap()
            .path = path.into();
        assert!(
            matches!(changed.validate(), Err(ModulePackageError::Path(_))),
            "{path}"
        );
    }
    let mut aliases = manifest;
    aliases.assets[0].file_path = "assets/σ.glb".into();
    aliases
        .files
        .iter_mut()
        .find(|file| file.role == ModuleFileRole::Asset)
        .unwrap()
        .path = "assets/σ.glb".into();
    let mut second = aliases.assets[0].clone();
    second.asset_id = AssetId::deterministic("final-sigma-alias");
    second.file_path = "assets/ς.glb".into();
    let mut second_file = aliases
        .files
        .iter()
        .find(|file| file.role == ModuleFileRole::Asset)
        .unwrap()
        .clone();
    second_file.path = second.file_path.clone();
    aliases.assets.push(second);
    aliases.files.push(second_file);
    assert!(matches!(
        aliases.validate(),
        Err(ModulePackageError::Path(_))
    ));
}

fn vegetation_package(
    key: &str,
    imported: bool,
) -> (
    ModulePackageManifest,
    ZoneEditSession,
    forge_project::VegetationLayerId,
    ContentHash,
) {
    use forge_project::imported_vegetation::{
        ImportedInstanceEncoding, ImportedVegetationBlock, ImportedVegetationSource,
    };
    let (mut manifest, archive) = fixture(key);
    let mut zone = ZoneEditSession::from_archive_bytes(&archive).unwrap();
    let mut vegetation = forge_project::VegetationDocument::from_json(include_str!(
        "../../../examples/visual_valley/vegetation.v1.json"
    ))
    .unwrap();
    vegetation.project_id = manifest.project_id;
    vegetation.area_id = manifest.zones[0].area_id;
    vegetation.extent_m.size_x_m = zone.head().base.terrain.extent_m.size_x_m;
    vegetation.extent_m.size_z_m = zone.head().base.terrain.extent_m.size_z_m;
    vegetation
        .layers
        .retain(|layer| layer.kind == forge_project::VegetationKind::Tree);
    vegetation.layers.truncate(1);
    vegetation.layers[0].material_filter = None;
    vegetation.layers[0].density_mask = None;
    vegetation.exclusions.clear();
    let layer_id = vegetation.layers[0].id.clone();
    let source_key = bytes_sha256(b"governed-imported-instance");
    if imported {
        let mut blob = source_key.as_bytes().to_vec();
        for value in [0.0_f64, 0.0, 0.0, 0.0, 0.0, 0.0, 1.0, 1.0, 1.0, 1.0] {
            blob.extend_from_slice(&value.to_le_bytes());
        }
        let blob_hash = bytes_sha256(&blob);
        vegetation.schema_version = 2;
        vegetation.imported_source = Some(ImportedVegetationSource {
            schema: "forge.imported-vegetation".into(),
            version: 1,
            blocks: vec![ImportedVegetationBlock {
                layer_id: layer_id.to_string(),
                prototype_key: "unresolved_source_tree".into(),
                count: 1,
                encoding: ImportedInstanceEncoding::TransformF64LeV1,
                instances_sha256: blob_hash,
                parameter_names: Vec::new(),
            }],
        });
        manifest.files.push(ModuleFile {
            path: "sources/trees.bin".into(),
            bytes: blob.len() as u64,
            sha256: blob_hash,
            role: ModuleFileRole::SourceBlob,
        });
        manifest.zones[0].source_blobs = vec![blob_hash];
    }
    let mut base = zone.head().base.clone();
    base.vegetation = Some(vegetation);
    let request = zone
        .head()
        .prepare_regeneration(
            base,
            forge_core::CommandId::deterministic(format!("package-vegetation-{key}")),
        )
        .unwrap();
    zone.execute(&request).unwrap();
    (manifest, zone, layer_id, source_key)
}

#[test]
fn unresolved_species_catalogs_are_refused_after_canonical_archive_validation() {
    use forge_project::zone_authoring::species::SpeciesReference;
    let (mut manifest, mut zone, layer_id, _) = vegetation_package("species-gap", false);
    let request = zone
        .head()
        .prepare_species(
            layer_id.clone(),
            Some(vec![SpeciesReference {
                catalog_sha256: bytes_sha256(b"catalog-not-in-module"),
                source_layer_id: layer_id,
                asset_id: "unresolved-source-tree".into(),
            }]),
            forge_core::CommandId::deterministic("package-species-gap"),
        )
        .unwrap();
    zone.execute(&request).unwrap();
    let archive = repin_archive(&mut manifest, &zone);
    ZoneEditSession::from_archive_bytes(&archive).unwrap();
    manifest.validate().unwrap();
    assert!(matches!(
        manifest.validate_zone_archive(manifest.zones[0].area_id, &archive),
        Err(ModulePackageError::Archive(_))
    ));
}

#[test]
fn unresolved_physics_and_particle_packages_are_refused_on_authored_assets() {
    use forge_project::zone_authoring::{
        placements::{ManualPlacement, ParticlePackageReference, PhysicsPackageReference},
        session::{ZoneAction, ZoneRequest},
    };
    for particle in [false, true] {
        let (mut manifest, archive) = fixture("annex-gap");
        let mut zone = ZoneEditSession::from_archive_bytes(&archive).unwrap();
        let mut placement = ManualPlacement {
            id: forge_core::EntityId::deterministic("package-annex"),
            name: "Object with unresolved companion package".into(),
            asset_id: manifest.assets[0].asset_id,
            asset_hash: manifest.assets[0].sha256,
            physics: None,
            particles: None,
            transform: forge_core::Transform::IDENTITY,
            locked: false,
        };
        if particle {
            placement.particles = Some(Box::new(ParticlePackageReference {
                schema_version: 1,
                carrier_sha256: placement.asset_hash,
                manifest_sha256: bytes_sha256(b"absent-particle-manifest"),
                package_sha256: bytes_sha256(b"absent-particle-package"),
            }));
        } else {
            placement.physics = Some(Box::new(PhysicsPackageReference {
                schema_version: 1,
                carrier_sha256: placement.asset_hash,
                manifest_sha256: bytes_sha256(b"absent-physics-manifest"),
                package_sha256: bytes_sha256(b"absent-physics-package"),
            }));
        }
        let mut corrections = zone.head().corrections.clone();
        corrections.placements = Some(vec![placement]);
        zone.execute(&ZoneRequest {
            command_id: forge_core::CommandId::deterministic("package-annex-edit"),
            expected_hash: zone.head().content_hash().unwrap(),
            expected_revision: zone.head().revision,
            action: ZoneAction::edit(corrections),
        })
        .unwrap();
        let archive = repin_archive(&mut manifest, &zone);
        ZoneEditSession::from_archive_bytes(&archive).unwrap();
        manifest.validate().unwrap();
        assert!(matches!(
            manifest.validate_zone_archive(manifest.zones[0].area_id, &archive),
            Err(ModulePackageError::Archive(_))
        ));
    }
}

#[test]
fn unresolved_imported_prototypes_are_refused_even_when_instance_blob_is_present() {
    let (mut manifest, zone, _, _) = vegetation_package("prototype-gap", true);
    let archive = repin_archive(&mut manifest, &zone);
    ZoneEditSession::from_archive_bytes(&archive).unwrap();
    manifest.validate().unwrap();
    assert!(matches!(
        manifest.validate_zone_archive(manifest.zones[0].area_id, &archive),
        Err(ModulePackageError::Archive(_))
    ));
}

#[test]
fn imported_plant_copies_cannot_hide_unresolved_source_asset_references() {
    use forge_project::zone_authoring::imported_plants::{
        ImportedPlantPlacement, ImportedPlantPlacementEdit,
    };
    let (mut manifest, mut zone, layer_id, source_key) = vegetation_package("plant-copy-gap", true);
    let request = zone
        .head()
        .prepare_imported_plant_placement(
            ImportedPlantPlacementEdit::Insert {
                placement: ImportedPlantPlacement {
                    id: forge_core::EntityId::deterministic("package-imported-copy"),
                    layer_id,
                    source_key,
                    translation_m: [1.0, 0.0, 1.0],
                    yaw_degrees: None,
                    scale_factor: None,
                },
            },
            forge_core::CommandId::deterministic("package-imported-copy-edit"),
        )
        .unwrap();
    zone.execute(&request).unwrap();
    let archive = repin_archive(&mut manifest, &zone);
    ZoneEditSession::from_archive_bytes(&archive).unwrap();
    manifest.validate().unwrap();
    assert!(matches!(
        manifest.validate_zone_archive(manifest.zones[0].area_id, &archive),
        Err(ModulePackageError::Archive(_))
    ));
}

fn visual_binding(
    manifest: &ModulePackageManifest,
) -> forge_project::module_gameplay_visuals::GameplayVisualBinding {
    forge_project::module_gameplay_visuals::GameplayVisualBinding {
        area_id: manifest.zones[0].area_id,
        entity_id: forge_core::EntityId::deterministic("manifest-visual-target"),
        asset: forge_project::module_gameplay_visuals::VisualAssetPin {
            asset_id: manifest.assets[0].asset_id,
            sha256: manifest.assets[0].sha256,
        },
        local_transform: forge_core::Transform::IDENTITY,
    }
}

#[test]
fn manifest_visual_catalog_references_require_a_declared_area_and_exact_asset_pin() {
    let (manifest, _) = fixture("visual-catalog");
    for absent_area in [false, true] {
        let mut changed = manifest.clone();
        let mut binding = visual_binding(&changed);
        if absent_area {
            binding.area_id = AreaId::deterministic("absent-visual-area");
        } else {
            binding.asset.sha256 = bytes_sha256(b"incorrect-visual-pin");
        }
        changed.gameplay_visuals.push(binding);
        assert!(matches!(
            changed.validate(),
            Err(ModulePackageError::Reference(_))
        ));
    }
}

#[test]
fn authored_visual_bindings_cannot_target_a_zone_without_gameplay() {
    let (mut manifest, archive) = fixture("visual-no-gameplay");
    let binding = visual_binding(&manifest);
    manifest.gameplay_visuals.push(binding);
    manifest.validate().unwrap();
    assert!(matches!(
        manifest.validate_zone_archive(manifest.zones[0].area_id, &archive),
        Err(ModulePackageError::Archive(_))
    ));
}

fn visual_zone() -> (ModulePackageManifest, ZoneEditSession) {
    let (mut manifest, archive) = fixture("canonical-visuals");
    let mut zone = ZoneEditSession::from_archive_bytes(&archive).unwrap();
    let player = forge_core::EntityId::deterministic("canonical-visuals/player");
    let target = visual_binding(&manifest).entity_id;
    let document = serde_json::from_value(serde_json::json!({
        "schema":"forge.gameplay","version":1,
        "module_id":manifest.module_id,"area_id":manifest.zones[0].area_id,"player":player,
        "profile":{"id":"forge.gate3-minimal","version":1,"interaction_range_m":3.0,
            "attack_range_m":2.0,"attack_damage":20,"attack_stamina_cost":5,"max_stamina":100,
            "max_action_receipts":512,"max_events_per_tick":128},
        "actors":[
            {"id":player,"name":"Player","role":"player","position":{"x":0.0,"y":0.0,"z":0.0},
                "max_vitality":100,"protection":0,"inventory_capacity":12,"dialogue":null,"patrol":[],"flees_danger":false},
            {"id":target,"name":"Keeper","role":"friendly","position":{"x":0.0,"y":0.0,"z":0.0},
                "max_vitality":100,"protection":0,"inventory_capacity":12,"dialogue":null,"patrol":[],"flees_danger":false}
        ],"items":[],"objects":[],"places":[],"dialogues":[],"quests":[]
    })).unwrap();
    let request = zone
        .head()
        .prepare_gameplay(
            Some(document),
            forge_core::CommandId::deterministic("canonical-visuals/gameplay"),
        )
        .unwrap();
    zone.execute(&request).unwrap();
    repin_archive(&mut manifest, &zone);
    (manifest, zone)
}

#[test]
fn manifest_cannot_introduce_a_visual_absent_from_its_canonical_zone() {
    let (mut manifest, zone) = visual_zone();
    manifest.gameplay_visuals.push(visual_binding(&manifest));
    let archive = repin_archive(&mut manifest, &zone);
    manifest.validate().unwrap();
    assert!(matches!(
        manifest.validate_zone_archive(manifest.zones[0].area_id, &archive),
        Err(ModulePackageError::Archive(_))
    ));
}

#[test]
fn manifest_cannot_omit_or_retarget_a_canonical_gameplay_visual() {
    use forge_project::zone_authoring::session::{ZoneAction, ZoneRequest};
    let (mut manifest, mut zone) = visual_zone();
    let binding = visual_binding(&manifest);
    let mut corrections = zone.head().corrections.clone();
    corrections.gameplay_visuals = Some(vec![binding.clone()]);
    zone.execute(&ZoneRequest {
        command_id: forge_core::CommandId::deterministic("canonical-visuals/bind"),
        expected_revision: zone.head().revision,
        expected_hash: zone.head().content_hash().unwrap(),
        action: ZoneAction::edit(corrections),
    })
    .unwrap();
    manifest.gameplay_visuals = vec![binding];
    let archive = repin_archive(&mut manifest, &zone);
    manifest
        .validate_zone_archive(manifest.zones[0].area_id, &archive)
        .unwrap();
    let mut omitted = manifest.clone();
    omitted.gameplay_visuals.clear();
    assert!(matches!(
        omitted.validate_zone_archive(omitted.zones[0].area_id, &archive),
        Err(ModulePackageError::Archive(_))
    ));
    manifest.gameplay_visuals[0].local_transform.translation_m.y = 2.0;
    manifest.validate().unwrap();
    assert!(matches!(
        manifest.validate_zone_archive(manifest.zones[0].area_id, &archive),
        Err(ModulePackageError::Archive(_))
    ));
}

#[test]
fn distinct_catalog_identities_may_reference_the_same_exact_payload_file() {
    let (mut manifest, archive) = fixture("shared-content-addressed-model");
    let file_count = manifest.files.len();
    let mut second_identity = manifest.assets[0].clone();
    second_identity.asset_id = AssetId::deterministic("second-semantic-identity-same-model");
    second_identity.dependencies = vec![manifest.assets[0].asset_id];
    manifest.assets.push(second_identity);
    manifest.validate().unwrap();
    assert_eq!(manifest.assets.len(), 2);
    assert_eq!(manifest.files.len(), file_count);
    manifest
        .validate_zone_archive(manifest.zones[0].area_id, &archive)
        .unwrap();
    let reopened = ModulePackageManifest::from_bytes(&manifest.to_bytes().unwrap()).unwrap();
    assert_eq!(reopened, manifest);
    let mut conflicting = manifest;
    conflicting.assets[1].sha256 = bytes_sha256(b"different revision under the same filename");
    assert!(conflicting.validate().is_err());
}
