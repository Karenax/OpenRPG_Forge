use crate::{
    TERRAIN_CHUNKS_PER_AXIS, TERRAIN_EXTENT_M, TERRAIN_HALF_EXTENT_M, TerrainChunkCoordinate,
    TerrainGenerationError, TerrainGenerationPlan,
};
use forge_core::{
    AreaId, CanonicalJsonError, ContentHash, EntityId, ProjectId, Revision, Vec3,
    canonical_json_sha256,
};
use forge_project::{
    TerrainDocument, TerrainMaterialLayerId, TerrainMaterialRole, VEGETATION_CHUNK_SIZE_M,
    VegetationCollisionProfile, VegetationCollisionShape, VegetationDocument,
    VegetationDocumentError, VegetationExclusion, VegetationExclusionId, VegetationExtent,
    VegetationKind, VegetationLayer, VegetationLayerId, VegetationMaterialFilter,
    VegetationPresentationProfile,
};
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;
use std::fmt;
use std::sync::{Arc, OnceLock};
use thiserror::Error;
mod render_density;
mod spatial_density;
pub use render_density::VegetationRenderDensityProjection;
pub(crate) use spatial_density::mask_weight;

pub const VISUAL_VALLEY_VEGETATION_SEED: u64 = 0x5645_4745_5441_5445;
pub const VISUAL_VALLEY_VEGETATION_CELLS_PER_CHUNK_AXIS: u16 = 8;
pub const VISUAL_VALLEY_VEGETATION_JSON: &str =
    include_str!("../../../examples/visual_valley/vegetation.v1.json");

const FNV_OFFSET_BASIS: u64 = 0xcbf2_9ce4_8422_2325;
const FNV_PRIME: u64 = 0x0000_0100_0000_01b3;
const CELL_X_MIX: u64 = 0x9e37_79b9_7f4a_7c15;
const CELL_Z_MIX: u64 = 0xbf58_476d_1ce4_e5b9;
const ATTEMPT_MIX: u64 = 0x94d0_49bb_1331_11eb;
const STREAM_MIX: u64 = 0xd6e8_feb8_6659_fd93;
const F64_UNIT_FROM_53_BITS: f64 = 1.0 / 9_007_199_254_740_992.0;
const F32_UNIT_FROM_16_BITS: f32 = 1.0 / 65_536.0;

/// Original, asset-free vegetation recipe for the 512 metre Visual Valley proof area.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct VisualValleyVegetationRecipe {
    pub seed: u64,
}

impl Default for VisualValleyVegetationRecipe {
    fn default() -> Self {
        Self {
            seed: VISUAL_VALLEY_VEGETATION_SEED,
        }
    }
}

impl VisualValleyVegetationRecipe {
    #[allow(
        clippy::too_many_lines,
        reason = "the fixed recipe keeps all four declarative vegetation layers together"
    )]
    pub fn build(self) -> Result<VegetationDocument, VegetationDocumentError> {
        let meadow_grass = || TerrainMaterialLayerId::new("meadow_grass");
        let mut document = VegetationDocument::new(
            ProjectId::deterministic("visual-valley-project"),
            AreaId::deterministic("visual-valley-area"),
            Revision::INITIAL,
            "Visual Valley vegetation",
            self.seed,
            VegetationExtent::new(TERRAIN_EXTENT_M, TERRAIN_EXTENT_M),
            VISUAL_VALLEY_VEGETATION_CELLS_PER_CHUNK_AXIS,
            vec![
                VegetationLayer::new(
                    "forge_trees",
                    "Forge trees",
                    VegetationKind::Tree,
                    0.10,
                    2,
                    6.0,
                    0.8,
                    1.3,
                    0.625,
                    -40.0,
                    64.0,
                    Some(VegetationMaterialFilter::new(
                        TerrainMaterialRole::Grass,
                        meadow_grass(),
                        0.25,
                    )),
                    Some(VegetationCollisionProfile {
                        shape: VegetationCollisionShape::Capsule,
                        radius_m: 0.45,
                        height_m: 5.0,
                        activation_distance_m: 96.0,
                    }),
                    0.35,
                ),
                VegetationLayer::new(
                    "forge_shrubs",
                    "Forge shrubs",
                    VegetationKind::Shrub,
                    0.16,
                    2,
                    3.5,
                    0.65,
                    1.15,
                    0.901,
                    -40.0,
                    64.0,
                    Some(VegetationMaterialFilter::new(
                        TerrainMaterialRole::Grass,
                        meadow_grass(),
                        0.18,
                    )),
                    None,
                    0.55,
                ),
                VegetationLayer::new(
                    "forge_ferns",
                    "Forge ferns",
                    VegetationKind::Fern,
                    0.18,
                    2,
                    2.5,
                    0.75,
                    1.2,
                    0.532,
                    -40.0,
                    56.0,
                    Some(VegetationMaterialFilter::new(
                        TerrainMaterialRole::Grass,
                        meadow_grass(),
                        0.45,
                    )),
                    None,
                    0.7,
                ),
                VegetationLayer::new(
                    "forge_grass",
                    "Forge grass patches",
                    VegetationKind::Grass,
                    0.8,
                    8,
                    0.0,
                    0.7,
                    1.25,
                    0.782,
                    -40.0,
                    60.0,
                    Some(VegetationMaterialFilter::new(
                        TerrainMaterialRole::Grass,
                        meadow_grass(),
                        0.35,
                    )),
                    None,
                    0.9,
                ),
            ],
            vec![
                VegetationExclusion::Circle {
                    id: VegetationExclusionId::new("forge_spawn_clearance"),
                    center_x_m: 0.0,
                    center_z_m: -208.0,
                    radius_m: 14.0,
                },
                VegetationExclusion::CorridorSegment {
                    id: VegetationExclusionId::new("forge_movement_corridor"),
                    start_x_m: 0.0,
                    start_z_m: -224.0,
                    end_x_m: 0.0,
                    end_z_m: 224.0,
                    half_width_m: 5.0,
                },
            ],
            VegetationPresentationProfile {
                near_lod_distance_m: 24.0,
                medium_lod_distance_m: 72.0,
                hlod_distance_m: 180.0,
                cull_distance_m: 384.0,
                shadow_distance_m: 128.0,
                wind_strength: 0.6,
                wind_speed_hz: 0.18,
                interaction_radius_m: 2.5,
                interaction_strength: 0.75,
                grass_blades_per_patch: 32,
            },
        );
        document.layers[3].density_pattern = Some(forge_project::VegetationDensityPattern {
            patch_size_m: 32.0,
            minimum_multiplier: 0.15,
            maximum_multiplier: 1.6,
        });
        document.validate()?;
        Ok(document)
    }
}

/// Loads and validates the exact R1-009 fixture embedded in Forge.
pub fn embedded_visual_valley_vegetation_document()
-> Result<VegetationDocument, VegetationDocumentError> {
    VegetationDocument::from_json(VISUAL_VALLEY_VEGETATION_JSON)
}

/// Stable generated identity for one vegetation chunk.
#[derive(Clone, Copy, Debug, Eq, Hash, Ord, PartialEq, PartialOrd, Serialize, Deserialize)]
#[serde(transparent)]
pub struct VegetationChunkId(EntityId);

impl VegetationChunkId {
    #[must_use]
    pub const fn as_entity_id(self) -> EntityId {
        self.0
    }
}

impl fmt::Display for VegetationChunkId {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        self.0.fmt(formatter)
    }
}

/// Stable generated identity for one accepted placement attempt.
#[derive(Clone, Copy, Debug, Eq, Hash, Ord, PartialEq, PartialOrd, Serialize, Deserialize)]
#[serde(transparent)]
pub struct VegetationInstanceId(EntityId);

impl VegetationInstanceId {
    #[must_use]
    pub const fn as_entity_id(self) -> EntityId {
        self.0
    }
}

impl fmt::Display for VegetationInstanceId {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        self.0.fmt(formatter)
    }
}

/// One engine-neutral placement generated from a logical layer.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationInstance {
    pub id: VegetationInstanceId,
    pub layer_id: VegetationLayerId,
    pub kind: VegetationKind,
    pub position_m: Vec3,
    pub yaw_radians: f32,
    pub scale: f32,
    pub wind_phase_radians: f32,
    pub wind_response: f32,
    pub slope_gradient_squared: f64,
    /// Interpolated selected material weight, or `None` for an unfiltered layer.
    pub material_weight: Option<f32>,
}

/// Stable per-kind counters used by generation evidence and renderer adapters.
#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationKindCounts {
    pub trees: u32,
    pub shrubs: u32,
    pub ferns: u32,
    /// Logical grass patches, not blade counts.
    pub grass_patches: u32,
}

impl VegetationKindCounts {
    #[must_use]
    pub const fn total(self) -> u32 {
        self.trees + self.shrubs + self.ferns + self.grass_patches
    }

    #[must_use]
    pub const fn count(self, kind: VegetationKind) -> u32 {
        match kind {
            VegetationKind::Tree => self.trees,
            VegetationKind::Shrub => self.shrubs,
            VegetationKind::Fern => self.ferns,
            VegetationKind::Grass => self.grass_patches,
        }
    }

    fn increment(&mut self, kind: VegetationKind) {
        match kind {
            VegetationKind::Tree => self.trees += 1,
            VegetationKind::Shrub => self.shrubs += 1,
            VegetationKind::Fern => self.ferns += 1,
            VegetationKind::Grass => self.grass_patches += 1,
        }
    }

    fn add(&mut self, other: Self) {
        self.trees += other.trees;
        self.shrubs += other.shrubs;
        self.ferns += other.ferns;
        self.grass_patches += other.grass_patches;
    }
}

/// Placements belonging to one existing 64 metre terrain chunk.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationChunk {
    pub id: VegetationChunkId,
    pub coordinate: TerrainChunkCoordinate,
    pub instances: Vec<VegetationInstance>,
    pub kind_counts: VegetationKindCounts,
}

/// Complete deterministic vegetation output, grouped in terrain chunk order.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GeneratedVegetation {
    pub terrain_document_hash: ContentHash,
    pub vegetation_document_hash: ContentHash,
    pub origin_xz_m: [f32; 2],
    pub cell_size_m: f32,
    pub cells_per_axis: u16,
    pub chunks: Vec<VegetationChunk>,
    pub kind_counts: VegetationKindCounts,
}

impl GeneratedVegetation {
    /// Hashes the complete derived cache for deterministic replay and evidence.
    pub fn content_hash(&self) -> Result<ContentHash, CanonicalJsonError> {
        canonical_json_sha256(self)
    }
}

/// Validated deterministic interpretation of a terrain and vegetation document pair.
#[derive(Clone, Debug)]
pub struct VegetationGenerationPlan {
    terrain: TerrainGenerationPlan,
    vegetation: VegetationDocument,
    vegetation_document_hash: ContentHash,
    path_exclusions: Vec<VegetationExclusion>,
    river_exclusions: Vec<crate::river_vegetation::Segment>,
    building_exclusions: Vec<crate::building_vegetation::Footprint>,
    material_filter_indices: Vec<Option<usize>>,
    cells_per_axis: u16,
    generated: OnceLock<Arc<GeneratedVegetation>>,
    promoted: std::collections::BTreeSet<(VegetationLayerId, EntityId)>,
}

impl VegetationGenerationPlan {
    /// Suppress exact promoted origins after spacing acceptance, retaining neighbours.
    /// An unresolved origin is an explicit conflict, never silently discarded.
    pub fn with_promotions(
        mut self,
        origins: &[forge_project::zone_authoring::promotion::PromotedInstance],
    ) -> Result<Self, VegetationGenerationError> {
        if !self.promoted.is_empty() {
            return Err(VegetationGenerationError::InvalidPromotions(
                "Promotions already installed".into(),
            ));
        }
        if origins.is_empty() {
            return Ok(self);
        }
        if origins.len() > forge_project::zone_authoring::placements::MAX_MANUAL_PLACEMENTS {
            return Err(VegetationGenerationError::InvalidPromotions(
                "Promotion quota exceeded".into(),
            ));
        }
        for origin in origins {
            if !self
                .vegetation
                .layers
                .iter()
                .any(|l| l.id == origin.layer_id && l.kind == VegetationKind::Tree)
                || !self
                    .promoted
                    .insert((origin.layer_id.clone(), origin.instance_id))
            {
                return Err(VegetationGenerationError::InvalidPromotions(
                    "Duplicate or absent tree layer".into(),
                ));
            }
        }
        self.vegetation_document_hash = canonical_json_sha256(&(
            "forge-promoted-scatter-v1",
            self.vegetation_document_hash,
            &self.promoted,
        ))
        .map_err(|e| VegetationGenerationError::InvalidPromotions(e.to_string()))?;
        self.generated = OnceLock::new();
        Ok(self)
    }
    /// Identity of the derived population, including authored corridor recipes.
    #[must_use]
    pub const fn source_hash(&self) -> ContentHash {
        self.vegetation_document_hash
    }
    pub fn from_documents(
        terrain: &TerrainDocument,
        vegetation: &VegetationDocument,
    ) -> Result<Self, VegetationGenerationError> {
        vegetation.validate()?;
        if vegetation.imported_source.is_some() {
            return Err(VegetationGenerationError::ImportedSourceRequiresResolver);
        }
        let terrain = TerrainGenerationPlan::from_document(terrain)?;
        if terrain.document().project_id != vegetation.project_id {
            return Err(VegetationGenerationError::ProjectIdentityMismatch);
        }
        if terrain.document().area_id != vegetation.area_id {
            return Err(VegetationGenerationError::AreaIdentityMismatch);
        }
        if terrain.document().extent_m.size_x_m.to_bits() != vegetation.extent_m.size_x_m.to_bits()
            || terrain.document().extent_m.size_z_m.to_bits()
                != vegetation.extent_m.size_z_m.to_bits()
        {
            return Err(VegetationGenerationError::ExtentMismatch);
        }

        let mut material_filter_indices = Vec::with_capacity(vegetation.layers.len());
        for layer in &vegetation.layers {
            let index = if let Some(filter) = &layer.material_filter {
                let actual = terrain.material_layer_id(filter.material_role);
                if actual != &filter.material_layer_id {
                    return Err(VegetationGenerationError::MaterialRoleIdentityMismatch {
                        layer_id: layer.id.clone(),
                        material_role: filter.material_role,
                        expected_material_layer_id: filter.material_layer_id.clone(),
                        actual_material_layer_id: actual.clone(),
                    });
                }
                Some(filter.material_role.index())
            } else {
                None
            };
            material_filter_indices.push(index);
        }

        Ok(Self {
            terrain,
            vegetation_document_hash: vegetation.content_hash()?,
            path_exclusions: Vec::new(),
            river_exclusions: Vec::new(),
            building_exclusions: Vec::new(),
            vegetation: vegetation.clone(),
            material_filter_indices,
            cells_per_axis: vegetation.cells_per_chunk_axis * TERRAIN_CHUNKS_PER_AXIS,
            generated: OnceLock::new(),
            promoted: std::collections::BTreeSet::new(),
        })
    }

    /// Interpret authored vegetation and path-derived clearances separately.
    /// Filtering precedes spacing acceptance, just like authored exclusions.
    pub fn from_documents_with_paths(
        terrain: &TerrainDocument,
        vegetation: &VegetationDocument,
        paths: &[forge_project::zone_authoring::paths::AuthoredPath],
    ) -> Result<Self, VegetationGenerationError> {
        let mut plan = Self::from_documents(terrain, vegetation)?;
        let prepared = crate::path_vegetation::prepare(vegetation, paths)
            .map_err(VegetationGenerationError::InvalidPathExclusions)?;
        plan.vegetation_document_hash = prepared.source_hash;
        plan.path_exclusions = prepared.exclusions;
        Ok(plan)
    }

    #[must_use]
    pub const fn terrain(&self) -> &TerrainGenerationPlan {
        &self.terrain
    }

    /// Combined generated clearances, independent from manual exclusion quotas.
    /// With no rivers, preserve the existing path projection hash exactly.
    pub fn from_documents_with_waterways(
        terrain: &TerrainDocument,
        vegetation: &VegetationDocument,
        paths: &[forge_project::zone_authoring::paths::AuthoredPath],
        rivers: &[forge_project::zone_authoring::rivers::AuthoredRiver],
    ) -> Result<Self, VegetationGenerationError> {
        let mut plan = Self::from_documents_with_paths(terrain, vegetation, paths)?;
        plan.river_exclusions = crate::river_vegetation::prepare(terrain, rivers)
            .map_err(VegetationGenerationError::InvalidRiverExclusions)?;
        if !rivers.is_empty() {
            plan.vegetation_document_hash = canonical_json_sha256(&(
                "river-vegetation-projection-v1",
                plan.vegetation_document_hash,
                rivers,
            ))
            .map_err(|error| {
                VegetationGenerationError::InvalidRiverExclusions(error.to_string())
            })?;
        }
        Ok(plan)
    }

    #[must_use]
    pub const fn vegetation(&self) -> &VegetationDocument {
        &self.vegetation
    }

    /// Root clearances derived from accepted rooms, without changing vegetation
    /// masks or consuming authored exclusion slots. Does not clip overhanging crowns.
    pub fn from_documents_with_buildings(
        terrain: &TerrainDocument,
        vegetation: &VegetationDocument,
        paths: &[forge_project::zone_authoring::paths::AuthoredPath],
        rivers: &[forge_project::zone_authoring::rivers::AuthoredRiver],
        buildings: &[forge_project::zone_authoring::buildings::AuthoredBuilding],
    ) -> Result<Self, VegetationGenerationError> {
        let mut plan = Self::from_documents_with_waterways(terrain, vegetation, paths, rivers)?;
        plan.building_exclusions = crate::building_vegetation::prepare(terrain, buildings)
            .map_err(VegetationGenerationError::InvalidBuildingExclusions)?;
        if !plan.building_exclusions.is_empty() {
            plan.vegetation_document_hash = canonical_json_sha256(&(
                "building-vegetation-projection-v1",
                plan.vegetation_document_hash,
                &plan.building_exclusions,
            ))
            .map_err(|e| VegetationGenerationError::InvalidBuildingExclusions(e.to_string()))?;
        }
        Ok(plan)
    }

    #[must_use]
    pub const fn cells_per_axis(&self) -> u16 {
        self.cells_per_axis
    }

    pub fn generate(&self) -> Result<Arc<GeneratedVegetation>, VegetationGenerationError> {
        Ok(Arc::clone(self.generated()?))
    }

    /// Resolve a canonical tree from an acknowledged projection, never a quality-only decoration.
    /// Asset selection is deliberately left to the governed prototype resolver.
    pub fn resolve_promotion_source(
        &self,
        terrain_hash: ContentHash,
        vegetation_hash: ContentHash,
        layer_id: &VegetationLayerId,
        instance_id: EntityId,
    ) -> Result<VegetationInstance, VegetationGenerationError> {
        if terrain_hash != self.terrain.document_hash() || vegetation_hash != self.source_hash() {
            return Err(VegetationGenerationError::InvalidPromotions(
                "Stale promotion selection: terrain or vegetation source changed".into(),
            ));
        }
        self.generate()?
            .chunks
            .iter()
            .flat_map(|chunk| &chunk.instances)
            .find(|instance| {
                instance.kind == VegetationKind::Tree
                    && &instance.layer_id == layer_id
                    && instance.id.as_entity_id() == instance_id
            })
            .cloned()
            .ok_or_else(|| {
                VegetationGenerationError::InvalidPromotions(
                    "Selected canonical tree is absent or already promoted".into(),
                )
            })
    }

    /// Returns the exact cached subset used by [`Self::generate`], including cross-chunk spacing.
    pub fn generate_chunk(
        &self,
        coordinate: TerrainChunkCoordinate,
    ) -> Result<VegetationChunk, VegetationGenerationError> {
        if !coordinate.is_in_bounds() {
            return Err(VegetationGenerationError::ChunkCoordinateOutOfBounds {
                x: coordinate.x,
                z: coordinate.z,
            });
        }
        let index = usize::from(coordinate.z * TERRAIN_CHUNKS_PER_AXIS + coordinate.x);
        Ok(self.generated()?.chunks[index].clone())
    }

    fn generated(&self) -> Result<&Arc<GeneratedVegetation>, VegetationGenerationError> {
        if let Some(generated) = self.generated.get() {
            return Ok(generated);
        }
        let generated = Arc::new(self.generate_uncached()?);
        Ok(self.generated.get_or_init(|| generated))
    }

    fn generate_uncached(&self) -> Result<GeneratedVegetation, VegetationGenerationError> {
        let mut unresolved = self.promoted.clone();
        let mut chunks = Vec::with_capacity(usize::from(TERRAIN_CHUNKS_PER_AXIS).pow(2));
        for z in 0..TERRAIN_CHUNKS_PER_AXIS {
            for x in 0..TERRAIN_CHUNKS_PER_AXIS {
                let coordinate = TerrainChunkCoordinate { x, z };
                chunks.push(VegetationChunk {
                    id: self.chunk_id(coordinate),
                    coordinate,
                    instances: Vec::new(),
                    kind_counts: VegetationKindCounts::default(),
                });
            }
        }

        // Per-layer spatial indexes enforce the canonical global Z/X/attempt greedy order.
        // `generate_chunk` reads this single partitioned result, so seams use the same spacing set.
        // Buckets keep the bounded source contract from degrading to an O(n^2) scan when a layer
        // uses a very small non-zero spacing value.
        let mut spacing_indexes = self
            .vegetation
            .layers
            .iter()
            .map(|layer| SpacingIndex::new(layer.minimum_spacing_m))
            .collect::<Vec<_>>();
        for cell_z in 0..self.cells_per_axis {
            for cell_x in 0..self.cells_per_axis {
                for (layer_index, layer) in self.vegetation.layers.iter().enumerate() {
                    for attempt in 0..layer.attempts_per_cell {
                        if let Some(instance) =
                            self.generate_attempt(layer_index, layer, cell_x, cell_z, attempt)?
                        {
                            let xz = [instance.position_m.x, instance.position_m.z];
                            if spacing_indexes[layer_index].accept(xz) {
                                if unresolved.remove(&(
                                    instance.layer_id.clone(),
                                    instance.id.as_entity_id(),
                                )) {
                                    continue;
                                }
                                let coordinate = self.owner_chunk(cell_x, cell_z);
                                let index = usize::from(
                                    coordinate.z * TERRAIN_CHUNKS_PER_AXIS + coordinate.x,
                                );
                                chunks[index].kind_counts.increment(instance.kind);
                                chunks[index].instances.push(instance);
                            }
                        }
                    }
                }
            }
        }

        if !unresolved.is_empty() {
            return Err(VegetationGenerationError::UnresolvedPromotions(
                unresolved.into_iter().collect(),
            ));
        }
        let mut kind_counts = VegetationKindCounts::default();
        for chunk in &chunks {
            kind_counts.add(chunk.kind_counts);
        }
        Ok(GeneratedVegetation {
            terrain_document_hash: self.terrain.document_hash(),
            vegetation_document_hash: self.vegetation_document_hash,
            origin_xz_m: self.terrain.origin_xz_m(),
            cell_size_m: self.vegetation.cell_size_m(),
            cells_per_axis: self.cells_per_axis,
            chunks,
            kind_counts,
        })
    }

    fn generate_attempt(
        &self,
        layer_index: usize,
        layer: &VegetationLayer,
        cell_x: u16,
        cell_z: u16,
        attempt: u8,
    ) -> Result<Option<VegetationInstance>, VegetationGenerationError> {
        self.generate_attempt_with_acceptance(
            layer_index,
            layer,
            cell_x,
            cell_z,
            attempt,
            self.attempt_acceptance(layer, cell_x, cell_z, attempt),
        )
    }

    fn generate_attempt_with_acceptance(
        &self,
        layer_index: usize,
        layer: &VegetationLayer,
        cell_x: u16,
        cell_z: u16,
        attempt: u8,
        acceptance: f64,
    ) -> Result<Option<VegetationInstance>, VegetationGenerationError> {
        let entropy = attempt_entropy(
            self.vegetation.seed,
            layer.id.as_str(),
            cell_x,
            cell_z,
            attempt,
        );
        if unit_f64(entropy, 0) >= acceptance {
            return Ok(None);
        }

        let (x_m, z_m) = self.candidate_xz(entropy, cell_x, cell_z);
        if self
            .vegetation
            .exclusions
            .iter()
            .chain(&self.path_exclusions)
            .any(|exclusion| exclusion_contains(exclusion, x_m, z_m))
            || self
                .river_exclusions
                .iter()
                .any(|segment| segment.contains(x_m, z_m))
            || self
                .building_exclusions
                .iter()
                .any(|footprint| footprint.contains(x_m, z_m))
        {
            return Ok(None);
        }

        let surface = self.terrain.surface_at(x_m, z_m)?;
        let maximum_slope_gradient_squared = f64::from(layer.maximum_slope_gradient).powi(2);
        if surface.slope_gradient_squared > maximum_slope_gradient_squared
            || surface.height_m < layer.minimum_altitude_m
            || surface.height_m > layer.maximum_altitude_m
        {
            return Ok(None);
        }

        let material_weight = self.material_filter_indices[layer_index]
            .map(|index| surface.material_weights.as_array()[index]);
        if let (Some(filter), Some(weight)) = (&layer.material_filter, material_weight)
            && weight < filter.minimum_weight
        {
            return Ok(None);
        }

        let yaw_radians = finite_f32(unit_f64(entropy, 3) * std::f64::consts::TAU)?;
        let scale = finite_f32(
            f64::from(layer.minimum_scale)
                + unit_f64(entropy, 4) * f64::from(layer.maximum_scale - layer.minimum_scale),
        )?;
        let wind_phase_radians = finite_f32(unit_f64(entropy, 5) * std::f64::consts::TAU)?;
        let id = VegetationInstanceId(EntityId::deterministic(format!(
            "vegetation-generation-v1/{}/{}/{:016x}/{:016x}/{}/{}/{}/{}/{}",
            self.vegetation.project_id,
            self.vegetation.area_id,
            self.terrain.document().seed,
            self.vegetation.seed,
            self.vegetation.cells_per_chunk_axis,
            layer.id,
            cell_x,
            cell_z,
            attempt
        )));
        Ok(Some(VegetationInstance {
            id,
            layer_id: layer.id.clone(),
            kind: layer.kind,
            position_m: Vec3::new(x_m, surface.height_m, z_m),
            yaw_radians,
            scale,
            wind_phase_radians,
            wind_response: layer.wind_response,
            slope_gradient_squared: surface.slope_gradient_squared,
            material_weight,
        }))
    }

    fn candidate_xz(&self, entropy: u64, cell_x: u16, cell_z: u16) -> (f32, f32) {
        let cells = self.vegetation.cells_per_chunk_axis;
        let chunk_x = cell_x / cells;
        let chunk_z = cell_z / cells;
        let local_x = cell_x % cells;
        let local_z = cell_z % cells;
        let cell_size_m = self.vegetation.cell_size_m();
        let chunk_min_x = -TERRAIN_HALF_EXTENT_M + f32::from(chunk_x) * VEGETATION_CHUNK_SIZE_M;
        let chunk_min_z = -TERRAIN_HALF_EXTENT_M + f32::from(chunk_z) * VEGETATION_CHUNK_SIZE_M;
        let x_m = chunk_min_x + (f32::from(local_x) + unit_f32_16(entropy, 1)) * cell_size_m;
        let z_m = chunk_min_z + (f32::from(local_z) + unit_f32_16(entropy, 2)) * cell_size_m;
        (x_m, z_m)
    }

    fn owner_chunk(&self, cell_x: u16, cell_z: u16) -> TerrainChunkCoordinate {
        TerrainChunkCoordinate {
            x: cell_x / self.vegetation.cells_per_chunk_axis,
            z: cell_z / self.vegetation.cells_per_chunk_axis,
        }
    }

    fn chunk_id(&self, coordinate: TerrainChunkCoordinate) -> VegetationChunkId {
        VegetationChunkId(EntityId::deterministic(format!(
            "vegetation-chunk-v1/{}/{}/{:016x}/{:016x}/{}/{}/{}",
            self.vegetation.project_id,
            self.vegetation.area_id,
            self.terrain.document().seed,
            self.vegetation.seed,
            self.vegetation.cells_per_chunk_axis,
            coordinate.x,
            coordinate.z
        )))
    }
}

#[must_use]
fn attempt_entropy(seed: u64, layer_id: &str, cell_x: u16, cell_z: u16, attempt: u8) -> u64 {
    let mut value = FNV_OFFSET_BASIS ^ seed;
    for byte in layer_id.bytes() {
        value ^= u64::from(byte);
        value = value.wrapping_mul(FNV_PRIME);
    }
    splitmix64(
        value
            .wrapping_add(u64::from(cell_x).wrapping_mul(CELL_X_MIX))
            .wrapping_add(u64::from(cell_z).wrapping_mul(CELL_Z_MIX))
            .wrapping_add(u64::from(attempt).wrapping_mul(ATTEMPT_MIX)),
    )
}

#[must_use]
#[allow(
    clippy::cast_precision_loss,
    reason = "the shifted value contains exactly 53 bits and is representable by f64"
)]
fn unit_f64(entropy: u64, stream: u64) -> f64 {
    let bits = splitmix64(entropy.wrapping_add(stream.wrapping_mul(STREAM_MIX)));
    ((bits >> 11) as f64) * F64_UNIT_FROM_53_BITS
}

#[must_use]
#[allow(clippy::cast_possible_truncation)]
fn unit_f32_16(entropy: u64, stream: u64) -> f32 {
    let bits = splitmix64(entropy.wrapping_add(stream.wrapping_mul(STREAM_MIX)));
    f32::from((bits >> 48) as u16) * F32_UNIT_FROM_16_BITS
}

#[must_use]
fn splitmix64(mut value: u64) -> u64 {
    value = (value ^ (value >> 30)).wrapping_mul(0xbf58_476d_1ce4_e5b9);
    value = (value ^ (value >> 27)).wrapping_mul(0x94d0_49bb_1331_11eb);
    value ^ (value >> 31)
}

#[allow(clippy::cast_possible_truncation)]
fn finite_f32(value: f64) -> Result<f32, VegetationGenerationError> {
    let converted = value as f32;
    if converted.is_finite() {
        Ok(converted)
    } else {
        Err(VegetationGenerationError::NonFiniteGeneratedValue)
    }
}

/// Deterministic bounded-neighbour spacing index for one logical layer.
struct SpacingIndex {
    minimum_spacing_squared: f64,
    bucket_size_m: f64,
    buckets: BTreeMap<(i32, i32), Vec<[f32; 2]>>,
}

impl SpacingIndex {
    fn new(minimum_spacing_m: f32) -> Self {
        Self {
            minimum_spacing_squared: f64::from(minimum_spacing_m).powi(2),
            // A bucket may be larger than the requested distance: any two points closer than the
            // requested distance still reside in the same bucket or one of its eight neighbours.
            // The floor prevents tiny-but-valid spacings from creating unbounded coordinates.
            bucket_size_m: f64::from(minimum_spacing_m).max(0.25),
            buckets: BTreeMap::new(),
        }
    }

    fn accept(&mut self, candidate: [f32; 2]) -> bool {
        if self.minimum_spacing_squared == 0.0 {
            return true;
        }
        let bucket = self.bucket(candidate);
        for offset_z in -1..=1 {
            for offset_x in -1..=1 {
                let neighbour = (bucket.0 + offset_x, bucket.1 + offset_z);
                if self.buckets.get(&neighbour).is_some_and(|points| {
                    points.iter().any(|other| {
                        let delta_x = f64::from(candidate[0]) - f64::from(other[0]);
                        let delta_z = f64::from(candidate[1]) - f64::from(other[1]);
                        delta_x * delta_x + delta_z * delta_z < self.minimum_spacing_squared
                    })
                }) {
                    return false;
                }
            }
        }
        self.buckets.entry(bucket).or_default().push(candidate);
        true
    }

    fn bucket(&self, candidate: [f32; 2]) -> (i32, i32) {
        let coordinate = |value: f32| {
            #[allow(clippy::cast_possible_truncation)]
            let bucket = ((f64::from(value) + f64::from(TERRAIN_HALF_EXTENT_M))
                / self.bucket_size_m)
                .floor() as i32;
            bucket
        };
        (coordinate(candidate[0]), coordinate(candidate[1]))
    }
}

#[must_use]
#[cfg(test)]
fn respects_spacing(candidate: [f32; 2], minimum_spacing_m: f32, accepted: &[[f32; 2]]) -> bool {
    if minimum_spacing_m == 0.0 {
        return true;
    }
    let minimum_squared = f64::from(minimum_spacing_m).powi(2);
    accepted.iter().all(|other| {
        let delta_x = f64::from(candidate[0]) - f64::from(other[0]);
        let delta_z = f64::from(candidate[1]) - f64::from(other[1]);
        delta_x * delta_x + delta_z * delta_z >= minimum_squared
    })
}

#[must_use]
fn exclusion_contains(exclusion: &VegetationExclusion, x_m: f32, z_m: f32) -> bool {
    exclusion_contains_f64(exclusion, f64::from(x_m), f64::from(z_m))
}

pub(crate) fn exclusion_contains_f64(exclusion: &VegetationExclusion, x_m: f64, z_m: f64) -> bool {
    match exclusion {
        VegetationExclusion::PolylineCorridor {
            points_xz_m,
            half_width_m,
            ..
        } => points_xz_m
            .windows(2)
            .any(|pair| corridor_contains(pair[0], pair[1], *half_width_m, x_m, z_m)),
        VegetationExclusion::Circle {
            center_x_m,
            center_z_m,
            radius_m,
            ..
        } => {
            let delta_x = x_m - f64::from(*center_x_m);
            let delta_z = z_m - f64::from(*center_z_m);
            delta_x * delta_x + delta_z * delta_z <= f64::from(*radius_m).powi(2)
        }
        VegetationExclusion::CorridorSegment {
            start_x_m,
            start_z_m,
            end_x_m,
            end_z_m,
            half_width_m,
            ..
        } => corridor_contains(
            [*start_x_m, *start_z_m],
            [*end_x_m, *end_z_m],
            *half_width_m,
            x_m,
            z_m,
        ),
    }
}

fn corridor_contains(
    start: [f32; 2],
    end: [f32; 2],
    half_width_m: f32,
    x_m: f64,
    z_m: f64,
) -> bool {
    let start_x = f64::from(start[0]);
    let start_z = f64::from(start[1]);
    let segment_x = f64::from(end[0]) - start_x;
    let segment_z = f64::from(end[1]) - start_z;
    let point_x = x_m - start_x;
    let point_z = z_m - start_z;
    let segment_length_squared = segment_x * segment_x + segment_z * segment_z;
    let amount =
        ((point_x * segment_x + point_z * segment_z) / segment_length_squared).clamp(0.0, 1.0);
    let delta_x = point_x - segment_x * amount;
    let delta_z = point_z - segment_z * amount;
    delta_x * delta_x + delta_z * delta_z <= f64::from(half_width_m).powi(2)
}

#[derive(Debug, Error)]
pub enum VegetationGenerationError {
    #[error("Imported vegetation requires its content-addressed instance resolver")]
    ImportedSourceRequiresResolver,
    #[error("promoted origins no longer resolve; explicit reconciliation required: {0:?}")]
    UnresolvedPromotions(Vec<(VegetationLayerId, EntityId)>),
    #[error("invalid promoted vegetation origins: {0}")]
    InvalidPromotions(String),
    #[error("river-derived vegetation clearances are invalid: {0}")]
    InvalidRiverExclusions(String),
    #[error("Invalid building vegetation exclusions: {0}")]
    InvalidBuildingExclusions(String),
    #[error("path-derived vegetation clearances are invalid: {0}")]
    InvalidPathExclusions(String),
    #[error("render vegetation density must be finite and within 0.1..=2.0")]
    InvalidRenderDensity,
    #[error("render vegetation placement count exceeds u32 capacity")]
    RenderDensityCountOverflow,
    #[error("vegetation document is invalid: {0}")]
    InvalidVegetationDocument(#[from] VegetationDocumentError),
    #[error("terrain document is invalid or unsupported: {0}")]
    InvalidTerrain(#[from] TerrainGenerationError),
    #[error("terrain and vegetation project identities do not match")]
    ProjectIdentityMismatch,
    #[error("terrain and vegetation area identities do not match")]
    AreaIdentityMismatch,
    #[error("terrain and vegetation extents do not match")]
    ExtentMismatch,
    #[error(
        "vegetation layer `{layer_id}` expects {material_role:?} material `{expected_material_layer_id}`, but terrain resolves `{actual_material_layer_id}`"
    )]
    MaterialRoleIdentityMismatch {
        layer_id: VegetationLayerId,
        material_role: TerrainMaterialRole,
        expected_material_layer_id: TerrainMaterialLayerId,
        actual_material_layer_id: TerrainMaterialLayerId,
    },
    #[error("vegetation chunk coordinate ({x}, {z}) is outside the 8 by 8 plan")]
    ChunkCoordinateOutOfBounds { x: u16, z: u16 },
    #[error("vegetation generation produced a non-finite f32 value")]
    NonFiniteGeneratedValue,
}

#[cfg(test)]
mod tests {
    #[test]
    fn world_sketch_corridors_exclude_scatter_without_moving_surviving_instances() {
        let compiled =
            crate::compile_world_sketch(&crate::embedded_visual_valley_world_sketch().unwrap())
                .unwrap();
        let mut before_document = compiled.vegetation.clone();
        before_document
            .exclusions
            .retain(|e| !matches!(e, VegetationExclusion::PolylineCorridor { .. }));
        let before =
            VegetationGenerationPlan::from_documents(&compiled.carved_terrain, &before_document)
                .unwrap()
                .generate()
                .unwrap();
        let after = VegetationGenerationPlan::from_documents(
            &compiled.carved_terrain,
            &compiled.vegetation,
        )
        .unwrap()
        .generate()
        .unwrap();
        let replay = VegetationGenerationPlan::from_documents(
            &compiled.carved_terrain,
            &compiled.vegetation,
        )
        .unwrap()
        .generate()
        .unwrap();
        assert_eq!(after, replay);
        let old: std::collections::HashMap<_, _> = before
            .chunks
            .iter()
            .flat_map(|c| &c.instances)
            .map(|i| (i.id, i))
            .collect();
        let mut survivors = 0;
        for instance in after.chunks.iter().flat_map(|c| &c.instances) {
            assert!(
                !compiled
                    .vegetation
                    .exclusions
                    .iter()
                    .any(|e| exclusion_contains(e, instance.position_m.x, instance.position_m.z))
            );
            if let Some(previous) = old.get(&instance.id) {
                assert_eq!(*previous, instance);
                survivors += 1;
            }
        }
        assert!(survivors > 1000);
        assert!(after.kind_counts.total() < before.kind_counts.total());
        let reopened =
            VegetationDocument::from_json(&compiled.vegetation.to_pretty_json().unwrap()).unwrap();
        assert_eq!(reopened, compiled.vegetation);
        let mut invalid = compiled.vegetation.clone();
        let VegetationExclusion::PolylineCorridor { points_xz_m, .. } = &mut invalid.exclusions[2]
        else {
            panic!("expected corridor");
        };
        points_xz_m[1] = points_xz_m[0];
        assert!(invalid.validate().is_err());
    }

    use super::*;
    use crate::VisualValleyTerrainRecipe;
    use forge_project::TerrainHeightDelta;
    use std::collections::HashSet;

    fn documents() -> (TerrainDocument, VegetationDocument) {
        (
            VisualValleyTerrainRecipe::default().build().unwrap(),
            VisualValleyVegetationRecipe::default().build().unwrap(),
        )
    }

    fn plan() -> VegetationGenerationPlan {
        let (terrain, vegetation) = documents();
        VegetationGenerationPlan::from_documents(&terrain, &vegetation).unwrap()
    }

    #[test]
    fn embedded_fixture_matches_recipe_and_all_four_original_kinds() {
        let recipe = VisualValleyVegetationRecipe::default().build().unwrap();
        let fixture = embedded_visual_valley_vegetation_document().unwrap();
        assert_eq!(fixture, recipe);
        assert_eq!(
            fixture.content_hash().unwrap(),
            recipe.content_hash().unwrap()
        );
        assert_eq!(
            fixture.content_hash().unwrap().to_string(),
            "b26f8ec68b996f41887bda3eddd9d641e861cc8bbd00645c0de2df6739e51b5c"
        );
        assert_eq!(fixture.extent_m, VegetationExtent::new(512.0, 512.0));
        assert_eq!(fixture.layers.len(), 4);
        assert_eq!(fixture.layers[0].kind, VegetationKind::Tree);
        assert_eq!(fixture.layers[1].kind, VegetationKind::Shrub);
        assert_eq!(fixture.layers[2].kind, VegetationKind::Fern);
        assert_eq!(fixture.layers[3].kind, VegetationKind::Grass);
        assert!(fixture.layers[0].collision.is_some());
        assert!(
            fixture.layers[1..]
                .iter()
                .all(|layer| layer.collision.is_none())
        );
    }

    #[test]
    fn fixture_contains_no_asset_or_renderer_reference() {
        let lowercase = VISUAL_VALLEY_VEGETATION_JSON.to_ascii_lowercase();
        for forbidden in ["asset", "cadle", "three", "bevy", "mesh", "material_path"] {
            assert!(!lowercase.contains(forbidden), "found `{forbidden}`");
        }
    }

    #[test]
    fn project_area_and_material_identity_must_match_the_terrain() {
        let (terrain, mut vegetation) = documents();
        vegetation.project_id = ProjectId::deterministic("other-project");
        assert!(matches!(
            VegetationGenerationPlan::from_documents(&terrain, &vegetation),
            Err(VegetationGenerationError::ProjectIdentityMismatch)
        ));

        let (_, mut vegetation) = documents();
        vegetation.area_id = AreaId::deterministic("other-area");
        assert!(matches!(
            VegetationGenerationPlan::from_documents(&terrain, &vegetation),
            Err(VegetationGenerationError::AreaIdentityMismatch)
        ));

        let (_, mut vegetation) = documents();
        vegetation.layers[0].material_filter = Some(VegetationMaterialFilter::new(
            TerrainMaterialRole::Grass,
            TerrainMaterialLayerId::new("unknown_material"),
            0.5,
        ));
        assert!(matches!(
            VegetationGenerationPlan::from_documents(&terrain, &vegetation),
            Err(VegetationGenerationError::MaterialRoleIdentityMismatch { .. })
        ));

        let (mut reordered_terrain, vegetation) = documents();
        reordered_terrain.material_layers.swap(0, 2);
        assert!(matches!(
            VegetationGenerationPlan::from_documents(&reordered_terrain, &vegetation),
            Err(VegetationGenerationError::MaterialRoleIdentityMismatch { .. })
        ));
    }

    #[test]
    fn replay_is_bit_identical_and_seed_changes_ids_and_output() {
        let (terrain, vegetation) = documents();
        let first = VegetationGenerationPlan::from_documents(&terrain, &vegetation)
            .unwrap()
            .generate()
            .unwrap();
        let replay = VegetationGenerationPlan::from_documents(&terrain, &vegetation)
            .unwrap()
            .generate()
            .unwrap();
        assert_eq!(first, replay);
        assert_eq!(
            first.content_hash().unwrap(),
            replay.content_hash().unwrap()
        );

        let changed = VegetationDocument {
            seed: vegetation.seed + 1,
            ..vegetation
        };
        let changed = VegetationGenerationPlan::from_documents(&terrain, &changed)
            .unwrap()
            .generate()
            .unwrap();
        assert_ne!(first, changed);
        assert_ne!(first.chunks[0].id, changed.chunks[0].id);
        assert_ne!(
            first
                .chunks
                .iter()
                .find_map(|chunk| chunk.instances.first())
                .unwrap()
                .id,
            changed
                .chunks
                .iter()
                .find_map(|chunk| chunk.instances.first())
                .unwrap()
                .id
        );
    }

    #[test]
    fn generated_values_are_finite_bounded_filtered_spaced_and_ids_are_unique() {
        let plan = plan();
        let generated = plan.generate().unwrap();
        let layers = &plan.vegetation().layers;
        let mut ids = HashSet::new();
        let mut chunk_ids = HashSet::new();
        let mut counted = VegetationKindCounts::default();
        let mut by_layer = vec![Vec::<[f32; 2]>::new(); layers.len()];
        for chunk in &generated.chunks {
            assert!(chunk_ids.insert(chunk.id));
            let minimum_x =
                -TERRAIN_HALF_EXTENT_M + f32::from(chunk.coordinate.x) * VEGETATION_CHUNK_SIZE_M;
            let minimum_z =
                -TERRAIN_HALF_EXTENT_M + f32::from(chunk.coordinate.z) * VEGETATION_CHUNK_SIZE_M;
            let maximum_x = minimum_x + VEGETATION_CHUNK_SIZE_M;
            let maximum_z = minimum_z + VEGETATION_CHUNK_SIZE_M;
            for instance in &chunk.instances {
                assert!(ids.insert(instance.id));
                assert!(instance.position_m.is_finite());
                assert!((minimum_x..maximum_x).contains(&instance.position_m.x));
                assert!((minimum_z..maximum_z).contains(&instance.position_m.z));
                assert!((0.0..std::f32::consts::TAU).contains(&instance.yaw_radians));
                assert!((0.0..std::f32::consts::TAU).contains(&instance.wind_phase_radians));
                let (layer_index, layer) = layers
                    .iter()
                    .enumerate()
                    .find(|(_, layer)| layer.id == instance.layer_id)
                    .unwrap();
                assert!((layer.minimum_scale..=layer.maximum_scale).contains(&instance.scale));
                assert!(
                    (layer.minimum_altitude_m..=layer.maximum_altitude_m)
                        .contains(&instance.position_m.y)
                );
                assert!(
                    instance.slope_gradient_squared
                        <= f64::from(layer.maximum_slope_gradient).powi(2)
                );
                assert_eq!(
                    instance.wind_response.to_bits(),
                    layer.wind_response.to_bits()
                );
                if let (Some(filter), Some(weight)) =
                    (&layer.material_filter, instance.material_weight)
                {
                    assert!(weight.is_finite() && weight >= filter.minimum_weight);
                }
                assert!(
                    !plan
                        .vegetation()
                        .exclusions
                        .iter()
                        .any(|exclusion| exclusion_contains(
                            exclusion,
                            instance.position_m.x,
                            instance.position_m.z
                        ))
                );
                assert!(respects_spacing(
                    [instance.position_m.x, instance.position_m.z],
                    layer.minimum_spacing_m,
                    &by_layer[layer_index]
                ));
                by_layer[layer_index].push([instance.position_m.x, instance.position_m.z]);

                let surface = plan
                    .terrain()
                    .surface_at(instance.position_m.x, instance.position_m.z)
                    .unwrap();
                assert_eq!(instance.position_m.y.to_bits(), surface.height_m.to_bits());
                counted.increment(instance.kind);
            }
        }
        assert_eq!(chunk_ids.len(), 64);
        assert_eq!(generated.kind_counts, counted);
        assert_eq!(
            u32::try_from(ids.len()).unwrap(),
            generated.kind_counts.total()
        );
        assert_eq!(
            generated.kind_counts,
            VegetationKindCounts {
                trees: 704,
                shrubs: 1_228,
                ferns: 1_282,
                grass_patches: 21_431,
            }
        );
        assert_eq!(
            generated.content_hash().unwrap().to_string(),
            "c24eee7a0a6f19e249436c568c8e5f1403cc6ec9013de5ef81554cf465eefa5f"
        );
        for kind in [
            VegetationKind::Tree,
            VegetationKind::Shrub,
            VegetationKind::Fern,
            VegetationKind::Grass,
        ] {
            assert!(generated.kind_counts.count(kind) > 0, "missing {kind:?}");
        }
    }

    #[test]
    fn global_chunks_equal_generate_chunk_exactly() {
        let plan = plan();
        let reverse_chunks = (0..TERRAIN_CHUNKS_PER_AXIS)
            .rev()
            .flat_map(|z| {
                (0..TERRAIN_CHUNKS_PER_AXIS)
                    .rev()
                    .map(move |x| TerrainChunkCoordinate { x, z })
            })
            .map(|coordinate| plan.generate_chunk(coordinate).unwrap())
            .collect::<Vec<_>>();
        let generated = plan.generate().unwrap();
        assert_eq!(generated.chunks.len(), 64);
        assert_eq!(generated.cells_per_axis, 64);
        for chunk in reverse_chunks {
            let index =
                usize::from(chunk.coordinate.z * TERRAIN_CHUNKS_PER_AXIS + chunk.coordinate.x);
            assert_eq!(chunk, generated.chunks[index]);
        }
        for chunk in &generated.chunks {
            assert_eq!(*chunk, plan.generate_chunk(chunk.coordinate).unwrap());
        }
        assert!(matches!(
            plan.generate_chunk(TerrainChunkCoordinate { x: 8, z: 0 }),
            Err(VegetationGenerationError::ChunkCoordinateOutOfBounds { x: 8, z: 0 })
        ));
    }

    #[test]
    fn authored_terrain_edit_preserves_surviving_attempt_ids() {
        let (terrain, vegetation) = documents();
        let before = VegetationGenerationPlan::from_documents(&terrain, &vegetation)
            .unwrap()
            .generate()
            .unwrap();
        let mut edited_terrain = terrain.migrate_to_latest().unwrap();
        edited_terrain.height_deltas = vec![TerrainHeightDelta::new(0, 0, 2.0)];
        let after = VegetationGenerationPlan::from_documents(&edited_terrain, &vegetation)
            .unwrap()
            .generate()
            .unwrap();

        assert_ne!(before.terrain_document_hash, after.terrain_document_hash);
        assert_eq!(
            before.vegetation_document_hash,
            after.vegetation_document_hash
        );
        let before_ids = before
            .chunks
            .iter()
            .flat_map(|chunk| chunk.instances.iter().map(|instance| instance.id))
            .collect::<HashSet<_>>();
        let after_ids = after
            .chunks
            .iter()
            .flat_map(|chunk| chunk.instances.iter().map(|instance| instance.id))
            .collect::<HashSet<_>>();
        let surviving = before_ids.intersection(&after_ids).count();
        assert!(surviving > 0);
        assert!(surviving >= before_ids.len().saturating_sub(16));
    }

    #[test]
    fn spacing_index_checks_same_and_neighbour_buckets() {
        let mut index = SpacingIndex::new(2.0);
        assert!(index.accept([-0.9, 0.0]));
        assert!(!index.accept([0.9, 0.0]));
        assert!(index.accept([1.1, 2.0]));
        assert!(index.accept([3.1, 0.0]));
    }

    #[test]
    fn zero_and_full_acceptance_have_exact_attempt_semantics() {
        let (terrain, mut vegetation) = documents();
        vegetation.layers.truncate(1);
        vegetation.layers[0].acceptance_probability = 0.0;
        vegetation.exclusions.clear();
        let empty = VegetationGenerationPlan::from_documents(&terrain, &vegetation)
            .unwrap()
            .generate()
            .unwrap();
        assert_eq!(empty.kind_counts.total(), 0);

        vegetation.layers[0] = VegetationLayer::new(
            "forge_grass",
            "Forge grass patches",
            VegetationKind::Grass,
            1.0,
            1,
            0.0,
            1.0,
            1.0,
            16.0,
            -10_000.0,
            10_000.0,
            None,
            None,
            0.0,
        );
        let full = VegetationGenerationPlan::from_documents(&terrain, &vegetation)
            .unwrap()
            .generate()
            .unwrap();
        assert_eq!(full.kind_counts.grass_patches, 64 * 64);
    }
}
