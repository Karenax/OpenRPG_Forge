//! Atomic disposable building placements with static triangle-surface collisions.
use super::ResidentMeshes;
use crate::{
    ManualPlacementCollision, ManualPlacementCollisionExpected, ManualPlacementPart,
    ManualPlacementProjection, tidewater_material::VillageMaterial,
};
use bevy::{light::NotShadowCaster, mesh::VertexAttributeValues, prelude::*};
use bevy_rapier3d::prelude::Collider;
use forge_project::zone_authoring::placements::{ManualPlacement, PlacementChanges};
use std::collections::BTreeMap;

#[derive(Clone)]
struct Part {
    mesh: Handle<Mesh>,
    material: Handle<VillageMaterial>,
    collider: Collider,
    triangles: usize,
}
struct Placement {
    source: ManualPlacement,
    parts: Vec<Part>,
}
#[derive(Default)]
pub struct PreparedScene {
    placements: Vec<Placement>,
}

pub struct Instance {
    pub source: ManualPlacement,
    pub root: Entity,
    pub parts: Vec<Entity>,
}
#[derive(Resource, Default)]
pub struct ResidentScene {
    pub instances: Vec<Instance>,
    retired: Vec<(Entity, Entity)>,
}

impl PreparedScene {
    /// Resolve all revisions, materials and triangle data before replacing any entity.
    pub fn new(
        sources: &[ManualPlacement],
        geometry: &ResidentMeshes,
        materials: &BTreeMap<String, Handle<VillageMaterial>>,
        meshes: &Assets<Mesh>,
        material_assets: &Assets<VillageMaterial>,
    ) -> Result<Self, String> {
        PlacementChanges::between(&[], sources)?;
        let mut templates = BTreeMap::new();
        let mut placements = Vec::with_capacity(sources.len());
        let mut triangles = 0;
        let mut parts_count = 0;
        for source in sources {
            let key = (source.asset_id, source.asset_hash);
            if let std::collections::btree_map::Entry::Vacant(entry) = templates.entry(key) {
                let parts = geometry
                    .resolve(key.0, key.1)?
                    .iter()
                    .map(|part| {
                        let material = materials
                            .get(&part.material)
                            .ok_or("Building material missing")?;
                        if material_assets.get(material).is_none() {
                            return Err("Building material unresolved".into());
                        }
                        let mesh = meshes.get(&part.mesh).ok_or("Building mesh unresolved")?;
                        let (collider, triangles) = surface(mesh)?;
                        Ok(Part {
                            mesh: part.mesh.clone(),
                            material: material.clone(),
                            collider,
                            triangles,
                        })
                    })
                    .collect::<Result<Vec<_>, String>>()?;
                entry.insert(parts);
            }
            let parts = templates.get(&key).ok_or("Building template missing")?;
            triangles += parts.iter().map(|p| p.triangles).sum::<usize>();
            parts_count += parts.len();
            if triangles > 2_000_000 || parts_count > 16_384 {
                return Err("Imported building collision budget exceeded".into());
            }
            placements.push(Placement {
                source: source.clone(),
                parts: parts.clone(),
            });
        }
        Ok(Self { placements })
    }

    /// Derived resources only. Canonical documents and undo history are untouched.
    pub fn stage(self, world: &mut World) {
        let mut instances = Vec::with_capacity(self.placements.len());
        for placement in self.placements {
            let source = placement.source;
            let root = world
                .spawn((
                    Name::new(source.name.clone()),
                    crate::domain_transform_to_bevy(source.transform),
                    Visibility::Inherited,
                    ManualPlacementProjection(source.clone()),
                ))
                .id();
            let parts = placement
                .parts
                .into_iter()
                .map(|part| {
                    world
                        .spawn((
                            Mesh3d(part.mesh),
                            MeshMaterial3d(part.material),
                            Transform::IDENTITY,
                            Visibility::Inherited,
                            NotShadowCaster,
                            ManualPlacementPart(source.id),
                            ManualPlacementCollision(source.id),
                            part.collider,
                            ChildOf(root),
                        ))
                        .id()
                })
                .collect::<Vec<_>>();
            world
                .entity_mut(root)
                .insert(ManualPlacementCollisionExpected {
                    placement: source.id,
                    primitives: parts.clone(),
                });
            instances.push(Instance {
                source,
                root,
                parts,
            });
        }
        let mut retired = Vec::new();
        if let Some(previous) = world.remove_resource::<ResidentScene>() {
            retired = previous.retired;
            for instance in previous.instances {
                for &part in &instance.parts {
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
    /// Live synchronization only; shader compilation and presented pixels are separate.
    pub fn is_synchronized(&self, world: &World) -> bool {
        self.retired
            .iter()
            .all(|&(context, part)| !registered(world, context, part))
            && self.instances.iter().all(|instance| {
                world
                    .get::<ManualPlacementProjection>(instance.root)
                    .is_some_and(|p| p.0 == instance.source)
                    && world.get::<Transform>(instance.root)
                        == Some(&crate::domain_transform_to_bevy(instance.source.transform))
                    && world.get::<ChildOf>(instance.root).is_none()
                    && !instance.parts.is_empty()
                    && instance.parts.iter().all(|&entity| {
                        world
                            .get::<ChildOf>(entity)
                            .is_some_and(|parent| parent.parent() == instance.root)
                            && world
                                .get::<ManualPlacementCollision>(entity)
                                .is_some_and(|p| p.0 == instance.source.id)
                            && crate::physics::static_collider_ready(world, entity)
                    })
            })
    }
}
fn registered(world: &World, context: Entity, part: Entity) -> bool {
    world
        .get::<bevy_rapier3d::plugin::context::RapierContextColliders>(context)
        .is_some_and(|colliders| colliders.entity2collider().contains_key(&part))
}

fn surface(mesh: &Mesh) -> Result<(Collider, usize), String> {
    let Some(VertexAttributeValues::Float32x3(positions)) =
        mesh.attribute(Mesh::ATTRIBUTE_POSITION)
    else {
        return Err("Building collision requires float positions".into());
    };
    let indices = mesh
        .indices()
        .ok_or("Building collision requires indices")?
        .iter()
        .map(|index| u32::try_from(index).map_err(|e| e.to_string()))
        .collect::<Result<Vec<_>, _>>()?;
    if positions.is_empty()
        || positions.iter().flatten().any(|v| !v.is_finite())
        || indices.is_empty()
        || !indices.len().is_multiple_of(3)
        || indices.iter().any(|&i| i as usize >= positions.len())
    {
        return Err("Invalid building collision triangles".into());
    }
    let count = indices.len() / 3;
    let triangles = indices.as_chunks::<3>().0.to_vec();
    Collider::trimesh(
        positions.iter().copied().map(Vec3::from_array).collect(),
        triangles,
    )
    .map(|c| (c, count))
    .map_err(|e| e.to_string())
}
