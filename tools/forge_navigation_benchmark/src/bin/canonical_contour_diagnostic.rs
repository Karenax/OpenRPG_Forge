//! Lab-only contour diagnostic for a provider rejection.
//!
//! It runs the public Rerecast stages and converts each generated contour independently. The
//! diagnostic identifies the contour that the public polygon triangulator rejects; it does not
//! alter the canonical triangles or repair the contour.
#![deny(unsafe_code)]
#![allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "the report uses the provider's validated u16 contour coordinates"
)]

use bevy_math::{UVec3, Vec3A};
use bevy_rerecast::NavmeshSettings;
use bevy_rerecast::rerecast::{AreaType, ConfigBuilder, ContourSet, HeightfieldBuilder, TriMesh};
use serde::Deserialize;
use serde_json::json;
use std::{error::Error, fs, path::Path};

#[path = "../geometry.rs"]
#[allow(
    dead_code,
    reason = "the diagnostic reuses only the exact global valley geometry builder"
)]
mod geometry;

const MAX_SIMPLIFICATION_ERROR: f32 = 1.1;
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
    reason = "the contour diagnostic preserves provider stages and rejection details in order"
)]
fn main() -> Result<(), Box<dyn Error>> {
    let args = std::env::args().skip(1).collect::<Vec<_>>();
    if args.len() != 2 {
        return Err(
            "usage: canonical_contour_diagnostic <canonical-input.json> <report.json>".into(),
        );
    }
    let input_path = Path::new(&args[0]);
    let output_path = Path::new(&args[1]);
    if output_path.exists() {
        return Err(format!("refusing to overwrite {}", output_path.display()).into());
    }
    let encoded = fs::read_to_string(input_path)?;
    let (mesh, agent_radius_m, agent_height_m) = if let Ok(report) =
        serde_json::from_str::<CanonicalReport>(&encoded)
    {
        (
            TriMesh {
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
            },
            AGENT_RADIUS_M,
            AGENT_HEIGHT_M,
        )
    } else {
        let sketch = forge_world::WorldSketchDocument::from_json(&encoded)?;
        let compiled = forge_world::compile_world_sketch(&sketch)?;
        let terrain = forge_world::TerrainGenerationPlan::from_document(&compiled.carved_terrain)?
            .generate()?;
        let bridge = compiled
            .bridge_placements
            .placements
            .first()
            .ok_or("missing bridge")?;
        let profile = forge_world::embedded_visual_valley_player_controller_profile()?;
        (
            geometry::exterior_bridge_mesh(
                &terrain,
                &compiled.traversal_audit,
                bridge,
                256.0,
                true,
            )?,
            profile.controller.capsule_radius_m,
            profile.controller.total_capsule_height_m(),
        )
    };
    let settings = NavmeshSettings {
        cell_size_fraction: 2.0,
        cell_height_fraction: 4.0,
        walkable_climb: 0.4,
        walkable_slope_angle: 45.0_f32.to_radians(),
        min_region_size: 8,
        merge_region_size: 20,
        edge_max_len_factor: 8,
        max_simplification_error: MAX_SIMPLIFICATION_ERROR,
        max_vertices_per_polygon: 3,
        detail_sample_dist: 6.0,
        detail_sample_max_error: 1.0,
        agent_radius: agent_radius_m,
        agent_height: agent_height_m,
        ..NavmeshSettings::default()
    };
    let aabb = mesh.compute_aabb().ok_or("empty canonical mesh")?;
    let config = ConfigBuilder {
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
    .build();
    let mut mesh = mesh;
    mesh.mark_walkable_triangles(config.walkable_slope_angle);
    let mut heightfield = HeightfieldBuilder {
        aabb: config.aabb,
        cell_size: config.cell_size,
        cell_height: config.cell_height,
    }
    .build()?;
    heightfield.rasterize_triangles(&mesh, config.walkable_climb)?;
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
    let mut entries = Vec::with_capacity(contours.contours.len());
    for (index, contour) in contours.contours.iter().enumerate() {
        let bounds = contour_bounds(contour);
        let signed_area_xz = signed_area_xz(contour);
        let single = ContourSet {
            contours: vec![contour.clone()],
            ..contours.clone()
        };
        let conversion = single.into_polygon_mesh(config.max_vertices_per_polygon);
        let inverted = conversion
            .as_ref()
            .ok()
            .map(|mesh| {
                mesh.polygons()
                    .enumerate()
                    .filter_map(|(polygon_index, indices)| {
                        let points = indices
                            .map(|index| mesh.vertices[usize::from(index)])
                            .collect::<Vec<_>>();
                        let signed = points
                            .iter()
                            .zip(points.iter().cycle().skip(1))
                            .take(points.len())
                            .map(|(a, b)| {
                                i64::from(a.x) * i64::from(b.z) - i64::from(b.x) * i64::from(a.z)
                            })
                            .sum::<i64>();
                        (signed >= 0).then(|| {
                            json!({"polygon_index": polygon_index, "signed_area2": signed,
                "points": points.iter().map(bevy_math::U16Vec3::to_array).collect::<Vec<_>>() })
                        })
                    })
                    .collect::<Vec<_>>()
            })
            .unwrap_or_default();
        entries.push(json!({
            "index": index,
            "region": contour.region.bits(),
            "area": contour.area.0,
            "raw_vertices": contour.raw_vertices.len(),
            "simplified_vertices": contour.vertices.len(),
            "bounds_xz_cells": bounds,
            "signed_area_xz_cells2": signed_area_xz,
            "adjacent_duplicate_xz": adjacent_duplicate_xz(contour),
            "status": if conversion.is_ok() { "ok" } else { "error" },
            "error": conversion.err().map(|error| error.to_string()),
            "inverted_or_degenerate_polygons": inverted,
            "vertices": contour.vertices.iter().map(|(point, flags)| json!({
                "point": [point.x, point.y, point.z],
                "flags": flags,
            })).collect::<Vec<_>>(),
        }));
    }
    let failures = entries
        .iter()
        .filter(|entry| entry["status"] == "error")
        .count();
    let output = json!({
        "schema": "forge.navigation.canonical-contour-diagnostic",
        "version": 1,
        "input": input_path,
        "max_simplification_error": MAX_SIMPLIFICATION_ERROR,
        "compact_grid": { "width": compact.width, "height": compact.height, "border_size": compact.border_size },
        "contour_count": entries.len(),
        "failed_contour_count": failures,
        "contours": entries,
    });
    fs::write(output_path, serde_json::to_vec_pretty(&output)?)?;
    Ok(())
}

fn contour_bounds(contour: &bevy_rerecast::rerecast::Contour) -> [u16; 4] {
    let min_x = contour
        .vertices
        .iter()
        .map(|(point, _)| point.x)
        .min()
        .unwrap_or(0);
    let max_x = contour
        .vertices
        .iter()
        .map(|(point, _)| point.x)
        .max()
        .unwrap_or(0);
    let min_z = contour
        .vertices
        .iter()
        .map(|(point, _)| point.z)
        .min()
        .unwrap_or(0);
    let max_z = contour
        .vertices
        .iter()
        .map(|(point, _)| point.z)
        .max()
        .unwrap_or(0);
    [min_x, max_x, min_z, max_z]
}

fn signed_area_xz(contour: &bevy_rerecast::rerecast::Contour) -> f32 {
    contour
        .vertices
        .iter()
        .zip(contour.vertices.iter().cycle().skip(1))
        .map(|((left, _), (right, _))| {
            f32::from(left.x) * f32::from(right.z) - f32::from(right.x) * f32::from(left.z)
        })
        .sum::<f32>()
        * 0.5
}

fn adjacent_duplicate_xz(contour: &bevy_rerecast::rerecast::Contour) -> usize {
    contour
        .vertices
        .iter()
        .zip(contour.vertices.iter().cycle().skip(1))
        .filter(|((left, _), (right, _))| left.x == right.x && left.z == right.z)
        .count()
}
