//! Explicit, bounded bridge/quality qualification using the normal Studio request queue.
use super::quality_motion_trace::{SlowFrame, StageTrace};
use bevy::ecs as bevy_ecs;
use bevy::prelude::*;
use forge_engine::{
    BridgeCrossingProbeStatus, GlobalQualityStats, PlayerRuntimeSnapshot, QualityCpuFrame,
    QualityCpuTracePlugin,
};
use forge_project::VisualQualityTier;

#[derive(Resource, Default)]
struct Probe {
    prewarmed: bool,
    preparation_requested: bool,
    preparation: Option<forge_engine::ProfileWarmup>,
    stage: usize,
    stable: u32,
    first_time: Option<f64>,
    records: Vec<serde_json::Value>,
    terminal: bool,
    traces: [StageTrace; 3],
    previous_cpu_ms: [Option<f64>; 6],
}

pub fn install(app: &mut App) -> Result<bool, String> {
    let search = web_sys::window()
        .ok_or("Fenêtre absente")?
        .location()
        .search()
        .map_err(|_| "URL inaccessible")?;
    let values: Vec<_> = search
        .trim_start_matches('?')
        .split('&')
        .filter_map(|part| part.strip_prefix("forge_quality_motion="))
        .collect();
    match values.as_slice() {
        [] => Ok(false),
        [mode @ ("1" | "prewarmed")] => {
            let prewarmed = *mode == "prewarmed";
            app.add_plugins(QualityCpuTracePlugin)
                .insert_resource(Probe {
                    prewarmed,
                    ..default()
                })
                .add_systems(Last, advance);
            Ok(prewarmed)
        }
        _ => Err("Paramètre forge_quality_motion invalide ou répété".into()),
    }
}

fn await_preparation(
    probe: &mut Probe,
    warmup: &mut forge_engine::ProfileWarmup,
    now: f64,
    bridge: &BridgeCrossingProbeStatus,
) -> bool {
    use forge_engine::ProfileWarmupPhase;
    if !probe.prewarmed || probe.preparation.is_some() {
        return false;
    }
    let started = *probe.first_time.get_or_insert(now);
    if !probe.preparation_requested {
        warmup.request();
        probe.preparation_requested = true;
        return true;
    }
    if matches!(
        warmup.phase,
        ProfileWarmupPhase::Failed | ProfileWarmupPhase::Cancelled
    ) || now - started > 120.0
        || bridge.failure_reason.is_some()
    {
        probe.preparation = Some(warmup.clone());
        finish(probe, "FAILED: préparation avant mouvement", bridge);
    } else if warmup.phase == ProfileWarmupPhase::Ready && warmup.completed.len() == 5 {
        probe.preparation = Some(warmup.clone());
        // The independent movement deadline starts only after explicit preparation.
        probe.first_time = None;
        probe.previous_cpu_ms = [None; 6];
        return false;
    }
    true
}

fn ready(stats: &GlobalQualityStats, tier: VisualQualityTier) -> bool {
    let (Some(application), Some(effective)) = (&stats.application, &stats.effective) else {
        return false;
    };
    let Some(plan) = &effective.plan else {
        return false;
    };
    stats.requested_tier == tier
        && stats.requests_dispatched
        && stats.available_budgets_projected
        && application.status == "plan_application_ready"
        && application.gpu_ready
        && application.error.is_none()
        && application.projected_domains.iter().all(|yes| *yes)
        && effective.status == "plan_application_ready"
        && effective.gpu_ready
        && effective.error.is_none()
        && plan.requested_tier == tier
        && plan.blockers.is_empty()
        && plan.source_bindings.is_some()
        && application.identity.is_some_and(|identity| {
            Some(identity.plan_hash) == effective.plan_hash
                && identity.adapter_generation == plan.adapter_generation
        })
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn advance(
    mut probe: ResMut<Probe>,
    time: Res<Time<Real>>,
    stats: Res<GlobalQualityStats>,
    player: Res<PlayerRuntimeSnapshot>,
    bridge: Res<BridgeCrossingProbeStatus>,
    cpu: Res<QualityCpuFrame>,
    mut warmup: ResMut<forge_engine::ProfileWarmup>,
) {
    if probe.terminal {
        return;
    }
    if await_preparation(&mut probe, &mut warmup, time.elapsed_secs_f64(), &bridge) {
        return;
    }
    let started = *probe.first_time.get_or_insert(time.elapsed_secs_f64());
    if time.elapsed_secs_f64() - started > 120.0 || bridge.failure_reason.is_some() {
        finish(&mut probe, "FAILED: délai ou parcours", &bridge);
        return;
    }
    let tier = [
        VisualQualityTier::High,
        VisualQualityTier::Low,
        VisualQualityTier::High,
    ][probe.stage];
    let moving = player.horizontal_velocity.length() > 0.05
        && player.effective_translation.length() > 0.0001;
    let gpu_ready = ready(&stats, tier);
    let cpu_ready = stats.requested_tier == tier
        && stats.available_budgets_projected
        && stats
            .application
            .as_ref()
            .is_some_and(|a| a.projected_domains.iter().all(|v| *v));
    let previous_cpu_ms = probe.previous_cpu_ms;
    let stage = probe.stage;
    probe.traces[stage].observe(
        SlowFrame {
            elapsed_seconds: time.elapsed_secs_f64() - started,
            delta_ms: time.delta_secs_f64() * 1000.0,
            previous_cpu_ms,
        },
        cpu.milliseconds,
        moving,
        cpu_ready,
        gpu_ready,
    );
    probe.previous_cpu_ms = cpu.milliseconds;
    if !moving || !gpu_ready {
        probe.stable = 0;
        return;
    }
    probe.stable += 1;
    if probe.stable < 8 {
        return;
    }
    probe.records.push(serde_json::json!({
        "tier": tier, "position": player.player_position.to_array(),
        "velocity": player.horizontal_velocity.to_array(), "fixed_tick": player.fixed_tick,
        "effective_translation": player.effective_translation.to_array(),
        "quality": &*stats,
    }));
    probe.stage += 1;
    probe.stable = 0;
    if probe.stage == 3 {
        let sources: Vec<_> = probe
            .records
            .iter()
            .map(|r| &r["quality"]["effective"]["plan"]["source_bindings"])
            .collect();
        let positions: Vec<_> = probe.records.iter().map(|r| &r["position"]).collect();
        let valid = sources[0] == sources[1]
            && sources[1] == sources[2]
            && positions[0] != positions[1]
            && positions[1] != positions[2];
        finish(
            &mut probe,
            if valid {
                "PASS: GPU High→Low→High en déplacement"
            } else {
                "FAILED: identité ou déplacement"
            },
            &bridge,
        );
    } else {
        super::quality_preview::request_manual(if probe.stage == 1 {
            VisualQualityTier::Low
        } else {
            VisualQualityTier::High
        });
    }
}

fn finish(probe: &mut Probe, status: &str, bridge: &BridgeCrossingProbeStatus) {
    probe.terminal = true;
    if let Some(element) = web_sys::window()
        .and_then(|w| w.document())
        .and_then(|d| d.get_element_by_id("studio-quality-motion-status"))
    {
        element.set_text_content(Some(status));
    }
    // Evidence stays local in the DOM; no network or project mutation.
    if let Some(element) = web_sys::window()
        .and_then(|w| w.document())
        .and_then(|d| d.get_element_by_id("studio-quality-motion-proof"))
    {
        element.set_text_content(Some(
            &serde_json::json!({
                "schema": "forge.studio-quality-motion-proof", "schema_version": 1,
                "status": status, "records": probe.records, "bridge": bridge,
                "protocol": if probe.prewarmed { "prewarmed_bridge_cycle" } else { "cold_bridge_cycle" },
                "profile_preparation": probe.preparation,
                "diagnostic_trace": probe.traces,
                "cpu_phase_order": QualityCpuFrame::phase_names(),
                "trace_scope": "main_thread_only; forest_inclusive_contains_far_batches; delta_includes_previous_frame_work_and_external_waits; not_gpu_timestamps",
                "visual_qualification": "requires_capture_and_inspection",
            })
            .to_string(),
        ));
    }
}
