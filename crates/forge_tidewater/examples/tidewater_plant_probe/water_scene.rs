//! Bind the Tidewater water materials to the common opaque scene depth.
use bevy::{
    asset::{AssetEventSystems, AssetId},
    camera::CameraUpdateSystems,
    image::ToExtents,
    pbr::MeshMaterial3d,
    prelude::*,
    render::{
        Render, RenderApp, RenderSystems,
        camera::ExtractedCamera,
        erased_render_asset::prepare_erased_assets,
        render_asset::{RenderAssets, prepare_assets},
        render_resource::TextureFormat,
        texture::GpuImage,
    },
};

pub use forge_engine::source_scene_depth::SceneDepth;

#[derive(Resource, Default)]
struct BoundDepth(Option<(AssetId<Image>, UVec2)>);

pub fn install(app: &mut App) -> Result<(), String> {
    forge_engine::source_scene_depth::register(app)?;
    app.init_resource::<BoundDepth>().add_systems(
        PostUpdate,
        resize_scene_depth
            .after(CameraUpdateSystems)
            .before(AssetEventSystems),
    );
    // Prepare a replacement GPU view before dependent material bind groups.
    app.sub_app_mut(RenderApp).add_systems(
        Render,
        validate_prepared_depth
            .in_set(RenderSystems::PrepareAssets)
            .after(prepare_assets::<GpuImage>)
            .before(prepare_erased_assets::<MeshMaterial3d<super::water_optics::WaterMaterial>>)
            .before(
                prepare_erased_assets::<
                    MeshMaterial3d<forge_engine::source_ocean_geometry::DiagnosticMaterial>,
                >,
            )
            .before(prepare_erased_assets::<MeshMaterial3d<super::spray_sprites::SprayMaterial>>),
    );
    Ok(())
}

fn validate_prepared_depth(
    cameras: Query<
        &ExtractedCamera,
        (
            With<Camera3d>,
            Without<forge_engine::source_refraction::CaptureCamera>,
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

fn resize_scene_depth(world: &mut World) {
    let camera = {
        let mut cameras = world.query_filtered::<(Entity, &Camera), (
            With<Camera3d>,
            Without<forge_engine::source_refraction::CaptureCamera>,
        )>();
        let mut active = cameras.iter(world).filter(|(_, camera)| camera.is_active);
        let Some((camera, _)) = active.next() else {
            return;
        };
        assert!(
            active.next().is_none(),
            "water depth requires a unique main camera"
        );
        camera
    };
    let Some(size) = forge_engine::source_scene_depth::resize(world, camera)
        .expect("resize common water scene depth")
    else {
        return;
    };
    let binding = (world.resource::<SceneDepth>().0.id(), size);
    if world.resource::<BoundDepth>().0 == Some(binding) {
        return;
    }
    // Another consumer may already have resized the common image. Track the
    // dimensions last bound by water, not only the current image dimensions.
    // Assets::iter_mut emits Modified, recreating dependent GPU bind groups.
    for (_, material) in world
        .resource_mut::<Assets<super::water_optics::WaterMaterial>>()
        .iter_mut()
    {
        forge_engine::source_water_frame::write_resolution(&mut material.frame, size);
    }
    for _ in world
        .resource_mut::<Assets<forge_engine::source_ocean_geometry::DiagnosticMaterial>>()
        .iter_mut()
    {}
    if let Some(mut spray) = world.get_resource_mut::<Assets<super::spray_sprites::SprayMaterial>>()
    {
        for (_, material) in spray.iter_mut() {
            forge_engine::source_water_frame::write_resolution(&mut material.frame, size);
        }
    }
    world.resource_mut::<BoundDepth>().0 = Some(binding);
}
