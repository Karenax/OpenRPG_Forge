//! Read-only item conservation proof derived from the retained action journal.
//! No positions, simulation clock or second executable game state are stored here.
use crate::containers::{insert_units, transfer_units};
use crate::{
    ContainerState, DialogueEffect, GameAction, GameReceipt, GameSession, GameplayDocument,
    GameplayError, GameplayEvent, ObjectKind, QuestStatus, TransferDirection,
};
use forge_core::EntityId;
use std::collections::{BTreeMap, BTreeSet};

type Inventory = BTreeMap<EntityId, u32>;

mod temporal;

fn invalid(reason: impl Into<String>) -> GameplayError {
    GameplayError::IncompatibleSave(format!("item ledger: {}", reason.into()))
}

fn is_item_event(event: &GameplayEvent) -> bool {
    matches!(
        event,
        GameplayEvent::PickedUp { .. }
            | GameplayEvent::QuestChanged { .. }
            | GameplayEvent::ItemTransferred { .. }
            | GameplayEvent::ContainerLocked { .. }
    )
}

struct ItemLedger<'a> {
    document: &'a GameplayDocument,
    actors: BTreeMap<EntityId, Inventory>,
    containers: BTreeMap<EntityId, ContainerState>,
    quests: BTreeMap<EntityId, QuestStatus>,
    pickups: BTreeSet<EntityId>,
}

impl<'a> ItemLedger<'a> {
    fn new(document: &'a GameplayDocument) -> Self {
        Self {
            document,
            actors: document
                .actors
                .iter()
                .map(|actor| (actor.id, Inventory::new()))
                .collect(),
            containers: document
                .objects
                .iter()
                .filter_map(|object| {
                    document
                        .container_definition(object.id)
                        .map(|definition| (object.id, definition.initial_state()))
                })
                .collect(),
            quests: document
                .quests
                .iter()
                .map(|quest| (quest.id, QuestStatus::Available))
                .collect(),
            pickups: BTreeSet::new(),
        }
    }

    fn give(
        &mut self,
        actor: EntityId,
        item: EntityId,
        quantity: u32,
    ) -> Result<(), GameplayError> {
        let capacity = self
            .document
            .actor(actor)
            .ok_or_else(|| invalid("actor absent"))?
            .inventory_capacity;
        insert_units(
            self.document,
            self.actors
                .get_mut(&actor)
                .ok_or_else(|| invalid("actor absent"))?,
            capacity,
            item,
            quantity,
        )
    }

    fn spend(
        &mut self,
        actor: EntityId,
        item: EntityId,
        quantity: u32,
    ) -> Result<(), GameplayError> {
        let inventory = self
            .actors
            .get_mut(&actor)
            .ok_or_else(|| invalid("actor absent"))?;
        let remaining = inventory
            .get(&item)
            .copied()
            .unwrap_or_default()
            .checked_sub(quantity)
            .ok_or_else(|| invalid("quest consumes unavailable items"))?;
        if remaining == 0 {
            inventory.remove(&item);
        } else {
            inventory.insert(item, remaining);
        }
        Ok(())
    }

    fn dialogue(
        &mut self,
        actor: EntityId,
        speaker: EntityId,
        node: EntityId,
        choice: EntityId,
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        let document = self.document;
        let selected = document
            .actor(speaker)
            .and_then(|a| a.dialogue)
            .and_then(|id| document.dialogue(id))
            .and_then(|d| d.nodes.iter().find(|n| n.id == node))
            .and_then(|n| n.choices.iter().find(|c| c.id == choice))
            .ok_or_else(|| invalid("dialogue source absent"))?;
        let mut events = Vec::new();
        for effect in &selected.effects {
            let (quest, expected, next) = match *effect {
                DialogueEffect::AcceptQuest { quest } => {
                    (quest, QuestStatus::Available, QuestStatus::Active)
                }
                DialogueEffect::CompleteQuest { quest }
                | DialogueEffect::CompleteQuestSolution { quest, .. } => {
                    (quest, QuestStatus::Active, QuestStatus::Completed)
                }
                DialogueEffect::FailQuest { quest } => {
                    (quest, QuestStatus::Active, QuestStatus::Failed)
                }
                DialogueEffect::SetDoor { .. } | DialogueEffect::SetVariable { .. } => continue,
            };
            let definition = document
                .quest(quest)
                .ok_or_else(|| invalid("quest source absent"))?;
            if definition.giver != speaker || self.quests.get(&quest) != Some(&expected) {
                return Err(invalid("quest transition does not match its source"));
            }
            if next == QuestStatus::Active
                && document.quest_flow(quest).is_some_and(|flow| {
                    flow.prerequisites
                        .iter()
                        .any(|id| self.quests.get(id) != Some(&QuestStatus::Completed))
                })
            {
                return Err(invalid("quest prerequisites were not completed"));
            }
            if next == QuestStatus::Completed {
                if let DialogueEffect::CompleteQuestSolution { solution, .. } = *effect {
                    let solution = document
                        .quest_solution(quest, solution)
                        .ok_or_else(|| invalid("quest solution source absent"))?;
                    for cost in &solution.consume {
                        self.spend(actor, cost.item, cost.quantity)?;
                    }
                } else {
                    if document.quest_flow(quest).is_some() {
                        return Err(invalid("quest requires explicit solution"));
                    }
                    self.spend(
                        actor,
                        definition.required_item,
                        definition.required_quantity,
                    )?;
                }
                if let Some(item) = definition.reward_item {
                    self.give(actor, item, definition.reward_quantity)?;
                }
            }
            self.quests.insert(quest, next);
            events.push(GameplayEvent::QuestChanged {
                quest,
                status: next,
            });
        }
        Ok(events)
    }

    fn transfer(
        &mut self,
        actor: EntityId,
        object: EntityId,
        item: EntityId,
        quantity: u32,
        direction: TransferDirection,
    ) -> Result<(), GameplayError> {
        let definition = self
            .document
            .container_definition(object)
            .ok_or_else(|| invalid("container source absent"))?;
        let container = self
            .containers
            .get_mut(&object)
            .ok_or_else(|| invalid("container absent"))?;
        if container.locked || (direction == TransferDirection::Put && !definition.accepts(item)) {
            return Err(invalid("transfer violates lock or filter"));
        }
        let actor_capacity = self
            .document
            .actor(actor)
            .ok_or_else(|| invalid("actor absent"))?
            .inventory_capacity;
        let inventory = self
            .actors
            .get_mut(&actor)
            .ok_or_else(|| invalid("actor absent"))?;
        match direction {
            TransferDirection::Take => transfer_units(
                self.document,
                &mut container.items,
                inventory,
                actor_capacity,
                item,
                quantity,
            ),
            TransferDirection::Put => transfer_units(
                self.document,
                inventory,
                &mut container.items,
                definition.capacity_units,
                item,
                quantity,
            ),
        }
    }

    fn apply(&mut self, receipt: &GameReceipt) -> Result<(), GameplayError> {
        let actor = receipt.request.actor;
        let expected = match receipt.request.action {
            GameAction::Pickup { object } => {
                let ObjectKind::Pickup { item, quantity } = self
                    .document
                    .object(object)
                    .ok_or_else(|| invalid("pickup absent"))?
                    .kind
                else {
                    return Err(invalid("source is not a pickup"));
                };
                if !self.pickups.insert(object) {
                    return Err(invalid("pickup repeated"));
                }
                self.give(actor, item, quantity)?;
                vec![GameplayEvent::PickedUp {
                    object,
                    item,
                    quantity,
                }]
            }
            GameAction::ChooseDialogue {
                speaker,
                node,
                choice,
            } => self.dialogue(actor, speaker, node, choice)?,
            GameAction::TransferItem {
                object,
                item,
                quantity,
                direction,
            } => {
                self.transfer(actor, object, item, quantity, direction)?;
                vec![GameplayEvent::ItemTransferred {
                    object,
                    actor,
                    item,
                    quantity,
                    direction,
                }]
            }
            GameAction::SetContainerLocked { object, locked } => {
                let key = self
                    .document
                    .container_definition(object)
                    .and_then(|d| d.key)
                    .ok_or_else(|| invalid("container lock absent"))?;
                if self
                    .actors
                    .get(&actor)
                    .and_then(|items| items.get(&key))
                    .is_none_or(|quantity| *quantity == 0)
                {
                    return Err(invalid("lock action has no possessed key"));
                }
                let container = self
                    .containers
                    .get_mut(&object)
                    .ok_or_else(|| invalid("container absent"))?;
                if container.locked == locked {
                    return Err(invalid("lock action repeated"));
                }
                container.locked = locked;
                vec![GameplayEvent::ContainerLocked { object, locked }]
            }
            GameAction::UseAbility { ability, .. } => {
                self.spend_ability_costs(actor, ability)?;
                Vec::new()
            }
            _ => Vec::new(),
        };
        let actual: Vec<_> = receipt
            .events
            .iter()
            .filter(|event| is_item_event(event))
            .cloned()
            .collect();
        if actual != expected {
            return Err(invalid("events do not match their action and source"));
        }
        Ok(())
    }
}

impl GameSession {
    pub(crate) fn validate_inventory_ledger(&self) -> Result<(), GameplayError> {
        if !self.document.has_containers() && self.document.narrative.is_none() {
            return Ok(());
        }
        if self
            .state
            .contacts
            .values()
            .flat_map(|contact| &contact.events)
            .chain(self.state.last_frame.iter().flat_map(|frame| &frame.events))
            .any(is_item_event)
        {
            return Err(invalid(
                "item event outside an authoritative action receipt",
            ));
        }
        let mut ledger = ItemLedger::new(&self.document);
        for receipt in &self.state.receipts {
            ledger
                .apply(receipt)
                .map_err(|error| invalid(error.to_string()))?;
        }
        for (id, inventory) in &ledger.actors {
            if self
                .state
                .actors
                .get(id)
                .is_none_or(|actor| actor.inventory != *inventory)
            {
                return Err(invalid("actor inventory differs from retained actions"));
            }
        }
        for (id, container) in &ledger.containers {
            if self
                .state
                .objects
                .get(id)
                .and_then(|object| object.container.as_ref())
                != Some(container)
            {
                return Err(invalid(
                    "container inventory or lock differs from retained actions",
                ));
            }
        }
        for object in &self.document.objects {
            if matches!(object.kind, ObjectKind::Pickup { .. })
                && self
                    .state
                    .objects
                    .get(&object.id)
                    .is_none_or(|current| current.consumed != ledger.pickups.contains(&object.id))
            {
                return Err(invalid("pickup consumption differs from retained actions"));
            }
        }
        if ledger.quests != self.state.quests {
            return Err(invalid("quest states differ from retained actions"));
        }
        Ok(())
    }
}
