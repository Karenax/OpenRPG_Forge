//! Procedural samples projected through the shared multi-cell terrain topology.
use super::{TerrainHlodTile, mesh::tile_topology};
use crate::{
    TERRAIN_CHUNK_INTERVALS, TERRAIN_CHUNKS_PER_AXIS, TerrainMaterialWeights,
    cell_streaming::ProceduralCellSource,
    terrain_generation::{SurfaceVertex, grid_uv, surface_vertex},
};
use forge_core::ContentHash;

/// Presentation-only geometry for an aggregated set of procedural cells.
/// CPU buffer accounting excludes the shared source, temporary preparation,
/// allocator overhead, GPU memory and physics; no collider is produced.
#[derive(Debug)]
pub struct ProceduralTerrainHlodMesh {
    pub tile: TerrainHlodTile,
    /// Source pin shared with the fine-cell controller, including its distant stride.
    pub source: ContentHash,
    /// Interior stride for this hierarchy mesh, independently of the fine-cell stride.
    pub sample_stride: u8,
    pub positions_m: Vec<[f32; 3]>,
    pub normals: Vec<[f32; 3]>,
    pub uvs: Vec<[f32; 2]>,
    pub material_weights: Vec<TerrainMaterialWeights>,
    pub indices: Vec<u32>,
}

impl ProceduralTerrainHlodMesh {
    #[must_use]
    pub fn bytes(&self) -> u64 {
        (self.positions_m.capacity() * size_of::<[f32; 3]>()
            + self.normals.capacity() * size_of::<[f32; 3]>()
            + self.uvs.capacity() * size_of::<[f32; 2]>()
            + self.material_weights.capacity() * size_of::<TerrainMaterialWeights>()
            + self.indices.capacity() * size_of::<u32>()) as u64
    }
}

/// Build one distant tile using the common stride-eight topology.
pub fn build_procedural_mesh(
    source: &ProceduralCellSource,
    tile: TerrainHlodTile,
) -> Result<ProceduralTerrainHlodMesh, String> {
    build_procedural_mesh_with_stride(source, tile, 8)
}

/// Reuses the raster hierarchy's exact topology and refined-edge construction.
pub fn build_procedural_mesh_with_stride(
    source: &ProceduralCellSource,
    tile: TerrainHlodTile,
    stride: u8,
) -> Result<ProceduralTerrainHlodMesh, String> {
    if stride < 2 {
        return Err("Terrain HLOD requires a distant sampling stride".into());
    }
    let key = tile.key;
    let side = key.side_cells();
    if side == 0
        || key.level > 3
        || !key.x.is_multiple_of(side)
        || !key.z.is_multiple_of(side)
        || u32::from(key.x) + u32::from(side) > u32::from(TERRAIN_CHUNKS_PER_AXIS)
        || u32::from(key.z) + u32::from(side) > u32::from(TERRAIN_CHUNKS_PER_AXIS)
        || (key.level == 0 && tile.finer_edges.iter().any(|edge| *edge))
    {
        return Err("Terrain HLOD tile is outside the pinned procedural grid or hierarchy".into());
    }
    let (coordinates, indices) = tile_topology(tile, stride)?;
    let mut mesh = ProceduralTerrainHlodMesh {
        tile,
        source: source.residency_source(),
        sample_stride: stride,
        positions_m: Vec::with_capacity(coordinates.len()),
        normals: Vec::with_capacity(coordinates.len()),
        uvs: Vec::with_capacity(coordinates.len()),
        material_weights: Vec::with_capacity(coordinates.len()),
        indices,
    };
    for [x2, z2] in coordinates {
        let x = key.x * TERRAIN_CHUNK_INTERVALS + x2 * side / 2;
        let z = key.z * TERRAIN_CHUNK_INTERVALS + z2 * side / 2;
        let vertex: SurfaceVertex = surface_vertex(source.terrain().document(), x, z);
        mesh.positions_m.push(vertex.position_m);
        mesh.normals.push(vertex.shading_normal);
        mesh.uvs.push(grid_uv(x, z));
        mesh.material_weights.push(vertex.material_weights);
    }
    Ok(mesh)
}
