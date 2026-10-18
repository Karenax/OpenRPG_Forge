//! Renderer-owned requirements for effects that sample the main 3D depth texture.
//! This augments the extracted camera, preserving authored camera settings.
use bevy::{
    core_pipeline::core_3d::prepare_core_3d_depth_textures,
    prelude::*,
    render::{
        Render, RenderApp, RenderSystems,
        extract_component::{ExtractComponent, ExtractComponentPlugin},
        render_resource::TextureUsages,
    },
};

#[derive(Component, Clone, Copy, Default, ExtractComponent)]
pub(crate) struct SampleableDepth;
struct SampleableDepthPlugin;
impl Plugin for SampleableDepthPlugin {
    fn build(&self, app: &mut App) {
        app.add_plugins(ExtractComponentPlugin::<SampleableDepth>::default());
        if let Some(render) = app.get_sub_app_mut(RenderApp) {
            render.add_systems(
                Render,
                require_texture_binding
                    .in_set(RenderSystems::PrepareResources)
                    .before(prepare_core_3d_depth_textures),
            );
        }
    }
}
pub(crate) fn install(app: &mut App) {
    if !app.is_plugin_added::<SampleableDepthPlugin>() {
        app.add_plugins(SampleableDepthPlugin);
    }
}
fn require_texture_binding(mut cameras: Query<&mut Camera3d, With<SampleableDepth>>) {
    for mut camera in &mut cameras {
        let required = TextureUsages::TEXTURE_BINDING.bits();
        if camera.depth_texture_usages.0 & required == 0 {
            camera.depth_texture_usages.0 |= required;
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn effect_depth_is_sampleable_before_preparation_without_changing_other_camera_usage() {
        let mut app = App::new();
        app.add_systems(Update, require_texture_binding);
        let original = TextureUsages::RENDER_ATTACHMENT | TextureUsages::COPY_SRC;
        let effect = app
            .world_mut()
            .spawn((
                Camera3d {
                    depth_texture_usages: original.into(),
                    ..default()
                },
                SampleableDepth,
            ))
            .id();
        let ordinary = app.world_mut().spawn(Camera3d::default()).id();
        app.update();
        let expected = (original | TextureUsages::TEXTURE_BINDING).bits();
        assert_eq!(
            app.world()
                .get::<Camera3d>(effect)
                .unwrap()
                .depth_texture_usages
                .0,
            expected
        );
        assert_eq!(
            app.world()
                .get::<Camera3d>(ordinary)
                .unwrap()
                .depth_texture_usages
                .0,
            TextureUsages::RENDER_ATTACHMENT.bits()
        );
        app.update();
        assert_eq!(
            app.world()
                .get::<Camera3d>(effect)
                .unwrap()
                .depth_texture_usages
                .0,
            expected
        );
    }
}
