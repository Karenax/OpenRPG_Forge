// SPDX-License-Identifier: MIT
// Transferred from forge_tidewater; source attribution is in third_party/tidewater/LICENSE.
//! Source fin-attribute packing and admitted masked shader assembly.
use super::{geometry::FISH_DATA, surface::extract_function};
use forge_assets::source_fish_payload::SourceFishPayload;

#[cfg(feature = "physics-rapier")]
pub mod cache;
/// Pack already posed geometry; retain the original fin classifier at every vertex.
///
/// # Errors
/// Rejects missing attributes, non-finite values, empty/non-triangle geometry,
/// mismatched vertex counts and indices outside the vertex stream.
pub fn masked_mesh(
    mesh: &bevy::prelude::Mesh,
) -> Result<crate::source_shadows::casters::masked::MaskedMesh, String> {
    use bevy::mesh::{Indices, VertexAttributeValues};
    let Some(VertexAttributeValues::Float32x3(positions)) =
        mesh.attribute(bevy::prelude::Mesh::ATTRIBUTE_POSITION)
    else {
        return Err("Fish shadow positions absent".into());
    };
    let Some(VertexAttributeValues::Float32x4(data)) = mesh.attribute(FISH_DATA) else {
        return Err("Fish shadow fin data absent".into());
    };
    if mesh.primitive_topology() != bevy::render::render_resource::PrimitiveTopology::TriangleList
        || positions.is_empty()
        || positions.len() != data.len()
        || positions.len() > 65536
        || positions
            .iter()
            .flatten()
            .chain(data.iter().flatten())
            .any(|value| !value.is_finite())
    {
        return Err("Invalid fish shadow vertex stream".into());
    }
    let indices: Vec<u32> = match mesh.indices() {
        Some(Indices::U16(values)) => values.iter().map(|value| u32::from(*value)).collect(),
        Some(Indices::U32(values)) => values.clone(),
        None => return Err("Fish shadow indices absent".into()),
    };
    if indices.is_empty()
        || !indices.len().is_multiple_of(3)
        || indices
            .iter()
            .any(|index| *index as usize >= positions.len())
    {
        return Err("Invalid fish shadow triangles".into());
    }
    Ok(crate::source_shadows::casters::masked::MaskedMesh {
        vertices: positions
            .iter()
            .zip(data)
            .flat_map(|(position, fish)| {
                position
                    .iter()
                    .chain(fish)
                    .flat_map(|value| value.to_le_bytes())
            })
            .collect(),
        indices: indices
            .iter()
            .flat_map(|index| index.to_le_bytes())
            .collect(),
    })
}

/// Assemble depth entries without replacing translucent fin coverage by opaque geometry.
/// The vertex stream is position (float3) followed by original `FISH_DATA` (float4).
/// The uniform contains two matrices followed by time and three scalar padding values.
///
/// # Errors
/// Rejects missing or non-UTF8 verified payloads and missing source helpers.
pub fn shader_source(package: &impl SourceFishPayload) -> Result<String, String> {
    let text = |name| {
        let bytes = package
            .file(name)
            .ok_or_else(|| format!("Missing fish shadow payload: {name}"))?;
        std::str::from_utf8(bytes).map_err(|error| error.to_string())
    };
    let noise = extract_function(
        text("material/module-0-common.wgsl")?,
        "interleavedGradientNoise",
    )?;
    let part = extract_function(text("material/module-2-fish.wgsl")?, "fishPartOf")?;
    let body = text("material/main-shadow.wgsl")?;
    Ok(format!(
        "{noise}\n{part}\n\
struct Frame {{clip_from_world:mat4x4f,world_from_local:mat4x4f,time:f32,pad0:f32,pad1:f32,pad2:f32}};\n\
@group(0) @binding(0) var<uniform> frame:Frame;\n\
struct ShadowVertexInput {{@location(0) position:vec3f,@location(1) data:vec4f}};\n\
struct ShadowVertexOutput {{@builtin(position) position:vec4f,@location(0) data:vec4f}};\n\
struct FishShadowVaryings {{vFishData:vec4f}};\n\
struct FishShadowInput {{vs:FishShadowVaryings,pixel:vec2f}};\n\
fn fishShadowMask(in:FishShadowInput)->bool {{\n{body}\n}}\n\
@vertex fn vertex(input:ShadowVertexInput)->ShadowVertexOutput {{\n\
var output:ShadowVertexOutput;\n\
output.position=frame.clip_from_world*frame.world_from_local*vec4f(input.position,1.0);\n\
output.data=input.data;return output;\n}}\n\
@fragment fn fragment(input:ShadowVertexOutput) {{\n\
let accepted=fishShadowMask(FishShadowInput(FishShadowVaryings(input.data),input.position.xy));\n\
if(!accepted){{discard;}}\n}}\n"
    ))
}
