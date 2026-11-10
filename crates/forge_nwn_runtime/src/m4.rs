//! M4 vertical slice: separate NWN rules, deterministic combat, spells, items,
//! factions, quest migration and save/resume on the synthetic `ab_proving` fixture.

use crate::scenario::{DialogueTurn, M3Error, M3Save, M3Scenario, ScenarioEvent};
use forge_core::{ContentHash, canonical_json_sha256};
use forge_nwn_rules::{
    ActiveEffect, CharacterState, CharacterTemplate, CombatAction, CombatModifiers,
    DeterministicRng, EncounterReport, FactionTable, Inventory, ItemCatalog, NwnRulesProfile,
    Position, QuestState, RulesSaveError, RulesSnapshot, SpellCatalog, SpellError, SpellResult,
    SpellSlotTable, Weapon, cast_spell, combined_modifiers,
};
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;
use std::path::Path;

pub const M4_REPORT_SCHEMA: &str = "forge.aurora-m4-report@1";
pub const M4_SAVE_SCHEMA: &str = "forge.aurora-m4-save@1";

#[derive(Debug, thiserror::Error)]
pub enum M4Error {
    #[error("M3 scenario failed: {0}")]
    M3(#[from] M3Error),
    #[error("rules profile failed: {0}")]
    Profile(String),
    #[error("rules state failed: {0}")]
    Rules(#[from] RulesSaveError),
    #[error("spell failed: {0}")]
    Spell(#[from] SpellError),
    #[error("combat failed: {0}")]
    Combat(String),
    #[error("inventory failed: {0}")]
    Inventory(String),
    #[error("faction table failed: {0}")]
    Factions(String),
    #[error("guardian blueprint is absent or invalid")]
    Guardian,
    #[error("I/O error at {path}: {detail}")]
    Io { path: String, detail: String },
    #[error("combat did not defeat the guardian")]
    GuardianSurvived,
    #[error("report hashing failed: {0}")]
    Hash(String),
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct M4Save {
    pub schema: String,
    pub m3: M3Save,
    pub rules: RulesSnapshot,
    pub combat: EncounterReport,
    pub spells: Vec<SpellResult>,
    pub rests: u32,
}

impl M4Save {
    pub fn validate(&self, catalog: &ItemCatalog) -> Result<(), RulesSaveError> {
        if self.schema != M4_SAVE_SCHEMA {
            return Err(RulesSaveError::Schema(self.schema.clone()));
        }
        if self.m3.schema != "forge.aurora-m3-save@1" {
            return Err(RulesSaveError::Schema(self.m3.schema.clone()));
        }
        if self.combat.schema != "forge.nwn-combat-encounter@1" {
            return Err(RulesSaveError::Schema(self.combat.schema.clone()));
        }
        self.rules.validate(catalog)
    }
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct M4Report {
    pub schema: String,
    pub module: String,
    pub profile_id: String,
    pub seed: u64,
    pub winner: Option<String>,
    pub rounds: u32,
    pub combat_actions: usize,
    pub ai_moves: usize,
    pub party_hp: i32,
    pub guardian_hp: i32,
    pub spells: Vec<SpellResult>,
    pub rests: u32,
    pub inventory: Vec<String>,
    pub gold: i64,
    pub journal_entries: Vec<u32>,
    pub quest_status: String,
    pub transitions: usize,
    pub rng_trace_len: usize,
    pub oracle_status: String,
    pub oracle_note: String,
    pub save_sha256: ContentHash,
    pub resume_idempotent: bool,
    pub messages: Vec<String>,
    pub dialogue_transcript: Vec<DialogueTurn>,
    pub events: Vec<ScenarioEvent>,
    pub executed_scripts: Vec<String>,
    pub unsupported: Vec<String>,
    pub combat: EncounterReport,
}

pub struct M4Scenario {
    m3: M3Scenario,
    profile: NwnRulesProfile,
    factions: FactionTable,
    catalog: ItemCatalog,
    spell_catalog: SpellCatalog,
    party: CharacterState,
    guardian: CharacterState,
    inventory: Inventory,
    slots: SpellSlotTable,
    effects: Vec<ActiveEffect>,
    quest: QuestState,
    rng: DeterministicRng,
    seed: u64,
    combat: Option<EncounterReport>,
    spells: Vec<SpellResult>,
    rests: u32,
}

impl M4Scenario {
    pub fn load(bundle: &Path, seed: u64) -> Result<Self, M4Error> {
        Self::load_with_profile(bundle, seed, NwnRulesProfile::vanilla_subset())
    }

    pub fn load_with_profile(
        bundle: &Path,
        seed: u64,
        profile: NwnRulesProfile,
    ) -> Result<Self, M4Error> {
        let m3 = M3Scenario::load(bundle)?;
        profile
            .validate()
            .map_err(|error| M4Error::Profile(error.to_string()))?;
        let catalog =
            ItemCatalog::synthetic().map_err(|error| M4Error::Inventory(error.to_string()))?;
        let spell_catalog = SpellCatalog::synthetic()?;
        let factions_text = read_text(&bundle.join("factions").join("factions.json"))?;
        let factions = FactionTable::from_json(&factions_text, profile.hostile_reputation_max)
            .map_err(|error| M4Error::Factions(error.to_string()))?;
        let mut party_template = CharacterTemplate::synthetic_fighter();
        party_template
            .validate()
            .map_err(|error| M4Error::Profile(error.to_string()))?;
        let mut inventory = Inventory::default();
        for definition in ["longsword", "chain-shirt", "light-shield"] {
            let instance = inventory
                .add_item(&catalog, definition, 1)
                .map_err(|error| M4Error::Inventory(error.to_string()))?
                .into_iter()
                .next()
                .ok_or_else(|| M4Error::Inventory(format!("no instance for {definition}")))?;
            inventory
                .equip(&catalog, &instance)
                .map_err(|error| M4Error::Inventory(error.to_string()))?;
        }
        let bonuses = inventory.equipment_bonuses(&catalog);
        if let Some(weapon) = party_template.weapon.as_mut() {
            weapon.attack_bonus += bonuses.attack_bonus;
            weapon.damage_bonus += bonuses.damage_bonus;
        }
        party_template.ac.armor += bonuses.armor_bonus;
        party_template.ac.shield += bonuses.shield_bonus;
        party_template.ac.natural += bonuses.natural_bonus;
        party_template
            .validate()
            .map_err(|error| M4Error::Profile(error.to_string()))?;
        let guardian_template = guardian_from_bundle(bundle)?;
        let party = CharacterState::new(party_template, Position::new(0.0, 0.0));
        let guardian = CharacterState::new(guardian_template, Position::new(0.0, 6.0));
        let slots = SpellSlotTable::new(&[(1, 3)]);
        Ok(Self {
            m3,
            profile,
            factions,
            catalog,
            spell_catalog,
            party,
            guardian,
            inventory,
            slots,
            effects: Vec::new(),
            quest: QuestState::new("ab_quest"),
            rng: DeterministicRng::new(seed),
            seed,
            combat: None,
            spells: Vec::new(),
            rests: 0,
        })
    }

    #[must_use]
    pub fn profile_id(&self) -> &str {
        &self.profile.id
    }

    pub fn run(&mut self) -> Result<M4Report, M4Error> {
        self.m3.start_module()?;
        self.m3.enter_camp()?;
        self.m3.talk_to_guide(0)?;
        self.m3.talk_to_guide(0)?;
        self.m3.enter_crypt()?;
        self.m3.schedule_relic_trigger()?;
        self.m3.run_due_jobs(1)?;
        self.m3.disturb_chest()?;
        self.loot_relic()?;
        self.cast_pre_combat()?;
        self.fight_guardian()?;
        if self.guardian.current_hp > 0 {
            return Err(M4Error::GuardianSurvived);
        }
        self.m3.kill_guard()?;
        self.cast_after_combat()?;
        self.rest();
        self.m3.return_to_camp()?;
        self.m3.talk_to_guide(0)?;
        self.m3.talk_to_guide(0)?;
        self.m3.verify_completion()?;
        self.quest = QuestState::from_m3(
            "ab_quest",
            self.m3.runtime().journal_entries("ab_quest"),
            &self.m3.quest_status(),
        );
        self.quest.grant_reward_once("ab_reward_gold_50");
        self.m3.event(
            "m4_combat",
            format!(
                "gardien vaincu en {} rounds, {} actions",
                self.combat.as_ref().map_or(0, |report| report.rounds),
                self.combat
                    .as_ref()
                    .map_or(0, |report| report.actions.len())
            ),
        );
        self.report()
    }

    fn loot_relic(&mut self) -> Result<(), M4Error> {
        self.inventory
            .add_item(&self.catalog, "ab_relic", 1)
            .map_err(|error| M4Error::Inventory(error.to_string()))?;
        self.m3
            .event("m4_loot", "relique ajoutée à l'inventaire M4");
        Ok(())
    }

    fn cast_pre_combat(&mut self) -> Result<(), M4Error> {
        let party_id = self.party.template.id.clone();
        let guardian_id = self.guardian.template.id.clone();
        self.cast("bless", &party_id)?;
        self.cast("magic-missile", &guardian_id)?;
        Ok(())
    }

    fn cast_after_combat(&mut self) -> Result<(), M4Error> {
        if self.party.current_hp < self.party.template.hit_points {
            let party_id = self.party.template.id.clone();
            self.cast("cure-light-wounds", &party_id)?;
        }
        Ok(())
    }

    fn cast(&mut self, spell: &str, target_id: &str) -> Result<(), M4Error> {
        let caster = self.party.clone();
        let target = if target_id == self.party.template.id {
            &mut self.party
        } else {
            &mut self.guardian
        };
        let result = cast_spell(
            &self.profile,
            &self.spell_catalog,
            &mut self.slots,
            spell,
            &caster,
            target,
            &mut self.effects,
            &mut self.rng,
        )?;
        self.spells.push(result);
        Ok(())
    }

    fn fight_guardian(&mut self) -> Result<(), M4Error> {
        let modifiers = combined_modifiers(&self.effects);
        let outcome = forge_nwn_rules::run_encounter(
            &self.profile,
            &self.factions,
            &self.party,
            &self.guardian,
            modifiers,
            CombatModifiers::default(),
            &mut self.rng,
        )
        .map_err(|error| M4Error::Combat(error.to_string()))?;
        self.party = outcome
            .combatants
            .first()
            .cloned()
            .ok_or_else(|| M4Error::Combat("missing party outcome".to_owned()))?;
        self.guardian = outcome
            .combatants
            .get(1)
            .cloned()
            .ok_or_else(|| M4Error::Combat("missing guardian outcome".to_owned()))?;
        self.combat = Some(outcome.report);
        Ok(())
    }

    fn rest(&mut self) {
        self.slots.rest();
        self.party.restore_full();
        self.effects.clear();
        self.rests += 1;
        self.m3
            .event("m4_rest", "repos M4 : sorts restaurés, PV restaurés");
    }

    pub(crate) fn save(&self) -> Result<M4Save, M4Error> {
        Ok(M4Save {
            schema: M4_SAVE_SCHEMA.to_owned(),
            m3: self.m3.save(),
            rules: RulesSnapshot {
                schema: forge_nwn_rules::RULES_STATE_SCHEMA.to_owned(),
                profile_id: self.profile.id.clone(),
                seed: self.seed,
                rng: self.rng.snapshot(),
                characters: vec![self.party.clone(), self.guardian.clone()],
                inventory: self.inventory.clone(),
                spell_slots: BTreeMap::from([(self.party.template.id.clone(), self.slots.clone())]),
                effects: self.effects.clone(),
                quest: self.quest.clone(),
                encounter_resolved: self.combat.is_some(),
                round: self.combat.as_ref().map_or(0, |report| report.rounds),
            },
            combat: self
                .combat
                .clone()
                .ok_or_else(|| M4Error::Combat("combat report is absent".to_owned()))?,
            spells: self.spells.clone(),
            rests: self.rests,
        })
    }

    fn report(&mut self) -> Result<M4Report, M4Error> {
        let save = self.save()?;
        save.validate(&self.catalog)?;
        let save_hash =
            canonical_json_sha256(&save).map_err(|error| M4Error::Hash(error.to_string()))?;
        let resume_idempotent = self.resume_check(&save)?;
        let session_report = self.m3.session().report();
        let combat = save.combat.clone();
        let mut unsupported = self
            .m3
            .runtime()
            .unsupported
            .iter()
            .map(|diagnostic| format!("{}: {}", diagnostic.code, diagnostic.detail))
            .collect::<Vec<_>>();
        for spell in &self.spells {
            if !spell.vfx_supported {
                unsupported.push(format!("{}: VFX non supporté", spell.spell));
            }
        }
        unsupported.extend(
            self.inventory
                .unknown_properties(&self.catalog)
                .into_iter()
                .map(|property| format!("{property}: propriété non interprétée")),
        );
        Ok(M4Report {
            schema: M4_REPORT_SCHEMA.to_owned(),
            module: self.m3.module_name().to_owned(),
            profile_id: self.profile.id.clone(),
            seed: self.seed,
            winner: combat.winner.clone(),
            rounds: combat.rounds,
            combat_actions: combat.actions.len(),
            ai_moves: combat
                .actions
                .iter()
                .filter(|action| matches!(action, CombatAction::Move { .. }))
                .count(),
            party_hp: self.party.current_hp,
            guardian_hp: self.guardian.current_hp,
            spells: self.spells.clone(),
            rests: self.rests,
            inventory: self.inventory_summary(),
            gold: self.m3.runtime().gold,
            journal_entries: self.m3.runtime().journal_entries("ab_quest"),
            quest_status: self.m3.quest_status(),
            transitions: session_report.transitions,
            rng_trace_len: self.rng.trace().len(),
            oracle_status: "not-run".to_owned(),
            oracle_note:
                "oracle NWN local non exécuté ; harnais de comparaison testé sur traces synthétiques"
                    .to_owned(),
            save_sha256: save_hash,
            resume_idempotent,
            messages: self.m3.runtime().messages.clone(),
            dialogue_transcript: self.m3.transcript().to_vec(),
            events: self.m3.scenario_events().to_vec(),
            executed_scripts: self.m3.runtime().executed_scripts.clone(),
            unsupported,
            combat,
        })
    }

    fn inventory_summary(&self) -> Vec<String> {
        self.inventory
            .items
            .values()
            .map(|item| {
                let name = self
                    .catalog
                    .get(&item.definition_id)
                    .map_or(item.definition_id.as_str(), |definition| {
                        definition.name.as_str()
                    });
                let slot = item
                    .equipped_slot
                    .map_or(String::new(), |slot| format!(" [{slot:?}]"));
                format!("{name} x{}{slot}", item.quantity)
            })
            .collect()
    }

    fn resume_check(&mut self, save: &M4Save) -> Result<bool, M4Error> {
        let m3_ok = self.m3.resume_and_check(&save.m3)?;
        let (restored, mut rng) = RulesSnapshot::restore(save.rules.clone(), &self.catalog)?;
        let rules_ok = restored.characters == vec![self.party.clone(), self.guardian.clone()]
            && restored.inventory == self.inventory
            && restored.spell_slots.get(&self.party.template.id) == Some(&self.slots)
            && restored.effects == self.effects
            && restored.quest == self.quest;
        let reward_once = restored.quest.rewards_granted.contains("ab_reward_gold_50");
        let trace_before = rng.trace().len();
        rng.d20("resume-check")
            .map_err(|error| M4Error::Combat(error.to_string()))?;
        let trace_continued = rng.trace().len() == trace_before + 1;
        Ok(m3_ok && rules_ok && reward_once && trace_continued)
    }
}

fn read_text(path: &Path) -> Result<String, M4Error> {
    std::fs::read_to_string(path).map_err(|error| M4Error::Io {
        path: path.display().to_string(),
        detail: error.to_string(),
    })
}

fn guardian_from_bundle(bundle: &Path) -> Result<CharacterTemplate, M4Error> {
    let text = read_text(&bundle.join("catalogs").join("blueprints.json"))?;
    let value: serde_json::Value = serde_json::from_str(&text).map_err(|error| M4Error::Io {
        path: "catalogs/blueprints.json".to_owned(),
        detail: error.to_string(),
    })?;
    let blueprints = value["blueprints"].as_array().ok_or(M4Error::Guardian)?;
    let guardian = blueprints
        .iter()
        .find(|blueprint| blueprint["resref"].as_str() == Some("ab_guardian"))
        .ok_or(M4Error::Guardian)?;

    let (class_id, level) = class_entry(guardian).ok_or(M4Error::Guardian)?;
    if class_id != 0 {
        return Err(M4Error::Guardian);
    }
    let ability = |label: &str| -> Result<i16, M4Error> {
        let value = field_i64(guardian, label).ok_or(M4Error::Guardian)?;
        i16::try_from(value).map_err(|_| M4Error::Guardian)
    };
    let hit_points = i32::try_from(field_i64(guardian, "HitPoints").ok_or(M4Error::Guardian)?)
        .map_err(|_| M4Error::Guardian)?;
    let natural = i32::try_from(field_i64(guardian, "NaturalAC").ok_or(M4Error::Guardian)?)
        .map_err(|_| M4Error::Guardian)?;
    let faction = u16::try_from(field_i64(guardian, "FactionID").ok_or(M4Error::Guardian)?)
        .map_err(|_| M4Error::Guardian)?;
    let level = u8::try_from(level).map_err(|_| M4Error::Guardian)?;
    let template = CharacterTemplate {
        id: "ab_guardian".to_owned(),
        name: "Le gardien".to_owned(),
        faction_id: faction,
        abilities: forge_nwn_rules::AbilityScores {
            strength: ability("Str")?,
            dexterity: ability("Dex")?,
            constitution: ability("Con")?,
            intelligence: ability("Int")?,
            wisdom: ability("Wis")?,
            charisma: ability("Cha")?,
        },
        level,
        classes: vec![forge_nwn_rules::CharacterClass {
            name: "barbarian".to_owned(),
            level,
        }],
        hit_points,
        bab: i32::from(level),
        ac: forge_nwn_rules::ArmorClass {
            natural,
            ..forge_nwn_rules::ArmorClass::unarmored()
        },
        saves: forge_nwn_rules::SavingThrows {
            fortitude: 2 + i32::from(level.saturating_sub(1) / 2),
            reflex: i32::from(level.saturating_sub(1) / 3),
            will: i32::from(level.saturating_sub(1) / 3),
        },
        weapon: Some(Weapon::unarmed()),
        attack_ability: forge_nwn_rules::AttackAbility::Strength,
        damage_reduction: 0,
        damage_immunities: Vec::new(),
        concealment_percent: 0,
        feats: Vec::new(),
        skills: BTreeMap::new(),
        source: format!("bundle:{}", bundle.display()),
    };
    template
        .validate()
        .map_err(|error| M4Error::Profile(error.to_string()))?;
    Ok(template)
}

fn field_i64(blueprint: &serde_json::Value, label: &str) -> Option<i64> {
    blueprint["fields"]
        .as_array()?
        .iter()
        .find(|field| field["label"].as_str() == Some(label))?["value"]["value"]
        .as_i64()
}

fn class_entry(blueprint: &serde_json::Value) -> Option<(i64, i64)> {
    let list = blueprint["fields"]
        .as_array()?
        .iter()
        .find(|field| field["label"].as_str() == Some("ClassList"))?["value"]["value"]
        .as_array()?;
    let first = list.first()?;
    let class_id = first["fields"]
        .as_array()?
        .iter()
        .find(|field| field["label"].as_str() == Some("Class"))?["value"]["value"]
        .as_i64()?;
    let level = first["fields"]
        .as_array()?
        .iter()
        .find(|field| field["label"].as_str() == Some("ClassLevel"))?["value"]["value"]
        .as_i64()?;
    Some((class_id, level))
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::path::PathBuf;

    fn bundle() -> PathBuf {
        PathBuf::from(env!("CARGO_MANIFEST_DIR"))
            .join("../forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1")
    }

    #[test]
    fn guardian_blueprint_is_mapped_from_the_bundle() {
        let guardian = guardian_from_bundle(&bundle()).expect("guardian");
        assert_eq!(guardian.id, "ab_guardian");
        assert_eq!(guardian.hit_points, 8);
        assert_eq!(guardian.faction_id, 1);
        assert_eq!(guardian.armor_class(), 10);
        assert_eq!(guardian.classes[0].name, "barbarian");
        assert_eq!(
            guardian.weapon().map(|weapon| weapon.name.as_str()),
            Some("unarmed")
        );
    }

    #[test]
    fn m4_scenario_completes_and_resumes_without_duplicates() {
        let mut scenario = M4Scenario::load(&bundle(), 2026).expect("load");
        let report = scenario.run().expect("run");
        assert_eq!(report.schema, M4_REPORT_SCHEMA);
        assert_eq!(report.winner.as_deref(), Some("synthetic-fighter"));
        assert_eq!(report.guardian_hp, 0);
        assert!(report.rounds > 0);
        assert_eq!(report.quest_status, "completed");
        assert_eq!(report.journal_entries, vec![10, 20, 30]);
        assert!(report.gold >= 50);
        assert!(report.resume_idempotent);
        assert!(report.inventory.iter().any(|item| item.contains("Relique")));
        assert!(report.spells.iter().any(|spell| spell.spell == "bless"));
        assert!(
            report
                .spells
                .iter()
                .any(|spell| spell.spell == "magic-missile")
        );
        assert!(report.rests >= 1);
        assert_eq!(report.transitions, 2);
        assert!(!report.combat.actions.is_empty());
    }

    #[test]
    fn m4_is_deterministic_across_runs() {
        let first = M4Scenario::load(&bundle(), 7)
            .expect("load")
            .run()
            .expect("run");
        let second = M4Scenario::load(&bundle(), 7)
            .expect("load")
            .run()
            .expect("run");
        assert_eq!(first.save_sha256, second.save_sha256);
        assert_eq!(first.combat, second.combat);
        assert_eq!(first.spells, second.spells);
    }

    #[test]
    fn m4_save_refuses_corruption() {
        let mut scenario = M4Scenario::load(&bundle(), 2026).expect("load");
        scenario.run().expect("run");
        let save = scenario.save().expect("save");
        assert!(save.validate(&scenario.catalog).is_ok());

        let mut corrupt = save.clone();
        corrupt.rules.characters[0].current_hp = 999;
        assert!(corrupt.validate(&scenario.catalog).is_err());

        let mut wrong_schema = save;
        wrong_schema.schema = "forge.aurora-m4-save@0".to_owned();
        assert!(wrong_schema.validate(&scenario.catalog).is_err());
    }

    #[test]
    fn profile_is_selectable_explicitly() {
        let mut profile = NwnRulesProfile::vanilla_subset();
        profile.id = "custom-m4-profile".to_owned();
        let scenario = M4Scenario::load_with_profile(&bundle(), 1, profile).expect("load profile");
        assert_eq!(scenario.profile_id(), "custom-m4-profile");
    }
}
