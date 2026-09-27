use std::{collections::HashMap, sync::Arc};

use bevy_landmass::{
    ArchipelagoOptions, FromAgentRadius, NavigationMesh3d, PathStep, PointSampleDistance3d,
    coords::ThreeD,
};
use bevy_math::Vec3;
use bevy_rerecast::Navmesh;
use landmass::{
    Agent, AgentState, AnimationLink, Archipelago, FindPathError, Island, PermittedAnimationLinks,
    Transform,
};
use landmass_rerecast::convert_rerecast_navmesh_to_landmass_navmesh;
use serde::Serialize;
use thiserror::Error;

const SIMULATION_DELTA_SECONDS: f32 = 1.0 / 30.0;
const SIMULATION_TICKS: usize = 600;

#[derive(Clone, Debug, Serialize)]
pub struct PathProbe {
    pub found: bool,
    pub waypoint_count: usize,
    pub animation_link_count: usize,
    pub waypoints_m: Vec<[f32; 3]>,
}

impl PathProbe {
    fn not_found() -> Self {
        Self {
            found: false,
            waypoint_count: 0,
            animation_link_count: 0,
            waypoints_m: Vec::new(),
        }
    }

    fn from_steps(steps: Vec<PathStep<ThreeD>>) -> Self {
        let waypoint_count = steps
            .iter()
            .filter(|step| matches!(step, PathStep::Waypoint(_)))
            .count();
        let animation_link_count = steps.len() - waypoint_count;
        let waypoints_m = steps
            .into_iter()
            .flat_map(|step| match step {
                PathStep::Waypoint(point) => vec![point.to_array()],
                PathStep::AnimationLink {
                    start_point,
                    end_point,
                    ..
                } => vec![start_point.to_array(), end_point.to_array()],
            })
            .collect();
        Self {
            found: true,
            waypoint_count,
            animation_link_count,
            waypoints_m,
        }
    }
}

pub fn query_rerecast_path(
    navmesh: &Navmesh,
    start_m: Vec3,
    end_m: Vec3,
    agent_radius_m: f32,
) -> Result<PathProbe, NavigationError> {
    let raw = convert_rerecast_navmesh_to_landmass_navmesh(navmesh);
    query_raw_path(raw, start_m, end_m, agent_radius_m)
}

pub fn query_rerecast_path_without_height_mesh(
    navmesh: &Navmesh,
    start_m: Vec3,
    end_m: Vec3,
    agent_radius_m: f32,
) -> Result<PathProbe, NavigationError> {
    let mut raw = convert_rerecast_navmesh_to_landmass_navmesh(navmesh);
    raw.height_mesh = None;
    query_raw_path(raw, start_m, end_m, agent_radius_m)
}

/// Builds a diagnostic-only adapter copy for the winding defect observed on
/// the full `WorldSketch` valley. This deliberately operates on Landmass' public
/// raw navigation document, and reports every mutation made before validation.
/// It is not a production fallback: the unmodified provider output remains the
/// authoritative result of the benchmark.
pub fn normalize_rerecast_winding(
    navmesh: &Navmesh,
) -> Result<(WindingNormalization, NavigationMesh3d), NavigationError> {
    let mut raw = convert_rerecast_navmesh_to_landmass_navmesh(navmesh);
    let normalization = normalize_negative_winding(&mut raw)?;
    Ok((normalization, raw))
}

pub fn query_raw_path(
    raw: NavigationMesh3d,
    start_m: Vec3,
    end_m: Vec3,
    agent_radius_m: f32,
) -> Result<PathProbe, NavigationError> {
    let valid = raw
        .validate()
        .map_err(|error| NavigationError::InvalidMesh(error.to_string()))?;
    let mut archipelago = archipelago_with_mesh(valid, agent_radius_m);
    archipelago.update(SIMULATION_DELTA_SECONDS);
    query_archipelago_path(
        &archipelago,
        start_m,
        end_m,
        agent_radius_m,
        PermittedAnimationLinks::All,
    )
}

pub fn off_mesh_link_probe() -> Result<(PathProbe, PathProbe), NavigationError> {
    let valid = disconnected_platforms_document()
        .validate()
        .map_err(|error| NavigationError::InvalidMesh(error.to_string()))?;
    let radius = 0.35;
    let mut archipelago = archipelago_with_mesh(valid, radius);
    archipelago.update(SIMULATION_DELTA_SECONDS);
    let without_link = query_archipelago_path(
        &archipelago,
        Vec3::new(-4.0, 0.0, 0.0),
        Vec3::new(4.0, 0.0, 0.0),
        radius,
        PermittedAnimationLinks::All,
    )?;

    archipelago.add_animation_link(AnimationLink::<ThreeD> {
        start_edge: (Vec3::new(-1.0, 0.0, -0.5), Vec3::new(-1.0, 0.0, 0.5)),
        end_edge: (Vec3::new(1.0, 0.0, -0.5), Vec3::new(1.0, 0.0, 0.5)),
        kind: 7,
        cost: 2.0,
        bidirectional: true,
    });
    archipelago.update(SIMULATION_DELTA_SECONDS);
    let with_link = query_archipelago_path(
        &archipelago,
        Vec3::new(-4.0, 0.0, 0.0),
        Vec3::new(4.0, 0.0, 0.0),
        radius,
        PermittedAnimationLinks::All,
    )?;
    Ok((without_link, with_link))
}

pub fn area_cost_probe() -> Result<PathProbe, NavigationError> {
    let valid = area_cost_document()
        .validate()
        .map_err(|error| NavigationError::InvalidMesh(error.to_string()))?;
    let radius = 0.35;
    let mut archipelago = archipelago_with_mesh(valid, radius);
    archipelago
        .set_type_index_cost(1, 100.0)
        .map_err(|error| NavigationError::AreaCost(error.to_string()))?;
    archipelago.update(SIMULATION_DELTA_SECONDS);
    query_archipelago_path(
        &archipelago,
        Vec3::new(-2.0, 0.0, 0.0),
        Vec3::new(2.0, 0.0, 0.0),
        radius,
        PermittedAnimationLinks::All,
    )
}

#[derive(Clone, Copy, Debug, Serialize)]
pub struct AvoidanceProbe {
    pub reached_targets: bool,
    pub minimum_separation_m: f32,
    pub maximum_lateral_speed_mps: f32,
    pub simulated_ticks: usize,
}

#[derive(Clone, Debug, Serialize)]
pub struct PolygonWindingDiagnostic {
    pub polygon_index: usize,
    pub signed_area_xz_m2: f32,
    pub vertices_m: Vec<[f32; 3]>,
}

#[derive(Clone, Copy, Debug, Serialize)]
pub struct WindingNormalization {
    pub normalized_polygon_count: usize,
    pub normalized_height_triangle_count: usize,
}

pub fn invalid_polygon_winding(navmesh: &Navmesh) -> Vec<PolygonWindingDiagnostic> {
    let raw = convert_rerecast_navmesh_to_landmass_navmesh(navmesh);
    raw.polygons
        .iter()
        .enumerate()
        .filter_map(|(polygon_index, polygon)| {
            let vertices = polygon
                .iter()
                .filter_map(|index| raw.vertices.get(*index).copied())
                .collect::<Vec<_>>();
            let signed_area_xz_m2 = signed_area_xz(&vertices);
            (vertices.len() < 3 || signed_area_xz_m2 <= 0.0).then(|| PolygonWindingDiagnostic {
                polygon_index,
                signed_area_xz_m2,
                vertices_m: vertices
                    .into_iter()
                    .map(|vertex| vertex.to_array())
                    .collect(),
            })
        })
        .collect()
}

fn normalize_negative_winding(
    mesh: &mut NavigationMesh3d,
) -> Result<WindingNormalization, NavigationError> {
    let mut normalized_polygon_count = 0;
    for polygon in &mut mesh.polygons {
        let vertices = polygon
            .iter()
            .map(|index| {
                mesh.vertices
                    .get(*index)
                    .copied()
                    .ok_or(NavigationError::InvalidPolygonVertex(*index))
            })
            .collect::<Result<Vec<_>, _>>()?;
        if signed_area_xz(&vertices) < 0.0 {
            polygon.reverse();
            normalized_polygon_count += 1;
        }
    }

    let mut normalized_height_triangle_count = 0;
    if let Some(height_mesh) = &mut mesh.height_mesh {
        for (polygon_index, polygon) in height_mesh.polygons.iter().enumerate() {
            let start = usize::try_from(polygon.base_triangle_index)
                .map_err(|_| NavigationError::InvalidHeightTriangleRange(polygon_index))?;
            let triangle_count = usize::try_from(polygon.triangle_count)
                .map_err(|_| NavigationError::InvalidHeightTriangleRange(polygon_index))?;
            let end = start
                .checked_add(triangle_count)
                .ok_or(NavigationError::InvalidHeightTriangleRange(polygon_index))?;
            if end > height_mesh.triangles.len() {
                return Err(NavigationError::InvalidHeightTriangleRange(polygon_index));
            }
            let base_vertex = usize::try_from(polygon.base_vertex_index)
                .map_err(|_| NavigationError::InvalidHeightVertex(polygon_index))?;

            for triangle_index in start..end {
                let triangle = height_mesh.triangles[triangle_index];
                let vertices = triangle.map(|local_index| {
                    base_vertex
                        .checked_add(usize::from(local_index))
                        .and_then(|index| height_mesh.vertices.get(index))
                        .copied()
                        .ok_or(NavigationError::InvalidHeightVertex(polygon_index))
                });
                let [a, b, c] = vertices;
                let vertices = [a?, b?, c?];
                if height_triangle_orientation_after_validation(vertices) < 0.0 {
                    height_mesh.triangles[triangle_index].swap(1, 2);
                    normalized_height_triangle_count += 1;
                }
            }
        }
    }

    Ok(WindingNormalization {
        normalized_polygon_count,
        normalized_height_triangle_count,
    })
}

fn signed_area_xz(vertices: &[Vec3]) -> f32 {
    vertices
        .iter()
        .zip(vertices.iter().cycle().skip(1))
        .take(vertices.len())
        .map(|(current, next)| current.x * next.z - next.x * current.z)
        .sum::<f32>()
        * 0.5
}

fn height_triangle_orientation_after_validation([a, b, c]: [Vec3; 3]) -> f32 {
    // bevy_landmass::ThreeD flips each triangle, then maps (x, y, z) to
    // Landmass' (x, -z, y). Mirror those exact operations so near-degenerate
    // triangles are classified using the provider's own validation semantics.
    let transformed_a = bevy_math::Vec2::new(a.x, -a.z);
    let transformed_b = bevy_math::Vec2::new(c.x, -c.z);
    let transformed_c = bevy_math::Vec2::new(b.x, -b.z);
    (transformed_b - transformed_a).perp_dot(transformed_c - transformed_a)
}

pub fn local_avoidance_probe(navmesh: &Navmesh) -> Result<AvoidanceProbe, NavigationError> {
    let raw = convert_rerecast_navmesh_to_landmass_navmesh(navmesh);
    let valid = raw
        .validate()
        .map_err(|error| NavigationError::InvalidMesh(error.to_string()))?;
    let radius = 0.35;
    let mut archipelago = archipelago_with_mesh(valid, radius);

    let mut first =
        Agent::<ThreeD>::create(Vec3::new(-7.0, 0.0, -0.15), Vec3::ZERO, radius, 2.0, 3.0);
    first.current_target = Some(Vec3::new(7.0, 0.0, -0.15));
    let first_id = archipelago.add_agent(first);
    let mut second =
        Agent::<ThreeD>::create(Vec3::new(7.0, 0.0, 0.15), Vec3::ZERO, radius, 2.0, 3.0);
    second.current_target = Some(Vec3::new(-7.0, 0.0, 0.15));
    let second_id = archipelago.add_agent(second);

    let mut minimum_separation_m = f32::INFINITY;
    let mut maximum_lateral_speed_mps = 0.0_f32;
    let mut simulated_ticks = 0;
    for tick in 0..SIMULATION_TICKS {
        archipelago.update(SIMULATION_DELTA_SECONDS);
        let first_velocity = *archipelago
            .get_agent(first_id)
            .ok_or(NavigationError::MissingAgent)?
            .get_desired_velocity();
        let second_velocity = *archipelago
            .get_agent(second_id)
            .ok_or(NavigationError::MissingAgent)?
            .get_desired_velocity();
        maximum_lateral_speed_mps = maximum_lateral_speed_mps
            .max(first_velocity.z.abs())
            .max(second_velocity.z.abs());

        for (id, velocity) in [(first_id, first_velocity), (second_id, second_velocity)] {
            let agent = archipelago
                .get_agent_mut(id)
                .ok_or(NavigationError::MissingAgent)?;
            agent.velocity = velocity;
            agent.position += velocity * SIMULATION_DELTA_SECONDS;
        }
        let first_position = archipelago
            .get_agent(first_id)
            .ok_or(NavigationError::MissingAgent)?
            .position;
        let second_position = archipelago
            .get_agent(second_id)
            .ok_or(NavigationError::MissingAgent)?
            .position;
        minimum_separation_m = minimum_separation_m.min(first_position.distance(second_position));
        simulated_ticks = tick + 1;

        let first_state = archipelago
            .get_agent(first_id)
            .ok_or(NavigationError::MissingAgent)?
            .state();
        let second_state = archipelago
            .get_agent(second_id)
            .ok_or(NavigationError::MissingAgent)?
            .state();
        if first_state == AgentState::ReachedTarget && second_state == AgentState::ReachedTarget {
            break;
        }
    }

    let reached_targets = [first_id, second_id].into_iter().all(|id| {
        archipelago
            .get_agent(id)
            .is_some_and(|agent| agent.state() == AgentState::ReachedTarget)
    });
    Ok(AvoidanceProbe {
        reached_targets,
        minimum_separation_m,
        maximum_lateral_speed_mps,
        simulated_ticks,
    })
}

fn archipelago_with_mesh(
    valid: bevy_landmass::ValidNavigationMesh3d,
    agent_radius_m: f32,
) -> Archipelago<ThreeD> {
    let mut options = ArchipelagoOptions::<ThreeD>::from_agent_radius(agent_radius_m);
    options.point_sample_distance = generous_sample_distance(agent_radius_m);
    let mut archipelago = Archipelago::new(options);
    archipelago.add_island(Island::new(Transform::default(), Arc::new(valid)));
    archipelago
}

fn query_archipelago_path(
    archipelago: &Archipelago<ThreeD>,
    start_m: Vec3,
    end_m: Vec3,
    agent_radius_m: f32,
    permitted_links: PermittedAnimationLinks,
) -> Result<PathProbe, NavigationError> {
    let sample_distance = generous_sample_distance(agent_radius_m);
    let start = archipelago
        .sample_point(start_m, &sample_distance)
        .map_err(|error| NavigationError::StartSample(error.to_string()))?;
    let end = archipelago
        .sample_point(end_m, &sample_distance)
        .map_err(|error| NavigationError::EndSample(error.to_string()))?;
    match archipelago.find_path(&start, &end, &HashMap::new(), permitted_links) {
        Ok(steps) => Ok(PathProbe::from_steps(steps)),
        Err(FindPathError::NoPathFound) => Ok(PathProbe::not_found()),
        Err(error) => Err(NavigationError::Path(error.to_string())),
    }
}

fn generous_sample_distance(agent_radius_m: f32) -> PointSampleDistance3d {
    PointSampleDistance3d {
        horizontal_distance: agent_radius_m * 4.0,
        distance_above: 2.0,
        distance_below: 2.0,
        vertical_preference_ratio: 2.0,
        animation_link_max_vertical_distance: 1.0,
    }
}

fn disconnected_platforms_document() -> NavigationMesh3d {
    NavigationMesh3d {
        vertices: vec![
            Vec3::new(-5.0, 0.0, -2.0),
            Vec3::new(-5.0, 0.0, 2.0),
            Vec3::new(-1.0, 0.0, 2.0),
            Vec3::new(-1.0, 0.0, -2.0),
            Vec3::new(1.0, 0.0, -2.0),
            Vec3::new(1.0, 0.0, 2.0),
            Vec3::new(5.0, 0.0, 2.0),
            Vec3::new(5.0, 0.0, -2.0),
        ],
        polygons: vec![vec![0, 3, 2, 1], vec![4, 7, 6, 5]],
        polygon_type_indices: vec![0, 0],
        height_mesh: None,
    }
}

fn area_cost_document() -> NavigationMesh3d {
    let coordinates = [-3.0_f32, -1.0, 1.0, 3.0];
    let mut vertices = Vec::with_capacity(16);
    for z in coordinates {
        for x in coordinates {
            vertices.push(Vec3::new(x, 0.0, z));
        }
    }
    let mut polygons = Vec::with_capacity(8);
    let mut polygon_type_indices = Vec::with_capacity(8);
    for z in 0..3 {
        for x in 0..3 {
            if x == 1 && z == 1 {
                continue;
            }
            let bottom_left = z * 4 + x;
            polygons.push(vec![
                bottom_left,
                bottom_left + 1,
                bottom_left + 5,
                bottom_left + 4,
            ]);
            polygon_type_indices.push(usize::from(z == 2));
        }
    }
    NavigationMesh3d {
        vertices,
        polygons,
        polygon_type_indices,
        height_mesh: None,
    }
}

#[derive(Debug, Error)]
pub enum NavigationError {
    #[error("Landmass rejected the navigation mesh: {0}")]
    InvalidMesh(String),
    #[error("cannot sample the path start: {0}")]
    StartSample(String),
    #[error("cannot sample the path end: {0}")]
    EndSample(String),
    #[error("Landmass path query failed: {0}")]
    Path(String),
    #[error("Landmass rejected an area cost: {0}")]
    AreaCost(String),
    #[error("winding normalization found an invalid polygon vertex index {0}")]
    InvalidPolygonVertex(usize),
    #[error("winding normalization found an invalid height triangle range for polygon {0}")]
    InvalidHeightTriangleRange(usize),
    #[error("winding normalization found an invalid height vertex for polygon {0}")]
    InvalidHeightVertex(usize),
    #[error("avoidance probe lost an agent")]
    MissingAgent,
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy_landmass::{HeightNavigationMesh3d, HeightPolygon};

    #[test]
    fn off_mesh_link_connects_two_disconnected_platforms() {
        let (without, with) = off_mesh_link_probe().unwrap();
        assert!(!without.found);
        assert!(with.found);
        assert_eq!(with.animation_link_count, 1);
    }

    #[test]
    fn expensive_upper_route_uses_lower_corridor() {
        let path = area_cost_probe().unwrap();
        assert!(path.found);
        assert!(
            path.waypoints_m.iter().any(|point| point[2] <= -0.99),
            "path should use the lower, inexpensive corridor: {:?}",
            path.waypoints_m
        );
        assert!(path.waypoints_m.iter().all(|point| point[2] <= 0.01));
    }

    #[test]
    fn diagnostic_normalization_flips_only_negative_winding() {
        let vertices = vec![
            Vec3::new(-1.0, 0.0, -1.0),
            Vec3::new(1.0, 0.0, -1.0),
            Vec3::new(1.0, 0.0, 1.0),
        ];
        let mut mesh = NavigationMesh3d {
            vertices: vertices.clone(),
            polygons: vec![vec![0, 2, 1]],
            polygon_type_indices: vec![0],
            height_mesh: Some(HeightNavigationMesh3d {
                polygons: vec![HeightPolygon {
                    base_vertex_index: 0,
                    vertex_count: 3,
                    base_triangle_index: 0,
                    triangle_count: 1,
                }],
                vertices,
                triangles: vec![[0, 2, 1]],
            }),
        };

        let result = normalize_negative_winding(&mut mesh).unwrap();

        assert_eq!(result.normalized_polygon_count, 1);
        assert_eq!(result.normalized_height_triangle_count, 1);
        assert_eq!(mesh.polygons[0], vec![1, 2, 0]);
        assert_eq!(mesh.height_mesh.as_ref().unwrap().triangles[0], [0, 1, 2]);
        mesh.validate().unwrap();
    }
}
