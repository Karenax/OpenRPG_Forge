//! Acquire the module's declared fish profiles using the common checked codecs.
use super::{Acquired, ModuleFishProfile, Reply, Request, Snapshot, read};
use forge_assets::{
    AssetCatalog,
    source_fish_package::{PinnedFishPayload, resource_index},
    source_fish_payload::{OwnedFishProfile, check_catalog_pins},
};
use forge_core::{ContentHash, bytes_sha256};
use forge_project::module_package::{
    ModuleFile, ModuleFilePin, ModuleFileRole, ModuleFishBinding,
    fish::{MAX_FISH_BYTES, MAX_FISH_PROFILES, MAX_FISH_RESOURCES},
};
use std::{
    cell::RefCell,
    collections::{BTreeMap, BTreeSet},
    future::Future,
    rc::Rc,
};

pub(super) async fn acquire<F, Fut>(
    snapshot: &Snapshot,
    previous: Option<&Acquired>,
    transport: &RefCell<F>,
    current: &impl Fn() -> bool,
) -> Result<Vec<Rc<ModuleFishProfile>>, String>
where
    F: FnMut(Request) -> Fut,
    Fut: Future<Output = Result<Reply, String>>,
{
    let inventory = preflight(snapshot)?;
    if let Some(old) = previous.filter(|old| {
        old.snapshot.package_sha256 == snapshot.package_sha256
            && old.snapshot.module_id == snapshot.module_id
            && old.snapshot.area_id == snapshot.area_id
            && old.snapshot.fish_profiles == snapshot.fish_profiles
            && preflight(&old.snapshot).is_ok_and(|files| files == inventory)
    }) {
        return Ok(old.fish_profiles.clone());
    }
    let mut profiles = Vec::new();
    for binding in &snapshot.fish_profiles {
        let raw = read(
            snapshot,
            &binding.package.manifest.path,
            256 * 1024,
            transport,
            current,
        )
        .await?;
        let index = resource_index(&raw, binding.package.manifest.sha256)?;
        let (parent, _) = binding
            .package
            .manifest
            .path
            .rsplit_once('/')
            .ok_or("Fish package directory absent")?;
        let supplied = binding
            .resource_inventory(&snapshot.files)
            .map_err(|error| error.to_string())?;
        let runtime_resources = index.runtime_resources(&binding.excluded_source_scripts)?;
        let expected: BTreeMap<_, _> = runtime_resources
            .iter()
            .map(|(name, pin)| (name.clone(), (pin.bytes as u64, pin.sha256)))
            .collect();
        if supplied != expected {
            return Err("Fish resource closure differs from its index".into());
        }
        let license = format!("{parent}/{}", index.license);
        if !inventory
            .get(license.as_str())
            .is_some_and(|file| file.role == ModuleFileRole::License)
        {
            return Err("Fish source license is absent from the module resources".into());
        }
        let catalog_bytes = read(
            snapshot,
            &binding.catalog.path,
            4 * 1024 * 1024,
            transport,
            current,
        )
        .await?;
        if bytes_sha256(&catalog_bytes) != binding.catalog.sha256 {
            return Err("Fish catalog pin differs".into());
        }
        let catalog: AssetCatalog =
            serde_json::from_slice(&catalog_bytes).map_err(|error| error.to_string())?;
        let expected: Vec<_> = binding
            .assets
            .iter()
            .map(|asset| (asset.asset_id, asset.sha256))
            .collect();
        check_catalog_pins(&catalog, &expected)?;
        let payload = PinnedFishPayload::load_async(&runtime_resources, |name, maximum| {
            let path = format!("{parent}/{name}");
            async move { read(snapshot, &path, maximum, transport, current).await }
        })
        .await?;
        profiles.push(Rc::new(OwnedFishProfile::new(
            binding.package.manifest.sha256,
            payload,
            catalog,
            binding.description.clone(),
            (
                usize::try_from(binding.prototypes).map_err(|error| error.to_string())?,
                usize::try_from(binding.instances).map_err(|error| error.to_string())?,
            ),
        )?));
    }
    Ok(profiles)
}

fn preflight(snapshot: &Snapshot) -> Result<BTreeMap<&str, &ModuleFile>, String> {
    if snapshot.fish_profiles.is_empty() {
        return Ok(BTreeMap::new());
    }
    if snapshot.fish_profiles.len() > MAX_FISH_PROFILES {
        return Err("Too many fish profiles".into());
    }
    let mut files = BTreeMap::new();
    for file in &snapshot.files {
        if files.insert(file.path.as_str(), file).is_some() {
            return Err("Ambiguous module resource inventory".into());
        }
    }
    let mut selected = BTreeMap::new();
    let mut profiles = BTreeSet::new();
    let mut assets = BTreeSet::new();
    for binding in &snapshot.fish_profiles {
        validate_binding(snapshot, binding, &mut profiles, &mut assets)?;
        binding
            .resource_inventory(&snapshot.files)
            .map_err(|error| error.to_string())?;
        let mut local = BTreeSet::new();
        for (pin, limit, required_role) in std::iter::once((
            &binding.package.manifest,
            256 * 1024,
            Some(ModuleFileRole::ObjectSource),
        ))
        .chain(std::iter::once((
            &binding.catalog,
            4 * 1024 * 1024,
            Some(ModuleFileRole::ObjectSource),
        )))
        .chain(
            binding
                .package
                .resources
                .iter()
                .map(|pin| (pin, MAX_FISH_BYTES, None)),
        ) {
            if !local.insert(pin.path.as_str()) {
                return Err("Repeated fish resource".into());
            }
            let file = checked_file(&files, pin, limit, required_role)?;
            selected.insert(pin.path.as_str(), file);
        }
        let description = binding
            .package
            .manifest
            .path
            .rsplit_once('/')
            .map(|(parent, _)| format!("{parent}/{}", binding.description))
            .ok_or("Fish package directory absent")?;
        if !binding
            .package
            .resources
            .iter()
            .any(|pin| pin.path == description)
        {
            return Err("Fish description is not declared".into());
        }
    }
    let total = selected.values().try_fold(0_u64, |sum, file| {
        sum.checked_add(file.bytes)
            .ok_or("Fish resource byte count overflow")
    })?;
    if selected.len() > MAX_FISH_RESOURCES || total > MAX_FISH_BYTES {
        return Err("Fish module resource budget exceeded".into());
    }
    Ok(selected)
}

fn validate_binding(
    snapshot: &Snapshot,
    binding: &ModuleFishBinding,
    profiles: &mut BTreeSet<ContentHash>,
    assets: &mut BTreeSet<forge_core::AssetId>,
) -> Result<(), String> {
    binding
        .validate_inventory()
        .map_err(|error| error.to_string())?;
    if binding.area_id != snapshot.area_id
        || !profiles.insert(binding.package.manifest.sha256)
        || binding
            .assets
            .iter()
            .any(|asset| !assets.insert(asset.asset_id))
    {
        return Err("Invalid module fish profile identity or inventory".into());
    }
    Ok(())
}

fn checked_file<'a>(
    files: &BTreeMap<&str, &'a ModuleFile>,
    pin: &ModuleFilePin,
    limit: u64,
    role: Option<ModuleFileRole>,
) -> Result<&'a ModuleFile, String> {
    let file = files
        .get(pin.path.as_str())
        .copied()
        .ok_or("Fish resource absent")?;
    if file.sha256 != pin.sha256 || file.bytes > limit || role.is_some_and(|role| role != file.role)
    {
        return Err("Fish resource pin, role or size differs".into());
    }
    Ok(file)
}
