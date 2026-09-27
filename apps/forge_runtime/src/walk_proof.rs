//! Bounded input replay, not navigation, teleportation, or physical-keyboard qualification.
mod cpu_phases;
mod measurement;
mod moving_measurement;
mod render_samples;
mod shadow_policy;
use bevy::prelude::*;
use forge_engine::{
    GovernedAssetKitReadiness, PendingPlayerControlCommand, PlayerControlCommand,
    PlayerControllerSet, PlayerRuntimeSnapshot,
};
pub use measurement::install_measurement;
pub use moving_measurement::install as install_moving_measurement;
pub use shadow_policy::load as shadow_policy;

pub fn is_measure_flag(flag: &str) -> bool {
    matches!(
        flag,
        "--walk-proof-measure"
            | "--walk-proof-measure-frozen"
            | "--walk-proof-measure-moving"
            | "--no-pipeline-statistics"
    )
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct WalkProof {
    pub ticks: u32,
    axis: Vec2,
}

impl WalkProof {
    pub fn parse(value: &str) -> Result<Self, String> {
        let fields: Vec<_> = value.split(':').collect();
        if fields.len() != 3 {
            return Err("--walk-proof expects ticks:axis_x:axis_y".into());
        }
        let ticks = fields[0].parse::<u32>().map_err(|_| "invalid walk ticks")?;
        let x = fields[1].parse::<f32>().map_err(|_| "invalid walk X")?;
        let y = fields[2].parse::<f32>().map_err(|_| "invalid walk Y")?;
        if !(1..=3600).contains(&ticks)
            || !x.is_finite()
            || !y.is_finite()
            || x.abs() > 1.0
            || y.abs() > 1.0
            || (x == 0.0 && y == 0.0)
        {
            return Err(
                "walk proof requires 1..3600 ticks and finite nonzero axes in [-1,1]".into(),
            );
        }
        Ok(Self {
            ticks,
            axis: Vec2::new(x, y),
        })
    }
}

#[derive(Resource)]
struct Replay {
    config: WalkProof,
    warmup: u32,
    tick: u32,
    start: Option<Vec3>,
    reported: bool,
}

impl Plugin for WalkProof {
    fn build(&self, app: &mut App) {
        app.insert_resource(Replay {
            config: *self,
            warmup: 0,
            tick: 0,
            start: None,
            reported: false,
        })
        .add_systems(FixedUpdate, replay.before(PlayerControllerSet::Drive));
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn replay(
    mut state: ResMut<Replay>,
    mut pending: ResMut<PendingPlayerControlCommand>,
    player: Res<PlayerRuntimeSnapshot>,
    kit: Option<Res<GovernedAssetKitReadiness>>,
    warmup: Option<Res<forge_engine::ProfileWarmup>>,
) {
    if warmup
        .as_deref()
        .is_some_and(forge_engine::ProfileWarmup::is_warming)
    {
        pending.cancel_input();
        return;
    }
    if state.reported {
        return;
    }
    if state.start.is_none() {
        if kit.is_some_and(|kit| !kit.ready) || !player.grounded {
            state.warmup = 0;
            return;
        }
        state.warmup += 1;
        if state.warmup < 120 {
            return;
        }
        state.start = Some(player.player_position);
        tracing::info!(event_kind = "walk_proof_started", ticks = state.config.ticks,
            axis_x = state.config.axis.x, axis_y = state.config.axis.y,
            position = ?player.player_position, "Replay through normal controller; sources unchanged");
    }
    let moving = state.tick < state.config.ticks;
    let command = PlayerControlCommand {
        move_axis: if moving {
            state.config.axis
        } else {
            Vec2::ZERO
        },
        ..default()
    };
    if let Err(error) = pending.submit(command) {
        tracing::error!(event_kind = "walk_proof_input_failed", %error);
        state.reported = true;
        return;
    }
    state.tick += 1;
    // Allow braking and physics observation after the final move command.
    if state.tick == state.config.ticks + 120 {
        tracing::info!(event_kind = "walk_proof_completed", start = ?state.start,
            end = ?player.player_position, grounded = player.grounded,
            velocity = ?player.horizontal_velocity, fixed_tick = player.fixed_tick,
            "Input replay completed; no assertion of reaching a forest or keyboard qualification");
        state.reported = true;
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn waits_for_ground_then_finishes_without_editing_player_pose() {
        let mut app = App::new();
        app.init_resource::<PendingPlayerControlCommand>()
            .init_resource::<PlayerRuntimeSnapshot>()
            .add_plugins(WalkProof::parse("2:0:1").unwrap());
        for _ in 0..150 {
            app.world_mut().run_schedule(FixedUpdate);
        }
        assert!(app.world().resource::<Replay>().start.is_none());
        app.world_mut()
            .resource_mut::<PlayerRuntimeSnapshot>()
            .grounded = true;
        for _ in 0..241 {
            app.world_mut().run_schedule(FixedUpdate);
        }
        assert!(app.world().resource::<Replay>().reported);
        assert_eq!(
            app.world()
                .resource::<PlayerRuntimeSnapshot>()
                .player_position,
            Vec3::ZERO
        );
    }

    #[test]
    fn bounds_replay_and_rejects_invalid_axes() {
        for value in [
            "",
            "10:0",
            "0:1:0",
            "3601:1:0",
            "10:NaN:1",
            "10:inf:1",
            "10:2:0",
            "10:0:0",
            "10:1:0:extra",
        ] {
            assert!(WalkProof::parse(value).is_err(), "{value}");
        }
        let proof = WalkProof::parse("1400:-0.5:1").unwrap();
        assert_eq!(proof.ticks, 1400);
        assert_eq!(proof.axis, Vec2::new(-0.5, 1.0));
    }
}
