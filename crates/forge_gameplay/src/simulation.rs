use crate::{GameplayDocument, GameplayError};
use forge_core::{CommandId, ContentHash, EntityId, Vec3};
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ActorState {
    #[serde(default = "initial_presence", skip_serializing_if = "is_present")]
    pub active: bool,
    pub position: Vec3,
    pub vitality: u32,
    pub stamina: u32,
    pub inventory: BTreeMap<EntityId, u32>,
    pub fleeing: bool,
}

// These independent component states compose (e.g. a released object can burn).
// A single lifecycle enum would incorrectly make them mutually exclusive.
#[allow(clippy::struct_excessive_bools)]
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ObjectState {
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub container: Option<crate::ContainerState>,
    pub position: Vec3,
    #[serde(default = "identity_rotation")]
    pub rotation_xyzw: [f32; 4],
    pub initiator: Option<EntityId>,
    pub consumed: bool,
    pub destroyed: bool,
    pub open: bool,
    pub burning: bool,
    pub released: bool,
    pub in_motion: bool,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum GameAction {
    RunScript {
        script: EntityId,
    },
    StartCinematic {
        sequence: EntityId,
    },
    SkipCinematic,
    InterruptCinematic,
    UseAbility {
        ability: EntityId,
        target: EntityId,
    },
    CancelEffect {
        instance: EntityId,
    },
    TransferItem {
        object: EntityId,
        item: EntityId,
        quantity: u32,
        direction: crate::TransferDirection,
    },
    SetContainerLocked {
        object: EntityId,
        locked: bool,
    },
    Pickup {
        object: EntityId,
    },
    ChooseDialogue {
        speaker: EntityId,
        node: EntityId,
        choice: EntityId,
    },
    Attack {
        target: EntityId,
    },
    SetDoor {
        object: EntityId,
        open: bool,
    },
    Push {
        object: EntityId,
        impulse: Vec3,
    },
    Throw {
        object: EntityId,
        impulse: Vec3,
    },
    Ignite {
        object: EntityId,
    },
    Extinguish {
        object: EntityId,
    },
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GameRequest {
    pub command_id: CommandId,
    pub expected_sequence: u64,
    pub actor: EntityId,
    pub action: GameAction,
}

/// Supplied by the engine's target/contact adapter, never by a presentation-only rule.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct ActionObservation {
    pub line_of_sight: bool,
    pub confirmed_contact: bool,
    pub water_contact: bool,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum GameplayEvent {
    ScriptMessage {
        script: EntityId,
        text: String,
    },
    ScriptExecuted {
        script: EntityId,
        steps: usize,
    },
    CinematicStarted {
        playback: crate::CinematicPlayback,
    },
    CinematicEnded {
        sequence: EntityId,
        cause: CommandId,
        reason: crate::CinematicEndReason,
        elapsed_ns: u64,
    },
    AbilityUsed {
        ability: EntityId,
        target: EntityId,
    },
    Healed {
        target: EntityId,
        amount: u32,
    },
    TimedEffectApplied {
        effect: crate::ActiveEffect,
    },
    TimedEffectEnded {
        instance: EntityId,
        reason: crate::EffectEndReason,
    },
    CalendarTriggered {
        event: EntityId,
        cycle: u64,
        scheduled_ns: u64,
    },
    EncounterActivated {
        activation: crate::EncounterActivation,
    },
    FactionReaction {
        change: crate::FactionConsequence,
    },
    VariableChanged {
        variable: EntityId,
        value: crate::NarrativeValue,
    },
    ItemTransferred {
        object: EntityId,
        actor: EntityId,
        item: EntityId,
        quantity: u32,
        direction: crate::TransferDirection,
    },
    ContainerLocked {
        object: EntityId,
        locked: bool,
    },
    PickedUp {
        object: EntityId,
        item: EntityId,
        quantity: u32,
    },
    QuestChanged {
        quest: EntityId,
        status: crate::QuestStatus,
    },
    Damage {
        target: EntityId,
        contact: bool,
        amount: u32,
    },
    Impact {
        source: EntityId,
        target: EntityId,
        initiator: Option<EntityId>,
        energy_j: f64,
    },
    DoorChanged {
        object: EntityId,
        open: bool,
    },
    Impulse {
        object: EntityId,
        impulse: Vec3,
    },
    Destroyed {
        object: EntityId,
    },
    Released {
        object: EntityId,
    },
    Burning {
        object: EntityId,
        active: bool,
    },
    Fleeing {
        actor: EntityId,
    },
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GameReceipt {
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub simulation_time_ns: Option<u64>,
    pub request: GameRequest,
    pub sequence: u64,
    pub tick: u64,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub faction_witnesses: Vec<crate::FactionWitnessReceipt>,
    pub events: Vec<GameplayEvent>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GameState {
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub cinematic: Option<crate::CinematicPlayback>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub simulation: Option<crate::SimulationState>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub factions: Option<crate::FactionsState>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub narrative: Option<crate::NarrativeState>,
    pub sequence: u64,
    pub tick: u64,
    pub actors: BTreeMap<EntityId, ActorState>,
    pub objects: BTreeMap<EntityId, ObjectState>,
    pub quests: BTreeMap<EntityId, crate::QuestStatus>,
    pub receipts: Vec<GameReceipt>,
    pub contacts: BTreeMap<CommandId, crate::ContactReceipt>,
    pub last_frame: Option<crate::PhysicsFrameReceipt>,
    #[serde(default)]
    pub requires_physics_revalidation: bool,
    pub tick_events_used: usize,
    pub dialogue_cursors: BTreeMap<EntityId, EntityId>,
}

pub struct GameSession {
    pub(crate) document: GameplayDocument,
    pub(crate) source_hash: ContentHash,
    pub(crate) state: GameState,
}

impl GameSession {
    pub fn new(
        document: GameplayDocument,
        source_hash: ContentHash,
    ) -> Result<Self, GameplayError> {
        document.validate()?;
        let state = GameState {
            cinematic: None,
            simulation: document
                .simulation
                .as_ref()
                .map(|_| crate::SimulationState::default()),
            factions: document
                .factions
                .as_ref()
                .map(|_| crate::FactionsState::default()),
            narrative: document
                .narrative
                .as_ref()
                .map(crate::NarrativeDefinition::initial_state),
            sequence: 0,
            tick: 0,
            actors: document
                .actors
                .iter()
                .map(|actor| {
                    (
                        actor.id,
                        ActorState {
                            active: document.actor_initially_present(actor.id),
                            position: actor.position,
                            vitality: actor.max_vitality,
                            stamina: document.profile.max_stamina,
                            inventory: BTreeMap::new(),
                            fleeing: false,
                        },
                    )
                })
                .collect(),
            objects: document
                .objects
                .iter()
                .map(|object| {
                    (
                        object.id,
                        ObjectState {
                            container: match &object.kind {
                                crate::ObjectKind::Container { definition } => {
                                    Some(definition.initial_state())
                                }
                                _ => None,
                            },
                            position: object.position,
                            rotation_xyzw: identity_rotation(),
                            initiator: None,
                            consumed: false,
                            destroyed: false,
                            open: matches!(
                                object.kind,
                                crate::ObjectKind::Door {
                                    initially_open: true
                                }
                            ),
                            burning: false,
                            released: false,
                            in_motion: false,
                        },
                    )
                })
                .collect(),
            quests: document
                .quests
                .iter()
                .map(|quest| (quest.id, crate::QuestStatus::Available))
                .collect(),
            receipts: Vec::new(),
            contacts: BTreeMap::new(),
            last_frame: None,
            requires_physics_revalidation: false,
            tick_events_used: 0,
            dialogue_cursors: BTreeMap::new(),
        };
        Ok(Self {
            document,
            source_hash,
            state,
        })
    }
    #[must_use]
    pub const fn state(&self) -> &GameState {
        &self.state
    }
    #[must_use]
    pub const fn document(&self) -> &GameplayDocument {
        &self.document
    }
    #[must_use]
    pub const fn source_hash(&self) -> ContentHash {
        self.source_hash
    }
    /// Position observed from the canonical collision adapter, not a teleport action.
    pub fn observe_position(
        &mut self,
        actor: EntityId,
        position: Vec3,
    ) -> Result<(), GameplayError> {
        crate::validation::position(position)?;
        let actor = self
            .state
            .actors
            .get_mut(&actor)
            .ok_or_else(|| GameplayError::MissingReference(actor.to_string()))?;
        if !actor.active && actor.position != position {
            return Err(GameplayError::ActionRefused(
                "an absent actor cannot change pose".into(),
            ));
        }
        actor.position = position;
        Ok(())
    }
    /// A physical object may only be saved after the adapter reports a settled state.
    pub fn observe_object(
        &mut self,
        object: EntityId,
        position: Vec3,
        in_motion: bool,
    ) -> Result<(), GameplayError> {
        crate::validation::position(position)?;
        let object = self
            .state
            .objects
            .get_mut(&object)
            .ok_or_else(|| GameplayError::MissingReference(object.to_string()))?;
        object.position = position;
        object.in_motion = in_motion;
        Ok(())
    }
}

pub(crate) fn identity_rotation() -> [f32; 4] {
    [0.0, 0.0, 0.0, 1.0]
}

fn initial_presence() -> bool {
    true
}

#[allow(
    clippy::trivially_copy_pass_by_ref,
    reason = "serde skip predicates receive a borrowed field"
)]
fn is_present(value: &bool) -> bool {
    *value
}
