//! One acquired terrain renderer over the existing collision/partition host.
//! Package admission is external; this owner never reads files or selects a map.
use super::{
    lod::{LodMaterial, ViewGeometry},
    resident_tiles::{PreparedTiles, ResidentTiles, TileAdmission},
    scene,
};
use crate::{source_environment::scene::ResidentEnvironment, source_lighting::surfaces::live};
#[cfg(feature = "physics-rapier")]
use bevy::light::NotShadowCaster;
use bevy::{prelude::*, tasks::Task};
use forge_assets::source_terrain_package::TerrainPackage;
use forge_core::ContentHash;
use forge_world::sampled_terrain::SampledTerrainPlan;
use std::sync::{Arc, atomic::Ordering};
mod updates;
pub use updates::{release_inventory, request_inventory};

pub struct PreparedHost {
    plan: Arc<SampledTerrainPlan>,
    heights: Vec<u8>,
    package: TerrainPackage,
    identity: ContentHash,
    relief: ContentHash,
    domain: Vec4,
    water_context: WaterContext,
}

#[derive(Clone, Copy)]
enum WaterContext {
    Unspecified,
    Explicit(Option<f64>),
}

impl WaterContext {
    fn level(self) -> Result<Option<f64>, String> {
        match self {
            Self::Unspecified => Err("Terrain water context absent".into()),
            Self::Explicit(level) => Ok(level),
        }
    }
}

#[derive(SystemSet, Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct TerrainHostSet;

#[derive(Resource, Clone, Debug, Default, serde::Serialize)]
pub struct Status {
    pub document: Option<ContentHash>,
    pub package: Option<ContentHash>,
    pub pending: bool,
    pub waiting_for_surface: bool,
    pub active_tiles: usize,
    pub cached_tiles: usize,
    pub normals_ready: bool,
    pub staged_frame: Option<u32>,
    pub error: Option<String>,
}

impl Status {
    fn observe_normals(&mut self, ready: bool, frame: u32) {
        self.normals_ready = ready;
        if ready {
            self.staged_frame.get_or_insert(frame);
        } else {
            self.staged_frame = None;
        }
    }
}

#[derive(Resource)]
struct Pending(Option<PreparedHost>);
type Prepared = updates::PreparedUpdate;
#[derive(Resource)]
struct State {
    document: ContentHash,
    relief: ContentHash,
    domain: Vec4,
    water_level_m: Option<f64>,
    task: Option<Task<Result<Prepared, String>>>,
    queued: Option<PreparedHost>,
    serial: u64,
    running_serial: u64,
    inventory_generation: u64,
    inventory: Option<updates::Inventory>,
    geometry: Option<ViewGeometry>,
    resident: Option<scene::ResidentTerrain>,
    tiles: ResidentTiles,
    view_cadence: crate::scene_camera::SourceViewCadence,
    selected_nodes: Vec<forge_world::terrain_lod::CdlodNode>,
    shore: Handle<Image>,
}
#[cfg(feature = "physics-rapier")]
#[derive(Component)]
/// Partition geometry hidden by this terrain host, retained for spatial queries.
/// Only the host can assign the admitted document identity.
pub struct PhysicsProjection(pub(crate) ContentHash);

impl PreparedHost {
    pub fn new(
        plan: Arc<SampledTerrainPlan>,
        heights: Vec<u8>,
        package: TerrainPackage,
        identity: ContentHash,
    ) -> Result<Self, String> {
        let source = plan.raster().source();
        if heights.len() != source.byte_length().map_err(|error| error.to_string())?
            || forge_core::bytes_sha256(&heights) != source.samples_sha256
        {
            return Err("Source terrain host requires its verified raster".into());
        }
        if package.identity() != identity {
            return Err("Terrain package identity differs from its admitted input".into());
        }
        scene::require_matching_package(&plan, &package)?;
        let relief = super::sampling::relief_identity(&plan)?;
        let size = plan
            .document()
            .extent_m
            .size_x_m
            .max(plan.document().extent_m.size_z_m);
        if !size.is_normal() || !(size * 0.5).is_normal() {
            return Err("Terrain domain exceeds shader precision".into());
        }
        let resolution = u16::try_from(source.width.max(source.height))
            .map(f32::from)
            .map_err(|_| "Terrain resolution exceeds the admitted domain")?;
        let domain = Vec4::new(-size * 0.5, size, resolution, 0.58);
        let water_context = if super::supports_fixed_source_raster(source) {
            WaterContext::Explicit(Some(0.0))
        } else {
            WaterContext::Unspecified
        };
        Ok(Self {
            plan,
            heights,
            package,
            identity,
            relief,
            domain,
            water_context,
        })
    }

    /// Whether the caller must explicitly supply the scene's water level (or
    /// explicitly state that no water cutoff applies) before installation.
    #[must_use]
    pub const fn requires_water_context(&self) -> bool {
        matches!(self.water_context, WaterContext::Unspecified)
    }

    /// Provide the actual scene water level for AO; `None` declares no cutoff.
    pub fn with_water_level(mut self, level_m: Option<f64>) -> Result<Self, String> {
        if level_m.is_some_and(|level| !level.is_finite()) {
            return Err("Nonfinite terrain AO water level".into());
        }
        self.water_context = WaterContext::Explicit(level_m);
        Ok(self)
    }

    pub fn install(self, app: &mut App) -> Result<(), String> {
        if self.requires_water_context() {
            return Err("Terrain requires an explicit water context before installation".into());
        }
        if app.world().contains_resource::<Pending>()
            || app.world().contains_resource::<State>()
            || app.world().contains_resource::<scene::ResidentTerrain>()
            || !live::installed(app.world())
        {
            return Err(
                "Source terrain requires its exclusive terrain and admitted surface scene".into(),
            );
        }
        scene::register(app)?;
        app.insert_resource(Status {
            document: Some(self.plan.document_hash()),
            package: Some(self.identity),
            pending: true,
            ..default()
        })
        .insert_resource(Pending(Some(self)))
        .add_systems(PostStartup, start.after(live::SurfaceHostSet));
        #[cfg(feature = "physics-rapier")]
        app.add_systems(PostUpdate, suppress_cells.before(live::SurfaceHostSet));
        app.add_systems(
            PostUpdate,
            update
                .in_set(TerrainHostSet)
                .after(live::SurfaceHostSet)
                .after(bevy::transform::TransformSystems::Propagate),
        );
        Ok(())
    }
}

fn view(
    world: &mut World,
) -> Result<
    (
        Entity,
        Transform,
        PerspectiveProjection,
        forge_world::terrain_lod::CdlodView,
    ),
    String,
> {
    let (camera, _) = crate::scene_camera::primary(world)?;
    let transform = world
        .get::<Transform>(camera)
        .ok_or("Terrain camera pose absent")?;
    let Some(Projection::Perspective(projection)) = world.get::<Projection>(camera) else {
        return Err("Terrain source requires finite perspective".into());
    };
    let source = crate::scene_camera::source_view(transform, projection)?;
    Ok((camera, *transform, projection.clone(), source))
}

fn start(world: &mut World) {
    let Some(input) = world.resource_mut::<Pending>().0.take() else {
        return;
    };
    let result = (|| {
        view(world)?;
        let document = input.plan.document_hash();
        let relief = input.relief;
        let domain = input.domain;
        let water_level_m = input.water_context.level()?;
        let task = updates::spawn(input, None);
        let shore = world.resource_mut::<Assets<Image>>().add(Image::default());
        world.insert_resource(State {
            document,
            relief,
            domain,
            water_level_m,
            task: Some(task),
            queued: None,
            serial: 1,
            running_serial: 1,
            inventory_generation: 0,
            inventory: None,
            geometry: None,
            resident: None,
            tiles: default(),
            view_cadence: default(),
            selected_nodes: Vec::new(),
            shore,
        });
        Ok::<_, String>(())
    })();
    if let Err(error) = result {
        fail(world, error);
    }
}

/// Only the admitted partition projections are hidden; colliders and identities stay intact.
#[cfg(feature = "physics-rapier")]
fn suppress_cells(world: &mut World) {
    let Some(document) = world.get_resource::<State>().map(|state| state.document) else {
        return;
    };
    let cells: Vec<_> = world
        .query_filtered::<(
            Entity,
            Option<&crate::cell_streaming::StreamedTerrainCell>,
            Option<&crate::cell_streaming::StreamedTerrainHlod>,
        ), Or<(
            With<crate::cell_streaming::StreamedTerrainCell>,
            With<crate::cell_streaming::StreamedTerrainHlod>,
        )>>()
        .iter(world)
        .filter_map(|(entity, cell, distant)| {
            (cell.is_some_and(|v| v.source == document)
                || distant.is_some_and(|v| v.source == document))
            .then_some(entity)
        })
        .collect();
    for entity in cells {
        if !owns_physics_projection(world, entity) {
            world.entity_mut(entity).insert((
                PhysicsProjection(document),
                Visibility::Hidden,
                NotShadowCaster,
            ));
        }
    }
}

#[must_use]
#[cfg(feature = "physics-rapier")]
pub fn owns_physics_projection(world: &World, entity: Entity) -> bool {
    let Some(state) = world.get_resource::<State>() else {
        return false;
    };
    world
        .get::<PhysicsProjection>(entity)
        .is_some_and(|p| p.0 == state.document)
        && world.get::<Visibility>(entity) == Some(&Visibility::Hidden)
        && (world
            .get::<crate::cell_streaming::StreamedTerrainCell>(entity)
            .is_some_and(|c| c.source == state.document)
            || world
                .get::<crate::cell_streaming::StreamedTerrainHlod>(entity)
                .is_some_and(|c| c.source == state.document))
}

#[must_use]
pub fn owns(world: &World, entity: Entity) -> bool {
    world.get_resource::<State>().is_some_and(|state| {
        state.tiles.owns(world, entity)
            && state
                .resident
                .as_ref()
                .and_then(|r| r.material.as_ref())
                .is_some_and(|material| {
                    world
                        .get::<MeshMaterial3d<LodMaterial>>(entity)
                        .is_some_and(|m| &m.0 == material)
                })
    })
}

/// Cached terrain sources eligible for a bounded typed renderer inventory.
#[must_use]
pub fn retained_meshes(world: &World) -> Option<Vec<Handle<Mesh>>> {
    let state = world.get_resource::<State>()?;
    let root = state.resident.as_ref()?.entity?;
    if !owns(world, root) {
        return None;
    }
    state.tiles.retained_meshes(world)
}

/// Prepare the exact source-LOD selection for a possible future view without
/// changing the live scene. The caller owns the candidate and its GPU request.
pub fn prepare_view(
    world: &mut World,
    view: forge_world::terrain_lod::CdlodView,
) -> Result<PreparedTiles<LodMaterial>, String> {
    prepare_views(world, std::slice::from_ref(&view))
}

/// Prepare the union of bounded possible views without publishing a selection.
pub fn prepare_views(
    world: &mut World,
    views: &[forge_world::terrain_lod::CdlodView],
) -> Result<PreparedTiles<LodMaterial>, String> {
    prepare_views_inner(world, views, None)?.ok_or("Terrain selection was not prepared".into())
}

/// Prepare a bounded batch of missing tiles from the supplied future views.
/// Returns `None` when every tile is resident or the renderer budget is full.
pub fn prepare_missing_views(
    world: &mut World,
    views: &[forge_world::terrain_lod::CdlodView],
    maximum_new: usize,
) -> Result<Option<PreparedTiles<LodMaterial>>, String> {
    prepare_views_inner(world, views, Some(maximum_new))
}

fn prepare_views_inner(
    world: &mut World,
    views: &[forge_world::terrain_lod::CdlodView],
    maximum_new: Option<usize>,
) -> Result<Option<PreparedTiles<LodMaterial>>, String> {
    if views.is_empty() || views.len() > 32 {
        return Err("Terrain preparation requires one to 32 views".into());
    }
    require_preparation_ready(world)?;
    world.resource_scope(|world, state: Mut<State>| {
        if state.task.is_some() || state.queued.is_some() {
            return Err("Terrain source update is still pending".into());
        }
        let geometry = state.geometry.as_ref().ok_or("Terrain bounds absent")?;
        if geometry.document_hash != state.document {
            return Err("Terrain bounds belong to another document".into());
        }
        let selections = views
            .iter()
            .map(|view| geometry.nodes(*view))
            .collect::<Result<Vec<_>, _>>()?;
        let nodes = super::resident_tiles::merge_selections(
            selections,
            crate::render_inventory::MAX_SCOPED_MESHES,
        )?;
        let root = state
            .resident
            .as_ref()
            .and_then(|resident| resident.entity)
            .ok_or("Terrain root absent")?;
        let build = |node: &forge_world::terrain_lod::CdlodNode| {
            ViewGeometry::mesh_from_nodes(std::slice::from_ref(node))
                .expect("One selected tile")
                .0
        };
        if let Some(maximum_new) = maximum_new {
            state.tiles.prepare_missing::<LodMaterial>(
                world,
                root,
                &nodes,
                crate::render_inventory::MAX_SCOPED_MESHES,
                maximum_new,
                build,
            )
        } else {
            state
                .tiles
                .prepare::<LodMaterial>(
                    world,
                    root,
                    &nodes,
                    crate::render_inventory::MAX_SCOPED_MESHES,
                    build,
                )
                .map(Some)
        }
    })
}

/// Cache the already GPU-admitted future terrain without publishing its view.
/// A caller that later selects it must still fence that scene publication.
pub fn retain_prepared(
    world: &mut World,
    candidate: &PreparedTiles<LodMaterial>,
    token: &crate::render_inventory::RenderInventoryToken,
    admission: &TileAdmission,
) -> Result<usize, String> {
    require_preparation_ready(world)?;
    world.resource_scope(|world, mut state: Mut<State>| {
        if state.task.is_some() || state.queued.is_some() {
            return Err("Terrain source update is still pending".into());
        }
        state
            .tiles
            .retain_prepared(world, candidate, token, admission)
    })
}

fn require_preparation_ready(world: &World) -> Result<(), String> {
    let status = world
        .get_resource::<Status>()
        .ok_or("Terrain status absent")?;
    let state = world.get_resource::<State>().ok_or("Terrain host absent")?;
    if status.pending
        || status.error.is_some()
        || status.document != Some(state.document)
        || live::snapshot(world).is_none_or(|surface| surface.terrain_relief != state.relief)
    {
        return Err("Terrain source is not current for preparation".into());
    }
    Ok(())
}

/// Hide a resident terrain as soon as the live shadow field belongs to another
/// relief. The caller keeps the request pending until the desired field arrives.
fn surface_matches(
    world: &mut World,
    resident: Option<(Entity, ContentHash)>,
    desired: ContentHash,
    observed: Option<ContentHash>,
) -> Result<bool, String> {
    if let Some((root, relief)) = resident
        && observed != Some(relief)
    {
        world
            .get_entity_mut(root)
            .map_err(|_| "Terrain root disappeared while waiting for its surface")?
            .insert(Visibility::Hidden);
    }
    Ok(observed == Some(desired))
}

fn mark_pending(status: &mut Status) {
    status.pending = true;
    status.normals_ready = false;
    status.staged_frame = None;
}

fn surface_ready_for_update(world: &mut World, state: &State) -> Result<bool, String> {
    let observed = live::snapshot(world).map(|snapshot| snapshot.terrain_relief);
    let resident = state
        .resident
        .as_ref()
        .and_then(|terrain| terrain.entity.map(|entity| (entity, terrain.relief)));
    let matched = surface_matches(world, resident, state.relief, observed)?;
    let mut status = world.resource_mut::<Status>();
    status.waiting_for_surface = !matched;
    if !matched {
        mark_pending(&mut status);
    }
    Ok(matched)
}

fn resident_ready_for_update(world: &mut World, state: &State) -> Result<bool, String> {
    let Some(terrain) = state.resident.as_ref() else {
        mark_pending(&mut world.resource_mut::<Status>());
        return Ok(false);
    };
    let changed_relief = terrain.relief != state.relief;
    let changed_water =
        terrain.water_level_m.map(f64::to_bits) != state.water_level_m.map(f64::to_bits);
    if changed_relief || changed_water || terrain.document_hash != state.document {
        if (changed_relief || changed_water)
            && let Some(root) = terrain.entity
        {
            world
                .get_entity_mut(root)
                .map_err(|_| "Terrain root disappeared while preparing its replacement")?
                .insert(Visibility::Hidden);
        }
        mark_pending(&mut world.resource_mut::<Status>());
        return Ok(false);
    }
    Ok(true)
}

/// Retained offscreen children are not current draw requirements. Keep the root
/// in the inventory even when every selected tile is outside the view.
#[must_use]
pub fn inactive_tile(world: &World, entity: Entity) -> bool {
    owns(world, entity)
        && world.get::<Visibility>(entity) == Some(&Visibility::Hidden)
        && world
            .get_resource::<State>()
            .and_then(|s| s.resident.as_ref())
            .is_some_and(|r| r.entity != Some(entity))
}

struct ShoreBindings {
    state: Handle<Image>,
    lace: Handle<Image>,
    params: [UVec4; 3],
}

fn shore_bindings(
    world: &World,
    document: ContentHash,
    fallback: &Handle<Image>,
) -> Result<ShoreBindings, String> {
    let Some(shore) = world.get_resource::<crate::source_shore_sim::Targets>() else {
        return Ok(ShoreBindings {
            state: fallback.clone(),
            lace: fallback.clone(),
            params: [UVec4::ZERO; 3],
        });
    };
    if shore.document_hash != document {
        return Err("Resident shore simulation belongs to another terrain revision".into());
    }
    Ok(ShoreBindings {
        state: shore.state.clone(),
        lace: shore.lace.clone(),
        params: shore.params,
    })
}

fn bindings(
    world: &World,
    document: ContentHash,
    shore: &Handle<Image>,
    domain: Vec4,
) -> Result<scene::Bindings, String> {
    let snapshot = live::snapshot(world).ok_or("Admitted surface frame absent")?;
    let receiver =
        crate::source_lighting::surfaces::Receiver::from_scene(world, snapshot.handle, true)?;
    let env = world
        .get_resource::<ResidentEnvironment>()
        .ok_or("Admitted terrain environment absent")?;
    let shore = shore_bindings(world, document, shore)?;
    Ok(scene::Bindings {
        environment: receiver.environment,
        hill_shadow: receiver.hill,
        object_shadow: receiver.objects,
        object_settings: receiver.object_settings,
        clouds: receiver.clouds,
        cloud_shadow: Some(receiver.cloud_shadow),
        shore_state: shore.state,
        shore_lace: shore.lace,
        shore_params: shore.params,
        caustics: world
            .get_resource::<crate::source_surface::underwater::CausticMaterialInputs>()
            .cloned(),
        settings: super::TerrainSettings {
            wind: snapshot.material_wind,
            sun: env.scene_lighting.sun_direction,
            domain,
            flags: Vec4::ZERO,
        },
    })
}

fn update(world: &mut World) {
    if !world.contains_resource::<State>() || world.resource::<Status>().error.is_some() {
        return;
    }
    let result = world.resource_scope(|world, mut state: Mut<State>| -> Result<(), String> {
        if !surface_ready_for_update(world, &state)? {
            return Ok(());
        }
        if !updates::prepare(world, &mut state)? {
            return Ok(());
        }
        if !resident_ready_for_update(world, &state)? {
            return Ok(());
        }
        let (camera, transform, projection, source) = view(world)?;
        let refresh = state
            .view_cadence
            .needs_refresh(camera, &transform, &projection);
        let nodes = if refresh {
            state
                .geometry
                .as_ref()
                .ok_or("Terrain bounds absent")?
                .nodes(source)?
        } else {
            state.selected_nodes.clone()
        };
        let resident = state
            .resident
            .as_ref()
            .ok_or("Terrain projection absent")?
            .clone();
        let root = resident.entity.ok_or("Terrain root absent")?;
        let material = resident
            .material
            .as_ref()
            .ok_or("Terrain material absent")?;
        let current = world
            .get::<MeshMaterial3d<LodMaterial>>(root)
            .ok_or("Terrain root replaced")?;
        if &current.0 != material {
            return Err("Terrain root material replaced".into());
        }
        let next = bindings(world, state.document, &state.shore, state.domain)?;
        let assets = world.resource::<Assets<LodMaterial>>();
        let mut changed = assets
            .get(material)
            .ok_or("Terrain material retired")?
            .base
            .clone();
        changed.environment = next.environment;
        changed.hill_shadow = next.hill_shadow;
        changed.object_shadow = next.object_shadow;
        changed.object_settings = next.object_settings;
        changed.clouds = next.clouds;
        changed.cloud_shadow = next.cloud_shadow;
        changed.shore_state = next.shore_state;
        changed.shore_lace = next.shore_lace;
        changed.shore_params = next.shore_params;
        changed.settings = next.settings;
        if assets
            .get(material)
            .is_none_or(|current| current.base != changed)
        {
            world
                .resource_mut::<Assets<LodMaterial>>()
                .get_mut(material)
                .ok_or("Terrain material absent")?
                .base = changed;
        }
        state
            .tiles
            .select::<LodMaterial>(world, root, &nodes, |node| {
                ViewGeometry::mesh_from_nodes(std::slice::from_ref(node))
                    .expect("One selected tile")
                    .0
            });
        if refresh {
            state.selected_nodes = nodes;
            state.view_cadence.record(camera, &transform, &projection);
        }
        let counts = state.tiles.counts();
        let normals_ready = resident.normals.ready.load(Ordering::Acquire);
        let frame = world.resource::<bevy::diagnostic::FrameCount>().0;
        let mut status = world.resource_mut::<Status>();
        status.pending = state.task.is_some() || state.queued.is_some();
        status.active_tiles = counts[0];
        status.cached_tiles = counts[1];
        // Normal mip generation can finish after geometry staging. Require a
        // queue completion from its observed ready frame, including revocation.
        status.observe_normals(normals_ready, frame);
        Ok(())
    });
    if let Err(error) = result {
        fail(world, error);
    }
}

/// Physics and a real typed renderer fence are both required; CPU handles cannot release play.
#[must_use]
pub fn ready(world: &World, token: Option<&crate::render_inventory::RenderInventoryToken>) -> bool {
    let Some(status) = world.get_resource::<Status>() else {
        return false;
    };
    let Some(state) = world.get_resource::<State>() else {
        return false;
    };
    !status.pending
        && !status.waiting_for_surface
        && status.error.is_none()
        && status.normals_ready
        && state.task.is_none()
        && state.queued.is_none()
        && state.resident.as_ref().is_some_and(|resident| {
            resident.document_hash == state.document
                && resident.relief == state.relief
                && resident.water_level_m.map(f64::to_bits) == state.water_level_m.map(f64::to_bits)
        })
        && live::snapshot(world).is_some_and(|surface| surface.terrain_relief == state.relief)
        && state
            .geometry
            .as_ref()
            .is_some_and(|geometry| geometry.document_hash == state.document)
        && state.resident.is_some()
        && state.geometry.is_some()
        && state
            .resident
            .as_ref()
            .and_then(|resident| resident.entity)
            .is_some_and(|entity| owns(world, entity))
        && status
            .staged_frame
            .is_some_and(|frame| token.is_some_and(|token| token.covers_frame(world, frame)))
        && physics_partition_ready(world, state.document)
        && state
            .tiles
            .entities()
            .iter()
            .all(|entity| owns(world, *entity))
}

#[cfg(feature = "physics-rapier")]
fn physics_partition_ready(world: &World, document: ContentHash) -> bool {
    crate::cell_streaming::source_is_synchronized(world, document)
}

// Without the physical streaming backend, never claim its synchronization.
#[cfg(not(feature = "physics-rapier"))]
fn physics_partition_ready(_world: &World, _document: ContentHash) -> bool {
    false
}

fn fail(world: &mut World, error: String) {
    error!(%error, "Admitted terrain source projection failed");
    world.resource_mut::<Status>().error = Some(error);
    #[cfg(feature = "physics-rapier")]
    if let Some(mut ready) = world.get_resource_mut::<crate::PlayerSimulationReady>() {
        ready.0 = false;
    }
    world.write_message(bevy::app::AppExit::error());
}

#[cfg(all(test, not(feature = "physics-rapier")))]
mod minimal_tests {
    use super::*;

    #[test]
    fn physical_partition_is_not_reported_synchronized_without_backend() {
        assert!(!physics_partition_ready(
            &World::new(),
            forge_core::bytes_sha256(b"terrain")
        ));
    }
}

#[cfg(all(test, feature = "physics-rapier"))]
mod tests {
    use super::*;
    use bevy_rapier3d::prelude::Collider;
    use forge_core::EntityId;
    use forge_world::cell_streaming::{CellDetail, CellKey};

    #[test]
    fn shore_binding_requires_the_current_terrain_revision() {
        let mut world = World::new();
        let document = forge_core::bytes_sha256(b"terrain revision one");
        let other = forge_core::bytes_sha256(b"terrain revision two");
        let fallback = Handle::<Image>::default();
        let neutral = shore_bindings(&world, document, &fallback).unwrap();
        assert_eq!(neutral.state, fallback);
        assert_eq!(neutral.lace, fallback);
        assert_eq!(neutral.params, [UVec4::ZERO; 3]);
        let mut images = Assets::<Image>::default();
        let state = images.add(Image::default());
        let lace = images.add(Image::default());
        let params = [UVec4::splat(42); 3];
        world.insert_resource(crate::source_shore_sim::Targets {
            state: state.clone(),
            lace: lace.clone(),
            params,
            document_hash: document,
            surface_package_sha256: forge_core::bytes_sha256(b"surface"),
            completed_steps: std::sync::Arc::new(std::sync::atomic::AtomicU32::new(0)),
            ready: std::sync::Arc::new(std::sync::atomic::AtomicBool::new(false)),
            error: std::sync::Arc::new(std::sync::Mutex::new(None)),
        });
        let bound = shore_bindings(&world, document, &fallback).unwrap();
        assert_eq!(
            (bound.state, bound.lace, bound.params),
            (state, lace, params)
        );
        assert!(shore_bindings(&world, other, &fallback).is_err());
    }

    fn state(document: ContentHash) -> State {
        State {
            document,
            relief: document,
            domain: Vec4::new(-1024.0, 2048.0, 2048.0, 0.58),
            water_level_m: Some(0.0),
            task: None,
            queued: None,
            serial: 1,
            running_serial: 1,
            inventory_generation: 0,
            inventory: None,
            geometry: None,
            resident: None,
            tiles: default(),
            view_cadence: default(),
            selected_nodes: Vec::new(),
            shore: default(),
        }
    }
    fn cell(document: ContentHash) -> crate::cell_streaming::StreamedTerrainCell {
        crate::cell_streaming::StreamedTerrainCell {
            logical_id: EntityId::deterministic("terrain-host-cell"),
            key: CellKey { x: 0, z: 0 },
            detail: CellDetail::Full,
            source: document,
        }
    }
    #[test]
    fn only_the_owned_partition_is_hidden_and_its_collider_and_identity_are_preserved() {
        let mut world = World::new();
        let document = forge_core::bytes_sha256(b"source-terrain");
        world.insert_resource(state(document));
        let collider = Collider::cuboid(1.0, 1.0, 1.0);
        let raw = collider.raw.clone();
        let entity = world
            .spawn((cell(document), collider, Visibility::Inherited))
            .id();
        let other = world
            .spawn((
                cell(forge_core::bytes_sha256(b"another")),
                Visibility::Inherited,
            ))
            .id();
        let ordinary = world.spawn(Visibility::Inherited).id();
        suppress_cells(&mut world);
        assert!(owns_physics_projection(&world, entity));
        assert!(std::ptr::eq(
            raw.as_ref(),
            world.get::<Collider>(entity).unwrap().raw.as_ref()
        ));
        assert_eq!(
            world
                .get::<crate::cell_streaming::StreamedTerrainCell>(entity)
                .unwrap()
                .logical_id,
            EntityId::deterministic("terrain-host-cell")
        );
        assert_eq!(world.get::<Visibility>(other), Some(&Visibility::Inherited));
        assert_eq!(
            world.get::<Visibility>(ordinary),
            Some(&Visibility::Inherited)
        );
        world.entity_mut(entity).insert(Visibility::Inherited);
        assert!(!owns_physics_projection(&world, entity));
        world.entity_mut(entity).insert(Visibility::Hidden);
        world.resource_mut::<State>().document = forge_core::bytes_sha256(b"new-source");
        assert!(!owns_physics_projection(&world, entity));
    }
    #[test]
    fn cpu_status_and_a_marker_cannot_replace_admission_or_a_renderer_fence() {
        let mut world = World::new();
        let document = forge_core::bytes_sha256(b"terrain-marker");
        let entity = world
            .spawn((PhysicsProjection(document), Visibility::Hidden))
            .id();
        assert!(!owns_physics_projection(&world, entity));
        assert!(!owns(&world, entity));
        world.insert_resource(state(document));
        world.insert_resource(Status {
            document: Some(document),
            pending: false,
            normals_ready: true,
            staged_frame: Some(1),
            ..default()
        });
        assert!(!owns_physics_projection(&world, entity));
        assert!(!ready(&world, None));
    }
    #[test]
    fn delayed_or_revoked_normal_generation_requires_a_later_completion_frame() {
        let mut status = Status::default();
        status.observe_normals(false, 10);
        assert!(status.staged_frame.is_none());
        status.observe_normals(true, 24);
        assert_eq!(status.staged_frame, Some(24));
        status.observe_normals(true, 25);
        assert_eq!(status.staged_frame, Some(24));
        status.observe_normals(false, 31);
        assert!(!status.normals_ready);
        assert!(status.staged_frame.is_none());
        status.observe_normals(true, 35);
        assert_eq!(status.staged_frame, Some(35));
        assert!(status.normals_ready);
    }

    #[test]
    fn changed_or_missing_surface_hides_stale_terrain_until_reliefs_match() {
        let mut world = World::new();
        let old = forge_core::bytes_sha256(b"old relief");
        let next = forge_core::bytes_sha256(b"new relief");
        let foreign = forge_core::bytes_sha256(b"unrelated relief");
        let root = world.spawn(Visibility::Inherited).id();

        // The old scene remains coherent while the requested replacement waits.
        assert!(!surface_matches(&mut world, Some((root, old)), next, Some(old)).unwrap());
        assert_eq!(world.get::<Visibility>(root), Some(&Visibility::Inherited));
        // Once the new shadow field arrives, the old terrain must disappear
        // until its matching geometry has been staged and selected.
        assert!(surface_matches(&mut world, Some((root, old)), next, Some(next)).unwrap());
        assert_eq!(world.get::<Visibility>(root), Some(&Visibility::Hidden));

        world.entity_mut(root).insert(Visibility::Inherited);
        assert!(!surface_matches(&mut world, Some((root, next)), next, Some(foreign)).unwrap());
        assert_eq!(world.get::<Visibility>(root), Some(&Visibility::Hidden));
        world.entity_mut(root).insert(Visibility::Inherited);
        assert!(!surface_matches(&mut world, Some((root, next)), next, None).unwrap());
        assert_eq!(world.get::<Visibility>(root), Some(&Visibility::Hidden));
        world.despawn(root);
        assert!(surface_matches(&mut world, Some((root, next)), next, None).is_err());
    }
    #[test]
    fn an_obsolete_worker_failure_cannot_publish_or_poison_the_newest_request() {
        let pool = bevy::tasks::TaskPoolBuilder::new().num_threads(1).build();
        let mut pending = state(forge_core::bytes_sha256(b"new-request"));
        pending.serial = 2;
        pending.running_serial = 1;
        pending.task = Some(pool.spawn(async { Err("obsolete CPU failure".to_owned()) }));
        let mut world = World::new();
        world.insert_resource(Status {
            pending: true,
            ..default()
        });
        let started = std::time::Instant::now();
        while pending.task.is_some() {
            assert!(!updates::prepare(&mut world, &mut pending).unwrap());
            assert!(started.elapsed() < std::time::Duration::from_secs(1));
            std::thread::sleep(std::time::Duration::from_millis(1));
        }
        assert!(pending.resident.is_none() && pending.geometry.is_none());
        assert!(world.resource::<Status>().error.is_none());
        assert!(world.resource::<Status>().pending);
    }
}
