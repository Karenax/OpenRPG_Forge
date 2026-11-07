// SPDX-License-Identifier: MIT
// Adapted from Tidewater Breakers.js and Spray.js, copyright 2026 DRG Software Solutions LLC.
// Revision 1438b1abfcaee3267092b75573014f4d9b4a983c; see third_party/tidewater/LICENSE.
//! Shared ordered GPU-head feedback and adaptive spray budget.
use crate::{
    source_ocean::live_clock::LiveStep,
    spray_budget::{HeadSample, SequencedHeadSample, SprayBudget},
};
use bevy::{
    prelude::*,
    render::{
        RenderApp,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        gpu_readback::{Readback, ReadbackComplete},
        storage::{GpuShaderBuffer, ShaderBuffer},
    },
};
use std::{
    collections::BTreeMap,
    sync::{Arc, Mutex},
};
use wgpu::util::DeviceExt;

/// One simulation generation's adaptive budget and tagged GPU samples.
pub struct BudgetFeedback {
    controller: SprayBudget,
    latest: Option<(u32, HeadSample)>,
    completed: BTreeMap<u32, HeadSample>,
    encoded_step: u32,
    encoded_time: f64,
    requested_step: u32,
    rows: Vec<serde_json::Value>,
}

impl BudgetFeedback {
    pub fn new(capacity: u32) -> Result<Self, &'static str> {
        Ok(Self {
            controller: SprayBudget::new(capacity)?,
            latest: None,
            completed: BTreeMap::new(),
            encoded_step: 0,
            encoded_time: 0.0,
            requested_step: 0,
            rows: Vec::new(),
        })
    }
    #[must_use]
    pub fn value(&self) -> f64 {
        self.controller.value()
    }
    #[must_use]
    pub const fn latest(&self) -> Option<(u32, HeadSample)> {
        self.latest
    }
    #[must_use]
    pub fn completed(&self, step: u32) -> Option<HeadSample> {
        self.completed.get(&step).copied()
    }
    #[must_use]
    pub const fn encoded_step(&self) -> u32 {
        self.encoded_step
    }
    #[must_use]
    pub const fn encoded_time(&self) -> f64 {
        self.encoded_time
    }
    #[must_use]
    pub fn rows(&self) -> &[serde_json::Value] {
        &self.rows
    }
    pub fn push_row(&mut self, row: serde_json::Value) {
        self.rows.push(row);
    }
    pub fn record_encoded(&mut self, step: u32, time: f64) {
        self.encoded_step = step;
        self.encoded_time = time;
    }
    /// Consume the latest completed sample under the source acquisition's order.
    pub fn after_emission(
        &mut self,
        latest: Option<(u32, HeadSample)>,
        sequenced: bool,
    ) -> Result<(), &'static str> {
        if sequenced {
            self.controller
                .after_sequenced_emission(latest.map(|(step, sample)| SequencedHeadSample {
                    sequence: u64::from(step),
                    sample,
                }))
        } else {
            self.controller
                .after_emission(latest.map(|(_, sample)| sample))
        }
    }
    /// Request at most one readback for each strict acquisition step.
    pub fn bootstrap_request(&mut self, bootstrap_steps: u32) -> Option<(u32, f64)> {
        let step = self.encoded_step;
        if step == 0 || step >= bootstrap_steps || step == self.requested_step {
            return None;
        }
        self.requested_step = step;
        Some((step, self.encoded_time))
    }
    /// Accept a strict bootstrap completion once; reject future or inconsistent steps.
    pub fn accept_bootstrap_sample(
        &mut self,
        step: u32,
        sample: HeadSample,
    ) -> Result<bool, &'static str> {
        if self.completed.contains_key(&step) {
            return Ok(false);
        }
        if self.encoded_step != step {
            return Err("bootstrap feedback does not match the encoded GPU step");
        }
        if self.latest.is_some_and(|value| value.0 >= step) {
            return Err("bootstrap feedback is not a new ordered sample");
        }
        self.completed.insert(step, sample);
        self.latest = Some((step, sample));
        Ok(true)
    }
    /// Ignore stale readbacks, while rejecting future steps and invalid timestamps.
    pub fn accept_live_sample(
        &mut self,
        step: u32,
        sample: HeadSample,
        bootstrap_steps: u32,
    ) -> Result<bool, &'static str> {
        if step <= bootstrap_steps || self.latest.is_some_and(|old| step <= old.0) {
            return Ok(false);
        }
        if !sample.time.is_finite() || sample.time <= 0.0 || sample.time > self.encoded_time {
            return Err("invalid live spray feedback time");
        }
        if step > self.encoded_step {
            return Err("future live spray feedback packet");
        }
        self.latest = Some((step, sample));
        Ok(true)
    }
}

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

/// Render-world packet and CPU-side recipient share the same generation state.
#[derive(Resource, Clone, ExtractResource)]
pub struct LiveFeedback {
    packet: Handle<ShaderBuffer>,
    budget: Arc<Mutex<BudgetFeedback>>,
    statistics: Arc<Mutex<Statistics>>,
    bootstrap_steps: u32,
}

impl LiveFeedback {
    /// Register the common packet in both worlds; consumers decide when to request it.
    pub fn install(
        app: &mut App,
        budget: Arc<Mutex<BudgetFeedback>>,
        bootstrap_steps: u32,
    ) -> Result<(), &'static str> {
        if app.get_sub_app(RenderApp).is_none()
            || !app.world().contains_resource::<Assets<ShaderBuffer>>()
            || app.world().contains_resource::<Self>()
        {
            return Err("Spray feedback requires an exclusive active render world");
        }
        let resource = Self::stage(app.world_mut(), budget, bootstrap_steps);
        app.insert_resource(resource.clone());
        app.sub_app_mut(RenderApp).insert_resource(resource);
        app.add_plugins(ExtractResourcePlugin::<Self>::default());
        Ok(())
    }

    /// A new shore generation has its own packet and budget. Extraction swaps
    /// the whole resource; in-flight packets from the old buffer are ignored.
    pub(crate) fn replace(
        world: &mut World,
        budget: Arc<Mutex<BudgetFeedback>>,
        bootstrap_steps: u32,
    ) -> Result<(), &'static str> {
        if !world.contains_resource::<Self>() || !world.contains_resource::<Assets<ShaderBuffer>>()
        {
            return Err("Spray feedback generation is not installed");
        }
        let resource = Self::stage(world, budget, bootstrap_steps);
        world.insert_resource(resource);
        Ok(())
    }

    fn stage(world: &mut World, budget: Arc<Mutex<BudgetFeedback>>, bootstrap_steps: u32) -> Self {
        let mut buffer = ShaderBuffer::from(vec![0_u32; 4]);
        buffer.buffer_description.usage |=
            wgpu::BufferUsages::COPY_SRC | wgpu::BufferUsages::COPY_DST;
        let packet = world.resource_mut::<Assets<ShaderBuffer>>().add(buffer);
        Self {
            packet,
            budget,
            statistics: default(),
            bootstrap_steps,
        }
    }
    #[must_use]
    pub fn packet(&self) -> &Handle<ShaderBuffer> {
        &self.packet
    }
    /// Copy the head with the exact step/time encoded in this submission.
    pub fn encode(
        &self,
        device: &wgpu::Device,
        encoder: &mut wgpu::CommandEncoder,
        packet: &GpuShaderBuffer,
        head: &wgpu::Buffer,
        frame: LiveStep,
        latest: Option<u32>,
    ) {
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
        stats.delayed_feedback_steps +=
            u64::from(latest.is_none_or(|old| old < frame.step.saturating_sub(1)));
        stats.latest_encoded_step = frame.step;
        stats.latest_encoded_time = frame.time;
    }
    #[must_use]
    pub fn statistics(&self) -> Statistics {
        self.statistics
            .lock()
            .expect("feedback statistics mutex")
            .clone()
    }
}

/// Bevy observer for the shared 16-byte GPU feedback packet.
#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy observer system parameters"
)]
pub fn receive(
    event: On<ReadbackComplete>,
    feedback: Res<LiveFeedback>,
    readers: Query<&Readback>,
    mut exit: MessageWriter<bevy::app::AppExit>,
) {
    let Ok(Readback::Buffer { buffer, .. }) = readers.get(event.entity) else {
        return;
    };
    if buffer != feedback.packet() {
        return;
    }
    let Ok(bytes) = <[u8; 16]>::try_from(event.data.as_slice()) else {
        error!("Invalid live spray feedback packet size");
        exit.write(bevy::app::AppExit::error());
        return;
    };
    let head = u32::from_le_bytes(bytes[..4].try_into().expect("packet head"));
    let step = u32::from_le_bytes(bytes[4..8].try_into().expect("packet step"));
    let time = f64::from_le_bytes(bytes[8..].try_into().expect("packet time"));
    let mut budget = feedback.budget.lock().expect("budget mutex");
    match budget.accept_live_sample(step, HeadSample { time, head }, feedback.bootstrap_steps) {
        Ok(false) => {}
        Ok(true) => {
            let mut stats = feedback
                .statistics
                .lock()
                .expect("feedback statistics mutex");
            stats.accepted_samples += 1;
            stats.latest_sample_step = step;
            stats.latest_sample_time = time;
        }
        Err(error) => {
            error!(%error, step, time, encoded = budget.encoded_step(), encoded_time = budget.encoded_time(), "Live spray feedback rejected");
            exit.write(bevy::app::AppExit::error());
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn a_replaced_feedback_packet_cannot_reach_the_new_generation_budget() {
        let mut app = App::new();
        app.add_message::<bevy::app::AppExit>();
        app.init_resource::<Assets<ShaderBuffer>>();
        let old_budget = Arc::new(Mutex::new(BudgetFeedback::new(32_768).unwrap()));
        let old = LiveFeedback::stage(app.world_mut(), Arc::clone(&old_budget), 120);
        let old_reader = app
            .world_mut()
            .spawn(Readback::buffer(old.packet.clone()))
            .observe(receive)
            .id();
        app.insert_resource(old);
        let new_budget = Arc::new(Mutex::new(BudgetFeedback::new(32_768).unwrap()));
        new_budget.lock().unwrap().record_encoded(126, 2.1);
        LiveFeedback::replace(app.world_mut(), Arc::clone(&new_budget), 120).unwrap();
        let packet = app.world().resource::<LiveFeedback>().packet.clone();
        let current = app
            .world_mut()
            .spawn(Readback::buffer(packet))
            .observe(receive)
            .id();
        // Even a malformed old completion must be ignored before decoding.
        app.world_mut().trigger(ReadbackComplete {
            entity: old_reader,
            data: vec![0],
        });
        assert!(
            app.world()
                .resource::<Messages<bevy::app::AppExit>>()
                .is_empty()
        );
        let bytes = [
            7_u32.to_le_bytes().as_slice(),
            125_u32.to_le_bytes().as_slice(),
            2.0_f64.to_le_bytes().as_slice(),
        ]
        .concat();
        app.world_mut().trigger(ReadbackComplete {
            entity: current,
            data: bytes,
        });
        assert_eq!(new_budget.lock().unwrap().latest().unwrap().0, 125);
        assert!(old_budget.lock().unwrap().latest().is_none());
        app.world_mut().trigger(ReadbackComplete {
            entity: current,
            data: vec![0],
        });
        assert!(
            !app.world()
                .resource::<Messages<bevy::app::AppExit>>()
                .is_empty()
        );
    }

    #[test]
    fn live_samples_use_admitted_bootstrap_and_reject_future_or_invalid_packets() {
        let mut feedback = BudgetFeedback::new(32_768).unwrap();
        feedback.record_encoded(126, 2.1);
        let sample = HeadSample {
            time: 2.0,
            head: 42,
        };
        assert_eq!(feedback.accept_live_sample(124, sample, 124), Ok(false));
        assert_eq!(feedback.accept_live_sample(125, sample, 124), Ok(true));
        assert_eq!(
            feedback.accept_live_sample(
                125,
                HeadSample {
                    time: 2.0,
                    head: 99
                },
                124
            ),
            Ok(false)
        );
        assert!(feedback.accept_live_sample(127, sample, 124).is_err());
        assert!(
            feedback
                .accept_live_sample(
                    126,
                    HeadSample {
                        time: f64::NAN,
                        head: 9
                    },
                    124
                )
                .is_err()
        );
        assert!(
            feedback
                .accept_live_sample(126, HeadSample { time: 2.2, head: 9 }, 124)
                .is_err()
        );
        assert_eq!(feedback.latest().unwrap().1.head, 42);
        assert_eq!(
            feedback.accept_live_sample(
                126,
                HeadSample {
                    time: 2.1,
                    head: 44
                },
                124
            ),
            Ok(true)
        );
        assert_eq!(feedback.latest().unwrap().1.head, 44);
    }
}
