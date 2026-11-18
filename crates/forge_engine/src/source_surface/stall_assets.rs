//! CPU mesh and texture-array preparation for admitted stall assets.
use super::stall_material::{LAYER, UV2};
use bevy::{
    asset::RenderAssetUsages,
    image::{ImageAddressMode, ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    mesh::Indices,
    prelude::*,
    render::render_resource::{
        Extent3d, PrimitiveTopology, TextureDataOrder, TextureDimension, TextureFormat,
    },
};
use forge_assets::source_stall_package::TEXTURES;
pub use forge_assets::source_stall_package::{Geometry, Layout, Payload, TextureArray};
use forge_core::{ContentHash, bytes_sha256};
use std::collections::BTreeMap;

pub struct PreparedStall {
    pub mesh: Mesh,
    pub textures: BTreeMap<String, Image>,
}

/// Prepare exactly the declared mesh and seven texture arrays. The caller
/// owns the admitted resource namespace and returns bytes by logical name.
///
/// # Errors
/// Refuses missing or altered resources, invalid stream lengths and texture formats.
pub fn prepare<'a>(
    layout: &Layout,
    read: impl Fn(&str) -> Option<&'a [u8]>,
) -> Result<PreparedStall, String> {
    let attribute = |name: &str, width| {
        let pin = layout.geometry.attribute(name, width)?;
        checked(&read, &pin.file, pin.bytes, pin.sha256)
    };
    let indices = layout.geometry.indices()?;
    let mesh = MeshBytes {
        positions: attribute("position", 3)?,
        normals: attribute("normal", 3)?,
        uv: attribute("uv", 2)?,
        colors: attribute("color", 4)?,
        layers: attribute("aLayer", 1)?,
        secondary_uv: attribute("aUV2", 2)?,
        indices: checked(&read, &indices.file, indices.bytes, indices.sha256)?,
    }
    .prepare()?;
    let textures = TEXTURES
        .into_iter()
        .map(|name| {
            let pin = layout.texture(name)?;
            let address = match pin.address_mode.as_str() {
                "repeat" => ImageAddressMode::Repeat,
                "clamp" => ImageAddressMode::ClampToEdge,
                _ => return Err("Invalid stall texture address mode".into()),
            };
            let bytes = checked(&read, &pin.file, pin.bytes, pin.sha256)?;
            let image = texture_array(
                bytes,
                Extent3d {
                    width: pin.width,
                    height: pin.height,
                    depth_or_array_layers: pin.layers,
                },
                pin.srgb,
                address,
            )?;
            Ok((name.to_owned(), image))
        })
        .collect::<Result<BTreeMap<_, _>, String>>()?;
    Ok(PreparedStall { mesh, textures })
}

fn checked<'a>(
    read: &impl Fn(&str) -> Option<&'a [u8]>,
    file: &str,
    length: usize,
    digest: ContentHash,
) -> Result<&'a [u8], String> {
    let bytes = read(file).ok_or_else(|| format!("Missing stall resource: {file}"))?;
    if bytes.len() != length || bytes_sha256(bytes) != digest {
        return Err(format!("Stall resource size or hash differs: {file}"));
    }
    Ok(bytes)
}

/// Little-endian attributes matching the shared stall material layout.
struct MeshBytes<'a> {
    positions: &'a [u8],
    normals: &'a [u8],
    uv: &'a [u8],
    colors: &'a [u8],
    layers: &'a [u8],
    secondary_uv: &'a [u8],
    indices: &'a [u8],
}

impl MeshBytes<'_> {
    /// Decode the admitted streams without allocating GPU state.
    ///
    /// # Errors
    /// Rejects inconsistent attribute counts, non-finite data or invalid indices.
    fn prepare(&self) -> Result<Mesh, String> {
        let positions = floats::<3>(self.positions)?;
        let normals = floats::<3>(self.normals)?;
        let uv = floats::<2>(self.uv)?;
        let color = floats::<4>(self.colors)?;
        let layer = scalars(self.layers)?;
        let uv2 = floats::<2>(self.secondary_uv)?;
        let count = positions.len();
        if count == 0
            || [normals.len(), uv.len(), color.len(), layer.len(), uv2.len()]
                .into_iter()
                .any(|length| length != count)
            || self.indices.is_empty()
            || !self.indices.len().is_multiple_of(3 * 4)
        {
            return Err("Invalid stall mesh stream lengths".into());
        }
        let indices: Vec<_> = self
            .indices
            .as_chunks::<4>()
            .0
            .iter()
            .map(|b| u32::from_le_bytes(*b))
            .collect();
        if indices.iter().any(|&index| index as usize >= count) {
            return Err("Stall index out of range".into());
        }
        Ok(Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::default(),
        )
        .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, positions)
        .with_inserted_attribute(Mesh::ATTRIBUTE_NORMAL, normals)
        .with_inserted_attribute(Mesh::ATTRIBUTE_UV_0, uv)
        .with_inserted_attribute(Mesh::ATTRIBUTE_COLOR, color)
        .with_inserted_attribute(LAYER, layer)
        .with_inserted_attribute(UV2, uv2)
        .with_inserted_indices(Indices::U32(indices)))
    }
}

/// Prepare the shared stall's RGBA8 array and derive its mip chain.
///
/// # Errors
/// Rejects inconsistent texture dimensions, layer counts or base-level byte lengths.
fn texture_array(
    bytes: &[u8],
    extent: Extent3d,
    srgb: bool,
    address_mode: ImageAddressMode,
) -> Result<Image, String> {
    let Extent3d {
        width,
        height,
        depth_or_array_layers: layers,
    } = extent;
    forge_assets::source_stall_package::validate_texture_extent(
        width,
        height,
        layers,
        bytes.len(),
    )?;
    // The immutable import carries source level zero for every array layer.
    // The source renders each successive mip from the previous one with a
    // linearly-filtered 2x2 box; prepare that derived chain once for Bevy.
    let mips = width.max(height).ilog2() + 1;
    let mip_bytes = box_mip_chain(bytes, width, height, layers, srgb);
    let mut image = Image::new(
        extent,
        TextureDimension::D2,
        bytes.to_vec(),
        if srgb {
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
        address_mode_u: address_mode,
        address_mode_v: address_mode,
        mag_filter: ImageFilterMode::Linear,
        min_filter: ImageFilterMode::Linear,
        mipmap_filter: ImageFilterMode::Linear,
        anisotropy_clamp: 8,
        ..default()
    });
    Ok(image)
}

fn floats<const N: usize>(bytes: &[u8]) -> Result<Vec<[f32; N]>, String> {
    if !bytes.len().is_multiple_of(N * 4) {
        return Err("Invalid stall float payload".into());
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
                .ok_or_else(|| "Non-finite stall attribute".into())
        })
        .collect()
}

fn scalars(bytes: &[u8]) -> Result<Vec<f32>, String> {
    if !bytes.len().is_multiple_of(4) {
        return Err("Invalid stall scalar payload".into());
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
                .ok_or_else(|| "Non-finite stall attribute".into())
        })
        .collect()
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
    fn declared_layout_refuses_altered_resource_bytes() {
        let stream = [0; 12];
        let pin = Payload {
            file: "position.bin".into(),
            bytes: stream.len(),
            sha256: bytes_sha256(&stream),
            item_size: 3,
            count: 1,
        };
        let layout = Layout {
            geometry: Geometry {
                attributes: BTreeMap::from([("position".into(), pin.clone())]),
                indices: Payload {
                    item_size: 4,
                    count: 3,
                    ..pin
                },
                triangles: 1,
            },
            textures: BTreeMap::new(),
        };
        let altered = [1; 12];
        let error = prepare(&layout, |_| Some(&altered)).err();
        assert_eq!(
            error.as_deref(),
            Some("Stall resource size or hash differs: position.bin")
        );
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
