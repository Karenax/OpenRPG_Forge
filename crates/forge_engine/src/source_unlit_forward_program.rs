//! Atomic admission for source programs which originally have only a forward pass.
//! Registration is structural validation; intake retains GPU/runtime qualification.
use super::source_unlit_forward::{
    SourceUnlitForwardExtension, SourceUnlitForwardKey, SourceUnlitForwardMaterial,
};
use crate::source_graph::{
    SOURCE_GRAPH_ADDITIONAL_UV_LOCATIONS, SourceGraphForwardState, SourceGraphScalarUniforms,
    SourceGraphShaderArtifact, SourceGraphShaderRegistry, SourceGraphSurfacePhase,
    SourceGraphUniforms, SourceGraphUvRequirements, register_source_graph_shaders,
};
use bevy::{material::OpaqueRendererMethod, prelude::*, shader::ShaderImport};

pub struct SourceUnlitForwardProgram {
    pub shaders: Vec<SourceGraphShaderArtifact>,
    pub forward_index: usize,
    pub required_texture_count: usize,
    pub forward_state: SourceGraphForwardState,
    pub color_write_mask: u32,
    pub phase: SourceGraphSurfacePhase,
    pub requires_original_uv0: bool,
    pub requires_original_color: bool,
}

fn texture_count(source: &str) -> Result<usize, &'static str> {
    let mut count = 0;
    for line in source.lines() {
        let declaration = line.trim();
        if declaration.contains("@binding(") && declaration.contains("texture_2d<") {
            if count >= 7 {
                return Err("Source forward stage has too many textures");
            }
            let expected = format!(
                "@group(#{{MATERIAL_BIND_GROUP}}) @binding({}) var t{}:texture_2d<f32>;",
                101 + 2 * count,
                count
            );
            if declaration != expected {
                return Err("Source forward texture declaration is not canonical");
            }
            count += 1;
        }
    }
    Ok(count)
}

/// Validate stage, image ABI and render state before atomic SHA registration.
/// No prepass, shadow shader, tangent, missing image or native PBR is synthesized.
#[expect(
    clippy::needless_pass_by_value,
    reason = "Public admission consumes the source program bundle so it cannot be reused after shader registration"
)]
pub fn register_source_unlit_forward_material(
    shaders: &mut Assets<Shader>,
    registry: &mut SourceGraphShaderRegistry,
    program: SourceUnlitForwardProgram,
    source: SourceGraphUniforms,
    scalars: SourceGraphScalarUniforms,
    images: Vec<Handle<Image>>,
) -> Result<SourceUnlitForwardMaterial, &'static str> {
    let artifact = program
        .shaders
        .get(program.forward_index)
        .ok_or("Source forward stage missing")?;
    let stage = Shader::from_wgsl(artifact.source.clone(), "source-unlit-forward".to_owned());
    if matches!(stage.import_path, ShaderImport::Custom(_)) {
        return Err("Forward entry stage is a library module");
    }
    if !artifact.source.contains("@vertex") || !artifact.source.contains("@fragment") {
        return Err("Forward entry stage is incomplete");
    }
    if texture_count(&artifact.source)? != program.required_texture_count {
        return Err("Source forward declarations differ from required image count");
    }
    let additional_uvs = SourceGraphUvRequirements::from_shader(
        &artifact.source,
        SOURCE_GRAPH_ADDITIONAL_UV_LOCATIONS,
    )?;
    let uuid = u128::from_be_bytes(
        artifact.expected_sha256.as_bytes()[..16]
            .try_into()
            .unwrap(),
    );
    let key = SourceUnlitForwardKey {
        vertex: uuid,
        fragment: uuid,
        forward: program.forward_state,
        color_write_mask: program.color_write_mask,
        requires_original_uv0: program.requires_original_uv0,
        requires_original_color: program.requires_original_color,
        additional_uvs,
    };
    let extension = SourceUnlitForwardExtension::from_source_bindings(
        key,
        source,
        program.required_texture_count,
        images,
        scalars,
    )?;
    let material = SourceUnlitForwardMaterial {
        base: StandardMaterial {
            alpha_mode: match program.phase {
                SourceGraphSurfacePhase::Opaque => AlphaMode::Opaque,
                // Routing marker only. Source fragment owns the actual clipping.
                SourceGraphSurfacePhase::AlphaTest => AlphaMode::Mask(0.0),
                SourceGraphSurfacePhase::Transparent => AlphaMode::Blend,
            },
            opaque_render_method: OpaqueRendererMethod::Forward,
            ..Default::default()
        },
        extension,
    };
    register_source_graph_shaders(shaders, registry, &program.shaders)?;
    Ok(material)
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{asset::AssetId, pbr::Material, render::render_resource::AsBindGroup};
    use forge_core::bytes_sha256;
    const SOURCE: &str = "@vertex fn vertex() -> @builtin(position) vec4<f32> { return vec4<f32>(0.0); }\n@fragment fn fragment() -> @location(0) vec4<f32> { return vec4<f32>(1.0); }\n";
    fn program(phase: SourceGraphSurfacePhase) -> SourceUnlitForwardProgram {
        SourceUnlitForwardProgram {
            shaders: vec![SourceGraphShaderArtifact {
                source: SOURCE.to_owned(),
                expected_sha256: bytes_sha256(SOURCE.as_bytes()),
            }],
            forward_index: 0,
            required_texture_count: 0,
            forward_state: SourceGraphForwardState {
                color_src: 5,
                color_dst: 10,
                alpha_src: 5,
                alpha_dst: 10,
                cull: 0,
                compare: 4,
                write_depth: false,
            },
            color_write_mask: 7,
            phase,
            requires_original_uv0: true,
            requires_original_color: true,
        }
    }
    fn source() -> SourceGraphUniforms {
        SourceGraphUniforms {
            source_object_from_target_object: Mat4::IDENTITY,
            target_object_from_source_object: Mat4::IDENTITY,
            source_world_from_target_world: Mat4::IDENTITY,
            source_absolute_world_from_target_world: Mat4::IDENTITY,
            source_time_parameters: Vec4::ZERO,
            source_previous_time_parameters: Vec4::ZERO,
            source_scale_mip_min: Vec4::ONE,
            normal_decode_modes: [Vec4::ZERO; 2],
        }
    }
    #[test]
    fn source_unlit_program_registers_real_shader_and_preserves_source_phase() {
        let mut assets = Assets::<Shader>::default();
        let mut registry = SourceGraphShaderRegistry::default();
        for phase in [
            SourceGraphSurfacePhase::Opaque,
            SourceGraphSurfacePhase::AlphaTest,
            SourceGraphSurfacePhase::Transparent,
        ] {
            let material = register_source_unlit_forward_material(
                &mut assets,
                &mut registry,
                program(phase),
                source(),
                SourceGraphScalarUniforms::default(),
                vec![],
            )
            .unwrap();
            assert_eq!(assets.len(), 1);
            let key = material.extension.bind_group_data();
            assert_eq!(key.color_write_mask, 7);
            assert_eq!(key.vertex, key.fragment);
            assert!(assets.contains(AssetId::Uuid {
                uuid: bevy::asset::uuid::Uuid::from_u128(key.vertex)
            }));
            assert!(!<SourceUnlitForwardMaterial as Material>::enable_prepass());
            assert!(!<SourceUnlitForwardMaterial as Material>::enable_shadows());
            assert_eq!(
                material.opaque_render_method(),
                OpaqueRendererMethod::Forward
            );
            match phase {
                SourceGraphSurfacePhase::Opaque => {
                    assert_eq!(material.alpha_mode(), AlphaMode::Opaque);
                }
                SourceGraphSurfacePhase::AlphaTest => {
                    assert_eq!(material.alpha_mode(), AlphaMode::Mask(0.0));
                }
                SourceGraphSurfacePhase::Transparent => {
                    assert_eq!(material.alpha_mode(), AlphaMode::Blend);
                }
            }
        }
    }
    #[test]
    fn source_unlit_program_missing_images_bad_mask_or_entry_refuse_before_registration() {
        for failure in 0..4 {
            let mut candidate = program(SourceGraphSurfacePhase::Transparent);
            match failure {
                0 => candidate.required_texture_count = 1,
                1 => candidate.color_write_mask = 16,
                2 => candidate.forward_index = 1,
                _ => {
                    candidate.shaders[0].source =
                        "#define_import_path forge_source_graph::library\nfn helper() {}"
                            .to_owned();
                }
            }
            let mut assets = Assets::<Shader>::default();
            let mut registry = SourceGraphShaderRegistry::default();
            assert!(
                register_source_unlit_forward_material(
                    &mut assets,
                    &mut registry,
                    candidate,
                    source(),
                    SourceGraphScalarUniforms::default(),
                    vec![]
                )
                .is_err()
            );
            assert!(assets.is_empty());
        }
    }
    #[test]
    fn source_unlit_program_wrong_full_sha_keeps_existing_shader_bytes() {
        let mut candidate = program(SourceGraphSurfacePhase::Transparent);
        candidate.shaders[0].source.push_str("// changed source\n");
        let mut assets = Assets::<Shader>::default();
        let mut registry = SourceGraphShaderRegistry::default();
        assert!(
            register_source_unlit_forward_material(
                &mut assets,
                &mut registry,
                candidate,
                source(),
                SourceGraphScalarUniforms::default(),
                vec![]
            )
            .is_err()
        );
        assert!(assets.is_empty());
        register_source_unlit_forward_material(
            &mut assets,
            &mut registry,
            program(SourceGraphSurfacePhase::Transparent),
            source(),
            SourceGraphScalarUniforms::default(),
            vec![],
        )
        .unwrap();
        assert_eq!(assets.len(), 1);
        assert_eq!(assets.iter().next().unwrap().1.source.as_str(), SOURCE);
    }
}
