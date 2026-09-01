use bevy::{
    diagnostic::DiagnosticsStore,
    prelude::*,
    render::{
        Render, RenderApp, RenderSystems,
        diagnostic::MeshAllocatorDiagnosticPlugin,
        render_resource::{CachedPipelineState, PipelineCache},
        renderer::RenderDevice,
    },
};
use serde::Serialize;
use std::sync::{Arc, Mutex, PoisonError};

pub const PIPELINE_WARMUP_STABLE_FRAMES: u32 = 30;

const WGPU_ALLOCATOR_SCOPE: &str = "current_wgpu_device_suballocations_not_total_vram_or_os_budget";
const MESH_ALLOCATOR_SCOPE: &str = "bevy_mesh_allocator_slab_capacity_only_not_total_gpu_memory";
const PIPELINE_WARMUP_SCOPE: &str =
    "pipelines_exercised_by_this_process_only_no_persistent_pipeline_cache";

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum WgpuAllocatorReportStatus {
    Observed,
    UnsupportedOrUnavailable,
    RenderDeviceUnavailable,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
pub struct GpuMemoryObservation {
    pub wgpu_allocator_report_status: WgpuAllocatorReportStatus,
    pub wgpu_allocator_allocated_bytes: Option<u64>,
    pub wgpu_allocator_reserved_bytes: Option<u64>,
    pub mesh_allocator_slabs_bytes: Option<u64>,
    pub wgpu_allocator_scope: &'static str,
    pub mesh_allocator_scope: &'static str,
}

impl GpuMemoryObservation {
    fn from_allocator_totals(
        render_device_available: bool,
        allocator_totals: Option<(u64, u64)>,
        mesh_allocator_slabs_bytes: Option<u64>,
    ) -> Result<Self, String> {
        let (status, allocated_bytes, reserved_bytes) = match allocator_totals {
            Some((allocated, reserved)) if allocated <= reserved => (
                WgpuAllocatorReportStatus::Observed,
                Some(allocated),
                Some(reserved),
            ),
            Some((allocated, reserved)) => {
                return Err(format!(
                    "wgpu allocator report is inconsistent: {allocated} allocated bytes exceed {reserved} reserved bytes"
                ));
            }
            None if render_device_available => (
                WgpuAllocatorReportStatus::UnsupportedOrUnavailable,
                None,
                None,
            ),
            None => (
                WgpuAllocatorReportStatus::RenderDeviceUnavailable,
                None,
                None,
            ),
        };

        Ok(Self {
            wgpu_allocator_report_status: status,
            wgpu_allocator_allocated_bytes: allocated_bytes,
            wgpu_allocator_reserved_bytes: reserved_bytes,
            mesh_allocator_slabs_bytes,
            wgpu_allocator_scope: WGPU_ALLOCATOR_SCOPE,
            mesh_allocator_scope: MESH_ALLOCATOR_SCOPE,
        })
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum PipelineWarmupStatus {
    NotObserved,
    WaitingForPipelines,
    Warming,
    Ready,
    Failed,
}

#[derive(Clone, Debug, Eq, PartialEq, Resource, Serialize)]
pub struct PipelineWarmupObservation {
    pub status: PipelineWarmupStatus,
    pub total: usize,
    pub queued: usize,
    pub creating: usize,
    pub ready: usize,
    pub errors: usize,
    pub waiting: usize,
    pub stable_frames: u32,
    pub required_stable_frames: u32,
    pub observed_render_frames: u64,
    pub stabilized_at_render_frame: Option<u64>,
    pub scope: &'static str,
}

impl Default for PipelineWarmupObservation {
    fn default() -> Self {
        Self {
            status: PipelineWarmupStatus::NotObserved,
            total: 0,
            queued: 0,
            creating: 0,
            ready: 0,
            errors: 0,
            waiting: 0,
            stable_frames: 0,
            required_stable_frames: PIPELINE_WARMUP_STABLE_FRAMES,
            observed_render_frames: 0,
            stabilized_at_render_frame: None,
            scope: PIPELINE_WARMUP_SCOPE,
        }
    }
}

impl PipelineWarmupObservation {
    #[must_use]
    pub const fn is_ready(&self) -> bool {
        matches!(self.status, PipelineWarmupStatus::Ready)
    }
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
pub struct PipelineWarmupReport {
    pub ready_before_measurement: bool,
    pub ready_for_entire_measurement: bool,
    pub final_observation: PipelineWarmupObservation,
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
struct PipelineStateCounts {
    total: usize,
    queued: usize,
    creating: usize,
    ready: usize,
    errors: usize,
    waiting: usize,
}

impl PipelineStateCounts {
    fn from_cache(cache: &PipelineCache) -> Self {
        let mut counts = Self::default();
        for pipeline in cache.pipelines() {
            counts.total = counts.total.saturating_add(1);
            match &pipeline.state {
                CachedPipelineState::Queued => {
                    counts.queued = counts.queued.saturating_add(1);
                }
                CachedPipelineState::Creating(_) => {
                    counts.creating = counts.creating.saturating_add(1);
                }
                CachedPipelineState::Ok(_) => {
                    counts.ready = counts.ready.saturating_add(1);
                }
                CachedPipelineState::Err(_) => {
                    counts.errors = counts.errors.saturating_add(1);
                }
            }
        }
        counts.waiting = cache.waiting_pipelines().count();
        counts
    }
}

fn advance_pipeline_warmup(
    previous: &PipelineWarmupObservation,
    counts: PipelineStateCounts,
) -> PipelineWarmupObservation {
    let observed_render_frames = previous.observed_render_frames.saturating_add(1);
    let settled = counts.total > 0
        && counts.queued == 0
        && counts.creating == 0
        && counts.errors == 0
        && counts.waiting == 0
        && counts.ready == counts.total;
    let previously_settled = previous.total > 0
        && previous.queued == 0
        && previous.creating == 0
        && previous.errors == 0
        && previous.waiting == 0
        && previous.ready == previous.total;
    let stable_frames = if settled {
        if previously_settled && previous.total == counts.total {
            previous.stable_frames.saturating_add(1)
        } else {
            1
        }
    } else {
        0
    };
    let status = if counts.errors > 0 {
        PipelineWarmupStatus::Failed
    } else if counts.total == 0 {
        PipelineWarmupStatus::WaitingForPipelines
    } else if stable_frames >= PIPELINE_WARMUP_STABLE_FRAMES {
        PipelineWarmupStatus::Ready
    } else {
        PipelineWarmupStatus::Warming
    };
    let stabilized_at_render_frame = if matches!(status, PipelineWarmupStatus::Ready) {
        if matches!(previous.status, PipelineWarmupStatus::Ready) && previous.total == counts.total
        {
            previous.stabilized_at_render_frame
        } else {
            Some(observed_render_frames)
        }
    } else {
        None
    };

    PipelineWarmupObservation {
        status,
        total: counts.total,
        queued: counts.queued,
        creating: counts.creating,
        ready: counts.ready,
        errors: counts.errors,
        waiting: counts.waiting,
        stable_frames,
        required_stable_frames: PIPELINE_WARMUP_STABLE_FRAMES,
        observed_render_frames,
        stabilized_at_render_frame,
        scope: PIPELINE_WARMUP_SCOPE,
    }
}

#[derive(Clone, Resource)]
struct MainPipelineWarmupBridge(Arc<Mutex<PipelineWarmupObservation>>);

#[derive(Clone, Resource)]
struct RenderPipelineWarmupBridge(Arc<Mutex<PipelineWarmupObservation>>);

pub(crate) struct GpuPerformancePlugin;

impl Plugin for GpuPerformancePlugin {
    fn build(&self, app: &mut App) {
        let shared = Arc::new(Mutex::new(PipelineWarmupObservation::default()));
        app.insert_resource(PipelineWarmupObservation::default())
            .insert_resource(MainPipelineWarmupBridge(Arc::clone(&shared)))
            .add_plugins(MeshAllocatorDiagnosticPlugin)
            .add_systems(PreUpdate, sync_pipeline_warmup_observation);

        if let Some(render_app) = app.get_sub_app_mut(RenderApp) {
            render_app
                .insert_resource(RenderPipelineWarmupBridge(shared))
                .add_systems(Render, observe_pipeline_cache.after(RenderSystems::Render));
        }
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy render systems require owned system parameter wrappers"
)]
fn observe_pipeline_cache(cache: Res<PipelineCache>, bridge: Res<RenderPipelineWarmupBridge>) {
    let mut observation = bridge.0.lock().unwrap_or_else(PoisonError::into_inner);
    *observation = advance_pipeline_warmup(&observation, PipelineStateCounts::from_cache(&cache));
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn sync_pipeline_warmup_observation(
    bridge: Res<MainPipelineWarmupBridge>,
    mut observation: ResMut<PipelineWarmupObservation>,
) {
    let latest = bridge.0.lock().unwrap_or_else(PoisonError::into_inner);
    if *observation != *latest {
        observation.clone_from(&latest);
    }
}

pub(crate) fn collect_gpu_memory_observation(
    render_device: Option<&RenderDevice>,
    diagnostics: &DiagnosticsStore,
) -> Result<GpuMemoryObservation, String> {
    let allocator_totals = render_device.and_then(|device| {
        device
            .wgpu_device()
            .generate_allocator_report()
            .map(|report| (report.total_allocated_bytes, report.total_reserved_bytes))
    });
    let mesh_allocator_slabs_bytes = diagnostics
        .get(MeshAllocatorDiagnosticPlugin::slabs_size_diagnostic_path())
        .and_then(bevy::diagnostic::Diagnostic::value)
        .and_then(finite_non_negative_integer);

    GpuMemoryObservation::from_allocator_totals(
        render_device.is_some(),
        allocator_totals,
        mesh_allocator_slabs_bytes,
    )
}

pub(crate) fn qualify_pipeline_warmup(
    ready_before_measurement: Option<bool>,
    ready_for_entire_measurement: bool,
    final_observation: &PipelineWarmupObservation,
) -> Result<PipelineWarmupReport, String> {
    if ready_before_measurement != Some(true) {
        return Err(format!(
            "pipeline cache was not stable for {} render frames before performance sampling",
            final_observation.required_stable_frames
        ));
    }
    if !ready_for_entire_measurement {
        return Err("pipeline cache left the ready state during performance sampling".to_owned());
    }
    if !final_observation.is_ready() {
        return Err(format!(
            "pipeline cache is not ready at report time: {:?}",
            final_observation.status
        ));
    }
    if final_observation.total == 0
        || final_observation.queued != 0
        || final_observation.creating != 0
        || final_observation.errors != 0
        || final_observation.waiting != 0
        || final_observation.ready != final_observation.total
        || final_observation.required_stable_frames != PIPELINE_WARMUP_STABLE_FRAMES
        || final_observation.stable_frames < final_observation.required_stable_frames
        || u64::from(final_observation.stable_frames) > final_observation.observed_render_frames
    {
        return Err(format!(
            "pipeline cache ready observation is structurally inconsistent: {final_observation:?}"
        ));
    }
    let stable_frames_after_qualification = u64::from(
        final_observation
            .stable_frames
            .saturating_sub(final_observation.required_stable_frames),
    );
    let expected_stabilized_at = final_observation
        .observed_render_frames
        .checked_sub(stable_frames_after_qualification);
    if final_observation.stabilized_at_render_frame != expected_stabilized_at {
        return Err(format!(
            "pipeline cache ready observation has an inconsistent stabilization frame: expected {expected_stabilized_at:?}, observed {:?}",
            final_observation.stabilized_at_render_frame
        ));
    }

    Ok(PipelineWarmupReport {
        ready_before_measurement: true,
        ready_for_entire_measurement: true,
        final_observation: final_observation.clone(),
    })
}

fn finite_non_negative_integer(value: f64) -> Option<u64> {
    (value.is_finite() && value >= 0.0 && value.fract() == 0.0)
        .then(|| format!("{value:.0}").parse().ok())
        .flatten()
}

#[cfg(test)]
mod tests {
    use super::*;

    fn settled_counts(total: usize) -> PipelineStateCounts {
        PipelineStateCounts {
            total,
            ready: total,
            ..default()
        }
    }

    #[test]
    fn unavailable_allocator_report_stays_null_instead_of_becoming_zero() {
        let observation =
            GpuMemoryObservation::from_allocator_totals(true, None, Some(4_096)).unwrap();

        assert_eq!(
            observation.wgpu_allocator_report_status,
            WgpuAllocatorReportStatus::UnsupportedOrUnavailable
        );
        assert_eq!(observation.wgpu_allocator_allocated_bytes, None);
        assert_eq!(observation.wgpu_allocator_reserved_bytes, None);
        assert_eq!(observation.mesh_allocator_slabs_bytes, Some(4_096));
        let serialized = serde_json::to_value(&observation).unwrap();
        assert!(serialized["wgpu_allocator_allocated_bytes"].is_null());
        assert!(serialized["wgpu_allocator_reserved_bytes"].is_null());
    }

    #[test]
    fn allocator_report_distinguishes_missing_device_from_observed_totals() {
        let missing_device =
            GpuMemoryObservation::from_allocator_totals(false, None, None).unwrap();
        assert_eq!(
            missing_device.wgpu_allocator_report_status,
            WgpuAllocatorReportStatus::RenderDeviceUnavailable
        );
        assert_eq!(missing_device.wgpu_allocator_allocated_bytes, None);
        assert_eq!(missing_device.wgpu_allocator_reserved_bytes, None);

        let observed =
            GpuMemoryObservation::from_allocator_totals(true, Some((4_096, 8_192)), None).unwrap();
        assert_eq!(
            observed.wgpu_allocator_report_status,
            WgpuAllocatorReportStatus::Observed
        );
        assert_eq!(observed.wgpu_allocator_allocated_bytes, Some(4_096));
        assert_eq!(observed.wgpu_allocator_reserved_bytes, Some(8_192));
    }

    #[test]
    fn allocator_report_rejects_allocated_bytes_above_reserved_bytes() {
        let error = GpuMemoryObservation::from_allocator_totals(true, Some((8_193, 8_192)), None)
            .unwrap_err();

        assert!(error.contains("allocated bytes exceed"));
    }

    #[test]
    fn pipeline_cache_requires_thirty_stable_render_frames() {
        let mut observation = PipelineWarmupObservation::default();
        for stable_frame in 1..PIPELINE_WARMUP_STABLE_FRAMES {
            observation = advance_pipeline_warmup(&observation, settled_counts(7));
            assert_eq!(observation.stable_frames, stable_frame);
            assert_eq!(observation.status, PipelineWarmupStatus::Warming);
        }

        observation = advance_pipeline_warmup(&observation, settled_counts(7));

        assert_eq!(observation.stable_frames, PIPELINE_WARMUP_STABLE_FRAMES);
        assert_eq!(observation.status, PipelineWarmupStatus::Ready);
        assert_eq!(observation.stabilized_at_render_frame, Some(30));
    }

    #[test]
    fn new_or_pending_pipeline_resets_stability() {
        let mut observation = PipelineWarmupObservation::default();
        for _ in 0..PIPELINE_WARMUP_STABLE_FRAMES {
            observation = advance_pipeline_warmup(&observation, settled_counts(4));
        }
        assert!(observation.is_ready());

        observation = advance_pipeline_warmup(
            &observation,
            PipelineStateCounts {
                total: 5,
                ready: 4,
                creating: 1,
                waiting: 1,
                ..default()
            },
        );
        assert_eq!(observation.status, PipelineWarmupStatus::Warming);
        assert_eq!(observation.stable_frames, 0);
        assert_eq!(observation.stabilized_at_render_frame, None);

        observation = advance_pipeline_warmup(&observation, settled_counts(5));
        assert_eq!(observation.status, PipelineWarmupStatus::Warming);
        assert_eq!(observation.stable_frames, 1);
    }

    #[test]
    fn empty_and_failed_caches_never_qualify() {
        let empty = advance_pipeline_warmup(
            &PipelineWarmupObservation::default(),
            PipelineStateCounts::default(),
        );
        assert_eq!(empty.status, PipelineWarmupStatus::WaitingForPipelines);

        let failed = advance_pipeline_warmup(
            &empty,
            PipelineStateCounts {
                total: 1,
                errors: 1,
                ..default()
            },
        );
        assert_eq!(failed.status, PipelineWarmupStatus::Failed);
        assert!(!failed.is_ready());
    }

    #[test]
    fn pipeline_qualification_rejects_sampling_before_or_during_warmup() {
        let mut ready = PipelineWarmupObservation::default();
        for _ in 0..PIPELINE_WARMUP_STABLE_FRAMES {
            ready = advance_pipeline_warmup(&ready, settled_counts(2));
        }

        assert!(qualify_pipeline_warmup(Some(false), true, &ready).is_err());
        assert!(qualify_pipeline_warmup(Some(true), false, &ready).is_err());
        assert!(qualify_pipeline_warmup(Some(true), true, &ready).is_ok());
    }

    #[test]
    fn pipeline_qualification_revalidates_ready_counters_and_timing() {
        let mut ready = PipelineWarmupObservation::default();
        for _ in 0..PIPELINE_WARMUP_STABLE_FRAMES {
            ready = advance_pipeline_warmup(&ready, settled_counts(2));
        }

        let mut empty_ready = ready.clone();
        empty_ready.total = 0;
        empty_ready.ready = 0;
        assert!(qualify_pipeline_warmup(Some(true), true, &empty_ready).is_err());

        let mut impossible_timing = ready;
        impossible_timing.observed_render_frames = 60;
        impossible_timing.stabilized_at_render_frame = Some(42);
        assert!(qualify_pipeline_warmup(Some(true), true, &impossible_timing).is_err());
    }

    #[test]
    fn finite_integer_conversion_rejects_missing_or_fractional_bytes() {
        assert_eq!(finite_non_negative_integer(8_192.0), Some(8_192));
        assert_eq!(finite_non_negative_integer(-1.0), None);
        assert_eq!(finite_non_negative_integer(1.5), None);
        assert_eq!(finite_non_negative_integer(f64::NAN), None);
    }
}
