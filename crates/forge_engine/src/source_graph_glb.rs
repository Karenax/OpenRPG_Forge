//! Integrity-checked GLB source-program assembly. This does not install images,
//! invent normal packing, route disabled passes, or qualify a rendered mesh.
use crate::source_graph::{
    SourceGraphForwardState, SourceGraphMaterial, SourceGraphMaterialProgram,
    SourceGraphShaderArtifact, SourceGraphShaderRegistry, SourceGraphUniforms,
    register_source_graph_material,
};
use bevy::{
    asset::RenderAssetUsages,
    mesh::{Indices, PrimitiveTopology},
    prelude::{Assets, Handle, Image, Mesh, Shader, Vec4},
};
use forge_assets::source_graph_bundle::{
    SourceGraphMaterialContract, SourceGraphNormalDecodeMode, SourceGraphTexture,
};

#[path = "source_graph_glb/captured_texture.rs"]
mod captured_texture;
use crate::source_graph::SourceGraphImportedTexture;
use bevy::render::renderer::RenderDevice;

pub struct SourceGraphGlbMesh {
    pub mesh: Mesh,
    pub material_index: usize,
}

/// Retain the declared static primitive and dedicated original shader inputs.
/// Scene transforms, materials, importer parity, and render admission are the
/// host's separate responsibilities. No source graph is replaced with PBR.
pub fn source_graph_mesh_from_glb(
    glb: &forge_assets::source_graph_glb::SourceGraphGlb<'_>,
    mesh_index: usize,
    primitive_index: usize,
) -> Result<SourceGraphGlbMesh, String> {
    let geometry = forge_assets::source_graph_attributes::read_source_graph_primitive_geometry(
        glb,
        mesh_index,
        primitive_index,
    )?;
    let material_index = geometry.source.material_index;
    let contract = &glb
        .materials
        .iter()
        .find(|(index, _)| *index == material_index)
        .ok_or("source material missing")?
        .1;
    if contract.requires_original_vertex_color && geometry.source.original_colors.is_none() {
        return Err("original vertex color required by source program".into());
    }
    let mut mesh = Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::MAIN_WORLD | RenderAssetUsages::RENDER_WORLD,
    );
    mesh.insert_attribute(Mesh::ATTRIBUTE_POSITION, geometry.positions);
    mesh.insert_attribute(Mesh::ATTRIBUTE_NORMAL, geometry.normals);
    if let Some(tangents) = geometry.tangents {
        mesh.insert_attribute(Mesh::ATTRIBUTE_TANGENT, tangents);
    }
    mesh.insert_attribute(Mesh::ATTRIBUTE_UV_0, geometry.uv0);
    for (attribute, values) in crate::source_graph::SOURCE_GRAPH_STANDARD_ADDITIONAL_UVS
        .into_iter()
        .zip(geometry.additional_uvs)
    {
        if let Some(values) = values {
            mesh.insert_attribute(attribute, values);
        }
    }
    mesh.insert_indices(Indices::U32(geometry.indices));
    mesh.insert_attribute(
        crate::source_graph::SOURCE_GRAPH_NORMAL,
        geometry.source.original_normals,
    );
    if let Some(tangents) = geometry.source.original_tangents {
        mesh.insert_attribute(crate::source_graph::SOURCE_GRAPH_TANGENT, tangents);
    }
    mesh.insert_attribute(
        crate::source_graph::SOURCE_GRAPH_UV0,
        geometry.source.original_uv0,
    );
    if let Some(colors) = geometry.source.original_colors {
        mesh.insert_attribute(crate::source_graph::SOURCE_GRAPH_COLOR, colors);
    }
    for (attribute, values) in crate::source_graph::SOURCE_GRAPH_ORIGINAL_ADDITIONAL_UVS
        .into_iter()
        .zip(geometry.source.original_additional_uvs)
    {
        if let Some(values) = values {
            mesh.insert_attribute(attribute, values);
        }
    }
    Ok(SourceGraphGlbMesh {
        mesh,
        material_index,
    })
}

pub struct SourceGraphGlbProgram {
    pub program: SourceGraphMaterialProgram,
    /// Retained for the host's separately qualified phase/pass routing.
    pub original_render_queue: i32,
    pub original_disabled_passes: Vec<String>,
    /// Exact serialized importer settings, including unresolved -1 defaults.
    pub original_texture_imports: Vec<SourceGraphTexture>,
    normal_decode_modes: [Option<SourceGraphNormalDecodeMode>; 8],
    captured_images: Option<Vec<SourceGraphImportedTexture>>,
    scalar_parameters:
        Option<forge_assets::source_material_parameters::SourceMaterialScalarParameters>,
}

impl SourceGraphGlbProgram {
    /// Apply an explicit scene policy before any image allocation. Each call
    /// starts from retained importer settings, never from an earlier override.
    /// Images are owned by this program; the shader registry caches no images.
    #[expect(
        clippy::cast_possible_truncation,
        reason = "Sampler bias is projected to the Float32 ABI; sampler admission rejects nonfinite and unsupported results"
    )]
    pub fn with_scene_sampling(
        mut self,
        scene: crate::source_graph::SourceGraphSceneSampling,
        adapter: Option<&bevy::render::renderer::RenderAdapter>,
    ) -> Result<Self, String> {
        use crate::source_graph::{
            SourceGraphSamplerSettings, SourceGraphSceneSampling,
            source_graph_image_sampler_in_scene,
        };
        let supported = if scene == SourceGraphSceneSampling::UnityForceEnableD3d {
            adapter
                .ok_or("Actual render adapter required for forced source sampling")?
                .get_downlevel_capabilities()
                .flags
                .contains(wgpu::DownlevelFlags::ANISOTROPIC_FILTERING)
        } else {
            false
        };
        let Some(images) = &mut self.captured_images else {
            if scene != SourceGraphSceneSampling::Authored {
                return Err("Forced source sampling requires captured storage".into());
            }
            return Ok(self);
        };
        if images.len() != self.original_texture_imports.len() {
            return Err("Scene sampling image/importer binding count differs".into());
        }
        let samplers = self
            .original_texture_imports
            .iter()
            .map(|texture| {
                let storage = texture
                    .imported_storage
                    .as_ref()
                    .ok_or("Source sampling storage absent")?;
                let s = &storage.sampler;
                source_graph_image_sampler_in_scene(
                    SourceGraphSamplerSettings {
                        filter_mode: s.filter_mode,
                        wrap_u: s.wrap[0],
                        wrap_v: s.wrap[1],
                        wrap_w: s.wrap[2],
                        anisotropy: u16::from(s.anisotropy),
                        mip_bias: s.mip_bias as f32,
                        source_mipmaps_enabled: s.source_mipmaps_enabled,
                    },
                    storage.width,
                    storage.height,
                    u32::try_from(storage.mips.len())
                        .map_err(|_| "Source mip count exceeds GPU limits")?,
                    scene,
                    supported,
                )
                .map_err(str::to_owned)
            })
            .collect::<Result<Vec<_>, String>>()?;
        for (image, sampler) in images.iter_mut().zip(samplers) {
            image.set_scene_sampler(sampler);
        }
        Ok(self)
    }
    #[must_use]
    pub fn requires_captured_images(&self) -> bool {
        self.captured_images.is_some()
    }

    /// Register precisely the images retained from checked embedded storage.
    /// Device support is checked before mutation. Rendering/source parity and
    /// original camera, time and disabled pass routing remain host responsibilities.
    pub fn register_captured_images(
        mut self,
        shaders: &mut Assets<Shader>,
        registry: &mut SourceGraphShaderRegistry,
        image_assets: &mut Assets<Image>,
        render_device: &RenderDevice,
        mut source: SourceGraphUniforms,
    ) -> Result<SourceGraphMaterial, &'static str> {
        let prepared = self
            .captured_images
            .take()
            .ok_or("GLB has no captured image batch")?;
        if prepared.len() != self.program.required_texture_count {
            return Err("Captured image batch binding count differs");
        }
        if prepared
            .iter()
            .any(|image| image.requires_texture_compression_bc)
            && !render_device
                .features()
                .contains(wgpu::Features::TEXTURE_COMPRESSION_BC)
        {
            return Err("Actual renderer device did not enable TEXTURE_COMPRESSION_BC");
        }
        source.normal_decode_modes = normal_decode_uniforms(self.normal_decode_modes);
        let scalar_parameters = self.scalar_parameters.take();
        let handles: Vec<_> = prepared
            .into_iter()
            .map(|image| image_assets.add(image.into_unqualified_image()))
            .collect();
        let created: Vec<_> = handles.iter().map(Handle::id).collect();
        let result =
            register_source_graph_material(shaders, registry, self.program, source, handles);
        if result.is_err() {
            for id in created {
                image_assets.remove(id);
            }
        }
        result.map(|mut material| {
            if material.extension.program_key.requires_deferred_srgb {
                material
                    .extension
                    .install_deferred_srgb_lookup(image_assets);
            }
            apply_scalar_parameters(material, scalar_parameters.as_ref())
        })
    }

    /// Register this integrity-checked GLB program with its declared normal ABI.
    /// Images, camera mappings and original rendering still need host qualification.
    pub fn register(
        self,
        shaders: &mut Assets<Shader>,
        registry: &mut SourceGraphShaderRegistry,
        mut source: SourceGraphUniforms,
        images: Vec<Handle<Image>>,
    ) -> Result<SourceGraphMaterial, &'static str> {
        if self.captured_images.is_some() {
            return Err(
                "Captured storage requires register_captured_images; external PNG handles are forbidden",
            );
        }
        source.normal_decode_modes = normal_decode_uniforms(self.normal_decode_modes);
        register_source_graph_material(shaders, registry, self.program, source, images)
            .map(|material| apply_scalar_parameters(material, self.scalar_parameters.as_ref()))
    }
}

fn apply_scalar_parameters(
    mut material: SourceGraphMaterial,
    parameters: Option<&forge_assets::source_material_parameters::SourceMaterialScalarParameters>,
) -> SourceGraphMaterial {
    if let Some(parameters) = parameters {
        material.extension.set_scalar_parameters(parameters);
    }
    material
}

#[cfg(test)]
mod scalar_transport_tests {
    use super::*;
    use bevy::prelude::{Mat4, Shader};
    use bevy::render::render_resource::encase::UniformBuffer;
    use forge_core::bytes_sha256;

    #[test]
    fn source_graph_glb_scalar_packet_reaches_registered_material_uniform_120() {
        let stage=b"@group(#{MATERIAL_BIND_GROUP}) @binding(120) var<uniform> values:vec4<f32>;\n@vertex fn vertex()->@builtin(position) vec4<f32>{return vec4<f32>(0.0);}\n@fragment fn fragment()->@location(0) vec4<f32>{return values;}";
        let hash = bytes_sha256(stage);
        let mut packet = [0u8; 256];
        packet[..4].copy_from_slice(&1f32.to_le_bytes());
        packet[4..8].copy_from_slice(&1f32.to_le_bytes());
        let contract:SourceGraphMaterialContract=serde_json::from_value(serde_json::json!({
            "schema_version":5,"source_program_sha256":hash,"source_material_sha256":hash,
            "forward":{"buffer_view":0,"sha256":hash},"prepass":{"buffer_view":0,"sha256":hash},"libraries":[],"textures":[],
            "requires_original_vertex_color":false,"normal_decode_modes":[null,null,null,null,null,null,null,null],
            "scalar_parameters":{"names":["measured_mask_r","measured_mask_g"],"values":{"buffer_view":1,"sha256":bytes_sha256(&packet)}},
            "state":{"color_write_mask":15,"color_blend":[1,0],"alpha_blend":[1,0],"forward_cull":0,"prepass_cull":0,
                "depth_compare":4,"write_depth":true,"transparent":false,"alpha_test":false,"alpha_to_coverage":false,"render_queue":2000,"disabled_passes":[]}
        })).unwrap();
        let loaded = source_graph_program_from_glb(&contract, &[stage, &packet]).unwrap();
        let source = SourceGraphUniforms {
            source_object_from_target_object: Mat4::IDENTITY,
            target_object_from_source_object: Mat4::IDENTITY,
            source_world_from_target_world: Mat4::IDENTITY,
            source_absolute_world_from_target_world: Mat4::IDENTITY,
            source_time_parameters: Vec4::ZERO,
            source_previous_time_parameters: Vec4::ZERO,
            source_scale_mip_min: Vec4::ONE,
            normal_decode_modes: [Vec4::ZERO; 2],
        };
        let mut shaders = Assets::<Shader>::default();
        let mut registry = SourceGraphShaderRegistry::default();
        let material = register_source_graph_material(
            &mut shaders,
            &mut registry,
            loaded.program,
            source,
            vec![],
        )
        .unwrap();
        let material = apply_scalar_parameters(material, loaded.scalar_parameters.as_ref());
        let mut encoded = UniformBuffer::new(Vec::<u8>::new());
        encoded.write(&material.extension.scalars).unwrap();
        assert_eq!(encoded.into_inner(), packet);
        assert_eq!(
            material.extension.scalars.lanes[0],
            Vec4::new(1., 1., 0., 0.)
        );
        assert_eq!(shaders.len(), 1);
    }
}

fn normal_decode_uniforms(modes: [Option<SourceGraphNormalDecodeMode>; 8]) -> [Vec4; 2] {
    let mut uniforms = [Vec4::ZERO; 2];
    for (slot, mode) in modes.into_iter().enumerate() {
        if let Some(mode) = mode {
            uniforms[slot / 4][slot % 4] = mode.shader_value();
        }
    }
    // Unspecified slots are unused: integrity admission rejects shader reads of None.
    uniforms
}

/// Build the actual native program from verified embedded WGSL. No shader or
/// image asset is registered here; any failure leaves native assets untouched.
pub fn source_graph_program_from_glb(
    contract: &SourceGraphMaterialContract,
    views: &[&[u8]],
) -> Result<SourceGraphGlbProgram, String> {
    contract
        .validate_integrity(views)
        .map_err(|e| format!("{e:?}"))?;
    let mut shaders = Vec::with_capacity(2 + contract.libraries.len());
    for blob in [&contract.forward, &contract.prepass]
        .into_iter()
        .chain(contract.libraries.iter())
    {
        let bytes = views
            .get(blob.buffer_view)
            .ok_or("missing shader bufferView")?;
        shaders.push(SourceGraphShaderArtifact {
            source: std::str::from_utf8(bytes)
                .map_err(|e| e.to_string())?
                .to_owned(),
            expected_sha256: blob.sha256,
        });
    }
    let state = &contract.state;
    let forward_state = SourceGraphForwardState {
        color_src: state.color_blend[0],
        color_dst: state.color_blend[1],
        alpha_src: state.alpha_blend[0],
        alpha_dst: state.alpha_blend[1],
        cull: state.forward_cull,
        compare: state.depth_compare,
        write_depth: state.write_depth,
    };
    forward_state.validate().map_err(str::to_owned)?;
    let captured_images = if matches!(contract.schema_version, 3..=6) {
        Some(
            contract
                .textures
                .iter()
                .map(|texture| {
                    captured_texture::prepare_captured_source_graph_texture(texture, views)
                })
                .collect::<Result<Vec<_>, String>>()?,
        )
    } else {
        None
    };
    Ok(SourceGraphGlbProgram {
        program: SourceGraphMaterialProgram {
            // Legacy transport explicitly retained the old ALL-writes ABI.
            // Schema 4 requires a measured/translated mask during validation.
            color_write_mask: state.color_write_mask.unwrap_or(15),
            prepass_cull: state.prepass_cull,
            shaders,
            forward_index: 0,
            prepass_index: 1,
            required_texture_count: contract.textures.len(),
            forward_state,
            original_transparent: state.transparent,
            original_alpha_test: state.alpha_test,
            original_alpha_to_mask: state.alpha_to_coverage,
            requires_original_vertex_color: contract.requires_original_vertex_color,
        },
        original_render_queue: state.render_queue,
        original_disabled_passes: state.disabled_passes.clone(),
        original_texture_imports: contract.textures.clone(),
        normal_decode_modes: contract.normal_decode_modes,
        captured_images,
        scalar_parameters: contract
            .read_scalar_parameters(views)
            .map_err(|e| format!("{e:?}"))?,
    })
}

#[cfg(test)]
mod normal_binding_tests {
    use super::*;

    #[test]
    fn glb_registration_uses_declared_modes_instead_of_callers_numeric_defaults() {
        use bevy::prelude::Mat4;
        use forge_assets::source_graph_bundle::{SourceGraphBlob, SourceGraphPipelineState};
        let bytes = b"fn decode() -> f32 { return source_graph.normal_decode_modes[1].w; }";
        let hash = forge_core::bytes_sha256(bytes);
        let mut modes = [None; 8];
        modes[7] = Some(SourceGraphNormalDecodeMode::RgOrAg);
        let contract = SourceGraphMaterialContract {
            schema_version: 2,
            source_program_sha256: hash,
            source_material_sha256: hash,
            forward: SourceGraphBlob {
                buffer_view: 0,
                sha256: hash,
            },
            prepass: SourceGraphBlob {
                buffer_view: 0,
                sha256: hash,
            },
            libraries: vec![],
            textures: vec![],
            state: SourceGraphPipelineState {
                color_write_mask: None,
                color_blend: [1, 0],
                alpha_blend: [1, 0],
                forward_cull: 2,
                prepass_cull: 2,
                depth_compare: 4,
                write_depth: true,
                transparent: false,
                alpha_test: false,
                alpha_to_coverage: false,
                render_queue: 2000,
                disabled_passes: vec![],
            },
            requires_original_vertex_color: false,
            normal_decode_modes: modes,
            scalar_parameters: None,
        };
        let program = source_graph_program_from_glb(&contract, &[bytes]).unwrap();
        assert!(!program.requires_captured_images());
        let source = SourceGraphUniforms {
            source_object_from_target_object: Mat4::IDENTITY,
            target_object_from_source_object: Mat4::IDENTITY,
            source_world_from_target_world: Mat4::IDENTITY,
            source_absolute_world_from_target_world: Mat4::IDENTITY,
            source_time_parameters: Vec4::ZERO,
            source_previous_time_parameters: Vec4::ZERO,
            source_scale_mip_min: Vec4::ONE,
            normal_decode_modes: [Vec4::splat(99.0); 2],
        };
        let material = program
            .register(
                &mut Assets::default(),
                &mut SourceGraphShaderRegistry::default(),
                source,
                vec![],
            )
            .unwrap();
        assert_eq!(
            material.extension.source.normal_decode_modes,
            [Vec4::ZERO, Vec4::new(0.0, 0.0, 0.0, 2.0)]
        );
    }

    #[test]
    fn glb_normal_modes_reach_both_native_uniform_vectors() {
        use SourceGraphNormalDecodeMode::{ReconstructZ, RgOrAg, Rgb};
        let modes = [
            Some(Rgb),
            Some(ReconstructZ),
            Some(RgOrAg),
            None,
            Some(RgOrAg),
            Some(Rgb),
            None,
            Some(ReconstructZ),
        ];
        assert_eq!(
            normal_decode_uniforms(modes),
            [Vec4::new(0.0, 1.0, 2.0, 0.0), Vec4::new(2.0, 0.0, 0.0, 1.0),]
        );
    }

    #[test]
    fn captured_storage_refuses_external_handles_before_shader_registration() {
        use bevy::prelude::Mat4;
        use forge_assets::source_graph_bundle::{SourceGraphBlob, SourceGraphPipelineState};
        let bytes = b"fn decode() -> f32 { return source_graph.normal_decode_modes[1].w; }";
        let hash = forge_core::bytes_sha256(bytes);
        let mut modes = [None; 8];
        modes[7] = Some(SourceGraphNormalDecodeMode::RgOrAg);
        let contract = SourceGraphMaterialContract {
            schema_version: 3,
            source_program_sha256: hash,
            source_material_sha256: hash,
            forward: SourceGraphBlob {
                buffer_view: 0,
                sha256: hash,
            },
            prepass: SourceGraphBlob {
                buffer_view: 0,
                sha256: hash,
            },
            libraries: vec![],
            textures: vec![],
            state: SourceGraphPipelineState {
                color_write_mask: None,
                color_blend: [1, 0],
                alpha_blend: [1, 0],
                forward_cull: 2,
                prepass_cull: 2,
                depth_compare: 4,
                write_depth: true,
                transparent: false,
                alpha_test: false,
                alpha_to_coverage: false,
                render_queue: 2000,
                disabled_passes: vec![],
            },
            requires_original_vertex_color: false,
            normal_decode_modes: modes,
            scalar_parameters: None,
        };
        let program = source_graph_program_from_glb(&contract, &[bytes]).unwrap();
        assert!(program.requires_captured_images());
        let source = SourceGraphUniforms {
            source_object_from_target_object: Mat4::IDENTITY,
            target_object_from_source_object: Mat4::IDENTITY,
            source_world_from_target_world: Mat4::IDENTITY,
            source_absolute_world_from_target_world: Mat4::IDENTITY,
            source_time_parameters: Vec4::ZERO,
            source_previous_time_parameters: Vec4::ZERO,
            source_scale_mip_min: Vec4::ONE,
            normal_decode_modes: [Vec4::splat(99.0); 2],
        };
        let mut shaders = Assets::<Shader>::default();
        let mut registry = SourceGraphShaderRegistry::default();
        let result = program.register(&mut shaders, &mut registry, source, vec![]);
        assert!(matches!(
            result,
            Err(
                "Captured storage requires register_captured_images; external PNG handles are forbidden"
            )
        ));
        assert_eq!(shaders.len(), 0);
    }
}

#[cfg(test)]
mod color_mask_transport_tests {
    use super::*;
    use forge_assets::source_graph_bundle::{SourceGraphBlob, SourceGraphPipelineState};
    fn contract(version: u32, mask: Option<u32>) -> SourceGraphMaterialContract {
        let hash = forge_core::bytes_sha256(b"fn test() -> f32 { return 0.0; }");
        SourceGraphMaterialContract {
            schema_version: version,
            source_program_sha256: hash,
            source_material_sha256: hash,
            forward: SourceGraphBlob {
                buffer_view: 0,
                sha256: hash,
            },
            prepass: SourceGraphBlob {
                buffer_view: 0,
                sha256: hash,
            },
            libraries: vec![],
            textures: vec![],
            state: SourceGraphPipelineState {
                color_write_mask: mask,
                color_blend: [5, 10],
                alpha_blend: [5, 10],
                forward_cull: 0,
                prepass_cull: 0,
                depth_compare: 4,
                write_depth: false,
                transparent: true,
                alpha_test: false,
                alpha_to_coverage: false,
                render_queue: 3000,
                disabled_passes: vec![],
            },
            requires_original_vertex_color: false,
            normal_decode_modes: [None; 8],
            scalar_parameters: None,
        }
    }
    #[test]
    fn schema_four_rgb_reaches_actual_material_program_and_legacy_keeps_all() {
        let bytes = b"fn test() -> f32 { return 0.0; }";
        let current = source_graph_program_from_glb(&contract(4, Some(7)), &[bytes]).unwrap();
        assert_eq!(current.program.color_write_mask, 7);
        assert!(current.requires_captured_images());
        let old = source_graph_program_from_glb(&contract(3, None), &[bytes]).unwrap();
        assert_eq!(old.program.color_write_mask, 15);
    }
    #[test]
    fn unknown_mask_missing_mask_or_mask_in_legacy_contract_refuse() {
        let bytes = b"fn test() -> f32 { return 0.0; }";
        for value in [
            contract(4, None),
            contract(4, Some(16)),
            contract(3, Some(7)),
            contract(99, Some(7)),
        ] {
            assert!(source_graph_program_from_glb(&value, &[bytes]).is_err());
        }
    }
}
