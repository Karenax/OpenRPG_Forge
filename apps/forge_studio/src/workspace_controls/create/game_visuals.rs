//! Select pinned images from the existing author collection, without file paths.
use super::game_form::GameForm;
use crate::asset_import_protocol::{ImportReadRole, ImportRequest, ImportResponse};
use forge_assets::governed_import::{GovernedAssetKind, GovernedImportAsset};
use forge_project::workspace::launcher::PresentationAsset;
use leptos::prelude::*;

const MAX_IMAGE_BYTES: u64 = 8 * 1024 * 1024;

#[derive(Clone, Copy)]
enum Role {
    Icon,
    Logo,
    Background,
}
impl Role {
    fn get(self, form: &GameForm) -> Option<PresentationAsset> {
        match self {
            Self::Icon => form.fields.icon,
            Self::Logo => form.fields.logo,
            Self::Background => form.fields.background,
        }
    }
    fn set(self, form: &mut GameForm, pin: Option<PresentationAsset>) {
        match self {
            Self::Icon => form.fields.icon = pin,
            Self::Logo => form.fields.logo = pin,
            Self::Background => form.fields.background = pin,
        }
    }
}
fn pin(asset: &GovernedImportAsset) -> PresentationAsset {
    PresentationAsset {
        asset_id: asset.id,
        content_hash: asset.payload.sha256,
    }
}
fn key(pin: PresentationAsset) -> String {
    format!("{}/{}", pin.asset_id, pin.content_hash)
}

#[component]
pub(super) fn GameVisuals(form: RwSignal<Option<GameForm>>) -> impl IntoView {
    let images = RwSignal::new(Vec::<GovernedImportAsset>::new());
    let busy = RwSignal::new(false);
    let error = RwSignal::new(String::new());
    let refresh = move || {
        if busy.get_untracked() {
            return;
        }
        busy.set(true);
        error.set(String::new());
        wasm_bindgen_futures::spawn_local(async move {
            let result = crate::asset_import_ui::invoke(&ImportRequest::State).await;
            if busy.try_set(false).is_some() {
                return;
            }
            match result {
                Ok(ImportResponse::State { state }) => {
                    let selected = state.catalog.map_or_else(Vec::new, |catalog| {
                        catalog
                            .assets
                            .into_iter()
                            .filter(|asset| {
                                asset.kind == GovernedAssetKind::Texture
                                    && asset.payload.bytes <= MAX_IMAGE_BYTES
                            })
                            .collect()
                    });
                    images.set(selected);
                }
                Ok(_) => error
                    .set("La collection n’a pas pu être chargée. Actualisez les images.".into()),
                Err(reason) => error.set(format!("Images indisponibles : {reason}")),
            }
        });
    };
    refresh();
    view! { <fieldset>
        <legend>"Visuels du jeu"</legend>
        <p>"Choisissez les images de votre collection d’auteur. Importez-les avec « Importer mes assets » si nécessaire (PNG ou JPEG, 8 Mio maximum)."</p>
        <button type="button" disabled=move ||busy.get() on:click=move |_|refresh()>"Actualiser les images"</button>
        <Show when=move ||busy.get()><p role="status">"Chargement des images…"</p></Show>
        <Show when=move ||!error.get().is_empty()><p role="alert">{move ||error.get()}</p></Show>
        <ImageChoice label="Icône" role=Role::Icon form=form images=images/>
        <ImageChoice label="Logo" role=Role::Logo form=form images=images/>
        <ImageChoice label="Arrière-plan" role=Role::Background form=form images=images/>
    </fieldset> }
}

#[component]
fn ImageChoice(
    label: &'static str,
    role: Role,
    form: RwSignal<Option<GameForm>>,
    images: RwSignal<Vec<GovernedImportAsset>>,
) -> impl IntoView {
    let selected = move || form.get().and_then(|form| role.get(&form));
    view! { <label>{label}
        <select prop:value=move ||selected().map_or_else(String::new, key)
            on:change=move |event| {
                let value = event_target_value(&event);
                let chosen = if value.is_empty() { None } else {
                    let Some(chosen) = images.get_untracked().iter().find(|asset| key(pin(asset)) == value).map(pin) else { return };
                    Some(chosen)
                };
                form.update(|form| if let Some(form) = form { role.set(form, chosen); });
            }>
            <option value="">"Aucune image"</option>
            {move ||selected().filter(|pin| !images.get().iter().any(|asset| self::pin(asset) == *pin))
                .map(|pin| view! { <option value=key(pin)>"Image conservée — révision antérieure"</option> })}
            {move ||images.get().into_iter().map(|asset| {
                let value = key(pin(&asset));
                view! { <option value=value>{asset.name}</option> }
            }).collect_view()}
        </select>
        {move ||selected().map(|pin| view! { <ImagePreview pin=pin label=label/> })}
    </label> }
}

#[component]
fn ImagePreview(pin: PresentationAsset, label: &'static str) -> impl IntoView {
    let url = RwSignal::new(String::new());
    let error = RwSignal::new(String::new());
    on_cleanup(move || {
        let current = url.get_untracked();
        if !current.is_empty() {
            let _ = web_sys::Url::revoke_object_url(&current);
        }
    });
    wasm_bindgen_futures::spawn_local(async move {
        let result = async {
            let response = crate::asset_import_ui::invoke(&ImportRequest::Read {
                asset_id: pin.asset_id,
                sha256: pin.content_hash,
                role: ImportReadRole::Payload,
            })
            .await?;
            let ImportResponse::File { file } = response else {
                return Err("Image indisponible".to_owned());
            };
            if file.kind != GovernedAssetKind::Texture
                || file.upload.byte_length > MAX_IMAGE_BYTES
                || !matches!(file.mime_type.as_str(), "image/png" | "image/jpeg")
            {
                return Err("Choisissez une image PNG ou JPEG de 8 Mio maximum.".into());
            }
            let parts = js_sys::Array::new();
            parts.push(&js_sys::Uint8Array::from(file.upload.bytes.as_slice()));
            let options = web_sys::BlobPropertyBag::new();
            options.set_type(&file.mime_type);
            let blob = web_sys::Blob::new_with_u8_array_sequence_and_options(&parts, &options)
                .map_err(|_| "Aperçu indisponible".to_owned())?;
            web_sys::Url::create_object_url_with_blob(&blob)
                .map_err(|_| "Aperçu indisponible".into())
        }
        .await;
        match result {
            Ok(value) => {
                if let Some(unused) = url.try_set(value) {
                    let _ = web_sys::Url::revoke_object_url(&unused);
                }
            }
            Err(reason) => {
                let _ = error.try_set(reason);
            }
        }
    });
    view! {
        <Show when=move ||!url.get().is_empty()><img src=move ||url.get() alt=format!("Aperçu : {label}") style="display:block;max-width:100%;max-height:140px;object-fit:contain"/></Show>
        <Show when=move ||!error.get().is_empty()><span role="alert">{move ||error.get()}</span></Show>
    }
}
