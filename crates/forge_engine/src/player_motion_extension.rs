//! Runtime contract for an optional locomotion provider.
//!
//! Providers own their simulation and publish observations before the controller's
//! camera and gameplay consumers. Removing this resource restores ordinary motion.
use bevy::prelude::*;

#[derive(Resource, Clone, Copy, Debug, Default)]
pub struct PlayerMotionExtension {
    /// An already positioned eye; None keeps the authored camera profile.
    pub camera_eye: Option<Vec3>,
    /// True while a provider waits for its collider change to reach the backend.
    pub backend_sync_pending: bool,
    /// A provider can project a vertical support correction after physics writeback.
    pub projects_vertical_pose: bool,
    /// Position of an observed wet contact, independently from its implementation.
    pub wet_contact: Option<Vec2>,
}

impl PlayerMotionExtension {
    #[must_use]
    pub fn is_wet_at(&self, position: Vec3) -> bool {
        self.wet_contact.is_some_and(|point| {
            point.is_finite() && position.is_finite() && point.distance(position.xz()) < 1.0
        })
    }
}

/// Stable phase after a provider's physics projection, before engine observation.
#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct PlayerMotionPublication;

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn contact_evidence_is_local_and_finite() {
        let mut state = PlayerMotionExtension::default();
        assert!(!state.is_wet_at(Vec3::ZERO));
        state.wet_contact = Some(Vec2::ZERO);
        assert!(state.is_wet_at(Vec3::new(0.5, 40.0, 0.0)));
        assert!(!state.is_wet_at(Vec3::X));
        assert!(!state.is_wet_at(Vec3::NAN));
        state.wet_contact = Some(Vec2::NAN);
        assert!(!state.is_wet_at(Vec3::ZERO));
    }
}
