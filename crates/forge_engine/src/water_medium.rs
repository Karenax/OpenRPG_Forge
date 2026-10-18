//! Current camera medium, independent of any imported scene.
use bevy::prelude::{Resource, SystemSet};

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub enum Immersion {
    #[default]
    Unavailable,
    Air,
    SurfaceBand,
    FullyImmersed,
}

/// CPU-visible state of the last camera/sample match.
#[derive(Resource, Clone, Copy, Debug, Default, PartialEq)]
pub struct UnderwaterObservation {
    pub immersion: Immersion,
    pub effect_enabled: bool,
    pub water_height_m: f32,
    pub camera_height_m: f32,
    pub camera_depth_m: f32,
    pub sample_step: u32,
    pub sample_time_s: f64,
    pub encoded_frames: u64,
    pub encoded_enabled_frames: u64,
    pub encoded_bypassed_frames: u64,
    pub shafts_strength: f32,
    pub encoded_shaft_frames: u64,
}

impl UnderwaterObservation {
    #[must_use]
    pub fn camera_underwater(self) -> bool {
        self.immersion == Immersion::FullyImmersed
    }

    #[must_use]
    pub fn effect_active(self) -> bool {
        self.effect_enabled
            && matches!(
                self.immersion,
                Immersion::SurfaceBand | Immersion::FullyImmersed
            )
    }

    #[must_use]
    pub fn pipeline_ready(self) -> bool {
        self.encoded_frames > 0
    }
}

/// Ordering boundary for effects that consume current camera immersion.
#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct WaterMediumUpdateSet;
