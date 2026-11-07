//! Closed terrain resources belong to an explicitly named area and raster source.
use super::{ModuleFile, ModuleFilePin, ModuleFileRole, ModulePackageError, ModulePackageManifest};
use forge_core::{AreaId, ContentHash, canonical_json_sha256};
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};

pub const MAX_TERRAIN_PACKAGE_RESOURCES: usize = 7;
pub const MAX_MODULE_TERRAIN_BYTES: u64 = 512 * 1024 * 1024;

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleTerrainFiles {
    pub manifest: ModuleFilePin,
    pub resources: Vec<ModuleFilePin>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum ModuleTerrainSource {
    SourceRaster {
        /// Pin of the complete canonical sampled-source descriptor, not just its samples.
        source_descriptor_sha256: ContentHash,
        package: ModuleTerrainFiles,
    },
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleTerrainBinding {
    pub area_id: AreaId,
    pub source: ModuleTerrainSource,
}

impl ModulePackageManifest {
    pub(super) fn validate_terrain_surfaces<'a>(
        &'a self,
        files: &BTreeMap<&str, &ModuleFile>,
        used: &mut BTreeSet<&'a str>,
    ) -> Result<(), ModulePackageError> {
        if !self.terrain_surfaces.is_empty()
            && !(super::MODULE_TERRAIN_API_VERSION..=super::MODULE_HISTORICAL_MODELS_API_VERSION)
                .contains(&self.compatibility.module_api_version)
        {
            return Err(ModulePackageError::Compatibility);
        }
        if self.terrain_surfaces.len() > self.zones.len() {
            return Err(ModulePackageError::Limit("terrain areas"));
        }
        let mut areas = BTreeSet::new();
        let mut unique = BTreeSet::new();
        for binding in &self.terrain_surfaces {
            if !areas.insert(binding.area_id)
                || !self
                    .zones
                    .iter()
                    .any(|zone| zone.area_id == binding.area_id)
            {
                return Err(ModulePackageError::Reference(
                    "orphan or duplicate terrain area",
                ));
            }
            let ModuleTerrainSource::SourceRaster { package, .. } = &binding.source;
            if package.resources.is_empty()
                || package.resources.len() > MAX_TERRAIN_PACKAGE_RESOURCES
            {
                return Err(ModulePackageError::Limit("terrain resources"));
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
                    .ok_or(ModulePackageError::Reference("missing terrain file"))?;
                let role_valid = file.role == ModuleFileRole::TerrainSurface
                    || (license_allowed
                        && file.role == ModuleFileRole::License
                        && self.provenance.license_paths.contains(&pin.path));
                if !role_valid || file.sha256 != pin.sha256 {
                    return Err(ModulePackageError::Reference("terrain file role or pin"));
                }
                used.insert(pin.path.as_str());
                unique.insert(pin.path.as_str());
            }
        }
        let mut bytes = 0_u64;
        for path in unique {
            bytes = bytes
                .checked_add(files[path].bytes)
                .ok_or(ModulePackageError::Limit("terrain bytes"))?;
            if bytes > MAX_MODULE_TERRAIN_BYTES {
                return Err(ModulePackageError::Limit("terrain bytes"));
            }
        }
        Ok(())
    }

    /// The caller supplies its validated current document, including unsaved corrections.
    /// No companion means explicit absence, even for a compatible raster geometry.
    pub fn validate_terrain_document(
        &self,
        document: &crate::TerrainDocument,
    ) -> Result<(), ModulePackageError> {
        if document.project_id != self.project_id
            || !self
                .zones
                .iter()
                .any(|zone| zone.area_id == document.area_id)
        {
            return Err(ModulePackageError::Reference(
                "terrain document belongs to another scope",
            ));
        }
        let Some(binding) = self
            .terrain_surfaces
            .iter()
            .find(|b| b.area_id == document.area_id)
        else {
            return Ok(());
        };
        let ModuleTerrainSource::SourceRaster {
            source_descriptor_sha256,
            ..
        } = &binding.source;
        let source = document
            .sampled_source
            .as_ref()
            .ok_or(ModulePackageError::Reference(
                "terrain companion requires a sampled source",
            ))?;
        source
            .validate()
            .map_err(|e| ModulePackageError::Archive(e.to_string()))?;
        if canonical_json_sha256(source).map_err(|e| ModulePackageError::Archive(e.to_string()))?
            != *source_descriptor_sha256
        {
            return Err(ModulePackageError::Reference(
                "terrain source descriptor differs from binding",
            ));
        }
        Ok(())
    }
}
