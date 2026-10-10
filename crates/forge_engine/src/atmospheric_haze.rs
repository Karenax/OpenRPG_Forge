//! Per-camera haze controls shared by runtime scenes and Studio.
//! These are renderer inputs; persistent world settings belong to project data.
use bevy::{prelude::*, render::extract_component::ExtractComponent};

pub mod clock;
pub mod scene;

#[derive(Component, Clone, Copy, Debug, ExtractComponent)]
pub struct AirHazeSettings {
    pub enabled: bool,
    pub god_rays: bool,
    density: f32,
    shafts: f32,
    resolution_scale: f64,
}

impl Default for AirHazeSettings {
    fn default() -> Self {
        Self {
            enabled: true,
            god_rays: true,
            density: 1.6,
            shafts: 1.0,
            resolution_scale: 1.0,
        }
    }
}

impl AirHazeSettings {
    #[must_use]
    pub fn new(density: f32) -> Self {
        let mut settings = Self::default();
        settings.set_density(density);
        settings
    }

    /// Invalid edits retain the previous finite value.
    pub fn set_density(&mut self, density: f32) {
        if density.is_finite() {
            self.density = density.clamp(0.0, 4.0);
        }
    }

    pub fn set_shafts(&mut self, shafts: f32) {
        if shafts.is_finite() {
            self.shafts = shafts.clamp(0.0, 4.0);
        }
    }

    /// Target preparation separately rejects extents beyond device limits.
    pub fn set_resolution_scale(&mut self, scale: f64) -> bool {
        if !scale.is_finite() || scale <= 0.0 {
            return false;
        }
        self.resolution_scale = scale;
        true
    }

    #[must_use]
    pub fn density(&self) -> f32 {
        self.density
    }
    #[must_use]
    pub fn shafts(&self) -> f32 {
        self.shafts
    }
    #[must_use]
    pub fn resolution_scale(&self) -> f64 {
        self.resolution_scale
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn non_finite_edits_preserve_valid_controls_and_scale() {
        let mut settings = AirHazeSettings::new(0.8);
        settings.set_shafts(2.0);
        assert!(settings.set_resolution_scale(0.75));
        for invalid in [f32::NAN, f32::INFINITY, f32::NEG_INFINITY] {
            settings.set_density(invalid);
            settings.set_shafts(invalid);
        }
        for invalid in [f64::NAN, f64::INFINITY, 0.0, -1.0] {
            assert!(!settings.set_resolution_scale(invalid));
        }
        assert_eq!(settings.density().to_bits(), 0.8_f32.to_bits());
        assert_eq!(settings.shafts().to_bits(), 2.0_f32.to_bits());
        assert_eq!(settings.resolution_scale().to_bits(), 0.75_f64.to_bits());
    }

    #[test]
    fn independent_cameras_keep_their_own_haze_configuration() {
        let mut world = World::new();
        let tropical = world
            .spawn((Camera3d::default(), AirHazeSettings::default()))
            .id();
        let mut dry = AirHazeSettings::new(0.0);
        dry.god_rays = false;
        assert!(dry.set_resolution_scale(0.5));
        let desert = world.spawn((Camera3d::default(), dry)).id();
        world.get_mut::<AirHazeSettings>(desert).unwrap().enabled = false;
        let other = world.get::<AirHazeSettings>(tropical).unwrap();
        assert!(other.enabled && other.god_rays);
        assert_eq!(other.density().to_bits(), 1.6_f32.to_bits());
        assert_eq!(other.resolution_scale().to_bits(), 1.0_f64.to_bits());
    }
}
