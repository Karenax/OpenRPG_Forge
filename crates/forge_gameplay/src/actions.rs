use crate::{
    ActionObservation, Condition, DialogueEffect, GameAction, GameReceipt, GameRequest,
    GameSession, GameState, GameplayError, GameplayEvent, ObjectKind, QuestStatus,
};
use forge_core::{EntityId, Vec3};

fn refused(message: &str) -> GameplayError {
    GameplayError::ActionRefused(message.into())
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct DialogueOption {
    pub id: EntityId,
    pub text: String,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct DialogueView {
    pub speaker: EntityId,
    pub node: EntityId,
    pub text: String,
    pub choices: Vec<DialogueOption>,
}

impl GameSession {
    pub fn execute(
        &mut self,
        request: &GameRequest,
        observation: ActionObservation,
    ) -> Result<GameReceipt, GameplayError> {
        if let Some(receipt) = self
            .state
            .receipts
            .iter()
            .find(|v| v.request.command_id == request.command_id)
        {
            if receipt.request != *request {
                return Err(refused("command ID reused with different parameters"));
            }
            return Ok(receipt.clone());
        }
        if request.expected_sequence != self.state.sequence {
            return Err(refused("stale game sequence"));
        }
        if self.state.requires_physics_revalidation {
            return Err(refused(
                "current physics projection must be observed before another action",
            ));
        }
        if self.state.receipts.len() >= self.document.profile.max_action_receipts {
            return Err(refused("action journal quota reached"));
        }
        let actor = self
            .state
            .actors
            .get(&request.actor)
            .ok_or_else(|| GameplayError::MissingReference(request.actor.to_string()))?;
        if actor.vitality == 0 {
            return Err(refused("actor is defeated"));
        }
        if !observation.line_of_sight {
            return Err(refused("target is occluded"));
        }
        let mut next = self.state.clone();
        let events = self.apply_action(&mut next, request, observation)?;
        if next.tick_events_used.saturating_add(events.len())
            > self.document.profile.max_events_per_tick
        {
            return Err(refused("event quota reached"));
        }
        next.tick_events_used += events.len();
        next.sequence = next
            .sequence
            .checked_add(1)
            .ok_or_else(|| refused("sequence overflow"))?;
        let receipt = GameReceipt {
            request: request.clone(),
            sequence: next.sequence,
            tick: next.tick,
            events,
        };
        next.receipts.push(receipt.clone());
        self.state = next;
        Ok(receipt)
    }

    fn apply_action(
        &self,
        state: &mut GameState,
        request: &GameRequest,
        observation: ActionObservation,
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        match request.action {
            GameAction::Pickup { object } => self.apply_pickup(state, request, object),
            GameAction::ChooseDialogue {
                speaker,
                node,
                choice,
            } => self.apply_dialogue(state, request, speaker, node, choice),
            GameAction::Attack { target } => self.apply_attack(state, request, observation, target),
            GameAction::SetDoor { object, open } => self.apply_door(state, request, object, open),
            GameAction::Push { object, impulse } | GameAction::Throw { object, impulse } => {
                self.apply_impulse(state, request, observation, object, impulse)
            }
            GameAction::Ignite { object } | GameAction::Extinguish { object } => {
                self.apply_heat(state, request, observation, object)
            }
        }
    }
    fn apply_pickup(
        &self,
        state: &mut GameState,
        request: &GameRequest,
        object: EntityId,
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        let mut events = Vec::new();
        let definition = self
            .document
            .object(object)
            .ok_or_else(|| GameplayError::MissingReference(object.to_string()))?;
        let ObjectKind::Pickup { item, quantity } = definition.kind else {
            return Err(refused("object is not a pickup"));
        };
        let current = state
            .objects
            .get(&object)
            .ok_or_else(|| GameplayError::MissingReference(object.to_string()))?;
        Self::within(
            state,
            request.actor,
            current.position,
            self.document.profile.interaction_range_m,
        )?;
        if current.consumed || current.destroyed || current.in_motion {
            return Err(refused("pickup is no longer available"));
        }
        self.add_item(state, request.actor, item, quantity)?;
        state
            .objects
            .get_mut(&object)
            .ok_or_else(|| GameplayError::MissingReference(object.to_string()))?
            .consumed = true;
        events.push(GameplayEvent::PickedUp {
            object,
            item,
            quantity,
        });

        Ok(events)
    }

    fn apply_dialogue(
        &self,
        state: &mut GameState,
        request: &GameRequest,
        speaker: EntityId,
        node: EntityId,
        choice: EntityId,
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        let mut events = Vec::new();
        let view = self.dialogue_view_for_state(state, request.actor, speaker)?;
        if view.node != node {
            return Err(refused("dialogue node is not active"));
        }
        if !view.choices.iter().any(|v| v.id == choice) {
            return Err(refused("dialogue choice is unavailable"));
        }
        let dialogue_id = self
            .document
            .actor(speaker)
            .and_then(|v| v.dialogue)
            .ok_or_else(|| refused("speaker has no dialogue"))?;
        let selected = self
            .document
            .dialogue(dialogue_id)
            .and_then(|d| d.nodes.iter().find(|n| n.id == node))
            .and_then(|n| n.choices.iter().find(|c| c.id == choice))
            .ok_or_else(|| GameplayError::MissingReference(choice.to_string()))?;
        for effect in &selected.effects {
            match *effect {
                DialogueEffect::AcceptQuest { quest } => {
                    let definition = self
                        .document
                        .quest(quest)
                        .ok_or_else(|| GameplayError::MissingReference(quest.to_string()))?;
                    if definition.giver != speaker
                        || state.quests.get(&quest) != Some(&QuestStatus::Available)
                    {
                        return Err(refused("quest cannot be accepted here"));
                    }
                    state.quests.insert(quest, QuestStatus::Active);
                    events.push(GameplayEvent::QuestChanged {
                        quest,
                        status: QuestStatus::Active,
                    });
                }
                DialogueEffect::CompleteQuest { quest } => {
                    self.complete_quest(state, request.actor, speaker, quest)?;
                    events.push(GameplayEvent::QuestChanged {
                        quest,
                        status: QuestStatus::Completed,
                    });
                }
                DialogueEffect::SetDoor { object, open } => {
                    let current = state
                        .objects
                        .get_mut(&object)
                        .ok_or_else(|| GameplayError::MissingReference(object.to_string()))?;
                    if current.destroyed || current.consumed || current.in_motion {
                        return Err(refused("door is unavailable"));
                    }
                    current.open = open;
                    events.push(GameplayEvent::DoorChanged { object, open });
                }
            }
        }
        if let Some(next) = selected.next {
            state.dialogue_cursors.insert(speaker, next);
        } else {
            state.dialogue_cursors.remove(&speaker);
        }

        Ok(events)
    }

    fn apply_attack(
        &self,
        state: &mut GameState,
        request: &GameRequest,
        observation: ActionObservation,
        target: EntityId,
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        let mut events = Vec::new();
        let target_state = state
            .actors
            .get(&target)
            .ok_or_else(|| GameplayError::MissingReference(target.to_string()))?;
        if target == request.actor || target_state.vitality == 0 {
            return Err(refused("target cannot be attacked"));
        }
        Self::within(
            state,
            request.actor,
            target_state.position,
            self.document.profile.attack_range_m,
        )?;
        if !observation.confirmed_contact {
            return Err(refused("no physical contact confirmed"));
        }
        let actor = state
            .actors
            .get_mut(&request.actor)
            .ok_or_else(|| GameplayError::MissingReference(request.actor.to_string()))?;
        let profile = &self.document.profile;
        if actor.stamina < profile.attack_stamina_cost {
            return Err(refused("insufficient stamina"));
        }
        actor.stamina -= profile.attack_stamina_cost;
        let protection = self
            .document
            .actor(target)
            .ok_or_else(|| GameplayError::MissingReference(target.to_string()))?
            .protection;
        let damage = profile.attack_damage.saturating_sub(protection);
        let victim = state
            .actors
            .get_mut(&target)
            .ok_or_else(|| refused("target disappeared"))?;
        victim.vitality = victim.vitality.saturating_sub(damage);
        events.push(GameplayEvent::Damage {
            target,
            contact: true,
            amount: damage,
        });

        Ok(events)
    }

    fn apply_door(
        &self,
        state: &mut GameState,
        request: &GameRequest,
        object: EntityId,
        open: bool,
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        let mut events = Vec::new();
        let definition = self
            .document
            .object(object)
            .ok_or_else(|| GameplayError::MissingReference(object.to_string()))?;
        if !matches!(definition.kind, ObjectKind::Door { .. }) {
            return Err(refused("object is not a door"));
        }
        let current = state
            .objects
            .get(&object)
            .ok_or_else(|| GameplayError::MissingReference(object.to_string()))?;
        Self::within(
            state,
            request.actor,
            current.position,
            self.document.profile.interaction_range_m,
        )?;
        if current.destroyed || current.in_motion {
            return Err(refused("door is destroyed"));
        }
        state
            .objects
            .get_mut(&object)
            .ok_or_else(|| refused("door disappeared"))?
            .open = open;
        events.push(GameplayEvent::DoorChanged { object, open });

        Ok(events)
    }

    fn apply_impulse(
        &self,
        state: &mut GameState,
        request: &GameRequest,
        observation: ActionObservation,
        object: EntityId,
        impulse: Vec3,
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        if !observation.confirmed_contact {
            return Err(refused("physical interaction requires confirmed contact"));
        }
        let mut events = Vec::new();
        let definition = self
            .document
            .object(object)
            .ok_or_else(|| GameplayError::MissingReference(object.to_string()))?;
        let throwing = matches!(request.action, GameAction::Throw { .. });
        if (throwing && !definition.physics.throwable)
            || (!throwing && !definition.physics.pushable)
        {
            return Err(refused("object does not support this action"));
        }
        crate::validation::position(impulse)?;
        if impulse == Vec3::ZERO {
            return Err(refused("impulse is zero"));
        }
        let current = state
            .objects
            .get(&object)
            .ok_or_else(|| GameplayError::MissingReference(object.to_string()))?;
        Self::within(
            state,
            request.actor,
            current.position,
            self.document.profile.interaction_range_m,
        )?;
        if current.destroyed || current.consumed {
            return Err(refused("object is unavailable"));
        }
        if !throwing
            && !current.released
            && self
                .document
                .objects
                .iter()
                .any(|parent| parent.physics.supports.contains(&object))
        {
            return Err(refused("object remains held by its support"));
        }
        let current = state
            .objects
            .get_mut(&object)
            .ok_or_else(|| refused("object disappeared"))?;
        current.in_motion = true;
        current.initiator = Some(request.actor);
        if throwing {
            current.released = true;
        }
        events.push(GameplayEvent::Impulse { object, impulse });

        Ok(events)
    }

    fn apply_heat(
        &self,
        state: &mut GameState,
        request: &GameRequest,
        observation: ActionObservation,
        object: EntityId,
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        let mut events = Vec::new();
        let definition = self
            .document
            .object(object)
            .ok_or_else(|| GameplayError::MissingReference(object.to_string()))?;
        let active = matches!(request.action, GameAction::Ignite { .. });
        if !active && !observation.water_contact {
            return Err(refused("extinguishing requires observed water contact"));
        }
        let current = state
            .objects
            .get(&object)
            .ok_or_else(|| GameplayError::MissingReference(object.to_string()))?;
        Self::within(
            state,
            request.actor,
            current.position,
            self.document.profile.interaction_range_m,
        )?;
        if current.destroyed
            || current.consumed
            || current.in_motion
            || (active && !definition.physics.flammable)
        {
            return Err(refused("object cannot burn"));
        }
        state
            .objects
            .get_mut(&object)
            .ok_or_else(|| refused("object disappeared"))?
            .burning = active;
        events.push(GameplayEvent::Burning { object, active });

        Ok(events)
    }

    fn complete_quest(
        &self,
        state: &mut GameState,
        actor: EntityId,
        speaker: EntityId,
        quest: EntityId,
    ) -> Result<(), GameplayError> {
        let definition = self
            .document
            .quest(quest)
            .ok_or_else(|| GameplayError::MissingReference(quest.to_string()))?;
        if definition.giver != speaker || state.quests.get(&quest) != Some(&QuestStatus::Active) {
            return Err(refused("quest is not active for this speaker"));
        }
        let inventory = &mut state
            .actors
            .get_mut(&actor)
            .ok_or_else(|| GameplayError::MissingReference(actor.to_string()))?
            .inventory;
        let quantity = inventory
            .get(&definition.required_item)
            .copied()
            .unwrap_or(0);
        if quantity < definition.required_quantity {
            return Err(refused("quest item missing"));
        }
        let remaining = quantity - definition.required_quantity;
        if remaining == 0 {
            inventory.remove(&definition.required_item);
        } else {
            inventory.insert(definition.required_item, remaining);
        }
        if let Some(item) = definition.reward_item {
            self.add_item(state, actor, item, definition.reward_quantity)?;
        }
        state.quests.insert(quest, QuestStatus::Completed);
        Ok(())
    }

    fn add_item(
        &self,
        state: &mut GameState,
        actor: EntityId,
        item: EntityId,
        quantity: u32,
    ) -> Result<(), GameplayError> {
        let definition = self
            .document
            .item(item)
            .ok_or_else(|| GameplayError::MissingReference(item.to_string()))?;
        let capacity = self
            .document
            .actor(actor)
            .ok_or_else(|| GameplayError::MissingReference(actor.to_string()))?
            .inventory_capacity;
        let inventory = &mut state
            .actors
            .get_mut(&actor)
            .ok_or_else(|| GameplayError::MissingReference(actor.to_string()))?
            .inventory;
        let current = inventory.get(&item).copied().unwrap_or(0);
        let next = current
            .checked_add(quantity)
            .ok_or_else(|| refused("inventory quantity overflow"))?;
        let total: u64 = inventory.values().map(|v| u64::from(*v)).sum();
        if next > definition.max_stack || total + u64::from(quantity) > u64::from(capacity) {
            return Err(refused("inventory full"));
        }
        inventory.insert(item, next);
        Ok(())
    }

    pub(crate) fn within(
        state: &GameState,
        actor: EntityId,
        target: Vec3,
        range: f32,
    ) -> Result<(), GameplayError> {
        let source = state
            .actors
            .get(&actor)
            .ok_or_else(|| GameplayError::MissingReference(actor.to_string()))?
            .position;
        let distance = (source.x - target.x).powi(2)
            + (source.y - target.y).powi(2)
            + (source.z - target.z).powi(2);
        if distance > range * range {
            return Err(refused("target out of range"));
        }
        Ok(())
    }

    #[must_use]
    pub fn condition(&self, actor: EntityId, condition: &Condition) -> bool {
        Self::condition_for_state(&self.state, actor, condition)
    }

    fn condition_for_state(state: &GameState, actor: EntityId, condition: &Condition) -> bool {
        match *condition {
            Condition::Always => true,
            Condition::Quest { quest, status } => state.quests.get(&quest) == Some(&status),
            Condition::HasItem { item, quantity } => state
                .actors
                .get(&actor)
                .and_then(|a| a.inventory.get(&item))
                .is_some_and(|v| *v >= quantity),
            Condition::ActorDefeated { actor } => {
                state.actors.get(&actor).is_some_and(|v| v.vitality == 0)
            }
            Condition::ObjectDestroyed { object } => {
                state.objects.get(&object).is_some_and(|v| v.destroyed)
            }
        }
    }

    pub fn dialogue_view(
        &self,
        actor: EntityId,
        speaker: EntityId,
    ) -> Result<DialogueView, GameplayError> {
        self.dialogue_view_for_state(&self.state, actor, speaker)
    }

    fn dialogue_view_for_state(
        &self,
        state: &GameState,
        actor: EntityId,
        speaker: EntityId,
    ) -> Result<DialogueView, GameplayError> {
        let speaker_state = state
            .actors
            .get(&speaker)
            .ok_or_else(|| GameplayError::MissingReference(speaker.to_string()))?;
        if speaker_state.vitality == 0 {
            return Err(refused("speaker is defeated"));
        }
        Self::within(
            state,
            actor,
            speaker_state.position,
            self.document.profile.interaction_range_m,
        )?;
        let definition = self
            .document
            .actor(speaker)
            .and_then(|v| v.dialogue)
            .and_then(|id| self.document.dialogue(id))
            .ok_or_else(|| refused("speaker has no dialogue"))?;
        let node_id = state
            .dialogue_cursors
            .get(&speaker)
            .copied()
            .unwrap_or(definition.start);
        let node = definition
            .nodes
            .iter()
            .find(|v| v.id == node_id)
            .ok_or_else(|| GameplayError::MissingReference(node_id.to_string()))?;
        Ok(DialogueView {
            speaker,
            node: node_id,
            text: node.text.clone(),
            choices: node
                .choices
                .iter()
                .filter(|c| Self::condition_for_state(state, actor, &c.condition))
                .map(|c| DialogueOption {
                    id: c.id,
                    text: c.text.clone(),
                })
                .collect(),
        })
    }
}
