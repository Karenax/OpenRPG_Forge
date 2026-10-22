//! Shared rule descriptors for authoring, help and runtime document validation.
use crate::{GameplayError, RulesProfile};
use serde::{Deserialize, Serialize};

/// Stable identifiers, independent of translated labels or panel order.
#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum RuleKey {
    InteractionRange,
    AttackRange,
    AttackDamage,
    AttackStaminaCost,
    MaxStamina,
    MaxActionReceipts,
    MaxEventsPerTick,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum RuleUnit {
    Metres,
    VitalityPoints,
    StaminaPoints,
    Receipts,
    EventsPerTick,
}

impl RuleUnit {
    #[must_use]
    pub const fn label(self) -> &'static str {
        match self {
            Self::Metres => "m",
            Self::VitalityPoints => "points de vitalité",
            Self::StaminaPoints => "points d'endurance",
            Self::Receipts => "reçus",
            Self::EventsPerTick => "événements par tick",
        }
    }
}

#[derive(Clone, Debug, PartialEq)]
pub struct RuleField {
    pub key: RuleKey,
    pub label: &'static str,
    pub unit: RuleUnit,
    pub value: String,
    pub minimum: f64,
    pub maximum: f64,
    pub integer: bool,
    pub help: &'static str,
}

impl RulesProfile {
    /// This contract describes the implemented minimal rules, not the full manual.
    pub const CONTRACT: &str = "forge.rules.minimal.v1";

    #[must_use]
    pub fn rule_fields(&self) -> [RuleField; 7] {
        [
            RuleField {
                key: RuleKey::InteractionRange,
                label: "Portée d'interaction",
                unit: RuleUnit::Metres,
                value: self.interaction_range_m.to_string(),
                minimum: 0.1,
                maximum: 20.0,
                integer: false,
                help: "Distance maximale ; la ligne de vue et les conditions restent exigées.",
            },
            RuleField {
                key: RuleKey::AttackRange,
                label: "Portée d'attaque",
                unit: RuleUnit::Metres,
                value: self.attack_range_m.to_string(),
                minimum: 0.1,
                maximum: 20.0,
                integer: false,
                help: "Le runtime exige aussi un contact physique confirmé et la ligne de vue.",
            },
            RuleField {
                key: RuleKey::AttackDamage,
                label: "Dégâts d'attaque",
                unit: RuleUnit::VitalityPoints,
                value: self.attack_damage.to_string(),
                minimum: 1.0,
                maximum: 1_000_000.0,
                integer: true,
                help: "Dégâts avant protection de la cible ; aucun jet ne remplace le contact.",
            },
            RuleField {
                key: RuleKey::AttackStaminaCost,
                label: "Coût d'attaque",
                unit: RuleUnit::StaminaPoints,
                value: self.attack_stamina_cost.to_string(),
                minimum: 0.0,
                maximum: f64::from(self.max_stamina),
                integer: true,
                help: "Coût consommé seulement lors d'une attaque acceptée, borné par l'endurance maximale.",
            },
            RuleField {
                key: RuleKey::MaxStamina,
                label: "Endurance maximale",
                unit: RuleUnit::StaminaPoints,
                value: self.max_stamina.to_string(),
                minimum: 1.0,
                maximum: 1_000_000.0,
                integer: true,
                help: "Valeur initiale et plafond de l'endurance ; modifier cette règle change la compatibilité des sauvegardes.",
            },
            RuleField {
                key: RuleKey::MaxActionReceipts,
                label: "Historique des actions",
                unit: RuleUnit::Receipts,
                value: self.max_action_receipts.to_string(),
                minimum: 1.0,
                maximum: 4096.0,
                integer: true,
                help: "Quota d'idempotence et de contacts ; une commande répétée ne répète pas sa récompense.",
            },
            RuleField {
                key: RuleKey::MaxEventsPerTick,
                label: "Budget des événements",
                unit: RuleUnit::EventsPerTick,
                value: self.max_events_per_tick.to_string(),
                minimum: 1.0,
                maximum: 4096.0,
                integer: true,
                help: "Limite des événements et observations d'une étape de simulation.",
            },
        ]
    }

    pub fn validate(&self) -> Result<(), GameplayError> {
        if self.id.trim().is_empty() || self.id.len() > 128 || self.version == 0 {
            return Err(GameplayError::InvalidDocument(
                "profile.id/version: nonempty identity and positive version required".into(),
            ));
        }
        for field in self.rule_fields() {
            let value: f64 = field.value.parse().map_err(|_| {
                GameplayError::InvalidDocument(format!("profile.{:?}: invalid number", field.key))
            })?;
            // f32 stores 0.1 slightly above the decimal bound; comparisons use f64.
            if !value.is_finite() || value < field.minimum || value > field.maximum {
                return Err(GameplayError::InvalidDocument(format!(
                    "profile.{:?}: {} must be in {}..={} {}",
                    field.key,
                    field.value,
                    field.minimum,
                    field.maximum,
                    field.unit.label()
                )));
            }
        }
        Ok(())
    }

    /// Parse into a candidate and validate the complete profile before returning it.
    pub fn with_rule_text(&self, key: RuleKey, value: &str) -> Result<Self, GameplayError> {
        let mut next = self.clone();
        macro_rules! parse {
            () => {
                value.trim().parse().map_err(|_| {
                    GameplayError::InvalidDocument(format!(
                        "profile.{key:?}: invalid numeric value"
                    ))
                })?
            };
        }
        match key {
            RuleKey::InteractionRange => next.interaction_range_m = parse!(),
            RuleKey::AttackRange => next.attack_range_m = parse!(),
            RuleKey::AttackDamage => next.attack_damage = parse!(),
            RuleKey::AttackStaminaCost => next.attack_stamina_cost = parse!(),
            RuleKey::MaxStamina => next.max_stamina = parse!(),
            RuleKey::MaxActionReceipts => next.max_action_receipts = parse!(),
            RuleKey::MaxEventsPerTick => next.max_events_per_tick = parse!(),
        }
        next.validate()?;
        Ok(next)
    }
}
