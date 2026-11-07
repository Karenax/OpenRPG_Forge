//! Native MCP inbox. Only Studio code can authorize a scope or approve its diff.
use crate::assistant_mcp::{Backend, ToolCall};
pub use crate::mcp_protocol::ProposalView;
use forge_core::{CommandId, ContentHash, canonical_json_sha256};
use forge_project::{
    assistant::{
        PlanReview,
        selection::{PlanningContext, Region, Selection, SelectionScope},
    },
    audit::AuditStamp,
    authoring::{AuthoringRequest, AuthoringState, host::AuthoringHost},
};
use serde_json::{Value, json};
use std::{
    collections::BTreeMap,
    path::{Path, PathBuf},
    sync::atomic::{AtomicU64, Ordering},
    time::{SystemTime, UNIX_EPOCH},
};
mod storage;
static GENERATION: AtomicU64 = AtomicU64::new(0);

struct Pending {
    proposal_hash: ContentHash,
    summary: String,
    review: PlanReview,
}
impl Pending {
    fn approval_hash(&self, authorization: &str) -> Result<ContentHash, String> {
        canonical_json_sha256(&json!({"schema":"forge.mcp-approval.v1","review":self.review.content_hash()?,"authorization":authorization})).map_err(|e|e.to_string())
    }
}

pub struct Inbox {
    scope: SelectionScope,
    pending: BTreeMap<CommandId, Pending>,
    nonce: String,
    storage_root: Option<PathBuf>,
}

impl Inbox {
    #[must_use]
    pub fn context(&self) -> &PlanningContext {
        self.scope.context()
    }
    /// Called by the trusted UI after the user chooses what MCP may inspect/edit.
    pub fn authorize(
        host: &mut AuthoringHost,
        selection: &Selection,
        region: Region,
    ) -> Result<Self, String> {
        let state = host.execute(AuthoringRequest::Inspect)?;
        if state.playtest_hash.is_some() {
            return Err("Quit the game before authorizing MCP".into());
        }
        let head = state
            .zone_head
            .ok_or("Open a zone before authorizing MCP")?;
        Self::authorize_scope(host, SelectionScope::new(&head, selection, region)?, None)
    }

    /// A native-verified scope, never deserialized from UI or MCP arguments.
    pub fn authorize_scope(
        host: &mut AuthoringHost,
        scope: SelectionScope,
        root: Option<&Path>,
    ) -> Result<Self, String> {
        let state = host.execute(AuthoringRequest::Inspect)?;
        let head = state.zone_head.ok_or("Open the authorized zone")?;
        if state.playtest_hash.is_some() || head.content_hash()? != scope.context().base_hash {
            return Err("MCP scope changed before authorization".into());
        }
        let mut inbox = Self {
            scope,
            pending: BTreeMap::new(),
            nonce: format!(
                "{}:{}:{}",
                std::process::id(),
                SystemTime::now()
                    .duration_since(UNIX_EPOCH)
                    .map_err(|error| error.to_string())?
                    .as_nanos(),
                GENERATION.fetch_add(1, Ordering::Relaxed)
            ),
            storage_root: None,
        };
        if let Some(root) = root {
            inbox.restore(host, root)?;
        }
        Ok(inbox)
    }

    pub fn authorize_persistent(
        host: &mut AuthoringHost,
        selection: &Selection,
        region: Region,
        root: &Path,
    ) -> Result<Self, String> {
        let mut inbox = Self::authorize(host, selection, region)?;
        inbox.restore(host, root)?;
        Ok(inbox)
    }

    fn restore(&mut self, host: &mut AuthoringHost, root: &Path) -> Result<(), String> {
        let root = root.canonicalize().map_err(|e| e.to_string())?;
        let context = self.context().content_hash()?;
        let head = host
            .execute(AuthoringRequest::Inspect)?
            .zone_head
            .ok_or("Open the authorized zone")?;
        for record in storage::load(&root, context)? {
            let command = record
                .proposal
                .compile(&head, &self.scope, record.command_id)?;
            let review = host.prepare_assistant(
                command,
                self.scope.policy(),
                AuditStamp::ai("mcp-proposal", record.proposed_utc)?,
            )?;
            self.pending.insert(
                record.command_id,
                Pending {
                    proposal_hash: record.proposal_hash,
                    summary: record.proposal.summary,
                    review,
                },
            );
        }
        self.storage_root = Some(root);
        Ok(())
    }

    pub fn backend<'a>(
        &'a mut self,
        host: &'a mut AuthoringHost,
        utc: &'a str,
    ) -> AuthorizedBackend<'a> {
        AuthorizedBackend {
            inbox: self,
            host,
            utc,
        }
    }

    pub fn proposals(&self) -> Result<Vec<ProposalView>, String> {
        self.pending
            .iter()
            .map(|(id, pending)| {
                Ok(ProposalView {
                    command_id: *id,
                    review_hash: pending.approval_hash(&self.nonce)?,
                    summary: pending.summary.clone(),
                    changes: pending.review.changes().to_vec(),
                })
            })
            .collect()
    }

    /// Trusted UI entry point, deliberately absent from MCP's tool catalogue.
    /// The host revalidates revision, permissions and native candidates on Apply.
    pub fn apply_confirmed(
        &mut self,
        host: &mut AuthoringHost,
        id: CommandId,
        hash: ContentHash,
    ) -> Result<AuthoringState, String> {
        let pending = self.pending.get(&id).ok_or("Unknown MCP proposal")?;
        if hash != pending.approval_hash(&self.nonce)? {
            return Err(
                "L’approbation ne correspond plus à cette autorisation MCP ; relire le diff actuel.".into(),
            );
        }
        let approval = pending.review.approve(pending.review.content_hash()?)?;
        host.apply_assistant(self.scope.policy(), &pending.review, &approval)
    }

    fn execute(
        &mut self,
        host: &mut AuthoringHost,
        utc: &str,
        call: ToolCall,
    ) -> Result<Value, String> {
        let state = host.execute(AuthoringRequest::Inspect)?;
        let head = state.zone_head.ok_or("Open the authorized zone")?;
        if state.playtest_hash.is_some() || head.content_hash()? != self.scope.context().base_hash {
            return Err(
                "MCP scope is stale; authorize the current selection again in Studio".into(),
            );
        }
        let ToolCall::ProposeEdits(proposal) = call else {
            return serde_json::to_value(self.scope.context()).map_err(|error| error.to_string());
        };
        proposal.validate()?;
        // Even unsupported-capability reports must identify the authorized context.
        if proposal.context_hash != self.scope.context().content_hash()? {
            return Err("MCP proposal context differs from the authorized selection".into());
        }
        if proposal.operations.is_empty() {
            return Ok(
                json!({"staged":false,"missing_capabilities":proposal.missing_capabilities,"summary":proposal.summary}),
            );
        }
        let proposal_hash =
            canonical_json_sha256(proposal.as_ref()).map_err(|error| error.to_string())?;
        if let Some((id, pending)) = self
            .pending
            .iter()
            .find(|(_, pending)| pending.proposal_hash == proposal_hash)
        {
            return Ok(
                json!({"staged":true,"command_id":id,"review_hash":pending.approval_hash(&self.nonce)?,"applied":false}),
            );
        }
        if self.pending.len() >= 16 {
            return Err("MCP inbox has reached its 16-proposal limit".into());
        }
        let id = storage::command_id(self.context().content_hash()?, proposal_hash);
        let command = proposal.compile(&head, &self.scope, id)?;
        let review = host.prepare_assistant(
            command,
            self.scope.policy(),
            AuditStamp::ai("mcp-proposal", utc)?,
        )?;
        if let Some(root) = &self.storage_root {
            storage::save(
                root,
                self.context().content_hash()?,
                &storage::Record {
                    schema: "forge.mcp-proposal.v1".into(),
                    command_id: id,
                    proposal_hash,
                    proposal: proposal.as_ref().clone(),
                    proposed_utc: utc.into(),
                },
            )?;
        }
        let pending = Pending {
            proposal_hash,
            summary: proposal.summary.clone(),
            review,
        };
        let review_hash = pending.approval_hash(&self.nonce)?;
        self.pending.insert(id, pending);
        Ok(
            json!({"staged":true,"command_id":id,"review_hash":review_hash,"applied":false,"message":"Review and approve this diff in Studio. No source has changed."}),
        )
    }
}

pub struct AuthorizedBackend<'a> {
    inbox: &'a mut Inbox,
    host: &'a mut AuthoringHost,
    utc: &'a str,
}
impl Backend for AuthorizedBackend<'_> {
    fn context(&self) -> Result<Option<PlanningContext>, String> {
        Ok(Some(self.inbox.scope.context().clone()))
    }
    fn execute(&mut self, call: ToolCall) -> Result<Value, String> {
        self.inbox.execute(self.host, self.utc, call)
    }
}
