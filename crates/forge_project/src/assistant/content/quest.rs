//! A concise authoring command, expanded into ordinary editable definitions.
//! No reference-adventure names, runtime rules or module settings are embedded.
use super::{ContentAddition, valid_key};
use forge_core::{CommandId, EntityId, Vec3};
use forge_gameplay::{
    ActorDefinition, ActorRole, Condition, DialogueChoice, DialogueDefinition, DialogueEffect,
    DialogueNode, ItemDefinition, NarrativeComparison, NarrativeDefinition, NarrativeValue,
    NarrativeVariable, ObjectDefinition, ObjectKind, ObjectPhysics, QuestConsumption,
    QuestDefinition, QuestFlow, QuestSolution, QuestStatus,
};
use serde::{Deserialize, Serialize};
use std::collections::BTreeSet;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct DialogueActorDraft {
    pub name: String,
    pub position: Vec3,
    pub greeting: String,
}
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct CollectionDraft {
    pub name: String,
    pub choice_text: String,
    pub item_name: String,
    pub pickup_name: String,
    pub position: Vec3,
    pub quantity: u32,
}
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "approach", rename_all = "snake_case", deny_unknown_fields)]
pub enum QuestAlternative {
    Testimony {
        key: String,
        name: String,
        choice_text: String,
        witness: DialogueActorDraft,
        learn_text: String,
    },
    Defeat {
        key: String,
        name: String,
        choice_text: String,
        enemy_name: String,
        position: Vec3,
        vitality: u32,
        protection: u32,
    },
}
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct DoorConsequence {
    pub name: String,
    pub position: Vec3,
    pub initially_open: bool,
    pub open_after_completion: bool,
}
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct QuestJournalDraft {
    pub available: String,
    pub active: String,
    pub completed: String,
    pub failed: String,
}
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct FetchQuestDraft {
    pub key: String,
    pub name: String,
    pub giver: DialogueActorDraft,
    pub accept_text: String,
    pub collection: CollectionDraft,
    pub alternatives: Vec<QuestAlternative>,
    pub reward_name: String,
    pub reward_quantity: u32,
    pub journal: QuestJournalDraft,
    pub door: Option<DoorConsequence>,
}

fn actor(id: EntityId, draft: &DialogueActorDraft, dialogue: EntityId) -> ActorDefinition {
    ActorDefinition {
        id,
        name: draft.name.clone(),
        role: ActorRole::Friendly,
        position: draft.position,
        max_vitality: 100,
        protection: 0,
        inventory_capacity: 24,
        dialogue: Some(dialogue),
        patrol: vec![],
        flees_danger: false,
    }
}
fn dialogue(
    id: EntityId,
    node: EntityId,
    text: &str,
    choices: Vec<DialogueChoice>,
) -> DialogueDefinition {
    DialogueDefinition {
        id,
        start: node,
        nodes: vec![DialogueNode {
            id: node,
            text: text.into(),
            choices,
        }],
    }
}

impl FetchQuestDraft {
    /// Builds data only. The caller still compiles it through `ContentAddition`'s
    /// scope, surface, region, quota, reference and canonical transaction checks.
    pub fn expand(&self, command: CommandId) -> Result<ContentAddition, String> {
        self.validate_keys()?;
        let identity = |part: &str| {
            EntityId::deterministic(format!(
                "forge/assistant/{command}/quest/{}/{part}",
                self.key
            ))
        };
        let (mut content, mut flow) = self.initial_content(command);
        let quest = flow.quest;
        let mut variables = Vec::new();
        let mut texts = vec![self.collection.choice_text.clone()];
        for alternative in &self.alternatives {
            self.append_alternative(
                alternative,
                command,
                &mut content,
                &mut flow,
                &mut variables,
                &mut texts,
            );
        }
        if let Some(door) = &self.door {
            content.objects.push(ObjectDefinition {
                id: identity("door"),
                name: door.name.clone(),
                position: door.position,
                kind: ObjectKind::Door {
                    initially_open: door.initially_open,
                },
                physics: ObjectPhysics::default(),
            });
        }
        let mut choices = vec![DialogueChoice {
            id: identity("choice/accept"),
            text: self.accept_text.clone(),
            condition: Condition::Quest {
                quest,
                status: QuestStatus::Available,
            },
            effects: vec![DialogueEffect::AcceptQuest { quest }],
            next: None,
        }];
        for (solution, text) in flow.solutions.iter().zip(texts) {
            let mut effects = vec![DialogueEffect::CompleteQuestSolution {
                quest,
                solution: solution.id,
            }];
            if let Some(door) = &self.door {
                effects.push(DialogueEffect::SetDoor {
                    object: identity("door"),
                    open: door.open_after_completion,
                });
            }
            choices.push(DialogueChoice {
                id: EntityId::deterministic(format!(
                    "forge/assistant/{command}/choice/{}",
                    solution.id
                )),
                text,
                condition: Condition::All {
                    conditions: vec![
                        Condition::Quest {
                            quest,
                            status: QuestStatus::Active,
                        },
                        solution.condition.clone(),
                    ],
                },
                effects,
                next: None,
            });
        }
        content.dialogues.push(dialogue(
            identity("dialogue/giver"),
            identity("node/giver"),
            &self.giver.greeting,
            choices,
        ));
        content.narrative = Some(NarrativeDefinition {
            version: 1,
            variables,
            quests: vec![flow],
        });
        Ok(content)
    }

    fn validate_keys(&self) -> Result<(), String> {
        let mut keys = BTreeSet::from(["collection".to_owned()]);
        if !valid_key(&self.key)
            || self.alternatives.len() > 15
            || !(1..=10_000).contains(&self.collection.quantity)
            || !(1..=10_000).contains(&self.reward_quantity)
        {
            return Err("Invalid quest key, solution count or item quantity".into());
        }
        for alternative in &self.alternatives {
            let key = match alternative {
                QuestAlternative::Testimony { key, .. } | QuestAlternative::Defeat { key, .. } => {
                    key
                }
            };
            if !valid_key(key) || !keys.insert(key.clone()) {
                return Err("Duplicate or invalid quest solution key".into());
            }
        }
        Ok(())
    }

    fn initial_content(&self, command: CommandId) -> (ContentAddition, QuestFlow) {
        let identity = |part: &str| {
            EntityId::deterministic(format!(
                "forge/assistant/{command}/quest/{}/{part}",
                self.key
            ))
        };
        let quest = identity("quest");
        let giver = identity("giver");
        let proof = identity("item/proof");
        let reward = identity("item/reward");
        let content = ContentAddition {
            actors: vec![actor(giver, &self.giver, identity("dialogue/giver"))],
            items: vec![
                ItemDefinition {
                    id: proof,
                    name: self.collection.item_name.clone(),
                    max_stack: self.collection.quantity,
                },
                ItemDefinition {
                    id: reward,
                    name: self.reward_name.clone(),
                    max_stack: self.reward_quantity,
                },
            ],
            objects: vec![ObjectDefinition {
                id: identity("pickup"),
                name: self.collection.pickup_name.clone(),
                position: self.collection.position,
                kind: ObjectKind::Pickup {
                    item: proof,
                    quantity: self.collection.quantity,
                },
                physics: ObjectPhysics::default(),
            }],
            places: vec![],
            dialogues: vec![],
            quests: vec![QuestDefinition {
                id: quest,
                name: self.name.clone(),
                giver,
                required_item: proof,
                required_quantity: self.collection.quantity,
                reward_item: Some(reward),
                reward_quantity: self.reward_quantity,
            }],
            narrative: None,
        };
        let flow = QuestFlow {
            quest,
            prerequisites: vec![],
            solutions: vec![QuestSolution {
                id: identity("solution/collection"),
                name: self.collection.name.clone(),
                condition: Condition::HasItem {
                    item: proof,
                    quantity: self.collection.quantity,
                },
                consume: vec![QuestConsumption {
                    item: proof,
                    quantity: self.collection.quantity,
                }],
            }],
            failure_condition: None,
            journal_available: self.journal.available.clone(),
            journal_active: self.journal.active.clone(),
            journal_completed: self.journal.completed.clone(),
            journal_failed: self.journal.failed.clone(),
        };
        (content, flow)
    }

    fn append_alternative(
        &self,
        alternative: &QuestAlternative,
        command: CommandId,
        content: &mut ContentAddition,
        flow: &mut QuestFlow,
        variables: &mut Vec<NarrativeVariable>,
        texts: &mut Vec<String>,
    ) {
        let (key, name, text) = match alternative {
            QuestAlternative::Testimony {
                key,
                name,
                choice_text,
                ..
            }
            | QuestAlternative::Defeat {
                key,
                name,
                choice_text,
                ..
            } => (key, name, choice_text),
        };
        let identity = |part: &str| {
            EntityId::deterministic(format!(
                "forge/assistant/{command}/quest/{}/{key}/{part}",
                self.key
            ))
        };
        let condition = match alternative {
            QuestAlternative::Testimony {
                witness,
                learn_text,
                ..
            } => {
                let variable = identity("variable");
                variables.push(NarrativeVariable {
                    id: variable,
                    name: name.clone(),
                    initial: NarrativeValue::Boolean(false),
                });
                content
                    .actors
                    .push(actor(identity("witness"), witness, identity("dialogue")));
                content.dialogues.push(dialogue(
                    identity("dialogue"),
                    identity("node"),
                    &witness.greeting,
                    vec![DialogueChoice {
                        id: identity("learn"),
                        text: learn_text.clone(),
                        condition: Condition::Always,
                        effects: vec![DialogueEffect::SetVariable {
                            variable,
                            value: NarrativeValue::Boolean(true),
                        }],
                        next: None,
                    }],
                ));
                Condition::Variable {
                    variable,
                    comparison: NarrativeComparison::Equal,
                    value: NarrativeValue::Boolean(true),
                }
            }
            QuestAlternative::Defeat {
                enemy_name,
                position,
                vitality,
                protection,
                ..
            } => {
                content.actors.push(ActorDefinition {
                    id: identity("enemy"),
                    name: enemy_name.clone(),
                    role: ActorRole::Enemy,
                    position: *position,
                    max_vitality: *vitality,
                    protection: *protection,
                    inventory_capacity: 0,
                    dialogue: None,
                    patrol: vec![],
                    flees_danger: false,
                });
                Condition::ActorDefeated {
                    actor: identity("enemy"),
                }
            }
        };
        flow.solutions.push(QuestSolution {
            id: identity("solution"),
            name: name.clone(),
            condition,
            consume: vec![],
        });
        texts.push(text.clone());
    }
}
