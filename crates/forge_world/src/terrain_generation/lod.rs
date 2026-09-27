//! Derived render-only LODs. Every perimeter sample is retained to join any neighbour LOD.
use super::{
    TERRAIN_CHUNK_INTERVALS, TERRAIN_CHUNK_VERTICES_PER_AXIS, TERRAIN_SAMPLE_SPACING_M,
    TerrainChunkCoordinate, TerrainChunkId, TerrainChunkMesh, TerrainMaterialWeights,
};
use thiserror::Error;

mod transition;

#[cfg(test)]
mod tests;

#[derive(Clone, Debug, Eq, PartialEq)]
pub struct TerrainLodTopology {
    sample_stride: u8,
    source_vertices: Vec<u16>,
    indices: Vec<u32>,
}

/// Presentation payload only: deliberately contains no collider or canonical document.
#[derive(Clone, Debug, PartialEq)]
pub struct TerrainLodMesh {
    pub source_chunk_id: TerrainChunkId,
    pub coordinate: TerrainChunkCoordinate,
    pub sample_stride: u8,
    pub positions_m: Vec<[f32; 3]>,
    pub normals: Vec<[f32; 3]>,
    pub uvs: Vec<[f32; 2]>,
    pub material_weights: Vec<TerrainMaterialWeights>,
    pub indices: Vec<u32>,
}

#[derive(Clone, Debug, Eq, Error, PartialEq)]
pub enum TerrainLodError {
    #[error("terrain LOD stride must be a power of two from 1 to 32, received {0}")]
    UnsupportedStride(u8),
    #[error("terrain LOD requires an intact full-resolution source grid: {0}")]
    InvalidSource(&'static str),
}

impl TerrainLodTopology {
    /// Samples the target surface at this topology's vertices for a future render morph.
    /// Keeps this topology/UVs/XZ and exact shared samples; does not mutate the source.
    /// This alone does not establish triangle-to-triangle coincidence: boundary fans may
    /// cross target edges, so an adapter must qualify its transition topology separately.
    pub fn project_morph_target(
        &self,
        source: &TerrainChunkMesh,
        target: &Self,
    ) -> Result<TerrainLodMesh, TerrainLodError> {
        let mut result = self.project(source)?;
        for (output, original) in self.source_vertices.iter().copied().enumerate() {
            if target.source_vertices.binary_search(&original).is_ok() {
                continue;
            }
            let point = grid_xz(original);
            let mut sample = None;
            for triangle in target.indices.as_chunks::<3>().0 {
                let ids = triangle.map(|index| target.source_vertices[index as usize]);
                let [a, b, c] = ids.map(grid_xz);
                let determinant = cross(subtract(b, a), subtract(c, a));
                let w_b = cross(subtract(point, a), subtract(c, a)) / determinant;
                let w_c = cross(subtract(b, a), subtract(point, a)) / determinant;
                let weights = [1.0 - w_b - w_c, w_b, w_c];
                if weights.iter().all(|weight| *weight >= -1e-12) {
                    sample = Some((ids.map(usize::from), weights));
                    break;
                }
            }
            let (indices, weights) = sample.ok_or(TerrainLodError::InvalidSource(
                "target topology does not cover morph vertex",
            ))?;
            #[allow(
                clippy::cast_possible_truncation,
                reason = "finite f32 convex bounds; f64 barycentric intermediate rounded back to render payload"
            )]
            let interpolate = |values: [f32; 3]| {
                let minimum = values.into_iter().fold(f32::INFINITY, f32::min);
                let maximum = values.into_iter().fold(f32::NEG_INFINITY, f32::max);
                values
                    .into_iter()
                    .zip(weights)
                    .map(|(value, weight)| f64::from(value) * weight)
                    .sum::<f64>()
                    .clamp(f64::from(minimum), f64::from(maximum)) as f32
            };
            result.positions_m[output][1] = interpolate(indices.map(|i| source.positions_m[i][1]));
            for axis in 0..3 {
                result.normals[output][axis] =
                    interpolate(indices.map(|i| source.normals[i][axis]));
            }
            // Keep interpolated normals unnormalized, as on the target triangle before shading.
            result.material_weights[output] = TerrainMaterialWeights {
                grass: interpolate(indices.map(|i| source.material_weights[i].grass)),
                earth: interpolate(indices.map(|i| source.material_weights[i].earth)),
                rock: interpolate(indices.map(|i| source.material_weights[i].rock)),
            };
        }
        Ok(result)
    }

    pub fn new(sample_stride: u8) -> Result<Self, TerrainLodError> {
        let stride = u16::from(sample_stride);
        if !stride.is_power_of_two() || stride > TERRAIN_CHUNK_INTERVALS {
            return Err(TerrainLodError::UnsupportedStride(sample_stride));
        }
        let mut source_indices = Vec::new();
        for z in (0..TERRAIN_CHUNK_INTERVALS).step_by(usize::from(stride)) {
            for x in (0..TERRAIN_CHUNK_INTERVALS).step_by(usize::from(stride)) {
                if stride == 1
                    || (x > 0
                        && z > 0
                        && x + stride < TERRAIN_CHUNK_INTERVALS
                        && z + stride < TERRAIN_CHUNK_INTERVALS)
                {
                    let [a, b, c, d] = [
                        grid_index(x, z),
                        grid_index(x + stride, z),
                        grid_index(x, z + stride),
                        grid_index(x + stride, z + stride),
                    ];
                    source_indices.extend_from_slice(&[a, c, b, b, c, d]);
                } else {
                    append_boundary_cell(&mut source_indices, x, z, stride);
                }
            }
        }
        let mut used = vec![false; usize::from(TERRAIN_CHUNK_VERTICES_PER_AXIS).pow(2)];
        for index in &source_indices {
            used[usize::from(*index)] = true;
        }
        let source_vertices = (0..TERRAIN_CHUNK_VERTICES_PER_AXIS.pow(2))
            .filter(|index| used[usize::from(*index)])
            .collect::<Vec<_>>();
        let mut remap = vec![0; used.len()];
        for (target, source) in (0_u32..).zip(&source_vertices) {
            remap[usize::from(*source)] = target;
        }
        let indices = source_indices
            .into_iter()
            .map(|index| remap[usize::from(index)])
            .collect();
        Ok(Self {
            sample_stride,
            source_vertices,
            indices,
        })
    }

    #[must_use]
    pub fn source_vertex_indices(&self) -> &[u16] {
        &self.source_vertices
    }

    #[must_use]
    pub fn triangle_indices(&self) -> &[u32] {
        &self.indices
    }

    /// Copies exact retained samples (including normals/UV/weights); never resamples the source.
    pub fn project(&self, source: &TerrainChunkMesh) -> Result<TerrainLodMesh, TerrainLodError> {
        validate_source_grid(source)?;
        let selected = self.source_vertices.iter().map(|index| usize::from(*index));
        Ok(TerrainLodMesh {
            source_chunk_id: source.id,
            coordinate: source.coordinate,
            sample_stride: self.sample_stride,
            positions_m: selected
                .clone()
                .map(|index| source.positions_m[index])
                .collect(),
            normals: selected
                .clone()
                .map(|index| source.normals[index])
                .collect(),
            uvs: selected.clone().map(|index| source.uvs[index]).collect(),
            material_weights: selected
                .map(|index| source.material_weights[index])
                .collect(),
            indices: self.indices.clone(),
        })
    }
}

const fn grid_index(x: u16, z: u16) -> u16 {
    z * TERRAIN_CHUNK_VERTICES_PER_AXIS + x
}

fn grid_xz(index: u16) -> [f64; 2] {
    [
        f64::from(index % TERRAIN_CHUNK_VERTICES_PER_AXIS),
        f64::from(index / TERRAIN_CHUNK_VERTICES_PER_AXIS),
    ]
}

fn subtract(a: [f64; 2], b: [f64; 2]) -> [f64; 2] {
    [a[0] - b[0], a[1] - b[1]]
}

fn cross(a: [f64; 2], b: [f64; 2]) -> f64 {
    a[0] * b[1] - a[1] * b[0]
}

fn append_boundary_cell(indices: &mut Vec<u16>, x: u16, z: u16, stride: u16) {
    let mut ring = Vec::new();
    let end = TERRAIN_CHUNK_INTERVALS;
    // Counter-clockwise from above: left, bottom, right, top. Endpoints belong to the next edge.
    for offset in (0..stride).step_by(if x == 0 { 1 } else { usize::from(stride) }) {
        ring.push(grid_index(x, z + offset));
    }
    for offset in (0..stride).step_by(if z + stride == end {
        1
    } else {
        usize::from(stride)
    }) {
        ring.push(grid_index(x + offset, z + stride));
    }
    for offset in (0..stride).step_by(if x + stride == end {
        1
    } else {
        usize::from(stride)
    }) {
        ring.push(grid_index(x + stride, z + stride - offset));
    }
    for offset in (0..stride).step_by(if z == 0 { 1 } else { usize::from(stride) }) {
        ring.push(grid_index(x + stride - offset, z));
    }
    let centre = grid_index(x + stride / 2, z + stride / 2);
    for (first, second) in ring.iter().zip(ring.iter().cycle().skip(1)) {
        indices.extend_from_slice(&[centre, *first, *second]);
    }
}

fn validate_source_grid(source: &TerrainChunkMesh) -> Result<(), TerrainLodError> {
    let count = usize::from(TERRAIN_CHUNK_VERTICES_PER_AXIS).pow(2);
    if source.positions_m.len() != count
        || source.normals.len() != count
        || source.uvs.len() != count
        || source.material_weights.len() != count
    {
        return Err(TerrainLodError::InvalidSource(
            "vertex attribute counts differ from 33 by 33",
        ));
    }
    if source.indices.len() != usize::from(TERRAIN_CHUNK_INTERVALS).pow(2) * 6 {
        return Err(TerrainLodError::InvalidSource(
            "full-resolution index count differs",
        ));
    }
    for (z, x) in (0..TERRAIN_CHUNK_VERTICES_PER_AXIS)
        .flat_map(|z| (0..TERRAIN_CHUNK_VERTICES_PER_AXIS).map(move |x| (z, x)))
    {
        let index = usize::from(grid_index(x, z));
        let position = source.positions_m[index];
        let weights = source.material_weights[index].as_array();
        if position
            .iter()
            .chain(source.normals[index].iter())
            .chain(source.uvs[index].iter())
            .chain(weights.iter())
            .any(|value| !value.is_finite())
            || weights.iter().any(|value| *value < 0.0)
            || (weights.iter().sum::<f32>() - 1.0).abs() > 0.001
        {
            return Err(TerrainLodError::InvalidSource("invalid vertex attribute"));
        }
        let expected_x = source.minimum_xz_m[0] + f32::from(x) * TERRAIN_SAMPLE_SPACING_M;
        let expected_z = source.minimum_xz_m[1] + f32::from(z) * TERRAIN_SAMPLE_SPACING_M;
        if position[0].to_bits() != expected_x.to_bits()
            || position[2].to_bits() != expected_z.to_bits()
        {
            return Err(TerrainLodError::InvalidSource(
                "positions are not a row-major 2 metre grid",
            ));
        }
    }
    let cells = (0..TERRAIN_CHUNK_INTERVALS)
        .flat_map(|z| (0..TERRAIN_CHUNK_INTERVALS).map(move |x| (z, x)));
    for ((z, x), indices) in cells.zip(source.indices.as_chunks::<6>().0) {
        let a = u32::from(grid_index(x, z));
        let c = a + u32::from(TERRAIN_CHUNK_VERTICES_PER_AXIS);
        if *indices != [a, c, a + 1, a + 1, c, c + 1] {
            return Err(TerrainLodError::InvalidSource(
                "unexpected full-resolution triangle order",
            ));
        }
    }
    Ok(())
}
