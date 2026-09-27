//! Latest accepted source only; GPU projection and authoring corrections are separate.
use crate::{StudioCommand, StudioResponse};
use forge_core::ContentHash;
use forge_project::{authoring::AuthoringState, zone_authoring::ZoneAuthoringHead};
use forge_world::imported_vegetation::{ImportedPopulation, acquisition};
use std::{
    cell::{Cell, RefCell},
    rc::Rc,
};

thread_local! {
    static EPOCH: Cell<u64> = const { Cell::new(0) };
    static HEAD: Cell<Option<ContentHash>> = const { Cell::new(None) };
    static DOCUMENT: RefCell<Option<Rc<ZoneAuthoringHead>>> = const { RefCell::new(None) };
    static LOADING: Cell<bool> = const { Cell::new(false) };
    static POPULATION: RefCell<Option<Rc<ImportedPopulation>>> = const { RefCell::new(None) };
}
fn current(epoch: u64) -> bool {
    EPOCH.with(Cell::get) == epoch
}

/// Return true for imported vegetation, so the procedural generator is not used.
pub(crate) fn observe(state: &AuthoringState) -> bool {
    let head = state.zone_head.as_ref().filter(|h| {
        h.base
            .vegetation
            .as_ref()
            .is_some_and(|v| v.imported_source.is_some())
    });
    let key = match head.map(ZoneAuthoringHead::content_hash).transpose() {
        Ok(key) => key,
        Err(error) => {
            clear();
            report(format!("Végétation importée refusée : {error}"));
            return head.is_some();
        }
    };
    DOCUMENT.with(|d| d.replace(head.cloned().map(Rc::new)));
    if HEAD.with(Cell::get) == key && (key.is_none() || LOADING.with(Cell::get)) {
        return head.is_some();
    }
    let epoch = EPOCH.with(|e| {
        let value = e.get().wrapping_add(1);
        e.set(value);
        value
    });
    HEAD.with(|h| h.set(key));
    LOADING.with(|l| l.set(false));
    let Some(head) = head else {
        POPULATION.with(|p| p.replace(None));
        return false;
    };
    if population(head).is_ok() {
        loaded(head);
        return true;
    }
    POPULATION.with(|p| p.replace(None));
    LOADING.with(|l| l.set(true));
    report("Chargement des placements végétaux importés en cours.".into());
    let head = head.clone();
    wasm_bindgen_futures::spawn_local(async move {
        let result = load(&head, epoch).await;
        if !current(epoch) {
            return;
        }
        LOADING.with(|l| l.set(false));
        match result {
            Ok(value) => {
                POPULATION.with(|p| p.replace(Some(Rc::new(value))));
                loaded(&head);
            }
            Err(error) => {
                POPULATION.with(|p| p.replace(None));
                report(format!("Placements végétaux indisponibles : {error}"));
            }
        }
    });
    true
}

fn clear() {
    DOCUMENT.with(|d| d.replace(None));
    EPOCH.with(|e| e.set(e.get().wrapping_add(1)));
    HEAD.with(|h| h.set(None));
    LOADING.with(|l| l.set(false));
    POPULATION.with(|p| p.replace(None));
}

pub(crate) fn current_head() -> Option<Rc<ZoneAuthoringHead>> {
    DOCUMENT.with(|d| d.borrow().clone())
}

pub(crate) fn population(head: &ZoneAuthoringHead) -> Result<Rc<ImportedPopulation>, String> {
    if HEAD.with(Cell::get) != Some(head.content_hash()?) {
        return Err("Population demandée pour une autre révision".into());
    }
    POPULATION
        .with(|p| {
            p.borrow()
                .as_ref()
                .filter(|p| Some(p.document()) == head.base.vegetation.as_ref())
                .cloned()
        })
        .ok_or_else(|| "Population importée non chargée".into())
}

fn report(message: String) {
    crate::authoring_vegetation::report(message);
}
fn loaded(head: &ZoneAuthoringHead) {
    if let Ok(population) = population(head) {
        report(format!(
            "{} placements végétaux chargés et vérifiés.",
            population.instance_count()
        ));
    }
}

async fn load(head: &ZoneAuthoringHead, epoch: u64) -> Result<ImportedPopulation, String> {
    let expected_head = head.content_hash()?;
    acquisition::acquire(
        head,
        |block_index, offset| async move {
            match crate::web::invoke_tauri(&StudioCommand::ReadVegetationSource {
                expected_head,
                block_index,
                offset,
            })
            .await?
            {
                StudioResponse::VegetationSource { chunk } => Ok(chunk),
                StudioResponse::Rejected { reason } => Err(reason),
                _ => Err("Réponse de population inattendue".into()),
            }
        },
        || current(epoch),
    )
    .await
}
