//! Asynchronous latest-head feedback with the simulation step copied on the GPU.
//! The first 120 acquisition steps keep their separate, strictly gated protocol.
use super::super::ocean::live_clock::LiveStep;
use super::{BudgetFeedback, Targets};
use bevy::{
    prelude::*,
    render::{
        RenderApp,
        gpu_readback::{Readback, ReadbackComplete},
        storage::{GpuShaderBuffer, ShaderBuffer},
    },
};
use std::sync::{Arc, Mutex, atomic::Ordering};
use wgpu::util::DeviceExt;

#[derive(Clone, Default, serde::Serialize)]
pub struct Statistics {
    pub encoded_steps: u64,
    pub delayed_feedback_steps: u64,
    pub accepted_samples: u64,
    pub latest_sample_step: u32,
    pub latest_encoded_step: u32,
    pub latest_encoded_time: f64,
    pub latest_sample_time: f64,
}

#[derive(Resource, Clone)]
pub struct LiveFeedback {
    pub(super) packet: Handle<ShaderBuffer>,
    budget: Arc<Mutex<BudgetFeedback>>,
    statistics: Arc<Mutex<Statistics>>,
}

pub(super) fn install(app: &mut App) {
    let budget = app
        .world()
        .resource::<Targets>()
        .budget
        .clone()
        .expect("validated native budget");
    let mut buffer = ShaderBuffer::from(vec![0_u32; 4]);
    buffer.buffer_description.usage |= wgpu::BufferUsages::COPY_SRC | wgpu::BufferUsages::COPY_DST;
    let packet = app
        .world_mut()
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(buffer);
    let resource = LiveFeedback {
        packet,
        budget,
        statistics: default(),
    };
    app.insert_resource(resource.clone())
        .add_systems(Update, request);
    app.sub_app_mut(RenderApp).insert_resource(resource);
}

impl LiveFeedback {
    pub(super) fn encode(
        &self,
        device: &wgpu::Device,
        encoder: &mut wgpu::CommandEncoder,
        packet: &GpuShaderBuffer,
        head: &wgpu::Buffer,
        frame: LiveStep,
        latest: Option<u32>,
    ) {
        // Each encoded step gets its own immutable COPY_SRC. Queue writes to a
        // shared stamp could label an older counter with a newer step.
        let mut stamp = [0_u8; 12];
        stamp[..4].copy_from_slice(&frame.step.to_le_bytes());
        stamp[4..].copy_from_slice(&frame.time.to_le_bytes());
        let stamp = device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
            label: Some("live spray feedback step and time"),
            contents: &stamp,
            usage: wgpu::BufferUsages::COPY_SRC,
        });
        encoder.copy_buffer_to_buffer(head, 0, &packet.buffer, 0, 4);
        encoder.copy_buffer_to_buffer(&stamp, 0, &packet.buffer, 4, 12);
        let mut stats = self.statistics.lock().expect("feedback statistics mutex");
        stats.encoded_steps += 1;
        stats.delayed_feedback_steps += u64::from(latest.is_none_or(|old| old < frame.step - 1));
        stats.latest_encoded_step = frame.step;
        stats.latest_encoded_time = frame.time;
    }

    #[allow(dead_code)] // Exposed to the native walk receipt, not the standalone probe.
    pub fn statistics(&self) -> Statistics {
        self.statistics
            .lock()
            .expect("feedback statistics mutex")
            .clone()
    }
}

fn request(
    mut commands: Commands,
    targets: Res<Targets>,
    feedback: Res<LiveFeedback>,
    ocean: Res<super::super::ocean::Targets>,
    mut started: Local<bool>,
    queries: Option<Res<super::water_queries::Targets>>,
) {
    if *started
        || !targets.readback_complete.load(Ordering::Acquire)
        || queries.is_some_and(|q| !q.bootstrap_ready.load(Ordering::Acquire))
    {
        return;
    }
    *started = true;
    commands
        .spawn(Readback::buffer(feedback.packet.clone()))
        .observe(receive);
    // No live step can overwrite frame 120 until every initial readback is saved.
    ocean.permitted_step.store(121, Ordering::Release);
}

fn receive(
    event: On<ReadbackComplete>,
    feedback: Res<LiveFeedback>,
    mut exit: MessageWriter<bevy::app::AppExit>,
) {
    let Ok(bytes) = <[u8; 16]>::try_from(event.data.as_slice()) else {
        error!("Invalid live spray feedback packet size");
        exit.write(bevy::app::AppExit::error());
        return;
    };
    let head = u32::from_le_bytes(bytes[..4].try_into().expect("packet head"));
    let step = u32::from_le_bytes(bytes[4..8].try_into().expect("packet step"));
    let time = f64::from_le_bytes(bytes[8..].try_into().expect("packet time"));
    // Initial zero packets and duplicate/out-of-order asynchronous completions
    // are expected. Only strictly newer tagged samples replace the latest head.
    let mut budget = feedback.budget.lock().expect("budget mutex");
    if step <= 120 || budget.latest.is_some_and(|old| step <= old.0) {
        return;
    }
    if !time.is_finite() || time <= 0.0 || time > budget.encoded_time {
        error!(
            step,
            time,
            encoded_time = budget.encoded_time,
            "Invalid live spray feedback time"
        );
        exit.write(bevy::app::AppExit::error());
        return;
    }
    if step > budget.encoded_step {
        error!(
            step,
            encoded = budget.encoded_step,
            "Future live spray feedback packet"
        );
        exit.write(bevy::app::AppExit::error());
        return;
    }
    budget.latest = Some((
        step,
        forge_tidewater::spray_budget::HeadSample { time, head },
    ));
    let mut stats = feedback
        .statistics
        .lock()
        .expect("feedback statistics mutex");
    stats.accepted_samples += 1;
    stats.latest_sample_step = step;
    stats.latest_sample_time = time;
}
