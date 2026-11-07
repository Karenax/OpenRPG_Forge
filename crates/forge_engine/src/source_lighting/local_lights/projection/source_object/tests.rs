use super::*;

#[test]
fn building_light_anchor_preserves_translation_yaw_and_scale_without_substitution() {
    let yaw = 1.234_f64;
    let frame = ConstructionFrame {
        translation_m: [12.0, 0.0, -34.0],
        rotation_xyzw: [0.0, (yaw / 2.0).sin(), 0.0, (yaw / 2.0).cos()],
        scale: [1.0, 1.0, 1.0],
    };
    let captured = DMat4::from_scale_rotation_translation(
        DVec3::ONE,
        DQuat::from_rotation_y(yaw),
        DVec3::from_array(frame.translation_m),
    )
    .to_cols_array();
    assert!(verify_anchor(&frame, captured).is_ok());
    for index in [0, 5, 12, 14] {
        let mut invalid = captured;
        invalid[index] += 0.001;
        assert!(verify_anchor(&frame, invalid).is_err());
    }
    let mut invalid = captured;
    invalid[2] = f64::NAN;
    assert!(verify_anchor(&frame, invalid).is_err());
    let singular = ConstructionFrame {
        scale: [0.0, 1.0, 1.0],
        ..frame
    };
    assert!(verify_anchor(&singular, captured).is_err());
}

fn plan(name: &str, base: u64) -> PreparedObjectLights {
    let descriptor = super::super::tests::descriptor(0.915_999_999_999_999_9);
    let placement = ManualPlacement {
        id: forge_core::EntityId::deterministic(name),
        name: "Independent model".into(),
        asset_id: AssetId::deterministic("independent-asset"),
        asset_hash: forge_core::bytes_sha256(b"independent-carrier"),
        transform: forge_core::Transform::from_translation(forge_core::Vec3::new(12.0, 0.0, 0.0)),
        physics: None,
        particles: None,
        local_lights: None,
        locked: false,
    };
    let lights = PreparedSourceLights::prepare_selected(
        &descriptor,
        placement.asset_id,
        forge_core::bytes_sha256(b"package"),
        base,
        Some(&[forge_core::bytes_sha256(b"emitter")]),
    )
    .unwrap();
    PreparedObjectLights {
        placement,
        frame: "owner".into(),
        lights,
    }
}

fn root(app: &mut App, placement: &ManualPlacement) -> Entity {
    app.world_mut()
        .spawn((
            crate::domain_transform_to_bevy(placement.transform),
            ManualPlacementProjection(placement.clone()),
        ))
        .id()
}

#[test]
fn acquired_object_lights_use_generic_roots_and_follow_canonical_edits_and_copies() {
    let mut app = App::new();
    app.add_plugins((MinimalPlugins, TransformPlugin));
    let camera = app
        .world_mut()
        .spawn((Camera3d::default(), Transform::IDENTITY))
        .id();
    let original = plan("independent-original", 0);
    let original_root = root(&mut app, &original.placement);
    app.update();
    let mut resident = original.stage(app.world_mut()).unwrap();
    app.update();
    assert!(resident.is_synchronized(app.world()));
    let before = super::super::super::scene::harvest(app.world_mut(), camera, &[0])
        .unwrap()
        .1;
    assert_eq!(before[0].position, DVec3::new(13.0, 2.0, 3.0));

    let mut copy = plan("independent-copy", 10);
    copy.placement.transform.translation_m.x += 7.0;
    let copy_root = root(&mut app, &copy.placement);
    app.update();
    let mut copy_resident = copy.stage(app.world_mut()).unwrap();
    app.update();
    let copied = super::super::super::scene::harvest(app.world_mut(), camera, &[0, 10])
        .unwrap()
        .1;
    assert_eq!(
        copied[1].position - before[0].position,
        DVec3::new(7.0, 0.0, 0.0)
    );
    assert_eq!(copied[1].phase.to_bits(), before[0].phase.to_bits());
    assert!(
        original
            .replace(app.world_mut(), &mut copy_resident)
            .is_err()
    );

    let mut edited = plan("independent-copy", 10);
    edited.placement.transform.translation_m.x += 10.0;
    app.world_mut().entity_mut(copy_root).insert((
        crate::domain_transform_to_bevy(edited.placement.transform),
        ManualPlacementProjection(edited.placement.clone()),
    ));
    app.update();
    assert!(copy.replace(app.world_mut(), &mut copy_resident).is_err());
    edited.replace(app.world_mut(), &mut copy_resident).unwrap();
    app.update();
    let moved = super::super::super::scene::harvest(app.world_mut(), camera, &[0, 10])
        .unwrap()
        .1;
    assert_eq!(
        moved[1].position - copied[1].position,
        DVec3::new(3.0, 0.0, 0.0)
    );
    assert_eq!(moved[1].phase.to_bits(), copied[1].phase.to_bits());
    resident.retire(app.world_mut());
    app.world_mut().despawn(original_root);
    app.update();
    assert!(copy_resident.is_synchronized(app.world()));
    copy_resident.retire(app.world_mut());
    assert!(app.world().get_entity(copy_root).is_ok());
}

#[test]
fn acquired_object_lights_refuse_ambiguous_parented_stale_and_changed_roots_atomically() {
    for invalid in 0..5 {
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, TransformPlugin));
        let prepared = plan("independent-model", 0);
        let entity = root(&mut app, &prepared.placement);
        app.update();
        match invalid {
            0 => {
                root(&mut app, &prepared.placement);
                app.update();
            }
            1 => {
                let parent = app.world_mut().spawn(Transform::IDENTITY).id();
                app.world_mut().entity_mut(entity).insert(ChildOf(parent));
                app.update();
            }
            2 => {
                app.world_mut()
                    .get_mut::<Transform>(entity)
                    .unwrap()
                    .translation
                    .x += 1.0;
            }
            3 => {
                app.world_mut()
                    .get_mut::<ManualPlacementProjection>(entity)
                    .unwrap()
                    .0
                    .name
                    .push('!');
            }
            _ => {
                app.world_mut()
                    .entity_mut(entity)
                    .remove::<GlobalTransform>();
            }
        }
        let before = app.world().entities().len();
        assert!(
            prepared.stage(app.world_mut()).is_err(),
            "accepted invalid root {invalid}"
        );
        assert_eq!(app.world().entities().len(), before);
    }
}
