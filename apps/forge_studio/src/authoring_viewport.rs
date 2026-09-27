//! Isolated authoring viewport: never labels the proof valley as the edited zone.
use bevy::prelude::*;
use forge_core::ContentHash;
use forge_project::TerrainDocument;
use leptos::prelude::*;
use std::cell::RefCell;
use std::collections::VecDeque;

thread_local! {
    static SOURCE: RefCell<Option<ContentHash>> = const { RefCell::new(None) };
    static STATUS: RefCell<Option<WriteSignal<String>>> = const { RefCell::new(None) };
    static UPDATES: RefCell<VecDeque<TerrainDocument>> = const { RefCell::new(VecDeque::new()) };
    static INVALIDATED: RefCell<bool> = const { RefCell::new(false) };
    static PLAYING: std::cell::Cell<bool> = const { std::cell::Cell::new(false) };
    static DOCUMENT: RefCell<Option<TerrainDocument>> = const { RefCell::new(None) };
}

pub(super) fn can_preview_asset() -> bool {
    can_project_sources() && !PLAYING.with(std::cell::Cell::get)
}

/// Source reconstruction must remain available while playtest waits for its caches.
/// Editing tools still use `can_preview_asset` and remain disabled during playtest.
pub(super) fn can_project_sources() -> bool {
    has_projection() && !INVALIDATED.with(|slot| *slot.borrow())
}

pub(super) fn requested() -> bool {
    web_sys::window()
        .and_then(|window| window.location().search().ok())
        .is_some_and(|query| {
            query
                .trim_start_matches('?')
                .split('&')
                .any(|part| part == "authoring=1")
        })
}

/// A session replacement must dispose the old WebGPU app, not run a second one.
pub(super) fn has_projection() -> bool {
    SOURCE.with(|slot| slot.borrow().is_some())
}

pub(crate) fn report(message: String) {
    STATUS.with(|slot| {
        if let Some(status) = *slot.borrow() {
            status.set(message);
        }
    });
}

#[component]
pub fn AuthoringWorkspace() -> impl IntoView {
    let active_tool = RwSignal::new(super::authoring_controls::AuthoringTab::Project);
    provide_context(active_tool);
    let atelier = super::atelier_ui::Link::new();
    provide_context(atelier);
    super::authoring_shortcuts::listen_workspace();
    let (status, set_status) =
        signal("Créer, ouvrir ou relire une session pour afficher son terrain.".to_owned());
    provide_context(status);
    STATUS.with(|slot| slot.replace(Some(set_status)));
    view! {
        <main class="authoring-shell" data-dual-screen=move || atelier.open.get().to_string()
            data-map-only=move || atelier.map_only.get().to_string()
            on:mousemove=move |event| super::authoring_assets::catalog_drop::motion(&event)
            on:mouseup=move |event| super::authoring_assets::catalog_drop::receive(&event)
            on:mouseleave=move |_| super::authoring_assets::catalog_drop::cancel()
            on:dragover=move |event| event.prevent_default()
            on:drop=move |event| event.prevent_default()>
            <style>{include_str!("authoring_gold.css")}</style>
            <header class="topbar">
                <h1>"OpenRPG Forge"</h1><span class="authoring-brand-caption">"Atelier de création"</span>
                <super::authoring_controls::WorkspaceActivities active=active_tool/>
                <super::asset_library_ui::LibraryLauncher/>
                <super::atelier_ui::WindowControls link=atelier/>
                <details class="forge-technical-menu"><summary>"Aide & diagnostics"</summary>
                    <div><p>"Ces pages rechargent l’atelier. Terminez vos aperçus avant de naviguer."</p>
                    <a href="?authoring=1">"Recharger la projection"</a>
                    <a href="?scene=gate0">"Scènes de preuve"</a></div>
                </details>
            </header>
            <div class="authoring-body">
                <aside class="authoring-tools" aria-label="Outils de zone">
                    <super::authoring_controls::AuthoringControls/>
                </aside>
                <section class="authoring-view" aria-label="Terrain édité">
                    <super::authoring_camera::CameraControls/>
                    <details class="gold-view-settings"><summary>"Affichage et diagnostics"</summary>
                    <super::authoring_lighting::LightingControls/>
                    <details class="authoring-viewport-info"><summary>"État technique de l’eau"</summary>
                        <super::authoring_scene::WaterStatus/>
                    </details>
                    <output class="authoring-status" aria-live="polite">{move || status.get()}</output>
                    </details>
                    <div class="authoring-canvas-wrap">
                    <super::authoring_controls::WorkspaceWelcome/>
                    <super::authoring_brush_pointer::ToolMode/>
                    <super::authoring_compass::CompassHud/>
                    <super::authoring_compass::AxisHud/>
                <canvas id="forge-authoring-canvas" tabindex="0"
                    on:mousemove=move |event| {
                        super::authoring_assets::placements::gizmo::pointer(&event, super::authoring_assets::placements::gizmo::Phase::Move);
                        super::authoring_camera::motion(&event);
                        super::authoring_brush_pointer::observe(&event, false);
                    }
                    on:mousedown=move |event| {
                        super::authoring_path_guides::press(&event);
                        super::authoring_assets::placements::gizmo::pointer(&event, super::authoring_assets::placements::gizmo::Phase::Down);
                        super::authoring_camera::press(&event);
                        super::authoring_brush_pointer::press(&event);
                    }
                    on:mouseup=move |event| {
                        super::authoring_assets::placements::gizmo::pointer(&event, super::authoring_assets::placements::gizmo::Phase::Up);
                        super::authoring_brush_pointer::release(&event);
                    }
                    on:mouseleave=move |_| suspend_tools()
                    on:blur=move |_| suspend_tools()
                    on:wheel=move |event| super::authoring_camera::wheel(&event)
                    on:contextmenu=move |event| event.prevent_default()
                    on:click=move |event| {
                        super::authoring_brush_pointer::observe(&event, true);
                        super::authoring_input::capture();
                    }
                    on:keydown=move |event| {
                        if !event.ctrl_key() && !event.alt_key() && !event.meta_key() && super::authoring_paths::drawing_key(&event.key()) {
                            event.prevent_default();
                            event.stop_propagation();
                            return;
                        }
                        super::input_capture::observe_key(&event.code());
                        if event.key() == "Escape" { cancel_tools(); }
                        if let Some(document) = web_sys::window().and_then(|window| window.document())
                            && document.pointer_lock_element().is_some()
                        {
                            if event.key() == "Escape" { document.exit_pointer_lock(); }
                            if event.code() == "Space" { event.prevent_default(); }
                        }
                    }></canvas>
                    </div>
                </section>
            </div>
        </main>
    }
}

pub(super) fn observe(
    document: Option<&TerrainDocument>,
    playtest: Option<forge_project::authoring::playtest::PlaytestIdentity>,
) {
    if !requested() {
        return;
    }
    DOCUMENT.with(|slot| slot.replace(document.cloned()));
    super::authoring_input::set_playing(playtest.is_some());
    PLAYING.with(|playing| playing.set(playtest.is_some()));
    if playtest.is_some() {
        super::authoring_assets::cancel_preview();
        super::authoring_brush_pointer::cancel();
    }
    let Some(document) = document else {
        super::authoring_assets::cancel_preview();
        if SOURCE.with(|slot| slot.borrow().is_some()) {
            INVALIDATED.with(|slot| slot.replace(true));
        }
        UPDATES.with(|slot| slot.borrow_mut().clear());
        report("Aucune session ouverte. Toute projection précédente est périmée.".into());
        return;
    };
    if INVALIDATED.with(|slot| *slot.borrow()) {
        report("Projection invalidée : recharger cet espace puis relire la session native.".into());
        return;
    }
    let hash = match document.content_hash() {
        Ok(hash) => hash,
        Err(error) => {
            report(error.to_string());
            return;
        }
    };
    if let Some(previous) = SOURCE.with(|slot| *slot.borrow()) {
        if previous != hash {
            super::authoring_assets::cancel_preview();
            super::authoring_brush_pointer::suspend();
            let accepted = UPDATES.with(|slot| {
                let mut queue = slot.borrow_mut();
                if queue.len() >= 64 {
                    return false;
                }
                queue.push_back(document.clone());
                true
            });
            if accepted {
                SOURCE.with(|slot| slot.replace(Some(hash)));
                report(
                    "Retouche en attente de projection et de vérification des collisions.".into(),
                );
            } else {
                INVALIDATED.with(|slot| slot.replace(true));
                UPDATES.with(|slot| slot.borrow_mut().clear());
                report("File de projection saturée : recharger puis relire la session.".into());
            }
        }
        return;
    }
    if document.sampled_source.is_some() {
        if playtest.is_some() {
            report("Playtest du terrain importé non encore raccordé.".into());
            return;
        }
        if let Err(error) = crate::authoring_imported_terrain::launch(document.clone()) {
            report(error);
            return;
        }
        SOURCE.with(|slot| slot.replace(Some(hash)));
        return;
    }
    if let Err(error) = forge_world::TerrainGenerationPlan::from_document(document) {
        report(format!("Terrain non projetable : {error}"));
        return;
    }
    let plugin = match viewport_plugin(
        document.clone(),
        playtest.map(|identity| identity.terrain_hash),
    ) {
        Ok(plugin) => plugin,
        Err(error) => {
            report(error);
            return;
        }
    };
    SOURCE.with(|slot| slot.replace(Some(hash)));
    report(format!(
        "Construction de la projection de {hash} ; pas encore une preuve GPU."
    ));
    launch_viewport(plugin, document.clone(), playtest);
}

pub(crate) fn retry_imported() {
    if has_projection() || PLAYING.with(std::cell::Cell::get) {
        return;
    }
    let document = DOCUMENT.with(|slot| slot.borrow().clone());
    if document
        .as_ref()
        .is_some_and(|d| d.sampled_source.is_some())
    {
        observe(document.as_ref(), None);
    }
}

pub(crate) fn latest_imported_update() -> Option<TerrainDocument> {
    UPDATES.with(|slot| {
        let mut queue = slot.borrow_mut();
        let latest = queue.pop_back();
        queue.clear();
        latest
    })
}

pub(crate) fn imported_failure(error: &str) {
    INVALIDATED.with(|slot| slot.replace(true));
    UPDATES.with(|slot| slot.borrow_mut().clear());
    super::authoring_brush_pointer::suspend();
    report(format!(
        "Projection importée refusée : {error}. Recharger la projection."
    ));
}

fn launch_viewport(
    plugin: forge_engine::VisualValleyPlugin,
    terrain: TerrainDocument,
    playtest: Option<forge_project::authoring::playtest::PlaytestIdentity>,
) {
    wasm_bindgen_futures::spawn_local(async move {
        let mut app = App::new();
        super::authoring_assets::preview::register(&mut app);
        app.add_plugins(DefaultPlugins.set(WindowPlugin {
            primary_window: Some(Window {
                title: "Forge · terrain édité".into(),
                canvas: Some("#forge-authoring-canvas".into()),
                fit_canvas_to_parent: true,
                prevent_default_event_handling: false,
                ..default()
            }),
            ..default()
        }))
        .add_plugins(plugin)
        .insert_resource(forge_engine::DiagnosticsOverlaySettings { visible: false })
        .insert_resource(forge_engine::DiagnosticsOverlayTitle(
            "OpenRPG Forge · Terrain auteur / Gate 2".into(),
        ))
        .init_resource::<forge_engine::TerrainBrushCursor>()
        .add_systems(
            PostUpdate,
            (
                super::authoring_brush_pointer::input,
                forge_engine::update_terrain_brush_cursor,
                super::authoring_brush_pointer::picked,
            )
                .chain()
                .after(bevy::transform::TransformSystems::Propagate),
        )
        .insert_resource(forge_engine::PlayerInputFocus(false))
        .add_systems(
            PreUpdate,
            super::authoring_input::update_focus.before(bevy::input::InputSystems),
        )
        .add_systems(Update, project_edits)
        .add_systems(Last, super::authoring_input::publish)
        .add_systems(Update, super::authoring_preview::draw)
        .add_systems(Update, super::authoring_camera::navigate);
        if let Err(error) = super::authoring_vegetation::install(&mut app, terrain) {
            INVALIDATED.with(|slot| slot.replace(true));
            report(format!("Initialisation végétation refusée : {error}"));
            return;
        }
        if let Err(error) = super::authoring_assets::imported_environment::install(&mut app) {
            INVALIDATED.with(|slot| slot.replace(true));
            report(format!("Initialisation environnement refusée : {error}"));
            return;
        }
        super::authoring_playtest::install(&mut app, playtest);
        super::authoring_lighting::install(&mut app);
        super::authoring_path_projection::install(&mut app);
        super::authoring_building_projection::install(&mut app);
        super::authoring_path_guides::install(&mut app);
        super::authoring_scene::install(&mut app);
        app.run();
    });
}

fn cancel_tools() {
    super::authoring_assets::placements::selection::cancel();
    super::authoring_camera::cancel();
    super::authoring_brush_pointer::cancel();
}

fn suspend_tools() {
    super::authoring_camera::cancel();
    super::authoring_brush_pointer::suspend();
}

fn viewport_plugin(
    document: TerrainDocument,
    playtest_hash: Option<ContentHash>,
) -> Result<forge_engine::VisualValleyPlugin, String> {
    if let Some(expected) = playtest_hash
        && document.content_hash().map_err(|error| error.to_string())? != expected
    {
        return Err("Snapshot de playtest incohérent : retour à l’édition requis.".into());
    }
    let launch_terrain = document.clone();
    let plugin = forge_engine::VisualValleyPlugin::new(document)
        .with_automation(forge_engine::RuntimeAutomation::default());
    if playtest_hash.is_some() {
        // Reuse the original asset-free controller, not the demonstration map.
        // Its spawn samples the current terrain; simulation owns no file session.
        let profile = forge_world::VisualValleyPlayerControllerRecipe
            .build()
            .map_err(|error| error.to_string())?;
        let start = super::authoring_playtest::start()
            .map(|start| {
                start.validate(&launch_terrain)?;
                let height = forge_world::TerrainGenerationPlan::from_document(&launch_terrain)
                    .and_then(|plan| plan.height_at(start.x_m, start.z_m))
                    .map_err(|e| e.to_string())?;
                let half_height =
                    profile.controller.capsule_radius_m + profile.controller.capsule_half_segment_m;
                Ok::<_, String>(Vec3::new(start.x_m, height + half_height + 0.02, start.z_m))
            })
            .transpose()?;
        let mut plugin = plugin.with_player_controller(profile);
        if let Some(position) = start
            && let Some(degrees) = super::authoring_playtest::start().and_then(|s| s.facing_degrees)
        {
            let heading = degrees.rem_euclid(360.0).to_radians();
            plugin = plugin.with_player_camera_target(
                position + Vec3::new(-heading.sin(), 0.0, -heading.cos()),
            );
        }
        Ok(match start {
            Some(start) => plugin.with_player_spawn_position(start),
            None => plugin,
        })
    } else {
        Ok(plugin)
    }
}

fn project_edits(
    mut pending: ResMut<forge_engine::PendingTerrainPartialUpdate>,
    state: Res<forge_engine::TerrainRuntimeState>,
    outcome: Res<forge_engine::TerrainRuntimeUpdateOutcome>,
    mut awaiting: Local<Option<u64>>,
    mut initial_reported: Local<bool>,
) {
    let state = state.into_inner();
    let outcome = outcome.into_inner();
    if INVALIDATED.with(|slot| *slot.borrow()) {
        return;
    }
    if !*initial_reported {
        *initial_reported = true;
        // Runtime state exists; this does not claim a presented GPU frame.
        report(format!(
            "Terrain chargé côté moteur : {}. Présentation GPU à vérifier.",
            state.cache().source_document_hash
        ));
    }
    if let Some(sequence) = *awaiting {
        if outcome.sequence() != Some(sequence) {
            return;
        }
        if let Some(error) = outcome.error() {
            INVALIDATED.with(|slot| slot.replace(true));
            UPDATES.with(|slot| slot.borrow_mut().clear());
            report(format!(
                "Projection refusée : {error}. Recharger la session."
            ));
            return;
        } else if outcome.report().is_some() {
            report(format!(
                "Projection CPU/Rapier acquittée : {}. Présentation GPU à vérifier.",
                state.cache().source_document_hash
            ));
        } else {
            return;
        }
        *awaiting = None;
    }
    if pending.is_pending() {
        return;
    }
    let Some(document) = UPDATES.with(|slot| slot.borrow_mut().pop_front()) else {
        return;
    };
    let request = forge_world::TerrainPartialUpdateRequest {
        base_revision: state.document().revision,
        base_document_hash: state.cache().source_document_hash,
        target_document: document,
    };
    match pending.submit_authoring(request) {
        Ok(sequence) => *awaiting = Some(sequence),
        Err(error) => {
            INVALIDATED.with(|slot| slot.replace(true));
            UPDATES.with(|slot| slot.borrow_mut().clear());
            report(format!(
                "Projection non soumise : {error}. Recharger la session."
            ));
        }
    }
}
