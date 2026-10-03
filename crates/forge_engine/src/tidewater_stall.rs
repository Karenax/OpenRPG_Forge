//! Verified CPU preparation for Tidewater's source-authored `FishStandStall`.
//!
//! This deliberately keeps the stall outside the 23-building portable-package
//! contract.  It preserves the source mesh's `aLayer` and `aUV2` attributes
//! and material texture arrays, but does not claim that source WGSL has been
//! transposed into a Bevy shader.  See `FishStandMaterialInputs::limitations`.
use crate::tidewater_material::SceneLighting;
use bevy::{
    asset::RenderAssetUsages,
    image::{ImageAddressMode, ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    mesh::{Indices, MeshVertexAttribute, MeshVertexBufferLayoutRef},
    pbr::{Material, MaterialPipeline, MaterialPipelineKey},
    prelude::*,
    render::render_resource::{
        AsBindGroup, Extent3d, Face, PrimitiveTopology, RenderPipelineDescriptor, TextureDataOrder,
        TextureDimension, TextureFormat, VertexFormat,
    },
    shader::ShaderRef,
};
use forge_core::bytes_sha256;
use serde::Deserialize;
use std::collections::BTreeMap;

pub const LAYER: MeshVertexAttribute =
    MeshVertexAttribute::new("FishStandLayer", 0x7457_0007, VertexFormat::Float32);
pub const UV2: MeshVertexAttribute =
    MeshVertexAttribute::new("FishStandUv2", 0x7457_0008, VertexFormat::Float32x2);

const SOURCE_COMMIT: &str = "1438b1abfcaee3267092b75573014f4d9b4a983c";
const VERTICES: usize = 32_333;
const TRIANGLES: usize = 40_259;
pub const FISH_STAND_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("c1396585-3120-4a83-92b3-32de96c58445");

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
pub struct Geometry {
    pub attributes: BTreeMap<String, Payload>,
    pub indices: Payload,
    pub triangles: usize,
}

#[derive(Clone, Debug, Deserialize)]
pub struct Payload {
    pub file: String,
    pub bytes: usize,
    pub sha256: String,
    pub item_size: usize,
    pub count: usize,
}

#[derive(Clone, Debug, Deserialize)]
pub struct TextureArray {
    pub file: String,
    pub bytes: usize,
    pub sha256: String,
    pub width: u32,
    pub height: u32,
    pub layers: u32,
    pub srgb: bool,
    pub address_mode: String,
    pub mip_status: String,
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

/// The first executable Bevy material path: it samples the source albedo,
/// normal and roughness arrays selected by `aLayer`, and consumes `aUV2` for
/// sign/dial layers.  Its compact lighting is deliberately not the upstream
/// `standard()` WGSL material; `limitations` records each omitted response.
#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct FishStandMaterial {
    #[texture(0, dimension = "2d_array")]
    #[sampler(1)]
    pub surf_a: Handle<Image>,
    #[texture(2, dimension = "2d_array")]
    pub surf_n: Handle<Image>,
    #[texture(3, dimension = "2d_array")]
    pub surf_r: Handle<Image>,
    #[texture(4, dimension = "2d_array")]
    pub prop_a: Handle<Image>,
    #[texture(5, dimension = "2d_array")]
    pub prop_n: Handle<Image>,
    #[texture(6, dimension = "2d_array")]
    pub prop_r: Handle<Image>,
    #[texture(7, dimension = "2d_array")]
    #[sampler(8)]
    pub signs: Handle<Image>,
    #[uniform(18)]
    pub scene_lighting: SceneLighting,
}

impl FishStandMaterial {
    pub fn from_resident(
        resident: &FishStandResident,
        scene_lighting: SceneLighting,
    ) -> Result<Self, String> {
        let texture = |name| {
            resident
                .textures
                .get(name)
                .cloned()
                .ok_or("FishStand texture unresolved")
        };
        Ok(Self {
            surf_a: texture("surf_a")?,
            surf_n: texture("surf_n")?,
            surf_r: texture("surf_r")?,
            prop_a: texture("prop_a")?,
            prop_n: texture("prop_n")?,
            prop_r: texture("prop_r")?,
            signs: texture("signs")?,
            scene_lighting,
        })
    }
}

impl Material for FishStandMaterial {
    fn vertex_shader() -> ShaderRef {
        FISH_STAND_SHADER.into()
    }
    fn fragment_shader() -> ShaderRef {
        FISH_STAND_SHADER.into()
    }
    fn enable_shadows() -> bool {
        false
    }
    fn specialize(
        _: &MaterialPipeline,
        descriptor: &mut RenderPipelineDescriptor,
        layout: &MeshVertexBufferLayoutRef,
        _: MaterialPipelineKey<Self>,
    ) -> Result<(), bevy::material::specialize::SpecializedMeshPipelineError> {
        if descriptor.vertex.shader.id() == FISH_STAND_SHADER.id() {
            descriptor.vertex.buffers = vec![layout.0.get_layout(&[
                Mesh::ATTRIBUTE_POSITION.at_shader_location(0),
                Mesh::ATTRIBUTE_NORMAL.at_shader_location(1),
                Mesh::ATTRIBUTE_UV_0.at_shader_location(2),
                Mesh::ATTRIBUTE_COLOR.at_shader_location(3),
                LAYER.at_shader_location(4),
                UV2.at_shader_location(5),
            ])?];
        }
        descriptor.primitive.cull_mode = Some(Face::Back);
        Ok(())
    }
}

/// Install the embedded adapter shader without an `AssetServer` fallback.
pub fn install_shader(app: &mut App) -> Result<(), String> {
    app.world_mut()
        .get_resource_mut::<Assets<Shader>>()
        .ok_or("FishStand shader assets unavailable")?
        .insert(
            FISH_STAND_SHADER.id(),
            Shader::from_wgsl(
                include_str!("tidewater_stall.wgsl"),
                "tidewater-fish-stand.wgsl",
            ),
        )
        .map_err(|e| e.to_string())?;
    Ok(())
}

impl FishStandPrepared {
    pub fn load(files: &BTreeMap<String, Vec<u8>>) -> Result<Self, String> {
        let description: FishStandDescription = serde_json::from_slice(
            files
                .get("fish-stand.json")
                .ok_or("Missing fish stand descriptor")?,
        )
        .map_err(|e| e.to_string())?;
        if description.schema != "forge.tidewater.fish-stand-bevy-input"
            || description.version != 1
            || description.source_commit != SOURCE_COMMIT
            || description.geometry.triangles != TRIANGLES
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
        let payload = |name: &str, item_size: usize| -> Result<&[u8], String> {
            let entry = description
                .geometry
                .attributes
                .get(name)
                .ok_or("Missing FishStand attribute")?;
            if entry.item_size != item_size || entry.count != VERTICES {
                return Err("Invalid FishStand attribute shape".into());
            }
            checked(files, entry)
        };
        let positions = floats::<3>(payload("position", 3)?)?;
        let normals = floats::<3>(payload("normal", 3)?)?;
        let uv = floats::<2>(payload("uv", 2)?)?;
        let color = floats::<4>(payload("color", 4)?)?;
        let layer = scalars(payload("aLayer", 1)?)?;
        let uv2 = floats::<2>(payload("aUV2", 2)?)?;
        let indices = checked(files, &description.geometry.indices)?;
        if description.geometry.indices.item_size != 4
            || description.geometry.indices.count != TRIANGLES * 3
            || indices.len() != TRIANGLES * 3 * 4
        {
            return Err("Invalid FishStand indices".into());
        }
        let indices: Vec<_> = indices
            .as_chunks::<4>()
            .0
            .iter()
            .map(|b| u32::from_le_bytes(*b))
            .collect();
        if indices.iter().any(|&i| i as usize >= VERTICES) {
            return Err("FishStand index out of range".into());
        }
        let mesh = Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::default(),
        )
        .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, positions)
        .with_inserted_attribute(Mesh::ATTRIBUTE_NORMAL, normals)
        .with_inserted_attribute(Mesh::ATTRIBUTE_UV_0, uv)
        .with_inserted_attribute(Mesh::ATTRIBUTE_COLOR, color)
        .with_inserted_attribute(LAYER, layer)
        .with_inserted_attribute(UV2, uv2)
        .with_inserted_indices(Indices::U32(indices));
        let mut textures = BTreeMap::new();
        for (name, texture) in &description.textures {
            textures.insert(name.clone(), array_image(files, texture)?);
        }
        if textures.len() != 7
            || ![
                "surf_a", "surf_n", "surf_r", "prop_a", "prop_n", "prop_r", "signs",
            ]
            .into_iter()
            .all(|key| textures.contains_key(key))
        {
            return Err("Incomplete FishStand texture arrays".into());
        }
        Ok(Self {
            mesh,
            textures,
            placement: description.placement,
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

fn checked<'a>(files: &'a BTreeMap<String, Vec<u8>>, entry: &Payload) -> Result<&'a [u8], String> {
    let bytes = files.get(&entry.file).ok_or("Missing FishStand payload")?;
    if bytes.len() != entry.bytes || bytes_sha256(bytes).to_string() != entry.sha256 {
        return Err("FishStand payload hash differs".into());
    }
    Ok(bytes)
}

fn floats<const N: usize>(bytes: &[u8]) -> Result<Vec<[f32; N]>, String> {
    if !bytes.len().is_multiple_of(N * 4) {
        return Err("Invalid FishStand float payload".into());
    }
    bytes
        .as_chunks::<4>()
        .0
        .as_chunks::<N>()
        .0
        .iter()
        .map(|chunk| {
            let value = std::array::from_fn(|i| f32::from_le_bytes(chunk[i]));
            value
                .iter()
                .all(|v| v.is_finite())
                .then_some(value)
                .ok_or_else(|| "Non-finite FishStand attribute".into())
        })
        .collect()
}

fn scalars(bytes: &[u8]) -> Result<Vec<f32>, String> {
    if !bytes.len().is_multiple_of(4) {
        return Err("Invalid FishStand scalar payload".into());
    }
    bytes
        .as_chunks::<4>()
        .0
        .iter()
        .map(|chunk| f32::from_le_bytes(*chunk))
        .map(|value| {
            value
                .is_finite()
                .then_some(value)
                .ok_or_else(|| "Non-finite FishStand attribute".into())
        })
        .collect()
}

fn array_image(files: &BTreeMap<String, Vec<u8>>, texture: &TextureArray) -> Result<Image, String> {
    if !texture.width.is_power_of_two()
        || !texture.height.is_power_of_two()
        || texture.width > 2048
        || texture.height > 2048
        || texture.layers == 0
        || texture.layers > 32
        || !matches!(texture.address_mode.as_str(), "repeat" | "clamp")
        || texture.mip_status != "base_level_only_source_gpu_mips_unported"
    {
        return Err("Invalid FishStand texture array metadata".into());
    }
    let bytes = files
        .get(&texture.file)
        .ok_or("Missing FishStand texture array")?;
    let expected = texture.width as usize * texture.height as usize * texture.layers as usize * 4;
    if bytes.len() != expected
        || bytes.len() != texture.bytes
        || bytes_sha256(bytes).to_string() != texture.sha256
    {
        return Err("FishStand texture array differs".into());
    }
    // The immutable import carries source level zero for every array layer.
    // Tidewater renders each successive mip from the previous one with a
    // linearly-filtered 2x2 box; prepare that derived chain once for Bevy.
    let mips = texture.width.max(texture.height).ilog2() + 1;
    let mip_bytes = box_mip_chain(
        bytes,
        texture.width,
        texture.height,
        texture.layers,
        texture.srgb,
    );
    let mut image = Image::new(
        Extent3d {
            width: texture.width,
            height: texture.height,
            depth_or_array_layers: texture.layers,
        },
        TextureDimension::D2,
        bytes.clone(),
        if texture.srgb {
            TextureFormat::Rgba8UnormSrgb
        } else {
            TextureFormat::Rgba8Unorm
        },
        RenderAssetUsages::default(),
    );
    image.data = Some(mip_bytes);
    image.data_order = TextureDataOrder::MipMajor;
    image.texture_descriptor.mip_level_count = mips;
    image.texture_view_descriptor = Some(bevy::render::render_resource::TextureViewDescriptor {
        dimension: Some(bevy::render::render_resource::TextureViewDimension::D2Array),
        ..default()
    });
    image.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
        address_mode_u: if texture.address_mode == "repeat" {
            ImageAddressMode::Repeat
        } else {
            ImageAddressMode::ClampToEdge
        },
        address_mode_v: if texture.address_mode == "repeat" {
            ImageAddressMode::Repeat
        } else {
            ImageAddressMode::ClampToEdge
        },
        mag_filter: ImageFilterMode::Linear,
        min_filter: ImageFilterMode::Linear,
        mipmap_filter: ImageFilterMode::Linear,
        anisotropy_clamp: 8,
        ..default()
    });
    Ok(image)
}

/// Mip-major RGBA8 array layout: all layers of level 0, then all layers of
/// level 1, etc. sRGB RGB channels are averaged in linear light, matching the
/// source's sRGB texture sampling followed by rendering to an sRGB mip target.
fn box_mip_chain(base: &[u8], width: u32, height: u32, layers: u32, srgb: bool) -> Vec<u8> {
    let mip_count = width.max(height).ilog2() + 1;
    let total_bytes: usize = (0..mip_count)
        .map(|level| ((width >> level).max(1) * (height >> level).max(1) * layers * 4) as usize)
        .sum();
    let mut chain = Vec::with_capacity(total_bytes);
    chain.extend_from_slice(base);
    let mut previous_start = 0;
    let mut previous_len = base.len();
    let (mut previous_width, mut previous_height) = (width as usize, height as usize);
    let layer_count = layers as usize;
    let decode: [f32; 256] = std::array::from_fn(|byte| {
        let encoded = f32::from(u8::try_from(byte).expect("256-entry byte lookup")) / 255.0;
        if encoded <= 0.04045 {
            encoded / 12.92
        } else {
            ((encoded + 0.055) / 1.055).powf(2.4)
        }
    });
    let encode: Vec<u8> = (0..=u16::MAX)
        .map(|index| {
            let linear = f32::from(index) / 65_535.0;
            let encoded = if linear <= 0.003_130_8 {
                linear * 12.92
            } else {
                1.055 * linear.powf(1.0 / 2.4) - 0.055
            };
            quantize_byte(encoded * 255.0)
        })
        .collect();
    while previous_width > 1 || previous_height > 1 {
        let next_width = (previous_width / 2).max(1);
        let next_height = (previous_height / 2).max(1);
        let mut next = vec![0; next_width * next_height * layer_count * 4];
        let previous = &chain[previous_start..previous_start + previous_len];
        for layer in 0..layer_count {
            for y in 0..next_height {
                for x in 0..next_width {
                    let dst = ((layer * next_height + y) * next_width + x) * 4;
                    for channel in 0..4 {
                        let mut sum = 0.0;
                        for dy in 0..2 {
                            for dx in 0..2 {
                                let sx = (x * 2 + dx).min(previous_width - 1);
                                let sy = (y * 2 + dy).min(previous_height - 1);
                                let src = ((layer * previous_height + sy) * previous_width + sx)
                                    * 4
                                    + channel;
                                sum += if srgb && channel < 3 {
                                    decode[previous[src] as usize]
                                } else {
                                    f32::from(previous[src]) / 255.0
                                };
                            }
                        }
                        let mean = sum * 0.25;
                        next[dst + channel] = if srgb && channel < 3 {
                            encode[quantize_lookup_index(mean * 65_535.0)]
                        } else {
                            quantize_byte(mean * 255.0)
                        };
                    }
                }
            }
        }
        chain.extend_from_slice(&next);
        previous_start += previous_len;
        previous_len = next.len();
        previous_width = next_width;
        previous_height = next_height;
    }
    chain
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "Rounding and clamping bound this float to one byte before conversion."
)]
fn quantize_byte(value: f32) -> u8 {
    value.round().clamp(0.0, 255.0) as u8
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "Rounding and clamping bound this float to the 65536-entry lookup before conversion."
)]
fn quantize_lookup_index(value: f32) -> usize {
    value.round().clamp(0.0, 65_535.0) as usize
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
    fn array_mips_preserve_layer_boundaries_and_srgb_filtering() {
        let first = [
            [0, 0, 0, 255],
            [255, 255, 255, 255],
            [0, 0, 0, 255],
            [255, 255, 255, 255],
        ];
        let second = [[32, 64, 128, 255]; 4];
        let base: Vec<u8> = first.into_iter().chain(second).flatten().collect();
        let linear = box_mip_chain(&base, 2, 2, 2, false);
        let gamma = box_mip_chain(&base, 2, 2, 2, true);
        assert_eq!(linear.len(), 40);
        assert_eq!(&linear[32..36], &[128, 128, 128, 255]);
        assert_eq!(&linear[36..40], &[32, 64, 128, 255]);
        assert_eq!(&gamma[32..36], &[188, 188, 188, 255]);
        assert_eq!(&gamma[36..40], &[32, 64, 128, 255]);
    }
}
