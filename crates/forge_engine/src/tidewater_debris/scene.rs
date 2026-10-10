//! Disposable render projection of accepted common placements, with exact caster subsets.
//! Collision waivers require the verified source sidecar, never an absent primitive list.
use super::{DebrisSurface, PreparedDebris, nature::NatureMaterial};
use crate::{
    ManualPlacementCollision, ManualPlacementCollisionExpected,
    ManualPlacementCollisionNotRequired, ManualPlacementPart, ManualPlacementProjection,
    tidewater_material::VillageMaterial, tidewater_object_shadow::OpaqueDraw,
};
use bevy::{light::NotShadowCaster, mesh::VertexAttributeValues, prelude::*};
use forge_core::AssetId;
use forge_project::zone_authoring::placements::{ManualPlacement, PlacementChanges};
use std::collections::BTreeMap;

#[cfg(test)]
mod tests;

pub struct ResidentMaterials {
    pub wood: Handle<VillageMaterial>,
    pub hard: Handle<VillageMaterial>,
    pub fabric: Handle<VillageMaterial>,
    pub nature: Handle<NatureMaterial>,
}

#[derive(Clone)]
enum MaterialHandle {
    Village(Handle<VillageMaterial>),
    Nature(Handle<NatureMaterial>),
}

struct Part {
    mesh: Mesh,
    shadow: Option<Mesh>,
    material: MaterialHandle,
}

pub struct PreparedScene {
    templates: BTreeMap<AssetId, Vec<Part>>,
    placements: Vec<ManualPlacement>,
    collisions: BTreeMap<AssetId, Vec<super::collisions::PreparedPrimitive>>,
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
    /// Resolve every accepted reference/material before allocating meshes or entities.
    ///
    /// # Errors
    /// Rejects invalid common placements, wrong revisions, absent assets and excess geometry.
    pub fn new(
        geometry: &PreparedDebris,
        placements: &[ManualPlacement],
        materials: &ResidentMaterials,
        world: &World,
    ) -> Result<Self, String> {
        PlacementChanges::between(&[], placements)?;
        let village = world
            .get_resource::<Assets<VillageMaterial>>()
            .ok_or("Debris Village materials unavailable")?;
        let nature = world
            .get_resource::<Assets<NatureMaterial>>()
            .ok_or("Debris Nature materials unavailable")?;
        for (handle, kind) in [
            (&materials.wood, 0),
            (&materials.hard, 1),
            (&materials.fabric, 5),
        ] {
            if village
                .get(handle)
                .is_none_or(|material| material.kind != kind)
            {
                return Err("Debris shared material role unresolved".into());
            }
        }
        if nature.get(&materials.nature).is_none() {
            return Err("Debris Nature material unresolved".into());
        }
        let shaders = world
            .get_resource::<Assets<Shader>>()
            .ok_or("Debris shaders unavailable")?;
        if shaders
            .get(&crate::tidewater_material::VILLAGE_SHADER)
            .is_none()
            || shaders.get(&super::nature::SHADER).is_none()
            || world.get_resource::<Assets<Mesh>>().is_none()
        {
            return Err("Debris renderer resources unresolved".into());
        }
        let mut templates = BTreeMap::new();
        let mut triangles = 0;
        let mut parts_count = 0;
        for placement in placements {
            if placement.asset_hash != forge_assets::tidewater_debris::package_hash() {
                return Err("Debris placement revision differs".into());
            }
            let object = geometry
                .objects
                .iter()
                .find(|object| object.asset_id == placement.asset_id)
                .ok_or("Debris placement prototype absent")?;
            triangles += object
                .parts
                .iter()
                .map(|part| part.mesh.indices().map_or(0, |indices| indices.len() / 3))
                .sum::<usize>();
            parts_count += object.parts.len();
            if triangles > 2_000_000 || parts_count > 16_384 {
                return Err("Debris scene geometry budget exceeded".into());
            }
            templates.entry(object.asset_id).or_insert_with(|| {
                object
                    .parts
                    .iter()
                    .map(|part| Part {
                        mesh: part.mesh.clone(),
                        shadow: part.shadow_mesh.clone(),
                        material: match part.surface {
                            DebrisSurface::Wood => MaterialHandle::Village(materials.wood.clone()),
                            DebrisSurface::Hard => MaterialHandle::Village(materials.hard.clone()),
                            DebrisSurface::Fabric => {
                                MaterialHandle::Village(materials.fabric.clone())
                            }
                            DebrisSurface::Nature => {
                                MaterialHandle::Nature(materials.nature.clone())
                            }
                        },
                    })
                    .collect()
            });
        }
        Ok(Self {
            templates,
            placements: placements.to_vec(),
            collisions: BTreeMap::new(),
        })
    }

    /// Attach hash-verified source primitives before adoption. Empty source owners are explicit.
    ///
    /// # Errors
    /// Rejects a missing owner or a collision frame that differs from the mesh prototype.
    #[allow(
        clippy::float_cmp,
        reason = "Captured collision and mesh frames must match exactly; tolerance would admit different source geometry."
    )]
    pub fn with_collision_frames(
        mut self,
        geometry: &PreparedDebris,
        frames: &forge_assets::tidewater_debris::collisions::CollisionFrames,
    ) -> Result<Self, String> {
        for asset in self.templates.keys() {
            let object = geometry
                .objects
                .iter()
                .find(|object| object.asset_id == *asset)
                .ok_or("Debris collision prototype absent")?;
            let owner = frames
                .owner(&object.source_id)
                .ok_or("Debris collision owner absent")?;
            if owner.category != forge_assets::tidewater_debris::collisions::Category::Procedural
                || owner.source_frame.translation_m != object.source_translation_m
                || owner.source_frame.rotation_xyzw != [0.0, 0.0, 0.0, 1.0]
                || owner.source_frame.scale != [1.0; 3]
            {
                return Err("Debris collision and mesh frames differ".into());
            }
            self.collisions
                .insert(*asset, super::collisions::prepare(owner)?);
        }
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
                    MaterialHandle::Village(handle) => {
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
            let Some(VertexAttributeValues::Float32x3(positions)) =
                mesh.attribute(Mesh::ATTRIBUTE_POSITION)
            else {
                return Err("Debris caster positions differ".into());
            };
            let positions = positions
                .iter()
                .flatten()
                .flat_map(|value| value.to_le_bytes())
                .collect();
            let indices = mesh
                .indices()
                .ok_or("Debris caster indices absent")?
                .iter()
                .map(|index| u32::try_from(index).map_err(|error| error.to_string()))
                .collect::<Result<Vec<_>, _>>()?
                .into_iter()
                .flat_map(u32::to_le_bytes)
                .collect();
            draws.push(OpaqueDraw::new(positions, indices, model)?);
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
