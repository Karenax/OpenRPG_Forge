use super::*;
use forge_core::{CommandId, EntityId, Transform};
use forge_project::{
    module_package::{
        MODULE_PLACEMENT_COMPANIONS_API_VERSION, ModuleFilePin, ModulePlacementCompanion,
        ModulePlacementCompanionKind,
    },
    zone_authoring::placements::{
        ManualPlacement, ParticlePackageReference, PhysicsPackageReference, PlacementEdit,
    },
};

// Synthetic API 12 wire created from the pre-API-13 field order. Its payload
// pins are structural only; this test does not admit a real archive.
const LEGACY_API_12: &str = r#"{"schema":"forge.module-package","version":2,"project_id":"11111111-1111-4111-8111-111111111111","module_id":"22222222-2222-4222-8222-222222222222","name":"Legacy reference","module_version":"1.0.0","compatibility":{"engine_version":"0.1.0","module_api_version":12},"provenance":{"publisher":"Forge tests","source":"synthetic legacy fixture","source_revision":"fixture-1","rights":"private_local_only","license_paths":["LICENSE.txt"],"proof_paths":["PROVENANCE.txt"],"credits_path":"CREDITS.txt"},"asset_catalog_path":"assets/catalog.json","files":[{"path":"zones/start.json","bytes":1,"sha256":"1111111111111111111111111111111111111111111111111111111111111111","role":"zone_archive"},{"path":"assets/catalog.json","bytes":1,"sha256":"1111111111111111111111111111111111111111111111111111111111111111","role":"asset_catalog"},{"path":"LICENSE.txt","bytes":1,"sha256":"1111111111111111111111111111111111111111111111111111111111111111","role":"license"},{"path":"PROVENANCE.txt","bytes":1,"sha256":"1111111111111111111111111111111111111111111111111111111111111111","role":"provenance"},{"path":"CREDITS.txt","bytes":1,"sha256":"1111111111111111111111111111111111111111111111111111111111111111","role":"credits"}],"zones":[{"area_id":"33333333-3333-4333-8333-333333333333","archive_path":"zones/start.json","archive_sha256":"1111111111111111111111111111111111111111111111111111111111111111","head_hash":"1111111111111111111111111111111111111111111111111111111111111111","gameplay_sha256":null,"source_blobs":[]}],"assets":[]}"#;

#[test]
fn legacy_wire_and_canonical_hash_remain_identical() {
    let module = ModulePackageManifest::from_bytes(LEGACY_API_12.as_bytes()).unwrap();
    assert_eq!(module.to_bytes().unwrap(), LEGACY_API_12.as_bytes());
    assert_eq!(
        module.content_hash().unwrap().to_string(),
        "b235956a17aad33c3666699b5aa594822b8a6d33bcde1d6fa1abb16575ef99f4"
    );
    for api in 1..MODULE_PLACEMENT_COMPANIONS_API_VERSION {
        let legacy = LEGACY_API_12.replace(
            "\"module_api_version\":12",
            &format!("\"module_api_version\":{api}"),
        );
        let module = ModulePackageManifest::from_bytes(legacy.as_bytes()).unwrap();
        assert_eq!(module.to_bytes().unwrap(), legacy.as_bytes());
        let original: serde_json::Value = serde_json::from_str(&legacy).unwrap();
        assert_eq!(
            module.content_hash().unwrap(),
            forge_core::canonical_json_sha256(&original).unwrap()
        );
        let mut injected = original;
        injected["placement_companions"] = serde_json::json!([]);
        assert!(matches!(
            ModulePackageManifest::from_bytes(&serde_json::to_vec(&injected).unwrap()),
            Err(ModulePackageError::Compatibility)
        ));
    }
}

fn history_fixture() -> (ModulePackageManifest, Vec<u8>) {
    let (mut module, archive) = fixture("placement-companions");
    module.compatibility.module_api_version = MODULE_PLACEMENT_COMPANIONS_API_VERSION;
    let mut zone = ZoneEditSession::from_archive_bytes(&archive).unwrap();
    let object = ManualPlacement {
        id: EntityId::deterministic("companion history object"),
        name: "Historical companion".into(),
        asset_id: module.assets[0].asset_id,
        asset_hash: module.assets[0].sha256,
        physics: Some(Box::new(PhysicsPackageReference {
            schema_version: 1,
            carrier_sha256: module.assets[0].sha256,
            manifest_sha256: bytes_sha256(b"physics manifest"),
            package_sha256: bytes_sha256(b"physics package"),
        })),
        particles: Some(Box::new(ParticlePackageReference {
            schema_version: 1,
            carrier_sha256: module.assets[0].sha256,
            manifest_sha256: bytes_sha256(b"particle manifest"),
            package_sha256: bytes_sha256(b"particle package"),
        })),
        local_lights: None,
        transform: Transform::IDENTITY,
        locked: false,
    };
    for (name, edit) in [
        (
            "insert",
            PlacementEdit::Insert {
                placement: object.clone(),
            },
        ),
        ("remove", PlacementEdit::Remove { id: object.id }),
    ] {
        let request = zone
            .head()
            .prepare_placement(CommandId::deterministic(name), edit)
            .unwrap();
        zone.execute(&request).unwrap();
    }
    assert!(
        zone.head()
            .corrections
            .placements
            .as_deref()
            .unwrap_or_default()
            .is_empty()
    );
    let required = zone.required_source_companions().unwrap();
    for (kind, path, pin) in [
        (
            ModulePlacementCompanionKind::Physics,
            "companions/physics.orpp",
            *required.physics.iter().next().unwrap(),
        ),
        (
            ModulePlacementCompanionKind::Particles,
            "companions/particles.orpe",
            *required.particles.iter().next().unwrap(),
        ),
    ] {
        let proof = format!("{path}.proof.json");
        let proof_hash = bytes_sha256(proof.as_bytes());
        module.files.push(ModuleFile {
            path: proof.clone(),
            bytes: 1,
            sha256: proof_hash,
            role: ModuleFileRole::Provenance,
        });
        module.provenance.proof_paths.push(proof.clone());
        module.files.push(ModuleFile {
            path: path.into(),
            bytes: 1,
            sha256: pin.package_sha256,
            role: ModuleFileRole::PlacementCompanion,
        });
        module.placement_companions.push(ModulePlacementCompanion {
            areas: vec![module.zones[0].area_id],
            kind,
            asset_id: pin.asset_id,
            carrier_sha256: pin.carrier_sha256,
            manifest_sha256: pin.manifest_sha256,
            package: ModuleFilePin {
                path: path.into(),
                sha256: pin.package_sha256,
            },
            proof: ModuleFilePin {
                path: proof,
                sha256: proof_hash,
            },
        });
    }
    let archive = repin_archive(&mut module, &zone);
    (module, archive)
}

#[test]
fn physics_and_particles_in_undo_history_have_exact_area_closure() {
    let (module, archive) = history_fixture();
    module.validate().unwrap();
    module
        .validate_zone_archive(module.zones[0].area_id, &archive)
        .unwrap();
    let restored = ModulePackageManifest::from_bytes(&module.to_bytes().unwrap()).unwrap();
    assert_eq!(restored, module);

    for index in 0..module.placement_companions.len() {
        let mut missing = module.clone();
        let removed = missing.placement_companions.remove(index);
        missing
            .files
            .retain(|file| file.path != removed.package.path);
        missing.validate().unwrap();
        assert!(
            missing
                .validate_zone_archive(missing.zones[0].area_id, &archive)
                .is_err()
        );
    }
    let mut extra = module.clone();
    extra.placement_companions[0].manifest_sha256 = bytes_sha256(b"another manifest");
    assert!(
        extra
            .validate_zone_archive(extra.zones[0].area_id, &archive)
            .is_err()
    );

    let mut old = module.clone();
    old.compatibility.module_api_version = 12;
    old.files
        .retain(|file| file.role != ModuleFileRole::PlacementCompanion);
    old.placement_companions.clear();
    assert!(
        old.validate_zone_archive(old.zones[0].area_id, &archive)
            .unwrap_err()
            .to_string()
            .contains("physics and particle companion")
    );
}

#[test]
fn companion_identity_roles_areas_and_paths_are_closed() {
    let (module, _) = history_fixture();
    for case in 0..8 {
        let mut changed = module.clone();
        match case {
            0 => changed
                .placement_companions
                .push(changed.placement_companions[0].clone()),
            1 => changed.placement_companions[0]
                .areas
                .push(changed.zones[0].area_id),
            2 => changed.placement_companions[0].areas[0] = AreaId::deterministic("foreign"),
            3 => changed.placement_companions[0].carrier_sha256 = bytes_sha256(b"wrong carrier"),
            4 => changed.placement_companions[0].package.sha256 = bytes_sha256(b"wrong package"),
            5 => changed.placement_companions[0].proof.sha256 = bytes_sha256(b"wrong proof"),
            6 => changed
                .provenance
                .proof_paths
                .retain(|path| path != &module.placement_companions[0].proof.path),
            _ => {
                changed
                    .files
                    .iter_mut()
                    .find(|f| f.role == ModuleFileRole::PlacementCompanion)
                    .unwrap()
                    .role = ModuleFileRole::AssetAuxiliary;
            }
        }
        assert!(
            changed.validate().is_err(),
            "invalid case {case} was admitted"
        );
    }
}

#[test]
fn companion_may_not_be_claimed_by_an_unrelated_area() {
    let (mut module, first_archive) = history_fixture();
    let terrain = new_terrain("project-placement-companions", "other", "Second area").unwrap();
    assert_eq!(terrain.project_id, module.project_id);
    let other = ZoneEditSession::from_initial_terrain(terrain).unwrap();
    let area = other.head().base.terrain.area_id;
    let archive = other.archive_bytes().unwrap();
    let hash = bytes_sha256(&archive);
    module.files.push(ModuleFile {
        path: "zones/other.json".into(),
        bytes: archive.len() as u64,
        sha256: hash,
        role: ModuleFileRole::ZoneArchive,
    });
    module.zones.push(ModuleZone {
        area_id: area,
        archive_path: "zones/other.json".into(),
        archive_sha256: hash,
        head_hash: other.head().content_hash().unwrap(),
        gameplay_sha256: None,
        source_blobs: Vec::new(),
    });
    module
        .validate_zone_archive(module.zones[0].area_id, &first_archive)
        .unwrap();
    module.validate_zone_archive(area, &archive).unwrap();
    module.placement_companions[0].areas.push(area);
    module.placement_companions[0].areas.sort_unstable();
    module.validate().unwrap();
    assert!(module.validate_zone_archive(area, &archive).is_err());
}
