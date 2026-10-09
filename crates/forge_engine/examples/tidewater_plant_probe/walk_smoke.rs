//! Real fixed-step controller exercise with the complete native rendering scene.
use super::Probe;
use bevy::{
    app::AppExit,
    diagnostic::FrameCount,
    prelude::*,
    render::view::screenshot::{Screenshot, ScreenshotCaptured, save_to_disk},
};
use forge_engine::{
    PendingPlayerControlCommand, PlayerControlCommand, PlayerControllerSet, PlayerRuntimeSnapshot,
    PlayerSimulationReady,
};
use std::io::Write;
#[path = "walk_cloud_readback.rs"]
mod clouds;
#[path = "walk_coupled_readback.rs"]
mod coupled;
#[path = "walk_grass_readback.rs"]
mod grass_gpu;
#[path = "walk_navigation.rs"]
mod navigation;
#[path = "walk_render_diagnostics.rs"]
mod render_diagnostics;
#[path = "walk_shadow_rate.rs"]
mod shadow_rate;
#[path = "walk_shadow_readback.rs"]
mod shadows;
#[path = "walk_wind_readback.rs"]
mod wind;

#[derive(Resource, Default)]
struct Smoke {
    ticks: u32,
    start: Option<Vec3>,
    grounded: u32,
    requested: bool,
    fft_samples: Vec<(u32, forge_core::ContentHash)>,
    live_start: Option<TimingSample>,
    live_end: Option<TimingSample>,
    minimum_ticks: u32,
    sample_step: u32,
    frame_intervals: Vec<f64>,
    throttle_30hz: bool,
    route: navigation::Route,
    camera_turn: bool,
    previous_camera_rotation: Option<Quat>,
    moving_camera_frames: u32,
    moving_frames_with_blur: u32,
    moving_frames_with_taa: u32,
    turn_capture_requested: bool,
}

#[derive(Clone, Copy, serde::Serialize)]
struct TimingSample {
    wall: f64,
    step: u32,
    simulation: f64,
}

pub(super) fn install(app: &mut App) {
    // A focus change during this bounded benchmark must not add Winit sleeps.
    // Interactive walks retain Bevy's normal game/background policy.
    app.insert_resource(bevy::winit::WinitSettings::continuous());
    let long = matches!(
        app.world().resource::<Probe>().plant_projection,
        crate::PlantProjection::WalkLongSmoke
    );
    coupled::install(app);
    clouds::install(app);
    wind::install(app);
    shadows::install(app);
    shadow_rate::install(app);
    render_diagnostics::install(app);
    grass_gpu::install(app);
    let route = navigation::Route::new();
    app.insert_resource(Smoke {
        minimum_ticks: if route.enabled {
            3600
        } else if long {
            1800
        } else {
            270
        },
        sample_step: if long { 1500 } else { 150 },
        throttle_30hz: std::env::var("FORGE_TIDEWATER_SMOKE_30HZ").as_deref() == Ok("1"),
        route,
        camera_turn: std::env::var("FORGE_TIDEWATER_SMOKE_CAMERA_TURN").as_deref() == Ok("1"),
        ..default()
    })
    .add_systems(FixedUpdate, drive.before(PlayerControllerSet::Drive))
    .add_systems(Update, (fft_readback, timing, finish.after(timing)))
    .add_systems(Last, throttle)
    .add_systems(
        PostUpdate,
        observe_camera_motion.after(bevy::transform::TransformSystems::Propagate),
    );
}

/// Bounded test-only pacing: the interactive walk never installs this system.
fn throttle(smoke: Res<Smoke>, mut previous: Local<Option<std::time::Instant>>) {
    if !smoke.throttle_30hz {
        return;
    }
    if let Some(previous) = *previous {
        let due = previous + std::time::Duration::from_secs_f64(1.0 / 30.0);
        if let Some(remaining) = due.checked_duration_since(std::time::Instant::now()) {
            std::thread::sleep(remaining);
        }
    }
    *previous = Some(std::time::Instant::now());
}

fn fft_readback(
    mut commands: Commands,
    ocean: Res<crate::ocean::Targets>,
    smoke: Res<Smoke>,
    mut started: Local<bool>,
) {
    use bevy::render::gpu_readback::{Readback, ReadbackComplete};
    use std::sync::atomic::Ordering;
    if *started || ocean.completed_steps.load(Ordering::Acquire) < smoke.sample_step {
        return;
    }
    *started = true;
    commands
        .spawn(Readback::texture(ocean.images[0].clone()))
        .observe(
            |event: On<ReadbackComplete>,
             ocean: Res<crate::ocean::Targets>,
             mut smoke: ResMut<Smoke>,
             mut commands: Commands| {
                let step = ocean.completed_steps.load(Ordering::Acquire);
                if smoke
                    .fft_samples
                    .last()
                    .is_some_and(|(old, _)| step < old + 30)
                {
                    return;
                }
                smoke
                    .fft_samples
                    .push((step, forge_core::bytes_sha256(&event.data)));
                if smoke.fft_samples.len() == 2 {
                    commands.entity(event.entity).despawn();
                }
            },
        );
}

fn drive(
    frame: Res<FrameCount>,
    ready: Res<PlayerSimulationReady>,
    state: Res<PlayerRuntimeSnapshot>,
    mut smoke: ResMut<Smoke>,
    mut command: ResMut<PendingPlayerControlCommand>,
) {
    if !ready.0 || frame.0 < 180 || smoke.requested {
        return;
    }
    smoke.start.get_or_insert(state.player_position);
    smoke.ticks += 1;
    smoke.grounded += u32::from(state.grounded);
    let input = if smoke.route.enabled {
        smoke.route.command(&state)
    } else {
        PlayerControlCommand {
            move_axis: if (60..240).contains(&smoke.ticks) {
                Vec2::Y
            } else {
                Vec2::ZERO
            },
            look_delta: if smoke.camera_turn && (60..240).contains(&smoke.ticks) {
                Vec2::new(
                    if (smoke.ticks / 30) % 2 == 0 {
                        8.0
                    } else {
                        -8.0
                    },
                    0.0,
                )
            } else {
                Vec2::ZERO
            },
            ..default()
        }
    };
    command.submit(input).expect("finite walk command");
}

fn observe_camera_motion(
    mut commands: Commands,
    mut smoke: ResMut<Smoke>,
    probe: Res<Probe>,
    cameras: Query<
        (
            &GlobalTransform,
            Option<&bevy::post_process::motion_blur::MotionBlur>,
            Option<&bevy::anti_alias::taa::TemporalAntiAliasing>,
        ),
        With<forge_engine::ForgePlayerCamera>,
    >,
) {
    if !smoke.camera_turn {
        return;
    }
    let Ok((camera, blur, taa)) = cameras.single() else {
        return;
    };
    let rotation = camera.compute_transform().rotation;
    let previous = smoke.previous_camera_rotation.replace(rotation);
    if previous.is_some_and(|q| q.angle_between(rotation).abs() > 0.0001) {
        smoke.moving_camera_frames += 1;
        smoke.moving_frames_with_blur += u32::from(blur.is_some());
        smoke.moving_frames_with_taa += u32::from(taa.is_some());
        if smoke.ticks >= 90 && !smoke.turn_capture_requested {
            smoke.turn_capture_requested = true;
            commands
                .spawn(Screenshot::primary_window())
                .observe(save_to_disk(
                    probe.screenshot.with_extension("camera-turn.png"),
                ));
        }
    }
}

fn timing(
    time: Res<Time<Real>>,
    feedback: Res<crate::spray::LiveFeedback>,
    wind: Res<wind::Proof>,
    mut smoke: ResMut<Smoke>,
) {
    let feedback = feedback.statistics();
    let sample = TimingSample {
        wall: time.elapsed_secs_f64(),
        step: feedback.latest_sample_step,
        simulation: feedback.latest_sample_time,
    };
    if feedback.latest_sample_step > 120 {
        smoke.live_start.get_or_insert(sample);
    }
    if smoke
        .live_start
        .is_some_and(|start| sample.wall - start.wall >= 1.0)
        && !smoke.requested
        && !wind.started
        && smoke.frame_intervals.len() < 100_000
    {
        smoke.frame_intervals.push(time.delta_secs_f64());
        smoke.live_end = Some(sample);
    }
}

#[allow(
    clippy::too_many_arguments,
    clippy::too_many_lines,
    clippy::type_complexity
)] // One bounded Bevy qualification system correlates every rendered water-layer receipt.
fn finish(
    mut commands: Commands,
    time: Res<Time<Real>>,
    ocean_clock: Res<crate::ocean::LiveClock>,
    mut smoke: ResMut<Smoke>,
    state: Res<PlayerRuntimeSnapshot>,
    (walk, coupled, camera, probe, feedback, cloud, wind, shadows, shadow_rate, grass): (
        Res<super::Walk>,
        Res<coupled::Proof>,
        Res<crate::spray::LiveCamera>,
        Res<Probe>,
        Res<crate::spray::LiveFeedback>,
        (Res<clouds::Proof>, Res<crate::walk_clouds::Receipt>),
        (Res<wind::Proof>, Res<crate::walk_wind::Receipt>),
        Res<shadows::Proof>,
        Res<shadow_rate::Proof>,
        (
            Res<crate::walk_grass::Live>,
            Res<grass_gpu::Proof>,
            Res<crate::spray::water_queries::Targets>,
        ),
    ),
    water: (
        Res<crate::water_time::WaterTime>,
        Res<Assets<crate::water_optics::WaterMaterial>>,
        Res<Assets<crate::spray_sprites::SprayMaterial>>,
        Res<Assets<crate::breaker_lip::LipMaterial>>,
    ),
    source_fish: Option<Res<crate::source_fish::Receipt>>,
    shadow_sources: Option<Res<crate::object_shadow::live::Sources>>,
    diagnostics: Res<bevy::diagnostic::DiagnosticsStore>,
    graphics: Res<super::graphics::GraphicsState>,
    mut exit: MessageWriter<AppExit>,
) {
    let feedback = feedback.statistics();
    if time.elapsed_secs() > 180.0 {
        error!(ticks = smoke.ticks, position = ?state.player_position,
            waypoints = smoke.route.reached.len(), grass_updates = grass.0.updates,
            wind_complete = wind.0.complete(), shadows_complete = shadows.complete(),
            grass_complete = grass.1.complete(), "walk smoke readiness timeout");
        exit.write(AppExit::error());
        return;
    }
    if smoke.ticks < smoke.minimum_ticks
        || smoke.requested
        || smoke.fft_samples.len() < 2
        || !coupled.complete()
        || !cloud.0.complete()
        || !wind.0.complete()
        || !shadows.complete()
        || !shadow_rate.complete()
        || !grass.1.complete()
    {
        return;
    }
    smoke.requested = true;
    let start = smoke.start.expect("walk started");
    let displacement = (state.player_position - start).xz().length();
    let camera_samples = camera.samples();
    let camera_moves = camera_moved(camera_samples);
    let water_frames: [Vec<_>; 3] = [
        water.1.iter().map(|(_, m)| m.frame[36]).collect(),
        water.2.iter().map(|(_, m)| m.frame[36]).collect(),
        water.3.iter().map(|(_, m)| m.frame[36]).collect(),
    ];
    let water_in_sync = water_in_sync(&water.0, &water_frames);
    let observed = observed_rates(&smoke);
    let observed_rate = observed.map(|rates| rates.0);
    let simulation_rate = observed.map(|rates| rates.1);
    let feedback_valid = feedback.encoded_steps >= 60
        && feedback.accepted_samples > 2
        && feedback.delayed_feedback_steps > 0
        && feedback.latest_sample_step <= feedback.latest_encoded_step
        && observed_rate.is_some_and(|rate| rate.is_finite() && rate > 0.0);
    let cloud_valid = cloud.0.changed() && cloud.1.valid();
    let wind_valid = wind.0.valid() && wind.1.submitted > 60 && wind.1.materials > 0;
    let functional_pass = displacement > 3.0
        && smoke.grounded > 180
        && state.grounded
        && state.player_position.is_finite()
        && state.camera_query_succeeded
        && state.backend_identity_stable
        && walk.ocean_updates > 1
        && camera_moves
        && water_in_sync
        && feedback_valid
        && cloud_valid
        && wind_valid
        && shadows.valid()
        && smoke.route.valid()
        && grass.0.navigation_valid(smoke.route.enabled)
        && grass.1.valid()
        && grass.2.report().valid()
        && smoke.fft_samples[0].1 != smoke.fft_samples[1].1
        && coupled.changed();
    let frame_stats = frame_statistics(&smoke.frame_intervals);
    let water_time_scale = ocean_clock.snapshot().time_scale;
    let cadence_pass = water_time_scale.to_bits() == super::WATER_TIME_SCALE.to_bits()
        && cadence_valid(
            simulation_rate,
            super::WATER_TIME_SCALE,
            &frame_stats,
            smoke.throttle_30hz,
        )
        && shadow_rate.valid();
    let source_fish_requested = probe
        .fish_stand_content
        .as_ref()
        .is_some_and(|content| content.source.is_some());
    let source_fish_pass =
        !source_fish_requested || source_fish.as_ref().is_some_and(|receipt| receipt.valid());
    let pass = functional_pass && cadence_pass && source_fish_pass;
    let mut receipt = serde_json::json!({"pass":pass,"ticks":smoke.ticks,"grounded_ticks":smoke.grounded,
        "functional_pass":functional_pass,"cadence_pass":cadence_pass,
        "window_pacing":"Winit continuous in both focus states; AutoVsync; 1920x1080",
        "test_throttle_30hz":smoke.throttle_30hz,
        "start":start.to_array(),"end":state.player_position.to_array(),"distance_m":displacement,
        "camera_query":state.camera_query_succeeded,"backend_stable":state.backend_identity_stable,
        "ocean_updates":walk.ocean_updates,"ocean_nodes":walk.ocean_nodes.len(),
        "fft_readbacks":smoke.fft_samples,
        "shore_spray_readbacks":coupled.samples,
        "shore_spray_finite":coupled.finite,
        "spray_encoded_camera_samples":camera_samples,
        "water_material_step":water.0.step,"water_material_frames":water_frames,
        "water_material_time":water.0.time,"water_material_dt":water.0.dt,
        "live_feedback":feedback,"live_observed_steps_per_second":observed_rate,
        "live_observed_simulation_seconds_per_second":simulation_rate,
        "timing_start":smoke.live_start,"timing_end":smoke.live_end,
        "display_frame_intervals":frame_stats,
        "clouds": &*cloud.1, "cloud_readbacks":cloud.0.samples, "cloud_finite":cloud.0.finite,
        "wind": &*wind.1, "wind_readbacks":wind.0.samples, "wind_depth_finite":wind.0.finite,
        "wind_changed_depth_texels":wind.0.changed_texels,
        "cascade_navigation_pass":shadows.valid(),"cascade_navigation_readbacks":shadows.samples,
        "shadow_cadence_pass":shadow_rate.valid(),"shadow_cadence":&*shadow_rate,
        "shadow_pass_ratios":shadow_rate.rates(),
        "scope":"controller commands, full native scene; OS keyboard and building traversal not qualified"});
    // Keep this diagnostic outside the already large object macro: an extra
    // field there exceeds serde_json's default macro expansion recursion limit.
    receipt["plant_caster_selection"] = serde_json::json!(
        shadow_sources
            .as_ref()
            .and_then(|sources| sources.selection_observation())
    );
    receipt["water_time_scale"] = serde_json::json!(water_time_scale);
    receipt["render_diagnostics"] = render_diagnostics::report(&diagnostics);
    receipt["graphics_values"] = graphics.values_receipt();
    receipt["camera_motion_observation"] = serde_json::json!({
        "requested": smoke.camera_turn,
        "moving_frames": smoke.moving_camera_frames,
        "moving_frames_with_blur": smoke.moving_frames_with_blur,
        "moving_frames_with_taa": smoke.moving_frames_with_taa,
        "capture_requested": smoke.turn_capture_requested,
    });
    receipt["source_fish_requested"] = serde_json::json!(source_fish_requested);
    receipt["source_fish_preparation_pass"] = serde_json::json!(source_fish_pass);
    receipt["source_fish"] = serde_json::json!(source_fish.as_deref());
    receipt["navigation"] = serde_json::json!({
        "route":smoke.route,"pass":smoke.route.valid(),
        "grass_updates":grass.0.updates,"grass_samples":grass.0.samples,
        "grass_readbacks":grass.1.samples,"grass_gpu_pass":grass.1.valid(),
    });
    receipt["water_queries"] = serde_json::json!(grass.2.report());
    publish_result(&mut commands, probe.screenshot.clone(), &receipt, &mut exit);
}

fn publish_result(
    commands: &mut Commands,
    screenshot: std::path::PathBuf,
    receipt: &serde_json::Value,
    exit: &mut MessageWriter<AppExit>,
) {
    let result = write_receipt(&screenshot, receipt);
    if receipt["pass"].as_bool() != Some(true) || result.is_err() {
        error!(?receipt, ?result, "walk smoke failed");
        exit.write(AppExit::error());
        return;
    }
    println!(
        "TIDEWATER_WALK_PASS distance_m={} grounded_ticks={}",
        receipt["distance_m"], receipt["grounded_ticks"]
    );
    commands
        .spawn(Screenshot::primary_window())
        .observe(save_to_disk(screenshot))
        .observe(
            |_: On<ScreenshotCaptured>, mut exit: MessageWriter<AppExit>| {
                exit.write(AppExit::Success);
            },
        );
}

fn camera_moved(samples: Option<[(u32, [f32; 3]); 2]>) -> bool {
    samples.is_some_and(|[first, last]| {
        first.0 == 121
            && last.0 > first.0
            && Vec3::from_array(first.1).distance(Vec3::from_array(last.1)) > 3.0
    })
}

fn observed_rates(smoke: &Smoke) -> Option<(f64, f64)> {
    let start = smoke.live_start?;
    let end = smoke.live_end?;
    let elapsed = end.wall - start.wall;
    (elapsed > 0.0).then(|| {
        (
            f64::from(end.step - start.step) / elapsed,
            (end.simulation - start.simulation) / elapsed,
        )
    })
}

fn cadence_valid(
    rate: Option<f64>,
    expected_scale: f64,
    frames: &serde_json::Value,
    throttled: bool,
) -> bool {
    rate.is_some_and(|rate| (0.90..=1.05).contains(&(rate / expected_scale)))
        && frames["mean_fps"].as_f64().is_some_and(|fps| {
            if throttled {
                (20.0..=35.0).contains(&fps)
            } else {
                fps >= 60.0
            }
        })
}

fn write_receipt(path: &std::path::Path, receipt: &serde_json::Value) -> std::io::Result<()> {
    std::fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(path.with_extension("walk.json"))?
        .write_all(serde_json::to_string_pretty(receipt)?.as_bytes())
}

fn water_in_sync(clock: &crate::water_time::WaterTime, frames: &[Vec<UVec4>; 3]) -> bool {
    #[allow(clippy::cast_possible_truncation)]
    let expected_time = (clock.time as f32).to_bits();
    clock.step > 120
        && frames.iter().all(|layer| {
            !layer.is_empty()
                && layer.iter().all(|frame| {
                    frame.x == clock.step
                        && frame.y == expected_time
                        && frame.z == clock.dt.to_bits()
                })
        })
}

#[allow(clippy::cast_precision_loss)] // At most 100000 finite frame intervals in this bounded probe.
fn frame_statistics(intervals: &[f64]) -> serde_json::Value {
    let mut sorted: Vec<_> = intervals
        .iter()
        .copied()
        .filter(|v| v.is_finite() && *v > 0.0)
        .collect();
    if sorted.is_empty() {
        return serde_json::Value::Null;
    }
    sorted.sort_by(f64::total_cmp);
    let count = sorted.len();
    let slow_count = count.div_ceil(100);
    serde_json::json!({
        "samples":count,
        "mean_fps": count as f64 / sorted.iter().sum::<f64>(),
        "p95_ms": sorted[(count * 95).div_ceil(100) - 1] * 1000.0,
        "one_percent_low_fps": slow_count as f64 / sorted[count-slow_count..].iter().sum::<f64>(),
        "scope":"main frame intervals after one second of live feedback; fixed native 1920x1080 walk, not GPU timestamp measurements"
    })
}
