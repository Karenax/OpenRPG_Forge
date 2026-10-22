//! Versioned simulation, ability and encounter definitions; no engine or UI handles.
use crate::{Condition, QuestConsumption};
use forge_core::{CommandId, EntityId};
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;

pub const MAX_ABILITIES: usize = 128;
pub const MAX_TIMED_EFFECT_DEFINITIONS: usize = 128;
pub const MAX_ACTOR_ABILITIES: usize = 16;
pub const MAX_ACTIVE_EFFECTS: usize = 128;
pub const MAX_CALENDAR_EVENTS: usize = 64;
pub const MAX_ENCOUNTERS: usize = 64;
pub const MAX_ENCOUNTER_PARTICIPANTS: usize = 16;
pub const MAX_ENCOUNTER_ACTIVATIONS: usize = 128;
pub const MAX_SIMULATION_STEP_NS: u64 = 1_000_000_000;
pub const MAX_ABILITY_EFFECTS: usize = 8;
pub const MAX_ABILITY_ITEM_COSTS: usize = 8;
pub const MAX_EFFECT_EXCLUSIONS: usize = 8;
pub const MAX_EFFECT_DURATION_MS: u32 = 600_000;
pub const MAX_ABILITY_POINTS: u32 = 1_000_000;
pub const MAX_ABILITY_RANGE_M: f32 = 50.0;
pub const MAX_SIMULATION_EVENTS_PER_STEP: usize = 128;

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SimulationClockDefinition {
    pub start_day: u32,
    pub start_minute: u16,
    /// A minute of the authored calendar, measured in simulation milliseconds.
    pub milliseconds_per_minute: u32,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct CalendarMoment {
    pub day: u64,
    pub hour: u8,
    pub minute: u8,
}

impl SimulationClockDefinition {
    #[must_use]
    pub fn nanoseconds_per_minute(&self) -> Option<u64> {
        (self.milliseconds_per_minute > 0)
            .then_some(u64::from(self.milliseconds_per_minute) * 1_000_000)
    }

    #[must_use]
    pub fn moment_at(&self, elapsed_ns: u64) -> Option<CalendarMoment> {
        let minutes = u64::from(self.start_day) * 1440
            + u64::from(self.start_minute)
            + elapsed_ns.checked_div(self.nanoseconds_per_minute()?)?;
        Some(CalendarMoment {
            day: minutes / 1440,
            hour: u8::try_from((minutes % 1440) / 60).ok()?,
            minute: u8::try_from(minutes % 60).ok()?,
        })
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum AbilityTarget {
    SelfOnly,
    AnyLivingActor,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum AbilityEffect {
    Heal { points: u32 },
    Timed { effect: EntityId },
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AbilityDefinition {
    pub id: EntityId,
    pub name: String,
    pub target: AbilityTarget,
    pub range_m: f32,
    pub condition: Condition,
    pub stamina_cost: u32,
    pub item_costs: Vec<QuestConsumption>,
    pub effects: Vec<AbilityEffect>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum EffectStacking {
    Reject,
    Refresh,
    Stack { max_instances: u8 },
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TimedEffectDefinition {
    pub id: EntityId,
    pub name: String,
    pub duration_ms: u32,
    pub attack_protection_bonus: u32,
    pub stacking: EffectStacking,
    pub exclusive_with: Vec<EntityId>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct CalendarEventDefinition {
    pub id: EntityId,
    pub name: String,
    /// Minutes elapsed since the authored starting date.
    pub first_minute: u32,
    pub repeat_every_minutes: Option<u32>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum EncounterRearm {
    Once,
    AfterDefeat { cooldown_minutes: u32 },
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct EncounterDefinition {
    pub id: EntityId,
    pub name: String,
    pub event: EntityId,
    pub participants: Vec<EntityId>,
    pub condition: Condition,
    pub rearm: EncounterRearm,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SimulationDefinition {
    pub version: u32,
    pub clock: SimulationClockDefinition,
    pub abilities: Vec<AbilityDefinition>,
    pub effects: Vec<TimedEffectDefinition>,
    pub actor_abilities: BTreeMap<EntityId, Vec<EntityId>>,
    pub events: Vec<CalendarEventDefinition>,
    pub encounters: Vec<EncounterDefinition>,
}

impl Default for SimulationDefinition {
    fn default() -> Self {
        Self {
            version: 1,
            clock: SimulationClockDefinition {
                start_day: 1,
                start_minute: 8 * 60,
                milliseconds_per_minute: 1000,
            },
            abilities: Vec::new(),
            effects: Vec::new(),
            actor_abilities: BTreeMap::new(),
            events: Vec::new(),
            encounters: Vec::new(),
        }
    }
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ActiveEffect {
    pub id: EntityId,
    pub effect: EntityId,
    pub target: EntityId,
    pub cause: CommandId,
    pub applied_at_ns: u64,
    pub expires_at_ns: u64,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum EffectEndReason {
    Expired,
    Cancelled,
    Refreshed,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct EncounterActivation {
    pub encounter: EntityId,
    pub event: EntityId,
    pub cycle: u64,
    pub elapsed_ns: u64,
    pub scheduled_ns: u64,
    pub tick: u64,
    pub previous_vitality: BTreeMap<EntityId, u32>,
}

#[derive(Clone, Debug, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SimulationState {
    pub elapsed_ns: u64,
    pub effects: BTreeMap<EntityId, ActiveEffect>,
    pub activations: Vec<EncounterActivation>,
}

impl SimulationDefinition {
    #[must_use]
    pub fn ability(&self, id: EntityId) -> Option<&AbilityDefinition> {
        self.abilities.iter().find(|ability| ability.id == id)
    }

    #[must_use]
    pub fn effect(&self, id: EntityId) -> Option<&TimedEffectDefinition> {
        self.effects.iter().find(|effect| effect.id == id)
    }

    #[must_use]
    pub fn event(&self, id: EntityId) -> Option<&CalendarEventDefinition> {
        self.events.iter().find(|event| event.id == id)
    }

    #[must_use]
    pub fn encounter(&self, id: EntityId) -> Option<&EncounterDefinition> {
        self.encounters.iter().find(|encounter| encounter.id == id)
    }
}

#[cfg(test)]
mod clock_tests {
    use super::*;

    #[test]
    fn calendar_day_rollover_uses_integer_simulation_time() {
        let clock = SimulationClockDefinition {
            start_day: 7,
            start_minute: 1439,
            milliseconds_per_minute: 999,
        };
        assert_eq!(
            clock.moment_at(998_999_999),
            Some(CalendarMoment {
                day: 7,
                hour: 23,
                minute: 59
            })
        );
        assert_eq!(
            clock.moment_at(999_000_000),
            Some(CalendarMoment {
                day: 8,
                hour: 0,
                minute: 0
            })
        );
        assert_eq!(
            clock.moment_at(999_000_000 * 61),
            Some(CalendarMoment {
                day: 8,
                hour: 1,
                minute: 0
            })
        );
    }

    #[test]
    fn zero_calendar_scale_is_rejected_and_large_elapsed_time_does_not_overflow() {
        let mut clock = SimulationClockDefinition {
            start_day: u32::MAX,
            start_minute: 1439,
            milliseconds_per_minute: 1,
        };
        assert!(clock.moment_at(u64::MAX).is_some());
        clock.milliseconds_per_minute = 0;
        assert!(clock.nanoseconds_per_minute().is_none());
        assert!(clock.moment_at(1).is_none());
    }
}
