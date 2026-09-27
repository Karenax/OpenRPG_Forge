//! Common refinement: each emitted triangle belongs to one triangle of BOTH LODs.
use super::{
    TERRAIN_SAMPLE_SPACING_M, TerrainChunkMesh, TerrainLodError, TerrainLodMesh,
    TerrainLodTopology, TerrainMaterialWeights, cross, grid_xz, subtract, validate_source_grid,
};

type Point = [f64; 2];

impl TerrainLodTopology {
    /// Builds matching render-only meshes for interpolation without crossing parent edges.
    /// Both endpoints retain the original surfaces, including their boundary fans.
    /// This is preparation work, never a per-frame operation.
    pub fn project_transition(
        &self,
        source: &TerrainChunkMesh,
        target: &Self,
    ) -> Result<(TerrainLodMesh, TerrainLodMesh), TerrainLodError> {
        validate_source_grid(source)?;
        let mut start = self.project(source)?;
        start.positions_m.clear();
        start.normals.clear();
        start.uvs.clear();
        start.material_weights.clear();
        start.indices.clear();
        let mut end = start.clone();
        end.sample_stride = target.sample_stride;
        for from in self.indices.as_chunks::<3>().0 {
            let from = from.map(|index| self.source_vertices[index as usize]);
            let a = from.map(grid_xz);
            for to in target.indices.as_chunks::<3>().0 {
                let to = to.map(|index| target.source_vertices[index as usize]);
                let b = to.map(grid_xz);
                if (0..2).any(|axis| {
                    maximum(a, axis) <= minimum(b, axis) || maximum(b, axis) <= minimum(a, axis)
                }) {
                    continue;
                }
                let polygon = intersection(a, b);
                for triangle in polygon[1.min(polygon.len())..].windows(2) {
                    let points = [polygon[0], triangle[0], triangle[1]];
                    if cross(
                        subtract(points[1], points[0]),
                        subtract(points[2], points[0]),
                    )
                    .abs()
                        < 1e-10
                    {
                        continue;
                    }
                    if start.positions_m.len() + 3 > 65_536 {
                        return Err(TerrainLodError::InvalidSource(
                            "transition exceeds 65536 vertices",
                        ));
                    }
                    for point in points {
                        append(&mut start, source, from, point);
                        append(&mut end, source, to, point);
                    }
                }
            }
        }
        compact(&mut start, &mut end);
        Ok((start, end))
    }
}

fn vertex_bits(mesh: &TerrainLodMesh, index: usize) -> [u32; 11] {
    let p = mesh.positions_m[index];
    let n = mesh.normals[index];
    let uv = mesh.uvs[index];
    let w = mesh.material_weights[index];
    [
        p[0], p[1], p[2], n[0], n[1], n[2], uv[0], uv[1], w.grass, w.earth, w.rock,
    ]
    .map(f32::to_bits)
}

/// Only merge bit-identical attributes at BOTH endpoints. No quantization or seam welding.
fn compact(start: &mut TerrainLodMesh, end: &mut TerrainLodMesh) {
    let mut unique = std::collections::BTreeMap::new();
    let mut remap = Vec::with_capacity(start.positions_m.len());
    let mut count = 0;
    for old in 0..start.positions_m.len() {
        let key = (vertex_bits(start, old), vertex_bits(end, old));
        let index = *unique.entry(key).or_insert_with(|| {
            for mesh in [&mut *start, &mut *end] {
                mesh.positions_m[count] = mesh.positions_m[old];
                mesh.normals[count] = mesh.normals[old];
                mesh.uvs[count] = mesh.uvs[old];
                mesh.material_weights[count] = mesh.material_weights[old];
            }
            let index = u32::try_from(count).expect("transition vertex count is bounded at 65536");
            count += 1;
            index
        });
        remap.push(index);
    }
    for mesh in [start, end] {
        for index in &mut mesh.indices {
            *index = remap[*index as usize];
        }
        mesh.positions_m.truncate(count);
        mesh.normals.truncate(count);
        mesh.uvs.truncate(count);
        mesh.material_weights.truncate(count);
        mesh.positions_m.shrink_to_fit();
        mesh.normals.shrink_to_fit();
        mesh.uvs.shrink_to_fit();
        mesh.material_weights.shrink_to_fit();
    }
}

fn minimum(points: [Point; 3], axis: usize) -> f64 {
    points
        .into_iter()
        .map(|p| p[axis])
        .fold(f64::INFINITY, f64::min)
}

fn maximum(points: [Point; 3], axis: usize) -> f64 {
    points
        .into_iter()
        .map(|p| p[axis])
        .fold(f64::NEG_INFINITY, f64::max)
}

fn intersection(a: [Point; 3], b: [Point; 3]) -> Vec<Point> {
    let mut polygon = a.to_vec();
    for (first, second) in [(b[0], b[1]), (b[1], b[2]), (b[2], b[0])] {
        if polygon.is_empty() {
            break;
        }
        let mut clipped = Vec::new();
        for (previous, current) in polygon
            .iter()
            .cycle()
            .skip(polygon.len() - 1)
            .zip(&polygon)
            .take(polygon.len())
        {
            // Grid triangles have clockwise XZ winding (upward Y normal).
            let previous_side = cross(subtract(second, first), subtract(*previous, first));
            let current_side = cross(subtract(second, first), subtract(*current, first));
            let previous_inside = previous_side <= 0.0;
            let current_inside = current_side <= 0.0;
            if previous_inside != current_inside {
                let t = previous_side / (previous_side - current_side);
                clipped.push([
                    previous[0] + t * (current[0] - previous[0]),
                    previous[1] + t * (current[1] - previous[1]),
                ]);
            }
            if current_inside {
                clipped.push(*current);
            }
        }
        polygon = clipped;
    }
    polygon
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "bounded grid coordinates and finite convex f32 samples; output vertex limit enforced by caller"
)]
fn append(mesh: &mut TerrainLodMesh, source: &TerrainChunkMesh, ids: [u16; 3], point: Point) {
    let [a, b, c] = ids.map(grid_xz);
    let determinant = cross(subtract(b, a), subtract(c, a));
    let wb = cross(subtract(point, a), subtract(c, a)) / determinant;
    let wc = cross(subtract(b, a), subtract(point, a)) / determinant;
    let weights = [1.0 - wb - wc, wb, wc];
    let ids = ids.map(usize::from);
    let sample = |values: [f32; 3]| {
        let min = values.into_iter().fold(f32::INFINITY, f32::min);
        let max = values.into_iter().fold(f32::NEG_INFINITY, f32::max);
        values
            .into_iter()
            .zip(weights)
            .map(|(v, w)| f64::from(v) * w)
            .sum::<f64>()
            .clamp(f64::from(min), f64::from(max)) as f32
    };
    // Both endpoints share the same XZ, independent of barycentric roundoff.
    mesh.positions_m.push([
        (f64::from(source.minimum_xz_m[0]) + point[0] * f64::from(TERRAIN_SAMPLE_SPACING_M)) as f32,
        sample(ids.map(|i| source.positions_m[i][1])),
        (f64::from(source.minimum_xz_m[1]) + point[1] * f64::from(TERRAIN_SAMPLE_SPACING_M)) as f32,
    ]);
    mesh.normals
        .push([0, 1, 2].map(|axis| sample(ids.map(|i| source.normals[i][axis]))));
    mesh.uvs
        .push([0, 1].map(|axis| sample(ids.map(|i| source.uvs[i][axis]))));
    mesh.material_weights.push(TerrainMaterialWeights {
        grass: sample(ids.map(|i| source.material_weights[i].grass)),
        earth: sample(ids.map(|i| source.material_weights[i].earth)),
        rock: sample(ids.map(|i| source.material_weights[i].rock)),
    });
    mesh.indices.push(mesh.indices.len() as u32);
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn compaction_preserves_every_corner_and_keeps_target_seams() {
        let generated = crate::TerrainGenerationPlan::from_document(
            &crate::embedded_visual_valley_terrain_document().unwrap(),
        )
        .unwrap()
        .generate()
        .unwrap();
        let original = TerrainLodTopology::new(1)
            .unwrap()
            .project(&generated.chunks[0])
            .unwrap();
        let mut start = original.clone();
        // Expand indexed triangles so the test has known duplicates before compaction.
        start.positions_m = original
            .indices
            .iter()
            .map(|i| original.positions_m[*i as usize])
            .collect();
        start.normals = original
            .indices
            .iter()
            .map(|i| original.normals[*i as usize])
            .collect();
        start.uvs = original
            .indices
            .iter()
            .map(|i| original.uvs[*i as usize])
            .collect();
        start.material_weights = original
            .indices
            .iter()
            .map(|i| original.material_weights[*i as usize])
            .collect();
        start.indices = (0..u32::try_from(start.positions_m.len()).unwrap()).collect();
        let mut end = start.clone();
        // Same start vertex as corner 2, but a distinct target normal must not be merged.
        assert_eq!(vertex_bits(&start, 2), vertex_bits(&start, 3));
        end.normals[3][0] += 0.25;
        let before = start
            .indices
            .iter()
            .map(|i| {
                (
                    vertex_bits(&start, *i as usize),
                    vertex_bits(&end, *i as usize),
                )
            })
            .collect::<Vec<_>>();
        compact(&mut start, &mut end);
        assert_eq!(start.positions_m.len(), original.positions_m.len() + 1);
        assert_ne!(start.indices[2], start.indices[3]);
        assert_eq!(start.indices, end.indices);
        let after = start
            .indices
            .iter()
            .map(|i| {
                (
                    vertex_bits(&start, *i as usize),
                    vertex_bits(&end, *i as usize),
                )
            })
            .collect::<Vec<_>>();
        assert_eq!(before, after);
        let once = (start.clone(), end.clone());
        compact(&mut start, &mut end);
        assert_eq!(once, (start, end));
    }
}
