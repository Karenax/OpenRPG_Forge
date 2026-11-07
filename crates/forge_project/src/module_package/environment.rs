//! Explicit closed resources for the common source environment, independent of any map.
use super::{
    MODULE_ENVIRONMENT_API_VERSION, ModuleFile, ModuleFilePin, ModuleFileRole, ModulePackageError,
    ModulePackageManifest,
};
use forge_core::AreaId;
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};

pub const MAX_ENVIRONMENT_RESOURCES: usize = 1024;
pub const MAX_MODULE_ENVIRONMENT_BYTES: u64 = 64 * 1024 * 1024;

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleEnvironmentFiles {
    pub manifest: ModuleFilePin,
    pub resources: Vec<ModuleFilePin>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum ModuleEnvironmentSource {
    SourceProfile {
        environment: ModuleEnvironmentFiles,
        /// Explicit absence keeps the acquired cube/SH; it does not enable a live HDR graph.
        #[serde(default, skip_serializing_if = "Option::is_none")]
        programs: Option<ModuleEnvironmentFiles>,
    },
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleEnvironmentBinding {
    pub area_id: AreaId,
    pub source: ModuleEnvironmentSource,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub solar_clock: Option<ModuleSolarClock>,
}

/// Source clock inputs, independent of graphics APIs. Minutes match the existing
/// presentation controls; elapsed time remains derived runtime state.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleSolarClock {
    pub captured_minutes: u16,
    pub start_minutes: u16,
    pub azimuth_degrees: i16,
    /// None pauses the clock. A duration must be nonzero.
    pub day_length_seconds: Option<u32>,
}

impl ModuleSolarClock {
    pub fn validate(self) -> Result<(), ModulePackageError> {
        if self.captured_minutes > 1440
            || self.start_minutes > 1440
            || !(-180..=180).contains(&self.azimuth_degrees)
            || self.day_length_seconds == Some(0)
        {
            return Err(ModulePackageError::Reference(
                "invalid solar clock parameters",
            ));
        }
        Ok(())
    }
}

impl ModulePackageManifest {
    pub(super) fn validate_environments<'a>(
        &'a self,
        files: &BTreeMap<&str, &ModuleFile>,
        used: &mut BTreeSet<&'a str>,
    ) -> Result<(), ModulePackageError> {
        if !self.environments.is_empty()
            && !(MODULE_ENVIRONMENT_API_VERSION..=super::MODULE_HISTORICAL_MODELS_API_VERSION)
                .contains(&self.compatibility.module_api_version)
        {
            return Err(ModulePackageError::Compatibility);
        }
        if self.environments.len() > self.zones.len() {
            return Err(ModulePackageError::Limit("environment areas"));
        }
        let mut areas = BTreeSet::new();
        let mut unique = BTreeSet::new();
        for binding in &self.environments {
            if !areas.insert(binding.area_id)
                || !self
                    .zones
                    .iter()
                    .any(|zone| zone.area_id == binding.area_id)
            {
                return Err(ModulePackageError::Reference(
                    "orphan or duplicate environment area",
                ));
            }
            let ModuleEnvironmentSource::SourceProfile {
                environment,
                programs,
            } = &binding.source;
            if let Some(clock) = binding.solar_clock {
                clock.validate()?;
                if self.compatibility.module_api_version < super::MODULE_SOLAR_CLOCK_API_VERSION
                    || programs.is_none()
                {
                    return Err(ModulePackageError::Compatibility);
                }
            }
            let mut local = BTreeSet::new();
            for package in std::iter::once(environment).chain(programs.iter()) {
                if package.resources.is_empty()
                    || package.resources.len() > MAX_ENVIRONMENT_RESOURCES
                {
                    return Err(ModulePackageError::Limit("environment resources"));
                }
                for (pin, license_allowed) in std::iter::once((&package.manifest, false))
                    .chain(package.resources.iter().map(|pin| (pin, true)))
                {
                    if !local.insert(pin.path.as_str()) {
                        return Err(ModulePackageError::Duplicate);
                    }
                    let file = files
                        .get(pin.path.as_str())
                        .copied()
                        .ok_or(ModulePackageError::Reference("missing environment file"))?;
                    let role_valid = file.role == ModuleFileRole::Environment
                        || (license_allowed
                            && file.role == ModuleFileRole::License
                            && self.provenance.license_paths.contains(&pin.path));
                    if !role_valid || file.sha256 != pin.sha256 {
                        return Err(ModulePackageError::Reference(
                            "environment file role or pin",
                        ));
                    }
                    used.insert(&pin.path);
                    unique.insert(pin.path.as_str());
                }
            }
        }
        if unique.len() > MAX_ENVIRONMENT_RESOURCES {
            return Err(ModulePackageError::Limit("module environment resources"));
        }
        let mut bytes = 0_u64;
        for path in unique {
            bytes = bytes
                .checked_add(files[path].bytes)
                .ok_or(ModulePackageError::Limit("environment bytes"))?;
            if bytes > MAX_MODULE_ENVIRONMENT_BYTES {
                return Err(ModulePackageError::Limit("environment bytes"));
            }
        }
        Ok(())
    }
}
