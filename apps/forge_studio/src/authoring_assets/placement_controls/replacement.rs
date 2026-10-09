//! Reviewed pin replacement, never an implicit reload of authored placements.
use super::{PlacementEdit, SendCommand, edit, head};
use forge_assets::asset_browser::AssetBrowser;
use forge_core::EntityId;
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;

#[component]
pub(super) fn ReplacementControls(
    state: RwSignal<Option<AuthoringState>>,
    id: EntityId,
    locked: bool,
    catalog: RwSignal<Option<AssetBrowser>>,
    selected: RwSignal<String>,
    busy: RwSignal<bool>,
    on_command: SendCommand,
) -> impl IntoView {
    let matching = RwSignal::new(false);
    view! {
        <details><summary>"Remplacer la référence d’asset"</summary>
            <p>"Choisir le nouvel asset dans le catalogue. Position, rotation, échelle, nom et identifiant restent inchangés. Aucun ajustement automatique du pivot ou des dimensions."</p>
            <label><input type="checkbox" prop:checked=move || matching.get()
                on:change=move |ev| matching.set(event_target_checked(&ev))/>
                "Inclure les objets de cette zone portant exactement la même référence (256 maximum)"</label>
            <output aria-live="polite">{move || {
                let include_matching = matching.get();
                let target = selected.get();
                state.with(|state| catalog.with(|catalog| {
                    replacement_scope(state.as_ref(), catalog.as_ref(), id, &target, include_matching)
                        .unwrap_or_else(|error| error)
                }))
            }}</output>
            <p>"Un objet verrouillé fait refuser tout le remplacement. Examiner l’aperçu avant Appliquer ; une seule annulation restaure les références précédentes."</p>
            <button disabled=locked on:click=move |_| {
                let setup = (|| {
                    let original = head(state.get_untracked())?;
                    let placements = super::super::objects::matching(&original,id)?;
                    let source = placements.iter().find(|p| p.id == id).ok_or("Objet absent")?;
                    let ids = placements.iter().filter(|p| p.id == id || (matching.get_untracked()
                        && p.asset_id == source.asset_id && p.asset_hash == source.asset_hash))
                        .map(|p| p.id).collect();
                    let name = selected.get_untracked();
                    let browser = super::super::catalog::resolve(catalog.get_untracked().as_ref(), &name)?;
                    let reference = browser.reference();
                    let request = edit(state.get_untracked(), PlacementEdit::ReplaceAsset {
                        ids, expected_asset_id: source.asset_id, expected_asset_hash: source.asset_hash,
                        asset_id: reference.0, asset_hash: reference.1,
                    })?;
                    Ok::<_, String>((original.content_hash()?, browser, name, request))
                })();
                let (hash, browser, name, request) = match setup {
                    Ok(value) => value,
                    Err(error) => { on_command.run(Err(error)); return; }
                };
                super::super::cancel_preview();
                let ticket = super::super::preview::ticket();
                busy.set(true);
                wasm_bindgen_futures::spawn_local(async move {
                    let checked = browser.check().await;
                    busy.set(false);
                    if !super::super::preview::is_current(ticket) || selected.get_untracked() != name { return; }
                    let result = checked.and_then(|_| {
                        browser.still_available()?;
                        if head(state.get_untracked())?.content_hash()? != hash {
                            return Err("Zone modifiée ; recommencer l’examen du remplacement".into());
                        }
                        Ok(request)
                    });
                    on_command.run(result);
                });
            }>"Prévisualiser le remplacement par l’asset du catalogue"</button>
        </details>
    }
}

/// Read-only explanation; the domain command remains the validation authority.
fn replacement_scope(
    state: Option<&AuthoringState>,
    catalog: Option<&AssetBrowser>,
    id: EntityId,
    target: &str,
    include_matching: bool,
) -> Result<String, String> {
    let head = state
        .and_then(|state| state.zone_head.as_ref())
        .ok_or("Ouvrir une zone contenant des objets")?;
    let placements = super::super::objects::matching(head, id)?;
    let source = placements
        .iter()
        .find(|p| p.id == id)
        .ok_or("Objet absent")?;
    let reference = super::super::catalog::resolve(catalog, target)?.reference();
    let source_name = super::super::catalog::entries(catalog)
        .into_iter()
        .find(|entry| entry.reference == (source.asset_id, source.asset_hash))
        .map_or_else(|| source.asset_id.to_string(), |entry| entry.label);
    let (count, locked) = placements
        .iter()
        .filter(|p| {
            p.id == id
                || (include_matching
                    && p.asset_id == source.asset_id
                    && p.asset_hash == source.asset_hash)
        })
        .fold((0, 0), |(count, locked), p| {
            (count + 1, locked + usize::from(p.locked))
        });
    let warning = if reference == (source.asset_id, source.asset_hash) {
        " · Choisir une référence différente : cible déjà utilisée."
    } else if locked > 0 {
        " · Remplacement refusé tant que ces objets restent verrouillés."
    } else if count > 256 {
        " · Lot supérieur à 256 : limiter le remplacement à un objet."
    } else {
        " · Aucun changement avant examen puis application."
    };
    Ok(format!(
        "Source : {source_name} (SHA-256 {}). Cible : {target} (SHA-256 {}). {count} objet(s) concerné(s), {locked} verrouillé(s).{warning}",
        source.asset_hash, reference.1
    ))
}
