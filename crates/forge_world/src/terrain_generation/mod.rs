mod dirty;
mod mesh;
mod partial;
mod sampling;

#[cfg(test)]
mod tests;

pub use dirty::{TerrainDirtyChunk, TerrainDirtyPlan, TerrainSampleRegion};
pub use mesh::{
    GeneratedTerrain, TerrainChunkColliderData, TerrainChunkMesh, TerrainColliderTriangleOrder,
    TerrainColliderVertexOrder, TerrainMaterialWeights,
};
pub use partial::{
    TerrainHeightSampleCoordinate, TerrainPartialUpdate, TerrainPartialUpdateCommit,
    TerrainPartialUpdateError, TerrainPartialUpdateRequest,
};
pub use sampling::sample_height;

use forge_core::{ContentHash, EntityId};
use forge_project::{
    HeightSamplingAlgorithm, TerrainDocument, TerrainDocumentError, TerrainMaterialLayerId,
    TerrainMaterialRole,
};
use serde::{Deserialize, Serialize};
use std::fmt;
use thiserror::Error;

/// Width and depth of the first Visual Valley terrain contract.
pub const TERRAIN_EXTENT_M: f32 = 512.0;
/// Distance from the centred origin to every horizontal edge.
pub const TERRAIN_HALF_EXTENT_M: f32 = 256.0;
/// Distance between adjacent source height samples.
pub const TERRAIN_SAMPLE_SPACING_M: f32 = 2.0;
/// Number of chunks on both horizontal axes.
pub const TERRAIN_CHUNKS_PER_AXIS: u16 = 8;
/// Number of quad intervals carried by one chunk on each axis.
pub const TERRAIN_CHUNK_INTERVALS: u16 = 32;
/// Number of vertices carried by one chunk on each axis, including duplicated seams.
pub const TERRAIN_CHUNK_VERTICES_PER_AXIS: u16 = TERRAIN_CHUNK_INTERVALS + 1;
/// Number of source intervals across the complete terrain.
pub const TERRAIN_GLOBAL_INTERVALS_PER_AXIS: u16 =
    TERRAIN_CHUNKS_PER_AXIS * TERRAIN_CHUNK_INTERVALS;
/// Number of unique source vertices across the complete terrain.
pub const TERRAIN_GLOBAL_VERTICES_PER_AXIS: u16 = TERRAIN_GLOBAL_INTERVALS_PER_AXIS + 1;
/// Stable number of generated chunks.
pub const TERRAIN_CHUNK_COUNT: usize = 64;
/// Stable number of chunk-local vertices, including duplicated seams.
pub const TERRAIN_VERTEX_COUNT: usize = 69_696;
/// Stable number of generated triangles.
pub const TERRAIN_TRIANGLE_COUNT: usize = 131_072;

const MATERIAL_ROLE_COUNT: usize = 3;
const TERRAIN_PADDED_MINIMUM_M: f32 = -TERRAIN_HALF_EXTENT_M - TERRAIN_SAMPLE_SPACING_M;
const TERRAIN_PADDED_MAXIMUM_M: f32 = TERRAIN_HALF_EXTENT_M + TERRAIN_SAMPLE_SPACING_M;

/// Exact point sample on the same triangle surface emitted to rendering and collision adapters.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainSurfaceSample {
    pub height_m: f32,
    /// Geometric normal of the selected collider triangle.
    pub normal: [f32; 3],
    /// Squared rise/run gradient of that triangle, computed without trigonometry.
    pub slope_gradient_squared: f64,
    /// Barycentrically interpolated and normalized logical material weights.
    pub material_weights: TerrainMaterialWeights,
}

/// Stable integer coordinate of one terrain chunk.
#[derive(Clone, Copy, Debug, Eq, Hash, Ord, PartialEq, PartialOrd, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainChunkCoordinate {
    pub x: u16,
    pub z: u16,
}

impl TerrainChunkCoordinate {
    /// Creates a coordinate inside the fixed 8 by 8 terrain plan.
    pub fn new(x: u16, z: u16) -> Result<Self, TerrainGenerationError> {
        let coordinate = Self { x, z };
        if coordinate.is_in_bounds() {
            Ok(coordinate)
        } else {
            Err(TerrainGenerationError::ChunkCoordinateOutOfBounds { x, z })
        }
    }

    #[must_use]
    pub const fn is_in_bounds(self) -> bool {
        self.x < TERRAIN_CHUNKS_PER_AXIS && self.z < TERRAIN_CHUNKS_PER_AXIS
    }
}

/// Engine-neutral stable identity for a generated chunk.
#[derive(Clone, Copy, Debug, Eq, Hash, Ord, PartialEq, PartialOrd, Serialize, Deserialize)]
#[serde(transparent)]
pub struct TerrainChunkId(EntityId);

impl TerrainChunkId {
    #[must_use]
    pub const fn as_entity_id(self) -> EntityId {
        self.0
    }
}

impl fmt::Display for TerrainChunkId {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        self.0.fmt(formatter)
    }
}

/// Validated, reusable interpretation of `TerrainDocument` version 1 for Visual Valley.
#[derive(Clone, Debug)]
pub struct TerrainGenerationPlan {
    document: TerrainDocument,
    document_hash: ContentHash,
}

impl TerrainGenerationPlan {
    /// Validates the exact first-generation 512 m, 2 m sampling contract.
    pub fn from_document(document: &TerrainDocument) -> Result<Self, TerrainGenerationError> {
        document.validate()?;
        if document.extent_m.size_x_m.to_bits() != TERRAIN_EXTENT_M.to_bits()
            || document.extent_m.size_z_m.to_bits() != TERRAIN_EXTENT_M.to_bits()
        {
            return Err(TerrainGenerationError::UnsupportedExtent);
        }
        if document.height_sampling.sample_spacing_m.to_bits() != TERRAIN_SAMPLE_SPACING_M.to_bits()
        {
            return Err(TerrainGenerationError::UnsupportedSampleSpacing);
        }
        if document.height_sampling.algorithm != HeightSamplingAlgorithm::FractalValueNoiseV1 {
            return Err(TerrainGenerationError::UnsupportedHeightAlgorithm);
        }
        if document.material_layers.len() != MATERIAL_ROLE_COUNT {
            return Err(TerrainGenerationError::UnsupportedMaterialLayerCount(
                document.material_layers.len(),
            ));
        }
        sampling::validate_arithmetic_contract(document)?;

        Ok(Self {
            document_hash: document.content_hash()?,
            document: document.clone(),
        })
    }

    #[must_use]
    pub const fn origin_xz_m(&self) -> [f32; 2] {
        [-TERRAIN_HALF_EXTENT_M, -TERRAIN_HALF_EXTENT_M]
    }

    #[must_use]
    pub const fn document(&self) -> &TerrainDocument {
        &self.document
    }

    #[must_use]
    pub const fn document_hash(&self) -> ContentHash {
        self.document_hash
    }

    /// Resolves one stable semantic role to the corresponding authored material identity.
    #[must_use]
    pub fn material_layer_id(&self, role: TerrainMaterialRole) -> &TerrainMaterialLayerId {
        &self.document.material_layers[role.index()].id
    }

    /// Samples the versioned height function inside the terrain plus one normal-padding sample.
    pub fn height_at(&self, x_m: f32, z_m: f32) -> Result<f32, TerrainGenerationError> {
        if !x_m.is_finite() || !z_m.is_finite() {
            return Err(TerrainGenerationError::NonFiniteSampleCoordinate);
        }
        if !(TERRAIN_PADDED_MINIMUM_M..=TERRAIN_PADDED_MAXIMUM_M).contains(&x_m)
            || !(TERRAIN_PADDED_MINIMUM_M..=TERRAIN_PADDED_MAXIMUM_M).contains(&z_m)
        {
            return Err(TerrainGenerationError::SampleCoordinateOutOfBounds);
        }
        Ok(sampling::sample_height_unchecked(&self.document, x_m, z_m))
    }

    /// Samples the exact 2 metre triangle mesh surface, including interpolated material weights.
    pub fn surface_at(
        &self,
        x_m: f32,
        z_m: f32,
    ) -> Result<TerrainSurfaceSample, TerrainGenerationError> {
        sampling::sample_surface(self, x_m, z_m)
    }

    fn chunk_id(&self, coordinate: TerrainChunkCoordinate) -> TerrainChunkId {
        TerrainChunkId(EntityId::deterministic(format!(
            "terrain-generation-v1/{}/{}/{:016x}/{}/{}",
            self.document.project_id,
            self.document.area_id,
            self.document.seed,
            coordinate.x,
            coordinate.z
        )))
    }
}

#[derive(Debug, Error)]
pub enum TerrainGenerationError {
    #[error("terrain document is invalid: {0}")]
    InvalidDocument(#[from] TerrainDocumentError),
    #[error("terrain generation v1 requires a 512 by 512 metre extent")]
    UnsupportedExtent,
    #[error("terrain generation v1 requires exactly 2 metre sample spacing")]
    UnsupportedSampleSpacing,
    #[error("terrain generation v1 only supports FractalValueNoiseV1")]
    UnsupportedHeightAlgorithm,
    #[error("terrain generation v1 requires exactly three logical material layers, found {0}")]
    UnsupportedMaterialLayerCount(usize),
    #[error("terrain height range cannot be represented as finite f32 output")]
    HeightOutputOutOfRange,
    #[error(
        "terrain octave {octave} exceeds the exact f64 lattice-coordinate range required for deterministic sampling"
    )]
    OctaveLatticeCoordinateOutOfRange { octave: u8 },
    #[error("terrain sample coordinates must be finite")]
    NonFiniteSampleCoordinate,
    #[error("terrain sample coordinate exceeds the 512 metre extent and one-sample normal padding")]
    SampleCoordinateOutOfBounds,
    #[error("terrain surface coordinate exceeds the closed 512 metre extent")]
    SurfaceCoordinateOutOfBounds,
    #[error("terrain surface interpolation cannot be represented as finite f32 output")]
    SurfaceOutputOutOfRange,
    #[error("chunk coordinate ({x}, {z}) is outside the 8 by 8 plan")]
    ChunkCoordinateOutOfBounds { x: u16, z: u16 },
    #[error("dirty sample region has inverted bounds")]
    InvertedDirtyRegion,
    #[error("dirty sample region exceeds the 0 through 256 source grid")]
    DirtyRegionOutOfBounds,
}
