//! One shared rule/surface definition; blueprint libraries remain local to each area.
use crate::{CreatorSurface, GameplayCapability, GameplayDocument, GameplayError, RulesProfile};
use forge_core::{ContentHash, ModuleId, bytes_sha256, canonical_json_bytes};
use serde::{Deserialize, Serialize};
use std::collections::BTreeSet;

pub const MODULE_CREATOR_SCHEMA: &str = "forge.module-creator";
pub const MODULE_CREATOR_VERSION: u32 = 1;
pub const MAX_MODULE_CREATOR_BYTES: usize = 16 * 1024;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleCreatorProfile {
    pub schema: String,
    pub version: u32,
    pub module_id: ModuleId,
    pub profile: RulesProfile,
    pub required_capabilities: Vec<GameplayCapability>,
    pub surfaces: Vec<CreatorSurface>,
}

fn invalid(reason: &str) -> GameplayError {
    GameplayError::InvalidDocument(format!("module creator: {reason}"))
}

impl ModuleCreatorProfile {
    /// Explicit shared settings may replace differing legacy area profiles.
    /// Requirements retain all data, including data behind hidden surfaces.
    pub fn for_documents(
        module_id: ModuleId,
        profile: RulesProfile,
        surfaces: Vec<CreatorSurface>,
        documents: &[&GameplayDocument],
    ) -> Result<Self, GameplayError> {
        let mut required = BTreeSet::from([GameplayCapability::Actors]);
        for document in documents {
            document.validate()?;
            if document.module_id != module_id {
                return Err(invalid("area belongs to another module"));
            }
            required.extend(document.used_capabilities());
        }
        required.extend(surfaces.iter().filter_map(|surface| surface.capability()));
        let definition = Self {
            schema: MODULE_CREATOR_SCHEMA.into(),
            version: MODULE_CREATOR_VERSION,
            module_id,
            profile,
            required_capabilities: required.into_iter().collect(),
            surfaces,
        };
        definition.validate()?;
        Ok(definition)
    }

    pub fn validate(&self) -> Result<(), GameplayError> {
        if self.schema != MODULE_CREATOR_SCHEMA || self.version != MODULE_CREATOR_VERSION {
            return Err(invalid("unsupported schema/version"));
        }
        self.profile.validate()?;
        let required: BTreeSet<_> = self.required_capabilities.iter().copied().collect();
        let surfaces: BTreeSet<_> = self.surfaces.iter().copied().collect();
        if required.len() != self.required_capabilities.len()
            || surfaces.len() != self.surfaces.len()
        {
            return Err(invalid("duplicate capability/surface"));
        }
        if required
            .iter()
            .any(|capability| !capability.is_implemented())
        {
            return Err(invalid("capability is not implemented by this runtime"));
        }
        let mut needed = BTreeSet::from([GameplayCapability::Actors]);
        needed.extend(surfaces.iter().filter_map(|surface| surface.capability()));
        if !needed.is_subset(&required) {
            return Err(invalid("required capability is missing"));
        }
        Ok(())
    }

    /// Materialize through the ordinary zone command; never mutate a source in place.
    pub fn apply_to(&self, document: &GameplayDocument) -> Result<GameplayDocument, GameplayError> {
        self.validate()?;
        document.validate()?;
        if document.module_id != self.module_id {
            return Err(invalid("area belongs to another module"));
        }
        let mut candidate = document.clone();
        candidate.profile.clone_from(&self.profile);
        let mut configuration = candidate.creator_configuration();
        configuration
            .required_capabilities
            .clone_from(&self.required_capabilities);
        configuration.surfaces.clone_from(&self.surfaces);
        configuration.required_capabilities.sort_unstable();
        configuration.surfaces.sort_unstable();
        candidate.with_creator_configuration(configuration)
    }

    /// Admission and storage require exact shared values; local blueprints are preserved.
    pub fn validate_document(&self, document: &GameplayDocument) -> Result<(), GameplayError> {
        self.validate()?;
        document.validate()?;
        if document.module_id != self.module_id || document.profile != self.profile {
            return Err(invalid(
                "area rules or module identity differ from the shared definition",
            ));
        }
        let configuration = document
            .creator
            .as_ref()
            .ok_or_else(|| invalid("shared configuration is not materialized in this area"))?;
        let required: BTreeSet<_> = self.required_capabilities.iter().collect();
        let actual: BTreeSet<_> = configuration.required_capabilities.iter().collect();
        let surfaces: BTreeSet<_> = self.surfaces.iter().collect();
        let actual_surfaces: BTreeSet<_> = configuration.surfaces.iter().collect();
        if required != actual || surfaces != actual_surfaces {
            return Err(invalid(
                "area capabilities/surfaces differ from the shared definition",
            ));
        }
        Ok(())
    }

    pub fn to_bytes(&self) -> Result<Vec<u8>, GameplayError> {
        self.validate()?;
        let mut canonical = self.clone();
        canonical.required_capabilities.sort_unstable();
        canonical.surfaces.sort_unstable();
        let bytes = canonical_json_bytes(&canonical)
            .map_err(|error| GameplayError::Codec(error.to_string()))?;
        if bytes.len() > MAX_MODULE_CREATOR_BYTES {
            return Err(invalid("byte budget exceeded"));
        }
        Ok(bytes)
    }

    pub fn from_bytes(bytes: &[u8]) -> Result<Self, GameplayError> {
        if bytes.len() > MAX_MODULE_CREATOR_BYTES {
            return Err(invalid("byte budget exceeded"));
        }
        let definition: Self = serde_json::from_slice(bytes)
            .map_err(|error| GameplayError::Codec(error.to_string()))?;
        definition.validate()?;
        Ok(definition)
    }

    pub fn content_hash(&self) -> Result<ContentHash, GameplayError> {
        Ok(bytes_sha256(&self.to_bytes()?))
    }
}
