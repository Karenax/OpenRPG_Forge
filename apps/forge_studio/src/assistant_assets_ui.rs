//! Local search; only checked references become part of the provider draft.
use crate::assistant_asset_protocol::{AssetQuery, AssetSearchPage, AssetSelection};
use leptos::prelude::*;
use std::collections::BTreeMap;
use wasm_bindgen::JsValue;

#[component]
pub(crate) fn AssetReferences(selection: RwSignal<Option<AssetSelection>>) -> impl IntoView {
    let text = RwSignal::new(String::new());
    let page = RwSignal::new(None::<AssetSearchPage>);
    let busy = RwSignal::new(false);
    let status = RwSignal::new(String::new());
    let diagnostic = RwSignal::new(String::new());
    let names = RwSignal::new(BTreeMap::<String, String>::new());
    Effect::new(move |_| {
        if selection.get().is_none() {
            names.set(BTreeMap::new());
        }
    });
    let search = move |offset: usize| {
        if busy.get_untracked() {
            return;
        }
        busy.set(true);
        page.set(None);
        status.set("Recherche dans la bibliothèque…".into());
        diagnostic.set(String::new());
        let query = AssetQuery {
            text: text.get_untracked(),
            pack: String::new(),
            offset,
        };
        wasm_bindgen_futures::spawn_local(async move {
            let response = invoke(&query).await;
            let _ = busy.try_set(false);
            match response {
                Ok(result) => {
                    if selection
                        .get_untracked()
                        .is_some_and(|selected| selected.revision != result.revision)
                    {
                        selection.set(None);
                        names.set(BTreeMap::new());
                    }
                    let _ = status.try_set(if result.assets.is_empty() {
                        "Aucune référence trouvée. Essayer un autre nom.".into()
                    } else {
                        format!(
                            "{} référence(s) trouvée(s). Cocher celles à joindre au plan.",
                            result.matched
                        )
                    });
                    let _ = page.try_set(Some(result));
                }
                Err(error) => {
                    let _ = status.try_set(
                        "Recherche indisponible. Vérifier la bibliothèque puis réessayer.".into(),
                    );
                    let _ = diagnostic.try_set(error);
                }
            }
        });
    };
    view! {<details class="assistant-asset-references"><summary>"Références de la bibliothèque pour le plan"</summary>
        <p>"La recherche reste locale. Seules les références cochées seront jointes à l’envoi au fournisseur."</p>
        <label>"Rechercher des références"<input type="search" maxlength="256" prop:value=move||text.get() on:input=move|event|text.set(event_target_value(&event)) on:keydown=move|event|if event.key()=="Enter" {event.prevent_default();search(0);}/></label>
        <button type="button" disabled=move||busy.get() on:click=move |_|search(0)>"Rechercher dans la bibliothèque"</button>
        <p role="status">{move||status.get()}</p>
        <p>{move||selection.get().map(|selection|format!("{} référence(s) sélectionnée(s) sur 16 maximum. Les modèles utilisables seront vérifiés avant l’envoi.",selection.ids.len()))}</p>
        {move||page.get().map(|results|{
            let revision=results.revision;
            results.assets.into_iter().map(move|entry|{
                let id=entry.library_id;let checked_id=id.clone();let available_id=id.clone();let name=entry.asset.name.clone();
                let availability=if entry.asset.model_available() {if entry.asset.metadata_ready {"Modèle disponible"}else{"Modèle à revoir"}}else{"Source conservée · conversion nécessaire"};
                let license=if entry.asset.license.is_empty(){"Droits non renseignés".into()}else{entry.asset.license.chars().take(160).collect::<String>()};
                view! {<div class="assistant-asset-reference"><label><input type="checkbox" disabled=move||selection.get().is_some_and(|selected|selected.revision==revision && selected.ids.len()>=16 && !selected.ids.contains(&available_id)) prop:checked=move||selection.get().is_some_and(|selected|selected.revision==revision && selected.ids.contains(&checked_id)) on:change=move|event|{
                    let previous=selection.get_untracked().filter(|selected|selected.revision==revision);
                    let allow_placement=previous.as_ref().is_some_and(|selected|selected.allow_placement);
                    let mut ids=previous.map_or_else(Vec::new,|selected|selected.ids);
                    if event_target_checked(&event){if !ids.contains(&id) && ids.len()<16 {ids.push(id.clone());names.update(|names|{names.insert(id.clone(),name.clone());});}}else{ids.retain(|selected|selected!=&id);names.update(|names|{names.remove(&id);});}
                    ids.sort();selection.set((!ids.is_empty()).then_some(AssetSelection{revision,ids,allow_placement}));
                }/>{entry.asset.name}</label><p>{format!("{} · {} · {}",entry.asset.pack,availability,license)}</p></div>}
            }).collect_view()
        })}
        <ul>{move||selection.get().map(|selected|selected.ids.into_iter().map(move|id|{
            let label=names.get().get(&id).cloned().unwrap_or_else(||"Référence mémorisée".into());
            view!{<li>{label}<button type="button" on:click=move |_|{
                if let Some(mut selected)=selection.get_untracked(){selected.ids.retain(|value|value!=&id);selection.set((!selected.ids.is_empty()).then_some(selected));names.update(|names|{names.remove(&id);});}
            }>"Retirer cette référence"</button></li>}
        }).collect_view())}</ul>
        <button type="button" disabled=move||busy.get() || page.get().is_none_or(|page|page.offset==0) on:click=move |_|if let Some(page)=page.get_untracked(){search(page.offset.saturating_sub(12));}>"Résultats précédents"</button>
        <button type="button" disabled=move||busy.get() || page.get().is_none_or(|page|page.offset+page.assets.len()>=page.matched) on:click=move |_|if let Some(page)=page.get_untracked(){search(page.offset+12);}>"Résultats suivants"</button>
        <button type="button" disabled=move||selection.get().is_none() on:click=move |_|selection.set(None)>"Retirer les références du plan"</button>
        <label><input type="checkbox" disabled=move||selection.get().is_none() prop:checked=move||selection.get().is_some_and(|selected|selected.allow_placement) on:change=move|event|selection.update(|selected|if let Some(selected)=selected {selected.allow_placement=event_target_checked(&event);})/>"Autoriser l’ajout de ces modèles dans la région choisie"</label>
        <Show when=move||!diagnostic.get().is_empty()><details><summary>"Diagnostics de la bibliothèque"</summary><pre style="white-space:pre-wrap;overflow-wrap:anywhere">{move||diagnostic.get()}</pre></details></Show>
    </details>}
}

async fn invoke(query: &AssetQuery) -> Result<AssetSearchPage, String> {
    let arguments = js_sys::Object::new();
    js_sys::Reflect::set(
        &arguments,
        &JsValue::from_str("request"),
        &JsValue::from_str(&serde_json::to_string(query).map_err(|error| error.to_string())?),
    )
    .map_err(|_| "Requête de bibliothèque impossible.")?;
    let result =
        crate::web::invoke_tauri_raw("search_assistant_assets", arguments.as_ref()).await?;
    serde_wasm_bindgen::from_value(result).map_err(|error| error.to_string())
}
