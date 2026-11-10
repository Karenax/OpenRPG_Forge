//! Governed manual meshes use the existing authored placement and collision contracts.
use bevy::{
    asset::{LoadState, RecursiveDependencyLoadState},
    gltf::{GltfAssetLabel, GltfExtras},
    prelude::*,
    world_serialization::{WorldAsset, WorldAssetRoot, WorldInstance, WorldInstanceSpawner},
};
use forge_assets::governed_import::{GovernedAssetKind, GovernedImportCatalog};
use forge_delivery::module_runtime::LoadedModuleZone;
use forge_engine::{
    ManualPlacementCollision, ManualPlacementCollisionExpected, ManualPlacementCollisionGeometry,
    ManualPlacementCollisionPlugin, ManualPlacementPart, ManualPlacementProjection,
    source_fish::host::{live as fish_live, scene as fish_scene},
};
use forge_project::zone_authoring::{placements::ManualPlacement, source_objects};
use std::{
    collections::{BTreeMap, BTreeSet},
    sync::{Arc, OnceLock},
};
mod decals;

#[derive(Resource)]
pub struct PreparedManual {
    governed: Vec<(ManualPlacement, String)>,
    source: Vec<ManualPlacement>,
    graph: Vec<PreparedGraph>,
    decals: Vec<(ManualPlacement, Arc<[u8]>)>,
    objects: Option<PreparedObjects>,
    fish_placements: Vec<ManualPlacement>,
    fish: Option<fish_scene::PreparedScene>,
}

struct PreparedGraph {
    placement: ManualPlacement,
    carrier: Arc<[u8]>,
    companions: super::companions::Prepared,
}

struct PreparedObjects {
    shader: String,
    host: forge_engine::source_surface::package::host::PreparedHost,
}

#[derive(Clone)]
enum Carrier {
    Ordinary,
    Graph(Arc<[u8]>),
    Decal(Arc<[u8]>),
}
type GraphCache = BTreeMap<forge_core::ContentHash, Carrier>;

fn graph_carrier(
    loaded: &LoadedModuleZone,
    path: &str,
    pin: forge_core::ContentHash,
    cache: &mut GraphCache,
) -> Result<Carrier, String> {
    if let Some(cached) = cache.get(&pin) {
        return Ok(cached.clone());
    }
    let file = loaded
        .manifest
        .files
        .iter()
        .find(|file| file.path == path && file.sha256 == pin)
        .ok_or("Manual source carrier differs from its admitted inventory")?;
    let length = usize::try_from(file.bytes).map_err(|error| error.to_string())?;
    // The ordinary route uses the existing native carrier budget too; it must
    // not inherit a smaller limit from the old diagnostic-only caller.
    forge_engine::source_graph_route::carrier_budget().validate_byte_length(length)?;
    let model = loaded.read_payload(path, length)?;
    let carrier = if forge_assets::source_decal_glb::source_decal_declared_in_glb(
        &model,
        forge_engine::source_graph_route::carrier_budget(),
    )? {
        Carrier::Decal(Arc::from(model))
    } else if forge_engine::source_graph_route::read(&model)?.is_some() {
        Carrier::Graph(Arc::from(model))
    } else {
        Carrier::Ordinary
    };
    cache.insert(pin, carrier.clone());
    Ok(carrier)
}

fn prepare_objects(
    loaded: &LoadedModuleZone,
    source: &[ManualPlacement],
    profile: Option<forge_delivery::module_objects::ResolvedObjects>,
) -> Result<Option<PreparedObjects>, String> {
    if source.is_empty() {
        return Ok(None);
    }
    if !loaded
        .manifest
        .environments
        .iter()
        .any(|binding| binding.area_id == loaded.zone.head().base.terrain.area_id)
    {
        return Err("Source object materials require the area's declared environment".into());
    }
    let profile = profile.ok_or("Source object palette absent")?;
    let shader = forge_engine::source_surface::package::shader_source(&profile.package)?.to_owned();
    Ok(Some(PreparedObjects {
        shader,
        host: forge_engine::source_surface::package::host::PreparedHost::new(
            &profile.package,
            source.to_vec(),
        )?,
    }))
}

fn prepare_fish(
    loaded: &LoadedModuleZone,
    profiles: &[forge_delivery::module_fish::ResolvedFish],
    placements: &[ManualPlacement],
    objects: Option<&forge_delivery::module_objects::ResolvedObjects>,
) -> Result<Option<fish_scene::PreparedScene>, String> {
    if placements.is_empty() {
        return Ok(None);
    }
    if !loaded
        .manifest
        .environments
        .iter()
        .any(|binding| binding.area_id == loaded.zone.head().base.terrain.area_id)
    {
        return Err("Fish materials require the area's declared environment".into());
    }
    let shader = if let Some(objects) = objects {
        if placements.iter().any(|placement| {
            objects
                .package
                .catalog()
                .assets
                .iter()
                .any(|asset| asset.id == placement.asset_id)
        }) {
            return Err("Manual asset identity is shared by fish and object profiles".into());
        }
        forge_engine::source_surface::package::shader_source(&objects.package)?
    } else {
        forge_engine::source_surface::PROFILE_PROGRAM
    };
    fish_scene::from_catalogs(
        profiles.iter(),
        placements,
        shader,
        forge_engine::source_fish::surface::SpecularMode::ThreeReference,
    )
}

impl PreparedManual {
    #[must_use]
    pub fn has_source_graphs(&self) -> bool {
        !self.graph.is_empty()
    }

    /// The explicitly admitted fish placements; collision policy remains a
    /// separate property so future non-colliding objects are not classified as fish.
    pub fn fish_ids(&self) -> BTreeSet<forge_core::EntityId> {
        self.fish_placements.iter().map(|item| item.id).collect()
    }

    pub fn non_colliding_ids(&self) -> BTreeSet<forge_core::EntityId> {
        let mut ids = self.fish_ids();
        ids.extend(self.decals.iter().map(|(placement, _)| placement.id));
        ids
    }

    pub fn accepted(&self) -> Vec<ManualPlacement> {
        let mut placements = self
            .governed
            .iter()
            .map(|(placement, _)| placement.clone())
            .chain(self.source.iter().cloned())
            .chain(self.graph.iter().map(|graph| graph.placement.clone()))
            .chain(self.decals.iter().map(|(placement, _)| placement.clone()))
            .chain(self.fish_placements.iter().cloned())
            .collect::<Vec<_>>();
        placements.sort_by_key(|placement| placement.id);
        placements
    }
}

#[derive(Component)]
struct ManualSceneHandle(Handle<WorldAsset>);

/// Source scenes mounted by the common engine, including ordinary module launches.
#[derive(Component)]
struct ModuleSourceGraph;

/// Keep the first actual load refusal available to the session report, including
/// failures raised before the first complete gameplay/renderer observation.
#[derive(Resource, Clone, Default)]
pub(super) struct ModuleAssetFailure(Arc<OnceLock<String>>);

impl ModuleAssetFailure {
    pub(super) fn reason(&self) -> Option<&str> {
        self.0.get().map(String::as_str)
    }
}

pub(super) fn refuse(world: &mut World, reason: String) {
    world.init_resource::<ModuleAssetFailure>();
    let failure = world.resource::<ModuleAssetFailure>().clone();
    if failure.0.set(reason).is_err() {
        return;
    }
    error!(
        "{}",
        failure.reason().expect("The first refusal was recorded")
    );
    world.write_message(bevy::app::AppExit::error());
}

#[allow(
    clippy::too_many_lines,
    reason = "Resolve each placement once against its admitted host and shared source packets"
)]
pub fn prepare(
    loaded: &LoadedModuleZone,
    source_graph_qualification: bool,
) -> Result<PreparedManual, String> {
    let pin = loaded
        .manifest
        .files
        .iter()
        .find(|file| file.path == loaded.manifest.asset_catalog_path)
        .ok_or("Module asset catalog is absent")?;
    let bytes = loaded.read_payload(
        &pin.path,
        forge_project::module_package::MAX_MODULE_MANIFEST_BYTES,
    )?;
    let catalog: GovernedImportCatalog =
        serde_json::from_slice(&bytes).map_err(|error| error.to_string())?;
    let head = loaded.zone.head();
    let mut placements = head.corrections.placements.clone().unwrap_or_default();
    for edit in head
        .corrections
        .source_object_edits
        .as_deref()
        .unwrap_or_default()
    {
        // The imported vegetation host applies these sparse edits and copies.
        // Their exact source pin names an instance block, never a manual mesh.
        if forge_project::zone_authoring::imported_plants::is_source_asset(
            head,
            edit.source.asset_id,
            edit.source.asset_hash,
        ) {
            continue;
        }
        placements.extend(source_objects::resolve(head, &edit.source)?);
    }
    placements.sort_by_key(|placement| placement.id);
    forge_project::zone_authoring::placements::PlacementChanges::between(&[], &placements)?;
    let profile = loaded.objects()?;
    let fish_profiles = loaded.fish_profiles()?;
    let fish_placements = fish_scene::select_placements(fish_profiles.iter(), &placements)?;
    let fish_ids: BTreeSet<_> = fish_placements.iter().map(|item| item.id).collect();
    let fish = prepare_fish(loaded, &fish_profiles, &fish_placements, profile.as_ref())?;
    let mut result = Vec::new();
    let mut source = Vec::new();
    let mut graph = Vec::new();
    let mut decals = Vec::new();
    let mut graph_cache = GraphCache::new();
    let mut companion_cache = super::companions::Cache::default();
    for placement in placements {
        let has_companions = placement.physics.is_some() || placement.particles.is_some();
        if fish_ids.contains(&placement.id) {
            if has_companions {
                return Err("Fish placement companions do not have an admitted source host".into());
            }
            continue;
        }
        if let Some(profile) = &profile
            && profile
                .package
                .catalog()
                .assets
                .iter()
                .any(|asset| asset.id == placement.asset_id)
        {
            if has_companions {
                return Err("Source object companions do not have an admitted source host".into());
            }
            profile
                .package
                .catalog()
                .resolve_import_pin(placement.asset_id, placement.asset_hash)?;
            source.push(placement);
            continue;
        }
        let asset = catalog
            .assets
            .iter()
            .find(|asset| {
                asset.id == placement.asset_id
                    && asset.payload.sha256 == placement.asset_hash
                    && asset.kind == GovernedAssetKind::Mesh
            })
            .ok_or("Manual placement must name a governed mesh with its exact pin")?;
        match graph_carrier(
            loaded,
            &asset.payload.path,
            placement.asset_hash,
            &mut graph_cache,
        )? {
            Carrier::Graph(bytes) => {
                let companions =
                    super::companions::prepare(loaded, &placement, &mut companion_cache)?;
                graph.push(PreparedGraph {
                    placement,
                    carrier: bytes,
                    companions,
                });
                continue;
            }
            Carrier::Decal(bytes) => {
                if has_companions || placement.local_lights.is_some() {
                    return Err(
                        "Mesh decals do not admit physics, particles or local lights".into(),
                    );
                }
                decals.push((placement, bytes));
                continue;
            }
            Carrier::Ordinary => {}
        }
        if has_companions {
            return Err("Saved companions require the admitted source graph host".into());
        }
        result.push((placement, asset.payload.path.clone()));
    }
    if source_graph_qualification && graph.is_empty() {
        return Err(
            "Source-graph qualification requires a referenced source-graph placement".into(),
        );
    }
    if !decals.is_empty() && graph.is_empty() {
        return Err("Mesh decals require a compatible source material in their map".into());
    }
    let objects = prepare_objects(loaded, &source, profile)?;
    Ok(PreparedManual {
        governed: result,
        source,
        graph,
        decals,
        objects,
        fish_placements,
        fish,
    })
}

pub fn install(app: &mut App, mut prepared: PreparedManual, water: bool) -> Result<(), String> {
    if let Some(objects) = prepared.objects.take() {
        forge_engine::source_surface::receivers::stage_shader(app.world_mut(), &objects.shader)?;
        if water {
            objects.host.install_with_water(app)?;
        } else {
            objects.host.install_without_water(app)?;
        }
    }
    if let Some(fish) = prepared.fish.take() {
        fish_live::install(app, Some(fish), water)?;
    }
    if !prepared.graph.is_empty() {
        if !app.is_plugin_added::<forge_engine::source_graph::SourceGraphPlugin>() {
            app.add_plugins(forge_engine::source_graph::SourceGraphPlugin);
        }
        if !app.is_plugin_added::<forge_engine::source_graph_runtime::SourceSceneRuntimePlugin>() {
            app.add_plugins(
                forge_engine::source_graph_runtime::SourceSceneRuntimePlugin::default(),
            );
        }
        install_source_graph_particles(app, &prepared.graph);
        app.add_systems(
            PostUpdate,
            observe_source_graph
                .after(forge_engine::source_graph_runtime::SourceSceneSystems::Placements)
                .after(super::graphics::ModuleGraphicsObserved),
        );
    }
    if !prepared.decals.is_empty() {
        decals::register(app);
    }
    app.init_resource::<ModuleAssetFailure>()
        .insert_resource(prepared)
        .add_plugins(ManualPlacementCollisionPlugin)
        .add_systems(Startup, spawn)
        .add_systems(PostUpdate, observe);
    Ok(())
}

fn install_source_graph_particles(app: &mut App, graph: &[PreparedGraph]) {
    if graph.iter().any(|item| item.companions.particles.is_some())
        && !app.is_plugin_added::<
            forge_engine::source_particle_compute::SourceParticleProjectionPlugin,
        >()
    {
        app.add_plugins(forge_engine::source_particle_compute::SourceParticleProjectionPlugin);
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy borrows system parameters"
)]
fn spawn(mut commands: Commands, prepared: Res<PreparedManual>, server: Res<AssetServer>) {
    for (placement, path) in &prepared.governed {
        let handle: Handle<WorldAsset> =
            server.load(GltfAssetLabel::Scene(0).from_asset(path.clone()));
        commands.spawn((
            Name::new(format!("Module object · {}", placement.name)),
            WorldAssetRoot(handle.clone()),
            ManualSceneHandle(handle),
            ManualPlacementProjection(placement.clone()),
            forge_engine::placement_transform(placement),
            Visibility::Hidden,
        ));
    }
    for graph in &prepared.graph {
        let placement = &graph.placement;
        let root = commands
            .spawn((
                Name::new(format!("Module source object · {}", placement.name)),
                ManualPlacementProjection(placement.clone()),
                forge_engine::placement_transform(placement),
                Visibility::Hidden,
                ModuleSourceGraph,
            ))
            .id();
        forge_engine::source_graph_placement::submit(
            &mut commands,
            root,
            Arc::clone(&graph.carrier),
            graph.companions.physics.clone(),
            graph.companions.particles.clone(),
            31,
        );
    }
}

fn observe_source_graph(world: &mut World) {
    let roots = world
        .query_filtered::<Entity, With<ModuleSourceGraph>>()
        .iter(world)
        .collect::<Vec<_>>();
    for root in roots {
        match forge_engine::source_graph_placement::admit_manual(world, root) {
            Ok(forge_engine::source_graph_placement::ManualAdmission::Pending) => {
                world.entity_mut(root).insert(Visibility::Hidden);
            }
            Ok(forge_engine::source_graph_placement::ManualAdmission::Attached) => {
                let collisions = world.get::<forge_engine::ManualPlacementCollisionStatus>(root);
                if let Some(forge_engine::ManualPlacementCollisionStatus::Failed(reason)) =
                    collisions
                {
                    let reason = format!("Module source collisions refused: {reason}");
                    world.entity_mut(root).insert(Visibility::Hidden);
                    refuse(world, reason);
                    return;
                }
                // The engine's source-physics admission already checked its
                // live backend and contacts. It deliberately does not create
                // the ordinary generated-triangle collision contract.
                let source_physics = world
                    .get::<ManualPlacementProjection>(root)
                    .is_some_and(|placement| placement.0.physics.is_some());
                let ready = (source_physics
                    || matches!(
                        collisions,
                        Some(forge_engine::ManualPlacementCollisionStatus::Registered { .. })
                    ))
                    && world
                        .get_resource::<super::graphics::State>()
                        .is_some_and(|state| state.ready && state.error.is_none());
                let visibility = if ready {
                    Visibility::Inherited
                } else {
                    Visibility::Hidden
                };
                if world.get::<Visibility>(root) != Some(&visibility) {
                    world.entity_mut(root).insert(visibility);
                }
            }
            Err(reason) => {
                world.entity_mut(root).insert(Visibility::Hidden);
                refuse(world, format!("Module source object refused: {reason}"));
                return;
            }
        }
    }
}

fn admitted_meshes(world: &World, entities: &[Entity]) -> Result<Vec<Entity>, String> {
    if let Some(reason) = entities.iter().find_map(|entity| {
        world
            .get::<forge_engine::source_fragment::SourceFragmentRejected>(*entity)
            .map(|rejected| &rejected.0)
    }) {
        return Err(format!("Source material rejected: {reason}"));
    }
    let meshes = entities
        .iter()
        .copied()
        .filter(|entity| world.get::<Mesh3d>(*entity).is_some())
        .collect::<Vec<_>>();
    if meshes.is_empty() {
        return Err("Manual mesh contains no renderable primitives".into());
    }
    Ok(meshes)
}

/// Only the explicit, bounded zero-thickness source box changes the ordinary
/// module mesh policy. Its invisible primitive supplies all its collision.
fn flat_box_mesh(world: &World, root: Entity, mut entity: Entity) -> Result<bool, String> {
    for _ in 0..128 {
        if entity == root {
            return Ok(false);
        }
        if let Some(extras) = world.get::<GltfExtras>(entity)
            && let Ok(value) = serde_json::from_str::<serde_json::Value>(&extras.value)
        {
            let shape = &value["openrpg_collision"];
            if shape["kind"].as_str() == Some("flat_box") {
                let name = world.get::<Name>(entity).map(Name::as_str).unwrap_or("");
                let component = value["source_component_id"].as_str().unwrap_or("");
                let hash = value["source_prefab_sha256"].as_str().unwrap_or("");
                if extras.value.len() > 65_536
                    || value["source_role"].as_str() != Some("collision")
                    || value["source_collider_type"].as_str() != Some("BoxCollider")
                    || shape["schema"].as_str() != Some("source-collider.v1")
                    || shape["source_dimensions_m"] != serde_json::json!([1, 1, 0])
                    || shape["target_geometry"].as_str() != Some("triangle_mesh")
                    || !name.starts_with("UTM_")
                    || component.is_empty()
                    || !component.bytes().all(|byte| byte.is_ascii_digit())
                    || !name.ends_with(&format!("_{component}"))
                    || hash.len() != 64
                    || !hash.bytes().all(|byte| byte.is_ascii_hexdigit())
                {
                    return Err("Invalid source flat box declaration".into());
                }
                return Ok(true);
            }
        }
        let Some(parent) = world.get::<ChildOf>(entity) else {
            return Ok(false);
        };
        entity = parent.parent();
    }
    Err("Invalid manual collision hierarchy".into())
}

fn observe(world: &mut World) {
    if world
        .get_resource::<ModuleAssetFailure>()
        .and_then(ModuleAssetFailure::reason)
        .is_some()
    {
        return;
    }
    if let Some(reason) = world
        .get_resource::<fish_live::Status>()
        .and_then(|status| status.error.clone())
    {
        refuse(world, format!("Module fish scene refused: {reason}"));
        return;
    }
    // Rejected fragments can also belong to gameplay visuals. Inspect them
    // once before admitting any manual mesh, then emit a single failure.
    let rejected = world
        .query::<&forge_engine::source_fragment::SourceFragmentRejected>()
        .iter(world)
        .next()
        .map(|rejected| rejected.0.clone());
    if let Some(reason) = rejected {
        refuse(world, format!("Module source material refused: {reason}"));
        return;
    }
    let pending = world
        .query_filtered::<(
            Entity,
            &ManualPlacementProjection,
            &ManualSceneHandle,
            Option<&WorldInstance>,
        ), Without<ManualPlacementCollisionExpected>>()
        .iter(world)
        .map(|(root, placement, handle, instance)| {
            (
                root,
                placement.0.id,
                placement.0.asset_hash,
                handle.0.clone(),
                instance.map(|instance| **instance),
            )
        })
        .collect::<Vec<_>>();
    for (root, id, source, handle, instance) in pending {
        if let Some(LoadState::Failed(error)) =
            world.resource::<AssetServer>().get_load_state(handle.id())
        {
            refuse(
                world,
                format!("Module manual mesh {id} failed to load: {error}"),
            );
            return;
        }
        if let Some(RecursiveDependencyLoadState::Failed(error)) = world
            .resource::<AssetServer>()
            .get_recursive_dependency_load_state(handle.id())
        {
            refuse(
                world,
                format!("Module manual mesh {id} dependency failed to load: {error}"),
            );
            return;
        }
        if !world
            .resource::<AssetServer>()
            .is_loaded_with_dependencies(handle.id())
        {
            continue;
        }
        let Some(instance) = instance else {
            continue;
        };
        let spawner = world.resource::<WorldInstanceSpawner>();
        if !spawner.instance_is_ready(instance) {
            continue;
        }
        let entities = spawner.iter_instance_entities(instance).collect::<Vec<_>>();
        let meshes = match admitted_meshes(world, &entities) {
            Ok(meshes) => meshes,
            Err(reason) => {
                refuse(world, format!("Module manual mesh {id} refused: {reason}"));
                return;
            }
        };
        let roles = meshes
            .iter()
            .map(|&entity| flat_box_mesh(world, root, entity).map(|flat| (entity, flat)))
            .collect::<Result<Vec<_>, _>>();
        let roles = match roles {
            Ok(roles) => roles,
            Err(reason) => {
                refuse(world, format!("Module manual mesh {id} refused: {reason}"));
                return;
            }
        };
        let has_flat = roles.iter().any(|&(_, flat)| flat);
        if has_flat && roles.iter().all(|&(_, flat)| flat) {
            refuse(
                world,
                format!("Module manual mesh {id} has no visual primitive"),
            );
            return;
        }
        let mut collision = Vec::new();
        for (entity, flat) in roles {
            let mut node = world.entity_mut(entity);
            if !flat {
                node.insert((
                    ManualPlacementPart(id),
                    forge_engine::static_hlod::StaticHlodSource { object: id, source },
                ));
            } else {
                node.insert(Visibility::Hidden);
            }
            if flat || !has_flat {
                node.insert((
                    ManualPlacementCollision(id),
                    if flat {
                        ManualPlacementCollisionGeometry::FlatBoxSurface
                    } else {
                        ManualPlacementCollisionGeometry::TriangleMesh
                    },
                ));
                collision.push(entity);
            }
        }
        world.entity_mut(root).insert((
            ManualPlacementCollisionExpected {
                placement: id,
                primitives: collision,
            },
            Visibility::Inherited,
        ));
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn source_flat_box_is_found_on_its_node_and_refuses_changed_dimensions() {
        let mut world = World::new();
        let root = world.spawn_empty().id();
        let mut extra = serde_json::json!({
            "source_role": "collision",
            "source_component_id": "42",
            "source_collider_type": "BoxCollider",
            "source_prefab_sha256": "a".repeat(64),
            "openrpg_collision": {
                "schema": "source-collider.v1", "kind": "flat_box",
                "source_dimensions_m": [1, 1, 0], "target_geometry": "triangle_mesh"
            }
        });
        let node = world
            .spawn((
                Name::new("UTM_test_42"),
                GltfExtras {
                    value: extra.to_string(),
                },
                ChildOf(root),
            ))
            .id();
        let primitive = world.spawn(ChildOf(node)).id();
        assert_eq!(flat_box_mesh(&world, root, primitive), Ok(true));
        extra["openrpg_collision"]["source_dimensions_m"][2] = serde_json::json!(0.1);
        world.entity_mut(node).insert(GltfExtras {
            value: extra.to_string(),
        });
        assert!(flat_box_mesh(&world, root, primitive).is_err());
        let visual = world.spawn(ChildOf(root)).id();
        assert_eq!(flat_box_mesh(&world, root, visual), Ok(false));
    }

    fn graph_with_particles(particles: bool) -> PreparedGraph {
        let carrier = forge_core::bytes_sha256(b"fixture carrier");
        let pin = forge_core::bytes_sha256(b"fixture packet");
        PreparedGraph {
            placement: ManualPlacement {
                id: forge_core::EntityId::deterministic("graph-plugin/placement"),
                name: "graph".into(),
                asset_id: forge_core::AssetId::deterministic("graph-plugin/asset"),
                asset_hash: carrier,
                physics: None,
                particles: None,
                local_lights: None,
                transform: forge_core::Transform::IDENTITY,
                locked: false,
            },
            carrier: Arc::from([]),
            companions: super::super::companions::Prepared {
                physics: None,
                particles: particles.then(|| {
                    forge_engine::source_graph_mount::SourceParticlePayload {
                        wire: Arc::from([]),
                        carrier_sha256: carrier,
                        manifest_sha256: pin,
                        package_sha256: pin,
                    }
                }),
            },
        }
    }

    #[test]
    fn source_particle_plugin_is_opt_in_and_installed_once() {
        use forge_engine::source_particle_compute::SourceParticleProjectionPlugin;

        let mut app = App::new();
        install_source_graph_particles(&mut app, &[]);
        install_source_graph_particles(&mut app, &[graph_with_particles(false)]);
        assert!(!app.is_plugin_added::<SourceParticleProjectionPlugin>());
        let graph = [graph_with_particles(true)];
        install_source_graph_particles(&mut app, &graph);
        install_source_graph_particles(&mut app, &graph);
        assert!(app.is_plugin_added::<SourceParticleProjectionPlugin>());
    }

    #[test]
    fn qualified_graph_never_reveals_a_refused_or_unowned_scene() {
        use forge_engine::source_graph_placement::Status;
        use forge_engine::source_graph_runtime::SourceFrameStatus;

        for (status, frame) in [
            (Status::Refused("mount failed".into()), None),
            (
                Status::MountedPendingRender,
                Some(SourceFrameStatus::Refused("frame failed".into())),
            ),
            (
                Status::MountedPendingRender,
                Some(SourceFrameStatus::Updated),
            ),
        ] {
            let mut world = World::new();
            world.init_resource::<Messages<bevy::app::AppExit>>();
            let placement = ManualPlacement {
                id: forge_core::EntityId::deterministic("graph-test/placement"),
                name: "graph".into(),
                asset_id: forge_core::AssetId::deterministic("graph-test/asset"),
                asset_hash: forge_core::bytes_sha256(b"graph"),
                physics: None,
                particles: None,
                local_lights: None,
                transform: forge_core::Transform::IDENTITY,
                locked: false,
            };
            let root = world
                .spawn((
                    ModuleSourceGraph,
                    ManualPlacementProjection(placement),
                    Visibility::Hidden,
                    status,
                ))
                .id();
            if let Some(frame) = frame {
                world.entity_mut(root).insert(frame);
            }
            observe_source_graph(&mut world);
            assert_eq!(world.get::<Visibility>(root), Some(&Visibility::Hidden));
            assert!(world.resource::<ModuleAssetFailure>().reason().is_some());
            assert!(
                world
                    .get::<ManualPlacementCollisionExpected>(root)
                    .is_none()
            );
        }
    }

    #[test]
    fn a_rejected_source_draw_prevents_partial_manual_scene_admission() {
        let mut world = World::new();
        let mesh = world.spawn(Mesh3d(Handle::default())).id();
        let rejected = world
            .spawn(forge_engine::source_fragment::SourceFragmentRejected(
                "missing tangent".into(),
            ))
            .id();
        assert_eq!(admitted_meshes(&world, &[mesh]).unwrap(), vec![mesh]);
        assert!(admitted_meshes(&world, &[mesh, rejected]).is_err());
        assert!(admitted_meshes(&world, &[]).is_err());
    }

    #[test]
    fn multiple_rejected_draws_emit_a_single_failure_before_scene_admission() {
        let mut world = World::new();
        world.init_resource::<Messages<bevy::app::AppExit>>();
        for reason in ["missing tangent", "unsupported vertex colour"] {
            world.spawn(forge_engine::source_fragment::SourceFragmentRejected(
                reason.into(),
            ));
        }
        observe(&mut world);
        let witness = world.resource::<ModuleAssetFailure>().clone();
        let failure = witness.reason().unwrap().to_owned();
        assert!(failure.starts_with("Module source material refused: "));
        assert!(
            failure.ends_with("missing tangent") || failure.ends_with("unsupported vertex colour")
        );
        observe(&mut world);
        assert_eq!(
            world.resource::<ModuleAssetFailure>().reason(),
            Some(failure.as_str())
        );
        let exits = world.resource::<Messages<bevy::app::AppExit>>();
        assert_eq!(exits.len(), 1);
        assert!(exits.get_cursor().read(exits).next().unwrap().is_error());
        drop(world);
        assert_eq!(witness.reason(), Some(failure.as_str()));
    }
}
