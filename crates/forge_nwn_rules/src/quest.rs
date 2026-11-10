//! Quest state shared by the M3 journal migration and the M4 save.

use serde::{Deserialize, Serialize};
use std::collections::BTreeSet;
use std::fmt;

pub const QUEST_SCHEMA: &str = "forge.nwn-quest@1";

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub enum QuestStatus {
    NotStarted,
    Available,
    Active,
    Completed,
    Failed,
}

impl QuestStatus {
    #[must_use]
    pub const fn label(self) -> &'static str {
        match self {
            Self::NotStarted => "not_started",
            Self::Available => "available",
            Self::Active => "active",
            Self::Completed => "completed",
            Self::Failed => "failed",
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct QuestState {
    pub schema_version: String,
    pub quest_id: String,
    pub status: QuestStatus,
    pub entries: BTreeSet<u32>,
    pub rewards_granted: BTreeSet<String>,
}

impl QuestState {
    #[must_use]
    pub fn new(quest_id: impl Into<String>) -> Self {
        Self {
            schema_version: QUEST_SCHEMA.to_owned(),
            quest_id: quest_id.into(),
            status: QuestStatus::NotStarted,
            entries: BTreeSet::new(),
            rewards_granted: BTreeSet::new(),
        }
    }

    /// Build the M4 quest view from M3 journal entries and quest status.
    #[must_use]
    pub fn from_m3(
        quest_id: impl Into<String>,
        entries: impl IntoIterator<Item = u32>,
        quest_status: &str,
    ) -> Self {
        let entries = entries.into_iter().collect::<BTreeSet<_>>();
        let status = match quest_status {
            "completed" => QuestStatus::Completed,
            "active" => QuestStatus::Active,
            "available" => QuestStatus::Available,
            "failed" => QuestStatus::Failed,
            _ => {
                if entries.is_empty() {
                    QuestStatus::NotStarted
                } else if entries.contains(&30) {
                    QuestStatus::Completed
                } else {
                    QuestStatus::Active
                }
            }
        };
        Self {
            schema_version: QUEST_SCHEMA.to_owned(),
            quest_id: quest_id.into(),
            status,
            entries,
            rewards_granted: BTreeSet::new(),
        }
    }

    pub fn add_entry(&mut self, entry: u32) {
        self.entries.insert(entry);
        if self.status == QuestStatus::NotStarted {
            self.status = QuestStatus::Available;
        }
    }

    pub fn activate(&mut self) {
        if matches!(
            self.status,
            QuestStatus::NotStarted | QuestStatus::Available
        ) {
            self.status = QuestStatus::Active;
        }
    }

    pub fn complete(&mut self) {
        self.status = QuestStatus::Completed;
    }

    pub fn fail(&mut self) {
        self.status = QuestStatus::Failed;
    }

    /// Return true only the first time a named reward is granted.
    pub fn grant_reward_once(&mut self, reward_id: impl Into<String>) -> bool {
        self.rewards_granted.insert(reward_id.into())
    }

    #[must_use]
    pub fn has_entry(&self, entry: u32) -> bool {
        self.entries.contains(&entry)
    }

    pub fn validate(&self) -> Result<(), QuestError> {
        if self.schema_version != QUEST_SCHEMA {
            return Err(QuestError::Schema(self.schema_version.clone()));
        }
        if self.quest_id.trim().is_empty() {
            return Err(QuestError::EmptyId);
        }
        if self.status == QuestStatus::Completed && self.entries.is_empty() {
            return Err(QuestError::CompletedWithoutEntries);
        }
        Ok(())
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum QuestError {
    Schema(String),
    EmptyId,
    CompletedWithoutEntries,
}

impl fmt::Display for QuestError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Schema(schema) => write!(formatter, "unsupported quest schema {schema}"),
            Self::EmptyId => formatter.write_str("quest id must not be empty"),
            Self::CompletedWithoutEntries => {
                formatter.write_str("completed quest must have at least one entry")
            }
        }
    }
}

impl std::error::Error for QuestError {}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn m3_journal_migrates_to_a_completed_quest() {
        let quest = QuestState::from_m3("ab_quest", [10, 20, 30], "completed");
        assert_eq!(quest.status, QuestStatus::Completed);
        assert!(quest.has_entry(30));
        assert_eq!(quest.validate(), Ok(()));
    }

    #[test]
    fn rewards_are_granted_once() {
        let mut quest = QuestState::new("ab_quest");
        assert!(quest.grant_reward_once("gold-50"));
        assert!(!quest.grant_reward_once("gold-50"));
    }

    #[test]
    fn failure_is_persistent() {
        let mut quest = QuestState::new("ab_quest");
        quest.add_entry(10);
        quest.fail();
        assert_eq!(quest.status, QuestStatus::Failed);
    }
}
