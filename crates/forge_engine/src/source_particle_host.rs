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
use bevy::{prelude::*, render::storage::ShaderBuffer};
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
}
impl PreparedHostedParticleFrame {
    pub fn apply(self, world: &mut World) -> Result<(), String> {
        let mut projection = world
            .get_mut::<SourceParticleProjection>(self.projection)
            .ok_or("Particle projection disappeared before frame apply")?;
        if !Arc::ptr_eq(&projection.instance_identity(), &self.identity) {
            return Err("Particle projection replaced before frame apply".into());
        }
        projection.apply_prepared_bank(self.bank);
        Ok(())
    }
}

impl HostedSourceParticle {
    pub fn invalidate_current_frame(&self, world: &mut World) {
        if let Some(mut projection) = world.get_mut::<SourceParticleProjection>(self.projection)
            && Arc::ptr_eq(&projection.instance_identity(), &self.projection_identity)
        {
            projection.invalidate_declared_frame();
        }
    }
    pub fn cancel_current_frame(&self, world: &mut World) {
        let retired = if let Some(mut projection) =
            world.get_mut::<SourceParticleProjection>(self.projection)
            && Arc::ptr_eq(&projection.instance_identity(), &self.projection_identity)
        {
            projection.cancel_declared_frame()
        } else {
            false
        };
        if retired && let Some(mut queue) = world.get_resource_mut::<SourceParticleComputeQueue>() {
            queue.remove(self.projection_slot);
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
        if world
            .get::<SourceParticleProjection>(self.projection)
            .is_none_or(|p| !Arc::ptr_eq(&p.instance_identity(), &self.projection_identity))
        {
            return Err("Particle host projection absent or replaced".into());
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
                    queue.remove(slot);
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
        assert!(
            !world
                .resource::<SourceParticleComputeQueue>()
                .contains_slot(7)
        );
        assert!(
            !world
                .resource::<SourceParticleComputeQueue>()
                .contains_slot(1)
        );
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
