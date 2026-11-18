//! Verified CPU preparation for Tidewater's source-authored `FishStandStall`.
//! Material behavior and shader assembly belong to the shared engine.
pub mod full_material;
use bevy::prelude::*;
#[cfg(test)]
use forge_core::bytes_sha256;
use forge_engine::source_surface::stall_assets::{self, Layout, PreparedStall};
pub use forge_engine::source_surface::stall_assets::{Geometry, Payload, TextureArray};
pub use forge_engine::source_surface::stall_material::{
    BASIC_SHADER as FISH_STAND_SHADER, BasicLightingKey as FishStandLightingKey,
    BasicMaterial as FishStandMaterial, LAYER, UV2, install_basic_shader as install_shader,
};
pub use forge_engine::source_surface::stall_scene as full_scene;
pub use forge_engine::source_surface::stall_shader as full_shader;
use serde::Deserialize;
use std::collections::BTreeMap;

const SOURCE_COMMIT: &str = "1438b1abfcaee3267092b75573014f4d9b4a983c";
const VERTICES: usize = 32_333;
const TRIANGLES: usize = 40_259;
#[derive(Clone, Debug, Deserialize)]
pub struct FishStandDescription {
    pub schema: String,
    pub version: u32,
    pub source_commit: String,
    pub placement: Placement,
    pub geometry: Geometry,
    pub textures: BTreeMap<String, TextureArray>,
    pub material: MaterialStatus,
}

#[derive(Clone, Debug, Deserialize)]
pub struct Placement {
    pub x: f32,
    pub z: f32,
    pub yaw_radians: f32,
    pub y: String,
}

#[derive(Clone, Debug, Deserialize)]
pub struct MaterialStatus {
    pub source_wgsl: String,
    pub status: String,
    pub limitations: Vec<String>,
}

/// CPU-prepared resources.  Calling `stage` only registers mesh and image
/// assets; it deliberately cannot imply shader compilation or a rendered pixel.
pub struct FishStandPrepared {
    pub mesh: Mesh,
    pub textures: BTreeMap<String, Image>,
    pub placement: Placement,
    /// Original JSON coordinates for terrain sampling, before GPU f32 projection.
    pub height_sample_xz: [f64; 2],
    pub limitations: Vec<String>,
}

impl FishStandPrepared {
    /// The fixture probe stages an isolated copy; canonical source bytes remain immutable.
    #[must_use]
    pub fn clone_for_probe(&self) -> Self {
        Self {
            mesh: self.mesh.clone(),
            textures: self.textures.clone(),
            placement: self.placement.clone(),
            height_sample_xz: self.height_sample_xz,
            limitations: self.limitations.clone(),
        }
    }
}

pub struct FishStandResident {
    pub mesh: Handle<Mesh>,
    pub textures: BTreeMap<String, Handle<Image>>,
    pub placement: Placement,
    pub limitations: Vec<String>,
}

impl forge_engine::source_surface::stall_material::TextureSource for FishStandResident {
    fn textures(&self) -> &BTreeMap<String, Handle<Image>> {
        &self.textures
    }
}

impl FishStandPrepared {
    pub fn load(files: &BTreeMap<String, Vec<u8>>) -> Result<Self, String> {
        #[derive(Deserialize)]
        struct SamplePosition {
            x: f64,
            z: f64,
        }
        #[derive(Deserialize)]
        struct SampleDescriptor {
            placement: SamplePosition,
        }
        let descriptor = files
            .get("fish-stand.json")
            .ok_or("Missing fish stand descriptor")?;
        let sample: SampleDescriptor =
            serde_json::from_slice(descriptor).map_err(|e| e.to_string())?;
        let height_sample_xz = [sample.placement.x, sample.placement.z];
        if !height_sample_xz.into_iter().all(f64::is_finite) {
            return Err("Non-finite FishStand terrain sampling coordinate".into());
        }
        let description: FishStandDescription =
            serde_json::from_slice(descriptor).map_err(|e| e.to_string())?;
        if description.schema != "forge.tidewater.fish-stand-bevy-input"
            || description.version != 1
            || description.source_commit != SOURCE_COMMIT
            || description.geometry.triangles != TRIANGLES
            || description
                .geometry
                .attributes
                .get("position")
                .is_none_or(|position| position.count != VERTICES)
            || description.placement.y != "terrain.heightAt(x,z)"
            || ![
                description.placement.x,
                description.placement.z,
                description.placement.yaw_radians,
            ]
            .into_iter()
            .all(f32::is_finite)
            || description.material.status != "bevy_base_array_shader_not_source_parity"
        {
            return Err("Unsupported FishStand descriptor".into());
        }
        let layout = Layout {
            geometry: description.geometry,
            textures: description.textures,
        };
        layout.resource_pins()?;
        let PreparedStall { mesh, textures } =
            stall_assets::prepare(&layout, |name| files.get(name).map(Vec::as_slice))?;
        Ok(Self {
            mesh,
            textures,
            placement: description.placement,
            height_sample_xz,
            limitations: description.material.limitations,
        })
    }

    pub fn stage(self, meshes: &mut Assets<Mesh>, images: &mut Assets<Image>) -> FishStandResident {
        FishStandResident {
            mesh: meshes.add(self.mesh),
            textures: self
                .textures
                .into_iter()
                .map(|(name, image)| (name, images.add(image)))
                .collect(),
            placement: self.placement,
            limitations: self.limitations,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn source_attributes_use_separate_bevy_slots() {
        assert_ne!(LAYER.id, UV2.id);
        assert_ne!(LAYER.id, Mesh::ATTRIBUTE_COLOR.id);
        assert_eq!(VERTICES, 32_333);
        assert_eq!(TRIANGLES * 3, 120_777);
    }

    #[test]
    #[ignore = "requires the byte-pinned private source lighting program"]
    fn engine_stall_shader_matches_the_source_lighting_program() {
        let path = std::env::var("FORGE_STALL_LIGHTING_REFERENCE").unwrap();
        let reference = std::fs::read(path).unwrap();
        assert_eq!(
            bytes_sha256(&reference).to_string(),
            "28dd6f58d3f9e51982810e438b9e219f867ceaf8ea34bb5bc2ab9aa2e2bc9e0b"
        );
        let original = std::str::from_utf8(&reference).unwrap();
        assert_eq!(
            forge_engine::source_surface::stall_scene::shader_source(original).unwrap(),
            forge_engine::source_surface::stall_scene::shader_source(
                forge_engine::source_surface::PROFILE_PROGRAM
            )
            .unwrap()
        );
    }
}
