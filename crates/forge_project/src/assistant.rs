//! Governed proposals over the existing authoring transaction bus.
//!
//! The trusted application owns policy and approval. Provider/MCP payloads may
//! supply a typed request, never permissions, an approval, or filesystem paths.
//! A review is a derived read model, not another writable project document.
use crate::{
    audit::{AuditActor, AuditStamp},
    zone_authoring::{
        ZoneAuthoringHead,
        session::{ZoneEditSession, ZoneReceipt, ZoneRequest},
    },
};
use forge_core::{ContentHash, canonical_json_bytes, canonical_json_sha256};
use serde::Serialize;
use serde_json::Value;
use std::collections::BTreeSet;

pub mod assets;
pub mod content;
pub mod jobs;
pub mod planning;
pub mod provider;
pub mod selection;

const MAX_REVIEW_BYTES: usize = 512 * 1024;
const MAX_CHANGES: usize = 256;
const MAX_SCOPE_PATHS: usize = 128;

/// JSON pointers into canonical source data, selected by the application.
/// A protected subtree always wins, including against replacement of its parent.
/// Array insertion/removal is reviewed as replacement of the whole array.
#[derive(Clone, Debug, PartialEq, Eq, Serialize)]
pub struct ScopePolicy {
    allowed: Vec<String>,
    protected: Vec<String>,
}

impl ScopePolicy {
    pub fn new(allowed: Vec<String>, protected: Vec<String>) -> Result<Self, String> {
        if allowed.is_empty() || allowed.len() + protected.len() > MAX_SCOPE_PATHS {
            return Err("Assistant scope must contain 1..128 source paths".into());
        }
        for path in allowed.iter().chain(&protected) {
            if !(within(path, "/corrections") || within(path, "/base")) || !valid_pointer(path) {
                return Err("Assistant scope must target canonical base or corrections".into());
            }
        }
        Ok(Self { allowed, protected })
    }

    fn check(&self, changes: &[SourceChange]) -> Result<(), String> {
        for change in changes {
            if !self.allowed.iter().any(|path| within(&change.path, path))
                || self
                    .protected
                    .iter()
                    .any(|path| within(&change.path, path) || within(path, &change.path))
            {
                return Err(format!(
                    "Assistant change outside authorized scope: {}",
                    change.path
                ));
            }
        }
        Ok(())
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, serde::Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceChange {
    pub path: String,
    /// None denotes absence, distinct from a JSON null value.
    pub before: Option<Value>,
    pub after: Option<Value>,
}

/// Immutable, serializable presentation. No Deserialize: external data cannot
/// manufacture an accepted review or bypass the transaction's own preview.
#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct PlanReview {
    schema: &'static str,
    policy: ScopePolicy,
    receipt: ZoneReceipt,
    changes: Vec<SourceChange>,
}

/// Created only by a trusted approval event for a displayed review hash.
/// Never deserialized from provider output or MCP tool arguments.
pub struct Approval {
    review_hash: ContentHash,
}

impl PlanReview {
    pub fn content_hash(&self) -> Result<ContentHash, String> {
        canonical_json_sha256(self).map_err(|error| error.to_string())
    }

    #[must_use]
    pub fn changes(&self) -> &[SourceChange] {
        &self.changes
    }

    #[must_use]
    pub const fn receipt(&self) -> &ZoneReceipt {
        &self.receipt
    }

    /// The application calls this from its human approval path, not an AI tool.
    pub fn approve(&self, displayed_hash: ContentHash) -> Result<Approval, String> {
        if displayed_hash != self.content_hash()? {
            return Err("Approval does not match the displayed assistant diff".into());
        }
        Ok(Approval {
            review_hash: displayed_hash,
        })
    }
}

/// Pure dry-run through the same validation and inverse as manual editing.
pub fn prepare(
    session: &ZoneEditSession,
    request: &ZoneRequest,
    policy: &ScopePolicy,
    audit: AuditStamp,
) -> Result<PlanReview, String> {
    audit.validate()?;
    if !matches!(audit.actor, AuditActor::Ai { .. }) {
        return Err("Assistant proposals require explicit AI provenance".into());
    }
    if canonical_json_bytes(request)
        .map_err(|error| error.to_string())?
        .len()
        > MAX_REVIEW_BYTES
    {
        return Err("Assistant request exceeds review budget".into());
    }
    if request.expected_hash != session.head().content_hash()?
        || request.expected_revision != session.head().revision
    {
        return Err("Assistant plan is stale; prepare and approve a new diff".into());
    }
    if session
        .audit()
        .iter()
        .any(|entry| entry.request.command_id == request.command_id)
    {
        return Err("Assistant command already exists; resume its original review".into());
    }
    let receipt = session.preview_with_audit(request, audit)?;
    let changes = source_changes(session.head(), &receipt)?;
    if changes.is_empty() {
        return Err("Assistant proposal has no source changes".into());
    }
    policy.check(&changes)?;
    let review = PlanReview {
        schema: "forge.assistant-plan-review.v1",
        policy: policy.clone(),
        receipt,
        changes,
    };
    if canonical_json_bytes(&review)
        .map_err(|error| error.to_string())?
        .len()
        > MAX_REVIEW_BYTES
    {
        return Err("Assistant diff exceeds review budget".into());
    }
    Ok(review)
}

/// Revalidates permissions, exact receipt and current revision before mutation.
/// A lost response can be replayed only for the identical already accepted
/// command. Later edits remain untouched and the original receipt is returned.
pub fn apply(
    session: &mut ZoneEditSession,
    policy: &ScopePolicy,
    review: &PlanReview,
    approval: &Approval,
) -> Result<ZoneReceipt, String> {
    validate_application(session, policy, review, approval)?;
    session.execute_with_audit(&review.receipt.request, review.receipt.audit.clone())
}

pub(crate) fn validate_application(
    session: &ZoneEditSession,
    policy: &ScopePolicy,
    review: &PlanReview,
    approval: &Approval,
) -> Result<(), String> {
    if approval.review_hash != review.content_hash()? || policy != &review.policy {
        return Err("Assistant approval or permissions changed; review again".into());
    }
    policy.check(&review.changes)?;
    if let Some(existing) = session
        .audit()
        .iter()
        .find(|entry| entry.request.command_id == review.receipt.request.command_id)
    {
        return if existing == &review.receipt {
            Ok(())
        } else {
            Err("Assistant command ID belongs to a different transaction".into())
        };
    }
    let checked = prepare(
        session,
        &review.receipt.request,
        policy,
        review.receipt.audit.clone(),
    )?;
    if &checked != review {
        return Err("Assistant preview changed; approve the new diff".into());
    }
    Ok(())
}

fn source_changes(
    head: &ZoneAuthoringHead,
    receipt: &ZoneReceipt,
) -> Result<Vec<SourceChange>, String> {
    let mut after = head.clone();
    after.corrections = receipt.applied.clone();
    if let Some(change) = &receipt.base_change {
        after.base = change.applied.clone();
    }
    // Revision and schema version are derived bookkeeping, never scope grants.
    let before =
        serde_json::to_value((&head.base, &head.corrections)).map_err(|error| error.to_string())?;
    let after = serde_json::to_value((&after.base, &after.corrections))
        .map_err(|error| error.to_string())?;
    let mut changes = Vec::new();
    diff("/base", Some(&before[0]), Some(&after[0]), &mut changes)?;
    diff(
        "/corrections",
        Some(&before[1]),
        Some(&after[1]),
        &mut changes,
    )?;
    Ok(changes)
}

fn diff(
    path: &str,
    before: Option<&Value>,
    after: Option<&Value>,
    changes: &mut Vec<SourceChange>,
) -> Result<(), String> {
    if before == after {
        return Ok(());
    }
    match (before, after) {
        (Some(Value::Object(left)), Some(Value::Object(right))) => {
            let keys: BTreeSet<_> = left.keys().chain(right.keys()).collect();
            for key in keys {
                let escaped = key.replace('~', "~0").replace('/', "~1");
                diff(
                    &format!("{path}/{escaped}"),
                    left.get(key),
                    right.get(key),
                    changes,
                )?;
            }
        }
        (Some(Value::Array(left)), Some(Value::Array(right))) if left.len() == right.len() => {
            for (index, (left, right)) in left.iter().zip(right).enumerate() {
                diff(&format!("{path}/{index}"), Some(left), Some(right), changes)?;
            }
        }
        _ => {
            if changes.len() >= MAX_CHANGES {
                return Err("Assistant diff exceeds 256 changes".into());
            }
            changes.push(SourceChange {
                path: path.into(),
                before: before.cloned(),
                after: after.cloned(),
            });
        }
    }
    Ok(())
}

fn within(path: &str, root: &str) -> bool {
    path == root
        || path
            .strip_prefix(root)
            .is_some_and(|suffix| suffix.starts_with('/'))
}

fn valid_pointer(path: &str) -> bool {
    if path.len() > 512 || path.ends_with('/') || path.chars().any(char::is_control) {
        return false;
    }
    let mut chars = path.chars();
    while let Some(character) = chars.next() {
        if character == '~' && !matches!(chars.next(), Some('0' | '1')) {
            return false;
        }
    }
    true
}
