//! The common terrain codec over the module's current-head chunk channel.
use super::{Acquired, Reply, Request, Snapshot, read};
use forge_assets::source_terrain_package::TerrainPackage;
use forge_core::ContentHash;
use forge_project::module_package::{
    ModuleFile, ModuleFilePin, ModuleFileRole, ModuleTerrainFiles, ModuleTerrainSource,
    terrain::{MAX_MODULE_TERRAIN_BYTES, MAX_TERRAIN_PACKAGE_RESOURCES},
};
use std::{cell::RefCell, collections::BTreeSet, future::Future, rc::Rc};

pub(super) async fn acquire<F, Fut>(
    snapshot: &Snapshot,
    previous: Option<&Acquired>,
    transport: &RefCell<F>,
    current: &impl Fn() -> bool,
) -> Result<(Option<ContentHash>, Option<Rc<TerrainPackage>>), String>
where
    F: FnMut(Request) -> Fut,
    Fut: Future<Output = Result<Reply, String>>,
{
    let Some(binding) = &snapshot.terrain else {
        return Ok((None, None));
    };
    let ModuleTerrainSource::SourceRaster { package, .. } = &binding.source;
    validate_inventory(snapshot, package)?;
    let index = &package.manifest;
    if let Some(decoded) = previous
        .filter(|old| old.terrain_index == Some(index.sha256))
        .filter(|old| old.snapshot.terrain == snapshot.terrain)
        .filter(|old| {
            std::iter::once(index)
                .chain(&package.resources)
                .all(|pin| file(snapshot, pin).ok() == file(&old.snapshot, pin).ok())
        })
        .and_then(|old| old.terrain.clone())
    {
        return Ok((Some(index.sha256), Some(decoded)));
    }
    let raw = read(snapshot, &index.path, 16 * 1024, transport, current).await?;
    let parent = index.path.rsplit_once('/').map_or("", |(parent, _)| parent);
    let pins = TerrainPackage::resource_pins(&raw, index.sha256)?;
    let licence_name = TerrainPackage::manifest_license_name(&raw, index.sha256)?;
    if pins.len() != package.resources.len() {
        return Err("Module terrain closure differs from its index".into());
    }
    for (name, expected) in &pins {
        let path = joined(parent, name);
        let pin = package
            .resources
            .iter()
            .find(|pin| pin.path == path)
            .ok_or("Module terrain resource is outside its declared closure")?;
        let entry = file(snapshot, pin)?;
        if pin.sha256 != expected.sha256
            || entry.bytes != expected.bytes as u64
            || (name == licence_name && entry.role != ModuleFileRole::License)
        {
            return Err("Module terrain index pin, size or licence differs".into());
        }
    }
    let decoded = TerrainPackage::load_async(&raw, index.sha256, |name, maximum| {
        let path = joined(parent, &name);
        async move { read(snapshot, &path, maximum, transport, current).await }
    })
    .await?;
    Ok((Some(index.sha256), Some(Rc::new(decoded))))
}

fn validate_inventory(snapshot: &Snapshot, package: &ModuleTerrainFiles) -> Result<(), String> {
    if package.resources.is_empty() || package.resources.len() > MAX_TERRAIN_PACKAGE_RESOURCES {
        return Err("Module terrain resource count exceeds its closed profile".into());
    }
    if file(snapshot, &package.manifest)?.role != ModuleFileRole::TerrainSurface {
        return Err("Module terrain index role differs".into());
    }
    let mut paths = BTreeSet::new();
    let mut total = 0_u64;
    for pin in std::iter::once(&package.manifest).chain(&package.resources) {
        let entry = file(snapshot, pin)?;
        if !paths.insert(&pin.path)
            || !matches!(
                entry.role,
                ModuleFileRole::TerrainSurface | ModuleFileRole::License
            )
        {
            return Err("Module terrain closure contains a duplicate or wrong file role".into());
        }
        total = total
            .checked_add(entry.bytes)
            .ok_or("Module terrain size overflow")?;
        if total > MAX_MODULE_TERRAIN_BYTES {
            return Err("Module terrain exceeds its admitted budget".into());
        }
    }
    Ok(())
}

fn file<'a>(snapshot: &'a Snapshot, pin: &ModuleFilePin) -> Result<&'a ModuleFile, String> {
    let mut entries = snapshot.files.iter().filter(|file| file.path == pin.path);
    let entry = entries.next().ok_or("Module terrain file is absent")?;
    if entries.next().is_some() || entry.sha256 != pin.sha256 {
        return Err("Module terrain file is ambiguous or repinned".into());
    }
    Ok(entry)
}

fn joined(parent: &str, name: &str) -> String {
    if parent.is_empty() {
        name.to_owned()
    } else {
        format!("{parent}/{name}")
    }
}
