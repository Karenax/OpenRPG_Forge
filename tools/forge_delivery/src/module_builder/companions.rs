//! Preserve admitted per-area source companions, using the existing codecs at final admission.
use super::*;
use crate::legacy_reference::ReferenceScene;
use origin::PreparedOrigin;

#[derive(Default)]
pub(super) struct Companions {
    pub environment: Option<ModuleEnvironmentBinding>,
    pub vegetation: Option<ModuleVegetationBinding>,
    pub objects: Option<ModuleObjectBinding>,
    pub fish: Vec<ModuleFishBinding>,
    pub terrain: Option<ModuleTerrainBinding>,
    pub ocean: Option<ModuleOceanBinding>,
    pub roles: BTreeMap<String, ModuleFileRole>,
}

impl Companions {
    pub fn api_version(&self) -> u32 {
        if !self.fish.is_empty() {
            MODULE_FISH_API_VERSION
        } else if self
            .ocean
            .as_ref()
            .is_some_and(|ocean| ocean.surface.is_some())
        {
            MODULE_OCEAN_SURFACE_API_VERSION
        } else if self.ocean.is_some() {
            MODULE_OCEAN_API_VERSION
        } else if self.terrain.is_some() {
            MODULE_TERRAIN_API_VERSION
        } else if self.objects.is_some() {
            MODULE_OBJECTS_API_VERSION
        } else if self
            .environment
            .as_ref()
            .is_some_and(|binding| binding.solar_clock.is_some())
        {
            MODULE_SOLAR_CLOCK_API_VERSION
        } else if self.environment.is_some() {
            MODULE_ENVIRONMENT_API_VERSION
        } else if self.vegetation.is_some() {
            MODULE_VEGETATION_API_VERSION
        } else {
            orpmod::MODULE_API_VERSION
        }
    }
}

pub(super) fn prepare(
    origin: Option<&PreparedOrigin>,
    zone: &ZoneEditSession,
    payloads: &mut BTreeMap<String, Vec<u8>>,
) -> Result<Companions> {
    let Some(origin) = origin else {
        return Ok(Companions::default());
    };
    let area = zone.head().base.terrain.area_id;
    let mut result = Companions {
        ocean: origin
            .module()
            .ocean_inputs
            .iter()
            .find(|binding| binding.area_id == area)
            .cloned(),
        terrain: origin
            .module()
            .terrain_surfaces
            .iter()
            .find(|binding| binding.area_id == area)
            .cloned(),
        objects: origin
            .module()
            .objects
            .iter()
            .find(|binding| binding.area_id == area)
            .cloned(),
        fish: origin
            .module()
            .fish_profiles
            .iter()
            .filter(|binding| binding.area_id == area)
            .cloned()
            .collect(),
        environment: origin
            .module()
            .environments
            .iter()
            .find(|b| b.area_id == area)
            .cloned(),
        vegetation: origin
            .module()
            .vegetation
            .iter()
            .find(|b| b.area_id == area)
            .cloned(),
        ..Default::default()
    };
    if let Some(binding) = &result.ocean {
        for package in std::iter::once(&binding.package)
            .chain(binding.surface.iter())
            .chain(binding.shore_sim.iter())
        {
            for pin in std::iter::once(&package.manifest).chain(&package.resources) {
                let role = insert(origin, pin, payloads)?;
                result.roles.insert(pin.path.clone(), role);
            }
        }
    }
    if let Some(binding) = &result.terrain {
        origin
            .module()
            .validate_terrain_document(&zone.head().effective()?.0)
            .map_err(|e| e.to_string())?;
        let ModuleTerrainSource::SourceRaster { package, .. } = &binding.source;
        for pin in std::iter::once(&package.manifest).chain(&package.resources) {
            let role = insert(origin, pin, payloads)?;
            result.roles.insert(pin.path.clone(), role);
        }
    }
    if let Some(binding) = &result.objects {
        let ModuleObjectSource::SourceSurfaces { package, .. } = &binding.source;
        for pin in std::iter::once(&package.manifest).chain(&package.resources) {
            let role = insert(origin, pin, payloads)?;
            result.roles.insert(pin.path.clone(), role);
        }
    }
    for binding in &result.fish {
        for pin in std::iter::once(&binding.package.manifest)
            .chain(&binding.package.resources)
            .chain(std::iter::once(&binding.catalog))
        {
            let role = insert(origin, pin, payloads)?;
            result.roles.insert(pin.path.clone(), role);
        }
    }
    if let Some(binding) = &result.environment {
        let ModuleEnvironmentSource::SourceProfile {
            environment,
            programs,
        } = &binding.source;
        for package in std::iter::once(environment).chain(programs.iter()) {
            for pin in std::iter::once(&package.manifest).chain(&package.resources) {
                let role = insert(origin, pin, payloads)?;
                result.roles.insert(pin.path.clone(), role);
            }
        }
    }
    if let Some(binding) = &mut result.vegetation {
        binding.vegetation_sha256 = zone
            .head()
            .effective()?
            .1
            .ok_or("Origin vegetation companion lost its authored document")?
            .content_hash()
            .map_err(|e| e.to_string())?;
        for source in &binding.sources {
            let pins: Vec<_> = match source {
                ModuleVegetationSource::GovernedSpecies {
                    catalogs,
                    asset_kit,
                } => std::iter::once(asset_kit).chain(catalogs).collect(),
                ModuleVegetationSource::ImportedPrototypes { index, resources } => {
                    std::iter::once(index).chain(resources).collect()
                }
            };
            for pin in pins {
                let role = insert(origin, pin, payloads)?;
                result.roles.insert(pin.path.clone(), role);
            }
        }
    }
    Ok(result)
}

pub(super) fn prepare_reference(
    reference: &ReferenceScene,
    zone: &ZoneEditSession,
    payloads: &mut BTreeMap<String, Vec<u8>>,
) -> Result<Companions> {
    let Some(terrain) = ReferenceScene::terrain_pin(zone)? else {
        return Ok(Companions::default());
    };
    let area = reference.index_area(terrain)?;
    let mut bindings = area.describe(reference, zone)?;
    let prefix = format!("_forge-export/reference/{}", bindings.vegetation.area_id);
    let scoped = |name: &str| format!("{prefix}/{name}");
    let rebase = |pin: &mut ModuleFilePin| pin.path = scoped(&pin.path);
    for source in &mut bindings.vegetation.sources {
        if let ModuleVegetationSource::ImportedPrototypes { index, resources } = source {
            std::iter::once(index).chain(resources).for_each(rebase);
        }
    }
    if let Some(binding) = &mut bindings.environment {
        let ModuleEnvironmentSource::SourceProfile {
            environment,
            programs,
        } = &mut binding.source;
        for package in std::iter::once(environment).chain(programs.iter_mut()) {
            std::iter::once(&mut package.manifest)
                .chain(&mut package.resources)
                .for_each(rebase);
        }
    }
    if let Some(binding) = &mut bindings.objects {
        let ModuleObjectSource::SourceSurfaces { package, .. } = &mut binding.source;
        std::iter::once(&mut package.manifest)
            .chain(&mut package.resources)
            .for_each(rebase);
    }
    let mut roles = BTreeMap::new();
    for (name, bytes) in reference.read_notices()? {
        let path = format!("_forge-export/reference/notices/{name}");
        if let Some(existing) = payloads.get(&path) {
            if existing != &bytes {
                return Err("An export payload conflicts with a reference notice".into());
            }
        } else {
            payloads.insert(path.clone(), bytes);
        }
        roles.insert(path, ModuleFileRole::License);
    }
    let used = payloads
        .values()
        .map(|bytes| bytes.len() as u64)
        .sum::<u64>();
    let mut remaining = forge_project::workspace::MAX_MODULE_ORIGIN_PACKAGE_BYTES
        .checked_sub(used)
        .ok_or("Reference export exceeds the bounded package budget")?;
    for file in bindings.files {
        let path = scoped(&file.path);
        if payloads.contains_key(&path) {
            return Err("An export payload conflicts with a reference resource".into());
        }
        remaining = remaining
            .checked_sub(file.bytes)
            .ok_or("Reference export exceeds the bounded package budget")?;
        let limit = usize::try_from(file.bytes).map_err(|error| error.to_string())?;
        let bytes = area.read_resource(reference, &file.path, limit)?;
        if bytes.len() != limit || bytes_sha256(&bytes) != file.sha256 {
            return Err("An installed reference resource differs from its export pin".into());
        }
        payloads.insert(path.clone(), bytes);
        roles.insert(path, file.role);
    }
    Ok(Companions {
        environment: bindings.environment,
        vegetation: Some(bindings.vegetation),
        objects: bindings.objects,
        roles,
        ..Default::default()
    })
}

fn insert(
    origin: &PreparedOrigin,
    pin: &ModuleFilePin,
    payloads: &mut BTreeMap<String, Vec<u8>>,
) -> Result<ModuleFileRole> {
    let file = origin
        .module()
        .files
        .iter()
        .find(|f| f.path == pin.path)
        .ok_or("Origin companion is absent from its admitted inventory")?;
    if file.sha256 != pin.sha256 {
        return Err("Origin companion pin differs from its admitted inventory".into());
    }
    if let Some(existing) = payloads.get(&pin.path) {
        if existing.len() as u64 != file.bytes || bytes_sha256(existing) != pin.sha256 {
            return Err("Export input conflicts with an immutable origin companion".into());
        }
    } else {
        payloads.insert(
            pin.path.clone(),
            origin.payload(
                &pin.path,
                forge_project::workspace::MAX_MODULE_ORIGIN_PACKAGE_BYTES as usize,
            )?,
        );
    }
    Ok(file.role)
}

pub(super) fn supply_lights(
    origin: Option<&PreparedOrigin>,
    zone: &ZoneEditSession,
    payloads: &mut BTreeMap<String, Vec<u8>>,
) -> Result<()> {
    let Some(origin) = origin else {
        return Ok(());
    };
    for (manifest, descriptor) in zone.required_local_light_packages()? {
        let Some(package) = origin
            .module()
            .local_light_packages
            .iter()
            .find(|p| p.manifest.sha256 == manifest)
        else {
            continue;
        };
        if package.descriptor_sha256 != descriptor {
            return Err("Saved light descriptor differs from the admitted origin".into());
        }
        for pin in std::iter::once(&package.manifest).chain(&package.resources) {
            insert(origin, pin, payloads)?;
        }
    }
    Ok(())
}

pub(super) fn append(
    prepared: &mut Prepared,
    zone: &ZoneEditSession,
    reference: Option<&ReferenceScene>,
) -> Result<()> {
    let companions = if let Some(scene) = reference {
        prepare_reference(scene, zone, &mut prepared.payloads)?
    } else {
        prepare(prepared.origin.as_ref(), zone, &mut prepared.payloads)?
    };
    prepared.module.compatibility.module_api_version = prepared
        .module
        .compatibility
        .module_api_version
        .max(companions.api_version());
    for (path, role) in companions.roles {
        if let Some(file) = prepared.module.files.iter().find(|f| f.path == path) {
            if file.role != role {
                return Err("Areas disagree on a shared companion role".into());
            }
            continue;
        }
        let bytes = &prepared.payloads[&path];
        prepared.module.files.push(ModuleFile {
            path: path.clone(),
            role,
            sha256: bytes_sha256(bytes),
            bytes: bytes.len() as u64,
        });
        if role == ModuleFileRole::License {
            prepared.module.provenance.license_paths.push(path);
        }
    }
    prepared.module.environments.extend(companions.environment);
    prepared.module.vegetation.extend(companions.vegetation);
    prepared.module.objects.extend(companions.objects);
    prepared.module.fish_profiles.extend(companions.fish);
    prepared.module.terrain_surfaces.extend(companions.terrain);
    prepared.module.ocean_inputs.extend(companions.ocean);
    prepared.module.environments.sort_by_key(|b| b.area_id);
    prepared.module.vegetation.sort_by_key(|b| b.area_id);
    prepared.module.objects.sort_by_key(|b| b.area_id);
    prepared
        .module
        .fish_profiles
        .sort_by_key(|b| (b.area_id, b.package.manifest.sha256));
    prepared.module.terrain_surfaces.sort_by_key(|b| b.area_id);
    prepared.module.ocean_inputs.sort_by_key(|b| b.area_id);
    Ok(())
}
