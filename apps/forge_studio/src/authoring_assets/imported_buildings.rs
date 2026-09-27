//! Explicit acquisition of the locally deployed imported building library.
use forge_assets::{tidewater_environment::EnvironmentPackage, tidewater_package::BuildingPackage};
use forge_assets::{tidewater_plants::PlantPackage, tidewater_terrain::TerrainPackage};
use forge_core::ContentHash;
use forge_engine::tidewater_assets::{PreparedLibrary, PreparedMeshes};
use forge_engine::tidewater_terrain::textures::PreparedSurface;
type PreparedBuildings = (PreparedMeshes, PreparedLibrary);
pub(super) enum PlantUpdate {
    Remove,
    Replace(Box<forge_engine::tidewater_plants::textures::PreparedTextures>),
}
pub(super) enum TerrainUpdate {
    Remove,
    Replace(Box<PreparedSurface>),
}
use leptos::prelude::*;
use std::{
    cell::{Cell, RefCell},
    rc::Rc,
};

thread_local! {
    static PREPARED_PLANTS: RefCell<Option<PlantUpdate>> = const { RefCell::new(None) };
    static PLANTS: RefCell<Option<Rc<PlantPackage>>> = const { RefCell::new(None) };
    static TERRAIN: RefCell<Option<Rc<TerrainPackage>>> = const { RefCell::new(None) };
    static PREPARED_TERRAIN: RefCell<Option<TerrainUpdate>> = const { RefCell::new(None) };
    static PREPARED_BUILDINGS: RefCell<Option<PreparedBuildings>> = const { RefCell::new(None) };
    static RENDER_STATUS: RefCell<Option<RwSignal<String>>> = const { RefCell::new(None) };
    static GENERATION: Cell<u64> = const { Cell::new(0) };
    static PUBLISHED: Cell<u64> = const { Cell::new(0) };
    static PREPARED_ENVIRONMENT: RefCell<Option<forge_engine::tidewater_scene_environment::PreparedEnvironment>> = const { RefCell::new(None) };
    static ENVIRONMENT: RefCell<Option<(ContentHash, Rc<EnvironmentPackage>)>> = const { RefCell::new(None) };
    static PACKAGE: RefCell<Option<(ContentHash, Rc<BuildingPackage>)>> = const { RefCell::new(None) };
}

#[derive(serde::Deserialize)]
#[serde(deny_unknown_fields)]
struct Candidate {
    schema: String,
    version: u32,
    package_sha256: ContentHash,
    #[serde(default)]
    environment_sha256: Option<ContentHash>,
    #[serde(default)]
    terrain_sha256: Option<ContentHash>,
    #[serde(default)]
    plants_sha256: Option<ContentHash>,
}

fn next() -> u64 {
    GENERATION.with(|value| {
        let next = value.get().wrapping_add(1);
        value.set(next);
        next
    })
}
fn current(ticket: u64) -> bool {
    GENERATION.with(Cell::get) == ticket
}

async fn load(ticket: u64) -> Result<(usize, bool), String> {
    use crate::governed_kit::transport;
    let candidate: Candidate =
        serde_json::from_slice(&transport::fetch_building_candidate().await?)
            .map_err(|e| e.to_string())?;
    if candidate.schema != "forge.imported-building-library" || candidate.version != 1 {
        return Err("Bibliothèque de bâtiments non reconnue".into());
    }
    let hash = candidate.package_sha256;
    let manifest = transport::fetch_building_payload(hash, "package.json", 2 * 1024 * 1024).await?;
    let package = BuildingPackage::load_async(&manifest, hash, |name, limit| async move {
        if !current(ticket) {
            return Err("Chargement remplacé".into());
        }
        transport::fetch_building_payload(hash, &name, limit).await
    })
    .await?;
    if !current(ticket) {
        return Err("Chargement remplacé".into());
    }
    let mut prepared_environment = None;
    let environment = if let Some(pin) = candidate.environment_sha256 {
        let manifest = transport::fetch_building_payload(pin, "package.json", 256 * 1024).await?;
        let resources = EnvironmentPackage::load_async(&manifest, pin, |name, limit| async move {
            if !current(ticket) {
                return Err("Chargement remplacé".into());
            }
            transport::fetch_building_payload(pin, &name, limit).await
        })
        .await?;
        prepared_environment = Some(
            forge_engine::tidewater_scene_environment::PreparedEnvironment::new(&resources)
                .map_err(|e| e.to_string())?,
        );
        Some((pin, Rc::new(resources)))
    } else {
        None
    };
    if !current(ticket) {
        return Err("Chargement remplacé".into());
    }
    let terrain = if let Some(pin) = candidate.terrain_sha256 {
        let manifest = transport::fetch_building_payload(pin, "package.json", 16 * 1024).await?;
        let resources = TerrainPackage::load_async(&manifest, pin, |name, limit| async move {
            if !current(ticket) {
                return Err("Chargement remplacé".into());
            }
            transport::fetch_building_payload(pin, &name, limit).await
        })
        .await?;
        Some(Rc::new(resources))
    } else {
        None
    };
    if !current(ticket) {
        return Err("Chargement remplacé".into());
    }
    let plants = load_plants(ticket, candidate.plants_sha256).await?;
    if !current(ticket) {
        return Err("Chargement remplacé".into());
    }
    let prepared_terrain = terrain
        .as_ref()
        .map(|p| PreparedSurface::from_package(p))
        .transpose()?;
    let prepared_plants = prepare_plants(plants.as_deref())?;
    let geometry = PreparedMeshes::load(&package)?;
    let library = PreparedLibrary::load(&package)?;
    let has_environment = environment.is_some();
    let count = package.catalog().assets.len();
    PREPARED_PLANTS.with(|slot| slot.replace(Some(prepared_plants)));
    PLANTS.with(|slot| slot.replace(plants));
    TERRAIN.with(|slot| slot.replace(terrain));
    PREPARED_TERRAIN.with(|slot| {
        slot.replace(Some(
            prepared_terrain
                .map(Box::new)
                .map_or(TerrainUpdate::Remove, TerrainUpdate::Replace),
        ))
    });
    PREPARED_ENVIRONMENT.with(|slot| slot.replace(prepared_environment));
    ENVIRONMENT.with(|slot| slot.replace(environment));
    PACKAGE.with(|slot| slot.replace(Some((hash, Rc::new(package)))));
    PREPARED_BUILDINGS.with(|slot| slot.replace(Some((geometry, library))));
    PUBLISHED.with(|slot| slot.set(ticket));
    super::placements::retry_library();
    super::imported_environment::hide_preview();
    if !has_environment {
        render_status("Cette bibliothèque ne fournit pas d’environnement.".into());
    }
    crate::authoring_viewport::retry_imported();
    Ok((count, has_environment))
}

async fn load_plants(
    ticket: u64,
    pin: Option<ContentHash>,
) -> Result<Option<Rc<PlantPackage>>, String> {
    use crate::governed_kit::transport;
    let Some(pin) = pin else {
        return Ok(None);
    };
    let manifest = transport::fetch_building_payload(pin, "package.json", 256 * 1024).await?;
    let resources = PlantPackage::load_async(&manifest, pin, |name, limit| async move {
        if !current(ticket) {
            return Err("Chargement remplacé".into());
        }
        transport::fetch_building_payload(pin, &name, limit).await
    })
    .await?;
    Ok(Some(Rc::new(resources)))
}

fn prepare_plants(package: Option<&PlantPackage>) -> Result<PlantUpdate, String> {
    package
        .map(forge_engine::tidewater_plants::textures::PreparedTextures::from_package)
        .transpose()
        .map(|value| {
            value
                .map(Box::new)
                .map_or(PlantUpdate::Remove, PlantUpdate::Replace)
        })
}
pub(super) fn take_plants() -> Option<PlantUpdate> {
    PREPARED_PLANTS.with(|slot| slot.borrow_mut().take())
}
pub(super) fn restore_plants() -> Result<(), String> {
    if PREPARED_PLANTS.with(|slot| slot.borrow().is_some()) {
        return Ok(());
    }
    let prepared = PLANTS.with(|slot| prepare_plants(slot.borrow().as_deref()))?;
    PREPARED_PLANTS.with(|slot| slot.replace(Some(prepared)));
    Ok(())
}

pub(crate) fn plant_package() -> Option<Rc<PlantPackage>> {
    PLANTS.with(|slot| slot.borrow().clone())
}

pub(crate) fn terrain_package() -> Option<Rc<TerrainPackage>> {
    TERRAIN.with(|slot| slot.borrow().clone())
}

pub(crate) fn building_package() -> Option<Rc<BuildingPackage>> {
    PACKAGE.with(|slot| slot.borrow().as_ref().map(|(_, package)| package.clone()))
}

pub(crate) fn contains_asset(id: forge_core::AssetId) -> bool {
    PACKAGE.with(|slot| {
        slot.borrow().as_ref().is_some_and(|(_, package)| {
            package
                .catalog()
                .assets
                .iter()
                .any(|record| record.id == id)
        })
    })
}

pub(crate) fn published_generation() -> u64 {
    PUBLISHED.with(Cell::get)
}

pub(crate) fn has_environment() -> bool {
    ENVIRONMENT.with(|slot| slot.borrow().is_some())
}

// None means no change; explicit removal cannot retain a previous library.
pub(super) fn take_terrain() -> Option<TerrainUpdate> {
    PREPARED_TERRAIN.with(|slot| slot.borrow_mut().take())
}

pub(super) fn restore_terrain() -> Result<(), String> {
    if PREPARED_TERRAIN.with(|slot| slot.borrow().is_some()) {
        return Ok(());
    }
    let prepared = TERRAIN.with(|slot| {
        slot.borrow()
            .as_ref()
            .map(|package| PreparedSurface::from_package(package))
            .transpose()
    })?;
    PREPARED_TERRAIN.with(|slot| {
        slot.replace(Some(
            prepared
                .map(Box::new)
                .map_or(TerrainUpdate::Remove, TerrainUpdate::Replace),
        ))
    });
    Ok(())
}

pub(super) fn take_buildings() -> Option<PreparedBuildings> {
    PREPARED_BUILDINGS.with(|slot| slot.borrow_mut().take())
}

pub(super) fn restore_buildings() -> Result<(), String> {
    if PREPARED_BUILDINGS.with(|slot| slot.borrow().is_some()) {
        return Ok(());
    }
    let prepared = PACKAGE.with(|slot| {
        slot.borrow()
            .as_ref()
            .map(|(_, package)| {
                Ok::<_, String>((
                    PreparedMeshes::load(package)?,
                    PreparedLibrary::load(package)?,
                ))
            })
            .transpose()
    })?;
    PREPARED_BUILDINGS.with(|slot| slot.replace(prepared));
    Ok(())
}

pub(super) fn take_environment()
-> Option<forge_engine::tidewater_scene_environment::PreparedEnvironment> {
    PREPARED_ENVIRONMENT.with(|slot| slot.borrow_mut().take())
}

pub(super) fn restore_environment() -> Result<(), String> {
    if PREPARED_ENVIRONMENT.with(|slot| slot.borrow().is_some()) {
        return Ok(());
    }
    let prepared = ENVIRONMENT.with(|slot| {
        slot.borrow()
            .as_ref()
            .map(|(_, package)| {
                forge_engine::tidewater_scene_environment::PreparedEnvironment::new(package)
                    .map_err(|e| e.to_string())
            })
            .transpose()
    })?;
    PREPARED_ENVIRONMENT.with(|slot| slot.replace(prepared));
    Ok(())
}

pub(super) fn render_status(message: String) {
    RENDER_STATUS.with(|slot| {
        if let Some(status) = *slot.borrow() {
            status.set(message);
        }
    });
}

#[component]
pub(super) fn LibraryControls() -> impl IntoView {
    let busy = RwSignal::new(false);
    let renderer = RwSignal::new(String::new());
    RENDER_STATUS.with(|slot| slot.replace(Some(renderer)));
    let status = RwSignal::new("Bibliothèque importée non vérifiée.".to_owned());
    on_cleanup(|| {
        RENDER_STATUS.with(|slot| slot.replace(None));
        next();
    });
    view! {
        <details>
            <summary>"Bibliothèque de bâtiments importés"</summary>
            <button disabled=move || busy.get() on:click=move |_| {
                if busy.get_untracked() { return; }
                let ticket = next();
                busy.set(true);
                status.set("Vérification de la bibliothèque locale…".into());
                wasm_bindgen_futures::spawn_local(async move {
                    let result = load(ticket).await;
                    if !current(ticket) { return; }
                    busy.set(false);
                    status.set(match result {
                        Ok((count, environment)) => {
                            let sky = if environment { " Ressources de ciel et de nuages vérifiées et chargées." } else { "" };
                            let plants = if plant_package().is_some() { " Ressources végétales chargées ; leur affichage reste à raccorder." } else { "" };
                            format!("{count} bâtiments vérifiés et chargés en mémoire.{sky}{plants} Les bâtiments déjà placés seront projetés dans la carte importée. L’ajout depuis cette bibliothèque reste à raccorder.")
                        },
                        Err(error) => format!("Bibliothèque indisponible : {error}"),
                    });
                });
            }>"Vérifier et charger la bibliothèque"</button>
            <output aria-live="polite">{move || status.get()}</output>
            <output aria-live="polite">{move || renderer.get()}</output>
            <button disabled=move || busy.get() on:click=move |_| {
                if ENVIRONMENT.with(|slot| slot.borrow().is_none()) {
                    render_status("Chargez une bibliothèque contenant un environnement.".into());
                    return;
                }
                if crate::authoring_viewport::can_preview_asset() {
                    super::imported_environment::preview();
                } else {
                    render_status("Ouvrez une zone dans l'éditeur pour calculer l'aperçu.".into());
                }
            }>"Afficher le ciel de référence"</button>
            <canvas id="forge-imported-sky-preview" width="1920" height="1080" hidden=true
                style="width:100%;max-width:480px;height:auto" aria-label="Aperçu fixe du ciel importé"/>

        </details>
    }
}
