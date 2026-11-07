//! Shared source-compatible plant material, attributes and instance records.
use super::{LOBE, MAT, Params, PlantKey, VEG, world::SourceMaterial};
use bevy::{
    mesh::MeshVertexBufferLayoutRef,
    pbr::{MaterialPipeline, MaterialPipelineKey},
    prelude::*,
    render::{
        render_resource::{AsBindGroup, RenderPipelineDescriptor},
        storage::ShaderBuffer,
    },
    shader::ShaderRef,
};
pub const PLANT_SHADER: Handle<Shader> =
    bevy::asset::uuid_handle!("04a33620-a592-4753-acf1-ec918dd6fa19");

use super::{motion, with_source_derivative_directive};
/// Stage caller-verified WGSL without a filesystem or network fallback.
pub fn stage_shader(world: &mut World, source: &str) -> Result<(), String> {
    if source.trim().is_empty() || source.len() > 1_000_000 {
        return Err("Invalid plant shader source".into());
    }
    let lighting = crate::source_lighting::scene_uniform::prepare(source)?;
    crate::source_lighting::scene_buffer::install(world, &lighting.seed)?;
    let derived = crate::source_lighting::surface_library::reuse_plant_receivers(&lighting.shader)?
        .replace("\r\n", "\n");
    let derived = super::with_valid_bark_power_domain(&derived)?;
    let derived = motion::shader_source(&derived)?;
    let derived = crate::source_shadows::receiver::shader_source(&derived)?;
    let derived =
        crate::source_lighting::local_lights::receiver::plant_shader_source(&derived, 56)?;
    let derived = with_source_derivative_directive(derived);
    crate::source_shadows::receiver::install(world)?;
    crate::source_lighting::local_lights::gpu::install_neutral(world)?;
    motion::install(world)?;
    world
        .get_resource_mut::<Assets<Shader>>()
        .ok_or("Shader assets unavailable")?
        .insert(
            PLANT_SHADER.id(),
            Shader::from_wgsl(derived, "source-plants-captured.wgsl"),
        )
        .map_err(|e| e.to_string())?;
    Ok(())
}

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
#[bind_group_data(PlantKey)]
pub struct CapturedPlantMaterial {
    #[storage(54, read_only)]
    #[dependency]
    pub scene_lighting: Handle<ShaderBuffer>,
    #[uniform(0)]
    pub params: Params,
    #[texture(1)]
    #[sampler(2)]
    #[dependency]
    pub detail: Handle<Image>,
    #[texture(3)]
    #[sampler(4)]
    #[dependency]
    pub leaf: Handle<Image>,
    #[texture(5)]
    #[sampler(6)]
    #[dependency]
    pub impostor_a: Handle<Image>,
    #[texture(7)]
    #[dependency]
    pub impostor_b: Handle<Image>,
    #[storage(8, read_only, visibility(vertex))]
    #[dependency]
    pub instances: Handle<ShaderBuffer>,
    #[storage(9, read_only, visibility(vertex))]
    #[dependency]
    pub models: Handle<ShaderBuffer>,
    #[storage(43, read_only)]
    #[dependency]
    pub motion: Handle<ShaderBuffer>,
    #[texture(10, dimension = "cube")]
    #[sampler(11)]
    #[dependency]
    pub environment: Handle<Image>,
    #[texture(12)]
    #[dependency]
    pub hill_shadow: Handle<Image>,
    #[texture(15, dimension = "2d_array", sample_type = "depth")]
    #[sampler(16, sampler_type = "comparison")]
    #[dependency]
    pub object_shadow: Handle<Image>,
    #[uniform(40)]
    pub clouds: [Vec4; 5],
    #[texture(41, filterable = false)]
    #[dependency]
    pub cloud_shadow: Option<Handle<Image>>,
    #[uniform(17)]
    pub object_settings: crate::source_shadows::ObjectShadowSettings,
    #[storage(44, read_only, visibility(fragment))]
    #[dependency]
    pub live_shadows: Handle<ShaderBuffer>,
    #[storage(56, read_only, visibility(fragment))]
    #[dependency]
    pub local_lights: Handle<ShaderBuffer>,
    #[texture(13, sample_type = "float", filterable = false)]
    #[dependency]
    pub grass_heights: Handle<Image>,
    #[texture(14)]
    #[dependency]
    pub grass_mask: Handle<Image>,
    pub kind: u8,
}
impl From<&CapturedPlantMaterial> for PlantKey {
    fn from(value: &CapturedPlantMaterial) -> Self {
        Self::new(value.kind)
    }
}
impl Material for CapturedPlantMaterial {
    fn enable_prepass() -> bool {
        false
    }
    fn enable_shadows() -> bool {
        false
    }
    fn vertex_shader() -> ShaderRef {
        PLANT_SHADER.into()
    }
    fn fragment_shader() -> ShaderRef {
        PLANT_SHADER.into()
    }
    fn specialize(
        _: &MaterialPipeline,
        descriptor: &mut RenderPipelineDescriptor,
        layout: &MeshVertexBufferLayoutRef,
        key: MaterialPipelineKey<Self>,
    ) -> Result<(), bevy::material::specialize::SpecializedMeshPipelineError> {
        descriptor.vertex.buffers = vec![layout.0.get_layout(&[
            Mesh::ATTRIBUTE_POSITION.at_shader_location(0),
            Mesh::ATTRIBUTE_NORMAL.at_shader_location(1),
            Mesh::ATTRIBUTE_UV_0.at_shader_location(2),
            VEG.at_shader_location(3),
            MAT.at_shader_location(4),
            LOBE.at_shader_location(5),
        ])?];
        descriptor.primitive.cull_mode = None;
        let define = format!("PLANT_KIND_{}", key.bind_group_data.kind());
        descriptor.vertex.shader_defs.push(define.clone().into());
        if let Some(fragment) = &mut descriptor.fragment {
            fragment.shader_defs.push(define.into());
        }
        Ok(())
    }
}

impl SourceMaterial for CapturedPlantMaterial {
    fn params(&self) -> &Params {
        &self.params
    }
    fn params_mut(&mut self) -> &mut Params {
        &mut self.params
    }
    fn kind(&self) -> u8 {
        self.kind
    }
    fn set_source(
        &mut self,
        kind: u8,
        instances: Handle<ShaderBuffer>,
        models: Handle<ShaderBuffer>,
    ) {
        self.kind = kind;
        self.instances = instances;
        self.models = models;
    }
}

#[cfg(test)]
mod shader_tests {
    use super::*;

    #[test]
    fn imported_shader_preserves_source_derivative_uniformity_policy() {
        let original = "@fragment fn fragment() {}".to_owned();
        let staged = with_source_derivative_directive(original.clone());
        assert!(staged.starts_with("diagnostic( off, derivative_uniformity );\n"));
        assert!(staged.ends_with(&original));
        assert_eq!(with_source_derivative_directive(staged.clone()), staged);
    }
}

#[cfg(test)]
mod world_shader_tests {
    fn assert_world_picking_geometry(source: &str, derived: &str) {
        let picked = crate::source_vegetation::picking::world::shader_source(source).unwrap();
        let vertex_start = derived.find("@vertex\nfn vertex(").unwrap();
        let vertex_end =
            derived[vertex_start..].find("\nfn forge_surface(").unwrap() + vertex_start;
        assert!(
            picked
                .replace("    o.pickId=pickingIds.x+tag;\n", "")
                .contains(&derived[vertex_start..vertex_end])
        );
        for name in [
            "sourceVertex",
            "sourceSurface",
            "vegLodScale",
            "vegLodDither",
        ] {
            let anchor = format!("fn {name}(");
            assert_eq!(
                picked.matches(&anchor).count(),
                derived.matches(&anchor).count()
            );
        }
        assert!(picked.contains("let surface=forge_surface(vs,front);\n    let id=vs.pickId;"));
    }

    #[test]
    fn refuses_unknown_shader_without_deriving_a_fallback() {
        assert!(
            crate::source_vegetation::world::shader::derive("@fragment fn fragment() {}").is_err()
        );
    }
    /// Explicit pinned source, never a workspace/network fallback or GPU verdict.
    #[test]
    #[ignore = "Requires FORGE_PLANT_SHADER_PATH pointing to the pinned shader"]
    fn pinned_shader_preserves_reference_lod_and_both_staging_seams() {
        use bevy::prelude::*;
        use std::{io::Read, path::PathBuf};
        let path =
            PathBuf::from(std::env::var_os("FORGE_PLANT_SHADER_PATH").expect("Explicit fixture"));
        assert!(path.is_absolute());
        for parent in path.ancestors() {
            let metadata = parent.symlink_metadata().unwrap();
            assert!(!metadata.file_type().is_symlink());
            #[cfg(windows)]
            {
                use std::os::windows::fs::MetadataExt;
                assert_eq!(metadata.file_attributes() & 0x400, 0);
            }
        }
        let mut bytes = Vec::new();
        std::fs::File::open(path)
            .unwrap()
            .take(1_000_001)
            .read_to_end(&mut bytes)
            .unwrap();
        assert!(bytes.len() <= 1_000_000);
        let source = std::str::from_utf8(&bytes).unwrap();
        let derived = crate::source_vegetation::world::shader::derive(source).unwrap();
        assert_world_picking_geometry(source, &derived);
        // The source BRDF and environment-light algorithms remain the acquired
        // functions. Only scene bindings and the geometry adapter may change.
        for name in [
            "sourceDirectLight",
            "sourceEnvironmentLight",
            "vegTranslucency",
        ] {
            let anchor = format!("fn {name}(");
            let mut rest = source;
            while let Some(start) = rest.find(&anchor) {
                let body = rest[start..].find('{').unwrap() + start;
                let mut depth = 1;
                let end = rest
                    .as_bytes()
                    .iter()
                    .enumerate()
                    .skip(body + 1)
                    .find_map(|(i, byte)| {
                        match byte {
                            b'{' => depth += 1,
                            b'}' => depth -= 1,
                            _ => {}
                        }
                        (depth == 0).then_some(i + 1)
                    })
                    .unwrap();
                assert!(
                    derived.contains(&rest[start..end].replace("\r\n", "\n")),
                    "changed {name}"
                );
                rest = &rest[end..];
            }
        }
        assert!(derived.contains("return forgeSourceFragment(vs,front);"));
        assert_eq!(
            derived
                .matches("+sourceLocalLights(surface,input.P,")
                .count(),
            4
        );
        let local = crate::source_lighting::local_lights::receiver::surface_shader_source(
            "SourceSurface",
            56,
        )
        .unwrap();
        assert!(derived.contains(&local));
        assert!(!derived.contains("fn forgeSourceFragment(vs:ProbeOut,@builtin"));
        assert_eq!(
            derived
                .matches("fn forgeSourceFragment(vs:ProbeOut,front:bool)->vec4f")
                .count(),
            4
        );
        assert!(derived.contains("frame.sunDir,sceneLighting.sunColor.xyz*terrainSunShadowAt"));
        assert_eq!(derived.matches("fn vegLodScale(").count(), 4);
        assert_eq!(derived.matches("fn vegLodDither(").count(), 4);
        assert!(derived.contains("source.iCell=record.iPos;"));
        assert!(derived.contains("@location(15) @interpolate(flat) mesh_flags:u32"));
        assert!(derived.contains("let ground = terrainHeightAt(xz);"));
        assert!(derived.contains("textureLoad(forgeTerrainDeltas"));
        assert!(!derived.contains("forgeCellSelected"));
        assert!(!derived.contains("probe.lodEndFrame.z"));
        let mut world = World::new();
        world.init_resource::<Assets<Shader>>();
        world.init_resource::<Assets<Image>>();
        world.init_resource::<Assets<bevy::render::storage::ShaderBuffer>>();
        crate::source_vegetation::world::stage_shader(&mut world, source).unwrap();
        let light_buffer = world
            .resource::<Assets<bevy::render::storage::ShaderBuffer>>()
            .get(&crate::source_lighting::local_lights::gpu::BUFFER)
            .unwrap();
        assert_eq!(
            light_buffer.data.as_ref().unwrap().len(),
            crate::source_lighting::local_lights::PACKET_BYTES
        );
        super::stage_shader(&mut world, source).unwrap();
    }
}

#[cfg(test)]
mod captured_material_dependencies {
    use super::*;
    use bevy::asset::VisitAssetDependencies;
    #[test]
    fn captured_source_material_declares_all_sixteen_actual_resources() {
        let image = Handle::<Image>::default();
        let buffer = Handle::<ShaderBuffer>::default();
        let base = CapturedPlantMaterial {
            scene_lighting: buffer.clone(),
            params: Params {
                i_pos: Vec4::ZERO,
                i_dat: Vec4::ZERO,
                wind: Vec4::ZERO,
                gust_lod: Vec4::ZERO,
                sun_night: Vec4::ZERO,
                lod_end_frame: Vec4::ZERO,
            },
            detail: image.clone(),
            leaf: image.clone(),
            impostor_a: image.clone(),
            impostor_b: image.clone(),
            instances: buffer.clone(),
            models: buffer.clone(),
            motion: buffer.clone(),
            environment: image.clone(),
            hill_shadow: image.clone(),
            object_shadow: image.clone(),
            clouds: [Vec4::ZERO; 5],
            cloud_shadow: Some(image.clone()),
            object_settings: crate::source_shadows::ObjectShadowSettings::default(),
            live_shadows: buffer,
            local_lights: crate::source_lighting::local_lights::gpu::BUFFER,
            grass_heights: image.clone(),
            grass_mask: image,
            kind: 0,
        };
        let mut dependencies = Vec::new();
        base.visit_dependencies(&mut |id| dependencies.push(id));
        assert_eq!(dependencies.len(), 16);
        assert!(
            dependencies.contains(
                &crate::source_lighting::local_lights::gpu::BUFFER
                    .id()
                    .untyped()
            )
        );
    }
}
