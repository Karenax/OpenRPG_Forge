//! Camera/history publication from an actually encoded cloud temporal pass.
//! This render-only receipt is not GPU completion or a paired final-image tag.
use super::Targets;
use bevy::prelude::*;
use std::sync::{
    Arc,
    atomic::{AtomicBool, Ordering},
};

/// Basis and frustum used by the encoded trace, decoded from its GPU uniform.
#[derive(Clone, Copy, Debug)]
pub struct Camera {
    pub right: Vec4,
    pub up: Vec4,
    pub forward: Vec4,
    /// Frustum tangents XY and actual history dimensions ZW.
    pub tangent_size: Vec4,
    pub clock: Vec4,
}

pub(super) struct Encoded {
    pub camera: Camera,
    pub slot: usize,
    pub step: u32,
}

impl Encoded {
    pub(super) fn from_frame(local: &[u8], slot: usize, step: u32) -> Option<Self> {
        if local.len() != 544 || slot > 1 || step == 0 {
            return None;
        }
        let vector = |index: usize| -> Option<Vec4> {
            let bytes = local.get(index * 16..index * 16 + 16)?;
            let value = Vec4::from_array(std::array::from_fn(|word| {
                f32::from_le_bytes(
                    bytes[word * 4..word * 4 + 4]
                        .try_into()
                        .expect("four-byte word"),
                )
            }));
            value.is_finite().then_some(value)
        };
        let position_tan = vector(0)?;
        let right_aspect = vector(1)?;
        let up = vector(2)?.with_w(0.0);
        let forward = vector(3)?.with_w(0.0);
        let size = vector(8)?.zw();
        let clock = vector(10)?;
        let tangents = Vec2::new(position_tan.w * right_aspect.w, position_tan.w);
        if !tangents.is_finite()
            || tangents.min_element() <= 0.0
            || size != Vec2::new(960.0, 540.0)
            || [right_aspect.truncate(), up.truncate(), forward.truncate()]
                .iter()
                .any(|basis| basis.length_squared() < 1e-12)
        {
            return None;
        }
        Some(Self {
            camera: Camera {
                right: right_aspect.with_w(0.0),
                up,
                forward,
                tangent_size: Vec4::new(tangents.x, tangents.y, size.x, size.y),
                clock,
            },
            slot,
            step,
        })
    }
}

/// The texture that was written, with the camera and generation that wrote it.
#[derive(Clone)]
pub struct View {
    pub camera: Camera,
    pub history: Handle<Image>,
    pub panorama: Handle<Image>,
    pub encoded_step: u32,
    slot: usize,
    generation: Arc<AtomicBool>,
}

impl View {
    #[must_use]
    pub fn encoded_for(&self, targets: &Targets) -> bool {
        Arc::ptr_eq(&self.generation, &targets.ready)
            && targets.ready.load(Ordering::Acquire)
            && targets.completed_steps.load(Ordering::Acquire) >= self.encoded_step
            && targets.images.get(5 + self.slot * 2) == Some(&self.history)
            && targets.images.get(9) == Some(&self.panorama)
    }
}

#[derive(Resource, Default)]
pub struct Sampling {
    pub current: Option<View>,
}

impl Sampling {
    pub(super) fn reset(&mut self) {
        self.current = None;
    }

    pub(super) fn record(&mut self, encoded: Encoded, targets: &Targets) -> bool {
        let (Some(history), Some(panorama)) = (
            targets.images.get(5 + encoded.slot * 2),
            targets.images.get(9),
        ) else {
            return false;
        };
        if self.current.as_ref().is_some_and(|view| {
            Arc::ptr_eq(&view.generation, &targets.ready) && view.encoded_step >= encoded.step
        }) {
            return false;
        }
        self.current = Some(View {
            camera: encoded.camera,
            history: history.clone(),
            panorama: panorama.clone(),
            encoded_step: encoded.step,
            slot: encoded.slot,
            generation: targets.ready.clone(),
        });
        true
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::atomic::AtomicU32;

    fn bytes() -> Vec<u8> {
        let mut words = [[0.0_f32; 4]; 34];
        words[0] = [40.0, 15.0, -90.0, 0.5];
        words[1] = [1.0, 0.0, 0.0, 2.0];
        words[2] = [0.0, 1.0, 0.0, 0.0];
        words[3] = [0.0, 0.0, -1.0, 0.0];
        words[8] = [1920.0, 1080.0, 960.0, 540.0];
        words[10] = [124.0, 2.0, 0.01, 0.0];
        words
            .into_iter()
            .flatten()
            .flat_map(f32::to_le_bytes)
            .collect()
    }

    fn new_targets() -> Targets {
        let mut images = Assets::<Image>::default();
        Targets {
            completed_steps: Arc::new(AtomicU32::new(124)),
            submissions: Arc::new(AtomicU32::new(1)),
            ready: Arc::new(AtomicBool::new(true)),
            images: (0..10).map(|_| images.add(Image::default())).collect(),
        }
    }

    #[test]
    fn encoded_camera_rejects_invalid_packets_and_preserves_gpu_fields() {
        let data = bytes();
        let encoded = Encoded::from_frame(&data, 1, 124).unwrap();
        assert_eq!(
            encoded.camera.tangent_size,
            Vec4::new(1.0, 0.5, 960.0, 540.0)
        );
        assert_eq!(encoded.camera.clock.x.to_bits(), 124.0_f32.to_bits());
        assert!(Encoded::from_frame(&data[..543], 0, 124).is_none());
        assert!(Encoded::from_frame(&data, 2, 124).is_none());
        let mut corrupt = data.clone();
        corrupt[16..20].copy_from_slice(&f32::NAN.to_le_bytes());
        assert!(Encoded::from_frame(&corrupt, 0, 124).is_none());
    }

    #[test]
    fn history_slot_camera_and_generation_remain_coherent() {
        let targets = new_targets();
        let mut publication = Sampling::default();
        assert!(publication.record(Encoded::from_frame(&bytes(), 1, 124).unwrap(), &targets));
        let view = publication.current.as_ref().unwrap();
        assert_eq!(view.history, targets.images[7]);
        assert!(view.encoded_for(&targets));
        // A frame which skipped temporal tracing must not relabel the history.
        targets.completed_steps.store(125, Ordering::Release);
        assert!(view.encoded_for(&targets));
        assert!(!publication.record(Encoded::from_frame(&bytes(), 0, 123).unwrap(), &targets));
        let replacement = new_targets();
        assert!(
            !publication
                .current
                .as_ref()
                .unwrap()
                .encoded_for(&replacement)
        );
        publication.reset();
        assert!(publication.current.is_none());
    }
}
