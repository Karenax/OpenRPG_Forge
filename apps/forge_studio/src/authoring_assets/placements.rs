//! Derived manual objects in the same world and verified memory asset source.
//! No source mutation, automatic asset substitution, or implied collision support.
use crate::authoring_projection_diagnostics::{
    MutationObservation, ObjectObservation, PartObservation, ProjectionState, ScopeObservation,
};
use bevy::{
    ecs as bevy_ecs,
    gltf::GltfAssetLabel,
    prelude::*,
    world_serialization::{WorldAsset, WorldAssetRoot, WorldInstance, WorldInstanceSpawner},
};
use forge_engine::authoring_buildings::ProjectionRapierColliderHandle;
use forge_project::{authoring::AuthoringState, zone_authoring::placements::ManualPlacement};
use std::{
    cell::{Cell, RefCell},
    collections::{BTreeMap, BTreeSet},
};

type AssetKey = (forge_core::AssetId, forge_core::ContentHash);
type DiagnosticNodes<'world, 'state> = Query<
    'world,
    'state,
    (
        Option<&'static Mesh3d>,
        Option<&'static MeshMaterial3d<StandardMaterial>>,
        Option<&'static ProjectionRapierColliderHandle>,
    ),
>;
pub(crate) mod gizmo;
mod physics_status;
pub(crate) mod selection;
struct Prepared {
    placements: Vec<ManualPlacement>,
    assets: BTreeMap<AssetKey, (String, Vec<u8>)>,
}
enum Request {
    Clear,
    Show(Prepared),
}
thread_local! {
    static ACCEPTED: RefCell<Vec<ManualPlacement>> = const { RefCell::new(Vec::new()) };
    static DESIRED: RefCell<Vec<ManualPlacement>> = const { RefCell::new(Vec::new()) };
    static EPOCH: Cell<u64> = const { Cell::new(0) };
    static RESIDENT: RefCell<BTreeMap<AssetKey, usize>> = const { RefCell::new(BTreeMap::new()) };
    static READY: Cell<bool> = const { Cell::new(true) };
    static FAILED: Cell<bool> = const { Cell::new(false) };
    static REQUEST: RefCell<Option<Request>> = const { RefCell::new(None) };
    static STATUS: RefCell<String> = RefCell::new("Aucun placement manuel.".into());
    static OUTPUT: RefCell<Option<leptos::prelude::WriteSignal<String>>> = const { RefCell::new(None) };
    static FOCUS: Cell<Option<forge_core::EntityId>> = const { Cell::new(None) };
}

pub(crate) fn focus(id: forge_core::EntityId) {
    if crate::authoring_viewport::can_preview_asset() {
        crate::authoring_brush_pointer::cancel();
        if !super::imported_scene::focus(id) {
            FOCUS.with(|slot| slot.set(Some(id)));
        }
    }
}

#[leptos::component]
pub(crate) fn ProjectionStatus() -> impl leptos::prelude::IntoView {
    use leptos::prelude::*;
    let (status, output) = signal(STATUS.with(|s| s.borrow().clone()));
    OUTPUT.with(|slot| slot.replace(Some(output)));
    on_cleanup(|| {
        OUTPUT.with(|slot| slot.replace(None));
    });
    leptos::view! { <output aria-live="polite">{move || status.get()}</output> }
}

pub(super) fn report(message: impl Into<String>) {
    use leptos::prelude::Set;
    let message = message.into();
    STATUS.with(|s| s.replace(message.clone()));
    OUTPUT.with(|slot| {
        if let Some(output) = *slot.borrow() {
            output.set(message);
        }
    });
}
fn current(epoch: u64) -> bool {
    EPOCH.with(Cell::get) == epoch
}

pub(crate) fn observe(state: &AuthoringState) {
    gizmo::cancel();
    let placements = state
        .zone_head
        .as_ref()
        .and_then(|head| head.corrections.placements.clone())
        .unwrap_or_default();
    ACCEPTED.with(|slot| slot.replace(placements.clone()));
    selection::reconcile(&placements);
    show(placements);
}

pub(crate) fn restore() {
    show(ACCEPTED.with(|s| s.borrow().clone()));
}

pub(super) fn desired_imports() -> Vec<ManualPlacement> {
    DESIRED.with(|slot| {
        slot.borrow()
            .iter()
            .filter(|p| super::imported_buildings::contains_asset(p.asset_id))
            .cloned()
            .collect()
    })
}

pub(super) fn retry_library() {
    gizmo::cancel();
    let desired = DESIRED.with(|slot| slot.borrow().clone());
    FAILED.with(|slot| slot.set(true)); // Force repartition after an explicit library replacement.
    show(desired);
}

/// Current requested placement projection, including a non-applied proposal.
pub(crate) fn proposal_ready() -> Result<bool, String> {
    if FAILED.with(Cell::get) {
        return Err(STATUS.with(|s| s.borrow().clone()));
    }
    Ok(READY.with(Cell::get)
        && REQUEST.with(|s| s.borrow().is_none())
        && super::imported_scene::ready()?)
}

pub(crate) fn accepted_projection(expected: &[ManualPlacement]) -> bool {
    ACCEPTED.with(|accepted| accepted.borrow().as_slice() == expected)
        && DESIRED.with(|desired| desired.borrow().as_slice() == expected)
        && REQUEST.with(|request| request.borrow().is_none())
        && READY.with(Cell::get)
        && super::imported_scene::ready() == Ok(true)
}

pub(crate) fn proposal(
    state: &AuthoringState,
    receipt: &forge_project::zone_authoring::session::ZoneReceipt,
) -> Result<(), String> {
    let head = state.zone_head.as_ref().ok_or("Aucune zone")?;
    if head.content_hash()? != receipt.request.expected_hash
        || head.revision != receipt.request.expected_revision
    {
        return Err("Proposition de placement périmée".into());
    }
    show(receipt.applied.placements.clone().unwrap_or_default());
    Ok(())
}

fn show(placements: Vec<ManualPlacement>) {
    if DESIRED.with(|slot| *slot.borrow() == placements) && !FAILED.with(Cell::get) {
        return;
    }
    DESIRED.with(|slot| slot.replace(placements.clone()));
    let epoch = EPOCH.with(|slot| {
        let next = slot.get().wrapping_add(1);
        slot.set(next);
        next
    });
    READY.with(|slot| slot.set(false));
    FAILED.with(|slot| slot.set(false));
    REQUEST.with(|slot| slot.replace(None));
    if placements.is_empty() {
        REQUEST.with(|slot| slot.replace(Some(Request::Clear)));
        report("Aucun placement manuel.");
        return;
    }
    report("Placements : résolution et vérification du kit local…");
    wasm_bindgen_futures::spawn_local(async move {
        match prepare(placements, epoch).await {
            Ok(prepared) if current(epoch) => {
                let request = if prepared.placements.is_empty() {
                    Request::Clear
                } else {
                    Request::Show(prepared)
                };
                REQUEST.with(|slot| slot.replace(Some(request)));
                report("Placements : chargement 3D en cours…");
            }
            Err(reason) if current(epoch) => {
                FAILED.with(|slot| slot.set(true));
                report(format!("Placements non projetés : {reason}"));
            }
            _ => {}
        }
    });
}

async fn prepare(placements: Vec<ManualPlacement>, epoch: u64) -> Result<Prepared, String> {
    let placements: Vec<_> = placements
        .into_iter()
        .filter(|p| !super::imported_buildings::contains_asset(p.asset_id))
        .collect();
    let resident = RESIDENT.with(|slot| slot.borrow().clone());
    let mut catalog = None;
    let mut assets = BTreeMap::new();
    let mut total = 0;
    let mut seen = std::collections::BTreeSet::new();
    for placement in &placements {
        if !current(epoch) {
            return Err("Chargement remplacé".into());
        }
        let key = (placement.asset_id, placement.asset_hash);
        if !seen.insert(key) {
            continue;
        }
        if let Some(size) = resident.get(&key) {
            total += size;
        } else {
            if catalog.is_none() {
                catalog = Some(super::load_browser().await?);
            }
            let browser = catalog.as_ref().ok_or("Catalogue indisponible")?;
            let (asset, bytes) = super::verify::check_reference(browser, key.0, key.1).await?;
            total += bytes.len();
            assets.insert(key, (asset.id, bytes));
        }
        if total > 256 * 1024 * 1024 {
            return Err("Budget de fichiers placements dépassé (256 Mio)".into());
        }
    }
    Ok(Prepared { placements, assets })
}

struct Object {
    source: ManualPlacement,
    id: forge_core::EntityId,
    root: Entity,
    handle: Handle<WorldAsset>,
    name: String,
    frame: Option<(Vec3, f32)>,
    parts: Vec<Entity>,
}

#[derive(Component)]
struct PlacementVisualReady;
struct ResidentAsset {
    name: String,
    handle: Handle<WorldAsset>,
    path: String,
    bytes: usize,
}

#[derive(Default)]
struct ProjectionMutation {
    retained: Vec<forge_core::EntityId>,
    spawned: Vec<forge_core::EntityId>,
    rebuilt: Vec<forge_core::EntityId>,
    removed: Vec<forge_core::EntityId>,
}

#[derive(Default)]
struct Projection {
    objects: Vec<Object>,
    assets: BTreeMap<AssetKey, ResidentAsset>,
    started: f64,
    ready: bool,
    epoch: u64,
    last_mutation: ProjectionMutation,
}
impl Projection {
    fn clear(&mut self, commands: &mut Commands) {
        let removed = self.objects.iter().map(|object| object.id).collect();
        for object in self.objects.drain(..) {
            commands.entity(object.root).despawn();
        }
        for asset in std::mem::take(&mut self.assets).into_values() {
            super::preview::remove(&asset.path);
        }
        RESIDENT.with(|slot| slot.borrow_mut().clear());
        READY.with(|slot| slot.set(false));
        self.ready = false;
        self.last_mutation = ProjectionMutation {
            removed,
            ..default()
        };
    }
    fn retain_reusable(
        &mut self,
        placements: &[ManualPlacement],
        commands: &mut Commands,
    ) -> BTreeSet<forge_core::EntityId> {
        let previous = self.objects.iter().map(|object| object.id).collect();
        let mut mutation = ProjectionMutation::default();
        self.objects.retain(
            |object| match crate::authoring_placement_reuse::reconciliation_decision(
                &object.source,
                placements,
            ) {
                crate::authoring_placement_reuse::ReconciliationDecision::Retain => {
                    mutation.retained.push(object.id);
                    true
                }
                crate::authoring_placement_reuse::ReconciliationDecision::Rebuild => {
                    commands.entity(object.root).despawn();
                    mutation.rebuilt.push(object.id);
                    false
                }
                crate::authoring_placement_reuse::ReconciliationDecision::Remove => {
                    commands.entity(object.root).despawn();
                    mutation.removed.push(object.id);
                    false
                }
            },
        );
        let needed = placements
            .iter()
            .map(|p| (p.asset_id, p.asset_hash))
            .collect::<std::collections::BTreeSet<_>>();
        self.assets.retain(|key, asset| {
            let keep = needed.contains(key);
            if !keep {
                super::preview::remove(&asset.path);
            }
            keep
        });
        self.last_mutation = mutation;
        previous
    }
    fn spawn(
        &mut self,
        prepared: Prepared,
        commands: &mut Commands,
        server: &AssetServer,
    ) -> Result<(), &'static str> {
        if prepared.placements.iter().any(|p| {
            let key = (p.asset_id, p.asset_hash);
            !self.assets.contains_key(&key) && !prepared.assets.contains_key(&key)
        }) {
            return Err("Cache remplacé pendant la préparation ; réexaminer la proposition");
        }
        self.epoch = EPOCH.with(Cell::get);
        let previous = self.retain_reusable(&prepared.placements, commands);
        for (key, (name, bytes)) in prepared.assets {
            if self.assets.contains_key(&key) {
                continue;
            }
            let size = bytes.len();
            let path = format!(
                "placements-{}-{}-{}.glb",
                EPOCH.with(Cell::get),
                key.0,
                key.1
            );
            super::preview::insert(&path, bytes);
            let handle: Handle<WorldAsset> = server
                .load(GltfAssetLabel::Scene(0).from_asset(format!("authoring-preview://{path}")));
            self.assets.insert(
                key,
                ResidentAsset {
                    name,
                    handle,
                    path,
                    bytes: size,
                },
            );
        }
        RESIDENT.with(|slot| {
            slot.replace(
                self.assets
                    .iter()
                    .map(|(key, asset)| (*key, asset.bytes))
                    .collect(),
            )
        });
        let existing = self
            .objects
            .iter()
            .enumerate()
            .map(|(index, object)| (object.id, index))
            .collect::<BTreeMap<_, _>>();
        for placement in prepared.placements {
            if let Some(&index) = existing.get(&placement.id) {
                let object = &mut self.objects[index];
                if object.source != placement {
                    commands.entity(object.root).insert((
                        forge_engine::domain_transform_to_bevy(placement.transform),
                        gizmo::PlacementRoot(placement.id, placement.transform),
                        forge_engine::ManualPlacementProjection(placement.clone()),
                        Name::new(format!("Placement {} · {}", placement.id, placement.name)),
                    ));
                    if object.source.transform != placement.transform {
                        object.frame = None;
                    }
                    object.source = placement;
                }
                continue;
            }
            let Some(asset) = self.assets.get(&(placement.asset_id, placement.asset_hash)) else {
                continue;
            };
            if !previous.contains(&placement.id) {
                self.last_mutation.spawned.push(placement.id);
            }
            let t = placement.transform;
            let root = commands
                .spawn((
                    WorldAssetRoot(asset.handle.clone()),
                    gizmo::PlacementRoot(placement.id, t),
                    forge_engine::ManualPlacementProjection(placement.clone()),
                    Visibility::Hidden,
                    forge_engine::domain_transform_to_bevy(t),
                    Name::new(format!("Placement {} · {}", placement.id, placement.name)),
                ))
                .id();
            self.objects.push(Object {
                id: placement.id,
                root,
                handle: asset.handle.clone(),
                name: asset.name.clone(),
                source: placement,
                frame: None,
                parts: Vec::new(),
            });
        }
        self.ready = false;
        Ok(())
    }
}

pub(super) fn register(app: &mut App) {
    app.add_plugins(forge_engine::ManualPlacementCollisionPlugin);
    app.add_systems(PostUpdate, physics_status::report_status);
    app.add_systems(
        PostUpdate,
        (update, sync_visibility, selection::pick, gizmo::update)
            .chain()
            .after(bevy::transform::TransformSystems::Propagate)
            .after(bevy::camera::visibility::VisibilitySystems::CalculateBounds),
    );
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    reason = "Bevy schedules and borrow-checks each system parameter independently"
)]
fn update(
    mut commands: Commands,
    server: Res<AssetServer>,
    time: Res<Time>,
    instances: Query<&WorldInstance>,
    spawner: Res<WorldInstanceSpawner>,
    nodes: super::preview::PreviewNodes,
    diagnostic_nodes: DiagnosticNodes,
    mut state: Local<Projection>,
) {
    if let Some(request) = REQUEST.with(|slot| slot.borrow_mut().take()) {
        match request {
            Request::Clear => {
                state.clear(&mut commands);
                READY.with(|slot| slot.set(true));
            }
            Request::Show(prepared) => {
                if let Err(reason) = state.spawn(prepared, &mut commands, &server) {
                    FAILED.with(|slot| slot.set(true));
                    report(format!("Placements non projetés : {reason}"));
                    publish_diagnostic(&state, &diagnostic_nodes);
                    return;
                }
                state.started = time.elapsed_secs_f64();
            }
        }
        // Deferred root transforms must propagate before reframing and qualification.
        publish_diagnostic(&state, &diagnostic_nodes);
        return;
    }
    if let Some(id) = FOCUS.with(Cell::take) {
        if let Some((pivot, distance)) = state
            .objects
            .iter()
            .find(|o| o.id == id)
            .and_then(|o| o.frame)
        {
            crate::authoring_camera::focus_object(pivot, distance);
        } else {
            report("Objet non prêt pour le cadrage ; attendre sa projection puis réessayer.");
        }
    }
    if state.ready || state.objects.is_empty() || !current(state.epoch) {
        publish_diagnostic(&state, &diagnostic_nodes);
        return;
    }
    let result = qualify(
        &mut state,
        &mut commands,
        &server,
        &instances,
        &spawner,
        &nodes,
    );
    match result {
        Ok(true) => {
            for object in &state.objects {
                commands.entity(object.root).insert(PlacementVisualReady);
            }
            state.ready = true;
            READY.with(|slot| slot.set(current(state.epoch)));
            report(format!(
                "{} placements LOD0 projetés · préparation UCX demandée ; Jouer attend les collisions synchronisées.",
                state.objects.len()
            ));
        }
        Ok(false) if time.elapsed_secs_f64() - state.started <= 30.0 => {}
        result => {
            state.clear(&mut commands);
            FAILED.with(|slot| slot.set(true));
            report(format!(
                "Placements non projetés : {}",
                result.err().unwrap_or("Délai de chargement dépassé (30 s)")
            ));
        }
    }
    publish_diagnostic(&state, &diagnostic_nodes);
}

fn sync_visibility(
    mut roots: Query<(&gizmo::PlacementRoot, &mut Visibility), With<PlacementVisualReady>>,
) {
    for (placement, mut visibility) in &mut roots {
        let desired = if crate::authoring_vegetation::promotion_placement_visible(placement.0) {
            Visibility::Inherited
        } else {
            Visibility::Hidden
        };
        if *visibility != desired {
            *visibility = desired;
        }
    }
}

fn qualify(
    state: &mut Projection,
    commands: &mut Commands,
    server: &AssetServer,
    instances: &Query<&WorldInstance>,
    spawner: &WorldInstanceSpawner,
    nodes: &super::preview::PreviewNodes,
) -> Result<bool, &'static str> {
    for object in &mut state.objects {
        if object.frame.is_some() {
            continue;
        }
        if server.load_state(object.handle.id()).is_failed() {
            return Err("Chargement GLB échoué");
        }
        let Ok(instance) = instances.get(object.root) else {
            continue;
        };
        if !spawner.instance_is_ready(**instance) {
            continue;
        }
        object.frame = super::preview::configure(
            object.root,
            &object.name,
            spawner.iter_instance_entities(**instance),
            nodes,
            commands,
        )?;
        if object.frame.is_some() {
            let mut primitives = Vec::new();
            let parts = spawner
                .iter_instance_entities(**instance)
                .collect::<Vec<_>>();
            for &entity in &parts {
                if nodes
                    .get(entity)
                    .is_ok_and(|(_, _, mesh, _, _)| mesh.is_some())
                    && super::preview::is_collision(entity, object.root, &object.name, nodes)
                {
                    primitives.push(entity);
                    commands
                        .entity(entity)
                        .insert(forge_engine::ManualPlacementCollision(object.id));
                }
                if nodes
                    .get(entity)
                    .is_ok_and(|(_, _, mesh, _, _)| mesh.is_some())
                    && super::preview::is_lod0(entity, object.root, &object.name, nodes)
                {
                    commands
                        .entity(entity)
                        .insert(selection::PlacementPart(object.id));
                }
            }
            commands
                .entity(object.root)
                .insert(forge_engine::ManualPlacementCollisionExpected {
                    placement: object.id,
                    primitives,
                });
            object.parts = parts;
        }
    }
    Ok(state.objects.iter().all(|o| o.frame.is_some()))
}

fn object_id(id: forge_core::EntityId) -> String {
    id.to_string()
}

fn ecs_entity_id(entity: Entity) -> String {
    format!("0x{:016x}", entity.to_bits())
}

/// This only serializes facts already observed from the local derived world.
/// It neither influences the projection request nor enters the playtest barrier.
fn publish_diagnostic(state: &Projection, nodes: &DiagnosticNodes) {
    let expected_epoch = EPOCH.with(Cell::get);
    let requested_object_ids = DESIRED.with(|placements| {
        placements
            .borrow()
            .iter()
            .map(|placement| object_id(placement.id))
            .collect::<Vec<_>>()
    });
    let projection_state = if FAILED.with(Cell::get) {
        ProjectionState::Failed
    } else if state.objects.is_empty() && requested_object_ids.is_empty() {
        ProjectionState::NotRequested
    } else if state.epoch != expected_epoch {
        ProjectionState::Stale
    } else if state.ready && READY.with(Cell::get) {
        ProjectionState::Ready
    } else {
        ProjectionState::Pending
    };
    let objects = state
        .objects
        .iter()
        .map(|object| ObjectObservation {
            object_id: object_id(object.id),
            root_ecs_entity_id: Some(ecs_entity_id(object.root)),
            world_asset_handle: Some(format!("{:?}", object.handle.id())),
            parts: object
                .parts
                .iter()
                .map(|&entity| {
                    let (mesh_handle, material_handle, rapier_collider_handle) = nodes
                        .get(entity)
                        .map_or((None, None, None), |(mesh, material, rapier)| {
                            (
                                mesh.map(|handle| format!("{:?}", handle.0.id())),
                                material.map(|handle| format!("{:?}", handle.0.id())),
                                rapier.map(|handle| format!("{:?}", handle.0)),
                            )
                        });
                    PartObservation {
                        part_id: None,
                        role: None,
                        ecs_entity_id: ecs_entity_id(entity),
                        root_ecs_entity_id: Some(ecs_entity_id(object.root)),
                        mesh_handle,
                        material_handle,
                        rapier_collider_handle,
                    }
                })
                .collect(),
        })
        .collect();
    crate::authoring_projection_diagnostics::publish_placements(ScopeObservation {
        expected_source_hash: None,
        observed_source_hash: None,
        expected_epoch: Some(expected_epoch),
        observed_epoch: Some(state.epoch),
        state: projection_state,
        requested_object_ids,
        objects,
        mutation: MutationObservation {
            retained: state
                .last_mutation
                .retained
                .iter()
                .copied()
                .map(object_id)
                .collect(),
            spawned: state
                .last_mutation
                .spawned
                .iter()
                .copied()
                .map(object_id)
                .collect(),
            rebuilt: state
                .last_mutation
                .rebuilt
                .iter()
                .copied()
                .map(object_id)
                .collect(),
            removed: state
                .last_mutation
                .removed
                .iter()
                .copied()
                .map(object_id)
                .collect(),
            ..Default::default()
        },
    });
}
