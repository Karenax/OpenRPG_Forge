//! Bounded diagnostic aggregates, independent of the qualification verdict.
use serde::Serialize;

#[derive(Clone, Debug, Serialize)]
pub struct SlowFrame {
    pub elapsed_seconds: f64,
    pub delta_ms: f64,
    pub previous_cpu_ms: [Option<f64>; 6],
}

#[derive(Default, Serialize)]
pub struct StageTrace {
    pub frames: u64,
    pub moving_frames: u64,
    pub first_observed_seconds: Option<f64>,
    pub first_cpu_ready_seconds: Option<f64>,
    pub first_gpu_ready_seconds: Option<f64>,
    pub maximum_cpu_call_ms: [Option<f64>; 6],
    pub slowest_frames: Vec<SlowFrame>,
}

impl StageTrace {
    pub fn observe(
        &mut self,
        frame: SlowFrame,
        current_cpu_ms: [Option<f64>; 6],
        moving: bool,
        cpu_ready: bool,
        gpu_ready: bool,
    ) {
        self.frames += 1;
        self.moving_frames += u64::from(moving);
        self.first_observed_seconds
            .get_or_insert(frame.elapsed_seconds);
        if cpu_ready {
            self.first_cpu_ready_seconds
                .get_or_insert(frame.elapsed_seconds);
        }
        if gpu_ready {
            self.first_gpu_ready_seconds
                .get_or_insert(frame.elapsed_seconds);
        }
        for (maximum, current) in self.maximum_cpu_call_ms.iter_mut().zip(current_cpu_ms) {
            if let Some(value) = current.filter(|v| v.is_finite() && *v >= 0.0) {
                *maximum = Some(maximum.map_or(value, |old| old.max(value)));
            }
        }
        if frame.delta_ms.is_finite() && frame.delta_ms >= 0.0 {
            self.slowest_frames.push(frame);
            self.slowest_frames
                .sort_by(|a, b| b.delta_ms.total_cmp(&a.delta_ms));
            self.slowest_frames.truncate(8);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn bounded_trace_retains_early_hitch_and_missing_cpu_is_not_zero() {
        let mut trace = StageTrace::default();
        for i in 0..1000 {
            trace.observe(
                SlowFrame {
                    elapsed_seconds: f64::from(i),
                    delta_ms: if i == 1 { 7000.0 } else { 16.0 },
                    previous_cpu_ms: [None; 6],
                },
                [None; 6],
                i < 20,
                i >= 10,
                i >= 30,
            );
        }
        assert_eq!(trace.slowest_frames.len(), 8);
        assert_eq!(
            trace.slowest_frames[0].delta_ms.to_bits(),
            7000.0_f64.to_bits()
        );
        assert_eq!(trace.moving_frames, 20);
        assert_eq!(
            trace.first_cpu_ready_seconds.unwrap().to_bits(),
            10.0_f64.to_bits()
        );
        assert_eq!(
            trace.first_gpu_ready_seconds.unwrap().to_bits(),
            30.0_f64.to_bits()
        );
        assert!(trace.maximum_cpu_call_ms.iter().all(Option::is_none));
    }
}
