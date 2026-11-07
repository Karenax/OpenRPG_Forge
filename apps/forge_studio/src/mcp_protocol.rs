//! UI-only MCP authorization and approval, separate from JSON-RPC tools.
use forge_core::{CommandId, ContentHash};
use forge_project::{
    assistant::{
        SourceChange,
        selection::{Region, Selection},
    },
    authoring::AuthoringState,
};
use serde::{Deserialize, Serialize};
use serde_json::Value;

#[derive(Clone, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ScopeRequest {
    pub expected_head: ContentHash,
    pub selection: Selection,
    pub region: Region,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub assets: Option<crate::assistant_asset_protocol::AssetSelection>,
}

#[derive(Clone, Serialize, Deserialize)]
#[serde(tag = "action", rename_all = "snake_case", deny_unknown_fields)]
pub enum McpRequest {
    Status,
    Preview {
        scope: ScopeRequest,
    },
    Authorize {
        scope: ScopeRequest,
        expected_context_hash: ContentHash,
    },
    Revoke,
    Apply {
        command_id: CommandId,
        review_hash: ContentHash,
    },
}

#[derive(Clone, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ProposalView {
    pub command_id: CommandId,
    pub review_hash: ContentHash,
    pub summary: String,
    pub changes: Vec<SourceChange>,
}

#[derive(Clone, Serialize, Deserialize)]
pub struct McpView {
    pub stdio_open: bool,
    pub authorized: bool,
    pub scope: Option<ScopeRequest>,
    pub context: Option<Value>,
    pub context_hash: Option<ContentHash>,
    pub proposals: Vec<ProposalView>,
    pub tool_calls: u64,
    pub authoring: Option<Box<AuthoringState>>,
    pub message: String,
}
