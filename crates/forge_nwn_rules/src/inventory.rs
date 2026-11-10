//! Typed items, stacks, equipment slots and transactional inventory moves.

use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};
use std::fmt;

pub const INVENTORY_SCHEMA: &str = "forge.nwn-inventory@1";
pub const MAX_INSTANCES: usize = 512;
pub const MAX_STACK: u16 = 99;

#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub enum EquipmentSlot {
    Weapon,
    Armor,
    Shield,
    Helmet,
    Cloak,
    Gloves,
    Boots,
    Ring,
    Amulet,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ItemProperty {
    pub id: String,
    pub label: String,
    pub value: i32,
    pub known: bool,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ItemDefinition {
    pub id: String,
    pub name: String,
    pub slot: Option<EquipmentSlot>,
    pub weight_kg: f32,
    pub stack_size: u16,
    pub properties: Vec<ItemProperty>,
    pub base_value: u32,
    pub source: String,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ItemInstance {
    pub instance_id: String,
    pub definition_id: String,
    pub quantity: u16,
    pub equipped_slot: Option<EquipmentSlot>,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ItemCatalog {
    pub definitions: BTreeMap<String, ItemDefinition>,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Inventory {
    pub schema_version: String,
    pub items: BTreeMap<String, ItemInstance>,
    pub next_instance: u64,
    pub max_weight_kg: f32,
}

#[derive(Debug, Clone, Copy, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct EquipmentBonuses {
    pub attack_bonus: i32,
    pub damage_bonus: i32,
    pub armor_bonus: i32,
    pub shield_bonus: i32,
    pub natural_bonus: i32,
}

#[derive(Debug, Clone, PartialEq)]
pub enum InventoryError {
    UnknownItem(String),
    InvalidDefinition(String),
    EmptyQuantity,
    StackLimit(u16),
    Capacity(usize),
    Weight {
        current: f32,
        added: f32,
        max: f32,
    },
    UnknownInstance(String),
    SlotMismatch {
        item: String,
        slot: Option<EquipmentSlot>,
    },
    DuplicateInstance(String),
    Corrupt(String),
}

impl fmt::Display for InventoryError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::UnknownItem(id) => write!(formatter, "unknown item definition {id}"),
            Self::InvalidDefinition(id) => write!(formatter, "invalid item definition {id}"),
            Self::EmptyQuantity => formatter.write_str("item quantity must be at least one"),
            Self::StackLimit(limit) => write!(formatter, "item stack limit exceeded ({limit})"),
            Self::Capacity(limit) => {
                write!(formatter, "inventory instance limit exceeded ({limit})")
            }
            Self::Weight {
                current,
                added,
                max,
            } => {
                write!(
                    formatter,
                    "inventory weight {current}+{added} exceeds {max} kg"
                )
            }
            Self::UnknownInstance(id) => write!(formatter, "unknown item instance {id}"),
            Self::SlotMismatch { item, slot } => {
                write!(formatter, "item {item} cannot be equipped in {slot:?}")
            }
            Self::DuplicateInstance(id) => write!(formatter, "duplicate item instance {id}"),
            Self::Corrupt(detail) => write!(formatter, "corrupt inventory: {detail}"),
        }
    }
}

impl std::error::Error for InventoryError {}

impl ItemDefinition {
    pub fn validate(&self) -> Result<(), InventoryError> {
        if self.id.trim().is_empty() || self.name.trim().is_empty() || self.source.trim().is_empty()
        {
            return Err(InventoryError::InvalidDefinition(self.id.clone()));
        }
        if !self.weight_kg.is_finite() || self.weight_kg < 0.0 {
            return Err(InventoryError::InvalidDefinition(self.id.clone()));
        }
        if self.stack_size == 0 || self.stack_size > MAX_STACK {
            return Err(InventoryError::InvalidDefinition(self.id.clone()));
        }
        Ok(())
    }
}

impl ItemCatalog {
    pub fn add(&mut self, definition: ItemDefinition) -> Result<(), InventoryError> {
        definition.validate()?;
        self.definitions.insert(definition.id.clone(), definition);
        Ok(())
    }

    #[must_use]
    pub fn get(&self, id: &str) -> Option<&ItemDefinition> {
        self.definitions.get(id)
    }

    pub fn synthetic() -> Result<Self, InventoryError> {
        let mut catalog = Self {
            definitions: BTreeMap::new(),
        };
        for definition in [
            ItemDefinition {
                id: "longsword".to_owned(),
                name: "Épée longue".to_owned(),
                slot: Some(EquipmentSlot::Weapon),
                weight_kg: 2.0,
                stack_size: 1,
                properties: vec![
                    ItemProperty {
                        id: "attack-bonus".to_owned(),
                        label: "+1 attaque".to_owned(),
                        value: 1,
                        known: true,
                    },
                    ItemProperty {
                        id: "damage-bonus".to_owned(),
                        label: "+1 dégâts".to_owned(),
                        value: 1,
                        known: true,
                    },
                ],
                base_value: 15,
                source: "synthetic M4 fixture".to_owned(),
            },
            ItemDefinition {
                id: "chain-shirt".to_owned(),
                name: "Chemise de mailles".to_owned(),
                slot: Some(EquipmentSlot::Armor),
                weight_kg: 10.0,
                stack_size: 1,
                properties: vec![ItemProperty {
                    id: "armor-bonus".to_owned(),
                    label: "+4 armure".to_owned(),
                    value: 4,
                    known: true,
                }],
                base_value: 100,
                source: "synthetic M4 fixture".to_owned(),
            },
            ItemDefinition {
                id: "light-shield".to_owned(),
                name: "Petit bouclier".to_owned(),
                slot: Some(EquipmentSlot::Shield),
                weight_kg: 3.0,
                stack_size: 1,
                properties: vec![ItemProperty {
                    id: "shield-bonus".to_owned(),
                    label: "+1 bouclier".to_owned(),
                    value: 1,
                    known: true,
                }],
                base_value: 9,
                source: "synthetic M4 fixture".to_owned(),
            },
            ItemDefinition {
                id: "ab_relic".to_owned(),
                name: "Relique du pont".to_owned(),
                slot: None,
                weight_kg: 0.5,
                stack_size: 1,
                properties: vec![ItemProperty {
                    id: "unknown-plot-property".to_owned(),
                    label: "Propriété de quête non interprétée".to_owned(),
                    value: 0,
                    known: false,
                }],
                base_value: 0,
                source: "bundle:ab_proving/ab_relic".to_owned(),
            },
        ] {
            catalog.add(definition)?;
        }
        Ok(catalog)
    }
}

impl Default for Inventory {
    fn default() -> Self {
        Self {
            schema_version: INVENTORY_SCHEMA.to_owned(),
            items: BTreeMap::new(),
            next_instance: 0,
            max_weight_kg: 80.0,
        }
    }
}

impl Inventory {
    pub fn add_item(
        &mut self,
        catalog: &ItemCatalog,
        definition_id: &str,
        quantity: u16,
    ) -> Result<Vec<String>, InventoryError> {
        if quantity == 0 {
            return Err(InventoryError::EmptyQuantity);
        }
        let definition = catalog
            .get(definition_id)
            .ok_or_else(|| InventoryError::UnknownItem(definition_id.to_owned()))?;
        definition.validate()?;
        if quantity > definition.stack_size {
            return Err(InventoryError::StackLimit(definition.stack_size));
        }
        let added_weight = definition.weight_kg * f32::from(quantity);
        let current_weight = self.total_weight(catalog);
        if current_weight + added_weight > self.max_weight_kg {
            return Err(InventoryError::Weight {
                current: current_weight,
                added: added_weight,
                max: self.max_weight_kg,
            });
        }

        let mut remaining = quantity;
        let mut touched = Vec::new();
        let stack_keys = self
            .items
            .iter()
            .filter(|(_, item)| item.definition_id == definition_id)
            .map(|(key, _)| key.clone())
            .collect::<Vec<_>>();
        for key in stack_keys {
            let item = self
                .items
                .get_mut(&key)
                .ok_or_else(|| InventoryError::UnknownInstance(key.clone()))?;
            let space = definition.stack_size.saturating_sub(item.quantity);
            if space == 0 {
                continue;
            }
            let moved = space.min(remaining);
            item.quantity += moved;
            remaining -= moved;
            touched.push(key);
            if remaining == 0 {
                return Ok(touched);
            }
        }
        while remaining > 0 {
            if self.items.len() >= MAX_INSTANCES {
                return Err(InventoryError::Capacity(MAX_INSTANCES));
            }
            let moved = remaining.min(definition.stack_size);
            let instance_id = format!("item-{}", self.next_instance);
            self.next_instance += 1;
            self.items.insert(
                instance_id.clone(),
                ItemInstance {
                    instance_id: instance_id.clone(),
                    definition_id: definition_id.to_owned(),
                    quantity: moved,
                    equipped_slot: None,
                },
            );
            touched.push(instance_id);
            remaining -= moved;
        }
        Ok(touched)
    }

    pub fn remove_item(&mut self, instance_id: &str, quantity: u16) -> Result<(), InventoryError> {
        if quantity == 0 {
            return Err(InventoryError::EmptyQuantity);
        }
        let item = self
            .items
            .get_mut(instance_id)
            .ok_or_else(|| InventoryError::UnknownInstance(instance_id.to_owned()))?;
        if item.quantity < quantity {
            return Err(InventoryError::EmptyQuantity);
        }
        item.quantity -= quantity;
        if item.quantity == 0 {
            self.items.remove(instance_id);
        }
        Ok(())
    }

    pub fn equip(
        &mut self,
        catalog: &ItemCatalog,
        instance_id: &str,
    ) -> Result<(), InventoryError> {
        let definition_id = self
            .items
            .get(instance_id)
            .ok_or_else(|| InventoryError::UnknownInstance(instance_id.to_owned()))?
            .definition_id
            .clone();
        let slot = catalog
            .get(&definition_id)
            .and_then(|definition| definition.slot)
            .ok_or_else(|| InventoryError::SlotMismatch {
                item: definition_id.clone(),
                slot: None,
            })?;
        let occupied = self
            .items
            .iter()
            .find(|(key, item)| *key != instance_id && item.equipped_slot == Some(slot))
            .map(|(key, _)| key.clone());
        if let Some(occupied) = occupied {
            self.items
                .get_mut(&occupied)
                .expect("occupied instance exists")
                .equipped_slot = None;
        }
        self.items
            .get_mut(instance_id)
            .expect("instance exists")
            .equipped_slot = Some(slot);
        Ok(())
    }

    pub fn unequip(&mut self, instance_id: &str) -> Result<(), InventoryError> {
        self.items
            .get_mut(instance_id)
            .ok_or_else(|| InventoryError::UnknownInstance(instance_id.to_owned()))?
            .equipped_slot = None;
        Ok(())
    }

    /// Move one whole instance to another inventory. The source is restored if
    /// the destination refuses the item.
    pub fn transfer_to(
        &mut self,
        destination: &mut Self,
        catalog: &ItemCatalog,
        instance_id: &str,
    ) -> Result<(), InventoryError> {
        let item = self
            .items
            .get(instance_id)
            .cloned()
            .ok_or_else(|| InventoryError::UnknownInstance(instance_id.to_owned()))?;
        let definition = catalog
            .get(&item.definition_id)
            .ok_or_else(|| InventoryError::UnknownItem(item.definition_id.clone()))?;
        let added_weight = definition.weight_kg * f32::from(item.quantity);
        let current_weight = destination.total_weight(catalog);
        if current_weight + added_weight > destination.max_weight_kg {
            return Err(InventoryError::Weight {
                current: current_weight,
                added: added_weight,
                max: destination.max_weight_kg,
            });
        }
        if destination.items.contains_key(instance_id) {
            return Err(InventoryError::DuplicateInstance(instance_id.to_owned()));
        }
        self.items.remove(instance_id);
        destination.items.insert(
            instance_id.to_owned(),
            ItemInstance {
                equipped_slot: None,
                ..item
            },
        );
        Ok(())
    }

    #[must_use]
    pub fn total_weight(&self, catalog: &ItemCatalog) -> f32 {
        self.items
            .values()
            .filter_map(|item| {
                catalog
                    .get(&item.definition_id)
                    .map(|definition| definition.weight_kg * f32::from(item.quantity))
            })
            .sum()
    }

    #[must_use]
    pub fn equipment_bonuses(&self, catalog: &ItemCatalog) -> EquipmentBonuses {
        let mut bonuses = EquipmentBonuses::default();
        for item in self.items.values() {
            if item.equipped_slot.is_none() {
                continue;
            }
            let Some(definition) = catalog.get(&item.definition_id) else {
                continue;
            };
            for property in &definition.properties {
                if !property.known {
                    continue;
                }
                match property.id.as_str() {
                    "attack-bonus" => bonuses.attack_bonus += property.value,
                    "damage-bonus" => bonuses.damage_bonus += property.value,
                    "armor-bonus" => bonuses.armor_bonus += property.value,
                    "shield-bonus" => bonuses.shield_bonus += property.value,
                    "natural-bonus" => bonuses.natural_bonus += property.value,
                    _ => {}
                }
            }
        }
        bonuses
    }

    #[must_use]
    pub fn unknown_properties(&self, catalog: &ItemCatalog) -> Vec<String> {
        let mut unknown = Vec::new();
        for item in self.items.values() {
            if let Some(definition) = catalog.get(&item.definition_id) {
                for property in &definition.properties {
                    if !property.known {
                        unknown.push(format!("{}:{}", definition.id, property.id));
                    }
                }
            }
        }
        unknown
    }

    pub fn validate(&self, catalog: &ItemCatalog) -> Result<(), InventoryError> {
        if self.schema_version != INVENTORY_SCHEMA {
            return Err(InventoryError::Corrupt(format!(
                "unsupported schema {}",
                self.schema_version
            )));
        }
        let mut slots = BTreeSet::new();
        for (key, item) in &self.items {
            if key != &item.instance_id {
                return Err(InventoryError::Corrupt(format!(
                    "key {key} differs from instance {}",
                    item.instance_id
                )));
            }
            let definition = catalog
                .get(&item.definition_id)
                .ok_or_else(|| InventoryError::UnknownItem(item.definition_id.clone()))?;
            if item.quantity == 0 || item.quantity > definition.stack_size {
                return Err(InventoryError::Corrupt(format!(
                    "invalid quantity {} for {}",
                    item.quantity, item.instance_id
                )));
            }
            if let Some(slot) = item.equipped_slot {
                if definition.slot != Some(slot) {
                    return Err(InventoryError::Corrupt(format!(
                        "instance {} is equipped in the wrong slot",
                        item.instance_id
                    )));
                }
                if !slots.insert(slot) {
                    return Err(InventoryError::Corrupt(format!(
                        "equipment slot {slot:?} is used twice"
                    )));
                }
            }
        }
        Ok(())
    }
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Store {
    pub id: String,
    pub markup_percent: u32,
    pub buyback_percent: u32,
    pub stock: BTreeMap<String, u16>,
    pub gold: i64,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct StoreTransaction {
    pub store: String,
    pub item: String,
    pub quantity: u16,
    pub unit_price: u32,
    pub total_price: i64,
    pub gold_after: i64,
}

#[derive(Debug, Clone, PartialEq)]
pub enum StoreError {
    UnknownItem(String),
    OutOfStock {
        item: String,
        requested: u16,
        available: u16,
    },
    InsufficientGold {
        required: i64,
        available: i64,
    },
    Inventory(InventoryError),
    InvalidPrice(String),
}

impl fmt::Display for StoreError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::UnknownItem(item) => write!(formatter, "store does not know item {item}"),
            Self::OutOfStock {
                item,
                requested,
                available,
            } => write!(
                formatter,
                "store has {available} of {item}, cannot sell {requested}"
            ),
            Self::InsufficientGold {
                required,
                available,
            } => write!(
                formatter,
                "merchant needs {required} gold, buyer has {available}"
            ),
            Self::Inventory(error) => {
                write!(formatter, "inventory refused the transaction: {error}")
            }
            Self::InvalidPrice(item) => write!(formatter, "invalid price for {item}"),
        }
    }
}

impl std::error::Error for StoreError {}

impl Store {
    #[must_use]
    pub fn new(id: impl Into<String>, stock: BTreeMap<String, u16>, gold: i64) -> Self {
        Self {
            id: id.into(),
            markup_percent: 100,
            buyback_percent: 50,
            stock,
            gold,
        }
    }

    fn unit_price(&self, catalog: &ItemCatalog, definition_id: &str) -> Result<u32, StoreError> {
        let definition = catalog
            .get(definition_id)
            .ok_or_else(|| StoreError::UnknownItem(definition_id.to_owned()))?;
        let base = u64::from(definition.base_value);
        let price = base
            .checked_mul(u64::from(self.markup_percent))
            .ok_or_else(|| StoreError::InvalidPrice(definition_id.to_owned()))?
            / 100;
        u32::try_from(price.max(1)).map_err(|_| StoreError::InvalidPrice(definition_id.to_owned()))
    }

    fn buyback_price(&self, catalog: &ItemCatalog, definition_id: &str) -> Result<u32, StoreError> {
        let unit = self.unit_price(catalog, definition_id)?;
        u32::try_from(
            u64::from(unit)
                .checked_mul(u64::from(self.buyback_percent))
                .ok_or_else(|| StoreError::InvalidPrice(definition_id.to_owned()))?
                / 100,
        )
        .map(|price| price.max(1))
        .map_err(|_| StoreError::InvalidPrice(definition_id.to_owned()))
    }

    /// Buy from the store. Gold, stock and inventory are mutated only after every
    /// precondition succeeds; the inventory call is the final commit point.
    pub fn buy(
        &mut self,
        catalog: &ItemCatalog,
        inventory: &mut Inventory,
        buyer_gold: &mut i64,
        definition_id: &str,
        quantity: u16,
    ) -> Result<StoreTransaction, StoreError> {
        if quantity == 0 {
            return Err(StoreError::Inventory(InventoryError::EmptyQuantity));
        }
        let available = self.stock.get(definition_id).copied().unwrap_or(0);
        if available < quantity {
            return Err(StoreError::OutOfStock {
                item: definition_id.to_owned(),
                requested: quantity,
                available,
            });
        }
        let unit_price = self.unit_price(catalog, definition_id)?;
        let total_price = i64::from(unit_price) * i64::from(quantity);
        if *buyer_gold < total_price {
            return Err(StoreError::InsufficientGold {
                required: total_price,
                available: *buyer_gold,
            });
        }
        inventory
            .add_item(catalog, definition_id, quantity)
            .map_err(StoreError::Inventory)?;
        *buyer_gold -= total_price;
        self.gold += total_price;
        let remaining = available - quantity;
        if remaining == 0 {
            self.stock.remove(definition_id);
        } else {
            self.stock.insert(definition_id.to_owned(), remaining);
        }
        Ok(StoreTransaction {
            store: self.id.clone(),
            item: definition_id.to_owned(),
            quantity,
            unit_price,
            total_price,
            gold_after: *buyer_gold,
        })
    }

    /// Sell one whole inventory instance. The inventory is restored when the
    /// store refuses the resulting stock or price.
    pub fn sell(
        &mut self,
        catalog: &ItemCatalog,
        inventory: &mut Inventory,
        seller_gold: &mut i64,
        instance_id: &str,
    ) -> Result<StoreTransaction, StoreError> {
        let instance = inventory.items.get(instance_id).cloned().ok_or_else(|| {
            StoreError::Inventory(InventoryError::UnknownInstance(instance_id.into()))
        })?;
        let unit_price = self.buyback_price(catalog, &instance.definition_id)?;
        let total_price = i64::from(unit_price) * i64::from(instance.quantity);
        inventory
            .remove_item(instance_id, instance.quantity)
            .map_err(StoreError::Inventory)?;
        *seller_gold += total_price;
        self.gold -= total_price;
        let stock = self
            .stock
            .entry(instance.definition_id.clone())
            .or_insert(0);
        *stock = stock.saturating_add(instance.quantity);
        Ok(StoreTransaction {
            store: self.id.clone(),
            item: instance.definition_id,
            quantity: instance.quantity,
            unit_price,
            total_price,
            gold_after: *seller_gold,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn stacks_respect_definition_limits() {
        let catalog = ItemCatalog::synthetic().expect("catalog");
        let mut inventory = Inventory::default();
        let ids = inventory
            .add_item(&catalog, "ab_relic", 1)
            .expect("add relic");
        assert_eq!(ids.len(), 1);
        assert!(matches!(
            inventory.add_item(&catalog, "ab_relic", 2),
            Err(InventoryError::StackLimit(1))
        ));
    }

    #[test]
    fn equip_replaces_the_same_slot_and_computes_bonuses() {
        let catalog = ItemCatalog::synthetic().expect("catalog");
        let mut inventory = Inventory::default();
        let sword = inventory
            .add_item(&catalog, "longsword", 1)
            .expect("sword")
            .remove(0);
        let armor = inventory
            .add_item(&catalog, "chain-shirt", 1)
            .expect("armor")
            .remove(0);
        inventory.equip(&catalog, &sword).expect("equip sword");
        inventory.equip(&catalog, &armor).expect("equip armor");
        let bonuses = inventory.equipment_bonuses(&catalog);
        assert_eq!(bonuses.attack_bonus, 1);
        assert_eq!(bonuses.damage_bonus, 1);
        assert_eq!(bonuses.armor_bonus, 4);
        assert!(inventory.validate(&catalog).is_ok());
    }

    #[test]
    fn transfer_is_atomic_on_destination_refusal() {
        let catalog = ItemCatalog::synthetic().expect("catalog");
        let mut source = Inventory::default();
        let mut destination = Inventory {
            max_weight_kg: 0.1,
            ..Inventory::default()
        };
        let relic = source
            .add_item(&catalog, "ab_relic", 1)
            .expect("relic")
            .remove(0);
        assert!(matches!(
            source.transfer_to(&mut destination, &catalog, &relic),
            Err(InventoryError::Weight { .. })
        ));
        assert!(source.items.contains_key(&relic));
        assert!(destination.items.is_empty());
    }

    #[test]
    fn unknown_properties_remain_visible() {
        let catalog = ItemCatalog::synthetic().expect("catalog");
        let mut inventory = Inventory::default();
        inventory.add_item(&catalog, "ab_relic", 1).expect("relic");
        assert_eq!(
            inventory.unknown_properties(&catalog),
            vec!["ab_relic:unknown-plot-property".to_owned()]
        );
    }

    #[test]
    fn store_buy_and_sell_keep_stock_gold_and_items_consistent() {
        let catalog = ItemCatalog::synthetic().expect("catalog");
        let mut inventory = Inventory::default();
        let mut gold = 1_000_i64;
        let mut store = Store::new(
            "ab_store",
            BTreeMap::from([("longsword".to_owned(), 2)]),
            500,
        );
        let bought = store
            .buy(&catalog, &mut inventory, &mut gold, "longsword", 1)
            .expect("buy");
        assert_eq!(bought.quantity, 1);
        assert_eq!(store.stock.get("longsword"), Some(&1));
        assert_eq!(inventory.items.len(), 1);
        let instance = inventory.items.keys().next().cloned().expect("instance");
        let sold = store
            .sell(&catalog, &mut inventory, &mut gold, &instance)
            .expect("sell");
        assert_eq!(sold.quantity, 1);
        assert!(inventory.items.is_empty());
        assert_eq!(store.stock.get("longsword"), Some(&2));
        assert_eq!(gold, 1_000 - bought.total_price + sold.total_price);
        assert_eq!(store.gold, 500 + bought.total_price - sold.total_price);
    }

    #[test]
    fn store_refuses_without_mutating_any_state() {
        let catalog = ItemCatalog::synthetic().expect("catalog");
        let mut inventory = Inventory::default();
        let mut gold = 1_i64;
        let mut store = Store::new("ab_store", BTreeMap::from([("longsword".to_owned(), 1)]), 0);
        let error = store
            .buy(&catalog, &mut inventory, &mut gold, "longsword", 1)
            .expect_err("gold");
        assert!(matches!(error, StoreError::InsufficientGold { .. }));
        assert_eq!(gold, 1);
        assert_eq!(store.stock.get("longsword"), Some(&1));
        assert!(inventory.items.is_empty());
        let error = store
            .buy(&catalog, &mut inventory, &mut gold, "ab_relic", 1)
            .expect_err("stock");
        assert!(matches!(error, StoreError::OutOfStock { .. }));
        assert_eq!(gold, 1);
        assert!(inventory.items.is_empty());
    }
}
