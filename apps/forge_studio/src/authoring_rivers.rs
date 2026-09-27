//! River recipes use the shared review/apply command bus; drafts are never persisted implicitly.
use forge_core::{CommandId, EntityId, Vec3};
use forge_project::{
    RiverControlPoint, RiverElevationPolicy, RiverPathAlgorithm,
    authoring::{AuthoringRequest, AuthoringState},
    zone_authoring::{
        ZoneAuthoringHead,
        rivers::{AuthoredRiver, RiverEdit},
    },
};
use leptos::prelude::*;

type SendCommand = Callback<Result<AuthoringRequest, String>>;
type Points = RwSignal<Vec<[String; 5]>>;
mod picking;

pub(super) fn describe_changes(before: &[AuthoredRiver], after: &[AuthoredRiver]) -> String {
    let describe = |r: &AuthoredRiver| {
        let points = r
            .water
            .river
            .control_points
            .iter()
            .map(|p| {
                format!(
                    "({}, {}, {} ; largeur {}, profondeur {})",
                    p.center_m.x, p.center_m.y, p.center_m.z, p.surface_width_m, p.bed_depth_m,
                )
            })
            .collect::<Vec<_>>()
            .join(" → ");
        format!(
            "{} [{}] : berge {} + {} m, verrou {}, points {}",
            r.water.name,
            r.id,
            r.water.banks.shoulder_width_m,
            r.water.banks.blend_width_m,
            r.locked,
            points
        )
    };
    let mut changes = Vec::new();
    for old in before {
        match after.iter().find(|r| r.id == old.id) {
            None => changes.push(format!("Supprimer {}", describe(old))),
            Some(new) if new != old => {
                changes.push(format!("Avant {} ; après {}", describe(old), describe(new)));
            }
            _ => (),
        }
    }
    for new in after
        .iter()
        .filter(|r| !before.iter().any(|old| old.id == r.id))
    {
        changes.push(format!("Ajouter {}", describe(new)));
    }
    changes.join(" | ")
}

fn head(state: Option<AuthoringState>) -> Result<ZoneAuthoringHead, String> {
    let state = state.ok_or("Ouvrir une zone")?;
    if state.playtest_hash.is_some() {
        return Err("Revenir en édition avant de modifier une rivière".into());
    }
    state
        .zone_head
        .ok_or_else(|| "Convertir la zone avant de créer une rivière".into())
}

fn request(state: Option<AuthoringState>, edit: RiverEdit) -> Result<AuthoringRequest, String> {
    let head = head(state)?;
    let identity =
        serde_json::to_string(&(head.content_hash()?, &edit)).map_err(|e| e.to_string())?;
    let request = forge_world::river_projection::prepare_edit(
        &head,
        CommandId::deterministic(identity),
        edit,
    )?;
    Ok(AuthoringRequest::PreviewZone { request })
}

fn new_river(head: &ZoneAuthoringHead) -> Result<AuthoredRiver, String> {
    let terrain = head.effective()?.0;
    let mut water =
        forge_world::embedded_visual_valley_water_document().map_err(|e| e.to_string())?;
    water.project_id = terrain.project_id;
    water.area_id = terrain.area_id;
    water.name = "Rivière".into();
    water.seed = terrain.seed;
    water.river.algorithm = RiverPathAlgorithm::PiecewiseLinearV1;
    water.river.elevation_policy = RiverElevationPolicy::AuthoredSurfaceV1;
    water.river.control_points = [-30.0, -10.0, 10.0, 30.0]
        .map(|x| RiverControlPoint {
            center_m: Vec3::new(x, terrain.height_sampling.base_elevation_m - 0.5, 0.0),
            surface_width_m: 6.0,
            bed_depth_m: 1.0,
        })
        .to_vec();
    water.banks.shoulder_width_m = 2.0;
    water.banks.blend_width_m = 2.0;
    water.banks.wet_margin_m = 0.5;
    if let forge_project::WaterRefractionProfile::ScreenSpace { max_depth_m, .. } =
        &mut water.optics.refraction
    {
        *max_depth_m = 1.0;
    }
    Ok(AuthoredRiver {
        id: EntityId::deterministic(format!("authoring-river:{}", head.content_hash()?)),
        water,
        maximum_cut_m: 0.0,
        locked: false,
    })
}

#[component]
pub(super) fn RiverControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    selected: RwSignal<String>,
    on_command: SendCommand,
) -> impl IntoView {
    let reset = RwSignal::new(0_u64);
    let inspected = Memo::new(move |_| {
        let head = head(state.get()).ok()?;
        // Identical rivers in copied zones must not share an unsaved inspector draft.
        let area = head.base.terrain.area_id;
        let id = selected.get();
        let reset = reset.get();
        if id.is_empty() {
            new_river(&head).ok().map(|r| (r, true, reset, area))
        } else {
            head.corrections
                .rivers
                .unwrap_or_default()
                .into_iter()
                .find(|r| r.id.to_string() == id)
                .map(|r| (r, false, reset, area))
        }
    });
    view! {
        <Show when=move || state.get().is_some_and(|s| s.zone_head.is_some())>
            <div class="gold-river-controls">
                <p class="authoring-hint">"Dessinez le cours d’eau, vérifiez son aperçu, puis appliquez."</p>
                <fieldset disabled=move || busy.get() || state.get().is_none_or(|s| s.playtest_hash.is_some())>
                    <button on:click=move |_| { selected.set(String::new()); reset.update(|v| *v = v.wrapping_add(1)); }>"Nouvelle rivière"</button>
                    <label>"Rivière à éditer"<select prop:value=move || selected.get() on:change=move |ev| selected.set(event_target_value(&ev))>
                        <option value="">"Nouveau tracé…"</option>
                        {move || state.get().and_then(|s| s.zone_head).and_then(|h| h.corrections.rivers).unwrap_or_default().into_iter().map(|r| view! {
                            <option value=r.id.to_string()>{format!("{} · {} points{}", r.water.name, r.water.river.control_points.len(), if r.locked { " · verrouillée" } else { "" })}</option>
                        }).collect_view()}
                    </select></label>
                    {move || inspected.get().map(|(river, insert, _, area)| view! { <RiverInspector river=river insert=insert area=area state=state on_command=on_command/> })}
                </fieldset>
            </div>
        </Show>
    }
}

fn number(text: &str) -> Result<f32, String> {
    let value = text.trim().parse::<f32>().map_err(|_| "Nombre invalide")?;
    if !value.is_finite() {
        return Err("Nombre fini requis".into());
    }
    Ok(value)
}

fn read_points(points: Points) -> Result<Vec<RiverControlPoint>, String> {
    points
        .get_untracked()
        .iter()
        .map(|p| {
            Ok(RiverControlPoint {
                center_m: Vec3::new(number(&p[0])?, number(&p[1])?, number(&p[2])?),
                surface_width_m: number(&p[3])?,
                bed_depth_m: number(&p[4])?,
            })
        })
        .collect()
}

fn insert_midpoint(points: Points, index: usize) -> Result<(), String> {
    let draft = points.get_untracked();
    if draft.len() >= 128 {
        return Err("128 points maximum".into());
    }
    let a = draft.get(index).ok_or("Point absent")?;
    let b = draft.get(index + 1).ok_or("Aucun segment après ce point")?;
    let mut inserted = [
        String::new(),
        String::new(),
        String::new(),
        String::new(),
        String::new(),
    ];
    for column in 0..5 {
        inserted[column] = number(&a[column])?
            .midpoint(number(&b[column])?)
            .to_string();
    }
    crate::authoring_brush_pointer::cancel();
    points.update(|p| p.insert(index + 1, inserted));
    Ok(())
}

#[component]
fn RiverInspector(
    river: AuthoredRiver,
    insert: bool,
    area: forge_core::AreaId,
    state: RwSignal<Option<AuthoringState>>,
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
    let id = river.id;
    let locked = river.locked;
    let name = RwSignal::new(river.water.name.clone());
    let shoulder = RwSignal::new(river.water.banks.shoulder_width_m.to_string());
    let blend = RwSignal::new(river.water.banks.blend_width_m.to_string());
    let points = RwSignal::new(
        river
            .water
            .river
            .control_points
            .iter()
            .map(|p| {
                [
                    p.center_m.x,
                    p.center_m.y,
                    p.center_m.z,
                    p.surface_width_m,
                    p.bed_depth_m,
                ]
                .map(|v| v.to_string())
            })
            .collect::<Vec<_>>(),
    );
    super::authoring_draft_scope::invalidate_on_change(move || {
        (name.get(), shoulder.get(), blend.get(), points.get())
    });
    let prepare = move || {
        let mut river = river.clone();
        river.water.name = name.get_untracked();
        river.water.banks.shoulder_width_m = number(&shoulder.get_untracked())?;
        river.water.banks.blend_width_m = number(&blend.get_untracked())?;
        river.water.river.control_points = read_points(points)?;
        request(
            state.get_untracked(),
            if insert {
                RiverEdit::Insert { river }
            } else {
                RiverEdit::Replace { river }
            },
        )
    };
    view! {
        <fieldset disabled=locked>
            <details><summary>"Source et protection"</summary><p>{if insert {
                "Source : nouvelle recette de rivière en brouillon, non présente dans la zone avant Appliquer."
            } else if locked {
                "Source : recette de rivière auteur acceptée, conservée lors des régénérations de zone. Verrou actif sur la rivière entière. Niveaux Y absolus en mètres ; les champs sont un brouillon jusqu’à Appliquer."
            } else {
                "Source : recette de rivière auteur acceptée, conservée lors des régénérations de zone. Verrou inactif. Niveaux Y absolus en mètres ; les champs sont un brouillon jusqu’à Appliquer."
            }}</p></details>
            <details><summary>"Nom de la rivière"</summary>
                <label>"Nom"<input prop:value=move || name.get() on:input=move |ev| name.set(event_target_value(&ev))/></label>
            </details>
            <RiverPoints points=points state=state/>
            <button class="authoring-primary" data-primary-action="river-preview" on:click=move |_| on_command.run(prepare())>"Examiner la rivière"</button>
            <details><summary>"Creusement et berges"</summary>
            <label>"Accotement (m)"<input type="number" step="0.1" prop:value=move || shoulder.get() on:input=move |ev| shoulder.set(event_target_value(&ev))/></label>
            <label>"Transition berge (m)"<input type="number" step="0.1" prop:value=move || blend.get() on:input=move |ev| blend.set(event_target_value(&ev))/></label>
            <p>"Niveaux absolus en mètres. Le creusement refuse les conflits avec vos retouches ; aucune modification avant application."</p>
            </details>
        </fieldset>
        <Show when=move || !insert>
            <button on:click=move |_| on_command.run(request(state.get_untracked(), RiverEdit::SetLocked { id, locked: !locked }))>{if locked { "Examiner le déverrouillage" } else { "Examiner le verrouillage" }}</button>
            <button disabled=locked on:click=move |_| on_command.run(request(state.get_untracked(), RiverEdit::Remove { id }))>"Examiner la suppression"</button>
        </Show>
    }
}

#[component]
fn RiverPoints(points: Points, state: RwSignal<Option<AuthoringState>>) -> impl IntoView {
    let status = RwSignal::new(String::new());
    let selected_point = RwSignal::new(0_usize);
    on_cleanup(crate::authoring_brush_pointer::cancel);
    view! {
        <div class="gold-inline-fields">
        {[ (3, "Largeur (m)"), (4, "Profondeur (m)") ].into_iter().map(|(column, label)| view! {
            <label>{label}<input type="number" min="0.1" step="0.1"
                prop:value=move || points.get().first().map(|p| p[column].clone()).unwrap_or_default()
                on:input=move |ev| { let value = event_target_value(&ev); points.update(|ps| { for p in ps { p[column].clone_from(&value); } }); }/></label>
        }).collect_view()}
        </div>
        <button on:click=move |_| {
            if let Err(error) = picking::trace(state, points, status) { status.set(error); }
        }>"Tracer la rivière dans la vue"</button>
        <p>"Maintenir le clic gauche sur le terrain, puis relâcher. Le dessin remplace le tracé du brouillon."</p>
        <details><summary>"Points et section de la rivière"</summary>
        <label>"Point à modifier"<select prop:value=move || selected_point.get().to_string()
            on:change=move |ev| selected_point.set(event_target_value(&ev).parse().unwrap_or(0))>
            {move || (0..points.get().len()).map(|i| view! { <option value=i.to_string()>{format!("Point {}", i + 1)}</option> }).collect_view()}
        </select></label>
        <For each=move || super::authoring_index_page::point_range(points.get().len(), selected_point.get()) key=|i| *i children=move |i| view! {
            <fieldset><legend>{format!("Point {}", i + 1)}</legend>
                { ["X", "Niveau Y", "Z", "Largeur", "Profondeur"].into_iter().enumerate().map(|(j, label)| view! {
                    <label>{label}<input type="number" step="0.1" prop:value=move || points.get().get(i).map(|p| p[j].clone()).unwrap_or_default() on:input=move |ev| points.update(|p| { if let Some(point) = p.get_mut(i) { point[j] = event_target_value(&ev); } })/></label>
                }).collect_view() }
                <button on:click=move |_| {
                    if let Err(error) = picking::arm(state, points, i, status) { status.set(error); }
                }>"Placer X/Z dans la vue"</button>
                <button disabled=move || { i + 1 >= points.get().len() || points.get().len() >= 128 } on:click=move |_| {
                    status.set(insert_midpoint(points, i).map_or_else(|error| error, |()| "Point intermédiaire ajouté au brouillon ; niveau et section interpolés, voisins inchangés.".into()));
                }>"Insérer au milieu du segment suivant"</button>
                <button disabled=move || { points.get().len() <= 4 } on:click=move |_| points.update(|p| { if p.len() > 4 && i < p.len() { p.remove(i); } })>"Retirer ce point du brouillon"</button>
            </fieldset>
        }/>
        <button disabled=move || { points.get().len() >= 128 } on:click=move |_| points.update(|p| {
            if p.len() < 128 && let Some(last) = p.last().cloned() { p.push(last); }
        })>"Ajouter un point à renseigner"</button>
        <p>"Un point ajouté duplique le dernier : renseigner ses coordonnées avant examen."</p>
        <p>"4 à 128 points. Les réglages de largeur et profondeur ci-dessus s’appliquent à tous les points ; les valeurs locales sont réglables ici."</p>
        </details>
        <button on:click=move |_| {
            crate::authoring_brush_pointer::cancel();
            status.set("Placement annulé ; brouillon conservé.".into());
        }>"Quitter le placement"</button>
        <output aria-live="polite">{move || status.get()}</output>
    }
}
