//! Owned renderer projections of ordinary surfaces, retaining the original assets.
use super::{ModelMaterial, Receiver, TerrainMaterial, VegetationMaterial};
use crate::VisualValleyVegetationMaterial;
use bevy::{
    asset::AssetId, ecs::message::MessageCursor, prelude::*, render::storage::ShaderBuffer,
};
use std::collections::{HashMap, HashSet};

/// Original renderer asset, kept live for gameplay/authoring changes. This is
/// neither a serialized document nor a second authored material definition.
#[derive(Component, Clone)]
pub enum Origin {
    Model(Handle<StandardMaterial>),
    Terrain(Handle<crate::VisualValleyTerrainMaterial>),
    Vegetation(Handle<VisualValleyVegetationMaterial>),
}
impl Origin {
    #[must_use]
    pub const fn standard(&self) -> Option<&Handle<StandardMaterial>> {
        match self {
            Self::Model(handle) => Some(handle),
            Self::Terrain(_) | Self::Vegetation(_) => None,
        }
    }
}

#[derive(Resource, Default, Clone, Debug, PartialEq, Eq, serde::Serialize, serde::Deserialize)]
pub struct Status {
    pub models: usize,
    pub terrain: usize,
    pub model_materials: usize,
    pub terrain_materials: usize,
    pub pending: usize,
}

#[derive(Resource, Default)]
struct State {
    models: HashMap<AssetId<StandardMaterial>, Handle<ModelMaterial>>,
    terrains: HashMap<AssetId<crate::VisualValleyTerrainMaterial>, Handle<TerrainMaterial>>,
    vegetation: HashMap<AssetId<VisualValleyVegetationMaterial>, Handle<VegetationMaterial>>,
    model_events: MessageCursor<AssetEvent<StandardMaterial>>,
    terrain_events: MessageCursor<AssetEvent<crate::VisualValleyTerrainMaterial>>,
    vegetation_events: MessageCursor<AssetEvent<VisualValleyVegetationMaterial>>,
    context: Option<Vec<String>>,
    entities: HashSet<Entity>,
}

pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<State>() {
        return Err("Source surface host already installed".into());
    }
    crate::visual_valley::register_terrain_material(app);
    if !app.is_plugin_added::<MaterialPlugin<ModelMaterial>>() {
        app.add_plugins(MaterialPlugin::<ModelMaterial>::default());
    }
    if !app.is_plugin_added::<MaterialPlugin<TerrainMaterial>>() {
        app.add_plugins(MaterialPlugin::<TerrainMaterial>::default());
    }
    if !app.is_plugin_added::<MaterialPlugin<VegetationMaterial>>() {
        app.add_plugins(MaterialPlugin::<VegetationMaterial>::default());
    }
    app.init_resource::<State>().init_resource::<Status>();
    Ok(())
}

/// An installed source host must prepare its actual lighting before models can
/// acquire a render token. A scene without this host keeps ordinary Bevy PBR.
#[must_use]
pub fn vegetation_lighting_installed(world: &World) -> bool {
    world.contains_resource::<State>()
}

/// Prepare every LOD recipe, including hidden representatives. The owning
/// vegetation projection requests the inventory for these actual materials.
pub fn vegetation_materials(
    world: &mut World,
    originals: &[Handle<VisualValleyVegetationMaterial>],
) -> Result<Option<Vec<Handle<VegetationMaterial>>>, String> {
    let Some(snapshot) = super::live::snapshot(world) else {
        return Ok(None);
    };
    let mut receiver = Receiver::from_scene(world, snapshot.handle, false)?;
    receiver.vegetation = true;
    world.resource_scope(|world, mut state: Mut<State>| {
        let mut handles = Vec::with_capacity(originals.len());
        for original in originals {
            if let std::collections::hash_map::Entry::Vacant(entry) =
                state.vegetation.entry(original.id())
            {
                let material = world
                    .resource::<Assets<VisualValleyVegetationMaterial>>()
                    .get(original)
                    .ok_or("Original governed vegetation material absent")?
                    .clone();
                super::validate_standard(&material.base)?;
                entry.insert(world.resource_mut::<Assets<VegetationMaterial>>().add(
                    VegetationMaterial {
                        base: material,
                        extension: receiver.clone(),
                    },
                ));
            }
            handles.push(state.vegetation[&original.id()].clone());
        }
        Ok(Some(handles))
    })
}

/// Ownership follows the live composed material, not merely a retained asset.
#[must_use]
pub fn matches_vegetation(
    world: &World,
    entity: Entity,
    expected: &Handle<VisualValleyVegetationMaterial>,
) -> bool {
    if let Some(binding) = world.get::<MeshMaterial3d<VisualValleyVegetationMaterial>>(entity) {
        return &binding.0 == expected
            && world
                .get::<MeshMaterial3d<VegetationMaterial>>(entity)
                .is_none();
    }
    let Some(Origin::Vegetation(original)) = world.get::<Origin>(entity) else {
        return false;
    };
    let Some(binding) = world.get::<MeshMaterial3d<VegetationMaterial>>(entity) else {
        return false;
    };
    original == expected
        && world.get_resource::<State>().is_some_and(|state| {
            state.entities.contains(&entity)
                && state.vegetation.get(&original.id()) == Some(&binding.0)
        })
        && world
            .get_resource::<Assets<VegetationMaterial>>()
            .is_some_and(|assets| assets.contains(&binding.0))
        && world
            .get_resource::<Assets<VisualValleyVegetationMaterial>>()
            .is_some_and(|assets| assets.contains(expected))
}

#[must_use]
pub fn owns(world: &World, entity: Entity) -> bool {
    world
        .get_resource::<State>()
        .is_some_and(|state| state.entities.contains(&entity))
}

/// Match the original surface through its actual, owned render projection.
/// Keeping the original asset alone does not prove that its replacement is live.
#[must_use]
pub fn matches_standard(
    world: &World,
    entity: Entity,
    expected: &Handle<StandardMaterial>,
) -> bool {
    if !world
        .get_resource::<Assets<StandardMaterial>>()
        .is_some_and(|assets| assets.contains(expected))
    {
        return false;
    }
    if let Some(binding) = world.get::<MeshMaterial3d<StandardMaterial>>(entity) {
        return &binding.0 == expected
            && world.get::<MeshMaterial3d<ModelMaterial>>(entity).is_none();
    }
    let Some(Origin::Model(original)) = world.get::<Origin>(entity) else {
        return false;
    };
    let Some(binding) = world.get::<MeshMaterial3d<ModelMaterial>>(entity) else {
        return false;
    };
    original == expected
        && world.get_resource::<State>().is_some_and(|state| {
            state.entities.contains(&entity) && state.models.get(&original.id()) == Some(&binding.0)
        })
        && world
            .get_resource::<Assets<ModelMaterial>>()
            .is_some_and(|assets| assets.contains(&binding.0))
}

fn context_key(receiver: &Receiver) -> Vec<String> {
    vec![
        format!(
            "{:?}:{:?}:{:?}:{:?}:{:?}",
            receiver.environment.id(),
            receiver.hill.id(),
            receiver.objects.id(),
            receiver.cloud_shadow.id(),
            receiver.frame.id()
        ),
        format!("{:?}", receiver.domain.to_array().map(f32::to_bits)),
    ]
}

/// Called after actual caster adoption and before Bevy extraction. Only real
/// material/target changes replace assets; the shared frame carries live clouds.
pub fn sync(world: &mut World, frame: &Handle<ShaderBuffer>) -> Result<(), String> {
    let base = Receiver::from_scene(world, frame.clone(), false)?;
    let key = context_key(&base);
    world.resource_scope(|world, mut state: Mut<State>| -> Result<(), String> {
        let context_changed = state.context.as_ref() != Some(&key);
        let changed_models: HashSet<_> = state
            .model_events
            .read(world.resource::<Messages<AssetEvent<StandardMaterial>>>())
            .filter_map(|event| match event {
                AssetEvent::Modified { id } | AssetEvent::Removed { id } => Some(*id),
                _ => None,
            })
            .collect();
        let changed_terrain: HashSet<_> = state
            .terrain_events
            .read(world.resource::<Messages<AssetEvent<crate::VisualValleyTerrainMaterial>>>())
            .filter_map(|event| match event {
                AssetEvent::Modified { id } | AssetEvent::Removed { id } => Some(*id),
                _ => None,
            })
            .collect();
        let changed_vegetation: HashSet<_> = world
            .get_resource::<Messages<AssetEvent<VisualValleyVegetationMaterial>>>()
            .map(|events| {
                state
                    .vegetation_events
                    .read(events)
                    .filter_map(|event| match event {
                        AssetEvent::Modified { id } | AssetEvent::Removed { id } => Some(*id),
                        _ => None,
                    })
                    .collect()
            })
            .unwrap_or_default();
        state.convert(world, &base)?;
        state.refresh_assets(
            world,
            &base,
            context_changed,
            &changed_models,
            &changed_terrain,
        )?;
        for (source, handle) in &state.vegetation {
            if context_changed || changed_vegetation.contains(source) {
                let material = world
                    .resource::<Assets<VisualValleyVegetationMaterial>>()
                    .get(*source)
                    .ok_or("Original governed vegetation material removed")?
                    .clone();
                super::validate_standard(&material.base)?;
                let mut receiver = base.clone();
                receiver.vegetation = true;
                world
                    .resource_mut::<Assets<VegetationMaterial>>()
                    .insert(
                        handle.id(),
                        VegetationMaterial {
                            base: material,
                            extension: receiver,
                        },
                    )
                    .map_err(|error| error.to_string())?;
            }
        }
        state.observe(world)?;
        state.context = Some(key);
        Ok(())
    })
}

impl State {
    fn convert(&mut self, world: &mut World, base: &Receiver) -> Result<(), String> {
        let models: Vec<_> = world
            .query::<(Entity, &MeshMaterial3d<StandardMaterial>)>()
            .iter(world)
            .map(|(entity, material)| (entity, material.0.clone()))
            .collect();
        let terrains: Vec<_> = world
            .query::<(Entity, &MeshMaterial3d<crate::VisualValleyTerrainMaterial>)>()
            .iter(world)
            .map(|(entity, material)| (entity, material.0.clone()))
            .collect();
        for (entity, source) in models {
            if let std::collections::hash_map::Entry::Vacant(entry) = self.models.entry(source.id())
            {
                let Some(material) = world
                    .resource::<Assets<StandardMaterial>>()
                    .get(&source)
                    .cloned()
                else {
                    continue;
                };
                super::validate_standard(&material)?;
                let handle = world
                    .resource_mut::<Assets<ModelMaterial>>()
                    .add(ModelMaterial {
                        base: material,
                        extension: base.clone(),
                    });
                entry.insert(handle);
            }
            let handle = self
                .models
                .get(&source.id())
                .ok_or("Source model material absent")?
                .clone();
            world
                .entity_mut(entity)
                .remove::<MeshMaterial3d<StandardMaterial>>()
                .insert((MeshMaterial3d(handle), Origin::Model(source)));
        }
        for (entity, source) in terrains {
            if let std::collections::hash_map::Entry::Vacant(entry) =
                self.terrains.entry(source.id())
            {
                let Some(material) = world
                    .resource::<Assets<crate::VisualValleyTerrainMaterial>>()
                    .get(&source)
                    .cloned()
                else {
                    continue;
                };
                super::validate_standard(&material.base)?;
                let mut receiver = base.clone();
                receiver.terrain = true;
                let handle = world
                    .resource_mut::<Assets<TerrainMaterial>>()
                    .add(TerrainMaterial {
                        base: material,
                        extension: receiver,
                    });
                entry.insert(handle);
            }
            let handle = self
                .terrains
                .get(&source.id())
                .ok_or("Source terrain material absent")?
                .clone();
            world
                .entity_mut(entity)
                .remove::<MeshMaterial3d<crate::VisualValleyTerrainMaterial>>()
                .insert((MeshMaterial3d(handle), Origin::Terrain(source)));
        }
        self.convert_vegetation(world);
        // A retired scene primitive must not keep a dead original material in
        // this projection's cache. Live HLOD outputs also retain their route.
        let live_models: HashSet<_> = world
            .query::<&MeshMaterial3d<ModelMaterial>>()
            .iter(world)
            .map(|value| value.id())
            .collect();
        let live_terrains: HashSet<_> = world
            .query::<&MeshMaterial3d<TerrainMaterial>>()
            .iter(world)
            .map(|value| value.id())
            .collect();
        self.models
            .retain(|_, value| live_models.contains(&value.id()));
        self.terrains
            .retain(|_, value| live_terrains.contains(&value.id()));
        let live_vegetation: HashSet<_> = world
            .query::<&MeshMaterial3d<VegetationMaterial>>()
            .iter(world)
            .map(|value| value.id())
            .collect();
        let originals = world.get_resource::<Assets<VisualValleyVegetationMaterial>>();
        self.vegetation.retain(|source, projected| {
            live_vegetation.contains(&projected.id())
                || originals.is_some_and(|assets| assets.contains(*source))
        });
        Ok(())
    }
    fn convert_vegetation(&self, world: &mut World) {
        // Only the governed imported-model owner participates here. Procedural
        // vegetation retains its existing material/lifecycle until admitted.
        let vegetation: Vec<_> = world
            .query_filtered::<(Entity, &MeshMaterial3d<VisualValleyVegetationMaterial>),
                With<crate::source_vegetation::scene::ImportedPlant>>()
            .iter(world)
            .filter_map(|(entity, source)| self.vegetation.get(&source.id())
                .map(|handle| (entity, source.0.clone(), handle.clone())))
            .collect();
        for (entity, source, handle) in vegetation {
            world
                .entity_mut(entity)
                .remove::<MeshMaterial3d<VisualValleyVegetationMaterial>>()
                .insert((MeshMaterial3d(handle), Origin::Vegetation(source)));
        }
    }

    fn refresh_assets(
        &self,
        world: &mut World,
        base: &Receiver,
        context_changed: bool,
        changed_models: &HashSet<AssetId<StandardMaterial>>,
        changed_terrain: &HashSet<AssetId<crate::VisualValleyTerrainMaterial>>,
    ) -> Result<(), String> {
        for (source, handle) in &self.models {
            if context_changed || changed_models.contains(source) {
                let material = world
                    .resource::<Assets<StandardMaterial>>()
                    .get(*source)
                    .ok_or("Original source model material removed")?
                    .clone();
                super::validate_standard(&material)?;
                world
                    .resource_mut::<Assets<ModelMaterial>>()
                    .insert(
                        handle.id(),
                        ModelMaterial {
                            base: material,
                            extension: base.clone(),
                        },
                    )
                    .map_err(|error| error.to_string())?;
            }
        }
        for (source, handle) in &self.terrains {
            if context_changed || changed_terrain.contains(source) {
                let material = world
                    .resource::<Assets<crate::VisualValleyTerrainMaterial>>()
                    .get(*source)
                    .ok_or("Original source terrain material removed")?
                    .clone();
                super::validate_standard(&material.base)?;
                let mut receiver = base.clone();
                receiver.terrain = true;
                world
                    .resource_mut::<Assets<TerrainMaterial>>()
                    .insert(
                        handle.id(),
                        TerrainMaterial {
                            base: material,
                            extension: receiver,
                        },
                    )
                    .map_err(|error| error.to_string())?;
            }
        }
        Ok(())
    }
    fn observe(&mut self, world: &mut World) -> Result<(), String> {
        self.entities.clear();
        let model_handles: HashSet<_> = self.models.values().map(Handle::id).collect();
        let terrain_handles: HashSet<_> = self.terrains.values().map(Handle::id).collect();
        let mut models = 0;
        let mut terrain = 0;
        for (entity, material) in world
            .query::<(Entity, &MeshMaterial3d<ModelMaterial>)>()
            .iter(world)
        {
            if !model_handles.contains(&material.id()) {
                return Err("Unowned source model material".into());
            }
            self.entities.insert(entity);
            models += 1;
        }
        for (entity, material) in world
            .query::<(Entity, &MeshMaterial3d<TerrainMaterial>)>()
            .iter(world)
        {
            if !terrain_handles.contains(&material.id()) {
                return Err("Unowned source terrain material".into());
            }
            self.entities.insert(entity);
            terrain += 1;
        }
        let vegetation_handles: HashSet<_> = self.vegetation.values().map(Handle::id).collect();
        for (entity, material) in world
            .query::<(Entity, &MeshMaterial3d<VegetationMaterial>)>()
            .iter(world)
        {
            if !vegetation_handles.contains(&material.id()) {
                return Err("Unowned source vegetation material".into());
            }
            self.entities.insert(entity);
        }
        let pending = world
            .query::<&MeshMaterial3d<StandardMaterial>>()
            .iter(world)
            .count()
            + world
                .query::<&MeshMaterial3d<crate::VisualValleyTerrainMaterial>>()
                .iter(world)
                .count();
        world.insert_resource(Status {
            models,
            terrain,
            model_materials: self.models.len(),
            terrain_materials: self.terrains.len(),
            pending,
        });
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn receiver() -> Receiver {
        Receiver {
            scene: default(),
            shadows: default(),
            frame: default(),
            local_lights: crate::source_lighting::local_lights::gpu::BUFFER,
            environment: default(),
            hill: default(),
            objects: default(),
            object_settings: default(),
            clouds: default(),
            cloud_shadow: default(),
            domain: default(),
            terrain: false,
            vegetation: false,
        }
    }

    #[test]
    fn standard_projection_keeps_source_barriers_and_refuses_stale_or_ambiguous_bindings() {
        let mut world = World::new();
        world.init_resource::<Assets<StandardMaterial>>();
        world.init_resource::<Assets<ModelMaterial>>();
        world.init_resource::<State>();
        let original = world
            .resource_mut::<Assets<StandardMaterial>>()
            .add(StandardMaterial {
                base_color_texture: Some(Handle::default()),
                ..default()
            });
        let entity = world.spawn(MeshMaterial3d(original.clone())).id();
        assert!(matches_standard(&world, entity, &original));
        let receiver = receiver();
        let projected = world
            .resource_mut::<Assets<ModelMaterial>>()
            .add(ModelMaterial {
                base: StandardMaterial {
                    base_color_texture: Some(Handle::default()),
                    ..default()
                },
                extension: receiver,
            });
        let mut dependencies = Vec::new();
        bevy::asset::VisitAssetDependencies::visit_dependencies(
            world
                .resource::<Assets<ModelMaterial>>()
                .get(&projected)
                .unwrap(),
            &mut |id| dependencies.push(id),
        );
        assert_eq!(dependencies.len(), 9); // Authored texture, four scene images, four buffers.
        assert!(
            dependencies.contains(
                &crate::source_lighting::local_lights::gpu::BUFFER
                    .id()
                    .untyped()
            )
        );
        world
            .entity_mut(entity)
            .remove::<MeshMaterial3d<StandardMaterial>>()
            .insert((
                MeshMaterial3d(projected.clone()),
                Origin::Model(original.clone()),
            ));
        assert!(!matches_standard(&world, entity, &original));
        {
            let mut state = world.resource_mut::<State>();
            state.entities.insert(entity);
            state.models.insert(original.id(), projected.clone());
        }
        assert!(matches_standard(&world, entity, &original));
        world
            .entity_mut(entity)
            .insert(Origin::Model(Handle::default()));
        assert!(!matches_standard(&world, entity, &original));
        world.entity_mut(entity).insert((
            Origin::Model(original.clone()),
            MeshMaterial3d(original.clone()),
        ));
        assert!(!matches_standard(&world, entity, &original));
        world
            .entity_mut(entity)
            .remove::<MeshMaterial3d<StandardMaterial>>();
        assert!(matches_standard(&world, entity, &original));
        let material = world
            .resource_mut::<Assets<ModelMaterial>>()
            .remove(projected.id())
            .unwrap();
        assert!(!matches_standard(&world, entity, &original));
        world
            .resource_mut::<Assets<ModelMaterial>>()
            .insert(projected.id(), material)
            .unwrap();
        world
            .resource_mut::<Assets<StandardMaterial>>()
            .remove(original.id());
        assert!(!matches_standard(&world, entity, &original));
    }

    #[test]
    fn governed_source_projection_keeps_dependencies_and_rejects_stale_receivers() {
        use bevy::asset::VisitAssetDependencies;
        let mut world = World::new();
        world.init_resource::<Assets<VisualValleyVegetationMaterial>>();
        world.init_resource::<Assets<VegetationMaterial>>();
        world.init_resource::<State>();
        let source = VisualValleyVegetationMaterial {
            base: StandardMaterial {
                alpha_mode: AlphaMode::Blend,
                double_sided: true,
                ..default()
            },
            extension: crate::vegetation::VisualValleyVegetationExtension::default(),
        };
        let wind = source.extension.temporal_lod_buffer.clone();
        let original = world
            .resource_mut::<Assets<VisualValleyVegetationMaterial>>()
            .add(source.clone());
        let entity = world
            .spawn((
                MeshMaterial3d(original.clone()),
                crate::source_vegetation::scene::ImportedPlant {
                    id: forge_core::EntityId::deterministic("source-vegetation-receiver-test"),
                    head_hash: None,
                },
            ))
            .id();
        let mut receiver = receiver();
        receiver.vegetation = true;
        let projected =
            world
                .resource_mut::<Assets<VegetationMaterial>>()
                .add(VegetationMaterial {
                    base: source,
                    extension: receiver.clone(),
                });
        let material = world
            .resource::<Assets<VegetationMaterial>>()
            .get(&projected)
            .unwrap();
        assert_eq!(material.base.base.alpha_mode, AlphaMode::Blend);
        assert!(material.base.base.double_sided);
        let mut dependencies = Vec::new();
        material.visit_dependencies(&mut |id| dependencies.push(id));
        assert!(dependencies.contains(&wind.id().untyped()));
        assert!(dependencies.contains(&receiver.frame.id().untyped()));
        assert!(dependencies.contains(&receiver.environment.id().untyped()));
        world.resource_scope(|world, mut state: Mut<State>| {
            state.vegetation.insert(original.id(), projected.clone());
            state.convert(world, &receiver).unwrap();
            state.observe(world).unwrap();
        });
        assert!(
            world
                .get::<MeshMaterial3d<VisualValleyVegetationMaterial>>(entity)
                .is_none()
        );
        assert!(matches_vegetation(&world, entity, &original));
        world
            .entity_mut(entity)
            .insert(MeshMaterial3d(original.clone()));
        assert!(!matches_vegetation(&world, entity, &original));
        world
            .entity_mut(entity)
            .remove::<MeshMaterial3d<VisualValleyVegetationMaterial>>();
        world.resource_mut::<State>().entities.remove(&entity);
        assert!(!matches_vegetation(&world, entity, &original));
        world.resource_mut::<State>().entities.insert(entity);
        world
            .resource_mut::<Assets<VegetationMaterial>>()
            .remove(projected.id());
        assert!(!matches_vegetation(&world, entity, &original));
    }
}
