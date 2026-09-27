//! Backend allocator accounting, not resident VRAM or total process GPU memory.
use crate::GpuDiagnostic;
use bevy::{diagnostic::FrameCount, prelude::*, render::renderer::RenderDevice};
use serde::Serialize;
use std::time::Duration;

const SAMPLE_INTERVAL: Duration = Duration::from_secs(5);

#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct GpuAllocatorTotals {
    pub allocated_bytes: u64,
    pub reserved_bytes: u64,
    pub allocation_count: usize,
    pub block_count: usize,
}

#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct GpuMemoryDiagnostic {
    pub sampled_frame: u32,
    pub elapsed_seconds: f64,
    pub scope: &'static str,
    pub status: &'static str,
    /// None is unavailable, never zero usage. Reserved includes allocator free space.
    pub allocator: Option<GpuAllocatorTotals>,
}

impl GpuMemoryDiagnostic {
    fn observed(frame: u32, elapsed: Duration, allocator: Option<GpuAllocatorTotals>) -> Self {
        Self {
            sampled_frame: frame,
            elapsed_seconds: elapsed.as_secs_f64(),
            scope: "backend_allocator_not_resident_vram_or_process_total",
            status: if allocator.is_some() {
                "available"
            } else {
                "backend_report_unavailable"
            },
            allocator,
        }
    }
}

#[derive(Default)]
pub(super) struct SampleClock(Option<Duration>);

impl SampleClock {
    fn due(&mut self, now: Duration) -> bool {
        if self
            .0
            .is_some_and(|last| now.saturating_sub(last) < SAMPLE_INTERVAL)
        {
            return false;
        }
        self.0 = Some(now);
        true
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
pub(super) fn collect(
    device: Option<Res<RenderDevice>>,
    diagnostic: Option<ResMut<GpuDiagnostic>>,
    time: Res<Time<Real>>,
    frame: Res<FrameCount>,
    mut clock: Local<SampleClock>,
) {
    let (Some(device), Some(mut diagnostic)) = (device, diagnostic) else {
        return;
    };
    if !clock.due(time.elapsed()) {
        return;
    }
    // Do not retain allocation labels or the per-allocation report in exported diagnostics.
    let totals = device
        .wgpu_device()
        .generate_allocator_report()
        .map(|report| GpuAllocatorTotals {
            allocated_bytes: report.total_allocated_bytes,
            reserved_bytes: report.total_reserved_bytes,
            allocation_count: report.allocations.len(),
            block_count: report.blocks.len(),
        });
    diagnostic.memory = Some(GpuMemoryDiagnostic::observed(
        frame.0,
        time.elapsed(),
        totals,
    ));
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn unsupported_report_is_not_zero_and_next_sample_replaces_previous_totals() {
        let available = GpuMemoryDiagnostic::observed(
            20,
            Duration::from_secs(2),
            Some(GpuAllocatorTotals {
                allocated_bytes: 123,
                reserved_bytes: 256,
                allocation_count: 3,
                block_count: 1,
            }),
        );
        let json = serde_json::to_value(&available).unwrap();
        assert_eq!(json["allocator"]["allocated_bytes"], 123);
        assert_eq!(json["allocator"]["reserved_bytes"], 256);
        assert_eq!(json["status"], "available");
        let unavailable = GpuMemoryDiagnostic::observed(90, Duration::from_secs(7), None);
        let json = serde_json::to_value(&unavailable).unwrap();
        assert!(json["allocator"].is_null());
        assert_eq!(json["status"], "backend_report_unavailable");
        assert_eq!(json["sampled_frame"], 90);
        assert_eq!(available.scope, unavailable.scope);
    }

    #[test]
    fn reports_are_sampled_at_most_once_per_five_seconds() {
        let mut clock = SampleClock::default();
        assert!(clock.due(Duration::ZERO));
        for millis in [0, 1, 1000, 4999] {
            assert!(!clock.due(Duration::from_millis(millis)));
        }
        assert!(clock.due(Duration::from_secs(5)));
        assert!(!clock.due(Duration::from_secs(5)));
        assert!(clock.due(Duration::from_secs(10)));
    }
}
