//! Shared lifecycle for independently authored moving placements.
use super::Wind;
use bevy::prelude::World;
use forge_core::EntityId;
use forge_project::zone_authoring::placements::ManualPlacement;
use std::collections::{BTreeMap, BTreeSet};

/// Resolve an admitted placement's data and delegate motion to the shared host.
/// Implementations may enforce additional asset-specific admission checks.
pub trait Projection: Sized {
    type Source: ?Sized;

    fn bind(
        world: &mut World,
        source: &Self::Source,
        placement: &ManualPlacement,
    ) -> Result<Self, String>;

    fn rebind(
        &mut self,
        world: &mut World,
        source: &Self::Source,
        placement: &ManualPlacement,
    ) -> Result<(), String>;

    fn advance(&mut self, world: &mut World, dt: f64, wind: Wind) -> Result<(), String>;

    /// Retire this projection's owned runtime state without removing scene data.
    fn release(self, world: &mut World);
}

/// Duplicate identities are refused before the collection or scene can change.
pub struct Desired<'a> {
    placements: &'a [ManualPlacement],
    ids: BTreeSet<EntityId>,
}

impl<'a> Desired<'a> {
    pub fn new(placements: &'a [ManualPlacement]) -> Result<Self, String> {
        let ids: BTreeSet<_> = placements.iter().map(|placement| placement.id).collect();
        if ids.len() != placements.len() {
            return Err("Duplicate canonical pendulum motion instance".into());
        }
        Ok(Self { placements, ids })
    }

    #[must_use]
    pub const fn placements(&self) -> &'a [ManualPlacement] {
        self.placements
    }
}

struct Instance<P> {
    placement: ManualPlacement,
    projection: P,
}

/// Motion state only: geometry and authored documents remain owned by callers.
pub struct Collection<P> {
    instances: BTreeMap<EntityId, Instance<P>>,
}

impl<P> Default for Collection<P> {
    fn default() -> Self {
        Self {
            instances: BTreeMap::new(),
        }
    }
}

impl<P> Collection<P> {
    #[must_use]
    pub fn len(&self) -> usize {
        self.instances.len()
    }

    #[must_use]
    pub fn is_empty(&self) -> bool {
        self.instances.is_empty()
    }
}

impl<P: Projection> Collection<P> {
    pub fn clear(&mut self, world: &mut World) {
        for (_, instance) in std::mem::take(&mut self.instances) {
            instance.projection.release(world);
        }
    }

    /// Retire removed motion state, bind or rebind each admitted placement and
    /// advance it. A stale hierarchy is reacquired once while retaining phases.
    pub fn update(
        &mut self,
        world: &mut World,
        source: &P::Source,
        desired: &Desired<'_>,
        dt: f64,
        wind: Wind,
    ) -> Result<(), String> {
        for (_, instance) in self
            .instances
            .extract_if(.., |id, _| !desired.ids.contains(id))
        {
            instance.projection.release(world);
        }
        for placement in desired.placements {
            if let Some(instance) = self.instances.get_mut(&placement.id) {
                if instance.placement != *placement {
                    instance.projection.rebind(world, source, placement)?;
                    instance.placement = placement.clone();
                }
                if instance.projection.advance(world, dt, wind).is_err() {
                    instance.projection.rebind(world, source, placement)?;
                    instance.projection.advance(world, dt, wind)?;
                }
            } else {
                let mut projection = P::bind(world, source, placement)?;
                if let Err(error) = projection.advance(world, dt, wind) {
                    projection.release(world);
                    return Err(error);
                }
                self.instances.insert(
                    placement.id,
                    Instance {
                        placement: placement.clone(),
                        projection,
                    },
                );
            }
        }
        Ok(())
    }
}
