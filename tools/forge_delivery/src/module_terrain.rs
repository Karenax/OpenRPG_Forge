//! Resolve the declared terrain profile without any library or geometry-based fallback.
use crate::Result;
use forge_assets::source_terrain_package::{RasterGeometry, TerrainPackage};
use forge_core::{AreaId, ContentHash, bytes_sha256};
use forge_project::{
    TerrainDocument,
    module_package::{ModuleFilePin, ModuleFileRole, ModulePackageManifest, ModuleTerrainSource},
};
use std::collections::BTreeMap;

pub struct ResolvedTerrain {
    pub package: TerrainPackage,
    pub package_sha256: ContentHash,
    pub source_descriptor_sha256: ContentHash,
}

pub fn resolve(
    module: &ModulePackageManifest,
    area: AreaId,
    document: &TerrainDocument,
    mut read: impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<Option<ResolvedTerrain>> {
    module.validate().map_err(|e| e.to_string())?;
    if document.area_id != area {
        return Err("Terrain document area differs from request".into());
    }
    module
        .validate_terrain_document(document)
        .map_err(|e| e.to_string())?;
    let Some(binding) = module
        .terrain_surfaces
        .iter()
        .find(|binding| binding.area_id == area)
    else {
        return Ok(None);
    };
    document.validate().map_err(|e| e.to_string())?;
    let ModuleTerrainSource::SourceRaster {
        package,
        source_descriptor_sha256,
    } = &binding.source;
    let index = read_pin(module, &package.manifest, 16 * 1024, &mut read)?;
    if !geometry_matches(
        document,
        TerrainPackage::manifest_geometry(&index, package.manifest.sha256)?,
    ) {
        return Err("Declared terrain profile does not support this raster geometry".into());
    }
    let declared = TerrainPackage::resource_pins(&index, package.manifest.sha256)?;
    let parent = package
        .manifest
        .path
        .rsplit_once('/')
        .map_or("", |(parent, _)| parent);
    let mut pins = BTreeMap::new();
    for (name, expected) in declared {
        let path = if parent.is_empty() {
            name.clone()
        } else {
            format!("{parent}/{name}")
        };
        let pin = package
            .resources
            .iter()
            .find(|pin| pin.path == path)
            .ok_or("Terrain resource closure differs from its index")?;
        let file = module
            .files
            .iter()
            .find(|file| file.path == path)
            .ok_or("Terrain resource is absent from module inventory")?;
        if pin.sha256 != expected.sha256 || file.bytes != expected.bytes as u64 {
            return Err("Terrain resource size or pin differs from its index".into());
        }
        pins.insert(name, pin);
    }
    if pins.len() != package.resources.len() {
        return Err("Terrain resource closure contains unexpected files".into());
    }
    let license = pins
        .get(TerrainPackage::manifest_license_name(
            &index,
            package.manifest.sha256,
        )?)
        .ok_or("Terrain source license is absent")?;
    if !module.provenance.license_paths.contains(&license.path)
        || !module
            .files
            .iter()
            .any(|file| file.path == license.path && file.role == ModuleFileRole::License)
    {
        return Err("Terrain source license must be preserved in module provenance".into());
    }
    let loaded = TerrainPackage::load(&index, package.manifest.sha256, |name, maximum| {
        read_pin(
            module,
            pins.get(name).ok_or("Terrain resource is not declared")?,
            maximum,
            &mut read,
        )
    })?;
    Ok(Some(ResolvedTerrain {
        package: loaded,
        package_sha256: package.manifest.sha256,
        source_descriptor_sha256: *source_descriptor_sha256,
    }))
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
        .ok_or("Terrain file is absent from module inventory")?;
    if file.sha256 != pin.sha256 || file.bytes > maximum as u64 {
        return Err("Terrain file pin or size exceeds admitted request".into());
    }
    let bytes = read(&pin.path, maximum)?;
    if bytes.len() as u64 != file.bytes || bytes_sha256(&bytes) != pin.sha256 {
        return Err("Terrain payload differs from admitted file".into());
    }
    Ok(bytes)
}

fn geometry_matches(document: &TerrainDocument, geometry: RasterGeometry) -> bool {
    let Some(source) = &document.sampled_source else {
        return false;
    };
    geometry.matches_sampled_domain(
        [source.width, source.height],
        source.origin_xz_m,
        source.sample_spacing_m,
        source.outside_height_m,
        [document.extent_m.size_x_m, document.extent_m.size_z_m],
        document.height_sampling.sample_spacing_m,
    )
}
