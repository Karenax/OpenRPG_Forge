//! One persistent project viewport, fed exclusively by native source projections.
use crate::{
    StudioCommand, StudioResponse,
    area_preview::{AreaPreviewRequest, read_area_preview},
    area_tabs::{AreaKey, AreaTabs},
    asset_drag::{AssetDragState, AssetDropScope, AssetSurfaceHit},
    workspace_api::WorkspaceCommand,
    workspace_ui::{WorkspaceActions, invoke_studio},
    world_selection::{
        SelectedPlacementDetails, WorldPickResolution, WorldPickSample, WorldPointerGesture,
        WorldSelectionScope, WorldSelectionState,
    },
};
use bevy::{
    asset::AssetMetaCheck,
    prelude::*,
    render::{
        RenderPlugin,
        settings::{RenderCreation, WgpuSettings},
    },
    window::{PresentMode, WindowResolution},
};
use forge_engine::editor_viewport::{
    EditorPathRequest, EditorRiverRequest, EditorRoadRequest, EditorSurfacePointer,
    EditorTerrainRequest, EditorVegetationRequest, EditorViewport, EditorViewportPlugin,
    EditorViewportStatus, EditorWallRequest,
};
use forge_engine::{
    EditorPlacementAssetBudget, EditorPlacementPointer, EditorPlacementProjectionBridge,
    EditorPlacementProjectionPlugin, EditorPlacementProjectionStatus, EditorPlacementSample,
};
use forge_project::WorkspaceDocument;
use leptos::prelude::Update;
use leptos::prelude::*;
use wasm_bindgen::{JsCast, closure::Closure};

#[path = "brush_ui.rs"]
mod brush;
use brush::{BrushActions, BrushPanel, VegetationPanel, create_brush_actions};
#[path = "path_ui.rs"]
mod path;
use path::{PathActions, PathPanel, create_path_actions};

#[derive(Clone, Copy)]
pub(crate) struct WorldActions {
    tabs: RwSignal<AreaTabs>,
    document: Memo<Option<WorkspaceDocument>>,
    viewport: StoredValue<EditorViewport>,
    placement_viewport: StoredValue<EditorPlacementProjectionBridge>,
    status: RwSignal<EditorViewportStatus>,
    placement_status: RwSignal<EditorPlacementProjectionStatus>,
    selection: RwSignal<WorldSelectionState>,
    started: RwSignal<bool>,
    observed_starts: RwSignal<u32>,
    busy: RwSignal<bool>,
    density_preview_busy: RwSignal<bool>,
    error: RwSignal<Option<String>>,
    generation: RwSignal<u64>,
    canonical_refresh: RwSignal<u64>,
}

impl WorldActions {
    fn invalidate_projection(self, target: Option<AreaKey>) {
        self.generation.update(|generation| *generation += 1);
        let generation = self.generation.get_untracked();
        self.error.set(None);
        if let Ok(mut bridge) = self.viewport.get_value().0.lock() {
            bridge.desired_generation = self.generation.get_untracked();
            bridge.clear = target.is_none_or(|key| {
                bridge.status.project_id != Some(key.project_id)
                    || bridge.status.area_id != Some(key.area_id)
                    || bridge.status.chunks != 64
            });
            bridge.pending = None;
            bridge.path_draft = None;
            bridge.surface_pointer = None;
            bridge.surface_hit = None;
            bridge.surface_sample_serial = None;
        }
        if target.is_none()
            && let Err(error) = self.placement_viewport.get_value().clear(generation)
        {
            self.error
                .set(Some(format!("Effacement des Assets refusé : {error}")));
        }
        let scope = self.selection_scope();
        let document = self.document.get_untracked();
        self.selection
            .update(|selection| selection.reconcile(scope, document.as_ref()));
    }

    pub(crate) fn open(self, key: AreaKey) {
        if let Some(document) = self.document.get_untracked() {
            self.tabs.update(|tabs| tabs.open(key, &document));
        }
    }

    fn request_canonical_projection(self) {
        self.invalidate_projection(self.tabs.get_untracked().active);
        self.canonical_refresh.update(|serial| *serial += 1);
    }

    fn selection_scope(self) -> Option<WorldSelectionScope> {
        let key = self.tabs.get_untracked().active?;
        let document = self.document.get_untracked()?;
        let placement_revision = document
            .area(key.module_id, key.area_id)?
            .placements
            .revision
            .get();
        (document.project.id == key.project_id).then_some(WorldSelectionScope {
            key,
            revision: placement_revision,
            generation: self.generation.get_untracked(),
        })
    }

    fn density_preview_base_hash(
        self,
        generation: u64,
        request: AreaPreviewRequest,
    ) -> Option<forge_core::ContentHash> {
        let viewport = self.viewport.get_value();
        let bridge = viewport.0.lock().ok()?;
        let status = &bridge.status;
        (status.generation == generation
            && status.project_id == Some(request.key.project_id)
            && status.area_id == Some(request.key.area_id)
            && status.chunks == 64)
            .then(|| status.source_hash.parse().ok())
            .flatten()
    }

    #[allow(
        clippy::too_many_lines,
        reason = "one atomic publication validates and transports every correlated projection source"
    )]
    fn publish_density_preview(
        self,
        generation: u64,
        candidate_revision: u64,
        preview: crate::area_preview::AreaPreview,
    ) -> Result<(), String> {
        let request = preview.request;
        let document = self
            .document
            .get_untracked()
            .ok_or("Projet absent pendant l’aperçu de densité.")?;
        if self.generation.get_untracked() != generation
            || self.tabs.get_untracked().active != Some(request.key)
            || document.revision.get() != request.base_revision
        {
            return Err("Cible d’aperçu périmée : projection ignorée.".into());
        }
        let area = document
            .area(request.key.module_id, request.key.area_id)
            .ok_or("Zone absente pendant l’aperçu de densité.")?;
        let terrain_hash = area
            .terrain
            .content_hash()
            .map_err(|error| error.to_string())?;
        let path_hash = area
            .paths
            .content_hash()
            .map_err(|error| error.to_string())?;
        let road_hash = area
            .roads
            .content_hash()
            .map_err(|error| error.to_string())?;
        let river_hash = area
            .rivers
            .content_hash()
            .map_err(|error| error.to_string())?;
        let wall_hash = area
            .walls
            .content_hash()
            .map_err(|error| error.to_string())?;
        let source_terrain = area.terrain.clone();
        let source_paths = area.paths.clone();
        let source_roads = area.roads.clone();
        let source_rivers = area.rivers.clone();
        let source_walls = area.walls.clone();
        let effective_terrain_hash = preview.rivers.effective_terrain_document_hash;
        let preview_effective_terrain_hash = preview
            .effective_terrain
            .content_hash()
            .map_err(|error| error.to_string())?;
        if preview.rivers.terrain_document_hash != terrain_hash
            || preview.rivers.path_document_hash != path_hash
            || preview.rivers.road_document_hash != road_hash
            || preview.rivers.river_document_hash != river_hash
            || preview.terrain.source_document_hash != effective_terrain_hash
            || preview.rivers.effective_terrain_document_hash
                != preview.terrain.source_document_hash
            || preview_effective_terrain_hash != effective_terrain_hash
            || preview.vegetation.terrain_document_hash != effective_terrain_hash
            || preview.paths.terrain_document_hash != effective_terrain_hash
            || preview.paths.path_document_hash != path_hash
            || preview.rivers.path_document_hash != preview.paths.path_document_hash
            || preview.roads.terrain_document_hash != effective_terrain_hash
            || preview.roads.path_document_hash != path_hash
            || preview.roads.road_document_hash != road_hash
            || preview.rivers.road_document_hash != preview.roads.road_document_hash
            || preview.walls.terrain_document_hash != effective_terrain_hash
            || preview.walls.path_document_hash != path_hash
            || preview.walls.wall_document_hash != wall_hash
            || preview.walls.path_document_hash != preview.paths.path_document_hash
        {
            return Err("Terrain, routes, rivières ou murs de l’aperçu de densité incohérents : projection ignorée.".into());
        }
        let viewport = self.viewport.get_value();
        let mut bridge = viewport
            .0
            .lock()
            .map_err(|_| "Viewport indisponible pour l’aperçu.".to_owned())?;
        if bridge.desired_generation != generation {
            return Err("Génération d’aperçu périmée : projection ignorée.".into());
        }
        bridge.pending = Some(EditorTerrainRequest {
            generation,
            project_id: request.key.project_id,
            area_id: request.key.area_id,
            revision: candidate_revision,
            terrain: preview.terrain,
            vegetation: Some(EditorVegetationRequest {
                generated: preview.vegetation,
                density_source_hash: preview.vegetation_density_source_hash,
            }),
            paths: Some(EditorPathRequest {
                generated: preview.paths,
                source_terrain: preview.effective_terrain,
                source_paths,
            }),
            roads: Some(EditorRoadRequest {
                generated: preview.roads,
                source_roads,
            }),
            rivers: Some(EditorRiverRequest {
                generated: preview.rivers,
                source_terrain,
                source_rivers,
            }),
            walls: Some(EditorWallRequest {
                generated: preview.walls,
                source_walls,
            }),
            spawn: [preview.spawn.x, preview.spawn.y, preview.spawn.z],
            base_source_hash: preview.base_source_hash,
        });
        drop(bridge);
        start_once(self);
        Ok(())
    }

    fn density_preview_observed(
        self,
        generation: u64,
        request: AreaPreviewRequest,
        candidate_revision: u64,
        density_hash: forge_core::ContentHash,
    ) -> bool {
        let status = self.status.get_untracked();
        status.generation == generation
            && status.project_id == Some(request.key.project_id)
            && status.area_id == Some(request.key.area_id)
            && status.revision == candidate_revision
            && status.chunks == 64
            && status.pending_chunks == 0
            && status.error.is_none()
            && status.vegetation_density_source_hash == density_hash.to_string()
    }

    fn status_text(self) -> String {
        if let Some(error) = self
            .error
            .get()
            .or(self.status.get().error)
            .or(self.placement_status.get().error)
        {
            return error;
        }
        if self.tabs.get().active.is_none() {
            return "Ouvrez une zone du projet pour afficher son terrain.".into();
        }
        if self.busy.get() {
            return "Préparation native du terrain et de la végétation… Les sources restent éditables."
                .into();
        }
        let status = self.status.get();
        let placements = self.placement_status.get();
        if status.generation != self.generation.get() || status.chunks < 64 {
            return format!("Projection en cours · {}/64 chunks", status.chunks);
        }
        if placements.presented_generation != self.generation.get() {
            return format!(
                "Chargement atomique des Assets · {} prêts / {} en attente",
                placements.retained_placements, placements.staged_placements
            );
        }
        format!(
            "Monde du projet · r{} · terrain {} recalculés / {} conservés · végétation {} instances, {} recalculés / {} conservés · assets {} placés · chemins {} / {} points · routes {} / {} sommets · rivières {} / {} sommets · murs {} / {} sommets · boucle {:.1} ms",
            status.revision,
            status.rebuilt_chunks,
            status.retained_chunks,
            status.vegetation_instances,
            status.vegetation_rebuilt_chunks,
            status.vegetation_retained_chunks,
            placements.presented_placements,
            status.path_count,
            status.path_samples,
            status.road_count,
            status.road_vertices,
            status.river_count,
            status.river_vertices,
            status.wall_count,
            status.wall_vertices,
            status.frame_ms
        )
    }
}

pub(crate) fn create_world_actions(workspace: WorkspaceActions) -> WorldActions {
    let document = Memo::new(move |_| {
        workspace
            .state
            .get()
            .document_json
            .as_deref()
            .and_then(|json| WorkspaceDocument::from_json(json).ok())
    });
    let actions = WorldActions {
        tabs: RwSignal::new(AreaTabs::default()),
        document,
        viewport: StoredValue::new(EditorViewport::default()),
        placement_viewport: StoredValue::new(EditorPlacementProjectionBridge::default()),
        status: RwSignal::new(EditorViewportStatus::default()),
        placement_status: RwSignal::new(EditorPlacementProjectionStatus::default()),
        selection: RwSignal::new(WorldSelectionState::default()),
        started: RwSignal::new(false),
        observed_starts: RwSignal::new(0),
        busy: RwSignal::new(false),
        density_preview_busy: RwSignal::new(false),
        error: RwSignal::new(None),
        generation: RwSignal::new(0),
        canonical_refresh: RwSignal::new(0),
    };
    install_tab_reconciliation(actions);
    let wanted = area_preview_target(actions);
    install_preview_updates(actions, wanted);
    install_status_poll(actions);
    actions
}

fn install_tab_reconciliation(actions: WorldActions) {
    Effect::new(move |_| {
        let document = actions.document.get();
        let before = actions.tabs.get_untracked();
        let mut after = before.clone();
        after.reconcile(document.as_ref());
        if after != before {
            actions.tabs.set(after);
        }
        let scope = actions.selection_scope();
        actions
            .selection
            .update(|selection| selection.reconcile(scope, document.as_ref()));
    });
}

#[derive(Clone, Copy, Eq, PartialEq)]
struct AreaPreviewTarget {
    request: AreaPreviewRequest,
    terrain_hash: forge_core::ContentHash,
    vegetation_hash: forge_core::ContentHash,
    vegetation_density_hash: forge_core::ContentHash,
    path_hash: forge_core::ContentHash,
    road_hash: forge_core::ContentHash,
    river_hash: forge_core::ContentHash,
    wall_hash: forge_core::ContentHash,
    placement_hash: forge_core::ContentHash,
}

fn current_projection_sources(
    actions: WorldActions,
    target: &AreaPreviewTarget,
) -> Result<
    (
        forge_project::TerrainDocument,
        forge_project::PathDocument,
        forge_project::RoadDocument,
        forge_project::RiverDocument,
        forge_project::WallDocument,
        forge_project::PlacementDocument,
    ),
    String,
> {
    let document = actions
        .document
        .get_untracked()
        .ok_or("Projet absent après la génération de l’aperçu.")?;
    if document.revision.get() != target.request.base_revision
        || actions.tabs.get_untracked().active != Some(target.request.key)
        || !target.request.key.exists(&document)
    {
        return Err(
            "Cible modifiée pendant la génération de l’aperçu : projection ignorée.".into(),
        );
    }
    let area = document
        .area(target.request.key.module_id, target.request.key.area_id)
        .ok_or("Zone absente après la génération de l’aperçu.")?;
    if area
        .terrain
        .content_hash()
        .map_err(|error| error.to_string())?
        != target.terrain_hash
        || area
            .paths
            .content_hash()
            .map_err(|error| error.to_string())?
            != target.path_hash
        || area
            .roads
            .content_hash()
            .map_err(|error| error.to_string())?
            != target.road_hash
        || area
            .rivers
            .content_hash()
            .map_err(|error| error.to_string())?
            != target.river_hash
        || area
            .walls
            .content_hash()
            .map_err(|error| error.to_string())?
            != target.wall_hash
        || area
            .placements
            .content_hash()
            .map_err(|error| error.to_string())?
            != target.placement_hash
    {
        return Err(
            "Sources terrain/chemin/route/rivière/mur modifiées pendant la génération de l’aperçu."
                .into(),
        );
    }
    Ok((
        area.terrain.clone(),
        area.paths.clone(),
        area.roads.clone(),
        area.rivers.clone(),
        area.walls.clone(),
        area.placements.clone(),
    ))
}

fn area_preview_target(actions: WorldActions) -> Memo<Option<AreaPreviewTarget>> {
    Memo::new(move |_| {
        let key = actions.tabs.get().active?;
        let document = actions.document.get()?;
        if !key.exists(&document) {
            return None;
        }
        let area = document.area(key.module_id, key.area_id)?;
        Some(AreaPreviewTarget {
            request: AreaPreviewRequest {
                key,
                base_revision: document.revision.get(),
            },
            terrain_hash: area.terrain.content_hash().ok()?,
            vegetation_hash: area.vegetation.content_hash().ok()?,
            vegetation_density_hash: area.vegetation_density.content_hash().ok()?,
            path_hash: area.paths.content_hash().ok()?,
            road_hash: area.roads.content_hash().ok()?,
            river_hash: area.rivers.content_hash().ok()?,
            wall_hash: area.walls.content_hash().ok()?,
            placement_hash: area.placements.content_hash().ok()?,
        })
    })
}

fn current_base_source_hash(actions: WorldActions) -> Option<forge_core::ContentHash> {
    actions
        .viewport
        .get_value()
        .0
        .lock()
        .ok()
        .and_then(|bridge| {
            if !bridge.clear && bridge.status.chunks == 64 {
                bridge.status.source_hash.parse().ok()
            } else {
                None
            }
        })
}

fn placement_asset_budgets(
    binding: &crate::workspace_api::PlacementProjectionBinding,
) -> Vec<EditorPlacementAssetBudget> {
    binding
        .asset_budgets
        .iter()
        .map(|budget| EditorPlacementAssetBudget {
            asset_id: budget.asset_id,
            tracked_bytes: budget.tracked_bytes,
        })
        .collect()
}

fn install_preview_updates(actions: WorldActions, wanted: Memo<Option<AreaPreviewTarget>>) {
    Effect::new(move |_| {
        actions.invalidate_projection(wanted.get().map(|target| target.request.key));
    });
    let completed = RwSignal::new(None::<(u64, u64)>);
    Effect::new(move |_| {
        let generation = actions.generation.get();
        let refresh = actions.canonical_refresh.get();
        let target = wanted.get();
        if actions.busy.get()
            || actions.density_preview_busy.get()
            || completed.get() == Some((generation, refresh))
        {
            return;
        }
        let Some(target) = target else {
            return;
        };
        actions.busy.set(true);
        let base_source_hash = current_base_source_hash(actions);
        wasm_bindgen_futures::spawn_local(async move {
            let result = fetch_preview(&target, base_source_hash).await;
            if actions.generation.get_untracked() == generation
                && actions.canonical_refresh.get_untracked() == refresh
            {
                match result {
                    Ok((preview, placement_binding)) => {
                        match current_projection_sources(actions, &target) {
                            Ok((
                                source_terrain,
                                source_paths,
                                source_roads,
                                source_rivers,
                                source_walls,
                                source_placements,
                            )) => {
                                let asset_budgets = placement_asset_budgets(&placement_binding);
                                if let Err(error) = actions
                                    .placement_viewport
                                    .get_value()
                                    .submit_document(generation, &source_placements, &asset_budgets)
                                {
                                    actions.error.set(Some(format!(
                                        "Projection des Assets refusée : {error}"
                                    )));
                                    actions.busy.set(false);
                                    return;
                                }
                                if let Ok(mut bridge) = actions.viewport.get_value().0.lock() {
                                    bridge.pending = Some(EditorTerrainRequest {
                                        generation,
                                        project_id: target.request.key.project_id,
                                        area_id: target.request.key.area_id,
                                        revision: target.request.base_revision,
                                        terrain: preview.terrain,
                                        vegetation: Some(EditorVegetationRequest {
                                            generated: preview.vegetation,
                                            density_source_hash: preview
                                                .vegetation_density_source_hash,
                                        }),
                                        paths: Some(EditorPathRequest {
                                            generated: preview.paths,
                                            source_terrain: preview.effective_terrain,
                                            source_paths,
                                        }),
                                        roads: Some(EditorRoadRequest {
                                            generated: preview.roads,
                                            source_roads,
                                        }),
                                        rivers: Some(EditorRiverRequest {
                                            generated: preview.rivers,
                                            source_terrain,
                                            source_rivers,
                                        }),
                                        walls: Some(EditorWallRequest {
                                            generated: preview.walls,
                                            source_walls,
                                        }),
                                        spawn: [preview.spawn.x, preview.spawn.y, preview.spawn.z],
                                        base_source_hash: preview.base_source_hash,
                                    });
                                }
                                start_once(actions);
                            }
                            Err(error) => actions.error.set(Some(error)),
                        }
                    }
                    Err(error) => actions.error.set(Some(error)),
                }
                completed.set(Some((generation, refresh)));
            }
            actions.busy.set(false);
        });
    });
}

fn install_status_poll(actions: WorldActions) {
    let poll = Closure::<dyn FnMut()>::new(move || {
        if let Ok(bridge) = actions.viewport.get_value().0.lock() {
            actions.status.set(bridge.status.clone());
            actions.observed_starts.set(bridge.renderer_starts);
        }
        let placement = actions.placement_viewport.get_value();
        actions.placement_status.set(placement.status());
        if let Some(sample) = placement.pick_sample() {
            apply_placement_sample(actions, sample);
        }
    });
    if let Some(window) = web_sys::window() {
        match window.set_interval_with_callback_and_timeout_and_arguments_0(
            poll.as_ref().unchecked_ref(),
            250,
        ) {
            Ok(_) => poll.forget(), // A single desktop owns the page lifetime.
            Err(error) => actions
                .error
                .set(Some(format!("Diagnostics indisponibles : {error:?}"))),
        }
    }
}

fn apply_placement_sample(actions: WorldActions, sample: EditorPlacementSample) {
    let Some(scope) = actions.selection_scope() else {
        actions
            .selection
            .update(|selection| selection.reconcile(None, None));
        return;
    };
    let Some(document) = actions.document.get_untracked() else {
        return;
    };
    let mapped = WorldPickSample {
        generation: sample.generation,
        serial: sample.serial,
        project_id: sample.project_id,
        area_id: sample.area_id,
        revision: sample.revision,
        placement_id: sample.placement_id,
    };
    let mut selection = actions.selection.get_untracked();
    match selection.apply_sample(scope, mapped, &document) {
        Ok(WorldPickResolution::IgnoredStale) => {}
        Ok(WorldPickResolution::Cleared { .. } | WorldPickResolution::Selected(_)) => {
            actions.selection.set(selection);
        }
        Err(error) => {
            actions.selection.set(selection);
            actions
                .error
                .set(Some(format!("Résultat de picking 3D refusé : {error}")));
        }
    }
}

async fn fetch_preview(
    target: &AreaPreviewTarget,
    base_source_hash: Option<forge_core::ContentHash>,
) -> Result<
    (
        crate::area_preview::AreaPreview,
        crate::workspace_api::PlacementProjectionBinding,
    ),
    String,
> {
    let request = target.request;
    match invoke_studio(StudioCommand::AreaPreview {
        request,
        base_source_hash,
    })
    .await?
    {
        StudioResponse::AreaPreview {
            preview_json,
            placement,
        } => {
            let preview = read_area_preview(&preview_json)?;
            let effective_terrain_hash = preview.rivers.effective_terrain_document_hash;
            let preview_effective_terrain_hash = preview
                .effective_terrain
                .content_hash()
                .map_err(|error| error.to_string())?;
            if preview.request != request
                || preview.rivers.terrain_document_hash != target.terrain_hash
                || preview.rivers.path_document_hash != target.path_hash
                || preview.rivers.road_document_hash != target.road_hash
                || preview.rivers.river_document_hash != target.river_hash
                || preview.terrain.source_document_hash != effective_terrain_hash
                || preview.rivers.effective_terrain_document_hash
                    != preview.terrain.source_document_hash
                || preview_effective_terrain_hash != effective_terrain_hash
                || preview.vegetation.terrain_document_hash != effective_terrain_hash
                || preview.vegetation.vegetation_document_hash != target.vegetation_hash
                || preview.vegetation_density_source_hash != target.vegetation_density_hash
                || preview.paths.terrain_document_hash != effective_terrain_hash
                || preview.paths.path_document_hash != target.path_hash
                || preview.rivers.path_document_hash != preview.paths.path_document_hash
                || preview.roads.terrain_document_hash != effective_terrain_hash
                || preview.roads.path_document_hash != target.path_hash
                || preview.roads.road_document_hash != target.road_hash
                || preview.rivers.road_document_hash != preview.roads.road_document_hash
                || preview.walls.terrain_document_hash != effective_terrain_hash
                || preview.walls.path_document_hash != target.path_hash
                || preview.walls.wall_document_hash != target.wall_hash
                || preview.walls.path_document_hash != preview.paths.path_document_hash
                || preview.vegetation_changed_chunks + preview.vegetation_retained_chunks != 64
                || placement.source_hash != target.placement_hash
                || (preview.base_source_hash.is_some()
                    && preview.base_source_hash != base_source_hash)
            {
                return Err("Aperçu périmé ou incohérent : aucune projection acceptée.".into());
            }
            Ok((preview, placement))
        }
        StudioResponse::Rejected { reason } => Err(reason),
        _ => Err("Réponse inattendue pour l’aperçu.".into()),
    }
}

fn start_once(actions: WorldActions) {
    if actions.started.get_untracked() {
        return;
    }
    actions.started.set(true);
    let callback = Closure::once_into_js(move || {
        let mut app = App::new();
        app.insert_resource(ClearColor(Color::srgb(0.50, 0.63, 0.70)))
            .add_plugins(
                DefaultPlugins
                    .set(WindowPlugin {
                        primary_window: Some(Window {
                            title: "Forge · Monde".into(),
                            canvas: Some("#forge-world-canvas".into()),
                            fit_canvas_to_parent: true,
                            prevent_default_event_handling: false,
                            focused: false,
                            resolution: WindowResolution::new(1024, 600),
                            present_mode: PresentMode::AutoVsync,
                            ..default()
                        }),
                        ..default()
                    })
                    .set(AssetPlugin {
                        meta_check: AssetMetaCheck::Never,
                        ..default()
                    })
                    .set(RenderPlugin {
                        render_creation: RenderCreation::Automatic(Box::new(WgpuSettings {
                            power_preference: bevy::render::settings::PowerPreference::None,
                            ..default()
                        })),
                        ..default()
                    }),
            )
            .add_plugins(EditorPlacementProjectionPlugin(
                actions.placement_viewport.get_value(),
            ))
            .add_plugins(EditorViewportPlugin(actions.viewport.get_value()))
            .run();
    });
    if let Some(window) = web_sys::window()
        && let Err(error) = window.request_animation_frame(callback.unchecked_ref())
    {
        actions.error.set(Some(format!(
            "Démarrage du viewport impossible : {error:?}"
        )));
    }
}

#[component]
fn WorldToolbar(actions: WorldActions) -> impl IntoView {
    view! {
        <div class="world-toolbar">
            <button type="button" on:click=move |_| { if let Ok(mut bridge) = actions.viewport.get_value().0.lock() { bridge.input.reset = true; } }>"Vue d’ensemble"</button>
            <button type="button" disabled=move || actions.tabs.get().active.is_none() on:click=move |_| {
                let Some(key) = actions.tabs.get_untracked().active else { return };
                if let Some(spawn) = actions.document.get_untracked().and_then(|doc| doc.area(key.module_id, key.area_id).map(|area| area.spawn))
                    && let Ok(mut bridge) = actions.viewport.get_value().0.lock() { bridge.input.focus = Some([spawn.x, spawn.y, spawn.z]); }
            }>"Voir le départ"</button>
            <span>"Glisser : orbite · Maj + glisser : déplacement · molette : zoom"</span>
        </div>
    }
}

#[component]
pub(crate) fn WorldWorkspace(
    actions: WorldActions,
    workspace: WorkspaceActions,
    asset_drag: RwSignal<AssetDragState>,
) -> impl IntoView {
    let drag = RwSignal::new(None::<WorldPointerGesture>);
    let brush = create_brush_actions(actions, workspace);
    let paths = create_path_actions(actions, workspace, brush);
    view! {
        <section class="world-workspace" aria-label="Éditeur de zones">
            <aside class="world-area-browser">
                <h2>"Zones du projet"</h2>
                {move || actions.document.get().map(|document| {
                    let project_id = document.project.id;
                    document.project.modules.into_iter().map(|module| {
                        let module_id = module.id;
                        view! { <section><h3>{module.name}</h3>{module.areas.into_iter().map(|area| {
                            let key = AreaKey { project_id, module_id, area_id: area.id };
                            view! { <button type="button" class:active=move || actions.tabs.get().active == Some(key)
                                on:click=move |_| actions.open(key)>{area.name}</button> }
                        }).collect_view()}</section> }
                    }).collect_view()
                })}
                <p class="hint">"Les sources s’éditent dans Projets. Fermer un onglet ne supprime pas la zone."</p>
                <BrushPanel brush=brush world=actions workspace=workspace/>
                <VegetationPanel brush=brush world=actions workspace=workspace/>
                <PathPanel paths=paths brush=brush world=actions workspace=workspace/>
                <WorldSelectionPanel actions/>
            </aside>
            <div class="world-editor">
                <div class="world-tabs" role="tablist" aria-label="Zones ouvertes">
                    {move || actions.tabs.get().open.into_iter().map(|key| {
                        let name = actions.document.get().and_then(|doc| doc.area(key.module_id, key.area_id).map(|area| area.name.clone())).unwrap_or_default();
                        view! { <span class="world-tab">
                            <button role="tab" type="button" aria-selected=move || (actions.tabs.get().active == Some(key)).to_string()
                                on:click=move |_| actions.open(key)>{name.clone()}</button>
                            <button type="button" aria-label=format!("Fermer l’onglet {name}")
                                on:click=move |_| actions.tabs.update(|tabs| tabs.close(key))>"×"</button>
                        </span> }
                    }).collect_view()}
                </div>
                <WorldToolbar actions/>
                <WorldCanvas actions workspace asset_drag drag brush paths/>
            </div>
        </section>
    }
}

#[component]
fn WorldCanvas(
    actions: WorldActions,
    workspace: WorkspaceActions,
    asset_drag: RwSignal<AssetDragState>,
    drag: RwSignal<Option<WorldPointerGesture>>,
    brush: BrushActions,
    paths: PathActions,
) -> impl IntoView {
    view! {
        <div class="world-canvas-host">
            <canvas id="forge-world-canvas" aria-label="Terrain 3D de la zone active" tabindex="0"
                on:keydown=move |event: web_sys::KeyboardEvent| { if event.key() == "Escape" {
                    paths.cancel(actions, false); paths.clear_hover(actions);
                    brush.cancel(actions, false); brush.clear_hover(actions);
                    cancel_asset_drag(actions, asset_drag);
                    actions.selection.update(|selection| { selection.clear(); });
                } }
                on:pointerleave=move |_| { paths.clear_hover(actions); brush.clear_hover(actions); }
                on:pointerdown=move |event: web_sys::PointerEvent| {
                    if paths.start(actions, workspace, brush, &event) { return; }
                    if brush.start(actions, workspace, &event) { return; }
                    if event.button() != 0 { return; }
                    if let Some(element) = event.current_target().and_then(|target| target.dyn_into::<web_sys::Element>().ok())
                        && element.set_pointer_capture(event.pointer_id()).is_ok() {
                        event.prevent_default();
                        drag.set(Some(WorldPointerGesture::begin(
                            event.pointer_id(),
                            [event.client_x(), event.client_y()],
                            event.shift_key(),
                        )));
                    }
                }
                on:pointermove=move |event: web_sys::PointerEvent| {
                    if !paths.move_pointer(actions, &event) { brush.move_pointer(actions, &event); }
                    let Some(mut gesture) = drag.get_untracked() else { return };
                    let Some(pointer_delta) = gesture.move_to(
                        event.pointer_id(),
                        [event.client_x(), event.client_y()],
                    ) else { return };
                    apply_camera_gesture_delta(actions, gesture.pan, pointer_delta);
                    drag.set(Some(gesture));
                }
                on:pointerup=move |event: web_sys::PointerEvent| {
                    if !paths.finish(actions, &event) { brush.finish(actions, &event); }
                    let Some(mut gesture) = drag.get_untracked() else { return };
                    let Some(pointer_delta) = gesture.move_to(
                        event.pointer_id(),
                        [event.client_x(), event.client_y()],
                    ) else { return };
                    drag.set(None);
                    apply_camera_gesture_delta(actions, gesture.pan, pointer_delta);
                    if let Some(viewport_uv) = pointer_viewport_uv(&event)
                        .and_then(|uv| gesture.finish(event.pointer_id(), uv))
                    {
                        request_placement_pick(actions, viewport_uv);
                    }
                }
                on:pointercancel=move |_| { paths.cancel(actions, false); brush.cancel(actions, false); drag.set(None); }
                on:lostpointercapture=move |_| { paths.cancel(actions, true); brush.cancel(actions, true); drag.set(None); }
                on:wheel=move |event: web_sys::WheelEvent| {
                    event.prevent_default();
                    if let Ok(mut bridge) = actions.viewport.get_value().0.lock() { bridge.input.zoom += wheel_delta(event.delta_y()); }
                }
                on:dragover=move |event: web_sys::DragEvent| {
                    if request_asset_surface(actions, asset_drag, &event) {
                        event.prevent_default();
                    }
                }
                on:dragleave=move |_| clear_asset_surface(actions)
                on:drop=move |event: web_sys::DragEvent| {
                    event.prevent_default();
                    schedule_asset_drop(actions, workspace, asset_drag);
                }/>
            <Show when=move || actions.tabs.get().active.is_none()>
                <div class="world-empty"><h2>"Un monde à ouvrir"</h2><p>"Choisissez une zone à gauche, ou créez-la dans Projets."</p></div>
            </Show>
        </div>
        <footer class="world-status" role="status">{move || {
            workspace.error.get().unwrap_or_else(|| actions.status_text())
        }}</footer>
    }
}

#[component]
fn WorldSelectionPanel(actions: WorldActions) -> impl IntoView {
    view! {
        <section class="world-selection" aria-label="Sélection du monde">
            <h3>"Sélection"</h3>
            {move || selected_placement_details(actions).map_or_else(
                || view! { <p class="hint">"Cliquez un Asset placé pour l’inspecter."</p> }.into_any(),
                |details| view! {
                    <dl>
                        <dt>"Source"</dt><dd>{details.stable_key}</dd>
                        <dt>"Identifiant"</dt><dd>{details.placement_id.to_string()}</dd>
                        <dt>"Position"</dt><dd>{format!(
                            "{:.2}, {:.2}, {:.2} m",
                            details.transform.translation_m.x,
                            details.transform.translation_m.y,
                            details.transform.translation_m.z,
                        )}</dd>
                        <dt>"Échelle"</dt><dd>{format!(
                            "{:.2}, {:.2}, {:.2}",
                            details.transform.scale.x,
                            details.transform.scale.y,
                            details.transform.scale.z,
                        )}</dd>
                    </dl>
                    <button type="button" on:click=move |_| {
                        actions.selection.update(|selection| { selection.clear(); });
                    }>"Désélectionner"</button>
                }.into_any(),
            )}
        </section>
    }
}

fn selected_placement_details(actions: WorldActions) -> Option<SelectedPlacementDetails> {
    let document = actions.document.get()?;
    actions.selection.get().details(&document)
}

fn request_placement_pick(actions: WorldActions, viewport_uv: [f32; 2]) {
    let result = (|| {
        let scope = actions
            .selection_scope()
            .ok_or("Aucune projection de zone active à sélectionner.")?;
        let mut selection = actions.selection.get_untracked();
        let request = selection
            .request_pick(scope, viewport_uv)
            .map_err(|error| format!("Sélection refusée : {error}"))?;
        actions
            .placement_viewport
            .get_value()
            .request_pick(EditorPlacementPointer {
                generation: request.generation,
                serial: request.serial,
                viewport_uv: request.viewport_uv,
            })
            .map_err(|error| format!("Picking 3D refusé : {error}"))?;
        actions.selection.set(selection);
        Ok::<(), String>(())
    })();
    match result {
        Ok(()) => actions.error.set(None),
        Err(error) => actions.error.set(Some(error)),
    }
}

fn pointer_viewport_uv(event: &web_sys::PointerEvent) -> Option<[f32; 2]> {
    let element = event
        .current_target()?
        .dyn_into::<web_sys::Element>()
        .ok()?;
    let (width, height) = (element.client_width(), element.client_height());
    if width <= 0 || height <= 0 {
        return None;
    }
    #[allow(
        clippy::cast_possible_truncation,
        reason = "Normalized finite canvas coordinates"
    )]
    let viewport_uv = [
        (f64::from(event.offset_x()) / f64::from(width)) as f32,
        (f64::from(event.offset_y()) / f64::from(height)) as f32,
    ];
    viewport_uv
        .iter()
        .all(|value| value.is_finite() && (0.0..=1.0).contains(value))
        .then_some(viewport_uv)
}

fn apply_camera_gesture_delta(actions: WorldActions, pan: bool, pointer_delta: [i32; 2]) {
    if pointer_delta == [0, 0] {
        return;
    }
    let delta = [pixel_delta(pointer_delta[0]), pixel_delta(pointer_delta[1])];
    if let Ok(mut bridge) = actions.viewport.get_value().0.lock() {
        let input = if pan {
            &mut bridge.input.pan
        } else {
            &mut bridge.input.orbit
        };
        input[0] += delta[0];
        input[1] += delta[1];
    }
}

fn request_asset_surface(
    actions: WorldActions,
    asset_drag: RwSignal<AssetDragState>,
    event: &web_sys::DragEvent,
) -> bool {
    if asset_drag.get_untracked().payload().is_none() {
        return false;
    }
    let Some(key) = actions.tabs.get_untracked().active else {
        return false;
    };
    let Some(document) = actions.document.get_untracked() else {
        return false;
    };
    if !key.exists(&document) {
        return false;
    }
    let Some(element) = event
        .current_target()
        .and_then(|target| target.dyn_into::<web_sys::Element>().ok())
    else {
        return false;
    };
    let (width, height) = (element.client_width(), element.client_height());
    if width <= 0 || height <= 0 {
        return false;
    }
    #[allow(
        clippy::cast_possible_truncation,
        reason = "Normalized finite canvas coordinates"
    )]
    let viewport_uv = [
        (f64::from(event.offset_x()) / f64::from(width)) as f32,
        (f64::from(event.offset_y()) / f64::from(height)) as f32,
    ];
    if !viewport_uv
        .iter()
        .all(|value| value.is_finite() && (0.0..=1.0).contains(value))
    {
        return false;
    }
    let mut state = asset_drag.get_untracked();
    let Ok(serial) = state.request_surface() else {
        return false;
    };
    asset_drag.set(state);
    let generation = actions.generation.get_untracked();
    let viewport = actions.viewport.get_value();
    let Ok(mut bridge) = viewport.0.lock() else {
        return false;
    };
    bridge.surface_pointer = Some(EditorSurfacePointer {
        generation,
        serial,
        viewport_uv,
    });
    true
}

fn schedule_asset_drop(
    actions: WorldActions,
    workspace: WorkspaceActions,
    asset_drag: RwSignal<AssetDragState>,
) {
    let drop_state = asset_drag.get_untracked();
    asset_drag.update(AssetDragState::clear);
    let Some(window) = web_sys::window() else {
        clear_asset_surface(actions);
        actions
            .error
            .set(Some("Fenêtre indisponible pendant le dépôt Asset.".into()));
        return;
    };
    // A final dragover can publish a new serial immediately before drop. Two bounded animation
    // frames let Bevy acknowledge that exact sample; dragend may clear the live signal meanwhile,
    // so this immutable state snapshot remains the sole correlation source.
    let first_frame = Closure::once_into_js(move || {
        let Some(window) = web_sys::window() else {
            clear_asset_surface(actions);
            actions
                .error
                .set(Some("Fenêtre indisponible pendant le dépôt Asset.".into()));
            return;
        };
        let second_frame = Closure::once_into_js(move || {
            finish_asset_drop(actions, workspace, drop_state);
        });
        if let Err(error) = window.request_animation_frame(second_frame.unchecked_ref()) {
            clear_asset_surface(actions);
            actions.error.set(Some(format!(
                "Échantillonnage final du terrain impossible : {error:?}"
            )));
        }
    });
    if let Err(error) = window.request_animation_frame(first_frame.unchecked_ref()) {
        clear_asset_surface(actions);
        actions.error.set(Some(format!(
            "Échantillonnage du terrain impossible : {error:?}"
        )));
    }
}

fn finish_asset_drop(
    actions: WorldActions,
    workspace: WorkspaceActions,
    drop_state: AssetDragState,
) {
    let result = (|| {
        if workspace.unavailable() {
            return Err(
                "Le projet est indisponible ou une autre opération est encore en cours.".to_owned(),
            );
        }
        let key = actions
            .tabs
            .get_untracked()
            .active
            .ok_or("Ouvrez une zone avant de déposer un Asset.")?;
        let document = actions
            .document
            .get_untracked()
            .ok_or("Aucun projet ouvert pour recevoir cet Asset.")?;
        if !key.exists(&document) {
            return Err("La zone de dépôt n’appartient plus au projet courant.".to_owned());
        }
        let generation = actions.generation.get_untracked();
        let hit = actions
            .viewport
            .get_value()
            .0
            .lock()
            .map_err(|_| "Viewport indisponible pendant le dépôt.".to_owned())?
            .surface_hit
            .map(|hit| AssetSurfaceHit {
                generation: hit.generation,
                serial: hit.serial,
                project_id: hit.project_id,
                area_id: hit.area_id,
                revision: hit.revision,
                world: hit.world,
            });
        drop_state
            .resolve_drop(
                AssetDropScope {
                    key,
                    revision: document.revision.get(),
                    generation,
                },
                hit,
            )
            .map_err(|error| error.to_string())
    })();
    clear_asset_surface(actions);
    match result {
        Ok(intent) => workspace.send(WorkspaceCommand::PlaceAsset {
            request: AreaPreviewRequest {
                key: intent.key,
                base_revision: intent.revision,
            },
            asset_id: intent.payload.asset_id,
            snapshot_hash: intent.payload.snapshot_hash,
            drop_x_m: intent.drop_xz_m[0],
            drop_z_m: intent.drop_xz_m[1],
            surface_generation: intent.surface_generation,
            surface_serial: intent.surface_serial,
        }),
        Err(error) => actions.error.set(Some(error)),
    }
}

fn clear_asset_surface(actions: WorldActions) {
    if let Ok(mut bridge) = actions.viewport.get_value().0.lock() {
        bridge.surface_pointer = None;
        bridge.surface_hit = None;
        bridge.surface_sample_serial = None;
    }
}

fn cancel_asset_drag(actions: WorldActions, asset_drag: RwSignal<AssetDragState>) {
    clear_asset_surface(actions);
    asset_drag.update(AssetDragState::clear);
}

fn selection_ledger(actions: WorldActions) -> serde_json::Value {
    let state = actions.selection.get();
    let pending_serial = state.pending_serial();
    selected_placement_details(actions).map_or_else(
        || {
            serde_json::json!({
                "status": "none",
                "pending_serial": pending_serial,
            })
        },
        |details| {
            serde_json::json!({
                "status": "selected",
                "pending_serial": pending_serial,
                "project_id": details.key.project_id,
                "module_id": details.key.module_id,
                "area_id": details.key.area_id,
                "placement_id": details.placement_id,
                "asset_id": details.asset_id,
                "stable_key": details.stable_key,
                "translation_m": [
                    details.transform.translation_m.x,
                    details.transform.translation_m.y,
                    details.transform.translation_m.z,
                ],
                "rotation": [
                    details.transform.rotation.x,
                    details.transform.rotation.y,
                    details.transform.rotation.z,
                    details.transform.rotation.w,
                ],
                "scale": [
                    details.transform.scale.x,
                    details.transform.scale.y,
                    details.transform.scale.z,
                ],
                "bevy_entity_exposed": false,
            })
        },
    )
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "Pointer deltas are explicitly clamped before f32 conversion"
)]
fn pixel_delta(value: i32) -> f32 {
    f32::from(i16::try_from(value.clamp(-1000, 1000)).unwrap_or_default())
}
#[allow(
    clippy::cast_possible_truncation,
    reason = "Finite wheel deltas are explicitly clamped before f32 conversion"
)]
fn wheel_delta(value: f64) -> f32 {
    if value.is_finite() {
        value.clamp(-1000.0, 1000.0) as f32
    } else {
        0.0
    }
}

#[component]
pub(crate) fn WorldDiagnostics(actions: WorldActions) -> impl IntoView {
    view! { <section class="world-diagnostics">
        <h1>"Diagnostics du monde"</h1>
        <p>"Même canvas et même renderer que Monde. Les scènes de preuve Gate 0/1 restent des routes séparées."</p>
        <p>{move || actions.status_text()}</p>
        <p>"Temps de boucle CPU observé, pas un timestamp GPU. La présentation GPU nécessite une capture réelle."</p>
        <p>"Le cube doré représente le départ saisi, sans personnage ni recalage automatique au sol."</p>
        <pre id="world-viewport-ledger">{move || serde_json::json!({
            "renderer_starts": actions.observed_starts.get(), "renderer_start_requests": u8::from(actions.started.get()), "desired_generation": actions.generation.get(),
            "busy": actions.busy.get(), "projection": actions.status.get(), "asset_projection": actions.placement_status.get(), "error": actions.error.get(),
            "selection": selection_ledger(actions),
            "gpu_presentation": "not_observed", "gpu_timestamps": "unavailable",
        }).to_string()}</pre>
    </section> }
}
