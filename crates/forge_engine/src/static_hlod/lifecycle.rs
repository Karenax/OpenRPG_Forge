use super::{
    InputGroup, Job, Live, MAX_DERIVED_BYTES, PreparedGroup, Routes, State, StaticHlodBatch,
    StaticHlodStats, collect, gpu,
};
use crate::static_hlod_geometry::{HlodGeometryPolicy, HlodPrimitive, prepare_aggregate};
use bevy::{
    asset::RenderAssetUsages,
    camera::visibility::RenderLayers,
    light::{NotShadowCaster, NotShadowReceiver},
    prelude::*,
    tasks::{AsyncComputeTaskPool, futures::check_ready},
};
use std::sync::Arc;

pub(super) fn update(world: &mut World) {
    world.resource_scope(|world, mut state: Mut<State>| {
        world.resource_scope(|world, mut metrics: Mut<StaticHlodStats>| {
            if !intact(world, &state) {
                retire(world, &mut state);
                state.request = None;
            }
            match collect::request(world, &mut state) {
                Ok(next) => {
                    if state.request != next {
                        retire(world, &mut state);
                        state.request = next;
                        if let Some(generation) = state.generation.checked_add(1) {
                            state.generation = generation;
                            state.attempted = false;
                            metrics.last_error = None;
                        } else {
                            state.request = None;
                            state.attempted = true;
                            metrics.last_error = Some("Static HLOD generation exhausted".into());
                        }
                    }
                }
                Err(error) => {
                    retire(world, &mut state);
                    state.request = None;
                    state.attempted = true;
                    metrics.last_error = Some(error);
                }
            }
            poll(world, &mut state, &mut metrics);
            if state.pending.is_none() && !state.attempted {
                state.attempted = true;
                start(&mut state);
            }
            if !state.live.is_empty() {
                if gpu::ready(world, state.generation) {
                    if !state.active {
                        activate(world, &mut state);
                        metrics.committed_transitions =
                            metrics.committed_transitions.saturating_add(1);
                    }
                } else {
                    deactivate(world, &mut state);
                }
            }
            observe(&state, &mut metrics);
        });
    });
}

fn start(state: &mut State) {
    let Some(request) = state
        .request
        .as_ref()
        .filter(|request| !request.groups.is_empty())
    else {
        return;
    };
    let mut input_bytes = 0;
    let inputs: Vec<_> = request
        .groups
        .iter()
        .map(|group| {
            let parts = group
                .members
                .iter()
                .map(|&index| {
                    let source = &request.sources[index];
                    let cached = &state.cache[&source.mesh];
                    input_bytes += cached.bytes;
                    let mut pose = source.pose;
                    pose.translation -= group.origin;
                    (Arc::clone(&cached.mesh), pose)
                })
                .collect();
            InputGroup {
                group: group.clone(),
                parts,
            }
        })
        .collect();
    let task = AsyncComputeTaskPool::get().spawn(async move {
        let start = bevy::platform::time::Instant::now();
        let result = prepare(inputs);
        (result, start.elapsed().as_secs_f64() * 1000.0)
    });
    state.pending = Some(Job {
        request: request.clone(),
        generation: state.generation,
        input_bytes,
        task,
    });
}

pub(super) fn prepare(inputs: Vec<InputGroup>) -> Result<Vec<PreparedGroup>, String> {
    let mut result = Vec::with_capacity(inputs.len());
    let mut bytes = 0;
    for input in inputs {
        let parts: Vec<_> = input
            .parts
            .iter()
            .map(|(mesh, pose)| HlodPrimitive { mesh, pose: *pose })
            .collect();
        let mut geometry = prepare_aggregate(
            &parts,
            HlodGeometryPolicy {
                maximum_position_error_m: (0.05 * 2.0_f64.powi(i32::from(input.group.node.level)))
                    .min(1.0),
                maximum_normal_delta: 0.1,
                // Numeric UV error is explicit; texture fidelity needs native review.
                maximum_uv_delta: 0.01,
            },
        )
        .map_err(|error| error.to_string())?;
        bytes += geometry.statistics.output_buffer_bytes;
        if bytes > MAX_DERIVED_BYTES {
            return Err("Static HLOD derived buffer budget exceeded".into());
        }
        geometry.mesh.asset_usage = RenderAssetUsages::default();
        result.push(PreparedGroup {
            group: input.group,
            geometry,
        });
    }
    Ok(result)
}

fn poll(world: &mut World, state: &mut State, metrics: &mut StaticHlodStats) {
    let Some((result, milliseconds)) = state
        .pending
        .as_mut()
        .and_then(|job| check_ready(&mut job.task))
    else {
        return;
    };
    let job = state.pending.take().expect("completed owned job");
    if job.generation != state.generation || state.request.as_ref() != Some(&job.request) {
        metrics.discarded_jobs = metrics.discarded_jobs.saturating_add(1);
        return;
    }
    metrics.last_preparation_ms = Some(milliseconds);
    match result {
        Ok(groups) => admit(world, state, groups),
        Err(error) => metrics.last_error = Some(error),
    }
}

pub(super) fn admit(world: &mut World, state: &mut State, groups: Vec<PreparedGroup>) {
    let routes = world.resource::<Routes>().0.clone();
    let request = state.request.as_ref().expect("matching admitted request");
    for PreparedGroup { group, geometry } in groups {
        let mesh = world.resource_mut::<Assets<Mesh>>().add(geometry.mesh);
        let entity = world
            .spawn((
                Name::new(format!(
                    "Static HLOD · {}:{}:{}",
                    group.node.level, group.node.x, group.node.z
                )),
                StaticHlodBatch {
                    generation: state.generation,
                    node: group.node,
                },
                Mesh3d(mesh.clone()),
                Transform::from_translation(group.origin),
                Visibility::Hidden,
                group.layers.clone(),
            ))
            .id();
        if group.flags & 1 != 0 {
            world.entity_mut(entity).insert(NotShadowCaster);
        }
        if group.flags & 2 != 0 {
            world.entity_mut(entity).insert(NotShadowReceiver);
        }
        let route = &routes[group.route];
        (route.insert)(
            world,
            entity,
            request.sources[group.members[0]].material.clone(),
        );
        (route.decorate)(world, entity, group.custom);
        state.live.push(Live {
            entity,
            mesh,
            group,
            bytes: geometry.statistics.output_buffer_bytes,
            triangles: geometry.statistics.output_triangles,
            statistics: geometry.statistics,
        });
    }
}

pub(super) fn activate(world: &mut World, state: &mut State) {
    let request = state
        .request
        .as_ref()
        .expect("live batches have an admitted request");
    for source in request
        .sources
        .iter()
        .filter(|source| source.node.is_some())
    {
        state.hidden.insert(source.entity, source.visibility);
        world.entity_mut(source.entity).insert(Visibility::Hidden);
    }
    for live in &state.live {
        world.entity_mut(live.entity).insert(Visibility::Visible);
    }
    state.active = true;
}

fn deactivate(world: &mut World, state: &mut State) {
    for (entity, visibility) in std::mem::take(&mut state.hidden) {
        if let Ok(mut entity) = world.get_entity_mut(entity) {
            entity.insert(visibility);
        }
    }
    for live in &state.live {
        if let Ok(mut entity) = world.get_entity_mut(live.entity) {
            entity.insert(Visibility::Hidden);
        }
    }
    state.active = false;
}

pub(super) fn retire(world: &mut World, state: &mut State) {
    deactivate(world, state);
    for live in state.live.drain(..) {
        world.despawn(live.entity);
        world.resource_mut::<Assets<Mesh>>().remove(live.mesh.id());
    }
}

pub(super) fn intact(world: &World, state: &State) -> bool {
    let Some(routes) = world.get_resource::<Routes>() else {
        return state.live.is_empty();
    };
    state
        .hidden
        .keys()
        .all(|entity| world.get::<Visibility>(*entity) == Some(&Visibility::Hidden))
        && (state.live.is_empty()
            || state.request.as_ref().is_some_and(|request| {
                request
                    .sources
                    .iter()
                    .all(|source| collect::source_is_intact(world, state, source))
            }))
        && state.live.iter().all(|live| {
            world
                .get::<StaticHlodBatch>(live.entity)
                .is_some_and(|marker| {
                    marker.generation == state.generation && marker.node == live.group.node
                })
                && world
                    .get::<Mesh3d>(live.entity)
                    .is_some_and(|mesh| mesh.0 == live.mesh)
                && world
                    .get_resource::<Assets<Mesh>>()
                    .is_some_and(|assets| assets.get(&live.mesh).is_some())
                && world.get::<Transform>(live.entity)
                    == Some(&Transform::from_translation(live.group.origin))
                && world.get::<ChildOf>(live.entity).is_none()
                && world.get::<Visibility>(live.entity)
                    == Some(&if state.active {
                        Visibility::Visible
                    } else {
                        Visibility::Hidden
                    })
                && world.get::<RenderLayers>(live.entity) == Some(&live.group.layers)
                && (world.get::<NotShadowCaster>(live.entity).is_some()
                    == (live.group.flags & 1 != 0))
                && (world.get::<NotShadowReceiver>(live.entity).is_some()
                    == (live.group.flags & 2 != 0))
                && routes.0.get(live.group.route).is_some_and(|route| {
                    (route.inspect)(world, live.entity).is_some_and(|material| {
                        material.is_ok_and(|material| {
                            material.can_bake && material.handle.id() == live.group.material
                        })
                    }) && (route.flags)(world, live.entity) == live.group.custom
                })
        })
}

fn observe(state: &State, metrics: &mut StaticHlodStats) {
    metrics.enabled = state
        .request
        .as_ref()
        .is_some_and(|request| request.enabled);
    metrics.generation = state.generation;
    metrics.source_primitives = state
        .request
        .as_ref()
        .map_or(0, |request| request.sources.len());
    metrics.source_objects = state.request.as_ref().map_or(0, |request| {
        request
            .sources
            .iter()
            .map(|source| source.identity.object)
            .collect::<std::collections::BTreeSet<_>>()
            .len()
    });
    metrics.source_triangles = state.request.as_ref().map_or(0, |request| {
        request
            .sources
            .iter()
            .map(|source| state.cache[&source.mesh].triangles)
            .sum()
    });
    metrics.selected_triangles = metrics.source_triangles;
    metrics.desired_batches = state
        .request
        .as_ref()
        .map_or(0, |request| request.groups.len());
    metrics.rendered_batches = if state.active { state.live.len() } else { 0 };
    metrics.replaced_primitives = state.hidden.len();
    if state.active {
        let replaced: usize = state
            .request
            .as_ref()
            .into_iter()
            .flat_map(|request| &request.sources)
            .filter(|source| source.node.is_some())
            .map(|source| state.cache[&source.mesh].triangles)
            .sum();
        metrics.selected_triangles = metrics.source_triangles - replaced
            + state.live.iter().map(|live| live.triangles).sum::<usize>();
    }
    metrics.source_snapshot_bytes = state.cache.values().map(|cached| cached.bytes).sum();
    metrics.retained_source_mesh_bytes = metrics.source_snapshot_bytes;
    metrics.derived_mesh_bytes = state.live.iter().map(|live| live.bytes).sum();
    metrics.maximum_position_error_m = state
        .live
        .iter()
        .map(|live| live.statistics.maximum_position_error_m)
        .fold(0.0, f64::max);
    metrics.minimum_face_alignment = state
        .live
        .iter()
        .map(|live| live.statistics.minimum_face_alignment)
        .reduce(f64::min);
    metrics.maximum_attribute_deltas.clear();
    for live in &state.live {
        for (name, delta) in &live.statistics.maximum_attribute_deltas {
            let current = metrics
                .maximum_attribute_deltas
                .entry((*name).into())
                .or_default();
            *current = current.max(*delta);
        }
    }
    let bound = metrics.retained_source_mesh_bytes
        + metrics.source_snapshot_bytes
        + metrics.derived_mesh_bytes
        + state
            .pending
            .as_ref()
            .map_or(0, |job| job.input_bytes + MAX_DERIVED_BYTES);
    metrics.peak_tracked_buffer_bound_bytes = metrics.peak_tracked_buffer_bound_bytes.max(bound);
    metrics.pending = state.pending.is_some() || (!state.live.is_empty() && !state.active);
}
