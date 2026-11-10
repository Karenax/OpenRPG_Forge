//! Data-driven character sheets and bounded derived statistics.

use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;
use std::fmt;

pub const MAX_LEVEL: u8 = 40;
pub const MAX_ABILITY: i16 = 50;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AbilityScores {
    pub strength: i16,
    pub dexterity: i16,
    pub constitution: i16,
    pub intelligence: i16,
    pub wisdom: i16,
    pub charisma: i16,
}

impl AbilityScores {
    #[must_use]
    pub const fn all(value: i16) -> Self {
        Self {
            strength: value,
            dexterity: value,
            constitution: value,
            intelligence: value,
            wisdom: value,
            charisma: value,
        }
    }

    #[must_use]
    pub const fn modifier(score: i16) -> i32 {
        (score as i32 - 10).div_euclid(2)
    }

    #[must_use]
    pub const fn strength_modifier(self) -> i32 {
        Self::modifier(self.strength)
    }

    #[must_use]
    pub const fn dexterity_modifier(self) -> i32 {
        Self::modifier(self.dexterity)
    }

    #[must_use]
    pub const fn constitution_modifier(self) -> i32 {
        Self::modifier(self.constitution)
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub enum AttackAbility {
    Strength,
    Dexterity,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub enum DamageType {
    Slashing,
    Piercing,
    Bludgeoning,
    Fire,
    Cold,
    Acid,
    Electrical,
    Magic,
    Positive,
    Negative,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Dice {
    pub count: u8,
    pub sides: u32,
    pub bonus: i32,
}

impl Dice {
    #[must_use]
    pub const fn new(count: u8, sides: u32, bonus: i32) -> Self {
        Self {
            count,
            sides,
            bonus,
        }
    }

    #[must_use]
    pub const fn minimum(self) -> i32 {
        self.count as i32 + self.bonus
    }

    pub fn validate(self) -> Result<(), CharacterError> {
        if self.count == 0 || self.count > 20 {
            return Err(CharacterError::DiceCount(self.count));
        }
        if self.sides < 2 || self.sides > 1000 {
            return Err(CharacterError::DiceSides(self.sides));
        }
        Ok(())
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Weapon {
    pub name: String,
    pub damage: Dice,
    pub critical_threat_min: u32,
    pub critical_multiplier: u8,
    pub attack_bonus: i32,
    pub damage_bonus: i32,
    pub damage_type: DamageType,
    pub ignores_damage_reduction: bool,
}

impl Weapon {
    #[must_use]
    pub fn longsword() -> Self {
        Self {
            name: "longsword".to_owned(),
            damage: Dice::new(1, 8, 0),
            critical_threat_min: 19,
            critical_multiplier: 2,
            attack_bonus: 0,
            damage_bonus: 0,
            damage_type: DamageType::Slashing,
            ignores_damage_reduction: false,
        }
    }

    #[must_use]
    pub fn unarmed() -> Self {
        Self {
            name: "unarmed".to_owned(),
            damage: Dice::new(1, 2, 0),
            critical_threat_min: 20,
            critical_multiplier: 2,
            attack_bonus: 0,
            damage_bonus: 0,
            damage_type: DamageType::Bludgeoning,
            ignores_damage_reduction: false,
        }
    }

    pub fn validate(&self) -> Result<(), CharacterError> {
        if self.name.trim().is_empty() {
            return Err(CharacterError::EmptyName("weapon"));
        }
        self.damage.validate()?;
        if !(1..=20).contains(&self.critical_threat_min) {
            return Err(CharacterError::CriticalThreat(self.critical_threat_min));
        }
        if !(1..=4).contains(&self.critical_multiplier) {
            return Err(CharacterError::CriticalMultiplier(self.critical_multiplier));
        }
        Ok(())
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ArmorClass {
    pub armor: i32,
    pub shield: i32,
    pub natural: i32,
    pub deflection: i32,
    pub dodge: i32,
    pub size: i32,
    pub misc: i32,
    pub max_dexterity: Option<i32>,
}

impl ArmorClass {
    #[must_use]
    pub const fn unarmored() -> Self {
        Self {
            armor: 0,
            shield: 0,
            natural: 0,
            deflection: 0,
            dodge: 0,
            size: 0,
            misc: 0,
            max_dexterity: None,
        }
    }

    #[must_use]
    pub fn total(self, dexterity_modifier: i32) -> i32 {
        let dexterity = self
            .max_dexterity
            .map_or(dexterity_modifier, |cap| dexterity_modifier.min(cap));
        10 + self.armor
            + self.shield
            + self.natural
            + self.deflection
            + self.dodge
            + self.size
            + self.misc
            + dexterity
    }

    pub fn validate(self) -> Result<(), CharacterError> {
        if self.armor < 0
            || self.shield < 0
            || self.natural < 0
            || self.deflection < 0
            || self.dodge < 0
        {
            return Err(CharacterError::NegativeArmorClass);
        }
        if self.max_dexterity.is_some_and(|cap| cap < 0) {
            return Err(CharacterError::NegativeMaxDexterity);
        }
        Ok(())
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SavingThrows {
    pub fortitude: i32,
    pub reflex: i32,
    pub will: i32,
}

impl SavingThrows {
    #[must_use]
    pub const fn bonus(self, kind: SaveKind) -> i32 {
        match kind {
            SaveKind::Fortitude => self.fortitude,
            SaveKind::Reflex => self.reflex,
            SaveKind::Will => self.will,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub enum SaveKind {
    Fortitude,
    Reflex,
    Will,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CharacterClass {
    pub name: String,
    pub level: u8,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CharacterTemplate {
    pub id: String,
    pub name: String,
    pub faction_id: u16,
    pub abilities: AbilityScores,
    pub level: u8,
    pub classes: Vec<CharacterClass>,
    pub hit_points: i32,
    pub bab: i32,
    pub ac: ArmorClass,
    pub saves: SavingThrows,
    pub weapon: Option<Weapon>,
    pub attack_ability: AttackAbility,
    pub damage_reduction: i32,
    pub damage_immunities: Vec<DamageType>,
    pub concealment_percent: i32,
    pub feats: Vec<String>,
    pub skills: BTreeMap<String, i32>,
    pub source: String,
}

impl CharacterTemplate {
    #[must_use]
    pub fn synthetic_fighter() -> Self {
        Self {
            id: "synthetic-fighter".to_owned(),
            name: "Combattant synthétique".to_owned(),
            faction_id: 0,
            abilities: AbilityScores {
                strength: 14,
                dexterity: 12,
                constitution: 12,
                intelligence: 10,
                wisdom: 10,
                charisma: 10,
            },
            level: 3,
            classes: vec![CharacterClass {
                name: "fighter".to_owned(),
                level: 3,
            }],
            hit_points: 24,
            bab: 3,
            ac: ArmorClass::unarmored(),
            saves: SavingThrows {
                fortitude: 4,
                reflex: 2,
                will: 1,
            },
            weapon: Some(Weapon::longsword()),
            attack_ability: AttackAbility::Strength,
            damage_reduction: 0,
            damage_immunities: Vec::new(),
            concealment_percent: 0,
            feats: vec!["weapon-focus-longsword".to_owned()],
            skills: BTreeMap::new(),
            source: "synthetic M4 fixture; not a 2DA extraction".to_owned(),
        }
    }

    pub fn validate(&self) -> Result<(), CharacterError> {
        if self.id.trim().is_empty() {
            return Err(CharacterError::EmptyName("id"));
        }
        if self.name.trim().is_empty() {
            return Err(CharacterError::EmptyName("name"));
        }
        if self.level == 0 || self.level > MAX_LEVEL {
            return Err(CharacterError::Level(self.level));
        }
        for value in [
            self.abilities.strength,
            self.abilities.dexterity,
            self.abilities.constitution,
            self.abilities.intelligence,
            self.abilities.wisdom,
            self.abilities.charisma,
        ] {
            if !(1..=MAX_ABILITY).contains(&value) {
                return Err(CharacterError::Ability(value));
            }
        }
        if self.hit_points <= 0 {
            return Err(CharacterError::HitPoints(self.hit_points));
        }
        if self.bab < 0 || self.bab > i32::from(self.level) {
            return Err(CharacterError::BaseAttackBonus(self.bab));
        }
        self.ac.validate()?;
        if let Some(weapon) = &self.weapon {
            weapon.validate()?;
        }
        if self.damage_reduction < 0 {
            return Err(CharacterError::DamageReduction(self.damage_reduction));
        }
        if !(0..=100).contains(&self.concealment_percent) {
            return Err(CharacterError::Concealment(self.concealment_percent));
        }
        if self.source.trim().is_empty() {
            return Err(CharacterError::EmptySource);
        }
        Ok(())
    }

    #[must_use]
    pub fn dexterity_modifier(&self) -> i32 {
        self.abilities.dexterity_modifier()
    }

    #[must_use]
    pub fn armor_class(&self) -> i32 {
        self.ac.total(self.dexterity_modifier())
    }

    #[must_use]
    pub fn attack_ability_modifier(&self) -> i32 {
        match self.attack_ability {
            AttackAbility::Strength => self.abilities.strength_modifier(),
            AttackAbility::Dexterity => self.abilities.dexterity_modifier(),
        }
    }

    /// Base attack values before active effects. Iterative attacks use BAB
    /// progression, not the total attack bonus.
    #[must_use]
    pub fn base_attack_bonuses(&self) -> Vec<i32> {
        let mut bonuses = Vec::new();
        for index in 0..4 {
            let base = self.bab - 5 * index;
            if index > 0 && base <= 0 {
                break;
            }
            bonuses.push(base + self.attack_ability_modifier());
        }
        bonuses
    }

    #[must_use]
    pub fn weapon(&self) -> Option<&Weapon> {
        self.weapon.as_ref()
    }

    #[must_use]
    pub fn damage_bonus(&self) -> i32 {
        let weapon_bonus = self.weapon.as_ref().map_or(0, |weapon| weapon.damage_bonus);
        self.attack_ability_modifier() + weapon_bonus
    }

    #[must_use]
    pub fn saving_throw(&self, kind: SaveKind) -> i32 {
        self.saves.bonus(kind)
            + match kind {
                SaveKind::Fortitude => self.abilities.constitution_modifier(),
                SaveKind::Reflex => self.dexterity_modifier(),
                SaveKind::Will => AbilityScores::modifier(self.abilities.wisdom),
            }
    }

    #[must_use]
    pub fn is_immune_to(&self, damage_type: DamageType) -> bool {
        self.damage_immunities.contains(&damage_type)
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Position {
    pub x: f32,
    pub z: f32,
}

impl Position {
    #[must_use]
    pub const fn new(x: f32, z: f32) -> Self {
        Self { x, z }
    }

    #[must_use]
    pub fn distance(self, other: Self) -> f32 {
        let dx = self.x - other.x;
        let dz = self.z - other.z;
        (dx * dx + dz * dz).sqrt()
    }

    #[must_use]
    pub fn toward(self, target: Self, distance: f32) -> Self {
        let total = self.distance(target);
        if total <= f32::EPSILON || distance >= total {
            return target;
        }
        let ratio = distance / total;
        Self {
            x: self.x + (target.x - self.x) * ratio,
            z: self.z + (target.z - self.z) * ratio,
        }
    }

    pub fn validate(self) -> Result<(), CharacterError> {
        if !self.x.is_finite() || !self.z.is_finite() {
            return Err(CharacterError::Position);
        }
        Ok(())
    }
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CharacterState {
    pub template: CharacterTemplate,
    pub current_hp: i32,
    pub position: Position,
}

impl CharacterState {
    #[must_use]
    pub fn new(template: CharacterTemplate, position: Position) -> Self {
        Self {
            current_hp: template.hit_points,
            template,
            position,
        }
    }

    #[must_use]
    pub fn is_dead(&self) -> bool {
        self.current_hp <= 0
    }

    pub fn take_damage(&mut self, amount: i32) {
        self.current_hp = (self.current_hp - amount.max(0)).max(0);
    }

    pub fn heal(&mut self, amount: i32) {
        if !self.is_dead() {
            self.current_hp = (self.current_hp + amount.max(0)).min(self.template.hit_points);
        }
    }

    pub fn restore_full(&mut self) {
        self.current_hp = self.template.hit_points;
    }

    pub fn validate(&self) -> Result<(), CharacterError> {
        self.template.validate()?;
        self.position.validate()?;
        if self.current_hp < 0 || self.current_hp > self.template.hit_points {
            return Err(CharacterError::CurrentHitPoints(self.current_hp));
        }
        Ok(())
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum CharacterError {
    EmptyName(&'static str),
    Level(u8),
    Ability(i16),
    HitPoints(i32),
    CurrentHitPoints(i32),
    BaseAttackBonus(i32),
    DiceCount(u8),
    DiceSides(u32),
    CriticalThreat(u32),
    CriticalMultiplier(u8),
    NegativeArmorClass,
    NegativeMaxDexterity,
    DamageReduction(i32),
    Concealment(i32),
    Position,
    EmptySource,
}

impl fmt::Display for CharacterError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::EmptyName(kind) => write!(formatter, "{kind} must not be empty"),
            Self::Level(level) => write!(formatter, "invalid character level {level}"),
            Self::Ability(value) => write!(formatter, "invalid ability score {value}"),
            Self::HitPoints(value) => write!(formatter, "invalid hit points {value}"),
            Self::CurrentHitPoints(value) => {
                write!(formatter, "invalid current hit points {value}")
            }
            Self::BaseAttackBonus(value) => write!(formatter, "invalid base attack bonus {value}"),
            Self::DiceCount(value) => write!(formatter, "invalid dice count {value}"),
            Self::DiceSides(value) => write!(formatter, "invalid dice sides {value}"),
            Self::CriticalThreat(value) => write!(formatter, "invalid critical threat {value}"),
            Self::CriticalMultiplier(value) => {
                write!(formatter, "invalid critical multiplier {value}")
            }
            Self::NegativeArmorClass => formatter.write_str("armor class components must be >= 0"),
            Self::NegativeMaxDexterity => {
                formatter.write_str("max dexterity must be >= 0 when present")
            }
            Self::DamageReduction(value) => write!(formatter, "invalid damage reduction {value}"),
            Self::Concealment(value) => write!(formatter, "invalid concealment {value}"),
            Self::Position => formatter.write_str("position must be finite"),
            Self::EmptySource => formatter.write_str("character source must not be empty"),
        }
    }
}

impl std::error::Error for CharacterError {}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn ability_modifiers_use_d20_floor_division() {
        assert_eq!(AbilityScores::modifier(1), -5);
        assert_eq!(AbilityScores::modifier(9), -1);
        assert_eq!(AbilityScores::modifier(10), 0);
        assert_eq!(AbilityScores::modifier(11), 0);
        assert_eq!(AbilityScores::modifier(14), 2);
        assert_eq!(AbilityScores::modifier(20), 5);
    }

    #[test]
    fn armor_class_caps_dexterity_from_armor() {
        let ac = ArmorClass {
            armor: 4,
            max_dexterity: Some(2),
            ..ArmorClass::unarmored()
        };
        assert_eq!(ac.total(4), 16);
        assert_eq!(ac.total(1), 15);
    }

    #[test]
    fn iterative_attacks_follow_bab_progression() {
        let mut template = CharacterTemplate::synthetic_fighter();
        assert_eq!(template.base_attack_bonuses(), vec![5]);
        template.bab = 6;
        assert_eq!(template.base_attack_bonuses(), vec![8, 3]);
        template.bab = 11;
        assert_eq!(template.base_attack_bonuses(), vec![13, 8, 3]);
        template.bab = 16;
        assert_eq!(template.base_attack_bonuses(), vec![18, 13, 8, 3]);
    }

    #[test]
    fn synthetic_fighter_is_valid() {
        let fighter = CharacterTemplate::synthetic_fighter();
        assert_eq!(fighter.validate(), Ok(()));
        assert_eq!(fighter.armor_class(), 11);
        assert_eq!(fighter.saving_throw(SaveKind::Fortitude), 5);
    }
}
