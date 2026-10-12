//! Shared transient ownership for preview and persisted placement render roots.
//! Persistence remains in ManualPlacement; this holds only renderer handles.
//! Viewport clock and camera drive source frames and measured perspective LODs.
//! Scene reveal remains a separate render admission.
use super::{
    source_graph_context::{HostFrame, time_parameters},
    source_graph_host::SourceSceneHost,
    source_graph_lod,
};
use bevy::{ecs as bevy_ecs, prelude::*};
use std::collections::BTreeMap;

#[derive(Resource, Clone, Copy)]
pub(super) struct SourceClock {
    pub current_seconds: f32,
    pub previous_seconds: f32,
}
#[derive(Clone, Copy)]
pub(super) struct RuntimePolicy {
    pub source_world_from_target_world: Mat4,
    pub source_absolute_world_from_target_world: Mat4,
    pub global_mip_bias: f32,
    pub source_float_min: f32,
}
impl RuntimePolicy {
    pub(super) fn frame(&self, clock: SourceClock) -> Result<HostFrame, String> {
        Ok(HostFrame {
            source_world_from_target_world: self.source_world_from_target_world,
            source_absolute_world_from_target_world: self.source_absolute_world_from_target_world,
            time_parameters: time_parameters(clock.current_seconds)?,
            previous_time_parameters: time_parameters(clock.previous_seconds)?,
            global_mip_bias: self.global_mip_bias,
            source_float_min: self.source_float_min,
        })
    }
}
struct Resident {
    host: SourceSceneHost,
    policy: RuntimePolicy,
}
#[derive(Resource, Default)]
pub(super) struct SourceSceneResidents {
    scenes: BTreeMap<Entity, Resident>,
}
#[derive(Component, Debug, Clone, PartialEq, Eq)]
pub(super) enum SourceFrameStatus {
    Updated,
    Refused(String),
}

/// Quality setting shared by the viewport; unity source measurements use 1.0.
#[derive(Resource)]
pub(super) struct SourceLodBias(pub f32);

fn viewport_lod_camera(world: &mut World) -> Result<source_graph_lod::LodCamera, String> {
    let views: Vec<_> = world
        .query_filtered::<(Entity, &Camera), forge_engine::tidewater_sky::preview::SceneCameraFilter>()
        .iter(world)
        .filter(|(_, camera)| camera.is_active)
        .map(|(entity, _)| entity)
        .collect();
    if views.len() != 1 {
        return Err("Exactly one active source viewport camera required".into());
    }
    let bias = world
        .get_resource::<SourceLodBias>()
        .ok_or("Source viewport LOD quality absent")?
        .0;
    source_graph_lod::camera(world, views[0], bias)
}

/// A successful mount owns its strong handles until this root is removed.
/// Do not remove borrowed assets or cache entries from Assets explicitly.
pub(super) fn retain(
    world: &mut World,
    host: SourceSceneHost,
    policy: RuntimePolicy,
) -> Result<(), String> {
    world.init_resource::<SourceSceneResidents>();
    if world
        .resource::<SourceSceneResidents>()
        .scenes
        .contains_key(&host.root)
    {
        return Err("A source host already owns this render root".into());
    }
    if world
        .get::<super::source_graph_scene::SourceSceneAttached>(host.root)
        .is_none()
    {
        return Err("Source root is not attached".into());
    }
    world
        .resource_mut::<SourceSceneResidents>()
        .scenes
        .insert(host.root, Resident { host, policy });
    Ok(())
}

/// Release this host's physical records before its hierarchy is removed.
/// The caller still owns despawning the root; source nodes and draws survive
/// this call. The returned count is released records, not backend contacts.
pub(super) fn release(world: &mut World, root: Entity) -> usize {
    let Some(mut residents) = world.get_resource_mut::<SourceSceneResidents>() else {
        return 0;
    };
    let Some(mut resident) = residents.scenes.remove(&root) else {
        return 0;
    };
    let count = resident.host.remove_colliders(world);
    if let Ok(mut entity) = world.get_entity_mut(root) {
        entity.remove::<SourceFrameStatus>();
    }
    count
}
/// Schedule after TransformSystems::Propagate, before render extraction.
/// The common viewport must explicitly supply its current/previous clock.
pub(super) fn update(world: &mut World) {
    if !world.contains_resource::<SourceSceneResidents>() {
        return;
    }
    let clock = world.get_resource::<SourceClock>().copied();
    let camera = viewport_lod_camera(world);
    world.resource_scope(|world, residents: Mut<SourceSceneResidents>| {
        let mut residents = residents;
        residents.scenes.retain(|root, resident| {
            // Despawned roots release handles naturally through Bevy's asset lifecycle.
            if world.get_entity(*root).is_err() {
                // Clear surviving owned children too, including a child that
                // lost its parent before the root disappeared.
                resident.host.remove_colliders(world);
                return false;
            }
            let result = clock
                .ok_or_else(|| "Explicit source viewport clock unavailable".to_owned())
                .and_then(|clock| resident.policy.frame(clock))
                .and_then(|frame| resident.host.update_runtime_frame(world, &frame))
                .and_then(|_| {
                    if resident
                        .host
                        .original_state
                        .as_ref()
                        .is_some_and(|state| !state.lod_groups.is_empty())
                    {
                        resident
                            .host
                            .update_lod(world, camera.as_ref().map_err(Clone::clone)?)?;
                    }
                    Ok(())
                });
            match result {
                Ok(_) => {
                    world.entity_mut(*root).insert(SourceFrameStatus::Updated);
                }
                Err(error) => {
                    world
                        .entity_mut(*root)
                        .insert((Visibility::Hidden, SourceFrameStatus::Refused(error)));
                }
            }
            true
        });
    });
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_engine::source_collider_projection::{ColliderProjection, SourceColliderIdentity};

    fn fixture(world: &mut World) -> (Entity, Entity, Entity, Entity, Entity) {
        let root = world
            .spawn((
                super::super::source_graph_scene::SourceSceneAttached,
                SourceFrameStatus::Updated,
                Name::new("owned root"),
            ))
            .id();
        let node = world.spawn((ChildOf(root), Name::new("source node"))).id();
        let draw = world.spawn((ChildOf(node), Name::new("source draw"))).id();
        let other = world.spawn_empty().id();
        let physical = world
            .spawn((
                ChildOf(node),
                SourceColliderIdentity {
                    placement_root: root,
                    guid: "owned fixture".into(),
                    file_id: "1".into(),
                    original: serde_json::json!({"owned_fixture":true}),
                    policy_evidence: "cleanup fixture".into(),
                },
            ))
            .id();
        let foreign = world
            .spawn((
                ChildOf(other),
                SourceColliderIdentity {
                    placement_root: other,
                    guid: "foreign fixture".into(),
                    file_id: "2".into(),
                    original: serde_json::json!({"owned_fixture":true}),
                    policy_evidence: "cleanup fixture".into(),
                },
            ))
            .id();
        let host = SourceSceneHost {
            root,
            attached: super::super::source_graph_scene::AttachedScene {
                nodes: BTreeMap::from([(0, node)]),
                primitives: vec![draw],
            },
            registered: super::super::source_graph_resources::RegisteredScene {
                owned_image_ids: Default::default(),
                sampling: Default::default(),
                bindings: Default::default(),
                original_passes: Default::default(),
            },
            original_state: None,
            object_basis: None,
            source_document: serde_json::json!({}),
            // Include a foreign record as a negative ownership control. The
            // real projection clear must preserve its differently owned child.
            collider_projection: Some(ColliderProjection {
                root,
                entities: vec![physical, foreign],
            }),
            collider_scene_projection: None,
        };
        retain(
            world,
            host,
            RuntimePolicy {
                source_world_from_target_world: Mat4::IDENTITY,
                source_absolute_world_from_target_world: Mat4::IDENTITY,
                global_mip_bias: 0.,
                source_float_min: f32::MIN_POSITIVE,
            },
        )
        .unwrap();
        (root, node, draw, physical, foreign)
    }

    #[test]
    fn release_preserves_source_hierarchy_and_foreign_children_and_is_idempotent() {
        let mut world = World::new();
        let (root, node, draw, physical, foreign) = fixture(&mut world);
        assert_eq!(release(&mut world, root), 2);
        assert!(world.get_entity(physical).is_err());
        for entity in [root, node, draw, foreign] {
            assert!(world.get_entity(entity).is_ok());
        }
        assert!(world.get::<SourceFrameStatus>(root).is_none());
        assert!(world.resource::<SourceSceneResidents>().scenes.is_empty());
        assert_eq!(release(&mut world, root), 0);
        assert_eq!(world.get::<Name>(root).unwrap().as_str(), "owned root");
    }

    #[test]
    fn vanished_root_releases_an_owned_child_that_lost_its_parent() {
        let mut world = World::new();
        let (root, _, _, physical, foreign) = fixture(&mut world);
        world.entity_mut(physical).remove::<ChildOf>();
        world.despawn(root);
        assert!(world.get_entity(physical).is_ok());
        update(&mut world);
        assert!(world.get_entity(physical).is_err());
        assert!(world.get_entity(foreign).is_ok());
        assert!(world.resource::<SourceSceneResidents>().scenes.is_empty());
        update(&mut world);
    }
}

/// The authoring viewport owns one current/previous Bevy clock. Paused time
/// remains paused; no wall-clock or captured asset timestamp is introduced.
fn capture_viewport_clock(time: Res<Time>, mut clock: ResMut<SourceClock>) {
    clock.current_seconds = time.elapsed_secs();
    clock.previous_seconds = time.elapsed_secs() - time.delta_secs();
}
pub(super) fn register(app: &mut App) {
    app.insert_resource(SourceLodBias(1.0));
    app.insert_resource(SourceClock {
        current_seconds: 0.,
        previous_seconds: 0.,
    });
    app.init_resource::<SourceSceneResidents>();
    app.add_systems(
        PostUpdate,
        (capture_viewport_clock, update)
            .chain()
            .after(bevy::transform::TransformSystems::Propagate),
    );
}
