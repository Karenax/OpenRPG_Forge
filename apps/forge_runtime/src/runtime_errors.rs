//! Fail automated proof runs on emitted errors, independently of the console log filter.

use bevy::app::AppExit;
use std::sync::{
    Arc,
    atomic::{AtomicBool, Ordering},
};
use tracing::{Event, Subscriber};
use tracing_subscriber::{Layer, layer::Context};

#[derive(Clone, Default)]
pub(crate) struct RuntimeErrors(Arc<AtomicBool>);

impl RuntimeErrors {
    pub(crate) fn observed(&self) -> bool {
        self.0.load(Ordering::Relaxed)
    }

    /// Preserve Bevy's explicit failures and the interactive close lifecycle.
    pub(crate) fn resolve_exit(&self, exit: AppExit, automated: bool) -> AppExit {
        if automated && exit.is_success() && self.observed() {
            AppExit::from_code(1)
        } else {
            exit
        }
    }
}

impl<S: Subscriber> Layer<S> for RuntimeErrors {
    fn on_event(&self, event: &Event<'_>, _context: Context<'_, S>) {
        if *event.metadata().level() == tracing::Level::ERROR {
            self.0.store(true, Ordering::Relaxed);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use tracing_subscriber::{EnvFilter, prelude::*};

    #[test]
    fn clean_and_warning_only_runs_keep_success() {
        let errors = RuntimeErrors::default();
        let subscriber = tracing_subscriber::registry().with(errors.clone());
        tracing::subscriber::with_default(subscriber, || {
            tracing::info!("startup");
            tracing::warn!("driver warning");
        });
        assert!(!errors.observed());
        assert!(errors.resolve_exit(AppExit::Success, true).is_success());
    }

    #[test]
    fn error_fails_proof_even_when_console_logging_is_off() {
        let errors = RuntimeErrors::default();
        let subscriber = tracing_subscriber::registry().with(errors.clone()).with(
            tracing_subscriber::fmt::layer()
                .with_writer(std::io::sink)
                .with_filter(EnvFilter::new("off")),
        );
        tracing::subscriber::with_default(subscriber, || {
            tracing::error!(target: "bevy_render::render_resource::pipeline_cache", "synthetic shader compilation failure");
        });
        assert!(errors.observed());
        assert_eq!(
            errors.resolve_exit(AppExit::Success, true),
            AppExit::from_code(1)
        );
        assert_eq!(
            errors.resolve_exit(AppExit::from_code(7), true),
            AppExit::from_code(7)
        );
        assert!(errors.resolve_exit(AppExit::Success, false).is_success());
    }

    #[test]
    fn errors_from_worker_dispatch_reach_the_exit_verdict() {
        let errors = RuntimeErrors::default();
        let dispatch = tracing::Dispatch::new(tracing_subscriber::registry().with(errors.clone()));
        std::thread::spawn(move || {
            tracing::dispatcher::with_default(&dispatch, || {
                tracing::error!(target: "bevy_asset", "synthetic asset load failure");
            });
        })
        .join()
        .unwrap();
        assert_eq!(
            errors.resolve_exit(AppExit::Success, true),
            AppExit::from_code(1)
        );
    }
}
