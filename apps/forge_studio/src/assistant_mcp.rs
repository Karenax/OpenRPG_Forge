//! Bounded MCP stdio framing and lifecycle. No approval or mutation tool exists.
//! A native backend supplies a scope already authorized in the Studio.
use forge_project::assistant::{
    planning::PlanProposal, provider::proposal_schema, selection::PlanningContext,
};
use serde::Deserialize;
use serde_json::{Value, json};
use std::io::{BufRead, Read, Write};

pub const MAX_FRAME_BYTES: usize = 192 * 1024;
const MAX_REPLY_BYTES: usize = 512 * 1024;
const MAX_REQUESTS: u16 = 512;
const VERSION: &str = "2025-11-25";

pub enum ToolCall {
    InspectSelection,
    ProposeEdits(Box<PlanProposal>),
}

/// Implemented by the native application, never deserialized from MCP.
pub trait Backend {
    fn context(&self) -> Result<Option<PlanningContext>, String>;
    fn execute(&mut self, call: ToolCall) -> Result<Value, String>;
}

#[derive(Default)]
pub struct Server {
    initialized: bool,
    ready: bool,
    requests: u16,
}

impl Server {
    /// One JSON-RPC frame. Notifications never produce stdout responses.
    pub fn dispatch(&mut self, bytes: &[u8], backend: &mut impl Backend) -> Option<Value> {
        if bytes.len() > MAX_FRAME_BYTES {
            return Some(error(Value::Null, -32600, "MCP frame exceeds 192 KiB"));
        }
        let message: Value = match serde_json::from_slice(bytes) {
            Ok(message) => message,
            Err(_) => return Some(error(Value::Null, -32700, "Invalid JSON")),
        };
        let Some(object) = message.as_object() else {
            return Some(error(Value::Null, -32600, "Expected a JSON-RPC object"));
        };
        let id = object.get("id").cloned();
        if object.get("jsonrpc").and_then(Value::as_str) != Some("2.0")
            || id.as_ref().is_some_and(|id| {
                !(id.as_i64().is_some()
                    || id
                        .as_str()
                        .is_some_and(|id| !id.is_empty() && id.len() <= 128))
            })
            || object
                .keys()
                .any(|key| !["jsonrpc", "id", "method", "params"].contains(&key.as_str()))
        {
            return Some(error(Value::Null, -32600, "Invalid JSON-RPC envelope"));
        }
        let Some(method) = message.get("method").and_then(Value::as_str) else {
            return Some(error(id.unwrap_or(Value::Null), -32600, "Missing method"));
        };
        let params = message.get("params").cloned().unwrap_or(json!({}));
        let Some(id) = id else {
            if method == "notifications/initialized" && self.initialized && params.is_object() {
                self.ready = true;
            }
            return None;
        };
        if self.requests >= MAX_REQUESTS {
            return Some(error(id, -32000, "MCP session request quota exhausted"));
        }
        self.requests += 1;
        let result = self.request(method, params, backend);
        Some(match result {
            Ok(result) => json!({"jsonrpc":"2.0","id":id,"result":result}),
            Err((code, message)) => error(id, code, &message),
        })
    }

    fn request(
        &mut self,
        method: &str,
        params: Value,
        backend: &mut impl Backend,
    ) -> Result<Value, (i32, String)> {
        if method == "initialize" {
            return self.initialize(&params);
        }
        if method == "ping" {
            return Ok(json!({}));
        }
        if !self.ready {
            return Err((-32002, "Initialize and notify readiness first".into()));
        }
        match method {
            "tools/list" => {
                let params: Empty = serde_json::from_value(params).map_err(invalid)?;
                let _ = params;
                let context = backend.context().map_err(internal)?;
                Ok(json!({"tools":tools(context.as_ref()).map_err(internal)?}))
            }
            "tools/call" => call(params, backend),
            _ => Err((-32601, "Method not supported".into())),
        }
    }

    fn initialize(&mut self, params: &Value) -> Result<Value, (i32, String)> {
        if self.initialized {
            return Err((-32600, "Session is already initialized".into()));
        }
        let version = params.get("protocolVersion").and_then(Value::as_str);
        let client = params.get("clientInfo");
        if version.is_none()
            || !params.get("capabilities").is_some_and(Value::is_object)
            || !client.is_some_and(|client| {
                ["name", "version"].into_iter().all(|key| {
                    client
                        .get(key)
                        .and_then(Value::as_str)
                        .is_some_and(|value| {
                            !value.is_empty()
                                && value.len() <= 128
                                && !value.chars().any(char::is_control)
                        })
                })
            })
        {
            return Err((-32602, "Invalid initialization parameters".into()));
        }
        self.initialized = true;
        let version = match version {
            Some("2025-06-18") => "2025-06-18",
            _ => VERSION,
        };
        Ok(
            json!({"protocolVersion":version,"capabilities":{"tools":{"listChanged":false}},"serverInfo":{"name":"openrpg-forge","version":env!("CARGO_PKG_VERSION")},"instructions":"Only the selection authorized in Studio is exposed. Proposals are untrusted and do not mutate project sources. Review and approval happen in Studio; MCP cannot grant permissions or approve an apply."}),
        )
    }
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Empty {}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Call {
    name: String,
    #[serde(default = "empty_object")]
    arguments: Value,
    #[serde(rename = "_meta")]
    _meta: Option<Value>,
}

fn empty_object() -> Value {
    json!({})
}

fn call(params: Value, backend: &mut impl Backend) -> Result<Value, (i32, String)> {
    let call: Call = serde_json::from_value(params).map_err(invalid)?;
    if backend.context().map_err(internal)?.is_none() {
        return Ok(tool_result(Err(
            "Authorize a selection in Studio first".into()
        )));
    }
    let command = match call.name.as_str() {
        "forge_inspect_selection" => {
            let _: Empty = serde_json::from_value(call.arguments).map_err(invalid)?;
            ToolCall::InspectSelection
        }
        "forge_propose_edits" => {
            let bytes = serde_json::to_vec(&call.arguments).map_err(invalid)?;
            ToolCall::ProposeEdits(Box::new(PlanProposal::from_bytes(&bytes).map_err(invalid)?))
        }
        _ => {
            return Err((
                -32602,
                "Unknown tool; no apply, approval, shell or file tool exists".into(),
            ));
        }
    };
    Ok(tool_result(backend.execute(command)))
}

fn tool_result(result: Result<Value, String>) -> Value {
    match result {
        Ok(value) => {
            json!({"content":[{"type":"text","text":value.to_string()}],"structuredContent":value,"isError":false})
        }
        Err(message) => json!({"content":[{"type":"text","text":message}],"isError":true}),
    }
}

fn tools(context: Option<&PlanningContext>) -> Result<Vec<Value>, String> {
    let Some(context) = context else {
        return Ok(Vec::new());
    };
    Ok(vec![
        json!({"name":"forge_inspect_selection","description":"Read only the selection and region authorized in Studio.","inputSchema":{"type":"object","properties":{},"additionalProperties":false},"annotations":{"readOnlyHint":true,"openWorldHint":false}}),
        json!({"name":"forge_propose_edits","description":"Stage a proposal for Studio review; does not approve or apply any source mutation.","inputSchema":proposal_schema(context)?,"annotations":{"readOnlyHint":false,"destructiveHint":false,"idempotentHint":false,"openWorldHint":false}}),
    ])
}

fn invalid(error: impl std::fmt::Display) -> (i32, String) {
    (-32602, format!("Invalid parameters: {error}"))
}
fn internal(error: impl std::fmt::Display) -> (i32, String) {
    (-32603, format!("MCP backend unavailable: {error}"))
}
fn error(id: Value, code: i32, message: &str) -> Value {
    let mut reply = json!({"jsonrpc":"2.0","error":{"code":code,"message":message}});
    reply["id"] = id;
    reply
}

/// Newline-delimited UTF-8 messages; diagnostics belong on stderr, never stdout.
/// An oversized frame terminates the connection without reading unbounded input.
pub fn serve(
    mut reader: impl BufRead,
    mut writer: impl Write,
    backend: &mut impl Backend,
) -> std::io::Result<()> {
    let mut server = Server::default();
    loop {
        let mut frame = Vec::new();
        let read = reader
            .by_ref()
            .take((MAX_FRAME_BYTES + 2) as u64)
            .read_until(b'\n', &mut frame)?;
        if read == 0 {
            return Ok(());
        }
        if frame.len() > MAX_FRAME_BYTES {
            return Err(std::io::Error::new(
                std::io::ErrorKind::InvalidData,
                "MCP frame limit exceeded",
            ));
        }
        if let Some(reply) = server.dispatch(&frame, backend) {
            let mut bytes = serde_json::to_vec(&reply)?;
            if bytes.len() > MAX_REPLY_BYTES {
                bytes = serde_json::to_vec(&error(
                    reply["id"].clone(),
                    -32603,
                    "MCP reply limit exceeded",
                ))?;
            }
            writer.write_all(&bytes)?;
            writer.write_all(b"\n")?;
            writer.flush()?;
        }
    }
}
