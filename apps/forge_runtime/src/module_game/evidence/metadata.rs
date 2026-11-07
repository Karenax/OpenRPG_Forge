//! Bounded dated metadata, not a reusable acknowledgement of later GPU work.
use super::super::evidence_contract::{GraphicsObservation, HostStatus, graphics::GraphicsScope};

// A real 6,874-frame walk/swim route exhausted 1,024 graphics snapshots at
// frame 4,866. Keep headroom for the same route and later admission changes,
// while retaining an independent bounded artifact size and 20,000 samples.
pub(super) const MAX_SNAPSHOTS: usize = 4096;
const HEARTBEAT_FRAMES: u32 = 60;
#[cfg(test)]
mod tests;

#[derive(Clone, serde::Serialize)]
pub(super) struct Snapshot<T> {
    pub frame: u32,
    pub observation: T,
}

#[derive(Clone, Default)]
pub(super) struct Metadata {
    pub graphics: Vec<Snapshot<GraphicsObservation>>,
    pub imported: Vec<Snapshot<HostStatus>>,
    pub truncated: bool,
    graphics_active: Option<u16>,
    imported_active: Option<u16>,
    projection_ready: bool,
}

impl Metadata {
    /// A whole-scene admission must retain the actual observations at that
    /// boundary, even when graphics alone became ready one frame earlier.
    pub fn projection_boundary(&mut self, ready: bool) {
        if ready && !self.projection_ready {
            self.graphics_active = None;
            self.imported_active = None;
        }
        self.projection_ready = ready;
    }

    pub fn observe(
        &mut self,
        frame: u32,
        graphics: Option<&GraphicsObservation>,
        imported: Option<&HostStatus>,
    ) -> (Option<u16>, Option<u16>) {
        let graphics = intern(
            &mut self.graphics,
            &mut self.graphics_active,
            frame,
            graphics,
            same_graphics,
            &mut self.truncated,
        );
        let imported = intern(
            &mut self.imported,
            &mut self.imported_active,
            frame,
            imported,
            same_imported,
            &mut self.truncated,
        );
        (graphics, imported)
    }
}

fn intern<T: Clone>(
    snapshots: &mut Vec<Snapshot<T>>,
    active: &mut Option<u16>,
    frame: u32,
    value: Option<&T>,
    same: impl Fn(&T, &T) -> bool,
    truncated: &mut bool,
) -> Option<u16> {
    let Some(value) = value else {
        *active = None;
        return None;
    };
    if let Some(previous) = active.and_then(|index| snapshots.get(usize::from(index)))
        && frame
            .checked_sub(previous.frame)
            .is_some_and(|age| age < HEARTBEAT_FRAMES)
        && same(&previous.observation, value)
    {
        return *active;
    }
    let Some(index) = u16::try_from(snapshots.len())
        .ok()
        .filter(|_| snapshots.len() < MAX_SNAPSHOTS)
    else {
        *active = None;
        *truncated = true;
        return None;
    };
    snapshots.push(Snapshot {
        frame,
        observation: value.clone(),
    });
    *active = Some(index);
    Some(index)
}

fn same_graphics(a: &GraphicsObservation, b: &GraphicsObservation) -> bool {
    (
        &a.identity,
        a.requested,
        a.applied,
        a.persisted,
        a.request_sequence,
        a.generation,
        a.application_frame,
        a.cpu_ready,
        a.ready,
        &a.error,
        &a.preference_error,
    ) == (
        &b.identity,
        b.requested,
        b.applied,
        b.persisted,
        b.request_sequence,
        b.generation,
        b.application_frame,
        b.cpu_ready,
        b.ready,
        &b.error,
        &b.preference_error,
    ) && a.source_terrain == b.source_terrain
        && same_surfaces(a.source_surfaces.as_ref(), b.source_surfaces.as_ref())
        && a.scopes.len() == b.scopes.len()
        && a.scopes
            .iter()
            .zip(&b.scopes)
            .all(|(a, b)| same_scope(a, b))
}

fn same_surfaces(
    a: Option<&super::super::evidence_contract::SurfaceStatus>,
    b: Option<&super::super::evidence_contract::SurfaceStatus>,
) -> bool {
    match (a, b) {
        (None, None) => true,
        (Some(a), Some(b)) => {
            (
                a.active,
                a.ready,
                a.generation,
                a.world_receivers,
                a.model_receivers,
                a.terrain_receivers,
                a.opaque_casters,
                a.plant_batches,
                a.motion_matches,
                a.encoded_frame,
                a.encoded_frames > 0,
                a.cascade_layers.map(|count| count > 0),
            ) == (
                b.active,
                b.ready,
                b.generation,
                b.world_receivers,
                b.model_receivers,
                b.terrain_receivers,
                b.opaque_casters,
                b.plant_batches,
                b.motion_matches,
                b.encoded_frame,
                b.encoded_frames > 0,
                b.cascade_layers.map(|count| count > 0),
            )
        }
        _ => false,
    }
}

fn same_scope(a: &GraphicsScope, b: &GraphicsScope) -> bool {
    (
        &a.name,
        a.owner,
        a.identity,
        a.generation,
        a.epoch,
        a.required_frame,
        &a.publication,
        a.token_current,
    ) == (
        &b.name,
        b.owner,
        b.identity,
        b.generation,
        b.epoch,
        b.required_frame,
        &b.publication,
        b.token_current,
    ) && (
        a.meshes,
        a.materials,
        a.images,
        a.buffers,
        a.pipelines,
        a.effect_groups,
        a.effects_ready,
        a.camera_ready,
    ) == (
        b.meshes,
        b.materials,
        b.images,
        b.buffers,
        b.pipelines,
        b.effect_groups,
        b.effects_ready,
        b.camera_ready,
    )
}

fn same_imported(a: &HostStatus, b: &HostStatus) -> bool {
    (
        a.identity,
        a.head,
        a.terrain,
        a.ready,
        &a.admission,
        (
            a.colliders,
            a.canonical_colliders,
            a.canonical_instances,
            a.entities,
            a.replacement_primitives,
        ),
        a.generation,
        a.sequence,
        a.detail,
        a.grouped,
        a.selected_triangles,
        &a.error,
    ) == (
        b.identity,
        b.head,
        b.terrain,
        b.ready,
        &b.admission,
        (
            b.colliders,
            b.canonical_colliders,
            b.canonical_instances,
            b.entities,
            b.replacement_primitives,
        ),
        b.generation,
        b.sequence,
        b.detail,
        b.grouped,
        b.selected_triangles,
        &b.error,
    )
}
