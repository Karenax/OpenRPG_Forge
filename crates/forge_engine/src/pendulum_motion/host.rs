//! Shared, transactional projection of acquired pendulum motion onto object pivots.
//!
//! Callers resolve their own admitted asset and supply stable part identities,
//! local pivots and acquired states. The host owns no asset or module identity.
pub mod collection;
pub mod nodes;
pub mod resolve;

use super::{LanternMotion, SignMotion, Wind};
use crate::{ManualPlacementProjection, domain_transform_to_bevy};
use bevy::prelude::*;
use forge_project::zone_authoring::placements::ManualPlacement;
use std::collections::{BTreeMap, HashSet};

#[cfg(test)]
mod tests;

const HLOD_OWNER: &str = "pendulum-motion";

/// One of the two existing shared pendulum models, independent of asset names.
#[derive(Clone, Copy, Debug)]
pub enum Motion {
    Suspended(LanternMotion),
    Hinged(SignMotion),
}

impl Motion {
    fn validate(self) -> Result<(), String> {
        match self {
            Self::Suspended(state) => state.validate(),
            Self::Hinged(state) => state.validate(),
        }
    }

    fn advance(&mut self, dt: f64, wind: Wind) -> Result<(), String> {
        match self {
            Self::Suspended(state) => state.advance(dt, wind),
            Self::Hinged(state) => state.advance(dt, wind),
        }
    }

    fn rotation(self) -> Quat {
        match self {
            Self::Suspended(state) => state.rotation().as_quat(),
            Self::Hinged(state) => state.rotation().as_quat(),
        }
    }
}

/// A resolved pivot. `key` identifies a part across a geometry rebuild.
#[derive(Clone, Copy, Debug)]
pub struct Binding {
    pub key: u64,
    pub entity: Entity,
    pub pivot: Vec3,
    pub rest_rotation: Quat,
    /// Exact local asset scale. Physical support for a composed transform is
    /// checked separately by the collider owner.
    pub rest_scale: Vec3,
    pub motion: Motion,
}

struct Part {
    binding: Binding,
    // Capture the whole chain, not just the immediate parent: an asset can
    // contain nested pivots and no intermediate node may be silently reparented.
    ancestors: Vec<Entity>,
}

/// One placement's live projection; canonical authoring data remains unchanged.
pub struct Resident {
    placement: ManualPlacement,
    root: Entity,
    parts: Vec<Part>,
    scratch: Vec<(Motion, Quat)>,
}

impl Resident {
    /// Validate the complete hierarchy before retaining any motion state.
    pub fn bind(
        world: &mut World,
        placement: &ManualPlacement,
        root: Entity,
        bindings: Vec<Binding>,
    ) -> Result<Self, String> {
        let resident = Self::prepare(world, placement, root, bindings)?;
        crate::static_hlod::retain_moving_object(world, placement.id, HLOD_OWNER)?;
        Ok(resident)
    }

    fn prepare(
        world: &mut World,
        placement: &ManualPlacement,
        root: Entity,
        bindings: Vec<Binding>,
    ) -> Result<Self, String> {
        placement
            .transform
            .validate()
            .map_err(|error| error.to_string())?;
        verify_root(world, root, placement)?;
        let matching_roots = world
            .query::<&ManualPlacementProjection>()
            .iter(world)
            .filter(|projection| projection.0.id == placement.id)
            .take(2)
            .count();
        if matching_roots != 1 {
            return Err("Pendulum placement root is absent or ambiguous".into());
        }
        if bindings.is_empty() {
            return Err("Pendulum projection has no pivots".into());
        }
        let mut keys = HashSet::with_capacity(bindings.len());
        let mut entities = HashSet::with_capacity(bindings.len());
        let mut parts = Vec::with_capacity(bindings.len());
        for binding in bindings {
            if !keys.insert(binding.key) || !entities.insert(binding.entity) {
                return Err("Pendulum pivot identity or entity is duplicated".into());
            }
            binding.motion.validate()?;
            verify_pivot(world, &binding)?;
            let ancestors = ancestors(world, binding.entity, root)?;
            parts.push(Part { binding, ancestors });
        }
        let scratch = Vec::with_capacity(parts.len());
        Ok(Self {
            placement: placement.clone(),
            root,
            parts,
            scratch,
        })
    }

    #[must_use]
    pub const fn root(&self) -> Entity {
        self.root
    }

    #[must_use]
    pub const fn placement(&self) -> &ManualPlacement {
        &self.placement
    }

    /// Release only this motion owner's detailed-render requirement. Geometry
    /// and the last applied pose remain owned by the scene.
    pub fn release(self, world: &mut World) {
        crate::static_hlod::release_moving_object(world, self.placement.id, HLOD_OWNER);
    }

    /// Reattach a rebuilt hierarchy while preserving each matching part's state.
    /// Any refusal leaves both the current host and the new hierarchy unchanged.
    pub fn rebind(
        &mut self,
        world: &mut World,
        placement: &ManualPlacement,
        root: Entity,
        bindings: Vec<Binding>,
    ) -> Result<(), String> {
        if placement.id != self.placement.id
            || placement.asset_id != self.placement.asset_id
            || placement.asset_hash != self.placement.asset_hash
        {
            return Err("Pendulum rebind changes the placement or asset revision".into());
        }
        if !crate::static_hlod::moving_object_owned(world, placement.id, HLOD_OWNER) {
            return Err("Pendulum detailed-render ownership was removed".into());
        }
        let mut next = Self::prepare(world, placement, root, bindings)?;
        if next.parts.len() != self.parts.len() {
            return Err("Pendulum rebind changes the pivot inventory".into());
        }
        let previous: BTreeMap<_, _> = self
            .parts
            .iter()
            .map(|part| (part.binding.key, part.binding))
            .collect();
        for part in &mut next.parts {
            let old = previous
                .get(&part.binding.key)
                .ok_or("Pendulum rebind changes a pivot identity")?;
            if std::mem::discriminant(&part.binding.motion) != std::mem::discriminant(&old.motion)
                || part.binding.pivot != old.pivot
                || part.binding.rest_rotation != old.rest_rotation
                || part.binding.rest_scale != old.rest_scale
            {
                return Err("Pendulum rebind changes the admitted pivot or motion model".into());
            }
            old.motion.validate()?;
            part.binding.motion = old.motion;
        }
        let rotations = next.rotations()?;
        next.apply(world, &rotations);
        *self = next;
        Ok(())
    }

    /// Validate and calculate every part before applying any frame to the world.
    pub fn advance(&mut self, world: &mut World, dt: f64, wind: Wind) -> Result<(), String> {
        verify_root(world, self.root, &self.placement)?;
        for part in &self.parts {
            verify_pivot(world, &part.binding)?;
            verify_ancestors(world, part.binding.entity, &part.ancestors)?;
        }
        // Reuse the bounded transaction buffer across frames. Failed validation
        // leaves only scratch data; the next frame clears it before reuse.
        self.scratch.clear();
        for part in &self.parts {
            let mut state = part.binding.motion;
            state.advance(dt, wind)?;
            let rotation = rotation(part.binding.rest_rotation, state)?;
            self.scratch.push((state, rotation));
        }
        for (part, (state, rotation)) in self.parts.iter_mut().zip(self.scratch.iter().copied()) {
            world
                .get_mut::<Transform>(part.binding.entity)
                .expect("Exclusive world and previously validated pivot")
                .rotation = rotation;
            part.binding.motion = state;
        }
        Ok(())
    }

    fn rotations(&self) -> Result<Vec<Quat>, String> {
        self.parts
            .iter()
            .map(|part| rotation(part.binding.rest_rotation, part.binding.motion))
            .collect()
    }

    fn apply(&self, world: &mut World, rotations: &[Quat]) {
        for (part, rotation) in self.parts.iter().zip(rotations) {
            world
                .get_mut::<Transform>(part.binding.entity)
                .expect("Exclusive world and previously validated pivot")
                .rotation = *rotation;
        }
    }
}

fn rotation(rest: Quat, motion: Motion) -> Result<Quat, String> {
    let motion = motion.rotation();
    let result = if rest == Quat::IDENTITY {
        // Preserve the acquired reference's quaternion without another multiply.
        motion
    } else {
        rest * motion
    };
    if !valid_rotation(result) {
        return Err("Pendulum rotation is invalid".into());
    }
    Ok(result)
}

fn valid_rotation(rotation: Quat) -> bool {
    rotation.is_finite() && (rotation.length_squared() - 1.0).abs() <= 1e-5
}

fn verify_root(world: &World, root: Entity, placement: &ManualPlacement) -> Result<(), String> {
    if world.get::<ChildOf>(root).is_some()
        || world
            .get::<ManualPlacementProjection>(root)
            .is_none_or(|projection| projection.0 != *placement)
        || world.get::<Transform>(root) != Some(&domain_transform_to_bevy(placement.transform))
    {
        return Err("Pendulum root differs from its admitted placement".into());
    }
    Ok(())
}

fn verify_pivot(world: &World, binding: &Binding) -> Result<(), String> {
    let transform = world
        .get::<Transform>(binding.entity)
        .ok_or("Pendulum pivot transform is absent")?;
    if !binding.pivot.is_finite()
        || !valid_rotation(binding.rest_rotation)
        || transform.translation != binding.pivot
        || !binding.rest_scale.is_finite()
        || binding
            .rest_scale
            .to_array()
            .iter()
            .any(|axis| axis.abs() <= 0.0)
        || transform.scale != binding.rest_scale
        || !valid_rotation(transform.rotation)
    {
        return Err("Pendulum pivot differs from its admitted local frame".into());
    }
    Ok(())
}

fn ancestors(world: &World, mut entity: Entity, root: Entity) -> Result<Vec<Entity>, String> {
    let mut result = Vec::new();
    for _ in 0..128 {
        let parent = world
            .get::<ChildOf>(entity)
            .ok_or("Pendulum pivot is outside its placement")?
            .parent();
        result.push(parent);
        if parent == root {
            return Ok(result);
        }
        entity = parent;
    }
    Err("Pendulum pivot ancestry exceeds the supported depth".into())
}

fn verify_ancestors(world: &World, mut entity: Entity, expected: &[Entity]) -> Result<(), String> {
    for &parent in expected {
        if world
            .get::<ChildOf>(entity)
            .is_none_or(|link| link.parent() != parent)
        {
            return Err("Pendulum pivot hierarchy changed".into());
        }
        entity = parent;
    }
    Ok(())
}
