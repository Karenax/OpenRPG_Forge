use serde::{Deserialize, Serialize};

#[cfg(all(target_arch = "wasm32", feature = "web"))]
pub const FRAME_WARMUP_SAMPLES: usize = 60;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
pub const FRAME_MEASURED_SAMPLES: usize = 120;
pub const TAURI_DENIED_COMMAND: &str = "plugin:window|set_title";

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
pub enum Gate0ProbeMode {
    #[default]
    None,
    WebGpuUnavailable,
    TauriPermission,
}

pub fn parse_gate0_probe(search: &str) -> Gate0ProbeMode {
    search
        .trim_start_matches('?')
        .split('&')
        .filter_map(|entry| entry.split_once('='))
        .find_map(|(key, value)| {
            if key != "forge_gate0_probe" {
                return None;
            }
            Some(match value {
                "webgpu-unavailable" => Gate0ProbeMode::WebGpuUnavailable,
                "tauri-permission" => Gate0ProbeMode::TauriPermission,
                _ => Gate0ProbeMode::None,
            })
        })
        .unwrap_or_default()
}

pub fn is_explicit_acl_denial(reason: &str) -> bool {
    reason
        .strip_prefix("Commande Tauri échouée : Command ")
        .and_then(|verdict| verdict.strip_suffix(" not allowed by ACL"))
        == Some(TAURI_DENIED_COMMAND)
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum StudioTelemetryTarget {
    WasmBrowser,
    TauriStudio,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum WebGpuProbeStatus {
    Checking,
    Ready,
    Unavailable,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WebGpuObservation {
    pub status: WebGpuProbeStatus,
    pub diagnostic_injection: bool,
    pub reason: Option<String>,
}

impl Default for WebGpuObservation {
    fn default() -> Self {
        Self {
            status: WebGpuProbeStatus::Checking,
            diagnostic_injection: false,
            reason: None,
        }
    }
}

#[derive(Clone, Debug, Default, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
#[allow(
    clippy::struct_field_names,
    reason = "the _ms suffix keeps units explicit in the serialized evidence contract"
)]
pub struct LoadingObservation {
    pub wasm_started_ms: f64,
    pub webgpu_ready_ms: Option<f64>,
    pub scene_ready_ms: Option<f64>,
}

#[derive(Clone, Debug, Default, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct BrowserMemoryObservation {
    pub wasm_linear_bytes: Option<u64>,
    pub js_heap_used_bytes: Option<u64>,
    pub js_heap_limit_bytes: Option<u64>,
    pub js_heap_api_available: bool,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct FrameDistribution {
    pub warmup_samples: u32,
    pub samples: u32,
    pub duration_ms: f64,
    pub mean_ms: f64,
    pub p50_ms: f64,
    pub p95_ms: f64,
    pub p99_ms: f64,
    pub max_ms: f64,
    pub mean_fps: f64,
    pub one_percent_low_fps: f64,
    pub hitches_over_16_7_ms: u32,
    pub hitches_over_33_3_ms: u32,
}

impl FrameDistribution {
    #[must_use]
    pub fn from_samples(warmup_samples: usize, samples_ms: &[f64]) -> Option<Self> {
        let mut sorted = validated_samples(samples_ms)?;
        sorted.sort_by(f64::total_cmp);
        let sample_count = u32::try_from(sorted.len()).ok()?;
        let duration_ms = sorted.iter().sum::<f64>();
        let mean_ms = duration_ms / f64::from(sample_count);
        let slowest_count = sorted.len().div_ceil(100).max(1);
        let slowest_count_u32 = u32::try_from(slowest_count).ok()?;
        let slowest_mean_ms =
            sorted.iter().rev().take(slowest_count).sum::<f64>() / f64::from(slowest_count_u32);

        Some(Self {
            warmup_samples: u32::try_from(warmup_samples).unwrap_or(u32::MAX),
            samples: sample_count,
            duration_ms,
            mean_ms,
            p50_ms: nearest_rank(&sorted, 50),
            p95_ms: nearest_rank(&sorted, 95),
            p99_ms: nearest_rank(&sorted, 99),
            max_ms: *sorted.last()?,
            mean_fps: 1_000.0 / mean_ms,
            one_percent_low_fps: 1_000.0 / slowest_mean_ms,
            hitches_over_16_7_ms: count_over(&sorted, 16.7),
            hitches_over_33_3_ms: count_over(&sorted, 33.3),
        })
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct LatencyDistribution {
    pub samples: u32,
    pub mean_ms: f64,
    pub p50_ms: f64,
    pub p95_ms: f64,
    pub p99_ms: f64,
    pub max_ms: f64,
}

impl LatencyDistribution {
    #[must_use]
    pub fn from_samples(samples_ms: &[f64]) -> Option<Self> {
        let mut sorted = validated_samples(samples_ms)?;
        sorted.sort_by(f64::total_cmp);
        let sample_count = u32::try_from(sorted.len()).ok()?;
        Some(Self {
            samples: sample_count,
            mean_ms: sorted.iter().sum::<f64>() / f64::from(sample_count),
            p50_ms: nearest_rank(&sorted, 50),
            p95_ms: nearest_rank(&sorted, 95),
            p99_ms: nearest_rank(&sorted, 99),
            max_ms: *sorted.last()?,
        })
    }
}

#[derive(Clone, Debug, Default, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct InteractionObservation {
    pub handler_to_post_paint: Option<LatencyDistribution>,
    pub stable_canvas_observations: u32,
    pub unstable_canvas_observations: u32,
    #[serde(skip)]
    samples_ms: Vec<f64>,
}

impl InteractionObservation {
    pub fn record(&mut self, latency_ms: f64, canvas_stable: bool) {
        if latency_ms.is_finite() && latency_ms > 0.0 {
            self.samples_ms.push(latency_ms);
            self.handler_to_post_paint = LatencyDistribution::from_samples(&self.samples_ms);
        }
        if canvas_stable {
            self.stable_canvas_observations = self.stable_canvas_observations.saturating_add(1);
        } else {
            self.unstable_canvas_observations = self.unstable_canvas_observations.saturating_add(1);
        }
    }
}

#[derive(Clone, Debug, Default, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ViewportObservation {
    pub starts: u32,
    pub canvas_width_px: Option<u32>,
    pub canvas_height_px: Option<u32>,
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum DeniedCommandProbeStatus {
    #[default]
    NotRun,
    BrowserBackendAbsent,
    ExplicitlyDenied,
    UnexpectedlyAllowed,
    ProbeFailed,
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum AllowedCommandProbeStatus {
    #[default]
    NotRun,
    Succeeded,
    Failed,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AllowedPermissionObservation {
    pub command: String,
    pub status: AllowedCommandProbeStatus,
    pub reason: Option<String>,
}

impl Default for AllowedPermissionObservation {
    fn default() -> Self {
        Self {
            command: "execute_studio_command".to_owned(),
            status: AllowedCommandProbeStatus::NotRun,
            reason: None,
        }
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct DeniedPermissionObservation {
    pub command: String,
    pub status: DeniedCommandProbeStatus,
    pub reason: Option<String>,
}

impl Default for DeniedPermissionObservation {
    fn default() -> Self {
        Self {
            command: TAURI_DENIED_COMMAND.to_owned(),
            status: DeniedCommandProbeStatus::NotRun,
            reason: None,
        }
    }
}

#[derive(Clone, Debug, Default, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PermissionsObservation {
    pub allowed: AllowedPermissionObservation,
    pub denied: DeniedPermissionObservation,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Gate0StudioLedger {
    pub schema: String,
    pub schema_version: u32,
    pub recorded_at_utc: Option<String>,
    pub target: StudioTelemetryTarget,
    pub webgpu: WebGpuObservation,
    pub loading: LoadingObservation,
    pub memory: BrowserMemoryObservation,
    pub frame: Option<FrameDistribution>,
    pub interaction: InteractionObservation,
    pub viewport: ViewportObservation,
    pub permissions: PermissionsObservation,
    pub limitations: Vec<String>,
}

impl Gate0StudioLedger {
    #[must_use]
    pub fn new(target: StudioTelemetryTarget, wasm_started_ms: f64) -> Self {
        Self {
            schema: "forge.gate0-studio-telemetry".to_owned(),
            schema_version: 1,
            recorded_at_utc: None,
            target,
            webgpu: WebGpuObservation::default(),
            loading: LoadingObservation {
                wasm_started_ms,
                ..LoadingObservation::default()
            },
            memory: BrowserMemoryObservation::default(),
            frame: None,
            interaction: InteractionObservation::default(),
            viewport: ViewportObservation::default(),
            permissions: PermissionsObservation::default(),
            limitations: vec![
                "Frame time is measured from the Bevy WASM update loop, not a GPU timestamp query."
                    .to_owned(),
                "performance.memory is Chromium-specific and may be unavailable or quantized."
                    .to_owned(),
            ],
        }
    }
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ViewportReadyEvent {
    pub recorded_at_utc: String,
    pub first_real_frame_ms: f64,
    pub viewport: ViewportObservation,
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RuntimeTelemetryEvent {
    pub recorded_at_utc: String,
    pub first_real_frame_ms: f64,
    pub memory: BrowserMemoryObservation,
    pub frame: FrameDistribution,
    pub viewport: ViewportObservation,
}

fn validated_samples(samples: &[f64]) -> Option<Vec<f64>> {
    if samples.is_empty()
        || samples
            .iter()
            .any(|sample| !sample.is_finite() || *sample <= 0.0)
    {
        return None;
    }
    Some(samples.to_vec())
}

fn nearest_rank(sorted: &[f64], percentile: usize) -> f64 {
    let rank = sorted.len().saturating_mul(percentile).div_ceil(100);
    sorted[rank.saturating_sub(1).min(sorted.len() - 1)]
}

fn count_over(sorted: &[f64], threshold_ms: f64) -> u32 {
    u32::try_from(
        sorted
            .iter()
            .filter(|sample| **sample > threshold_ms)
            .count(),
    )
    .unwrap_or(u32::MAX)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn assert_approx_eq(actual: f64, expected: f64) {
        let tolerance = f64::EPSILON * expected.abs().max(1.0) * 8.0;
        assert!(
            (actual - expected).abs() <= tolerance,
            "expected {expected}, got {actual} (tolerance {tolerance})"
        );
    }

    #[test]
    fn probe_mode_requires_the_exact_gate0_parameter() {
        assert_eq!(
            parse_gate0_probe("?build=final&forge_gate0_probe=webgpu-unavailable"),
            Gate0ProbeMode::WebGpuUnavailable
        );
        assert_eq!(
            parse_gate0_probe("?forge_gate0_probe=tauri-permission"),
            Gate0ProbeMode::TauriPermission
        );
        assert_eq!(
            parse_gate0_probe("?forge_gate0_probe=unknown"),
            Gate0ProbeMode::None
        );
        assert_eq!(
            parse_gate0_probe("?forge_gate0_probe_extra=webgpu-unavailable"),
            Gate0ProbeMode::None
        );
    }

    #[test]
    fn acl_denial_requires_the_exact_tauri_verdict() {
        assert!(is_explicit_acl_denial(
            "Commande Tauri échouée : Command plugin:window|set_title not allowed by ACL"
        ));
        assert!(!is_explicit_acl_denial(
            "Commande Tauri échouée : Command plugin:window|close not allowed by ACL"
        ));
        assert!(!is_explicit_acl_denial(
            "Commande Tauri échouée : Command plugin:window|set_title denied"
        ));
        assert!(!is_explicit_acl_denial(
            "Command plugin:window|set_title not allowed by ACL"
        ));
        assert!(!is_explicit_acl_denial(
            "Commande Tauri échouée : Command plugin:window|set_title not allowed by ACL (cached)"
        ));
        assert!(!is_explicit_acl_denial(
            "window.set_title explicitly denied on origin local"
        ));
    }

    #[test]
    fn frame_distribution_uses_nearest_rank_and_slowest_one_percent() {
        let samples: Vec<f64> = (1..=100).map(f64::from).collect();
        let distribution = FrameDistribution::from_samples(60, &samples).unwrap();
        assert_eq!(distribution.samples, 100);
        assert_approx_eq(distribution.p50_ms, 50.0);
        assert_approx_eq(distribution.p95_ms, 95.0);
        assert_approx_eq(distribution.p99_ms, 99.0);
        assert_approx_eq(distribution.max_ms, 100.0);
        assert_approx_eq(distribution.one_percent_low_fps, 10.0);
        assert_eq!(distribution.hitches_over_33_3_ms, 67);
    }

    #[test]
    fn invalid_frame_samples_are_rejected() {
        assert!(FrameDistribution::from_samples(0, &[]).is_none());
        assert!(FrameDistribution::from_samples(0, &[16.0, 0.0]).is_none());
        assert!(FrameDistribution::from_samples(0, &[f64::NAN]).is_none());
    }

    #[test]
    fn interaction_distribution_and_canvas_stability_are_recorded() {
        let mut observation = InteractionObservation::default();
        observation.record(5.0, true);
        observation.record(7.0, true);
        observation.record(11.0, false);
        let distribution = observation.handler_to_post_paint.unwrap();
        assert_eq!(distribution.samples, 3);
        assert_approx_eq(distribution.p95_ms, 11.0);
        assert_eq!(observation.stable_canvas_observations, 2);
        assert_eq!(observation.unstable_canvas_observations, 1);
    }

    #[test]
    fn ledger_serialization_omits_raw_interaction_samples() {
        let mut ledger = Gate0StudioLedger::new(StudioTelemetryTarget::WasmBrowser, 12.5);
        ledger.interaction.record(3.0, true);
        ledger.permissions.allowed.status = AllowedCommandProbeStatus::Succeeded;
        ledger.permissions.allowed.reason = Some("typed command accepted".to_owned());
        ledger.permissions.denied.status = DeniedCommandProbeStatus::ExplicitlyDenied;
        ledger.permissions.denied.reason = Some("explicitly denied by ACL".to_owned());
        let encoded = serde_json::to_string(&ledger).unwrap();
        assert!(encoded.contains("forge.gate0-studio-telemetry"));
        assert!(!encoded.contains("samples_ms"));

        let value: serde_json::Value = serde_json::from_str(&encoded).unwrap();
        assert_eq!(
            value["permissions"]["allowed"]["command"],
            "execute_studio_command"
        );
        assert_eq!(value["permissions"]["allowed"]["status"], "succeeded");
        assert_eq!(
            value["permissions"]["denied"]["command"],
            "plugin:window|set_title"
        );
        assert_eq!(
            value["permissions"]["denied"]["status"],
            "explicitly_denied"
        );
        assert!(value["interaction"].get("input_to_next_paint").is_none());
        assert!(value["interaction"].get("handler_to_post_paint").is_some());
    }
}
