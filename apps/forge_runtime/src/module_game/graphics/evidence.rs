use super::{Policy, State, runtime};
use crate::module_game::evidence_contract::{
    GraphicsIdentity, GraphicsObservation,
    graphics::{GraphicsPublication, GraphicsScope},
};
use bevy::prelude::*;
use forge_engine::render_inventory;

pub fn expected(
    loaded: &forge_delivery::module_runtime::LoadedModuleZone,
    procedural_streaming: bool,
) -> Result<GraphicsIdentity, String> {
    let terrain = loaded.zone.head().effective()?.0;
    let policy = if procedural_streaming {
        Policy::with_procedural_streaming(&terrain, true)?
    } else {
        Policy::new(&terrain)?
    };
    Ok(GraphicsIdentity {
        head: loaded.zone.head().content_hash()?,
        policy: policy.identity,
        water: loaded
            .manifest
            .ocean_inputs
            .iter()
            .any(|binding| binding.area_id == loaded.zone.head().base.terrain.area_id),
    })
}
pub fn observe(world: &mut World) -> Option<GraphicsObservation> {
    if !world.contains_resource::<State>() || world.resource::<State>().requested_custom.is_some() {
        return None;
    }
    Some(world.resource_scope(|world, state: Mut<State>| {
        let scopes = state
            .active_scopes
            .iter()
            .filter_map(|name| {
                let evidence = render_inventory::scoped_evidence(world, state.scope_owner(name))?;
                Some(GraphicsScope {
                    name: (*name).into(),
                    owner: evidence.owner,
                    identity: evidence.identity,
                    generation: evidence.generation,
                    epoch: evidence.epoch,
                    observed: evidence.observed,
                    completed: evidence.completed,
                    required_frame: state
                        .scope_revisions
                        .get(name)
                        .map(|revision| revision.required_frame),
                    publication: state.publications.get(name).map(|publication| {
                        GraphicsPublication {
                            frame: publication.frame,
                            sources: publication.sources,
                            seal: publication.seal,
                        }
                    }),
                    token_current: runtime::scope_token(world, &state, name).is_some(),
                    meshes: [
                        evidence.assets.meshes_expected,
                        evidence.assets.meshes_prepared,
                    ],
                    materials: [
                        evidence.assets.materials_expected,
                        evidence.assets.materials_bound,
                    ],
                    images: [
                        evidence.assets.images_expected,
                        evidence.assets.images_prepared,
                    ],
                    buffers: [
                        evidence.assets.buffers_expected,
                        evidence.assets.buffers_prepared,
                    ],
                    pipelines: [
                        evidence.pipelines_total,
                        evidence.pipelines_pending,
                        evidence.pipelines_failed,
                    ],
                    effect_groups: [
                        evidence.effects.required_groups,
                        evidence.effects.prepared_groups,
                    ],
                    effects_ready: evidence.effects.public_resources_ready,
                    camera_ready: evidence.view.camera_resources_ready,
                })
            })
            .collect();
        GraphicsObservation {
            identity: GraphicsIdentity {
                head: state.source,
                policy: state.policy.identity,
                water: state.water,
            },
            requested: state.requested,
            applied: state.applied,
            persisted: state.saved,
            request_sequence: state.request_sequence,
            generation: state.generation,
            application_frame: state.applied_frame,
            cpu_ready: runtime::cpu_ready(world, &state).unwrap_or(false),
            ready: state.ready,
            error: state.error.clone(),
            preference_error: state.preference_error.clone(),
            scopes,
            source_terrain: source_terrain(world, &state),
            source_surfaces: {
                let token = runtime::scope_token(world, &state, "terrain");
                forge_engine::source_lighting::surfaces::live::observe(world, token.as_ref())
                    .as_ref()
                    .map(crate::module_game::imported::surface_observation)
            },
            plant_completed_frame: world
                .get_resource::<render_inventory::RenderInventoryEvidence>()
                .and_then(|evidence| evidence.completed),
        }
    }))
}

fn source_terrain(
    world: &World,
    state: &State,
) -> Option<crate::module_game::evidence_contract::graphics::SourceTerrainObservation> {
    let source = world.get_resource::<forge_engine::source_terrain::host::Status>()?;
    let token = runtime::scope_token(world, state, "terrain");
    Some(
        crate::module_game::evidence_contract::graphics::SourceTerrainObservation {
            document: source.document,
            package: source.package,
            pending: source.pending,
            active_tiles: source.active_tiles,
            cached_tiles: source.cached_tiles,
            normals_ready: source.normals_ready,
            staged_frame: source.staged_frame,
            ready: forge_engine::source_terrain::host::ready(world, token.as_ref()),
            error: source.error.clone(),
        },
    )
}
