use super::*;
use crate::source_lighting::local_lights::{
    Flashlight, Settings, collection::Collection, scene::SourceLight,
};
use bevy::math::DVec3;
use forge_core::AssetId;
use forge_project::zone_authoring::placements::{LocalLightAnchor, LocalLightPackageReference};

fn placement(descriptor: &SourceLocalLights, name: &str) -> ManualPlacement {
    let carrier = forge_core::bytes_sha256(b"carrier");
    ManualPlacement {
        id: EntityId::deterministic(name),
        name: "Model".into(),
        asset_id: AssetId::deterministic("asset"),
        asset_hash: carrier,
        physics: None,
        particles: None,
        local_lights: Some(Box::new(LocalLightPackageReference {
            schema_version: 1,
            carrier_sha256: carrier,
            manifest_sha256: forge_core::bytes_sha256(b"package"),
            descriptor_sha256: descriptor.content_hash(),
            light_ids: vec![forge_core::bytes_sha256(b"emitter")],
            anchors: vec![
                LocalLightAnchor {
                    frame_id: "owner".into(),
                    transform: forge_core::Transform::from_translation(forge_core::Vec3::new(
                        2.0, 0.0, 0.0,
                    )),
                },
                LocalLightAnchor {
                    frame_id: "root".into(),
                    transform: forge_core::Transform::IDENTITY,
                },
            ],
        })),
        transform: forge_core::Transform::from_translation(forge_core::Vec3::new(10.0, 0.0, 0.0)),
        locked: false,
    }
}
fn prepare(
    descriptor: &SourceLocalLights,
    p: &ManualPlacement,
    base: u64,
) -> PreparedPlacementLights {
    PreparedPlacementLights::prepare(descriptor, forge_core::bytes_sha256(b"package"), p, base)
        .unwrap()
}
fn app() -> App {
    let mut app = App::new();
    app.add_plugins((MinimalPlugins, TransformPlugin));
    app
}
fn root(app: &mut App, p: &ManualPlacement) -> Entity {
    let entity = app
        .world_mut()
        .spawn((
            ManualPlacementProjection(p.clone()),
            crate::domain_transform_to_bevy(p.transform),
        ))
        .id();
    app.update();
    entity
}

#[test]
fn saved_anchors_follow_actual_canonical_root_preserving_source_phase_and_parameters() {
    let descriptor = super::super::tests::descriptor(0.915_999_999_999_999_9);
    let p = placement(&descriptor, "source-model");
    let mut app = app();
    let root = root(&mut app, &p);
    let camera = app
        .world_mut()
        .spawn((Camera3d::default(), Transform::IDENTITY))
        .id();
    let mut resident = prepare(&descriptor, &p, 4)
        .stage(app.world_mut(), root)
        .unwrap();
    assert!(!resident.is_synchronized(app.world()));
    app.update();
    assert!(resident.is_synchronized(app.world()));
    assert_eq!(resident.registration_orders(), &[4]);
    let lights =
        crate::source_lighting::local_lights::scene::harvest(app.world_mut(), camera, &[4])
            .unwrap()
            .1;
    assert_eq!(lights[0].position, DVec3::new(13.0, 2.0, 3.0));
    assert_eq!(
        lights[0].phase.to_bits(),
        0.915_999_999_999_999_9_f64.to_bits()
    );
    app.world_mut().insert_resource(Collection::default());
    app.world_mut()
        .resource_scope(|world, mut collection: Mut<Collection>| {
            collection
                .collect(
                    world,
                    camera,
                    Settings {
                        delta_seconds: 1.0 / 60.0,
                        night: 1.0,
                        enabled: true,
                        strength: 1.0,
                        flashlight: Flashlight::default(),
                    },
                    &[4],
                )
                .unwrap();
        });
    assert!(
        app.world()
            .resource::<Collection>()
            .for_camera(camera, Mat4::IDENTITY)
            .is_ok()
    );
    let owner = resident.anchors["owner"];
    app.world_mut()
        .get_mut::<Transform>(owner)
        .unwrap()
        .translation
        .x = 3.0;
    app.update();
    assert!(!resident.is_synchronized(app.world()));
    resident.retire(app.world_mut()).unwrap();
    assert!(
        app.world()
            .resource::<Collection>()
            .for_camera(camera, Mat4::IDENTITY)
            .is_err()
    );
    assert!(app.world().get_entity(root).is_ok());
    assert_eq!(
        app.world_mut()
            .query::<&SourceLight>()
            .iter(app.world())
            .count(),
        0
    );
}

#[test]
fn saved_companion_refuses_pins_incomplete_extra_frames_and_root_aliases() {
    let descriptor = super::super::tests::descriptor(0.2);
    let p = placement(&descriptor, "guarded");
    for index in 0..5 {
        let mut bad = p.clone();
        let reference = bad.local_lights.as_mut().unwrap();
        match index {
            0 => reference.manifest_sha256 = forge_core::bytes_sha256(b"other"),
            1 => reference.descriptor_sha256 = forge_core::bytes_sha256(b"other"),
            2 => reference.anchors.retain(|a| a.frame_id != "root"),
            3 => reference.anchors.push(LocalLightAnchor {
                frame_id: "unused".into(),
                transform: forge_core::Transform::IDENTITY,
            }),
            _ => reference.light_ids = vec![forge_core::bytes_sha256(b"foreign emitter")],
        }
        assert!(
            PreparedPlacementLights::prepare(
                &descriptor,
                forge_core::bytes_sha256(b"package"),
                &bad,
                4
            )
            .is_err()
        );
    }
    let mut app = app();
    let root_id = root(&mut app, &p);
    root(&mut app, &p);
    assert!(
        prepare(&descriptor, &p, 4)
            .stage(app.world_mut(), root_id)
            .is_err()
    );
    assert_eq!(
        app.world_mut()
            .query::<&AnchorIdentity>()
            .iter(app.world())
            .count(),
        0
    );
}

#[test]
fn stage_conflict_rolls_back_derived_anchors_and_retirement_protects_foreign_descendants() {
    let descriptor = super::super::tests::descriptor(0.2);
    let p = placement(&descriptor, "first");
    let copy = placement(&descriptor, "copy");
    let mut app = app();
    let first = root(&mut app, &p);
    let second = root(&mut app, &copy);
    let mut resident = prepare(&descriptor, &p, 4)
        .stage(app.world_mut(), first)
        .unwrap();
    app.update();
    assert!(
        prepare(&descriptor, &copy, 4)
            .stage(app.world_mut(), second)
            .is_err()
    );
    assert_eq!(
        app.world_mut()
            .query::<&AnchorIdentity>()
            .iter(app.world())
            .filter(|a| a.instance == copy.id)
            .count(),
        0
    );
    assert!(resident.is_synchronized(app.world()));
    let emitter = resident.lights.lights[0].entity;
    let foreign = app
        .world_mut()
        .spawn((Transform::IDENTITY, ChildOf(emitter)))
        .id();
    assert!(resident.retire(app.world_mut()).is_err());
    assert!(app.world().get_entity(foreign).is_ok());
    assert!(app.world().get_entity(emitter).is_ok());
    app.world_mut().entity_mut(foreign).remove::<ChildOf>();
    resident.retire(app.world_mut()).unwrap();
    assert!(app.world().get_entity(foreign).is_ok());
    assert!(app.world().get_entity(first).is_ok());
    assert_eq!(
        app.world_mut()
            .query::<&AnchorIdentity>()
            .iter(app.world())
            .count(),
        0
    );
    assert!(!resident.is_synchronized(app.world()));
}
