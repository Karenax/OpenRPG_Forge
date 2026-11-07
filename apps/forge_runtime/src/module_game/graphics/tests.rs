//! CPU contracts only. Real rendering and persistence replay are qualified separately.
use super::*;
use forge_engine::{
    PendingPlayerControlCommand, PlayerInputFocus, native_gameplay::NativeGraphicsCommand,
};
use std::{
    path::PathBuf,
    sync::atomic::{AtomicU64, Ordering},
};

static NEXT: AtomicU64 = AtomicU64::new(0);

#[test]
fn profile_resource_identity_tracks_requests_and_settings_not_visibility() {
    let directory = Directory::new();
    let mut state = state(&directory);
    let baseline = runtime::application_identity(&state).unwrap();
    state.generation += 1;
    assert_eq!(runtime::application_identity(&state).unwrap(), baseline);
    state.request_sequence += 1;
    assert_ne!(runtime::application_identity(&state).unwrap(), baseline);
    state.request_sequence -= 1;
    state.requested_custom = Some(forge_engine::source_graphics::Settings {
        bloom: 0.1,
        ..default()
    });
    assert_ne!(runtime::application_identity(&state).unwrap(), baseline);
    state.requested_custom = None;
    state.source = ContentHash::from_bytes([8; 32]);
    assert_ne!(runtime::application_identity(&state).unwrap(), baseline);
}

fn failed_stream_waiting_graphics(directory: &Directory, streamed: bool) -> App {
    use forge_engine::cell_streaming::CellStreamingStats;
    let mut state = state(directory);
    if streamed {
        let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
        state.policy = Policy::with_procedural_streaming(&terrain, true).unwrap();
    }
    state.dispatched = Some(state.requested);
    state.waiting_since = Some(0.0);
    state.saved = Some(VisualQualityTier::Low);
    state.preference_error = Some("existing preference failure".into());
    let mut app = App::new();
    // Model the processed-frame clock provided by the real runtime plugins.
    app.init_resource::<bevy::diagnostic::FrameCount>()
        .insert_resource(state)
        .insert_resource(GraphicsUi::new(VisualQualityTier::High, false))
        .insert_resource(bevy::time::Time::<bevy::time::Real>::default())
        .insert_resource(CellStreamingStats {
            last_error: Some("terrain failure fixture".into()),
            ..default()
        })
        .insert_resource(forge_engine::source_terrain::host::Status {
            pending: true,
            ..default()
        });
    app
}

fn advance_graphics_clock(app: &mut App, seconds: u64) {
    app.world_mut()
        .resource_mut::<bevy::time::Time<bevy::time::Real>>()
        .advance_by(std::time::Duration::from_secs(seconds));
}

#[test]
fn failed_terrain_does_not_consume_graphics_deadline_but_timeout_resumes_after_repair() {
    use forge_engine::cell_streaming::CellStreamingStats;
    let directory = Directory::new();
    let mut app = failed_stream_waiting_graphics(&directory, true);
    advance_graphics_clock(&mut app, 121);
    runtime::observe(app.world_mut());
    {
        let state = app.world().resource::<State>();
        assert!(state.error.is_none());
        assert!(!state.ready);
        assert_eq!(
            state.waiting_since.map(f64::to_bits),
            Some(121_f64.to_bits())
        );
        assert_eq!(state.saved, Some(VisualQualityTier::Low));
        assert_eq!(
            state.preference_error.as_deref(),
            Some("existing preference failure")
        );
        assert!(!state.save_requested);
    }
    advance_graphics_clock(&mut app, 180);
    runtime::observe(app.world_mut());
    assert!(app.world().resource::<State>().error.is_none());
    assert_eq!(
        app.world()
            .resource::<State>()
            .waiting_since
            .map(f64::to_bits),
        Some(301_f64.to_bits())
    );
    app.world_mut()
        .resource_mut::<CellStreamingStats>()
        .last_error = None;
    runtime::observe(app.world_mut());
    assert!(app.world().resource::<State>().error.is_none());
    advance_graphics_clock(&mut app, 121);
    runtime::observe(app.world_mut());
    let state = app.world().resource::<State>();
    assert_eq!(
        state.error.as_deref(),
        Some("Graphics application did not become ready within 120 seconds")
    );
    assert!(!state.ready);
    assert_eq!(state.saved, Some(VisualQualityTier::Low));
    assert_eq!(
        state.preference_error.as_deref(),
        Some("existing preference failure")
    );
}

#[test]
fn terrain_failure_does_not_hide_or_replace_independent_graphics_errors() {
    let directory = Directory::new();
    let mut app = failed_stream_waiting_graphics(&directory, true);
    app.world_mut().spawn(Mesh3d(Handle::default()));
    advance_graphics_clock(&mut app, 121);
    runtime::observe(app.world_mut());
    let error = app
        .world()
        .resource::<State>()
        .error
        .clone()
        .expect("graphics ownership must still be checked");
    assert!(
        error.contains("unsupported or ambiguous graphics material"),
        "{error}"
    );
    advance_graphics_clock(&mut app, 180);
    runtime::observe(app.world_mut());
    assert_eq!(
        app.world().resource::<State>().error.as_deref(),
        Some(error.as_str())
    );
    assert_eq!(
        app.world().resource::<State>().preference_error.as_deref(),
        Some("existing preference failure")
    );
}

#[test]
fn unrelated_legacy_graphics_timeout_is_not_suspended_by_stream_error() {
    let directory = Directory::new();
    let mut app = failed_stream_waiting_graphics(&directory, false);
    assert!(
        app.world()
            .resource::<State>()
            .policy
            .preset(VisualQualityTier::High)
            .raster_strides
            .is_none()
    );
    advance_graphics_clock(&mut app, 121);
    runtime::observe(app.world_mut());
    assert_eq!(
        app.world().resource::<State>().error.as_deref(),
        Some("Graphics application did not become ready within 120 seconds")
    );
}

#[test]
fn source_terrain_material_and_cpu_status_do_not_forge_renderer_ownership() {
    let mut world = World::new();
    world.insert_resource(forge_engine::source_terrain::host::Status::default());
    world.spawn((
        Mesh3d(Handle::default()),
        MeshMaterial3d::<forge_engine::source_terrain::lod::LodMaterial>(Handle::default()),
    ));
    assert!(runtime::coverage(&mut world).is_err());
}

#[test]
fn graphics_coverage_refuses_unknown_ambiguous_and_unowned_environment_meshes() {
    let mut world = World::new();
    let mesh = world.spawn(Mesh3d(Handle::default())).id();
    assert!(runtime::coverage(&mut world).is_err());
    world
        .entity_mut(mesh)
        .insert(MeshMaterial3d::<StandardMaterial>(Handle::default()));
    assert!(runtime::coverage(&mut world).is_ok());
    world
        .entity_mut(mesh)
        .insert(MeshMaterial3d::<forge_engine::VisualValleyTerrainMaterial>(
            Handle::default(),
        ));
    assert!(runtime::coverage(&mut world).is_err());
    world.despawn(mesh);
    let sky = world
        .spawn((
            Mesh3d(Handle::default()),
            MeshMaterial3d::<forge_engine::source_sky::SkyMaterial>(Handle::default()),
        ))
        .id();
    assert!(runtime::coverage(&mut world).is_err());
    world.despawn(sky);
    world.spawn((
        Mesh3d(Handle::default()),
        MeshMaterial3d::<forge_engine::source_surface::SourceSurfaceMaterial>(Handle::default()),
    ));
    assert!(
        runtime::coverage(&mut world).is_err(),
        "An arbitrary source-material entity cannot claim module object ownership"
    );
}

#[test]
fn shared_source_fragment_has_its_own_unambiguous_graphics_route() {
    let mut world = World::new();
    let draw = world
        .spawn((
            Mesh3d(Handle::default()),
            MeshMaterial3d::<forge_engine::source_fragment::SourceFragmentMaterial>(
                Handle::default(),
            ),
        ))
        .id();
    assert!(runtime::coverage(&mut world).is_ok());
    world
        .entity_mut(draw)
        .insert(MeshMaterial3d::<StandardMaterial>(Handle::default()));
    assert!(runtime::coverage(&mut world).is_err());
}

#[test]
fn governed_source_lighting_requires_its_live_owner_and_one_material() {
    let mut world = World::new();
    let draw = world
        .spawn((
            Mesh3d(Handle::default()),
            MeshMaterial3d::<forge_engine::source_lighting::surfaces::VegetationMaterial>(
                Handle::default(),
            ),
        ))
        .id();
    assert!(runtime::coverage(&mut world).is_err());
    world
        .entity_mut(draw)
        .insert(MeshMaterial3d::<forge_engine::VisualValleyVegetationMaterial>(Handle::default()));
    assert!(runtime::coverage(&mut world).is_err());
}

#[test]
fn fish_material_without_its_live_scene_owner_is_refused() {
    let mut world = World::new();
    let draw = world
        .spawn((
            Mesh3d(Handle::default()),
            MeshMaterial3d::<forge_engine::source_fish::material::FishMaterial>(Handle::default()),
        ))
        .id();
    assert!(runtime::coverage(&mut world).is_err());
    world
        .entity_mut(draw)
        .insert(MeshMaterial3d::<StandardMaterial>(Handle::default()));
    assert!(runtime::coverage(&mut world).is_err());
}

#[test]
fn source_graph_materials_without_a_scene_owner_are_refused() {
    let mut world = World::new();
    let graph = world
        .spawn((
            Mesh3d(Handle::default()),
            MeshMaterial3d::<forge_engine::source_graph::SourceGraphMaterial>(Handle::default()),
        ))
        .id();
    assert!(runtime::coverage(&mut world).is_err());
    world
        .entity_mut(graph)
        .insert(MeshMaterial3d::<StandardMaterial>(Handle::default()));
    assert!(runtime::coverage(&mut world).is_err());
    world.init_resource::<forge_engine::source_graph_runtime::SourceSceneResidents>();
    assert!(runtime::coverage(&mut world).is_err());
    world.despawn(graph);
    world.spawn((
        Mesh3d(Handle::default()),
        MeshMaterial3d::<forge_engine::source_graph::SourceUnlitForwardMaterial>(Handle::default()),
    ));
    assert!(runtime::coverage(&mut world).is_err());
}

#[test]
fn water_material_without_its_real_host_is_refused() {
    let mut world = World::new();
    world.insert_resource(forge_engine::source_water_host::Status {
        document: Some(forge_core::bytes_sha256(b"terrain")),
        staged: true,
        error: None,
    });
    world.spawn((
        Mesh3d(Handle::default()),
        MeshMaterial3d::<forge_engine::source_water_optics::OpticalMaterial>(Handle::default()),
    ));
    assert!(runtime::coverage(&mut world).is_err());
}

#[test]
fn unstaged_water_cannot_release_the_graphics_gate() {
    let directory = Directory::new();
    let mut state = state(&directory);
    state.water = true;
    let mut world = World::new();
    world.insert_resource(forge_engine::source_water_host::Status {
        document: Some(state.terrain),
        staged: false,
        error: None,
    });
    assert!(!runtime::cpu_ready(&mut world, &state).unwrap());
    world
        .resource_mut::<forge_engine::source_water_host::Status>()
        .staged = true;
    assert!(
        !runtime::cpu_ready(&mut world, &state).unwrap(),
        "CPU status cannot replace a missing owned water projection"
    );
}

struct Directory(PathBuf);
impl Directory {
    fn new() -> Self {
        let path = std::env::temp_dir().join(format!(
            "forge-graphics-test-{}-{}",
            std::process::id(),
            NEXT.fetch_add(1, Ordering::Relaxed)
        ));
        std::fs::create_dir(&path).unwrap();
        Self(path)
    }
}
impl Drop for Directory {
    fn drop(&mut self) {
        assert!(self.0.starts_with(std::env::temp_dir()));
        assert!(
            self.0
                .file_name()
                .unwrap()
                .to_string_lossy()
                .starts_with("forge-graphics-test-")
        );
        std::fs::remove_dir_all(&self.0).unwrap();
    }
}

#[test]
fn authored_quality_is_used_only_before_the_player_saves_a_preference() {
    let directory = Directory::new();
    let store = store::Store::new(&directory.0).unwrap();
    assert_eq!(
        initial_quality(store.load().unwrap(), Some(VisualQualityTier::Low)),
        VisualQualityTier::Low
    );
    store.save(VisualQualityTier::Ultra).unwrap();
    assert_eq!(
        initial_quality(store.load().unwrap(), Some(VisualQualityTier::Low)),
        VisualQualityTier::Ultra
    );
    assert_eq!(initial_quality(None, None), VisualQualityTier::High);
}

#[test]
fn procedural_streaming_qualification_uses_common_terrain_quality_without_changing_legacy() {
    let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    let legacy = Policy::new(&terrain).unwrap();
    let streamed = Policy::with_procedural_streaming(&terrain, true).unwrap();
    for tier in policy::TIERS {
        assert!(legacy.preset(tier).raster_strides.is_none());
        assert!(streamed.preset(tier).raster_strides.is_some());
    }
    assert_ne!(legacy.identity, streamed.identity);
}

#[test]
fn preferences_roundtrip_every_tier_without_touching_game_saves() {
    let directory = Directory::new();
    let saved_game = directory.0.join("game-save.json");
    std::fs::write(&saved_game, b"preserve-gameplay").unwrap();
    let store = store::Store::new(&directory.0).unwrap();
    assert_eq!(store.load().unwrap(), None);
    for tier in policy::TIERS {
        store.save(tier).unwrap();
        assert_eq!(
            store::Store::new(&directory.0).unwrap().load().unwrap(),
            Some(tier)
        );
        assert_eq!(std::fs::read(&saved_game).unwrap(), b"preserve-gameplay");
        assert_eq!(std::fs::read_dir(&directory.0).unwrap().count(), 2);
    }
}

#[test]
fn invalid_preferences_are_bounded_and_preserved_until_explicit_save() {
    let directory = Directory::new();
    let path = directory.0.join("graphics.json");
    let store = store::Store::new(&directory.0).unwrap();
    for invalid in [
        b"bad-json".to_vec(),
        vec![b' '; 4097],
        br#"{"schema":"forge.module-graphics-preferences","version":2,"tier":"high"}"#.to_vec(),
        br#"{"schema":"forge.module-graphics-preferences","version":1,"tier":"extreme"}"#.to_vec(),
    ] {
        std::fs::write(&path, &invalid).unwrap();
        assert!(store.load().is_err());
        assert_eq!(std::fs::read(&path).unwrap(), invalid);
    }
    store.save(VisualQualityTier::Medium).unwrap();
    assert_eq!(store.load().unwrap(), Some(VisualQualityTier::Medium));
}

#[test]
fn failed_preference_replace_does_not_leak_staging_or_remove_target() {
    let directory = Directory::new();
    let path = directory.0.join("graphics.json");
    std::fs::create_dir(&path).unwrap();
    std::fs::write(path.join("preserve"), b"owned-test-fixture").unwrap();
    let store = store::Store::new(&directory.0).unwrap();
    assert!(store.load().is_err());
    for _ in 0..20 {
        assert!(store.save(VisualQualityTier::Ultra).is_err());
    }
    assert_eq!(std::fs::read_dir(&directory.0).unwrap().count(), 1);
    assert_eq!(
        std::fs::read(path.join("preserve")).unwrap(),
        b"owned-test-fixture"
    );
}

fn state(directory: &Directory) -> State {
    let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    State {
        policy: Policy::new(&terrain).unwrap(),
        store: store::Store::new(&directory.0).unwrap(),
        requested: VisualQualityTier::High,
        requested_custom: None,
        applied: None,
        saved: None,
        saved_custom: None,
        error: None,
        preference_error: None,
        dispatched: None,
        save_requested: false,
        source: ContentHash::from_bytes([7; 32]),
        terrain: terrain.content_hash().unwrap(),
        water: false,
        generation: 0,
        request_sequence: 0,
        applied_frame: 0,
        simulation_admission: None,
        inventory: None,
        active_scopes: vec![],
        scope_revisions: std::collections::BTreeMap::new(),
        scope_owners: std::collections::BTreeMap::new(),
        scope_leases: std::collections::BTreeMap::new(),
        publications: std::collections::BTreeMap::new(),
        waiting_since: None,
        ready: false,
    }
}

#[test]
fn keyboard_selection_apply_and_escape_cancel_player_input() {
    let directory = Directory::new();
    let mut app = App::new();
    app.insert_resource(state(&directory))
        .insert_resource(GraphicsUi::new(VisualQualityTier::High, false))
        .insert_resource(PlayerInputFocus(true))
        .init_resource::<PendingPlayerControlCommand>()
        .init_resource::<ButtonInput<KeyCode>>()
        .add_message::<NativeGraphicsCommand>()
        .add_systems(Update, ui::input);
    press(&mut app, &[KeyCode::F2]);
    assert!(app.world().resource::<GraphicsUi>().open);
    assert!(!app.world().resource::<PlayerInputFocus>().0);
    // High -> Medium -> Low, select it, then navigate backwards to Apply.
    press(&mut app, &[KeyCode::ArrowUp]);
    press(&mut app, &[KeyCode::ArrowUp]);
    press(&mut app, &[KeyCode::Enter]);
    press(&mut app, &[KeyCode::ShiftLeft, KeyCode::Tab]);
    press(&mut app, &[KeyCode::ShiftLeft, KeyCode::Tab]);
    press(&mut app, &[KeyCode::ShiftLeft, KeyCode::Tab]);
    press(&mut app, &[KeyCode::Space]);
    let requests = app
        .world_mut()
        .resource_mut::<Messages<NativeGraphicsCommand>>()
        .drain()
        .collect::<Vec<_>>();
    assert_eq!(requests.len(), 1);
    assert_eq!(requests[0].0, VisualQualityTier::Low);
    // Escape wins over a simultaneous Apply, including keyboard focus on Apply.
    press(&mut app, &[KeyCode::Escape, KeyCode::Enter]);
    assert!(!app.world().resource::<GraphicsUi>().open);
    assert!(app.world().resource::<GraphicsUi>().consumed_pointer);
    assert_eq!(
        app.world_mut()
            .resource_mut::<Messages<NativeGraphicsCommand>>()
            .drain()
            .count(),
        0
    );
    assert!(!app.world().resource::<PlayerInputFocus>().0);
}

#[test]
fn graphics_advanced_preferences_preserve_legacy_and_reject_incomplete_data() {
    use forge_engine::source_graphics::Settings;
    let directory = Directory::new();
    let store = store::Store::new(&directory.0).unwrap();
    store.save(VisualQualityTier::High).unwrap();
    let legacy = directory.0.join("graphics.json");
    let before = std::fs::read(&legacy).unwrap();
    let settings = Settings {
        bloom: 0.17,
        ..Settings::default()
    };
    store
        .save_preferences(store::Preferences {
            tier: VisualQualityTier::High,
            settings: Some(settings),
        })
        .unwrap();
    assert_eq!(std::fs::read(&legacy).unwrap(), before);
    assert_eq!(
        store.load_preferences().unwrap(),
        Some(store::Preferences {
            tier: VisualQualityTier::High,
            settings: Some(settings),
        })
    );
    let custom = directory.0.join("graphics-custom.json");
    let retained = std::fs::read(&custom).unwrap();
    // Simulate an older executable changing its own existing preference.
    store.save(VisualQualityTier::Low).unwrap();
    assert!(store.load_preferences().is_err());
    assert_eq!(std::fs::read(&custom).unwrap(), retained);
    // An explicit profile application resolves the conflict without touching v1.
    store
        .save_preferences(store::Preferences {
            tier: VisualQualityTier::Low,
            settings: None,
        })
        .unwrap();
    assert_eq!(
        store.load_preferences().unwrap().unwrap().tier,
        VisualQualityTier::Low
    );
    let mut malformed: serde_json::Value =
        serde_json::from_slice(&std::fs::read(&custom).unwrap()).unwrap();
    malformed["settings"] = serde_json::to_value(settings).unwrap();
    malformed["settings"]
        .as_object_mut()
        .unwrap()
        .remove("haze");
    std::fs::write(&custom, serde_json::to_vec(&malformed).unwrap()).unwrap();
    assert!(store.load_preferences().is_err());
}

#[test]
fn graphics_advanced_profile_resolves_bloom_and_antialiasing_coherently() {
    let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    let policy = Policy::new(&terrain).unwrap();
    for tier in policy::TIERS {
        let baseline = policy.resolve(tier, None).unwrap();
        assert_eq!(baseline.preset, policy.preset(tier));
    }
    let mut low = policy
        .resolve(VisualQualityTier::Low, None)
        .unwrap()
        .settings;
    low.bloom = 0.1;
    low.temporal_aa = 1.0;
    let low = policy.resolve(VisualQualityTier::Low, Some(low)).unwrap();
    assert!(low.preset.post_process.bloom_enabled);
    assert!(low.preset.temporal_aa);
    assert!(!low.fxaa);
    let mut high = policy
        .resolve(VisualQualityTier::High, None)
        .unwrap()
        .settings;
    high.temporal_aa = 0.0;
    let high = policy.resolve(VisualQualityTier::High, Some(high)).unwrap();
    assert!(!high.preset.shadows.contact_shadows_enabled);
    assert!(high.preset.contact_shadows.is_none());
}

#[test]
fn graphics_advanced_keyboard_edits_the_shared_controls_and_submits_them() {
    use forge_engine::source_graphics::Settings;
    let directory = Directory::new();
    let mut app = App::new();
    app.insert_resource(state(&directory))
        .insert_resource(GraphicsUi::new(VisualQualityTier::High, false))
        .insert_resource(PlayerInputFocus(true))
        .init_resource::<PendingPlayerControlCommand>()
        .init_resource::<ButtonInput<KeyCode>>()
        .add_message::<NativeGraphicsCommand>()
        .add_message::<CustomSettingsCommand>()
        .add_systems(Update, (ui::input, ui::update).chain());
    app.world_mut().spawn((ui::Status, Text::new("")));
    press(&mut app, &[KeyCode::F2]);
    app.world_mut().resource_mut::<GraphicsUi>().focus = 7;
    press(&mut app, &[KeyCode::Enter]);
    app.world_mut().resource_mut::<GraphicsUi>().focus = 10;
    press(&mut app, &[KeyCode::Enter]); // Decrease automatic exposure.
    app.world_mut().resource_mut::<GraphicsUi>().focus = 9;
    press(&mut app, &[KeyCode::Enter]); // Next page.
    app.world_mut().resource_mut::<GraphicsUi>().focus = 10;
    press(&mut app, &[KeyCode::Enter]); // Decrease exposure.
    app.world_mut().resource_mut::<GraphicsUi>().focus = 4;
    press(&mut app, &[KeyCode::Enter]);
    let requests = app
        .world_mut()
        .resource_mut::<Messages<CustomSettingsCommand>>()
        .drain()
        .collect::<Vec<_>>();
    assert_eq!(requests.len(), 1);
    assert_eq!(requests[0].tier, VisualQualityTier::High);
    assert_eq!(
        requests[0].settings.auto_exposure.to_bits(),
        0.0_f32.to_bits()
    );
    assert!(requests[0].settings.exposure_ev < Settings::default().exposure_ev);
}

fn press(app: &mut App, keys: &[KeyCode]) {
    let mut input = app.world_mut().resource_mut::<ButtonInput<KeyCode>>();
    input.reset_all();
    for key in keys {
        input.press(*key);
    }
    app.update();
}

#[test]
fn preference_errors_remain_visible_in_the_actual_panel_status() {
    let directory = Directory::new();
    let mut state = state(&directory);
    state.ready = true;
    state.preference_error = Some("controlled filesystem refusal".into());
    let mut app = App::new();
    app.insert_resource(state)
        .insert_resource(GraphicsUi::new(VisualQualityTier::High, true))
        .add_systems(Update, ui::update);
    let status = app.world_mut().spawn((ui::Status, Text::new(""))).id();
    app.update();
    assert!(
        app.world()
            .get::<Text>(status)
            .unwrap()
            .0
            .contains("non enregistrés")
    );
    app.world_mut().resource_mut::<State>().preference_error = None;
    app.world_mut().resource_mut::<State>().saved = Some(VisualQualityTier::High);
    app.update();
    assert!(
        app.world()
            .get::<Text>(status)
            .unwrap()
            .0
            .contains("appliqué et enregistré")
    );
}

#[test]
fn unavailable_display_opens_the_panel_without_hiding_preference_errors() {
    use forge_engine::native_display::{DisplayFallback, DisplayOutcome, InitialDisplayStatus};
    use forge_project::workspace::launcher::DisplayMode;

    let directory = Directory::new();
    let legacy = br#"{"schema":"forge.module-graphics-preferences","version":1,"tier":"high"}"#;
    let preferences = directory.0.join("graphics.json");
    std::fs::write(&preferences, legacy).unwrap();
    assert_eq!(
        store::Store::new(&directory.0).unwrap().load().unwrap(),
        Some(VisualQualityTier::High)
    );
    let mut state = state(&directory);
    state.saved = Some(VisualQualityTier::High);
    state.preference_error = Some("controlled filesystem refusal".into());
    let mut display = InitialDisplayStatus::new(DisplayMode::Fullscreen);
    display.outcome = DisplayOutcome::Fallback(DisplayFallback::MonitorUnavailable);
    let mut app = App::new();
    app.insert_resource(state)
        .insert_resource(display)
        .insert_resource(GraphicsUi::new(VisualQualityTier::High, false))
        .add_systems(Update, ui::update);
    let status = app.world_mut().spawn((ui::Status, Text::new(""))).id();
    app.update();
    let text = &app.world().get::<Text>(status).unwrap().0;
    assert!(text.contains("non enregistrés"));
    assert!(text.contains("Écran principal indisponible"));
    assert!(app.world().resource::<GraphicsUi>().open);
    app.world_mut().resource_mut::<GraphicsUi>().open = false;
    app.update();
    assert!(!app.world().resource::<GraphicsUi>().open);
    assert_eq!(std::fs::read(preferences).unwrap(), legacy.to_vec());
}

#[test]
fn retry_has_a_new_identity_and_clears_errors_without_claiming_readiness() {
    let directory = Directory::new();
    let mut state = state(&directory);
    state.preference_error = Some("refused".into());
    for tier in policy::TIERS {
        let sequence = state.request_sequence;
        state.request(tier);
        assert_eq!(state.request_sequence, sequence + 1);
        assert!(!state.ready && state.save_requested && state.dispatched.is_none());
        assert!(state.preference_error.is_none() && state.error.is_none());
    }
    state.request_sequence = u64::MAX;
    state.request(VisualQualityTier::Low);
    assert!(state.error.is_some() && !state.ready);
}

#[test]
fn graphics_wait_between_fixed_steps_discards_old_input_and_keeps_a_new_command() {
    use forge_engine::{
        PlayerControlCommand, PlayerSimulationReady,
        cell_streaming::{PlayerCellStreamingSet, PlayerCellStreamingStatus},
        runtime_static_admission::install_static_admission,
    };

    let directory = Directory::new();
    let terrain = forge_project::authoring::new_terrain("input-test", "area", "Area").unwrap();
    let zone =
        forge_project::zone_authoring::session::ZoneEditSession::from_initial_terrain(terrain)
            .unwrap();
    let mut app = App::new();
    app.add_plugins(MinimalPlugins)
        .init_resource::<Assets<Mesh>>()
        .init_resource::<Assets<StandardMaterial>>()
        .init_resource::<PendingPlayerControlCommand>()
        .insert_resource(PlayerSimulationReady(true))
        .insert_resource(PlayerCellStreamingStatus {
            ready: true,
            ..default()
        });
    forge_engine::runtime_buildings::install(&mut app, zone.head()).unwrap();
    install_static_admission(&mut app, vec![]);
    app.add_systems(
        FixedUpdate,
        (|mut ready: ResMut<PlayerSimulationReady>| ready.0 = true)
            .in_set(PlayerCellStreamingSet::Request),
    );
    app.update();
    app.world_mut().run_schedule(FixedUpdate);
    assert!(app.world().resource::<PlayerSimulationReady>().0);

    let mut state = state(&directory);
    state.ready = true;
    state.dispatched = Some(state.requested);
    app.insert_resource(state);
    app.world_mut()
        .resource_mut::<State>()
        .request(VisualQualityTier::Low);
    runtime::apply(app.world_mut());
    assert!(!app.world().resource::<State>().ready);

    // This command was sampled after apply but before the next graphics observation.
    app.world_mut()
        .resource_mut::<PendingPlayerControlCommand>()
        .submit(PlayerControlCommand {
            move_axis: Vec2::X,
            jump: true,
            ..default()
        })
        .unwrap();
    runtime::report_input_readiness(app.world_mut());
    assert_eq!(
        app.world_mut()
            .resource_mut::<PendingPlayerControlCommand>()
            .drain_tick()
            .0,
        None
    );
    // A second command during the same wait is discarded at the ready transition.
    app.world_mut()
        .resource_mut::<PendingPlayerControlCommand>()
        .submit(PlayerControlCommand {
            move_axis: Vec2::Y,
            ..default()
        })
        .unwrap();
    // A display flag alone cannot grant physical resource admission.
    app.world_mut().resource_mut::<State>().ready = true;
    runtime::report_input_readiness(app.world_mut());
    // Exercise the existing input boundary's admitted transition explicitly;
    // this CPU test creates no renderer-context or resource receipt.
    forge_engine::runtime_static_admission::observe_graphics_input_readiness(app.world_mut(), true);
    assert_eq!(
        app.world_mut()
            .resource_mut::<PendingPlayerControlCommand>()
            .drain_tick()
            .0,
        None
    );
    let fresh = app
        .world_mut()
        .resource_mut::<PendingPlayerControlCommand>()
        .submit(PlayerControlCommand {
            move_axis: Vec2::NEG_X,
            ..default()
        })
        .unwrap();
    app.world_mut().run_schedule(FixedUpdate);
    assert_eq!(
        app.world_mut()
            .resource_mut::<PendingPlayerControlCommand>()
            .drain_tick()
            .0,
        Some(fresh)
    );
}

#[test]
fn graphics_input_observation_keeps_legacy_hosts_unchanged() {
    let directory = Directory::new();
    let mut world = World::new();
    world.insert_resource(state(&directory));
    world.init_resource::<PendingPlayerControlCommand>();
    let sequence = world
        .resource_mut::<PendingPlayerControlCommand>()
        .submit(forge_engine::PlayerControlCommand {
            move_axis: Vec2::X,
            ..default()
        })
        .unwrap();
    runtime::report_input_readiness(&mut world);
    assert_eq!(
        world
            .resource_mut::<PendingPlayerControlCommand>()
            .drain_tick()
            .0,
        Some(sequence)
    );
}

#[test]
fn contact_profiles_require_temporal_reconstruction_without_multisampling() {
    let directory = Directory::new();
    let state = state(&directory);
    for tier in policy::TIERS {
        let preset = state.policy.preset(tier);
        assert_eq!(preset.msaa_samples, 1);
        assert_eq!(preset.temporal_aa, preset.contact_shadows.is_some());
        if preset.temporal_aa {
            assert_eq!(preset.msaa_samples, 1);
            let contact = preset.contact_shadows.unwrap();
            assert_eq!(contact.linear_steps, 16);
            assert_eq!(contact.thickness_m.to_bits(), 0.1_f32.to_bits());
            assert_eq!(contact.length_m.to_bits(), 0.3_f32.to_bits());
        }
    }
}

#[test]
fn applying_profiles_switches_real_camera_components_without_premature_save() {
    let directory = Directory::new();
    let state = state(&directory);
    let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    let mut app = App::new();
    app.add_plugins(MinimalPlugins)
        .add_plugins(
            forge_engine::VisualValleyShadowQualityPlugin::new(
                &terrain,
                state.policy.shadow_source.clone(),
                VisualQualityTier::High,
            )
            .unwrap(),
        )
        .insert_resource(bevy::diagnostic::FrameCount::default())
        .insert_resource(forge_engine::source_graphics::ExposureAssets {
            curve: Handle::default(),
            mask: Handle::default(),
        })
        .insert_resource(state);
    let camera = app.world_mut().spawn(Camera3d::default()).id();
    let capture = app
        .world_mut()
        .spawn((
            Camera3d::default(),
            forge_engine::scene_camera::OffscreenSceneCamera,
        ))
        .id();
    for tier in [
        VisualQualityTier::High,
        VisualQualityTier::Medium,
        VisualQualityTier::Ultra,
        VisualQualityTier::Low,
    ] {
        app.world_mut().resource_mut::<State>().request(tier);
        runtime::apply(app.world_mut());
        let state = app.world().resource::<State>();
        assert!(state.error.is_none(), "{:?}", state.error);
        assert!(!state.ready && state.saved.is_none());
        let preset = state.policy.preset(tier);
        assert_eq!(
            app.world().get::<Msaa>(camera).unwrap().samples(),
            u32::from(preset.msaa_samples)
        );
        assert!(app.world().get::<Msaa>(capture).is_none());
        assert_eq!(
            app.world()
                .get::<bevy::anti_alias::taa::TemporalAntiAliasing>(camera)
                .is_some(),
            preset.temporal_aa
        );
        assert_eq!(
            app.world()
                .get::<bevy::render::camera::TemporalJitter>(camera)
                .is_some(),
            preset.temporal_aa
        );
        assert_eq!(
            app.world()
                .get::<bevy::render::camera::MipBias>(camera)
                .is_some(),
            preset.temporal_aa
        );
        assert!(!directory.0.join("graphics.json").exists());
        assert_eq!(
            app.world()
                .get::<bevy::post_process::bloom::Bloom>(camera)
                .is_some(),
            preset.post_process.bloom_enabled
        );
        assert_eq!(
            app.world()
                .get::<bevy::core_pipeline::tonemapping::Tonemapping>(camera),
            Some(&bevy::core_pipeline::tonemapping::Tonemapping::AcesFitted)
        );
        let expected = forge_engine::source_graphics::Settings::for_quality(
            preset.post_process,
            preset.temporal_aa,
        )
        .unwrap();
        assert!(forge_engine::source_graphics::reference_matches(
            app.world(),
            camera,
            expected,
            preset.post_process,
        ));
        assert_eq!(
            app.world()
                .get::<bevy::anti_alias::fxaa::Fxaa>(camera)
                .is_some_and(|f| f.enabled),
            !preset.temporal_aa
        );
    }
}

#[test]
fn terrain_failure_opens_once_and_keeps_graphics_and_preference_errors_visible() {
    use forge_engine::cell_streaming::PlayerCellStreamingStatus;
    let directory = Directory::new();
    let mut current = state(&directory);
    current.error = Some("independent graphics failure".into());
    current.preference_error = Some("save failure".into());
    let mut app = App::new();
    app.insert_resource(current)
        .insert_resource(GraphicsUi::new(VisualQualityTier::High, false))
        .insert_resource(PlayerCellStreamingStatus {
            error: Some("private technical path".into()),
            ..default()
        })
        .add_systems(Update, (ui::sync_terrain, ui::update).chain());
    let status = app.world_mut().spawn((ui::Status, Text::new(""))).id();
    let title = app.world_mut().spawn((ui::Title, Text::new(""))).id();
    let terrain = app
        .world_mut()
        .spawn((ui::TerrainStatus, Text::new(""), Node::default()))
        .id();
    app.update();
    assert!(app.world().resource::<GraphicsUi>().open);
    assert_eq!(
        app.world().resource::<GraphicsUi>().terrain,
        ui::TerrainNotice::Unavailable
    );
    let graphics_text = &app.world().get::<Text>(status).unwrap().0;
    assert!(graphics_text.contains("Impossible d'appliquer"));
    assert!(graphics_text.contains("non enregistrés"));
    let terrain_text = &app.world().get::<Text>(terrain).unwrap().0;
    assert!(terrain_text.contains("partie est en pause"));
    assert!(terrain_text.contains("relancez le jeu"));
    assert!(terrain_text.contains("non sauvegardée"));
    assert!(!terrain_text.contains("private technical path"));
    assert_eq!(
        app.world().get::<Text>(title).unwrap().0,
        "Chargement du terrain"
    );
    app.world_mut().resource_mut::<GraphicsUi>().open = false;
    app.update();
    assert!(!app.world().resource::<GraphicsUi>().open);
    // Clearing the stream error before the fixed-step guard updates does not imply readiness.
    let mut player = app.world_mut().resource_mut::<PlayerCellStreamingStatus>();
    player.error = None;
    player.ready = false;
    app.update();
    assert_eq!(
        app.world().resource::<GraphicsUi>().terrain,
        ui::TerrainNotice::Waiting
    );
    assert!(
        app.world()
            .get::<Text>(terrain)
            .unwrap()
            .0
            .contains("reste bloquée")
    );
    // This synthetic status transition checks the presentation, not actual Rapier readiness.
    app.world_mut()
        .resource_mut::<PlayerCellStreamingStatus>()
        .ready = true;
    app.update();
    assert_eq!(
        app.world().resource::<GraphicsUi>().terrain,
        ui::TerrainNotice::None
    );
    assert_eq!(
        app.world().get::<Text>(title).unwrap().0,
        "Réglages graphiques"
    );
}

#[test]
fn terrain_waits_for_a_fresh_fixed_decision_after_the_stream_error_clears() {
    use forge_engine::cell_streaming::{CellStreamingStats, PlayerCellStreamingStatus};

    #[derive(Resource, Default)]
    struct NextDecision(Option<(bool, Option<&'static str>)>);
    fn guard_decision(
        mut player: ResMut<PlayerCellStreamingStatus>,
        mut next: ResMut<NextDecision>,
    ) {
        if let Some((ready, error)) = next.0.take() {
            player.ready = ready;
            player.error = error.map(str::to_owned);
        }
    }
    let mut app = App::new();
    app.insert_resource(GraphicsUi::new(VisualQualityTier::High, false))
        .insert_resource(CellStreamingStats {
            last_error: Some("request failed".into()),
            ..default()
        })
        .insert_resource(PlayerCellStreamingStatus {
            ready: false,
            error: Some("old player refusal".into()),
            ..default()
        })
        .init_resource::<NextDecision>()
        .add_systems(PostUpdate, ui::sync_terrain)
        .add_systems(FixedUpdate, guard_decision);
    app.update();
    app.world_mut()
        .resource_mut::<CellStreamingStats>()
        .last_error = None;
    for _ in 0..4 {
        app.update();
        assert_eq!(
            app.world().resource::<GraphicsUi>().terrain,
            ui::TerrainNotice::Waiting
        );
    }
    // A new, still-failing fixed decision is a real refusal, not the old status.
    app.world_mut().resource_mut::<NextDecision>().0 = Some((false, Some("persistent refusal")));
    app.world_mut().run_schedule(FixedUpdate);
    app.update();
    assert_eq!(
        app.world().resource::<GraphicsUi>().terrain,
        ui::TerrainNotice::Unavailable
    );

    // A later attempt may succeed at the stream before Rapier is ready.
    app.world_mut()
        .resource_mut::<CellStreamingStats>()
        .last_error = Some("next attempt".into());
    app.update();
    app.world_mut()
        .resource_mut::<CellStreamingStats>()
        .last_error = None;
    app.update();
    assert_eq!(
        app.world().resource::<GraphicsUi>().terrain,
        ui::TerrainNotice::Waiting
    );
    app.world_mut().resource_mut::<NextDecision>().0 = Some((false, None));
    app.world_mut().run_schedule(FixedUpdate);
    app.update();
    assert_eq!(
        app.world().resource::<GraphicsUi>().terrain,
        ui::TerrainNotice::Waiting
    );
    // Simulated status changes check only presentation, not actual backend readiness.
    app.world_mut().resource_mut::<NextDecision>().0 = Some((true, None));
    app.world_mut().run_schedule(FixedUpdate);
    app.update();
    assert_eq!(
        app.world().resource::<GraphicsUi>().terrain,
        ui::TerrainNotice::None
    );
}

#[test]
fn keyboard_terrain_retry_does_not_apply_graphics_or_overwrite_preferences() {
    let directory = Directory::new();
    let mut current = state(&directory);
    current.preference_error = Some("save failure".into());
    let mut ui = GraphicsUi::new(VisualQualityTier::High, true);
    ui.terrain = ui::TerrainNotice::Retryable;
    ui.focus = 6;
    let mut app = App::new();
    app.insert_resource(current)
        .insert_resource(ui)
        .insert_resource(PlayerInputFocus(true))
        .init_resource::<PendingPlayerControlCommand>()
        .init_resource::<ButtonInput<KeyCode>>()
        .add_message::<NativeGraphicsCommand>()
        .add_systems(Update, ui::input);
    // With no stream installed the actual service refuses the stale request.
    press(&mut app, &[KeyCode::Enter]);
    assert!(!app.world().resource::<GraphicsUi>().retrying);
    assert!(!app.world().resource::<PlayerInputFocus>().0);
    assert_eq!(
        app.world_mut()
            .resource_mut::<Messages<NativeGraphicsCommand>>()
            .drain()
            .count(),
        0
    );
    let state = app.world().resource::<State>();
    assert_eq!(state.requested, VisualQualityTier::High);
    assert_eq!(state.preference_error.as_deref(), Some("save failure"));
    assert!(!state.save_requested);
}

// The real Bevy input, procedural streaming and Rapier guard run together.
// This CPU fixture does not render, send OS input or prove pixels.
mod keyboard_terrain_retry_fixture {
    use super::*;
    use bevy_rapier3d::prelude::RapierConfiguration;
    use forge_engine::{
        ForgePlayerCharacter, PlayerControllerPlugin, PlayerSimulationReady,
        cell_streaming::{
            CellStreamingObserver, CellStreamingStats, PlayerCellStreamingPlugin,
            PlayerCellStreamingStatus, ProceduralCellStreamingPlugin, StreamedTerrainCell,
            render_selection,
        },
    };
    use forge_world::{
        TerrainGenerationPlan,
        cell_streaming::{CellDetail, CellKey, CellStreamingPolicy},
    };
    use std::{
        sync::Arc,
        time::{Duration, Instant},
    };

    fn setup(directory: &Directory) -> (App, Arc<TerrainGenerationPlan>, forge_core::AreaId) {
        let document = forge_world::embedded_visual_valley_terrain_document().unwrap();
        let terrain = Arc::new(TerrainGenerationPlan::from_document(&document).unwrap());
        let policy = CellStreamingPolicy {
            origin_xz_m: [-256.0; 2],
            cell_size_m: 64.0,
            dimensions: [8; 2],
            detail_radius: 0,
            far_radius: 2,
            max_resident_cells: 25,
            full_cell_bytes: 100_000,
            hlod_cell_bytes: 100_000,
            max_resident_bytes: 8_000_000,
            max_staging_bytes: 16_000_000,
        };
        let spawn = Vec3::new(
            -32.0,
            terrain.surface_height_at(-32.0, -32.0).unwrap() + 2.0,
            -32.0,
        );
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, TransformPlugin))
            .insert_resource(bevy::time::TimeUpdateStrategy::ManualDuration(
                Duration::from_secs_f64(1.0 / 60.0),
            ))
            .insert_resource(Assets::<Mesh>::default())
            .insert_resource(Assets::<StandardMaterial>::default())
            .insert_resource(state(directory))
            .insert_resource(GraphicsUi::new(VisualQualityTier::High, false))
            .init_resource::<ButtonInput<KeyCode>>()
            .add_message::<NativeGraphicsCommand>()
            .add_plugins(
                ProceduralCellStreamingPlugin::new(
                    terrain.clone(),
                    policy,
                    forge_core::EntityId::deterministic("retry-cpu"),
                    StandardMaterial::default(),
                    [-32.0; 2],
                )
                .unwrap(),
            );
        forge_engine::sampled_terrain_physics::register(&mut app);
        app.add_plugins((
            PlayerControllerPlugin::new(
                forge_world::embedded_visual_valley_player_controller_profile().unwrap(),
                spawn,
            )
            .with_interactive_input(false)
            .with_initial_camera_target(spawn + Vec3::NEG_Z),
            PlayerCellStreamingPlugin,
        ))
        .add_systems(Update, ui::input)
        .add_systems(PostUpdate, ui::sync_terrain);
        (app, terrain, document.area_id)
    }

    fn wait_initial_admission(app: &mut App, deadline: Instant) {
        loop {
            app.update();
            let status = app.world().resource::<PlayerCellStreamingStatus>();
            assert!(
                status.error.is_none(),
                "initial admission: {:?}",
                status.error
            );
            if status.ready && app.world().resource::<PlayerSimulationReady>().0 {
                break;
            }
            assert!(
                Instant::now() < deadline,
                "initial player/terrain admission did not finish"
            );
            std::thread::sleep(Duration::from_millis(1));
        }
    }

    fn remove_retained_cell(
        app: &mut App,
        terrain: &TerrainGenerationPlan,
        area: forge_core::AreaId,
        deadline: Instant,
    ) -> (Entity, Handle<Mesh>, StreamedTerrainCell) {
        // Move the fixture player across one cell to request a real transition.
        // This placement is not an observed gameplay movement.
        let player = {
            let world = app.world_mut();
            let mut query = world.query_filtered::<Entity, With<ForgePlayerCharacter>>();
            query.single(world).unwrap()
        };
        {
            let mut transform = app.world_mut().get_mut::<Transform>(player).unwrap();
            transform.translation.x = 32.0;
            transform.translation.y = terrain.surface_height_at(32.0, -32.0).unwrap() + 2.0;
        }
        loop {
            app.update();
            if app.world().resource::<CellStreamingStats>().pending {
                break;
            }
            assert!(
                Instant::now() < deadline,
                "no real terrain transition was requested"
            );
            std::thread::sleep(Duration::from_millis(1));
        }
        // Protected-body neighbourhoods keep [3,4] Full in both requests.
        let key = CellKey { x: 3, z: 4 };
        let (entity, mesh, marker) = {
            let world = app.world_mut();
            let mut query = world.query::<(Entity, &Mesh3d, &StreamedTerrainCell)>();
            query
                .iter(world)
                .find_map(|(entity, mesh, marker)| {
                    (marker.key == key && marker.detail == CellDetail::Full)
                        .then(|| (entity, mesh.0.clone(), *marker))
                })
                .expect("expected retained streamed terrain cell absent")
        };
        assert_eq!(marker.logical_id, key.entity_id(area));
        assert_eq!(
            render_selection(app.world())
                .unwrap()
                .cells
                .get(&key)
                .unwrap()
                .detail,
            CellDetail::Full
        );
        app.world_mut().entity_mut(entity).remove::<Mesh3d>();
        (entity, mesh, marker)
    }

    fn wait_for_refusal_and_restore(
        app: &mut App,
        entity: Entity,
        mesh: &Handle<Mesh>,
        deadline: Instant,
    ) {
        loop {
            app.update();
            let stats = app.world().resource::<CellStreamingStats>();
            let player = app.world().resource::<PlayerCellStreamingStatus>();
            if stats.last_error.as_deref().is_some_and(|e| {
                e.contains("active streamed terrain projection was removed or replaced")
            }) && !player.ready
                && !app.world().resource::<PlayerSimulationReady>().0
            {
                break;
            }
            assert!(
                Instant::now() < deadline,
                "missing projection did not stop the player"
            );
            std::thread::sleep(Duration::from_millis(1));
        }
        assert!(app.world().get::<Mesh3d>(entity).is_none());
        app.world_mut()
            .entity_mut(entity)
            .insert(Mesh3d(mesh.clone()));
        assert_eq!(&app.world().get::<Mesh3d>(entity).unwrap().0, mesh);
    }

    fn retry_from_keyboard(app: &mut App, deadline: Instant) {
        // The menu may initially focus Close; navigate rather than assuming focus.
        loop {
            app.update();
            if app.world().resource::<GraphicsUi>().terrain == ui::TerrainNotice::Retryable {
                break;
            }
            assert!(
                Instant::now() < deadline,
                "restored terrain did not offer retry"
            );
            std::thread::sleep(Duration::from_millis(1));
        }
        let before = app
            .world()
            .resource::<CellStreamingObserver>()
            .retry_sequence;
        for _ in 0..7 {
            if app.world().resource::<GraphicsUi>().focus == 6 {
                break;
            }
            press(app, &[KeyCode::Tab]);
        }
        assert_eq!(app.world().resource::<GraphicsUi>().focus, 6);
        press(app, &[KeyCode::Enter]);
        assert_eq!(
            app.world()
                .resource::<CellStreamingObserver>()
                .retry_sequence,
            before + 1
        );
    }

    fn verify_recovered(
        app: &mut App,
        entity: Entity,
        mesh: &Handle<Mesh>,
        marker: StreamedTerrainCell,
        deadline: Instant,
    ) {
        loop {
            app.update();
            let stats = app.world().resource::<CellStreamingStats>();
            let player = app.world().resource::<PlayerCellStreamingStatus>();
            if stats.last_error.is_none()
                && !stats.pending
                && player.ready
                && app.world().resource::<PlayerSimulationReady>().0
            {
                break;
            }
            assert!(
                Instant::now() < deadline,
                "real admission did not recover after keyboard retry"
            );
            std::thread::sleep(Duration::from_millis(1));
        }
        assert_eq!(&app.world().get::<Mesh3d>(entity).unwrap().0, mesh);
        let final_marker = app.world().get::<StreamedTerrainCell>(entity).unwrap();
        assert_eq!(final_marker.key, marker.key);
        assert_eq!(final_marker.source, marker.source);
        assert_eq!(final_marker.logical_id, marker.logical_id);
        assert_eq!(
            render_selection(app.world())
                .unwrap()
                .cells
                .get(&marker.key)
                .unwrap()
                .detail,
            CellDetail::Full
        );
        let world = app.world_mut();
        let mut contexts = world.query::<&RapierConfiguration>();
        let configurations: Vec<_> = contexts.iter(world).collect();
        assert_eq!(configurations.len(), 1);
        assert!(configurations[0].physics_pipeline_active);
    }

    #[test]
    fn keyboard_retry_recovers_real_stream_admission_and_player_guard() {
        let directory = Directory::new();
        let (mut app, terrain, area) = setup(&directory);
        let deadline = Instant::now() + Duration::from_secs(30);
        wait_initial_admission(&mut app, deadline);
        let (entity, mesh, marker) = remove_retained_cell(&mut app, &terrain, area, deadline);
        wait_for_refusal_and_restore(&mut app, entity, &mesh, deadline);
        retry_from_keyboard(&mut app, deadline);
        verify_recovered(&mut app, entity, &mesh, marker, deadline);
    }
}
