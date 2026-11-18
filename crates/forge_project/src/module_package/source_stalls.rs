//! Area bindings for source stalls and references into the existing fish profiles.
use super::{
    MODULE_SOURCE_STALL_API_VERSION, ModuleFile, ModuleFilePin, ModuleFileRole, ModuleObjectFiles,
    ModuleObjectSource, ModulePackageError, ModulePackageManifest, ModuleRights, refer,
};
use forge_core::{AreaId, AssetId, Transform};
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};

pub const MAX_STALL_DESCRIPTOR_BYTES: usize = 64 * 1024;
const STALL_RESOURCE_COUNT: usize = 14;
const MAX_STALL_BYTES: u64 = 256 * 1024 * 1024;
pub const MAX_STALL_MODEL_BYTES: usize = 64 * 1024 * 1024;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleStallFishAttachment {
    pub asset_id: AssetId,
    /// Pose relative to the authored stall root, in the same units as placements.
    pub local_transform: Transform,
}

/// A self-contained model and its repeating clip, mounted relative to the stall.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleStallAnimatedModel {
    pub model: ModuleFilePin,
    pub license_path: String,
    pub scene_index: u32,
    pub clip_index: u32,
    pub start_seconds: f32,
    pub local_transform: Transform,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleSourceStallBinding {
    pub area_id: AreaId,
    pub asset_id: AssetId,
    /// The descriptor pin is also the revision used by canonical placements.
    pub package: ModuleObjectFiles,
    pub license_paths: Vec<String>,
    /// Fish and local poses from one fish profile in this area.
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub attached_fish: Vec<ModuleStallFishAttachment>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub animated_model: Option<ModuleStallAnimatedModel>,
}

impl ModulePackageManifest {
    pub(super) fn validate_source_stalls<'a>(
        &'a self,
        files: &BTreeMap<&str, &ModuleFile>,
        used: &mut BTreeSet<&'a str>,
    ) -> Result<(), ModulePackageError> {
        if self.source_stalls.is_empty() {
            return Ok(());
        }
        if self.compatibility.module_api_version < MODULE_SOURCE_STALL_API_VERSION {
            return Err(ModulePackageError::Compatibility);
        }
        if self.provenance.rights != ModuleRights::PrivateLocalOnly {
            return Err(ModulePackageError::Reference("source stall redistribution"));
        }
        if self.source_stalls.len() > self.zones.len() {
            return Err(ModulePackageError::Limit("source stall areas"));
        }
        let mut areas = BTreeSet::new();
        for stall in &self.source_stalls {
            if !areas.insert(stall.area_id)
                || !self.zones.iter().any(|zone| zone.area_id == stall.area_id)
            {
                return Err(ModulePackageError::Reference(
                    "orphan or duplicate source stall area",
                ));
            }
            self.validate_stall_identities(stall)?;
            let mut licenses = BTreeSet::new();
            if stall.license_paths.is_empty()
                || stall.license_paths.iter().any(|path| {
                    !licenses.insert(path) || !self.provenance.license_paths.contains(path)
                })
            {
                return Err(ModulePackageError::Reference("source stall licenses"));
            }
            if stall.package.resources.len() != STALL_RESOURCE_COUNT {
                return Err(ModulePackageError::Limit("source stall payload count"));
            }
            let descriptor = &stall.package.manifest;
            let file = refer(files, &descriptor.path, ModuleFileRole::ObjectSource)?;
            if file.bytes > MAX_STALL_DESCRIPTOR_BYTES as u64 {
                return Err(ModulePackageError::Limit("source stall descriptor bytes"));
            }
            let mut paths = BTreeSet::new();
            let mut bytes = 0_u64;
            for pin in std::iter::once(descriptor).chain(&stall.package.resources) {
                let file = refer(files, &pin.path, ModuleFileRole::ObjectSource)?;
                if !paths.insert(pin.path.as_str()) || file.sha256 != pin.sha256 {
                    return Err(ModulePackageError::Reference("source stall resource pin"));
                }
                bytes += file.bytes;
                if bytes > MAX_STALL_BYTES {
                    return Err(ModulePackageError::Limit("source stall payload bytes"));
                }
                used.insert(pin.path.as_str());
            }
            if let Some(animated) = &stall.animated_model {
                let pin = &animated.model;
                let file = refer(files, &pin.path, ModuleFileRole::ObjectSource)?;
                #[expect(
                    clippy::case_sensitive_file_extension_comparisons,
                    reason = "Package paths use the literal lowercase .glb suffix"
                )]
                let is_glb = pin.path.ends_with(".glb");
                if file.bytes > MAX_STALL_MODEL_BYTES as u64
                    || file.sha256 != pin.sha256
                    || !paths.insert(pin.path.as_str())
                    || !is_glb
                    || !stall.license_paths.contains(&animated.license_path)
                    || animated.local_transform.validate().is_err()
                    || !animated.start_seconds.is_finite()
                    || animated.start_seconds < 0.0
                {
                    return Err(ModulePackageError::Reference("source stall animated model"));
                }
                used.insert(pin.path.as_str());
            }
        }
        Ok(())
    }

    fn validate_stall_identities(
        &self,
        stall: &ModuleSourceStallBinding,
    ) -> Result<(), ModulePackageError> {
        let mut fish = self
            .fish_profiles
            .iter()
            .filter(|fish| fish.area_id == stall.area_id);
        if stall.asset_id.as_uuid().is_nil()
            || self
                .assets
                .iter()
                .any(|asset| asset.asset_id == stall.asset_id)
            || self
                .objects
                .iter()
                .filter(|objects| objects.area_id == stall.area_id)
                .any(|objects| {
                    let ModuleObjectSource::SourceSurfaces { assets, .. } = &objects.source;
                    assets.iter().any(|asset| asset.asset_id == stall.asset_id)
                })
            || self.scanned_debris.iter().any(|scans| {
                scans.area_id == stall.area_id
                    && scans.assets.binary_search(&stall.asset_id).is_ok()
            })
            || fish.clone().any(|profile| {
                profile
                    .assets
                    .iter()
                    .any(|asset| asset.asset_id == stall.asset_id)
            })
        {
            return Err(ModulePackageError::Reference("source stall asset identity"));
        }
        if stall.attached_fish.is_empty() {
            return Ok(());
        }
        let mut assets = BTreeSet::new();
        if stall
            .attached_fish
            .iter()
            .any(|fish| fish.local_transform.validate().is_err() || !assets.insert(fish.asset_id))
            || !fish.any(|profile| {
                assets.iter().all(|id| {
                    profile
                        .assets
                        .binary_search_by_key(id, |asset| asset.asset_id)
                        .is_ok()
                })
            })
        {
            return Err(ModulePackageError::Reference(
                "source stall fish attachments",
            ));
        }
        Ok(())
    }
}
