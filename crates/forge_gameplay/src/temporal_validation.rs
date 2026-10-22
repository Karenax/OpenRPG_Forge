//! Prepared lot D source checks; not registered until the runtime is wired.
use crate::temporal::{
    AbilityEffect, EffectStacking, EncounterRearm, MAX_ABILITIES, MAX_ABILITY_EFFECTS,
    MAX_ABILITY_ITEM_COSTS, MAX_ABILITY_POINTS, MAX_ABILITY_RANGE_M, MAX_ACTOR_ABILITIES,
    MAX_CALENDAR_EVENTS, MAX_EFFECT_DURATION_MS, MAX_EFFECT_EXCLUSIONS, MAX_ENCOUNTER_PARTICIPANTS,
    MAX_ENCOUNTERS, MAX_TIMED_EFFECT_DEFINITIONS, SimulationDefinition,
};
use crate::{ActorRole, GameplayDocument, GameplayError};
use forge_core::EntityId;
use std::collections::BTreeSet;

fn invalid(reason: impl Into<String>) -> GameplayError {
    GameplayError::InvalidDocument(format!("simulation: {}", reason.into()))
}

fn named(ids: &mut BTreeSet<EntityId>, id: EntityId, name: &str) -> Result<(), GameplayError> {
    if !ids.insert(id) {
        return Err(invalid(format!("duplicate identity {id}")));
    }
    if name.trim().is_empty() || name.len() > 256 {
        return Err(invalid(format!("invalid name {id}")));
    }
    Ok(())
}

impl SimulationDefinition {
    pub(crate) fn validate(
        &self,
        document: &GameplayDocument,
        ids: &mut BTreeSet<EntityId>,
    ) -> Result<(), GameplayError> {
        if self.version != 1
            || self.abilities.len() > MAX_ABILITIES
            || self.effects.len() > MAX_TIMED_EFFECT_DEFINITIONS
            || self.actor_abilities.len() > crate::MAX_GAMEPLAY_ENTRIES
            || self.events.len() > MAX_CALENDAR_EVENTS
            || self.encounters.len() > MAX_ENCOUNTERS
        {
            return Err(invalid("unsupported version or definition quota exceeded"));
        }
        if self.clock.start_minute >= 1440
            || !(1..=3_600_000).contains(&self.clock.milliseconds_per_minute)
        {
            return Err(invalid(
                "clock minute must be within a day and its duration between 1 ms and one hour",
            ));
        }
        self.validate_effects(ids)?;
        self.validate_abilities(document, ids)?;
        self.validate_assignments(document)?;
        self.validate_events(ids)?;
        self.validate_encounters(document, ids)
    }

    fn validate_effects(&self, ids: &mut BTreeSet<EntityId>) -> Result<(), GameplayError> {
        for effect in &self.effects {
            named(ids, effect.id, &effect.name)?;
            if !(1..=MAX_EFFECT_DURATION_MS).contains(&effect.duration_ms)
                || !(1..=MAX_ABILITY_POINTS).contains(&effect.attack_protection_bonus)
                || effect.exclusive_with.len() > MAX_EFFECT_EXCLUSIONS
            {
                return Err(invalid(format!("effect bounds {}", effect.id)));
            }
            if let EffectStacking::Stack { max_instances } = effect.stacking
                && !(1..=8).contains(&max_instances)
            {
                return Err(invalid(format!("effect stack bound {}", effect.id)));
            }
            let mut references = BTreeSet::new();
            for other in &effect.exclusive_with {
                if *other == effect.id
                    || self.effect(*other).is_none()
                    || !references.insert(*other)
                {
                    return Err(invalid(format!("effect exclusivity reference {other}")));
                }
            }
        }
        Ok(())
    }

    fn validate_abilities(
        &self,
        document: &GameplayDocument,
        ids: &mut BTreeSet<EntityId>,
    ) -> Result<(), GameplayError> {
        for ability in &self.abilities {
            named(ids, ability.id, &ability.name)?;
            if !ability.range_m.is_finite()
                || !(0.0..=MAX_ABILITY_RANGE_M).contains(&ability.range_m)
                || ability.stamina_cost > document.profile.max_stamina
                || ability.effects.is_empty()
                || ability.effects.len() > MAX_ABILITY_EFFECTS
                || ability.item_costs.len() > MAX_ABILITY_ITEM_COSTS
            {
                return Err(invalid(format!("ability bounds {}", ability.id)));
            }
            document.validate_condition(&ability.condition)?;
            let mut costs = BTreeSet::new();
            for cost in &ability.item_costs {
                if !costs.insert(cost.item)
                    || cost.quantity == 0
                    || document
                        .item(cost.item)
                        .is_none_or(|item| cost.quantity > item.max_stack)
                {
                    return Err(invalid(format!("ability item cost {}", cost.item)));
                }
            }
            for effect in &ability.effects {
                match effect {
                    AbilityEffect::Heal { points }
                        if !(1..=MAX_ABILITY_POINTS).contains(points) =>
                    {
                        return Err(invalid("healing points out of bounds"));
                    }
                    AbilityEffect::Timed { effect } if self.effect(*effect).is_none() => {
                        return Err(invalid(format!("missing timed effect {effect}")));
                    }
                    _ => (),
                }
            }
        }
        Ok(())
    }

    fn validate_assignments(&self, document: &GameplayDocument) -> Result<(), GameplayError> {
        for (actor, abilities) in &self.actor_abilities {
            if document.actor(*actor).is_none() || abilities.len() > MAX_ACTOR_ABILITIES {
                return Err(invalid(format!("actor ability assignment {actor}")));
            }
            let mut unique = BTreeSet::new();
            for ability in abilities {
                if !unique.insert(*ability) || self.ability(*ability).is_none() {
                    return Err(invalid(format!(
                        "actor ability reference {actor}/{ability}"
                    )));
                }
            }
        }
        Ok(())
    }

    fn validate_events(&self, ids: &mut BTreeSet<EntityId>) -> Result<(), GameplayError> {
        let minute_ns = self
            .clock
            .nanoseconds_per_minute()
            .ok_or_else(|| invalid("invalid calendar duration"))?;
        for event in &self.events {
            named(ids, event.id, &event.name)?;
            if event.first_minute > 1_000_000_000
                || event
                    .repeat_every_minutes
                    .is_some_and(|period| period == 0 || period > 1_000_000_000)
                || u64::from(event.first_minute)
                    .checked_mul(minute_ns)
                    .is_none()
                || event
                    .repeat_every_minutes
                    .is_some_and(|period| u64::from(period).checked_mul(minute_ns).is_none())
            {
                return Err(invalid(format!("calendar event bounds {}", event.id)));
            }
        }
        Ok(())
    }

    fn validate_encounters(
        &self,
        document: &GameplayDocument,
        ids: &mut BTreeSet<EntityId>,
    ) -> Result<(), GameplayError> {
        let mut assigned = BTreeSet::new();
        for encounter in &self.encounters {
            named(ids, encounter.id, &encounter.name)?;
            if matches!(encounter.rearm, EncounterRearm::AfterDefeat { .. })
                && self
                    .event(encounter.event)
                    .is_some_and(|event| event.repeat_every_minutes.is_none())
            {
                return Err(invalid(format!(
                    "recurrent encounter requires a recurrent calendar event {}",
                    encounter.id
                )));
            }
            if self.event(encounter.event).is_none()
                || encounter.participants.is_empty()
                || encounter.participants.len() > MAX_ENCOUNTER_PARTICIPANTS
            {
                return Err(invalid(format!(
                    "encounter event or participant bound {}",
                    encounter.id
                )));
            }
            if let EncounterRearm::AfterDefeat { cooldown_minutes } = encounter.rearm
                && (cooldown_minutes > 1_000_000_000
                    || self.clock.nanoseconds_per_minute().is_none_or(|minute_ns| {
                        u64::from(cooldown_minutes).checked_mul(minute_ns).is_none()
                    }))
            {
                return Err(invalid(format!(
                    "encounter cooldown bound {}",
                    encounter.id
                )));
            }
            for actor in &encounter.participants {
                if document
                    .actor(*actor)
                    .is_none_or(|actor| actor.role == ActorRole::Player)
                    || !assigned.insert(*actor)
                {
                    return Err(invalid(format!(
                        "encounter participant must be a unique non-player actor {actor}"
                    )));
                }
            }
            document.validate_condition(&encounter.condition)?;
        }
        Ok(())
    }
}
