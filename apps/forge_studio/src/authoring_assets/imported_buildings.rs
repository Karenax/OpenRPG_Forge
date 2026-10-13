mod catalog;
mod debris;
pub(super) use catalog::entries as catalog_entries;
pub(crate) use debris::Prepared as PreparedDebrisInput;

pub(crate) fn debris_input() -> Option<Rc<PreparedDebrisInput>> {
    debris::input()
}

pub(crate) fn debris_templates() -> Vec<forge_engine::tidewater_debris::DebrisTemplate> {
    debris::templates()
}

pub(crate) fn is_debris_asset(asset: forge_core::AssetId) -> bool {
    debris::contains(asset)
}
mod source_objects;
pub(crate) use source_objects::source_placements;
// Explicit acquisition of the locally deployed imported building library.
use bevy::ecs as bevy_ecs;
use forge_assets::{
    tidewater_beach_props::BeachPropPackage, tidewater_boardwalk::BoardwalkPackage,
    tidewater_environment::EnvironmentPackage, tidewater_fish_props::FishPropPackage,
    tidewater_fish_stand::FishStandPackage, tidewater_game_fish::GameFishPackage,
    tidewater_ground_bounce::GroundBouncePackage, tidewater_ocean::OceanPackage,
    tidewater_package::BuildingPackage, tidewater_pier::PierPackage, tidewater_plaza::PlazaPackage,
    tidewater_shore_sim::ShoreSimPackage, tidewater_village_props::VillagePropPackage,
    tidewater_water_surface::WaterSurfacePackage,
};
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
    static FISH_STAND: RefCell<Option<Rc<FishStandPackage>>> = const { RefCell::new(None) };
    static PREPARED_FISH_STAND: RefCell<Option<forge_engine::tidewater_stall::FishStandPrepared>> = const { RefCell::new(None) };
    static FISH_STAND_READY: RefCell<Option<RwSignal<bool>>> = const { RefCell::new(None) };
    static RENDER_STATUS: RefCell<Option<RwSignal<String>>> = const { RefCell::new(None) };
    static GENERATION: Cell<u64> = const { Cell::new(0) };
    static PUBLISHED: Cell<u64> = const { Cell::new(0) };
    static PROJECTION_LOADING: Cell<bool> = const { Cell::new(false) };
    static PREPARED_ENVIRONMENT: RefCell<Option<forge_engine::tidewater_scene_environment::PreparedEnvironment>> = const { RefCell::new(None) };
    static ENVIRONMENT: RefCell<Option<(ContentHash, Rc<EnvironmentPackage>)>> = const { RefCell::new(None) };
    static ENVIRONMENT_PROGRAMS: RefCell<Option<Rc<forge_assets::tidewater_environment::programs::ProgramsPackage>>> = const { RefCell::new(None) };
    static OCEAN: RefCell<Option<(ContentHash, Rc<OceanPackage>)>> = const { RefCell::new(None) };
    static WATER_SURFACE: RefCell<Option<(ContentHash, Rc<WaterSurfacePackage>)>> = const { RefCell::new(None) };
    static SHORE_SIM: RefCell<Option<(ContentHash, Rc<ShoreSimPackage>)>> = const { RefCell::new(None) };
    static GROUND_BOUNCE: RefCell<Option<(ContentHash, Rc<GroundBouncePackage>)>> = const { RefCell::new(None) };
    static BOARDWALK: RefCell<Option<(ContentHash, Rc<BoardwalkPackage>)>> = const { RefCell::new(None) };
    static BOARDWALK_TEMPLATES: RefCell<Vec<forge_engine::tidewater_boardwalk::BoardwalkTemplate>> = const { RefCell::new(Vec::new()) };
    static BOARDWALK_READY: RefCell<Option<RwSignal<bool>>> = const { RefCell::new(None) };
    static PIER: RefCell<Option<(ContentHash, Rc<PierPackage>)>> = const { RefCell::new(None) };
    static PIER_MOTION: RefCell<Option<Rc<forge_assets::tidewater_pier_motion::PierMotionPackage>>> = const { RefCell::new(None) };
    static PIER_TEMPLATE: RefCell<Option<forge_engine::tidewater_pier::PierTemplate>> = const { RefCell::new(None) };
    static PIER_READY: RefCell<Option<RwSignal<bool>>> = const { RefCell::new(None) };
    static PLAZA: RefCell<Option<(ContentHash, Rc<PlazaPackage>)>> = const { RefCell::new(None) };
    static PLAZA_TEMPLATES: RefCell<Vec<forge_engine::tidewater_plaza::PlazaTemplate>> = const { RefCell::new(Vec::new()) };
    static VILLAGE_PROPS: RefCell<Option<(ContentHash, Rc<VillagePropPackage>)>> = const { RefCell::new(None) };
    static VILLAGE_PROPS_TEMPLATES: RefCell<Vec<forge_engine::tidewater_village_props::VillagePropsTemplate>> = const { RefCell::new(Vec::new()) };
    static BEACH_PROPS: RefCell<Option<(ContentHash, Rc<BeachPropPackage>)>> = const { RefCell::new(None) };
    static BEACH_PROPS_TEMPLATES: RefCell<Vec<forge_engine::tidewater_beach_props::BeachPropsTemplate>> = const { RefCell::new(Vec::new()) };
    static FISH_PROPS: RefCell<Option<(ContentHash, Rc<FishPropPackage>)>> = const { RefCell::new(None) };
    static GAME_FISH: RefCell<Option<Rc<GameFishPackage>>> = const { RefCell::new(None) };
    static FISH_PROPS_TEMPLATES: RefCell<Vec<forge_engine::tidewater_fish_props::FishPropsTemplate>> = const { RefCell::new(Vec::new()) };
    static PLAZA_READY: RefCell<Option<RwSignal<bool>>> = const { RefCell::new(None) };
    static PACKAGE: RefCell<Option<(ContentHash, Rc<BuildingPackage>)>> = const { RefCell::new(None) };
    static LOCAL_LIGHTS: RefCell<Option<std::sync::Arc<forge_assets::source_local_lights::SourceLocalLightsPackage>>> = const { RefCell::new(None) };
}

const RESUME_KEY: &str = "forge.imported-library.resume.v1";

fn resume_store() -> Option<web_sys::Storage> {
    web_sys::window()?.session_storage().ok().flatten()
}

fn restore_choice(
    document: &forge_project::TerrainDocument,
) -> Result<Option<crate::library_resume::LibraryResume>, String> {
    let Some(store) = resume_store() else {
        return Ok(None);
    };
    let Some(json) = store
        .get_item(RESUME_KEY)
        .map_err(|_| "Choix de bibliothèque illisible")?
    else {
        return Ok(None);
    };
    let choice: crate::library_resume::LibraryResume = serde_json::from_str(&json)
        .map_err(|_| "Choix de bibliothèque invalide. Rechargez explicitement la bibliothèque.")?;
    Ok(choice.matches(document).then_some(choice))
}

fn remember_choice(document: &forge_project::TerrainDocument, hash: ContentHash) {
    let Some(choice) = crate::library_resume::LibraryResume::new(document, hash) else {
        return;
    };
    if let Some(store) = resume_store()
        && let Ok(json) = serde_json::to_string(&choice)
        && store.set_item(RESUME_KEY, &json).is_err()
    {
        web_sys::console::warn_1(
            &"Bibliothèque vérifiée, mais choix de session non mémorisé.".into(),
        );
    }
}

/// Resolve the deployed source resources when the author opens an imported map.
/// Every package and its raster binding are verified before projection.
pub(crate) fn ensure_for_projection(
    document: &forge_project::TerrainDocument,
    playtest: bool,
) -> Result<bool, String> {
    if building_package().is_some()
        && terrain_package().is_some()
        && plant_package().is_some()
        && has_environment()
    {
        source_objects::verify_projection_source(
            document,
            terrain_package().as_deref(),
            plant_package().as_deref(),
        )?;
        return Ok(true);
    }
    let choice = restore_choice(document)?;
    if choice.is_none() && !playtest && !crate::authoring_viewport::requested() {
        return Err("Charger les ressources du terrain importé avant sa projection.".into());
    }
    if PROJECTION_LOADING.with(|loading| loading.replace(true)) {
        return Ok(false);
    }
    let ticket = next();
    let source = (document.area_id, document.sampled_source.clone());
    crate::authoring_viewport::report("Chargement du terrain et du décor…".into());
    wasm_bindgen_futures::spawn_local(async move {
        let result = load(ticket, choice, LibraryPurpose::Projection).await;
        PROJECTION_LOADING.with(|loading| loading.set(false));
        if !current(ticket) {
            return;
        }
        if let Err(error) = result {
            if crate::authoring_viewport::imported_document()
                .is_some_and(|active| (active.area_id, active.sampled_source) != source)
            {
                // Retry the newly selected map, never an unchanged failing package.
                crate::authoring_viewport::retry_imported();
                return;
            }
            crate::authoring_viewport::report(format!("Bibliothèque indisponible : {error}"));
            crate::authoring_loading::finish();
        }
    });
    Ok(false)
}

#[derive(Clone, Copy)]
enum LibraryPurpose {
    Projection,
    Catalog,
}

#[derive(serde::Deserialize)]
#[serde(deny_unknown_fields)]
struct Candidate {
    schema: String,
    version: u32,
    package_sha256: ContentHash,
    #[serde(default)]
    source_local_lights_sha256: Option<ContentHash>,
    #[serde(default)]
    environment_sha256: Option<ContentHash>,
    #[serde(default)]
    environment_programs_sha256: Option<ContentHash>,
    #[serde(default)]
    ocean_sha256: Option<ContentHash>,
    #[serde(default)]
    water_surface_sha256: Option<ContentHash>,
    #[serde(default)]
    shore_sim_sha256: Option<ContentHash>,
    #[serde(default)]
    ground_bounce_sha256: Option<ContentHash>,
    #[serde(default)]
    boardwalk_sha256: Option<ContentHash>,
    #[serde(default)]
    pier_sha256: Option<ContentHash>,
    #[serde(default)]
    pier_motion_sha256: Option<ContentHash>,
    #[serde(default)]
    plaza_sha256: Option<ContentHash>,
    #[serde(default)]
    village_props_sha256: Option<ContentHash>,
    #[serde(default)]
    beach_props_sha256: Option<ContentHash>,
    #[serde(default)]
    debris_native_sha256: Option<ContentHash>,
    #[serde(default)]
    fish_props_sha256: Option<ContentHash>,
    #[serde(default)]
    game_fish_sha256: Option<ContentHash>,
    #[serde(default)]
    terrain_sha256: Option<ContentHash>,
    #[serde(default)]
    plants_sha256: Option<ContentHash>,
    #[serde(default)]
    fish_stand_bundle_sha256: Option<ContentHash>,
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

#[allow(
    clippy::too_many_lines,
    reason = "Package verification and ticket checks retain their ordered refusal and publication steps."
)]
async fn load(
    ticket: u64,
    expected: Option<crate::library_resume::LibraryResume>,
    purpose: LibraryPurpose,
) -> Result<(usize, bool), String> {
    use crate::governed_kit::transport;
    let document = crate::authoring_viewport::imported_document();
    let bytes = transport::fetch_building_candidate().await?;
    let candidate_hash = forge_core::bytes_sha256(&bytes);
    if let Some(choice) = &expected {
        if !document
            .as_ref()
            .is_some_and(|document| choice.matches(document))
        {
            return Err("La zone ne correspond plus au choix de bibliothèque.".into());
        }
        choice.verify_candidate(candidate_hash)?;
    }
    let candidate: Candidate = serde_json::from_slice(&bytes).map_err(|e| e.to_string())?;
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
    let environment_programs = if let Some(pin) = candidate.environment_programs_sha256 {
        if environment.is_none() {
            return Err("Programmes d'environnement sans ressources de scène".into());
        }
        let manifest =
            transport::fetch_building_payload(pin, "environment-programs.json", 2_000_000).await?;
        let package = forge_assets::tidewater_environment::programs::ProgramsPackage::load_async(
            &manifest,
            pin,
            |name, limit| async move {
                if !current(ticket) {
                    return Err("Chargement remplacé".into());
                }
                transport::fetch_building_payload(pin, &name, limit).await
            },
        )
        .await?;
        // Validate kernel identities/layouts before publishing any library resources.
        forge_engine::tidewater_environment::programs::Input::from_package(&package)?;
        Some(Rc::new(package))
    } else {
        None
    };
    if !current(ticket) {
        return Err("Chargement remplacé".into());
    }
    let ocean = if let Some(pin) = candidate.ocean_sha256 {
        // Acquired v2 packages include the uniforms of every bootstrap frame.
        // Match the bounded reader; it still enforces the tighter v1 limit.
        let manifest = transport::fetch_building_payload(pin, "package.json", 256 * 1024).await?;
        let resources = OceanPackage::load_async(&manifest, pin, |name, limit| async move {
            if !current(ticket) {
                return Err("Chargement remplacé".into());
            }
            transport::fetch_building_payload(pin, &name, limit).await
        })
        .await?;
        Some((pin, Rc::new(resources)))
    } else {
        None
    };
    let water_surface = if let Some(pin) = candidate.water_surface_sha256 {
        let manifest = transport::fetch_building_payload(pin, "package.json", 12 * 1024).await?;
        let resources = WaterSurfacePackage::load_async(&manifest, pin, |name, limit| async move {
            if !current(ticket) {
                return Err("Chargement remplacé".into());
            }
            transport::fetch_building_payload(pin, &name, limit).await
        })
        .await?;
        Some((pin, Rc::new(resources)))
    } else {
        None
    };
    if !current(ticket) {
        return Err("Chargement remplacé".into());
    }
    let shore_sim = if let Some(pin) = candidate.shore_sim_sha256 {
        let manifest = transport::fetch_building_payload(pin, "package.json", 12 * 1024).await?;
        let resources = ShoreSimPackage::load_async(&manifest, pin, |name, limit| async move {
            if !current(ticket) {
                return Err("Chargement remplacé".into());
            }
            transport::fetch_building_payload(pin, &name, limit).await
        })
        .await?;
        Some((pin, Rc::new(resources)))
    } else {
        None
    };
    let ground_bounce = if let Some(pin) = candidate.ground_bounce_sha256 {
        let manifest = transport::fetch_building_payload(pin, "package.json", 4 * 1024).await?;
        let resources = GroundBouncePackage::load_async(&manifest, pin, |name, limit| async move {
            if !current(ticket) {
                return Err("Chargement remplacé".into());
            }
            transport::fetch_building_payload(pin, &name, limit).await
        })
        .await?;
        Some((pin, Rc::new(resources)))
    } else {
        None
    };
    let (boardwalk, boardwalk_templates) = if let Some(pin) = candidate.boardwalk_sha256 {
        let manifest = transport::fetch_building_payload(pin, "package.json", 16 * 1024).await?;
        let resources = BoardwalkPackage::load_async(&manifest, pin, |name, limit| async move {
            if !current(ticket) {
                return Err("Chargement remplacé".into());
            }
            transport::fetch_building_payload(pin, &name, limit).await
        })
        .await?;
        let material_hash = forge_core::bytes_sha256(
            package
                .file("materials.json")
                .ok_or("Matériaux du village absents")?,
        );
        if material_hash.to_string() != resources.material_library_hash() {
            return Err("Passerelles incompatibles avec les matériaux du village".into());
        }
        let prepared =
            forge_engine::tidewater_boardwalk::PreparedBoardwalk::from_package(&resources)?;
        (Some((pin, Rc::new(resources))), prepared.templates())
    } else {
        (None, Vec::new())
    };
    let (pier, pier_template) = if let Some(pin) = candidate.pier_sha256 {
        let manifest = transport::fetch_building_payload(pin, "package.json", 64 * 1024).await?;
        let resources = PierPackage::load_async(&manifest, pin, |name, limit| async move {
            if !current(ticket) {
                return Err("Chargement remplacé".into());
            }
            transport::fetch_building_payload(pin, &name, limit).await
        })
        .await?;
        let material_hash = forge_core::bytes_sha256(
            package
                .file("materials.json")
                .ok_or("Matériaux du village absents")?,
        );
        if material_hash.to_string() != resources.material_library_hash() {
            return Err("Quai incompatible avec les matériaux du village".into());
        }
        let prepared = forge_engine::tidewater_pier::PreparedPier::from_package(&resources)?;
        (Some((pin, Rc::new(resources))), Some(prepared.template()))
    } else {
        (None, None)
    };
    let (plaza, plaza_templates) = if let Some(pin) = candidate.plaza_sha256 {
        let manifest = transport::fetch_building_payload(pin, "package.json", 16 * 1024).await?;
        let resources = PlazaPackage::load_async(&manifest, pin, |name, limit| async move {
            if !current(ticket) {
                return Err("Chargement remplacé".into());
            }
            transport::fetch_building_payload(pin, &name, limit).await
        })
        .await?;
        let material_hash = forge_core::bytes_sha256(
            package
                .file("materials.json")
                .ok_or("Matériaux du village absents")?,
        );
        if material_hash.to_string() != resources.material_library_hash() {
            return Err("Objets de la place incompatibles avec les matériaux du village".into());
        }
        let prepared = forge_engine::tidewater_plaza::PreparedPlaza::from_package(&resources)?;
        (Some((pin, Rc::new(resources))), prepared.templates())
    } else {
        (None, Vec::new())
    };
    let (village_props, village_props_templates) = if let Some(pin) = candidate.village_props_sha256
    {
        let manifest = transport::fetch_building_payload(pin, "package.json", 128 * 1024).await?;
        let resources = VillagePropPackage::load_async(&manifest, pin, |name, limit| async move {
            if !current(ticket) {
                return Err("Chargement remplacé".into());
            }
            transport::fetch_building_payload(pin, &name, limit).await
        })
        .await?;
        let material_hash = forge_core::bytes_sha256(
            package
                .file("materials.json")
                .ok_or("Matériaux du village absents")?,
        );
        if material_hash.to_string() != resources.material_library_hash() {
            return Err(
                "Accessoires du village incompatibles avec les matériaux du village".into(),
            );
        }
        let prepared =
            forge_engine::tidewater_village_props::PreparedVillageProps::from_package(&resources)?;
        (Some((pin, Rc::new(resources))), prepared.templates())
    } else {
        (None, Vec::new())
    };
    let (beach_props, beach_props_templates) = if let Some(pin) = candidate.beach_props_sha256 {
        let manifest = transport::fetch_building_payload(pin, "package.json", 128 * 1024).await?;
        let resources = BeachPropPackage::load_async(&manifest, pin, |name, limit| async move {
            if !current(ticket) {
                return Err("Chargement remplacé".into());
            }
            transport::fetch_building_payload(pin, &name, limit).await
        })
        .await?;
        let material_hash = forge_core::bytes_sha256(
            package
                .file("materials.json")
                .ok_or("Matériaux du village absents")?,
        );
        if material_hash.to_string() != resources.material_library_hash() {
            return Err("Accessoires de plage incompatibles avec les matériaux du village".into());
        }
        let prepared =
            forge_engine::tidewater_beach_props::PreparedBeachProps::from_package(&resources)?;
        (Some((pin, Rc::new(resources))), prepared.templates())
    } else {
        (None, Vec::new())
    };
    if !current(ticket) {
        return Err("Chargement remplacé".into());
    }
    let debris = debris::load(ticket, candidate.debris_native_sha256).await?;
    let (fish_props, fish_props_templates) = if let Some(pin) = candidate.fish_props_sha256 {
        let manifest = transport::fetch_building_payload(pin, "package.json", 128 * 1024).await?;
        let resources = FishPropPackage::load_async(&manifest, pin, |name, limit| async move {
            if !current(ticket) {
                return Err("Chargement remplacé".into());
            }
            transport::fetch_building_payload(pin, &name, limit).await
        })
        .await?;
        let prepared =
            forge_engine::tidewater_fish_props::PreparedFishProps::from_package(&resources)?;
        (Some((pin, Rc::new(resources))), prepared.templates())
    } else {
        (None, Vec::new())
    };
    if !current(ticket) {
        return Err("Chargement remplacé".into());
    }
    let game_fish = if let Some(pin) = candidate.game_fish_sha256 {
        let manifest = transport::fetch_building_payload(pin, "package.json", 128 * 1024).await?;
        let package = GameFishPackage::load_async(&manifest, pin, |name, limit| async move {
            if !current(ticket) {
                return Err("Chargement remplacé".into());
            }
            transport::fetch_building_payload(pin, &name, limit).await
        })
        .await?;
        Some(Rc::new(package))
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
    let pier_motion = if let Some(pin) = candidate.pier_motion_sha256 {
        let (_, geometry) = pier
            .as_ref()
            .ok_or("Pier motion requires verified geometry")?;
        let manifest = transport::fetch_building_payload(pin, "manifest.json", 16 * 1024).await?;
        if forge_core::bytes_sha256(&manifest) != pin {
            return Err("Pier motion candidate manifest hash differs".into());
        }
        let motion = forge_assets::tidewater_pier_motion::PierMotionPackage::load_async(
            &manifest,
            geometry,
            |name, limit| async move {
                if !current(ticket) {
                    return Err("Chargement remplacé".into());
                }
                transport::fetch_building_payload(pin, &name, limit).await
            },
        )
        .await?;
        Some(Rc::new(motion))
    } else {
        None
    };
    let fish_stand = load_fish_stand(ticket, candidate.fish_stand_bundle_sha256).await?;
    if !current(ticket) {
        return Err("Chargement remplacé".into());
    }
    let prepared_fish_stand = fish_stand
        .as_ref()
        .map(|package| forge_engine::tidewater_stall::FishStandPrepared::load(package.files()))
        .transpose()?;
    if fish_stand
        .as_ref()
        .is_some_and(|package| package.building_package_hash() != hash)
    {
        return Err("Le bundle FishStand ne correspond pas à la bibliothèque de bâtiments".into());
    }
    let geometry = PreparedMeshes::load(&package)?;
    let library = PreparedLibrary::load(&package)?;
    let local_lights =
        load_local_lights(ticket, candidate.source_local_lights_sha256, &package).await?;
    if !current(ticket) {
        return Err("Chargement remplacé".into());
    }
    if let Some(document) = &document {
        if matches!(purpose, LibraryPurpose::Projection) {
            source_objects::verify_projection_source(
                document,
                terrain.as_deref(),
                plants.as_deref(),
            )?;
        }
        let scope = crate::library_resume::LibraryResume::new(document, candidate_hash)
            .ok_or("Source terrain invalide pour la bibliothèque")?;
        if !crate::authoring_viewport::imported_document()
            .as_ref()
            .is_some_and(|current| scope.matches(current))
        {
            return Err("La zone a changé pendant le chargement de la bibliothèque.".into());
        }
    }
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
    ENVIRONMENT_PROGRAMS.with(|slot| slot.replace(environment_programs));
    PIER_MOTION.with(|slot| slot.replace(pier_motion));
    OCEAN.with(|slot| slot.replace(ocean));
    WATER_SURFACE.with(|slot| slot.replace(water_surface));
    SHORE_SIM.with(|slot| slot.replace(shore_sim));
    GROUND_BOUNCE.with(|slot| slot.replace(ground_bounce));
    BOARDWALK.with(|slot| slot.replace(boardwalk));
    BOARDWALK_TEMPLATES.with(|slot| slot.replace(boardwalk_templates));
    publish_boardwalk_ready();
    PIER.with(|slot| slot.replace(pier));
    PIER_TEMPLATE.with(|slot| slot.replace(pier_template));
    publish_pier_ready();
    PLAZA.with(|slot| slot.replace(plaza));
    PLAZA_TEMPLATES.with(|slot| slot.replace(plaza_templates));
    publish_plaza_ready();
    VILLAGE_PROPS.with(|slot| slot.replace(village_props));
    VILLAGE_PROPS_TEMPLATES.with(|slot| slot.replace(village_props_templates));
    BEACH_PROPS.with(|slot| slot.replace(beach_props));
    BEACH_PROPS_TEMPLATES.with(|slot| slot.replace(beach_props_templates));
    debris::publish(debris);
    FISH_PROPS.with(|slot| slot.replace(fish_props));
    GAME_FISH.with(|slot| slot.replace(game_fish));
    FISH_PROPS_TEMPLATES.with(|slot| slot.replace(fish_props_templates));
    PACKAGE.with(|slot| slot.replace(Some((hash, Rc::new(package)))));
    LOCAL_LIGHTS.with(|slot| slot.replace(local_lights));
    PREPARED_BUILDINGS.with(|slot| slot.replace(Some((geometry, library))));
    FISH_STAND.with(|slot| slot.replace(fish_stand));
    PREPARED_FISH_STAND.with(|slot| slot.replace(prepared_fish_stand));
    publish_fish_stand_ready();
    PUBLISHED.with(|slot| slot.set(ticket));
    super::catalog::changed();
    super::placements::retry_library();
    super::imported_environment::hide_preview();
    if !has_environment {
        render_status("Cette bibliothèque ne fournit pas d’environnement.".into());
    }
    if let Some(document) = &document {
        remember_choice(document, candidate_hash);
    }
    crate::authoring_viewport::retry_imported();
    Ok((count, has_environment))
}

async fn load_fish_stand(
    ticket: u64,
    pin: Option<ContentHash>,
) -> Result<Option<Rc<FishStandPackage>>, String> {
    use crate::governed_kit::transport;
    let Some(pin) = pin else {
        return Ok(None);
    };
    let manifest =
        transport::fetch_building_payload(pin, "preview-scene.json", 2 * 1024 * 1024).await?;
    let package = FishStandPackage::load_async(&manifest, pin, |name, limit| async move {
        if !current(ticket) {
            return Err("Chargement remplacé".into());
        }
        transport::fetch_building_payload(pin, &name, limit).await
    })
    .await?;
    Ok(Some(Rc::new(package)))
}

async fn load_local_lights(
    ticket: u64,
    pin: Option<ContentHash>,
    buildings: &BuildingPackage,
) -> Result<
    Option<std::sync::Arc<forge_assets::source_local_lights::SourceLocalLightsPackage>>,
    String,
> {
    use crate::governed_kit::transport;
    let Some(pin) = pin else {
        return Ok(None);
    };
    let manifest = transport::fetch_building_payload(pin, "manifest.json", 2 * 1024 * 1024).await?;
    let lights = forge_assets::source_local_lights::SourceLocalLightsPackage::load_async(
        &manifest,
        pin,
        |name, limit| async move {
            if !current(ticket) {
                return Err("Chargement remplacé".into());
            }
            transport::fetch_building_payload(pin, &name, limit).await
        },
    )
    .await
    .map_err(|error| error.to_string())?;
    if !current(ticket) {
        return Err("Chargement remplacé".into());
    }
    forge_engine::tidewater_assets::local_lights::BuildingLightBindings::from_packages(
        buildings, &lights,
    )?;
    Ok(Some(std::sync::Arc::new(lights)))
}

/// Verified immutable acquisition available to the scene adapter. Its presence
/// does not attest emitter projection or a material draw.
#[derive(bevy::prelude::Resource, Clone)]
pub(super) struct VerifiedLocalLights(
    pub std::sync::Arc<forge_assets::source_local_lights::SourceLocalLightsPackage>,
);

pub(super) fn local_lights_package()
-> Option<std::sync::Arc<forge_assets::source_local_lights::SourceLocalLightsPackage>> {
    LOCAL_LIGHTS.with(|slot| slot.borrow().clone())
}

pub(super) fn stage_local_lights(world: &mut bevy::prelude::World) {
    LOCAL_LIGHTS.with(|slot| {
        let package = slot.borrow();
        if let Some(package) = package.as_ref() {
            let current = world.get_resource::<VerifiedLocalLights>();
            if current.is_none_or(|current| current.0.receipt_hash() != package.receipt_hash()) {
                world.insert_resource(VerifiedLocalLights(package.clone()));
            }
        } else {
            world.remove_resource::<VerifiedLocalLights>();
        }
    });
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

pub(crate) fn ocean_package() -> Option<Rc<OceanPackage>> {
    OCEAN.with(|slot| slot.borrow().as_ref().map(|(_, package)| package.clone()))
}

pub(crate) fn water_surface_package() -> Option<Rc<WaterSurfacePackage>> {
    WATER_SURFACE.with(|slot| slot.borrow().as_ref().map(|(_, package)| package.clone()))
}

pub(crate) fn shore_sim_package() -> Option<Rc<ShoreSimPackage>> {
    SHORE_SIM.with(|slot| slot.borrow().as_ref().map(|(_, package)| package.clone()))
}

pub(crate) fn ground_bounce_package() -> Option<Rc<GroundBouncePackage>> {
    GROUND_BOUNCE.with(|slot| slot.borrow().as_ref().map(|(_, package)| package.clone()))
}

pub(crate) fn boardwalk_package() -> Option<Rc<BoardwalkPackage>> {
    BOARDWALK.with(|slot| slot.borrow().as_ref().map(|(_, package)| package.clone()))
}

pub(crate) fn pier_package() -> Option<Rc<PierPackage>> {
    PIER.with(|slot| slot.borrow().as_ref().map(|(_, package)| package.clone()))
}

pub(crate) fn plaza_package() -> Option<Rc<PlazaPackage>> {
    PLAZA.with(|slot| slot.borrow().as_ref().map(|(_, package)| package.clone()))
}

pub(crate) fn plaza_templates() -> Vec<forge_engine::tidewater_plaza::PlazaTemplate> {
    PLAZA_TEMPLATES.with(|slot| slot.borrow().clone())
}

pub(crate) fn is_plaza_asset(id: forge_core::AssetId) -> bool {
    forge_assets::tidewater_plaza::asset_ordinal(id).is_some()
}

pub(crate) fn village_props_package() -> Option<Rc<VillagePropPackage>> {
    VILLAGE_PROPS.with(|slot| slot.borrow().as_ref().map(|(_, package)| package.clone()))
}

pub(crate) fn village_props_templates()
-> Vec<forge_engine::tidewater_village_props::VillagePropsTemplate> {
    VILLAGE_PROPS_TEMPLATES.with(|slot| slot.borrow().clone())
}

pub(crate) fn is_village_props_asset(id: forge_core::AssetId) -> bool {
    forge_assets::tidewater_village_props::asset_ordinal(id).is_some()
}

fn invalidate_village_props() {
    VILLAGE_PROPS.with(|slot| slot.replace(None));
    VILLAGE_PROPS_TEMPLATES.with(|slot| slot.borrow_mut().clear());
    super::catalog::changed();
}

pub(crate) fn beach_props_package() -> Option<Rc<BeachPropPackage>> {
    BEACH_PROPS.with(|slot| slot.borrow().as_ref().map(|(_, package)| package.clone()))
}

pub(crate) fn beach_props_templates() -> Vec<forge_engine::tidewater_beach_props::BeachPropsTemplate>
{
    BEACH_PROPS_TEMPLATES.with(|slot| slot.borrow().clone())
}

pub(crate) fn is_beach_props_asset(id: forge_core::AssetId) -> bool {
    forge_assets::tidewater_beach_props::asset_ordinal(id).is_some()
}

fn invalidate_beach_props() {
    BEACH_PROPS.with(|slot| slot.replace(None));
    BEACH_PROPS_TEMPLATES.with(|slot| slot.borrow_mut().clear());
    super::catalog::changed();
}

pub(crate) fn fish_props_templates() -> Vec<forge_engine::tidewater_fish_props::FishPropsTemplate> {
    FISH_PROPS_TEMPLATES.with(|slot| slot.borrow().clone())
}

pub(super) fn fish_props_package() -> Option<Rc<FishPropPackage>> {
    FISH_PROPS.with(|slot| slot.borrow().as_ref().map(|(_, package)| package.clone()))
}

pub(super) fn game_fish_package() -> Option<Rc<GameFishPackage>> {
    GAME_FISH.with(|slot| slot.borrow().clone())
}

pub(crate) fn is_fish_props_asset(id: forge_core::AssetId) -> bool {
    forge_assets::tidewater_fish_props::asset_ordinal(id).is_some()
}

fn invalidate_fish_props() {
    GAME_FISH.with(|slot| slot.replace(None));
    FISH_PROPS.with(|slot| slot.replace(None));
    FISH_PROPS_TEMPLATES.with(|slot| slot.borrow_mut().clear());
    super::catalog::changed();
}

fn publish_plaza_ready() {
    let ready = plaza_package().is_some() && plaza_templates().len() == 5;
    PLAZA_READY.with(|slot| {
        if let Some(signal) = *slot.borrow() {
            signal.set(ready);
        }
    });
}

fn invalidate_plaza() {
    PLAZA.with(|slot| slot.replace(None));
    PLAZA_TEMPLATES.with(|slot| slot.borrow_mut().clear());
    publish_plaza_ready();
    super::catalog::changed();
}

pub(crate) fn pier_template() -> Option<forge_engine::tidewater_pier::PierTemplate> {
    PIER_TEMPLATE.with(|slot| slot.borrow().clone())
}

pub(crate) fn is_pier_asset(id: forge_core::AssetId) -> bool {
    id == forge_assets::tidewater_pier::asset_id()
}

fn publish_pier_ready() {
    let ready = pier_package().is_some() && pier_template().is_some();
    PIER_READY.with(|slot| {
        if let Some(signal) = *slot.borrow() {
            signal.set(ready);
        }
    });
}

fn invalidate_pier() {
    PIER.with(|slot| slot.replace(None));
    PIER_TEMPLATE.with(|slot| slot.replace(None));
    publish_pier_ready();
    super::catalog::changed();
}

pub(crate) fn boardwalk_templates() -> Vec<forge_engine::tidewater_boardwalk::BoardwalkTemplate> {
    BOARDWALK_TEMPLATES.with(|slot| slot.borrow().clone())
}

pub(crate) fn is_boardwalk_asset(id: forge_core::AssetId) -> bool {
    forge_assets::tidewater_boardwalk::asset_ordinal(id).is_some()
}

fn publish_boardwalk_ready() {
    let ready = boardwalk_package().is_some() && boardwalk_templates().len() == 3;
    BOARDWALK_READY.with(|slot| {
        if let Some(signal) = *slot.borrow() {
            signal.set(ready);
        }
    });
}

fn invalidate_boardwalk() {
    BOARDWALK.with(|slot| slot.replace(None));
    BOARDWALK_TEMPLATES.with(|slot| slot.borrow_mut().clear());
    publish_boardwalk_ready();
    super::catalog::changed();
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

pub(crate) fn fish_stand_reference()
-> Option<forge_assets::tidewater_fish_stand::FishStandReference> {
    FISH_STAND.with(|slot| slot.borrow().as_ref().map(|package| package.reference()))
}

pub(crate) fn fish_stand_package() -> Option<Rc<FishStandPackage>> {
    FISH_STAND.with(|slot| slot.borrow().clone())
}

fn publish_fish_stand_ready() {
    super::catalog::changed();
    let ready = fish_stand_reference().is_some();
    FISH_STAND_READY.with(|slot| {
        if let Some(signal) = *slot.borrow() {
            signal.set(ready);
        }
    });
}

fn invalidate_fish_stand() {
    FISH_STAND.with(|slot| slot.replace(None));
    PREPARED_FISH_STAND.with(|slot| slot.replace(None));
    publish_fish_stand_ready();
    super::catalog::changed();
}

pub(crate) fn is_fish_stand_asset(id: forge_core::AssetId) -> bool {
    id == forge_assets::tidewater_fish_stand::asset_id()
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

pub(super) fn take_fish_stand() -> Option<forge_engine::tidewater_stall::FishStandPrepared> {
    PREPARED_FISH_STAND.with(|slot| slot.borrow_mut().take())
}

pub(super) fn restore_fish_stand() -> Result<(), String> {
    if PREPARED_FISH_STAND.with(|slot| slot.borrow().is_some()) {
        return Ok(());
    }
    let prepared = FISH_STAND.with(|slot| {
        slot.borrow()
            .as_ref()
            .map(|package| forge_engine::tidewater_stall::FishStandPrepared::load(package.files()))
            .transpose()
    })?;
    PREPARED_FISH_STAND.with(|slot| slot.replace(prepared));
    Ok(())
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

pub(super) fn environment_programs() -> Option<(
    u64,
    Rc<forge_assets::tidewater_environment::programs::ProgramsPackage>,
)> {
    ENVIRONMENT_PROGRAMS
        .with(|slot| slot.borrow().clone())
        .map(|package| (PUBLISHED.with(Cell::get), package))
}

pub(crate) fn pier_motion_package()
-> Option<Rc<forge_assets::tidewater_pier_motion::PierMotionPackage>> {
    PIER_MOTION.with(|slot| slot.borrow().clone())
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
    let fish_stand_ready = RwSignal::new(fish_stand_reference().is_some());
    FISH_STAND_READY.with(|slot| slot.replace(Some(fish_stand_ready)));
    let boardwalk_ready = RwSignal::new(boardwalk_package().is_some());
    BOARDWALK_READY.with(|slot| slot.replace(Some(boardwalk_ready)));
    let pier_ready = RwSignal::new(pier_package().is_some());
    PIER_READY.with(|slot| slot.replace(Some(pier_ready)));
    let plaza_ready = RwSignal::new(plaza_package().is_some());
    PLAZA_READY.with(|slot| slot.replace(Some(plaza_ready)));
    let status = RwSignal::new("Bibliothèque importée non vérifiée.".to_owned());
    on_cleanup(|| {
        RENDER_STATUS.with(|slot| slot.replace(None));
        FISH_STAND_READY.with(|slot| slot.replace(None));
        BOARDWALK_READY.with(|slot| slot.replace(None));
        PIER_READY.with(|slot| slot.replace(None));
        PLAZA_READY.with(|slot| slot.replace(None));
        next();
    });
    view! {
        <details>
            <summary>"Bibliothèque de bâtiments importés"</summary>
            <button disabled=move || busy.get() on:click=move |_| {
                if busy.get_untracked() { return; }
                let ticket = next();
                busy.set(true);
                invalidate_fish_stand();
                invalidate_boardwalk();
                invalidate_pier();
                invalidate_plaza();
                invalidate_village_props();
                invalidate_beach_props();
                invalidate_fish_props();
                status.set("Vérification de la bibliothèque locale…".into());
                wasm_bindgen_futures::spawn_local(async move {
                    let result = load(ticket, None, LibraryPurpose::Catalog).await;
                    // Replacement must release this control even when another
                    // loader owns publication. The panel may also be disposed.
                    let _ = busy.try_set(false);
                    if !current(ticket) {
                        let _ = status.try_set("Chargement interrompu. Relancez la vérification de la bibliothèque.".into());
                        return;
                    }
                    status.set(match result {
                        Ok((count, environment)) => {
                            let sky = if environment { " Ressources de ciel et de nuages vérifiées et chargées." } else { "" };
                            let plants = if plant_package().is_some() { " Ressources végétales chargées ; leur affichage reste à raccorder." } else { "" };
                            let stand = if fish_stand_reference().is_some() { " L’échoppe de Joe est disponible dans Ajouter un objet depuis le catalogue." } else { "" };
                            let debris = debris::summary();
                            format!("{count} bâtiments vérifiés et chargés en mémoire.{sky}{plants} Les bâtiments déjà placés seront projetés dans la carte importée.{stand}{debris}")
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
