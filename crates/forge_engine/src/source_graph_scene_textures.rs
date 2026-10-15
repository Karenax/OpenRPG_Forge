//! Borrow scene images after explicit format, viewport and sampler admission.
//! Producers own their contents; this does not qualify rendering or freshness.
use crate::source_graph::{SourceGraphSamplerSettings, source_graph_image_sampler};
use bevy::{
    image::ImageSampler,
    prelude::*,
    render::render_resource::{TextureDimension, TextureFormat, TextureUsages},
};
use forge_assets::source_graph_bundle::{SourceGraphSceneTexture, SourceGraphSceneTextureFormat};
use std::collections::BTreeMap;

#[derive(Clone, Debug)]
pub struct SourceGraphSceneTextureBindings {
    declarations: Vec<SourceGraphSceneTexture>,
    handles: Vec<(usize, Handle<Image>)>,
    viewport: [u32; 2],
}

/// Supplied explicitly by the current scene producer. Absence is not a blank
/// frame and never admits a material that declares a required scene image.
#[derive(Resource)]
pub struct SourceGraphSceneTextureInputs {
    pub viewport: [u32; 2],
    pub images: BTreeMap<String, Handle<Image>>,
}

impl SourceGraphSceneTextureBindings {
    pub fn prepare(
        declarations: &[SourceGraphSceneTexture],
        images: &Assets<Image>,
        viewport: [u32; 2],
        resources: &BTreeMap<String, Handle<Image>>,
    ) -> Result<Self, &'static str> {
        let mut handles = Vec::new();
        let mut slots = std::collections::BTreeSet::new();
        for declaration in declarations {
            declaration
                .validate()
                .map_err(|_| "Invalid scene texture declaration")?;
            if !slots.insert(declaration.slot) {
                return Err("Duplicate scene texture slot");
            }
            let handle = resources
                .get(&declaration.resource_key)
                .ok_or("Required scene texture unavailable")?;
            validate_image(declaration, handle, images, viewport)?;
            handles.push((declaration.slot, handle.clone()));
        }
        handles.sort_by_key(|(slot, _)| *slot);
        Ok(Self {
            declarations: declarations.to_vec(),
            handles,
            viewport,
        })
    }

    pub(crate) fn for_program(
        &self,
        declarations: &[SourceGraphSceneTexture],
        images: &Assets<Image>,
    ) -> Result<Vec<(usize, Handle<Image>)>, &'static str> {
        if declarations != self.declarations {
            return Err("Scene texture declarations changed");
        }
        for declaration in declarations {
            let (_, handle) = self
                .handles
                .iter()
                .find(|(slot, _)| *slot == declaration.slot)
                .ok_or("Scene texture slot absent")?;
            validate_image(declaration, handle, images, self.viewport)?;
        }
        Ok(self.handles.clone())
    }
}

#[allow(
    clippy::float_cmp,
    reason = "Sampler LOD controls are exact source ABI values, not measured pixels"
)]
fn validate_image(
    declaration: &SourceGraphSceneTexture,
    handle: &Handle<Image>,
    images: &Assets<Image>,
    viewport: [u32; 2],
) -> Result<(), &'static str> {
    let image = images.get(handle).ok_or("Scene image asset absent")?;
    let size = declaration
        .dimensions
        .resolve(viewport)
        .map_err(|_| "Scene viewport invalid")?;
    let format = match declaration.format {
        SourceGraphSceneTextureFormat::Rgba8Unorm => TextureFormat::Rgba8Unorm,
        SourceGraphSceneTextureFormat::Rgb10a2Unorm => TextureFormat::Rgb10a2Unorm,
        SourceGraphSceneTextureFormat::Rgba16Float => TextureFormat::Rgba16Float,
        SourceGraphSceneTextureFormat::Rgba32Float => TextureFormat::Rgba32Float,
    };
    let d = &image.texture_descriptor;
    if d.dimension != TextureDimension::D2
        || d.format != format
        || d.size.width != size[0]
        || d.size.height != size[1]
        || d.size.depth_or_array_layers != 1
        || d.mip_level_count != 1
        || d.sample_count != 1
        || !d.usage.contains(TextureUsages::TEXTURE_BINDING)
        || !image
            .asset_usage
            .contains(bevy::asset::RenderAssetUsages::RENDER_WORLD)
    {
        return Err("Scene image format, viewport, mips or usage differs");
    }
    let s = &declaration.sampler;
    if s.mip_bias != 0.0 {
        return Err("Scene sampler nonzero mip bias requires qualification");
    }
    let settings = SourceGraphSamplerSettings {
        filter_mode: s.filter_mode,
        wrap_u: s.wrap[0],
        wrap_v: s.wrap[1],
        wrap_w: s.wrap[2],
        anisotropy: u16::from(s.anisotropy),
        mip_bias: 0.0,
        source_mipmaps_enabled: s.source_mipmaps_enabled,
    };
    let expected = source_graph_image_sampler(settings, size[0], size[1], 1)?;
    let ImageSampler::Descriptor(actual) = &image.sampler else {
        return Err("Scene sampler must be explicit");
    };
    if actual.address_mode_u != expected.address_mode_u
        || actual.address_mode_v != expected.address_mode_v
        || actual.address_mode_w != expected.address_mode_w
        || actual.min_filter != expected.min_filter
        || actual.mag_filter != expected.mag_filter
        || actual.mipmap_filter != expected.mipmap_filter
        || actual.lod_min_clamp != expected.lod_min_clamp
        || actual.lod_max_clamp != expected.lod_max_clamp
        || actual.anisotropy_clamp != expected.anisotropy_clamp
        || actual.compare != expected.compare
        || actual.border_color != expected.border_color
    {
        return Err("Scene sampler differs from the source declaration");
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{asset::RenderAssetUsages, render::render_resource::Extent3d};
    use forge_assets::source_graph_bundle::{
        SourceGraphResolvedStorageSampler, SourceGraphSceneTextureDimensions,
    };

    fn declaration(
        slot: usize,
        key: &str,
        format: SourceGraphSceneTextureFormat,
    ) -> SourceGraphSceneTexture {
        SourceGraphSceneTexture {
            slot,
            resource_key: key.into(),
            format,
            dimensions: if slot == 0 {
                SourceGraphSceneTextureDimensions::Viewport
            } else {
                SourceGraphSceneTextureDimensions::Fixed {
                    width: 4,
                    height: 4,
                }
            },
            sampler: SourceGraphResolvedStorageSampler {
                filter_mode: 0,
                wrap: [1; 3],
                anisotropy: 1,
                mip_bias: 0.0,
                source_mipmaps_enabled: false,
            },
        }
    }
    fn image(format: TextureFormat) -> Image {
        let mut image = Image::new(
            Extent3d {
                width: 4,
                height: 4,
                depth_or_array_layers: 1,
            },
            TextureDimension::D2,
            vec![0; 64],
            format,
            RenderAssetUsages::default(),
        );
        image.sampler = ImageSampler::Descriptor(
            source_graph_image_sampler(
                SourceGraphSamplerSettings {
                    filter_mode: 0,
                    wrap_u: 1,
                    wrap_v: 1,
                    wrap_w: 1,
                    anisotropy: 1,
                    mip_bias: 0.0,
                    source_mipmaps_enabled: false,
                },
                4,
                4,
                1,
            )
            .unwrap(),
        );
        image
    }
    #[test]
    fn borrowed_scene_images_require_exact_residency_and_never_allocate_fallbacks() {
        let declarations = vec![
            declaration(0, "frame.buffer", SourceGraphSceneTextureFormat::Rgba8Unorm),
            declaration(
                1,
                "scene.lookup",
                SourceGraphSceneTextureFormat::Rgb10a2Unorm,
            ),
        ];
        let mut images = Assets::default();
        let target = images.add(image(TextureFormat::Rgba8Unorm));
        let lookup = images.add(image(TextureFormat::Rgb10a2Unorm));
        let mut resources = BTreeMap::from([("frame.buffer".into(), target.clone())]);
        assert!(
            SourceGraphSceneTextureBindings::prepare(&declarations, &images, [4, 4], &resources)
                .is_err()
        );
        assert_eq!(images.len(), 2);
        resources.insert("scene.lookup".into(), lookup.clone());
        let admitted =
            SourceGraphSceneTextureBindings::prepare(&declarations, &images, [4, 4], &resources)
                .unwrap();
        let handles = admitted.for_program(&declarations, &images).unwrap();
        assert_eq!(handles, vec![(0, target.clone()), (1, lookup.clone())]);
        assert!(
            SourceGraphSceneTextureBindings::prepare(&declarations, &images, [8, 4], &resources)
                .is_err()
        );
        images.get_mut(&lookup).unwrap().sampler = ImageSampler::Default;
        assert!(admitted.for_program(&declarations, &images).is_err());
        *images.get_mut(&lookup).unwrap() = image(TextureFormat::Rgba8Unorm);
        assert!(admitted.for_program(&declarations, &images).is_err());
        images.remove(target.id());
        assert!(admitted.for_program(&declarations, &images).is_err());
        assert!(images.get(&lookup).is_some());
    }
}
