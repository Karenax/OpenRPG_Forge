use crate::{
    ActorDefinition, ActorRole, DialogueDefinition, GAMEPLAY_SCHEMA, GAMEPLAY_VERSION,
    GameplayDocument, GameplayError, ItemDefinition, MAX_GAMEPLAY_ENTRIES, ObjectDefinition,
    ObjectKind, QuestDefinition,
};
use forge_core::{EntityId, Vec3};
use std::collections::BTreeSet;

fn invalid(message: impl Into<String>) -> GameplayError {
    GameplayError::InvalidDocument(message.into())
}

fn text(value: &str, maximum: usize) -> Result<(), GameplayError> {
    if value.trim().is_empty() || value.len() > maximum {
        return Err(invalid("text must be nonempty and bounded"));
    }
    Ok(())
}

pub(crate) fn position(value: Vec3) -> Result<(), GameplayError> {
    if !value.is_finite()
        || [value.x, value.y, value.z]
            .iter()
            .any(|v| v.abs() > 1_000_000.0)
    {
        return Err(invalid("position must be finite and bounded"));
    }
    Ok(())
}

pub(crate) fn rotation(value: [f32; 4]) -> Result<(), GameplayError> {
    let norm_squared: f32 = value.iter().map(|v| v * v).sum();
    if value.iter().any(|v| !v.is_finite()) || (norm_squared - 1.0).abs() > 0.001 {
        return Err(invalid("rotation quaternion must be finite and normalized"));
    }
    Ok(())
}

fn reference(present: bool, id: EntityId) -> Result<(), GameplayError> {
    if present {
        Ok(())
    } else {
        Err(GameplayError::MissingReference(id.to_string()))
    }
}

impl GameplayDocument {
    #[must_use]
    pub fn actor(&self, id: EntityId) -> Option<&ActorDefinition> {
        self.actors.iter().find(|v| v.id == id)
    }
    #[must_use]
    pub fn object(&self, id: EntityId) -> Option<&ObjectDefinition> {
        self.objects.iter().find(|v| v.id == id)
    }
    #[must_use]
    pub fn item(&self, id: EntityId) -> Option<&ItemDefinition> {
        self.items.iter().find(|v| v.id == id)
    }
    #[must_use]
    pub fn quest(&self, id: EntityId) -> Option<&QuestDefinition> {
        self.quests.iter().find(|v| v.id == id)
    }
    #[must_use]
    pub fn dialogue(&self, id: EntityId) -> Option<&DialogueDefinition> {
        self.dialogues.iter().find(|v| v.id == id)
    }

    pub fn validate(&self) -> Result<(), GameplayError> {
        let supported_version = (self.version == GAMEPLAY_VERSION && self.creator.is_none())
            || (self.version == crate::GAMEPLAY_CREATOR_VERSION && self.creator.is_some());
        if self.schema != GAMEPLAY_SCHEMA || !supported_version {
            return Err(invalid("unsupported schema/version"));
        }
        self.profile.validate()?;
        let counts = [
            self.actors.len(),
            self.items.len(),
            self.objects.len(),
            self.places.len(),
            self.dialogues.len(),
            self.quests.len(),
        ];
        if counts.iter().any(|n| *n > MAX_GAMEPLAY_ENTRIES) {
            return Err(invalid("too many definitions"));
        }
        let mut ids = BTreeSet::new();
        let mut unique = |id| {
            if ids.insert(id) {
                Ok(())
            } else {
                Err(invalid(format!("duplicate identity {id}")))
            }
        };
        for actor in &self.actors {
            unique(actor.id)?;
        }
        for item in &self.items {
            unique(item.id)?;
        }
        for object in &self.objects {
            unique(object.id)?;
        }
        for place in &self.places {
            unique(place.id)?;
        }
        for dialogue in &self.dialogues {
            unique(dialogue.id)?;
            if dialogue.nodes.is_empty() || dialogue.nodes.len() > 128 {
                return Err(invalid("dialogue nodes must be bounded"));
            }
            for node in &dialogue.nodes {
                unique(node.id)?;
                if node.choices.len() > 32 {
                    return Err(invalid("too many dialogue choices"));
                }
                for choice in &node.choices {
                    unique(choice.id)?;
                }
            }
        }
        for quest in &self.quests {
            unique(quest.id)?;
        }
        self.validate_presentations(&mut ids)?;
        if let Some(simulation) = &self.simulation {
            if self.creator.is_none() {
                return Err(invalid(
                    "simulation requires explicit Creator v2 capabilities",
                ));
            }
            simulation.validate(self, &mut ids)?;
        }
        if let Some(factions) = &self.factions {
            if self.creator.is_none() {
                return Err(invalid("factions require explicit Creator v2 capabilities"));
            }
            factions.validate(self, &mut ids)?;
        }
        if let Some(narrative) = &self.narrative {
            if self.creator.is_none() {
                return Err(invalid(
                    "narrative requires explicit Creator v2 capabilities",
                ));
            }
            narrative.validate(self, &mut ids)?;
        }
        reference(
            self.actor(self.player)
                .is_some_and(|a| a.role == ActorRole::Player),
            self.player,
        )?;
        if self
            .actors
            .iter()
            .filter(|a| a.role == ActorRole::Player)
            .count()
            != 1
        {
            return Err(invalid("exactly one player required in this profile"));
        }
        self.validate_actors()?;
        self.validate_items()?;
        self.validate_objects()?;
        self.validate_places()?;
        self.validate_dialogues()?;
        self.validate_quests()?;
        self.validate_creator()?;
        Ok(())
    }

    fn validate_presentations(&self, ids: &mut BTreeSet<EntityId>) -> Result<(), GameplayError> {
        if (self.scripting.is_some() || self.cinematics.is_some()) && self.creator.is_none() {
            return Err(invalid(
                "presentation requires explicit Creator v2 capabilities",
            ));
        }
        if let Some(scripting) = &self.scripting {
            scripting
                .validate(self, ids)
                .map_err(GameplayError::ScriptRefused)?;
        }
        if let Some(cinematics) = &self.cinematics {
            if self.simulation.is_none() {
                return Err(invalid("cinematics require the common simulation clock"));
            }
            cinematics.validate(self, ids)?;
        }
        Ok(())
    }

    fn validate_actors(&self) -> Result<(), GameplayError> {
        for actor in &self.actors {
            text(&actor.name, 256)?;
            position(actor.position)?;
            if actor.max_vitality == 0
                || actor.max_vitality > 1_000_000
                || actor.protection > 1_000_000
                || actor.inventory_capacity > crate::MAX_INVENTORY_UNITS
                || actor.patrol.len() > 128
            {
                return Err(invalid(format!("actor bounds: {}", actor.id)));
            }
            for point in &actor.patrol {
                position(*point)?;
            }
            if let Some(id) = actor.dialogue {
                reference(self.dialogue(id).is_some(), id)?;
            }
        }
        Ok(())
    }

    fn validate_items(&self) -> Result<(), GameplayError> {
        for item in &self.items {
            text(&item.name, 256)?;
            if item.max_stack == 0 || item.max_stack > 10_000 {
                return Err(invalid("invalid item stack"));
            }
        }
        Ok(())
    }

    fn validate_objects(&self) -> Result<(), GameplayError> {
        for object in &self.objects {
            text(&object.name, 256)?;
            position(object.position)?;
            let physics = &object.physics;
            if !physics.body_half_extents_m.is_finite()
                || [
                    physics.body_half_extents_m.x,
                    physics.body_half_extents_m.y,
                    physics.body_half_extents_m.z,
                ]
                .iter()
                .any(|v| !(0.025..=10.0).contains(v))
                || !physics.mass_kg.is_finite()
                || !(0.0..=100_000.0).contains(&physics.mass_kg)
                || ((physics.pushable || physics.throwable) && physics.mass_kg <= 0.0)
                || physics
                    .break_energy_j
                    .is_some_and(|v| !v.is_finite() || !(0.01..=1_000_000.0).contains(&v))
                || physics.supports.len() > 64
                || physics.heat_damage_per_tick > 1_000_000
            {
                return Err(invalid(format!("object physics bounds: {}", object.id)));
            }
            if let ObjectKind::Pickup { item, quantity } = object.kind {
                let definition = self
                    .item(item)
                    .ok_or_else(|| GameplayError::MissingReference(item.to_string()))?;
                if quantity == 0 || quantity > definition.max_stack {
                    return Err(invalid("invalid pickup quantity"));
                }
            }
            if let ObjectKind::Container { definition } = &object.kind {
                if self.creator.is_none() {
                    return Err(invalid(
                        "containers require explicit Creator v2 capabilities",
                    ));
                }
                definition.validate(self)?;
            }
            let mut supports = BTreeSet::new();
            for supported in &physics.supports {
                reference(self.object(*supported).is_some(), *supported)?;
                if *supported == object.id || !supports.insert(*supported) {
                    return Err(invalid("invalid support identity"));
                }
            }
            // Bounded traversal rejects indirect support cycles as well as self cycles.
            let mut pending = physics.supports.clone();
            let mut visited = BTreeSet::new();
            while let Some(id) = pending.pop() {
                if id == object.id {
                    return Err(invalid("cyclic support relationship"));
                }
                if visited.insert(id)
                    && let Some(supported) = self.object(id)
                {
                    pending.extend(&supported.physics.supports);
                }
            }
        }
        Ok(())
    }

    fn validate_places(&self) -> Result<(), GameplayError> {
        for place in &self.places {
            text(&place.name, 256)?;
            position(place.position)?;
            if place.links.len() > 64 {
                return Err(invalid("too many place links"));
            }
            let mut links = BTreeSet::new();
            for id in &place.links {
                reference(self.places.iter().any(|v| v.id == *id), *id)?;
                if !links.insert(*id) {
                    return Err(invalid("duplicate place link"));
                }
            }
        }
        Ok(())
    }

    fn validate_dialogues(&self) -> Result<(), GameplayError> {
        for dialogue in &self.dialogues {
            reference(
                dialogue.nodes.iter().any(|n| n.id == dialogue.start),
                dialogue.start,
            )?;
            for node in &dialogue.nodes {
                text(&node.text, 8192)?;
                for choice in &node.choices {
                    text(&choice.text, 1024)?;
                    self.validate_condition(&choice.condition)?;
                    if choice.effects.len() > 32 {
                        return Err(invalid("too many dialogue effects"));
                    }
                    for effect in &choice.effects {
                        self.validate_dialogue_effect(effect)?;
                    }
                    if let Some(next) = choice.next {
                        reference(dialogue.nodes.iter().any(|n| n.id == next), next)?;
                    }
                }
            }
        }
        Ok(())
    }

    fn validate_quests(&self) -> Result<(), GameplayError> {
        for quest in &self.quests {
            text(&quest.name, 256)?;
            reference(
                self.actor(quest.giver)
                    .is_some_and(|a| a.role == ActorRole::Friendly),
                quest.giver,
            )?;
            let item = self
                .item(quest.required_item)
                .ok_or_else(|| GameplayError::MissingReference(quest.required_item.to_string()))?;
            if quest.required_quantity == 0 || quest.required_quantity > item.max_stack {
                return Err(invalid("invalid quest quantity"));
            }
            if let Some(id) = quest.reward_item {
                let reward = self
                    .item(id)
                    .ok_or_else(|| GameplayError::MissingReference(id.to_string()))?;
                if quest.reward_quantity == 0 || quest.reward_quantity > reward.max_stack {
                    return Err(invalid("invalid reward quantity"));
                }
            } else if quest.reward_quantity != 0 {
                return Err(invalid("reward quantity without item"));
            }
        }
        Ok(())
    }
}
