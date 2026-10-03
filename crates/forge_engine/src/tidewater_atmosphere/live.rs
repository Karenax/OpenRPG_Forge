//! In-place atmosphere updates preserve resident image and readback identities.
use super::{KernelInput, Targets, ensure_registered};
use bevy::{prelude::*, render::extract_resource::ExtractResource};
use std::sync::{
    Arc,
    atomic::{AtomicU64, Ordering},
};

#[derive(Resource, Clone, ExtractResource)]
pub(super) struct Work {
    pub revision: u64,
    pub mask: u8,
    pub encoded: Arc<AtomicU64>,
}
impl Default for Work {
    fn default() -> Self {
        Self {
            revision: 1,
            mask: 0b1111,
            encoded: Arc::new(AtomicU64::new(0)),
        }
    }
}

/// Update uniforms without replacing LUTs, the irradiance buffer or generation.
/// `invalidate_static` follows source scattering invalidation; `integrate` is
/// requested by the periodic controller only when no irradiance read is pending.
/// This queues commands, and does not prove that a GPU result has completed.
pub fn update_parameters(
    world: &mut World,
    values: [f32; 16],
    invalidate_static: bool,
    integrate: bool,
) -> Result<(), String> {
    ensure_registered(world)?;
    if values.iter().any(|v| !v.is_finite()) {
        return Err("non-finite live atmosphere parameters".into());
    }
    let targets = world
        .get_resource::<Targets>()
        .ok_or("atmosphere targets absent")?;
    if !world.contains_resource::<KernelInput>() {
        return Err("atmosphere kernels absent".into());
    }
    let params =
        std::array::from_fn(|i| Vec4::from_array(std::array::from_fn(|j| values[i * 4 + j])));
    let changed = params != targets.params;
    let work = world
        .get_resource::<Work>()
        .ok_or("atmosphere work absent")?;
    let remaining = if work.encoded.load(Ordering::Acquire) == work.revision {
        0
    } else {
        work.mask
    };
    let mask = remaining
        | if invalidate_static {
            0b0111
        } else if changed {
            0b0100
        } else {
            0
        }
        | if integrate { 0b1000 } else { 0 };
    if mask == 0 {
        return Ok(());
    }
    let revision = work
        .revision
        .checked_add(1)
        .ok_or("atmosphere revision overflow")?;
    world.resource_mut::<Targets>().params = params;
    let mut work = world.resource_mut::<Work>();
    work.revision = revision;
    work.mask = mask;
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::super::{Input, Registered};
    use super::*;
    use bevy::render::storage::ShaderBuffer;
    use std::sync::atomic::AtomicBool;

    fn scene() -> World {
        let mut world = World::new();
        world.init_resource::<Assets<Image>>();
        world.init_resource::<Assets<ShaderBuffer>>();
        world.insert_resource(Registered);
        world.insert_resource(KernelInput(Arc::new(
            Input::new(
                std::array::from_fn(|_| "qualified in GPU harness".into()),
                [0.0; 16],
            )
            .unwrap(),
        )));
        world.insert_resource(Targets {
            params: [Vec4::ZERO; 4],
            images: vec![],
            irradiance: Handle::default(),
            ready: Arc::new(AtomicBool::new(true)),
        });
        let work = Work::default();
        work.encoded.store(work.revision, Ordering::Release);
        world.insert_resource(work);
        world
    }

    #[test]
    fn moving_sun_preserves_identity_and_pending_integration() {
        let mut world = scene();
        let before = world.resource::<Targets>().clone();
        let mut values = [0.0; 16];
        values[12] = 1.0;
        update_parameters(&mut world, values, false, true).unwrap();
        assert_eq!(world.resource::<Work>().mask, 0b1100);
        values[12] = 0.5;
        update_parameters(&mut world, values, false, false).unwrap();
        assert_eq!(world.resource::<Work>().mask, 0b1100);
        let after = world.resource::<Targets>();
        assert!(Arc::ptr_eq(&before.ready, &after.ready));
        assert_eq!(before.irradiance, after.irradiance);
        assert_eq!(before.images, after.images);
    }

    #[test]
    fn completed_work_is_not_repeated_and_static_invalidation_is_explicit() {
        let mut world = scene();
        update_parameters(&mut world, [0.0; 16], false, false).unwrap();
        assert_eq!(world.resource::<Work>().revision, 1);
        update_parameters(&mut world, [0.0; 16], true, false).unwrap();
        assert_eq!(world.resource::<Work>().mask, 0b0111);
        let work = world.resource::<Work>();
        work.encoded.store(work.revision, Ordering::Release);
        update_parameters(&mut world, [0.0; 16], false, true).unwrap();
        assert_eq!(world.resource::<Work>().mask, 0b1000);
    }

    #[test]
    fn invalid_input_and_revision_overflow_leave_state_unchanged() {
        let mut world = scene();
        let mut values = [0.0; 16];
        values[12] = f32::NAN;
        assert!(update_parameters(&mut world, values, false, true).is_err());
        assert_eq!(world.resource::<Targets>().params, [Vec4::ZERO; 4]);
        world.resource_mut::<Work>().revision = u64::MAX;
        values[12] = 1.0;
        assert!(update_parameters(&mut world, values, false, true).is_err());
        assert_eq!(world.resource::<Targets>().params, [Vec4::ZERO; 4]);
        assert_eq!(world.resource::<Work>().revision, u64::MAX);
    }
}
