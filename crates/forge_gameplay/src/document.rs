use forge_core::{AreaId, EntityId, ModuleId, Vec3};
use serde::{Deserialize, Serialize};

pub const GAMEPLAY_SCHEMA: &str = "forge.gameplay";
pub const GAMEPLAY_VERSION: u32 = 1;
pub const MAX_GAMEPLAY_ENTRIES: usize = 1024;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GameplayDocument {
    pub schema: String,
    pub version: u32,
    pub module_id: ModuleId,
    pub area_id: AreaId,
    pub profile: RulesProfile,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub creator: Option<crate::CreatorConfiguration>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub narrative: Option<crate::NarrativeDefinition>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub factions: Option<crate::FactionsDefinition>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub simulation: Option<crate::SimulationDefinition>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub scripting: Option<crate::ScriptingDefinition>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub cinematics: Option<crate::CinematicsDefinition>,
    pub player: EntityId,
    pub actors: Vec<ActorDefinition>,
    pub items: Vec<ItemDefinition>,
    pub objects: Vec<ObjectDefinition>,
    pub places: Vec<PlaceDefinition>,
    pub dialogues: Vec<DialogueDefinition>,
    pub quests: Vec<QuestDefinition>,
}

/// Explicit small Gate 3 profile, not conformance to the entire reference manual.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RulesProfile {
    pub id: String,
    pub version: u32,
    pub interaction_range_m: f32,
    pub attack_range_m: f32,
    pub attack_damage: u32,
    pub attack_stamina_cost: u32,
    pub max_stamina: u32,
    pub max_action_receipts: usize,
    pub max_events_per_tick: usize,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ActorRole {
    Player,
    Friendly,
    Enemy,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ActorDefinition {
    pub id: EntityId,
    pub name: String,
    pub role: ActorRole,
    pub position: Vec3,
    pub max_vitality: u32,
    pub protection: u32,
    pub inventory_capacity: u32,
    pub dialogue: Option<EntityId>,
    pub patrol: Vec<Vec3>,
    pub flees_danger: bool,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ItemDefinition {
    pub id: EntityId,
    pub name: String,
    pub max_stack: u32,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ObjectDefinition {
    pub id: EntityId,
    pub name: String,
    pub position: Vec3,
    pub kind: ObjectKind,
    pub physics: ObjectPhysics,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum ObjectKind {
    Pickup {
        item: EntityId,
        quantity: u32,
    },
    Door {
        initially_open: bool,
    },
    Container {
        definition: crate::ContainerDefinition,
    },
    Prop,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ObjectPhysics {
    #[serde(default = "default_half_extents")]
    pub body_half_extents_m: Vec3,
    pub mass_kg: f32,
    pub pushable: bool,
    pub throwable: bool,
    pub break_energy_j: Option<f32>,
    pub supports: Vec<EntityId>,
    pub flammable: bool,
    pub heat_damage_per_tick: u32,
}

fn default_half_extents() -> Vec3 {
    Vec3::new(0.5, 0.5, 0.5)
}

impl Default for ObjectPhysics {
    fn default() -> Self {
        Self {
            body_half_extents_m: default_half_extents(),
            mass_kg: 0.0,
            pushable: false,
            throwable: false,
            break_energy_j: None,
            supports: Vec::new(),
            flammable: false,
            heat_damage_per_tick: 0,
        }
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PlaceDefinition {
    pub id: EntityId,
    pub name: String,
    pub position: Vec3,
    pub links: Vec<EntityId>,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum QuestStatus {
    Available,
    Active,
    Completed,
    Failed,
}

impl QuestStatus {
    #[must_use]
    pub const fn label(self) -> &'static str {
        match self {
            Self::Available => "À découvrir",
            Self::Active => "En cours",
            Self::Completed => "Terminée",
            Self::Failed => "Échec",
        }
    }
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum Condition {
    Relation {
        observer: EntityId,
        subject: EntityId,
        comparison: crate::NarrativeComparison,
        threshold: i16,
    },
    Variable {
        variable: EntityId,
        comparison: crate::NarrativeComparison,
        value: crate::NarrativeValue,
    },
    All {
        conditions: Vec<Condition>,
    },
    Any {
        conditions: Vec<Condition>,
    },
    Always,
    Quest {
        quest: EntityId,
        status: QuestStatus,
    },
    HasItem {
        item: EntityId,
        quantity: u32,
    },
    ActorDefeated {
        actor: EntityId,
    },
    ObjectDestroyed {
        object: EntityId,
    },
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum DialogueEffect {
    SetVariable {
        variable: EntityId,
        value: crate::NarrativeValue,
    },
    CompleteQuestSolution {
        quest: EntityId,
        solution: EntityId,
    },
    FailQuest {
        quest: EntityId,
    },
    AcceptQuest {
        quest: EntityId,
    },
    CompleteQuest {
        quest: EntityId,
    },
    SetDoor {
        object: EntityId,
        open: bool,
    },
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct DialogueChoice {
    pub id: EntityId,
    pub text: String,
    pub condition: Condition,
    pub effects: Vec<DialogueEffect>,
    pub next: Option<EntityId>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct DialogueNode {
    pub id: EntityId,
    pub text: String,
    pub choices: Vec<DialogueChoice>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct DialogueDefinition {
    pub id: EntityId,
    pub start: EntityId,
    pub nodes: Vec<DialogueNode>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct QuestDefinition {
    pub id: EntityId,
    pub name: String,
    pub giver: EntityId,
    pub required_item: EntityId,
    pub required_quantity: u32,
    pub reward_item: Option<EntityId>,
    pub reward_quantity: u32,
}
