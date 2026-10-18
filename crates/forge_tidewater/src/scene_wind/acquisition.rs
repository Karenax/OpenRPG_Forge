//! Actual uniforms acquired after source precompile and 120 submitted frames.
use super::WindFrame;
use crate::pendulum_motion::Wind;
use bevy::{math::DVec4, prelude::Resource};
use serde::Deserialize;

#[derive(Clone, Debug, Deserialize, Resource)]
pub struct AcquiredFrame {
    pub global_time_s: f64,
    pub global_dt_s: f64,
    pub ocean_time_s: f64,
    pub ocean_time_scale: f64,
    pub cloud_clock: [f64; 4],
    pub wind_speed: f64,
    pub wind_direction: [f64; 2],
    pub vegetation_gust_offset: [f64; 2],
    #[serde(skip)]
    pub global_frame: u32,
}

impl AcquiredFrame {
    /// Decode scene-supplied clocks. Source identity and provenance are validated
    /// by the importing content adapter, before these renderer inputs are staged.
    pub fn from_json(bytes: &[u8], global_frame: u32) -> Result<Self, String> {
        let mut frame: Self = serde_json::from_slice(bytes).map_err(|error| error.to_string())?;
        frame.global_frame = global_frame;
        frame.wind_frame()?;
        if !frame.global_dt_s.is_finite()
            || !(0.0..=0.1).contains(&frame.global_dt_s)
            || !frame.ocean_time_s.is_finite()
            || frame.ocean_time_s < 0.0
            || !frame.ocean_time_scale.is_finite()
            || frame.ocean_time_scale <= 0.0
            || frame.cloud_clock.iter().any(|value| !value.is_finite())
            || frame.cloud_clock[0] < 0.0
            || frame.cloud_clock[0].fract() != 0.0
            || frame.cloud_clock[1] < 0.0
            || !(0.0..=0.1).contains(&frame.cloud_clock[2])
            || !DVec4::new(
                frame.vegetation_gust_offset[0],
                frame.vegetation_gust_offset[1],
                0.0,
                0.0,
            )
            .as_vec4()
            .is_finite()
        {
            return Err("Animation acquisition cannot be projected to GPU".into());
        }
        Ok(frame)
    }

    pub fn wind_frame(&self) -> Result<WindFrame, String> {
        WindFrame::new(
            self.global_time_s,
            Wind {
                speed: self.wind_speed,
                direction: self.wind_direction,
            },
        )
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[cfg(feature = "physics-rapier")]
    #[test]
    #[expect(
        clippy::float_cmp,
        reason = "Exact acquired clocks and retained payloads must not admit a tolerance"
    )]
    fn retains_independent_source_histories_and_gust_phase() {
        let (bytes, index) =
            forge_tidewater_assets::tidewater_environment::animation_reference().unwrap();
        let frame = AcquiredFrame::from_json(&bytes, index).expect("actual acquired source frame");
        assert_eq!(frame.global_frame, 124);
        assert!((frame.ocean_time_s - frame.global_time_s - 4.0 / 60.0).abs() < 1e-12);
        assert_eq!(frame.cloud_clock[1], frame.ocean_time_s);
        assert!(
            frame
                .vegetation_gust_offset
                .iter()
                .all(|value| *value > 0.0)
        );
        assert_eq!(
            frame.wind_frame().unwrap().time_seconds(),
            frame.global_time_s
        );
    }
}
