// SPDX-License-Identifier: MIT
// Upstream 1438b1abfcaee3267092b75573014f4d9b4a983c; third_party/tidewater/LICENSE.
// Port of Tidewater TerrainBake.js, copyright DRG Software Solutions LLC.
//! Horizon AO plus the source two-metre cavity term, for a validated 2048² raster.
#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    clippy::cast_precision_loss
)]
pub(super) fn bake(heights: &[f32]) -> Vec<u8> {
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
