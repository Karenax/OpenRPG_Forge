//! Dedicated, read-only Vault search. Opening the dialog keeps the map mounted.
use crate::asset_library_protocol::{
    LibraryDetail, LibraryPage, LibraryQuery, LibraryRequest, LibraryResponse,
};
use leptos::prelude::*;
use wasm_bindgen::JsValue;

pub(super) fn requested() -> bool {
    web_sys::window()
        .and_then(|w| w.location().search().ok())
        .is_some_and(|query| {
            query
                .trim_start_matches('?')
                .split('&')
                .any(|part| part == "asset_library=1")
        })
}

#[component]
pub(super) fn LibraryWorkspace() -> impl IntoView {
    let close = Callback::new(|()| {
        if let Some(window) = web_sys::window() {
            let _ = window.location().set_search("?authoring=1");
        }
    });
    view! { <style>{include_str!("asset_library.css")}</style>
    <main class="asset-library-standalone"><LibraryPageView close=close/></main> }
}

pub(crate) async fn read(request: &LibraryRequest) -> Result<LibraryResponse, String> {
    let arguments = js_sys::Object::new();
    let text = serde_json::to_string(request).map_err(|e| e.to_string())?;
    js_sys::Reflect::set(
        &arguments,
        &JsValue::from_str("request"),
        &JsValue::from_str(&text),
    )
    .map_err(|_| "Impossible de préparer la recherche".to_owned())?;
    let response = crate::web::invoke_tauri_raw("read_asset_library", arguments.as_ref()).await?;
    serde_json::from_str(
        &response
            .as_string()
            .ok_or("Réponse de bibliothèque illisible")?,
    )
    .map_err(|e| e.to_string())
}

#[component]
pub(super) fn LibraryLauncher() -> impl IntoView {
    let dialog = NodeRef::<leptos::html::Dialog>::new();
    let open = RwSignal::new(false);
    let close = Callback::new(move |()| {
        if let Some(dialog) = dialog.get() {
            dialog.close();
        }
        open.set(false);
    });
    view! {
        <style>{include_str!("asset_library.css")}</style>
        <button class="asset-library-launch" title="Rechercher dans les packs importés" on:click=move |_| {
            if let Some(dialog) = dialog.get() && dialog.show_modal().is_ok() {
                open.set(true);
            }
        }>"Bibliothèque d’assets"</button>
        <dialog node_ref=dialog class="asset-library-dialog" aria-label="Recherche d’assets"
            on:close=move |_| open.set(false)
            on:keydown=move |event| event.stop_propagation()
            on:mousedown=move |event| event.stop_propagation()
            on:mouseup=move |event| event.stop_propagation()>
            <Show when=move || open.get()><LibraryPageView close=close/></Show>
        </dialog>
    }
}

#[component]
fn LibraryPageView(close: Callback<()>) -> impl IntoView {
    let draft = RwSignal::new(String::new());
    let text = RwSignal::new(String::new());
    let pack = RwSignal::new(String::new());
    let family = RwSignal::new(String::new());
    let kind = RwSignal::new(String::new());
    let subtype = RwSignal::new(String::new());
    let availability = RwSignal::new(String::new());
    let status = RwSignal::new(String::new());
    let sort = RwSignal::new(String::new());
    let offset = RwSignal::new(0_usize);
    let reload = RwSignal::new(0_u64);
    let loaded_reload = RwSignal::new(0_u64);
    let generation = RwSignal::new(0_u64);
    let page = RwSignal::new(None::<LibraryPage>);
    let page_epoch = RwSignal::new(0_u64);
    let busy = RwSignal::new(false);
    let error = RwSignal::new(String::new());
    let selected = RwSignal::new(String::new());
    let detail = RwSignal::new(None::<LibraryDetail>);
    let detail_error = RwSignal::new(String::new());
    let detail_busy = RwSignal::new(false);
    // Any filter change starts from the first page. Pagination is not part of this effect.
    Effect::new(move |_| {
        let _ = (
            text.get(),
            pack.get(),
            family.get(),
            kind.get(),
            subtype.get(),
            availability.get(),
            status.get(),
            sort.get(),
        );
        offset.set(0);
    });
    Effect::new(move |_| {
        let requested_reload = reload.get();
        let query = LibraryQuery {
            text: text.get(),
            pack: pack.get(),
            family: family.get(),
            asset_type: kind.get(),
            subtype: subtype.get(),
            availability: availability.get(),
            status: status.get(),
            sort: sort.get(),
            offset: offset.get(),
            limit: 24,
            refresh: requested_reload != loaded_reload.get_untracked(),
        };
        loaded_reload.set(requested_reload);
        let ticket = generation.get_untracked().wrapping_add(1);
        generation.set(ticket);
        busy.set(true);
        error.set(String::new());
        selected.set(String::new());
        detail.set(None);
        wasm_bindgen_futures::spawn_local(async move {
            let result = read(&LibraryRequest::Search { query }).await;
            if generation.try_get_untracked() != Some(ticket) {
                return;
            }
            busy.set(false);
            match result {
                Ok(LibraryResponse::Page { page: value }) => {
                    page.set(Some(value));
                    page_epoch.set(ticket);
                }
                Ok(_) => {
                    page.set(None);
                    error.set("Réponse de recherche inattendue".into());
                }
                Err(reason) => {
                    page.set(None);
                    error.set(reason);
                }
            }
        });
    });
    Effect::new(move |_| {
        let id = selected.get();
        detail.set(None);
        detail_error.set(String::new());
        if id.is_empty() {
            detail_busy.set(false);
            return;
        }
        let Some(current) = page.get_untracked() else {
            return;
        };
        detail_busy.set(true);
        let request_generation = generation.get_untracked();
        wasm_bindgen_futures::spawn_local(async move {
            let result = read(&LibraryRequest::Details {
                id: id.clone(),
                revision: current.revision.clone(),
            })
            .await;
            if selected.try_get_untracked().as_ref() != Some(&id)
                || generation.try_get_untracked() != Some(request_generation)
            {
                return;
            }
            detail_busy.set(false);
            match result {
                Ok(LibraryResponse::Details { detail: value }) => detail.set(Some(*value)),
                Ok(_) => detail_error.set("Fiche inattendue".into()),
                Err(reason) => detail_error.set(reason),
            }
        });
    });
    let reset = move |_| {
        draft.set(String::new());
        text.set(String::new());
        pack.set(String::new());
        family.set(String::new());
        kind.set(String::new());
        subtype.set(String::new());
        availability.set(String::new());
        status.set(String::new());
        sort.set(String::new());
        offset.set(0);
    };
    view! {
        <section class="asset-library-page">
            <header class="asset-library-header"><div><p class="asset-eyebrow">"VOTRE COLLECTION LOCALE"</p>
                <h1>"Bibliothèque d’assets"</h1><p>"Trouvez un objet, examinez ses dimensions et consultez sa provenance."</p></div>
                <button class="asset-library-close" on:click=move |_| close.run(())>"Fermer ×"</button></header>
            <form class="asset-library-search" on:submit=move |event| { event.prevent_default(); text.set(draft.get_untracked()); }>
                <label for="asset-search">"Nom, description, identifiant ou mot-clé"</label>
                <div><input id="asset-search" type="search" autofocus maxlength="256" placeholder="Ex. arbre, épée, chaise, hôpital…"
                    prop:value=move || draft.get() on:input=move |event| draft.set(event_target_value(&event))/>
                    <button type="submit" disabled=move || busy.get()>"Rechercher"</button>
                    <button type="button" on:click=move |_| reload.update(|n| *n=n.wrapping_add(1)) disabled=move || busy.get()>"Actualiser"</button></div>
            </form>
            <div class="asset-library-body">
                <aside class="asset-library-filters" aria-label="Filtres d’assets"><h2>"Affiner la recherche"</h2>
                    <Filter label="Pack" value=pack options=Signal::derive(move || page.get().map(|p|p.facets.packs).unwrap_or_default())/>
                    <Filter label="Famille" value=family options=Signal::derive(move || page.get().map(|p|p.facets.families).unwrap_or_default())/>
                    <Filter label="Type" value=kind options=Signal::derive(move || page.get().map(|p|p.facets.types).unwrap_or_default())/>
                    <Filter label="Sous-type" value=subtype options=Signal::derive(move || page.get().map(|p|p.facets.subtypes).unwrap_or_default())/>
                    <Filter label="Disponibilité" value=availability options=Signal::derive(move || page.get().map(|p|p.facets.availabilities).unwrap_or_default())/>
                    <label>"Vérification"<select prop:value=move ||status.get() on:change=move |e|status.set(event_target_value(&e))>
                        <option value="">"Tous les états"</option><option value="metadata_ready">"Fiche complète"</option>
                        <option value="needs_review">"À compléter / vérifier"</option><option value="runtime_qualified">"Qualifié dans le moteur"</option>
                    </select></label>
                    <label>"Trier par"<select prop:value=move ||sort.get() on:change=move |e|sort.set(event_target_value(&e))>
                        <option value="">"Nom A → Z"</option><option value="triangles">"Nombre de triangles"</option><option value="height">"Hauteur"</option>
                    </select></label>
                    <button on:click=reset>"Réinitialiser les filtres"</button>
                    <p class="asset-library-note">"Les variantes et niveaux de détail peuvent apparaître séparément. La recherche couvre les assets déjà indexés."</p>
                    <details><summary>"Emplacement de la collection"</summary><p class="asset-path">{move ||page.get().map(|p|p.vault_path).unwrap_or_default()}</p></details>
                </aside>
                <section class="asset-library-results" aria-label="Résultats de recherche" aria-busy=move ||busy.get().to_string()>
                    <div class="asset-results-heading"><h2>{move || page.get().map_or_else(||"Collection".into(), |p|format!("{} résultats",p.matched_assets))}</h2>
                        <span>{move ||page.get().map(|p|format!("sur {} assets indexés",p.total_assets)).unwrap_or_default()}</span></div>
                    <Show when=move ||busy.get()><p role="status">"Recherche dans votre bibliothèque…"</p></Show>
                    <Show when=move ||!error.get().is_empty()><p class="asset-error" role="alert">{move ||error.get()}</p></Show>
                    <Show when=move ||!busy.get() && page.get().is_some_and(|p|p.matched_assets==0)>
                        <div class="asset-empty"><h3>"Aucun asset ne correspond"</h3><p>"Essayez un autre mot ou retirez un filtre."</p></div>
                    </Show>
                    <div class="asset-library-grid">
                    <For each=move || page.get().map(|p|p.assets.into_iter().map(|a|(a,p.revision.clone(),page_epoch.get())).collect::<Vec<_>>()).unwrap_or_default()
                        key=|(a,revision,epoch)|(a.id.clone(),revision.clone(),*epoch) children=move |(asset,revision,_)| {
                            let id=asset.id.clone(); let active=id.clone(); let image_id=id.clone();
                            let source_support=asset.availability=="source_support";
                            let dimensions_label=if source_support {
                                if asset.dimensions.iter().all(|value| *value > 0.0) {
                                    format!("Dimensions source : {}", dimensions(asset.dimensions))
                                } else {"Dimensions non mesurées".into()}
                            } else {dimensions(asset.dimensions)};
                            let triangles_label=if source_support {
                                if asset.triangles > 0 {format!("{} triangles source",asset.triangles)}
                                else {"Triangles non mesurés".into()}
                            } else {format!("{} triangles",asset.triangles)};
                            let status_label=if source_support {"Source conservée — non plaçable"}else if asset.metadata_ready {"Fiche complète"}else{"Fiche à compléter"};
                            view! { <button class="asset-result-card" disabled=move ||busy.get()
                                aria-pressed=move ||(selected.get()==active).to_string() on:click=move |_|selected.set(id.clone())>
                                <Thumbnail id=image_id revision=revision available=asset.has_thumbnail/>
                                <div class="asset-card-copy"><strong>{asset.name}</strong><small>{asset.subtype}</small>
                                    <span>{dimensions_label}</span><span>{triangles_label}</span>
                                    <span class="asset-card-status">{status_label}</span></div>
                            </button> }
                        }/>
                    </div>
                    <nav class="asset-pagination" aria-label="Pages de résultats">
                        <button disabled=move ||busy.get() || offset.get()==0 on:click=move |_|offset.update(|n|*n=n.saturating_sub(24))>"← Précédente"</button>
                        <span>{move ||page.get().map(|p|if p.matched_assets==0 {"0 résultat".into()}else{format!("{}–{} / {}",p.offset+1,(p.offset+p.assets.len()).min(p.matched_assets),p.matched_assets)}).unwrap_or_default()}</span>
                        <button disabled=move ||busy.get() || page.get().is_none_or(|p|p.offset+p.assets.len()>=p.matched_assets)
                            on:click=move |_|offset.update(|n|*n+=24)>"Suivante →"</button>
                    </nav>
                </section>
                <aside class="asset-library-inspector" aria-label="Fiche de l’asset sélectionné">
                    <Show when=move ||selected.get().is_empty()><div class="asset-empty"><h2>"Examinez un asset"</h2><p>"Sélectionnez une vignette pour afficher sa fiche et ses informations de placement."</p></div></Show>
                    <Show when=move ||detail_busy.get()><p role="status">"Chargement de la fiche…"</p></Show>
                    <Show when=move ||!detail_error.get().is_empty()><p class="asset-error" role="alert">{move ||detail_error.get()}</p></Show>
                    {move ||detail.get().map(|value| {
                        let revision = page.get_untracked().map(|p|p.revision).unwrap_or_default();
                        view!{
                            <crate::authoring_assets::vault::VaultActions card=value.card.clone() revision=revision.clone() profile=value.profile.clone() selected=selected close=close/>
                            <crate::authoring_assets::vault::SourcePhysicsDetails id=value.card.id.clone() revision=revision.clone() profile=value.profile.clone()/>
                            <AssetDetails value=value revision=revision/>
                        }
                    })}
                </aside>
            </div>
        </section>
    }
}

#[component]
fn Filter(
    label: &'static str,
    value: RwSignal<String>,
    options: Signal<Vec<String>>,
) -> impl IntoView {
    view! { <label>{label}<select prop:value=move ||value.get() on:change=move |e|value.set(event_target_value(&e))>
        <option value="">"Tous"</option>{move ||options.get().into_iter().map(|v|view!{<option value=v.clone()>{filter_label(&v)}</option>}).collect_view()}
    </select></label> }
}

#[component]
fn Thumbnail(id: String, revision: String, available: bool) -> impl IntoView {
    let url = RwSignal::new(String::new());
    let error = RwSignal::new(String::new());
    on_cleanup(move || {
        if let Some(value) = url.try_get_untracked() {
            let _ = web_sys::Url::revoke_object_url(&value);
        }
    });
    if available {
        wasm_bindgen_futures::spawn_local(async move {
            let result = read(&LibraryRequest::Thumbnail { id, revision })
                .await
                .and_then(|response| {
                    let LibraryResponse::Thumbnail { bytes } = response else {
                        return Err("Vignette inattendue".into());
                    };
                    let parts = js_sys::Array::new();
                    parts.push(&js_sys::Uint8Array::from(bytes.as_slice()));
                    let blob = web_sys::Blob::new_with_u8_array_sequence(&parts)
                        .map_err(|_| "Image illisible".to_owned())?;
                    web_sys::Url::create_object_url_with_blob(&blob)
                        .map_err(|_| "Image indisponible".to_owned())
                });
            match result {
                Ok(value) => {
                    if url.try_set(value.clone()).is_some() {
                        let _ = web_sys::Url::revoke_object_url(&value);
                    }
                }
                Err(reason) => {
                    let _ = error.try_set(reason);
                }
            }
        });
    }
    view! {<div class="asset-thumbnail">
        <Show when=move ||!url.get().is_empty() fallback=move ||view!{<span title=move ||error.get()>{if !available {"Sans vignette"}else if error.get().is_empty(){"Chargement…"}else{"Vignette indisponible"}}</span>}>
            <img src=move ||url.get() alt="Aperçu de l’asset"/>
        </Show>
    </div>}
}

fn dimensions(d: [f64; 3]) -> String {
    format!("{:.2} × {:.2} × {:.2} m", d[0], d[1], d[2])
}
fn filter_label(value: &str) -> String {
    match value {
        "props" => "Décors et objets".into(),
        "equipment" => "Équipement".into(),
        "environment" => "Environnement".into(),
        "character" => "Personnage".into(),
        "weapon" => "Arme".into(),
        "converted_model" => "Modèle GLB utilisable".into(),
        "source_support" => "Source conservée".into(),
        "generated-review-required" => "Généré — revue requise".into(),
        _ => value
            .trim_start_matches("hivemind.")
            .replace(['-', '_'], " "),
    }
}
fn field(v: &serde_json::Value, pointer: &str) -> String {
    match v.pointer(pointer) {
        None | Some(serde_json::Value::Null) => "Non renseigné".into(),
        Some(serde_json::Value::String(s)) => filter_label(s),
        Some(serde_json::Value::Array(values)) if values.is_empty() => "Aucun renseigné".into(),
        Some(serde_json::Value::Array(values))
            if values.iter().all(serde_json::Value::is_string) =>
        {
            values
                .iter()
                .filter_map(serde_json::Value::as_str)
                .map(filter_label)
                .collect::<Vec<_>>()
                .join(", ")
        }
        Some(other) => other.to_string(),
    }
}

fn coordinates(value: Option<&serde_json::Value>) -> String {
    let Some(value) = value else {
        return "Non renseigné".into();
    };
    let axes = ["x", "y", "z"];
    let values: Option<Vec<f64>> = axes
        .iter()
        .enumerate()
        .map(|(index, axis)| {
            value
                .get(*axis)
                .or_else(|| value.get(index))
                .and_then(serde_json::Value::as_f64)
        })
        .collect();
    values.map_or_else(
        || "Non renseigné".into(),
        |v| format!("X {:.3} · Y {:.3} · Z {:.3} m", v[0], v[1], v[2]),
    )
}

fn anchors_text(details: &serde_json::Value) -> String {
    let Some(anchors) = details
        .get("anchors")
        .and_then(serde_json::Value::as_array)
        .filter(|a| !a.is_empty())
    else {
        return "Aucun renseigné".into();
    };
    anchors
        .iter()
        .map(|anchor| {
            let name = anchor
                .get("name")
                .and_then(serde_json::Value::as_str)
                .unwrap_or("Ancre");
            let position = coordinates(anchor.get("position_meters"));
            let review = if anchor
                .get("requires_review")
                .and_then(serde_json::Value::as_bool)
                .unwrap_or(true)
            {
                "À vérifier, orientation non qualifiée"
            } else {
                "Voir la preuve de qualification"
            };
            format!("{name}\n{position}\n{review}")
        })
        .collect::<Vec<_>>()
        .join("\n\n")
}

#[component]
fn AssetDetails(value: LibraryDetail, revision: String) -> impl IntoView {
    let d = &value.details;
    let p = &value.profile;
    let source_support = value.card.availability == "source_support";
    let dimensions_text = if source_support {
        if value.card.dimensions.iter().all(|value| *value > 0.0) {
            format!(
                "{} (mesure source, sans dérivé Forge)",
                dimensions(value.card.dimensions)
            )
        } else {
            "Non mesurées dans la source".into()
        }
    } else {
        dimensions(value.card.dimensions)
    };
    let measures = vec![
        ("Dimensions (X × Y × Z)", dimensions_text),
        (
            "Triangles",
            if source_support {
                if value.card.triangles > 0 {
                    format!("{} (mesure source)", value.card.triangles)
                } else {
                    "Non mesurés dans la source".into()
                }
            } else {
                value.card.triangles.to_string()
            },
        ),
        ("Sommets", field(d, "/measurement/vertices")),
        ("Matériaux", field(d, "/measurement/materials")),
        (
            "Origine conservée",
            coordinates(d.pointer("/origin/position_meters")),
        ),
        (
            "Pivot suggéré (m, à vérifier)",
            coordinates(p.pointer("/placement/suggested_pivot/position_meters")),
        ),
        ("Points d’attache", anchors_text(d)),
        ("Placement", field(p, "/placement/surface")),
        ("Éléments non configurés", field(d, "/not_configured")),
        ("Métadonnées manquantes", field(d, "/missing")),
        ("Revue", field(p, "/review_status")),
        (
            "Confiance du profil",
            p.get("confidence")
                .and_then(serde_json::Value::as_f64)
                .map_or_else(
                    || "Non renseignée".into(),
                    |n| format!("{:.0} %", n * 100.0),
                ),
        ),
    ];
    let mut statuses = vec![
        (
            "Géométrie mesurée",
            d.pointer("/readiness/geometry_measured")
                .and_then(serde_json::Value::as_bool)
                .unwrap_or(false),
        ),
        (
            "glTF validé",
            d.pointer("/readiness/gltf_validated")
                .and_then(serde_json::Value::as_bool)
                .unwrap_or(false),
        ),
        (
            "Placement qualifié",
            d.pointer("/readiness/placement_qualified")
                .and_then(serde_json::Value::as_bool)
                .unwrap_or(false),
        ),
        ("Moteur qualifié", value.card.runtime_qualified),
    ];
    if source_support {
        statuses.insert(
            0,
            (
                "Source Unity conservée et vérifiée",
                d.pointer("/readiness/source_preserved")
                    .and_then(serde_json::Value::as_bool)
                    .unwrap_or(false),
            ),
        );
    }
    let model_path = value
        .model_path
        .clone()
        .unwrap_or_else(|| "Aucun GLB : entrée de support source".into());
    let source_path = value
        .source_path
        .clone()
        .unwrap_or_else(|| "Voir la provenance détaillée ci-dessous".into());
    let qualification_note = if source_support {
        "Cette entrée conserve une source Unity ou une dépendance. Elle reste consultable et filtrable, mais ne constitue pas un objet plaçable tant qu’un dérivé fidèle n’est pas disponible."
    } else {
        "Une fiche complète ne garantit pas le placement ou le fonctionnement dans le moteur. Les propositions de pivot et d’ancrage restent à vérifier."
    };
    let raw=serde_json::to_string_pretty(&serde_json::json!({"details":value.details,"profile":value.profile,"catalog_asset":value.catalog_asset})).unwrap_or_default();
    view! {<article class="asset-detail">
        <Thumbnail id=value.card.id.clone() revision=revision available=value.card.has_thumbnail/>
        <p class="asset-eyebrow">{value.card.subtype.clone()}</p><h2>{value.card.name}</h2><p>{value.card.description}</p>
        <div class="asset-tags">{value.card.tags.into_iter().map(|tag|view!{<span>{tag}</span>}).collect_view()}</div>
        <h3>"Dimensions et placement"</h3><dl>{measures.into_iter().map(|(label,text)|view!{<dt>{label}</dt><dd>{text}</dd>}).collect_view()}</dl>
        <h3>"Vérifications enregistrées"</h3><ul>{statuses.into_iter().map(|(label,passed)|view!{<li>{format!("{} : {}",label,if passed{"validé"}else{"non qualifié"})}</li>}).collect_view()}</ul>
        <p class="asset-library-note">{qualification_note}</p>
        <h3>"Provenance et fichiers"</h3><dl><dt>"Pack"</dt><dd>{value.card.pack}</dd><dt>"Licence"</dt><dd>{value.license}</dd>
            <dt>"Identifiant"</dt><dd>{value.card.id}</dd><dt>"Fichier GLB"</dt><dd>{model_path}</dd>
            <dt>"Source conservée"</dt><dd>{source_path}</dd>
            <dt>"Vignette"</dt><dd>{value.preview_path.unwrap_or_else(||"Non disponible".into())}</dd></dl>
        <p class="asset-library-note">"Collection privée locale. Aucun asset n’est copié dans votre module par cette consultation."</p>
        <details><summary>"Toutes les métadonnées et preuves"</summary><pre>{raw}</pre></details>
    </article>}
}
