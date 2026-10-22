//! Prepared clock/encounter mutations; the physics-frame transaction promotes them.
use crate::temporal::{
    EffectEndReason, EncounterActivation, EncounterDefinition, EncounterRearm,
    MAX_ENCOUNTER_ACTIVATIONS, MAX_SIMULATION_STEP_NS,
};
use crate::temporal_schedule::CalendarOccurrence;
use crate::{GameSession, GameState, GameplayError, GameplayEvent, PhysicsFrame};

fn refused(reason: &str) -> GameplayError {
    GameplayError::ActionRefused(format!("simulation clock: {reason}"))
}

impl GameSession {
    pub(crate) fn advance_simulation_clock(
        &self,
        state: &mut GameState,
        frame: &PhysicsFrame,
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        match (
            &self.document.simulation,
            &mut state.simulation,
            frame.simulation_step_ns,
        ) {
            (None, None, None) => Ok(Vec::new()),
            (Some(_), Some(current), Some(step))
                if (1..=MAX_SIMULATION_STEP_NS).contains(&step) =>
            {
                current.elapsed_ns = current
                    .elapsed_ns
                    .checked_add(step)
                    .ok_or_else(|| refused("elapsed time overflow"))?;
                let ended: Vec<_> = current
                    .effects
                    .values()
                    .filter(|effect| effect.expires_at_ns <= current.elapsed_ns)
                    .map(|effect| effect.id)
                    .collect();
                let mut events = Vec::new();
                for instance in ended {
                    current.effects.remove(&instance);
                    events.push(GameplayEvent::TimedEffectEnded {
                        instance,
                        reason: EffectEndReason::Expired,
                    });
                }
                Ok(events)
            }
            _ => Err(refused(
                "content and state require the same explicit bounded time observation",
            )),
        }
    }

    pub(crate) fn apply_calendar(
        &self,
        state: &mut GameState,
        frame: &PhysicsFrame,
        budget: usize,
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        let Some(definitions) = &self.document.simulation else {
            return Ok(Vec::new());
        };
        let now = state
            .simulation
            .as_ref()
            .ok_or_else(|| refused("simulation state is absent"))?
            .elapsed_ns;
        let before = now
            .checked_sub(
                frame
                    .simulation_step_ns
                    .ok_or_else(|| refused("time observation is absent"))?,
            )
            .ok_or_else(|| refused("invalid observed interval"))?;
        let due = definitions.due_events(before, now, self.state.tick == 0, budget)?;
        let mut events = Vec::new();
        for occurrence in due {
            if events.len() >= budget {
                return Err(refused("calendar event quota exceeded"));
            }
            events.push(GameplayEvent::CalendarTriggered {
                event: occurrence.event,
                cycle: occurrence.cycle,
                scheduled_ns: occurrence.elapsed_ns,
            });
            for encounter in definitions
                .encounters
                .iter()
                .filter(|encounter| encounter.event == occurrence.event)
            {
                if let Some(activation) =
                    self.activate_encounter(state, encounter, &occurrence, frame.tick)?
                {
                    if events.len() >= budget {
                        return Err(refused("encounter event quota exceeded"));
                    }
                    events.push(GameplayEvent::EncounterActivated { activation });
                }
            }
        }
        Ok(events)
    }

    fn activate_encounter(
        &self,
        state: &mut GameState,
        definition: &EncounterDefinition,
        occurrence: &CalendarOccurrence,
        tick: u64,
    ) -> Result<Option<EncounterActivation>, GameplayError> {
        let simulation = state
            .simulation
            .as_ref()
            .ok_or_else(|| refused("simulation state is absent"))?;
        if simulation.activations.iter().any(|activation| {
            activation.encounter == definition.id && activation.cycle == occurrence.cycle
        }) {
            return Ok(None);
        }
        let latest = simulation
            .activations
            .iter()
            .rev()
            .find(|activation| activation.encounter == definition.id)
            .map(|activation| activation.elapsed_ns);
        let now = simulation.elapsed_ns;
        if let Some(previous) = latest {
            let EncounterRearm::AfterDefeat { cooldown_minutes } = definition.rearm else {
                return Ok(None);
            };
            if definition.participants.iter().any(|actor| {
                state
                    .actors
                    .get(actor)
                    .is_none_or(|actor| !actor.active || actor.vitality != 0)
            }) {
                return Ok(None);
            }
            let minute = self
                .document
                .simulation
                .as_ref()
                .and_then(|definitions| definitions.clock.nanoseconds_per_minute())
                .ok_or_else(|| refused("calendar scale is absent"))?;
            let ready_at = u64::from(cooldown_minutes)
                .checked_mul(minute)
                .and_then(|delay| previous.checked_add(delay))
                .ok_or_else(|| refused("encounter cooldown overflow"))?;
            if now < ready_at {
                return Ok(None);
            }
        }
        if !self.condition_for_state(state, self.document.player, &definition.condition) {
            return Ok(None);
        }
        if simulation.activations.len() >= MAX_ENCOUNTER_ACTIVATIONS {
            return Err(refused("encounter activation quota exceeded"));
        }
        let mut previous_vitality = std::collections::BTreeMap::new();
        for actor in &definition.participants {
            let authored = self
                .document
                .actor(*actor)
                .ok_or_else(|| refused("encounter actor definition is absent"))?;
            let current = state
                .actors
                .get_mut(actor)
                .ok_or_else(|| refused("encounter actor is absent"))?;
            previous_vitality.insert(*actor, current.vitality);
            current.active = true;
            current.vitality = authored.max_vitality;
            current.stamina = self.document.profile.max_stamina;
            current.position = authored.position;
            current.fleeing = false;
        }
        let activation = EncounterActivation {
            encounter: definition.id,
            event: occurrence.event,
            cycle: occurrence.cycle,
            elapsed_ns: now,
            scheduled_ns: occurrence.elapsed_ns,
            tick,
            previous_vitality,
        };
        state
            .simulation
            .as_mut()
            .ok_or_else(|| refused("simulation state disappeared"))?
            .activations
            .push(activation.clone());
        Ok(Some(activation))
    }
}
