//! At most sixteen nearby candidates receive physical line-of-sight queries.
use super::{GameplayRuntime, RapierContext, engine, visible};
use bevy::prelude::Entity;
use forge_core::EntityId;
use forge_gameplay::{FactionWitnessObservation, GameAction, GameRequest, MAX_FACTION_WITNESSES};

pub(super) fn collect(
    runtime: &GameplayRuntime,
    context: &RapierContext,
    player_entity: Entity,
    request: &GameRequest,
) -> Vec<FactionWitnessObservation> {
    let GameAction::Attack { target } = request.action else {
        return Vec::new();
    };
    let document = runtime.session.document();
    let Some(definitions) = &document.factions else {
        return Vec::new();
    };
    let state = runtime.session.state();
    let (Some(aggressor), Some(victim), Some(victim_faction)) = (
        state.actors.get(&request.actor),
        state.actors.get(&target),
        definitions.memberships.get(&target),
    ) else {
        return Vec::new();
    };
    let entity = |id: EntityId| {
        if id == document.player {
            Some(player_entity)
        } else {
            runtime.entities.get(&id).copied()
        }
    };
    let mut candidates: Vec<_> = definitions
        .memberships
        .keys()
        .filter_map(|id| {
            if *id == request.actor {
                return None;
            }
            let observer = state.actors.get(id)?;
            let faction = definitions.actor_faction(*id)?;
            let origin = engine(observer.position);
            let distance = origin.distance_squared(engine(aggressor.position));
            if !observer.active
                || observer.vitality == 0
                || !faction.protects_factions.contains(victim_faction)
                || !faction.positions_in_perception_range(
                    observer.position,
                    aggressor.position,
                    victim.position,
                )
            {
                return None;
            }
            Some((*id, origin, distance, entity(*id)?))
        })
        .collect();
    // Stable distance/identity ordering bounds raycasts independently of scene population.
    candidates.sort_unstable_by(|a, b| a.2.total_cmp(&b.2).then_with(|| a.0.cmp(&b.0)));
    candidates
        .into_iter()
        .take(MAX_FACTION_WITNESSES)
        .map(|(observer, origin, _, exclude)| FactionWitnessObservation {
            observer,
            line_of_sight: visible(
                context,
                origin,
                engine(aggressor.position),
                exclude,
                entity(request.actor),
            ) && visible(
                context,
                origin,
                engine(victim.position),
                exclude,
                entity(target),
            ),
        })
        .collect()
}
