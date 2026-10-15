//! Private resident vertex projection through the existing shared compute queue.
use bevy::{
    asset::RenderAssetUsages,
    ecs::system::SystemState,
    prelude::*,
    render::{
        RenderApp,
        render_asset::RenderAssets,
        render_resource::{AsBindGroup, OwnedBindingResource, PipelineCache},
        renderer::{RenderDevice, RenderQueue},
        storage::{GpuShaderBuffer, ShaderBuffer},
    },
};
use forge_core::bytes_sha256;
use forge_engine::{
    source_graph::SourceGraphMaterial,
    source_particle_compute::{
        SourceParticleComputeQueue, SourceParticleComputeRequest, SourceParticleComputeStatus,
        SourceParticleProjection, register_source_particle_compute_program,
    },
};
use serde::Deserialize;
use std::{
    path::Path,
    time::{Duration, Instant},
};

const QUEUE_SLOT: u64 = 8;
fn default_state_bytes() -> usize {
    320
}
const VERTEX_BYTES: usize = 112;

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
pub(super) struct Spec {
    pub(super) shader: super::Pin,
    pub(super) bank: super::Pin,
    pub(super) entry_point: String,
    vertex_count: u32,
    #[serde(default = "default_state_bytes")]
    pub(super) state_prefix_bytes: usize,
}

pub(super) struct Projection {
    shader: Handle<Shader>,
    curve: Handle<Image>,
    input: Handle<ShaderBuffer>,
    output: Handle<ShaderBuffer>,
    bank: [u8; 1024],
    entry_point: String,
    vertices: u32,
    bytes: usize,
    state_bytes: usize,
    entity: Option<Entity>,
}

impl Projection {
    pub(super) fn attach_governed(
        &mut self,
        app: &mut App,
        owner: Entity,
        instance: forge_engine::source_particle_transport::InstantiatedSourceParticleOwner,
    ) -> Result<(), String> {
        if self.entity.is_some() || instance.buffers.len() != 5 || instance.shaders.len() != 2 {
            return Err("Governed projection already attached or resource layout differs".into());
        }
        self.shader = instance.shaders[1].clone();
        self.curve = instance.curve;
        self.input = instance.buffers[3].clone();
        self.output = instance.buffers[4].clone();
        app.world_mut().entity_mut(owner).insert(instance.owner);
        self.entity = Some(
            app.world_mut()
                .spawn((instance.projection, ChildOf(owner)))
                .id(),
        );
        Ok(())
    }
    pub(super) fn new(
        app: &mut App,
        root: &Path,
        spec: &Spec,
        curve: Handle<Image>,
        source_mesh: &[u8],
    ) -> Result<Self, String> {
        let count = usize::try_from(spec.vertex_count).map_err(|_| "Vertex count overflow")?;
        let bytes = count
            .checked_mul(VERTEX_BYTES)
            .and_then(|n| n.checked_add(spec.state_prefix_bytes))
            .ok_or("Vertex projection byte size overflow")?;
        if !matches!(spec.state_prefix_bytes, 160 | 320)
            || count == 0
            || count * 64 != source_mesh.len()
            || bytes > 64 * 1024 * 1024
        {
            return Err("Vertex projection cardinality differs from the source mesh".into());
        }
        let bank = super::read(root, &spec.bank)?
            .try_into()
            .map_err(|_| "Vertex projection bank ABI mismatch")?;
        let source =
            String::from_utf8(super::read(root, &spec.shader)?).map_err(|e| e.to_string())?;
        let shader = register_source_particle_compute_program(
            &mut app.world_mut().resource_mut::<Assets<Shader>>(),
            &source,
            spec.shader.sha256,
            &spec.entry_point,
        )?;
        let mut input_bytes = vec![0; spec.state_prefix_bytes];
        input_bytes.extend_from_slice(source_mesh);
        let input = app
            .world_mut()
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::new(
                &input_bytes,
                RenderAssetUsages::default(),
            ));
        let output = app
            .world_mut()
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::new(
                &vec![0; bytes],
                RenderAssetUsages::default(),
            ));
        Ok(Self {
            shader,
            curve,
            input,
            output,
            bank,
            entry_point: spec.entry_point.clone(),
            vertices: spec.vertex_count,
            bytes,
            state_bytes: spec.state_prefix_bytes,
            entity: None,
        })
    }

    pub(super) fn install(
        &mut self,
        app: &mut App,
        owner: Entity,
        source: Handle<ShaderBuffer>,
        producer: u64,
        slot: u64,
        material: Handle<SourceGraphMaterial>,
    ) -> Result<(), String> {
        if self.entity.is_some() {
            return Err("Vertex projection already attached".into());
        }
        let request = SourceParticleComputeRequest::new(
            self.shader.clone(),
            self.entry_point.clone(),
            self.curve.clone(),
            self.bank,
            self.input.clone(),
            self.output.clone(),
            self.vertices.max(20).div_ceil(64),
        )
        .map_err(str::to_owned)?
        .copy_input_prefix_from(source, self.state_bytes as u64)
        .map_err(str::to_owned)?
        .after_slot(producer)
        .map_err(str::to_owned)?;
        let projection = SourceParticleProjection::new(owner, slot, request)
            .map_err(str::to_owned)?
            .with_material(material);
        self.entity = Some(app.world_mut().spawn((projection, ChildOf(owner))).id());
        Ok(())
    }

    pub(super) fn update(
        &mut self,
        app: &mut App,
        input: &Handle<ShaderBuffer>,
        expected_state: &[u8],
        stamp: u16,
        directory: &Path,
    ) -> Result<Handle<ShaderBuffer>, String> {
        if self.entity.is_none() {
            return self.update_legacy(app, input, expected_state, stamp, directory);
        }
        let entity = self
            .entity
            .ok_or("Continuous vertex projection not installed")?;
        let projection = app
            .world()
            .get::<SourceParticleProjection>(entity)
            .ok_or("Continuous vertex projection removed")?;
        if let Some(error) = projection.error() {
            return Err(error.to_owned());
        }
        let deadline = Instant::now() + Duration::from_secs(90);
        let (entity, slot) = super::receive_buffer(app, self.output.clone());
        let data = loop {
            app.update();
            if let Some(data) = slot.lock().map_err(|_| "Projection mutex poisoned")?.take()
                && data.len() == self.bytes
                && super::u32_at(&data, 56) == u32::from(stamp)
            {
                break data;
            }
            if Instant::now() > deadline {
                return Err("Resident vertex projection readback timeout".into());
            }
            std::thread::sleep(Duration::from_millis(2));
        };
        app.world_mut()
            .entity_mut(entity)
            .remove::<bevy::render::gpu_readback::Readback>();
        if data[..self.state_bytes] != *expected_state {
            return Err("Vertex projection changed the resident source state".into());
        }
        std::fs::write(
            directory.join(format!("step-{stamp}.projected-vertices.bin")),
            &data,
        )
        .map_err(|e| e.to_string())?;
        Ok(self.output.clone())
    }

    pub(super) fn encoded_frames(&self, app: &App) -> Result<u64, String> {
        let Some(entity) = self.entity else {
            return Ok(0);
        };
        let projection = app
            .world()
            .get::<SourceParticleProjection>(entity)
            .ok_or("Continuous projection removed")?;
        if let Some(error) = projection.error() {
            return Err(error.to_owned());
        }
        Ok(projection.encoded_frames())
    }

    fn update_legacy(
        &mut self,
        app: &mut App,
        input: &Handle<ShaderBuffer>,
        expected_state: &[u8],
        stamp: u16,
        directory: &Path,
    ) -> Result<Handle<ShaderBuffer>, String> {
        self.copy_state_input_legacy(app, input)?;
        let request = SourceParticleComputeRequest::new(
            self.shader.clone(),
            self.entry_point.clone(),
            self.curve.clone(),
            self.bank,
            self.input.clone(),
            self.output.clone(),
            self.vertices.max(20).div_ceil(64),
        )
        .map_err(str::to_owned)?;
        app.world_mut()
            .resource_mut::<SourceParticleComputeQueue>()
            .insert(QUEUE_SLOT, request.clone());
        let deadline = Instant::now() + Duration::from_secs(90);
        loop {
            app.update();
            match request.status().map_err(str::to_owned)? {
                SourceParticleComputeStatus::Encoded => break,
                SourceParticleComputeStatus::Failed(error) => return Err(error),
                _ => {}
            }
            if Instant::now() > deadline {
                return Err("Resident vertex projection encoding timeout".into());
            }
            std::thread::sleep(Duration::from_millis(2));
        }
        let (entity, slot) = super::receive_buffer(app, self.output.clone());
        let data = loop {
            app.update();
            if let Some(data) = slot.lock().map_err(|_| "Projection mutex poisoned")?.take()
                && data.len() == self.bytes
                && super::u32_at(&data, 56) == u32::from(stamp)
            {
                break data;
            }
            if Instant::now() > deadline {
                return Err("Resident vertex projection readback timeout".into());
            }
            std::thread::sleep(Duration::from_millis(2));
        };
        app.world_mut()
            .entity_mut(entity)
            .remove::<bevy::render::gpu_readback::Readback>();
        if data[..self.state_bytes] != *expected_state {
            return Err("Vertex projection changed the resident source state".into());
        }
        std::fs::write(
            directory.join(format!("step-{stamp}.projected-vertices.bin")),
            &data,
        )
        .map_err(|e| e.to_string())?;
        app.world_mut()
            .resource_mut::<SourceParticleComputeQueue>()
            .remove(QUEUE_SLOT);
        Ok(self.output.clone())
    }

    fn copy_state_input_legacy(
        &self,
        app: &mut App,
        source: &Handle<ShaderBuffer>,
    ) -> Result<(), String> {
        let deadline = Instant::now() + Duration::from_secs(90);
        loop {
            app.update();
            let render = app
                .get_sub_app(RenderApp)
                .ok_or("Projection renderer absent")?
                .world();
            let buffers = render.resource::<RenderAssets<GpuShaderBuffer>>();
            if let (Some(source), Some(input)) = (buffers.get(source), buffers.get(&self.input)) {
                let mut encoder = render
                    .resource::<RenderDevice>()
                    .create_command_encoder(&default());
                encoder.copy_buffer_to_buffer(
                    &source.buffer,
                    0,
                    &input.buffer,
                    0,
                    self.state_bytes as u64,
                );
                render.resource::<RenderQueue>().submit([encoder.finish()]);
                return Ok(());
            }
            if Instant::now() > deadline {
                return Err("Resident vertex input resources timeout".into());
            }
            std::thread::sleep(Duration::from_millis(2));
        }
    }

    pub(super) fn verify_material_binding(
        &self,
        app: &mut App,
        material: &Handle<SourceGraphMaterial>,
        stamp: u16,
    ) -> Result<serde_json::Value, String> {
        let material = app
            .world()
            .resource::<Assets<SourceGraphMaterial>>()
            .get(material)
            .ok_or("Projection material absent")?
            .clone();
        let render = app
            .get_sub_app_mut(RenderApp)
            .ok_or("Projection renderer absent")?
            .world_mut();
        let device = render.resource::<RenderDevice>().clone();
        let mut state = SystemState::<<SourceGraphMaterial as AsBindGroup>::Param>::new(render);
        let descriptor = SourceGraphMaterial::bind_group_layout_descriptor(&device);
        let prepared = render
            .resource_scope(|world, cache: Mut<PipelineCache>| {
                let mut parameters = state.get_mut(world).expect("Material parameters required");
                material.unprepared_bind_group(
                    &cache.get_bind_group_layout(&descriptor),
                    &device,
                    &mut parameters,
                    true,
                )
            })
            .map_err(|e| format!("Projection material resources absent: {e:?}"))?;
        let gpu = render
            .resource::<RenderAssets<GpuShaderBuffer>>()
            .get(&self.output)
            .ok_or("Resident vertex output absent on GPU")?;
        let binding = prepared.bindings.iter().find(|(slot, _)| *slot == 130);
        if !matches!(binding, Some((_, OwnedBindingResource::Buffer(buffer))) if buffer.id() == gpu.buffer.id())
        {
            return Err("Actual material does not bind the resident vertex projection".into());
        }
        Ok(serde_json::json!({
            "step": stamp,
            "vertices": self.vertices,
            "bytes": self.bytes,
            "material_binding": 130,
            "actual_GPU_compute_output_and_material_buffer_identity": true,
            "resident_source_state_echo_bytes": self.state_bytes,
            "resident_source_state_GPU_copy_bytes": self.state_bytes,
            "original_mesh_uploaded_once_as_storage": true,
            "bank_sha256": bytes_sha256(&self.bank).to_string(),
        }))
    }
}
