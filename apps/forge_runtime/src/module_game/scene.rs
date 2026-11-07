use super::input::ModuleGameOptions;
use bevy::{
    asset::AssetPlugin,
    ecs::system::SystemParam,
    input::{keyboard::KeyboardInput, mouse::MouseButtonInput},
    prelude::*,
    window::{CursorGrabMode, CursorOptions, PrimaryWindow},
};
use forge_delivery::module_runtime::LoadedModuleZone;
use forge_engine::{
    DiagnosticsOverlaySettings, PendingPlayerControlCommand, PlayerControllerSet, PlayerInputFocus,
    RuntimeAutomation, VisualValleyPlugin, VisualValleyVegetationPlugin,
    native_gameplay::{NativeGameplayUi, NativeGameplayUiSet, PreparedNativeGameplay},
};

#[cfg(feature = "qualification")]
fn fixed_shore_baseline() -> bool {
    std::env::var_os("FORGE_QUALIFY_FIXED_SHORE_BASELINE").is_some()
}

#[cfg(not(feature = "qualification"))]
const fn fixed_shore_baseline() -> bool {
    false
}

#[allow(
    clippy::too_many_lines,
    reason = "Admit and install one zone in dependency order"
)]
pub fn run(
    options: ModuleGameOptions,
    loaded: LoadedModuleZone,
    water_qualification: bool,
    source_graph_qualification: bool,
    default_display_mode: Option<forge_project::workspace::launcher::DisplayMode>,
) -> Result<(), String> {
    let environment = super::environment::prepare(&loaded)?;
    let manual = super::manual::prepare(&loaded, source_graph_qualification)?;
    let temporal_dry = super::temporal::prepare_dry(
        &loaded,
        &options.save_root,
        environment.is_some(),
        manual.has_source_graphs(),
    )?;
    let imported = super::imported::prepare(&loaded)?;
    let species = super::species::prepare(&loaded)?;
    let (terrain, vegetation) = loaded.zone.head().effective()?;
    let sampled = super::streamed::prepare_with_imported(
        &loaded,
        &terrain,
        vegetation.as_ref(),
        imported.as_ref(),
    )?;
    let probe_value = std::env::var_os("FORGE_QUALIFY_PROCEDURAL_STREAMING");
    let procedural_streaming = super::streamed::procedural_streaming(
        sampled.is_some(),
        probe_value.as_deref(),
        cfg!(feature = "qualification"),
    )?;
    let source_surfaces =
        super::environment::surfaces(environment.is_some(), imported.is_some(), sampled.as_ref())?;
    let source_terrain =
        super::source_terrain::prepare(&loaded, sampled.as_ref(), environment.is_some())?;
    let water = super::water::prepare(
        &loaded,
        sampled.as_ref(),
        environment.is_some(),
        source_terrain.is_some(),
    )?;
    let source_terrain = source_terrain
        .map(|terrain| {
            let level = water
                .as_ref()
                .map(forge_engine::source_water_host::PreparedHost::water_level_m)
                .transpose()?;
            if level.is_some() || terrain.requires_water_context() {
                terrain.with_water_level(level)
            } else {
                Ok(terrain)
            }
        })
        .transpose()?;
    let mut profile = forge_world::embedded_visual_valley_player_controller_profile()
        .map_err(|error| error.to_string())?;
    forge_engine::amphibious_player::configure_player_profile(&mut profile);
    forge_engine::player_controller::configure_source_camera_look(&mut profile);
    let player_sampled_plan = sampled.as_ref().map(|prepared| prepared.plan.clone());
    let player_procedural_plan = if player_sampled_plan.is_none() {
        Some(std::sync::Arc::new(
            forge_world::TerrainGenerationPlan::from_document(&terrain)
                .map_err(|error| error.to_string())?,
        ))
    } else {
        None
    };
    let procedural_policy = if procedural_streaming {
        let plan = player_procedural_plan
            .as_ref()
            .ok_or("Procedural terrain plan absent")?;
        Some(forge_engine::StreamedTerrainPlugin::procedural_policy(
            plan,
        )?)
    } else {
        None
    };
    let water_plan = if water.is_some() {
        Some(
            player_sampled_plan
                .as_ref()
                .ok_or("Water player requires the admitted terrain raster")?
                .clone(),
        )
    } else {
        None
    };
    let start = loaded.zone.head().corrections.start.as_deref();
    let spawn = initial_spawn(
        loaded.zone.head(),
        sampled.as_ref(),
        player_procedural_plan.as_deref(),
        &profile,
    )?;
    if water_qualification && water.is_none() {
        return Err("Water qualification requires the shared water host".into());
    }
    let water = water
        .map(|water| {
            let plan = water_plan
                .as_ref()
                .ok_or("Water shoreline requires the admitted terrain raster")?
                .clone();
            water.prepare_resident_scene(plan, [f64::from(spawn.x), f64::from(spawn.z)])
        })
        .transpose()?;
    super::output::prepare(
        &options.save_root,
        options.capture.as_deref(),
        &loaded.package_root,
    )?;
    let local_lights = super::lights::prepare(&loaded, &manual.accepted())?;
    let capture_frame = options
        .exit_after_frames
        .map_or(120, |last| last.saturating_sub(30).max(1));
    let evidence = super::evidence::prepare(
        &options,
        &loaded,
        &manual,
        capture_frame,
        species.as_ref().map(|prepared| prepared.identity.clone()),
        imported.as_ref().map(super::imported::expected),
        super::evidence::AdditionalSources {
            local_lights: local_lights.as_ref().map(super::lights::expected),
            procedural_streaming: procedural_policy
                .as_ref()
                .zip(player_procedural_plan.as_ref())
                .map(
                    |(policy, plan)| super::evidence_contract::ExpectedProceduralTerrain {
                        document_hash: plan.document_hash(),
                        maximum_cells: policy.max_resident_cells,
                        maximum_cpu_mesh_bytes: policy.max_resident_bytes,
                        dimensions: policy.dimensions,
                        maximum_staging_cpu_mesh_bytes: policy.max_staging_bytes,
                    },
                ),
        },
    )?;
    if probe_value.is_some() && evidence.is_none() {
        return Err("Procedural streaming qualification requires gameplay evidence".into());
    }
    let prepared_gameplay = prepare_gameplay(&loaded, &options.save_root, options.start_intent)?;
    let root = loaded
        .package_root
        .to_str()
        .ok_or("Non-Unicode installed module root")?;
    let mut app = module_app(root, &loaded.manifest.name, species.as_ref());
    configure_source_graph_contacts(
        &mut app,
        manual.has_source_graphs(),
        loaded.zone.head().corrections.physics.as_deref(),
    )?;
    if let Some(mode) = default_display_mode {
        app.add_plugins(forge_engine::native_display::InitialDisplayPlugin(mode));
    }
    forge_engine::source_wind::SceneWind::publish(
        app.world_mut(),
        loaded.zone.head().corrections.wind,
    )?;
    let automation = RuntimeAutomation {
        capture_path: options.capture,
        capture_frame,
        exit_after_frames: options.exit_after_frames,
        ..default()
    };
    let facing = start.and_then(|start| start.facing_degrees).map(|angle| {
        let angle = angle.to_radians();
        spawn + Vec3::new(-angle.sin(), 0.0, -angle.cos())
    });
    let streamed = sampled.is_some() || procedural_streaming;
    let terrain_host = if let Some(sampled) = sampled {
        TerrainHost::Sampled(sampled)
    } else if let Some(policy) = procedural_policy {
        let plan = player_procedural_plan
            .as_ref()
            .ok_or("Procedural terrain plan absent")?
            .clone();
        app.insert_resource(super::streamed::ProceduralTerrainIdentity {
            document_hash: plan.document_hash(),
            policy: policy.clone(),
        });
        TerrainHost::Procedural { plan, policy }
    } else {
        TerrainHost::CompleteBaseline
    };
    install_terrain(
        &mut app,
        terrain_host,
        profile,
        spawn,
        automation,
        facing,
        &loaded,
    )?;
    app.insert_resource(DiagnosticsOverlaySettings { visible: false });
    if imported.is_none() {
        install_vegetation(&mut app, &loaded, terrain, vegetation)?;
    }
    if let Some(source_surfaces) = source_surfaces {
        source_surfaces.install(&mut app)?;
    }
    if let Some(imported) = imported {
        imported.install(&mut app)?;
    }
    let species_required = species.is_some();
    if let Some(species) = species {
        species.install(&mut app, manual.accepted(), streamed);
    }
    if streamed {
        install_static_world(
            &mut app,
            manual.accepted(),
            species_required,
            manual.has_source_graphs(),
        )?;
    }
    if let Some(prepared) = prepared_gameplay {
        prepared.install(&mut app)?;
    }
    install_gameplay_visuals(&mut app, &loaded)?;
    forge_engine::runtime_buildings::install(&mut app, loaded.zone.head())?;
    super::graphics::install(
        &mut app,
        &loaded,
        &options.save_root,
        options.default_quality,
        procedural_streaming,
    )?;
    let has_water = water.is_some();
    install_source_hosts(
        &mut app,
        environment,
        source_terrain,
        water,
        !fixed_shore_baseline(),
    )?;
    super::manual::install(&mut app, manual, has_water)?;
    let locomotion = if let Some(plan) = water_plan {
        let queries = app
            .world()
            .get_resource::<forge_engine::source_surface_queries::Targets>()
            .ok_or("Water player requires the shared surface query host")?
            .queries
            .clone();
        forge_engine::amphibious_player::AmphibiousPlayerPlugin::new(queries, plan)?
    } else if let Some(plan) = player_sampled_plan {
        forge_engine::amphibious_player::AmphibiousPlayerPlugin::without_water(plan)
    } else {
        forge_engine::amphibious_player::AmphibiousPlayerPlugin::without_water_procedural(
            player_procedural_plan.ok_or("Player terrain plan absent")?,
        )
    };
    app.add_plugins((
        locomotion.with_first_person(false),
        forge_engine::amphibious_player::ladder_prompt::LadderPromptPlugin,
    ));
    if let Some(local_lights) = local_lights {
        super::lights::install(&mut app, local_lights)?;
    }
    if let Some(evidence) = &evidence {
        evidence.install(&mut app);
    }
    super::temporal::install(
        &mut app,
        &options.save_root,
        &loaded.package_root,
        temporal_dry,
    )?;
    install_player_input(&mut app);
    // All adapters own their admitted inputs; do not retain a second raw raster
    // and the module archive throughout the rendered session.
    drop(loaded);
    run_app(app, evidence, water_qualification)
}

fn install_source_hosts(
    app: &mut App,
    environment: Option<forge_engine::source_environment::host::PreparedHost>,
    terrain: Option<forge_engine::source_terrain::host::PreparedHost>,
    water: Option<forge_engine::source_water_host::PreparedResidentScene>,
    follow_shore: bool,
) -> Result<(), String> {
    if let Some(environment) = environment {
        environment.install(app)?;
    }
    if let Some(terrain) = terrain {
        terrain.install(app)?;
    }
    if let Some(water) = water {
        water.install(app, follow_shore)?;
    }
    Ok(())
}

fn run_app(
    mut app: App,
    evidence: Option<super::evidence::RuntimeEvidence>,
    water_qualification: bool,
) -> Result<(), String> {
    let coupled = if water_qualification {
        Some((
            app.world()
                .get_resource::<forge_engine::source_breaker_resident::Targets>()
                .ok_or("Qualified module breaker resources absent")?
                .clone(),
            app.world()
                .get_resource::<forge_engine::source_spray_feedback::LiveFeedback>()
                .ok_or("Qualified module spray feedback absent")?
                .clone(),
            app.world()
                .get_resource::<forge_engine::source_shore_sim::Targets>()
                .ok_or("Qualified module shore resources absent")?
                .clone(),
            app.world()
                .get_resource::<forge_engine::source_ocean::Targets>()
                .ok_or("Qualified module ocean resources absent")?
                .clone(),
        ))
    } else {
        None
    };
    // Bevy transfers the world to its runner and replaces `app` with an empty
    // shell. Keep the one-shot refusal witness outside that world until return.
    let asset_failure = app
        .world()
        .resource::<super::manual::ModuleAssetFailure>()
        .clone();
    let exit = app.run();
    if let Some((effects, feedback, shore, ocean)) = coupled {
        let statistics = feedback.statistics();
        let step = effects
            .completed_steps
            .load(std::sync::atomic::Ordering::Acquire);
        let shore_step = shore
            .completed_steps
            .load(std::sync::atomic::Ordering::Acquire);
        let ocean_step = ocean
            .completed_steps
            .load(std::sync::atomic::Ordering::Acquire);
        let head = effects.latest_head();
        if effects.error().is_some()
            || !effects.ready.load(std::sync::atomic::Ordering::Acquire)
            || step <= 120
            || statistics.accepted_samples < 2
            || statistics.latest_encoded_step != step
            || shore_step != step
            || ocean_step < step
            || ocean_step - step > 1
        {
            return Err("Qualified module coupled water did not complete GPU feedback".into());
        }
        println!(
            "FORGE_RESIDENT_BREAKER_EVIDENCE {}",
            serde_json::json!({
                "stations": effects.stations(), "window_switches": effects.switches(), "completed_step": step,
                "shore_step": shore_step, "ocean_step": ocean_step,
                "latest_gpu_head": head,
                "coverage": effects.coverage(),
                "feedback": statistics, "scope": "coupled GPU calculation and feedback; visible material admission is tracked by graphics scopes",
            })
        );
    }
    if let Some(evidence) = evidence {
        evidence.finish(&exit)?;
    }
    if exit.is_success() {
        Ok(())
    } else if let Some(reason) = asset_failure.reason() {
        Err(format!("Native module asset refused: {reason}"))
    } else {
        Err(format!("Native module runtime exited with {exit:?}"))
    }
}

fn install_static_world(
    app: &mut App,
    placements: Vec<forge_project::zone_authoring::placements::ManualPlacement>,
    species_required: bool,
    source_graphs_present: bool,
) -> Result<(), String> {
    forge_engine::runtime_static_admission::install_static_admission_with_species(
        app,
        placements,
        species_required,
    );
    app.insert_resource(static_hlod_settings(std::env::var_os(
        "FORGE_GAME_STATIC_HLOD",
    ))?);
    app.add_plugins(static_hlod_plugin(source_graphs_present));
    Ok(())
}

fn static_hlod_plugin(source_graphs_present: bool) -> forge_engine::static_hlod::StaticHlodPlugin {
    let mut materials = vec![
        // Source shaders include local-coordinate deformation and blended
        // surfaces. Keep their admitted originals; never bake those blindly.
        forge_engine::static_hlod::StaticHlodMaterial::retained_detail::<
            forge_engine::source_surface::SourceSurfaceMaterial,
        >(),
        forge_engine::static_hlod::StaticHlodMaterial::retained_detail::<
            forge_engine::source_fragment::SourceFragmentMaterial,
        >(),
        forge_engine::static_hlod::StaticHlodMaterial::pose_baked::<StandardMaterial>(),
        forge_engine::static_hlod::StaticHlodMaterial::pose_baked::<
            forge_engine::source_lighting::surfaces::ModelMaterial,
        >(),
    ];
    if source_graphs_present {
        materials.extend([
            forge_engine::static_hlod::StaticHlodMaterial::retained_detail::<
                forge_engine::source_graph::SourceGraphMaterial,
            >(),
            forge_engine::static_hlod::StaticHlodMaterial::retained_detail::<
                forge_engine::source_graph::SourceUnlitForwardMaterial,
            >(),
        ]);
    }
    forge_engine::static_hlod::StaticHlodPlugin::new(materials)
}

enum TerrainHost {
    Sampled(super::streamed::Prepared),
    Procedural {
        plan: std::sync::Arc<forge_world::TerrainGenerationPlan>,
        policy: forge_world::cell_streaming::CellStreamingPolicy,
    },
    CompleteBaseline,
}

fn install_terrain(
    app: &mut App,
    terrain: TerrainHost,
    profile: forge_project::PlayerControllerProfileDocument,
    spawn: Vec3,
    automation: RuntimeAutomation,
    facing: Option<Vec3>,
    loaded: &LoadedModuleZone,
) -> Result<(), String> {
    app.add_plugins(forge_engine::finite_perspective::FinitePerspectivePlugin);
    let session = forge_core::EntityId::deterministic(format!(
        "module-stream:{}:{}",
        loaded.manifest.module_id,
        loaded.zone.head().base.terrain.area_id
    ));
    let stream = match terrain {
        TerrainHost::Sampled(sampled) => Some(forge_engine::StreamedTerrainPlugin::new(
            sampled.plan,
            sampled.policy,
            session,
            profile.clone(),
            spawn,
        )?),
        TerrainHost::Procedural { plan, policy } => {
            Some(forge_engine::StreamedTerrainPlugin::new_procedural(
                plan,
                policy,
                session,
                profile.clone(),
                spawn,
            )?)
        }
        TerrainHost::CompleteBaseline => None,
    };
    if let Some(plugin) = stream {
        let mut plugin = plugin.with_automation(automation);
        if let Some(target) = facing {
            plugin = plugin.with_camera_target(target)?;
        }
        app.add_plugins(plugin);
    } else {
        let mut terrain_plugin = VisualValleyPlugin::new(loaded.zone.head().effective()?.0)
            .with_player_controller(profile)
            .with_player_spawn_position(spawn)
            .with_automation(automation);
        if let Some(target) = facing {
            terrain_plugin = terrain_plugin.with_player_camera_target(target);
        }
        app.add_plugins(terrain_plugin);
    }
    Ok(())
}

fn prepare_gameplay(
    loaded: &LoadedModuleZone,
    save_root: &std::path::Path,
    intent: forge_gameplay::native_storage::StartIntent,
) -> Result<Option<PreparedNativeGameplay>, String> {
    PreparedNativeGameplay::from_archive_with_intent(
        &loaded.archive,
        save_root,
        std::env::var_os("FORGE_GAME_OBSERVATIONS")
            .as_deref()
            .map(std::path::Path::new),
        intent,
    )
}

fn install_vegetation(
    app: &mut App,
    loaded: &LoadedModuleZone,
    terrain: forge_project::TerrainDocument,
    vegetation: Option<forge_project::VegetationDocument>,
) -> Result<(), String> {
    if let Some(vegetation) = vegetation {
        app.add_plugins(
            VisualValleyVegetationPlugin::new(terrain, vegetation)
                .with_authoring_head(loaded.zone.head())?,
        );
    }
    Ok(())
}

fn static_hlod_settings(
    value: Option<std::ffi::OsString>,
) -> Result<forge_engine::static_hlod::StaticHlodSettings, String> {
    let enabled = match value.as_deref() {
        None => true,
        Some(value) if value == "hierarchy" => true,
        Some(value) if value == "detail" => false,
        _ => return Err("FORGE_GAME_STATIC_HLOD must be hierarchy or detail".into()),
    };
    Ok(forge_engine::static_hlod::StaticHlodSettings { enabled })
}

fn module_app(root: &str, name: &str, species: Option<&super::species::Prepared>) -> App {
    let mut app = App::new();
    app.add_plugins(forge_engine::model_nodes::ModelNodeIdentityPlugin);
    if let Some(species) = species {
        species.register(&mut app);
    }
    let plugins = DefaultPlugins.set(AssetPlugin { file_path: root.into(), ..default() })
        .set(WindowPlugin { primary_window: Some(Window {
            title: format!("{name} | clic : jouer · G : actions · F2 : réglages · F11 : affichage · F5 : sauvegarder · F9 : reprendre · Échap : souris"),
            resolution: (1280, 800).into(), ..default()
        }), ..default() });
    // This diagnostic observes the existing Main-owned auxiliary path. Its
    // synchronization requirement is explicit and does not qualify production
    // Render-owned history or the complete source HDR chain.
    let plugins = if super::temporal::enabled() {
        plugins.disable::<bevy::render::pipelined_rendering::PipelinedRenderingPlugin>()
    } else {
        plugins
    };
    app.add_plugins(plugins);
    // Use the same source-fragment loader and material as the Studio. Its GLB
    // route refuses source graphs until their separate renderer is connected.
    app.add_plugins(forge_engine::source_fragment::SourceFragmentPlugin);
    if forge_engine::cpu_timing::enabled() {
        app.add_plugins(forge_engine::render_timing::RenderTimingPlugin);
    }
    app
}

fn initial_spawn(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    sampled: Option<&super::streamed::Prepared>,
    procedural: Option<&forge_world::TerrainGenerationPlan>,
    profile: &forge_project::PlayerControllerProfileDocument,
) -> Result<Vec3, String> {
    // The single admitted plan is also used by the player after startup.
    if sampled.is_some() == procedural.is_some() {
        return Err("Exactly one player terrain plan is required".into());
    }
    // The explicit map start takes priority for a new party. A requested save
    // is restored later by PreparedNativeGameplay::install, before startup.
    if let (None, Some(document)) = (
        head.corrections.start.as_deref(),
        head.corrections.gameplay.as_deref(),
    ) {
        let player = document
            .actor(document.player)
            .ok_or("Module player is absent")?;
        return Ok(Vec3::new(
            player.position.x,
            player.position.y,
            player.position.z,
        ));
    }
    let (x, z) = head
        .corrections
        .start
        .as_deref()
        .map_or((0.0, 0.0), |start| (start.x_m, start.z_m));
    let height = if let Some(sampled) = sampled {
        #[allow(clippy::cast_possible_truncation)]
        // Validated sampled plan bounds all heights to f32.
        let height = sampled
            .plan
            .height_at(f64::from(x), f64::from(z))
            .map_err(|error| error.to_string())? as f32;
        height
    } else {
        procedural
            .ok_or("Terrain generation plan absent")?
            .surface_height_at(x, z)
            .map_err(|error| error.to_string())?
    };
    Ok(Vec3::new(
        x,
        height + profile.controller.total_capsule_height_m() * 0.5 + 0.05,
        z,
    ))
}

fn install_player_input(app: &mut App) {
    app.insert_resource(PlayerInputFocus(false))
        .add_systems(
            Update,
            pointer_capture
                .after(NativeGameplayUiSet::Input)
                .after(super::graphics::GraphicsUiInput),
        )
        .add_systems(
            Update,
            toggle_module_window
                .after(NativeGameplayUiSet::Input)
                .after(super::graphics::GraphicsUiInput),
        )
        .add_systems(
            FixedUpdate,
            cancel_blocked_player_input.before(PlayerControllerSet::Drive),
        );
}

#[allow(
    clippy::fn_params_excessive_bools,
    reason = "Independent UI and focus barriers"
)]
fn allow_module_window_toggle(
    window_focused: bool,
    gameplay_open: bool,
    graphics_open: bool,
    conflicting_key: bool,
) -> bool {
    window_focused && !gameplay_open && !graphics_open && !conflicting_key
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn toggle_module_window(
    keys: Res<ButtonInput<KeyCode>>,
    ui: Option<Res<NativeGameplayUi>>,
    graphics: Option<Res<super::graphics::GraphicsUi>>,
    mut initial: Option<ResMut<forge_engine::native_display::InitialDisplayStatus>>,
    mut windows: Query<&mut Window, With<PrimaryWindow>>,
) {
    if !keys.just_pressed(KeyCode::F11) {
        return;
    }
    let Ok(mut window) = windows.single_mut() else {
        return;
    };
    let conflicting_key = [KeyCode::Escape, KeyCode::F2, KeyCode::KeyG]
        .iter()
        .any(|key| keys.just_pressed(*key));
    if !allow_module_window_toggle(
        window.focused,
        ui.as_ref().is_some_and(|ui| ui.is_open()),
        graphics
            .as_ref()
            .is_some_and(|graphics| graphics.open || graphics.consumed_pointer),
        conflicting_key,
    ) {
        return;
    }
    if let Some(initial) = &mut initial {
        initial.supersede_by_player();
    }
    forge_engine::native_display::toggle_window_mode(&mut window);
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy borrows system parameters"
)]
fn cancel_blocked_player_input(
    keys: Res<ButtonInput<KeyCode>>,
    ui: Option<Res<NativeGameplayUi>>,
    graphics: Option<Res<super::graphics::GraphicsUi>>,
    windows: Query<&Window, With<PrimaryWindow>>,
    mut focus: ResMut<PlayerInputFocus>,
    mut pending: ResMut<PendingPlayerControlCommand>,
) {
    // Main samples keyboard before fixed ticks and toggles G's panel in Update.
    // Cancel that frame's queued input before Drive, including the G edge.
    if !windows.single().is_ok_and(|window| window.focused)
        || keys.just_pressed(KeyCode::Escape)
        || keys.just_pressed(KeyCode::F2)
        || graphics
            .as_ref()
            .is_some_and(|graphics| graphics.open || graphics.consumed_pointer)
        || ui
            .as_ref()
            .is_some_and(|ui| ui.is_open() || keys.just_pressed(KeyCode::KeyG))
    {
        focus.0 = false;
        pending.cancel_input();
    }
}

fn install_gameplay_visuals(app: &mut App, loaded: &LoadedModuleZone) -> Result<(), String> {
    let Some(document) = loaded.zone.head().corrections.gameplay.as_deref() else {
        return Ok(());
    };
    let models = loaded
        .manifest
        .assets
        .iter()
        .map(
            |asset| forge_engine::gameplay::visuals::GameplayVisualModel {
                pin: forge_project::module_gameplay_visuals::VisualAssetPin {
                    asset_id: asset.asset_id,
                    sha256: asset.sha256,
                },
                asset_path: asset.file_path.clone(),
            },
        )
        .collect::<Vec<_>>();
    let bindings = loaded
        .manifest
        .gameplay_visuals
        .iter()
        .filter(|binding| binding.area_id == document.area_id)
        .cloned()
        .collect::<Vec<_>>();
    forge_engine::gameplay::visuals::install(app, document, &bindings, &models)
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy borrows system parameters"
)]
#[allow(
    clippy::too_many_arguments,
    reason = "The optional diagnostic only observes the existing capture transition"
)]
fn pointer_capture(
    mouse: Res<ButtonInput<MouseButton>>,
    keys: Res<ButtonInput<KeyCode>>,
    ui: Option<Res<NativeGameplayUi>>,
    graphics: Option<Res<super::graphics::GraphicsUi>>,
    mut focus: ResMut<PlayerInputFocus>,
    mut control_requested: Local<bool>,
    mut pending: ResMut<PendingPlayerControlCommand>,
    mut windows: Query<(&Window, &mut CursorOptions), With<PrimaryWindow>>,
    mut trace: PlayerInputTrace,
) {
    let focus_before_capture = focus.0;
    let ui_open = ui.as_ref().is_some_and(|ui| ui.is_open())
        || graphics
            .as_ref()
            .is_some_and(|graphics| graphics.open || graphics.consumed_pointer);
    let Ok((window, mut cursor)) = windows.single_mut() else {
        *control_requested = false;
        focus.0 = false;
        pending.cancel_input();
        trace.record(
            &keys,
            &mouse,
            InputTraceFlags {
                window_focused: None,
                ui_open,
                control_requested: false,
                focus_before_capture,
                focus_after_capture: focus.0,
            },
        );
        return;
    };
    let blocked = !window.focused
        || keys.just_pressed(KeyCode::Escape)
        || ui_open
        || keys.just_pressed(KeyCode::F2);
    let clicked = mouse.just_pressed(MouseButton::Left);
    if blocked {
        cursor.grab_mode = CursorGrabMode::None;
        cursor.visible = true;
    } else if clicked {
        cursor.grab_mode = CursorGrabMode::Locked;
        cursor.visible = false;
    }
    focus.0 = update_control_intent(&mut control_requested, blocked, clicked, &mut pending);
    trace.record(
        &keys,
        &mouse,
        InputTraceFlags {
            window_focused: Some(window.focused),
            ui_open,
            control_requested: *control_requested,
            focus_before_capture,
            focus_after_capture: focus.0,
        },
    );
}

// Opt-in local diagnostics: no text, logical keys, unrelated keys or OS hooks.
// Update still runs when gameplay has not advanced. Events are read through
// independent readers and never consumed from the actual input systems.
const INPUT_TRACE_KEYS: [KeyCode; 10] = [
    KeyCode::KeyW,
    KeyCode::KeyA,
    KeyCode::KeyS,
    KeyCode::KeyD,
    KeyCode::ShiftLeft,
    KeyCode::ShiftRight,
    KeyCode::Space,
    KeyCode::KeyV,
    KeyCode::KeyG,
    KeyCode::Escape,
];
const INPUT_TRACE_INTERVAL_S: f64 = 0.25;
const INPUT_TRACE_MAX_RECORDS: u16 = 512;

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "These independent observed input barriers are a diagnostic snapshot, not a new state machine"
)]
struct InputTraceFlags {
    window_focused: Option<bool>,
    ui_open: bool,
    control_requested: bool,
    focus_before_capture: bool,
    focus_after_capture: bool,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
struct InputTraceState {
    flags: InputTraceFlags,
    ready: bool,
    held_keys: u16,
    left_mouse: bool,
}

#[derive(Clone, Copy, Debug, Default)]
struct InputTraceEvents {
    pressed: u32,
    released: u32,
    repeated: u32,
}

#[derive(Default)]
struct InputTraceHistory {
    previous: Option<InputTraceState>,
    last_record_s: f64,
    records: u16,
    keys: [InputTraceEvents; INPUT_TRACE_KEYS.len()],
    left_mouse: InputTraceEvents,
}

impl InputTraceHistory {
    fn should_record(&mut self, state: InputTraceState, now_s: f64, event_edge: bool) -> bool {
        if self.records >= INPUT_TRACE_MAX_RECORDS {
            return false;
        }
        let changed = self.previous != Some(state);
        let held = state.held_keys != 0 || state.left_mouse;
        if !(changed || event_edge || held && now_s - self.last_record_s >= INPUT_TRACE_INTERVAL_S)
        {
            return false;
        }
        self.previous = Some(state);
        self.last_record_s = now_s;
        self.records += 1;
        true
    }
}

#[derive(SystemParam)]
struct PlayerInputTrace<'w, 's> {
    keyboard: MessageReader<'w, 's, KeyboardInput>,
    mouse: MessageReader<'w, 's, MouseButtonInput>,
    clock: Res<'w, Time<Real>>,
    frame: Res<'w, bevy::diagnostic::FrameCount>,
    ready: Res<'w, forge_engine::PlayerSimulationReady>,
    player: Res<'w, forge_engine::PlayerRuntimeSnapshot>,
    history: Local<'s, InputTraceHistory>,
}

impl PlayerInputTrace<'_, '_> {
    fn record(
        &mut self,
        keys: &ButtonInput<KeyCode>,
        mouse: &ButtonInput<MouseButton>,
        flags: InputTraceFlags,
    ) {
        if !tracing::enabled!(target: "forge::input", tracing::Level::DEBUG)
            || self.history.records >= INPUT_TRACE_MAX_RECORDS
        {
            self.keyboard.clear();
            self.mouse.clear();
            return;
        }
        let mut event_edge = false;
        for event in self.keyboard.read() {
            let Some(index) = INPUT_TRACE_KEYS
                .iter()
                .position(|key| *key == event.key_code)
            else {
                continue;
            };
            let counts = &mut self.history.keys[index];
            if event.repeat {
                counts.repeated = counts.repeated.saturating_add(1);
            } else if event.state.is_pressed() {
                counts.pressed = counts.pressed.saturating_add(1);
                event_edge = true;
            } else {
                counts.released = counts.released.saturating_add(1);
                event_edge = true;
            }
        }
        for event in self
            .mouse
            .read()
            .filter(|event| event.button == MouseButton::Left)
        {
            let counts = &mut self.history.left_mouse;
            if event.state.is_pressed() {
                counts.pressed = counts.pressed.saturating_add(1);
            } else {
                counts.released = counts.released.saturating_add(1);
            }
            event_edge = true;
        }
        let state = InputTraceState {
            flags,
            ready: self.ready.0,
            held_keys: INPUT_TRACE_KEYS
                .iter()
                .enumerate()
                .fold(0, |mask, (index, key)| {
                    mask | (u16::from(keys.pressed(*key)) << index)
                }),
            left_mouse: mouse.pressed(MouseButton::Left),
        };
        let elapsed_s = self.clock.elapsed_secs_f64();
        if self.history.should_record(state, elapsed_s, event_edge) {
            tracing::debug!(target: "forge::input", frame = self.frame.0, tick = self.player.fixed_tick,
                elapsed_s, input = ?state, key_order = ?INPUT_TRACE_KEYS,
                keyboard_events = ?self.history.keys, left_mouse_events = ?self.history.left_mouse,
                position = ?self.player.player_position, desired_translation = ?self.player.desired_translation,
                effective_translation = ?self.player.effective_translation, grounded = self.player.grounded,
                collisions = self.player.collisions, last_applied_sequence = ?self.player.last_applied_sequence,
                diagnostic_limit_reached = self.history.records == INPUT_TRACE_MAX_RECORDS,
                "native player input observation");
            self.history.keys.fill(InputTraceEvents::default());
            self.history.left_mouse = InputTraceEvents::default();
        }
    }
}

fn update_control_intent(
    control_requested: &mut bool,
    blocked: bool,
    clicked: bool,
    pending: &mut PendingPlayerControlCommand,
) -> bool {
    if blocked {
        *control_requested = false;
        pending.cancel_input();
    } else if clicked {
        *control_requested = true;
    }
    // Bevy restores grab_mode after an OS refusal. Keyboard ownership follows
    // the player's click until explicitly released, even when locking is denied.
    *control_requested
}

fn configure_source_graph_contacts(
    app: &mut App,
    source_graphs_present: bool,
    settings: Option<&forge_project::zone_authoring::physics::PhysicsSceneSettings>,
) -> Result<(), String> {
    if source_graphs_present && let Some(settings) = settings {
        settings.validate()?;
        app.insert_resource(forge_engine::source_graph_placement::SceneContactSettings(
            settings.clone(),
        ));
    }
    Ok(())
}

#[cfg(test)]
mod tests;

#[cfg(test)]
mod source_graph_contact_wiring_tests {
    use super::*;
    use forge_engine::source_graph_placement::SceneContactSettings;
    use forge_project::zone_authoring::physics::PhysicsSceneSettings;

    fn settings() -> PhysicsSceneSettings {
        PhysicsSceneSettings {
            schema_version: 1,
            layer_matrix: [u32::MAX; 32],
            contact_skin_m: 0.01,
            evidence: "explicit fixture settings".into(),
        }
    }

    #[test]
    fn contacts_are_copied_only_when_the_common_source_host_is_present() {
        let mut app = App::new();
        let settings = settings();
        configure_source_graph_contacts(&mut app, false, Some(&settings)).unwrap();
        assert!(!app.world().contains_resource::<SceneContactSettings>());
        configure_source_graph_contacts(&mut app, true, None).unwrap();
        assert!(!app.world().contains_resource::<SceneContactSettings>());
        configure_source_graph_contacts(&mut app, true, Some(&settings)).unwrap();
        assert_eq!(app.world().resource::<SceneContactSettings>().0, settings);
    }

    #[test]
    fn invalid_contact_settings_are_not_published() {
        let mut app = App::new();
        let mut invalid = settings();
        invalid.evidence.clear();
        assert!(configure_source_graph_contacts(&mut app, true, Some(&invalid)).is_err());
        assert!(!app.world().contains_resource::<SceneContactSettings>());
    }
}
