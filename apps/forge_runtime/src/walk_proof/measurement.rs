//! Measures a stationary post-replay view; never includes the walking frames.
use super::{Replay, cpu_phases};
use bevy::{
    app::AppExit,
    diagnostic::{DiagnosticsStore, FrameCount},
    prelude::*,
};
use forge_engine::PlayerRuntimeSnapshot;

#[derive(Default, Resource)]
struct Measurement {
    freeze_requested: bool,
    warmup: u32,
    position: Option<Vec3>,
    samples: Vec<f64>,
    cpu_samples: Vec<[f64; 5]>,
    done: bool,
}

pub fn install_measurement(app: &mut App, freeze_requested: bool) {
    cpu_phases::install(app);
    app.insert_resource(Measurement {
        freeze_requested,
        ..default()
    })
    .add_systems(Last, measure);
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    reason = "Bevy measurement parameters"
)]
fn measure(
    replay: Res<Replay>,
    player: Res<PlayerRuntimeSnapshot>,
    mut state: ResMut<Measurement>,
    time: Res<Time<Real>>,
    mut virtual_time: ResMut<Time<Virtual>>,
    frame: Res<FrameCount>,
    diagnostics: Res<DiagnosticsStore>,
    cpu: Res<cpu_phases::CpuPhases>,
    shadows: Option<Res<forge_engine::VisualValleyShadowStats>>,
    device: Option<Res<bevy::render::renderer::RenderDevice>>,
    windows: Query<&Window>,
    mut exit: MessageWriter<AppExit>,
) {
    if state.done || !replay.reported || replay.tick != replay.config.ticks + 120 {
        return;
    }
    if state.warmup < 120 {
        state.warmup += 1;
        return;
    }
    if state.freeze_requested && !virtual_time.is_paused() {
        virtual_time.pause();
        return;
    }
    let origin = *state.position.get_or_insert(player.player_position);
    if origin.distance(player.player_position) > 0.02 || player.horizontal_velocity.length() > 0.01
    {
        tracing::error!(
            event_kind = "walk_measurement_moved",
            origin = ?origin, current = ?player.player_position,
            velocity = ?player.horizontal_velocity, samples = state.samples.len(),
            "Stationary window invalidated"
        );
        state.done = true;
        exit.write(AppExit::from_code(1));
        return;
    }
    state.samples.push(time.delta_secs_f64() * 1000.0);
    if let Some(sample) = cpu.latest {
        state.cpu_samples.push(sample);
    }
    if state.samples.len() != 300 {
        return;
    }
    state.done = true;
    let distribution = match forge_core::FrameDistribution::from_samples_ms(&state.samples) {
        Ok(value) => value,
        Err(error) => {
            tracing::error!(event_kind = "walk_measurement_invalid", %error);
            exit.write(AppExit::from_code(1));
            return;
        }
    };
    let render: Vec<_> = diagnostics
        .iter()
        .filter(|d| d.path().as_str().starts_with("render/"))
        .filter_map(|d| {
            d.value().map(|value| {
                serde_json::json!({
                    "path": d.path().as_str(), "value": value, "suffix": d.suffix,
                })
            })
        })
        .collect();
    let window: Vec<_> = windows
        .iter()
        .map(|w| {
            serde_json::json!({
                "physical_width": w.physical_width(), "physical_height": w.physical_height(),
            })
        })
        .collect();
    let report = serde_json::json!({
        "schema": "forge.walk-stationary-measurement", "schema_version": 1,
        "virtual_time_frozen": virtual_time.is_paused(),
        "scope": if state.freeze_requested { "static_render_isolation_not_live_game_fps" } else { "stationary_player_live_simulation" },
        "warmup_render_frames_after_walk": 120, "last_render_frame": frame.0,
        "position_m": player.player_position.to_array(), "grounded": player.grounded,
        "camera_yaw_radians": player.camera_yaw_radians,
        "camera_pitch_radians": player.camera_pitch_radians,
        "camera_boom_distance_m": player.camera_boom_distance_m,
        "window": window, "frame": distribution, "samples_ms": state.samples,
        "cpu_wall_phase_names": cpu_phases::NAMES,
        "cpu_wall_phase_samples_ms": state.cpu_samples,
        "shadow_projection": shadows.as_deref(),
        "pipeline_statistics_query_enabled": device.as_ref().map(|d| d.features().contains(bevy::render::settings::WgpuFeatures::PIPELINE_STATISTICS_QUERY)),
        "timestamp_query_enabled": device.as_ref().map(|d| d.features().contains(bevy::render::settings::WgpuFeatures::TIMESTAMP_QUERY)),
        "last_render_diagnostics_not_total_gpu_time": render,
    });
    tracing::info!(event_kind = "walk_measurement_completed", report = %report,
        "Stationary samples after replay; not target-GPU or Studio qualification");
    exit.write(AppExit::Success);
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::walk_proof::WalkProof;

    #[test]
    fn excludes_movement_and_warmup_then_collects_exactly_300_samples() {
        let mut app = App::new();
        app.insert_resource(Replay {
            config: WalkProof::parse("1:0:1").unwrap(),
            warmup: 120,
            tick: 0,
            start: Some(Vec3::ZERO),
            reported: false,
        })
        .init_resource::<PlayerRuntimeSnapshot>()
        .init_resource::<Time<Real>>()
        .init_resource::<Time<Virtual>>()
        .init_resource::<FrameCount>()
        .init_resource::<DiagnosticsStore>()
        .add_message::<AppExit>();
        install_measurement(&mut app, false);
        for _ in 0..500 {
            app.world_mut().run_schedule(Last);
        }
        assert_eq!(app.world().resource::<Measurement>().warmup, 0);
        app.world_mut().resource_mut::<Replay>().reported = true;
        app.world_mut().resource_mut::<Replay>().tick = 121;
        app.world_mut()
            .resource_mut::<Time<Real>>()
            .advance_by(std::time::Duration::from_millis(16));
        for _ in 0..120 {
            app.world_mut().run_schedule(Last);
        }
        assert!(app.world().resource::<Measurement>().samples.is_empty());
        for _ in 0..300 {
            app.world_mut().run_schedule(Last);
        }
        assert_eq!(app.world().resource::<Measurement>().samples.len(), 300);
        assert!(app.world().resource::<Measurement>().done);
        app.world_mut().run_schedule(Last);
        assert_eq!(app.world().resource::<Measurement>().samples.len(), 300);
    }
}
