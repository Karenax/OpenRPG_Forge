//! Shared, byte-only admission for the source-fragment GLB extension.
//! The adapted bytes are temporary; the caller retains the original pinned GLB.
//! Full glTF, geometry, image and GPU validation remain separate requirements.
use crate::source_fragment::{
    SOURCE_FRAGMENT_EXTENSION, SourceFragmentContract, SourcePngBufferView,
};
use std::{borrow::Cow, collections::BTreeSet};

pub struct PreparedSourceFragmentGlb<'a> {
    pub bytes: Cow<'a, [u8]>,
    /// Pinned extension images, deduplicated by buffer-view index. The caller
    /// must decode them under its own media budget before claiming validation.
    pub source_pngs: Vec<&'a [u8]>,
}

/// Admit the closed source-fragment declaration and expose temporary glTF bytes.
/// Ordinary GLBs borrow the original buffer; partial source graphs are refused.
#[expect(
    clippy::too_many_lines,
    reason = "Source contract admission and temporary adaptation form one transaction"
)]
pub fn prepare(raw: &[u8]) -> Result<PreparedSourceFragmentGlb<'_>, String> {
    let header: [u8; 20] = raw
        .get(..20)
        .ok_or("truncated GLB")?
        .try_into()
        .map_err(|_| "GLB header")?;
    let json_length = crate::governed_glb_json_length(
        &header,
        u64::try_from(raw.len()).map_err(|e| e.to_string())?,
    )
    .map_err(|e| e.to_string())?;
    let end = 20 + json_length;
    let json = raw.get(20..end).ok_or("GLB JSON bounds")?;
    let mut document: serde_json::Value =
        serde_json::from_slice(json).map_err(|e| e.to_string())?;
    if crate::source_graph_glb::source_graph_declared(&document)? {
        return Err(
            "source graph requires its own scene renderer; ordinary glTF loading is refused".into(),
        );
    }
    if crate::source_decal_glb::source_decal_declared(&document)? {
        return Err(
            "source decal requires its own scene renderer; ordinary glTF loading is refused".into(),
        );
    }
    let required = document
        .get("extensionsRequired")
        .and_then(serde_json::Value::as_array)
        .is_some_and(|values| {
            values
                .iter()
                .any(|value| value.as_str() == Some(SOURCE_FRAGMENT_EXTENSION))
        });
    if required
        && !document
            .get("extensionsUsed")
            .and_then(serde_json::Value::as_array)
            .is_some_and(|used| {
                used.iter()
                    .any(|name| name.as_str() == Some(SOURCE_FRAGMENT_EXTENSION))
            })
    {
        return Err("required source fragment extension missing from extensionsUsed".into());
    }
    if required {
        crate::validate_governed_glb_json(json).map_err(|e| e.to_string())?;
    }
    let materials = document
        .get("materials")
        .and_then(serde_json::Value::as_array);
    if required && materials.is_none_or(Vec::is_empty) {
        return Err("empty required source materials".into());
    }
    let mut seen = BTreeSet::new();
    let mut source_pngs = Vec::new();
    for material in materials.into_iter().flatten() {
        let value = material
            .get("extensions")
            .and_then(|extensions| extensions.get(SOURCE_FRAGMENT_EXTENSION));
        let Some(value) = value else {
            if required {
                return Err("required source fragment missing on material".into());
            }
            continue;
        };
        let contract: SourceFragmentContract =
            serde_json::from_value(value.clone()).map_err(|e| e.to_string())?;
        contract.validate().map_err(str::to_owned)?;
        let mut images = Vec::new();
        images.extend(contract.normal_png.as_ref());
        images.extend(contract.base_png.as_ref().map(|base| &base.image));
        if let Some(program) = &contract.world_blend {
            images.extend([
                &program.blend_mask,
                &program.noise_texture,
                &program.noise_mask,
            ]);
        }
        if !images.is_empty() {
            let chunk_header = raw
                .get(end..end + 8)
                .ok_or("source image BIN header missing")?;
            if &chunk_header[4..8] != b"BIN\0" {
                return Err("source image requires GLB BIN chunk".into());
            }
            let length = usize::try_from(u32::from_le_bytes(
                chunk_header[..4].try_into().map_err(|_| "BIN length")?,
            ))
            .map_err(|e| e.to_string())?;
            let bin_end = (end + 8)
                .checked_add(length)
                .ok_or("source image BIN overflow")?;
            let bin = raw.get(end + 8..bin_end).ok_or("source image BIN bounds")?;
            let views = document
                .get("bufferViews")
                .and_then(serde_json::Value::as_array)
                .ok_or("source image bufferViews missing")?;
            for image in images {
                let bytes = source_png_view_bytes(bin, views, image)?;
                if seen.insert(image.buffer_view) {
                    source_pngs.push(bytes);
                }
            }
        }
    }
    if !required {
        return Ok(PreparedSourceFragmentGlb {
            bytes: Cow::Borrowed(raw),
            source_pngs,
        });
    }
    let required = document
        .get_mut("extensionsRequired")
        .and_then(serde_json::Value::as_array_mut)
        .ok_or("required list")?;
    required.retain(|value| value.as_str() != Some(SOURCE_FRAGMENT_EXTENSION));
    document["extensions"][SOURCE_FRAGMENT_EXTENSION] =
        serde_json::json!({"schema_version":1,"required":true});
    let mut new_json = serde_json::to_vec(&document).map_err(|e| e.to_string())?;
    new_json.resize(new_json.len().next_multiple_of(4), b' ');
    let mut output = Vec::with_capacity(raw.len() + new_json.len());
    output.extend_from_slice(&raw[..12]);
    output.extend_from_slice(
        &u32::try_from(new_json.len())
            .map_err(|e| e.to_string())?
            .to_le_bytes(),
    );
    output.extend_from_slice(b"JSON");
    output.extend_from_slice(&new_json);
    output.extend_from_slice(raw.get(end..).ok_or("GLB tail")?);
    let length = u32::try_from(output.len()).map_err(|e| e.to_string())?;
    output[8..12].copy_from_slice(&length.to_le_bytes());
    Ok(PreparedSourceFragmentGlb {
        bytes: Cow::Owned(output),
        source_pngs,
    })
}

/// Verify the exact source image before any decoder or renderer sees it.
fn source_png_view_bytes<'a>(
    bin: &'a [u8],
    views: &[serde_json::Value],
    image: &SourcePngBufferView,
) -> Result<&'a [u8], String> {
    let view = views
        .get(usize::try_from(image.buffer_view).map_err(|e| e.to_string())?)
        .ok_or("source PNG bufferView missing")?;
    if view.get("buffer").and_then(serde_json::Value::as_u64) != Some(0) {
        return Err("source PNG requires buffer zero".into());
    }
    let offset = match view.get("byteOffset") {
        None => 0,
        Some(value) => value.as_u64().ok_or("source PNG offset must be unsigned")?,
    };
    let length = view
        .get("byteLength")
        .and_then(serde_json::Value::as_u64)
        .ok_or("source PNG length missing")?;
    if length == 0 || length > 64 * 1024 * 1024 {
        return Err("source PNG length outside image allocation limit".into());
    }
    let offset = usize::try_from(offset).map_err(|e| e.to_string())?;
    let length = usize::try_from(length).map_err(|e| e.to_string())?;
    let end = offset
        .checked_add(length)
        .ok_or("source PNG view overflow")?;
    let bytes = bin.get(offset..end).ok_or("source PNG view outside BIN")?;
    if forge_core::bytes_sha256(bytes).to_string() != image.sha256 {
        return Err("source PNG SHA mismatch".into());
    }
    Ok(bytes)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn source_png_views_require_exact_pinned_bounded_bin_bytes() {
        let bin = [0x81, 0x20, 0xff, 0x42];
        let image = SourcePngBufferView {
            buffer_view: 0,
            sha256: forge_core::bytes_sha256(&bin).to_string(),
        };
        let valid = serde_json::json!({"buffer":0,"byteLength":4});
        assert_eq!(
            source_png_view_bytes(&bin, std::slice::from_ref(&valid), &image).unwrap(),
            bin
        );
        for invalid in [
            serde_json::json!({"buffer":1,"byteLength":4}),
            serde_json::json!({"buffer":0,"byteOffset":-1,"byteLength":4}),
            serde_json::json!({"buffer":0,"byteOffset":1,"byteLength":4}),
            serde_json::json!({"buffer":0,"byteLength":0}),
            serde_json::json!({"buffer":0,"byteLength":67_108_865}),
            serde_json::json!({"buffer":0,"byteOffset":18_446_744_073_709_551_615_u64,"byteLength":4}),
            serde_json::json!({"buffer":0}),
        ] {
            assert!(source_png_view_bytes(&bin, &[invalid], &image).is_err());
        }
        let mut wrong = image.clone();
        wrong.sha256 = "a".repeat(64);
        assert!(source_png_view_bytes(&bin, std::slice::from_ref(&valid), &wrong).is_err());
        wrong.buffer_view = 1;
        assert!(source_png_view_bytes(&bin, &[valid], &wrong).is_err());
    }
}
