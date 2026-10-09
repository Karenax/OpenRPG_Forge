//! Shared transient ownership for preview and persisted placement render roots.
//! Persistence remains in ManualPlacement; this holds only renderer handles.
//! No implicit clock, material replacement, reveal or LOD selection.
use super::{
    source_graph_context::{HostFrame, time_parameters},
    source_graph_host::SourceSceneHost,
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
/// Schedule after TransformSystems::Propagate, before render extraction.
/// The common viewport must explicitly supply its current/previous clock.
pub(super) fn update(world: &mut World) {
    if !world.contains_resource::<SourceSceneResidents>() {
        return;
    }
    let clock = world.get_resource::<SourceClock>().copied();
    world.resource_scope(|world, residents: Mut<SourceSceneResidents>| {
        let mut residents = residents;
        residents.scenes.retain(|root, resident| {
            // Despawned roots release handles naturally through Bevy's asset lifecycle.
            if world.get_entity(*root).is_err() {
                return false;
            }
            let result = clock
                .ok_or_else(|| "Explicit source viewport clock unavailable".to_owned())
                .and_then(|clock| resident.policy.frame(clock))
                .and_then(|frame| resident.host.update_runtime_frame(world, &frame));
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

/// The authoring viewport owns one current/previous Bevy clock. Paused time
/// remains paused; no wall-clock or captured asset timestamp is introduced.
fn capture_viewport_clock(time: Res<Time>, mut clock: ResMut<SourceClock>) {
    clock.current_seconds = time.elapsed_secs();
    clock.previous_seconds = time.elapsed_secs() - time.delta_secs();
}
pub(super) fn register(app: &mut App) {
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
