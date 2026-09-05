//! Minimal direct path authoring. A gesture stays local until one release command is dispatched.

use super::{WorldActions, brush::BrushActions};
use crate::{
    area_preview::AreaPreviewRequest, brush_state::PathTrace, workspace_api::WorkspaceCommand,
    workspace_ui::WorkspaceActions,
};
use forge_engine::editor_viewport::{EditorBrushHit, EditorBrushPointer, EditorPathDraft};
use forge_project::{
    AuthoredRiver, AuthoredRoad, AuthoredWall, PathDocument, PathId, RiverChannelProfile,
    RiverChannelProfileAlgorithm, RiverDocument, RiverSurface, RoadDocument, RoadProfile,
    RoadProfileAlgorithm, RoadSurface, WallDocument, WallProfile, WallProfileAlgorithm,
    WallSurface,
};
use leptos::prelude::*;
use wasm_bindgen::{JsCast, closure::Closure};

const PATH_POINTER_RADIUS_M: f32 = 2.0;
const MAX_PATH_COORDINATE_M: f32 = 255.999_98;
const DEFAULT_ROAD_WIDTH_M: f32 = 4.0;
const DEFAULT_ROAD_CROWN_HEIGHT_M: f32 = 0.08;
const DEFAULT_RIVER_SURFACE_WIDTH_M: f32 = 8.0;
const DEFAULT_RIVER_BED_DEPTH_M: f32 = 1.5;
const DEFAULT_RIVER_BANK_BLEND_WIDTH_M: f32 = 4.0;
const DEFAULT_WALL_HEIGHT_M: f32 = 1.5;
const DEFAULT_WALL_THICKNESS_M: f32 = 0.3;

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
enum PathIntentKind {
    Create,
    Redraw(PathId),
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct PathIntent {
    request: AreaPreviewRequest,
    generation: u64,
    kind: PathIntentKind,
}

#[derive(Clone, Debug, PartialEq)]
struct PathDraft {
    intent: PathIntent,
    pointer_id: i32,
    trace: PathTrace,
    last_serial: u64,
    sampled_serial: u64,
    finishing: bool,
}

#[derive(Clone, Copy)]
pub(super) struct PathActions {
    intent: RwSignal<Option<PathIntent>>,
    draft: RwSignal<Option<PathDraft>>,
    serial: RwSignal<u64>,
    error: RwSignal<Option<String>>,
}

impl PathActions {
    fn activate(
        self,
        world: WorldActions,
        workspace: WorkspaceActions,
        brush: BrushActions,
        kind: PathIntentKind,
    ) {
        if workspace.unavailable()
            || world.busy.get_untracked()
            || world.density_preview_busy.get_untracked()
            || brush.interaction_active()
        {
            return;
        }
        let Some(request) = active_request_untracked(world) else {
            self.error
                .set(Some("Ouvrez une zone avant de tracer un chemin.".into()));
            return;
        };
        let generation = world.generation.get_untracked();
        let status = world.status.get_untracked();
        if status.generation != generation
            || status.project_id != Some(request.key.project_id)
            || status.area_id != Some(request.key.area_id)
            || status.revision != request.base_revision
            || status.chunks != 64
            || status.pending_chunks != 0
            || status.error.is_some()
        {
            self.error.set(Some(
                "Attendez la projection canonique de la zone avant de tracer.".into(),
            ));
            return;
        }

        self.cancel(world, false);
        brush.select_navigation(world);
        self.error.set(None);
        self.intent.set(Some(PathIntent {
            request,
            generation,
            kind,
        }));
        focus_world_canvas();
    }

    pub(super) fn start(
        self,
        world: WorldActions,
        workspace: WorkspaceActions,
        brush: BrushActions,
        event: &web_sys::PointerEvent,
    ) -> bool {
        let Some(intent) = self.intent.get_untracked() else {
            return false;
        };
        if event.alt_key() || event.button() != 0 {
            return false;
        }
        if workspace.unavailable()
            || world.busy.get_untracked()
            || world.density_preview_busy.get_untracked()
            || brush.interaction_active()
            || self.draft.get_untracked().is_some()
        {
            return true;
        }
        if !Self::intent_is_current(world, workspace, intent) {
            self.error.set(Some(
                "La zone a changé : tracé annulé sans modification.".into(),
            ));
            self.cancel(world, false);
            return true;
        }
        if !self.move_pointer(world, event) {
            return true;
        }
        if let Ok(mut bridge) = world.viewport.get_value().0.lock() {
            bridge.brush_start_pointer = bridge.brush_pointer;
            bridge.brush_start_hit = None;
        }
        let Some(element) = event
            .current_target()
            .and_then(|target| target.dyn_into::<web_sys::Element>().ok())
        else {
            return true;
        };
        if element.set_pointer_capture(event.pointer_id()).is_err() {
            return true;
        }
        if let Some(element) = element.dyn_ref::<web_sys::HtmlElement>() {
            let _ = element.focus();
        }
        event.prevent_default();
        self.draft.set(Some(PathDraft {
            intent,
            pointer_id: event.pointer_id(),
            trace: PathTrace::default(),
            last_serial: self.serial.get_untracked(),
            sampled_serial: 0,
            finishing: false,
        }));
        true
    }

    pub(super) fn move_pointer(self, world: WorldActions, event: &web_sys::PointerEvent) -> bool {
        if self.intent.get_untracked().is_none() {
            return false;
        }
        if self
            .draft
            .get_untracked()
            .is_some_and(|draft| draft.finishing || draft.pointer_id != event.pointer_id())
        {
            return true;
        }
        let Some(element) = event
            .current_target()
            .and_then(|target| target.dyn_into::<web_sys::Element>().ok())
        else {
            return true;
        };
        let (width, height) = (element.client_width(), element.client_height());
        if width <= 0 || height <= 0 {
            return true;
        }
        #[allow(
            clippy::cast_possible_truncation,
            reason = "Normalized finite canvas coordinates"
        )]
        let uv = [
            (f64::from(event.offset_x()) / f64::from(width)) as f32,
            (f64::from(event.offset_y()) / f64::from(height)) as f32,
        ];
        if uv.iter().any(|value| !(0.0..=1.0).contains(value)) {
            return true;
        }
        let Some(serial) = self.serial.get_untracked().checked_add(1) else {
            self.error.set(Some("Compteur de tracé épuisé.".into()));
            self.cancel(world, false);
            return true;
        };
        self.serial.set(serial);
        if let Ok(mut bridge) = world.viewport.get_value().0.lock() {
            bridge.brush_pointer = Some(EditorBrushPointer {
                generation: world.generation.get_untracked(),
                serial,
                uv,
                radius_m: PATH_POINTER_RADIUS_M,
                strength: 1.0,
            });
        }
        self.draft.update(|draft| {
            if let Some(draft) = draft {
                draft.last_serial = serial;
            }
        });
        true
    }

    pub(super) fn finish(self, world: WorldActions, event: &web_sys::PointerEvent) -> bool {
        if self
            .draft
            .get_untracked()
            .is_none_or(|draft| draft.pointer_id != event.pointer_id())
        {
            return false;
        }
        self.move_pointer(world, event);
        self.draft.update(|draft| {
            if let Some(draft) = draft {
                draft.finishing = true;
            }
        });
        true
    }

    pub(super) fn cancel(self, world: WorldActions, unless_finishing: bool) {
        if unless_finishing
            && self
                .draft
                .get_untracked()
                .is_some_and(|draft| draft.finishing)
        {
            return;
        }
        self.intent.set(None);
        self.draft.set(None);
        clear_path_interaction(world);
    }

    pub(super) fn clear_hover(self, world: WorldActions) {
        if self.draft.get_untracked().is_none()
            && self.intent.get_untracked().is_some()
            && let Ok(mut bridge) = world.viewport.get_value().0.lock()
        {
            bridge.brush_pointer = None;
            bridge.brush_hit = None;
        }
    }

    fn intent_is_current(
        world: WorldActions,
        workspace: WorkspaceActions,
        intent: PathIntent,
    ) -> bool {
        world.generation.get_untracked() == intent.generation
            && world.tabs.get_untracked().active == Some(intent.request.key)
            && workspace.state.get_untracked().revision == intent.request.base_revision
    }

    fn record_hit(
        self,
        world: WorldActions,
        draft: &mut PathDraft,
        hit: EditorBrushHit,
    ) -> Option<bool> {
        let point_m = [
            hit.point[0].clamp(-256.0, MAX_PATH_COORDINATE_M),
            hit.point[1],
            hit.point[2].clamp(-256.0, MAX_PATH_COORDINATE_M),
        ];
        match draft.trace.append(point_m) {
            Ok(changed) => {
                draft.sampled_serial = hit.serial;
                Some(changed)
            }
            Err(error) => {
                self.error.set(Some(error));
                self.cancel(world, false);
                None
            }
        }
    }

    fn sample(self, world: WorldActions, workspace: WorkspaceActions) {
        let Some(mut draft) = self.draft.get_untracked() else {
            return;
        };
        if !Self::intent_is_current(world, workspace, draft.intent) {
            self.error.set(Some(
                "La zone a changé : tracé annulé sans modification.".into(),
            ));
            self.cancel(world, false);
            return;
        }
        let (hit, sampled, start_hit) = {
            let viewport = world.viewport.get_value();
            let Ok(bridge) = viewport.0.lock() else {
                return;
            };
            (
                bridge.brush_hit,
                bridge.brush_sample_serial,
                bridge.brush_start_hit,
            )
        };

        let mut changed = false;
        if draft.sampled_serial == 0
            && let Some(hit) = start_hit.filter(|hit| hit.generation == draft.intent.generation)
        {
            let Some(was_changed) = self.record_hit(world, &mut draft, hit) else {
                return;
            };
            changed |= was_changed;
        }
        if let Some(hit) = hit.filter(|hit| {
            hit.generation == draft.intent.generation && hit.serial > draft.sampled_serial
        }) {
            let Some(was_changed) = self.record_hit(world, &mut draft, hit) else {
                return;
            };
            changed |= was_changed;
        }
        if changed {
            publish_path_draft(world, &draft);
        }

        if draft.finishing && sampled == Some(draft.last_serial) {
            let trace_xz_m = match draft.trace.command_points() {
                Ok(points) => points,
                Err(error) => {
                    self.error.set(Some(error));
                    self.cancel(world, false);
                    return;
                }
            };
            let command = match draft.intent.kind {
                PathIntentKind::Create => WorkspaceCommand::CreatePath {
                    request: draft.intent.request,
                    name: String::new(),
                    trace_xz_m,
                },
                PathIntentKind::Redraw(path_id) => WorkspaceCommand::RedrawPath {
                    request: draft.intent.request,
                    path_id,
                    trace_xz_m,
                },
            };
            self.intent.set(None);
            self.draft.set(None);
            clear_path_interaction(world);
            workspace.send(command);
        } else {
            self.draft.set(Some(draft));
        }
    }
}

fn active_request_untracked(world: WorldActions) -> Option<AreaPreviewRequest> {
    let key = world.tabs.get_untracked().active?;
    let document = world.document.get_untracked()?;
    document.area(key.module_id, key.area_id)?;
    Some(AreaPreviewRequest {
        key,
        base_revision: document.revision.get(),
    })
}

fn active_paths(
    world: WorldActions,
) -> Option<(
    AreaPreviewRequest,
    PathDocument,
    RoadDocument,
    RiverDocument,
    WallDocument,
)> {
    let key = world.tabs.get().active?;
    let document = world.document.get()?;
    let area = document.area(key.module_id, key.area_id)?;
    let paths = area.paths.clone();
    let roads = area.roads.clone();
    let rivers = area.rivers.clone();
    let walls = area.walls.clone();
    Some((
        AreaPreviewRequest {
            key,
            base_revision: document.revision.get(),
        },
        paths,
        roads,
        rivers,
        walls,
    ))
}

fn parse_profile_number(value: &str, label: &str) -> Result<f32, String> {
    let parsed = value
        .trim()
        .parse::<f32>()
        .map_err(|_| format!("{label} doit être un nombre fini."))?;
    if !parsed.is_finite() {
        return Err(format!("{label} doit être un nombre fini."));
    }
    Ok(parsed)
}

#[component]
fn RoadProfileForm(
    path_id: PathId,
    current: Option<AuthoredRoad>,
    request: AreaPreviewRequest,
    paths: PathActions,
    brush: BrushActions,
    world: WorldActions,
    workspace: WorkspaceActions,
) -> impl IntoView {
    let width = RwSignal::new(
        current
            .map_or(DEFAULT_ROAD_WIDTH_M, |road| road.profile.width_m)
            .to_string(),
    );
    let crown = RwSignal::new(
        current
            .map_or(DEFAULT_ROAD_CROWN_HEIGHT_M, |road| {
                road.profile.crown_height_m
            })
            .to_string(),
    );
    let action_label = if current.is_some() {
        "Mettre à jour le profil"
    } else {
        "Créer la route"
    };

    view! {
        <form class="vegetation-add-form" on:submit=move |event| {
            event.prevent_default();
            let result = (|| {
                let profile = RoadProfile {
                    algorithm: RoadProfileAlgorithm::TerrainDrapedCrownV1,
                    width_m: parse_profile_number(&width.get_untracked(), "La largeur")?,
                    crown_height_m: parse_profile_number(&crown.get_untracked(), "Le bombé")?,
                };
                profile.validate().map_err(|error| error.to_string())?;
                let road = AuthoredRoad {
                    path_id,
                    profile,
                    surface: RoadSurface::CompactedEarthV1,
                };
                if current == Some(road) {
                    return Err("Le profil de route est déjà identique.".to_owned());
                }
                Ok(road)
            })();
            match result {
                Ok(road) => {
                    paths.error.set(None);
                    workspace.send(WorkspaceCommand::UpsertRoad { request, road });
                }
                Err(error) => paths.error.set(Some(error)),
            }
        }>
            <fieldset disabled=move || workspace.unavailable()
                || paths.intent.get().is_some()
                || world.busy.get()
                || world.density_preview_busy.get()
                || brush.interaction_active()>
                <legend class="project-sr-only">"Profil de route en terre"</legend>
                <label>"Largeur (m)"
                    <input type="number" min="1" max="16" step="0.1"
                        aria-label="Largeur de route en mètres"
                        prop:value=move || width.get()
                        on:input=move |event| width.set(event_target_value(&event))/>
                </label>
                <label>"Bombé (m)"
                    <input type="number" min="0" max="0.3" step="0.01"
                        aria-label="Bombé de route en mètres"
                        prop:value=move || crown.get()
                        on:input=move |event| crown.set(event_target_value(&event))/>
                </label>
                <button type="submit">{action_label}</button>
            </fieldset>
        </form>
    }
}

#[component]
fn RiverProfileForm(
    path_id: PathId,
    current: Option<AuthoredRiver>,
    request: AreaPreviewRequest,
    paths: PathActions,
    brush: BrushActions,
    world: WorldActions,
    workspace: WorkspaceActions,
) -> impl IntoView {
    let width = RwSignal::new(
        current
            .map_or(DEFAULT_RIVER_SURFACE_WIDTH_M, |river| {
                river.channel.surface_width_m
            })
            .to_string(),
    );
    let depth = RwSignal::new(
        current
            .map_or(DEFAULT_RIVER_BED_DEPTH_M, |river| river.channel.bed_depth_m)
            .to_string(),
    );
    let bank_blend = RwSignal::new(
        current
            .map_or(DEFAULT_RIVER_BANK_BLEND_WIDTH_M, |river| {
                river.channel.bank_blend_width_m
            })
            .to_string(),
    );
    let action_label = if current.is_some() {
        "Mettre à jour la rivière"
    } else {
        "Créer la rivière"
    };

    view! {
        <form class="vegetation-add-form" on:submit=move |event| {
            event.prevent_default();
            let result = (|| {
                let channel = RiverChannelProfile {
                    algorithm: RiverChannelProfileAlgorithm::TerrainCarvedChannelV1,
                    surface_width_m: parse_profile_number(
                        &width.get_untracked(),
                        "La largeur de surface",
                    )?,
                    bed_depth_m: parse_profile_number(
                        &depth.get_untracked(),
                        "La profondeur du lit",
                    )?,
                    bank_blend_width_m: parse_profile_number(
                        &bank_blend.get_untracked(),
                        "Le raccord de berge",
                    )?,
                };
                channel.validate().map_err(|error| error.to_string())?;
                let river = AuthoredRiver {
                    path_id,
                    channel,
                    surface: RiverSurface::ForgeWaterV1,
                };
                if current == Some(river) {
                    return Err("Le profil de rivière est déjà identique.".to_owned());
                }
                Ok(river)
            })();
            match result {
                Ok(river) => {
                    paths.error.set(None);
                    workspace.send(WorkspaceCommand::UpsertRiver { request, river });
                }
                Err(error) => paths.error.set(Some(error)),
            }
        }>
            <fieldset disabled=move || workspace.unavailable()
                || paths.intent.get().is_some()
                || world.busy.get()
                || world.density_preview_busy.get()
                || brush.interaction_active()>
                <legend class="project-sr-only">"Profil de rivière sculptée"</legend>
                <label>"Largeur d’eau (m)"
                    <input type="number" min="2" max="32" step="0.1"
                        aria-label="Largeur de surface de rivière en mètres"
                        prop:value=move || width.get()
                        on:input=move |event| width.set(event_target_value(&event))/>
                </label>
                <label>"Profondeur (m)"
                    <input type="number" min="0.15" max="8" step="0.05"
                        aria-label="Profondeur du lit de rivière en mètres"
                        prop:value=move || depth.get()
                        on:input=move |event| depth.set(event_target_value(&event))/>
                </label>
                <label>"Raccord de berge (m)"
                    <input type="number" min="1" max="16" step="0.1"
                        aria-label="Largeur du raccord de berge en mètres"
                        prop:value=move || bank_blend.get()
                        on:input=move |event| bank_blend.set(event_target_value(&event))/>
                </label>
                <button type="submit">{action_label}</button>
            </fieldset>
        </form>
    }
}

#[component]
fn WallProfileForm(
    path_id: PathId,
    current: Option<AuthoredWall>,
    request: AreaPreviewRequest,
    paths: PathActions,
    brush: BrushActions,
    world: WorldActions,
    workspace: WorkspaceActions,
) -> impl IntoView {
    let height = RwSignal::new(
        current
            .map_or(DEFAULT_WALL_HEIGHT_M, |wall| wall.profile.height_m)
            .to_string(),
    );
    let thickness = RwSignal::new(
        current
            .map_or(DEFAULT_WALL_THICKNESS_M, |wall| wall.profile.thickness_m)
            .to_string(),
    );
    let action_label = if current.is_some() {
        "Mettre à jour le mur"
    } else {
        "Créer le mur"
    };

    view! {
        <form class="vegetation-add-form" on:submit=move |event| {
            event.prevent_default();
            let result = (|| {
                let profile = WallProfile {
                    algorithm: WallProfileAlgorithm::TerrainDrapedSolidV1,
                    height_m: parse_profile_number(&height.get_untracked(), "La hauteur")?,
                    thickness_m: parse_profile_number(
                        &thickness.get_untracked(),
                        "L’épaisseur",
                    )?,
                };
                profile.validate().map_err(|error| error.to_string())?;
                let wall = AuthoredWall {
                    path_id,
                    profile,
                    surface: WallSurface::ForgeStoneV1,
                };
                if current == Some(wall) {
                    return Err("Le profil de mur est déjà identique.".to_owned());
                }
                Ok(wall)
            })();
            match result {
                Ok(wall) => {
                    paths.error.set(None);
                    workspace.send(WorkspaceCommand::UpsertWall { request, wall });
                }
                Err(error) => paths.error.set(Some(error)),
            }
        }>
            <h3>"Mur procédural"</h3>
            <fieldset disabled=move || workspace.unavailable()
                || paths.intent.get().is_some()
                || world.busy.get()
                || world.density_preview_busy.get()
                || brush.interaction_active()>
                <legend class="project-sr-only">"Profil de mur procédural en pierre"</legend>
                <label>"Hauteur (m)"
                    <input type="number" min="0.25" max="8" step="0.05"
                        aria-label="Hauteur du mur en mètres"
                        prop:value=move || height.get()
                        on:input=move |event| height.set(event_target_value(&event))/>
                </label>
                <label>"Épaisseur (m)"
                    <input type="number" min="0.1" max="2" step="0.05"
                        aria-label="Épaisseur du mur en mètres"
                        prop:value=move || thickness.get()
                        on:input=move |event| thickness.set(event_target_value(&event))/>
                </label>
                <button type="submit">{action_label}</button>
            </fieldset>
        </form>
    }
}

fn publish_path_draft(world: WorldActions, draft: &PathDraft) {
    if let Ok(mut bridge) = world.viewport.get_value().0.lock() {
        bridge.path_draft = Some(EditorPathDraft {
            generation: draft.intent.generation,
            points_m: draft.trace.points_m.clone(),
        });
    }
}

fn clear_path_interaction(world: WorldActions) {
    if let Ok(mut bridge) = world.viewport.get_value().0.lock() {
        bridge.path_draft = None;
        bridge.brush_pointer = None;
        bridge.brush_hit = None;
        bridge.brush_sample_serial = None;
        bridge.brush_start_pointer = None;
        bridge.brush_start_hit = None;
    }
}

fn focus_world_canvas() {
    if let Some(canvas) = web_sys::window()
        .and_then(|window| window.document())
        .and_then(|document| document.get_element_by_id("forge-world-canvas"))
        .and_then(|element| element.dyn_into::<web_sys::HtmlElement>().ok())
    {
        let _ = canvas.focus();
    }
}

pub(super) fn create_path_actions(
    world: WorldActions,
    workspace: WorkspaceActions,
    brush: BrushActions,
) -> PathActions {
    let actions = PathActions {
        intent: RwSignal::new(None),
        draft: RwSignal::new(None),
        serial: RwSignal::new(0),
        error: RwSignal::new(None),
    };
    Effect::new(move |_| {
        let generation = world.generation.get();
        let active = world.tabs.get().active;
        let revision = workspace.state.get().revision;
        let brush_mode = brush.selected_mode();
        let invalid = actions.intent.get_untracked().is_some_and(|intent| {
            intent.generation != generation
                || Some(intent.request.key) != active
                || intent.request.base_revision != revision
                || brush_mode != "navigate"
        });
        if invalid {
            actions.cancel(world, false);
        }
    });
    let poll = Closure::<dyn FnMut()>::new(move || actions.sample(world, workspace));
    if let Some(window) = web_sys::window() {
        match window.set_interval_with_callback_and_timeout_and_arguments_0(
            poll.as_ref().unchecked_ref(),
            32,
        ) {
            Ok(_) => poll.forget(),
            Err(error) => actions
                .error
                .set(Some(format!("Pointeur de chemin indisponible : {error:?}"))),
        }
    }
    actions
}

#[component]
#[allow(
    clippy::too_many_lines,
    reason = "one direct-authoring panel keeps the mutually exclusive path profiles visibly together"
)]
pub(super) fn PathPanel(
    paths: PathActions,
    brush: BrushActions,
    world: WorldActions,
    workspace: WorkspaceActions,
) -> impl IntoView {
    view! {
        <section class="vegetation-panel" aria-labelledby="path-panel-heading">
            <h2 id="path-panel-heading">"Chemins"</h2>
            <button type="button"
                disabled=move || paths.intent.get().is_none()
                    && (workspace.unavailable()
                        || active_paths(world).is_none()
                        || world.busy.get()
                        || world.density_preview_busy.get()
                        || brush.interaction_active())
                on:click=move |_| {
                    if paths.intent.get_untracked().is_some() {
                        paths.cancel(world, false);
                    } else {
                        paths.activate(world, workspace, brush, PathIntentKind::Create);
                    }
                }>
                {move || if paths.intent.get().is_some() { "Annuler le tracé" } else { "Tracer un chemin" }}
            </button>
            <p class="hint">{move || match paths.intent.get().map(|intent| intent.kind) {
                Some(PathIntentKind::Create) => "Glissez sur le terrain puis relâchez pour créer un chemin.",
                Some(PathIntentKind::Redraw(_)) => "Glissez sur le terrain puis relâchez pour remplacer le tracé choisi.",
                None => "Un geste crée une seule modification annulable. Alt + glisser conserve la navigation.",
            }}</p>
            <p class="hint">
                "Les contacts entre objets portés par des chemins distincts sont autorisés : géométrie superposée déterministe, sans ouverture ni jonction automatique."
            </p>
            <ul class="vegetation-list">
                {move || active_paths(world).map(|(request, document, roads, rivers, walls)| {
                    if document.paths.is_empty() {
                        return view! { <li class="vegetation-empty">"Aucun chemin"</li> }.into_any();
                    }
                    document.paths.into_iter().map(|path| {
                        let path_id = path.id;
                        let closed = path.closed;
                        let road = roads.roads.iter().find(|road| road.path_id == path_id).copied();
                        let river = rivers.rivers.iter().find(|river| river.path_id == path_id).copied();
                        let wall = walls.walls.iter().find(|wall| wall.path_id == path_id).copied();
                        let description = match (road, river, wall) {
                            (Some(road), None, None) => format!(
                                "{} · {} points · route terre {:.1} m",
                                path.name,
                                path.control_points.len(),
                                road.profile.width_m,
                            ),
                            (None, Some(river), None) => format!(
                                "{} · {} points · rivière {:.1} m × {:.1} m",
                                path.name,
                                path.control_points.len(),
                                river.channel.surface_width_m,
                                river.channel.bed_depth_m,
                            ),
                            (None, None, Some(wall)) => format!(
                                "{} · {} points · mur pierre {:.1} m × {:.1} m",
                                path.name,
                                path.control_points.len(),
                                wall.profile.height_m,
                                wall.profile.thickness_m,
                            ),
                            _ => format!("{} · {} points", path.name, path.control_points.len()),
                        };
                        view! {
                            <li class="vegetation-row">
                                <span class="vegetation-exclusion-description">{description}</span>
                                <button type="button"
                                    disabled=move || workspace.unavailable()
                                        || paths.intent.get().is_some()
                                        || world.busy.get()
                                        || world.density_preview_busy.get()
                                        || brush.interaction_active()
                                    on:click=move |_| paths.activate(
                                        world,
                                        workspace,
                                        brush,
                                        PathIntentKind::Redraw(path_id),
                                )>"Redessiner"</button>
                                {if closed {
                                    view! { <p class="hint">"Route, rivière et mur indisponibles : ce chemin est fermé."</p> }.into_any()
                                } else {
                                    view! {
                                        <div>
                                            {if river.is_some() {
                                                view! { <p class="hint">"Route indisponible : ce chemin porte une rivière."</p> }.into_any()
                                            } else if wall.is_some() {
                                                view! { <p class="hint">"Route indisponible : ce chemin porte un mur."</p> }.into_any()
                                            } else {
                                                view! { <RoadProfileForm path_id=path_id current=road request=request
                                                    paths=paths brush=brush world=world workspace=workspace/> }.into_any()
                                            }}
                                            {if road.is_some() {
                                                view! { <p class="hint">"Rivière indisponible : ce chemin porte une route."</p> }.into_any()
                                            } else if wall.is_some() {
                                                view! { <p class="hint">"Rivière indisponible : ce chemin porte un mur."</p> }.into_any()
                                            } else {
                                                view! { <RiverProfileForm path_id=path_id current=river request=request
                                                    paths=paths brush=brush world=world workspace=workspace/> }.into_any()
                                            }}
                                            {if road.is_some() {
                                                view! { <p class="hint">"Mur indisponible : ce chemin porte une route."</p> }.into_any()
                                            } else if river.is_some() {
                                                view! { <p class="hint">"Mur indisponible : ce chemin porte une rivière."</p> }.into_any()
                                            } else {
                                                view! { <WallProfileForm path_id=path_id current=wall request=request
                                                    paths=paths brush=brush world=world workspace=workspace/> }.into_any()
                                            }}
                                        </div>
                                    }.into_any()
                                }}
                            </li>
                        }
                    }).collect_view().into_any()
                })}
            </ul>
            <p class="brush-position">{move || paths.draft.get().map_or_else(String::new, |draft| {
                format!("Tracé · {} points · non appliqué", draft.trace.points_m.len())
            })}</p>
            <p class="brush-error" role="alert">{move || paths.error.get().or(workspace.error.get()).unwrap_or_default()}</p>
        </section>
    }
}
