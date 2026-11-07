// SPDX-License-Identifier: MIT
// Adapted from Tidewater Breakers.js, copyright 2026 DRG Software Solutions LLC.
// Revision 1438b1ab; see third_party/tidewater/LICENSE.
//! Shared derived shoreline stations and ribbon topology; terrain remains caller-owned.

pub const PROFILE_VERTICES: u32 = 20;

/// Bounds and source settings for the existing straight-axis shoreline scan.
/// Bounds are local to `origin_xz` and `rotation_radians`; the sampler receives
/// world coordinates, and stations and normals are returned in world coordinates.
#[derive(Clone, Copy, Debug)]
pub struct ShorelineScan {
    pub origin_xz: [f64; 2],
    pub rotation_radians: f64,
    pub x_start: f64,
    pub x_steps: u32,
    pub x_step: f64,
    pub z_start: f64,
    pub z_steps: u32,
    pub z_step: f64,
    pub water_probe_height: f64,
    pub shoreline_height: f64,
    pub maximum_depth_jump: f64,
    pub maximum_x_gap: f64,
    pub bisections: u32,
    pub smoothing_passes: u32,
    pub station_spacing: f64,
}

/// Apply the source's 1:2:1 position filter before stations are sampled.
/// Open paths retain their endpoints; closed paths wrap around. Callers choose
/// the coordinates to filter: the source's axial scan changes local z only.
/// Callers must bound the pass count and check that positions are finite.
pub(crate) fn smooth_shoreline_positions(
    points: &mut [[f64; 2]],
    passes: u32,
    closed: bool,
    components: [bool; 2],
) {
    if points.len() < 3 {
        return;
    }
    for _ in 0..passes {
        let mut smoothed = points.to_vec();
        let first = usize::from(!closed);
        let end = points.len() - usize::from(!closed);
        for k in first..end {
            let previous = (k + points.len() - 1) % points.len();
            let following = (k + 1) % points.len();
            for (axis, enabled) in components.into_iter().enumerate() {
                if enabled {
                    smoothed[k][axis] =
                        (points[previous][axis] + 2.0 * points[k][axis] + points[following][axis])
                            * 0.25;
                }
            }
        }
        points.copy_from_slice(&smoothed);
    }
}

/// Endpoint policy for the shared source-style station traversal.
#[derive(Clone, Copy)]
pub(crate) enum ShorelineEnds {
    Open { include_end: bool },
    Closed,
}

pub(crate) struct ShorelinePoint {
    pub position: [f64; 2],
    pub normal: [f64; 2],
}

/// Place stations along independent paths using the source's segment traversal.
/// Closed paths distribute the spacing around the loop without duplicating the
/// first station; open contours can retain their final endpoint explicitly.
#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    clippy::cast_precision_loss,
    clippy::too_many_lines,
    reason = "The finite nonnegative counts are bounded by the caller's station limit."
)]
pub(crate) fn resample_shoreline(
    points: &[[f64; 2]],
    spacing: f64,
    limit: usize,
    ends: ShorelineEnds,
    reject_degenerate: bool,
) -> Result<Vec<ShorelinePoint>, &'static str> {
    if !spacing.is_finite() || spacing <= 0.0 {
        return Err("Invalid shoreline spacing");
    }
    if points.len() < 2 {
        return Ok(Vec::new());
    }
    let closed = matches!(ends, ShorelineEnds::Closed);
    let count = points.len() - 1 + usize::from(closed);
    let mut segments = Vec::with_capacity(count);
    let mut total = 0.0;
    for index in 0..count {
        let a = points[index];
        let b = points[(index + 1) % points.len()];
        let length = (b[0] - a[0]).hypot(b[1] - a[1]);
        if !length.is_finite() || !(total + length).is_finite() {
            return Err("Invalid shoreline segment");
        }
        if length == 0.0 {
            if reject_degenerate {
                return Err("Invalid shoreline segment");
            }
            continue;
        }
        let start = total;
        total += length;
        segments.push((a, b, length, start, total));
    }
    if segments.is_empty() {
        return Ok(Vec::new());
    }
    let include_end = matches!(ends, ShorelineEnds::Open { include_end: true });
    let regular_target = if closed || include_end {
        let ratio = total / spacing;
        if !ratio.is_finite() || ratio.ceil() > limit as f64 {
            return Err("Shoreline exceeds station limit");
        }
        let target = if closed {
            (ratio.ceil() as usize).max(3)
        } else {
            ratio.ceil() as usize + 1
        };
        if target > limit {
            return Err("Shoreline exceeds station limit");
        }
        Some(target - usize::from(include_end))
    } else {
        None
    };
    let interval = if closed {
        total / regular_target.expect("closed paths have a bounded target") as f64
    } else {
        spacing
    };
    let mut output = Vec::new();
    let mut next = 0.0;
    for &(a, b, length, start, end) in &segments {
        while next <= end && regular_target.is_none_or(|target| output.len() < target) {
            if output.len() == limit {
                return Err("Shoreline exceeds station limit");
            }
            let t = (next - start) / length;
            output.push(ShorelinePoint {
                position: [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t],
                normal: [-(b[1] - a[1]) / length, (b[0] - a[0]) / length],
            });
            // Bounded contours use their station index to avoid cumulative
            // rounding stepping past the last valid segment. The source scan
            // retains its original repeated-addition sequence exactly.
            next = if regular_target.is_some() {
                (output.len() as f64 * interval).min(total)
            } else {
                next + interval
            };
        }
    }
    if include_end {
        let &(a, b, length, _, _) = segments.last().expect("nonempty segments");
        output.push(ShorelinePoint {
            position: b,
            normal: [-(b[1] - a[1]) / length, (b[0] - a[0]) / length],
        });
    }
    if regular_target.is_some_and(|target| output.len() != target + usize::from(include_end)) {
        return Err("Invalid shoreline station coverage");
    }
    Ok(output)
}

/// Rebuild a finite, bounded shoreline from the caller's terrain sampler.
/// Scanning moves from `z_start` toward decreasing local z, as in the source.
#[allow(
    clippy::too_many_lines,
    clippy::manual_midpoint,
    clippy::cast_possible_truncation,
    reason = "Preserve the source bisection and Float32Array output"
)]
pub fn shoreline_stations(
    scan: ShorelineScan,
    height_at: impl Fn(f64, f64) -> f64,
) -> Result<Vec<[f32; 4]>, &'static str> {
    let finite = [
        scan.origin_xz[0],
        scan.origin_xz[1],
        scan.rotation_radians,
        scan.x_start,
        scan.x_step,
        scan.z_start,
        scan.z_step,
        scan.water_probe_height,
        scan.shoreline_height,
        scan.maximum_depth_jump,
        scan.maximum_x_gap,
        scan.station_spacing,
    ]
    .into_iter()
    .all(f64::is_finite);
    if !finite
        || scan.x_step <= 0.0
        || scan.z_step <= 0.0
        || scan.station_spacing <= 0.0
        || scan.maximum_depth_jump <= 0.0
        || scan.maximum_x_gap <= 0.0
        || scan.x_steps > 4096
        || scan.z_steps == 0
        || scan.z_steps > 4096
        || u64::from(scan.x_steps + 1) * u64::from(scan.z_steps) > 2_000_000
        || scan.bisections > 32
        || scan.smoothing_passes > 64
        || !(scan.x_start + f64::from(scan.x_steps) * scan.x_step).is_finite()
        || !(scan.z_start - f64::from(scan.z_steps) * scan.z_step).is_finite()
    {
        return Err("Invalid bounded shoreline scan");
    }
    let identity = scan.origin_xz.map(f64::to_bits) == [0.0_f64.to_bits(); 2]
        && scan.rotation_radians.to_bits() == 0.0_f64.to_bits();
    let (sine, cosine) = scan.rotation_radians.sin_cos();
    let world_point = |x: f64, z: f64| {
        if identity {
            [x, z]
        } else {
            [
                scan.origin_xz[0] + cosine * x - sine * z,
                scan.origin_xz[1] + sine * x + cosine * z,
            ]
        }
    };
    let sample = |x, z| {
        let [world_x, world_z] = world_point(x, z);
        if !world_x.is_finite() || !world_z.is_finite() {
            return Err("Non-finite shoreline coordinate");
        }
        let height = height_at(world_x, world_z);
        if height.is_finite() {
            Ok(height)
        } else {
            Err("Non-finite shoreline height")
        }
    };
    let mut points = Vec::new();
    for step in 0..=scan.x_steps {
        let x = scan.x_start + f64::from(step) * scan.x_step;
        if sample(x, scan.z_start)? > scan.water_probe_height {
            continue;
        }
        for z_step in 0..scan.z_steps {
            let z = scan.z_start - f64::from(z_step) * scan.z_step;
            if sample(x, z)? > scan.shoreline_height {
                let (mut lo, mut hi) = (z, z + scan.z_step);
                for _ in 0..scan.bisections {
                    let mid = (lo + hi) * 0.5;
                    if sample(x, mid)? > scan.shoreline_height {
                        lo = mid;
                    } else {
                        hi = mid;
                    }
                }
                points.push([x, (lo + hi) * 0.5]);
                break;
            }
        }
    }
    let mut best = Vec::new();
    let mut run: Vec<[f64; 2]> = Vec::new();
    for point in points {
        if run.last().is_some_and(|last| {
            (point[1] - last[1]).abs() > scan.maximum_depth_jump
                || point[0] - last[0] > scan.maximum_x_gap
        }) {
            if run.len() > best.len() {
                best = std::mem::take(&mut run);
            }
            run.clear();
        }
        run.push(point);
    }
    if run.len() > best.len() {
        best = run;
    }
    smooth_shoreline_positions(&mut best, scan.smoothing_passes, false, [false, true]);
    let mut out = Vec::new();
    let stations = resample_shoreline(
        &best,
        scan.station_spacing,
        4096,
        ShorelineEnds::Open { include_end: false },
        true,
    )
    .map_err(|error| {
        if error == "Shoreline exceeds station limit" {
            "Shoreline exceeds ribbon station limit"
        } else {
            error
        }
    })?;
    for station in stations {
        let [world_x, world_z] = world_point(station.position[0], station.position[1]);
        let [normal_x, normal_z] = station.normal;
        let [world_normal_x, world_normal_z] = if identity {
            [normal_x, normal_z]
        } else {
            [
                cosine * normal_x - sine * normal_z,
                sine * normal_x + cosine * normal_z,
            ]
        };
        let station = [
            world_x as f32,
            world_z as f32,
            world_normal_x as f32,
            world_normal_z as f32,
        ];
        if station.iter().any(|value| !value.is_finite()) {
            return Err("Non-finite shoreline station");
        }
        out.push(station);
    }
    Ok(out)
}

pub struct LipTopology {
    /// (segment, wave parity slot, side, profile vertex), consumed by the GPU.
    pub sheet_ids: Vec<[f32; 4]>,
    pub indices: Vec<u32>,
}

/// A contiguous range within the flattened station buffer.
/// A closed contour includes its first station again as its final station.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct LipRange {
    pub start: u32,
    pub count: u32,
}

/// Packed stations and their independent ribbon ranges.
pub struct PackedContours {
    pub stations: Vec<[f32; 4]>,
    pub ranges: Vec<LipRange>,
}

/// Flatten contour stations without joining components; close loops explicitly.
pub fn pack_contours(
    contours: &[crate::source_breaker_contours::ShoreContour],
) -> Result<PackedContours, &'static str> {
    let mut stations = Vec::new();
    let mut ranges = Vec::new();
    for contour in contours {
        let minimum = if contour.closed { 3 } else { 2 };
        if contour.stations.len() < minimum
            || (contour.closed
                && contour.stations.first().map(|v| &v[..2])
                    == contour.stations.last().map(|v| &v[..2]))
            || contour.stations.iter().any(|station| {
                station.iter().any(|value| !value.is_finite())
                    || (station[2].hypot(station[3]) - 1.0).abs() > 0.001
            })
        {
            return Err("Invalid shoreline contour");
        }
        let count = contour
            .stations
            .len()
            .checked_add(usize::from(contour.closed))
            .ok_or("Shoreline exceeds station limit")?;
        let end = stations
            .len()
            .checked_add(count)
            .ok_or("Shoreline exceeds station limit")?;
        if end > 4096 {
            return Err("Shoreline exceeds station limit");
        }
        ranges.push(LipRange {
            start: u32::try_from(stations.len()).map_err(|_| "Shoreline station overflow")?,
            count: u32::try_from(count).map_err(|_| "Shoreline station overflow")?,
        });
        stations.extend_from_slice(&contour.stations);
        if contour.closed {
            stations.push(contour.stations[0]);
        }
    }
    Ok(PackedContours { stations, ranges })
}

/// Original two-slot ribbon. Bounded to avoid accidental unbounded allocations.
#[must_use]
pub fn lip_topology(stations: u32) -> Option<LipTopology> {
    lip_topology_ranges(&[LipRange {
        start: 0,
        count: stations,
    }])
}

/// Build independent ribbons over packed contour ranges without joining them.
#[must_use]
#[allow(clippy::cast_precision_loss)] // All converted integers are bounded below 4096.
pub fn lip_topology_ranges(ranges: &[LipRange]) -> Option<LipTopology> {
    if ranges.is_empty() {
        return None;
    }
    let mut end = 0_u32;
    for range in ranges {
        if range.start != end || range.count < 2 {
            return None;
        }
        end = end.checked_add(range.count)?;
        if end > 4096 {
            return None;
        }
    }
    let mut sheet_ids = Vec::new();
    let mut indices = Vec::new();
    for range in ranges {
        for segment in range.start..range.start + range.count - 1 {
            for slot in 0..2 {
                let first = u32::try_from(sheet_ids.len()).ok()?;
                for side in 0..2 {
                    for k in 0..PROFILE_VERTICES {
                        sheet_ids.push([segment as f32, slot as f32, side as f32, k as f32]);
                    }
                }
                for k in 0..PROFILE_VERTICES - 1 {
                    let a = first + k;
                    let b = a + 1;
                    let c = a + PROFILE_VERTICES;
                    let d = c + 1;
                    indices.extend([a, c, b, b, c, d]);
                }
            }
        }
    }
    Some(LipTopology { sheet_ids, indices })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn source_position_filter_preserves_open_ends_and_selected_axis() {
        let mut points = [[0.0, 0.0], [1.0, 2.0], [2.0, 0.0]];
        smooth_shoreline_positions(&mut points, 1, false, [false, true]);
        assert_eq!(points, [[0.0, 0.0], [1.0, 1.0], [2.0, 0.0]]);
    }

    #[test]
    fn position_filter_wraps_closed_paths_and_moves_positions() {
        let mut island = [[0.0, 0.0], [2.0, 0.0], [2.0, 2.0], [0.0, 2.0]];
        smooth_shoreline_positions(&mut island, 1, true, [true, true]);
        assert_eq!(island, [[0.5, 0.5], [1.5, 0.5], [1.5, 1.5], [0.5, 1.5]]);
    }

    #[test]
    fn shared_open_traversal_keeps_the_source_stations_and_optional_endpoint() {
        let path = [[0.0, 0.0], [1.0, 0.0], [1.0, 1.1]];
        let reference = resample_shoreline(
            &path,
            0.6,
            20,
            ShorelineEnds::Open { include_end: false },
            true,
        )
        .unwrap();
        let contour = resample_shoreline(
            &path,
            0.6,
            20,
            ShorelineEnds::Open { include_end: true },
            false,
        )
        .unwrap();
        assert_eq!(contour.len(), reference.len() + 1);
        for (left, right) in reference.iter().zip(&contour) {
            assert_eq!(
                left.position.map(f64::to_bits),
                right.position.map(f64::to_bits)
            );
            assert_eq!(
                left.normal.map(f64::to_bits),
                right.normal.map(f64::to_bits)
            );
        }
        assert_eq!(
            contour.last().unwrap().position.map(f64::to_bits),
            [1.0_f64, 1.1].map(f64::to_bits)
        );
    }

    #[test]
    fn shared_traversal_closes_loops_and_bounds_degenerate_paths() {
        let square = [[0.0, 0.0], [1.0, 0.0], [1.0, 1.0], [0.0, 1.0]];
        let stations = resample_shoreline(&square, 1.5, 10, ShorelineEnds::Closed, false).unwrap();
        assert_eq!(stations.len(), 3);
        assert_eq!(
            stations[0].position.map(f64::to_bits),
            [0.0_f64, 0.0].map(f64::to_bits)
        );
        assert_ne!(
            stations.last().unwrap().position.map(f64::to_bits),
            stations[0].position.map(f64::to_bits)
        );
        assert!(resample_shoreline(&square, 1.5, 2, ShorelineEnds::Closed, false).is_err());
        let repeated = [[0.0, 0.0], [0.0, 0.0], [1.0, 0.0]];
        assert!(
            resample_shoreline(
                &repeated,
                0.5,
                10,
                ShorelineEnds::Open { include_end: false },
                true,
            )
            .is_err()
        );
        assert_eq!(
            resample_shoreline(
                &repeated,
                0.5,
                10,
                ShorelineEnds::Open { include_end: true },
                false,
            )
            .unwrap()
            .len(),
            3
        );
    }

    #[test]
    fn bounded_open_traversal_survives_accumulated_spacing_roundoff() {
        // The path is just longer than 619 nominal intervals. Repeated f64
        // addition overshoots this endpoint by several ulps on this input.
        let spacing = 0.003_896_387_295_887_739_5;
        let path = [[0.0, 0.0], [2.411_863_736_154_511, 0.0]];
        let stations = resample_shoreline(
            &path,
            spacing,
            621,
            ShorelineEnds::Open { include_end: true },
            false,
        )
        .unwrap();
        assert_eq!(stations.len(), 621);
        assert_eq!(
            stations.last().unwrap().position.map(f64::to_bits),
            path[1].map(f64::to_bits)
        );
    }

    #[test]
    fn lip_ranges_keep_contours_separate_and_preserve_source_layout() {
        let single = lip_topology(3).unwrap();
        assert_eq!(single.sheet_ids.len(), 160);
        assert_eq!(single.indices.len(), 456);
        assert_eq!(
            single.sheet_ids[0].map(f32::to_bits),
            [0.0_f32.to_bits(); 4]
        );
        assert_eq!(&single.indices[..6], &[0, 20, 1, 1, 20, 21]);

        let combined = lip_topology_ranges(&[
            LipRange { start: 0, count: 3 },
            LipRange { start: 3, count: 3 },
        ])
        .unwrap();
        assert_eq!(combined.sheet_ids.len(), 320);
        assert_eq!(combined.indices.len(), 912);
        let segments: Vec<_> = combined
            .sheet_ids
            .as_chunks::<80>()
            .0
            .iter()
            .map(|vertices| vertices[0][0])
            .collect();
        assert_eq!(segments, [0.0, 1.0, 3.0, 4.0]);
        assert!(combined.indices.iter().all(|&index| index < 320));
        assert!(lip_topology_ranges(&[]).is_none());
        assert!(lip_topology_ranges(&[LipRange { start: 1, count: 2 }]).is_none());
        assert!(
            lip_topology_ranges(&[LipRange {
                start: 0,
                count: 4097
            }])
            .is_none()
        );
        assert!(
            lip_topology_ranges(&[
                LipRange { start: 0, count: 3 },
                LipRange { start: 2, count: 3 },
            ])
            .is_none()
        );
    }

    #[test]
    fn packed_closed_and_open_contours_keep_separate_ribbons() {
        use crate::source_breaker_contours::ShoreContour;
        let island = ShoreContour {
            stations: vec![
                [0.0, 0.0, 1.0, 0.0],
                [1.0, 0.0, 1.0, 0.0],
                [0.0, 1.0, 1.0, 0.0],
            ],
            closed: true,
        };
        let open = ShoreContour {
            stations: vec![[10.0, 0.0, 0.0, 1.0], [11.0, 0.0, 0.0, 1.0]],
            closed: false,
        };
        let packed = pack_contours(&[island, open]).unwrap();
        assert_eq!(
            packed.ranges,
            [
                LipRange { start: 0, count: 4 },
                LipRange { start: 4, count: 2 }
            ]
        );
        assert_eq!(
            packed.stations[0].map(f32::to_bits),
            packed.stations[3].map(f32::to_bits)
        );
        assert_ne!(
            packed.stations[3].map(f32::to_bits),
            packed.stations[4].map(f32::to_bits)
        );
        let topology = lip_topology_ranges(&packed.ranges).unwrap();
        let segments: Vec<_> = topology
            .sheet_ids
            .as_chunks::<80>()
            .0
            .iter()
            .map(|ids| ids[0][0])
            .collect();
        assert_eq!(segments, [0.0, 1.0, 2.0, 4.0]);
        assert!(
            pack_contours(&[ShoreContour {
                stations: vec![[0.0; 4]; 2],
                closed: false
            }])
            .is_err()
        );
    }

    fn other_zone_scan() -> ShorelineScan {
        ShorelineScan {
            origin_xz: [0.0, 0.0],
            rotation_radians: 0.0,
            x_start: 100.0,
            x_steps: 20,
            x_step: 0.5,
            z_start: 30.0,
            z_steps: 30,
            z_step: 0.5,
            water_probe_height: -0.3,
            shoreline_height: 0.0,
            maximum_depth_jump: 3.0,
            maximum_x_gap: 1.01,
            bisections: 16,
            smoothing_passes: 30,
            station_spacing: 0.6,
        }
    }

    #[test]
    fn shoreline_uses_other_zone_bounds_and_preserves_seaward_normals() {
        let stations = shoreline_stations(other_zone_scan(), |_, z| 25.0 - z).unwrap();
        assert_eq!(stations.len(), 17);
        assert!((stations[0][0] - 100.0).abs() < 0.00001);
        for station in &stations {
            assert!((station[1] - 25.0).abs() < 0.00001);
            assert!(station[2].abs() < 0.00001 && (station[3] - 1.0).abs() < 0.00001);
        }
        assert!(
            shoreline_stations(other_zone_scan(), |_, _| -1.0)
                .unwrap()
                .is_empty()
        );
        assert!(
            shoreline_stations(other_zone_scan(), |_, _| 1.0)
                .unwrap()
                .is_empty()
        );
    }

    #[test]
    fn shoreline_rotates_and_translates_sampler_positions_and_normals() {
        let scan = ShorelineScan {
            origin_xz: [100.0, 200.0],
            rotation_radians: std::f64::consts::FRAC_PI_2,
            x_start: 0.0,
            z_start: 5.0,
            ..other_zone_scan()
        };
        // Water is west of x=100 and land east; the original source scan
        // runs in local -z and is rotated onto this different coastline.
        let stations = shoreline_stations(scan, |world_x, _| world_x - 100.0).unwrap();
        assert_eq!(stations.len(), 17);
        assert!((stations[0][0] - 100.0).abs() < 0.00001);
        assert!((stations[0][1] - 200.0).abs() < 0.00001);
        for station in &stations {
            assert!((station[0] - 100.0).abs() < 0.00001);
            assert!((200.0..=210.0).contains(&station[1]));
            assert!((station[2] + 1.0).abs() < 0.00001 && station[3].abs() < 0.00001);
        }
    }

    #[test]
    fn shoreline_rejects_nonfinite_data_and_unbounded_sampling() {
        let scan = other_zone_scan();
        assert!(shoreline_stations(scan, |_, _| f64::NAN).is_err());
        assert!(
            shoreline_stations(
                ShorelineScan {
                    rotation_radians: f64::NAN,
                    ..scan
                },
                |_, _| 0.0
            )
            .is_err()
        );
        assert!(
            shoreline_stations(
                ShorelineScan {
                    station_spacing: 0.0,
                    ..scan
                },
                |_, _| 0.0
            )
            .is_err()
        );
        assert!(
            shoreline_stations(
                ShorelineScan {
                    x_steps: 4096,
                    z_steps: 4096,
                    ..scan
                },
                |_, _| 0.0
            )
            .is_err()
        );
        assert!(
            shoreline_stations(
                ShorelineScan {
                    station_spacing: 1e-9,
                    ..scan
                },
                |_, z| 25.0 - z
            )
            .is_err()
        );
    }

    #[test]
    fn ribbon_keeps_both_slots_and_original_triangle_order() {
        let mesh = lip_topology(3).unwrap();
        assert_eq!(mesh.sheet_ids.len(), 160);
        assert_eq!(mesh.indices.len(), 456);
        assert_eq!(
            mesh.sheet_ids[0].map(f32::to_bits),
            [0.0_f32, 0.0, 0.0, 0.0].map(f32::to_bits)
        );
        assert_eq!(
            mesh.sheet_ids[40].map(f32::to_bits),
            [0.0_f32, 1.0, 0.0, 0.0].map(f32::to_bits)
        );
        assert_eq!(
            mesh.sheet_ids[80].map(f32::to_bits),
            [1.0_f32, 0.0, 0.0, 0.0].map(f32::to_bits)
        );
        assert_eq!(&mesh.indices[..6], &[0, 20, 1, 1, 20, 21]);
        assert!(
            mesh.indices
                .iter()
                .all(|&index| (index as usize) < mesh.sheet_ids.len())
        );
    }

    #[test]
    fn ribbon_rejects_invalid_counts_and_accepts_the_declared_maximum() {
        assert!(lip_topology(0).is_none());
        assert!(lip_topology(1).is_none());
        assert!(lip_topology(4097).is_none());
        let mesh = lip_topology(4096).unwrap();
        assert_eq!(mesh.sheet_ids.len(), 4095 * 2 * 20 * 2);
        assert_eq!(mesh.indices.len(), 4095 * 2 * 19 * 6);
        assert!(
            mesh.indices
                .iter()
                .all(|&index| (index as usize) < mesh.sheet_ids.len())
        );
    }

    #[test]
    fn reference_station_count_reproduces_pinned_source_topology() {
        let mesh = lip_topology(577).unwrap();
        let ids: Vec<u8> = mesh
            .sheet_ids
            .iter()
            .flatten()
            .flat_map(|value| value.to_le_bytes())
            .collect();
        let indices: Vec<u8> = mesh
            .indices
            .iter()
            .flat_map(|value| value.to_le_bytes())
            .collect();
        assert_eq!(
            forge_core::bytes_sha256(&ids).to_string(),
            "72525103c084cb6537ed315b78056dcde80aa9635dc82043714871b6c0a547ec"
        );
        assert_eq!(
            forge_core::bytes_sha256(&indices).to_string(),
            "49ccc7aeaaba995db98935a2acf9f281801c5a9938c3fe675168fb538eeea59a"
        );
    }
}
