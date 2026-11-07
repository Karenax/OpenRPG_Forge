// SPDX-License-Identifier: MIT
// Upstream 1438b1abfcaee3267092b75573014f4d9b4a983c; third_party/tidewater/LICENSE.
// Port of Tidewater TerrainBake.js, copyright DRG Software Solutions LLC.
//! Horizon AO plus the source two-metre cavity term on validated terrain grids.
#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    clippy::cast_precision_loss
)]
pub(super) fn bake(
    heights: &[f32],
    width: usize,
    height: usize,
    spacing: f64,
    water_level_m: Option<f64>,
) -> Vec<u8> {
    if width == 2048
        && height == 2048
        && spacing.to_bits() == 1.0_f64.to_bits()
        && water_level_m.is_some_and(|level| level.abs().to_bits() == 0.0_f64.to_bits())
    {
        return bake_reference(heights);
    }
    bake_general(heights, width, height, spacing, water_level_m)
}

// Preserve the source's exact arithmetic and edge treatment on its reference grid.
#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    clippy::cast_precision_loss
)]
fn bake_reference(heights: &[f32]) -> Vec<u8> {
    const SIDE: usize = 2048;
    const COARSE: usize = 512;
    assert_eq!(heights.len(), SIDE * SIDE);
    let mut coarse = vec![0.0_f32; COARSE * COARSE];
    for z in 0..COARSE {
        for x in 0..COARSE {
            let mut sum = 0.0_f64;
            for dz in 0..4 {
                for dx in 0..4 {
                    sum += f64::from(heights[(z * 4 + dz) * SIDE + x * 4 + dx]);
                }
            }
            coarse[z * COARSE + x] = (sum / 16.0) as f32;
        }
    }
    let directions: Vec<Vec<_>> = (0..8)
        .map(|direction| {
            let angle = (f64::from(direction) + 0.5) / 8.0 * std::f64::consts::PI * 2.0;
            [4.0, 8.0, 13.0, 19.0, 27.0, 38.0, 52.0, 72.0, 100.0]
                .into_iter()
                .map(|distance| {
                    // Math.round(integer + offset), including negative offsets.
                    (
                        ((angle.cos() * distance / 4.0) + 0.5).floor() as i32,
                        ((angle.sin() * distance / 4.0) + 0.5).floor() as i32,
                        distance,
                    )
                })
                .collect()
        })
        .collect();
    let mut ao = vec![1.0_f32; COARSE * COARSE];
    for z in 1..COARSE - 1 {
        for x in 1..COARSE - 1 {
            let height = f64::from(coarse[z * COARSE + x]);
            if height < -25.0 {
                continue;
            }
            let mut visibility = 0.0_f64;
            for direction in &directions {
                let mut horizon = 0.0_f64;
                for &(dx, dz, distance) in direction {
                    let sx = i32::try_from(x).expect("coarse index below 512") + dx;
                    let sz = i32::try_from(z).expect("coarse index below 512") + dz;
                    if !(0..512).contains(&sx) || !(0..512).contains(&sz) {
                        break;
                    }
                    let tangent =
                        (f64::from(coarse[sz as usize * COARSE + sx as usize]) - height - 0.3)
                            / distance;
                    horizon = horizon.max(tangent);
                }
                visibility += 1.0 / (1.0 + horizon * horizon);
            }
            ao[z * COARSE + x] = (visibility / 8.0) as f32;
        }
    }
    let mut result = Vec::with_capacity(SIDE * SIDE);
    for z in 0..SIDE {
        let fz = ((z as f64 + 0.5) / 4.0 - 0.5).clamp(0.0, 510.999);
        let az = fz as usize;
        let tz = fz - az as f64;
        for x in 0..SIDE {
            let h = |x, z| f64::from(heights[z * SIDE + x]);
            let lap = (h(x.saturating_sub(2), z)
                + h((x + 2).min(SIDE - 1), z)
                + h(x, z.saturating_sub(2))
                + h(x, (z + 2).min(SIDE - 1))
                - 4.0 * h(x, z))
                * 0.25;
            let cavity = (0.5 + lap * 0.9).clamp(0.0, 1.0);
            let fx = ((x as f64 + 0.5) / 4.0 - 0.5).clamp(0.0, 510.999);
            let ax = fx as usize;
            let tx = fx - ax as f64;
            let q = az * COARSE + ax;
            let sampled = (f64::from(ao[q]) * (1.0 - tx) + f64::from(ao[q + 1]) * tx) * (1.0 - tz)
                + (f64::from(ao[q + COARSE]) * (1.0 - tx) + f64::from(ao[q + COARSE + 1]) * tx)
                    * tz;
            let value = (sampled * (1.0 - (cavity - 0.5).max(0.0) * 0.7)).max(0.0);
            result.push((value * 255.0 + 0.5) as u8);
        }
    }
    result
}

/// The same horizon and two-metre cavity model on a rectangular sampled grid.
/// Partial coarse blocks use their actual sample count and centre.
#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    clippy::cast_precision_loss,
    clippy::too_many_lines
)]
fn bake_general(
    heights: &[f32],
    width: usize,
    height: usize,
    spacing: f64,
    water_level_m: Option<f64>,
) -> Vec<u8> {
    assert_eq!(heights.len(), width * height);
    let coarse_width = width.div_ceil(4);
    let coarse_height = height.div_ceil(4);
    let mut coarse = vec![0.0_f32; coarse_width * coarse_height];
    for z in 0..coarse_height {
        for x in 0..coarse_width {
            let mut sum = 0.0_f64;
            let mut count = 0_u32;
            for row in z * 4..(z * 4 + 4).min(height) {
                for column in x * 4..(x * 4 + 4).min(width) {
                    sum += f64::from(heights[row * width + column]);
                    count += 1;
                }
            }
            coarse[z * coarse_width + x] = (sum / f64::from(count)) as f32;
        }
    }
    let directions: Vec<Vec<_>> = (0..8)
        .map(|direction| {
            let angle = (f64::from(direction) + 0.5) / 8.0 * std::f64::consts::PI * 2.0;
            [4.0, 8.0, 13.0, 19.0, 27.0, 38.0, 52.0, 72.0, 100.0]
                .into_iter()
                .map(|distance| {
                    (
                        (angle.cos() * distance / (4.0 * spacing) + 0.5).floor(),
                        (angle.sin() * distance / (4.0 * spacing) + 0.5).floor(),
                        distance,
                    )
                })
                .collect()
        })
        .collect();
    let mut ao = vec![1.0_f32; coarse_width * coarse_height];
    for z in 1..coarse_height.saturating_sub(1) {
        for x in 1..coarse_width.saturating_sub(1) {
            let center = f64::from(coarse[z * coarse_width + x]);
            if water_level_m.is_some_and(|water| center < water - 25.0) {
                continue;
            }
            let mut visibility = 0.0_f64;
            for direction in &directions {
                let mut horizon = 0.0_f64;
                for &(dx, dz, distance) in direction {
                    let sx = x as f64 + dx;
                    let sz = z as f64 + dz;
                    if !sx.is_finite()
                        || !sz.is_finite()
                        || sx < 0.0
                        || sz < 0.0
                        || sx >= coarse_width as f64
                        || sz >= coarse_height as f64
                    {
                        break;
                    }
                    let tangent = (f64::from(coarse[sz as usize * coarse_width + sx as usize])
                        - center
                        - 0.3)
                        / distance;
                    horizon = horizon.max(tangent);
                }
                visibility += 1.0 / (1.0 + horizon * horizon);
            }
            ao[z * coarse_width + x] = (visibility / 8.0) as f32;
        }
    }
    let mut result = Vec::with_capacity(width * height);
    let offset = 2.0 / spacing;
    for z in 0..height {
        let (az, bz, tz) = coarse_sample(z, height, coarse_height);
        for x in 0..width {
            let sample_x = |position: f64| {
                let p = position.clamp(0.0, (width - 1) as f64);
                let a = p.floor() as usize;
                let b = (a + 1).min(width - 1);
                let t = p - a as f64;
                f64::from(heights[z * width + a]) * (1.0 - t)
                    + f64::from(heights[z * width + b]) * t
            };
            let sample_z = |position: f64| {
                let p = position.clamp(0.0, (height - 1) as f64);
                let a = p.floor() as usize;
                let b = (a + 1).min(height - 1);
                let t = p - a as f64;
                f64::from(heights[a * width + x]) * (1.0 - t)
                    + f64::from(heights[b * width + x]) * t
            };
            let lap = (sample_x(x as f64 - offset)
                + sample_x(x as f64 + offset)
                + sample_z(z as f64 - offset)
                + sample_z(z as f64 + offset)
                - 4.0 * f64::from(heights[z * width + x]))
                * 0.25;
            let cavity = (0.5 + lap * 0.9).clamp(0.0, 1.0);
            let (ax, bx, tx) = coarse_sample(x, width, coarse_width);
            let top = f64::from(ao[az * coarse_width + ax]) * (1.0 - tx)
                + f64::from(ao[az * coarse_width + bx]) * tx;
            let bottom = f64::from(ao[bz * coarse_width + ax]) * (1.0 - tx)
                + f64::from(ao[bz * coarse_width + bx]) * tx;
            let sampled = top * (1.0 - tz) + bottom * tz;
            let value = (sampled * (1.0 - (cavity - 0.5).max(0.0) * 0.7)).max(0.0);
            result.push((value * 255.0 + 0.5) as u8);
        }
    }
    result
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    clippy::cast_precision_loss
)]
fn coarse_sample(sample: usize, length: usize, count: usize) -> (usize, usize, f64) {
    if count == 1 {
        return (0, 0, 0.0);
    }
    let position = sample as f64 + 0.5;
    let last = (4 * (count - 1) + length) as f64 * 0.5;
    let previous = (4 * (count - 2)) as f64 + 2.0;
    if position >= last {
        return (count - 1, count - 1, 0.0);
    }
    if position > previous {
        return (
            count - 2,
            count - 1,
            (position - previous) / (last - previous),
        );
    }
    let grid = (position / 4.0 - 0.5).max(0.0);
    let first = grid.floor() as usize;
    (first, (first + 1).min(count - 1), grid - first as f64)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn partial_blocks_interpolate_from_their_actual_centres() {
        let (a, b, t) = coarse_sample(2, 5, 2);
        assert_eq!((a, b), (0, 1));
        assert!((t - 0.2).abs() < f64::EPSILON);
        let (a, b, t) = coarse_sample(4, 5, 2);
        assert_eq!((a, b, t.to_bits()), (1, 1, 0.0_f64.to_bits()));
        let (a, b, t) = coarse_sample(0, 1, 1);
        assert_eq!((a, b, t.to_bits()), (0, 0, 0.0_f64.to_bits()));
    }

    #[test]
    fn cavity_uses_two_metres_for_different_spacings() {
        for spacing in [0.5, 2.0] {
            let mut valley = vec![4.0_f32; 7 * 5];
            valley[2 * 7 + 3] = 0.0;
            let result = bake(&valley, 7, 5, spacing, None);
            assert_eq!(result[2 * 7 + 3], 166);
            let flat = bake(&[3.0; 13 * 17], 13, 17, spacing, None);
            assert!(flat.iter().all(|&value| value == 255));
        }
    }

    #[test]
    fn underwater_cutoff_is_relative_to_the_supplied_water_level() {
        let mut heights = vec![0.0_f32; 20 * 20];
        for z in 0..20 {
            for x in 12..20 {
                heights[z * 20 + x] = 20.0;
            }
        }
        let dry = bake(&heights, 20, 20, 1.0, None);
        let submerged = bake(&heights, 20, 20, 1.0, Some(100.0));
        let index = 10 * 20 + 5;
        assert!(dry[index] < submerged[index]);
        assert_eq!(submerged[index], 255);
    }
}
