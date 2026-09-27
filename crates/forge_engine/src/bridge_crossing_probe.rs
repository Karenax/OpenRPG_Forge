mod recording;
mod temporal_trace;
pub use temporal_trace::BridgeTemporalLodStatus;

use crate::{
    ForgePlayerCharacter, PendingPlayerControlCommand, PlayerControlCommand, PlayerControllerSet,
    PlayerRuntimeBindingSnapshot, PlayerRuntimeBindings, PlayerRuntimeSnapshot,
    TerrainRuntimeBindingSnapshot, TerrainRuntimeBindings, TerrainRuntimeState,
    WorldSketchBridgeDeck,
};
use bevy::platform::time::Instant;
use bevy::{app::AppExit, prelude::*};
use bevy_rapier3d::{
    control::KinematicCharacterControllerOutput,
    prelude::{RapierColliderHandle, RapierContextEntityLink},
};
use forge_core::ContentHash;
use forge_project::{PlayerControllerProfileDocument, TerrainDocument};
use forge_world::{
    TerrainGenerationPlan, WorldSketchBridgePlacement, WorldSketchBridgePlacementDocument,
};
use recording::{BridgeRecording, recording_ready};
use std::path::PathBuf;
use tracing::{error, info};

const APPROACH_DISTANCE_M: f32 = 3.0;
const EXIT_DISTANCE_M: f32 = 3.5;
const SPAWN_CLEARANCE_M: f32 = 0.02;
const WARMUP_MIN_FIXED_TICKS: u64 = 8;
const WARMUP_MAX_FIXED_TICKS: u64 = 180;
const CROSSING_MAX_FIXED_TICKS: u32 = 900;
const STOP_MAX_FIXED_TICKS: u32 = 120;
const STOP_STABLE_FIXED_TICKS: u32 = 8;
const MAX_UNGROUNDED_RUN: u32 = 6;
const MIN_BRIDGE_TERRAIN_CLEARANCE_M: f32 = 0.5;
const PROGRESS_REGRESSION_TOLERANCE_M: f32 = 0.12;
const PROBE_TIMEOUT_SECONDS: u64 = 30;
const PREPARATION_TIMEOUT_SECONDS: u64 = 120;

#[derive(Clone, Resource)]
struct BridgeCrossingProbeConfiguration {
    exit_when_finished: bool,
    wait_for_profile_warmup: bool,
    profile_content_hash: ContentHash,
    terrain_content_hash: ContentHash,
    bridge_content_hash: ContentHash,
    terrain_plan: TerrainGenerationPlan,
    placement: WorldSketchBridgePlacement,
    spawn_position: Vec3,
    horizontal_forward: Vec3,
    move_axis: Vec2,
    capsule_half_height_m: f32,
    bridge_horizontal_length_m: f32,
    bridge_surface_length_m: f32,
    target_progress_m: f32,
}

pub struct BridgeCrossingProbePlugin {
    configuration: BridgeCrossingProbeConfiguration,
    recording_directory: Option<PathBuf>,
    temporal_lod_probe: bool,
}

impl BridgeCrossingProbePlugin {
    pub fn new(
        terrain: &TerrainDocument,
        bridge_document: &WorldSketchBridgePlacementDocument,
        profile: &PlayerControllerProfileDocument,
    ) -> Result<Self, String> {
        bridge_document
            .validate()
            .map_err(|error| format!("invalid bridge-crossing placements: {error}"))?;
        let placement = bridge_document
            .placements
            .first()
            .cloned()
            .ok_or_else(|| "bridge-crossing probe requires one bridge placement".to_owned())?;
        profile
            .validate()
            .map_err(|error| format!("invalid bridge-crossing player profile: {error}"))?;
        let terrain_content_hash = terrain
            .content_hash()
            .map_err(|error| format!("cannot hash bridge-crossing terrain: {error}"))?;
        if terrain_content_hash != bridge_document.source_carved_terrain_sha256 {
            return Err(format!(
                "bridge-crossing probe requires terrain {}, loaded terrain is {terrain_content_hash}",
                bridge_document.source_carved_terrain_sha256
            ));
        }
        let profile_content_hash = profile
            .content_hash()
            .map_err(|error| format!("cannot hash bridge-crossing player profile: {error}"))?;
        let bridge_content_hash = bridge_document
            .content_hash()
            .map_err(|error| format!("cannot hash bridge-crossing placements: {error}"))?;
        let terrain_plan = TerrainGenerationPlan::from_document(terrain)
            .map_err(|error| format!("cannot plan bridge-crossing terrain: {error}"))?;
        let start = array_to_vec3(placement.deck_start_surface_m);
        let end = array_to_vec3(placement.deck_end_surface_m);
        let horizontal_delta = Vec3::new(end.x - start.x, 0.0, end.z - start.z);
        let bridge_horizontal_length_m = horizontal_delta.length();
        if !bridge_horizontal_length_m.is_finite() || bridge_horizontal_length_m <= 1.0 {
            return Err("bridge-crossing placement has a degenerate horizontal span".to_owned());
        }
        let bridge_surface_length_m = start.distance(end);
        let horizontal_forward = horizontal_delta / bridge_horizontal_length_m;
        let spawn_xz = start - horizontal_forward * APPROACH_DISTANCE_M;
        let spawn_surface_y = terrain_plan
            .height_at(spawn_xz.x, spawn_xz.z)
            .map_err(|error| format!("cannot sample bridge-crossing spawn: {error}"))?;
        let capsule_half_height_m =
            profile.controller.capsule_radius_m + profile.controller.capsule_half_segment_m;
        let spawn_position = Vec3::new(
            spawn_xz.x,
            spawn_surface_y + capsule_half_height_m + SPAWN_CLEARANCE_M,
            spawn_xz.z,
        );
        // The default camera yaw is zero: axis X maps to world +X and axis Y to world -Z.
        let move_axis = Vec2::new(horizontal_forward.x, -horizontal_forward.z);

        Ok(Self {
            recording_directory: None,
            temporal_lod_probe: false,
            configuration: BridgeCrossingProbeConfiguration {
                exit_when_finished: true,
                wait_for_profile_warmup: false,
                profile_content_hash,
                terrain_content_hash,
                bridge_content_hash,
                terrain_plan,
                placement,
                spawn_position,
                horizontal_forward,
                move_axis,
                capsule_half_height_m,
                bridge_horizontal_length_m,
                bridge_surface_length_m,
                target_progress_m: APPROACH_DISTANCE_M
                    + bridge_horizontal_length_m
                    + EXIT_DISTANCE_M,
            },
        })
    }

    #[must_use]
    pub fn spawn_position(&self) -> Vec3 {
        self.configuration.spawn_position
    }

    /// Automated viewport proof: keep the scene open, with the camera facing the bridge.
    #[must_use]
    pub fn in_viewport(mut self) -> Self {
        self.configuration.exit_when_finished = false;
        self.configuration.move_axis = Vec2::Y;
        self
    }

    /// Opt-in temporal LOD observation: pause the walk for 600 ms after a fade
    /// starts, then resume. This changes the proof itinerary, never authored data.
    #[must_use]
    pub const fn with_temporal_lod_probe(mut self, enabled: bool) -> Self {
        self.temporal_lod_probe = enabled;
        self
    }

    /// Distinct prewarmed qualification: never pause a crossing already started.
    #[must_use]
    pub fn after_profile_warmup(mut self) -> Self {
        self.configuration.wait_for_profile_warmup = true;
        self
    }

    #[must_use]
    pub fn camera_target(&self) -> Vec3 {
        self.configuration.spawn_position + self.configuration.horizontal_forward
    }

    pub fn with_recording_directory(mut self, directory: Option<PathBuf>) -> Result<Self, String> {
        if let Some(path) = &directory {
            // Atomic creation refuses an existing directory, including symlinks. Never overwrite frames.
            std::fs::create_dir(path).map_err(|error| {
                format!(
                    "cannot create fresh bridge frame directory {}: {error}",
                    path.display()
                )
            })?;
            // The recording camera faces along the bridge instead of the legacy probe's world -Z.
            self.configuration.move_axis = Vec2::Y;
        }
        self.recording_directory = directory;
        Ok(self)
    }

    #[must_use]
    pub fn recording_camera_target(&self) -> Option<Vec3> {
        self.recording_directory
            .as_ref()
            .map(|_| self.configuration.spawn_position + self.configuration.horizontal_forward)
    }
}

impl Plugin for BridgeCrossingProbePlugin {
    fn build(&self, app: &mut App) {
        if self.temporal_lod_probe {
            temporal_trace::install(app);
        }
        if !self.configuration.exit_when_finished
            && !app.is_plugin_added::<crate::capture_readiness::CaptureReadinessPlugin>()
        {
            app.add_plugins(crate::capture_readiness::CaptureReadinessPlugin);
        }
        if let Some(directory) = &self.recording_directory {
            recording::install(app, directory.clone());
        }
        app.insert_resource(self.configuration.clone())
            .insert_resource(BridgeCrossingProbeState::new())
            .init_resource::<BridgeCrossingProbeStatus>()
            .init_resource::<BridgeRenderPrepared>()
            .add_systems(
                PreUpdate,
                viewport_render_prepared.pipe(store_render_prepared),
            )
            .add_systems(Last, publish_probe_status)
            .add_systems(
                FixedUpdate,
                drive_bridge_crossing_probe
                    .before(PlayerControllerSet::Drive)
                    .run_if(recording_ready)
                    .run_if(render_prepared),
            )
            .add_systems(
                FixedUpdate,
                observe_bridge_crossing_probe
                    .after(PlayerControllerSet::Camera)
                    .run_if(recording_ready)
                    .run_if(render_prepared),
            )
            .add_systems(Update, watchdog_bridge_crossing_probe);
    }
}

/// Cold WebGPU compilation belongs to preparation, not to the movement deadline.
/// Once crossing starts, never suspend its clock or checks for a quality change.
#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy run condition parameters"
)]
fn viewport_render_prepared(
    configuration: Res<BridgeCrossingProbeConfiguration>,
    probe: Res<BridgeCrossingProbeState>,
    progress: Option<ResMut<crate::capture_readiness::CaptureProgress>>,
    warmup: Option<Res<crate::ProfileWarmup>>,
) -> bool {
    if configuration.exit_when_finished || probe.phase != BridgeCrossingProbePhase::WarmingUp {
        return true;
    }
    if configuration.wait_for_profile_warmup
        && !warmup.is_some_and(|state| {
            state.phase == crate::ProfileWarmupPhase::Ready && state.completed.len() == 5
        })
    {
        return false;
    }
    progress.is_some_and(|mut progress| progress.render_ready(true))
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn store_render_prepared(In(ready): In<bool>, mut prepared: ResMut<BridgeRenderPrepared>) {
    prepared.0 = ready;
}

#[derive(Resource, Default)]
struct BridgeRenderPrepared(bool);

#[allow(clippy::needless_pass_by_value, reason = "Bevy run condition")]
fn render_prepared(prepared: Res<BridgeRenderPrepared>) -> bool {
    prepared.0
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
enum BridgeCrossingProbePhase {
    WarmingUp,
    Crossing,
    Stopping,
    Completed,
    Failed,
}

impl BridgeCrossingProbePhase {
    const fn label(self) -> &'static str {
        match self {
            Self::WarmingUp => "warming_up",
            Self::Crossing => "crossing",
            Self::Stopping => "stopping",
            Self::Completed => "completed",
            Self::Failed => "failed",
        }
    }

    const fn is_terminal(self) -> bool {
        matches!(self, Self::Completed | Self::Failed)
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct BridgeBindingSnapshot {
    entity: Entity,
    placement_index: usize,
    rapier_collider_handle: (u32, u32),
    rapier_context_entity: Entity,
}

#[derive(Resource)]
struct BridgeCrossingProbeState {
    failure_reason: Option<String>,
    started_at: Instant,
    phase: BridgeCrossingProbePhase,
    fixed_ticks: u64,
    phase_ticks: u32,
    initial_player_bindings: Option<PlayerRuntimeBindingSnapshot>,
    initial_terrain_bindings: Option<Vec<TerrainRuntimeBindingSnapshot>>,
    initial_bridge_binding: Option<BridgeBindingSnapshot>,
    walk_sequence: Option<u64>,
    walk_sequence_applied: bool,
    stop_sequence: Option<u64>,
    stop_sequence_applied: bool,
    stop_stable_ticks: u32,
    last_progress_m: f32,
    maximum_progress_m: f32,
    crossing_observed_ticks: u32,
    crossing_grounded_ticks: u32,
    current_ungrounded_run: u32,
    maximum_ungrounded_run: u32,
    deck_collision_ticks: u32,
    deck_entry_progress_m: Option<f32>,
    deck_exit_progress_m: Option<f32>,
    maximum_terrain_clearance_m: f32,
    crossed_audit_center: bool,
}

impl BridgeCrossingProbeState {
    fn start_crossing(&mut self, now: Instant) {
        self.started_at = now;
        self.phase = BridgeCrossingProbePhase::Crossing;
        self.phase_ticks = 0;
    }

    fn watchdog_failure(&self, now: Instant) -> Option<&'static str> {
        if self.phase.is_terminal() {
            return None;
        }
        let (limit, reason) = if self.phase == BridgeCrossingProbePhase::WarmingUp {
            (
                PREPARATION_TIMEOUT_SECONDS,
                "bridge-crossing preparation exceeded its 120 second real-time watchdog",
            )
        } else {
            (
                PROBE_TIMEOUT_SECONDS,
                "bridge-crossing movement exceeded its 30 second real-time watchdog",
            )
        };
        (now.duration_since(self.started_at).as_secs() >= limit).then_some(reason)
    }

    fn new() -> Self {
        Self {
            failure_reason: None,
            started_at: Instant::now(),
            phase: BridgeCrossingProbePhase::WarmingUp,
            fixed_ticks: 0,
            phase_ticks: 0,
            initial_player_bindings: None,
            initial_terrain_bindings: None,
            initial_bridge_binding: None,
            walk_sequence: None,
            walk_sequence_applied: false,
            stop_sequence: None,
            stop_sequence_applied: false,
            stop_stable_ticks: 0,
            last_progress_m: 0.0,
            maximum_progress_m: 0.0,
            crossing_observed_ticks: 0,
            crossing_grounded_ticks: 0,
            current_ungrounded_run: 0,
            maximum_ungrounded_run: 0,
            deck_collision_ticks: 0,
            deck_entry_progress_m: None,
            deck_exit_progress_m: None,
            maximum_terrain_clearance_m: 0.0,
            crossed_audit_center: false,
        }
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn drive_bridge_crossing_probe(
    configuration: Res<BridgeCrossingProbeConfiguration>,
    mut pending: ResMut<PendingPlayerControlCommand>,
    mut probe: ResMut<BridgeCrossingProbeState>,
    mut exit: MessageWriter<AppExit>,
    mut terminal_stop_sent: Local<bool>,
) {
    if probe.phase.is_terminal() {
        if !configuration.exit_when_finished && !*terminal_stop_sent {
            *terminal_stop_sent = true;
            if let Err(error) = pending.submit(PlayerControlCommand::default()) {
                probe.failure_reason =
                    Some(format!("cannot stop terminal viewport probe: {error}"));
                probe.phase = BridgeCrossingProbePhase::Failed;
            }
        }
        return;
    }
    let result = match probe.phase {
        BridgeCrossingProbePhase::Crossing if probe.walk_sequence.is_none() => pending
            .submit(PlayerControlCommand {
                move_axis: configuration.move_axis,
                ..default()
            })
            .map(|sequence| probe.walk_sequence = Some(sequence))
            .map_err(|error| format!("cannot submit bridge-crossing walk command: {error}")),
        BridgeCrossingProbePhase::Stopping if probe.stop_sequence.is_none() => pending
            .submit(PlayerControlCommand::default())
            .map(|sequence| probe.stop_sequence = Some(sequence))
            .map_err(|error| format!("cannot submit bridge-crossing stop command: {error}")),
        _ => Ok(()),
    };
    if let Err(reason) = result {
        let phase = probe.phase;
        fail_probe(
            probe.fixed_ticks,
            phase,
            &reason,
            &mut exit,
            configuration.exit_when_finished,
        );
        probe.failure_reason = Some(reason);
        probe.phase = BridgeCrossingProbePhase::Failed;
    }
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    clippy::type_complexity,
    reason = "the runtime proof compares independent Forge, Bevy and Rapier projections"
)]
fn observe_bridge_crossing_probe(
    configuration: Res<BridgeCrossingProbeConfiguration>,
    terrain_runtime: Res<TerrainRuntimeState>,
    terrain_bindings: Res<TerrainRuntimeBindings>,
    player_bindings: Res<PlayerRuntimeBindings>,
    snapshot: Res<PlayerRuntimeSnapshot>,
    player_output: Query<&KinematicCharacterControllerOutput, With<ForgePlayerCharacter>>,
    bridge: Query<
        (
            Entity,
            &WorldSketchBridgeDeck,
            Option<&RapierColliderHandle>,
            Option<&RapierContextEntityLink>,
        ),
        With<WorldSketchBridgeDeck>,
    >,
    mut probe: ResMut<BridgeCrossingProbeState>,
    mut exit: MessageWriter<AppExit>,
    recording: Option<Res<BridgeRecording>>,
) {
    if probe.phase.is_terminal() {
        return;
    }
    probe.fixed_ticks = probe.fixed_ticks.saturating_add(1);
    probe.phase_ticks = probe.phase_ticks.saturating_add(1);
    let result = advance_probe(
        &configuration,
        &terrain_runtime,
        &terrain_bindings,
        &player_bindings,
        &snapshot,
        &player_output,
        &bridge,
        &mut probe,
    );
    match result {
        Ok(true) => {
            probe.phase = BridgeCrossingProbePhase::Completed;
            if recording.is_none() && configuration.exit_when_finished {
                exit.write(AppExit::Success);
            }
        }
        Ok(false) => {}
        Err(reason) => {
            let phase = probe.phase;
            fail_probe(
                probe.fixed_ticks,
                phase,
                &reason,
                &mut exit,
                configuration.exit_when_finished,
            );
            probe.failure_reason = Some(reason);
            probe.phase = BridgeCrossingProbePhase::Failed;
        }
    }
}

#[allow(clippy::too_many_arguments, clippy::type_complexity)]
fn advance_probe(
    configuration: &BridgeCrossingProbeConfiguration,
    terrain_runtime: &TerrainRuntimeState,
    terrain_bindings: &TerrainRuntimeBindings,
    player_bindings: &PlayerRuntimeBindings,
    snapshot: &PlayerRuntimeSnapshot,
    player_output: &Query<&KinematicCharacterControllerOutput, With<ForgePlayerCharacter>>,
    bridge: &Query<
        (
            Entity,
            &WorldSketchBridgeDeck,
            Option<&RapierColliderHandle>,
            Option<&RapierContextEntityLink>,
        ),
        With<WorldSketchBridgeDeck>,
    >,
    probe: &mut BridgeCrossingProbeState,
) -> Result<bool, String> {
    match probe.phase {
        BridgeCrossingProbePhase::WarmingUp => {
            advance_warmup(terrain_bindings, player_bindings, snapshot, bridge, probe)
        }
        BridgeCrossingProbePhase::Crossing => {
            advance_crossing(configuration, snapshot, player_output, probe)
        }
        BridgeCrossingProbePhase::Stopping => advance_stopping(
            configuration,
            terrain_runtime,
            terrain_bindings,
            player_bindings,
            snapshot,
            bridge,
            probe,
        ),
        BridgeCrossingProbePhase::Completed | BridgeCrossingProbePhase::Failed => Ok(false),
    }
}

#[allow(clippy::type_complexity)]
fn advance_warmup(
    terrain_bindings: &TerrainRuntimeBindings,
    player_bindings: &PlayerRuntimeBindings,
    snapshot: &PlayerRuntimeSnapshot,
    bridge: &Query<
        (
            Entity,
            &WorldSketchBridgeDeck,
            Option<&RapierColliderHandle>,
            Option<&RapierContextEntityLink>,
        ),
        With<WorldSketchBridgeDeck>,
    >,
    probe: &mut BridgeCrossingProbeState,
) -> Result<bool, String> {
    let player_binding = player_bindings.snapshot();
    let bridge_binding = bridge_binding_snapshot(bridge)?;
    let terrain_binding = terrain_bindings.snapshots().ok();
    let ready = probe.fixed_ticks >= WARMUP_MIN_FIXED_TICKS
        && snapshot.grounded
        && player_binding.is_some_and(|binding| {
            binding.rapier_collider_handle.is_some()
                && binding.rapier_context_entity.is_some()
                && binding.backend_identity_stable
        })
        && bridge_binding.is_some()
        && terrain_binding
            .as_ref()
            .is_some_and(|bindings| !bindings.is_empty());
    if !ready {
        if probe.fixed_ticks >= WARMUP_MAX_FIXED_TICKS {
            return Err(
                "bridge-crossing runtime bindings did not become grounded and stable".to_owned(),
            );
        }
        return Ok(false);
    }
    probe.initial_player_bindings = player_binding;
    probe.initial_bridge_binding = bridge_binding;
    probe.initial_terrain_bindings = terrain_binding;
    // Loading assets and establishing grounded Rapier bindings is not walking.
    // The movement deadline starts exactly once; stopping shares this same budget.
    probe.start_crossing(Instant::now());
    Ok(false)
}

fn advance_crossing(
    configuration: &BridgeCrossingProbeConfiguration,
    snapshot: &PlayerRuntimeSnapshot,
    player_output: &Query<&KinematicCharacterControllerOutput, With<ForgePlayerCharacter>>,
    probe: &mut BridgeCrossingProbeState,
) -> Result<bool, String> {
    let sequence = probe
        .walk_sequence
        .ok_or_else(|| "bridge crossing has no submitted walk command".to_owned())?;
    if !probe.walk_sequence_applied {
        if snapshot.last_applied_sequence != Some(sequence) {
            if snapshot
                .last_applied_sequence
                .is_some_and(|applied| applied > sequence)
            {
                return Err(format!(
                    "bridge walk sequence {sequence} was skipped by {:?}",
                    snapshot.last_applied_sequence
                ));
            }
            return Ok(false);
        }
        probe.walk_sequence_applied = true;
    }

    let progress_m = horizontal_progress(configuration, snapshot.player_position);
    if progress_m + PROGRESS_REGRESSION_TOLERANCE_M < probe.last_progress_m {
        return Err(format!(
            "bridge crossing regressed from {} to {progress_m} metres",
            probe.last_progress_m
        ));
    }
    probe.last_progress_m = progress_m;
    probe.maximum_progress_m = probe.maximum_progress_m.max(progress_m);
    probe.crossing_observed_ticks = probe.crossing_observed_ticks.saturating_add(1);
    if snapshot.grounded {
        probe.crossing_grounded_ticks = probe.crossing_grounded_ticks.saturating_add(1);
        probe.current_ungrounded_run = 0;
    } else {
        probe.current_ungrounded_run = probe.current_ungrounded_run.saturating_add(1);
        probe.maximum_ungrounded_run = probe
            .maximum_ungrounded_run
            .max(probe.current_ungrounded_run);
    }
    if probe.maximum_ungrounded_run > MAX_UNGROUNDED_RUN {
        return Err(format!(
            "player remained ungrounded for {} fixed ticks during bridge crossing",
            probe.maximum_ungrounded_run
        ));
    }

    let bridge_entity = probe
        .initial_bridge_binding
        .ok_or_else(|| "bridge binding disappeared from crossing state".to_owned())?
        .entity;
    let collides_with_deck = player_output.single().is_ok_and(|output| {
        output
            .collisions
            .iter()
            .any(|hit| hit.entity == bridge_entity)
    });
    if collides_with_deck {
        probe.deck_collision_ticks = probe.deck_collision_ticks.saturating_add(1);
        probe.deck_entry_progress_m.get_or_insert(progress_m);
        probe.deck_exit_progress_m = Some(progress_m);
    }

    let terrain_height = configuration
        .terrain_plan
        .height_at(snapshot.player_position.x, snapshot.player_position.z)
        .map_err(|error| format!("cannot sample terrain below crossing player: {error}"))?;
    let terrain_clearance_m =
        snapshot.player_position.y - configuration.capsule_half_height_m - terrain_height;
    probe.maximum_terrain_clearance_m = probe.maximum_terrain_clearance_m.max(terrain_clearance_m);
    let center = configuration.placement.crossing_center_m;
    let center_position = Vec3::new(center[0], 0.0, center[1]);
    let center_progress = horizontal_progress(configuration, center_position);
    probe.crossed_audit_center |= progress_m >= center_progress;

    if progress_m < configuration.target_progress_m {
        if probe.phase_ticks >= CROSSING_MAX_FIXED_TICKS {
            return Err(format!(
                "bridge crossing reached only {progress_m} of {} metres",
                configuration.target_progress_m
            ));
        }
        return Ok(false);
    }
    if probe.deck_collision_ticks == 0 || !probe.crossed_audit_center {
        return Err(
            "player reached the far bank without proving contact with the audited bridge deck"
                .to_owned(),
        );
    }
    if probe.maximum_terrain_clearance_m < MIN_BRIDGE_TERRAIN_CLEARANCE_M {
        return Err(format!(
            "bridge deck never lifted the player above carved terrain by {MIN_BRIDGE_TERRAIN_CLEARANCE_M} metres; observed {}",
            probe.maximum_terrain_clearance_m
        ));
    }
    probe.phase = BridgeCrossingProbePhase::Stopping;
    probe.phase_ticks = 0;
    Ok(false)
}

#[allow(clippy::too_many_arguments, clippy::type_complexity)]
fn advance_stopping(
    configuration: &BridgeCrossingProbeConfiguration,
    terrain_runtime: &TerrainRuntimeState,
    terrain_bindings: &TerrainRuntimeBindings,
    player_bindings: &PlayerRuntimeBindings,
    snapshot: &PlayerRuntimeSnapshot,
    bridge: &Query<
        (
            Entity,
            &WorldSketchBridgeDeck,
            Option<&RapierColliderHandle>,
            Option<&RapierContextEntityLink>,
        ),
        With<WorldSketchBridgeDeck>,
    >,
    probe: &mut BridgeCrossingProbeState,
) -> Result<bool, String> {
    let sequence = probe
        .stop_sequence
        .ok_or_else(|| "bridge crossing has no submitted stop command".to_owned())?;
    if !probe.stop_sequence_applied {
        if snapshot.last_applied_sequence != Some(sequence) {
            if probe.phase_ticks >= STOP_MAX_FIXED_TICKS {
                return Err("bridge-crossing stop command was not applied".to_owned());
            }
            return Ok(false);
        }
        probe.stop_sequence_applied = true;
    }
    if snapshot.grounded && snapshot.horizontal_velocity.length() <= 0.05 {
        probe.stop_stable_ticks = probe.stop_stable_ticks.saturating_add(1);
    } else {
        probe.stop_stable_ticks = 0;
    }
    if probe.stop_stable_ticks < STOP_STABLE_FIXED_TICKS {
        if probe.phase_ticks >= STOP_MAX_FIXED_TICKS {
            return Err("player did not settle on the far bank after bridge crossing".to_owned());
        }
        return Ok(false);
    }
    verify_final_bindings(
        configuration,
        terrain_runtime,
        terrain_bindings,
        player_bindings,
        snapshot,
        bridge,
        probe,
    )?;
    emit_completed_event(configuration, snapshot, probe);
    Ok(true)
}

#[allow(clippy::too_many_arguments, clippy::type_complexity)]
fn verify_final_bindings(
    configuration: &BridgeCrossingProbeConfiguration,
    terrain_runtime: &TerrainRuntimeState,
    terrain_bindings: &TerrainRuntimeBindings,
    player_bindings: &PlayerRuntimeBindings,
    snapshot: &PlayerRuntimeSnapshot,
    bridge: &Query<
        (
            Entity,
            &WorldSketchBridgeDeck,
            Option<&RapierColliderHandle>,
            Option<&RapierContextEntityLink>,
        ),
        With<WorldSketchBridgeDeck>,
    >,
    probe: &BridgeCrossingProbeState,
) -> Result<(), String> {
    let terrain_hash = terrain_runtime
        .document()
        .content_hash()
        .map_err(|error| format!("cannot hash final bridge-crossing terrain: {error}"))?;
    if terrain_hash != configuration.terrain_content_hash {
        return Err(format!(
            "terrain changed during bridge crossing: expected {}, got {terrain_hash}",
            configuration.terrain_content_hash
        ));
    }
    if snapshot.profile_content_hash != Some(configuration.profile_content_hash) {
        return Err("player profile changed during bridge crossing".to_owned());
    }
    let final_player = player_bindings
        .snapshot()
        .ok_or_else(|| "player binding disappeared after bridge crossing".to_owned())?;
    if Some(final_player) != probe.initial_player_bindings
        || !final_player.backend_identity_stable
        || !snapshot.backend_identity_stable
    {
        return Err("player Bevy/Rapier identities changed during bridge crossing".to_owned());
    }
    let final_terrain = terrain_bindings
        .snapshots()
        .map_err(|error| format!("cannot snapshot final bridge terrain bindings: {error}"))?;
    if Some(final_terrain) != probe.initial_terrain_bindings {
        return Err("terrain mesh/collider identities changed during bridge crossing".to_owned());
    }
    let final_bridge = bridge_binding_snapshot(bridge)?
        .ok_or_else(|| "bridge collider disappeared after crossing".to_owned())?;
    if Some(final_bridge) != probe.initial_bridge_binding {
        return Err("bridge entity or Rapier collider identity changed during crossing".to_owned());
    }
    if !snapshot.grounded || probe.crossing_grounded_ticks * 10 < probe.crossing_observed_ticks * 9
    {
        return Err(format!(
            "bridge crossing was not grounded for at least 90 percent of samples: {}/{}",
            probe.crossing_grounded_ticks, probe.crossing_observed_ticks
        ));
    }
    Ok(())
}

#[allow(clippy::type_complexity)]
fn bridge_binding_snapshot(
    bridge: &Query<
        (
            Entity,
            &WorldSketchBridgeDeck,
            Option<&RapierColliderHandle>,
            Option<&RapierContextEntityLink>,
        ),
        With<WorldSketchBridgeDeck>,
    >,
) -> Result<Option<BridgeBindingSnapshot>, String> {
    let mut matches = bridge.iter();
    let Some((entity, marker, collider, context)) = matches.next() else {
        return Ok(None);
    };
    if matches.next().is_some() {
        return Err("bridge-crossing probe currently requires exactly one bridge deck".to_owned());
    }
    let Some(collider) = collider else {
        return Ok(None);
    };
    let Some(context) = context else {
        return Ok(None);
    };
    Ok(Some(BridgeBindingSnapshot {
        entity,
        placement_index: marker.placement_index,
        rapier_collider_handle: collider.0.into_raw_parts(),
        rapier_context_entity: context.0,
    }))
}

fn horizontal_progress(configuration: &BridgeCrossingProbeConfiguration, position: Vec3) -> f32 {
    let delta = Vec3::new(
        position.x - configuration.spawn_position.x,
        0.0,
        position.z - configuration.spawn_position.z,
    );
    delta.dot(configuration.horizontal_forward)
}

fn emit_completed_event(
    configuration: &BridgeCrossingProbeConfiguration,
    snapshot: &PlayerRuntimeSnapshot,
    probe: &BridgeCrossingProbeState,
) {
    info!(
        target: "forge::bridge",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "bridge_crossing_probe_completed",
        success = true,
        fixed_ticks = probe.fixed_ticks,
        terrain_document_hash = %configuration.terrain_content_hash,
        bridge_placements_hash = %configuration.bridge_content_hash,
        player_profile_hash = %configuration.profile_content_hash,
        landmark_id = configuration.placement.landmark_id,
        road_feature_id = configuration.placement.road_feature_id,
        water_feature_id = configuration.placement.water_feature_id,
        bridge_horizontal_length_m = f64::from(configuration.bridge_horizontal_length_m),
        bridge_surface_length_m = f64::from(configuration.bridge_surface_length_m),
        bridge_slope_degrees = f64::from(configuration.placement.slope_degrees),
        target_progress_m = f64::from(configuration.target_progress_m),
        achieved_progress_m = f64::from(probe.maximum_progress_m),
        crossing_fixed_ticks = probe.crossing_observed_ticks,
        grounded_fixed_ticks = probe.crossing_grounded_ticks,
        maximum_ungrounded_run = probe.maximum_ungrounded_run,
        deck_collision_ticks = probe.deck_collision_ticks,
        deck_entry_progress_m = f64::from(probe.deck_entry_progress_m.unwrap_or_default()),
        deck_exit_progress_m = f64::from(probe.deck_exit_progress_m.unwrap_or_default()),
        maximum_terrain_clearance_m = f64::from(probe.maximum_terrain_clearance_m),
        crossed_audit_center = probe.crossed_audit_center,
        terrain_bindings_preserved = true,
        player_bindings_preserved = true,
        bridge_binding_preserved = true,
        final_grounded = snapshot.grounded,
        navigation_provider_status = "not_selected",
        navigation_qualified = false,
        "actual Forge player crossed the authored road-water link on the materialized Rapier bridge collider"
    );
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameter")]
fn watchdog_bridge_crossing_probe(
    configuration: Res<BridgeCrossingProbeConfiguration>,
    mut probe: ResMut<BridgeCrossingProbeState>,
    mut exit: MessageWriter<AppExit>,
    recording: Option<Res<BridgeRecording>>,
) {
    if recording.is_some() {
        return;
    }
    let Some(reason) = probe.watchdog_failure(Instant::now()) else {
        return;
    };
    let phase = probe.phase;
    fail_probe(
        probe.fixed_ticks,
        phase,
        reason,
        &mut exit,
        configuration.exit_when_finished,
    );
    probe.failure_reason = Some(reason.to_owned());
    probe.phase = BridgeCrossingProbePhase::Failed;
}

fn fail_probe(
    fixed_tick: u64,
    phase: BridgeCrossingProbePhase,
    reason: &str,
    exit: &mut MessageWriter<AppExit>,
    exit_when_finished: bool,
) {
    error!(
        target: "forge::bridge",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "bridge_crossing_probe_failed",
        success = false,
        fixed_tick,
        phase = phase.label(),
        error = reason,
        navigation_provider_status = "not_selected",
        navigation_qualified = false,
        "bridge-crossing probe failed"
    );
    if exit_when_finished {
        exit.write(AppExit::error());
    }
}

/// Derived proof result only, never a persistent world document or human acceptance.
#[derive(Clone, Debug, Default, PartialEq, Resource, serde::Serialize)]
pub struct BridgeCrossingProbeStatus {
    pub phase: &'static str,
    pub crossing_ticks: u32,
    pub grounded_ticks: u32,
    pub deck_contacts: u32,
    pub progress_m: f32,
    pub failure_reason: Option<String>,
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn publish_probe_status(
    probe: Res<BridgeCrossingProbeState>,
    mut status: ResMut<BridgeCrossingProbeStatus>,
) {
    let observed = BridgeCrossingProbeStatus {
        phase: probe.phase.label(),
        crossing_ticks: probe.crossing_observed_ticks,
        grounded_ticks: probe.crossing_grounded_ticks,
        deck_contacts: probe.deck_collision_ticks,
        progress_m: probe.maximum_progress_m,
        failure_reason: probe.failure_reason.clone(),
    };
    if *status != observed {
        *status = observed;
    }
}

const fn array_to_vec3(value: [f32; 3]) -> Vec3 {
    Vec3::new(value[0], value[1], value[2])
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_world::{
        compile_world_sketch, embedded_visual_valley_player_controller_profile,
        embedded_visual_valley_world_sketch,
    };

    #[test]
    fn viewport_waits_for_fresh_render_samples_only_before_crossing() {
        use crate::capture_readiness::CaptureProgress;
        use bevy::ecs::system::RunSystemOnce;
        let compiled =
            compile_world_sketch(&embedded_visual_valley_world_sketch().unwrap()).unwrap();
        let profile = embedded_visual_valley_player_controller_profile().unwrap();
        let plugin = BridgeCrossingProbePlugin::new(
            &compiled.carved_terrain,
            &compiled.bridge_placements,
            &profile,
        )
        .unwrap()
        .in_viewport();
        let mut world = World::new();
        world.insert_resource(plugin.configuration);
        world.insert_resource(BridgeCrossingProbeState::new());
        assert!(!world.run_system_once(viewport_render_prepared).unwrap());
        world.init_resource::<CaptureProgress>();
        for _ in 0..20 {
            assert!(!world.run_system_once(viewport_render_prepared).unwrap());
        }
        for _ in 0..7 {
            world
                .resource_mut::<CaptureProgress>()
                .set_test_pipeline_sample(12, 0, 0);
            assert!(!world.run_system_once(viewport_render_prepared).unwrap());
        }
        // Rechecking a single render observation does not count as another GPU frame.
        assert!(!world.run_system_once(viewport_render_prepared).unwrap());
        world
            .resource_mut::<CaptureProgress>()
            .set_test_pipeline_sample(12, 0, 0);
        assert!(world.run_system_once(viewport_render_prepared).unwrap());
        world
            .resource_mut::<BridgeCrossingProbeConfiguration>()
            .wait_for_profile_warmup = true;
        assert!(!world.run_system_once(viewport_render_prepared).unwrap());
        world.init_resource::<crate::ProfileWarmup>();
        for phase in [
            crate::ProfileWarmupPhase::Idle,
            crate::ProfileWarmupPhase::Warming,
            crate::ProfileWarmupPhase::Failed,
            crate::ProfileWarmupPhase::Cancelled,
        ] {
            world.resource_mut::<crate::ProfileWarmup>().phase = phase;
            assert!(!world.run_system_once(viewport_render_prepared).unwrap());
        }
        world.resource_mut::<crate::ProfileWarmup>().phase = crate::ProfileWarmupPhase::Ready;
        assert!(!world.run_system_once(viewport_render_prepared).unwrap());
        world.resource_mut::<crate::ProfileWarmup>().completed = [
            forge_project::VisualQualityTier::Low,
            forge_project::VisualQualityTier::Medium,
            forge_project::VisualQualityTier::High,
            forge_project::VisualQualityTier::Ultra,
            forge_project::VisualQualityTier::High,
        ]
        .into_iter()
        .map(|tier| crate::PreparedQualityVariant {
            tier,
            pipelines: 12,
            elapsed_seconds: 1.0,
        })
        .collect();
        assert!(world.run_system_once(viewport_render_prepared).unwrap());
        world
            .resource_mut::<CaptureProgress>()
            .set_test_pipeline_sample(13, 1, 0);
        assert!(!world.run_system_once(viewport_render_prepared).unwrap());
        world
            .resource_mut::<BridgeCrossingProbeState>()
            .start_crossing(Instant::now());
        world.resource_mut::<crate::ProfileWarmup>().phase = crate::ProfileWarmupPhase::Failed;
        assert!(world.run_system_once(viewport_render_prepared).unwrap());
        world.remove_resource::<CaptureProgress>();
        assert!(world.run_system_once(viewport_render_prepared).unwrap());
    }

    #[test]
    fn cold_preparation_does_not_consume_the_movement_deadline() {
        use std::time::Duration;
        let mut probe = BridgeCrossingProbeState::new();
        let born = probe.started_at;
        assert!(
            probe
                .watchdog_failure(born + Duration::from_secs(31))
                .is_none()
        );
        assert!(
            probe
                .watchdog_failure(born + Duration::from_secs(120))
                .is_some()
        );
        let ready = born + Duration::from_secs(45);
        probe.start_crossing(ready);
        assert!(
            probe
                .watchdog_failure(ready + Duration::from_secs(29))
                .is_none()
        );
        assert!(
            probe
                .watchdog_failure(ready + Duration::from_secs(30))
                .is_some()
        );
        probe.phase = BridgeCrossingProbePhase::Stopping;
        assert!(
            probe
                .watchdog_failure(ready + Duration::from_secs(30))
                .is_some()
        );
        probe.phase = BridgeCrossingProbePhase::Completed;
        assert!(
            probe
                .watchdog_failure(ready + Duration::from_secs(150))
                .is_none()
        );
    }

    #[test]
    fn recording_refuses_an_existing_output_directory() {
        let compiled =
            compile_world_sketch(&embedded_visual_valley_world_sketch().unwrap()).unwrap();
        let profile = embedded_visual_valley_player_controller_profile().unwrap();
        let probe = BridgeCrossingProbePlugin::new(
            &compiled.carved_terrain,
            &compiled.bridge_placements,
            &profile,
        )
        .unwrap();
        assert!(
            probe
                .with_recording_directory(Some(std::env::temp_dir()))
                .is_err()
        );
    }

    #[test]
    fn configuration_starts_before_the_deck_and_maps_axis_to_world_direction() {
        let compiled =
            compile_world_sketch(&embedded_visual_valley_world_sketch().unwrap()).unwrap();
        let profile = embedded_visual_valley_player_controller_profile().unwrap();
        let probe = BridgeCrossingProbePlugin::new(
            &compiled.carved_terrain,
            &compiled.bridge_placements,
            &profile,
        )
        .unwrap();
        let configuration = &probe.configuration;
        let start = array_to_vec3(configuration.placement.deck_start_surface_m);
        let start_progress = horizontal_progress(configuration, start);
        let mapped_world_direction =
            Vec3::new(configuration.move_axis.x, 0.0, -configuration.move_axis.y);

        assert!((start_progress - APPROACH_DISTANCE_M).abs() < 1.0e-4);
        assert!(mapped_world_direction.abs_diff_eq(configuration.horizontal_forward, 1.0e-5));
        assert!(configuration.target_progress_m > configuration.bridge_horizontal_length_m);
        assert!(configuration.spawn_position.is_finite());
        assert!(configuration.exit_when_finished);
        let viewport = probe.in_viewport();
        assert!(!viewport.configuration.exit_when_finished);
        assert_eq!(viewport.configuration.move_axis, Vec2::Y);
        assert!(
            (viewport.camera_target() - viewport.spawn_position())
                .abs_diff_eq(viewport.configuration.horizontal_forward, 1.0e-5)
        );
    }
}
