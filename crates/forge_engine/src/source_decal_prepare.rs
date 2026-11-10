//! Typed preparation of integrity-checked source decal carriers for a future
//! scene/render mount. This stage copies validated payloads into explicit
//! program and texture records; it does not compile shaders or create GPU
//! resources.
use bevy::math::Mat4;
use forge_assets::source_decal_glb::SourceDecalGlb;
use serde_json::Value;

#[derive(Clone, Debug)]
pub struct SourceDecalPrograms {
    pub surface: String,
    pub dbuffer_generation: String,
    pub color_composition: String,
    pub normal_composition: String,
}

#[derive(Clone, Debug, Eq, PartialEq)]
pub struct SourceDecalMip {
    pub width: u32,
    pub height: u32,
    pub offset: usize,
    pub bytes: usize,
}

#[derive(Clone, Debug)]
pub struct SourceDecalTexture {
    /// The carrier reader currently admits only this captured storage format.
    pub format: SourceDecalTextureFormat,
    pub width: u32,
    pub height: u32,
    /// Exact BC3 bytes pinned by the carrier's whole-storage and per-mip hashes.
    pub storage: Vec<u8>,
    pub mips: Vec<SourceDecalMip>,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum SourceDecalTextureFormat {
    Bc3RgbaUnormSrgb,
}

#[derive(Clone, Debug)]
pub struct PreparedSourceDecal {
    pub material_index: usize,
    pub programs: SourceDecalPrograms,
    pub texture: SourceDecalTexture,
}

/// Mesh vertices stay in carrier-local coordinates. A scene consumer must
/// apply both the placement and the node matrix before projecting them.
pub struct SourceDecalDraw {
    pub material_index: usize,
    pub root_from_mesh: Mat4,
    pub positions: Vec<[f32; 3]>,
    pub normals: Vec<[f32; 3]>,
    pub uv: Vec<[f32; 2]>,
    pub indices: Vec<u32>,
}

fn payload<'a>(reference: &Value, views: &[&'a [u8]], field: &str) -> Result<&'a [u8], String> {
    let index = reference
        .get("buffer_view")
        .and_then(Value::as_u64)
        .and_then(|value| usize::try_from(value).ok())
        .ok_or_else(|| format!("invalid {field} buffer_view"))?;
    views
        .get(index)
        .copied()
        .ok_or_else(|| format!("{field} buffer_view out of range"))
}

fn wgsl(reference: &Value, views: &[&[u8]], field: &str) -> Result<String, String> {
    let bytes = payload(reference, views, field)?;
    let source = std::str::from_utf8(bytes).map_err(|_| format!("{field} is not UTF-8"))?;
    if source.is_empty() || source.contains('\0') {
        return Err(format!("{field} is empty or contains NUL"));
    }
    Ok(source.to_owned())
}

fn number(value: &Value, key: &str) -> Result<usize, String> {
    value
        .get(key)
        .and_then(Value::as_u64)
        .and_then(|number| usize::try_from(number).ok())
        .ok_or_else(|| format!("invalid decal texture {key}"))
}

fn prepare_material(
    material_index: usize,
    contract: &Value,
    views: &[&[u8]],
) -> Result<PreparedSourceDecal, String> {
    // These programs were verified with this exact raster and blend state.
    // Preparing them for another state would invalidate the reference.
    if contract["blend"].as_str() != Some("urp_mesh_dbuffer_reference")
        || contract["dbuffer_reference"]["state"]
            != serde_json::json!({
                "depth_compare": "reversed_z_greater_equal",
                "depth_write": false,
                "cull": "back",
                "targets": ["rgba8unorm", "rgba8unorm"],
                "color_blend": "src_alpha_one_minus_src_alpha",
                "alpha_blend": "zero_one_minus_src_alpha",
            })
    {
        return Err("unsupported decal reference raster or blend state".into());
    }
    let surface = wgsl(&contract["surface_wgsl"], views, "surface_wgsl")?;
    let texture = contract
        .get("textures")
        .and_then(Value::as_array)
        .and_then(|textures| textures.first())
        .ok_or("missing decal texture")?;
    if texture
        .get("imported_storage")
        .and_then(|storage| storage.get("format"))
        .and_then(Value::as_str)
        != Some("bc3_rgba_unorm_srgb")
    {
        return Err("unsupported decal texture format".into());
    }
    let imported = &texture["imported_storage"];
    let width = u32::try_from(number(imported, "width")?).map_err(|_| "decal width overflow")?;
    let height = u32::try_from(number(imported, "height")?).map_err(|_| "decal height overflow")?;
    let storage = payload(&imported["storage"], views, "texture storage")?.to_vec();
    let mip_values = imported
        .get("mips")
        .and_then(Value::as_array)
        .ok_or("missing decal mips")?;
    let mips = mip_values
        .iter()
        .map(|mip| {
            Ok(SourceDecalMip {
                width: u32::try_from(number(mip, "width")?)
                    .map_err(|_| "decal mip width overflow")?,
                height: u32::try_from(number(mip, "height")?)
                    .map_err(|_| "decal mip height overflow")?,
                offset: number(mip, "offset")?,
                bytes: number(mip, "bytes")?,
            })
        })
        .collect::<Result<Vec<_>, String>>()?;

    let programs = contract
        .get("dbuffer_reference")
        .and_then(|reference| reference.get("programs"))
        .ok_or("missing D-buffer programs")?;
    Ok(PreparedSourceDecal {
        material_index,
        programs: SourceDecalPrograms {
            surface,
            dbuffer_generation: wgsl(&programs["generation"], views, "generation")?,
            color_composition: wgsl(&programs["color_composition"], views, "color_composition")?,
            normal_composition: wgsl(&programs["normal_composition"], views, "normal_composition")?,
        },
        texture: SourceDecalTexture {
            format: SourceDecalTextureFormat::Bc3RgbaUnormSrgb,
            width,
            height,
            storage,
            mips,
        },
    })
}

/// Convert the carrier reader's integrity-checked declarations to typed,
/// owned preparation records. Resource bytes are copied so records can outlive
/// the borrowed GLB while a scene mount is assembled.
pub fn prepare_source_decals(
    carrier: &SourceDecalGlb<'_>,
) -> Result<Vec<PreparedSourceDecal>, String> {
    carrier
        .materials
        .iter()
        // The carrier reader returns the validated extension payload, not
        // the enclosing glTF material.
        .map(|(index, contract)| prepare_material(*index, contract, &carrier.views))
        .collect()
}

fn stream<'a>(
    carrier: &'a SourceDecalGlb<'_>,
    index: usize,
    shape: &str,
    component: usize,
    element_bytes: usize,
) -> Result<&'a [u8], String> {
    let doc = carrier.document();
    let accessor = doc["accessors"]
        .get(index)
        .ok_or("Decal accessor out of range")?;
    let view = number(accessor, "bufferView")?;
    let count = number(accessor, "count")?;
    let len = count
        .checked_mul(element_bytes)
        .ok_or("Decal accessor length overflow")?;
    // The captured carrier uses finite, tightly packed float data and u32
    // indices. Do not reinterpret sparse or interleaved attributes.
    if accessor["type"].as_str() != Some(shape)
        || number(accessor, "componentType")? != component
        || accessor
            .get("normalized")
            .is_some_and(|x| x.as_bool() != Some(false))
        || accessor.get("sparse").is_some()
        || doc["bufferViews"][view].get("byteStride").is_some()
        || len == 0
        || count > 1_000_000
    {
        return Err("Unsupported decal accessor layout".into());
    }
    let offset = match accessor.get("byteOffset") {
        None => 0,
        Some(_) => number(accessor, "byteOffset")?,
    };
    let end = offset
        .checked_add(len)
        .ok_or("Decal accessor offset overflow")?;
    carrier
        .views
        .get(view)
        .and_then(|bytes| bytes.get(offset..end))
        .ok_or_else(|| "Decal accessor outside its buffer view".into())
}

fn floats<const N: usize>(bytes: &[u8]) -> Result<Vec<[f32; N]>, String> {
    bytes
        .chunks_exact(N * 4)
        .map(|vertex| {
            let mut values = [0.0; N];
            for (value, word) in values.iter_mut().zip(vertex.as_chunks::<4>().0) {
                *value = f32::from_le_bytes(*word);
                if !value.is_finite() {
                    return Err("Nonfinite decal vertex attribute".into());
                }
            }
            Ok(values)
        })
        .collect()
}

/// Prepare the flat, explicitly selected mesh-decal carrier accepted by the
/// URP reference pass. This performs no draw or receiver substitution.
pub fn prepare_source_decal_draws(
    carrier: &SourceDecalGlb<'_>,
) -> Result<Vec<SourceDecalDraw>, String> {
    let doc = carrier.document();
    if doc["scenes"].as_array().map(Vec::len) != Some(1)
        || doc["scenes"][0]["nodes"] != serde_json::json!([0])
        || doc["nodes"].as_array().map(Vec::len) != Some(1)
        || doc["meshes"].as_array().map(Vec::len) != Some(1)
        || number(&doc["nodes"][0], "mesh")? != 0
        || ["children", "skin", "rotation", "translation", "scale"]
            .iter()
            .any(|key| doc["nodes"][0].get(key).is_some())
    {
        return Err("Decal reference requires a single flat scene mesh".into());
    }
    let matrix: [f32; 16] = serde_json::from_value(doc["nodes"][0]["matrix"].clone())
        .map_err(|_| "Decal node matrix absent or malformed")?;
    let root_from_mesh = Mat4::from_cols_array(&matrix);
    let determinant = root_from_mesh.determinant();
    if !root_from_mesh.is_finite()
        || !determinant.is_finite()
        || determinant == 0.0
        || [matrix[3], matrix[7], matrix[11], matrix[15]] != [0., 0., 0., 1.]
    {
        return Err("Invalid affine decal node matrix".into());
    }
    let primitives = doc["meshes"][0]["primitives"]
        .as_array()
        .filter(|items| items.len() == 1)
        .ok_or("Decal reference requires one primitive")?;
    primitives
        .iter()
        .map(|primitive| {
            if number(primitive, "mode")? != 4 {
                return Err("Decal reference requires indexed triangles".into());
            }
            let material_index = number(primitive, "material")?;
            if !carrier
                .materials
                .iter()
                .any(|(index, _)| *index == material_index)
            {
                return Err("Decal primitive lacks an admitted decal material".into());
            }
            let attr = &primitive["attributes"];
            let positions = floats::<3>(stream(
                carrier,
                number(attr, "POSITION")?,
                "VEC3",
                5126,
                12,
            )?)?;
            let normals = floats::<3>(stream(carrier, number(attr, "NORMAL")?, "VEC3", 5126, 12)?)?;
            let uv = floats::<2>(stream(
                carrier,
                number(attr, "TEXCOORD_0")?,
                "VEC2",
                5126,
                8,
            )?)?;
            let indices: Vec<_> =
                stream(carrier, number(primitive, "indices")?, "SCALAR", 5125, 4)?
                    .as_chunks::<4>()
                    .0
                    .iter()
                    .copied()
                    .map(u32::from_le_bytes)
                    .collect();
            if positions.len() != normals.len()
                || positions.len() != uv.len()
                || !indices.len().is_multiple_of(3)
                || indices
                    .iter()
                    .any(|&index| index as usize >= positions.len())
            {
                return Err("Invalid decal triangle or attribute count".into());
            }
            Ok(SourceDecalDraw {
                material_index,
                root_from_mesh,
                positions,
                normals,
                uv,
                indices,
            })
        })
        .collect()
}
