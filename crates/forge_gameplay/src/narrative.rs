//! Bounded authored narrative data. Runtime values belong to `GameState` only.
use crate::{Condition, GameplayDocument, GameplayError};
use forge_core::EntityId;
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};

pub const MAX_NARRATIVE_VARIABLES: usize = 128;

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(
    tag = "type",
    content = "value",
    rename_all = "snake_case",
    deny_unknown_fields
)]
pub enum NarrativeValue {
    Boolean(bool),
    Integer(i32),
    Text(String),
}

impl NarrativeValue {
    #[must_use]
    pub fn same_type(&self, other: &Self) -> bool {
        std::mem::discriminant(self) == std::mem::discriminant(other)
    }

    pub fn validate(&self) -> Result<(), GameplayError> {
        if matches!(self, Self::Text(value) if value.len() > 256) {
            return Err(invalid("variable text exceeds 256 bytes"));
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum NarrativeComparison {
    Equal,
    NotEqual,
    AtLeast,
    AtMost,
}

impl NarrativeComparison {
    #[must_use]
    pub fn evaluate(self, actual: &NarrativeValue, expected: &NarrativeValue) -> bool {
        if !actual.same_type(expected) {
            return false;
        }
        match self {
            Self::Equal => actual == expected,
            Self::NotEqual => actual != expected,
            Self::AtLeast | Self::AtMost => {
                let (NarrativeValue::Integer(actual), NarrativeValue::Integer(expected)) =
                    (actual, expected)
                else {
                    return false;
                };
                if self == Self::AtLeast {
                    actual >= expected
                } else {
                    actual <= expected
                }
            }
        }
    }
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct NarrativeVariable {
    pub id: EntityId,
    pub name: String,
    pub initial: NarrativeValue,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct QuestConsumption {
    pub item: EntityId,
    pub quantity: u32,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct QuestSolution {
    pub id: EntityId,
    pub name: String,
    pub condition: Condition,
    pub consume: Vec<QuestConsumption>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct QuestFlow {
    pub quest: EntityId,
    pub prerequisites: Vec<EntityId>,
    pub solutions: Vec<QuestSolution>,
    pub failure_condition: Option<Condition>,
    pub journal_available: String,
    pub journal_active: String,
    pub journal_completed: String,
    pub journal_failed: String,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct NarrativeDefinition {
    pub version: u32,
    pub variables: Vec<NarrativeVariable>,
    pub quests: Vec<QuestFlow>,
}

#[derive(Clone, Debug, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct NarrativeState {
    pub variables: BTreeMap<EntityId, NarrativeValue>,
    pub quest_solutions: BTreeMap<EntityId, EntityId>,
}

fn invalid(message: impl Into<String>) -> GameplayError {
    GameplayError::InvalidDocument(format!("narrative: {}", message.into()))
}

fn text(value: &str) -> Result<(), GameplayError> {
    if value.trim().is_empty() || value.len() > 2048 {
        return Err(invalid(
            "journal/name must be nonempty and at most 2048 bytes",
        ));
    }
    Ok(())
}

impl NarrativeDefinition {
    #[must_use]
    pub fn variable(&self, id: EntityId) -> Option<&NarrativeVariable> {
        self.variables.iter().find(|entry| entry.id == id)
    }

    #[must_use]
    pub fn quest(&self, id: EntityId) -> Option<&QuestFlow> {
        self.quests.iter().find(|entry| entry.quest == id)
    }

    #[must_use]
    pub fn initial_state(&self) -> NarrativeState {
        NarrativeState {
            variables: self
                .variables
                .iter()
                .map(|entry| (entry.id, entry.initial.clone()))
                .collect(),
            quest_solutions: BTreeMap::new(),
        }
    }

    pub(crate) fn validate(
        &self,
        document: &GameplayDocument,
        identities: &mut BTreeSet<EntityId>,
    ) -> Result<(), GameplayError> {
        if self.version != 1
            || self.variables.len() > MAX_NARRATIVE_VARIABLES
            || self.quests.len() > MAX_NARRATIVE_VARIABLES
        {
            return Err(invalid("unsupported version or definition quota exceeded"));
        }
        for variable in &self.variables {
            if !identities.insert(variable.id) {
                return Err(invalid(format!(
                    "duplicate variable identity {}",
                    variable.id
                )));
            }
            text(&variable.name)?;
            variable.initial.validate()?;
        }
        let mut quest_ids = BTreeSet::new();
        for flow in &self.quests {
            if !quest_ids.insert(flow.quest) || document.quest(flow.quest).is_none() {
                return Err(invalid(format!("missing or repeated quest {}", flow.quest)));
            }
            Self::validate_flow(document, flow, identities)?;
        }
        self.validate_dependencies()
    }

    fn validate_flow(
        document: &GameplayDocument,
        flow: &QuestFlow,
        identities: &mut BTreeSet<EntityId>,
    ) -> Result<(), GameplayError> {
        if flow.solutions.is_empty() || flow.solutions.len() > 16 || flow.prerequisites.len() > 16 {
            return Err(invalid("quest solution/dependency quota exceeded"));
        }
        for value in [
            &flow.journal_available,
            &flow.journal_active,
            &flow.journal_completed,
            &flow.journal_failed,
        ] {
            text(value)?;
        }
        let mut dependencies = BTreeSet::new();
        for dependency in &flow.prerequisites {
            if *dependency == flow.quest
                || !dependencies.insert(*dependency)
                || document.quest(*dependency).is_none()
            {
                return Err(invalid(format!("invalid quest dependency {dependency}")));
            }
        }
        for solution in &flow.solutions {
            if !identities.insert(solution.id) {
                return Err(invalid(format!(
                    "duplicate solution identity {}",
                    solution.id
                )));
            }
            text(&solution.name)?;
            document.validate_condition(&solution.condition)?;
            if solution.consume.len() > 16 {
                return Err(invalid("too many item costs for a quest solution"));
            }
            let mut items = BTreeSet::new();
            for cost in &solution.consume {
                if !items.insert(cost.item)
                    || !document
                        .item(cost.item)
                        .is_some_and(|item| cost.quantity > 0 && cost.quantity <= item.max_stack)
                {
                    return Err(invalid(format!("invalid solution item cost {}", cost.item)));
                }
            }
        }
        if let Some(condition) = &flow.failure_condition {
            document.validate_condition(condition)?;
        }
        Ok(())
    }

    fn validate_dependencies(&self) -> Result<(), GameplayError> {
        for flow in &self.quests {
            let mut visited = BTreeSet::new();
            let mut pending = flow.prerequisites.clone();
            while let Some(id) = pending.pop() {
                if id == flow.quest {
                    return Err(invalid(format!("cyclic quest dependency {id}")));
                }
                if visited.insert(id)
                    && let Some(dependency) = self.quest(id)
                {
                    pending.extend(&dependency.prerequisites);
                }
            }
        }
        Ok(())
    }
}
