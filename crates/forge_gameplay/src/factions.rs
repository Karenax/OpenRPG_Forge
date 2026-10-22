//! Local faction definitions and observation records; no global reputation state.
//! Authored baselines remain separate from each witness's persistent opinion.
use crate::{GameplayDocument, GameplayError};
use forge_core::{CommandId, EntityId, Vec3};
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};

pub const MAX_FACTIONS: usize = 32;
pub const MAX_FACTION_WITNESSES: usize = 16;
pub const MAX_PERSONAL_RELATIONS: usize = 1024;
pub const MIN_RELATION: i16 = -100;
pub const MAX_RELATION: i16 = 100;
pub const MAX_PERCEPTION_RANGE_M: f32 = 50.0;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct FactionDefinition {
    pub id: EntityId,
    pub name: String,
    pub perception_range_m: f32,
    pub protects_factions: Vec<EntityId>,
    /// Points applied to this witness's opinion after a visible direct assault.
    pub assault_delta: i16,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct FactionRelation {
    pub from: EntityId,
    pub to: EntityId,
    pub value: i16,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct FactionsDefinition {
    pub version: u32,
    pub factions: Vec<FactionDefinition>,
    pub memberships: BTreeMap<EntityId, EntityId>,
    pub relations: Vec<FactionRelation>,
}

/// Trusted engine observation, like the existing contact and visibility inputs.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct FactionWitnessObservation {
    pub observer: EntityId,
    pub line_of_sight: bool,
}

/// Accepted physical snapshot retained with the originating action, not live belief.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct FactionWitnessReceipt {
    pub observer: EntityId,
    pub observer_position: Vec3,
    pub aggressor_position: Vec3,
    pub victim_position: Vec3,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct FactionConsequence {
    pub observer: EntityId,
    pub subject: EntityId,
    pub previous: i16,
    pub current: i16,
    pub cause: CommandId,
}

#[derive(Clone, Debug, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct FactionsState {
    pub opinions: BTreeMap<EntityId, BTreeMap<EntityId, i16>>,
}

fn invalid(reason: impl Into<String>) -> GameplayError {
    GameplayError::InvalidDocument(format!("factions: {}", reason.into()))
}

impl FactionsDefinition {
    #[must_use]
    pub fn faction(&self, id: EntityId) -> Option<&FactionDefinition> {
        self.factions.iter().find(|faction| faction.id == id)
    }

    #[must_use]
    pub fn actor_faction(&self, actor: EntityId) -> Option<&FactionDefinition> {
        self.faction(*self.memberships.get(&actor)?)
    }

    #[must_use]
    pub fn base_relation(&self, observer: EntityId, subject: EntityId) -> i16 {
        let (Some(from), Some(to)) = (
            self.memberships.get(&observer),
            self.memberships.get(&subject),
        ) else {
            return 0;
        };
        self.relations
            .iter()
            .find(|relation| relation.from == *from && relation.to == *to)
            .map_or(0, |relation| relation.value)
    }

    pub(crate) fn validate(
        &self,
        document: &GameplayDocument,
        identities: &mut BTreeSet<EntityId>,
    ) -> Result<(), GameplayError> {
        if self.version != 1
            || self.factions.is_empty()
            || self.factions.len() > MAX_FACTIONS
            || self.memberships.len() > crate::MAX_GAMEPLAY_ENTRIES
            || self.relations.len() > MAX_FACTIONS * MAX_FACTIONS
        {
            return Err(invalid("unsupported version or definition quota exceeded"));
        }
        for faction in &self.factions {
            if !identities.insert(faction.id) {
                return Err(invalid(format!(
                    "duplicate faction identity {}",
                    faction.id
                )));
            }
            if faction.name.trim().is_empty()
                || faction.name.len() > 256
                || !faction.perception_range_m.is_finite()
                || !(0.0..=MAX_PERCEPTION_RANGE_M).contains(&faction.perception_range_m)
                || !(MIN_RELATION..=0).contains(&faction.assault_delta)
                || faction.protects_factions.len() > MAX_FACTIONS
            {
                return Err(invalid(format!("invalid faction bounds {}", faction.id)));
            }
            let mut protected = BTreeSet::new();
            for group in &faction.protects_factions {
                if !protected.insert(*group) || self.faction(*group).is_none() {
                    return Err(invalid(format!("invalid protected faction {group}")));
                }
            }
        }
        for (actor, group) in &self.memberships {
            if document.actor(*actor).is_none() || self.faction(*group).is_none() {
                return Err(GameplayError::MissingReference(format!(
                    "faction membership {actor}/{group}"
                )));
            }
        }
        let mut relations = BTreeSet::new();
        for relation in &self.relations {
            if self.faction(relation.from).is_none()
                || self.faction(relation.to).is_none()
                || !relations.insert((relation.from, relation.to))
                || !(MIN_RELATION..=MAX_RELATION).contains(&relation.value)
            {
                return Err(invalid(format!(
                    "invalid faction relation {}/{}",
                    relation.from, relation.to
                )));
            }
        }
        Ok(())
    }
}

impl FactionDefinition {
    /// Shared geometric bound; physical visibility is a separate engine observation.
    #[must_use]
    pub fn positions_in_perception_range(
        &self,
        observer: Vec3,
        aggressor: Vec3,
        victim: Vec3,
    ) -> bool {
        let contains = |target: Vec3| {
            (observer.x - target.x).powi(2)
                + (observer.y - target.y).powi(2)
                + (observer.z - target.z).powi(2)
                <= self.perception_range_m.powi(2)
        };
        contains(aggressor) && contains(victim)
    }
}

impl FactionsState {
    #[must_use]
    pub fn personal_relation(
        &self,
        definitions: &FactionsDefinition,
        observer: EntityId,
        subject: EntityId,
    ) -> i16 {
        self.opinions
            .get(&observer)
            .and_then(|opinions| opinions.get(&subject))
            .copied()
            .unwrap_or_else(|| definitions.base_relation(observer, subject))
    }

    #[must_use]
    pub fn modified_relations(&self) -> usize {
        self.opinions.values().map(BTreeMap::len).sum()
    }
}
