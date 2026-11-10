//! Deterministic d20 combat for the NWN compatibility profile.
//!
//! This is a bounded subset: attacks, AC, iterative attacks, critical threats
//! and confirmation, concealment, damage reduction, immunities, initiative and
//! simple melee movement. It is not a complete NWN combat engine.

use crate::ai::{MeleeStep, plan_melee_step};
use crate::character::{CharacterState, DamageType, Position, SaveKind, Weapon};
use crate::factions::FactionTable;
use crate::profile::NwnRulesProfile;
use crate::rng::{DeterministicRng, RngError};
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;
use std::fmt;

#[derive(Debug, Clone, Copy, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CombatModifiers {
    pub attack_bonus: i32,
    pub damage_bonus: i32,
    pub ac_bonus: i32,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
#[allow(clippy::struct_excessive_bools)]
pub struct AttackOutcome {
    pub round: u32,
    pub attacker: String,
    pub target: String,
    pub attack_index: usize,
    pub natural: u32,
    pub total: i32,
    pub target_ac: i32,
    pub hit: bool,
    pub critical_threat: bool,
    pub critical_confirmed: bool,
    pub concealment_miss: bool,
    pub damage: i32,
    pub target_hp_after: i32,
    pub note: String,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum CombatAction {
    Move {
        round: u32,
        actor: String,
        from: Position,
        to: Position,
        distance_after: f32,
    },
    Attack {
        outcome: AttackOutcome,
    },
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct EncounterReport {
    pub schema: String,
    pub winner: Option<String>,
    pub rounds: u32,
    pub actions: Vec<CombatAction>,
    pub final_hp: BTreeMap<String, i32>,
    pub final_positions: BTreeMap<String, Position>,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct EncounterOutcome {
    pub report: EncounterReport,
    pub combatants: Vec<CharacterState>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum CombatError {
    Rng(RngError),
    NoWeapon(String),
    NotHostile { source: u16, target: u16 },
    Invalid(String),
}

impl fmt::Display for CombatError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Rng(error) => write!(formatter, "RNG error: {error}"),
            Self::NoWeapon(id) => write!(formatter, "character {id} has no weapon"),
            Self::NotHostile { source, target } => {
                write!(formatter, "factions {source} and {target} are not hostile")
            }
            Self::Invalid(detail) => write!(formatter, "combat refused: {detail}"),
        }
    }
}

impl std::error::Error for CombatError {}

impl From<RngError> for CombatError {
    fn from(error: RngError) -> Self {
        Self::Rng(error)
    }
}

/// Bounded NWN special attack subset. The mechanics are expressed as attack and
/// damage modifiers, never as hidden state mutations.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub enum SpecialAttack {
    PowerAttack,
    ImprovedPowerAttack,
    CalledShot,
    Knockdown,
    StunningFist,
}

impl SpecialAttack {
    #[must_use]
    pub const fn label(self) -> &'static str {
        match self {
            Self::PowerAttack => "power-attack",
            Self::ImprovedPowerAttack => "improved-power-attack",
            Self::CalledShot => "called-shot",
            Self::Knockdown => "knockdown",
            Self::StunningFist => "stunning-fist",
        }
    }

    #[must_use]
    pub const fn modifiers(self) -> CombatModifiers {
        match self {
            Self::PowerAttack => CombatModifiers {
                attack_bonus: -5,
                damage_bonus: 5,
                ac_bonus: 0,
            },
            Self::ImprovedPowerAttack => CombatModifiers {
                attack_bonus: -10,
                damage_bonus: 10,
                ac_bonus: 0,
            },
            Self::CalledShot | Self::Knockdown | Self::StunningFist => CombatModifiers {
                attack_bonus: -4,
                damage_bonus: 0,
                ac_bonus: 0,
            },
        }
    }

    #[must_use]
    pub const fn save(self) -> Option<(SaveKind, i32)> {
        match self {
            Self::Knockdown | Self::StunningFist => Some((SaveKind::Fortitude, 15)),
            _ => None,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SpecialSave {
    pub kind: SaveKind,
    pub dc: i32,
    pub success: bool,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SpecialAttackOutcome {
    pub attack: AttackOutcome,
    pub save: Option<SpecialSave>,
}

/// Resolve a special attack and its optional save. A missed attack never asks
/// for a save, so the RNG trace stays deterministic and minimal.
#[allow(clippy::too_many_arguments)]
pub fn resolve_special_attack(
    profile: &NwnRulesProfile,
    attacker: &CharacterState,
    target: &mut CharacterState,
    rng: &mut DeterministicRng,
    round: u32,
    attack_index: usize,
    attack: SpecialAttack,
    attacker_modifiers: CombatModifiers,
    target_modifiers: CombatModifiers,
) -> Result<SpecialAttackOutcome, CombatError> {
    let modifiers = CombatModifiers {
        attack_bonus: attacker_modifiers.attack_bonus + attack.modifiers().attack_bonus,
        damage_bonus: attacker_modifiers.damage_bonus + attack.modifiers().damage_bonus,
        ac_bonus: attacker_modifiers.ac_bonus,
    };
    let mut outcome = resolve_attack(
        profile,
        attacker,
        target,
        rng,
        round,
        attack_index,
        modifiers,
        target_modifiers,
    )?;
    if outcome.hit && !outcome.concealment_miss {
        outcome.note = format!("{}:{}", attack.label(), outcome.note);
    }
    let save = match attack.save() {
        Some((kind, dc)) if outcome.hit && !outcome.concealment_miss => {
            let success = saving_throw(profile, target, kind, dc, rng)?;
            Some(SpecialSave { kind, dc, success })
        }
        _ => None,
    };
    Ok(SpecialAttackOutcome {
        attack: outcome,
        save,
    })
}

/// Resolve one attack. The target is mutated only when damage is applied.
#[allow(clippy::too_many_arguments)]
#[allow(clippy::too_many_lines)]
pub fn resolve_attack(
    profile: &NwnRulesProfile,
    attacker: &CharacterState,
    target: &mut CharacterState,
    rng: &mut DeterministicRng,
    round: u32,
    attack_index: usize,
    attacker_modifiers: CombatModifiers,
    target_modifiers: CombatModifiers,
) -> Result<AttackOutcome, CombatError> {
    let weapon = attacker
        .template
        .weapon()
        .ok_or_else(|| CombatError::NoWeapon(attacker.template.id.clone()))?;
    let base = *attacker
        .template
        .base_attack_bonuses()
        .get(attack_index)
        .ok_or_else(|| CombatError::Invalid("attack index outside BAB sequence".to_owned()))?;
    let attack_bonus = base + weapon.attack_bonus + attacker_modifiers.attack_bonus;
    let target_ac = target.template.armor_class() + target_modifiers.ac_bonus;

    if target.template.concealment_percent > 0 {
        let concealment = rng.roll(100, format!("concealment:{}", target.template.id))?;
        if i32::try_from(concealment).unwrap_or(100) <= target.template.concealment_percent {
            return Ok(AttackOutcome {
                round,
                attacker: attacker.template.id.clone(),
                target: target.template.id.clone(),
                attack_index,
                natural: 0,
                total: attack_bonus,
                target_ac,
                hit: false,
                critical_threat: false,
                critical_confirmed: false,
                concealment_miss: true,
                damage: 0,
                target_hp_after: target.current_hp,
                note: "concealment".to_owned(),
            });
        }
    }

    let natural = rng.d20(format!(
        "attack:{}:{}:{}",
        attacker.template.id, target.template.id, attack_index
    ))?;
    let total = i32::try_from(natural).unwrap_or(20) + attack_bonus;
    let mut hit = total >= target_ac;
    if profile.natural_one_misses && natural == 1 {
        hit = false;
    }
    if profile.natural_twenty_hits && natural == 20 {
        hit = true;
    }
    let critical_threat = hit && natural >= weapon.critical_threat_min;
    let mut critical_confirmed = false;
    if critical_threat && profile.critical_confirmation {
        let confirmation = rng.d20(format!(
            "critical-confirm:{}:{}:{}",
            attacker.template.id, target.template.id, attack_index
        ))?;
        let confirmation_total = i32::try_from(confirmation).unwrap_or(20) + attack_bonus;
        critical_confirmed =
            confirmation_total >= target_ac && !(profile.natural_one_misses && confirmation == 1);
    }

    let mut damage = 0;
    let note;
    if !hit {
        note = if natural == 1 {
            "natural-1".to_owned()
        } else {
            "miss".to_owned()
        };
    } else if target.template.is_immune_to(weapon.damage_type) {
        note = "immune".to_owned();
    } else {
        let rolls = rng.roll_dice(
            weapon.damage.count,
            weapon.damage.sides,
            format!(
                "damage:{}:{}:{}",
                attacker.template.id, target.template.id, attack_index
            ),
        )?;
        let mut rolled = i32::try_from(rolls.iter().sum::<u32>()).unwrap_or(i32::MAX);
        rolled += weapon.damage.bonus
            + attacker.template.damage_bonus()
            + attacker_modifiers.damage_bonus;
        if critical_confirmed {
            rolled *= i32::from(weapon.critical_multiplier);
        }
        if !weapon.ignores_damage_reduction {
            rolled -= target.template.damage_reduction;
        }
        damage = rolled.max(0);
        note = if critical_confirmed {
            "critical".to_owned()
        } else if critical_threat {
            "critical-not-confirmed".to_owned()
        } else {
            "hit".to_owned()
        };
    }
    target.take_damage(damage);
    Ok(AttackOutcome {
        round,
        attacker: attacker.template.id.clone(),
        target: target.template.id.clone(),
        attack_index,
        natural,
        total,
        target_ac,
        hit,
        critical_threat,
        critical_confirmed,
        concealment_miss: false,
        damage,
        target_hp_after: target.current_hp,
        note,
    })
}

/// Run a deterministic melee encounter between two hostile combatants.
pub fn run_encounter(
    profile: &NwnRulesProfile,
    factions: &FactionTable,
    left: &CharacterState,
    right: &CharacterState,
    left_modifiers: CombatModifiers,
    right_modifiers: CombatModifiers,
    rng: &mut DeterministicRng,
) -> Result<EncounterOutcome, CombatError> {
    run_encounter_with_perception(
        profile,
        factions,
        left,
        right,
        left_modifiers,
        right_modifiers,
        rng,
        None,
    )
}

/// Same encounter with an explicit deterministic perception gate. A combatant
/// that cannot perceive its target performs no attack; line of sight is the
/// caller's responsibility, as the M4 arena is open and obstacle-free.
#[allow(clippy::too_many_arguments, clippy::too_many_lines)]
pub fn run_encounter_with_perception(
    profile: &NwnRulesProfile,
    factions: &FactionTable,
    left: &CharacterState,
    right: &CharacterState,
    left_modifiers: CombatModifiers,
    right_modifiers: CombatModifiers,
    rng: &mut DeterministicRng,
    perception: Option<crate::ai::Perception>,
) -> Result<EncounterOutcome, CombatError> {
    left.validate()
        .map_err(|error| CombatError::Invalid(error.to_string()))?;
    right
        .validate()
        .map_err(|error| CombatError::Invalid(error.to_string()))?;
    if !factions.is_hostile(left.template.faction_id, right.template.faction_id)
        && !factions.is_hostile(right.template.faction_id, left.template.faction_id)
    {
        return Err(CombatError::NotHostile {
            source: left.template.faction_id,
            target: right.template.faction_id,
        });
    }

    let mut combatants = [left.clone(), right.clone()];
    let left_initiative = i32::try_from(rng.d20(format!("initiative:{}", left.template.id))?)
        .unwrap_or(20)
        + left.template.dexterity_modifier();
    let right_initiative = i32::try_from(rng.d20(format!("initiative:{}", right.template.id))?)
        .unwrap_or(20)
        + right.template.dexterity_modifier();
    let mut order = match left_initiative.cmp(&right_initiative) {
        std::cmp::Ordering::Equal => {
            if left.template.id <= right.template.id {
                [0_usize, 1]
            } else {
                [1, 0]
            }
        }
        std::cmp::Ordering::Greater => [0, 1],
        std::cmp::Ordering::Less => [1, 0],
    };

    let mut actions = Vec::new();
    let mut round = 0_u32;
    while round < profile.max_rounds && combatants[0].current_hp > 0 && combatants[1].current_hp > 0
    {
        round += 1;
        for actor_index in order {
            let target_index = 1 - actor_index;
            if combatants[actor_index].current_hp <= 0 || combatants[target_index].current_hp <= 0 {
                continue;
            }
            if let Some(perception) = perception {
                let distance = combatants[actor_index]
                    .position
                    .distance(combatants[target_index].position);
                if !perception.can_perceive(distance, true) {
                    continue;
                }
            }
            let step = plan_melee_step(
                combatants[actor_index].position,
                combatants[target_index].position,
                f32::from(u16::try_from(profile.melee_reach_m).unwrap_or(2)),
                f32::from(u16::try_from(profile.move_speed_m_per_round).unwrap_or(3)),
            );
            if let MeleeStep::Move { to } = step {
                let from = combatants[actor_index].position;
                combatants[actor_index].position = to;
                actions.push(CombatAction::Move {
                    round,
                    actor: combatants[actor_index].template.id.clone(),
                    from,
                    to,
                    distance_after: to.distance(combatants[target_index].position),
                });
                continue;
            }
            let attack_count = combatants[actor_index].template.base_attack_bonuses().len();
            for attack_index in 0..attack_count {
                if combatants[target_index].current_hp <= 0 {
                    break;
                }
                let (actor, target) = if actor_index == 0 {
                    let (a, b) = combatants.split_at_mut(1);
                    (&a[0], &mut b[0])
                } else {
                    let (a, b) = combatants.split_at_mut(1);
                    (&b[0], &mut a[0])
                };
                let (actor_modifiers, target_modifiers) = if actor_index == 0 {
                    (left_modifiers, right_modifiers)
                } else {
                    (right_modifiers, left_modifiers)
                };
                let outcome = resolve_attack(
                    profile,
                    actor,
                    target,
                    rng,
                    round,
                    attack_index,
                    actor_modifiers,
                    target_modifiers,
                )?;
                actions.push(CombatAction::Attack { outcome });
            }
        }
        order = [order[1], order[0]];
    }

    let winner = if combatants[0].current_hp <= 0 && combatants[1].current_hp <= 0 {
        None
    } else if combatants[0].current_hp <= 0 {
        Some(combatants[1].template.id.clone())
    } else if combatants[1].current_hp <= 0 {
        Some(combatants[0].template.id.clone())
    } else {
        None
    };
    Ok(EncounterOutcome {
        report: EncounterReport {
            schema: "forge.nwn-combat-encounter@1".to_owned(),
            winner,
            rounds: round,
            actions,
            final_hp: combatants
                .iter()
                .map(|state| (state.template.id.clone(), state.current_hp))
                .collect(),
            final_positions: combatants
                .iter()
                .map(|state| (state.template.id.clone(), state.position))
                .collect(),
        },
        combatants: combatants.to_vec(),
    })
}

pub fn saving_throw(
    profile: &NwnRulesProfile,
    target: &mut CharacterState,
    kind: SaveKind,
    dc: i32,
    rng: &mut DeterministicRng,
) -> Result<bool, CombatError> {
    let natural = rng.d20(format!("save:{:?}:{}", kind, target.template.id))?;
    let total = i32::try_from(natural).unwrap_or(20) + target.template.saving_throw(kind);
    if profile.natural_one_misses && natural == 1 {
        return Ok(false);
    }
    if profile.natural_twenty_hits && natural == 20 {
        return Ok(true);
    }
    Ok(total >= dc)
}

#[must_use]
pub fn weapon_damage_type(weapon: &Weapon) -> DamageType {
    weapon.damage_type
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::character::{CharacterTemplate, Position};

    fn fighter_and_guardian() -> (CharacterState, CharacterState) {
        let fighter = CharacterState::new(
            CharacterTemplate::synthetic_fighter(),
            Position::new(0.0, 0.0),
        );
        let mut guardian = CharacterTemplate::synthetic_fighter();
        guardian.id = "ab_guardian".to_owned();
        guardian.name = "Le gardien".to_owned();
        guardian.faction_id = 1;
        guardian.hit_points = 8;
        guardian.bab = 1;
        guardian.ac = crate::character::ArmorClass::unarmored();
        guardian.saves = crate::character::SavingThrows {
            fortitude: 2,
            reflex: 0,
            will: 0,
        };
        guardian.classes = vec![crate::character::CharacterClass {
            name: "fighter".to_owned(),
            level: 1,
        }];
        let guardian = CharacterState::new(guardian, Position::new(0.0, 6.0));
        (fighter, guardian)
    }

    #[test]
    fn combat_is_deterministic_with_the_same_seed() {
        let profile = NwnRulesProfile::vanilla_subset();
        let factions = FactionTable::default();
        let (fighter, guardian) = fighter_and_guardian();
        let mut left_rng = DeterministicRng::new(99);
        let mut right_rng = DeterministicRng::new(99);
        let left = run_encounter(
            &profile,
            &factions,
            &fighter,
            &guardian,
            CombatModifiers::default(),
            CombatModifiers::default(),
            &mut left_rng,
        )
        .expect("encounter");
        let right = run_encounter(
            &profile,
            &factions,
            &fighter,
            &guardian,
            CombatModifiers::default(),
            CombatModifiers::default(),
            &mut right_rng,
        )
        .expect("encounter");
        assert_eq!(left, right);
        assert_eq!(left_rng.trace(), right_rng.trace());
    }

    #[test]
    fn a_natural_one_always_misses_and_a_natural_twenty_hits() {
        let profile = NwnRulesProfile::vanilla_subset();
        let (fighter, mut target) = fighter_and_guardian();
        target.template.ac.armor = 100;
        let mut rng = DeterministicRng::new(1);
        let outcome = resolve_attack(
            &profile,
            &fighter,
            &mut target,
            &mut rng,
            1,
            0,
            CombatModifiers::default(),
            CombatModifiers::default(),
        )
        .expect("attack");
        assert!(!outcome.hit || outcome.natural == 20);
    }

    #[test]
    fn non_hostile_factions_cannot_start_an_encounter() {
        let profile = NwnRulesProfile::vanilla_subset();
        let factions = FactionTable::default();
        let (fighter, guardian) = fighter_and_guardian();
        let mut peaceful = guardian;
        peaceful.template.faction_id = 2;
        let mut rng = DeterministicRng::new(1);
        assert!(matches!(
            run_encounter(
                &profile,
                &factions,
                &fighter,
                &peaceful,
                CombatModifiers::default(),
                CombatModifiers::default(),
                &mut rng
            ),
            Err(CombatError::NotHostile { .. })
        ));
    }

    #[test]
    fn damage_reduction_and_immunity_are_applied() {
        let profile = NwnRulesProfile::vanilla_subset();
        let (fighter, mut target) = fighter_and_guardian();
        target.template.ac = crate::character::ArmorClass::unarmored();
        target.template.damage_reduction = 100;
        let mut rng = DeterministicRng::new(2);
        for _ in 0..20 {
            let outcome = resolve_attack(
                &profile,
                &fighter,
                &mut target,
                &mut rng,
                1,
                0,
                CombatModifiers::default(),
                CombatModifiers::default(),
            )
            .expect("attack");
            if outcome.hit {
                assert_eq!(outcome.damage, 0);
                break;
            }
        }
        target.template.damage_reduction = 0;
        target.template.damage_immunities = vec![DamageType::Slashing];
        let mut rng = DeterministicRng::new(3);
        for _ in 0..20 {
            let outcome = resolve_attack(
                &profile,
                &fighter,
                &mut target,
                &mut rng,
                1,
                0,
                CombatModifiers::default(),
                CombatModifiers::default(),
            )
            .expect("attack");
            if outcome.hit {
                assert_eq!(outcome.damage, 0);
                break;
            }
        }
    }

    #[test]
    fn saving_throw_compares_the_d20_result_to_the_dc() {
        let mut profile = NwnRulesProfile::vanilla_subset();
        profile.natural_twenty_hits = false;
        profile.natural_one_misses = false;
        let (fighter, _) = fighter_and_guardian();
        let mut target = fighter;
        let mut rng = DeterministicRng::new(11);
        assert!(
            saving_throw(&profile, &mut target, SaveKind::Fortitude, 0, &mut rng).expect("save")
        );
        assert!(
            !saving_throw(&profile, &mut target, SaveKind::Fortitude, 100, &mut rng).expect("save")
        );
    }

    #[test]
    fn special_attacks_apply_bounded_modifiers_and_saves() {
        let mut profile = NwnRulesProfile::vanilla_subset();
        profile.natural_one_misses = false;
        let (mut fighter, mut target) = fighter_and_guardian();
        fighter
            .template
            .weapon
            .as_mut()
            .expect("weapon")
            .attack_bonus = 50;
        target.template.ac = crate::character::ArmorClass::unarmored();
        target.template.hit_points = 100;
        target.template.saves.fortitude = 0;

        let mut base_rng = DeterministicRng::new(7);
        let base = resolve_attack(
            &profile,
            &fighter,
            &mut target.clone(),
            &mut base_rng,
            1,
            0,
            CombatModifiers::default(),
            CombatModifiers::default(),
        )
        .expect("base");
        let mut power_rng = DeterministicRng::new(7);
        let power = resolve_special_attack(
            &profile,
            &fighter,
            &mut target,
            &mut power_rng,
            1,
            0,
            SpecialAttack::PowerAttack,
            CombatModifiers::default(),
            CombatModifiers::default(),
        )
        .expect("power attack");
        assert!(base.hit && power.attack.hit);
        assert_eq!(base.natural, power.attack.natural);
        assert_eq!(power.attack.damage, base.damage + 5);
        assert_eq!(power.save, None);
        assert!(power.attack.note.starts_with("power-attack:"));

        let mut knockdown_rng = DeterministicRng::new(9);
        let knockdown = resolve_special_attack(
            &profile,
            &fighter,
            &mut target,
            &mut knockdown_rng,
            1,
            0,
            SpecialAttack::Knockdown,
            CombatModifiers::default(),
            CombatModifiers::default(),
        )
        .expect("knockdown");
        assert!(knockdown.attack.hit);
        let save = knockdown.save.expect("fortitude save");
        assert_eq!(save.kind, SaveKind::Fortitude);
        assert_eq!(save.dc, 15);
    }

    #[test]
    fn perception_gate_prevents_attacks_when_the_target_is_not_perceived() {
        let mut profile = NwnRulesProfile::vanilla_subset();
        profile.max_rounds = 3;
        let factions = FactionTable::default();
        let (fighter, guardian) = fighter_and_guardian();
        let mut rng = DeterministicRng::new(21);
        let outcome = run_encounter_with_perception(
            &profile,
            &factions,
            &fighter,
            &guardian,
            CombatModifiers::default(),
            CombatModifiers::default(),
            &mut rng,
            Some(crate::ai::Perception::new(0.0)),
        )
        .expect("encounter");
        assert!(outcome.report.actions.is_empty());
        assert_eq!(outcome.report.winner, None);
        assert_eq!(outcome.report.rounds, 3);
    }
}
