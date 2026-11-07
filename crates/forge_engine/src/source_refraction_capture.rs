// SPDX-License-Identifier: MIT
// Adapted from Tidewater RefractionPass.js at 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
//! Derived half-resolution HDR underwater capture, on Bevy's existing renderer.
//! It owns no authoring data or physics; its meshes reference the current scene.
use bevy::{
    asset::RenderAssetUsages,
    camera::{Hdr, RenderTarget, visibility::RenderLayers},
    core_pipeline::{
        core_3d::main_opaque_pass_3d,
        schedule::{Core3d, Core3dSystems},
        tonemapping::Tonemapping,
    },
    pbr::{ExtendedMaterial, MaterialExtension},
    prelude::*,
    render::{
        RenderApp,
        extract_component::ExtractComponentPlugin,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        render_asset::RenderAssets,
        render_resource::{
            AsBindGroup, Extent3d, TextureAspect, TextureDimension, TextureFormat, TextureUsages,
        },
        renderer::{RenderContext, ViewQuery},
        texture::GpuImage,
        view::ViewDepthTexture,
    },
    shader::ShaderRef,
};
use std::sync::{
    Arc,
    atomic::{AtomicU64, Ordering},
};

mod mirror;
mod observation;
pub use observation::{GpuObservation, observe_while_held};
pub mod taa_mirror;

const LAYER: usize = 17;
const TERRAIN_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("9731fb6b-5ce5-4440-9124-f58ae1e503de");
const SURFACE_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("53519c03-ea67-444a-adf5-7b198d46ebd9");

use crate::source_refraction::{CaptureCamera, CaptureSource, Synchronize};

#[derive(Resource, Clone, ExtractResource)]
pub struct Targets {
    pub color: Handle<Image>,
    pub depth: Handle<Image>,
    pub encoded_frames: Arc<AtomicU64>,
}

#[derive(Resource)]
pub struct Control {
    pub enabled: bool,
}
impl Default for Control {
    fn default() -> Self {
        Self { enabled: true }
    }
}

/// Per-frame synchronization counts; these do not establish GPU completion.
#[derive(Resource, Default, Clone, serde::Serialize)]
pub struct Activity {
    pub demanded: bool,
    pub material_updates: u32,
    pub renderable_updates: u32,
    pub suspended_frames: u64,
}

/// CPU state and encoded-command count, not GPU texture-readback evidence.
#[derive(Resource, Default, serde::Serialize)]
pub struct Observation {
    pub size: [u32; 2],
    pub village_parts: usize,
    pub terrain_parts: usize,
    pub source_camera: [f32; 16],
    pub capture_camera: [f32; 16],
    pub ready: bool,
    pub error: Option<String>,
}

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct TerrainExtension {
    #[uniform(200, visibility(fragment))]
    sea: Vec4,
}
impl MaterialExtension for TerrainExtension {
    fn fragment_shader() -> ShaderRef {
        TERRAIN_SHADER.into()
    }
    fn enable_prepass() -> bool {
        false
    }
    fn enable_shadows() -> bool {
        false
    }
}

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
pub struct VillageExtension {
    #[uniform(200, visibility(fragment))]
    sea: Vec4,
}
impl MaterialExtension for VillageExtension {
    fn fragment_shader() -> ShaderRef {
        SURFACE_SHADER.into()
    }
    fn enable_prepass() -> bool {
        false
    }
    fn enable_shadows() -> bool {
        false
    }
}

type TerrainCapture = ExtendedMaterial<crate::source_terrain::lod::LodMaterial, TerrainExtension>;
type VillageCapture =
    ExtendedMaterial<crate::source_surface::SourceSurfaceMaterial, VillageExtension>;

pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Targets>() {
        return Ok(());
    }
    if app.get_sub_app(RenderApp).is_none() || !app.world().contains_resource::<Assets<Image>>() {
        return Err("Refraction requires initialized Bevy rendering".into());
    }
    let mut images = app.world_mut().resource_mut::<Assets<Image>>();
    let color = images.add(target_image(UVec2::ONE, TextureFormat::Rgba16Float));
    let depth = images.add(target_image(UVec2::ONE, TextureFormat::Depth32Float));
    app.insert_resource(Targets {
        color,
        depth,
        encoded_frames: Arc::default(),
    })
    .init_resource::<Control>()
    .init_resource::<Observation>()
    .init_resource::<Activity>()
    .init_resource::<mirror::State>()
    .add_plugins((
        MaterialPlugin::<TerrainCapture>::default(),
        MaterialPlugin::<VillageCapture>::default(),
        ExtractResourcePlugin::<Targets>::default(),
        ExtractComponentPlugin::<CaptureCamera>::default(),
        ExtractComponentPlugin::<CaptureSource>::default(),
    ))
    // This follows propagation and bounds; the cloned render entities have no
    // parents. Their GlobalTransform is set explicitly before extraction.
    .add_systems(
        PostUpdate,
        mirror::synchronize
            .in_set(Synchronize)
            .after(bevy::camera::visibility::VisibilitySystems::CheckVisibility),
    );
    app.sub_app_mut(RenderApp).add_systems(
        Core3d,
        copy_depth
            .after(main_opaque_pass_3d)
            .in_set(Core3dSystems::MainPass),
    );
    taa_mirror::install(app);
    Ok(())
}

/// Select the adapter's scene camera. Synchronization validates its current
/// camera, projection, viewport and terrain before enabling the capture.
pub fn select_camera(world: &mut World, camera: Entity) {
    if let Some(mut state) = world.get_resource_mut::<mirror::State>() {
        state.source = Some(camera);
    }
}

/// Capture-only geometry belongs to this renderer, not the authored main view.
#[must_use]
pub fn owns_mirror(world: &World, entity: Entity) -> bool {
    world.contains_resource::<mirror::State>()
        && world.get::<mirror::CaptureMirror>(entity).is_some()
        && world.get::<RenderLayers>(entity) == Some(&RenderLayers::layer(LAYER))
}

fn size(full: UVec2) -> UVec2 {
    // JS Math.round(size * 0.5), including odd canvas dimensions.
    UVec2::new(full.x.div_ceil(2).max(1), full.y.div_ceil(2).max(1))
}

fn target_image(size: UVec2, format: TextureFormat) -> Image {
    let mut image = Image::new_uninit(
        Extent3d {
            width: size.x,
            height: size.y,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        format,
        RenderAssetUsages::default(),
    );
    image.texture_descriptor.usage = TextureUsages::TEXTURE_BINDING
        | TextureUsages::COPY_SRC
        | if format == TextureFormat::Depth32Float {
            TextureUsages::COPY_DST
        } else {
            TextureUsages::RENDER_ATTACHMENT
        };
    image
}

fn spawn_camera(world: &mut World, target: Handle<Image>) -> Entity {
    world
        .spawn((
            CaptureCamera,
            Name::new("Source underwater refraction capture"),
            Camera3d {
                depth_texture_usages: (TextureUsages::RENDER_ATTACHMENT | TextureUsages::COPY_SRC)
                    .into(),
                ..default()
            },
            Camera {
                order: -10,
                is_active: false,
                clear_color: ClearColorConfig::Custom(Color::NONE),
                ..default()
            },
            RenderTarget::Image(target.into()),
            Hdr,
            Msaa::Off,
            Tonemapping::None,
            RenderLayers::layer(LAYER),
        ))
        .id()
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn copy_depth(
    view: ViewQuery<(&ViewDepthTexture, Option<&CaptureCamera>)>,
    targets: Res<Targets>,
    images: Res<RenderAssets<GpuImage>>,
    mut context: RenderContext,
) {
    let (source, capture) = view.into_inner();
    if capture.is_none() {
        return;
    }
    let Some(destination) = images.get(&targets.depth) else {
        return;
    };
    if source.texture.size() != destination.texture.size() || source.texture.sample_count() != 1 {
        return;
    }
    let mut from = source.texture.as_image_copy();
    let mut to = destination.texture.as_image_copy();
    from.aspect = TextureAspect::DepthOnly;
    to.aspect = TextureAspect::DepthOnly;
    context
        .command_encoder()
        .copy_texture_to_texture(from, to, destination.texture.size());
    targets.encoded_frames.fetch_add(1, Ordering::Release);
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn odd_resize_matches_source_and_targets_keep_coverage_and_reversed_depth() {
        assert_eq!(size(UVec2::new(1919, 1079)), UVec2::new(960, 540));
        assert_eq!(size(UVec2::ZERO), UVec2::ONE);
        let color = target_image(UVec2::new(960, 540), TextureFormat::Rgba16Float);
        let depth = target_image(UVec2::new(960, 540), TextureFormat::Depth32Float);
        assert!(
            color
                .texture_descriptor
                .usage
                .contains(TextureUsages::RENDER_ATTACHMENT | TextureUsages::TEXTURE_BINDING)
        );
        assert!(
            depth
                .texture_descriptor
                .usage
                .contains(TextureUsages::COPY_DST | TextureUsages::TEXTURE_BINDING)
        );
        assert_eq!(depth.texture_descriptor.size.depth_or_array_layers, 1);
    }
}

#[cfg(all(test, feature = "physics-rapier"))]
mod common_camera_filter_tests {
    use super::*;
    #[test]
    fn refraction_view_requires_the_common_offscreen_identity() {
        let mut world = World::new();
        let visible = world.spawn(Camera3d::default()).id();
        let capture = world.spawn((Camera3d::default(), CaptureCamera)).id();
        assert!(
            world
                .get::<crate::scene_camera::OffscreenSceneCamera>(capture)
                .is_some()
        );
        let mut views =
            world.query_filtered::<Entity, crate::source_sky::preview::SceneCameraFilter>();
        assert_eq!(views.single(&world).unwrap(), visible);
    }
}
