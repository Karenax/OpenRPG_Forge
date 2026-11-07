// SPDX-License-Identifier: MIT
// Selection port derived from dgreenheck/tidewater 1438b1ab, Vegetation.js.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
//! Shared source-compatible selection over caller-admitted instance streams.
//! No map, filesystem, resource lookup or authored population mutation.

/// The reference walk's one-metre radial selection budget. Orientation and
/// sub-metre motion still update shader frame uniforms, without rebuilding lists.
/// Callers validate finite positions before using this cache decision.
#[must_use]
pub fn needs_refresh(previous: Option<[f32; 3]>, camera: [f32; 3]) -> bool {
    previous.is_none_or(|previous| {
        bevy::math::Vec3::from_array(previous)
            .distance_squared(bevy::math::Vec3::from_array(camera))
            >= 1.0
    })
}

#[derive(Clone, Copy)]
pub struct NearPolicy {
    pub query_radius_m: f64,
    pub cell_size_m: f64,
}

#[derive(Clone, Copy)]
pub struct FarPolicy {
    pub exclude_near: bool,
    pub near_range_m: f64,
    pub refresh_distance_m: f64,
    pub sort_far: bool,
}

/// Read the same immutable, validated population throughout this selection.
/// The consumer admits finite positions/radii and finite positive cell size.
#[allow(clippy::cast_possible_truncation)] // Source f32 distance ordering is intentional.
pub fn near_indices(
    count: usize,
    position: impl Fn(usize) -> [f32; 3],
    query_radius_squared: impl Fn(usize) -> f32,
    camera: [f32; 3],
    policy: NearPolicy,
) -> Vec<usize> {
    let x = f64::from(camera[0]);
    let z = f64::from(camera[2]);
    let r = policy.query_radius_m;
    let cs = policy.cell_size_m;
    let min_x = ((x - r) / cs).floor();
    let max_x = ((x + r) / cs).floor();
    let min_z = ((z - r) / cs).floor();
    let max_z = ((z + r) / cs).floor();
    let mut selected = Vec::new();
    for i in 0..count {
        let p = position(i);
        let px = f64::from(p[0]);
        let pz = f64::from(p[2]);
        let cell_x = (px / cs).floor();
        let cell_z = (pz / cs).floor();
        let radius_squared = query_radius_squared(i);
        let radius = if radius_squared > 0.0 {
            f64::from(radius_squared)
        } else {
            r * r
        };
        if cell_x >= min_x && cell_x <= max_x && cell_z >= min_z && cell_z <= max_z {
            let distance = (px - x).powi(2) + (pz - z).powi(2);
            if distance < radius {
                selected.push((i, cell_z, cell_x, distance as f32));
            }
        }
    }
    // The reference traverses cells (z, then x) before a stable f32 distance
    // sort. One stable lexicographic sort gives the same order, including ties.
    selected.sort_by(|a, b| {
        a.3.total_cmp(&b.3)
            .then_with(|| a.1.total_cmp(&b.1))
            .then_with(|| a.2.total_cmp(&b.2))
    });
    selected.into_iter().map(|(i, _, _, _)| i).collect()
}

/// Preserve source order, the vertical near exclusion, stable 6 m buckets and
/// the 1023 bucket clamp. Resource capacities are applied to the returned prefix.
pub fn far_indices(
    count: usize,
    position: impl Fn(usize) -> [f32; 3],
    camera: [f32; 3],
    policy: FarPolicy,
) -> Vec<usize> {
    let distance = |i: usize| {
        let p = position(i);
        let dx = f64::from(p[0]) - f64::from(camera[0]);
        let dy = f64::from(p[1]) - f64::from(camera[1]);
        let dz = f64::from(p[2]) - f64::from(camera[2]);
        (dx * dx + dz * dz, dy * dy)
    };
    let radius = (policy.near_range_m - policy.refresh_distance_m - 2.0).max(0.0);
    let mut ids: Vec<_> = if policy.exclude_near {
        (0..count)
            .filter(|&i| {
                let (horizontal, vertical) = distance(i);
                horizontal + vertical >= radius * radius
            })
            .collect()
    } else {
        (0..count).collect()
    };
    if policy.sort_far && !policy.exclude_near {
        let bucket = |i: usize| (distance(i).0.sqrt() / 6.0).floor().min(1023.0);
        ids.sort_by_cached_key(|&i| bucket(i).to_bits());
    }
    ids
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn negative_cells_strict_radius_and_per_record_envelopes_preserve_near_order() {
        let points = [
            [-2.0, 0.0, 0.0],
            [2.0, 0.0, 0.0],
            [4.0, 0.0, 0.0],
            [0.0, 0.0, -2.0],
            [0.0, 0.0, 2.0],
            [5.0, 0.0, 0.0],
        ];
        let radii = [0.0, 0.0, 0.0, 0.0, 0.0, 36.0];
        assert_eq!(
            near_indices(
                points.len(),
                |i| points[i],
                |i| radii[i],
                [0.0; 3],
                NearPolicy {
                    query_radius_m: 4.0,
                    cell_size_m: 4.0
                }
            ),
            [3, 0, 1, 4, 5]
        );
    }

    #[test]
    fn near_order_preserves_grid_and_source_order_when_f32_distances_tie() {
        let points = [
            [1.0, 0.0, 0.0001],
            [1.0, 0.0, -0.0001],
            [1.0, 0.0, 0.0],
            [1.0, 0.0, 0.0002],
        ];
        assert_eq!(
            near_indices(
                points.len(),
                |i| points[i],
                |_| 0.0,
                [0.0; 3],
                NearPolicy {
                    query_radius_m: 2.0,
                    cell_size_m: 10.0,
                },
            ),
            [1, 0, 2, 3]
        );
    }

    #[test]
    fn far_buckets_keep_source_ties_capacity_prefixes_and_moved_camera_order() {
        let points = [12.0, 6.0, 11.999, 0.0, -6.0, 6144.0, 1.0e30, 6140.0].map(|x| [x, 0.0, 0.0]);
        let policy = FarPolicy {
            exclude_near: false,
            near_range_m: 20.0,
            refresh_distance_m: 2.0,
            sort_far: true,
        };
        let ids = far_indices(points.len(), |i| points[i], [0.0; 3], policy);
        assert_eq!(ids, [3, 1, 2, 4, 0, 5, 6, 7]);
        assert_eq!(&ids[..4], [3, 1, 2, 4]);
        assert_eq!(
            far_indices(points.len(), |i| points[i], [1.0, 0.0, 0.0], policy),
            [1, 3, 0, 2, 4, 5, 6, 7]
        );
    }

    #[test]
    fn far_exclusion_uses_vertical_distance_without_resorting_source_order() {
        let points = [
            [0.0, 0.0, 0.0],
            [0.0, 30.0, 0.0],
            [25.0, 0.0, 0.0],
            [-25.0, 0.0, 0.0],
        ];
        assert_eq!(
            far_indices(
                points.len(),
                |i| points[i],
                [0.0; 3],
                FarPolicy {
                    exclude_near: true,
                    near_range_m: 20.0,
                    refresh_distance_m: 2.0,
                    sort_far: true
                }
            ),
            [1, 2, 3]
        );
    }
}
