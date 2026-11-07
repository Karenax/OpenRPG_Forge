// SPDX-License-Identifier: MIT
// Adapted from Tidewater src/fx/Spray.js, copyright 2026 DRG Software Solutions LLC.
// Revision 1438b1abfcaee3267092b75573014f4d9b4a983c; see third_party/tidewater/LICENSE.
//! Source-equivalent procedural sprite base textures; mip generation is a GPU stage.

pub const PUFF_SIDE: usize = 64;
pub const DOTS_SIDE: usize = 128;

/// Admit captured mip chains only when their declared hashes and generated
/// base levels both agree. The caller owns the capture-specific hashes.
#[must_use]
pub fn captured_chains_match(
    puff: &[u8],
    dots: &[u8],
    expected: [forge_core::ContentHash; 2],
) -> bool {
    forge_core::bytes_sha256(puff) == expected[0]
        && forge_core::bytes_sha256(dots) == expected[1]
        && puff.get(..PUFF_SIDE * PUFF_SIDE * 4) == Some(puff_rgba8().as_slice())
        && dots.get(..DOTS_SIDE * DOTS_SIDE * 4) == Some(dots_rgba8().as_slice())
}

/// Generate the original tileable mist noise as RGBA8, without retaining an asset copy.
#[must_use]
#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "Noise coordinates and rounded channel values are finite and bounded."
)]
pub fn puff_rgba8() -> Vec<u8> {
    let mut data = vec![0; PUFF_SIDE * PUFF_SIDE * 4];
    for y in 0..PUFF_SIDE {
        for x in 0..PUFF_SIDE {
            let (mut sum, mut amplitude, mut normalization) = (0.0, 0.5, 0.0);
            for octave in [4_i32, 8, 16, 32] {
                let x = f64::from(u32::try_from(x).expect("fixed puff size")) / 64.0
                    * f64::from(octave);
                let y = f64::from(u32::try_from(y).expect("fixed puff size")) / 64.0
                    * f64::from(octave);
                sum += noise(x, y, octave) * amplitude;
                normalization += amplitude;
                amplitude *= 0.55;
            }
            let value = (((sum / normalization - 0.2) * 1.6).clamp(0.0, 1.0) * 255.0).round() as u8;
            let offset = (y * PUFF_SIDE + x) * 4;
            data[offset..offset + 4].copy_from_slice(&[value, value, value, 255]);
        }
    }
    data
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "Noise coordinates are nonnegative and bounded by the fixed octave."
)]
fn noise(x: f64, y: f64, octave: i32) -> f64 {
    let column = x.floor() as i32;
    let row = y.floor() as i32;
    let fraction_x = x - f64::from(column);
    let fraction_y = y - f64::from(row);
    let weight_x = fraction_x * fraction_x * (3.0 - 2.0 * fraction_x);
    let weight_y = fraction_y * fraction_y * (3.0 - 2.0 * fraction_y);
    let random = |column: i32, row: i32| {
        let sample = (f64::from(column % octave) * 127.1
            + f64::from(row % octave) * 311.7
            + f64::from(octave) * 17.3)
            .sin()
            * 43758.5453;
        sample - sample.floor()
    };
    let (top_left, top_right, bottom_left, bottom_right) = (
        random(column, row),
        random(column + 1, row),
        random(column, row + 1),
        random(column + 1, row + 1),
    );
    (top_left * (1.0 - weight_x) + top_right * weight_x) * (1.0 - weight_y)
        + (bottom_left * (1.0 - weight_x) + bottom_right * weight_x) * weight_y
}

/// Generate the original tileable scattered-drop coverage as RGBA8.
#[must_use]
#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "The fixed texture, radii and rounded coverage values are bounded."
)]
pub fn dots_rgba8() -> Vec<u8> {
    let side = i32::try_from(DOTS_SIDE).expect("fixed dots size");
    let mut seed = 12_345_u32;
    let mut random = || {
        seed = seed.wrapping_mul(1_664_525).wrapping_add(1_013_904_223);
        f64::from(seed) / 4_294_967_296.0
    };
    let mut coverage = vec![0.0_f32; DOTS_SIDE * DOTS_SIDE];
    for _ in 0..150 {
        let cx = random() * f64::from(side);
        let cy = random() * f64::from(side);
        let radius = (0.9 * (1.0 - random() * 0.97).powf(-0.55)).min(6.0);
        let bound = (radius + 1.5).ceil() as i32;
        for dy in -bound..=bound {
            for dx in -bound..=bound {
                let ix = cx.floor() as i32 + dx;
                let iy = cy.floor() as i32 + dy;
                let x = ix.rem_euclid(side);
                let y = iy.rem_euclid(side);
                let distance = (f64::from(ix) + 0.5 - cx).hypot(f64::from(iy) + 0.5 - cy);
                let value = (radius + 0.5 - distance).clamp(0.0, 1.0);
                let index = usize::try_from(y * side + x).expect("wrapped texture coordinate");
                coverage[index] = f64::from(coverage[index]).max(value) as f32;
            }
        }
    }
    let mut data = vec![0; DOTS_SIDE * DOTS_SIDE * 4];
    for (index, value) in coverage.into_iter().enumerate() {
        let value = (f64::from(value) * 255.0).round() as u8;
        data[index * 4..index * 4 + 4].copy_from_slice(&[value, value, value, 255]);
    }
    data
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn source_sprite_base_textures_match_the_pinned_acquisition() {
        let puff = puff_rgba8();
        let dots = dots_rgba8();
        assert_eq!(puff.len(), PUFF_SIDE * PUFF_SIDE * 4);
        assert_eq!(dots.len(), DOTS_SIDE * DOTS_SIDE * 4);
        assert_eq!(
            forge_core::bytes_sha256(&puff).to_string(),
            "ac6716a07c89582263b400f75583b048fc7c4116aae0882cdff143f2a0e78613"
        );
        assert_eq!(
            forge_core::bytes_sha256(&dots).to_string(),
            "d3ece3fdfdb24926c769fc2505894d709aad99a54ddd58940f889b4b74a52383"
        );
    }

    #[test]
    fn captured_chains_require_the_pins_and_the_generated_base() {
        let mut puff = puff_rgba8();
        let dots = dots_rgba8();
        puff.extend_from_slice(&[1, 2, 3, 4]);
        let pins = [
            forge_core::bytes_sha256(&puff),
            forge_core::bytes_sha256(&dots),
        ];
        assert!(captured_chains_match(&puff, &dots, pins));

        let last = puff.len() - 1;
        puff[last] ^= 1;
        assert!(!captured_chains_match(&puff, &dots, pins));
        let altered_pins = [forge_core::bytes_sha256(&puff), pins[1]];
        assert!(captured_chains_match(&puff, &dots, altered_pins));
        puff[0] ^= 1;
        let altered_pins = [forge_core::bytes_sha256(&puff), pins[1]];
        assert!(!captured_chains_match(&puff, &dots, altered_pins));
        let short = &puff[..8];
        let short_pins = [forge_core::bytes_sha256(short), pins[1]];
        assert!(!captured_chains_match(short, &dots, short_pins));
    }
}
