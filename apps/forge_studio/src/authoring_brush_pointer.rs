//! Canvas event bridge: pick a terrain point, then prepare the ordinary brush command.
use bevy::prelude::*;
use leptos::prelude::*;
use std::cell::{Cell, RefCell};
use wasm_bindgen::JsCast;

struct Pick {
    radius: f32,
    continuous: bool,
    points: Vec<[f32; 2]>,
    callback: Callback<Result<Vec<[f32; 2]>, String>>,
}

thread_local! {
    static POSITION: Cell<Option<Vec2>> = const { Cell::new(None) };
    static CLICKED: Cell<bool> = const { Cell::new(false) };
    static PICK: RefCell<Option<Pick>> = const { RefCell::new(None) };
    static DRAWING: Cell<bool> = const { Cell::new(false) };
    static RELEASED: Cell<bool> = const { Cell::new(false) };
    static EVENTS: RefCell<Vec<Vec2>> = const { RefCell::new(Vec::new()) };
    static OVERFLOW: Cell<bool> = const { Cell::new(false) };
    static PREVIEW_READY: Cell<bool> = const { Cell::new(false) };
    static COMMAND_BUSY: Cell<bool> = const { Cell::new(false) };
    static MODE: Cell<Option<RwSignal<String>>> = const { Cell::new(None) };
}

#[component]
pub(super) fn ToolMode() -> impl IntoView {
    let mode = RwSignal::new(String::new());
    MODE.with(|slot| slot.set(Some(mode)));
    on_cleanup(|| MODE.with(|slot| slot.set(None)));
    view! { <output class="authoring-tool-mode" hidden=move || mode.get().is_empty() aria-live="polite">{move || mode.get()}</output> }
}

fn report_mode(message: String) {
    MODE.with(|slot| {
        if let Some(mode) = slot.get() {
            mode.set(message);
        }
    });
}

pub(super) fn arm(
    radius: f32,
    continuous: bool,
    callback: Callback<Result<Vec<[f32; 2]>, String>>,
) {
    if PREVIEW_READY.with(Cell::get) {
        report_mode("Aperçu prêt · Appliquer ou Abandonner avant un autre tracé".into());
        return;
    }
    if COMMAND_BUSY.with(Cell::get) {
        report_mode("Calcul en cours · attendez le résultat avant de retracer".into());
        return;
    }
    cancel();
    PICK.with(|slot| {
        slot.replace(Some(Pick {
            radius,
            continuous,
            points: Vec::new(),
            callback,
        }))
    });
    let gesture = if continuous { "Tracer" } else { "Placer" };
    report_mode(format!(
        "{gesture} · rayon {radius:.0} m · clic gauche · Échap pour annuler"
    ));
}

pub(super) fn cancel() {
    crate::authoring_path_guides::cancel_drag();
    report_mode(String::new());
    crate::authoring_species::selection::cancel();
    crate::authoring_building_projection::selection::cancel();
    crate::authoring_imported_plants::selection::cancel();
    crate::authoring_assets::placements::gizmo::cancel();
    crate::authoring_assets::placements::selection::cancel();
    PICK.with(|slot| slot.replace(None));
    POSITION.with(|slot| slot.set(None));
    CLICKED.with(|slot| slot.set(false));
    DRAWING.with(|slot| slot.set(false));
    RELEASED.with(|slot| slot.set(false));
    EVENTS.with(|slot| slot.borrow_mut().clear());
    OVERFLOW.with(|slot| slot.set(false));
    PREVIEW_READY.with(|slot| slot.set(false));
}

pub(super) fn set_preview_ready(ready: bool) {
    PREVIEW_READY.with(|slot| slot.set(ready));
    if ready && PICK.with(|slot| slot.borrow().is_some()) {
        report_mode("Aperçu prêt · Appliquer ou Abandonner avant un autre tracé".into());
    } else if !ready && !COMMAND_BUSY.with(Cell::get) {
        PICK.with(|slot| {
            if let Some(pick) = slot.borrow().as_ref() {
                report_mode(if pick.continuous {
                    "Brosse active · tracer avec le clic gauche".into()
                } else {
                    "Brosse active · placer avec le clic gauche".into()
                });
            }
        });
    }
}

pub(super) fn set_command_busy(busy: bool) {
    COMMAND_BUSY.with(|slot| slot.set(busy));
    if busy && PICK.with(|slot| slot.borrow().is_some()) {
        report_mode("Calcul en cours · attendez le résultat avant de retracer".into());
    } else if !busy {
        set_preview_ready(PREVIEW_READY.with(Cell::get));
    }
}

pub(super) fn ready_for_tool() -> bool {
    !PREVIEW_READY.with(Cell::get) && !COMMAND_BUSY.with(Cell::get)
}

/// Leaving the canvas or navigating cancels a held stroke, not the chosen tool.
/// The next left gesture starts afresh; a point-by-point path retains its draft.
pub(super) fn suspend() {
    crate::authoring_path_guides::cancel_drag();
    crate::authoring_assets::placements::gizmo::cancel();
    PICK.with(|slot| {
        if let Some(pick) = slot.borrow_mut().as_mut() {
            pick.points.clear();
        }
    });
    POSITION.with(|slot| slot.set(None));
    CLICKED.with(|slot| slot.set(false));
    DRAWING.with(|slot| slot.set(false));
    RELEASED.with(|slot| slot.set(false));
    EVENTS.with(|slot| slot.borrow_mut().clear());
    OVERFLOW.with(|slot| slot.set(false));
}

/// User changes to brush settings invalidate both a queued gesture and its proposal.
/// Programmatic signal updates do not emit DOM input/change events.
pub(super) fn settings_changed() {
    cancel();
    crate::authoring_shortcuts::cancel_preview();
}

/// Queue one already-normalized DOM point through the ordinary terrain picker.
pub(super) fn pick_at(position: Vec2, callback: Callback<Result<Vec<[f32; 2]>, String>>) {
    if PREVIEW_READY.with(Cell::get) || COMMAND_BUSY.with(Cell::get) {
        callback.run(Err(
            "Terminer l’aperçu ou la commande en cours avant de placer un objet".into(),
        ));
        return;
    }
    if !position.is_finite() || position.min_element() < 0.0 || position.max_element() > 1.0 {
        callback.run(Err("Dépôt hors de la vue du terrain".into()));
        return;
    }
    // The shared terrain cursor accepts radii in 2..=128 m, even for a single pick.
    arm(2.0, false, callback);
    POSITION.with(|slot| slot.set(Some(position)));
    EVENTS.with(|slot| slot.borrow_mut().push(position));
    CLICKED.with(|slot| slot.set(true));
}

pub(super) fn press(event: &web_sys::MouseEvent) {
    if event.button() != 0 {
        return;
    }
    if PREVIEW_READY.with(Cell::get) || COMMAND_BUSY.with(Cell::get) {
        report_mode(if COMMAND_BUSY.with(Cell::get) {
            "Calcul en cours · attendez le résultat avant de retracer".into()
        } else {
            "Aperçu prêt · Appliquer ou Abandonner avant un autre tracé".into()
        });
        return;
    }
    if PICK.with(|slot| slot.borrow().as_ref().is_some_and(|pick| pick.continuous)) {
        DRAWING.with(|slot| slot.set(true));
        event.prevent_default();
    }
    observe(event, false);
}

pub(super) fn release(event: &web_sys::MouseEvent) {
    if event.button() != 0 || PREVIEW_READY.with(Cell::get) || COMMAND_BUSY.with(Cell::get) {
        return;
    }
    observe(event, false);
    if DRAWING.with(|slot| slot.replace(false)) {
        RELEASED.with(|slot| slot.set(true));
    }
}

#[allow(
    clippy::cast_precision_loss,
    reason = "Browser pixel coordinates fit f32 precision"
)]
pub(super) fn observe(event: &web_sys::MouseEvent, click: bool) {
    let Some(target) = event
        .current_target()
        .and_then(|target| target.dyn_into::<web_sys::HtmlElement>().ok())
    else {
        return;
    };
    let width = target.client_width();
    let height = target.client_height();
    if width <= 0 || height <= 0 {
        return;
    }
    let position = Vec2::new(
        event.offset_x() as f32 / width as f32,
        event.offset_y() as f32 / height as f32,
    );
    POSITION.with(|slot| slot.set(Some(position)));
    if PREVIEW_READY.with(Cell::get) || COMMAND_BUSY.with(Cell::get) {
        return;
    }
    let collect = DRAWING.with(Cell::get)
        || (click && PICK.with(|slot| slot.borrow().as_ref().is_some_and(|pick| !pick.continuous)));
    if collect {
        EVENTS.with(|slot| {
            let mut points = slot.borrow_mut();
            if points.last() == Some(&position) {
                return;
            }
            if points.len() == forge_world::terrain_brush_stroke::MAX_STROKE_POINTS {
                OVERFLOW.with(|slot| slot.set(true));
            } else {
                points.push(position);
            }
        });
    }
    if click {
        if PICK.with(|slot| slot.borrow().is_none()) {
            crate::authoring_assets::placements::selection::click(position);
            crate::authoring_building_projection::selection::click(position);
            crate::authoring_imported_plants::selection::click(position);
            crate::authoring_species::selection::click(position);
        }
        CLICKED.with(|slot| slot.set(true));
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameter")]
pub(super) fn input(mut cursor: ResMut<forge_engine::TerrainBrushCursor>) {
    let radius = PICK.with(|slot| slot.borrow().as_ref().map(|pick| pick.radius));
    cursor.normalized_position = POSITION.with(Cell::get);
    cursor.radius_m = radius.unwrap_or(0.0);
    cursor.pending_positions = EVENTS.with(|slot| std::mem::take(&mut *slot.borrow_mut()));
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameter")]
pub(super) fn picked(cursor: Res<forge_engine::TerrainBrushCursor>) {
    super::authoring_compass::publish_hit(cursor.hit);
    let clicked = CLICKED.with(|slot| slot.replace(false));
    let released = RELEASED.with(|slot| slot.replace(false));
    let drawing = DRAWING.with(Cell::get);
    // Release the RefCell borrow before invoking Leptos (which can cancel this tool).
    let result = PICK.with(|slot| {
        let mut slot = slot.borrow_mut();
        let pick = slot.as_mut()?;
        if !(if pick.continuous {
            drawing || released
        } else {
            clicked
        }) {
            return None;
        }
        if OVERFLOW.with(Cell::get) {
            let pick = slot.take()?;
            return Some((
                pick.callback,
                Err("Tracé trop long : recommencer avec un geste plus court".into()),
            ));
        }
        if cursor.path_hits.iter().any(Option::is_none) {
            let pick = slot.take()?;
            return Some((
                pick.callback,
                Err("Tracé annulé : pointeur hors terrain".into()),
            ));
        }
        for point in cursor.path_hits.iter().flatten() {
            let point = [point.x, point.z];
            if pick
                .points
                .last()
                .is_none_or(|last| (point[0] - last[0]).hypot(point[1] - last[1]) > 0.01)
            {
                if pick.points.len() == forge_world::terrain_brush_stroke::MAX_STROKE_POINTS {
                    let pick = slot.take()?;
                    return Some((
                        pick.callback,
                        Err("Tracé trop long : recommencer avec un geste plus court".into()),
                    ));
                }
                pick.points.push(point);
            }
        }
        if released || !pick.continuous {
            let callback = pick.callback;
            let points = std::mem::take(&mut pick.points);
            let result = if points.is_empty() {
                Err("Aucun point de terrain dans le geste".into())
            } else {
                Ok(points)
            };
            Some((callback, result))
        } else {
            None
        }
    });
    if let Some((callback, result)) = result {
        // Keep the chosen brush after a stroke; only its transient gesture ends.
        suspend();
        callback.run(result);
    }
}
