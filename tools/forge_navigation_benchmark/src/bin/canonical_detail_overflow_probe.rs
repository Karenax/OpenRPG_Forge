//! Native-only diagnostics for the browser overflow at `DetailNavmesh::new`.
//!
//! This binary mirrors the pinned public Rerecast stages and reports the integer dimensions
//! that are otherwise hidden inside the provider. It deliberately does not change the provider
//! or normalize the canonical geometry.
#![deny(unsafe_code)]
#![allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "the probe prints the pinned provider's u16 grid dimensions and checked products"
)]

use bevy_math::{UVec3, Vec3A};
use bevy_rerecast::{
    NavmeshSettings,
    rerecast::{AreaType, ConfigBuilder, HeightfieldBuilder, PolygonNavmesh, TriMesh},
};
use serde::Deserialize;
use std::{error::Error, fs, path::Path};

#[path = "../provider.rs"]
#[allow(
    dead_code,
    reason = "the diagnostic runs public stages itself and reuses only provider settings"
)]
mod provider;

const AGENT_RADIUS_M: f32 = 0.35;
const AGENT_HEIGHT_M: f32 = 1.8;

#[derive(Debug, Deserialize)]
struct CanonicalReport {
    canonical_geometry: CanonicalGeometry,
}

#[derive(Debug, Deserialize)]
struct CanonicalGeometry {
    positions_m: Vec<[f32; 3]>,
    triangles: Vec<[u32; 3]>,
}

#[allow(
    clippy::too_many_lines,
    reason = "the diagnostic preserves stage order and integer bounds beside the report"
)]
fn main() -> Result<(), Box<dyn Error>> {
    let input = std::env::args()
        .nth(1)
        .ok_or("usage: canonical_detail_overflow_probe <canonical-terrain-probe.v2.json>")?;
    let report: CanonicalReport = serde_json::from_str(&fs::read_to_string(Path::new(&input))?)?;
    let mesh = TriMesh {
        vertices: report
            .canonical_geometry
            .positions_m
            .iter()
            .copied()
            .map(Vec3A::from_array)
            .collect(),
        indices: report
            .canonical_geometry
            .triangles
            .iter()
            .copied()
            .map(UVec3::from_array)
            .collect(),
        area_types: vec![AreaType::NOT_WALKABLE; report.canonical_geometry.triangles.len()],
    };
    let settings = provider::settings_for_agent(AGENT_RADIUS_M, AGENT_HEIGHT_M);
    let aabb = mesh.compute_aabb().ok_or("empty canonical mesh")?;
    let config = config_for(&settings, aabb).build();
    let mut trimesh = mesh;
    trimesh.mark_walkable_triangles(config.walkable_slope_angle);
    let mut heightfield = HeightfieldBuilder {
        aabb: config.aabb,
        cell_size: config.cell_size,
        cell_height: config.cell_height,
    }
    .build()?;
    heightfield.rasterize_triangles(&trimesh, config.walkable_climb)?;
    heightfield.filter_low_hanging_walkable_obstacles(config.walkable_climb);
    heightfield.filter_ledge_spans(config.walkable_height, config.walkable_climb);
    heightfield.filter_walkable_low_height_spans(config.walkable_height);
    let mut compact = heightfield.into_compact(config.walkable_height, config.walkable_climb)?;
    compact.erode_walkable_area(config.walkable_radius);
    compact.build_distance_field();
    compact.build_regions(
        config.border_size,
        config.min_region_area,
        config.merge_region_area,
    )?;
    let contours = compact.build_contours(
        config.max_simplification_error,
        config.max_edge_len,
        config.contour_flags,
    );
    let polygon = contours.into_polygon_mesh(config.max_vertices_per_polygon)?;
    let nvp = usize::from(polygon.max_vertices_per_polygon);
    let mut max_patch_width = 0_u16;
    let mut max_patch_depth = 0_u16;
    let mut poly_vert_count = 0_usize;
    let mut max_polygon = None;
    for (index, chunk) in polygon.polygons.chunks(nvp).enumerate() {
        let mut xmin = compact.width;
        let mut xmax = 0_u16;
        let mut zmin = compact.height;
        let mut zmax = 0_u16;
        for point in chunk
            .iter()
            .take_while(|point| **point != PolygonNavmesh::NO_INDEX)
        {
            let vertex = polygon.vertices[usize::from(*point)];
            xmin = xmin.min(vertex.x);
            xmax = xmax.max(vertex.x);
            zmin = zmin.min(vertex.z);
            zmax = zmax.max(vertex.z);
            poly_vert_count += 1;
        }
        let width = xmax
            .saturating_add(1)
            .min(compact.width)
            .saturating_sub(xmin.saturating_sub(1));
        let height = zmax
            .saturating_add(1)
            .min(compact.height)
            .saturating_sub(zmin.saturating_sub(1));
        if u32::from(width) * u32::from(height)
            > u32::from(max_patch_width) * u32::from(max_patch_depth)
        {
            max_polygon = Some((index, width, height));
        }
        max_patch_width = max_patch_width.max(width);
        max_patch_depth = max_patch_depth.max(height);
    }
    let patch_columns = usize::from(max_patch_width);
    let patch_rows = usize::from(max_patch_depth);
    let detail_patch_product = (patch_columns as u64) * (patch_rows as u64);
    let detail_capacity_product = (poly_vert_count as u64) * 2;
    println!(
        "config width={} height={} tile_size={} border={} cell_size={} cell_height={}",
        config.width,
        config.height,
        config.tile_size,
        config.border_size,
        config.cell_size,
        config.cell_height
    );
    println!(
        "compact width={} height={} cells={} spans={} polygons={} vertices={} nvp={} max_edge_error={} height_search_radius={}",
        compact.width,
        compact.height,
        compact.cells.len(),
        compact.spans.len(),
        polygon.polygon_count(),
        polygon.vertices.len(),
        nvp,
        polygon.max_edge_error,
        1_u32.max(polygon.max_edge_error.ceil() as u32)
    );
    println!(
        "detail max_patch_width={} max_patch_depth={} product_u64={} product_usize={} poly_vert_count={} tcap_u64={}",
        max_patch_width,
        max_patch_depth,
        detail_patch_product,
        patch_columns.saturating_mul(patch_rows),
        poly_vert_count,
        detail_capacity_product
    );
    println!("detail largest_polygon={max_polygon:?}");
    Ok(())
}

fn config_for(settings: &NavmeshSettings, aabb: bevy_rerecast::rerecast::Aabb3d) -> ConfigBuilder {
    ConfigBuilder {
        cell_size_fraction: settings.cell_size_fraction,
        cell_height_fraction: settings.cell_height_fraction,
        agent_height: settings.agent_height,
        agent_radius: settings.agent_radius,
        walkable_climb: settings.walkable_climb,
        walkable_slope_angle: settings.walkable_slope_angle,
        min_region_size: settings.min_region_size,
        merge_region_size: settings.merge_region_size,
        edge_max_len_factor: settings.edge_max_len_factor,
        max_simplification_error: settings.max_simplification_error,
        max_vertices_per_polygon: settings.max_vertices_per_polygon,
        detail_sample_dist: settings.detail_sample_dist,
        detail_sample_max_error: settings.detail_sample_max_error,
        tile_size: settings.tile_size,
        aabb,
        contour_flags: settings.contour_flags,
        tiling: settings.tiling,
        area_volumes: settings.area_volumes.clone(),
    }
}
