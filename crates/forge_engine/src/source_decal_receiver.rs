//! Opt-in forward PBR receiver for a camera's source mesh DBuffer.
//! The caller owns both the receiver selection and the matching view images.
use crate::material_composition::ComposedMaterial;
use bevy::{
    asset::{load_internal_asset, uuid_handle},
    material::OpaqueRendererMethod,
    pbr::{MaterialExtension, MaterialPlugin},
    prelude::*,
    render::render_resource::AsBindGroup,
    shader::{Shader, ShaderRef},
};

const RECEIVER: Handle<Shader> = uuid_handle!("fd40e351-3de4-4811-9872-801f8158ff83");

pub type SourceDecalPbrMaterial = ComposedMaterial<StandardMaterial, SourceDecalReceiver>;

#[derive(Asset, AsBindGroup, TypePath, Clone, Debug)]
pub struct SourceDecalReceiver {
    #[dependency]
    #[texture(100)]
    color: Handle<Image>,
    #[dependency]
    #[texture(101)]
    normal: Handle<Image>,
}

impl SourceDecalReceiver {
    /// The images must be populated by the same view before its forward pass.
    /// Alpha-dependent and unlit routes require their own receiver contracts.
    pub fn material(
        base: StandardMaterial,
        color: Handle<Image>,
        normal: Handle<Image>,
    ) -> Result<SourceDecalPbrMaterial, &'static str> {
        if base.alpha_mode != AlphaMode::Opaque
            || base.unlit
            || base.specular_transmission != 0.0
            || base.diffuse_transmission != 0.0
            || base.opaque_render_method != OpaqueRendererMethod::Forward
            || color.id() == normal.id()
        {
            return Err("Source decals require an explicit opaque forward PBR receiver");
        }
        Ok(ComposedMaterial {
            base,
            extension: Self { color, normal },
        })
    }
}

impl MaterialExtension for SourceDecalReceiver {
    fn fragment_shader() -> ShaderRef {
        RECEIVER.into()
    }
}

pub struct SourceDecalReceiverPlugin;

impl Plugin for SourceDecalReceiverPlugin {
    fn build(&self, app: &mut App) {
        bevy::shader::load_shader_library!(app, "source_decal_composition.wgsl");
        load_internal_asset!(
            app,
            RECEIVER,
            "source_decal_receiver.wgsl",
            Shader::from_wgsl
        );
        app.add_plugins(MaterialPlugin::<SourceDecalPbrMaterial>::default());
    }
}
