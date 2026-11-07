//! Source-compatible averaging, maximum mip reduction and half conversion.
#[allow(clippy::cast_possible_truncation)]
pub fn mip_chain(heights: &[f32]) -> Result<Vec<u8>, String> {
    if heights.len() != 2048 * 2048 || heights.iter().any(|height| !height.is_finite()) {
        return Err("Invalid heightfield for shadow mip chain".into());
    }
    let mut current = vec![0.0_f32; 512 * 512];
    for z in 0..512 {
        for x in 0..512 {
            let mut sum = 0.0_f64;
            for dz in 0..4 {
                for dx in 0..4 {
                    sum += f64::from(heights[(z * 4 + dz) * 2048 + x * 4 + dx]);
                }
            }
            current[z * 512 + x] = (sum / 16.0) as f32;
        }
    }
    Ok(encode_mips(current))
}

/// Project the admitted rectangular terrain into the square world domain used
/// by the existing hill-shadow kernel. Outside cells retain the source's
/// outside height; the 2048-square path keeps its exact historical averaging.
#[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
pub(super) fn for_plan(
    plan: &forge_world::sampled_terrain::SampledTerrainPlan,
) -> Result<(Vec<u8>, f32, f32), String> {
    let source = plan.raster().source();
    let document = plan.document();
    let size = document.extent_m.size_x_m.max(document.extent_m.size_z_m);
    let spacing = document.height_sampling.sample_spacing_m;
    let texel = size / 512.0;
    let scale = 1.0 / texel;
    if !size.is_normal()
        || size <= 0.0
        || !spacing.is_normal()
        || spacing <= 0.0
        || !texel.is_normal()
        || !scale.is_normal()
    {
        return Err("Heightfield domain exceeds shader precision".into());
    }
    let origin = -size * 0.5;
    if !origin.is_normal() {
        return Err("Heightfield origin exceeds shader precision".into());
    }
    if source.width == 2048 && source.height == 2048 {
        return Ok((mip_chain(&plan.cell_center_heights())?, origin, size));
    }
    let cell = f64::from(size) / 512.0;
    let origin64 = f64::from(origin);
    let mut coarse = Vec::with_capacity(512 * 512);
    if cell < source.sample_spacing_m {
        // Upsampling uses the canonical bilinear evaluator rather than making
        // a 512-square image of blocky nearest-neighbour samples.
        for z in 0..512 {
            for x in 0..512 {
                let wx = origin64 + (f64::from(x) + 0.5) * cell;
                let wz = origin64 + (f64::from(z) + 0.5) * cell;
                coarse.push(plan.height_at(wx, wz).map_err(|error| error.to_string())? as f32);
            }
        }
    } else {
        // Area-weighted averages prevent point sampling from dropping source
        // cells when the output texel spans multiple terrain samples.
        let weights = |axis: usize, count: u32| {
            (0..512)
                .map(|index| {
                    let lo = origin64 + f64::from(index) * cell;
                    let hi = lo + cell;
                    let first = ((lo - source.origin_xz_m[axis]) / source.sample_spacing_m)
                        .floor()
                        .clamp(0.0, f64::from(count)) as usize;
                    let last = ((hi - source.origin_xz_m[axis]) / source.sample_spacing_m)
                        .ceil()
                        .clamp(0.0, f64::from(count)) as usize;
                    (first..last)
                        .filter_map(|sample| {
                            let sample_coordinate = f64::from(
                                u32::try_from(sample)
                                    .expect("sample is bounded by the u32 raster dimensions"),
                            );
                            let start = source.origin_xz_m[axis]
                                + sample_coordinate * source.sample_spacing_m;
                            let overlap =
                                (hi.min(start + source.sample_spacing_m) - lo.max(start)).max(0.0);
                            (overlap > 0.0).then_some((sample, overlap / cell))
                        })
                        .collect::<Vec<_>>()
                })
                .collect::<Vec<_>>()
        };
        let xs = weights(0, source.width);
        let zs = weights(1, source.height);
        let samples = plan.cell_center_heights();
        let outside = f64::from(source.outside_height_m)
            + f64::from(document.height_sampling.base_elevation_m);
        for zw in &zs {
            for xw in &xs {
                let mut sum = 0.0;
                let mut covered = 0.0;
                for &(z, wz) in zw {
                    for &(x, wx) in xw {
                        let weight = wx * wz;
                        covered += weight;
                        sum += f64::from(samples[z * source.width as usize + x]) * weight;
                    }
                }
                coarse.push((sum + outside * (1.0 - covered.clamp(0.0, 1.0))) as f32);
            }
        }
    }
    if coarse.iter().any(|height| !height.is_finite()) {
        return Err("Nonfinite projected heightfield".into());
    }
    Ok((encode_mips(coarse), origin, size))
}

#[allow(clippy::cast_possible_truncation)]
fn encode_mips(mut current: Vec<f32>) -> Vec<u8> {
    let mut bytes = Vec::with_capacity(699_050);
    let mut width = 512;
    loop {
        for &value in &current {
            bytes.extend_from_slice(&source_half(value).to_le_bytes());
        }
        if width == 1 {
            break;
        }
        let n = width / 2;
        let mut next = vec![0.0; n * n];
        for z in 0..n {
            for x in 0..n {
                let i = z * 2 * width + x * 2;
                next[z * n + x] = current[i]
                    .max(current[i + 1])
                    .max(current[i + width])
                    .max(current[i + width + 1]);
            }
        }
        current = next;
        width = n;
    }
    bytes
}

// Source DataUtils uses truncation, not IEEE round-to-nearest.
#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    clippy::cast_possible_wrap
)]
fn source_half(value: f32) -> u16 {
    let bits = value.clamp(-65504.0, 65504.0).to_bits();
    let sign = (bits >> 16) & 0x8000;
    let mant = bits & 0x7f_ffff;
    let e = ((bits >> 23) & 0xff) as i32 - 127 + 15;
    let packed = if e >= 31 {
        sign | 0x7c00
    } else if e <= 0 {
        if e < -10 {
            sign
        } else {
            sign | ((mant | 0x80_0000) >> (14 - e))
        }
    } else {
        sign | ((e as u32) << 10) | (mant >> 13)
    };
    packed as u16
}
