//! Bounded readings of existing Bevy diagnostics, not a new GPU profiler.
use bevy::diagnostic::DiagnosticsStore;

#[derive(Default)]
pub(super) struct RenderSamples {
    last_bucket: u32,
    pub samples: Vec<serde_json::Value>,
}

impl RenderSamples {
    pub fn record(&mut self, tick: u32, frame: u32, store: Option<&DiagnosticsStore>) {
        let bucket = tick / 60;
        if bucket <= self.last_bucket || self.samples.len() >= 64 {
            return;
        }
        self.last_bucket = bucket;
        let mut diagnostics: Vec<_> = store
            .into_iter()
            .flat_map(DiagnosticsStore::iter)
            .filter(|d| {
                let path = d.path().as_str();
                path.starts_with("render/")
                    && (path.ends_with("elapsed_gpu") || path.ends_with("elapsed_cpu"))
            })
            .collect();
        diagnostics.sort_by(|a, b| a.path().as_str().cmp(b.path().as_str()));
        let available_paths = diagnostics.len();
        let rows: Vec<_> = diagnostics.into_iter().take(128).map(|d| {
            let measurement = d.measurement();
            let value = measurement.map(|m| m.value).filter(|v| v.is_finite() && *v >= 0.0);
            serde_json::json!({
                "path": d.path().as_str(), "suffix": d.suffix,
                "enabled": d.is_enabled, "value": value,
                "measurement_age_ms": measurement.map(|m| m.time.elapsed().as_secs_f64() * 1000.0),
                "value_status": if measurement.is_none() { "unavailable" }
                    else if value.is_none() { "invalid" } else { "observed" },
            })
        }).collect();
        self.samples.push(serde_json::json!({
            "read_replay_tick": tick, "read_render_frame": frame,
            "store_available": store.is_some(), "available_paths": available_paths,
            "truncated": available_paths > rows.len(), "passes": rows,
        }));
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::diagnostic::{Diagnostic, DiagnosticMeasurement, DiagnosticPath};

    #[test]
    fn samples_once_per_bucket_and_stays_bounded_without_a_store() {
        let mut samples = RenderSamples::default();
        for tick in 0..10_000 {
            samples.record(tick, tick, None);
        }
        assert_eq!(samples.samples.len(), 64);
        assert_eq!(samples.samples[0]["read_replay_tick"], 60);
        assert_eq!(samples.samples[0]["store_available"], false);
        assert_eq!(samples.samples[0]["passes"], serde_json::json!([]));
        samples.record(60, 10_001, None);
        assert_eq!(samples.samples.len(), 64);
    }

    #[test]
    fn preserves_units_zero_missing_and_invalid_without_claiming_a_gpu_total() {
        let mut store = DiagnosticsStore::default();
        for (path, value) in [
            ("render/a/elapsed_gpu", Some(0.0)),
            ("render/b/elapsed_cpu", None),
            ("render/c/elapsed_gpu", Some(f64::NAN)),
            ("render/d/elapsed_gpu", Some(-1.0)),
            ("unrelated/elapsed_gpu", Some(99.0)),
            ("render/a/triangles", Some(100.0)),
        ] {
            let mut diagnostic = Diagnostic::new(DiagnosticPath::new(path)).with_suffix("ms");
            if let Some(value) = value {
                diagnostic.add_measurement(DiagnosticMeasurement {
                    time: std::time::Instant::now(),
                    value,
                });
            }
            store.add(diagnostic);
        }
        let mut samples = RenderSamples::default();
        samples.record(61, 80, Some(&store));
        let rows = samples.samples[0]["passes"].as_array().unwrap();
        assert_eq!(rows.len(), 4);
        assert_eq!(rows[0]["value"], 0.0);
        assert_eq!(rows[0]["suffix"], "ms");
        assert_eq!(rows[0]["value_status"], "observed");
        assert_eq!(rows[1]["value_status"], "unavailable");
        assert!(rows[1]["measurement_age_ms"].is_null());
        assert_eq!(rows[2]["value_status"], "invalid");
        assert_eq!(rows[3]["value_status"], "invalid");
    }
}
