//! Partition overlapping ribbons by maximum coverage, then stable face order.
//! A single surface owns every area; shoulders never cover another road's core.
use super::PathSurfaceMesh;
use crate::TerrainGenerationPlan;
use std::collections::{BTreeMap, BTreeSet};
mod geometry;
use geometry::{Plane, Point};

const MAX_COMPARISONS: usize = 10_000_000;
const MAX_FRAGMENTS: usize = 256;
const MAX_INDICES: usize = 1_500_000;
const MAX_GRID_ENTRIES: usize = 4_000_000;
type SpatialGrid = BTreeMap<(i32, i32), Vec<usize>>;

struct Face {
    mesh: usize,
    indices: [u32; 3],
    points: [Point; 3],
    positions: [[f32; 3]; 3],
    normals: [[f32; 3]; 3],
    colors: [[f32; 4]; 3],
    alpha: Plane,
    bounds: [f64; 4],
}

impl Face {
    fn new(mesh: usize, indices: [u32; 3], source: &PathSurfaceMesh) -> Self {
        let positions = indices.map(|i| source.positions[i as usize]);
        let normals = indices.map(|i| source.normals[i as usize]);
        let colors = indices.map(|i| source.colors[i as usize]);
        let points = indices.map(|i| {
            let p = source.positions[i as usize];
            [f64::from(p[0]), f64::from(p[2])]
        });
        let alpha = geometry::alpha_plane(points, colors.map(|color| f64::from(color[3])));
        let bounds = [
            points.iter().map(|p| p[0]).fold(f64::INFINITY, f64::min),
            points.iter().map(|p| p[1]).fold(f64::INFINITY, f64::min),
            points
                .iter()
                .map(|p| p[0])
                .fold(f64::NEG_INFINITY, f64::max),
            points
                .iter()
                .map(|p| p[1])
                .fold(f64::NEG_INFINITY, f64::max),
        ];
        Self {
            mesh,
            indices,
            points,
            positions,
            normals,
            colors,
            alpha,
            bounds,
        }
    }

    #[allow(
        clippy::cast_possible_truncation,
        reason = "validated terrain coordinates bound 2 m spatial cells"
    )]
    fn cells(&self) -> impl Iterator<Item = (i32, i32)> {
        let [left, top, right, bottom] = self.bounds.map(|v| (v / 2.0).floor() as i32);
        (left..=right).flat_map(move |x| (top..=bottom).map(move |z| (x, z)))
    }

    fn overlaps(&self, other: &Self) -> bool {
        self.bounds[0] < other.bounds[2]
            && other.bounds[0] < self.bounds[2]
            && self.bounds[1] < other.bounds[3]
            && other.bounds[1] < self.bounds[3]
    }

    /// Sample the already-projected source face so clipped profiled ribbons keep
    /// their authored elevation, crossfall and shading rather than re-draping.
    #[allow(
        clippy::cast_possible_truncation,
        reason = "clipped points and interpolated attributes originate within validated f32 terrain coordinates"
    )]
    fn attributes_at(&self, point: Point, coverage: f32) -> ([f32; 3], [f32; 3], [f32; 4]) {
        let first = [
            self.points[1][0] - self.points[0][0],
            self.points[1][1] - self.points[0][1],
        ];
        let second = [
            self.points[2][0] - self.points[0][0],
            self.points[2][1] - self.points[0][1],
        ];
        let offset = [point[0] - self.points[0][0], point[1] - self.points[0][1]];
        let determinant = first[0] * second[1] - first[1] * second[0];
        let b = (offset[0] * second[1] - offset[1] * second[0]) / determinant;
        let c = (first[0] * offset[1] - first[1] * offset[0]) / determinant;
        let weights = [1.0 - b - c, b, c];
        let height = weights
            .into_iter()
            .zip(self.positions)
            .map(|(weight, position)| weight * f64::from(position[1]))
            .sum::<f64>() as f32;
        let mut normal = [0.0; 3];
        let mut color = [0.0; 4];
        for (weight, (source_normal, source_color)) in weights
            .into_iter()
            .zip(self.normals.into_iter().zip(self.colors))
        {
            for axis in 0..3 {
                normal[axis] += weight as f32 * source_normal[axis];
            }
            for channel in 0..4 {
                color[channel] += weight as f32 * source_color[channel];
            }
        }
        let length = normal
            .into_iter()
            .map(|component| component * component)
            .sum::<f32>()
            .sqrt();
        if length > 0.0 {
            normal = normal.map(|component| component / length);
        }
        color[3] = coverage;
        ([point[0] as f32, height, point[1] as f32], normal, color)
    }
}

fn spatial_grid(faces: &[Face]) -> Result<SpatialGrid, String> {
    let mut grid = SpatialGrid::new();
    let mut entries = 0;
    for (index, face) in faces.iter().enumerate() {
        for cell in face.cells() {
            entries += 1;
            if entries > MAX_GRID_ENTRIES {
                return Err("Path junctions exceed 4000000 spatial entries; simplify paths".into());
            }
            grid.entry(cell).or_default().push(index);
        }
    }
    Ok(grid)
}

pub(super) fn merge(
    meshes: &mut [PathSurfaceMesh],
    terrain: &TerrainGenerationPlan,
) -> Result<(), String> {
    let faces: Vec<_> = meshes
        .iter()
        .enumerate()
        .flat_map(|(mesh, source)| {
            source
                .indices
                .as_chunks::<3>()
                .0
                .iter()
                .map(move |indices| Face::new(mesh, *indices, source))
        })
        .collect();
    let grid = spatial_grid(&faces)?;
    let mut output = meshes.to_vec();
    let mut vertices = output.iter().map(|m| m.positions.len()).sum::<usize>();
    let mut indices = 0;
    let mut keys: Vec<_> = output.iter().map(vertex_keys).collect();
    for mesh in &mut output {
        mesh.indices.clear();
    }
    let mut comparisons = 0;
    for (index, face) in faces.iter().enumerate() {
        let candidates: BTreeSet<_> = face
            .cells()
            .filter_map(|cell| grid.get(&cell))
            .flatten()
            .copied()
            .collect();
        let mut fragments = vec![face.points.to_vec()];
        for other_index in candidates {
            let other = &faces[other_index];
            if index == other_index || !face.overlaps(other) {
                continue;
            }
            comparisons += 1;
            if comparisons > MAX_COMPARISONS {
                return Err(
                    "Path junctions exceed 10000000 face comparisons; simplify paths".into(),
                );
            }
            // Shared-edge neighbours already tile their ribbon without overlap.
            if face.mesh == other.mesh
                && face
                    .indices
                    .iter()
                    .filter(|i| other.indices.contains(i))
                    .count()
                    >= 2
            {
                continue;
            }
            let difference = [0, 1, 2].map(|axis| other.alpha[axis] - face.alpha[axis]);
            let equal = difference.iter().all(|v| *v == 0.0);
            if equal && index < other_index {
                continue;
            }
            let cutter = if equal {
                other.points.to_vec()
            } else {
                geometry::clip(&other.points, difference)
            };
            if geometry::area(&cutter) <= 0.0 {
                continue;
            }
            fragments = fragments
                .iter()
                .flat_map(|p| geometry::subtract(p, &cutter))
                .collect();
            if fragments.len() > MAX_FRAGMENTS {
                return Err("Path junction exceeds 256 fragments per face; simplify paths".into());
            }
            if fragments.is_empty() {
                break;
            }
        }
        let mesh = &mut output[face.mesh];
        if fragments.len() == 1 && fragments[0] == face.points {
            mesh.indices.extend_from_slice(&face.indices);
            indices += 3;
        } else {
            for polygon in fragments {
                for pair in polygon.windows(2).skip(1) {
                    let points = [polygon[0], pair[0], pair[1]];
                    // f32 quantization may collapse a sub-micrometre sliver.
                    append(
                        mesh,
                        &mut keys[face.mesh],
                        terrain,
                        face,
                        points,
                        &mut vertices,
                        &mut indices,
                    )?;
                }
            }
        }
        if indices > MAX_INDICES {
            return Err("Path junctions exceed 1500000 indices; simplify paths".into());
        }
    }
    meshes.clone_from_slice(&output);
    Ok(())
}

#[derive(Clone, Copy, Eq, Ord, PartialEq, PartialOrd)]
struct VertexKey {
    position: [u32; 3],
    normal: [u32; 3],
    color: [u32; 4],
}

type VertexKeys = BTreeMap<VertexKey, u32>;

fn vertex_key(position: [f32; 3], normal: [f32; 3], color: [f32; 4]) -> VertexKey {
    VertexKey {
        position: position.map(f32::to_bits),
        normal: normal.map(f32::to_bits),
        color: color.map(f32::to_bits),
    }
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "projection vertices are bounded to 250000"
)]
fn vertex_keys(mesh: &PathSurfaceMesh) -> VertexKeys {
    mesh.positions
        .iter()
        .zip(&mesh.colors)
        .enumerate()
        .map(|(i, (p, color))| (vertex_key(*p, mesh.normals[i], *color), i as u32))
        .collect()
}

#[allow(
    clippy::too_many_arguments,
    reason = "explicit output buffers and shared budgets; no global mutable state"
)]
#[allow(
    clippy::cast_possible_truncation,
    reason = "clipped finite f64 coordinates within validated f32 terrain, bounded vertex count"
)]
fn append(
    mesh: &mut PathSurfaceMesh,
    keys: &mut VertexKeys,
    terrain: &TerrainGenerationPlan,
    face: &Face,
    points: [Point; 3],
    vertices: &mut usize,
    indices: &mut usize,
) -> Result<(), String> {
    let rounded = points.map(|p| p.map(|v| f64::from(v as f32)));
    if geometry::area(&rounded) <= 0.0 {
        return Ok(());
    }
    for point in rounded {
        let x = point[0] as f32;
        let z = point[1] as f32;
        let coverage = geometry::value(face.alpha, point).clamp(0.0, 1.0) as f32;
        let (position, normal, color) = if mesh.profiled {
            face.attributes_at(point, coverage)
        } else {
            let (surface, normal) = terrain
                .surface_with_shading_normal_at(x, z)
                .map_err(|e| e.to_string())?;
            (
                [x, surface.height_m + 0.02, z],
                normal,
                [1.0, 1.0, 1.0, coverage],
            )
        };
        let key = vertex_key(position, normal, color);
        let index = if let Some(index) = keys.get(&key) {
            *index
        } else {
            *vertices += 1;
            if *vertices > super::MAX_VERTICES {
                return Err("Path junctions exceed 250000 vertices; simplify paths".into());
            }
            let index = mesh.positions.len() as u32;
            mesh.positions.push(position);
            mesh.normals.push(normal);
            mesh.colors.push(color);
            keys.insert(key, index);
            index
        };
        mesh.indices.push(index);
        *indices += 1;
    }
    Ok(())
}
