// SPDX-License-Identifier: MIT
// Tidewater Frame.js lighting ABI at 1438b1ab. Copyright 2026 DRG Software
// Solutions LLC; see third_party/tidewater/LICENSE.
//! One validated lighting payload for the source cloud and water frame layouts.
use super::{solar, solar_lighting};

const OFFSETS: [usize; 13] = [
    592, 596, 600, 604, 608, 612, 616, 624, 628, 632, 640, 644, 648,
];

#[derive(Clone, Debug)]
pub struct FrameLighting {
    words: [u32; 13],
}

impl FrameLighting {
    /// Build from the current source key light and a completed atmosphere readback.
    /// This does not publish or qualify an environment cube or its diffuse SH.
    #[allow(clippy::cast_possible_truncation)] // Range and representability checked below.
    pub fn new(frame: solar::Frame, lighting: &solar_lighting::Lighting) -> Result<Self, String> {
        let values = [
            frame.key.x,
            frame.key.y,
            frame.key.z,
            frame.night,
            lighting.sun_color[0],
            lighting.sun_color[1],
            lighting.sun_color[2],
            lighting.sky_irradiance[0],
            lighting.sky_irradiance[1],
            lighting.sky_irradiance[2],
            lighting.horizon_color[0],
            lighting.horizon_color[1],
            lighting.horizon_color[2],
        ];
        if values
            .iter()
            .any(|v| !v.is_finite() || !(*v as f32).is_finite())
            || (frame.key.length_squared() - 1.0).abs() > 0.001
            || !(0.0..=1.0).contains(&frame.night)
            || values[4..].iter().any(|v| *v < 0.0)
        {
            return Err("Invalid source frame lighting".into());
        }
        Ok(Self {
            words: values.map(|v| (v as f32).to_bits()),
        })
    }

    /// Exact 720-byte Frame.js ABI. Wrong lengths leave the destination intact.
    pub fn write_bytes(&self, frame: &mut [u8]) -> Result<(), String> {
        if frame.len() != 720 {
            return Err("Source frame requires 720 bytes".into());
        }
        for (offset, word) in OFFSETS.into_iter().zip(self.words) {
            frame[offset..offset + 4].copy_from_slice(&word.to_le_bytes());
        }
        Ok(())
    }

    /// Same ABI for the water material's mixed integer/float words. Camera,
    /// time, sun angular size, immersion and water height remain unchanged.
    pub fn write_words(&self, frame: &mut [[u32; 4]; 45]) {
        for (offset, word) in OFFSETS.into_iter().zip(self.words) {
            frame[offset / 16][(offset % 16) / 4] = word;
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    fn input() -> (solar::Frame, solar_lighting::Lighting) {
        (
            *solar::Clock::new(Default::default()).unwrap().frame(),
            solar_lighting::Lighting {
                sun_color: [1.0, 2.0, 3.0],
                sky_irradiance: [0.1, 0.2, 0.3],
                horizon_color: [0.4, 0.5, 0.6],
            },
        )
    }
    #[test]
    fn cloud_bytes_and_water_words_match_and_preserve_every_other_field() {
        let (frame, lighting) = input();
        let payload = FrameLighting::new(frame, &lighting).unwrap();
        let mut bytes = vec![0x5a; 720];
        let mut words = [[0x5a5a5a5a; 4]; 45];
        payload.write_bytes(&mut bytes).unwrap();
        payload.write_words(&mut words);
        let packed: Vec<_> = words
            .into_iter()
            .flatten()
            .flat_map(u32::to_le_bytes)
            .collect();
        assert_eq!(bytes, packed);
        for (i, byte) in bytes.iter().enumerate() {
            if !OFFSETS
                .iter()
                .any(|offset| (*offset..*offset + 4).contains(&i))
            {
                assert_eq!(*byte, 0x5a);
            }
        }
        assert_eq!(
            &bytes[608..620],
            &lighting
                .sun_color
                .map(|v| (v as f32).to_le_bytes())
                .concat()
        );
    }
    #[test]
    fn invalid_payloads_and_wrong_lengths_are_refused_before_writing() {
        let (mut frame, mut lighting) = input();
        let payload = FrameLighting::new(frame, &lighting).unwrap();
        let mut short = vec![0x5a; 719];
        assert!(payload.write_bytes(&mut short).is_err());
        assert_eq!(short, vec![0x5a; 719]);
        lighting.sun_color[0] = f64::MAX;
        assert!(FrameLighting::new(frame, &lighting).is_err());
        lighting.sun_color[0] = -1.0;
        assert!(FrameLighting::new(frame, &lighting).is_err());
        lighting.sun_color[0] = 1.0;
        frame.key *= 2.0;
        assert!(FrameLighting::new(frame, &lighting).is_err());
    }
}
