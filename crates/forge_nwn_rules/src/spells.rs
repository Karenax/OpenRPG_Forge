//! Data-driven spell subset, spell slots, rest and timed effects.

use crate::character::{CharacterState, DamageType, Dice, SaveKind};
use crate::combat::{CombatModifiers, SpecialSave, saving_throw};
use crate::profile::NwnRulesProfile;
use crate::rng::{DeterministicRng, RngError};
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;
use std::fmt;

pub const SPELL_SLOTS_SCHEMA: &str = "forge.nwn-spell-slots@1";
pub const MAX_EFFECTS: usize = 64;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub enum SpellTarget {
    SingleEnemy,
    SingleAlly,
    SelfOnly,
    AreaEnemy,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub enum SpellEffect {
    Damage {
        dice: Dice,
        damage_type: DamageType,
    },
    Heal {
        dice: Dice,
        bonus: i32,
    },
    Buff {
        attack_bonus: i32,
        ac_bonus: i32,
        duration_rounds: u32,
    },
    Control {
        duration_rounds: u32,
        save_dc: i32,
        attack_penalty: i32,
        ac_penalty: i32,
    },
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SpellDefinition {
    pub id: String,
    pub name: String,
    pub level: u8,
    pub target: SpellTarget,
    pub effect: SpellEffect,
    pub vfx_supported: bool,
    pub source: String,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SpellCatalog {
    pub definitions: BTreeMap<String, SpellDefinition>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SpellSlotTable {
    pub schema_version: String,
    pub max: BTreeMap<u8, u8>,
    pub used: BTreeMap<u8, u8>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ActiveEffect {
    pub id: String,
    pub source_spell: String,
    pub attack_bonus: i32,
    pub ac_bonus: i32,
    pub remaining_rounds: u32,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SpellResult {
    pub spell: String,
    pub caster: String,
    pub target: String,
    pub slot_level: u8,
    pub damage: i32,
    pub healing: i32,
    pub effect_added: Option<String>,
    #[serde(default)]
    pub save: Option<SpecialSave>,
    pub vfx_supported: bool,
    pub note: String,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum SpellError {
    UnknownSpell(String),
    InvalidDefinition(String),
    NoSlot(u8),
    WrongTarget { spell: String, target: String },
    EffectLimit(usize),
    Rng(RngError),
    Corrupt(String),
}

impl fmt::Display for SpellError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::UnknownSpell(id) => write!(formatter, "unknown spell {id}"),
            Self::InvalidDefinition(id) => write!(formatter, "invalid spell definition {id}"),
            Self::NoSlot(level) => write!(formatter, "no remaining spell slot at level {level}"),
            Self::WrongTarget { spell, target } => {
                write!(formatter, "spell {spell} cannot target {target}")
            }
            Self::EffectLimit(limit) => write!(formatter, "active effect limit exceeded ({limit})"),
            Self::Rng(error) => write!(formatter, "RNG error: {error}"),
            Self::Corrupt(detail) => write!(formatter, "corrupt spell state: {detail}"),
        }
    }
}

impl std::error::Error for SpellError {}

impl From<RngError> for SpellError {
    fn from(error: RngError) -> Self {
        Self::Rng(error)
    }
}

impl SpellDefinition {
    pub fn validate(&self) -> Result<(), SpellError> {
        if self.id.trim().is_empty() || self.name.trim().is_empty() || self.source.trim().is_empty()
        {
            return Err(SpellError::InvalidDefinition(self.id.clone()));
        }
        if !(1..=9).contains(&self.level) {
            return Err(SpellError::InvalidDefinition(self.id.clone()));
        }
        match self.effect {
            SpellEffect::Damage { dice, .. } | SpellEffect::Heal { dice, .. } => dice
                .validate()
                .map_err(|_| SpellError::InvalidDefinition(self.id.clone()))?,
            SpellEffect::Buff {
                duration_rounds, ..
            }
            | SpellEffect::Control {
                duration_rounds, ..
            } => {
                if duration_rounds == 0 || duration_rounds > 1_000 {
                    return Err(SpellError::InvalidDefinition(self.id.clone()));
                }
            }
        }
        Ok(())
    }
}

impl SpellCatalog {
    pub fn add(&mut self, definition: SpellDefinition) -> Result<(), SpellError> {
        definition.validate()?;
        self.definitions.insert(definition.id.clone(), definition);
        Ok(())
    }

    #[must_use]
    pub fn get(&self, id: &str) -> Option<&SpellDefinition> {
        self.definitions.get(id)
    }

    pub fn synthetic() -> Result<Self, SpellError> {
        let mut catalog = Self {
            definitions: BTreeMap::new(),
        };
        for definition in [
            SpellDefinition {
                id: "magic-missile".to_owned(),
                name: "Projectile magique".to_owned(),
                level: 1,
                target: SpellTarget::SingleEnemy,
                effect: SpellEffect::Damage {
                    dice: Dice::new(1, 4, 1),
                    damage_type: DamageType::Magic,
                },
                vfx_supported: false,
                source: "synthetic M4 fixture; NWN spell semantics approximated".to_owned(),
            },
            SpellDefinition {
                id: "cure-light-wounds".to_owned(),
                name: "Soins légers".to_owned(),
                level: 1,
                target: SpellTarget::SingleAlly,
                effect: SpellEffect::Heal {
                    dice: Dice::new(1, 8, 1),
                    bonus: 1,
                },
                vfx_supported: false,
                source: "synthetic M4 fixture; NWN spell semantics approximated".to_owned(),
            },
            SpellDefinition {
                id: "bless".to_owned(),
                name: "Bénédiction".to_owned(),
                level: 1,
                target: SpellTarget::SingleAlly,
                effect: SpellEffect::Buff {
                    attack_bonus: 1,
                    ac_bonus: 0,
                    duration_rounds: 3,
                },
                vfx_supported: false,
                source: "synthetic M4 fixture; NWN spell semantics approximated".to_owned(),
            },
            SpellDefinition {
                id: "sleep".to_owned(),
                name: "Sommeil".to_owned(),
                level: 1,
                target: SpellTarget::AreaEnemy,
                effect: SpellEffect::Control {
                    duration_rounds: 2,
                    save_dc: 11,
                    attack_penalty: 2,
                    ac_penalty: 2,
                },
                vfx_supported: false,
                source: "synthetic M4 fixture; NWN spell semantics approximated".to_owned(),
            },
        ] {
            catalog.add(definition)?;
        }
        Ok(catalog)
    }
}

impl SpellSlotTable {
    #[must_use]
    pub fn new(levels: &[(u8, u8)]) -> Self {
        let max = levels.iter().copied().collect::<BTreeMap<_, _>>();
        Self {
            schema_version: SPELL_SLOTS_SCHEMA.to_owned(),
            max,
            used: BTreeMap::new(),
        }
    }

    #[must_use]
    pub fn remaining(&self, level: u8) -> u8 {
        self.max.get(&level).copied().unwrap_or(0) - self.used.get(&level).copied().unwrap_or(0)
    }

    pub fn consume(&mut self, level: u8) -> Result<(), SpellError> {
        if self.remaining(level) == 0 {
            return Err(SpellError::NoSlot(level));
        }
        *self.used.entry(level).or_insert(0) += 1;
        Ok(())
    }

    pub fn rest(&mut self) {
        self.used.clear();
    }

    pub fn validate(&self) -> Result<(), SpellError> {
        if self.schema_version != SPELL_SLOTS_SCHEMA {
            return Err(SpellError::Corrupt(format!(
                "unsupported schema {}",
                self.schema_version
            )));
        }
        for (level, used) in &self.used {
            if *used > self.max.get(level).copied().unwrap_or(0) {
                return Err(SpellError::Corrupt(format!(
                    "level {level} uses {used} slots beyond its maximum"
                )));
            }
        }
        Ok(())
    }
}

#[allow(clippy::too_many_arguments)]
pub fn cast_spell(
    profile: &NwnRulesProfile,
    catalog: &SpellCatalog,
    slots: &mut SpellSlotTable,
    spell_id: &str,
    caster: &CharacterState,
    target: &mut CharacterState,
    effects: &mut Vec<ActiveEffect>,
    rng: &mut DeterministicRng,
) -> Result<SpellResult, SpellError> {
    let spell = catalog
        .get(spell_id)
        .ok_or_else(|| SpellError::UnknownSpell(spell_id.to_owned()))?
        .clone();
    spell.validate()?;
    let valid_target = match spell.target {
        SpellTarget::SingleEnemy | SpellTarget::AreaEnemy => {
            caster.template.faction_id != target.template.faction_id
        }
        SpellTarget::SingleAlly => caster.template.faction_id == target.template.faction_id,
        SpellTarget::SelfOnly => caster.template.id == target.template.id,
    };
    if !valid_target {
        return Err(SpellError::WrongTarget {
            spell: spell.id.clone(),
            target: target.template.id.clone(),
        });
    }
    if matches!(
        spell.effect,
        SpellEffect::Buff { .. } | SpellEffect::Control { .. }
    ) && effects.len() >= MAX_EFFECTS
    {
        return Err(SpellError::EffectLimit(MAX_EFFECTS));
    }
    slots.consume(spell.level)?;
    apply_spell(profile, &spell, caster, target, effects, rng)
}

/// Cast an area spell once, applying it to every hostile target. One slot is
/// consumed; damage is rolled per target so the trace records each result.
#[allow(clippy::too_many_arguments)]
pub fn cast_area_spell(
    profile: &NwnRulesProfile,
    catalog: &SpellCatalog,
    slots: &mut SpellSlotTable,
    spell_id: &str,
    caster: &CharacterState,
    targets: &mut [CharacterState],
    effects: &mut Vec<ActiveEffect>,
    rng: &mut DeterministicRng,
) -> Result<Vec<SpellResult>, SpellError> {
    let spell = catalog
        .get(spell_id)
        .ok_or_else(|| SpellError::UnknownSpell(spell_id.to_owned()))?
        .clone();
    spell.validate()?;
    if spell.target != SpellTarget::AreaEnemy {
        return Err(SpellError::WrongTarget {
            spell: spell.id.clone(),
            target: "area".to_owned(),
        });
    }
    slots.consume(spell.level)?;
    let mut results = Vec::new();
    for target in targets
        .iter_mut()
        .filter(|target| caster.template.faction_id != target.template.faction_id)
    {
        results.push(apply_spell(profile, &spell, caster, target, effects, rng)?);
    }
    Ok(results)
}

#[allow(clippy::too_many_lines)]
fn apply_spell(
    profile: &NwnRulesProfile,
    spell: &SpellDefinition,
    caster: &CharacterState,
    target: &mut CharacterState,
    effects: &mut Vec<ActiveEffect>,
    rng: &mut DeterministicRng,
) -> Result<SpellResult, SpellError> {
    let mut damage = 0;
    let mut healing = 0;
    let mut effect_added = None;
    let mut save = None;
    let note;
    match spell.effect {
        SpellEffect::Damage { dice, damage_type } => {
            if target.template.is_immune_to(damage_type) {
                note = "immune".to_owned();
            } else {
                let rolls = rng.roll_dice(
                    dice.count,
                    dice.sides,
                    format!("spell:{}:{}", spell.id, target.template.id),
                )?;
                damage = i32::try_from(rolls.iter().sum::<u32>()).unwrap_or(i32::MAX) + dice.bonus;
                target.take_damage(damage);
                note = "damage".to_owned();
            }
        }
        SpellEffect::Heal { dice, bonus } => {
            let rolls = rng.roll_dice(
                dice.count,
                dice.sides,
                format!("spell:{}:{}", spell.id, target.template.id),
            )?;
            let rolled =
                i32::try_from(rolls.iter().sum::<u32>()).unwrap_or(i32::MAX) + dice.bonus + bonus;
            let before = target.current_hp;
            target.heal(rolled);
            healing = target.current_hp - before;
            note = "heal".to_owned();
        }
        SpellEffect::Buff {
            attack_bonus,
            ac_bonus,
            duration_rounds,
        } => {
            let effect_id = format!("effect-{}", effects.len());
            effects.push(ActiveEffect {
                id: effect_id.clone(),
                source_spell: spell.id.clone(),
                attack_bonus,
                ac_bonus,
                remaining_rounds: duration_rounds,
            });
            effect_added = Some(effect_id);
            note = "buff".to_owned();
        }
        SpellEffect::Control {
            duration_rounds,
            save_dc,
            attack_penalty,
            ac_penalty,
        } => {
            let success = saving_throw(profile, target, SaveKind::Will, save_dc, rng)
                .map_err(|error| SpellError::Corrupt(error.to_string()))?;
            save = Some(SpecialSave {
                kind: SaveKind::Will,
                dc: save_dc,
                success,
            });
            if success {
                note = "control-saved".to_owned();
            } else {
                let effect_id = format!("effect-{}", effects.len());
                effects.push(ActiveEffect {
                    id: effect_id.clone(),
                    source_spell: spell.id.clone(),
                    attack_bonus: -attack_penalty,
                    ac_bonus: -ac_penalty,
                    remaining_rounds: duration_rounds,
                });
                effect_added = Some(effect_id);
                note = "control".to_owned();
            }
        }
    }
    Ok(SpellResult {
        spell: spell.id.clone(),
        caster: caster.template.id.clone(),
        target: target.template.id.clone(),
        slot_level: spell.level,
        damage,
        healing,
        effect_added,
        save,
        vfx_supported: spell.vfx_supported,
        note,
    })
}

#[must_use]
pub fn combined_modifiers(effects: &[ActiveEffect]) -> CombatModifiers {
    let mut modifiers = CombatModifiers::default();
    for effect in effects {
        if effect.remaining_rounds > 0 {
            modifiers.attack_bonus += effect.attack_bonus;
            modifiers.ac_bonus += effect.ac_bonus;
        }
    }
    modifiers
}

pub fn tick_effects(effects: &mut Vec<ActiveEffect>) -> Vec<String> {
    let mut expired = Vec::new();
    for effect in effects.iter_mut() {
        effect.remaining_rounds = effect.remaining_rounds.saturating_sub(1);
        if effect.remaining_rounds == 0 {
            expired.push(effect.id.clone());
        }
    }
    effects.retain(|effect| effect.remaining_rounds > 0);
    expired
}

pub fn validate_effects(effects: &[ActiveEffect]) -> Result<(), SpellError> {
    if effects.len() > MAX_EFFECTS {
        return Err(SpellError::EffectLimit(effects.len()));
    }
    for effect in effects {
        if effect.id.trim().is_empty()
            || effect.source_spell.trim().is_empty()
            || effect.remaining_rounds == 0
            || effect.remaining_rounds > 1_000
        {
            return Err(SpellError::Corrupt(format!(
                "invalid active effect {}",
                effect.id
            )));
        }
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::character::{CharacterTemplate, Position};

    fn caster_and_target() -> (CharacterState, CharacterState) {
        let caster = CharacterState::new(
            CharacterTemplate::synthetic_fighter(),
            Position::new(0.0, 0.0),
        );
        let mut guardian = CharacterTemplate::synthetic_fighter();
        guardian.id = "guardian".to_owned();
        guardian.faction_id = 1;
        guardian.hit_points = 8;
        let target = CharacterState::new(guardian, Position::new(0.0, 3.0));
        (caster, target)
    }

    fn profile() -> NwnRulesProfile {
        NwnRulesProfile::vanilla_subset()
    }

    #[test]
    fn spell_slots_are_consumed_and_rest_restores_them() {
        let catalog = SpellCatalog::synthetic().expect("catalog");
        let mut slots = SpellSlotTable::new(&[(1, 2)]);
        let (caster, mut target) = caster_and_target();
        let mut effects = Vec::new();
        let mut rng = DeterministicRng::new(5);
        cast_spell(
            &profile(),
            &catalog,
            &mut slots,
            "magic-missile",
            &caster,
            &mut target,
            &mut effects,
            &mut rng,
        )
        .expect("cast");
        assert_eq!(slots.remaining(1), 1);
        slots.rest();
        assert_eq!(slots.remaining(1), 2);
    }

    #[test]
    fn buff_effects_expire_after_their_duration() {
        let mut effects = vec![ActiveEffect {
            id: "effect-0".to_owned(),
            source_spell: "bless".to_owned(),
            attack_bonus: 1,
            ac_bonus: 0,
            remaining_rounds: 2,
        }];
        assert_eq!(combined_modifiers(&effects).attack_bonus, 1);
        assert!(tick_effects(&mut effects).is_empty());
        assert_eq!(tick_effects(&mut effects), vec!["effect-0".to_owned()]);
        assert!(effects.is_empty());
    }

    #[test]
    fn enemy_spell_cannot_target_an_ally() {
        let catalog = SpellCatalog::synthetic().expect("catalog");
        let mut slots = SpellSlotTable::new(&[(1, 1)]);
        let (caster, mut target) = caster_and_target();
        target.template.faction_id = 0;
        let mut effects = Vec::new();
        let mut rng = DeterministicRng::new(1);
        assert!(matches!(
            cast_spell(
                &profile(),
                &catalog,
                &mut slots,
                "magic-missile",
                &caster,
                &mut target,
                &mut effects,
                &mut rng
            ),
            Err(SpellError::WrongTarget { .. })
        ));
    }

    #[test]
    fn area_control_casts_once_and_records_a_save_per_target() {
        let catalog = SpellCatalog::synthetic().expect("catalog");
        let mut slots = SpellSlotTable::new(&[(1, 1)]);
        let (caster, first) = caster_and_target();
        let mut second = first.clone();
        second.template.id = "guardian-2".to_owned();
        let mut targets = vec![first, second];
        let mut effects = Vec::new();
        let mut rng = DeterministicRng::new(13);
        let results = cast_area_spell(
            &profile(),
            &catalog,
            &mut slots,
            "sleep",
            &caster,
            &mut targets,
            &mut effects,
            &mut rng,
        )
        .expect("area");
        assert_eq!(results.len(), 2);
        assert_eq!(slots.remaining(1), 0);
        assert!(results.iter().all(|result| result.save.is_some()));
        assert!(
            results
                .iter()
                .all(|result| result.save.is_some_and(|save| save.kind == SaveKind::Will))
        );
        assert!(
            results
                .iter()
                .all(|result| matches!(result.note.as_str(), "control" | "control-saved"))
        );
    }
}
