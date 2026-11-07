//! Derived static-object hierarchy sharing the admitted terrain partition.
//! Sources, identities and physics stay live. Originals remain rendered until
//! the complete replacement has a fresh renderer allocation/queue observation.
use bevy::{
    asset::{AssetId, UntypedAssetId, UntypedHandle},
    camera::visibility::RenderLayers,
    prelude::*,
    tasks::Task,
};
use forge_core::{ContentHash, EntityId};
use forge_world::terrain_hlod::TerrainHlodKey;
use serde::Serialize;
use std::{
    collections::{BTreeMap, BTreeSet},
    sync::Arc,
};

mod collect;
mod gpu;
mod lifecycle;
mod material;
mod selection;
pub use gpu::StaticHlodGpuEvidence;
pub use material::StaticHlodMaterial;
#[cfg(test)]
mod tests;

const MAX_PRIMITIVES: usize = 4096;
const MAX_SOURCE_BYTES: u64 = 64 * 1024 * 1024;
const MAX_DERIVED_BYTES: u64 = 32 * 1024 * 1024;

/// Renderer control, independent of authored content. Disabling the hierarchy
/// restores detailed originals and invalidates any pending replacement.
#[derive(Resource, Clone, Copy, Debug, PartialEq, Eq)]
pub struct StaticHlodSettings {
    pub enabled: bool,
}

impl Default for StaticHlodSettings {
    fn default() -> Self {
        Self { enabled: true }
    }
}

/// A host attaches this to every primitive only after admitting the complete,
/// immutable static object. The pin identifies its actual accepted source.
/// Animated, displaced or locally shaded primitives require a retained-detail
/// material route until their own pose-baking adapter has been reviewed.
#[derive(Component, Clone, Copy, Debug, PartialEq, Eq)]
pub struct StaticHlodSource {
    pub object: EntityId,
    pub source: ContentHash,
}

/// Owners of live movement retain the whole authored object in its detailed
/// representation. The source and collision entities are never replaced.
#[derive(Resource, Default)]
struct MovingObjects(BTreeMap<EntityId, BTreeSet<&'static str>>);

/// Register a movement owner before publishing its first pose. If a proxy was
/// already active, restore its originals immediately, before the next render.
pub fn retain_moving_object(
    world: &mut World,
    object: EntityId,
    owner: &'static str,
) -> Result<(), String> {
    if object.as_uuid().is_nil() || owner.is_empty() {
        return Err("Moving HLOD object or owner is invalid".into());
    }
    world.init_resource::<MovingObjects>();
    let first = {
        let mut moving = world.resource_mut::<MovingObjects>();
        let owners = moving.0.entry(object).or_default();
        if !owners.insert(owner) {
            return Err("Moving HLOD owner is already registered".into());
        }
        owners.len() == 1
    };
    if first && world.contains_resource::<State>() {
        world.resource_scope(|world, mut state: Mut<State>| {
            if state.request.as_ref().is_some_and(|request| {
                request
                    .sources
                    .iter()
                    .any(|source| source.identity.object == object)
            }) {
                lifecycle::retire(world, &mut state);
                state.request = None;
            }
        });
    }
    Ok(())
}

/// Release only this owner. A different live animation keeps the object detailed.
pub fn release_moving_object(world: &mut World, object: EntityId, owner: &'static str) {
    let Some(mut moving) = world.get_resource_mut::<MovingObjects>() else {
        return;
    };
    if let Some(owners) = moving.0.get_mut(&object) {
        owners.remove(owner);
        if owners.is_empty() {
            moving.0.remove(&object);
        }
    }
}

#[must_use]
pub fn moving_object_retained(world: &World, object: EntityId) -> bool {
    world
        .get_resource::<MovingObjects>()
        .is_some_and(|moving| moving.0.contains_key(&object))
}

pub(crate) fn moving_object_owned(world: &World, object: EntityId, owner: &'static str) -> bool {
    world
        .get_resource::<MovingObjects>()
        .and_then(|moving| moving.0.get(&object))
        .is_some_and(|owners| owners.contains(owner))
}

#[derive(Component, Clone, Copy, Debug)]
pub struct StaticHlodBatch {
    pub generation: u64,
    pub node: TerrainHlodKey,
}

/// Buffer counters exclude allocator overhead, textures, physics and GPU memory.
/// Retaining originals for fallback is explicitly not a memory-unloading claim.
#[derive(Resource, Clone, Debug, Default, Serialize)]
pub struct StaticHlodStats {
    pub enabled: bool,
    pub generation: u64,
    pub source_objects: usize,
    pub source_primitives: usize,
    pub source_triangles: usize,
    pub selected_triangles: usize,
    pub desired_batches: usize,
    pub rendered_batches: usize,
    pub replaced_primitives: usize,
    pub retained_source_mesh_bytes: u64,
    pub source_snapshot_bytes: u64,
    pub derived_mesh_bytes: u64,
    pub maximum_position_error_m: f64,
    pub maximum_attribute_deltas: BTreeMap<String, f64>,
    pub minimum_face_alignment: Option<f64>,
    /// Conservative sum of retained sources, snapshots, live outputs and the
    /// pending job's input/output envelope. Not measured process working set.
    pub peak_tracked_buffer_bound_bytes: u64,
    pub committed_transitions: u64,
    pub discarded_jobs: u64,
    pub pending: bool,
    pub last_preparation_ms: Option<f64>,
    pub last_error: Option<String>,
}

/// Pose baking is opt-in per material family. A shader using per-object local
/// coordinates, instance tags, animation or deformation needs another route.
pub struct StaticHlodPlugin {
    materials: Vec<StaticHlodMaterial>,
}

impl Default for StaticHlodPlugin {
    fn default() -> Self {
        Self {
            materials: vec![StaticHlodMaterial::pose_baked::<StandardMaterial>()],
        }
    }
}

impl StaticHlodPlugin {
    #[must_use]
    pub fn new(materials: Vec<StaticHlodMaterial>) -> Self {
        Self { materials }
    }
}

impl Plugin for StaticHlodPlugin {
    fn build(&self, app: &mut App) {
        app.insert_resource(Routes(self.materials.clone()))
            .init_resource::<State>()
            .init_resource::<StaticHlodSettings>()
            .init_resource::<StaticHlodStats>()
            .add_systems(
                Update,
                lifecycle::update.after(crate::cell_streaming::CellStreamingSet::Update),
            );
        gpu::install(app);
    }
}

#[derive(Resource)]
struct Routes(Vec<StaticHlodMaterial>);

struct CachedMesh {
    mesh: Arc<Mesh>,
    minimum: Vec3,
    maximum: Vec3,
    bytes: u64,
    triangles: usize,
    layout: String,
}

#[derive(Clone, Debug, PartialEq)]
struct Source {
    entity: Entity,
    identity: StaticHlodSource,
    mesh: AssetId<Mesh>,
    material: UntypedHandle,
    images: Vec<AssetId<Image>>,
    route: usize,
    can_bake: bool,
    pose: Transform,
    retained_motion: bool,
    visibility: Visibility,
    layers: RenderLayers,
    flags: u8,
    custom: u64,
    node: Option<TerrainHlodKey>,
}

#[derive(Clone, Debug, PartialEq)]
struct Request {
    enabled: bool,
    terrain: ContentHash,
    sources: Vec<Source>,
    groups: Vec<Group>,
}

#[derive(Clone, Debug, PartialEq)]
struct Group {
    node: TerrainHlodKey,
    origin: Vec3,
    material: UntypedAssetId,
    layout: String,
    layers: RenderLayers,
    flags: u8,
    custom: u64,
    route: usize,
    members: Vec<usize>,
}

struct InputGroup {
    group: Group,
    parts: Vec<(Arc<Mesh>, Transform)>,
}

struct PreparedGroup {
    group: Group,
    geometry: crate::static_hlod_geometry::PreparedHlodGeometry,
}

struct Job {
    request: Request,
    generation: u64,
    input_bytes: u64,
    task: Task<(Result<Vec<PreparedGroup>, String>, f64)>,
}

struct Live {
    entity: Entity,
    mesh: Handle<Mesh>,
    group: Group,
    bytes: u64,
    triangles: usize,
    statistics: crate::static_hlod_geometry::HlodGeometryStats,
}

/// Current proxy allocations, view resources, stable pipelines and queue work
/// have been observed. This is not a pixel-fidelity or performance verdict.
#[must_use]
pub fn gpu_upload_ready(world: &World) -> bool {
    world
        .get_resource::<State>()
        .is_some_and(|state| !state.live.is_empty() && gpu::ready(world, state.generation))
}

/// The source shadow atlas retains the authored casters across a visual swap.
/// Its textures are also inputs to the proxy materials, so rebuilding the atlas
/// when a proxy becomes visible would invalidate that proxy's admission.
pub(crate) fn source_caster_visibility(world: &World, entity: Entity) -> Option<bool> {
    let state = world.get_resource::<State>()?;
    if world
        .get::<StaticHlodBatch>(entity)
        .is_some_and(|batch| batch.generation == state.generation)
        && state.live.iter().any(|live| live.entity == entity)
    {
        return Some(false);
    }
    (state.active
        && state.hidden.contains_key(&entity)
        && world.get::<Visibility>(entity) == Some(&Visibility::Hidden))
    .then(|| collect::is_visible(world, entity, &state.hidden))
}

/// Qualification/capture barrier. Gameplay may continue rendering originals
/// during preparation; a completed-selection observation must wait for it.
#[must_use]
pub fn selection_is_ready(world: &World) -> bool {
    let Some(state) = world.get_resource::<State>() else {
        return true;
    };
    let Some(metrics) = world.get_resource::<StaticHlodStats>() else {
        return false;
    };
    state.request.is_some()
        && !metrics.pending
        && metrics.last_error.is_none()
        && metrics.rendered_batches == metrics.desired_batches
        && covered_sources(world).len() == metrics.source_primitives
}

#[derive(Resource, Default)]
struct State {
    generation: u64,
    cache: BTreeMap<AssetId<Mesh>, CachedMesh>,
    request: Option<Request>,
    pending: Option<Job>,
    live: Vec<Live>,
    hidden: BTreeMap<Entity, Visibility>,
    active: bool,
    attempted: bool,
    mesh_events: bevy::ecs::message::MessageCursor<AssetEvent<Mesh>>,
    modified_meshes: std::collections::BTreeSet<AssetId<Mesh>>,
}

/// Fresh coverage of an admitted primitive. A hidden original is covered only
/// by this adapter's intact, current, GPU-acknowledged complete transaction.
#[must_use]
pub fn source_is_covered(world: &World, entity: Entity) -> bool {
    covered_sources(world).contains(&entity)
}

/// Inspect the complete replacement once for a host's inventory check. Calling
/// this once avoids repeating a global transaction check for every primitive.
#[must_use]
pub fn covered_sources(world: &World) -> std::collections::BTreeSet<Entity> {
    let state = world.get_resource::<State>();
    let replacement_ready = state.is_some_and(|state| {
        state.active && lifecycle::intact(world, state) && gpu::ready(world, state.generation)
    });
    let empty = BTreeMap::new();
    let hidden = state.map_or(&empty, |state| &state.hidden);
    // Procedural modules can render originals without a hierarchy host.
    // Validate their real standard material without inventing proxy readiness.
    let ordinary = [StaticHlodMaterial::retained_detail::<StandardMaterial>()];
    let routes = world
        .get_resource::<Routes>()
        .map_or(ordinary.as_slice(), |routes| routes.0.as_slice());
    let Some(mut query) = world.try_query_filtered::<Entity, With<StaticHlodSource>>() else {
        return std::collections::BTreeSet::new();
    };
    query
        .iter(world)
        .filter(|&entity| {
            let mut materials = routes
                .iter()
                .filter_map(|route| (route.inspect)(world, entity));
            if !matches!(materials.next(), Some(Ok(_))) || materials.next().is_some() {
                return false;
            }
            world.get::<Mesh3d>(entity).is_some_and(|mesh| {
                world
                    .get_resource::<Assets<Mesh>>()
                    .is_some_and(|assets| assets.get(mesh.id()).is_some())
            }) && if hidden.contains_key(&entity) {
                replacement_ready
            } else {
                collect::is_visible(world, entity, hidden)
            }
        })
        .collect()
}
