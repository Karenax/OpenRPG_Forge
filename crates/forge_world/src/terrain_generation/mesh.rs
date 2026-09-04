use super::sampling::{material_weights_at, sample_height_unchecked, sample_normal};
use super::{
    MATERIAL_ROLE_COUNT, TERRAIN_CHUNK_COUNT, TERRAIN_CHUNK_INTERVALS,
    TERRAIN_CHUNK_VERTICES_PER_AXIS, TERRAIN_CHUNKS_PER_AXIS, TERRAIN_GLOBAL_INTERVALS_PER_AXIS,
    TERRAIN_GLOBAL_VERTICES_PER_AXIS, TERRAIN_HALF_EXTENT_M, TERRAIN_SAMPLE_SPACING_M,
    TerrainChunkCoordinate, TerrainChunkId, TerrainGenerationError, TerrainGenerationPlan,
};
use forge_core::{CanonicalJsonError, ContentHash, canonical_json_sha256};
use forge_project::TerrainMaterialLayerId;
use serde::{Deserialize, Serialize};

/// Three normalized logical surface weights in grass, earth, rock order.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainMaterialWeights {
    pub grass: f32,
    pub earth: f32,
    pub rock: f32,
}

impl TerrainMaterialWeights {
    #[must_use]
    pub const fn as_array(self) -> [f32; MATERIAL_ROLE_COUNT] {
        [self.grass, self.earth, self.rock]
    }
}

/// Explicit ordering of collider vertices.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum TerrainColliderVertexOrder {
    /// Rows advance along +Z; values within a row advance along +X.
    RowMajorZThenX,
}

/// Explicit ordering and winding of collider triangles.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum TerrainColliderTriangleOrder {
    /// Cells advance row-major and each cell emits two counter-clockwise triangles from above.
    CellRowMajorZThenXCounterClockwiseFromAbove,
}

/// Renderer- and physics-engine-neutral triangle mesh for a local terrain collider.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainChunkColliderData {
    pub vertex_order: TerrainColliderVertexOrder,
    pub triangle_order: TerrainColliderTriangleOrder,
    pub vertices_m: Vec<[f32; 3]>,
    pub triangles: Vec<[u32; 3]>,
}

/// One self-contained render payload. Positions are absolute Forge world metres.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainChunkMesh {
    pub id: TerrainChunkId,
    pub coordinate: TerrainChunkCoordinate,
    pub minimum_xz_m: [f32; 2],
    pub maximum_xz_m: [f32; 2],
    pub positions_m: Vec<[f32; 3]>,
    pub normals: Vec<[f32; 3]>,
    pub uvs: Vec<[f32; 2]>,
    pub material_weights: Vec<TerrainMaterialWeights>,
    pub indices: Vec<u32>,
    pub collider: TerrainChunkColliderData,
}

impl TerrainChunkMesh {
    #[must_use]
    pub fn triangle_count(&self) -> usize {
        self.indices.len() / 3
    }
}

/// Complete deterministic derived terrain cache for one document revision.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GeneratedTerrain {
    pub source_document_hash: ContentHash,
    /// Minimum global `[x, z]` coordinate. Visual Valley is centred at the world origin.
    pub origin_xz_m: [f32; 2],
    /// Logical material IDs aligned with grass, earth and rock weights.
    pub material_layer_ids: [TerrainMaterialLayerId; MATERIAL_ROLE_COUNT],
    pub chunks: Vec<TerrainChunkMesh>,
}

impl GeneratedTerrain {
    #[must_use]
    pub fn vertex_count(&self) -> usize {
        self.chunks
            .iter()
            .map(|chunk| chunk.positions_m.len())
            .sum()
    }

    #[must_use]
    pub fn triangle_count(&self) -> usize {
        self.chunks
            .iter()
            .map(TerrainChunkMesh::triangle_count)
            .sum()
    }

    pub fn content_hash(&self) -> Result<ContentHash, CanonicalJsonError> {
        canonical_json_sha256(self)
    }
}

impl TerrainGenerationPlan {
    /// Builds all chunk payloads in stable row-major Z-then-X order.
    pub fn generate(&self) -> Result<GeneratedTerrain, TerrainGenerationError> {
        let global = GlobalTerrainGrid::build(self);
        let mut chunks = Vec::with_capacity(TERRAIN_CHUNK_COUNT);
        for z in 0..TERRAIN_CHUNKS_PER_AXIS {
            for x in 0..TERRAIN_CHUNKS_PER_AXIS {
                chunks.push(self.generate_chunk(TerrainChunkCoordinate { x, z }, &global));
            }
        }

        Ok(GeneratedTerrain {
            source_document_hash: self.document_hash,
            origin_xz_m: self.origin_xz_m(),
            material_layer_ids: [
                self.document.material_layers[0].id.clone(),
                self.document.material_layers[1].id.clone(),
                self.document.material_layers[2].id.clone(),
            ],
            chunks,
        })
    }

    fn generate_chunk(
        &self,
        coordinate: TerrainChunkCoordinate,
        global: &GlobalTerrainGrid,
    ) -> TerrainChunkMesh {
        let vertex_capacity = usize::from(TERRAIN_CHUNK_VERTICES_PER_AXIS).pow(2);
        let triangle_capacity = usize::from(TERRAIN_CHUNK_INTERVALS).pow(2) * 2;
        let mut positions_m = Vec::with_capacity(vertex_capacity);
        let mut normals = Vec::with_capacity(vertex_capacity);
        let mut uvs = Vec::with_capacity(vertex_capacity);
        let mut material_weights = Vec::with_capacity(vertex_capacity);

        let first_global_x = coordinate.x * TERRAIN_CHUNK_INTERVALS;
        let first_global_z = coordinate.z * TERRAIN_CHUNK_INTERVALS;
        for local_z in 0..TERRAIN_CHUNK_VERTICES_PER_AXIS {
            for local_x in 0..TERRAIN_CHUNK_VERTICES_PER_AXIS {
                let global_x = first_global_x + local_x;
                let global_z = first_global_z + local_z;
                let index = global_grid_index(global_x, global_z);
                positions_m.push(global.positions_m[index]);
                normals.push(global.normals[index]);
                uvs.push(global.uvs[index]);
                material_weights.push(global.material_weights[index]);
            }
        }

        let mut indices = Vec::with_capacity(triangle_capacity * 3);
        let mut triangles = Vec::with_capacity(triangle_capacity);
        for local_z in 0..TERRAIN_CHUNK_INTERVALS {
            for local_x in 0..TERRAIN_CHUNK_INTERVALS {
                let a = u32::from(local_z) * u32::from(TERRAIN_CHUNK_VERTICES_PER_AXIS)
                    + u32::from(local_x);
                let b = a + 1;
                let c = a + u32::from(TERRAIN_CHUNK_VERTICES_PER_AXIS);
                let d = c + 1;
                let first = [a, c, b];
                let second = [b, c, d];
                indices.extend_from_slice(&first);
                indices.extend_from_slice(&second);
                triangles.push(first);
                triangles.push(second);
            }
        }

        let minimum_x = world_coordinate(first_global_x);
        let minimum_z = world_coordinate(first_global_z);
        let maximum_x = world_coordinate(first_global_x + TERRAIN_CHUNK_INTERVALS);
        let maximum_z = world_coordinate(first_global_z + TERRAIN_CHUNK_INTERVALS);
        let collider = TerrainChunkColliderData {
            vertex_order: TerrainColliderVertexOrder::RowMajorZThenX,
            triangle_order:
                TerrainColliderTriangleOrder::CellRowMajorZThenXCounterClockwiseFromAbove,
            vertices_m: positions_m.clone(),
            triangles,
        };

        TerrainChunkMesh {
            id: self.chunk_id(coordinate),
            coordinate,
            minimum_xz_m: [minimum_x, minimum_z],
            maximum_xz_m: [maximum_x, maximum_z],
            positions_m,
            normals,
            uvs,
            material_weights,
            indices,
            collider,
        }
    }
}

#[derive(Debug)]
struct GlobalTerrainGrid {
    positions_m: Vec<[f32; 3]>,
    normals: Vec<[f32; 3]>,
    uvs: Vec<[f32; 2]>,
    material_weights: Vec<TerrainMaterialWeights>,
}

impl GlobalTerrainGrid {
    fn build(plan: &TerrainGenerationPlan) -> Self {
        let capacity = usize::from(TERRAIN_GLOBAL_VERTICES_PER_AXIS).pow(2);
        let mut positions_m = Vec::with_capacity(capacity);
        let mut normals = Vec::with_capacity(capacity);
        let mut uvs = Vec::with_capacity(capacity);
        let mut material_weights = Vec::with_capacity(capacity);

        for global_z in 0..TERRAIN_GLOBAL_VERTICES_PER_AXIS {
            for global_x in 0..TERRAIN_GLOBAL_VERTICES_PER_AXIS {
                let x_m = world_coordinate(global_x);
                let z_m = world_coordinate(global_z);
                let height_m = sample_height_unchecked(&plan.document, x_m, z_m);
                let normal = sample_normal(&plan.document, x_m, z_m);
                positions_m.push([x_m, height_m, z_m]);
                normals.push(normal);
                uvs.push([
                    f32::from(global_x) / f32::from(TERRAIN_GLOBAL_INTERVALS_PER_AXIS),
                    f32::from(global_z) / f32::from(TERRAIN_GLOBAL_INTERVALS_PER_AXIS),
                ]);
                material_weights.push(material_weights_at(
                    &plan.document,
                    global_x,
                    global_z,
                    height_m,
                    normal[1],
                ));
            }
        }

        Self {
            positions_m,
            normals,
            uvs,
            material_weights,
        }
    }
}

#[must_use]
fn global_grid_index(x: u16, z: u16) -> usize {
    usize::from(z) * usize::from(TERRAIN_GLOBAL_VERTICES_PER_AXIS) + usize::from(x)
}

#[must_use]
fn world_coordinate(sample: u16) -> f32 {
    -TERRAIN_HALF_EXTENT_M + f32::from(sample) * TERRAIN_SAMPLE_SPACING_M
}
