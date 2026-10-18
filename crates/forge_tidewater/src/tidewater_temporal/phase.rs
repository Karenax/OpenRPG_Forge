// SPDX-License-Identifier: MIT OR Apache-2.0
//! Real mesh draws for the source temporal auxiliary buffers.
//!
//! The ordinary Bevy mesh allocator, material bindings and phase preprocessing
//! remain authoritative. Group 1 is reserved for the temporal view packet;
//! derived shaders must not import the ordinary mesh-view binding array there.
use std::{
    any::{Any, TypeId, type_name},
    collections::{HashMap, HashSet},
    marker::PhantomData,
    ops::Range,
    sync::{
        Arc, Mutex,
        atomic::{AtomicU32, Ordering},
    },
};

use bevy::{
    asset::UntypedAssetId,
    core_pipeline::schedule::{Core3d, Core3dSystems},
    ecs::{
        entity::EntityHash,
        query::ROQueryItem,
        system::{
            SystemParamItem,
            lifetimeless::{Read, SRes},
        },
    },
    material::{
        MaterialProperties,
        key::{ErasedMaterialPipelineKey, ErasedMeshPipelineKey},
    },
    mesh::{Mesh3d, MeshVertexBufferLayoutRef},
    pbr::{
        DrawMesh, Material, MaterialPipeline, MeshInputUniform, MeshPipeline, MeshPipelineKey,
        MeshPipelineSystems, MeshUniform, PreparedMaterial, PreprocessBindGroups,
        RenderMaterialInstances, RenderMeshInstances, SetMaterialBindGroup, SetMeshBindGroup,
        SetMeshViewBindGroup, StandardMaterialKey, ViewKeyCache, alpha_mode_pipeline_key,
        get_mesh_instance_world_from_local,
    },
    prelude::*,
    render::{
        Render, RenderApp, RenderDebugFlags, RenderStartup, RenderSystems,
        batching::gpu_preprocessing::BatchedInstanceBuffers,
        erased_render_asset::ErasedRenderAssets,
        mesh::{RenderMesh, allocator::MeshAllocator},
        render_asset::RenderAssets,
        render_phase::{
            AddRenderCommand, CachedRenderPipelinePhaseItem, DrawFunctionId, DrawFunctions,
            PhaseItem, PhaseItemExtraIndex, RenderCommand, RenderCommandResult, SetItemPipeline,
            SortedPhaseItem, SortedRenderPhasePlugin, TrackedRenderPass, ViewSortedRenderPhases,
            sort_phase_system,
        },
        render_resource::{
            BindGroup, BindGroupEntries, BindGroupLayoutDescriptor, BindGroupLayoutEntries,
            BlendState, CachedRenderPipelineId, ColorTargetState, ColorWrites,
            DynamicUniformBuffer, PipelineCache, RenderPipelineDescriptor, ShaderStages,
            SpecializedMeshPipeline, SpecializedMeshPipelineError, SpecializedMeshPipelines,
            TextureFormat, binding_types::uniform_buffer,
        },
        renderer::{RenderDevice, RenderQueue},
        sync_world::MainEntity,
        view::{ExtractedView, visibility::RenderVisibleEntities},
    },
    shader::{Shader, ShaderDefVal},
};
use indexmap::IndexMap;

use super::frame::{Packet, ViewPacket};

/// Required order of the source's late auxiliary blend.
#[derive(Clone, Copy, Debug, Hash, PartialEq, Eq)]
pub enum MaterialRole {
    Opaque,
    Water,
    Transparent,
}

impl MaterialRole {
    const fn lane(self) -> u8 {
        match self {
            Self::Opaque => 0,
            Self::Water => 1,
            Self::Transparent => 2,
        }
    }
}

/// One material family can contain opaque meshes and late alpha meshes, such
/// as the village fishing net. Its real prepared alpha mode decides the lane.
fn effective_role(registration: MaterialRole, alpha: AlphaMode) -> MaterialRole {
    if registration == MaterialRole::Water {
        return MaterialRole::Water;
    }
    if matches!(
        alpha,
        AlphaMode::Blend | AlphaMode::Premultiplied | AlphaMode::Add | AlphaMode::Multiply
    ) {
        MaterialRole::Transparent
    } else {
        registration
    }
}

/// Separate stages preserve the original source's imports and vertex layout.
#[derive(Clone, Debug)]
pub struct AuxShaders {
    pub vertex: Handle<Shader>,
    pub fragment: Handle<Shader>,
}

#[derive(Clone)]
struct Registration {
    shaders: AuxShaders,
    role: MaterialRole,
    material_type: &'static str,
    contract: MaterialContract,
}

#[derive(Clone, Copy)]
enum MaterialContract {
    DerivedSource,
    StandardOpaque,
}

#[derive(Resource, Default)]
struct Registry(HashMap<TypeId, Registration>, HashSet<TypeId>);

/// Concrete material types must be registered, including native probe types.
/// Unknown material types are counted as unsupported; they are never replaced
/// with camera-only geometry or silently omitted from a ready observation.
pub fn register_material<M: Material>(
    app: &mut App,
    shaders: AuxShaders,
    role: MaterialRole,
) -> Result<(), String> {
    let Some(render_app) = app.get_sub_app_mut(RenderApp) else {
        return Err("Temporal auxiliary materials require Bevy RenderApp".into());
    };
    render_app.init_resource::<Registry>();
    render_app.world_mut().resource_mut::<Registry>().0.insert(
        TypeId::of::<M>(),
        Registration {
            shaders,
            role,
            material_type: type_name::<M>(),
            contract: MaterialContract::DerivedSource,
        },
    );
    Ok(())
}

/// Explicit support for rigid opaque `StandardMaterial` geometry. The original
/// material's prepared layout, culling and depth bias remain authoritative.
/// Alpha clipping/blending, transmission, skin/morph, dither and deferred
/// geometry are refused until their actual auxiliary contracts are implemented.
pub fn register_standard_opaque(app: &mut App) -> Result<(), String> {
    if app.get_sub_app(RenderApp).is_none() {
        return Err("Temporal standard geometry requires Bevy RenderApp".into());
    }
    let shader = app
        .world_mut()
        .resource_mut::<Assets<Shader>>()
        .add(Shader::from_wgsl(
            format!(
                "{}\n{}",
                include_str!("packet.wgsl"),
                include_str!("standard.wgsl")
            ),
            "tidewater_temporal/standard.wgsl",
        ));
    register_material::<StandardMaterial>(
        app,
        AuxShaders {
            vertex: shader.clone(),
            fragment: shader,
        },
        MaterialRole::Opaque,
    )?;
    app.sub_app_mut(RenderApp)
        .world_mut()
        .resource_mut::<Registry>()
        .0
        .get_mut(&TypeId::of::<StandardMaterial>())
        .expect("Registered above")
        .contract = MaterialContract::StandardOpaque;
    Ok(())
}

/// Register a known source sky material covered by the explicit direction-W0
/// fullscreen sky renderer. This does not classify unknown meshes as sky.
pub fn register_sky_material<M: Material>(app: &mut App) -> Result<(), String> {
    let Some(render_app) = app.get_sub_app_mut(RenderApp) else {
        return Err("Temporal auxiliary sky requires Bevy RenderApp".into());
    };
    render_app.init_resource::<Registry>();
    render_app
        .world_mut()
        .resource_mut::<Registry>()
        .1
        .insert(TypeId::of::<M>());
    Ok(())
}

/// Public ordering seam for the parent camera extraction / target allocator.
#[derive(SystemSet, Debug, Clone, Copy, Hash, PartialEq, Eq)]
pub enum TemporalAuxSystems {
    PreparePacket,
    Queue,
    Sort,
    Render,
}

/// Counts are scene coverage, not GPU fence or visual acceptance evidence.
#[derive(Component, Clone, Debug, Default)]
pub struct AuxCoverage {
    pub visible: u32,
    pub sky_visible: u32,
    pub registered: u32,
    pub queued: u32,
    pub opaque: u32,
    pub water: u32,
    pub transparent: u32,
    pub unsupported: u32,
    pub pending: u32,
    pub errors: Vec<String>,
    pub diagnostics: Vec<AuxDiagnostic>,
    pub diagnostics_omitted: u32,
}

/// Exact identities at the failed queue/command branch. No names or module
/// slugs are used to decide coverage. `MainWorld` can resolve these entity bits.
#[derive(Clone, Debug, serde::Serialize)]
pub struct AuxDiagnostic {
    pub stage: String,
    pub main_entity_bits: u64,
    pub render_entity_bits: u64,
    pub material_asset: Option<String>,
    pub material_type: Option<String>,
    pub mesh_asset: Option<String>,
    pub pipeline: Option<String>,
    pub reason: String,
}

const MAX_DIAGNOSTICS: usize = 64;

fn record_diagnostic(
    entries: &mut Vec<AuxDiagnostic>,
    omitted: &mut u32,
    mut entry: AuxDiagnostic,
) {
    if entries.len() == MAX_DIAGNOSTICS {
        *omitted = omitted.saturating_add(1);
        return;
    }
    entry.reason = entry.reason.chars().take(4096).collect();
    entries.push(entry);
}

impl AuxCoverage {
    fn diagnostic(&mut self, entry: AuxDiagnostic) {
        record_diagnostic(&mut self.diagnostics, &mut self.diagnostics_omitted, entry);
    }
    #[must_use]
    pub fn complete(&self) -> bool {
        self.queued > 0
            && self.unsupported == 0
            && self.pending == 0
            && self.errors.is_empty()
            && self.queued == self.visible
    }
}

/// The counter increments only after the real public `DrawMesh` command succeeds.
#[derive(Component, Clone, Default)]
pub struct AuxDrawCounter {
    draws: Arc<AtomicU32>,
    diagnostics: Arc<Mutex<DrawDiagnostics>>,
}

#[derive(Default)]
struct DrawDiagnostics {
    entries: Vec<AuxDiagnostic>,
    omitted: u32,
}

impl AuxDrawCounter {
    pub fn reset(&self) {
        self.draws.store(0, Ordering::Release);
        if let Ok(mut diagnostics) = self.diagnostics.lock() {
            *diagnostics = DrawDiagnostics::default();
        }
    }
    #[must_use]
    pub fn draws(&self) -> u32 {
        self.draws.load(Ordering::Acquire)
    }
    #[must_use]
    pub fn diagnostics(&self) -> (Vec<AuxDiagnostic>, u32) {
        self.diagnostics.lock().map_or_else(
            |_| (Vec::new(), 1),
            |diagnostics| (diagnostics.entries.clone(), diagnostics.omitted),
        )
    }
    fn record(&self, entry: AuxDiagnostic) {
        if let Ok(mut diagnostics) = self.diagnostics.lock() {
            let DrawDiagnostics { entries, omitted } = &mut *diagnostics;
            record_diagnostic(entries, omitted, entry);
        }
    }
}

#[derive(Component, Clone, Copy)]
pub struct PacketOffset(pub u32);

#[derive(Resource)]
pub struct PacketUniforms {
    pub layout: BindGroupLayoutDescriptor,
    pub binding: Option<BindGroup>,
    buffer: DynamicUniformBuffer<Packet>,
}

#[derive(Clone, Hash, PartialEq, Eq)]
struct AuxKey {
    material: ErasedMaterialPipelineKey,
    vertex: AssetId<Shader>,
    fragment: AssetId<Shader>,
    role: MaterialRole,
}

#[derive(Resource)]
struct AuxPipeline {
    original: MaterialPipeline,
    packet_layout: BindGroupLayoutDescriptor,
    properties: HashMap<AuxKey, Arc<MaterialProperties>>,
    stages: HashMap<AuxKey, AuxShaders>,
}

impl SpecializedMeshPipeline for AuxPipeline {
    type Key = AuxKey;

    fn specialize(
        &self,
        key: Self::Key,
        layout: &MeshVertexBufferLayoutRef,
    ) -> Result<RenderPipelineDescriptor, SpecializedMeshPipelineError> {
        let properties = &self.properties[&key];
        let shaders = &self.stages[&key];
        let mesh_key: MeshPipelineKey = key.material.mesh_key.downcast();
        let mut descriptor = self.original.mesh_pipeline.specialize(mesh_key, layout)?;
        descriptor
            .vertex
            .shader_defs
            .push(ShaderDefVal::UInt("MATERIAL_BIND_GROUP".into(), 3));
        if let Some(fragment) = &mut descriptor.fragment {
            fragment
                .shader_defs
                .push(ShaderDefVal::UInt("MATERIAL_BIND_GROUP".into(), 3));
        }
        descriptor.layout.insert(
            3,
            properties
                .material_layout
                .as_ref()
                .expect("Only materials with public prepared layouts are queued")
                .clone(),
        );
        // Preserve exact material specialization (vertex attributes, culling,
        // shader defines, clipping and depth writes) before replacing MRTs.
        if let Some(specialize) = properties.user_specialize {
            specialize(
                &self.original as &dyn Any,
                &mut descriptor,
                layout,
                key.material.clone(),
            )?;
        }
        if properties.bindless {
            descriptor.vertex.shader_defs.push("BINDLESS".into());
            if let Some(fragment) = &mut descriptor.fragment {
                fragment.shader_defs.push("BINDLESS".into());
            }
        }
        descriptor.label = Some("Tidewater temporal real-material auxiliary".into());
        descriptor.layout[1] = self.packet_layout.clone();
        descriptor.vertex.shader = shaders.vertex.clone();
        descriptor.vertex.entry_point = Some("vertex".into());
        let fragment = descriptor
            .fragment
            .as_mut()
            .expect("Mesh pipeline has fragment stage");
        fragment.shader = shaders.fragment.clone();
        fragment.entry_point = Some("fragment".into());
        fragment.targets = auxiliary_color_targets(key.role);
        if let Some(depth) = &mut descriptor.depth_stencil {
            depth.format = TextureFormat::Depth32Float;
        }
        descriptor.multisample.count = 1;
        descriptor.multisample.alpha_to_coverage_enabled = false;
        Ok(descriptor)
    }
}

/// Source mask always uses normal alpha blend, including opaque zero-alpha masks.
/// Late velocity is premultiplied, while opaque / water velocity replaces pixels.
#[must_use]
pub fn auxiliary_color_targets(role: MaterialRole) -> Vec<Option<ColorTargetState>> {
    vec![
        Some(ColorTargetState {
            format: TextureFormat::Rgba16Float,
            blend: (role == MaterialRole::Transparent)
                .then_some(BlendState::PREMULTIPLIED_ALPHA_BLENDING),
            write_mask: ColorWrites::ALL,
        }),
        Some(ColorTargetState {
            format: TextureFormat::Rgba8Unorm,
            blend: Some(BlendState::ALPHA_BLENDING),
            write_mask: ColorWrites::ALL,
        }),
    ]
}

/// Real mesh instance indices are populated by `SortedRenderPhasePlugin`, using
/// `MeshPipeline`'s public `GetFullBatchData` implementation for CPU or GPU draws.
pub struct AuxItem {
    pub entity: (Entity, MainEntity),
    pub pipeline: CachedRenderPipelineId,
    pub draw_function: DrawFunctionId,
    pub role: MaterialRole,
    pub center: Vec3,
    pub depth_bias: f32,
    pub distance: f32,
    pub indexed: bool,
    pub batch_range: Range<u32>,
    pub extra_index: PhaseItemExtraIndex,
    pub material_asset: UntypedAssetId,
    pub material_type: &'static str,
    pub mesh_asset: AssetId<Mesh>,
}

impl AuxItem {
    fn diagnostic(&self, stage: &str, reason: String) -> AuxDiagnostic {
        AuxDiagnostic {
            stage: stage.into(),
            main_entity_bits: self.entity.1.id().to_bits(),
            render_entity_bits: self.entity.0.to_bits(),
            material_asset: Some(format!("{:?}", self.material_asset)),
            material_type: Some(self.material_type.into()),
            mesh_asset: Some(format!("{:?}", self.mesh_asset)),
            pipeline: Some(format!("{:?}", self.pipeline)),
            reason,
        }
    }
}

impl PhaseItem for AuxItem {
    // Avoid a batch straddling the opaque/hull/water range boundary.
    const AUTOMATIC_BATCHING: bool = false;
    fn entity(&self) -> Entity {
        self.entity.0
    }
    fn main_entity(&self) -> MainEntity {
        self.entity.1
    }
    fn draw_function(&self) -> DrawFunctionId {
        self.draw_function
    }
    fn batch_range(&self) -> &Range<u32> {
        &self.batch_range
    }
    fn batch_range_mut(&mut self) -> &mut Range<u32> {
        &mut self.batch_range
    }
    fn extra_index(&self) -> PhaseItemExtraIndex {
        self.extra_index.clone()
    }
    fn batch_range_and_extra_index_mut(&mut self) -> (&mut Range<u32>, &mut PhaseItemExtraIndex) {
        (&mut self.batch_range, &mut self.extra_index)
    }
}

impl SortedPhaseItem for AuxItem {
    type SortKey = (u8, bevy::math::FloatOrd, u64);
    fn sort_key(&self) -> Self::SortKey {
        (
            self.role.lane(),
            bevy::math::FloatOrd(self.distance),
            self.entity.1.id().to_bits(),
        )
    }
    fn recalculate_sort_keys(
        items: &mut IndexMap<(Entity, MainEntity), Self, EntityHash>,
        view: &ExtractedView,
    ) {
        let rangefinder = view.rangefinder3d();
        for item in items.values_mut() {
            item.distance = if item.role == MaterialRole::Transparent {
                rangefinder.distance(&item.center) + item.depth_bias
            } else {
                0.0
            };
        }
    }
    fn indexed(&self) -> bool {
        self.indexed
    }
}

impl CachedRenderPipelinePhaseItem for AuxItem {
    fn cached_pipeline(&self) -> CachedRenderPipelineId {
        self.pipeline
    }
}

pub struct SetTemporalPacket<const I: usize>;
impl<P: PhaseItem, const I: usize> RenderCommand<P> for SetTemporalPacket<I> {
    type Param = SRes<PacketUniforms>;
    type ViewQuery = Read<PacketOffset>;
    type ItemQuery = ();
    fn render<'w>(
        _: &P,
        offset: ROQueryItem<'w, '_, Self::ViewQuery>,
        _: Option<()>,
        uniforms: SystemParamItem<'w, '_, Self::Param>,
        pass: &mut TrackedRenderPass<'w>,
    ) -> RenderCommandResult {
        let Some(binding) = &uniforms.into_inner().binding else {
            return RenderCommandResult::Skip;
        };
        pass.set_bind_group(I, binding, &[offset.0]);
        RenderCommandResult::Success
    }
}

/// Preserve each public command's actual result while recording the first
/// command which causes a mesh to be skipped. A phase-level draw error cannot
/// otherwise identify a successful tuple whose inner command returned Skip.
pub struct ObserveCommand<C>(PhantomData<C>);
impl<C: RenderCommand<AuxItem>> RenderCommand<AuxItem> for ObserveCommand<C> {
    type Param = (C::Param, SRes<PipelineCache>);
    type ViewQuery = (C::ViewQuery, Read<AuxDrawCounter>);
    type ItemQuery = C::ItemQuery;
    fn render<'w>(
        item: &AuxItem,
        (view, counter): ROQueryItem<'w, '_, Self::ViewQuery>,
        entity: Option<ROQueryItem<'w, '_, Self::ItemQuery>>,
        (param, cache): SystemParamItem<'w, '_, Self::Param>,
        pass: &mut TrackedRenderPass<'w>,
    ) -> RenderCommandResult {
        let result = C::render(item, view, entity, param, pass);
        if !matches!(result, RenderCommandResult::Success) {
            let reason = format!(
                "{result:?}; pipeline state: {:?}",
                cache.into_inner().get_render_pipeline_state(item.pipeline)
            );
            counter.record(item.diagnostic(type_name::<C>(), reason));
        }
        result
    }
}

pub struct DrawCountedMesh;
impl RenderCommand<AuxItem> for DrawCountedMesh {
    type Param = <DrawMesh as RenderCommand<AuxItem>>::Param;
    type ViewQuery = (Has<PreprocessBindGroups>, Read<AuxDrawCounter>);
    type ItemQuery = ();
    fn render<'w>(
        item: &AuxItem,
        (preprocess, counter): ROQueryItem<'w, '_, Self::ViewQuery>,
        entity: Option<()>,
        param: SystemParamItem<'w, '_, Self::Param>,
        pass: &mut TrackedRenderPass<'w>,
    ) -> RenderCommandResult {
        let result =
            <DrawMesh as RenderCommand<AuxItem>>::render(item, preprocess, entity, param, pass);
        if matches!(result, RenderCommandResult::Success) {
            counter.draws.fetch_add(1, Ordering::AcqRel);
        }
        result
    }
}

type DrawAuxMaterial = (
    ObserveCommand<SetItemPipeline>,
    ObserveCommand<SetMeshViewBindGroup<0>>,
    ObserveCommand<SetTemporalPacket<1>>,
    ObserveCommand<SetMeshBindGroup<2>>,
    ObserveCommand<SetMaterialBindGroup<3>>,
    ObserveCommand<DrawCountedMesh>,
);

/// Installs only render systems; the parent owns packet extraction, history and
/// camera epoch resets. Must run after Bevy's rendering/PBR plugins are present.
pub fn install_render(app: &mut App, sky_shader: Handle<Shader>) -> Result<(), String> {
    if app.get_sub_app(RenderApp).is_none() {
        return Err("Temporal auxiliary rendering requires RenderApp".into());
    }
    super::render::install_scene_contract(app);
    let observations = super::render::AuxObservations::default();
    app.insert_resource(observations.clone());
    app.add_plugins(SortedRenderPhasePlugin::<AuxItem, MeshPipeline>::new(
        RenderDebugFlags::default(),
    ));
    let render_app = app.sub_app_mut(RenderApp);
    render_app
        .init_resource::<Registry>()
        .init_resource::<DrawFunctions<AuxItem>>()
        .init_resource::<SpecializedMeshPipelines<AuxPipeline>>()
        .insert_resource(super::render::SkyShader(sky_shader))
        .insert_resource(observations)
        .add_render_command::<AuxItem, DrawAuxMaterial>()
        .add_systems(
            RenderStartup,
            (initialize, super::render::initialize_sky)
                .chain()
                .after(MeshPipelineSystems),
        )
        .add_systems(
            Render,
            (
                queue
                    .in_set(RenderSystems::Queue)
                    .in_set(TemporalAuxSystems::Queue),
                sort_phase_system::<AuxItem>
                    .in_set(RenderSystems::PhaseSort)
                    .in_set(TemporalAuxSystems::Sort),
                prepare_packet
                    .in_set(RenderSystems::PrepareResources)
                    .in_set(TemporalAuxSystems::PreparePacket),
            ),
        )
        .add_systems(
            Core3d,
            super::render::encode
                .after(Core3dSystems::MainPass)
                .before(Core3dSystems::EarlyPostProcess)
                .in_set(TemporalAuxSystems::Render),
        );
    Ok(())
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy startup systems receive render resources by value"
)]
fn initialize(mut commands: Commands, mesh: Res<MeshPipeline>, device: Res<RenderDevice>) {
    let layout = BindGroupLayoutDescriptor::new(
        "Tidewater temporal view packet",
        &BindGroupLayoutEntries::single(
            ShaderStages::VERTEX_FRAGMENT,
            uniform_buffer::<Packet>(true),
        ),
    );
    commands.insert_resource(AuxPipeline {
        original: MaterialPipeline {
            mesh_pipeline: mesh.clone(),
        },
        packet_layout: layout.clone(),
        properties: HashMap::new(),
        stages: HashMap::new(),
    });
    commands.insert_resource(PacketUniforms {
        layout,
        binding: None,
        buffer: DynamicUniformBuffer::new_with_alignment(u64::from(
            device.limits().min_uniform_buffer_offset_alignment,
        )),
    });
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy packet preparation receives resources and queries by value"
)]
fn prepare_packet(
    mut commands: Commands,
    views: Query<(Entity, &ViewPacket)>,
    mut uniforms: ResMut<PacketUniforms>,
    device: Res<RenderDevice>,
    queue: Res<RenderQueue>,
    cache: Res<PipelineCache>,
) {
    uniforms.buffer.clear();
    for (entity, packet) in &views {
        let offset = uniforms.buffer.push(&packet.packet);
        commands.entity(entity).insert(PacketOffset(offset));
    }
    if uniforms.buffer.is_empty() {
        uniforms.binding = None;
        return;
    }
    uniforms.buffer.write_buffer(&device, &queue);
    uniforms.binding = uniforms.buffer.binding().map(|binding| {
        device.create_bind_group(
            "Tidewater temporal view packet",
            &cache.get_bind_group_layout(&uniforms.layout),
            &BindGroupEntries::single(binding),
        )
    });
}

#[allow(
    clippy::too_many_arguments,
    clippy::needless_pass_by_value,
    clippy::too_many_lines,
    reason = "Bevy render queue systems receive all resource/query parameters by value"
)]
fn queue(
    mut commands: Commands,
    registry: Res<Registry>,
    materials: Res<ErasedRenderAssets<PreparedMaterial>>,
    material_instances: Res<RenderMaterialInstances>,
    mesh_instances: Res<RenderMeshInstances>,
    meshes: Res<RenderAssets<RenderMesh>>,
    allocator: Res<MeshAllocator>,
    batched: Option<Res<BatchedInstanceBuffers<MeshUniform, MeshInputUniform>>>,
    view_keys: Res<ViewKeyCache>,
    draw_functions: Res<DrawFunctions<AuxItem>>,
    cache: Res<PipelineCache>,
    mut pipeline: ResMut<AuxPipeline>,
    mut specialized: ResMut<SpecializedMeshPipelines<AuxPipeline>>,
    mut phases: ResMut<ViewSortedRenderPhases<AuxItem>>,
    views: Query<(Entity, &ExtractedView, &RenderVisibleEntities), With<ViewPacket>>,
) {
    let draw_function = draw_functions.read().id::<DrawAuxMaterial>();
    let mut live_views = Vec::new();
    for (entity, view, visible) in &views {
        live_views.push(view.retained_view_entity);
        phases.prepare_for_new_frame(view.retained_view_entity);
        let phase = phases
            .get_mut(&view.retained_view_entity)
            .expect("Prepared phase");
        phase.clear();
        let mut coverage = AuxCoverage::default();
        let Some(view_key) = view_keys.get(&view.retained_view_entity) else {
            coverage
                .errors
                .push("Main mesh-view pipeline key is not prepared".into());
            commands
                .entity(entity)
                .insert((coverage, AuxDrawCounter::default()));
            continue;
        };
        if view_key.msaa_samples() != 1 {
            coverage
                .errors
                .push("MSAA auxiliary depth resolve is not implemented".into());
            commands
                .entity(entity)
                .insert((coverage, AuxDrawCounter::default()));
            continue;
        }
        let Some(visible_meshes) = visible.get::<Mesh3d>() else {
            commands
                .entity(entity)
                .insert((coverage, AuxDrawCounter::default()));
            continue;
        };
        for (render_entity, main_entity) in
            visible_meshes.entities_cpu_culling.iter().copied().chain(
                visible_meshes
                    .entities_gpu_culling
                    .iter()
                    .map(|(main, render)| (*render, *main)),
            )
        {
            coverage.visible += 1;
            let Some(instance) = material_instances.instances.get(&main_entity) else {
                coverage.pending += 1;
                coverage.diagnostic(AuxDiagnostic {
                    stage: "queue.material-instance".into(),
                    main_entity_bits: main_entity.id().to_bits(),
                    render_entity_bits: render_entity.to_bits(),
                    material_asset: None,
                    material_type: None,
                    mesh_asset: mesh_instances
                        .render_mesh_queue_data(main_entity)
                        .map(|mesh| format!("{:?}", mesh.mesh_asset_id())),
                    pipeline: None,
                    reason: "RenderMaterialInstances has no instance for this visible mesh".into(),
                });
                continue;
            };
            if registry.1.contains(&instance.asset_id.type_id()) {
                coverage.sky_visible += 1;
                coverage.visible -= 1;
                continue;
            }
            let mesh_instance = mesh_instances.render_mesh_queue_data(main_entity);
            let mesh_asset = mesh_instance.as_ref().map(|mesh| mesh.mesh_asset_id());
            let diagnostic = |stage: &str,
                              pipeline: Option<CachedRenderPipelineId>,
                              reason: String| {
                AuxDiagnostic {
                    stage: stage.into(),
                    main_entity_bits: main_entity.id().to_bits(),
                    render_entity_bits: render_entity.to_bits(),
                    material_asset: Some(format!("{:?}", instance.asset_id)),
                    material_type: Some(registry.0.get(&instance.asset_id.type_id()).map_or_else(
                        || {
                            if instance.asset_id.type_id() == TypeId::of::<StandardMaterial>() {
                                type_name::<StandardMaterial>().into()
                            } else {
                                format!("Unregistered {:?}", instance.asset_id.type_id())
                            }
                        },
                        |registration| registration.material_type.to_owned(),
                    )),
                    mesh_asset: mesh_asset.map(|asset| format!("{asset:?}")),
                    pipeline: pipeline.map(|id| format!("{id:?}")),
                    reason,
                }
            };
            let Some(registration) = registry.0.get(&instance.asset_id.type_id()) else {
                coverage.unsupported += 1;
                coverage.diagnostic(diagnostic(
                    "queue.material-type",
                    None,
                    "Visible material type has no explicit auxiliary contract".into(),
                ));
                continue;
            };
            coverage.registered += 1;
            let Some(mesh_instance) = mesh_instance else {
                coverage.pending += 1;
                coverage.diagnostic(diagnostic(
                    "queue.mesh-instance",
                    None,
                    "RenderMeshInstances queue data is not prepared".into(),
                ));
                continue;
            };
            let Some(material) = materials.get(instance.asset_id) else {
                coverage.pending += 1;
                coverage.diagnostic(diagnostic(
                    "queue.prepared-material",
                    None,
                    "PreparedMaterial is not available".into(),
                ));
                continue;
            };
            let Some(mesh) = meshes.get(mesh_instance.mesh_asset_id()) else {
                coverage.pending += 1;
                coverage.diagnostic(diagnostic(
                    "queue.render-mesh",
                    None,
                    "RenderMesh is not available".into(),
                ));
                continue;
            };
            let Some(slabs) = allocator.mesh_slabs(&mesh_instance.mesh_asset_id()) else {
                coverage.pending += 1;
                coverage.diagnostic(diagnostic(
                    "queue.mesh-allocator",
                    None,
                    "Actual mesh allocator slabs are not available".into(),
                ));
                continue;
            };
            if mesh.has_morph_targets()
                || mesh.layout.0.contains(Mesh::ATTRIBUTE_JOINT_INDEX)
                || mesh.layout.0.contains(Mesh::ATTRIBUTE_JOINT_WEIGHT)
            {
                coverage.unsupported += 1;
                coverage.diagnostic(diagnostic("queue.vertex-history", None, "Registered auxiliary material does not support skinned or morphed vertex history".into()));
                continue;
            }
            if material.properties.material_layout.is_none() {
                coverage.errors.push(format!(
                    "Prepared material {:?} has no public layout",
                    instance.asset_id
                ));
                coverage.diagnostic(diagnostic(
                    "queue.material-layout",
                    None,
                    "Prepared material has no public bind group layout".into(),
                ));
                continue;
            }
            // Retain the real view/mesh/material pipeline bits. Registered
            // source shaders are not skinned/morphed and do not use lightmaps.
            // Refuse those extensions until their temporal contracts exist.
            let role = effective_role(registration.role, material.properties.alpha_mode);
            let material_bits: MeshPipelineKey =
                material.properties.mesh_pipeline_key_bits.downcast();
            let mesh_key = *view_key
                | MeshPipelineKey::from_bits_retain(mesh.key_bits.bits())
                | material_bits
                | alpha_mode_pipeline_key(material.properties.alpha_mode, &Msaa::Off);
            if matches!(registration.contract, MaterialContract::StandardOpaque) {
                let standard_key = material
                    .properties
                    .material_key
                    .to_key::<StandardMaterialKey>();
                let allowed = StandardMaterialKey::CULL_FRONT
                    | StandardMaterialKey::CULL_BACK
                    | StandardMaterialKey::DEPTH_BIAS;
                let unsupported_bits = standard_key.bits() & !allowed.bits();
                let geometry_extensions = MeshPipelineKey::VISIBILITY_RANGE_DITHER
                    | MeshPipelineKey::DEFERRED_PREPASS
                    | MeshPipelineKey::LIGHTMAPPED;
                if material.properties.alpha_mode != AlphaMode::Opaque
                    || material.properties.reads_view_transmission_texture
                    || unsupported_bits != 0
                    || mesh_key.intersects(geometry_extensions)
                    || !mesh.layout.0.contains(Mesh::ATTRIBUTE_POSITION)
                {
                    coverage.unsupported += 1;
                    coverage.diagnostic(diagnostic("queue.standard-opaque-contract", None, format!(
                        "Rigid opaque contract refused: alpha={:?}, transmission={}, unsupported_material_bits={unsupported_bits:#x}, geometry_extension_bits={:#x}, positions={}",
                        material.properties.alpha_mode, material.properties.reads_view_transmission_texture,
                        (mesh_key & geometry_extensions).bits(), mesh.layout.0.contains(Mesh::ATTRIBUTE_POSITION),
                    )));
                    continue;
                }
            }
            let key = AuxKey {
                material: ErasedMaterialPipelineKey {
                    mesh_key: ErasedMeshPipelineKey::new(mesh_key),
                    material_key: material.properties.material_key.clone(),
                    type_id: instance.asset_id.type_id(),
                },
                vertex: registration.shaders.vertex.id(),
                fragment: registration.shaders.fragment.id(),
                role,
            };
            pipeline
                .properties
                .insert(key.clone(), material.properties.clone());
            pipeline
                .stages
                .insert(key.clone(), registration.shaders.clone());
            let pipeline_id = match specialized.specialize(&cache, &pipeline, key, &mesh.layout) {
                Ok(id) => id,
                Err(error) => {
                    coverage
                        .errors
                        .push(format!("Auxiliary specialization: {error}"));
                    coverage.diagnostic(diagnostic(
                        "queue.pipeline-specialization",
                        None,
                        error.to_string(),
                    ));
                    continue;
                }
            };
            if cache.get_render_pipeline(pipeline_id).is_none() {
                coverage.pending += 1;
                coverage.diagnostic(diagnostic(
                    "queue.pipeline-cache",
                    Some(pipeline_id),
                    format!("{:?}", cache.get_render_pipeline_state(pipeline_id)),
                ));
            }
            let model = get_mesh_instance_world_from_local(
                main_entity,
                mesh_instance.current_uniform_index,
                &mesh_instances,
                batched.as_deref(),
            );
            phase.add_transient(AuxItem {
                entity: (render_entity, main_entity),
                pipeline: pipeline_id,
                draw_function,
                role,
                center: model.transform_point3(mesh.aabb_center),
                depth_bias: material.properties.depth_bias,
                distance: 0.0,
                indexed: slabs.index_slab_id.is_some(),
                batch_range: 0..1,
                extra_index: PhaseItemExtraIndex::None,
                material_asset: instance.asset_id,
                material_type: registration.material_type,
                mesh_asset: mesh_instance.mesh_asset_id(),
            });
            coverage.queued += 1;
            match role {
                MaterialRole::Opaque => coverage.opaque += 1,
                MaterialRole::Water => coverage.water += 1,
                MaterialRole::Transparent => coverage.transparent += 1,
            }
        }
        commands
            .entity(entity)
            .insert((coverage, AuxDrawCounter::default()));
    }
    phases.retain(|view, _| live_views.contains(view));
}
