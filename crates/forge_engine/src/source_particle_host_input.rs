//! Explicit scene ticks and ordered events, independent of Bevy Time or UI.
use crate::source_particle_host::HostedSourceParticle;
use bevy::prelude::World;
use forge_assets::source_particle_emission::SourceEmissionEvent;

#[derive(Default)]
pub struct HostedParticleInput {
    last_tick: Option<u64>,
    events: Vec<SourceEmissionEvent>,
}
impl HostedParticleInput {
    pub fn push_event(&mut self, event: SourceEmissionEvent) -> Result<(), String> {
        if self.events.len() >= 4096 {
            return Err("Particle event backlog exceeds budget".into());
        }
        self.events.push(event);
        Ok(())
    }
    /// False means this tick was already accepted; pending new events remain.
    /// Preflight every owner before mutation. Rejection preserves tick/events.
    pub fn advance(
        &mut self,
        world: &mut World,
        instances: &[HostedSourceParticle],
        tick: u64,
        delta: f32,
    ) -> Result<bool, String> {
        if !delta.is_finite() || delta < 0.0 {
            return Err("Invalid explicit particle delta".into());
        }
        if self.last_tick.is_some_and(|last| tick < last) {
            return Err("Particle scene tick moved backwards".into());
        }
        if self.last_tick == Some(tick) {
            return Ok(false);
        }
        if instances.is_empty() {
            return Err("Particle scene has no admitted instance".into());
        }
        for instance in instances {
            instance.validate_frame(world, delta, &self.events)?;
        }
        for instance in instances {
            instance.queue_frame(world, delta, &self.events, false)?;
        }
        self.last_tick = Some(tick);
        self.events.clear();
        Ok(true)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::source_particle_compute::SourceParticleSceneOwner;
    use crate::source_particle_host::tests::{prepared, setup};
    #[test]
    fn explicit_ticks_zero_delta_and_event_order_are_retained_once() {
        let (mut world, node, material) = setup(true);
        let ticket =
            HostedSourceParticle::install(&mut world, prepared(), node, material, 7, 1).unwrap();
        let mut input = HostedParticleInput::default();
        input.push_event(SourceEmissionEvent::Stop).unwrap();
        input.push_event(SourceEmissionEvent::Play).unwrap();
        assert!(
            input
                .advance(&mut world, std::slice::from_ref(&ticket), 10, 0.0)
                .unwrap()
        );
        assert_eq!(
            world
                .get::<SourceParticleSceneOwner>(node)
                .unwrap()
                .queued_frames(),
            1
        );
        assert_eq!(
            world
                .get::<SourceParticleSceneOwner>(node)
                .unwrap()
                .queued_frame_events(0),
            &[SourceEmissionEvent::Stop, SourceEmissionEvent::Play]
        );
        input.push_event(SourceEmissionEvent::Stop).unwrap();
        assert!(
            !input
                .advance(&mut world, std::slice::from_ref(&ticket), 10, 0.0)
                .unwrap()
        );
        assert_eq!(input.events, vec![SourceEmissionEvent::Stop]);
        assert!(
            input
                .advance(&mut world, std::slice::from_ref(&ticket), 9, 0.125)
                .is_err()
        );
        assert!(
            input
                .advance(&mut world, std::slice::from_ref(&ticket), 11, f32::NAN)
                .is_err()
        );
        assert_eq!(input.last_tick, Some(10));
        assert!(
            input
                .advance(&mut world, std::slice::from_ref(&ticket), 11, 0.125)
                .unwrap()
        );
        assert_eq!(
            world
                .get::<SourceParticleSceneOwner>(node)
                .unwrap()
                .queued_frames(),
            2
        );
        assert!(input.events.is_empty());
    }
    #[test]
    fn invalid_member_refuses_whole_group_without_consuming_tick_or_events() {
        let (mut world, node, material) = setup(true);
        let first =
            HostedSourceParticle::install(&mut world, prepared(), node, material.clone(), 7, 1)
                .unwrap();
        let other = world.spawn_empty().id();
        let material_copy = world
            .resource::<bevy::prelude::Assets<crate::source_graph::SourceGraphMaterial>>()
            .get(&material)
            .unwrap()
            .clone();
        let other_material = world
            .resource_mut::<bevy::prelude::Assets<crate::source_graph::SourceGraphMaterial>>()
            .add(material_copy);
        let second =
            HostedSourceParticle::install(&mut world, prepared(), other, other_material, 9, 2)
                .unwrap();
        let mut instances = vec![first, second];
        instances[1].release(&mut world);
        let mut input = HostedParticleInput::default();
        input.push_event(SourceEmissionEvent::Play).unwrap();
        assert!(input.advance(&mut world, &instances, 1, 0.25).is_err());
        assert_eq!(
            world
                .get::<SourceParticleSceneOwner>(node)
                .unwrap()
                .queued_frames(),
            0
        );
        assert_eq!(input.last_tick, None);
        assert_eq!(input.events, vec![SourceEmissionEvent::Play]);
    }
}
