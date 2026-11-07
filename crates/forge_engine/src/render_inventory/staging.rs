//! Prepare a replacement scoped inventory while the visible one remains live.
//! Publication and its subsequent GPU fence remain the host's responsibility.
use super::{
    Request, ScopedRequests, acquire_scoped, release_scoped, request_scoped,
    request_scoped_with_effects,
};
use bevy::{diagnostic::FrameCount, pbr::Material, prelude::*};
use forge_core::{ContentHash, EntityId};
use std::any::TypeId;

/// A staged inventory retains its candidate handles until it is activated or
/// explicitly cancelled. Abandoning it without cancellation leaves its scoped
/// request installed; hosts must clean it up on replacement and shutdown.
#[must_use]
pub struct StagedScopedInventory<M: Material> {
    current_owner: EntityId,
    current_serial: u64,
    owner: EntityId,
    serial: u64,
    identity: ContentHash,
    generation: u64,
    registration_frame: u32,
    required_live: super::Requirements,
    meshes: Vec<Handle<Mesh>>,
    materials: Vec<Handle<M>>,
    transferred: bool,
    cancelled: bool,
}

/// Exact candidate receipt and its strong handles. The receipt covers staging,
/// not a later scene publication: the host must require a new completion after
/// it changes visibility and must separately retire the old owner.
pub struct ActivatedScopedInventory<M: Material> {
    previous_owner: EntityId,
    previous_serial: u64,
    pub owner: EntityId,
    serial: u64,
    pub identity: ContentHash,
    pub generation: u64,
    pub registration_frame: u32,
    pub token: super::RenderInventoryToken,
    pub meshes: Vec<Handle<Mesh>>,
    pub materials: Vec<Handle<M>>,
}

impl<M: Material> ActivatedScopedInventory<M> {
    /// Whether this candidate still owns its exact request lifetime. This is
    /// not a substitute for checking its current GPU token and publication fence.
    #[must_use]
    pub fn request_is_current(&self, world: &World) -> bool {
        scoped(world, self.owner).is_some_and(|request| request.serial == self.serial)
    }

    /// Whether the current owner still names the exact request that was live
    /// when staging began. Equal owner, identity and generation are not enough.
    #[must_use]
    pub fn previous_request_is_current(&self, world: &World, owner: EntityId) -> bool {
        owner == self.previous_owner
            && scoped(world, owner).is_some_and(|request| request.serial == self.previous_serial)
    }

    /// Retire only the exact previous request, and only while this candidate's
    /// GPU receipt is current. The host must independently fence publication.
    pub fn release_previous(&self, world: &mut World, owner: EntityId) -> Result<(), String> {
        if !self.previous_request_is_current(world, owner)
            || !self.request_is_current(world)
            || !self
                .token
                .is_current(world, self.owner, self.identity, self.generation)
        {
            return Err("Scoped handoff was replaced before previous request retirement".into());
        }
        release_scoped(world, owner);
        Ok(())
    }

    /// Release only the exact request which produced this receipt. A later
    /// request with the same owner must survive cancellation of this one.
    pub fn cancel(self, world: &mut World) -> Result<(), String> {
        if scoped(world, self.owner).is_none_or(|request| request.serial != self.serial) {
            return Err("Activated owner was replaced before cancellation".into());
        }
        release_scoped(world, self.owner);
        Ok(())
    }
}

impl<M: Material> StagedScopedInventory<M> {
    /// Check request lifetimes without treating a temporary GPU wait as replacement.
    #[must_use]
    pub fn requests_are_current(&self, world: &World) -> bool {
        !self.transferred
            && !self.cancelled
            && scoped(world, self.current_owner)
                .is_some_and(|request| request.serial == self.current_serial)
            && scoped(world, self.owner).is_some_and(|request| request.serial == self.serial)
    }

    /// Begin only while the current owner has a valid renderer receipt. The
    /// replacement must contain every current typed source and preserve its
    /// display-effect requirements. It receives a separate serial and fence.
    pub fn begin(
        world: &mut World,
        current_owner: EntityId,
        staging_owner: EntityId,
        identity: ContentHash,
        generation: u64,
        meshes: &[Handle<Mesh>],
        materials: &[Handle<M>],
    ) -> Result<Self, String> {
        Self::begin_inner(
            world,
            current_owner,
            staging_owner,
            identity,
            generation,
            None,
            meshes,
            materials,
        )
    }

    /// Prepare a replacement which may drop unused sources from the old scope.
    /// The caller supplies every live mesh and must recheck the complete live
    /// scene and its publication fence before releasing the previous request.
    /// All supplied materials must already belong to the current scope.
    #[allow(
        clippy::too_many_arguments,
        reason = "The existing scope, its live sources and its candidate have independent identities"
    )]
    pub fn begin_with_live_sources(
        world: &mut World,
        current_owner: EntityId,
        staging_owner: EntityId,
        identity: ContentHash,
        generation: u64,
        live_meshes: &[Handle<Mesh>],
        meshes: &[Handle<Mesh>],
        materials: &[Handle<M>],
    ) -> Result<Self, String> {
        Self::begin_inner(
            world,
            current_owner,
            staging_owner,
            identity,
            generation,
            Some(live_meshes),
            meshes,
            materials,
        )
    }

    #[allow(
        clippy::too_many_arguments,
        reason = "One transaction binds the old scope, candidate and its live-source floor"
    )]
    fn begin_inner(
        world: &mut World,
        current_owner: EntityId,
        staging_owner: EntityId,
        identity: ContentHash,
        generation: u64,
        live_meshes: Option<&[Handle<Mesh>]>,
        meshes: &[Handle<Mesh>],
        materials: &[Handle<M>],
    ) -> Result<Self, String> {
        if current_owner == staging_owner
            || super::current_owner(world) == Some(staging_owner)
            || world
                .get_resource::<ScopedRequests>()
                .is_some_and(|requests| requests.0.contains_key(&staging_owner))
        {
            return Err("Staging requires an unused independent owner".into());
        }
        let current = scoped(world, current_owner)
            .cloned()
            .ok_or("Current scoped inventory absent")?;
        if current.material_type != TypeId::of::<M>()
            || acquire_scoped(world, current_owner, current.identity, current.generation).is_none()
        {
            return Err("Current scoped renderer receipt is not valid".into());
        }
        let candidate = super::Requirements::custom(world, meshes, materials)?;
        let required_live = if let Some(live_meshes) = live_meshes {
            super::Requirements::custom(world, live_meshes, materials)?
        } else {
            current.required.clone()
        };
        if !current.required.contains(&required_live) || !candidate.contains(&required_live) {
            return Err("Staged inventory does not cover the current sources".into());
        }
        let frame = world
            .get_resource::<FrameCount>()
            .ok_or("Staging requires a renderer frame")?
            .0;
        if let Some(effects) = current.effects.clone() {
            request_scoped_with_effects(
                world,
                staging_owner,
                identity,
                generation,
                meshes,
                materials,
                effects,
            )?;
        } else {
            request_scoped(
                world,
                staging_owner,
                identity,
                generation,
                meshes,
                materials,
            )?;
        }
        let serial = scoped(world, staging_owner)
            .expect("a successful scoped registration installs its request")
            .serial;
        Ok(Self {
            current_owner,
            current_serial: current.serial,
            owner: staging_owner,
            serial,
            identity,
            generation,
            registration_frame: frame,
            required_live,
            meshes: meshes.to_vec(),
            materials: materials.to_vec(),
            transferred: false,
            cancelled: false,
        })
    }

    /// Return a fully observed candidate only while both requests are still
    /// current. The caller must publish atomically and fence that publication.
    pub fn try_activate(
        &mut self,
        world: &World,
    ) -> Result<Option<ActivatedScopedInventory<M>>, String> {
        if self.transferred || self.cancelled {
            return Err("Staged inventory already consumed".into());
        }
        let current = scoped(world, self.current_owner)
            .filter(|request| request.serial == self.current_serial)
            .ok_or("Current scoped inventory changed during staging")?;
        let staged = scoped(world, self.owner)
            .filter(|request| request.serial == self.serial)
            .ok_or("Staged scoped inventory changed during staging")?;
        if staged.identity != self.identity
            || staged.generation != self.generation
            || staged.material_type != current.material_type
            || staged.effects != current.effects
            || !current.required.contains(&self.required_live)
            || !staged.required.contains(&self.required_live)
        {
            return Err("Staged scoped inventory no longer matches the current sources".into());
        }
        if acquire_scoped(
            world,
            self.current_owner,
            current.identity,
            current.generation,
        )
        .is_none()
        {
            return Ok(None);
        }
        let Some(token) = acquire_scoped(world, self.owner, self.identity, self.generation) else {
            return Ok(None);
        };
        if !token.covers_frame(world, self.registration_frame)
            || !token.covers_sources(world, &self.meshes, &self.materials)
        {
            return Ok(None);
        }
        self.transferred = true;
        Ok(Some(ActivatedScopedInventory {
            previous_owner: self.current_owner,
            previous_serial: self.current_serial,
            owner: self.owner,
            serial: self.serial,
            identity: self.identity,
            generation: self.generation,
            registration_frame: self.registration_frame,
            token,
            meshes: std::mem::take(&mut self.meshes),
            materials: std::mem::take(&mut self.materials),
        }))
    }

    /// Release only this exact staged request; never retire a later occupant.
    pub fn cancel(&mut self, world: &mut World) -> Result<(), String> {
        if self.transferred || self.cancelled {
            return Err("Staged inventory already consumed".into());
        }
        if scoped(world, self.owner).is_none_or(|request| request.serial != self.serial) {
            return Err("Staged owner was replaced before cancellation".into());
        }
        release_scoped(world, self.owner);
        self.meshes.clear();
        self.materials.clear();
        self.cancelled = true;
        Ok(())
    }
}

fn scoped(world: &World, owner: EntityId) -> Option<&Request> {
    world.get_resource::<ScopedRequests>()?.0.get(&owner)
}
