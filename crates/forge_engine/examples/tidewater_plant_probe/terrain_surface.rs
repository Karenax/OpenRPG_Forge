//! Native acquisition adapter for the shared terrain surface material.
use bevy::prelude::*;
use forge_engine::tidewater_caustics::CausticMaterialInputs;
pub use forge_engine::tidewater_terrain::{TerrainMaterial, TerrainSettings};

#[allow(clippy::too_many_arguments)] // Shared scene textures and material resources.
pub fn load(
    data: &[Vec<u8>; 3],
    images: &mut Assets<Image>,
    materials: &mut Assets<TerrainMaterial>,
    environment: Handle<Image>,
    hill_shadow: Handle<Image>,
    object: &super::object_shadow::Target,
    normal: Handle<Image>,
    caustics: Option<&CausticMaterialInputs>,
) -> Handle<TerrainMaterial> {
    let surface =
        forge_engine::tidewater_terrain::textures::PreparedSurface::new(&data[1], &data[2])
            .expect("terrain mip sizes validated before application construction")
            .stage(images);
    let placeholder = images.add(Image::default());
    let caustics = caustics
        .cloned()
        .unwrap_or_else(|| CausticMaterialInputs::disabled(images));
    materials.add(TerrainMaterial {
        shore_state: placeholder.clone(),
        shore_lace: placeholder,
        shore_params: [UVec4::ZERO; 3],
        caustic_fine: caustics.fine,
        caustic_broad: caustics.broad,
        caustic_params: caustics.params,
        underwater_waves: caustics.light_maps.waves,
        underwater_level: caustics.light_maps.level,
        underwater_map_frame: caustics.light_maps.frame,
        environment,
        hill_shadow,
        object_shadow: object.image.clone(),
        object_settings: object.settings.clone(),
        live_shadows: forge_engine::tidewater_object_shadow::receiver::DEFAULT_BUFFER.clone(),
        clouds: [Vec4::ZERO; 5],
        cloud_shadow: None,
        normal,
        splat: surface.splat,
        detail: surface.detail,
        linear_detail: surface.linear_detail,
        settings: TerrainSettings {
            wind: Vec4::new(1.0, 0.0, 6.0, 2.0),
            sun: Vec3::new(0.3, 0.6, -0.7).normalize().extend(0.0),
            domain: Vec4::new(-1024.0, 2048.0, 2048.0, 0.58),
            flags: Vec4::ZERO,
        },
    })
}
