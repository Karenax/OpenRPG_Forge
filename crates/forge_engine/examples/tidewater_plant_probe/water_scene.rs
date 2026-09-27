//! Opaque depth snapshot for the water pass, on the scene's existing device.
use bevy::{
    asset::RenderAssetUsages,
    core_pipeline::{
        core_3d::main_opaque_pass_3d,
        schedule::{Core3d, Core3dSystems},
    },
    pbr::main_transmissive_pass_3d,
    prelude::*,
    render::{
        RenderApp,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        render_asset::RenderAssets,
        render_resource::{
            Extent3d, TextureAspect, TextureDimension, TextureFormat, TextureUsages,
        },
        renderer::{RenderContext, ViewQuery},
        texture::GpuImage,
        view::ViewDepthTexture,
    },
};

#[derive(Resource, Clone, ExtractResource)]
pub struct SceneDepth(pub Handle<Image>);

pub fn install(app: &mut App) {
    let mut depth = Image::new_uninit(
        Extent3d {
            width: 1920,
            height: 1080,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        TextureFormat::Depth32Float,
        RenderAssetUsages::default(),
    );
    depth.texture_descriptor.usage = TextureUsages::COPY_DST | TextureUsages::TEXTURE_BINDING;
    let handle = app.world_mut().resource_mut::<Assets<Image>>().add(depth);
    app.insert_resource(SceneDepth(handle))
        .add_plugins(ExtractResourcePlugin::<SceneDepth>::default());
    app.sub_app_mut(RenderApp).add_systems(
        Core3d,
        copy_opaque_depth
            .after(main_opaque_pass_3d)
            .before(main_transmissive_pass_3d)
            .in_set(Core3dSystems::MainPass),
    );
}

fn copy_opaque_depth(
    view: ViewQuery<&ViewDepthTexture>,
    target: Res<SceneDepth>,
    images: Res<RenderAssets<GpuImage>>,
    mut context: RenderContext,
) {
    let Some(destination) = images.get(&target.0) else {
        return;
    };
    let source = view.into_inner();
    assert_eq!(
        source.texture.size(),
        destination.texture.size(),
        "fixed water capture size"
    );
    let mut source_copy = source.texture.as_image_copy();
    let mut target_copy = destination.texture.as_image_copy();
    source_copy.aspect = TextureAspect::DepthOnly;
    target_copy.aspect = TextureAspect::DepthOnly;
    context.command_encoder().copy_texture_to_texture(
        source_copy,
        target_copy,
        destination.texture.size(),
    );
}
