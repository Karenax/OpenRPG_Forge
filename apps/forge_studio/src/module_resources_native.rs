//! Native restoration from the open module's admitted immutable origin.
//! Cache ownership follows the canonical module pin; a view cannot choose paths.
use crate::module_resources_protocol::{CHUNK_BYTES, Chunk, Reply, Request, Snapshot};
use forge_core::ModuleId;
use forge_delivery::orpmod::OriginDocuments;
use forge_project::{
    authoring::host::AuthoringHost,
    module_package::{ModuleFilePin, ModuleVegetationSource},
    workspace::ModuleOriginPin,
    zone_authoring::{ZoneAuthoringHead, session::ZoneEditSession},
};
use std::{
    collections::BTreeSet,
    path::{Path, PathBuf},
};
mod legacy;

struct Cached {
    root: PathBuf,
    module: ModuleId,
    pin: ModuleOriginPin,
    origin: OriginDocuments,
    snapshot: Option<Snapshot>,
    payload: Option<(usize, Vec<u8>)>,
}

/// At most one admitted origin and one decoded payload are retained.
#[derive(Default)]
pub struct Resources {
    cached: Option<Cached>,
    legacy: Option<legacy::Cached>,
}

impl Resources {
    #[must_use]
    pub const fn new() -> Self {
        Self {
            cached: None,
            legacy: None,
        }
    }

    pub fn execute(
        &mut self,
        root: &Path,
        host: &mut AuthoringHost,
        request: Request,
    ) -> Result<Reply, String> {
        let expected_head = request.expected_head();
        let Some((module, pin)) = host.module_origin_snapshot(expected_head)? else {
            self.cached = None;
            return legacy::execute(&mut self.legacy, root, host, request);
        };
        self.legacy = None;
        let cache = self.origin(root, module, pin)?;
        match request {
            Request::Describe { .. } => {
                if cache
                    .snapshot
                    .as_ref()
                    .is_some_and(|snapshot| snapshot.head_hash == expected_head)
                {
                    return Ok(Reply::Description {
                        snapshot: cache.snapshot.clone().map(Box::new),
                    });
                }
                let archive = host.game_archive(expected_head)?;
                let zone = ZoneEditSession::from_archive_bytes(&archive)?;
                cache.snapshot = None;
                cache.payload = None;
                let snapshot = restore(&cache.origin, pin.package_sha256, &zone)?;
                cache.snapshot.clone_from(&snapshot);
                Ok(Reply::Description {
                    snapshot: snapshot.map(Box::new),
                })
            }
            Request::Read {
                expected_package,
                resource_index,
                offset,
                ..
            } => {
                if expected_package != pin.package_sha256 {
                    return Err("Module resource package request is stale".into());
                }
                read_chunk(
                    cache.snapshot.as_ref(),
                    &mut cache.payload,
                    expected_head,
                    resource_index,
                    offset,
                    |path, size| cache.origin.read_payload(path, size),
                )
                .map(|chunk| Reply::Resource { chunk })
            }
        }
    }

    /// Read a model from the current module's admitted origin during journal replay.
    pub fn read_historical_model(
        &mut self,
        root: &Path,
        host: &AuthoringHost,
        asset_id: forge_core::AssetId,
        sha256: forge_core::ContentHash,
    ) -> Result<Option<crate::asset_library_protocol::ModuleModel>, String> {
        use crate::asset_library_protocol::{ModuleCollisionPolicy, ModuleModel};
        let Some((module, pin)) = host.current_module_origin()? else {
            return Ok(None);
        };
        let cache = self.origin(root, module, pin)?;
        let Some(model) = cache
            .origin
            .module
            .historical_models
            .iter()
            .find(|model| model.asset_id == asset_id && model.payload.sha256 == sha256)
        else {
            return Ok(None);
        };
        let bytes = cache.origin.read_payload(
            &model.payload.path,
            crate::asset_library_model_wire::MAX_MODEL_BYTES,
        )?;
        Ok(Some(ModuleModel {
            asset_id,
            sha256,
            catalog_sha256: pin.package_sha256,
            collision: model
                .static_triangle_meshes
                .then_some(ModuleCollisionPolicy::StaticTriangleMeshes),
            bytes,
        }))
    }

    fn origin(
        &mut self,
        root: &Path,
        module: ModuleId,
        pin: ModuleOriginPin,
    ) -> Result<&mut Cached, String> {
        let root = root.canonicalize().map_err(|error| error.to_string())?;
        if !self
            .cached
            .as_ref()
            .is_some_and(|cache| cache.root == root && cache.module == module && cache.pin == pin)
        {
            // Never retain another module after an origin admission fails.
            self.cached = None;
            let origin = crate::module_workspace_bootstrap::read_export_origin(&root, module, pin)?;
            self.cached = Some(Cached {
                root,
                module,
                pin,
                origin: forge_delivery::orpmod::read_origin_documents(origin.package_bytes)?,
                snapshot: None,
                payload: None,
            });
        }
        self.cached
            .as_mut()
            .ok_or("Module resource cache absent".into())
    }
}

fn read_chunk(
    snapshot: Option<&Snapshot>,
    payload: &mut Option<(usize, Vec<u8>)>,
    expected_head: forge_core::ContentHash,
    index: usize,
    offset: usize,
    read: impl FnOnce(&str, usize) -> Result<Vec<u8>, String>,
) -> Result<Chunk, String> {
    let snapshot = snapshot
        .filter(|snapshot| snapshot.head_hash == expected_head)
        .ok_or("Describe module resources for the current head before reading")?;
    let file = snapshot
        .files
        .get(index)
        .ok_or("Module resource ordinal absent")?;
    let total = usize::try_from(file.bytes).map_err(|error| error.to_string())?;
    if file.bytes > crate::module_resources_protocol::MAX_RESOURCE_BYTES
        || offset >= total
        || !offset.is_multiple_of(CHUNK_BYTES)
    {
        return Err("Invalid module resource chunk offset".into());
    }
    if payload.as_ref().is_none_or(|(actual, _)| *actual != index) {
        *payload = Some((index, read(&file.path, total)?));
    }
    let bytes = &payload.as_ref().ok_or("Module resource bytes absent")?.1;
    Ok(Chunk {
        head_hash: snapshot.head_hash,
        package_sha256: snapshot.package_sha256,
        resource_index: index,
        resource_sha256: file.sha256,
        total_bytes: total,
        offset,
        bytes: bytes[offset..(offset + CHUNK_BYTES).min(total)].to_vec(),
    })
}

/// Resolve both existing source families using the delivery codecs. This does
/// not publish renderer readiness, mutate a zone or reinterpret source rights.
fn restore_vegetation(
    origin: &OriginDocuments,
    package_sha256: forge_core::ContentHash,
    head: &ZoneAuthoringHead,
) -> Result<Option<Snapshot>, String> {
    let area = head.base.terrain.area_id;
    if head.base.terrain.project_id != origin.module.project_id {
        return Err("Module vegetation project differs from its authoring authority".into());
    }
    let module = &origin.module;
    let original = module
        .zones
        .iter()
        .find(|zone| zone.area_id == area)
        .ok_or("Current vegetation area is absent from the original module")?;
    let archive = origin.read_payload(
        &original.archive_path,
        forge_project::zone_authoring::MAX_ZONE_HEAD_BYTES,
    )?;
    let original = ZoneEditSession::from_archive_bytes(&archive)?;
    let catalog: forge_assets::governed_import::GovernedImportCatalog =
        serde_json::from_slice(&origin.read_payload(&module.asset_catalog_path, 16 * 1024 * 1024)?)
            .map_err(|error| error.to_string())?;
    let restored = forge_delivery::module_vegetation::restore_authoring(
        module,
        original.head(),
        head,
        &catalog,
        |path, maximum| origin.read_payload(path, maximum),
    )?;
    let Some(binding) = restored.binding else {
        return Ok(None);
    };
    let resolved = restored.species;
    let mut paths = BTreeSet::new();
    for source in &binding.sources {
        match source {
            ModuleVegetationSource::ImportedPrototypes { index, resources } => {
                insert(&mut paths, index);
                for resource in resources {
                    insert(&mut paths, resource);
                }
            }
            ModuleVegetationSource::GovernedSpecies {
                catalogs,
                asset_kit,
            } => {
                insert(&mut paths, asset_kit);
                for catalog in catalogs {
                    insert(&mut paths, catalog);
                }
            }
        }
    }
    if let Some(resolved) = &resolved {
        for resource in resolved.assets.values() {
            insert(&mut paths, resource);
        }
    }
    let files = paths
        .into_iter()
        .map(|path| {
            module
                .files
                .iter()
                .find(|file| file.path == path)
                .cloned()
                .ok_or_else(|| {
                    "Restored vegetation resource is outside the admitted inventory".to_owned()
                })
        })
        .collect::<Result<Vec<_>, _>>()?;
    Ok(Some(Snapshot {
        head_hash: head.content_hash()?,
        package_sha256,
        legacy_reference: false,
        module_id: module.module_id,
        area_id: area,
        vegetation: Some(binding),
        objects: None,
        fish_profiles: Vec::new(),
        environment: None,
        terrain: None,
        ocean: None,
        local_lights: Vec::new(),
        placement_companions: Vec::new(),
        species: resolved.map(|value| {
            Box::new(crate::module_resources_protocol::Species {
                catalog: value.catalog,
                asset_kit: value.asset_kit,
                assets: value.assets,
            })
        }),
        files,
    }))
}

fn insert(paths: &mut BTreeSet<String>, pin: &ModuleFilePin) {
    paths.insert(pin.path.clone());
}

/// One admitted origin and one ordinal/chunk channel serve every declared
/// render companion. Absence never imports a reference library.
pub fn restore(
    origin: &OriginDocuments,
    package_sha256: forge_core::ContentHash,
    zone: &ZoneEditSession,
) -> Result<Option<Snapshot>, String> {
    use forge_project::module_package::{ModuleEnvironmentSource, ModuleObjectSource};
    let head = zone.head();
    let area = head.base.terrain.area_id;
    let module = &origin.module;
    // Vegetation restoration also verifies the original project/area authority
    // and the current effective document, including author corrections.
    let mut snapshot = restore_vegetation(origin, package_sha256, head)?.unwrap_or(Snapshot {
        head_hash: head.content_hash()?,
        package_sha256,
        legacy_reference: false,
        module_id: module.module_id,
        area_id: area,
        vegetation: None,
        objects: None,
        fish_profiles: Vec::new(),
        environment: None,
        terrain: None,
        ocean: None,
        local_lights: Vec::new(),
        placement_companions: Vec::new(),
        species: None,
        files: Vec::new(),
    });
    let mut paths: BTreeSet<_> = snapshot
        .files
        .iter()
        .map(|file| file.path.clone())
        .collect();
    if let Some(binding) = module
        .objects
        .iter()
        .find(|binding| binding.area_id == area)
    {
        forge_delivery::module_objects::resolve(module, area, |path, maximum| {
            origin.read_payload(path, maximum)
        })?
        .ok_or("Declared module objects disappeared")?;
        let ModuleObjectSource::SourceSurfaces { package, .. } = &binding.source;
        insert(&mut paths, &package.manifest);
        for pin in &package.resources {
            insert(&mut paths, pin);
        }
        snapshot.objects = Some(binding.clone());
    }
    restore_fish(origin, area, &mut snapshot, &mut paths)?;
    if let Some(binding) = module
        .environments
        .iter()
        .find(|binding| binding.area_id == area)
    {
        forge_delivery::module_environment::resolve(module, area, |path, maximum| {
            origin.read_payload(path, maximum)
        })?
        .ok_or("Declared module environment disappeared")?;
        let ModuleEnvironmentSource::SourceProfile {
            environment,
            programs,
        } = &binding.source;
        for package in std::iter::once(environment).chain(programs.iter()) {
            insert(&mut paths, &package.manifest);
            for pin in &package.resources {
                insert(&mut paths, pin);
            }
        }
        snapshot.environment = Some(binding.clone());
    }
    restore_terrain(origin, head, &mut snapshot, &mut paths)?;
    restore_ocean(origin, area, &mut snapshot, &mut paths)?;
    restore_local_lights(origin, area, &mut snapshot, &mut paths)?;
    // A head hash does not identify undo/redo history. Expose the admitted
    // origin inventory for the whole module so a cached description cannot
    // mistake a retained revision for an absent one after history changes.
    // The reader below still requires the complete companion pin to select it.
    for companion in &module.placement_companions {
        insert(&mut paths, &companion.package);
        insert(&mut paths, &companion.proof);
        snapshot.placement_companions.push(companion.clone());
    }
    if snapshot.vegetation.is_none()
        && snapshot.objects.is_none()
        && snapshot.fish_profiles.is_empty()
        && snapshot.environment.is_none()
        && snapshot.terrain.is_none()
        && snapshot.ocean.is_none()
        && snapshot.local_lights.is_empty()
        && snapshot.placement_companions.is_empty()
    {
        return Ok(None);
    }
    snapshot.files = paths
        .into_iter()
        .map(|path| {
            module
                .files
                .iter()
                .find(|file| file.path == path)
                .cloned()
                .ok_or_else(|| "Module resource is outside its admitted inventory".to_owned())
        })
        .collect::<Result<_, _>>()?;
    Ok(Some(snapshot))
}

fn restore_ocean(
    origin: &OriginDocuments,
    area: forge_core::AreaId,
    snapshot: &mut Snapshot,
    paths: &mut BTreeSet<String>,
) -> Result<(), String> {
    let Some(binding) = origin
        .module
        .ocean_inputs
        .iter()
        .find(|binding| binding.area_id == area)
    else {
        return Ok(());
    };
    forge_delivery::module_ocean::resolve(&origin.module, area, |path, maximum| {
        origin.read_payload(path, maximum)
    })?
    .ok_or("Declared module ocean disappeared")?;
    for package in std::iter::once(&binding.package)
        .chain(binding.surface.iter())
        .chain(binding.shore_sim.iter())
    {
        insert(paths, &package.manifest);
        for pin in &package.resources {
            insert(paths, pin);
        }
    }
    snapshot.ocean = Some(binding.clone());
    Ok(())
}

fn restore_local_lights(
    origin: &OriginDocuments,
    area: forge_core::AreaId,
    snapshot: &mut Snapshot,
    paths: &mut BTreeSet<String>,
) -> Result<(), String> {
    let module = &origin.module;
    // Reuse admission of source descriptors, original inputs and licences.
    forge_delivery::module_local_lights::resolve(module, area, |path, maximum| {
        origin.read_payload(path, maximum)
    })?;
    for package in module
        .local_light_packages
        .iter()
        .filter(|package| package.areas.contains(&area))
    {
        insert(paths, &package.manifest);
        for pin in &package.resources {
            insert(paths, pin);
        }
        snapshot.local_lights.push(package.clone());
    }
    Ok(())
}

fn restore_fish(
    origin: &OriginDocuments,
    area: forge_core::AreaId,
    snapshot: &mut Snapshot,
    paths: &mut BTreeSet<String>,
) -> Result<(), String> {
    let module = &origin.module;
    let fish: Vec<_> = module
        .fish_profiles
        .iter()
        .filter(|binding| binding.area_id == area)
        .cloned()
        .collect();
    if !fish.is_empty() {
        let resolved = forge_delivery::module_fish::resolve(module, area, |path, maximum| {
            origin.read_payload(path, maximum)
        })?;
        if resolved.len() != fish.len() {
            return Err("Declared module fish profiles disappeared".into());
        }
        for binding in &fish {
            insert(paths, &binding.package.manifest);
            insert(paths, &binding.catalog);
            for pin in &binding.package.resources {
                insert(paths, pin);
            }
        }
        snapshot.fish_profiles = fish;
    }
    Ok(())
}

fn restore_terrain(
    origin: &OriginDocuments,
    head: &ZoneAuthoringHead,
    snapshot: &mut Snapshot,
    paths: &mut BTreeSet<String>,
) -> Result<(), String> {
    let module = &origin.module;
    let area = head.base.terrain.area_id;
    if let Some(binding) = module
        .terrain_surfaces
        .iter()
        .find(|binding| binding.area_id == area)
    {
        let document = head.effective()?.0;
        forge_delivery::module_terrain::resolve(module, area, &document, |path, maximum| {
            origin.read_payload(path, maximum)
        })?
        .ok_or("Declared module terrain disappeared")?;
        let forge_project::module_package::ModuleTerrainSource::SourceRaster { package, .. } =
            &binding.source;
        insert(paths, &package.manifest);
        for pin in &package.resources {
            insert(paths, pin);
        }
        snapshot.terrain = Some(binding.clone());
    }
    Ok(())
}
