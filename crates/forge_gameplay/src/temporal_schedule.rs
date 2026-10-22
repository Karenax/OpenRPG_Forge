//! Prepared bounded calendar enumeration; runtime registration follows lot C.
use crate::GameplayError;
use crate::temporal::{
    CalendarEventDefinition, MAX_SIMULATION_EVENTS_PER_STEP, SimulationDefinition,
};
use forge_core::EntityId;

#[derive(Clone, Debug, PartialEq, Eq)]
pub(crate) struct CalendarOccurrence {
    pub event: EntityId,
    pub cycle: u64,
    pub elapsed_ns: u64,
}

fn refused(reason: &str) -> GameplayError {
    GameplayError::ActionRefused(format!("simulation calendar: {reason}"))
}

impl SimulationDefinition {
    pub(crate) fn due_events(
        &self,
        before_ns: u64,
        after_ns: u64,
        initial: bool,
        budget: usize,
    ) -> Result<Vec<CalendarOccurrence>, GameplayError> {
        if after_ns < before_ns {
            return Err(refused("time moved backwards"));
        }
        let minute_ns = self
            .clock
            .nanoseconds_per_minute()
            .ok_or_else(|| refused("invalid calendar minute"))?;
        let budget = budget.min(MAX_SIMULATION_EVENTS_PER_STEP);
        let mut due = Vec::new();
        for event in &self.events {
            enumerate(
                event, minute_ns, before_ns, after_ns, initial, budget, &mut due,
            )?;
        }
        due.sort_unstable_by_key(|occurrence| {
            (occurrence.elapsed_ns, occurrence.event, occurrence.cycle)
        });
        Ok(due)
    }
}

fn enumerate(
    event: &CalendarEventDefinition,
    minute_ns: u64,
    before_ns: u64,
    after_ns: u64,
    initial: bool,
    budget: usize,
    due: &mut Vec<CalendarOccurrence>,
) -> Result<(), GameplayError> {
    let first = u64::from(event.first_minute)
        .checked_mul(minute_ns)
        .ok_or_else(|| refused("event time overflow"))?;
    if after_ns < first {
        return Ok(());
    }
    let include_first = initial && before_ns == 0 && first == 0;
    let Some(period) = event.repeat_every_minutes else {
        if first > before_ns || include_first {
            if due.len() >= budget {
                return Err(refused("due event quota exceeded"));
            }
            due.push(CalendarOccurrence {
                event: event.id,
                cycle: 0,
                elapsed_ns: first,
            });
        }
        return Ok(());
    };
    let period = u64::from(period)
        .checked_mul(minute_ns)
        .filter(|period| *period > 0)
        .ok_or_else(|| refused("invalid event period"))?;
    let next = if before_ns < first || include_first {
        0
    } else {
        (before_ns - first) / period + 1
    };
    let last = (after_ns - first) / period;
    if next > last {
        return Ok(());
    }
    let count =
        usize::try_from(last - next + 1).map_err(|_| refused("due event quota exceeded"))?;
    if count > budget.saturating_sub(due.len()) {
        return Err(refused("due event quota exceeded"));
    }
    for cycle in next..=last {
        let elapsed_ns = cycle
            .checked_mul(period)
            .and_then(|offset| first.checked_add(offset))
            .ok_or_else(|| refused("event time overflow"))?;
        due.push(CalendarOccurrence {
            event: event.id,
            cycle,
            elapsed_ns,
        });
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    const MINUTE_NS: u64 = 1_000_000_000;
    fn event(name: &str, first: u32, period: Option<u32>) -> CalendarEventDefinition {
        CalendarEventDefinition {
            id: EntityId::deterministic(format!("schedule/{name}")),
            name: name.into(),
            first_minute: first,
            repeat_every_minutes: period,
        }
    }
    fn schedule(events: Vec<CalendarEventDefinition>) -> SimulationDefinition {
        SimulationDefinition {
            events,
            ..SimulationDefinition::default()
        }
    }

    #[test]
    fn initial_zero_occurrence_is_included_once_then_the_boundary_is_exclusive() {
        let definition = schedule(vec![event("initial", 0, None)]);
        assert_eq!(definition.due_events(0, 1, true, 128).unwrap().len(), 1);
        assert!(
            definition
                .due_events(1, MINUTE_NS, false, 128)
                .unwrap()
                .is_empty()
        );
        assert!(definition.due_events(0, 1, false, 128).unwrap().is_empty());
    }

    #[test]
    fn one_shot_includes_exact_deadline_and_never_repeats_after_it() {
        let definition = schedule(vec![event("one-shot", 1, None)]);
        assert!(
            definition
                .due_events(0, MINUTE_NS - 1, true, 128)
                .unwrap()
                .is_empty()
        );
        let occurrence = definition
            .due_events(MINUTE_NS - 1, MINUTE_NS, false, 128)
            .unwrap();
        assert_eq!(occurrence.len(), 1);
        assert_eq!(occurrence[0].elapsed_ns, MINUTE_NS);
        assert_eq!(occurrence[0].cycle, 0);
        assert!(
            definition
                .due_events(MINUTE_NS, 2 * MINUTE_NS, false, 128)
                .unwrap()
                .is_empty()
        );
    }

    #[test]
    fn recurrent_cycles_continue_from_observed_boundary_without_duplicates() {
        let definition = schedule(vec![event("repeat", 1, Some(2))]);
        let first = definition.due_events(0, 7 * MINUTE_NS, true, 128).unwrap();
        assert_eq!(
            first
                .iter()
                .map(|event| (event.cycle, event.elapsed_ns / MINUTE_NS))
                .collect::<Vec<_>>(),
            vec![(0, 1), (1, 3), (2, 5), (3, 7)]
        );
        let resumed = definition
            .due_events(7 * MINUTE_NS, 9 * MINUTE_NS, false, 128)
            .unwrap();
        assert_eq!(resumed.len(), 1);
        assert_eq!(resumed[0].cycle, 4);
        assert_eq!(resumed[0].elapsed_ns, 9 * MINUTE_NS);
    }

    #[test]
    fn output_order_depends_on_time_then_identity_not_authored_event_order() {
        let events = vec![
            event("a", 1, None),
            event("b", 0, None),
            event("c", 1, None),
        ];
        let mut reversed = events.clone();
        reversed.reverse();
        let actual = schedule(events)
            .due_events(0, MINUTE_NS, true, 128)
            .unwrap();
        assert_eq!(
            actual,
            schedule(reversed)
                .due_events(0, MINUTE_NS, true, 128)
                .unwrap()
        );
        assert_eq!(actual[0].elapsed_ns, 0);
        assert!(actual[1].event < actual[2].event);
    }

    #[test]
    fn huge_due_work_is_refused_before_enumerating_or_allocating_it() {
        let definition = schedule(vec![event("repeat", 0, Some(1))]);
        assert!(
            definition
                .due_events(0, 1_000_000_000 * MINUTE_NS, true, 128)
                .is_err()
        );
        assert!(
            definition
                .due_events(0, 128 * MINUTE_NS, true, usize::MAX)
                .is_err()
        );
        assert_eq!(
            definition
                .due_events(0, 127 * MINUTE_NS, true, usize::MAX)
                .unwrap()
                .len(),
            128
        );
        assert!(definition.due_events(0, 1, true, 0).is_err());
    }

    #[test]
    fn backwards_time_zero_period_and_overflow_are_refused() {
        assert!(schedule(vec![]).due_events(2, 1, false, 128).is_err());
        assert!(
            schedule(vec![event("bad-period", 0, Some(0))])
                .due_events(0, 1, true, 128)
                .is_err()
        );
        let mut definition = schedule(vec![event("overflow", 1_000_000_000, None)]);
        definition.clock.milliseconds_per_minute = 3_600_000;
        assert!(definition.due_events(0, u64::MAX, true, 128).is_err());
    }
}
