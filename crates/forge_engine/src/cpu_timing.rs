//! Opt-in native CPU timings. No sampling, source-check bypass or GPU-time claim.
#[cfg(not(target_arch = "wasm32"))]
mod native {
    use std::{
        collections::BTreeMap,
        sync::{Mutex, OnceLock},
        time::Instant,
    };

    #[derive(Default)]
    struct Total {
        calls: u64,
        milliseconds: f64,
        maximum_ms: f64,
    }
    struct Stats {
        since: Instant,
        stages: BTreeMap<&'static str, Total>,
    }
    static STATS: OnceLock<Option<Mutex<Stats>>> = OnceLock::new();

    pub(super) fn enabled() -> bool {
        STATS
            .get_or_init(|| {
                (std::env::var_os("FORGE_RUNTIME_CPU_PROFILE").as_deref()
                    == Some(std::ffi::OsStr::new("1")))
                .then(|| {
                    Mutex::new(Stats {
                        since: Instant::now(),
                        stages: BTreeMap::new(),
                    })
                })
            })
            .is_some()
    }

    pub(super) struct Span {
        label: &'static str,
        started: Option<Instant>,
    }
    impl Span {
        pub(super) fn new(label: &'static str) -> Self {
            let enabled = enabled();
            Self {
                label,
                started: enabled.then(Instant::now),
            }
        }
    }
    impl Drop for Span {
        fn drop(&mut self) {
            if let Some(started) = self.started {
                record(self.label, started.elapsed().as_secs_f64() * 1000.0);
            }
        }
    }

    pub(super) fn record(label: &'static str, elapsed_ms: f64) {
        let Some(Some(stats)) = STATS.get() else {
            return;
        };
        let Ok(mut stats) = stats.lock() else {
            return;
        };
        let total = stats.stages.entry(label).or_default();
        total.calls += 1;
        total.milliseconds += elapsed_ms;
        total.maximum_ms = total.maximum_ms.max(elapsed_ms);
        if stats.since.elapsed().as_secs_f64() >= 5.0 {
            for (&stage, total) in &stats.stages {
                #[allow(clippy::cast_precision_loss)] // Diagnostic call counter only.
                let mean_ms = total.milliseconds / total.calls as f64;
                bevy::log::info!(
                    event_kind = "runtime_cpu_sample",
                    stage,
                    calls = total.calls,
                    total_ms = total.milliseconds,
                    mean_ms,
                    maximum_ms = total.maximum_ms,
                    "Native CPU scope, nested stages are not additive"
                );
            }
            stats.since = Instant::now();
        }
    }
}

pub(crate) fn record(label: &'static str, elapsed_ms: f64) {
    #[cfg(not(target_arch = "wasm32"))]
    if native::enabled() {
        native::record(label, elapsed_ms);
    }
    #[cfg(target_arch = "wasm32")]
    let _ = (label, elapsed_ms);
}

pub struct Span {
    #[cfg(not(target_arch = "wasm32"))]
    _native: native::Span,
}
impl Span {
    #[must_use]
    pub fn new(label: &'static str) -> Self {
        #[cfg(target_arch = "wasm32")]
        let _ = label;
        Self {
            #[cfg(not(target_arch = "wasm32"))]
            _native: native::Span::new(label),
        }
    }
}

/// Whether the caller explicitly enabled transient runtime CPU observations.
#[must_use]
pub fn enabled() -> bool {
    #[cfg(not(target_arch = "wasm32"))]
    {
        native::enabled()
    }
    #[cfg(target_arch = "wasm32")]
    {
        false
    }
}
