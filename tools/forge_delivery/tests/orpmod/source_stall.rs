use super::*;
use forge_assets::source_stall_package::Layout;
use forge_core::{CommandId, EntityId, Transform};
use forge_project::zone_authoring::placements::{ManualPlacement, PlacementEdit};

#[test]
#[ignore = "Requires the private source stall assets and source notices"]
fn source_stall_round_trips_with_exact_resource_closure() {
    let root = PathBuf::from(std::env::var_os("FORGE_SOURCE_STALL_FIXTURE").unwrap());
    let descriptor = fs::read(root.join("fish-stand.json")).unwrap();
    assert_eq!(
        bytes_sha256(&descriptor).to_string(),
        "0740a6e7881b6508eac4a0b6ee5f202d1342359df95af2f7c5183ee290ab405d"
    );
    let layout: Layout = serde_json::from_slice(&descriptor).unwrap();
    let mut fixture = Fixture::new("private-source-stall");
    fs::create_dir(fixture.source.join("stall")).unwrap();
    let mut stage = |name: &str, bytes: &[u8], role| {
        let path = format!("stall/{name}");
        fs::write(fixture.source.join(&path), bytes).unwrap();
        let sha256 = bytes_sha256(bytes);
        fixture.manifest.files.push(ModuleFile {
            path: path.clone(),
            sha256,
            bytes: bytes.len() as u64,
            role,
        });
        ModuleFilePin { path, sha256 }
    };
    let manifest = stage("fish-stand.json", &descriptor, ModuleFileRole::ObjectSource);
    let mut resources = Vec::new();
    for (name, (size, sha256)) in layout.resource_pins().unwrap() {
        let bytes = fs::read(root.join(name)).unwrap();
        assert_eq!(bytes.len(), size);
        let pin = stage(name, &bytes, ModuleFileRole::ObjectSource);
        assert_eq!(pin.sha256, sha256);
        resources.push(pin);
    }
    let license = fs::read(root.join("source/LICENSE-Tidewater.txt")).unwrap();
    let credits = fs::read(root.join("source/props/CREDITS.md")).unwrap();
    let license_paths = vec![
        stage("LICENSE-Tidewater.txt", &license, ModuleFileRole::License).path,
        stage("PROP-NOTICES.md", &credits, ModuleFileRole::License).path,
    ];
    let area_id = fixture.manifest.zones[0].area_id;
    fixture
        .manifest
        .source_stalls
        .push(ModuleSourceStallBinding {
            area_id,
            asset_id: AssetId::deterministic("fixture/source-stall"),
            package: ModuleObjectFiles {
                manifest,
                resources,
            },
            license_paths: license_paths.clone(),
            attached_fish: Vec::new(),
            animated_model: None,
        });
    fixture
        .manifest
        .provenance
        .license_paths
        .extend(license_paths);
    assert!(matches!(
        fixture.manifest.validate(),
        Err(ModulePackageError::Compatibility)
    ));
    fixture.manifest.compatibility.module_api_version = MODULE_SOURCE_STALL_API_VERSION;
    let zone_path = fixture.manifest.zones[0].archive_path.clone();
    let mut zone =
        ZoneEditSession::from_archive_bytes(&fs::read(fixture.source.join(&zone_path)).unwrap())
            .unwrap();
    let stall = &fixture.manifest.source_stalls[0];
    let placement = ManualPlacement {
        id: EntityId::deterministic("fixture/source-stall-placement"),
        name: "Étal de qualification".into(),
        asset_id: stall.asset_id,
        asset_hash: stall.package.manifest.sha256,
        transform: Transform::IDENTITY,
        locked: false,
        physics: None,
        particles: None,
        local_lights: None,
    };
    let request = zone
        .head()
        .prepare_placement(
            CommandId::deterministic("fixture/insert-source-stall"),
            PlacementEdit::Insert {
                placement: placement.clone(),
            },
        )
        .unwrap();
    zone.execute(&request).unwrap();
    let zone_bytes = zone.archive_bytes().unwrap();
    fixture.manifest.zones[0].archive_sha256 = bytes_sha256(&zone_bytes);
    fixture.manifest.zones[0].head_hash = zone.head().content_hash().unwrap();
    fixture.replace(&zone_path, &zone_bytes);
    let archive = fixture.root.join("stand.orpmod");
    let mut descriptor = fixture.descriptor();
    descriptor.license_paths = fixture.manifest.provenance.license_paths.clone();
    orpmod::pack(&fixture.source, descriptor.clone(), &archive, false).unwrap();
    assert!(orpmod::inspect(&archive, true).is_err());
    let loaded =
        forge_delivery::module_runtime::load(&archive, area_id, Some(&fixture.root.join("cache")))
            .unwrap();
    assert_eq!(
        loaded.manifest.source_stalls,
        fixture.manifest.source_stalls
    );
    assert_eq!(
        loaded.zone.head().corrections.placements.as_deref(),
        Some(&[placement][..])
    );
    assert!(loaded.reject_unrendered_source_stall().is_err());
    let installed = loaded.source_stall().unwrap().unwrap();
    assert_eq!(installed.binding, &fixture.manifest.source_stalls[0]);
    for (name, (size, sha256)) in installed.layout.resource_pins().unwrap() {
        let bytes = &installed.resources[name];
        assert_eq!(bytes.len(), size);
        assert_eq!(bytes_sha256(bytes), sha256);
    }

    // A matching outer manifest and archive pin must still agree with the inner descriptor.
    let path = fixture.manifest.source_stalls[0].package.resources[0]
        .path
        .clone();
    let mut bytes = fs::read(fixture.source.join(&path)).unwrap();
    bytes[0] ^= 1;
    fixture.replace(&path, &bytes);
    fixture.manifest.source_stalls[0].package.resources[0].sha256 = bytes_sha256(&bytes);
    fixture.save();
    let error = orpmod::pack(
        &fixture.source,
        descriptor,
        &fixture.root.join("bad.orpmod"),
        false,
    )
    .expect_err("the changed resource must disagree with its stall descriptor");
    assert!(
        error
            .to_string()
            .contains("Source stall resource differs from its descriptor")
    );
}
