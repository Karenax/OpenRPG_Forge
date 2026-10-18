//! Derived local emitters attached to explicit, live asset-frame bindings.
//! No source label selects an asset or an entity. The importer supplies its
//! governed asset identity, instance identity and resolved owner entities.
use super::{Light, scene::SourceLight};
use bevy::{math::DVec3, prelude::*};
use forge_assets::source_local_lights::{SourceLocalLights, SourceLocalLightsPackage};
use forge_core::{AssetId, ContentHash, EntityId};
use std::collections::{BTreeMap, BTreeSet};

/// Runtime identity only; canonical placement/asset records remain engine-neutral.
#[derive(Component, Clone, Copy, Debug, PartialEq, Eq)]
pub struct SourceLightIdentity {
    pub asset_id: AssetId,
    pub package_revision: ContentHash,
    pub descriptor_revision: ContentHash,
    pub instance_id: EntityId,
    pub source_light_id: ContentHash,
}

struct PreparedLight {
    id: ContentHash,
    owner_frame: String,
    order: u64,
    parameters: Light,
}

/// Validated immutable parameters; owner entities are resolved at staging time.
pub struct PreparedSourceLights {
    asset_id: AssetId,
    package_revision: ContentHash,
    descriptor_revision: ContentHash,
    frame_parents: BTreeMap<String, Option<String>>,
    lights: Vec<PreparedLight>,
    orders: Vec<u64>,
}

impl PreparedSourceLights {
    /// Registration offsets are selected explicitly by the scene adapter, so
    /// copies do not silently reuse another instance's distance-sort tie order.
    pub fn from_package(
        package: &SourceLocalLightsPackage,
        asset_id: AssetId,
        registration_base: u64,
    ) -> Result<Self, String> {
        Self::prepare(
            package.descriptor(),
            asset_id,
            package.receipt_hash(),
            registration_base,
        )
    }

    /// Prepare only explicitly selected source IDs for one governed placement.
    /// Input order does not renumber the original registration order or phase.
    /// Unselected frames are omitted from this derived projection. Relationships
    /// between selected frames still require the same live ancestor relation;
    /// the importer resolves each resulting root to its actual asset anchor.
    pub fn from_package_selection(
        package: &SourceLocalLightsPackage,
        asset_id: AssetId,
        registration_base: u64,
        selected_ids: &[ContentHash],
    ) -> Result<Self, String> {
        Self::prepare_selected(
            package.descriptor(),
            asset_id,
            package.receipt_hash(),
            registration_base,
            Some(selected_ids),
        )
    }

    fn prepare(
        descriptor: &SourceLocalLights,
        asset_id: AssetId,
        package_revision: ContentHash,
        registration_base: u64,
    ) -> Result<Self, String> {
        Self::prepare_selected(
            descriptor,
            asset_id,
            package_revision,
            registration_base,
            None,
        )
    }

    fn prepare_selected(
        descriptor: &SourceLocalLights,
        asset_id: AssetId,
        package_revision: ContentHash,
        registration_base: u64,
        selected_ids: Option<&[ContentHash]>,
    ) -> Result<Self, String> {
        let document = descriptor.document();
        if selected_ids.is_some_and(|ids| ids.is_empty() || ids.len() > document.lights.len()) {
            return Err("Source light selection is empty or larger than its inventory".into());
        }
        let selection = selected_ids.map(|ids| ids.iter().copied().collect::<BTreeSet<_>>());
        if let Some(ids) = selected_ids {
            let unique = selection
                .as_ref()
                .ok_or("Source light selection is absent")?;
            let available: BTreeSet<_> = document.lights.iter().map(|record| record.id).collect();
            if ids.is_empty() || unique.len() != ids.len() || !unique.is_subset(&available) {
                return Err("Source light selection is empty, duplicated or unknown".into());
            }
        }
        let defaults = document.source_defaults;
        let mut lights = Vec::with_capacity(document.lights.len());
        for record in &document.lights {
            if selection
                .as_ref()
                .is_some_and(|ids| !ids.contains(&record.id))
            {
                continue;
            }
            let order = registration_base
                .checked_add(record.registration_order)
                .ok_or("Source light registration offset overflows")?;
            let parameters = Light {
                position: DVec3::from_array(record.position_local_m),
                direction: record.direction_local.map(DVec3::from_array),
                color: DVec3::from_array(record.color_linear_rgb),
                intensity: record.intensity_at_1m,
                range: record.range_m,
                cos_inner: record.cos_inner,
                cos_outer: record.cos_outer,
                enabled: record.enabled.unwrap_or(defaults.enabled),
                scale: record.scale.unwrap_or(defaults.scale),
                flicker: record.flicker.unwrap_or(defaults.flicker),
                phase: record.phase,
            };
            parameters.validate().map_err(|error| error.to_string())?;
            lights.push(PreparedLight {
                id: record.id,
                owner_frame: record.owner_frame.clone(),
                order,
                parameters,
            });
        }
        let orders = lights.iter().map(|light| light.order).collect();
        let all_frames: BTreeMap<_, _> = document
            .frames
            .iter()
            .map(|frame| (frame.id.clone(), frame.parent.clone()))
            .collect();
        let frame_parents = scoped_frame_parents(
            all_frames,
            selection.as_ref().map(|_| {
                lights
                    .iter()
                    .map(|light| light.owner_frame.clone())
                    .collect()
            }),
        )?;
        Ok(Self {
            asset_id,
            package_revision,
            descriptor_revision: descriptor.content_hash(),
            frame_parents,
            lights,
            orders,
        })
    }

    #[must_use]
    pub fn registration_orders(&self) -> &[u64] {
        &self.orders
    }

    /// Exact live frame binding keys for this preparation.
    pub fn owner_frame_ids(&self) -> impl Iterator<Item = &str> {
        self.frame_parents.keys().map(String::as_str)
    }

    /// All bindings and conflicts are checked before any entity is spawned.
    /// No captured world matrix is substituted for an owner's authored transform.
    pub fn stage(
        &self,
        world: &mut World,
        instance_id: EntityId,
        owners: &BTreeMap<String, Entity>,
    ) -> Result<ResidentSourceLights, String> {
        self.validate_bindings(world, instance_id, owners, &[])?;
        Ok(self.spawn(world, instance_id, owners))
    }

    fn validate_bindings(
        &self,
        world: &mut World,
        instance_id: EntityId,
        owners: &BTreeMap<String, Entity>,
        previous: &[ProjectedLight],
    ) -> Result<(), String> {
        if self.frame_parents.keys().ne(owners.keys())
            || owners.values().copied().collect::<BTreeSet<_>>().len() != owners.len()
        {
            return Err("Source light owners are missing, extra or aliased".into());
        }
        for (frame, entity) in owners {
            let local = world
                .get::<Transform>(*entity)
                .ok_or("Source light owner has no local transform")?;
            let global = world
                .get::<GlobalTransform>(*entity)
                .ok_or("Source light owner has no world transform")?;
            let matrix = global.to_matrix();
            if !local.to_matrix().is_finite()
                || !valid_matrix(matrix)
                || propagated_transform(world, *entity)
                    .is_none_or(|live| live.to_matrix() != matrix)
            {
                return Err("Source light owner transform is nonfinite or singular".into());
            }
            if let Some(parent) = &self.frame_parents[frame]
                && !has_ancestor(world, *entity, owners[parent])
            {
                return Err("Source light frame binding contradicts live ownership".into());
            }
        }
        // A changed previous emitter cannot be left behind when its replacement
        // uses another registration range. Already-despawned entities are safe.
        for light in previous {
            if world.get_entity(light.entity).is_err() {
                continue;
            }
            if world.get::<SourceLightIdentity>(light.entity) != Some(&light.identity)
                || world.get::<SourceLight>(light.entity).is_none_or(|source| {
                    source.registration_order != light.order
                        || !same_parameters(&source.parameters, &light.parameters)
                })
                || world
                    .get::<ChildOf>(light.entity)
                    .is_none_or(|parent| parent.parent() != light.owner)
                || world.get::<Transform>(light.entity) != Some(&Transform::IDENTITY)
            {
                return Err("Previous source light projection changed outside its owner".into());
            }
        }
        let retired: BTreeSet<_> = previous
            .iter()
            .filter(|light| world.get::<SourceLightIdentity>(light.entity) == Some(&light.identity))
            .map(|light| light.entity)
            .collect();
        let orders: BTreeSet<_> = self.orders.iter().copied().collect();
        let mut query = world.query::<(Entity, &SourceLight, Option<&SourceLightIdentity>)>();
        for (entity, light, identity) in query.iter(world) {
            if retired.contains(&entity) {
                continue;
            }
            if orders.contains(&light.registration_order)
                || identity.is_some_and(|identity| {
                    identity.asset_id == self.asset_id && identity.instance_id == instance_id
                })
            {
                return Err("Source light order or asset instance is already projected".into());
            }
        }
        Ok(())
    }

    fn spawn(
        &self,
        world: &mut World,
        instance_id: EntityId,
        owners: &BTreeMap<String, Entity>,
    ) -> ResidentSourceLights {
        let lights = self
            .lights
            .iter()
            .map(|light| {
                let owner = owners[&light.owner_frame];
                let identity = SourceLightIdentity {
                    asset_id: self.asset_id,
                    package_revision: self.package_revision,
                    descriptor_revision: self.descriptor_revision,
                    instance_id,
                    source_light_id: light.id,
                };
                let entity = world
                    .spawn((
                        identity,
                        ChildOf(owner),
                        Transform::IDENTITY,
                        SourceLight {
                            registration_order: light.order,
                            parameters: light.parameters.clone(),
                        },
                    ))
                    .id();
                ProjectedLight {
                    entity,
                    owner,
                    identity,
                    order: light.order,
                    parameters: light.parameters.clone(),
                }
            })
            .collect();
        ResidentSourceLights {
            lights,
            orders: self.orders.clone(),
            asset_id: self.asset_id,
            instance_id,
            active: true,
            owner_ancestry: self
                .frame_parents
                .iter()
                .filter_map(|(frame, parent)| {
                    parent
                        .as_ref()
                        .map(|parent| (owners[frame], owners[parent]))
                })
                .collect(),
        }
    }
}

struct ProjectedLight {
    entity: Entity,
    owner: Entity,
    identity: SourceLightIdentity,
    order: u64,
    parameters: Light,
}

/// Owns only its emitter children, never the parent placement or other assets.
pub struct ResidentSourceLights {
    asset_id: AssetId,
    instance_id: EntityId,
    lights: Vec<ProjectedLight>,
    orders: Vec<u64>,
    active: bool,
    owner_ancestry: Vec<(Entity, Entity)>,
}

impl ResidentSourceLights {
    #[must_use]
    pub const fn instance_id(&self) -> EntityId {
        self.instance_id
    }

    #[must_use]
    pub fn registration_orders(&self) -> &[u64] {
        &self.orders
    }

    /// Call after transform propagation, before publishing the scene inventory.
    #[must_use]
    pub fn is_synchronized(&self, world: &World) -> bool {
        self.active
            && self
                .owner_ancestry
                .iter()
                .all(|&(owner, ancestor)| has_ancestor(world, owner, ancestor))
            && self.lights.iter().all(|light| {
                world.get::<SourceLightIdentity>(light.entity) == Some(&light.identity)
                    && world
                        .get::<SourceLight>(light.entity)
                        .is_some_and(|source| {
                            source.registration_order == light.order
                                && same_parameters(&source.parameters, &light.parameters)
                        })
                    && world
                        .get::<ChildOf>(light.entity)
                        .is_some_and(|parent| parent.parent() == light.owner)
                    && world.get::<Transform>(light.entity) == Some(&Transform::IDENTITY)
                    && world
                        .get::<GlobalTransform>(light.entity)
                        .zip(world.get::<GlobalTransform>(light.owner))
                        .is_some_and(|(child, parent)| {
                            valid_matrix(parent.to_matrix())
                                && child.to_matrix() == parent.to_matrix()
                                && propagated_transform(world, light.owner)
                                    .is_some_and(|live| live.to_matrix() == parent.to_matrix())
                        })
            })
    }

    /// A rejected revision retains the previous projection. A successful revision
    /// is swapped synchronously under the caller's exclusive `World` borrow.
    pub fn replace(
        &mut self,
        world: &mut World,
        prepared: &PreparedSourceLights,
        owners: &BTreeMap<String, Entity>,
    ) -> Result<(), String> {
        if !self.active {
            return Err("Retired source light projection cannot be replaced".into());
        }
        if prepared.asset_id != self.asset_id {
            return Err("Source light replacement changes its governed asset identity".into());
        }
        prepared.validate_bindings(world, self.instance_id, owners, &self.lights)?;
        let next = prepared.spawn(world, self.instance_id, owners);
        self.retire(world);
        *self = next;
        Ok(())
    }

    pub fn retire(&mut self, world: &mut World) {
        for light in self.lights.drain(..) {
            if world.get::<SourceLightIdentity>(light.entity) == Some(&light.identity) {
                world.despawn(light.entity);
            }
        }
        self.orders.clear();
        self.active = false;
    }
}

fn scoped_frame_parents(
    all: BTreeMap<String, Option<String>>,
    used: Option<BTreeSet<String>>,
) -> Result<BTreeMap<String, Option<String>>, String> {
    let Some(used) = used else {
        return Ok(all);
    };
    let mut parents = BTreeMap::new();
    for frame in &used {
        let mut parent = all
            .get(frame)
            .cloned()
            .ok_or("Selected source light frame is absent")?;
        while let Some(id) = &parent {
            if used.contains(id) {
                break;
            }
            parent = all
                .get(id)
                .cloned()
                .ok_or("Selected source light ancestor is absent")?;
        }
        parents.insert(frame.clone(), parent);
    }
    Ok(parents)
}

fn valid_matrix(matrix: Mat4) -> bool {
    let determinant = matrix.determinant();
    matrix.is_finite() && determinant.is_finite() && determinant.abs() > f32::MIN_POSITIVE
}

// Imported parameters are an immutable source snapshot. Gameplay controls need
// an explicit derived layer; changing this snapshot invalidates its readiness.
fn same_parameters(left: &Light, right: &Light) -> bool {
    left.position.to_array().map(f64::to_bits) == right.position.to_array().map(f64::to_bits)
        && left.color.to_array().map(f64::to_bits) == right.color.to_array().map(f64::to_bits)
        && left.direction.map(|v| v.to_array().map(f64::to_bits))
            == right.direction.map(|v| v.to_array().map(f64::to_bits))
        && left.cos_inner.map(f64::to_bits) == right.cos_inner.map(f64::to_bits)
        && left.cos_outer.map(f64::to_bits) == right.cos_outer.map(f64::to_bits)
        && left.enabled == right.enabled
        && [
            left.intensity,
            left.range,
            left.scale,
            left.flicker,
            left.phase,
        ]
        .map(f64::to_bits)
            == [
                right.intensity,
                right.range,
                right.scale,
                right.flicker,
                right.phase,
            ]
            .map(f64::to_bits)
}

fn propagated_transform(world: &World, entity: Entity) -> Option<GlobalTransform> {
    let mut current = entity;
    let mut visited = BTreeSet::new();
    let mut transforms = Vec::new();
    loop {
        if !visited.insert(current) {
            return None;
        }
        transforms.push(*world.get::<Transform>(current)?);
        let Some(parent) = world.get::<ChildOf>(current) else {
            break;
        };
        current = parent.parent();
    }
    let mut transforms = transforms.into_iter().rev();
    let mut global = GlobalTransform::from(transforms.next()?);
    for local in transforms {
        global = global.mul_transform(local);
    }
    Some(global)
}

fn has_ancestor(world: &World, entity: Entity, ancestor: Entity) -> bool {
    let mut current = entity;
    let mut visited = BTreeSet::new();
    while let Some(parent) = world.get::<ChildOf>(current) {
        current = parent.parent();
        if current == ancestor {
            return true;
        }
        if !visited.insert(current) {
            return false;
        }
    }
    false
}

#[cfg(test)]
mod tests;
