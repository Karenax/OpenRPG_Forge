use crate::{ForgeCoordinates, TileInput, point, source_hash};
use glam::{UVec3, Vec2, Vec3, Vec3A};
use landmass::{HeightNavigationMesh, HeightPolygon, NavigationMesh, ValidNavigationMesh};
use rerecast::{
    Aabb3d, AreaType, Config, ConfigBuilder, ConvexVolume, DetailNavmesh, HeightfieldBuilder,
    PolygonNavmesh, TriMesh,
};
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct BuiltTile {
    pub key: crate::TileKey,
    pub profile: crate::AgentProfile,
    pub revision: u64,
    pub source_hash: String,
    pub token: u64,
    pub source_triangles: usize,
    pub curved_obstacles: usize,
    pub curved_footprint_excess_bound_m: f32,
    pub polygon: PolygonNavmesh,
    pub detail: DetailNavmesh,
}
impl BuiltTile {
    /// Direct qualified conversion, derived from `landmass_rerecast` 0.3.0,
    /// MIT OR Apache-2.0, Andriy Dev (notice in third_party/navigation-NOTICE.md).
    #[must_use]
    pub fn raw(&self) -> NavigationMesh<ForgeCoordinates> {
        let mesh = &self.polygon;
        let scale = Vec3::new(mesh.cell_size, mesh.cell_height, mesh.cell_size);
        let nvp = usize::from(mesh.max_vertices_per_polygon);
        NavigationMesh {
            vertices: mesh
                .vertices
                .iter()
                .map(|v| point((mesh.aabb.min + v.as_vec3() * scale).to_array()))
                .collect(),
            polygons: (0..mesh.polygon_count())
                .map(|i| {
                    mesh.polygons[i * nvp..][..nvp]
                        .iter()
                        .rev()
                        .filter(|index| **index != PolygonNavmesh::NO_INDEX)
                        .map(|index| usize::from(*index))
                        .collect()
                })
                .collect(),
            polygon_type_indices: mesh.areas.iter().map(|area| usize::from(area.0)).collect(),
            height_mesh: Some(HeightNavigationMesh {
                polygons: self
                    .detail
                    .meshes
                    .iter()
                    .map(|mesh| HeightPolygon {
                        base_vertex_index: mesh.base_vertex_index,
                        vertex_count: mesh.vertex_count,
                        base_triangle_index: mesh.base_triangle_index,
                        triangle_count: mesh.triangle_count,
                    })
                    .collect(),
                triangles: self
                    .detail
                    .triangles
                    .iter()
                    .map(|&[a, b, c]| [a, c, b])
                    .collect(),
                vertices: self
                    .detail
                    .vertices
                    .iter()
                    .map(|v| point(v.to_array()))
                    .collect(),
            }),
        }
    }
    pub fn validate(&self) -> Result<ValidNavigationMesh<ForgeCoordinates>, String> {
        self.profile.validate()?;
        let mesh = &self.polygon;
        if mesh.max_vertices_per_polygon != 3
            || self.curved_obstacles > 256
            || !self.curved_footprint_excess_bound_m.is_finite()
            || !(0.0..=100_000.0).contains(&self.curved_footprint_excess_bound_m)
            || !mesh.polygons.len().is_multiple_of(3)
            || mesh.vertices.len() > usize::from(u16::MAX)
            || mesh.polygons.len() > usize::from(u16::MAX) * 3
            || mesh.areas.len() != mesh.polygons.len() / 3
            || self.detail.meshes.len() != mesh.polygons.len() / 3
            || !mesh.aabb.min.is_finite()
            || !mesh.aabb.max.is_finite()
            || mesh.aabb.min.cmpgt(mesh.aabb.max).any()
            || !mesh.cell_size.is_finite()
            || mesh.cell_size <= 0.0
            || !mesh.cell_height.is_finite()
            || mesh.cell_height <= 0.0
            || self.detail.vertices.len() > 2_000_000
            || self.detail.triangles.len() > 2_000_000
            || self
                .detail
                .vertices
                .iter()
                .any(|point| !point.is_finite() || point.abs().max_element() > 100_000.0)
            || mesh.polygons.iter().any(|&index| {
                index != PolygonNavmesh::NO_INDEX && usize::from(index) >= mesh.vertices.len()
            })
            || mesh.vertices.iter().any(|vertex| {
                let world = mesh.aabb.min
                    + vertex.as_vec3()
                        * Vec3::new(mesh.cell_size, mesh.cell_height, mesh.cell_size);
                !world.is_finite() || world.abs().max_element() > 100_000.0
            })
        {
            return Err("invalid or over-budget navigation output structure".into());
        }
        for detail in &self.detail.meshes {
            let vertices = usize::try_from(
                u64::from(detail.base_vertex_index) + u64::from(detail.vertex_count),
            )
            .map_err(|error| error.to_string())?;
            let triangles = usize::try_from(
                u64::from(detail.base_triangle_index) + u64::from(detail.triangle_count),
            )
            .map_err(|error| error.to_string())?;
            if vertices > self.detail.vertices.len() || triangles > self.detail.triangles.len() {
                return Err("navigation output detail range exceeds its buffers".into());
            }
        }
        self.raw()
            .validate()
            .map_err(|error| format!("raw navigation output rejected: {error}"))
    }
}

fn validate_input(input: &TileInput) -> Result<(), String> {
    input.profile.validate()?;
    if input.vertices.is_empty()
        || input.triangles.is_empty()
        || input.vertices.len() > 600_000
        || input.triangles.len() > 200_000
        || input.areas.len() > 256
        || input.curved_obstacles.len() > 256
        || input.curved_obstacles.len() > input.areas.iter().filter(|area| area.area == 0).count()
        || input.policy_source_hash.as_ref().is_some_and(|hash| {
            hash.len() != 64 || !hash.bytes().all(|byte| byte.is_ascii_hexdigit())
        })
        || input
            .vertices
            .iter()
            .flatten()
            .any(|v| !v.is_finite() || v.abs() > 100_000.0)
        || input
            .triangles
            .iter()
            .flatten()
            .any(|&i| usize::try_from(i).map_or(true, |i| i >= input.vertices.len()))
    {
        return Err("empty, invalid or over-budget canonical navigation geometry".into());
    }
    if source_hash(input)? != input.source_hash {
        return Err("navigation geometry hash mismatch".into());
    }
    for area in &input.areas {
        area.validate()?;
    }
    for obstacle in &input.curved_obstacles {
        obstacle.validate(input.profile)?;
    }
    Ok(())
}

fn config(input: &TileInput, source_bounds: Aabb3d) -> Result<Config, String> {
    if source_bounds.max.y - source_bounds.min.y > 512.0 {
        return Err("navigation tile vertical span budget exceeded".into());
    }
    let (min, max) = input.key.bounds(input.profile);
    let border = input.profile.border_m();
    Ok(ConfigBuilder {
        aabb: Aabb3d {
            min: Vec3::new(min[0] - border, source_bounds.min.y - 0.1, min[1] - border),
            max: Vec3::new(
                max[0] + border,
                source_bounds.max.y + input.profile.height_m,
                max[1] + border,
            ),
        },
        agent_radius: input.profile.radius_m,
        agent_height: input.profile.height_m,
        cell_size_fraction: 2.0,
        cell_height_fraction: 4.0,
        walkable_climb: input.profile.step_m,
        walkable_slope_angle: input.profile.max_slope_radians,
        min_region_size: 8,
        merge_region_size: 20,
        edge_max_len_factor: 8,
        max_simplification_error: crate::CONTOUR_ERROR_CELLS,
        max_vertices_per_polygon: 3,
        detail_sample_dist: 6.0,
        detail_sample_max_error: 1.0,
        tile_size: 192,
        tiling: true,
        area_volumes: input
            .areas
            .iter()
            .map(|area| ConvexVolume {
                vertices: area
                    .vertices_xz
                    .iter()
                    .map(|v| Vec2::from_array(*v))
                    .collect(),
                min_y: area.min_y,
                max_y: area.max_y,
                area: AreaType(area.area),
            })
            .collect(),
        ..ConfigBuilder::default()
    }
    .build())
}

pub fn generate_tile(input: TileInput) -> Result<BuiltTile, String> {
    generate_tile_cancellable(input, || false)
}

/// Native jobs cooperate between provider stages; browser jobs are terminated by their Worker owner.
pub fn generate_tile_cancellable(
    input: TileInput,
    cancelled: impl Fn() -> bool,
) -> Result<BuiltTile, String> {
    let check = || {
        if cancelled() {
            Err("navigation generation cancelled or timed out".to_owned())
        } else {
            Ok(())
        }
    };
    check()?;
    validate_input(&input)?;
    let mut mesh = TriMesh {
        vertices: input
            .vertices
            .iter()
            .map(|v| Vec3A::from_array(*v))
            .collect(),
        indices: input
            .triangles
            .iter()
            .map(|v| UVec3::from_array(*v))
            .collect(),
        area_types: vec![AreaType::NOT_WALKABLE; input.triangles.len()],
    };
    let source_bounds = mesh
        .compute_aabb()
        .ok_or("missing canonical triangle bounds")?;
    let config = config(&input, source_bounds)?;
    mesh.mark_walkable_triangles(config.walkable_slope_angle);
    let mut field = HeightfieldBuilder {
        aabb: config.aabb,
        cell_size: config.cell_size,
        cell_height: config.cell_height,
    }
    .build()
    .map_err(|error| error.to_string())?;
    field
        .rasterize_triangles(&mesh, config.walkable_climb)
        .map_err(|error| error.to_string())?;
    check()?;
    field.filter_low_hanging_walkable_obstacles(config.walkable_climb);
    field.filter_ledge_spans(config.walkable_height, config.walkable_climb);
    field.filter_walkable_low_height_spans(config.walkable_height);
    let mut compact = field
        .into_compact(config.walkable_height, config.walkable_climb)
        .map_err(|error| error.to_string())?;
    compact.erode_walkable_area(config.walkable_radius);
    check()?;
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
        .map_err(|error| error.to_string())?;
    check()?;
    let contours = compact.build_contours(
        config.max_simplification_error,
        config.max_edge_len,
        config.contour_flags,
    );
    let polygon = contours
        .into_polygon_mesh(config.max_vertices_per_polygon)
        .map_err(|error| error.to_string())?;
    check()?;
    let detail = DetailNavmesh::new(
        &polygon,
        &compact,
        config.detail_sample_dist,
        config.detail_sample_max_error,
    )
    .map_err(|error| error.to_string())?;
    check()?;
    let result = BuiltTile {
        key: input.key,
        profile: input.profile,
        revision: input.revision,
        source_hash: input.source_hash,
        token: input.token,
        source_triangles: input.triangles.len(),
        curved_obstacles: input.curved_obstacles.len(),
        curved_footprint_excess_bound_m: input
            .curved_obstacles
            .iter()
            .map(|obstacle| obstacle.footprint_excess_bound_m)
            .fold(0.0, f32::max),
        polygon,
        detail,
    };
    result.validate()?;
    Ok(result)
}
