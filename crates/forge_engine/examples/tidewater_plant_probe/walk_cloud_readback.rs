//! GPU evidence that the native cloud targets remain finite and evolve after acquisition.
use bevy::{
    prelude::*,
    render::gpu_readback::{Readback, ReadbackComplete},
};
use std::sync::atomic::Ordering;

#[derive(Resource)]
pub(super) struct Proof {
    pub samples: [Vec<(u32, forge_core::ContentHash)>; 3],
    pub finite: bool,
}

impl Default for Proof {
    fn default() -> Self {
        Self {
            samples: default(),
            finite: true,
        }
    }
}

impl Proof {
    pub fn complete(&self) -> bool {
        self.samples.iter().all(|samples| samples.len() >= 2)
    }

    pub fn changed(&self) -> bool {
        self.finite
            && self.complete()
            && self
                .samples
                .iter()
                .all(|samples| samples[0].1 != samples[1].1)
    }
}

#[derive(Component)]
struct Kind(usize);

pub(super) fn install(app: &mut App) {
    app.init_resource::<Proof>().add_systems(Update, request);
}

fn request(
    mut commands: Commands,
    targets: Res<forge_engine::tidewater_clouds::Targets>,
    smoke: Res<super::Smoke>,
    mut started: Local<bool>,
) {
    if *started || targets.completed_steps.load(Ordering::Acquire) < smoke.sample_step {
        return;
    }
    *started = true;
    for (kind, image) in [2, 5, 9].into_iter().enumerate() {
        commands
            .spawn((Readback::texture(targets.images[image].clone()), Kind(kind)))
            .observe(receive);
    }
}

fn receive(
    event: On<ReadbackComplete>,
    kinds: Query<&Kind>,
    targets: Res<forge_engine::tidewater_clouds::Targets>,
    mut proof: ResMut<Proof>,
    mut commands: Commands,
) {
    let Ok(kind) = kinds.get(event.entity) else {
        return;
    };
    let samples = &mut proof.samples[kind.0];
    let step = targets.completed_steps.load(Ordering::Acquire);
    if samples.len() >= 2
        || samples
            .last()
            .is_some_and(|(previous, _)| step < previous + 30)
    {
        return;
    }
    samples.push((step, forge_core::bytes_sha256(&event.data)));
    if samples.len() == 2 {
        commands.entity(event.entity).despawn();
    }
    proof.finite &= if kind.0 == 0 {
        event
            .data
            .as_chunks::<4>()
            .0
            .iter()
            .all(|value| f32::from_le_bytes(*value).is_finite())
    } else {
        event
            .data
            .as_chunks::<2>()
            .0
            .iter()
            .all(|value| u16::from_le_bytes(*value) & 0x7c00 != 0x7c00)
    };
}
