//! Governed particle resources to transient shared-renderer components.
//! Preparation is filesystem independent and precedes host asset mutation.
use crate::source_graph::SourceGraphMaterial;
use crate::source_particle_compute::{
    SourceParticleComputeRequest, SourceParticleComputeSequence, SourceParticleEmissionCommand,
    SourceParticleEmitterSequence, SourceParticleProjection, SourceParticleSceneOwner,
    register_source_particle_compute_program,
};
use bevy::{
    asset::RenderAssetUsages,
    image::{ImageSampler, ImageSamplerDescriptor},
    prelude::*,
    render::{
        render_resource::{Extent3d, TextureDimension, TextureFormat},
        storage::ShaderBuffer,
    },
};
use forge_assets::source_particle_transport::{SourceParticleBlob, SourceParticleOwnerTransport};
use forge_core::bytes_sha256;
use std::collections::BTreeMap;

pub struct PreparedSourceParticleOwner {
    declaration: SourceParticleOwnerTransport,
    compute: Shader,
    projection: Shader,
    curve: Image,
    bank: [u8; 1024],
    projection_bank: [u8; 1024],
    initial: Vec<u8>,
    native_vertices: Vec<u8>,
}

/// The host attaches these components to its admitted node and owns the assets.
/// No automatic clock, implicit Play, geometry or camera frame is introduced.
pub struct InstantiatedSourceParticleOwner {
    pub owner: SourceParticleSceneOwner,
    pub projection: SourceParticleProjection,
    pub buffers: Vec<Handle<ShaderBuffer>>,
    pub shaders: Vec<Handle<Shader>>,
    pub curve: Handle<Image>,
    pub source_component_guid: String,
    pub source_component_file_id: String,
}

impl PreparedSourceParticleOwner {
    pub fn prepare(
        declaration: &SourceParticleOwnerTransport,
        resources: &BTreeMap<String, Vec<u8>>,
    ) -> Result<Self, String> {
        declaration.validate()?;
        let read = |blob: &SourceParticleBlob| -> Result<Vec<u8>, String> {
            let bytes = resources
                .get(&blob.path)
                .ok_or("Source particle resource absent")?;
            if bytes.len() != blob.byte_length || bytes_sha256(bytes) != blob.sha256 {
                return Err("Source particle resource changed after transport admission".into());
            }
            Ok(bytes.clone())
        };
        // Reuse standalone Naga admission in a private staging collection.
        let mut staged = Assets::<Shader>::default();
        let compute_bytes = read(&declaration.compute.source)?;
        let projection_bytes = read(&declaration.projection.program.source)?;
        let compute_handle = register_source_particle_compute_program(
            &mut staged,
            std::str::from_utf8(&compute_bytes).map_err(|e| e.to_string())?,
            declaration.compute.source.sha256,
            &declaration.compute.entry_point,
        )?;
        let projection_handle = register_source_particle_compute_program(
            &mut staged,
            std::str::from_utf8(&projection_bytes).map_err(|e| e.to_string())?,
            declaration.projection.program.source.sha256,
            &declaration.projection.program.entry_point,
        )?;
        let compute = staged
            .remove(compute_handle.id())
            .ok_or("Staged compute shader absent")?;
        let projection = staged
            .remove(projection_handle.id())
            .ok_or("Staged projection shader absent")?;
        let mut curve = Image::new(
            Extent3d {
                width: declaration.curve_width,
                height: declaration.curve_height,
                depth_or_array_layers: 1,
            },
            TextureDimension::D2,
            read(&declaration.curve_atlas)?,
            TextureFormat::Rgba16Float,
            RenderAssetUsages::default(),
        );
        curve.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor::linear());
        Ok(Self {
            declaration: declaration.clone(),
            compute,
            projection,
            curve,
            bank: read(&declaration.curve_bank)?
                .try_into()
                .map_err(|_| "Curve bank ABI differs")?,
            projection_bank: read(&declaration.projection.bank)?
                .try_into()
                .map_err(|_| "Projection bank ABI differs")?,
            initial: read(&declaration.initial_state)?,
            native_vertices: read(&declaration.projection.native_vertices)?,
        })
    }

    #[allow(
        clippy::too_many_arguments,
        reason = "Explicit host ownership and separate asset collections"
    )]
    pub fn instantiate(
        self,
        shaders: &mut Assets<Shader>,
        images: &mut Assets<Image>,
        buffers: &mut Assets<ShaderBuffer>,
        owner_entity: Entity,
        material: Handle<SourceGraphMaterial>,
        producer_slot: u64,
        projection_slot: u64,
    ) -> Result<InstantiatedSourceParticleOwner, String> {
        if producer_slot == projection_slot {
            return Err("Source particle producer/projection slots overlap".into());
        }
        let compute = shaders.add(self.compute);
        let project = shaders.add(self.projection);
        let curve = images.add(self.curve);
        let states = std::array::from_fn(|_| {
            buffers.add(ShaderBuffer::new(
                &self.initial,
                RenderAssetUsages::default(),
            ))
        });
        let published = buffers.add(ShaderBuffer::new(
            &self.initial,
            RenderAssetUsages::default(),
        ));
        let sequence = SourceParticleComputeSequence::new(
            producer_slot,
            compute.clone(),
            self.declaration.compute.entry_point.clone(),
            curve.clone(),
            states.clone(),
            self.declaration.workgroups,
        )
        .map_err(str::to_owned)?
        .with_publication_buffer(published.clone(), self.initial.len() as u64)
        .map_err(str::to_owned)?;
        let controller = SourceParticleEmitterSequence::new(sequence, self.declaration.spawner)
            .map_err(str::to_owned)?;
        let declaration = self.declaration.clone();
        let bank = self.bank;
        let owner = SourceParticleSceneOwner::new(controller, move |command, stamp| {
            let (delta, mode, count) = match command {
                SourceParticleEmissionCommand::Reset => (0.0, declaration.control.reset_mode, 0.0),
                SourceParticleEmissionCommand::Allocate { count } => {
                    (0.0, declaration.control.allocate_mode, count)
                }
                SourceParticleEmissionCommand::Update { delta_seconds } => {
                    (delta_seconds, declaration.control.update_mode, 0.0)
                }
            };
            declaration
                .control_bank(&bank, delta, mode, stamp, count)?
                .try_into()
                .map_err(|_| "Source control bank ABI differs".into())
        });
        let mut input_bytes = vec![0; self.initial.len()];
        input_bytes.extend_from_slice(&self.native_vertices);
        let input = buffers.add(ShaderBuffer::new(
            &input_bytes,
            RenderAssetUsages::default(),
        ));
        let size = self.initial.len()
            + self.declaration.projection.vertex_count
                * self.declaration.projection.output_stride_bytes;
        let output = buffers.add(ShaderBuffer::new(
            &vec![0; size],
            RenderAssetUsages::default(),
        ));
        let vertices = u32::try_from(self.declaration.projection.vertex_count)
            .map_err(|_| "Projection vertex count exceeds GPU ABI")?;
        let prefix = u32::try_from(self.initial.len() / 16)
            .map_err(|_| "Projection prefix exceeds GPU ABI")?;
        let request = SourceParticleComputeRequest::new(
            project.clone(),
            self.declaration.projection.program.entry_point.clone(),
            curve.clone(),
            self.projection_bank,
            input.clone(),
            output.clone(),
            vertices.max(prefix).div_ceil(64),
        )
        .map_err(str::to_owned)?
        .copy_input_prefix_from(published.clone(), self.initial.len() as u64)
        .map_err(str::to_owned)?
        .after_slot(producer_slot)
        .map_err(str::to_owned)?;
        let mut projection = SourceParticleProjection::new(owner_entity, projection_slot, request)
            .map_err(str::to_owned)?
            .with_material(material);
        if let Some(layout) = self.declaration.projection.frame_layout {
            projection = projection.with_frame_layout(layout)?;
        }
        Ok(InstantiatedSourceParticleOwner {
            owner,
            projection,
            buffers: vec![
                states[0].clone(),
                states[1].clone(),
                published,
                input,
                output,
            ],
            shaders: vec![compute, project],
            curve,
            source_component_guid: self.declaration.source_component_guid,
            source_component_file_id: self.declaration.source_component_file_id,
        })
    }
}

#[cfg(test)]
pub(crate) mod tests {
    use super::*;
    pub(crate) fn fixture() -> (SourceParticleOwnerTransport, BTreeMap<String, Vec<u8>>) {
        let mut resources = BTreeMap::new();
        let mut blob = |name: &str, data: Vec<u8>| {
            let value = serde_json::json!({"path":name,"sha256":bytes_sha256(&data),"byte_length":data.len()});
            resources.insert(name.into(), data);
            value
        };
        let program = blob(
            "compute.wgsl",
            b"@compute @workgroup_size(64) fn advance() {}".to_vec(),
        );
        let bank = blob("bank.bin", vec![0; 1024]);
        let initial = blob("state.bin", vec![0; 16]);
        let atlas = blob("atlas.bin", vec![0; 8]);
        let vertices = blob("vertices.bin", vec![0; 64]);
        let declaration=serde_json::from_value(serde_json::json!({
            "source_component_guid":"a".repeat(32),"source_component_file_id":"11400000",
            "source_node":0,"source_primitive":0,"source_material":0,
            "compute":{"source":program.clone(),"entry_point":"advance"},
            "curve_atlas":atlas,"curve_width":1,"curve_height":1,"curve_bank":bank.clone(),
            "initial_state":initial,"state_prefix_bytes":16,"workgroups":1,
            "control":{"delta_offset":1008,"mode_offset":1012,"stamp_offset":1016,"count_offset":1020,
                "reset_mode":3.0,"allocate_mode":2.0,"update_mode":1.0},
            "spawner":{"loop_duration_seconds":3.0,"loop_count":1,"burst_delay_seconds":0.0,
                "burst_count":1.0,"burst_active":true,"initial_play":true,"prewarm_steps":0},
            "projection":{"program":{"source":program,"entry_point":"advance"},"bank":bank,
                "native_vertices":vertices,"vertex_count":1,"input_stride_bytes":64,"output_stride_bytes":112}
        })).unwrap();
        (declaration, resources)
    }
    #[test]
    fn admitted_resources_create_owned_components_and_refusals_leave_assets_unchanged() {
        let (declaration, mut resources) = fixture();
        let mut shaders = Assets::<Shader>::default();
        let mut images = Assets::<Image>::default();
        let mut buffers = Assets::<ShaderBuffer>::default();
        let prepared = PreparedSourceParticleOwner::prepare(&declaration, &resources).unwrap();
        assert!(
            prepared
                .instantiate(
                    &mut shaders,
                    &mut images,
                    &mut buffers,
                    Entity::PLACEHOLDER,
                    Handle::default(),
                    7,
                    7
                )
                .is_err()
        );
        assert_eq!((shaders.len(), images.len(), buffers.len()), (0, 0, 0));
        let prepared = PreparedSourceParticleOwner::prepare(&declaration, &resources).unwrap();
        let instance = prepared
            .instantiate(
                &mut shaders,
                &mut images,
                &mut buffers,
                Entity::PLACEHOLDER,
                Handle::default(),
                7,
                1,
            )
            .unwrap();
        assert_eq!((shaders.len(), images.len(), buffers.len()), (2, 1, 5));
        assert_eq!(instance.owner.slot(), 7);
        assert_eq!(instance.owner.queued_frames(), 0);
        assert_eq!(
            instance.source_component_guid,
            declaration.source_component_guid
        );
        assert_eq!(instance.projection.output_buffer(), instance.buffers[4]);
        resources.get_mut("state.bin").unwrap()[0] ^= 1;
        assert!(PreparedSourceParticleOwner::prepare(&declaration, &resources).is_err());
        assert_eq!((shaders.len(), images.len(), buffers.len()), (2, 1, 5));
    }
}
