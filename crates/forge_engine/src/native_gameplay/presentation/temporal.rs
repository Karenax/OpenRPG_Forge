use super::{GameView, distance_squared};
use forge_gameplay::{AbilityTarget, GameAction, GameSession};
use std::fmt::Write as _;

pub(super) fn append(session: &GameSession, view: &mut GameView) {
    let document = session.document();
    let state = session.state();
    let (Some(definitions), Some(current)) = (&document.simulation, &state.simulation) else {
        return;
    };
    if let Some(moment) = definitions.clock.moment_at(current.elapsed_ns) {
        let _ = write!(
            view.summary,
            "\nCalendrier : jour {} · {:02} h {:02}",
            moment.day, moment.hour, moment.minute
        );
    }
    let player = &state.actors[&document.player];
    if !player.active || player.vitality == 0 {
        return;
    }
    if let Some(acquired) = definitions.actor_abilities.get(&document.player) {
        for ability in acquired
            .iter()
            .filter_map(|ability| definitions.ability(*ability))
        {
            if !session.condition(document.player, &ability.condition) {
                continue;
            }
            for actor in &document.actors {
                let target = &state.actors[&actor.id];
                if !target.active
                    || target.vitality == 0
                    || (ability.target == AbilityTarget::SelfOnly && actor.id != document.player)
                    || distance_squared(player.position, target.position) > ability.range_m.powi(2)
                {
                    continue;
                }
                view.actions.push((
                    format!(
                        "{} → {} · {} endurance",
                        ability.name, actor.name, ability.stamina_cost
                    ),
                    GameAction::UseAbility {
                        ability: ability.id,
                        target: actor.id,
                    },
                ));
            }
        }
    }
    for effect in current
        .effects
        .values()
        .filter(|effect| effect.target == document.player)
    {
        let name = definitions
            .effect(effect.effect)
            .map_or("Effet absent", |effect| effect.name.as_str());
        let seconds = effect
            .expires_at_ns
            .saturating_sub(current.elapsed_ns)
            .div_ceil(1_000_000_000);
        view.actions.push((
            format!("Annuler {name} · {seconds} s restantes"),
            GameAction::CancelEffect {
                instance: effect.id,
            },
        ));
    }
}
