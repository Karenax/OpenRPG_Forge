//! Preserve Bevy's composed material, with a complete dependency declaration.
//! `ExtendedMaterial` itself does not visit the fields of its base or extension.
use bevy::{
    asset::{UntypedAssetId, VisitAssetDependencies},
    ecs::system::SystemParamItem,
    material::OpaqueRendererMethod,
    mesh::MeshVertexBufferLayoutRef,
    pbr::{ExtendedMaterial, Material, MaterialExtension, MaterialPipeline, MaterialPipelineKey},
    prelude::*,
    render::{
        render_resource::{
            AsBindGroup, AsBindGroupError, BindGroupLayout, BindGroupLayoutEntry,
            RenderPipelineDescriptor, SpecializedMeshPipelineError, UnpreparedBindGroup,
        },
        renderer::RenderDevice,
    },
    shader::ShaderRef,
};

pub trait Base: Material {
    fn visit_surface_dependencies(&self, visit: &mut impl FnMut(UntypedAssetId));
}
impl Base for StandardMaterial {
    fn visit_surface_dependencies(&self, visit: &mut impl FnMut(UntypedAssetId)) {
        self.visit_dependencies(visit);
    }
}
impl Base for crate::VisualValleyTerrainMaterial {
    fn visit_surface_dependencies(&self, visit: &mut impl FnMut(UntypedAssetId)) {
        self.base.visit_dependencies(visit);
        visit(self.extension.detail_texture.id().untyped());
    }
}
impl Base for crate::VisualValleyVegetationMaterial {
    fn visit_surface_dependencies(&self, visit: &mut impl FnMut(UntypedAssetId)) {
        self.base.visit_dependencies(visit);
        self.extension.visit_dependencies(visit);
    }
}
impl Base for crate::source_terrain::TerrainMaterial {
    fn visit_surface_dependencies(&self, visit: &mut impl FnMut(UntypedAssetId)) {
        self.visit_dependencies(visit);
    }
}

#[derive(Clone, Debug, TypePath)]
pub struct ComposedMaterial<B: Base, E: MaterialExtension> {
    pub base: B,
    pub extension: E,
}
impl<B: Base, E: MaterialExtension> Asset for ComposedMaterial<B, E> {}
impl<B: Base, E: MaterialExtension> VisitAssetDependencies for ComposedMaterial<B, E> {
    fn visit_dependencies(&self, visit: &mut impl FnMut(UntypedAssetId)) {
        self.base.visit_surface_dependencies(visit);
        self.extension.visit_dependencies(visit);
    }
}
impl<B: Base, E: MaterialExtension> ComposedMaterial<B, E> {
    fn composed(&self) -> ExtendedMaterial<B, E> {
        ExtendedMaterial {
            base: self.base.clone(),
            extension: self.extension.clone(),
        }
    }
}
impl<B: Base, E: MaterialExtension> AsBindGroup for ComposedMaterial<B, E> {
    type Data = <ExtendedMaterial<B, E> as AsBindGroup>::Data;
    type Param = <ExtendedMaterial<B, E> as AsBindGroup>::Param;
    fn label() -> &'static str {
        ExtendedMaterial::<B, E>::label()
    }
    fn bind_group_data(&self) -> Self::Data {
        self.composed().bind_group_data()
    }
    fn unprepared_bind_group(
        &self,
        layout: &BindGroupLayout,
        device: &RenderDevice,
        param: &mut SystemParamItem<'_, '_, Self::Param>,
        force_no_bindless: bool,
    ) -> Result<UnpreparedBindGroup, AsBindGroupError> {
        self.composed()
            .unprepared_bind_group(layout, device, param, force_no_bindless)
    }
    fn bind_group_layout_entries(
        device: &RenderDevice,
        force_no_bindless: bool,
    ) -> Vec<BindGroupLayoutEntry> {
        ExtendedMaterial::<B, E>::bind_group_layout_entries(device, force_no_bindless)
    }
}
impl<B: Base, E: MaterialExtension> Material for ComposedMaterial<B, E> {
    fn vertex_shader() -> ShaderRef {
        ExtendedMaterial::<B, E>::vertex_shader()
    }
    fn fragment_shader() -> ShaderRef {
        ExtendedMaterial::<B, E>::fragment_shader()
    }
    fn alpha_mode(&self) -> AlphaMode {
        self.composed().alpha_mode()
    }
    fn opaque_render_method(&self) -> OpaqueRendererMethod {
        self.composed().opaque_render_method()
    }
    fn depth_bias(&self) -> f32 {
        self.composed().depth_bias()
    }
    fn reads_view_transmission_texture(&self) -> bool {
        self.composed().reads_view_transmission_texture()
    }
    fn enable_prepass() -> bool {
        ExtendedMaterial::<B, E>::enable_prepass()
    }
    fn enable_shadows() -> bool {
        ExtendedMaterial::<B, E>::enable_shadows()
    }
    fn prepass_vertex_shader() -> ShaderRef {
        ExtendedMaterial::<B, E>::prepass_vertex_shader()
    }
    fn prepass_fragment_shader() -> ShaderRef {
        ExtendedMaterial::<B, E>::prepass_fragment_shader()
    }
    fn deferred_vertex_shader() -> ShaderRef {
        ExtendedMaterial::<B, E>::deferred_vertex_shader()
    }
    fn deferred_fragment_shader() -> ShaderRef {
        ExtendedMaterial::<B, E>::deferred_fragment_shader()
    }
    fn specialize(
        pipeline: &MaterialPipeline,
        descriptor: &mut RenderPipelineDescriptor,
        layout: &MeshVertexBufferLayoutRef,
        key: MaterialPipelineKey<Self>,
    ) -> Result<(), SpecializedMeshPipelineError> {
        ExtendedMaterial::<B, E>::specialize(
            pipeline,
            descriptor,
            layout,
            MaterialPipelineKey {
                mesh_key: key.mesh_key,
                bind_group_data: key.bind_group_data,
            },
        )
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::source_terrain::lod::{LodExtension, LodSettings};

    #[test]
    fn dependencies_include_the_actual_base_and_extension_instead_of_an_empty_composition() {
        let mut images = Assets::<Image>::default();
        let handles: Vec<_> = (0..4).map(|_| images.add(Image::default())).collect();
        let material = ComposedMaterial {
            base: StandardMaterial {
                base_color_texture: Some(handles[0].clone()),
                normal_map_texture: Some(handles[1].clone()),
                ..default()
            },
            extension: LodExtension {
                height: handles[2].clone(),
                corrections: handles[3].clone(),
                settings: LodSettings {
                    morph: [Vec4::ZERO; 9],
                },
            },
        };
        let mut found = std::collections::HashSet::new();
        material.visit_dependencies(&mut |id| {
            found.insert(id);
        });
        assert_eq!(
            found,
            handles.iter().map(|handle| handle.id().untyped()).collect()
        );
        let bevy = ExtendedMaterial {
            base: material.base,
            extension: material.extension,
        };
        let mut missing = Vec::new();
        bevy.visit_dependencies(&mut |id| missing.push(id));
        assert!(
            missing.is_empty(),
            "The regression must exercise the SDK's empty composition visitor"
        );
    }
}
