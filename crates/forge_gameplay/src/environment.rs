//! Bounded consequences of a canonical physics frame; geometry remains in the adapter.
use crate::{GameSession, GameState, GameplayError, GameplayEvent};
use forge_core::{CommandId, ContentHash, EntityId, Vec3};
use serde::{Deserialize, Serialize};
use std::collections::BTreeSet;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ActorObservation {
    pub actor: EntityId,
    pub position: Vec3,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ObjectObservation {
    pub object: EntityId,
    pub position: Vec3,
    #[serde(default = "crate::simulation::identity_rotation")]
    pub rotation_xyzw: [f32; 4],
    pub in_motion: bool,
}

/// One collision episode, identified by the adapter. Normal closing speed excludes resting contacts.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ContactObservation {
    pub command_id: CommandId,
    pub source: EntityId,
    pub target: EntityId,
    pub initiator: Option<EntityId>,
    pub normal_speed_m_s: f32,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ContactReceipt {
    pub observation: ContactObservation,
    pub tick: u64,
    pub events: Vec<GameplayEvent>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct HeatExposure {
    pub object: EntityId,
    pub actor: EntityId,
    pub line_of_sight: bool,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct DangerObservation {
    pub object: EntityId,
    pub actor: EntityId,
    pub line_of_sight: bool,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PhysicsFrame {
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub simulation_step_ns: Option<u64>,
    pub source_hash: ContentHash,
    pub tick: u64,
    pub actors: Vec<ActorObservation>,
    pub objects: Vec<ObjectObservation>,
    pub contacts: Vec<ContactObservation>,
    pub heat: Vec<HeatExposure>,
    pub danger: Vec<DangerObservation>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PhysicsFrameReceipt {
    pub frame: PhysicsFrame,
    pub events: Vec<GameplayEvent>,
}

fn refused(message: &str) -> GameplayError {
    GameplayError::ActionRefused(message.into())
}

impl GameSession {
    pub fn advance_frame(
        &mut self,
        frame: &PhysicsFrame,
    ) -> Result<PhysicsFrameReceipt, GameplayError> {
        if frame.source_hash != self.source_hash {
            return Err(refused("stale physics source"));
        }
        if let Some(previous) = &self.state.last_frame
            && frame.tick == self.state.tick
        {
            return if previous.frame == *frame {
                Ok(previous.clone())
            } else {
                Err(refused("physics tick reused with different observations"))
            };
        }
        if frame.source_hash != self.source_hash
            || self.state.tick.checked_add(1) != Some(frame.tick)
        {
            return Err(refused("stale source or non-monotonic physics frame"));
        }
        if frame.actors.len() > self.document.actors.len()
            || frame.objects.len() > self.document.objects.len()
            || frame.contacts.len() > self.document.profile.max_events_per_tick
            || frame.heat.len() > self.document.profile.max_events_per_tick
            || frame.danger.len() > self.document.profile.max_events_per_tick
        {
            return Err(refused("physics observation quota exceeded"));
        }
        let mut next = self.state.clone();
        if self.document.simulation.is_some()
            && self.state.requires_physics_revalidation
            && frame.actors.len() != self.document.actors.len()
        {
            return Err(refused(
                "observe every actor before clearing the current projection guard",
            ));
        }
        next.requires_physics_revalidation = false;
        next.tick = frame.tick;
        next.tick_events_used = 0;
        Self::apply_positions(&mut next, frame)?;
        let mut events = self.advance_simulation_clock(&mut next, frame)?;
        let mut contacts = BTreeSet::new();
        let mut pairs = BTreeSet::new();
        for contact in &frame.contacts {
            if !contacts.insert(contact.command_id)
                || !pairs.insert((contact.source, contact.target))
            {
                return Err(refused("duplicate collision episode in frame"));
            }
            events.extend(self.apply_contact(&mut next, contact)?);
        }
        events.extend(self.apply_heat_exposures(&mut next, &frame.heat)?);
        events.extend(self.apply_danger(&mut next, &frame.danger)?);
        events.extend(self.advance_cinematic(&mut next)?);
        let remaining = self
            .document
            .profile
            .max_events_per_tick
            .saturating_sub(events.len());
        events.extend(self.apply_calendar(&mut next, frame, remaining)?);
        if events
            .iter()
            .any(|event| matches!(event, GameplayEvent::EncounterActivated { .. }))
        {
            next.requires_physics_revalidation = true;
        }
        if events.len() > self.document.profile.max_events_per_tick {
            return Err(refused("physics event quota exceeded"));
        }
        next.tick_events_used = events.len();
        let receipt = PhysicsFrameReceipt {
            frame: frame.clone(),
            events,
        };
        next.last_frame = Some(receipt.clone());
        self.state = next;
        Ok(receipt)
    }

    fn apply_positions(state: &mut GameState, frame: &PhysicsFrame) -> Result<(), GameplayError> {
        let mut ids = BTreeSet::new();
        for observation in &frame.actors {
            crate::validation::position(observation.position)?;
            if !ids.insert(observation.actor) {
                return Err(refused("duplicate actor observation"));
            }
            let actor = state
                .actors
                .get_mut(&observation.actor)
                .ok_or_else(|| GameplayError::MissingReference(observation.actor.to_string()))?;
            if !actor.active && observation.position != actor.position {
                return Err(refused("an absent actor cannot change pose"));
            }
            actor.position = observation.position;
        }
        for observation in &frame.objects {
            crate::validation::rotation(observation.rotation_xyzw)?;
            crate::validation::position(observation.position)?;
            if !ids.insert(observation.object) {
                return Err(refused("duplicate object observation"));
            }
            let object = state
                .objects
                .get_mut(&observation.object)
                .ok_or_else(|| GameplayError::MissingReference(observation.object.to_string()))?;
            if object.consumed || object.destroyed {
                return Err(refused("observation of unavailable object"));
            }
            object.position = observation.position;
            object.rotation_xyzw = observation.rotation_xyzw;
            object.in_motion = observation.in_motion;
        }
        Ok(())
    }

    pub(crate) fn validate_contact(
        &self,
        contact: &ContactObservation,
    ) -> Result<(), GameplayError> {
        if self
            .state
            .actors
            .get(&contact.target)
            .is_some_and(|actor| !actor.active)
        {
            return Err(refused("collision target is absent"));
        }
        let source = self
            .document
            .object(contact.source)
            .ok_or_else(|| GameplayError::MissingReference(contact.source.to_string()))?;
        if source.physics.mass_kg <= 0.0
            || contact.source == contact.target
            || (!self.state.actors.contains_key(&contact.target)
                && !self.state.objects.contains_key(&contact.target))
            || !contact.normal_speed_m_s.is_finite()
            || !(0.0..=200.0).contains(&contact.normal_speed_m_s)
            || contact
                .initiator
                .is_some_and(|id| !self.state.actors.contains_key(&id))
        {
            return Err(refused("invalid collision observation"));
        }
        Ok(())
    }

    fn apply_contact(
        &self,
        state: &mut GameState,
        contact: &ContactObservation,
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        self.validate_contact(contact)?;
        if let Some(previous) = state.contacts.get(&contact.command_id) {
            return if previous.observation == *contact {
                Ok(Vec::new())
            } else {
                Err(refused("collision ID reused with different observations"))
            };
        }
        if state.contacts.len() >= self.document.profile.max_action_receipts {
            return Err(refused("collision journal quota reached"));
        }
        let source = self
            .document
            .object(contact.source)
            .ok_or_else(|| refused("source missing"))?;
        if state
            .objects
            .get(&contact.source)
            .is_none_or(|o| o.consumed || o.destroyed || o.initiator != contact.initiator)
        {
            return Err(refused("collision source unavailable"));
        }
        let energy =
            0.5 * f64::from(source.physics.mass_kg) * f64::from(contact.normal_speed_m_s).powi(2);
        let mut events = vec![GameplayEvent::Impact {
            source: contact.source,
            target: contact.target,
            initiator: contact.initiator,
            energy_j: energy,
        }];
        // A contact remains observable even when below the damage threshold or absorbed.
        if contact.normal_speed_m_s > 0.1 {
            if let Some(actor) = state.actors.get_mut(&contact.target) {
                let protection = self
                    .document
                    .actor(contact.target)
                    .ok_or_else(|| refused("target missing"))?
                    .protection;
                let damage = impact_damage(energy, protection, actor.vitality);
                actor.vitality -= damage;
                events.push(GameplayEvent::Damage {
                    target: contact.target,
                    contact: true,
                    amount: damage,
                });
            } else if self
                .document
                .object(contact.target)
                .and_then(|o| o.physics.break_energy_j)
                .is_some_and(|limit| energy >= f64::from(limit))
            {
                events.extend(self.destroy_object(state, contact.target, contact.initiator)?);
            }
        }
        state.contacts.insert(
            contact.command_id,
            ContactReceipt {
                observation: contact.clone(),
                tick: state.tick,
                events: events.clone(),
            },
        );
        Ok(events)
    }

    fn destroy_object(
        &self,
        state: &mut GameState,
        object: EntityId,
        initiator: Option<EntityId>,
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        let definition = self
            .document
            .object(object)
            .ok_or_else(|| refused("destruction target missing"))?;
        let current = state
            .objects
            .get_mut(&object)
            .ok_or_else(|| refused("destruction state missing"))?;
        if current.destroyed || current.consumed {
            return Ok(Vec::new());
        }
        current.destroyed = true;
        current.burning = false;
        current.in_motion = false;
        let mut events = vec![GameplayEvent::Destroyed { object }];
        for child in &definition.physics.supports {
            let supported = state
                .objects
                .get_mut(child)
                .ok_or_else(|| refused("supported object missing"))?;
            if !supported.destroyed && !supported.consumed && !supported.released {
                supported.released = true;
                supported.in_motion = true;
                supported.initiator = initiator;
                events.push(GameplayEvent::Released { object: *child });
            }
        }
        Ok(events)
    }

    fn apply_heat_exposures(
        &self,
        state: &mut GameState,
        observations: &[HeatExposure],
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        let mut unique = BTreeSet::new();
        let mut events = Vec::new();
        for exposure in observations {
            if !unique.insert((exposure.object, exposure.actor)) {
                return Err(refused("duplicate heat exposure"));
            }
            let definition = self
                .document
                .object(exposure.object)
                .ok_or_else(|| GameplayError::MissingReference(exposure.object.to_string()))?;
            let source = state
                .objects
                .get(&exposure.object)
                .ok_or_else(|| refused("heat source missing"))?;
            Self::within(
                state,
                exposure.actor,
                source.position,
                self.document.profile.interaction_range_m,
            )?;
            if !source.burning || !exposure.line_of_sight {
                continue;
            }
            let actor = state
                .actors
                .get_mut(&exposure.actor)
                .ok_or_else(|| GameplayError::MissingReference(exposure.actor.to_string()))?;
            let damage = definition.physics.heat_damage_per_tick.min(actor.vitality);
            actor.vitality -= damage;
            if damage > 0 {
                events.push(GameplayEvent::Damage {
                    target: exposure.actor,
                    contact: false,
                    amount: damage,
                });
            }
        }
        Ok(events)
    }

    fn apply_danger(
        &self,
        state: &mut GameState,
        observations: &[DangerObservation],
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        let mut seen = BTreeSet::new();
        let mut fleeing = BTreeSet::new();
        let mut events = Vec::new();
        for observation in observations {
            if !seen.insert((observation.object, observation.actor)) {
                return Err(refused("duplicate danger observation"));
            }
            let definition = self
                .document
                .actor(observation.actor)
                .ok_or_else(|| GameplayError::MissingReference(observation.actor.to_string()))?;
            let source = state
                .objects
                .get(&observation.object)
                .ok_or_else(|| GameplayError::MissingReference(observation.object.to_string()))?;
            Self::within(
                state,
                observation.actor,
                source.position,
                self.document.profile.interaction_range_m,
            )?;
            if observation.line_of_sight
                && definition.flees_danger
                && !source.destroyed
                && !source.consumed
                && (source.burning || (source.released && source.in_motion))
            {
                fleeing.insert(observation.actor);
            }
        }
        for (id, actor) in &mut state.actors {
            let active = actor.active && actor.vitality > 0 && fleeing.contains(id);
            if active && !actor.fleeing {
                events.push(GameplayEvent::Fleeing { actor: *id });
            }
            actor.fleeing = active;
        }
        Ok(events)
    }
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "finite nonnegative impact damage is explicitly clamped to validated vitality (at most 1000000)"
)]
fn impact_damage(energy: f64, protection: u32, vitality: u32) -> u32 {
    (energy.floor() - f64::from(protection))
        .max(0.0)
        .min(f64::from(vitality)) as u32
}
