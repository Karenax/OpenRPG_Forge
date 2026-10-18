//! Renderer pass measurements for the bounded walk, not an interactive quality setting.
use bevy::{diagnostic::DiagnosticsStore, prelude::*, render::diagnostic::RenderDiagnosticsPlugin};

pub(super) fn install(app: &mut App) {
    app.add_plugins(RenderDiagnosticsPlugin);
}

pub(super) fn report(store: &DiagnosticsStore) -> serde_json::Value {
    let mut passes = std::collections::BTreeMap::new();
    for diagnostic in store.iter() {
        let path = diagnostic.path().to_string();
        if !path.starts_with("render/")
            || !(path.ends_with("elapsed_cpu") || path.ends_with("elapsed_gpu"))
        {
            continue;
        }
        let Some(mean) = diagnostic.average().filter(|value| value.is_finite()) else {
            continue;
        };
        passes.insert(
            path,
            serde_json::json!({
                "mean_ms": mean,
                "last_ms": diagnostic.value().filter(|value| value.is_finite()),
                "samples": diagnostic.history_len(),
            }),
        );
    }
    let gpu_available = passes.keys().any(|path| path.ends_with("elapsed_gpu"));
    serde_json::json!({
        "gpu_timestamps_available": gpu_available,
        "passes": passes,
        "scope": "Bevy instrumented passes and their retained diagnostic histories; custom Forge passes without spans are excluded; nested spans are not additive; unavailable GPU timings are not zero",
    })
}
