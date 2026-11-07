//! Resolve declared FFT inputs solely from the admitted module inventory.
use crate::Result;
use forge_assets::source_ocean_package::OceanPackage;
use forge_assets::source_shore_sim_package::ShoreSimPackage;
use forge_assets::source_water_surface_package::WaterSurfacePackage;
use forge_core::{AreaId, ContentHash, bytes_sha256};
use forge_project::module_package::{
    ModuleFile, ModuleFilePin, ModuleFileRole, ModuleOceanFiles, ModulePackageManifest,
};
use std::collections::BTreeMap;

pub struct ResolvedOcean {
    pub package: OceanPackage,
    pub package_sha256: ContentHash,
    pub surface: Option<WaterSurfacePackage>,
    pub shore_sim: Option<ShoreSimPackage>,
}

pub fn resolve(
    module: &ModulePackageManifest,
    area: AreaId,
    mut read: impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<Option<ResolvedOcean>> {
    module.validate().map_err(|error| error.to_string())?;
    if !module.zones.iter().any(|zone| zone.area_id == area) {
        return Err("Ocean area is absent from the module".into());
    }
    let Some(binding) = module
        .ocean_inputs
        .iter()
        .find(|binding| binding.area_id == area)
    else {
        return Ok(None);
    };
    let package = &binding.package;
    let files: BTreeMap<_, _> = module
        .files
        .iter()
        .map(|file| (file.path.as_str(), file))
        .collect();
    let index = read_pin(&files, &package.manifest, 256 * 1024, &mut read)?;
    let declared = OceanPackage::resource_pins(&index, package.manifest.sha256)?;
    let pins = close(
        module,
        &files,
        package,
        declared
            .into_iter()
            .map(|(name, pin)| (name, pin.sha256, pin.bytes)),
        ModuleFileRole::OceanSimulation,
        OceanPackage::license_name(),
    )?;
    let loaded = OceanPackage::load(&index, package.manifest.sha256, |name, maximum| {
        read_pin(
            &files,
            pins.get(name).ok_or("Ocean resource is not declared")?,
            maximum,
            &mut read,
        )
    })?;
    let (surface, shore_sim) = if let (Some(surface), Some(shore)) =
        (&binding.surface, &binding.shore_sim)
    {
        let index = read_pin(&files, &surface.manifest, 12 * 1024, &mut read)?;
        let declared = WaterSurfacePackage::resource_pins(&index, surface.manifest.sha256)?;
        let pins = close(
            module,
            &files,
            surface,
            declared
                .into_iter()
                .map(|(name, pin)| (name, pin.sha256, pin.bytes)),
            ModuleFileRole::OceanSurface,
            WaterSurfacePackage::license_name(),
        )?;
        let water = WaterSurfacePackage::load(&index, surface.manifest.sha256, |name, maximum| {
            read_pin(
                &files,
                pins.get(name).ok_or("Water resource is not declared")?,
                maximum,
                &mut read,
            )
        })?;
        let index = read_pin(&files, &shore.manifest, 12 * 1024, &mut read)?;
        let declared = ShoreSimPackage::resource_pins(&index, shore.manifest.sha256)?;
        let pins = close(
            module,
            &files,
            shore,
            declared
                .into_iter()
                .map(|(name, pin)| (name, pin.sha256, pin.bytes)),
            ModuleFileRole::ShoreSimulation,
            ShoreSimPackage::license_name(),
        )?;
        let sim = ShoreSimPackage::load(&index, shore.manifest.sha256, |name, maximum| {
            read_pin(
                &files,
                pins.get(name).ok_or("Shore resource is not declared")?,
                maximum,
                &mut read,
            )
        })?;
        for (shore_name, surface_name) in [
            ("shore-field.rgba32.bin", "shore-field.rgba32.bin"),
            ("shore-params.bin", "uniform-shoreP.bin"),
            ("simulation-params.bin", "uniform-shoreSimP.bin"),
        ] {
            if sim.file(shore_name) != water.file(surface_name) {
                return Err(format!("Shore and surface inputs differ: {shore_name}"));
            }
        }
        (Some(water), Some(sim))
    } else {
        (None, None)
    };
    Ok(Some(ResolvedOcean {
        package: loaded,
        package_sha256: package.manifest.sha256,
        surface,
        shore_sim,
    }))
}

fn close<'a>(
    module: &ModulePackageManifest,
    files: &BTreeMap<&str, &ModuleFile>,
    package: &'a ModuleOceanFiles,
    declared: impl Iterator<Item = (String, ContentHash, usize)>,
    role: ModuleFileRole,
    license: &str,
) -> Result<BTreeMap<String, &'a ModuleFilePin>> {
    let pins = forge_project::module_package::ocean::close_resources(
        files, package, declared, role, license,
    )?;
    if !module
        .provenance
        .license_paths
        .contains(&pins[license].path)
    {
        return Err("Ocean source license must be preserved in module provenance".into());
    }
    Ok(pins)
}

fn read_pin(
    files: &BTreeMap<&str, &ModuleFile>,
    pin: &ModuleFilePin,
    maximum: usize,
    read: &mut impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<Vec<u8>> {
    let file = files
        .get(pin.path.as_str())
        .copied()
        .ok_or("Ocean file is absent from module inventory")?;
    if file.sha256 != pin.sha256 || file.bytes > maximum as u64 {
        return Err("Ocean file pin or size exceeds admitted request".into());
    }
    let bytes = read(&pin.path, maximum)?;
    if bytes.len() as u64 != file.bytes || bytes_sha256(&bytes) != pin.sha256 {
        return Err("Ocean payload differs from admitted file".into());
    }
    Ok(bytes)
}
