//! The existing water codecs over the same pinned, current-head resource channel.
use super::{Acquired, Reply, Request, Snapshot, read};
use forge_assets::{
    source_ocean_package::OceanPackage, source_shore_sim_package::ShoreSimPackage,
    source_water_surface_package::WaterSurfacePackage,
};
use forge_project::module_package::{ModuleFileRole, ModuleOceanFiles, ocean::close_resources};
use std::{cell::RefCell, collections::BTreeMap, future::Future, rc::Rc, sync::Arc};

#[derive(Debug)]
pub struct Packages {
    pub ocean: OceanPackage,
    pub surface: Option<Arc<WaterSurfacePackage>>,
    pub shore: Option<ShoreSimPackage>,
}

pub(super) async fn acquire<F, Fut>(
    snapshot: &Snapshot,
    previous: Option<&Acquired>,
    transport: &RefCell<F>,
    current: &impl Fn() -> bool,
) -> Result<Option<Rc<Packages>>, String>
where
    F: FnMut(Request) -> Fut,
    Fut: Future<Output = Result<Reply, String>>,
{
    let Some(binding) = &snapshot.ocean else {
        return Ok(None);
    };
    if binding.area_id != snapshot.area_id
        || binding.surface.is_some() != binding.shore_sim.is_some()
    {
        return Err("Ocean binding is incomplete or belongs to another area".into());
    }
    let unique: std::collections::BTreeSet<_> =
        snapshot.files.iter().map(|file| &file.path).collect();
    if unique.len() != snapshot.files.len() {
        return Err("Duplicate ocean inventory file".into());
    }
    let packages = || {
        std::iter::once(&binding.package)
            .chain(binding.surface.iter())
            .chain(binding.shore_sim.iter())
    };
    let old_file =
        |old: &Snapshot, path: &str| old.files.iter().find(|file| file.path == path).cloned();
    if let Some(cached) = previous
        .filter(|old| old.snapshot.ocean == snapshot.ocean)
        .filter(|old| {
            packages()
                .flat_map(|package| std::iter::once(&package.manifest).chain(&package.resources))
                .all(|pin| old_file(&old.snapshot, &pin.path) == old_file(snapshot, &pin.path))
        })
        .and_then(|old| old.ocean.clone())
    {
        return Ok(Some(cached));
    }
    let index = &binding.package.manifest;
    let raw = read(snapshot, &index.path, 256 * 1024, transport, current).await?;
    validate(
        snapshot,
        &binding.package,
        OceanPackage::resource_pins(&raw, index.sha256)?
            .into_iter()
            .map(|(name, pin)| (name, pin.sha256, pin.bytes)),
        ModuleFileRole::OceanSimulation,
        OceanPackage::license_name(),
    )?;
    let ocean = OceanPackage::load_async(&raw, index.sha256, |name, maximum| {
        let path = joined(&index.path, &name);
        async move { read(snapshot, &path, maximum, transport, current).await }
    })
    .await?;
    let surface = if let Some(package) = &binding.surface {
        let index = &package.manifest;
        let raw = read(snapshot, &index.path, 12 * 1024, transport, current).await?;
        validate(
            snapshot,
            package,
            WaterSurfacePackage::resource_pins(&raw, index.sha256)?
                .into_iter()
                .map(|(name, pin)| (name, pin.sha256, pin.bytes)),
            ModuleFileRole::OceanSurface,
            WaterSurfacePackage::license_name(),
        )?;
        Some(Arc::new(
            WaterSurfacePackage::load_async(&raw, index.sha256, |name, maximum| {
                let path = joined(&index.path, &name);
                async move { read(snapshot, &path, maximum, transport, current).await }
            })
            .await?,
        ))
    } else {
        None
    };
    let shore = if let Some(package) = &binding.shore_sim {
        let index = &package.manifest;
        let raw = read(snapshot, &index.path, 12 * 1024, transport, current).await?;
        validate(
            snapshot,
            package,
            ShoreSimPackage::resource_pins(&raw, index.sha256)?
                .into_iter()
                .map(|(name, pin)| (name, pin.sha256, pin.bytes)),
            ModuleFileRole::ShoreSimulation,
            ShoreSimPackage::license_name(),
        )?;
        Some(
            ShoreSimPackage::load_async(&raw, index.sha256, |name, maximum| {
                let path = joined(&index.path, &name);
                async move { read(snapshot, &path, maximum, transport, current).await }
            })
            .await?,
        )
    } else {
        None
    };
    Ok(Some(Rc::new(Packages {
        ocean,
        surface,
        shore,
    })))
}

fn joined(index: &str, name: &str) -> String {
    index
        .rsplit_once('/')
        .map_or_else(|| name.to_owned(), |(parent, _)| format!("{parent}/{name}"))
}

fn validate(
    snapshot: &Snapshot,
    package: &ModuleOceanFiles,
    declared: impl Iterator<Item = (String, forge_core::ContentHash, usize)>,
    role: ModuleFileRole,
    license: &str,
) -> Result<(), String> {
    let files: BTreeMap<_, _> = snapshot
        .files
        .iter()
        .map(|file| (file.path.as_str(), file))
        .collect();
    if files.len() != snapshot.files.len() {
        return Err("Duplicate ocean inventory file".into());
    }
    if files
        .get(package.manifest.path.as_str())
        .is_none_or(|file| file.sha256 != package.manifest.sha256 || file.role != role)
    {
        return Err("Ocean manifest role or identity differs from its binding".into());
    }
    close_resources(&files, package, declared, role, license)?;
    Ok(())
}
