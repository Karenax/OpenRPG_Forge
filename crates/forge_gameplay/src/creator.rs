//! Capability declarations and authoring presentation; never a second game state.
use crate::{ActorRole, GameplayDocument, GameplayError, ObjectKind};
use serde::{Deserialize, Serialize};
use std::collections::BTreeSet;

pub const CREATOR_SCHEMA: &str = "forge.creator-configuration";
pub const CREATOR_VERSION: u32 = 1;
pub const GAMEPLAY_CREATOR_VERSION: u32 = 2;

#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum GameplayCapability {
    Actors,
    Inventory,
    Dialogue,
    Quests,
    Places,
    Combat,
    PhysicalInteractions,
    Factions,
    Blueprints,
    Containers,
    AbilitiesAndEffects,
    Cinematics,
    Calendar,
    Encounters,
    SandboxedScripts,
}

impl GameplayCapability {
    pub const IMPLEMENTED: [Self; 15] = [
        Self::Actors,
        Self::Inventory,
        Self::Dialogue,
        Self::Quests,
        Self::Places,
        Self::Combat,
        Self::PhysicalInteractions,
        Self::Blueprints,
        Self::Containers,
        Self::Factions,
        Self::AbilitiesAndEffects,
        Self::Calendar,
        Self::Encounters,
        Self::Cinematics,
        Self::SandboxedScripts,
    ];

    #[must_use]
    pub fn is_implemented(self) -> bool {
        Self::IMPLEMENTED.contains(&self)
    }

    #[must_use]
    pub const fn label(self) -> &'static str {
        match self {
            Self::Actors => "Personnages",
            Self::Inventory => "Objets et inventaire",
            Self::Dialogue => "Dialogues conditionnels",
            Self::Quests => "Quêtes de récupération",
            Self::Places => "Lieux et liaisons",
            Self::Combat => "Combat physique",
            Self::PhysicalInteractions => "Interactions physiques",
            Self::Factions => "Factions",
            Self::Blueprints => "Blueprints",
            Self::Containers => "Conteneurs",
            Self::AbilitiesAndEffects => "Capacités et effets",
            Self::Cinematics => "Cinématiques",
            Self::Calendar => "Calendrier",
            Self::Encounters => "Rencontres",
            Self::SandboxedScripts => "Scripts sandboxés",
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum CreatorSurface {
    Actors,
    Objects,
    Dialogues,
    Quests,
    Places,
    Rules,
    Blueprints,
    Containers,
    Factions,
    AbilitiesAndEffects,
    Calendar,
    Encounters,
    Cinematics,
    SandboxedScripts,
}

impl CreatorSurface {
    pub const ALL: [Self; 14] = [
        Self::Actors,
        Self::Objects,
        Self::Dialogues,
        Self::Quests,
        Self::Places,
        Self::Rules,
        Self::Blueprints,
        Self::Containers,
        Self::Factions,
        Self::AbilitiesAndEffects,
        Self::Calendar,
        Self::Encounters,
        Self::Cinematics,
        Self::SandboxedScripts,
    ];

    #[must_use]
    pub const fn label(self) -> &'static str {
        match self {
            Self::Actors => "Personnages",
            Self::Objects => "Objets et mécanismes",
            Self::Dialogues => "Dialogues",
            Self::Quests => "Quêtes",
            Self::Places => "Lieux",
            Self::Rules => "Profil de règles",
            Self::Blueprints => "Blueprints de personnages",
            Self::Containers => "Conteneurs",
            Self::Factions => "Factions et relations locales",
            Self::AbilitiesAndEffects => "Capacités et effets",
            Self::Calendar => "Horloge et calendrier",
            Self::Encounters => "Rencontres planifiées",
            Self::Cinematics => "Séquences et dialogues cinématiques",
            Self::SandboxedScripts => "Scripts et permissions",
        }
    }

    #[must_use]
    pub const fn capability(self) -> Option<GameplayCapability> {
        match self {
            Self::Actors => Some(GameplayCapability::Actors),
            Self::Objects => Some(GameplayCapability::Inventory),
            Self::Dialogues => Some(GameplayCapability::Dialogue),
            Self::Quests => Some(GameplayCapability::Quests),
            Self::Places => Some(GameplayCapability::Places),
            Self::Rules => None,
            Self::Blueprints => Some(GameplayCapability::Blueprints),
            Self::Containers => Some(GameplayCapability::Containers),
            Self::Factions => Some(GameplayCapability::Factions),
            Self::AbilitiesAndEffects => Some(GameplayCapability::AbilitiesAndEffects),
            Self::Calendar => Some(GameplayCapability::Calendar),
            Self::Encounters => Some(GameplayCapability::Encounters),
            Self::Cinematics => Some(GameplayCapability::Cinematics),
            Self::SandboxedScripts => Some(GameplayCapability::SandboxedScripts),
        }
    }
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct CreatorConfiguration {
    pub schema: String,
    pub version: u32,
    pub required_capabilities: Vec<GameplayCapability>,
    pub surfaces: Vec<CreatorSurface>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub blueprints: Option<crate::ActorBlueprintLibrary>,
}

impl GameplayDocument {
    #[must_use]
    pub fn used_capabilities(&self) -> BTreeSet<GameplayCapability> {
        let mut used = BTreeSet::from([GameplayCapability::Actors]);
        if self.scripting.is_some() {
            used.insert(GameplayCapability::SandboxedScripts);
        }
        if self.cinematics.is_some() {
            used.insert(GameplayCapability::Cinematics);
        }
        if let Some(simulation) = &self.simulation {
            used.insert(GameplayCapability::Calendar);
            if !simulation.abilities.is_empty()
                || !simulation.effects.is_empty()
                || !simulation.actor_abilities.is_empty()
            {
                used.insert(GameplayCapability::AbilitiesAndEffects);
            }
            if !simulation.encounters.is_empty() {
                used.insert(GameplayCapability::Encounters);
            }
        }
        if self.factions.is_some() {
            used.insert(GameplayCapability::Factions);
        }
        if let Some(narrative) = &self.narrative {
            if !narrative.variables.is_empty() {
                used.insert(GameplayCapability::Dialogue);
            }
            if !narrative.quests.is_empty() {
                used.insert(GameplayCapability::Quests);
            }
        }
        if self.has_containers() {
            used.insert(GameplayCapability::Containers);
            used.insert(GameplayCapability::Inventory);
        }
        if self.actor_blueprints().is_some() {
            used.insert(GameplayCapability::Blueprints);
        }
        if !self.items.is_empty()
            || self
                .objects
                .iter()
                .any(|o| matches!(o.kind, ObjectKind::Pickup { .. }))
        {
            used.insert(GameplayCapability::Inventory);
        }
        if !self.dialogues.is_empty() {
            used.insert(GameplayCapability::Dialogue);
        }
        if !self.quests.is_empty() {
            used.insert(GameplayCapability::Quests);
        }
        if !self.places.is_empty() {
            used.insert(GameplayCapability::Places);
        }
        if self.actors.iter().any(|a| a.role == ActorRole::Enemy) {
            used.insert(GameplayCapability::Combat);
        }
        if self
            .objects
            .iter()
            .any(|o| !matches!(o.kind, ObjectKind::Pickup { .. }))
        {
            used.insert(GameplayCapability::PhysicalInteractions);
        }
        used
    }

    /// Requirements include data on hidden surfaces; hiding never deletes it.
    #[must_use]
    pub fn creator_configuration(&self) -> CreatorConfiguration {
        self.creator
            .clone()
            .unwrap_or_else(|| CreatorConfiguration {
                schema: CREATOR_SCHEMA.into(),
                version: CREATOR_VERSION,
                required_capabilities: GameplayCapability::IMPLEMENTED.to_vec(),
                surfaces: CreatorSurface::ALL.to_vec(),
                blueprints: None,
            })
    }

    #[must_use]
    pub fn surface_visible(&self, surface: CreatorSurface) -> bool {
        self.creator
            .as_ref()
            .is_none_or(|configuration| configuration.surfaces.contains(&surface))
    }

    pub(crate) fn validate_creator(&self) -> Result<(), GameplayError> {
        let Some(config) = &self.creator else {
            return Ok(());
        };
        let invalid =
            |message: String| GameplayError::InvalidDocument(format!("creator: {message}"));
        if config.schema != CREATOR_SCHEMA || config.version != CREATOR_VERSION {
            return Err(invalid("unsupported schema/version".into()));
        }
        let required: BTreeSet<_> = config.required_capabilities.iter().copied().collect();
        let surfaces: BTreeSet<_> = config.surfaces.iter().copied().collect();
        if required.len() != config.required_capabilities.len()
            || surfaces.len() != config.surfaces.len()
        {
            return Err(invalid("duplicate capability/surface".into()));
        }
        for capability in &required {
            if !capability.is_implemented() {
                return Err(invalid(format!(
                    "capability {capability:?} is not implemented by this runtime"
                )));
            }
        }
        let mut needed = self.used_capabilities();
        needed.extend(surfaces.iter().filter_map(|s| s.capability()));
        if let Some(missing) = needed.difference(&required).next() {
            return Err(invalid(format!(
                "required_capabilities lacks {missing:?}; existing content is preserved, including hidden surfaces"
            )));
        }
        if let Some(library) = &config.blueprints {
            library.validate(self)?;
        }
        Ok(())
    }

    /// Return a validated candidate; callers promote it through their normal command bus.
    pub fn with_creator_configuration(
        &self,
        config: CreatorConfiguration,
    ) -> Result<Self, GameplayError> {
        let mut candidate = self.clone();
        candidate.version = GAMEPLAY_CREATOR_VERSION;
        candidate.creator = Some(config);
        candidate.validate()?;
        Ok(candidate)
    }
}
