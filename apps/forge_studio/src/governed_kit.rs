//! Opt-in, read-only preview of a locally deployed governed kit, verified before Bevy starts.
use bevy::{
    asset::{
        AssetApp,
        io::{
            AssetSourceBuilder,
            memory::{Dir, MemoryAssetReader},
        },
    },
    prelude::*,
};
use forge_assets::{GovernedAssetKitBuildReport, GovernedAssetKitRuntimePreview};
use forge_core::ContentHash;
use forge_engine::VisualValleyGovernedAssetKitPlugin;
use leptos::prelude::*;
use sha2::{Digest, Sha256};
use std::{cell::RefCell, path::Path};
pub(super) mod transport;
use super::governed_kit_request::{self, KitRequest};
use forge_engine::{VegetationPrototypeForestPreview, VegetationPrototypeLoadPlugin};
use transport::fetch;

thread_local! {
    static OUTPUT: RefCell<Option<WriteSignal<String>>> = const { RefCell::new(None) };
}

#[component]
pub fn KitStatus() -> impl IntoView {
    let (status, output) = signal("Kit externe non demandé".to_owned());
    OUTPUT.with(|slot| slot.replace(Some(output)));
    view! { <output id="studio-kit-status" role="status">{move || status.get()}</output> }
}

fn status(message: String) {
    OUTPUT.with(|slot| {
        if let Some(output) = *slot.borrow() {
            output.set(message);
        }
    });
}

pub fn report_failure(reason: &str) {
    status(format!("Kit refusé : {reason}"));
}

pub struct LoadedKit {
    root: Dir,
    plugin: VisualValleyGovernedAssetKitPlugin,
    forest: Option<VegetationPrototypeLoadPlugin>,
    far_batches: bool,
}

impl LoadedKit {
    /// Asset sources must precede `AssetPlugin`; render plugins must follow it.
    pub fn register_source(&self, app: &mut App) {
        let root = self.root.clone();
        app.register_asset_source(
            "forge-kit",
            AssetSourceBuilder::new(move || Box::new(MemoryAssetReader { root: root.clone() })),
        );
    }

    pub fn install(self, app: &mut App) {
        if self.far_batches {
            app.insert_resource(forge_engine::GovernedFarBatchPreview);
        }
        app.add_plugins(self.plugin).add_systems(Last, publish);
        if let Some(forest) = self.forest {
            app.add_plugins(forest)
                .insert_resource(VegetationPrototypeForestPreview);
        }
    }
}

pub async fn load(scene: &super::LoadedStudioScene) -> Result<Option<LoadedKit>, String> {
    let window = web_sys::window().ok_or("Fenêtre absente")?;
    let search = window.location().search().map_err(js_error)?;
    let request = governed_kit_request::parse(&search)?;
    if request == KitRequest::None {
        return Ok(None);
    }
    let super::LoadedStudioScene::VisualValley(loaded) = scene else {
        return Err("Le kit requiert la vallée WorldSketch".to_owned());
    };
    status("Vérification du kit local…".to_owned());
    let report: GovernedAssetKitBuildReport =
        serde_json::from_slice(&fetch("build.json", 1024 * 1024).await?)
            .map_err(|error| error.to_string())?;
    let preview: GovernedAssetKitRuntimePreview =
        serde_json::from_slice(&fetch("preview.json", 1024 * 1024).await?)
            .map_err(|error| error.to_string())?;
    report.validate().map_err(|error| error.to_string())?;
    preview
        .validate_against_build_report(&report)
        .map_err(|error| error.to_string())?;
    let sketch =
        forge_world::embedded_visual_valley_world_sketch().map_err(|error| error.to_string())?;
    let plugin =
        VisualValleyGovernedAssetKitPlugin::new(&loaded.terrain, &sketch, report.clone(), preview)
            .map_err(|error| error.to_string())?
            .with_validation_camera(false)
            .with_memory_asset_source();
    let forest = if matches!(
        request,
        KitRequest::ForestPreview | KitRequest::ForestBatches
    ) {
        let catalog = serde_json::from_slice(&fetch("prototype-catalog.json", 1024 * 1024).await?)
            .map_err(|error| error.to_string())?;
        Some(
            VegetationPrototypeLoadPlugin::new(catalog, &report, &loaded.vegetation)?
                .with_memory_asset_source(),
        )
    } else {
        None
    };
    let root = Dir::default();
    let mut total = 0_usize;
    for asset in &report.assets {
        // Contract validation rejects traversal; encoding each component prevents URL metacharacters.
        let path = asset
            .glb
            .path
            .split('/')
            .map(|part| String::from(js_sys::encode_uri_component(part)))
            .collect::<Vec<_>>()
            .join("/");
        let bytes = fetch(&path, 64 * 1024 * 1024).await?;
        total = total
            .checked_add(bytes.len())
            .ok_or("Kit trop volumineux")?;
        if total > 256 * 1024 * 1024 {
            return Err("Kit au-delà de 256 Mio".to_owned());
        }
        let actual = ContentHash::from_bytes(Sha256::digest(&bytes).into());
        if actual != asset.glb.sha256 {
            return Err(format!("Hash GLB incorrect : {}", asset.id));
        }
        let header: &[u8; 20] = bytes
            .get(..20)
            .and_then(|slice| slice.try_into().ok())
            .ok_or("GLB tronqué")?;
        let length = forge_assets::governed_glb_json_length(header, bytes.len() as u64)
            .map_err(|error| error.to_string())?;
        forge_assets::validate_governed_glb_json(&bytes[20..20 + length])
            .map_err(|error| error.to_string())?;
        root.insert_asset(Path::new(&asset.glb.path), bytes);
    }
    status("Kit vérifié · chargement des instances et collisions".to_owned());
    Ok(Some(LoadedKit {
        far_batches: request == KitRequest::ForestBatches,
        root,
        plugin,
        forest,
    }))
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Result::map_err consumes the JavaScript error"
)]
fn js_error(error: wasm_bindgen::JsValue) -> String {
    format!("{error:?}")
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn publish(
    stats: Res<forge_engine::GovernedAssetKitRuntimeStats>,
    ready: Res<forge_engine::GovernedAssetKitReadiness>,
    forest: Option<Res<forge_engine::VegetationPrototypeLoadStatus>>,
    vegetation: Option<Res<forge_engine::VegetationProjectionStats>>,
    mut last: Local<String>,
) {
    let mut text = format!(
        "Kit : {}/{} instances · {} colliders · {}",
        stats.loaded_scene_instances,
        stats.requested_scene_instances,
        ready.registered_colliders,
        if ready.ready {
            "prêt"
        } else if stats.failed_scene_instances + stats.failed_collision_nodes > 0 {
            "échec"
        } else {
            "chargement"
        }
    );
    if let Some(forest) = forest {
        use forge_engine::VegetationPrototypeLoadStatus;
        let detail = if let Some(error) = vegetation
            .as_ref()
            .and_then(|stats| stats.prototype_preview_error.as_ref())
        {
            format!("échec : {error}")
        } else if let VegetationPrototypeLoadStatus::Failed(error) = &*forest {
            format!("chargement refusé : {error}")
        } else if let Some(preview) = vegetation
            .as_ref()
            .and_then(|stats| stats.prototype_preview.as_ref())
        {
            format!(
                "{} arbres · {} primitives · non qualifié",
                preview.trees, preview.primitive_entities
            )
        } else {
            "préparation · rendu non confirmé".to_owned()
        };
        text.push_str(" · Forêt expérimentale : ");
        text.push_str(&detail);
    }
    if *last != text {
        status(text.clone());
        *last = text;
    }
}
