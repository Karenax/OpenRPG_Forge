use super::*;
use crate::source_lighting::local_lights::{collection::Collection, scene::SourceLight};
use crate::source_sky::{Settings as SkySettings, SkyMaterial};
use bevy::time::TimeUpdateStrategy;
use forge_assets::source_local_lights::SourceLocalLights;
use forge_core::{AssetId, bytes_sha256};
use forge_project::zone_authoring::placements::{LocalLightAnchor, LocalLightPackageReference};
use std::time::Duration;

fn descriptor() -> SourceLocalLights {
    let original = super::super::super::tests::descriptor(0.915_999_999_999_999_9);
    let mut value = serde_json::to_value(original.document()).unwrap();
    value["lights"][0]["scale"] = serde_json::json!(1.0);
    value["lights"][0]["flicker"] = serde_json::json!(0.0);
    let bytes = serde_json::to_vec(&value).unwrap();
    SourceLocalLights::load(&bytes, bytes_sha256(&bytes)).unwrap()
}
fn placement(name: &str) -> ManualPlacement {
    let descriptor = descriptor();
    let carrier = bytes_sha256(b"carrier");
    ManualPlacement {
        id: EntityId::deterministic(name),
        name: name.into(),
        asset_id: AssetId::deterministic("carrier"),
        asset_hash: carrier,
        transform: forge_core::Transform::IDENTITY,
        locked: false,
        physics: None,
        particles: None,
        local_lights: Some(Box::new(LocalLightPackageReference {
            schema_version: 1,
            carrier_sha256: carrier,
            manifest_sha256: bytes_sha256(b"package"),
            descriptor_sha256: descriptor.content_hash(),
            light_ids: vec![bytes_sha256(b"emitter")],
            anchors: vec![
                LocalLightAnchor {
                    frame_id: "owner".into(),
                    transform: forge_core::Transform::IDENTITY,
                },
                LocalLightAnchor {
                    frame_id: "root".into(),
                    transform: forge_core::Transform::IDENTITY,
                },
            ],
        })),
    }
}
fn prepared(placements: &[ManualPlacement]) -> PreparedHost {
    PreparedHost::prepare(placements, |p, base| {
        PreparedPlacementLights::prepare(&descriptor(), bytes_sha256(b"package"), p, base)
    })
    .unwrap()
}
fn app(placements: &[ManualPlacement]) -> (App, Entity, Handle<SkyMaterial>) {
    let mut app = App::new();
    app.add_plugins((MinimalPlugins, TransformPlugin))
        .insert_resource(TimeUpdateStrategy::ManualDuration(Duration::from_secs_f64(
            1.0 / 60.0,
        )))
        .init_resource::<Assets<SkyMaterial>>();
    let camera = app
        .world_mut()
        .spawn((Camera3d::default(), Transform::IDENTITY))
        .id();
    let sky = app
        .world_mut()
        .resource_mut::<Assets<SkyMaterial>>()
        .add(SkyMaterial {
            atmosphere: [Vec4::ZERO; 4],
            settings: SkySettings {
                moon_disk: Vec4::ZERO,
                stars_time_night: Vec4::Z,
            },
            transmittance: default(),
            sky: default(),
            clouds: [Vec4::ZERO; 5],
            panorama: None,
            cloud_view: None,
        });
    app.world_mut().spawn(MeshMaterial3d(sky.clone()));
    for p in placements {
        app.world_mut().spawn((
            ManualPlacementProjection(p.clone()),
            crate::domain_transform_to_bevy(p.transform),
        ));
    }
    prepared(placements).install_scene(&mut app);
    (app, camera, sky)
}
fn count(app: &App, camera: Entity) -> i32 {
    let global = app
        .world()
        .get::<GlobalTransform>(camera)
        .unwrap()
        .to_matrix();
    app.world()
        .resource::<Collection>()
        .for_camera(camera, global)
        .unwrap()
        .1
        .count
}

#[test]
fn accepted_light_replacement_retires_owned_emitters_and_refuses_stale_identity() {
    let placements = vec![placement("replace-one")];
    let (mut app, camera, _) = app(&placements);
    app.update();
    app.update();
    app.update();
    assert_eq!(count(&app, camera), 1);
    let identity = app.world().resource::<Status>().identity.unwrap();
    assert!(
        prepared(&[])
            .replace(app.world_mut(), bytes_sha256(b"foreign"))
            .is_err()
    );
    assert_eq!(app.world().resource::<Status>().identity, Some(identity));
    assert_eq!(count(&app, camera), 1);
    assert!(suspend_authoring(app.world_mut(), bytes_sha256(b"foreign")).is_err());
    suspend_authoring(app.world_mut(), identity).unwrap();
    let root = app
        .world_mut()
        .query_filtered::<Entity, With<ManualPlacementProjection>>()
        .single(app.world())
        .unwrap();
    app.world_mut().despawn(root);
    app.update();
    app.update();
    assert!(!app.world().resource::<Status>().ready);
    assert!(app.world().resource::<Status>().error.is_none());
    prepared(&[]).replace(app.world_mut(), identity).unwrap();
    assert!(!app.world().resource::<Status>().ready);
    app.update();
    app.update();
    assert!(app.world().resource::<Status>().ready);
    assert_eq!(count(&app, camera), 0);
    assert_eq!(app.world().resource::<Status>().staged_placements, 0);
}

#[test]
fn saved_light_host_uses_stable_document_inventory_and_waits_for_propagation() {
    let mut placements = vec![placement("one"), placement("two")];
    let first = prepared(&placements);
    placements.reverse();
    let second = prepared(&placements);
    assert_eq!(first.orders, second.orders);
    assert_eq!(first.orders, vec![0, 1]);
    assert_eq!(
        first.pending.keys().collect::<Vec<_>>(),
        second.pending.keys().collect::<Vec<_>>()
    );
    let (mut app, camera, _) = app(&placements);
    app.update();
    assert!(!app.world().resource::<Status>().ready);
    app.update();
    assert!(app.world().resource::<Status>().ready);
    assert_eq!(app.world().resource::<Status>().expected_emitters, 2);
    assert_eq!(count(&app, camera), 2);
    let phases: Vec<_> = app
        .world_mut()
        .query::<&SourceLight>()
        .iter(app.world())
        .map(|l| l.parameters.phase.to_bits())
        .collect();
    assert_eq!(phases, vec![0.915_999_999_999_999_9_f64.to_bits(); 2]);
}

#[test]
fn saved_light_host_tracks_actual_sky_enablement_and_camera_and_keeps_flashlight_history() {
    let (mut app, camera, sky) = app(&[placement("one")]);
    app.update();
    app.update();
    assert_eq!(count(&app, camera), 1);
    app.world_mut()
        .resource_mut::<Assets<SkyMaterial>>()
        .get_mut(&sky)
        .unwrap()
        .settings
        .stars_time_night
        .z = 0.0;
    app.update();
    assert_eq!(count(&app, camera), 0);
    app.world_mut()
        .resource_mut::<Assets<SkyMaterial>>()
        .get_mut(&sky)
        .unwrap()
        .settings
        .stars_time_night
        .z = 1.0;
    app.world_mut().resource_mut::<Controls>().enabled = false;
    app.update();
    assert_eq!(count(&app, camera), 0);
    {
        let mut controls = app.world_mut().resource_mut::<Controls>();
        controls.enabled = true;
        controls.flashlight_on = true;
    }
    app.update();
    assert_eq!(count(&app, camera), 2);
    app.world_mut()
        .get_mut::<Transform>(camera)
        .unwrap()
        .rotate_y(0.3);
    app.update();
    assert_eq!(count(&app, camera), 2);
    let world = app.world();
    assert!(
        world
            .resource::<Collection>()
            .for_camera(camera, Mat4::IDENTITY)
            .is_err()
    );
    // Reconciliation does not toggle the same flashlight state every frame.
    let packet = world
        .resource::<Collection>()
        .for_camera(
            camera,
            world.get::<GlobalTransform>(camera).unwrap().to_matrix(),
        )
        .unwrap()
        .1;
    let forward = world
        .get::<GlobalTransform>(camera)
        .unwrap()
        .forward()
        .as_dvec3();
    assert!((packet.flash_direction - forward).length() > 0.1);
}

#[test]
fn saved_light_host_refuses_missing_changed_or_foreign_emitters_and_duplicate_roots() {
    for corruption in 0..4 {
        let p = placement("one");
        let (mut app, camera, _) = app(std::slice::from_ref(&p));
        app.update();
        app.update();
        assert_eq!(count(&app, camera), 1);
        let light = app
            .world_mut()
            .query_filtered::<Entity, With<SourceLight>>()
            .single(app.world())
            .unwrap();
        match corruption {
            0 => {
                app.world_mut().despawn(light);
            }
            1 => {
                app.world_mut()
                    .get_mut::<SourceLight>(light)
                    .unwrap()
                    .parameters
                    .intensity += 1.0;
            }
            2 => {
                let mut source = app.world().get::<SourceLight>(light).unwrap().clone();
                source.registration_order = 99;
                app.world_mut().spawn((source, Transform::IDENTITY));
            }
            _ => {
                app.world_mut()
                    .spawn((ManualPlacementProjection(p), Transform::IDENTITY));
            }
        }
        app.update();
        let status = app.world().resource::<Status>();
        assert!(status.error.is_some(), "corruption {corruption}");
        assert!(!status.ready);
        assert!(
            app.world()
                .resource::<Collection>()
                .for_camera(camera, Mat4::IDENTITY)
                .is_err()
        );
    }
}

#[test]
fn saved_light_host_handles_pending_roots_and_refuses_ambiguous_frame_inputs() {
    let p = placement("one");
    let (mut app, camera, sky) = app(std::slice::from_ref(&p));
    let root = app
        .world_mut()
        .query_filtered::<Entity, With<ManualPlacementProjection>>()
        .single(app.world())
        .unwrap();
    app.world_mut().despawn(root);
    app.update();
    app.update();
    assert!(!app.world().resource::<Status>().ready);
    assert!(app.world().resource::<Status>().error.is_none());
    app.world_mut()
        .spawn((ManualPlacementProjection(p.clone()), Transform::IDENTITY));
    app.update();
    app.update();
    assert_eq!(count(&app, camera), 1);
    let capture = app
        .world_mut()
        .spawn((
            Camera3d::default(),
            Transform::IDENTITY,
            crate::scene_camera::OffscreenSceneCamera,
        ))
        .id();
    app.update();
    assert!(app.world().resource::<Status>().ready);
    app.world_mut()
        .entity_mut(capture)
        .remove::<crate::scene_camera::OffscreenSceneCamera>();
    app.update();
    assert!(app.world().resource::<Status>().error.is_some());
    let (mut other, _, _) = self::app(&[p]);
    other.world_mut().spawn(MeshMaterial3d(sky));
    other.update();
    other.update();
    assert!(other.world().resource::<Status>().error.is_some());
}

#[test]
fn saved_light_host_refuses_unknown_packages_and_duplicate_document_ids() {
    let p = placement("one");
    assert!(PreparedHost::from_packages(std::slice::from_ref(&p), &BTreeMap::new()).is_err());
    assert!(
        PreparedHost::prepare(&[p.clone(), p], |_, _| panic!(
            "duplicate must refuse before preparation"
        ))
        .is_err()
    );
}

#[test]
fn saved_light_host_dates_its_collection_and_refuses_a_second_scene_producer() {
    let (mut app, camera, _) = app(&[placement("one")]);
    app.update();
    app.update();
    app.world_mut()
        .insert_resource(bevy::diagnostic::FrameCount(99));
    super::update(app.world_mut());
    assert_eq!(count(&app, camera), 1);
    assert_eq!(app.world().resource::<Collection>().scene_frame(), Some(99));
    local_lights::collection::claim_producer(app.world_mut()).unwrap();
    assert!(local_lights::collection::claim_producer(app.world_mut()).is_err());
    app.world_mut().resource_mut::<Collection>().invalidate();
    assert_eq!(app.world().resource::<Collection>().scene_frame(), None);
}
