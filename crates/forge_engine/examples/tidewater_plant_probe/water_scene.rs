//! Opaque depth snapshot for the water pass, on the scene's existing device.
use bevy::{
    asset::{AssetEventSystems, RenderAssetUsages},
    camera::CameraUpdateSystems,
    core_pipeline::{
        core_3d::main_opaque_pass_3d,
        schedule::{Core3d, Core3dSystems},
    },
    image::ToExtents,
    pbr::{MeshMaterial3d, main_transmissive_pass_3d},
    prelude::*,
    render::{
        Render, RenderApp, RenderSystems,
        camera::ExtractedCamera,
        erased_render_asset::prepare_erased_assets,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        render_asset::{RenderAssets, prepare_assets},
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
        .add_plugins(ExtractResourcePlugin::<SceneDepth>::default())
        .add_systems(
            PostUpdate,
            resize_scene_depth
                .after(CameraUpdateSystems)
                .before(AssetEventSystems),
        );
    // A resized Image creates a new GPU texture view. Prepare it before the
    // existing material systems bind that view; never add duplicate systems.
    app.sub_app_mut(RenderApp).add_systems(
        Render,
        validate_prepared_depth
            .in_set(RenderSystems::PrepareAssets)
            .after(prepare_assets::<GpuImage>)
            .before(prepare_erased_assets::<MeshMaterial3d<super::water_optics::WaterMaterial>>)
            .before(prepare_erased_assets::<MeshMaterial3d<super::ocean_geometry::OceanGeometryMaterial>>)
            .before(prepare_erased_assets::<MeshMaterial3d<super::spray_sprites::SprayMaterial>>),
    );
    app.sub_app_mut(RenderApp).add_systems(
        Core3d,
        copy_opaque_depth
            .after(main_opaque_pass_3d)
            .before(main_transmissive_pass_3d)
            .in_set(Core3dSystems::MainPass),
    );
}

fn validate_prepared_depth(
    cameras: Query<
        &ExtractedCamera,
        (
            With<Camera3d>,
            Without<forge_engine::tidewater_refraction::CaptureCamera>,
        ),
    >,
    target: Res<SceneDepth>,
    images: Res<RenderAssets<GpuImage>>,
) {
    // Inactive cameras are not extracted. The derived capture is excluded.
    let mut active = cameras.iter();
    let Some(camera) = active.next() else {
        return;
    };
    assert!(
        active.next().is_none(),
        "water depth requires a unique extracted main camera"
    );
    let Some(size) = camera.physical_target_size else {
        return;
    };
    let Some(image) = images.get(&target.0) else {
        return;
    };
    assert_eq!(
        image.texture.size(),
        size.to_extents(),
        "prepared water depth must match extracted target before material binding"
    );
    assert_eq!(image.texture.format(), TextureFormat::Depth32Float);
}

fn resize_image(image: &mut Image, size: UVec2) -> bool {
    if size.min_element() == 0 || image.texture_descriptor.size == size.to_extents() {
        return false;
    }
    // Do not retain a partial copy of the old depth: opaque rendering fills the
    // whole replacement before any water/spray sampling in this actual frame.
    image.resize(size.to_extents());
    true
}

fn resize_scene_depth(
    cameras: Query<
        &Camera,
        (
            With<Camera3d>,
            Without<forge_engine::tidewater_refraction::CaptureCamera>,
        ),
    >,
    target: Res<SceneDepth>,
    mut images: ResMut<Assets<Image>>,
    mut water: ResMut<Assets<super::water_optics::WaterMaterial>>,
    mut geometry: ResMut<Assets<super::ocean_geometry::OceanGeometryMaterial>>,
    mut spray: Option<ResMut<Assets<super::spray_sprites::SprayMaterial>>>,
) {
    let mut active = cameras.iter().filter(|camera| camera.is_active);
    let Some(camera) = active.next() else {
        return;
    };
    assert!(
        active.next().is_none(),
        "water depth requires a unique main camera"
    );
    let Some(size) = camera.physical_target_size() else {
        return;
    };
    if size.min_element() == 0
        || images
            .get(&target.0)
            .expect("water scene depth image")
            .texture_descriptor
            .size
            == size.to_extents()
    {
        return;
    }
    let image = images.get_mut(&target.0).expect("water scene depth image");
    if !resize_image(image.into_inner(), size) {
        return;
    }
    let resolution = size.as_vec2();
    let inverse = resolution.recip();
    let resolution_words = UVec4::new(
        resolution.x.to_bits(),
        resolution.y.to_bits(),
        inverse.x.to_bits(),
        inverse.y.to_bits(),
    );
    // Assets::iter_mut emits Modified: recreate every dependent bind group in
    // the same extraction, including geometry-only and optional spray modes.
    for (_, material) in water.iter_mut() {
        material.frame[34] = resolution_words;
        material.frame[35].x = resolution.x.to_bits();
        material.frame[35].y = resolution.y.to_bits();
    }
    for _ in geometry.iter_mut() {}
    if let Some(spray) = &mut spray {
        for (_, material) in spray.iter_mut() {
            material.frame[34] = resolution_words;
            material.frame[35].x = resolution.x.to_bits();
            material.frame[35].y = resolution.y.to_bits();
        }
    }
}

fn copy_opaque_depth(
    view: ViewQuery<(
        &ViewDepthTexture,
        Option<&forge_engine::tidewater_refraction::CaptureCamera>,
    )>,
    target: Res<SceneDepth>,
    images: Res<RenderAssets<GpuImage>>,
    mut context: RenderContext,
) {
    let (source, capture) = view.into_inner();
    if capture.is_some() {
        return;
    }
    let Some(destination) = images.get(&target.0) else {
        return;
    };
    assert_eq!(
        source.texture.size(),
        destination.texture.size(),
        "water depth must cover the actual full render target"
    );
    assert_eq!(
        source.texture.sample_count(),
        destination.texture.sample_count(),
        "water depth sample count"
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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn image_resize_reuses_handle_and_preserves_complete_depth_descriptor() {
        let mut image = Image::new_uninit(
            UVec2::new(1920, 1080).to_extents(),
            TextureDimension::D2,
            TextureFormat::Depth32Float,
            RenderAssetUsages::default(),
        );
        image.texture_descriptor.usage = TextureUsages::COPY_DST | TextureUsages::TEXTURE_BINDING;
        let mut images = Assets::<Image>::default();
        let handle = images.add(image);
        let bound = handle.clone();
        assert!(resize_image(
            images.get_mut(&handle).unwrap().into_inner(),
            UVec2::new(1919, 1079)
        ));
        let actual = images.get(&bound).unwrap();
        assert_eq!(
            actual.texture_descriptor.size,
            UVec2::new(1919, 1079).to_extents()
        );
        assert_eq!(
            actual.texture_descriptor.format,
            TextureFormat::Depth32Float
        );
        assert_eq!(
            actual.texture_descriptor.usage,
            TextureUsages::COPY_DST | TextureUsages::TEXTURE_BINDING
        );
        assert_eq!(actual.texture_descriptor.sample_count, 1);
        assert!(!actual.copy_on_resize);
        assert!(actual.data.is_none());
        assert_eq!(handle.id(), bound.id());
    }

    #[test]
    fn unchanged_or_unavailable_size_does_not_replace_depth() {
        let size = UVec2::new(1919, 1079);
        let mut image = Image::new_uninit(
            size.to_extents(),
            TextureDimension::D2,
            TextureFormat::Depth32Float,
            RenderAssetUsages::default(),
        );
        assert!(!resize_image(&mut image, size));
        assert!(!resize_image(&mut image, UVec2::new(0, 1079)));
        assert_eq!(image.texture_descriptor.size, size.to_extents());
        assert!(resize_image(&mut image, UVec2::new(1920, 1080)));
    }
}
