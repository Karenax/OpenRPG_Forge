//! Resolve environment resources only from the admitted module's closed inventory.
use crate::Result;
use forge_assets::source_environment_package::{EnvironmentPackage, programs::ProgramsPackage};
use forge_core::{AreaId, ContentHash, bytes_sha256};
use forge_project::module_package::{
    ModuleEnvironmentFiles, ModuleEnvironmentSource, ModuleFilePin, ModuleFileRole,
    ModulePackageManifest,
};
use std::collections::BTreeMap;

pub struct ResolvedModuleEnvironment {
    pub environment: EnvironmentPackage,
    pub environment_sha256: ContentHash,
    pub programs: Option<ProgramsPackage>,
}

pub fn resolve(
    module: &ModulePackageManifest,
    area: AreaId,
    mut read: impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<Option<ResolvedModuleEnvironment>> {
    module.validate().map_err(|error| error.to_string())?;
    if !module.zones.iter().any(|zone| zone.area_id == area) {
        return Err("Environment area is absent from the module".into());
    }
    let Some(binding) = module
        .environments
        .iter()
        .find(|binding| binding.area_id == area)
    else {
        return Ok(None);
    };
    let ModuleEnvironmentSource::SourceProfile {
        environment,
        programs,
    } = &binding.source;
    let bytes = read_pin(module, &environment.manifest, 256 * 1024, &mut read)?;
    let declared = EnvironmentPackage::resource_pins(&bytes, environment.manifest.sha256)?;
    let pins = close(
        module,
        environment,
        declared
            .iter()
            .map(|(name, pin)| (name.as_str(), pin.sha256, Some(pin.bytes))),
        "LICENSE-Tidewater.txt",
    )?;
    let environment_package =
        EnvironmentPackage::load(&bytes, environment.manifest.sha256, |name, maximum| {
            read_pin(module, pins[name], maximum, &mut read)
        })?;
    let programs = programs
        .as_ref()
        .map(|package| {
            let bytes = read_pin(module, &package.manifest, 2_000_000, &mut read)?;
            let declared = ProgramsPackage::resource_pins(&bytes, package.manifest.sha256)?;
            let pins = close(
                module,
                package,
                declared
                    .iter()
                    .map(|(name, hash)| (name.as_str(), *hash, None)),
                "LICENSE",
            )?;
            ProgramsPackage::load(&bytes, package.manifest.sha256, |name, maximum| {
                read_pin(module, pins[name], maximum, &mut read)
            })
        })
        .transpose()?;
    Ok(Some(ResolvedModuleEnvironment {
        environment: environment_package,
        environment_sha256: environment.manifest.sha256,
        programs,
    }))
}

fn close<'a>(
    module: &ModulePackageManifest,
    package: &'a ModuleEnvironmentFiles,
    declared: impl Iterator<Item = (&'a str, ContentHash, Option<usize>)>,
    license: &str,
) -> Result<BTreeMap<&'a str, &'a ModuleFilePin>> {
    let parent = package
        .manifest
        .path
        .rsplit_once('/')
        .map_or("", |(parent, _)| parent);
    let supplied: BTreeMap<_, _> = package
        .resources
        .iter()
        .map(|pin| (pin.path.as_str(), pin))
        .collect();
    let mut result = BTreeMap::new();
    for (name, hash, bytes) in declared {
        let path = if parent.is_empty() {
            name.to_owned()
        } else {
            format!("{parent}/{name}")
        };
        let pin = supplied
            .get(path.as_str())
            .copied()
            .ok_or("Missing indexed environment resource")?;
        let file = module
            .files
            .iter()
            .find(|file| file.path == path)
            .ok_or("Environment resource is absent from inventory")?;
        if pin.sha256 != hash
            || file.sha256 != hash
            || bytes.is_some_and(|bytes| file.bytes != bytes as u64)
            || (name == license && file.role != ModuleFileRole::License)
        {
            return Err(
                "Environment resource size, pin or license role differs from its index".into(),
            );
        }
        result.insert(name, pin);
    }
    if result.len() != supplied.len() {
        return Err("Environment resource closure differs from its index".into());
    }
    Ok(result)
}

fn read_pin(
    module: &ModulePackageManifest,
    pin: &ModuleFilePin,
    maximum: usize,
    read: &mut impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<Vec<u8>> {
    let file = module
        .files
        .iter()
        .find(|file| file.path == pin.path)
        .ok_or("Environment payload is absent from inventory")?;
    if file.bytes == 0 || file.bytes > maximum as u64 || file.sha256 != pin.sha256 {
        return Err("Environment payload declared size or pin differs".into());
    }
    let bytes = read(&pin.path, maximum)?;
    if bytes.len() as u64 != file.bytes || bytes_sha256(&bytes) != pin.sha256 {
        return Err("Environment payload bytes differ from the admitted pin".into());
    }
    Ok(bytes)
}
