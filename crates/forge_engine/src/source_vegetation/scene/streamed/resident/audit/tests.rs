use super::*;
use crate::source_vegetation::scene::streamed::tests::{Fixture, material::PlantMaterial, world};
use forge_core::EntityId;
use forge_world::imported_vegetation::streaming::PlantRepresentation;

// CPU integrity only: attach the streamed identity controls to warm entities.
// No GPU evidence is fabricated; `ready` must still refuse this world.
fn cpu_projection() -> (World, Resident<PlantMaterial>, Vec<Entity>) {
    let f = Fixture::with_canopy_stretch(0.0);
    let (mut world, base) = world();
    let mut resident = Resident::stage(
        &mut world,
        f.prepare(),
        &base,
        EntityId::deterministic("incremental-cpu-audit"),
    )
    .unwrap();
    let entities = resident.inner.entities();
    let levels: Vec<_> = resident.inner.entities_with_lod().collect();
    for (entity, lod) in levels {
        let id = world.get::<ImportedPlant>(entity).unwrap().id;
        let source_lod = u8::try_from(lod).unwrap();
        resident
            .parts
            .insert((id, source_lod), PlantRepresentation::Detail);
        world.entity_mut(entity).insert(StreamedPlantPart {
            id,
            representation: PlantRepresentation::Detail,
            source_lod,
        });
    }
    resident.published = true;
    resident.selected = resident.parts.len();
    world.increment_change_tick();
    world.increment_change_tick();
    assert!(resident.projection_intact(&world));
    assert!(resident.audited_projection_intact(&world));
    assert!(!resident.ready(&world));
    (world, resident, entities)
}

#[test]
fn picking_reuses_published_instances_and_refuses_a_changed_projection() {
    let (mut world, mut resident, entities) = cpu_projection();
    let meshes = world.resource::<Assets<Mesh>>().len();
    let materials = world.resource::<Assets<PlantMaterial>>().len();
    let snapshot = resident
        .picking_snapshot(&world, |_, first, count| Ok((first, count)))
        .unwrap();
    assert!(snapshot.identity_count() > 0);
    assert!(snapshot.visible_count() > 0);
    assert_eq!(world.resource::<Assets<Mesh>>().len(), meshes);
    assert_eq!(world.resource::<Assets<PlantMaterial>>().len(), materials);
    resident.published = false;
    assert!(resident.picking_snapshot(&world, |_, _, _| Ok(())).is_err());
    resident.published = true;
    world
        .get_mut::<Transform>(entities[0])
        .unwrap()
        .translation
        .x = 1.0;
    assert!(resident.picking_snapshot(&world, |_, _, _| Ok(())).is_err());
}

#[test]
fn unchanged_projection_skips_full_audits_but_same_tick_mutations_are_refused() {
    let (mut world, resident, entities) = cpu_projection();
    let previous = resident.audit.lock().unwrap().complete_audits;
    for _ in 0..4 {
        world.increment_change_tick();
        assert!(resident.audited_projection_intact(&world));
    }
    assert_eq!(resident.audit.lock().unwrap().complete_audits, previous);
    world
        .get_mut::<Transform>(entities[0])
        .unwrap()
        .translation
        .x = 1.0;
    assert!(!resident.audited_projection_intact(&world));
    world
        .get_mut::<Transform>(entities[0])
        .unwrap()
        .translation
        .x = 0.0;
    assert!(resident.audited_projection_intact(&world));
    assert!(!resident.ready(&world));
}

#[test]
fn component_removal_parenting_and_material_mutations_revoke_cached_integrity() {
    let (mut world, resident, entities) = cpu_projection();
    let entity = entities[0];
    world.increment_change_tick();
    assert!(resident.audited_projection_intact(&world));
    world.entity_mut(entity).remove::<NoFrustumCulling>();
    assert!(!resident.audited_projection_intact(&world));
    world.entity_mut(entity).insert(NoFrustumCulling);
    assert!(resident.audited_projection_intact(&world));
    let parent = world.spawn_empty().id();
    world.entity_mut(entity).insert(ChildOf(parent));
    assert!(!resident.audited_projection_intact(&world));
    world.entity_mut(entity).remove::<ChildOf>();
    assert!(resident.audited_projection_intact(&world));
    let material = world
        .get::<MeshMaterial3d<PlantMaterial>>(entity)
        .unwrap()
        .0
        .clone();
    let kind = world
        .resource::<Assets<PlantMaterial>>()
        .get(&material)
        .unwrap()
        .kind;
    world
        .resource_mut::<Assets<PlantMaterial>>()
        .get_mut(&material)
        .unwrap()
        .kind = kind + 1;
    assert!(!resident.audited_projection_intact(&world));
    world
        .resource_mut::<Assets<PlantMaterial>>()
        .get_mut(&material)
        .unwrap()
        .kind = kind;
    assert!(resident.audited_projection_intact(&world));
}

#[test]
fn balanced_despawn_and_foreign_replacement_cannot_hide_a_missing_owned_entity() {
    let (mut world, resident, entities) = cpu_projection();
    let entity = entities[0];
    world.increment_change_tick();
    assert!(resident.audited_projection_intact(&world));
    let mesh = world.get::<Mesh3d>(entity).unwrap().clone();
    let material = world
        .get::<MeshMaterial3d<PlantMaterial>>(entity)
        .unwrap()
        .clone();
    let tag = world.get::<MeshTag>(entity).unwrap().clone();
    let plant = *world.get::<ImportedPlant>(entity).unwrap();
    let part = *world.get::<StreamedPlantPart>(entity).unwrap();
    assert!(world.despawn(entity));
    world.spawn((
        mesh,
        material,
        tag,
        plant,
        part,
        Transform::IDENTITY,
        Visibility::Visible,
        NoFrustumCulling,
        NotShadowCaster,
    ));
    assert!(!resident.audited_projection_intact(&world));
    assert!(!resident.ready(&world));
}

#[test]
fn owner_marker_or_streamed_identity_removal_is_not_a_valid_cache_hit() {
    let (mut world, resident, entities) = cpu_projection();
    let entity = entities[0];
    let plant = *world.get::<ImportedPlant>(entity).unwrap();
    world.increment_change_tick();
    assert!(resident.audited_projection_intact(&world));
    world.entity_mut(entity).remove::<ImportedPlant>();
    assert!(!resident.audited_projection_intact(&world));
    world.entity_mut(entity).insert(plant);
    assert!(resident.audited_projection_intact(&world));
    let part = *world.get::<StreamedPlantPart>(entity).unwrap();
    world.entity_mut(entity).remove::<StreamedPlantPart>();
    assert!(!resident.audited_projection_intact(&world));
    world.entity_mut(entity).insert(part);
    assert!(resident.audited_projection_intact(&world));
    world.get_mut::<StreamedPlantPart>(entity).unwrap().id =
        EntityId::deterministic("foreign-identity");
    assert!(!resident.audited_projection_intact(&world));
}

#[test]
fn balanced_removal_only_moves_are_detected_even_after_removal_messages_age_out() {
    let (mut world, resident, entities) = cpu_projection();
    let entity = entities[0];
    let parent = world.spawn_empty().id();
    let foreign = world
        .spawn((
            world.get::<Mesh3d>(entity).unwrap().clone(),
            world
                .get::<MeshMaterial3d<PlantMaterial>>(entity)
                .unwrap()
                .clone(),
            world.get::<MeshTag>(entity).unwrap().clone(),
            *world.get::<ImportedPlant>(entity).unwrap(),
            *world.get::<StreamedPlantPart>(entity).unwrap(),
            Transform::IDENTITY,
            Visibility::Visible,
            NoFrustumCulling,
            NotShadowCaster,
            ChildOf(parent),
        ))
        .id();
    for _ in 0..3 {
        world.increment_change_tick();
        assert!(resident.audited_projection_intact(&world));
    }
    let previous = resident.audit.lock().unwrap().complete_audits;
    world.entity_mut(entity).remove::<ImportedPlant>();
    world.entity_mut(foreign).remove::<ChildOf>();
    // Neither removed component can satisfy a Changed<T> filter afterwards.
    // Structural matching count stays equal; the removal cursor must revoke.
    world.clear_trackers();
    world.clear_trackers();
    assert!(!resident.audited_projection_intact(&world));
    assert!(resident.audit.lock().unwrap().complete_audits > previous);
}
