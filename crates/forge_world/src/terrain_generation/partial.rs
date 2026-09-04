use super::sampling::{material_weights_at, sample_height_unchecked, sample_normal};
use super::{
    GeneratedTerrain, TERRAIN_CHUNK_COUNT, TERRAIN_CHUNK_INTERVALS,
    TERRAIN_CHUNK_VERTICES_PER_AXIS, TERRAIN_CHUNKS_PER_AXIS, TERRAIN_GLOBAL_INTERVALS_PER_AXIS,
    TERRAIN_HALF_EXTENT_M, TERRAIN_SAMPLE_SPACING_M, TerrainChunkCoordinate, TerrainChunkMesh,
    TerrainDirtyChunk, TerrainGenerationError, TerrainGenerationPlan,
};
use forge_core::{ContentHash, Revision, RevisionError};
use forge_project::{
    TERRAIN_DOCUMENT_SCHEMA_VERSION, TerrainDocument, TerrainHeightDelta,
    TerrainMaterialWeightOverride,
};
use serde::{Deserialize, Serialize};
use std::collections::BTreeSet;
use thiserror::Error;

const CHUNK_VERTEX_COUNT: usize = 1_089;
const CHUNK_INDEX_COUNT: usize = 6_144;
const CHUNK_TRIANGLE_COUNT: usize = 2_048;

/// Immutable request for a cache transition based on an exact source revision and content hash.
#[derive(Clone, Debug, PartialEq)]
pub struct TerrainPartialUpdateRequest {
    pub base_revision: Revision,
    pub base_document_hash: ContentHash,
    pub target_document: TerrainDocument,
}

/// One unique source-grid sample whose authored height or material weights changed.
#[derive(Clone, Copy, Debug, Eq, Hash, Ord, PartialEq, PartialOrd, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainHeightSampleCoordinate {
    pub sample_x: u16,
    pub sample_z: u16,
}

impl TerrainHeightSampleCoordinate {
    #[must_use]
    pub const fn new(sample_x: u16, sample_z: u16) -> Self {
        Self { sample_x, sample_z }
    }
}

/// Prepared atomic cache transition containing only the chunks that must be replaced.
#[derive(Debug, PartialEq)]
pub struct TerrainPartialUpdate {
    source_document_hash: ContentHash,
    target_document_hash: ContentHash,
    target_document: TerrainDocument,
    replacement_chunks: Vec<TerrainChunkMesh>,
    changed_samples: Vec<TerrainHeightSampleCoordinate>,
    /// Chunks whose positions, normals or material weights were patched.
    mesh_chunks: Vec<TerrainDirtyChunk>,
    /// Chunks whose local collider heights were patched.
    collider_chunks: Vec<TerrainDirtyChunk>,
}

impl TerrainPartialUpdate {
    #[must_use]
    pub const fn source_document_hash(&self) -> ContentHash {
        self.source_document_hash
    }

    #[must_use]
    pub const fn target_document_hash(&self) -> ContentHash {
        self.target_document_hash
    }

    #[must_use]
    pub const fn target_document(&self) -> &TerrainDocument {
        &self.target_document
    }

    #[must_use]
    pub fn replacement_chunks(&self) -> &[TerrainChunkMesh] {
        &self.replacement_chunks
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

    /// Atomically replaces only the prepared chunks after fully revalidating the live cache.
    ///
    /// All fallible validation completes before the first mutation. The subsequent replacement
    /// loop cannot change vector cardinality or allocate and therefore has no error path.
    pub fn apply_to_cache(
        self,
        cache: &mut GeneratedTerrain,
    ) -> Result<TerrainPartialUpdateCommit, TerrainPartialUpdateError> {
        validate_cache_application(&self, cache)?;

        let Self {
            source_document_hash,
            target_document_hash,
            target_document,
            replacement_chunks,
            changed_samples,
            mesh_chunks,
            collider_chunks,
        } = self;

        for replacement in replacement_chunks {
            let chunk_index = chunk_index(replacement.coordinate);
            cache.chunks[chunk_index] = replacement;
        }
        cache.source_document_hash = target_document_hash;

        Ok(TerrainPartialUpdateCommit {
            source_document_hash,
            target_document_hash,
            target_document,
            changed_samples,
            mesh_chunks,
            collider_chunks,
        })
    }
}

/// Lightweight metadata returned after a prepared terrain update has been committed.
#[derive(Clone, Debug, PartialEq)]
pub struct TerrainPartialUpdateCommit {
    source_document_hash: ContentHash,
    target_document_hash: ContentHash,
    target_document: TerrainDocument,
    changed_samples: Vec<TerrainHeightSampleCoordinate>,
    mesh_chunks: Vec<TerrainDirtyChunk>,
    collider_chunks: Vec<TerrainDirtyChunk>,
}

impl TerrainPartialUpdateCommit {
    #[must_use]
    pub const fn source_document_hash(&self) -> ContentHash {
        self.source_document_hash
    }

    #[must_use]
    pub const fn target_document_hash(&self) -> ContentHash {
        self.target_document_hash
    }

    #[must_use]
    pub const fn target_document(&self) -> &TerrainDocument {
        &self.target_document
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
}

impl TerrainGenerationPlan {
    /// Applies a sparse height/material transition to a valid derived cache.
    ///
    /// The request cannot provide a dirty region: changed samples and their cardinal normal
    /// dependencies are derived from the exact source/target document diff. Inputs are borrowed and
    /// no target value is constructed until every stale, structural and layout check passes.
    pub fn apply_partial_update(
        &self,
        source_cache: &GeneratedTerrain,
        request: TerrainPartialUpdateRequest,
    ) -> Result<TerrainPartialUpdate, TerrainPartialUpdateError> {
        if request.base_revision != self.document.revision {
            return Err(TerrainPartialUpdateError::StaleBaseRevision {
                expected: self.document.revision,
                actual: request.base_revision,
            });
        }
        if request.base_document_hash != self.document_hash {
            return Err(TerrainPartialUpdateError::StaleBaseDocumentHash {
                expected: self.document_hash,
                actual: request.base_document_hash,
            });
        }
        validate_source_cache(self, source_cache)?;

        let expected_revision = self.document.revision.next()?;
        if request.target_document.revision != expected_revision {
            return Err(TerrainPartialUpdateError::UnexpectedTargetRevision {
                expected: expected_revision,
                actual: request.target_document.revision,
            });
        }
        self.prepare_cache_transition(source_cache, request.target_document, false)
    }

    /// Refreshes a derived projection between two already accepted source snapshots.
    ///
    /// Unlike an authoring command, a renderer may skip revisions, reopen an older snapshot,
    /// or observe only metadata changes. Exact cache hashes, layout and structural compatibility
    /// are still checked; this never changes a canonical document or invents a revision.
    pub fn refresh_cached_projection(
        &self,
        source_cache: &GeneratedTerrain,
        target_document: TerrainDocument,
    ) -> Result<TerrainPartialUpdate, TerrainPartialUpdateError> {
        validate_source_cache(self, source_cache)?;
        self.prepare_cache_transition(source_cache, target_document, true)
    }

    fn prepare_cache_transition(
        &self,
        source_cache: &GeneratedTerrain,
        target_document: TerrainDocument,
        allow_no_source_changes: bool,
    ) -> Result<TerrainPartialUpdate, TerrainPartialUpdateError> {
        if target_document.schema_version != TERRAIN_DOCUMENT_SCHEMA_VERSION {
            return Err(TerrainPartialUpdateError::TargetMustUseCurrentSchema);
        }
        validate_partial_compatibility(&self.document, &target_document)?;
        let target_plan = TerrainGenerationPlan::from_document(&target_document)
            .map_err(TerrainPartialUpdateError::InvalidTargetDocument)?;
        let changed_height_samples =
            changed_height_samples(&self.document.height_deltas, &target_document.height_deltas);
        let changed_material_samples = changed_material_samples(
            &self.document.material_weight_overrides,
            &target_document.material_weight_overrides,
        );
        let changed_samples = union_samples(&changed_height_samples, &changed_material_samples);
        if changed_samples.is_empty() && !allow_no_source_changes {
            return Err(TerrainPartialUpdateError::NoSourceChanges);
        }

        let height_dependency_samples = normal_dependency_samples(&changed_height_samples);
        let mesh_samples = union_samples(&height_dependency_samples, &changed_material_samples);
        let mesh_coordinates = affected_chunk_coordinates(&mesh_samples);
        let collider_coordinates = affected_chunk_coordinates(&changed_height_samples);
        let mesh_chunks = dirty_chunks(&target_plan, &mesh_coordinates);
        let collider_chunks = dirty_chunks(&target_plan, &collider_coordinates);

        let height_changed_keys = changed_height_samples
            .iter()
            .map(|sample| (sample.sample_z, sample.sample_x))
            .collect::<BTreeSet<_>>();
        let height_dependency_keys = height_dependency_samples
            .iter()
            .map(|sample| (sample.sample_z, sample.sample_x))
            .collect::<BTreeSet<_>>();
        let mut replacement_chunks = mesh_coordinates
            .iter()
            .map(|coordinate| source_cache.chunks[chunk_index(*coordinate)].clone())
            .collect::<Vec<_>>();
        for sample in &mesh_samples {
            patch_sample(
                &target_document,
                &mut replacement_chunks,
                *sample,
                height_changed_keys.contains(&(sample.sample_z, sample.sample_x)),
                height_dependency_keys.contains(&(sample.sample_z, sample.sample_x)),
            );
        }

        Ok(TerrainPartialUpdate {
            source_document_hash: self.document_hash,
            target_document_hash: target_plan.document_hash,
            target_document,
            replacement_chunks,
            changed_samples,
            mesh_chunks,
            collider_chunks,
        })
    }
}

fn validate_partial_compatibility(
    source: &TerrainDocument,
    target: &TerrainDocument,
) -> Result<(), TerrainPartialUpdateError> {
    let compatible = source.schema == target.schema
        && source.project_id == target.project_id
        && source.area_id == target.area_id
        && source.coordinate_system == target.coordinate_system
        && source.name == target.name
        && source.seed == target.seed
        && source.extent_m.size_x_m.to_bits() == target.extent_m.size_x_m.to_bits()
        && source.extent_m.size_z_m.to_bits() == target.extent_m.size_z_m.to_bits()
        && source.height_sampling.algorithm == target.height_sampling.algorithm
        && source.height_sampling.sample_spacing_m.to_bits()
            == target.height_sampling.sample_spacing_m.to_bits()
        && source.height_sampling.base_elevation_m.to_bits()
            == target.height_sampling.base_elevation_m.to_bits()
        && source.height_sampling.amplitude_m.to_bits()
            == target.height_sampling.amplitude_m.to_bits()
        && source.height_sampling.base_frequency_cycles_per_m.to_bits()
            == target.height_sampling.base_frequency_cycles_per_m.to_bits()
        && source.height_sampling.octaves == target.height_sampling.octaves
        && source.height_sampling.lacunarity.to_bits()
            == target.height_sampling.lacunarity.to_bits()
        && source.height_sampling.persistence.to_bits()
            == target.height_sampling.persistence.to_bits()
        && source.material_layers == target.material_layers;
    if compatible {
        Ok(())
    } else {
        Err(TerrainPartialUpdateError::FullRebuildRequired)
    }
}

fn validate_source_cache(
    plan: &TerrainGenerationPlan,
    cache: &GeneratedTerrain,
) -> Result<(), TerrainPartialUpdateError> {
    if cache.source_document_hash != plan.document_hash {
        return Err(TerrainPartialUpdateError::SourceCacheHashMismatch {
            expected: plan.document_hash,
            actual: cache.source_document_hash,
        });
    }
    validate_cache_layout(plan, cache)
}

fn validate_cache_layout(
    plan: &TerrainGenerationPlan,
    cache: &GeneratedTerrain,
) -> Result<(), TerrainPartialUpdateError> {
    if cache.origin_xz_m.map(f32::to_bits) != plan.origin_xz_m().map(f32::to_bits) {
        return Err(TerrainPartialUpdateError::InvalidSourceCacheLayout(
            "origin does not match the source plan",
        ));
    }
    let expected_layers = [
        plan.document.material_layers[0].id.clone(),
        plan.document.material_layers[1].id.clone(),
        plan.document.material_layers[2].id.clone(),
    ];
    if cache.material_layer_ids != expected_layers {
        return Err(TerrainPartialUpdateError::InvalidSourceCacheLayout(
            "material-layer identities do not match the source document",
        ));
    }
    if cache.chunks.len() != TERRAIN_CHUNK_COUNT {
        return Err(TerrainPartialUpdateError::InvalidSourceCacheLayout(
            "chunk count is not 64",
        ));
    }

    for (index, chunk) in cache.chunks.iter().enumerate() {
        let coordinate = TerrainChunkCoordinate {
            x: u16::try_from(index % usize::from(TERRAIN_CHUNKS_PER_AXIS))
                .map_err(|_| TerrainPartialUpdateError::InvalidSourceCacheLayout("chunk x"))?,
            z: u16::try_from(index / usize::from(TERRAIN_CHUNKS_PER_AXIS))
                .map_err(|_| TerrainPartialUpdateError::InvalidSourceCacheLayout("chunk z"))?,
        };
        if chunk.coordinate != coordinate || chunk.id != plan.chunk_id(coordinate) {
            return Err(TerrainPartialUpdateError::InvalidSourceCacheLayout(
                "chunk order or stable identity is invalid",
            ));
        }
        if chunk.positions_m.len() != CHUNK_VERTEX_COUNT
            || chunk.normals.len() != CHUNK_VERTEX_COUNT
            || chunk.uvs.len() != CHUNK_VERTEX_COUNT
            || chunk.material_weights.len() != CHUNK_VERTEX_COUNT
            || chunk.indices.len() != CHUNK_INDEX_COUNT
            || chunk.collider.vertices_m.len() != CHUNK_VERTEX_COUNT
            || chunk.collider.triangles.len() != CHUNK_TRIANGLE_COUNT
        {
            return Err(TerrainPartialUpdateError::InvalidSourceCacheLayout(
                "chunk payload cardinality is invalid",
            ));
        }
    }
    Ok(())
}

fn validate_cache_application(
    update: &TerrainPartialUpdate,
    cache: &GeneratedTerrain,
) -> Result<(), TerrainPartialUpdateError> {
    if cache.source_document_hash != update.source_document_hash {
        return Err(TerrainPartialUpdateError::SourceCacheHashMismatch {
            expected: update.source_document_hash,
            actual: cache.source_document_hash,
        });
    }

    let target_plan = TerrainGenerationPlan::from_document(&update.target_document)
        .map_err(TerrainPartialUpdateError::InvalidTargetDocument)?;
    if target_plan.document_hash != update.target_document_hash {
        return Err(TerrainPartialUpdateError::InvalidReplacementLayout(
            "target document hash does not match the prepared update",
        ));
    }
    validate_cache_layout(&target_plan, cache)?;

    if update.replacement_chunks.len() != update.mesh_chunks.len() {
        return Err(TerrainPartialUpdateError::InvalidReplacementLayout(
            "replacement count does not match the mesh invalidation count",
        ));
    }
    let mut replacement_coordinates = BTreeSet::new();
    for (replacement, dirty) in update.replacement_chunks.iter().zip(&update.mesh_chunks) {
        if !dirty.coordinate.is_in_bounds() || target_plan.chunk_id(dirty.coordinate) != dirty.id {
            return Err(TerrainPartialUpdateError::InvalidReplacementLayout(
                "mesh invalidation identity is outside the target plan",
            ));
        }
        if replacement.coordinate != dirty.coordinate || replacement.id != dirty.id {
            return Err(TerrainPartialUpdateError::InvalidReplacementLayout(
                "replacement identity does not match its mesh invalidation",
            ));
        }
        if !replacement_coordinates.insert(dirty.coordinate) {
            return Err(TerrainPartialUpdateError::InvalidReplacementLayout(
                "mesh invalidation coordinates are not unique",
            ));
        }
        validate_chunk_payload(replacement)?;
        let source = &cache.chunks[chunk_index(dirty.coordinate)];
        if source.coordinate != dirty.coordinate || source.id != dirty.id {
            return Err(TerrainPartialUpdateError::InvalidSourceCacheLayout(
                "replacement destination identity is invalid",
            ));
        }
    }
    let mut collider_coordinates = BTreeSet::new();
    for collider in &update.collider_chunks {
        if !collider.coordinate.is_in_bounds()
            || target_plan.chunk_id(collider.coordinate) != collider.id
            || !update.mesh_chunks.contains(collider)
        {
            return Err(TerrainPartialUpdateError::InvalidReplacementLayout(
                "collider invalidation identity is not a replaced mesh chunk",
            ));
        }
        if !collider_coordinates.insert(collider.coordinate) {
            return Err(TerrainPartialUpdateError::InvalidReplacementLayout(
                "collider invalidation coordinates are not unique",
            ));
        }
    }

    Ok(())
}

fn validate_chunk_payload(chunk: &TerrainChunkMesh) -> Result<(), TerrainPartialUpdateError> {
    if chunk.positions_m.len() != CHUNK_VERTEX_COUNT
        || chunk.normals.len() != CHUNK_VERTEX_COUNT
        || chunk.uvs.len() != CHUNK_VERTEX_COUNT
        || chunk.material_weights.len() != CHUNK_VERTEX_COUNT
        || chunk.indices.len() != CHUNK_INDEX_COUNT
        || chunk.collider.vertices_m.len() != CHUNK_VERTEX_COUNT
        || chunk.collider.triangles.len() != CHUNK_TRIANGLE_COUNT
    {
        return Err(TerrainPartialUpdateError::InvalidReplacementLayout(
            "replacement chunk payload cardinality is invalid",
        ));
    }
    Ok(())
}

fn changed_height_samples(
    source: &[TerrainHeightDelta],
    target: &[TerrainHeightDelta],
) -> Vec<TerrainHeightSampleCoordinate> {
    let mut changed = Vec::new();
    let mut source_index = 0;
    let mut target_index = 0;
    while source_index < source.len() || target_index < target.len() {
        match (source.get(source_index), target.get(target_index)) {
            (Some(left), Some(right)) => match left.sort_key().cmp(&right.sort_key()) {
                std::cmp::Ordering::Less => {
                    changed.push(sample_coordinate(*left));
                    source_index += 1;
                }
                std::cmp::Ordering::Greater => {
                    changed.push(sample_coordinate(*right));
                    target_index += 1;
                }
                std::cmp::Ordering::Equal => {
                    if left.delta_m.to_bits() != right.delta_m.to_bits() {
                        changed.push(sample_coordinate(*right));
                    }
                    source_index += 1;
                    target_index += 1;
                }
            },
            (Some(left), None) => {
                changed.push(sample_coordinate(*left));
                source_index += 1;
            }
            (None, Some(right)) => {
                changed.push(sample_coordinate(*right));
                target_index += 1;
            }
            (None, None) => break,
        }
    }
    changed
}

fn changed_material_samples(
    source: &[TerrainMaterialWeightOverride],
    target: &[TerrainMaterialWeightOverride],
) -> Vec<TerrainHeightSampleCoordinate> {
    let mut changed = Vec::new();
    let mut source_index = 0;
    let mut target_index = 0;
    while source_index < source.len() || target_index < target.len() {
        match (source.get(source_index), target.get(target_index)) {
            (Some(left), Some(right)) => match left.sort_key().cmp(&right.sort_key()) {
                std::cmp::Ordering::Less => {
                    changed.push(material_sample_coordinate(*left));
                    source_index += 1;
                }
                std::cmp::Ordering::Greater => {
                    changed.push(material_sample_coordinate(*right));
                    target_index += 1;
                }
                std::cmp::Ordering::Equal => {
                    if left.weights.map(f32::to_bits) != right.weights.map(f32::to_bits) {
                        changed.push(material_sample_coordinate(*right));
                    }
                    source_index += 1;
                    target_index += 1;
                }
            },
            (Some(left), None) => {
                changed.push(material_sample_coordinate(*left));
                source_index += 1;
            }
            (None, Some(right)) => {
                changed.push(material_sample_coordinate(*right));
                target_index += 1;
            }
            (None, None) => break,
        }
    }
    changed
}

fn union_samples(
    left: &[TerrainHeightSampleCoordinate],
    right: &[TerrainHeightSampleCoordinate],
) -> Vec<TerrainHeightSampleCoordinate> {
    left.iter()
        .chain(right)
        .map(|sample| (sample.sample_z, sample.sample_x))
        .collect::<BTreeSet<_>>()
        .into_iter()
        .map(|(sample_z, sample_x)| TerrainHeightSampleCoordinate::new(sample_x, sample_z))
        .collect()
}

#[must_use]
const fn sample_coordinate(delta: TerrainHeightDelta) -> TerrainHeightSampleCoordinate {
    TerrainHeightSampleCoordinate::new(delta.sample_x, delta.sample_z)
}

#[must_use]
const fn material_sample_coordinate(
    authored: TerrainMaterialWeightOverride,
) -> TerrainHeightSampleCoordinate {
    TerrainHeightSampleCoordinate::new(authored.sample_x, authored.sample_z)
}

fn normal_dependency_samples(
    changed: &[TerrainHeightSampleCoordinate],
) -> Vec<TerrainHeightSampleCoordinate> {
    let mut dependency_keys = BTreeSet::new();
    for sample in changed {
        let x = sample.sample_x;
        let z = sample.sample_z;
        dependency_keys.insert((z, x));
        if x > 0 {
            dependency_keys.insert((z, x - 1));
        }
        if x < TERRAIN_GLOBAL_INTERVALS_PER_AXIS {
            dependency_keys.insert((z, x + 1));
        }
        if z > 0 {
            dependency_keys.insert((z - 1, x));
        }
        if z < TERRAIN_GLOBAL_INTERVALS_PER_AXIS {
            dependency_keys.insert((z + 1, x));
        }
    }
    dependency_keys
        .into_iter()
        .map(|(sample_z, sample_x)| TerrainHeightSampleCoordinate::new(sample_x, sample_z))
        .collect()
}

fn affected_chunk_coordinates(
    samples: &[TerrainHeightSampleCoordinate],
) -> Vec<TerrainChunkCoordinate> {
    let mut keys = BTreeSet::new();
    for sample in samples {
        for z in 0..TERRAIN_CHUNKS_PER_AXIS {
            for x in 0..TERRAIN_CHUNKS_PER_AXIS {
                let minimum_x = x * TERRAIN_CHUNK_INTERVALS;
                let minimum_z = z * TERRAIN_CHUNK_INTERVALS;
                if sample.sample_x >= minimum_x
                    && sample.sample_x <= minimum_x + TERRAIN_CHUNK_INTERVALS
                    && sample.sample_z >= minimum_z
                    && sample.sample_z <= minimum_z + TERRAIN_CHUNK_INTERVALS
                {
                    keys.insert((z, x));
                }
            }
        }
    }
    keys.into_iter()
        .map(|(z, x)| TerrainChunkCoordinate { x, z })
        .collect()
}

fn dirty_chunks(
    plan: &TerrainGenerationPlan,
    coordinates: &[TerrainChunkCoordinate],
) -> Vec<TerrainDirtyChunk> {
    coordinates
        .iter()
        .copied()
        .map(|coordinate| TerrainDirtyChunk {
            id: plan.chunk_id(coordinate),
            coordinate,
        })
        .collect()
}

fn patch_sample(
    document: &TerrainDocument,
    replacement_chunks: &mut [TerrainChunkMesh],
    sample: TerrainHeightSampleCoordinate,
    height_changed: bool,
    normal_changed: bool,
) {
    let x_m = world_coordinate(sample.sample_x);
    let z_m = world_coordinate(sample.sample_z);
    let height_m = sample_height_unchecked(document, x_m, z_m);
    let normal = sample_normal(document, x_m, z_m);
    let weights = material_weights_at(
        document,
        sample.sample_x,
        sample.sample_z,
        height_m,
        normal[1],
    );

    for coordinate in affected_chunk_coordinates(&[sample]) {
        let local_x = sample.sample_x - coordinate.x * TERRAIN_CHUNK_INTERVALS;
        let local_z = sample.sample_z - coordinate.z * TERRAIN_CHUNK_INTERVALS;
        let vertex_index = usize::from(local_z) * usize::from(TERRAIN_CHUNK_VERTICES_PER_AXIS)
            + usize::from(local_x);
        let chunk = replacement_chunks
            .iter_mut()
            .find(|chunk| chunk.coordinate == coordinate)
            .expect("every dependency chunk is prepared as a replacement");
        if height_changed {
            chunk.positions_m[vertex_index][1] = height_m;
            chunk.collider.vertices_m[vertex_index][1] = height_m;
        }
        if normal_changed {
            chunk.normals[vertex_index] = normal;
        }
        chunk.material_weights[vertex_index] = weights;
    }
}

#[must_use]
fn chunk_index(coordinate: TerrainChunkCoordinate) -> usize {
    usize::from(coordinate.z) * usize::from(TERRAIN_CHUNKS_PER_AXIS) + usize::from(coordinate.x)
}

#[must_use]
fn world_coordinate(sample: u16) -> f32 {
    -TERRAIN_HALF_EXTENT_M + f32::from(sample) * TERRAIN_SAMPLE_SPACING_M
}

#[derive(Debug, Error)]
pub enum TerrainPartialUpdateError {
    #[error("stale terrain base revision: expected {expected:?}, got {actual:?}")]
    StaleBaseRevision {
        expected: Revision,
        actual: Revision,
    },
    #[error("stale terrain base document hash: expected {expected}, got {actual}")]
    StaleBaseDocumentHash {
        expected: ContentHash,
        actual: ContentHash,
    },
    #[error("source terrain cache hash mismatch: expected {expected}, got {actual}")]
    SourceCacheHashMismatch {
        expected: ContentHash,
        actual: ContentHash,
    },
    #[error("invalid source terrain cache layout: {0}")]
    InvalidSourceCacheLayout(&'static str),
    #[error("invalid prepared terrain replacement layout: {0}")]
    InvalidReplacementLayout(&'static str),
    #[error("target terrain revision must be {expected:?}, got {actual:?}")]
    UnexpectedTargetRevision {
        expected: Revision,
        actual: Revision,
    },
    #[error("partial terrain target must use the current schema version")]
    TargetMustUseCurrentSchema,
    #[error("terrain change affects generation structure and requires a full rebuild")]
    FullRebuildRequired,
    #[error("partial terrain target is invalid: {0}")]
    InvalidTargetDocument(#[source] TerrainGenerationError),
    #[error("partial terrain target contains no height or material-weight change")]
    NoSourceChanges,
    #[error("cannot advance terrain revision: {0}")]
    Revision(#[from] RevisionError),
}
