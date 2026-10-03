//! Explicit acquisition identities for import adapters; never a live binding.
//! Only a closed, hash-verified package can create a public ownership index.
use super::{SourceLocalLights, SourceLocalLightsPackage};
use forge_core::ContentHash;
use serde::Deserialize;
use std::collections::{BTreeMap, BTreeSet};

#[derive(Clone, Debug, PartialEq, Eq, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceOwnerReference {
    pub producer_path: String,
    pub producer_function: Option<String>,
    pub collection: Option<String>,
    pub ordinal: Option<usize>,
    pub source_object_id: Option<String>,
    pub anchor: String,
    pub evidence: String,
}

#[derive(Clone, Debug, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceLightOwner {
    pub light_id: ContentHash,
    pub owner_frame: String,
    pub source_binding: SourceOwnerReference,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "Independent acquisition claims are validated, not runtime qualification."
)]
struct Observation {
    schema: String,
    version: u32,
    resolved: Vec<SourceLightOwner>,
    unresolved_light_ids: Vec<ContentHash>,
    source_binding_complete: bool,
    catalogue_binding_executed: bool,
    studio_projection_executed: bool,
}

/// Derived immutable index. Source tokens do not select a module, mesh, or ECS
/// entity. An importer joins them to a verified asset's source identity, then
/// resolves the live placement and each anchor separately.
pub struct SourceLightOwnership {
    package_revision: ContentHash,
    descriptor_revision: ContentHash,
    records: Vec<SourceLightOwner>,
    selections: BTreeMap<String, Vec<ContentHash>>,
    unresolved: Vec<ContentHash>,
}

impl SourceLightOwnership {
    pub fn from_package(package: &SourceLocalLightsPackage) -> Result<Self, String> {
        Self::prepare(
            package.descriptor(),
            package.receipt_hash(),
            package.acquisition(),
        )
    }

    fn prepare(
        descriptor: &SourceLocalLights,
        package_revision: ContentHash,
        acquisition: &serde_json::Value,
    ) -> Result<Self, String> {
        let value = acquisition
            .get("editable_ownership")
            .ok_or("Source ownership observation absent")?;
        let observation: Observation =
            serde_json::from_value(value.clone()).map_err(|error| error.to_string())?;
        let document = descriptor.document();
        if observation.schema != "forge.source-local-light-owner-observation"
            || observation.version != 1
            || observation.catalogue_binding_executed
            || observation.studio_projection_executed
            || observation.source_binding_complete != observation.unresolved_light_ids.is_empty()
            || observation.resolved.len() + observation.unresolved_light_ids.len()
                != document.lights.len()
        {
            return Err("Source ownership envelope differs from acquisition inventory".into());
        }
        let lights: BTreeMap<_, _> = document
            .lights
            .iter()
            .map(|light| (light.id, light))
            .collect();
        let sources: BTreeSet<_> = document
            .source_inputs
            .iter()
            .map(|source| source.path.as_str())
            .collect();
        let mut seen = BTreeSet::new();
        let mut frames = BTreeMap::new();
        let mut selections: BTreeMap<String, Vec<(u64, ContentHash)>> = BTreeMap::new();
        for row in &observation.resolved {
            let light = lights
                .get(&row.light_id)
                .ok_or("Source ownership light is unknown")?;
            let reference = &row.source_binding;
            if !seen.insert(row.light_id)
                || row.owner_frame != light.owner_frame
                || !sources.contains(reference.producer_path.as_str())
                || !token(&reference.anchor)
                || !token(&reference.evidence)
                || reference
                    .producer_function
                    .as_deref()
                    .is_some_and(|value| !token(value))
                || reference
                    .source_object_id
                    .as_deref()
                    .is_some_and(|value| !token(value))
                || reference
                    .collection
                    .as_deref()
                    .is_some_and(|value| !token(value))
                || reference.collection.is_some() != reference.ordinal.is_some()
            {
                return Err("Source ownership identity, frame or producer differs".into());
            }
            if frames
                .insert(row.owner_frame.as_str(), reference)
                .is_some_and(|previous| previous != reference)
            {
                return Err("One source frame has conflicting ownership references".into());
            }
            if let Some(source_id) = &reference.source_object_id {
                selections
                    .entry(source_id.clone())
                    .or_default()
                    .push((light.registration_order, light.id));
            }
        }
        for id in &observation.unresolved_light_ids {
            if !lights.contains_key(id) || !seen.insert(*id) {
                return Err("Unresolved source ownership is unknown or duplicated".into());
            }
        }
        let selections = selections
            .into_iter()
            .map(|(source, mut lights)| {
                lights.sort_by_key(|&(order, _)| order);
                (source, lights.into_iter().map(|(_, id)| id).collect())
            })
            .collect();
        Ok(Self {
            package_revision,
            descriptor_revision: descriptor.content_hash(),
            records: observation.resolved,
            selections,
            unresolved: observation.unresolved_light_ids,
        })
    }

    #[must_use]
    pub const fn package_revision(&self) -> ContentHash {
        self.package_revision
    }

    #[must_use]
    pub const fn descriptor_revision(&self) -> ContentHash {
        self.descriptor_revision
    }

    #[must_use]
    pub fn records(&self) -> &[SourceLightOwner] {
        &self.records
    }

    #[must_use]
    pub fn unresolved_light_ids(&self) -> &[ContentHash] {
        &self.unresolved
    }

    /// Exact, case-sensitive source key. No inference from labels or spatial proximity.
    #[must_use]
    pub fn selection_for_source_object(&self, source_id: &str) -> Option<&[ContentHash]> {
        self.selections.get(source_id).map(Vec::as_slice)
    }
}

fn token(value: &str) -> bool {
    !value.is_empty() && value.len() <= 1024 && !value.chars().any(char::is_control)
}

#[cfg(test)]
mod tests;
