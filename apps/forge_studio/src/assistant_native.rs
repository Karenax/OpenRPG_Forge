//! Native coordinator shared by the Studio's bounded assistant entry points.
//! HTTP runs outside both coordinator and authoring-host locks.
use crate::{
    assistant_job_native::StoredJob,
    assistant_protocol::{AssistantDraft, AssistantView, ProviderDisclosure},
    assistant_provider_native::NativeProvider,
    launch_configuration::AssistantConfiguration,
};
use forge_core::{CommandId, ContentHash};
use forge_project::{
    assistant::{
        PlanReview,
        jobs::{JobJournal, JobState},
        provider::{OutboundApproval, ProviderRequest, ProviderResult},
        selection::SelectionScope,
    },
    audit::{AuditActor, AuditStamp},
    authoring::{AuthoringRequest, host::AuthoringHost},
};
use std::{
    collections::BTreeMap,
    path::{Path, PathBuf},
    time::{SystemTime, UNIX_EPOCH},
};

mod recovery;

struct Entry {
    scope: SelectionScope,
    request: ProviderRequest,
    store: StoredJob,
    review: Option<PlanReview>,
}

pub struct Generation {
    pub job_id: CommandId,
    pub attempt: u8,
    provider: NativeProvider,
    request: ProviderRequest,
    approval: OutboundApproval,
}

impl Generation {
    pub fn run(self) -> Result<ProviderResult, String> {
        self.provider.generate(&self.request, &self.approval)
    }
}

pub struct AssistantCoordinator {
    root: PathBuf,
    configuration: AssistantConfiguration,
    provider: NativeProvider,
    jobs: BTreeMap<CommandId, Entry>,
    active: BTreeMap<CommandId, u8>,
}

impl AssistantCoordinator {
    pub fn new(root: &Path, configuration: AssistantConfiguration) -> Result<Self, String> {
        let root = root.canonicalize().map_err(|error| error.to_string())?;
        let key = configuration
            .api_key_environment
            .as_ref()
            .map(|name| -> Result<String, String> {
                if name.is_empty()
                    || name.len() > 128
                    || !name
                        .bytes()
                        .all(|byte| byte.is_ascii_alphanumeric() || byte == b'_')
                {
                    return Err("Nom de variable de clé invalide".into());
                }
                std::env::var(name).map_err(|_| {
                    "La clé du fournisseur n'est pas disponible dans son environnement natif."
                        .into()
                })
            })
            .transpose()?;
        let provider = NativeProvider::new(
            &configuration.base_url,
            &configuration.model,
            configuration.api_kind,
            key,
        )?;
        Ok(Self {
            root,
            configuration,
            provider,
            jobs: BTreeMap::new(),
            active: BTreeMap::new(),
        })
    }

    #[must_use]
    pub fn configuration_view(&self) -> AssistantView {
        AssistantView {
            provider: ProviderDisclosure {
                destination: self.provider.destination().into(),
                model: self.configuration.model.clone(),
                reasoning: self.configuration.enable_reasoning,
                maximum_request_seconds: NativeProvider::request_timeout_seconds(
                    self.configuration.limits.maximum_output_tokens,
                ),
                limits: self.configuration.limits,
            },
            job: None,
            outbound_hash: None,
            outbound_body: None,
            review_hash: None,
            changes: Vec::new(),
            zone_name_before: None,
            authoring: None,
            recovery_page: None,
            recovered_job: None,
            message: "Fournisseur prêt ; préparer un aperçu avant tout envoi.".into(),
        }
    }

    fn view_for_job(&self, store: &StoredJob) -> Result<AssistantView, String> {
        let mut view = self.configuration_view();
        // A resumed journal keeps its reviewed budget, even after local settings
        // change. Disclose the budget actually enforced by that durable job.
        view.provider.limits = store.limits();
        view.provider.maximum_request_seconds =
            NativeProvider::request_timeout_seconds(store.limits().maximum_output_tokens);
        view.job = Some(store.snapshot()?);
        Ok(view)
    }

    pub fn prepare(
        &mut self,
        host: &mut AuthoringHost,
        draft: &AssistantDraft,
    ) -> Result<AssistantView, String> {
        self.prepare_with_assets(host, draft, |_| {
            Err("Asset references require native library verification".into())
        })
    }

    pub fn prepare_with_assets(
        &mut self,
        host: &mut AuthoringHost,
        draft: &AssistantDraft,
        resolve: impl FnOnce(
            &crate::assistant_asset_protocol::AssetSelection,
        ) -> Result<crate::assistant_assets_native::BoundCatalog, String>,
    ) -> Result<AssistantView, String> {
        if draft
            .resume_job
            .is_none_or(|id| !self.jobs.contains_key(&id))
        {
            self.ensure_capacity()?;
        }
        let state = host.execute(AuthoringRequest::Inspect)?;
        let head = state
            .zone_head
            .as_ref()
            .ok_or("Ouvrir une carte de module pour préparer l'assistant.")?;
        if state.playtest_hash.is_some() {
            return Err("Quitter le jeu avant de préparer des retouches.".into());
        }
        if let Some(id) = draft.resume_job.filter(|id| !self.jobs.contains_key(id)) {
            let mut store = StoredJob::open(&self.root, id)?;
            // An acknowledgement lost after source Apply is recovered by reading
            // canonical receipts only. This branch never authorizes a new mutation.
            if let Some(receipt) = host.assistant_receipt(id)? {
                let snapshot = store.snapshot()?;
                let model = snapshot.result.as_ref().map(|result| result.model.as_str());
                if !matches!(&receipt.audit.actor, AuditActor::Ai { subject } if Some(subject.as_str()) == model)
                {
                    return Err(
                        "Le reçu existant ne correspond pas au fournisseur de ce job.".into(),
                    );
                }
                store.acknowledge_applied(id, receipt.resulting_hash)?;
                let mut view = self.view_for_job(&store)?;
                view.authoring = Some(Box::new(state));
                view.message = "Application déjà enregistrée dans le projet ; accusé du job réconcilié sans nouvelle mutation.".into();
                return Ok(view);
            }
        }
        if draft.expected_head != head.content_hash()? {
            return Err(
                "La carte a changé ; préparer le contexte depuis sa révision actuelle.".into(),
            );
        }
        let scope = if let Some(selection) = &draft.assets {
            let catalog = resolve(selection)?;
            SelectionScope::new_with_assets(
                head,
                &draft.selection,
                draft.region,
                catalog.catalog_for(selection)?,
            )?
        } else {
            SelectionScope::new(head, &draft.selection, draft.region)?
        };
        let mut request = ProviderRequest::new(
            self.configuration.api_kind,
            self.provider.destination(),
            &self.configuration.model,
            &draft.brief,
            scope.context(),
            u32::try_from(self.configuration.limits.maximum_output_tokens)
                .map_err(|_| "Budget de sortie invalide")?,
        )?;
        if self.configuration.enable_reasoning {
            request = request.with_local_reasoning()?;
        }
        if let Some(id) = draft
            .resume_job
            .and_then(|id| self.jobs.contains_key(&id).then_some(id))
        {
            return self.reopen_loaded_job(host, id, &request);
        }
        let mut restored_application = false;
        let (id, store) = if let Some(id) = draft.resume_job {
            let mut store = StoredJob::open(&self.root, id)?;
            store.verify_request(&request)?;
            if store.snapshot()?.state == JobState::Applied {
                // The receipt branch above already handled accepted source. The
                // verified request binds the exact original head and selection.
                store.restore_unpersisted_application(&request)?;
                restored_application = true;
            }
            (id, store)
        } else {
            let nonce = SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .map_err(|_| "Horloge native indisponible")?
                .as_nanos();
            let id =
                CommandId::deterministic(format!("assistant-job/{}/{nonce}", std::process::id()));
            let journal = JobJournal::new(id, &request, self.configuration.limits)?;
            (id, StoredJob::create(&self.root, journal)?)
        };
        self.jobs.insert(
            id,
            Entry {
                scope,
                request,
                store,
                review: None,
            },
        );
        self.restored_view(id, restored_application)
    }

    pub fn inspect(&self, id: CommandId) -> Result<AssistantView, String> {
        let entry = self
            .jobs
            .get(&id)
            .ok_or("Recharger le brief et sa sélection pour reprendre ce job.")?;
        let mut view = self.view_for_job(&entry.store)?;
        view.outbound_hash = Some(entry.request.content_hash()?);
        view.outbound_body = Some(entry.request.body().clone());
        view.zone_name_before = Some(entry.scope.context().zone_name.clone());
        if let Some(review) = &entry.review {
            view.review_hash = Some(review.content_hash()?);
            view.changes = review.changes().to_vec();
        }
        view.message = match view.job.as_ref().map(|job| job.state) {
            Some(JobState::Prepared) => "Contexte prêt ; vérifier les données avant d'autoriser l'envoi.",
            Some(JobState::Running) => "Appel fournisseur en cours ; aucune mutation du projet.",
            Some(JobState::Interrupted) => "Appel interrompu ; sa réservation reste comptée. Une reprise nécessite une nouvelle confirmation d'envoi.",
            Some(JobState::Staged) => "Proposition conservée ; relire le diff avant application.",
            Some(JobState::Applied) => "Proposition appliquée ; sauvegarder la carte pour conserver les sources sur disque.",
            Some(JobState::Cancelled) => "Job annulé ; aucune nouvelle application n'est autorisée.",
            Some(JobState::BudgetExceeded) => "Plafond dépassé ; résultat conservé, application et reprises bloquées.",
            None => "Job absent.",
        }.into();
        Ok(view)
    }

    pub fn begin(
        &mut self,
        host: &mut AuthoringHost,
        id: CommandId,
        hash: ContentHash,
    ) -> Result<Generation, String> {
        let head = host
            .execute(AuthoringRequest::Inspect)?
            .zone_head
            .ok_or("Ouvrir la carte du job.")?;
        let entry = self
            .jobs
            .get_mut(&id)
            .ok_or("Préparer ou recharger le job avant l'envoi.")?;
        entry.scope.validate_head(&head)?;
        let approval = entry.request.approve_outbound(hash)?;
        let attempt = entry.store.begin(&entry.request, &approval)?;
        self.active.insert(id, attempt);
        Ok(Generation {
            job_id: id,
            attempt,
            provider: self.provider.clone(),
            request: entry.request.clone(),
            approval,
        })
    }

    pub fn finish(
        &mut self,
        id: CommandId,
        attempt: u8,
        result: Result<ProviderResult, String>,
    ) -> Result<AssistantView, String> {
        if self.active.get(&id) != Some(&attempt) {
            return Err("Résultat sans appel actif correspondant.".into());
        }
        self.active.remove(&id);
        let entry = self.jobs.get_mut(&id).ok_or("Job absent après l'appel.")?;
        if entry.store.snapshot()?.state == JobState::Cancelled {
            return self.inspect(id);
        }
        match result {
            Ok(result) => entry.store.stage(attempt, result)?,
            Err(error) => {
                entry.store.interrupt(attempt)?;
                let mut view = self.inspect(id)?;
                view.message = format!("{error} Réservation conservée ; pas de retry automatique.");
                return Ok(view);
            }
        }
        self.inspect(id)
    }

    pub fn review(
        &mut self,
        host: &mut AuthoringHost,
        id: CommandId,
        utc: &str,
    ) -> Result<AssistantView, String> {
        let state = host.execute(AuthoringRequest::Inspect)?;
        let head = state
            .zone_head
            .ok_or("Ouvrir la carte du job avant de relire le diff.")?;
        let entry = self.jobs.get_mut(&id).ok_or("Recharger le job.")?;
        if entry.store.recovery()?.is_some() {
            return Err(
                "Récupérer l’enregistrement interrompu avant de relire cette proposition.".into(),
            );
        }
        let snapshot = entry.store.snapshot()?;
        if snapshot.state != JobState::Staged {
            return Err("Seul un résultat en staging peut être relu.".into());
        }
        entry.scope.validate_head(&head)?;
        let result = snapshot.result.ok_or("Proposition absente.")?;
        let request = result.proposal.compile(&head, &entry.scope, id)?;
        entry.review = Some(host.prepare_assistant(
            request,
            entry.scope.policy(),
            AuditStamp::ai(result.model, utc)?,
        )?);
        self.inspect(id)
    }

    pub fn apply(
        &mut self,
        host: &mut AuthoringHost,
        id: CommandId,
        hash: ContentHash,
    ) -> Result<AssistantView, String> {
        let entry = self.jobs.get_mut(&id).ok_or("Recharger le job.")?;
        if entry.store.recovery()?.is_some() {
            return Err(
                "Récupérer l’enregistrement interrompu avant d’appliquer cette proposition.".into(),
            );
        }
        if !matches!(
            entry.store.snapshot()?.state,
            JobState::Staged | JobState::Applied
        ) {
            return Err("Le job n'autorise pas d'application.".into());
        }
        let review = entry
            .review
            .as_ref()
            .ok_or("Relire le diff avant de l'appliquer.")?;
        let approval = review.approve(hash)?;
        let state = host.apply_assistant(entry.scope.policy(), review, &approval)?;
        let receipt = state
            .zone_receipt
            .as_ref()
            .ok_or("Reçu canonique absent après application.")?;
        let acknowledgement = entry.store.acknowledge_applied(id, receipt.resulting_hash);
        let mut view = self.inspect(id)?;
        view.authoring = Some(Box::new(state));
        if acknowledgement.is_err() {
            view.message = "Le projet a accepté la retouche, mais l'accusé du job n'a pas été sauvegardé. Reprendre ce même job ; ne pas en créer un autre.".into();
        }
        Ok(view)
    }

    pub fn interrupt(&mut self, id: CommandId, attempt: u8) -> Result<AssistantView, String> {
        if self.active.contains_key(&id) {
            return Err(
                "L'appel natif est encore actif ; attendre sa fin ou annuler le job.".into(),
            );
        }
        self.jobs
            .get_mut(&id)
            .ok_or("Recharger le job.")?
            .store
            .interrupt(attempt)?;
        self.inspect(id)
    }

    pub fn cancel(
        &mut self,
        host: &mut AuthoringHost,
        id: CommandId,
    ) -> Result<AssistantView, String> {
        if let Some(receipt) = host.assistant_receipt(id)? {
            let entry = self.jobs.get_mut(&id).ok_or("Recharger le job.")?;
            if entry
                .review
                .as_ref()
                .is_some_and(|review| review.receipt() == &receipt)
            {
                entry
                    .store
                    .acknowledge_applied(id, receipt.resulting_hash)?;
                return self.inspect(id);
            }
            return Err(
                "Le projet contient déjà ce reçu ; réconcilier le job avant de l'annuler.".into(),
            );
        }
        self.jobs
            .get_mut(&id)
            .ok_or("Recharger le job.")?
            .store
            .cancel()?;
        self.inspect(id)
    }

    fn ensure_capacity(&mut self) -> Result<(), String> {
        if self.jobs.len() < 16 {
            return Ok(());
        }
        let removable = self.jobs.iter().find_map(|(id, entry)| {
            (!self.active.contains_key(id)
                && entry.store.snapshot().is_ok_and(|snapshot| {
                    matches!(
                        snapshot.state,
                        JobState::Applied | JobState::Cancelled | JobState::BudgetExceeded
                    )
                }))
            .then_some(*id)
        });
        if let Some(id) = removable {
            self.jobs.remove(&id);
            Ok(())
        } else {
            Err("Terminer ou annuler un job avant d'en ouvrir plus de seize.".into())
        }
    }
}
