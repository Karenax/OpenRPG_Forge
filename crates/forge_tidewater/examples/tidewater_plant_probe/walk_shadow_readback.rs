//! Freeze wind, step four camera updates, and read the actual depth/receiver pair.
use crate::walk_wind::{CascadeControl, Receipt};
use bevy::{
    prelude::*,
    render::gpu_readback::{Readback, ReadbackComplete},
};
use std::sync::atomic::Ordering;
use {
    forge_engine::ForgePlayerCamera,
    forge_tidewater::{
        sun_cascades::CascadeView,
        tidewater_object_shadow::{
            graph::{self, animation},
            receiver,
        },
    },
};

#[derive(Clone, serde::Serialize)]
pub(super) struct Sample {
    pub update: u64,
    pub layers: Vec<usize>,
    pub camera: [f32; 3],
    pub wind: [f32; 4],
    pub gust: [f32; 4],
    pub depth: [forge_core::ContentHash; 3],
    pub receiver: forge_core::ContentHash,
    pub receiver_matches: bool,
    pub trace: [u32; 4],
    pub trace_matches: bool,
    pub matrices: [[f32; 16]; 3],
    pub eyes: [[f32; 4]; 3],
    pub terrain_nodes: u32,
    pub plants: Vec<u32>,
}
#[derive(Resource)]
pub(super) struct Proof {
    pub samples: Vec<Sample>,
    pub finite: bool,
    warm_remaining: u32,
    expected_sequence: u32,
    need_advance: bool,
    waiting: bool,
    depth: Option<Vec<u8>>,
    receiver: Option<Vec<u8>>,
    trace: Option<Vec<u8>>,
    view_a: Option<CascadeView>,
    restored_updates: u64,
    restored_start: Option<u64>,
}
impl Default for Proof {
    fn default() -> Self {
        Self {
            samples: vec![],
            finite: true,
            warm_remaining: 4,
            expected_sequence: 0,
            need_advance: false,
            waiting: false,
            depth: None,
            receiver: None,
            trace: None,
            view_a: None,
            restored_updates: 0,
            restored_start: None,
        }
    }
}
impl Proof {
    pub fn complete(&self) -> bool {
        self.samples.len() == 5 && self.restored_updates >= 4
    }
    pub fn valid(&self) -> bool {
        if !self.complete()
            || !self.finite
            || self
                .samples
                .iter()
                .any(|s| !s.receiver_matches || !s.trace_matches)
        {
            return false;
        }
        let baseline = &self.samples[0];
        let mut followed = [false; 3];
        for pair in self.samples.windows(2) {
            let (a, b) = (&pair[0], &pair[1]);
            let expected: Vec<_> = (0..3)
                .filter(|&i| (b.update + i as u64).is_multiple_of(1_u64 << i))
                .collect();
            if b.update != a.update + 1
                || b.trace[0] != a.trace[0] + 1
                || b.trace[2] != a.trace[2] + 1
                || b.trace[3] != a.trace[3]
                || b.layers != expected
                || b.wind.map(f32::to_bits) != baseline.wind.map(f32::to_bits)
                || b.gust.map(f32::to_bits) != baseline.gust.map(f32::to_bits)
                || (Vec3::from_array(b.camera) - Vec3::from_array(baseline.camera)).length() < 50.0
            {
                return false;
            }
            for (i, seen) in followed.iter_mut().enumerate() {
                if b.layers.contains(&i) {
                    *seen |= b.matrices[i].map(f32::to_bits)
                        != baseline.matrices[i].map(f32::to_bits)
                        && b.eyes[i].map(f32::to_bits) != baseline.eyes[i].map(f32::to_bits)
                        && b.depth[i] != baseline.depth[i];
                } else if a.matrices[i].map(f32::to_bits) != b.matrices[i].map(f32::to_bits)
                    || a.eyes[i].map(f32::to_bits) != b.eyes[i].map(f32::to_bits)
                    || a.depth[i] != b.depth[i]
                {
                    return false;
                }
            }
        }
        followed.into_iter().all(|v| v)
    }
}
#[derive(Component)]
struct Kind {
    buffer: BufferKind,
    sequence: u32,
}
#[derive(Clone, Copy)]
enum BufferKind {
    Depth,
    Receiver,
    Trace,
}

pub(super) fn install(app: &mut App) {
    app.init_resource::<Proof>().add_systems(Update, request);
}

#[allow(clippy::too_many_arguments)]
fn request(
    mut commands: Commands,
    target: Res<graph::Target>,
    wind: Res<Receipt>,
    wind_proof: Res<super::wind::Proof>,
    animation: Res<animation::State>,
    cameras: Query<(&GlobalTransform, &Projection), With<ForgePlayerCamera>>,
    mut control: ResMut<CascadeControl>,
    mut proof: ResMut<Proof>,
) {
    if proof.complete() {
        *control = default();
        return;
    }
    // Restore the real camera through a complete cadence before the final PNG.
    if proof.samples.len() == 5 {
        if control.view.is_some() {
            *control = default();
            proof.restored_start = Some(animation.statistics().submissions);
        } else {
            proof.restored_updates = animation
                .statistics()
                .submissions
                .saturating_sub(proof.restored_start.expect("restoration started"));
        }
        return;
    }
    if !wind_proof.complete() || proof.waiting {
        return;
    }
    if proof.view_a.is_none() {
        let Ok((camera, Projection::Perspective(p))) = cameras.single() else {
            return;
        };
        let view = CascadeView {
            world_from_view: camera.to_matrix().as_dmat4(),
            near: f64::from(p.near),
            fov_degrees: f64::from(p.fov).to_degrees(),
            aspect: f64::from(p.aspect_ratio),
        };
        proof.view_a = Some(view);
        control.view = Some(view);
        control.motion = Some(animation::Motion {
            main_camera: Vec3::from_array(wind.camera).extend(0.0),
            wind_time: Vec4::from_array(wind.wind),
            gust_frame: Vec4::from_array(wind.gust_frame),
        });
        control.advance = true;
        proof.expected_sequence = wind.sequence + 1;
        return;
    }
    if proof.need_advance {
        let mut view = proof.view_a.expect("baseline view");
        view.world_from_view.w_axis.x += 48.0;
        view.world_from_view.w_axis.z += 32.0;
        control.view = Some(view);
        control.advance = true;
        proof.expected_sequence = wind.sequence + 1;
        proof.need_advance = false;
        return;
    }
    if control.advance
        || wind.sequence != proof.expected_sequence
        || animation.encoded.load(Ordering::Acquire) != wind.sequence
    {
        return;
    }
    if proof.warm_remaining > 0 {
        proof.warm_remaining -= 1;
        if proof.warm_remaining > 0 {
            control.advance = true;
            proof.expected_sequence = wind.sequence + 1;
            return;
        }
    }
    proof.waiting = true;
    commands
        .spawn((
            Readback::texture(target.image.clone()),
            Kind {
                buffer: BufferKind::Depth,
                sequence: wind.sequence,
            },
        ))
        .observe(receive);
    commands
        .spawn((
            Readback::buffer(receiver::DEFAULT_BUFFER.clone()),
            Kind {
                buffer: BufferKind::Receiver,
                sequence: wind.sequence,
            },
        ))
        .observe(receive);
    commands
        .spawn((
            Readback::buffer(target.trace.clone()),
            Kind {
                buffer: BufferKind::Trace,
                sequence: wind.sequence,
            },
        ))
        .observe(receive);
}

fn receive(
    event: On<ReadbackComplete>,
    kinds: Query<&Kind>,
    mut commands: Commands,
    wind: Res<Receipt>,
    mut proof: ResMut<Proof>,
) {
    let Ok(kind) = kinds.get(event.entity) else {
        return;
    };
    commands.entity(event.entity).despawn();
    if !proof.waiting || kind.sequence != proof.expected_sequence {
        return;
    }
    match kind.buffer {
        BufferKind::Receiver => {
            if proof.receiver.is_some() {
                return;
            }
            proof.finite &= event.data.len() == 464;
            proof.receiver = Some(event.data.clone());
        }
        BufferKind::Depth => {
            if proof.depth.is_some() {
                return;
            }
            proof.finite &= event.data.len() == 2048 * 2048 * 3 * 4
                && event
                    .data
                    .as_chunks::<4>()
                    .0
                    .iter()
                    .all(|v| (0.0..=1.0).contains(&f32::from_le_bytes(*v)));
            proof.depth = Some(event.data.clone());
        }
        BufferKind::Trace => {
            if proof.trace.is_some() {
                return;
            }
            proof.finite &= event.data.len() == 16;
            proof.trace = Some(event.data.clone());
        }
    }
    // Do not take one buffer before its partner arrives: callbacks are independent.
    if proof.depth.is_none() || proof.receiver.is_none() || proof.trace.is_none() {
        return;
    }
    let depth = proof.depth.take().expect("depth ready");
    let receiver = proof.receiver.take().expect("receiver ready");
    let trace = proof.trace.take().expect("trace ready");
    let trace: [u32; 4] = trace
        .as_chunks::<4>()
        .0
        .iter()
        .map(|v| u32::from_le_bytes(*v))
        .collect::<Vec<_>>()
        .try_into()
        .unwrap_or([0; 4]);
    let expected_mask = wind
        .shadow_layers
        .iter()
        .fold(0, |mask, layer| mask | (1 << layer));
    proof.waiting = false;
    proof.need_advance = true;
    let side = 2048 * 2048 * 4;
    let hashes = std::array::from_fn(|i| {
        forge_core::bytes_sha256(depth.get(i * side..(i + 1) * side).unwrap_or(&[]))
    });
    let expected_matrices: Vec<_> = wind
        .shadow_matrices
        .iter()
        .flatten()
        .flat_map(|v| v.to_le_bytes())
        .collect();
    let expected_camera: Vec<_> = wind
        .camera
        .into_iter()
        .chain([0.0])
        .flat_map(f32::to_le_bytes)
        .collect();
    let matches = receiver.get(..192) == Some(expected_matrices.as_slice())
        && receiver.get(416..432) == Some(expected_camera.as_slice())
        && receiver.get(448..452) == Some(1_f32.to_le_bytes().as_slice());
    proof.samples.push(Sample {
        update: wind.shadow_update,
        layers: wind.shadow_layers.clone(),
        camera: wind.camera,
        wind: wind.wind,
        gust: wind.gust_frame,
        depth: hashes,
        receiver: forge_core::bytes_sha256(&receiver),
        receiver_matches: matches,
        trace,
        trace_matches: trace[0] == wind.sequence && trace[1] == expected_mask,
        matrices: wind.shadow_matrices,
        eyes: wind.shadow_eyes,
        terrain_nodes: wind.terrain_nodes,
        plants: wind.plant_counts.clone(),
    });
}
