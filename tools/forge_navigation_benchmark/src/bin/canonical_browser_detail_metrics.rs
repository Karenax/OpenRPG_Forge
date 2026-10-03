//! WASM diagnostic that stops immediately before Rerecast detail generation.
//!
//! The pinned browser bundle panics inside `DetailNavmesh::new`. This entry point repeats only
//! the public stages before that call and returns the dimensions and integer products as JSON,
//! allowing a real browser to compare them with the native diagnostic without mutating the
//! provider or the canonical input.
#![deny(unsafe_code)]
#![allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "the probe reports the provider's checked u16 grid dimensions"
)]

use bevy_math::{UVec3, Vec3A};
use bevy_rerecast::{
    NavmeshSettings,
    rerecast::{AreaType, ConfigBuilder, HeightfieldBuilder, PolygonNavmesh, TriMesh},
};
use serde::{Deserialize, Serialize};
use std::fmt::Display;
#[cfg(target_arch = "wasm32")]
use wasm_bindgen::prelude::*;

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

#[derive(Debug, Serialize)]
struct Metrics {
    target: &'static str,
    config_width: u16,
    config_height: u16,
    tile_size: u16,
    border_size: u16,
    cell_size: f32,
    cell_height: f32,
    compact_width: u16,
    compact_height: u16,
    cells: usize,
    spans: usize,
    polygon_count: usize,
    polygon_vertices: usize,
    max_vertices_per_polygon: usize,
    max_edge_error: f32,
    height_search_radius: u32,
    max_detail_width: u16,
    max_detail_height: u16,
    detail_patch_product_u64: u64,
    detail_capacity_product_u64: u64,
    largest_polygon: Option<[usize; 3]>,
}

#[cfg(target_arch = "wasm32")]
#[wasm_bindgen]
pub fn run_canonical_browser_detail_metrics(input_json: &str) -> Result<String, JsValue> {
    build_metrics(input_json)
        .and_then(|metrics| serde_json::to_string(&metrics).map_err(|error| error.to_string()))
        .map_err(|error| JsValue::from_str(&error))
}

#[allow(
    clippy::too_many_lines,
    reason = "the diagnostic records each public provider stage and its bounded integer metrics in order"
)]
fn build_metrics(input_json: &str) -> Result<Metrics, String> {
    let report: CanonicalReport = serde_json::from_str(input_json).map_err(display)?;
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
    let settings = settings();
    let aabb = mesh.compute_aabb().ok_or("empty canonical mesh")?;
    let config = config_for(&settings, aabb).build();
    let mut trimesh = mesh;
    trimesh.mark_walkable_triangles(config.walkable_slope_angle);
    let mut heightfield = HeightfieldBuilder {
        aabb: config.aabb,
        cell_size: config.cell_size,
        cell_height: config.cell_height,
    }
    .build()
    .map_err(display)?;
    heightfield
        .rasterize_triangles(&trimesh, config.walkable_climb)
        .map_err(display)?;
    heightfield.filter_low_hanging_walkable_obstacles(config.walkable_climb);
    heightfield.filter_ledge_spans(config.walkable_height, config.walkable_climb);
    heightfield.filter_walkable_low_height_spans(config.walkable_height);
    let mut compact = heightfield
        .into_compact(config.walkable_height, config.walkable_climb)
        .map_err(display)?;
    compact.erode_walkable_area(config.walkable_radius);
    compact.build_distance_field();
    compact
        .build_regions(
            config.border_size,
            config.min_region_area,
            config.merge_region_area,
        )
        .map_err(display)?;
    let contours = compact.build_contours(
        config.max_simplification_error,
        config.max_edge_len,
        config.contour_flags,
    );
    let polygon = contours
        .into_polygon_mesh(config.max_vertices_per_polygon)
        .map_err(display)?;
    let nvp = usize::from(polygon.max_vertices_per_polygon);
    let mut max_width = 0_u16;
    let mut max_height = 0_u16;
    let mut poly_vert_count = 0_usize;
    let mut largest_polygon = None;
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
        if u32::from(width) * u32::from(height) > u32::from(max_width) * u32::from(max_height) {
            largest_polygon = Some([index, usize::from(width), usize::from(height)]);
        }
        max_width = max_width.max(width);
        max_height = max_height.max(height);
    }
    let height_search_radius = 1_u32.max(polygon.max_edge_error.ceil() as u32);
    Ok(Metrics {
        target: "wasm32-unknown-unknown",
        config_width: config.width,
        config_height: config.height,
        tile_size: config.tile_size,
        border_size: config.border_size,
        cell_size: config.cell_size,
        cell_height: config.cell_height,
        compact_width: compact.width,
        compact_height: compact.height,
        cells: compact.cells.len(),
        spans: compact.spans.len(),
        polygon_count: polygon.polygon_count(),
        polygon_vertices: polygon.vertices.len(),
        max_vertices_per_polygon: nvp,
        max_edge_error: polygon.max_edge_error,
        height_search_radius,
        max_detail_width: max_width,
        max_detail_height: max_height,
        detail_patch_product_u64: u64::from(max_width) * u64::from(max_height),
        detail_capacity_product_u64: (poly_vert_count as u64) * 2,
        largest_polygon,
    })
}

fn settings() -> NavmeshSettings {
    NavmeshSettings {
        cell_size_fraction: 2.0,
        cell_height_fraction: 4.0,
        walkable_climb: 0.4,
        walkable_slope_angle: 45.0_f32.to_radians(),
        min_region_size: 8,
        merge_region_size: 20,
        edge_max_len_factor: 8,
        max_simplification_error: 1.3,
        max_vertices_per_polygon: 3,
        detail_sample_dist: 6.0,
        detail_sample_max_error: 1.0,
        agent_radius: AGENT_RADIUS_M,
        agent_height: AGENT_HEIGHT_M,
        ..NavmeshSettings::default()
    }
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

fn display(error: impl Display) -> String {
    error.to_string()
}

#[cfg(target_arch = "wasm32")]
fn main() {}

#[cfg(not(target_arch = "wasm32"))]
fn main() -> Result<(), Box<dyn std::error::Error>> {
    let path = std::env::args()
        .nth(1)
        .ok_or("usage: canonical_browser_detail_metrics <canonical-input.json>")?;
    let metrics = build_metrics(&std::fs::read_to_string(path)?).map_err(std::io::Error::other)?;
    println!("{}", serde_json::to_string(&metrics)?);
    Ok(())
}
