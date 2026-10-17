//! Authored RPG model bindings use the ordinary zone review and history controls.
use crate::asset_import_protocol::{ImportReadRole, ImportRequest, ImportResponse};
use forge_assets::governed_import::{GovernedAssetKind, GovernedImportAsset};
use forge_core::{AssetId, CommandId, EntityId, Transform};
use forge_project::{
    authoring::{AuthoringRequest, AuthoringState},
    module_gameplay_visuals::{GameplayVisualBinding, VisualAssetPin},
};
use leptos::prelude::*;

type Edit = Result<AuthoringRequest, String>;

pub(crate) fn preview_label(
    receipt: &forge_project::zone_authoring::session::ZoneReceipt,
) -> Option<String> {
    (receipt.applied.gameplay_visuals != receipt.inverse.gameplay_visuals).then(|| {
        format!(
            "Modèles de jeu proposés : {} → {}. Non appliqué ; Appliquer puis Sauvegarder. Le rendu sera vérifié dans le jeu séparé.",
            receipt.inverse.gameplay_visuals.as_ref().map_or(0, Vec::len),
            receipt.applied.gameplay_visuals.as_ref().map_or(0, Vec::len),
        )
    })
}

#[component]
pub(crate) fn GameplayVisualControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    send: Callback<Edit>,
) -> impl IntoView {
    let selected = RwSignal::new(String::new());
    let model = RwSignal::new(String::new());
    let models = RwSignal::new(Vec::<GovernedImportAsset>::new());
    let loading = RwSignal::new(false);
    let status = RwSignal::new(String::new());
    let disabled = move || {
        busy.get()
            || loading.get()
            || state.get().is_none_or(|s| {
                s.playtest_hash.is_some()
                    || s.zone_head.is_none_or(|h| h.corrections.gameplay.is_none())
            })
    };
    Effect::new(move |previous: Option<Option<forge_core::AreaId>>| {
        let area = state
            .get()
            .and_then(|s| s.zone_head)
            .map(|h| h.base.terrain.area_id);
        if previous != Some(area) {
            selected.set(String::new());
            model.set(String::new());
        }
        area
    });
    let targets = move || {
        state
            .get()
            .and_then(|s| s.zone_head)
            .and_then(|h| h.corrections.gameplay)
            .map(|d| {
                let player = d.player;
                d.actors
                    .into_iter()
                    .filter(move |a| a.id != player)
                    .map(|a| (a.id, a.name))
                    .chain(d.objects.into_iter().map(|o| (o.id, o.name)))
                    .collect::<Vec<_>>()
            })
            .unwrap_or_default()
    };
    let current = move || {
        let id = selected.get().parse::<EntityId>().ok()?;
        state
            .get()?
            .zone_head?
            .corrections
            .gameplay_visuals?
            .into_iter()
            .find(|b| b.entity_id == id)
            .map(|b| b.asset)
    };
    view! {
        <details class="authoring-gameplay-visuals"><summary>"Modèles des personnages et objets"</summary>
            <p>"Choisir un modèle importé pour un élément de jeu. Examiner puis appliquer en haut ; Annuler rétablit le modèle précédent."</p>
            <fieldset disabled=disabled>
                <label>"Personnage ou objet"<select prop:value=move ||selected.get() on:change=move|e|selected.set(event_target_value(&e))>
                    <option value="">"Choisir"</option>
                    {move || targets().into_iter().map(|(id,name)|view!{<option value=id.to_string()>{name}</option>}).collect_view()}
                </select></label>
                <p>{move ||if current().is_some() {"Un modèle est enregistré pour cet élément."} else {"Cet élément utilise sa forme de jeu par défaut."}}</p>
                <button on:click=move |_| refresh(models,loading,status)>"Actualiser mes modèles importés"</button>
                <label>"Modèle ou variante"<select prop:value=move||model.get() on:change=move|e|model.set(event_target_value(&e))>
                    <option value="">"Choisir un modèle importé"</option>
                    {move ||models.get().into_iter().map(|asset|view!{<option value=asset.id.to_string()>{asset.name}</option>}).collect_view()}
                </select></label>
                <button on:click=move |_|preview_model(state,models,model,loading,status)>"Voir le modèle seul"</button>
                <button on:click=move |_|crate::authoring_assets::cancel_preview()>"Revenir à la carte"</button>
                <button on:click=move |_|send.run(prepare(state.get_untracked(),&models.get_untracked(),&selected.get_untracked(),Some(&model.get_untracked())) )>"Examiner ce modèle"</button>
                <button disabled=move||current().is_none() on:click=move |_|send.run(prepare(state.get_untracked(),&models.get_untracked(),&selected.get_untracked(),None))>"Examiner le retrait du modèle"</button>
            </fieldset>
            <output aria-live="polite">{move ||status.get()}</output>
        </details>
    }
}

fn preview_model(
    state: RwSignal<Option<AuthoringState>>,
    models: RwSignal<Vec<GovernedImportAsset>>,
    model: RwSignal<String>,
    loading: RwSignal<bool>,
    status: RwSignal<String>,
) {
    let snapshot = (|| {
        let id = model
            .get_untracked()
            .parse::<AssetId>()
            .map_err(|_| "Choisir un modèle importé")?;
        let asset = models
            .get_untracked()
            .into_iter()
            .find(|a| a.id == id)
            .ok_or("Actualiser les modèles importés")?;
        let expected_head = state
            .get_untracked()
            .and_then(|s| s.zone_head)
            .ok_or("Ouvrir une zone")?
            .content_hash()?;
        Ok::<_, String>((asset, expected_head))
    })();
    let (asset, expected_head) = match snapshot {
        Ok(snapshot) => snapshot,
        Err(error) => {
            status.set(error);
            return;
        }
    };
    loading.set(true);
    status.set("Vérification du modèle…".into());
    wasm_bindgen_futures::spawn_local(async move {
        let response = crate::asset_import_ui::invoke(&ImportRequest::Read {
            asset_id: asset.id,
            sha256: asset.payload.sha256,
            role: ImportReadRole::Payload,
        })
        .await;
        let _ = loading.try_set(false);
        let result = (|| {
            if state
                .try_get_untracked()
                .flatten()
                .and_then(|s| s.zone_head)
                .and_then(|h| h.content_hash().ok())
                != Some(expected_head)
                || model.try_get_untracked().as_deref() != Some(asset.id.to_string().as_str())
            {
                return Err("Sélection ou zone modifiée ; redemander l’aperçu.".to_owned());
            }
            let ImportResponse::File { file } = response? else {
                return Err("Réponse de modèle inattendue".into());
            };
            if file.asset_id != asset.id
                || file.sha256 != asset.payload.sha256
                || file.kind != GovernedAssetKind::Mesh
                || u64::try_from(file.upload.bytes.len()).ok() != Some(asset.payload.bytes)
                || forge_core::bytes_sha256(&file.upload.bytes) != asset.payload.sha256
            {
                return Err("Le modèle reçu diffère de la version choisie.".into());
            }
            crate::authoring_assets::preview_imported_asset(
                format!("gameplay-visual:{}:{}", asset.id, asset.payload.sha256),
                file.upload.bytes,
            )
        })();
        let message = result.map_or_else(|error|error,|()|"Modèle vérifié ; chargement temporaire demandé. Revenir à la carte avant d’examiner son affectation.".into());
        let _ = status.try_set(message);
    });
}

fn refresh(
    models: RwSignal<Vec<GovernedImportAsset>>,
    loading: RwSignal<bool>,
    status: RwSignal<String>,
) {
    loading.set(true);
    status.set("Lecture de la collection…".into());
    wasm_bindgen_futures::spawn_local(async move {
        let response = crate::asset_import_ui::invoke(&ImportRequest::State).await;
        let _ = loading.try_set(false);
        match response {
            Ok(ImportResponse::State { state }) => {
                let meshes = state
                    .catalog
                    .map(|c| c.assets)
                    .unwrap_or_default()
                    .into_iter()
                    .filter(|a| a.kind == GovernedAssetKind::Mesh)
                    .collect::<Vec<_>>();
                let message = if meshes.is_empty() {
                    "Importer d’abord un modèle 3D dans la collection."
                } else {
                    "Collection actualisée. Les modèles déjà enregistrés gardent leur version."
                };
                let _ = models.try_set(meshes);
                let _ = status.try_set(message.into());
            }
            Ok(_) => {
                let _ = status.try_set("Réponse de collection inattendue".into());
            }
            Err(error) => {
                let _ = status.try_set(error);
            }
        }
    });
}

fn prepare(
    state: Option<AuthoringState>,
    models: &[GovernedImportAsset],
    entity: &str,
    selected_model: Option<&str>,
) -> Edit {
    let state = state.ok_or("Ouvrir une zone")?;
    if state.playtest_hash.is_some() {
        return Err("Revenir à l’édition avant de changer un modèle.".into());
    }
    let head = state.zone_head.ok_or("Ouvrir une zone agrégée")?;
    let entity = entity
        .parse::<EntityId>()
        .map_err(|_| "Choisir un personnage ou objet")?;
    let mut bindings = head
        .corrections
        .gameplay_visuals
        .clone()
        .unwrap_or_default();
    let old = bindings.iter().find(|b| b.entity_id == entity).cloned();
    let mut pins = models
        .iter()
        .map(|a| VisualAssetPin {
            asset_id: a.id,
            sha256: a.payload.sha256,
        })
        .chain(bindings.iter().map(|b| b.asset))
        .collect::<Vec<_>>();
    pins.sort();
    pins.dedup();
    let asset = selected_model
        .map(|value| {
            let id = value
                .parse::<AssetId>()
                .map_err(|_| "Choisir un modèle importé")?;
            let asset = models
                .iter()
                .find(|a| a.id == id && a.kind == GovernedAssetKind::Mesh)
                .ok_or("Modèle absent de la collection actualisée")?;
            Ok::<_, String>(VisualAssetPin {
                asset_id: id,
                sha256: asset.payload.sha256,
            })
        })
        .transpose()?;
    let key =
        serde_json::to_string(&(head.content_hash()?, entity, asset)).map_err(|e| e.to_string())?;
    let command = CommandId::deterministic(format!("gameplay-visual/{key}"));
    let request = if let (Some(old), Some(asset)) = (&old, asset) {
        head.prepare_gameplay_visual_replacement(&[entity], old.asset, asset, &pins, command)?
    } else {
        bindings.retain(|binding| binding.entity_id != entity);
        if let Some(asset) = asset {
            bindings.push(GameplayVisualBinding {
                area_id: head.base.terrain.area_id,
                entity_id: entity,
                asset,
                local_transform: Transform::IDENTITY,
            });
        } else if old.is_none() {
            return Err("Cet élément n’a aucun modèle à retirer.".into());
        }
        head.prepare_gameplay_visuals(Some(bindings), &pins, command)?
    };
    Ok(AuthoringRequest::PreviewZone { request })
}
