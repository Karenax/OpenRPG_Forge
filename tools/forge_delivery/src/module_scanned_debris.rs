//! Resolve private scans from the module's verified payload inventory.
use crate::{Result, module_objects::read_pin};
use forge_assets::{
    source_debris_collisions::CollisionFrames,
    source_scanned_package::{ScannedPackage, atlases, geometry, templates},
};
use forge_core::{AreaId, ContentHash, bytes_sha256};
use forge_project::module_package::ModulePackageManifest;
use std::collections::BTreeMap;

pub struct ResolvedScannedDebris {
    pub package: ScannedPackage,
    pub revision: ContentHash,
    pub geometry: geometry::Geometry,
    pub collisions: CollisionFrames,
    pub instance_program: String,
}

pub fn resolve(
    module: &ModulePackageManifest,
    area: AreaId,
    mut read: impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<Option<ResolvedScannedDebris>> {
    module.validate().map_err(|error| error.to_string())?;
    let Some(binding) = module
        .scanned_debris
        .iter()
        .find(|binding| binding.area_id == area)
    else {
        return Ok(None);
    };
    let manifest = &binding.package.manifest;
    let revision = manifest.sha256;
    let index = read_pin(module, manifest, 64 * 1024, &mut read)?;
    let program = read_pin(module, &binding.instance_program, 128 * 1024, &mut read)?;
    let instance_program = String::from_utf8(program).map_err(|error| error.to_string())?;
    if instance_program.trim().is_empty() {
        return Err("Scanned instance program is empty".into());
    }
    let prefix = manifest
        .path
        .rsplit_once('/')
        .map_or("", |(parent, _)| &manifest.path[..parent.len() + 1]);
    let mut pins: BTreeMap<_, _> = binding
        .package
        .resources
        .iter()
        .map(|pin| (pin.path.as_str(), pin))
        .collect();
    let package = ScannedPackage::load(&index, revision, |name, maximum| {
        let path = format!("{prefix}{name}");
        let pin = pins
            .remove(path.as_str())
            .ok_or("Scanned resource is absent from the module closure")?;
        read_pin(module, pin, maximum, &mut read)
    })?;
    if !pins.is_empty() {
        return Err("Scanned resource closure includes unrelated files".into());
    }
    let geometry = geometry::prepare(&package)?;
    atlases::prepare(&package)?;
    let collision_bytes = package
        .file("collisions/collision-frames.json")
        .ok_or("Scanned collision ownership is absent")?;
    let collisions = CollisionFrames::load(collision_bytes, bytes_sha256(collision_bytes))?;
    if package
        .document("geometry")
        .and_then(|geometry| geometry["source_commit"].as_str())
        != Some(collisions.source_commit())
    {
        return Err("Scanned geometry and collision source commits differ".into());
    }
    collisions.validate_scanned(&geometry)?;
    let mut variations: Vec<_> = geometry
        .instances
        .iter()
        .map(|instance| templates::asset_id(&instance.source_id))
        .collect();
    variations.sort_unstable();
    if variations != binding.assets {
        return Err("Scanned variation identities differ from the module binding".into());
    }
    Ok(Some(ResolvedScannedDebris {
        package,
        revision,
        geometry,
        collisions,
        instance_program,
    }))
}
