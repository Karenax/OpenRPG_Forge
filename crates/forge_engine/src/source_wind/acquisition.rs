//! Validated explicit source clocks and wind for shared scene consumers.
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
    /// Neutral initial scene state when the content supplies no acquired phase.
    /// This synthetic starting point is not a reference capture or GPU observation.
    #[must_use]
    pub const fn neutral() -> Self {
        Self {
            global_time_s: 0.0,
            global_dt_s: 0.0,
            ocean_time_s: 0.0,
            ocean_time_scale: 1.0,
            cloud_clock: [0.0; 4],
            wind_speed: 0.0,
            wind_direction: [1.0, 0.0],
            vegetation_gust_offset: [0.0; 2],
            global_frame: 0,
        }
    }

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
    use serde_json::{Value, json};

    fn clocks() -> Value {
        json!({
            "global_time_s": 2.0, "global_dt_s": 0.02,
            "ocean_time_s": 3.0, "ocean_time_scale": 1.0,
            "cloud_clock": [12.0, 3.0, 0.02, 0.0],
            "wind_speed": 7.0, "wind_direction": [0.0, 1.0],
            "vegetation_gust_offset": [0.25, 0.5], "global_frame": 999
        })
    }

    #[test]
    fn neutral_scene_starts_without_a_captured_wind_or_phase() {
        let frame = AcquiredFrame::neutral();
        let wind = frame.wind_frame().unwrap();
        assert_eq!(frame.global_frame, 0);
        assert_eq!(frame.global_time_s.to_bits(), 0.0_f64.to_bits());
        assert_eq!(frame.ocean_time_s.to_bits(), 0.0_f64.to_bits());
        assert_eq!(
            frame.vegetation_gust_offset.map(f64::to_bits),
            [0.0_f64.to_bits(); 2]
        );
        assert_eq!(wind.time_seconds().to_bits(), 0.0_f64.to_bits());
        assert_eq!(wind.wind().speed.to_bits(), 0.0_f64.to_bits());
        assert_eq!(
            wind.wind().direction.map(f64::to_bits),
            [1.0_f64.to_bits(), 0.0_f64.to_bits()]
        );
    }

    #[test]
    #[expect(
        clippy::float_cmp,
        reason = "Exact source clock values must survive parsing"
    )]
    fn scene_supplied_clocks_preserve_frame_and_wind() {
        let frame = AcquiredFrame::from_json(&serde_json::to_vec(&clocks()).unwrap(), 31).unwrap();
        assert_eq!(frame.global_frame, 31);
        assert_eq!(frame.ocean_time_s, 3.0);
        assert_eq!(frame.wind_frame().unwrap().time_seconds(), 2.0);
        assert_eq!(frame.wind_frame().unwrap().wind().speed, 7.0);
    }

    #[test]
    fn invalid_scene_clock_or_wind_is_refused() {
        let invalid = [
            ("global_dt_s", json!(0.11)),
            ("ocean_time_s", json!(-1.0)),
            ("ocean_time_scale", json!(0.0)),
            ("cloud_clock", json!([12.5, 3.0, 0.02, 0.0])),
            ("wind_direction", json!([0.0, 0.0])),
            ("vegetation_gust_offset", json!([1e100, 0.0])),
        ];
        for (field, value) in invalid {
            let mut source = clocks();
            source[field] = value;
            assert!(
                AcquiredFrame::from_json(&serde_json::to_vec(&source).unwrap(), 31).is_err(),
                "{field}"
            );
        }
        assert!(AcquiredFrame::from_json(b"{}", 31).is_err());
    }
}
