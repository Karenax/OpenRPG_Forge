//! Verified immutable raster transport for the current native authoring session.
use crate::{StudioCommand, StudioResponse};
use forge_core::{ContentHash, Transform};
use forge_project::{
    TerrainDocument,
    authoring::{AuthoringState, terrain_source::TerrainSourceAssembly},
};
use forge_world::{
    TerrainGenerationPlan, placement_snap::PlacementSnap, sampled_terrain::SampledTerrainPlan,
};
use std::{
    cell::{Cell, RefCell},
    rc::Rc,
};
thread_local! {
    static LOADING: Cell<bool> = const { Cell::new(false) };
    static EPOCH: Cell<u64> = const { Cell::new(0) };
    static DOCUMENT: RefCell<Option<TerrainDocument>> = const { RefCell::new(None) };
    static SOURCE: RefCell<Option<(ContentHash,Rc<Vec<u8>>)>> = const { RefCell::new(None) };
    static PLAN: RefCell<Option<Rc<SampledTerrainPlan>>> = const { RefCell::new(None) };
    static ERROR: RefCell<String> = const { RefCell::new(String::new()) };
}
fn current(epoch: u64) -> bool {
    EPOCH.with(Cell::get) == epoch
}

pub(crate) fn observe(state: &AuthoringState) {
    let document = state
        .document
        .as_ref()
        .filter(|doc| doc.sampled_source.is_some())
        .cloned();
    if DOCUMENT.with(|old| *old.borrow() == document)
        && (document.is_none()
            || LOADING.with(Cell::get)
            || SOURCE.with(|s| {
                s.borrow().as_ref().is_some_and(|(hash, _)| {
                    document
                        .as_ref()
                        .and_then(|d| d.sampled_source.as_ref())
                        .is_some_and(|source| source.samples_sha256 == *hash)
                })
            }))
    {
        return;
    }
    DOCUMENT.with(|old| old.replace(document.clone()));
    PLAN.with(|p| p.replace(None));
    let epoch = EPOCH.with(|e| {
        let value = e.get().wrapping_add(1);
        e.set(value);
        value
    });
    let Some(document) = document else {
        LOADING.with(|v| v.set(false));
        SOURCE.with(|s| s.replace(None));
        return;
    };
    ERROR.with(|s| s.replace("Chargement du relief importé en cours.".into()));
    LOADING.with(|v| v.set(true));
    wasm_bindgen_futures::spawn_local(async move {
        let result = load(&document, epoch).await;
        if !current(epoch) {
            return;
        }
        LOADING.with(|v| v.set(false));
        match result {
            Ok((hash, bytes)) => {
                SOURCE.with(|s| s.replace(Some((hash, bytes))));
                ERROR.with(|s| s.borrow_mut().clear());
            }
            Err(error) => {
                SOURCE.with(|s| s.replace(None));
                ERROR.with(|s| s.replace(format!("Relief importé indisponible : {error}")));
            }
        }
        crate::authoring_viewport::retry_imported();
    });
}

/// Whether the observed document still has an active source acquisition.
pub(crate) fn loading() -> bool {
    LOADING.with(Cell::get)
}

/// Verified immutable source for the currently observed document only.
pub(crate) fn source_bytes(document: &TerrainDocument) -> Result<Rc<Vec<u8>>, String> {
    if !DOCUMENT.with(|d| d.borrow().as_ref() == Some(document)) {
        return Err("Le relief demandé ne correspond pas au document courant".into());
    }
    let source = document.sampled_source.as_ref().ok_or("Aucun raster")?;
    SOURCE.with(|s| {
        s.borrow()
            .as_ref()
            .filter(|(hash, _)| *hash == source.samples_sha256)
            .map(|(_, bytes)| bytes.clone())
            .ok_or_else(|| {
                ERROR.with(|error| {
                    let error = error.borrow();
                    if error.is_empty() {
                        "Relief importé non chargé.".into()
                    } else {
                        error.clone()
                    }
                })
            })
    })
}
async fn load(
    document: &TerrainDocument,
    epoch: u64,
) -> Result<(ContentHash, Rc<Vec<u8>>), String> {
    let source = document.sampled_source.as_ref().ok_or("Aucun raster")?;
    if let Some(cached) = SOURCE.with(|s| {
        s.borrow()
            .as_ref()
            .filter(|(hash, _)| *hash == source.samples_sha256)
            .cloned()
    }) {
        return Ok(cached);
    }
    let expected_document = document.content_hash().map_err(|e| e.to_string())?;
    let mut assembly = TerrainSourceAssembly::new(document)?;
    while let Some(offset) = assembly.next_offset() {
        if !current(epoch) {
            return Err("Chargement remplacé".into());
        }
        match crate::web::invoke_tauri(&StudioCommand::ReadTerrainSource {
            expected_document,
            offset,
        })
        .await?
        {
            StudioResponse::TerrainSource { chunk } => assembly.accept(chunk)?,
            StudioResponse::Rejected { reason } => return Err(reason),
            _ => return Err("Réponse de raster inattendue".into()),
        }
    }
    Ok((source.samples_sha256, Rc::new(assembly.finish()?)))
}

pub(crate) fn prepare_alignment(
    document: &TerrainDocument,
    snap: PlacementSnap,
) -> Result<Alignment, String> {
    snap.validate()?;
    let Some(source) = &document.sampled_source else {
        let terrain = TerrainGenerationPlan::from_document(document).map_err(|e| e.to_string())?;
        return Ok(Alignment {
            snap,
            surface: Surface::Procedural(Rc::new(terrain)),
        });
    };
    let active = DOCUMENT.with(|d| {
        d.borrow().as_ref().is_some_and(|d| {
            d.area_id == document.area_id
                && d.project_id == document.project_id
                && d.revision == document.revision
                && d.sampled_source
                    .as_ref()
                    .is_some_and(|s| s.samples_sha256 == source.samples_sha256)
        })
    });
    if !active {
        return Err("Le relief demandé ne correspond pas à la zone ouverte".into());
    }
    let hash = document.content_hash().map_err(|e| e.to_string())?;
    let cached = PLAN.with(|p| {
        p.borrow()
            .as_ref()
            .filter(|p| p.document_hash() == hash)
            .cloned()
    });
    let plan = if let Some(plan) = cached {
        plan
    } else {
        let bytes = SOURCE
            .with(|s| {
                s.borrow()
                    .as_ref()
                    .filter(|(h, _)| *h == source.samples_sha256)
                    .map(|(_, bytes)| bytes.clone())
            })
            .ok_or_else(|| {
                ERROR.with(|s| {
                    let error = s.borrow();
                    if error.is_empty() {
                        "Relief importé non chargé pour cette révision".into()
                    } else {
                        error.clone()
                    }
                })
            })?;
        let plan = Rc::new(
            SampledTerrainPlan::from_document(document, &bytes).map_err(|e| e.to_string())?,
        );
        PLAN.with(|p| p.replace(Some(plan.clone())));
        plan
    };
    Ok(Alignment {
        snap,
        surface: Surface::Sampled(plan),
    })
}

pub(crate) struct Alignment {
    snap: PlacementSnap,
    surface: Surface,
}
enum Surface {
    Procedural(Rc<TerrainGenerationPlan>),
    Sampled(Rc<SampledTerrainPlan>),
}
impl Alignment {
    pub(crate) fn resolve(&self, transform: Transform) -> Result<Transform, String> {
        match &self.surface {
            Surface::Procedural(plan) => self.snap.resolve(transform, plan),
            Surface::Sampled(plan) => self.snap.resolve_sampled(transform, plan),
        }
    }
    pub(crate) fn resolve_axis(
        &self,
        transform: Transform,
        axis: forge_world::placement_transform::Axis,
    ) -> Result<Transform, String> {
        match &self.surface {
            Surface::Procedural(plan) => self.snap.resolve_axis(transform, axis, plan),
            Surface::Sampled(plan) => self.snap.resolve_axis_sampled(transform, axis, plan),
        }
    }
}
pub(crate) fn align(
    document: &TerrainDocument,
    snap: PlacementSnap,
    transform: Transform,
) -> Result<Transform, String> {
    prepare_alignment(document, snap)?.resolve(transform)
}
