//! Resolve fish profiles only from an explicitly admitted module resource closure.
use crate::{Result, module_objects::read_pin};
use forge_assets::{
    AssetCatalog,
    source_fish_package::{PinnedFishPayload, ResourceIndex, ResourcePin, resource_index},
    source_fish_payload::{OwnedFishProfile, check_catalog_pins},
};
use forge_core::AreaId;
use forge_project::module_package::{ModuleFileRole, ModuleFishBinding, ModulePackageManifest};
use std::collections::BTreeMap;

pub type ResolvedFish = OwnedFishProfile<PinnedFishPayload>;

/// Resolve every explicitly declared profile for one area, without Vault fallback.
/// This does not publish a renderer or grant redistribution rights.
///
/// # Errors
/// Rejects foreign areas, incomplete closures, changed bytes or mismatched identities.
pub fn resolve(
    module: &ModulePackageManifest,
    area: AreaId,
    mut read: impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<Vec<ResolvedFish>> {
    module.validate().map_err(|error| error.to_string())?;
    if !module.zones.iter().any(|zone| zone.area_id == area) {
        return Err("Fish area is absent from module".into());
    }
    module
        .fish_profiles
        .iter()
        .filter(|binding| binding.area_id == area)
        .map(|binding| resolve_binding(module, binding, &mut read))
        .collect()
}

fn resolve_binding(
    module: &ModulePackageManifest,
    binding: &ModuleFishBinding,
    read: &mut impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<ResolvedFish> {
    let raw = read_pin(module, &binding.package.manifest, 256 * 1024, read)?;
    let index = resource_index(&raw, binding.package.manifest.sha256)?;
    let (parent, _) = binding
        .package
        .manifest
        .path
        .rsplit_once('/')
        .ok_or("Fish package directory absent")?;
    let pins = exact_resources(module, binding, &index)?;
    let license_path = format!("{parent}/{}", index.license);
    if !module.provenance.license_paths.contains(&license_path)
        || !module
            .files
            .iter()
            .any(|file| file.path == license_path && file.role == ModuleFileRole::License)
    {
        return Err("Fish source license is absent from module provenance".into());
    }
    let raw_catalog = read_pin(module, &binding.catalog, 4 * 1024 * 1024, read)?;
    let catalog: AssetCatalog =
        serde_json::from_slice(&raw_catalog).map_err(|error| error.to_string())?;
    let expected: Vec<_> = binding
        .assets
        .iter()
        .map(|asset| (asset.asset_id, asset.sha256))
        .collect();
    check_catalog_pins(&catalog, &expected)?;
    let payload = PinnedFishPayload::load(&pins, |name, maximum| {
        let path = format!("{parent}/{name}");
        let pin = binding
            .package
            .resources
            .iter()
            .find(|pin| pin.path == path)
            .ok_or("Fish resource is not declared")?;
        read_pin(module, pin, maximum, read)
    })?;
    ResolvedFish::new(
        binding.package.manifest.sha256,
        payload,
        catalog,
        binding.description.clone(),
        (
            usize::try_from(binding.prototypes).map_err(|error| error.to_string())?,
            usize::try_from(binding.instances).map_err(|error| error.to_string())?,
        ),
    )
}

fn exact_resources(
    module: &ModulePackageManifest,
    binding: &ModuleFishBinding,
    index: &ResourceIndex,
) -> Result<BTreeMap<String, ResourcePin>> {
    let mut pins = BTreeMap::new();
    for (name, (bytes, sha256)) in binding
        .resource_inventory(&module.files)
        .map_err(|error| error.to_string())?
    {
        pins.insert(
            name,
            ResourcePin {
                bytes: usize::try_from(bytes).map_err(|error| error.to_string())?,
                sha256,
            },
        );
    }
    if pins != index.runtime_resources(&binding.excluded_source_scripts)? {
        return Err("Fish resource closure differs from its index".into());
    }
    Ok(pins)
}
