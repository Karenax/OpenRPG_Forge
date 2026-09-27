//! Split the bank footprint on the canonical terrain grid before projecting its height.
//! Sampling only ribbon corners lets long triangles bridge over (or cut through) the ground.
use super::{BANK_MESH_CLEARANCE_M, RiverSurfaceGeometry, WaterAdapterError};
use bevy::math::DVec2;
use forge_world::{
    TERRAIN_GLOBAL_INTERVALS_PER_AXIS, TERRAIN_HALF_EXTENT_M, TERRAIN_SAMPLE_SPACING_M,
    TerrainGenerationPlan,
};

const MAX_CELLS: usize = 1_000_000;
const MAX_VERTICES: usize = 1_000_000;

#[derive(Clone, Copy)]
struct Vertex {
    xz: DVec2,
    uv: [f32; 2],
    color: [f32; 4],
}

impl Vertex {
    #[allow(clippy::cast_possible_truncation)]
    fn interpolate(self, other: Self, amount: f64) -> Self {
        let blend = |a: f32, b: f32| (f64::from(a) + (f64::from(b) - f64::from(a)) * amount) as f32;
        Self {
            xz: self.xz.lerp(other.xz, amount),
            uv: std::array::from_fn(|i| blend(self.uv[i], other.uv[i])),
            color: std::array::from_fn(|i| blend(self.color[i], other.color[i])),
        }
    }
}

/// Clip a convex polygon to `normal.dot(xz) <= distance`.
fn clip(polygon: Vec<Vertex>, normal: DVec2, distance: f64) -> Vec<Vertex> {
    let mut output = Vec::with_capacity(8);
    let Some(mut previous) = polygon.last().copied() else {
        return output;
    };
    let mut previous_distance = normal.dot(previous.xz) - distance;
    for current in polygon {
        let current_distance = normal.dot(current.xz) - distance;
        if (previous_distance <= 0.0) != (current_distance <= 0.0) {
            output.push(previous.interpolate(
                current,
                previous_distance / (previous_distance - current_distance),
            ));
        }
        if current_distance <= 0.0 {
            output.push(current);
        }
        previous = current;
        previous_distance = current_distance;
    }
    output
}

#[allow(clippy::cast_possible_truncation)]
pub(super) fn conform(
    source: &RiverSurfaceGeometry,
    terrain: &TerrainGenerationPlan,
) -> Result<(RiverSurfaceGeometry, usize), WaterAdapterError> {
    let mut output = RiverSurfaceGeometry {
        positions: Vec::new(),
        terrain_heights: Vec::new(),
        normals: Vec::new(),
        uvs: Vec::new(),
        colors: Some(Vec::new()),
        indices: Vec::new(),
    };
    let spacing = f64::from(TERRAIN_SAMPLE_SPACING_M);
    let half = f64::from(TERRAIN_HALF_EXTENT_M);
    let mut cells = 0;
    let mut winding_corrections = 0;
    for indices in source.indices.as_chunks::<3>().0 {
        let triangle = indices.map(|index| {
            let index = index as usize;
            let position = source.positions[index];
            Vertex {
                xz: DVec2::new(f64::from(position[0]), f64::from(position[2])),
                uv: source.uvs[index],
                color: source
                    .colors
                    .as_ref()
                    .expect("bank builder supplies colors")[index],
            }
        });
        let minimum = triangle
            .iter()
            .fold(DVec2::splat(f64::INFINITY), |v, p| v.min(p.xz));
        let maximum = triangle
            .iter()
            .fold(DVec2::splat(f64::NEG_INFINITY), |v, p| v.max(p.xz));
        let cell = |value: f64| {
            ((value + half) / spacing)
                .floor()
                .clamp(0.0, f64::from(TERRAIN_GLOBAL_INTERVALS_PER_AXIS - 1)) as i32
        };
        for z in cell(minimum.y)..=cell(maximum.y) {
            for x in cell(minimum.x)..=cell(maximum.x) {
                cells += 1;
                if cells > MAX_CELLS {
                    return Err(WaterAdapterError::BankProjectionBudgetExceeded);
                }
                let left = f64::from(x) * spacing - half;
                let back = f64::from(z) * spacing - half;
                let polygon = clip(triangle.to_vec(), -DVec2::X, -left);
                let polygon = clip(polygon, DVec2::X, left + spacing);
                let polygon = clip(polygon, -DVec2::Y, -back);
                let polygon = clip(polygon, DVec2::Y, back + spacing);
                // Exact terrain split: [a,c,b], [b,c,d], at local u + v = 1.
                let diagonal = left + back + spacing;
                for sign in [1.0, -1.0] {
                    let polygon = clip(polygon.clone(), DVec2::splat(sign), diagonal * sign);
                    for index in 1..polygon.len().saturating_sub(1) {
                        let vertices = [polygon[0], polygon[index], polygon[index + 1]];
                        let area = (vertices[1].xz - vertices[0].xz)
                            .perp_dot(vertices[2].xz - vertices[0].xz);
                        // Clipping can leave an edge or duplicate vertices, not a surface.
                        if area.abs() <= 1.0e-9 {
                            continue;
                        }
                        winding_corrections += append(&mut output, vertices, terrain)?;
                    }
                }
            }
        }
    }
    Ok((output, winding_corrections))
}

#[allow(clippy::cast_possible_truncation)]
fn append(
    output: &mut RiverSurfaceGeometry,
    mut vertices: [Vertex; 3],
    terrain: &TerrainGenerationPlan,
) -> Result<usize, WaterAdapterError> {
    if output.positions.len() + 3 > MAX_VERTICES {
        return Err(WaterAdapterError::BankProjectionBudgetExceeded);
    }
    let mut positions = vertices.map(|vertex| [vertex.xz.x as f32, 0.0, vertex.xz.y as f32]);
    // f64 intersections can collapse after conversion to the renderer's f32 coordinates.
    if super::triangle_normal_y(&positions, [0, 1, 2]).abs() <= super::MIN_POSITIVE_WINDING_Y {
        return Ok(0);
    }
    let reversed = super::triangle_normal_y(&positions, [0, 1, 2]) < 0.0;
    if reversed {
        positions.swap(1, 2);
        vertices.swap(1, 2);
    }
    for (vertex, mut position) in vertices.into_iter().zip(positions) {
        let (surface, shading_normal) =
            terrain.surface_with_shading_normal_at(position[0], position[2])?;
        let height = surface.height_m;
        position[1] = height + BANK_MESH_CLEARANCE_M;
        output.indices.push(
            u32::try_from(output.positions.len())
                .map_err(|_| WaterAdapterError::BankProjectionBudgetExceeded)?,
        );
        output.positions.push(position);
        output.normals.push(shading_normal);
        output.terrain_heights.push(height);
        output.uvs.push(vertex.uv);
        output
            .colors
            .as_mut()
            .expect("bank output has colors")
            .push(bank_material_weights(
                surface.material_weights,
                vertex.color[0],
            ));
    }
    Ok(usize::from(reversed))
}

fn bank_material_weights(terrain: forge_world::TerrainMaterialWeights, inward: f32) -> [f32; 4] {
    let amount = inward.clamp(0.0, 1.0);
    let earth = amount * amount * (3.0 - 2.0 * amount);
    [
        terrain.grass * (1.0 - earth),
        terrain.earth * (1.0 - earth) + earth,
        terrain.rock * (1.0 - earth),
        1.0,
    ]
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn clipping_preserves_footprint_and_interpolated_attributes() {
        let terrain = TerrainGenerationPlan::from_document(
            &forge_world::embedded_visual_valley_terrain_document().unwrap(),
        )
        .unwrap();
        let source = RiverSurfaceGeometry {
            positions: vec![[0.0, 50.0, 0.0], [0.0, 50.0, 6.0], [6.0, 50.0, 0.0]],
            terrain_heights: vec![0.0; 3],
            normals: vec![[0.0, 1.0, 0.0]; 3],
            uvs: vec![[0.0, 0.0], [0.0, 1.0], [1.0, 0.0]],
            colors: Some(vec![
                [1.0, 0.0, 0.0, 1.0],
                [0.0, 0.0, 1.0, 1.0],
                [0.0, 1.0, 0.0, 1.0],
            ]),
            indices: vec![0, 1, 2],
        };
        let (result, _) = conform(&source, &terrain).unwrap();
        let area: f64 = result
            .indices
            .as_chunks::<3>()
            .0
            .iter()
            .map(|triangle| {
                super::super::triangle_normal_y(
                    &result.positions,
                    triangle.map(|index| index as usize),
                ) * 0.5
            })
            .sum();
        assert!((area - 18.0).abs() < 1.0e-8);
        assert!(result.indices.len() > 3);
        for ((position, uv), color) in result
            .positions
            .iter()
            .zip(&result.uvs)
            .zip(result.colors.as_ref().unwrap())
        {
            assert!((uv[0] - position[0] / 6.0).abs() < 1.0e-6);
            assert!((uv[1] - position[2] / 6.0).abs() < 1.0e-6);
            let expected = bank_material_weights(
                terrain
                    .surface_at(position[0], position[2])
                    .unwrap()
                    .material_weights,
                1.0 - uv[0] - uv[1],
            );
            for (actual, expected) in color.iter().zip(expected) {
                assert!((actual - expected).abs() < 1.0e-6);
            }
            assert!(
                (position[1]
                    - terrain
                        .surface_at(position[0], position[2])
                        .unwrap()
                        .height_m
                    - BANK_MESH_CLEARANCE_M)
                    .abs()
                    < 1.0e-5
            );
        }
    }

    #[test]
    fn bank_material_matches_ground_at_outer_edge_and_earth_at_water() {
        let ground = forge_world::TerrainMaterialWeights {
            grass: 0.6,
            earth: 0.3,
            rock: 0.1,
        };
        assert_eq!(
            bank_material_weights(ground, 0.0).map(f32::to_bits),
            [0.6, 0.3, 0.1, 1.0].map(f32::to_bits)
        );
        assert_eq!(
            bank_material_weights(ground, 1.0).map(f32::to_bits),
            [0.0, 1.0, 0.0, 1.0].map(f32::to_bits)
        );
        let middle = bank_material_weights(ground, 0.5);
        for (actual, expected) in middle.into_iter().zip([0.3, 0.65, 0.05, 1.0]) {
            assert!((actual - expected).abs() < 1.0e-6);
        }
        assert!((middle[0] + middle[1] + middle[2] - 1.0).abs() < 1.0e-6);
    }
}
