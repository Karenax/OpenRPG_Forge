//! Transient scene owners driven by explicit admitted events/deltas. No shader,
//! source ABI, filesystem path or particle value is inferred or persisted here.
use super::{
    SOURCE_PARTICLE_CURVE_BANK_BYTES, SourceParticleComputePlugin, SourceParticleComputeQueue,
    SourceParticleEmissionCommand, SourceParticleEmitterSequence,
};
use bevy::{prelude::*, render::storage::ShaderBuffer};
use forge_assets::source_particle_emission::{
    SourceEmissionAdvance, SourceEmissionEvent, SourceEmissionSnapshot,
};
use std::{
    collections::{BTreeMap, VecDeque},
    sync::Arc,
};

type LowerCommand = Box<
    dyn FnMut(
            SourceParticleEmissionCommand,
            u64,
        ) -> Result<[u8; SOURCE_PARTICLE_CURVE_BANK_BYTES], String>
        + Send
        + Sync,
>;

struct Frame {
    delta: f32,
    events: Vec<SourceEmissionEvent>,
    reset: bool,
}

/// Attach one component to one scene owner, not one entity per particle. The
/// caller has admitted the sequence and lowers commands to its source ABI.
/// Rendering/persistent author documents remain separate host responsibilities.
#[derive(Component)]
pub struct SourceParticleSceneOwner {
    controller: SourceParticleEmitterSequence,
    lower: LowerCommand,
    inputs: VecDeque<Frame>,
    identity: Arc<()>,
    queue_identities: Vec<super::RequestIdentity>,
    submitted: u64,
    completed: u64,
    in_flight: bool,
    error: Option<String>,
    #[cfg(feature = "physics-rapier")]
    scene_bound: bool,
}

impl SourceParticleSceneOwner {
    #[cfg(test)]
    pub(crate) fn queued_frame_events(&self, index: usize) -> &[SourceEmissionEvent] {
        &self.inputs[index].events
    }
    #[cfg(any(feature = "physics-rapier", test))]
    pub(crate) fn instance_identity(&self) -> Arc<()> {
        self.identity.clone()
    }
    pub fn new(
        controller: SourceParticleEmitterSequence,
        lower: impl FnMut(
            SourceParticleEmissionCommand,
            u64,
        ) -> Result<[u8; SOURCE_PARTICLE_CURVE_BANK_BYTES], String>
        + Send
        + Sync
        + 'static,
    ) -> Self {
        Self {
            controller,
            lower: Box::new(lower),
            inputs: VecDeque::new(),
            identity: Arc::new(()),
            queue_identities: Vec::new(),
            submitted: 0,
            completed: 0,
            in_flight: false,
            error: None,
            #[cfg(feature = "physics-rapier")]
            scene_bound: false,
        }
    }

    /// FIFO input is retained while prior GPU phases wait. Overflow/invalid
    /// input is rejected before enqueueing; no event is silently discarded.
    pub fn queue_frame(
        &mut self,
        delta: f32,
        events: &[SourceEmissionEvent],
        reset: bool,
    ) -> Result<(), &'static str> {
        self.validate_frame(delta, events)?;
        self.inputs.push_back(Frame {
            delta,
            events: events.to_vec(),
            reset,
        });
        Ok(())
    }

    pub(crate) fn validate_frame(
        &self,
        delta: f32,
        events: &[SourceEmissionEvent],
    ) -> Result<(), &'static str> {
        if self.error.is_some() {
            return Err("Source scene owner failed; replace its admitted component to restart");
        }
        if !delta.is_finite() || delta < 0.0 {
            return Err("Invalid source scene owner delta");
        }
        if self.inputs.len() >= 4096 || events.len() > 4096 {
            return Err("Source scene owner input backlog exceeds budget");
        }
        Ok(())
    }

    #[must_use]
    pub fn error(&self) -> Option<&str> {
        self.error.as_deref()
    }
    #[must_use]
    pub fn queued_frames(&self) -> usize {
        self.inputs.len()
    }
    #[must_use]
    pub const fn submitted_frames(&self) -> u64 {
        self.submitted
    }
    #[must_use]
    pub const fn completed_frames(&self) -> u64 {
        self.completed
    }
    #[must_use]
    pub const fn encoded_steps(&self) -> u64 {
        self.controller.encoded_steps()
    }
    #[must_use]
    pub const fn slot(&self) -> u64 {
        self.controller.slot()
    }
    #[must_use]
    pub const fn snapshot(&self) -> SourceEmissionSnapshot {
        self.controller.snapshot()
    }
    #[must_use]
    pub const fn last_frame(&self) -> Option<SourceEmissionAdvance> {
        self.controller.last_frame()
    }
    #[must_use]
    pub fn current_buffer(&self) -> Handle<ShaderBuffer> {
        self.controller.current_buffer()
    }

    #[cfg(feature = "physics-rapier")]
    pub(crate) fn validate_scene_binding(
        &self,
        queue: &SourceParticleComputeQueue,
    ) -> Result<(), &'static str> {
        if self.queue_identities.is_empty() {
            return self.controller.validate_scene_binding(queue);
        }
        if !queue.matches_exact(self.slot(), &self.identity, &self.queue_identities) {
            return Err("Particle producer queue ownership changed before scene admission");
        }
        if self.in_flight {
            self.controller.validate_pending(queue)?;
        }
        Ok(())
    }

    #[cfg(feature = "physics-rapier")]
    pub(crate) fn bind_scene(
        &mut self,
        queue: &mut SourceParticleComputeQueue,
    ) -> Result<(), &'static str> {
        self.validate_scene_binding(queue)?;
        if self.in_flight {
            self.controller.bind_scene_pending(queue, &self.identity)?;
        }
        self.scene_bound = true;
        Ok(())
    }

    #[cfg(feature = "physics-rapier")]
    pub(crate) fn queue_identities(&self) -> Vec<super::RequestIdentity> {
        self.queue_identities.clone()
    }

    #[cfg(feature = "physics-rapier")]
    pub(super) fn owns_current_queue(&self, queue: &SourceParticleComputeQueue) -> bool {
        if self.queue_identities.is_empty() {
            !queue.contains_slot(self.slot())
        } else {
            queue.matches_exact(self.slot(), &self.identity, &self.queue_identities)
        }
    }

    fn advance(&mut self, queue: &mut SourceParticleComputeQueue) -> Result<(), String> {
        if !queue.free_or_owned(self.slot(), &self.identity)
            || (!self.queue_identities.is_empty()
                && !queue.matches_exact(self.slot(), &self.identity, &self.queue_identities))
        {
            return Err("Source particle producer slot was replaced".into());
        }
        if self.in_flight {
            self.controller
                .validate_pending(queue)
                .map_err(str::to_owned)?;
        }
        if !self.controller.poll()? {
            return Ok(());
        }
        if self.in_flight {
            self.completed = self.submitted;
            self.in_flight = false;
        }
        let Some(frame) = self.inputs.front() else {
            return Ok(());
        };
        let stamp = self
            .submitted
            .checked_add(1)
            .ok_or("Source scene frame counter exhausted")?;
        let lower = &mut self.lower;
        if self.controller.submit_frame(
            queue,
            frame.delta,
            &frame.events,
            frame.reset,
            |command| lower(command, stamp),
        )? {
            self.controller
                .bind_owner_pending(queue, &self.identity)
                .map_err(str::to_owned)?;
            #[cfg(feature = "physics-rapier")]
            if self.scene_bound {
                self.controller
                    .bind_scene_pending(queue, &self.identity)
                    .map_err(str::to_owned)?;
            }
            self.queue_identities = self
                .controller
                .pending_identities()
                .ok_or("Source particle submitted group disappeared")?;
            self.inputs.pop_front();
            self.submitted = stamp;
            self.in_flight = true;
        }
        Ok(())
    }
}

struct Claim {
    slot: u64,
    identity: Arc<()>,
    queue_identities: Vec<super::RequestIdentity>,
}
#[derive(Resource, Default)]
pub(super) struct SceneOwners {
    claims: BTreeMap<Entity, Claim>,
}

#[cfg(feature = "physics-rapier")]
impl SceneOwners {
    /// The late render refresh may only advance an owner already checked by
    /// the normal scene update; newly inserted or replaced owners wait for it.
    pub(super) fn owns(&self, entity: Entity, owner: &SourceParticleSceneOwner) -> bool {
        self.claims.get(&entity).is_some_and(|claim| {
            claim.slot == owner.slot() && Arc::ptr_eq(&claim.identity, &owner.identity)
        })
    }
}

/// Opt-in scene adapter; adding it alone enables no effect or implicit clock.
/// Each host queues its own author/runtime events and delta. Failed owners keep
/// their published state and input; a fresh admitted component restarts them.
pub struct SourceParticleScenePlugin;
#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub(super) struct SourceParticleSceneUpdate;
impl Plugin for SourceParticleScenePlugin {
    fn build(&self, app: &mut App) {
        if !app.is_plugin_added::<SourceParticleComputePlugin>() {
            app.add_plugins(SourceParticleComputePlugin);
        }
        app.init_resource::<SceneOwners>().add_systems(
            Update,
            advance_scene_owners.in_set(SourceParticleSceneUpdate),
        );
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
pub(super) fn advance_scene_owners(
    mut queue: ResMut<SourceParticleComputeQueue>,
    mut residents: ResMut<SceneOwners>,
    mut owners: Query<(Entity, &mut SourceParticleSceneOwner)>,
) {
    let current: BTreeMap<_, _> = owners
        .iter()
        .map(|(entity, owner)| {
            (
                entity,
                Claim {
                    slot: owner.slot(),
                    identity: owner.identity.clone(),
                    queue_identities: owner.queue_identities.clone(),
                },
            )
        })
        .collect();
    for (entity, claim) in &residents.claims {
        if current.get(entity).is_none_or(|next| {
            next.slot != claim.slot || !Arc::ptr_eq(&next.identity, &claim.identity)
        }) {
            queue.remove_exact(claim.slot, &claim.identity, &claim.queue_identities);
        }
    }
    let mut counts = BTreeMap::<u64, usize>::new();
    for claim in current.values() {
        *counts.entry(claim.slot).or_default() += 1;
    }
    residents.claims = current;
    for (entity, mut owner) in &mut owners {
        if counts[&owner.slot()] != 1 {
            queue.remove_exact(owner.slot(), &owner.identity, &owner.queue_identities);
            owner.error = Some("Source scene owners have a duplicate compute slot".into());
        } else if owner.error.is_none()
            && let Err(error) = owner.advance(&mut queue)
        {
            owner.error = Some(error);
        }
        if let Some(claim) = residents.claims.get_mut(&entity) {
            claim.queue_identities.clone_from(&owner.queue_identities);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::super::{SourceParticleComputeSequence, SourceParticleComputeStatus};
    use super::*;
    use forge_assets::source_particle_emission::SourceSingleBurstLoopParameters;

    fn owner(slot: u64, assets: &mut Assets<ShaderBuffer>) -> SourceParticleSceneOwner {
        let pair = std::array::from_fn(|_| assets.add(ShaderBuffer::new(&[0_u8; 16], default())));
        let compute = SourceParticleComputeSequence::new(
            slot,
            Handle::default(),
            "advance".into(),
            Handle::default(),
            pair,
            1,
        )
        .unwrap();
        let controller = SourceParticleEmitterSequence::new(
            compute,
            SourceSingleBurstLoopParameters {
                loop_duration_seconds: 3.0,
                loop_count: 1,
                burst_delay_seconds: 0.0,
                burst_count: 1.0,
                burst_active: true,
                initial_play: false,
                prewarm_steps: 0,
            },
        )
        .unwrap();
        SourceParticleSceneOwner::new(controller, |command, _| {
            if matches!(command,SourceParticleEmissionCommand::Allocate {count} if count.to_bits()!=1.0_f32.to_bits())
            {
                return Err("Test only admits Count1".into());
            }
            Ok([0; 1024])
        })
    }

    fn encode(app: &mut App, slot: u64) {
        for request in &app.world().resource::<SourceParticleComputeQueue>().0[&slot] {
            request.set_status(SourceParticleComputeStatus::Encoded);
        }
    }

    #[test]
    fn foreign_producer_request_survives_next_cycle_and_owner_removal() {
        use std::sync::Mutex;
        let mut app = App::new();
        app.add_plugins(SourceParticleScenePlugin);
        let mut assets = Assets::<ShaderBuffer>::default();
        let mut component = owner(7, &mut assets);
        component.queue_frame(0.125, &[], false).unwrap();
        let entity = app.world_mut().spawn(component).id();
        app.update();
        let previous = app.world().resource::<SourceParticleComputeQueue>().0[&7].clone();
        for request in &previous {
            request.set_status(SourceParticleComputeStatus::Encoded);
        }
        let mut foreign = previous[0].clone();
        foreign.status = Arc::new(Mutex::new(SourceParticleComputeStatus::WaitingForResources));
        let status = foreign.status.clone();
        app.world_mut()
            .resource_mut::<SourceParticleComputeQueue>()
            .insert(7, foreign);
        app.update();
        assert!(Arc::ptr_eq(
            &app.world().resource::<SourceParticleComputeQueue>().0[&7][0].status,
            &status
        ));
        assert!(
            app.world()
                .get::<SourceParticleSceneOwner>(entity)
                .unwrap()
                .error()
                .is_some()
        );
        app.world_mut().despawn(entity);
        app.update();
        assert!(Arc::ptr_eq(
            &app.world().resource::<SourceParticleComputeQueue>().0[&7][0].status,
            &status
        ));
    }

    #[test]
    fn scene_update_keeps_unsent_input_commits_per_owner_and_retires_removed_slots() {
        let mut app = App::new();
        app.add_plugins(SourceParticleScenePlugin);
        let mut assets = Assets::<ShaderBuffer>::default();
        let mut a = owner(11, &mut assets);
        let mut b = owner(22, &mut assets);
        a.queue_frame(0.125, &[SourceEmissionEvent::Play], false)
            .unwrap();
        b.queue_frame(0.25, &[SourceEmissionEvent::Play], false)
            .unwrap();
        let a = app.world_mut().spawn(a).id();
        let b = app.world_mut().spawn(b).id();
        app.update();
        app.world_mut()
            .get_mut::<SourceParticleSceneOwner>(a)
            .unwrap()
            .queue_frame(0.5, &[SourceEmissionEvent::Stop], false)
            .unwrap();
        app.update();
        assert_eq!(
            app.world()
                .get::<SourceParticleSceneOwner>(a)
                .unwrap()
                .queued_frames(),
            1
        );
        encode(&mut app, 22);
        app.update();
        assert_eq!(
            app.world()
                .get::<SourceParticleSceneOwner>(b)
                .unwrap()
                .completed_frames(),
            1
        );
        assert_eq!(
            app.world()
                .get::<SourceParticleSceneOwner>(a)
                .unwrap()
                .snapshot()
                .effect_time_seconds
                .to_bits(),
            0
        );
        encode(&mut app, 11);
        app.update();
        let state = app.world().get::<SourceParticleSceneOwner>(a).unwrap();
        assert_eq!(state.completed_frames(), 1);
        assert_eq!(state.submitted_frames(), 2);
        assert_eq!(state.queued_frames(), 0);
        assert_eq!(
            state.snapshot().effect_time_seconds.to_bits(),
            0.125_f32.to_bits()
        );
        encode(&mut app, 11);
        app.update();
        let state = app.world().get::<SourceParticleSceneOwner>(a).unwrap();
        assert_eq!(state.completed_frames(), 2);
        assert_eq!(
            state.snapshot().effect_time_seconds.to_bits(),
            0.625_f32.to_bits()
        );
        assert!(!state.snapshot().playing());
        assert!(
            app.world()
                .get::<SourceParticleSceneOwner>(b)
                .unwrap()
                .snapshot()
                .playing()
        );
        app.world_mut().despawn(a);
        app.update();
        let queue = app.world().resource::<SourceParticleComputeQueue>();
        assert!(!queue.0.contains_key(&11));
        assert!(queue.0.contains_key(&22));
        assert_eq!(assets.len(), 4);
    }

    #[test]
    fn duplicate_slots_and_lowering_failure_keep_input_and_last_published_state() {
        let mut app = App::new();
        app.add_plugins(SourceParticleScenePlugin);
        let mut assets = Assets::<ShaderBuffer>::default();
        for _ in 0..2 {
            let mut duplicate = owner(11, &mut assets);
            duplicate.queue_frame(0.125, &[], false).unwrap();
            app.world_mut().spawn(duplicate);
        }
        let mut failed = owner(22, &mut assets);
        failed.lower = Box::new(|_, _| Err("admitted source lowering refused".into()));
        failed
            .queue_frame(0.25, &[SourceEmissionEvent::Play], false)
            .unwrap();
        let failed = app.world_mut().spawn(failed).id();
        app.update();
        assert!(
            app.world()
                .resource::<SourceParticleComputeQueue>()
                .0
                .is_empty()
        );
        let state = app.world().get::<SourceParticleSceneOwner>(failed).unwrap();
        assert_eq!(state.queued_frames(), 1);
        assert_eq!(state.completed_frames(), 0);
        assert_eq!(state.snapshot().effect_time_seconds.to_bits(), 0);
        assert!(state.error().unwrap().contains("lowering"));
        let mut query = app.world_mut().query::<&SourceParticleSceneOwner>();
        for state in query.iter(app.world()) {
            assert!(state.error().is_some());
            assert_eq!(state.queued_frames(), 1);
        }
    }
}
