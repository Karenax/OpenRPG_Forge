//! Source image sampler mapping. Does not qualify imported pixels, mip
//! generation, normal packing, compression, resizing, or runtime GPU parity.
use bevy::image::{ImageAddressMode, ImageFilterMode, ImageSamplerDescriptor};
#[derive(Copy, Clone, Debug)]
pub struct SourceGraphSamplerSettings {
    pub filter_mode: u8,
    pub wrap_u: u8,
    pub wrap_v: u8,
    pub wrap_w: u8,
    pub anisotropy: u16,
    pub mip_bias: f32,
    pub source_mipmaps_enabled: bool,
}
/// Scene quality is separate from the immutable texture importer settings.
/// The D3D policy was measured in the source capture project, not inferred
/// from a pack name or promoted to a global renderer default.
#[derive(Copy, Clone, Debug, Default, PartialEq, Eq, Hash)]
pub enum SourceGraphSceneSampling {
    #[default]
    Authored,
    UnityForceEnableD3d,
}
pub fn source_graph_image_sampler_in_scene(
    settings: SourceGraphSamplerSettings,
    width: u32,
    height: u32,
    mip_levels: u32,
    scene: SourceGraphSceneSampling,
    anisotropic_filtering_supported: bool,
) -> Result<ImageSamplerDescriptor, &'static str> {
    let mut sampler = source_graph_image_sampler(settings, width, height, mip_levels)?;
    if scene == SourceGraphSceneSampling::UnityForceEnableD3d {
        // Only the measured authored aniso=1 case is admitted by the base adapter.
        // Point filtering needs a separate source/native qualification.
        if settings.filter_mode == 0 {
            return Err("Forced source anisotropy with point filtering is unqualified");
        }
        if !anisotropic_filtering_supported {
            return Err("Actual adapter lacks anisotropic filtering; silent downgrade forbidden");
        }
        sampler.anisotropy_clamp = 9;
        sampler.mipmap_filter = ImageFilterMode::Linear;
    }
    Ok(sampler)
}
fn address(value: u8) -> Result<ImageAddressMode, &'static str> {
    match value {
        0 => Ok(ImageAddressMode::Repeat),
        1 => Ok(ImageAddressMode::ClampToEdge),
        2 => Ok(ImageAddressMode::MirrorRepeat),
        _ => Err("Source address mode requires qualification; MirrorOnce is not MirrorRepeat"),
    }
}
/// Width/height/mips refer to the actual imported image after its separately
/// qualified resize/decode. Require all levels declared by the source importer.
/// This creates a descriptor only; it neither manufactures mips nor installs
/// an unqualified Image in the renderer.
pub fn source_graph_image_sampler(
    settings: SourceGraphSamplerSettings,
    width: u32,
    height: u32,
    mip_levels: u32,
) -> Result<ImageSamplerDescriptor, &'static str> {
    if width == 0 || height == 0 {
        return Err("Source image has zero dimension");
    }
    let expected = if settings.source_mipmaps_enabled {
        u32::BITS - width.max(height).leading_zeros()
    } else {
        1
    };
    if mip_levels != expected {
        return Err("Source image mip chain is absent or differs from importer settings");
    }
    if !settings.mip_bias.is_finite() || settings.mip_bias != 0.0 {
        return Err("Nonzero source sampler mip bias requires shader binding qualification");
    }
    if settings.anisotropy != 1 {
        return Err("Source anisotropy requires separate native qualification");
    }
    let (min_filter, mag_filter, mipmap_filter) = match settings.filter_mode {
        0 => (
            ImageFilterMode::Nearest,
            ImageFilterMode::Nearest,
            ImageFilterMode::Nearest,
        ),
        1 => (
            ImageFilterMode::Linear,
            ImageFilterMode::Linear,
            ImageFilterMode::Nearest,
        ),
        2 => (
            ImageFilterMode::Linear,
            ImageFilterMode::Linear,
            ImageFilterMode::Linear,
        ),
        _ => return Err("Unknown source filter mode"),
    };
    Ok(ImageSamplerDescriptor {
        label: None,
        address_mode_u: address(settings.wrap_u)?,
        address_mode_v: address(settings.wrap_v)?,
        address_mode_w: address(settings.wrap_w)?,
        min_filter,
        mag_filter,
        mipmap_filter,
        lod_min_clamp: 0.0,
        lod_max_clamp: f32::from(
            u16::try_from(mip_levels - 1).map_err(|_| "Source mip count exceeds sampler limits")?,
        ),
        compare: None,
        anisotropy_clamp: 1,
        border_color: None,
    })
}
#[cfg(test)]
mod tests {
    use super::*;
    fn settings(mips: bool) -> SourceGraphSamplerSettings {
        SourceGraphSamplerSettings {
            filter_mode: 1,
            wrap_u: 0,
            wrap_v: 0,
            wrap_w: 0,
            anisotropy: 1,
            mip_bias: 0.0,
            source_mipmaps_enabled: mips,
        }
    }
    #[test]
    #[expect(
        clippy::float_cmp,
        reason = "Copied GPU wire values must remain exact; tolerance would weaken this preservation test"
    )]
    fn scene_sampling_keeps_authored_and_effective_descriptors_distinct() {
        let source = settings(true);
        let authored = source_graph_image_sampler_in_scene(
            source,
            2048,
            2048,
            12,
            SourceGraphSceneSampling::Authored,
            false,
        )
        .unwrap();
        let forced = source_graph_image_sampler_in_scene(
            source,
            2048,
            2048,
            12,
            SourceGraphSceneSampling::UnityForceEnableD3d,
            true,
        )
        .unwrap();
        assert_eq!(authored.anisotropy_clamp, 1);
        assert_eq!(authored.mipmap_filter, ImageFilterMode::Nearest);
        assert_eq!(forced.anisotropy_clamp, 9);
        assert_eq!(forced.mipmap_filter, ImageFilterMode::Linear);
        assert_eq!(forced.lod_max_clamp, 11.);
        assert_eq!(forced.address_mode_u, authored.address_mode_u);
        assert_eq!(source.anisotropy, 1);
        assert!(
            source_graph_image_sampler_in_scene(
                source,
                2048,
                2048,
                12,
                SourceGraphSceneSampling::UnityForceEnableD3d,
                false
            )
            .is_err()
        );
        let mut point = source;
        point.filter_mode = 0;
        assert!(
            source_graph_image_sampler_in_scene(
                point,
                2048,
                2048,
                12,
                SourceGraphSceneSampling::UnityForceEnableD3d,
                true
            )
            .is_err()
        );
        assert!(
            source_graph_image_sampler_in_scene(
                source,
                2048,
                2048,
                1,
                SourceGraphSceneSampling::UnityForceEnableD3d,
                true
            )
            .is_err()
        );
    }
    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Exact copied fixture values and GPU wire fields must remain identical; an epsilon would weaken this regression."
    )]
    #[expect(
        clippy::too_many_lines,
        reason = "The complete physical source fixture table is kept with its assertions for review of all admitted cases"
    )]
    fn source_graph_sampler_all150_original_settings_produce_bilinear_repeat_without_trilinear_substitution()
     {
        let cases = [
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (8192, 8192, 14, true),
            (2048, 2048, 12, true),
            (4096, 2048, 13, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 1024, 12, true),
            (2048, 1024, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (4096, 4096, 13, true),
            (2048, 2048, 12, true),
            (2048, 1024, 12, true),
            (2048, 1024, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 1024, 12, true),
            (2048, 1024, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (8192, 8192, 14, true),
            (2048, 2048, 12, true),
            (4096, 2048, 13, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 1024, 12, true),
            (2048, 1024, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (4096, 4096, 13, true),
            (2048, 2048, 12, true),
            (2048, 1024, 12, true),
            (2048, 1024, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 1024, 12, true),
            (2048, 1024, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
            (2048, 2048, 12, true),
        ];
        assert_eq!(cases.len(), 150);
        for (w, h, levels, mips) in cases {
            let sampler = source_graph_image_sampler(settings(mips), w, h, levels).unwrap();
            assert_eq!(sampler.min_filter, ImageFilterMode::Linear);
            assert_eq!(sampler.mag_filter, ImageFilterMode::Linear);
            assert_eq!(sampler.mipmap_filter, ImageFilterMode::Nearest);
            assert_eq!(sampler.address_mode_u, ImageAddressMode::Repeat);
            assert_eq!(sampler.address_mode_v, ImageAddressMode::Repeat);
            assert_eq!(sampler.address_mode_w, ImageAddressMode::Repeat);
            assert_eq!(
                sampler.lod_max_clamp,
                f32::from(u16::try_from(levels - 1).unwrap())
            );
        }
    }
    #[test]
    fn source_graph_sampler_missing_mips_do_not_become_base_level_only() {
        assert!(source_graph_image_sampler(settings(true), 2048, 2048, 1).is_err());
        assert!(source_graph_image_sampler(settings(false), 2048, 2048, 12).is_err());
        assert!(source_graph_image_sampler(settings(true), 0, 2048, 12).is_err());
        assert!(source_graph_image_sampler(settings(true), 2048, 2048, 0).is_err());
        assert!(source_graph_image_sampler(settings(true), 1, 1, 1).is_ok());
    }
    #[test]
    fn source_graph_sampler_filter_address_modes_are_explicit_and_unknown_modes_refuse() {
        for (filter, mip) in [
            (0, ImageFilterMode::Nearest),
            (1, ImageFilterMode::Nearest),
            (2, ImageFilterMode::Linear),
        ] {
            let mut s = settings(false);
            s.filter_mode = filter;
            s.wrap_u = 1;
            s.wrap_v = 2;
            let descriptor = source_graph_image_sampler(s, 8, 8, 1).unwrap();
            assert_eq!(descriptor.mipmap_filter, mip);
            assert_eq!(descriptor.address_mode_u, ImageAddressMode::ClampToEdge);
            assert_eq!(descriptor.address_mode_v, ImageAddressMode::MirrorRepeat);
        }
        let mut s = settings(false);
        s.filter_mode = 255;
        assert!(source_graph_image_sampler(s, 8, 8, 1).is_err());
        let mut s = settings(false);
        s.wrap_w = 3;
        assert!(source_graph_image_sampler(s, 8, 8, 1).is_err());
    }
    #[test]
    fn source_graph_sampler_bias_and_anisotropy_are_not_silently_ignored() {
        for bias in [0.5, f32::NAN, f32::INFINITY] {
            let mut s = settings(false);
            s.mip_bias = bias;
            assert!(source_graph_image_sampler(s, 8, 8, 1).is_err());
        }
        let mut s = settings(false);
        s.anisotropy = 4;
        assert!(source_graph_image_sampler(s, 8, 8, 1).is_err());
    }
}
