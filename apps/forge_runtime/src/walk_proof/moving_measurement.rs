//! Complete live input-replay interval, not a stationary or frozen benchmark.
use super::{Replay, cpu_phases, render_samples::RenderSamples};
use bevy::{
    app::AppExit,
    diagnostic::{DiagnosticsStore, FrameCount},
    prelude::*,
};
use forge_engine::PlayerRuntimeSnapshot;

#[derive(Default, Resource)]
struct Measurement {
    started: bool,
    done: bool,
    samples_ms: Vec<f64>,
    poses: Vec<serde_json::Value>,
    render_samples: RenderSamples,
}

pub fn install(app: &mut App) {
    cpu_phases::install(app);
    app.init_resource::<Measurement>()
        .add_systems(Last, measure);
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    reason = "Bevy proof parameters"
)]
fn measure(
    replay: Res<Replay>,
    player: Res<PlayerRuntimeSnapshot>,
    mut state: ResMut<Measurement>,
    time: Res<Time<Real>>,
    virtual_time: Res<Time<Virtual>>,
    frame: Res<FrameCount>,
    diagnostics: Option<Res<DiagnosticsStore>>,
    cpu: Res<cpu_phases::CpuPhases>,
    windows: Query<&Window>,
    shadows: Option<Res<forge_engine::VisualValleyShadowStats>>,
    water: Option<Res<forge_engine::WaterProjectionStats>>,
    mut exit: MessageWriter<AppExit>,
) {
    if state.done || replay.start.is_none() {
        return;
    }
    if virtual_time.is_paused() || !player.player_position.is_finite() {
        fail(
            &mut state,
            &mut exit,
            "paused simulation or non-finite position",
        );
        return;
    }
    if replay.tick > 0 && replay.tick <= replay.config.ticks {
        // The first observed moving frame can include preparation: exclude its delta.
        if !state.started {
            state.started = true;
            return;
        }
        if state.samples_ms.len() >= 100_000 {
            fail(&mut state, &mut exit, "sample cap reached");
            return;
        }
        state.samples_ms.push(time.delta_secs_f64() * 1000.0);
        state.poses.push(serde_json::json!({
            "render_frame": frame.0, "replay_tick": replay.tick,
            "player_fixed_tick": player.fixed_tick,
            "position_m": player.player_position.to_array(), "grounded": player.grounded,
            "virtual_elapsed_s": virtual_time.elapsed_secs_f64(),
            "camera_yaw_radians": player.camera_yaw_radians,
            "camera_pitch_radians": player.camera_pitch_radians,
            "camera_boom_distance_m": player.camera_boom_distance_m,
            "cpu_wall_phases_ms": cpu.latest,
        }));
        state
            .render_samples
            .record(replay.tick, frame.0, diagnostics.as_deref());
        return;
    }
    if !replay.reported || replay.tick != replay.config.ticks + 120 {
        return;
    }
    let Ok(distribution) = forge_core::FrameDistribution::from_samples_ms(&state.samples_ms) else {
        fail(&mut state, &mut exit, "invalid or empty sample interval");
        return;
    };
    let window: Vec<_> = windows
        .iter()
        .map(|w| {
            serde_json::json!({
                "physical_width": w.physical_width(), "physical_height": w.physical_height(),
            })
        })
        .collect();
    let report = serde_json::json!({
        "schema": "forge.walk-moving-measurement", "schema_version": 1,
        "scope": "live_input_replay_excluding_first_render_delta_and_braking",
        "virtual_time_frozen": false,
        "requested_ticks": replay.config.ticks, "axis": replay.config.axis.to_array(),
        "start_position_m": replay.start.map(|position| position.to_array()),
        "settled_position_m": player.player_position.to_array(),
        "settled_grounded": player.grounded, "window": window,
        "frame": distribution, "samples_ms": state.samples_ms, "poses": state.poses,
        "shadow_projection": shadows.as_deref(),
        "water_projection": water.as_deref(),
        "render_diagnostic_snapshots": state.render_samples.samples,
        "cpu_wall_phase_names": cpu_phases::NAMES,
        "render_diagnostic_scope": "latest_async_pass_measurements_at_read_tick_not_atomic_gpu_frame_time; collection_overhead_included",
    });
    tracing::info!(event_kind = "walk_moving_measurement_completed", report = %report,
        "Compare actual pose/tick coverage before comparing profiles; not target GPU qualification");
    state.done = true;
    exit.write(AppExit::Success);
}

fn fail(state: &mut Measurement, exit: &mut MessageWriter<AppExit>, reason: &str) {
    state.done = true;
    tracing::error!(event_kind = "walk_moving_measurement_invalid", reason);
    exit.write(AppExit::from_code(1));
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::walk_proof::WalkProof;

    fn fixture() -> App {
        let mut app = App::new();
        app.insert_resource(Replay {
            config: WalkProof::parse("10:1:0").unwrap(),
            warmup: 120,
            tick: 0,
            start: None,
            reported: false,
        })
        .init_resource::<PlayerRuntimeSnapshot>()
        .init_resource::<Time<Real>>()
        .init_resource::<Time<Virtual>>()
        .init_resource::<FrameCount>()
        .add_message::<AppExit>();
        app.world_mut()
            .resource_mut::<Time<Real>>()
            .advance_by(std::time::Duration::from_millis(16));
        install(&mut app);
        app
    }

    #[test]
    fn measures_movement_but_not_preparation_first_delta_or_braking() {
        let mut app = fixture();
        app.world_mut().run_schedule(Last);
        assert!(!app.world().resource::<Measurement>().started);
        app.world_mut().resource_mut::<Replay>().start = Some(Vec3::ZERO);
        for tick in 1_u16..=130 {
            app.world_mut().resource_mut::<Replay>().tick = u32::from(tick);
            app.world_mut()
                .resource_mut::<PlayerRuntimeSnapshot>()
                .player_position
                .x = f32::from(tick);
            app.world_mut().run_schedule(Last);
        }
        assert_eq!(app.world().resource::<Measurement>().samples_ms.len(), 9);
        assert!(!app.world().resource::<Measurement>().done);
        app.world_mut().resource_mut::<Replay>().reported = true;
        app.world_mut().run_schedule(Last);
        assert!(app.world().resource::<Measurement>().done);
        assert_eq!(app.world().resource::<Measurement>().poses.len(), 9);
    }

    #[test]
    fn rejects_paused_or_empty_measurements() {
        for paused in [true, false] {
            let mut app = fixture();
            app.world_mut().resource_mut::<Replay>().start = Some(Vec3::ZERO);
            app.world_mut().resource_mut::<Replay>().tick = 130;
            app.world_mut().resource_mut::<Replay>().reported = true;
            if paused {
                app.world_mut().resource_mut::<Time<Virtual>>().pause();
            }
            app.world_mut().run_schedule(Last);
            assert!(app.world().resource::<Measurement>().done);
            assert!(app.world().resource::<Measurement>().samples_ms.is_empty());
            let mut cursor = bevy::ecs::message::MessageCursor::<AppExit>::default();
            assert!(
                cursor
                    .read(app.world().resource::<Messages<AppExit>>())
                    .any(|message| matches!(message, AppExit::Error(_)))
            );
        }
    }
}
