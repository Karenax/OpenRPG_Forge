//! Bounded replay-validated job journal. Project archives remain authoritative.
use super::{
    planning::MAX_PROPOSAL_BYTES,
    provider::{
        MAX_PROVIDER_OUTPUT_TOKENS, OutboundApproval, ProviderRequest, ProviderResult, TokenUsage,
    },
};
use forge_core::{CommandId, ContentHash, canonical_json_bytes};
use serde::{Deserialize, Serialize};

const MAX_JOB_BYTES: usize = 2 * 1024 * 1024;

/// Integer micro-USD rates per million tokens, explicitly supplied by the native
/// configuration. Zero rates describe a local provider, not inferred cloud prices.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TokenPrices {
    pub input_per_million: u64,
    pub output_per_million: u64,
}

impl TokenPrices {
    pub fn charge(self, usage: TokenUsage) -> Result<u64, String> {
        let numerator = (u128::from(usage.input_tokens) * u128::from(self.input_per_million))
            .checked_add(u128::from(usage.output_tokens) * u128::from(self.output_per_million))
            .ok_or("Provider token charge overflow")?;
        u64::try_from(numerator.div_ceil(1_000_000))
            .map_err(|_| "Provider token charge overflow".into())
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct JobLimits {
    pub maximum_attempts: u8,
    pub maximum_input_tokens: u64,
    pub maximum_output_tokens: u64,
    pub maximum_micro_usd: u64,
    pub prices: TokenPrices,
}

impl JobLimits {
    fn reservation(self) -> Result<u64, String> {
        if !(1..=5).contains(&self.maximum_attempts)
            || self.maximum_input_tokens == 0
            || self.maximum_input_tokens > 1_000_000
            || !(128..=u64::from(MAX_PROVIDER_OUTPUT_TOKENS)).contains(&self.maximum_output_tokens)
        {
            return Err("Invalid job attempt or token limits".into());
        }
        self.prices.charge(TokenUsage {
            input_tokens: self.maximum_input_tokens,
            output_tokens: self.maximum_output_tokens,
        })
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum JobState {
    Prepared,
    Running,
    Interrupted,
    Staged,
    Applied,
    Cancelled,
    BudgetExceeded,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "event", rename_all = "snake_case", deny_unknown_fields)]
enum Event {
    Started {
        attempt: u8,
    },
    Interrupted {
        attempt: u8,
    },
    Staged {
        attempt: u8,
        result: Box<ProviderResult>,
    },
    Applied {
        command_id: CommandId,
        resulting_hash: ContentHash,
    },
    SourceApplicationLost {
        resulting_hash: ContentHash,
    },
    Cancelled,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct Archive {
    schema: String,
    job_id: CommandId,
    request_hash: ContentHash,
    context_hash: ContentHash,
    limits: JobLimits,
    events: Vec<Event>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct JobSnapshot {
    pub job_id: CommandId,
    pub state: JobState,
    pub attempts: u8,
    /// Known charges plus conservative reservations for uncertain/unfinished calls.
    pub committed_or_reserved_micro_usd: u64,
    pub uncertain_attempts: u8,
    pub result: Option<ProviderResult>,
    pub resulting_hash: Option<ContentHash>,
}

/// Contains no credential or human approval token. Reload never grants permission
/// to send/apply. Each send still needs the exact outbound approval and each
/// mutation still needs a fresh/current `PlanReview` approval in the common host.
pub struct JobJournal {
    archive: Archive,
}

impl JobJournal {
    #[must_use]
    pub const fn limits(&self) -> JobLimits {
        self.archive.limits
    }

    pub fn verify_request(&self, request: &ProviderRequest) -> Result<(), String> {
        if request.content_hash()? != self.archive.request_hash {
            return Err("Le brief, la sélection ou le fournisseur du job ont changé ; préparer un nouveau job.".into());
        }
        Ok(())
    }

    pub fn new(
        job_id: CommandId,
        request: &ProviderRequest,
        limits: JobLimits,
    ) -> Result<Self, String> {
        limits.reservation()?;
        if limits.maximum_output_tokens != u64::from(request.maximum_output_tokens()) {
            return Err("Job and provider output budgets differ".into());
        }
        let journal = Self {
            archive: Archive {
                schema: "forge.assistant-job.v1".into(),
                job_id,
                request_hash: request.content_hash()?,
                context_hash: request.context_hash(),
                limits,
                events: Vec::new(),
            },
        };
        journal.snapshot()?;
        Ok(journal)
    }

    /// Persist this Started event before dispatching HTTP. A running job is never
    /// resent automatically after a lost process: the operator must interrupt it.
    pub fn begin(
        &mut self,
        request: &ProviderRequest,
        approval: &OutboundApproval,
    ) -> Result<u8, String> {
        request.verify_outbound(approval)?;
        if request.content_hash()? != self.archive.request_hash {
            return Err("Job request changed; create a new reviewed job".into());
        }
        let snapshot = self.snapshot()?;
        let attempt = snapshot
            .attempts
            .checked_add(1)
            .ok_or("Attempt counter overflow")?;
        self.append(Event::Started { attempt })?;
        Ok(attempt)
    }

    pub fn interrupt(&mut self, attempt: u8) -> Result<(), String> {
        self.append(Event::Interrupted { attempt })
    }

    /// Retains valid output even if the provider exceeded the reviewed token/cost
    /// budget; such a result remains blocked and cannot be marked applied.
    pub fn stage(&mut self, attempt: u8, result: ProviderResult) -> Result<(), String> {
        self.append(Event::Staged {
            attempt,
            result: Box::new(result),
        })
    }

    /// A journal records an acknowledgement, never authorizes a mutation.
    /// The caller must find this command/hash in the canonical project's receipts.
    pub fn acknowledge_applied(
        &mut self,
        command_id: CommandId,
        resulting_hash: ContentHash,
    ) -> Result<(), String> {
        if self.snapshot()?.state == JobState::Applied
            && self.snapshot()?.resulting_hash == Some(resulting_hash)
            && command_id == self.archive.job_id
        {
            return Ok(());
        }
        self.append(Event::Applied {
            command_id,
            resulting_hash,
        })
    }

    pub fn cancel(&mut self) -> Result<(), String> {
        self.append(Event::Cancelled)
    }

    /// The trusted caller must establish that the canonical source has no receipt
    /// and is back at the exact original request context. This only restores a
    /// proposal for fresh review; it never authorizes or repeats an application.
    pub fn restore_unpersisted_application(
        &mut self,
        request: &ProviderRequest,
    ) -> Result<(), String> {
        self.verify_request(request)?;
        let snapshot = self.snapshot()?;
        if snapshot.state != JobState::Applied {
            return Err("Only an acknowledged application can be restored for review".into());
        }
        self.append(Event::SourceApplicationLost {
            resulting_hash: snapshot.resulting_hash.ok_or("Missing application hash")?,
        })
    }

    pub fn archive_bytes(&self) -> Result<Vec<u8>, String> {
        self.snapshot()?;
        let bytes = canonical_json_bytes(&self.archive).map_err(|error| error.to_string())?;
        if bytes.len() > MAX_JOB_BYTES {
            return Err("Job journal exceeds 2 MiB".into());
        }
        Ok(bytes)
    }

    pub fn from_bytes(bytes: &[u8]) -> Result<Self, String> {
        if bytes.len() > MAX_JOB_BYTES {
            return Err("Job journal exceeds 2 MiB".into());
        }
        let journal = Self {
            archive: serde_json::from_slice(bytes).map_err(|error| error.to_string())?,
        };
        journal.snapshot()?;
        Ok(journal)
    }

    pub fn snapshot(&self) -> Result<JobSnapshot, String> {
        if self.archive.schema != "forge.assistant-job.v1" || self.archive.events.len() > 16 {
            return Err("Invalid job schema or event budget".into());
        }
        let reserve = self.archive.limits.reservation()?;
        let mut snapshot = JobSnapshot {
            job_id: self.archive.job_id,
            state: JobState::Prepared,
            attempts: 0,
            committed_or_reserved_micro_usd: 0,
            uncertain_attempts: 0,
            result: None,
            resulting_hash: None,
        };
        for event in &self.archive.events {
            replay(&mut snapshot, event, &self.archive, reserve)?;
        }
        Ok(snapshot)
    }

    fn append(&mut self, event: Event) -> Result<(), String> {
        let mut candidate = Self {
            archive: self.archive.clone(),
        };
        candidate.archive.events.push(event);
        candidate.archive_bytes()?;
        self.archive = candidate.archive;
        Ok(())
    }
}

fn replay(
    snapshot: &mut JobSnapshot,
    event: &Event,
    archive: &Archive,
    reserve: u64,
) -> Result<(), String> {
    match event {
        Event::Started { attempt } => {
            let committed = snapshot
                .committed_or_reserved_micro_usd
                .checked_add(reserve)
                .ok_or("Job budget overflow")?;
            if !matches!(snapshot.state, JobState::Prepared | JobState::Interrupted)
                || *attempt != snapshot.attempts + 1
                || *attempt > archive.limits.maximum_attempts
                || committed > archive.limits.maximum_micro_usd
            {
                return Err("Job cannot start: state, attempts or cost ceiling".into());
            }
            snapshot.attempts = *attempt;
            snapshot.committed_or_reserved_micro_usd = committed;
            snapshot.uncertain_attempts += 1;
            snapshot.state = JobState::Running;
        }
        Event::Interrupted { attempt } => {
            if snapshot.state != JobState::Running || *attempt != snapshot.attempts {
                return Err("Only the current running attempt can be interrupted".into());
            }
            snapshot.state = JobState::Interrupted;
        }
        Event::Staged { attempt, result } => {
            if snapshot.state != JobState::Running || *attempt != snapshot.attempts {
                return Err(
                    "Late/duplicate provider result cannot replace the current attempt".into(),
                );
            }
            result.proposal.validate()?;
            if result.proposal.context_hash != archive.context_hash
                || canonical_json_bytes(&result.proposal)
                    .map_err(|error| error.to_string())?
                    .len()
                    > MAX_PROPOSAL_BYTES
                || result.model.is_empty()
                || result.model.len() > 256
                || result.response_id.is_empty()
                || result.response_id.len() > 256
            {
                return Err("Staged provider result has wrong context or invalid metadata".into());
            }
            let charge = archive.limits.prices.charge(result.usage)?;
            snapshot.committed_or_reserved_micro_usd = snapshot
                .committed_or_reserved_micro_usd
                .checked_sub(reserve)
                .and_then(|previous| previous.checked_add(charge))
                .ok_or("Job budget overflow")?;
            snapshot.uncertain_attempts -= 1;
            snapshot.state = if result.usage.input_tokens > archive.limits.maximum_input_tokens
                || result.usage.output_tokens > archive.limits.maximum_output_tokens
                || snapshot.committed_or_reserved_micro_usd > archive.limits.maximum_micro_usd
            {
                JobState::BudgetExceeded
            } else {
                JobState::Staged
            };
            snapshot.result = Some((**result).clone());
        }
        Event::Applied {
            command_id,
            resulting_hash,
        } => {
            if snapshot.state != JobState::Staged || *command_id != archive.job_id {
                return Err("Only the staged job command can be acknowledged".into());
            }
            snapshot.state = JobState::Applied;
            snapshot.resulting_hash = Some(*resulting_hash);
        }
        Event::Cancelled => {
            if matches!(snapshot.state, JobState::Applied | JobState::Cancelled) {
                return Err("Terminal job cannot be cancelled again".into());
            }
            snapshot.state = JobState::Cancelled;
        }
        Event::SourceApplicationLost { resulting_hash } => {
            if snapshot.state != JobState::Applied
                || snapshot.resulting_hash != Some(*resulting_hash)
            {
                return Err("Only the current application can return to source review".into());
            }
            snapshot.state = JobState::Staged;
            snapshot.resulting_hash = None;
        }
    }
    Ok(())
}
