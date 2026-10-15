//! One disposable, verified GLB in the existing authoring world. Never a placement.
use bevy::{
    asset::{
        AssetApp,
        io::{
            AssetSourceBuilder,
            memory::{Dir, MemoryAssetReader},
        },
    },
    camera::{primitives::Aabb, visibility::VisibilitySystems},
    ecs as bevy_ecs,
    gltf::GltfAssetLabel,
    prelude::*,
    world_serialization::{WorldAsset, WorldAssetRoot, WorldInstance, WorldInstanceSpawner},
};
use std::{
    cell::{Cell, RefCell},
    path::Path,
};

thread_local! {
    static ROOT: Dir = Dir::default();
    static EPOCH: Cell<u64> = const { Cell::new(0) };
    static REQUEST: RefCell<Option<PreviewRequest>> = const { RefCell::new(None) };
}

enum PreviewRequest {
    Show(
        String,
        Vec<u8>,
        SceneLayout,
        Option<super::source_graph_physics::SourcePhysicsPayload>,
        Option<super::source_graph_particles::SourceParticlePayload>,
    ),
    Clear,
}

#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub(super) enum PreviewSystems {
    Requests,
}

/// The admitted source determines scene interpretation, never a pack or name.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub(super) enum SceneLayout {
    CatalogLod0,
    AutonomousScene,
    /// Authenticated catalog authority for all static Scene0 mesh primitives.
    ModuleStaticTriangles {
        catalog_sha256: forge_core::ContentHash,
    },
}

impl SceneLayout {
    pub(super) fn module(
        policy: Option<crate::asset_library_protocol::ModuleCollisionPolicy>,
        catalog_sha256: forge_core::ContentHash,
    ) -> Self {
        match policy {
            Some(crate::asset_library_protocol::ModuleCollisionPolicy::StaticTriangleMeshes) => {
                Self::ModuleStaticTriangles { catalog_sha256 }
            }
            None => Self::AutonomousScene,
        }
    }
}

pub(super) fn cancel() {
    EPOCH.with(|epoch| epoch.set(epoch.get().wrapping_add(1)));
    REQUEST.with(|request| request.replace(Some(PreviewRequest::Clear)));
}

pub(super) fn ticket() -> u64 {
    cancel();
    EPOCH.with(Cell::get)
}

pub(super) fn submit(ticket: u64, id: String, bytes: Vec<u8>) -> bool {
    submit_layout(ticket, id, bytes, SceneLayout::CatalogLod0)
}

pub(super) fn submit_scene_with_physics(
    ticket: u64,
    id: String,
    bytes: Vec<u8>,
    physics: Option<super::source_graph_physics::SourcePhysicsPayload>,
    particles: Option<super::source_graph_particles::SourceParticlePayload>,
) -> bool {
    if !is_current(ticket) {
        return false;
    }
    REQUEST.with(|request| {
        request.replace(Some(PreviewRequest::Show(
            id,
            bytes,
            SceneLayout::AutonomousScene,
            physics,
            particles,
        )))
    });
    true
}

fn submit_layout(ticket: u64, id: String, bytes: Vec<u8>, layout: SceneLayout) -> bool {
    if !is_current(ticket) {
        return false;
    }
    REQUEST
        .with(|request| request.replace(Some(PreviewRequest::Show(id, bytes, layout, None, None))));
    true
}

pub(super) fn is_current(ticket: u64) -> bool {
    EPOCH.with(Cell::get) == ticket
}

pub(crate) fn register(app: &mut App) {
    super::map_shelf::install(app);
    crate::authoring_species::register(app);
    let root = ROOT.with(Clone::clone);
    app.register_asset_source(
        "authoring-preview",
        AssetSourceBuilder::new(move || Box::new(MemoryAssetReader { root: root.clone() })),
    );
    // Framing consumes composed transforms and mesh bounds, never loader guesses.
    app.add_systems(
        PostUpdate,
        update
            .in_set(PreviewSystems::Requests)
            .after(bevy::transform::TransformSystems::Propagate)
            .after(VisibilitySystems::CalculateBounds),
    );
    super::source_graph_runtime::register(app);
    if !app
        .is_plugin_added::<forge_engine::source_particle_compute::SourceParticleProjectionPlugin>()
    {
        app.add_plugins(forge_engine::source_particle_compute::SourceParticleProjectionPlugin);
    }
    super::source_graph_physics::register(app);
    super::source_graph_preview::register(app);
    super::placements::register(app);
}

pub(super) fn insert(path: &str, bytes: Vec<u8>) {
    ROOT.with(|root| root.insert_asset(Path::new(path), bytes));
}

pub(super) fn remove(path: &str) {
    ROOT.with(|root| root.remove_asset(Path::new(path)));
}

pub(super) type PreviewNodes<'w, 's> = Query<
    'w,
    's,
    (
        Option<&'static Name>,
        Option<&'static ChildOf>,
        Option<&'static Mesh3d>,
        Option<&'static GlobalTransform>,
        Option<&'static Aabb>,
    ),
>;

#[derive(Default)]
struct PreviewState {
    active: Option<(Entity, Handle<WorldAsset>, String, String, SceneLayout)>,
    ready: bool,
    started: f64,
}

impl PreviewState {
    fn clear(&mut self, commands: &mut Commands) {
        if let Some((entity, _, path, _, _)) = self.active.take() {
            commands.entity(entity).despawn();
            ROOT.with(|root| root.remove_asset(Path::new(&path)));
            crate::authoring_camera::leave_preview();
        }
        self.ready = false;
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn update(
    mut commands: Commands,
    server: Res<AssetServer>,
    time: Res<Time>,
    instances: Query<&WorldInstance>,
    spawner: Res<WorldInstanceSpawner>,
    nodes: PreviewNodes,
    mut state: Local<PreviewState>,
) {
    if let Some(request) = REQUEST.with(|request| request.borrow_mut().take()) {
        super::source_graph_preview::cancel();
        state.clear(&mut commands);
        if let PreviewRequest::Show(id, bytes, layout, physics, particles) = request {
            match super::source_graph_route::read(&bytes) {
                Ok(Some(_)) => {
                    super::source_graph_preview::submit(id, bytes, physics, particles);
                    return;
                }
                Ok(None) => {
                    if physics.is_some() || particles.is_some() {
                        super::preview_status(
                            "Aperçu refusé : données physiques sans déclaration source.",
                        );
                        return;
                    }
                }
                Err(reason) => {
                    super::preview_status(&format!("Aperçu refusé : {reason}"));
                    return;
                }
            }
            let path = format!("{}.glb", EPOCH.with(Cell::get));
            ROOT.with(|root| root.insert_asset(Path::new(&path), bytes));
            let handle = server
                .load(GltfAssetLabel::Scene(0).from_asset(format!("authoring-preview://{path}")));
            let entity = commands
                .spawn((
                    WorldAssetRoot(handle.clone()),
                    Visibility::Hidden,
                    Transform::from_xyz(0.0, 1024.0, 0.0),
                    Name::new("Aperçu temporaire · non sauvegardé"),
                ))
                .id();
            state.active = Some((entity, handle, path, id, layout));
            state.started = time.elapsed_secs_f64();
        }
    }
    let Some((root, handle, _, id, layout)) = &state.active else {
        return;
    };
    if state.ready {
        return;
    }
    if server.load_state(handle.id()).is_failed() || time.elapsed_secs_f64() - state.started > 30.0
    {
        state.clear(&mut commands);
        super::preview_status(
            "Aperçu refusé : erreur de chargement ou délai de 30 secondes dépassé.",
        );
        return;
    }
    let Ok(instance) = instances.get(*root) else {
        return;
    };
    if !spawner.instance_is_ready(**instance) {
        return;
    }
    let framing = configure_layout(
        *root,
        id,
        *layout,
        spawner.iter_instance_entities(**instance),
        &nodes,
        &mut commands,
    );
    let (pivot, distance) = match framing {
        Ok(Some(frame)) => frame,
        Ok(None) => return,
        Err(reason) => {
            state.clear(&mut commands);
            super::preview_status(reason);
            return;
        }
    };
    commands.entity(*root).insert(Visibility::Inherited);
    state.ready = true;
    // Isolated presentation position above the zone; coordinates are not persisted.
    crate::authoring_camera::frame_preview(pivot, distance);
    super::preview_status(
        "Aperçu chargé au-dessus de la zone, sans collision ni placement. Rotation et zoom disponibles ; rendu à inspecter.",
    );
}

pub(super) fn configure(
    root: Entity,
    id: &str,
    entities: impl Iterator<Item = Entity>,
    nodes: &PreviewNodes,
    commands: &mut Commands,
) -> Result<Option<(Vec3, f32)>, &'static str> {
    configure_layout(
        root,
        id,
        SceneLayout::CatalogLod0,
        entities,
        nodes,
        commands,
    )
}

pub(super) fn configure_layout(
    root: Entity,
    id: &str,
    layout: SceneLayout,
    entities: impl Iterator<Item = Entity>,
    nodes: &PreviewNodes,
    commands: &mut Commands,
) -> Result<Option<(Vec3, f32)>, &'static str> {
    let mut min = Vec3::splat(f32::INFINITY);
    let mut max = Vec3::splat(f32::NEG_INFINITY);
    let mut visible = 0;
    let mut pending = false;
    for entity in entities {
        let Ok((_, _, Some(_), transform, bounds)) = nodes.get(entity) else {
            continue;
        };
        let show = is_renderable(entity, root, id, layout, nodes);
        commands.entity(entity).insert(if show {
            Visibility::Inherited
        } else {
            Visibility::Hidden
        });
        if !show {
            continue;
        }
        visible += 1;
        let (Some(transform), Some(bounds)) = (transform, bounds) else {
            pending = true;
            continue;
        };
        for x in [-1.0, 1.0] {
            for y in [-1.0, 1.0] {
                for z in [-1.0, 1.0] {
                    let corner = Vec3::from(bounds.center)
                        + Vec3::from(bounds.half_extents) * Vec3::new(x, y, z);
                    let point = transform.transform_point(corner);
                    if !point.is_finite() {
                        return Err("Aperçu refusé : dimensions non finies.");
                    }
                    min = min.min(point);
                    max = max.max(point);
                }
            }
        }
    }
    if visible == 0 {
        return Err("Aperçu refusé : aucune primitive de rendu identifiée dans cette scène.");
    }
    if pending {
        return Ok(None);
    }
    let radius = (max - min).length() * 0.5;
    if !radius.is_finite() || radius > 300.0 {
        return Err("Aperçu refusé : gabarit supérieur à 600 mètres, hors vue individuelle.");
    }
    Ok(Some(((min + max) * 0.5, (radius * 3.0).max(4.0))))
}

pub(super) fn is_lod0(mut entity: Entity, root: Entity, id: &str, nodes: &PreviewNodes) -> bool {
    for _ in 0..128 {
        if entity == root {
            return false;
        }
        let Ok((name, parent, _, _, _)) = nodes.get(entity) else {
            return false;
        };
        if let Some(name) = name {
            let name = name.as_str();
            if name.starts_with("UCX_") || name.starts_with("UTM_") {
                return false;
            }
            if let Some(suffix) = name.strip_prefix(id) {
                for (role, visible) in [("_LOD0", true), ("_LOD1", false), ("_LOD2", false)] {
                    if suffix
                        .strip_prefix(role)
                        .is_some_and(|rest| rest.is_empty() || rest.starts_with('_'))
                    {
                        return visible;
                    }
                }
            }
        }
        let Some(parent) = parent else { return false };
        entity = parent.parent();
    }
    false
}

pub(super) fn is_renderable(
    mut entity: Entity,
    root: Entity,
    id: &str,
    layout: SceneLayout,
    nodes: &PreviewNodes,
) -> bool {
    if layout == SceneLayout::CatalogLod0 {
        return is_lod0(entity, root, id, nodes);
    }
    // A normalized Vault entry already selects its own scene and LOD variant.
    // Keep its primitive names intact; do not infer a kit naming convention.
    for _ in 0..128 {
        if entity == root {
            return true;
        }
        let Ok((name, parent, _, _, _)) = nodes.get(entity) else {
            return false;
        };
        if name.is_some_and(|n| n.as_str().starts_with("UCX_") || n.as_str().starts_with("UTM_")) {
            return false;
        }
        let Some(parent) = parent else { return false };
        entity = parent.parent();
    }
    false
}

/// A host-admitted static module uses owned Scene0 primitives; otherwise only UCX/UTM roles apply.
pub(super) fn is_collision(
    mut entity: Entity,
    root: Entity,
    id: &str,
    layout: SceneLayout,
    nodes: &PreviewNodes,
) -> bool {
    if matches!(layout, SceneLayout::ModuleStaticTriangles { .. }) {
        return crate::asset_library_protocol::ModuleCollisionPolicy::StaticTriangleMeshes
            .selects_primitive(
                entity,
                root,
                nodes
                    .get(entity)
                    .is_ok_and(|(_, _, mesh, _, _)| mesh.is_some()),
                |entity| {
                    nodes
                        .get(entity)
                        .ok()
                        .and_then(|(_, parent, _, _, _)| parent.map(ChildOf::parent))
                },
            );
    }
    let prefixes = [format!("UCX_{id}"), format!("UTM_{id}")];
    for _ in 0..128 {
        if entity == root {
            return false;
        }
        let Ok((name, parent, _, _, _)) = nodes.get(entity) else {
            return false;
        };
        if let Some(name) = name {
            for prefix in &prefixes {
                if let Some(suffix) = name.as_str().strip_prefix(prefix) {
                    return suffix.is_empty() || suffix.starts_with('_');
                }
            }
            if name.as_str().starts_with("UCX_") || name.as_str().starts_with("UTM_") {
                return false;
            }
        }
        let Some(parent) = parent else {
            return false;
        };
        entity = parent.parent();
    }
    false
}

/// Only an explicit source declaration admits a concave triangle collision mesh.
/// Older UCX primitives retain the existing convex contract.
pub(super) fn collision_geometry(
    mut entity: Entity,
    root: Entity,
    id: &str,
    layout: SceneLayout,
    nodes: &PreviewNodes,
    extras: &Query<&bevy::gltf::GltfExtras>,
) -> Result<Option<forge_engine::ManualPlacementCollisionGeometry>, &'static str> {
    if matches!(layout, SceneLayout::ModuleStaticTriangles { .. }) {
        return Ok(is_collision(entity, root, id, layout, nodes)
            .then_some(forge_engine::ManualPlacementCollisionGeometry::TriangleMesh));
    }
    let convex_prefix = format!("UCX_{id}");
    let triangle_prefix = format!("UTM_{id}_");
    for _ in 0..128 {
        if entity == root {
            return Ok(None);
        }
        let Ok((name, parent, mesh, _, _)) = nodes.get(entity) else {
            return Ok(None);
        };
        if let Some(name) = name {
            let name = name.as_str();
            if name
                .strip_prefix(&convex_prefix)
                .is_some_and(|s| s.is_empty() || s.starts_with('_'))
            {
                return Ok(Some(
                    forge_engine::ManualPlacementCollisionGeometry::ConvexPrimitive,
                ));
            }
            if let Some(component) = name.strip_prefix(&triangle_prefix) {
                // Bevy gives the primitive child the same mesh name. The source
                // declaration belongs to its owning glTF node, not that child.
                if mesh.is_some() {
                    let Some(parent) = parent else {
                        return Err("Collision refusée : nœud source absent.");
                    };
                    entity = parent.parent();
                    continue;
                }
                let invalid =
                    "Collision concave refusée : déclaration source absente ou incompatible.";
                let raw = extras.get(entity).map_err(|_| invalid)?;
                if raw.value.len() > 65_536 {
                    return Err(invalid);
                }
                let declaration: serde_json::Value =
                    serde_json::from_str(&raw.value).map_err(|_| invalid)?;
                let shape = &declaration["openrpg_collision"];
                let hash = declaration["source_prefab_sha256"]
                    .as_str()
                    .ok_or(invalid)?;
                if declaration["source_role"].as_str() != Some("collision")
                    || declaration["source_component_id"].as_str() != Some(component)
                    || component.is_empty()
                    || shape["schema"].as_str() != Some("source-collider.v1")
                    || shape["kind"].as_str() != Some("triangle_mesh")
                    || shape["source_convex"].as_bool() != Some(false)
                    || hash.len() != 64
                    || !hash.bytes().all(|c| c.is_ascii_hexdigit())
                {
                    return Err(invalid);
                }
                return Ok(Some(
                    forge_engine::ManualPlacementCollisionGeometry::TriangleMesh,
                ));
            }
            if name.starts_with("UCX_") || name.starts_with("UTM_") {
                return Ok(None);
            }
        }
        let Some(parent) = parent else {
            return Ok(None);
        };
        entity = parent.parent();
    }
    Err("Collision refusée : hiérarchie source invalide.")
}
