//! Native walk/wade/swim/dive/return exercise through real controller commands.
use bevy::{
    app::AppExit,
    prelude::*,
    render::view::screenshot::{Screenshot, ScreenshotCaptured, save_to_disk},
};
use std::{io::Write, path::PathBuf, sync::atomic::Ordering};
use {
    forge_engine::{
        ForgePlayerCamera, PendingPlayerControlCommand, PlayerControlCommand, PlayerControllerSet,
        PlayerRuntimeSnapshot, PlayerSimulationReady,
    },
    forge_tidewater::{
        AmphibiousPlayerInput, AmphibiousPlayerState,
        amphibious_locomotion::Mode,
        tidewater_underwater::{Immersion, TidewaterUnderwaterControl, UnderwaterObservation},
    },
};
#[path = "swim_smoke/shadow_proof.rs"]
mod shadow_proof;

const PAIR_CAPTURE_DEADLINE_SECS: f64 = 10.0;
const WATERLINE_TOLERANCE_M: f32 = 0.02;

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq, serde::Serialize)]
enum Stage {
    #[default]
    Enter,
    Float,
    Dive,
    Neutral,
    Ascend,
    Surface,
    Return,
    Done,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, serde::Serialize)]
enum UnderwaterExpectation {
    Active,
    Bypassed,
}

impl UnderwaterExpectation {
    fn requires_effect(self) -> bool {
        self == Self::Active
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, serde::Serialize)]
enum CaptureVariant {
    Active,
    Bypassed,
}

impl CaptureVariant {
    const fn suffix(self) -> &'static str {
        match self {
            Self::Active => "ACTIVE",
            Self::Bypassed => "BYPASSED",
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, serde::Serialize)]
enum PixelExpectation {
    Equal,
    Different,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, serde::Serialize)]
enum MediumExpectation {
    SurfaceBandAboveWater,
    WaterlineWithinTwoCentimeters,
    FullyImmersed,
}

impl MediumExpectation {
    const fn for_stage(stage: Stage) -> Option<Self> {
        match stage {
            Stage::Float => Some(Self::SurfaceBandAboveWater),
            Stage::Dive => Some(Self::WaterlineWithinTwoCentimeters),
            Stage::Neutral => Some(Self::FullyImmersed),
            _ => None,
        }
    }
}

impl PixelExpectation {
    const fn for_stage(stage: Stage) -> Option<Self> {
        match stage {
            Stage::Float => Some(Self::Equal),
            Stage::Dive | Stage::Neutral => Some(Self::Different),
            _ => None,
        }
    }
}

/// Exact render inputs that must stay unchanged across one active/bypassed pair.
#[derive(Clone, Copy, Debug, PartialEq, Eq, serde::Serialize)]
struct FreezeSignature {
    stage: Stage,
    camera_matrix_bits: [u32; 16],
    projection_bits: [u32; 4],
    ocean_step: u32,
    ocean_time_bits: u64,
    ocean_dt_bits: u32,
    ocean_next_step: u32,
    query_step: u32,
    query_time_bits: u64,
    water_step: u32,
    water_time_bits: u64,
    water_dt_bits: u32,
    underwater_sample_step: u32,
    underwater_sample_time_bits: u64,
    underwater_water_height_bits: u32,
    underwater_camera_height_bits: u32,
    light_map_origin_bits: [u32; 4],
    light_map_step: u32,
    light_map_time_bits: u32,
}

#[derive(Clone, Debug, serde::Serialize)]
struct CaptureEvidence {
    variant: CaptureVariant,
    path: PathBuf,
    bytes: Option<u64>,
    dimensions: [u32; 2],
    sha256: Option<String>,
    signature: Option<FreezeSignature>,
    encoded_counter_before: u64,
    encoded_counter_after: u64,
    error: Option<String>,
}

impl CaptureEvidence {
    fn valid_for(&self, variant: CaptureVariant, signature: FreezeSignature) -> bool {
        self.variant == variant
            && self.bytes.is_some_and(|bytes| bytes > 0)
            && self.dimensions.into_iter().all(|dimension| dimension > 0)
            && self.sha256.is_some()
            && self.signature == Some(signature)
            && self.encoded_counter_after > self.encoded_counter_before
            && self.error.is_none()
    }
}

#[derive(Clone, Debug, serde::Serialize)]
struct PairEvidence {
    stage: Stage,
    pixel_expectation: PixelExpectation,
    medium_expectation: MediumExpectation,
    observed_camera_relative_height_m: f32,
    freeze_signature: FreezeSignature,
    active: Option<CaptureEvidence>,
    bypassed: Option<CaptureEvidence>,
}

impl PairEvidence {
    fn valid(&self) -> bool {
        let (Some(active), Some(bypassed)) = (&self.active, &self.bypassed) else {
            return false;
        };
        PixelExpectation::for_stage(self.stage) == Some(self.pixel_expectation)
            && MediumExpectation::for_stage(self.stage) == Some(self.medium_expectation)
            && active.valid_for(CaptureVariant::Active, self.freeze_signature)
            && bypassed.valid_for(CaptureVariant::Bypassed, self.freeze_signature)
            && active.dimensions == bypassed.dimensions
            && match self.pixel_expectation {
                PixelExpectation::Equal => active.sha256 == bypassed.sha256,
                PixelExpectation::Different => active.sha256 != bypassed.sha256,
            }
    }
}

#[derive(Clone, Copy, Debug, Default, PartialEq)]
enum QualificationPhase {
    #[default]
    Idle,
    Draining {
        stage: Stage,
        started_at: f64,
        encoded_baseline: u64,
    },
    AwaitActiveEncode {
        stage: Stage,
        started_at: f64,
        signature: FreezeSignature,
        baseline: u64,
    },
    AwaitActiveCapture {
        stage: Stage,
        started_at: f64,
        signature: FreezeSignature,
    },
    AwaitBypassedEncode {
        stage: Stage,
        started_at: f64,
        signature: FreezeSignature,
        baseline: u64,
    },
    AwaitBypassedCapture {
        stage: Stage,
        started_at: f64,
        signature: FreezeSignature,
    },
    ExtraShadow {
        started_at: f64,
        signature: FreezeSignature,
    },
}

impl QualificationPhase {
    const fn started_at(self) -> Option<f64> {
        match self {
            Self::Idle => None,
            Self::Draining { started_at, .. }
            | Self::AwaitActiveEncode { started_at, .. }
            | Self::AwaitActiveCapture { started_at, .. }
            | Self::AwaitBypassedEncode { started_at, .. }
            | Self::AwaitBypassedCapture { started_at, .. }
            | Self::ExtraShadow { started_at, .. } => Some(started_at),
        }
    }
}

#[derive(Resource)]
struct Smoke {
    stage: Stage,
    stage_ticks: u32,
    total_ticks: u32,
    grounded_ticks: u32,
    snapshots: Vec<serde_json::Value>,
    previous_camera: Option<f32>,
    max_camera_step: f32,
    max_wade: f32,
    neutral_start: Option<Vec3>,
    neutral_drift: f32,
    stable: bool,
    underwater_expectation: UnderwaterExpectation,
    saw_underwater_effect: bool,
    qualification: QualificationPhase,
    paired_evidence: Vec<PairEvidence>,
    qualification_error: Option<String>,
    waterline_retries: Vec<serde_json::Value>,
    requested: bool,
}

pub(super) fn install(app: &mut App) {
    app.insert_resource(bevy::winit::WinitSettings::continuous())
        .insert_resource(Smoke {
            stage: Stage::Enter,
            stage_ticks: 0,
            total_ticks: 0,
            grounded_ticks: 0,
            snapshots: Vec::new(),
            previous_camera: None,
            max_camera_step: 0.0,
            max_wade: 0.0,
            neutral_start: None,
            neutral_drift: 0.0,
            stable: true,
            underwater_expectation: if std::env::var_os("FORGE_TIDEWATER_UNDERWATER_DISABLED")
                .is_none()
            {
                UnderwaterExpectation::Active
            } else {
                UnderwaterExpectation::Bypassed
            },
            saw_underwater_effect: false,
            qualification: QualificationPhase::Idle,
            paired_evidence: Vec::new(),
            qualification_error: None,
            waterline_retries: Vec::new(),
            requested: false,
        })
        .add_systems(FixedUpdate, drive.before(PlayerControllerSet::Drive))
        .add_systems(FixedUpdate, arm_pair.after(PlayerControllerSet::Camera))
        .add_systems(Update, (qualify_pairs, finish).chain());
}

impl Smoke {
    fn pair_ready(&self, stage: Stage) -> bool {
        self.paired_evidence
            .iter()
            .any(|pair| pair.stage == stage && pair.valid())
    }

    fn requires_pair(&self, stage: Stage) -> bool {
        self.underwater_expectation.requires_effect()
            && PixelExpectation::for_stage(stage).is_some()
    }
}

#[allow(clippy::too_many_arguments)]
fn begin_pair(
    stage: Stage,
    elapsed: f64,
    underwater: &UnderwaterObservation,
    smoke: &mut Smoke,
    virtual_time: &mut Time<Virtual>,
    live: &super::super::ocean::LiveClock,
    control: &mut TidewaterUnderwaterControl,
    cloud_hold: &mut super::super::walk_clouds::QualificationHold,
    wind_hold: &mut super::super::walk_wind::QualificationHold,
) {
    cloud_hold.0 = true;
    wind_hold.0 = true;
    control.enabled = true;
    live.pause();
    virtual_time.pause();
    smoke.qualification = QualificationPhase::Draining {
        stage,
        started_at: elapsed,
        encoded_baseline: underwater.encoded_frames,
    };
}

#[allow(clippy::too_many_arguments)]
fn arm_pair(
    ready: Res<PlayerSimulationReady>,
    real_time: Res<Time<Real>>,
    mut virtual_time: ResMut<Time<Virtual>>,
    underwater: Res<UnderwaterObservation>,
    live: Res<super::super::ocean::LiveClock>,
    mut control: ResMut<TidewaterUnderwaterControl>,
    mut cloud_hold: ResMut<super::super::walk_clouds::QualificationHold>,
    mut wind_hold: ResMut<super::super::walk_wind::QualificationHold>,
    camera: Query<&Transform, With<ForgePlayerCamera>>,
    mut smoke: ResMut<Smoke>,
) {
    if !ready.0
        || smoke.requested
        || smoke.qualification_error.is_some()
        || smoke.qualification != QualificationPhase::Idle
        || !smoke.requires_pair(smoke.stage)
        || smoke.pair_ready(smoke.stage)
        || underwater.immersion == Immersion::Unavailable
    {
        return;
    }
    let Ok(camera_transform) = camera.single() else {
        smoke.qualification_error =
            Some("qualification requires one player camera transform".into());
        return;
    };
    let relative_height = camera_transform.translation.y - underwater.water_height_m;
    let due = match smoke.stage {
        Stage::Float => smoke.stage_ticks >= 180,
        Stage::Dive => relative_height.abs() <= WATERLINE_TOLERANCE_M,
        Stage::Neutral => smoke.stage_ticks >= 240,
        _ => false,
    };
    if !due {
        return;
    }
    let proof_stage = smoke.stage;
    begin_pair(
        proof_stage,
        real_time.elapsed_secs_f64(),
        &underwater,
        &mut smoke,
        &mut virtual_time,
        &live,
        &mut control,
        &mut cloud_hold,
        &mut wind_hold,
    );
}

#[allow(clippy::too_many_arguments, clippy::too_many_lines)]
fn drive(
    ready: Res<PlayerSimulationReady>,
    state: Res<AmphibiousPlayerState>,
    underwater: Res<UnderwaterObservation>,
    player: Res<PlayerRuntimeSnapshot>,
    camera: Query<&Transform, With<ForgePlayerCamera>>,
    probe: Res<super::Probe>,
    mut commands: Commands,
    mut smoke: ResMut<Smoke>,
    mut pending: ResMut<PendingPlayerControlCommand>,
    mut vertical: ResMut<AmphibiousPlayerInput>,
) {
    if !ready.0 || smoke.requested || smoke.qualification_error.is_some() {
        return;
    }
    smoke.total_ticks += 1;
    smoke.stage_ticks += 1;
    smoke.grounded_ticks += u32::from(player.grounded);
    smoke.stable &= player.backend_identity_stable;
    smoke.max_wade = smoke.max_wade.max(state.motion.wade);
    let camera_y = camera.single().map_or(f32::NAN, |c| c.translation.y);
    if let Some(previous) = smoke.previous_camera {
        smoke.max_camera_step = smoke.max_camera_step.max((camera_y - previous).abs());
    }
    smoke.previous_camera = Some(camera_y);
    smoke.stable &= camera_y.is_finite() && state.motion.position.is_finite();
    smoke.saw_underwater_effect |=
        underwater.effect_active() && underwater.pipeline_ready() && underwater.encoded_frames > 0;
    if smoke.stage == Stage::Neutral {
        if smoke.stage_ticks == 180 {
            smoke.neutral_start = Some(state.motion.position);
        }
        if let Some(start) = smoke.neutral_start {
            smoke.neutral_drift = smoke
                .neutral_drift
                .max(start.distance(state.motion.position));
            smoke.stable &= state.motion.position.y > state.ground_height + 0.35;
        }
    }
    let depth = state.motion.water_h - state.motion.position.y;
    let next = match smoke.stage {
        Stage::Enter
            if state.motion.mode == Mode::Swim
                && state.motion.water_mean.unwrap_or(0.0) - state.ground_height > 3.5 =>
        {
            Some(Stage::Float)
        }
        Stage::Float
            if smoke.stage_ticks >= 180
                && (!smoke.requires_pair(Stage::Float) || smoke.pair_ready(Stage::Float)) =>
        {
            Some(Stage::Dive)
        }
        Stage::Dive
            if depth > 1.1
                && (!smoke.requires_pair(Stage::Dive) || smoke.pair_ready(Stage::Dive)) =>
        {
            Some(Stage::Neutral)
        }
        Stage::Neutral
            if smoke.stage_ticks >= 240
                && (!smoke.requires_pair(Stage::Neutral) || smoke.pair_ready(Stage::Neutral)) =>
        {
            Some(Stage::Ascend)
        }
        Stage::Ascend if state.motion.floating && depth.abs() < 0.3 => Some(Stage::Surface),
        Stage::Surface if smoke.stage_ticks >= 180 => Some(Stage::Return),
        Stage::Return
            if state.walk_returns > 0 && player.grounded && state.motion.position.z < -47.0 =>
        {
            Some(Stage::Done)
        }
        _ => None,
    };
    if let Some(next) = next {
        let previous = smoke.stage;
        let sample = serde_json::json!({
            "completed_stage":previous,"tick":smoke.total_ticks,"stage_ticks":smoke.stage_ticks,
            "anchor":state.motion.position.to_array(),"velocity":state.motion.velocity.to_array(),
            "mode":format!("{:?}",state.motion.mode),"grounded":player.grounded,
            "water_height":state.motion.water_h,"water_mean":state.motion.water_mean,
            "floor":state.ground_height,"floating":state.motion.floating,"camera_y":camera_y,
            "query_step":state.water_query_step,"query_time":state.water_query_time,
            "query_position":state.water_query_position,"stable_backend":player.backend_identity_stable,
            "underwater_effect_active":underwater.effect_active(),
            "underwater_effect_enabled":underwater.effect_enabled,
            "underwater_camera_depth_m":underwater.camera_depth_m,
            "underwater_pipeline_ready":underwater.pipeline_ready(),
            "underwater_encoded_frames":underwater.encoded_frames,
        });
        println!("TIDEWATER_SWIM_STAGE {sample}");
        smoke.snapshots.push(sample);
        if matches!(previous, Stage::Float | Stage::Neutral | Stage::Surface) {
            commands
                .spawn(Screenshot::primary_window())
                .observe(save_to_disk(
                    probe.screenshot.with_extension(format!("{previous:?}.png")),
                ));
        }
        smoke.stage = next;
        smoke.stage_ticks = 0;
    }
    *vertical = AmphibiousPlayerInput::default();
    let mut command = PlayerControlCommand::default();
    match smoke.stage {
        Stage::Enter => {
            command.move_axis = Vec2::Y;
            command.sprint = true;
        }
        Stage::Dive => {
            // Reach the near-lens band with the same source swim commands.
            // A pause can drain a later wave step, so a rejected freeze must
            // be allowed to return from below before trying the strict band again.
            let camera_relative = camera_y - underwater.water_height_m;
            if smoke.requires_pair(Stage::Dive)
                && !smoke.pair_ready(Stage::Dive)
                && camera_relative < -WATERLINE_TOLERANCE_M * 0.5
            {
                vertical.ascend = true;
            } else {
                vertical.descend = true;
            }
        }
        Stage::Ascend => vertical.ascend = true,
        Stage::Return => {
            command.move_axis = Vec2::NEG_Y;
            command.sprint = true;
        }
        _ => {}
    }
    pending.submit(command).expect("finite native swim command");
}

#[allow(clippy::too_many_arguments)]
fn synchronized_signature(
    stage: Stage,
    camera: &Query<(&GlobalTransform, &Projection), With<ForgePlayerCamera>>,
    live: &super::super::ocean::LiveClock,
    ocean: &super::super::ocean::Targets,
    spray: &super::super::spray::Targets,
    queries: &super::super::spray::water_queries::Targets,
    water: &super::super::water_time::WaterTime,
    underwater: &UnderwaterObservation,
    light_maps: &forge_tidewater::tidewater_underwater_lighting::Targets,
) -> Result<Option<FreezeSignature>, String> {
    let Ok((camera, Projection::Perspective(projection))) = camera.single() else {
        return Err("qualification requires one perspective player camera".into());
    };
    let ocean_clock = live.snapshot();
    if !ocean_clock.quiescent {
        return Ok(None);
    }
    let Some(completed) = ocean_clock.completed else {
        return Ok(None);
    };
    let Some(query) = queries.queries.latest() else {
        return Ok(None);
    };
    let ocean_step = ocean.completed_steps.load(Ordering::Acquire);
    let spray_step = spray.completed_steps.load(Ordering::Acquire);
    let map = *light_maps.observation.lock().expect("light map readback");
    let position = camera.translation();
    let expected_origins = [
        (position.x / 0.25).floor() * 0.25 - 64.0,
        (position.z / 0.25).floor() * 0.25 - 64.0,
        (position.x / 2.0).floor() * 2.0 - 512.0,
        (position.z / 2.0).floor() * 2.0 - 512.0,
    ];
    #[allow(clippy::cast_possible_truncation)] // Optical frame time is source f32.
    let map_time = completed.time as f32;
    let synchronized = ocean_step == completed.step
        && map.step == completed.step
        && map.time.to_bits() == map_time.to_bits()
        && map.origins.map(f32::to_bits) == expected_origins.map(f32::to_bits)
        && map.resolution == 512
        && map.layers == 2
        && spray_step == completed.step
        && query.step == completed.step
        && water.step == completed.step
        && underwater.sample_step == completed.step
        && query.time.to_bits() == completed.time.to_bits()
        && water.time.to_bits() == completed.time.to_bits()
        && underwater.sample_time_s.to_bits() == completed.time.to_bits()
        && water.dt.to_bits() == completed.dt.to_bits();
    if !synchronized {
        return Ok(None);
    }
    Ok(Some(FreezeSignature {
        stage,
        camera_matrix_bits: camera.to_matrix().to_cols_array().map(f32::to_bits),
        projection_bits: [
            projection.fov.to_bits(),
            projection.aspect_ratio.to_bits(),
            projection.near.to_bits(),
            projection.far.to_bits(),
        ],
        ocean_step: completed.step,
        ocean_time_bits: completed.time.to_bits(),
        ocean_dt_bits: completed.dt.to_bits(),
        ocean_next_step: ocean_clock.next_step,
        query_step: query.step,
        query_time_bits: query.time.to_bits(),
        water_step: water.step,
        water_time_bits: water.time.to_bits(),
        water_dt_bits: water.dt.to_bits(),
        underwater_sample_step: underwater.sample_step,
        underwater_sample_time_bits: underwater.sample_time_s.to_bits(),
        underwater_water_height_bits: underwater.water_height_m.to_bits(),
        underwater_camera_height_bits: underwater.camera_height_m.to_bits(),
        light_map_origin_bits: map.origins.map(f32::to_bits),
        light_map_step: map.step,
        light_map_time_bits: map.time.to_bits(),
    }))
}

fn validate_pair_medium(
    stage: Stage,
    underwater: &UnderwaterObservation,
) -> Result<(PixelExpectation, MediumExpectation, f32), String> {
    if !underwater.effect_enabled {
        return Err(format!(
            "{stage:?} ACTIVE branch was disabled before qualification"
        ));
    }
    let relative_height = underwater.camera_height_m - underwater.water_height_m;
    let expectation = PixelExpectation::for_stage(stage)
        .ok_or_else(|| format!("{stage:?} has no underwater A/B expectation"))?;
    let medium_expectation = MediumExpectation::for_stage(stage)
        .ok_or_else(|| format!("{stage:?} has no medium expectation"))?;
    let valid = match stage {
        Stage::Float => underwater.immersion == Immersion::SurfaceBand && relative_height > 0.0,
        Stage::Dive => {
            underwater.immersion == Immersion::SurfaceBand
                && relative_height.abs() <= WATERLINE_TOLERANCE_M
        }
        Stage::Neutral => underwater.immersion == Immersion::FullyImmersed,
        _ => false,
    };
    if valid {
        Ok((expectation, medium_expectation, relative_height))
    } else {
        Err(format!(
            "{stage:?} medium mismatch: immersion={:?}, relative_height_m={relative_height}",
            underwater.immersion
        ))
    }
}

fn capture_slot(smoke: &mut Smoke, stage: Stage, evidence: CaptureEvidence) {
    let variant = evidence.variant;
    let Some(pair) = smoke
        .paired_evidence
        .iter_mut()
        .find(|pair| pair.stage == stage)
    else {
        smoke.qualification_error = Some(format!(
            "{stage:?} {variant:?} capture returned without a pending pair"
        ));
        return;
    };
    let slot = match variant {
        CaptureVariant::Active => &mut pair.active,
        CaptureVariant::Bypassed => &mut pair.bypassed,
    };
    if slot.is_some() {
        smoke.qualification_error = Some(format!(
            "{stage:?} {variant:?} capture callback was duplicated"
        ));
    } else {
        *slot = Some(evidence);
    }
}

fn request_pair_capture(
    commands: &mut Commands,
    path: PathBuf,
    stage: Stage,
    variant: CaptureVariant,
    expected_signature: FreezeSignature,
    encoded_counter_before: u64,
    encoded_counter_after: u64,
) -> Result<(), String> {
    match std::fs::remove_file(&path) {
        Ok(()) => {}
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => {}
        Err(error) => return Err(format!("cannot remove {}: {error}", path.display())),
    }
    commands.spawn(Screenshot::primary_window()).observe(
        move |event: On<ScreenshotCaptured>,
              camera: Query<(&GlobalTransform, &Projection), With<ForgePlayerCamera>>,
              live: Res<super::super::ocean::LiveClock>,
              ocean: Res<super::super::ocean::Targets>,
              spray: Res<super::super::spray::Targets>,
              queries: Res<super::super::spray::water_queries::Targets>,
              water: Res<super::super::water_time::WaterTime>,
              underwater: Res<UnderwaterObservation>,
              light_maps: Res<forge_tidewater::tidewater_underwater_lighting::Targets>,
              mut smoke: ResMut<Smoke>| {
            let dimensions = [
                event.image.texture_descriptor.size.width,
                event.image.texture_descriptor.size.height,
            ];
            let signature = synchronized_signature(
                stage,
                &camera,
                &live,
                &ocean,
                &spray,
                &queries,
                &water,
                &underwater,
                &light_maps,
            );
            save_to_disk(&path)(event);
            let mut errors = Vec::new();
            let actual_signature = match signature {
                Ok(Some(signature)) if signature == expected_signature => Some(signature),
                Ok(Some(_)) => {
                    errors.push("freeze signature changed before screenshot readback".to_owned());
                    None
                }
                Ok(None) => {
                    errors.push(
                        "water sources were not synchronized at screenshot readback".to_owned(),
                    );
                    None
                }
                Err(error) => {
                    errors.push(error);
                    None
                }
            };
            let (bytes, sha256) = match std::fs::read(&path) {
                Ok(data) if !data.is_empty() => (
                    u64::try_from(data.len()).ok(),
                    Some(forge_core::bytes_sha256(&data).to_string()),
                ),
                Ok(_) => {
                    errors.push("saved screenshot is empty".to_owned());
                    (Some(0), None)
                }
                Err(error) => {
                    errors.push(format!("cannot read saved screenshot: {error}"));
                    (None, None)
                }
            };
            capture_slot(
                &mut smoke,
                stage,
                CaptureEvidence {
                    variant,
                    path: path.clone(),
                    bytes,
                    dimensions,
                    sha256,
                    signature: actual_signature,
                    encoded_counter_before,
                    encoded_counter_after,
                    error: (!errors.is_empty()).then(|| errors.join("; ")),
                },
            );
        },
    );
    Ok(())
}

fn restore_after_pair(
    elapsed: f64,
    virtual_time: &mut Time<Virtual>,
    live: &super::super::ocean::LiveClock,
    control: &mut TidewaterUnderwaterControl,
    cloud_hold: &mut super::super::walk_clouds::QualificationHold,
    wind_hold: &mut super::super::walk_wind::QualificationHold,
) -> Result<(), String> {
    control.enabled = true;
    let resumed = live.resume_at(elapsed).map_err(str::to_owned);
    virtual_time.unpause();
    cloud_hold.0 = false;
    wind_hold.0 = false;
    resumed
}

#[allow(clippy::too_many_arguments, clippy::too_many_lines)]
fn qualify_pairs(
    mut commands: Commands,
    real_time: Res<Time<Real>>,
    mut virtual_time: ResMut<Time<Virtual>>,
    live: Res<super::super::ocean::LiveClock>,
    ocean: Res<super::super::ocean::Targets>,
    spray: Res<super::super::spray::Targets>,
    queries: Res<super::super::spray::water_queries::Targets>,
    water: Res<super::super::water_time::WaterTime>,
    camera: Query<(&GlobalTransform, &Projection), With<ForgePlayerCamera>>,
    underwater: Res<UnderwaterObservation>,
    lighting: (
        Res<forge_tidewater::tidewater_underwater_lighting::Targets>,
        ResMut<forge_tidewater::tidewater_underwater_lighting::Control>,
        Local<Option<shadow_proof::Proof>>,
    ),
    mut control: ResMut<TidewaterUnderwaterControl>,
    mut cloud_hold: ResMut<super::super::walk_clouds::QualificationHold>,
    mut wind_hold: ResMut<super::super::walk_wind::QualificationHold>,
    probe: Res<super::Probe>,
    mut smoke: ResMut<Smoke>,
) {
    let (light_maps, mut lighting_control, mut shadow) = lighting;
    let phase = smoke.qualification;
    let Some(started_at) = phase.started_at() else {
        return;
    };
    let elapsed = real_time.elapsed_secs_f64();
    let deadline = if matches!(phase, QualificationPhase::ExtraShadow { .. }) {
        20.0
    } else {
        PAIR_CAPTURE_DEADLINE_SECS
    };
    if smoke.qualification_error.is_some() || elapsed - started_at > deadline {
        if let Some(proof) = shadow.as_mut() {
            proof.fail();
            proof.restore(&mut control, &mut lighting_control);
        }
        let mut error = smoke.qualification_error.take().unwrap_or_else(|| {
            format!(
                "underwater {:?} pair exceeded the {deadline:.0}s real deadline",
                smoke.stage
            )
        });
        if let Err(cleanup) = restore_after_pair(
            elapsed,
            &mut virtual_time,
            &live,
            &mut control,
            &mut cloud_hold,
            &mut wind_hold,
        ) {
            error.push_str("; cleanup failed: ");
            error.push_str(&cleanup);
        }
        smoke.qualification_error = Some(error);
        smoke.qualification = QualificationPhase::Idle;
        return;
    }

    let current_signature = |stage| {
        synchronized_signature(
            stage,
            &camera,
            &live,
            &ocean,
            &spray,
            &queries,
            &water,
            &underwater,
            &light_maps,
        )
    };
    match phase {
        QualificationPhase::Idle => {}
        QualificationPhase::Draining {
            stage,
            started_at,
            encoded_baseline,
        } => {
            // The pause happens after the fixed camera system but before
            // PostUpdate propagation and rendering. Observe that later encoded
            // frame before latching any A/B inputs or branch counters.
            if underwater.encoded_frames <= encoded_baseline {
                return;
            }
            let signature = match current_signature(stage) {
                Ok(Some(signature)) => signature,
                Ok(None) => return,
                Err(error) => {
                    smoke.qualification_error = Some(error);
                    return;
                }
            };
            let (pixel_expectation, medium_expectation, relative_height) =
                match validate_pair_medium(stage, &underwater) {
                    Ok(expectation) => expectation,
                    Err(error) => {
                        if stage == Stage::Dive && smoke.waterline_retries.len() < 40 {
                            smoke.waterline_retries.push(serde_json::json!({
                                "reason": error,
                                "camera_height_m": underwater.camera_height_m,
                                "water_height_m": underwater.water_height_m,
                                "sample_step": underwater.sample_step,
                                "sample_time_s": underwater.sample_time_s,
                                "frozen_signature": signature,
                            }));
                            if let Err(cleanup) = restore_after_pair(
                                elapsed,
                                &mut virtual_time,
                                &live,
                                &mut control,
                                &mut cloud_hold,
                                &mut wind_hold,
                            ) {
                                smoke.qualification_error = Some(cleanup);
                            }
                            smoke.qualification = QualificationPhase::Idle;
                            return;
                        }
                        smoke.qualification_error = Some(error);
                        return;
                    }
                };
            smoke.paired_evidence.push(PairEvidence {
                stage,
                pixel_expectation,
                medium_expectation,
                observed_camera_relative_height_m: relative_height,
                freeze_signature: signature,
                active: None,
                bypassed: None,
            });
            smoke.qualification = QualificationPhase::AwaitActiveEncode {
                stage,
                started_at,
                signature,
                baseline: underwater.encoded_enabled_frames,
            };
        }
        QualificationPhase::AwaitActiveEncode {
            stage,
            started_at,
            signature,
            baseline,
        } => {
            match current_signature(stage) {
                Ok(Some(current)) if current == signature => {}
                Ok(Some(_)) => {
                    smoke.qualification_error = Some(format!(
                        "{stage:?} freeze signature changed before ACTIVE capture"
                    ));
                    return;
                }
                Ok(None) => return,
                Err(error) => {
                    smoke.qualification_error = Some(error);
                    return;
                }
            }
            if !underwater.effect_enabled || underwater.encoded_enabled_frames <= baseline {
                return;
            }
            let path = probe
                .screenshot
                .with_extension(format!("{stage:?}.{}.png", CaptureVariant::Active.suffix()));
            if let Err(error) = request_pair_capture(
                &mut commands,
                path,
                stage,
                CaptureVariant::Active,
                signature,
                baseline,
                underwater.encoded_enabled_frames,
            ) {
                smoke.qualification_error = Some(error);
                return;
            }
            smoke.qualification = QualificationPhase::AwaitActiveCapture {
                stage,
                started_at,
                signature,
            };
        }
        QualificationPhase::AwaitActiveCapture {
            stage,
            started_at,
            signature,
        } => {
            let Some(active) = smoke
                .paired_evidence
                .iter()
                .find(|pair| pair.stage == stage)
                .and_then(|pair| pair.active.as_ref())
            else {
                return;
            };
            if !active.valid_for(CaptureVariant::Active, signature) {
                smoke.qualification_error = Some(format!(
                    "{stage:?} ACTIVE capture is invalid: {:?}",
                    active.error
                ));
                return;
            }
            match current_signature(stage) {
                Ok(Some(current)) if current == signature => {}
                Ok(Some(_)) => {
                    smoke.qualification_error = Some(format!(
                        "{stage:?} freeze signature changed after ACTIVE capture"
                    ));
                    return;
                }
                Ok(None) => return,
                Err(error) => {
                    smoke.qualification_error = Some(error);
                    return;
                }
            }
            control.enabled = false;
            smoke.qualification = QualificationPhase::AwaitBypassedEncode {
                stage,
                started_at,
                signature,
                baseline: underwater.encoded_bypassed_frames,
            };
        }
        QualificationPhase::AwaitBypassedEncode {
            stage,
            started_at,
            signature,
            baseline,
        } => {
            match current_signature(stage) {
                Ok(Some(current)) if current == signature => {}
                Ok(Some(_)) => {
                    smoke.qualification_error = Some(format!(
                        "{stage:?} freeze signature changed before BYPASSED capture"
                    ));
                    return;
                }
                Ok(None) => return,
                Err(error) => {
                    smoke.qualification_error = Some(error);
                    return;
                }
            }
            if underwater.effect_enabled || underwater.encoded_bypassed_frames <= baseline {
                return;
            }
            let path = probe.screenshot.with_extension(format!(
                "{stage:?}.{}.png",
                CaptureVariant::Bypassed.suffix()
            ));
            if let Err(error) = request_pair_capture(
                &mut commands,
                path,
                stage,
                CaptureVariant::Bypassed,
                signature,
                baseline,
                underwater.encoded_bypassed_frames,
            ) {
                smoke.qualification_error = Some(error);
                return;
            }
            smoke.qualification = QualificationPhase::AwaitBypassedCapture {
                stage,
                started_at,
                signature,
            };
        }
        QualificationPhase::AwaitBypassedCapture {
            stage, signature, ..
        } => {
            let Some(pair) = smoke
                .paired_evidence
                .iter()
                .find(|pair| pair.stage == stage)
            else {
                smoke.qualification_error =
                    Some(format!("{stage:?} pair disappeared before completion"));
                return;
            };
            if pair.bypassed.is_none() {
                return;
            }
            if pair.freeze_signature != signature || !pair.valid() {
                smoke.qualification_error = Some(format!(
                    "{stage:?} ACTIVE/BYPASSED pair did not preserve inputs or meet its pixel expectation"
                ));
                return;
            }
            if stage == Stage::Neutral
                && std::env::var_os("FORGE_TIDEWATER_REFRACTED_SHADOWS_PROOF").is_some()
            {
                *shadow = Some(shadow_proof::Proof::start(
                    signature,
                    &probe.screenshot,
                    &mut control,
                    &mut lighting_control,
                    underwater.encoded_frames,
                ));
                smoke.qualification = QualificationPhase::ExtraShadow {
                    started_at: elapsed,
                    signature,
                };
                return;
            }
            if let Err(error) = restore_after_pair(
                elapsed,
                &mut virtual_time,
                &live,
                &mut control,
                &mut cloud_hold,
                &mut wind_hold,
            ) {
                smoke.qualification_error = Some(error);
                smoke.qualification = QualificationPhase::Idle;
                return;
            }
            smoke.qualification = QualificationPhase::Idle;
        }
        QualificationPhase::ExtraShadow { signature, .. } => {
            let Some(proof) = shadow.as_mut() else {
                smoke.qualification_error = Some("additional shadow proof missing".into());
                return;
            };
            match proof.update(
                &mut commands,
                current_signature(Stage::Neutral),
                underwater.encoded_frames,
                &mut lighting_control,
            ) {
                Ok(false) => {}
                Ok(true) => {
                    proof.restore(&mut control, &mut lighting_control);
                    if let Err(error) = restore_after_pair(
                        elapsed,
                        &mut virtual_time,
                        &live,
                        &mut control,
                        &mut cloud_hold,
                        &mut wind_hold,
                    ) {
                        smoke.qualification_error = Some(error);
                    }
                    smoke.qualification = QualificationPhase::Idle;
                    debug_assert_eq!(signature.stage, Stage::Neutral);
                }
                Err(error) => {
                    proof.fail();
                    smoke.qualification_error = Some(error);
                }
            }
        }
    }
}

// This Bevy terminal system validates the complete cross-subsystem receipt;
// its resources are intentionally kept as separate owned parameters.
#[allow(clippy::too_many_arguments)]
fn finish(
    mut commands: Commands,
    time: Res<Time<Real>>,
    state: Res<AmphibiousPlayerState>,
    underwater: Res<UnderwaterObservation>,
    light_maps: Res<forge_tidewater::tidewater_underwater_lighting::Targets>,
    mut smoke: ResMut<Smoke>,
    probe: Res<super::Probe>,
    mut exit: MessageWriter<AppExit>,
) {
    if smoke.requested {
        return;
    }
    let timeout = time.elapsed_secs() > 300.0;
    if timeout && smoke.qualification != QualificationPhase::Idle {
        return;
    }
    let qualification_failed =
        smoke.qualification_error.is_some() && smoke.qualification == QualificationPhase::Idle;
    if smoke.stage != Stage::Done && !timeout && !qualification_failed {
        return;
    }
    smoke.requested = true;
    let paired_evidence_valid = match smoke.underwater_expectation {
        UnderwaterExpectation::Active => {
            smoke.paired_evidence.len() == 3
                && [Stage::Float, Stage::Dive, Stage::Neutral]
                    .into_iter()
                    .all(|stage| {
                        smoke
                            .paired_evidence
                            .iter()
                            .any(|pair| pair.stage == stage && pair.valid())
                    })
        }
        UnderwaterExpectation::Bypassed => smoke.paired_evidence.is_empty(),
    };
    let map_observation = light_maps.observation.lock().ok().map(|state| *state);
    let light_maps_ready = map_observation
        .is_some_and(|state| state.step > 0 && state.resolution == 512 && state.layers == 2);
    let pass = light_maps_ready
        && !timeout
        && smoke.qualification_error.is_none()
        && paired_evidence_valid
        && smoke.stable
        && state.swim_entries > 0
        && state.walk_returns > 0
        && smoke.max_wade > 0.9
        && smoke.neutral_start.is_some()
        && smoke.neutral_drift < 0.02
        && smoke.max_camera_step < 0.3
        && smoke.saw_underwater_effect == smoke.underwater_expectation.requires_effect()
        && underwater.pipeline_ready()
        && underwater.encoded_frames > 0
        && smoke.snapshots.len() == 7;
    let receipt = serde_json::json!({
        "pass":pass,"scope":"native source locomotion on canonical terrain and GPU wave samples; command injection, not OS input",
        "timeout":timeout,"stage":smoke.stage,"ticks":smoke.total_ticks,
        "grounded_ticks":smoke.grounded_ticks,"swim_entries":state.swim_entries,"walk_returns":state.walk_returns,
        "max_wade":smoke.max_wade,"neutral_drift_m":smoke.neutral_drift,
        "max_camera_step_m":smoke.max_camera_step,"stable_backend":smoke.stable,
        "saw_underwater_effect":smoke.saw_underwater_effect,
        "underwater_expectation":smoke.underwater_expectation,
        "underwater_effect_enabled":underwater.effect_enabled,
        "underwater_pipeline_ready":underwater.pipeline_ready(),
        "underwater_encoded_frames":underwater.encoded_frames,
        "underwater_encoded_enabled_frames":underwater.encoded_enabled_frames,
        "underwater_encoded_bypassed_frames":underwater.encoded_bypassed_frames,
        "underwater_light_maps": {
            "ready": light_maps_ready,
            "encoded_frames":light_maps.encoded_frames.load(std::sync::atomic::Ordering::Relaxed),
            "gpu":map_observation.map(|state| serde_json::json!({ "origins":state.origins, "step":state.step,
                "time":state.time, "resolution":state.resolution, "layers":state.layers })),
        },
        "qualification_timeout_seconds":PAIR_CAPTURE_DEADLINE_SECS,
        "paired_evidence_valid":paired_evidence_valid,
        "paired_evidence":smoke.paired_evidence,
        "qualification_error":smoke.qualification_error,
        "waterline_retries":smoke.waterline_retries,
        "snapshots":smoke.snapshots,
    });
    let saved = serde_json::to_vec_pretty(&receipt)
        .map_err(std::io::Error::other)
        .and_then(|data| {
            std::fs::OpenOptions::new()
                .create_new(true)
                .write(true)
                .open(probe.screenshot.with_extension("walk.json"))?
                .write_all(&data)
        });
    if !pass || saved.is_err() {
        error!(?receipt, ?saved, "native swim smoke failed");
        exit.write(AppExit::error());
        return;
    }
    println!("TIDEWATER_SWIM_PASS {receipt}");
    commands
        .spawn(Screenshot::primary_window())
        .observe(save_to_disk(probe.screenshot.clone()))
        .observe(
            |_: On<ScreenshotCaptured>, mut exit: MessageWriter<AppExit>| {
                exit.write(AppExit::Success);
            },
        );
}
