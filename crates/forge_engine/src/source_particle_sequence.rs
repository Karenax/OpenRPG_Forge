//! Ordered GPU-resident state; no particle values are copied back to the CPU.
use super::{
    SOURCE_PARTICLE_CURVE_BANK_BYTES, SourceParticleComputeQueue, SourceParticleComputeRequest,
    SourceParticleComputeStatus,
};
use bevy::{prelude::*, render::storage::ShaderBuffer};

/// Transient state for a caller-owned compute slot and two distinct buffers.
/// The caller supplies its actual source timestep/parameters in each bank.
/// Only a successfully encoded step changes the published buffer. Subsequent
/// GPU passes rely on the shared renderer's ordered submissions, not CPU fences.
pub struct SourceParticleComputeSequence {
    slot: u64,
    shader: Handle<Shader>,
    entry_point: String,
    curve: Handle<Image>,
    buffers: [Handle<ShaderBuffer>; 2],
    workgroups: u32,
    current: usize,
    encoded_steps: u64,
    pending: Option<Vec<SourceParticleComputeRequest>>,
    publication: Option<(Handle<ShaderBuffer>, u64)>,
}

impl SourceParticleComputeSequence {
    pub fn new(
        slot: u64,
        shader: Handle<Shader>,
        entry_point: String,
        curve: Handle<Image>,
        buffers: [Handle<ShaderBuffer>; 2],
        workgroups: u32,
    ) -> Result<Self, &'static str> {
        // Validate the ABI and alias guard before taking ownership of a slot.
        SourceParticleComputeRequest::new(
            shader.clone(),
            entry_point.clone(),
            curve.clone(),
            [0; SOURCE_PARTICLE_CURVE_BANK_BYTES],
            buffers[0].clone(),
            buffers[1].clone(),
            workgroups,
        )?;
        Ok(Self {
            slot,
            shader,
            entry_point,
            curve,
            buffers,
            workgroups,
            current: 0,
            encoded_steps: 0,
            pending: None,
            publication: None,
        })
    }

    /// Configure stable publication before the first submission. The material
    /// handle remains fixed while the two simulation buffers alternate. Only
    /// the final admitted phase copies its result; no particle reaches the CPU.
    pub fn with_publication_buffer(
        mut self,
        buffer: Handle<ShaderBuffer>,
        bytes: u64,
    ) -> Result<Self, &'static str> {
        if self.pending.is_some()
            || self.encoded_steps != 0
            || self.buffers.contains(&buffer)
            || bytes == 0
            || !bytes.is_multiple_of(4)
        {
            return Err(
                "Source sequence publication must be configured before simulation in distinct aligned storage",
            );
        }
        self.publication = Some((buffer, bytes));
        Ok(self)
    }

    /// Poll only command encoding. This does not attest GPU completion/readback.
    pub fn poll(&mut self) -> Result<bool, String> {
        let Some(requests) = &self.pending else {
            return Ok(true);
        };
        let mut complete = true;
        for request in requests {
            match request.status().map_err(str::to_owned)? {
                SourceParticleComputeStatus::Encoded => {}
                SourceParticleComputeStatus::Failed(error) => return Err(error),
                SourceParticleComputeStatus::WaitingForResources
                | SourceParticleComputeStatus::Compiling => complete = false,
            }
        }
        if !complete {
            return Ok(false);
        }
        let count = u64::try_from(requests.len()).map_err(|_| "Source phase count overflow")?;
        let next = self
            .encoded_steps
            .checked_add(count)
            .ok_or("Source particle sequence step counter exhausted")?;
        if requests.len() % 2 != 0 {
            self.current = 1 - self.current;
        }
        self.encoded_steps = next;
        self.pending = None;
        Ok(true)
    }

    /// Returns false while the prior step is pending; does not discard it or
    /// advance the source clock. Callers retain the unsent timestep/parameters.
    pub fn submit_next(
        &mut self,
        queue: &mut SourceParticleComputeQueue,
        bank: [u8; SOURCE_PARTICLE_CURVE_BANK_BYTES],
    ) -> Result<bool, String> {
        self.submit_ordered(queue, vec![bank])
    }

    /// Encode all phases of a logical update in order before camera rendering.
    /// A pending, unavailable or failed group publishes no intermediate buffer.
    pub fn submit_ordered(
        &mut self,
        queue: &mut SourceParticleComputeQueue,
        banks: Vec<[u8; SOURCE_PARTICLE_CURVE_BANK_BYTES]>,
    ) -> Result<bool, String> {
        if !self.poll()? {
            return Ok(false);
        }
        if banks.is_empty() {
            return Err("Source particle compute group is empty".into());
        }
        self.encoded_steps
            .checked_add(u64::try_from(banks.len()).map_err(|_| "Source phase count overflow")?)
            .ok_or("Source particle sequence step counter exhausted")?;
        let mut current = self.current;
        let mut requests = Vec::with_capacity(banks.len());
        for bank in banks {
            let request = SourceParticleComputeRequest::new(
                self.shader.clone(),
                self.entry_point.clone(),
                self.curve.clone(),
                bank,
                self.buffers[current].clone(),
                self.buffers[1 - current].clone(),
                self.workgroups,
            )
            .map_err(str::to_owned)?;
            current = 1 - current;
            requests.push(request);
        }
        if let Some((buffer, bytes)) = &self.publication {
            let last = requests
                .pop()
                .ok_or("Source publication group unexpectedly empty")?;
            requests.push(
                last.publish_into(buffer.clone(), *bytes)
                    .map_err(str::to_owned)?,
            );
        }
        queue
            .insert_ordered(self.slot, requests.clone())
            .map_err(str::to_owned)?;
        self.pending = Some(requests);
        Ok(true)
    }

    /// Buffer containing the last encoded state, suitable for a later GPU pass.
    /// A CPU consumer still needs a stamped GPU readback to observe this state.
    #[must_use]
    pub fn current_buffer(&self) -> Handle<ShaderBuffer> {
        self.publication.as_ref().map_or_else(
            || self.buffers[self.current].clone(),
            |(buffer, _)| buffer.clone(),
        )
    }

    #[must_use]
    pub const fn encoded_steps(&self) -> u64 {
        self.encoded_steps
    }

    #[must_use]
    pub const fn slot(&self) -> u64 {
        self.slot
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn buffers() -> [Handle<ShaderBuffer>; 2] {
        let mut assets = Assets::<ShaderBuffer>::default();
        std::array::from_fn(|_| assets.add(ShaderBuffer::new(&[0_u8; 16], default())))
    }

    fn sequence(buffers: [Handle<ShaderBuffer>; 2]) -> SourceParticleComputeSequence {
        SourceParticleComputeSequence::new(
            7,
            Handle::default(),
            "advance".into(),
            Handle::default(),
            buffers,
            1,
        )
        .unwrap()
    }

    #[test]
    fn aliased_state_buffers_are_rejected_before_dispatch() {
        let [a, _] = buffers();
        assert!(
            SourceParticleComputeSequence::new(
                7,
                Handle::default(),
                "advance".into(),
                Handle::default(),
                [a.clone(), a],
                1,
            )
            .is_err()
        );
    }

    #[test]
    fn pending_steps_keep_their_request_and_publish_each_encoded_buffer_once() {
        let pair = buffers();
        let mut sequence = sequence(pair.clone());
        let mut queue = SourceParticleComputeQueue::default();
        assert!(sequence.submit_next(&mut queue, [1; 1024]).unwrap());
        let first = queue.0[&7][0].clone();
        first.set_status(SourceParticleComputeStatus::Compiling);
        assert!(!sequence.submit_next(&mut queue, [2; 1024]).unwrap());
        assert!(std::sync::Arc::ptr_eq(
            &first.status,
            &queue.0[&7][0].status
        ));
        assert_eq!(sequence.current_buffer(), pair[0]);
        assert_eq!(sequence.encoded_steps(), 0);
        first.set_status(SourceParticleComputeStatus::Encoded);
        assert!(sequence.poll().unwrap());
        assert!(sequence.poll().unwrap());
        assert_eq!(sequence.current_buffer(), pair[1]);
        assert_eq!(sequence.encoded_steps(), 1);
        assert!(sequence.submit_next(&mut queue, [3; 1024]).unwrap());
        assert_eq!(queue.0[&7][0].input, pair[1]);
        assert_eq!(queue.0[&7][0].output, pair[0]);
        assert_eq!(queue.0[&7][0].bank.as_ref(), &[3; 1024]);
    }

    #[test]
    fn shader_failure_preserves_the_last_published_state() {
        let pair = buffers();
        let mut sequence = sequence(pair.clone());
        let mut queue = SourceParticleComputeQueue::default();
        assert!(sequence.submit_next(&mut queue, [0; 1024]).unwrap());
        queue.0[&7][0].set_status(SourceParticleComputeStatus::Failed("compile failed".into()));
        assert_eq!(sequence.poll().unwrap_err(), "compile failed");
        assert!(sequence.submit_next(&mut queue, [1; 1024]).is_err());
        assert_eq!(sequence.current_buffer(), pair[0]);
        assert_eq!(sequence.encoded_steps(), 0);
    }

    #[test]
    fn ordered_phases_publish_only_the_complete_final_state_and_keep_their_chain() {
        let pair = buffers();
        let mut sequence = sequence(pair.clone());
        let mut queue = SourceParticleComputeQueue::default();
        assert!(
            sequence
                .submit_ordered(&mut queue, vec![[1; 1024], [2; 1024], [3; 1024]])
                .unwrap()
        );
        let phases = queue.0[&7].clone();
        assert_eq!(phases[0].input, pair[0]);
        assert_eq!(phases[0].output, phases[1].input);
        assert_eq!(phases[1].output, phases[2].input);
        phases[0].set_status(SourceParticleComputeStatus::Encoded);
        phases[1].set_status(SourceParticleComputeStatus::Compiling);
        assert!(!sequence.poll().unwrap());
        assert_eq!(sequence.current_buffer(), pair[0]);
        assert_eq!(sequence.encoded_steps(), 0);
        assert!(!sequence.submit_next(&mut queue, [4; 1024]).unwrap());
        for phase in &phases {
            phase.set_status(SourceParticleComputeStatus::Encoded);
        }
        assert!(sequence.poll().unwrap());
        assert_eq!(sequence.current_buffer(), pair[1]);
        assert_eq!(sequence.encoded_steps(), 3);
        assert!(
            sequence
                .submit_ordered(&mut queue, vec![[5; 1024], [6; 1024]])
                .unwrap()
        );
        for phase in &queue.0[&7] {
            phase.set_status(SourceParticleComputeStatus::Encoded);
        }
        assert!(sequence.poll().unwrap());
        assert_eq!(sequence.current_buffer(), pair[1]);
        assert_eq!(sequence.encoded_steps(), 5);
    }

    #[test]
    fn empty_or_disconnected_groups_preserve_an_existing_queue_slot() {
        let pair = buffers();
        let mut sequence = sequence(pair.clone());
        let mut queue = SourceParticleComputeQueue::default();
        assert!(sequence.submit_next(&mut queue, [1; 1024]).unwrap());
        let original = queue.0[&7][0].clone();
        assert!(queue.insert_ordered(7, vec![]).is_err());
        assert!(
            queue
                .insert_ordered(7, vec![original.clone(), original.clone()])
                .is_err()
        );
        assert!(std::sync::Arc::ptr_eq(
            &original.status,
            &queue.0[&7][0].status
        ));
        original.set_status(SourceParticleComputeStatus::Encoded);
        assert!(sequence.poll().unwrap());
        assert!(sequence.submit_ordered(&mut queue, vec![]).is_err());
        assert_eq!(sequence.encoded_steps(), 1);
        assert_eq!(sequence.current_buffer(), pair[1]);
    }

    #[test]
    fn stable_publication_is_final_only_keeps_its_handle_and_rejects_aliases() {
        let mut assets = Assets::<ShaderBuffer>::default();
        let pair = std::array::from_fn(|_| assets.add(ShaderBuffer::new(&[0_u8; 16], default())));
        let stable = assets.add(ShaderBuffer::new(&[0_u8; 16], default()));
        assert!(
            sequence(pair.clone())
                .with_publication_buffer(pair[0].clone(), 16)
                .is_err()
        );
        assert!(
            sequence(pair.clone())
                .with_publication_buffer(stable.clone(), 3)
                .is_err()
        );
        let mut source = sequence(pair.clone())
            .with_publication_buffer(stable.clone(), 16)
            .unwrap();
        let mut queue = SourceParticleComputeQueue::default();
        assert!(
            source
                .submit_ordered(&mut queue, vec![[0; 1024]; 3])
                .unwrap()
        );
        assert_eq!(source.current_buffer(), stable);
        assert!(queue.0[&7][0].publication.is_none());
        assert!(queue.0[&7][1].publication.is_none());
        assert_eq!(queue.0[&7][2].publication.as_ref().unwrap().buffer, stable);
        assert_eq!(queue.0[&7][2].output, pair[1]);
        for request in &queue.0[&7] {
            request.set_status(SourceParticleComputeStatus::Encoded);
        }
        assert!(source.poll().unwrap());
        assert_eq!(source.current_buffer(), stable);
        assert!(
            source
                .submit_ordered(&mut queue, vec![[0; 1024]; 2])
                .unwrap()
        );
        assert_eq!(queue.0[&7][1].output, pair[1]);
        assert_eq!(source.current_buffer(), stable);
    }

    #[test]
    fn early_shared_or_aliased_publication_preserves_an_existing_slot() {
        let mut assets = Assets::<ShaderBuffer>::default();
        let buffers: [Handle<ShaderBuffer>; 4] =
            std::array::from_fn(|_| assets.add(ShaderBuffer::new(&[0_u8; 16], default())));
        let request = |a: usize, b: usize| {
            SourceParticleComputeRequest::new(
                Handle::default(),
                "advance".into(),
                Handle::default(),
                [0; 1024],
                buffers[a].clone(),
                buffers[b].clone(),
                1,
            )
            .unwrap()
        };
        let mut queue = SourceParticleComputeQueue::default();
        let existing = request(0, 1);
        queue.insert(7, existing.clone());
        assert!(
            existing
                .clone()
                .publish_into(buffers[3].clone(), 16)
                .is_err()
        );
        let early = request(0, 1).publish_into(buffers[3].clone(), 16).unwrap();
        assert!(queue.insert_ordered(7, vec![early, request(1, 2)]).is_err());
        let aliases_earlier = request(1, 2).publish_into(buffers[0].clone(), 16).unwrap();
        assert!(
            queue
                .insert_ordered(7, vec![request(0, 1), aliases_earlier])
                .is_err()
        );
        assert!(std::sync::Arc::ptr_eq(
            &existing.status,
            &queue.0[&7][0].status
        ));
    }
}
