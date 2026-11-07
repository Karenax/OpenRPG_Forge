//! One validated wind and animation time shared by imported scene consumers.
use crate::pendulum_motion::Wind;
use bevy::{
    math::{DVec4, Vec4},
    prelude::{Resource, World},
};
mod acquisition;
pub mod vegetation;

pub use acquisition::AcquiredFrame;

/// Accepted scene wind. Absence retains the historical consumer-specific input.
#[derive(Clone, Copy, Debug, Resource)]
pub struct SceneWind {
    settings: forge_project::zone_authoring::wind::SceneWindSettings,
    wind: Wind,
}

impl SceneWind {
    pub fn new(
        settings: forge_project::zone_authoring::wind::SceneWindSettings,
    ) -> Result<Self, String> {
        let wind = Wind {
            speed: f64::from(settings.speed_mps),
            direction: settings.direction_xz()?,
        };
        WindFrame::new(0.0, wind)?;
        Ok(Self { settings, wind })
    }

    #[must_use]
    pub const fn wind(self) -> Wind {
        self.wind
    }

    #[must_use]
    pub const fn speed_mps(self) -> f32 {
        self.settings.speed_mps
    }

    /// Replace the accepted wind only after validating the complete new setting.
    pub fn publish(
        world: &mut World,
        settings: Option<forge_project::zone_authoring::wind::SceneWindSettings>,
    ) -> Result<(), String> {
        let next = settings.map(Self::new).transpose()?;
        if world.get_resource::<Self>().map(|value| value.settings) == settings {
            return Ok(());
        }
        if let Some(next) = next {
            world.insert_resource(next);
        } else {
            world.remove_resource::<Self>();
        }
        Ok(())
    }

    /// Publish an authored setting and update the scene clock without resetting
    /// its phase. Removing the setting restores this scene's acquired wind when
    /// one exists; no input is borrowed from another scene.
    ///
    /// # Errors
    /// Rejects invalid settings or scene inputs before changing either resource.
    pub fn publish_with_frame(
        world: &mut World,
        settings: Option<forge_project::zone_authoring::wind::SceneWindSettings>,
    ) -> Result<(), String> {
        let next = settings.map(Self::new).transpose()?;
        let wind = if let Some(next) = next {
            Some(next.wind())
        } else {
            world
                .get_resource::<AcquiredFrame>()
                .map(|frame| frame.wind_frame().map(WindFrame::wind))
                .transpose()?
        };
        let updated = match (world.get_resource::<WindFrame>().copied(), wind) {
            (Some(mut frame), Some(wind)) => {
                frame.set_wind(wind)?;
                Some(frame)
            }
            _ => None,
        };
        // Both candidates have been validated, so neither resource can be
        // changed by a rejected setting or invalid fallback.
        if let Some(frame) = updated {
            world.insert_resource(frame);
        }
        if let Some(next) = next {
            world.insert_resource(next);
        } else {
            world.remove_resource::<Self>();
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, Resource)]
pub struct WindFrame {
    wind: Wind,
    time_seconds: f64,
    max_delta_seconds: f64,
}

impl WindFrame {
    /// Accept a source frame only when both CPU and GPU values are representable.
    pub fn new(time_seconds: f64, wind: Wind) -> Result<Self, String> {
        Self::with_max_delta(time_seconds, wind, 0.1)
    }

    /// A scene chooses its own bounded animation step; no content identity is consulted.
    pub fn with_max_delta(
        time_seconds: f64,
        wind: Wind,
        max_delta_seconds: f64,
    ) -> Result<Self, String> {
        let [x, z] = wind.direction;
        let uniform = DVec4::new(x, z, wind.speed, time_seconds).as_vec4();
        if !max_delta_seconds.is_finite()
            || max_delta_seconds <= 0.0
            || !time_seconds.is_finite()
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
        Ok(Self {
            wind,
            time_seconds,
            max_delta_seconds,
        })
    }

    /// Update wind without resetting the animation clock or acquired pendulums.
    pub fn set_wind(&mut self, wind: Wind) -> Result<(), String> {
        let next = Self::with_max_delta(self.time_seconds, wind, self.max_delta_seconds)?;
        *self = next;
        Ok(())
    }

    /// Advance the scene clock by its declared maximum delta.
    pub fn advance(&mut self, delta_seconds: f64) -> Result<(), String> {
        if !delta_seconds.is_finite() || delta_seconds < 0.0 {
            return Err("Imported scene wind delta is invalid".into());
        }
        let next = Self::with_max_delta(
            self.time_seconds + delta_seconds.min(self.max_delta_seconds),
            self.wind,
            self.max_delta_seconds,
        )?;
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
    /// Exact packing used by village-probe-header.wgsl's `SourceFrame`.
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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn accepted_scene_wind_replaces_atomically_and_can_be_removed() {
        use forge_project::zone_authoring::wind::SceneWindSettings;
        let mut world = World::new();
        let first = SceneWindSettings {
            schema_version: 1,
            heading_degrees: 90.0,
            speed_mps: 7.0,
        };
        SceneWind::publish(&mut world, Some(first)).unwrap();
        let previous = world.resource::<SceneWind>().wind();
        assert!((previous.direction[0] - 1.0).abs() < 1e-12);
        assert_eq!(previous.speed.to_bits(), 7.0_f64.to_bits());
        let invalid = SceneWindSettings {
            speed_mps: -1.0,
            ..first
        };
        assert!(SceneWind::publish(&mut world, Some(invalid)).is_err());
        assert_eq!(world.resource::<SceneWind>().settings, first);
        SceneWind::publish(&mut world, None).unwrap();
        assert!(!world.contains_resource::<SceneWind>());
    }

    #[test]
    fn authored_wind_restores_only_the_current_scenes_fallback_and_keeps_phase() {
        use forge_project::zone_authoring::wind::SceneWindSettings;
        let mut world = World::new();
        let neutral = AcquiredFrame::neutral();
        world.insert_resource(neutral.wind_frame().unwrap());
        world.insert_resource(neutral);
        world.resource_mut::<WindFrame>().advance(0.08).unwrap();
        let phase = world.resource::<WindFrame>().time_seconds();
        let authored = SceneWindSettings {
            schema_version: 1,
            heading_degrees: 90.0,
            speed_mps: 7.0,
        };
        SceneWind::publish_with_frame(&mut world, Some(authored)).unwrap();
        assert_eq!(
            world.resource::<WindFrame>().wind().speed.to_bits(),
            7.0_f64.to_bits()
        );
        assert_eq!(
            world.resource::<WindFrame>().time_seconds().to_bits(),
            phase.to_bits()
        );

        let invalid = SceneWindSettings {
            speed_mps: -1.0,
            ..authored
        };
        let before = world.resource::<WindFrame>().material_uniform();
        assert!(SceneWind::publish_with_frame(&mut world, Some(invalid)).is_err());
        assert_eq!(world.resource::<WindFrame>().material_uniform(), before);
        assert_eq!(world.resource::<SceneWind>().settings, authored);
        SceneWind::publish_with_frame(&mut world, None).unwrap();
        assert_eq!(
            world.resource::<WindFrame>().wind().speed.to_bits(),
            0.0_f64.to_bits()
        );
        assert_eq!(
            world.resource::<WindFrame>().time_seconds().to_bits(),
            phase.to_bits()
        );

        let mut second = AcquiredFrame::neutral();
        second.wind_speed = 3.0;
        second.wind_direction = [0.0, 1.0];
        world.insert_resource(second);
        SceneWind::publish_with_frame(&mut world, None).unwrap();
        let wind = world.resource::<WindFrame>().wind();
        assert_eq!(wind.speed.to_bits(), 3.0_f64.to_bits());
        assert_eq!(
            wind.direction.map(f64::to_bits),
            [0.0_f64.to_bits(), 1.0_f64.to_bits()]
        );
        assert_eq!(
            world.resource::<WindFrame>().time_seconds().to_bits(),
            phase.to_bits()
        );
        assert!(!world.contains_resource::<SceneWind>());
    }

    #[test]
    fn invalid_fallback_does_not_replace_the_authored_wind() {
        use forge_project::zone_authoring::wind::SceneWindSettings;
        let mut world = World::new();
        let neutral = AcquiredFrame::neutral();
        world.insert_resource(neutral.wind_frame().unwrap());
        let authored = SceneWindSettings {
            schema_version: 1,
            heading_degrees: 0.0,
            speed_mps: 4.0,
        };
        SceneWind::publish_with_frame(&mut world, Some(authored)).unwrap();
        let before = world.resource::<WindFrame>().material_uniform();
        let mut invalid = neutral;
        invalid.wind_direction = [0.0, 0.0];
        world.insert_resource(invalid);
        assert!(SceneWind::publish_with_frame(&mut world, None).is_err());
        assert_eq!(world.resource::<WindFrame>().material_uniform(), before);
        assert_eq!(world.resource::<SceneWind>().settings, authored);
    }

    #[test]
    #[expect(
        clippy::float_cmp,
        reason = "Exact acquired clocks and retained payloads must not admit a tolerance"
    )]
    fn global_clock_uses_source_engine_delta_including_slow_frames() {
        let mut frame = WindFrame::new(
            2.0,
            Wind {
                speed: 7.0,
                direction: [0.0, 1.0],
            },
        )
        .expect("acquired global time");
        let mut source_time = 2.0;
        for delta in [1.0_f64 / 60.0, 0.25, 0.0, 0.031, 1.2, 0.001] {
            // Engine.js clamps the application delta before App.frame.
            source_time += delta.min(0.1);
            frame.advance(delta).expect("finite source frame");
            assert_eq!(frame.time_seconds(), source_time);
        }
    }

    #[test]
    fn invalid_delta_does_not_replace_an_acquired_frame() {
        let mut frame = WindFrame::new(
            2.0,
            Wind {
                speed: 7.0,
                direction: [0.0, 1.0],
            },
        )
        .expect("acquired global time");
        let before = frame.material_uniform();
        for delta in [f64::NAN, f64::INFINITY, -1.0] {
            assert!(frame.advance(delta).is_err());
            assert_eq!(frame.material_uniform(), before);
        }
    }
}
