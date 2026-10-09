//! Decode dedicated shader inputs without normalizing, UV flipping, or replacing
//! absent authored color with white. This is transport, not render admission.
use crate::source_graph_glb::SourceGraphGlb;
use serde_json::Value;

/// Unity supplies eight UV channels. Channel zero has its existing dedicated field.
pub const ADDITIONAL_UV_CHANNELS: usize = 7;
pub type AdditionalUvChannels<const WIDTH: usize> =
    [Option<Vec<[f32; WIDTH]>>; ADDITIONAL_UV_CHANNELS];

pub struct SourceGraphPrimitiveAttributes {
    pub material_index: usize,
    pub original_normals: Vec<[f32; 3]>,
    /// None retains source absence; GPU input defaults require separate evidence.
    pub original_tangents: Option<Vec<[f32; 4]>>,
    pub original_uv0: Vec<[f32; 4]>,
    /// Slots zero through six retain authored channels one through seven.
    pub original_additional_uvs: AdditionalUvChannels<4>,
    pub original_colors: Option<Vec<[f32; 4]>>,
}

/// Static geometry in the declared GLB local space. No scene transform, vertex
/// welding, tangent generation, or source shader frame qualification occurs.
pub struct SourceGraphPrimitiveGeometry {
    pub positions: Vec<[f32; 3]>,
    pub normals: Vec<[f32; 3]>,
    pub tangents: Option<Vec<[f32; 4]>>,
    pub uv0: Vec<[f32; 2]>,
    pub additional_uvs: AdditionalUvChannels<2>,
    pub indices: Vec<u32>,
    pub source: SourceGraphPrimitiveAttributes,
}

fn number(value: &Value, field: &str) -> Result<usize, String> {
    value
        .get(field)
        .and_then(Value::as_u64)
        .and_then(|v| usize::try_from(v).ok())
        .ok_or_else(|| format!("invalid {field}"))
}

fn optional_attribute<const WIDTH: usize>(
    document: &Value,
    views: &[&[u8]],
    attrs: &Value,
    name: &str,
    count: usize,
) -> Result<Option<Vec<[f32; WIDTH]>>, String> {
    if attrs.get(name).is_none() {
        return Ok(None);
    }
    // A declared but malformed stream is an error, never source absence.
    decode(document, views, number(attrs, name)?, count).map(Some)
}

fn offset(value: &Value) -> Result<usize, String> {
    if value.get("byteOffset").is_none() {
        Ok(0)
    } else {
        number(value, "byteOffset")
    }
}

fn triangle_mode(primitive: &Value) -> Result<(), String> {
    let mode = match primitive.get("mode") {
        None => 4,
        Some(value) => value.as_u64().ok_or("invalid primitive mode")?,
    };
    if mode != 4 || primitive.get("targets").is_some() {
        return Err("source morph/nontriangle adapter required".into());
    }
    Ok(())
}

fn uv_channel_count(attrs: &Value, prefix: &str) -> Result<usize, String> {
    let mut count = 0;
    for name in attrs.as_object().ok_or("invalid attributes")?.keys() {
        if let Some(suffix) = name.strip_prefix(prefix) {
            let channel = suffix.parse::<usize>().map_err(|_| "invalid UV channel")?;
            if channel > ADDITIONAL_UV_CHANNELS || suffix != channel.to_string() {
                return Err("unsupported UV channel adapter required".into());
            }
            count += usize::from(channel > 0);
        }
    }
    Ok(count)
}

fn additional_uvs<const WIDTH: usize>(
    document: &Value,
    views: &[&[u8]],
    attrs: &Value,
    prefix: &str,
    count: usize,
) -> Result<AdditionalUvChannels<WIDTH>, String> {
    uv_channel_count(attrs, prefix)?;
    let mut channels = std::array::from_fn(|_| None);
    for (slot, channel) in channels.iter_mut().enumerate() {
        let name = format!("{prefix}{}", slot + 1);
        if attrs.get(&name).is_some() {
            *channel = Some(decode(document, views, number(attrs, &name)?, count)?);
        }
    }
    Ok(channels)
}

fn check_geometry_budget(attrs: &Value, count: usize, indices: usize) -> Result<(), String> {
    // Include both conventional and unmodified source UV buffers before allocating.
    let floats = 23
        + usize::from(attrs.get("_OPENRPG_SOURCE_COLOR").is_some()) * 4
        + uv_channel_count(attrs, "TEXCOORD_")? * 2
        + uv_channel_count(attrs, "_OPENRPG_SOURCE_UV")? * 4;
    let budget = count
        .checked_mul(floats)
        .and_then(|n| n.checked_mul(4))
        .and_then(|n| indices.checked_mul(4).and_then(|i| n.checked_add(i)));
    if budget.is_none_or(|n| n > 512 * 1024 * 1024) {
        return Err("decoded geometry allocation limit".into());
    }
    Ok(())
}

fn decode<const WIDTH: usize>(
    document: &Value,
    views: &[&[u8]],
    index: usize,
    count: usize,
) -> Result<Vec<[f32; WIDTH]>, String> {
    let accessor = document
        .get("accessors")
        .and_then(Value::as_array)
        .and_then(|a| a.get(index))
        .ok_or("missing source accessor")?;
    let kind = match WIDTH {
        2 => "VEC2",
        3 => "VEC3",
        4 => "VEC4",
        _ => return Err("unsupported source width".into()),
    };
    if accessor.get("componentType").and_then(Value::as_u64) != Some(5126)
        || accessor.get("type").and_then(Value::as_str) != Some(kind)
        || number(accessor, "count")? != count
        || count == 0
        || accessor.get("sparse").is_some()
        || !matches!(accessor.get("normalized"), None | Some(Value::Bool(false)))
    {
        return Err("source attribute encoding/count differs".into());
    }
    let view_index = number(accessor, "bufferView")?;
    let declaration = document
        .get("bufferViews")
        .and_then(Value::as_array)
        .and_then(|v| v.get(view_index))
        .ok_or("missing source bufferView")?;
    let bytes = views.get(view_index).ok_or("missing source buffer bytes")?;
    let width = WIDTH * 4;
    let stride = if declaration.get("byteStride").is_some() {
        number(declaration, "byteStride")?
    } else {
        width
    };
    let start = offset(accessor)?;
    if start % 4 != 0
        || offset(declaration)? % 4 != 0
        || stride < width
        || stride % 4 != 0
        || (declaration.get("byteStride").is_some() && stride > 252)
    {
        return Err("misaligned source attribute".into());
    }
    let end = (count - 1)
        .checked_mul(stride)
        .and_then(|v| v.checked_add(start))
        .and_then(|v| v.checked_add(width))
        .ok_or("source accessor overflow")?;
    if end > bytes.len() {
        return Err("source accessor outside bufferView".into());
    }
    let mut output = Vec::with_capacity(count);
    for vertex in 0..count {
        let base = start + vertex * stride;
        let mut item = [0.0; WIDTH];
        for (component, value) in item.iter_mut().enumerate() {
            let at = base + component * 4;
            *value = f32::from_le_bytes(
                bytes[at..at + 4]
                    .try_into()
                    .map_err(|_| "source float bytes")?,
            );
            if !value.is_finite() {
                return Err("nonfinite original shader input".into());
            }
        }
        output.push(item);
    }
    Ok(output)
}

fn indices(
    document: &Value,
    views: &[&[u8]],
    index: Option<usize>,
    vertices: usize,
) -> Result<Vec<u32>, String> {
    let Some(index) = index else {
        if vertices == 0 || !vertices.is_multiple_of(3) {
            return Err("invalid nonindexed triangle count".into());
        }
        let end = u32::try_from(vertices).map_err(|_| "vertex index overflow")?;
        return Ok((0..end).collect());
    };
    let accessor = document
        .get("accessors")
        .and_then(Value::as_array)
        .and_then(|a| a.get(index))
        .ok_or("missing index accessor")?;
    let width = match number(accessor, "componentType")? {
        5121 => 1,
        5123 => 2,
        5125 => 4,
        _ => return Err("unsupported index component type".into()),
    };
    let count = number(accessor, "count")?;
    if count == 0
        || count % 3 != 0
        || accessor.get("type").and_then(Value::as_str) != Some("SCALAR")
        || accessor.get("sparse").is_some()
        || !matches!(accessor.get("normalized"), None | Some(Value::Bool(false)))
    {
        return Err("invalid source triangle indices".into());
    }
    let vi = number(accessor, "bufferView")?;
    let declaration = document
        .get("bufferViews")
        .and_then(Value::as_array)
        .and_then(|v| v.get(vi))
        .ok_or("missing index bufferView")?;
    if declaration.get("byteStride").is_some() || offset(declaration)? % width != 0 {
        return Err("strided or misaligned index buffer".into());
    }
    let start = offset(accessor)?;
    let end = count
        .checked_mul(width)
        .and_then(|n| n.checked_add(start))
        .ok_or("index overflow")?;
    let bytes = views
        .get(vi)
        .and_then(|v| v.get(start..end))
        .ok_or("index buffer bounds")?;
    if start % width != 0 {
        return Err("misaligned index accessor".into());
    }
    let mut result = Vec::with_capacity(count);
    for raw in bytes.chunks_exact(width) {
        let value = match width {
            1 => u32::from(raw[0]),
            2 => u32::from(u16::from_le_bytes([raw[0], raw[1]])),
            _ => u32::from_le_bytes([raw[0], raw[1], raw[2], raw[3]]),
        };
        if usize::try_from(value).map_or(true, |v| v >= vertices) {
            return Err("index references missing source vertex".into());
        }
        result.push(value);
    }
    Ok(result)
}

pub fn read_source_graph_primitive_geometry(
    glb: &SourceGraphGlb<'_>,
    mesh: usize,
    primitive: usize,
) -> Result<SourceGraphPrimitiveGeometry, String> {
    let document = glb.document();
    let p = document
        .get("meshes")
        .and_then(Value::as_array)
        .and_then(|m| m.get(mesh))
        .and_then(|m| m.get("primitives"))
        .and_then(Value::as_array)
        .and_then(|p| p.get(primitive))
        .ok_or("missing source primitive")?;
    triangle_mode(p)?;
    let attrs = p.get("attributes").ok_or("missing attributes")?;
    let position = number(attrs, "POSITION")?;
    let accessors = document
        .get("accessors")
        .and_then(Value::as_array)
        .ok_or("missing accessors")?;
    let count = number(
        accessors.get(position).ok_or("missing position accessor")?,
        "count",
    )?;
    let index = if p.get("indices").is_some() {
        Some(number(p, "indices")?)
    } else {
        None
    };
    let index_count = match index {
        Some(i) => number(accessors.get(i).ok_or("missing index accessor")?, "count")?,
        None => count,
    };
    check_geometry_budget(attrs, count, index_count)?;
    let source = read_source_graph_primitive_attributes(glb, mesh, primitive)?;
    Ok(SourceGraphPrimitiveGeometry {
        positions: decode(document, &glb.views, position, count)?,
        normals: decode(document, &glb.views, number(attrs, "NORMAL")?, count)?,
        tangents: optional_attribute(document, &glb.views, attrs, "TANGENT", count)?,
        uv0: decode(document, &glb.views, number(attrs, "TEXCOORD_0")?, count)?,
        additional_uvs: additional_uvs(document, &glb.views, attrs, "TEXCOORD_", count)?,
        indices: indices(document, &glb.views, index, count)?,
        source,
    })
}

pub fn read_source_graph_primitive_attributes(
    glb: &SourceGraphGlb<'_>,
    mesh: usize,
    primitive: usize,
) -> Result<SourceGraphPrimitiveAttributes, String> {
    let document = glb.document();
    let p = document
        .get("meshes")
        .and_then(Value::as_array)
        .and_then(|m| m.get(mesh))
        .and_then(|m| m.get("primitives"))
        .and_then(Value::as_array)
        .and_then(|p| p.get(primitive))
        .ok_or("missing source primitive")?;
    triangle_mode(p)?;
    let material_index = number(p, "material")?;
    if !glb
        .materials
        .iter()
        .any(|(index, _)| *index == material_index)
    {
        return Err("primitive has no verified source graph material".into());
    }
    let attrs = p.get("attributes").ok_or("missing attributes")?;
    if attrs
        .as_object()
        .ok_or("invalid attributes")?
        .keys()
        .any(|name| name.starts_with("JOINTS_") || name.starts_with("WEIGHTS_"))
    {
        return Err("source rig adapter required".into());
    }
    let position = number(attrs, "POSITION")?;
    let position = document
        .get("accessors")
        .and_then(Value::as_array)
        .and_then(|a| a.get(position))
        .ok_or("missing position accessor")?;
    let count = number(position, "count")?;
    check_geometry_budget(attrs, count, 0)?;
    Ok(SourceGraphPrimitiveAttributes {
        material_index,
        original_normals: decode(
            document,
            &glb.views,
            number(attrs, "_OPENRPG_SOURCE_NORMAL")?,
            count,
        )?,
        original_tangents: optional_attribute(
            document,
            &glb.views,
            attrs,
            "_OPENRPG_SOURCE_TANGENT",
            count,
        )?,
        original_uv0: decode(
            document,
            &glb.views,
            number(attrs, "_OPENRPG_SOURCE_UV0")?,
            count,
        )?,
        original_additional_uvs: additional_uvs(
            document,
            &glb.views,
            attrs,
            "_OPENRPG_SOURCE_UV",
            count,
        )?,
        original_colors: if attrs.get("_OPENRPG_SOURCE_COLOR").is_some() {
            Some(decode(
                document,
                &glb.views,
                number(attrs, "_OPENRPG_SOURCE_COLOR")?,
                count,
            )?)
        } else {
            None
        },
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn absent_tangent_stays_absent_and_present_values_are_exact() {
        let (d, b) = fixture();
        assert!(
            optional_attribute::<4>(&d, &[], &serde_json::json!({}), "TANGENT", 2)
                .unwrap()
                .is_none()
        );
        let attrs = serde_json::json!({"TANGENT":0});
        assert_eq!(
            optional_attribute::<4>(&d, &[&b], &attrs, "TANGENT", 2).unwrap(),
            Some(vec![[2.0, 3.0, 4.0, -1.0], [5.0, 6.0, 7.0, 1.0]])
        );
        for value in [
            serde_json::json!(null),
            serde_json::json!("0"),
            serde_json::json!(-1),
        ] {
            assert!(
                optional_attribute::<4>(
                    &d,
                    &[&b],
                    &serde_json::json!({"TANGENT":value}),
                    "TANGENT",
                    2
                )
                .is_err()
            );
        }
        assert!(optional_attribute::<4>(&d, &[&b[..39]], &attrs, "TANGENT", 2).is_err());
        assert!(optional_attribute::<4>(&d, &[&b], &attrs, "TANGENT", 3).is_err());
    }
    #[test]
    fn triangle_indices_preserve_order_and_refuse_missing_vertices() {
        let mut d = serde_json::json!({"accessors":[{"componentType":5123,"type":"SCALAR","count":3,"bufferView":0}],"bufferViews":[{"buffer":0}]});
        let b = [2u16, 0, 1]
            .into_iter()
            .flat_map(u16::to_le_bytes)
            .collect::<Vec<_>>();
        assert_eq!(indices(&d, &[&b], Some(0), 3).unwrap(), vec![2, 0, 1]);
        assert!(indices(&d, &[&b], Some(0), 2).is_err());
        assert!(indices(&d, &[&b[..5]], Some(0), 3).is_err());
        d["bufferViews"][0]["byteStride"] = 2.into();
        assert!(indices(&d, &[&b], Some(0), 3).is_err());
        assert_eq!(indices(&d, &[], None, 3).unwrap(), vec![0, 1, 2]);
        assert!(indices(&d, &[], None, 4).is_err());
    }
    #[test]
    fn only_absent_or_integer_triangle_mode_is_accepted() {
        assert!(triangle_mode(&serde_json::json!({})).is_ok());
        assert!(triangle_mode(&serde_json::json!({"mode":4})).is_ok());
        for mode in [
            serde_json::json!("4"),
            serde_json::json!(4.0),
            serde_json::json!(false),
            serde_json::json!(null),
            serde_json::json!(-1),
            serde_json::json!(5),
        ] {
            assert!(triangle_mode(&serde_json::json!({"mode":mode})).is_err());
        }
        assert!(triangle_mode(&serde_json::json!({"mode":4,"targets":[]})).is_err());
    }
    fn fixture() -> (Value, Vec<u8>) {
        let document = serde_json::json!({"accessors":[{"componentType":5126,"type":"VEC4","count":2,"bufferView":0,"byteOffset":4}],"bufferViews":[{"buffer":0,"byteOffset":0,"byteStride":20}]});
        let bytes = [99.0f32, 2.0, 3.0, 4.0, -1.0, 98.0, 5.0, 6.0, 7.0, 1.0]
            .into_iter()
            .flat_map(f32::to_le_bytes)
            .collect();
        (document, bytes)
    }
    #[test]
    fn additional_uvs_preserve_vec4_and_sparse_channel_presence() {
        let (d, b) = fixture();
        let attrs = serde_json::json!({"_OPENRPG_SOURCE_UV1":0,"_OPENRPG_SOURCE_UV7":0});
        let channels = additional_uvs::<4>(&d, &[&b], &attrs, "_OPENRPG_SOURCE_UV", 2).unwrap();
        let expected = vec![[2.0, 3.0, 4.0, -1.0], [5.0, 6.0, 7.0, 1.0]];
        assert_eq!(channels[0].as_ref(), Some(&expected));
        assert_eq!(channels[6].as_ref(), Some(&expected));
        assert!(channels[1..6].iter().all(Option::is_none));
        assert!(additional_uvs::<4>(&d, &[&b], &attrs, "_OPENRPG_SOURCE_UV", 3).is_err());
        assert!(additional_uvs::<2>(&d, &[&b], &attrs, "_OPENRPG_SOURCE_UV", 2).is_err());
        let absent = additional_uvs::<2>(&d, &[], &serde_json::json!({}), "TEXCOORD_", 2).unwrap();
        assert!(absent.iter().all(Option::is_none));
    }
    #[test]
    fn additional_uvs_refuse_nonfinite_and_unsupported_channels() {
        let (d, mut b) = fixture();
        b[4..8].copy_from_slice(&f32::INFINITY.to_le_bytes());
        assert!(
            additional_uvs::<4>(
                &d,
                &[&b],
                &serde_json::json!({"_OPENRPG_SOURCE_UV1":0}),
                "_OPENRPG_SOURCE_UV",
                2
            )
            .is_err()
        );
        for name in [
            "TEXCOORD_8",
            "TEXCOORD_01",
            "TEXCOORD_-1",
            "TEXCOORD_invalid",
        ] {
            let attrs = serde_json::json!({name:0});
            assert!(additional_uvs::<2>(&d, &[], &attrs, "TEXCOORD_", 2).is_err());
        }
    }
    #[test]
    fn uv_allocation_budget_includes_source_and_standard_channels() {
        let empty = serde_json::json!({});
        assert!(check_geometry_budget(&empty, 5_000_000, 0).is_ok());
        let additional = serde_json::json!({"TEXCOORD_1":0,"TEXCOORD_7":0,"_OPENRPG_SOURCE_UV1":0,"_OPENRPG_SOURCE_UV7":0});
        assert!(check_geometry_budget(&additional, 5_000_000, 0).is_err());
        assert!(check_geometry_budget(&empty, usize::MAX, 0).is_err());
        assert!(check_geometry_budget(&empty, 1, usize::MAX).is_err());
    }
    #[test]
    fn interleaved_original_values_are_not_normalized() {
        let (d, b) = fixture();
        assert_eq!(
            decode::<4>(&d, &[&b], 0, 2).unwrap(),
            vec![[2.0, 3.0, 4.0, -1.0], [5.0, 6.0, 7.0, 1.0]]
        );
    }
    #[test]
    fn malformed_counts_offsets_and_nonfinite_values_refuse() {
        let (d, b) = fixture();
        assert!(decode::<4>(&d, &[&b[..39]], 0, 2).is_err());
        assert!(decode::<4>(&d, &[&b], 0, 3).is_err());
        let mut bad = d.clone();
        bad["accessors"][0]["byteOffset"] = 1.into();
        assert!(decode::<4>(&bad, &[&b], 0, 2).is_err());
        let mut bad_bytes = b.clone();
        bad_bytes[4..8].copy_from_slice(&f32::NAN.to_le_bytes());
        assert!(decode::<4>(&d, &[&bad_bytes], 0, 2).is_err());
    }
    #[test]
    fn normalized_integer_sparse_and_incorrect_width_refuse() {
        let (d, b) = fixture();
        for (field, value) in [
            ("normalized", serde_json::json!(true)),
            ("componentType", serde_json::json!(5123)),
            ("sparse", serde_json::json!({})),
            ("type", serde_json::json!("VEC3")),
        ] {
            let mut bad = d.clone();
            bad["accessors"][0][field] = value;
            assert!(decode::<4>(&bad, &[&b], 0, 2).is_err());
        }
    }
}
