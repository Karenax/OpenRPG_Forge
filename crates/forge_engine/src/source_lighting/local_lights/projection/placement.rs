//! Common projection of saved light companions onto canonical model placements.
use super::{
    PreparedSourceLights, ResidentSourceLights, SourceLightIdentity, propagated_transform,
    valid_matrix,
};
use crate::ManualPlacementProjection;
use bevy::prelude::*;
use forge_assets::source_local_lights::{SourceLocalLights, SourceLocalLightsPackage};
use forge_core::{ContentHash, EntityId};
use forge_project::zone_authoring::placements::ManualPlacement;
use std::collections::{BTreeMap, BTreeSet, VecDeque};

pub mod host;

struct Anchor {
    id: String,
    parent: Option<String>,
    local: Transform,
}

pub struct PreparedPlacementLights {
    placement: ManualPlacement,
    revision: ContentHash,
    lights: PreparedSourceLights,
    anchors: Vec<Anchor>,
}

#[derive(Component, Clone, PartialEq, Eq)]
struct AnchorIdentity {
    instance: EntityId,
    revision: ContentHash,
    frame: String,
}

pub struct ResidentPlacementLights {
    prepared: PreparedPlacementLights,
    root: Entity,
    anchors: BTreeMap<String, Entity>,
    lights: ResidentSourceLights,
}

impl PreparedPlacementLights {
    pub fn from_package(
        package: &SourceLocalLightsPackage,
        placement: &ManualPlacement,
        registration_base: u64,
    ) -> Result<Self, String> {
        Self::prepare(
            package.descriptor(),
            package.receipt_hash(),
            placement,
            registration_base,
        )
    }

    fn prepare(
        descriptor: &SourceLocalLights,
        package: ContentHash,
        placement: &ManualPlacement,
        registration_base: u64,
    ) -> Result<Self, String> {
        let reference = placement
            .local_lights
            .as_deref()
            .ok_or("Placement has no declared source-light companion")?;
        reference.validate(placement.asset_hash)?;
        placement
            .transform
            .validate()
            .map_err(|error| error.to_string())?;
        if reference.manifest_sha256 != package
            || reference.descriptor_sha256 != descriptor.content_hash()
        {
            return Err("Placement source-light package or descriptor pin differs".into());
        }
        let lights = PreparedSourceLights::prepare_selected(
            descriptor,
            placement.asset_id,
            package,
            registration_base,
            Some(&reference.light_ids),
        )?;
        let anchors = anchor_plan(descriptor, reference)?;
        let revision = forge_core::bytes_sha256(
            &serde_json::to_vec(reference).map_err(|error| error.to_string())?,
        );
        Ok(Self {
            placement: placement.clone(),
            revision,
            lights,
            anchors,
        })
    }

    /// All saved transforms are model-relative. The captured acquisition matrices
    /// are not substituted for this model's current root or its authored anchors.
    pub fn stage(self, world: &mut World, root: Entity) -> Result<ResidentPlacementLights, String> {
        invalidate_collection(world);
        validate_root(world, root, &self.placement)?;
        let roots = world
            .query::<&ManualPlacementProjection>()
            .iter(world)
            .filter(|actual| actual.0.id == self.placement.id)
            .take(2)
            .count();
        if roots != 1 {
            return Err("Source lights require a unique canonical model placement".into());
        }
        if world
            .query::<&AnchorIdentity>()
            .iter(world)
            .any(|a| a.instance == self.placement.id)
        {
            return Err("Placement light anchors already have a resident owner".into());
        }
        let root_global = *world
            .get::<GlobalTransform>(root)
            .ok_or("Placement root transform absent")?;
        let mut globals = BTreeMap::new();
        for anchor in &self.anchors {
            let parent = anchor
                .parent
                .as_ref()
                .map_or(&root_global, |id| &globals[id]);
            let global = parent.mul_transform(anchor.local);
            if !valid_matrix(global.to_matrix()) {
                return Err("Placement light anchor world transform is invalid".into());
            }
            globals.insert(anchor.id.clone(), global);
        }
        let mut anchors = BTreeMap::new();
        for anchor in &self.anchors {
            let parent = anchor.parent.as_ref().map_or(root, |id| anchors[id]);
            let entity = world
                .spawn((
                    anchor.local,
                    globals[&anchor.id],
                    ChildOf(parent),
                    AnchorIdentity {
                        instance: self.placement.id,
                        revision: self.revision,
                        frame: anchor.id.clone(),
                    },
                ))
                .id();
            anchors.insert(anchor.id.clone(), entity);
        }
        let owners = self
            .lights
            .owner_frame_ids()
            .map(|id| (id.to_owned(), anchors[id]))
            .collect();
        match self.lights.stage(world, self.placement.id, &owners) {
            Ok(lights) => Ok(ResidentPlacementLights {
                prepared: self,
                root,
                anchors,
                lights,
            }),
            Err(error) => {
                // Exclusive World borrow: these newly created roots contain only
                // this operation's derived anchor hierarchy.
                for anchor in &self.anchors {
                    if anchor.parent.is_none() {
                        world.despawn(anchors[&anchor.id]);
                    }
                }
                Err(error)
            }
        }
    }
}

impl ResidentPlacementLights {
    #[must_use]
    pub fn registration_orders(&self) -> &[u64] {
        self.lights.registration_orders()
    }

    #[must_use]
    pub fn is_synchronized(&self, world: &World) -> bool {
        validate_root(world, self.root, &self.prepared.placement).is_ok()
            && self.anchors.len() == self.prepared.anchors.len()
            && self.prepared.anchors.iter().all(|anchor| {
                let Some(&entity) = self.anchors.get(&anchor.id) else {
                    return false;
                };
                let parent = anchor
                    .parent
                    .as_ref()
                    .map_or(self.root, |id| self.anchors[id]);
                world.get::<AnchorIdentity>(entity).is_some_and(|key| {
                    key.instance == self.prepared.placement.id
                        && key.revision == self.prepared.revision
                        && key.frame == anchor.id
                }) && world.get::<Transform>(entity) == Some(&anchor.local)
                    && world
                        .get::<ChildOf>(entity)
                        .is_some_and(|p| p.parent() == parent)
                    && world
                        .get::<GlobalTransform>(entity)
                        .zip(propagated_transform(world, entity))
                        .is_some_and(|(actual, expected)| {
                            actual.to_matrix() == expected.to_matrix()
                        })
            })
            && self.lights.is_synchronized(world)
    }

    /// Retire only owned derived anchors/emitters. The actual model and foreign
    /// children are protected; a contradicted ownership refuses the operation.
    pub fn retire(&mut self, world: &mut World) -> Result<(), String> {
        invalidate_collection(world);
        let mut owned: BTreeSet<_> = self.anchors.values().copied().collect();
        owned.extend(
            self.lights
                .lights
                .iter()
                .filter(|light| {
                    world.get::<SourceLightIdentity>(light.entity) == Some(&light.identity)
                })
                .map(|light| light.entity),
        );
        for (id, &entity) in &self.anchors {
            if world.get_entity(entity).is_err() {
                continue;
            }
            if world.get::<AnchorIdentity>(entity).is_none_or(|key| {
                key.instance != self.prepared.placement.id
                    || key.revision != self.prepared.revision
                    || key.frame != *id
            }) || world
                .get::<Children>(entity)
                .is_some_and(|children| children.iter().any(|child| !owned.contains(&child)))
            {
                return Err(
                    "Placement light retirement refuses foreign anchor ownership or children"
                        .into(),
                );
            }
        }
        if owned.iter().any(|&entity| {
            world
                .get::<Children>(entity)
                .is_some_and(|children| children.iter().any(|child| !owned.contains(&child)))
        }) {
            return Err("Placement light retirement refuses foreign descendants".into());
        }
        self.lights.retire(world);
        for anchor in self.prepared.anchors.iter().rev() {
            if let Some(&entity) = self.anchors.get(&anchor.id)
                && world.get_entity(entity).is_ok()
            {
                world.despawn(entity);
            }
        }
        self.anchors.clear();
        Ok(())
    }
}

#[cfg(test)]
mod tests;

fn validate_root(world: &World, root: Entity, placement: &ManualPlacement) -> Result<(), String> {
    if world
        .get::<ManualPlacementProjection>(root)
        .is_none_or(|actual| actual.0 != *placement)
        || world.get::<ChildOf>(root).is_some()
        || world.get::<Transform>(root)
            != Some(&crate::domain_transform_to_bevy(placement.transform))
        || world
            .get::<GlobalTransform>(root)
            .zip(propagated_transform(world, root))
            .is_none_or(|(actual, expected)| {
                !valid_matrix(actual.to_matrix()) || actual.to_matrix() != expected.to_matrix()
            })
    {
        return Err("Source-light root differs from its unique canonical model placement".into());
    }
    Ok(())
}

fn invalidate_collection(world: &mut World) {
    if let Some(mut collection) = world.get_resource_mut::<super::super::collection::Collection>() {
        collection.invalidate();
    }
}

fn anchor_plan(
    descriptor: &SourceLocalLights,
    reference: &forge_project::zone_authoring::placements::LocalLightPackageReference,
) -> Result<Vec<Anchor>, String> {
    let all = descriptor
        .selected_frame_parents(&reference.light_ids)
        .map_err(|error| error.to_string())?;
    let required: BTreeSet<_> = all.keys().cloned().collect();
    let authored: BTreeMap<_, _> = reference
        .anchors
        .iter()
        .map(|a| (a.frame_id.clone(), a.transform))
        .collect();
    if authored.keys().cloned().collect::<BTreeSet<_>>() != required {
        return Err(
            "Placement light anchors differ from the complete selected frame closure".into(),
        );
    }
    let mut children: BTreeMap<Option<String>, Vec<String>> = BTreeMap::new();
    for id in &required {
        children
            .entry(all[id].clone())
            .or_default()
            .push(id.clone());
    }
    let mut queue: VecDeque<_> = children.remove(&None).unwrap_or_default().into();
    let mut anchors = Vec::with_capacity(required.len());
    while let Some(id) = queue.pop_front() {
        queue.extend(children.remove(&Some(id.clone())).unwrap_or_default());
        let local = crate::domain_transform_to_bevy(authored[&id]);
        if !valid_matrix(local.to_matrix()) {
            return Err("Light anchor cannot be represented by the renderer".into());
        }
        anchors.push(Anchor {
            parent: all[&id].clone(),
            local,
            id,
        });
    }
    if anchors.len() != required.len() {
        return Err("Placement light frame hierarchy is incomplete".into());
    }
    Ok(anchors)
}
