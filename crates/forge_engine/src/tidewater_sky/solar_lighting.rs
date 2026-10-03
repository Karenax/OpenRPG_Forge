// SPDX-License-Identifier: MIT
// Adapted from Tidewater App.applyAtmosphereReadback at 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
//! Source CPU lighting from a completed atmosphere readback, independent of a renderer.
#[derive(Clone, Debug, PartialEq)]
pub struct Readback {
    pub sky: [f64; 3],
    pub transmittance: [f64; 3],
    pub horizon: [f64; 3],
}
impl Readback {
    /// The original kernel writes exactly three vec4f values (48 bytes).
    /// Adapters with a padded GPU buffer must pass only this qualified payload.
    pub fn decode(bytes: &[u8]) -> Result<Self, String> {
        if bytes.len() != 48 {
            return Err("Atmosphere readback requires three vec4f values".into());
        }
        let values: [f64; 12] = std::array::from_fn(|i| {
            f64::from(f32::from_le_bytes(
                bytes[i * 4..i * 4 + 4].try_into().expect("checked length"),
            ))
        });
        if values.iter().any(|v| !v.is_finite() || *v < 0.0)
            || [3, 7, 11].iter().any(|i| values[*i] != 1.0)
        {
            return Err("Invalid atmosphere readback values".into());
        }
        Ok(Self {
            sky: [values[0], values[1], values[2]],
            transmittance: [values[4], values[5], values[6]],
            horizon: [values[8], values[9], values[10]],
        })
    }
}
#[derive(Clone, Debug, PartialEq)]
pub struct Lighting {
    pub sun_color: [f64; 3],
    pub sky_irradiance: [f64; 3],
    pub horizon_color: [f64; 3],
}
/// Use the real sun elevation and night value from the same solar frame.
/// This function does not publish a frame or acknowledge GPU completion.
pub fn evaluate(sun_y: f64, night: f64, readback: &Readback) -> Result<Lighting, String> {
    if !sun_y.is_finite()
        || !(-1.0..=1.0).contains(&sun_y)
        || !night.is_finite()
        || !(0.0..=1.0).contains(&night)
        || readback
            .sky
            .iter()
            .chain(readback.transmittance.iter())
            .chain(readback.horizon.iter())
            .any(|v| !v.is_finite() || *v < 0.0)
    {
        return Err("Invalid solar lighting input".into());
    }
    let fade = ((sun_y - (-0.03)) / (0.02 - (-0.03))).clamp(0.0, 1.0);
    let horizon_fade = fade * fade * (3.0 - 2.0 * fade);
    let sun_color = if sun_y > -0.07 {
        let strength = 11.0 * horizon_fade;
        readback.transmittance.map(|v| v * strength)
    } else {
        let strength = 0.12 * night;
        [0.6 * strength, 0.7 * strength, strength]
    };
    let night_ambient = 0.012 * night;
    let sky_irradiance = [
        readback.sky[0] + night_ambient * 0.6,
        readback.sky[1] + night_ambient * 0.7,
        readback.sky[2] + night_ambient,
    ];
    let result = Lighting {
        sun_color,
        sky_irradiance,
        horizon_color: readback.horizon,
    };
    if result
        .sun_color
        .iter()
        .chain(result.sky_irradiance.iter())
        .chain(result.horizon_color.iter())
        .any(|v| !v.is_finite() || *v > f64::from(f32::MAX))
    {
        return Err("Solar lighting cannot be represented in GPU uniforms".into());
    }
    Ok(result)
}
