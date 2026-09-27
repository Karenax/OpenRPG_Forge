//! Verified catalog delivery for accepted authoring heads; no source mutation.
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
use forge_assets::{GovernedAssetKitBuildReport, VegetationPrototypeCatalog};
use forge_core::ContentHash;
use forge_project::zone_authoring::ZoneAuthoringHead;
use std::{cell::RefCell, path::Path};
mod palette;
pub(crate) mod selection;
pub(super) use palette::SpeciesControls;

pub(super) fn preview_label(
    state: &forge_project::authoring::AuthoringState,
    receipt: &forge_project::zone_authoring::session::ZoneReceipt,
) -> Option<String> {
    if receipt.applied.species == receipt.inverse.species {
        return None;
    }
    crate::authoring_preview::clear();
    let result = state
        .zone_head
        .as_ref()
        .ok_or_else(|| "Zone absente".to_owned())
        .and_then(|head| crate::authoring_vegetation::preview_species(head, receipt));
    let names = receipt
        .applied
        .species
        .as_deref()
        .unwrap_or_default()
        .iter()
        .map(|l| {
            format!(
                "{} : {}",
                l.layer_id,
                l.prototypes
                    .iter()
                    .map(|p| p.asset_id.as_str())
                    .collect::<Vec<_>>()
                    .join(", ")
            )
        })
        .collect::<Vec<_>>()
        .join(" ; ");
    Some(format!(
        "Espèces · non appliquées · {}. {}. Appliquer puis sauvegarder ; Abandonner restaure le choix accepté.",
        if names.is_empty() {
            "retour aux arbres procéduraux"
        } else {
            &names
        },
        result.map_or_else(
            |e| format!("Aperçu indisponible : {e}"),
            |()| "Chargement de l'aperçu demandé ; consulter l'état de végétation".into()
        )
    ))
}

struct Prepared {
    loader: forge_engine::VegetationPrototypeLoadPlugin,
    files: Vec<(String, Vec<u8>)>,
}
#[derive(Default)]
struct Delivery {
    epoch: u64,
    desired: Option<ContentHash>,
    pending: Option<Prepared>,
    error: Option<String>,
    loading: bool,
    submitted: bool,
    loader: Option<forge_engine::VegetationPrototypeLoadPlugin>,
    resident: Vec<String>,
}
thread_local! {
    static ACCEPTED_HEAD: RefCell<Option<ZoneAuthoringHead>> = const { RefCell::new(None) };
    static ROOT: Dir = Dir::default();
    static DELIVERY: RefCell<Delivery> = RefCell::new(Delivery::default());
}

pub(super) fn register(app: &mut App) {
    let root = ROOT.with(Clone::clone);
    app.register_asset_source(
        "forge-species",
        AssetSourceBuilder::new(move || Box::new(MemoryAssetReader { root: root.clone() })),
    );
}

pub(super) fn observe(head: Option<&ZoneAuthoringHead>) {
    ACCEPTED_HEAD.with(|slot| slot.replace(head.cloned()));
    show(head);
}

pub(super) fn restore() {
    ACCEPTED_HEAD.with(|slot| show(slot.borrow().as_ref()));
}

pub(super) fn preview(head: &ZoneAuthoringHead) {
    show(Some(head));
}

fn show(head: Option<&ZoneAuthoringHead>) {
    let desired = head.filter(|h| {
        h.corrections
            .species
            .as_ref()
            .is_some_and(|s| !s.is_empty())
    });
    let hash = match desired.map(ZoneAuthoringHead::content_hash).transpose() {
        Ok(hash) => hash,
        Err(error) => {
            DELIVERY.with(|slot| slot.borrow_mut().error = Some(error));
            return;
        }
    };
    let epoch = DELIVERY.with(|slot| {
        let mut state = slot.borrow_mut();
        // A repeated user request may retry a failed fetch for the same head.
        // Keep in-flight and successful deliveries deduplicated. This function
        // runs on session/preview events, never from the per-frame readiness poll.
        if state.desired == hash && state.error.is_none() {
            return None;
        }
        state.epoch = state.epoch.wrapping_add(1);
        state.desired = hash;
        state.pending = None;
        state.error = None;
        state.submitted = false;
        state.loader = None;
        state.loading = desired.is_some();
        Some(state.epoch)
    });
    let (Some(epoch), Some(head)) = (epoch, desired.cloned()) else {
        return;
    };
    wasm_bindgen_futures::spawn_local(async move {
        let result = prepare(&head, epoch).await;
        DELIVERY.with(|slot| {
            let mut state = slot.borrow_mut();
            if state.epoch != epoch {
                return;
            }
            state.loading = false;
            match result {
                Ok(prepared) => state.pending = Some(prepared),
                Err(error) => state.error = Some(error),
            }
        });
    });
}

fn current(epoch: u64) -> Result<(), String> {
    DELIVERY.with(|slot| {
        if slot.borrow().epoch == epoch {
            Ok(())
        } else {
            Err("Chargement d'espèces remplacé".into())
        }
    })
}

async fn prepare(head: &ZoneAuthoringHead, epoch: u64) -> Result<Prepared, String> {
    let fetch = crate::governed_kit::transport::fetch;
    let kit: GovernedAssetKitBuildReport =
        serde_json::from_slice(&fetch("build.json", 1024 * 1024).await?)
            .map_err(|e| e.to_string())?;
    current(epoch)?;
    kit.validate().map_err(|e| e.to_string())?;
    let source: VegetationPrototypeCatalog =
        serde_json::from_slice(&fetch("prototype-catalog.json", 1024 * 1024).await?)
            .map_err(|e| e.to_string())?;
    current(epoch)?;
    let catalog = forge_engine::resolve_authored_species(head, &[source], &kit)?
        .ok_or("Sélection d'espèces absente")?;
    let (_, vegetation) = head.effective()?;
    let loader = forge_engine::VegetationPrototypeLoadPlugin::new(
        catalog.clone(),
        &kit,
        &vegetation.ok_or("Végétation absente")?,
    )?
    .with_species_memory_source();
    let mut files = Vec::new();
    let mut total = 0;
    let mut seen = std::collections::BTreeSet::new();
    for binding in &catalog.bindings {
        if !seen.insert(binding.glb_sha256) {
            continue;
        }
        current(epoch)?;
        let asset = kit
            .assets
            .iter()
            .find(|asset| asset.id == binding.asset_id)
            .ok_or("Asset absent du kit")?;
        let bytes = crate::authoring_assets::verify::check(asset).await?;
        current(epoch)?;
        total += bytes.len();
        if total > 256 * 1024 * 1024 {
            return Err("Espèces au-delà de 256 Mio".into());
        }
        files.push((format!("{}.glb", binding.glb_sha256), bytes));
    }
    Ok(Prepared { loader, files })
}

/// Gate source submission on fully resolved prototypes, never on fetch success alone.
pub(super) fn ready(world: &mut World) -> Result<bool, String> {
    DELIVERY.with(|slot| {
        let mut state = slot.borrow_mut();
        if let Some(error) = &state.error {
            return Err(error.clone());
        }
        if state.desired.is_none() {
            forge_engine::stop_governed_forest(world)?;
            if !state.resident.is_empty() {
                ROOT.with(|root| {
                    for path in state.resident.drain(..) {
                        root.remove_asset(Path::new(&path));
                    }
                });
                forge_engine::VegetationPrototypeLoadPlugin::clear(world);
            }
            return Ok(true);
        }
        if state.loading {
            return Ok(false);
        }
        if let Some(prepared) = state.pending.take() {
            ROOT.with(|root| {
                for path in state.resident.drain(..) {
                    root.remove_asset(Path::new(&path));
                }
                for (path, bytes) in prepared.files {
                    root.insert_asset(Path::new(&path), bytes);
                    state.resident.push(path);
                }
            });
            state.loader = Some(prepared.loader.clone());
            prepared.loader.request_reload(world)?;
            state.submitted = true;
        }
        if !state.submitted {
            return Ok(false);
        }
        if world
            .get_resource::<forge_engine::VegetationPrototypeCache>()
            .is_some_and(|cache| cache.selection_catalog.is_none())
            && matches!(
                world.get_resource::<forge_engine::VegetationPrototypeLoadStatus>(),
                Some(forge_engine::VegetationPrototypeLoadStatus::Ready { .. })
            )
            && let Some(loader) = state.loader.clone()
        {
            loader.request_reload(world)?;
            return Ok(false);
        }
        if let Some(forge_engine::VegetationPrototypeLoadStatus::Failed(error)) =
            world.get_resource::<forge_engine::VegetationPrototypeLoadStatus>()
        {
            return Err(error.clone());
        }
        if matches!(
            world.get_resource::<forge_engine::VegetationPrototypeLoadStatus>(),
            Some(forge_engine::VegetationPrototypeLoadStatus::Ready { .. })
        ) && let Some(cache) = world.get_resource::<forge_engine::VegetationPrototypeWindCache>()
        {
            if let Some(error) = &cache.failure {
                return Err(error.clone());
            }
            if !cache.layers.is_empty() {
                world.insert_resource(forge_engine::VegetationPrototypeForestPreview);
                return Ok(true);
            }
        }
        Ok(false)
    })
}
