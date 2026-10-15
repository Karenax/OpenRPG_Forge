#[path = "source_graph_srgb_async.rs"]
#[cfg_attr(not(target_arch = "wasm32"), allow(dead_code))]
mod source_srgb_async;
#[path = "source_graph_srgb_curve.rs"]
mod source_srgb_curve;
#[path = "source_graph_srgb_measure.rs"]
mod source_srgb_measure;
#[path = "source_graph_srgb_runtime.rs"]
mod source_srgb_runtime;
#[path = "source_unlit_forward.rs"]
mod source_unlit_forward;
pub use source_unlit_forward::{
    SourceUnlitForwardExtension, SourceUnlitForwardKey, SourceUnlitForwardMaterial,
    SourceUnlitForwardPlugin,
};
#[path = "source_unlit_forward_program.rs"]
mod source_unlit_forward_program;
pub use source_unlit_forward_program::{
    SourceUnlitForwardProgram, register_source_unlit_forward_material,
};
#[path = "source_graph_imported_texture.rs"]
mod source_imported_texture;
pub use source_imported_texture::{
    SourceGraphImportedMip, SourceGraphImportedTexture, SourceGraphImportedTextureFormat,
    SourceGraphImportedTextureMetadata, prepare_source_graph_imported_texture,
};
#[path = "source_graph_shadow_raster.rs"]
mod source_shadow_raster;
pub use source_shadow_raster::SourceGraphShadowRasterState;
#[path = "source_graph_punctual_atlas.rs"]
mod source_punctual_atlas;
pub use source_punctual_atlas::{
    SOURCE_GRAPH_PUNCTUAL_ATLAS_MAPPING_WGSL, SourceGraphPunctualAtlas,
    SourceGraphPunctualAtlasLayout, SourceGraphPunctualViewport,
};
#[path = "source_graph_shadow_routing.rs"]
mod source_shadow_routing;
pub use source_shadow_routing::{
    SourceGraphShadowRoute, SourceGraphShadowRouteStatus, SourceGraphShadowRoutingPlugin,
    install_source_graph_shadow_route, set_source_graph_shadow_enabled,
    uninstall_source_graph_shadow_route,
};
#[path = "source_graph_png.rs"]
mod source_png;
pub use source_png::{SourceGraphPngBase, SourceGraphPngMetadata, decode_source_graph_png_base};
#[path = "source_graph_sampler.rs"]
mod source_sampler;
pub use source_sampler::{
    SourceGraphSamplerSettings, SourceGraphSceneSampling, source_graph_image_sampler,
    source_graph_image_sampler_in_scene,
};
#[path = "source_graph_program.rs"]
mod source_program;
pub use source_program::{SourceGraphMaterialProgram, register_source_graph_material};
#[path = "source_graph_scalars.rs"]
mod source_scalars;
pub use source_scalars::SourceGraphScalarUniforms;
#[path = "source_graph_state_buffer.rs"]
mod source_state_buffer;
pub use source_state_buffer::SourceGraphStateBufferBinding;
#[path = "source_graph_diffuse_probe.rs"]
mod source_diffuse_probe;
pub use source_diffuse_probe::{SOURCE_GRAPH_DIFFUSE_PROBE_WGSL, SourceGraphDiffuseProbeUniforms};
#[path = "source_graph_preintegrated_brdf.rs"]
mod source_preintegrated_brdf;
pub use source_preintegrated_brdf::{
    SOURCE_GRAPH_PREINTEGRATED_BRDF_WGSL, SourceGraphPreintegratedBrdf,
    SourceGraphPreintegratedBrdfBinding,
};
#[path = "source_graph_sky_cube.rs"]
mod source_sky_cube;
pub use source_sky_cube::{SourceGraphSkyCube, SourceGraphSkyCubeBinding};
#[path = "source_graph_phase.rs"]
mod source_phase;
#[path = "source_graph_phase_material.rs"]
mod source_phase_material;
pub use source_phase::{SourceGraphSurfacePhase, source_graph_material};
pub use source_phase_material::SourceGraphPhaseMaterial;
#[path = "source_graph_shader_registry.rs"]
mod source_shader_registry;
pub use source_shader_registry::{
    SourceGraphShaderArtifact, SourceGraphShaderRegistry, register_source_graph_shaders,
};
#[path = "source_graph_render_state.rs"]
mod source_render_state;
pub use source_render_state::SourceGraphForwardState;
#[path = "source_graph_color_write_mask.rs"]
mod source_color_write_mask;
pub use source_color_write_mask::SourceGraphForwardPassState;
#[path = "source_graph_uv.rs"]
mod source_uv;
pub use source_uv::{
    SOURCE_GRAPH_ADDITIONAL_UV_LOCATIONS, SOURCE_GRAPH_ORIGINAL_ADDITIONAL_UVS,
    SOURCE_GRAPH_STANDARD_ADDITIONAL_UVS, source_graph_required_uv_bindings,
};
#[path = "source_graph_uv_requirements.rs"]
mod source_uv_requirements;
pub use source_uv_requirements::SourceGraphUvRequirements;
#[path = "source_graph_position_frame.rs"]
mod source_position_frame;
pub use source_position_frame::{SourceGraphPositionFrame, source_graph_position_frame};
// Generic native adapter for source graph programs. Shader/texture provenance is
// checked by intake; this adapter alone does not qualify an imported material.
use bevy::core_pipeline::prepass::{DepthPrepass, NormalPrepass};
use bevy::{
    mesh::{MeshVertexAttribute, MeshVertexBufferLayoutRef, VertexBufferLayout},
    pbr::{
        ExtendedMaterial, MaterialExtension, MaterialExtensionKey, MaterialExtensionPipeline,
        MaterialPlugin, MeshPipelineKey,
    },
    prelude::*,
    render::render_resource::{
        AsBindGroup, CompareFunction, RenderPipelineDescriptor, ShaderType,
        SpecializedMeshPipelineError, VertexFormat,
    },
    shader::ShaderDefVal,
};
#[path = "source_graph_coverage.rs"]
mod source_coverage;
use source_coverage::{SourceCoveragePolicy, source_coverage_policy};

/// Dedicated source attributes: normalized glTF normals/tangents and default
/// white color cannot substitute for the original shader inputs. The source
/// loader must retain the original values and qualify the source tangent sign.
pub const SOURCE_GRAPH_NORMAL: MeshVertexAttribute = MeshVertexAttribute::new(
    "SourceGraphOriginalNormal",
    0x5347_0008,
    VertexFormat::Float32x3,
);
pub const SOURCE_GRAPH_TANGENT: MeshVertexAttribute = MeshVertexAttribute::new(
    "SourceGraphOriginalTangentSign",
    0x5347_0009,
    VertexFormat::Float32x4,
);
pub const SOURCE_GRAPH_UV0: MeshVertexAttribute = MeshVertexAttribute::new(
    "SourceGraphOriginalUv0",
    0x5347_000a,
    VertexFormat::Float32x4,
);
pub const SOURCE_GRAPH_COLOR: MeshVertexAttribute = MeshVertexAttribute::new(
    "SourceGraphOriginalColor",
    0x5347_000b,
    VertexFormat::Float32x4,
);

fn source_graph_vertex_layout(
    layout: &MeshVertexBufferLayoutRef,
    requires_original_color: bool,
    uv_requirements: SourceGraphUvRequirements,
) -> Result<VertexBufferLayout, SpecializedMeshPipelineError> {
    let mut attributes = vec![
        Mesh::ATTRIBUTE_POSITION.at_shader_location(0),
        Mesh::ATTRIBUTE_UV_0.at_shader_location(2),
        SOURCE_GRAPH_NORMAL.at_shader_location(8),
        SOURCE_GRAPH_TANGENT.at_shader_location(9),
        SOURCE_GRAPH_UV0.at_shader_location(10),
    ];
    if requires_original_color {
        attributes.push(SOURCE_GRAPH_COLOR.at_shader_location(11));
    }
    for (attribute, location) in source_graph_required_uv_bindings(uv_requirements.channels())
        .expect("Admitted UV requirements contain only additional source channels")
    {
        attributes.push(attribute.at_shader_location(location));
    }
    Ok(layout.0.get_layout(&attributes)?)
}

#[derive(Clone, Debug, ShaderType, Reflect)]
pub struct SourceGraphUniforms {
    /// Explicit source basis mappings; no assumed Unity/Gltf coordinate alias.
    pub source_object_from_target_object: Mat4,
    pub target_object_from_source_object: Mat4,
    pub source_world_from_target_world: Mat4,
    pub source_absolute_world_from_target_world: Mat4,
    /// Source time parameters and previous time for displacement history.
    pub source_time_parameters: Vec4,
    pub source_previous_time_parameters: Vec4,
    /// Original inverse object X/Z scale; mip bias; pinned source `FLT_MIN`.
    pub source_scale_mip_min: Vec4,
    /// Explicit per-image original normal decoder; never inferred from a name.
    pub normal_decode_modes: [Vec4; 2],
}

#[derive(Copy, Clone, Debug, PartialEq, Eq, Hash, Reflect)]
#[expect(
    clippy::struct_excessive_bools,
    reason = "Independent authored coverage, color and scene resource flags are retained in the pipeline key"
)]
pub struct SourceGraphKey {
    pub requires_source_state: bool,
    pub source_state_ready: bool,
    pub requires_native_srgb_encoder: bool,
    pub native_srgb_encoder_ready: bool,
    pub requires_deferred_srgb: bool,
    pub deferred_srgb_ready: bool,
    pub requires_preintegrated_brdf: bool,
    pub preintegrated_brdf_ready: bool,
    pub requires_sky_cube: bool,
    pub sky_cube_ready: bool,
    /// Explicit source non-forward culling; independent of forward culling.
    pub prepass_cull: u8,
    /// Explicit host routing, never inferred from depth/projection flags.
    pub shadow_only: bool,
    /// Explicit raster policy included in pipeline identity; absence preserves native state.
    pub orthographic_shadow_raster: Option<SourceGraphShadowRasterState>,
    pub vertex: u128,
    pub fragment: u128,
    pub prepass_vertex: u128,
    pub prepass_fragment: u128,
    pub original_alpha_to_mask: bool,
    /// Derived from actual reads in the pinned active source DAG.
    pub requires_original_vertex_color: bool,
    /// Derived from the pinned vertex declarations; participates in pipeline caching.
    pub original_uv_requirements: SourceGraphUvRequirements,
    /// Explicit renderer channel mask; included in Eq/Hash pipeline caching.
    pub color_write_mask: u32,
    pub forward_state: SourceGraphForwardState,
}

#[derive(Asset, AsBindGroup, Clone, Debug, Reflect)]
#[bind_group_data(SourceGraphKey)]
pub struct SourceGraphExtension {
    /// UUID identities registered in Assets<Shader>, required by packed Bevy material keys.
    pub program_key: SourceGraphKey,
    pub original_alpha_to_mask: bool,
    #[uniform(100)]
    pub source: SourceGraphUniforms,
    /// GPU state remains owned by its producer; no per-particle CPU projection.
    #[storage(130, read_only)]
    source_state: Handle<bevy::render::storage::ShaderBuffer>,
    #[reflect(ignore)]
    source_state_admitted: bool,
    /// Additional declaration-ordered values for programs with runtime properties.
    #[uniform(120)]
    pub scalars: SourceGraphScalarUniforms,
    /// Explicit scene irradiance; disabled until measured data and basis are supplied.
    #[uniform(121)]
    pub diffuse_probe: SourceGraphDiffuseProbeUniforms,
    /// Only the validated shared-table constructor can supply this binding.
    #[texture(122, sample_type = "float", filterable = false)]
    preintegrated_brdf: Option<Handle<Image>>,
    /// Canonical 256-code sRGB texture decoded by the current renderer.
    #[texture(128, sample_type = "float", filterable = false)]
    native_srgb_thresholds: Option<Handle<Image>>,
    #[reflect(ignore)]
    native_srgb_binding: Option<source_srgb_runtime::NativeSrgbBinding>,
    #[reflect(ignore)]
    native_srgb_validated: bool,
    #[texture(127)]
    deferred_srgb_lookup: Option<Handle<Image>>,
    #[texture(123, dimension = "cube_array")]
    #[sampler(124)]
    sky_cube: Option<Handle<Image>>,
    /// Admission metadata stays opaque; the sampled handle is reflected above.
    #[reflect(ignore)]
    sky_cube_binding: Option<SourceGraphSkyCubeBinding>,
    #[texture(101)]
    #[sampler(102)]
    pub texture_0: Option<Handle<Image>>,
    #[texture(103)]
    #[sampler(104)]
    pub texture_1: Option<Handle<Image>>,
    #[texture(105)]
    #[sampler(106)]
    pub texture_2: Option<Handle<Image>>,
    #[texture(107)]
    #[sampler(108)]
    pub texture_3: Option<Handle<Image>>,
    #[texture(109)]
    #[sampler(110)]
    pub texture_4: Option<Handle<Image>>,
    #[texture(111)]
    #[sampler(112)]
    pub texture_5: Option<Handle<Image>>,
    #[texture(113)]
    #[sampler(114)]
    pub texture_6: Option<Handle<Image>>,
    #[texture(115)]
    #[sampler(116)]
    pub texture_7: Option<Handle<Image>>,
    #[texture(117)]
    #[sampler(118)]
    pub texture_8: Option<Handle<Image>>,
    #[texture(125)]
    #[sampler(126)]
    pub texture_9: Option<Handle<Image>>,
}
impl From<&SourceGraphExtension> for SourceGraphKey {
    fn from(value: &SourceGraphExtension) -> Self {
        SourceGraphKey {
            source_state_ready: value.source_state_admitted,
            original_alpha_to_mask: value.original_alpha_to_mask,
            native_srgb_encoder_ready: value.native_srgb_validated
                && value.native_srgb_thresholds.is_some()
                && value.native_srgb_binding.is_some(),
            deferred_srgb_ready: value.deferred_srgb_lookup.is_some(),
            preintegrated_brdf_ready: value.preintegrated_brdf.is_some(),
            sky_cube_ready: value.sky_cube.is_some() && value.sky_cube_binding.is_some(),
            ..value.program_key
        }
    }
}
impl SourceGraphExtension {
    pub fn set_sky_cube(&mut self, binding: &SourceGraphSkyCubeBinding) {
        self.sky_cube = Some(binding.handle().clone());
        self.sky_cube_binding = Some(binding.clone());
    }
    pub fn clear_sky_cube(&mut self) {
        self.sky_cube = None;
        self.sky_cube_binding = None;
    }
    pub fn set_preintegrated_brdf(&mut self, binding: &SourceGraphPreintegratedBrdfBinding) {
        self.preintegrated_brdf = Some(binding.handle().clone());
    }
    /// Install a source-independent code texture; this does not qualify scene parity.
    pub fn install_deferred_srgb_lookup(&mut self, images: &mut Assets<Image>) {
        use bevy::render::render_resource::{Extent3d, TextureDimension, TextureFormat};
        let bytes: Vec<u8> = (u8::MIN..=u8::MAX)
            .flat_map(|code| [code, code, code, 255])
            .collect();
        self.deferred_srgb_lookup = Some(images.add(Image::new(
            Extent3d {
                width: 256,
                height: 1,
                depth_or_array_layers: 1,
            },
            TextureDimension::D2,
            bytes,
            TextureFormat::Rgba8UnormSrgb,
            bevy::asset::RenderAssetUsages::MAIN_WORLD
                | bevy::asset::RenderAssetUsages::RENDER_WORLD,
        )));
    }
    pub fn clear_preintegrated_brdf(&mut self) {
        self.preintegrated_brdf = None;
    }
    /// Reject required scene resources before mutating any frame clocks.
    /// Content SHA is checked by the opaque table constructor, not inferred here.
    pub fn validate_scene_dependencies(&self, images: &Assets<Image>) -> Result<(), &'static str> {
        if self.program_key.requires_source_state && !self.source_state_admitted {
            return Err("Required source state buffer is not bound");
        }
        source_srgb_runtime::validate_extension(self, images)?;
        if self.program_key.requires_deferred_srgb && self.deferred_srgb_lookup.is_none() {
            return Err("Required deferred sRGB lookup is absent");
        }
        if let Some(handle) = &self.deferred_srgb_lookup {
            let image = images
                .get(handle)
                .ok_or("Deferred sRGB code texture is not resident")?;
            let expected: Vec<u8> = (u8::MIN..=u8::MAX)
                .flat_map(|code| [code, code, code, 255])
                .collect();
            if image.texture_descriptor.format
                != bevy::render::render_resource::TextureFormat::Rgba8UnormSrgb
                || image.texture_descriptor.dimension
                    != bevy::render::render_resource::TextureDimension::D2
                || image.texture_descriptor.size.width != 256
                || image.texture_descriptor.size.height != 1
                || image.texture_descriptor.size.depth_or_array_layers != 1
                || image.texture_descriptor.mip_level_count != 1
                || image.data.as_deref() != Some(expected.as_slice())
            {
                return Err("Deferred sRGB code texture storage changed");
            }
        }
        if self.program_key.requires_sky_cube {
            let binding = self
                .sky_cube_binding
                .as_ref()
                .ok_or("Required source sky is not bound")?;
            if self.sky_cube.as_ref() != Some(binding.handle()) {
                return Err("Required source sky binding identity changed");
            }
            binding.validate_residency(images)?;
        }
        if !self.program_key.requires_preintegrated_brdf {
            return Ok(());
        }
        let handle = self
            .preintegrated_brdf
            .as_ref()
            .ok_or("Required source scene BRDF is not bound")?;
        let image = images
            .get(handle)
            .ok_or("Required source scene BRDF image is not resident")?;
        let d = &image.texture_descriptor;
        if d.format != bevy::render::render_resource::TextureFormat::Rgba32Float
            || d.dimension != bevy::render::render_resource::TextureDimension::D2
            || d.size.width != 64
            || d.size.height != 64
            || d.size.depth_or_array_layers != 1
            || d.mip_level_count != 1
            || image.data.as_ref().map(Vec::len) != Some(65536)
        {
            return Err("Required source scene BRDF image storage changed");
        }
        Ok(())
    }
    /// Apply the validated per-instance packet. Programs without scalar reads
    /// keep their original source/time ABI and ignore this separate binding.
    pub fn set_scalar_parameters(
        &mut self,
        parameters: &forge_assets::source_material_parameters::SourceMaterialScalarParameters,
    ) {
        self.scalars = SourceGraphScalarUniforms::from_parameters(parameters);
    }
    /// Every source-declared image must be supplied. Padding slots are unused by
    /// the generated program, and cannot replace a missing source texture.
    pub fn from_source_bindings(
        key: SourceGraphKey,
        source: SourceGraphUniforms,
        required_texture_count: usize,
        images: Vec<Handle<Image>>,
    ) -> Result<Self, &'static str> {
        SourceGraphForwardPassState {
            forward: key.forward_state,
            color_write_mask: key.color_write_mask,
        }
        .validate()?;
        source_graph_cull_face(key.prepass_cull, false)?;
        if required_texture_count > forge_assets::source_graph_bundle::SOURCE_GRAPH_MAX_TEXTURES
            || images.len() != required_texture_count
        {
            return Err("Source graph texture binding count differs from pinned program");
        }
        let mut textures = images.into_iter().map(Some).chain(std::iter::repeat(None));
        if [
            key.vertex,
            key.fragment,
            key.prepass_vertex,
            key.prepass_fragment,
        ]
        .contains(&0)
        {
            return Err("Source graph program shader UUID is missing");
        }
        Ok(Self {
            program_key: key,
            original_alpha_to_mask: key.original_alpha_to_mask,
            scalars: SourceGraphScalarUniforms::default(),
            diffuse_probe: SourceGraphDiffuseProbeUniforms::default(),
            preintegrated_brdf: None,
            native_srgb_thresholds: None,
            native_srgb_binding: None,
            native_srgb_validated: false,
            deferred_srgb_lookup: None,
            sky_cube: None,
            sky_cube_binding: None,
            source,
            source_state: source_state_buffer::unused_source_state_handle(),
            source_state_admitted: false,
            texture_0: textures.next().unwrap(),
            texture_1: textures.next().unwrap(),
            texture_2: textures.next().unwrap(),
            texture_3: textures.next().unwrap(),
            texture_4: textures.next().unwrap(),
            texture_5: textures.next().unwrap(),
            texture_6: textures.next().unwrap(),
            texture_7: textures.next().unwrap(),
            texture_8: textures.next().unwrap(),
            texture_9: textures.next().unwrap(),
        })
    }
}
fn source_shader_handle(id: u128) -> Handle<Shader> {
    Handle::Uuid(
        bevy::asset::uuid::Uuid::from_u128(id),
        std::marker::PhantomData,
    )
}
// A native depth-only opaque prepass may omit its fragment even with
// custom shader declarations. Source graph clipping and depth outputs still
// need an actual fragment. Keep existing MRT outputs when they are present.
fn source_graph_bind_stages(
    descriptor: &mut RenderPipelineDescriptor,
    vertex: u128,
    fragment: u128,
    prepass: bool,
) {
    descriptor.vertex.shader = source_shader_handle(vertex);
    if prepass && descriptor.fragment.is_none() {
        descriptor.fragment = Some(bevy::render::render_resource::FragmentState {
            shader: source_shader_handle(fragment),
            shader_defs: descriptor.vertex.shader_defs.clone(),
            targets: Vec::new(),
            ..Default::default()
        });
    }
    if let Some(stage) = &mut descriptor.fragment {
        stage.shader = source_shader_handle(fragment);
    }
}
fn source_graph_cull_face(
    mode: u8,
    inverted_view: bool,
) -> Result<Option<bevy::render::render_resource::Face>, &'static str> {
    use bevy::render::render_resource::Face;
    match (mode, inverted_view) {
        (0, _) => Ok(None),
        (1, false) | (2, true) => Ok(Some(Face::Front)),
        (2, false) | (1, true) => Ok(Some(Face::Back)),
        _ => Err("Source prepass cull mode requires qualification"),
    }
}
fn source_graph_bind_role(
    descriptor: &mut RenderPipelineDescriptor,
    shadow_only: bool,
    prepass: bool,
    vertex: u128,
    fragment: u128,
) {
    // A shadow-only host proxy must never draw in a camera. Fail closed if it
    // accidentally reaches forward specialization. UUID0 is never registered.
    if shadow_only && !prepass {
        source_graph_bind_stages(descriptor, 0, 0, false);
        return;
    }
    let keep = |v: &ShaderDefVal| match v {
        ShaderDefVal::Bool(n, _) | ShaderDefVal::Int(n, _) | ShaderDefVal::UInt(n, _) => {
            n != "SOURCE_GRAPH_SHADOW_PASS"
        }
    };
    descriptor.vertex.shader_defs.retain(keep);
    if let Some(f) = &mut descriptor.fragment {
        f.shader_defs.retain(keep);
    }
    if shadow_only {
        descriptor
            .vertex
            .shader_defs
            .push("SOURCE_GRAPH_SHADOW_PASS".into());
        if let Some(f) = &mut descriptor.fragment {
            f.shader_defs.push("SOURCE_GRAPH_SHADOW_PASS".into());
        }
    }
    source_graph_bind_stages(descriptor, vertex, fragment, prepass);
}
/// Copy exact admitted source bindings into a distinct pipeline-cache key.
/// The host must use this only on an identical child mesh at identity transform,
/// excluded from ALL cameras by a private render layer. The original mesh must
/// have `NotShadowCaster`, and its affected lights must include the private layer.
/// This factory does not install those host routes or qualify GPU rendering.
pub fn source_graph_shadow_only_material(
    original: &SourceGraphMaterial,
) -> Result<SourceGraphMaterial, &'static str> {
    if original.extension.program_key.shadow_only {
        return Err("Source material already belongs to shadow-only routing");
    }
    let mut shadow = original.clone();
    shadow.extension.program_key.shadow_only = true;
    Ok(shadow)
}
impl MaterialExtension for SourceGraphExtension {
    // Native Bevy decides the material layout and fragment-stage presence
    // before specialize. Declare both per-program prepass stages explicitly; source
    // bindings already use the inherited StandardMaterial layout.
    // Zero is an intentionally unregistered fail-closed placeholder: specialize
    // replaces it with the admitted per-material UUID, never a native fallback.
    fn prepass_vertex_shader() -> bevy::shader::ShaderRef {
        bevy::shader::ShaderRef::Handle(source_shader_handle(0))
    }
    fn prepass_fragment_shader() -> bevy::shader::ShaderRef {
        bevy::shader::ShaderRef::Handle(source_shader_handle(0))
    }

    #[expect(
        clippy::too_many_lines,
        reason = "Vertex ABI and pass state are validated before shader definitions and pipeline state are installed"
    )]
    fn specialize(
        _pipeline: &MaterialExtensionPipeline,
        descriptor: &mut RenderPipelineDescriptor,
        layout: &MeshVertexBufferLayoutRef,
        key: MaterialExtensionKey<Self>,
    ) -> Result<(), SpecializedMeshPipelineError> {
        if (key.bind_group_data.requires_source_state && !key.bind_group_data.source_state_ready)
            || (key.bind_group_data.requires_native_srgb_encoder
                && !key.bind_group_data.native_srgb_encoder_ready)
            || (key.bind_group_data.requires_deferred_srgb
                && !key.bind_group_data.deferred_srgb_ready)
            || (key.bind_group_data.requires_preintegrated_brdf
                && !key.bind_group_data.preintegrated_brdf_ready)
            || (key.bind_group_data.requires_sky_cube && !key.bind_group_data.sky_cube_ready)
        {
            // Explicit source scene dependency missing: never draw with the
            // optional texture's placeholder or a native BRDF substitute.
            source_graph_bind_stages(descriptor, 0, 0, false);
            return Ok(());
        }
        // Fail closed when the source loader has not supplied exact raw data.
        descriptor.vertex.buffers = vec![source_graph_vertex_layout(
            layout,
            key.bind_group_data.requires_original_vertex_color,
            key.bind_group_data.original_uv_requirements,
        )?];
        // A source DAG that reads original colors always requires its raw
        // color payload. An unused color input is removed from that program,
        // never replaced with white or an ordinary glTF color.
        let keep_non_color_def = |value: &ShaderDefVal| match value {
            ShaderDefVal::Bool(name, _)
            | ShaderDefVal::Int(name, _)
            | ShaderDefVal::UInt(name, _) => name != "VERTEX_COLORS" && name != "VERTEX_UVS_B",
        };
        descriptor.vertex.shader_defs.retain(keep_non_color_def);
        if let Some(fragment) = &mut descriptor.fragment {
            fragment.shader_defs.retain(keep_non_color_def);
        }
        if key.bind_group_data.requires_original_vertex_color {
            descriptor.vertex.shader_defs.push("VERTEX_COLORS".into());
            if let Some(fragment) = &mut descriptor.fragment {
                fragment.shader_defs.push("VERTEX_COLORS".into());
            }
        }
        for name in [
            "VERTEX_NORMALS",
            "VERTEX_TANGENTS",
            "VERTEX_UVS_A",
            "VERTEX_OUTPUT_INSTANCE_INDEX",
        ] {
            descriptor.vertex.shader_defs.push(name.into());
            if let Some(fragment) = &mut descriptor.fragment {
                fragment.shader_defs.push(name.into());
            }
        }
        let prepass =
            descriptor.vertex.shader_defs.iter().any(
                |value| matches!(value,ShaderDefVal::Bool(name,true) if name=="PREPASS_PIPELINE"),
            );
        source_graph_bind_role(
            descriptor,
            key.bind_group_data.shadow_only,
            prepass,
            if prepass {
                key.bind_group_data.prepass_vertex
            } else {
                key.bind_group_data.vertex
            },
            if prepass {
                key.bind_group_data.prepass_fragment
            } else {
                key.bind_group_data.fragment
            },
        );
        if key.bind_group_data.shadow_only && !prepass {
            return Ok(());
        }
        if let Some(raster) = key.bind_group_data.orthographic_shadow_raster
            && raster
                .apply(
                    descriptor,
                    key.bind_group_data.shadow_only,
                    prepass,
                    SourceGraphShadowRasterState::is_orthographic_view(key.mesh_key),
                )
                .is_err()
        {
            source_graph_bind_stages(descriptor, 0, 0, prepass);
            return Ok(());
        }

        if !prepass
            && (SourceGraphForwardPassState {
                forward: key.bind_group_data.forward_state,
                color_write_mask: key.bind_group_data.color_write_mask,
            })
            .apply(descriptor)
            .is_err()
        {
            // UUID zero is forbidden by program admission. An unsupported
            // descriptor cannot draw a default/source-fragment substitute.
            descriptor.vertex.shader = source_shader_handle(0);
            if let Some(fragment) = &mut descriptor.fragment {
                fragment.shader = source_shader_handle(0);
            }
            return Ok(());
        }
        // Preserve native view inversion (mirrors/reflections) for BOTH
        // forward and source non-forward states; no change to front-face order.
        let cull = if prepass {
            key.bind_group_data.prepass_cull
        } else {
            key.bind_group_data.forward_state.cull
        };
        if let Ok(face) =
            source_graph_cull_face(cull, key.mesh_key.contains(MeshPipelineKey::INVERT_CULLING))
        {
            descriptor.primitive.cull_mode = face;
        } else {
            descriptor.vertex.shader = source_shader_handle(0);
            if let Some(f) = &mut descriptor.fragment {
                f.shader = source_shader_handle(0);
            }
            return Ok(());
        }
        let policy = source_coverage_policy(
            key.bind_group_data.original_alpha_to_mask,
            key.mesh_key.msaa_samples(),
            key.mesh_key.contains(MeshPipelineKey::DEPTH_PREPASS),
            key.mesh_key.contains(MeshPipelineKey::NORMAL_PREPASS),
            prepass,
        );
        match policy {
            SourceCoveragePolicy::Unchanged => {}
            SourceCoveragePolicy::PrepassCoverage => {
                descriptor.multisample.alpha_to_coverage_enabled = true;
                if let Some(fragment) = &mut descriptor.fragment {
                    fragment
                        .shader_defs
                        .push("SOURCE_GRAPH_ALPHA_TO_COVERAGE".into());
                }
            }
            SourceCoveragePolicy::ForwardDepthEqual => {
                if let Some(depth) = &mut descriptor.depth_stencil {
                    depth.depth_compare = Some(CompareFunction::Equal);
                }
            }
            SourceCoveragePolicy::RejectMissingPrepass => {
                // The generated graph fragment must discard under this define.
                // Program admission also verifies the fail-closed branch.
                if let Some(fragment) = &mut descriptor.fragment {
                    fragment
                        .shader_defs
                        .push("SOURCE_GRAPH_COVERAGE_PREPASS_REQUIRED".into());
                }
            }
        }
        Ok(())
    }
}
pub type SourceGraphMaterial = ExtendedMaterial<SourceGraphPhaseMaterial, SourceGraphExtension>;

/// Explicit installation by the host after source bindings are prepared. No
/// static source-fragment material, unknown shader, or missing texture fallback.
pub struct SourceGraphPlugin;
impl Plugin for SourceGraphPlugin {
    fn build(&self, app: &mut App) {
        source_state_buffer::install_unused_state_asset(app);
        bevy::shader::load_shader_library!(app, "source_graph_srgb_encode.wgsl");
        app.init_resource::<source_srgb_runtime::NativeSrgbRuntime>();
        app.add_systems(Last, source_srgb_runtime::ensure_native_srgb_bindings);
        app.init_resource::<SourceGraphShaderRegistry>();
        app.add_plugins(SourceGraphShadowRoutingPlugin);
        app.add_plugins(MaterialPlugin::<SourceGraphMaterial>::default());
        app.add_plugins(SourceUnlitForwardPlugin);
        app.add_systems(Update, ensure_source_graph_coverage_prepasses);
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned Res and Query parameter wrappers."
)]
#[expect(
    clippy::type_complexity,
    reason = "Bevy material queries declare their component tuple and filters in the SystemParam signature"
)]
fn ensure_source_graph_coverage_prepasses(
    mut commands: Commands,
    materials: Res<Assets<SourceGraphMaterial>>,
    cameras: Query<(Entity, &Msaa, Option<&DepthPrepass>, Option<&NormalPrepass>), With<Camera3d>>,
) {
    let needs_depth = materials
        .iter()
        .any(|(_, m)| m.extension.program_key.forward_state.compare == 3);
    let needs_coverage = materials
        .iter()
        .any(|(_, m)| m.extension.original_alpha_to_mask);
    if !needs_depth && !needs_coverage {
        return;
    }
    for (entity, msaa, depth, normal) in &cameras {
        let coverage = needs_coverage && msaa.samples() > 1;
        if (needs_depth || coverage) && depth.is_none() {
            commands.entity(entity).insert(DepthPrepass);
        }
        if coverage && normal.is_none() {
            commands.entity(entity).insert(NormalPrepass);
        }
    }
}

#[cfg(test)]
mod source_graph_vertex_data_tests {
    use super::*;
    use bevy::{
        asset::RenderAssetUsages, mesh::MeshVertexBufferLayouts,
        render::render_resource::PrimitiveTopology,
    };
    fn ordinary_gltf_mesh() -> Mesh {
        let mut mesh = Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::default(),
        );
        mesh.insert_attribute(Mesh::ATTRIBUTE_POSITION, vec![[0.0, 0.0, 0.0]]);
        mesh.insert_attribute(Mesh::ATTRIBUTE_UV_0, vec![[0.2, 0.7]]);
        mesh.insert_attribute(Mesh::ATTRIBUTE_NORMAL, vec![[0.0, 1.0, 0.0]]);
        mesh.insert_attribute(Mesh::ATTRIBUTE_TANGENT, vec![[1.0, 0.0, 0.0, 1.0]]);
        mesh.insert_attribute(Mesh::ATTRIBUTE_COLOR, vec![[1.0, 1.0, 1.0, 1.0]]);
        mesh
    }
    fn layout(mesh: &Mesh) -> MeshVertexBufferLayoutRef {
        mesh.get_mesh_vertex_buffer_layout(&mut MeshVertexBufferLayouts::default())
    }
    #[test]
    fn ordinary_normalized_gltf_data_cannot_replace_original_source_inputs() {
        assert!(
            source_graph_vertex_layout(
                &layout(&ordinary_gltf_mesh()),
                true,
                SourceGraphUvRequirements::default()
            )
            .is_err()
        );
    }
    #[test]
    fn ordinary_white_color_cannot_replace_missing_original_source_color() {
        let mut mesh = ordinary_gltf_mesh();
        mesh.insert_attribute(SOURCE_GRAPH_NORMAL, vec![[0.0, 2.0, 0.0]]);
        mesh.insert_attribute(SOURCE_GRAPH_TANGENT, vec![[3.0, 0.0, 0.0, -1.0]]);
        mesh.insert_attribute(SOURCE_GRAPH_UV0, vec![[0.2, 0.3, 0.0, 0.0]]);
        assert!(
            source_graph_vertex_layout(&layout(&mesh), true, SourceGraphUvRequirements::default())
                .is_err()
        );
    }
    #[test]
    fn unused_original_color_does_not_require_fabricating_a_missing_channel() {
        let mut mesh = ordinary_gltf_mesh();
        mesh.remove_attribute(Mesh::ATTRIBUTE_COLOR);
        assert!(
            source_graph_vertex_layout(&layout(&mesh), false, SourceGraphUvRequirements::default())
                .is_err()
        );
        mesh.insert_attribute(SOURCE_GRAPH_NORMAL, vec![[0.0, 2.0, 0.0]]);
        mesh.insert_attribute(SOURCE_GRAPH_TANGENT, vec![[3.0, 0.0, 0.0, -1.0]]);
        mesh.insert_attribute(SOURCE_GRAPH_UV0, vec![[0.2, 0.3, 0.0, 0.0]]);
        assert!(
            source_graph_vertex_layout(&layout(&mesh), false, SourceGraphUvRequirements::default())
                .is_ok()
        );
        assert!(
            source_graph_vertex_layout(&layout(&mesh), true, SourceGraphUvRequirements::default())
                .is_err()
        );
        assert!(mesh.attribute(SOURCE_GRAPH_COLOR).is_none());
    }
    #[test]
    fn original_payload_without_ordinary_color_can_supply_native_stage() {
        let mut mesh = ordinary_gltf_mesh();
        mesh.remove_attribute(Mesh::ATTRIBUTE_COLOR);
        mesh.insert_attribute(SOURCE_GRAPH_NORMAL, vec![[0.0, 2.0, 0.0]]);
        mesh.insert_attribute(SOURCE_GRAPH_TANGENT, vec![[3.0, 0.0, 0.0, -1.0]]);
        mesh.insert_attribute(SOURCE_GRAPH_UV0, vec![[0.2, 0.3, 0.0, 0.0]]);
        mesh.insert_attribute(SOURCE_GRAPH_COLOR, vec![[0.1, 0.4, 0.8, 0.2]]);
        assert!(
            source_graph_vertex_layout(&layout(&mesh), true, SourceGraphUvRequirements::default())
                .is_ok()
        );
    }
    #[test]
    fn required_source_uv1_is_not_replaced_by_lightmap_uv_or_another_channel() {
        let mut mesh = ordinary_gltf_mesh();
        mesh.remove_attribute(Mesh::ATTRIBUTE_COLOR);
        mesh.insert_attribute(SOURCE_GRAPH_NORMAL, vec![[0.0, 2.0, 0.0]]);
        mesh.insert_attribute(SOURCE_GRAPH_TANGENT, vec![[3.0, 0.0, 0.0, -1.0]]);
        mesh.insert_attribute(SOURCE_GRAPH_UV0, vec![[0.2, 0.3, 0.0, 0.0]]);
        mesh.insert_attribute(Mesh::ATTRIBUTE_UV_1, vec![[0.8, 0.7]]);
        mesh.insert_attribute(
            SOURCE_GRAPH_ORIGINAL_ADDITIONAL_UVS[1],
            vec![[0.8, 0.7, 0.0, 0.0]],
        );
        let requirements = SourceGraphUvRequirements::from_shader(
            "struct SourceGraphVertex {\n @location(12) source_uv1:vec4<f32>,\n}",
            SOURCE_GRAPH_ADDITIONAL_UV_LOCATIONS,
        )
        .unwrap();
        assert!(source_graph_vertex_layout(&layout(&mesh), false, requirements).is_err());
        mesh.insert_attribute(
            SOURCE_GRAPH_ORIGINAL_ADDITIONAL_UVS[0],
            vec![[0.8, 0.7, 0.0, 0.0]],
        );
        let bound = source_graph_vertex_layout(&layout(&mesh), false, requirements).unwrap();
        let uv1 = bound
            .attributes
            .iter()
            .find(|attribute| attribute.shader_location == 12)
            .unwrap();
        assert_eq!(uv1.format, VertexFormat::Float32x4);
        assert!(
            !bound
                .attributes
                .iter()
                .any(|attribute| attribute.shader_location == 11)
        );
    }
}

#[cfg(test)]
mod source_prepass_declaration_tests {
    use super::*;
    use bevy::{pbr::Material, shader::ShaderRef};
    #[test]
    fn source_graph_prepass_material_declares_both_stages_before_specialization() {
        // Exercise ExtendedMaterial forwarding as used by native material
        // extraction, not only the extension methods. Native depth-only opaque prepasses can omit a fragment even with
        // inherited material bindings; explicit source stages plus bind_stages
        // preserve the actual source fragment.
        for reference in [
            <SourceGraphMaterial as Material>::prepass_vertex_shader(),
            <SourceGraphMaterial as Material>::prepass_fragment_shader(),
        ] {
            match reference {
                ShaderRef::Handle(handle) => assert_eq!(handle.id(), source_shader_handle(0).id()),
                _ => panic!("Source prepass became native default or a path-loaded fallback"),
            }
        }
        assert!(matches!(
            <StandardMaterial as Material>::prepass_vertex_shader(),
            ShaderRef::Default
        ));
        match <StandardMaterial as Material>::prepass_fragment_shader() {
            ShaderRef::Path(path) => assert!(path.to_string().ends_with("pbr_prepass.wgsl")),
            _ => panic!("Pinned native StandardMaterial prepass declaration changed"),
        }
    }
}

#[cfg(test)]
mod source_prepass_fragment_tests {
    use super::*;
    use bevy::render::render_resource::{
        ColorTargetState, ColorWrites, FragmentState, TextureFormat,
    };
    #[test]
    fn source_graph_prepass_depth_only_keeps_source_fragment_and_native_definitions() {
        let mut d = RenderPipelineDescriptor::default();
        d.vertex.shader_defs = vec![
            "PREPASS_PIPELINE".into(),
            "DEPTH_PREPASS".into(),
            "VIEW_PROJECTION_PERSPECTIVE".into(),
        ];
        source_graph_bind_stages(&mut d, 123, 456, true);
        let f = d.fragment.as_ref().unwrap();
        assert_eq!(f.shader.id(), source_shader_handle(456).id());
        assert_eq!(d.vertex.shader.id(), source_shader_handle(123).id());
        assert_eq!(f.shader_defs, d.vertex.shader_defs);
        assert!(f.targets.is_empty());
        // This does not infer perspective depth is a shadow.
        assert!(
            !f.shader_defs
                .iter()
                .any(|v| matches!(v,ShaderDefVal::Bool(n,true) if n=="SOURCE_GRAPH_SHADOW_PASS"))
        );
    }
    #[test]
    fn source_graph_prepass_keeps_existing_outputs_and_forward_does_not_invent_fragment() {
        let mut d = RenderPipelineDescriptor {
            fragment: Some(FragmentState {
                entry_point: Some("fragment".into()),
                shader_defs: vec!["NORMAL_PREPASS".into()],
                targets: vec![Some(ColorTargetState {
                    format: TextureFormat::Rgba16Float,
                    blend: None,
                    write_mask: ColorWrites::ALL,
                })],
                ..Default::default()
            }),
            ..Default::default()
        };
        let old = d.fragment.clone().unwrap();
        source_graph_bind_stages(&mut d, 123, 456, true);
        let f = d.fragment.as_ref().unwrap();
        assert_eq!(f.targets, old.targets);
        assert_eq!(f.shader_defs, old.shader_defs);
        assert_eq!(f.entry_point, old.entry_point);
        let mut forward = RenderPipelineDescriptor::default();
        source_graph_bind_stages(&mut forward, 123, 456, false);
        assert!(forward.fragment.is_none());
    }
}

#[cfg(test)]
mod source_shadow_role_tests {
    use super::*;
    fn shadow_def(d: &RenderPipelineDescriptor) -> bool {
        d.fragment
            .as_ref()
            .unwrap()
            .shader_defs
            .iter()
            .any(|v| matches!(v,ShaderDefVal::Bool(n,true) if n=="SOURCE_GRAPH_SHADOW_PASS"))
    }
    fn depth() -> RenderPipelineDescriptor {
        let mut d = RenderPipelineDescriptor::default();
        d.vertex.shader_defs = vec![
            "PREPASS_PIPELINE".into(),
            "DEPTH_PREPASS".into(),
            "VIEW_PROJECTION_PERSPECTIVE".into(),
        ];
        d
    }
    #[test]
    fn source_graph_shadow_role_separates_same_perspective_depth_without_heuristics() {
        let mut camera = depth();
        let mut shadow = depth();
        source_graph_bind_role(&mut camera, false, true, 123, 456);
        source_graph_bind_role(&mut shadow, true, true, 123, 456);
        assert!(!shadow_def(&camera));
        assert!(shadow_def(&shadow));
        assert_eq!(
            camera.fragment.as_ref().unwrap().targets,
            shadow.fragment.as_ref().unwrap().targets
        );
        source_graph_bind_role(&mut shadow, false, true, 123, 456);
        assert!(!shadow_def(&shadow));
    }
    #[test]
    fn source_graph_shadow_only_forward_fails_closed_instead_of_drawing_camera_material() {
        let mut d = depth();
        source_graph_bind_stages(&mut d, 123, 456, true);
        source_graph_bind_role(&mut d, true, false, 123, 456);
        assert_eq!(d.vertex.shader.id(), source_shader_handle(0).id());
        assert_eq!(
            d.fragment.unwrap().shader.id(),
            source_shader_handle(0).id()
        );
    }
}

#[cfg(test)]
mod ten_texture_binding_tests {
    use super::*;
    fn key() -> SourceGraphKey {
        SourceGraphKey {
            requires_source_state: false,
            source_state_ready: false,
            requires_native_srgb_encoder: false,
            native_srgb_encoder_ready: false,
            requires_deferred_srgb: false,
            deferred_srgb_ready: false,
            requires_preintegrated_brdf: false,
            preintegrated_brdf_ready: false,
            requires_sky_cube: false,
            sky_cube_ready: false,
            prepass_cull: 2,
            shadow_only: false,
            orthographic_shadow_raster: None,
            vertex: 1,
            fragment: 2,
            prepass_vertex: 3,
            prepass_fragment: 4,
            original_alpha_to_mask: false,
            requires_original_vertex_color: false,
            original_uv_requirements: SourceGraphUvRequirements::default(),
            color_write_mask: 15,
            forward_state: SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 4,
                write_depth: true,
            },
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
            normal_decode_modes: [Vec4::splat(-1.0); 2],
        }
    }

    #[test]
    fn required_state_never_becomes_ready_without_an_admitted_buffer() {
        let mut k = key();
        k.requires_source_state = true;
        let mut extension =
            SourceGraphExtension::from_source_bindings(k, source(), 0, vec![]).unwrap();
        let images = Assets::<Image>::default();
        assert!(extension.validate_scene_dependencies(&images).is_err());
        assert!(!SourceGraphKey::from(&extension).source_state_ready);
        let mut buffers = Assets::<bevy::render::storage::ShaderBuffer>::default();
        let handle = buffers.add(bevy::render::storage::ShaderBuffer::new(
            &[0; 32],
            bevy::asset::RenderAssetUsages::default(),
        ));
        let binding = SourceGraphStateBufferBinding::new(handle, &buffers).unwrap();
        extension.set_source_state_buffer(&binding);
        assert!(SourceGraphKey::from(&extension).source_state_ready);
        extension.validate_source_state_buffer(&buffers).unwrap();
        extension.validate_scene_dependencies(&images).unwrap();
        extension.clear_source_state_buffer();
        assert!(!SourceGraphKey::from(&extension).source_state_ready);
        assert!(extension.validate_source_state_buffer(&buffers).is_err());
    }
    fn images(n: usize) -> Vec<Handle<Image>> {
        (0..n)
            .map(|i| {
                Handle::Uuid(
                    bevy::asset::uuid::Uuid::from_u128(1000 + i as u128),
                    std::marker::PhantomData,
                )
            })
            .collect()
    }
    #[test]
    fn source_graph_ten_images_keep_exact_declared_order_and_cloned_extension() {
        let expected = images(10);
        let extension =
            SourceGraphExtension::from_source_bindings(key(), source(), 10, expected.clone())
                .unwrap();
        for value in [extension.clone(), extension] {
            let actual = [
                value.texture_0,
                value.texture_1,
                value.texture_2,
                value.texture_3,
                value.texture_4,
                value.texture_5,
                value.texture_6,
                value.texture_7,
                value.texture_8,
                value.texture_9,
            ];
            assert_eq!(
                actual.to_vec(),
                expected.iter().cloned().map(Some).collect::<Vec<_>>()
            );
        }
    }
    #[test]
    fn native_srgb_required_resource_refuses_absence_corruption_and_handle_changes() {
        let mut k = key();
        k.requires_native_srgb_encoder = true;
        let mut ext = SourceGraphExtension::from_source_bindings(k, source(), 0, vec![]).unwrap();
        let mut assets = Assets::<Image>::default();
        assert!(ext.validate_scene_dependencies(&assets).is_err());
        assert!(!SourceGraphKey::from(&ext).native_srgb_encoder_ready);
        let binding = source_srgb_runtime::NativeSrgbBinding::new(vec![0; 1024], &mut assets);
        ext.native_srgb_thresholds = Some(binding.handle.clone());
        ext.native_srgb_binding = Some(binding.clone());
        assert!(ext.validate_scene_dependencies(&assets).is_err());
        ext.native_srgb_validated = true;
        assert!(ext.validate_scene_dependencies(&assets).is_ok());
        assert!(SourceGraphKey::from(&ext).native_srgb_encoder_ready);
        assets
            .get_mut(&binding.handle)
            .unwrap()
            .data
            .as_mut()
            .unwrap()[8] ^= 1;
        assert!(ext.validate_scene_dependencies(&assets).is_err());
        assets
            .get_mut(&binding.handle)
            .unwrap()
            .data
            .as_mut()
            .unwrap()[8] ^= 1;
        assets
            .get_mut(&binding.handle)
            .unwrap()
            .texture_descriptor
            .format = bevy::render::render_resource::TextureFormat::Rgba8Unorm;
        assert!(ext.validate_scene_dependencies(&assets).is_err());
        assets
            .get_mut(&binding.handle)
            .unwrap()
            .texture_descriptor
            .format = bevy::render::render_resource::TextureFormat::R32Float;
        ext.native_srgb_thresholds = Some(Handle::default());
        assert!(ext.validate_scene_dependencies(&assets).is_err());
        ext.native_srgb_thresholds = Some(binding.handle.clone());
        assets.remove(&binding.handle);
        assert!(ext.validate_scene_dependencies(&assets).is_err());
    }
    #[test]
    fn deferred_srgb_required_resource_refuses_absence_removal_and_corruption() {
        let mut k = key();
        k.requires_deferred_srgb = true;
        let mut extension =
            SourceGraphExtension::from_source_bindings(k, source(), 7, images(7)).unwrap();
        let mut assets = Assets::<Image>::default();
        assert!(extension.validate_scene_dependencies(&assets).is_err());
        extension.install_deferred_srgb_lookup(&mut assets);
        assert!(extension.validate_scene_dependencies(&assets).is_ok());
        assert!(SourceGraphKey::from(&extension).deferred_srgb_ready);
        let handle = extension.deferred_srgb_lookup.clone().unwrap();
        assets
            .get_mut(&handle)
            .unwrap()
            .texture_descriptor
            .dimension = bevy::render::render_resource::TextureDimension::D1;
        assert!(extension.validate_scene_dependencies(&assets).is_err());
        assets
            .get_mut(&handle)
            .unwrap()
            .texture_descriptor
            .dimension = bevy::render::render_resource::TextureDimension::D2;
        assert!(extension.validate_scene_dependencies(&assets).is_ok());
        assets.get_mut(&handle).unwrap().data.as_mut().unwrap()[12] ^= 1;
        assert!(extension.validate_scene_dependencies(&assets).is_err());
        assets.remove(handle.id());
        assert!(extension.validate_scene_dependencies(&assets).is_err());
    }
    #[test]
    fn source_graph_legacy_padding_and_missing_or_excess_images_fail_closed() {
        let legacy =
            SourceGraphExtension::from_source_bindings(key(), source(), 7, images(7)).unwrap();
        assert!(
            legacy.texture_7.is_none() && legacy.texture_8.is_none() && legacy.texture_9.is_none()
        );
        assert!(
            SourceGraphExtension::from_source_bindings(key(), source(), 10, images(9)).is_err()
        );
        assert!(
            SourceGraphExtension::from_source_bindings(key(), source(), 11, images(11)).is_err()
        );
    }
}

#[cfg(test)]
mod source_cull_tests {
    use super::*;
    use bevy::render::render_resource::Face;
    #[test]
    fn source_graph_cull_mapping_preserves_disabled_faces_and_inverted_native_views() {
        assert_eq!(source_graph_cull_face(0, false).unwrap(), None);
        assert_eq!(source_graph_cull_face(0, true).unwrap(), None);
        assert_eq!(source_graph_cull_face(1, false).unwrap(), Some(Face::Front));
        assert_eq!(source_graph_cull_face(1, true).unwrap(), Some(Face::Back));
        assert_eq!(source_graph_cull_face(2, false).unwrap(), Some(Face::Back));
        assert_eq!(source_graph_cull_face(2, true).unwrap(), Some(Face::Front));
        assert!(source_graph_cull_face(3, false).is_err());
        assert!(source_graph_cull_face(255, true).is_err());
    }
}
