//! Controlled A/A/B depth reads after the walk, with camera and dither index held.
use bevy::{
    prelude::*,
    render::{
        gpu_readback::{Readback, ReadbackComplete},
        view::screenshot::{Screenshot, ScreenshotCaptured, save_to_disk},
    },
};
use forge_engine::tidewater_object_shadow::graph::{self, animation};
use std::sync::atomic::Ordering;

#[derive(serde::Serialize)]
pub(super) struct Sample {
    encoded_sequence: u32,
    wind: [f32; 4],
    gust_frame: [f32; 4],
    camera: [f32; 3],
    hash: forge_core::ContentHash,
}

#[derive(Resource)]
pub(super) struct Proof {
    pub started: bool,
    pub samples: Vec<Sample>,
    pub finite: bool,
    pub changed_texels: [usize; 3],
    captures: u32,
    waiting: bool,
    settled: u32,
    baseline: Vec<u8>,
}
impl Default for Proof {
    fn default() -> Self {
        Self {
            started: false,
            samples: Vec::new(),
            finite: true,
            changed_texels: [0; 3],
            captures: 0,
            waiting: false,
            settled: 0,
            baseline: Vec::new(),
        }
    }
}
impl Proof {
    pub fn complete(&self) -> bool {
        self.samples.len() == 3 && self.captures == 2
    }
    pub fn valid(&self) -> bool {
        self.complete()
            && self.finite
            && self.same_camera_and_dither()
            && self.samples[0].hash == self.samples[1].hash
            && self.samples[0].hash != self.samples[2].hash
            && self.changed_texels.iter().sum::<usize>() > 0
    }

    fn same_camera_and_dither(&self) -> bool {
        let [a, repeated, b] = self.samples.as_slice() else {
            return false;
        };
        a.camera.map(f32::to_bits) == repeated.camera.map(f32::to_bits)
            && a.camera.map(f32::to_bits) == b.camera.map(f32::to_bits)
            && a.wind.map(f32::to_bits) == repeated.wind.map(f32::to_bits)
            && a.gust_frame.map(f32::to_bits) == repeated.gust_frame.map(f32::to_bits)
            && a.wind[..3]
                .iter()
                .zip(&b.wind[..3])
                .all(|(x, y)| x.to_bits() == y.to_bits())
            && a.gust_frame[2..]
                .iter()
                .zip(&b.gust_frame[2..])
                .all(|(x, y)| x.to_bits() == y.to_bits())
            && b.wind[3].to_bits() == (a.wind[3] + 1.0).to_bits()
            && (0..2).all(|axis| {
                b.gust_frame[axis].to_bits()
                    == (a.gust_frame[axis] + a.wind[axis] * (0.7 * a.wind[2] + 1.5)).to_bits()
            })
    }
}

pub(super) fn install(app: &mut App) {
    app.init_resource::<Proof>().add_systems(Update, request);
}

#[allow(clippy::too_many_arguments)]
fn request(
    mut commands: Commands,
    smoke: Res<super::Smoke>,
    probe: Res<crate::Probe>,
    target: Res<graph::Target>,
    animation: Res<animation::State>,
    wind: Res<crate::walk_wind::Receipt>,
    mut held: ResMut<crate::walk_wind::HeldPose>,
    mut proof: ResMut<Proof>,
) {
    if proof.complete() {
        held.0 = None;
        return;
    }
    if smoke.ticks < smoke.minimum_ticks
        || !smoke.route.complete()
        || proof.samples.len() == 3
        || proof.waiting
    {
        return;
    }
    if !proof.started {
        proof.started = true;
        held.0 = Some(animation::Motion {
            main_camera: Vec3::from_array(wind.camera).extend(0.0),
            wind_time: Vec4::from_array(wind.wind),
            gust_frame: Vec4::from_array(wind.gust_frame),
        });
    }
    let pose = held.0.expect("controlled pose");
    if wind.wind.map(f32::to_bits) != pose.wind_time.to_array().map(f32::to_bits)
        || wind.gust_frame.map(f32::to_bits) != pose.gust_frame.to_array().map(f32::to_bits)
        || animation.encoded.load(Ordering::Acquire) != wind.sequence
    {
        proof.settled = 0;
        return;
    }
    // Submission acknowledgement is not a GPU fence. Keep the pose held through
    // the actual asynchronous readback, so every intervening queue write agrees.
    proof.settled += 1;
    if proof.settled < 3 {
        return;
    }
    proof.waiting = true;
    if matches!(proof.samples.len(), 0 | 2) {
        let suffix = if proof.samples.is_empty() {
            "wind-a.png"
        } else {
            "wind-b.png"
        };
        commands
            .spawn(Screenshot::primary_window())
            .observe(save_to_disk(probe.screenshot.with_extension(suffix)))
            .observe(|_: On<ScreenshotCaptured>, mut proof: ResMut<Proof>| {
                proof.captures += 1;
            });
    }
    commands
        .spawn(Readback::texture(target.image.clone()))
        .observe(receive);
}

fn receive(
    event: On<ReadbackComplete>,
    mut commands: Commands,
    animation: Res<animation::State>,
    wind: Res<crate::walk_wind::Receipt>,
    mut held: ResMut<crate::walk_wind::HeldPose>,
    mut proof: ResMut<Proof>,
) {
    // A readback component repeats until removed; accept exactly one callback per request.
    if !proof.waiting {
        return;
    }
    commands.entity(event.entity).despawn();
    proof.waiting = false;
    proof.settled = 0;
    let bytes = &event.data;
    proof.finite &= bytes.len() == 2048 * 2048 * 3 * 4
        && bytes
            .as_chunks::<4>()
            .0
            .iter()
            .all(|v| (0.0..=1.0).contains(&f32::from_le_bytes(*v)));
    if proof.samples.is_empty() {
        proof.baseline.clone_from(bytes);
    } else if proof.samples.len() == 2 {
        let mut changes = [0_usize; 3];
        for (index, (a, b)) in proof
            .baseline
            .as_chunks::<4>()
            .0
            .iter()
            .zip(bytes.as_chunks::<4>().0)
            .enumerate()
        {
            if a != b {
                changes[index / (2048 * 2048)] += 1;
            }
        }
        proof.changed_texels = changes;
        proof.baseline = Vec::new();
    }
    proof.samples.push(Sample {
        encoded_sequence: animation.encoded.load(Ordering::Acquire),
        wind: wind.wind,
        gust_frame: wind.gust_frame,
        camera: wind.camera,
        hash: forge_core::bytes_sha256(bytes),
    });
    if proof.samples.len() == 2 {
        let pose = held.0.as_mut().expect("held A pose");
        // Exactly one second of source wind. Keep the camera and Bayer frame fixed.
        pose.wind_time.w += 1.0;
        let speed = 0.7 * pose.wind_time.z + 1.5;
        pose.gust_frame.x += pose.wind_time.x * speed;
        pose.gust_frame.y += pose.wind_time.y * speed;
    }
}
