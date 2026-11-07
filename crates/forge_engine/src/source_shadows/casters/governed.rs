//! Governed vegetation in the existing source cascade pass, using resident Bevy
//! meshes and images. No model, texture or shader source is persisted here.
use crate::vegetation::VisualValleyVegetationMaterial;
use bevy::{
    camera::primitives::{Aabb, Frustum},
    ecs::system::SystemParam,
    math::{Affine3A, primitives::ViewFrustum},
    prelude::*,
    render::{
        Extract,
        mesh::{RenderMesh, RenderMeshBufferInfo, allocator::MeshAllocator},
        render_asset::RenderAssets,
        render_resource::{
            self as gpu, BindGroup, BindGroupEntries, ShaderType, StorageBuffer, UniformBuffer,
        },
        renderer::{RenderDevice, RenderQueue},
        storage::{GpuShaderBuffer, ShaderBuffer},
        texture::{FallbackImage, GpuImage},
    },
};
use std::sync::{
    Arc,
    atomic::{AtomicU32, Ordering},
};

/// A preparation/encoding observation, never a GPU completion fence.
#[derive(Default)]
pub struct Receipt {
    prepared: AtomicU32,
    forward_prepared: AtomicU32,
    encoded: AtomicU32,
    first_encoded: AtomicU32,
    target: std::sync::Mutex<Option<bevy::asset::AssetId<Image>>>,
    error: std::sync::Mutex<Option<String>>,
    forward_resources: std::sync::Mutex<Option<ForwardResourcesReceipt>>,
}

/// Actual prepared forward/prepass keys, not a queue-completion receipt.
#[derive(Clone, PartialEq, Eq)]
pub(crate) struct ForwardResourcesReceipt {
    context: crate::render_inventory::PreparedGraphicsContext,
    target: bevy::asset::AssetId<Image>,
    keys: Vec<(
        bevy::material::key::ErasedMaterialPipelineKey,
        Option<bevy::pbr::MeshPipelineKey>,
        bevy::mesh::MeshVertexBufferLayoutRef,
    )>,
}
impl Receipt {
    pub(crate) fn bind_target(&self, target: bevy::asset::AssetId<Image>) {
        let mut current = self.target.lock().expect("source shadow target lock");
        if *current != Some(target) {
            *current = Some(target);
            self.prepared.store(0, Ordering::Release);
            self.forward_prepared.store(0, Ordering::Release);
            self.encoded.store(0, Ordering::Release);
            self.first_encoded.store(0, Ordering::Release);
            *self
                .forward_resources
                .lock()
                .expect("source forward receipt lock") = None;
        }
    }
    fn record_prepared(&self, frame: &Frame) {
        let target = self.target.lock().expect("source shadow target lock");
        if *target == Some(frame.target.id()) {
            self.prepared
                .store(frame.stamp.saturating_add(2), Ordering::Release);
        }
    }
    fn record_forward(&self, frame: &Frame, ready: bool) {
        let target = self.target.lock().expect("source shadow target lock");
        if *target == Some(frame.target.id()) {
            self.forward_prepared.store(
                if ready {
                    frame.stamp.saturating_add(2)
                } else {
                    0
                },
                Ordering::Release,
            );
        }
    }
    pub(crate) fn forward_prepared_frame(&self) -> Option<u32> {
        self.forward_prepared.load(Ordering::Acquire).checked_sub(1)
    }

    pub(crate) fn forward_resources(
        &self,
        world: &World,
        context: &crate::render_inventory::GraphicsContextReceipt,
    ) -> Option<ForwardResourcesReceipt> {
        if self.error().is_some() || !context.is_current(world) {
            return None;
        }
        let prepared = self.forward_resources.lock().ok()?.clone()?;
        let target = *self.target.lock().ok()?;
        let frame = world.get_resource::<bevy::diagnostic::FrameCount>()?.0;
        (target == Some(prepared.target)
            && context.matches_prepared(&prepared.context)
            && self.forward_prepared_frame().is_some_and(|prepared| {
                // record_forward stamps the next main frame; accept only that
                // frame or the immediately following one, never a stale view.
                prepared.checked_sub(frame).is_some_and(|ahead| ahead <= 1)
                    || frame.checked_sub(prepared).is_some_and(|age| age <= 1)
            }))
        .then_some(prepared)
    }

    pub(crate) fn forward_resources_current(
        &self,
        world: &World,
        context: &crate::render_inventory::GraphicsContextReceipt,
        admitted: &ForwardResourcesReceipt,
    ) -> bool {
        self.forward_resources(world, context).as_ref() == Some(admitted)
    }
    fn record_encoded(&self, frame: &Frame) {
        let target = self.target.lock().expect("source shadow target lock");
        if *target == Some(frame.target.id()) {
            let encoded = frame.stamp.saturating_add(2);
            let _ = self.first_encoded.compare_exchange(
                0,
                encoded,
                Ordering::AcqRel,
                Ordering::Acquire,
            );
            self.encoded.store(encoded, Ordering::Release);
        }
    }
    pub(crate) fn first_encoded_frame(&self) -> Option<u32> {
        self.first_encoded.load(Ordering::Acquire).checked_sub(1)
    }
    pub(crate) fn refuse(&self, frame: &Frame, error: String) {
        let target = self.target.lock().expect("source shadow target lock");
        if *target == Some(frame.target.id()) {
            *self.error.lock().expect("source shadow receipt lock") = Some(error);
        }
    }
    pub(crate) fn error(&self) -> Option<String> {
        self.error
            .lock()
            .expect("source shadow receipt lock")
            .clone()
    }
    #[must_use]
    pub fn prepared_frame(&self) -> Option<u32> {
        self.prepared.load(Ordering::Acquire).checked_sub(1)
    }
    #[must_use]
    pub fn encoded_frame(&self) -> Option<u32> {
        self.encoded.load(Ordering::Acquire).checked_sub(1)
    }
}

#[derive(Clone, ShaderType)]
pub(crate) struct Instance {
    pub model: Mat4,
    pub tag: u32,
    pub pad0: u32,
    pub pad1: u32,
    pub pad2: u32,
}

#[derive(Clone, ShaderType)]
struct MaterialUniform {
    primary_color_roughness: Vec4,
    secondary_color_roughness: Vec4,
    bark_color_roughness: Vec4,
    metallic_reflectance: Vec4,
    wind_parameters: Vec4,
    interaction_center_radius: Vec4,
    interaction_direction_strength: Vec4,
    wind_direction: Vec4,
    wetness_parameters: Vec4,
    hlod_distances: Vec4,
    prototype_parameters: Vec4,
    shadow_fade_distances: Vec4,
    temporal_parameters: Vec4,
}

#[derive(Clone, ShaderType)]
struct Params {
    clip: Mat4,
    camera: Vec4,
    alpha: Vec4,
    color: Vec4,
    uv_transform: Mat3,
    vegetation: MaterialUniform,
}

#[derive(Clone)]
pub(crate) struct Packet {
    mesh: Handle<Mesh>,
    texture: Option<Handle<Image>>,
    uv: bevy::mesh::MeshVertexAttribute,
    cull: Option<gpu::Face>,
    params: Params,
    pub instances: Arc<Vec<Instance>>,
    /// Local bounds already padded for wind and interaction by the model owner.
    /// Legacy callers without bounds retain their complete caster stream.
    pub bounds: Option<Arc<Vec<Aabb>>>,
    pub capacity: usize,
    temporal: Handle<ShaderBuffer>,
}

impl Packet {
    pub(crate) fn set_frame_times(&mut self, times: [f32; 2]) {
        // The custom cascade uses the same Time snapshot as the existing
        // forward/prepass clock buffer, without mutating the material asset.
        if self.params.vegetation.temporal_parameters.z > 0.5 {
            self.params.vegetation.wind_parameters.z = times[0];
            self.params.vegetation.wind_parameters.w = times[1];
        }
    }

    pub(crate) fn refresh(
        &mut self,
        material: &VisualValleyVegetationMaterial,
        camera: Vec3,
    ) -> Result<(), String> {
        let mut current = Self::new(self.mesh.clone(), material, camera)?;
        current.instances = self.instances.clone();
        current.bounds.clone_from(&self.bounds);
        current.capacity = self.capacity;
        *self = current;
        Ok(())
    }
    pub(crate) fn new(
        mesh: Handle<Mesh>,
        material: &VisualValleyVegetationMaterial,
        camera: Vec3,
    ) -> Result<Self, String> {
        let e = &material.extension;
        if !camera.is_finite() || e.prototype_parameters.x <= 0.5 {
            return Err("Source species shadows require normalized governed geometry".into());
        }
        let (mode, cutoff) = match material.base.alpha_mode {
            AlphaMode::Opaque | AlphaMode::Multiply => (0.0, 0.0),
            AlphaMode::Mask(cutoff) => (1.0, cutoff),
            AlphaMode::Blend | AlphaMode::Add | AlphaMode::AlphaToCoverage => (2.0, 0.05),
            AlphaMode::Premultiplied => (3.0, 0.05),
        };
        let wind = MaterialUniform {
            primary_color_roughness: e.primary_color_roughness,
            secondary_color_roughness: e.secondary_color_roughness,
            bark_color_roughness: e.bark_color_roughness,
            metallic_reflectance: e.metallic_reflectance,
            wind_parameters: e.wind_parameters,
            interaction_center_radius: e.interaction_center_radius,
            interaction_direction_strength: e.interaction_direction_strength,
            wind_direction: e.wind_direction,
            wetness_parameters: e.wetness_parameters,
            hlod_distances: e.hlod_distances,
            prototype_parameters: e.prototype_parameters,
            shadow_fade_distances: e.shadow_fade_distances,
            temporal_parameters: e.temporal_parameters,
        };
        let params = Params {
            clip: Mat4::IDENTITY,
            camera: camera.extend(1.0),
            alpha: Vec4::new(
                mode,
                cutoff,
                if material.base.base_color_texture.is_some() {
                    1.0
                } else {
                    0.0
                },
                0.0,
            ),
            color: material.base.base_color.to_linear().to_vec4(),
            uv_transform: Mat3::from(material.base.uv_transform),
            vegetation: wind,
        };
        if !params.color.is_finite()
            || !params.uv_transform.is_finite()
            || !params.alpha.is_finite()
            || [
                e.wind_parameters,
                e.interaction_center_radius,
                e.interaction_direction_strength,
                e.wind_direction,
                e.prototype_parameters,
                e.shadow_fade_distances,
                e.temporal_parameters,
            ]
            .iter()
            .any(|v| !v.is_finite())
        {
            return Err("Source species shadow material has non-finite parameters".into());
        }
        Ok(Self {
            mesh,
            texture: material.base.base_color_texture.clone(),
            uv: match material.base.base_color_channel {
                bevy::mesh::UvChannel::Uv0 => Mesh::ATTRIBUTE_UV_0,
                bevy::mesh::UvChannel::Uv1 => Mesh::ATTRIBUTE_UV_1,
            },
            cull: material.base.cull_mode,
            params,
            instances: Arc::default(),
            bounds: None,
            capacity: 0,
            temporal: e.temporal_lod_buffer.clone(),
        })
    }
}

#[derive(Resource, Clone)]
pub(crate) struct Frame {
    pub target: Handle<Image>,
    pub stamp: u32,
    pub enabled: bool,
    pub packets: Vec<Packet>,
    pub receipt: Arc<Receipt>,
    pub camera: Option<Entity>,
    pub forward: Arc<Vec<ForwardRecipe>>,
}

#[derive(Clone)]
pub(crate) struct ForwardRecipe {
    pub mesh: Handle<Mesh>,
    pub material: bevy::asset::UntypedHandle,
    pub material_type: std::any::TypeId,
    pub crossfade: bool,
}

struct ForwardWork {
    key: bevy::material::key::ErasedMaterialPipelineKey,
    prepass: Option<bevy::pbr::MeshPipelineKey>,
    layout: bevy::mesh::MeshVertexBufferLayoutRef,
    properties: Arc<bevy::material::MaterialProperties>,
}

/// Queue the very same material specializers and mesh/view keys as Bevy's
/// visible queue, without making the candidate visible or creating another view.
pub(crate) fn prewarm_forward(world: &mut World) {
    let Some(frame) = world.get_resource::<Frame>().cloned() else {
        return;
    };
    // A pending specialization or device/view change cannot reuse the previous
    // preparation, even when its immutable model recipes are unchanged.
    *frame
        .receipt
        .forward_resources
        .lock()
        .expect("source forward receipt lock") = None;
    match prepare_forward(world, &frame) {
        Ok(ready) => frame.receipt.record_forward(&frame, ready),
        Err(error) => frame.receipt.refuse(&frame, error),
    }
}

#[allow(
    clippy::too_many_lines,
    reason = "Keep actual forward/prepass specialization and its context-bound preparation receipt in one transaction"
)]
fn prepare_forward(world: &mut World, frame: &Frame) -> Result<bool, String> {
    use bevy::{
        material::key::{ErasedMaterialPipelineKey, ErasedMeshPipelineKey},
        pbr::{
            MeshPipelineKey, PreparedMaterial, ViewKeyCache, ViewKeyPrepassCache,
            alpha_mode_pipeline_key,
        },
        render::{
            camera::ExtractedCamera, erased_render_asset::ErasedRenderAssets,
            sync_world::MainEntity, view::ExtractedView,
        },
    };
    if frame.forward.is_empty() {
        return Ok(true);
    }
    let Some(camera) = frame.camera else {
        return Ok(false);
    };
    let mut views = world.query_filtered::<(&ExtractedView, &MainEntity), With<ExtractedCamera>>();
    let view = views
        .iter(world)
        .find(|(_, main)| main.id() == camera)
        .map(|(view, _)| view.retained_view_entity);
    let Some(view_key) =
        view.and_then(|view| world.get_resource::<ViewKeyCache>()?.get(&view).copied())
    else {
        return Ok(false);
    };
    let prepass_flags = MeshPipelineKey::DEPTH_PREPASS
        | MeshPipelineKey::NORMAL_PREPASS
        | MeshPipelineKey::MOTION_VECTOR_PREPASS
        | MeshPipelineKey::DEFERRED_PREPASS;
    let prepass_view = view.and_then(|view| {
        world
            .get_resource::<ViewKeyPrepassCache>()?
            .get(&view)
            .copied()
    });
    let mut work = Vec::new();
    for recipe in frame.forward.iter() {
        let Some(mesh) = world
            .get_resource::<RenderAssets<RenderMesh>>()
            .and_then(|meshes| meshes.get(recipe.mesh.id()))
        else {
            return Ok(false);
        };
        let Some(material) = world
            .get_resource::<ErasedRenderAssets<PreparedMaterial>>()
            .and_then(|materials| materials.get(recipe.material.id()))
        else {
            return Ok(false);
        };
        let mut bits: MeshPipelineKey = material.properties.mesh_pipeline_key_bits.downcast();
        bits.insert(alpha_mode_pipeline_key(
            material.properties.alpha_mode,
            &Msaa::from_samples(view_key.msaa_samples()),
        ));
        let mut mesh_key =
            view_key | MeshPipelineKey::from_bits_retain(mesh.key_bits.bits()) | bits;
        if recipe.crossfade {
            mesh_key |= MeshPipelineKey::VISIBILITY_RANGE_DITHER;
        }
        let prepass = if view_key.intersects(prepass_flags)
            && material.properties.prepass_enabled
            && !material.properties.reads_view_transmission_texture
            && matches!(
                material.properties.alpha_mode,
                AlphaMode::Opaque | AlphaMode::Mask(_) | AlphaMode::AlphaToCoverage
            ) {
            let Some(view) = prepass_view else {
                return Ok(false);
            };
            Some(prepass_mesh_key(
                view,
                MeshPipelineKey::from_bits_retain(mesh.key_bits.bits()),
                &material.properties,
                recipe.crossfade,
            ))
        } else {
            None
        };
        work.push(ForwardWork {
            key: ErasedMaterialPipelineKey {
                mesh_key: ErasedMeshPipelineKey::new(mesh_key),
                material_key: material.properties.material_key.clone(),
                type_id: recipe.material_type,
            },
            prepass,
            layout: mesh.layout.clone(),
            properties: material.properties.clone(),
        });
    }
    let context =
        crate::render_inventory::prepared_graphics_context(world, camera, view_key, prepass_view);
    let keys = work
        .iter()
        .map(|work| (work.key.clone(), work.prepass, work.layout.clone()))
        .collect();
    let ready = compile_forward(world, work)?;
    if ready && let Some(context) = context {
        let target = frame
            .receipt
            .target
            .lock()
            .expect("source shadow target lock");
        if *target == Some(frame.target.id()) {
            *frame
                .receipt
                .forward_resources
                .lock()
                .expect("source forward receipt lock") = Some(ForwardResourcesReceipt {
                context,
                target: frame.target.id(),
                keys,
            });
        }
    }
    Ok(ready)
}

fn compile_forward(world: &mut World, work: Vec<ForwardWork>) -> Result<bool, String> {
    let mut pipelines = Vec::new();
    for ForwardWork {
        key,
        prepass,
        layout,
        properties,
    } in work
    {
        let base = properties
            .base_specialize
            .ok_or("Species forward specializer is absent")?;
        pipelines.push(
            base(world, key.clone(), &layout, &properties).map_err(|error| error.to_string())?,
        );
        if let Some(mesh_key) = prepass {
            let prepare = properties
                .prepass_specialize
                .ok_or("Species prepass specializer is absent")?;
            pipelines.push(
                prepare(
                    world,
                    bevy::material::key::ErasedMaterialPipelineKey {
                        mesh_key: bevy::material::key::ErasedMeshPipelineKey::new(mesh_key),
                        ..key
                    },
                    &layout,
                    &properties,
                )
                .map_err(|error| error.to_string())?,
            );
        }
    }
    let cache = world.resource::<gpu::PipelineCache>();
    let mut ready = true;
    for pipeline in pipelines {
        match cache.get_render_pipeline_state(pipeline) {
            gpu::CachedPipelineState::Ok(_) => {}
            // Bevy retries these states as embedded shaders and their imports
            // arrive. Keep the candidate hidden until that retry succeeds.
            gpu::CachedPipelineState::Err(
                bevy::shader::ShaderCacheError::ShaderNotLoaded(_)
                | bevy::shader::ShaderCacheError::ShaderImportNotYetAvailable,
            ) => ready = false,
            gpu::CachedPipelineState::Err(error) => {
                return Err(format!("Species color/prepass pipeline failed: {error}"));
            }
            _ => ready = false,
        }
    }
    Ok(ready)
}

fn prepass_mesh_key(
    view: bevy::pbr::MeshPipelineKey,
    mesh: bevy::pbr::MeshPipelineKey,
    properties: &bevy::material::MaterialProperties,
    crossfade: bool,
) -> bevy::pbr::MeshPipelineKey {
    use bevy::pbr::{
        MeshPipelineKey, PrepassFragmentShader, PrepassVertexShader, alpha_mode_pipeline_key,
    };
    let mut key = view
        | mesh
        | alpha_mode_pipeline_key(
            properties.alpha_mode,
            &Msaa::from_samples(view.msaa_samples()),
        );
    if crossfade {
        key |= MeshPipelineKey::VISIBILITY_RANGE_DITHER;
    }
    if properties.get_shader(PrepassVertexShader).is_some()
        || properties.get_shader(PrepassFragmentShader).is_some()
    {
        key |= MeshPipelineKey::PREPASS_READS_MATERIAL;
    }
    key
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy extraction requires owned system parameter wrappers"
)]
pub(crate) fn extract(
    mut commands: Commands,
    frame: Extract<Option<Res<Frame>>>,
    stamp: Extract<Res<bevy::diagnostic::FrameCount>>,
) {
    // Inputs are published during PostUpdate. Bevy increments FrameCount in
    // Last before extraction, so this is the exact renderer frame, not an
    // allowance for a stale input from an earlier update.
    if let Some(frame) = frame.as_ref().filter(|frame| {
        frame.stamp.checked_add(1) == Some(stamp.0) && frame.stamp.checked_add(2).is_some()
    }) {
        commands.insert_resource((**frame).clone());
    } else {
        commands.remove_resource::<Frame>();
    }
}

#[derive(SystemParam)]
pub(crate) struct RenderResources<'w> {
    meshes: Res<'w, RenderAssets<RenderMesh>>,
    allocator: Res<'w, MeshAllocator>,
    images: Res<'w, RenderAssets<GpuImage>>,
    fallback: Res<'w, FallbackImage>,
    buffers: Res<'w, RenderAssets<GpuShaderBuffer>>,
}

pub(crate) struct Draw {
    pub pipeline: gpu::RenderPipeline,
    pub groups: Vec<BindGroup>,
    pub mesh: Handle<Mesh>,
    ranges: [std::ops::Range<u32>; 3],
    capacity: usize,
    texture: Option<Handle<Image>>,
    uv: bevy::mesh::MeshVertexAttribute,
    cull: Option<gpu::Face>,
    layout: bevy::mesh::MeshVertexBufferLayoutRef,
    texture_view: gpu::TextureView,
    sampler: gpu::Sampler,
    temporal: gpu::Buffer,
    uniforms: Vec<UniformBuffer<Params>>,
    instances: StorageBuffer<Vec<Instance>>,
    selection: Option<SelectionKey>,
}

struct SelectionKey {
    instances: Arc<Vec<Instance>>,
    bounds: Option<Arc<Vec<Aabb>>>,
    matrices: [Mat4; 3],
}

impl SelectionKey {
    fn matches(&self, packet: &Packet, matrices: &[Mat4]) -> bool {
        Arc::ptr_eq(&self.instances, &packet.instances)
            && self.matrices.as_slice() == matrices
            && match (&self.bounds, &packet.bounds) {
                (Some(a), Some(b)) => Arc::ptr_eq(a, b),
                (None, None) => true,
                _ => false,
            }
    }
}

fn select_cascades(
    packet: &Packet,
    matrices: &[Mat4],
) -> (Vec<Instance>, [std::ops::Range<u32>; 3]) {
    let mut instances = Vec::with_capacity(packet.instances.len() * 3);
    let mut ranges = std::array::from_fn(|_| 0..0);
    for (layer, matrix) in matrices.iter().enumerate() {
        let frustum = Frustum(ViewFrustum::from_clip_from_world(matrix));
        let start = u32::try_from(instances.len()).expect("bounded cascade instances");
        for (index, instance) in packet.instances.iter().enumerate() {
            if packet
                .bounds
                .as_ref()
                .and_then(|bounds| bounds.get(index))
                .is_none_or(|bounds| {
                    frustum.intersects_obb(bounds, &Affine3A::from_mat4(instance.model), true, true)
                })
            {
                instances.push(instance.clone());
            }
        }
        ranges[layer] = start..u32::try_from(instances.len()).expect("bounded cascade instances");
    }
    (instances, ranges)
}

pub(crate) struct Batch {
    receipt: Arc<Receipt>,
    pub draws: Vec<Draw>,
    pub enabled: bool,
}

impl Batch {
    pub(crate) fn matches(&self, frame: &Frame, resources: &RenderResources) -> bool {
        Arc::ptr_eq(&self.receipt, &frame.receipt)
            && self.draws.len() == frame.packets.len()
            && self.draws.iter().zip(&frame.packets).all(|(draw, packet)| {
                let texture = packet
                    .texture
                    .as_ref()
                    .and_then(|handle| resources.images.get(handle.id()))
                    .or_else(|| packet.texture.is_none().then_some(&resources.fallback.d2));
                draw.mesh == packet.mesh
                    && packet.instances.len() <= packet.capacity
                    && packet
                        .bounds
                        .as_ref()
                        .is_none_or(|bounds| bounds.len() == packet.instances.len())
                    && draw.capacity >= packet.capacity.max(1)
                    && draw.texture == packet.texture
                    && draw.uv.id == packet.uv.id
                    && draw.cull == packet.cull
                    && resources
                        .meshes
                        .get(packet.mesh.id())
                        .is_some_and(|mesh| mesh.layout == draw.layout)
                    && texture.is_some_and(|image| {
                        image.texture_view.id() == draw.texture_view.id()
                            && image.sampler.id() == draw.sampler.id()
                    })
                    && resources.buffers.get(packet.temporal.id()).is_some()
                    && resources
                        .allocator
                        .mesh_vertex_slice(&packet.mesh.id())
                        .is_some()
                    && resources.meshes.get(packet.mesh.id()).is_some_and(|mesh| {
                        !mesh.indexed()
                            || resources
                                .allocator
                                .mesh_index_slice(&packet.mesh.id())
                                .is_some()
                    })
            })
    }

    pub(crate) fn update(
        &mut self,
        frame: &Frame,
        matrices: &[Mat4],
        resources: &RenderResources,
        device: &RenderDevice,
        queue: &RenderQueue,
    ) {
        for (draw, packet) in self.draws.iter_mut().zip(&frame.packets) {
            if draw
                .selection
                .as_ref()
                .is_none_or(|key| !key.matches(packet, matrices))
            {
                let (instances, ranges) = select_cascades(packet, matrices);
                draw.ranges = ranges;
                *draw.instances.get_mut() = if instances.is_empty() {
                    vec![empty_instance()]
                } else {
                    instances
                };
                draw.instances.write_buffer(device, queue);
                draw.selection = Some(SelectionKey {
                    instances: packet.instances.clone(),
                    bounds: packet.bounds.clone(),
                    matrices: matrices.try_into().expect("three admitted cascades"),
                });
            }
            let temporal = &resources
                .buffers
                .get(packet.temporal.id())
                .expect("admitted temporal buffer")
                .buffer;
            if temporal.id() != draw.temporal.id() {
                let layout: gpu::BindGroupLayout = draw.pipeline.get_bind_group_layout(0).into();
                draw.groups = draw
                    .uniforms
                    .iter()
                    .map(|uniform| {
                        device.create_bind_group(
                            "Source species depth bindings",
                            &layout,
                            &BindGroupEntries::sequential((
                                uniform,
                                &draw.instances,
                                &draw.texture_view,
                                &draw.sampler,
                                temporal.as_entire_binding(),
                            )),
                        )
                    })
                    .collect();
                draw.temporal = temporal.clone();
            }
            for (uniform, clip) in draw.uniforms.iter_mut().zip(matrices) {
                let mut params = packet.params.clone();
                params.clip = *clip;
                *uniform.get_mut() = params;
                uniform.write_buffer(device, queue);
            }
        }
        self.enabled = frame.enabled;
        self.receipt.record_prepared(frame);
    }

    pub(crate) fn encoded(&self, frame: &Frame) {
        self.receipt.record_encoded(frame);
    }
}

/// Reuses renderer allocations. A missing admitted mesh/image remains pending;
/// there is no replacement texture when the authored material declares one.
#[allow(
    clippy::too_many_lines,
    reason = "Keep the scoped source-depth pipeline ABI and resident-resource adoption together"
)]
pub(crate) fn prepare(
    frame: &Frame,
    matrices: &[Mat4],
    resources: &RenderResources,
    device: &RenderDevice,
    queue: &RenderQueue,
) -> Result<Option<Batch>, String> {
    if matrices.len() != 3 || matrices.iter().any(|m| !m.is_finite()) {
        return Err("Source species shadows require three finite cascades".into());
    }
    let mut draws = Vec::new();
    for packet in &frame.packets {
        if packet.instances.len() > packet.capacity
            || packet.capacity as u64 > crate::MAX_VEGETATION_PROJECTION_ENTITIES
            || packet
                .bounds
                .as_ref()
                .is_some_and(|bounds| bounds.len() != packet.instances.len())
        {
            return Err("Source species shadow instance bounds or capacity differ".into());
        }
        let Some(mesh) = resources.meshes.get(packet.mesh.id()) else {
            return Ok(None);
        };
        if mesh.primitive_topology() != gpu::PrimitiveTopology::TriangleList {
            return Err("Source species shadows require triangle-list geometry".into());
        }
        if resources
            .allocator
            .mesh_vertex_slice(&packet.mesh.id())
            .is_none()
            || mesh.indexed()
                && resources
                    .allocator
                    .mesh_index_slice(&packet.mesh.id())
                    .is_none()
        {
            return Ok(None);
        }
        let Some(temporal) = resources.buffers.get(packet.temporal.id()) else {
            return Ok(None);
        };
        let texture = match &packet.texture {
            Some(handle) => {
                let Some(image) = resources.images.get(handle.id()) else {
                    return Ok(None);
                };
                image
            }
            None => &resources.fallback.d2,
        };
        let mut attributes = vec![Mesh::ATTRIBUTE_POSITION.at_shader_location(0)];
        let has_uv = mesh.layout.0.contains(packet.uv);
        if has_uv {
            attributes.push(packet.uv.at_shader_location(1));
        }
        let layout = mesh
            .layout
            .0
            .get_layout(&attributes)
            .map_err(|e| e.to_string())?;
        let shader = device.create_and_validate_shader_module(gpu::ShaderModuleDescriptor {
            label: Some("Governed source species depth"),
            source: gpu::ShaderSource::Wgsl(shader(has_uv)?.into()),
        });
        let pipeline = device.create_render_pipeline(&gpu::RawRenderPipelineDescriptor {
            label: Some("Governed source species depth"),
            layout: None,
            vertex: gpu::RawVertexState {
                module: &shader,
                entry_point: Some("vertex"),
                compilation_options: default(),
                buffers: &[gpu::RawVertexBufferLayout {
                    array_stride: layout.array_stride,
                    step_mode: layout.step_mode,
                    attributes: &layout.attributes,
                }],
            },
            primitive: gpu::PrimitiveState {
                cull_mode: packet.cull,
                ..default()
            },
            depth_stencil: Some(gpu::DepthStencilState {
                format: gpu::TextureFormat::Depth32Float,
                depth_write_enabled: Some(true),
                depth_compare: Some(gpu::CompareFunction::LessEqual),
                stencil: default(),
                bias: gpu::DepthBiasState {
                    constant: 2,
                    slope_scale: 1.5,
                    clamp: 0.0,
                },
            }),
            multisample: default(),
            fragment: Some(gpu::RawFragmentState {
                module: &shader,
                entry_point: Some("fragment"),
                compilation_options: default(),
                targets: &[],
            }),
            multiview_mask: None,
            cache: None,
        });
        // One existing allocation serves all three layer ranges. Reserve the
        // bounded worst case once; camera motion must not reallocate it.
        let mut instances =
            StorageBuffer::from(vec![empty_instance(); (packet.capacity * 3).max(1)]);
        instances.write_buffer(device, queue);
        let bind_layout: gpu::BindGroupLayout = pipeline.get_bind_group_layout(0).into();
        let mut uniforms = Vec::new();
        let mut groups = Vec::new();
        for clip in matrices {
            let mut params = packet.params.clone();
            params.clip = *clip;
            let mut uniform = UniformBuffer::from(params);
            uniform.write_buffer(device, queue);
            groups.push(device.create_bind_group(
                "Source species depth inputs",
                &bind_layout,
                &BindGroupEntries::sequential((
                    &uniform,
                    &instances,
                    &texture.texture_view,
                    &texture.sampler,
                    temporal.buffer.as_entire_binding(),
                )),
            ));
            uniforms.push(uniform);
        }
        draws.push(Draw {
            pipeline,
            groups,
            mesh: packet.mesh.clone(),
            ranges: std::array::from_fn(|_| 0..0),
            uniforms,
            instances,
            selection: None,
            capacity: packet.capacity.max(1),
            texture: packet.texture.clone(),
            uv: packet.uv,
            cull: packet.cull,
            layout: mesh.layout.clone(),
            texture_view: texture.texture_view.clone(),
            sampler: texture.sampler.clone(),
            temporal: temporal.buffer.clone(),
        });
    }
    let mut batch = Batch {
        receipt: frame.receipt.clone(),
        draws,
        enabled: frame.enabled,
    };
    batch.update(frame, matrices, resources, device, queue);
    Ok(Some(batch))
}

fn empty_instance() -> Instance {
    Instance {
        model: Mat4::IDENTITY,
        tag: 0,
        pad0: 0,
        pad1: 0,
        pad2: 0,
    }
}

pub(crate) fn draw<'a>(
    batch: &'a Batch,
    resources: &'a RenderResources,
    pass: &mut wgpu::RenderPass<'a>,
    layer: usize,
) {
    if !batch.enabled {
        return;
    }
    for draw in &batch.draws {
        if draw.ranges[layer].is_empty() {
            continue;
        }
        let Some(mesh) = resources.meshes.get(draw.mesh.id()) else {
            continue;
        };
        let Some(vertices) = resources.allocator.mesh_vertex_slice(&draw.mesh.id()) else {
            continue;
        };
        pass.set_pipeline(&draw.pipeline);
        pass.set_bind_group(0, &*draw.groups[layer], &[]);
        pass.set_vertex_buffer(0, *vertices.buffer.slice(..));
        match mesh.buffer_info {
            RenderMeshBufferInfo::Indexed {
                count,
                index_format,
            } => {
                let Some(indices) = resources.allocator.mesh_index_slice(&draw.mesh.id()) else {
                    continue;
                };
                pass.set_index_buffer(*indices.buffer.slice(..), index_format);
                pass.draw_indexed(
                    indices.range.start..indices.range.start + count,
                    i32::try_from(vertices.range.start).expect("renderer vertex allocation"),
                    draw.ranges[layer].clone(),
                );
            }
            RenderMeshBufferInfo::NonIndexed => pass.draw(
                vertices.range.start..vertices.range.start + mesh.vertex_count,
                draw.ranges[layer].clone(),
            ),
        }
    }
}

/// Keep the actual deformation functions and distance-mask body from the shared
/// forward/shadow shader. Only bindings and vertex/fragment I/O are adapted.
fn shader(has_uv: bool) -> Result<String, String> {
    let raw =
        include_str!("../../shaders/visual_valley_vegetation_deform.wgsl").replace("\r\n", "\n");
    let item = |start: &str| -> Result<&str, String> {
        let begin = raw
            .find(start)
            .ok_or_else(|| format!("Missing governed shader item: {start}"))?;
        let tail = &raw[begin..];
        let end = tail
            .find("\n}")
            .ok_or("Unterminated governed shader item")?
            + 2;
        Ok(&tail[..end])
    };
    let wind = item("struct VisualValleyVegetationExtension {")?;
    let safe = item("fn safe_normalize_xz(")?;
    let deformation = item("fn bounded_deformation(")?;
    let valid = item("fn temporal_record_valid(")?;
    let original_tag = item("fn original_mesh_tag(")?.replace(
        "mesh_functions::get_tag(instance_index)",
        "instances[instance_index].tag",
    );
    let code = item("fn temporal_code(")?;
    let phase = item("fn variation_phase(")?;
    let response = item("fn instance_wind_response(")?;
    let discard = item("fn temporal_shadow_discard(")?;
    let body = item("fn shadow_fragment(")?
        .split_once("pbr_prepass_functions::prepass_alpha_discard(in);")
        .ok_or("Missing governed alpha rejection")?
        .1
        .split_once("\n#ifdef UNCLIPPED_DEPTH_ORTHO_EMULATION")
        .ok_or("Missing governed shadow fade end")?
        .0;
    let body = body
        .replace("mesh_functions::get_tag(in.instance_index)", "input.tag")
        .replace(
            "let root = mesh_functions::get_world_from_local(in.instance_index)[3].xyz;",
            "let root = input.root;",
        )
        .replace("view.lod_view_world_position.xyz", "params.camera.xyz")
        .replace("in.position", "input.position");
    let mut io = IO.to_owned();
    if !has_uv {
        io = io.replace("@location(1) uv: vec2<f32>,", "").replace(
            "vegetation = params.vegetation;",
            "let uv = vec2<f32>(0.0); vegetation = params.vegetation;",
        );
    }
    Ok(format!(
        "{wind}\nconst TAU: f32 = 6.283185307179586;\nconst HAS_UV: bool = {has_uv};\n{safe}\n{valid}\n{original_tag}\n{code}\n{phase}\n{response}\n{deformation}\n{discard}\n{io}\n{body}\n}}"
    ))
}

const IO: &str = r"
struct Params { clip: mat4x4<f32>, camera: vec4<f32>, alpha: vec4<f32>, color: vec4<f32>,
    uv_transform: mat3x3<f32>, vegetation: VisualValleyVegetationExtension, }
struct Instance { model: mat4x4<f32>, tag: u32, pad0: u32, pad1: u32, pad2: u32, }
@group(0) @binding(0) var<uniform> params: Params;
@group(0) @binding(1) var<storage, read> instances: array<Instance>;
@group(0) @binding(2) var base_color_texture: texture_2d<f32>;
@group(0) @binding(3) var base_color_sampler: sampler;
struct TemporalLodRecord { codes: vec4<i32>, metadata: vec4<u32>, }
@group(0) @binding(4) var<storage, read> temporal_lods: array<TemporalLodRecord>;
const TAG_PHASE_MASK: u32 = 0x00ffffffu;
const TAG_PHASE_DENOMINATOR: f32 = 16777215.0;
// Same ordered ranks as Bevy's color/prepass signed coverage contract.
const DITHER_THRESHOLD_MAP: vec4<u32> = vec4(0x0a020800u, 0x060e040cu, 0x09010b03u, 0x050d070fu);
var<private> vegetation: VisualValleyVegetationExtension;
struct Output { @builtin(position) position: vec4<f32>, @location(0) uv: vec2<f32>, @location(1) root: vec3<f32>, @location(2) @interpolate(flat) tag: u32, }
@vertex fn vertex(@location(0) position: vec3<f32>, @location(1) uv: vec2<f32>,
    @builtin(instance_index) index: u32) -> Output {
    vegetation = params.vegetation;
    let instance = instances[index];
    let world = instance.model * vec4(position, 1.0);
    let height = clamp(position.y * vegetation.prototype_parameters.y, 0.0, 1.0);
    let phase = variation_phase(index);
    let response = instance_wind_response(index);
    let scale = max(length(instance.model[0].xyz), length(instance.model[2].xyz));
    let displacement = bounded_deformation(world.xyz, height * height, phase, response,
        scale, vegetation.wind_parameters.z);
    var output: Output;
    output.position = params.clip * (world + vec4(displacement, 0.0));
    output.uv = (params.uv_transform * vec3(uv, 1.0)).xy;
    output.root = instance.model[3].xyz;
    output.tag = instance.tag;
    return output;
}
@fragment fn fragment(input: Output) {
    vegetation = params.vegetation;
    var color = params.color;
    if HAS_UV && params.alpha.z > 0.5 { color *= textureSampleBias(base_color_texture, base_color_sampler, input.uv, params.alpha.w); }
    if params.alpha.x == 1.0 || params.alpha.x == 2.0 {
        if color.a < params.alpha.y { discard; }
    } else if params.alpha.x == 3.0 && all(color < vec4(params.alpha.y)) { discard; }
";

#[cfg(test)]
mod tests {
    use super::*;

    fn cascade_packet(models: &[Mat4], bounds: Option<Vec<Aabb>>) -> Packet {
        let material = VisualValleyVegetationMaterial {
            base: StandardMaterial::default(),
            extension: crate::vegetation::VisualValleyVegetationExtension::default()
                .with_governed_prototype_height(1.0)
                .unwrap(),
        };
        let mut packet = Packet::new(Handle::default(), &material, Vec3::ZERO).unwrap();
        packet.capacity = models.len();
        packet.instances = Arc::new(
            models
                .iter()
                .enumerate()
                .map(|(index, model)| Instance {
                    model: *model,
                    tag: u32::try_from(index + 7).unwrap(),
                    pad0: 0,
                    pad1: 0,
                    pad2: 0,
                })
                .collect(),
        );
        packet.bounds = bounds.map(Arc::new);
        packet
    }

    #[test]
    fn cascade_selection_keeps_each_light_volume_and_source_tags() {
        let packet = cascade_packet(
            &[
                Mat4::from_translation(Vec3::Z * 0.5),
                Mat4::from_translation(Vec3::new(10.0, 0.0, 0.5)),
            ],
            Some(vec![
                Aabb::from_min_max(-Vec3::splat(0.1), Vec3::splat(0.1));
                2
            ]),
        );
        let matrices = [
            Mat4::IDENTITY,
            Mat4::from_translation(Vec3::X * -10.0),
            Mat4::from_translation(Vec3::X * -100.0),
        ];
        let (selected, ranges) = select_cascades(&packet, &matrices);
        assert_eq!(ranges, [0..1, 1..2, 2..2]);
        assert_eq!(selected.iter().map(|i| i.tag).collect::<Vec<_>>(), [7, 8]);
        assert_eq!(selected[1].model, packet.instances[1].model);
        let key = SelectionKey {
            instances: packet.instances.clone(),
            bounds: packet.bounds.clone(),
            matrices,
        };
        assert!(key.matches(&packet, &matrices));
        let mut moved = matrices;
        moved[0] = moved[1];
        assert!(!key.matches(&packet, &moved));
        let mut changed = packet.clone();
        changed.bounds = Some(Arc::new(vec![Aabb::default(); 2]));
        assert!(!key.matches(&changed, &matrices));
    }

    #[test]
    fn cascade_selection_retains_deformation_and_unbounded_callers() {
        let mut packet = cascade_packet(
            &[Mat4::from_scale_rotation_translation(
                Vec3::new(2.0, 1.0, 0.5),
                Quat::from_rotation_y(0.35),
                Vec3::new(1.2, 0.0, 0.5),
            )],
            Some(vec![Aabb::from_min_max(
                -Vec3::splat(0.01),
                Vec3::splat(0.01),
            )]),
        );
        let matrices = [Mat4::IDENTITY; 3];
        assert!(select_cascades(&packet, &matrices).0.is_empty());
        packet.bounds = Some(Arc::new(vec![Aabb::from_min_max(
            -Vec3::splat(0.3),
            Vec3::splat(0.3),
        )]));
        assert_eq!(select_cascades(&packet, &matrices).1, [0..1, 1..2, 2..3]);
        packet.bounds = None;
        assert_eq!(select_cascades(&packet, &matrices).0.len(), 3);
    }

    #[test]
    fn a_replaced_target_cannot_inherit_or_receive_the_previous_targets_receipt() {
        let mut images = Assets::<Image>::default();
        let old = images.add(Image::default());
        let current = images.add(Image::default());
        let receipt = Arc::new(Receipt::default());
        receipt.bind_target(old.id());
        let mut frame = Frame {
            target: old,
            stamp: 10,
            enabled: true,
            packets: vec![],
            receipt: receipt.clone(),
            camera: None,
            forward: Arc::default(),
        };
        receipt.record_prepared(&frame);
        receipt.record_forward(&frame, true);
        receipt.record_encoded(&frame);
        assert_eq!(receipt.prepared_frame(), Some(11));
        assert_eq!(receipt.first_encoded_frame(), Some(11));
        receipt.bind_target(current.id());
        receipt.refuse(&frame, "old target refusal".into());
        assert!(receipt.error().is_none());
        receipt.record_prepared(&frame);
        receipt.record_forward(&frame, true);
        receipt.record_encoded(&frame);
        assert!(
            receipt.prepared_frame().is_none()
                && receipt.forward_prepared_frame().is_none()
                && receipt.encoded_frame().is_none()
                && receipt.first_encoded_frame().is_none()
        );
        frame.target = current;
        frame.stamp = 20;
        receipt.record_prepared(&frame);
        receipt.record_encoded(&frame);
        frame.stamp = 21;
        receipt.record_encoded(&frame);
        assert_eq!(receipt.prepared_frame(), Some(21));
        assert_eq!(receipt.first_encoded_frame(), Some(21));
        assert_eq!(receipt.encoded_frame(), Some(22));
    }

    #[test]
    fn extraction_accepts_only_the_postupdate_input_of_this_renderer_frame() {
        use bevy::render::{ExtractSchedule, MainWorld};
        let mut main = MainWorld::default();
        main.insert_resource(bevy::diagnostic::FrameCount(10));
        main.insert_resource(Frame {
            target: Handle::default(),
            stamp: 9,
            enabled: false,
            packets: Vec::new(),
            receipt: Arc::default(),
            camera: None,
            forward: Arc::default(),
        });
        let mut render = World::new();
        render.insert_resource(main);
        let mut schedule = Schedule::new(ExtractSchedule);
        schedule.add_systems(extract);
        schedule.run(&mut render);
        assert_eq!(render.resource::<Frame>().stamp, 9);
        render
            .resource_mut::<MainWorld>()
            .resource_mut::<bevy::diagnostic::FrameCount>()
            .0 = 11;
        schedule.run(&mut render);
        assert!(!render.contains_resource::<Frame>());
        render
            .resource_mut::<MainWorld>()
            .resource_mut::<Frame>()
            .stamp = 11;
        schedule.run(&mut render);
        assert!(!render.contains_resource::<Frame>());
        render
            .resource_mut::<MainWorld>()
            .resource_mut::<Frame>()
            .stamp = 10;
        schedule.run(&mut render);
        assert_eq!(render.resource::<Frame>().stamp, 10);
        render
            .resource_mut::<MainWorld>()
            .remove_resource::<Frame>();
        schedule.run(&mut render);
        assert!(!render.contains_resource::<Frame>());
    }

    #[derive(Clone, Debug)]
    struct Coverage {
        pixels: usize,
        centroid: f64,
        hash: forge_core::ContentHash,
    }

    fn advance(app: &mut App, updates: usize) {
        for _ in 0..updates {
            app.update();
            std::thread::sleep(std::time::Duration::from_millis(1));
        }
    }

    #[allow(
        clippy::cast_precision_loss,
        reason = "At most 2048 squared texels, exactly representable in f64"
    )]
    fn read_coverage(app: &mut App, target: Handle<Image>) -> Coverage {
        use bevy::render::gpu_readback::{Readback, ReadbackComplete};
        let result = Arc::new(std::sync::Mutex::new(None));
        let reply = result.clone();
        let entity = app
            .world_mut()
            .spawn(Readback::texture(target))
            .observe(move |event: On<ReadbackComplete>| {
                let bytes = event
                    .data
                    .get(..2048 * 2048 * 4)
                    .expect("one full source cascade");
                let mut pixels = 0;
                let mut x_sum = 0_usize;
                for (index, word) in bytes.as_chunks::<4>().0.iter().enumerate() {
                    let value = f32::from_le_bytes(*word);
                    assert!(value.is_finite());
                    if value < 0.99 {
                        pixels += 1;
                        x_sum += index % 2048;
                    }
                }
                *reply.lock().unwrap() = Some(Coverage {
                    pixels,
                    centroid: x_sum as f64 / pixels.max(1) as f64,
                    hash: forge_core::bytes_sha256(bytes),
                });
            })
            .id();
        let started = std::time::Instant::now();
        loop {
            advance(app, 1);
            if let Some(value) = result.lock().unwrap().take() {
                app.world_mut().despawn(entity);
                return value;
            }
            assert!(
                started.elapsed().as_secs() < 30,
                "source depth readback timed out"
            );
        }
    }

    #[test]
    #[ignore = "Requires the native Bevy renderer and a hardware GPU"]
    #[allow(
        clippy::too_many_lines,
        reason = "One resident-mesh GPU lifecycle checks opaque coverage, the real alpha texture and forward wind without changing its device"
    )]
    fn shared_cascades_use_resident_species_meshes_alpha_and_forward_wind() {
        use crate::source_shadows::casters::graph;
        use bevy::{
            asset::RenderAssetUsages,
            render::render_resource::{Extent3d, TextureDimension, TextureFormat},
        };
        let mut app = App::new();
        app.add_plugins(
            DefaultPlugins
                .set(WindowPlugin {
                    primary_window: None,
                    exit_condition: bevy::window::ExitCondition::DontExit,
                    ..default()
                })
                .disable::<bevy::winit::WinitPlugin>()
                .disable::<bevy::render::pipelined_rendering::PipelinedRenderingPlugin>(),
        );
        crate::vegetation::install_temporal_material(&mut app);
        let view_target =
            app.world_mut()
                .resource_mut::<Assets<Image>>()
                .add(Image::new_target_texture(
                    16,
                    16,
                    TextureFormat::Rgba8UnormSrgb,
                    None,
                ));
        let camera = app
            .world_mut()
            .spawn((
                Camera3d::default(),
                Camera::default(),
                bevy::camera::RenderTarget::Image(view_target.into()),
                Msaa::Off,
                Transform::default(),
                bevy::core_pipeline::prepass::DepthPrepass,
                bevy::core_pipeline::prepass::MotionVectorPrepass,
            ))
            .id();
        let mesh = app
            .world_mut()
            .resource_mut::<Assets<Mesh>>()
            .add(Cuboid::new(0.8, 0.8, 0.1));
        let mut without_uv = Mesh::from(Cuboid::new(0.8, 0.8, 0.1));
        without_uv.remove_attribute(Mesh::ATTRIBUTE_UV_0);
        let without_uv = app
            .world_mut()
            .resource_mut::<Assets<Mesh>>()
            .add(without_uv);
        let texture = app
            .world_mut()
            .resource_mut::<Assets<Image>>()
            .add(Image::new(
                Extent3d {
                    width: 2,
                    height: 2,
                    depth_or_array_layers: 1,
                },
                TextureDimension::D2,
                vec![
                    255, 255, 255, 0, 255, 255, 255, 255, 255, 255, 255, 0, 255, 255, 255, 255,
                ],
                TextureFormat::Rgba8UnormSrgb,
                RenderAssetUsages::default(),
            ));
        app.world_mut()
            .resource_mut::<Assets<ShaderBuffer>>()
            .insert(
                crate::temporal_lod::render::BUFFER.id(),
                ShaderBuffer::from(vec![[0_u32; 4]; 2]),
            )
            .unwrap();
        let settings = crate::source_shadows::ObjectShadowSettings {
            enabled: 1.0,
            ..default()
        };
        let target = graph::install(
            &mut app,
            graph::Input {
                matrices: settings.matrices[..3].to_vec(),
                eyes: vec![Vec4::ZERO; 3],
                settings,
                draws: vec![],
                terrain: None,
                plants: None,
            },
        )
        .unwrap();
        let mut material = VisualValleyVegetationMaterial {
            base: StandardMaterial {
                base_color_texture: Some(texture),
                ..default()
            },
            extension: crate::vegetation::VisualValleyVegetationExtension::default()
                .with_governed_prototype_height(1.0)
                .unwrap(),
        };
        material.extension.shadow_fade_distances = Vec4::new(10.0, 20.0, 0.0, 0.0);
        let mut packet = Packet::new(mesh.clone(), &material, Vec3::ZERO).unwrap();
        material.base.alpha_mode = AlphaMode::Mask(0.5);
        let material = app
            .world_mut()
            .resource_mut::<Assets<VisualValleyVegetationMaterial>>()
            .add(material);
        packet.capacity = 1;
        packet.instances = Arc::new(vec![Instance {
            model: Mat4::from_translation(Vec3::Z * 0.5),
            tag: crate::vegetation::mesh_tag_for(1.4, 1.0).0,
            pad0: 0,
            pad1: 0,
            pad2: 0,
        }]);
        let receipt = Arc::new(Receipt::default());
        receipt.bind_target(target.image.id());
        app.insert_resource(Frame {
            target: target.image.clone(),
            stamp: 0,
            enabled: false,
            packets: vec![packet],
            receipt: receipt.clone(),
            camera: Some(camera),
            forward: Arc::new(vec![ForwardRecipe {
                mesh,
                material: material.untyped(),
                material_type: std::any::TypeId::of::<VisualValleyVegetationMaterial>(),
                crossfade: true,
            }]),
        });
        app.add_systems(
            PostUpdate,
            |mut frame: ResMut<Frame>, stamp: Res<bevy::diagnostic::FrameCount>| {
                frame.stamp = stamp.0;
            },
        );
        app.finish();
        app.cleanup();
        let adapter = app
            .sub_app(bevy::render::RenderApp)
            .world()
            .resource::<bevy::render::renderer::RenderAdapterInfo>();
        assert_ne!(adapter.device_type, wgpu::DeviceType::Cpu);
        eprintln!(
            "species preparation adapter: {} {:?} {:?}",
            adapter.name, adapter.device_type, adapter.backend
        );
        let started = std::time::Instant::now();
        while receipt.prepared_frame().is_none() || receipt.forward_prepared_frame().is_none() {
            advance(&mut app, 1);
            assert!(receipt.error().is_none(), "{:?}", receipt.error());
            assert!(
                started.elapsed().as_secs() < 30,
                "resident species shadow preparation timed out"
            );
        }
        assert!(
            receipt.encoded_frame().is_none(),
            "hidden preparation must not cast species shadows"
        );
        let hidden = read_coverage(&mut app, target.image.clone());
        assert_eq!(hidden.pixels, 0);
        app.world_mut().resource_mut::<Frame>().enabled = true;
        while receipt.encoded_frame().is_none() {
            advance(&mut app, 1);
            assert!(
                started.elapsed().as_secs() < 30,
                "species visibility encoding timed out"
            );
        }
        let opaque = read_coverage(&mut app, target.image.clone());
        assert!(
            opaque.pixels > 100_000 && opaque.pixels < 4_000_000,
            "{opaque:?}"
        );
        app.world_mut().resource_mut::<Frame>().packets[0].bounds =
            Some(Arc::new(vec![Aabb::from_min_max(
                -Vec3::splat(0.401),
                Vec3::splat(0.401),
            )]));
        advance(&mut app, 4);
        let bounded = read_coverage(&mut app, target.image.clone());
        assert_eq!(
            bounded.hash, opaque.hash,
            "cascade culling changed depth coverage"
        );
        app.world_mut().resource_mut::<Frame>().packets[0].bounds = None;
        let original_mesh = app.world().resource::<Frame>().packets[0].mesh.clone();
        app.world_mut().resource_mut::<Frame>().packets[0].mesh = without_uv;
        advance(&mut app, 4);
        let untextured = read_coverage(&mut app, target.image.clone());
        assert_eq!(untextured.hash, opaque.hash);
        app.world_mut().resource_mut::<Frame>().packets[0].mesh = original_mesh;
        app.world_mut().resource_mut::<Frame>().packets[0]
            .params
            .alpha
            .x = 1.0;
        app.world_mut().resource_mut::<Frame>().packets[0]
            .params
            .alpha
            .y = 0.5;
        advance(&mut app, 4);
        let masked = read_coverage(&mut app, target.image.clone());
        assert!(
            masked.pixels > opaque.pixels / 5 && masked.pixels < opaque.pixels * 4 / 5,
            "{opaque:?} {masked:?}"
        );
        app.world_mut().resource_mut::<Frame>().packets[0]
            .params
            .vegetation
            .wind_parameters
            .x = 0.8;
        advance(&mut app, 4);
        let wind = read_coverage(&mut app, target.image);
        assert_ne!(masked.hash, wind.hash);
        assert!(
            (wind.centroid - masked.centroid).abs() > 2.0,
            "{masked:?} {wind:?}"
        );
        assert!(receipt.prepared_frame().is_some() && receipt.encoded_frame().is_some());
        eprintln!(
            "resident source species coverage: opaque={opaque:?}; mask={masked:?}; wind={wind:?}"
        );
        advance(&mut app, 4);
    }
    #[test]
    fn governed_depth_keeps_forward_deformation_and_validates_its_real_shader() {
        for has_uv in [false, true] {
            let source = shader(has_uv).unwrap();
            let module = naga::front::wgsl::parse_str(&source).unwrap();
            naga::valid::Validator::new(
                naga::valid::ValidationFlags::all(),
                naga::valid::Capabilities::all(),
            )
            .validate(&module)
            .unwrap();
            assert!(
                source.contains("bounded_deformation(world.xyz, height * height, phase, response")
            );
            assert!(source.contains("let root = input.root;"));
            assert!(!source.contains("mesh_functions::"));
            assert!(source.contains("textureSampleBias(base_color_texture"));
        }
    }
}
