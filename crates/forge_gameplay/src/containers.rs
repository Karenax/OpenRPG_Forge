//! Shared item-unit arithmetic for actors and authored containers.
use crate::{
    GameAction, GameSession, GameState, GameplayCapability, GameplayDocument, GameplayError,
    GameplayEvent, MAX_GAMEPLAY_ENTRIES, ObjectKind,
};
use forge_core::EntityId;
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};

pub const MAX_INVENTORY_UNITS: u32 = 10_000;

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ContainerDefinition {
    pub capacity_units: u32,
    pub initial_items: BTreeMap<EntityId, u32>,
    /// Empty accepts every declared item; duplicates and absent references are invalid.
    pub accepts: Vec<EntityId>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub key: Option<EntityId>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ContainerState {
    pub items: BTreeMap<EntityId, u32>,
    pub locked: bool,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum TransferDirection {
    Take,
    Put,
}

impl ContainerDefinition {
    #[must_use]
    pub fn accepts(&self, item: EntityId) -> bool {
        self.accepts.is_empty() || self.accepts.contains(&item)
    }

    #[must_use]
    pub fn initial_state(&self) -> ContainerState {
        ContainerState {
            items: self.initial_items.clone(),
            locked: self.key.is_some(),
        }
    }

    pub fn validate(&self, document: &GameplayDocument) -> Result<(), GameplayError> {
        let invalid = |reason: &str| GameplayError::InvalidDocument(format!("container: {reason}"));
        if self.capacity_units > MAX_INVENTORY_UNITS
            || self.initial_items.len() > MAX_GAMEPLAY_ENTRIES
            || self.accepts.len() > MAX_GAMEPLAY_ENTRIES
        {
            return Err(invalid("capacity or item limit exceeded"));
        }
        let filters: BTreeSet<_> = self.accepts.iter().copied().collect();
        if filters.len() != self.accepts.len() {
            return Err(invalid("duplicate item filter"));
        }
        for item in self.accepts.iter().copied().chain(self.key) {
            if document.item(item).is_none() {
                return Err(GameplayError::MissingReference(format!(
                    "container.item/{item}"
                )));
            }
        }
        let mut checked = BTreeMap::new();
        for (&item, &quantity) in &self.initial_items {
            if !self.accepts(item) {
                return Err(invalid("initial item excluded by filter"));
            }
            insert_units(document, &mut checked, self.capacity_units, item, quantity)
                .map_err(|error| invalid(&error.to_string()))?;
        }
        Ok(())
    }
}

fn refused(reason: &str) -> GameplayError {
    GameplayError::ActionRefused(reason.into())
}

fn checked_insert(
    document: &GameplayDocument,
    inventory: &BTreeMap<EntityId, u32>,
    capacity: u32,
    item: EntityId,
    quantity: u32,
) -> Result<u32, GameplayError> {
    if quantity == 0 {
        return Err(refused("item quantity must be positive"));
    }
    let definition = document
        .item(item)
        .ok_or_else(|| GameplayError::MissingReference(item.to_string()))?;
    let next = inventory
        .get(&item)
        .copied()
        .unwrap_or_default()
        .checked_add(quantity)
        .ok_or_else(|| refused("inventory quantity overflow"))?;
    let total: u64 = inventory.values().map(|value| u64::from(*value)).sum();
    if next > definition.max_stack || total + u64::from(quantity) > u64::from(capacity) {
        return Err(refused("inventory full"));
    }
    Ok(next)
}

pub(crate) fn insert_units(
    document: &GameplayDocument,
    inventory: &mut BTreeMap<EntityId, u32>,
    capacity: u32,
    item: EntityId,
    quantity: u32,
) -> Result<(), GameplayError> {
    let next = checked_insert(document, inventory, capacity, item, quantity)?;
    inventory.insert(item, next);
    Ok(())
}

/// All checks precede both writes. This primitive also verifies the saved item ledger.
pub(crate) fn transfer_units(
    document: &GameplayDocument,
    source: &mut BTreeMap<EntityId, u32>,
    destination: &mut BTreeMap<EntityId, u32>,
    capacity: u32,
    item: EntityId,
    quantity: u32,
) -> Result<(), GameplayError> {
    let remaining = source
        .get(&item)
        .copied()
        .unwrap_or_default()
        .checked_sub(quantity)
        .ok_or_else(|| refused("source has insufficient items"))?;
    let next = checked_insert(document, destination, capacity, item, quantity)?;
    if remaining == 0 {
        source.remove(&item);
    } else {
        source.insert(item, remaining);
    }
    destination.insert(item, next);
    Ok(())
}

impl GameplayDocument {
    /// Catalog edits preserve stable IDs and validate every authored reference and stack.
    pub fn with_item_definition(
        &self,
        definition: crate::ItemDefinition,
    ) -> Result<Self, GameplayError> {
        let mut candidate = self.clone();
        if let Some(current) = candidate
            .items
            .iter_mut()
            .find(|item| item.id == definition.id)
        {
            *current = definition;
        } else {
            candidate.items.push(definition);
        }
        let needed = candidate.used_capabilities();
        if let Some(configuration) = &mut candidate.creator {
            let mut required: BTreeSet<_> = configuration
                .required_capabilities
                .iter()
                .copied()
                .collect();
            required.extend(needed);
            configuration.required_capabilities = required.into_iter().collect();
        }
        candidate.validate()?;
        Ok(candidate)
    }

    pub fn without_item_definition(&self, item: EntityId) -> Result<Self, GameplayError> {
        let mut candidate = self.clone();
        if !candidate.items.iter().any(|entry| entry.id == item) {
            return Err(GameplayError::MissingReference(item.to_string()));
        }
        candidate.items.retain(|entry| entry.id != item);
        candidate.validate()?;
        Ok(candidate)
    }

    #[must_use]
    pub fn has_containers(&self) -> bool {
        self.objects
            .iter()
            .any(|object| matches!(object.kind, ObjectKind::Container { .. }))
    }

    #[must_use]
    pub fn container_definition(&self, object: EntityId) -> Option<&ContainerDefinition> {
        match &self.object(object)?.kind {
            ObjectKind::Container { definition } => Some(definition),
            _ => None,
        }
    }

    /// A prop becomes a container without replacing its identity, pose or physics.
    pub fn with_container_definition(
        &self,
        object: EntityId,
        definition: ContainerDefinition,
    ) -> Result<Self, GameplayError> {
        let mut candidate = self.clone();
        let target = candidate
            .objects
            .iter_mut()
            .find(|entry| entry.id == object)
            .ok_or_else(|| GameplayError::MissingReference(object.to_string()))?;
        if !matches!(target.kind, ObjectKind::Prop | ObjectKind::Container { .. }) {
            return Err(GameplayError::InvalidDocument(
                "only a prop or container can receive container contents".into(),
            ));
        }
        target.kind = ObjectKind::Container { definition };
        let mut configuration = candidate.creator_configuration();
        let mut required: BTreeSet<_> = configuration
            .required_capabilities
            .iter()
            .copied()
            .collect();
        required.extend(candidate.used_capabilities());
        required.insert(GameplayCapability::Containers);
        configuration.required_capabilities = required.into_iter().collect();
        candidate.with_creator_configuration(configuration)
    }
}

impl GameSession {
    fn available_container<'a>(
        &self,
        state: &'a GameState,
        actor: EntityId,
        object: EntityId,
    ) -> Result<&'a ContainerState, GameplayError> {
        let current = state
            .objects
            .get(&object)
            .ok_or_else(|| GameplayError::MissingReference(object.to_string()))?;
        Self::within(
            state,
            actor,
            current.position,
            self.document.profile.interaction_range_m,
        )?;
        if current.consumed || current.destroyed || current.in_motion {
            return Err(refused("container is unavailable"));
        }
        current
            .container
            .as_ref()
            .ok_or_else(|| refused("object is not a container"))
    }

    pub(crate) fn apply_container_transfer(
        &self,
        state: &mut GameState,
        actor: EntityId,
        object: EntityId,
        item: EntityId,
        quantity: u32,
        direction: TransferDirection,
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        let definition = self
            .document
            .container_definition(object)
            .ok_or_else(|| refused("object is not a container"))?;
        if self.available_container(state, actor, object)?.locked {
            return Err(refused("container is locked"));
        }
        if direction == TransferDirection::Put && !definition.accepts(item) {
            return Err(refused("item excluded by container filter"));
        }
        let actor_capacity = self
            .document
            .actor(actor)
            .ok_or_else(|| GameplayError::MissingReference(actor.to_string()))?
            .inventory_capacity;
        let actor_inventory = &mut state
            .actors
            .get_mut(&actor)
            .ok_or_else(|| GameplayError::MissingReference(actor.to_string()))?
            .inventory;
        let container_inventory = &mut state
            .objects
            .get_mut(&object)
            .and_then(|current| current.container.as_mut())
            .ok_or_else(|| refused("container disappeared"))?
            .items;
        match direction {
            TransferDirection::Take => transfer_units(
                &self.document,
                container_inventory,
                actor_inventory,
                actor_capacity,
                item,
                quantity,
            )?,
            TransferDirection::Put => transfer_units(
                &self.document,
                actor_inventory,
                container_inventory,
                definition.capacity_units,
                item,
                quantity,
            )?,
        }
        Ok(vec![GameplayEvent::ItemTransferred {
            object,
            actor,
            item,
            quantity,
            direction,
        }])
    }

    pub(crate) fn apply_container_lock(
        &self,
        state: &mut GameState,
        actor: EntityId,
        object: EntityId,
        locked: bool,
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        let definition = self
            .document
            .container_definition(object)
            .ok_or_else(|| refused("object is not a container"))?;
        let current = self.available_container(state, actor, object)?;
        if current.locked == locked {
            return Err(refused("container lock is already in that state"));
        }
        let key = definition
            .key
            .ok_or_else(|| refused("container has no lock"))?;
        if state
            .actors
            .get(&actor)
            .and_then(|a| a.inventory.get(&key))
            .is_none_or(|quantity| *quantity == 0)
        {
            return Err(refused("container key is missing"));
        }
        state
            .objects
            .get_mut(&object)
            .and_then(|current| current.container.as_mut())
            .ok_or_else(|| refused("container disappeared"))?
            .locked = locked;
        Ok(vec![GameplayEvent::ContainerLocked { object, locked }])
    }

    /// Presentation options use current quantities and the same unit/stack limits.
    /// Collision visibility and all quotas are checked again by `execute`.
    #[must_use]
    pub fn container_actions(&self, actor: EntityId, object: EntityId) -> Vec<GameAction> {
        let Some(definition) = self.document.container_definition(object) else {
            return Vec::new();
        };
        let Ok(container) = self.available_container(&self.state, actor, object) else {
            return Vec::new();
        };
        let Some(actor_state) = self.state.actors.get(&actor) else {
            return Vec::new();
        };
        let Some(actor_definition) = self.document.actor(actor) else {
            return Vec::new();
        };
        let mut actions = Vec::new();
        if container.locked
            || definition.key.is_some_and(|key| {
                actor_state
                    .inventory
                    .get(&key)
                    .is_some_and(|quantity| *quantity > 0)
            })
        {
            actions.push(GameAction::SetContainerLocked {
                object,
                locked: !container.locked,
            });
        }
        if container.locked {
            return actions;
        }
        for (source, destination, capacity, direction) in [
            (
                &container.items,
                &actor_state.inventory,
                actor_definition.inventory_capacity,
                TransferDirection::Take,
            ),
            (
                &actor_state.inventory,
                &container.items,
                definition.capacity_units,
                TransferDirection::Put,
            ),
        ] {
            let used: u64 = destination
                .values()
                .map(|quantity| u64::from(*quantity))
                .sum();
            let free = u64::from(capacity).saturating_sub(used);
            for (&item, &quantity) in source {
                if direction == TransferDirection::Put && !definition.accepts(item) {
                    continue;
                }
                let Some(item_definition) = self.document.item(item) else {
                    continue;
                };
                let stack_free = item_definition
                    .max_stack
                    .saturating_sub(destination.get(&item).copied().unwrap_or_default());
                let amount = u64::from(quantity.min(stack_free)).min(free);
                let Ok(amount) = u32::try_from(amount) else {
                    continue;
                };
                if amount == 0 {
                    continue;
                }
                actions.push(GameAction::TransferItem {
                    object,
                    item,
                    quantity: 1,
                    direction,
                });
                if amount > 1 {
                    actions.push(GameAction::TransferItem {
                        object,
                        item,
                        quantity: amount,
                        direction,
                    });
                }
            }
        }
        actions
    }

    /// User-requestable transfers, including attempts that execution may refuse.
    /// Unlike `container_actions`, full piles are not silently clipped or hidden
    /// by capacity or item filters. Execution still owns every validation and
    /// preserves the whole state on refusal; this method only prepares choices.
    #[must_use]
    pub fn container_transfer_requests(
        &self,
        actor: EntityId,
        object: EntityId,
    ) -> Vec<GameAction> {
        let mut actions = self.container_actions(actor, object);
        let Ok(container) = self.available_container(&self.state, actor, object) else {
            return actions;
        };
        let Some(actor_state) = self.state.actors.get(&actor) else {
            return actions;
        };
        if container.locked {
            return actions;
        }
        let mut offered: BTreeSet<_> = actions
            .iter()
            .filter_map(|action| match action {
                GameAction::TransferItem {
                    item,
                    quantity,
                    direction,
                    ..
                } => Some((*item, *quantity, *direction == TransferDirection::Put)),
                _ => None,
            })
            .collect();
        for (inventory, direction) in [
            (&container.items, TransferDirection::Take),
            (&actor_state.inventory, TransferDirection::Put),
        ] {
            for (&item, &quantity) in inventory {
                for amount in [1, quantity] {
                    let request = GameAction::TransferItem {
                        object,
                        item,
                        quantity: amount,
                        direction,
                    };
                    if quantity > 0
                        && offered.insert((item, amount, direction == TransferDirection::Put))
                    {
                        actions.push(request);
                    }
                }
            }
        }
        actions
    }

    /// Shared authored-item names, quantities and unit labels for both presentations.
    #[must_use]
    pub fn container_action_label(&self, action: &GameAction) -> Option<String> {
        match *action {
            GameAction::TransferItem {
                object,
                item,
                quantity,
                direction,
            } => Some(format!(
                "{} {} × {quantity} — {}",
                if direction == TransferDirection::Take {
                    "Prendre"
                } else {
                    "Déposer"
                },
                self.document.item(item)?.name,
                self.document.object(object)?.name
            )),
            GameAction::SetContainerLocked { object, locked } => Some(format!(
                "{} {}",
                if locked {
                    "Verrouiller"
                } else {
                    "Déverrouiller"
                },
                self.document.object(object)?.name
            )),
            _ => None,
        }
    }

    #[must_use]
    pub fn container_summary(&self, object: EntityId) -> Option<String> {
        let definition = self.document.container_definition(object)?;
        let state = self.state.objects.get(&object)?.container.as_ref()?;
        let name = &self.document.object(object)?.name;
        if state.locked {
            return Some(format!("{name} · verrouillé, clé requise"));
        }
        let used: u64 = state
            .items
            .values()
            .map(|quantity| u64::from(*quantity))
            .sum();
        Some(format!(
            "{name} · {used}/{} unités d’items",
            definition.capacity_units
        ))
    }
}
