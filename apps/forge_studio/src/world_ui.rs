//! One persistent project viewport, fed exclusively by native source projections.
use crate::{
    StudioCommand, StudioResponse,
    area_preview::{AreaPreviewRequest, read_area_preview},
    area_tabs::{AreaKey, AreaTabs},
    workspace_ui::{WorkspaceActions, invoke_studio},
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
    EditorPathRequest, EditorTerrainRequest, EditorVegetationRequest, EditorViewport,
    EditorViewportPlugin, EditorViewportStatus,
};
use forge_project::WorkspaceDocument;
use leptos::prelude::Update;
use leptos::prelude::*;
use wasm_bindgen::{JsCast, closure::Closure};

#[path = "brush_ui.rs"]
mod brush;
use brush::{BrushPanel, VegetationPanel, create_brush_actions};
#[path = "path_ui.rs"]
mod path;
use path::{PathPanel, create_path_actions};

#[derive(Clone, Copy)]
pub(crate) struct WorldActions {
    tabs: RwSignal<AreaTabs>,
    document: Memo<Option<WorkspaceDocument>>,
    viewport: StoredValue<EditorViewport>,
    status: RwSignal<EditorViewportStatus>,
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
        }
    }

    pub(crate) fn open(self, key: AreaKey) {
        if let Some(document) = self.document.get_untracked() {
            self.tabs.update(|tabs| tabs.open(key, &document));
        }
    }

    fn request_canonical_projection(self) {
        self.canonical_refresh.update(|serial| *serial += 1);
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

    fn publish_density_preview(
        self,
        generation: u64,
        candidate_revision: u64,
        preview: crate::area_preview::AreaPreview,
    ) -> Result<(), String> {
        let request = preview.request;
        if self.generation.get_untracked() != generation
            || self.tabs.get_untracked().active != Some(request.key)
            || self
                .document
                .get_untracked()
                .is_none_or(|document| document.revision.get() != request.base_revision)
        {
            return Err("Cible d’aperçu périmée : projection ignorée.".into());
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
        if let Some(error) = self.error.get().or(self.status.get().error) {
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
        if status.generation != self.generation.get() || status.chunks < 64 {
            return format!("Projection en cours · {}/64 chunks", status.chunks);
        }
        format!(
            "Monde du projet · r{} · terrain {} recalculés / {} conservés · végétation {} instances, {} recalculés / {} conservés · chemins {} / {} points · boucle {:.1} ms",
            status.revision,
            status.rebuilt_chunks,
            status.retained_chunks,
            status.vegetation_instances,
            status.vegetation_rebuilt_chunks,
            status.vegetation_retained_chunks,
            status.path_count,
            status.path_samples,
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
        status: RwSignal::new(EditorViewportStatus::default()),
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
    });
}

#[derive(Clone, Copy, Eq, PartialEq)]
struct AreaPreviewTarget {
    request: AreaPreviewRequest,
    terrain_hash: forge_core::ContentHash,
    vegetation_hash: forge_core::ContentHash,
    vegetation_density_hash: forge_core::ContentHash,
    path_hash: forge_core::ContentHash,
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
        })
    })
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
        let base_source_hash = actions
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
            });
        wasm_bindgen_futures::spawn_local(async move {
            let result = fetch_preview(
                target.request,
                target.terrain_hash,
                target.vegetation_hash,
                target.vegetation_density_hash,
                target.path_hash,
                base_source_hash,
            )
            .await;
            if actions.generation.get_untracked() == generation
                && actions.canonical_refresh.get_untracked() == refresh
            {
                match result {
                    Ok(preview) => {
                        if let Ok(mut bridge) = actions.viewport.get_value().0.lock() {
                            bridge.pending = Some(EditorTerrainRequest {
                                generation,
                                project_id: target.request.key.project_id,
                                area_id: target.request.key.area_id,
                                revision: target.request.base_revision,
                                terrain: preview.terrain,
                                vegetation: Some(EditorVegetationRequest {
                                    generated: preview.vegetation,
                                    density_source_hash: preview.vegetation_density_source_hash,
                                }),
                                paths: Some(EditorPathRequest {
                                    generated: preview.paths,
                                }),
                                spawn: [preview.spawn.x, preview.spawn.y, preview.spawn.z],
                                base_source_hash: preview.base_source_hash,
                            });
                        }
                        start_once(actions);
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

async fn fetch_preview(
    request: AreaPreviewRequest,
    terrain_hash: forge_core::ContentHash,
    vegetation_hash: forge_core::ContentHash,
    vegetation_density_hash: forge_core::ContentHash,
    path_hash: forge_core::ContentHash,
    base_source_hash: Option<forge_core::ContentHash>,
) -> Result<crate::area_preview::AreaPreview, String> {
    match invoke_studio(StudioCommand::AreaPreview {
        request,
        base_source_hash,
    })
    .await?
    {
        StudioResponse::AreaPreview { preview_json } => {
            let preview = read_area_preview(&preview_json)?;
            if preview.request != request
                || preview.terrain.source_document_hash != terrain_hash
                || preview.vegetation.terrain_document_hash != terrain_hash
                || preview.vegetation.vegetation_document_hash != vegetation_hash
                || preview.vegetation_density_source_hash != vegetation_density_hash
                || preview.paths.terrain_document_hash != terrain_hash
                || preview.paths.path_document_hash != path_hash
                || preview.vegetation_changed_chunks + preview.vegetation_retained_chunks != 64
                || (preview.base_source_hash.is_some()
                    && preview.base_source_hash != base_source_hash)
            {
                return Err("Aperçu périmé ou incohérent : aucune projection acceptée.".into());
            }
            Ok(preview)
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

#[derive(Clone, Copy)]
struct PointerDrag {
    id: i32,
    x: i32,
    y: i32,
    pan: bool,
}

#[component]
pub(crate) fn WorldWorkspace(actions: WorldActions, workspace: WorkspaceActions) -> impl IntoView {
    let drag = RwSignal::new(None::<PointerDrag>);
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
                <div class="world-toolbar">
                    <button type="button" on:click=move |_| { if let Ok(mut bridge) = actions.viewport.get_value().0.lock() { bridge.input.reset = true; } }>"Vue d’ensemble"</button>
                    <button type="button" disabled=move || actions.tabs.get().active.is_none() on:click=move |_| {
                        let Some(key) = actions.tabs.get_untracked().active else { return };
                        if let Some(spawn) = actions.document.get_untracked().and_then(|doc| doc.area(key.module_id, key.area_id).map(|area| area.spawn))
                            && let Ok(mut bridge) = actions.viewport.get_value().0.lock() { bridge.input.focus = Some([spawn.x, spawn.y, spawn.z]); }
                    }>"Voir le départ"</button>
                    <span>"Glisser : orbite · Maj + glisser : déplacement · molette : zoom"</span>
                </div>
                <div class="world-canvas-host">
                    <canvas id="forge-world-canvas" aria-label="Terrain 3D de la zone active" tabindex="0"
                        on:keydown=move |event: web_sys::KeyboardEvent| { if event.key() == "Escape" {
                            paths.cancel(actions, false); paths.clear_hover(actions);
                            brush.cancel(actions, false); brush.clear_hover(actions);
                        } }
                        on:pointerleave=move |_| { paths.clear_hover(actions); brush.clear_hover(actions); }
                        on:pointerdown=move |event: web_sys::PointerEvent| {
                            if paths.start(actions, workspace, brush, &event) { return; }
                            if brush.start(actions, workspace, &event) { return; }
                            if event.button() != 0 { return; }
                            if let Some(element) = event.current_target().and_then(|target| target.dyn_into::<web_sys::Element>().ok())
                                && element.set_pointer_capture(event.pointer_id()).is_ok() {
                                event.prevent_default();
                                drag.set(Some(PointerDrag { id: event.pointer_id(), x: event.client_x(), y: event.client_y(), pan: event.shift_key() }));
                            }
                        }
                        on:pointermove=move |event: web_sys::PointerEvent| {
                            if !paths.move_pointer(actions, &event) { brush.move_pointer(actions, &event); }
                            let Some(previous) = drag.get_untracked().filter(|previous| previous.id == event.pointer_id()) else { return };
                            let delta = [pixel_delta(event.client_x() - previous.x), pixel_delta(event.client_y() - previous.y)];
                            if let Ok(mut bridge) = actions.viewport.get_value().0.lock() {
                                let input = if previous.pan { &mut bridge.input.pan } else { &mut bridge.input.orbit };
                                input[0] += delta[0]; input[1] += delta[1];
                            }
                            drag.set(Some(PointerDrag { x: event.client_x(), y: event.client_y(), ..previous }));
                        }
                        on:pointerup=move |event: web_sys::PointerEvent| {
                            if !paths.finish(actions, &event) { brush.finish(actions, &event); }
                            drag.set(None);
                        }
                        on:pointercancel=move |_| { paths.cancel(actions, false); brush.cancel(actions, false); drag.set(None); }
                        on:lostpointercapture=move |_| { paths.cancel(actions, true); brush.cancel(actions, true); drag.set(None); }
                        on:wheel=move |event: web_sys::WheelEvent| {
                            event.prevent_default();
                            if let Ok(mut bridge) = actions.viewport.get_value().0.lock() { bridge.input.zoom += wheel_delta(event.delta_y()); }
                        }/>
                    <Show when=move || actions.tabs.get().active.is_none()>
                        <div class="world-empty"><h2>"Un monde à ouvrir"</h2><p>"Choisissez une zone à gauche, ou créez-la dans Projets."</p></div>
                    </Show>
                </div>
                <footer class="world-status" role="status">{move || actions.status_text()}</footer>
            </div>
        </section>
    }
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
            "busy": actions.busy.get(), "projection": actions.status.get(), "error": actions.error.get(),
            "gpu_presentation": "not_observed", "gpu_timestamps": "unavailable",
        }).to_string()}</pre>
    </section> }
}
