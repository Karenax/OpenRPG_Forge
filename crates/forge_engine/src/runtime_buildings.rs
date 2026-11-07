//! Project canonical rooms through the same disposable adapter as the Studio.
use bevy::prelude::*;
use forge_core::ContentHash;
use forge_engine::authoring_buildings::BuildingProjection;
use forge_project::zone_authoring::ZoneAuthoringHead;

#[derive(Resource)]
struct Scene {
    head: ZoneAuthoringHead,
    hash: ContentHash,
    count: usize,
    projection: BuildingProjection,
    released: bool,
}

pub struct Observation {
    pub source_hash: ContentHash,
    pub count: usize,
    pub parts: usize,
    pub registered_parts: usize,
    pub ready: bool,
}

pub fn install(app: &mut App, head: &ZoneAuthoringHead) -> Result<(), String> {
    head.validate()?;
    let count = head
        .corrections
        .buildings
        .as_deref()
        .unwrap_or_default()
        .len();
    let hash = head.content_hash()?;
    if count != 0 {
        // Rapier still registers the static geometry while player input waits.
        // Release this initial barrier once; never override a later save-recovery pause.
        app.insert_resource(forge_engine::PlayerSimulationReady(false));
    }
    app.insert_resource(Scene {
        head: head.clone(),
        hash,
        count,
        projection: BuildingProjection::default(),
        released: count == 0,
    })
    .add_systems(PostStartup, spawn)
    .add_systems(PreUpdate, release_initial_barrier);
    Ok(())
}

fn spawn(world: &mut World) {
    world.resource_scope(|world, mut scene: Mut<Scene>| {
        let Scene {
            head, projection, ..
        } = &mut *scene;
        if let Err(error) = projection.replace(world, head, false) {
            error!(%error, "Canonical module building projection failed");
            world.write_message(AppExit::error());
            return;
        }
        let source = projection
            .source_hash()
            .expect("successful building replacement");
        let admitted: std::collections::BTreeSet<_> = head
            .corrections
            .buildings
            .as_deref()
            .unwrap_or_default()
            .iter()
            .map(|building| building.id)
            .collect();
        let parts: Vec<_> = world
            .query::<(
                Entity,
                &forge_engine::authoring_buildings::BuildingPartIdentity,
            )>()
            .iter(world)
            .filter(|(_, part)| admitted.contains(&part.building))
            .map(|(entity, part)| (entity, part.building))
            .collect();
        for (entity, object) in parts {
            world
                .entity_mut(entity)
                .insert(forge_engine::static_hlod::StaticHlodSource { object, source });
        }
        // The procedural room has no authored luminaires yet. Give this neutral
        // presentation usable interior lighting under its opaque roof without
        // changing sunlight, outdoor exposure or the canonical room document.
        // The validated document bounds this to at most 32 rooms.
        for room in head.corrections.buildings.as_deref().unwrap_or_default() {
            world.spawn((
                Name::new(format!("Room {} / neutral interior light", room.id)),
                PointLight {
                    intensity: 25_000.0,
                    range: room.dimensions.width_m.hypot(room.dimensions.depth_m) * 0.5
                        + room.dimensions.clear_height_m,
                    radius: 0.25,
                    shadow_maps_enabled: true,
                    ..default()
                },
                Transform::from_xyz(
                    room.origin_m.x,
                    room.origin_m.y + room.dimensions.clear_height_m * 0.75,
                    room.origin_m.z,
                ),
            ));
        }
    });
}

fn release_initial_barrier(world: &mut World) {
    world.resource_scope(|world, mut scene: Mut<Scene>| {
        if !scene.released
            && scene
                .projection
                .source_ready(world, scene.hash, scene.count)
        {
            scene.released = true;
            world
                .resource_mut::<forge_engine::PlayerSimulationReady>()
                .0 = true;
        }
    });
}

pub fn observe(world: &World) -> Option<Observation> {
    let scene = world.get_resource::<Scene>()?;
    let parts = scene.projection.diagnostic_parts(world);
    let covered = forge_engine::static_hlod::covered_sources(world);
    Some(Observation {
        source_hash: scene.hash,
        count: scene.count,
        parts: parts.len(),
        registered_parts: parts
            .iter()
            .filter(|part| part.rapier_collider_handle.is_some())
            .count(),
        ready: scene
            .projection
            .source_ready(world, scene.hash, scene.count)
            && scene
                .projection
                .part_entities()
                .all(|entity| covered.contains(&entity)),
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy_rapier3d::prelude::{Collider, NoUserData, RapierPhysicsPlugin};
    use forge_core::{CommandId, EntityId};
    use forge_project::{
        authoring::new_terrain,
        building::RoomDimensions,
        terrain_edit::TerrainEditSession,
        zone_authoring::{
            GeneratorStamp,
            buildings::{AuthoredBuilding, BuildingEdit},
            migration::ZoneMigrationProposal,
            session::ZoneEditSession,
        },
    };

    fn head(with_room: bool) -> ZoneAuthoringHead {
        let terrain = new_terrain("module-building", "room", "Room").unwrap();
        let hash = terrain.content_hash().unwrap();
        let legacy = TerrainEditSession::new(terrain).unwrap();
        let proposal = ZoneMigrationProposal::prepare(
            &legacy.archive_bytes().unwrap(),
            hash,
            GeneratorStamp {
                name: "fixture".into(),
                version: "1".into(),
                inputs_hash: hash,
            },
            None,
        )
        .unwrap();
        let mut session = ZoneEditSession::from_migration(proposal).unwrap();
        if with_room {
            let request = session
                .head()
                .prepare_building(
                    CommandId::deterministic("room"),
                    BuildingEdit::Insert {
                        building: AuthoredBuilding {
                            id: EntityId::deterministic("room"),
                            name: "Refuge".into(),
                            generator_version: 1,
                            origin_m: forge_core::Vec3::new(4.0, 0.0, 7.0),
                            yaw_degrees: 90.0,
                            dimensions: RoomDimensions {
                                width_m: 8.0,
                                depth_m: 6.0,
                                clear_height_m: 3.0,
                                wall_m: 0.25,
                                slab_m: 0.2,
                                door_width_m: 1.4,
                                door_height_m: 2.2,
                                door_offset_x_m: 0.0,
                            },
                            door_offset_override_m: Some(1.0),
                            locked: false,
                        },
                    },
                )
                .unwrap();
            session.execute(&request).unwrap();
        }
        session.head().clone()
    }

    fn app(head: &ZoneAuthoringHead) -> App {
        let mut app = App::new();
        app.add_plugins((
            MinimalPlugins,
            TransformPlugin,
            RapierPhysicsPlugin::<NoUserData>::default(),
        ))
        .init_resource::<Assets<Mesh>>()
        .init_resource::<Assets<StandardMaterial>>()
        .insert_resource(forge_engine::PlayerSimulationReady(true));
        install(&mut app, head).unwrap();
        app
    }

    #[test]
    fn canonical_room_blocks_player_until_registered_and_preserves_a_later_recovery_pause() {
        let head = head(true);
        let original = head.content_hash().unwrap();
        let mut app = app(&head);
        assert!(
            !app.world()
                .resource::<forge_engine::PlayerSimulationReady>()
                .0
        );
        assert!(!observe(app.world()).unwrap().ready);
        for _ in 0..8 {
            app.update();
        }
        let observed = observe(app.world()).unwrap();
        assert!(observed.ready);
        assert_eq!(
            (observed.count, observed.parts, observed.registered_parts),
            (1, 8, 8)
        );
        assert_eq!(observed.source_hash, original);
        assert_eq!(app.world().resource::<Scene>().head, head);
        assert!(
            app.world()
                .resource::<forge_engine::PlayerSimulationReady>()
                .0
        );
        app.world_mut()
            .resource_mut::<forge_engine::PlayerSimulationReady>()
            .0 = false;
        app.update();
        assert!(
            !app.world()
                .resource::<forge_engine::PlayerSimulationReady>()
                .0
        );
        let part = app
            .world_mut()
            .query_filtered::<Entity, With<Collider>>()
            .iter(app.world())
            .next()
            .unwrap();
        app.world_mut().entity_mut(part).remove::<Collider>();
        assert!(!observe(app.world()).unwrap().ready);
    }

    #[test]
    fn module_without_rooms_keeps_the_existing_player_barrier() {
        let mut app = app(&head(false));
        assert!(
            app.world()
                .resource::<forge_engine::PlayerSimulationReady>()
                .0
        );
        app.update();
        let observed = observe(app.world()).unwrap();
        assert!(observed.ready);
        assert_eq!((observed.count, observed.parts), (0, 0));
    }

    #[test]
    fn physical_readiness_does_not_claim_coverage_of_a_hidden_original() {
        let mut app = app(&head(true));
        for _ in 0..8 {
            app.update();
        }
        let part = app
            .world()
            .resource::<Scene>()
            .projection
            .part_entities()
            .next()
            .unwrap();
        app.world_mut().entity_mut(part).insert(Visibility::Hidden);
        let scene = app.world().resource::<Scene>();
        assert!(
            scene
                .projection
                .source_ready(app.world(), scene.hash, scene.count)
        );
        assert!(!scene.projection.ready(app.world(), scene.hash, scene.count));
        assert!(!observe(app.world()).unwrap().ready);
        app.world_mut()
            .entity_mut(part)
            .insert(Visibility::Inherited);
        assert!(observe(app.world()).unwrap().ready);
    }
}
