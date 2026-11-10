//! Typed effects emitted by NCS natives and applied by the runtime state.
//!
//! The VM never writes gameplay state directly. Every mutation is expressed as a
//! `RuntimeEffect`, validated, then applied with a sequenced receipt. This is the
//! adapter boundary for the M3 slice: there is no Bevy ECS access and no direct
//! document write from a script.

use forge_nwn_rules::QuestStatus;
use serde::{Deserialize, Serialize};
use std::collections::BTreeSet;
use std::fmt;

/// Permission gate applied before any effect mutates gameplay state.
///
/// The default is permissive for the local compatibility runtime. A host can
/// deny a whole class of effects (for example during a read-only replay) or a
/// specific effect kind; the refused batch leaves the state untouched and the
/// diagnostics name the refused effect.
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct EffectPolicy {
    pub allow_effects: bool,
    pub denied_kinds: BTreeSet<String>,
}

impl Default for EffectPolicy {
    fn default() -> Self {
        Self {
            allow_effects: true,
            denied_kinds: BTreeSet::new(),
        }
    }
}

impl EffectPolicy {
    #[must_use]
    pub fn permissive() -> Self {
        Self::default()
    }

    pub fn deny(&mut self, kind: impl Into<String>) {
        self.denied_kinds.insert(kind.into());
    }

    #[must_use]
    pub fn allows(&self, effect: &RuntimeEffect) -> bool {
        self.allow_effects && !self.denied_kinds.contains(effect.kind())
    }
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum RuntimeEffect {
    SetLocalInt {
        object: u32,
        name: String,
        value: i32,
    },
    AddJournalEntry {
        quest: String,
        entry: u32,
        target: u32,
        all_party: bool,
        all_players: bool,
    },
    SetQuestStatus {
        quest: String,
        status: QuestStatus,
    },
    SetDoorState {
        door: String,
        open: bool,
    },
    SendMessage {
        target: u32,
        text: String,
    },
    GiveGold {
        target: u32,
        amount: i32,
    },
    AddInventory {
        item: String,
    },
    MarkDead {
        object: u32,
    },
    StartConversation {
        target: u32,
        dialogue: String,
        private: bool,
        play_hello: bool,
    },
    EndConversation,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum EffectError {
    EmptyName,
    EmptyQuest,
    EmptyItem,
    EmptyDialogue,
    EmptyDoor,
    Denied(String),
    SequenceExhausted,
}

impl fmt::Display for EffectError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::EmptyName => formatter.write_str("effect name must not be empty"),
            Self::EmptyQuest => formatter.write_str("journal quest must not be empty"),
            Self::EmptyItem => formatter.write_str("inventory item must not be empty"),
            Self::EmptyDialogue => formatter.write_str("dialogue resref must not be empty"),
            Self::EmptyDoor => formatter.write_str("door identifier must not be empty"),
            Self::Denied(kind) => write!(formatter, "effect {kind} is denied by the host policy"),
            Self::SequenceExhausted => formatter.write_str("effect receipt sequence exhausted"),
        }
    }
}

impl std::error::Error for EffectError {}

impl RuntimeEffect {
    #[must_use]
    pub const fn kind(&self) -> &'static str {
        match self {
            Self::SetLocalInt { .. } => "setLocalInt",
            Self::AddJournalEntry { .. } => "addJournalEntry",
            Self::SetQuestStatus { .. } => "setQuestStatus",
            Self::SetDoorState { .. } => "setDoorState",
            Self::SendMessage { .. } => "sendMessage",
            Self::GiveGold { .. } => "giveGold",
            Self::AddInventory { .. } => "addInventory",
            Self::MarkDead { .. } => "markDead",
            Self::StartConversation { .. } => "startConversation",
            Self::EndConversation => "endConversation",
        }
    }

    pub fn validate(&self) -> Result<(), EffectError> {
        match self {
            Self::SetLocalInt { name, .. } if name.is_empty() => Err(EffectError::EmptyName),
            Self::AddJournalEntry { quest, .. } | Self::SetQuestStatus { quest, .. }
                if quest.is_empty() =>
            {
                Err(EffectError::EmptyQuest)
            }
            Self::SetDoorState { door, .. } if door.is_empty() => Err(EffectError::EmptyDoor),
            Self::AddInventory { item } if item.is_empty() => Err(EffectError::EmptyItem),
            Self::StartConversation { dialogue, .. } if dialogue.is_empty() => {
                Err(EffectError::EmptyDialogue)
            }
            _ => Ok(()),
        }
    }

    #[must_use]
    pub fn summary(&self) -> String {
        match self {
            Self::SetLocalInt {
                object,
                name,
                value,
            } => {
                format!("SetLocalInt({object}, {name}, {value})")
            }
            Self::AddJournalEntry { quest, entry, .. } => {
                format!("AddJournalEntry({quest}, {entry})")
            }
            Self::SetQuestStatus { quest, status } => {
                format!("SetQuestStatus({quest}, {})", status.label())
            }
            Self::SetDoorState { door, open } => format!("SetDoorState({door}, {open})"),
            Self::SendMessage { target, text } => format!("SendMessage({target}, {text})"),
            Self::GiveGold { target, amount } => format!("GiveGold({target}, {amount})"),
            Self::AddInventory { item } => format!("AddInventory({item})"),
            Self::MarkDead { object } => format!("MarkDead({object})"),
            Self::StartConversation {
                target, dialogue, ..
            } => {
                format!("StartConversation({target}, {dialogue})")
            }
            Self::EndConversation => "EndConversation".to_owned(),
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct AppliedEffect {
    pub sequence: u64,
    pub script: String,
    #[serde(default)]
    pub receipt: String,
    pub effect: RuntimeEffect,
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn empty_names_are_refused_before_application() {
        let effect = RuntimeEffect::SetLocalInt {
            object: 1,
            name: String::new(),
            value: 1,
        };
        assert_eq!(effect.validate(), Err(EffectError::EmptyName));
        let effect = RuntimeEffect::AddJournalEntry {
            quest: String::new(),
            entry: 10,
            target: 2,
            all_party: true,
            all_players: false,
        };
        assert_eq!(effect.validate(), Err(EffectError::EmptyQuest));
    }

    #[test]
    fn policy_denies_an_effect_kind_before_application() {
        let effect = RuntimeEffect::GiveGold {
            target: 1,
            amount: 50,
        };
        let mut policy = EffectPolicy::permissive();
        assert!(policy.allows(&effect));
        policy.deny("giveGold");
        assert!(!policy.allows(&effect));
        policy.allow_effects = false;
        assert!(!policy.allows(&RuntimeEffect::EndConversation));
    }

    #[test]
    fn quest_and_door_effects_validate_their_identifiers() {
        assert_eq!(
            RuntimeEffect::SetQuestStatus {
                quest: String::new(),
                status: QuestStatus::Failed,
            }
            .validate(),
            Err(EffectError::EmptyQuest)
        );
        assert_eq!(
            RuntimeEffect::SetDoorState {
                door: String::new(),
                open: true,
            }
            .validate(),
            Err(EffectError::EmptyDoor)
        );
    }
}
