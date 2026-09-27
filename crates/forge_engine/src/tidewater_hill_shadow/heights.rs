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
    Ok(bytes)
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
