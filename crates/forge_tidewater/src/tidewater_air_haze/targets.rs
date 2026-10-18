//! Per-view render targets matching AirHaze.js setSize, not authoring state.
// SPDX-License-Identifier: MIT
// Tidewater 1438b1ab; copyright 2026 DRG Software Solutions LLC.
use bevy::{
    prelude::*,
    render::{
        render_resource::{
            Extent3d, TextureDescriptor, TextureDimension, TextureFormat, TextureUsages,
        },
        renderer::RenderDevice,
        texture::{CachedTexture, TextureCache},
    },
};

/// Sizes use the drawing-buffer output and source haze scale, before TAAU.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Dimensions {
    pub march: UVec2,
    pub screen: UVec2,
}

impl Dimensions {
    /// Preserve JS Math.round for positive dimensions; reject zero/invalid
    /// extents rather than silently changing the source scale or GPU limits.
    #[must_use]
    pub fn from_output(output: UVec2, scale: f64, maximum_dimension: u32) -> Option<Self> {
        if output.min_element() == 0 || !scale.is_finite() || scale <= 0.0 {
            return None;
        }
        let size = |factor: f64| {
            let axis = |value: u32| {
                let rounded = (f64::from(value) * factor * scale).round();
                if rounded < 1.0 || rounded > f64::from(maximum_dimension) {
                    return None;
                }
                #[allow(
                    clippy::cast_possible_truncation,
                    clippy::cast_sign_loss,
                    reason = "Rounded positive dimension checked against the u32 GPU limit"
                )]
                Some(rounded as u32)
            };
            Some(UVec2::new(axis(output.x)?, axis(output.y)?))
        };
        Some(Self {
            march: size(0.5)?,
            screen: size(0.25)?,
        })
    }

    /// One half-resolution march and four quarter-resolution screen targets.
    #[must_use]
    pub fn descriptors(self) -> [TextureDescriptor<'static>; 5] {
        let descriptor = |label, size: UVec2, format| TextureDescriptor {
            label: Some(label),
            size: Extent3d {
                width: size.x,
                height: size.y,
                depth_or_array_layers: 1,
            },
            mip_level_count: 1,
            sample_count: 1,
            dimension: TextureDimension::D2,
            format,
            usage: TextureUsages::RENDER_ATTACHMENT
                | TextureUsages::TEXTURE_BINDING
                | TextureUsages::COPY_SRC,
            view_formats: &[],
        };
        [
            descriptor(
                "tidewater haze march",
                self.march,
                TextureFormat::Rgba16Float,
            ),
            descriptor(
                "tidewater haze sun mask",
                self.screen,
                TextureFormat::R16Float,
            ),
            descriptor(
                "tidewater haze radial 0",
                self.screen,
                TextureFormat::R16Float,
            ),
            descriptor(
                "tidewater haze radial 1",
                self.screen,
                TextureFormat::R16Float,
            ),
            descriptor(
                "tidewater haze radial 2",
                self.screen,
                TextureFormat::R16Float,
            ),
        ]
    }
}

/// Disposable targets, kept on the render view. Consumers must preflight all
/// pipelines and scene inputs before encoding; allocation alone is not ready.
#[derive(Component)]
pub struct Targets {
    pub dimensions: Dimensions,
    pub march: CachedTexture,
    pub screen: [CachedTexture; 4],
}

impl Targets {
    /// Uses the existing renderer/cache; no second device or asset image copy.
    #[must_use]
    pub fn allocate(
        device: &RenderDevice,
        cache: &mut TextureCache,
        dimensions: Dimensions,
    ) -> Self {
        let [march, mask, radial0, radial1, radial2] = dimensions.descriptors();
        Self {
            dimensions,
            march: cache.get(device, march),
            screen: [
                cache.get(device, mask),
                cache.get(device, radial0),
                cache.get(device, radial1),
                cache.get(device, radial2),
            ],
        }
    }
}

type SelectedViews<'w, 's> = Query<
    'w,
    's,
    (
        Entity,
        &'static crate::tidewater_temporal::frame::ViewPacket,
        &'static forge_engine::atmospheric_haze::AirHazeSettings,
        Option<&'static Targets>,
    ),
    (
        With<super::TidewaterAirHaze>,
        With<crate::tidewater_temporal::resolve::ResolveEnabled>,
    ),
>;

#[allow(clippy::needless_pass_by_value, reason = "Bevy render-system wrappers")]
pub(super) fn prepare(
    mut commands: Commands,
    views: SelectedViews,
    device: Res<RenderDevice>,
    mut cache: ResMut<TextureCache>,
) {
    for (entity, frame, settings, existing) in &views {
        let Some(dimensions) = Dimensions::from_output(
            frame.packet.sizes.zw(),
            settings.resolution_scale(),
            device.limits().max_texture_dimension_2d,
        ) else {
            commands.entity(entity).remove::<Targets>();
            continue;
        };
        if existing.is_some_and(|targets| targets.dimensions == dimensions) {
            continue;
        }
        commands
            .entity(entity)
            .insert(Targets::allocate(&device, &mut cache, dimensions));
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn drawing_buffer_and_scale_determine_source_rounding() {
        let dimensions = Dimensions::from_output(UVec2::new(1919, 1079), 1.0, 16384).unwrap();
        assert_eq!(dimensions.march, UVec2::new(960, 540));
        assert_eq!(dimensions.screen, UVec2::new(480, 270));
        let scaled = Dimensions::from_output(UVec2::new(1920, 1080), 0.75, 16384).unwrap();
        assert_eq!(scaled.march, UVec2::new(720, 405));
        assert_eq!(scaled.screen, UVec2::new(360, 203));
        assert!(Dimensions::from_output(UVec2::ZERO, 1.0, 16384).is_none());
        assert!(Dimensions::from_output(UVec2::new(1, 1), 1.0, 16384).is_none());
        assert!(Dimensions::from_output(UVec2::new(1920, 1080), f64::NAN, 16384).is_none());
        assert!(Dimensions::from_output(UVec2::new(1920, 1080), 1.0, 512).is_none());
    }

    #[test]
    fn target_formats_and_sampling_contract_match_the_five_source_passes() {
        let dimensions = Dimensions::from_output(UVec2::new(1920, 1080), 1.0, 16384).unwrap();
        let descriptors = dimensions.descriptors();
        assert_eq!(descriptors[0].format, TextureFormat::Rgba16Float);
        for descriptor in &descriptors[1..] {
            assert_eq!(descriptor.format, TextureFormat::R16Float);
            assert_eq!((descriptor.size.width, descriptor.size.height), (480, 270));
        }
        for descriptor in descriptors {
            assert_eq!(descriptor.sample_count, 1);
            assert_eq!(descriptor.mip_level_count, 1);
            assert!(descriptor.usage.contains(
                TextureUsages::RENDER_ATTACHMENT
                    | TextureUsages::TEXTURE_BINDING
                    | TextureUsages::COPY_SRC
            ));
        }
    }
}
