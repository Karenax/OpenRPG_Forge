// SPDX-License-Identifier: MIT
// Adapted from Tidewater Sky.js and App.updateSun at 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
//! Source solar clock, independent of the ocean animation clock. These derived
//! values still require an ordered scene publisher and atmosphere readback.
use bevy::math::DVec3;

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Settings {
    pub hours: f64,
    pub azimuth_degrees: f64,
    /// Hours per real second, as in the source App (zero pauses the clock).
    pub speed: f64,
}

impl Default for Settings {
    fn default() -> Self {
        Self {
            hours: 16.2,
            azimuth_degrees: 0.0,
            speed: 0.0,
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Frame {
    pub hours: f64,
    pub sun: DVec3,
    pub moon: DVec3,
    /// Source sun/moon switch; the atmosphere continues to use the true sun.
    pub key: DVec3,
    /// Shared source night value, also used for stars and local-lamp activation.
    pub night: f64,
}

#[derive(Clone, Debug)]
pub struct Clock {
    settings: Settings,
    frame: Frame,
}

impl Clock {
    pub fn new(settings: Settings) -> Result<Self, String> {
        Ok(Self {
            frame: evaluate(settings)?,
            settings,
        })
    }

    #[must_use]
    pub const fn settings(&self) -> Settings {
        self.settings
    }

    #[must_use]
    pub const fn frame(&self) -> &Frame {
        &self.frame
    }

    /// Rejected edits leave the previous settings and frame intact.
    pub fn set(&mut self, settings: Settings) -> Result<&Frame, String> {
        let frame = evaluate(settings)?;
        self.settings = settings;
        self.frame = frame;
        Ok(&self.frame)
    }

    /// Use the actual App delta, without the water's separate 1.5x multiplier.
    /// Paused time is left bit-identical (including an authored hour of 24).
    pub fn advance(&mut self, delta_seconds: f64) -> Result<&Frame, String> {
        if !delta_seconds.is_finite() || delta_seconds < 0.0 {
            return Err("Invalid solar delta".into());
        }
        let mut next = self.settings;
        if next.speed != 0.0 {
            next.hours = (next.hours + delta_seconds * next.speed + 24.0) % 24.0;
        }
        self.set(next)
    }
}

fn normalize(v: DVec3) -> DVec3 {
    v * (1.0 / (v.x * v.x + v.y * v.y + v.z * v.z).sqrt())
}

fn evaluate(settings: Settings) -> Result<Frame, String> {
    if !settings.hours.is_finite()
        || !(0.0..=24.0).contains(&settings.hours)
        || !settings.azimuth_degrees.is_finite()
        || !(-180.0..=180.0).contains(&settings.azimuth_degrees)
        || !settings.speed.is_finite()
        || settings.speed < 0.0
    {
        return Err("Invalid source solar settings".into());
    }
    let radians = std::f64::consts::PI / 180.0;
    let phi = 24.0 * radians;
    let dec = 6.0 * radians;
    let hour_angle = ((settings.hours - 12.0) * 15.0) * radians;
    let east = -dec.cos() * hour_angle.sin();
    let north = phi.cos() * dec.sin() - phi.sin() * dec.cos() * hour_angle.cos();
    let up = phi.sin() * dec.sin() + phi.cos() * dec.cos() * hour_angle.cos();
    let direction = normalize(DVec3::new(east, up, -north));
    // Preserve Vector3.applyAxisAngle's quaternion operation order, including
    // rotations at the UI's +/-180-degree boundaries.
    let half = (settings.azimuth_degrees * radians) / 2.0;
    let qy = half.sin();
    let qw = half.cos();
    let tx = 2.0 * (qy * direction.z);
    let tz = 2.0 * (-qy * direction.x);
    let sun = DVec3::new(
        direction.x + qw * tx + qy * tz,
        direction.y,
        direction.z + qw * tz - qy * tx,
    );
    let value = -sun.y;
    let night = if value <= 0.02 {
        0.0
    } else if value >= 0.18 {
        1.0
    } else {
        let x = (value - 0.02) / (0.18 - 0.02);
        x * x * (3.0 - 2.0 * x)
    };
    let moon = normalize(DVec3::new(-sun.x, sun.y.abs() * 0.8 + 0.25, -sun.z));
    Ok(Frame {
        hours: settings.hours,
        sun,
        moon,
        key: if sun.y > -0.07 { sun } else { moon },
        night,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn source_solar_matches_48_executed_upstream_samples() {
        let oracle: serde_json::Value =
            serde_json::from_str(include_str!("solar/oracle.json")).unwrap();
        assert_eq!(
            oracle["source_commit"],
            "1438b1abfcaee3267092b75573014f4d9b4a983c"
        );
        let samples = oracle["samples"].as_array().unwrap();
        assert_eq!(samples.len(), 48);
        let mut partial_night = false;
        for row in samples {
            let clock = Clock::new(Settings {
                hours: row["hours"].as_f64().unwrap(),
                azimuth_degrees: row["azimuth"].as_f64().unwrap(),
                speed: 0.0,
            })
            .unwrap();
            let frame = clock.frame();
            for (name, actual) in [("sun", frame.sun), ("moon", frame.moon), ("key", frame.key)] {
                for (index, value) in actual.to_array().iter().enumerate() {
                    assert!(
                        (value - row[name][index].as_f64().unwrap()).abs() < 1e-12,
                        "{name}: {row}"
                    );
                }
            }
            assert!((frame.night - row["night"].as_f64().unwrap()).abs() < 1e-12);
            partial_night |= frame.night > 0.0 && frame.night < 1.0;
        }
        assert!(
            partial_night,
            "The oracle must cover twilight, not only day/night endpoints"
        );
    }

    #[test]
    fn source_solar_pause_wrap_and_refusals_preserve_last_valid_frame() {
        let mut clock = Clock::new(Settings::default()).unwrap();
        let before = *clock.frame();
        assert_eq!(*clock.advance(10_000.0).unwrap(), before);
        for delta in [f64::NAN, f64::INFINITY, -1.0] {
            assert!(clock.advance(delta).is_err());
            assert_eq!(*clock.frame(), before);
        }
        assert!(
            clock
                .set(Settings {
                    hours: 25.0,
                    ..Settings::default()
                })
                .is_err()
        );
        assert_eq!(*clock.frame(), before);
        clock
            .set(Settings {
                hours: 23.9,
                speed: 0.5,
                azimuth_degrees: 0.0,
            })
            .unwrap();
        assert!((clock.advance(1.0).unwrap().hours - 0.4).abs() < 1e-12);
        let before = *clock.frame();
        clock
            .set(Settings {
                speed: f64::MAX,
                ..clock.settings()
            })
            .unwrap();
        let settings = clock.settings();
        assert!(clock.advance(f64::MAX).is_err());
        assert_eq!(clock.settings(), settings);
        assert_eq!(*clock.frame(), before);
    }
}
