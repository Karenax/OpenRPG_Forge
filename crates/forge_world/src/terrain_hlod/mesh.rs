use super::TerrainHlodTile;
use crate::{TerrainLodTopology, sampled_terrain::SampledTerrainPlan};
use forge_core::ContentHash;
use std::collections::BTreeMap;

/// Owned CPU buffers, excluding immutable raster, GPU and allocator overhead.
#[derive(Debug)]
pub struct TerrainHlodMesh {
    pub tile: TerrainHlodTile,
    pub source: ContentHash,
    pub positions_m: Vec<[f32; 3]>,
    pub normals: Vec<[f32; 3]>,
    pub material_weights: Vec<[f32; 4]>,
    pub indices: Vec<u32>,
}

impl TerrainHlodMesh {
    #[must_use]
    pub fn bytes(&self) -> u64 {
        (self.positions_m.capacity() * size_of::<[f32; 3]>()
            + self.normals.capacity() * size_of::<[f32; 3]>()
            + self.material_weights.capacity() * size_of::<[f32; 4]>()
            + self.indices.capacity() * size_of::<u32>()) as u64
    }
}

/// One coarse mesh aggregates 4^level base cells. The interior uses the existing
/// stride-eight topology; perimeter samples are refined only for finer neighbours.
/// All vertices evaluate the same canonical raster/corrections/material weights.
pub fn build_mesh(
    terrain: &SampledTerrainPlan,
    tile: TerrainHlodTile,
) -> Result<TerrainHlodMesh, String> {
    build_mesh_with_stride(terrain, tile, 8)
}

/// A quality-specific interior; perimeter sampling and refined seams remain canonical.
pub fn build_mesh_with_stride(
    terrain: &SampledTerrainPlan,
    tile: TerrainHlodTile,
    stride: u8,
) -> Result<TerrainHlodMesh, String> {
    if stride < 2 {
        return Err("Terrain HLOD requires a distant sampling stride".into());
    }
    let key = tile.key;
    let side = key.side_cells();
    let source = terrain.raster().source();
    if side == 0
        || key.level > 7
        || !key.x.is_multiple_of(side)
        || !key.z.is_multiple_of(side)
        || (u32::from(key.x) + u32::from(side)) * 32 > source.width
        || (u32::from(key.z) + u32::from(side)) * 32 > source.height
        || (key.level == 0 && tile.finer_edges.iter().any(|edge| *edge))
    {
        return Err("Terrain HLOD tile is outside the canonical raster or hierarchy".into());
    }
    let (coordinates, indices) = tile_topology(tile, stride)?;
    let mut mesh = TerrainHlodMesh {
        tile,
        source: terrain.document_hash(),
        positions_m: Vec::with_capacity(coordinates.len()),
        normals: Vec::with_capacity(coordinates.len()),
        material_weights: Vec::with_capacity(coordinates.len()),
        indices,
    };
    for [x2, z2] in coordinates {
        let sample = [key.x * 32 + x2 * side / 2, key.z * 32 + z2 * side / 2];
        let vertex = terrain
            .sample_vertex(sample)
            .map_err(|error| error.to_string())?;
        let weights = crate::terrain_generation::material_weights_for(
            terrain.document(),
            vertex.position_m[1],
            vertex.normal[1],
            sample[0],
            sample[1],
        );
        mesh.positions_m.push(vertex.position_m);
        mesh.normals.push(vertex.normal);
        mesh.material_weights
            .push([weights.grass, weights.earth, weights.rock, 1.0]);
    }
    Ok(mesh)
}

/// Shared topology, including the same refined edge splitting for both sources.
pub(super) fn tile_topology(
    tile: TerrainHlodTile,
    stride: u8,
) -> Result<(Vec<[u16; 2]>, Vec<u32>), String> {
    let topology = TerrainLodTopology::new(stride).map_err(|error| error.to_string())?;
    // Doubled local coordinates retain integer midpoints along level >0 edges.
    let mut coordinates: Vec<_> = topology
        .source_vertex_indices()
        .iter()
        .map(|&i| [2 * (i % 33), 2 * (i / 33)])
        .collect();
    let mut triangles: Vec<_> = topology.triangle_indices().as_chunks::<3>().0.to_vec();
    let mut lookup: BTreeMap<_, _> = coordinates.iter().copied().zip(0_u32..).collect();
    for (edge, finer) in tile.finer_edges.iter().enumerate() {
        if *finer {
            triangles = split_edge(triangles, edge, &mut coordinates, &mut lookup)?;
        }
    }
    Ok((coordinates, triangles.into_iter().flatten().collect()))
}

fn split_edge(
    triangles: Vec<[u32; 3]>,
    edge: usize,
    coordinates: &mut Vec<[u16; 2]>,
    lookup: &mut BTreeMap<[u16; 2], u32>,
) -> Result<Vec<[u32; 3]>, String> {
    let axis = usize::from(edge < 2);
    let fixed_axis = 1 - axis;
    let fixed = if edge.is_multiple_of(2) { 0 } else { 64 };
    let mut output = Vec::with_capacity(triangles.len() + 32);
    for triangle in triangles {
        let boundary_edge = (0..3).find(|&i| {
            let a = coordinates[triangle[i] as usize];
            let b = coordinates[triangle[(i + 1) % 3] as usize];
            a[fixed_axis] == fixed && b[fixed_axis] == fixed && a[axis].abs_diff(b[axis]) == 2
        });
        if let Some(i) = boundary_edge {
            let [a, b, c] = [triangle[i], triangle[(i + 1) % 3], triangle[(i + 2) % 3]];
            let first = coordinates[a as usize];
            let second = coordinates[b as usize];
            let midpoint = [first[0].midpoint(second[0]), first[1].midpoint(second[1])];
            let next = u32::try_from(coordinates.len()).map_err(|error| error.to_string())?;
            let index = *lookup.entry(midpoint).or_insert_with(|| {
                coordinates.push(midpoint);
                next
            });
            output.extend_from_slice(&[[a, index, c], [index, b, c]]);
        } else {
            output.push(triangle);
        }
    }
    Ok(output)
}
