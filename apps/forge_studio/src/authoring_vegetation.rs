//! One latest derived source pair, projected only after its terrain is acknowledged.
use bevy::prelude::*;
use forge_project::{TerrainDocument, VegetationDocument, authoring::AuthoringState};
use leptos::prelude::*;
use std::cell::RefCell;

thread_local! {
    static PENDING: RefCell<Option<Pending>> = const { RefCell::new(None) };
    static ACCEPTED: RefCell<Option<Pending>> = const { RefCell::new(None) };
    static PREVIEW_ACTIVE: std::cell::Cell<bool> = const { std::cell::Cell::new(false) };
    static STATUS: RefCell<Option<WriteSignal<String>>> = const { RefCell::new(None) };
    static PROMOTION_GATE: RefCell<crate::promotion_preview_gate::PromotionPreviewGate> = RefCell::new(crate::promotion_preview_gate::PromotionPreviewGate::default());
    static GATE_OUTPUT: RefCell<Option<WriteSignal<bool>>> = const { RefCell::new(None) };
}

pub(super) fn bind_promotion_ready() -> ReadSignal<bool> {
    let (ready, output) = signal(false);
    GATE_OUTPUT.with(|slot| slot.replace(Some(output)));
    on_cleanup(|| {
        GATE_OUTPUT.with(|slot| slot.replace(None));
    });
    ready
}

fn notify_gate(ready: bool) {
    GATE_OUTPUT.with(|slot| {
        if let Some(output) = *slot.borrow() {
            output.set(ready);
        }
    });
}

pub(super) fn cancel_promotion_gate() {
    PROMOTION_GATE.with(|gate| gate.borrow_mut().cancel());
    notify_gate(false);
}

pub(crate) fn promotion_placement_visible(id: forge_core::EntityId) -> bool {
    PROMOTION_GATE.with(|gate| gate.borrow().placement_visible(id))
}

/// Called by the shared dispatch before clearing previews or sending a mutation.
pub(super) fn check_promotion_apply(
    state: Option<&AuthoringState>,
    request: &forge_project::authoring::AuthoringRequest,
) -> Result<(), String> {
    use forge_project::{authoring::AuthoringRequest, zone_authoring::session::ZoneAction};
    let AuthoringRequest::ApplyZone { request } = request else {
        return Ok(());
    };
    let Some(head) = state.and_then(|s| s.zone_head.as_ref()) else {
        return Ok(());
    };
    let promotion = match &request.action {
        ZoneAction::Edit(corrections) => corrections.promotions != head.corrections.promotions,
        ZoneAction::ReconcilePromotions(_) => true,
        _ => false,
    };
    if !promotion {
        return Ok(());
    }
    let key = forge_core::canonical_json_sha256(request).map_err(|e| e.to_string())?;
    let placements = super::authoring_assets::placements::proposal_ready()?;
    PROMOTION_GATE.with(|gate| gate.borrow().check(key, placements))
}

#[derive(Clone)]
struct Pending {
    terrain: TerrainDocument,
    vegetation: VegetationDocument,
    paths: Vec<forge_project::zone_authoring::paths::AuthoredPath>,
    rivers: Vec<forge_project::zone_authoring::rivers::AuthoredRiver>,
    buildings: Vec<forge_project::zone_authoring::buildings::AuthoredBuilding>,
    promotions: Vec<forge_project::zone_authoring::promotion::PromotedInstance>,
    terrain_hash: forge_core::ContentHash,
    terrain_key: String,
    vegetation_key: String,
    submitted: bool,
    preview: bool,
    wait_for_placement: bool,
    promotion_key: Option<forge_core::ContentHash>,
}

impl Pending {
    fn new(
        (terrain, vegetation, paths, rivers, buildings, promotions): (
            TerrainDocument,
            VegetationDocument,
            Vec<forge_project::zone_authoring::paths::AuthoredPath>,
            Vec<forge_project::zone_authoring::rivers::AuthoredRiver>,
            Vec<forge_project::zone_authoring::buildings::AuthoredBuilding>,
            Vec<forge_project::zone_authoring::promotion::PromotedInstance>,
        ),
    ) -> Result<Self, String> {
        let terrain_hash = terrain.content_hash().map_err(|error| error.to_string())?;
        let vegetation_key = forge_world::VegetationGenerationPlan::from_documents_with_buildings(
            &terrain,
            &vegetation,
            &paths,
            &rivers,
            &buildings,
        )
        .and_then(|plan| plan.with_promotions(&promotions))
        .map_err(|e| e.to_string())?
        .source_hash()
        .to_string();
        Ok(Self {
            terrain,
            vegetation,
            paths,
            rivers,
            buildings,
            promotions,
            terrain_hash,
            terrain_key: terrain_hash.to_string(),
            vegetation_key,
            submitted: false,
            preview: false,
            wait_for_placement: false,
            promotion_key: None,
        })
    }
}

pub(crate) fn report(message: String) {
    STATUS.with(|slot| {
        if let Some(signal) = *slot.borrow() {
            signal.set(message);
        }
    });
}

pub(crate) fn selection_hashes(
    stats: &forge_engine::VegetationProjectionStats,
) -> Result<(forge_core::ContentHash, forge_core::ContentHash), String> {
    if PREVIEW_ACTIVE.with(std::cell::Cell::get) || PENDING.with(|s| s.borrow().is_some()) {
        return Err("Attendre la projection acceptée avant de sélectionner un arbre".into());
    }
    ACCEPTED.with(|s| {
        let s = s.borrow();
        let accepted = s.as_ref().ok_or("Source acceptée absente")?;
        if stats.terrain_document_hash != accepted.terrain_key
            || stats.vegetation_document_hash != accepted.vegetation_key
        {
            return Err("Projection périmée".into());
        }
        Ok((
            accepted.terrain_hash,
            accepted
                .vegetation_key
                .parse()
                .map_err(|e| format!("Hash invalide : {e}"))?,
        ))
    })
}

#[component]
pub(super) fn ProjectionStatus() -> impl IntoView {
    let (status, set_status) = signal("Végétation : en attente de session.".to_owned());
    STATUS.with(|slot| slot.replace(Some(set_status)));
    view! { <output aria-live="polite">{move || status.get()}</output> }
}

pub(super) fn empty_source(terrain: &TerrainDocument) -> Result<VegetationDocument, String> {
    forge_world::editor_vegetation::empty(terrain)
}

pub(super) fn observe(state: &AuthoringState) {
    PROMOTION_GATE.with(|gate| gate.borrow_mut().reset());
    notify_gate(false);
    crate::authoring_species::observe(state.zone_head.as_ref());
    if crate::authoring_imported_population::observe(state) {
        PREVIEW_ACTIVE.with(|active| active.set(false));
        ACCEPTED.with(|slot| slot.replace(None));
        PENDING.with(|slot| slot.replace(None));
        return;
    }
    let result = if let Some(head) = &state.zone_head {
        head.effective().and_then(|(_, vegetation)| {
            let terrain = super::authoring_scene::terrain(head)?;
            let vegetation = vegetation.map_or_else(|| empty_source(&terrain), Ok)?;
            Ok(Some((
                terrain,
                vegetation,
                head.corrections.paths.clone().unwrap_or_default(),
                head.corrections.rivers.clone().unwrap_or_default(),
                head.corrections.buildings.clone().unwrap_or_default(),
                head.corrections.promotions.clone().unwrap_or_default(),
            )))
        })
    } else if let Some(terrain) = &state.document {
        empty_source(terrain).map(|vegetation| {
            Some((
                terrain.clone(),
                vegetation,
                Vec::new(),
                Vec::new(),
                Vec::new(),
                Vec::new(),
            ))
        })
    } else {
        Ok(None)
    };
    match result.and_then(|pair| pair.map(Pending::new).transpose()) {
        Ok(pair) => {
            PREVIEW_ACTIVE.with(|active| active.set(false));
            ACCEPTED.with(|slot| slot.replace(pair.clone()));
            PENDING.with(|slot| slot.replace(pair));
        }
        Err(error) => {
            PREVIEW_ACTIVE.with(|active| active.set(false));
            ACCEPTED.with(|slot| slot.replace(None));
            PENDING.with(|slot| slot.replace(None));
            report(format!("Source végétation refusée : {error}"));
        }
    }
}

pub(super) fn restore() {
    if PREVIEW_ACTIVE.with(|active| active.replace(false)) {
        crate::authoring_species::restore();
        PENDING.with(|slot| slot.replace(ACCEPTED.with(|accepted| accepted.borrow().clone())));
        report("Retour à la végétation acceptée demandé.".into());
    }
}

pub(super) fn preview_species(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    receipt: &forge_project::zone_authoring::session::ZoneReceipt,
) -> Result<(), String> {
    if !super::authoring_viewport::can_preview_asset()
        || !super::authoring_regeneration_review::matches_source(Some(head), receipt)
        || receipt.inverse != head.corrections
        || receipt.base_change.is_some()
    {
        return Err("Aperçu d'espèces indisponible ou périmé".into());
    }
    let mut unchanged = receipt.applied.clone();
    unchanged.species.clone_from(&head.corrections.species);
    if unchanged != head.corrections {
        return Err("La proposition change d'autres retouches".into());
    }
    let mut candidate = head.clone();
    candidate.corrections = receipt.applied.clone();
    candidate.version = candidate.corrections.schema_version();
    candidate.revision = receipt.resulting_revision;
    if candidate.content_hash()? != receipt.resulting_hash {
        return Err("Reçu d'espèces incohérent".into());
    }
    candidate.revision = head.revision;
    let mut pending = ACCEPTED
        .with(|slot| slot.borrow().clone())
        .ok_or("Projection acceptée absente")?;
    pending.preview = true;
    pending.submitted = false;
    crate::authoring_species::preview(&candidate);
    PREVIEW_ACTIVE.with(|flag| flag.set(true));
    PENDING.with(|slot| slot.replace(Some(pending)));
    Ok(())
}

pub(super) fn preview_exclusions(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    receipt: &forge_project::zone_authoring::session::ZoneReceipt,
) -> Result<(), String> {
    if !super::authoring_viewport::can_preview_asset()
        || !super::authoring_regeneration_review::matches_source(Some(head), receipt)
        || receipt.inverse != head.corrections
        || receipt.base_change.is_some()
    {
        return Err("Aperçu d'exclusions indisponible ou périmé".into());
    }
    let mut unchanged = receipt.applied.clone();
    unchanged
        .vegetation_exclusions
        .clone_from(&head.corrections.vegetation_exclusions);
    if unchanged != head.corrections {
        return Err("La proposition change d'autres retouches".into());
    }
    let mut candidate = head.clone();
    candidate.corrections = receipt.applied.clone();
    candidate.version = candidate.corrections.schema_version();
    candidate.revision = receipt.resulting_revision;
    if candidate.content_hash()? != receipt.resulting_hash {
        return Err("Reçu d'exclusions incohérent".into());
    }
    candidate.revision = head.revision;
    let terrain = super::authoring_scene::terrain(head)?;
    let vegetation = candidate.effective()?.1.ok_or("Végétation absente")?;
    let mut pending = Pending::new((
        terrain,
        vegetation,
        head.corrections.paths.clone().unwrap_or_default(),
        head.corrections.rivers.clone().unwrap_or_default(),
        head.corrections.buildings.clone().unwrap_or_default(),
        head.corrections.promotions.clone().unwrap_or_default(),
    ))?;
    if ACCEPTED.with(|slot| slot.borrow().is_none()) {
        return Err("Source acceptée indisponible pour restaurer l’aperçu".into());
    }
    pending.preview = true;
    PREVIEW_ACTIVE.with(|active| active.set(true));
    PENDING.with(|slot| slot.replace(Some(pending)));
    report(
        "Aperçu d'exclusions demandé, non appliqué ; Abandonner restaure la version acceptée."
            .into(),
    );
    Ok(())
}

pub(super) fn preview_description(
    head: Option<&forge_project::zone_authoring::ZoneAuthoringHead>,
    receipt: &forge_project::zone_authoring::session::ZoneReceipt,
) -> String {
    head.ok_or_else(|| "Zone absente".to_owned())
        .and_then(|head| preview(head, receipt))
        .map_or_else(
            |error| format!("Aperçu 3D indisponible : {error} ; retour à la vue acceptée demandé."),
            |()| "Aperçu végétal demandé, non appliqué ; terrain, eau et bâtiments acceptés inchangés. Abandonner restaure la végétation acceptée.".into(),
        )
}

pub(crate) fn preview_promotion(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    receipt: &forge_project::zone_authoring::session::ZoneReceipt,
) -> Result<(), String> {
    if !super::authoring_viewport::can_preview_asset()
        || !super::authoring_regeneration_review::matches_source(Some(head), receipt)
        || receipt.inverse != head.corrections
    {
        return Err("Promotion périmée ou aperçu indisponible".into());
    }
    let mut unchanged = receipt.applied.clone();
    unchanged
        .placements
        .clone_from(&head.corrections.placements);
    unchanged
        .promotions
        .clone_from(&head.corrections.promotions);
    if unchanged != head.corrections {
        return Err("La promotion change d'autres recettes".into());
    }
    let mut candidate = head.clone();
    if let Some(change) = &receipt.base_change {
        if change.inverse != head.base
            || change.applied.terrain != head.base.terrain
            || receipt.applied.placements != head.corrections.placements
            || !matches!(
                receipt.request.action,
                forge_project::zone_authoring::session::ZoneAction::ReconcilePromotions(_)
            )
        {
            return Err(
                "Réconciliation végétale incompatible avec le terrain ou les placements".into(),
            );
        }
        candidate.base = change.applied.clone();
    }
    candidate.corrections = receipt.applied.clone();
    candidate.version = candidate.corrections.schema_version();
    candidate.revision = receipt.resulting_revision;
    if candidate.content_hash()? != receipt.resulting_hash {
        return Err("Reçu de promotion incohérent".into());
    }
    // Derived preview stays on the accepted terrain revision until Apply.
    candidate.revision = head.revision;
    let terrain = super::authoring_scene::terrain(head)?;
    let (_, vegetation) = candidate.effective()?;
    let vegetation = vegetation.map_or_else(|| empty_source(&terrain), Ok)?;
    let mut pending = Pending::new((
        terrain,
        vegetation,
        candidate.corrections.paths.unwrap_or_default(),
        candidate.corrections.rivers.unwrap_or_default(),
        candidate.corrections.buildings.unwrap_or_default(),
        candidate.corrections.promotions.unwrap_or_default(),
    ))?;
    if ACCEPTED.with(|s| s.borrow().is_none()) {
        return Err("Projection acceptée absente".into());
    }
    pending.preview = true;
    pending.wait_for_placement = receipt.base_change.is_none();
    let key = forge_core::canonical_json_sha256(&receipt.request).map_err(|e| e.to_string())?;
    pending.promotion_key = Some(key);
    let accepted = head.corrections.placements.as_deref().unwrap_or_default();
    let hidden_placements = receipt
        .applied
        .placements
        .as_deref()
        .unwrap_or_default()
        .iter()
        .filter(|p| accepted.binary_search_by_key(&p.id, |p| p.id).is_err())
        .map(|p| p.id)
        .collect();
    PROMOTION_GATE.with(|gate| gate.borrow_mut().begin(key, hidden_placements));
    notify_gate(false);
    PREVIEW_ACTIVE.with(|flag| flag.set(true));
    PENDING.with(|slot| slot.replace(Some(pending)));
    Ok(())
}

fn preview(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    receipt: &forge_project::zone_authoring::session::ZoneReceipt,
) -> Result<(), String> {
    if !super::authoring_viewport::can_preview_asset() {
        return Err("Projection absente ou mode jeu actif".into());
    }
    let vegetation = super::authoring_regeneration_review::vegetation_proposal(head, receipt)?;
    let terrain = super::authoring_scene::terrain(head)?;
    let vegetation = vegetation.map_or_else(|| empty_source(&terrain), Ok)?;
    let mut pending = Pending::new((
        terrain,
        vegetation,
        head.corrections.paths.clone().unwrap_or_default(),
        head.corrections.rivers.clone().unwrap_or_default(),
        head.corrections.buildings.clone().unwrap_or_default(),
        head.corrections.promotions.clone().unwrap_or_default(),
    ))?;
    if ACCEPTED.with(|slot| slot.borrow().is_none()) {
        return Err("Source acceptée indisponible pour restaurer l’aperçu".into());
    }
    pending.preview = true;
    PREVIEW_ACTIVE.with(|active| active.set(true));
    PENDING.with(|slot| slot.replace(Some(pending)));
    report(
        "Aperçu végétal demandé, non appliqué ; Abandonner restaure la version acceptée.".into(),
    );
    Ok(())
}

pub(super) fn install(app: &mut App, terrain: TerrainDocument) -> Result<(), String> {
    // Existing presentation presets only; replace all area-scoped identity fields.
    // This renderer policy is derived, not a second authored zone document.
    let mut policy = forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .map_err(|error| error.to_string())?;
    policy.project_id = terrain.project_id;
    policy.area_id = terrain.area_id;
    policy.revision = terrain.revision;
    policy.name = "Présentation végétation auteur".into();
    let source = empty_source(&terrain)?;
    let request = forge_engine::VegetationQualityRequest::new(
        &terrain,
        policy,
        forge_project::VisualQualityTier::High,
    )
    .map_err(|error| error.to_string())?;
    request
        .validate_vegetation(&source)
        .map_err(|error| error.to_string())?;
    app.insert_resource(request)
        .add_plugins(forge_engine::VegetationPrototypeLoaderPlugin)
        .add_plugins(forge_engine::VisualValleyVegetationPlugin::new(
            terrain, source,
        ))
        .add_systems(Last, project)
        .add_systems(Update, crate::authoring_species::selection::pick);
    Ok(())
}

fn projection_inputs_ready(world: &mut World) -> bool {
    if !super::authoring_viewport::can_project_sources() {
        return false;
    }
    if PENDING.with(|s| s.borrow().as_ref().is_some_and(|p| p.wait_for_placement)) {
        match super::authoring_assets::placements::proposal_ready() {
            Ok(true) => {}
            Ok(false) => return false,
            Err(error) => {
                super::authoring_preview::clear();
                report(format!(
                    "Aperçu de promotion abandonné : {error}. Restauration demandée."
                ));
                return false;
            }
        }
    }
    match crate::authoring_species::ready(world) {
        Ok(true) => true,
        Ok(false) => false,
        Err(error) => {
            if let Some(pending) = PENDING.with(|slot| slot.borrow_mut().take()) {
                if pending.promotion_key.is_some() {
                    super::authoring_preview::clear();
                }
                report(format!("Espèces non projetées : {error}"));
            }
            false
        }
    }
}

fn project(world: &mut World) {
    if !projection_inputs_ready(world) {
        return;
    }
    let mut failed_promotion = false;
    let message = PENDING.with(|slot| {
        let mut slot = slot.borrow_mut();
        let pending = slot.as_mut()?;
        let runtime = world.get_resource::<forge_engine::TerrainRuntimeState>()?;
        if runtime.cache().source_document_hash != pending.terrain_hash {
            return None;
        }
        let stats = world.get_resource::<forge_engine::VegetationProjectionStats>()?;
        if stats.terrain_document_hash == pending.terrain_key
            && stats.vegetation_document_hash == pending.vegetation_key
            && forge_engine::governed_forest_ready(world)
        {
            if let Some(key) = pending.promotion_key {
                PROMOTION_GATE.with(|gate| gate.borrow_mut().acknowledge(key));
                notify_gate(true);
            }
            let message = format!(
                "{} projetée côté moteur : {}. Présentation GPU à vérifier.",
                if pending.preview {
                    "Proposition de végétation (non appliquée)"
                } else {
                    "Végétation acceptée"
                },
                pending.vegetation_key
            );
            *slot = None;
            return Some(message);
        }
        if pending.submitted {
            if let Some(quality) = &stats.quality
                && quality.status.starts_with("rejected_")
            {
                let message = format!("Projection végétation refusée : {}", quality.status);
                failed_promotion = pending.promotion_key.is_some();
                *slot = None;
                return Some(message);
            }
            return None;
        }
        // Clone and hash only on source receipt/submission, never once per rendered frame.
        match forge_engine::update_vegetation_sources_with_promotions(
            world,
            pending.terrain.clone(),
            pending.vegetation.clone(),
            pending.paths.clone(),
            pending.rivers.clone(),
            pending.buildings.clone(),
            pending.promotions.clone(),
        ) {
            Ok(_) => {
                pending.submitted = true;
                Some(if pending.preview {
                    "Aperçu végétal en reconstruction, non appliqué.".into()
                } else {
                    "Végétation acceptée en reconstruction.".into()
                })
            }
            Err(error) => {
                failed_promotion = pending.promotion_key.is_some();
                *slot = None;
                Some(format!("Projection végétation refusée : {error}"))
            }
        }
    });
    if failed_promotion {
        // Restore only after releasing PENDING's mutable borrow.
        super::authoring_preview::clear();
    }
    if let Some(message) = message {
        report(if failed_promotion {
            format!("{message}. Promotion non appliquée, restauration demandée.")
        } else {
            message
        });
    }
}
