//! Shared native half-float source reflection storage. No GPU/render parity claim.
use bevy::image::{ImageAddressMode, ImageFilterMode, ImageSampler, ImageSamplerDescriptor};
use bevy::render::render_resource::{
    Extent3d, TextureDataOrder, TextureDimension, TextureFormat, TextureViewDescriptor,
    TextureViewDimension,
};
use bevy::{asset::RenderAssetUsages, prelude::*};
use forge_core::{ContentHash, bytes_sha256};

pub struct SourceGraphSkyCube {
    image: Image,
}

/// Installation returns one shared image; only checked storage can create it.
#[derive(Clone, Debug)]
pub struct SourceGraphSkyCubeBinding {
    image: Handle<Image>,
    width: u32,
    layers: u32,
    mip_count: u32,
    byte_count: usize,
    sampler: ImageSampler,
}

impl SourceGraphSkyCubeBinding {
    pub(super) fn handle(&self) -> &Handle<Image> {
        &self.image
    }

    /// Cheap frame preflight against the admitted storage layout. Content SHA is
    /// checked at admission; this does not rehash the whole cube every frame.
    pub fn validate_residency(&self, images: &Assets<Image>) -> Result<(), &'static str> {
        let image = images
            .get(&self.image)
            .ok_or("Required source sky image is not resident")?;
        let d = &image.texture_descriptor;
        if d.format != TextureFormat::Rgba16Float
            || d.dimension != TextureDimension::D2
            || d.size.width != self.width
            || d.size.height != self.width
            || d.size.depth_or_array_layers != self.layers
            || d.mip_level_count != self.mip_count
            || d.sample_count != 1
            || image.data_order != TextureDataOrder::MipMajor
            || image.data.as_ref().map(Vec::len) != Some(self.byte_count)
            || image.sampler != self.sampler
        {
            return Err("Required source sky image storage or sampler changed");
        }
        let view = image
            .texture_view_descriptor
            .as_ref()
            .ok_or("Required source sky cube-array view is missing")?;
        if view.dimension != Some(TextureViewDimension::CubeArray)
            || view.format.is_some_and(|f| f != TextureFormat::Rgba16Float)
            || view.aspect != bevy::render::render_resource::TextureAspect::All
            || view.base_mip_level != 0
            || view.base_array_layer != 0
            || view.mip_level_count.is_some_and(|n| n != self.mip_count)
            || view.array_layer_count.is_some_and(|n| n != self.layers)
        {
            return Err("Required source sky cube-array view changed");
        }
        Ok(())
    }
}

impl SourceGraphSkyCube {
    /// Input order is mip, then cube, then CubemapFace (+X,-X,+Y,-Y,+Z,-Z),
    /// then row/column. All native mips are retained, including empty tail mips.
    pub fn from_mip_major_rgba16f_le(
        bytes: &[u8],
        expected_sha256: ContentHash,
        width: u32,
        cube_count: u32,
        mip_count: u32,
    ) -> Result<Self, &'static str> {
        if width == 0
            || !width.is_power_of_two()
            || width > 2048
            || !(1..=4).contains(&cube_count)
            || mip_count != width.ilog2() + 1
        {
            return Err(
                "Source sky requires bounded square power-of-two cubes and a complete mip chain",
            );
        }
        let layers = cube_count * 6;
        let mut expected_bytes = 0_usize;
        for mip in 0..mip_count {
            let side = (width >> mip).max(1) as usize;
            let mip_bytes = side
                .checked_mul(side)
                .and_then(|v| v.checked_mul(layers as usize))
                .and_then(|v| v.checked_mul(8))
                .ok_or("Source sky extent overflow")?;
            expected_bytes = expected_bytes
                .checked_add(mip_bytes)
                .ok_or("Source sky storage overflow")?;
        }
        if bytes.len() != expected_bytes {
            return Err("Source sky complete mip buffer length differs");
        }
        if bytes_sha256(bytes) != expected_sha256 {
            return Err("Source sky storage SHA mismatch");
        }
        for channel in bytes.chunks_exact(2) {
            let bits = u16::from_le_bytes(channel.try_into().unwrap());
            if bits & 0x7c00 == 0x7c00 {
                return Err("Source sky half channel is nonfinite");
            }
        }
        if !cfg!(target_endian = "little") {
            return Err("Source sky little-endian storage requires qualification");
        }
        let mut image = Image::new_uninit(
            Extent3d {
                width,
                height: width,
                depth_or_array_layers: layers,
            },
            TextureDimension::D2,
            TextureFormat::Rgba16Float,
            RenderAssetUsages::MAIN_WORLD | RenderAssetUsages::RENDER_WORLD,
        );
        image.data = Some(bytes.to_vec());
        image.data_order = TextureDataOrder::MipMajor;
        image.texture_descriptor.mip_level_count = mip_count;
        image.texture_view_descriptor = Some(TextureViewDescriptor {
            dimension: Some(TextureViewDimension::CubeArray),
            ..default()
        });
        image.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
            address_mode_u: ImageAddressMode::ClampToEdge,
            address_mode_v: ImageAddressMode::ClampToEdge,
            address_mode_w: ImageAddressMode::ClampToEdge,
            mag_filter: ImageFilterMode::Linear,
            min_filter: ImageFilterMode::Linear,
            mipmap_filter: ImageFilterMode::Linear,
            lod_min_clamp: 0.0,
            lod_max_clamp: (mip_count - 1) as f32,
            ..default()
        });
        Ok(Self { image })
    }

    pub fn install_unqualified(self, images: &mut Assets<Image>) -> SourceGraphSkyCubeBinding {
        let width = self.image.texture_descriptor.size.width;
        let layers = self.image.texture_descriptor.size.depth_or_array_layers;
        let mip_count = self.image.texture_descriptor.mip_level_count;
        let byte_count = self.image.data.as_ref().unwrap().len();
        let sampler = self.image.sampler.clone();
        SourceGraphSkyCubeBinding {
            image: images.add(self.image),
            width,
            layers,
            mip_count,
            byte_count,
            sampler,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    fn valid(cubes: u32) -> Vec<u8> {
        let texels = (16 + 4 + 1) * 6 * cubes as usize;
        [0x3400_u16, 0x3800, 0x3a00, 0x3c00]
            .into_iter()
            .flat_map(u16::to_le_bytes)
            .cycle()
            .take(texels * 8)
            .collect()
    }
    #[test]
    fn all_mips_and_cubes_preserved_in_one_shared_linear_image() {
        for cubes in [1, 2] {
            let bytes = valid(cubes);
            let cube = SourceGraphSkyCube::from_mip_major_rgba16f_le(
                &bytes,
                bytes_sha256(&bytes),
                4,
                cubes,
                3,
            )
            .unwrap();
            assert_eq!(cube.image.data.as_deref(), Some(bytes.as_slice()));
            assert_eq!(cube.image.data_order, TextureDataOrder::MipMajor);
            assert_eq!(
                cube.image.texture_descriptor.format,
                TextureFormat::Rgba16Float
            );
            assert_eq!(
                cube.image.texture_descriptor.size.depth_or_array_layers,
                cubes * 6
            );
            assert_eq!(cube.image.texture_descriptor.mip_level_count, 3);
            assert_eq!(
                cube.image
                    .texture_view_descriptor
                    .as_ref()
                    .unwrap()
                    .dimension,
                Some(TextureViewDimension::CubeArray)
            );
            let ImageSampler::Descriptor(s) = &cube.image.sampler else {
                panic!("Explicit source sampler required")
            };
            assert_eq!(s.mipmap_filter, ImageFilterMode::Linear);
            assert_eq!(s.lod_max_clamp, 2.0);
            let mut images = Assets::<Image>::default();
            let binding = cube.install_unqualified(&mut images);
            let copies: Vec<_> = (0..48).map(|_| binding.clone()).collect();
            assert_eq!(images.len(), 1);
            assert!(
                copies
                    .iter()
                    .all(|b| b.handle().id() == binding.handle().id())
            );
        }
    }
    #[test]
    fn wrong_hash_dimensions_cube_count_and_mip_lengths_refused() {
        let bytes = valid(1);
        let sha = bytes_sha256(&bytes);
        assert!(
            SourceGraphSkyCube::from_mip_major_rgba16f_le(&bytes, bytes_sha256(b"wrong"), 4, 1, 3)
                .is_err()
        );
        for (width, cubes, mips) in [
            (0, 1, 1),
            (3, 1, 2),
            (4096, 1, 13),
            (4, 0, 3),
            (4, 5, 3),
            (4, 1, 0),
            (4, 1, 2),
            (4, 1, 4),
        ] {
            assert!(
                SourceGraphSkyCube::from_mip_major_rgba16f_le(&bytes, sha, width, cubes, mips)
                    .is_err()
            );
        }
        for len in [0, bytes.len() - 1, bytes.len() + 1] {
            let mut b = bytes.clone();
            b.resize(len, 0);
            assert!(
                SourceGraphSkyCube::from_mip_major_rgba16f_le(&b, bytes_sha256(&b), 4, 1, 3)
                    .is_err()
            );
        }
    }
    #[test]
    fn nonfinite_half_values_refused_in_first_or_last_mip() {
        for bits in [0x7c00_u16, 0xfc00, 0x7e01, 0xfe01] {
            for last in [false, true] {
                let mut bytes = valid(1);
                let start = if last { bytes.len() - 2 } else { 0 };
                bytes[start..start + 2].copy_from_slice(&bits.to_le_bytes());
                assert!(
                    SourceGraphSkyCube::from_mip_major_rgba16f_le(
                        &bytes,
                        bytes_sha256(&bytes),
                        4,
                        1,
                        3
                    )
                    .is_err()
                );
            }
        }
    }

    #[test]
    fn residency_refuses_eviction_and_mutated_layout_sampler_or_view() {
        let bytes = valid(1);
        let cube =
            SourceGraphSkyCube::from_mip_major_rgba16f_le(&bytes, bytes_sha256(&bytes), 4, 1, 3)
                .unwrap();
        let mut images = Assets::<Image>::default();
        let binding = cube.install_unqualified(&mut images);
        let original = images.get(binding.handle()).unwrap().clone();
        assert!(binding.validate_residency(&images).is_ok());
        for mutation in 0..13 {
            let mut image = images.get_mut(binding.handle()).unwrap();
            *image = original.clone();
            match mutation {
                0 => image.texture_descriptor.format = TextureFormat::Rgba32Float,
                1 => image.texture_descriptor.size.height = 2,
                2 => image.texture_descriptor.size.depth_or_array_layers = 12,
                3 => image.texture_descriptor.mip_level_count = 2,
                4 => image.texture_descriptor.sample_count = 4,
                5 => image.data_order = TextureDataOrder::LayerMajor,
                6 => {
                    image.data.as_mut().unwrap().pop();
                }
                7 => image.sampler = ImageSampler::Default,
                8 => image.texture_view_descriptor = None,
                9 => {
                    image.texture_view_descriptor.as_mut().unwrap().dimension =
                        Some(TextureViewDimension::D2Array)
                }
                10 => {
                    image
                        .texture_view_descriptor
                        .as_mut()
                        .unwrap()
                        .base_mip_level = 1
                }
                11 => {
                    image
                        .texture_view_descriptor
                        .as_mut()
                        .unwrap()
                        .base_array_layer = 6
                }
                12 => {
                    image
                        .texture_view_descriptor
                        .as_mut()
                        .unwrap()
                        .mip_level_count = Some(2)
                }
                _ => unreachable!(),
            }
            drop(image);
            assert!(
                binding.validate_residency(&images).is_err(),
                "mutation {mutation}"
            );
        }
        *images.get_mut(binding.handle()).unwrap() = original;
        assert!(binding.validate_residency(&images).is_ok());
        images.remove(binding.handle().id());
        assert!(binding.validate_residency(&images).is_err());
    }
}
