//! Disposable projection of a verified boat package into an authoring frame.
//! Geometry, source hierarchy and water exclusion are independent of the
//! material adapter. Staging does not qualify appearance or boat controllers.
use crate::{
    ManualPlacementCollision, ManualPlacementCollisionExpected, ManualPlacementPart,
    ManualPlacementProjection,
    tidewater_hull_mask::{Geometry, HullVolume},
    tidewater_mesh::{DATA, TINT},
};
use bevy::{
    asset::RenderAssetUsages,
    light::NotShadowCaster,
    mesh::{Indices, VertexAttributeValues},
    prelude::*,
    render::render_resource::PrimitiveTopology,
};
use bevy_rapier3d::prelude::{Collider, Sensor};
use forge_assets::{
    source_geometry::AssetLocalFrame,
    tidewater_boat::{BoatPackage, ResolvedBoatPart},
};
use forge_core::{AssetId, ContentHash, EntityId, Transform as ForgeTransform};
use forge_project::zone_authoring::placements::ManualPlacement;
use std::{collections::BTreeMap, marker::PhantomData, sync::Arc};

struct PreparedNode {
    id: String,
    parent: Option<usize>,
    transform: Transform,
    visible: Visibility,
}
struct PreparedPart {
    node: usize,
    mesh: Mesh,
    source: BoatPart,
}
struct PreparedCollider {
    transform: Transform,
    shape: Collider,
    source: BoatCollisionVolume,
}

/// A source part's render intent. The material/shadow adapters must consume it;
/// retaining these flags alone does not implement procedural alpha or shadows.
#[derive(Component, Clone, Debug, PartialEq, Eq)]
pub struct BoatPart {
    pub id: String,
    pub material: String,
    pub cast_shadow: bool,
    pub receive_shadow: bool,
    pub render_order: i32,
}
#[derive(Component, Clone, Debug, PartialEq, Eq)]
pub struct BoatSourceNode(pub String);

/// Derived identity, not the canonical asset or placement document.
#[derive(Component, Clone, Debug, PartialEq, Eq)]
pub struct BoatInstanceIdentity {
    pub asset_id: AssetId,
    pub revision: ContentHash,
}

#[derive(Component, Clone, Debug, PartialEq, Eq)]
pub struct BoatProjectionOwner {
    pub root: Entity,
    pub asset_id: AssetId,
    pub revision: ContentHash,
    pub placement: Option<EntityId>,
}

/// Source collision intent is retained separately from the static Rapier shape.
/// In Player.js, `walkable` selects a top face; `solid` selects wall push-out.
/// A walkable non-solid deck is therefore not a sensor. These static cuboids
/// provide authoring collision; boat-local walking needs a separate controller.
#[derive(Component, Clone, Debug, PartialEq, Eq)]
pub struct BoatCollisionVolume {
    pub id: String,
    pub tag: String,
    pub walkable: bool,
    pub solid: bool,
}

/// Explicit ownership for a water-exclusion census. An unrelated `HullVolume`
/// elsewhere in the scene cannot satisfy this boat instance's requirement.
#[derive(Component, Clone, Debug, PartialEq, Eq)]
pub struct BoatWaterExclusion {
    pub owner: Entity,
    pub asset_id: AssetId,
    pub revision: ContentHash,
    pub placement: Option<EntityId>,
    pub source_node: String,
    pub source_volume: String,
}

/// CPU-only preparation: no ECS mutation, handles, renderer or physics backend.
pub struct PreparedBoat {
    asset_id: AssetId,
    revision: ContentHash,
    source_id: String,
    canonical: Transform,
    nodes: Vec<PreparedNode>,
    parts: Vec<PreparedPart>,
    colliders: Vec<PreparedCollider>,
    hull: Arc<Geometry>,
    hull_parent: usize,
    source_root: usize,
    hull_transform: Transform,
    hull_id: String,
    triangles: usize,
}

struct ResidentNode {
    entity: Entity,
    parent: Entity,
    id: String,
    transform: Transform,
    visible: Visibility,
}
struct ResidentPart<M: Material> {
    entity: Entity,
    mesh: Handle<Mesh>,
    material: Handle<M>,
    source: BoatPart,
}
struct ResidentCollider {
    entity: Entity,
    parent: Entity,
    transform: Transform,
    shape: Collider,
    source: BoatCollisionVolume,
}
struct Instance<M: Material> {
    root: Entity,
    transform: Transform,
    placement: Option<ManualPlacement>,
    nodes: Vec<ResidentNode>,
    parts: Vec<ResidentPart<M>>,
    colliders: Vec<ResidentCollider>,
    hull: Entity,
    hull_parent: Entity,
    hull_identity: BoatWaterExclusion,
    owner: BoatProjectionOwner,
}

/// Counts only this adapter's verified instances, after a fresh ownership check.
/// This is not a claim that every boat in an entire zone has been loaded.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct BoatCensus {
    pub instances: usize,
    pub source_nodes: usize,
    pub render_parts: usize,
    pub collision_volumes: usize,
    pub water_exclusions: usize,
    pub triangles: usize,
}

#[derive(Resource)]
pub struct ResidentBoat<M: Material> {
    identity: BoatInstanceIdentity,
    instances: Vec<Instance<M>>,
    meshes: Vec<Handle<Mesh>>,
    mesh_fingerprints: Vec<ContentHash>,
    hull_geometry: Arc<Geometry>,
    hull_transform: Transform,
    triangles: usize,
    marker: PhantomData<fn() -> M>,
}

impl PreparedBoat {
    /// `asset_id` belongs to the governed catalog entry supplied by the caller.
    /// The package's receipt provides the exact revision; no module slug routing.
    pub fn from_package(package: &BoatPackage, asset_id: AssetId) -> Result<Self, String> {
        let description = package.description();
        let indices: BTreeMap<_, _> = description
            .nodes
            .iter()
            .enumerate()
            .map(|(i, node)| (node.id.as_str(), i))
            .collect();
        let node_index = |id: &str| {
            indices
                .get(id)
                .copied()
                .ok_or_else(|| format!("Missing boat node: {id}"))
        };
        let nodes = description
            .nodes
            .iter()
            .filter(|node| node.parent.is_none())
            .count();
        if nodes != 1 {
            return Err("Boat source requires one hierarchy root".into());
        }
        let source_root = description
            .nodes
            .iter()
            .position(|node| node.parent.is_none())
            .ok_or("Boat root is absent")?;
        let nodes = description
            .nodes
            .iter()
            .map(|node| {
                Ok(PreparedNode {
                    id: node.id.clone(),
                    parent: node.parent.as_deref().map(node_index).transpose()?,
                    transform: local_frame(&node.transform)?,
                    visible: if node.visible {
                        Visibility::Inherited
                    } else {
                        Visibility::Hidden
                    },
                })
            })
            .collect::<Result<Vec<_>, String>>()?;
        let mut parts = Vec::with_capacity(description.parts.len());
        for part in &description.parts {
            parts.push(PreparedPart {
                node: node_index(&part.node)?,
                mesh: source_mesh(package.resolve_part(&part.id)?)?,
                source: BoatPart {
                    id: part.id.clone(),
                    material: part.material.clone(),
                    cast_shadow: part.cast_shadow,
                    receive_shadow: part.receive_shadow,
                    render_order: part.render_order,
                },
            });
        }
        let colliders = prepare_colliders(&description.colliders)?;
        let exclusion = package.water_exclusion()?;
        let hull_parent = node_index(&exclusion.parent_node)?;
        let hull_transform = local_frame(&exclusion.local_frame)?;
        let boundary = exclusion
            .source_boundary_edges
            .ok_or("Boat exclusion must retain its verified source-proxy policy")?;
        let hull = Geometry::from_verified_source_proxy(
            exclusion.positions,
            exclusion.indices,
            exclusion.fingerprint,
            boundary,
        )?;
        Ok(Self {
            asset_id,
            revision: package.receipt_hash(),
            source_id: description.source_id.clone(),
            canonical: Transform::from_translation(vector(
                description.canonical_placement.translation_m,
            )?)
            .with_rotation(Quat::from_rotation_y(finite(
                description.canonical_placement.heading_radians,
            )?)),
            nodes,
            parts,
            colliders,
            hull,
            hull_parent,
            source_root,
            hull_transform,
            hull_id: exclusion.id,
            triangles: description.triangles,
        })
    }

    #[must_use]
    pub fn identity(&self) -> BoatInstanceIdentity {
        BoatInstanceIdentity {
            asset_id: self.asset_id,
            revision: self.revision,
        }
    }

    #[must_use]
    pub fn canonical_transform(&self) -> ForgeTransform {
        let value = self.canonical;
        ForgeTransform {
            translation_m: forge_core::Vec3::new(
                value.translation.x,
                value.translation.y,
                value.translation.z,
            ),
            rotation: forge_core::UnitQuaternion::new(
                value.rotation.x,
                value.rotation.y,
                value.rotation.z,
                value.rotation.w,
            ),
            scale: forge_core::Vec3::ONE,
        }
    }

    /// Stage a reference instance. Material handles are supplied by the separate
    /// source-material adapter; this method never creates inspection materials.
    pub fn stage<M: Material>(
        self,
        world: &mut World,
        materials: &BTreeMap<String, Handle<M>>,
    ) -> Result<ResidentBoat<M>, String> {
        self.stage_instances(world, materials, &[])
    }

    pub fn stage_authored<M: Material>(
        self,
        world: &mut World,
        materials: &BTreeMap<String, Handle<M>>,
        placements: &[ManualPlacement],
    ) -> Result<ResidentBoat<M>, String> {
        if placements.is_empty() {
            return Err("No authored boat placements to stage".into());
        }
        self.stage_instances(world, materials, placements)
    }

    fn stage_instances<M: Material>(
        self,
        world: &mut World,
        materials: &BTreeMap<String, Handle<M>>,
        placements: &[ManualPlacement],
    ) -> Result<ResidentBoat<M>, String> {
        if placements.len() > 64 {
            return Err("Boat placement limit exceeded".into());
        }
        let mut seen = std::collections::BTreeSet::new();
        for placement in placements {
            if placement.asset_id != self.asset_id
                || placement.asset_hash != self.revision
                || placement.transform.scale != forge_core::Vec3::ONE
                || !seen.insert(placement.id)
            {
                return Err("Boat placement identity, revision, scale or duplicate differs".into());
            }
            placement
                .transform
                .validate()
                .map_err(|error| error.to_string())?;
        }
        if !world.contains_resource::<Assets<Mesh>>() {
            return Err("Boat mesh store is not installed".into());
        }
        let store = world
            .get_resource::<Assets<M>>()
            .ok_or("Boat material store is not installed")?;
        for part in &self.parts {
            if materials
                .get(&part.source.material)
                .is_none_or(|handle| !store.contains(handle.id()))
            {
                return Err(format!(
                    "Boat material is not resident: {}",
                    part.source.material
                ));
            }
        }
        let mesh_fingerprints = self
            .parts
            .iter()
            .map(|part| mesh_fingerprint(&part.mesh))
            .collect::<Result<_, _>>()?;
        // All fallible validation precedes the first allocation or ECS mutation.
        let meshes: Vec<_> = self
            .parts
            .iter()
            .map(|part| world.resource_mut::<Assets<Mesh>>().add(part.mesh.clone()))
            .collect();
        let mut resident = ResidentBoat {
            identity: self.identity(),
            instances: Vec::new(),
            meshes,
            mesh_fingerprints,
            hull_geometry: self.hull.clone(),
            hull_transform: self.hull_transform,
            triangles: self.triangles,
            marker: PhantomData,
        };
        let placements: Vec<_> = if placements.is_empty() {
            vec![None]
        } else {
            placements.iter().map(Some).collect()
        };
        for placement in placements {
            let instance = self.stage_instance(world, &resident, materials, placement);
            resident.instances.push(instance);
        }
        Ok(resident)
    }

    fn stage_instance<M: Material>(
        &self,
        world: &mut World,
        resident: &ResidentBoat<M>,
        materials: &BTreeMap<String, Handle<M>>,
        placement: Option<&ManualPlacement>,
    ) -> Instance<M> {
        let transform = placement.map_or(self.canonical, |p| {
            crate::domain_transform_to_bevy(p.transform)
        });
        let root = world
            .spawn((
                Name::new(self.source_id.clone()),
                resident.identity.clone(),
                transform,
                Visibility::Inherited,
            ))
            .id();
        let owner = BoatProjectionOwner {
            root,
            asset_id: self.asset_id,
            revision: self.revision,
            placement: placement.map(|p| p.id),
        };
        world.entity_mut(root).insert(owner.clone());
        let (entities, nodes) = self.stage_nodes(world, root, &owner);
        let parts = self.stage_parts(world, &entities, &resident.meshes, materials, placement);
        let source_root = entities[self.source_root];
        let colliders = self.stage_colliders(world, source_root, placement, &owner);
        let hull_identity = BoatWaterExclusion {
            owner: root,
            asset_id: self.asset_id,
            revision: self.revision,
            placement: placement.map(|p| p.id),
            source_node: self.nodes[self.hull_parent].id.clone(),
            source_volume: self.hull_id.clone(),
        };
        let hull = world
            .spawn((
                Name::new(self.hull_id.clone()),
                HullVolume(self.hull.clone()),
                hull_identity.clone(),
                self.hull_transform,
                ChildOf(entities[self.hull_parent]),
                owner.clone(),
            ))
            .id();
        if let Some(placement) = placement {
            world.entity_mut(root).insert((
                ManualPlacementProjection(placement.clone()),
                ManualPlacementCollisionExpected {
                    placement: placement.id,
                    primitives: colliders.iter().map(|c| c.entity).collect(),
                },
            ));
        }
        Instance {
            root,
            transform,
            placement: placement.cloned(),
            nodes,
            parts,
            colliders,
            hull,
            hull_parent: entities[self.hull_parent],
            hull_identity,
            owner,
        }
    }

    fn stage_nodes(
        &self,
        world: &mut World,
        root: Entity,
        owner: &BoatProjectionOwner,
    ) -> (Vec<Entity>, Vec<ResidentNode>) {
        let entities: Vec<_> = self
            .nodes
            .iter()
            .map(|node| {
                world
                    .spawn((
                        Name::new(node.id.clone()),
                        BoatSourceNode(node.id.clone()),
                        node.transform,
                        node.visible,
                        owner.clone(),
                    ))
                    .id()
            })
            .collect();
        let nodes = self
            .nodes
            .iter()
            .enumerate()
            .map(|(i, node)| {
                let parent = node.parent.map_or(root, |index| entities[index]);
                world.entity_mut(entities[i]).insert(ChildOf(parent));
                ResidentNode {
                    entity: entities[i],
                    parent,
                    id: node.id.clone(),
                    transform: node.transform,
                    visible: node.visible,
                }
            })
            .collect();
        (entities, nodes)
    }

    fn stage_parts<M: Material>(
        &self,
        world: &mut World,
        entities: &[Entity],
        meshes: &[Handle<Mesh>],
        materials: &BTreeMap<String, Handle<M>>,
        placement: Option<&ManualPlacement>,
    ) -> Vec<ResidentPart<M>> {
        let mut parts = Vec::with_capacity(self.parts.len());
        for (i, part) in self.parts.iter().enumerate() {
            let entity = entities[part.node];
            let material = materials[&part.source.material].clone();
            let mesh = meshes[i].clone();
            world.entity_mut(entity).insert((
                Mesh3d(mesh.clone()),
                MeshMaterial3d(material.clone()),
                part.source.clone(),
            ));
            if !part.source.cast_shadow {
                world.entity_mut(entity).insert(NotShadowCaster);
            }
            if let Some(placement) = placement {
                world
                    .entity_mut(entity)
                    .insert(ManualPlacementPart(placement.id));
            }
            parts.push(ResidentPart {
                entity,
                mesh,
                material,
                source: part.source.clone(),
            });
        }
        parts
    }

    fn stage_colliders(
        &self,
        world: &mut World,
        source_root: Entity,
        placement: Option<&ManualPlacement>,
        owner: &BoatProjectionOwner,
    ) -> Vec<ResidentCollider> {
        self.colliders
            .iter()
            .map(|collider| {
                let entity = world
                    .spawn((
                        collider.shape.clone(),
                        collider.transform,
                        collider.source.clone(),
                        owner.clone(),
                        ChildOf(source_root),
                    ))
                    .id();
                if let Some(placement) = placement {
                    world
                        .entity_mut(entity)
                        .insert(ManualPlacementCollision(placement.id));
                }
                ResidentCollider {
                    entity,
                    parent: source_root,
                    transform: collider.transform,
                    shape: collider.shape.clone(),
                    source: collider.source.clone(),
                }
            })
            .collect()
    }
}

impl<M: Material> ResidentBoat<M> {
    #[must_use]
    pub fn render_parts(&self) -> Vec<Entity> {
        self.instances
            .iter()
            .flat_map(|i| i.parts.iter().map(|p| p.entity))
            .collect()
    }

    #[must_use]
    pub fn authored_roots(&self) -> Vec<(Entity, EntityId, ForgeTransform)> {
        self.instances
            .iter()
            .filter_map(|i| i.placement.as_ref().map(|p| (i.root, p.id, p.transform)))
            .collect()
    }

    #[must_use]
    pub fn authored_parts(&self, id: EntityId) -> Option<Vec<Entity>> {
        self.instances
            .iter()
            .find(|i| i.placement.as_ref().is_some_and(|p| p.id == id))
            .map(|i| i.parts.iter().map(|p| p.entity).collect())
    }

    #[must_use]
    pub fn water_exclusions(&self) -> Vec<Entity> {
        self.instances.iter().map(|i| i.hull).collect()
    }

    /// Check live components/handles/hierarchy, never cached flags or the mere
    /// presence of a hull. Physics readiness is checked separately below.
    pub fn census(&self, world: &World) -> Result<BoatCensus, String> {
        if self.instances.is_empty() {
            return Err("Boat residency has no instances".into());
        }
        let meshes = world
            .get_resource::<Assets<Mesh>>()
            .ok_or("Boat mesh store disappeared")?;
        let materials = world
            .get_resource::<Assets<M>>()
            .ok_or("Boat material store disappeared")?;
        for (handle, expected) in self.meshes.iter().zip(&self.mesh_fingerprints) {
            if mesh_fingerprint(meshes.get(handle).ok_or("Boat mesh disappeared")?)? != *expected {
                return Err("Boat source mesh payload changed".into());
            }
        }
        for instance in &self.instances {
            instance.verify(world, self, meshes, materials)?;
        }
        Ok(BoatCensus {
            instances: self.instances.len(),
            source_nodes: self.instances.iter().map(|i| i.nodes.len()).sum(),
            render_parts: self.instances.iter().map(|i| i.parts.len()).sum(),
            collision_volumes: self.instances.iter().map(|i| i.colliders.len()).sum(),
            water_exclusions: self.instances.len(),
            triangles: self.triangles * self.instances.len(),
        })
    }

    #[must_use]
    pub fn is_synchronized(&self, world: &World) -> bool {
        self.census(world).is_ok()
            && self.instances.iter().all(|i| {
                i.colliders.iter().all(|c| {
                    world
                        .get::<crate::ManualPlacementCollisionFailed>(c.entity)
                        .is_none()
                        && crate::physics::static_collider_ready(world, c.entity)
                })
            })
    }

    /// Retire only entities and mesh handles allocated by this residency.
    /// Shared materials and unrelated scene hulls remain owned by their callers.
    pub fn retire(self, world: &mut World) {
        for instance in self.instances {
            let owned: Vec<_> = instance
                .nodes
                .iter()
                .map(|n| n.entity)
                .chain(instance.colliders.iter().map(|c| c.entity))
                .chain([instance.hull, instance.root])
                .collect();
            // Preserve foreign children even if another adapter reparented them
            // under our hierarchy; also retire our own reparented entities.
            for &entity in &owned {
                let foreign: Vec<_> = world
                    .get::<Children>(entity)
                    .into_iter()
                    .flat_map(bevy::ecs::relationship::RelationshipTarget::iter)
                    .filter(|child| !owned.contains(child))
                    .collect();
                for child in foreign {
                    world.entity_mut(child).remove::<ChildOf>();
                }
            }
            for entity in owned {
                if world.get_entity(entity).is_ok() {
                    world.despawn(entity);
                }
            }
        }
        let adopted: std::collections::HashSet<_> = world
            .query::<&Mesh3d>()
            .iter(world)
            .map(|mesh| mesh.0.id())
            .collect();
        if let Some(mut meshes) = world.get_resource_mut::<Assets<Mesh>>() {
            for mesh in self.meshes {
                if !adopted.contains(&mesh.id()) {
                    meshes.remove(mesh.id());
                }
            }
        }
    }
}

impl<M: Material> Instance<M> {
    fn verify(
        &self,
        world: &World,
        resident: &ResidentBoat<M>,
        meshes: &Assets<Mesh>,
        materials: &Assets<M>,
    ) -> Result<(), String> {
        if world.get::<BoatInstanceIdentity>(self.root) != Some(&resident.identity)
            || world.get::<BoatProjectionOwner>(self.root) != Some(&self.owner)
            || world.get::<Transform>(self.root) != Some(&self.transform)
            || world.get::<ChildOf>(self.root).is_some()
        {
            return Err("Boat instance root changed".into());
        }
        match &self.placement {
            Some(placement) => {
                if world
                    .get::<ManualPlacementProjection>(self.root)
                    .is_none_or(|p| &p.0 != placement)
                    || world
                        .get::<ManualPlacementCollisionExpected>(self.root)
                        .is_none_or(|e| {
                            e.placement != placement.id
                                || e.primitives
                                    != self.colliders.iter().map(|c| c.entity).collect::<Vec<_>>()
                        })
                {
                    return Err("Boat authored projection changed".into());
                }
            }
            None if world.get::<ManualPlacementProjection>(self.root).is_some() => {
                return Err("Reference boat gained an authored identity".into());
            }
            None => {}
        }
        for node in &self.nodes {
            if !parent_is(world, node.entity, node.parent)
                || world.get::<BoatProjectionOwner>(node.entity) != Some(&self.owner)
                || world
                    .get::<BoatSourceNode>(node.entity)
                    .is_none_or(|n| n.0 != node.id)
                || world.get::<Transform>(node.entity) != Some(&node.transform)
                || world.get::<Visibility>(node.entity) != Some(&node.visible)
            {
                return Err("Boat source node changed".into());
            }
        }
        self.verify_parts(world, meshes, materials)?;
        self.verify_colliders(world)?;
        if !parent_is(world, self.hull, self.hull_parent)
            || world.get::<BoatProjectionOwner>(self.hull) != Some(&self.owner)
            || world.get::<Transform>(self.hull) != Some(&resident.hull_transform)
            || world.get::<BoatWaterExclusion>(self.hull) != Some(&self.hull_identity)
            || world
                .get::<HullVolume>(self.hull)
                .is_none_or(|h| !Arc::ptr_eq(&h.0, &resident.hull_geometry))
        {
            return Err("Boat water-exclusion ownership or geometry changed".into());
        }
        self.check_inventory(world)?;
        Ok(())
    }

    fn verify_parts(
        &self,
        world: &World,
        meshes: &Assets<Mesh>,
        materials: &Assets<M>,
    ) -> Result<(), String> {
        for part in &self.parts {
            if world.get::<BoatPart>(part.entity) != Some(&part.source)
                || world
                    .get::<Mesh3d>(part.entity)
                    .is_none_or(|h| h.0 != part.mesh)
                || world
                    .get::<MeshMaterial3d<M>>(part.entity)
                    .is_none_or(|h| h.0 != part.material)
                || !meshes.contains(part.mesh.id())
                || !materials.contains(part.material.id())
                || world.get::<NotShadowCaster>(part.entity).is_some() == part.source.cast_shadow
                || self.placement.as_ref().is_some_and(|p| {
                    world
                        .get::<ManualPlacementPart>(part.entity)
                        .is_none_or(|marker| marker.0 != p.id)
                })
            {
                return Err("Boat render projection changed".into());
            }
        }
        Ok(())
    }

    fn verify_colliders(&self, world: &World) -> Result<(), String> {
        for collider in &self.colliders {
            if !parent_is(world, collider.entity, collider.parent)
                || world.get::<Sensor>(collider.entity).is_some()
                || world.get::<BoatProjectionOwner>(collider.entity) != Some(&self.owner)
                || world.get::<Transform>(collider.entity) != Some(&collider.transform)
                || world.get::<BoatCollisionVolume>(collider.entity) != Some(&collider.source)
                || world
                    .get::<Collider>(collider.entity)
                    .is_none_or(|c| !std::ptr::eq(c.raw.as_ref(), collider.shape.raw.as_ref()))
                || self.placement.as_ref().is_some_and(|p| {
                    world
                        .get::<ManualPlacementCollision>(collider.entity)
                        .is_none_or(|m| m.0 != p.id)
                })
            {
                return Err("Boat collision projection changed".into());
            }
        }
        Ok(())
    }

    fn check_inventory(&self, world: &World) -> Result<(), String> {
        let expected: Vec<_> = self
            .nodes
            .iter()
            .map(|n| n.entity)
            .chain(self.colliders.iter().map(|c| c.entity))
            .chain([self.hull, self.root])
            .collect();
        let mut pending = vec![self.root];
        let mut seen = std::collections::HashSet::new();
        while let Some(entity) = pending.pop() {
            if !seen.insert(entity) || seen.len() > 4096 {
                return Err("Boat descendant inventory is cyclic or oversized".into());
            }
            if expected.contains(&entity) {
                let node = self.nodes.iter().any(|n| n.entity == entity);
                let part = self.parts.iter().any(|p| p.entity == entity);
                let collider = self.colliders.iter().any(|c| c.entity == entity);
                let hull = self.hull == entity;
                if (
                    world.get::<BoatSourceNode>(entity).is_some(),
                    world.get::<BoatPart>(entity).is_some(),
                    world.get::<BoatCollisionVolume>(entity).is_some(),
                    world.get::<BoatWaterExclusion>(entity).is_some(),
                    world.get::<HullVolume>(entity).is_some(),
                    world.get::<Mesh3d>(entity).is_some(),
                    world.get::<Collider>(entity).is_some(),
                ) != (node, part, collider, hull, hull, part, collider)
                {
                    return Err("Boat projection roles changed on a tracked entity".into());
                }
            } else if world.get::<BoatProjectionOwner>(entity).is_some()
                || world.get::<BoatSourceNode>(entity).is_some()
                || world.get::<BoatPart>(entity).is_some()
                || world.get::<BoatCollisionVolume>(entity).is_some()
                || world.get::<BoatWaterExclusion>(entity).is_some()
                || world.get::<HullVolume>(entity).is_some()
            {
                return Err("Untracked boat projection or hull below the instance".into());
            }
            if let Some(children) = world.get::<Children>(entity) {
                pending.extend(children.iter());
            }
        }
        Ok(())
    }
}

fn parent_is(world: &World, entity: Entity, parent: Entity) -> bool {
    world
        .get::<ChildOf>(entity)
        .is_some_and(|p| p.parent() == parent)
}
#[expect(
    clippy::cast_possible_truncation,
    reason = "Bevy coordinates use f32; the source-to-renderer conversion checks both values for finiteness"
)]
fn finite(value: f64) -> Result<f32, String> {
    let narrowed = value as f32;
    if value.is_finite() && narrowed.is_finite() {
        Ok(narrowed)
    } else {
        Err("Non-finite boat coordinate".into())
    }
}
fn vector(values: [f64; 3]) -> Result<Vec3, String> {
    Ok(Vec3::new(
        finite(values[0])?,
        finite(values[1])?,
        finite(values[2])?,
    ))
}
fn local_frame(frame: &AssetLocalFrame) -> Result<Transform, String> {
    frame.validate()?;
    let r = frame.rotation_xyzw;
    Ok(Transform {
        translation: vector(frame.translation_m)?,
        rotation: Quat::from_xyzw(finite(r[0])?, finite(r[1])?, finite(r[2])?, finite(r[3])?),
        scale: vector(frame.scale)?,
    })
}
fn attribute<const N: usize>(part: &ResolvedBoatPart, name: &str) -> Result<Vec<[f32; N]>, String> {
    let values = part
        .attributes
        .get(name)
        .ok_or_else(|| format!("Boat attribute absent: {name}"))?;
    let (chunks, tail) = values.as_chunks::<N>();
    if !tail.is_empty() {
        return Err("Boat attribute width differs".into());
    }
    Ok(chunks.to_vec())
}
fn source_mesh(part: ResolvedBoatPart) -> Result<Mesh, String> {
    Ok(Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    )
    .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, attribute::<3>(&part, "position")?)
    .with_inserted_attribute(Mesh::ATTRIBUTE_NORMAL, attribute::<3>(&part, "normal")?)
    .with_inserted_attribute(Mesh::ATTRIBUTE_UV_0, attribute::<2>(&part, "uv")?)
    .with_inserted_attribute(TINT, attribute::<3>(&part, "color")?)
    .with_inserted_attribute(DATA, attribute::<4>(&part, "aux")?)
    .with_inserted_indices(Indices::U32(part.indices)))
}

fn mesh_fingerprint(mesh: &Mesh) -> Result<ContentHash, String> {
    if mesh.primitive_topology() != PrimitiveTopology::TriangleList {
        return Err("Boat mesh primitive topology changed".into());
    }
    let mut bytes = Vec::new();
    for (attribute, width) in [
        (Mesh::ATTRIBUTE_POSITION, 3),
        (Mesh::ATTRIBUTE_NORMAL, 3),
        (Mesh::ATTRIBUTE_UV_0, 2),
        (TINT, 3),
        (DATA, 4),
    ] {
        let values: Vec<_> = match (width, mesh.attribute(attribute)) {
            (3, Some(VertexAttributeValues::Float32x3(values))) => {
                values.iter().flatten().copied().collect()
            }
            (2, Some(VertexAttributeValues::Float32x2(values))) => {
                values.iter().flatten().copied().collect()
            }
            (4, Some(VertexAttributeValues::Float32x4(values))) => {
                values.iter().flatten().copied().collect()
            }
            _ => return Err("Boat mesh attribute layout changed".into()),
        };
        bytes.extend_from_slice(&(values.len() as u64).to_le_bytes());
        for value in values {
            bytes.extend_from_slice(&value.to_le_bytes());
        }
    }
    let Some(Indices::U32(indices)) = mesh.indices() else {
        return Err("Boat mesh index layout changed".into());
    };
    bytes.extend_from_slice(&(indices.len() as u64).to_le_bytes());
    for index in indices {
        bytes.extend_from_slice(&index.to_le_bytes());
    }
    Ok(forge_core::bytes_sha256(&bytes))
}

fn prepare_colliders(
    source: &[forge_assets::tidewater_boat::BoatCollider],
) -> Result<Vec<PreparedCollider>, String> {
    let mut colliders = Vec::with_capacity(source.len());
    for collider in source {
        let half = vector(collider.half_extents_m)?;
        if half.min_element() <= 0.0 || !(collider.walkable || collider.solid) {
            return Err("Boat collision volume has no supported static surface".into());
        }
        colliders.push(PreparedCollider {
            transform: Transform::from_translation(vector(collider.center_m)?),
            shape: Collider::cuboid(half.x, half.y, half.z),
            source: BoatCollisionVolume {
                id: collider.id.clone(),
                tag: collider.tag.clone(),
                walkable: collider.walkable,
                solid: collider.solid,
            },
        });
    }
    Ok(colliders)
}
