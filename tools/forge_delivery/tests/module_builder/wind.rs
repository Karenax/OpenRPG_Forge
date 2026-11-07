use super::*;
use forge_project::{
    module_package::{MODULE_OCEAN_SURFACE_API_VERSION, MODULE_SCENE_WIND_API_VERSION},
    zone_authoring::{
        session::{ZoneAction, ZoneRequest},
        wind::SceneWindSettings,
    },
};

fn authored() -> SceneWindSettings {
    SceneWindSettings {
        schema_version: 1,
        heading_degrees: 90.0,
        speed_mps: 7.0,
    }
}

#[test]
fn exported_module_installs_scene_wind_and_refuses_older_api_even_after_undo() {
    let temp = Temp::new();
    let mut fixture = Fixture::new(false);
    let request = fixture
        .zone
        .head()
        .prepare_wind(Some(authored()), CommandId::deterministic("module-wind"))
        .unwrap();
    fixture.zone.execute(&request).unwrap();
    let exported = fixture.build(&temp.0.join("wind")).unwrap();
    let loaded = module_runtime::load(
        &exported.package_path,
        exported.area_id,
        Some(&temp.0.join("cache")),
    )
    .unwrap();
    assert_eq!(loaded.zone.head().corrections.wind, Some(authored()));
    assert_eq!(
        loaded.manifest.compatibility.module_api_version,
        MODULE_SCENE_WIND_API_VERSION
    );
    let mut downgraded = loaded.manifest.clone();
    downgraded.compatibility.module_api_version = MODULE_OCEAN_SURFACE_API_VERSION;
    assert!(
        downgraded
            .validate_zone_archive(exported.area_id, &loaded.archive)
            .is_err()
    );

    let undo = ZoneRequest {
        command_id: CommandId::deterministic("module-wind-undo"),
        expected_hash: fixture.zone.head().content_hash().unwrap(),
        expected_revision: fixture.zone.head().revision,
        action: ZoneAction::Undo,
    };
    fixture.zone.execute(&undo).unwrap();
    let undone = fixture.build(&temp.0.join("undone")).unwrap();
    let loaded = module_runtime::load(
        &undone.package_path,
        undone.area_id,
        Some(&temp.0.join("cache2")),
    )
    .unwrap();
    assert!(loaded.zone.head().corrections.wind.is_none());
    assert!(loaded.zone.has_wind_history());
    assert_eq!(
        loaded.manifest.compatibility.module_api_version,
        MODULE_SCENE_WIND_API_VERSION
    );
}

#[test]
fn secondary_area_wind_promotes_the_entire_module_api() {
    let temp = Temp::new();
    let mut fixture = Fixture::new(false);
    let mut snapshot = aggregate_snapshot(&mut fixture);
    let primary_area = snapshot.primary_area;
    let secondary = snapshot
        .zones
        .iter_mut()
        .find(|zone| zone.area_id != primary_area)
        .unwrap();
    let mut session = ZoneEditSession::from_archive_bytes(&secondary.bytes).unwrap();
    let request = session
        .head()
        .prepare_wind(Some(authored()), CommandId::deterministic("secondary-wind"))
        .unwrap();
    session.execute(&request).unwrap();
    secondary.head_hash = session.head().content_hash().unwrap();
    secondary.bytes = session.archive_bytes().unwrap();
    let secondary_id = secondary.area_id;
    let exported = build_module_with_origin(
        &temp.0.join("multi"),
        &snapshot,
        Fixture::metadata(),
        fixture.catalog,
        fixture.payloads,
        None,
    )
    .unwrap();
    let loaded = module_runtime::load(
        &exported.package_path,
        secondary_id,
        Some(&temp.0.join("cache")),
    )
    .unwrap();
    assert_eq!(loaded.zone.head().corrections.wind, Some(authored()));
    assert_eq!(
        loaded.manifest.compatibility.module_api_version,
        MODULE_SCENE_WIND_API_VERSION
    );
}
