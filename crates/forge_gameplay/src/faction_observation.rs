//! Bounded local perception snapshots and deterministic consequence projection.
use crate::factions::{
    FactionConsequence, FactionWitnessObservation, FactionWitnessReceipt, FactionsDefinition,
    FactionsState, MAX_FACTION_WITNESSES, MAX_PERSONAL_RELATIONS, MAX_RELATION, MIN_RELATION,
};
use crate::{GameAction, GameRequest, GameState, GameplayDocument, GameplayError};
use forge_core::EntityId;
use std::collections::BTreeSet;

fn refused(reason: &str) -> GameplayError {
    GameplayError::ActionRefused(format!("faction observation: {reason}"))
}

pub(crate) fn snapshot_witnesses(
    definitions: &FactionsDefinition,
    state: &GameState,
    request: &GameRequest,
    observations: &[FactionWitnessObservation],
) -> Result<Vec<FactionWitnessReceipt>, GameplayError> {
    if observations.len() > MAX_FACTION_WITNESSES {
        return Err(refused("witness quota exceeded"));
    }
    let GameAction::Attack { target } = request.action else {
        if !observations.is_empty() {
            return Err(refused("this trigger requires a direct assault"));
        }
        return Ok(Vec::new());
    };
    let aggressor = state
        .actors
        .get(&request.actor)
        .ok_or_else(|| refused("aggressor is absent"))?;
    let victim = state
        .actors
        .get(&target)
        .ok_or_else(|| refused("victim is absent"))?;
    let mut unique = BTreeSet::new();
    let mut accepted = Vec::new();
    for observation in observations {
        if !unique.insert(observation.observer) {
            return Err(refused("duplicate witness"));
        }
        let observer = state
            .actors
            .get(&observation.observer)
            .ok_or_else(|| refused("witness is absent"))?;
        if observation.observer == request.actor
            || !observer.active
            || observer.vitality == 0
            || !observation.line_of_sight
        {
            continue;
        }
        let Some(faction) = definitions.actor_faction(observation.observer) else {
            continue;
        };
        let Some(victim_faction) = definitions.memberships.get(&target) else {
            continue;
        };
        if !faction.protects_factions.contains(victim_faction)
            || !faction.positions_in_perception_range(
                observer.position,
                aggressor.position,
                victim.position,
            )
        {
            continue;
        }
        accepted.push(FactionWitnessReceipt {
            observer: observation.observer,
            observer_position: observer.position,
            aggressor_position: aggressor.position,
            victim_position: victim.position,
        });
    }
    accepted.sort_unstable_by_key(|witness| witness.observer);
    Ok(accepted)
}

/// Pure reconstruction also used to check retained observations before resuming.
pub(crate) fn consequences(
    definitions: &FactionsDefinition,
    document: &GameplayDocument,
    opinions: &FactionsState,
    request: &GameRequest,
    witnesses: &[FactionWitnessReceipt],
) -> Result<Vec<FactionConsequence>, GameplayError> {
    if witnesses.len() > MAX_FACTION_WITNESSES {
        return Err(refused("witness receipt quota exceeded"));
    }
    let GameAction::Attack { target } = request.action else {
        if !witnesses.is_empty() {
            return Err(refused("witness receipt has no direct assault"));
        }
        return Ok(Vec::new());
    };
    if document.actor(request.actor).is_none() || document.actor(target).is_none() {
        return Err(refused("assault actor reference is absent"));
    }
    let mut changes = Vec::new();
    let mut unique = BTreeSet::new();
    let mut additional = 0;
    for witness in witnesses {
        if !unique.insert(witness.observer) {
            return Err(refused("duplicate witness receipt"));
        }
        validate_witness(definitions, document, request.actor, target, witness)?;
        if witnesses.first().is_some_and(|first| {
            first.aggressor_position != witness.aggressor_position
                || first.victim_position != witness.victim_position
        }) {
            return Err(refused("witness receipts refer to different assault poses"));
        }
        let faction = definitions
            .actor_faction(witness.observer)
            .ok_or_else(|| refused("witness faction is absent"))?;
        let previous = opinions.personal_relation(definitions, witness.observer, request.actor);
        let current = previous
            .saturating_add(faction.assault_delta)
            .clamp(MIN_RELATION, MAX_RELATION);
        if current == previous {
            continue;
        }
        if opinions
            .opinions
            .get(&witness.observer)
            .is_none_or(|entries| !entries.contains_key(&request.actor))
        {
            additional += 1;
        }
        changes.push(FactionConsequence {
            observer: witness.observer,
            subject: request.actor,
            previous,
            current,
            cause: request.command_id,
        });
    }
    if opinions.modified_relations().saturating_add(additional) > MAX_PERSONAL_RELATIONS {
        return Err(refused("personal relation quota exceeded"));
    }
    Ok(changes)
}

fn validate_witness(
    definitions: &FactionsDefinition,
    document: &GameplayDocument,
    aggressor: EntityId,
    victim: EntityId,
    witness: &FactionWitnessReceipt,
) -> Result<(), GameplayError> {
    if witness.observer == aggressor || document.actor(witness.observer).is_none() {
        return Err(refused("witness actor reference is invalid"));
    }
    for position in [
        witness.observer_position,
        witness.aggressor_position,
        witness.victim_position,
    ] {
        crate::validation::position(position)?;
    }
    let faction = definitions
        .actor_faction(witness.observer)
        .ok_or_else(|| refused("witness faction is absent"))?;
    let victim_faction = definitions
        .memberships
        .get(&victim)
        .ok_or_else(|| refused("victim faction is absent"))?;
    if !faction.protects_factions.contains(victim_faction)
        || !faction.positions_in_perception_range(
            witness.observer_position,
            witness.aggressor_position,
            witness.victim_position,
        )
    {
        return Err(refused(
            "witness receipt violates protection or perception range",
        ));
    }
    Ok(())
}
