//! Opt-in main-thread phase timings. No GPU timings or readiness decisions.
use bevy::prelude::*;
use serde::Serialize;

#[derive(Clone, Copy, Debug, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum QualityCpuPhase {
    TerrainPrepare,
    TerrainCommit,
    VegetationPrepare,
    VegetationCommit,
    GovernedForestInclusive,
    GovernedFarBatches,
}

impl QualityCpuPhase {
    const fn index(self) -> usize {
        self as usize
    }
}

/// Missing samples stay null. Forest-inclusive contains far-batch work: do not sum them.
#[derive(Clone, Debug, Default, Resource, Serialize)]
pub struct QualityCpuFrame {
    pub milliseconds: [Option<f64>; 6],
}

impl QualityCpuFrame {
    pub(crate) fn record(&mut self, phase: QualityCpuPhase, milliseconds: f64) {
        if milliseconds.is_finite() && milliseconds >= 0.0 {
            *self.milliseconds[phase.index()].get_or_insert(0.0) += milliseconds;
        }
    }

    #[must_use]
    pub fn phase_names() -> [QualityCpuPhase; 6] {
        [
            QualityCpuPhase::TerrainPrepare,
            QualityCpuPhase::TerrainCommit,
            QualityCpuPhase::VegetationPrepare,
            QualityCpuPhase::VegetationCommit,
            QualityCpuPhase::GovernedForestInclusive,
            QualityCpuPhase::GovernedFarBatches,
        ]
    }
}

pub struct QualityCpuTracePlugin;

impl Plugin for QualityCpuTracePlugin {
    fn build(&self, app: &mut App) {
        app.init_resource::<QualityCpuFrame>().add_systems(
            First,
            |mut frame: ResMut<QualityCpuFrame>| {
                *frame = QualityCpuFrame::default();
            },
        );
    }
}

#[cfg(any(feature = "physics-rapier", test))]
pub(crate) fn record_elapsed(
    frame: Option<&mut QualityCpuFrame>,
    phase: QualityCpuPhase,
    started: bevy::platform::time::Instant,
) {
    if let Some(frame) = frame {
        frame.record(phase, started.elapsed().as_secs_f64() * 1000.0);
    }
}

pub(crate) fn measure<T>(
    world: &mut World,
    phase: QualityCpuPhase,
    operation: impl FnOnce(&mut World) -> T,
) -> T {
    if !world.contains_resource::<QualityCpuFrame>() {
        return operation(world);
    }
    let started = bevy::platform::time::Instant::now();
    let result = operation(world);
    let elapsed = started.elapsed().as_secs_f64() * 1000.0;
    world
        .resource_mut::<QualityCpuFrame>()
        .record(phase, elapsed);
    result
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn missing_invalid_and_nested_samples_are_not_false_totals() {
        let mut frame = QualityCpuFrame::default();
        frame.record(QualityCpuPhase::TerrainPrepare, f64::NAN);
        frame.record(QualityCpuPhase::TerrainPrepare, -1.0);
        assert!(frame.milliseconds.iter().all(Option::is_none));
        frame.record(QualityCpuPhase::GovernedFarBatches, 2.0);
        frame.record(QualityCpuPhase::GovernedForestInclusive, 3.0);
        frame.record(QualityCpuPhase::GovernedForestInclusive, 4.0);
        assert_eq!(frame.milliseconds[4].unwrap().to_bits(), 7.0_f64.to_bits());
        assert_eq!(frame.milliseconds[5].unwrap().to_bits(), 2.0_f64.to_bits());
    }

    #[test]
    fn optional_trace_preserves_results_and_resets_each_frame() {
        let mut app = App::new();
        let result: Result<(), &str> =
            measure(app.world_mut(), QualityCpuPhase::TerrainCommit, |_| {
                Err("kept")
            });
        assert_eq!(result, Err("kept"));
        assert!(!app.world().contains_resource::<QualityCpuFrame>());
        app.add_plugins(QualityCpuTracePlugin);
        assert_eq!(
            measure(app.world_mut(), QualityCpuPhase::TerrainCommit, |_| 42),
            42
        );
        assert!(app.world().resource::<QualityCpuFrame>().milliseconds[1].is_some());
        app.update();
        assert!(
            app.world()
                .resource::<QualityCpuFrame>()
                .milliseconds
                .iter()
                .all(Option::is_none)
        );
    }
}
