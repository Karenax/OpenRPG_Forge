//! One validated wind and animation time shared by imported scene consumers.
use crate::tidewater_pier_motion::Wind;
use bevy::{
    math::{DVec4, Vec4},
    prelude::Resource,
};

#[derive(Clone, Copy, Debug, Resource)]
pub struct WindFrame {
    wind: Wind,
    time_seconds: f64,
}

impl WindFrame {
    /// Original Frame.js wind defaults, with an explicit acquisition time.
    pub fn source_defaults(time_seconds: f64) -> Result<Self, String> {
        // Vector2.normalize divides by sqrt(x*x+y*y) via multiplication by
        // its reciprocal. Preserve that operation order from the source.
        let inverse_length = 1.0 / (0.35_f64 * 0.35 + 0.94_f64 * 0.94).sqrt();
        Self::new(
            time_seconds,
            Wind {
                speed: 7.0,
                direction: [0.35 * inverse_length, 0.94 * inverse_length],
            },
        )
    }

    /// Accept a source frame only when both CPU and GPU values are representable.
    pub fn new(time_seconds: f64, wind: Wind) -> Result<Self, String> {
        let [x, z] = wind.direction;
        let uniform = DVec4::new(x, z, wind.speed, time_seconds).as_vec4();
        if !time_seconds.is_finite()
            || time_seconds < 0.0
            || !wind.speed.is_finite()
            || wind.speed < 0.0
            || !x.is_finite()
            || !z.is_finite()
            || (x.hypot(z) - 1.0).abs() > 1e-12
            || !uniform.is_finite()
        {
            return Err(
                "Imported scene wind frame is invalid or cannot be projected to GPU".into(),
            );
        }
        Ok(Self { wind, time_seconds })
    }

    /// Update wind without resetting the animation clock or acquired pendulums.
    pub fn set_wind(&mut self, wind: Wind) -> Result<(), String> {
        let next = Self::new(self.time_seconds, wind)?;
        *self = next;
        Ok(())
    }

    /// Global shader time advances by the full frame delta, as in the source.
    /// Pendulums separately retain their original 0.1-second integration clamp.
    pub fn advance(&mut self, delta_seconds: f64) -> Result<(), String> {
        if !delta_seconds.is_finite() || delta_seconds < 0.0 {
            return Err("Imported scene wind delta is invalid".into());
        }
        let next = Self::new(self.time_seconds + delta_seconds, self.wind)?;
        *self = next;
        Ok(())
    }

    #[must_use]
    pub fn wind(self) -> Wind {
        self.wind
    }
    #[must_use]
    pub fn time_seconds(self) -> f64 {
        self.time_seconds
    }
    /// Exact packing used by village-probe-header.wgsl's SourceFrame.
    #[must_use]
    pub fn material_uniform(self) -> Vec4 {
        DVec4::new(
            self.wind.direction[0],
            self.wind.direction[1],
            self.wind.speed,
            self.time_seconds,
        )
        .as_vec4()
    }
}
