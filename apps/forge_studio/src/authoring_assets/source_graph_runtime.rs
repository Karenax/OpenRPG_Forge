//! Shared transient ownership for preview and persisted placement render roots.
//! Persistence remains in `ManualPlacement`; this holds only renderer handles.
//! Viewport clock and camera drive source frames and measured perspective LODs.
//! Scene reveal remains a separate render admission.
use super::{
    source_graph_context::{HostFrame, time_parameters},
    source_graph_host::SourceSceneHost,
    source_graph_lod,
};
use bevy::{ecs as bevy_ecs, prelude::*};
use leptos::prelude::Callable;
use std::collections::BTreeMap;

#[derive(Resource, Clone, Copy)]
pub(super) struct SourceClock {
    pub current_seconds: f32,
    pub previous_seconds: f32,
    pub delta_seconds: f32,
    pub sequence: u64,
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
    particle_input: forge_engine::source_particle_host_input::HostedParticleInput,
    input_error: Option<String>,
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
        .query_filtered::<(Entity, &Camera), forge_tidewater::tidewater_sky::preview::SceneCameraFilter>()
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
    mut host: SourceSceneHost,
    policy: RuntimePolicy,
) -> Result<(), String> {
    world.init_resource::<SourceSceneResidents>();
    if world
        .resource::<SourceSceneResidents>()
        .scenes
        .contains_key(&host.root)
    {
        host.release_particle_instances(world);
        return Err("A source host already owns this render root".into());
    }
    if world
        .get::<super::source_graph_scene::SourceSceneAttached>(host.root)
        .is_none()
    {
        host.release_particle_instances(world);
        return Err("Source root is not attached".into());
    }
    world.resource_mut::<SourceSceneResidents>().scenes.insert(
        host.root,
        Resident {
            host,
            policy,
            particle_input: forge_engine::source_particle_host_input::HostedParticleInput::default(
            ),
            input_error: None,
        },
    );
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
    resident.host.release_particle_instances(world);
    if let Ok(mut entity) = world.get_entity_mut(root) {
        entity.remove::<SourceFrameStatus>();
    }
    count
}
/// Schedule after `TransformSystems::Propagate`, before render extraction.
/// The common viewport must explicitly supply its current/previous clock.
pub(super) fn update(world: &mut World) {
    if !world.contains_resource::<SourceSceneResidents>() {
        return;
    }
    let clock = world.get_resource::<SourceClock>().copied();
    let camera = viewport_lod_camera(world);
    let particle_camera = current_particle_camera(world);
    world.resource_scope(|world, residents: Mut<SourceSceneResidents>| {
        let mut residents = residents;
        residents.scenes.retain(|root, resident| {
            // Despawned roots release handles naturally through Bevy's asset lifecycle.
            if world.get_entity(*root).is_err() {
                // Clear surviving owned children too, including a child that
                // lost its parent before the root disappeared.
                resident.host.remove_colliders(world);
                resident.host.release_particle_instances(world);
                return false;
            }
            resident.host.invalidate_particle_frames(world);
            let result = resident.input_error.as_ref().map_or_else(
                || {
                    if !resident.host.particle_instances.is_empty() {
                        let (pose, clip) = particle_camera.as_ref().map_err(Clone::clone)?;
                        resident.host.update_particle_frames(
                            world,
                            resident.policy.source_absolute_world_from_target_world,
                            *pose,
                            *clip,
                        )?;
                    }
                    clock
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
                                let decisions = resident
                                    .host
                                    .update_lod(world, camera.as_ref().map_err(Clone::clone)?)?;
                                if bevy::log::tracing::enabled!(bevy::log::Level::TRACE) {
                                    for decision in decisions {
                                        bevy::log::trace!(source_root=?root,source_node=decision.node,
                                            relative_height=decision.relative_height,selected_lod=?decision.level,
                                            "source LOD decision");
                                    }
                                }
                            }
                            Ok(())
                        })
                },
                |error| Err(error.clone()),
            );
            match result {
                Ok(()) => {
                    world.entity_mut(*root).insert(SourceFrameStatus::Updated);
                }
                Err(error) => {
                    resident.host.cancel_particle_frames(world);
                    world
                        .entity_mut(*root)
                        .insert((Visibility::Hidden, SourceFrameStatus::Refused(error)));
                }
            }
            true
        });
    });
}

fn current_particle_camera(world: &mut World) -> Result<(Mat4, Mat4), String> {
    let views:Vec<_>=world.query_filtered::<(Entity,&Camera),forge_tidewater::tidewater_sky::preview::SceneCameraFilter>()
        .iter(world).filter(|(_,camera)|camera.is_active).map(|(entity,_)|entity).collect();
    if views.len() != 1 {
        return Err("Exactly one current particle viewport camera required".into());
    }
    let entity = views[0];
    if world.get::<Camera3d>(entity).is_none()
        || world
            .get::<bevy::render::camera::TemporalJitter>(entity)
            .is_some()
    {
        return Err("Particle viewport requires a current non-jittered 3D camera".into());
    }
    let camera = world.get::<Camera>(entity).unwrap();
    let pose = world
        .get::<GlobalTransform>(entity)
        .ok_or("Current particle camera transform absent")?
        .to_matrix();
    let clip = camera.clip_from_view();
    if camera.physical_viewport_size().is_none() {
        return Err("Current particle camera viewport not computed".into());
    }
    Ok((pose, clip))
}

/// The authoring viewport owns one current/previous Bevy clock. Paused time
/// remains paused; no wall-clock or captured asset timestamp is introduced.
fn capture_viewport_clock(time: Res<Time>, mut clock: ResMut<SourceClock>) {
    let time = time.into_inner();
    clock.current_seconds = time.elapsed_secs();
    clock.previous_seconds = time.elapsed_secs() - time.delta_secs();
    clock.delta_seconds = time.delta_secs();
    clock.sequence = clock.sequence.wrapping_add(1);
}

/// Runs before owner Update systems, so this explicit delta reaches the same frame.
fn forward_particle_inputs(world: &mut World) {
    let clock = world.get_resource::<SourceClock>().copied();
    let controls = super::source_graph_particles::take_controls();
    world.resource_scope(|world, residents: Mut<SourceSceneResidents>| {
        let mut residents = residents;
        for control in controls {
            let root = match control.target {
                super::source_graph_particles::ControlTarget::Preview => {
                    super::source_graph_preview::current_root(world)
                }
                super::source_graph_particles::ControlTarget::Placement(id) => {
                    let roots: Vec<_> = world
                        .query::<(Entity, &forge_engine::ManualPlacementProjection)>()
                        .iter(world)
                        .filter(|(entity, placement)| {
                            placement.0.id == id && residents.scenes.contains_key(entity)
                        })
                        .map(|(entity, _)| entity)
                        .collect();
                    if roots.len() == 1 {
                        Some(roots[0])
                    } else {
                        None
                    }
                }
            };
            let result = (|| {
                let resident = residents
                    .scenes
                    .get_mut(&root.ok_or("Effet chargé introuvable")?)
                    .ok_or("Effet chargé introuvable")?;
                if resident.host.particle_instances.is_empty() {
                    return Err("Cet objet ne contient pas d’effet chargé".into());
                }
                if let Some(error) = &resident.input_error {
                    return Err(error.clone());
                }
                resident.particle_input.push_event(control.event)
            })();
            control.reply.run(result);
        }
        for (root, resident) in &mut residents.scenes {
            if resident.host.particle_instances.is_empty() || resident.input_error.is_some() {
                continue;
            }
            let result = clock
                .ok_or("Horloge explicite de scène indisponible".to_owned())
                .and_then(|clock| {
                    resident.particle_input.advance(
                        world,
                        &resident.host.particle_instances,
                        clock.sequence,
                        clock.delta_seconds,
                    )
                });
            if let Err(error) = result {
                resident.input_error = Some(error.clone());
                if let Ok(mut entity) = world.get_entity_mut(*root) {
                    entity.insert((Visibility::Hidden, SourceFrameStatus::Refused(error)));
                }
            }
        }
    });
}
pub(super) fn register(app: &mut App) {
    app.insert_resource(SourceLodBias(1.0));
    app.insert_resource(SourceClock {
        current_seconds: 0.,
        previous_seconds: 0.,
        delta_seconds: 0.,
        sequence: 0,
    });
    app.init_resource::<SourceSceneResidents>();
    app.add_systems(
        PreUpdate,
        (capture_viewport_clock, forward_particle_inputs).chain(),
    );
    app.add_systems(
        PostUpdate,
        update
            .after(bevy::transform::TransformSystems::Propagate)
            .after(bevy::camera::CameraUpdateSystems)
            .before(forge_engine::source_particle_compute::SourceParticleProjectionUpdate),
    );
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
            carrier_sha256: forge_core::bytes_sha256(b"fixture"),
            particle_instances: Vec::new(),
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
