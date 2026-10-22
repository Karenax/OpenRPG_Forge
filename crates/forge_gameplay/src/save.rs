use crate::{GameSession, GameState, GameplayDocument, GameplayError, ObjectKind};
use forge_core::{ContentHash, canonical_json_bytes, canonical_json_sha256};
use serde::{Deserialize, Serialize};
use std::collections::BTreeSet;

const MAX_SAVE_BYTES: usize = 4 * 1024 * 1024;

#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct GameSave {
    schema: String,
    version: u32,
    module_id: forge_core::ModuleId,
    area_id: forge_core::AreaId,
    source_hash: ContentHash,
    functional_hash: ContentHash,
    state_hash: ContentHash,
    state: GameState,
}

impl GameplayDocument {
    /// Text and initial door pose may be retouched without changing persistent meaning.
    pub fn functional_hash(&self) -> Result<ContentHash, GameplayError> {
        self.validate()?;
        let mut functional = self.clone();
        // Authoring surface visibility never changes the meaning of a saved game.
        functional.creator = None;
        functional.version = crate::GAMEPLAY_VERSION;
        if let Some(scripting) = &mut functional.scripting {
            for script in &mut scripting.scripts {
                script.name.clear();
            }
        }
        if let Some(cinematics) = &mut functional.cinematics {
            for sequence in &mut cinematics.sequences {
                sequence.name.clear();
            }
        }
        if let Some(simulation) = &mut functional.simulation {
            for ability in &mut simulation.abilities {
                ability.name.clear();
            }
            for effect in &mut simulation.effects {
                effect.name.clear();
            }
            for event in &mut simulation.events {
                event.name.clear();
            }
            for encounter in &mut simulation.encounters {
                encounter.name.clear();
            }
        }
        if let Some(factions) = &mut functional.factions {
            for faction in &mut factions.factions {
                faction.name.clear();
            }
        }
        if let Some(narrative) = &mut functional.narrative {
            for variable in &mut narrative.variables {
                variable.name.clear();
            }
            for flow in &mut narrative.quests {
                flow.journal_available.clear();
                flow.journal_active.clear();
                flow.journal_completed.clear();
                flow.journal_failed.clear();
                for solution in &mut flow.solutions {
                    solution.name.clear();
                }
            }
        }
        for actor in &mut functional.actors {
            actor.name.clear();
        }
        for item in &mut functional.items {
            item.name.clear();
        }
        for object in &mut functional.objects {
            object.name.clear();
            if matches!(object.kind, ObjectKind::Door { .. }) {
                object.position = forge_core::Vec3::ZERO;
            }
        }
        for place in &mut functional.places {
            place.name.clear();
        }
        for quest in &mut functional.quests {
            quest.name.clear();
        }
        for dialogue in &mut functional.dialogues {
            for node in &mut dialogue.nodes {
                node.text.clear();
                for choice in &mut node.choices {
                    choice.text.clear();
                }
            }
        }
        canonical_json_sha256(&functional).map_err(|e| GameplayError::Codec(e.to_string()))
    }

    fn save_version(&self) -> u32 {
        if self.scripting.is_some() || self.cinematics.is_some() {
            6
        } else if self.simulation.is_some() {
            5
        } else if self.factions.is_some() {
            4
        } else if self.narrative.is_some() {
            3
        } else if self.has_containers() {
            2
        } else {
            1
        }
    }
}

impl GameSession {
    pub fn save_bytes(&self) -> Result<Vec<u8>, GameplayError> {
        if self.state.requires_physics_revalidation {
            return Err(GameplayError::ActionRefused(
                "wait for the current physics projection after compatible edits".into(),
            ));
        }
        if self.state.objects.values().any(|v| v.in_motion) {
            return Err(GameplayError::ActionRefused(
                "wait for physical objects to settle before saving".into(),
            ));
        }
        self.validate_state()?;
        let save = GameSave {
            schema: "forge.game-save".into(),
            version: self.document.save_version(),
            module_id: self.document.module_id,
            area_id: self.document.area_id,
            source_hash: self.source_hash,
            functional_hash: self.document.functional_hash()?,
            state_hash: canonical_json_sha256(&self.state)
                .map_err(|e| GameplayError::Codec(e.to_string()))?,
            state: self.state.clone(),
        };
        let bytes = canonical_json_bytes(&save).map_err(|e| GameplayError::Codec(e.to_string()))?;
        if bytes.len() > MAX_SAVE_BYTES {
            return Err(GameplayError::Codec("save exceeds 4 MiB".into()));
        }
        Ok(bytes)
    }

    pub fn resume(
        document: GameplayDocument,
        source_hash: ContentHash,
        bytes: &[u8],
        allow_compatible_edits: bool,
    ) -> Result<Self, GameplayError> {
        document.validate()?;
        if bytes.len() > MAX_SAVE_BYTES {
            return Err(GameplayError::Codec("save exceeds 4 MiB".into()));
        }
        let save: GameSave =
            serde_json::from_slice(bytes).map_err(|e| GameplayError::Codec(e.to_string()))?;
        if save.schema != "forge.game-save"
            || save.version != document.save_version()
            || save.module_id != document.module_id
            || save.area_id != document.area_id
        {
            return Err(GameplayError::IncompatibleSave(
                "schema/version/module/area mismatch".into(),
            ));
        }
        if save.source_hash != source_hash && !allow_compatible_edits {
            return Err(GameplayError::IncompatibleSave(
                "source changed; explicitly review compatible edits".into(),
            ));
        }
        if save.functional_hash != document.functional_hash()? {
            return Err(GameplayError::IncompatibleSave(
                "functional identity or rules changed".into(),
            ));
        }
        if save.state_hash
            != canonical_json_sha256(&save.state)
                .map_err(|e| GameplayError::Codec(e.to_string()))?
        {
            return Err(GameplayError::IncompatibleSave(
                "state integrity mismatch".into(),
            ));
        }
        let mut session = Self {
            document,
            source_hash: save.source_hash,
            state: save.state,
        };
        session.validate_state()?;
        if source_hash != session.source_hash {
            // Old observations remain historical receipts, never proof of the retouched geometry.
            session.state.last_frame = None;
            session.state.tick_events_used = 0;
            session.state.requires_physics_revalidation = true;
            session.source_hash = source_hash;
        }
        // A door keeps its game state but projects the current authored pose.
        for definition in &session.document.objects {
            if matches!(definition.kind, ObjectKind::Door { .. })
                && let Some(state) = session.state.objects.get_mut(&definition.id)
            {
                state.position = definition.position;
            }
        }
        session.validate_state()?;
        Ok(session)
    }

    fn validate_state(&self) -> Result<(), GameplayError> {
        let invalid = |message: &str| GameplayError::IncompatibleSave(message.into());
        if self.state.actors.len() != self.document.actors.len()
            || self.state.objects.len() != self.document.objects.len()
            || self.state.quests.len() != self.document.quests.len()
            || self.state.receipts.len() > self.document.profile.max_action_receipts
            || self.state.contacts.len() > self.document.profile.max_action_receipts
        {
            return Err(invalid("state identity or journal bounds mismatch"));
        }
        for definition in &self.document.actors {
            let state = self
                .state
                .actors
                .get(&definition.id)
                .ok_or_else(|| invalid("actor missing from save"))?;
            crate::validation::position(state.position)?;
            if state.vitality > definition.max_vitality
                || state.stamina > self.document.profile.max_stamina
                || (state.fleeing && !definition.flees_danger)
            {
                return Err(invalid("actor resources invalid"));
            }
            let total: u64 = state.inventory.values().map(|v| u64::from(*v)).sum();
            if total > u64::from(definition.inventory_capacity) {
                return Err(invalid("inventory capacity exceeded"));
            }
            for (item, quantity) in &state.inventory {
                if *quantity == 0
                    || self
                        .document
                        .item(*item)
                        .is_none_or(|v| *quantity > v.max_stack)
                {
                    return Err(invalid("inventory item invalid"));
                }
            }
        }
        for definition in &self.document.objects {
            let state = self
                .state
                .objects
                .get(&definition.id)
                .ok_or_else(|| invalid("object missing from save"))?;
            crate::validation::position(state.position)?;
            crate::validation::rotation(state.rotation_xyzw)?;
            self.validate_saved_container(definition, state)?;
            if state.in_motion
                || (state.burning
                    && (!definition.physics.flammable || state.destroyed || state.consumed))
                || (state.consumed && !matches!(definition.kind, ObjectKind::Pickup { .. }))
                || (state.open && !matches!(definition.kind, ObjectKind::Door { .. }))
                || state
                    .initiator
                    .is_some_and(|id| !self.state.actors.contains_key(&id))
                || (state.destroyed && definition.physics.break_energy_j.is_none())
                || (state.released
                    && !definition.physics.throwable
                    && !self
                        .document
                        .objects
                        .iter()
                        .any(|support| support.physics.supports.contains(&definition.id)))
            {
                return Err(invalid("object state invalid"));
            }
        }
        self.validate_saved_quests()?;
        let mut command_ids = BTreeSet::new();
        let mut previous = 0_u64;
        let mut previous_tick = 0_u64;
        for receipt in &self.state.receipts {
            if !command_ids.insert(receipt.request.command_id)
                || previous.checked_add(1) != Some(receipt.sequence)
                || receipt.request.expected_sequence.checked_add(1) != Some(receipt.sequence)
                || !self.state.actors.contains_key(&receipt.request.actor)
                || receipt.tick > self.state.tick
                || receipt.tick < previous_tick
                || receipt.events.len() > self.document.profile.max_events_per_tick
            {
                return Err(invalid("action journal invalid"));
            }
            previous = receipt.sequence;
            previous_tick = receipt.tick;
            self.validate_saved_action(&receipt.request)?;
            for event in &receipt.events {
                self.validate_event(event)?;
            }
        }
        if self.state.sequence != previous {
            return Err(invalid("action sequence does not match retained journal"));
        }
        self.validate_state_ledgers()
    }

    fn validate_state_ledgers(&self) -> Result<(), GameplayError> {
        self.validate_environment_state()?;
        self.validate_dialogue_cursors()?;
        self.validate_inventory_ledger()?;
        self.validate_narrative_state()?;
        self.validate_faction_state()?;
        self.validate_simulation_state()?;
        self.validate_script_output_authority()?;
        self.validate_door_projection()?;
        self.validate_cinematic_state()?;
        Ok(())
    }

    fn validate_saved_container(
        &self,
        definition: &crate::ObjectDefinition,
        state: &crate::ObjectState,
    ) -> Result<(), GameplayError> {
        let invalid = |message: &str| GameplayError::IncompatibleSave(message.into());
        match (&definition.kind, &state.container) {
            (
                ObjectKind::Container {
                    definition: container,
                },
                Some(saved),
            ) => {
                let mut checked = container.clone();
                checked.initial_items.clone_from(&saved.items);
                checked
                    .validate(&self.document)
                    .map_err(|error| invalid(&error.to_string()))?;
                if saved.locked && container.key.is_none() {
                    return Err(invalid("container has no lock"));
                }
            }
            (ObjectKind::Container { .. }, None) => return Err(invalid("container state missing")),
            (_, Some(_)) => return Err(invalid("container state on another object kind")),
            (_, None) => (),
        }
        Ok(())
    }

    fn validate_saved_quests(&self) -> Result<(), GameplayError> {
        let invalid = |message: &str| GameplayError::IncompatibleSave(message.into());
        for definition in &self.document.quests {
            if !self.state.quests.contains_key(&definition.id) {
                return Err(invalid("quest missing from save"));
            }
            if self.state.quests.get(&definition.id) == Some(&crate::QuestStatus::Failed)
                && self
                    .document
                    .quest_flow(definition.id)
                    .is_none_or(|flow| flow.failure_condition.is_none())
            {
                return Err(invalid("failed quest has no authored failure"));
            }
        }
        Ok(())
    }

    fn validate_dialogue_cursors(&self) -> Result<(), GameplayError> {
        for (speaker, node) in &self.state.dialogue_cursors {
            let dialogue = self
                .document
                .actor(*speaker)
                .and_then(|a| a.dialogue)
                .and_then(|id| self.document.dialogue(id));
            if dialogue.is_none_or(|d| !d.nodes.iter().any(|n| n.id == *node)) {
                return Err(GameplayError::IncompatibleSave(
                    "dialogue cursor invalid".into(),
                ));
            }
        }
        Ok(())
    }
}
