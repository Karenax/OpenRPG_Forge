use super::collection::{Collection, Desired, Projection};
use super::*;
use forge_core::{AssetId, ContentHash, EntityId};

fn placement(name: &str, hash: u8) -> ManualPlacement {
    ManualPlacement {
        id: EntityId::deterministic(name),
        name: name.into(),
        asset_id: AssetId::deterministic(name),
        asset_hash: ContentHash::from_bytes([hash; 32]),
        transform: forge_core::Transform::IDENTITY,
        locked: false,
        physics: None,
        particles: None,
        local_lights: None,
    }
}

fn suspended() -> LanternMotion {
    LanternMotion {
        t: 1.5,
        ph: 0.7,
        x: 0.04,
        z: -0.03,
        vx: 0.02,
        vz: -0.01,
    }
}

fn hinged() -> SignMotion {
    SignMotion {
        t: 2.0,
        a: 0.1,
        av: 0.02,
        b: -0.05,
        bv: 0.01,
    }
}

fn wind() -> Wind {
    Wind {
        speed: 8.0,
        direction: [0.6, 0.8],
    }
}

fn hierarchy(world: &mut World, placement: &ManualPlacement) -> (Entity, Entity, Vec<Binding>) {
    let root = world
        .spawn((
            ManualPlacementProjection(placement.clone()),
            domain_transform_to_bevy(placement.transform),
        ))
        .id();
    let nested = world.spawn((ChildOf(root), Transform::IDENTITY)).id();
    let first_pivot = Vec3::new(1.0, 2.0, 3.0);
    let second_pivot = Vec3::new(-2.0, 1.0, 4.0);
    let rest = Quat::from_rotation_y(0.4);
    let first = world
        .spawn((ChildOf(root), Transform::from_translation(first_pivot)))
        .id();
    let second = world
        .spawn((
            ChildOf(nested),
            Transform::from_translation(second_pivot).with_rotation(rest),
        ))
        .id();
    (
        root,
        nested,
        vec![
            Binding {
                key: 7,
                entity: first,
                pivot: first_pivot,
                rest_rotation: Quat::IDENTITY,
                rest_scale: Vec3::ONE,
                motion: Motion::Suspended(suspended()),
            },
            Binding {
                key: 42,
                entity: second,
                pivot: second_pivot,
                rest_rotation: rest,
                rest_scale: Vec3::ONE,
                motion: Motion::Hinged(hinged()),
            },
        ],
    )
}

fn rotations(world: &World, bindings: &[Binding]) -> Vec<Quat> {
    bindings
        .iter()
        .map(|binding| world.get::<Transform>(binding.entity).unwrap().rotation)
        .collect()
}

#[test]
fn independent_assets_use_the_same_host_and_keep_their_own_states() {
    let mut world = World::new();
    let dock = placement("dock", 3);
    let cave = placement("cave", 9);
    let (root_a, _, a) = hierarchy(&mut world, &dock);
    let (root_b, _, b) = hierarchy(&mut world, &cave);
    let mut host_a = Resident::bind(&mut world, &dock, root_a, a.clone()).unwrap();
    let mut host_b = Resident::bind(&mut world, &cave, root_b, b.clone()).unwrap();
    let b_before = rotations(&world, &b);

    let mut expected_suspended = suspended();
    let mut expected_hinged = hinged();
    expected_suspended.advance(0.04, wind()).unwrap();
    expected_hinged.advance(0.04, wind()).unwrap();
    host_a.advance(&mut world, 0.04, wind()).unwrap();
    assert_eq!(rotations(&world, &b), b_before);
    assert_eq!(
        world.get::<Transform>(a[0].entity).unwrap().rotation,
        expected_suspended.rotation().as_quat()
    );
    assert_eq!(
        world.get::<Transform>(a[1].entity).unwrap().rotation,
        a[1].rest_rotation * expected_hinged.rotation().as_quat()
    );

    host_b.advance(&mut world, 0.04, wind()).unwrap();
    assert_eq!(rotations(&world, &a), rotations(&world, &b));
    assert_eq!(
        world.get::<ManualPlacementProjection>(root_a).unwrap().0,
        dock
    );
    assert_eq!(
        world.get::<ManualPlacementProjection>(root_b).unwrap().0,
        cave
    );
}

#[test]
fn rebind_preserves_each_phase_when_entities_and_order_change() {
    let mut world = World::new();
    let placement = placement("rebuild", 5);
    let (root, _, bindings) = hierarchy(&mut world, &placement);
    let mut host = Resident::bind(&mut world, &placement, root, bindings.clone()).unwrap();
    host.advance(&mut world, 0.05, wind()).unwrap();
    let previous = rotations(&world, &bindings);

    let _ = world.despawn(root);
    let mut moved = placement.clone();
    moved.transform =
        forge_core::Transform::from_translation(forge_core::Vec3::new(9.0, 1.0, -3.0));
    let (new_root, _, replacement) = hierarchy(&mut world, &moved);
    let mut reordered = replacement.clone();
    reordered.reverse();
    host.rebind(&mut world, &moved, new_root, reordered)
        .unwrap();
    assert_eq!(rotations(&world, &replacement), previous);

    let mut expected_suspended = suspended();
    let mut expected_hinged = hinged();
    for _ in 0..2 {
        expected_suspended.advance(0.05, wind()).unwrap();
        expected_hinged.advance(0.05, wind()).unwrap();
    }
    host.advance(&mut world, 0.05, wind()).unwrap();
    assert_eq!(
        world
            .get::<Transform>(replacement[0].entity)
            .unwrap()
            .rotation,
        expected_suspended.rotation().as_quat()
    );
    assert_eq!(
        world
            .get::<Transform>(replacement[1].entity)
            .unwrap()
            .rotation,
        replacement[1].rest_rotation * expected_hinged.rotation().as_quat()
    );
}

#[test]
fn nonuniform_and_mirrored_pivot_scale_is_retained_across_rebuild() {
    let mut world = World::new();
    let object = placement("scaled-pivot", 13);
    let (root, _, mut parts) = hierarchy(&mut world, &object);
    let scale = Vec3::new(2.0, -0.5, 1.25);
    world.get_mut::<Transform>(parts[0].entity).unwrap().scale = scale;
    parts[0].rest_scale = scale;
    let mut host = Resident::bind(&mut world, &object, root, parts.clone()).unwrap();
    host.advance(&mut world, 0.05, wind()).unwrap();
    let before = rotations(&world, &parts);
    assert_eq!(
        world.get::<Transform>(parts[0].entity).unwrap().scale,
        scale
    );

    world.get_mut::<Transform>(parts[0].entity).unwrap().scale = Vec3::ONE;
    assert!(host.advance(&mut world, 0.05, wind()).is_err());
    assert_eq!(rotations(&world, &parts), before);
    world.get_mut::<Transform>(parts[0].entity).unwrap().scale = scale;
    let _ = world.despawn(root);
    let (new_root, _, mut replacement) = hierarchy(&mut world, &object);
    world
        .get_mut::<Transform>(replacement[0].entity)
        .unwrap()
        .scale = scale;
    replacement[0].rest_scale = scale;
    host.rebind(&mut world, &object, new_root, replacement.clone())
        .unwrap();
    assert_eq!(rotations(&world, &replacement), before);
    assert_eq!(
        world.get::<Transform>(replacement[0].entity).unwrap().scale,
        scale
    );
    host.advance(&mut world, 0.05, wind()).unwrap();
    host.release(&mut world);

    let zero = placement("zero-pivot-scale", 14);
    let (root, _, mut parts) = hierarchy(&mut world, &zero);
    world.get_mut::<Transform>(parts[0].entity).unwrap().scale = Vec3::ZERO;
    parts[0].rest_scale = Vec3::ZERO;
    assert!(Resident::bind(&mut world, &zero, root, parts).is_err());
    assert!(!crate::static_hlod::moving_object_retained(&world, zero.id));
}

#[test]
fn invalid_input_and_changed_ancestry_do_not_partially_advance() {
    let mut world = World::new();
    let placement = placement("transaction", 7);
    let (root, nested, bindings) = hierarchy(&mut world, &placement);
    let mut host = Resident::bind(&mut world, &placement, root, bindings.clone()).unwrap();
    host.advance(&mut world, 0.05, wind()).unwrap();
    let before = rotations(&world, &bindings);
    assert!(host.advance(&mut world, f64::NAN, wind()).is_err());
    assert!(
        host.advance(
            &mut world,
            0.05,
            Wind {
                speed: f64::MAX,
                ..wind()
            }
        )
        .is_err()
    );
    assert_eq!(rotations(&world, &bindings), before);

    let other = world.spawn(Transform::IDENTITY).id();
    world.entity_mut(nested).insert(ChildOf(other));
    assert!(host.advance(&mut world, 0.05, wind()).is_err());
    assert_eq!(rotations(&world, &bindings), before);
    world.entity_mut(nested).insert(ChildOf(root));
    host.advance(&mut world, 0.05, wind()).unwrap();

    let mut expected = suspended();
    expected.advance(0.05, wind()).unwrap();
    expected.advance(0.05, wind()).unwrap();
    assert_eq!(
        world.get::<Transform>(bindings[0].entity).unwrap().rotation,
        expected.rotation().as_quat()
    );
}

#[test]
fn later_part_overflow_does_not_commit_an_earlier_part() {
    let mut world = World::new();
    let placement = placement("late-overflow", 6);
    let (root, _, mut bindings) = hierarchy(&mut world, &placement);
    let mut extreme = hinged();
    extreme.a = f64::MAX;
    extreme.av = f64::MAX;
    bindings[1].motion = Motion::Hinged(extreme);
    let mut host = Resident::bind(&mut world, &placement, root, bindings.clone()).unwrap();
    let before = rotations(&world, &bindings);
    assert!(host.advance(&mut world, 0.1, wind()).is_err());
    assert_eq!(rotations(&world, &bindings), before);
    host.advance(&mut world, 0.0, wind()).unwrap();
    assert_eq!(
        world.get::<Transform>(bindings[0].entity).unwrap().rotation,
        suspended().rotation().as_quat()
    );
}

#[test]
fn duplicate_pivots_and_changed_rebind_metadata_are_refused() {
    let mut world = World::new();
    let placement = placement("duplicates", 2);
    let (root, _, bindings) = hierarchy(&mut world, &placement);
    let mut duplicate = bindings.clone();
    duplicate[1].key = duplicate[0].key;
    assert!(Resident::bind(&mut world, &placement, root, duplicate).is_err());
    let mut duplicate = bindings.clone();
    duplicate[1].entity = duplicate[0].entity;
    assert!(Resident::bind(&mut world, &placement, root, duplicate).is_err());

    let mut host = Resident::bind(&mut world, &placement, root, bindings.clone()).unwrap();
    host.advance(&mut world, 0.05, wind()).unwrap();
    let before = rotations(&world, &bindings);
    let mut changed = bindings.clone();
    changed[1].motion = Motion::Suspended(suspended());
    assert!(host.rebind(&mut world, &placement, root, changed).is_err());
    assert_eq!(rotations(&world, &bindings), before);
    let mut changed = placement.clone();
    changed.asset_hash = ContentHash::from_bytes([8; 32]);
    assert!(
        host.rebind(&mut world, &changed, root, bindings.clone())
            .is_err()
    );
    assert_eq!(rotations(&world, &bindings), before);
}

struct IndependentSource(BTreeMap<EntityId, (Entity, Vec<Binding>)>);

struct IndependentProjection(Resident);

impl Projection for IndependentProjection {
    type Source = IndependentSource;

    fn bind(
        world: &mut World,
        source: &Self::Source,
        placement: &ManualPlacement,
    ) -> Result<Self, String> {
        let (root, bindings) = source.0.get(&placement.id).ok_or("Missing scene input")?;
        Resident::bind(world, placement, *root, bindings.clone()).map(Self)
    }

    fn rebind(
        &mut self,
        world: &mut World,
        source: &Self::Source,
        placement: &ManualPlacement,
    ) -> Result<(), String> {
        let (root, bindings) = source.0.get(&placement.id).ok_or("Missing scene input")?;
        self.0.rebind(world, placement, *root, bindings.clone())
    }

    fn advance(&mut self, world: &mut World, dt: f64, wind: Wind) -> Result<(), String> {
        self.0.advance(world, dt, wind)
    }

    fn release(self, world: &mut World) {
        self.0.release(world);
    }
}

#[test]
fn collection_reacquires_independent_objects_and_only_retires_motion_state() {
    let mut world = World::new();
    let a = placement("independent-a", 1);
    let b = placement("independent-b", 2);
    let (root_a, _, parts_a) = hierarchy(&mut world, &a);
    let (root_b, _, parts_b) = hierarchy(&mut world, &b);
    let mut source = IndependentSource(BTreeMap::from([
        (a.id, (root_a, parts_a.clone())),
        (b.id, (root_b, parts_b.clone())),
    ]));
    let mut collection = Collection::<IndependentProjection>::default();
    let placements = [a.clone(), b.clone()];
    collection
        .update(
            &mut world,
            &source,
            &Desired::new(&placements).unwrap(),
            0.05,
            wind(),
        )
        .unwrap();
    assert_eq!(collection.len(), 2);
    assert!(crate::static_hlod::moving_object_retained(&world, a.id));
    assert!(crate::static_hlod::moving_object_retained(&world, b.id));
    assert_eq!(rotations(&world, &parts_a), rotations(&world, &parts_b));

    let before = rotations(&world, &parts_a);
    assert!(Desired::new(&[a.clone(), a.clone()]).is_err());
    assert_eq!(rotations(&world, &parts_a), before);
    let b_before = rotations(&world, &parts_b);
    collection
        .update(
            &mut world,
            &source,
            &Desired::new(std::slice::from_ref(&a)).unwrap(),
            0.05,
            wind(),
        )
        .unwrap();
    assert_eq!(collection.len(), 1);
    assert!(crate::static_hlod::moving_object_retained(&world, a.id));
    assert!(!crate::static_hlod::moving_object_retained(&world, b.id));
    assert_eq!(rotations(&world, &parts_b), b_before);
    assert!(world.get_entity(root_b).is_ok());

    let _ = world.despawn(root_a);
    let (new_root, _, replacement) = hierarchy(&mut world, &a);
    source.0.insert(a.id, (new_root, replacement.clone()));
    collection
        .update(
            &mut world,
            &source,
            &Desired::new(std::slice::from_ref(&a)).unwrap(),
            0.05,
            wind(),
        )
        .unwrap();
    let mut expected = suspended();
    for _ in 0..3 {
        expected.advance(0.05, wind()).unwrap();
    }
    assert_eq!(
        world
            .get::<Transform>(replacement[0].entity)
            .unwrap()
            .rotation,
        expected.rotation().as_quat()
    );
    collection.clear(&mut world);
    assert!(collection.is_empty());
    assert!(!crate::static_hlod::moving_object_retained(&world, a.id));
    assert!(world.get_entity(new_root).is_ok());
}

#[test]
fn failed_first_frame_releases_its_detail_owner_without_changing_geometry() {
    let mut world = World::new();
    let object = placement("failed-initial-frame", 4);
    let (root, _, bindings) = hierarchy(&mut world, &object);
    let before = rotations(&world, &bindings);
    let source = IndependentSource(BTreeMap::from([(object.id, (root, bindings.clone()))]));
    let mut collection = Collection::<IndependentProjection>::default();
    assert!(
        collection
            .update(
                &mut world,
                &source,
                &Desired::new(std::slice::from_ref(&object)).unwrap(),
                f64::NAN,
                wind(),
            )
            .is_err()
    );
    assert!(collection.is_empty());
    assert!(!crate::static_hlod::moving_object_retained(
        &world, object.id
    ));
    assert_eq!(rotations(&world, &bindings), before);
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "The scenario verifies the same collider before and after physics synchronization."
)]
fn moving_pivot_updates_the_real_manual_mesh_collider() {
    use bevy::{ecs::system::SystemState, time::TimeUpdateStrategy};
    use bevy_rapier3d::prelude::{NoUserData, QueryFilter, RapierPhysicsPlugin, ReadRapierContext};
    use std::time::Duration;

    fn ray(world: &mut World, target: Entity, z: f32) -> Option<Entity> {
        let mut state = SystemState::<ReadRapierContext>::new(world);
        let contexts = state.get(world).unwrap();
        let context = contexts.single().unwrap();
        let predicate = |entity| entity == target;
        context.with_query_pipeline(QueryFilter::default().predicate(&predicate), |queries| {
            queries
                .cast_ray(Vec3::new(0.0, 10.0, z), Vec3::NEG_Y, 20.0, true)
                .map(|(entity, _)| entity)
        })
    }

    let mut app = App::new();
    app.add_plugins((
        MinimalPlugins,
        TransformPlugin,
        RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule(),
        crate::ManualPlacementCollisionPlugin,
    ))
    .insert_resource(TimeUpdateStrategy::ManualDuration(Duration::from_secs_f64(
        1.0 / 60.0,
    )))
    .init_resource::<Assets<Mesh>>();
    let object = placement("pendulum-collider", 10);
    let root = app
        .world_mut()
        .spawn((
            ManualPlacementProjection(object.clone()),
            domain_transform_to_bevy(object.transform),
        ))
        .id();
    let pivot = app
        .world_mut()
        .spawn((ChildOf(root), Transform::IDENTITY))
        .id();
    let mesh = app
        .world_mut()
        .resource_mut::<Assets<Mesh>>()
        .add(Mesh::from(Cuboid::new(0.5, 0.5, 0.5)));
    let primitive = app
        .world_mut()
        .spawn((
            ChildOf(pivot),
            Transform::from_xyz(0.0, -2.0, 0.0),
            Mesh3d(mesh),
            crate::ManualPlacementCollision(object.id),
            crate::ManualPlacementCollisionGeometry::TriangleMesh,
        ))
        .id();
    app.world_mut()
        .entity_mut(root)
        .insert(crate::ManualPlacementCollisionExpected {
            placement: object.id,
            primitives: vec![primitive],
        });
    for _ in 0..4 {
        app.update();
    }
    assert!(crate::manual_placements_ready(
        app.world_mut(),
        std::slice::from_ref(&object)
    ));
    assert_eq!(ray(app.world_mut(), primitive, 0.0), Some(primitive));
    assert_eq!(ray(app.world_mut(), primitive, -2.0), None);

    let mut host = Resident::bind(
        app.world_mut(),
        &object,
        root,
        vec![Binding {
            key: 1,
            entity: pivot,
            pivot: Vec3::ZERO,
            rest_rotation: Quat::IDENTITY,
            rest_scale: Vec3::ONE,
            motion: Motion::Hinged(SignMotion {
                t: 0.0,
                a: std::f64::consts::FRAC_PI_2,
                av: 0.0,
                b: 0.0,
                bv: 0.0,
            }),
        }],
    )
    .unwrap();
    host.advance(app.world_mut(), 0.0, wind()).unwrap();
    assert!(!crate::manual_placements_ready(
        app.world_mut(),
        std::slice::from_ref(&object)
    ));
    for _ in 0..4 {
        app.update();
    }
    assert!(crate::manual_placements_ready(
        app.world_mut(),
        std::slice::from_ref(&object)
    ));
    assert_eq!(ray(app.world_mut(), primitive, 0.0), None);
    assert_eq!(ray(app.world_mut(), primitive, -2.0), Some(primitive));
    host.release(app.world_mut());
}

#[test]
fn shared_resolver_scopes_nested_pivots_to_each_placement() {
    use super::resolve::{Target, bindings};

    #[derive(Component)]
    struct Key(u64);

    fn from_keys(world: &World, root: Entity, targets: &[Target]) -> Result<Vec<Binding>, String> {
        bindings(world, root, targets, |world, entity| {
            Ok(world.get::<Key>(entity).map(|key| key.0))
        })
    }

    let mut world = World::new();
    let a = placement("resolver-a", 11);
    let b = placement("resolver-b", 12);
    let (root_a, _, parts_a) = hierarchy(&mut world, &a);
    let (root_b, _, parts_b) = hierarchy(&mut world, &b);
    for part in parts_a.iter().chain(&parts_b) {
        world.entity_mut(part.entity).insert(Key(part.key));
    }
    let targets: Vec<_> = parts_a
        .iter()
        .map(|part| Target {
            key: part.key,
            pivot: part.pivot,
            rest_rotation: part.rest_rotation,
            rest_scale: part.rest_scale,
            motion: part.motion,
        })
        .collect();
    let resolved_a = from_keys(&world, root_a, &targets).unwrap();
    let resolved_b = from_keys(&world, root_b, &targets).unwrap();
    assert_eq!(resolved_a[0].entity, parts_a[0].entity);
    assert_eq!(resolved_a[1].entity, parts_a[1].entity);
    assert_eq!(resolved_b[0].entity, parts_b[0].entity);
    assert_eq!(resolved_b[1].entity, parts_b[1].entity);
    let before_a = rotations(&world, &parts_a);
    let before_b = rotations(&world, &parts_b);

    let duplicate = world
        .spawn((ChildOf(root_a), Transform::IDENTITY, Key(targets[0].key)))
        .id();
    assert!(from_keys(&world, root_a, &targets).is_err());
    assert!(from_keys(&world, root_b, &targets).is_ok());
    let _ = world.despawn(duplicate);
    let unexpected = world
        .spawn((ChildOf(root_a), Transform::IDENTITY, Key(99)))
        .id();
    assert!(from_keys(&world, root_a, &targets).is_err());
    let _ = world.despawn(unexpected);
    world.entity_mut(parts_a[0].entity).remove::<Key>();
    assert!(from_keys(&world, root_a, &targets).is_err());
    world
        .entity_mut(parts_a[0].entity)
        .insert(Key(targets[0].key));
    assert!(from_keys(&world, root_a, &[targets[0], targets[0]]).is_err());
    assert!(from_keys(&world, root_a, &[]).is_err());
    assert!(
        bindings(&world, root_a, &targets, |_, _| Err(
            "invalid source key".into()
        ))
        .is_err()
    );
    assert_eq!(rotations(&world, &parts_a), before_a);
    assert_eq!(rotations(&world, &parts_b), before_b);

    let resolved_a = from_keys(&world, root_a, &targets).unwrap();
    let mut first = Resident::bind(&mut world, &a, root_a, resolved_a).unwrap();
    let mut second = Resident::bind(&mut world, &b, root_b, resolved_b).unwrap();
    first.advance(&mut world, 0.04, wind()).unwrap();
    assert_eq!(rotations(&world, &parts_b), before_b);
    second.advance(&mut world, 0.04, wind()).unwrap();
    assert_eq!(rotations(&world, &parts_a), rotations(&world, &parts_b));
    first.release(&mut world);
    second.release(&mut world);
}
