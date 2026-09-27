//! Native acquisition adapter for the shared terrain surface material.
use bevy::prelude::*;
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
) -> Handle<TerrainMaterial> {
    let surface =
        forge_engine::tidewater_terrain::textures::PreparedSurface::new(&data[1], &data[2])
            .expect("terrain mip sizes validated before application construction")
            .stage(images);
    let placeholder = images.add(Image::default());
    materials.add(TerrainMaterial {
        shore_state: placeholder.clone(),
        shore_lace: placeholder,
        shore_params: [UVec4::ZERO; 3],
        environment,
        hill_shadow,
        object_shadow: object.image.clone(),
        object_settings: object.settings.clone(),
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
