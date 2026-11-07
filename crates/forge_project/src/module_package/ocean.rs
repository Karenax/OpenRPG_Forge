//! Explicit, closed FFT inputs for an area; these do not imply a water renderer.
use super::{ModuleFile, ModuleFilePin, ModuleFileRole, ModulePackageError, ModulePackageManifest};
use forge_core::{AreaId, ContentHash};
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};

/// Four cascades, nine kernels and up to 256 pairs of acquired frames.
pub const MAX_OCEAN_RESOURCES: usize = 524;
pub const MAX_WATER_SURFACE_RESOURCES: usize = 34;
pub const MAX_SHORE_SIM_RESOURCES: usize = 19;
pub const MAX_MODULE_OCEAN_BYTES: u64 = 256 * 1024 * 1024;

/// Match a decoded source index to its declared portable closure. Native and
/// browser consumers share this check; the native origin also admits provenance.
pub fn close_resources<'a>(
    files: &BTreeMap<&str, &ModuleFile>,
    package: &'a ModuleOceanFiles,
    declared: impl Iterator<Item = (String, ContentHash, usize)>,
    role: ModuleFileRole,
    license: &str,
) -> Result<BTreeMap<String, &'a ModuleFilePin>, String> {
    let supplied: BTreeMap<_, _> = package
        .resources
        .iter()
        .map(|pin| (pin.path.as_str(), pin))
        .collect();
    if supplied.len() != package.resources.len() {
        return Err("Duplicate ocean resource".into());
    }
    let parent = package
        .manifest
        .path
        .rsplit_once('/')
        .map_or("", |(p, _)| p);
    let mut pins = BTreeMap::new();
    for (name, expected_hash, expected_bytes) in declared {
        let path = if parent.is_empty() {
            name.clone()
        } else {
            format!("{parent}/{name}")
        };
        let pin = supplied
            .get(path.as_str())
            .copied()
            .ok_or("Ocean resource closure differs from its index")?;
        let file = files
            .get(path.as_str())
            .copied()
            .ok_or("Ocean resource is absent from module inventory")?;
        if pin.sha256 != expected_hash
            || file.sha256 != pin.sha256
            || file.bytes != expected_bytes as u64
        {
            return Err("Ocean resource size or pin differs from its index".into());
        }
        let expected_role = if name == license {
            ModuleFileRole::License
        } else {
            role
        };
        if file.role != expected_role {
            return Err("Ocean resource role differs from its source index".into());
        }
        if pins.insert(name, pin).is_some() {
            return Err("Duplicate ocean index resource".into());
        }
    }
    if pins.len() != package.resources.len() || !pins.contains_key(license) {
        return Err("Ocean resource closure contains unexpected files or lacks its license".into());
    }
    Ok(pins)
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleOceanFiles {
    pub manifest: ModuleFilePin,
    pub resources: Vec<ModuleFilePin>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleOceanBinding {
    pub area_id: AreaId,
    pub package: ModuleOceanFiles,
    /// Surface and shore are admitted together; no implicit defaults.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub surface: Option<ModuleOceanFiles>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub shore_sim: Option<ModuleOceanFiles>,
}

impl ModulePackageManifest {
    pub(super) fn validate_ocean_inputs<'a>(
        &'a self,
        files: &BTreeMap<&str, &ModuleFile>,
        used: &mut BTreeSet<&'a str>,
    ) -> Result<(), ModulePackageError> {
        if !self.ocean_inputs.is_empty()
            && !(super::MODULE_OCEAN_API_VERSION..=super::MODULE_HISTORICAL_MODELS_API_VERSION)
                .contains(&self.compatibility.module_api_version)
        {
            return Err(ModulePackageError::Compatibility);
        }
        if self.ocean_inputs.len() > self.zones.len() {
            return Err(ModulePackageError::Limit("ocean areas"));
        }
        let mut areas = BTreeSet::new();
        let mut unique = BTreeSet::new();
        for binding in &self.ocean_inputs {
            if !areas.insert(binding.area_id)
                || !self
                    .zones
                    .iter()
                    .any(|zone| zone.area_id == binding.area_id)
            {
                return Err(ModulePackageError::Reference(
                    "orphan or duplicate ocean area",
                ));
            }
            if binding.surface.is_some() != binding.shore_sim.is_some() {
                return Err(ModulePackageError::Reference(
                    "incomplete ocean surface and shore",
                ));
            }
            if binding.surface.is_some()
                && self.compatibility.module_api_version < super::MODULE_OCEAN_SURFACE_API_VERSION
            {
                return Err(ModulePackageError::Compatibility);
            }
            let packages = std::iter::once((
                &binding.package,
                ModuleFileRole::OceanSimulation,
                MAX_OCEAN_RESOURCES,
            ))
            .chain(
                binding
                    .surface
                    .iter()
                    .map(|p| (p, ModuleFileRole::OceanSurface, MAX_WATER_SURFACE_RESOURCES)),
            )
            .chain(
                binding
                    .shore_sim
                    .iter()
                    .map(|p| (p, ModuleFileRole::ShoreSimulation, MAX_SHORE_SIM_RESOURCES)),
            );
            for (package, role, limit) in packages {
                if package.resources.is_empty() || package.resources.len() > limit {
                    return Err(ModulePackageError::Limit("ocean resources"));
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
                        .ok_or(ModulePackageError::Reference("missing ocean file"))?;
                    let role_valid = file.role == role
                        || (license_allowed
                            && file.role == ModuleFileRole::License
                            && self.provenance.license_paths.contains(&pin.path));
                    if !role_valid || file.sha256 != pin.sha256 {
                        return Err(ModulePackageError::Reference("ocean file role or pin"));
                    }
                    used.insert(pin.path.as_str());
                    unique.insert(pin.path.as_str());
                }
            }
        }
        let mut bytes = 0_u64;
        for path in unique {
            bytes = bytes
                .checked_add(files[path].bytes)
                .ok_or(ModulePackageError::Limit("ocean bytes"))?;
            if bytes > MAX_MODULE_OCEAN_BYTES {
                return Err(ModulePackageError::Limit("ocean bytes"));
            }
        }
        Ok(())
    }
}
