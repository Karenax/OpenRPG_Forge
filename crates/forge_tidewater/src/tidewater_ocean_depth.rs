// SPDX-License-Identifier: MIT
// Adapted from the pinned Tidewater native water probe, revision 1438b1ab.
//! Opaque scene depth for the imported ocean's transmissive material.
//! The destination follows the actual Studio canvas size instead of assuming a fixed window.
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
use std::sync::{Arc, Mutex};

#[derive(Resource, Clone, ExtractResource)]
pub struct SceneDepth(pub Handle<Image>);

/// Receipt recorded only after encoding the shared opaque-depth copy.
#[derive(Clone, Debug)]
pub struct DepthCopy {
    pub image: Handle<Image>,
    pub tag: crate::tidewater_temporal::frame::FrameTag,
    pub size: UVec2,
    pub history_epoch: u64,
    pub unjittered_clip_from_world: Mat4,
}

/// Latest tagged opaque copy into `SceneDepth`. The shared receipt is invalidated
/// by resize and by any untagged view overwriting the global depth texture.
/// This is command-encoding evidence, never a GPU completion fence.
#[derive(Resource, Clone, Default, ExtractResource)]
pub struct OpaqueDepthHistory(pub Arc<Mutex<Option<DepthCopy>>>);

pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<SceneDepth>() {
        return Ok(());
    }
    if !app.world().contains_resource::<Assets<Image>>() || app.get_sub_app(RenderApp).is_none() {
        return Err("Scene depth requires initialized Bevy rendering".into());
    }
    let handle = app
        .world_mut()
        .resource_mut::<Assets<Image>>()
        .add(image(1, 1));
    app.insert_resource(SceneDepth(handle))
        .insert_resource(OpaqueDepthHistory::default())
        .add_plugins((
            ExtractResourcePlugin::<SceneDepth>::default(),
            ExtractResourcePlugin::<OpaqueDepthHistory>::default(),
        ));
    app.sub_app_mut(RenderApp).add_systems(
        Core3d,
        copy_opaque_depth
            .after(main_opaque_pass_3d)
            .before(main_transmissive_pass_3d)
            .in_set(Core3dSystems::MainPass),
    );
    Ok(())
}

/// Reuse the material handle while recreating only its derived GPU target on canvas resize.
pub fn resize(world: &mut World, camera: Entity) -> Result<Option<UVec2>, String> {
    let Some(size) = world
        .get::<Camera>(camera)
        .and_then(Camera::physical_target_size)
    else {
        return Ok(None);
    };
    if size.x == 0 || size.y == 0 {
        return Ok(None);
    }
    let handle = world
        .get_resource::<SceneDepth>()
        .ok_or("Scene depth not registered")?
        .0
        .clone();
    let current = world
        .resource::<Assets<Image>>()
        .get(&handle)
        .map(|image| image.texture_descriptor.size);
    if current.map(|extent| (extent.width, extent.height)) != Some((size.x, size.y)) {
        *world
            .resource::<OpaqueDepthHistory>()
            .0
            .lock()
            .map_err(|_| "Opaque-depth history lock poisoned")? = None;
        world
            .resource_mut::<Assets<Image>>()
            .insert(handle.id(), image(size.x, size.y))
            .map_err(|error| error.to_string())?;
    }
    Ok(Some(size))
}

fn image(width: u32, height: u32) -> Image {
    let mut image = Image::new_uninit(
        Extent3d {
            width,
            height,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        TextureFormat::Depth32Float,
        RenderAssetUsages::default(),
    );
    image.texture_descriptor.usage = TextureUsages::COPY_DST | TextureUsages::TEXTURE_BINDING;
    image
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy render systems require owned system parameter wrappers."
)]
#[expect(
    clippy::type_complexity,
    reason = "Bevy view queries declare their component tuple and filters in the SystemParam signature"
)]
fn copy_opaque_depth(
    view: ViewQuery<(
        &ViewDepthTexture,
        Option<&crate::tidewater_refraction::CaptureCamera>,
        Option<&crate::tidewater_temporal::frame::ViewPacket>,
        Option<&crate::tidewater_stall::contact_camera::ContactView>,
    )>,
    target: Res<SceneDepth>,
    images: Res<RenderAssets<GpuImage>>,
    history: Res<OpaqueDepthHistory>,
    independent: Option<Res<crate::tidewater_stall::contact_camera::History>>,
    mut context: RenderContext,
) {
    let Some(destination) = images.get(&target.0) else {
        return;
    };
    let (source, refraction, packet, contact) = view.into_inner();
    if refraction.is_some() {
        return;
    }
    if source.texture.size() != destination.texture.size()
        || source.texture.sample_count() != destination.texture.sample_count()
        || !source.texture.usage().contains(TextureUsages::COPY_SRC)
    {
        // Resize and MSAA changes can reach the view before the replacement
        // texture is extracted. Never submit an invalid WebGPU depth copy.
        return;
    }
    let mut source_copy = source.texture.as_image_copy();
    let mut target_copy = destination.texture.as_image_copy();
    source_copy.aspect = TextureAspect::DepthOnly;
    target_copy.aspect = TextureAspect::DepthOnly;
    context.command_encoder().copy_texture_to_texture(
        source_copy,
        target_copy,
        destination.texture.size(),
    );
    if let Ok(mut last) = history.0.lock() {
        let size = UVec2::new(
            destination.texture.size().width,
            destination.texture.size().height,
        );
        *last = packet
            .filter(|packet| packet.frame.input_size == size)
            .map(|packet| DepthCopy {
                image: target.0.clone(),
                tag: packet.tag,
                size,
                history_epoch: packet.frame.history_epoch,
                unjittered_clip_from_world: packet.frame.unjittered_clip_from_world,
            });
    }
    if let Some(independent) = independent
        && let Ok(mut last) = independent.0.lock()
    {
        *last = contact
            .filter(|contact| {
                packet.is_none()
                    && contact.0.input_size
                        == UVec2::new(
                            destination.texture.size().width,
                            destination.texture.size().height,
                        )
            })
            .map(|contact| DepthCopy {
                image: target.0.clone(),
                tag: contact.0.tag,
                size: contact.0.input_size,
                history_epoch: contact.0.history_epoch,
                unjittered_clip_from_world: contact.0.unjittered_clip_from_world,
            });
    }
}
