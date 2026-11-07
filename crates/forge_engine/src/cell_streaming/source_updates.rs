//! In-place replacement of derived cells; the public source follows successful admission only.
use super::{
    CellSource, CellStreamingObserver, PreparedBatch, SourceReplacement, State, admit, requested,
};
use bevy::prelude::{Resource, World};
use forge_core::ContentHash;
use forge_world::{
    cell_streaming::{CellStreamPlan, CellStreamer, SampledCellSource, SampledMeshQuality},
    sampled_terrain::SampledTerrainPlan,
};
use std::sync::Arc;

/// The immutable canonical source currently admitted by the cell adapter.
/// A queued edit never makes an old mesh appear to represent the new document.
#[derive(Resource)]
pub struct ResidentSampledTerrain {
    plan: Arc<SampledTerrainPlan>,
}

impl ResidentSampledTerrain {
    pub(super) const fn new(plan: Arc<SampledTerrainPlan>) -> Self {
        Self { plan }
    }

    #[must_use]
    pub const fn plan(&self) -> &Arc<SampledTerrainPlan> {
        &self.plan
    }
}

/// Queue the latest canonical edit in the existing world, without rebuilding its scene.
/// `expected` pins the currently admitted source, not a pending edit. Success means queued;
/// observe `ResidentSampledTerrain` and `CellStreamingStats` for completion or failure.
/// The same zone and grid are required. Passing the current source cancels a pending edit.
pub fn request_source_update(
    world: &mut World,
    expected: ContentHash,
    terrain: Arc<SampledTerrainPlan>,
) -> Result<(), String> {
    let state = world
        .get_resource::<State>()
        .ok_or("Sampled cell adapter is not installed")?;
    let current = state
        .source
        .sampled()
        .ok_or("Sampled cell adapter is not installed")?
        .terrain();
    if expected != current.document_hash() {
        return Err("Sampled cell source changed before the edit was queued".into());
    }
    if (terrain.document().project_id, terrain.document().area_id)
        != (current.document().project_id, current.document().area_id)
    {
        return Err("A sampled cell edit cannot replace another project or zone".into());
    }
    let quality = state
        .requested_source
        .as_ref()
        .unwrap_or(&state.source)
        .mesh_quality();
    let requested = if terrain.document_hash() == expected && quality == state.source.mesh_quality()
    {
        None
    } else {
        let source = SampledCellSource::with_mesh_quality(terrain, state.policy.clone(), quality)?;
        let candidate = replacement(state, source.clone())?;
        let request = requested(world, state)?;
        let plan = candidate
            .stream
            .prepare_with_protected_cells(
                world.resource::<CellStreamingObserver>().position_xz_m,
                &request.protected,
            )
            .map_err(|error| error.to_string())?;
        check_staging_budget(state, &plan)?;
        Some(source.into())
    };
    // Every fallible check precedes mutation, including when replacing an earlier pending edit.
    let mut state = world.resource_mut::<State>();
    state.requested_source = requested;
    state.last_request = None;
    Ok(())
}

/// Queue renderer-only topology on the exact current raster. Old meshes remain
/// live until bounded preparation and atomic admission finish. This is CPU
/// admission; a host still owes an independent GPU resource/fence observation.
pub fn request_mesh_quality(
    world: &mut World,
    expected: ContentHash,
    quality: SampledMeshQuality,
) -> Result<(), String> {
    let state = world
        .get_resource::<State>()
        .ok_or("Sampled cell adapter is not installed")?;
    if state.source.document_hash() != expected {
        return Err("Sampled terrain changed before the quality request".into());
    }
    if state
        .requested_source
        .as_ref()
        .is_some_and(|source| source.document_hash() != expected)
    {
        return Err(
            "Admit or cancel the pending terrain edit before changing its mesh quality".into(),
        );
    }
    let requested = if quality == state.source.mesh_quality() {
        None
    } else {
        let source = state.source.with_mesh_quality(&state.policy, quality)?;
        let candidate = replacement(state, source.clone())?;
        let request = requested(world, state)?;
        let plan = candidate
            .stream
            .prepare_with_protected_cells(
                world.resource::<CellStreamingObserver>().position_xz_m,
                &request.protected,
            )
            .map_err(|e| e.to_string())?;
        check_staging_budget(state, &plan)?;
        Some(source)
    };
    let mut state = world.resource_mut::<State>();
    state.requested_source = requested;
    state.last_request = None;
    Ok(())
}

/// Returns only the currently admitted CPU topology, excluding pending requests.
#[must_use]
pub fn admitted_mesh_quality(world: &World) -> Option<SampledMeshQuality> {
    world
        .get_resource::<State>()
        .map(|state| state.source.mesh_quality())
}

pub(super) fn replacement(
    state: &State,
    source: impl Into<CellSource>,
) -> Result<SourceReplacement, String> {
    let source = source.into();
    Ok(SourceReplacement {
        stream: CellStreamer::new(
            state.session_id,
            source.residency_source(),
            state.policy.clone(),
        )
        .map_err(|error| error.to_string())?,
        source,
    })
}

pub(super) fn check_staging_budget(state: &State, plan: &CellStreamPlan) -> Result<(), String> {
    let retained = state
        .stream
        .resident()
        .values()
        .try_fold(0_u64, |sum, cell| {
            sum.checked_add(cell.bytes)
                .ok_or("Retained sampled cell byte count overflow")
        })?;
    let peak = retained
        .checked_add(plan.estimated_peak_bytes)
        .ok_or("Sampled cell source replacement byte count overflow")?;
    if peak > state.policy.max_staging_bytes {
        return Err("Sampled cell source replacement exceeds the staging byte budget".into());
    }
    Ok(())
}

pub(super) fn admit_job(
    world: &mut World,
    state: &mut State,
    plan: &CellStreamPlan,
    prepared: PreparedBatch,
    replacement: Option<SourceReplacement>,
) -> Result<(), String> {
    let Some(next) = replacement else {
        return admit(world, state, plan, prepared);
    };
    check_staging_budget(state, plan)?;
    // Exclusive admission cannot expose these temporary swaps to another system. `admit`
    // validates live projections and all acknowledgements before its first ECS/asset mutation.
    let previous_source = std::mem::replace(&mut state.source, next.source);
    let previous_stream = std::mem::replace(&mut state.stream, next.stream);
    if let Err(error) = admit(world, state, plan, prepared) {
        state.source = previous_source;
        state.stream = previous_stream;
        return Err(error);
    }
    state.requested_source = None;
    if let Some(sampled) = state.source.sampled() {
        world.insert_resource(ResidentSampledTerrain::new(sampled.terrain().clone()));
    } else {
        world.remove_resource::<ResidentSampledTerrain>();
    }
    Ok(())
}
