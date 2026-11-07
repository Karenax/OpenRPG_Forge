//! Additive content proposals; existing definitions and module rules stay owned
//! by the canonical authoring document, never by provider output.
use super::selection::SelectionScope;
use crate::zone_authoring::{ZoneAuthoringHead, session::ZoneRequest};
use forge_core::{CommandId, EntityId, Vec3};
use forge_gameplay::{
    ActorDefinition, ActorRole, CreatorSurface, DialogueDefinition, GameplayCapability,
    GameplayDocument, ItemDefinition, NarrativeDefinition, ObjectDefinition, PlaceDefinition,
    QuestDefinition, RulesProfile,
};
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};

pub mod quest;
pub(crate) mod schema;

#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct CreationContext {
    pub player_id: EntityId,
    pub rules: RulesProfile,
    pub surfaces: Vec<CreatorSurface>,
    pub capabilities: Vec<GameplayCapability>,
    pub narrative_available: bool,
}

impl CreationContext {
    #[must_use]
    pub fn new(document: &GameplayDocument) -> Self {
        let creator = document.creator_configuration();
        Self {
            player_id: document.player,
            rules: document.profile.clone(),
            surfaces: creator.surfaces,
            capabilities: creator.required_capabilities,
            narrative_available: document.creator.is_some(),
        }
    }
}

/// Canonical definition types, carried only as an untrusted, bounded addition.
/// No player, profile, module identity, script, existing-entry edit or asset path.
#[derive(Clone, Debug, Default, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ContentAddition {
    pub actors: Vec<ActorDefinition>,
    pub items: Vec<ItemDefinition>,
    pub objects: Vec<ObjectDefinition>,
    pub places: Vec<PlaceDefinition>,
    pub dialogues: Vec<DialogueDefinition>,
    pub quests: Vec<QuestDefinition>,
    pub narrative: Option<NarrativeDefinition>,
}

/// Local keys are proposal data; only the native compiler allocates source IDs.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PlaceDraft {
    pub key: String,
    pub name: String,
    pub position: Vec3,
    pub links: Vec<String>,
}

pub(crate) fn valid_key(value: &str) -> bool {
    !value.is_empty()
        && value.len() <= 64
        && value
            .bytes()
            .all(|byte| byte.is_ascii_alphanumeric() || matches!(byte, b'_' | b'-'))
}

pub(crate) fn expand_places(
    places: &[PlaceDraft],
    command: CommandId,
) -> Result<ContentAddition, String> {
    if places.is_empty() || places.len() > 32 {
        return Err("Proposer entre un et 32 lieux à créer.".into());
    }
    let mut identities = BTreeMap::new();
    for place in places {
        if !valid_key(&place.key)
            || identities
                .insert(
                    place.key.as_str(),
                    EntityId::deterministic(format!(
                        "forge/assistant/{command}/place/{}",
                        place.key
                    )),
                )
                .is_some()
        {
            return Err(
                "Chaque nouveau lieu exige une clé ASCII distincte de 1 à 64 caractères.".into(),
            );
        }
    }
    let mut definitions = Vec::new();
    for place in places {
        if place.links.len() > 32
            || place.links.iter().collect::<BTreeSet<_>>().len() != place.links.len()
        {
            return Err(
                "Les liaisons de chaque nouveau lieu doivent être distinctes et bornées à 32."
                    .into(),
            );
        }
        let links = place
            .links
            .iter()
            .map(|key| {
                identities.get(key.as_str()).copied().ok_or_else(|| {
                    "Une liaison doit cibler la clé d’un lieu de cette proposition.".to_owned()
                })
            })
            .collect::<Result<Vec<_>, _>>()?;
        definitions.push(PlaceDefinition {
            id: identities[place.key.as_str()],
            name: place.name.clone(),
            position: place.position,
            links,
        });
    }
    Ok(ContentAddition {
        places: definitions,
        ..ContentAddition::default()
    })
}

impl ContentAddition {
    pub fn compile(
        &self,
        head: &ZoneAuthoringHead,
        scope: &SelectionScope,
        command: CommandId,
    ) -> Result<ZoneRequest, String> {
        if scope.context().creation.is_none() {
            return Err("La création de contenu RPG n’a pas été autorisée.".into());
        }
        let source =
            head.corrections.gameplay.as_deref().ok_or(
                "Initialiser le contenu RPG dans le Studio avant de proposer une aventure.",
            )?;
        self.check_budget()?;
        self.check_surfaces(source)?;
        if self.actors.iter().any(|actor| {
            actor.role == ActorRole::Player
                || !scope.context().region.contains(actor.position)
                || actor
                    .patrol
                    .iter()
                    .any(|point| !scope.context().region.contains(*point))
        }) || self
            .objects
            .iter()
            .any(|object| !scope.context().region.contains(object.position))
            || self
                .places
                .iter()
                .any(|place| !scope.context().region.contains(place.position))
        {
            return Err("Les nouveaux éléments doivent rester dans la région ; le joueur existant est conservé.".into());
        }
        // Validate references in isolation: an addition can link its own new
        // entries and the shared player, never hidden pre-existing definitions.
        let mut isolated = source.clone();
        let mut player = source.actor(source.player).ok_or("Joueur absent")?.clone();
        player.dialogue = None;
        isolated.actors = vec![player];
        isolated.items.clear();
        isolated.objects.clear();
        isolated.places.clear();
        isolated.dialogues.clear();
        isolated.quests.clear();
        isolated.narrative = None;
        isolated.factions = None;
        isolated.simulation = None;
        isolated.scripting = None;
        isolated.cinematics = None;
        if let Some(creator) = &mut isolated.creator {
            creator.blueprints = None;
        }
        self.append_to(&mut isolated)?;
        isolated.validate().map_err(|error| error.to_string())?;
        let mut candidate = source.clone();
        self.append_to(&mut candidate)?;
        // Duplicate identities (including nested IDs), incompatible profiles,
        // foreign references and invalid dialogue/quest graphs fail atomically.
        head.prepare_gameplay(Some(candidate), command)
    }

    fn check_budget(&self) -> Result<(), String> {
        let mut count = self.actors.len()
            + self.items.len()
            + self.objects.len()
            + self.places.len()
            + self.dialogues.len()
            + self.quests.len();
        count += self
            .dialogues
            .iter()
            .map(|dialogue| {
                dialogue.nodes.len()
                    + dialogue
                        .nodes
                        .iter()
                        .map(|node| node.choices.len())
                        .sum::<usize>()
            })
            .sum::<usize>();
        if let Some(narrative) = &self.narrative {
            count += narrative.variables.len()
                + narrative.quests.len()
                + narrative
                    .quests
                    .iter()
                    .map(|flow| flow.solutions.len())
                    .sum::<usize>();
        }
        if count == 0 || count > 128 {
            return Err(
                "Une proposition de création doit contenir entre 1 et 128 définitions.".into(),
            );
        }
        Ok(())
    }

    fn check_surfaces(&self, document: &GameplayDocument) -> Result<(), String> {
        for (surface, needed) in [
            (CreatorSurface::Actors, !self.actors.is_empty()),
            (
                CreatorSurface::Objects,
                !self.items.is_empty() || !self.objects.is_empty(),
            ),
            (CreatorSurface::Places, !self.places.is_empty()),
            (
                CreatorSurface::Dialogues,
                !self.dialogues.is_empty()
                    || self
                        .narrative
                        .as_ref()
                        .is_some_and(|n| !n.variables.is_empty()),
            ),
            (
                CreatorSurface::Quests,
                !self.quests.is_empty()
                    || self
                        .narrative
                        .as_ref()
                        .is_some_and(|n| !n.quests.is_empty()),
            ),
        ] {
            if needed && !document.surface_visible(surface) {
                return Err(format!(
                    "Activer la surface {} dans le Creator avant sa création assistée.",
                    surface.label()
                ));
            }
        }
        Ok(())
    }

    fn append_to(&self, document: &mut GameplayDocument) -> Result<(), String> {
        document.actors.extend(self.actors.iter().cloned());
        document.items.extend(self.items.iter().cloned());
        document.objects.extend(self.objects.iter().cloned());
        document.places.extend(self.places.iter().cloned());
        document.dialogues.extend(self.dialogues.iter().cloned());
        document.quests.extend(self.quests.iter().cloned());
        if let Some(addition) = &self.narrative {
            if addition.version != 1 || document.creator.is_none() {
                return Err("Configurer les capacités du Creator pour créer des quêtes à solutions multiples ; le plan ne peut pas changer les règles.".into());
            }
            let narrative = document
                .narrative
                .get_or_insert_with(|| NarrativeDefinition {
                    version: 1,
                    variables: vec![],
                    quests: vec![],
                });
            narrative
                .variables
                .extend(addition.variables.iter().cloned());
            narrative.quests.extend(addition.quests.iter().cloned());
        }
        Ok(())
    }
}
