//! Offscreen ID geometry. The owner supplies matching depth occluders on LAYER,
//! pipeline completion, readback and revision/camera freshness checks.
use super::PickingMaterial;
use crate::{
    tidewater_plants::scene::resident::picking::Snapshot, tidewater_sky::preview::PreviewCamera,
};
use bevy::{
    camera::{
        RenderTarget,
        visibility::{NoFrustumCulling, RenderLayers},
    },
    core_pipeline::tonemapping::{DebandDither, Tonemapping},
    light::NotShadowCaster,
    mesh::MeshTag,
    prelude::*,
    render::render_resource::{TextureFormat, TextureUsages},
};

pub const LAYER: usize = 30;
pub struct View {
    pub width: u32,
    pub height: u32,
    pub transform: Transform,
    pub projection: Projection,
}
pub struct Pass {
    pub target: Handle<Image>,
    pub snapshot: Snapshot,
    pub camera: Entity,
    entities: Vec<Entity>,
}

pub fn install(app: &mut App) {
    if !app.is_plugin_added::<MaterialPlugin<PickingMaterial>>() {
        app.add_plugins(MaterialPlugin::<PickingMaterial>::default());
    }
}

impl Pass {
    pub fn spawn(world: &mut World, snapshot: Snapshot, view: View) -> Result<Self, String> {
        if view.width == 0
            || view.height == 0
            || view.width > 4096
            || view.height > 4096
            || !view.transform.translation.is_finite()
            || !view.transform.rotation.is_finite()
            || !world.contains_resource::<Assets<Image>>()
            || !world.contains_resource::<Assets<PickingMaterial>>()
        {
            return Err("Plant picking render inputs unavailable or invalid".into());
        }
        let mut image =
            Image::new_target_texture(view.width, view.height, TextureFormat::Rgba8Unorm, None);
        image.texture_descriptor.usage |= TextureUsages::COPY_SRC;
        let target = world.resource_mut::<Assets<Image>>().add(image);
        let mut entities = Vec::new();
        for batch in &snapshot.batches {
            let material = world
                .resource_mut::<Assets<PickingMaterial>>()
                .add(batch.material.clone());
            for &tag in &batch.tags {
                entities.push(
                    world
                        .spawn((
                            Mesh3d(batch.mesh.clone()),
                            MeshMaterial3d(material.clone()),
                            MeshTag(tag),
                            Transform::IDENTITY,
                            Visibility::Visible,
                            NoFrustumCulling,
                            NotShadowCaster,
                            RenderLayers::layer(LAYER),
                        ))
                        .id(),
                );
            }
        }
        let camera = world
            .spawn((
                PreviewCamera,
                Camera3d::default(),
                Camera {
                    order: -21,
                    clear_color: ClearColorConfig::Custom(Color::BLACK),
                    ..default()
                },
                RenderTarget::Image(target.clone().into()),
                Msaa::Off,
                Tonemapping::None,
                DebandDither::Disabled,
                view.transform,
                view.projection,
                RenderLayers::layer(LAYER),
            ))
            .id();
        Ok(Self {
            target,
            snapshot,
            camera,
            entities,
        })
    }

    /// Caller retires the readback entity separately before releasing this pass.
    pub fn retire(self, world: &mut World) {
        world.despawn(self.camera);
        for entity in self.entities {
            world.despawn(entity);
        }
    }
}
