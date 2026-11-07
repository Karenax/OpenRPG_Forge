//! Material-side water lighting parameters; no map or ocean dependency.
// SPDX-License-Identifier: MIT
// Transferred from tidewater_caustics.rs; see third_party/tidewater/LICENSE.
use bevy::{
    asset::RenderAssetUsages,
    image::{ImageAddressMode, ImageSampler, ImageSamplerDescriptor},
    prelude::*,
    render::{
        render_resource::{Extent3d, ShaderType, TextureDimension, TextureFormat},
        storage::ShaderBuffer,
    },
};
pub const NEUTRAL_FRAME: Handle<bevy::render::storage::ShaderBuffer> =
    bevy::asset::uuid_handle!("f8dc95fb-cc74-431b-832d-7bf1506b9778");

pub mod receivers;

pub fn install_neutral(world: &mut World) -> Result<(), String> {
    let mut buffers = world
        .get_resource_mut::<Assets<ShaderBuffer>>()
        .ok_or("Underwater receivers require shader buffers")?;
    if buffers.get(&NEUTRAL_FRAME).is_none() {
        buffers
            .insert(NEUTRAL_FRAME.id(), ShaderBuffer::from(vec![0_u32; 8]))
            .map_err(|error| error.to_string())?;
    }
    Ok(())
}

/// Explicitly absent water: bindable inputs with the scattering enable bit off.
/// This does not simulate an ocean or admit a missing authored water resource.
pub fn absent_water(world: &mut World) -> Result<(CausticSampleParams, Handle<Image>), String> {
    use bevy::{
        asset::RenderAssetUsages,
        render::render_resource::{Extent3d, TextureDimension, TextureFormat},
    };
    install_neutral(world)?;
    let level = world
        .get_resource_mut::<Assets<Image>>()
        .ok_or("Underwater receivers require images")?
        .add(Image::new_fill(
            Extent3d {
                width: 1,
                height: 1,
                depth_or_array_layers: 2,
            },
            TextureDimension::D2,
            &[0; 8],
            TextureFormat::Rgba16Float,
            RenderAssetUsages::RENDER_WORLD,
        ));
    Ok((CausticSampleParams::new(1.0, 2.0, Vec3::Y, 0.0)?, level))
}
#[derive(Clone, Debug, PartialEq, ShaderType)]
pub struct CausticSampleParams {
    /// Direction from water toward the sun, strength in W.
    pub sun_and_strength: Vec4,
    /// Fine and broad FFT tile size in metres.
    pub tiles_m: Vec4,
    /// Four FFT cascade tile sizes, coarsest first.
    pub wave_tiles_m: Vec4,
    /// XYZ: absorption coefficients. W: refracted shadow-entry control.
    pub water_absorption: Vec4,
    pub water_scattering: Vec4,
}

impl CausticSampleParams {
    pub fn new(
        fine_tile_m: f32,
        broad_tile_m: f32,
        sun_direction: Vec3,
        strength: f32,
    ) -> Result<Self, String> {
        if !fine_tile_m.is_finite()
            || fine_tile_m <= 0.0
            || !broad_tile_m.is_finite()
            || broad_tile_m <= fine_tile_m
            || !sun_direction.is_finite()
            || (sun_direction.length_squared() - 1.0).abs() > 0.001
            || sun_direction.y <= 0.0
            || !strength.is_finite()
            || !(0.0..=4.0).contains(&strength)
        {
            return Err("Invalid source caustic sampling inputs".into());
        }
        Ok(Self {
            sun_and_strength: sun_direction.extend(strength),
            tiles_m: Vec4::new(fine_tile_m, broad_tile_m, 0.0, 0.0),
            wave_tiles_m: Vec4::ZERO,
            water_absorption: Vec4::ZERO,
            water_scattering: Vec4::ZERO,
        })
    }

    pub fn with_underwater(
        mut self,
        wave_tiles_m: [f32; 4],
        sea_level_m: f32,
        reach_m: f32,
        absorption: Vec3,
        scattering: Vec3,
    ) -> Result<Self, String> {
        if wave_tiles_m
            .iter()
            .any(|size| !size.is_finite() || *size <= 0.0)
            || wave_tiles_m.windows(2).any(|pair| pair[0] <= pair[1])
            || !sea_level_m.is_finite()
            || !reach_m.is_finite()
            || !(0.0..=20.0).contains(&reach_m)
            || !absorption.is_finite()
            || !scattering.is_finite()
            || absorption.min_element() < 0.0
            || scattering.min_element() < 0.0
        {
            return Err("Invalid source underwater lighting inputs".into());
        }
        self.tiles_m.z = sea_level_m;
        self.tiles_m.w = reach_m;
        self.wave_tiles_m = Vec4::from_array(wave_tiles_m);
        self.water_absorption = absorption.extend(1.0);
        self.water_scattering = scattering.extend(1.0);
        Ok(self)
    }
}

#[derive(Clone, Debug)]
pub struct Bindings {
    pub waves: Handle<Image>,
    pub level: Handle<Image>,
    pub frame: Handle<ShaderBuffer>,
}
#[derive(Resource, Clone)]
pub struct CausticMaterialInputs {
    pub fine: Handle<Image>,
    pub broad: Handle<Image>,
    pub light_maps: Bindings,
    pub params: CausticSampleParams,
}

impl CausticMaterialInputs {
    /// Valid bindable textures for scenes whose ocean has not been installed.
    /// The disabled bit in the scattering uniform makes their modulation neutral.
    pub fn disabled(images: &mut Assets<Image>) -> Self {
        let mut plain = Image::new_fill(
            Extent3d {
                width: 1,
                height: 1,
                depth_or_array_layers: 1,
            },
            TextureDimension::D2,
            &[0, 60, 0, 60, 0, 0, 0, 0],
            TextureFormat::Rgba16Float,
            RenderAssetUsages::RENDER_WORLD,
        );
        plain.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
            address_mode_u: ImageAddressMode::Repeat,
            address_mode_v: ImageAddressMode::Repeat,
            ..default()
        });
        let plain = images.add(plain);
        let array = images.add(Image::new_fill(
            Extent3d {
                width: 1,
                height: 1,
                depth_or_array_layers: 2,
            },
            TextureDimension::D2,
            &[0; 8],
            TextureFormat::Rgba16Float,
            RenderAssetUsages::RENDER_WORLD,
        ));
        Self {
            fine: plain.clone(),
            broad: plain,
            light_maps: Bindings {
                waves: array.clone(),
                level: array,
                frame: NEUTRAL_FRAME,
            },
            params: CausticSampleParams::new(1.0, 2.0, Vec3::Y, 0.0)
                .expect("neutral caustic inputs"),
        }
    }
}
