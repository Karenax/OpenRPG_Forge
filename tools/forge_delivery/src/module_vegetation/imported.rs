//! Closed installed source-profile resources; no renderer or original-path fallback.
use super::{authored_binding, read_pinned};
use crate::Result;
use forge_assets::source_vegetation_package::{
    PlantPackage, geometry::PlantGeometry, profile::PlantPrototype,
};
use forge_core::ContentHash;
use forge_project::{
    module_package::{
        ModuleFilePin, ModuleFileRole, ModulePackageManifest, ModuleVegetationSource,
    },
    zone_authoring::ZoneAuthoringHead,
};
use std::collections::BTreeMap;

pub struct ResolvedImportedPrototype {
    pub prototype_key: String,
    pub profile: PlantPrototype,
}

pub struct ResolvedModuleImported {
    pub package: PlantPackage,
    pub geometry: PlantGeometry,
    pub index_sha256: ContentHash,
    pub vegetation_sha256: ContentHash,
    /// Layer identities remain in the authoring head; this is a derived profile binding.
    pub prototypes: BTreeMap<String, ResolvedImportedPrototype>,
}

pub fn resolve_imported(
    module: &ModulePackageManifest,
    head: &ZoneAuthoringHead,
    read: impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<Option<ResolvedModuleImported>> {
    let binding = authored_binding(module, head)?;
    resolve_binding(module, head, binding, read)
}

pub(super) fn resolve_binding(
    module: &ModulePackageManifest,
    head: &ZoneAuthoringHead,
    binding: Option<&forge_project::module_package::ModuleVegetationBinding>,
    mut read: impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<Option<ResolvedModuleImported>> {
    let (_, vegetation) = head.effective()?;
    let Some(vegetation) = vegetation else {
        return Ok(None);
    };
    let Some(source) = &vegetation.imported_source else {
        return Ok(None);
    };
    let binding = binding.ok_or("Missing imported vegetation binding")?;
    let (index, resources) = binding
        .sources
        .iter()
        .find_map(|source| match source {
            ModuleVegetationSource::ImportedPrototypes { index, resources } => {
                Some((index, resources))
            }
            ModuleVegetationSource::GovernedSpecies { .. } => None,
        })
        .ok_or("Missing imported prototype resources")?;
    let raw = read_pinned(module, index, 256 * 1024, &mut read)?;
    if source
        .grass
        .as_ref()
        .is_some_and(|grass| grass.profile_manifest_sha256 != index.sha256)
    {
        return Err("Authored grass field differs from its installed profile index".into());
    }
    let declared = PlantPackage::resource_pins(&raw, index.sha256)?;
    let prefix = index.path.rsplit_once('/').map_or("", |(parent, _)| parent);
    let installed = |name: &str| {
        if prefix.is_empty() {
            name.to_owned()
        } else {
            format!("{prefix}/{name}")
        }
    };
    let pins: BTreeMap<_, _> = resources
        .iter()
        .map(|pin| (pin.path.as_str(), pin))
        .collect();
    if pins.len() != declared.len() {
        return Err("Imported resource closure differs from its source index".into());
    }
    for (name, expected) in &declared {
        let path = installed(name);
        let pin = pins
            .get(path.as_str())
            .ok_or("Missing indexed imported resource")?;
        let file = module
            .files
            .iter()
            .find(|file| file.path == path)
            .ok_or("Imported resource is absent from inventory")?;
        if pin.sha256 != expected.sha256
            || file.sha256 != expected.sha256
            || file.bytes != expected.bytes as u64
        {
            return Err("Imported resource size/pin differs from its source index".into());
        }
        if name == "LICENSE-Tidewater.txt" && file.role != ModuleFileRole::License {
            return Err("Source prototype license must retain its explicit license role".into());
        }
    }
    let mut prototypes = BTreeMap::new();
    for block in &source.blocks {
        PlantPrototype::validate_parameters(&block.parameter_names)?;
        let profile = PlantPrototype::from_key(&block.prototype_key)?;
        prototypes.insert(
            block.layer_id.clone(),
            ResolvedImportedPrototype {
                prototype_key: block.prototype_key.clone(),
                profile,
            },
        );
    }
    let package = PlantPackage::load(&raw, index.sha256, |name, maximum| {
        let path = installed(name);
        let pin: &ModuleFilePin = pins
            .get(path.as_str())
            .ok_or("Source resource outside the declared closure")?;
        read_pinned(module, pin, maximum, &mut read)
    })?;
    let geometry = package.decode_geometry()?;
    if prototypes
        .values()
        .any(|prototype| !geometry.groups.contains_key(prototype.profile.group()))
    {
        return Err("Authored prototype has no decoded source group".into());
    }
    Ok(Some(ResolvedModuleImported {
        package,
        geometry,
        index_sha256: index.sha256,
        vegetation_sha256: binding.vegetation_sha256,
        prototypes,
    }))
}
