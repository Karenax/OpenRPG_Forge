//! Owned canonical imported-plant proxies, independent of graphics LOD/residency.
use super::{BodyEnvelope, body_envelopes, manual_readiness::static_collider_activation_ready};
use bevy::prelude::*;
use bevy_rapier3d::plugin::context::DefaultRapierContext;
use bevy_rapier3d::prelude::{
    Collider, ColliderDisabled, ColliderScale, CollisionGroups, PhysicsSet, RapierColliderHandle,
    RapierContextColliders, RapierContextEntityLink, RigidBody, Sensor,
};
use forge_core::{ContentHash, EntityId};
use forge_world::imported_vegetation::streaming::StreamedPlantSelection;
use std::{collections::BTreeMap, sync::Arc};

mod prepare;
#[cfg(test)]
mod tests;

pub const MAX_IMPORTED_COLLIDERS: usize = 100_000;
const MAX_DERIVED_HULL_VERTICES: usize = 1_000_000;

struct Body {
    transform: Transform,
    collider: Collider,
    base: Vec3,
    reach: f32,
    activation: f32,
}

pub struct Prepared {
    identity: ContentHash,
    head: ContentHash,
    terrain: ContentHash,
    bodies: BTreeMap<EntityId, Body>,
}

impl Prepared {
    /// Consume the complete collision selection from the canonical world adapter.
    /// Validation and bounded shape construction finish before any ECS mutation.
    pub fn from_selection(selection: &StreamedPlantSelection<'_>) -> Result<Self, String> {
        prepare::prepare(selection)
    }
}

#[derive(Resource, Default)]
struct Owners {
    groups: BTreeMap<EntityId, Projection>,
    retired: Vec<Retired>,
}

struct Projection {
    prepared: Arc<Prepared>,
    entities: BTreeMap<EntityId, Entity>,
}

struct Retired {
    entity: Entity,
    backend: Option<(Entity, RapierColliderHandle)>,
}

/// Derived ownership only; no renderer or shape data is serialized into authoring.
#[derive(Component, Clone, Copy, PartialEq)]
pub struct ImportedPlantCollider {
    owner: EntityId,
    id: EntityId,
    identity: ContentHash,
    base: Vec3,
    reach: f32,
    activation: f32,
}
impl ImportedPlantCollider {
    #[must_use]
    pub fn id(&self) -> EntityId {
        self.id
    }
}

pub struct Resident {
    owner: EntityId,
    prepared: Arc<Prepared>,
}

/// Match ascending canonical IDs with ascending resident IDs without restarting
/// a tree lookup for every nonresident plant. Missing and stray IDs remain
/// observable to the same ownership and activation checks as before.
fn next_ordered_resident(
    residents: &mut std::iter::Peekable<std::collections::btree_map::Iter<'_, EntityId, Entity>>,
    id: EntityId,
) -> Option<Entity> {
    while let Some((&candidate, &entity)) = residents.peek().copied() {
        match candidate.cmp(&id) {
            std::cmp::Ordering::Less => {
                residents.next();
            }
            std::cmp::Ordering::Equal => {
                residents.next();
                return Some(entity);
            }
            std::cmp::Ordering::Greater => return None,
        }
    }
    None
}

/// Cursor for ascending IDs when the caller may insert or remove the current
/// ID. Future IDs must not be changed while the cursor is in use.
struct MutableResidentCursor {
    next: Option<(EntityId, Entity)>,
}

impl MutableResidentCursor {
    fn new(residents: &BTreeMap<EntityId, Entity>) -> Self {
        Self {
            next: residents
                .first_key_value()
                .map(|(&id, &entity)| (id, entity)),
        }
    }

    fn take(&mut self, residents: &BTreeMap<EntityId, Entity>, id: EntityId) -> Option<Entity> {
        while let Some((candidate, entity)) = self.next {
            if candidate > id {
                return None;
            }
            self.next = residents
                .range((
                    std::ops::Bound::Excluded(candidate),
                    std::ops::Bound::Unbounded,
                ))
                .next()
                .map(|(&next, &entity)| (next, entity));
            if candidate == id {
                return Some(entity);
            }
        }
        None
    }
}

impl Resident {
    pub fn stage(world: &mut World, owner: EntityId, prepared: Prepared) -> Result<Self, String> {
        if owner.as_uuid().is_nil()
            || world
                .get_resource::<Owners>()
                .is_some_and(|owners| owners.groups.contains_key(&owner))
            || world
                .query::<&ImportedPlantCollider>()
                .iter(world)
                .any(|body| body.owner == owner)
        {
            return Err("Imported collision owner is invalid or already staged".into());
        }
        world.init_resource::<Owners>();
        let envelopes = body_envelopes(world).unwrap_or_default();
        let prepared = Arc::new(prepared);
        let mut projection = Projection {
            prepared: Arc::clone(&prepared),
            entities: BTreeMap::new(),
        };
        // Reuse the existing conservative activation policy before backend admission.
        synchronize(world, owner, &mut projection, &envelopes, &mut Vec::new());
        world
            .resource_mut::<Owners>()
            .groups
            .insert(owner, projection);
        Ok(Self { owner, prepared })
    }

    #[must_use]
    pub fn identity(&self) -> ContentHash {
        self.prepared.identity
    }
    #[must_use]
    pub fn head(&self) -> ContentHash {
        self.prepared.head
    }
    #[must_use]
    pub fn terrain(&self) -> ContentHash {
        self.prepared.terrain
    }
    #[must_use]
    pub fn resident_count(&self, world: &World) -> usize {
        self.projection(world)
            .map_or(0, |group| group.entities.len())
    }
    /// Complete canonical population, independent of the current physical residence.
    #[must_use]
    pub fn source_count(&self) -> usize {
        self.prepared.bodies.len()
    }
    #[must_use]
    pub fn is_empty(&self) -> bool {
        self.prepared.bodies.is_empty()
    }
    #[must_use]
    pub fn matches(&self, selection: &StreamedPlantSelection<'_>) -> bool {
        prepare::identity(selection).is_ok_and(|identity| identity == self.prepared.identity)
    }

    /// Checks owned shapes/poses and the real default backend, including activation.
    /// An empty canonical collision selection certifies no other scene physics.
    pub fn ready(&self, world: &mut World) -> bool {
        if self.projection(world).is_none() {
            return false;
        }
        world.resource_scope(|world, mut owners: Mut<Owners>| prune_retired(world, &mut owners));
        if !world.resource::<Owners>().retired.is_empty() {
            return false;
        }
        let count = world
            .query::<&ImportedPlantCollider>()
            .iter(world)
            .filter(|body| body.owner == self.owner)
            .count();
        if count != self.resident_count(world) {
            return false;
        }
        if self.is_empty() {
            return true;
        }
        let valid_context = {
            let mut query =
                world.query_filtered::<Has<DefaultRapierContext>, With<RapierContextColliders>>();
            let mut contexts = query.iter(world);
            contexts.next() == Some(true) && contexts.next().is_none()
        };
        if !valid_context {
            return false;
        }
        let Ok(envelopes) = body_envelopes(world) else {
            return false;
        };
        let group = self.projection(world).expect("checked physical owner");
        let mut residents = group.entities.iter().peekable();
        self.prepared.bodies.iter().all(|(&id, body)| {
            let expected = marker(self.owner, self.prepared.identity, id, body);
            let Some(entity) = next_ordered_resident(&mut residents, id) else {
                return !active(&expected, &envelopes);
            };
            if !active(&expected, &envelopes) {
                return false;
            }
            world.get::<ImportedPlantCollider>(entity) == Some(&expected)
                && world.get::<Transform>(entity) == Some(&body.transform)
                && world.get::<ChildOf>(entity).is_none()
                && world.get::<RigidBody>(entity).is_none()
                && world.get::<Sensor>(entity).is_none()
                && world.get::<ColliderScale>(entity).is_none()
                && world.get::<CollisionGroups>(entity).is_none()
                && world.get::<ColliderDisabled>(entity).is_none()
                && world.get::<Collider>(entity).is_some_and(|actual| {
                    actual.scale() == Vec3::ONE
                        && std::ptr::eq(actual.raw.as_ref(), body.collider.raw.as_ref())
                })
                && static_collider_activation_ready(world, entity, true)
        })
    }

    fn projection<'a>(&self, world: &'a World) -> Option<&'a Projection> {
        world
            .get_resource::<Owners>()?
            .groups
            .get(&self.owner)
            .filter(|group| Arc::ptr_eq(&group.prepared, &self.prepared))
    }

    /// Retire exact owned entities; a missing/reassigned marker never authorizes deletion.
    pub fn retire(self, world: &mut World) -> Result<(), String> {
        if self.projection(world).is_none() {
            return Err("Imported collision ownership changed before retirement".into());
        }
        let count = world
            .query::<&ImportedPlantCollider>()
            .iter(world)
            .filter(|body| body.owner == self.owner)
            .count();
        let mut intact = count == self.resident_count(world);
        world.resource_scope(|world, mut owners: Mut<Owners>| {
            let group = owners.groups.remove(&self.owner).expect("checked owner");
            for (id, entity) in group.entities {
                let expected = marker(
                    self.owner,
                    self.prepared.identity,
                    id,
                    &self.prepared.bodies[&id],
                );
                if world.get::<ImportedPlantCollider>(entity) == Some(&expected) {
                    retire_entity(world, entity, &mut owners.retired);
                } else {
                    intact = false;
                }
            }
            prune_retired(world, &mut owners);
        });
        if intact {
            Ok(())
        } else {
            Err("Imported collision ownership changed before retirement".into())
        }
    }
}

fn marker(
    owner: EntityId,
    identity: ContentHash,
    id: EntityId,
    body: &Body,
) -> ImportedPlantCollider {
    ImportedPlantCollider {
        owner,
        id,
        identity,
        base: body.base,
        reach: body.reach,
        activation: body.activation,
    }
}

fn active(body: &ImportedPlantCollider, envelopes: &[BodyEnvelope]) -> bool {
    envelopes.is_empty()
        || envelopes.iter().any(|observer| {
            let start = observer.centre.xz().as_dvec2();
            let delta = observer.predicted.xz().as_dvec2() - start;
            let base = body.base.xz().as_dvec2();
            let t = if delta.length_squared() > 0.0 {
                ((base - start).dot(delta) / delta.length_squared()).clamp(0.0, 1.0)
            } else {
                0.0
            };
            let radius =
                f64::from(body.activation) + f64::from(body.reach) + f64::from(observer.radius);
            (base - (start + delta * t)).length_squared() <= radius * radius
        })
}

#[derive(SystemSet, Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct ImportedPlantActivationSet;

pub struct ImportedPlantCollisionPlugin;
impl Plugin for ImportedPlantCollisionPlugin {
    fn build(&self, app: &mut App) {
        app.add_systems(
            FixedUpdate,
            update_activation
                .in_set(ImportedPlantActivationSet)
                .before(PhysicsSet::SyncBackend),
        );
    }
}

fn update_activation(world: &mut World) {
    if !world.contains_resource::<Owners>() {
        return;
    }
    let _profile = crate::cpu_timing::Span::new("physics.imported_residency");
    // Invalid/unsupported motion keeps all obstacles resident; `ready` stays false.
    let envelopes = body_envelopes(world).unwrap_or_default();
    world.resource_scope(|world, mut owners: Mut<Owners>| {
        prune_retired(world, &mut owners);
        let Owners { groups, retired } = &mut *owners;
        for (&owner, group) in groups {
            synchronize(world, owner, group, &envelopes, retired);
        }
    });
}

fn synchronize(
    world: &mut World,
    owner: EntityId,
    group: &mut Projection,
    envelopes: &[BodyEnvelope],
    retired: &mut Vec<Retired>,
) {
    // Dense residency favors the existing direct tree lookup; an ordered
    // cursor avoids repeated lookups when fewer than half the bodies reside.
    let mut residents = (group.entities.len() < group.prepared.bodies.len().div_ceil(2))
        .then(|| MutableResidentCursor::new(&group.entities));
    for (&id, body) in &group.prepared.bodies {
        let expected = marker(owner, group.prepared.identity, id, body);
        let entity = if let Some(cursor) = &mut residents {
            cursor.take(&group.entities, id)
        } else {
            group.entities.get(&id).copied()
        };
        match (entity, active(&expected, envelopes)) {
            (None, true) => {
                let entity = world
                    .spawn((
                        Name::new("Imported vegetation collision"),
                        expected,
                        body.transform,
                        body.collider.clone(),
                    ))
                    .id();
                group.entities.insert(id, entity);
            }
            (Some(entity), false)
                if world.get::<ImportedPlantCollider>(entity) == Some(&expected) =>
            {
                retire_entity(world, entity, retired);
                group.entities.remove(&id);
            }
            // Corruption stays observable. Never heal or delete reassigned entities.
            _ => {}
        }
    }
}

fn retire_entity(world: &mut World, entity: Entity, retired: &mut Vec<Retired>) {
    retired.push(Retired {
        entity,
        backend: world
            .get::<RapierContextEntityLink>(entity)
            .and_then(|link| {
                world
                    .get::<RapierColliderHandle>(entity)
                    .copied()
                    .map(|handle| (link.0, handle))
            }),
    });
    world.despawn(entity);
}

fn prune_retired(world: &mut World, owners: &mut Owners) {
    let mut contexts = world.query::<(Entity, &RapierContextColliders)>();
    owners.retired.retain(|retired| {
        contexts.iter(world).any(|(entity, context)| {
            context.entity2collider().contains_key(&retired.entity)
                || retired.backend.is_some_and(|(expected, handle)| {
                    entity == expected && context.colliders.get(handle.0).is_some()
                })
        })
    });
}
