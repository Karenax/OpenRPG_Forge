#![deny(unsafe_code)]
//! Separate `NWN` compatibility rules profile for the Aurora bridge.
//!
//! This crate does not modify the native `OpenRPG` action rules. It provides a
//! bounded, deterministic subset used by the M4 synthetic scenario.

pub mod ai;
pub mod character;
pub mod combat;
pub mod factions;
pub mod inventory;
pub mod oracle;
pub mod profile;
pub mod quest;
pub mod rng;
pub mod save;
pub mod spells;
pub mod tables;

pub use ai::{DEFAULT_PERCEPTION_RANGE_M, MeleeStep, Perception, plan_melee_step};
pub use character::{
    AbilityScores, ArmorClass, AttackAbility, CharacterClass, CharacterError, CharacterState,
    CharacterTemplate, DamageType, Dice, Position, SaveKind, SavingThrows, Weapon,
};
pub use combat::{
    AttackOutcome, CombatAction, CombatError, CombatModifiers, EncounterOutcome, EncounterReport,
    SpecialAttack, SpecialAttackOutcome, SpecialSave, resolve_attack, resolve_special_attack,
    run_encounter, run_encounter_with_perception, saving_throw,
};
pub use factions::{Faction, FactionError, FactionTable, Reputation};
pub use inventory::{
    EquipmentBonuses, EquipmentSlot, Inventory, InventoryError, ItemCatalog, ItemDefinition,
    ItemInstance, ItemProperty, Store, StoreError, StoreTransaction,
};
pub use oracle::{
    ORACLE_LOG_SCHEMA, ORACLE_SCHEMA, OracleAttack, OracleLog, OracleLogError, TraceComparison,
    TraceDivergence, compare_encounters,
};
pub use profile::{DEFAULT_PROFILE_ID, NwnRulesProfile, NwnRuleset, PROFILE_SCHEMA, ProfileError};
pub use quest::{QuestError, QuestState, QuestStatus};
pub use rng::{DeterministicRng, MAX_TRACE_ENTRIES, RNG_SCHEMA, RngError, RngSnapshot, RollTrace};
pub use save::{RULES_STATE_SCHEMA, RulesSaveError, RulesSnapshot};
pub use spells::{
    ActiveEffect, SpellCatalog, SpellDefinition, SpellEffect, SpellError, SpellResult,
    SpellSlotTable, SpellTarget, cast_area_spell, cast_spell, combined_modifiers, tick_effects,
};
pub use tables::{
    MAX_2DA_BYTES, MAX_2DA_ROWS, TwoDaAudit, TwoDaError, TwoDaRow, TwoDaSummary, TwoDaTable,
    TwoDaTableSummary,
};
