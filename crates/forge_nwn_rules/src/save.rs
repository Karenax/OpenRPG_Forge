//! Versioned rules snapshot for the M4 save and resume path.

use crate::character::CharacterState;
use crate::inventory::{Inventory, InventoryError, ItemCatalog};
use crate::quest::{QuestError, QuestState};
use crate::rng::{DeterministicRng, RngError, RngSnapshot};
use crate::spells::{ActiveEffect, SpellError, SpellSlotTable, validate_effects};
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;
use std::fmt;

pub const RULES_STATE_SCHEMA: &str = "forge.nwn-rules-state@1";

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RulesSnapshot {
    pub schema: String,
    pub profile_id: String,
    pub seed: u64,
    pub rng: RngSnapshot,
    pub characters: Vec<CharacterState>,
    pub inventory: Inventory,
    pub spell_slots: BTreeMap<String, SpellSlotTable>,
    pub effects: Vec<ActiveEffect>,
    pub quest: QuestState,
    pub encounter_resolved: bool,
    pub round: u32,
}

impl RulesSnapshot {
    pub fn validate(&self, catalog: &ItemCatalog) -> Result<(), RulesSaveError> {
        if self.schema != RULES_STATE_SCHEMA {
            return Err(RulesSaveError::Schema(self.schema.clone()));
        }
        if self.profile_id.trim().is_empty() {
            return Err(RulesSaveError::EmptyProfile);
        }
        if self.characters.is_empty() {
            return Err(RulesSaveError::NoCharacters);
        }
        let mut ids = std::collections::BTreeSet::new();
        for character in &self.characters {
            if !ids.insert(character.template.id.clone()) {
                return Err(RulesSaveError::DuplicateCharacter(
                    character.template.id.clone(),
                ));
            }
            character
                .validate()
                .map_err(|error| RulesSaveError::Character(error.to_string()))?;
        }
        self.inventory
            .validate(catalog)
            .map_err(|error| RulesSaveError::Inventory(error.to_string()))?;
        self.quest
            .validate()
            .map_err(|error| RulesSaveError::Quest(error.to_string()))?;
        DeterministicRng::restore(self.rng.clone())
            .map_err(|error| RulesSaveError::Rng(error.to_string()))?;
        for slots in self.spell_slots.values() {
            slots
                .validate()
                .map_err(|error| RulesSaveError::Spell(error.to_string()))?;
        }
        validate_effects(&self.effects)
            .map_err(|error| RulesSaveError::Spell(error.to_string()))?;
        if self.round > 1_000_000 {
            return Err(RulesSaveError::Round(self.round));
        }
        Ok(())
    }

    pub fn restore(
        snapshot: Self,
        catalog: &ItemCatalog,
    ) -> Result<(Self, DeterministicRng), RulesSaveError> {
        snapshot.validate(catalog)?;
        let rng = DeterministicRng::restore(snapshot.rng.clone())
            .map_err(|error| RulesSaveError::Rng(error.to_string()))?;
        Ok((snapshot, rng))
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum RulesSaveError {
    Schema(String),
    EmptyProfile,
    NoCharacters,
    DuplicateCharacter(String),
    Character(String),
    Inventory(String),
    Quest(String),
    Spell(String),
    Rng(String),
    Round(u32),
}

impl fmt::Display for RulesSaveError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Schema(schema) => write!(formatter, "unsupported rules state schema {schema}"),
            Self::EmptyProfile => formatter.write_str("rules state profile id must not be empty"),
            Self::NoCharacters => formatter.write_str("rules state has no characters"),
            Self::DuplicateCharacter(id) => write!(formatter, "duplicate character {id}"),
            Self::Character(detail) => write!(formatter, "invalid character: {detail}"),
            Self::Inventory(detail) => write!(formatter, "invalid inventory: {detail}"),
            Self::Quest(detail) => write!(formatter, "invalid quest: {detail}"),
            Self::Spell(detail) => write!(formatter, "invalid spell state: {detail}"),
            Self::Rng(detail) => write!(formatter, "invalid RNG state: {detail}"),
            Self::Round(round) => write!(formatter, "invalid round {round}"),
        }
    }
}

impl std::error::Error for RulesSaveError {}

impl From<InventoryError> for RulesSaveError {
    fn from(error: InventoryError) -> Self {
        Self::Inventory(error.to_string())
    }
}

impl From<QuestError> for RulesSaveError {
    fn from(error: QuestError) -> Self {
        Self::Quest(error.to_string())
    }
}

impl From<SpellError> for RulesSaveError {
    fn from(error: SpellError) -> Self {
        Self::Spell(error.to_string())
    }
}

impl From<RngError> for RulesSaveError {
    fn from(error: RngError) -> Self {
        Self::Rng(error.to_string())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::character::{CharacterTemplate, Position};
    use crate::quest::QuestState;

    fn snapshot() -> RulesSnapshot {
        let catalog = ItemCatalog::synthetic().expect("catalog");
        let mut inventory = Inventory::default();
        inventory.add_item(&catalog, "ab_relic", 1).expect("relic");
        let mut rng = DeterministicRng::new(7);
        rng.d20("initiative").expect("roll");
        let character = CharacterState::new(
            CharacterTemplate::synthetic_fighter(),
            Position::new(0.0, 0.0),
        );
        RulesSnapshot {
            schema: RULES_STATE_SCHEMA.to_owned(),
            profile_id: "nwn-ee-vanilla-m4-subset".to_owned(),
            seed: 7,
            rng: rng.snapshot(),
            characters: vec![character],
            inventory,
            spell_slots: BTreeMap::from([(
                "synthetic-fighter".to_owned(),
                SpellSlotTable::new(&[(1, 2)]),
            )]),
            effects: Vec::new(),
            quest: QuestState::from_m3("ab_quest", [10, 20, 30], "completed"),
            encounter_resolved: true,
            round: 3,
        }
    }

    #[test]
    fn valid_snapshot_restores_the_rng_state() {
        let catalog = ItemCatalog::synthetic().expect("catalog");
        let snapshot = snapshot();
        let (restored, _rng) = RulesSnapshot::restore(snapshot.clone(), &catalog).expect("restore");
        assert_eq!(restored, snapshot);
    }

    #[test]
    fn corrupt_hp_and_duplicate_character_are_refused() {
        let catalog = ItemCatalog::synthetic().expect("catalog");
        let mut snapshot = snapshot();
        snapshot.characters[0].current_hp = 999;
        assert!(matches!(
            snapshot.validate(&catalog),
            Err(RulesSaveError::Character(_))
        ));
        let mut snapshot = self::snapshot();
        snapshot.characters.push(snapshot.characters[0].clone());
        assert!(matches!(
            snapshot.validate(&catalog),
            Err(RulesSaveError::DuplicateCharacter(_))
        ));
    }

    #[test]
    fn unknown_inventory_definition_is_refused() {
        let catalog = ItemCatalog::synthetic().expect("catalog");
        let mut snapshot = snapshot();
        snapshot.inventory.items.insert(
            "item-x".to_owned(),
            crate::inventory::ItemInstance {
                instance_id: "item-x".to_owned(),
                definition_id: "missing".to_owned(),
                quantity: 1,
                equipped_slot: None,
            },
        );
        assert!(matches!(
            snapshot.validate(&catalog),
            Err(RulesSaveError::Inventory(_))
        ));
    }

    #[test]
    fn active_spell_effects_survive_the_snapshot() {
        let catalog = ItemCatalog::synthetic().expect("catalog");
        let mut snapshot = snapshot();
        snapshot.effects.push(crate::spells::ActiveEffect {
            id: "effect-0".to_owned(),
            source_spell: "bless".to_owned(),
            attack_bonus: 1,
            ac_bonus: 0,
            remaining_rounds: 2,
        });
        let (restored, _) = RulesSnapshot::restore(snapshot.clone(), &catalog).expect("restore");
        assert_eq!(restored.effects, snapshot.effects);
    }
}
