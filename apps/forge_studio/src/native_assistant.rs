//! Tauri adapter: human-confirmed UI operations only; provider IO holds no host lock.
use forge_studio_frontend::{
    assistant_native::AssistantCoordinator,
    assistant_protocol::{AssistantRequest, AssistantView},
    launch_configuration,
};
use std::sync::{Mutex, OnceLock};

fn with_coordinator<T>(
    operation: impl FnOnce(&mut AssistantCoordinator) -> Result<T, String>,
) -> Result<T, String> {
    static COORDINATOR: OnceLock<Mutex<Result<AssistantCoordinator, String>>> = OnceLock::new();
    let coordinator = COORDINATOR.get_or_init(|| {
        Mutex::new((|| {
            let root = launch_configuration::authoring_root()?
                .ok_or("Configurer un espace utilisateur avant l'assistant.")?;
            AssistantCoordinator::new(&root, launch_configuration::assistant()?)
        })())
    });
    let mut guard = coordinator
        .lock()
        .map_err(|_| "Assistant indisponible après une erreur interne.")?;
    operation(guard.as_mut().map_err(|error| error.clone())?)
}

pub(super) fn execute(request: AssistantRequest) -> Result<AssistantView, String> {
    match request {
        AssistantRequest::Configuration => {
            with_coordinator(|coordinator| Ok(coordinator.configuration_view()))
        }
        AssistantRequest::InspectRecoveries {
            offset,
            current_job,
        } => with_coordinator(|coordinator| coordinator.inspect_recoveries(offset, current_job)),
        AssistantRequest::RecoverJob {
            job_id,
            expected_saved,
            expected_pending,
            promote,
        } => with_coordinator(|coordinator| {
            coordinator.recover_job(job_id, expected_saved, expected_pending, promote)
        }),
        AssistantRequest::Prepare { draft } => with_coordinator(|coordinator| {
            super::with_authoring_host(|host| {
                coordinator.prepare_with_assets(host, &draft, |selection| {
                    super::with_asset_library(|library| {
                        forge_studio_frontend::assistant_assets_native::resolve(library, selection)
                    })
                })
            })
        }),
        AssistantRequest::Inspect { job_id } => {
            with_coordinator(|coordinator| coordinator.inspect(job_id))
        }
        AssistantRequest::Generate {
            job_id,
            expected_outbound_hash,
        } => {
            let generation = with_coordinator(|coordinator| {
                super::with_authoring_host(|host| {
                    coordinator.begin(host, job_id, expected_outbound_hash)
                })
            })?;
            let attempt = generation.attempt;
            let result = generation.run();
            with_coordinator(|coordinator| coordinator.finish(job_id, attempt, result))
        }
        AssistantRequest::Review { job_id } => {
            let utc = super::utc_now_rfc3339()?;
            with_coordinator(|coordinator| {
                super::with_authoring_host(|host| coordinator.review(host, job_id, &utc))
            })
        }
        AssistantRequest::Apply {
            job_id,
            expected_review_hash,
        } => with_coordinator(|coordinator| {
            super::with_authoring_host(|host| coordinator.apply(host, job_id, expected_review_hash))
        }),
        AssistantRequest::Interrupt { job_id, attempt } => {
            with_coordinator(|coordinator| coordinator.interrupt(job_id, attempt))
        }
        AssistantRequest::Cancel { job_id } => with_coordinator(|coordinator| {
            super::with_authoring_host(|host| coordinator.cancel(host, job_id))
        }),
    }
}
