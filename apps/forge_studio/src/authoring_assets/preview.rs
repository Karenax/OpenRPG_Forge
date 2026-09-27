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
    Show(String, Vec<u8>),
    Clear,
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
    if !is_current(ticket) {
        return false;
    }
    REQUEST.with(|request| request.replace(Some(PreviewRequest::Show(id, bytes))));
    true
}

pub(super) fn is_current(ticket: u64) -> bool {
    EPOCH.with(Cell::get) == ticket
}

pub(crate) fn register(app: &mut App) {
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
            .after(bevy::transform::TransformSystems::Propagate)
            .after(VisibilitySystems::CalculateBounds),
    );
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
    active: Option<(Entity, Handle<WorldAsset>, String, String)>,
    ready: bool,
    started: f64,
}

impl PreviewState {
    fn clear(&mut self, commands: &mut Commands) {
        if let Some((entity, _, path, _)) = self.active.take() {
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
        state.clear(&mut commands);
        if let PreviewRequest::Show(id, bytes) = request {
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
            state.active = Some((entity, handle, path, id));
            state.started = time.elapsed_secs_f64();
        }
    }
    let Some((root, handle, _, id)) = &state.active else {
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
    let framing = configure(
        *root,
        id,
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
        "Aperçu LOD0 chargé au-dessus de la zone, sans collision ni placement. Rotation et zoom disponibles ; rendu à inspecter.",
    );
}

pub(super) fn configure(
    root: Entity,
    id: &str,
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
        let show = is_lod0(entity, root, id, nodes);
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
        return Err("Aperçu refusé : aucune primitive LOD0 identifiée dans ce kit.");
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
            if name.starts_with("UCX_") {
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

/// UCX role belonging to this verified asset only; render LODs are never colliders.
pub(super) fn is_collision(
    mut entity: Entity,
    root: Entity,
    id: &str,
    nodes: &PreviewNodes,
) -> bool {
    let prefix = format!("UCX_{id}");
    for _ in 0..128 {
        if entity == root {
            return false;
        }
        let Ok((name, parent, _, _, _)) = nodes.get(entity) else {
            return false;
        };
        if let Some(name) = name {
            if let Some(suffix) = name.as_str().strip_prefix(&prefix) {
                return suffix.is_empty() || suffix.starts_with('_');
            }
            if name.as_str().starts_with("UCX_") {
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
