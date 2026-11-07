//! Host-owned particle lifecycle, independent of asset-library filesystem/UI.
//! Borrowed node/material survive release; only this instance's components and
//! slot reservations are retired. Asset handles use Bevy's normal lifetime.
use crate::{
    source_graph::SourceGraphMaterial,
    source_particle_compute::{
        SourceParticleComputeQueue, SourceParticleProjection, SourceParticleSceneOwner,
    },
    source_particle_transport::PreparedSourceParticleOwner,
};
use bevy::{ecs::change_detection::Tick, prelude::*, render::storage::ShaderBuffer};
use forge_assets::source_particle_transport::ResolvedSourceParticleTransport;
use forge_core::ContentHash;
use std::{collections::BTreeMap, sync::Arc};

#[derive(Resource, Default)]
struct HostedSlots {
    slots: BTreeMap<u64, Arc<()>>,
    materials: BTreeMap<bevy::asset::AssetId<SourceGraphMaterial>, Arc<()>>,
}

pub struct HostedSourceParticle {
    owner: Entity,
    projection: Entity,
    producer_slot: u64,
    projection_slot: u64,
    owner_identity: Arc<()>,
    projection_identity: Arc<()>,
    reservation: Arc<()>,
    material: Handle<SourceGraphMaterial>,
    buffers: Vec<Handle<ShaderBuffer>>,
    shaders: Vec<Handle<Shader>>,
    curve: Option<Handle<Image>>,
    released: bool,
}

pub struct PreparedHostedParticleFrame {
    projection: Entity,
    identity: Arc<()>,
    bank: Arc<[u8; 1024]>,
    scene_bound: bool,
}

pub(crate) struct HostedParticleObservation {
    pub initialized: bool,
    pub current_frame: bool,
    pub producer_steps: u64,
    pub projection_frames: u64,
    pub material: bevy::asset::AssetId<SourceGraphMaterial>,
}

#[derive(Clone)]
struct HierarchyNode {
    entity: Entity,
    parent: Option<Entity>,
    parent_tick: Option<Tick>,
    transform_tick: Tick,
    local: Mat4,
}

#[derive(Clone)]
pub(crate) struct ParticleViewWitness {
    hierarchy: Vec<HierarchyNode>,
    view: Entity,
    world_from_view: Mat4,
    camera_tick: Tick,
    projection_tick: Option<Tick>,
}

impl ParticleViewWitness {
    pub(crate) fn capture(world: &World, view: Entity, pose: Mat4) -> Result<Self, String> {
        let mut hierarchy = Vec::new();
        let mut seen = std::collections::BTreeSet::new();
        let mut current = view;
        for _ in 0..128 {
            if !seen.insert(current) {
                return Err("Particle camera hierarchy contains a cycle".into());
            }
            let entity = world
                .get_entity(current)
                .map_err(|_| "Particle camera parent absent")?;
            let transform = entity
                .get_ref::<Transform>()
                .ok_or("Particle camera transform absent")?;
            let parent = entity.get_ref::<ChildOf>();
            let next = parent.as_ref().map(|value| value.parent());
            hierarchy.push(HierarchyNode {
                entity: current,
                parent: next,
                parent_tick: parent.as_ref().map(DetectChanges::last_changed),
                transform_tick: transform.last_changed(),
                local: transform.to_matrix(),
            });
            let Some(next) = next else { break };
            current = next;
        }
        if hierarchy.last().is_none_or(|node| node.parent.is_some())
            || crate::physics::current_global(world, view)
                .is_none_or(|value| !value.to_matrix().abs_diff_eq(pose, 1.0e-5))
        {
            return Err("Particle camera hierarchy has not propagated".into());
        }
        let entity = world
            .get_entity(view)
            .map_err(|_| "Particle camera absent")?;
        let camera_tick = entity
            .get_ref::<Camera>()
            .ok_or("Particle camera absent")?
            .last_changed();
        let projection_tick = entity
            .get_ref::<Projection>()
            .map(|value| value.last_changed());
        Ok(Self {
            hierarchy,
            view,
            world_from_view: pose,
            camera_tick,
            projection_tick,
        })
    }

    pub(crate) fn is_fresh(&self, world: &World) -> bool {
        let Ok(view) = world.get_entity(self.view) else {
            return false;
        };
        if view
            .get_ref::<Camera>()
            .is_none_or(|camera| camera.last_changed() != self.camera_tick)
            || view
                .get_ref::<Projection>()
                .map(|value| value.last_changed())
                != self.projection_tick
        {
            return false;
        }
        hierarchy_is_fresh(world, &self.hierarchy)
            && crate::physics::current_global(world, self.view).is_some_and(|current| {
                current
                    .to_matrix()
                    .abs_diff_eq(self.world_from_view, 1.0e-5)
            })
    }
}

fn hierarchy_is_fresh(world: &World, hierarchy: &[HierarchyNode]) -> bool {
    hierarchy.iter().all(|node| {
        let Ok(entity) = world.get_entity(node.entity) else {
            return false;
        };
        let Some(transform) = entity.get_ref::<Transform>() else {
            return false;
        };
        let parent = entity.get_ref::<ChildOf>();
        transform.last_changed() == node.transform_tick
            && transform.to_matrix() == node.local
            && parent.as_ref().map(|value| value.parent()) == node.parent
            && parent.as_ref().map(DetectChanges::last_changed) == node.parent_tick
    })
}

/// A snapshot is usable only for the same admitted owner and unchanged
/// hierarchy between scene preparation and render extraction.
#[derive(Clone)]
pub(crate) struct ParticleSceneWitness {
    pub(crate) root: Entity,
    pub(crate) owner: Entity,
    pub(crate) projection: Entity,
    pub(crate) producer_slot: u64,
    pub(crate) owner_identity: Arc<()>,
    pub(crate) projection_identity: Arc<()>,
    scene_nodes: Vec<(usize, Entity)>,
    root_attachment_tick: Tick,
    root_status_tick: Tick,
    projection_parent_tick: Tick,
    hierarchy: Vec<HierarchyNode>,
    world_from_owner: Mat4,
}

impl ParticleSceneWitness {
    #[allow(
        clippy::too_many_arguments,
        reason = "Exact source, owner and projection identities are explicit"
    )]
    pub(crate) fn capture(
        world: &World,
        root: Entity,
        owner: Entity,
        projection: Entity,
        producer_slot: u64,
        owner_identity: Arc<()>,
        projection_identity: Arc<()>,
        nodes: &BTreeMap<Entity, usize>,
    ) -> Result<Self, String> {
        let root_entity = world
            .get_entity(root)
            .map_err(|_| "Particle scene root absent")?;
        let root_attachment_tick = root_entity
            .get_ref::<crate::source_graph_scene_attachment::SourceSceneAttached>()
            .ok_or("Particle scene root is not attached")?
            .last_changed();
        let status = root_entity
            .get_ref::<crate::source_graph_runtime::SourceFrameStatus>()
            .ok_or("Particle scene frame status absent")?;
        if *status != crate::source_graph_runtime::SourceFrameStatus::Updated {
            return Err("Particle scene frame was refused".into());
        }
        let root_status_tick = status.last_changed();
        let parent = world
            .get_entity(projection)
            .ok()
            .and_then(|entity| entity.get_ref::<ChildOf>())
            .ok_or("Particle projection parent absent")?;
        if parent.parent() != owner {
            return Err("Particle projection detached from its owner".into());
        }
        let projection_parent_tick = parent.last_changed();
        let mut hierarchy = Vec::new();
        let mut seen = std::collections::BTreeSet::new();
        let owner_node = *nodes
            .get(&owner)
            .ok_or("Particle owner is not an admitted source node")?;
        let mut scene_nodes = vec![(owner_node, owner)];
        let mut entity = owner;
        let mut reached_root = false;
        for _ in 0..128 {
            if !seen.insert(entity) {
                return Err("Particle hierarchy contains a cycle".into());
            }
            if !reached_root && entity != root && entity != owner {
                let source_node = nodes
                    .get(&entity)
                    .ok_or("Particle owner is outside its admitted source hierarchy")?;
                scene_nodes.push((*source_node, entity));
            }
            reached_root |= entity == root;
            let current = world
                .get_entity(entity)
                .map_err(|_| "Particle hierarchy node absent")?;
            let transform = current
                .get_ref::<Transform>()
                .ok_or("Particle hierarchy transform absent")?;
            let parent = current.get_ref::<ChildOf>();
            let next = parent.as_ref().map(|parent| parent.parent());
            hierarchy.push(HierarchyNode {
                entity,
                parent: next,
                parent_tick: parent.as_ref().map(DetectChanges::last_changed),
                transform_tick: transform.last_changed(),
                local: transform.to_matrix(),
            });
            let Some(next) = next else {
                break;
            };
            entity = next;
        }
        if !reached_root || hierarchy.last().is_none_or(|node| node.parent.is_some()) {
            return Err("Particle hierarchy does not reach an admitted root".into());
        }
        let propagated = world
            .get::<GlobalTransform>(owner)
            .ok_or("Particle owner global transform absent")?
            .to_matrix();
        let current = crate::physics::current_global(world, owner)
            .ok_or("Particle owner hierarchy transform unavailable")?
            .to_matrix();
        if !current.abs_diff_eq(propagated, 1.0e-5) {
            return Err("Particle owner transform has not propagated".into());
        }
        Ok(Self {
            root,
            owner,
            projection,
            producer_slot,
            owner_identity,
            projection_identity,
            scene_nodes,
            root_attachment_tick,
            root_status_tick,
            projection_parent_tick,
            hierarchy,
            world_from_owner: current,
        })
    }

    pub(crate) fn is_fresh(&self, world: &World) -> bool {
        let Ok(root) = world.get_entity(self.root) else {
            return false;
        };
        if root
            .get_ref::<crate::source_graph_scene_attachment::SourceSceneAttached>()
            .is_none_or(|marker| marker.last_changed() != self.root_attachment_tick)
            || root
                .get_ref::<crate::source_graph_runtime::SourceFrameStatus>()
                .is_none_or(|status| {
                    *status != crate::source_graph_runtime::SourceFrameStatus::Updated
                        || status.last_changed() != self.root_status_tick
                })
        {
            return false;
        }
        let projection_parent = world
            .get_entity(self.projection)
            .ok()
            .and_then(|entity| entity.get_ref::<ChildOf>());
        if projection_parent.is_none_or(|parent| {
            parent.parent() != self.owner || parent.last_changed() != self.projection_parent_tick
        }) {
            return false;
        }
        hierarchy_is_fresh(world, &self.hierarchy)
            && world
                .get::<GlobalTransform>(self.owner)
                .is_some_and(|global| {
                    global
                        .to_matrix()
                        .abs_diff_eq(self.world_from_owner, 1.0e-5)
                })
            && crate::physics::current_global(world, self.owner).is_some_and(|current| {
                current
                    .to_matrix()
                    .abs_diff_eq(self.world_from_owner, 1.0e-5)
            })
    }

    pub(crate) fn matches_nodes(&self, nodes: &BTreeMap<usize, Entity>) -> bool {
        self.scene_nodes
            .iter()
            .all(|(source, entity)| nodes.get(source) == Some(entity))
    }
}
impl PreparedHostedParticleFrame {
    pub fn apply(self, world: &mut World) -> Result<(), String> {
        let mut projection = world
            .get_mut::<SourceParticleProjection>(self.projection)
            .ok_or("Particle projection disappeared before frame apply")?;
        if !Arc::ptr_eq(&projection.instance_identity(), &self.identity) {
            return Err("Particle projection replaced before frame apply".into());
        }
        if self.scene_bound {
            projection.apply_scene_bank(self.bank);
        } else {
            projection.apply_prepared_bank(self.bank);
        }
        Ok(())
    }
}

impl HostedSourceParticle {
    pub(crate) fn validate_scene_binding(&self, world: &World) -> Result<(), String> {
        if self.released {
            return Err("Particle host instance released".into());
        }
        let slots = world
            .get_resource::<HostedSlots>()
            .ok_or("Particle host reservations absent")?;
        if [self.producer_slot, self.projection_slot]
            .iter()
            .any(|slot| {
                slots
                    .slots
                    .get(slot)
                    .is_none_or(|reservation| !Arc::ptr_eq(reservation, &self.reservation))
            })
            || slots
                .materials
                .get(&self.material.id())
                .is_none_or(|reservation| !Arc::ptr_eq(reservation, &self.reservation))
        {
            return Err("Particle host reservation replaced".into());
        }
        let owner = world
            .get::<SourceParticleSceneOwner>(self.owner)
            .filter(|owner| Arc::ptr_eq(&owner.instance_identity(), &self.owner_identity))
            .ok_or("Particle host owner absent or replaced")?;
        world
            .get::<SourceParticleProjection>(self.projection)
            .filter(|projection| {
                Arc::ptr_eq(&projection.instance_identity(), &self.projection_identity)
            })
            .ok_or("Particle host projection absent or replaced")?;
        let queue = world
            .get_resource::<SourceParticleComputeQueue>()
            .ok_or("Particle compute queue absent")?;
        owner.validate_scene_binding(queue).map_err(str::to_owned)
    }

    /// Observe the existing lifecycle. Encoded commands alone are not GPU completion;
    /// callers still need the current renderer inventory for the bound output.
    pub(crate) fn observe(
        &self,
        world: &World,
        root: Entity,
    ) -> Result<HostedParticleObservation, String> {
        self.validate_scene_binding(world)?;
        let owner = world
            .get::<SourceParticleSceneOwner>(self.owner)
            .ok_or("Particle host owner absent")?;
        let projection = world
            .get::<SourceParticleProjection>(self.projection)
            .ok_or("Particle host projection absent")?;
        if let Some(error) = owner.error().or_else(|| projection.error()) {
            return Err(format!("Particle host failed: {error}"));
        }
        let material = world
            .get_resource::<Assets<SourceGraphMaterial>>()
            .and_then(|assets| assets.get(&self.material))
            .ok_or("Particle host material absent")?;
        let output = projection.output_buffer();
        let expected = crate::source_particle_compute::draw::ParticleFrameOwner {
            slot: self.projection_slot,
            identity: self.projection_identity.clone(),
            output: output.id(),
        };
        let bound = material.extension.is_source_state_buffer(&output)
            && material
                .extension
                .particle_frame_owner()
                .is_some_and(|owner| owner.matches(&expected));
        if projection.encoded_frames() > 0 && !bound {
            return Err("Particle host output binding changed".into());
        }
        Ok(HostedParticleObservation {
            // A deliberately stopped emitter can retain its admitted initial
            // state without producing new simulation steps.
            initialized: projection.encoded_frames() > 0 && bound,
            current_frame: projection.scene_frame_current(world, root),
            producer_steps: owner.encoded_steps(),
            projection_frames: projection.encoded_frames(),
            material: self.material.id(),
        })
    }

    pub(crate) fn bind_scene(&self, world: &mut World) -> Result<(), String> {
        self.validate_scene_binding(world)?;
        world.resource_scope(|world, mut queue: Mut<SourceParticleComputeQueue>| {
            world
                .get_mut::<SourceParticleSceneOwner>(self.owner)
                .ok_or_else(|| "Particle host owner absent".to_owned())?
                .bind_scene(&mut queue)
                .map_err(str::to_owned)
        })
    }

    pub(crate) fn scene_identity(&self) -> Option<(Entity, Entity, Arc<()>, Arc<()>)> {
        (!self.released).then(|| {
            (
                self.owner,
                self.projection,
                self.owner_identity.clone(),
                self.projection_identity.clone(),
            )
        })
    }

    pub(crate) fn seal_scene_frame(
        &self,
        world: &mut World,
        root: Entity,
        nodes: &BTreeMap<Entity, usize>,
    ) -> Result<(), String> {
        let witness = ParticleSceneWitness::capture(
            world,
            root,
            self.owner,
            self.projection,
            self.producer_slot,
            self.owner_identity.clone(),
            self.projection_identity.clone(),
            nodes,
        )?;
        let mut projection = world
            .get_mut::<SourceParticleProjection>(self.projection)
            .filter(|projection| {
                Arc::ptr_eq(&projection.instance_identity(), &self.projection_identity)
            })
            .ok_or("Particle projection absent or replaced before frame seal")?;
        projection.seal_scene_frame(witness);
        Ok(())
    }
    /// Install an already verified transport on an admitted scene. All owners
    /// are prepared before changing the world; a later refusal releases every
    /// instance created by this batch. Model and package pins remain the
    /// responsibility of the transport decoder and the scene owner.
    pub fn install_batch(
        world: &mut World,
        source: &ResolvedSourceParticleTransport,
        carrier: ContentHash,
        nodes: &BTreeMap<usize, Entity>,
        materials: &BTreeMap<(usize, usize), Handle<SourceGraphMaterial>>,
        slots: &[(u64, u64)],
    ) -> Result<Vec<Self>, String> {
        if source.contract.carrier_sha256 != carrier {
            return Err("Particle carrier differs from attached host".into());
        }
        if source.contract.owners.is_empty() || slots.len() != source.contract.owners.len() {
            return Err("Particle host slot count differs".into());
        }
        let mut plans = Vec::with_capacity(slots.len());
        for declaration in &source.contract.owners {
            let node = *nodes
                .get(&declaration.source_node)
                .ok_or("Particle source node not attached")?;
            let material = materials
                .get(&(declaration.source_node, declaration.source_material))
                .ok_or("Particle source material not attached")?
                .clone();
            let prepared = PreparedSourceParticleOwner::prepare(declaration, &source.resources)?;
            plans.push((node, material, prepared));
        }
        let mut installed: Vec<Self> = Vec::with_capacity(plans.len());
        for ((node, material, prepared), &(producer, projection)) in plans.into_iter().zip(slots) {
            match Self::install(world, prepared, node, material, producer, projection) {
                Ok(instance) => installed.push(instance),
                Err(error) => {
                    for instance in &mut installed {
                        instance.release(world);
                    }
                    return Err(error);
                }
            }
        }
        Ok(installed)
    }

    /// Prepare every owner's current scene frame before applying any of them.
    pub fn update_scene_frames(
        world: &mut World,
        instances: &[Self],
        source_world: Mat4,
        object_basis: Mat4,
        target_world_from_view: Mat4,
        clip_from_view: Mat4,
    ) -> Result<(), String> {
        Self::update_scene_frames_inner(
            world,
            instances,
            source_world,
            object_basis,
            target_world_from_view,
            clip_from_view,
            false,
        )
    }

    pub(crate) fn update_scene_frames_for_camera(
        world: &mut World,
        instances: &[Self],
        source_world: Mat4,
        object_basis: Mat4,
        target_world_from_view: Mat4,
        clip_from_view: Mat4,
    ) -> Result<(), String> {
        Self::update_scene_frames_inner(
            world,
            instances,
            source_world,
            object_basis,
            target_world_from_view,
            clip_from_view,
            true,
        )
    }

    fn update_scene_frames_inner(
        world: &mut World,
        instances: &[Self],
        source_world: Mat4,
        object_basis: Mat4,
        target_world_from_view: Mat4,
        clip_from_view: Mat4,
        scene_bound: bool,
    ) -> Result<(), String> {
        let plans = instances
            .iter()
            .map(|instance| {
                instance.prepare_current_scene_frame_inner(
                    world,
                    source_world,
                    object_basis,
                    target_world_from_view,
                    clip_from_view,
                    scene_bound,
                )
            })
            .collect::<Result<Vec<_>, _>>()?;
        for plan in plans {
            plan.apply(world)?;
        }
        Ok(())
    }

    pub fn invalidate_current_frame(&self, world: &mut World) {
        if let Some(mut projection) = world.get_mut::<SourceParticleProjection>(self.projection)
            && Arc::ptr_eq(&projection.instance_identity(), &self.projection_identity)
        {
            projection.invalidate_declared_frame();
        }
    }
    pub fn cancel_current_frame(&self, world: &mut World) {
        let (retired, request) = if let Some(mut projection) =
            world.get_mut::<SourceParticleProjection>(self.projection)
            && Arc::ptr_eq(&projection.instance_identity(), &self.projection_identity)
        {
            let request = projection.queue_identity();
            (projection.cancel_declared_frame(), request)
        } else {
            (false, None)
        };
        if retired
            && let Some(request) = request
            && let Some(mut queue) = world.get_resource_mut::<SourceParticleComputeQueue>()
        {
            queue.remove_exact(
                self.projection_slot,
                &self.projection_identity,
                std::slice::from_ref(&request),
            );
        }
    }
    pub fn prepare_current_scene_frame(
        &self,
        world: &World,
        source_world: Mat4,
        object_basis: Mat4,
        target_world_from_view: Mat4,
        clip_from_view: Mat4,
    ) -> Result<PreparedHostedParticleFrame, String> {
        self.prepare_current_scene_frame_inner(
            world,
            source_world,
            object_basis,
            target_world_from_view,
            clip_from_view,
            false,
        )
    }

    fn prepare_current_scene_frame_inner(
        &self,
        world: &World,
        source_world: Mat4,
        object_basis: Mat4,
        target_world_from_view: Mat4,
        clip_from_view: Mat4,
        scene_bound: bool,
    ) -> Result<PreparedHostedParticleFrame, String> {
        self.validate_frame(world, 0.0, &[])?;
        let pose = world
            .get::<GlobalTransform>(self.owner)
            .ok_or("Current particle owner transform absent")?
            .to_matrix();
        let frame = crate::source_particle_frame::SourceParticleFrame::from_current_scene(
            source_world,
            pose,
            object_basis,
            target_world_from_view,
            clip_from_view,
        )?;
        let projection = world
            .get::<SourceParticleProjection>(self.projection)
            .unwrap();
        let bank = projection.prepare_frame(&frame)?;
        Ok(PreparedHostedParticleFrame {
            projection: self.projection,
            identity: self.projection_identity.clone(),
            bank,
            scene_bound,
        })
    }
    /// Select free slots for immediate installation on this exclusive World.
    /// These are transient compute identities, never saved author metadata.
    pub fn available_slot_pairs(
        world: &mut World,
        count: usize,
    ) -> Result<Vec<(u64, u64)>, String> {
        if count == 0 || count > 4096 || !world.contains_resource::<SourceParticleComputeQueue>() {
            return Err("Particle slot budget or compute queue unavailable".into());
        }
        let mut used = std::collections::BTreeSet::new();
        if let Some(reservations) = world.get_resource::<HostedSlots>() {
            used.extend(reservations.slots.keys().copied());
        }
        used.extend(
            world
                .query::<&SourceParticleSceneOwner>()
                .iter(world)
                .map(SourceParticleSceneOwner::slot),
        );
        used.extend(
            world
                .query::<&SourceParticleProjection>()
                .iter(world)
                .map(SourceParticleProjection::compute_slot),
        );
        let mut slots = Vec::with_capacity(count * 2);
        let mut candidate = 0_u64;
        while slots.len() < count * 2 {
            if !used.contains(&candidate)
                && !world
                    .resource::<SourceParticleComputeQueue>()
                    .contains_slot(candidate)
            {
                slots.push(candidate);
            }
            candidate = candidate
                .checked_add(1)
                .ok_or("Particle slot space exhausted")?;
        }
        Ok(slots
            .as_chunks::<2>()
            .0
            .iter()
            .map(|pair| (pair[0], pair[1]))
            .collect())
    }
    pub fn install(
        world: &mut World,
        prepared: PreparedSourceParticleOwner,
        owner: Entity,
        material: Handle<SourceGraphMaterial>,
        producer_slot: u64,
        projection_slot: u64,
    ) -> Result<Self, String> {
        if producer_slot == projection_slot
            || world.get_entity(owner).is_err()
            || world.get::<SourceParticleSceneOwner>(owner).is_some()
            || !world.contains_resource::<Assets<Shader>>()
            || !world.contains_resource::<Assets<Image>>()
            || !world.contains_resource::<Assets<ShaderBuffer>>()
            || !world.contains_resource::<SourceParticleComputeQueue>()
        {
            return Err("Particle host node, resources or distinct slots unavailable".into());
        }
        let valid_material = world
            .get_resource::<Assets<SourceGraphMaterial>>()
            .and_then(|a| a.get(&material))
            .is_some_and(|m| {
                m.extension.program_key.requires_source_state
                    && !crate::source_graph::SourceGraphKey::from(&m.extension).source_state_ready
            });
        if !valid_material {
            return Err("Particle host requires an admitted source-state material".into());
        }
        if world
            .get_resource::<HostedSlots>()
            .is_some_and(|r| r.materials.contains_key(&material.id()))
        {
            return Err("Particle host material already owned by another instance".into());
        }
        for slot in [producer_slot, projection_slot] {
            if world
                .resource::<SourceParticleComputeQueue>()
                .contains_slot(slot)
                || world
                    .get_resource::<HostedSlots>()
                    .is_some_and(|r| r.slots.contains_key(&slot))
                || world
                    .query::<&SourceParticleSceneOwner>()
                    .iter(world)
                    .any(|o| o.slot() == slot)
                || world
                    .query::<&SourceParticleProjection>()
                    .iter(world)
                    .any(|p| p.compute_slot() == slot)
            {
                return Err("Particle host compute slot already owned".into());
            }
        }
        let instance = world.resource_scope(|world, mut shaders: Mut<Assets<Shader>>| {
            world.resource_scope(|world, mut images: Mut<Assets<Image>>| {
                prepared.instantiate(
                    &mut shaders,
                    &mut images,
                    &mut world.resource_mut::<Assets<ShaderBuffer>>(),
                    owner,
                    material.clone(),
                    producer_slot,
                    projection_slot,
                )
            })
        })?;
        let owner_identity = instance.owner.instance_identity();
        let projection_identity = instance.projection.instance_identity();
        world.entity_mut(owner).insert(instance.owner);
        let projection = world.spawn((instance.projection, ChildOf(owner))).id();
        let reservation = Arc::new(());
        world.init_resource::<HostedSlots>();
        for slot in [producer_slot, projection_slot] {
            world
                .resource_mut::<HostedSlots>()
                .slots
                .insert(slot, reservation.clone());
        }
        world
            .resource_mut::<HostedSlots>()
            .materials
            .insert(material.id(), reservation.clone());
        Ok(Self {
            owner,
            projection,
            producer_slot,
            projection_slot,
            owner_identity,
            projection_identity,
            reservation,
            material,
            buffers: instance.buffers,
            shaders: instance.shaders,
            curve: Some(instance.curve),
            released: false,
        })
    }

    pub fn queue_frame(
        &self,
        world: &mut World,
        delta: f32,
        events: &[forge_assets::source_particle_emission::SourceEmissionEvent],
        reset: bool,
    ) -> Result<(), String> {
        self.validate_frame(world, delta, events)?;
        world
            .get_mut::<SourceParticleSceneOwner>(self.owner)
            .unwrap()
            .queue_frame(delta, events, reset)
            .map_err(str::to_owned)
    }

    pub(crate) fn validate_frame(
        &self,
        world: &World,
        delta: f32,
        events: &[forge_assets::source_particle_emission::SourceEmissionEvent],
    ) -> Result<(), String> {
        if self.released {
            return Err("Particle host instance released".into());
        }
        let owner = world
            .get::<SourceParticleSceneOwner>(self.owner)
            .ok_or("Particle host owner absent")?;
        if !Arc::ptr_eq(&owner.instance_identity(), &self.owner_identity) {
            return Err("Particle host owner replaced".into());
        }
        let projection = world
            .get::<SourceParticleProjection>(self.projection)
            .filter(|p| Arc::ptr_eq(&p.instance_identity(), &self.projection_identity))
            .ok_or("Particle host projection absent or replaced")?;
        if let Some(error) = projection.error() {
            return Err(format!("Particle host projection failed: {error}"));
        }
        owner.validate_frame(delta, events).map_err(str::to_owned)
    }

    pub fn release(&mut self, world: &mut World) {
        if self.released {
            return;
        }
        let ours = world
            .get::<SourceParticleSceneOwner>(self.owner)
            .is_some_and(|o| Arc::ptr_eq(&o.instance_identity(), &self.owner_identity));
        let our_projection = world
            .get::<SourceParticleProjection>(self.projection)
            .is_some_and(|p| Arc::ptr_eq(&p.instance_identity(), &self.projection_identity));
        let producer_requests = world
            .get::<SourceParticleSceneOwner>(self.owner)
            .filter(|owner| Arc::ptr_eq(&owner.instance_identity(), &self.owner_identity))
            .map_or_else(Vec::new, SourceParticleSceneOwner::queue_identities);
        let projection_request = world
            .get::<SourceParticleProjection>(self.projection)
            .filter(|projection| {
                Arc::ptr_eq(&projection.instance_identity(), &self.projection_identity)
            })
            .and_then(SourceParticleProjection::queue_identity);
        if ours {
            world
                .entity_mut(self.owner)
                .remove::<SourceParticleSceneOwner>();
        }
        if our_projection {
            world.despawn(self.projection);
        }
        for slot in [self.producer_slot, self.projection_slot] {
            let owned = world
                .get_resource::<HostedSlots>()
                .and_then(|r| r.slots.get(&slot))
                .is_some_and(|id| Arc::ptr_eq(id, &self.reservation));
            if owned {
                world.resource_mut::<HostedSlots>().slots.remove(&slot);
                let foreign = world
                    .query::<&SourceParticleSceneOwner>()
                    .iter(world)
                    .any(|o| o.slot() == slot)
                    || world
                        .query::<&SourceParticleProjection>()
                        .iter(world)
                        .any(|p| p.compute_slot() == slot);
                if !foreign
                    && let Some(mut queue) = world.get_resource_mut::<SourceParticleComputeQueue>()
                {
                    if slot == self.producer_slot {
                        queue.remove_exact(slot, &self.owner_identity, &producer_requests);
                    } else if let Some(request) = &projection_request {
                        queue.remove_exact(
                            slot,
                            &self.projection_identity,
                            std::slice::from_ref(request),
                        );
                    }
                }
            }
        }
        let owned_material = world
            .get_resource::<HostedSlots>()
            .and_then(|r| r.materials.get(&self.material.id()))
            .is_some_and(|id| Arc::ptr_eq(id, &self.reservation));
        if owned_material {
            world
                .resource_mut::<HostedSlots>()
                .materials
                .remove(&self.material.id());
        }
        if owned_material
            && let Some(mut materials) = world.get_resource_mut::<Assets<SourceGraphMaterial>>()
            && let Some(mut material) = materials.get_mut(&self.material)
            && material.extension.is_source_state_buffer(&self.buffers[4])
        {
            material.extension.clear_source_state_buffer();
        }
        self.released = true;
        self.buffers.clear();
        self.shaders.clear();
        self.curve.take();
    }
}

#[cfg(test)]
pub(crate) mod tests {
    use super::*;
    use crate::source_graph::{
        SourceGraphExtension, SourceGraphForwardState, SourceGraphKey,
        SourceGraphStateBufferBinding, SourceGraphUniforms, SourceGraphUvRequirements,
    };

    #[test]
    fn scene_witness_rejects_reparenting_and_local_aba_until_a_new_frame() {
        let mut world = World::new();
        let root = world
            .spawn((
                Transform::IDENTITY,
                crate::source_graph_scene_attachment::SourceSceneAttached,
                crate::source_graph_runtime::SourceFrameStatus::Updated,
            ))
            .id();
        let foreign = world.spawn(Transform::IDENTITY).id();
        let owner = world.spawn((Transform::IDENTITY, ChildOf(root))).id();
        let projection = world.spawn(ChildOf(owner)).id();
        let nodes = BTreeMap::from([(owner, 0)]);
        let capture = |world: &World| {
            ParticleSceneWitness::capture(
                world,
                root,
                owner,
                projection,
                7,
                Arc::new(()),
                Arc::new(()),
                &nodes,
            )
            .unwrap()
        };
        let first = capture(&world);
        world.increment_change_tick();
        assert!(first.is_fresh(&world));
        let unchanged = capture(&world);
        world.increment_change_tick();
        assert!(first.is_fresh(&world));
        assert!(unchanged.is_fresh(&world));
        world.entity_mut(owner).insert(ChildOf(foreign));
        assert!(!first.is_fresh(&world));
        world.entity_mut(owner).insert(ChildOf(root));
        assert!(!first.is_fresh(&world));

        let next = capture(&world);
        world.increment_change_tick();
        assert!(next.is_fresh(&world));
        world.get_mut::<Transform>(owner).unwrap().translation.x = 1.0;
        world.get_mut::<Transform>(owner).unwrap().translation.x = 0.0;
        assert!(!next.is_fresh(&world));
        assert!(capture(&world).is_fresh(&world));

        let projection_frame = capture(&world);
        world.increment_change_tick();
        world.entity_mut(projection).insert(ChildOf(foreign));
        assert!(!projection_frame.is_fresh(&world));
    }

    #[test]
    fn view_witness_rejects_camera_changes_after_capture() {
        let mut world = World::new();
        let camera = world.spawn((Camera3d::default(), Transform::IDENTITY)).id();
        let witness = ParticleViewWitness::capture(&world, camera, Mat4::IDENTITY).unwrap();
        world.increment_change_tick();
        assert!(witness.is_fresh(&world));
        world.get_mut::<Transform>(camera).unwrap().translation.x = 1.0;
        world.get_mut::<Transform>(camera).unwrap().translation.x = 0.0;
        assert!(!witness.is_fresh(&world));
    }

    pub(crate) fn prepared() -> PreparedSourceParticleOwner {
        let (declaration, resources) = crate::source_particle_transport::tests::fixture();
        PreparedSourceParticleOwner::prepare(&declaration, &resources).unwrap()
    }

    pub(crate) fn setup(required: bool) -> (World, Entity, Handle<SourceGraphMaterial>) {
        let mut world = World::new();
        world.init_resource::<Assets<Shader>>();
        world.init_resource::<Assets<Image>>();
        world.init_resource::<Assets<ShaderBuffer>>();
        world.init_resource::<Assets<SourceGraphMaterial>>();
        world.init_resource::<SourceParticleComputeQueue>();
        let key = SourceGraphKey {
            requires_source_state: required,
            source_state_ready: false,
            requires_native_srgb_encoder: false,
            native_srgb_encoder_ready: false,
            requires_deferred_srgb: false,
            deferred_srgb_ready: false,
            requires_preintegrated_brdf: false,
            preintegrated_brdf_ready: false,
            requires_sky_cube: false,
            sky_cube_ready: false,
            prepass_cull: 2,
            shadow_only: false,
            orthographic_shadow_raster: None,
            vertex: 1,
            fragment: 2,
            prepass_vertex: 3,
            prepass_fragment: 4,
            original_alpha_to_mask: false,
            requires_original_vertex_color: false,
            original_uv_requirements: SourceGraphUvRequirements::default(),
            color_write_mask: 15,
            forward_state: SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 4,
                write_depth: true,
            },
        };
        let uniforms = SourceGraphUniforms {
            source_object_from_target_object: Mat4::IDENTITY,
            target_object_from_source_object: Mat4::IDENTITY,
            source_world_from_target_world: Mat4::IDENTITY,
            source_absolute_world_from_target_world: Mat4::IDENTITY,
            source_time_parameters: Vec4::ZERO,
            source_previous_time_parameters: Vec4::ZERO,
            source_scale_mip_min: Vec4::ONE,
            normal_decode_modes: [Vec4::splat(-1.0); 2],
        };
        let extension =
            SourceGraphExtension::from_source_bindings(key, uniforms, 0, vec![]).unwrap();
        let material =
            world
                .resource_mut::<Assets<SourceGraphMaterial>>()
                .add(SourceGraphMaterial {
                    base: default(),
                    extension,
                });
        let node = world.spawn_empty().id();
        (world, node, material)
    }

    fn counts(world: &World) -> (usize, usize, usize) {
        (
            world.resource::<Assets<Shader>>().len(),
            world.resource::<Assets<Image>>().len(),
            world.resource::<Assets<ShaderBuffer>>().len(),
        )
    }

    #[test]
    #[allow(
        clippy::too_many_lines,
        reason = "One scenario verifies package pins, preflight, rollback and successful retry."
    )]
    fn decoded_batch_prepares_before_mutation_and_rolls_back_a_late_refusal() {
        use forge_assets::source_particle_transport::SourceParticleTransport;
        use forge_core::bytes_sha256;

        let (first, resources) = crate::source_particle_transport::tests::fixture();
        let mut second = first.clone();
        second.source_node = 1;
        second.source_component_file_id = "11400001".into();
        let contract = SourceParticleTransport {
            schema_version: 1,
            carrier_sha256: bytes_sha256(b"carrier"),
            source_archive_sha256: bytes_sha256(b"archive"),
            source_graph_sha256: bytes_sha256(b"graph"),
            licence_ref: "LicenseRef-test".into(),
            owners: vec![first, second],
        };
        let wire = serde_json::to_vec(&contract).unwrap();
        let mut resolved = SourceParticleTransport::decode_pinned(
            &wire,
            bytes_sha256(&wire),
            contract.carrier_sha256,
            |name| {
                resources
                    .get(name)
                    .cloned()
                    .ok_or("missing resource".into())
            },
        )
        .unwrap();
        let (mut world, first_node, first_material) = setup(true);
        let second_node = world.spawn_empty().id();
        let material_copy = world
            .resource::<Assets<SourceGraphMaterial>>()
            .get(&first_material)
            .unwrap()
            .clone();
        let second_material = world
            .resource_mut::<Assets<SourceGraphMaterial>>()
            .add(material_copy);
        let nodes = BTreeMap::from([(0, first_node), (1, second_node)]);
        let mut materials = BTreeMap::from([((0, 0), first_material.clone())]);
        assert!(
            HostedSourceParticle::install_batch(
                &mut world,
                &resolved,
                bytes_sha256(b"different carrier"),
                &nodes,
                &materials,
                &[(7, 1), (9, 2)],
            )
            .is_err()
        );
        assert!(
            HostedSourceParticle::install_batch(
                &mut world,
                &resolved,
                contract.carrier_sha256,
                &nodes,
                &materials,
                &[(7, 1), (9, 2)],
            )
            .is_err()
        );
        assert_eq!(counts(&world), (0, 0, 0));

        materials.insert((1, 0), second_material);
        resolved.resources.get_mut("state.bin").unwrap()[0] ^= 1;
        assert!(
            HostedSourceParticle::install_batch(
                &mut world,
                &resolved,
                contract.carrier_sha256,
                &nodes,
                &materials,
                &[(7, 1), (9, 2)],
            )
            .is_err()
        );
        assert_eq!(counts(&world), (0, 0, 0));
        resolved.resources.get_mut("state.bin").unwrap()[0] ^= 1;
        assert!(
            HostedSourceParticle::install_batch(
                &mut world,
                &resolved,
                contract.carrier_sha256,
                &nodes,
                &materials,
                &[(7, 1), (7, 2)],
            )
            .is_err()
        );
        for node in [first_node, second_node] {
            assert!(world.get_entity(node).is_ok());
            assert!(world.get::<SourceParticleSceneOwner>(node).is_none());
        }
        let mut installed = HostedSourceParticle::install_batch(
            &mut world,
            &resolved,
            contract.carrier_sha256,
            &nodes,
            &materials,
            &[(7, 1), (9, 2)],
        )
        .unwrap();
        assert_eq!(installed.len(), 2);
        for node in [first_node, second_node] {
            assert!(world.get::<SourceParticleSceneOwner>(node).is_some());
        }
        for instance in &mut installed {
            instance.release(&mut world);
        }
        for node in [first_node, second_node] {
            assert!(world.get_entity(node).is_ok());
            assert!(world.get::<SourceParticleSceneOwner>(node).is_none());
        }
    }

    #[test]
    fn scene_frames_follow_both_owners_and_recover_after_a_missing_transform() {
        use forge_assets::source_particle_transport::SourceParticleFrameLayout;
        use forge_core::bytes_sha256;

        let (mut declaration, resources) = crate::source_particle_transport::tests::fixture();
        declaration.projection.frame_layout = Some(SourceParticleFrameLayout {
            object_to_world_offset: 0,
            world_to_object_offset: 64,
            relative_world_to_clip_offset: 128,
            camera_position_offset: 192,
            evidence_sha256: bytes_sha256(b"scene frame fixture"),
        });
        let (mut world, first_node, first_material) = setup(true);
        world
            .entity_mut(first_node)
            .insert(GlobalTransform::IDENTITY);
        let second_node = world.spawn(GlobalTransform::IDENTITY).id();
        let material_copy = world
            .resource::<Assets<SourceGraphMaterial>>()
            .get(&first_material)
            .unwrap()
            .clone();
        let second_material = world
            .resource_mut::<Assets<SourceGraphMaterial>>()
            .add(material_copy);
        let first = HostedSourceParticle::install(
            &mut world,
            PreparedSourceParticleOwner::prepare(&declaration, &resources).unwrap(),
            first_node,
            first_material,
            7,
            1,
        )
        .unwrap();
        let second = HostedSourceParticle::install(
            &mut world,
            PreparedSourceParticleOwner::prepare(&declaration, &resources).unwrap(),
            second_node,
            second_material,
            9,
            2,
        )
        .unwrap();
        let mut instances = vec![first, second];
        let update = |world: &mut World, instances: &[HostedSourceParticle]| {
            HostedSourceParticle::update_scene_frames(
                world,
                instances,
                Mat4::IDENTITY,
                Mat4::IDENTITY,
                Mat4::IDENTITY,
                Mat4::IDENTITY,
            )
        };
        update(&mut world, &instances).unwrap();
        world.entity_mut(second_node).remove::<GlobalTransform>();
        assert!(update(&mut world, &instances).is_err());
        world
            .entity_mut(second_node)
            .insert(GlobalTransform::IDENTITY);
        update(&mut world, &instances).unwrap();
        for instance in &mut instances {
            instance.release(&mut world);
        }
    }

    #[test]
    fn admission_refuses_ordinary_missing_and_reserved_resources_before_allocation() {
        let (mut world, node, material) = setup(false);
        assert!(
            HostedSourceParticle::install(&mut world, prepared(), node, material, 7, 1).is_err()
        );
        assert_eq!(counts(&world), (0, 0, 0));
        let (mut world, node, material) = setup(true);
        let foreign = world
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::new(&[0; 16], default()));
        let binding =
            SourceGraphStateBufferBinding::new(foreign.clone(), world.resource()).unwrap();
        world
            .resource_mut::<Assets<SourceGraphMaterial>>()
            .get_mut(&material)
            .unwrap()
            .extension
            .set_source_state_buffer(&binding);
        assert!(
            HostedSourceParticle::install(&mut world, prepared(), node, material.clone(), 7, 1)
                .is_err()
        );
        assert_eq!(counts(&world), (0, 0, 1));
        assert!(
            world
                .resource::<Assets<SourceGraphMaterial>>()
                .get(&material)
                .unwrap()
                .extension
                .is_source_state_buffer(&foreign)
        );
        let (mut world, node, material) = setup(true);
        assert!(
            HostedSourceParticle::install(&mut world, prepared(), node, Handle::default(), 7, 1)
                .is_err()
        );
        assert!(
            HostedSourceParticle::install(&mut world, prepared(), node, material.clone(), 7, 7)
                .is_err()
        );
        assert_eq!(counts(&world), (0, 0, 0));
        let mut ticket =
            HostedSourceParticle::install(&mut world, prepared(), node, material.clone(), 7, 1)
                .unwrap();
        assert_eq!(counts(&world), (2, 1, 5));
        let free = HostedSourceParticle::available_slot_pairs(&mut world, 2).unwrap();
        let selected: std::collections::BTreeSet<_> =
            free.iter().flat_map(|(a, b)| [*a, *b]).collect();
        assert_eq!(selected.len(), 4);
        assert!(!selected.contains(&7) && !selected.contains(&1));
        let other = world.spawn_empty().id();
        assert!(
            HostedSourceParticle::install(&mut world, prepared(), other, material.clone(), 9, 2)
                .is_err()
        );
        assert!(
            HostedSourceParticle::install(&mut world, prepared(), node, material, 9, 2).is_err()
        );
        assert_eq!(counts(&world), (2, 1, 5));
        ticket.release(&mut world);
    }

    #[test]
    fn release_keeps_borrowed_node_material_children_and_allows_reinstallation() {
        let (mut world, node, material) = setup(true);
        let foreign_child = world.spawn(ChildOf(node)).id();
        let mut ticket =
            HostedSourceParticle::install(&mut world, prepared(), node, material.clone(), 7, 1)
                .unwrap();
        assert_eq!(
            world
                .get::<SourceParticleSceneOwner>(node)
                .unwrap()
                .queued_frames(),
            0
        );
        assert!(
            ticket
                .queue_frame(&mut world, f32::NAN, &[], false)
                .is_err()
        );
        ticket.queue_frame(&mut world, 0.25, &[], false).unwrap();
        assert_eq!(
            world
                .get::<SourceParticleSceneOwner>(node)
                .unwrap()
                .queued_frames(),
            1
        );
        let binding =
            SourceGraphStateBufferBinding::new(ticket.buffers[4].clone(), world.resource())
                .unwrap();
        world
            .resource_mut::<Assets<SourceGraphMaterial>>()
            .get_mut(&material)
            .unwrap()
            .extension
            .set_source_state_buffer(&binding);
        let projection = ticket.projection;
        let request = crate::source_particle_compute::SourceParticleComputeRequest::new(
            ticket.shaders[0].clone(),
            "advance".into(),
            ticket.curve.clone().unwrap(),
            [0; 1024],
            ticket.buffers[0].clone(),
            ticket.buffers[1].clone(),
            1,
        )
        .unwrap();
        world
            .resource_mut::<SourceParticleComputeQueue>()
            .insert(7, request.clone());
        world
            .resource_mut::<SourceParticleComputeQueue>()
            .insert(1, request);
        ticket.release(&mut world);
        ticket.release(&mut world);
        assert!(ticket.queue_frame(&mut world, 0.0, &[], false).is_err());
        assert!(world.get_entity(node).is_ok());
        assert!(world.get_entity(foreign_child).is_ok());
        assert!(world.get_entity(projection).is_err());
        assert!(world.get::<SourceParticleSceneOwner>(node).is_none());
        // These raw requests were inserted by the test, not by the hosted
        // components. Release must preserve them until their caller retires them.
        assert!(
            world
                .resource::<SourceParticleComputeQueue>()
                .contains_slot(7)
        );
        assert!(
            world
                .resource::<SourceParticleComputeQueue>()
                .contains_slot(1)
        );
        world.resource_mut::<SourceParticleComputeQueue>().remove(7);
        world.resource_mut::<SourceParticleComputeQueue>().remove(1);
        assert!(
            world
                .resource::<Assets<SourceGraphMaterial>>()
                .get(&material)
                .is_some()
        );
        assert!(
            !SourceGraphKey::from(
                &world
                    .resource::<Assets<SourceGraphMaterial>>()
                    .get(&material)
                    .unwrap()
                    .extension
            )
            .source_state_ready
        );
        assert!(ticket.buffers.is_empty() && ticket.shaders.is_empty() && ticket.curve.is_none());
        let mut replacement =
            HostedSourceParticle::install(&mut world, prepared(), node, material, 7, 1).unwrap();
        replacement.release(&mut world);
    }

    #[test]
    fn stale_ticket_preserves_replaced_components_and_material_binding() {
        let (mut world, node, material) = setup(true);
        let mut ticket =
            HostedSourceParticle::install(&mut world, prepared(), node, material.clone(), 7, 1)
                .unwrap();
        let replacement = world.resource_scope(|world, mut shaders: Mut<Assets<Shader>>| {
            world.resource_scope(|world, mut images: Mut<Assets<Image>>| {
                prepared()
                    .instantiate(
                        &mut shaders,
                        &mut images,
                        &mut world.resource_mut::<Assets<ShaderBuffer>>(),
                        node,
                        material.clone(),
                        7,
                        1,
                    )
                    .unwrap()
            })
        });
        let replacement_id = replacement.owner.instance_identity();
        world.entity_mut(node).insert(replacement.owner);
        world
            .entity_mut(ticket.projection)
            .insert(replacement.projection);
        let binding =
            SourceGraphStateBufferBinding::new(replacement.buffers[4].clone(), world.resource())
                .unwrap();
        world
            .resource_mut::<Assets<SourceGraphMaterial>>()
            .get_mut(&material)
            .unwrap()
            .extension
            .set_source_state_buffer(&binding);
        assert!(ticket.queue_frame(&mut world, 0.0, &[], false).is_err());
        let request = crate::source_particle_compute::SourceParticleComputeRequest::new(
            replacement.shaders[0].clone(),
            "advance".into(),
            replacement.curve.clone(),
            [0; 1024],
            replacement.buffers[0].clone(),
            replacement.buffers[1].clone(),
            1,
        )
        .unwrap();
        world
            .resource_mut::<SourceParticleComputeQueue>()
            .insert(7, request.clone());
        world
            .resource_mut::<SourceParticleComputeQueue>()
            .insert(1, request);
        ticket.release(&mut world);
        assert!(
            world
                .resource::<SourceParticleComputeQueue>()
                .contains_slot(7)
        );
        assert!(
            world
                .resource::<SourceParticleComputeQueue>()
                .contains_slot(1)
        );
        assert!(Arc::ptr_eq(
            &replacement_id,
            &world
                .get::<SourceParticleSceneOwner>(node)
                .unwrap()
                .instance_identity()
        ));
        assert!(
            world
                .get::<SourceParticleProjection>(ticket.projection)
                .is_some()
        );
        assert!(
            world
                .resource::<Assets<SourceGraphMaterial>>()
                .get(&material)
                .unwrap()
                .extension
                .is_source_state_buffer(&replacement.buffers[4])
        );
    }
}
