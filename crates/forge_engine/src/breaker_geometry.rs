// SPDX-License-Identifier: MIT
// Adapted from Tidewater Breakers.js, copyright 2026 DRG Software Solutions LLC.
// Revision 1438b1ab; see third_party/tidewater/LICENSE.
//! Derived beach stations and lip topology, rebuilt from the caller's terrain.
//! These are render inputs, not another canonical world representation.

pub const STATION_SPACING: f64 = 0.6;
pub const PROFILE_VERTICES: u32 = 20;

/// Find the longest continuous main-beach shoreline and sample its smoothed
/// arc length. The caller supplies the terrain's finite, f64 bilinear sampler.
#[must_use]
#[allow(
    clippy::manual_midpoint,
    reason = "Preserve source arithmetic; bisection endpoints stay within -110..12.5."
)]
pub fn beach_stations(height_at: impl Fn(f64, f64) -> f64) -> Vec<[f32; 4]> {
    let mut points = Vec::new();
    for step in 0..=740 {
        let x = -175.0 + f64::from(step) * 0.5;
        if height_at(x, 12.0) > -0.3 {
            continue;
        }
        for z_step in 0..244 {
            let z = 12.0 - f64::from(z_step) * 0.5;
            if height_at(x, z) > 0.0 {
                let (mut lo, mut hi) = (z, z + 0.5);
                for _ in 0..16 {
                    let mid = (lo + hi) * 0.5;
                    if height_at(x, mid) > 0.0 {
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
        if run
            .last()
            .is_some_and(|last| (point[1] - last[1]).abs() > 3.0 || point[0] - last[0] > 1.01)
        {
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
    for _ in 0..30 {
        let mut smoothed = best.clone();
        for k in 1..best.len().saturating_sub(1) {
            smoothed[k][1] = (best[k - 1][1] + 2.0 * best[k][1] + best[k + 1][1]) * 0.25;
        }
        best = smoothed;
    }
    resample(&best)
}

#[allow(clippy::cast_possible_truncation)] // The source stores final stations in Float32Array.
fn resample(points: &[[f64; 2]]) -> Vec<[f32; 4]> {
    let mut out = Vec::new();
    let (mut accumulated, mut next) = (0.0, 0.0);
    for edge in points.windows(2) {
        let ([ax, az], [bx, bz]) = (edge[0], edge[1]);
        let segment = (bx - ax).hypot(bz - az);
        while next <= accumulated + segment {
            let t = (next - accumulated) / segment;
            out.push([
                (ax + (bx - ax) * t) as f32,
                (az + (bz - az) * t) as f32,
                (-(bz - az) / segment) as f32,
                ((bx - ax) / segment) as f32,
            ]);
            next += STATION_SPACING;
        }
        accumulated += segment;
    }
    out
}

pub struct LipTopology {
    /// (segment, wave parity slot, side, profile vertex), consumed by the GPU.
    pub sheet_ids: Vec<[f32; 4]>,
    pub indices: Vec<u32>,
}

/// Original two-slot ribbon. Bounded to avoid accidental unbounded allocations.
#[must_use]
#[allow(clippy::cast_precision_loss)] // All converted integers are bounded below 4096.
pub fn lip_topology(stations: u32) -> Option<LipTopology> {
    if !(2..=4096).contains(&stations) {
        return None;
    }
    let mut sheet_ids = Vec::new();
    let mut indices = Vec::new();
    for segment in 0..stations - 1 {
        for slot in 0..2 {
            let first = (segment * 2 + slot) * PROFILE_VERTICES * 2;
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
    Some(LipTopology { sheet_ids, indices })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn open_water_and_land_without_a_waterfront_have_no_stations() {
        assert!(beach_stations(|_, _| -10.0).is_empty());
        assert!(beach_stations(|_, _| 10.0).is_empty());
    }

    #[test]
    fn straight_beach_has_seaward_normals_and_equal_spacing() {
        let stations = beach_stations(|_, z| -z);
        assert!(stations.len() > 600);
        for s in &stations {
            assert!(s[1].abs() < 0.00001 && s[2].abs() < 0.00001 && (s[3] - 1.0).abs() < 0.00001);
        }
        for edge in stations.windows(2) {
            assert!((edge[1][0] - edge[0][0] - 0.6).abs() < 0.00002);
        }
    }
}
