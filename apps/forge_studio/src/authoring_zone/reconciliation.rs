//! Explicit, paginated origin choices. Never writes files or applies a command itself.
use crate::reconciliation_protocol::{Page, Query, ReconciliationRecipe};
use forge_project::authoring::{AuthoringRequest, AuthoringState};
use leptos::prelude::*;
use std::collections::BTreeMap;

type Scope = (
    Result<ReconciliationRecipe, String>,
    Option<forge_core::ContentHash>,
    Option<forge_core::EntityId>,
);

#[component]
pub(crate) fn ReconciliationControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    recipe: Memo<Result<ReconciliationRecipe, String>>,
    on_command: Callback<Result<AuthoringRequest, String>>,
) -> impl IntoView {
    let draft = RwSignal::new(None::<Page>);
    let loading = RwSignal::new(false);
    let selected = RwSignal::new(BTreeMap::<forge_core::EntityId, forge_core::EntityId>::new());
    let origin_index = RwSignal::new(0_usize);
    let page = RwSignal::new(0_usize);
    let status = RwSignal::new(String::new());
    let origins = Memo::new(move |_| {
        state
            .get()
            .and_then(|s| s.zone_head)
            .and_then(|h| h.corrections.promotions)
            .unwrap_or_default()
    });
    let scope = Memo::new(move |_| {
        (
            recipe.get(),
            state
                .get()
                .and_then(|s| s.zone_head)
                .and_then(|h| h.content_hash().ok()),
        )
    });
    Effect::new(move |_| {
        let _ = scope.get();
        draft.set(None);
        selected.set(BTreeMap::new());
        origin_index.set(0);
        page.set(0);
        status.set(String::new());
    });
    let request_scope = Memo::new(move |_| {
        let (recipe, hash) = scope.get();
        let placement = origins
            .get()
            .get(origin_index.get())
            .map(|o| o.placement_id);
        (recipe, hash, placement)
    });
    let load = loader(
        request_scope,
        draft,
        loading,
        selected,
        page,
        status,
        on_command,
    );
    view! {
        <Show when=move || !origins.get().is_empty()>
        <details><summary>"Résoudre les origines promues pour ces réglages"</summary>
            <p>"Les objets retouchés ne bougent pas. Choisir pour chacun l’arbre de la nouvelle recette à exclure. Les coordonnées désignent les arbres candidats, pas les positions des objets retouchés. Rien n’est appliqué ici."</p>
            <p>"Arbres classés par distance horizontale à l’objet retouché, sans choix automatique. Pour un objet supprimé, classement par identifiant, sans distance. Une origine ne peut servir qu’à un objet."</p>
            <fieldset disabled=move || busy.get() || loading.get() || state.get().is_none_or(|s| s.playtest_hash.is_some())>
                <button on:click=move |_| {
                    selected.set(BTreeMap::new()); page.set(0); origin_index.set(0);
                    draft.set(None); load.run((0, false));
                }>"Lister les correspondances de ces réglages"</button>
                <label>"Objet dont l’origine doit être remplacée"
                    <select prop:value=move || origin_index.get().to_string() on:change=move |ev| {
                        origin_index.set(event_target_value(&ev).parse().unwrap_or(0));
                        if draft.get_untracked().is_some() {
                            draft.set(None); page.set(0); load.run((0, false));
                        }
                    }>
                        {move || origins.get().iter().enumerate().map(|(index, origin)| {
                            let name = state.get().and_then(|s| s.zone_head).and_then(|h| h.corrections.placements)
                                .and_then(|p| p.into_iter().find(|p| p.id == origin.placement_id))
                                .map_or_else(|| "Objet supprimé · exclusion conservée".into(), |p| p.name);
                            view! { <option value=index.to_string()>{format!("{name} · {}", origin.placement_id)}</option> }
                        }).collect_view()}
                    </select>
                </label>
                <button disabled=move || request_scope.get().2.is_none_or(|object| !selected.get().contains_key(&object))
                    on:click=move |_| {
                        let Some(object) = request_scope.get_untracked().2 else { return; };
                        crate::authoring_shortcuts::cancel_preview();
                        selected.update(|values| { values.remove(&object); });
                        status.set("Choix retiré du brouillon ; aucun objet supprimé.".into());
                    }>"Retirer le choix pour cet objet"</button>
                <CandidateButtons draft scope=request_scope selected status/>
                <button disabled=move || page.get() == 0 on:click=move |_| load.run(((page.get_untracked() - 1) * 20, false))>"Arbres précédents"</button>
                <button disabled=move || draft.get().is_none_or(|d| (page.get() + 1) * 20 >= d.total) on:click=move |_| load.run(((page.get_untracked() + 1) * 20, false))>"Arbres suivants"</button>
                <output>{move || format!("Page {} · {} correspondances choisies", page.get() + 1, selected.get().len())}</output>
                <output>{move || selected.get().iter().map(|(object, tree)| format!("Objet {object} → origine {tree}")).collect::<Vec<_>>().join(" ; ")}</output>
                <button disabled=move || draft.get().is_none() || selected.get().len() != origins.get().len() on:click=move |_| load.run((0, true))>"Examiner la réconciliation complète"</button>
            </fieldset>
            <output aria-live="polite">{move || status.get()}</output>
        </details>
        </Show>
    }
}

#[component]
fn CandidateButtons(
    draft: RwSignal<Option<Page>>,
    scope: Memo<Scope>,
    selected: RwSignal<BTreeMap<forge_core::EntityId, forge_core::EntityId>>,
    status: RwSignal<String>,
) -> impl IntoView {
    move || {
        draft.get().map(|d| d.trees.iter().map(|tree| {
        let id = tree.id;
        let distance = tree.distance_m.map_or_else(|| "distance indisponible".into(), |d| format!("à {d:.1} m"));
        let label = format!("{distance} · {} · X {:.1}, Y {:.1}, Z {:.1} · {}", tree.layer, tree.position[0], tree.position[1], tree.position[2], id);
        view! { <button
            disabled=move || selected.get().iter().any(|(object, tree)| *tree == id && Some(*object) != scope.get().2)
            aria-pressed=move || scope.get().2.is_some_and(|object| selected.get().get(&object) == Some(&id)).to_string()
            on:click=move |_| {
                let Some(object) = scope.get_untracked().2 else { return; };
                if selected.get_untracked().iter().any(|(other, tree)| *tree == id && *other != object) { return; }
                crate::authoring_shortcuts::cancel_preview();
                selected.update(|values| { values.insert(object, id); });
                status.set(format!("{} correspondances choisies", selected.get_untracked().len()));
            }>{label}</button> }
    }).collect_view())
    }
}

fn loader(
    scope: Memo<Scope>,
    draft: RwSignal<Option<Page>>,
    loading: RwSignal<bool>,
    selected: RwSignal<BTreeMap<forge_core::EntityId, forge_core::EntityId>>,
    page: RwSignal<usize>,
    status: RwSignal<String>,
    on_command: Callback<Result<AuthoringRequest, String>>,
) -> Callback<(usize, bool)> {
    Callback::new(move |(offset, prepare): (usize, bool)| {
        crate::authoring_shortcuts::cancel_preview();
        let expected = scope.get_untracked();
        let (Some(source_hash), Ok(recipe), Some(placement_id)) =
            (expected.1, expected.0.clone(), expected.2)
        else {
            status.set(
                expected
                    .0
                    .err()
                    .unwrap_or_else(|| "Zone ou objet promu absent".into()),
            );
            return;
        };
        loading.set(true);
        status.set("Calcul natif en cours…".into());
        let query = Query {
            source_hash,
            recipe: recipe.clone(),
            offset,
            placement_id,
            selections: prepare.then(|| selected.get_untracked()),
        };
        wasm_bindgen_futures::spawn_local(async move {
            let result = crate::web::invoke_tauri(&crate::StudioCommand::Reconciliation {
                query: Box::new(query),
            })
            .await;
            if scope.try_get_untracked().is_none() {
                return;
            }
            loading.set(false);
            if scope.get_untracked() != expected {
                return;
            }
            match result {
                Ok(crate::StudioResponse::Reconciliation { page: response }) => {
                    if response.source_hash != source_hash
                        || response.recipe != recipe
                        || response.offset != offset
                        || response.placement_id != placement_id
                        || response.trees.len() > 20
                    {
                        status.set("Réponse de correspondance incohérente".into());
                        return;
                    }
                    if prepare {
                        status.set(
                            "Correspondances calculées ; proposition transmise pour examen.".into(),
                        );
                        on_command.run(
                            response
                                .request
                                .ok_or("Commande de réconciliation absente".into())
                                .map(|request| AuthoringRequest::PreviewZone { request }),
                        );
                    } else {
                        page.set(offset / 20);
                        status.set(format!("{} arbres candidats", response.total));
                        draft.set(Some(*response));
                    }
                }
                Ok(crate::StudioResponse::Rejected { reason }) | Err(reason) => status.set(reason),
                _ => status.set("Réponse native inattendue".into()),
            }
        });
    })
}
