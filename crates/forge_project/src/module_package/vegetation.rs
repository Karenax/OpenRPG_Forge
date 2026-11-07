//! Delivery references to existing vegetation contracts; no duplicate authoring document.
use super::{
    MODULE_VEGETATION_API_VERSION, ModuleFile, ModuleFileRole, ModulePackageError,
    ModulePackageManifest,
};
use forge_core::{AreaId, ContentHash};
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};

pub const MAX_VEGETATION_CATALOGS: usize = 64;
pub const MAX_VEGETATION_RESOURCES: usize = 4096;
pub const MAX_MODULE_VEGETATION_BYTES: u64 = 256 * 1024 * 1024;
type DeclaredPins<'a> = (u8, Vec<(&'a ModuleFilePin, bool)>);

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleFilePin {
    pub path: String,
    pub sha256: ContentHash,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum ModuleVegetationSource {
    GovernedSpecies {
        catalogs: Vec<ModuleFilePin>,
        asset_kit: ModuleFilePin,
    },
    ImportedPrototypes {
        index: ModuleFilePin,
        resources: Vec<ModuleFilePin>,
    },
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleVegetationBinding {
    pub area_id: AreaId,
    pub vegetation_sha256: ContentHash,
    pub sources: Vec<ModuleVegetationSource>,
}

impl ModulePackageManifest {
    pub(super) fn validate_zone_vegetation_contract(
        &self,
        head: &crate::zone_authoring::ZoneAuthoringHead,
    ) -> Result<(), ModulePackageError> {
        if (MODULE_VEGETATION_API_VERSION..=super::MODULE_HISTORICAL_MODELS_API_VERSION)
            .contains(&self.compatibility.module_api_version)
        {
            return self.validate_zone_vegetation(head);
        }
        // Historical APIs cannot substitute unresolved vegetation companions.
        if head
            .corrections
            .species
            .as_ref()
            .is_some_and(|species| !species.is_empty())
        {
            return Err(super::archive_error(
                "species catalogs are not supported by this module API",
            ));
        }
        if head
            .corrections
            .imported_plant_placements
            .as_ref()
            .is_some_and(|placements| !placements.is_empty())
        {
            return Err(super::archive_error(
                "imported plant copies require a resolved prototype catalog",
            ));
        }
        if head
            .base
            .vegetation
            .as_ref()
            .is_some_and(|vegetation| vegetation.imported_source.is_some())
        {
            return Err(super::archive_error(
                "imported vegetation prototype catalogs are not supported by this module API",
            ));
        }
        Ok(())
    }

    pub(super) fn validate_zone_vegetation(
        &self,
        head: &crate::zone_authoring::ZoneAuthoringHead,
    ) -> Result<(), ModulePackageError> {
        let (_, vegetation) = head.effective().map_err(ModulePackageError::Archive)?;
        let species = head.corrections.species.as_deref().unwrap_or_default();
        let imported = vegetation
            .as_ref()
            .is_some_and(|value| value.imported_source.is_some());
        let binding = self
            .vegetation
            .iter()
            .find(|binding| binding.area_id == head.base.terrain.area_id);
        let Some(binding) = binding else {
            return if !species.is_empty() || imported {
                Err(ModulePackageError::Reference(
                    "missing authored vegetation dependencies",
                ))
            } else {
                Ok(())
            };
        };
        let vegetation = vegetation.ok_or(ModulePackageError::Reference(
            "vegetation binding without authored document",
        ))?;
        if vegetation
            .content_hash()
            .map_err(|error| ModulePackageError::Archive(error.to_string()))?
            != binding.vegetation_sha256
        {
            return Err(ModulePackageError::Reference("vegetation document pin"));
        }
        let mut has_imported = false;
        let mut catalogs = BTreeSet::new();
        for source in &binding.sources {
            match source {
                ModuleVegetationSource::GovernedSpecies {
                    catalogs: supplied, ..
                } => {
                    for pin in supplied {
                        if !catalogs.insert(pin.sha256) {
                            return Err(ModulePackageError::Duplicate);
                        }
                    }
                }
                ModuleVegetationSource::ImportedPrototypes { .. } => has_imported = true,
            }
        }
        let expected: BTreeSet<_> = species
            .iter()
            .flat_map(|layer| &layer.prototypes)
            .map(|reference| reference.catalog_sha256)
            .collect();
        if catalogs != expected || has_imported != imported {
            return Err(ModulePackageError::Reference(
                "vegetation companions differ from authored sources",
            ));
        }
        Ok(())
    }

    pub(super) fn validate_vegetation<'a>(
        &'a self,
        files: &BTreeMap<&str, &ModuleFile>,
        used: &mut BTreeSet<&'a str>,
    ) -> Result<(), ModulePackageError> {
        if !self.vegetation.is_empty()
            && !(MODULE_VEGETATION_API_VERSION..=super::MODULE_HISTORICAL_MODELS_API_VERSION)
                .contains(&self.compatibility.module_api_version)
        {
            return Err(ModulePackageError::Compatibility);
        }
        if self.vegetation.len() > self.zones.len() {
            return Err(ModulePackageError::Limit("vegetation areas"));
        }
        let mut areas = BTreeSet::new();
        let mut resources = BTreeSet::new();
        for binding in &self.vegetation {
            if !areas.insert(binding.area_id)
                || !self
                    .zones
                    .iter()
                    .any(|zone| zone.area_id == binding.area_id)
            {
                return Err(ModulePackageError::Reference(
                    "orphan or duplicate vegetation area",
                ));
            }
            if binding.sources.is_empty() || binding.sources.len() > 2 {
                return Err(ModulePackageError::Limit("vegetation source families"));
            }
            let mut families = BTreeSet::new();
            let mut local = BTreeSet::new();
            for source in &binding.sources {
                let (family, pins) = source.pins()?;
                if !families.insert(family) {
                    return Err(ModulePackageError::Duplicate);
                }
                for (pin, license_allowed) in pins {
                    if !local.insert(pin.path.as_str()) {
                        return Err(ModulePackageError::Duplicate);
                    }
                    let file = files
                        .get(pin.path.as_str())
                        .copied()
                        .ok_or(ModulePackageError::Reference("missing vegetation file"))?;
                    let role_valid = file.role == ModuleFileRole::Vegetation
                        || (license_allowed
                            && file.role == ModuleFileRole::License
                            && self.provenance.license_paths.contains(&pin.path));
                    if !role_valid || file.sha256 != pin.sha256 {
                        return Err(ModulePackageError::Reference("vegetation file role or pin"));
                    }
                    used.insert(&pin.path);
                    resources.insert(pin.path.as_str());
                }
            }
        }
        let mut bytes = 0_u64;
        for path in resources {
            bytes = bytes
                .checked_add(files[path].bytes)
                .ok_or(ModulePackageError::Limit("vegetation bytes"))?;
            if bytes > MAX_MODULE_VEGETATION_BYTES {
                return Err(ModulePackageError::Limit("vegetation bytes"));
            }
        }
        Ok(())
    }
}

impl ModuleVegetationSource {
    /// Declaration closure only; payload decoding belongs to adapters.
    pub(super) fn pins(&self) -> Result<DeclaredPins<'_>, ModulePackageError> {
        match self {
            Self::GovernedSpecies {
                catalogs,
                asset_kit,
            } => {
                if catalogs.is_empty() || catalogs.len() > MAX_VEGETATION_CATALOGS {
                    return Err(ModulePackageError::Limit("species catalogs"));
                }
                Ok((
                    0,
                    std::iter::once((asset_kit, false))
                        .chain(catalogs.iter().map(|pin| (pin, false)))
                        .collect(),
                ))
            }
            Self::ImportedPrototypes { index, resources } => {
                if resources.is_empty() || resources.len() > MAX_VEGETATION_RESOURCES {
                    return Err(ModulePackageError::Limit("imported prototype resources"));
                }
                Ok((
                    1,
                    std::iter::once((index, false))
                        .chain(resources.iter().map(|pin| (pin, true)))
                        .collect(),
                ))
            }
        }
    }
}
