//! GPU evidence that both persistent coupled states change after step 120.
use bevy::{
    prelude::*,
    render::gpu_readback::{Readback, ReadbackComplete},
};
use std::sync::atomic::Ordering;

#[derive(Resource)]
pub(super) struct Proof {
    pub samples: [Vec<(u32, forge_core::ContentHash)>; 2],
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
        self.samples.iter().all(|s| s.len() >= 2)
    }
    pub fn changed(&self) -> bool {
        self.finite && self.complete() && self.samples.iter().all(|s| s[0].1 != s[1].1)
    }
}
#[derive(Component)]
struct Kind(usize);

pub(super) fn install(app: &mut App) {
    app.init_resource::<Proof>().add_systems(Update, request);
}

fn request(
    mut commands: Commands,
    target: Res<crate::spray::Targets>,
    smoke: Res<super::Smoke>,
    mut started: Local<bool>,
) {
    if *started || target.completed_steps.load(Ordering::Acquire) < smoke.sample_step {
        return;
    }
    *started = true;
    commands
        .spawn((
            Readback::texture(target.shore_state.clone().expect("coupled shore")),
            Kind(0),
        ))
        .observe(receive);
    commands
        .spawn((Readback::buffer(target.buffers[1].clone()), Kind(1)))
        .observe(receive);
}

fn receive(
    event: On<ReadbackComplete>,
    kinds: Query<&Kind>,
    target: Res<crate::spray::Targets>,
    mut proof: ResMut<Proof>,
    mut commands: Commands,
) {
    let Ok(kind) = kinds.get(event.entity) else {
        return;
    };
    let samples = &mut proof.samples[kind.0];
    let step = target.completed_steps.load(Ordering::Acquire);
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
            .as_chunks::<2>()
            .0
            .iter()
            .all(|v| u16::from_le_bytes(*v) & 0x7c00 != 0x7c00)
    } else {
        event
            .data
            .as_chunks::<4>()
            .0
            .iter()
            .all(|v| f32::from_le_bytes(*v).is_finite())
    };
}
