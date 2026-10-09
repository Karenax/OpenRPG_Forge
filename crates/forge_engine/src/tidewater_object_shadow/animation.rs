//! Transient vegetation motion for the current resident caster generation.
use super::{KernelInput, Target};
use bevy::{prelude::*, render::extract_resource::ExtractResource};
use std::sync::{
    Arc, Mutex,
    atomic::{AtomicU32, Ordering},
};

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Motion {
    pub main_camera: Vec4,
    /// Wind direction XZ, speed in m/s, elapsed seconds.
    pub wind_time: Vec4,
    /// Integrated gust offset in world metres XZ, shader frame index, night factor.
    pub gust_frame: Vec4,
}
impl Motion {
    pub(crate) fn validate(self) -> Result<(), String> {
        if !self.main_camera.is_finite()
            || !self.wind_time.is_finite()
            || !self.gust_frame.is_finite()
            || self.wind_time.z < 0.0
            || self.wind_time.w < 0.0
            || self.gust_frame.z < 0.0
        {
            return Err("invalid plant shadow motion".into());
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, Default)]
pub struct Statistics {
    pub submissions: u64,
    pub layers: [u64; 3],
    pub recovered: u64,
    pub last_sequence: u32,
    pub last_mask: u32,
}

#[derive(Resource, Clone, Default)]
pub struct State {
    pub(super) motion: Option<Motion>,
    pub(super) sequence: u32,
    /// Latest animation sequence encoded; not a GPU completion fence.
    pub encoded: Arc<AtomicU32>,
    extracted: Arc<AtomicU32>,
    statistics: Arc<Mutex<Statistics>>,
    pub(super) frame: Option<Arc<crate::tidewater_object_shadow::live::Frame>>,
}

impl ExtractResource for State {
    type Source = Self;

    fn extract_resource(source: &Self) -> Self {
        // Retain the immutable pose before allowing the next main-world update.
        let snapshot = source.clone();
        source.extracted.store(source.sequence, Ordering::Release);
        snapshot
    }
}

impl State {
    #[must_use]
    pub fn can_submit(&self) -> bool {
        self.sequence == self.extracted.load(Ordering::Acquire)
    }

    #[must_use]
    pub fn is_idle(&self) -> bool {
        self.sequence == self.encoded.load(Ordering::Acquire)
    }

    #[must_use]
    pub fn statistics(&self) -> Statistics {
        *self.statistics.lock().expect("shadow statistics mutex")
    }

    pub(super) fn needs_recovery(&self) -> bool {
        self.sequence
            .saturating_sub(self.encoded.load(Ordering::Acquire))
            > 1
    }

    /// Called only after the actual depth pass loop, never on main submission.
    pub(super) fn record_encoded(&self, layers: &[usize], recovered: bool) -> Statistics {
        let mut statistics = self.statistics.lock().expect("shadow statistics mutex");
        statistics.submissions += 1;
        statistics.last_mask = 0;
        for &layer in layers {
            statistics.layers[layer] += 1;
            statistics.last_mask |= 1 << layer;
        }
        statistics.recovered += u64::from(recovered);
        statistics.last_sequence = self.sequence;
        self.encoded.store(self.sequence, Ordering::Release);
        *statistics
    }
}

/// Commit a complete camera/caster pose after its predecessor was extracted.
/// The render snapshot retains the previous pose while the main world advances.
pub fn submit_frame(
    world: &mut World,
    motion: Motion,
    frame: crate::tidewater_object_shadow::live::Frame,
) -> Result<u32, String> {
    motion.validate()?;
    frame.validate()?;
    let target = world
        .get_resource::<Target>()
        .ok_or("shadow target absent")?;
    let input = world
        .get_resource::<KernelInput>()
        .ok_or("shadow inputs absent")?;
    if let Some(models) = &frame.opaque_models {
        crate::tidewater_object_shadow::validate_models(models, input.0.draws.len())?;
    }
    if !target.ready.load(Ordering::Acquire)
        || input.0.plants.as_ref().map_or(0, |p| p.batches.len()) != frame.plants.len()
        || input.0.terrain.is_some() != frame.terrain.is_some()
    {
        return Err("Live shadow generation differs from resident casters".into());
    }
    let mut state = world
        .get_resource_mut::<State>()
        .ok_or("shadow animation absent")?;
    if !state.can_submit() {
        return Err("Previous shadow frame has not been extracted".into());
    }
    state.sequence = state
        .sequence
        .checked_add(1)
        .ok_or("shadow animation sequence exhausted")?;
    state.motion = Some(motion);
    state.frame = Some(Arc::new(frame));
    Ok(state.sequence)
}

/// Publish the same pose used by visible materials before Bevy's extraction.
/// Replacing a shadow generation clears animation; callers must rebind explicitly.
pub fn submit(world: &mut World, motion: Motion) -> Result<u32, String> {
    motion.validate()?;
    let target = world
        .get_resource::<Target>()
        .ok_or("shadow target absent")?;
    let input = world
        .get_resource::<KernelInput>()
        .ok_or("shadow inputs absent")?;
    if !target.ready.load(Ordering::Acquire) || input.0.plants.is_none() {
        return Err("plant shadows are not ready".into());
    }
    let mut state = world
        .get_resource_mut::<State>()
        .ok_or("shadow animation absent")?;
    if state.motion == Some(motion) {
        return Ok(state.sequence);
    }
    if !state.can_submit() {
        return Err("Previous shadow frame has not been extracted".into());
    }
    let sequence = state
        .sequence
        .checked_add(1)
        .ok_or("shadow animation sequence exhausted")?;
    state.sequence = sequence;
    state.motion = Some(motion);
    state.frame = None;
    Ok(sequence)
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn extraction_releases_main_pose_without_waiting_for_encoding() {
        let mut tracker =
            crate::tidewater_object_shadow::live::Tracker::new(bevy::math::DVec3::Y).unwrap();
        let view = crate::sun_cascades::CascadeView {
            world_from_view: bevy::math::DMat4::IDENTITY,
            near: 0.06,
            fov_degrees: 60.0,
            aspect: 16.0 / 9.0,
        };
        let mut first = tracker.next(view, 1).unwrap();
        let old_pose = Mat4::from_translation(Vec3::new(10.0, 2.0, 3.0));
        first.opaque_models = Some(vec![old_pose]);
        let mut main = State {
            sequence: 1,
            frame: Some(Arc::new(first)),
            ..default()
        };
        assert!(!main.can_submit());
        assert!(!main.is_idle());
        let snapshot = State::extract_resource(&main);
        assert!(main.can_submit());
        assert!(!main.is_idle());
        main.sequence = 2;
        let mut second = tracker.next(view, 2).unwrap();
        let new_pose = Mat4::from_translation(Vec3::new(10.0, 2.5, 3.0));
        second.opaque_models = Some(vec![new_pose]);
        main.frame = Some(Arc::new(second));
        assert!(!main.can_submit());
        assert_eq!(snapshot.sequence, 1);
        assert_eq!(snapshot.frame.as_ref().unwrap().settings.frame_index, 1);
        assert_eq!(main.frame.as_ref().unwrap().settings.frame_index, 2);
        assert_eq!(
            snapshot.frame.as_ref().unwrap().opaque_models,
            Some(vec![old_pose])
        );
        assert_eq!(
            main.frame.as_ref().unwrap().opaque_models,
            Some(vec![new_pose])
        );
        snapshot.record_encoded(&[0, 2], false);
        assert_eq!(main.encoded.load(Ordering::Acquire), 1);
        assert!(!main.needs_recovery());
        assert_eq!(main.statistics().layers, [1, 0, 1]);
    }

    #[test]
    fn skipped_encoding_is_recovered_and_generations_are_isolated() {
        let mut main = State {
            sequence: 1,
            ..default()
        };
        let old = State::extract_resource(&main);
        main.sequence = 2;
        let next = State::extract_resource(&main);
        assert!(next.needs_recovery());
        next.record_encoded(&[0, 1, 2], true);
        assert!(main.is_idle());
        assert_eq!(main.statistics().recovered, 1);
        let fresh = State::default();
        old.record_encoded(&[0], false);
        assert_eq!(fresh.statistics().submissions, 0);
        assert!(fresh.is_idle());
    }
    #[test]
    fn rejects_nonfinite_motion_before_touching_a_world() {
        let mut world = World::new();
        for bad in [f32::NAN, f32::INFINITY, -1.0] {
            assert!(
                submit(
                    &mut world,
                    Motion {
                        main_camera: Vec4::ZERO,
                        wind_time: Vec4::new(1.0, 0.0, 6.0, bad),
                        gust_frame: Vec4::ZERO
                    }
                )
                .is_err()
            );
            assert!(!world.contains_resource::<State>());
        }
    }
}
