//! Source fin coverage for the dedicated depth pass on already posed fish meshes.
use super::surface::extract_function;
use forge_assets::tidewater_fish_props::FishPropPackage;
#[path = "shadow/gpu.rs"]
pub mod gpu;

/// Ephemeral depth input for the current shared cascade target, never persisted.
#[derive(bevy::prelude::Resource, Clone, bevy::render::extract_resource::ExtractResource)]
pub struct Frame {
    pub target: bevy::prelude::Handle<bevy::prelude::Image>,
    pub shader: std::sync::Arc<String>,
    pub meshes: std::sync::Arc<Vec<MaskedMesh>>,
    pub poses: Vec<gpu::Pose>,
    pub time: f32,
}

/// Interleaved position/data stream for the masked depth entries.
#[derive(Clone)]
pub struct MaskedMesh {
    pub vertices: Vec<u8>,
    pub indices: Vec<u8>,
}

impl MaskedMesh {
    /// Pack already posed geometry; retain the original fin classifier at every vertex.
    ///
    /// # Errors
    /// Rejects missing attributes, non-finite values, empty/non-triangle geometry,
    /// mismatched vertex counts and indices outside the vertex stream.
    pub fn from_mesh(mesh: &bevy::prelude::Mesh) -> Result<Self, String> {
        use bevy::mesh::{Indices, VertexAttributeValues};
        let Some(VertexAttributeValues::Float32x3(positions)) =
            mesh.attribute(bevy::prelude::Mesh::ATTRIBUTE_POSITION)
        else {
            return Err("Fish shadow positions absent".into());
        };
        let Some(VertexAttributeValues::Float32x4(data)) = mesh.attribute(super::FISH_DATA) else {
            return Err("Fish shadow fin data absent".into());
        };
        if mesh.primitive_topology()
            != bevy::render::render_resource::PrimitiveTopology::TriangleList
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
        Ok(Self {
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
}

/// Assemble depth entries without replacing translucent fin coverage by opaque geometry.
/// The vertex stream is position (float3) followed by original `FISH_DATA` (float4).
/// The uniform contains two matrices followed by time and three scalar padding values.
///
/// # Errors
/// Rejects missing or non-UTF8 verified payloads and missing source helpers.
pub fn shader_source(package: &FishPropPackage) -> Result<String, String> {
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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn depth_entries_validate_and_retain_exact_source_mask() {
        let root =
            std::path::PathBuf::from(std::env::var("FORGE_TIDEWATER_FISH_PROP_PACKAGE").unwrap());
        let package = FishPropPackage::load(
            &std::fs::read(root.join("package.json")).unwrap(),
            forge_assets::tidewater_fish_props::package_hash(),
            |name, _| std::fs::read(root.join(name)).map_err(|error| error.to_string()),
        )
        .unwrap();
        let shader = shader_source(&package).unwrap();
        let mask = std::str::from_utf8(package.file("material/main-shadow.wgsl").unwrap()).unwrap();
        assert!(shader.contains(mask));
        assert!(!shader.contains("lodFade"));
        let module = naga::front::wgsl::parse_str(&shader).unwrap();
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::all(),
        )
        .validate(&module)
        .unwrap();
        assert_eq!(module.entry_points.len(), 2);
        let prepared = super::super::PreparedFishProps::from_package(&package).unwrap();
        assert_eq!(prepared.templates.len(), 78);
        for template in prepared.templates {
            let mut mesh = template.lod_meshes.into_iter().nth(2).unwrap();
            let packed = MaskedMesh::from_mesh(&mesh).unwrap();
            assert_eq!(packed.vertices.len(), mesh.count_vertices() * 28);
            let bevy::mesh::VertexAttributeValues::Float32x4(data) =
                mesh.attribute(super::super::FISH_DATA).unwrap()
            else {
                panic!("source fin data changed")
            };
            for (vertex, fish) in packed.vertices.as_chunks::<28>().0.iter().zip(data) {
                assert_eq!(
                    &vertex[12..],
                    &fish
                        .iter()
                        .flat_map(|value| value.to_le_bytes())
                        .collect::<Vec<_>>()
                );
            }
            mesh.remove_attribute(super::super::FISH_DATA);
            assert!(MaskedMesh::from_mesh(&mesh).is_err());
        }
        if let Ok(path) = std::env::var("FORGE_FISH_SHADOW_SHADER_OUTPUT") {
            std::fs::write(path, shader).unwrap();
        }
    }
}
