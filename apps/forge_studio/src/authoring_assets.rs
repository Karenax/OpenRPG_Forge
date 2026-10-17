//! Metadata browser for the deployed governed kit. Does not load a second renderer.
use forge_assets::{
    GovernedAssetKitBuildReport,
    asset_browser::{AssetBrowser, category_label},
};
use leptos::prelude::*;
use wasm_bindgen::JsCast;

mod catalog;
pub(crate) mod catalog_drop;
pub(crate) mod fish_props_scene;
mod fish_shadow_scene;
pub(crate) mod fish_stand_scene;
pub(crate) mod imported_buildings;
pub(crate) mod imported_environment;
pub(crate) mod imported_scene;
mod map_shelf;
mod model_read_gate;
pub(crate) mod objects;
mod placement_controls;
pub(crate) mod placements;
pub(crate) mod preview;
#[allow(
    dead_code,
    reason = "Initial source host contexts await preview/placement routing"
)]
mod source_graph_bootstrap;
#[allow(
    dead_code,
    reason = "Dynamic source frames await viewport pass activation"
)]
mod source_graph_context;
#[allow(
    dead_code,
    reason = "Explicit frame updates await source-scene viewport activation"
)]
mod source_graph_frame;
#[allow(
    dead_code,
    reason = "Shared source scene host awaits viewport pass activation"
)]
mod source_graph_host;
#[allow(
    dead_code,
    reason = "Shared intake awaits qualified viewport attachment and resource routing"
)]
mod source_graph_intake;
#[allow(
    dead_code,
    reason = "Source light transport is being qualified before viewport activation"
)]
mod source_graph_lights;
mod source_graph_lod;
mod source_graph_particles;
mod source_graph_physics;
mod source_graph_placement;
mod source_graph_preview;
#[allow(
    dead_code,
    reason = "Source intake and pass routing are being qualified before viewport activation"
)]
mod source_graph_resources;
mod source_graph_route;
#[allow(
    dead_code,
    reason = "Shared lifecycle installed before preview/placement routing"
)]
mod source_graph_runtime;
#[allow(
    dead_code,
    reason = "Source intake and pass routing are being qualified before viewport activation"
)]
mod source_graph_scene;
#[allow(
    dead_code,
    reason = "Native LOD driver remains to be qualified before reveal"
)]
mod source_graph_state;
pub(crate) mod vault;
pub(crate) mod verify;

/// Verified user-library GLBs share the current authoring preview world.
pub(crate) fn preview_imported_asset(id: String, bytes: Vec<u8>) -> Result<(), String> {
    if !crate::authoring_viewport::can_preview_asset() {
        return Err("Ouvrir la carte en mode édition pour prévisualiser le modèle.".into());
    }
    let ticket = preview::ticket();
    if preview::submit_scene_with_physics(ticket, id, bytes, None, None) {
        Ok(())
    } else {
        Err("Aperçu remplacé ; recommencer.".into())
    }
}

/// The same pointer and Preview/Apply commands own imported model placement.
pub(crate) fn place_imported_asset(
    bindings: crate::atelier_ui::MainBindings,
    name: String,
    id: forge_core::AssetId,
    sha256: forge_core::ContentHash,
) -> Result<(), String> {
    if bindings.busy.get_untracked() || !crate::authoring_viewport::can_preview_asset() {
        return Err("Ouvrir la carte en mode édition et terminer la commande en cours.".into());
    }
    let head = placement_controls::head(bindings.state.get_untracked())?;
    let expected = head.content_hash()?;
    cancel_preview();
    crate::authoring_brush_pointer::arm(
        2.0,
        false,
        Callback::new(move |points: Result<Vec<[f32; 2]>, String>| {
            let request = (|| {
                let current = placement_controls::head(bindings.state.get_untracked())?;
                if current.content_hash()? != expected {
                    return Err("Zone modifiée ; recommencer le placement.".into());
                }
                let point = *points?.first().ok_or("Cliquer le terrain")?;
                placement_controls::insertion::prepare(
                    &current,
                    &name,
                    (id, sha256),
                    (
                        forge_core::Transform::IDENTITY,
                        forge_world::placement_snap::PlacementSnap {
                            grid_step_m: None,
                            surface_offset_m: Some(0.0),
                        },
                    ),
                    point,
                )
            })();
            bindings.send.run(request);
        }),
    );
    bindings
        .status
        .set("Cliquer le terrain pour proposer le placement ; examiner puis appliquer.".into());
    Ok(())
}

/// Adopt a reviewed library revision for one selected object, keeping its identity.
pub(crate) fn replace_imported_asset(
    bindings: crate::atelier_ui::MainBindings,
    id: forge_core::AssetId,
    sha256: forge_core::ContentHash,
) -> Result<(), String> {
    if bindings.busy.get_untracked() || !crate::authoring_viewport::can_preview_asset() {
        return Err("Terminer la commande en cours et ouvrir la carte en mode édition.".into());
    }
    let selected = bindings
        .placement
        .get_untracked()
        .ok_or("Sélectionner l’objet à remplacer sur la carte.")?;
    let head = placement_controls::head(bindings.state.get_untracked())?;
    let source = objects::lookup(&head, selected)?;
    if source.physics.is_some() || source.particles.is_some() {
        return Err("Cet objet porte un paquet physique ou d’effet. Utiliser un remplacement qui traite explicitement ces données.".into());
    }
    let action = forge_project::zone_authoring::placements::PlacementEdit::ReplaceAsset {
        ids: vec![selected],
        expected_asset_id: source.asset_id,
        expected_asset_hash: source.asset_hash,
        asset_id: id,
        asset_hash: sha256,
    };
    let key = serde_json::to_string(&(head.content_hash()?, &action)).map_err(|e| e.to_string())?;
    let request = objects::edit(
        &head,
        action,
        forge_core::CommandId::deterministic(format!("imported-asset-replacement:{key}")),
    )?;
    cancel_preview();
    bindings.send.run(Ok(
        forge_project::authoring::AuthoringRequest::PreviewZone { request },
    ));
    Ok(())
}

thread_local! {
    static PREVIEW_STATUS: std::cell::RefCell<Option<RwSignal<String>>> = const { std::cell::RefCell::new(None) };
}

fn preview_status(message: &str) {
    PREVIEW_STATUS.with(|slot| {
        if let Some(output) = *slot.borrow() {
            output.set(message.into());
        }
    });
}

pub(crate) fn cancel_preview() {
    catalog_drop::cancel();
    crate::authoring_brush_pointer::cancel();
    preview::cancel();
    crate::authoring_camera::leave_preview();
    preview_status("Aucun aperçu actif. La carte n’a pas été modifiée.");
}

#[component]
#[allow(
    clippy::too_many_lines,
    reason = "Shelf and inspector share one catalog, verification and placement session"
)]
pub(super) fn AssetBrowserControls(
    state: RwSignal<Option<forge_project::authoring::AuthoringState>>,
    host_busy: RwSignal<bool>,
    inspected: RwSignal<Option<forge_core::EntityId>>,
    on_command: Callback<Result<forge_project::authoring::AuthoringRequest, String>>,
) -> impl IntoView {
    catalog::mount(imported_buildings::catalog_entries);
    let browser = RwSignal::new(None::<AssetBrowser>);
    let query = RwSignal::new(String::new());
    let category = RwSignal::new(String::new());
    let map_objects = RwSignal::new(true);
    let atelier = use_context::<crate::atelier_ui::Link>();
    let selected = atelier.map_or_else(
        || RwSignal::new(String::new()),
        |link| link.catalog_selected,
    );
    // Keep every form mounted: switching workspaces must not discard a draft.
    let workspace = RwSignal::new("manipulate");
    let short_surface = || {
        web_sys::window()
            .and_then(|w| w.inner_height().ok())
            .and_then(|v| v.as_f64())
            .is_some_and(crate::panel_layout::compact_library)
    };
    let compact = RwSignal::new(short_surface());
    let library_open = RwSignal::new(!compact.get_untracked());
    let resize = window_event_listener(leptos::ev::resize, move |_| {
        let next = short_surface();
        if compact.get_untracked() != next {
            compact.set(next);
            if next {
                library_open.set(false);
            }
        }
    });
    on_cleanup(move || resize.remove());
    Effect::new(move |_| {
        workspace.set(if inspected.get().is_some() {
            "transform"
        } else {
            "manipulate"
        });
    });
    Effect::new(move |_| {
        if let Some(link) = atelier {
            link.catalog.set(
                catalog::entries(browser.get().as_ref())
                    .into_iter()
                    .take(4096)
                    .map(|entry| crate::atelier_protocol::CatalogItem {
                        id: entry.id,
                        category: category_label(&entry.kind).to_owned(),
                    })
                    .collect(),
            );
        }
    });
    let busy = RwSignal::new(false);
    let verification = RwSignal::new(String::new());
    let preview_output = RwSignal::new(String::new());
    Effect::new(move |_| {
        let _ = selected.get();
        verification.set(String::new());
        preview_output.set(String::new());
    });
    PREVIEW_STATUS.with(|slot| slot.replace(Some(preview_output)));
    on_cleanup(|| {
        catalog::unmount();
        catalog_drop::cancel();
        preview::cancel();
        PREVIEW_STATUS.with(|slot| slot.replace(None));
    });
    let status = RwSignal::new(
        "Catalogue non chargé. Les placements exigent une zone convertie et des ressources vérifiées."
            .to_owned(),
    );
    let matches = Memo::new(move |_| {
        let raw_query = query.get();
        let query = catalog::search_terms(&raw_query);
        let category = category.get();
        Some(if raw_query.len() > 256 {
            Err("Recherche limitée à 256 octets".to_owned())
        } else {
            Ok(catalog::entries(browser.get().as_ref())
                .into_iter()
                .filter(|entry| {
                    let terms = catalog::search_terms(&format!(
                        "{} {} {} {}",
                        entry.id,
                        entry.label,
                        entry.kind,
                        category_label(&entry.kind)
                    ));
                    (category.is_empty() || entry.kind == category)
                        && query.split_whitespace().all(|word| terms.contains(word))
                })
                .collect::<Vec<_>>())
        })
    });
    let load = move |()| {
        if busy.get_untracked() {
            return;
        }
        busy.set(true);
        cancel_preview();
        browser.set(None);
        selected.set(String::new());
        verification.set(String::new());
        status.set("Lecture du manifeste déployé…".into());
        wasm_bindgen_futures::spawn_local(async move {
            let result = load_browser().await;
            match result {
                Ok(value) => {
                    status.set(format!("{} références gouvernées. Charger le catalogue ne vérifie pas les fichiers ; leur contrôle est individuel.", value.report().assets.len()));
                    browser.set(Some(value));
                }
                Err(reason) => status.set(format!("Catalogue indisponible ou refusé : {reason}. Le kit doit être déployé dans forge-kit ; aucun téléchargement externe automatique.")),
            }
            busy.set(false);
        });
    };
    Effect::new(move |_| load(()));
    view! {
        <div class="gold-assets">
        <details class="gold-library" prop:open=move ||library_open.get()
            on:toggle=move |event| {
                if let Some(element)=event.current_target().and_then(|t|t.dyn_into::<web_sys::Element>().ok()) {
                    let open=element.has_attribute("open");
                    if library_open.get_untracked()!=open { library_open.set(open); }
                }
            }><summary>{move ||if map_objects.get(){"Objets de la carte"}else{"Bibliothèque d’objets"}}<span class="atelier-library-count">{move ||if map_objects.get(){"Cliquer une vignette pour sélectionner et cadrer l’objet".to_owned()}else{matches.get().and_then(Result::ok).map(|items|format!("{} objets · glisser vers la scène",items.len())).unwrap_or_default()}}</span></summary>
            <div class="gold-library-filters">
            <button aria-pressed=move ||map_objects.get().to_string() on:click=move |_|map_objects.set(true)>"Objets de la carte"</button>
            <button aria-pressed=move ||(!map_objects.get()).to_string() on:click=move |_|map_objects.set(false)>"Bibliothèque"</button>
            <label>"Rechercher"<input placeholder="Nom ou catégorie…" maxlength="256" prop:value=move || query.get()
                on:input=move |ev| query.set(event_target_value(&ev))/></label>
            <label hidden=move ||map_objects.get()>"Catégorie"<select prop:value=move || category.get() on:change=move |ev| category.set(event_target_value(&ev))>
                <option value="">"Toutes"</option><option value="tree">"Arbres / végétation"</option>
                <option value="rock">"Rochers"</option><option value="architecture">"Architecture"</option>
            </select></label>
            <button title="Relire le catalogue local" disabled=move || busy.get() on:click=move |_| load(())>"Actualiser"</button>
            </div>
            <div hidden=move ||!map_objects.get()><map_shelf::MapShelf inspected=inspected query=query/></div>
            <div hidden=move ||map_objects.get()>
            {move || matches.get().map(|result| match result {
                Ok(assets) => view! { <div class="atelier-library-results">
                    <div class="gold-asset-cards">{assets.into_iter().map(|asset| {
                        let id = asset.id.clone();
                        let drag_id = id.clone();
                        let selected_id = id.clone();
                        view! { <button draggable="false" data-catalog-asset-id=asset.id.clone() title=format!("{} · {}", asset.label, category_label(&asset.kind)) aria-pressed=move || (selected.get() == selected_id).to_string()
                            on:dragstart=move |event| event.prevent_default()
                            on:mousedown=move |event| {
                                if host_busy.get_untracked() { event.prevent_default(); return; }
                                catalog_drop::begin(&event, state, browser, selected, busy, drag_id.clone(), on_command);
                            }
                            on:click=move |event| { if !catalog_drop::consume_click(&event) { cancel_preview(); selected.set(id.clone()); verification.set(String::new()); } }>{crate::atelier_ui::asset_glyph(&asset.kind)}<span>{asset.label.clone()}</span><small>{category_label(&asset.kind).to_owned()}</small></button> }
                    }).collect_view()}</div>
                </div> }.into_any(),
                Err(reason) => view! { <p>{reason}</p> }.into_any(),
            })}
            </div>
            <Show when=move || browser.get().is_none()><output aria-live="polite">{move ||status.get()}</output></Show>
        </details>
            <nav class="atelier-object-workspaces" aria-label="Travail sur les objets">
                {[("transform","Transformer"),("manipulate","Placer / manipuler"),("catalog","Fiche")].into_iter().map(|(key,label)|view!{
                    <button aria-pressed=move ||(workspace.get()==key).to_string() on:click=move |_|workspace.set(key)>{label}</button>
                }).collect_view()}
            </nav>
            <section hidden=move ||workspace.get()!="catalog" aria-label="Informations du catalogue">
            <imported_buildings::LibraryControls/>
            <details><summary>"Fiche de l’objet du catalogue"</summary>
            {move || catalog::imported().into_iter().find(|entry| entry.id == selected.get()).map(|entry| view! {
                <section aria-label="Fiche asset" style="overflow-wrap:anywhere">
                    <h3>{entry.label}</h3><p>{entry.provenance}</p>
                    <p>{format!("Asset : {} · SHA-256 de la référence : {}", entry.reference.0, entry.reference.1)}</p>
                    <p>"Ressources importées vérifiées en mémoire. Utilisez Placer / manipuler pour préparer un aperçu sur le terrain puis Appliquer."</p>
                </section>
            })}
            {move || browser.get().and_then(|catalog| {
                let report = catalog.report();
                report.assets.iter().find(|asset| asset.id == selected.get()).map(|asset| view! {
                    <section aria-label="Fiche asset" style="overflow-wrap:anywhere">
                        <h3>{asset.id.clone()}</h3>
                        <p>{format!("{} · kit {} r{}", category_label(&asset.kind), report.asset_kit_id, report.asset_kit_revision)}</p>
                        <p>{format!("Provenance déclarée : {} · licence : {}", report.provenance.source_kind, report.provenance.license)}</p>
                        <p>{format!("GLB relatif : {} · SHA-256 attendu : {}", asset.glb.path, asset.glb.sha256)}</p>
                        <p>"Export technique déclaré valide. Le contrôle de disponibilité est présenté ci-dessous ; dimensions, pivots et collisions ne sont pas qualifiés par ce panneau."</p>
                        <p>{if report.authored_source.is_some() { "Source retouchée : revue humaine toujours en attente." } else { "Source procédurale originale déclarée." }}</p>
                        <button disabled=move || busy.get() on:click=move |_| verify::start(browser, selected, busy, verification)>"Vérifier le fichier GLB sélectionné"</button>
                        <output aria-live="polite">{move || verification.get()}</output>
                        <button disabled=move || busy.get() on:click=move |_| show_preview(browser, selected, busy)>"Aperçu 3D temporaire"</button>
                        <button on:click=move |_| cancel_preview()>"Retirer l’aperçu / revenir à la carte"</button>
                        <source_graph_particles::SourceParticleControls target=source_graph_particles::ControlTarget::Preview/>
                        <output aria-live="polite">{move || preview_output.get()}</output>
                    </section>
                })
            })}
        </details>
            <details><summary>"État du catalogue"</summary><output aria-live="polite">{move ||status.get()}</output><p>"Les pictogrammes indiquent une catégorie, pas la forme exacte de l’objet. Ouvrez l’aperçu 3D depuis sa fiche pour l’examiner."</p></details>
            </section>
            <section hidden=move ||workspace.get()!="transform" aria-label="Transformation de l’objet">
            <crate::atelier_ui::MainInspector/>
            </section>
            <section hidden=move ||workspace.get()!="manipulate" aria-label="Placement et manipulation">
            <placement_controls::PlacementControls state=state catalog=browser selected=selected inspected=inspected busy=busy host_busy=host_busy on_command=on_command/>
            </section>
        </div>
    }
}

fn show_preview(
    browser: RwSignal<Option<AssetBrowser>>,
    selected: RwSignal<String>,
    busy: RwSignal<bool>,
) {
    if busy.get_untracked() {
        return;
    }
    if !crate::authoring_viewport::can_preview_asset() {
        preview_status("Ouvrir une zone en mode édition avant de demander cet aperçu.");
        return;
    }
    let id = selected.get_untracked();
    let Some(catalog) = browser.get_untracked() else {
        return;
    };
    let reference = match catalog.reference(&id) {
        Ok(reference) => reference,
        Err(reason) => {
            preview_status(&reason.to_string());
            return;
        }
    };
    let ticket = preview::ticket();
    busy.set(true);
    preview_status("Contrôle du hash et du GLB avant chargement temporaire…");
    wasm_bindgen_futures::spawn_local(async move {
        let result = verify::check_reference(&catalog, reference.0, reference.1).await;
        if preview::is_current(ticket)
            && selected.get_untracked() == id
            && crate::authoring_viewport::can_preview_asset()
        {
            match result {
                Ok((_, bytes)) => {
                    if preview::submit(ticket, id, bytes) {
                        preview_status("GLB vérifié ; chargement dans le moteur existant…");
                    }
                }
                Err(reason) => preview_status(&format!("Aperçu refusé : {reason}")),
            }
        }
        busy.set(false);
    });
}

async fn load_browser() -> Result<AssetBrowser, String> {
    let bytes = super::governed_kit::transport::fetch("build.json", 1024 * 1024).await?;
    let report: GovernedAssetKitBuildReport =
        serde_json::from_slice(&bytes).map_err(|e| e.to_string())?;
    AssetBrowser::new(report).map_err(|e| e.to_string())
}
