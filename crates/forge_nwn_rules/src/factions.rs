//! Faction reputations and deterministic hostility decisions.

use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};
use std::fmt;

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Faction {
    pub id: u16,
    pub name: String,
    pub parent_id: u16,
    pub global: bool,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Reputation {
    pub source_id: u16,
    pub target_id: u16,
    pub value: i32,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct FactionTable {
    pub schema_version: String,
    pub factions: Vec<Faction>,
    pub reputations: Vec<Reputation>,
    #[serde(skip)]
    hostile_reputation_max: i32,
    #[serde(skip)]
    reputation_index: BTreeMap<(u16, u16), i32>,
}

impl FactionTable {
    pub fn new(
        factions: Vec<Faction>,
        reputations: Vec<Reputation>,
        hostile_reputation_max: i32,
    ) -> Result<Self, FactionError> {
        if factions.is_empty() {
            return Err(FactionError::Empty);
        }
        let mut ids = BTreeSet::new();
        for faction in &factions {
            if !ids.insert(faction.id) {
                return Err(FactionError::Duplicate(faction.id));
            }
            if faction.name.trim().is_empty() {
                return Err(FactionError::EmptyName(faction.id));
            }
        }
        let mut index = BTreeMap::new();
        for reputation in &reputations {
            if !ids.contains(&reputation.source_id) || !ids.contains(&reputation.target_id) {
                return Err(FactionError::UnknownFaction {
                    source: reputation.source_id,
                    target: reputation.target_id,
                });
            }
            if !(0..=100).contains(&reputation.value) {
                return Err(FactionError::InvalidReputation(reputation.value));
            }
            if index
                .insert(
                    (reputation.source_id, reputation.target_id),
                    reputation.value,
                )
                .is_some()
            {
                return Err(FactionError::DuplicateReputation {
                    source: reputation.source_id,
                    target: reputation.target_id,
                });
            }
        }
        Ok(Self {
            schema_version: "module-factions@1".to_owned(),
            factions,
            reputations,
            hostile_reputation_max,
            reputation_index: index,
        })
    }

    #[must_use]
    pub fn reputation(&self, source: u16, target: u16) -> i32 {
        self.reputation_index
            .get(&(source, target))
            .copied()
            .unwrap_or(50)
    }

    #[must_use]
    pub fn is_hostile(&self, source: u16, target: u16) -> bool {
        self.reputation(source, target) <= self.hostile_reputation_max
    }

    #[must_use]
    pub fn faction(&self, id: u16) -> Option<&Faction> {
        self.factions.iter().find(|faction| faction.id == id)
    }

    pub fn from_json(text: &str, hostile_reputation_max: i32) -> Result<Self, FactionError> {
        let raw: Self =
            serde_json::from_str(text).map_err(|error| FactionError::Json(error.to_string()))?;
        Self::new(raw.factions, raw.reputations, hostile_reputation_max)
    }

    pub fn validate(&self) -> Result<(), FactionError> {
        let rebuilt = Self::new(
            self.factions.clone(),
            self.reputations.clone(),
            self.hostile_reputation_max,
        )?;
        if rebuilt.factions != self.factions || rebuilt.reputations != self.reputations {
            return Err(FactionError::InconsistentIndex);
        }
        Ok(())
    }
}

impl Default for FactionTable {
    fn default() -> Self {
        Self {
            schema_version: "module-factions@1".to_owned(),
            factions: vec![
                Faction {
                    id: 0,
                    name: "PC".to_owned(),
                    parent_id: 0,
                    global: false,
                },
                Faction {
                    id: 1,
                    name: "AB_HOSTILE".to_owned(),
                    parent_id: 0,
                    global: false,
                },
                Faction {
                    id: 2,
                    name: "AB_NEUTRAL".to_owned(),
                    parent_id: 0,
                    global: false,
                },
            ],
            reputations: vec![
                Reputation {
                    source_id: 0,
                    target_id: 1,
                    value: 0,
                },
                Reputation {
                    source_id: 0,
                    target_id: 2,
                    value: 100,
                },
                Reputation {
                    source_id: 1,
                    target_id: 0,
                    value: 0,
                },
                Reputation {
                    source_id: 2,
                    target_id: 0,
                    value: 100,
                },
            ],
            hostile_reputation_max: 0,
            reputation_index: [((0, 1), 0), ((0, 2), 100), ((1, 0), 0), ((2, 0), 100)]
                .into_iter()
                .collect(),
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum FactionError {
    Empty,
    EmptyName(u16),
    Duplicate(u16),
    UnknownFaction { source: u16, target: u16 },
    InvalidReputation(i32),
    DuplicateReputation { source: u16, target: u16 },
    InconsistentIndex,
    Json(String),
}

impl fmt::Display for FactionError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Empty => formatter.write_str("faction table must not be empty"),
            Self::EmptyName(id) => write!(formatter, "faction {id} has an empty name"),
            Self::Duplicate(id) => write!(formatter, "duplicate faction {id}"),
            Self::UnknownFaction { source, target } => {
                write!(
                    formatter,
                    "reputation references unknown faction {source}->{target}"
                )
            }
            Self::InvalidReputation(value) => write!(formatter, "invalid reputation {value}"),
            Self::DuplicateReputation { source, target } => {
                write!(formatter, "duplicate reputation {source}->{target}")
            }
            Self::InconsistentIndex => {
                formatter.write_str("faction reputation index is inconsistent")
            }
            Self::Json(detail) => write!(formatter, "invalid faction JSON: {detail}"),
        }
    }
}

impl std::error::Error for FactionError {}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn fixture_reputations_mark_the_guardian_hostile() {
        let table = FactionTable::default();
        assert!(table.is_hostile(0, 1));
        assert!(!table.is_hostile(0, 2));
        assert_eq!(table.reputation(0, 2), 100);
        assert_eq!(table.validate(), Ok(()));
    }

    #[test]
    fn duplicate_factions_and_reputations_are_refused() {
        let faction = Faction {
            id: 0,
            name: "PC".to_owned(),
            parent_id: 0,
            global: false,
        };
        assert_eq!(
            FactionTable::new(vec![faction.clone(), faction.clone()], vec![], 0),
            Err(FactionError::Duplicate(0))
        );
        let reputation = Reputation {
            source_id: 0,
            target_id: 0,
            value: 0,
        };
        assert!(matches!(
            FactionTable::new(vec![faction], vec![reputation.clone(), reputation], 0),
            Err(FactionError::DuplicateReputation { .. })
        ));
    }
}
