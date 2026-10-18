//! User-library import dialog. Every durable mutation is a reviewed native command.
use crate::asset_import_protocol::{
    ImportCandidate, ImportReadRole, ImportRequest, ImportResponse, ImportSource, ImportSourceKind,
    ImportState, ImportUpload, MAX_PACKET_BYTES, PreparedImport, decode_response, encode_request,
};
use forge_assets::governed_import::{
    AssetProvenance, AssetRights, GovernedAssetKind, GovernedImportAsset, RedistributionRights,
};
use forge_core::{AssetId, CommandId};
use leptos::prelude::*;
use wasm_bindgen::JsCast;

#[derive(Clone, Copy)]
struct Ui {
    state: RwSignal<Option<ImportState>>,
    plan: RwSignal<Option<PreparedImport>>,
    selected: RwSignal<Option<AssetId>>,
    parent: RwSignal<Option<AssetId>>,
    busy: RwSignal<bool>,
    status: RwSignal<String>,
    name: RwSignal<String>,
    kind: RwSignal<String>,
    author: RwSignal<String>,
    origin: RwSignal<String>,
    license: RwSignal<String>,
    proof: RwSignal<String>,
    date: RwSignal<String>,
    rights: RwSignal<String>,
    local: RwSignal<bool>,
    authored: RwSignal<bool>,
    recipe: RwSignal<bool>,
    uploads: StoredValue<UploadSlots>,
    preview_url: RwSignal<String>,
    preview_kind: RwSignal<String>,
    bindings: StoredValue<Option<crate::atelier_ui::MainBindings>>,
}

#[derive(Clone, Copy)]
struct UploadSlots {
    payload: RwSignal<Option<ImportUpload>>,
    source: RwSignal<Option<ImportUpload>>,
    thumbnail: RwSignal<Option<ImportUpload>>,
}

impl Ui {
    fn new() -> Self {
        Self {
            state: RwSignal::new(None),
            plan: RwSignal::new(None),
            selected: RwSignal::new(None),
            parent: RwSignal::new(None),
            busy: RwSignal::new(false),
            status: RwSignal::new(String::new()),
            name: RwSignal::new(String::new()),
            kind: RwSignal::new("mesh".into()),
            author: RwSignal::new(String::new()),
            origin: RwSignal::new(String::new()),
            license: RwSignal::new(String::new()),
            proof: RwSignal::new(String::new()),
            date: RwSignal::new(
                js_sys::Date::new_0()
                    .to_iso_string()
                    .as_string()
                    .unwrap_or_default()
                    .chars()
                    .take(10)
                    .collect(),
            ),
            rights: RwSignal::new("unknown".into()),
            local: RwSignal::new(false),
            authored: RwSignal::new(false),
            recipe: RwSignal::new(false),
            uploads: StoredValue::new(UploadSlots {
                payload: RwSignal::new(None),
                source: RwSignal::new(None),
                thumbnail: RwSignal::new(None),
            }),
            preview_url: RwSignal::new(String::new()),
            preview_kind: RwSignal::new(String::new()),
            bindings: StoredValue::new(use_context::<crate::atelier_ui::MainBindings>().or_else(
                || {
                    use_context::<crate::atelier_ui::Link>()
                        .and_then(|link| link.main.get_untracked())
                },
            )),
        }
    }
    fn current(self) -> Option<GovernedImportAsset> {
        let selected = self.selected.get_untracked()?;
        self.state
            .get_untracked()?
            .catalog?
            .assets
            .into_iter()
            .find(|asset| asset.id == selected)
    }
    fn select(self, asset: &GovernedImportAsset) {
        self.selected.set(Some(asset.id));
        self.parent.set(asset.derived_from);
        self.plan.set(None);
        self.uploads.get_value().payload.set(None);
        self.uploads.get_value().source.set(None);
        self.uploads.get_value().thumbnail.set(None);
        self.name.set(asset.name.clone());
        self.kind.set(kind_name(asset.kind).into());
        self.author.set(asset.provenance.author.clone());
        self.origin.set(asset.provenance.source.clone());
        self.license.set(asset.provenance.license.clone());
        self.date.set(asset.provenance.acquired_on.clone());
        self.local.set(asset.provenance.rights.local_use);
        self.rights.set(
            match asset.provenance.rights.redistribution {
                RedistributionRights::Allowed => "allowed",
                RedistributionRights::Forbidden => "forbidden",
                RedistributionRights::Unknown => "unknown",
            }
            .into(),
        );
        self.authored.set(asset.authored_changes);
        self.recipe.set(matches!(
            asset.durable_source,
            Some(forge_assets::governed_import::DurableAssetSource::Recipe { .. })
        ));
        self.proof.set(String::new());
    }
    fn new_asset(self) {
        self.selected.set(None);
        self.parent.set(None);
        self.plan.set(None);
        self.uploads.get_value().payload.set(None);
        self.uploads.get_value().source.set(None);
        self.uploads.get_value().thumbnail.set(None);
        self.name.set(String::new());
        self.proof.set(String::new());
        self.authored.set(false);
        self.recipe.set(false);
        self.local.set(false);
        self.rights.set("unknown".into());
    }
    fn variant(self) {
        let Some(asset) = self.current() else {
            return;
        };
        self.select(&asset);
        self.selected.set(None);
        self.parent.set(Some(asset.id));
        self.authored.set(true);
        let mut name = asset.name;
        while name.len() > 230 {
            name.pop();
        }
        self.name.set(format!("{name} · variante"));
        self.status.set("Nouvelle identité dérivée. Chargez le fichier retouché et sa source ou recette avant d’examiner l’import.".into());
    }
    fn candidate(self) -> Result<ImportCandidate, String> {
        let state = self
            .state
            .get_untracked()
            .ok_or("Actualiser la collection avant l’import")?;
        let payload = self
            .uploads
            .get_value()
            .payload
            .get_untracked()
            .ok_or("Choisir le fichier à importer")?;
        let command = command_id("prepare");
        let old = self.current();
        Ok(ImportCandidate {
            command_id: command,
            expected_revision: state.revision,
            asset_id: old.as_ref().map_or_else(
                || AssetId::deterministic(format!("user-import:{command}")),
                |asset| asset.id,
            ),
            expected_asset_sha256: old.as_ref().map(|asset| asset.payload.sha256),
            derived_from: self.parent.get_untracked(),
            name: self.name.get_untracked(),
            kind: match self.kind.get_untracked().as_str() {
                "texture" => GovernedAssetKind::Texture,
                "audio" => GovernedAssetKind::Audio,
                _ => GovernedAssetKind::Mesh,
            },
            provenance: AssetProvenance {
                source: self.origin.get_untracked(),
                author: self.author.get_untracked(),
                license: self.license.get_untracked(),
                evidence: "Licence et provenance conservées avec cet import".into(),
                acquired_on: self.date.get_untracked(),
                rights: AssetRights {
                    local_use: self.local.get_untracked(),
                    redistribution: match self.rights.get_untracked().as_str() {
                        "allowed" => RedistributionRights::Allowed,
                        "forbidden" => RedistributionRights::Forbidden,
                        _ => RedistributionRights::Unknown,
                    },
                    redistribution_evidence: None,
                },
            },
            license_text: self.proof.get_untracked(),
            payload,
            authored_changes: self.authored.get_untracked(),
            source: self
                .uploads
                .get_value()
                .source
                .get_untracked()
                .map(|upload| ImportSource {
                    kind: if self.recipe.get_untracked() {
                        ImportSourceKind::Recipe
                    } else {
                        ImportSourceKind::AuthoredFile
                    },
                    upload,
                }),
            thumbnail: self.uploads.get_value().thumbnail.get_untracked(),
        })
    }
}

fn kind_name(kind: GovernedAssetKind) -> &'static str {
    match kind {
        GovernedAssetKind::Mesh => "mesh",
        GovernedAssetKind::Texture => "texture",
        GovernedAssetKind::Audio => "audio",
    }
}
fn command_id(action: &str) -> CommandId {
    CommandId::deterministic(format!(
        "asset-import-ui:{action}:{}:{}",
        js_sys::Date::now(),
        js_sys::Math::random()
    ))
}

pub(crate) async fn invoke(request: &ImportRequest) -> Result<ImportResponse, String> {
    let wire = encode_request(request)?;
    let argument = js_sys::Uint8Array::from(wire.as_slice());
    let response = crate::web::invoke_tauri_raw("execute_asset_import", argument.as_ref()).await?;
    let bytes = if response.is_instance_of::<js_sys::ArrayBuffer>()
        || response.is_instance_of::<js_sys::Uint8Array>()
    {
        js_sys::Uint8Array::new(&response)
    } else {
        return Err("Réponse binaire d’import attendue".into());
    };
    if u64::from(bytes.length()) > u64::try_from(MAX_PACKET_BYTES).map_err(|e| e.to_string())? {
        return Err("Réponse d’import trop volumineuse".into());
    }
    decode_response(&bytes.to_vec())
}

fn refresh(ui: Ui) {
    ui.busy.set(true);
    wasm_bindgen_futures::spawn_local(async move {
        let result = invoke(&ImportRequest::State).await;
        if ui.busy.try_set(false).is_some() {
            return;
        }
        match result {
            Ok(ImportResponse::State { state }) => {
                let _ = ui.state.try_set(Some(state));
            }
            Ok(_) => {
                let _ = ui.status.try_set("Réponse de collection inattendue".into());
            }
            Err(error) => {
                let _ = ui.status.try_set(error);
            }
        }
    });
}

#[component]
pub(crate) fn AssetImportLauncher() -> impl IntoView {
    let dialog = NodeRef::<leptos::html::Dialog>::new();
    let open = RwSignal::new(false);
    view! {<style>{include_str!("asset_import.css")}</style><button class="asset-library-launch" on:click=move |_|{if let Some(dialog)=dialog.get() &&dialog.show_modal().is_ok(){open.set(true);}}>"Importer mes assets"</button>
    <dialog node_ref=dialog class="asset-import-dialog" aria-label="Importer des assets" on:close=move |_|open.set(false) on:keydown=move|event|event.stop_propagation() on:mousedown=move|event|event.stop_propagation()>
    <Show when=move||open.get()><ImportPanel close=Callback::new(move|()|{if let Some(dialog)=dialog.get(){dialog.close();}open.set(false);})/></Show></dialog>}
}

#[component]
fn ImportPanel(close: Callback<()>) -> impl IntoView {
    let ui = Ui::new();
    refresh(ui);
    on_cleanup(move || {
        let url = ui.preview_url.get_untracked();
        if !url.is_empty() {
            let _ = web_sys::Url::revoke_object_url(&url);
        }
    });
    view! {<section class="asset-import-panel"><header><div><p>"VOTRE COLLECTION D’AUTEUR"</p><h1>"Importer des assets"</h1><p>"Modèles GLB autonomes, images et sons conservés dans vos données utilisateur."</p></div><button on:click=move |_|close.run(())>"Fermer ×"</button></header>
    <div class="asset-import-columns"><aside><ImportedEntries ui=ui/><button disabled=move||ui.busy.get() on:click=move |_|ui.new_asset()>"Nouvel asset"</button><button disabled=move||ui.busy.get()||ui.selected.get().is_none() on:click=move |_|ui.variant()>"Créer une variante"</button><button disabled=move||ui.busy.get() on:click=move |_|refresh(ui)>"Actualiser"</button></aside>
    <main><MetadataFields ui=ui/><SourceFields ui=ui/><ImportActions ui=ui close=close/><AssetPreview ui=ui/></main></div><output role="status" aria-live="polite">{move||ui.status.get()}</output></section>}
}

#[component]
fn ImportedEntries(ui: Ui) -> impl IntoView {
    view! {<h2>"Collection locale"</h2><p>{move||ui.state.get().map_or_else(||"Chargement…".into(),|s|format!("Révision {}",s.revision))}</p><ul class="asset-import-list">{move||ui.state.get().and_then(|s|s.catalog).map(|catalog|catalog.assets.into_iter().map(|asset|{let selected=asset.clone();let id=asset.id;view!{<li><button aria-pressed=move||(ui.selected.get()==Some(id)).to_string() on:click=move |_|ui.select(&selected)>{asset.name}<small>{kind_name(asset.kind)}</small></button></li>}}).collect_view())}</ul>}
}

#[component]
fn MetadataFields(ui: Ui) -> impl IntoView {
    view! {<fieldset disabled=move||ui.busy.get() on:input=move |_|ui.plan.set(None)><legend>{move||if ui.selected.get().is_some(){"Réimporter le fichier, conserver l’identité"}else if ui.parent.get().is_some(){"Nouvelle variante, nouvelle identité"}else{"Nouvel asset"}}</legend>
    <Show when=move||ui.parent.get().is_some()><p>{move||ui.parent.get().map(|id|format!("Parent conservé : {id}"))}</p></Show>
    <label>"Nom"<input maxlength="256" prop:value=move||ui.name.get() on:input=move|e|ui.name.set(event_target_value(&e))/></label>
    <label>"Type"<select prop:value=move||ui.kind.get() on:change=move|e|ui.kind.set(event_target_value(&e))><option value="mesh">"Modèle 3D GLB"</option><option value="texture">"Texture / image"</option><option value="audio">"Audio"</option></select></label>
    <label>"Source ou URL d’origine"<input maxlength="2048" prop:value=move||ui.origin.get() on:input=move|e|ui.origin.set(event_target_value(&e))/></label>
    <label>"Auteur"<input maxlength="256" prop:value=move||ui.author.get() on:input=move|e|ui.author.set(event_target_value(&e))/></label>
    <label>"Licence SPDX ou LicenseRef"<input maxlength="256" placeholder="CC0-1.0, CC-BY-4.0, LicenseRef-…" prop:value=move||ui.license.get() on:input=move|e|ui.license.set(event_target_value(&e))/></label>
    <label>"Date d’acquisition"<input type="date" prop:value=move||ui.date.get() on:input=move|e|ui.date.set(event_target_value(&e))/></label>
    <label>"Licence et preuve de provenance (texte conservé)"<textarea maxlength="65536" rows="5" placeholder="Texte de licence, crédits et référence de la preuve. Un réimport ou une variante peut conserver la preuve existante de son parent." prop:value=move||ui.proof.get() on:input=move|e|ui.proof.set(event_target_value(&e))></textarea></label>
    <label class="asset-import-check"><input type="checkbox" prop:checked=move||ui.local.get() on:change=move|e|ui.local.set(event_target_checked(&e))/>"Je dispose du droit d’utiliser ce fichier localement."</label>
    <label>"Droit de redistribution"<select prop:value=move||ui.rights.get() on:change=move|e|ui.rights.set(event_target_value(&e))><option value="unknown">"Non établi — collection privée"</option><option value="forbidden">"Interdite"</option><option value="allowed">"Autorisée par la preuve fournie"</option></select></label></fieldset>}
}

#[component]
fn SourceFields(ui: Ui) -> impl IntoView {
    view! {<fieldset disabled=move||ui.busy.get() on:input=move |_|ui.plan.set(None)><legend>"Fichiers"</legend><label>"Fichier à importer (256 Mio maximum)"<input type="file" accept=".glb,.png,.jpg,.jpeg,.wav,.ogg,.flac" on:change=move|e|pick_upload(&e,ui.uploads.get_value().payload,268_435_456,ui)/></label><p>{move||ui.uploads.get_value().payload.get().map(|f|format!("{} · {} octets",f.name,f.byte_length))}</p>
    <label class="asset-import-check"><input type="checkbox" prop:checked=move||ui.authored.get() on:change=move|e|ui.authored.set(event_target_checked(&e))/>"Ce fichier comprend des retouches à préserver."</label>
    <label>"Source de travail ou recette JSON conservée (obligatoire pour les retouches et variantes)"<input type="file" accept=".blend,.glb,.png,.jpg,.jpeg,.wav,.ogg,.flac,.json" on:change=move|e|pick_upload(&e,ui.uploads.get_value().source,268_435_456,ui)/></label>
    <label class="asset-import-check"><input type="checkbox" prop:checked=move||ui.recipe.get() on:change=move|e|ui.recipe.set(event_target_checked(&e))/>"La source est une recette JSON."</label>
    <label>"Vignette choisie (PNG, 2 Mio maximum)"<input type="file" accept=".png" on:change=move|e|pick_upload(&e,ui.uploads.get_value().thumbnail,2_097_152,ui)/></label><p>"Une image choisie n’est pas une capture du modèle. Une texture utilise sa propre image en l’absence de vignette."</p></fieldset>}
}

fn pick_upload(
    event: &leptos::ev::Event,
    slot: RwSignal<Option<ImportUpload>>,
    limit: u32,
    ui: Ui,
) {
    slot.set(None);
    let file = event
        .target()
        .and_then(|t| t.dyn_into::<web_sys::HtmlInputElement>().ok())
        .and_then(|input| {
            let file = input.files().and_then(|files| files.get(0));
            input.set_value("");
            file
        });
    let Some(file) = file else {
        slot.set(None);
        return;
    };
    ui.plan.set(None);
    if file.size() <= 0.0 || file.size() > f64::from(limit) {
        ui.status.set("Fichier vide ou trop volumineux".into());
        return;
    }
    ui.busy.set(true);
    let name = file.name();
    wasm_bindgen_futures::spawn_local(async move {
        let result = wasm_bindgen_futures::JsFuture::from(file.array_buffer()).await;
        if ui.busy.try_set(false).is_some() {
            return;
        }
        match result {
            Ok(buffer) => {
                let bytes = js_sys::Uint8Array::new(&buffer);
                if bytes.length() > limit {
                    let _ = ui.status.try_set("Fichier trop volumineux".into());
                    return;
                }
                let _ = slot.try_set(Some(ImportUpload {
                    name,
                    byte_length: u64::from(bytes.length()),
                    bytes: bytes.to_vec(),
                }));
            }
            Err(_) => {
                let _ = ui
                    .status
                    .try_set("Le fichier sélectionné est illisible".into());
            }
        }
    });
}

#[component]
fn ImportActions(ui: Ui, close: Callback<()>) -> impl IntoView {
    view! {<div class="asset-import-actions"><button disabled=move||ui.busy.get() on:click=move |_|{
        match ui.candidate(){Ok(candidate)=>{ui.busy.set(true);wasm_bindgen_futures::spawn_local(async move{let result=invoke(&ImportRequest::Prepare{candidate:Box::new(candidate)}).await;if ui.busy.try_set(false).is_some(){return;}match result{Ok(ImportResponse::Prepared{plan})=>{let _=ui.status.try_set("Aperçu prêt. Vérifiez le nouvel identifiant de contenu avant Appliquer.".into());let _=ui.plan.try_set(Some(*plan));},Ok(_)=>{let _=ui.status.try_set("Aperçu d’import inattendu".into());},Err(error)=>{let _=ui.status.try_set(error);}}});},Err(error)=>ui.status.set(error)}
    }>"Examiner l’import"</button>
    <button disabled=move||ui.busy.get()||ui.plan.get().is_none() on:click=move |_|{if let Some(plan)=ui.plan.get_untracked(){mutate(ui,ImportRequest::Apply{command_id:plan.command_id,plan_sha256:plan.plan_sha256});}}>"Appliquer cet import"</button>
    <button disabled=move||ui.busy.get()||ui.state.get().is_none_or(|s|s.revision==0) on:click=move |_|{if let Some(state)=ui.state.get_untracked(){mutate(ui,ImportRequest::Undo{command_id:command_id("undo"),expected_revision:state.revision});}}>"Annuler le dernier import"</button>
    <Show when=move||ui.selected.get().is_some()><button disabled=move||ui.busy.get() on:click=move |_|preview(ui,ImportReadRole::Payload,close)>"Aperçu / écouter"</button><button disabled=move||ui.busy.get() on:click=move |_|preview(ui,ImportReadRole::Thumbnail,close)>"Voir la vignette"</button><button disabled=move||ui.busy.get() on:click=move |_|place(ui,close,false)>"Placer le modèle"</button><button disabled=move||ui.busy.get() on:click=move |_|place(ui,close,true)>"Proposer le remplacement de l’objet sélectionné"</button></Show></div>
    <Show when=move||ui.plan.get().is_some()><div class="asset-import-plan">{move||ui.plan.get().map(|p|format!("{} · ID {}\nRévision {} · ancien SHA : {}\nNouveau SHA : {}\n{} octets. Les placements existants conservent leurs pins jusqu’à un remplacement explicite.",p.asset.name,p.asset.id,p.base_revision,p.previous_asset_sha256.map_or_else(||"nouvel asset".into(),|h|h.to_string()),p.asset.payload.sha256,p.asset.payload.bytes))}</div></Show>}
}

fn mutate(ui: Ui, request: ImportRequest) {
    ui.busy.set(true);
    wasm_bindgen_futures::spawn_local(async move {
        let result = invoke(&request).await;
        if ui.busy.try_set(false).is_some() {
            return;
        }
        match result {
            Ok(ImportResponse::Applied { receipt }) => {
                let _ = ui.plan.try_set(None);
                let _ = ui.selected.try_set(receipt.asset_id);
                if let Some(uploads) = ui.uploads.try_get_value() {
                    let _ = uploads.payload.try_set(None);
                    let _ = uploads.source.try_set(None);
                    let _ = uploads.thumbnail.try_set(None);
                }
                if receipt.undo {
                    let _ = ui.parent.try_set(None);
                }
                let _ = ui.status.try_set(format!(
                    "Commande enregistrée · révision {}",
                    receipt.revision
                ));
                refresh(ui);
            }
            Ok(_) => {
                let _ = ui.status.try_set("Résultat d’import inattendu".into());
            }
            Err(error) => {
                let _ = ui.status.try_set(error);
            }
        }
    });
}

fn preview(ui: Ui, role: ImportReadRole, close: Callback<()>) {
    let Some(asset) = ui.current() else {
        return;
    };
    ui.busy.set(true);
    wasm_bindgen_futures::spawn_local(async move {
        let result = invoke(&ImportRequest::Read {
            asset_id: asset.id,
            sha256: asset.payload.sha256,
            role,
        })
        .await;
        if ui.busy.try_set(false).is_some() {
            return;
        }
        let result = (|| {
            let ImportResponse::File { file } = result? else {
                return Err("Réponse de fichier inattendue".to_string());
            };
            if forge_core::bytes_sha256(&file.upload.bytes) != file.sha256 {
                return Err("Empreinte du fichier reçu différente".into());
            }
            if file.kind == GovernedAssetKind::Mesh && role == ImportReadRole::Payload {
                crate::authoring_assets::preview_imported_asset(
                    asset.id.to_string(),
                    file.upload.bytes,
                )?;
                close.run(());
                return Ok(());
            }
            let parts = js_sys::Array::new();
            parts.push(&js_sys::Uint8Array::from(file.upload.bytes.as_slice()));
            let options = web_sys::BlobPropertyBag::new();
            options.set_type(&file.mime_type);
            let blob = web_sys::Blob::new_with_u8_array_sequence_and_options(&parts, &options)
                .map_err(|_| "Aperçu illisible")?;
            let url = web_sys::Url::create_object_url_with_blob(&blob)
                .map_err(|_| "Aperçu indisponible")?;
            let previous = ui.preview_url.get_untracked();
            if !previous.is_empty() {
                let _ = web_sys::Url::revoke_object_url(&previous);
            }
            ui.preview_kind.set(
                if file.mime_type.starts_with("audio/") {
                    "audio"
                } else {
                    "image"
                }
                .into(),
            );
            ui.preview_url.set(url);
            Ok(())
        })();
        if let Err(error) = result {
            let _ = ui.status.try_set(error);
        }
    });
}

fn place(ui: Ui, close: Callback<()>, replacement: bool) {
    let Some(asset) = ui.current() else {
        return;
    };
    if asset.kind != GovernedAssetKind::Mesh {
        ui.status
            .set("Seul un modèle 3D se place sur la carte".into());
        return;
    }
    let Some(bindings) = ui.bindings.get_value() else {
        ui.status.set("Ouvrir une carte avant le placement".into());
        return;
    };
    ui.busy.set(true);
    wasm_bindgen_futures::spawn_local(async move {
        let result = invoke(&ImportRequest::Read {
            asset_id: asset.id,
            sha256: asset.payload.sha256,
            role: ImportReadRole::Payload,
        })
        .await;
        if ui.busy.try_set(false).is_some() {
            return;
        }
        let result = (|| {
            let ImportResponse::File { file } = result? else {
                return Err("Réponse de modèle inattendue".to_string());
            };
            if file.asset_id != asset.id
                || file.kind != GovernedAssetKind::Mesh
                || file.sha256 != asset.payload.sha256
                || forge_core::bytes_sha256(&file.upload.bytes) != asset.payload.sha256
            {
                return Err("Le modèle vérifié diffère de la sélection".into());
            }
            if replacement {
                crate::authoring_assets::replace_imported_asset(
                    bindings,
                    asset.id,
                    asset.payload.sha256,
                )
            } else {
                crate::authoring_assets::place_imported_asset(
                    bindings,
                    asset.name,
                    asset.id,
                    asset.payload.sha256,
                )
            }
        })();
        match result {
            Ok(()) => close.run(()),
            Err(error) => {
                let _ = ui.status.try_set(error);
            }
        }
    });
}

#[component]
fn AssetPreview(ui: Ui) -> impl IntoView {
    view! {<Show when=move||!ui.preview_url.get().is_empty()><section class="asset-import-preview"><Show when=move||ui.preview_kind.get()=="audio" fallback=move||view!{<img src=move||ui.preview_url.get() alt="Aperçu du fichier importé"/>}><audio controls src=move||ui.preview_url.get()>"Lecture audio indisponible"</audio></Show></section></Show>}
}
