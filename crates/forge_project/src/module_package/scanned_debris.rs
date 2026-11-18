//! Explicit per-area resources of the original scanned-debris package format.
use super::{
    MODULE_SCANNED_DEBRIS_API_VERSION, ModuleFile, ModuleFilePin, ModuleFileRole,
    ModuleObjectFiles, ModuleObjectSource, ModulePackageError, ModulePackageManifest, ModuleRights,
};
use forge_core::{AreaId, AssetId};
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleScannedDebrisBinding {
    pub area_id: AreaId,
    pub package: ModuleObjectFiles,
    pub instance_program: ModuleFilePin,
    pub license_path: String,
    /// Canonically ordered variation identities; their revision is the package manifest hash.
    pub assets: Vec<AssetId>,
}

impl ModulePackageManifest {
    pub(super) fn validate_scanned_debris<'a>(
        &'a self,
        files: &BTreeMap<&str, &ModuleFile>,
        used: &mut BTreeSet<&'a str>,
    ) -> Result<(), ModulePackageError> {
        if self.scanned_debris.is_empty() {
            return Ok(());
        }
        if self.compatibility.module_api_version < MODULE_SCANNED_DEBRIS_API_VERSION {
            return Err(ModulePackageError::Compatibility);
        }
        if self.provenance.rights != ModuleRights::PrivateLocalOnly {
            return Err(ModulePackageError::Reference(
                "acquired scan redistribution",
            ));
        }
        if self.scanned_debris.len() > self.zones.len() {
            return Err(ModulePackageError::Limit("scanned debris areas"));
        }
        let governed: BTreeSet<_> = self.assets.iter().map(|asset| asset.asset_id).collect();
        let mut areas = BTreeSet::new();
        for binding in &self.scanned_debris {
            if !areas.insert(binding.area_id)
                || !self
                    .zones
                    .iter()
                    .any(|zone| zone.area_id == binding.area_id)
            {
                return Err(ModulePackageError::Reference(
                    "orphan or duplicate scanned debris area",
                ));
            }
            self.validate_scanned_assets(binding, &governed)?;
            if !self
                .provenance
                .license_paths
                .contains(&binding.license_path)
            {
                return Err(ModulePackageError::Reference("scanned debris license"));
            }
            if binding.package.resources.is_empty() || binding.package.resources.len() > 4096 {
                return Err(ModulePackageError::Limit("scanned debris payload count"));
            }
            let mut paths = BTreeSet::new();
            let mut bytes = 0_u64;
            for pin in [&binding.package.manifest, &binding.instance_program]
                .into_iter()
                .chain(&binding.package.resources)
            {
                let file = super::refer(files, &pin.path, ModuleFileRole::ScannedDebris)?;
                if !paths.insert(pin.path.as_str()) || file.sha256 != pin.sha256 {
                    return Err(ModulePackageError::Reference("scanned debris resource pin"));
                }
                bytes += file.bytes;
                if bytes > 81 * 1024 * 1024 {
                    return Err(ModulePackageError::Limit("scanned debris payload bytes"));
                }
                used.insert(pin.path.as_str());
            }
        }
        Ok(())
    }

    fn validate_scanned_assets(
        &self,
        binding: &ModuleScannedDebrisBinding,
        governed: &BTreeSet<AssetId>,
    ) -> Result<(), ModulePackageError> {
        let other = self
            .objects
            .iter()
            .filter(|objects| objects.area_id == binding.area_id)
            .flat_map(|objects| match &objects.source {
                ModuleObjectSource::SourceSurfaces { assets, .. } => assets.iter(),
            })
            .chain(
                self.fish_profiles
                    .iter()
                    .filter(|fish| fish.area_id == binding.area_id)
                    .flat_map(|fish| fish.assets.iter()),
            );
        if binding.assets.is_empty()
            || binding.assets.len() > 16_384
            || binding.assets.windows(2).any(|pair| pair[0] >= pair[1])
            || binding
                .assets
                .iter()
                .any(|id| id.as_uuid().is_nil() || governed.contains(id))
            || other
                .into_iter()
                .any(|asset| binding.assets.binary_search(&asset.asset_id).is_ok())
        {
            return Err(ModulePackageError::Reference(
                "scanned debris asset identity",
            ));
        }
        Ok(())
    }
}
