//! Lab-only Rerecast adapter with a bounded preflight for the WASM detail pass.
//!
//! The pinned browser campaign exposed a `usize` overflow in the detail-mesh jitter sequence on
//! wasm32. The vendored lab provider fixes that arithmetic without changing the low bits. This
//! module keeps the public provider stages unchanged and refuses an invalid or unbounded detail
//! radius before entering the provider. A rejection is deliberate: it is not a detail-disabled
//! fallback and therefore cannot be mistaken for a valid navmesh.

use bevy_rerecast::{
    Navmesh, NavmeshSettings,
    rerecast::{Aabb3d, ConfigBuilder, DetailNavmesh, HeightfieldBuilder, TriMesh},
};
use thiserror::Error;

/// Uniform conservative contour simplification used by every lab replay.
pub const CONSERVATIVE_MAX_SIMPLIFICATION_ERROR: f32 = 1.1;

pub fn settings_for_agent(agent_radius_m: f32, agent_height_m: f32) -> NavmeshSettings {
    NavmeshSettings {
        cell_size_fraction: 2.0,
        cell_height_fraction: 4.0,
        walkable_climb: 0.4,
        walkable_slope_angle: 45.0_f32.to_radians(),
        min_region_size: 8,
        merge_region_size: 20,
        edge_max_len_factor: 8,
        max_simplification_error: CONSERVATIVE_MAX_SIMPLIFICATION_ERROR,
        max_vertices_per_polygon: 3,
        detail_sample_dist: 6.0,
        detail_sample_max_error: 1.0,
        agent_radius: agent_radius_m,
        agent_height: agent_height_m,
        ..NavmeshSettings::default()
    }
}

/// Generates a navmesh while guarding the integer domain used by the detail-height search.
pub fn generate_navmesh(
    mut trimesh: TriMesh,
    settings: NavmeshSettings,
) -> Result<Navmesh, GuardedProviderError> {
    let aabb = trimesh
        .compute_aabb()
        .ok_or(GuardedProviderError::EmptyTriangleMesh)?;
    let config = settings_to_config(&settings, aabb).build();

    trimesh.mark_walkable_triangles(config.walkable_slope_angle);

    let mut heightfield = HeightfieldBuilder {
        aabb: config.aabb,
        cell_size: config.cell_size,
        cell_height: config.cell_height,
    }
    .build()
    .map_err(provider_error)?;
    heightfield
        .rasterize_triangles(&trimesh, config.walkable_climb)
        .map_err(provider_error)?;
    heightfield.filter_low_hanging_walkable_obstacles(config.walkable_climb);
    heightfield.filter_ledge_spans(config.walkable_height, config.walkable_climb);
    heightfield.filter_walkable_low_height_spans(config.walkable_height);

    let mut compact = heightfield
        .into_compact(config.walkable_height, config.walkable_climb)
        .map_err(provider_error)?;
    compact.erode_walkable_area(config.walkable_radius);
    for volume in &config.area_volumes {
        compact.mark_convex_poly_area(volume);
    }
    compact.build_distance_field();
    compact
        .build_regions(
            config.border_size,
            config.min_region_area,
            config.merge_region_area,
        )
        .map_err(provider_error)?;
    let contours = compact.build_contours(
        config.max_simplification_error,
        config.max_edge_len,
        config.contour_flags,
    );
    let polygon = contours
        .into_polygon_mesh(config.max_vertices_per_polygon)
        .map_err(provider_error)?;

    guard_detail_radius(polygon.max_edge_error, compact.width, compact.height)?;
    let detail = DetailNavmesh::new(
        &polygon,
        &compact,
        config.detail_sample_dist,
        config.detail_sample_max_error,
    )
    .map_err(provider_error)?;

    Ok(Navmesh {
        polygon,
        detail,
        settings,
    })
}

fn guard_detail_radius(
    max_edge_error: f32,
    compact_width: u16,
    compact_height: u16,
) -> Result<(), GuardedProviderError> {
    if !max_edge_error.is_finite() || max_edge_error < 0.0 {
        return Err(GuardedProviderError::InvalidDetailRadius { max_edge_error });
    }

    // A radius above the compact grid is unnecessary for the bounded detail search and is
    // rejected before the provider enters that loop. The explicit 2^31 boundary also avoids
    // f32-to-u32 saturation at the edge of the type. This guard is defense in depth; it is not
    // the wasm32 jitter-overflow fix.
    if max_edge_error.ceil() >= 2_147_483_648.0 {
        return Err(GuardedProviderError::DetailRadiusOverflow { max_edge_error });
    }
    let radius = (max_edge_error.ceil() as u32).max(1);
    let grid_limit = u32::from(compact_width.max(compact_height));
    if radius > grid_limit {
        return Err(GuardedProviderError::DetailRadiusOutOfGrid { radius, grid_limit });
    }
    radius
        .checked_mul(2)
        .and_then(|value| value.checked_add(1))
        .ok_or(GuardedProviderError::DetailRadiusOverflow { max_edge_error })?;
    Ok(())
}

fn settings_to_config(settings: &NavmeshSettings, aabb: Aabb3d) -> ConfigBuilder {
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

fn provider_error(error: impl std::fmt::Display) -> GuardedProviderError {
    GuardedProviderError::Build(error.to_string())
}

#[derive(Debug, Error)]
pub enum GuardedProviderError {
    #[error("navigation input triangle mesh is empty")]
    EmptyTriangleMesh,
    #[error("Rerecast build failed: {0}")]
    Build(String),
    #[error("Rerecast detail radius is not finite: {max_edge_error}")]
    InvalidDetailRadius { max_edge_error: f32 },
    #[error("Rerecast detail radius would overflow: {max_edge_error}")]
    DetailRadiusOverflow { max_edge_error: f32 },
    #[error("Rerecast detail radius {radius} exceeds compact grid bound {grid_limit}")]
    DetailRadiusOutOfGrid { radius: u32, grid_limit: u32 },
}
