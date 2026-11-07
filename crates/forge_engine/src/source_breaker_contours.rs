//! Bounded shoreline contours for authored terrain, independent of a map or asset path.
//!
//! The existing source stripe scan remains available for its original reference.
//! Separate contours must remain separate when their ribbons are constructed.
use std::collections::BTreeMap;

const MAX_SAMPLES: u64 = 2_000_000;
const MAX_CROSSINGS: usize = 65_536;
const MAX_STATIONS: usize = 4_096;

/// Sampling domain and station spacing for a bounded shoreline extraction.
#[derive(Clone, Copy, Debug)]
pub struct ContourConfig {
    pub min: [f64; 2],
    pub max: [f64; 2],
    pub cells: [u32; 2],
    pub water_level: f64,
    pub station_spacing: f64,
}

/// One disconnected shoreline; station normals point toward lower terrain.
#[derive(Debug, PartialEq)]
pub struct ShoreContour {
    pub stations: Vec<[f32; 4]>,
    /// Closed contours do not duplicate their first station at the end.
    pub closed: bool,
}

#[derive(Clone, Copy, Eq, PartialEq, Ord, PartialOrd)]
struct Edge(u8, u32, u32);

struct Node {
    point: [f64; 2],
    neighbours: Vec<usize>,
}

/// Extract all separate shorelines on a bounded grid.
///
/// The sampler must return finite world-space heights, including a small
/// neighbourhood of the domain boundary for outward-normal estimation.
pub fn shore_contours(
    config: ContourConfig,
    height_at: impl Fn(f64, f64) -> f64,
) -> Result<Vec<ShoreContour>, &'static str> {
    let (step, heights) = sample_grid(config, &height_at)?;
    let nodes = extract_nodes(config, step, &heights)?;
    materialize_contours(config, step, &height_at, &nodes)
}

fn sample_grid(
    config: ContourConfig,
    height_at: &impl Fn(f64, f64) -> f64,
) -> Result<([f64; 2], Vec<f64>), &'static str> {
    let width = config.max[0] - config.min[0];
    let depth = config.max[1] - config.min[1];
    if config
        .min
        .into_iter()
        .chain(config.max)
        .chain([config.water_level, config.station_spacing, width, depth])
        .any(|value| !value.is_finite())
        || width <= 0.0
        || depth <= 0.0
        || config.station_spacing <= 0.0
        || config.cells.contains(&0)
        || config.cells.iter().any(|&n| n > 4096)
        || (u64::from(config.cells[0]) + 1) * (u64::from(config.cells[1]) + 1) > MAX_SAMPLES
    {
        return Err("Invalid bounded shoreline domain");
    }
    let [nx, nz] = config.cells;
    let step = [width / f64::from(nx), depth / f64::from(nz)];
    if step.iter().any(|value| !value.is_finite() || *value <= 0.0)
        || (0..2).any(|axis| {
            config.min[axis] + step[axis] <= config.min[axis]
                || config.max[axis] - step[axis] >= config.max[axis]
        })
    {
        return Err("Invalid shoreline grid spacing");
    }
    let mut heights = Vec::with_capacity((nx as usize + 1) * (nz as usize + 1));
    for z in 0..=nz {
        for x in 0..=nx {
            let value = height_at(
                config.min[0] + f64::from(x) * step[0],
                config.min[1] + f64::from(z) * step[1],
            );
            if !value.is_finite() {
                return Err("Non-finite shoreline height");
            }
            heights.push(value);
        }
    }
    Ok((step, heights))
}

#[allow(
    clippy::too_many_lines,
    reason = "Keep the bounded crossing graph and saddle connectivity in one pass."
)]
fn extract_nodes(
    config: ContourConfig,
    step: [f64; 2],
    heights: &[f64],
) -> Result<Vec<Node>, &'static str> {
    let [nx, nz] = config.cells;
    let at = |x: u32, z: u32| heights[z as usize * (nx as usize + 1) + x as usize];
    let mut nodes = Vec::<Node>::new();
    let mut edges = BTreeMap::<Edge, usize>::new();
    for z in 0..nz {
        for x in 0..nx {
            let values = [at(x, z), at(x + 1, z), at(x + 1, z + 1), at(x, z + 1)];
            let land = values.map(|value| value >= config.water_level);
            let corners = [[x, z], [x + 1, z], [x + 1, z + 1], [x, z + 1]];
            let keys = [
                Edge(0, x, z),
                Edge(1, x + 1, z),
                Edge(0, x, z + 1),
                Edge(1, x, z),
            ];
            let mut crossings = [None; 4];
            for edge in 0..4 {
                let next = (edge + 1) % 4;
                if land[edge] == land[next] {
                    continue;
                }
                let index = if let Some(&index) = edges.get(&keys[edge]) {
                    index
                } else {
                    if nodes.len() >= MAX_CROSSINGS {
                        return Err("Shoreline grid is too complex");
                    }
                    let denominator = values[next] - values[edge];
                    let ratio = (config.water_level - values[edge]) / denominator;
                    if !denominator.is_finite()
                        || !ratio.is_finite()
                        || !(0.0..=1.0).contains(&ratio)
                    {
                        return Err("Invalid shoreline interpolation");
                    }
                    let mut point = [0.0; 2];
                    for axis in 0..2 {
                        let position = f64::from(corners[edge][axis])
                            + ratio
                                * (f64::from(corners[next][axis]) - f64::from(corners[edge][axis]));
                        point[axis] = config.min[axis] + position * step[axis];
                    }
                    if point.iter().any(|value| !value.is_finite()) {
                        return Err("Non-finite shoreline coordinate");
                    }
                    let index = nodes.len();
                    nodes.push(Node {
                        point,
                        neighbours: Vec::with_capacity(2),
                    });
                    edges.insert(keys[edge], index);
                    index
                };
                crossings[edge] = Some(index);
            }
            let count = crossings.iter().flatten().count();
            let pairs: &[(usize, usize)] = match count {
                0 => &[],
                2 => {
                    let first = crossings
                        .iter()
                        .position(Option::is_some)
                        .expect("two crossings");
                    let second = crossings
                        .iter()
                        .rposition(Option::is_some)
                        .expect("two crossings");
                    connect(
                        &mut nodes,
                        crossings[first].unwrap(),
                        crossings[second].unwrap(),
                    )?;
                    &[]
                }
                4 => {
                    let center = values.iter().map(|value| value / 4.0).sum::<f64>();
                    if !center.is_finite() {
                        return Err("Non-finite shoreline saddle");
                    }
                    if (center >= config.water_level) == land[0] {
                        &[(0, 1), (2, 3)]
                    } else {
                        &[(3, 0), (1, 2)]
                    }
                }
                _ => return Err("Invalid shoreline crossing count"),
            };
            for &(left, right) in pairs {
                connect(
                    &mut nodes,
                    crossings[left].unwrap(),
                    crossings[right].unwrap(),
                )?;
            }
        }
    }
    Ok(nodes)
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "The GPU station layout uses f32 and rejects non-finite converted values."
)]
fn materialize_contours(
    config: ContourConfig,
    step: [f64; 2],
    height_at: &impl Fn(f64, f64) -> f64,
    nodes: &[Node],
) -> Result<Vec<ShoreContour>, &'static str> {
    let mut visited = vec![false; nodes.len()];
    let mut contours = Vec::new();
    let mut station_count = 0;
    for endpoints in [true, false] {
        for start in 0..nodes.len() {
            if visited[start] || (nodes[start].neighbours.len() == 1) != endpoints {
                continue;
            }
            let (points, closed) = trace(nodes, &mut visited, start)?;
            let stations = resample(
                &points,
                closed,
                config.station_spacing,
                MAX_STATIONS - station_count,
            )?;
            if stations.is_empty() {
                continue;
            }
            let epsilon = step[0].min(step[1]) * 0.1;
            let mut result = Vec::with_capacity(stations.len());
            for point in stations {
                let samples = [
                    height_at(point[0] + epsilon, point[1]),
                    height_at(point[0] - epsilon, point[1]),
                    height_at(point[0], point[1] + epsilon),
                    height_at(point[0], point[1] - epsilon),
                ];
                if samples.iter().any(|value| !value.is_finite()) {
                    return Err("Non-finite shoreline normal sample");
                }
                let normal = [samples[1] - samples[0], samples[3] - samples[2]];
                let length = normal[0].hypot(normal[1]);
                if !length.is_finite() || length <= f64::EPSILON {
                    return Err("Ambiguous shoreline normal");
                }
                let values = [
                    point[0] as f32,
                    point[1] as f32,
                    (normal[0] / length) as f32,
                    (normal[1] / length) as f32,
                ];
                if values.iter().any(|value| !value.is_finite()) {
                    return Err("Non-finite shoreline station");
                }
                result.push(values);
            }
            station_count += result.len();
            contours.push(ShoreContour {
                stations: result,
                closed,
            });
        }
    }
    Ok(contours)
}

fn connect(nodes: &mut [Node], left: usize, right: usize) -> Result<(), &'static str> {
    if left == right || nodes[left].neighbours.len() >= 2 || nodes[right].neighbours.len() >= 2 {
        return Err("Invalid shoreline topology");
    }
    nodes[left].neighbours.push(right);
    nodes[right].neighbours.push(left);
    Ok(())
}

fn trace(
    nodes: &[Node],
    visited: &mut [bool],
    start: usize,
) -> Result<(Vec<[f64; 2]>, bool), &'static str> {
    let mut points = Vec::new();
    let mut previous = None;
    let mut current = start;
    loop {
        if visited[current] {
            return Err("Shoreline topology intersects itself");
        }
        visited[current] = true;
        points.push(nodes[current].point);
        let next = nodes[current]
            .neighbours
            .iter()
            .copied()
            .find(|candidate| Some(*candidate) != previous);
        let Some(next) = next else {
            return Ok((points, false));
        };
        if next == start {
            return Ok((points, true));
        }
        previous = Some(current);
        current = next;
    }
}

fn resample(
    points: &[[f64; 2]],
    closed: bool,
    spacing: f64,
    limit: usize,
) -> Result<Vec<[f64; 2]>, &'static str> {
    let ends = if closed {
        crate::source_breaker_geometry::ShorelineEnds::Closed
    } else {
        crate::source_breaker_geometry::ShorelineEnds::Open { include_end: true }
    };
    Ok(
        crate::source_breaker_geometry::resample_shoreline(points, spacing, limit, ends, false)?
            .into_iter()
            .map(|point| point.position)
            .collect(),
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    /// Diagnostic only: compare real generic downhill and segment normals
    /// against the recorded source beach before changing production output.
    #[test]
    #[ignore = "Requires the explicitly pinned source height raster and breaker stations"]
    #[allow(
        clippy::too_many_lines,
        clippy::cast_precision_loss,
        clippy::cast_possible_truncation,
        reason = "Compare the bounded recorded raster and both current station directions."
    )]
    fn source_tangents_and_downhill_normals_on_pinned_terrain() {
        use forge_project::sampled_terrain::{
            HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource,
        };
        use forge_world::sampled_terrain::SampledTerrain;
        use std::{fs, path::PathBuf};

        let shore = PathBuf::from(std::env::var_os("FORGE_SHORE_PACKAGE").unwrap());
        let source = PathBuf::from(std::env::var_os("FORGE_BREAKER_SOURCE_DIR").unwrap());
        assert!(shore.is_absolute() && source.is_absolute());
        let heights = fs::read(shore.join("heights.f32.bin")).unwrap();
        assert_eq!(
            forge_core::bytes_sha256(&heights).to_string(),
            "be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3"
        );
        let terrain = SampledTerrain::from_le_bytes(
            SampledTerrainSource {
                schema: SAMPLED_TERRAIN_SCHEMA.into(),
                schema_version: 1,
                width: 2048,
                height: 2048,
                origin_xz_m: [-1024.0; 2],
                sample_spacing_m: 1.0,
                outside_height_m: -90.0,
                encoding: HeightEncoding::Float32Le,
                samples_sha256: forge_core::bytes_sha256(&heights),
            },
            &heights,
        )
        .unwrap();
        let reference_bytes = fs::read(source.join("stations.f32.bin")).unwrap();
        assert_eq!(
            forge_core::bytes_sha256(&reference_bytes).to_string(),
            "0c8d25b4f743529a97a9264daafc17559184112025087d170a1da4a3fa99fe2b"
        );
        let (chunks, remainder) = reference_bytes.as_chunks::<16>();
        assert!(remainder.is_empty());
        let reference: Vec<[f32; 4]> = chunks
            .iter()
            .map(|chunk| {
                std::array::from_fn(|i| {
                    f32::from_le_bytes(chunk[i * 4..i * 4 + 4].try_into().unwrap())
                })
            })
            .collect();
        assert_eq!(reference.len(), 577);
        let domain = ContourConfig {
            min: [-296.0, -389.0],
            max: [312.0, 219.0],
            cells: [608, 608],
            water_level: 0.0,
            station_spacing: 0.6,
        };
        let sample = |x, z| terrain.height_at(x, z).unwrap();
        let baseline = shore_contours(domain, sample).unwrap();
        let baseline: Vec<_> = baseline.iter().flat_map(|c| c.stations.iter()).collect();
        let (step, grid) = sample_grid(domain, &sample).unwrap();
        let nodes = extract_nodes(domain, step, &grid).unwrap();
        let mut visited = vec![false; nodes.len()];
        let mut points = Vec::new();
        let mut ambiguous = 0;
        let mut weak_alignment = 0;
        let mut gradient_turns = Vec::new();
        let mut tangent_turns = Vec::new();
        let mut displacements = Vec::new();
        let mut closed_count = 0;
        let mut closed_area_halved = 0;
        for endpoints in [true, false] {
            for start in 0..nodes.len() {
                if visited[start] || (nodes[start].neighbours.len() == 1) != endpoints {
                    continue;
                }
                let (path, closed) = trace(&nodes, &mut visited, start).unwrap();
                let mut smoothed = path.clone();
                crate::source_breaker_geometry::smooth_shoreline_positions(
                    &mut smoothed,
                    30,
                    closed,
                    [true, true],
                );
                displacements.extend(
                    path.iter()
                        .zip(&smoothed)
                        .map(|(a, b)| (a[0] - b[0]).hypot(a[1] - b[1])),
                );
                if closed {
                    closed_count += 1;
                    let area = |vertices: &[[f64; 2]]| {
                        (0..vertices.len())
                            .map(|i| {
                                let a = vertices[i];
                                let b = vertices[(i + 1) % vertices.len()];
                                a[0] * b[1] - a[1] * b[0]
                            })
                            .sum::<f64>()
                            .abs()
                    };
                    let before = area(&path);
                    if before > 1.0e-9 && area(&smoothed) < before * 0.5 {
                        closed_area_halved += 1;
                    }
                }
                let ends = if closed {
                    crate::source_breaker_geometry::ShorelineEnds::Closed
                } else {
                    crate::source_breaker_geometry::ShorelineEnds::Open { include_end: true }
                };
                let stations = crate::source_breaker_geometry::resample_shoreline(
                    &path,
                    domain.station_spacing,
                    MAX_STATIONS - points.len(),
                    ends,
                    false,
                )
                .unwrap();
                let first = points.len();
                for station in stations {
                    let previous = baseline[points.len()];
                    assert_eq!(
                        station.position.map(|value| (value as f32).to_bits()),
                        [previous[0].to_bits(), previous[1].to_bits()]
                    );
                    let gradient = [f64::from(previous[2]), f64::from(previous[3])];
                    let dot = station.normal[0] * gradient[0] + station.normal[1] * gradient[1];
                    if dot.abs() <= 0.01 {
                        ambiguous += 1;
                    }
                    if dot.abs() <= 0.2 {
                        weak_alignment += 1;
                    }
                    let sign = if dot < 0.0 { -1.0 } else { 1.0 };
                    points.push((station.position, gradient, station.normal.map(|v| v * sign)));
                }
                for pair in points[first..].windows(2) {
                    let angle = |a: [f64; 2], b: [f64; 2]| {
                        (a[0] * b[0] + a[1] * b[1])
                            .clamp(-1.0, 1.0)
                            .acos()
                            .to_degrees()
                    };
                    gradient_turns.push(angle(pair[0].1, pair[1].1));
                    tangent_turns.push(angle(pair[0].2, pair[1].2));
                }
            }
        }
        assert_eq!(points.len(), baseline.len());
        assert!(!points.is_empty());
        let mut gradient_angles = Vec::new();
        let mut tangent_angles = Vec::new();
        for station in &reference {
            let nearest = points
                .iter()
                .min_by(|a, b| {
                    let distance = |point: &&([f64; 2], [f64; 2], [f64; 2])| {
                        (point.0[0] - f64::from(station[0]))
                            .hypot(point.0[1] - f64::from(station[1]))
                    };
                    distance(a).total_cmp(&distance(b))
                })
                .unwrap();
            let angle = |normal: [f64; 2]| {
                (f64::from(station[2]) * normal[0] + f64::from(station[3]) * normal[1])
                    .clamp(-1.0, 1.0)
                    .acos()
                    .to_degrees()
            };
            gradient_angles.push(angle(nearest.1));
            tangent_angles.push(angle(nearest.2));
        }
        gradient_angles.sort_by(f64::total_cmp);
        tangent_angles.sort_by(f64::total_cmp);
        gradient_turns.sort_by(f64::total_cmp);
        tangent_turns.sort_by(f64::total_cmp);
        displacements.sort_by(f64::total_cmp);
        let percentile = (gradient_angles.len() - 1) * 95 / 100;
        let turn_percentile = (gradient_turns.len() - 1) * 95 / 100;
        let displacement_percentile = (displacements.len() - 1) * 95 / 100;
        println!(
            "SHORE_TANGENT_DIAGNOSTIC stations={} compared={} ambiguous={} weak_alignment={} gradient_p95_deg={} tangent_p95_deg={} gradient_turn_p95_deg={} tangent_turn_p95_deg={} closed={} closed_area_halved={} displacement_p95_m={} displacement_max_m={}",
            points.len(),
            reference.len(),
            ambiguous,
            weak_alignment,
            gradient_angles[percentile],
            tangent_angles[percentile],
            gradient_turns[turn_percentile],
            tangent_turns[turn_percentile],
            closed_count,
            closed_area_halved,
            displacements[displacement_percentile],
            displacements.last().unwrap()
        );
    }

    fn config(min: [f64; 2], max: [f64; 2], cells: [u32; 2]) -> ContourConfig {
        ContourConfig {
            min,
            max,
            cells,
            water_level: 0.0,
            station_spacing: 0.4,
        }
    }

    #[test]
    fn open_shore_tracks_full_domain_and_points_toward_water() {
        let curves =
            shore_contours(config([0.0, 0.0], [10.0, 10.0], [10, 10]), |x, _| x - 5.0).unwrap();
        assert_eq!(curves.len(), 1);
        assert!(!curves[0].closed);
        for station in &curves[0].stations {
            assert!((station[0] - 5.0).abs() < 1e-4);
            assert!((station[2] + 1.0).abs() < 1e-4 && station[3].abs() < 1e-4);
        }
        let z = curves[0].stations.iter().map(|s| s[1]).collect::<Vec<_>>();
        assert!(z.iter().any(|v| v.abs() < 1e-4));
        assert!(z.iter().any(|v| (v - 10.0).abs() < 1e-4));
    }

    #[test]
    fn island_is_closed_with_outward_normals() {
        let curves = shore_contours(config([-5.0, -5.0], [5.0, 5.0], [50, 50]), |x, z| {
            4.0 - x * x - z * z
        })
        .unwrap();
        assert_eq!(curves.len(), 1);
        assert!(curves[0].closed);
        assert!(curves[0].stations.len() > 10);
        for s in &curves[0].stations {
            let radius = s[0].hypot(s[1]);
            assert!((radius - 2.0).abs() < 0.02);
            assert!((s[0] * s[2] + s[1] * s[3]) / radius > 0.99);
        }
        assert_ne!(curves[0].stations.first(), curves[0].stations.last());
    }

    #[test]
    fn separate_islands_remain_separate() {
        let curves = shore_contours(config([-8.0, -4.0], [8.0, 4.0], [80, 40]), |x, z| {
            (2.25 - (x + 4.0).powi(2) - z * z).max(2.25 - (x - 4.0).powi(2) - z * z)
        })
        .unwrap();
        assert_eq!(curves.len(), 2);
        assert!(curves.iter().all(|c| c.closed && c.stations.len() > 10));
        assert!(curves.iter().all(
            |c| c.stations.iter().all(|s| s[0] < 0.0) || c.stations.iter().all(|s| s[0] > 0.0)
        ));
    }

    #[test]
    fn ambiguous_saddle_keeps_two_disconnected_paths() {
        let curves =
            shore_contours(config([-2.0, -2.0], [2.0, 2.0], [1, 1]), |x, z| x * z).unwrap();
        assert_eq!(curves.len(), 2);
        assert!(curves.iter().all(|c| !c.closed));
    }

    #[test]
    fn refuses_nonfinite_and_unbounded_inputs() {
        assert!(
            shore_contours(config([0.0, 0.0], [10.0, 10.0], [10, 10]), |_, _| f64::NAN).is_err()
        );
        assert!(shore_contours(config([0.0, 0.0], [10.0, 10.0], [4096, 4096]), |x, _| x).is_err());
        let mut fine = config([0.0, 0.0], [10.0, 10.0], [10, 10]);
        fine.station_spacing = 0.00001;
        assert!(shore_contours(fine, |x, _| x - 5.0).is_err());
    }
}
