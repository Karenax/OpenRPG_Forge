//! Offscreen ID geometry. The owner supplies matching depth occluders on LAYER,
//! pipeline completion, readback and revision/camera freshness checks.
use super::PickingMaterial;
use crate::scene_camera::OffscreenSceneCamera;
use crate::source_vegetation::scene::resident::picking::Snapshot;
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
#[derive(Clone)]
pub struct View {
    pub width: u32,
    pub height: u32,
    pub transform: Transform,
    pub projection: Projection,
}
pub struct Pass<M: Material + Clone = PickingMaterial> {
    pub target: Handle<Image>,
    pub snapshot: Snapshot<M>,
    pub camera: Entity,
    layer: usize,
    entities: Vec<Entity>,
}

pub fn install(app: &mut App) {
    if !app.is_plugin_added::<MaterialPlugin<PickingMaterial>>() {
        app.add_plugins(MaterialPlugin::<PickingMaterial>::default());
    }
}

impl<M: Material + Clone> Pass<M> {
    pub fn spawn(world: &mut World, snapshot: Snapshot<M>, view: View) -> Result<Self, String> {
        Self::spawn_on_layer(world, snapshot, view, LAYER)
    }

    /// Separate diagnostic passes can render the same source view without
    /// sharing plant copies or occluders.
    pub fn spawn_on_layer(
        world: &mut World,
        snapshot: Snapshot<M>,
        view: View,
        layer: usize,
    ) -> Result<Self, String> {
        if view.width == 0
            || view.height == 0
            || view.width > 4096
            || view.height > 4096
            || !view.transform.translation.is_finite()
            || !view.transform.rotation.is_finite()
            || !world.contains_resource::<Assets<Image>>()
            || !world.contains_resource::<Assets<M>>()
            || layer >= 32
        {
            return Err("Plant picking render inputs unavailable or invalid".into());
        }
        let models = super::governed::stage(world, &snapshot.models, layer)?;
        let mut image =
            Image::new_target_texture(view.width, view.height, TextureFormat::Rgba8Unorm, None);
        image.texture_descriptor.usage |= TextureUsages::COPY_SRC;
        let target = world.resource_mut::<Assets<Image>>().add(image);
        let mut entities = models;
        for batch in &snapshot.batches {
            let material = world
                .resource_mut::<Assets<M>>()
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
                            RenderLayers::layer(layer),
                        ))
                        .id(),
                );
            }
        }
        let camera = world
            .spawn((
                OffscreenSceneCamera,
                Camera3d {
                    depth_texture_usages: (TextureUsages::RENDER_ATTACHMENT
                        | TextureUsages::COPY_SRC)
                        .into(),
                    ..default()
                },
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
                RenderLayers::layer(layer),
            ))
            .id();
        Ok(Self {
            target,
            snapshot,
            camera,
            layer,
            entities,
        })
    }

    /// Add the terrain and opaque village meshes from the same already-projected
    /// world. Prepare before mutating this pass so missing scene resources cannot
    /// leave a partly masked ID image.
    pub fn add_occluders<T: Material, V: Material>(
        &mut self,
        world: &mut World,
        prepared: super::occluders::Prepared<T, V>,
    ) -> Result<(), String> {
        if !world.contains_resource::<Assets<super::occluders::TerrainOccluder<T>>>()
            || !world.contains_resource::<Assets<super::occluders::VillageOccluder<V>>>()
            || !world.contains_resource::<Assets<super::occluders::StandardOccluder>>()
            || !world.contains_resource::<Assets<super::occluders::ModelOccluder>>()
        {
            return Err("Picking occluder plugins are absent".into());
        }
        self.entities.extend(prepared.stage(world, self.layer));
        Ok(())
    }

    /// Caller retires the readback entity separately before releasing this pass.
    pub fn retire(self, world: &mut World) {
        world.despawn(self.camera);
        for entity in self.entities {
            world.despawn(entity);
        }
    }
}
