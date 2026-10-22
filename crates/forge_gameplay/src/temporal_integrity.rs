//! Prepared lot D checks reconstruct timed effects and encounter resources.
use crate::temporal::{
    AbilityDefinition, AbilityEffect, ActiveEffect, CalendarEventDefinition, EffectEndReason,
    EncounterActivation, EncounterRearm, MAX_ACTIVE_EFFECTS, MAX_ENCOUNTER_ACTIVATIONS,
    MAX_SIMULATION_EVENTS_PER_STEP, MAX_SIMULATION_STEP_NS, SimulationDefinition, SimulationState,
};
use crate::{GameAction, GameSession, GameplayError, GameplayEvent};
use forge_core::EntityId;
use std::collections::{BTreeMap, BTreeSet};

fn invalid(reason: impl Into<String>) -> GameplayError {
    GameplayError::IncompatibleSave(format!("simulation: {}", reason.into()))
}

fn temporal_event(event: &GameplayEvent) -> bool {
    matches!(
        event,
        GameplayEvent::AbilityUsed { .. }
            | GameplayEvent::Healed { .. }
            | GameplayEvent::TimedEffectApplied { .. }
            | GameplayEvent::TimedEffectEnded { .. }
            | GameplayEvent::CalendarTriggered { .. }
            | GameplayEvent::EncounterActivated { .. }
    )
}

fn timed_event(event: &GameplayEvent) -> bool {
    matches!(
        event,
        GameplayEvent::TimedEffectApplied { .. } | GameplayEvent::TimedEffectEnded { .. }
    )
}

impl GameSession {
    pub(crate) fn temporal_event_valid(&self, event: &GameplayEvent) -> bool {
        match *event {
            GameplayEvent::AbilityUsed { ability, target } => {
                self.document
                    .simulation
                    .as_ref()
                    .is_some_and(|simulation| simulation.ability(ability).is_some())
                    && self.document.actor(target).is_some()
            }
            GameplayEvent::Healed { target, amount } => {
                amount > 0
                    && self
                        .document
                        .actor(target)
                        .is_some_and(|actor| amount <= actor.max_vitality)
            }
            GameplayEvent::TimedEffectApplied { ref effect } => {
                self.document
                    .simulation
                    .as_ref()
                    .and_then(|simulation| simulation.effect(effect.effect))
                    .is_some_and(|definition| {
                        effect.expires_at_ns.checked_sub(effect.applied_at_ns)
                            == Some(u64::from(definition.duration_ms) * 1_000_000)
                    })
                    && self.document.actor(effect.target).is_some()
            }
            GameplayEvent::TimedEffectEnded { .. } => self.document.simulation.is_some(),
            GameplayEvent::CalendarTriggered { event, .. } => self
                .document
                .simulation
                .as_ref()
                .is_some_and(|simulation| simulation.event(event).is_some()),
            GameplayEvent::EncounterActivated { ref activation } => self
                .document
                .simulation
                .as_ref()
                .is_some_and(|simulation| simulation.encounter(activation.encounter).is_some()),
            _ => false,
        }
    }

    pub(crate) fn validate_simulation_state(&self) -> Result<(), GameplayError> {
        if self
            .state
            .contacts
            .values()
            .flat_map(|contact| &contact.events)
            .any(temporal_event)
        {
            return Err(invalid(
                "temporal consequences outside the physics/action authority",
            ));
        }
        let Some(definitions) = &self.document.simulation else {
            if self.state.simulation.is_some()
                || self.state.actors.values().any(|actor| !actor.active)
                || self.state.receipts.iter().any(|receipt| {
                    receipt.simulation_time_ns.is_some()
                        || receipt.events.iter().any(temporal_event)
                })
                || self.state.last_frame.as_ref().is_some_and(|frame| {
                    frame.frame.simulation_step_ns.is_some()
                        || frame.events.iter().any(temporal_event)
                })
            {
                return Err(invalid("temporal state without authored definitions"));
            }
            return Ok(());
        };
        let current = self
            .state
            .simulation
            .as_ref()
            .ok_or_else(|| invalid("simulation state is absent"))?;
        if current.effects.len() > MAX_ACTIVE_EFFECTS
            || current.activations.len() > MAX_ENCOUNTER_ACTIVATIONS
            || current.elapsed_ns < self.state.tick
            || current.elapsed_ns > self.state.tick.saturating_mul(MAX_SIMULATION_STEP_NS)
        {
            return Err(invalid("simulation time or state quota is invalid"));
        }
        self.validate_action_times(current.elapsed_ns)?;
        self.validate_encounter_records(definitions, current)?;
        self.validate_temporal_actor_resources(definitions, current)?;
        let expected = self.rebuild_effects_at(current.elapsed_ns, self.state.tick)?;
        if expected != current.effects {
            return Err(invalid(
                "active effects differ from retained ability/cancel receipts",
            ));
        }
        self.validate_last_simulation_frame(definitions, current)
    }

    fn validate_action_times(&self, now: u64) -> Result<(), GameplayError> {
        let mut previous = None;
        for receipt in &self.state.receipts {
            let time = receipt
                .simulation_time_ns
                .ok_or_else(|| invalid("action is missing its simulation time"))?;
            if time > now
                || time < receipt.tick
                || time > receipt.tick.saturating_mul(MAX_SIMULATION_STEP_NS)
                || (receipt.tick == self.state.tick && time != now)
            {
                return Err(invalid("action time is outside its observed tick"));
            }
            if let Some((tick, before)) = previous
                && ((tick == receipt.tick && before != time)
                    || (tick < receipt.tick && before >= time))
            {
                return Err(invalid("action times are not monotone"));
            }
            previous = Some((receipt.tick, time));
        }
        Ok(())
    }

    fn validate_encounter_records(
        &self,
        definitions: &SimulationDefinition,
        current: &SimulationState,
    ) -> Result<(), GameplayError> {
        let minute = definitions
            .clock
            .nanoseconds_per_minute()
            .ok_or_else(|| invalid("invalid minute duration"))?;
        let mut seen = BTreeSet::new();
        let mut latest = BTreeMap::<EntityId, &EncounterActivation>::new();
        let mut previous = None;
        for activation in &current.activations {
            let encounter = definitions
                .encounter(activation.encounter)
                .ok_or_else(|| invalid("encounter source is absent"))?;
            let event = definitions
                .event(activation.event)
                .ok_or_else(|| invalid("encounter event is absent"))?;
            let scheduled = occurrence_time(event, minute, activation.cycle)?;
            if encounter.event != activation.event
                || activation.scheduled_ns != scheduled
                || activation.elapsed_ns < scheduled
                || activation.elapsed_ns - scheduled > MAX_SIMULATION_STEP_NS
                || activation.elapsed_ns > current.elapsed_ns
                || activation.tick == 0
                || activation.tick > self.state.tick
                || activation.elapsed_ns < activation.tick
                || activation.elapsed_ns > activation.tick.saturating_mul(MAX_SIMULATION_STEP_NS)
                || !seen.insert((activation.encounter, activation.cycle))
            {
                return Err(invalid("encounter occurrence/cause is invalid"));
            }
            if let Some((tick, time)) = previous
                && (activation.tick < tick
                    || activation.elapsed_ns < time
                    || (activation.tick == tick && activation.elapsed_ns != time))
            {
                return Err(invalid("encounter chronology is invalid"));
            }
            previous = Some((activation.tick, activation.elapsed_ns));
            let expected: BTreeSet<_> = encounter.participants.iter().copied().collect();
            if activation
                .previous_vitality
                .keys()
                .copied()
                .collect::<BTreeSet<_>>()
                != expected
            {
                return Err(invalid(
                    "encounter participant snapshot differs from its definition",
                ));
            }
            for (actor, vitality) in &activation.previous_vitality {
                if self
                    .document
                    .actor(*actor)
                    .is_none_or(|actor| *vitality > actor.max_vitality)
                {
                    return Err(invalid("encounter snapshot vitality is invalid"));
                }
            }
            if let Some(before) = latest.insert(activation.encounter, activation) {
                let EncounterRearm::AfterDefeat { cooldown_minutes } = encounter.rearm else {
                    return Err(invalid("one-shot encounter repeated"));
                };
                let ready_at = u64::from(cooldown_minutes)
                    .checked_mul(minute)
                    .and_then(|delay| before.elapsed_ns.checked_add(delay))
                    .ok_or_else(|| invalid("encounter cooldown overflow"))?;
                if activation.cycle <= before.cycle
                    || activation.elapsed_ns < ready_at
                    || activation
                        .previous_vitality
                        .values()
                        .any(|vitality| *vitality != 0)
                {
                    return Err(invalid("encounter rearmed before defeat or cooldown"));
                }
            } else if activation
                .previous_vitality
                .iter()
                .any(|(actor, vitality)| {
                    self.document
                        .actor(*actor)
                        .is_none_or(|actor| *vitality != actor.max_vitality)
                })
            {
                return Err(invalid(
                    "initial encounter snapshot differs from absent participants",
                ));
            }
        }
        Ok(())
    }

    fn validate_temporal_actor_resources(
        &self,
        definitions: &SimulationDefinition,
        current: &SimulationState,
    ) -> Result<(), GameplayError> {
        let mut stamina: BTreeMap<_, _> = self
            .document
            .actors
            .iter()
            .map(|actor| (actor.id, self.document.profile.max_stamina))
            .collect();
        let mut present: BTreeMap<_, _> = self
            .document
            .actors
            .iter()
            .map(|actor| (actor.id, self.document.actor_initially_present(actor.id)))
            .collect();
        let mut activations = current.activations.iter().peekable();
        for receipt in &self.state.receipts {
            while activations
                .peek()
                .is_some_and(|activation| activation.tick <= receipt.tick)
            {
                let activation = activations
                    .next()
                    .ok_or_else(|| invalid("activation disappeared"))?;
                reset_resources(
                    activation,
                    definitions,
                    &mut stamina,
                    &mut present,
                    self.document.profile.max_stamina,
                )?;
            }
            if present.get(&receipt.request.actor) != Some(&true) {
                return Err(invalid("absent actor acted"));
            }
            if receipt
                .faction_witnesses
                .iter()
                .any(|witness| present.get(&witness.observer) != Some(&true))
            {
                return Err(invalid("absent actor witnessed an assault"));
            }
            let cost = match receipt.request.action {
                GameAction::Attack { target } => {
                    if present.get(&target) != Some(&true) {
                        return Err(invalid("attack on an absent actor"));
                    }
                    self.document.profile.attack_stamina_cost
                }
                GameAction::UseAbility { ability, target } => {
                    if present.get(&target) != Some(&true) {
                        return Err(invalid("ability on an absent actor"));
                    }
                    definitions
                        .ability(ability)
                        .ok_or_else(|| invalid("ability source is absent"))?
                        .stamina_cost
                }
                GameAction::ChooseDialogue { speaker, .. } => {
                    if present.get(&speaker) != Some(&true) {
                        return Err(invalid("dialogue with an absent speaker"));
                    }
                    0
                }
                _ => 0,
            };
            let available = stamina
                .get_mut(&receipt.request.actor)
                .ok_or_else(|| invalid("resource actor is absent"))?;
            *available = available
                .checked_sub(cost)
                .ok_or_else(|| invalid("retained costs exceed available stamina"))?;
        }
        for activation in activations {
            reset_resources(
                activation,
                definitions,
                &mut stamina,
                &mut present,
                self.document.profile.max_stamina,
            )?;
        }
        for (actor, state) in &self.state.actors {
            if stamina.get(actor) != Some(&state.stamina)
                || present.get(actor) != Some(&state.active)
            {
                return Err(invalid(
                    "actor resources/presence differ from actions and encounter activations",
                ));
            }
            if !state.active
                && self.document.actor(*actor).is_none_or(|definition| {
                    state.vitality != definition.max_vitality
                        || state.position != definition.position
                })
            {
                return Err(invalid("absent actor changed vitality or pose"));
            }
        }
        Ok(())
    }

    fn rebuild_effects_at(
        &self,
        cutoff_ns: u64,
        cutoff_tick: u64,
    ) -> Result<BTreeMap<EntityId, ActiveEffect>, GameplayError> {
        let definitions = self
            .document
            .simulation
            .as_ref()
            .ok_or_else(|| invalid("effect definitions are absent"))?;
        let mut projected = self.state.clone();
        projected.simulation = Some(SimulationState::default());
        for receipt in self
            .state
            .receipts
            .iter()
            .filter(|receipt| receipt.tick <= cutoff_tick)
        {
            let time = receipt
                .simulation_time_ns
                .ok_or_else(|| invalid("ability time is absent"))?;
            if time > cutoff_ns {
                continue;
            }
            let simulation = projected
                .simulation
                .as_mut()
                .ok_or_else(|| invalid("effect projection disappeared"))?;
            simulation.elapsed_ns = time;
            simulation
                .effects
                .retain(|_, effect| effect.expires_at_ns > time);
            let mut expected = Vec::new();
            match receipt.request.action {
                GameAction::UseAbility { ability, target } => {
                    let definition = definitions
                        .ability(ability)
                        .ok_or_else(|| invalid("retained ability source is absent"))?;
                    if !definitions
                        .actor_abilities
                        .get(&receipt.request.actor)
                        .is_some_and(|abilities| abilities.contains(&ability))
                    {
                        return Err(invalid("retained ability was not assigned"));
                    }
                    for (ordinal, effect) in definition.effects.iter().enumerate() {
                        if let AbilityEffect::Timed { effect } = effect {
                            expected.extend(
                                self.apply_timed_effect(
                                    &mut projected,
                                    &receipt.request,
                                    ability,
                                    target,
                                    *effect,
                                    ordinal,
                                )
                                .map_err(|error| invalid(error.to_string()))?,
                            );
                        }
                    }
                    validate_ability_events(receipt, definition, target, !expected.is_empty())?;
                }
                GameAction::CancelEffect { instance } => expected.extend(
                    Self::apply_cancel_effect(&mut projected, receipt.request.actor, instance)
                        .map_err(|error| invalid(error.to_string()))?,
                ),
                _ => {
                    if receipt.events.iter().any(|event| {
                        matches!(
                            event,
                            GameplayEvent::AbilityUsed { .. } | GameplayEvent::Healed { .. }
                        )
                    }) {
                        return Err(invalid("ability output outside an ability receipt"));
                    }
                }
            }
            let actual: Vec<_> = receipt
                .events
                .iter()
                .filter(|event| timed_event(event))
                .cloned()
                .collect();
            if actual != expected {
                return Err(invalid("timed events differ from their source action"));
            }
            if receipt.events.iter().any(|event| {
                matches!(
                    event,
                    GameplayEvent::CalendarTriggered { .. }
                        | GameplayEvent::EncounterActivated { .. }
                )
            }) {
                return Err(invalid("calendar consequence outside a physics frame"));
            }
        }
        let mut expected = projected
            .simulation
            .ok_or_else(|| invalid("effect projection disappeared"))?
            .effects;
        expected.retain(|_, effect| effect.expires_at_ns > cutoff_ns);
        Ok(expected)
    }

    fn validate_last_simulation_frame(
        &self,
        definitions: &SimulationDefinition,
        current: &SimulationState,
    ) -> Result<(), GameplayError> {
        let Some(receipt) = &self.state.last_frame else {
            return Ok(());
        };
        let step = receipt
            .frame
            .simulation_step_ns
            .filter(|step| (1..=MAX_SIMULATION_STEP_NS).contains(step))
            .ok_or_else(|| invalid("last frame has no bounded simulation step"))?;
        let before = current
            .elapsed_ns
            .checked_sub(step)
            .ok_or_else(|| invalid("last simulation interval is invalid"))?;
        if receipt.events.iter().any(|event| {
            matches!(
                event,
                GameplayEvent::AbilityUsed { .. }
                    | GameplayEvent::Healed { .. }
                    | GameplayEvent::TimedEffectApplied { .. }
            )
        }) {
            return Err(invalid("ability event in a physics frame"));
        }
        validate_calendar_frame(definitions, current, receipt, before)?;
        let before_effects =
            self.rebuild_effects_at(before, receipt.frame.tick.saturating_sub(1))?;
        let expected_ended: BTreeSet<_> = before_effects
            .values()
            .filter(|effect| effect.expires_at_ns <= current.elapsed_ns)
            .map(|effect| effect.id)
            .collect();
        let mut actual_ended = BTreeSet::new();
        for event in &receipt.events {
            if let GameplayEvent::TimedEffectEnded { instance, reason } = event
                && (*reason != EffectEndReason::Expired || !actual_ended.insert(*instance))
            {
                return Err(invalid(
                    "last-frame expiration is duplicated or has another cause",
                ));
            }
        }
        if expected_ended != actual_ended {
            return Err(invalid(
                "last-frame expirations differ from retained activations",
            ));
        }
        let expected: Vec<_> = current
            .activations
            .iter()
            .filter(|activation| activation.tick == receipt.frame.tick)
            .cloned()
            .collect();
        let actual: Vec<_> = receipt
            .events
            .iter()
            .filter_map(|event| {
                if let GameplayEvent::EncounterActivated { activation } = event {
                    Some(activation.clone())
                } else {
                    None
                }
            })
            .collect();
        if actual != expected {
            return Err(invalid(
                "last-frame encounter outputs differ from retained activations",
            ));
        }
        Ok(())
    }
}

fn validate_calendar_frame(
    definitions: &SimulationDefinition,
    current: &SimulationState,
    receipt: &crate::PhysicsFrameReceipt,
    before: u64,
) -> Result<(), GameplayError> {
    let expected_due = definitions
        .due_events(
            before,
            current.elapsed_ns,
            receipt.frame.tick == 1,
            MAX_SIMULATION_EVENTS_PER_STEP,
        )
        .map_err(|error| invalid(error.to_string()))?;
    let actual_due: Vec<_> = receipt
        .events
        .iter()
        .filter_map(|event| {
            if let GameplayEvent::CalendarTriggered {
                event,
                cycle,
                scheduled_ns,
            } = event
            {
                Some((*scheduled_ns, *event, *cycle))
            } else {
                None
            }
        })
        .collect();
    if actual_due
        != expected_due
            .iter()
            .map(|event| (event.elapsed_ns, event.event, event.cycle))
            .collect::<Vec<_>>()
    {
        return Err(invalid(
            "last calendar outputs differ from the observed interval",
        ));
    }
    Ok(())
}

fn reset_resources(
    activation: &EncounterActivation,
    definitions: &SimulationDefinition,
    stamina: &mut BTreeMap<EntityId, u32>,
    present: &mut BTreeMap<EntityId, bool>,
    maximum: u32,
) -> Result<(), GameplayError> {
    for actor in &definitions
        .encounter(activation.encounter)
        .ok_or_else(|| invalid("encounter source is absent"))?
        .participants
    {
        stamina.insert(*actor, maximum);
        present.insert(*actor, true);
    }
    Ok(())
}

fn occurrence_time(
    event: &CalendarEventDefinition,
    minute: u64,
    cycle: u64,
) -> Result<u64, GameplayError> {
    let first = u64::from(event.first_minute)
        .checked_mul(minute)
        .ok_or_else(|| invalid("event time overflow"))?;
    match event.repeat_every_minutes {
        Some(period) => cycle
            .checked_mul(u64::from(period))
            .and_then(|offset| offset.checked_mul(minute))
            .and_then(|offset| first.checked_add(offset))
            .ok_or_else(|| invalid("event occurrence overflow")),
        None if cycle == 0 => Ok(first),
        None => Err(invalid("one-shot event has another cycle")),
    }
}

fn validate_ability_events(
    receipt: &crate::GameReceipt,
    definition: &AbilityDefinition,
    target: EntityId,
    timed: bool,
) -> Result<(), GameplayError> {
    let used: Vec<_> = receipt
        .events
        .iter()
        .filter_map(|event| {
            if let GameplayEvent::AbilityUsed { ability, target } = event {
                Some((*ability, *target))
            } else {
                None
            }
        })
        .collect();
    if used != vec![(definition.id, target)] {
        return Err(invalid("ability use event differs from request"));
    }
    let maximum: u64 = definition
        .effects
        .iter()
        .filter_map(|effect| {
            if let AbilityEffect::Heal { points } = effect {
                Some(u64::from(*points))
            } else {
                None
            }
        })
        .sum();
    let mut healed = 0_u64;
    for event in &receipt.events {
        if let GameplayEvent::Healed {
            target: recipient,
            amount,
        } = event
        {
            if *recipient != target || *amount == 0 {
                return Err(invalid("invalid healing target/amount"));
            }
            healed = healed
                .checked_add(u64::from(*amount))
                .ok_or_else(|| invalid("healing overflow"))?;
        }
    }
    if healed > maximum || (healed == 0 && !timed) {
        return Err(invalid("healing differs from source effects"));
    }
    Ok(())
}
