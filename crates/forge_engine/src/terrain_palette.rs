//! One authored three-role palette shared by terrain material consumers.
use bevy::{asset::embedded_asset, prelude::*, render::render_resource::ShaderType};

/// Same seven uniform rows as the existing procedural terrain extension.
#[derive(Clone, Debug, PartialEq, ShaderType)]
pub struct Parameters {
    pub grass_color_roughness: Vec4,
    pub earth_color_roughness: Vec4,
    pub rock_color_roughness: Vec4,
    pub grass_metallic_reflectance: Vec4,
    pub earth_metallic_reflectance: Vec4,
    pub rock_metallic_reflectance: Vec4,
    pub wetness_parameters: Vec4,
}

impl Default for Parameters {
    fn default() -> Self {
        let original = crate::visual_valley::VisualValleyTerrainExtension::default();
        Self {
            grass_color_roughness: original.grass_color_roughness,
            earth_color_roughness: original.earth_color_roughness,
            rock_color_roughness: original.rock_color_roughness,
            grass_metallic_reflectance: original.grass_metallic_reflectance,
            earth_metallic_reflectance: original.earth_metallic_reflectance,
            rock_metallic_reflectance: original.rock_metallic_reflectance,
            wetness_parameters: original.wetness_parameters,
        }
    }
}

#[derive(Resource)]
struct Registered {
    // Manually inserted Shader::from_wgsl programs do not load file imports.
    // Retain this dependency even when no file-loaded parent shader is active.
    shader: Option<Handle<Shader>>,
}

pub(crate) fn register(app: &mut App) {
    if !app.world().contains_resource::<Registered>() {
        embedded_asset!(app, "shaders/terrain_palette.wgsl");
        app.insert_resource(Registered { shader: None });
    }
    // CPU authoring/quality apps deliberately have no graphics asset allocator.
    // A later renderer installation can call registration again and acquire it.
    if app.get_sub_app(bevy::render::RenderApp).is_some()
        && app.world().resource::<Registered>().shader.is_none()
    {
        let shader = app
            .world()
            .resource::<AssetServer>()
            .load("embedded://forge_engine/shaders/terrain_palette.wgsl");
        app.world_mut().resource_mut::<Registered>().shader = Some(shader);
    }
}
