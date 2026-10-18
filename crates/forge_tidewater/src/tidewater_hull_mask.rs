// SPDX-License-Identifier: MIT
// Adapted from Tidewater SceneRenderer.js at 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
//! Dedicated closed-hull distance pass, shared by beauty and auxiliary water.
//! Explicit volumes are scene data. Their presence is not a complete scene census.
use bevy::{
    asset::RenderAssetUsages,
    camera::{
        MainPassResolutionOverride,
        primitives::{Frustum, Sphere},
    },
    core_pipeline::{
        core_3d::main_opaque_pass_3d,
        schedule::{Core3d, Core3dSystems},
    },
    mesh::VertexBufferLayout,
    pbr::main_transmissive_pass_3d,
    prelude::*,
    render::{
        Render, RenderApp, RenderStartup, RenderSystems,
        camera::TemporalJitter,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        render_asset::RenderAssets,
        render_resource::{
            BindGroup, BindGroupEntries, BindGroupLayoutDescriptor, BindGroupLayoutEntries, Buffer,
            BufferInitDescriptor, BufferUsages, CachedRenderPipelineId, ColorTargetState,
            ColorWrites, CompareFunction, DepthStencilState, Extent3d, FragmentState, IndexFormat,
            LoadOp, Operations, PipelineCache, PrimitiveState, RenderPassColorAttachment,
            RenderPassDepthStencilAttachment, RenderPassDescriptor, RenderPipelineDescriptor,
            ShaderStages, ShaderType, StoreOp, TextureDimension, TextureFormat, TextureUsages,
            UniformBuffer, VertexFormat, VertexState, VertexStepMode,
            binding_types::uniform_buffer,
        },
        renderer::{RenderContext, RenderDevice, RenderQueue, ViewQuery},
        storage::{GpuShaderBuffer, ShaderBuffer},
        sync_world::MainEntity,
        texture::GpuImage,
        view::{ExtractedView, ViewUniform, ViewUniformOffset, ViewUniforms},
    },
};
use std::{
    collections::HashMap,
    sync::{Arc, Mutex},
};

#[path = "tidewater_hull_mask/geometry.rs"]
mod geometry;
pub use geometry::{Geometry, HullVolume};
#[path = "tidewater_hull_mask/shaders.rs"]
mod shaders;
use crate::tidewater_temporal::frame::{FrameTag, ViewPacket};
pub use shaders::water_shader_source;

#[derive(Resource, Clone, ExtractResource)]
pub struct Targets {
    pub distance: Handle<Image>,
    pub depth: Handle<Image>,
    /// Stable storage allocation, reset in `RenderWorld` before selected-view work.
    pub readiness: Handle<ShaderBuffer>,
}

#[derive(Resource, Default)]
struct Selection(Option<Entity>);

#[derive(Clone)]
struct Volume {
    entity: Entity,
    geometry: Arc<Geometry>,
    world_from_local: Mat4,
}

#[derive(Resource, Clone, Default, ExtractResource)]
struct Snapshot {
    camera: Option<Entity>,
    size: UVec2,
    generation: u64,
    revision: u64,
    volumes: Vec<Volume>,
    errors: Vec<String>,
}

/// CPU command-encoding observation. This is not a GPU fence or a scene-completeness claim.
#[derive(Clone, Debug)]
pub struct Encoding {
    pub view: Entity,
    pub render_view: Entity,
    pub tag: Option<FrameTag>,
    pub generation: u64,
    pub revision: u64,
    pub size: UVec2,
    pub registered: u32,
    pub active: u32,
    pub encoded: u32,
    pub cleared: bool,
    pub flag_initialized: bool,
    pub active_published: bool,
    pub errors: Vec<String>,
}
impl Encoding {
    #[must_use]
    pub fn complete(&self) -> bool {
        self.generation != 0
            && self.cleared
            && self.flag_initialized
            && self.active_published == (self.active > 0)
            && self.active <= self.registered
            && self.encoded == self.active
            && self.errors.is_empty()
    }
    /// Required-volume mode accepts zero ACTIVE hulls, but never invents an empty
    /// scene assertion from an empty registration list. The adapter owns its census.
    #[must_use]
    pub fn matches_required_frame(&self, tag: FrameTag, size: UVec2) -> bool {
        self.complete()
            && self.registered > 0
            && self.view == tag.view
            && self.tag == Some(tag)
            && self.size == size
    }
}

#[derive(Resource, Clone, Default)]
pub struct Observation(Arc<Mutex<Option<Encoding>>>);
impl Observation {
    #[must_use]
    pub fn snapshot(&self) -> Option<Encoding> {
        self.0.lock().ok()?.clone()
    }
    fn record(&self, encoding: Option<Encoding>) {
        if let Ok(mut value) = self.0.lock() {
            *value = encoding;
        }
    }
}

/// Runs after actual camera sizing, propagation and source jitter preparation.
#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct Synchronize;

/// Installing rendering does not install a temporal scene assertion. Studio must
/// still provide an audited inventory, including the expected canonical boat.
pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Targets>() {
        return Ok(());
    }
    if app.get_sub_app(RenderApp).is_none()
        || !app.world().contains_resource::<Assets<Image>>()
        || !app.world().contains_resource::<Assets<Shader>>()
        || !app.world().contains_resource::<Assets<ShaderBuffer>>()
    {
        return Err("Hull masks require initialized Bevy rendering".into());
    }
    let mut images = app.world_mut().resource_mut::<Assets<Image>>();
    let distance = images.add(image(UVec2::ONE, TextureFormat::R16Float));
    let depth = images.add(image(UVec2::ONE, TextureFormat::Depth32Float));
    let mut flag = ShaderBuffer::from(Vec4::ZERO);
    flag.buffer_description.usage |= BufferUsages::COPY_SRC;
    let readiness = app
        .world_mut()
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(flag);
    let shader = app
        .world_mut()
        .resource_mut::<Assets<Shader>>()
        .add(Shader::from_wgsl(
            include_str!("tidewater_hull_mask/distance.wgsl"),
            "forge://tidewater/hull-distance.wgsl",
        ));
    let observation = Observation::default();
    app.insert_resource(Targets {
        distance,
        depth,
        readiness,
    })
    .insert_resource(observation.clone())
    .init_resource::<Selection>()
    .init_resource::<Snapshot>()
    .add_plugins((
        ExtractResourcePlugin::<Targets>::default(),
        ExtractResourcePlugin::<Snapshot>::default(),
    ))
    .add_systems(
        PostUpdate,
        synchronize
            .in_set(Synchronize)
            .after(bevy::camera::CameraUpdateSystems)
            .after(bevy::transform::TransformSystems::Propagate)
            .after(crate::tidewater_temporal::FrameSystems::Jitter)
            .before(crate::tidewater_refraction::Synchronize),
    );
    app.sub_app_mut(RenderApp)
        .insert_resource(ShaderSource(shader))
        .insert_resource(observation)
        .init_resource::<Prepared>()
        .init_resource::<GeometryCache>()
        .add_systems(RenderStartup, initialize)
        .add_systems(Render, prepare.in_set(RenderSystems::PrepareBindGroups))
        .add_systems(
            Core3d,
            encode
                .after(main_opaque_pass_3d)
                .before(main_transmissive_pass_3d)
                .in_set(Core3dSystems::MainPass),
        );
    Ok(())
}

pub fn select_camera(world: &mut World, camera: Option<Entity>) {
    if let Some(mut selection) = world.get_resource_mut::<Selection>() {
        selection.0 = camera;
    }
}

fn image(size: UVec2, format: TextureFormat) -> Image {
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
    image.texture_descriptor.usage =
        TextureUsages::RENDER_ATTACHMENT | TextureUsages::TEXTURE_BINDING | TextureUsages::COPY_SRC;
    image
}

fn synchronize(world: &mut World) {
    let selected = world.resource::<Selection>().0;
    let camera =
        selected.filter(|entity| world.get::<Camera>(*entity).is_some_and(|c| c.is_active));
    let size = camera
        .and_then(|e| world.get::<Camera>(e)?.physical_viewport_size())
        .unwrap_or(UVec2::ONE)
        .max(UVec2::ONE);
    let previous = world.resource::<Snapshot>();
    let changed = previous.camera != camera || previous.size != size;
    let generation = if changed {
        previous
            .generation
            .checked_add(1)
            .expect("Hull allocation generation exhausted")
    } else {
        previous.generation
    };
    let revision = previous
        .revision
        .checked_add(1)
        .expect("Hull snapshot revision exhausted");
    let mut errors = Vec::new();
    if let Some(e) = camera {
        let c = world.get::<Camera>(e).expect("validated camera");
        if c.physical_target_size() != Some(size)
            || c.viewport
                .as_ref()
                .is_some_and(|v| v.physical_position != UVec2::ZERO)
            || world.get::<Msaa>(e).is_some_and(|m| m.samples() != 1)
        {
            errors.push("Hull distance requires full viewport and MSAA off".into());
        }
    }
    let mut query = world.query::<(Entity, &HullVolume, Option<&GlobalTransform>)>();
    let volumes = query
        .iter(world)
        .filter_map(|(entity, volume, transform)| {
            let Some(transform) = transform else {
                errors.push(format!(
                    "Registered hull {entity:?} has no propagated transform"
                ));
                return None;
            };
            Some(Volume {
                entity,
                geometry: volume.0.clone(),
                world_from_local: transform.to_matrix(),
            })
        })
        .collect();
    if changed {
        let targets = world.resource::<Targets>().clone();
        let mut images = world.resource_mut::<Assets<Image>>();
        for (handle, format) in [
            (targets.distance, TextureFormat::R16Float),
            (targets.depth, TextureFormat::Depth32Float),
        ] {
            if let Err(error) = images.insert(handle.id(), image(size, format)) {
                errors.push(error.to_string());
            }
        }
    }
    world.insert_resource(Snapshot {
        camera,
        size,
        generation,
        revision,
        volumes,
        errors,
    });
}

#[derive(Resource)]
struct ShaderSource(Handle<Shader>);
#[derive(Resource)]
struct Pipeline {
    id: CachedRenderPipelineId,
    view_layout: BindGroupLayoutDescriptor,
    object_layout: BindGroupLayoutDescriptor,
}
#[derive(Clone, Copy, ShaderType)]
struct ObjectUniform {
    world_from_local: Mat4,
}

#[expect(
    clippy::needless_pass_by_value,
    reason = "Bevy system parameters must be owned SystemParam values, not borrowed Res wrappers"
)]
fn initialize(mut commands: Commands, source: Res<ShaderSource>, cache: Res<PipelineCache>) {
    let view_layout = BindGroupLayoutDescriptor::new(
        "Hull actual view",
        &BindGroupLayoutEntries::single(
            ShaderStages::VERTEX_FRAGMENT,
            uniform_buffer::<ViewUniform>(true),
        ),
    );
    let object_layout = BindGroupLayoutDescriptor::new(
        "Hull object",
        &BindGroupLayoutEntries::single(
            ShaderStages::VERTEX,
            uniform_buffer::<ObjectUniform>(false),
        ),
    );
    let id = cache.queue_render_pipeline(RenderPipelineDescriptor {
        label: Some("Tidewater closed hull distance".into()),
        layout: vec![view_layout.clone(), object_layout.clone()],
        vertex: VertexState {
            shader: source.0.clone(),
            entry_point: Some("vertex".into()),
            buffers: vec![VertexBufferLayout::from_vertex_formats(
                VertexStepMode::Vertex,
                [VertexFormat::Float32x3],
            )],
            ..default()
        },
        fragment: Some(FragmentState {
            shader: source.0.clone(),
            entry_point: Some("fragment".into()),
            targets: vec![Some(ColorTargetState {
                format: TextureFormat::R16Float,
                blend: None,
                write_mask: ColorWrites::ALL,
            })],
            ..default()
        }),
        primitive: PrimitiveState {
            cull_mode: None,
            ..default()
        },
        depth_stencil: Some(DepthStencilState {
            format: TextureFormat::Depth32Float,
            depth_write_enabled: Some(true),
            depth_compare: Some(CompareFunction::GreaterEqual),
            stencil: default(),
            bias: default(),
        }),
        ..default()
    });
    commands.insert_resource(Pipeline {
        id,
        view_layout,
        object_layout,
    });
}

struct CachedGeometry {
    source: Arc<Geometry>,
    vertex: Buffer,
    index: Buffer,
}
#[derive(Resource, Default)]
struct GeometryCache(HashMap<Entity, CachedGeometry>);
struct Draw {
    entity: Entity,
    group: BindGroup,
    _uniform: UniformBuffer<ObjectUniform>,
    indices: u32,
}
#[derive(Resource, Default)]
struct Prepared {
    view: Option<Entity>,
    view_group: Option<BindGroup>,
    draws: Vec<Draw>,
    encoding: Option<Encoding>,
}

#[allow(clippy::too_many_arguments)]
#[expect(
    clippy::needless_pass_by_value,
    reason = "Bevy system parameters must be owned SystemParam values, not borrowed Res wrappers"
)]
#[expect(
    clippy::too_many_lines,
    reason = "Ordered source validation and GPU preparation remain a single auditable operation"
)]
#[expect(
    clippy::type_complexity,
    reason = "The selected render-view query keeps the Bevy pass inputs explicit"
)]
fn prepare(
    snapshot: Res<Snapshot>,
    targets: Res<Targets>,
    buffers: Res<RenderAssets<GpuShaderBuffer>>,
    views: Query<(
        Entity,
        &MainEntity,
        &ExtractedView,
        Option<&ViewPacket>,
        Option<&TemporalJitter>,
        Option<&MainPassResolutionOverride>,
    )>,
    device: Res<RenderDevice>,
    queue: Res<RenderQueue>,
    pipeline: Res<Pipeline>,
    cache: Res<PipelineCache>,
    view_uniforms: Res<ViewUniforms>,
    mut geometry: ResMut<GeometryCache>,
    mut prepared: ResMut<Prepared>,
    observation: Res<Observation>,
) {
    *prepared = Prepared::default();
    observation.record(None);
    // A deleted camera or pending/mismatched image must make an old silhouette
    // inaccessible even when encode cannot open a render pass to clear it.
    let flag_initialized = if let Some(flag) = buffers.get(&targets.readiness) {
        queue.write_buffer(&flag.buffer, 0, &[0; 16]);
        true
    } else {
        false
    };
    geometry
        .0
        .retain(|e, _| snapshot.volumes.iter().any(|v| v.entity == *e));
    let mut matching = views
        .iter()
        .filter(|(_, e, _, _, _, _)| Some(e.id()) == snapshot.camera);
    let Some((entity, main, view, packet, jitter, resolution)) = matching.next() else {
        return;
    };
    let registered = u32::try_from(snapshot.volumes.len());
    let mut encoding = Encoding {
        view: main.id(),
        render_view: entity,
        tag: packet.map(|p| p.tag),
        generation: snapshot.generation,
        revision: snapshot.revision,
        size: snapshot.size,
        registered: registered.unwrap_or(u32::MAX),
        active: 0,
        encoded: 0,
        cleared: false,
        flag_initialized,
        active_published: false,
        errors: snapshot.errors.clone(),
    };
    if registered.is_err() {
        encoding
            .errors
            .push("Hull registration count exceeds the GPU counter limit".into());
    }
    if !flag_initialized {
        encoding
            .errors
            .push("Hull activation buffer is not prepared".into());
    }
    if matching.next().is_some() {
        encoding
            .errors
            .push("Selected hull camera has multiple extracted views".into());
    }
    if view.viewport != UVec4::new(0, 0, snapshot.size.x, snapshot.size.y) {
        encoding
            .errors
            .push("Extracted hull viewport differs from its allocation".into());
    }
    if resolution.is_some_and(|r| r.0 != snapshot.size) {
        encoding
            .errors
            .push("Hull distance does not support a different main-pass resolution".into());
    }
    // Match prepare_view_uniforms, including its actual RenderWorld jitter.
    // Draws bind the real Bevy uniform; this matrix only culls source bounds.
    let mut projection = view.clip_from_view;
    if let Some(jitter) = jitter {
        jitter.jitter_projection(&mut projection, snapshot.size.as_vec2());
    }
    let clip = if jitter.is_some() {
        projection * view.world_from_view.to_matrix().inverse()
    } else {
        view.clip_from_world
            .unwrap_or_else(|| projection * view.world_from_view.to_matrix().inverse())
    };
    if !clip.is_finite() || !view.world_from_view.to_matrix().is_finite() {
        encoding
            .errors
            .push("Hull view matrix is non-finite".into());
    }
    let frustum = Frustum(bevy::math::primitives::ViewFrustum::from_clip_from_world(
        &clip,
    ));
    let camera = view.world_from_view.translation();
    if let Some(binding) = view_uniforms.uniforms.binding() {
        prepared.view_group = Some(device.create_bind_group(
            "Hull actual view",
            &cache.get_bind_group_layout(&pipeline.view_layout),
            &BindGroupEntries::single(binding),
        ));
    } else {
        encoding
            .errors
            .push("Actual hull view uniform is not prepared".into());
    }
    for volume in &snapshot.volumes {
        match volume
            .geometry
            .camera_inside(volume.world_from_local, camera)
        {
            Ok(true) => continue,
            Err(error) => {
                encoding.errors.push(error);
                continue;
            }
            Ok(false) => (),
        }
        let center = volume
            .world_from_local
            .transform_point3((volume.geometry.min + volume.geometry.max) * 0.5);
        // Matches Three Sphere.applyMatrix4: radius times the largest column length.
        let scale = [
            volume.world_from_local.x_axis.truncate(),
            volume.world_from_local.y_axis.truncate(),
            volume.world_from_local.z_axis.truncate(),
        ]
        .map(Vec3::length_squared)
        .into_iter()
        .fold(0.0_f32, f32::max)
        .sqrt();
        if !center.is_finite()
            || !scale.is_finite()
            || !(volume.geometry.radius * scale).is_finite()
        {
            encoding
                .errors
                .push("Transformed hull bounds are non-finite".into());
            continue;
        }
        let far = frustum.half_spaces[bevy::math::primitives::ViewFrustum::FAR_PLANE_IDX].normal();
        let finite_far = far.is_finite() && far.length_squared() > 0.0;
        if !frustum.intersects_sphere(
            &Sphere {
                center: center.into(),
                radius: volume.geometry.radius * scale,
            },
            finite_far,
        ) {
            continue;
        }
        encoding.active += 1;
        if !geometry
            .0
            .get(&volume.entity)
            .is_some_and(|g| Arc::ptr_eq(&g.source, &volume.geometry))
        {
            let bytes: Vec<u8> = volume
                .geometry
                .positions
                .iter()
                .flatten()
                .flat_map(|x| x.to_le_bytes())
                .collect();
            let vertex = device.create_buffer_with_data(&BufferInitDescriptor {
                label: Some("Hull positions"),
                contents: &bytes,
                usage: BufferUsages::VERTEX,
            });
            let bytes: Vec<u8> = volume
                .geometry
                .indices
                .iter()
                .flat_map(|x| x.to_le_bytes())
                .collect();
            let index = device.create_buffer_with_data(&BufferInitDescriptor {
                label: Some("Hull indices"),
                contents: &bytes,
                usage: BufferUsages::INDEX,
            });
            geometry.0.insert(
                volume.entity,
                CachedGeometry {
                    source: volume.geometry.clone(),
                    vertex,
                    index,
                },
            );
        }
        let mut uniform = UniformBuffer::from(ObjectUniform {
            world_from_local: volume.world_from_local,
        });
        uniform.write_buffer(&device, &queue);
        let group = device.create_bind_group(
            "Hull object",
            &cache.get_bind_group_layout(&pipeline.object_layout),
            &BindGroupEntries::single(uniform.binding().expect("written object uniform")),
        );
        prepared.draws.push(Draw {
            entity: volume.entity,
            group,
            _uniform: uniform,
            indices: u32::try_from(volume.geometry.indices.len())
                .expect("validated hull index count fits the GPU draw counter"),
        });
    }
    prepared.view = Some(entity);
    prepared.encoding = Some(encoding);
}

#[allow(clippy::too_many_arguments)]
#[expect(
    clippy::needless_pass_by_value,
    reason = "Bevy system parameters must be owned SystemParam values, not borrowed Res wrappers"
)]
fn encode(
    view: ViewQuery<&ViewUniformOffset>,
    targets: Res<Targets>,
    images: Res<RenderAssets<GpuImage>>,
    buffers: Res<RenderAssets<GpuShaderBuffer>>,
    queue: Res<RenderQueue>,
    pipeline: Res<Pipeline>,
    cache: Res<PipelineCache>,
    geometry: Res<GeometryCache>,
    prepared: Res<Prepared>,
    observation: Res<Observation>,
    mut context: RenderContext,
) {
    if prepared.view != Some(view.entity()) {
        return;
    }
    let Some(mut encoding) = prepared.encoding.clone() else {
        return;
    };
    let (Some(distance), Some(depth)) = (images.get(&targets.distance), images.get(&targets.depth))
    else {
        encoding.errors.push("Hull images are not prepared".into());
        observation.record(Some(encoding));
        return;
    };
    if distance.texture.size()
        != (Extent3d {
            width: encoding.size.x,
            height: encoding.size.y,
            depth_or_array_layers: 1,
        })
        || depth.texture.size() != distance.texture.size()
        || distance.texture.format() != TextureFormat::R16Float
        || depth.texture.format() != TextureFormat::Depth32Float
    {
        encoding
            .errors
            .push("Hull GPU images differ from this frame allocation".into());
        observation.record(Some(encoding));
        return;
    }
    let colors = [Some(RenderPassColorAttachment {
        view: &distance.texture_view,
        depth_slice: None,
        resolve_target: None,
        ops: Operations {
            load: LoadOp::Clear(wgpu::Color::TRANSPARENT),
            store: StoreOp::Store,
        },
    })];
    {
        // Clear even when no hull is active or the pipeline is preparing. A prior
        // boat/camera/resize cannot leave a stale exclusion silhouette in the sea.
        let mut pass = context.begin_tracked_render_pass(RenderPassDescriptor {
            label: Some("Tidewater closed hull distance"),
            color_attachments: &colors,
            depth_stencil_attachment: Some(RenderPassDepthStencilAttachment {
                view: &depth.texture_view,
                depth_ops: Some(Operations {
                    load: LoadOp::Clear(0.0),
                    store: StoreOp::Store,
                }),
                stencil_ops: None,
            }),
            ..default()
        });
        encoding.cleared = true;
        if encoding.active > 0 {
            match (
                cache.get_render_pipeline(pipeline.id),
                prepared.view_group.as_ref(),
            ) {
                (Some(pipeline), Some(group)) if encoding.errors.is_empty() => {
                    pass.set_render_pipeline(pipeline);
                    pass.set_bind_group(0, group, &[view.into_inner().offset]);
                    for draw in &prepared.draws {
                        let mesh = &geometry.0[&draw.entity];
                        pass.set_bind_group(1, &draw.group, &[]);
                        pass.set_vertex_buffer(0, mesh.vertex.slice(..));
                        pass.set_index_buffer(mesh.index.slice(..), IndexFormat::Uint32);
                        pass.draw_indexed(0..draw.indices, 0, 0..1);
                        encoding.encoded += 1;
                    }
                }
                _ => encoding
                    .errors
                    .push("Hull pipeline or view binding is not ready".into()),
            }
        }
    }
    if encoding.active > 0 && encoding.encoded == encoding.active && encoding.errors.is_empty() {
        if let Some(flag) = buffers.get(&targets.readiness) {
            let mut value = [0_u8; 16];
            value[..4].copy_from_slice(&1_f32.to_le_bytes());
            queue.write_buffer(&flag.buffer, 0, &value);
            encoding.active_published = true;
        } else {
            encoding
                .errors
                .push("Hull activation buffer disappeared before encoding".into());
        }
    }
    observation.record(Some(encoding));
}

#[cfg(test)]
mod tests {
    use super::*;

    fn world() -> World {
        let mut world = World::new();
        world.init_resource::<Assets<Image>>();
        world.init_resource::<Assets<ShaderBuffer>>();
        let mut images = world.resource_mut::<Assets<Image>>();
        let distance = images.add(image(UVec2::ONE, TextureFormat::R16Float));
        let depth = images.add(image(UVec2::ONE, TextureFormat::Depth32Float));
        let readiness = world
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::from(Vec4::ZERO));
        world.insert_resource(Targets {
            distance,
            depth,
            readiness,
        });
        world.init_resource::<Selection>();
        world.init_resource::<Snapshot>();
        world
    }
    fn volume() -> HullVolume {
        HullVolume(
            Geometry::new(
                vec![[0., 0., 0.], [1., 0., 0.], [0., 1., 0.], [0., 0., 1.]],
                vec![0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3],
            )
            .unwrap(),
        )
    }
    #[test]
    fn edits_move_and_remove_the_actual_registered_volume() {
        let mut world = world();
        let root = world.spawn(volume()).id();
        synchronize(&mut world);
        let first = world.resource::<Snapshot>().revision;
        assert_eq!(world.resource::<Snapshot>().volumes.len(), 1);
        let moved = GlobalTransform::from_translation(Vec3::new(64.5, 0., 36.5));
        world.entity_mut(root).insert(moved);
        synchronize(&mut world);
        assert_eq!(
            world.resource::<Snapshot>().volumes[0].world_from_local,
            moved.to_matrix()
        );
        assert!(world.resource::<Snapshot>().revision > first);
        world.despawn(root);
        synchronize(&mut world);
        assert!(world.resource::<Snapshot>().volumes.is_empty());
    }
    #[test]
    fn missing_propagated_transform_is_an_error_instead_of_an_empty_scene_assertion() {
        let mut world = world();
        let root = world.spawn(volume()).id();
        world.entity_mut(root).remove::<GlobalTransform>();
        synchronize(&mut world);
        assert_eq!(world.resource::<Snapshot>().errors.len(), 1);
        assert!(world.resource::<Snapshot>().errors[0].contains("propagated transform"));
    }
    fn encoding() -> Encoding {
        let mut world = World::new();
        let view = world.spawn_empty().id();
        Encoding {
            view,
            render_view: view,
            tag: Some(FrameTag { view, serial: 9 }),
            generation: 1,
            revision: 3,
            size: UVec2::new(1919, 1079),
            registered: 1,
            active: 0,
            encoded: 0,
            cleared: true,
            flag_initialized: true,
            active_published: false,
            errors: vec![],
        }
    }
    #[test]
    fn inactive_registered_hull_is_valid_and_an_empty_registry_is_not_an_assertion() {
        let mut e = encoding();
        let tag = e.tag.unwrap();
        assert!(e.matches_required_frame(tag, e.size));
        e.registered = 0;
        assert!(e.complete());
        assert!(!e.matches_required_frame(tag, e.size));
    }
    #[test]
    fn failed_or_foreign_frame_mask_cannot_qualify_auxiliary_water() {
        let e = encoding();
        let tag = e.tag.unwrap();
        assert!(!e.matches_required_frame(FrameTag { serial: 10, ..tag }, e.size));
        assert!(!e.matches_required_frame(tag, UVec2::new(1920, 1080)));
        let mut broken = e.clone();
        broken.active = 1;
        assert!(!broken.complete());
        let mut broken = e.clone();
        broken.cleared = false;
        assert!(!broken.complete());
        let mut broken = e.clone();
        broken.generation = 0;
        assert!(!broken.complete());
        let mut broken = e;
        broken.errors.push("pending pipeline".into());
        assert!(!broken.complete());
    }
}
