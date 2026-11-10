//! Bounded integrity reader for source mesh-decal carriers.
//!
//! Admission only checks the carrier and pinned payloads. It does not execute
//! shaders, create a `DBuffer`, or qualify Studio, placement, or runtime.
use crate::source_graph_glb::{SourceGraphGlbReadBudget, read_glb_document};
use forge_core::{ContentHash, bytes_sha256};
use serde_json::Value;
use std::collections::BTreeSet;

pub const SOURCE_DECAL_EXTENSION: &str = "OPENRPG_source_decal";
const MAX_PROGRAM: usize = 1024 * 1024;
const MAX_IMAGE_DIMENSION: usize = 16384;
const MAX_MATERIALS: usize = 64;
const MAX_BUFFER_VIEWS: usize = 4096;

/// Detect a decal declaration without admitting the carrier for rendering.
pub fn source_decal_declared_in_glb(
    raw: &[u8],
    budget: SourceGraphGlbReadBudget,
) -> Result<bool, String> {
    let (document, _) = read_glb_document(raw, budget)?;
    source_decal_declared(&document)
}

/// Borrowed, validated resources from a source-decal carrier.
pub struct SourceDecalGlb<'a> {
    pub materials: Vec<(usize, Value)>,
    pub views: Vec<&'a [u8]>,
    document: Value,
}

impl SourceDecalGlb<'_> {
    #[must_use]
    pub fn document(&self) -> &Value {
        &self.document
    }
}

fn declared(document: &Value, field: &str) -> Result<bool, String> {
    let Some(value) = document.get(field) else {
        return Ok(false);
    };
    let list = value.as_array().ok_or_else(|| format!("invalid {field}"))?;
    let mut unique = BTreeSet::new();
    for entry in list {
        let name = entry
            .as_str()
            .ok_or_else(|| format!("invalid {field} entry"))?;
        if !unique.insert(name) {
            return Err(format!("duplicate {field} entry"));
        }
    }
    Ok(unique.contains(SOURCE_DECAL_EXTENSION))
}

/// An incomplete declaration is refused rather than treated as ordinary glTF.
pub fn source_decal_declared(document: &Value) -> Result<bool, String> {
    let used = declared(document, "extensionsUsed")?;
    let required = declared(document, "extensionsRequired")?;
    let payload = document
        .get("materials")
        .and_then(Value::as_array)
        .is_some_and(|items| {
            items.iter().any(|m| {
                m.get("extensions")
                    .and_then(|e| e.get(SOURCE_DECAL_EXTENSION))
                    .is_some()
            })
        });
    if !used && !required && !payload {
        return Ok(false);
    }
    if !used || !required || !payload {
        return Err("source decal must be used, required, and attached to a material".into());
    }
    Ok(true)
}

fn integer(value: &Value, field: &str) -> Result<usize, String> {
    value
        .get(field)
        .and_then(Value::as_u64)
        .and_then(|x| usize::try_from(x).ok())
        .ok_or_else(|| format!("invalid {field}"))
}
fn word(raw: &[u8], pos: usize) -> Result<usize, String> {
    let end = pos.checked_add(4).ok_or("GLB word overflow")?;
    let bytes: [u8; 4] = raw
        .get(pos..end)
        .ok_or("truncated GLB word")?
        .try_into()
        .map_err(|_| "invalid GLB word")?;
    Ok(u32::from_le_bytes(bytes) as usize)
}
fn pinned<'a>(reference: &Value, views: &[&'a [u8]]) -> Result<&'a [u8], String> {
    let index = integer(reference, "buffer_view")?;
    let data = *views.get(index).ok_or("buffer view index out of range")?;
    let expected: ContentHash = reference
        .get("sha256")
        .and_then(Value::as_str)
        .ok_or("missing sha256")?
        .parse()
        .map_err(|_| "invalid sha256")?;
    if bytes_sha256(data) != expected {
        return Err("source decal payload hash mismatch".into());
    }
    Ok(data)
}
fn program(reference: &Value, views: &[&[u8]]) -> Result<(), String> {
    let bytes = pinned(reference, views)?;
    if bytes.len() > MAX_PROGRAM || bytes.contains(&0) || std::str::from_utf8(bytes).is_err() {
        return Err("invalid source decal shader payload".into());
    }
    Ok(())
}
fn validate_texture(texture: &Value, views: &[&[u8]]) -> Result<(), String> {
    let png = pinned(&texture["png"], views)?;
    if !png.starts_with(b"\x89PNG\r\n\x1a\n") {
        return Err("invalid source decal PNG header".into());
    }
    pinned(&texture["source_meta"], views)?;
    if texture.get("source_meta_sha256").and_then(Value::as_str)
        != texture["source_meta"].get("sha256").and_then(Value::as_str)
        || texture
            .get("source_meta_sha256")
            .and_then(Value::as_str)
            .is_none()
    {
        return Err("source decal metadata hash declaration mismatch".into());
    }
    let imported = texture
        .get("imported_storage")
        .ok_or("missing imported texture")?;
    if imported.get("format").and_then(Value::as_str) != Some("bc3_rgba_unorm_srgb") {
        return Err("unsupported source decal texture format".into());
    }
    let width = integer(imported, "width")?;
    let height = integer(imported, "height")?;
    if width == 0 || height == 0 || width > MAX_IMAGE_DIMENSION || height > MAX_IMAGE_DIMENSION {
        return Err("invalid source decal texture dimensions".into());
    }
    let storage = pinned(&imported["storage"], views)?;
    pinned(&imported["capture"], views)?;
    let mips = imported
        .get("mips")
        .and_then(Value::as_array)
        .ok_or("missing source decal mips")?;
    let expected_count = usize::try_from(width.max(height).ilog2()).map_err(|_| "mip count")? + 1;
    if mips.len() != expected_count {
        return Err("invalid source decal mip count".into());
    }
    let mut offset = 0usize;
    for (level, mip) in mips.iter().enumerate() {
        let w = (width >> level).max(1);
        let h = (height >> level).max(1);
        let size = w
            .div_ceil(4)
            .checked_mul(h.div_ceil(4))
            .and_then(|x| x.checked_mul(16))
            .ok_or("mip size overflow")?;
        if integer(mip, "width")? != w
            || integer(mip, "height")? != h
            || integer(mip, "offset")? != offset
            || integer(mip, "bytes")? != size
        {
            return Err("invalid source decal mip layout".into());
        }
        let end = offset.checked_add(size).ok_or("mip bounds overflow")?;
        let bytes = storage
            .get(offset..end)
            .ok_or("source decal mip out of bounds")?;
        let expected: ContentHash = mip
            .get("sha256")
            .and_then(Value::as_str)
            .ok_or("missing mip sha256")?
            .parse()
            .map_err(|_| "invalid mip sha256")?;
        if bytes_sha256(bytes) != expected {
            return Err("source decal mip hash mismatch".into());
        }
        offset = end;
    }
    if offset != storage.len() {
        return Err("unexpected source decal texture data".into());
    }
    Ok(())
}

/// Validate a v2-tested source decal carrier without admitting it to rendering.
#[expect(
    clippy::too_many_lines,
    reason = "Carrier structure and all resource checks form one bounded admission transaction"
)]
pub fn read_source_decal_glb(raw: &[u8]) -> Result<Option<SourceDecalGlb<'_>>, String> {
    let (document, json_end) = read_glb_document(raw, SourceGraphGlbReadBudget::Standard)?;
    if !source_decal_declared(&document)? {
        return Ok(None);
    }
    if document
        .get("asset")
        .and_then(|a| a.get("version"))
        .and_then(Value::as_str)
        != Some("2.0")
    {
        return Err("source decal requires glTF 2.0".into());
    }
    if raw.get(json_end + 4..json_end + 8) != Some(b"BIN\0") {
        return Err("source decal requires embedded BIN".into());
    }
    let len = word(raw, json_end)?;
    let start = json_end.checked_add(8).ok_or("BIN overflow")?;
    let end = start.checked_add(len).ok_or("BIN overflow")?;
    if len % 4 != 0 || end != raw.len() {
        return Err("invalid source decal BIN length".into());
    }
    let bin = raw.get(start..end).ok_or("BIN bounds")?;
    let buffers = document
        .get("buffers")
        .and_then(Value::as_array)
        .ok_or("missing buffers")?;
    if buffers.len() != 1 || buffers[0].get("uri").is_some() {
        return Err("source decal requires one embedded buffer".into());
    }
    let logical = integer(&buffers[0], "byteLength")?;
    if logical > bin.len() || bin.len() - logical > 3 || bin[logical..].iter().any(|x| *x != 0) {
        return Err("invalid source decal buffer length".into());
    }
    let declarations = document
        .get("bufferViews")
        .and_then(Value::as_array)
        .ok_or("missing buffer views")?;
    if declarations.len() > MAX_BUFFER_VIEWS {
        return Err("too many source decal buffer views".into());
    }
    let mut views = Vec::with_capacity(declarations.len());
    for view in declarations {
        if integer(view, "buffer")? != 0 {
            return Err("external source decal buffer".into());
        }
        let offset = if view.get("byteOffset").is_some() {
            integer(view, "byteOffset")?
        } else {
            0
        };
        let length = integer(view, "byteLength")?;
        let end = offset.checked_add(length).ok_or("buffer view overflow")?;
        if length == 0 || end > logical {
            return Err("source decal view outside buffer".into());
        }
        views.push(&bin[offset..end]);
    }
    let mut materials = Vec::new();
    let declared_materials = document
        .get("materials")
        .and_then(Value::as_array)
        .ok_or("missing materials")?;
    if declared_materials.len() > MAX_MATERIALS {
        return Err("too many source decal materials".into());
    }
    for (index, material) in declared_materials.iter().enumerate() {
        let Some(c) = material
            .get("extensions")
            .and_then(|e| e.get(SOURCE_DECAL_EXTENSION))
        else {
            continue;
        };
        if c.get("schema_version").and_then(Value::as_u64) != Some(1)
            || c.get("status").and_then(Value::as_str)
                != Some("SOURCE_DBUFFER_SYNTHETIC_REFERENCE_VERIFIED_NOT_RUNTIME_READY")
            || c.get("runtime_verified").and_then(Value::as_bool) != Some(false)
            || c.get("full_import").and_then(Value::as_bool) != Some(false)
        {
            return Err("unsupported source decal contract or qualification".into());
        }
        program(&c["surface_wgsl"], &views)?;
        let textures = c
            .get("textures")
            .and_then(Value::as_array)
            .ok_or("missing decal textures")?;
        if textures.len() != 1 {
            return Err("unsupported source decal texture count".into());
        }
        validate_texture(&textures[0], &views)?;
        let db = c
            .get("dbuffer_reference")
            .ok_or("missing DBuffer reference")?;
        if db.get("schema_version").and_then(Value::as_u64) != Some(1) {
            return Err("unsupported DBuffer reference".into());
        }
        let programs = db
            .get("programs")
            .and_then(Value::as_object)
            .ok_or("missing DBuffer programs")?;
        if programs.len() != 3 {
            return Err("invalid DBuffer program count".into());
        }
        for name in ["generation", "color_composition", "normal_composition"] {
            program(programs.get(name).ok_or("missing DBuffer program")?, &views)?;
        }
        materials.push((index, c.clone()));
    }
    if materials.is_empty() {
        return Err("source decal has no material".into());
    }
    Ok(Some(SourceDecalGlb {
        materials,
        views,
        document,
    }))
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    fn fixture(invalid_mip_layout: bool) -> Vec<u8> {
        let mut bin = Vec::<u8>::new();
        let mut views = Vec::<Value>::new();
        let mut add = |data: &[u8]| {
            while bin.len() % 4 != 0 {
                bin.push(0);
            }
            let idx = views.len();
            views.push(json!({"buffer":0,"byteOffset":bin.len(),"byteLength":data.len()}));
            bin.extend_from_slice(data);
            json!({"buffer_view":idx,"sha256":bytes_sha256(data)})
        };
        let png = add(b"\x89PNG\r\n\x1a\nfixture");
        let meta = add(b"meta");
        let capture = add(b"capture");
        let surface = add(b"surface wgsl");
        let generation = add(b"generation wgsl");
        let color = add(b"color wgsl");
        let normal = add(b"normal wgsl");
        let bc = vec![7u8; 48];
        let storage = add(&bc);
        let mut mips=(0..3).map(|level|json!({"width":(4usize>>level).max(1),"height":(4usize>>level).max(1),"offset":level*16,"bytes":16,"sha256":bytes_sha256(&bc[level*16..level*16+16])})).collect::<Vec<_>>();
        if invalid_mip_layout {
            mips[0]["bytes"] = json!(15);
        }
        drop(add);
        let logical = bin.len();
        while bin.len() % 4 != 0 {
            bin.push(0);
        }
        let meta_sha = meta["sha256"].clone();
        let contract = json!({"schema_version":1,"status":"SOURCE_DBUFFER_SYNTHETIC_REFERENCE_VERIFIED_NOT_RUNTIME_READY","runtime_verified":false,"full_import":false,"surface_wgsl":surface,"textures":[{"png":png,"source_meta":meta,"source_meta_sha256":meta_sha,"imported_storage":{"format":"bc3_rgba_unorm_srgb","width":4,"height":4,"storage":storage,"capture":capture,"mips":mips}}],"dbuffer_reference":{"schema_version":1,"programs":{"generation":generation,"color_composition":color,"normal_composition":normal}}});
        let doc = json!({"asset":{"version":"2.0"},"extensionsUsed":[SOURCE_DECAL_EXTENSION],"extensionsRequired":[SOURCE_DECAL_EXTENSION],"buffers":[{"byteLength":logical}],"bufferViews":views,"materials":[{"extensions":{(SOURCE_DECAL_EXTENSION):contract}}]});
        let mut j = serde_json::to_vec(&doc).unwrap();
        while j.len() % 4 != 0 {
            j.push(b' ');
        }
        let total = 28 + j.len() + bin.len();
        let mut raw = Vec::new();
        raw.extend_from_slice(b"glTF");
        raw.extend_from_slice(&2u32.to_le_bytes());
        raw.extend_from_slice(&(total as u32).to_le_bytes());
        raw.extend_from_slice(&(j.len() as u32).to_le_bytes());
        raw.extend_from_slice(b"JSON");
        raw.extend_from_slice(&j);
        raw.extend_from_slice(&(bin.len() as u32).to_le_bytes());
        raw.extend_from_slice(b"BIN\0");
        raw.extend_from_slice(&bin);
        raw
    }
    #[test]
    fn validates_declared_carrier_and_refuses_ordinary_fragment_fallback() {
        let raw = fixture(false);
        let admission = crate::media_validation::validate_declared_media(
            &raw,
            crate::media_validation::MediaFormat::Glb,
            crate::media_validation::MediaLimits::default(),
            SourceGraphGlbReadBudget::Standard,
        );
        assert!(admission.unwrap_err().reason.contains("source decal"));
        let parsed = read_source_decal_glb(&raw).unwrap().unwrap();
        assert_eq!(parsed.materials.len(), 1);
        assert_eq!(parsed.views.len(), 8);
        assert!(
            crate::source_fragment_glb::prepare(&raw)
                .err()
                .unwrap()
                .contains("source decal requires")
        );
    }
    #[test]
    fn rejects_changed_embedded_payload() {
        let mut raw = fixture(false);
        let json_len = u32::from_le_bytes(raw[12..16].try_into().unwrap()) as usize;
        let bin_start = 28 + json_len;
        raw[bin_start + 8] ^= 1;
        assert!(
            read_source_decal_glb(&raw)
                .err()
                .unwrap()
                .contains("hash mismatch")
        );
    }
    #[test]
    fn rejects_invalid_compressed_mip_layout() {
        assert!(
            read_source_decal_glb(&fixture(true))
                .err()
                .unwrap()
                .contains("mip layout")
        );
    }
    #[test]
    fn rejects_partial_or_duplicate_extension_declarations() {
        let partial = json!({"extensionsUsed":[SOURCE_DECAL_EXTENSION],"materials":[{"extensions":{(SOURCE_DECAL_EXTENSION):{}}}]});
        assert!(source_decal_declared(&partial).is_err());
        let duplicate = json!({"extensionsUsed":[SOURCE_DECAL_EXTENSION,SOURCE_DECAL_EXTENSION]});
        assert!(source_decal_declared(&duplicate).is_err());
        assert!(!source_decal_declared(&json!({})).unwrap());
    }
}
