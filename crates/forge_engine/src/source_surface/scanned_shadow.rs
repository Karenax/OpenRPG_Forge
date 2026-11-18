//! Depth shaders and ephemeral scanned casters for the shared cascade graph.
use super::{scanned_draws, scanned_meshes::PreparedMeshes};
use crate::source_shadows::casters::{
    graph::Target,
    textured::{Frame, MeshBytes, Pose},
};
use bevy::{
    mesh::{Indices, VertexAttributeValues},
    prelude::*,
};
use forge_assets::source_scanned_package::ScannedPackage;
use forge_core::AssetId;
use forge_project::zone_authoring::placements::ManualPlacement;
use std::{collections::BTreeMap, sync::Arc};

/// Build source depth coverage without making transitioning casters opaque.
/// Uniform layout: two matrices, iData vec4, frameIndex and three u32 pads.
/// The scene must submit only near-LOD prototypes with their matching asset ordinal.
///
/// # Errors
/// Refuses missing/non-UTF8 acquired modules or drifted source coverage seams.
pub fn shader_source(package: &ScannedPackage) -> Result<String, String> {
    let text = |name| {
        std::str::from_utf8(package.file(name).ok_or("Scanned depth module absent")?)
            .map_err(|e| e.to_string())
    };
    let lod = text(
        "material/module-dbf4d515e1bceccfca7bbb1e74bbf0e1f0f8abf3013c6850c72624bdd80d5887.wgsl",
    )?;
    let vertex = text("material/scanned-vertex.wgsl")?;
    let shadow = text("material/scanned-shadow.wgsl")?;
    if !lod.contains("let f = frame.frameIndex;")
        || !vertex.contains("o.vIData = v.iData;")
        || shadow.trim()
            != "return lodFadeVisible( in.pixel, in.vs.vIData.z, in.vs.vIData.w > 0.5 );"
    {
        return Err("Scanned source depth coverage differs".into());
    }
    Ok(format!(
        "struct Frame {{clip_from_world:mat4x4f,world_from_local:mat4x4f,iData:vec4f,frameIndex:u32,pad0:u32,pad1:u32,pad2:u32}};\n\
@group(0) @binding(0) var<uniform> frame:Frame;\n\
{lod}\n\
struct SourceVertex {{position:vec3f,aAsset:f32,iData:vec4f}};\n\
struct SourceOutput {{vIData:vec4f}};\n\
struct ShadowInput {{vs:SourceOutput,pixel:vec2f}};\n\
fn sourceVertex(input:SourceVertex)->SourceVertex {{var v=input;var o:SourceOutput;\n{vertex}\nreturn v;}}\n\
fn sourceShadow(in:ShadowInput)->bool {{\n{shadow}\n}}\n\
struct Output {{@builtin(position) position:vec4f,@location(0) data:vec4f}};\n\
@vertex fn vertex(@location(0) position:vec3f)->Output {{\n\
let v=sourceVertex(SourceVertex(position,frame.iData.x,frame.iData));\n\
var o:Output;o.position=frame.clip_from_world*frame.world_from_local*vec4f(v.position,1.0);o.data=v.iData;return o;\n}}\n\
@fragment fn fragment(in:Output) {{if(!sourceShadow(ShadowInput(SourceOutput(in.data),in.position.xy))){{discard;}}}}\n"
    ))
}

/// Retain only the four acquired near prototypes; share their buffers between owners.
/// # Errors
/// Refuses missing positions/indices or incomplete near-LOD inventory.
pub fn geometry(prepared: &PreparedMeshes) -> Result<Vec<MeshBytes>, String> {
    let mut near: Vec<_> = prepared.prototypes.iter().filter(|p| p.lod == 0).collect();
    near.sort_by_key(|p| p.asset);
    if near.len() != 4
        || near
            .iter()
            .enumerate()
            .any(|(i, p)| usize::from(p.asset) != i)
    {
        return Err("Scanned near caster inventory differs".into());
    }
    near.into_iter()
        .map(|prototype| {
            let Some(VertexAttributeValues::Float32x3(positions)) =
                prototype.mesh.attribute(Mesh::ATTRIBUTE_POSITION)
            else {
                return Err("Scanned caster positions absent".into());
            };
            let Some(Indices::U32(indices)) = prototype.mesh.indices() else {
                return Err("Scanned caster indices absent".into());
            };
            Ok(MeshBytes {
                vertices: positions
                    .iter()
                    .flatten()
                    .flat_map(|v| v.to_le_bytes())
                    .collect(),
                indices: indices.iter().flat_map(|v| v.to_le_bytes()).collect(),
            })
        })
        .collect()
}

/// Keep an entry per accepted placement, enabling only the source's near-LOD draws.
/// # Errors
/// Refuses absent source variations or malformed per-draw storage references.
pub fn poses(
    placements: &[ManualPlacement],
    frame: &scanned_draws::Frame,
    asset_ordinal: impl Fn(AssetId) -> Option<u8>,
) -> Result<Vec<Pose>, String> {
    let mut near = BTreeMap::new();
    for draw in frame.draws.iter().filter(|draw| draw.lod == 0) {
        near.entry(draw.placement_id).or_insert(draw);
    }
    placements
        .iter()
        .map(|placement| {
            let asset =
                asset_ordinal(placement.asset_id).ok_or("Scanned caster variation absent")?;
            let draw = near.get(&placement.id);
            let data = if let Some(draw) = draw {
                *frame
                    .records
                    .get(usize::try_from(draw.tag).map_err(|e| e.to_string())?)
                    .ok_or("Scanned caster tag out of bounds")?
            } else {
                [f32::from(asset), 0.0, 1.0, 1.0]
            };
            Ok(Pose {
                asset,
                model: crate::domain_transform_to_bevy(placement.transform).to_matrix(),
                data,
                enabled: draw.is_some(),
            })
        })
        .collect()
}

/// Prepare submission to the resident cascade graph before publishing the matching visible draws.
///
/// # Errors
/// Refuses invalid poses or a missing scene shadow target.
pub fn prepare_frame(
    world: &World,
    shader: &Arc<String>,
    meshes: &Arc<Vec<MeshBytes>>,
    placements: &[ManualPlacement],
    draws: &scanned_draws::Frame,
    asset_ordinal: impl Fn(AssetId) -> Option<u8>,
) -> Result<Frame, String> {
    let target = world
        .get_resource::<Target>()
        .ok_or("Scanned depth graph target unavailable")?
        .image
        .clone();
    let poses = poses(placements, draws, asset_ordinal)?;
    Ok(Frame {
        target,
        shader: Arc::clone(shader),
        meshes: Arc::clone(meshes),
        poses,
    })
}
