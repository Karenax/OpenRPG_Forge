//! Separate headless diagnostic using Bevy's real TAA component and jitter.
//! This is not imported-effect parity, motion-vector, shadow or resolve proof.

#[allow(
    dead_code,
    reason = "Shared synthetic scene helpers, not a second probe run"
)]
#[path = "source_particle_view_gate_probe.rs"]
mod fixture;

use bevy::{
    anti_alias::taa::{TemporalAntiAliasPlugin, TemporalAntiAliasing},
    app::PluginsState,
    core_pipeline::prepass::{DepthPrepass, MotionVectorPrepass},
    prelude::*,
    render::{
        Render, RenderApp, RenderSystems, camera::TemporalJitter,
        extract_component::ExtractComponentPlugin, pipelined_rendering::PipelinedRenderingPlugin,
        sync_world::MainEntity, view::prepare_view_uniforms,
    },
    window::{ExitCondition, WindowPlugin},
    winit::WinitPlugin,
};
use forge_engine::{
    source_graph::SourceGraphPlugin,
    source_graph_runtime::{
        SourceClock, SourceFrameStatus, SourceSceneResidents, SourceSceneRuntimePlugin,
    },
    source_particle_compute::SourceParticleProjectionPlugin,
    source_refraction::{CaptureCamera, CaptureSource},
};
use std::{
    collections::BTreeSet,
    path::{Path, PathBuf},
    sync::{Arc, Mutex},
    time::{Duration, Instant},
};

#[derive(Resource)]
struct JitterWatch {
    main: Entity,
    phases: Arc<Mutex<BTreeSet<(u32, u32)>>>,
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy supplies render system parameters"
)]
fn observe_real_taa(
    watch: Res<JitterWatch>,
    views: Query<(&MainEntity, &TemporalJitter), With<TemporalAntiAliasing>>,
) {
    for (main, jitter) in &views {
        if main.id() == watch.main
            && jitter.offset.is_finite()
            && let Ok(mut phases) = watch.phases.lock()
        {
            phases.insert((jitter.offset.x.to_bits(), jitter.offset.y.to_bits()));
        }
    }
}

#[allow(
    clippy::too_many_arguments,
    reason = "One bounded readback gate checks this exact render transition"
)]
fn wait_for_transition(
    app: &mut App,
    root: Entity,
    main_camera: Entity,
    main: &fixture::PixelSlot,
    capture: &fixture::PixelSlot,
    visible: bool,
    moved: bool,
    reset_taa: bool,
    deadline: Instant,
) -> Result<fixture::Sample, String> {
    let mut seen = [
        main.lock().map_err(|_| "Main readback lock poisoned")?.0,
        capture
            .lock()
            .map_err(|_| "Capture readback lock poisoned")?
            .0,
    ];
    let mut stable = 0;
    loop {
        if Instant::now() > deadline {
            return Err(format!(
                "Timed out waiting for TAA particle visibility={visible}, moved={moved}"
            ));
        }
        if reset_taa
            && let Some(mut taa) = app.world_mut().get_mut::<TemporalAntiAliasing>(main_camera)
        {
            taa.reset = true;
        }
        app.update();
        if visible
            && let Some(SourceFrameStatus::Refused(reason)) =
                app.world().get::<SourceFrameStatus>(root)
        {
            return Err(format!("TAA source scene refused: {reason}"));
        }
        let a = main
            .lock()
            .map_err(|_| "Main readback lock poisoned")?
            .clone();
        let b = capture
            .lock()
            .map_err(|_| "Capture readback lock poisoned")?
            .clone();
        if a.0 > seen[0]
            && b.0 > seen[1]
            && let (Some(main_bytes), Some(capture_bytes)) = (a.1, b.1)
        {
            seen = [a.0, b.0];
            let counts = [
                fixture::colored_pixels(&main_bytes)?,
                fixture::colored_pixels(&capture_bytes)?,
            ];
            let matches = if visible {
                counts[0][0] >= 20 && (!moved || counts[0][2] >= 20)
            } else {
                counts[0][0] == 0
            };
            if matches {
                stable += 1;
                if stable >= 4 {
                    return Ok((main_bytes, capture_bytes, counts));
                }
            } else {
                stable = 0;
            }
        }
        std::thread::sleep(Duration::from_millis(4));
    }
}

#[allow(
    clippy::too_many_lines,
    reason = "Ordered GPU transitions share one real Bevy TAA renderer"
)]
fn run(output: &Path) -> Result<(), String> {
    std::fs::create_dir_all(output).map_err(|error| error.to_string())?;
    let mut app = App::new();
    app.add_plugins(
        DefaultPlugins
            .set(WindowPlugin {
                primary_window: None,
                exit_condition: ExitCondition::DontExit,
                ..default()
            })
            .disable::<WinitPlugin>(),
    )
    .add_plugins((
        SourceGraphPlugin,
        SourceParticleProjectionPlugin,
        SourceSceneRuntimePlugin {
            capture_bevy_clock: false,
            ..default()
        },
        ExtractComponentPlugin::<CaptureCamera>::default(),
        ExtractComponentPlugin::<CaptureSource>::default(),
    ))
    .insert_resource(SourceClock {
        sequence: 1,
        ..default()
    });
    if !app.is_plugin_added::<TemporalAntiAliasPlugin>() {
        app.add_plugins(TemporalAntiAliasPlugin);
    }
    if !app.is_plugin_added::<PipelinedRenderingPlugin>() {
        return Err("Pipelined renderer unavailable".into());
    }
    let main_image = fixture::image(&mut app);
    let capture_image = fixture::image(&mut app);
    let main = fixture::camera(&mut app, main_image.clone(), None);
    let capture = fixture::camera(&mut app, capture_image.clone(), Some(main));
    app.world_mut()
        .entity_mut(main)
        .insert(TemporalAntiAliasing::default());
    let phases = Arc::new(Mutex::new(BTreeSet::new()));
    app.get_sub_app_mut(RenderApp)
        .ok_or("RenderApp absent")?
        .insert_resource(JitterWatch {
            main,
            phases: phases.clone(),
        })
        .add_systems(
            Render,
            observe_real_taa
                .in_set(RenderSystems::PrepareResources)
                .before(prepare_view_uniforms),
        );
    let deadline = Instant::now() + Duration::from_secs(240);
    while app.plugins_state() != PluginsState::Ready {
        if Instant::now() > deadline {
            return Err("Renderer initialization timeout".into());
        }
        std::thread::sleep(Duration::from_millis(8));
    }
    app.finish();
    let adapter = app
        .get_sub_app(RenderApp)
        .ok_or("RenderApp absent before start")?
        .world()
        .resource::<bevy::render::renderer::RenderAdapter>()
        .get_info();
    app.cleanup();
    loop {
        if Instant::now() > deadline {
            return Err("Camera viewport initialization timeout".into());
        }
        app.update();
        if [main, capture].into_iter().all(|entity| {
            app.world().get::<Camera>(entity).is_some_and(|camera| {
                camera.physical_viewport_size() == Some(UVec2::splat(fixture::SIZE))
            })
        }) {
            break;
        }
        std::thread::sleep(Duration::from_millis(4));
    }
    let root = fixture::mount_scene(&mut app)?;
    let main_pixels = fixture::readback(&mut app, main_image);
    let capture_pixels = fixture::readback(&mut app, capture_image);
    let mut cases = serde_json::Map::new();
    let mut sample = |app: &mut App, name: &str, visible: bool, moved: bool, reset: bool| {
        let result = wait_for_transition(
            app,
            root,
            main,
            &main_pixels,
            &capture_pixels,
            visible,
            moved,
            reset,
            deadline,
        )?;
        let value = fixture::save_case(output, name, result)?;
        cases.insert(name.to_owned(), value);
        Ok::<(), String>(())
    };
    sample(&mut app, "taa-initial", true, false, false)?;
    let moved = Transform::from_xyz(0.4, 0.2, 3.0).looking_at(Vec3::ZERO, Vec3::Y);
    app.world_mut().entity_mut(main).insert(moved);
    app.world_mut().entity_mut(capture).insert(moved);
    sample(&mut app, "taa-moved", true, true, false)?;
    app.world_mut()
        .entity_mut(main)
        .remove::<(TemporalAntiAliasing, TemporalJitter)>();
    sample(&mut app, "taa-disabled", true, true, false)?;
    app.world_mut()
        .entity_mut(main)
        .insert(TemporalAntiAliasing::default());
    sample(&mut app, "taa-restored", true, true, false)?;
    let residents = app
        .world_mut()
        .remove_resource::<SourceSceneResidents>()
        .ok_or("Source scenes absent")?;
    sample(&mut app, "source-resource-absent", false, false, true)?;
    app.world_mut().insert_resource(residents);
    sample(&mut app, "fresh-scene-restored", true, true, false)?;
    if app.world().get::<TemporalAntiAliasing>(main).is_none()
        || app.world().get::<TemporalJitter>(main).is_none()
        || app.world().get::<DepthPrepass>(main).is_none()
        || app.world().get::<MotionVectorPrepass>(main).is_none()
    {
        return Err("Main-world Bevy TAA, jitter or required prepasses absent".into());
    }
    let phases = phases
        .lock()
        .map_err(|_| "Jitter observation lock poisoned")?;
    if phases.len() < 2 || phases.iter().all(|(x, y)| *x == 0 && *y == 0) {
        return Err("Fewer than two real Bevy TAA jitter phases observed".into());
    }
    let offsets: Vec<_> = phases
        .iter()
        .map(|(x, y)| [f32::from_bits(*x), f32::from_bits(*y)])
        .collect();
    let report = serde_json::json!({
        "schema": "forge.synthetic-particle-real-bevy-taa-gpu.v1",
        "status": "GPU_SYNTHETIC_REAL_BEVY_TAA_PARTICLE_TRANSITIONS_PASS",
        "adapter": {"name":adapter.name,"backend":format!("{:?}",adapter.backend),
            "vendor":adapter.vendor,"device":adapter.device,"driver":adapter.driver},
        "pipelined_rendering_enabled": true,
        "main_world_bevy_taa_and_jitter": true,
        "main_world_required_prepasses": true,
        "compute_sha256": forge_core::bytes_sha256(fixture::COMPUTE.as_bytes()),
        "projection_sha256": forge_core::bytes_sha256(fixture::PROJECT.as_bytes()),
        "draw_shader_sha256": forge_core::bytes_sha256(fixture::DRAW.as_bytes()),
        "render_world_observed_jitter_offsets": offsets,
        "cases": cases,
        "limitations": ["synthetic particle and camera; no imported effect or authoring parity",
            "TAA history is reset while checking the missing-source transition",
            "readback requires four fresh stable observations, not same-frame GPU timestamps",
            "particle motion vectors, ghosting quality, shadows and resolution overrides remain unqualified"]
    });
    std::fs::write(
        output.join("RESULT.v1.json"),
        serde_json::to_vec_pretty(&report).map_err(|error| error.to_string())?,
    )
    .map_err(|error| error.to_string())?;
    println!("GPU synthetic real Bevy TAA particle transitions passed");
    Ok(())
}

fn main() {
    let result = std::env::args_os()
        .nth(1)
        .ok_or("Fresh output directory required".to_owned())
        .and_then(|path| run(&PathBuf::from(path)));
    if let Err(error) = result {
        eprintln!("FAIL {error}");
        std::process::exit(1);
    }
}
