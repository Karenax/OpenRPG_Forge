//! Private Vault bytes enter the same GLB admission and projection as kit assets.
use crate::asset_library_protocol::{
    LibraryModel, LibraryRequest, ModuleModelReply, ModuleModelRequest, vault_asset_id,
};
use forge_core::{AssetId, ContentHash};
use leptos::prelude::*;
use wasm_bindgen::{JsCast, JsValue};

mod saved_particles;
mod saved_physics;
pub(super) use saved_particles::load_for_model as saved_particles_for_model;
pub(crate) use saved_physics::SavedPhysicsDetails;
pub(super) use saved_physics::load_for_model as saved_physics_for_model;

thread_local! {
    static MODEL_READ_GATE: super::model_read_gate::ModelReadGate = super::model_read_gate::ModelReadGate::default();
}

async fn read_model(request: LibraryRequest) -> Result<LibraryModel, String> {
    // Wait before invoking IPC, so pending requests hold only small metadata.
    // Keep admission through original SHA verification; cancellation releases it.
    let _permit = MODEL_READ_GATE.with(|gate| gate.acquire())?.await;
    let bytes = read_wire(&request).await?;
    let model = crate::asset_library_model_wire::decode(bytes)?;
    if model.asset_id != vault_asset_id(&model.id) {
        return Err("Identité du modèle différente du Vault".into());
    }
    super::verify::check_bytes(&model.bytes, model.sha256)?;
    Ok(model)
}

async fn read_wire(request: &impl serde::Serialize) -> Result<Vec<u8>, String> {
    read_wire_from(
        "read_asset_model",
        request,
        crate::asset_library_model_wire::MAX_MODEL_BYTES + 4108,
    )
    .await
}

async fn read_wire_from(
    command: &str,
    request: &impl serde::Serialize,
    maximum: usize,
) -> Result<Vec<u8>, String> {
    let arguments = js_sys::Object::new();
    let text = serde_json::to_string(request).map_err(|e| e.to_string())?;
    js_sys::Reflect::set(
        &arguments,
        &JsValue::from_str("request"),
        &JsValue::from_str(&text),
    )
    .map_err(|_| "Requête de modèle illisible".to_owned())?;
    let response = crate::web::invoke_tauri_raw(command, arguments.as_ref()).await?;
    let bytes = if response.is_instance_of::<js_sys::ArrayBuffer>() {
        js_sys::Uint8Array::new(&response)
    } else if response.is_instance_of::<js_sys::Uint8Array>() {
        response.unchecked_into::<js_sys::Uint8Array>()
    } else {
        return Err("Réponse binaire de modèle attendue".into());
    };
    if bytes.length() as usize > maximum {
        return Err("Transfert de modèle trop volumineux".into());
    }
    Ok(bytes.to_vec())
}

async fn selected_physics(
    request: crate::asset_library_protocol::LibraryPhysicsRequest,
    package_sha256: ContentHash,
) -> Result<forge_assets::source_physics_package::SourcePhysicsPackage, String> {
    let _permit = MODEL_READ_GATE.with(|gate| gate.acquire())?.await;
    let wire = read_wire_from(
        "read_asset_physics",
        &request,
        forge_assets::source_physics_package::MAX_PACKAGE_BYTES,
    )
    .await?;
    forge_assets::source_physics_package::SourcePhysicsPackage::decode_pinned(
        &wire,
        request.carrier_sha256,
        request.manifest_sha256,
        package_sha256,
    )
}

async fn physics_for_model(
    model: &LibraryModel,
    revision: &str,
    profile: &serde_json::Value,
) -> Result<Option<super::source_graph_physics::SourcePhysicsPayload>, String> {
    let Some(declaration) = profile.get("source_physics_package") else {
        return Ok(None);
    };
    let pin = |key: &str| {
        ContentHash::parse(declaration[key].as_str().ok_or("Pin physique absent")?)
            .map_err(|e| e.to_string())
    };
    let carrier_sha256 = pin("carrier_sha256")?;
    if carrier_sha256 != model.sha256 {
        return Err("Données physiques différentes du modèle sélectionné".into());
    }
    let manifest_sha256 = pin("manifest_sha256")?;
    let package_sha256 = pin("sha256")?;
    let request = crate::asset_library_protocol::LibraryPhysicsRequest {
        schema_version: 1,
        id: model.id.clone(),
        revision: revision.into(),
        carrier_sha256,
        manifest_sha256,
    };
    let _permit = MODEL_READ_GATE.with(|gate| gate.acquire())?.await;
    let wire = read_wire_from(
        "read_asset_physics",
        &request,
        forge_assets::source_physics_package::MAX_PACKAGE_BYTES,
    )
    .await?;
    forge_assets::source_physics_package::SourcePhysicsPackage::decode_pinned(
        &wire,
        carrier_sha256,
        manifest_sha256,
        package_sha256,
    )?;
    Ok(Some(super::source_graph_physics::SourcePhysicsPayload {
        wire: wire.into(),
        carrier_sha256,
        manifest_sha256,
        package_sha256,
    }))
}

async fn particles_for_model(
    model: &LibraryModel,
    revision: &str,
    profile: &serde_json::Value,
) -> Result<Option<super::source_graph_particles::SourceParticlePayload>, String> {
    let Some(declaration) = profile.get("source_particle_package") else {
        return Ok(None);
    };
    let pin = |key: &str| {
        ContentHash::parse(
            declaration[key]
                .as_str()
                .ok_or("Empreinte d’effet absente")?,
        )
        .map_err(|e| e.to_string())
    };
    let carrier_sha256 = pin("carrier_sha256")?;
    if carrier_sha256 != model.sha256 {
        return Err("Effet différent du modèle sélectionné".into());
    }
    let manifest_sha256 = pin("manifest_sha256")?;
    let package_sha256 = pin("sha256")?;
    let request = crate::asset_library_protocol::LibraryParticleRequest {
        schema_version: 1,
        id: model.id.clone(),
        revision: revision.into(),
        carrier_sha256,
        manifest_sha256,
        package_sha256,
    };
    let _permit = MODEL_READ_GATE.with(|gate| gate.acquire())?.await;
    let wire = read_wire_from(
        "read_asset_particles",
        &request,
        forge_assets::source_particle_package::MAX_PACKAGE_BYTES,
    )
    .await?;
    forge_assets::source_particle_package::SourceParticlePackage::decode_pinned(
        &wire,
        carrier_sha256,
        manifest_sha256,
        package_sha256,
    )?
    .resolve_for_carrier(&model.bytes)?;
    Ok(Some(super::source_graph_particles::SourceParticlePayload {
        wire: wire.into(),
        carrier_sha256,
        manifest_sha256,
        package_sha256,
    }))
}

/// Read-only collision information is usable without mounting a 3D scene.
/// The source host still requires separately resolved contact policy.
#[component]
pub(crate) fn SourcePhysicsDetails(
    id: String,
    revision: String,
    profile: serde_json::Value,
) -> impl IntoView {
    let declaration = profile.get("source_physics_package").cloned();
    let available = declaration.is_some();
    let working = RwSignal::new(false);
    let status = RwSignal::new(String::new());
    let action = Callback::new(move |()| {
        if working.get_untracked() {
            return;
        }
        let request = (|| {
            let declaration = declaration.as_ref().ok_or("Paquet physique absent")?;
            let pin = |key: &str| {
                ContentHash::parse(declaration[key].as_str().ok_or("Pin physique absent")?)
                    .map_err(|e| e.to_string())
            };
            Ok::<_, String>((
                crate::asset_library_protocol::LibraryPhysicsRequest {
                    schema_version: 1,
                    id: id.clone(),
                    revision: revision.clone(),
                    carrier_sha256: pin("carrier_sha256")?,
                    manifest_sha256: pin("manifest_sha256")?,
                },
                pin("sha256")?,
            ))
        })();
        let (request, package_sha256) = match request {
            Ok(request) => request,
            Err(error) => {
                status.set(error);
                return;
            }
        };
        working.set(true);
        status.set("Vérification des données de collision…".into());
        wasm_bindgen_futures::spawn_local(async move {
            let result = (async {
                let package = selected_physics(request.clone(), package_sha256).await?;
                let manifest: serde_json::Value =
                    serde_json::from_slice(&package.manifest).map_err(|e| e.to_string())?;
                let rows = manifest["rows"]
                    .as_array()
                    .ok_or("Inventaire physique absent")?;
                let hash = request.carrier_sha256.to_string();
                let row = rows
                    .iter()
                    .find(|row| row["carrier_sha256"].as_str() == Some(hash.as_str()))
                    .ok_or("Modèle absent des données de collision")?;
                let components = row["components"]
                    .as_array()
                    .ok_or("Composants physiques absents")?;
                let empty = components
                    .iter()
                    .filter(|component| component["shape_transport"].is_null())
                    .count();
                Ok::<_, String>(format!(
                    "{} composant(s) de collision, dont {} sans géométrie. Simulation à qualifier.",
                    components.len(),
                    empty
                ))
            })
            .await;
            let _ = working.try_set(false);
            let _ = status
                .try_set(result.unwrap_or_else(|e| format!("Données de collision refusées : {e}")));
        });
    });
    view! {
        <Show when=move ||available>
            <div class="asset-library-actions">
                <button disabled=move ||working.get() on:click=move |_|action.run(())>"Données de collision"</button>
                <p role="status">{move ||status.get()}</p>
            </div>
        </Show>
    }
}

pub(crate) async fn selected(id: String, revision: String) -> Result<LibraryModel, String> {
    let model = read_model(LibraryRequest::Model {
        id: id.clone(),
        revision,
    })
    .await?;
    if model.id != id {
        return Err("Sélection différente de la réponse".into());
    }
    Ok(model)
}

pub(crate) async fn reference(
    asset_id: AssetId,
    sha256: ContentHash,
) -> Result<LibraryModel, String> {
    let model = read_model(LibraryRequest::ModelReference { asset_id, sha256 }).await?;
    if model.asset_id != asset_id || model.sha256 != sha256 {
        return Err("Référence sauvegardée différente de la réponse".into());
    }
    Ok(model)
}

/// Module catalogues publish explicit UUIDs, while the Vault has its own strict
/// identity namespace. Only the typed V2 Missing reply permits a Vault fallback.
pub(crate) async fn installed_reference(
    asset_id: AssetId,
    sha256: ContentHash,
) -> Result<(String, Vec<u8>, super::preview::SceneLayout), String> {
    let reply = {
        let _permit = MODEL_READ_GATE.with(|gate| gate.acquire())?.await;
        let wire = read_wire(&ModuleModelRequest::ModuleModelReference {
            schema_version: 2,
            asset_id,
            sha256,
        })
        .await?;
        let reply = crate::asset_library_model_wire::decode_module(wire, asset_id, sha256)?;
        if let ModuleModelReply::Model(model) = &reply {
            super::verify::check_bytes(&model.bytes, model.sha256)?;
        }
        reply
    };
    match reply {
        ModuleModelReply::Model(model) => Ok((
            format!("module:{asset_id}"),
            model.bytes,
            super::preview::SceneLayout::module(model.collision, model.catalog_sha256),
        )),
        ModuleModelReply::Missing { .. } => {
            let model = reference(asset_id, sha256).await?;
            Ok((
                model.id,
                model.bytes,
                super::preview::SceneLayout::AutonomousScene,
            ))
        }
    }
}

#[component]
pub(crate) fn VaultActions(
    card: crate::asset_library_protocol::LibraryCard,
    revision: String,
    profile: serde_json::Value,
    selected: RwSignal<String>,
    close: Callback<()>,
) -> impl IntoView {
    let bindings = use_context::<crate::atelier_ui::MainBindings>().or_else(|| {
        use_context::<crate::atelier_ui::Link>().and_then(|link| link.main.get_untracked())
    });
    let working = RwSignal::new(false);
    let status = RwSignal::new(String::new());
    let placeable = card.availability == "converted_model";
    let action = Callback::new(move |place: bool| {
        let Some(bindings) = bindings else {
            return;
        };
        if working.get_untracked() || bindings.busy.get_untracked() {
            return;
        }
        let head = match super::placement_controls::head(bindings.state.get_untracked()) {
            Ok(head) if crate::authoring_viewport::can_preview_asset() => head,
            Ok(_) => {
                status.set("Ouvrir la carte en mode édition.".into());
                return;
            }
            Err(e) => {
                status.set(e);
                return;
            }
        };
        if !placeable {
            return;
        }
        let id = card.id.clone();
        let name = card.name.clone();
        let revision = revision.clone();
        let profile = profile.clone();
        super::cancel_preview();
        let ticket = super::preview::ticket();
        working.set(true);
        status.set("Vérification du modèle…".into());
        wasm_bindgen_futures::spawn_local(async move {
            let result = async {
                let model = self::selected(id.clone(), revision.clone()).await?;
                let physics = physics_for_model(&model, &revision, &profile).await?;
                let particles = particles_for_model(&model, &revision, &profile).await?;
                Ok::<_, String>((model, physics, particles))
            }
            .await;
            let _ = working.try_set(false);
            if selected.try_get_untracked().as_deref() != Some(id.as_str())
                || !super::preview::is_current(ticket)
                || !crate::authoring_viewport::can_preview_asset()
                || bindings.busy.get_untracked()
            {
                return;
            }
            let (model, physics, particles) = match result {
                Ok(model) => model,
                Err(e) => {
                    let _ = status.try_set(format!("Modèle refusé : {e}"));
                    return;
                }
            };
            let current_head = super::placement_controls::head(bindings.state.get_untracked());
            if current_head
                .as_ref()
                .ok()
                .and_then(|h| h.content_hash().ok())
                != head.content_hash().ok()
            {
                let _ = status.try_set("Zone modifiée ; recommencer.".into());
                return;
            }
            if place {
                if physics.is_some() {
                    let _ = status.try_set("Placement avec collisions source : chargement persistant encore à qualifier.".into());
                    return;
                }
                let reference = (model.asset_id, model.sha256);
                let particle_pin = particles.as_ref().map(|payload| {
                    Box::new(
                        forge_project::zone_authoring::placements::ParticlePackageReference {
                            schema_version: 1,
                            carrier_sha256: payload.carrier_sha256,
                            manifest_sha256: payload.manifest_sha256,
                            package_sha256: payload.package_sha256,
                        },
                    )
                });
                // The regular projection reads this saved reference itself;
                // never inject bytes into a second map or bypass Preview/Apply.
                crate::authoring_brush_pointer::arm(
                    2.0,
                    false,
                    Callback::new(move |points: Result<Vec<[f32; 2]>, String>| {
                        let request = (|| {
                            let current =
                                super::placement_controls::head(bindings.state.get_untracked())?;
                            if current.content_hash()? != head.content_hash()? {
                                return Err("Zone modifiée ; recommencer le placement".into());
                            }
                            let point = *points?.first().ok_or("Cliquer le terrain")?;
                            super::placement_controls::insertion::prepare_with_packages(
                                &current,
                                &name,
                                reference,
                                (
                                    forge_core::Transform::IDENTITY,
                                    forge_world::placement_snap::PlacementSnap {
                                        grid_step_m: None,
                                        surface_offset_m: Some(0.0),
                                    },
                                ),
                                point,
                                (None, particle_pin.clone()),
                            )
                        })();
                        bindings.send.run(request);
                    }),
                );
                bindings
                    .status
                    .set("Cliquer le terrain pour proposer le placement ; Échap annule.".into());
                close.run(());
            } else if super::preview::submit_scene_with_physics(
                ticket,
                model.id,
                model.bytes,
                physics,
                particles,
            ) {
                bindings
                    .status
                    .set("Modèle vérifié ; chargement de l’aperçu 3D…".into());
                close.run(());
            }
        });
    });
    view! {
        <div class="asset-library-actions">
            <button disabled=move ||!placeable || bindings.is_none() ||working.get() ||bindings.is_some_and(|b|b.busy.get()) on:click=move |_|action.run(false)>"Aperçu 3D"</button>
            <button disabled=move ||!placeable || bindings.is_none() ||working.get() ||bindings.is_some_and(|b|b.busy.get()) on:click=move |_|action.run(true)>"Placer sur la carte"</button>
            <p role="status">{move ||status.get()}</p>
            {bindings.is_none().then(|| view!{<p>"Ouvrez cette bibliothèque depuis une carte pour utiliser l’aperçu et le placement."</p>})}
        </div>
    }
}
