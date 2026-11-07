//! Closed resources for the source references saved in canonical zone archives.
use super::{
    AreaId, BTreeMap, BTreeSet, ContentHash, Deserialize, MODULE_LOCAL_LIGHTS_API_VERSION,
    ModuleFile, ModuleFilePin, ModuleFileRole, ModulePackageError, ModulePackageManifest,
    Serialize,
};
use crate::zone_authoring::session::ZoneEditSession;

pub const MAX_LOCAL_LIGHT_PACKAGE_RESOURCES: usize = 4096;
pub const MAX_MODULE_LOCAL_LIGHT_BYTES: u64 = 512 * 1024 * 1024;

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleLocalLightPackage {
    /// Sorted areas requiring this package, including their retained history.
    pub areas: Vec<AreaId>,
    pub manifest: ModuleFilePin,
    pub descriptor_sha256: ContentHash,
    pub resources: Vec<ModuleFilePin>,
}

impl ModulePackageManifest {
    pub(super) fn validate_local_light_packages<'a>(
        &'a self,
        files: &BTreeMap<&str, &ModuleFile>,
        used: &mut BTreeSet<&'a str>,
    ) -> Result<(), ModulePackageError> {
        if !self.local_light_packages.is_empty()
            && !(MODULE_LOCAL_LIGHTS_API_VERSION..=super::MODULE_HISTORICAL_MODELS_API_VERSION)
                .contains(&self.compatibility.module_api_version)
        {
            return Err(ModulePackageError::Compatibility);
        }
        let mut packages = BTreeSet::new();
        let mut unique = BTreeSet::new();
        for package in &self.local_light_packages {
            if !packages.insert(package.manifest.sha256) {
                return Err(ModulePackageError::Duplicate);
            }
            if package.areas.is_empty()
                || package.areas.len() > self.zones.len()
                || package.areas.windows(2).any(|ids| ids[0] >= ids[1])
                || package
                    .areas
                    .iter()
                    .any(|id| !self.zones.iter().any(|z| z.area_id == *id))
            {
                return Err(ModulePackageError::Reference(
                    "local-light package area closure",
                ));
            }
            if package.resources.is_empty()
                || package.resources.len() > MAX_LOCAL_LIGHT_PACKAGE_RESOURCES
            {
                return Err(ModulePackageError::Limit("local-light resources"));
            }
            let mut local = BTreeSet::new();
            for (pin, member) in std::iter::once((&package.manifest, false))
                .chain(package.resources.iter().map(|p| (p, true)))
            {
                if !local.insert(pin.path.as_str()) {
                    return Err(ModulePackageError::Duplicate);
                }
                let file =
                    files
                        .get(pin.path.as_str())
                        .copied()
                        .ok_or(ModulePackageError::Reference(
                            "missing local-light resource",
                        ))?;
                let valid_role = file.role == ModuleFileRole::LocalLights
                    || (member
                        && file.role == ModuleFileRole::License
                        && self.provenance.license_paths.contains(&pin.path));
                if !valid_role || file.sha256 != pin.sha256 {
                    return Err(ModulePackageError::Reference(
                        "local-light file role or pin",
                    ));
                }
                used.insert(&pin.path);
                unique.insert(pin.path.as_str());
            }
        }
        let mut bytes = 0_u64;
        for path in unique {
            bytes = bytes
                .checked_add(files[path].bytes)
                .ok_or(ModulePackageError::Limit("local-light bytes"))?;
            if bytes > MAX_MODULE_LOCAL_LIGHT_BYTES {
                return Err(ModulePackageError::Limit("local-light bytes"));
            }
        }
        Ok(())
    }

    pub(super) fn validate_zone_local_lights(
        &self,
        zone: &ZoneEditSession,
        area: AreaId,
    ) -> Result<(), ModulePackageError> {
        let required = zone
            .required_local_light_packages()
            .map_err(ModulePackageError::Archive)?;
        if self.compatibility.module_api_version < MODULE_LOCAL_LIGHTS_API_VERSION
            && !required.is_empty()
        {
            return Err(ModulePackageError::Archive(
                "source local-light companion packages are not supported by this module API".into(),
            ));
        }
        let declared: BTreeMap<_, _> = self
            .local_light_packages
            .iter()
            .filter(|p| p.areas.contains(&area))
            .map(|p| (p.manifest.sha256, p.descriptor_sha256))
            .collect();
        if required != declared {
            return Err(ModulePackageError::Reference(
                "zone local-light package closure differs",
            ));
        }
        zone.visit_local_light_placements(|placement| {
            if !self.assets.iter().any(|asset| {
                asset.asset_id == placement.asset_id && asset.sha256 == placement.asset_hash
            }) {
                return Err("Historical source-light carrier asset is not declared".into());
            }
            Ok(())
        })
        .map_err(ModulePackageError::Archive)?;
        Ok(())
    }
}
