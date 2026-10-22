use crate::{
    ProofSceneStats, TerrainHeightfieldCollider, VisualValleyChunkDebugBounds, VisualValleyStats,
    build_bevy_terrain_mesh, build_terrain_heightfield_collider,
};
use bevy::{
    asset::{AssetId, RenderAssetUsages},
    mesh::{Indices, VertexAttributeValues},
    prelude::*,
    render::render_resource::PrimitiveTopology,
};
use bevy_rapier3d::{
    plugin::{PhysicsSet, context::RapierContextColliders},
    prelude::{Collider, RapierColliderHandle, RapierContextEntityLink},
};
use forge_core::{ContentHash, Revision};
use forge_project::TerrainDocument;
use forge_world::{
    GeneratedTerrain, TERRAIN_CHUNK_COUNT, TERRAIN_CHUNK_VERTICES_PER_AXIS, TerrainChunkCoordinate,
    TerrainChunkId, TerrainChunkMesh, TerrainDirtyChunk, TerrainGenerationPlan,
    TerrainHeightSampleCoordinate, TerrainPartialUpdateRequest,
};
use std::{collections::BTreeMap, error::Error, fmt};

/// Public fixed-schedule boundary for runtime probes and later Studio commands.
#[derive(Clone, Debug, Hash, PartialEq, Eq, SystemSet)]
pub enum TerrainRuntimeUpdateSet {
    /// Applies the CPU/ECS transaction before Rapier synchronizes changed colliders.
    Apply,
    /// Publishes success only after Rapier writeback proves the backend projection.
    Verify,
}

/// Canonical terrain source and its mutable derived cache inside the Bevy adapter.
#[derive(Resource)]
pub struct TerrainRuntimeState {
    document: TerrainDocument,
    cache: GeneratedTerrain,
}

impl TerrainRuntimeState {
    pub(crate) const fn new(document: TerrainDocument, cache: GeneratedTerrain) -> Self {
        Self { document, cache }
    }

    #[must_use]
    pub const fn document(&self) -> &TerrainDocument {
        &self.document
    }

    #[must_use]
    pub const fn cache(&self) -> &GeneratedTerrain {
        &self.cache
    }
}

#[derive(Default)]
struct TerrainChunkBinding {
    coordinate: Option<TerrainChunkCoordinate>,
    render_entity: Option<Entity>,
    mesh_handle: Option<Handle<Mesh>>,
    collider_entity: Option<Entity>,
}

/// Stable mapping between one Forge chunk and its render/physics projections.
#[derive(Default, Resource)]
pub struct TerrainRuntimeBindings {
    chunks: BTreeMap<TerrainChunkId, TerrainChunkBinding>,
}

impl TerrainRuntimeBindings {
    pub(crate) fn register_render(
        &mut self,
        id: TerrainChunkId,
        coordinate: TerrainChunkCoordinate,
        entity: Entity,
        mesh_handle: Handle<Mesh>,
    ) {
        let binding = self.chunks.entry(id).or_default();
        register_coordinate(binding, coordinate);
        assert!(
            binding.render_entity.replace(entity).is_none(),
            "a terrain render binding is registered exactly once"
        );
        assert!(
            binding.mesh_handle.replace(mesh_handle).is_none(),
            "a terrain mesh handle is registered exactly once"
        );
    }

    pub(crate) fn register_collider(
        &mut self,
        id: TerrainChunkId,
        coordinate: TerrainChunkCoordinate,
        entity: Entity,
    ) {
        let binding = self.chunks.entry(id).or_default();
        register_coordinate(binding, coordinate);
        assert!(
            binding.collider_entity.replace(entity).is_none(),
            "a terrain collider binding is registered exactly once"
        );
    }

    /// Returns a deterministic snapshot only when all 64 bindings are complete.
    pub fn snapshots(
        &self,
    ) -> Result<Vec<TerrainRuntimeBindingSnapshot>, TerrainRuntimeUpdateError> {
        if self.chunks.len() != TERRAIN_CHUNK_COUNT {
            return Err(TerrainRuntimeUpdateError::IncompleteBindings {
                expected: TERRAIN_CHUNK_COUNT,
                actual: self.chunks.len(),
            });
        }

        self.chunks
            .iter()
            .map(|(id, binding)| {
                Ok(TerrainRuntimeBindingSnapshot {
                    id: *id,
                    coordinate: binding
                        .coordinate
                        .ok_or(TerrainRuntimeUpdateError::IncompleteBinding(*id))?,
                    render_entity: binding
                        .render_entity
                        .ok_or(TerrainRuntimeUpdateError::IncompleteBinding(*id))?,
                    mesh_asset_id: binding
                        .mesh_handle
                        .as_ref()
                        .ok_or(TerrainRuntimeUpdateError::IncompleteBinding(*id))?
                        .id(),
                    collider_entity: binding
                        .collider_entity
                        .ok_or(TerrainRuntimeUpdateError::IncompleteBinding(*id))?,
                })
            })
            .collect()
    }

    fn binding(
        &self,
        id: TerrainChunkId,
    ) -> Result<&TerrainChunkBinding, TerrainRuntimeUpdateError> {
        self.chunks
            .get(&id)
            .ok_or(TerrainRuntimeUpdateError::MissingBinding(id))
    }
}

fn register_coordinate(binding: &mut TerrainChunkBinding, coordinate: TerrainChunkCoordinate) {
    if let Some(existing) = binding.coordinate {
        assert_eq!(
            existing, coordinate,
            "render and collider bindings must use the same chunk coordinate"
        );
    } else {
        binding.coordinate = Some(coordinate);
    }
}

/// Copyable identity snapshot used by tests and bounded runtime probes.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct TerrainRuntimeBindingSnapshot {
    pub id: TerrainChunkId,
    pub coordinate: TerrainChunkCoordinate,
    pub render_entity: Entity,
    pub mesh_asset_id: AssetId<Mesh>,
    pub collider_entity: Entity,
}

impl TerrainRuntimeBindingSnapshot {
    #[must_use]
    pub const fn entity(self) -> Entity {
        self.render_entity
    }

    #[must_use]
    pub const fn mesh_asset_id(self) -> AssetId<Mesh> {
        self.mesh_asset_id
    }

    #[must_use]
    pub const fn collider_entity(self) -> Entity {
        self.collider_entity
    }
}

#[derive(Debug)]
struct QueuedTerrainPartialUpdate {
    sequence: u64,
    request: TerrainPartialUpdateRequest,
    mode: TerrainProjectionMode,
}

#[derive(Debug)]
enum TerrainProjectionMode {
    SparseEdit,
    AuthoringEdit,
    AuthoringSnapshot,
}

/// Single-slot request queue. At most one terrain transaction is applied per fixed tick.
#[derive(Default, Resource)]
pub struct PendingTerrainPartialUpdate {
    next_sequence: u64,
    request: Option<QueuedTerrainPartialUpdate>,
}

impl PendingTerrainPartialUpdate {
    pub fn submit(
        &mut self,
        request: TerrainPartialUpdateRequest,
    ) -> Result<u64, TerrainUpdateSubmissionError> {
        if self.request.is_some() {
            return Err(TerrainUpdateSubmissionError::RequestAlreadyPending);
        }
        self.next_sequence = self
            .next_sequence
            .checked_add(1)
            .ok_or(TerrainUpdateSubmissionError::SequenceOverflow)?;
        let sequence = self.next_sequence;
        self.request = Some(QueuedTerrainPartialUpdate {
            sequence,
            request,
            mode: TerrainProjectionMode::SparseEdit,
        });
        Ok(sequence)
    }

    /// Editor metadata/base-elevation update, preserving the strict sparse API.
    pub fn submit_authoring(
        &mut self,
        request: TerrainPartialUpdateRequest,
    ) -> Result<u64, TerrainUpdateSubmissionError> {
        let sequence = self.submit(request)?;
        if let Some(queued) = &mut self.request {
            queued.mode = TerrainProjectionMode::AuthoringEdit;
        }
        Ok(sequence)
    }

    /// Projects an accepted source snapshot without rewriting its historical revision.
    /// The live base and all mesh/collider transaction checks still apply.
    pub fn submit_authoring_snapshot(
        &mut self,
        request: TerrainPartialUpdateRequest,
    ) -> Result<u64, TerrainUpdateSubmissionError> {
        let sequence = self.submit(request)?;
        if let Some(queued) = &mut self.request {
            queued.mode = TerrainProjectionMode::AuthoringSnapshot;
        }
        Ok(sequence)
    }

    #[must_use]
    pub const fn is_pending(&self) -> bool {
        self.request.is_some()
    }

    fn take(&mut self) -> Option<QueuedTerrainPartialUpdate> {
        self.request.take()
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum TerrainUpdateSubmissionError {
    RequestAlreadyPending,
    SequenceOverflow,
}

impl fmt::Display for TerrainUpdateSubmissionError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::RequestAlreadyPending => {
                formatter.write_str("a terrain partial update is already pending")
            }
            Self::SequenceOverflow => formatter.write_str("terrain update sequence overflow"),
        }
    }
}

impl Error for TerrainUpdateSubmissionError {}

/// Successful application report retained until the next processed request.
#[derive(Clone, Debug, PartialEq)]
pub struct TerrainRuntimeUpdateReport {
    sequence: u64,
    source_document_hash: ContentHash,
    target_document_hash: ContentHash,
    target_cache_hash: ContentHash,
    source_revision: Revision,
    target_revision: Revision,
    changed_samples: Vec<TerrainHeightSampleCoordinate>,
    mesh_chunks: Vec<TerrainDirtyChunk>,
    collider_chunks: Vec<TerrainDirtyChunk>,
    binding_count: usize,
    mesh_assets_verified: usize,
    collider_components_verified: usize,
    rapier_backends_verified: usize,
}

impl TerrainRuntimeUpdateReport {
    #[must_use]
    pub const fn sequence(&self) -> u64 {
        self.sequence
    }

    #[must_use]
    pub const fn source_document_hash(&self) -> ContentHash {
        self.source_document_hash
    }

    #[must_use]
    pub const fn target_document_hash(&self) -> ContentHash {
        self.target_document_hash
    }

    #[must_use]
    pub const fn target_cache_hash(&self) -> ContentHash {
        self.target_cache_hash
    }

    #[must_use]
    pub const fn source_revision(&self) -> Revision {
        self.source_revision
    }

    #[must_use]
    pub const fn target_revision(&self) -> Revision {
        self.target_revision
    }

    #[must_use]
    pub fn changed_samples(&self) -> &[TerrainHeightSampleCoordinate] {
        &self.changed_samples
    }

    #[must_use]
    pub fn mesh_chunks(&self) -> &[TerrainDirtyChunk] {
        &self.mesh_chunks
    }

    #[must_use]
    pub fn collider_chunks(&self) -> &[TerrainDirtyChunk] {
        &self.collider_chunks
    }

    #[must_use]
    pub const fn binding_count(&self) -> usize {
        self.binding_count
    }

    #[must_use]
    pub const fn mesh_assets_verified(&self) -> usize {
        self.mesh_assets_verified
    }

    #[must_use]
    pub const fn collider_components_verified(&self) -> usize {
        self.collider_components_verified
    }

    #[must_use]
    pub const fn rapier_backends_verified(&self) -> usize {
        self.rapier_backends_verified
    }
}

/// Last processed update, exposed as a resource for automation without coupling to the ECS source.
#[derive(Default, Resource)]
pub struct TerrainRuntimeUpdateOutcome {
    processed: u64,
    sequence: Option<u64>,
    attempted_target_cache_hash: Option<ContentHash>,
    report: Option<TerrainRuntimeUpdateReport>,
    error: Option<String>,
    projection_failed: bool,
    rollback_verified: bool,
}

impl TerrainRuntimeUpdateOutcome {
    #[must_use]
    pub const fn processed(&self) -> u64 {
        self.processed
    }

    #[must_use]
    pub const fn sequence(&self) -> Option<u64> {
        self.sequence
    }

    #[must_use]
    pub const fn attempted_target_cache_hash(&self) -> Option<ContentHash> {
        self.attempted_target_cache_hash
    }

    #[must_use]
    pub const fn report(&self) -> Option<&TerrainRuntimeUpdateReport> {
        self.report.as_ref()
    }

    #[must_use]
    pub fn error(&self) -> Option<&str> {
        self.error.as_deref()
    }

    #[must_use]
    pub const fn projection_failed(&self) -> bool {
        self.projection_failed
    }

    #[must_use]
    pub const fn rollback_verified(&self) -> bool {
        self.rollback_verified
    }

    fn record_applied(&mut self, report: TerrainRuntimeUpdateReport) {
        self.processed = self.processed.saturating_add(1);
        self.sequence = Some(report.sequence);
        self.attempted_target_cache_hash = Some(report.target_cache_hash);
        self.report = Some(report);
        self.error = None;
        self.projection_failed = false;
        self.rollback_verified = false;
    }

    fn record_rejected(&mut self, sequence: u64, error: &TerrainRuntimeUpdateError) {
        self.processed = self.processed.saturating_add(1);
        self.sequence = Some(sequence);
        self.attempted_target_cache_hash = None;
        self.report = None;
        self.error = Some(error.to_string());
        self.projection_failed = false;
        self.rollback_verified = false;
    }

    fn record_projection_failure(
        &mut self,
        sequence: u64,
        attempted_target_cache_hash: ContentHash,
        error: &TerrainRuntimeUpdateError,
        rollback_verified: bool,
    ) {
        self.processed = self.processed.saturating_add(1);
        self.sequence = Some(sequence);
        self.attempted_target_cache_hash = Some(attempted_target_cache_hash);
        self.report = None;
        self.error = Some(error.to_string());
        self.projection_failed = true;
        self.rollback_verified = rollback_verified;
    }
}

/// Applies prepared terrain transactions before Rapier synchronizes changed colliders.
pub(crate) struct TerrainRuntimeUpdatePlugin;

impl Plugin for TerrainRuntimeUpdatePlugin {
    fn build(&self, app: &mut App) {
        app.init_resource::<PendingTerrainPartialUpdate>()
            .init_resource::<TerrainRuntimeUpdateOutcome>()
            .init_resource::<PendingTerrainBackendProof>()
            .configure_sets(
                FixedUpdate,
                TerrainRuntimeUpdateSet::Apply.before(PhysicsSet::SyncBackend),
            )
            .configure_sets(
                FixedUpdate,
                TerrainRuntimeUpdateSet::Verify.after(PhysicsSet::Writeback),
            )
            .add_systems(
                FixedUpdate,
                apply_pending_terrain_partial_update.in_set(TerrainRuntimeUpdateSet::Apply),
            )
            .add_systems(
                FixedUpdate,
                verify_pending_terrain_backend.in_set(TerrainRuntimeUpdateSet::Verify),
            );
    }
}

#[derive(Default, Resource)]
struct PendingTerrainBackendProof(Option<TerrainBackendPhase>);

enum TerrainBackendPhase {
    VerifyTarget(Box<TerrainBackendProof>),
    VerifyRollback(Box<TerrainRollbackProof>),
    Poisoned,
}

struct TerrainBackendProof {
    report: TerrainRuntimeUpdateReport,
    colliders: Vec<ExpectedBackendCollider>,
    component_error: Option<TerrainRuntimeUpdateError>,
    rollback: TerrainRuntimeRollback,
}

struct TerrainRollbackProof {
    sequence: u64,
    attempted_target_cache_hash: ContentHash,
    original_error: TerrainRuntimeUpdateError,
    rollback: TerrainRuntimeRollback,
}

#[derive(Clone, Copy)]
struct ExpectedBackendCollider {
    id: TerrainChunkId,
    entity: Entity,
    handle: RapierColliderHandle,
    context_entity: Entity,
}

struct TerrainRuntimeRollback {
    document: TerrainDocument,
    cache_source_document_hash: ContentHash,
    cache_hash: ContentHash,
    cache_chunks: Vec<TerrainChunkMesh>,
    bindings: Vec<TerrainRuntimeBindingSnapshot>,
    render: Vec<RenderRollback>,
    colliders: Vec<ColliderRollback>,
    visual_stats_source_hash: String,
    proof_stats_source_hash: String,
}

struct RenderRollback {
    id: TerrainChunkId,
    entity: Entity,
    asset_id: AssetId<Mesh>,
    mesh: Mesh,
    debug_bounds: VisualValleyChunkDebugBounds,
}

struct ColliderRollback {
    id: TerrainChunkId,
    entity: Entity,
    collider: Collider,
    handle: RapierColliderHandle,
    context_entity: Entity,
}

struct PreparedRenderReplacement {
    id: TerrainChunkId,
    entity: Entity,
    asset_id: AssetId<Mesh>,
    payload: PreparedRenderPayload,
    debug_bounds: VisualValleyChunkDebugBounds,
}

enum PreparedRenderPayload {
    Mesh(Mesh),
    MaterialWeights(Vec<[f32; 4]>),
}

struct PreparedColliderReplacement {
    id: TerrainChunkId,
    entity: Entity,
    handle: RapierColliderHandle,
    context_entity: Entity,
    collider: Collider,
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    clippy::too_many_lines,
    reason = "Bevy systems require owned system-parameter wrappers; this transaction keeps validation and commit visibly contiguous"
)]
fn apply_pending_terrain_partial_update(
    mut pending: ResMut<PendingTerrainPartialUpdate>,
    mut outcome: ResMut<TerrainRuntimeUpdateOutcome>,
    mut pending_backend: ResMut<PendingTerrainBackendProof>,
    mut state: ResMut<TerrainRuntimeState>,
    bindings: Res<TerrainRuntimeBindings>,
    render_entities: Query<&Mesh3d>,
    mut debug_bounds: Query<&mut VisualValleyChunkDebugBounds>,
    mut collider_components: Query<&mut Collider>,
    collider_handles: Query<(&RapierColliderHandle, &RapierContextEntityLink)>,
    mut meshes: ResMut<Assets<Mesh>>,
    mut visual_stats: ResMut<VisualValleyStats>,
    mut proof_stats: ResMut<ProofSceneStats>,
) {
    if pending_backend.0.is_some() {
        return;
    }
    let Some(queued) = pending.take() else {
        return;
    };
    let sequence = queued.sequence;

    let result: Result<TerrainBackendProof, TerrainRuntimeUpdateError> = (|| {
        let source_revision = state.document.revision;
        let plan = TerrainGenerationPlan::from_document(&state.document)
            .map_err(|error| TerrainRuntimeUpdateError::Core(error.to_string()))?;
        let update = match queued.mode {
            TerrainProjectionMode::SparseEdit => {
                plan.apply_partial_update(&state.cache, queued.request)
            }
            TerrainProjectionMode::AuthoringEdit => {
                plan.apply_authoring_update(&state.cache, queued.request)
            }
            TerrainProjectionMode::AuthoringSnapshot => {
                plan.apply_authoring_snapshot(&state.cache, queued.request)
            }
        }
        .map_err(|error| TerrainRuntimeUpdateError::Core(error.to_string()))?;
        let binding_snapshots = bindings.snapshots()?;

        // Validate every live binding and asset before preparing or mutating any projection.
        for binding in &binding_snapshots {
            let rendered = render_entities
                .get(binding.render_entity)
                .map_err(|_| TerrainRuntimeUpdateError::MissingRenderEntity(binding.id))?;
            if rendered.0.id() != binding.mesh_asset_id {
                return Err(TerrainRuntimeUpdateError::MeshHandleMismatch(binding.id));
            }
            if meshes.get(binding.mesh_asset_id).is_none() {
                return Err(TerrainRuntimeUpdateError::MissingMeshAsset(binding.id));
            }
            debug_bounds
                .get_mut(binding.render_entity)
                .map_err(|_| TerrainRuntimeUpdateError::MissingRenderEntity(binding.id))?;
            collider_components
                .get_mut(binding.collider_entity)
                .map_err(|_| TerrainRuntimeUpdateError::MissingColliderEntity(binding.id))?;
        }

        let mut prepared_render = Vec::with_capacity(update.mesh_chunks().len());
        for replacement in update.replacement_chunks() {
            let binding = bindings.binding(replacement.id)?;
            let render_entity = binding
                .render_entity
                .ok_or(TerrainRuntimeUpdateError::IncompleteBinding(replacement.id))?;
            let mesh_handle = binding
                .mesh_handle
                .as_ref()
                .ok_or(TerrainRuntimeUpdateError::IncompleteBinding(replacement.id))?;
            let rendered = render_entities
                .get(render_entity)
                .map_err(|_| TerrainRuntimeUpdateError::MissingRenderEntity(replacement.id))?;
            if rendered.0.id() != mesh_handle.id() {
                return Err(TerrainRuntimeUpdateError::MeshHandleMismatch(
                    replacement.id,
                ));
            }
            if meshes.get(mesh_handle.id()).is_none() {
                return Err(TerrainRuntimeUpdateError::MissingMeshAsset(replacement.id));
            }
            debug_bounds
                .get_mut(render_entity)
                .map_err(|_| TerrainRuntimeUpdateError::MissingRenderEntity(replacement.id))?;
            let original = state
                .cache
                .chunks
                .iter()
                .find(|chunk| chunk.id == replacement.id)
                .ok_or(TerrainRuntimeUpdateError::MissingReplacement(
                    replacement.id,
                ))?;
            let material_only = original.positions_m == replacement.positions_m
                && original.normals == replacement.normals
                && original.uvs == replacement.uvs
                && original.indices == replacement.indices;
            let (payload, replacement_bounds) = if material_only {
                // Check the live mesh before preserving its geometry in-place.
                verify_mesh_projection(
                    meshes
                        .get(mesh_handle.id())
                        .ok_or(TerrainRuntimeUpdateError::MissingMeshAsset(replacement.id))?,
                    original,
                )
                .map_err(|reason| TerrainRuntimeUpdateError::Adapter(reason.into()))?;
                let bounds = *debug_bounds
                    .get_mut(render_entity)
                    .map_err(|_| TerrainRuntimeUpdateError::MissingRenderEntity(replacement.id))?;
                (
                    PreparedRenderPayload::MaterialWeights(
                        replacement
                            .material_weights
                            .iter()
                            .map(|w| [w.grass, w.earth, w.rock, 1.0])
                            .collect(),
                    ),
                    bounds,
                )
            } else {
                let (mesh, bounds) = build_bevy_terrain_mesh(replacement)
                    .map_err(|error| TerrainRuntimeUpdateError::Adapter(error.to_string()))?;
                (PreparedRenderPayload::Mesh(mesh), bounds)
            };
            prepared_render.push(PreparedRenderReplacement {
                id: replacement.id,
                entity: render_entity,
                asset_id: mesh_handle.id(),
                payload,
                debug_bounds: replacement_bounds,
            });
        }

        let mut prepared_colliders = Vec::with_capacity(update.collider_chunks().len());
        for dirty in update.collider_chunks() {
            let replacement = update
                .replacement_chunks()
                .iter()
                .find(|chunk| chunk.id == dirty.id)
                .ok_or(TerrainRuntimeUpdateError::MissingReplacement(dirty.id))?;
            let binding = bindings.binding(dirty.id)?;
            let collider_entity = binding
                .collider_entity
                .ok_or(TerrainRuntimeUpdateError::IncompleteBinding(dirty.id))?;
            collider_components
                .get_mut(collider_entity)
                .map_err(|_| TerrainRuntimeUpdateError::MissingColliderEntity(dirty.id))?;
            let (handle, context_link) = collider_handles
                .get(collider_entity)
                .map_err(|_| TerrainRuntimeUpdateError::MissingRapierHandle(dirty.id))?;
            let collider = build_terrain_heightfield_collider(replacement)
                .map_err(|error| TerrainRuntimeUpdateError::Adapter(error.to_string()))?;
            prepared_colliders.push(PreparedColliderReplacement {
                id: dirty.id,
                entity: collider_entity,
                handle: *handle,
                context_entity: context_link.0,
                collider,
            });
        }

        let source_hash = update.source_document_hash();
        let source_cache_hash = state
            .cache
            .content_hash()
            .map_err(|_| TerrainRuntimeUpdateError::TargetCacheHashUnavailable)?;
        let source_document = state.document.clone();
        let source_chunks = update
            .mesh_chunks()
            .iter()
            .map(|dirty| {
                state
                    .cache
                    .chunks
                    .iter()
                    .find(|chunk| chunk.id == dirty.id)
                    .cloned()
                    .ok_or(TerrainRuntimeUpdateError::MissingReplacement(dirty.id))
            })
            .collect::<Result<Vec<_>, _>>()?;
        let source_visual_stats_hash = visual_stats.source_hash.clone();
        let source_proof_stats_hash = proof_stats.source_hash.clone();
        let mut render_rollback = Vec::with_capacity(prepared_render.len());
        for replacement in &prepared_render {
            let mut asset = meshes
                .get_mut(replacement.asset_id)
                .expect("validated terrain mesh asset remains borrowed for the atomic commit");
            let previous_mesh = match &replacement.payload {
                PreparedRenderPayload::Mesh(mesh) => std::mem::replace(&mut *asset, mesh.clone()),
                PreparedRenderPayload::MaterialWeights(weights) => {
                    // Keep a complete rollback snapshot, but preserve live geometry buffers.
                    let previous = asset.clone();
                    asset.insert_attribute(Mesh::ATTRIBUTE_COLOR, weights.clone());
                    previous
                }
            };
            let mut bounds = debug_bounds
                .get_mut(replacement.entity)
                .expect("validated terrain render entity remains borrowed for the atomic commit");
            let previous_bounds = *bounds;
            *bounds = replacement.debug_bounds;
            render_rollback.push(RenderRollback {
                id: replacement.id,
                asset_id: replacement.asset_id,
                entity: replacement.entity,
                mesh: previous_mesh,
                debug_bounds: previous_bounds,
            });
        }
        let mut collider_rollback = Vec::with_capacity(prepared_colliders.len());
        for replacement in &prepared_colliders {
            let previous = std::mem::replace(
                &mut *collider_components
                    .get_mut(replacement.entity)
                    .expect("validated terrain collider remains borrowed for the atomic commit"),
                replacement.collider.clone(),
            );
            collider_rollback.push(ColliderRollback {
                id: replacement.id,
                entity: replacement.entity,
                collider: previous,
                handle: replacement.handle,
                context_entity: replacement.context_entity,
            });
        }

        let commit = match update.apply_to_cache(&mut state.cache) {
            Ok(commit) => commit,
            Err(error) => {
                for rollback in render_rollback {
                    *meshes
                        .get_mut(rollback.asset_id)
                        .expect("rollback asset was prevalidated") = rollback.mesh;
                    *debug_bounds
                        .get_mut(rollback.entity)
                        .expect("rollback entity was prevalidated") = rollback.debug_bounds;
                }
                for rollback in collider_rollback {
                    *collider_components
                        .get_mut(rollback.entity)
                        .expect("rollback collider was prevalidated") = rollback.collider;
                }
                return Err(TerrainRuntimeUpdateError::Core(error.to_string()));
            }
        };

        let rollback = TerrainRuntimeRollback {
            document: source_document,
            cache_source_document_hash: source_hash,
            cache_hash: source_cache_hash,
            cache_chunks: source_chunks,
            bindings: binding_snapshots,
            render: render_rollback,
            colliders: collider_rollback,
            visual_stats_source_hash: source_visual_stats_hash,
            proof_stats_source_hash: source_proof_stats_hash,
        };
        state.document = commit.target_document().clone();
        let target_hash = commit.target_document_hash();
        let Ok(target_cache_hash) = state.cache.content_hash() else {
            restore_before_backend_sync(
                &rollback,
                &mut state,
                &mut debug_bounds,
                &mut meshes,
                &mut collider_components,
                &mut visual_stats,
                &mut proof_stats,
            );
            return Err(TerrainRuntimeUpdateError::TargetCacheHashUnavailable);
        };
        visual_stats.source_hash = target_hash.to_string();
        proof_stats.source_hash = target_hash.to_string();

        let component_result = verify_committed_components(
            &state.cache,
            &bindings,
            &meshes,
            &mut collider_components,
            commit.mesh_chunks(),
            commit.collider_chunks(),
        );
        let report = TerrainRuntimeUpdateReport {
            sequence,
            source_document_hash: source_hash,
            target_document_hash: target_hash,
            target_cache_hash,
            source_revision,
            target_revision: state.document.revision,
            changed_samples: commit.changed_samples().to_vec(),
            mesh_chunks: commit.mesh_chunks().to_vec(),
            collider_chunks: commit.collider_chunks().to_vec(),
            binding_count: rollback.bindings.len(),
            mesh_assets_verified: if component_result.is_ok() {
                commit.mesh_chunks().len()
            } else {
                0
            },
            collider_components_verified: if component_result.is_ok() {
                commit.collider_chunks().len()
            } else {
                0
            },
            rapier_backends_verified: 0,
        };
        Ok(TerrainBackendProof {
            report,
            colliders: prepared_colliders
                .iter()
                .map(|replacement| ExpectedBackendCollider {
                    id: replacement.id,
                    entity: replacement.entity,
                    handle: replacement.handle,
                    context_entity: replacement.context_entity,
                })
                .collect(),
            component_error: component_result.err(),
            rollback,
        })
    })();

    match result {
        Ok(proof) => pending_backend.0 = Some(TerrainBackendPhase::VerifyTarget(Box::new(proof))),
        Err(error) => outcome.record_rejected(sequence, &error),
    }
}

fn restore_before_backend_sync(
    rollback: &TerrainRuntimeRollback,
    state: &mut TerrainRuntimeState,
    debug_bounds: &mut Query<&mut VisualValleyChunkDebugBounds>,
    meshes: &mut Assets<Mesh>,
    collider_components: &mut Query<&mut Collider>,
    visual_stats: &mut VisualValleyStats,
    proof_stats: &mut ProofSceneStats,
) {
    state.document = rollback.document.clone();
    state.cache.source_document_hash = rollback.cache_source_document_hash;
    for source in &rollback.cache_chunks {
        let destination = state
            .cache
            .chunks
            .iter_mut()
            .find(|chunk| chunk.id == source.id)
            .expect("prevalidated cache chunks remain addressable before backend sync");
        *destination = source.clone();
    }
    for render in &rollback.render {
        *meshes
            .get_mut(render.asset_id)
            .expect("prevalidated mesh assets remain addressable before backend sync") =
            render.mesh.clone();
        *debug_bounds
            .get_mut(render.entity)
            .expect("prevalidated render entities remain addressable before backend sync") =
            render.debug_bounds;
    }
    for source in &rollback.colliders {
        *collider_components
            .get_mut(source.entity)
            .expect("prevalidated colliders remain addressable before backend sync") =
            source.collider.clone();
    }
    visual_stats
        .source_hash
        .clone_from(&rollback.visual_stats_source_hash);
    proof_stats
        .source_hash
        .clone_from(&rollback.proof_stats_source_hash);
}

fn verify_committed_components(
    cache: &GeneratedTerrain,
    bindings: &TerrainRuntimeBindings,
    meshes: &Assets<Mesh>,
    collider_components: &mut Query<&mut Collider>,
    mesh_chunks: &[TerrainDirtyChunk],
    collider_chunks: &[TerrainDirtyChunk],
) -> Result<(), TerrainRuntimeUpdateError> {
    for dirty in mesh_chunks {
        let canonical = cache
            .chunks
            .iter()
            .find(|chunk| chunk.id == dirty.id)
            .ok_or(TerrainRuntimeUpdateError::MissingReplacement(dirty.id))?;
        let binding = bindings.binding(dirty.id)?;
        let handle = binding
            .mesh_handle
            .as_ref()
            .ok_or(TerrainRuntimeUpdateError::IncompleteBinding(dirty.id))?;
        let mesh = meshes
            .get(handle.id())
            .ok_or(TerrainRuntimeUpdateError::MissingMeshAsset(dirty.id))?;
        verify_mesh_projection(mesh, canonical).map_err(|reason| {
            TerrainRuntimeUpdateError::InvalidMeshProjection {
                id: dirty.id,
                reason,
            }
        })?;
    }

    for dirty in collider_chunks {
        let canonical = cache
            .chunks
            .iter()
            .find(|chunk| chunk.id == dirty.id)
            .ok_or(TerrainRuntimeUpdateError::MissingReplacement(dirty.id))?;
        let binding = bindings.binding(dirty.id)?;
        let entity = binding
            .collider_entity
            .ok_or(TerrainRuntimeUpdateError::IncompleteBinding(dirty.id))?;
        let collider = collider_components
            .get_mut(entity)
            .map_err(|_| TerrainRuntimeUpdateError::MissingColliderEntity(dirty.id))?;
        verify_collider_projection(&collider, canonical).map_err(|reason| {
            TerrainRuntimeUpdateError::InvalidColliderProjection {
                id: dirty.id,
                reason,
            }
        })?;
    }
    Ok(())
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    clippy::too_many_lines,
    reason = "Bevy systems require owned wrappers; verification and rollback stay contiguous so no partial outcome can be published"
)]
fn verify_pending_terrain_backend(
    mut pending_backend: ResMut<PendingTerrainBackendProof>,
    mut outcome: ResMut<TerrainRuntimeUpdateOutcome>,
    mut state: ResMut<TerrainRuntimeState>,
    bindings: Res<TerrainRuntimeBindings>,
    render_entities: Query<&Mesh3d>,
    mut debug_bounds: Query<&mut VisualValleyChunkDebugBounds>,
    mut meshes: ResMut<Assets<Mesh>>,
    mut collider_entities: Query<(
        &TerrainHeightfieldCollider,
        &mut Collider,
        &RapierColliderHandle,
        &mut RapierContextEntityLink,
    )>,
    contexts: Query<&RapierContextColliders>,
    mut visual_stats: ResMut<VisualValleyStats>,
    mut proof_stats: ResMut<ProofSceneStats>,
) {
    let Some(phase) = pending_backend.0.take() else {
        return;
    };
    match phase {
        TerrainBackendPhase::VerifyTarget(mut proof) => {
            let target_result = if let Some(error) = proof.component_error.take() {
                Err(error)
            } else {
                (|| {
                    verify_target_render_projection(
                        &proof,
                        &state,
                        &bindings,
                        &render_entities,
                        &mut debug_bounds,
                        &meshes,
                    )?;
                    verify_target_backend(&mut proof, &state, &mut collider_entities, &contexts)
                })()
            };
            match target_result {
                Ok(()) => {
                    let report = proof.report;
                    info!(
                        target: "forge::terrain",
                        schema = "forge.runtime-event",
                        schema_version = 1_u64,
                        event_kind = "terrain_partial_update_backend_verified",
                        sequence = report.sequence(),
                        source_hash = %report.source_document_hash(),
                        target_hash = %report.target_document_hash(),
                        target_cache_hash = %report.target_cache_hash(),
                        mesh_chunks = report.mesh_chunks().len(),
                        collider_chunks = report.collider_chunks().len(),
                        rapier_backends = report.rapier_backends_verified(),
                        "terrain partial update is bit-exact in CPU assets, ECS and Rapier"
                    );
                    outcome.record_applied(report);
                }
                Err(original_error) => {
                    let sequence = proof.report.sequence;
                    let attempted_target_cache_hash = proof.report.target_cache_hash;
                    let rollback_result = restore_runtime_projection(
                        &proof.rollback,
                        &mut state,
                        &bindings,
                        &render_entities,
                        &mut debug_bounds,
                        &mut meshes,
                        &mut collider_entities,
                        &mut visual_stats,
                        &mut proof_stats,
                    );
                    match rollback_result {
                        Ok(()) => {
                            pending_backend.0 = Some(TerrainBackendPhase::VerifyRollback(
                                Box::new(TerrainRollbackProof {
                                    sequence,
                                    attempted_target_cache_hash,
                                    original_error,
                                    rollback: proof.rollback,
                                }),
                            ));
                        }
                        Err(rollback_error) => {
                            let combined_error =
                                TerrainRuntimeUpdateError::RollbackVerificationFailed {
                                    original: original_error.to_string(),
                                    rollback: rollback_error.to_string(),
                                };
                            outcome.record_projection_failure(
                                sequence,
                                attempted_target_cache_hash,
                                &combined_error,
                                false,
                            );
                            pending_backend.0 = Some(TerrainBackendPhase::Poisoned);
                        }
                    }
                }
            }
        }
        TerrainBackendPhase::VerifyRollback(proof) => {
            match verify_runtime_rollback(
                &proof.rollback,
                &state,
                &bindings,
                &render_entities,
                &mut debug_bounds,
                &meshes,
                &mut collider_entities,
                &contexts,
                &visual_stats,
                &proof_stats,
            ) {
                Ok(()) => outcome.record_projection_failure(
                    proof.sequence,
                    proof.attempted_target_cache_hash,
                    &proof.original_error,
                    true,
                ),
                Err(rollback_error) => {
                    let combined_error = TerrainRuntimeUpdateError::RollbackVerificationFailed {
                        original: proof.original_error.to_string(),
                        rollback: rollback_error.to_string(),
                    };
                    outcome.record_projection_failure(
                        proof.sequence,
                        proof.attempted_target_cache_hash,
                        &combined_error,
                        false,
                    );
                    pending_backend.0 = Some(TerrainBackendPhase::Poisoned);
                }
            }
        }
        TerrainBackendPhase::Poisoned => {
            pending_backend.0 = Some(TerrainBackendPhase::Poisoned);
        }
    }
}

fn verify_target_render_projection(
    proof: &TerrainBackendProof,
    state: &TerrainRuntimeState,
    bindings: &TerrainRuntimeBindings,
    render_entities: &Query<&Mesh3d>,
    debug_bounds: &mut Query<&mut VisualValleyChunkDebugBounds>,
    meshes: &Assets<Mesh>,
) -> Result<(), TerrainRuntimeUpdateError> {
    if bindings.snapshots()? != proof.rollback.bindings {
        return Err(TerrainRuntimeUpdateError::BindingsChangedDuringProof);
    }
    for dirty in proof.report.mesh_chunks() {
        let canonical = state
            .cache
            .chunks
            .iter()
            .find(|chunk| chunk.id == dirty.id)
            .ok_or(TerrainRuntimeUpdateError::MissingReplacement(dirty.id))?;
        let binding = bindings.binding(dirty.id)?;
        let entity = binding
            .render_entity
            .ok_or(TerrainRuntimeUpdateError::IncompleteBinding(dirty.id))?;
        let handle = binding
            .mesh_handle
            .as_ref()
            .ok_or(TerrainRuntimeUpdateError::IncompleteBinding(dirty.id))?;
        let entity_mesh = render_entities
            .get(entity)
            .map_err(|_| TerrainRuntimeUpdateError::MissingRenderEntity(dirty.id))?;
        if entity_mesh.0.id() != handle.id() {
            return Err(TerrainRuntimeUpdateError::MeshHandleMismatch(dirty.id));
        }
        let mesh = meshes
            .get(handle.id())
            .ok_or(TerrainRuntimeUpdateError::MissingMeshAsset(dirty.id))?;
        verify_mesh_projection(mesh, canonical).map_err(|reason| {
            TerrainRuntimeUpdateError::InvalidMeshProjection {
                id: dirty.id,
                reason,
            }
        })?;
        let (_, expected_bounds) = build_bevy_terrain_mesh(canonical)
            .map_err(|error| TerrainRuntimeUpdateError::Adapter(error.to_string()))?;
        if *debug_bounds
            .get_mut(entity)
            .map_err(|_| TerrainRuntimeUpdateError::MissingRenderEntity(dirty.id))?
            != expected_bounds
        {
            return Err(TerrainRuntimeUpdateError::InvalidMeshProjection {
                id: dirty.id,
                reason: "debug bounds differ from the canonical target chunk",
            });
        }
    }
    Ok(())
}

fn verify_target_backend(
    proof: &mut TerrainBackendProof,
    state: &TerrainRuntimeState,
    collider_entities: &mut Query<(
        &TerrainHeightfieldCollider,
        &mut Collider,
        &RapierColliderHandle,
        &mut RapierContextEntityLink,
    )>,
    contexts: &Query<&RapierContextColliders>,
) -> Result<(), TerrainRuntimeUpdateError> {
    let observed_hash = state
        .cache
        .content_hash()
        .map_err(|_| TerrainRuntimeUpdateError::TargetCacheHashUnavailable)?;
    if observed_hash != proof.report.target_cache_hash {
        return Err(TerrainRuntimeUpdateError::TargetCacheChangedBeforeProof);
    }

    for expected in &proof.colliders {
        let (marker, component, handle, context_link) = collider_entities
            .get_mut(expected.entity)
            .map_err(|_| TerrainRuntimeUpdateError::MissingColliderEntity(expected.id))?;
        if marker.id != expected.id {
            return Err(TerrainRuntimeUpdateError::ColliderIdentityMismatch(
                expected.id,
            ));
        }
        if handle.0 != expected.handle.0 {
            return Err(TerrainRuntimeUpdateError::RapierHandleChanged(expected.id));
        }
        if context_link.0 != expected.context_entity {
            return Err(TerrainRuntimeUpdateError::RapierContextChanged(expected.id));
        }
        let canonical = state
            .cache
            .chunks
            .iter()
            .find(|chunk| chunk.id == expected.id)
            .ok_or(TerrainRuntimeUpdateError::MissingReplacement(expected.id))?;
        verify_collider_projection(&component, canonical).map_err(|reason| {
            TerrainRuntimeUpdateError::InvalidColliderProjection {
                id: expected.id,
                reason,
            }
        })?;
        verify_backend_projection(
            expected.id,
            expected.handle,
            expected.context_entity,
            canonical,
            contexts,
        )?;
    }
    proof.report.rapier_backends_verified = proof.colliders.len();
    Ok(())
}

#[allow(
    clippy::too_many_arguments,
    reason = "rollback validates every borrowed projection before restoring the atomic snapshot"
)]
fn restore_runtime_projection(
    rollback: &TerrainRuntimeRollback,
    state: &mut TerrainRuntimeState,
    bindings: &TerrainRuntimeBindings,
    render_entities: &Query<&Mesh3d>,
    debug_bounds: &mut Query<&mut VisualValleyChunkDebugBounds>,
    meshes: &mut Assets<Mesh>,
    collider_entities: &mut Query<(
        &TerrainHeightfieldCollider,
        &mut Collider,
        &RapierColliderHandle,
        &mut RapierContextEntityLink,
    )>,
    visual_stats: &mut VisualValleyStats,
    proof_stats: &mut ProofSceneStats,
) -> Result<(), TerrainRuntimeUpdateError> {
    if bindings.snapshots()? != rollback.bindings {
        return Err(TerrainRuntimeUpdateError::BindingsChangedDuringProof);
    }
    for render in &rollback.render {
        let entity_mesh = render_entities
            .get(render.entity)
            .map_err(|_| TerrainRuntimeUpdateError::MissingRenderEntity(render.id))?;
        if entity_mesh.0.id() != render.asset_id || meshes.get(render.asset_id).is_none() {
            return Err(TerrainRuntimeUpdateError::MissingMeshAsset(render.id));
        }
        debug_bounds
            .get_mut(render.entity)
            .map_err(|_| TerrainRuntimeUpdateError::MissingRenderEntity(render.id))?;
    }
    for collider in &rollback.colliders {
        let (marker, _, handle, _) = collider_entities
            .get_mut(collider.entity)
            .map_err(|_| TerrainRuntimeUpdateError::MissingColliderEntity(collider.id))?;
        if marker.id != collider.id || handle.0 != collider.handle.0 {
            return Err(TerrainRuntimeUpdateError::RapierHandleChanged(collider.id));
        }
    }
    for chunk in &rollback.cache_chunks {
        if !state
            .cache
            .chunks
            .iter()
            .any(|current| current.id == chunk.id)
        {
            return Err(TerrainRuntimeUpdateError::MissingReplacement(chunk.id));
        }
    }

    state.document = rollback.document.clone();
    state.cache.source_document_hash = rollback.cache_source_document_hash;
    for source in &rollback.cache_chunks {
        let destination = state
            .cache
            .chunks
            .iter_mut()
            .find(|chunk| chunk.id == source.id)
            .expect("rollback cache destinations were prevalidated");
        *destination = source.clone();
    }
    for render in &rollback.render {
        *meshes
            .get_mut(render.asset_id)
            .expect("rollback mesh assets were prevalidated") = render.mesh.clone();
        *debug_bounds
            .get_mut(render.entity)
            .expect("rollback render entities were prevalidated") = render.debug_bounds;
    }
    for source in &rollback.colliders {
        let (_, mut collider, _, mut context_link) = collider_entities
            .get_mut(source.entity)
            .expect("rollback collider entities were prevalidated");
        *collider = source.collider.clone();
        context_link.0 = source.context_entity;
    }
    visual_stats
        .source_hash
        .clone_from(&rollback.visual_stats_source_hash);
    proof_stats
        .source_hash
        .clone_from(&rollback.proof_stats_source_hash);
    Ok(())
}

#[allow(
    clippy::too_many_arguments,
    reason = "rollback proof covers source, bindings, assets, ECS colliders, context and backend"
)]
fn verify_runtime_rollback(
    rollback: &TerrainRuntimeRollback,
    state: &TerrainRuntimeState,
    bindings: &TerrainRuntimeBindings,
    render_entities: &Query<&Mesh3d>,
    debug_bounds: &mut Query<&mut VisualValleyChunkDebugBounds>,
    meshes: &Assets<Mesh>,
    collider_entities: &mut Query<(
        &TerrainHeightfieldCollider,
        &mut Collider,
        &RapierColliderHandle,
        &mut RapierContextEntityLink,
    )>,
    contexts: &Query<&RapierContextColliders>,
    visual_stats: &VisualValleyStats,
    proof_stats: &ProofSceneStats,
) -> Result<(), TerrainRuntimeUpdateError> {
    if state.document != rollback.document
        || state.cache.source_document_hash != rollback.cache_source_document_hash
        || state
            .cache
            .content_hash()
            .map_err(|_| TerrainRuntimeUpdateError::TargetCacheHashUnavailable)?
            != rollback.cache_hash
    {
        return Err(TerrainRuntimeUpdateError::RollbackStateMismatch(
            "document or cache differs from the source snapshot",
        ));
    }
    if bindings.snapshots()? != rollback.bindings {
        return Err(TerrainRuntimeUpdateError::BindingsChangedDuringProof);
    }
    if visual_stats.source_hash != rollback.visual_stats_source_hash
        || proof_stats.source_hash != rollback.proof_stats_source_hash
    {
        return Err(TerrainRuntimeUpdateError::RollbackStateMismatch(
            "runtime statistics differ from the source snapshot",
        ));
    }
    for render in &rollback.render {
        let entity_mesh = render_entities
            .get(render.entity)
            .map_err(|_| TerrainRuntimeUpdateError::MissingRenderEntity(render.id))?;
        if entity_mesh.0.id() != render.asset_id {
            return Err(TerrainRuntimeUpdateError::MeshHandleMismatch(render.id));
        }
        let mesh = meshes
            .get(render.asset_id)
            .ok_or(TerrainRuntimeUpdateError::MissingMeshAsset(render.id))?;
        let canonical = rollback_chunk(rollback, render.id)?;
        verify_mesh_projection(mesh, canonical).map_err(|reason| {
            TerrainRuntimeUpdateError::InvalidMeshProjection {
                id: render.id,
                reason,
            }
        })?;
        if *debug_bounds
            .get_mut(render.entity)
            .map_err(|_| TerrainRuntimeUpdateError::MissingRenderEntity(render.id))?
            != render.debug_bounds
        {
            return Err(TerrainRuntimeUpdateError::RollbackStateMismatch(
                "terrain debug bounds differ from the source snapshot",
            ));
        }
    }
    for expected in &rollback.colliders {
        let (marker, collider, handle, context_link) =
            collider_entities
                .get_mut(expected.entity)
                .map_err(|_| TerrainRuntimeUpdateError::MissingColliderEntity(expected.id))?;
        if marker.id != expected.id {
            return Err(TerrainRuntimeUpdateError::ColliderIdentityMismatch(
                expected.id,
            ));
        }
        if handle.0 != expected.handle.0 {
            return Err(TerrainRuntimeUpdateError::RapierHandleChanged(expected.id));
        }
        if context_link.0 != expected.context_entity {
            return Err(TerrainRuntimeUpdateError::RapierContextChanged(expected.id));
        }
        let canonical = rollback_chunk(rollback, expected.id)?;
        verify_collider_projection(&collider, canonical).map_err(|reason| {
            TerrainRuntimeUpdateError::InvalidColliderProjection {
                id: expected.id,
                reason,
            }
        })?;
        verify_backend_projection(
            expected.id,
            expected.handle,
            expected.context_entity,
            canonical,
            contexts,
        )?;
    }
    Ok(())
}

fn rollback_chunk(
    rollback: &TerrainRuntimeRollback,
    id: TerrainChunkId,
) -> Result<&TerrainChunkMesh, TerrainRuntimeUpdateError> {
    rollback
        .cache_chunks
        .iter()
        .find(|chunk| chunk.id == id)
        .ok_or(TerrainRuntimeUpdateError::MissingReplacement(id))
}

fn verify_backend_projection(
    id: TerrainChunkId,
    handle: RapierColliderHandle,
    context_entity: Entity,
    canonical: &TerrainChunkMesh,
    contexts: &Query<&RapierContextColliders>,
) -> Result<(), TerrainRuntimeUpdateError> {
    let context = contexts
        .get(context_entity)
        .map_err(|_| TerrainRuntimeUpdateError::MissingRapierContext(id))?;
    let backend = context
        .colliders
        .get(handle.0)
        .ok_or(TerrainRuntimeUpdateError::MissingRapierBackend(id))?;
    let heightfield = backend.shape().as_heightfield().ok_or(
        TerrainRuntimeUpdateError::InvalidColliderProjection {
            id,
            reason: "Rapier backend shape is not a heightfield",
        },
    )?;
    verify_heightfield_projection(heightfield, canonical)
        .map_err(|reason| TerrainRuntimeUpdateError::InvalidColliderProjection { id, reason })
}

fn verify_mesh_projection(mesh: &Mesh, canonical: &TerrainChunkMesh) -> Result<(), &'static str> {
    if mesh.primitive_topology() != PrimitiveTopology::TriangleList {
        return Err("primitive topology differs from the canonical chunk");
    }
    if mesh.asset_usage != (RenderAssetUsages::MAIN_WORLD | RenderAssetUsages::RENDER_WORLD) {
        return Err("render asset usage differs from the canonical adapter contract");
    }
    match mesh.attribute(Mesh::ATTRIBUTE_POSITION) {
        Some(VertexAttributeValues::Float32x3(values))
            if float3_bits_equal(values, &canonical.positions_m) => {}
        _ => return Err("POSITION values differ bit-for-bit"),
    }
    match mesh.attribute(Mesh::ATTRIBUTE_NORMAL) {
        Some(VertexAttributeValues::Float32x3(values))
            if float3_bits_equal(values, &canonical.normals) => {}
        _ => return Err("NORMAL values differ bit-for-bit"),
    }
    match mesh.attribute(Mesh::ATTRIBUTE_UV_0) {
        Some(VertexAttributeValues::Float32x2(values))
            if float2_bits_equal(values, &canonical.uvs) => {}
        _ => return Err("UV_0 values differ bit-for-bit"),
    }
    let expected_colors = canonical
        .material_weights
        .iter()
        .map(|weights| [weights.grass, weights.earth, weights.rock, 1.0])
        .collect::<Vec<_>>();
    match mesh.attribute(Mesh::ATTRIBUTE_COLOR) {
        Some(VertexAttributeValues::Float32x4(values))
            if float4_bits_equal(values, &expected_colors) => {}
        _ => return Err("COLOR material weights differ bit-for-bit"),
    }
    match mesh.indices() {
        Some(Indices::U32(values)) if values == &canonical.indices => {}
        _ => return Err("indices differ from the canonical chunk"),
    }
    Ok(())
}

fn verify_collider_projection(
    collider: &Collider,
    canonical: &TerrainChunkMesh,
) -> Result<(), &'static str> {
    let heightfield = collider
        .as_heightfield()
        .ok_or("ECS collider is not a heightfield")?;
    verify_heightfield_projection(heightfield.raw, canonical)
}

fn verify_heightfield_projection(
    heightfield: &bevy_rapier3d::parry::shape::HeightField,
    canonical: &TerrainChunkMesh,
) -> Result<(), &'static str> {
    if heightfield.heights().nrows() != usize::from(TERRAIN_CHUNK_VERTICES_PER_AXIS)
        || heightfield.heights().ncols() != usize::from(TERRAIN_CHUNK_VERTICES_PER_AXIS)
    {
        return Err("heightfield dimensions differ from the canonical chunk");
    }
    let expected = (0..usize::from(TERRAIN_CHUNK_VERTICES_PER_AXIS)).flat_map(|x| {
        (0..usize::from(TERRAIN_CHUNK_VERTICES_PER_AXIS)).map(move |z| {
            canonical.collider.vertices_m[z * usize::from(TERRAIN_CHUNK_VERTICES_PER_AXIS) + x][1]
        })
    });
    if !heightfield
        .heights()
        .data()
        .iter()
        .copied()
        .zip(expected)
        .all(|(actual, expected)| actual.to_bits() == expected.to_bits())
    {
        return Err("heightfield values differ bit-for-bit");
    }
    let scale = heightfield.scale();
    if scale.x.to_bits() != 64.0_f32.to_bits()
        || scale.y.to_bits() != 1.0_f32.to_bits()
        || scale.z.to_bits() != 64.0_f32.to_bits()
    {
        return Err("heightfield scale differs bit-for-bit");
    }
    Ok(())
}

fn float2_bits_equal(left: &[[f32; 2]], right: &[[f32; 2]]) -> bool {
    left.len() == right.len()
        && left.iter().zip(right).all(|(left, right)| {
            left.iter()
                .zip(right)
                .all(|(left, right)| left.to_bits() == right.to_bits())
        })
}

fn float3_bits_equal(left: &[[f32; 3]], right: &[[f32; 3]]) -> bool {
    left.len() == right.len()
        && left.iter().zip(right).all(|(left, right)| {
            left.iter()
                .zip(right)
                .all(|(left, right)| left.to_bits() == right.to_bits())
        })
}

fn float4_bits_equal(left: &[[f32; 4]], right: &[[f32; 4]]) -> bool {
    left.len() == right.len()
        && left.iter().zip(right).all(|(left, right)| {
            left.iter()
                .zip(right)
                .all(|(left, right)| left.to_bits() == right.to_bits())
        })
}

/// Failures observed before a runtime terrain transaction mutates any source or projection.
#[derive(Debug)]
pub enum TerrainRuntimeUpdateError {
    Core(String),
    Adapter(String),
    IncompleteBindings {
        expected: usize,
        actual: usize,
    },
    MissingBinding(TerrainChunkId),
    IncompleteBinding(TerrainChunkId),
    MissingRenderEntity(TerrainChunkId),
    MissingColliderEntity(TerrainChunkId),
    MissingMeshAsset(TerrainChunkId),
    MeshHandleMismatch(TerrainChunkId),
    MissingReplacement(TerrainChunkId),
    MissingRapierHandle(TerrainChunkId),
    MissingRapierContext(TerrainChunkId),
    MissingRapierBackend(TerrainChunkId),
    RapierHandleChanged(TerrainChunkId),
    RapierContextChanged(TerrainChunkId),
    ColliderIdentityMismatch(TerrainChunkId),
    InvalidMeshProjection {
        id: TerrainChunkId,
        reason: &'static str,
    },
    InvalidColliderProjection {
        id: TerrainChunkId,
        reason: &'static str,
    },
    TargetCacheHashUnavailable,
    TargetCacheChangedBeforeProof,
    BindingsChangedDuringProof,
    RollbackStateMismatch(&'static str),
    RollbackVerificationFailed {
        original: String,
        rollback: String,
    },
}

impl fmt::Display for TerrainRuntimeUpdateError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Core(error) => write!(formatter, "terrain core rejected the update: {error}"),
            Self::Adapter(error) => {
                write!(formatter, "terrain adapter rejected the update: {error}")
            }
            Self::IncompleteBindings { expected, actual } => write!(
                formatter,
                "terrain runtime expected {expected} complete bindings, found {actual}"
            ),
            Self::MissingBinding(id) => write!(formatter, "terrain binding {id} is missing"),
            Self::IncompleteBinding(id) => write!(formatter, "terrain binding {id} is incomplete"),
            Self::MissingRenderEntity(id) => {
                write!(formatter, "terrain render entity {id} is missing")
            }
            Self::MissingColliderEntity(id) => {
                write!(formatter, "terrain collider entity {id} is missing")
            }
            Self::MissingMeshAsset(id) => write!(formatter, "terrain mesh asset {id} is missing"),
            Self::MeshHandleMismatch(id) => {
                write!(
                    formatter,
                    "terrain mesh handle {id} no longer matches its binding"
                )
            }
            Self::MissingReplacement(id) => {
                write!(formatter, "terrain replacement payload {id} is missing")
            }
            Self::MissingRapierHandle(id) => {
                write!(formatter, "terrain collider {id} has no Rapier handle")
            }
            Self::MissingRapierContext(id) => {
                write!(
                    formatter,
                    "terrain collider {id} has no live Rapier context"
                )
            }
            Self::MissingRapierBackend(id) => {
                write!(
                    formatter,
                    "terrain collider {id} has no Rapier backend shape"
                )
            }
            Self::RapierHandleChanged(id) => {
                write!(
                    formatter,
                    "terrain collider {id} replaced its Rapier handle"
                )
            }
            Self::RapierContextChanged(id) => {
                write!(formatter, "terrain collider {id} changed Rapier context")
            }
            Self::ColliderIdentityMismatch(id) => {
                write!(
                    formatter,
                    "terrain collider entity no longer represents {id}"
                )
            }
            Self::InvalidMeshProjection { id, reason } => {
                write!(
                    formatter,
                    "terrain mesh projection {id} is invalid: {reason}"
                )
            }
            Self::InvalidColliderProjection { id, reason } => {
                write!(
                    formatter,
                    "terrain collider projection {id} is invalid: {reason}"
                )
            }
            Self::TargetCacheHashUnavailable => {
                formatter.write_str("target terrain cache hash could not be computed")
            }
            Self::TargetCacheChangedBeforeProof => {
                formatter.write_str("target terrain cache changed before backend verification")
            }
            Self::BindingsChangedDuringProof => {
                formatter.write_str("terrain runtime bindings changed during projection proof")
            }
            Self::RollbackStateMismatch(reason) => {
                write!(formatter, "terrain rollback state is invalid: {reason}")
            }
            Self::RollbackVerificationFailed { original, rollback } => {
                write!(
                    formatter,
                    "terrain projection failed ({original}) and rollback verification failed ({rollback})"
                )
            }
        }
    }
}

impl Error for TerrainRuntimeUpdateError {}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{mesh::VertexAttributeValues, transform::TransformPlugin};
    use bevy_rapier3d::{
        parry::{
            math::Vector,
            query::{Ray, RayCast},
        },
        plugin::context::{DefaultRapierContext, RapierContextColliders},
        prelude::{NoUserData, RapierColliderHandle, RapierPhysicsPlugin, RigidBody, TimestepMode},
    };
    use forge_project::TerrainHeightDelta;
    use forge_world::VisualValleyTerrainRecipe;

    fn build_test_app(with_rapier: bool) -> App {
        let document = VisualValleyTerrainRecipe::default().build().unwrap();
        let plan = TerrainGenerationPlan::from_document(&document).unwrap();
        let cache = plan.generate().unwrap();
        let source_hash = cache.source_document_hash.to_string();

        let mut app = App::new();
        app.add_plugins((MinimalPlugins, TransformPlugin));
        if with_rapier {
            app.insert_resource(Time::<Fixed>::from_hz(60.0))
                .insert_resource(TimestepMode::Fixed {
                    dt: 1.0 / 60.0,
                    substeps: 1,
                })
                .add_plugins(RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule());
        }
        app.init_resource::<Assets<Mesh>>()
            .init_resource::<TerrainRuntimeBindings>()
            .init_resource::<PendingTerrainPartialUpdate>()
            .init_resource::<PendingTerrainBackendProof>()
            .init_resource::<TerrainRuntimeUpdateOutcome>()
            .insert_resource(VisualValleyStats {
                source_hash: source_hash.clone(),
                chunks: TERRAIN_CHUNK_COUNT,
                ..default()
            })
            .insert_resource(ProofSceneStats {
                source_entities: 1,
                source_mesh_instances: TERRAIN_CHUNK_COUNT,
                source_hash,
            });
        if with_rapier {
            app.configure_sets(
                FixedUpdate,
                TerrainRuntimeUpdateSet::Apply.before(PhysicsSet::SyncBackend),
            )
            .configure_sets(
                FixedUpdate,
                TerrainRuntimeUpdateSet::Verify.after(PhysicsSet::Writeback),
            )
            .add_systems(
                FixedUpdate,
                apply_pending_terrain_partial_update.in_set(TerrainRuntimeUpdateSet::Apply),
            )
            .add_systems(
                FixedUpdate,
                verify_pending_terrain_backend.in_set(TerrainRuntimeUpdateSet::Verify),
            );
        } else {
            app.add_systems(Update, apply_pending_terrain_partial_update);
        }

        for chunk in &cache.chunks {
            let (mesh, bounds) = build_bevy_terrain_mesh(chunk).unwrap();
            let mesh_handle = app.world_mut().resource_mut::<Assets<Mesh>>().add(mesh);
            let render_entity = app
                .world_mut()
                .spawn((Mesh3d(mesh_handle.clone()), bounds))
                .id();
            let collider = build_terrain_heightfield_collider(chunk).unwrap();
            let collider_entity = app
                .world_mut()
                .spawn((
                    TerrainHeightfieldCollider {
                        id: chunk.id,
                        coordinate: chunk.coordinate,
                    },
                    collider,
                    RigidBody::Fixed,
                    Transform::IDENTITY,
                    GlobalTransform::IDENTITY,
                ))
                .id();
            app.world_mut()
                .resource_mut::<TerrainRuntimeBindings>()
                .register_render(chunk.id, chunk.coordinate, render_entity, mesh_handle);
            app.world_mut()
                .resource_mut::<TerrainRuntimeBindings>()
                .register_collider(chunk.id, chunk.coordinate, collider_entity);
        }
        app.insert_resource(TerrainRuntimeState::new(document, cache));
        app
    }

    fn request_for_current_state(app: &App) -> (TerrainDocument, TerrainPartialUpdateRequest) {
        let state = app.world().resource::<TerrainRuntimeState>();
        let source_plan = TerrainGenerationPlan::from_document(state.document()).unwrap();
        let mut target_document = state.document().migrate_to_latest().unwrap();
        target_document.revision = state.document().revision.next().unwrap();
        target_document.height_deltas = vec![TerrainHeightDelta::new(31, 31, 5.0)];
        let request = TerrainPartialUpdateRequest {
            base_revision: state.document().revision,
            base_document_hash: source_plan.document_hash(),
            target_document: target_document.clone(),
        };
        (target_document, request)
    }

    fn snapshots(app: &App) -> Vec<TerrainRuntimeBindingSnapshot> {
        app.world()
            .resource::<TerrainRuntimeBindings>()
            .snapshots()
            .unwrap()
    }

    fn mesh_payload_bits(
        app: &App,
        bindings: &[TerrainRuntimeBindingSnapshot],
    ) -> BTreeMap<TerrainChunkId, Vec<u32>> {
        let meshes = app.world().resource::<Assets<Mesh>>();
        bindings
            .iter()
            .map(|binding| {
                let mesh = meshes.get(binding.mesh_asset_id).unwrap();
                let Some(VertexAttributeValues::Float32x3(positions)) =
                    mesh.attribute(Mesh::ATTRIBUTE_POSITION)
                else {
                    panic!("terrain test meshes must retain Float32x3 positions");
                };
                let Some(VertexAttributeValues::Float32x3(normals)) =
                    mesh.attribute(Mesh::ATTRIBUTE_NORMAL)
                else {
                    panic!("terrain test meshes must retain Float32x3 normals");
                };
                let Some(VertexAttributeValues::Float32x4(colors)) =
                    mesh.attribute(Mesh::ATTRIBUTE_COLOR)
                else {
                    panic!("terrain test meshes must retain Float32x4 material weights");
                };
                let payload = positions
                    .iter()
                    .flat_map(|position| position.iter().copied())
                    .chain(normals.iter().flat_map(|normal| normal.iter().copied()))
                    .chain(colors.iter().flat_map(|color| color.iter().copied()))
                    .map(f32::to_bits)
                    .collect();
                (binding.id, payload)
            })
            .collect()
    }

    fn collider_height_bits(
        app: &App,
        bindings: &[TerrainRuntimeBindingSnapshot],
    ) -> BTreeMap<TerrainChunkId, Vec<u32>> {
        bindings
            .iter()
            .map(|binding| {
                let collider = app
                    .world()
                    .get::<Collider>(binding.collider_entity)
                    .unwrap();
                let heightfield = collider.as_heightfield().unwrap();
                (
                    binding.id,
                    heightfield
                        .raw
                        .heights()
                        .data()
                        .iter()
                        .copied()
                        .map(f32::to_bits)
                        .collect(),
                )
            })
            .collect()
    }

    #[derive(Resource)]
    struct CorruptTargetMeshOnce(bool);

    #[allow(
        clippy::needless_pass_by_value,
        reason = "Bevy test systems require owned system-parameter wrappers"
    )]
    fn corrupt_target_mesh_once(
        mut trigger: ResMut<CorruptTargetMeshOnce>,
        pending: Res<PendingTerrainBackendProof>,
        mut meshes: ResMut<Assets<Mesh>>,
    ) {
        if !trigger.0 {
            return;
        }
        let Some(TerrainBackendPhase::VerifyTarget(proof)) = pending.0.as_ref() else {
            return;
        };
        let render = proof
            .rollback
            .render
            .first()
            .expect("the oracle always changes three render chunks");
        *meshes
            .get_mut(render.asset_id)
            .expect("the target mesh exists before injected corruption") = Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::MAIN_WORLD | RenderAssetUsages::RENDER_WORLD,
        );
        trigger.0 = false;
    }

    #[derive(Resource)]
    struct SabotageRollbackOnce(bool);

    #[allow(
        clippy::needless_pass_by_value,
        reason = "Bevy test systems require owned system-parameter wrappers"
    )]
    fn sabotage_rollback_once(
        mut trigger: ResMut<SabotageRollbackOnce>,
        pending: Res<PendingTerrainBackendProof>,
        mut meshes: ResMut<Assets<Mesh>>,
    ) {
        if !trigger.0 {
            return;
        }
        let Some(TerrainBackendPhase::VerifyTarget(proof)) = pending.0.as_ref() else {
            return;
        };
        let render = proof
            .rollback
            .render
            .first()
            .expect("the oracle always changes three render chunks");
        meshes
            .remove(render.asset_id)
            .expect("the target mesh exists before injected rollback sabotage");
        trigger.0 = false;
    }

    #[derive(Resource)]
    struct ChangeTargetRapierContextOnce(bool);

    #[allow(
        clippy::needless_pass_by_value,
        reason = "Bevy test systems require owned system-parameter wrappers"
    )]
    fn change_target_rapier_context_once(
        mut trigger: ResMut<ChangeTargetRapierContextOnce>,
        pending: Res<PendingTerrainBackendProof>,
        mut context_links: Query<&mut RapierContextEntityLink>,
    ) {
        if !trigger.0 {
            return;
        }
        let Some(TerrainBackendPhase::VerifyTarget(proof)) = pending.0.as_ref() else {
            return;
        };
        let expected = proof
            .colliders
            .first()
            .expect("the oracle always changes one collider");
        context_links
            .get_mut(expected.entity)
            .expect("the target collider retains its context link")
            .0 = Entity::PLACEHOLDER;
        trigger.0 = false;
    }

    struct SourceRuntimeSnapshot {
        document: TerrainDocument,
        cache: GeneratedTerrain,
        bindings: Vec<TerrainRuntimeBindingSnapshot>,
        meshes: BTreeMap<TerrainChunkId, Vec<u32>>,
        colliders: BTreeMap<TerrainChunkId, Vec<u32>>,
        visual_stats_source_hash: String,
        proof_stats_source_hash: String,
        collider_binding: TerrainRuntimeBindingSnapshot,
        collider_handle: RapierColliderHandle,
        context_entity: Entity,
        backend_height: f32,
    }

    fn snapshot_source_runtime(app: &mut App) -> SourceRuntimeSnapshot {
        const LOCAL_SAMPLE_METRES: f32 = 30.0;

        let bindings = snapshots(app);
        let collider_binding = bindings
            .iter()
            .find(|binding| binding.coordinate == TerrainChunkCoordinate { x: 0, z: 0 })
            .copied()
            .unwrap();
        let collider_handle = *app
            .world()
            .get::<RapierColliderHandle>(collider_binding.collider_entity)
            .unwrap();
        let context_entity = app
            .world()
            .get::<RapierContextEntityLink>(collider_binding.collider_entity)
            .unwrap()
            .0;
        let backend_height = backend_raycast_height(app, collider_handle, LOCAL_SAMPLE_METRES);
        SourceRuntimeSnapshot {
            document: app
                .world()
                .resource::<TerrainRuntimeState>()
                .document()
                .clone(),
            cache: app
                .world()
                .resource::<TerrainRuntimeState>()
                .cache()
                .clone(),
            meshes: mesh_payload_bits(app, &bindings),
            colliders: collider_height_bits(app, &bindings),
            visual_stats_source_hash: app
                .world()
                .resource::<VisualValleyStats>()
                .source_hash
                .clone(),
            proof_stats_source_hash: app
                .world()
                .resource::<ProofSceneStats>()
                .source_hash
                .clone(),
            bindings,
            collider_binding,
            collider_handle,
            context_entity,
            backend_height,
        }
    }

    fn assert_source_runtime_restored(app: &mut App, source: &SourceRuntimeSnapshot) {
        const LOCAL_SAMPLE_METRES: f32 = 30.0;

        let state = app.world().resource::<TerrainRuntimeState>();
        assert_eq!(state.document(), &source.document);
        assert_eq!(state.cache(), &source.cache);
        assert_eq!(snapshots(app), source.bindings);
        assert_eq!(mesh_payload_bits(app, &source.bindings), source.meshes);
        assert_eq!(
            collider_height_bits(app, &source.bindings),
            source.colliders
        );
        assert_eq!(
            app.world().resource::<VisualValleyStats>().source_hash,
            source.visual_stats_source_hash
        );
        assert_eq!(
            app.world().resource::<ProofSceneStats>().source_hash,
            source.proof_stats_source_hash
        );
        let handle = *app
            .world()
            .get::<RapierColliderHandle>(source.collider_binding.collider_entity)
            .unwrap();
        let context_entity = app
            .world()
            .get::<RapierContextEntityLink>(source.collider_binding.collider_entity)
            .unwrap()
            .0;
        assert_eq!(handle.0, source.collider_handle.0);
        assert_eq!(context_entity, source.context_entity);
        let backend_height = backend_raycast_height(app, handle, LOCAL_SAMPLE_METRES);
        assert!((backend_height - source.backend_height).abs() <= 1.0e-4);
    }

    #[test]
    fn partial_patch_preserves_entities_and_mesh_handles() {
        let mut app = build_test_app(true);
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        let before_bindings = snapshots(&app);
        let before_meshes = mesh_payload_bits(&app, &before_bindings);
        let before_colliders = collider_height_bits(&app, &before_bindings);
        let (target_document, request) = request_for_current_state(&app);
        let full_rebuild = TerrainGenerationPlan::from_document(&target_document)
            .unwrap()
            .generate()
            .unwrap();

        app.world_mut()
            .resource_mut::<PendingTerrainPartialUpdate>()
            .submit(request)
            .unwrap();
        app.world_mut().run_schedule(FixedUpdate);

        let after_bindings = snapshots(&app);
        let after_meshes = mesh_payload_bits(&app, &after_bindings);
        let after_colliders = collider_height_bits(&app, &after_bindings);
        assert_eq!(after_bindings, before_bindings);
        assert_eq!(
            before_meshes
                .iter()
                .filter(|(id, positions)| after_meshes.get(id) != Some(*positions))
                .count(),
            3
        );
        assert_eq!(
            before_colliders
                .iter()
                .filter(|(id, heights)| after_colliders.get(id) != Some(*heights))
                .count(),
            1
        );

        let state = app.world().resource::<TerrainRuntimeState>();
        assert_eq!(state.document(), &target_document);
        assert_eq!(state.cache(), &full_rebuild);
        let outcome = app.world().resource::<TerrainRuntimeUpdateOutcome>();
        assert_eq!(outcome.processed(), 1);
        assert!(outcome.error().is_none());
        let report = outcome.report().unwrap();
        assert_eq!(report.mesh_chunks().len(), 3);
        assert_eq!(report.collider_chunks().len(), 1);
        assert_eq!(report.binding_count(), TERRAIN_CHUNK_COUNT);
        assert_eq!(report.mesh_assets_verified(), 3);
        assert_eq!(report.collider_components_verified(), 1);
        assert_eq!(report.rapier_backends_verified(), 1);
        assert_eq!(
            report.target_document_hash(),
            full_rebuild.source_document_hash
        );
        assert_eq!(
            report.target_cache_hash(),
            full_rebuild.content_hash().unwrap()
        );
    }

    #[test]
    fn missing_mesh_asset_aborts_without_mutating_source_or_colliders() {
        let mut app = build_test_app(false);
        let before_bindings = snapshots(&app);
        let before_colliders = collider_height_bits(&app, &before_bindings);
        let before_document = app
            .world()
            .resource::<TerrainRuntimeState>()
            .document()
            .clone();
        let before_cache = app
            .world()
            .resource::<TerrainRuntimeState>()
            .cache()
            .clone();
        let missing = before_bindings
            .iter()
            .find(|binding| binding.coordinate == TerrainChunkCoordinate { x: 0, z: 0 })
            .unwrap();
        app.world_mut()
            .resource_mut::<Assets<Mesh>>()
            .remove(missing.mesh_asset_id)
            .unwrap();
        let (_, request) = request_for_current_state(&app);

        app.world_mut()
            .resource_mut::<PendingTerrainPartialUpdate>()
            .submit(request)
            .unwrap();
        app.update();

        let state = app.world().resource::<TerrainRuntimeState>();
        assert_eq!(state.document(), &before_document);
        assert_eq!(state.cache(), &before_cache);
        assert_eq!(snapshots(&app), before_bindings);
        assert_eq!(
            collider_height_bits(&app, &before_bindings),
            before_colliders
        );
        let outcome = app.world().resource::<TerrainRuntimeUpdateOutcome>();
        assert!(outcome.report().is_none());
        assert!(outcome.error().unwrap().contains("mesh asset"));
    }

    #[test]
    fn missing_binding_aborts_without_mutating_source_or_projections() {
        let mut app = build_test_app(false);
        let before_bindings = snapshots(&app);
        let before_meshes = mesh_payload_bits(&app, &before_bindings);
        let before_colliders = collider_height_bits(&app, &before_bindings);
        let before_document = app
            .world()
            .resource::<TerrainRuntimeState>()
            .document()
            .clone();
        let before_cache = app
            .world()
            .resource::<TerrainRuntimeState>()
            .cache()
            .clone();
        let missing_id = before_bindings
            .iter()
            .find(|binding| binding.coordinate == TerrainChunkCoordinate { x: 0, z: 0 })
            .unwrap()
            .id;
        app.world_mut()
            .resource_mut::<TerrainRuntimeBindings>()
            .chunks
            .remove(&missing_id);
        let (_, request) = request_for_current_state(&app);

        app.world_mut()
            .resource_mut::<PendingTerrainPartialUpdate>()
            .submit(request)
            .unwrap();
        app.update();

        let state = app.world().resource::<TerrainRuntimeState>();
        assert_eq!(state.document(), &before_document);
        assert_eq!(state.cache(), &before_cache);
        assert_eq!(mesh_payload_bits(&app, &before_bindings), before_meshes);
        assert_eq!(
            collider_height_bits(&app, &before_bindings),
            before_colliders
        );
        let outcome = app.world().resource::<TerrainRuntimeUpdateOutcome>();
        assert!(outcome.report().is_none());
        assert!(outcome.error().unwrap().contains("expected 64"));
        assert!(!outcome.projection_failed());
    }

    #[test]
    fn post_commit_mesh_corruption_rolls_back_every_projection_before_failure_is_published() {
        let mut app = build_test_app(true);
        app.insert_resource(CorruptTargetMeshOnce(false))
            .add_systems(
                FixedUpdate,
                corrupt_target_mesh_once
                    .after(PhysicsSet::Writeback)
                    .before(TerrainRuntimeUpdateSet::Verify),
            );
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        let source = snapshot_source_runtime(&mut app);
        let (target_document, request) = request_for_current_state(&app);
        let attempted_target_cache_hash = TerrainGenerationPlan::from_document(&target_document)
            .unwrap()
            .generate()
            .unwrap()
            .content_hash()
            .unwrap();
        app.world_mut().resource_mut::<CorruptTargetMeshOnce>().0 = true;
        app.world_mut()
            .resource_mut::<PendingTerrainPartialUpdate>()
            .submit(request)
            .unwrap();

        app.world_mut().run_schedule(FixedUpdate);
        assert_eq!(
            app.world()
                .resource::<TerrainRuntimeUpdateOutcome>()
                .processed(),
            0,
            "failure remains unpublished until Rapier has resynchronized the rollback"
        );
        app.world_mut().run_schedule(FixedUpdate);

        let outcome = app.world().resource::<TerrainRuntimeUpdateOutcome>();
        assert_eq!(outcome.processed(), 1);
        assert!(outcome.report().is_none());
        assert!(outcome.projection_failed());
        assert!(outcome.rollback_verified());
        assert_eq!(
            outcome.attempted_target_cache_hash(),
            Some(attempted_target_cache_hash)
        );
        assert!(outcome.error().unwrap().contains("mesh projection"));
        assert_source_runtime_restored(&mut app, &source);
    }

    #[test]
    fn failed_rollback_poison_blocks_later_transactions_without_consuming_queue() {
        let mut app = build_test_app(true);
        app.insert_resource(SabotageRollbackOnce(false))
            .add_systems(
                FixedUpdate,
                sabotage_rollback_once
                    .after(PhysicsSet::Writeback)
                    .before(TerrainRuntimeUpdateSet::Verify),
            );
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        let (_, first_request) = request_for_current_state(&app);
        app.world_mut().resource_mut::<SabotageRollbackOnce>().0 = true;
        app.world_mut()
            .resource_mut::<PendingTerrainPartialUpdate>()
            .submit(first_request)
            .unwrap();

        app.world_mut().run_schedule(FixedUpdate);

        let outcome = app.world().resource::<TerrainRuntimeUpdateOutcome>();
        assert_eq!(outcome.processed(), 1);
        assert!(outcome.report().is_none());
        assert!(outcome.projection_failed());
        assert!(!outcome.rollback_verified());
        assert!(
            outcome
                .error()
                .unwrap()
                .contains("rollback verification failed")
        );
        assert!(matches!(
            app.world().resource::<PendingTerrainBackendProof>().0,
            Some(TerrainBackendPhase::Poisoned)
        ));

        let (_, second_request) = request_for_current_state(&app);
        app.world_mut()
            .resource_mut::<PendingTerrainPartialUpdate>()
            .submit(second_request)
            .unwrap();
        let processed = app
            .world()
            .resource::<TerrainRuntimeUpdateOutcome>()
            .processed();
        let document = app
            .world()
            .resource::<TerrainRuntimeState>()
            .document()
            .clone();
        let cache = app
            .world()
            .resource::<TerrainRuntimeState>()
            .cache()
            .clone();

        app.world_mut().run_schedule(FixedUpdate);
        app.world_mut().run_schedule(FixedUpdate);

        assert!(
            app.world()
                .resource::<PendingTerrainPartialUpdate>()
                .is_pending(),
            "a poisoned runtime must leave later transactions in the queue"
        );
        assert_eq!(
            app.world()
                .resource::<TerrainRuntimeUpdateOutcome>()
                .processed(),
            processed
        );
        let state = app.world().resource::<TerrainRuntimeState>();
        assert_eq!(state.document(), &document);
        assert_eq!(state.cache(), &cache);
        assert!(matches!(
            app.world().resource::<PendingTerrainBackendProof>().0,
            Some(TerrainBackendPhase::Poisoned)
        ));
    }

    #[test]
    fn rapier_context_change_is_rejected_and_the_source_context_is_restored() {
        let mut app = build_test_app(true);
        app.insert_resource(ChangeTargetRapierContextOnce(false))
            .add_systems(
                FixedUpdate,
                change_target_rapier_context_once
                    .after(PhysicsSet::Writeback)
                    .before(TerrainRuntimeUpdateSet::Verify),
            );
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        let source = snapshot_source_runtime(&mut app);
        let (_, request) = request_for_current_state(&app);
        app.world_mut()
            .resource_mut::<ChangeTargetRapierContextOnce>()
            .0 = true;
        app.world_mut()
            .resource_mut::<PendingTerrainPartialUpdate>()
            .submit(request)
            .unwrap();

        app.world_mut().run_schedule(FixedUpdate);
        app.world_mut().run_schedule(FixedUpdate);

        let outcome = app.world().resource::<TerrainRuntimeUpdateOutcome>();
        assert_eq!(outcome.processed(), 1);
        assert!(outcome.report().is_none());
        assert!(outcome.projection_failed());
        assert!(outcome.rollback_verified());
        assert!(outcome.error().unwrap().contains("changed Rapier context"));
        assert_source_runtime_restored(&mut app, &source);
    }

    #[test]
    fn material_paint_preserves_live_geometry_and_rapier_components() {
        let mut app = build_test_app(true);
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        let bindings = snapshots(&app);
        let geometry = |app: &App| {
            let meshes = app.world().resource::<Assets<Mesh>>();
            bindings
                .iter()
                .map(|b| {
                    let mesh = meshes.get(b.mesh_asset_id).unwrap();
                    let Some(VertexAttributeValues::Float32x3(positions)) =
                        mesh.attribute(Mesh::ATTRIBUTE_POSITION)
                    else {
                        panic!("positions");
                    };
                    let Some(VertexAttributeValues::Float32x3(normals)) =
                        mesh.attribute(Mesh::ATTRIBUTE_NORMAL)
                    else {
                        panic!("normals");
                    };
                    (
                        positions.as_ptr() as usize,
                        normals.as_ptr() as usize,
                        mesh.indices().unwrap().clone(),
                    )
                })
                .collect::<Vec<_>>()
        };
        let before_geometry = geometry(&app);
        let collider_state = |app: &App| {
            bindings
                .iter()
                .map(|b| {
                    let entity = app.world().entity(b.collider_entity);
                    (
                        entity.get::<RapierColliderHandle>().unwrap().0,
                        entity.get_ref::<Collider>().unwrap().last_changed(),
                    )
                })
                .collect::<Vec<_>>()
        };
        let before_colliders = collider_state(&app);
        let before_heights = collider_height_bits(&app, &bindings);
        let source = app.world().resource::<TerrainRuntimeState>();
        let mut target = source.document().clone();
        target.schema_version = 3;
        target.revision = target.revision.next().unwrap();
        target.material_overrides = vec![forge_project::TerrainMaterialOverride {
            sample_x: 32,
            sample_z: 32,
            weights: vec![forge_project::TerrainMaterialWeight {
                layer_id: target.material_layers[1].id.clone(),
                weight: u16::MAX,
            }],
        }];
        let request = TerrainPartialUpdateRequest {
            base_revision: source.document().revision,
            base_document_hash: source.cache().source_document_hash,
            target_document: target.clone(),
        };
        app.world_mut()
            .resource_mut::<PendingTerrainPartialUpdate>()
            .submit_authoring(request)
            .unwrap();
        app.world_mut().run_schedule(FixedUpdate);
        let outcome = app.world().resource::<TerrainRuntimeUpdateOutcome>();
        assert!(outcome.error().is_none(), "{:?}", outcome.error());
        let report = outcome.report().unwrap();
        assert_eq!(report.mesh_chunks().len(), 4);
        assert!(report.collider_chunks().is_empty());
        assert_eq!(
            geometry(&app),
            before_geometry,
            "live position/normal allocations retained"
        );
        assert_eq!(
            collider_state(&app),
            before_colliders,
            "no collider change tick or backend handle change"
        );
        assert_eq!(collider_height_bits(&app, &bindings), before_heights);
        assert_eq!(snapshots(&app), bindings);
        assert_eq!(
            app.world().resource::<TerrainRuntimeState>().document(),
            &target
        );
    }

    #[test]
    fn material_paint_corruption_restores_source_and_all_projections() {
        let mut app = build_test_app(true);
        app.insert_resource(CorruptTargetMeshOnce(false))
            .add_systems(
                FixedUpdate,
                corrupt_target_mesh_once
                    .after(PhysicsSet::Writeback)
                    .before(TerrainRuntimeUpdateSet::Verify),
            );
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        let original = snapshot_source_runtime(&mut app);
        let state = app.world().resource::<TerrainRuntimeState>();
        let mut target = state.document().clone();
        target.schema_version = 3;
        target.revision = target.revision.next().unwrap();
        target.material_overrides = vec![forge_project::TerrainMaterialOverride {
            sample_x: 32,
            sample_z: 32,
            weights: vec![forge_project::TerrainMaterialWeight {
                layer_id: target.material_layers[2].id.clone(),
                weight: u16::MAX,
            }],
        }];
        let request = TerrainPartialUpdateRequest {
            base_revision: state.document().revision,
            base_document_hash: state.cache().source_document_hash,
            target_document: target,
        };
        app.world_mut().resource_mut::<CorruptTargetMeshOnce>().0 = true;
        app.world_mut()
            .resource_mut::<PendingTerrainPartialUpdate>()
            .submit_authoring(request)
            .unwrap();
        app.world_mut().run_schedule(FixedUpdate);
        app.world_mut().run_schedule(FixedUpdate);
        let outcome = app.world().resource::<TerrainRuntimeUpdateOutcome>();
        assert!(outcome.report().is_none());
        assert!(outcome.projection_failed());
        assert!(outcome.rollback_verified(), "{:?}", outcome.error());
        assert_source_runtime_restored(&mut app, &original);
    }

    #[test]
    fn authoring_history_restores_exact_snapshots_and_rapier_height() {
        let mut app = build_test_app(true);
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        let original_bindings = snapshots(&app);
        let original = app
            .world()
            .resource::<TerrainRuntimeState>()
            .document()
            .clone();
        let handle = *app
            .world()
            .get::<RapierColliderHandle>(original_bindings[0].collider_entity)
            .unwrap();
        let initial_height = backend_raycast_height(&mut app, handle, 30.0);
        let mut elevated = original.clone();
        elevated.revision = original.revision.next().unwrap();
        elevated.height_sampling.base_elevation_m += 8.0;
        for target in [&elevated, &original, &elevated] {
            let state = app.world().resource::<TerrainRuntimeState>();
            let request = TerrainPartialUpdateRequest {
                base_revision: state.document().revision,
                base_document_hash: state.cache().source_document_hash,
                target_document: target.clone(),
            };
            app.world_mut()
                .resource_mut::<PendingTerrainPartialUpdate>()
                .submit_authoring_snapshot(request)
                .unwrap();
            app.world_mut().run_schedule(FixedUpdate);
            let outcome = app.world().resource::<TerrainRuntimeUpdateOutcome>();
            assert!(outcome.error().is_none(), "{:?}", outcome.error());
            assert_eq!(outcome.report().unwrap().target_revision(), target.revision);
            let state = app.world().resource::<TerrainRuntimeState>();
            assert_eq!(state.document(), target);
            assert_eq!(
                state.cache().source_document_hash,
                target.content_hash().unwrap()
            );
            assert_eq!(snapshots(&app), original_bindings);
            let height = backend_raycast_height(&mut app, handle, 30.0);
            let difference = if target == &original { 0.0 } else { 8.0 };
            assert!((height - initial_height - difference).abs() < 1.0e-4);
        }
    }

    #[test]
    fn authoring_rename_and_elevation_preserve_bindings_and_update_rapier() {
        let mut app = build_test_app(true);
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        let original_bindings = snapshots(&app);
        let original_meshes = mesh_payload_bits(&app, &original_bindings);
        let handle = *app
            .world()
            .get::<RapierColliderHandle>(original_bindings[0].collider_entity)
            .unwrap();
        let initial_height = backend_raycast_height(&mut app, handle, 30.0);
        for elevate in [false, true] {
            let state = app.world().resource::<TerrainRuntimeState>();
            let mut target = state.document().migrate_to_latest().unwrap();
            target.revision = state.document().revision.next().unwrap();
            if elevate {
                target.height_sampling.base_elevation_m += 8.0;
            } else {
                target.name = "Nom édité".into();
            }
            let request = TerrainPartialUpdateRequest {
                base_revision: state.document().revision,
                base_document_hash: state.cache().source_document_hash,
                target_document: target.clone(),
            };
            app.world_mut()
                .resource_mut::<PendingTerrainPartialUpdate>()
                .submit_authoring(request)
                .unwrap();
            app.world_mut().run_schedule(FixedUpdate);
            let outcome = app.world().resource::<TerrainRuntimeUpdateOutcome>();
            assert!(outcome.error().is_none(), "{:?}", outcome.error());
            let report = outcome.report().expect("backend proof must be available");
            assert_eq!(report.mesh_chunks().len(), if elevate { 64 } else { 0 });
            assert_eq!(
                app.world().resource::<TerrainRuntimeState>().document(),
                &target
            );
            assert_eq!(snapshots(&app), original_bindings);
            if !elevate {
                assert_eq!(mesh_payload_bits(&app, &original_bindings), original_meshes);
            }
        }
        let final_height = backend_raycast_height(&mut app, handle, 30.0);
        assert!((final_height - initial_height - 8.0).abs() < 1.0e-4);
    }

    #[test]
    fn rapier_backend_keeps_handle_and_observes_new_height() {
        const LOCAL_SAMPLE_METRES: f32 = 30.0;

        let mut app = build_test_app(true);
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        let binding = snapshots(&app)
            .into_iter()
            .find(|binding| binding.coordinate == TerrainChunkCoordinate { x: 0, z: 0 })
            .unwrap();
        let before_handle = *app
            .world()
            .get::<RapierColliderHandle>(binding.collider_entity)
            .expect("Rapier must materialize the terrain collider handle");
        let before_height = backend_raycast_height(&mut app, before_handle, LOCAL_SAMPLE_METRES);
        let (_, request) = request_for_current_state(&app);

        app.world_mut()
            .resource_mut::<PendingTerrainPartialUpdate>()
            .submit(request)
            .unwrap();
        app.world_mut().run_schedule(FixedUpdate);

        let after_handle = *app
            .world()
            .get::<RapierColliderHandle>(binding.collider_entity)
            .unwrap();
        let after_height = backend_raycast_height(&mut app, after_handle, LOCAL_SAMPLE_METRES);
        assert_eq!(after_handle.0, before_handle.0);
        assert!((after_height - before_height - 5.0).abs() <= 1.0e-4);
    }

    fn backend_raycast_height(
        app: &mut App,
        handle: RapierColliderHandle,
        local_sample_metres: f32,
    ) -> f32 {
        let world = app.world_mut();
        let mut contexts =
            world.query_filtered::<&RapierContextColliders, With<DefaultRapierContext>>();
        let context = contexts.single(world).unwrap();
        let collider = context.colliders.get(handle.0).unwrap();
        let heightfield = collider.shape().as_heightfield().unwrap();
        let ray_origin_y = 200.0;
        let ray = Ray::new(
            Vector::new(local_sample_metres, ray_origin_y, local_sample_metres),
            Vector::new(0.0, -1.0, 0.0),
        );
        let time_of_impact = heightfield
            .cast_local_ray(&ray, 400.0, true)
            .expect("the backend heightfield must answer at the changed sample");
        ray_origin_y - time_of_impact
    }
}
