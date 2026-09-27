//! One bounded derived terrain cache shared by authoring adapters. Never rewrites `AuthoringState`.
use bevy::prelude::*;
use forge_core::ContentHash;
use forge_engine::authoring_water::{AuthoringWaterPlugin, PreparedAuthoringWater};
use forge_project::{
    TerrainDocument, authoring::AuthoringState, zone_authoring::ZoneAuthoringHead,
};
use leptos::prelude::*;
use std::{cell::RefCell, rc::Rc};

thread_local! {
    static CACHE: RefCell<Option<Rc<forge_world::river_projection::RiverProjection>>> = const { RefCell::new(None) };
    static REQUESTED: RefCell<Option<ContentHash>> = const { RefCell::new(None) };
    static WATER: RefCell<Option<PreparedAuthoringWater>> = const { RefCell::new(None) };
    static CLEAR: RefCell<bool> = const { RefCell::new(false) };
    static STATUS: RefCell<String> = const { RefCell::new(String::new()) };
    static SIGNAL: RefCell<Option<WriteSignal<String>>> = const { RefCell::new(None) };
}

fn report(message: String) {
    STATUS.with(|s| s.replace(message.clone()));
    SIGNAL.with(|s| {
        if let Some(signal) = *s.borrow() {
            let _ = signal.try_set(message);
        }
    });
}

#[component]
pub(super) fn WaterStatus() -> impl IntoView {
    let (status, set_status) = signal(STATUS.with(|s| s.borrow().clone()));
    SIGNAL.with(|s| s.replace(Some(set_status)));
    on_cleanup(move || {
        SIGNAL.with(|s| {
            if *s.borrow() == Some(set_status) {
                s.replace(None);
            }
        });
    });
    view! { <output aria-live="polite">{move || status.get()}</output> }
}

pub(crate) fn projection(
    head: &ZoneAuthoringHead,
) -> Result<Rc<forge_world::river_projection::RiverProjection>, String> {
    let hash = head.content_hash()?;
    if let Some(cached) = CACHE.with(|c| {
        c.borrow()
            .as_ref()
            .filter(|p| p.source_hash == hash)
            .cloned()
    }) {
        return Ok(cached);
    }
    let projected = Rc::new(forge_world::river_projection::project(head)?);
    CACHE.with(|c| c.replace(Some(projected.clone())));
    Ok(projected)
}

pub(crate) fn terrain(head: &ZoneAuthoringHead) -> Result<TerrainDocument, String> {
    Ok(projection(head)?.terrain.clone())
}

pub(super) fn observe(state: &AuthoringState) -> Result<Option<TerrainDocument>, String> {
    match prepare(state) {
        Ok(terrain) => Ok(terrain),
        Err(error) => {
            WATER.with(|p| p.replace(None));
            REQUESTED.with(|p| p.replace(None));
            CLEAR.with(|p| p.replace(true));
            report(format!(
                "Eau non projetée : {error}. Corriger la recette puis relire la session."
            ));
            Err(error)
        }
    }
}

fn prepare(state: &AuthoringState) -> Result<Option<TerrainDocument>, String> {
    let Some(head) = &state.zone_head else {
        CACHE.with(|c| c.replace(None));
        WATER.with(|p| p.replace(None));
        REQUESTED.with(|p| p.replace(None));
        CLEAR.with(|p| p.replace(true));
        report("Eau : aucune recette de zone active.".into());
        return Ok(state.document.clone());
    };
    let projected = projection(head)?;
    if REQUESTED.with(|p| *p.borrow()) != Some(projected.source_hash) {
        let batch = PreparedAuthoringWater::from_projection(&projected)?;
        report(format!(
            "Eau : {} rivière(s) préparée(s), en attente du terrain concordant. Aucun rendu GPU confirmé.",
            batch.river_count()
        ));
        WATER.with(|p| p.replace(Some(batch)));
        REQUESTED.with(|p| p.replace(Some(projected.source_hash)));
    }
    Ok(Some(projected.terrain.clone()))
}

pub(super) fn install(app: &mut App) {
    app.add_plugins(AuthoringWaterPlugin)
        .add_systems(Update, project_water);
}

fn project_water(world: &mut World) {
    // Session disposal must run even after viewport source projection is invalidated.
    if CLEAR.with(|p| p.replace(false)) {
        forge_engine::authoring_water::clear(world);
    }
    if !super::authoring_viewport::can_project_sources() {
        return;
    }
    let ready = WATER.with(|pending| {
        pending.borrow().as_ref().is_some_and(|batch| {
            world
                .get_resource::<forge_engine::TerrainRuntimeState>()
                .is_some_and(|state| batch.terrain_hash() == state.cache().source_document_hash)
        })
    });
    if !ready {
        return;
    }
    if let Some(batch) = WATER.with(|p| p.borrow_mut().take()) {
        let key = batch.source_hash();
        let count = batch.river_count();
        if REQUESTED.with(|p| *p.borrow()) != Some(key) {
            return;
        }
        if let Err(error) = forge_engine::authoring_water::replace(world, key, batch) {
            REQUESTED.with(|p| p.replace(None));
            forge_engine::authoring_water::clear(world);
            report(format!(
                "Eau non projetée : {error}. Relire la session pour réessayer ; aucun nouvel essai automatique."
            ));
            bevy::log::error!(%error, "Authoring water projection rejected");
        } else {
            report(format!(
                "Eau : {count} rivière(s) installée(s) côté moteur, terrain concordant. Rendu GPU non confirmé ; profils optiques non qualifiés."
            ));
        }
    }
}
