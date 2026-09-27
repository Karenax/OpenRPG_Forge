//! Aggregate authoring presentation. No filesystem access or alternate writer.
use forge_project::{
    authoring::{AuthoringRequest, AuthoringState},
    terrain_edit::TerrainAction,
    zone_authoring::{
        GeneratorStamp, ZoneAuthoringHead, migration::ZoneMigrationProposal, session::ZoneReceipt,
    },
};
use leptos::prelude::*;
pub(super) mod hierarchy;
pub(crate) mod reconciliation;

#[component]
pub(super) fn SourceControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    active_tab: RwSignal<super::authoring_controls::AuthoringTab>,
    path: RwSignal<String>,
    river: RwSignal<String>,
    building: RwSignal<String>,
    vegetation_selection: RwSignal<String>,
    on_command: Callback<Result<AuthoringRequest, String>>,
) -> impl IntoView {
    let nature_tool = RwSignal::new("paint".to_owned());
    Effect::new(move |previous: Option<Option<forge_core::AreaId>>| {
        let area = state
            .get()
            .and_then(|state| state.zone_head)
            .map(|head| head.base.terrain.area_id);
        if previous != Some(area) {
            path.set(String::new());
            river.set(String::new());
            building.set(String::new());
            super::authoring_building_projection::selection::cancel();
            super::authoring_assets::placements::selection::clear();
        }
        area
    });
    view! {
        <section class="authoring-panel" data-authoring-tab="project" hidden=move || active_tab.get() != super::authoring_controls::AuthoringTab::Project>
            <MigrationControls state=state busy=busy on_command=on_command/>
        </section>
        <section class="authoring-panel" data-authoring-tab="nature" hidden=move || active_tab.get() != super::authoring_controls::AuthoringTab::Nature>
            <h2>"Nature"</h2>
            <label>"Outil de végétation"<select prop:value=move || nature_tool.get() on:change=move |ev| {
                super::authoring_brush_pointer::cancel(); nature_tool.set(event_target_value(&ev));
            }><option value="paint">"Peindre la densité"</option><option value="generate">"Générer la végétation"</option><option value="species">"Choisir les espèces"</option><option value="layers">"Régler les couches"</option><option value="exclude">"Créer une clairière"</option></select></label>
            <div hidden=move || nature_tool.get() != "paint"><super::authoring_controls::VegetationDensityControls state=state busy=busy vegetation_selection=vegetation_selection send=on_command/></div>
            <Show when=move || nature_tool.get() == "paint" && state.get().is_some_and(|s| s.zone_head.is_some_and(|h| h.base.vegetation.is_none()))>
                <div class="gold-empty-state"><h3>"Votre première forêt"</h3><p>"Cette carte n’a pas encore de couche végétale. Commencez par en générer une, puis peignez sa densité."</p>
                <button class="authoring-primary" on:click=move |_| nature_tool.set("generate".into())>"Préparer la végétation"</button></div>
            </Show>
            <div hidden=move || nature_tool.get() != "generate">
            <VegetationControls state=state busy=busy on_command=on_command/>
            </div><div hidden=move || nature_tool.get() != "exclude">
            <super::authoring_exclusions::ExclusionControls state=state busy=busy on_command=on_command/>
            </div><div hidden=move || nature_tool.get() != "species">
            <super::authoring_species::SpeciesControls state=state busy=busy on_command=on_command/>
            </div><div hidden=move || nature_tool.get() != "layers">
            <super::authoring_vegetation_recipe::LayerControls state=state busy=busy selected=vegetation_selection on_command=on_command/>
            </div>
        </section>
        <section class="authoring-panel" data-authoring-tab="routes" hidden=move || active_tab.get() != super::authoring_controls::AuthoringTab::Routes>
            <h2>"Chemins"</h2>
            <super::authoring_paths::PathControls state=state busy=busy selected=path active_tab=active_tab on_command=on_command/>
        </section>
        <section class="authoring-panel" data-authoring-tab="water" hidden=move || active_tab.get() != super::authoring_controls::AuthoringTab::Water>
            <h2>"Eau et rivières"</h2>
            <super::authoring_rivers::RiverControls state=state busy=busy selected=river on_command=on_command/>
        </section>
        <section class="authoring-panel" data-authoring-tab="buildings" hidden=move || active_tab.get() != super::authoring_controls::AuthoringTab::Buildings>
            <h2>"Construire"</h2>
            <super::authoring_buildings::BuildingControls state=state busy=busy selected=building on_command=on_command/>
        </section>
        <section class="authoring-panel" data-authoring-tab="follow-up" hidden=move || active_tab.get() != super::authoring_controls::AuthoringTab::FollowUp>
            <h2>"Tester et suivre la zone"</h2>
            <p>"Jouer avec les commandes du haut, choisir un départ ou consulter l’historique."</p>
            <CompactionControls state=state busy=busy on_command=on_command/>
        </section>
    }
}

pub(super) fn prepare_terrain(
    head: &ZoneAuthoringHead,
    action: TerrainAction,
) -> Result<AuthoringRequest, String> {
    let identity =
        serde_json::to_string(&(head.content_hash()?, &action)).map_err(|e| e.to_string())?;
    let is_edit = matches!(action, TerrainAction::Edit(_));
    let request =
        head.prepare_terrain_action(action, forge_core::CommandId::deterministic(identity))?;
    Ok(if is_edit {
        AuthoringRequest::PreviewZone { request }
    } else {
        AuthoringRequest::ApplyZone { request }
    })
}

fn placement_changes_label(
    changes: forge_project::zone_authoring::placements::PlacementChanges,
) -> String {
    format!(
        "{} ajout(s), {} retrait(s), {} référence(s) remplacée(s), {} transformation(s), {} nom(s), {} verrou(s), {} objet(s) inchangé(s)",
        changes.added,
        changes.removed,
        changes.references,
        changes.transforms,
        changes.names,
        changes.locks,
        changes.unchanged,
    )
}

pub(super) fn preview_label(state: &AuthoringState, receipt: &ZoneReceipt) -> String {
    if !super::authoring_regeneration_review::matches_source(state.zone_head.as_ref(), receipt) {
        super::authoring_preview::clear();
        return "Proposition périmée : la zone ou sa révision a changé. Réexaminer la retouche avant de l’appliquer ; aucun nouvel aperçu demandé.".into();
    }
    let corrections = &receipt.applied;
    if receipt.applied.promotions != receipt.inverse.promotions {
        return promotion_preview_label(state, receipt);
    }
    if let Some(label) = super::authoring_exclusions::preview_label(state, receipt) {
        return label;
    }
    if let Some(label) = super::authoring_species::preview_label(state, receipt) {
        return label;
    }
    if receipt.applied.start != receipt.inverse.start {
        return start_preview_label(receipt);
    }
    if let Some(label) = super::authoring_buildings::preview_label(state, receipt) {
        return label;
    }
    if let Some(head) = &state.zone_head
        && head.corrections.rivers != corrections.rivers
    {
        return format!(
            "Proposition rivière · non appliquée. {}. Aperçu du relief uniquement ; eau et végétation restent acceptées jusqu'à Appliquer.{}",
            super::authoring_rivers::describe_changes(
                head.corrections.rivers.as_deref().unwrap_or_default(),
                corrections.rivers.as_deref().unwrap_or_default(),
            ),
            super::authoring_preview::prepare_zone(state, receipt),
        );
    }
    if let Some(head) = &state.zone_head
        && head.corrections.paths != corrections.paths
    {
        super::authoring_preview::clear();
        let projection = super::authoring_path_projection::preview(state, receipt).map_or_else(
            |error| format!("Aperçu 3D indisponible : {error}"),
            |()| "Aperçu cyan demandé ; végétation inchangée avant application".into(),
        );
        return format!(
            "Proposition chemins · {} → {} tracés · {} points · non appliquée. {projection}. {}",
            head.corrections.paths.as_ref().map_or(0, Vec::len),
            corrections.paths.as_ref().map_or(0, Vec::len),
            corrections.paths.as_ref().map_or(0, |paths| paths
                .iter()
                .map(|p| p.points.len())
                .sum::<usize>()),
            super::authoring_paths::describe_changes(
                head.corrections.paths.as_deref().unwrap_or_default(),
                corrections.paths.as_deref().unwrap_or_default()
            ),
        );
    }
    if let Some(head) = &state.zone_head
        && head.corrections.placements != corrections.placements
    {
        let changes = forge_project::zone_authoring::placements::PlacementChanges::between(
            head.corrections.placements.as_deref().unwrap_or_default(),
            corrections.placements.as_deref().unwrap_or_default(),
        );
        let details = match changes {
            Ok(changes) => placement_changes_label(changes),
            Err(reason) => return format!("Comparaison des placements refusée : {reason}"),
        };
        let result = super::authoring_assets::placements::proposal(state, receipt);
        return format!(
            "Proposition placements · {} → {} objets · {details} · non appliquée · {}",
            head.corrections.placements.as_ref().map_or(0, Vec::len),
            corrections.placements.as_ref().map_or(0, Vec::len),
            result.map_or_else(
                |e| e,
                |()| "chargement de l’aperçu 3D demandé ; consulter son état".into()
            )
        );
    }
    if let Some(head) = &state.zone_head
        && head.corrections.density != corrections.density
    {
        return format!(
            "Proposition densité · non appliquée. {}{}",
            super::authoring_density_review::describe(
                &receipt.inverse.density,
                &corrections.density
            ),
            super::authoring_preview::prepare_density(head, receipt),
        );
    }
    if let Some(label) = super::authoring_regeneration_review::label(receipt) {
        super::authoring_preview::clear();
        return format!(
            "{label} {}",
            super::authoring_vegetation::preview_description(state.zone_head.as_ref(), receipt)
        );
    }
    format!(
        "{}{}",
        super::authoring_regeneration_review::terrain_label(state.zone_head.as_ref(), receipt),
        super::authoring_preview::prepare_zone(state, receipt)
    )
}

fn promotion_preview_label(state: &AuthoringState, receipt: &ZoneReceipt) -> String {
    super::authoring_preview::clear();
    let result = (|| {
        let head = state.zone_head.as_ref().ok_or("Zone absente")?;
        super::authoring_vegetation::preview_promotion(head, receipt)?;
        if receipt.base_change.is_none() {
            super::authoring_assets::placements::proposal(state, receipt)?;
        }
        Ok::<(), String>(())
    })();
    match result {
        Ok(()) if receipt.base_change.is_some() => "Réconciliation non appliquée : nouvelle végétation et correspondances demandées ensemble ; objets retouchés inchangés. Abandonner restaure la recette acceptée.".into(),
        Ok(()) => "Promotion non appliquée : retrait du scatter et chargement du placement demandés. Attendre les deux projections ; Abandonner restaure la version acceptée.".into(),
        Err(error) => {
            super::authoring_preview::clear();
            format!("Aperçu de promotion refusé : {error}. Version acceptée restaurée sur demande.")
        }
    }
}

fn start_preview_label(receipt: &ZoneReceipt) -> String {
    super::authoring_preview::clear();
    format!(
        "Départ de zone · {} → {} · non appliqué. Altitude calculée sur le terrain au lancement ; choisir une position libre. Appliquer puis sauvegarder.",
        super::authoring_start_label::describe(receipt.inverse.start.as_deref()),
        super::authoring_start_label::describe(receipt.applied.start.as_deref())
    )
}

#[component]
pub(super) fn VegetationControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    on_command: Callback<Result<AuthoringRequest, String>>,
) -> impl IntoView {
    let seed = RwSignal::new("42".to_owned());
    let recipe = Memo::new(move |_| {
        seed.get()
            .trim()
            .parse::<u64>()
            .map(|seed| crate::reconciliation_protocol::ReconciliationRecipe::Seed { seed })
            .map_err(|_| "Seed invalide".to_owned())
    });
    view! {
        <Show when=move || state.get().is_some_and(|s| s.zone_head.is_some())>
            <details>
                <summary>"Recette de végétation"</summary>
                <p>"Initialiser une recette arbres/herbe, ou modifier uniquement sa seed. Les masques et verrous restent conservés. Appliquer l’aperçu puis sauvegarder. La végétation se reconstruit après synchronisation du terrain."</p>
                <super::authoring_vegetation::ProjectionStatus/>
                <output>{move || state.get().and_then(|s| s.zone_head).and_then(|h| h.base.vegetation).map_or_else(|| "Aucune recette acceptée".into(), |v| format!("Seed acceptée : {} · {} couches", v.seed, v.layers.len()))}</output>
                <label>"Nouvelle seed de végétation"<input inputmode="numeric" prop:value=move || seed.get() on:input=move |ev| { seed.set(event_target_value(&ev)); super::authoring_shortcuts::cancel_preview(); }/></label>
                <button disabled=move || busy.get() || state.get().is_none_or(|s| s.playtest_hash.is_some())
                    on:click=move |_| on_command.run(vegetation_request(state.get_untracked(), &seed.get_untracked()))>"Prévisualiser la recette de végétation"</button>
                <reconciliation::ReconciliationControls state=state busy=busy recipe=recipe on_command=on_command/>
            </details>
        </Show>
    }
}

fn vegetation_request(
    state: Option<AuthoringState>,
    seed: &str,
) -> Result<AuthoringRequest, String> {
    let head = state
        .and_then(|s| s.zone_head)
        .ok_or("Convertir la zone avant d’éditer sa végétation")?;
    let seed = seed
        .trim()
        .parse::<u64>()
        .map_err(|_| "Seed invalide : entier positif ou nul requis")?;
    forge_world::editor_vegetation::prepare_request(&head, seed)
        .map(|request| AuthoringRequest::PreviewZone { request })
}

#[component]
pub(super) fn CompactionControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    on_command: Callback<Result<AuthoringRequest, String>>,
) -> impl IntoView {
    let review =
        RwSignal::new(None::<forge_project::zone_authoring::session::ZoneCompactionReview>);
    Effect::new(move |_| review.set(state.get().and_then(|s| s.zone_compaction)));
    view! {
        <Show when=move || state.get().is_some_and(|s| s.zone_head.is_some())>
            <details>
                <summary>"Taille du journal de zone"</summary>
                <p>"Format compact v6 : évite de répéter les corrections inchangées et conserve commandes, Undo/Redo et retouches. Examiner puis convertir ; Sauvegarder reste nécessaire. Le changement de format ne fait pas partie d’Undo."</p>
                <button disabled=move || busy.get() on:click=move |_| on_command.run(Ok(AuthoringRequest::ReviewZoneCompaction))>"Examiner la réduction du journal"</button>
                <output>{move || review.get().map(|r| format!("{} → {} octets · {} commandes conservées · aucune écriture", r.source_bytes, r.target_bytes, r.retained_commands))}</output>
                <button disabled=move || busy.get() || review.get().is_none()
                    on:click=move |_| on_command.run(review.get_untracked().map(|review| AuthoringRequest::CompactZoneArchive { review }).ok_or_else(|| "Examiner le journal avant conversion".into()))>"Convertir le journal examiné (sans sauvegarder)"</button>
            </details>
        </Show>
    }
}

#[component]
pub(super) fn MigrationControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    on_command: Callback<Result<AuthoringRequest, String>>,
) -> impl IntoView {
    let proposal = RwSignal::new(None::<ZoneMigrationProposal>);
    Effect::new(move |_| {
        if let Some(state) = state.get() {
            if let Some(reviewed) = state.zone_migration {
                proposal.set(Some(reviewed));
            } else if state.zone_head.is_some() {
                proposal.set(None);
            } else if let Some(document) = state.document {
                // Retain the review across Close, not across edits or a different zone.
                if proposal.get_untracked().is_some_and(|p| {
                    p.head().base.terrain.area_id != document.area_id
                        || p.head().revision != document.revision
                }) {
                    proposal.set(None);
                }
            }
        }
    });
    view! {
        <details>
            <summary>"Source de zone et conservation des retouches"</summary>
            <Show when=move || state.get().is_some_and(|s| s.zone_head.is_some()) fallback=move || view! {
                <p>"Conversion explicite : terrain accepté comme base, retouches séparées et archive historique conservée. Sauvegarder, examiner, puis convertir. La conversion ferme la session sauvegardée avant de la migrer. Undo repart de la conversion ; l’ancien journal reste archivé. Le playtest attend la concordance des projections et des collisions ; la recette de végétation se retouche dans son panneau."</p>
                <button disabled=move || busy.get() || state.get().is_none_or(|s| s.document.is_none() || s.dirty || s.playtest_hash.is_some())
                    on:click=move |_| on_command.run(review_request(state.get_untracked()))>"Examiner la migration de zone"</button>
                <output>{move || proposal.get().map(|p| format!("Proposition vérifiée · {} · révision {:?}. Aucun fichier modifié.", p.head().base.terrain.name, p.head().revision))}</output>
                <button disabled=move || busy.get() || proposal.get().is_none() || state.get().is_none_or(|s| s.dirty || s.playtest_hash.is_some())
                    on:click=move |_| begin_reviewed(proposal.get_untracked(), state, busy, on_command)>"Convertir la zone examinée (sans sauvegarder)"</button>
            }>
                <p>"Source unifiée active. Les retouches terrain préservent les masques et verrous de végétation. Sauvegarde et Undo/Redo portent sur toute la zone. Le playtest attend la concordance des projections et des collisions avec la révision acceptée."</p>
            </Show>
        </details>
    }
}

fn begin_reviewed(
    proposal: Option<ZoneMigrationProposal>,
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    on_command: Callback<Result<AuthoringRequest, String>>,
) {
    let Some(proposal) = proposal else {
        return;
    };
    if busy.get_untracked() {
        return;
    }
    busy.set(true);
    // One explicit user action, two ordinary host commands. Keep the reviewed
    // proposal in this future across Close; don't reload an intermediate page.
    wasm_bindgen_futures::spawn_local(async move {
        let result = crate::web::invoke_tauri(&crate::StudioCommand::Authoring {
            request: Box::new(AuthoringRequest::Close),
        })
        .await;
        busy.set(false);
        match result {
            Ok(crate::StudioResponse::Authoring { state: closed }) => {
                super::authoring_viewport::observe(None, None);
                state.set(Some(*closed));
                on_command.run(Ok(AuthoringRequest::BeginZoneMigration {
                    proposal: Box::new(proposal),
                }));
            }
            Ok(crate::StudioResponse::Rejected { reason }) | Err(reason) => {
                on_command.run(Err(reason));
            }
            Ok(_) => on_command.run(Err(
                "Réponse de fermeture inattendue ; migration non lancée".into(),
            )),
        }
    });
}

fn review_request(state: Option<AuthoringState>) -> Result<AuthoringRequest, String> {
    let document = state
        .and_then(|s| s.document)
        .ok_or("Ouvrir une zone avant examen")?;
    let expected_hash = document.content_hash().map_err(|e| e.to_string())?;
    Ok(AuthoringRequest::PrepareZoneMigration {
        expected_hash,
        generator: GeneratorStamp {
            name: "accepted-terrain-checkpoint".into(),
            version: "1".into(),
            inputs_hash: expected_hash,
        },
        // Do not invent vegetation or claim historical generator provenance.
        vegetation: None,
    })
}
