//! Explicit closed object resources; terrain identity never implies object content.
use super::{ModuleFile, ModuleFilePin, ModuleFileRole, ModulePackageError, ModulePackageManifest};
use forge_core::{AreaId, AssetId, ContentHash};
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};

pub const MAX_OBJECT_RESOURCES: usize = 4096;
pub const MAX_MODULE_OBJECT_BYTES: u64 = 128 * 1024 * 1024;

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleObjectFiles {
    pub manifest: ModuleFilePin,
    pub resources: Vec<ModuleFilePin>,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleObjectAsset {
    pub asset_id: AssetId,
    pub sha256: ContentHash,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum ModuleObjectSource {
    SourceSurfaces {
        package: ModuleObjectFiles,
        /// Exact source catalog identities, separate from governed GLB assets.
        assets: Vec<ModuleObjectAsset>,
    },
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleObjectBinding {
    pub area_id: AreaId,
    pub source: ModuleObjectSource,
}

impl ModulePackageManifest {
    pub(super) fn validate_objects<'a>(
        &'a self,
        files: &BTreeMap<&str, &ModuleFile>,
        used: &mut BTreeSet<&'a str>,
    ) -> Result<(), ModulePackageError> {
        if !self.objects.is_empty()
            && !(super::MODULE_OBJECTS_API_VERSION..=super::MODULE_HISTORICAL_MODELS_API_VERSION)
                .contains(&self.compatibility.module_api_version)
        {
            return Err(ModulePackageError::Compatibility);
        }
        if self.objects.len() > self.zones.len() {
            return Err(ModulePackageError::Limit("object areas"));
        }
        let governed: BTreeSet<_> = self.assets.iter().map(|asset| asset.asset_id).collect();
        let mut areas = BTreeSet::new();
        let mut unique = BTreeSet::new();
        for binding in &self.objects {
            if !areas.insert(binding.area_id)
                || !self
                    .zones
                    .iter()
                    .any(|zone| zone.area_id == binding.area_id)
            {
                return Err(ModulePackageError::Reference(
                    "orphan or duplicate object area",
                ));
            }
            let ModuleObjectSource::SourceSurfaces { package, assets } = &binding.source;
            if assets.is_empty()
                || assets.len() > super::MAX_MODULE_ASSETS
                || assets
                    .windows(2)
                    .any(|pair| pair[0].asset_id >= pair[1].asset_id)
                || assets
                    .iter()
                    .any(|asset| governed.contains(&asset.asset_id))
            {
                return Err(ModulePackageError::Reference(
                    "source object asset identities",
                ));
            }
            if package.resources.is_empty() || package.resources.len() > MAX_OBJECT_RESOURCES {
                return Err(ModulePackageError::Limit("object resources"));
            }
            let mut local = BTreeSet::new();
            for (pin, license_allowed) in std::iter::once((&package.manifest, false))
                .chain(package.resources.iter().map(|pin| (pin, true)))
            {
                if !local.insert(pin.path.as_str()) {
                    return Err(ModulePackageError::Duplicate);
                }
                let file = files
                    .get(pin.path.as_str())
                    .copied()
                    .ok_or(ModulePackageError::Reference("missing object file"))?;
                if file.sha256 != pin.sha256
                    || !(file.role == ModuleFileRole::ObjectSource
                        || (license_allowed
                            && file.role == ModuleFileRole::License
                            && self.provenance.license_paths.contains(&pin.path)))
                {
                    return Err(ModulePackageError::Reference("object file role or pin"));
                }
                used.insert(&pin.path);
                unique.insert(pin.path.as_str());
            }
        }
        if unique.len() > MAX_OBJECT_RESOURCES {
            return Err(ModulePackageError::Limit("module object resources"));
        }
        let mut bytes = 0_u64;
        for path in unique {
            bytes = bytes
                .checked_add(files[path].bytes)
                .ok_or(ModulePackageError::Limit("object bytes"))?;
            if bytes > MAX_MODULE_OBJECT_BYTES {
                return Err(ModulePackageError::Limit("object bytes"));
            }
        }
        Ok(())
    }
}
