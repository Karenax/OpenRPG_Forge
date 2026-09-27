use forge_assets::{GovernedAssetKitBuildReport, VegetationPrototypeCatalog};
use forge_core::{CommandId, canonical_json_sha256};
use forge_project::{
    VegetationKind,
    authoring::{AuthoringRequest, AuthoringState},
    zone_authoring::{
        session::{ZoneAction, ZoneRequest},
        species::{LayerSpecies, SpeciesReference},
    },
};
use leptos::prelude::*;

#[component]
pub(crate) fn SpeciesControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    on_command: Callback<Result<AuthoringRequest, String>>,
) -> impl IntoView {
    let catalog = RwSignal::new(None::<(VegetationPrototypeCatalog, GovernedAssetKitBuildReport)>);
    let draft = RwSignal::new(Vec::<LayerSpecies>::new());
    let loading = RwSignal::new(false);
    let message =
        RwSignal::new("Charger le catalogue local pour choisir les espèces d'arbres.".to_owned());
    let accepted = Memo::new(move |_| {
        state
            .get()
            .and_then(|s| s.zone_head)
            .and_then(|h| h.content_hash().ok())
    });
    Effect::new(move |_| {
        let _ = accepted.get();
        draft.set(
            state
                .get_untracked()
                .and_then(|s| s.zone_head)
                .and_then(|h| h.corrections.species)
                .unwrap_or_default(),
        );
    });
    view! {
        <details><summary>"Espèces d’arbres"</summary>
            <p>"Choisir au moins une espèce pour chaque couche arborée. Plusieurs choix produisent un mélange stable. Décocher tout revient aux arbres procéduraux. Les poses, masques et règles de répartition restent inchangés."</p>
            <fieldset disabled=move || busy.get() || loading.get() || state.get().is_none_or(|s| s.playtest_hash.is_some() || s.zone_head.is_none())>
                <button on:click=move |_| {
                    loading.set(true);
                    wasm_bindgen_futures::spawn_local(async move {
                        match load().await {
                            Ok(value) => { catalog.set(Some(value)); message.set("Catalogue disponible ; examiner les choix avant application.".into()); }
                            Err(error) => message.set(format!("Catalogue indisponible : {error}")),
                        }
                        loading.set(false);
                    });
                }>"Charger le catalogue d’espèces"</button>
                {move || {
                    let Some((source, _)) = catalog.get() else { return Vec::new(); };
                    let Ok(hash) = canonical_json_sha256(&source) else { return Vec::new(); };
                    let layers = state.get().and_then(|s| s.zone_head).and_then(|h| h.base.vegetation).map_or_else(Vec::new, |v| v.layers);
                    layers.into_iter().filter(|l| l.kind == VegetationKind::Tree).map(|layer| {
                        let id = layer.id;
                        let options = source.bindings.iter().map(|binding| {
                            let target = id.clone();
                            let reference = SpeciesReference { catalog_sha256: hash, source_layer_id: forge_project::VegetationLayerId::new(&binding.layer_id), asset_id: binding.asset_id.clone() };
                            let checked_ref = reference.clone();
                            let checked_id = id.clone();
                            let label = format!("{} · hauteur {} m", binding.asset_id, binding.target_height_m);
                            view! { <label><input type="checkbox" prop:checked=move || draft.with(|d| d.iter().any(|l| l.layer_id == checked_id && l.prototypes.contains(&checked_ref)))
                                on:change=move |_| {
                                    crate::authoring_shortcuts::cancel_preview();
                                    draft.update(|d| toggle(d, target.clone(), reference.clone()));
                                }/>{label}</label> }
                        }).collect_view();
                        view! { <fieldset><legend>{layer.display_name}</legend>{options}
                            <button on:click=move |_| {
                                let result = spread_to_tree_layers(state.get_untracked(), &draft.get_untracked(), &id);
                                match result {
                                    Ok(choices) => {
                                        crate::authoring_shortcuts::cancel_preview();
                                        draft.set(choices);
                                        message.set("Mélange repris sur les couches arborées, sans application. Examiner les espèces choisies.".into());
                                    }
                                    Err(error) => message.set(error),
                                }
                            }>"Reprendre ce mélange sur toutes les couches d’arbres"</button>
                        </fieldset> }
                    }).collect::<Vec<_>>()
                }}
                <button disabled=move || catalog.get().is_none() && !draft.get().is_empty() on:click=move |_| {
                    let result = prepare(state.get_untracked(), draft.get_untracked(), catalog.get_untracked());
                    on_command.run(result);
                }>"Examiner les espèces choisies"</button>
                <button on:click=move |_| { crate::authoring_shortcuts::cancel_preview(); draft.set(Vec::new()); }>"Effacer tous les choix (non appliqué)"</button>
                <button disabled=move || catalog.get().is_none() on:click=move |_| {
                    let Some(head) = state.get_untracked().and_then(|s| s.zone_head) else { return; };
                    let Ok(hash) = head.content_hash() else { return; };
                    message.set("Cliquer un arbre individuel visible pour examiner sa conversion en objet retouchable.".into());
                    super::selection::arm(hash, Callback::new(move |selection: Result<forge_engine::TreePromotionSelection, String>| {
                        let result = (|| {
                            let selection = selection?;
                            let current = state.get_untracked().ok_or("Session absente")?;
                            if current.playtest_hash.is_some() || busy.get_untracked() { return Err("Édition indisponible".into()); }
                            let head = current.zone_head.ok_or("Zone absente")?;
                            let (source, kit) = catalog.get_untracked().ok_or("Catalogue absent")?;
                            let browser = forge_assets::asset_browser::AssetBrowser::new(kit).map_err(|e| e.to_string())?;
                            let key = format!("promotion:{}:{}:{}", selection.head_hash, selection.layer_id, selection.instance_id);
                            forge_engine::prepare_tree_promotion(&head, &selection, &[source], &browser,
                                forge_core::EntityId::deterministic(&key), "Arbre promu".into(), CommandId::deterministic(&key))
                                .map(|request| AuthoringRequest::PreviewZone { request })
                        })();
                        on_command.run(result);
                    }));
                }>"Convertir un arbre de la vue en objet retouchable"</button>
                <output>{move || draft.get().iter().map(|layer| format!("{} : {}", layer.layer_id, layer.prototypes.iter().map(|p| p.asset_id.as_str()).collect::<Vec<_>>().join(", "))).collect::<Vec<_>>().join(" ; ")}</output>
            </fieldset>
            <output aria-live="polite">{move || message.get()}</output>
        </details>
    }
}

async fn load() -> Result<(VegetationPrototypeCatalog, GovernedAssetKitBuildReport), String> {
    let fetch = crate::governed_kit::transport::fetch;
    let kit: GovernedAssetKitBuildReport =
        serde_json::from_slice(&fetch("build.json", 1024 * 1024).await?)
            .map_err(|e| e.to_string())?;
    let catalog: VegetationPrototypeCatalog =
        serde_json::from_slice(&fetch("prototype-catalog.json", 1024 * 1024).await?)
            .map_err(|e| e.to_string())?;
    let ids: std::collections::BTreeSet<_> = catalog
        .bindings
        .iter()
        .map(|b| b.layer_id.as_str())
        .collect();
    catalog
        .validate_against(
            catalog.vegetation_sha256,
            &ids.into_iter().collect::<Vec<_>>(),
            &kit,
        )
        .map_err(|e| e.to_string())?;
    Ok((catalog, kit))
}

fn toggle(
    draft: &mut Vec<LayerSpecies>,
    id: forge_project::VegetationLayerId,
    reference: SpeciesReference,
) {
    if let Some(layer) = draft.iter_mut().find(|l| l.layer_id == id) {
        if layer.prototypes.contains(&reference) {
            layer.prototypes.retain(|r| r != &reference);
        } else {
            layer
                .prototypes
                .retain(|r| r.asset_id != reference.asset_id);
            layer.prototypes.push(reference);
        }
    } else {
        draft.push(LayerSpecies {
            layer_id: id,
            prototypes: vec![reference],
        });
    }
    draft.retain(|l| !l.prototypes.is_empty());
    draft.sort_by(|a, b| a.layer_id.cmp(&b.layer_id));
    for layer in draft {
        layer.prototypes.sort_by(|a, b| a.asset_id.cmp(&b.asset_id));
    }
}

fn prepare(
    state: Option<AuthoringState>,
    choices: Vec<LayerSpecies>,
    catalog: Option<(VegetationPrototypeCatalog, GovernedAssetKitBuildReport)>,
) -> Result<AuthoringRequest, String> {
    let head = state.and_then(|s| s.zone_head).ok_or("Zone absente")?;
    if head.corrections.species.as_deref().unwrap_or_default() == choices {
        return Err("Choix inchangés".into());
    }
    let mut candidate = head.clone();
    candidate.corrections.species = Some(choices);
    candidate.version = candidate.corrections.schema_version();
    candidate.validate()?;
    // Returning to procedural trees needs no external catalog or GLB. In
    // particular, a missing kit must not trap the author in a broken selection.
    if candidate
        .corrections
        .species
        .as_ref()
        .is_some_and(|choices| !choices.is_empty())
    {
        let (source, kit) = catalog.ok_or("Catalogue absent")?;
        forge_engine::resolve_authored_species(&candidate, &[source], &kit)?;
    }
    let hash = head.content_hash()?;
    let id = canonical_json_sha256(&(hash, &candidate.corrections.species))
        .map_err(|e| e.to_string())?;
    Ok(AuthoringRequest::PreviewZone {
        request: ZoneRequest {
            command_id: CommandId::deterministic(format!("species:{id}")),
            expected_revision: head.revision,
            expected_hash: hash,
            action: ZoneAction::edit(candidate.corrections),
        },
    })
}

fn spread_to_tree_layers(
    state: Option<AuthoringState>,
    draft: &[LayerSpecies],
    source: &forge_project::VegetationLayerId,
) -> Result<Vec<LayerSpecies>, String> {
    let head = state.and_then(|s| s.zone_head).ok_or("Zone absente")?;
    forge_project::zone_authoring::species::spread_to_tree_layers(&head, draft, source)
}
