// SPDX-License-Identifier: MIT
// Adapted from Tidewater OceanFFT.js, copyright 2026 DRG Software Solutions LLC.
// Revision 1438b1ab; see third_party/tidewater/LICENSE.
//! Native parameters for the four-cascade Tessendorf/JONSWAP ocean kernels.
//! These are render settings, not a second canonical world document.
use serde::Deserialize;

pub const FFT_SIZE: u32 = 256;
const GRAVITY: f64 = 9.81;

#[derive(Clone, Debug, Deserialize)]
#[serde(default, rename_all = "camelCase", deny_unknown_fields)]
pub struct WaveSystem {
    pub scale: f64,
    pub wind_speed: f64,
    pub wind_direction: f64,
    pub fetch: f64,
    pub spread_blend: f64,
    pub swell: f64,
    pub peak_enhancement: f64,
    pub short_waves_fade: f64,
}
impl Default for WaveSystem {
    fn default() -> Self {
        Self {
            scale: 1.0,
            wind_speed: 8.0,
            wind_direction: 20.0,
            fetch: 200.0,
            spread_blend: 0.9,
            swell: 0.2,
            peak_enhancement: 3.3,
            short_waves_fade: 0.01,
        }
    }
}

#[derive(Clone, Debug, Deserialize)]
#[serde(default, rename_all = "camelCase", deny_unknown_fields)]
pub struct OceanSpectrum {
    pub cascades: usize,
    pub sizes: Vec<f64>,
    pub depth: f64,
    pub choppiness: f64,
    pub local: WaveSystem,
    pub swell: WaveSystem,
}
impl Default for OceanSpectrum {
    fn default() -> Self {
        Self {
            cascades: 4,
            sizes: vec![733.0, 157.0, 33.3, 7.1],
            depth: 500.0,
            choppiness: 0.9,
            local: WaveSystem {
                wind_speed: 7.0,
                wind_direction: 25.0,
                fetch: 120.0,
                spread_blend: 0.85,
                swell: 0.05,
                ..Default::default()
            },
            swell: WaveSystem {
                scale: 0.48,
                wind_speed: 6.0,
                wind_direction: 5.0,
                fetch: 1200.0,
                spread_blend: 1.0,
                swell: 0.9,
                short_waves_fade: 0.1,
                ..Default::default()
            },
        }
    }
}

#[derive(Clone, Copy, Debug, Deserialize)]
#[serde(default, rename_all = "camelCase", deny_unknown_fields)]
pub struct OceanFrame {
    pub time: f64,
    pub foam_bias: f64,
    pub foam_gain: f64,
    pub foam_decay: f64,
    pub foam_add: f64,
    pub seed: u32,
}
impl Default for OceanFrame {
    fn default() -> Self {
        Self {
            time: 0.0,
            foam_bias: 0.58,
            foam_gain: 3.0,
            foam_decay: 0.35,
            foam_add: 2.5,
            seed: 1337,
        }
    }
}

impl OceanSpectrum {
    /// Pack the original `OceanParams` layout. Arithmetic remains binary64 until
    /// the uniform write, matching JavaScript's Number -> `Float32Array` boundary.
    ///
    /// # Errors
    /// Rejects invalid cascade geometry, nonfinite or out-of-range controls,
    /// and values that cannot be represented by the GPU's f32 uniforms.
    #[allow(clippy::cast_possible_truncation)]
    pub fn uniform_bytes(&self, frame: OceanFrame) -> Result<[u8; 224], &'static str> {
        if !(1..=4).contains(&self.cascades) || self.sizes.len() < self.cascades {
            return Err("ocean cascade count or sizes invalid");
        }
        let sizes = &self.sizes[..self.cascades];
        if sizes.iter().any(|v| !v.is_finite() || *v <= 0.0)
            || sizes.windows(2).any(|v| v[0] <= v[1])
            || !self.depth.is_finite()
            || self.depth <= 0.0
        {
            return Err("ocean sizes must descend and depth must be positive");
        }
        let tail = [
            self.choppiness,
            frame.foam_bias,
            frame.foam_gain,
            frame.foam_decay,
            frame.foam_add,
            frame.time,
            self.depth,
        ];
        if tail.iter().any(|v| !v.is_finite() || *v < 0.0) {
            return Err("invalid ocean frame");
        }
        let mut words = [0.0_f64; 55];
        for i in 0..4 {
            words[i * 4] = sizes.get(i).copied().unwrap_or(1.0);
            // Unused cuts retain Vector4()'s default w=1 in the source.
            if i >= self.cascades {
                words[19 + i * 4] = 1.0;
            }
        }
        for i in 0..self.cascades {
            words[16 + i * 4] = if i == 0 {
                0.0001
            } else {
                std::f64::consts::TAU / sizes[i] * 6.0
            };
            words[17 + i * 4] = if i + 1 == self.cascades {
                9999.0
            } else {
                std::f64::consts::TAU / sizes[i + 1] * 6.0
            };
        }
        for (i, s) in [&self.local, &self.swell].into_iter().enumerate() {
            let values = [
                s.scale,
                s.wind_speed,
                s.wind_direction,
                s.fetch,
                s.spread_blend,
                s.swell,
                s.peak_enhancement,
                s.short_waves_fade,
            ];
            if values.iter().any(|v| !v.is_finite())
                || s.scale < 0.0
                || !(0.0..=1.0).contains(&s.spread_blend)
                || s.swell < 0.0
                || s.peak_enhancement <= 0.0
                || s.short_waves_fade < 0.0
            {
                return Err("invalid ocean wave system");
            }
            let fetch_m = s.fetch.max(1.0) * 1000.0;
            let wind = s.wind_speed.max(0.1);
            let alpha = 0.076 * (GRAVITY * fetch_m / (wind * wind)).powf(-0.22);
            let peak = 22.0 * (wind * fetch_m / (GRAVITY * GRAVITY)).powf(-0.33);
            words[32 + i * 4..36 + i * 4].copy_from_slice(&[
                s.scale,
                s.wind_direction * (std::f64::consts::PI / 180.0),
                s.spread_blend,
                s.swell,
            ]);
            words[40 + i * 4..44 + i * 4].copy_from_slice(&[
                alpha,
                peak,
                s.peak_enhancement,
                s.short_waves_fade,
            ]);
        }
        words[48..55].copy_from_slice(&tail);
        let mut bytes = [0_u8; 224];
        for (i, v) in words.into_iter().enumerate() {
            let f = v as f32;
            if !f.is_finite() {
                return Err("ocean uniform exceeds f32 range");
            }
            bytes[i * 4..i * 4 + 4].copy_from_slice(&f.to_le_bytes());
        }
        bytes[220..224].copy_from_slice(&frame.seed.to_le_bytes());
        Ok(bytes)
    }
}
