//! Read the resident grass buffers after the controller has stopped at the last view.
use bevy::{
    prelude::*,
    render::{
        gpu_readback::{Readback, ReadbackComplete},
        storage::ShaderBuffer,
    },
};

#[derive(serde::Serialize)]
pub(super) struct Sample {
    level: usize,
    expected: forge_core::ContentHash,
    actual: forge_core::ContentHash,
    bytes: usize,
    matches: bool,
}

#[derive(Resource, Default)]
pub(super) struct Proof {
    started: bool,
    pub samples: Vec<Sample>,
}

impl Proof {
    pub fn complete(&self) -> bool {
        self.samples.len() == 3
    }
    pub fn valid(&self) -> bool {
        self.complete() && self.samples.iter().all(|s| s.matches)
    }
}

#[derive(Component)]
struct Expected {
    level: usize,
    hash: forge_core::ContentHash,
    bytes: usize,
}

pub(super) fn install(app: &mut App) {
    app.init_resource::<Proof>().add_systems(Update, request);
}

fn request(
    mut commands: Commands,
    wind: Res<super::wind::Proof>,
    grass: Res<crate::walk_grass::Live>,
    buffers: Res<Assets<ShaderBuffer>>,
    mut proof: ResMut<Proof>,
) {
    if proof.started || !wind.complete() {
        return;
    }
    proof.started = true;
    for (level, handle) in grass.buffers().into_iter().enumerate() {
        let data = buffers
            .get(&handle)
            .and_then(|b| b.data.as_deref())
            .expect("resident grass data");
        commands
            .spawn((
                Readback::buffer(handle),
                Expected {
                    level,
                    hash: forge_core::bytes_sha256(data),
                    bytes: data.len(),
                },
            ))
            .observe(receive);
    }
}

fn receive(
    event: On<ReadbackComplete>,
    expected: Query<&Expected>,
    mut commands: Commands,
    mut proof: ResMut<Proof>,
) {
    let Ok(expected) = expected.get(event.entity) else {
        return;
    };
    commands.entity(event.entity).despawn();
    if proof.samples.iter().any(|s| s.level == expected.level) {
        return;
    }
    let actual = forge_core::bytes_sha256(&event.data);
    proof.samples.push(Sample {
        level: expected.level,
        expected: expected.hash,
        actual,
        bytes: event.data.len(),
        matches: actual == expected.hash && event.data.len() == expected.bytes,
    });
    proof.samples.sort_by_key(|s| s.level);
}
