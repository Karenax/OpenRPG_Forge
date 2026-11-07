use super::*;
use forge_delivery::module_local_lights;
use forge_project::zone_authoring::placements::{ManualPlacement, PlacementEdit};
#[path = "light_packages/source_fixture.rs"]
mod source_fixture;
use source_fixture::{Lights, lights, pin};

fn attach(zone: &mut ZoneEditSession, fixture: &Fixture, lights: &Lights, wrong: bool) {
    let asset = &fixture.catalog.assets[0];
    let mut reference = pin(lights, asset.payload.sha256);
    if wrong {
        reference.light_ids = vec![bytes_sha256(b"unknown")];
    }
    let p = ManualPlacement {
        id: EntityId::deterministic(format!("lamp/{}", zone.head().base.terrain.area_id)),
        name: "Fixture lamp".into(),
        asset_id: asset.id,
        asset_hash: asset.payload.sha256,
        physics: None,
        particles: None,
        local_lights: Some(reference),
        transform: Transform::IDENTITY,
        locked: false,
    };
    let request = zone
        .head()
        .prepare_placement(
            CommandId::deterministic("attach-source-lights"),
            PlacementEdit::Insert { placement: p },
        )
        .unwrap();
    zone.execute(&request).unwrap();
}
fn fixture_with_lights() -> (Fixture, Lights) {
    let mut f = Fixture::new(false);
    let lights = lights();
    let mut zone = ZoneEditSession::from_archive_bytes(&f.zone.archive_bytes().unwrap()).unwrap();
    attach(&mut zone, &f, &lights, false);
    f.zone = zone;
    f.payloads.extend(
        module_local_lights::transport_payloads(&lights.manifest, &lights.package).unwrap(),
    );
    (f, lights)
}

#[test]
fn closed_source_package_exports_reopens_and_preserves_originals_and_phase() {
    let (f, lights) = fixture_with_lights();
    let temp = Temp::new();
    let exported = f.build(&temp.0.join("export")).unwrap();
    let loaded = module_runtime::load(
        &exported.package_path,
        f.zone.head().base.terrain.area_id,
        Some(&temp.0.join("cache")),
    )
    .unwrap();
    assert_eq!(loaded.manifest.compatibility.module_api_version, 5);
    assert_eq!(loaded.manifest.local_light_packages.len(), 1);
    assert!(
        loaded
            .manifest
            .files
            .iter()
            .all(|f| !f.path.ends_with(".js"))
    );
    let packages = loaded.local_lights().unwrap();
    let package = &packages[&lights.package.receipt_hash()];
    assert_eq!(
        package.source_file("src/light.js"),
        Some(source_fixture::ORIGINAL)
    );
    assert_eq!(package.license(), lights.package.license());
    assert_eq!(
        package.descriptor().document().lights[0].phase.to_bits(),
        0.915_999_999_999_999_9_f64.to_bits()
    );
    assert_eq!(loaded.zone.head(), f.zone.head());
}

#[test]
fn source_object_copy_exports_its_light_package_and_reopens_it() {
    use forge_project::zone_authoring::{
        session::{ZoneAction, ZoneRequest},
        source_objects::{SourceObjectEdit, SourceObjectState},
    };

    let mut f = Fixture::new(false);
    let lights = lights();
    let asset = &f.catalog.assets[0];
    let source = ManualPlacement {
        id: EntityId::deterministic("source-without-light"),
        name: "Source fixture".into(),
        asset_id: asset.id,
        asset_hash: asset.payload.sha256,
        physics: None,
        particles: None,
        local_lights: None,
        transform: Transform::IDENTITY,
        locked: false,
    };
    let mut copy = source.clone();
    copy.id = EntityId::deterministic("source-copy-with-light");
    copy.local_lights = Some(pin(&lights, source.asset_hash));
    let mut corrections = f.zone.head().corrections.clone();
    corrections.source_object_edits = Some(vec![SourceObjectEdit {
        source,
        state: SourceObjectState::Removed,
        copies: vec![copy.clone()],
    }]);
    f.zone
        .execute(&ZoneRequest {
            command_id: CommandId::deterministic("copy-with-source-lights"),
            expected_hash: f.zone.head().content_hash().unwrap(),
            expected_revision: f.zone.head().revision,
            action: ZoneAction::edit(corrections),
        })
        .unwrap();
    f.payloads.extend(
        module_local_lights::transport_payloads(&lights.manifest, &lights.package).unwrap(),
    );
    let temp = Temp::new();
    let exported = f.build(&temp.0.join("export")).unwrap();
    let loaded = module_runtime::load(
        &exported.package_path,
        f.zone.head().base.terrain.area_id,
        Some(&temp.0.join("cache")),
    )
    .unwrap();
    assert_eq!(loaded.manifest.local_light_packages.len(), 1);
    assert_eq!(loaded.local_lights().unwrap().len(), 1);
    assert_eq!(
        loaded
            .zone
            .head()
            .corrections
            .source_object_edits
            .as_ref()
            .unwrap()[0]
            .copies,
        vec![copy]
    );
}

#[test]
fn source_transport_and_binding_corruption_are_refused_without_silent_drop() {
    let (f, lights) = fixture_with_lights();
    let temp = Temp::new();
    let member = f
        .payloads
        .keys()
        .find(|p| p.ends_with(".source-data"))
        .unwrap()
        .clone();
    let mut missing = Fixture::new(false);
    missing.zone = ZoneEditSession::from_archive_bytes(&f.zone.archive_bytes().unwrap()).unwrap();
    missing.payloads = f.payloads.clone();
    missing.payloads.remove(&member);
    assert!(missing.build(&temp.0.join("missing")).is_err());
    missing.payloads = f.payloads.clone();
    missing.payloads.get_mut(&member).unwrap()[0] = b'x';
    assert!(missing.build(&temp.0.join("corrupt")).is_err());
    missing.payloads = f.payloads.clone();
    missing.payloads.insert(
        format!("{}unused.source-data", module_local_lights::PREFIX),
        b"b64:eA==".to_vec(),
    );
    assert!(missing.build(&temp.0.join("extra")).is_err());
    let mut invalid = Fixture::new(false);
    let mut zone =
        ZoneEditSession::from_archive_bytes(&invalid.zone.archive_bytes().unwrap()).unwrap();
    attach(&mut zone, &invalid, &lights, true);
    invalid.zone = zone;
    invalid.payloads.extend(
        module_local_lights::transport_payloads(&lights.manifest, &lights.package).unwrap(),
    );
    assert!(invalid.build(&temp.0.join("unknown-source")).is_err());
}

#[test]
fn a_secondary_area_can_own_the_only_light_package_and_shared_areas_deduplicate_it() {
    for primary in [false, true] {
        let mut f = Fixture::new(false);
        let lights = lights();
        if primary {
            let mut zone =
                ZoneEditSession::from_archive_bytes(&f.zone.archive_bytes().unwrap()).unwrap();
            attach(&mut zone, &f, &lights, false);
            f.zone = zone;
        }
        let mut snapshot = aggregate_snapshot(&mut f);
        let second = &mut snapshot.zones[1];
        let mut zone = ZoneEditSession::from_archive_bytes(&second.bytes).unwrap();
        attach(&mut zone, &f, &lights, false);
        second.head_hash = zone.head().content_hash().unwrap();
        second.bytes = zone.archive_bytes().unwrap();
        f.payloads.extend(
            module_local_lights::transport_payloads(&lights.manifest, &lights.package).unwrap(),
        );
        let temp = Temp::new();
        let exported = build_module_with_origin(
            &temp.0.join("export"),
            &snapshot,
            Fixture::metadata(),
            f.catalog,
            f.payloads,
            None,
        )
        .unwrap();
        for area in &snapshot.zones {
            let loaded = module_runtime::load(
                &exported.package_path,
                area.area_id,
                Some(&temp.0.join("cache")),
            )
            .unwrap();
            assert_eq!(loaded.manifest.local_light_packages.len(), 1);
            assert_eq!(
                loaded.manifest.local_light_packages[0].areas.len(),
                if primary { 2 } else { 1 }
            );
            assert_eq!(
                loaded.local_lights().unwrap().len(),
                usize::from(
                    primary && area.area_id == snapshot.primary_area
                        || area.area_id == snapshot.zones[1].area_id
                )
            );
        }
    }
}

#[test]
fn history_only_packages_remain_closed_and_invalid_historical_sources_are_refused() {
    for wrong in [false, true] {
        let mut f = Fixture::new(false);
        let lights = lights();
        let mut zone =
            ZoneEditSession::from_archive_bytes(&f.zone.archive_bytes().unwrap()).unwrap();
        attach(&mut zone, &f, &lights, wrong);
        let id = zone.head().corrections.placements.as_ref().unwrap()[0].id;
        let remove = zone
            .head()
            .prepare_placement(
                CommandId::deterministic("remove-source-lights"),
                PlacementEdit::Remove { id },
            )
            .unwrap();
        zone.execute(&remove).unwrap();
        f.zone = zone;
        f.payloads.extend(
            module_local_lights::transport_payloads(&lights.manifest, &lights.package).unwrap(),
        );
        let temp = Temp::new();
        let exported = f.build(&temp.0.join("export"));
        if wrong {
            assert!(exported.is_err());
            continue;
        }
        let mut loaded = module_runtime::load(
            &exported.unwrap().package_path,
            f.zone.head().base.terrain.area_id,
            Some(&temp.0.join("cache")),
        )
        .unwrap();
        assert!(
            loaded
                .zone
                .head()
                .corrections
                .placements
                .as_ref()
                .unwrap()
                .is_empty()
        );
        assert_eq!(loaded.local_lights().unwrap().len(), 1);
        use forge_project::zone_authoring::session::{ZoneAction, ZoneRequest};
        loaded
            .zone
            .execute(&ZoneRequest {
                command_id: CommandId::deterministic("undo-light-removal"),
                expected_hash: loaded.zone.head().content_hash().unwrap(),
                expected_revision: loaded.zone.head().revision,
                action: ZoneAction::Undo,
            })
            .unwrap();
        assert!(
            loaded.zone.head().corrections.placements.as_ref().unwrap()[0]
                .local_lights
                .is_some()
        );
        // A changed authoring head is no longer the installed immutable revision.
        assert!(loaded.local_lights().is_err());
    }
}

#[test]
fn old_module_apis_refuse_even_an_explicit_empty_light_package_field() {
    use forge_project::module_package::ModulePackageManifest;
    let f = Fixture::new(false);
    let temp = Temp::new();
    let exported = f.build(&temp.0.join("export")).unwrap();
    let loaded = module_runtime::load(
        &exported.package_path,
        exported.area_id,
        Some(&temp.0.join("cache")),
    )
    .unwrap();
    for api in 1..=4 {
        let mut value = serde_json::to_value(&loaded.manifest).unwrap();
        value["compatibility"]["module_api_version"] = json!(api);
        assert!(ModulePackageManifest::from_bytes(&serde_json::to_vec(&value).unwrap()).is_ok());
        value["local_light_packages"] = json!([]);
        assert!(ModulePackageManifest::from_bytes(&serde_json::to_vec(&value).unwrap()).is_err());
    }
}
