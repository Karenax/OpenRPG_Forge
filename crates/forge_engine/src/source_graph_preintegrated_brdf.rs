//! Preserve a measured 64x64 source BRDF table as a shared linear image.
//! Admission checks storage only; target GPU sampling and render parity are separate.
use bevy::render::render_resource::{Extent3d, TextureDimension, TextureFormat};
use bevy::{asset::RenderAssetUsages, prelude::*};
use forge_core::{ContentHash, bytes_sha256};

pub const SOURCE_GRAPH_PREINTEGRATED_BRDF_WGSL: &str =
    include_str!("shaders/source_preintegrated_brdf.wgsl");

pub struct SourceGraphPreintegratedBrdf {
    image: Image,
}

/// Opaque shared binding. Construction requires validated source storage.
/// Keeping this handle alive does not qualify device support or material parity.
#[derive(Clone, Debug)]
pub struct SourceGraphPreintegratedBrdfBinding {
    image: Handle<Image>,
}

impl SourceGraphPreintegratedBrdfBinding {
    pub(super) fn handle(&self) -> &Handle<Image> {
        &self.image
    }
}

impl SourceGraphPreintegratedBrdf {
    pub fn from_rgba32f_le(
        bytes: &[u8],
        expected_sha256: ContentHash,
        width: u32,
        height: u32,
    ) -> Result<Self, &'static str> {
        if width != 64 || height != 64 || bytes.len() != 64 * 64 * 16 {
            return Err("Source BRDF requires an exact 64x64 RGBA32Float buffer");
        }
        if bytes_sha256(bytes) != expected_sha256 {
            return Err("Source BRDF storage SHA mismatch");
        }
        for channel in bytes.as_chunks::<4>().0 {
            let value = f32::from_le_bytes(*channel);
            if !value.is_finite() || !(0.0..=1.0).contains(&value) {
                return Err("Source BRDF channel is nonfinite or outside [0,1]");
            }
        }
        // Bevy's image storage is native endian. Refuse an incompatible host;
        // the source capture remains little endian and is never reinterpreted.
        if !cfg!(target_endian = "little") {
            return Err("Source BRDF little-endian image storage requires qualification");
        }
        let image = Image::new(
            Extent3d {
                width,
                height,
                depth_or_array_layers: 1,
            },
            TextureDimension::D2,
            bytes.to_vec(),
            TextureFormat::Rgba32Float,
            RenderAssetUsages::MAIN_WORLD | RenderAssetUsages::RENDER_WORLD,
        );
        Ok(Self { image })
    }

    /// Install once and clone the binding across materials. The shader uses
    /// explicit textureLoad interpolation, so no float32 filter feature is needed.
    pub fn install_unqualified(
        self,
        images: &mut Assets<Image>,
    ) -> SourceGraphPreintegratedBrdfBinding {
        SourceGraphPreintegratedBrdfBinding {
            image: images.add(self.image),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn valid() -> Vec<u8> {
        [0.25_f32, 0.75, 0.5, 1.0]
            .into_iter()
            .flat_map(f32::to_le_bytes)
            .cycle()
            .take(64 * 64 * 16)
            .collect()
    }

    #[test]
    fn exact_linear_storage_is_installed_once_and_shared() {
        let bytes = valid();
        let table =
            SourceGraphPreintegratedBrdf::from_rgba32f_le(&bytes, bytes_sha256(&bytes), 64, 64)
                .unwrap();
        assert_eq!(table.image.data.as_deref(), Some(bytes.as_slice()));
        assert_eq!(
            table.image.texture_descriptor.format,
            TextureFormat::Rgba32Float
        );
        assert_eq!(table.image.texture_descriptor.mip_level_count, 1);
        let mut images = Assets::<Image>::default();
        let binding = table.install_unqualified(&mut images);
        let material_bindings: Vec<_> = (0..48).map(|_| binding.clone()).collect();
        assert_eq!(images.len(), 1);
        assert!(
            material_bindings
                .iter()
                .all(|b| b.handle().id() == binding.handle().id())
        );
    }

    #[test]
    fn wrong_sha_extent_and_buffer_lengths_are_refused() {
        let bytes = valid();
        let digest = bytes_sha256(&bytes);
        assert!(
            SourceGraphPreintegratedBrdf::from_rgba32f_le(&bytes, bytes_sha256(b"wrong"), 64, 64)
                .is_err()
        );
        for (width, height) in [(0, 64), (64, 0), (63, 64), (64, 65)] {
            assert!(
                SourceGraphPreintegratedBrdf::from_rgba32f_le(&bytes, digest, width, height)
                    .is_err()
            );
        }
        for len in [0, bytes.len() - 1, bytes.len() + 1] {
            let mut invalid = bytes.clone();
            invalid.resize(len, 0);
            assert!(
                SourceGraphPreintegratedBrdf::from_rgba32f_le(
                    &invalid,
                    bytes_sha256(&invalid),
                    64,
                    64
                )
                .is_err()
            );
        }
    }

    #[test]
    fn invalid_channels_are_refused_even_with_matching_sha() {
        for value in [f32::NAN, f32::INFINITY, f32::NEG_INFINITY, -0.01, 1.01] {
            let mut invalid = valid();
            invalid[..4].copy_from_slice(&value.to_le_bytes());
            assert!(
                SourceGraphPreintegratedBrdf::from_rgba32f_le(
                    &invalid,
                    bytes_sha256(&invalid),
                    64,
                    64
                )
                .is_err()
            );
        }
    }
}
