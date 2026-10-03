//! Camera-following cascade state, independent of filesystem and render adapters.
use super::{plants, terrain};
use crate::{
    sun_cascades::{CascadeConfig, CascadeView, SunCascades},
    tidewater_material::ObjectShadowSettings,
};
use bevy::{
    math::{DVec3, DVec4},
    prelude::*,
};

pub struct Frame {
    pub settings: ObjectShadowSettings,
    pub eyes: [Vec4; 3],
    pub layers: Vec<usize>,
    pub terrain: Option<terrain::Stream>,
    pub plants: Vec<plants::Stream>,
    pub update_index: u64,
}

impl Frame {
    pub fn validate(&self) -> Result<(), String> {
        let s = &self.settings;
        if !matches!(s.enabled, 0.0 | 1.0)
            || s.count != 3
            || s.map_size.to_bits() != 2048_f32.to_bits()
            || !s.camera.is_finite()
            || !s.forward.is_finite()
            || s.matrices.iter().any(|v| !v.is_finite())
            || s.cascades
                .iter()
                .chain(&s.blend)
                .chain(&self.eyes)
                .any(|v| !v.is_finite())
            || [s.bias, s.fade, s.sun_angular_diameter]
                .iter()
                .any(|v| !v.is_finite())
            || self.layers.iter().any(|&i| i >= 3)
            || self.layers.windows(2).any(|v| v[0] >= v[1])
        {
            return Err("Invalid live shadow frame".into());
        }
        if let Some(terrain) = &self.terrain {
            terrain.validate()?;
        }
        for stream in &self.plants {
            stream.validate()?;
        }
        Ok(())
    }
}

/// Advance only after the previous frame was extracted; an unsubmitted/aborted
/// frame requires `invalidate` before resuming. This preserves the source cadence.
pub struct Tracker {
    cascades: SunCascades,
    sun: DVec3,
    update_index: u64,
}
impl Tracker {
    pub fn new(sun: DVec3) -> Result<Self, String> {
        if !sun.is_finite() || (sun.length_squared() - 1.0).abs() > 1.0e-6 {
            return Err("Invalid live shadow sun".into());
        }
        Ok(Self {
            cascades: SunCascades::new(CascadeConfig::default())?,
            sun,
            update_index: 0,
        })
    }

    pub fn invalidate(&mut self) {
        self.cascades.invalidate();
    }

    pub fn next(&mut self, view: CascadeView, frame_index: u32) -> Result<Frame, String> {
        let next = self
            .update_index
            .checked_add(1)
            .ok_or("Shadow update counter exhausted")?;
        let layers = self.cascades.update(view, self.sun, true)?;
        self.update_index = next;
        let mut settings = ObjectShadowSettings {
            enabled: f32::from(self.cascades.sampling_enabled),
            camera: view.world_from_view.w_axis.as_vec4().truncate().extend(0.0),
            forward: (-view.world_from_view.z_axis).as_vec4(),
            frame_index,
            ..default()
        };
        let mut eyes = [Vec4::ZERO; 3];
        for (i, cascade) in self.cascades.cascades().iter().enumerate() {
            if let Some(c) = cascade {
                settings.matrices[i] = c.clip_from_world.as_mat4();
                settings.cascades[i] = DVec4::from_array(c.info).as_vec4();
                settings.blend[i] = DVec4::from_array(c.blend).as_vec4();
                eyes[i] = c.world_from_view.w_axis.as_vec4();
            }
        }
        Ok(Frame {
            settings,
            eyes,
            layers,
            terrain: None,
            plants: Vec::new(),
            update_index: next,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::math::DMat4;
    #[test]
    fn navigation_preserves_non_due_cascades_and_source_cadence() {
        let mut tracker = Tracker::new(DVec3::new(0.3, 0.8, 0.2).normalize()).unwrap();
        let mut view = CascadeView {
            world_from_view: DMat4::IDENTITY,
            near: 0.06,
            fov_degrees: 60.0,
            aspect: 16.0 / 9.0,
        };
        let expected = [
            vec![0, 1, 2],
            vec![0, 2],
            vec![0, 1],
            vec![0],
            vec![0, 1],
            vec![0, 2],
        ];
        let mut previous: Option<Frame> = None;
        for (i, layers) in expected.into_iter().enumerate() {
            view.world_from_view.w_axis.x += 40.0;
            let frame = tracker.next(view, 37).unwrap();
            frame.validate().unwrap();
            assert_eq!(frame.layers, layers);
            assert_eq!(frame.update_index, i as u64 + 1);
            if let Some(old) = previous {
                for c in 0..3 {
                    assert_eq!(
                        frame.settings.matrices[c] != old.settings.matrices[c],
                        layers.contains(&c)
                    );
                    assert_eq!(frame.eyes[c] != old.eyes[c], layers.contains(&c));
                }
            }
            previous = Some(frame);
        }
    }
}
