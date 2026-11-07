use super::{
    MediaFormat, MediaInfo, MediaLimits, MediaValidationError, bounded_add, error, number,
};
use crate::{governed_glb_json_length, validate_governed_glb_json};
use std::{borrow::Cow, collections::BTreeSet};
const FORMAT: MediaFormat = MediaFormat::Glb;

pub(super) fn decode(bytes: &[u8], limits: MediaLimits) -> Result<MediaInfo, MediaValidationError> {
    preflight(bytes)?;
    // The ordinary adapter still refuses a full source graph; it must never be
    // rendered through glTF fallback when the declared source program is needed.
    let source =
        crate::source_fragment_glb::prepare(bytes).map_err(|reason| error(FORMAT, reason))?;
    let (document, blob) = checked_document(&source.bytes, false)?;
    validate_document(&document, &blob, &source.source_pngs, limits)
}

pub(super) fn decode_source_graph(
    bytes: &[u8],
    limits: MediaLimits,
    carrier_budget: crate::source_graph_glb::SourceGraphGlbReadBudget,
) -> Result<MediaInfo, MediaValidationError> {
    preflight(bytes)?;
    let graph = crate::source_graph_glb::read_source_graph_glb_with_budget(bytes, carrier_budget)
        .map_err(|reason| error(FORMAT, reason))?
        .ok_or_else(|| error(FORMAT, "a complete declared source graph is required"))?;
    if has_source_fragment(graph.document()) {
        return Err(error(
            FORMAT,
            "mixed source-graph and source-fragment materials require a shared renderer",
        ));
    }
    let mut seen = BTreeSet::new();
    let mut pngs = Vec::new();
    for (_, contract) in &graph.materials {
        for texture in &contract.textures {
            if seen.insert(texture.png.buffer_view) {
                pngs.push(
                    *graph
                        .views
                        .get(texture.png.buffer_view)
                        .ok_or_else(|| error(FORMAT, "source graph image view absent"))?,
                );
            }
        }
    }
    let (document, blob) = checked_document(bytes, true)?;
    let info = validate_document(&document, &blob, &pngs, limits)?;
    let materials: BTreeSet<_> = graph.materials.iter().map(|(index, _)| *index).collect();
    let maximum = usize::try_from(limits.max_decoded_bytes).unwrap_or(usize::MAX);
    for mesh in document.meshes() {
        for primitive in mesh.primitives() {
            if primitive
                .material()
                .index()
                .is_some_and(|index| materials.contains(&index))
            {
                crate::source_graph_attributes::read_source_graph_primitive_geometry_with_budget(
                    &graph,
                    mesh.index(),
                    primitive.index(),
                    maximum,
                )
                .map_err(|reason| error(FORMAT, reason))?;
            }
        }
    }
    Ok(info)
}

fn has_source_fragment(document: &serde_json::Value) -> bool {
    let name = crate::source_fragment::SOURCE_FRAGMENT_EXTENSION;
    ["extensionsUsed", "extensionsRequired"]
        .iter()
        .any(|field| {
            document[*field]
                .as_array()
                .is_some_and(|entries| entries.iter().any(|entry| entry.as_str() == Some(name)))
        })
        || document["materials"].as_array().is_some_and(|materials| {
            materials
                .iter()
                .any(|material| material["extensions"].get(name).is_some())
        })
}

fn preflight(bytes: &[u8]) -> Result<(), MediaValidationError> {
    let header: &[u8; 20] = bytes
        .get(..20)
        .ok_or_else(|| error(FORMAT, "truncated GLB header"))?
        .try_into()
        .map_err(|_| error(FORMAT, "GLB header"))?;
    let length = governed_glb_json_length(header, number(bytes.len(), FORMAT)?)
        .map_err(|e| error(FORMAT, e.to_string()))?;
    validate_governed_glb_json(
        bytes
            .get(20..20 + length)
            .ok_or_else(|| error(FORMAT, "truncated GLB JSON"))?,
    )
    .map_err(|e| error(FORMAT, e.to_string()))
}

fn validate_document(
    document: &gltf::Document,
    blob: &[u8],
    source_pngs: &[&[u8]],
    limits: MediaLimits,
) -> Result<MediaInfo, MediaValidationError> {
    let mut buffers = 0;
    for buffer in document.buffers() {
        buffers += 1;
        if buffers > 1
            || !matches!(buffer.source(), gltf::buffer::Source::Bin)
            || buffer.length() > blob.len()
        {
            return Err(error(
                FORMAT,
                "GLB buffer is missing, external or outside its binary chunk",
            ));
        }
    }
    for view in document.views() {
        view_bytes(&view, blob)?;
        if view
            .offset()
            .checked_add(view.length())
            .is_none_or(|end| end > view.buffer().length())
        {
            return Err(error(FORMAT, "buffer view exceeds its declared buffer"));
        }
    }
    let decoded = validate_accessors(document, blob, limits)?;
    validate_nodes(document, limits)?;
    validate_rig_layout(document)?;
    let decoded = validate_images(document, blob, limits, decoded)?;
    validate_source_pngs(
        source_pngs,
        number(document.images().len(), FORMAT)?,
        limits,
        decoded,
    )?;
    validate_meshes(document, blob, limits)
}

fn checked_document(
    bytes: &[u8],
    admitted_graph: bool,
) -> Result<(gltf::Document, Cow<'_, [u8]>), MediaValidationError> {
    use gltf::json::validation::Validate;
    // Parse the same typed glTF document while borrowing the embedded BIN.
    // `Gltf::from_slice_without_validation` otherwise copies the whole BIN.
    let glb = gltf::binary::Glb::from_slice(bytes)
        .map_err(|e| error(FORMAT, format!("glTF structure: {e}")))?;
    let mut root: gltf::json::Root = gltf::json::deserialize::from_slice(&glb.json)
        .map_err(|e| error(FORMAT, format!("glTF structure: {e}")))?;
    // gltf-json 1.4's primitive validation hook indexes POSITION directly, even
    // after the index validator reports an error. Refuse that reference before
    // calling the hook; all other schema validation still runs afterwards.
    for mesh in &root.meshes {
        for primitive in &mesh.primitives {
            if let Some(index) = primitive
                .attributes
                .get(&gltf::json::validation::Checked::Valid(
                    gltf::json::mesh::Semantic::Positions,
                ))
                && root.accessors.get(index.value()).is_none()
            {
                return Err(error(FORMAT, "POSITION refers to a missing accessor"));
            }
        }
    }
    // gltf-json's Animation validator checks samplers but omits channel targets.
    // Validate their node references and Checked<Property> before typed getters.
    for (animation_index, animation) in root.animations.iter().enumerate() {
        for (channel_index, channel) in animation.channels.iter().enumerate() {
            let mut invalid = None;
            channel
                .target
                .validate(&root, gltf::json::Path::new, &mut |path, issue| {
                    invalid.get_or_insert_with(|| {
                        format!(
                            "animation target {animation_index}/{channel_index} {:?}: {issue:?}",
                            path()
                        )
                    });
                });
            if let Some(reason) = invalid {
                return Err(error(FORMAT, reason));
            }
        }
    }
    if admitted_graph {
        // Only after the original graph contract and pins were authenticated;
        // every other required extension remains subject to the typed validator.
        root.extensions_required
            .retain(|name| name != crate::source_graph_bundle::SOURCE_GRAPH_EXTENSION);
    }
    let document = gltf::Document::from_json(root)
        .map_err(|e| error(FORMAT, format!("glTF structure: {e}")))?;
    Ok((document, glb.bin.unwrap_or(Cow::Borrowed(&[]))))
}

fn primitive_layout(primitive: &gltf::Primitive<'_>) -> Result<(), MediaValidationError> {
    let position = primitive
        .get(&gltf::Semantic::Positions)
        .ok_or_else(|| error(FORMAT, "mesh primitive has no POSITION accessor"))?;
    if position.data_type() != gltf::accessor::DataType::F32
        || position.dimensions() != gltf::accessor::Dimensions::Vec3
        || position.normalized()
    {
        return Err(error(
            FORMAT,
            "POSITION must use unnormalized Float32 Vec3 data",
        ));
    }
    if let Some(index) = primitive.indices()
        && (!matches!(
            index.data_type(),
            gltf::accessor::DataType::U8
                | gltf::accessor::DataType::U16
                | gltf::accessor::DataType::U32
        ) || index.dimensions() != gltf::accessor::Dimensions::Scalar
            || index.normalized()
            || index.count() == 0)
    {
        return Err(error(
            FORMAT,
            "indices must use nonempty unnormalized unsigned scalar data",
        ));
    }
    for target in primitive.morph_targets() {
        if let Some(position) = target.positions() {
            float_layout(
                &position,
                gltf::accessor::Dimensions::Vec3,
                "morph POSITION",
            )?;
        }
    }
    Ok(())
}

fn float_layout(
    accessor: &gltf::Accessor<'_>,
    dimensions: gltf::accessor::Dimensions,
    usage: &str,
) -> Result<(), MediaValidationError> {
    if accessor.data_type() != gltf::accessor::DataType::F32
        || accessor.dimensions() != dimensions
        || accessor.normalized()
    {
        return Err(error(
            FORMAT,
            format!(
                "{usage} accessor {} must use unnormalized Float32 {dimensions:?}",
                accessor.index()
            ),
        ));
    }
    Ok(())
}

fn validate_rig_layout(document: &gltf::Document) -> Result<(), MediaValidationError> {
    use gltf::accessor::{DataType, Dimensions};
    for animation in document.animations() {
        for sampler in animation.samplers() {
            float_layout(&sampler.input(), Dimensions::Scalar, "animation input")?;
        }
        for channel in animation.channels() {
            if channel.target().property() != gltf::animation::Property::Rotation {
                continue;
            }
            let output = channel.sampler().output();
            // Match the reader's supported component variants; U32 reaches its
            // unreachable branch. Integer rotations use normalized conversion.
            let supported = match output.data_type() {
                DataType::F32 => !output.normalized(),
                DataType::I8 | DataType::U8 | DataType::I16 | DataType::U16 => output.normalized(),
                DataType::U32 => false,
            };
            if output.dimensions() != Dimensions::Vec4 || !supported {
                return Err(error(
                    FORMAT,
                    format!(
                        "animation rotation accessor {} requires Float32 Vec4 or normalized 8/16-bit Vec4",
                        output.index()
                    ),
                ));
            }
        }
    }
    for skin in document.skins() {
        if let Some(matrices) = skin.inverse_bind_matrices() {
            float_layout(&matrices, Dimensions::Mat4, "inverseBindMatrices")?;
        }
    }
    Ok(())
}

fn validate_images(
    document: &gltf::Document,
    blob: &[u8],
    limits: MediaLimits,
    mut decoded: u64,
) -> Result<u64, MediaValidationError> {
    if number(document.images().len(), FORMAT)? > limits.max_mesh_primitives {
        return Err(error(FORMAT, "embedded image count budget exceeded"));
    }
    for image in document.images() {
        let gltf::image::Source::View { view, mime_type } = image.source() else {
            return Err(error(FORMAT, "external GLB image refused"));
        };
        let encoded = view_bytes(&view, blob)?;
        let format = match mime_type {
            "image/png" => MediaFormat::Png,
            "image/jpeg" => MediaFormat::Jpeg,
            _ => return Err(error(FORMAT, "unsupported embedded GLB image format")),
        };
        let remaining = limits
            .max_decoded_bytes
            .checked_sub(decoded)
            .ok_or_else(|| error(FORMAT, "GLB decoded byte budget exceeded"))?;
        let image = super::images::decode(
            encoded,
            format,
            MediaLimits {
                max_decoded_bytes: remaining,
                ..limits
            },
        )?;
        if let MediaInfo::Image { decoded_bytes, .. } = image {
            decoded = bounded_add(
                decoded,
                decoded_bytes,
                limits.max_decoded_bytes,
                FORMAT,
                "GLB decoded bytes",
            )?;
        }
    }
    Ok(decoded)
}

fn validate_source_pngs(
    images: &[&[u8]],
    existing_images: u64,
    limits: MediaLimits,
    mut decoded: u64,
) -> Result<(), MediaValidationError> {
    bounded_add(
        existing_images,
        number(images.len(), FORMAT)?,
        limits.max_mesh_primitives,
        FORMAT,
        "GLB image count",
    )?;
    for bytes in images {
        let remaining = limits
            .max_decoded_bytes
            .checked_sub(decoded)
            .ok_or_else(|| error(FORMAT, "GLB decoded byte budget exceeded"))?;
        let image = super::images::decode(
            bytes,
            MediaFormat::Png,
            MediaLimits {
                max_decoded_bytes: remaining,
                ..limits
            },
        )?;
        if let MediaInfo::Image { decoded_bytes, .. } = image {
            decoded = bounded_add(
                decoded,
                decoded_bytes,
                limits.max_decoded_bytes,
                FORMAT,
                "GLB decoded bytes",
            )?;
        }
    }
    Ok(())
}

fn validate_meshes(
    document: &gltf::Document,
    blob: &[u8],
    limits: MediaLimits,
) -> Result<MediaInfo, MediaValidationError> {
    let mut meshes = 0_u64;
    let mut primitives = 0_u64;
    let mut vertices = 0_u64;
    let mut indices = 0_u64;
    for mesh in document.meshes() {
        meshes = bounded_add(meshes, 1, limits.max_mesh_primitives, FORMAT, "mesh")?;
        for primitive in mesh.primitives() {
            primitive_layout(&primitive)?;
            primitives = bounded_add(
                primitives,
                1,
                limits.max_mesh_primitives,
                FORMAT,
                "primitive",
            )?;
            let accessor = primitive
                .get(&gltf::Semantic::Positions)
                .ok_or_else(|| error(FORMAT, "mesh primitive has no POSITION accessor"))?;
            let count = number(accessor.count(), FORMAT)?;
            vertices = bounded_add(vertices, count, limits.max_mesh_vertices, FORMAT, "vertex")?;
            let reader = primitive.reader(|buffer| {
                if buffer.index() == 0 {
                    Some(blob)
                } else {
                    None
                }
            });
            let positions = reader
                .read_positions()
                .ok_or_else(|| error(FORMAT, "invalid POSITION data"))?;
            if number(positions.count(), FORMAT)? != count {
                return Err(error(
                    FORMAT,
                    "POSITION cardinality differs from its accessor",
                ));
            }
            let mut primitive_indices = 0_u64;
            if let Some(values) = reader.read_indices() {
                for index in values.into_u32() {
                    if u64::from(index) >= count {
                        return Err(error(FORMAT, "mesh index exceeds vertex count"));
                    }
                    primitive_indices = bounded_add(
                        primitive_indices,
                        1,
                        limits.max_mesh_indices,
                        FORMAT,
                        "index",
                    )?;
                }
            }
            indices = bounded_add(
                indices,
                primitive_indices,
                limits.max_mesh_indices,
                FORMAT,
                "index",
            )?;
            topology(
                primitive.mode(),
                if primitive_indices > 0 {
                    primitive_indices
                } else {
                    count
                },
            )?;
        }
    }
    Ok(MediaInfo::Glb {
        meshes,
        primitives,
        vertices,
        indices,
        has_geometry: primitives > 0 && vertices > 0,
    })
}

fn view_bytes<'a>(
    view: &gltf::buffer::View<'_>,
    blob: &'a [u8],
) -> Result<&'a [u8], MediaValidationError> {
    if view.buffer().index() != 0 {
        return Err(error(FORMAT, "GLB view refers to another buffer"));
    }
    let end = view
        .offset()
        .checked_add(view.length())
        .ok_or_else(|| error(FORMAT, "buffer view range overflow"))?;
    blob.get(view.offset()..end)
        .ok_or_else(|| error(FORMAT, "buffer view outside binary chunk"))
}
fn region(
    data: &[u8],
    offset: usize,
    count: usize,
    stride: usize,
    size: usize,
) -> Result<&[u8], MediaValidationError> {
    let length = if count == 0 {
        0
    } else {
        count
            .checked_sub(1)
            .and_then(|n| n.checked_mul(stride))
            .and_then(|n| n.checked_add(size))
            .ok_or_else(|| error(FORMAT, "accessor range overflow"))?
    };
    let end = offset
        .checked_add(length)
        .ok_or_else(|| error(FORMAT, "accessor offset overflow"))?;
    data.get(offset..end)
        .ok_or_else(|| error(FORMAT, "accessor exceeds its buffer view"))
}

fn validate_accessors(
    document: &gltf::Document,
    blob: &[u8],
    limits: MediaLimits,
) -> Result<u64, MediaValidationError> {
    let mut decoded = 0_u64;
    for accessor in document.accessors() {
        if accessor.count() == 0 {
            return Err(error(
                FORMAT,
                format!("accessor {} has a zero element count", accessor.index()),
            ));
        }
        let size = item_size(&accessor);
        let bytes = number(accessor.count(), FORMAT)?
            .checked_mul(number(size, FORMAT)?)
            .ok_or_else(|| error(FORMAT, "accessor byte budget overflow"))?;
        decoded = bounded_add(
            decoded,
            bytes,
            limits.max_decoded_bytes,
            FORMAT,
            "accessor byte",
        )?;
        if let Some(view) = accessor.view() {
            let stride = view.stride().unwrap_or(size);
            if stride < size || accessor.offset() % accessor.data_type().size() != 0 {
                return Err(error(FORMAT, "invalid accessor stride/alignment"));
            }
            let data = region(
                view_bytes(&view, blob)?,
                accessor.offset(),
                accessor.count(),
                stride,
                size,
            )?;
            finite(&accessor, data, stride, size)?;
        } else if accessor.sparse().is_none() {
            return Err(error(
                FORMAT,
                "accessor has neither a buffer view nor sparse data",
            ));
        }
        if let Some(sparse) = accessor.sparse() {
            if sparse.count() == 0 {
                return Err(error(
                    FORMAT,
                    format!("accessor {} has a zero sparse count", accessor.index()),
                ));
            }
            if sparse.count() > accessor.count() {
                return Err(error(FORMAT, "sparse accessor count exceeds its parent"));
            }
            let index = sparse.indices();
            let width = index.index_type().size();
            let view = index.view();
            let data = region(
                view_bytes(&view, blob)?,
                index.offset(),
                sparse.count(),
                width,
                width,
            )?;
            let mut previous = None;
            for bytes in data.chunks_exact(width) {
                let index = match width {
                    1 => u32::from(bytes[0]),
                    2 => u32::from(u16::from_le_bytes(
                        bytes.try_into().map_err(|_| error(FORMAT, "sparse u16"))?,
                    )),
                    4 => u32::from_le_bytes(
                        bytes.try_into().map_err(|_| error(FORMAT, "sparse u32"))?,
                    ),
                    _ => return Err(error(FORMAT, "unsupported sparse index size")),
                };
                if usize::try_from(index)
                    .ok()
                    .is_none_or(|i| i >= accessor.count())
                    || previous.is_some_and(|p| p >= index)
                {
                    return Err(error(FORMAT, "invalid or unordered sparse accessor index"));
                }
                previous = Some(index);
            }
            let values = sparse.values();
            let view = values.view();
            let data = region(
                view_bytes(&view, blob)?,
                values.offset(),
                sparse.count(),
                size,
                size,
            )?;
            finite(&accessor, data, size, size)?;
        }
    }
    Ok(decoded)
}

fn item_size(accessor: &gltf::Accessor<'_>) -> usize {
    let component = accessor.data_type().size();
    let (columns, rows) = match accessor.dimensions() {
        gltf::accessor::Dimensions::Mat2 => (2, 2),
        gltf::accessor::Dimensions::Mat3 => (3, 3),
        gltf::accessor::Dimensions::Mat4 => (4, 4),
        _ => return accessor.size(),
    };
    let column = rows * component;
    columns * column.next_multiple_of(4)
}
fn finite(
    accessor: &gltf::Accessor<'_>,
    data: &[u8],
    stride: usize,
    size: usize,
) -> Result<(), MediaValidationError> {
    if accessor.data_type() != gltf::accessor::DataType::F32 {
        return Ok(());
    }
    for element in data.chunks(stride) {
        for bytes in element
            .get(..size)
            .ok_or_else(|| error(FORMAT, "truncated float accessor"))?
            .as_chunks::<4>()
            .0
        {
            let value = f32::from_le_bytes(*bytes);
            if !value.is_finite() {
                return Err(error(FORMAT, "nonfinite accessor component"));
            }
        }
    }
    Ok(())
}

fn topology(mode: gltf::mesh::Mode, count: u64) -> Result<(), MediaValidationError> {
    let valid = match mode {
        gltf::mesh::Mode::Triangles => count >= 3 && count.is_multiple_of(3),
        gltf::mesh::Mode::Lines => count >= 2 && count.is_multiple_of(2),
        gltf::mesh::Mode::LineLoop | gltf::mesh::Mode::LineStrip => count >= 2,
        gltf::mesh::Mode::TriangleFan | gltf::mesh::Mode::TriangleStrip => count >= 3,
        gltf::mesh::Mode::Points => count > 0,
    };
    if !valid {
        return Err(error(FORMAT, "primitive topology/cardinality mismatch"));
    }
    Ok(())
}

fn validate_nodes(
    document: &gltf::Document,
    limits: MediaLimits,
) -> Result<(), MediaValidationError> {
    let nodes: Vec<_> = document.nodes().collect();
    if number(nodes.len(), FORMAT)? > limits.max_mesh_primitives {
        return Err(error(FORMAT, "node budget exceeded"));
    }
    let mut parents = vec![None; nodes.len()];
    for node in &nodes {
        if node
            .transform()
            .matrix()
            .iter()
            .flatten()
            .any(|n| !n.is_finite())
        {
            return Err(error(FORMAT, "nonfinite node transform"));
        }
        for child in node.children() {
            if parents[child.index()].replace(node.index()).is_some() {
                return Err(error(FORMAT, "node has multiple parents"));
            }
        }
    }
    let mut complete = vec![false; nodes.len()];
    for node in &nodes {
        let mut chain = std::collections::BTreeSet::new();
        let mut cursor = Some(node.index());
        while let Some(index) = cursor {
            if complete[index] {
                break;
            }
            if !chain.insert(index) {
                return Err(error(FORMAT, "cyclic node hierarchy"));
            }
            cursor = parents[index];
        }
        for index in chain {
            complete[index] = true;
        }
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn typed_validation_borrows_the_original_binary_chunk() {
        let mut json = br#"{"asset":{"version":"2.0"},"buffers":[{"byteLength":4}]}"#.to_vec();
        json.resize(json.len().next_multiple_of(4), b' ');
        let mut bytes = b"glTF".to_vec();
        bytes.extend(2_u32.to_le_bytes());
        bytes.extend(u32::try_from(28 + json.len() + 4).unwrap().to_le_bytes());
        bytes.extend(u32::try_from(json.len()).unwrap().to_le_bytes());
        bytes.extend(b"JSON");
        bytes.extend(json);
        bytes.extend(4_u32.to_le_bytes());
        bytes.extend(b"BIN\0");
        bytes.extend([1, 2, 3, 4]);
        let (_, blob) = checked_document(&bytes, false).unwrap();
        assert!(matches!(blob, Cow::Borrowed([1, 2, 3, 4])));
    }
}
