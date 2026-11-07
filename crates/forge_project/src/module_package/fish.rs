//! Explicit fish profiles use the existing catalog and closed source resources.
use super::{
    MAX_MODULE_FILES, MODULE_FISH_API_VERSION, ModuleFile, ModuleFilePin, ModuleFileRole,
    ModuleObjectAsset, ModuleObjectFiles, ModuleObjectSource, ModulePackageError,
    ModulePackageManifest,
};
use forge_core::{AreaId, AssetId, ContentHash};
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};

pub const MAX_FISH_PROFILES: usize = 64;
pub const MAX_FISH_RESOURCES: usize = 4096;
pub const MAX_FISH_BYTES: u64 = 128 * 1024 * 1024;

/// Source profile and catalog for an area. The package pin is also the asset revision.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleFishBinding {
    pub area_id: AreaId,
    pub package: ModuleObjectFiles,
    pub catalog: ModuleFilePin,
    /// Original JavaScript source files intentionally omitted from the runtime
    /// package. Their names and hashes remain in the pinned source index.
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub excluded_source_scripts: Vec<String>,
    /// Name within the package, relative to the package manifest's directory.
    pub description: String,
    pub prototypes: u32,
    pub instances: u32,
    pub assets: Vec<ModuleObjectAsset>,
}

impl ModuleFishBinding {
    /// Validate the profile's declared dimensions and exact asset revisions.
    ///
    /// # Errors
    /// Rejects invalid counts, repeated or unsorted identities, invalid script
    /// exclusions and foreign pins.
    pub fn validate_inventory(&self) -> Result<(), ModulePackageError> {
        if self.prototypes == 0
            || self.prototypes > 4096
            || self.instances == 0
            || self.instances > 131_072
            || self.assets.is_empty()
            || self.assets.len() > 4096
            || self.excluded_source_scripts.len() > MAX_FISH_RESOURCES
            || self
                .excluded_source_scripts
                .windows(2)
                .any(|pair| pair[0] >= pair[1])
            || self.excluded_source_scripts.iter().any(|name| {
                !resource_name(name)
                    || !name
                        .rsplit_once('.')
                        .is_some_and(|(_, extension)| extension.eq_ignore_ascii_case("js"))
                    || name == &self.description
            })
            || u32::try_from(self.assets.len()).map_or(true, |count| count > self.instances)
            || self
                .assets
                .windows(2)
                .any(|pair| pair[0].asset_id >= pair[1].asset_id)
        {
            return Err(ModulePackageError::Limit("fish inventory"));
        }
        if self
            .assets
            .iter()
            .any(|asset| asset.sha256 != self.package.manifest.sha256)
        {
            return Err(ModulePackageError::Reference("fish asset identity or pin"));
        }
        Ok(())
    }

    /// Map explicit package resources to relative names and their pinned sizes.
    /// This checks file identity; callers still verify bytes, provenance and rights.
    ///
    /// # Errors
    /// Rejects ambiguous files, unsafe names, wrong roles, missing pins or duplicates.
    pub fn resource_inventory(
        &self,
        files: &[ModuleFile],
    ) -> Result<BTreeMap<String, (u64, ContentHash)>, ModulePackageError> {
        let (parent, _) = self
            .package
            .manifest
            .path
            .rsplit_once('/')
            .ok_or(ModulePackageError::Reference("fish package directory"))?;
        if self.package.resources.is_empty() || self.package.resources.len() > MAX_FISH_RESOURCES {
            return Err(ModulePackageError::Limit("fish resources"));
        }
        if files.len() > MAX_MODULE_FILES {
            return Err(ModulePackageError::Limit("module files"));
        }
        let mut known = BTreeMap::new();
        for file in files {
            if known.insert(file.path.as_str(), file).is_some() {
                return Err(ModulePackageError::Duplicate);
            }
        }
        let mut result = BTreeMap::new();
        for pin in &self.package.resources {
            let relative = pin
                .path
                .strip_prefix(parent)
                .and_then(|name| name.strip_prefix('/'))
                .filter(|name| resource_name(name))
                .ok_or(ModulePackageError::Reference("invalid fish resource path"))?;
            let file = known
                .get(pin.path.as_str())
                .ok_or(ModulePackageError::Reference("missing fish file"))?;
            if file.sha256 != pin.sha256
                || !matches!(
                    file.role,
                    ModuleFileRole::ObjectSource | ModuleFileRole::License
                )
                || result
                    .insert(relative.to_owned(), (file.bytes, file.sha256))
                    .is_some()
            {
                return Err(ModulePackageError::Reference("fish file role or pin"));
            }
        }
        Ok(result)
    }
}

fn resource_name(name: &str) -> bool {
    !name.is_empty()
        && name.len() <= 256
        && name
            .bytes()
            .all(|byte| byte.is_ascii_alphanumeric() || matches!(byte, b'_' | b'-' | b'.' | b'/'))
        && !name.split('/').any(|part| matches!(part, "" | "." | ".."))
}

#[derive(Default)]
struct Claims<'a> {
    profiles: BTreeSet<(AreaId, ContentHash)>,
    assets: BTreeSet<(AreaId, AssetId)>,
    resources: BTreeSet<&'a str>,
}

impl ModulePackageManifest {
    pub(super) fn validate_fish_profiles<'a>(
        &'a self,
        files: &BTreeMap<&str, &ModuleFile>,
        used: &mut BTreeSet<&'a str>,
    ) -> Result<(), ModulePackageError> {
        if !self.fish_profiles.is_empty()
            && !(MODULE_FISH_API_VERSION..=super::MODULE_HISTORICAL_MODELS_API_VERSION)
                .contains(&self.compatibility.module_api_version)
        {
            return Err(ModulePackageError::Compatibility);
        }
        if self.fish_profiles.len() > MAX_FISH_PROFILES {
            return Err(ModulePackageError::Limit("fish profiles"));
        }
        let governed: BTreeSet<_> = self.assets.iter().map(|asset| asset.asset_id).collect();
        let mut claims = Claims::default();
        for binding in &self.fish_profiles {
            self.validate_fish_identity(binding, &governed, &mut claims)?;
            self.validate_fish_resources(binding, files, used, &mut claims)?;
        }
        if claims.resources.len() > MAX_FISH_RESOURCES {
            return Err(ModulePackageError::Limit("module fish resources"));
        }
        let mut bytes = 0_u64;
        for path in claims.resources {
            bytes = bytes
                .checked_add(files[path].bytes)
                .ok_or(ModulePackageError::Limit("fish bytes"))?;
            if bytes > MAX_FISH_BYTES {
                return Err(ModulePackageError::Limit("fish bytes"));
            }
        }
        Ok(())
    }

    fn validate_fish_identity(
        &self,
        binding: &ModuleFishBinding,
        governed: &BTreeSet<AssetId>,
        claims: &mut Claims<'_>,
    ) -> Result<(), ModulePackageError> {
        if !self
            .zones
            .iter()
            .any(|zone| zone.area_id == binding.area_id)
            || !claims
                .profiles
                .insert((binding.area_id, binding.package.manifest.sha256))
        {
            return Err(ModulePackageError::Reference(
                "orphan or duplicate fish profile",
            ));
        }
        binding.validate_inventory()?;
        for asset in &binding.assets {
            let collides_with_object = self
                .objects
                .iter()
                .filter(|object| object.area_id == binding.area_id)
                .any(|object| match &object.source {
                    ModuleObjectSource::SourceSurfaces { assets, .. } => assets
                        .iter()
                        .any(|source| source.asset_id == asset.asset_id),
                });
            if governed.contains(&asset.asset_id)
                || collides_with_object
                || !claims.assets.insert((binding.area_id, asset.asset_id))
            {
                return Err(ModulePackageError::Reference("fish asset identity or pin"));
            }
        }
        Ok(())
    }

    fn validate_fish_resources<'a>(
        &'a self,
        binding: &'a ModuleFishBinding,
        files: &BTreeMap<&str, &ModuleFile>,
        used: &mut BTreeSet<&'a str>,
        claims: &mut Claims<'a>,
    ) -> Result<(), ModulePackageError> {
        let (parent, _) = binding
            .package
            .manifest
            .path
            .rsplit_once('/')
            .ok_or(ModulePackageError::Reference("fish package directory"))?;
        if !resource_name(&binding.description)
            || binding.package.resources.is_empty()
            || binding.package.resources.len() > MAX_FISH_RESOURCES
        {
            return Err(ModulePackageError::Limit("fish resources"));
        }
        let description = format!("{parent}/{}", binding.description);
        if !binding
            .package
            .resources
            .iter()
            .any(|pin| pin.path == description)
        {
            return Err(ModulePackageError::Reference(
                "fish description is not a declared resource",
            ));
        }
        let mut local = BTreeSet::new();
        for (pin, is_resource) in std::iter::once((&binding.package.manifest, false))
            .chain(std::iter::once((&binding.catalog, false)))
            .chain(binding.package.resources.iter().map(|pin| (pin, true)))
        {
            if !local.insert(pin.path.as_str()) {
                return Err(ModulePackageError::Duplicate);
            }
            if is_resource {
                let relative = pin
                    .path
                    .strip_prefix(parent)
                    .and_then(|name| name.strip_prefix('/'))
                    .ok_or(ModulePackageError::Reference(
                        "fish resource outside package directory",
                    ))?;
                if !resource_name(relative) {
                    return Err(ModulePackageError::Reference("invalid fish resource name"));
                }
            }
            let file = files
                .get(pin.path.as_str())
                .copied()
                .ok_or(ModulePackageError::Reference("missing fish file"))?;
            if file.sha256 != pin.sha256
                || !(file.role == ModuleFileRole::ObjectSource
                    || (is_resource
                        && file.role == ModuleFileRole::License
                        && self.provenance.license_paths.contains(&pin.path)))
            {
                return Err(ModulePackageError::Reference("fish file role or pin"));
            }
            used.insert(pin.path.as_str());
            claims.resources.insert(pin.path.as_str());
        }
        Ok(())
    }
}
