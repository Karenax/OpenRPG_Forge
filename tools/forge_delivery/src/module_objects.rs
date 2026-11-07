//! Resolve source objects only from the module's explicit closed resource binding.
use crate::Result;
use forge_assets::source_surface_package::SurfacePackage;
use forge_core::{AreaId, ContentHash, bytes_sha256};
use forge_project::module_package::{
    ModuleFilePin, ModuleObjectAsset, ModuleObjectSource, ModulePackageManifest,
};
use std::collections::BTreeMap;

pub struct ResolvedObjects {
    pub package: SurfacePackage,
    pub package_sha256: ContentHash,
}

pub fn resolve(
    module: &ModulePackageManifest,
    area: AreaId,
    mut read: impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<Option<ResolvedObjects>> {
    module.validate().map_err(|e| e.to_string())?;
    if !module.zones.iter().any(|zone| zone.area_id == area) {
        return Err("Object area is absent from module".into());
    }
    let Some(binding) = module
        .objects
        .iter()
        .find(|binding| binding.area_id == area)
    else {
        return Ok(None);
    };
    let ModuleObjectSource::SourceSurfaces { package, assets } = &binding.source;
    let index = read_pin(module, &package.manifest, 2 * 1024 * 1024, &mut read)?;
    let declared = SurfacePackage::resource_pins(&index, package.manifest.sha256)?;
    let parent = package
        .manifest
        .path
        .rsplit_once('/')
        .map_or("", |(parent, _)| parent);
    let mut pins = BTreeMap::new();
    for (name, pin) in declared {
        let path = if parent.is_empty() {
            name.clone()
        } else {
            format!("{parent}/{name}")
        };
        let supplied = package
            .resources
            .iter()
            .find(|pin| pin.path == path)
            .ok_or("Object resource closure differs from its index")?;
        let file = module
            .files
            .iter()
            .find(|file| file.path == path)
            .ok_or("Object payload is absent")?;
        if supplied.sha256 != pin.sha256 || file.bytes != pin.bytes as u64 {
            return Err("Object resource size or pin differs from its index".into());
        }
        pins.insert(name, supplied);
    }
    if pins.len() != package.resources.len() {
        return Err("Object resource closure contains unexpected files".into());
    }
    let license = SurfacePackage::license_name(&index, package.manifest.sha256)?;
    let license = pins
        .get(&license)
        .ok_or("Object source license is absent from its closure")?;
    if !module.provenance.license_paths.contains(&license.path)
        || !module.files.iter().any(|file| {
            file.path == license.path
                && file.role == forge_project::module_package::ModuleFileRole::License
        })
    {
        return Err("Object source license must be preserved in module provenance".into());
    }
    let loaded = SurfacePackage::load(&index, package.manifest.sha256, |name, maximum| {
        read_pin(
            module,
            pins.get(name).ok_or("Object resource is not declared")?,
            maximum,
            &mut read,
        )
    })?;
    let mut actual: Vec<_> = loaded
        .catalog()
        .assets
        .iter()
        .map(|asset| ModuleObjectAsset {
            asset_id: asset.id,
            sha256: asset.content_hash,
        })
        .collect();
    actual.sort_by_key(|asset| asset.asset_id);
    if &actual != assets {
        return Err("Object source asset identities differ from their binding".into());
    }
    Ok(Some(ResolvedObjects {
        package: loaded,
        package_sha256: package.manifest.sha256,
    }))
}

pub(crate) fn read_pin(
    module: &ModulePackageManifest,
    pin: &ModuleFilePin,
    maximum: usize,
    read: &mut impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<Vec<u8>> {
    let file = module
        .files
        .iter()
        .find(|file| file.path == pin.path)
        .ok_or("Object file is absent from module inventory")?;
    if file.sha256 != pin.sha256 || file.bytes > maximum as u64 {
        return Err("Object file pin or size exceeds admitted request".into());
    }
    let bytes = read(&pin.path, maximum)?;
    if bytes.len() as u64 != file.bytes || bytes_sha256(&bytes) != pin.sha256 {
        return Err("Object payload differs from admitted file".into());
    }
    Ok(bytes)
}
