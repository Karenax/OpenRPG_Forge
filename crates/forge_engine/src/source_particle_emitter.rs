//! Owner-local source clock and ordered resident GPU phases. The caller admits
//! the program and lowers typed commands to its explicit source bank ABI.
use super::{
    SOURCE_PARTICLE_CURVE_BANK_BYTES, SourceParticleComputeQueue, SourceParticleComputeSequence,
};
use bevy::{prelude::Handle, render::storage::ShaderBuffer};
use forge_assets::source_particle_emission::{
    SourceEmissionAdvance, SourceEmissionEvent, SourceEmissionSnapshot, SourceSingleBurstLoop,
    SourceSingleBurstLoopParameters,
};

#[derive(Clone, Copy, Debug, PartialEq)]
pub enum SourceParticleEmissionCommand {
    Reset,
    Allocate { count: f32 },
    Update { delta_seconds: f32 },
}

/// One caller-owned compute slot is one emitter owner. Requests from other
/// owners have separate clocks and pending groups. No particle state reaches CPU.
pub struct SourceParticleEmitterSequence {
    compute: SourceParticleComputeSequence,
    clock: SourceSingleBurstLoop,
    pending: Option<(SourceSingleBurstLoop, SourceEmissionAdvance)>,
    last_frame: Option<SourceEmissionAdvance>,
}

impl SourceParticleEmitterSequence {
    pub fn new(
        compute: SourceParticleComputeSequence,
        parameters: SourceSingleBurstLoopParameters,
    ) -> Result<Self, &'static str> {
        Ok(Self {
            compute,
            clock: SourceSingleBurstLoop::new(parameters)?,
            pending: None,
            last_frame: None,
        })
    }

    /// Commit the owner clock only when every phase was encoded. This is not a
    /// GPU fence; CPU evidence still needs readback and material use stays on GPU.
    pub fn poll(&mut self) -> Result<bool, String> {
        if !self.compute.poll()? {
            return Ok(false);
        }
        if let Some((clock, frame)) = self.pending.take() {
            self.clock = clock;
            self.last_frame = Some(frame);
        }
        Ok(true)
    }

    /// False preserves all unsent input: the caller retains delta/events/reset.
    /// A bank-lowering error also leaves the committed clock and queue unchanged.
    pub fn submit_frame(
        &mut self,
        queue: &mut SourceParticleComputeQueue,
        delta_seconds: f32,
        events: &[SourceEmissionEvent],
        reinitialize: bool,
        mut lower: impl FnMut(
            SourceParticleEmissionCommand,
        ) -> Result<[u8; SOURCE_PARTICLE_CURVE_BANK_BYTES], String>,
    ) -> Result<bool, String> {
        if !self.poll()? {
            return Ok(false);
        }
        let mut candidate = self.clock.clone();
        if reinitialize {
            candidate.reinitialize();
        }
        for event in events {
            candidate.queue_event(*event);
        }
        let frame = candidate.advance(delta_seconds).map_err(str::to_owned)?;
        let mut banks = Vec::with_capacity(3);
        if frame.reset_particles {
            banks.push(lower(SourceParticleEmissionCommand::Reset)?);
        }
        if frame.spawn_request > 0.0 {
            banks.push(lower(SourceParticleEmissionCommand::Allocate {
                count: frame.spawn_request,
            })?);
        }
        banks.push(lower(SourceParticleEmissionCommand::Update {
            delta_seconds: frame.delta_seconds,
        })?);
        if !self.compute.submit_ordered(queue, banks)? {
            return Ok(false);
        }
        self.pending = Some((candidate, frame));
        Ok(true)
    }

    #[must_use]
    pub const fn snapshot(&self) -> SourceEmissionSnapshot {
        self.clock.snapshot()
    }

    #[must_use]
    pub const fn last_frame(&self) -> Option<SourceEmissionAdvance> {
        self.last_frame
    }

    #[must_use]
    pub fn current_buffer(&self) -> Handle<ShaderBuffer> {
        self.compute.current_buffer()
    }

    #[must_use]
    pub const fn encoded_steps(&self) -> u64 {
        self.compute.encoded_steps()
    }

    #[must_use]
    pub const fn slot(&self) -> u64 {
        self.compute.slot()
    }
}

#[cfg(test)]
mod tests {
    use super::super::SourceParticleComputeStatus;
    use super::*;
    use bevy::prelude::*;

    fn emitter(slot: u64, assets: &mut Assets<ShaderBuffer>) -> SourceParticleEmitterSequence {
        let buffers =
            std::array::from_fn(|_| assets.add(ShaderBuffer::new(&[0_u8; 16], default())));
        let compute = SourceParticleComputeSequence::new(
            slot,
            Handle::default(),
            "advance".into(),
            Handle::default(),
            buffers,
            1,
        )
        .unwrap();
        SourceParticleEmitterSequence::new(
            compute,
            SourceSingleBurstLoopParameters {
                loop_duration_seconds: 3.0,
                loop_count: 1,
                burst_delay_seconds: 0.0,
                burst_count: 1.0,
                burst_active: true,
                initial_play: true,
                prewarm_steps: 0,
            },
        )
        .unwrap()
    }

    fn bank(command: SourceParticleEmissionCommand) -> Result<[u8; 1024], String> {
        if let SourceParticleEmissionCommand::Allocate { count } = command
            && count.to_bits() != 1.0_f32.to_bits()
        {
            return Err("Test bank admits Count1 only".into());
        }
        Ok([0; 1024])
    }

    fn encode(queue: &SourceParticleComputeQueue, slot: u64) {
        for request in &queue.0[&slot] {
            request.set_status(SourceParticleComputeStatus::Encoded);
        }
    }

    #[test]
    fn owner_clocks_commit_only_their_complete_groups_and_keep_unsent_events() {
        let mut assets = Assets::<ShaderBuffer>::default();
        let mut a = emitter(7, &mut assets);
        let mut b = emitter(8, &mut assets);
        assert_ne!(a.current_buffer(), b.current_buffer());
        let mut queue = SourceParticleComputeQueue::default();
        assert!(a.submit_frame(&mut queue, 0.125, &[], false, bank).unwrap());
        assert!(b.submit_frame(&mut queue, 0.25, &[], false, bank).unwrap());
        assert_eq!(a.snapshot().effect_time_seconds.to_bits(), 0);
        assert_eq!(b.snapshot().effect_time_seconds.to_bits(), 0);
        queue.0[&7][0].set_status(SourceParticleComputeStatus::Encoded);
        assert!(!a.poll().unwrap());
        assert!(
            !a.submit_frame(&mut queue, 0.5, &[SourceEmissionEvent::Stop], false, bank)
                .unwrap()
        );
        encode(&queue, 8);
        assert!(b.poll().unwrap());
        assert_eq!(
            b.snapshot().effect_time_seconds.to_bits(),
            0.25_f32.to_bits()
        );
        assert_eq!(a.snapshot().effect_time_seconds.to_bits(), 0);
        encode(&queue, 7);
        assert!(a.poll().unwrap());
        assert!(
            a.submit_frame(&mut queue, 0.5, &[SourceEmissionEvent::Stop], false, bank)
                .unwrap()
        );
        encode(&queue, 7);
        assert!(a.poll().unwrap());
        assert!(!a.snapshot().playing());
        assert_eq!(
            a.snapshot().effect_time_seconds.to_bits(),
            0.625_f32.to_bits()
        );
        assert!(b.snapshot().playing());
    }

    #[test]
    fn lowering_and_shader_failures_preserve_the_last_committed_clock() {
        let mut owner = emitter(7, &mut Assets::<ShaderBuffer>::default());
        let mut queue = SourceParticleComputeQueue::default();
        assert!(
            owner
                .submit_frame(&mut queue, 0.125, &[], false, |_| Err(
                    "unsupported bank ABI".into()
                ))
                .is_err()
        );
        assert!(queue.0.is_empty());
        assert_eq!(owner.snapshot().effect_time_seconds.to_bits(), 0);
        assert!(
            owner
                .submit_frame(&mut queue, 0.125, &[], false, bank)
                .unwrap()
        );
        encode(&queue, 7);
        assert!(owner.poll().unwrap());
        let before = owner.snapshot();
        assert!(
            owner
                .submit_frame(&mut queue, 0.25, &[SourceEmissionEvent::Play], false, bank)
                .unwrap()
        );
        queue.0[&7][1].set_status(SourceParticleComputeStatus::Failed("failed phase".into()));
        assert!(owner.poll().is_err());
        assert_eq!(owner.snapshot(), before);
    }
}
