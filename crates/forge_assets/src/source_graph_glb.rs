//! Read autonomous source-graph payloads without changing GLB requirements.
//! This verifies transport integrity; it does not admit a material to rendering.
use crate::source_graph_bundle::{SOURCE_GRAPH_EXTENSION, SourceGraphMaterialContract};
use serde_json::Value;

pub struct SourceGraphGlb<'a> {
    /// Original glTF material indices, including gaps for ordinary materials.
    pub materials: Vec<(usize, SourceGraphMaterialContract)>,
    pub views: Vec<&'a [u8]>,
    document: Value,
}

impl SourceGraphGlb<'_> {
    /// Unmodified declarations for geometry adapters. Material integrity does
    /// not qualify vertex frames, texture import, or rendering.
    #[must_use]
    pub fn document(&self) -> &Value {
        &self.document
    }
}

fn word(bytes: &[u8], offset: usize) -> Result<usize, String> {
    let raw = bytes.get(offset..offset + 4).ok_or("truncated GLB word")?;
    Ok(u32::from_le_bytes(raw.try_into().map_err(|_| "GLB word")?) as usize)
}

fn unsigned(value: &Value, field: &str) -> Result<usize, String> {
    value
        .get(field)
        .and_then(Value::as_u64)
        .and_then(|v| usize::try_from(v).ok())
        .ok_or_else(|| format!("invalid {field}"))
}

fn declared(document: &Value, field: &str) -> Result<bool, String> {
    let Some(value) = document.get(field) else {
        return Ok(false);
    };
    let list = value.as_array().ok_or_else(|| format!("invalid {field}"))?;
    let mut names = std::collections::BTreeSet::new();
    for value in list {
        let name = value
            .as_str()
            .ok_or_else(|| format!("invalid {field} entry"))?;
        if !names.insert(name) {
            return Err(format!("duplicate {field} entry"));
        }
    }
    Ok(names.contains(SOURCE_GRAPH_EXTENSION))
}

/// Carrier-byte limits are independent of JSON, decoded geometry and image
/// limits. They do not represent a GPU or whole-scene memory admission.
#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
pub enum SourceGraphGlbReadBudget {
    #[default]
    Standard,
    /// Explicit native-desktop transport, bounded to one GiB. Web callers must
    /// retain Standard; enabling a larger carrier never changes validation.
    LargeDesktop,
}

impl SourceGraphGlbReadBudget {
    /// Hosts can check file metadata before allocating the carrier bytes.
    pub fn validate_byte_length(self, byte_length: usize) -> Result<(), String> {
        let limit = match self {
            Self::Standard => 512 * 1024 * 1024,
            Self::LargeDesktop => {
                if cfg!(target_arch = "wasm32") {
                    return Err("large desktop GLB budget unavailable on Web".into());
                }
                1024 * 1024 * 1024
            }
        };
        if byte_length > limit {
            return Err("GLB carrier exceeds explicit byte budget".into());
        }
        Ok(())
    }
}

fn read_glb_document(
    raw: &[u8],
    budget: SourceGraphGlbReadBudget,
) -> Result<(Value, usize), String> {
    budget.validate_byte_length(raw.len())?;
    if raw.get(..4) != Some(b"glTF")
        || word(raw, 4)? != 2
        || word(raw, 8)? != raw.len()
        || raw.get(16..20) != Some(b"JSON")
    {
        return Err("invalid GLB header or allocation limit".into());
    }
    let json_len = word(raw, 12)?;
    if json_len == 0 || json_len > 16 * 1024 * 1024 || json_len % 4 != 0 {
        return Err("invalid GLB JSON allocation".into());
    }
    let json_end = 20usize.checked_add(json_len).ok_or("GLB JSON overflow")?;
    let document: Value = serde_json::from_slice(raw.get(20..json_end).ok_or("GLB JSON bounds")?)
        .map_err(|e| e.to_string())?;
    Ok((document, json_end))
}

pub fn read_source_graph_glb(raw: &[u8]) -> Result<Option<SourceGraphGlb<'_>>, String> {
    read_source_graph_glb_with_budget(raw, SourceGraphGlbReadBudget::Standard)
}

/// Apply an explicit carrier budget without relaxing any integrity, JSON or
/// source-program requirement. This does not admit the scene to rendering.
pub fn read_source_graph_glb_with_budget(
    raw: &[u8],
    budget: SourceGraphGlbReadBudget,
) -> Result<Option<SourceGraphGlb<'_>>, String> {
    let (document, json_end) = read_glb_document(raw, budget)?;
    let used = declared(&document, "extensionsUsed")?;
    let required = declared(&document, "extensionsRequired")?;
    let materials = document.get("materials").and_then(Value::as_array);
    let contains_payload = materials.is_some_and(|list| {
        list.iter().any(|m| {
            m.get("extensions")
                .and_then(|e| e.get(SOURCE_GRAPH_EXTENSION))
                .is_some()
        })
    });
    if !used && !required && !contains_payload {
        return Ok(None);
    }
    // Never allow ordinary PBR fallback to conceal an unimplemented source graph.
    if !used || !required || !contains_payload {
        return Err("source graph must be used, required, and attached to a material".into());
    }
    if document
        .get("asset")
        .and_then(|a| a.get("version"))
        .and_then(Value::as_str)
        != Some("2.0")
    {
        return Err("source graph requires glTF 2.0".into());
    }
    if raw.get(json_end + 4..json_end + 8) != Some(b"BIN\0") {
        return Err("source graph requires autonomous BIN chunk".into());
    }
    let bin_len = word(raw, json_end)?;
    let bin_start = json_end.checked_add(8).ok_or("BIN overflow")?;
    let bin_end = bin_start.checked_add(bin_len).ok_or("BIN overflow")?;
    if bin_len % 4 != 0 || bin_end != raw.len() {
        return Err("invalid BIN length or trailing chunks".into());
    }
    let bin = raw.get(bin_start..bin_end).ok_or("BIN bounds")?;
    let buffers = document
        .get("buffers")
        .and_then(Value::as_array)
        .ok_or("missing buffer")?;
    if buffers.len() != 1 || buffers[0].get("uri").is_some() {
        return Err("source graph requires one embedded buffer".into());
    }
    let logical_len = unsigned(&buffers[0], "byteLength")?;
    if logical_len > bin.len()
        || bin.len() - logical_len > 3
        || bin[logical_len..].iter().any(|b| *b != 0)
    {
        return Err("invalid embedded buffer length or padding".into());
    }
    let declarations = document
        .get("bufferViews")
        .and_then(Value::as_array)
        .ok_or("missing bufferViews")?;
    let mut views = Vec::with_capacity(declarations.len());
    for view in declarations {
        if unsigned(view, "buffer")? != 0 {
            return Err("external source graph bufferView".into());
        }
        let offset = if view.get("byteOffset").is_some() {
            unsigned(view, "byteOffset")?
        } else {
            0
        };
        let length = unsigned(view, "byteLength")?;
        let end = offset.checked_add(length).ok_or("bufferView overflow")?;
        if length == 0 || end > logical_len {
            return Err("bufferView outside logical BIN".into());
        }
        views.push(&bin[offset..end]);
    }
    let mut contracts = Vec::new();
    for (index, material) in materials.ok_or("missing materials")?.iter().enumerate() {
        if let Some(value) = material
            .get("extensions")
            .and_then(|e| e.get(SOURCE_GRAPH_EXTENSION))
        {
            let contract: SourceGraphMaterialContract =
                serde_json::from_value(value.clone()).map_err(|e| e.to_string())?;
            contract
                .validate_integrity(&views)
                .map_err(|e| format!("material {index}: {e:?}"))?;
            contracts.push((index, contract));
        }
    }
    Ok(Some(SourceGraphGlb {
        materials: contracts,
        views,
        document,
    }))
}

#[cfg(test)]
mod budget_tests {
    use super::*;

    #[test]
    fn standard_budget_retains_the_original_limit_including_boundary() {
        let budget = SourceGraphGlbReadBudget::default();
        assert_eq!(budget, SourceGraphGlbReadBudget::Standard);
        assert!(budget.validate_byte_length(512 * 1024 * 1024).is_ok());
        assert!(budget.validate_byte_length(512 * 1024 * 1024 + 1).is_err());
    }

    #[cfg(not(target_arch = "wasm32"))]
    #[test]
    fn desktop_budget_admits_faithful_large_carriers_and_has_a_hard_upper_bound() {
        let budget = SourceGraphGlbReadBudget::LargeDesktop;
        assert!(budget.validate_byte_length(709_944_920).is_ok());
        assert!(budget.validate_byte_length(1024 * 1024 * 1024).is_ok());
        assert!(budget.validate_byte_length(1024 * 1024 * 1024 + 1).is_err());
        assert!(budget.validate_byte_length(usize::MAX).is_err());
    }

    #[cfg(target_arch = "wasm32")]
    #[test]
    fn desktop_budget_is_not_a_web_memory_override() {
        assert!(
            SourceGraphGlbReadBudget::LargeDesktop
                .validate_byte_length(1)
                .is_err()
        );
    }

    #[test]
    fn larger_budget_keeps_header_checks_and_does_not_allocate_declared_json() {
        let mut raw = b"glTF".to_vec();
        raw.extend(2u32.to_le_bytes());
        raw.extend(20u32.to_le_bytes());
        raw.extend((16u32 * 1024 * 1024 + 4).to_le_bytes());
        raw.extend(b"JSON");
        let error = read_source_graph_glb_with_budget(&raw, SourceGraphGlbReadBudget::Standard)
            .err()
            .expect("oversized declared JSON refused");
        assert!(error.contains("JSON allocation"));
        #[cfg(not(target_arch = "wasm32"))]
        {
            let error =
                read_source_graph_glb_with_budget(&raw, SourceGraphGlbReadBudget::LargeDesktop)
                    .err()
                    .expect("same JSON limit for desktop budget");
            assert!(error.contains("JSON allocation"));
            raw[0] = 0;
            assert!(
                read_source_graph_glb_with_budget(&raw, SourceGraphGlbReadBudget::LargeDesktop)
                    .is_err()
            );
        }
    }
}
