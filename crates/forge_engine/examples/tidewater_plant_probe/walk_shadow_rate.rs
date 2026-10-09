//! Render-thread shadow cadence proof, sampled from its acknowledged statistics.
use bevy::{diagnostic::FrameCount, prelude::*};
use forge_engine::tidewater_object_shadow::graph::animation;

#[derive(Clone, Copy, Default, serde::Serialize)]
pub(super) struct Snapshot {
    pub main_frame: u32,
    pub wall: f64,
    pub submissions: u64,
    pub layers: [u64; 3],
    pub recovered: u64,
}

#[derive(Resource, Default, serde::Serialize)]
pub(super) struct Proof {
    pub start: Option<Snapshot>,
    pub end: Option<Snapshot>,
    frozen: bool,
    invalid: bool,
}

impl Proof {
    /// The controlled wind capture must leave enough live frames to measure cadence.
    /// Readiness does not require a passing cadence: an invalid result must still finish.
    pub fn window_ready(&self) -> bool {
        self.start.zip(self.end).is_some_and(|(start, end)| {
            end.main_frame
                .checked_sub(start.main_frame)
                .is_some_and(|frames| frames >= 120)
        })
    }

    pub fn complete(&self) -> bool {
        self.frozen
    }

    pub fn valid(&self) -> bool {
        !self.invalid
            && self.complete()
            && self
                .start
                .zip(self.end)
                .is_some_and(|(start, end)| valid(start, end))
    }

    // Smoke-test counters are bounded by the proof window before conversion to f64.
    #[allow(clippy::cast_precision_loss)]
    pub fn rates(&self) -> Option<[f64; 4]> {
        let (start, end) = (self.start?, self.end?);
        let frames = end.main_frame.checked_sub(start.main_frame)?;
        let submissions = end.submissions.checked_sub(start.submissions)?;
        let layers = [
            end.layers[0].checked_sub(start.layers[0])?,
            end.layers[1].checked_sub(start.layers[1])?,
            end.layers[2].checked_sub(start.layers[2])?,
        ];
        if frames == 0 || submissions == 0 {
            return None;
        }

        let submissions = submissions as f64;
        let layer_rates = [
            layers[0] as f64 / submissions,
            layers[1] as f64 / submissions,
            layers[2] as f64 / submissions,
        ];
        Some([
            submissions / f64::from(frames),
            layer_rates[0],
            layer_rates[1],
            layer_rates[2],
        ])
    }
}

#[cfg(test)]
mod window_readiness_tests {
    use super::{Proof, Snapshot};

    #[test]
    fn wind_capture_waits_for_full_live_window() {
        let mut proof = Proof {
            start: Some(Snapshot {
                main_frame: 561,
                ..Default::default()
            }),
            end: Some(Snapshot {
                main_frame: 671,
                ..Default::default()
            }),
            ..Default::default()
        };
        assert!(!proof.window_ready());
        proof.end.as_mut().unwrap().main_frame = 681;
        assert!(proof.window_ready());
        // Enough samples must allow reporting failed submission cadence, not hang.
        assert!(!proof.valid());
    }

    #[test]
    fn missing_or_reversed_window_is_not_ready() {
        let mut proof = Proof::default();
        assert!(!proof.window_ready());
        proof.start = Some(Snapshot {
            main_frame: 561,
            ..Default::default()
        });
        assert!(!proof.window_ready());
        proof.end = Some(Snapshot {
            main_frame: 560,
            ..Default::default()
        });
        assert!(!proof.window_ready());
    }
}

fn valid(start: Snapshot, end: Snapshot) -> bool {
    let Some(frames) = end.main_frame.checked_sub(start.main_frame) else {
        return false;
    };
    let Some(submissions) = end.submissions.checked_sub(start.submissions) else {
        return false;
    };
    let Some(layer0) = end.layers[0].checked_sub(start.layers[0]) else {
        return false;
    };
    let Some(layer1) = end.layers[1].checked_sub(start.layers[1]) else {
        return false;
    };
    let Some(layer2) = end.layers[2].checked_sub(start.layers[2]) else {
        return false;
    };
    if frames < 120 || submissions < 100 || end.recovered != start.recovered {
        return false;
    }

    // Smoke-test counters are bounded by the proof window before conversion to f64.
    #[allow(clippy::cast_precision_loss)]
    let submission_ratio = submissions as f64 / f64::from(frames);
    layer0 == submissions
        && (0.95..=1.05).contains(&submission_ratio)
        && layer1
            .checked_mul(2)
            .is_some_and(|doubled| doubled.abs_diff(submissions) <= 2)
        && layer2
            .checked_mul(4)
            .is_some_and(|quadrupled| quadrupled.abs_diff(submissions) <= 4)
}

pub(super) fn install(app: &mut App) {
    app.init_resource::<Proof>().add_systems(Update, observe);
}

fn observe(
    frame: Res<FrameCount>,
    time: Res<Time<Real>>,
    state: Res<animation::State>,
    wind: Res<super::wind::Proof>,
    mut proof: ResMut<Proof>,
) {
    if proof.frozen {
        return;
    }
    if wind.started {
        proof.frozen = true;
        proof.invalid |= proof.start.is_none() || proof.end.is_none();
        return;
    }

    let statistics = state.statistics();
    let wall = time.elapsed_secs_f64();
    if statistics.submissions < 60 || wall < 1.0 {
        return;
    }
    let snapshot = Snapshot {
        main_frame: frame.0,
        wall,
        submissions: statistics.submissions,
        layers: statistics.layers,
        recovered: statistics.recovered,
    };

    match proof.start {
        None => proof.start = Some(snapshot),
        Some(start) => {
            if snapshot.main_frame < start.main_frame
                || snapshot.submissions < start.submissions
                || snapshot
                    .layers
                    .iter()
                    .zip(start.layers)
                    .any(|(now, was)| *now < was)
                || snapshot.recovered < start.recovered
            {
                proof.invalid = true;
                return;
            }
            proof.end = Some(snapshot);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn snapshot(main_frame: u32, submissions: u64, layers: [u64; 3]) -> Snapshot {
        Snapshot {
            main_frame,
            wall: 0.0,
            submissions,
            layers,
            recovered: 0,
        }
    }

    #[test]
    fn rejects_previous_half_rate() {
        assert!(!valid(
            snapshot(0, 0, [0; 3]),
            snapshot(120, 60, [60, 30, 15])
        ));
    }

    #[test]
    fn accepts_full_source_cadence() {
        assert!(valid(
            snapshot(0, 0, [0; 3]),
            snapshot(120, 120, [120, 60, 30])
        ));
    }

    #[test]
    fn accepts_one_frame_and_submission_skew() {
        assert!(valid(
            snapshot(0, 0, [0; 3]),
            snapshot(121, 120, [120, 60, 30])
        ));
    }

    #[test]
    fn rejects_decreasing_generation_counters_without_panicking() {
        assert!(!valid(
            snapshot(120, 120, [120, 60, 30]),
            snapshot(121, 119, [119, 59, 29])
        ));
    }

    #[test]
    fn rejects_forced_all_layers() {
        assert!(!valid(
            snapshot(0, 0, [0; 3]),
            snapshot(120, 120, [120, 120, 120])
        ));
    }
}
