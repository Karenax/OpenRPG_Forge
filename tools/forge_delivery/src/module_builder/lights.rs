//! Close the saved source-light requirements with the ordinary immutable codec.
use super::*;

pub(super) fn prepare(
    zone: &ZoneEditSession,
    payloads: &BTreeMap<String, Vec<u8>>,
) -> Result<Vec<ModuleLocalLightPackage>> {
    zone.required_local_light_packages()?
        .into_iter()
        .map(|(manifest, descriptor)| {
            crate::module_local_lights::prepare_export(
                manifest,
                descriptor,
                zone.head().base.terrain.area_id,
                payloads,
            )
        })
        .collect()
}

pub(super) fn inventory(
    packages: &[ModuleLocalLightPackage],
    inventory: &mut Inventory,
) -> Result<()> {
    for package in packages {
        inventory.role(&package.manifest.path, ModuleFileRole::LocalLights)?;
        let license = crate::module_local_lights::license_path(package.manifest.sha256);
        for pin in &package.resources {
            inventory.role(
                &pin.path,
                if pin.path == license {
                    ModuleFileRole::License
                } else {
                    ModuleFileRole::LocalLights
                },
            )?;
        }
    }
    Ok(())
}

pub(super) fn take_payloads(payloads: &mut BTreeMap<String, Vec<u8>>) -> BTreeMap<String, Vec<u8>> {
    let paths = payloads
        .keys()
        .filter(|p| p.starts_with(crate::module_local_lights::PREFIX))
        .cloned()
        .collect::<Vec<_>>();
    paths
        .into_iter()
        .filter_map(|p| payloads.remove(&p).map(|bytes| (p, bytes)))
        .collect()
}

/// Select caller candidates by the existing immutable package namespace. Full
/// closure admission happens after origin resources have been supplied.
pub(super) fn insert_candidates(
    zone: &ZoneEditSession,
    payloads: &mut BTreeMap<String, Vec<u8>>,
    supplied: &BTreeMap<String, Vec<u8>>,
) -> Result<()> {
    for (manifest, _) in zone.required_local_light_packages()? {
        let manifest_path = crate::module_local_lights::manifest_path(manifest);
        let prefix = format!(
            "{}/",
            manifest_path
                .rsplit_once('/')
                .ok_or("Light namespace absent")?
                .0
        );
        for (path, bytes) in supplied
            .iter()
            .filter(|(path, _)| path.starts_with(&prefix))
        {
            if payloads.insert(path.clone(), bytes.clone()).is_some() {
                return Err("Source-light transport conflicts with an export payload".into());
            }
        }
    }
    Ok(())
}

pub(super) fn append(
    prepared: &mut Prepared,
    zone: &ZoneEditSession,
    supplied: &BTreeMap<String, Vec<u8>>,
) -> Result<()> {
    let area = zone.head().base.terrain.area_id;
    for package in prepare(zone, supplied)? {
        if let Some(existing) = prepared
            .module
            .local_light_packages
            .iter_mut()
            .find(|p| p.manifest.sha256 == package.manifest.sha256)
        {
            if existing.manifest != package.manifest
                || existing.descriptor_sha256 != package.descriptor_sha256
                || existing.resources != package.resources
            {
                return Err("Areas disagree on an immutable source-light package".into());
            }
            existing.areas.push(area);
            existing.areas.sort_unstable();
            existing.areas.dedup();
            continue;
        }
        let license = crate::module_local_lights::license_path(package.manifest.sha256);
        for pin in std::iter::once(&package.manifest).chain(&package.resources) {
            let bytes = &supplied[&pin.path];
            if prepared.payloads.contains_key(&pin.path) {
                return Err("Source-light payload conflicts with another area".into());
            }
            prepared.module.files.push(ModuleFile {
                path: pin.path.clone(),
                bytes: bytes.len() as u64,
                sha256: pin.sha256,
                role: if pin.path == license {
                    ModuleFileRole::License
                } else {
                    ModuleFileRole::LocalLights
                },
            });
            prepared.payloads.insert(pin.path.clone(), bytes.clone());
        }
        prepared.module.provenance.license_paths.push(license);
        prepared.module.local_light_packages.push(package);
    }
    if !prepared.module.local_light_packages.is_empty() {
        prepared.module.compatibility.module_api_version = prepared
            .module
            .compatibility
            .module_api_version
            .max(MODULE_LOCAL_LIGHTS_API_VERSION);
    }
    Ok(())
}
