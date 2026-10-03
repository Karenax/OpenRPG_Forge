use super::super::scene;
use super::*;
use forge_core::bytes_sha256;
use serde_json::json;

mod selection;

fn descriptor(phase: f64) -> SourceLocalLights {
    let mut frame = Mat4::IDENTITY.to_cols_array().map(f64::from);
    frame[12] = 12.0;
    let document = json!({
        "schema":"forge.source-local-lights","version":1,
        "source_commit":"1438b1abfcaee3267092b75573014f4d9b4a983c",
        "capture_mode":"fixture","gpu_initialized":false,"browser_required":false,
        "registration_scope":"fixture","full_application_inventory":false,
        "missing_owners":["outside-fixture"],"phase_reacquisition_reproducible":false,
        "source_defaults":{"enabled":true,"scale":2.0,"flicker":0.17},
        "coordinate_system":{"units":"metres","up":"+Y","handedness":"right"},
        "frames":[{"id":"root","source_object":"root","parent":null},
            {"id":"owner","source_object":"owner","parent":"root","world_from_local":frame}],
        "sets":{"one":{"start_order":0,"count":1}},
        "source_inputs":[{"path":"source.js","bytes":1,"sha256":bytes_sha256(b"x")}],
        "lights":[{
            "id":bytes_sha256(b"emitter"),"registration_order":0,"owner_frame":"owner",
            "source_kind":"provenance-only","source_function":"capture",
            "position_local_m":[1.0,2.0,3.0],"acquired_position_world_m":[13.0,2.0,3.0],
            "direction_local":[0.0,-1.0,0.0],"acquired_direction_world":[0.0,-1.0,0.0],
            "color_linear_rgb":[1.0,0.7,0.4],"intensity_at_1m":1.5,"range_m":10.0,
            "cos_inner":0.8,"cos_outer":0.3,"enabled":null,"scale":0.0,"flicker":null,
            "phase":phase,"phase_source":"acquired","source_has_pose_update":true
        }],"runtime_selection_executed":false,"renderer_parity":false,"studio_qualified":false
    });
    let bytes = serde_json::to_vec(&document).unwrap();
    SourceLocalLights::load(&bytes, bytes_sha256(&bytes)).unwrap()
}

fn prepared(base: u64, phase: f64) -> PreparedSourceLights {
    let descriptor = descriptor(phase);
    PreparedSourceLights::prepare(
        &descriptor,
        AssetId::deterministic("fixture-asset"),
        bytes_sha256(b"package"),
        base,
    )
    .unwrap()
}

fn scene() -> (App, Entity, Entity, BTreeMap<String, Entity>) {
    let mut app = App::new();
    app.add_plugins((MinimalPlugins, TransformPlugin));
    let camera = app
        .world_mut()
        .spawn((Camera3d::default(), Transform::IDENTITY))
        .id();
    let root = app
        .world_mut()
        .spawn(Transform::from_xyz(10.0, 0.0, 0.0))
        .id();
    let owner = app
        .world_mut()
        .spawn((ChildOf(root), Transform::from_xyz(2.0, 0.0, 0.0)))
        .id();
    app.update();
    (
        app,
        camera,
        owner,
        BTreeMap::from([("root".into(), root), ("owner".into(), owner)]),
    )
}

#[test]
fn source_local_lights_projection_follows_live_parent_without_double_transform_or_phase_reset() {
    let prepared = prepared(4, 0.915_999_999_999_999_9);
    let (mut app, camera, owner, bindings) = scene();
    let mut resident = prepared
        .stage(
            app.world_mut(),
            EntityId::deterministic("instance"),
            &bindings,
        )
        .unwrap();
    assert!(!resident.is_synchronized(app.world()));
    app.update();
    assert!(resident.is_synchronized(app.world()));
    let initial = scene::harvest(app.world_mut(), camera, resident.registration_orders())
        .unwrap()
        .1;
    assert_eq!(initial[0].position, DVec3::new(13.0, 2.0, 3.0));
    assert_eq!(
        initial[0].phase.to_bits(),
        0.915_999_999_999_999_9_f64.to_bits()
    );
    assert!(initial[0].enabled);
    assert_eq!(initial[0].scale.to_bits(), 0.0_f64.to_bits());
    assert_eq!(initial[0].flicker.to_bits(), 0.17_f64.to_bits());
    app.world_mut()
        .get_mut::<Transform>(owner)
        .unwrap()
        .translation
        .x = 20.0;
    app.world_mut().get_mut::<Transform>(owner).unwrap().scale = Vec3::new(2.0, 3.0, 4.0);
    app.update();
    let moved = scene::harvest(app.world_mut(), camera, resident.registration_orders())
        .unwrap()
        .1;
    assert_eq!(moved[0].position, DVec3::new(32.0, 6.0, 12.0));
    assert_eq!(moved[0].direction, Some(DVec3::NEG_Y));
    assert_eq!(moved[0].phase.to_bits(), initial[0].phase.to_bits());
    assert!(resident.is_synchronized(app.world()));
    resident.retire(app.world_mut());
    assert!(!resident.is_synchronized(app.world()));
    assert!(app.world().get_entity(owner).is_ok());
    assert!(app.world().get_entity(bindings["root"]).is_ok());
    assert!(scene::harvest(app.world_mut(), camera, &[4]).is_err());
}

#[test]
fn source_local_lights_projection_refuses_missing_aliased_foreign_and_conflicting_owners_without_mutation()
 {
    let prepared = prepared(4, 1.0);
    for change in 0..4 {
        let (mut app, _, owner, mut bindings) = scene();
        match change {
            0 => {
                bindings.remove("owner");
            }
            1 => {
                bindings.insert("owner".into(), bindings["root"]);
            }
            2 => {
                app.world_mut().entity_mut(owner).remove::<ChildOf>();
            }
            _ => {
                app.world_mut().spawn((
                    Transform::IDENTITY,
                    SourceLight {
                        registration_order: 4,
                        parameters: prepared.lights[0].parameters.clone(),
                    },
                ));
            }
        }
        let entities = app.world().entities().len();
        assert!(
            prepared
                .stage(
                    app.world_mut(),
                    EntityId::deterministic("instance"),
                    &bindings
                )
                .is_err()
        );
        assert_eq!(
            app.world().entities().len(),
            entities,
            "invalid binding {change}"
        );
    }
}

#[test]
fn source_local_lights_projection_replaces_atomically_and_retires_only_owned_emitters() {
    let (mut app, camera, owner, bindings) = scene();
    let mut resident = prepared(4, 1.0)
        .stage(
            app.world_mut(),
            EntityId::deterministic("instance"),
            &bindings,
        )
        .unwrap();
    app.update();
    let old = resident.lights[0].entity;
    let foreign = app.world_mut().spawn(Transform::IDENTITY).id();
    let replacement = prepared(4, 2.0);
    let mut missing = bindings.clone();
    missing.remove("owner");
    assert!(
        resident
            .replace(app.world_mut(), &replacement, &missing)
            .is_err()
    );
    assert_eq!(resident.lights[0].entity, old);
    assert!(resident.is_synchronized(app.world()));
    assert_eq!(
        app.world()
            .get::<SourceLight>(old)
            .unwrap()
            .parameters
            .phase
            .to_bits(),
        1.0_f64.to_bits()
    );
    resident
        .replace(app.world_mut(), &replacement, &bindings)
        .unwrap();
    app.update();
    assert!(app.world().get_entity(old).is_err());
    assert!(resident.is_synchronized(app.world()));
    assert_eq!(
        scene::harvest(app.world_mut(), camera, &[4]).unwrap().1[0]
            .phase
            .to_bits(),
        2.0_f64.to_bits()
    );
    app.world_mut().despawn(owner);
    assert!(!resident.is_synchronized(app.world()));
    assert!(scene::harvest(app.world_mut(), camera, &[4]).is_err());
    resident.retire(app.world_mut());
    assert!(app.world().get_entity(foreign).is_ok());
    assert!(app.world().get_entity(bindings["root"]).is_ok());
}

#[test]
fn source_local_lights_projection_refuses_compromised_old_emitter_even_with_a_new_order_range() {
    let (mut app, _, _, bindings) = scene();
    let mut resident = prepared(4, 1.0)
        .stage(
            app.world_mut(),
            EntityId::deterministic("instance"),
            &bindings,
        )
        .unwrap();
    app.update();
    let old = resident.lights[0].entity;
    app.world_mut()
        .get_mut::<SourceLightIdentity>(old)
        .unwrap()
        .instance_id = EntityId::deterministic("foreign");
    let count = app.world().entities().len();
    assert!(
        resident
            .replace(app.world_mut(), &prepared(8, 2.0), &bindings)
            .is_err()
    );
    assert_eq!(app.world().entities().len(), count);
    assert_eq!(resident.lights[0].entity, old);
    assert!(!resident.is_synchronized(app.world()));
    resident.retire(app.world_mut());
    assert!(
        app.world().get_entity(old).is_ok(),
        "changed identity is not ours to delete"
    );
}

#[test]
fn source_local_lights_projection_refuses_snapshot_mutation_and_stale_owner_transform() {
    let (mut app, _, owner, bindings) = scene();
    app.world_mut()
        .get_mut::<Transform>(owner)
        .unwrap()
        .translation
        .x = 20.0;
    let prepared = prepared(4, 1.0);
    let count = app.world().entities().len();
    assert!(
        prepared
            .stage(
                app.world_mut(),
                EntityId::deterministic("instance"),
                &bindings
            )
            .is_err()
    );
    assert_eq!(app.world().entities().len(), count);
    app.update();
    let mut resident = prepared
        .stage(
            app.world_mut(),
            EntityId::deterministic("instance"),
            &bindings,
        )
        .unwrap();
    app.update();
    assert!(resident.is_synchronized(app.world()));
    let emitter = resident.lights[0].entity;
    app.world_mut()
        .get_mut::<SourceLight>(emitter)
        .unwrap()
        .parameters
        .phase = 2.0;
    assert!(!resident.is_synchronized(app.world()));
    app.world_mut()
        .get_mut::<SourceLight>(emitter)
        .unwrap()
        .parameters
        .phase = 1.0;
    assert!(resident.is_synchronized(app.world()));
    app.world_mut()
        .get_mut::<Transform>(owner)
        .unwrap()
        .translation
        .x = 30.0;
    assert!(!resident.is_synchronized(app.world()));
    app.update();
    assert!(resident.is_synchronized(app.world()));
    resident.retire(app.world_mut());
}
