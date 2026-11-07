use super::*;
use bevy_rapier3d::prelude::*;
use forge_core::bytes_sha256;
use forge_project::{
    VegetationCollisionProfile, VegetationCollisionShape,
    imported_vegetation::ImportedVegetationInstance,
};
use forge_world::imported_vegetation::{
    ImportedEntity,
    streaming::{PlantCollisionSource, PlantRepresentation, SelectedPlant},
};

fn plant(name: &str) -> ImportedEntity {
    ImportedEntity {
        id: EntityId::deterministic(name),
        source: ImportedVegetationInstance {
            source_key: bytes_sha256(name.as_bytes()),
            position_m: [0.0; 3],
            rotation_xyzw: [0.0, 0.0, 0.0, 1.0],
            scale: [1.0; 3],
            parameters: Vec::new(),
        },
    }
}

fn profile(shape: VegetationCollisionShape) -> VegetationCollisionProfile {
    VegetationCollisionProfile {
        shape,
        radius_m: 0.5,
        height_m: 3.0,
        activation_distance_m: 5.0,
    }
}

fn selection(
    plants: &[ImportedEntity],
    shape: VegetationCollisionShape,
) -> StreamedPlantSelection<'_> {
    StreamedPlantSelection {
        head_hash: bytes_sha256(b"authored head"),
        terrain_hash: bytes_sha256(b"admitted raster"),
        plants: plants
            .iter()
            .map(|entity| SelectedPlant {
                entity,
                layer_id: "authored",
                prototype_key: "custom",
                parameter_names: &[],
                representation: PlantRepresentation::Detail,
            })
            .collect(),
        collisions: plants
            .iter()
            .map(|entity| PlantCollisionSource {
                entity,
                profile: profile(shape),
            })
            .collect(),
    }
}

fn app() -> App {
    let mut app = App::new();
    app.add_plugins((MinimalPlugins, TransformPlugin))
        .insert_resource(bevy::time::TimeUpdateStrategy::ManualDuration(
            std::time::Duration::from_secs_f64(1.0 / 60.0),
        ))
        .insert_resource(Time::<Fixed>::from_hz(60.0))
        .insert_resource(TimestepMode::Fixed {
            dt: 1.0 / 60.0,
            substeps: 1,
        })
        .add_plugins((
            RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule(),
            ImportedPlantCollisionPlugin,
        ));
    app
}

fn step(app: &mut App, count: usize) {
    for _ in 0..count {
        app.update();
    }
}

fn stage(app: &mut App, plants: &[ImportedEntity], shape: VegetationCollisionShape) -> Resident {
    Resident::stage(
        app.world_mut(),
        EntityId::deterministic("imported-physical-owner"),
        Prepared::from_selection(&selection(plants, shape)).unwrap(),
    )
    .unwrap()
}

fn entity(app: &App, resident: &Resident, id: EntityId) -> Entity {
    resident.projection(app.world()).unwrap().entities[&id]
}

#[test]
fn ordered_resident_lookup_matches_tree_lookup_with_gaps_and_stray_ids() {
    let mut ids: Vec<_> = (0..180)
        .map(|index| EntityId::deterministic(format!("ordered-physical-{index}")))
        .collect();
    ids.sort_unstable();
    ids.dedup();
    assert_eq!(ids.len(), 180);
    let mut world = World::new();
    let residents: BTreeMap<_, _> = ids
        .iter()
        .enumerate()
        .filter(|(index, _)| index % 3 == 0)
        .map(|(_, &id)| (id, world.spawn_empty().id()))
        .collect();
    let mut cursor = residents.iter().peekable();
    for &id in ids.iter().step_by(2) {
        assert_eq!(
            next_ordered_resident(&mut cursor, id),
            residents.get(&id).copied()
        );
    }
}

#[test]
fn mutable_ordered_lookup_matches_tree_during_current_id_insertions_and_removals() {
    let mut ids: Vec<_> = (0..180)
        .map(|index| EntityId::deterministic(format!("mutable-physical-{index}")))
        .collect();
    ids.sort_unstable();
    ids.dedup();
    assert_eq!(ids.len(), 180);
    let mut world = World::new();
    let mut residents: BTreeMap<_, _> = ids
        .iter()
        .enumerate()
        .filter(|(index, _)| index % 3 == 0)
        .map(|(_, &id)| (id, world.spawn_empty().id()))
        .collect();
    let mut cursor = MutableResidentCursor::new(&residents);
    for (index, &id) in ids.iter().enumerate().step_by(2) {
        let expected = residents.get(&id).copied();
        assert_eq!(cursor.take(&residents, id), expected);
        if index % 4 == 0 {
            residents.remove(&id);
        } else if expected.is_none() {
            residents.insert(id, world.spawn_empty().id());
        }
    }
}

#[test]
fn actual_backend_and_owned_shape_pose_groups_and_context_are_required() {
    let mut plants = vec![plant("tilted-anisotropic")];
    plants[0].source.position_m = [2.0, 1.0, -3.0];
    plants[0].source.rotation_xyzw = bevy::math::DQuat::from_rotation_z(0.3).to_array();
    plants[0].source.scale = [1.2, 1.5, 0.8];
    let mut app = app();
    let resident = stage(&mut app, &plants, VegetationCollisionShape::Cylinder);
    assert!(!resident.ready(app.world_mut()));
    step(&mut app, 3);
    assert!(resident.ready(app.world_mut()));
    let entity = entity(&app, &resident, plants[0].id);
    let expected = resident.prepared.bodies[&plants[0].id].transform;
    assert_eq!(
        app.world()
            .get::<ImportedPlantCollider>(entity)
            .unwrap()
            .id(),
        plants[0].id
    );
    app.world_mut()
        .get_mut::<Transform>(entity)
        .unwrap()
        .translation
        .x += 0.1;
    assert!(!resident.ready(app.world_mut()));
    step(&mut app, 2);
    assert!(!resident.ready(app.world_mut())); // Backend agreeing with corruption is insufficient.
    app.world_mut().entity_mut(entity).insert(expected);
    step(&mut app, 2);
    assert!(resident.ready(app.world_mut()));
    app.world_mut()
        .entity_mut(entity)
        .insert(Collider::ball(2.0));
    step(&mut app, 2);
    assert!(!resident.ready(app.world_mut()));
    app.world_mut()
        .entity_mut(entity)
        .insert(resident.prepared.bodies[&plants[0].id].collider.clone());
    step(&mut app, 2);
    assert!(resident.ready(app.world_mut()));
    app.world_mut()
        .entity_mut(entity)
        .insert(CollisionGroups::new(Group::GROUP_1, Group::NONE));
    assert!(!resident.ready(app.world_mut()));
    app.world_mut()
        .entity_mut(entity)
        .insert(CollisionGroups::default());
    step(&mut app, 2);
    app.world_mut()
        .entity_mut(entity)
        .remove::<CollisionGroups>();
    step(&mut app, 2);
    assert!(resident.ready(app.world_mut()));
    let foreign = app
        .world_mut()
        .spawn(RapierContextColliders::default())
        .id();
    assert!(!resident.ready(app.world_mut()));
    app.world_mut().despawn(foreign);
    assert!(resident.ready(app.world_mut()));
}

#[test]
fn graphics_lod_keeps_physical_handles_and_raw_pose_or_head_changes_revoke_identity() {
    let plants = vec![plant("fixed-source")];
    let mut app = app();
    let resident = stage(&mut app, &plants, VegetationCollisionShape::Capsule);
    step(&mut app, 3);
    let entity = entity(&app, &resident, plants[0].id);
    let handle = app.world().get::<RapierColliderHandle>(entity).unwrap().0;
    let mut far = selection(&plants, VegetationCollisionShape::Capsule);
    far.plants[0].representation = PlantRepresentation::Group {
        tile: forge_world::terrain_hlod::TerrainHlodKey {
            x: 0,
            z: 0,
            level: 1,
        },
        lod: 2,
    };
    assert!(resident.matches(&far));
    step(&mut app, 3);
    assert!(resident.ready(app.world_mut()));
    assert_eq!(
        app.world().get::<RapierColliderHandle>(entity).unwrap().0,
        handle
    );
    let mut changed = plants.clone();
    changed[0].source.position_m[1] += 1e-10;
    assert!(!resident.matches(&selection(&changed, VegetationCollisionShape::Capsule)));
    far.head_hash = bytes_sha256(b"another authored head");
    assert!(!resident.matches(&far));
    assert!(resident.matches(&selection(&plants, VegetationCollisionShape::Capsule)));
}

#[test]
fn any_moving_body_and_its_predicted_step_protect_collisions_independently_of_view() {
    let plants = vec![plant("activation")];
    let mut app = app();
    let resident = stage(&mut app, &plants, VegetationCollisionShape::Cylinder);
    let initial = entity(&app, &resident, plants[0].id);
    let distant = app
        .world_mut()
        .spawn((
            RigidBody::KinematicVelocityBased,
            Collider::ball(0.5),
            Transform::from_xyz(1000.0, 0.0, 0.0),
            Velocity::zero(),
        ))
        .id();
    step(&mut app, 3);
    assert!(app.world().get_entity(initial).is_err());
    assert_eq!(resident.resident_count(app.world()), 0);
    assert_eq!(resident.source_count(), 1);
    assert!(!resident.is_empty());
    assert!(resident.ready(app.world_mut()));
    let actor = app
        .world_mut()
        .spawn((
            RigidBody::KinematicVelocityBased,
            Collider::ball(0.5),
            Transform::from_xyz(1.0, 0.0, 0.0),
            Velocity::zero(),
        ))
        .id();
    step(&mut app, 3);
    let near = entity(&app, &resident, plants[0].id);
    assert_ne!(initial, near);
    assert!(app.world().get::<ColliderDisabled>(near).is_none());
    assert!(resident.ready(app.world_mut()));
    app.world_mut().despawn(actor);
    // Moving away revokes the previous receipt even before the residency system runs.
    assert!(!resident.ready(app.world_mut()));
    update_activation(app.world_mut());
    assert!(app.world().get_entity(near).is_err());
    // ECS disappearance is insufficient while Rapier still owns the old handle.
    assert!(!resident.ready(app.world_mut()));
    step(&mut app, 2);
    assert!(resident.ready(app.world_mut()));
    app.world_mut()
        .get_mut::<Velocity>(distant)
        .unwrap()
        .linear
        .x = -60_000.0;
    // Prediction spans the obstacle before the backend advances this velocity.
    update_activation(app.world_mut());
    let predicted = entity(&app, &resident, plants[0].id);
    assert!(app.world().get::<Collider>(predicted).is_some());
    assert!(!resident.ready(app.world_mut()));
    app.world_mut().get_mut::<Velocity>(distant).unwrap().linear = Vec3::ZERO;
    step(&mut app, 2);
    assert!(resident.ready(app.world_mut()));
    let unsupported = app
        .world_mut()
        .spawn((RigidBody::Dynamic, Transform::IDENTITY))
        .id();
    update_activation(app.world_mut());
    let conservative = entity(&app, &resident, plants[0].id);
    assert!(app.world().get::<Collider>(conservative).is_some());
    assert!(!resident.ready(app.world_mut()));
    app.world_mut().despawn(unsupported);
}

#[test]
fn refused_inputs_duplicate_owners_and_empty_populations_never_mutate_foreign_entities() {
    let plants = vec![plant("refusal")];
    let mut invalid = selection(&plants, VegetationCollisionShape::Capsule);
    invalid.collisions[0].profile.height_m = 0.1;
    assert!(Prepared::from_selection(&invalid).is_err());
    let mut duplicate = selection(&plants, VegetationCollisionShape::Cylinder);
    duplicate.collisions.push(PlantCollisionSource {
        entity: &plants[0],
        profile: profile(VegetationCollisionShape::Cylinder),
    });
    assert!(Prepared::from_selection(&duplicate).is_err());
    let mut app = app();
    let foreign = app
        .world_mut()
        .spawn((Collider::ball(0.2), Transform::IDENTITY))
        .id();
    let resident = stage(&mut app, &plants, VegetationCollisionShape::Cylinder);
    assert!(
        Resident::stage(
            app.world_mut(),
            resident.owner,
            Prepared::from_selection(&selection(&plants, VegetationCollisionShape::Cylinder))
                .unwrap()
        )
        .is_err()
    );
    resident.retire(app.world_mut()).unwrap();
    assert!(app.world().get_entity(foreign).is_ok());
    let empty: Vec<ImportedEntity> = Vec::new();
    let empty = stage(&mut app, &empty, VegetationCollisionShape::Cylinder);
    assert!(empty.is_empty() && empty.ready(app.world_mut()));
    empty.retire(app.world_mut()).unwrap();
    assert!(app.world().get_entity(foreign).is_ok());
}

#[test]
fn real_character_stops_at_a_scaled_source_proxy_and_passes_beside_it() {
    let mut plants = vec![plant("walk-obstacle")];
    plants[0].source.scale = [2.0, 1.0, 2.0];
    let mut app = app();
    let resident = stage(&mut app, &plants, VegetationCollisionShape::Cylinder);
    app.world_mut().spawn((
        Collider::cuboid(20.0, 0.1, 20.0),
        Transform::from_xyz(0.0, -0.1, 0.0),
    ));
    let player = app
        .world_mut()
        .spawn((
            RigidBody::KinematicPositionBased,
            Collider::capsule_y(0.4, 0.3),
            KinematicCharacterController::default(),
            Transform::from_xyz(0.0, 0.75, -3.0),
        ))
        .id();
    app.add_systems(
        FixedUpdate,
        (|mut controllers: Query<&mut KinematicCharacterController>| {
            for mut controller in &mut controllers {
                controller.translation = Some(Vec3::Z * 0.1);
            }
        })
        .before(ImportedPlantActivationSet),
    );
    step(&mut app, 45);
    assert!(resident.ready(app.world_mut()));
    assert!(app.world().get::<Transform>(player).unwrap().translation.z < -1.2);
    app.world_mut()
        .get_mut::<Transform>(player)
        .unwrap()
        .translation = Vec3::new(3.0, 0.75, -3.0);
    step(&mut app, 60);
    assert!(app.world().get::<Transform>(player).unwrap().translation.z > 1.0);
    assert!(resident.ready(app.world_mut()));
}

#[test]
fn both_nonuniform_proxy_shapes_use_a_bounded_real_rapier_hull() {
    for shape in [
        VegetationCollisionShape::Capsule,
        VegetationCollisionShape::Cylinder,
    ] {
        let mut plants = vec![plant("nonuniform-proxy")];
        plants[0].source.scale = [1.2, 1.5, 0.8];
        let mut app = app();
        let resident = stage(&mut app, &plants, shape);
        let hull = resident.prepared.bodies[&plants[0].id]
            .collider
            .raw
            .as_convex_polyhedron()
            .unwrap();
        assert!(!hull.points().is_empty() && hull.points().len() < MAX_DERIVED_HULL_VERTICES);
        step(&mut app, 3);
        assert!(resident.ready(app.world_mut()));
    }
}

#[test]
fn a_lost_ownership_marker_closes_readiness_and_never_authorizes_deletion() {
    let plants = vec![plant("ownership")];
    let mut app = app();
    let resident = stage(&mut app, &plants, VegetationCollisionShape::Cylinder);
    let entity = entity(&app, &resident, plants[0].id);
    step(&mut app, 3);
    assert!(resident.ready(app.world_mut()));
    app.world_mut()
        .entity_mut(entity)
        .remove::<ImportedPlantCollider>();
    assert!(!resident.ready(app.world_mut()));
    app.world_mut().spawn((
        RigidBody::KinematicVelocityBased,
        Collider::ball(0.5),
        Transform::from_xyz(1000.0, 0.0, 0.0),
        Velocity::zero(),
    ));
    update_activation(app.world_mut());
    assert!(app.world().get_entity(entity).is_ok());
    assert!(!resident.ready(app.world_mut()));
    assert!(resident.retire(app.world_mut()).is_err());
    assert!(app.world().get_entity(entity).is_ok());
}

#[test]
fn physical_underflow_is_rejected_before_a_zero_width_proxy_can_be_published() {
    let mut plants = vec![plant("physical-underflow")];
    plants[0].source.scale = [0.0001; 3];
    let mut selected = selection(&plants, VegetationCollisionShape::Capsule);
    selected.collisions[0].profile.radius_m = f32::from_bits(1);
    assert!(Prepared::from_selection(&selected).is_err());
    plants[0].source.scale = [1.0; 3];
    let mut selected = selection(&plants, VegetationCollisionShape::Cylinder);
    selected.collisions[0].profile.height_m = f32::from_bits(1);
    assert!(Prepared::from_selection(&selected).is_err());
}

#[test]
fn canonical_sources_survive_bounded_residence_and_reentry_with_real_backend_acknowledgements() {
    let plants: Vec<_> = (0..128)
        .map(|index| {
            let mut source = plant(&format!("residency-{index}"));
            source.source.position_m[0] = f64::from(index) * 100.0;
            source
        })
        .collect();
    let mut app = app();
    let actor = app
        .world_mut()
        .spawn((
            RigidBody::KinematicVelocityBased,
            Collider::ball(0.5),
            Transform::IDENTITY,
            Velocity::zero(),
        ))
        .id();
    let resident = stage(&mut app, &plants, VegetationCollisionShape::Cylinder);
    assert_eq!(resident.source_count(), 128);
    assert_eq!(resident.resident_count(app.world()), 1);
    let first = entity(&app, &resident, plants[0].id);
    assert!(!resident.ready(app.world_mut()));
    step(&mut app, 3);
    assert!(resident.ready(app.world_mut()));
    app.world_mut()
        .get_mut::<Transform>(actor)
        .unwrap()
        .translation
        .x = 12_700.0;
    assert!(!resident.ready(app.world_mut()));
    update_activation(app.world_mut());
    let distant = entity(&app, &resident, plants[127].id);
    assert!(app.world().get_entity(first).is_err());
    assert!(app.world().get::<RapierColliderHandle>(distant).is_none());
    assert!(!resident.ready(app.world_mut()));
    step(&mut app, 3);
    assert!(resident.ready(app.world_mut()));
    assert_eq!(resident.resident_count(app.world()), 1);
    assert_eq!(resident.source_count(), 128);
    app.world_mut()
        .get_mut::<Transform>(actor)
        .unwrap()
        .translation
        .x = 0.0;
    assert!(!resident.ready(app.world_mut()));
    step(&mut app, 3);
    assert!(resident.ready(app.world_mut()));
    let returned = entity(&app, &resident, plants[0].id);
    assert_ne!(first, returned);
    assert_eq!(
        app.world()
            .get::<ImportedPlantCollider>(returned)
            .unwrap()
            .id(),
        plants[0].id
    );
    assert!(std::ptr::eq(
        app.world().get::<Collider>(returned).unwrap().raw.as_ref(),
        resident.prepared.bodies[&plants[0].id]
            .collider
            .raw
            .as_ref()
    ));
}

#[test]
fn sparse_to_dense_and_back_preserves_backend_residency() {
    let plants: Vec<_> = (0..32)
        .map(|index| {
            let mut source = plant(&format!("density-transition-{index}"));
            source.source.position_m[0] = f64::from(index) * 10.0;
            source
        })
        .collect();
    let mut app = app();
    let distant = app
        .world_mut()
        .spawn((
            RigidBody::KinematicVelocityBased,
            Collider::ball(0.5),
            Transform::from_xyz(10_000.0, 0.0, 0.0),
            Velocity::zero(),
        ))
        .id();
    let resident = stage(&mut app, &plants, VegetationCollisionShape::Cylinder);
    step(&mut app, 3);
    assert_eq!(resident.resident_count(app.world()), 0);
    assert!(resident.ready(app.world_mut()));

    // Without moving observers, the conservative policy keeps all bodies.
    app.world_mut().despawn(distant);
    assert!(!resident.ready(app.world_mut()));
    update_activation(app.world_mut());
    assert_eq!(resident.resident_count(app.world()), plants.len());
    assert!(!resident.ready(app.world_mut()));
    step(&mut app, 3);
    assert!(resident.ready(app.world_mut()));
    for plant in &plants {
        assert!(
            app.world()
                .get::<RapierColliderHandle>(entity(&app, &resident, plant.id))
                .is_some()
        );
    }

    app.world_mut().spawn((
        RigidBody::KinematicVelocityBased,
        Collider::ball(0.5),
        Transform::from_xyz(10_000.0, 0.0, 0.0),
        Velocity::zero(),
    ));
    assert!(!resident.ready(app.world_mut()));
    update_activation(app.world_mut());
    assert_eq!(resident.resident_count(app.world()), 0);
    assert!(!resident.ready(app.world_mut()));
    step(&mut app, 3);
    assert!(resident.ready(app.world_mut()));
}

#[test]
fn replacing_a_selection_with_empty_sources_waits_for_actual_backend_retirement() {
    let plants = vec![plant("old-backend")];
    let mut app = app();
    let resident = stage(&mut app, &plants, VegetationCollisionShape::Cylinder);
    step(&mut app, 3);
    assert!(resident.ready(app.world_mut()));
    let old = entity(&app, &resident, plants[0].id);
    resident.retire(app.world_mut()).unwrap();
    assert!(
        app.world_mut()
            .query::<&RapierContextColliders>()
            .iter(app.world())
            .any(|context| context.entity2collider().contains_key(&old))
    );
    let empty = stage(&mut app, &[], VegetationCollisionShape::Cylinder);
    assert!(!empty.ready(app.world_mut()));
    step(&mut app, 2);
    assert!(empty.ready(app.world_mut()));
    assert!(
        app.world_mut()
            .query::<&RapierContextColliders>()
            .iter(app.world())
            .all(|context| !context.entity2collider().contains_key(&old))
    );
}

#[test]
fn an_empty_resident_set_still_requires_the_unique_default_backend() {
    let mut app = app();
    app.world_mut().spawn((
        RigidBody::KinematicVelocityBased,
        Collider::ball(0.5),
        Transform::from_xyz(1000.0, 0.0, 0.0),
        Velocity::zero(),
    ));
    let resident = stage(
        &mut app,
        &[plant("far-source")],
        VegetationCollisionShape::Cylinder,
    );
    assert_eq!(resident.resident_count(app.world()), 0);
    assert_eq!(resident.source_count(), 1);
    step(&mut app, 3);
    assert!(resident.ready(app.world_mut()));
    let context = app
        .world_mut()
        .query_filtered::<Entity, With<RapierContextColliders>>()
        .single(app.world())
        .unwrap();
    app.world_mut()
        .entity_mut(context)
        .remove::<DefaultRapierContext>();
    assert!(!resident.ready(app.world_mut()));
    app.world_mut()
        .entity_mut(context)
        .insert(DefaultRapierContext);
    assert!(resident.ready(app.world_mut()));
    let extra = app
        .world_mut()
        .spawn(RapierContextColliders::default())
        .id();
    assert!(!resident.ready(app.world_mut()));
    app.world_mut().despawn(extra);
    assert!(resident.ready(app.world_mut()));
}
