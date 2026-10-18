//! Explicit solar lighting for optical effects, independent from scene materials.
use bevy::prelude::*;
#[derive(Resource, Clone, Copy, Debug)]
pub struct SceneSun {
    direction: Vec3,
    radiance: Vec3,
}
impl SceneSun {
    pub fn new(direction: Vec3, radiance: Vec3) -> Result<Self, String> {
        if !direction.is_finite()
            || (direction.length_squared() - 1.0).abs() > 0.001
            || !radiance.is_finite()
            || radiance.min_element() < 0.0
        {
            return Err("Invalid solar direction or radiance".into());
        }
        Ok(Self {
            direction,
            radiance,
        })
    }
    #[must_use]
    pub const fn direction(self) -> Vec3 {
        self.direction
    }
    #[must_use]
    pub const fn radiance(self) -> Vec3 {
        self.radiance
    }
}
