//! Shared acquisition for browser/native consumers of the pinned module protocol.
//! Existing asset codecs own package interpretation; this owns transport and cancellation.
use crate::module_resources_protocol::{Assembly, Reply, Request, Snapshot};
use forge_assets::{VegetationPrototypeCatalog, source_vegetation_package::PlantPackage};
use forge_core::{AreaId, ContentHash, ModuleId};
use forge_project::{
    authoring::AuthoringState, module_package::ModuleVegetationSource, workspace::ModuleOriginPin,
};
use std::{cell::RefCell, collections::BTreeMap, future::Future, rc::Rc};
mod fish;
mod lights;
pub mod ocean;
mod surfaces;
mod terrain;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Scope {
    pub head: ContentHash,
    pub module: ModuleId,
    pub area: AreaId,
    pub origin: Option<ModuleOriginPin>,
    pub vegetation: Option<ContentHash>,
    pub terrain: Option<ContentHash>,
    pub imported: bool,
    pub species: bool,
}

/// Renderer lifetime: a local module has no received-package identity.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct SceneScope {
    module: ModuleId,
    area: AreaId,
    origin: Option<ModuleOriginPin>,
}

/// A module identity and generation captured before any asynchronous read.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct SourceToken {
    scope: Scope,
    epoch: u64,
}

impl SourceToken {
    pub fn capture(owned: bool, scope: Option<Scope>, epoch: u64) -> Result<Self, String> {
        Ok(Self {
            scope: Scope::require_current_source(owned, scope)?,
            epoch,
        })
    }

    #[must_use]
    pub const fn scope(self) -> Scope {
        self.scope
    }

    pub fn ensure_current(
        self,
        owned: bool,
        scope: Option<Scope>,
        epoch: u64,
    ) -> Result<(), String> {
        if !owned || scope != Some(self.scope) || epoch != self.epoch {
            return Err("Chargement des ressources du module remplacé".into());
        }
        Ok(())
    }

    /// Restart an unfinished read on any generation change; a ready projection
    /// only needs replacement when its module, area or origin changes.
    #[must_use]
    pub fn projection_needs_restart(
        previous: Option<Self>,
        next: Option<Self>,
        ready: bool,
    ) -> bool {
        let scene = |token: Option<Self>| token.map(|token| token.scope.scene());
        scene(previous) != scene(next) || (!ready && previous != next)
    }
}

impl Scope {
    /// A caller which captured a module head must not treat its replacement as
    /// an absent package; a current local module may still have no origin.
    pub fn require_current_source(owned: bool, scope: Option<Self>) -> Result<Self, String> {
        if !owned {
            return Err("Chargement des ressources du module remplacé".into());
        }
        scope.ok_or_else(|| "Origine du module indisponible".into())
    }

    #[must_use]
    pub fn owns_source(state: &AuthoringState) -> bool {
        state.module_origin.is_some() || state.module_creator_context.is_some()
    }

    #[must_use]
    pub fn package(self) -> Option<ContentHash> {
        self.origin.map(|origin| origin.package_sha256)
    }

    #[must_use]
    pub fn scene(self) -> SceneScope {
        SceneScope {
            module: self.module,
            area: self.area,
            origin: self.origin,
        }
    }

    pub fn from_state(state: &AuthoringState) -> Result<Option<Self>, String> {
        if !Self::owns_source(state) {
            return Ok(None);
        }
        let origin = state.module_origin;
        if let Some(origin) = origin {
            origin.validate().map_err(|error| error.to_string())?;
        }
        let module = state.gameplay_module_id.ok_or("Module identity absent")?;
        if state
            .module_creator_context
            .as_ref()
            .is_some_and(|context| context.module_id != module)
        {
            return Err("Managed module identity differs from its authoring context".into());
        }
        let head = state
            .zone_head
            .as_ref()
            .ok_or("Managed module without an aggregate authoring head")?;
        let (terrain, vegetation) = head.effective()?;
        Ok(Some(Self {
            head: head.content_hash()?,
            module,
            area: head.base.terrain.area_id,
            origin,
            terrain: terrain
                .sampled_source
                .as_ref()
                .map(forge_core::canonical_json_sha256)
                .transpose()
                .map_err(|error| error.to_string())?,
            vegetation: vegetation
                .as_ref()
                .map(|value| value.content_hash().map_err(|error| error.to_string()))
                .transpose()?,
            imported: vegetation
                .as_ref()
                .is_some_and(|value| value.imported_source.is_some()),
            species: head
                .corrections
                .species
                .as_ref()
                .is_some_and(|value| !value.is_empty()),
        }))
    }
}

pub type ModuleFishProfile = forge_assets::source_fish_payload::OwnedFishProfile<
    forge_assets::source_fish_package::PinnedFishPayload,
>;

pub struct Acquired {
    pub snapshot: Snapshot,
    pub ocean: Option<Rc<ocean::Packages>>,
    pub terrain_index: Option<ContentHash>,
    pub terrain: Option<Rc<forge_assets::source_terrain_package::TerrainPackage>>,
    pub imported_index: Option<ContentHash>,
    pub imported: Option<Rc<PlantPackage>>,
    pub catalogs: BTreeMap<ContentHash, Rc<VegetationPrototypeCatalog>>,
    pub models: BTreeMap<ContentHash, Rc<Vec<u8>>>,
    pub objects_index: Option<ContentHash>,
    pub objects: Option<Rc<forge_assets::source_surface_package::SurfacePackage>>,
    pub fish_profiles: Vec<Rc<ModuleFishProfile>>,
    pub environment_index: Option<ContentHash>,
    pub environment: Option<Rc<forge_assets::source_environment_package::EnvironmentPackage>>,
    pub programs_index: Option<ContentHash>,
    pub programs: Option<Rc<forge_assets::source_environment_package::programs::ProgramsPackage>>,
    pub local_lights: BTreeMap<
        ContentHash,
        std::sync::Arc<forge_assets::source_local_lights::SourceLocalLightsPackage>,
    >,
}

pub async fn acquire<F, Fut>(
    scope: Scope,
    previous: Option<Rc<Acquired>>,
    request: F,
    current: impl Fn() -> bool,
) -> Result<Option<Acquired>, String>
where
    F: FnMut(Request) -> Fut,
    Fut: Future<Output = Result<Reply, String>>,
{
    ensure_current(&current)?;
    let transport = RefCell::new(request);
    let future = transport.borrow_mut()(Request::Describe {
        expected_head: scope.head,
    });
    let reply = future.await?;
    ensure_current(&current)?;
    let Reply::Description { snapshot } = reply else {
        return Err("Unexpected module resource description".into());
    };
    let Some(snapshot) = snapshot else {
        return if scope.imported || scope.species {
            Err("The module omitted its authored vegetation companions".into())
        } else {
            Ok(None)
        };
    };
    let snapshot = *snapshot;
    verify_snapshot(&snapshot, scope)?;
    let imported_pin = imported_pin(&snapshot, scope)?;
    let imported_index = imported_pin.map(|pin| pin.sha256);
    let imported = if let Some(index) = imported_pin {
        if let Some(package) = previous
            .as_ref()
            .filter(|old| old.imported_index == Some(index.sha256))
            .and_then(|old| old.imported.clone())
        {
            Some(package)
        } else {
            let raw = read(&snapshot, &index.path, 256 * 1024, &transport, &current).await?;
            if forge_core::bytes_sha256(&raw) != index.sha256 {
                return Err("Module plant index pin differs".into());
            }
            let parent = index.path.rsplit_once('/').map_or("", |(parent, _)| parent);
            let package = PlantPackage::load_async(&raw, index.sha256, |name, maximum| {
                let path = if parent.is_empty() {
                    name
                } else {
                    format!("{parent}/{name}")
                };
                let snapshot = &snapshot;
                let transport = &transport;
                let current = &current;
                async move { read(snapshot, &path, maximum, transport, current).await }
            })
            .await?;
            Some(Rc::new(package))
        }
    } else {
        None
    };
    let (catalogs, models) =
        acquire_species(&snapshot, previous.as_deref(), &transport, &current).await?;
    let (objects_index, objects) =
        surfaces::objects(&snapshot, previous.as_deref(), &transport, &current).await?;
    let fish_profiles = fish::acquire(&snapshot, previous.as_deref(), &transport, &current).await?;
    let environment =
        surfaces::environment(&snapshot, previous.as_deref(), &transport, &current).await?;
    let local_lights =
        lights::acquire(&snapshot, previous.as_deref(), &transport, &current).await?;
    let (terrain_index, terrain) =
        terrain::acquire(&snapshot, previous.as_deref(), &transport, &current).await?;
    let ocean = ocean::acquire(&snapshot, previous.as_deref(), &transport, &current).await?;
    ensure_current(&current)?;
    Ok(Some(Acquired {
        snapshot,
        ocean,
        terrain_index,
        terrain,
        imported_index,
        imported,
        catalogs,
        models,
        objects_index,
        objects,
        fish_profiles,
        environment_index: environment.index,
        environment: environment.environment,
        programs_index: environment.programs_index,
        programs: environment.programs,
        local_lights,
    }))
}

fn imported_pin(
    snapshot: &Snapshot,
    scope: Scope,
) -> Result<Option<&forge_project::module_package::ModuleFilePin>, String> {
    let imported = snapshot
        .vegetation
        .as_ref()
        .into_iter()
        .flat_map(|binding| &binding.sources)
        .find_map(|source| match source {
            ModuleVegetationSource::ImportedPrototypes { index, .. } => Some(index),
            ModuleVegetationSource::GovernedSpecies { .. } => None,
        });
    let governed = snapshot
        .vegetation
        .as_ref()
        .into_iter()
        .flat_map(|binding| &binding.sources)
        .any(|source| matches!(source, ModuleVegetationSource::GovernedSpecies { .. }));
    if imported.is_some() != scope.imported
        || governed != scope.species
        || snapshot.species.is_some() != scope.species
    {
        return Err("Module vegetation source families differ from the authored scope".into());
    }
    Ok(imported)
}

fn verify_snapshot(snapshot: &Snapshot, scope: Scope) -> Result<(), String> {
    let valid_package = match scope.package() {
        Some(pin) => !snapshot.legacy_reference && snapshot.package_sha256 == pin,
        None => {
            snapshot.legacy_reference
                && scope.imported
                && !scope.species
                && snapshot.vegetation.is_some()
                && snapshot.fish_profiles.is_empty()
                && snapshot.terrain.is_none()
                && snapshot.ocean.is_none()
                && snapshot.local_lights.is_empty()
                && snapshot.placement_companions.is_empty()
                && snapshot.species.is_none()
        }
    };
    if snapshot.head_hash != scope.head
        || snapshot.module_id != scope.module
        || snapshot.area_id != scope.area
        || snapshot.vegetation.as_ref().is_some_and(|binding| {
            binding.area_id != scope.area || Some(binding.vegetation_sha256) != scope.vegetation
        })
        || snapshot
            .objects
            .as_ref()
            .is_some_and(|binding| binding.area_id != scope.area)
        || snapshot
            .fish_profiles
            .iter()
            .any(|binding| binding.area_id != scope.area)
        || snapshot
            .environment
            .as_ref()
            .is_some_and(|binding| binding.area_id != scope.area)
        || snapshot
            .ocean
            .as_ref()
            .is_some_and(|binding| binding.area_id != scope.area)
        || !valid_package
        || snapshot.terrain.as_ref().is_some_and(|binding| {
            let forge_project::module_package::ModuleTerrainSource::SourceRaster {
                source_descriptor_sha256,
                ..
            } = &binding.source;
            binding.area_id != scope.area || Some(*source_descriptor_sha256) != scope.terrain
        })
        || snapshot
            .local_lights
            .iter()
            .any(|package| !package.areas.contains(&scope.area))
    {
        return Err("Module resource description differs from its current canonical scope".into());
    }
    let mut companions = std::collections::BTreeSet::new();
    for binding in &snapshot.placement_companions {
        if !companions.insert((binding.kind, binding.pin())) {
            return Err("Duplicate module placement companion".into());
        }
        for (pin, role) in [
            (
                &binding.package,
                forge_project::module_package::ModuleFileRole::PlacementCompanion,
            ),
            (
                &binding.proof,
                forge_project::module_package::ModuleFileRole::Provenance,
            ),
        ] {
            let mut files = snapshot.files.iter().filter(|file| file.path == pin.path);
            if files
                .next()
                .is_none_or(|file| file.role != role || file.sha256 != pin.sha256)
                || files.next().is_some()
            {
                return Err("Module placement companion inventory differs".into());
            }
        }
    }
    Ok(())
}

/// Read only an exact retained companion. Absence permits a separate provider;
/// stale or corrupt origin bytes always return an error instead.
pub async fn read_companion<F, Fut>(
    scope: Scope,
    snapshot: &Snapshot,
    kind: forge_project::module_package::ModulePlacementCompanionKind,
    pin: forge_project::zone_authoring::session::SourceCompanionPin,
    request: F,
    current: impl Fn() -> bool,
) -> Result<Option<Vec<u8>>, String>
where
    F: FnMut(Request) -> Fut,
    Fut: Future<Output = Result<Reply, String>>,
{
    verify_snapshot(snapshot, scope)?;
    ensure_current(&current)?;
    let Some(binding) = snapshot
        .placement_companions
        .iter()
        .find(|binding| binding.kind == kind && binding.pin() == pin)
    else {
        return Ok(None);
    };
    let maximum = match kind {
        forge_project::module_package::ModulePlacementCompanionKind::Physics => {
            forge_assets::source_physics_package::MAX_PACKAGE_BYTES
        }
        forge_project::module_package::ModulePlacementCompanionKind::Particles => {
            forge_assets::source_particle_package::MAX_PACKAGE_BYTES
        }
    };
    let wire = read(
        snapshot,
        &binding.package.path,
        maximum,
        &RefCell::new(request),
        &current,
    )
    .await?;
    match kind {
        forge_project::module_package::ModulePlacementCompanionKind::Physics => {
            forge_assets::source_physics_package::SourcePhysicsPackage::decode_pinned(
                &wire,
                pin.carrier_sha256,
                pin.manifest_sha256,
                pin.package_sha256,
            )?;
        }
        forge_project::module_package::ModulePlacementCompanionKind::Particles => {
            forge_assets::source_particle_package::SourceParticlePackage::decode_pinned(
                &wire,
                pin.carrier_sha256,
                pin.manifest_sha256,
                pin.package_sha256,
            )?;
        }
    }
    Ok(Some(wire))
}

type SpeciesData = (
    BTreeMap<ContentHash, Rc<VegetationPrototypeCatalog>>,
    BTreeMap<ContentHash, Rc<Vec<u8>>>,
);

async fn acquire_species<F, Fut>(
    snapshot: &Snapshot,
    previous: Option<&Acquired>,
    transport: &RefCell<F>,
    current: &impl Fn() -> bool,
) -> Result<SpeciesData, String>
where
    F: FnMut(Request) -> Fut,
    Fut: Future<Output = Result<Reply, String>>,
{
    let mut catalogs = BTreeMap::new();
    for source in snapshot
        .vegetation
        .as_ref()
        .into_iter()
        .flat_map(|binding| &binding.sources)
    {
        if let ModuleVegetationSource::GovernedSpecies { catalogs: pins, .. } = source {
            for pin in pins {
                let catalog = if let Some(catalog) = previous
                    .as_ref()
                    .and_then(|old| old.catalogs.get(&pin.sha256))
                    .cloned()
                {
                    catalog
                } else {
                    let raw = read(snapshot, &pin.path, 1024 * 1024, transport, current).await?;
                    if forge_core::bytes_sha256(&raw) != pin.sha256 {
                        return Err("Module species catalogue pin differs".into());
                    }
                    Rc::new(serde_json::from_slice(&raw).map_err(|error| error.to_string())?)
                };
                catalogs.insert(pin.sha256, catalog);
            }
        }
    }
    let mut models = BTreeMap::new();
    if let Some(species) = &snapshot.species {
        let mut total = 0;
        for pin in species.assets.values() {
            if models.contains_key(&pin.sha256) {
                continue;
            }
            let bytes = if let Some(bytes) = previous
                .as_ref()
                .and_then(|old| old.models.get(&pin.sha256))
                .cloned()
            {
                bytes
            } else {
                Rc::new(read(snapshot, &pin.path, 256 * 1024 * 1024, transport, &current).await?)
            };
            if forge_core::bytes_sha256(&bytes) != pin.sha256 {
                return Err("Module species model pin differs".into());
            }
            total += bytes.len();
            if total > 256 * 1024 * 1024 {
                return Err("Module species models exceed 256 MiB".into());
            }
            models.insert(pin.sha256, bytes);
        }
    }
    Ok((catalogs, models))
}

fn ensure_current(current: &impl Fn() -> bool) -> Result<(), String> {
    if current() {
        Ok(())
    } else {
        Err("Module resource acquisition was replaced".into())
    }
}

async fn read<F, Fut>(
    snapshot: &Snapshot,
    path: &str,
    maximum: usize,
    transport: &RefCell<F>,
    current: &impl Fn() -> bool,
) -> Result<Vec<u8>, String>
where
    F: FnMut(Request) -> Fut,
    Fut: Future<Output = Result<Reply, String>>,
{
    let mut matches = snapshot
        .files
        .iter()
        .enumerate()
        .filter(|(_, file)| file.path == path);
    let (index, file) = matches
        .next()
        .ok_or("Module resource absent from its snapshot")?;
    if matches.next().is_some() || file.bytes > maximum as u64 {
        return Err("Module resource is ambiguous or exceeds its budget".into());
    }
    let mut assembly = Assembly::new(snapshot, index)?;
    while let Some(offset) = assembly.next_offset() {
        ensure_current(current)?;
        let future = transport.borrow_mut()(Request::Read {
            expected_head: snapshot.head_hash,
            expected_package: snapshot.package_sha256,
            resource_index: index,
            offset,
        });
        let reply = future.await?;
        ensure_current(current)?;
        let Reply::Resource { chunk } = reply else {
            return Err("Unexpected module resource response".into());
        };
        assembly.accept(chunk)?;
    }
    assembly.finish()
}
