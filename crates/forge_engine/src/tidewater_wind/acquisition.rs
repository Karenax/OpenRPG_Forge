//! Actual uniforms acquired after source precompile and 120 submitted frames.
use super::WindFrame;
use crate::tidewater_pier_motion::Wind;
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

#[derive(Deserialize)]
struct Capture {
    status: String,
    source_commit: String,
    warmup_frames: u32,
    gpu_frame: u32,
    animation_clocks: AcquiredFrame,
}

impl AcquiredFrame {
    /// Read the retained, unmodified capture, not a guessed animation phase.
    pub fn reference() -> Result<Self, String> {
        let capture: Capture = serde_json::from_str(include_str!(
            "../../../../third_party/tidewater/acquisitions/animation-reference-village-1438b1ab.json"
        ))
        .map_err(|error| error.to_string())?;
        if capture.status != "ready_reference_only"
            || capture.source_commit != "1438b1abfcaee3267092b75573014f4d9b4a983c"
            || capture.warmup_frames != 120
            || capture.gpu_frame == 0
        {
            return Err("Animation acquisition has an invalid source or frame sequence".into());
        }
        let mut frame = capture.animation_clocks;
        frame.global_frame = capture.gpu_frame;
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

    #[test]
    #[expect(
        clippy::float_cmp,
        reason = "Exact acquired clocks and retained payloads must not admit a tolerance"
    )]
    fn retains_independent_source_histories_and_gust_phase() {
        let frame = AcquiredFrame::reference().expect("actual acquired source frame");
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
