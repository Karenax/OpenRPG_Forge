//! Shared capture identities and source water shader transforms.
// SPDX-License-Identifier: MIT
// Shader transforms derive from Tidewater 1438b1ab, DRG Software Solutions LLC.
// See third_party/tidewater/LICENSE. These are transient renderer contracts.
use bevy::{prelude::*, render::extract_component::ExtractComponent};
pub mod shaders;
pub use shaders::water as water_shader_source;
/// Camera adapters synchronize derived captures in this shared ordering set.
#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct Synchronize;
#[derive(Component, Clone, ExtractComponent)]
#[require(crate::scene_camera::OffscreenSceneCamera)]
pub struct CaptureCamera;
/// Source view identity for a derived capture, never a persistent document.
#[derive(Component, Clone, Copy, Debug, ExtractComponent)]
pub struct CaptureSource {
    pub view: Entity,
}

/// Carries the source visible-surface medium in the opaque HDR alpha channel.
/// The immersion pass consumes it before other post passes overwrite alpha.
pub fn visible_water_mask_shader_source(source: &str) -> Result<String, String> {
    const OLD: &str = "return shadeWater(i).color;";
    const NEW: &str =
        "let result = shadeWater(i);\n return vec4f(result.color.rgb, 0.25 + 0.5 * result.mask.x);";
    if source.matches(OLD).count() != 1 {
        return Err("water optics fragment mask anchor is missing or ambiguous".into());
    }
    Ok(source.replacen(OLD, NEW, 1))
}
