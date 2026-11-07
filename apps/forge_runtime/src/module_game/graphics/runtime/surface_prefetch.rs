//! Bounded preparation of future source terrain and water without scene publication.
use super::super::{Publication, ScopeRevision, owner, prepared_owner};
use super::{State, family, identity, scope_token, signature_family};
use bevy::{diagnostic::FrameCount, pbr::Material, prelude::*};
use forge_core::{EntityId, canonical_json_sha256};
use forge_engine::{
    render_inventory::{
        self, ActivatedScopedInventory, RenderInventoryToken, StagedScopedInventory,
    },
    source_terrain::resident_tiles::{PreparedTiles, TileAdmission},
};
use forge_world::terrain_lod::CdlodView;
use std::marker::PhantomData;

#[derive(Clone, Copy, PartialEq, Eq)]
enum Mode {
    Disabled,
    Diagnostic,
    Runtime,
}

#[derive(Resource)]
struct Config {
    runtime: bool,
    terrain_diagnostic: bool,
    water_diagnostic: bool,
}
impl Default for Config {
    fn default() -> Self {
        Self {
            runtime: std::env::var_os("FORGE_DISABLE_SURFACE_PREFETCH").is_none(),
            terrain_diagnostic: cfg!(feature = "qualification")
                && std::env::var_os(Terrain::FLAG).is_some(),
            water_diagnostic: cfg!(feature = "qualification")
                && std::env::var_os(Water::FLAG).is_some(),
        }
    }
}
impl Config {
    fn mode(&self, diagnostic: bool) -> Mode {
        if diagnostic {
            Mode::Diagnostic
        } else if self.runtime {
            Mode::Runtime
        } else {
            Mode::Disabled
        }
    }
}

trait Surface: Send + Sync + 'static {
    type Material: Material;
    const NAME: &'static str;
    const FLAG: &'static str;
    fn installed(world: &World) -> bool;
    fn prepare_missing(
        world: &mut World,
        views: &[CdlodView],
        maximum_new: usize,
    ) -> Result<Option<PreparedTiles<Self::Material>>, String>;
    fn retain(
        world: &mut World,
        candidate: &PreparedTiles<Self::Material>,
        token: &RenderInventoryToken,
        admission: &TileAdmission,
    ) -> Result<usize, String>;
}

struct Terrain;
impl Surface for Terrain {
    type Material = forge_engine::source_terrain::lod::LodMaterial;
    const NAME: &'static str = "terrain";
    const FLAG: &'static str = "FORGE_QUALIFY_TERRAIN_PREFETCH";
    fn installed(world: &World) -> bool {
        world.contains_resource::<forge_engine::source_terrain::host::Status>()
    }
    fn prepare_missing(
        world: &mut World,
        views: &[CdlodView],
        maximum_new: usize,
    ) -> Result<Option<PreparedTiles<Self::Material>>, String> {
        forge_engine::source_terrain::host::prepare_missing_views(world, views, maximum_new)
    }
    fn retain(
        world: &mut World,
        candidate: &PreparedTiles<Self::Material>,
        token: &RenderInventoryToken,
        admission: &TileAdmission,
    ) -> Result<usize, String> {
        forge_engine::source_terrain::host::retain_prepared(world, candidate, token, admission)
    }
}

struct Water;
impl Surface for Water {
    type Material = forge_engine::source_water_optics::OpticalMaterial;
    const NAME: &'static str = "water";
    const FLAG: &'static str = "FORGE_QUALIFY_WATER_PREFETCH";
    fn installed(world: &World) -> bool {
        world.contains_resource::<forge_engine::source_water_host::Status>()
    }
    fn prepare_missing(
        world: &mut World,
        views: &[CdlodView],
        maximum_new: usize,
    ) -> Result<Option<PreparedTiles<Self::Material>>, String> {
        forge_engine::source_ocean_geometry::prepare_missing_views::<Self::Material>(
            world,
            views,
            maximum_new,
        )
    }
    fn retain(
        world: &mut World,
        candidate: &PreparedTiles<Self::Material>,
        token: &RenderInventoryToken,
        admission: &TileAdmission,
    ) -> Result<usize, String> {
        forge_engine::source_ocean_geometry::retain_prepared(world, candidate, token, admission)
    }
}

#[derive(Resource)]
struct Probe<S: Surface> {
    phase: Phase<S::Material>,
    started_frame: Option<u32>,
    attempted_frame: Option<u32>,
    marker: PhantomData<S>,
}
impl<S: Surface> Default for Probe<S> {
    fn default() -> Self {
        Self {
            phase: Phase::Start,
            started_frame: None,
            attempted_frame: None,
            marker: PhantomData,
        }
    }
}

struct Waiting<M: Material> {
    staged: StagedScopedInventory<M>,
    candidate: PreparedTiles<M>,
    visible_keys: Vec<String>,
    handoff: Option<Handoff>,
    new_meshes: usize,
}

#[derive(Clone, Copy)]
struct Handoff {
    owner: EntityId,
    revision: ScopeRevision,
    publication: Publication,
    next: ScopeRevision,
    requested: forge_project::VisualQualityTier,
    request_sequence: u64,
    applied_frame: u32,
}

enum Phase<M: Material> {
    Start,
    Waiting(Box<Waiting<M>>),
    LaterFence(
        Box<ActivatedScopedInventory<M>>,
        u32,
        Vec<String>,
        Option<Handoff>,
    ),
    Done,
}

pub(super) fn observe(world: &mut World, state: &mut State) {
    world.init_resource::<Config>();
    let config = world.resource::<Config>();
    let terrain = config.mode(config.terrain_diagnostic);
    let water = config.mode(config.water_diagnostic);
    if terrain == Mode::Disabled && water == Mode::Disabled {
        return;
    }
    // Only one candidate may own the renderer's spare scope at a time.
    if world
        .get_resource::<Probe<Terrain>>()
        .is_some_and(|probe| matches!(probe.phase, Phase::Waiting(_) | Phase::LaterFence(..)))
    {
        observe_surface::<Terrain>(world, state, terrain);
    } else if world
        .get_resource::<Probe<Water>>()
        .is_some_and(|probe| matches!(probe.phase, Phase::Waiting(_) | Phase::LaterFence(..)))
    {
        observe_surface::<Water>(world, state, water);
    } else if terrain == Mode::Diagnostic
        && world
            .get_resource::<Probe<Terrain>>()
            .is_none_or(|probe| !matches!(probe.phase, Phase::Done))
    {
        observe_surface::<Terrain>(world, state, terrain);
    } else if terrain == Mode::Runtime && water == Mode::Runtime {
        if world.resource::<FrameCount>().0.is_multiple_of(2) {
            observe_surface::<Terrain>(world, state, terrain);
        } else {
            observe_surface::<Water>(world, state, water);
        }
    } else {
        observe_surface::<Terrain>(world, state, terrain);
        observe_surface::<Water>(world, state, water);
    }
}

#[expect(
    clippy::too_many_lines,
    reason = "One staged surface transaction keeps observation, refusal and retirement in phase order."
)]
fn observe_surface<S: Surface>(world: &mut World, state: &mut State, mode: Mode) {
    if mode == Mode::Disabled {
        return;
    }
    world.init_resource::<Probe<S>>();
    world.resource_scope(|world, mut probe: Mut<Probe<S>>| {
        let phase = std::mem::replace(&mut probe.phase, Phase::Done);
        let frame = world.resource::<FrameCount>().0;
        if matches!(&phase, Phase::Waiting(_) | Phase::LaterFence(..))
            && probe.started_frame.is_some_and(|started| {
                frame.checked_sub(started).is_none_or(|age| age >= 120)
            })
        {
            let cancellation = match phase {
                Phase::Waiting(mut waiting) => waiting.staged.cancel(world).err(),
                Phase::LaterFence(active, ..) => active.cancel(world).err(),
                _ => None,
            };
            record::<S>("failed", || serde_json::json!({"error": "Probe timed out in processed frames", "frame": frame, "cancellation": cancellation}));
            probe.started_frame = None;
            if mode == Mode::Runtime { probe.phase = Phase::Start; }
            return;
        }
        let mut started = None;
        let next = match phase {
            Phase::Start if mode == Mode::Runtime && probe.attempted_frame.is_some_and(|previous| {
                frame.checked_sub(previous).is_some_and(|age| age < 30)
            }) => Phase::Start,
            Phase::Start => {
                probe.attempted_frame = Some(frame);
                match begin::<S>(world, state, mode) {
                    Ok(Some(waiting)) => { started = Some(frame); Phase::Waiting(Box::new(waiting)) },
                    Ok(None) => Phase::Start,
                    Err(error) => { record::<S>("failed", || serde_json::json!({"error": error})); Phase::Done }
                }
            },
            Phase::Waiting(mut waiting) => {
                record::<S>("waiting", || serde_json::json!({
                    "frame": world.resource::<FrameCount>().0,
                    "current_live": scope_token(world, state, S::NAME).is_some(),
                    "graphics_ready": state.ready,
                    "simulation_ready": world.get_resource::<forge_engine::PlayerSimulationReady>().map(|ready| ready.0),
                }));
                if !waiting.staged.requests_are_current(world)
                    || waiting.handoff.is_some_and(|handoff| !same_request::<S>(state, handoff))
                {
                    let cancellation = waiting.staged.cancel(world).err();
                    record::<S>("failed", || serde_json::json!({"error": "Candidate request was replaced", "cancellation": cancellation}));
                    Phase::Done
                } else if !state.ready || scope_token(world, state, S::NAME).is_none() {
                    Phase::Waiting(waiting)
                } else { match waiting.staged.try_activate(world) {
                    Ok(Some(active)) => match retain::<S>(world, state, &waiting, &active, mode) {
                        Ok(added) => {
                            let frame = world.resource::<FrameCount>().0;
                            record::<S>("retained", || serde_json::json!({
                                "frame": frame, "registration_frame": active.registration_frame,
                                "candidate_meshes": waiting.candidate.meshes().len(),
                                "staged_meshes": active.meshes.len(), "added_to_cache": added,
                                "current_live": scope_token(world, state, S::NAME).is_some(),
                                "inventory_source_keys_unchanged": mode == Mode::Diagnostic, "scene_published_by_probe": false,
                                "completed": active.token.completed_frame(world),
                            }));
                            Phase::LaterFence(Box::new(active), frame, waiting.visible_keys.clone(), waiting.handoff)
                        }
                        Err(error) => {
                            let cancellation = active.cancel(world).err();
                            record::<S>("failed", || serde_json::json!({"error": error, "cancellation": cancellation})); Phase::Done
                        }
                    },
                    Ok(None) => Phase::Waiting(waiting),
                    Err(error) => {
                        let cancellation = waiting.staged.cancel(world).err();
                        record::<S>("failed", || serde_json::json!({"error": error, "cancellation": cancellation}));
                        Phase::Done
                    }
                }}
            }
            Phase::LaterFence(active, marker, keys, handoff) => {
                match handoff_frame::<S>(world, state, &active, marker, &keys, handoff, mode) {
                    Ok(Some(marker)) => {
                    record::<S>("later_fence", || serde_json::json!({
                        "frame": world.resource::<FrameCount>().0, "marker": marker,
                        "completed": active.token.completed_frame(world),
                        "current_live": true, "scene_published_by_probe": false,
                    }));
                    if let Some(handoff) = handoff {
                        let new_owner = active.owner;
                        match adopt::<S>(world, state, &active, marker, &keys, handoff, mode) {
                            Ok(()) => record::<S>("adopted", || serde_json::json!({"frame": world.resource::<FrameCount>().0, "marker": marker, "inventory_source_keys_unchanged": mode == Mode::Diagnostic, "scene_published_by_probe": false})),
                            Err(error) => {
                                let cancellation = active.cancel(world).err();
                                record::<S>("failed", || serde_json::json!({"error": error, "owner": new_owner.to_string(), "cancellation": cancellation}));
                            }
                        }
                    } else if let Err(error) = active.cancel(world) {
                        record::<S>("failed", || serde_json::json!({"error": error}));
                    }
                    Phase::Done
                    }
                    Ok(None) => Phase::LaterFence(active, marker, keys, handoff),
                    Err(error) => {
                        let cancellation = active.cancel(world).err();
                        record::<S>("failed", || serde_json::json!({"error": error, "cancellation": cancellation}));
                        Phase::Done
                    }
                }
            }
            Phase::Done if mode == Mode::Runtime => Phase::Start,
            Phase::Done => Phase::Done,
        };
        probe.phase = next;
        if let Some(frame) = started { probe.started_frame = Some(frame); }
        if matches!(&probe.phase, Phase::Done) { probe.started_frame = None; }
    });
}

#[allow(
    clippy::too_many_lines,
    reason = "Qualification records the complete staged preparation"
)]
fn begin<S: Surface>(
    world: &mut World,
    state: &mut State,
    mode: Mode,
) -> Result<Option<Waiting<S::Material>>, String> {
    if !state.ready
        || !S::installed(world)
        || (mode == Mode::Diagnostic
            && world
                .get_resource::<forge_engine::gameplay::GameplayRuntime>()
                .is_none_or(|game| game.session().state().tick < 120))
        || scope_token(world, state, S::NAME).is_none()
    {
        return Ok(None);
    }
    let Some(current) = family::<S::Material>(world)? else {
        return Ok(None);
    };
    if current.meshes.is_empty() || current.materials.is_empty() {
        return Ok(None);
    }
    let (camera, _) = forge_engine::scene_camera::primary(world)?;
    let origin = *world
        .get::<Transform>(camera)
        .ok_or("Surface camera pose absent")?;
    let Some(Projection::Perspective(projection)) = world.get::<Projection>(camera) else {
        return Err("Surface camera perspective absent".into());
    };
    let offsets: &[f32] = if std::env::var_os("FORGE_QUALIFY_SURFACE_MULTIVIEW").is_some() {
        &[2.0, 4.0, 6.0, 8.0, 10.0, 12.0, 14.0, 16.0]
    } else {
        &[16.0]
    };
    let views = if mode == Mode::Diagnostic {
        offsets
            .iter()
            .map(|offset| {
                let mut future = origin;
                future.translation.x += offset;
                forge_engine::scene_camera::source_view(&future, projection)
            })
            .collect::<Result<Vec<_>, _>>()?
    } else {
        if world
            .get_resource::<forge_engine::PlayerSimulationReady>()
            .is_none_or(|ready| !ready.0)
        {
            return Ok(None);
        }
        let Some(player) = world.get_resource::<forge_engine::PlayerRuntimeSnapshot>() else {
            return Ok(None);
        };
        forge_engine::scene_camera::future_source_views(
            &origin,
            projection,
            player.horizontal_velocity,
            &[8.0, 16.0],
        )?
    };
    if views.is_empty() {
        return Ok(None);
    }
    // Keep each speculative preparation small; the visible inventory and the
    // resident cache keep their own existing limits.
    let available = render_inventory::MAX_SCOPED_MESHES.saturating_sub(current.meshes.len());
    let maximum_new = available.min(16);
    if maximum_new == 0 {
        return Ok(None);
    }
    let Some(candidate) = S::prepare_missing(world, &views, maximum_new)? else {
        return Ok(None);
    };
    if !current.materials.contains(candidate.material()) {
        return Err("Prepared material differs from the visible surface".into());
    }
    let live_meshes = current.meshes;
    let mut meshes = live_meshes.clone();
    let before = meshes.len();
    for mesh in candidate.meshes() {
        if !meshes.contains(mesh) {
            meshes.push(mesh.clone());
        }
    }
    if meshes.len() == before || meshes.len() > render_inventory::MAX_SCOPED_MESHES {
        return Err("Future surface has no new meshes or exceeds the inventory budget".into());
    }
    let handoff =
        if mode == Mode::Runtime || std::env::var_os("FORGE_QUALIFY_SURFACE_ADOPT").is_some() {
            let Some(revision) = state.scope_revisions.get(S::NAME).copied() else {
                return Ok(None);
            };
            let Some(publication) = state.publications.get(S::NAME).copied() else {
                return Ok(None);
            };
            Some(Handoff {
                owner: state.scope_owner(S::NAME),
                revision,
                publication,
                next: ScopeRevision {
                    generation: state
                        .generation
                        .checked_add(1)
                        .ok_or("Graphics generation exhausted")?,
                    required_frame: world.resource::<FrameCount>().0,
                },
                requested: state.requested,
                request_sequence: state.request_sequence,
                applied_frame: state.applied_frame,
            })
        } else {
            None
        };
    let identity = if let Some(h) = handoff {
        identity(state, h.next)?
    } else {
        canonical_json_sha256(&(
            state.source,
            state.terrain,
            S::NAME,
            "surface-prefetch-probe",
        ))
        .map_err(|error| error.to_string())?
    };
    let staged_owner = if let Some(h) = handoff {
        if h.owner == owner(S::NAME) {
            prepared_owner(S::NAME)
        } else if h.owner == prepared_owner(S::NAME) {
            owner(S::NAME)
        } else {
            return Err("Unexpected active surface owner".into());
        }
    } else {
        EntityId::deterministic(format!("qualification/staged-{}-tiles", S::NAME))
    };
    let staged = StagedScopedInventory::begin_with_live_sources(
        world,
        state.scope_owner(S::NAME),
        staged_owner,
        identity,
        handoff.map_or(1, |h| h.next.generation),
        &live_meshes,
        &meshes,
        &current.materials,
    )?;
    if let Some(h) = handoff {
        state.generation = h.next.generation;
    }
    record::<S>("registered", || {
        serde_json::json!({
            "frame": world.resource::<FrameCount>().0, "current_meshes": before,
            "candidate_meshes": candidate.meshes().len(), "staged_meshes": meshes.len(),
            "new_meshes": meshes.len() - before,
            "hypothetical_camera_x_offsets_m": (mode == Mode::Diagnostic).then_some(offsets),
            "prediction": if mode == Mode::Runtime { "player_velocity" } else { "diagnostic_positive_x" },
            "prediction_distances_m": if mode == Mode::Runtime { &[8.0_f32, 16.0][..] } else { offsets },
        })
    });
    Ok(Some(Waiting {
        staged,
        candidate,
        visible_keys: current.keys,
        handoff,
        new_meshes: meshes.len() - before,
    }))
}

fn retain<S: Surface>(
    world: &mut World,
    state: &State,
    waiting: &Waiting<S::Material>,
    active: &ActivatedScopedInventory<S::Material>,
    mode: Mode,
) -> Result<usize, String> {
    let current = family::<S::Material>(world)?.ok_or("Visible surface sources are unavailable")?;
    if !active.previous_request_is_current(world, state.scope_owner(S::NAME))
        || scope_token(world, state, S::NAME).is_none()
        || waiting
            .handoff
            .is_some_and(|handoff| !same_request::<S>(state, handoff))
        || (mode == Mode::Diagnostic && current.keys != waiting.visible_keys)
        || !active
            .token
            .covers_subset(world, &current.meshes, &current.materials)
    {
        return Err("Visible surface changed during candidate preparation".into());
    }
    let admission = TileAdmission {
        owner: active.owner,
        identity: active.identity,
        generation: active.generation,
        registration_frame: active.registration_frame,
    };
    let added = S::retain(world, &waiting.candidate, &active.token, &admission)?;
    if added == 0
        || added > waiting.new_meshes
        || family::<S::Material>(world)?.is_none_or(|family| family.keys != current.keys)
    {
        return Err("Retained candidate changed the visible surface or added no mesh".into());
    }
    Ok(added)
}

fn same_request<S: Surface>(state: &State, handoff: Handoff) -> bool {
    state.error.is_none()
        && state.requested == handoff.requested
        && state.dispatched == Some(handoff.requested)
        && state.applied == Some(handoff.requested)
        && state.request_sequence == handoff.request_sequence
        && state.applied_frame == handoff.applied_frame
        && state.scope_owner(S::NAME) == handoff.owner
        && state.scope_revisions.get(S::NAME) == Some(&handoff.revision)
}

fn handoff_frame<S: Surface>(
    world: &mut World,
    state: &State,
    active: &ActivatedScopedInventory<S::Material>,
    marker: u32,
    initial_keys: &[String],
    handoff: Option<Handoff>,
    mode: Mode,
) -> Result<Option<u32>, String> {
    if !active.request_is_current(world)
        || !active.previous_request_is_current(world, state.scope_owner(S::NAME))
        || handoff.is_some_and(|handoff| !same_request::<S>(state, handoff))
    {
        return Err("Candidate or previous request was replaced".into());
    }
    if !state.ready || scope_token(world, state, S::NAME).is_none() {
        return Ok(None);
    }
    let Some(current) = family::<S::Material>(world)? else {
        return Ok(None);
    };
    let publication = *state
        .publications
        .get(S::NAME)
        .ok_or("Surface publication absent")?;
    if (mode == Mode::Diagnostic
        && (current.keys != initial_keys
            || handoff.is_some_and(|handoff| handoff.publication != publication)))
        || state
            .inventory
            .as_ref()
            .is_none_or(|signature| signature_family(signature, S::NAME) != current.keys.as_slice())
        || !render_inventory::scoped_contains_sources(
            world,
            active.owner,
            &current.meshes,
            &current.materials,
        )
    {
        return Err("Candidate no longer covers the live surface".into());
    }
    let frame = marker.max(publication.frame);
    Ok(active.token.covers_frame(world, frame).then_some(frame))
}

fn adopt<S: Surface>(
    world: &mut World,
    state: &mut State,
    active: &ActivatedScopedInventory<S::Material>,
    marker: u32,
    keys: &[String],
    handoff: Handoff,
    mode: Mode,
) -> Result<(), String> {
    let expected_owner = if handoff.owner == owner(S::NAME) {
        prepared_owner(S::NAME)
    } else {
        owner(S::NAME)
    };
    let previous_publication = *state
        .publications
        .get(S::NAME)
        .ok_or("Surface publication absent")?;
    let current = family::<S::Material>(world)?.ok_or("Visible surface sources are unavailable")?;
    if !state.ready
        || !same_request::<S>(state, handoff)
        || scope_token(world, state, S::NAME).is_none()
        || (mode == Mode::Diagnostic
            && (current.keys != keys || previous_publication != handoff.publication))
        || state
            .inventory
            .as_ref()
            .is_none_or(|signature| signature_family(signature, S::NAME) != current.keys.as_slice())
        || !active.previous_request_is_current(world, handoff.owner)
        || active.owner != expected_owner
        || active.identity != identity(state, handoff.next)?
        || active.generation != handoff.next.generation
        || active.registration_frame != handoff.next.required_frame
        || marker < active.registration_frame
        || marker < previous_publication.frame
        || !active.token.covers_frame(world, marker)
        || canonical_json_sha256(&current.keys).map_err(|error| error.to_string())?
            != previous_publication.sources
    {
        return Err("Prepared surface no longer matches the active scene".into());
    }
    if !active
        .token
        .covers_subset(world, &current.meshes, &current.materials)
    {
        return Err("Prepared inventory does not cover the current surface sources".into());
    }
    let publication = Publication {
        frame: marker,
        sources: previous_publication.sources,
        seal: canonical_json_sha256(&(active.identity, marker, previous_publication.sources))
            .map_err(|error| error.to_string())?,
    };
    let previous_owner = state.scope_owners.insert(S::NAME, active.owner);
    state.scope_revisions.insert(S::NAME, handoff.next);
    state.publications.insert(S::NAME, publication);
    let retirement = if scope_token(world, state, S::NAME).is_some() {
        active.release_previous(world, handoff.owner)
    } else {
        Err("Prepared inventory lost its fence during handoff".into())
    };
    if let Err(error) = retirement {
        match previous_owner {
            Some(owner) => {
                state.scope_owners.insert(S::NAME, owner);
            }
            None => {
                state.scope_owners.remove(S::NAME);
            }
        }
        state.scope_revisions.insert(S::NAME, handoff.revision);
        state.publications.insert(S::NAME, previous_publication);
        return Err(error);
    }
    let leases = active
        .meshes
        .iter()
        .cloned()
        .map(Handle::untyped)
        .chain(active.materials.iter().cloned().map(Handle::untyped))
        .collect();
    state.scope_leases.insert(S::NAME, leases);
    Ok(())
}

fn record<S: Surface>(status: &str, detail: impl FnOnce() -> serde_json::Value) {
    let diagnostic = cfg!(feature = "qualification") && std::env::var_os(S::FLAG).is_some();
    let trace = status != "waiting" && std::env::var_os("FORGE_SURFACE_PREFETCH_TRACE").is_some();
    if !diagnostic && !trace {
        return;
    }
    eprintln!(
        "QUALIFY_SURFACE_PREFETCH {}",
        serde_json::json!({"surface": S::NAME, "status": status, "detail": detail()})
    );
}
