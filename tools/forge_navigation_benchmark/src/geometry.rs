use bevy_math::{UVec3, Vec3, Vec3A};
use bevy_rerecast::rerecast::{AreaType, TriMesh};
use forge_world::{
    GeneratedTerrain, WorldSketchBridgePlacement, WorldSketchTraversalAuditDocument,
};
use thiserror::Error;

const MASK_BLOCKED_THRESHOLD: u8 = 1;

pub fn exterior_bridge_mesh(
    terrain: &GeneratedTerrain,
    audit: &WorldSketchTraversalAuditDocument,
    bridge: &WorldSketchBridgePlacement,
    half_extent_m: f32,
    include_bridge: bool,
) -> Result<TriMesh, GeometryError> {
    let centre_x = bridge.crossing_center_m[0];
    let centre_z = bridge.crossing_center_m[1];
    let mut mesh = TriMesh::default();

    for chunk in &terrain.chunks {
        for triangle in &chunk.collider.triangles {
            let Some([a, b, c]) = triangle_vertices(&chunk.collider.vertices_m, *triangle) else {
                continue;
            };
            let centroid_x = (a[0] + b[0] + c[0]) / 3.0;
            let centroid_z = (a[2] + b[2] + c[2]) / 3.0;
            if (centroid_x - centre_x).abs() > half_extent_m
                || (centroid_z - centre_z).abs() > half_extent_m
                || mask_sample(&audit.water_blocked_mask_u8, audit, centroid_x, centroid_z)
                    >= MASK_BLOCKED_THRESHOLD
            {
                continue;
            }
            push_triangle(&mut mesh, a, b, c)?;
        }
    }

    if include_bridge {
        push_bridge_deck(&mut mesh, bridge)?;
    }
    Ok(mesh)
}

pub fn interior_mesh(door_open: bool, doorway_width_m: f32) -> Result<TriMesh, GeometryError> {
    let mut mesh = TriMesh::default();
    push_horizontal_quad(&mut mesh, -10.0, 10.0, -5.0, 5.0, 0.0)?;

    let half_gap = doorway_width_m * 0.5;
    push_box(
        &mut mesh,
        Vec3::new(0.0, 1.5, f32::midpoint(-5.0, -half_gap)),
        Vec3::new(0.4, 3.0, 5.0 - half_gap),
    )?;
    push_box(
        &mut mesh,
        Vec3::new(0.0, 1.5, f32::midpoint(5.0, half_gap)),
        Vec3::new(0.4, 3.0, 5.0 - half_gap),
    )?;
    if !door_open {
        push_box(
            &mut mesh,
            Vec3::new(0.0, 1.5, 0.0),
            Vec3::new(0.4, 3.0, doorway_width_m),
        )?;
    }
    Ok(mesh)
}

#[cfg(test)]
pub fn disconnected_platforms_mesh() -> Result<TriMesh, GeometryError> {
    let mut mesh = TriMesh::default();
    push_horizontal_quad(&mut mesh, -5.0, -1.0, -2.0, 2.0, 0.0)?;
    push_horizontal_quad(&mut mesh, 1.0, 5.0, -2.0, 2.0, 0.0)?;
    Ok(mesh)
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "coordinates are rounded and clamped to an exact non-negative u16 grid range first"
)]
fn mask_sample(mask: &[u8], audit: &WorldSketchTraversalAuditDocument, x_m: f32, z_m: f32) -> u8 {
    let grid = audit.grid;
    let max_x = grid.vertices_x.saturating_sub(1);
    let max_z = grid.vertices_z.saturating_sub(1);
    let grid_x = ((x_m - grid.origin_x_m) / grid.sample_spacing_m)
        .round()
        .clamp(0.0, f32::from(max_x)) as u16;
    let grid_z = ((z_m - grid.origin_z_m) / grid.sample_spacing_m)
        .round()
        .clamp(0.0, f32::from(max_z)) as u16;
    let index = usize::from(grid_z) * usize::from(grid.vertices_x) + usize::from(grid_x);
    mask.get(index).copied().unwrap_or_default()
}

fn triangle_vertices(vertices: &[[f32; 3]], triangle: [u32; 3]) -> Option<[[f32; 3]; 3]> {
    Some([
        *vertices.get(usize::try_from(triangle[0]).ok()?)?,
        *vertices.get(usize::try_from(triangle[1]).ok()?)?,
        *vertices.get(usize::try_from(triangle[2]).ok()?)?,
    ])
}

fn push_bridge_deck(
    mesh: &mut TriMesh,
    bridge: &WorldSketchBridgePlacement,
) -> Result<(), GeometryError> {
    let start = Vec3::from_array(bridge.deck_start_surface_m);
    let end = Vec3::from_array(bridge.deck_end_surface_m);
    let direction = Vec3::new(end.x - start.x, 0.0, end.z - start.z).normalize_or_zero();
    let left = Vec3::new(-direction.z, 0.0, direction.x) * (bridge.deck_width_m * 0.5);
    let start_left = (start + left).to_array();
    let start_right = (start - left).to_array();
    let end_left = (end + left).to_array();
    let end_right = (end - left).to_array();
    push_triangle(mesh, start_left, end_left, start_right)?;
    push_triangle(mesh, start_right, end_left, end_right)
}

fn push_horizontal_quad(
    mesh: &mut TriMesh,
    min_x: f32,
    max_x: f32,
    min_z: f32,
    max_z: f32,
    y: f32,
) -> Result<(), GeometryError> {
    let bottom_left = [min_x, y, min_z];
    let bottom_right = [max_x, y, min_z];
    let top_left = [min_x, y, max_z];
    let top_right = [max_x, y, max_z];
    push_triangle(mesh, bottom_left, top_left, bottom_right)?;
    push_triangle(mesh, bottom_right, top_left, top_right)
}

fn push_box(mesh: &mut TriMesh, centre: Vec3, dimensions: Vec3) -> Result<(), GeometryError> {
    let half = dimensions * 0.5;
    let min = centre - half;
    let max = centre + half;
    let vertices = [
        [min.x, min.y, min.z],
        [max.x, min.y, min.z],
        [max.x, min.y, max.z],
        [min.x, min.y, max.z],
        [min.x, max.y, min.z],
        [max.x, max.y, min.z],
        [max.x, max.y, max.z],
        [min.x, max.y, max.z],
    ];
    for [a, b, c] in [
        [4, 7, 5],
        [5, 7, 6],
        [0, 1, 3],
        [1, 2, 3],
        [0, 4, 1],
        [1, 4, 5],
        [1, 5, 2],
        [2, 5, 6],
        [2, 6, 3],
        [3, 6, 7],
        [3, 7, 0],
        [0, 7, 4],
    ] {
        push_triangle(mesh, vertices[a], vertices[b], vertices[c])?;
    }
    Ok(())
}

fn push_triangle(
    mesh: &mut TriMesh,
    a: [f32; 3],
    b: [f32; 3],
    c: [f32; 3],
) -> Result<(), GeometryError> {
    let vertex_count = mesh.vertices.len();
    let base =
        u32::try_from(vertex_count).map_err(|_| GeometryError::TooManyVertices(vertex_count))?;
    let second = base
        .checked_add(1)
        .ok_or(GeometryError::TooManyVertices(vertex_count))?;
    let third = base
        .checked_add(2)
        .ok_or(GeometryError::TooManyVertices(vertex_count))?;
    mesh.vertices.extend([
        Vec3A::from_array(a),
        Vec3A::from_array(b),
        Vec3A::from_array(c),
    ]);
    mesh.indices.push(UVec3::new(base, second, third));
    mesh.area_types.push(AreaType::NOT_WALKABLE);
    Ok(())
}

#[derive(Debug, Error)]
pub enum GeometryError {
    #[error("navigation benchmark input exceeds the u32 vertex limit at {0} vertices")]
    TooManyVertices(usize),
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn open_and_closed_interiors_have_consistent_triangle_payloads() {
        let open = interior_mesh(true, 2.0).unwrap();
        let closed = interior_mesh(false, 2.0).unwrap();
        assert_eq!(open.indices.len(), open.area_types.len());
        assert_eq!(closed.indices.len(), closed.area_types.len());
        assert_eq!(closed.indices.len(), open.indices.len() + 12);
    }

    #[test]
    fn disconnected_platform_fixture_contains_two_quads() {
        let mesh = disconnected_platforms_mesh().unwrap();
        assert_eq!(mesh.indices.len(), 4);
        assert_eq!(mesh.vertices.len(), 12);
    }
}
