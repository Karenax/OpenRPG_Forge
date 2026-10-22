//! Authoring inheritance materializes into the same actors consumed by the runtime.
use crate::{ActorDefinition, ActorRole, GameplayCapability, GameplayDocument, GameplayError};
use forge_core::EntityId;
use serde::{Deserialize, Serialize};
use std::collections::BTreeSet;

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ActorBlueprintDefaults {
    pub role: ActorRole,
    pub max_vitality: u32,
    pub protection: u32,
    pub inventory_capacity: u32,
    pub flees_danger: bool,
}

impl ActorBlueprintDefaults {
    #[must_use]
    pub const fn from_actor(actor: &ActorDefinition) -> Self {
        Self {
            role: actor.role,
            max_vitality: actor.max_vitality,
            protection: actor.protection,
            inventory_capacity: actor.inventory_capacity,
            flees_danger: actor.flees_danger,
        }
    }

    fn validate(&self) -> Result<(), GameplayError> {
        if !(1..=1_000_000).contains(&self.max_vitality)
            || self.protection > 1_000_000
            || self.inventory_capacity > crate::MAX_INVENTORY_UNITS
        {
            return Err(invalid("actor defaults exceed the supported actor bounds"));
        }
        Ok(())
    }

    fn project(&self, actor: &mut ActorDefinition) {
        actor.role = self.role;
        actor.max_vitality = self.max_vitality;
        actor.protection = self.protection;
        actor.inventory_capacity = self.inventory_capacity;
        actor.flees_danger = self.flees_danger;
    }
}

#[derive(Clone, Debug, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ActorBlueprintOverrides {
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub role: Option<ActorRole>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub max_vitality: Option<u32>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub protection: Option<u32>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub inventory_capacity: Option<u32>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub flees_danger: Option<bool>,
}

impl ActorBlueprintOverrides {
    #[must_use]
    pub fn resolve(&self, defaults: &ActorBlueprintDefaults) -> ActorBlueprintDefaults {
        ActorBlueprintDefaults {
            role: self.role.unwrap_or(defaults.role),
            max_vitality: self.max_vitality.unwrap_or(defaults.max_vitality),
            protection: self.protection.unwrap_or(defaults.protection),
            inventory_capacity: self
                .inventory_capacity
                .unwrap_or(defaults.inventory_capacity),
            flees_danger: self.flees_danger.unwrap_or(defaults.flees_danger),
        }
    }

    /// Existing inspector edits become explicit local overrides, one changed field at a time.
    pub fn capture_changes(&mut self, before: &ActorDefinition, after: &ActorDefinition) {
        if before.role != after.role {
            self.role = Some(after.role);
        }
        if before.max_vitality != after.max_vitality {
            self.max_vitality = Some(after.max_vitality);
        }
        if before.protection != after.protection {
            self.protection = Some(after.protection);
        }
        if before.inventory_capacity != after.inventory_capacity {
            self.inventory_capacity = Some(after.inventory_capacity);
        }
        if before.flees_danger != after.flees_danger {
            self.flees_danger = Some(after.flees_danger);
        }
    }
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ActorBlueprintDefinition {
    pub id: EntityId,
    pub version: u32,
    pub name: String,
    pub defaults: ActorBlueprintDefaults,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ActorBlueprintBinding {
    pub actor: EntityId,
    pub blueprint: EntityId,
    pub blueprint_version: u32,
    pub overrides: ActorBlueprintOverrides,
}

#[derive(Clone, Debug, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ActorBlueprintLibrary {
    pub definitions: Vec<ActorBlueprintDefinition>,
    pub bindings: Vec<ActorBlueprintBinding>,
}

fn invalid(message: impl Into<String>) -> GameplayError {
    GameplayError::InvalidDocument(format!("creator.blueprints: {}", message.into()))
}

impl ActorBlueprintLibrary {
    fn resolve(
        &self,
        binding: &ActorBlueprintBinding,
    ) -> Result<ActorBlueprintDefaults, GameplayError> {
        let definition = self
            .definitions
            .iter()
            .find(|d| d.id == binding.blueprint)
            .ok_or_else(|| {
                GameplayError::MissingReference(format!(
                    "creator.blueprints.definition/{}",
                    binding.blueprint
                ))
            })?;
        if definition.version != binding.blueprint_version {
            return Err(invalid(format!(
                "stale blueprint pin for actor {}",
                binding.actor
            )));
        }
        let values = binding.overrides.resolve(&definition.defaults);
        values.validate()?;
        Ok(values)
    }

    pub(crate) fn validate(&self, document: &GameplayDocument) -> Result<(), GameplayError> {
        if self.definitions.len() > 256 || self.bindings.len() > crate::MAX_GAMEPLAY_ENTRIES {
            return Err(invalid("library limit exceeded"));
        }
        let occupied = core_ids(document);
        let mut definitions = BTreeSet::new();
        for definition in &self.definitions {
            if !definitions.insert(definition.id) || occupied.contains(&definition.id) {
                return Err(invalid(format!(
                    "duplicate definition identity {}",
                    definition.id
                )));
            }
            if definition.version == 0
                || definition.name.trim().is_empty()
                || definition.name.len() > 256
            {
                return Err(invalid(format!(
                    "invalid definition label/version {}",
                    definition.id
                )));
            }
            definition.defaults.validate()?;
        }
        let mut actors = BTreeSet::new();
        for binding in &self.bindings {
            if !actors.insert(binding.actor) {
                return Err(invalid(format!(
                    "duplicate actor binding {}",
                    binding.actor
                )));
            }
            let actor = document.actor(binding.actor).ok_or_else(|| {
                GameplayError::MissingReference(format!(
                    "creator.blueprints.actor/{}",
                    binding.actor
                ))
            })?;
            if ActorBlueprintDefaults::from_actor(actor) != self.resolve(binding)? {
                return Err(invalid(format!(
                    "actor {} differs from its blueprint and explicit overrides",
                    binding.actor
                )));
            }
        }
        Ok(())
    }

    fn project(&self, document: &mut GameplayDocument) -> Result<(), GameplayError> {
        for binding in &self.bindings {
            let defaults = self.resolve(binding)?;
            let actor = document
                .actors
                .iter_mut()
                .find(|a| a.id == binding.actor)
                .ok_or_else(|| {
                    GameplayError::MissingReference(format!(
                        "creator.blueprints.actor/{}",
                        binding.actor
                    ))
                })?;
            defaults.project(actor);
        }
        Ok(())
    }
}

fn core_ids(document: &GameplayDocument) -> BTreeSet<EntityId> {
    document
        .actors
        .iter()
        .map(|v| v.id)
        .chain(document.objects.iter().map(|v| v.id))
        .chain(document.items.iter().map(|v| v.id))
        .chain(document.places.iter().map(|v| v.id))
        .chain(document.quests.iter().map(|v| v.id))
        .chain(document.dialogues.iter().map(|v| v.id))
        .chain(
            document
                .dialogues
                .iter()
                .flat_map(|d| d.nodes.iter())
                .map(|n| n.id),
        )
        .chain(
            document
                .dialogues
                .iter()
                .flat_map(|d| d.nodes.iter())
                .flat_map(|n| n.choices.iter())
                .map(|c| c.id),
        )
        .collect()
}

impl GameplayDocument {
    #[must_use]
    pub fn actor_blueprints(&self) -> Option<&ActorBlueprintLibrary> {
        self.creator.as_ref().and_then(|c| c.blueprints.as_ref())
    }

    fn with_actor_library(&self, library: ActorBlueprintLibrary) -> Result<Self, GameplayError> {
        let mut candidate = self.clone();
        library.project(&mut candidate)?;
        let mut config = candidate.creator_configuration();
        let mut required: BTreeSet<_> = config.required_capabilities.iter().copied().collect();
        required.extend(candidate.used_capabilities());
        required.insert(GameplayCapability::Blueprints);
        config.required_capabilities = required.into_iter().collect();
        config.blueprints = Some(library);
        candidate.with_creator_configuration(config)
    }

    /// A new definition starts at version 1; revision requires the next exact version.
    pub fn with_actor_blueprint(
        &self,
        definition: ActorBlueprintDefinition,
    ) -> Result<Self, GameplayError> {
        let mut library = self.actor_blueprints().cloned().unwrap_or_default();
        if let Some(current) = library
            .definitions
            .iter_mut()
            .find(|d| d.id == definition.id)
        {
            if current.version.checked_add(1) != Some(definition.version) {
                return Err(invalid("definition revision must advance by exactly one"));
            }
            *current = definition.clone();
            for binding in library
                .bindings
                .iter_mut()
                .filter(|b| b.blueprint == definition.id)
            {
                binding.blueprint_version = definition.version;
            }
        } else {
            if definition.version != 1 {
                return Err(invalid("new definition must start at version one"));
            }
            library.definitions.push(definition);
        }
        self.with_actor_library(library)
    }

    pub fn with_actor_blueprint_binding(
        &self,
        actor: EntityId,
        blueprint: EntityId,
        overrides: ActorBlueprintOverrides,
    ) -> Result<Self, GameplayError> {
        let mut library = self
            .actor_blueprints()
            .cloned()
            .ok_or_else(|| invalid("no definitions"))?;
        let version = library
            .definitions
            .iter()
            .find(|d| d.id == blueprint)
            .ok_or_else(|| {
                GameplayError::MissingReference(format!(
                    "creator.blueprints.definition/{blueprint}"
                ))
            })?
            .version;
        let binding = ActorBlueprintBinding {
            actor,
            blueprint,
            blueprint_version: version,
            overrides,
        };
        if let Some(current) = library.bindings.iter_mut().find(|b| b.actor == actor) {
            *current = binding;
        } else {
            library.bindings.push(binding);
        }
        self.with_actor_library(library)
    }

    /// Replacing an override set can explicitly restore inheritance with `None`.
    pub fn with_actor_blueprint_overrides(
        &self,
        actor: EntityId,
        overrides: ActorBlueprintOverrides,
    ) -> Result<Self, GameplayError> {
        let mut library = self
            .actor_blueprints()
            .cloned()
            .ok_or_else(|| invalid("no definitions"))?;
        let binding = library
            .bindings
            .iter_mut()
            .find(|b| b.actor == actor)
            .ok_or_else(|| {
                GameplayError::MissingReference(format!("creator.blueprints.actor/{actor}"))
            })?;
        binding.overrides = overrides;
        self.with_actor_library(library)
    }

    /// Detachment keeps the resolved actor values; future revisions no longer affect it.
    pub fn without_actor_blueprint_binding(&self, actor: EntityId) -> Result<Self, GameplayError> {
        let mut library = self
            .actor_blueprints()
            .cloned()
            .ok_or_else(|| invalid("no definitions"))?;
        let index = library
            .bindings
            .iter()
            .position(|b| b.actor == actor)
            .ok_or_else(|| {
                GameplayError::MissingReference(format!("creator.blueprints.actor/{actor}"))
            })?;
        library.bindings.remove(index);
        self.with_actor_library(library)
    }

    /// In-use definitions must be detached explicitly before removal.
    pub fn without_actor_blueprint(&self, blueprint: EntityId) -> Result<Self, GameplayError> {
        let mut library = self
            .actor_blueprints()
            .cloned()
            .ok_or_else(|| invalid("no definitions"))?;
        if library.bindings.iter().any(|b| b.blueprint == blueprint) {
            return Err(invalid(
                "definition is still used by actors; detach them first",
            ));
        }
        let index = library
            .definitions
            .iter()
            .position(|d| d.id == blueprint)
            .ok_or_else(|| {
                GameplayError::MissingReference(format!(
                    "creator.blueprints.definition/{blueprint}"
                ))
            })?;
        library.definitions.remove(index);
        self.with_actor_library(library)
    }
}
