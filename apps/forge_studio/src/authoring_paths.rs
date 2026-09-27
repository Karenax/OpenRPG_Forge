//! One path inspector; all durable edits use the existing preview/apply bus.
use forge_core::{CommandId, EntityId};
use forge_project::{
    authoring::{AuthoringRequest, AuthoringState},
    zone_authoring::{
        ZoneAuthoringHead,
        paths::{AuthoredPath, PathEdit, PathPoint, PathVerticalProfileV1},
    },
};
use leptos::prelude::*;

type SendCommand = Callback<Result<AuthoringRequest, String>>;
type DraftPoints = RwSignal<Vec<[String; 2]>>;

thread_local! {
    static DRAWING_KEY: std::cell::Cell<Option<Callback<String, bool>>> = const { std::cell::Cell::new(None) };
}

pub(super) fn drawing_key(key: &str) -> bool {
    DRAWING_KEY.with(|handler| {
        handler
            .get()
            .is_some_and(|handler| handler.run(key.to_owned()))
    })
}

#[derive(Clone, Copy)]
struct ProfileDraftSignals {
    use_profile: RwSignal<bool>,
    elevations: RwSignal<Vec<String>>,
    crossfall: RwSignal<String>,
    cut_fill: RwSignal<String>,
}

#[derive(Clone, Copy)]
struct DrawingSession {
    state: RwSignal<Option<AuthoringState>>,
    active: RwSignal<bool>,
    status: RwSignal<String>,
}

fn invalidate_changed_draft(
    name: RwSignal<String>,
    width: RwSignal<String>,
    shoulder: RwSignal<String>,
    points: DraftPoints,
    profile: ProfileDraftSignals,
    drawing: DrawingSession,
) {
    Effect::new(move |previous: Option<(_, Vec<[String; 2]>)>| {
        let fields = (
            name.get(),
            width.get(),
            shoulder.get(),
            profile.use_profile.get(),
            profile.elevations.get(),
            profile.crossfall.get(),
            profile.cut_fill.get(),
        );
        let current = (fields, points.get());
        if let Some(old) = previous.as_ref().filter(|old| *old != &current) {
            untrack(|| {
                super::authoring_shortcuts::cancel_preview();
                if drawing.active.get_untracked() && old.0 == current.0 {
                    if let Err(error) = append_points(drawing.state, points, drawing.status, false)
                    {
                        drawing.active.set(false);
                        drawing.status.set(error);
                    }
                } else {
                    drawing.active.set(false);
                }
            });
        }
        current
    });
}

fn subdivide(points: DraftPoints, segment: usize) -> Result<(), String> {
    let draft = points
        .get_untracked()
        .iter()
        .map(|point| Ok([number(&point[0])?, number(&point[1])?]))
        .collect::<Result<Vec<_>, String>>()?;
    let result = forge_world::path_stroke::insert_midpoint(&draft, segment)?;
    let [x, z] = result[segment + 1];
    // Preserve the author's text in all existing fields; only the new point is formatted.
    points.update(|draft| draft.insert(segment + 1, [x.to_string(), z.to_string()]));
    Ok(())
}

#[component]
fn TranslatePath(points: DraftPoints, status: RwSignal<String>) -> impl IntoView {
    let x = RwSignal::new("0".to_owned());
    let z = RwSignal::new("0".to_owned());
    view! {
        <details><summary>"Déplacer tout le tracé"</summary>
            <p>"Décalage X/Z en mètres, appliqué au brouillon seulement. Examiner ensuite pour vérifier les limites du terrain et les raccords."</p>
            {[ ("Décalage X (m)", x), ("Décalage Z (m)", z) ].into_iter().map(|(label, value)| view! {
                <label>{label}<input type="number" step="0.1" prop:value=move || value.get()
                    on:input=move |ev| value.set(event_target_value(&ev))/></label>
            }).collect_view()}
            <button on:click=move |_| {
                let result = (|| {
                    let draft = points.get_untracked().iter()
                        .map(|p| Ok([number(&p[0])?, number(&p[1])?]))
                        .collect::<Result<Vec<_>, String>>()?;
                    forge_world::path_stroke::translate(&draft, [number(&x.get_untracked())?, number(&z.get_untracked())?])
                })();
                match result {
                    Ok(translated) => {
                        points.set(translated.into_iter().map(|p| p.map(|v| v.to_string())).collect());
                        status.set("Tracé décalé dans le brouillon. Examiner puis Appliquer ; aucune sauvegarde.".into());
                    }
                    Err(error) => status.set(error),
                }
            }>"Décaler les points du brouillon"</button>
        </details>
    }
}

// TerrainBrushCursor accepts 2..=128 m. This is the outline radius only:
// picking still returns the exact ray hit, independent of the path width.
const PATH_PICK_OUTLINE_RADIUS_M: f32 = 2.0;

fn pick_point(
    state: RwSignal<Option<AuthoringState>>,
    points: DraftPoints,
    index: usize,
    status: RwSignal<String>,
) -> Result<(), String> {
    if !super::authoring_viewport::can_preview_asset() {
        return Err("Attendre la projection de la zone avant de placer un point".into());
    }
    let expected = head(state.get_untracked())?.content_hash()?;
    let draft = points.get_untracked();
    if index >= draft.len() {
        return Err("Point de chemin absent".into());
    }
    super::authoring_brush_pointer::arm(
        PATH_PICK_OUTLINE_RADIUS_M,
        false,
        Callback::new(move |picked: Result<Vec<[f32; 2]>, String>| {
            let result = (|| {
                if head(state.get_untracked())?.content_hash()? != expected
                    || points.get_untracked() != draft
                {
                    return Err("Zone ou points modifiés pendant le placement : recommencer".into());
                }
                let picked = picked?;
                let [x, z] = *picked.first().ok_or("Aucun terrain sous le pointeur")?;
                points.update(|values| values[index] = [x.to_string(), z.to_string()]);
                Ok(format!(
                    "Point {} placé dans le brouillon. Examiner puis Appliquer ; aucune modification enregistrée.",
                    index + 1
                ))
            })();
            status.set(result.unwrap_or_else(|error: String| error));
        }),
    );
    status.set(format!("Cliquer sur le terrain pour placer le point {}. Échap ou sortie de la vue : annulation, coordonnées inchangées.", index + 1));
    Ok(())
}

fn trace_path(
    state: RwSignal<Option<AuthoringState>>,
    points: DraftPoints,
    status: RwSignal<String>,
) -> Result<(), String> {
    if !super::authoring_viewport::can_preview_asset() {
        return Err("Attendre la projection de la zone avant de tracer".into());
    }
    let expected = head(state.get_untracked())?.content_hash()?;
    let draft = points.get_untracked();
    super::authoring_brush_pointer::arm(
        PATH_PICK_OUTLINE_RADIUS_M,
        true,
        Callback::new(move |picked: Result<Vec<[f32; 2]>, String>| {
            let result = (|| {
                if head(state.get_untracked())?.content_hash()? != expected
                    || points.get_untracked() != draft
                {
                    return Err("Zone ou points modifiés pendant le tracé : recommencer".into());
                }
                let simplified = forge_world::path_stroke::simplify(&picked?, 1.0)?;
                let count = simplified.len();
                points.set(
                    simplified
                        .into_iter()
                        .map(|[x, z]| [x.to_string(), z.to_string()])
                        .collect(),
                );
                Ok(format!(
                    "Brouillon remplacé par {count} points, simplification ≤ 1 m aux échantillons. Examiner puis Appliquer ; aucune sauvegarde implicite."
                ))
            })();
            status.set(result.unwrap_or_else(|error: String| error));
        }),
    );
    status.set("Tracer sur le terrain en maintenant le bouton gauche, puis relâcher. Remplace uniquement les points du brouillon ; Échap ou sortie de vue annule.".into());
    Ok(())
}

/// Each click uses the ordinary terrain picker and retains its revision guard.
/// Re-arming happens after the picker releases its borrow; no source is written.
fn append_points(
    state: RwSignal<Option<AuthoringState>>,
    points: DraftPoints,
    status: RwSignal<String>,
    first: bool,
) -> Result<(), String> {
    if !super::authoring_viewport::can_preview_asset() {
        return Err("Attendre le terrain avant de dessiner le chemin".into());
    }
    let expected = head(state.get_untracked())?.content_hash()?;
    let before = points.get_untracked();
    super::authoring_brush_pointer::arm(
        2.0,
        false,
        Callback::new(move |picked: Result<Vec<[f32; 2]>, String>| {
            let result = (|| {
                if head(state.get_untracked())?.content_hash()? != expected
                    || points.get_untracked() != before
                {
                    return Err("Le brouillon a changé. Relancer le dessin.".into());
                }
                let [x, z] = *picked?.first().ok_or("Cliquer à l’intérieur du terrain")?;
                if first {
                    points.set(Vec::new());
                }
                if points.get_untracked().len() >= 128 {
                    return Err("Limite de 128 points atteinte. Terminer le tracé.".into());
                }
                points.update(|values| values.push([x.to_string(), z.to_string()]));
                append_points(state, points, status, false)?;
                Ok(format!(
                    "{} points · cliquer pour continuer, puis Terminer.",
                    points.get_untracked().len()
                ))
            })();
            status.set(result.unwrap_or_else(|error: String| error));
        }),
    );
    status.set(if first {
        "Cliquer les points du chemin sur le terrain, puis Terminer.".into()
    } else {
        format!(
            "{} points · cliquer pour continuer, puis Terminer.",
            points.get_untracked().len()
        )
    });
    Ok(())
}

pub(super) fn describe_changes(before: &[AuthoredPath], after: &[AuthoredPath]) -> String {
    let describe = |p: &AuthoredPath| {
        let points = p
            .points
            .iter()
            .map(|p| format!("({}, {})", p.x_m, p.z_m))
            .collect::<Vec<_>>()
            .join(" → ");
        format!(
            "{} [{}] : largeur {} m, accotement {} m, verrou {}, points {}",
            p.name, p.id, p.width_m, p.shoulder_m, p.locked, points
        )
    };
    let mut changes = Vec::new();
    for old in before {
        match after.iter().find(|p| p.id == old.id) {
            None => changes.push(format!("Supprimer {}", describe(old))),
            Some(new) if new != old => {
                changes.push(format!("Avant {} ; après {}", describe(old), describe(new)));
            }
            _ => (),
        }
    }
    for new in after
        .iter()
        .filter(|p| !before.iter().any(|old| old.id == p.id))
    {
        changes.push(format!("Ajouter {}", describe(new)));
    }
    changes.join(" | ")
}

fn head(state: Option<AuthoringState>) -> Result<ZoneAuthoringHead, String> {
    let state = state.ok_or("Ouvrir une zone")?;
    if state.playtest_hash.is_some() {
        return Err("Revenir en édition avant de modifier un chemin".into());
    }
    state
        .zone_head
        .ok_or_else(|| "Convertir la zone avant d’éditer les chemins".into())
}

fn request(state: Option<AuthoringState>, edit: PathEdit) -> Result<AuthoringRequest, String> {
    let head = head(state)?;
    let identity =
        serde_json::to_string(&(head.content_hash()?, &edit)).map_err(|e| e.to_string())?;
    forge_world::path_surface::prepare_edit(&head, CommandId::deterministic(identity), edit)
        .map_err(|error| format!("Tracé non préparé : {error}. Aucune modification appliquée."))
        .map(|request| AuthoringRequest::PreviewZone { request })
}

fn intersections(state: Option<AuthoringState>) -> Result<AuthoringRequest, String> {
    let head = head(state)?;
    let id = CommandId::deterministic(format!("path-intersections:{}", head.content_hash()?));
    forge_world::path_intersections::prepare(&head, id)
        .map(|request| AuthoringRequest::PreviewZone { request })
}

// Navigation only: read the accepted path, never the inspector's unaccepted draft.
fn frame_selected(state: Option<AuthoringState>, selected: &str) -> Result<(), String> {
    let head = head(state)?;
    let path = head
        .corrections
        .paths
        .as_deref()
        .unwrap_or_default()
        .iter()
        .find(|path| path.id.to_string() == selected)
        .ok_or("Sélectionner un chemin accepté")?;
    let terrain = super::authoring_scene::terrain(&head)?;
    let plan = forge_world::TerrainGenerationPlan::from_document(&terrain)
        .map_err(|error| error.to_string())?;
    let mut min = bevy::prelude::Vec3::splat(f32::INFINITY);
    let mut max = bevy::prelude::Vec3::splat(f32::NEG_INFINITY);
    for point in &path.points {
        let y = plan
            .surface_height_at(point.x_m, point.z_m)
            .map_err(|error| error.to_string())?;
        let position = bevy::prelude::Vec3::new(point.x_m, y, point.z_m);
        min = min.min(position);
        max = max.max(position);
    }
    if !min.is_finite() || !max.is_finite() {
        return Err("Le chemin ne contient aucun point cadrable".into());
    }
    let margin = path.width_m + 2.0 * path.shoulder_m;
    super::authoring_camera::focus_object(
        (min + max) * 0.5,
        ((max - min).length() + margin).max(4.0) * 1.8,
    );
    Ok(())
}

#[component]
pub(super) fn PathControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    selected: RwSignal<String>,
    active_tab: RwSignal<super::authoring_controls::AuthoringTab>,
    on_command: SendCommand,
) -> impl IntoView {
    let framing_status = RwSignal::new(String::new());
    let reset = RwSignal::new(0_u64);
    // Inspect/Preview/Save update AuthoringState without changing the selected
    // path. Remount on zone identity too: copied zones may contain identical paths.
    let inspected = Memo::new(move |_| {
        let reset = reset.get();
        let id = selected.get();
        let h = head(state.get()).ok()?;
        let area = h.base.terrain.area_id;
        if id.is_empty() {
            let fingerprint = h.content_hash().ok()?;
            Some((
                AuthoredPath {
                    id: EntityId::deterministic(format!("authored-path:{fingerprint}")),
                    name: "Chemin".into(),
                    points: vec![
                        PathPoint {
                            x_m: -5.0,
                            z_m: 0.0,
                        },
                        PathPoint { x_m: 5.0, z_m: 0.0 },
                    ],
                    width_m: 3.0,
                    shoulder_m: 1.0,
                    profile: None,
                    locked: false,
                },
                true,
                reset,
                area,
            ))
        } else {
            h.corrections
                .paths
                .unwrap_or_default()
                .into_iter()
                .find(|p| p.id.to_string() == id)
                .map(|p| (p, false, reset, area))
        }
    });
    view! {
        <Show when=move || state.get().is_some_and(|s| s.zone_head.is_some())>
            <div class="gold-path-controls">
                <details><summary>"Aide et état du chemin"</summary>
                    <super::authoring_path_projection::ProjectionStatus/>
                    <p>"Préparer, examiner puis Appliquer. Sauvegarder avec les commandes de zone. La surface suit le terrain et écarte la végétation générée sans effacer les masques. Le playtest attend les projections concordantes ; intersections et qualification visuelle restent à terminer."</p>
                </details>
                <fieldset disabled=move || busy.get() || state.get().is_none_or(|s| s.playtest_hash.is_some())>
                    <details><summary>"Raccords et croisements"</summary>
                    <button on:click=move |_| on_command.run(intersections(state.get_untracked()))>"Examiner les points de croisement"</button>
                    <details><summary>"Comment fonctionnent les croisements ?"</summary><p>"Ajoute les points communs aux tracés acceptés qui se croisent, sans déplacer leurs points existants. Une seule retouche annulable après Appliquer. Déverrouiller les chemins concernés. Les surfaces sont fusionnées à la projection ; les futurs déplacements des points restent indépendants."</p></details>
                    </details>
                    <button on:click=move |_| {
                        selected.set(String::new());
                        reset.update(|value| *value = value.wrapping_add(1));
                    }>"Nouveau chemin"</button>
                    <label>"Chemin à éditer"
                        <select prop:value=move || selected.get() on:change=move |ev| selected.set(event_target_value(&ev))>
                            <option value="">"Nouveau tracé…"</option>
                            {move || state.get().and_then(|s| s.zone_head).and_then(|h| h.corrections.paths).unwrap_or_default().into_iter().map(|p| view! {
                                <option value=p.id.to_string()>{format!("{} · {} points{}", p.name, p.points.len(), if p.locked { " · verrouillé" } else { "" })}</option>
                            }).collect_view()}
                        </select>
                    </label>
                    <button hidden=move || selected.get().is_empty() on:click=move |_| {
                        let result = frame_selected(state.get_untracked(), &selected.get_untracked());
                        framing_status.set(result.map_or_else(|error| error, |()| "Cadrage du chemin accepté demandé ; carte inchangée.".into()));
                    }>"Cadrer le chemin accepté"</button>
                    <output aria-live="polite">{move || framing_status.get()}</output>
                    {move || inspected.get().map(|(path, insert, _, area)| view! { <PathInspector path=path insert=insert area=area state=state active_tab=active_tab on_command=on_command/> })}
                </fieldset>
            </div>
        </Show>
    }
}

#[component]
fn PathInspector(
    path: AuthoredPath,
    insert: bool,
    area: forge_core::AreaId,
    state: RwSignal<Option<AuthoringState>>,
    active_tab: RwSignal<super::authoring_controls::AuthoringTab>,
    on_command: SendCommand,
) -> impl IntoView {
    let on_command = Callback::new(move |request| {
        let current = state.with_untracked(|s| {
            s.as_ref()
                .and_then(|s| s.zone_head.as_ref())
                .map(|h| h.base.terrain.area_id)
        });
        on_command.run(super::authoring_draft_scope::check(area, current).and(request));
    });
    let id = path.id;
    let locked = path.locked;
    let name = RwSignal::new(path.name);
    let width = RwSignal::new(path.width_m.to_string());
    let shoulder = RwSignal::new(path.shoulder_m.to_string());
    let use_profile = RwSignal::new(path.profile.is_some());
    let elevations = RwSignal::new(path.profile.as_ref().map_or_else(Vec::new, |profile| {
        profile
            .elevations_m
            .iter()
            .map(ToString::to_string)
            .collect()
    }));
    let crossfall = RwSignal::new(
        path.profile
            .as_ref()
            .map_or_else(String::new, |profile| profile.crossfall_ratio.to_string()),
    );
    let cut_fill = RwSignal::new(path.profile.as_ref().map_or_else(String::new, |profile| {
        profile.maximum_cut_fill_m.to_string()
    }));
    let points = RwSignal::new(
        path.points
            .iter()
            .map(|p| [p.x_m.to_string(), p.z_m.to_string()])
            .collect::<Vec<_>>(),
    );
    let point_status = RwSignal::new(String::new());
    let drawing = RwSignal::new(false);
    invalidate_changed_draft(
        name,
        width,
        shoulder,
        points,
        ProfileDraftSignals {
            use_profile,
            elevations,
            crossfall,
            cut_fill,
        },
        DrawingSession {
            state,
            active: drawing,
            status: point_status,
        },
    );
    let guides = RwSignal::new(true);
    let advanced = RwSignal::new(false);
    let before_drawing = RwSignal::new(points.get_untracked());
    Effect::new(move |_| {
        if active_tab.get() != super::authoring_controls::AuthoringTab::Routes {
            drawing.set(false);
        }
    });
    let active_point = RwSignal::new(None::<usize>);
    Effect::new(move |_| {
        let state = state.get();
        let points = points.get();
        let active = active_point.get();
        if !guides.get()
            || active_tab.get() != super::authoring_controls::AuthoringTab::Routes
            || state.as_ref().is_none_or(|s| s.playtest_hash.is_some())
        {
            super::authoring_path_guides::clear();
            return;
        }
        let terrain = state
            .and_then(|s| s.zone_head)
            .and_then(|h| super::authoring_scene::terrain(&h).ok())
            .and_then(|terrain| terrain.content_hash().ok());
        if let Some(terrain) = terrain {
            super::authoring_path_guides::show(
                terrain,
                points
                    .iter()
                    .map(|p| Some([number(&p[0]).ok()?, number(&p[1]).ok()?]))
                    .collect(),
                active,
                drawing.get(),
            );
        } else {
            super::authoring_path_guides::clear();
        }
    });
    super::authoring_path_guides::bind_drag(Callback::new(move |index| {
        if locked
            || drawing.get_untracked()
            || active_tab.get_untracked() != super::authoring_controls::AuthoringTab::Routes
        {
            return false;
        }
        let Ok(expected) = head(state.get_untracked()).and_then(|head| head.content_hash()) else {
            return false;
        };
        let before = points.get_untracked();
        if index >= before.len() {
            return false;
        }
        active_point.set(Some(index));
        super::authoring_brush_pointer::arm(
            2.0,
            true,
            Callback::new(move |picked: Result<Vec<[f32; 2]>, String>| {
                let result = (|| {
                    if head(state.get_untracked())?.content_hash()? != expected
                        || points.get_untracked() != before
                    {
                        return Err(
                            "Le brouillon a changé pendant le geste ; déplacement refusé."
                                .to_owned(),
                        );
                    }
                    let [x, z] = *picked?.last().ok_or("Relâcher la poignée sur le terrain")?;
                    points.update(|values| values[index] = [x.to_string(), z.to_string()]);
                    Ok("Point déplacé dans le brouillon. Examiner puis Appliquer pour conserver la retouche.".to_owned())
                })();
                point_status.set(result.unwrap_or_else(|error| error));
            }),
        );
        point_status.set(
            "Glisser la poignée sur le terrain ; Échap annule sans changer le brouillon.".into(),
        );
        true
    }));
    on_cleanup(|| {
        DRAWING_KEY.with(|handler| handler.set(None));
        super::authoring_brush_pointer::cancel();
        super::authoring_path_guides::clear();
    });
    let prepare = move || {
        let points = points
            .get_untracked()
            .iter()
            .map(|p| {
                Ok(PathPoint {
                    x_m: number(&p[0])?,
                    z_m: number(&p[1])?,
                })
            })
            .collect::<Result<Vec<_>, String>>()?;
        let profile = use_profile
            .get_untracked()
            .then(|| {
                let values = elevations.get_untracked();
                if values.len() != points.len() {
                    return Err(
                        "Une altitude (m) est requise pour chaque point du profil".to_owned()
                    );
                }
                Ok(PathVerticalProfileV1 {
                    elevations_m: values
                        .iter()
                        .map(|value| number(value))
                        .collect::<Result<_, _>>()?,
                    crossfall_ratio: number(&crossfall.get_untracked())?,
                    maximum_cut_fill_m: number(&cut_fill.get_untracked())?,
                })
            })
            .transpose()?;
        let path = AuthoredPath {
            id,
            name: name.get_untracked(),
            points,
            width_m: number(&width.get_untracked())?,
            shoulder_m: number(&shoulder.get_untracked())?,
            profile,
            locked,
        };
        request(
            state.get_untracked(),
            if insert {
                PathEdit::Insert { path }
            } else {
                PathEdit::Replace { path }
            },
        )
    };
    DRAWING_KEY.with(|handler| {
        handler.set(Some(Callback::new(move |key: String| {
            if !drawing.get_untracked()
                || active_tab.get_untracked() != super::authoring_controls::AuthoringTab::Routes
            {
                return false;
            }
            match key.as_str() {
                "Enter" => {
                    drawing.set(false);
                    super::authoring_brush_pointer::cancel();
                    on_command.run(prepare());
                }
                "Backspace" => {
                    super::authoring_brush_pointer::cancel();
                    points.update(|values| {
                        values.pop();
                    });
                    if let Err(error) = append_points(state, points, point_status, false) {
                        point_status.set(error);
                    }
                }
                "Escape" => {
                    drawing.set(false);
                    super::authoring_brush_pointer::cancel();
                    points.set(before_drawing.get_untracked());
                    point_status.set("Dessin annulé ; brouillon précédent restauré.".into());
                }
                _ => return false,
            }
            true
        })));
    });
    view! {
        <div class="gold-path-inspector">
        <div class="gold-subtools">
            <button disabled=locked aria-pressed=move || drawing.get().to_string() on:click=move |_| {
                if drawing.get_untracked() {
                    if let Err(error) = append_points(state, points, point_status, false) { point_status.set(error); }
                    return;
                }
                before_drawing.set(points.get_untracked());
                drawing.set(true);
                if let Err(error) = append_points(state, points, point_status, true) { point_status.set(error); }
            }>"Par points"</button>
            <button disabled=locked on:click=move |_| {
                drawing.set(false);
                if let Err(error) = trace_path(state, points, point_status) { point_status.set(error); }
            }>"Main levée"</button>
        </div>
        <button disabled=locked hidden=move || drawing.get() on:click=move |_| {
            before_drawing.set(points.get_untracked());
            drawing.set(true);
            if let Err(error) = append_points(state, points, point_status, false) { point_status.set(error); }
        }>"Continuer le tracé existant"</button>
        <button disabled=locked hidden=move || !drawing.get() data-primary-action="path-finish" on:click=move |_| {
            drawing.set(false);
            super::authoring_brush_pointer::cancel();
            on_command.run(prepare());
        }>"Terminer et prévisualiser"</button>
        <p class="authoring-hint">"Clics pour dessiner · Entrée termine · hors dessin, glisser une poignée pour ajuster · Échap annule."</p>
        <output aria-live="polite">{move || point_status.get()}</output>
        <details><summary>"Aide et repères"</summary>
        <label><input type="checkbox" prop:checked=move || guides.get()
            on:change=move |ev| guides.set(event_target_checked(&ev))/>
            "Afficher les repères des points du brouillon"
        </label>
        <p>{if insert {
            "Source : nouveau tracé en brouillon, non présent dans la zone avant Appliquer."
        } else if locked {
            "Source : tracé auteur accepté. Points, largeur et accotement conservés lors des régénérations de zone. Verrou actif sur le chemin entier ; les champs sont un brouillon jusqu’à Appliquer."
        } else {
            "Source : tracé auteur accepté. Points, largeur et accotement conservés lors des régénérations de zone. Verrou inactif ; les champs sont un brouillon jusqu’à Appliquer."
        }}</p>
        <p>"Repères jaunes, point repéré orange. Coordonnées invalides ou hors terrain non affichées. Repères uniquement : Examiner puis Appliquer pour modifier le chemin."</p>
        </details>
        <fieldset disabled=locked>
            <details class="atelier-path-properties"><summary>"Propriétés et points du tracé"</summary>
            <label>"Nom"<input prop:value=move || name.get() on:input=move |ev| name.set(event_target_value(&ev))/></label>
            <div class="gold-inline-fields">
            <label>"Largeur (m)"<input type="number" min="0.1" max="64" step="0.1" prop:value=move || width.get() on:input=move |ev| width.set(event_target_value(&ev))/></label>
            <label>"Accotement (m)"<input type="number" min="0" max="32" step="0.1" prop:value=move || shoulder.get() on:input=move |ev| shoulder.set(event_target_value(&ev))/></label>
            </div>
            <button on:click=move |_| advanced.update(|value| *value = !*value)>{move || if advanced.get() { "← Fermer l’édition précise" } else { "Points et profil avancés →" }}</button>
            <div hidden=move || !advanced.get()>
            <TranslatePath points=points status=point_status/>
            <label><input type="checkbox" prop:checked=move || use_profile.get() on:change=move |ev| { use_profile.set(event_target_checked(&ev)); super::authoring_shortcuts::cancel_preview(); }/>"Profil vertical auteur"</label>
            <Show when=move || use_profile.get()>
                <label>"Dévers (ratio, -0.25 à 0.25)"<input type="number" step="0.01" prop:value=move || crossfall.get() on:input=move |ev| { crossfall.set(event_target_value(&ev)); super::authoring_shortcuts::cancel_preview(); }/></label>
                <label>"Cut/fill maximum (m)"<input type="number" min="0" max="32" step="0.1" prop:value=move || cut_fill.get() on:input=move |ev| { cut_fill.set(event_target_value(&ev)); super::authoring_shortcuts::cancel_preview(); }/></label>
            </Show>
            <label>"Point à modifier"<select prop:value=move || active_point.get().unwrap_or(0).to_string()
                on:change=move |ev| active_point.set(event_target_value(&ev).parse::<usize>().ok())>
                {move || (0..points.get().len()).map(|i| view! { <option value=i.to_string()>{format!("Point {}", i + 1)}</option> }).collect_view()}
            </select></label>
            <For each=move || super::authoring_index_page::point_range(points.get().len(), active_point.get().unwrap_or(0)) key=|i| *i children=move |i| view! {
                <div class="gold-point-editor">
                    <span>{format!("Point {}", i + 1)}</span>
                    <Show when=move || use_profile.get()>
                        <label>"Altitude profil (m)"<input type="number" step="0.1" prop:value=move || elevations.get().get(i).cloned().unwrap_or_default() on:input=move |ev| { elevations.update(|values| { if values.len() < points.get_untracked().len() { values.resize(points.get_untracked().len(), String::new()); } let Some(value) = values.get_mut(i) else { return; }; *value = event_target_value(&ev); }); super::authoring_shortcuts::cancel_preview(); }/></label>
                    </Show>
                    <button on:click=move |_| {
                        active_point.set(Some(i));
                        guides.set(true);
                        point_status.set(format!("Point {} repéré en orange dans la vue ; aucune modification.", i + 1));
                    }>"Repérer ce point"</button>
                    {["X (m)", "Z (m)"].into_iter().enumerate().map(|(axis, label)| view! {
                        <label>{label}<input type="number" step="0.1"
                            prop:value=move || points.get().get(i).map_or_else(String::new, |p| p[axis].clone())
                            on:input=move |ev| points.update(|ps| { if let Some(p) = ps.get_mut(i) { p[axis] = event_target_value(&ev); } })/></label>
                    }).collect_view()}
                    <button on:click=move |_| {
                        active_point.set(Some(i));
                        guides.set(true);
                        if let Err(error) = pick_point(state, points, i, point_status) {
                            point_status.set(error);
                        }
                    }>"Placer ce point dans la vue"</button>
                    <button disabled={move || points.get().len() <= 2} on:click=move |_| points.update(|ps| { if i < ps.len() { ps.remove(i); } })>"Retirer ce point"</button>
                    <button disabled=move || i == 0 on:click=move |_| points.update(|ps| { if i > 0 && i < ps.len() { ps.swap(i, i - 1); } })>"Monter"</button>
                    <button disabled=move || { points.get().len() >= 128 || i + 1 >= points.get().len() }
                        on:click=move |_| {
                            match subdivide(points, i) {
                                Ok(()) => {
                                    super::authoring_brush_pointer::cancel();
                                    active_point.set(Some(i + 1));
                                    guides.set(true);
                                    point_status.set(format!("Point {} inséré au milieu du segment et repéré en orange. Voisins conservés ; Examiner puis Appliquer.", i + 2));
                                }
                                Err(error) => point_status.set(error),
                            }
                        }>"Insérer un point avant le suivant"</button>
                </div>
            }/>
            <button disabled={move || points.get().len() >= 128} on:click=move |_| points.update(|ps| { if ps.len() < 128 { ps.push([String::new(), String::new()]); } })>"Ajouter un point"</button>
            </div>
            <details><summary>"Autres actions de dessin"</summary>
            <button on:click=move |_| {
                if let Err(error) = trace_path(state, points, point_status) {
                    point_status.set(error);
                }
            }>"Retracer les points dans la vue"</button>
            <button on:click=move |_| {
                super::authoring_brush_pointer::cancel();
                point_status.set("Placement du point annulé ; coordonnées conservées.".into());
            }>"Quitter le placement de point"</button>
            <output aria-live="polite">{move || point_status.get()}</output>
            </details>
            </details>
            <button class="authoring-primary" hidden=move || drawing.get() data-primary-action="path-preview" on:click=move |_| on_command.run(prepare())>"Examiner le tracé"</button>
        </fieldset>
        <Show when=move || !insert>
            <details><summary>"Verrouillage et suppression"</summary>
            <button on:click=move |_| on_command.run(request(state.get_untracked(), PathEdit::SetLocked { id, locked: !locked }))>{if locked { "Examiner le déverrouillage" } else { "Examiner le verrouillage" }}</button>
            <button disabled=locked on:click=move |_| on_command.run(request(state.get_untracked(), PathEdit::Remove { id }))>"Examiner la suppression du chemin"</button>
            </details>
        </Show>
        </div>
    }
}

fn number(text: &str) -> Result<f32, String> {
    text.trim()
        .parse::<f32>()
        .ok()
        .filter(|v| v.is_finite())
        .ok_or_else(|| "Coordonnées et profil : nombres finis requis".into())
}
