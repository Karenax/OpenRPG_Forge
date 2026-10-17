//! Offline, explicitly selected historical data; never a historical script runner.
use crate::Result;
use forge_core::{ContentHash, EntityId};
use serde::{Deserialize, Serialize};

pub const SOURCE_COMMIT: &str = "a7f4a3f5dd0eeff1dc060ea1237b1d7d650b7ca0";
pub const SOURCE_PATH: &str = "apps/Forge.Runtime/content/solaren/project.json";
pub const FIRST_NODE: &str = "00f251a2-aa30-9749-9888-4f7f90d5f9fd";
pub const SECOND_NODE: &str = "79e7b610-fd89-0c48-956f-4bd14d5211c4";
pub const SELECTED_CHOICE: &str = "2662fcf7-4fda-714c-80d8-d090202f314c";
pub const MAX_LEGACY_PROJECT_BYTES: usize = 8 * 1024 * 1024;

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SelectedLegacyNode {
    pub id: EntityId,
    pub text: String,
    pub localization_key: String,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SelectedLegacyChoice {
    pub id: EntityId,
    pub from: EntityId,
    pub to: EntityId,
    pub text: String,
    pub localization_key: String,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SelectedLegacyDialogue {
    pub source_commit: String,
    pub source_path: String,
    pub source_sha256: ContentHash,
    pub license: String,
    pub author: String,
    pub dialogue_key: String,
    pub nodes: Vec<SelectedLegacyNode>,
    pub choice: SelectedLegacyChoice,
    pub exclusions: Vec<String>,
}

/// `expected` is the independently pinned source document, never a hash inferred
/// from an untrusted upload. The regression factory pins the exact Git export.
pub fn extract(bytes: &[u8], expected: ContentHash) -> Result<SelectedLegacyDialogue> {
    if bytes.len() > MAX_LEGACY_PROJECT_BYTES || forge_core::bytes_sha256(bytes) != expected {
        return Err("Historical project size or independently pinned SHA-256 differs".into());
    }
    let value: serde_json::Value = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
    if value
        .get("formatVersion")
        .and_then(serde_json::Value::as_str)
        != Some("1.30.0")
    {
        return Err("Unsupported selected historical project format".into());
    }
    let project = value.get("project").ok_or("Historical project is absent")?;
    let metadata = project
        .get("module")
        .and_then(|v| v.get("metadata"))
        .ok_or("Historical module metadata is absent")?;
    let dialogue = unique(array(project, "dialogues")?, "slug", "maelis-beacon")?;
    let nodes = array(dialogue, "nodes")?;
    let first = unique(nodes, "id", FIRST_NODE)?;
    let second = unique(nodes, "id", SECOND_NODE)?;
    for node in [first, second] {
        require_empty(node, "onEnterActions")?;
    }
    let choice = unique(array(first, "choices")?, "id", SELECTED_CHOICE)?;
    if choice
        .get("targetNodeId")
        .and_then(serde_json::Value::as_str)
        != Some(SECOND_NODE)
    {
        return Err("Selected historical edge has a different target".into());
    }
    require_empty(choice, "actions")?;
    require_empty(choice, "conditions")?;
    if choice
        .get("edgeKind")
        .is_some_and(|v| v.as_str() != Some("flow"))
    {
        return Err("Selected historical edge is not a flow edge".into());
    }
    Ok(SelectedLegacyDialogue {
        source_commit: SOURCE_COMMIT.into(),
        source_path: SOURCE_PATH.into(),
        source_sha256: expected,
        license: text(metadata, "license", 512)?,
        author: text(metadata, "author", 512)?,
        dialogue_key: "maelis-beacon".into(),
        nodes: vec![node(first, FIRST_NODE)?, node(second, SECOND_NODE)?],
        choice: SelectedLegacyChoice {
            id: SELECTED_CHOICE
                .parse::<EntityId>()
                .map_err(|e| e.to_string())?,
            from: FIRST_NODE.parse::<EntityId>().map_err(|e| e.to_string())?,
            to: SECOND_NODE.parse::<EntityId>().map_err(|e| e.to_string())?,
            text: localized(choice, "fallback", 4096)?,
            localization_key: localized(choice, "key", 512)?,
        },
        exclusions: vec![
            "All dialogue nodes and edges outside the two explicitly selected nodes and first edge"
                .into(),
            "The second selected node's outgoing choices and their effects".into(),
            "All historical scenes, gameplay scripts, conditions, actions and quest state".into(),
            "All historical audio recordings, voice assets and visual payloads".into(),
        ],
    })
}

fn node(value: &serde_json::Value, id: &str) -> Result<SelectedLegacyNode> {
    Ok(SelectedLegacyNode {
        id: id.parse::<EntityId>().map_err(|e| e.to_string())?,
        text: localized(value, "fallback", 4096)?,
        localization_key: localized(value, "key", 512)?,
    })
}

fn localized(value: &serde_json::Value, key: &str, maximum: usize) -> Result<String> {
    text(
        value
            .get("text")
            .ok_or("Selected localized text is absent")?,
        key,
        maximum,
    )
}

fn text(value: &serde_json::Value, key: &str, maximum: usize) -> Result<String> {
    let text = value
        .get(key)
        .and_then(serde_json::Value::as_str)
        .ok_or_else(|| format!("Selected historical field is absent: {key}"))?;
    if text.trim().is_empty()
        || text.len() > maximum
        || text
            .chars()
            .any(|c| c.is_control() && !matches!(c, '\n' | '\r' | '\t'))
    {
        return Err(format!(
            "Selected historical text is empty, invalid or oversized: {key}"
        ));
    }
    Ok(text.into())
}

fn array<'a>(value: &'a serde_json::Value, key: &str) -> Result<&'a [serde_json::Value]> {
    value
        .get(key)
        .and_then(serde_json::Value::as_array)
        .map(Vec::as_slice)
        .ok_or_else(|| format!("Selected historical array is absent: {key}"))
}

fn require_empty(value: &serde_json::Value, key: &str) -> Result<()> {
    if array(value, key)?.is_empty() {
        Ok(())
    } else {
        Err(format!(
            "Selected historical behavior cannot be discarded: {key}"
        ))
    }
}

fn unique<'a>(
    rows: &'a [serde_json::Value],
    key: &str,
    expected: &str,
) -> Result<&'a serde_json::Value> {
    let mut matches = rows
        .iter()
        .filter(|row| row.get(key).and_then(serde_json::Value::as_str) == Some(expected));
    let selected = matches
        .next()
        .ok_or_else(|| format!("Selected historical identity is absent: {expected}"))?;
    if matches.next().is_some() {
        return Err(format!(
            "Selected historical identity is duplicated: {expected}"
        ));
    }
    Ok(selected)
}
