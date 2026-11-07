//! Adapt the module's admitted stall textures to the shared engine material.
use super::FishStandResident;
use bevy::prelude::{Handle, Image};
use forge_engine::{
    source_scene_depth::{DepthCopy, contact_gpu},
    source_surface::{LightingBindings, SourceSurfaceMaterial, stall_material},
    source_temporal::frame::ViewPacket,
};

pub use stall_material::{FullLightingKey, FullMaterial, SHADER, SurfaceFrame, stage_shader};

/// Prepare the common material with the module's resident image arrays.
///
/// # Errors
/// Propagates missing arrays or invalid scene contact inputs.
pub fn prepare_render_owned(
    resident: &FishStandResident,
    lighting: &SourceSurfaceMaterial,
    contact_depth: Handle<Image>,
    inputs: &contact_gpu::Inputs,
) -> Result<FullMaterial, String> {
    let textures = stall_material::TextureArrays::from_named(&resident.textures)?;
    stall_material::prepare_render_owned(&textures, lighting, contact_depth, inputs)
}

/// Prepare the common material using the caller's explicit depth history.
///
/// # Errors
/// Propagates missing arrays, invalid animation or inconsistent depth history.
pub fn prepare(
    resident: &FishStandResident,
    lighting: &LightingBindings,
    camera: &ViewPacket,
    depth: &DepthCopy,
    contact_depth: Handle<Image>,
    frame: SurfaceFrame,
) -> Result<FullMaterial, String> {
    let textures = stall_material::TextureArrays::from_named(&resident.textures)?;
    stall_material::prepare(&textures, lighting, camera, depth, contact_depth, frame)
}
