//! Prepared lot D mutations; the normal action transaction owns their promotion.
use crate::temporal::{
    AbilityDefinition, AbilityEffect, AbilityTarget, ActiveEffect, EffectEndReason, EffectStacking,
    MAX_ACTIVE_EFFECTS,
};
use crate::{GameRequest, GameSession, GameState, GameplayError, GameplayEvent};
use forge_core::EntityId;

fn refused(reason: &str) -> GameplayError {
    GameplayError::ActionRefused(format!("ability: {reason}"))
}

impl GameSession {
    #[must_use]
    pub fn effective_attack_protection(&self, actor: EntityId) -> Option<u32> {
        self.attack_protection_for_state(&self.state, actor)
    }

    pub(crate) fn attack_protection_for_state(
        &self,
        state: &GameState,
        actor: EntityId,
    ) -> Option<u32> {
        let base = self.document.actor(actor)?.protection;
        let (Some(definitions), Some(current)) = (&self.document.simulation, &state.simulation)
        else {
            return Some(base);
        };
        Some(
            current
                .effects
                .values()
                .filter(|effect| {
                    effect.target == actor && effect.expires_at_ns > current.elapsed_ns
                })
                .filter_map(|active| definitions.effect(active.effect))
                .fold(base, |total, effect| {
                    total.saturating_add(effect.attack_protection_bonus)
                }),
        )
    }

    pub(crate) fn apply_ability(
        &self,
        state: &mut GameState,
        request: &GameRequest,
        ability: EntityId,
        target: EntityId,
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        let definitions = self
            .document
            .simulation
            .as_ref()
            .ok_or_else(|| refused("simulation definitions are absent"))?;
        let definition = definitions
            .ability(ability)
            .ok_or_else(|| refused("ability is absent"))?;
        self.validate_ability_use(state, request, definition, target)?;
        let mut events = Vec::new();
        for (ordinal, effect) in definition.effects.iter().enumerate() {
            match effect {
                AbilityEffect::Heal { points } => {
                    let maximum = self
                        .document
                        .actor(target)
                        .ok_or_else(|| refused("target definition is absent"))?
                        .max_vitality;
                    let recipient = state
                        .actors
                        .get_mut(&target)
                        .ok_or_else(|| refused("target disappeared"))?;
                    let amount = recipient.vitality.saturating_add(*points).min(maximum)
                        - recipient.vitality;
                    if amount > 0 {
                        recipient.vitality += amount;
                        events.push(GameplayEvent::Healed { target, amount });
                    }
                }
                AbilityEffect::Timed { effect } => {
                    events.extend(
                        self.apply_timed_effect(state, request, ability, target, *effect, ordinal)?,
                    );
                }
            }
        }
        if events.is_empty() {
            return Err(refused("ability would have no effect"));
        }
        let actor = state
            .actors
            .get_mut(&request.actor)
            .ok_or_else(|| refused("user disappeared"))?;
        actor.stamina -= definition.stamina_cost;
        for cost in &definition.item_costs {
            let remaining = actor.inventory.get(&cost.item).copied().unwrap_or(0) - cost.quantity;
            if remaining == 0 {
                actor.inventory.remove(&cost.item);
            } else {
                actor.inventory.insert(cost.item, remaining);
            }
        }
        events.insert(0, GameplayEvent::AbilityUsed { ability, target });
        Ok(events)
    }

    fn validate_ability_use(
        &self,
        state: &GameState,
        request: &GameRequest,
        definition: &AbilityDefinition,
        target: EntityId,
    ) -> Result<(), GameplayError> {
        if !self
            .document
            .simulation
            .as_ref()
            .and_then(|simulation| simulation.actor_abilities.get(&request.actor))
            .is_some_and(|abilities| abilities.contains(&definition.id))
        {
            return Err(refused("actor has not acquired this ability"));
        }
        if definition.target == AbilityTarget::SelfOnly && target != request.actor {
            return Err(refused("this ability targets its user"));
        }
        let recipient = state
            .actors
            .get(&target)
            .ok_or_else(|| refused("target is absent"))?;
        if !recipient.active || recipient.vitality == 0 {
            return Err(refused("target must be present and living"));
        }
        Self::within(state, request.actor, recipient.position, definition.range_m)?;
        if !self.condition_for_state(state, request.actor, &definition.condition) {
            return Err(refused("ability condition is not met"));
        }
        let actor = state
            .actors
            .get(&request.actor)
            .ok_or_else(|| refused("user is absent"))?;
        if !actor.active || actor.vitality == 0 {
            return Err(refused("user must be present and living"));
        }
        if actor.stamina < definition.stamina_cost {
            return Err(refused("insufficient stamina"));
        }
        for cost in &definition.item_costs {
            if actor.inventory.get(&cost.item).copied().unwrap_or(0) < cost.quantity {
                return Err(refused("insufficient items"));
            }
        }
        Ok(())
    }

    pub(crate) fn apply_timed_effect(
        &self,
        state: &mut GameState,
        request: &GameRequest,
        ability: EntityId,
        target: EntityId,
        effect: EntityId,
        ordinal: usize,
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        let definitions = self
            .document
            .simulation
            .as_ref()
            .ok_or_else(|| refused("simulation definitions are absent"))?;
        let definition = definitions
            .effect(effect)
            .ok_or_else(|| refused("timed effect is absent"))?;
        let current = state
            .simulation
            .as_mut()
            .ok_or_else(|| refused("simulation state is absent"))?;
        let mut matching = Vec::new();
        for active in current
            .effects
            .values()
            .filter(|active| active.target == target && active.expires_at_ns > current.elapsed_ns)
        {
            if active.effect == effect {
                matching.push(active.id);
            } else if definition.exclusive_with.contains(&active.effect)
                || definitions
                    .effect(active.effect)
                    .is_some_and(|other| other.exclusive_with.contains(&effect))
            {
                return Err(refused("target has an incompatible effect"));
            }
        }
        match definition.stacking {
            EffectStacking::Reject if !matching.is_empty() => {
                return Err(refused("effect is already active"));
            }
            EffectStacking::Stack { max_instances }
                if matching.len() >= usize::from(max_instances) =>
            {
                return Err(refused("effect stack limit reached"));
            }
            _ => (),
        }
        let refresh = matches!(definition.stacking, EffectStacking::Refresh);
        let retained =
            current
                .effects
                .len()
                .saturating_sub(if refresh { matching.len() } else { 0 });
        if retained >= MAX_ACTIVE_EFFECTS {
            return Err(refused("active effect quota reached"));
        }
        let expires_at_ns = current
            .elapsed_ns
            .checked_add(u64::from(definition.duration_ms) * 1_000_000)
            .ok_or_else(|| refused("effect expiration overflow"))?;
        let mut events = Vec::new();
        if refresh {
            for instance in matching {
                current.effects.remove(&instance);
                events.push(GameplayEvent::TimedEffectEnded {
                    instance,
                    reason: EffectEndReason::Refreshed,
                });
            }
        }
        let id = EntityId::deterministic(format!(
            "effect/{}/{ability}/{target}/{effect}/{ordinal}",
            request.command_id
        ));
        let active = ActiveEffect {
            id,
            effect,
            target,
            cause: request.command_id,
            applied_at_ns: current.elapsed_ns,
            expires_at_ns,
        };
        if current.effects.insert(id, active.clone()).is_some() {
            return Err(refused("effect identity reused"));
        }
        events.push(GameplayEvent::TimedEffectApplied { effect: active });
        Ok(events)
    }

    pub(crate) fn apply_cancel_effect(
        state: &mut GameState,
        actor: EntityId,
        instance: EntityId,
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        let current = state
            .simulation
            .as_mut()
            .ok_or_else(|| refused("simulation state is absent"))?;
        let effect = current
            .effects
            .get(&instance)
            .ok_or_else(|| refused("effect is no longer active"))?;
        if effect.target != actor || effect.expires_at_ns <= current.elapsed_ns {
            return Err(refused(
                "only the living target may cancel its current effect",
            ));
        }
        current.effects.remove(&instance);
        Ok(vec![GameplayEvent::TimedEffectEnded {
            instance,
            reason: EffectEndReason::Cancelled,
        }])
    }
}
