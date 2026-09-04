use crate::{
    GeneratedVegetation, TERRAIN_CHUNKS_PER_AXIS, TerrainChunkCoordinate, VegetationChunk,
    VegetationGenerationError, VegetationGenerationPlan, VegetationKindCounts,
};
use forge_core::ContentHash;
use forge_project::{
    TerrainDocumentError, VegetationDensityError, VegetationDocumentError, WorkspaceArea,
};
use std::{error::Error, fmt, sync::Arc};

/// Exact source identity for a dynamic vegetation cache.
///
/// Terrain is part of the key because altitude, slope and material filters all affect accepted
/// placements. The density document is independent from the recipe and therefore has its own hash.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct VegetationRuntimeCacheKey {
    terrain: ContentHash,
    vegetation: ContentHash,
    density: ContentHash,
}

impl VegetationRuntimeCacheKey {
    #[must_use]
    pub const fn terrain_document_hash(self) -> ContentHash {
        self.terrain
    }

    #[must_use]
    pub const fn vegetation_document_hash(self) -> ContentHash {
        self.vegetation
    }

    #[must_use]
    pub const fn vegetation_density_document_hash(self) -> ContentHash {
        self.density
    }
}

/// Result of one source refresh. Changed chunks are always reported in canonical Z/X order.
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct VegetationRuntimeCacheUpdate {
    source_key: VegetationRuntimeCacheKey,
    target_key: VegetationRuntimeCacheKey,
    regenerated: bool,
    changed_chunks: Vec<TerrainChunkCoordinate>,
    retained_chunks: usize,
}

impl VegetationRuntimeCacheUpdate {
    #[must_use]
    pub const fn source_key(&self) -> VegetationRuntimeCacheKey {
        self.source_key
    }

    #[must_use]
    pub const fn target_key(&self) -> VegetationRuntimeCacheKey {
        self.target_key
    }

    /// False only when all three canonical source hashes were already current.
    #[must_use]
    pub const fn regenerated(&self) -> bool {
        self.regenerated
    }

    #[must_use]
    pub fn changed_chunks(&self) -> &[TerrainChunkCoordinate] {
        &self.changed_chunks
    }

    #[must_use]
    pub const fn retained_chunks(&self) -> usize {
        self.retained_chunks
    }
}

/// Dynamic, engine-neutral cache of all 64 deterministic vegetation chunks.
///
/// A refresh prepares and validates the complete target generation before mutating this cache. It
/// then reuses the existing `Arc` for every equal chunk and replaces only changed chunks. Renderers
/// may use these arcs as stable CPU cache handles without making Bevy or Studio a source of truth.
#[derive(Clone, Debug)]
pub struct VegetationRuntimeCache {
    key: VegetationRuntimeCacheKey,
    origin_xz_m: [f32; 2],
    cell_size_m: f32,
    cells_per_axis: u16,
    chunks: Vec<Arc<VegetationChunk>>,
    kind_counts: VegetationKindCounts,
}

impl VegetationRuntimeCache {
    pub fn from_area(area: &WorkspaceArea) -> Result<Self, VegetationRuntimeCacheError> {
        let key = vegetation_runtime_cache_key(area)?;
        let generated = generate_area_vegetation(area)?;
        Self::from_generated(key, &generated)
    }

    #[must_use]
    pub const fn key(&self) -> VegetationRuntimeCacheKey {
        self.key
    }

    #[must_use]
    pub fn chunks(&self) -> &[Arc<VegetationChunk>] {
        &self.chunks
    }

    /// Returns the stable CPU cache handle for one canonical chunk.
    #[must_use]
    pub fn chunk_handle(
        &self,
        coordinate: TerrainChunkCoordinate,
    ) -> Option<&Arc<VegetationChunk>> {
        if !coordinate.is_in_bounds() {
            return None;
        }
        let index = usize::from(coordinate.z * TERRAIN_CHUNKS_PER_AXIS + coordinate.x);
        self.chunks.get(index)
    }

    /// Reconstructs the complete generated value represented by this cache.
    #[must_use]
    pub fn snapshot(&self) -> GeneratedVegetation {
        GeneratedVegetation {
            terrain_document_hash: self.key.terrain,
            vegetation_document_hash: self.key.vegetation,
            origin_xz_m: self.origin_xz_m,
            cell_size_m: self.cell_size_m,
            cells_per_axis: self.cells_per_axis,
            chunks: self
                .chunks
                .iter()
                .map(|chunk| chunk.as_ref().clone())
                .collect(),
            kind_counts: self.kind_counts,
        }
    }

    /// Regenerates on any terrain, recipe or density hash change, then swaps only unequal chunks.
    pub fn refresh_from_area(
        &mut self,
        area: &WorkspaceArea,
    ) -> Result<VegetationRuntimeCacheUpdate, VegetationRuntimeCacheError> {
        let source_key = self.key;
        let target_key = vegetation_runtime_cache_key(area)?;
        if source_key == target_key {
            return Ok(VegetationRuntimeCacheUpdate {
                source_key,
                target_key,
                regenerated: false,
                changed_chunks: vec![],
                retained_chunks: self.chunks.len(),
            });
        }

        let generated = generate_area_vegetation(area)?;
        validate_generated_chunk_layout(&generated)?;
        if generated.terrain_document_hash != target_key.terrain
            || generated.vegetation_document_hash != target_key.vegetation
        {
            return Err(VegetationRuntimeCacheError::GeneratedSourceHashMismatch);
        }

        let mut changed_chunks = Vec::new();
        let next_chunks = self
            .chunks
            .iter()
            .zip(&generated.chunks)
            .map(|(current, target)| {
                if current.as_ref() == target {
                    Arc::clone(current)
                } else {
                    changed_chunks.push(target.coordinate);
                    Arc::new(target.clone())
                }
            })
            .collect::<Vec<_>>();
        let retained_chunks = next_chunks.len().saturating_sub(changed_chunks.len());

        // Commit only after every source and generated chunk has been validated and compared.
        self.key = target_key;
        self.origin_xz_m = generated.origin_xz_m;
        self.cell_size_m = generated.cell_size_m;
        self.cells_per_axis = generated.cells_per_axis;
        self.chunks = next_chunks;
        self.kind_counts = generated.kind_counts;

        Ok(VegetationRuntimeCacheUpdate {
            source_key,
            target_key,
            regenerated: true,
            changed_chunks,
            retained_chunks,
        })
    }

    fn from_generated(
        key: VegetationRuntimeCacheKey,
        generated: &GeneratedVegetation,
    ) -> Result<Self, VegetationRuntimeCacheError> {
        validate_generated_chunk_layout(generated)?;
        if generated.terrain_document_hash != key.terrain
            || generated.vegetation_document_hash != key.vegetation
        {
            return Err(VegetationRuntimeCacheError::GeneratedSourceHashMismatch);
        }
        Ok(Self {
            key,
            origin_xz_m: generated.origin_xz_m,
            cell_size_m: generated.cell_size_m,
            cells_per_axis: generated.cells_per_axis,
            chunks: generated.chunks.iter().cloned().map(Arc::new).collect(),
            kind_counts: generated.kind_counts,
        })
    }
}

fn vegetation_runtime_cache_key(
    area: &WorkspaceArea,
) -> Result<VegetationRuntimeCacheKey, VegetationRuntimeCacheError> {
    Ok(VegetationRuntimeCacheKey {
        terrain: area.terrain.content_hash()?,
        vegetation: area.vegetation.content_hash()?,
        density: area.vegetation_density.content_hash()?,
    })
}

fn generate_area_vegetation(
    area: &WorkspaceArea,
) -> Result<Arc<GeneratedVegetation>, VegetationRuntimeCacheError> {
    Ok(VegetationGenerationPlan::from_documents_with_density(
        &area.terrain,
        &area.vegetation,
        &area.vegetation_density,
    )?
    .generate()?)
}

fn validate_generated_chunk_layout(
    generated: &GeneratedVegetation,
) -> Result<(), VegetationRuntimeCacheError> {
    let expected_count = usize::from(TERRAIN_CHUNKS_PER_AXIS).pow(2);
    if generated.chunks.len() != expected_count {
        return Err(VegetationRuntimeCacheError::InvalidChunkCount {
            expected: expected_count,
            actual: generated.chunks.len(),
        });
    }
    for (index, (chunk, expected)) in generated
        .chunks
        .iter()
        .zip((0..TERRAIN_CHUNKS_PER_AXIS).flat_map(|z| {
            (0..TERRAIN_CHUNKS_PER_AXIS).map(move |x| TerrainChunkCoordinate { x, z })
        }))
        .enumerate()
    {
        if chunk.coordinate != expected {
            return Err(VegetationRuntimeCacheError::UnexpectedChunkCoordinate {
                index,
                expected,
                actual: chunk.coordinate,
            });
        }
    }
    Ok(())
}

#[derive(Debug)]
pub enum VegetationRuntimeCacheError {
    TerrainDocument(TerrainDocumentError),
    VegetationDocument(VegetationDocumentError),
    VegetationDensityDocument(VegetationDensityError),
    Generation(VegetationGenerationError),
    InvalidChunkCount {
        expected: usize,
        actual: usize,
    },
    UnexpectedChunkCoordinate {
        index: usize,
        expected: TerrainChunkCoordinate,
        actual: TerrainChunkCoordinate,
    },
    GeneratedSourceHashMismatch,
}

impl fmt::Display for VegetationRuntimeCacheError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::TerrainDocument(error) => write!(formatter, "invalid terrain source: {error}"),
            Self::VegetationDocument(error) => {
                write!(formatter, "invalid vegetation recipe source: {error}")
            }
            Self::VegetationDensityDocument(error) => {
                write!(formatter, "invalid vegetation density source: {error}")
            }
            Self::Generation(error) => write!(formatter, "vegetation generation failed: {error}"),
            Self::InvalidChunkCount { expected, actual } => write!(
                formatter,
                "vegetation cache expected {expected} chunks, found {actual}"
            ),
            Self::UnexpectedChunkCoordinate {
                index,
                expected,
                actual,
            } => write!(
                formatter,
                "vegetation cache chunk {index} is {actual:?}, expected {expected:?}"
            ),
            Self::GeneratedSourceHashMismatch => {
                formatter.write_str("generated vegetation source hashes do not match the cache key")
            }
        }
    }
}

impl Error for VegetationRuntimeCacheError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::TerrainDocument(error) => Some(error),
            Self::VegetationDocument(error) => Some(error),
            Self::VegetationDensityDocument(error) => Some(error),
            Self::Generation(error) => Some(error),
            Self::InvalidChunkCount { .. }
            | Self::UnexpectedChunkCoordinate { .. }
            | Self::GeneratedSourceHashMismatch => None,
        }
    }
}

impl From<TerrainDocumentError> for VegetationRuntimeCacheError {
    fn from(value: TerrainDocumentError) -> Self {
        Self::TerrainDocument(value)
    }
}

impl From<VegetationDocumentError> for VegetationRuntimeCacheError {
    fn from(value: VegetationDocumentError) -> Self {
        Self::VegetationDocument(value)
    }
}

impl From<VegetationDensityError> for VegetationRuntimeCacheError {
    fn from(value: VegetationDensityError) -> Self {
        Self::VegetationDensityDocument(value)
    }
}

impl From<VegetationGenerationError> for VegetationRuntimeCacheError {
    fn from(value: VegetationGenerationError) -> Self {
        Self::Generation(value)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::{AreaId, CommandId, ModuleId, ProjectId, Vec3};
    use forge_project::{
        NewProjectCommand, TerrainMaterialLayerId, TerrainMaterialRole,
        TerrainMaterialWeightOverride, VegetationDensityLayer, VegetationDensityOverride,
        VegetationMaterialFilter, WorkspaceChange, WorkspaceDocument, WorkspaceEdit,
    };

    fn test_workspace() -> WorkspaceDocument {
        let mut document = WorkspaceDocument::create(NewProjectCommand {
            command_id: CommandId::deterministic("vegetation-cache/create"),
            project_id: ProjectId::deterministic("vegetation-cache/project"),
            module_id: ModuleId::deterministic("vegetation-cache/module"),
            area_id: AreaId::deterministic("vegetation-cache/area"),
            project_name: "Cache project".to_owned(),
            module_name: "Cache module".to_owned(),
            area_name: "Cache area".to_owned(),
            seed: 0x4341_4348_455f_5631,
            spawn: Vec3::new(0.0, 16.0, 0.0),
        })
        .unwrap()
        .document;
        let area = &mut document.project.modules[0].areas[0];
        let mut grass = area.vegetation.layers.pop().unwrap();
        grass.acceptance_probability = 1.0;
        grass.attempts_per_cell = 1;
        grass.minimum_spacing_m = 0.0;
        grass.maximum_slope_gradient = 16.0;
        grass.minimum_altitude_m = -10_000.0;
        grass.maximum_altitude_m = 10_000.0;
        grass.material_filter = None;
        area.vegetation.layers = vec![grass];
        area.vegetation.exclusions.clear();
        area.vegetation_density.layers = vec![VegetationDensityLayer::new(
            area.vegetation.layers[0].id.clone(),
            0,
            vec![],
        )];
        document.validate().unwrap();
        document
    }

    fn paint_density(
        document: &WorkspaceDocument,
        command_key: &str,
        cells: &[(u16, u16)],
    ) -> WorkspaceDocument {
        let module = &document.project.modules[0];
        let area = &module.areas[0];
        let layer_id = area.vegetation.layers[0].id.clone();
        document
            .preview_edit(WorkspaceEdit {
                command_id: CommandId::deterministic(command_key),
                base_revision: document.revision,
                change: WorkspaceChange::SetVegetationDensityOverrides {
                    module_id: module.id,
                    area_id: area.id,
                    layer_id,
                    expected_density_hash: area.vegetation_density.content_hash().unwrap(),
                    density_overrides: cells
                        .iter()
                        .map(|&(x, z)| VegetationDensityOverride::new(x, z, u8::MAX))
                        .collect(),
                },
            })
            .unwrap()
            .document
    }

    fn generated_reference(area: &WorkspaceArea) -> Arc<GeneratedVegetation> {
        VegetationGenerationPlan::from_documents_with_density(
            &area.terrain,
            &area.vegetation,
            &area.vegetation_density,
        )
        .unwrap()
        .generate()
        .unwrap()
    }

    fn area(document: &WorkspaceDocument) -> &WorkspaceArea {
        &document.project.modules[0].areas[0]
    }

    fn material_cell(cell: u16, weights: [f32; 3]) -> Vec<TerrainMaterialWeightOverride> {
        let first_terrain_sample = cell * 4;
        (first_terrain_sample..=first_terrain_sample + 4)
            .flat_map(|sample_z| {
                (first_terrain_sample..=first_terrain_sample + 4).map(move |sample_x| {
                    TerrainMaterialWeightOverride::new(sample_x, sample_z, weights)
                })
            })
            .collect()
    }

    #[test]
    fn complete_cache_matches_the_full_generation_reference() {
        let source = test_workspace();
        let painted = paint_density(
            &source,
            "vegetation-cache/full-reference",
            &[(0, 0), (1, 0), (8, 0)],
        );
        let area = area(&painted);
        let reference = generated_reference(area);
        let cache = VegetationRuntimeCache::from_area(area).unwrap();

        assert_eq!(cache.snapshot(), *reference);
        assert_eq!(cache.chunks().len(), 64);
        assert_eq!(
            cache.key().terrain_document_hash(),
            area.terrain.content_hash().unwrap()
        );
        assert_eq!(
            cache.key().vegetation_document_hash(),
            area.vegetation.content_hash().unwrap()
        );
        assert_eq!(
            cache.key().vegetation_density_document_hash(),
            area.vegetation_density.content_hash().unwrap()
        );
    }

    #[test]
    fn local_mask_replaces_one_chunk_and_retains_the_other_sixty_three_handles() {
        let empty = test_workspace();
        let source = paint_density(&empty, "vegetation-cache/local-source", &[(0, 0)]);
        let target = paint_density(&source, "vegetation-cache/local-target", &[(0, 0), (8, 0)]);
        let mut cache = VegetationRuntimeCache::from_area(area(&source)).unwrap();
        let before = cache.chunks().to_vec();

        let update = cache.refresh_from_area(area(&target)).unwrap();

        assert!(update.regenerated());
        assert_eq!(
            update.changed_chunks(),
            &[TerrainChunkCoordinate { x: 1, z: 0 }]
        );
        assert_eq!(update.retained_chunks(), 63);
        assert_eq!(cache.snapshot(), *generated_reference(area(&target)));
        for (index, (old, current)) in before.iter().zip(cache.chunks()).enumerate() {
            assert_eq!(
                Arc::ptr_eq(old, current),
                index != 1,
                "only chunk (1, 0) may receive a new CPU cache handle"
            );
        }
    }

    #[test]
    fn replay_save_equivalent_undo_and_redo_are_exact() {
        let empty = test_workspace();
        let source = paint_density(&empty, "vegetation-cache/history-source", &[(0, 0)]);
        let target = paint_density(
            &source,
            "vegetation-cache/history-target",
            &[(0, 0), (8, 0)],
        );
        let source_reference = generated_reference(area(&source));
        let target_reference = generated_reference(area(&target));
        let mut cache = VegetationRuntimeCache::from_area(area(&target)).unwrap();
        let target_key = cache.key();
        let before_save_handles = cache.chunks().to_vec();

        let saved = target.to_pretty_json().unwrap();
        let restored = WorkspaceDocument::from_json(&saved).unwrap();
        let save_equivalent = cache.refresh_from_area(area(&restored)).unwrap();
        assert!(!save_equivalent.regenerated());
        assert!(save_equivalent.changed_chunks().is_empty());
        assert_eq!(save_equivalent.retained_chunks(), 64);
        assert_eq!(cache.key(), target_key);
        assert!(
            before_save_handles
                .iter()
                .zip(cache.chunks())
                .all(|(before, after)| Arc::ptr_eq(before, after))
        );

        let replay = VegetationRuntimeCache::from_area(area(&restored)).unwrap();
        assert_eq!(replay.key(), target_key);
        assert_eq!(replay.snapshot(), cache.snapshot());

        let undo = cache.refresh_from_area(area(&source)).unwrap();
        assert_eq!(
            undo.changed_chunks(),
            &[TerrainChunkCoordinate { x: 1, z: 0 }]
        );
        assert_eq!(cache.snapshot(), *source_reference);
        let redo = cache.refresh_from_area(area(&target)).unwrap();
        assert_eq!(
            redo.changed_chunks(),
            &[TerrainChunkCoordinate { x: 1, z: 0 }]
        );
        assert_eq!(cache.snapshot(), *target_reference);
    }

    #[test]
    fn terrain_material_change_invalidates_filtered_placements() {
        const CELL: u16 = 32;

        let mut empty = test_workspace();
        let source_area = &mut empty.project.modules[0].areas[0];
        source_area.vegetation.layers[0].material_filter = Some(VegetationMaterialFilter::new(
            TerrainMaterialRole::Grass,
            TerrainMaterialLayerId::new("meadow_grass"),
            0.35,
        ));
        source_area.terrain.material_weight_overrides = material_cell(CELL, [1.0, 0.0, 0.0]);
        empty.validate().unwrap();
        let source = paint_density(
            &empty,
            "vegetation-cache/terrain-filter-source",
            &[(CELL, CELL)],
        );
        let mut target = source.clone();
        target.revision = target.revision.next().unwrap();
        let target_area = &mut target.project.modules[0].areas[0];
        target_area.terrain.revision = target.revision;
        target_area.terrain.material_weight_overrides = material_cell(CELL, [0.0, 1.0, 0.0]);
        target.validate().unwrap();

        let mut cache = VegetationRuntimeCache::from_area(area(&source)).unwrap();
        assert_eq!(cache.snapshot().kind_counts.grass_patches, 1);
        let source_key = cache.key();
        let update = cache.refresh_from_area(area(&target)).unwrap();

        assert_ne!(
            source_key.terrain_document_hash(),
            update.target_key().terrain_document_hash()
        );
        assert_eq!(
            source_key.vegetation_document_hash(),
            update.target_key().vegetation_document_hash()
        );
        assert_eq!(
            source_key.vegetation_density_document_hash(),
            update.target_key().vegetation_density_document_hash()
        );
        assert_eq!(
            update.changed_chunks(),
            &[TerrainChunkCoordinate { x: 4, z: 4 }]
        );
        assert_eq!(cache.snapshot().kind_counts.grass_patches, 0);
    }
}
