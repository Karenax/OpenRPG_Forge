// SPDX-License-Identifier: MIT
// Adapted from Tidewater src/ocean/Breakers.js and src/fx/Spray.js,
// copyright 2026 DRG Software Solutions LLC; revision 1438b1abfcaee3267092b75573014f4d9b4a983c.
// See third_party/tidewater/LICENSE.
//! Source breaker and body-free particle parameter blocks for a live water frame.

use bevy::prelude::UVec4;

/// Source unlit material defaults followed by the two effect-specific scalars.
#[must_use]
pub fn unlit_material_words(first: f32, second: f32) -> [UVec4; 3] {
    let one = 1.0_f32.to_bits();
    [
        UVec4::splat(one),
        UVec4::new(0, 0, 0, one),
        UVec4::new(0, 0, first.to_bits(), second.to_bits()),
    ]
}

/// Controls the existing source crest and emission programs.
#[derive(Clone, Copy, Debug)]
pub struct BreakerParams {
    pub camera: [f32; 3],
    pub spray: f32,
    pub sheet: f32,
    pub emit_range: f32,
    pub amplitude: f32,
    pub budget: f32,
}

impl Default for BreakerParams {
    fn default() -> Self {
        Self {
            camera: [0.0; 3],
            spray: 1.0,
            sheet: 1.0,
            emit_range: 240.0,
            amplitude: 1.0,
            budget: 0.3,
        }
    }
}

impl BreakerParams {
    /// Encode the source uniform block; non-finite or invalid controls are refused.
    pub fn encode(self) -> Result<[u8; 32], &'static str> {
        let values = [
            self.camera[0],
            self.camera[1],
            self.camera[2],
            self.spray,
            self.sheet,
            self.emit_range,
            self.amplitude,
            self.budget,
        ];
        if values.iter().any(|value| !value.is_finite())
            || self.spray < 0.0
            || self.sheet < 0.0
            || self.emit_range <= 0.0
            || self.amplitude < 0.0
            || !(0.0..=1.0).contains(&self.budget)
        {
            return Err("Invalid breaker parameters");
        }
        let mut bytes = [0; 32];
        for (index, value) in values.into_iter().enumerate() {
            bytes[index * 4..index * 4 + 4].copy_from_slice(&value.to_le_bytes());
        }
        Ok(bytes)
    }
}

/// Source spray parameters without a moving collision body or CPU emit requests.
/// The actual source identity matrices and hull defaults remain present.
#[must_use]
pub fn spray_without_body(frame_seed: u32) -> [u8; 240] {
    let mut bytes = [0; 240];
    bytes[..4].copy_from_slice(&frame_seed.to_le_bytes());
    for offset in [16, 36, 56, 76, 80, 100, 120, 140, 168, 180] {
        bytes[offset..offset + 4].copy_from_slice(&1.0_f32.to_le_bytes());
    }
    bytes[200..204].copy_from_slice(&(-1.0_f32).to_le_bytes());
    bytes
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn unlit_effect_defaults_match_the_acquired_source_uniforms() {
        let bytes = |values: [UVec4; 3]| -> Vec<u8> {
            values
                .iter()
                .flat_map(UVec4::to_array)
                .flat_map(u32::to_le_bytes)
                .collect()
        };
        assert_eq!(
            forge_core::bytes_sha256(&bytes(unlit_material_words(1.0, 0.0))).to_string(),
            "0b503372c3861e3c63c9abff106530fa745f9e75c42c1156b1614835df83a38a"
        );
        assert_eq!(
            forge_core::bytes_sha256(&bytes(unlit_material_words(1.0, 320.0))).to_string(),
            "8e63c6667a1473f0f29219232cba10a22386ca86636c31654bf4c57502e98af1"
        );
    }

    #[test]
    fn source_uniforms_encode_and_refuse_invalid_controls() {
        let params = BreakerParams {
            camera: [42.0, 14.0, -90.0],
            ..Default::default()
        };
        let bytes = params.encode().unwrap();
        let words: Vec<_> = bytes
            .as_chunks::<4>()
            .0
            .iter()
            .map(|v| f32::from_le_bytes(*v))
            .collect();
        assert_eq!(words, [42.0, 14.0, -90.0, 1.0, 1.0, 240.0, 1.0, 0.3]);
        assert!(
            BreakerParams {
                camera: [f32::NAN, 0.0, 0.0],
                ..params
            }
            .encode()
            .is_err()
        );
        assert!(
            BreakerParams {
                emit_range: 0.0,
                ..params
            }
            .encode()
            .is_err()
        );
        assert!(
            BreakerParams {
                budget: 1.1,
                ..params
            }
            .encode()
            .is_err()
        );
        let spray = spray_without_body(0x1234_5678);
        assert_eq!(&spray[..4], &0x1234_5678_u32.to_le_bytes());
        assert_eq!(spray.len(), 240);
    }
}
