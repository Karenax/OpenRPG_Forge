//! Disposable prop instances with independently admitted collision and caster subsets.
//! Content adapters supply verified prototypes and revisions; the engine owns
//! entity adoption, material bindings, collision retirement and shadow projection.
use crate::source_shadows::casters::OpaqueDraw;
use crate::source_surface::{SourceSurfaceMaterial, nature::NatureMaterial};
use crate::{
    ManualPlacementCollision, ManualPlacementCollisionExpected,
    ManualPlacementCollisionNotRequired, ManualPlacementPart, ManualPlacementProjection,
};
use bevy::{light::NotShadowCaster, prelude::*};
use bevy_rapier3d::prelude::Collider;
use forge_core::AssetId;
use forge_project::zone_authoring::placements::{ManualPlacement, PlacementChanges};
use std::collections::BTreeMap;

/// Retained source semantics. The character controller must consume walkability separately.
#[derive(Component, Clone, Debug)]
pub struct SourceCollision {
    pub source_index: usize,
    pub source_shape: String,
    pub tag: String,
    pub solid: bool,
    pub walkable: Option<bool>,
}

#[derive(Clone)]
pub struct PreparedPrimitive {
    pub transform: Transform,
    pub collider: Collider,
    pub source: SourceCollision,
}

#[derive(Clone)]
pub enum MaterialHandle {
    Surface(Handle<SourceSurfaceMaterial>),
    Nature(Handle<NatureMaterial>),
}

pub struct PreparedPart {
    pub mesh: Mesh,
    pub shadow: Option<Mesh>,
    pub material: MaterialHandle,
}

pub struct PreparedScene {
    templates: BTreeMap<AssetId, Vec<PreparedPart>>,
    placements: Vec<ManualPlacement>,
    collisions: BTreeMap<AssetId, Vec<PreparedPrimitive>>,
}

pub struct Instance {
    pub source: ManualPlacement,
    pub root: Entity,
    pub parts: Vec<Entity>,
    pub collisions: Vec<Entity>,
    casters: Vec<Handle<Mesh>>,
}

#[derive(Resource, Default)]
pub struct ResidentScene {
    pub instances: Vec<Instance>,
    retired: Vec<(Entity, Entity)>,
}

impl PreparedScene {
    /// Validate the whole admitted batch before allocating meshes or entities.
    pub fn new(
        templates: BTreeMap<AssetId, Vec<PreparedPart>>,
        placements: &[ManualPlacement],
        world: &World,
    ) -> Result<Self, String> {
        PlacementChanges::between(&[], placements)?;
        let surfaces = world
            .get_resource::<Assets<SourceSurfaceMaterial>>()
            .ok_or("Prop surface materials unavailable")?;
        let nature = world
            .get_resource::<Assets<NatureMaterial>>()
            .ok_or("Prop nature materials unavailable")?;
        if world.get_resource::<Assets<Mesh>>().is_none() {
            return Err("Prop mesh store unavailable".into());
        }
        let mut triangles = 0_usize;
        let mut parts_count = 0_usize;
        for placement in placements {
            let parts = templates
                .get(&placement.asset_id)
                .ok_or("Prop placement prototype absent")?;
            for part in parts {
                let resident = match &part.material {
                    MaterialHandle::Surface(handle) => surfaces.get(handle).is_some(),
                    MaterialHandle::Nature(handle) => nature.get(handle).is_some(),
                };
                if !resident {
                    return Err("Prop material unresolved".into());
                }
                triangles = triangles
                    .saturating_add(part.mesh.indices().map_or(0, |indices| indices.len() / 3));
                parts_count = parts_count.saturating_add(1);
            }
            if triangles > 2_000_000 || parts_count > 16_384 {
                return Err("Prop scene geometry budget exceeded".into());
            }
        }
        Ok(Self {
            templates,
            placements: placements.to_vec(),
            collisions: BTreeMap::new(),
        })
    }

    pub fn asset_ids(&self) -> impl Iterator<Item = AssetId> + '_ {
        self.templates.keys().copied()
    }

    /// Only an explicitly supplied empty owner waives collision requirements.
    pub fn with_collisions(
        mut self,
        collisions: BTreeMap<AssetId, Vec<PreparedPrimitive>>,
    ) -> Result<Self, String> {
        if !self.templates.keys().eq(collisions.keys()) {
            return Err("Prop collision owners differ from admitted prototypes".into());
        }
        self.collisions = collisions;
        Ok(self)
    }

    /// Adopt one fully prepared batch; derived render state never edits authoring documents.
    #[expect(
        clippy::too_many_lines,
        reason = "Source validation and GPU operations remain in execution order so rejection and resource mutation boundaries are auditable"
    )]
    pub fn stage(self, world: &mut World) {
        let templates: BTreeMap<_, Vec<_>> = self
            .templates
            .into_iter()
            .map(|(id, parts)| {
                let parts = parts
                    .into_iter()
                    .map(|part| {
                        let mesh = world.resource_mut::<Assets<Mesh>>().add(part.mesh);
                        let shadow = part
                            .shadow
                            .map(|mesh| world.resource_mut::<Assets<Mesh>>().add(mesh));
                        (mesh, shadow, part.material)
                    })
                    .collect();
                (id, parts)
            })
            .collect();
        let mut instances = Vec::with_capacity(self.placements.len());
        for source in self.placements {
            let root = world
                .spawn((
                    Name::new(source.name.clone()),
                    crate::domain_transform_to_bevy(source.transform),
                    Visibility::Inherited,
                    ManualPlacementProjection(source.clone()),
                    // Empty is intentionally MissingPrimitives, not a proof of no collisions.
                    ManualPlacementCollisionExpected {
                        placement: source.id,
                        primitives: Vec::new(),
                    },
                ))
                .id();
            let mut parts = Vec::new();
            let mut casters = Vec::new();
            let mut collisions = Vec::new();
            if let Some(primitives) = self.collisions.get(&source.asset_id) {
                for primitive in primitives {
                    collisions.push(
                        world
                            .spawn((
                                primitive.transform,
                                primitive.collider.clone(),
                                primitive.source.clone(),
                                ManualPlacementCollision(source.id),
                                ChildOf(root),
                            ))
                            .id(),
                    );
                }
                if primitives.is_empty() {
                    world
                        .entity_mut(root)
                        .insert(ManualPlacementCollisionNotRequired(source.id));
                }
                world
                    .entity_mut(root)
                    .insert(ManualPlacementCollisionExpected {
                        placement: source.id,
                        primitives: collisions.clone(),
                    });
            }
            for (mesh, shadow, material) in &templates[&source.asset_id] {
                let mut entity = world.spawn((
                    Mesh3d(mesh.clone()),
                    Transform::IDENTITY,
                    Visibility::Inherited,
                    ChildOf(root),
                    ManualPlacementPart(source.id),
                    NotShadowCaster,
                ));
                match material {
                    MaterialHandle::Surface(handle) => {
                        entity.insert(MeshMaterial3d(handle.clone()));
                    }
                    MaterialHandle::Nature(handle) => {
                        entity.insert(MeshMaterial3d(handle.clone()));
                    }
                }
                parts.push(entity.id());
                if let Some(mesh) = shadow {
                    casters.push(mesh.clone());
                }
            }
            instances.push(Instance {
                source,
                root,
                parts,
                collisions,
                casters,
            });
        }
        let mut retired = Vec::new();
        if let Some(previous) = world.remove_resource::<ResidentScene>() {
            retired = previous.retired;
            for instance in previous.instances {
                for &part in &instance.collisions {
                    if let Some(link) =
                        world.get::<bevy_rapier3d::prelude::RapierContextEntityLink>(part)
                    {
                        retired.push((link.0, part));
                    }
                }
                world.despawn(instance.root);
            }
        }
        retired.retain(|&(context, part)| registered(world, context, part));
        world.insert_resource(ResidentScene { instances, retired });
    }
}

impl ResidentScene {
    /// Retired backend primitives must disappear before the replacement is ready to play.
    #[must_use]
    pub fn collisions_retired(&self, world: &World) -> bool {
        self.retired
            .iter()
            .all(|&(context, part)| !registered(world, context, part))
    }
    /// Rebuild custom caster inputs using live root transforms, including edits and duplicates.
    ///
    /// # Errors
    /// Rejects disappeared roots, absent mesh data or malformed caster attributes.
    pub fn shadow_draws(&self, world: &World) -> Result<Vec<OpaqueDraw>, String> {
        let meshes = world
            .get_resource::<Assets<Mesh>>()
            .ok_or("Debris shadow meshes unavailable")?;
        let mut draws = Vec::new();
        for (_, handle, model) in self.caster_parts(world)? {
            let mesh = meshes.get(&handle).ok_or("Debris caster mesh absent")?;
            draws.push(OpaqueDraw::from_mesh(mesh, model)?);
        }
        Ok(draws)
    }

    /// Enumerate exactly one identity and model per opaque draw, in upload order.
    /// Roots with no caster contribute no entry; a root may own several parts.
    ///
    /// # Errors
    /// Rejects stale roots or missing model transforms before frame publication.
    pub fn shadow_parts(&self, world: &World) -> Result<Vec<(Entity, Mat4)>, String> {
        Ok(self
            .caster_parts(world)?
            .into_iter()
            .map(|(root, _, model)| (root, model))
            .collect())
    }

    fn caster_parts(&self, world: &World) -> Result<Vec<(Entity, Handle<Mesh>, Mat4)>, String> {
        let mut parts = Vec::new();
        for instance in &self.instances {
            if instance.casters.is_empty()
                || world
                    .get::<InheritedVisibility>(instance.root)
                    .is_some_and(|visible| !visible.get())
            {
                continue;
            }
            if world.get::<ChildOf>(instance.root).is_some() {
                return Err("Debris shadow root must be unparented".into());
            }
            let model = world
                .get::<GlobalTransform>(instance.root)
                .ok_or("Debris root transform absent")?
                .to_matrix();
            for handle in &instance.casters {
                parts.push((instance.root, handle.clone(), model));
            }
        }
        Ok(parts)
    }
}

fn registered(world: &World, context: Entity, part: Entity) -> bool {
    world
        .get::<bevy_rapier3d::plugin::context::RapierContextColliders>(context)
        .is_some_and(|colliders| colliders.entity2collider().contains_key(&part))
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{
        asset::RenderAssetUsages, mesh::Indices, render::render_resource::PrimitiveTopology,
    };
    use forge_core::EntityId;

    #[test]
    #[allow(
        clippy::too_many_lines,
        reason = "One lifecycle verifies independent adoption, caster coverage and actual backend retirement in order."
    )]
    fn independent_props_preserve_caster_subset_and_wait_for_backend_retirement() {
        let mut app = App::new();
        app.add_plugins((
            MinimalPlugins,
            TransformPlugin,
            crate::ManualPlacementCollisionPlugin,
        ));
        crate::sampled_terrain_physics::register(&mut app);
        app.init_resource::<Assets<Mesh>>()
            .init_resource::<Assets<SourceSurfaceMaterial>>()
            .init_resource::<Assets<NatureMaterial>>();
        let material = app
            .world_mut()
            .resource_mut::<Assets<SourceSurfaceMaterial>>()
            .add(crate::source_surface::tests::lighting());
        let mesh = Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::default(),
        )
        .with_inserted_attribute(
            Mesh::ATTRIBUTE_POSITION,
            vec![[0.0, 0.0, 0.0], [2.0, 0.0, 0.0], [0.0, 0.0, 2.0]],
        )
        .with_inserted_indices(Indices::U32(vec![0, 1, 2]));
        let asset = AssetId::deterministic("independent prop geometry");
        let source = ManualPlacement {
            id: EntityId::deterministic("independent prop placement"),
            name: "Module prop".into(),
            asset_id: asset,
            asset_hash: forge_core::bytes_sha256(b"module revision"),
            transform: forge_core::Transform::IDENTITY,
            locked: false,
            particles: None,
            physics: None,
            local_lights: None,
        };
        let templates = BTreeMap::from([(
            asset,
            vec![
                PreparedPart {
                    mesh: mesh.clone(),
                    shadow: Some(mesh.clone()),
                    material: MaterialHandle::Surface(material.clone()),
                },
                PreparedPart {
                    mesh,
                    shadow: None,
                    material: MaterialHandle::Surface(material),
                },
            ],
        )]);
        let prepared =
            PreparedScene::new(templates, std::slice::from_ref(&source), app.world()).unwrap();
        // Supply the admitted owner; absence is never an implicit collision waiver.
        let collisions = BTreeMap::from([(
            asset,
            vec![PreparedPrimitive {
                transform: Transform::IDENTITY,
                collider: Collider::cuboid(1.0, 1.0, 1.0),
                source: SourceCollision {
                    source_index: 0,
                    source_shape: "box".into(),
                    tag: "solid".into(),
                    solid: true,
                    walkable: Some(false),
                },
            }],
        )]);
        prepared
            .with_collisions(collisions)
            .unwrap()
            .stage(app.world_mut());
        assert!(!crate::manual_placements_ready(
            app.world_mut(),
            std::slice::from_ref(&source)
        ));
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        assert!(crate::manual_placements_ready(
            app.world_mut(),
            std::slice::from_ref(&source)
        ));
        let root = app.world().resource::<ResidentScene>().instances[0].root;
        // This CPU-only app has no render visibility propagation, as in the
        // original acquired-scene regression. Publish its explicit observation.
        app.world_mut()
            .entity_mut(root)
            .insert(InheritedVisibility::VISIBLE);
        let resident = app.world().resource::<ResidentScene>();
        assert_eq!(resident.instances[0].parts.len(), 2);
        assert_eq!(resident.shadow_draws(app.world()).unwrap().len(), 1);
        assert_eq!(resident.shadow_parts(app.world()).unwrap().len(), 1);
        app.world_mut()
            .entity_mut(root)
            .insert(Transform::from_xyz(8.0, 1.0, -2.0));
        app.update();
        assert_eq!(
            app.world()
                .resource::<ResidentScene>()
                .shadow_parts(app.world())
                .unwrap()[0]
                .1,
            Transform::from_xyz(8.0, 1.0, -2.0).to_matrix()
        );
        let empty = PreparedScene::new(BTreeMap::new(), &[], app.world()).unwrap();
        empty.stage(app.world_mut());
        let resident = app.world().resource::<ResidentScene>();
        assert!(resident.instances.is_empty());
        assert!(resident.shadow_draws(app.world()).unwrap().is_empty());
        assert!(!resident.collisions_retired(app.world()));
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        assert!(
            app.world()
                .resource::<ResidentScene>()
                .collisions_retired(app.world())
        );
        assert!(app.world().get_entity(root).is_err());
    }
}
