//! Opt-in terrain residency. Preparation is asynchronous; admission is exclusive.
//!
//! CPU mesh byte budgets exclude the retained raster, GPU allocations and Rapier's internal
//! acceleration structures. Those require process/GPU measurements, not estimated PASS claims.
use bevy::{
    asset::RenderAssetUsages,
    ecs::system::SystemParam,
    mesh::Indices,
    prelude::*,
    render::render_resource::PrimitiveTopology,
    tasks::{AsyncComputeTaskPool, Task, futures::check_ready},
};
use bevy_rapier3d::prelude::Collider;
use forge_core::{AreaId, ContentHash, EntityId};
use forge_world::{
    TerrainGenerationPlan,
    cell_streaming::{
        CellDetail, CellKey, CellStreamPlan, CellStreamer, CellStreamingPolicy, LoadedCell,
        ProceduralCellMesh, ProceduralCellSource, SampledCellMesh, SampledCellSource,
        SampledMeshQuality,
    },
    sampled_terrain::SampledTerrainPlan,
};
use std::{
    collections::BTreeMap,
    sync::{Arc, Mutex},
};

mod hlod;
mod player;
mod source_updates;
pub use hlod::{StreamedTerrainHlod, TerrainHlodStats};
pub use player::{
    PlayerCellStreamingPlugin, PlayerCellStreamingSet, PlayerCellStreamingStatus,
    TemporarySimulationHold, hold_player_simulation,
};
pub use source_updates::{
    ResidentSampledTerrain, admitted_mesh_quality, request_mesh_quality, request_source_update,
};

/// Read-only partition currently admitted by the terrain controller. Pending
/// requests are deliberately excluded; other render adapters share this grid.
pub struct SampledRenderSelection<'a> {
    pub source: ContentHash,
    pub policy: &'a CellStreamingPolicy,
    pub cells: &'a BTreeMap<CellKey, LoadedCell>,
    pub distant: Option<&'a forge_world::terrain_hlod::TerrainHlodLayout>,
}

#[must_use]
pub fn render_selection(world: &World) -> Option<SampledRenderSelection<'_>> {
    let state = world.get_resource::<State>()?;
    (!state.live.is_empty()).then_some(SampledRenderSelection {
        source: state.source.document_hash(),
        policy: &state.policy,
        cells: state.stream.resident(),
        distant: state.hlod.as_ref().and_then(hlod::State::layout),
    })
}

/// Read the admitted mesh identity from its controller, including procedural
/// residency identities. Pending source replacements keep the previous identity.
#[derive(SystemParam)]
pub struct TerrainRenderSource<'w> {
    state: Option<Res<'w, State>>,
}

impl TerrainRenderSource<'_> {
    #[must_use]
    pub fn identity(&self) -> Option<ContentHash> {
        self.state
            .as_ref()
            .map(|state| state.source.residency_source())
    }
}

#[derive(SystemSet, Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum CellStreamingSet {
    Update,
}

/// Shared initial grid and residency budget of the module game and authoring view.
pub fn sampled_policy(
    source: &forge_project::sampled_terrain::SampledTerrainSource,
) -> Result<CellStreamingPolicy, String> {
    source.validate().map_err(|error| error.to_string())?;
    if !source.width.is_multiple_of(32) || !source.height.is_multiple_of(32) {
        return Err("Le terrain doit être divisible en cellules de 32 échantillons.".into());
    }
    let policy = CellStreamingPolicy {
        origin_xz_m: source.origin_xz_m,
        cell_size_m: source.sample_spacing_m * 32.0,
        dimensions: [
            u16::try_from(source.width / 32).map_err(|e| e.to_string())?,
            u16::try_from(source.height / 32).map_err(|e| e.to_string())?,
        ],
        detail_radius: 1,
        far_radius: 6,
        max_resident_cells: 289,
        full_cell_bytes: 100_000,
        hlod_cell_bytes: 100_000,
        max_resident_bytes: 28_900_000,
        max_staging_bytes: 57_800_000,
    };
    policy.validate().map_err(|error| error.to_string())?;
    Ok(policy)
}

/// Checks live owned cells and their synchronized near colliders; GPU readiness is separate.
#[must_use]
pub fn source_is_synchronized(world: &World, expected: ContentHash) -> bool {
    let Some(state) = world.get_resource::<State>() else {
        return false;
    };
    let Some(metrics) = world.get_resource::<CellStreamingStats>() else {
        return false;
    };
    let source = state.source.residency_source();
    state.source.document_hash() == expected
        && state.requested_source.is_none()
        && !metrics.pending
        && metrics.last_error.is_none()
        && !state.live.is_empty()
        && hlod::is_synchronized(world, state)
        && state.live.iter().all(|(key, live)| {
            live.is_intact(world)
                && world
                    .get::<StreamedTerrainCell>(live.entity)
                    .is_some_and(|marker| {
                        marker.source == source
                            && marker.key == *key
                            && marker.logical_id == live.logical_id
                            && state
                                .stream
                                .resident()
                                .get(key)
                                .is_some_and(|cell| cell.detail == marker.detail)
                    })
                && live
                    .collider_entity
                    .is_none_or(|entity| crate::physics::static_collider_ready(world, entity))
        })
}

#[cfg(test)]
pub(crate) mod tests;

#[derive(Resource, Clone, Copy, Debug)]
pub struct CellStreamingObserver {
    pub position_xz_m: [f64; 2],
    /// Increment only to explicitly retry the same failed cell; ordinary motion needs no increment.
    pub retry_sequence: u64,
}

/// Whether the current failed request can be retried without changing its source.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum TerrainRetryAvailability {
    NoFailure,
    Retryable,
    Waiting,
    Unavailable,
}

/// Transient presentation state; the stream remains the only retry authority.
#[allow(
    clippy::struct_excessive_bools,
    reason = "Presentation flags and the queued-action latch are consumed independently"
)]
#[derive(Resource, Clone, Copy, Debug, Default)]
pub struct TerrainRetryUiStatus {
    pub failed: bool,
    pub retryable: bool,
    pub waiting: bool,
    pub request_queued: bool,
}

/// Inspect the live request and its failure under the same world authority.
/// A failed synchronous plan is not treated as an admitted failed job.
#[must_use]
pub fn terrain_retry_availability(world: &World) -> TerrainRetryAvailability {
    let Some(metrics) = world.get_resource::<CellStreamingStats>() else {
        return TerrainRetryAvailability::NoFailure;
    };
    if metrics.last_error.is_none() {
        return TerrainRetryAvailability::NoFailure;
    }
    let Some(state) = world.get_resource::<State>() else {
        return TerrainRetryAvailability::Unavailable;
    };
    if state.pending.is_some() {
        return TerrainRetryAvailability::Waiting;
    }
    let Some(observer) = world.get_resource::<CellStreamingObserver>() else {
        return TerrainRetryAvailability::Unavailable;
    };
    if world.get_resource::<CellStreamingProtection>().is_none() {
        return TerrainRetryAvailability::Unavailable;
    }
    let Ok(current) = requested(world, state) else {
        return TerrainRetryAvailability::Unavailable;
    };
    if state.last_request.as_ref() == Some(&current) && observer.retry_sequence < u64::MAX {
        TerrainRetryAvailability::Retryable
    } else {
        TerrainRetryAvailability::Unavailable
    }
}

/// Retry only the current failed request. Never clear its failure or alter the
/// requested source, protected cells, player, gameplay or saved preferences.
/// A repeated click before the resulting request completes returns false.
pub fn retry_failed_terrain(world: &mut World) -> bool {
    if terrain_retry_availability(world) != TerrainRetryAvailability::Retryable {
        return false;
    }
    let mut observer = world.resource_mut::<CellStreamingObserver>();
    let Some(next) = observer.retry_sequence.checked_add(1) else {
        return false;
    };
    observer.retry_sequence = next;
    if let Some(mut status) = world.get_resource_mut::<TerrainRetryUiStatus>() {
        status.retryable = false;
        status.waiting = true;
        status.request_queued = true;
    }
    true
}

/// Caller-owned protected simulation cells, including full-detail neighbours.
/// Empty for a view-only diagnostic; a playable host must publish its active body coverage.
#[derive(Resource, Clone, Debug, Default)]
pub struct CellStreamingProtection {
    pub cells: Vec<CellKey>,
}

#[derive(Clone, Debug, PartialEq, Eq)]
struct Request {
    source: ContentHash,
    mesh_quality: SampledMeshQuality,
    observer: CellKey,
    protected: Vec<CellKey>,
    retry_sequence: u64,
}

#[derive(Resource, Clone, Debug, Default)]
pub struct CellStreamingStats {
    pub resident_cells: usize,
    pub detailed_cells: usize,
    pub coarse_cells: usize,
    pub cpu_mesh_bytes: u64,
    pub resident_triangles: usize,
    pub committed_transitions: u64,
    pub discarded_jobs: u64,
    pub pending: bool,
    pub last_preparation_ms: Option<f64>,
    pub last_error: Option<String>,
}

#[derive(Component, Clone, Copy, Debug)]
pub struct StreamedTerrainCell {
    pub logical_id: EntityId,
    pub key: CellKey,
    pub detail: CellDetail,
    pub source: ContentHash,
}

/// Private non-walkable limits owned and physically verified by the playable host.
#[derive(Component)]
pub(crate) struct StreamedTerrainBoundary;

struct LiveCell {
    entity: Entity,
    // Immutable identity assigned at publication, independent of the ECS marker.
    logical_id: EntityId,
    mesh: Handle<Mesh>,
    collider: Option<Collider>,
    collider_entity: Option<Entity>,
    collider_transform: Option<Transform>,
    collider_marker: Option<crate::TerrainHeightfieldCollider>,
    triangles: usize,
    bytes: u64,
}

impl LiveCell {
    fn is_intact(&self, world: &World) -> bool {
        world
            .get::<Mesh3d>(self.entity)
            .is_some_and(|mesh| mesh.0 == self.mesh)
            && world.resource::<Assets<Mesh>>().get(&self.mesh).is_some()
            && world
                .get::<Transform>(self.entity)
                .is_some_and(|transform| *transform == Transform::IDENTITY)
            && world.get::<ChildOf>(self.entity).is_none()
            && self.collider_marker.is_none_or(|marker| {
                world
                    .get::<crate::visual_valley::VisualValleyTerrainChunk>(self.entity)
                    .is_some_and(|chunk| {
                        chunk.id == marker.id
                            && chunk.coordinate == marker.coordinate
                            && chunk.lod_level == 0
                    })
            })
            && match (&self.collider, self.collider_entity) {
                (Some(expected), Some(entity)) => {
                    (entity == self.entity || world.get::<Collider>(self.entity).is_none())
                        && world.get::<ChildOf>(entity).is_none()
                        && world.get::<Transform>(entity) == self.collider_transform.as_ref()
                        && world.get::<crate::TerrainHeightfieldCollider>(entity)
                            == self.collider_marker.as_ref()
                        && world.get::<Collider>(entity).is_some_and(|current| {
                            std::ptr::eq(expected.raw.as_ref(), current.raw.as_ref())
                        })
                }
                (None, None) => world.get::<Collider>(self.entity).is_none(),
                _ => false,
            }
    }
}

enum CellMaterial {
    Uniform(Handle<StandardMaterial>),
    Authored(Handle<crate::visual_valley::VisualValleyTerrainMaterial>),
}

struct PreparedGeometry {
    acknowledgement: LoadedCell,
    mesh: Mesh,
    collider: Option<Collider>,
    collider_center: Option<Vec3>,
    collider_marker: Option<crate::TerrainHeightfieldCollider>,
    staging_bytes: u64,
    triangles: usize,
}

struct Job {
    request: Request,
    plan: CellStreamPlan,
    replacement: Option<SourceReplacement>,
    task: Task<(Result<PreparedBatch, String>, f64)>,
}

struct PreparedBatch {
    cells: Vec<PreparedGeometry>,
    hlod: Option<hlod::Prepared>,
}

#[derive(Clone)]
enum CellSource {
    Sampled(SampledCellSource),
    Procedural {
        source: ProceduralCellSource,
        quality: SampledMeshQuality,
    },
}

impl CellSource {
    fn sampled(&self) -> Option<&SampledCellSource> {
        match self {
            Self::Sampled(source) => Some(source),
            Self::Procedural { .. } => None,
        }
    }

    fn document_hash(&self) -> ContentHash {
        match self {
            Self::Sampled(source) => source.terrain().document_hash(),
            Self::Procedural { source, .. } => source.terrain().document_hash(),
        }
    }

    fn area_id(&self) -> AreaId {
        match self {
            Self::Sampled(source) => source.terrain().document().area_id,
            Self::Procedural { source, .. } => source.terrain().document().area_id,
        }
    }

    fn residency_source(&self) -> ContentHash {
        match self {
            Self::Sampled(source) => source.terrain().document_hash(),
            Self::Procedural { source, .. } => source.residency_source(),
        }
    }

    fn mesh_quality(&self) -> SampledMeshQuality {
        match self {
            Self::Sampled(source) => source.mesh_quality(),
            Self::Procedural { quality, .. } => *quality,
        }
    }

    fn with_mesh_quality(
        &self,
        policy: &CellStreamingPolicy,
        quality: SampledMeshQuality,
    ) -> Result<Self, String> {
        match self {
            Self::Sampled(source) => Ok(Self::Sampled(SampledCellSource::with_mesh_quality(
                source.terrain().clone(),
                policy.clone(),
                quality,
            )?)),
            Self::Procedural { source, .. } => Ok(Self::Procedural {
                source: {
                    validate_procedural_staging(policy, quality)?;
                    ProceduralCellSource::new(
                        source.terrain().clone(),
                        policy.clone(),
                        quality.coarse_stride(),
                    )?
                },
                quality,
            }),
        }
    }

    fn prepare_geometry(
        &self,
        plan: &CellStreamPlan,
        policy: &CellStreamingPolicy,
        authored: bool,
    ) -> Result<Vec<PreparedGeometry>, String> {
        match self {
            Self::Sampled(source) => source
                .prepare(plan)?
                .into_iter()
                .map(|payload| geometry(payload, authored))
                .collect(),
            Self::Procedural { source, .. } => source
                .prepare(plan)?
                .into_iter()
                .map(|payload| procedural_geometry(payload, policy, authored))
                .collect(),
        }
    }

    #[cfg(test)]
    fn terrain(&self) -> &Arc<SampledTerrainPlan> {
        self.sampled().expect("sampled test fixture").terrain()
    }

    #[cfg(test)]
    fn build(&self, key: CellKey, detail: CellDetail) -> Result<SampledCellMesh, String> {
        self.sampled()
            .expect("sampled test fixture")
            .build(key, detail)
    }

    #[cfg(test)]
    fn prepare(&self, plan: &CellStreamPlan) -> Result<Vec<SampledCellMesh>, String> {
        self.sampled().expect("sampled test fixture").prepare(plan)
    }
}

impl From<SampledCellSource> for CellSource {
    fn from(source: SampledCellSource) -> Self {
        Self::Sampled(source)
    }
}

struct SourceReplacement {
    source: CellSource,
    stream: CellStreamer,
}

#[derive(Resource)]
struct State {
    session_id: EntityId,
    source: CellSource,
    requested_source: Option<CellSource>,
    policy: CellStreamingPolicy,
    stream: CellStreamer,
    material: CellMaterial,
    pending: Option<Job>,
    last_request: Option<Request>,
    live: BTreeMap<CellKey, LiveCell>,
    hlod: Option<hlod::State>,
}

/// One controller per world; existing renderers and installed sessions opt in explicitly.
pub struct SampledCellStreamingPlugin {
    source: Mutex<Option<SampledCellSource>>,
    policy: CellStreamingPolicy,
    session_id: EntityId,
    material: StandardMaterial,
    initial_position: [f64; 2],
    authored_material_layers: bool,
    terrain_hlod: bool,
}

impl SampledCellStreamingPlugin {
    pub fn new(
        terrain: Arc<SampledTerrainPlan>,
        policy: CellStreamingPolicy,
        session_id: EntityId,
        material: StandardMaterial,
        initial_position: [f64; 2],
    ) -> Result<Self, String> {
        policy
            .cell_at(initial_position)
            .map_err(|error| error.to_string())?;
        let source = SampledCellSource::new(terrain, policy.clone(), 8)?;
        Ok(Self {
            source: Mutex::new(Some(source)),
            policy,
            session_id,
            material,
            initial_position,
            authored_material_layers: false,
            terrain_hlod: false,
        })
    }

    /// Reuse Forge's existing three-role PBR palette and canonical paint weights,
    /// including source sample overrides, at both full and coarse detail.
    #[must_use]
    pub const fn with_authored_material_layers(mut self) -> Self {
        self.authored_material_layers = true;
        self
    }

    /// Cover the remainder of the finite map with a balanced, aggregated terrain
    /// hierarchy. Its buffers share the existing resident/staging byte budgets.
    #[must_use]
    pub const fn with_terrain_hlod(mut self) -> Self {
        self.terrain_hlod = true;
        self
    }
}

impl Plugin for SampledCellStreamingPlugin {
    fn build(&self, app: &mut App) {
        // Bevy retains plugin configuration after cleanup. Transfer the only bootstrap
        // owner now so a later source replacement can release the initial raster too.
        let source = self
            .source
            .lock()
            .expect("Only plugin build accesses the source transfer lock")
            .take()
            .expect("A cell streaming plugin instance is built exactly once");
        install_cell_streaming(
            app,
            CellBootstrap {
                source: CellSource::Sampled(source),
                policy: &self.policy,
                session_id: self.session_id,
                material: &self.material,
                initial_position: self.initial_position,
                authored_material_layers: self.authored_material_layers,
                terrain_hlod: self.terrain_hlod,
            },
        );
    }
}

/// The same bounded controller for a procedural terrain plan.
pub struct ProceduralCellStreamingPlugin {
    source: Mutex<Option<ProceduralCellSource>>,
    policy: CellStreamingPolicy,
    session_id: EntityId,
    material: StandardMaterial,
    initial_position: [f64; 2],
    authored_material_layers: bool,
    terrain_hlod: bool,
}

impl ProceduralCellStreamingPlugin {
    pub fn new(
        terrain: Arc<TerrainGenerationPlan>,
        policy: CellStreamingPolicy,
        session_id: EntityId,
        material: StandardMaterial,
        initial_position: [f64; 2],
    ) -> Result<Self, String> {
        policy
            .cell_at(initial_position)
            .map_err(|error| error.to_string())?;
        let quality = SampledMeshQuality::new(8, 8)?;
        validate_procedural_staging(&policy, quality)?;
        let source = ProceduralCellSource::new(terrain, policy.clone(), quality.coarse_stride())?;
        Ok(Self {
            source: Mutex::new(Some(source)),
            policy,
            session_id,
            material,
            initial_position,
            authored_material_layers: false,
            terrain_hlod: false,
        })
    }

    #[must_use]
    pub const fn with_authored_material_layers(mut self) -> Self {
        self.authored_material_layers = true;
        self
    }

    #[must_use]
    pub const fn with_terrain_hlod(mut self) -> Self {
        self.terrain_hlod = true;
        self
    }
}

impl Plugin for ProceduralCellStreamingPlugin {
    fn build(&self, app: &mut App) {
        let source = self
            .source
            .lock()
            .expect("source transfer lock")
            .take()
            .expect("A cell streaming plugin instance is built exactly once");
        install_cell_streaming(
            app,
            CellBootstrap {
                source: CellSource::Procedural {
                    source,
                    quality: SampledMeshQuality::new(8, 8).expect("known procedural quality"),
                },
                policy: &self.policy,
                session_id: self.session_id,
                material: &self.material,
                initial_position: self.initial_position,
                authored_material_layers: self.authored_material_layers,
                terrain_hlod: self.terrain_hlod,
            },
        );
    }
}

struct CellBootstrap<'a> {
    source: CellSource,
    policy: &'a CellStreamingPolicy,
    session_id: EntityId,
    material: &'a StandardMaterial,
    initial_position: [f64; 2],
    authored_material_layers: bool,
    terrain_hlod: bool,
}

fn install_cell_streaming(app: &mut App, bootstrap: CellBootstrap<'_>) {
    let material = if bootstrap.authored_material_layers {
        crate::visual_valley::register_terrain_material(app);
        let detail_texture = app
            .world_mut()
            .resource_mut::<Assets<Image>>()
            .add(crate::visual_valley::terrain_detail_image());
        CellMaterial::Authored(
            app.world_mut()
                .resource_mut::<Assets<crate::visual_valley::VisualValleyTerrainMaterial>>()
                .add(crate::visual_valley::VisualValleyTerrainMaterial {
                    base: bootstrap.material.clone(),
                    extension: crate::visual_valley::VisualValleyTerrainExtension {
                        detail_texture,
                        ..default()
                    },
                }),
        )
    } else {
        CellMaterial::Uniform(
            app.world_mut()
                .resource_mut::<Assets<StandardMaterial>>()
                .add(bootstrap.material.clone()),
        )
    };
    // Constructor validates the immutable policy, so this cannot fail during plugin registration.
    let stream = CellStreamer::new(
        bootstrap.session_id,
        bootstrap.source.residency_source(),
        bootstrap.policy.clone(),
    )
    .expect("cell streaming constructor validated its policy");
    if let Some(sampled) = bootstrap.source.sampled() {
        app.insert_resource(ResidentSampledTerrain::new(sampled.terrain().clone()));
    }
    app.insert_resource(CellStreamingObserver {
        position_xz_m: bootstrap.initial_position,
        retry_sequence: 0,
    })
    .init_resource::<CellStreamingStats>()
    .init_resource::<TerrainHlodStats>()
    .init_resource::<CellStreamingProtection>()
    .insert_resource(State {
        session_id: bootstrap.session_id,
        source: bootstrap.source,
        requested_source: None,
        policy: bootstrap.policy.clone(),
        stream,
        material,
        pending: None,
        last_request: None,
        live: BTreeMap::new(),
        hlod: bootstrap.terrain_hlod.then(hlod::State::default),
    })
    .add_systems(Update, update.in_set(CellStreamingSet::Update));
}

fn validate_procedural_staging(
    policy: &CellStreamingPolicy,
    quality: SampledMeshQuality,
) -> Result<(), String> {
    let bound = |stride| -> Result<u64, String> {
        let topology =
            forge_world::TerrainLodTopology::new(stride).map_err(|error| error.to_string())?;
        let vertices = u64::try_from(topology.source_vertex_indices().len())
            .map_err(|error| error.to_string())?;
        let indices =
            u64::try_from(topology.triangle_indices().len()).map_err(|error| error.to_string())?;
        // Raw positions, normals, UV, RGB and the temporary RGBA buffer coexist.
        vertices
            .checked_mul(60)
            .and_then(|size| {
                indices
                    .checked_mul(4)
                    .and_then(|value| size.checked_add(value))
            })
            .ok_or_else(|| "Procedural cell staging byte count overflow".into())
    };
    if policy.full_cell_bytes < bound(1)?
        || policy.hlod_cell_bytes < bound(quality.coarse_stride())?
    {
        return Err("Procedural cell estimates do not cover color conversion staging".into());
    }
    Ok(())
}

fn geometry(
    mut payload: SampledCellMesh,
    authored_material_layers: bool,
) -> Result<PreparedGeometry, String> {
    let staging_bytes = payload.acknowledgement.bytes;
    let triangles = payload.indices.len() / 3;
    let collider = if payload.acknowledgement.detail == CellDetail::Full {
        Some(
            Collider::trimesh(
                payload
                    .positions_m
                    .iter()
                    .copied()
                    .map(Vec3::from_array)
                    .collect(),
                payload.indices.as_chunks::<3>().0.to_vec(),
            )
            .map_err(|error| error.to_string())?,
        )
    } else {
        None
    };
    let mut mesh = Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    );
    mesh.insert_attribute(Mesh::ATTRIBUTE_POSITION, payload.positions_m);
    mesh.insert_attribute(Mesh::ATTRIBUTE_NORMAL, payload.normals);
    if authored_material_layers {
        mesh.insert_attribute(Mesh::ATTRIBUTE_COLOR, payload.material_weights);
    } else {
        // The uniform diagnostic does not retain unused paint buffers.
        payload.acknowledgement.bytes -=
            (payload.material_weights.capacity() * size_of::<[f32; 4]>()) as u64;
    }
    mesh.insert_indices(Indices::U32(payload.indices));
    Ok(PreparedGeometry {
        acknowledgement: payload.acknowledgement,
        mesh,
        collider,
        collider_center: None,
        collider_marker: None,
        staging_bytes,
        triangles,
    })
}

fn procedural_geometry(
    mut payload: ProceduralCellMesh,
    policy: &CellStreamingPolicy,
    authored_material_layers: bool,
) -> Result<PreparedGeometry, String> {
    let triangles = payload.indices.len() / 3;
    let prepared = if payload.acknowledgement.detail == CellDetail::Full {
        Some(
            crate::physics::build_procedural_heightfield(&payload)
                .map_err(|error| error.to_string())?,
        )
    } else {
        None
    };
    let mut mesh = Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    );
    let mut staging_bytes = payload.acknowledgement.bytes;
    let rgb_bytes = u64::try_from(
        payload.material_weights.capacity() * size_of::<forge_world::TerrainMaterialWeights>(),
    )
    .map_err(|error| error.to_string())?;
    if authored_material_layers {
        let colors: Vec<_> = payload
            .material_weights
            .iter()
            .map(|weights| [weights.grass, weights.earth, weights.rock, 1.0])
            .collect();
        let color_bytes = u64::try_from(colors.capacity() * size_of::<[f32; 4]>())
            .map_err(|error| error.to_string())?;
        let staging = payload
            .acknowledgement
            .bytes
            .checked_add(color_bytes)
            .ok_or("Procedural cell conversion byte count overflow")?;
        let bound = if payload.acknowledgement.detail == CellDetail::Full {
            policy.full_cell_bytes
        } else {
            policy.hlod_cell_bytes
        };
        if staging > bound {
            return Err("Procedural cell conversion exceeds the staging estimate".into());
        }
        staging_bytes = staging;
        payload.acknowledgement.bytes = staging
            .checked_sub(rgb_bytes)
            .ok_or("Procedural cell material byte count mismatch")?;
        mesh.insert_attribute(Mesh::ATTRIBUTE_COLOR, colors);
    } else {
        payload.acknowledgement.bytes = payload
            .acknowledgement
            .bytes
            .checked_sub(rgb_bytes)
            .ok_or("Procedural cell material byte count mismatch")?;
    }
    mesh.insert_attribute(Mesh::ATTRIBUTE_POSITION, payload.positions_m);
    mesh.insert_attribute(Mesh::ATTRIBUTE_NORMAL, payload.normals);
    mesh.insert_attribute(Mesh::ATTRIBUTE_UV_0, payload.uvs);
    mesh.insert_indices(Indices::U32(payload.indices));
    let (collider, collider_center, collider_marker) = prepared
        .map_or((None, None, None), |(collider, center, marker)| {
            (Some(collider), Some(center), Some(marker))
        });
    Ok(PreparedGeometry {
        acknowledgement: payload.acknowledgement,
        mesh,
        collider,
        collider_center,
        collider_marker,
        staging_bytes,
        triangles,
    })
}

fn update(world: &mut World) {
    let Some(mut state) = world.remove_resource::<State>() else {
        return;
    };
    if let Err(error) = tick(world, &mut state) {
        world.resource_mut::<CellStreamingStats>().last_error = Some(error);
    }
    world.resource_mut::<CellStreamingStats>().pending = state.pending.is_some();
    world.insert_resource(state);
}

fn tick(world: &mut World, state: &mut State) -> Result<(), String> {
    let observer = *world.resource::<CellStreamingObserver>();
    let request = requested(world, state);
    // Always poll an outstanding job, including after the observer leaves the grid.
    if let Some(job) = state.pending.as_mut() {
        let Some((prepared, elapsed)) = check_ready(&mut job.task) else {
            return request.map(|_| ());
        };
        let job = state
            .pending
            .take()
            .ok_or("Cell preparation job disappeared")?;
        world
            .resource_mut::<CellStreamingStats>()
            .last_preparation_ms = Some(elapsed);
        if request
            .as_ref()
            .is_ok_and(|current| *current == job.request)
        {
            state.last_request = Some(job.request);
            source_updates::admit_job(world, state, &job.plan, prepared?, job.replacement)?;
        } else {
            world.resource_mut::<CellStreamingStats>().discarded_jobs += 1;
        }
    }
    let request = request?;
    if state.last_request.as_ref() == Some(&request) {
        return Ok(());
    }
    let replacement = state
        .requested_source
        .as_ref()
        .map(|source| source_updates::replacement(state, source.clone()))
        .transpose()?;
    let stream = replacement
        .as_ref()
        .map_or(&state.stream, |next| &next.stream);
    let plan = stream
        .prepare_with_protected_cells(observer.position_xz_m, &request.protected)
        .map_err(|error| error.to_string())?;
    if replacement.is_some() {
        source_updates::check_staging_budget(state, &plan)?;
    }
    if plan.acquisitions().is_empty() && plan.retirements().is_empty() {
        validate_live_projection(world, state)?;
        state.last_request = Some(request);
        world.resource_mut::<CellStreamingStats>().last_error = None;
        return Ok(());
    }
    let pool = AsyncComputeTaskPool::try_get().ok_or("Cell streaming compute pool unavailable")?;
    let source = replacement
        .as_ref()
        .map_or_else(|| state.source.clone(), |next| next.source.clone());
    let preparation_plan = plan.clone();
    let distant = hlod::request_with_stride(state, &plan, source.mesh_quality().distant_stride())?;
    let authored_material_layers = matches!(state.material, CellMaterial::Authored(_));
    let preparation_policy = state.policy.clone();
    state.pending = Some(Job {
        request,
        plan,
        replacement,
        task: pool.spawn(async move {
            let started = bevy::platform::time::Instant::now();
            let result = source
                .prepare_geometry(
                    &preparation_plan,
                    &preparation_policy,
                    authored_material_layers,
                )
                .and_then(|cells| {
                    let hlod = distant
                        .map(|request| {
                            hlod::prepare_source(&source, request, authored_material_layers)
                        })
                        .transpose()?;
                    Ok(PreparedBatch { cells, hlod })
                });
            (result, started.elapsed().as_secs_f64() * 1000.0)
        }),
    });
    Ok(())
}

fn requested(world: &World, state: &State) -> Result<Request, String> {
    let observer = world.resource::<CellStreamingObserver>();
    let protected = &world.resource::<CellStreamingProtection>().cells;
    if protected.len() > usize::from(state.policy.max_resident_cells) {
        return Err("Protected cell requests exceed the residency bound".into());
    }
    let mut protected = protected.clone();
    protected.sort_unstable();
    protected.dedup();
    Ok(Request {
        mesh_quality: state
            .requested_source
            .as_ref()
            .unwrap_or(&state.source)
            .mesh_quality(),
        source: state
            .requested_source
            .as_ref()
            .unwrap_or(&state.source)
            .document_hash(),
        observer: state
            .policy
            .cell_at(observer.position_xz_m)
            .map_err(|e| e.to_string())?,
        protected,
        retry_sequence: observer.retry_sequence,
    })
}

fn validate_live_projection(world: &World, state: &State) -> Result<(), String> {
    for live in state.live.values() {
        if !live.is_intact(world) {
            return Err("An active streamed terrain projection was removed or replaced".into());
        }
    }
    Ok(())
}

fn admit(
    world: &mut World,
    state: &mut State,
    plan: &CellStreamPlan,
    prepared: PreparedBatch,
) -> Result<(), String> {
    // An unrelated removal invalidates the projection; do not publish a false residency receipt.
    validate_live_projection(world, state)?;
    let procedural = matches!(&state.source, CellSource::Procedural { .. });
    if prepared.cells.iter().any(|cell| {
        cell.acknowledgement.bytes > cell.staging_bytes
            || (cell.acknowledgement.detail == CellDetail::Full) != cell.collider.is_some()
            || (procedural && cell.collider.is_some() && cell.collider_center.is_none())
            || (!procedural && cell.collider_center.is_some())
            || cell.collider_center.is_some() != cell.collider_marker.is_some()
            || (cell.collider_center.is_some() && cell.collider.is_none())
            || cell.collider_marker.is_some_and(|marker| {
                marker.coordinate.x != cell.acknowledgement.key.x
                    || marker.coordinate.z != cell.acknowledgement.key.z
            })
    }) {
        return Err("Streamed terrain collider metadata is inconsistent".into());
    }
    let staging_bytes = hlod::validate(world, state, plan, &prepared)?;
    let acknowledgements: Vec<_> = prepared
        .cells
        .iter()
        .map(|cell| cell.acknowledgement)
        .collect();
    state
        .stream
        .commit(plan, &acknowledgements)
        .map_err(|error| error.to_string())?;
    // All fallible admission is complete. Mutate immediately in one exclusive schedule,
    // without yielding a frame or exposing a partially executed Commands buffer.
    let retired: Vec<_> = state
        .live
        .keys()
        .filter(|key| !plan.desired().contains_key(key) || plan.acquisitions().contains_key(key))
        .copied()
        .collect();
    for key in retired {
        if let Some(live) = state.live.remove(&key) {
            world.despawn(live.entity);
            if let Some(collider) = live.collider_entity.filter(|entity| *entity != live.entity) {
                world.despawn(collider);
            }
            world.resource_mut::<Assets<Mesh>>().remove(live.mesh.id());
        }
    }
    let area = state.source.area_id();
    for cell in prepared.cells {
        let (key, live) = publish_cell(world, &state.material, area, cell);
        state.live.insert(key, live);
    }
    if let Some(distant) = prepared.hlod {
        hlod::apply(world, state, distant, staging_bytes);
    }
    let resident = state.stream.resident();
    let mut metrics = world.resource_mut::<CellStreamingStats>();
    metrics.resident_cells = resident.len();
    metrics.detailed_cells = resident
        .values()
        .filter(|cell| cell.detail == CellDetail::Full)
        .count();
    metrics.coarse_cells = resident.len() - metrics.detailed_cells;
    metrics.cpu_mesh_bytes = resident.values().map(|cell| cell.bytes).sum();
    metrics.resident_triangles = state.live.values().map(|cell| cell.triangles).sum();
    metrics.committed_transitions += 1;
    metrics.last_error = None;
    Ok(())
}

fn publish_cell(
    world: &mut World,
    material: &CellMaterial,
    area: AreaId,
    cell: PreparedGeometry,
) -> (CellKey, LiveCell) {
    let ack = cell.acknowledgement;
    let logical_id = ack.key.entity_id(area);
    let mesh = world.resource_mut::<Assets<Mesh>>().add(cell.mesh);
    // Keep a shared reference to the exact prepared shape, not a later replacement.
    let expected_collider = cell.collider.clone();
    let mut centered_collider = None;
    let root = {
        let mut entity = world.spawn((
            Mesh3d(mesh.clone()),
            Transform::IDENTITY,
            StreamedTerrainCell {
                logical_id,
                key: ack.key,
                detail: ack.detail,
                source: ack.source,
            },
        ));
        match material {
            CellMaterial::Uniform(handle) => {
                entity.insert(MeshMaterial3d(handle.clone()));
            }
            CellMaterial::Authored(handle) => {
                entity.insert(MeshMaterial3d(handle.clone()));
            }
        }
        if let Some(marker) = cell.collider_marker {
            entity.insert(crate::visual_valley::VisualValleyTerrainChunk {
                id: marker.id,
                coordinate: marker.coordinate,
                lod_level: 0,
            });
        }
        if let Some(collider) = cell.collider {
            if let (Some(center), Some(marker)) = (cell.collider_center, cell.collider_marker) {
                centered_collider = Some((collider, Transform::from_translation(center), marker));
            } else {
                entity.insert(collider);
            }
        }
        entity.id()
    };
    let (collider_entity, collider_transform) =
        if let Some((collider, transform, marker)) = centered_collider {
            (
                Some(world.spawn((collider, transform, marker)).id()),
                Some(transform),
            )
        } else if expected_collider.is_some() {
            (Some(root), Some(Transform::IDENTITY))
        } else {
            (None, None)
        };
    (
        ack.key,
        LiveCell {
            entity: root,
            logical_id,
            mesh,
            collider: expected_collider,
            collider_entity,
            collider_transform,
            collider_marker: cell.collider_marker,
            triangles: cell.triangles,
            bytes: ack.bytes,
        },
    )
}
