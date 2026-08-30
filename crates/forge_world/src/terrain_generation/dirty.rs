use super::{
    TERRAIN_CHUNK_INTERVALS, TERRAIN_CHUNKS_PER_AXIS, TERRAIN_GLOBAL_INTERVALS_PER_AXIS,
    TerrainChunkCoordinate, TerrainChunkId, TerrainGenerationError, TerrainGenerationPlan,
};
use serde::{Deserialize, Serialize};

/// Inclusive rectangle of samples in the unique 257 by 257 source grid.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainSampleRegion {
    pub minimum_x: u16,
    pub minimum_z: u16,
    pub maximum_x: u16,
    pub maximum_z: u16,
}

impl TerrainSampleRegion {
    pub fn new(
        minimum_x: u16,
        minimum_z: u16,
        maximum_x: u16,
        maximum_z: u16,
    ) -> Result<Self, TerrainGenerationError> {
        if minimum_x > maximum_x || minimum_z > maximum_z {
            return Err(TerrainGenerationError::InvertedDirtyRegion);
        }
        Ok(Self {
            minimum_x,
            minimum_z,
            maximum_x,
            maximum_z,
        })
    }

    #[must_use]
    pub const fn point(x: u16, z: u16) -> Self {
        Self {
            minimum_x: x,
            minimum_z: z,
            maximum_x: x,
            maximum_z: z,
        }
    }
}

/// Stable identity and coordinate of a chunk selected for partial rebuilding.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainDirtyChunk {
    pub id: TerrainChunkId,
    pub coordinate: TerrainChunkCoordinate,
}

/// Deterministic partial-rebuild plan including one sample of normal dependency padding.
#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainDirtyPlan {
    pub requested_samples: TerrainSampleRegion,
    /// Bounding rectangle of the requested samples and their cardinal normal dependencies.
    /// `affected_chunks` is computed from the exact cross-shaped dependency, not its corners.
    pub dependency_samples: TerrainSampleRegion,
    pub affected_chunks: Vec<TerrainDirtyChunk>,
}

impl TerrainGenerationPlan {
    /// Selects the exact chunk set whose vertices or finite-difference normals can change.
    pub fn plan_dirty_region(
        &self,
        requested_samples: TerrainSampleRegion,
    ) -> Result<TerrainDirtyPlan, TerrainGenerationError> {
        if requested_samples.maximum_x > TERRAIN_GLOBAL_INTERVALS_PER_AXIS
            || requested_samples.maximum_z > TERRAIN_GLOBAL_INTERVALS_PER_AXIS
        {
            return Err(TerrainGenerationError::DirtyRegionOutOfBounds);
        }
        if requested_samples.minimum_x > requested_samples.maximum_x
            || requested_samples.minimum_z > requested_samples.maximum_z
        {
            return Err(TerrainGenerationError::InvertedDirtyRegion);
        }

        let dependency_samples = TerrainSampleRegion {
            minimum_x: requested_samples.minimum_x.saturating_sub(1),
            minimum_z: requested_samples.minimum_z.saturating_sub(1),
            maximum_x: requested_samples
                .maximum_x
                .saturating_add(1)
                .min(TERRAIN_GLOBAL_INTERVALS_PER_AXIS),
            maximum_z: requested_samples
                .maximum_z
                .saturating_add(1)
                .min(TERRAIN_GLOBAL_INTERVALS_PER_AXIS),
        };

        let mut affected_chunks = Vec::with_capacity(4);
        for z in 0..TERRAIN_CHUNKS_PER_AXIS {
            for x in 0..TERRAIN_CHUNKS_PER_AXIS {
                let coordinate = TerrainChunkCoordinate { x, z };
                if chunk_intersects_dirty_cross(coordinate, requested_samples) {
                    affected_chunks.push(TerrainDirtyChunk {
                        id: self.chunk_id(coordinate),
                        coordinate,
                    });
                }
            }
        }

        Ok(TerrainDirtyPlan {
            requested_samples,
            dependency_samples,
            affected_chunks,
        })
    }
}

#[must_use]
fn chunk_intersects_dirty_cross(
    coordinate: TerrainChunkCoordinate,
    requested: TerrainSampleRegion,
) -> bool {
    if chunk_intersects_region(coordinate, requested) {
        return true;
    }

    if requested.minimum_x > 0 {
        let left = TerrainSampleRegion {
            minimum_x: requested.minimum_x - 1,
            maximum_x: requested.maximum_x - 1,
            ..requested
        };
        if chunk_intersects_region(coordinate, left) {
            return true;
        }
    }
    if requested.maximum_x < TERRAIN_GLOBAL_INTERVALS_PER_AXIS {
        let right = TerrainSampleRegion {
            minimum_x: requested.minimum_x + 1,
            maximum_x: requested.maximum_x + 1,
            ..requested
        };
        if chunk_intersects_region(coordinate, right) {
            return true;
        }
    }
    if requested.minimum_z > 0 {
        let back = TerrainSampleRegion {
            minimum_z: requested.minimum_z - 1,
            maximum_z: requested.maximum_z - 1,
            ..requested
        };
        if chunk_intersects_region(coordinate, back) {
            return true;
        }
    }
    if requested.maximum_z < TERRAIN_GLOBAL_INTERVALS_PER_AXIS {
        let front = TerrainSampleRegion {
            minimum_z: requested.minimum_z + 1,
            maximum_z: requested.maximum_z + 1,
            ..requested
        };
        if chunk_intersects_region(coordinate, front) {
            return true;
        }
    }

    false
}

#[must_use]
fn chunk_intersects_region(
    coordinate: TerrainChunkCoordinate,
    region: TerrainSampleRegion,
) -> bool {
    let minimum_x = coordinate.x * TERRAIN_CHUNK_INTERVALS;
    let minimum_z = coordinate.z * TERRAIN_CHUNK_INTERVALS;
    let maximum_x = minimum_x + TERRAIN_CHUNK_INTERVALS;
    let maximum_z = minimum_z + TERRAIN_CHUNK_INTERVALS;
    region.minimum_x <= maximum_x
        && region.maximum_x >= minimum_x
        && region.minimum_z <= maximum_z
        && region.maximum_z >= minimum_z
}
