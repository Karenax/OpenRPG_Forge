//! Forward-only source material. No fabricated prepass, shadows or tangents.
//! Uses the existing source uniform/image ABI; qualification stays with intake.
use crate::source_graph::{
    SOURCE_GRAPH_COLOR, SOURCE_GRAPH_UV0, SourceGraphExtension, SourceGraphForwardPassState,
    SourceGraphForwardState, SourceGraphKey, SourceGraphScalarUniforms, SourceGraphUniforms,
    SourceGraphUvRequirements, source_graph_required_uv_bindings,
};
use bevy::{
    asset::{Asset, UntypedAssetId, VisitAssetDependencies},
    ecs::system::SystemParamItem,
    mesh::{MeshVertexBufferLayoutRef, VertexBufferLayout},
    pbr::{
        ExtendedMaterial, MaterialExtension, MaterialExtensionKey, MaterialExtensionPipeline,
        MaterialPlugin, MeshPipelineKey,
    },
    prelude::*,
    render::{
        render_resource::{
            AsBindGroup, AsBindGroupError, BindGroupLayout, BindGroupLayoutEntry,
            RenderPipelineDescriptor, SpecializedMeshPipelineError, UnpreparedBindGroup,
        },
        renderer::RenderDevice,
    },
    shader::{ShaderDefVal, ShaderRef},
};

#[derive(Copy, Clone, Debug, PartialEq, Eq, Hash, Reflect)]
pub struct SourceUnlitForwardKey {
    pub vertex: u128,
    pub fragment: u128,
    pub forward: SourceGraphForwardState,
    pub color_write_mask: u32,
    pub requires_original_uv0: bool,
    pub requires_original_color: bool,
    pub additional_uvs: SourceGraphUvRequirements,
}

#[derive(Clone, Debug, Reflect)]
pub struct SourceUnlitForwardExtension {
    key: SourceUnlitForwardKey,
    bindings: SourceGraphExtension,
}
impl Asset for SourceUnlitForwardExtension {}
impl VisitAssetDependencies for SourceUnlitForwardExtension {
    fn visit_dependencies(&self, visit: &mut impl FnMut(UntypedAssetId)) {
        for handle in [
            &self.bindings.texture_0,
            &self.bindings.texture_1,
            &self.bindings.texture_2,
            &self.bindings.texture_3,
            &self.bindings.texture_4,
            &self.bindings.texture_5,
            &self.bindings.texture_6,
        ]
        .into_iter()
        .flatten()
        {
            visit(handle.id().untyped());
        }
    }
}
impl SourceUnlitForwardExtension {
    pub fn from_source_bindings(
        key: SourceUnlitForwardKey,
        source: SourceGraphUniforms,
        required_texture_count: usize,
        images: Vec<Handle<Image>>,
        scalars: SourceGraphScalarUniforms,
    ) -> Result<Self, &'static str> {
        SourceGraphForwardPassState {
            forward: key.forward,
            color_write_mask: key.color_write_mask,
        }
        .validate()?;
        if key.vertex == 0 || key.fragment == 0 {
            return Err("Forward-only source shader identity is missing");
        }
        if required_texture_count > 7 || required_texture_count != images.len() {
            return Err("Forward-only image count differs from pinned declarations");
        }
        let mut textures = images.into_iter().map(Some).chain(std::iter::repeat(None));
        let bindings = SourceGraphExtension {
            program_key: SourceGraphKey {
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
                prepass_cull: 0,
                shadow_only: false,
                orthographic_shadow_raster: None,
                vertex: key.vertex,
                fragment: key.fragment,
                prepass_vertex: 0,
                prepass_fragment: 0,
                original_alpha_to_mask: false,
                requires_original_vertex_color: key.requires_original_color,
                original_uv_requirements: key.additional_uvs,
                forward_state: key.forward,
                color_write_mask: key.color_write_mask,
            },
            original_alpha_to_mask: false,
            source,
            source_state: super::source_state_buffer::unused_source_state_handle(),
            source_state_admitted: false,
            scalars,
            diffuse_probe: crate::source_graph::SourceGraphDiffuseProbeUniforms::default(),
            native_srgb_thresholds: None,
            native_srgb_binding: None,
            native_srgb_validated: false,
            deferred_srgb_lookup: None,
            preintegrated_brdf: None,
            sky_cube: None,
            sky_cube_binding: None,
            texture_0: textures.next().unwrap(),
            texture_1: textures.next().unwrap(),
            texture_2: textures.next().unwrap(),
            texture_3: textures.next().unwrap(),
            texture_4: textures.next().unwrap(),
            texture_5: textures.next().unwrap(),
            texture_6: textures.next().unwrap(),
            // This forward-only profile still admits at most seven textures.
            // Initialize the common ABI's extra slots without inventing inputs.
            texture_7: None,
            texture_8: None,
            texture_9: None,
        };
        Ok(Self { key, bindings })
    }
}
impl AsBindGroup for SourceUnlitForwardExtension {
    type Data = SourceUnlitForwardKey;
    type Param = <SourceGraphExtension as AsBindGroup>::Param;
    fn label() -> &'static str {
        "source_unlit_forward"
    }
    fn bind_group_data(&self) -> Self::Data {
        self.key
    }
    fn unprepared_bind_group(
        &self,
        layout: &BindGroupLayout,
        device: &RenderDevice,
        param: &mut SystemParamItem<'_, '_, Self::Param>,
        force_no_bindless: bool,
    ) -> Result<UnpreparedBindGroup, AsBindGroupError> {
        self.bindings
            .unprepared_bind_group(layout, device, param, force_no_bindless)
    }
    fn bind_group_layout_entries(
        device: &RenderDevice,
        force_no_bindless: bool,
    ) -> Vec<BindGroupLayoutEntry> {
        SourceGraphExtension::bind_group_layout_entries(device, force_no_bindless)
    }
}
fn shader(id: u128) -> Handle<Shader> {
    Handle::Uuid(
        bevy::asset::uuid::Uuid::from_u128(id),
        std::marker::PhantomData,
    )
}
fn source_layout(
    layout: &MeshVertexBufferLayoutRef,
    key: SourceUnlitForwardKey,
) -> Result<VertexBufferLayout, SpecializedMeshPipelineError> {
    let mut attributes = vec![Mesh::ATTRIBUTE_POSITION.at_shader_location(0)];
    if key.requires_original_uv0 {
        attributes.push(SOURCE_GRAPH_UV0.at_shader_location(10));
    }
    if key.requires_original_color {
        attributes.push(SOURCE_GRAPH_COLOR.at_shader_location(11));
    }
    for (attribute, location) in source_graph_required_uv_bindings(key.additional_uvs.channels())
        .expect("Admitted original UV channels")
    {
        attributes.push(attribute.at_shader_location(location));
    }
    Ok(layout.0.get_layout(&attributes)?)
}
fn apply_source_pipeline(
    descriptor: &mut RenderPipelineDescriptor,
    key: SourceUnlitForwardKey,
    inverted_view: bool,
) -> Result<(), &'static str> {
    if descriptor
        .vertex
        .shader_defs
        .iter()
        .any(|value| matches!(value, ShaderDefVal::Bool(name, true) if name == "PREPASS_PIPELINE"))
    {
        return Err("Forward-only source cannot draw in a prepass");
    }
    if key.vertex == 0 || key.fragment == 0 {
        return Err("Missing source stages");
    }
    SourceGraphForwardPassState {
        forward: key.forward,
        color_write_mask: key.color_write_mask,
    }
    .apply(descriptor)?;
    descriptor.vertex.shader = shader(key.vertex);
    descriptor
        .fragment
        .as_mut()
        .ok_or("Missing forward fragment")?
        .shader = shader(key.fragment);
    // Preserve native reflection/mirror winding without changing captured data.
    if inverted_view {
        descriptor.primitive.cull_mode = descriptor.primitive.cull_mode.map(|face| match face {
            bevy::render::render_resource::Face::Front => bevy::render::render_resource::Face::Back,
            bevy::render::render_resource::Face::Back => bevy::render::render_resource::Face::Front,
        });
    }
    Ok(())
}
impl MaterialExtension for SourceUnlitForwardExtension {
    fn enable_prepass() -> bool {
        false
    }
    fn enable_shadows() -> bool {
        false
    }
    fn vertex_shader() -> ShaderRef {
        ShaderRef::Handle(shader(0))
    }
    fn fragment_shader() -> ShaderRef {
        ShaderRef::Handle(shader(0))
    }
    fn specialize(
        _: &MaterialExtensionPipeline,
        descriptor: &mut RenderPipelineDescriptor,
        layout: &MeshVertexBufferLayoutRef,
        key: MaterialExtensionKey<Self>,
    ) -> Result<(), SpecializedMeshPipelineError> {
        descriptor.vertex.buffers = vec![source_layout(layout, key.bind_group_data)?];
        if apply_source_pipeline(
            descriptor,
            key.bind_group_data,
            key.mesh_key.contains(MeshPipelineKey::INVERT_CULLING),
        )
        .is_err()
        {
            // No ordinary PBR or invented non-forward source fallback can draw.
            descriptor.vertex.shader = shader(0);
            if let Some(fragment) = &mut descriptor.fragment {
                fragment.shader = shader(0);
            }
        }
        Ok(())
    }
}
pub type SourceUnlitForwardMaterial =
    ExtendedMaterial<StandardMaterial, SourceUnlitForwardExtension>;
pub struct SourceUnlitForwardPlugin;
impl Plugin for SourceUnlitForwardPlugin {
    fn build(&self, app: &mut App) {
        super::source_state_buffer::install_unused_state_asset(app);
        app.add_plugins(MaterialPlugin::<SourceUnlitForwardMaterial>::default());
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{
        asset::RenderAssetUsages,
        mesh::MeshVertexBufferLayouts,
        pbr::Material,
        render::render_resource::{
            ColorTargetState, ColorWrites, CompareFunction, DepthStencilState, FragmentState,
            PrimitiveTopology, TextureFormat,
        },
    };
    fn key() -> SourceUnlitForwardKey {
        SourceUnlitForwardKey {
            vertex: 123,
            fragment: 456,
            forward: SourceGraphForwardState {
                color_src: 5,
                color_dst: 10,
                alpha_src: 5,
                alpha_dst: 10,
                cull: 0,
                compare: 4,
                write_depth: false,
            },
            color_write_mask: 7,
            requires_original_uv0: true,
            requires_original_color: true,
            additional_uvs: SourceGraphUvRequirements::default(),
        }
    }
    #[test]
    fn source_unlit_forward_disables_native_non_forward_material_routes() {
        assert!(!<SourceUnlitForwardMaterial as Material>::enable_prepass());
        assert!(!<SourceUnlitForwardMaterial as Material>::enable_shadows());
        assert!(<crate::source_graph::SourceGraphMaterial as Material>::enable_prepass());
        for reference in [
            <SourceUnlitForwardMaterial as Material>::vertex_shader(),
            <SourceUnlitForwardMaterial as Material>::fragment_shader(),
        ] {
            assert!(
                matches!(reference, ShaderRef::Handle(handle) if handle.id() == shader(0).id())
            );
        }
    }
    #[test]
    fn source_unlit_forward_requires_raw_uv_color_without_fabricating_tangents() {
        let mut mesh = Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::default(),
        );
        mesh.insert_attribute(Mesh::ATTRIBUTE_POSITION, vec![[0.0, 0.0, 0.0]]);
        mesh.insert_attribute(Mesh::ATTRIBUTE_UV_0, vec![[0.2, 0.3]]);
        mesh.insert_attribute(Mesh::ATTRIBUTE_COLOR, vec![[1.0, 1.0, 1.0, 1.0]]);
        let mut layouts = MeshVertexBufferLayouts::default();
        assert!(source_layout(&mesh.get_mesh_vertex_buffer_layout(&mut layouts), key()).is_err());
        mesh.insert_attribute(SOURCE_GRAPH_UV0, vec![[0.2, 0.3, 0.7, 0.9]]);
        assert!(source_layout(&mesh.get_mesh_vertex_buffer_layout(&mut layouts), key()).is_err());
        mesh.insert_attribute(SOURCE_GRAPH_COLOR, vec![[0.1, 0.2, 0.3, 0.4]]);
        let bound =
            source_layout(&mesh.get_mesh_vertex_buffer_layout(&mut layouts), key()).unwrap();
        assert_eq!(
            bound
                .attributes
                .iter()
                .map(|a| a.shader_location)
                .collect::<Vec<_>>(),
            [0, 10, 11]
        );
        assert!(mesh.attribute(Mesh::ATTRIBUTE_TANGENT).is_none());
        assert!(
            mesh.attribute(crate::source_graph::SOURCE_GRAPH_TANGENT)
                .is_none()
        );
    }
    fn descriptor() -> RenderPipelineDescriptor {
        RenderPipelineDescriptor {
            fragment: Some(FragmentState {
                targets: vec![Some(ColorTargetState {
                    format: TextureFormat::Rgba16Float,
                    blend: None,
                    write_mask: ColorWrites::ALL,
                })],
                ..Default::default()
            }),
            depth_stencil: Some(DepthStencilState {
                format: TextureFormat::Depth32Float,
                depth_write_enabled: Some(true),
                depth_compare: Some(CompareFunction::Less),
                stencil: bevy::render::render_resource::StencilState::default(),
                bias: bevy::render::render_resource::DepthBiasState::default(),
            }),
            ..Default::default()
        }
    }
    #[test]
    fn source_unlit_forward_specializes_original_rgb_blend_and_rejects_prepass() {
        let mut d = descriptor();
        apply_source_pipeline(&mut d, key(), false).unwrap();
        assert_eq!(d.vertex.shader.id(), shader(123).id());
        assert_eq!(d.fragment.as_ref().unwrap().shader.id(), shader(456).id());
        assert_eq!(
            d.fragment.as_ref().unwrap().targets[0]
                .as_ref()
                .unwrap()
                .write_mask,
            ColorWrites::RED | ColorWrites::GREEN | ColorWrites::BLUE
        );
        assert_eq!(
            d.depth_stencil.as_ref().unwrap().depth_write_enabled,
            Some(false)
        );
        assert_eq!(
            d.depth_stencil.as_ref().unwrap().depth_compare,
            Some(CompareFunction::GreaterEqual)
        );
        let old_vertex = d.vertex.shader.id();
        d.vertex.shader_defs.push("PREPASS_PIPELINE".into());
        assert!(apply_source_pipeline(&mut d, key(), false).is_err());
        assert_eq!(d.vertex.shader.id(), old_vertex);
        let mut all = key();
        all.color_write_mask = 15;
        assert_ne!(key(), all);
        let mut invalid = key();
        invalid.color_write_mask = 16;
        assert!(apply_source_pipeline(&mut descriptor(), invalid, false).is_err());
    }
    #[test]
    fn source_unlit_forward_preserves_mirrored_view_culling_and_secondary_target() {
        let mut d = descriptor();
        let secondary = d.fragment.as_ref().unwrap().targets[0].clone();
        d.fragment.as_mut().unwrap().targets.push(secondary.clone());
        let mut k = key();
        k.forward.cull = 2;
        apply_source_pipeline(&mut d, k, true).unwrap();
        assert_eq!(
            d.primitive.cull_mode,
            Some(bevy::render::render_resource::Face::Front)
        );
        assert_eq!(d.fragment.as_ref().unwrap().targets[1], secondary);
    }
}
