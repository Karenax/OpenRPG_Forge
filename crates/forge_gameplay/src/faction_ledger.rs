//! Reconstruct local consequences from retained authoritative action receipts.
use crate::factions::{
    FactionConsequence, FactionWitnessReceipt, FactionsDefinition, FactionsState,
};
use crate::{GameAction, GameReceipt, GameplayDocument, GameplayError, GameplayEvent};

/// The caller extracts local-reaction events from the authoritative receipt.
pub(crate) struct LocalFactionAction<'a> {
    pub receipt: &'a GameReceipt,
    pub witnesses: &'a [FactionWitnessReceipt],
    pub changes: &'a [FactionConsequence],
}

fn invalid(reason: &str) -> GameplayError {
    GameplayError::IncompatibleSave(format!("local faction journal: {reason}"))
}

/// Applies only a projection already validated against its authored cause.
pub(crate) fn promote(opinions: &mut FactionsState, changes: &[FactionConsequence]) {
    for change in changes {
        opinions
            .opinions
            .entry(change.observer)
            .or_default()
            .insert(change.subject, change.current);
    }
}

pub(crate) fn replay(
    definitions: &FactionsDefinition,
    document: &GameplayDocument,
    opinions: &mut FactionsState,
    action: &LocalFactionAction<'_>,
) -> Result<(), GameplayError> {
    if !action.witnesses.is_empty() {
        let GameAction::Attack { target } = action.receipt.request.action else {
            return Err(invalid("witnesses require a direct assault"));
        };
        if !action.receipt.events.iter().any(|event| {
            matches!(event,
                GameplayEvent::Damage { target: observed, contact: true, .. } if *observed == target
            )
        }) {
            return Err(invalid("witnesses lack an accepted physical contact"));
        }
    }
    let expected = crate::faction_observation::consequences(
        definitions,
        document,
        opinions,
        &action.receipt.request,
        action.witnesses,
    )
    .map_err(|error| invalid(&error.to_string()))?;
    if action.changes != expected {
        return Err(invalid(
            "reactions differ from their retained local observations",
        ));
    }
    promote(opinions, &expected);
    Ok(())
}
