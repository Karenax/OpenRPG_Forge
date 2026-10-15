//! Continuous resident projection; the caller admits programs, prefixes, mesh
//! suffix and material layout. No vertex/particle data returns from CPU here.
use super::{
    SourceParticleComputeQueue, SourceParticleComputeRequest, SourceParticleComputeStatus,
    SourceParticleSceneOwner, SourceParticleScenePlugin,
};
use crate::source_graph::{SourceGraphMaterial, SourceGraphStateBufferBinding};
use bevy::{prelude::*, render::storage::ShaderBuffer};
use std::{
    collections::BTreeMap,
    sync::{Arc, Mutex},
};

#[derive(Component)]
pub struct SourceParticleProjection {
    owner: Entity,
    slot: u64,
    prototype: SourceParticleComputeRequest,
    pending: Option<SourceParticleComputeRequest>,
    identity: Arc<()>,
    encoded_frames: u64,
    error: Option<String>,
    material: Option<Handle<SourceGraphMaterial>>,
    frame_layout: Option<forge_assets::source_particle_transport::SourceParticleFrameLayout>,
    original_bank: [u8; 1024],
    frame_ready: bool,
}

impl SourceParticleProjection {
    pub(crate) fn instance_identity(&self) -> Arc<()> {
        self.identity.clone()
    }
    pub(crate) const fn compute_slot(&self) -> u64 {
        self.slot
    }
    /// The admitted request must declare GPU prefix copy and producer slot.
    pub fn new(
        owner: Entity,
        slot: u64,
        request: SourceParticleComputeRequest,
    ) -> Result<Self, &'static str> {
        if request.input_copy.is_none()
            || request.after_slot.is_none()
            || request.after_slot == Some(slot)
            || Arc::strong_count(&request.status) != 1
            || request.status() != Ok(SourceParticleComputeStatus::WaitingForResources)
        {
            return Err(
                "Continuous projection needs a new prefix-copy request and distinct producer dependency",
            );
        }
        let original_bank = *request.bank;
        Ok(Self {
            owner,
            slot,
            prototype: request,
            pending: None,
            identity: Arc::new(()),
            encoded_frames: 0,
            error: None,
            material: None,
            frame_layout: None,
            original_bank,
            frame_ready: true,
        })
    }
    #[must_use]
    pub fn with_material(mut self, material: Handle<SourceGraphMaterial>) -> Self {
        self.material = Some(material);
        self
    }
    pub fn with_frame_layout(
        mut self,
        layout: forge_assets::source_particle_transport::SourceParticleFrameLayout,
    ) -> Result<Self, String> {
        layout.validate()?;
        self.frame_layout = Some(layout);
        self.frame_ready = false;
        Ok(self)
    }
    pub fn update_frame(
        &mut self,
        frame: &crate::source_particle_frame::SourceParticleFrame,
    ) -> Result<(), String> {
        let bank = self.prepare_frame(frame)?;
        self.apply_prepared_bank(bank);
        Ok(())
    }
    pub(crate) fn prepare_frame(
        &self,
        frame: &crate::source_particle_frame::SourceParticleFrame,
    ) -> Result<Arc<[u8; 1024]>, String> {
        let layout = self
            .frame_layout
            .as_ref()
            .ok_or("Projection has no declared frame layout")?;
        frame.apply_bank(layout, &self.original_bank).map(Arc::new)
    }
    pub(crate) fn invalidate_declared_frame(&mut self) -> bool {
        if self.frame_layout.is_some() {
            self.frame_ready = false;
            return true;
        }
        false
    }
    pub(crate) fn cancel_declared_frame(&mut self) -> bool {
        if self.invalidate_declared_frame() {
            self.pending = None;
            true
        } else {
            false
        }
    }
    pub(crate) fn apply_prepared_bank(&mut self, bank: Arc<[u8; 1024]>) {
        self.prototype.bank = bank;
        self.frame_ready = true;
    }
    #[must_use]
    pub fn output_buffer(&self) -> Handle<ShaderBuffer> {
        self.prototype.output.clone()
    }
    #[must_use]
    pub const fn encoded_frames(&self) -> u64 {
        self.encoded_frames
    }
    #[must_use]
    pub fn error(&self) -> Option<&str> {
        self.error.as_deref()
    }

    fn advance(
        &mut self,
        owner: &SourceParticleSceneOwner,
        queue: &mut SourceParticleComputeQueue,
    ) -> Result<(), String> {
        if !self.frame_ready {
            return Ok(());
        }
        if let Some(error) = owner.error() {
            return Err(format!("Source projection owner failed: {error}"));
        }
        if self.prototype.after_slot != Some(owner.slot())
            || self
                .prototype
                .input_copy
                .as_ref()
                .is_none_or(|copy| copy.source != owner.current_buffer())
        {
            return Err(
                "Source projection producer or stable publication changed after admission".into(),
            );
        }
        if let Some(pending) = &self.pending {
            match pending.status().map_err(str::to_owned)? {
                SourceParticleComputeStatus::Encoded => {
                    self.encoded_frames = self
                        .encoded_frames
                        .checked_add(1)
                        .ok_or("Projection frame count exhausted")?;
                    self.pending = None;
                }
                SourceParticleComputeStatus::Failed(error) => return Err(error),
                _ => return Ok(()),
            }
        }
        let mut request = self.prototype.clone();
        request.status = Arc::new(Mutex::new(SourceParticleComputeStatus::WaitingForResources));
        queue.insert(self.slot, request.clone());
        self.pending = Some(request);
        Ok(())
    }
}

struct Claim {
    slot: u64,
    identity: Arc<()>,
}
#[derive(Resource, Default)]
struct Projections {
    claims: BTreeMap<Entity, Claim>,
}

pub struct SourceParticleProjectionPlugin;
#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct SourceParticleProjectionUpdate;
impl Plugin for SourceParticleProjectionPlugin {
    fn build(&self, app: &mut App) {
        if !app.is_plugin_added::<SourceParticleScenePlugin>() {
            app.add_plugins(SourceParticleScenePlugin);
        }
        app.init_resource::<Projections>()
            .init_resource::<Assets<ShaderBuffer>>()
            .init_resource::<Assets<SourceGraphMaterial>>()
            .add_systems(
                PostUpdate,
                advance_projections
                    .in_set(SourceParticleProjectionUpdate)
                    .after(bevy::transform::TransformSystems::Propagate)
                    .after(bevy::camera::CameraUpdateSystems),
            );
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn advance_projections(
    mut queue: ResMut<SourceParticleComputeQueue>,
    mut residents: ResMut<Projections>,
    owners: Query<&SourceParticleSceneOwner>,
    mut projections: Query<(Entity, &mut SourceParticleProjection)>,
    buffers: Res<Assets<ShaderBuffer>>,
    mut materials: ResMut<Assets<SourceGraphMaterial>>,
) {
    let current: BTreeMap<_, _> = projections
        .iter()
        .map(|(entity, projection)| {
            (
                entity,
                Claim {
                    slot: projection.slot,
                    identity: projection.identity.clone(),
                },
            )
        })
        .collect();
    for (entity, claim) in &residents.claims {
        if current.get(entity).is_none_or(|next| {
            next.slot != claim.slot || !Arc::ptr_eq(&next.identity, &claim.identity)
        }) {
            queue.remove(claim.slot);
        }
    }
    let mut counts = BTreeMap::<u64, usize>::new();
    for claim in current.values() {
        *counts.entry(claim.slot).or_default() += 1;
    }
    let source_slots: std::collections::BTreeSet<_> =
        owners.iter().map(SourceParticleSceneOwner::slot).collect();
    residents.claims = current;
    for (_, mut projection) in &mut projections {
        if counts[&projection.slot] != 1 || source_slots.contains(&projection.slot) {
            projection.error =
                Some("Source projection has a duplicate or producer-owned slot".into());
        }
        if projection.error.is_some() {
            queue.remove(projection.slot);
            continue;
        }
        let result = owners
            .get(projection.owner)
            .map_err(|_| "Source projection owner removed".to_owned())
            .and_then(|owner| projection.advance(owner, &mut queue));
        if let Err(error) = result {
            projection.error = Some(error);
            queue.remove(projection.slot);
            continue;
        }
        if projection.encoded_frames > 0
            && let Some(material) = &projection.material
        {
            let output = projection.output_buffer();
            let binding = SourceGraphStateBufferBinding::new(output.clone(), &buffers);
            match binding {
                Ok(binding) => {
                    if let Some(existing) = materials.get(material) {
                        if !existing.extension.is_source_state_buffer(&output) {
                            materials
                                .get_mut(material)
                                .unwrap()
                                .extension
                                .set_source_state_buffer(&binding);
                        }
                    } else {
                        projection.error = Some("Source projection material asset absent".into());
                        queue.remove(projection.slot);
                    }
                }
                Err(error) => {
                    projection.error = Some(error.into());
                    queue.remove(projection.slot);
                }
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::super::{SourceParticleComputeSequence, SourceParticleEmitterSequence};
    use super::*;
    use forge_assets::source_particle_emission::SourceSingleBurstLoopParameters;

    fn fixture() -> (
        App,
        Entity,
        SourceParticleComputeRequest,
        Handle<ShaderBuffer>,
    ) {
        let mut app = App::new();
        app.add_plugins(SourceParticleProjectionPlugin);
        let mut assets = Assets::<ShaderBuffer>::default();
        let pair: [Handle<ShaderBuffer>; 2] =
            std::array::from_fn(|_| assets.add(ShaderBuffer::new(&[0_u8; 16], default())));
        let stable = assets.add(ShaderBuffer::new(&[0_u8; 16], default()));
        let input = assets.add(ShaderBuffer::new(&[0_u8; 32], default()));
        let output = assets.add(ShaderBuffer::new(&[0_u8; 32], default()));
        let compute = SourceParticleComputeSequence::new(
            90,
            Handle::default(),
            "advance".into(),
            Handle::default(),
            pair,
            1,
        )
        .unwrap()
        .with_publication_buffer(stable.clone(), 16)
        .unwrap();
        let controller = SourceParticleEmitterSequence::new(
            compute,
            SourceSingleBurstLoopParameters {
                loop_duration_seconds: 3.0,
                loop_count: 1,
                burst_delay_seconds: 0.0,
                burst_count: 1.0,
                burst_active: true,
                initial_play: true,
                prewarm_steps: 0,
            },
        )
        .unwrap();
        let mut owner = SourceParticleSceneOwner::new(controller, |command, _| {
            if matches!(command,super::super::SourceParticleEmissionCommand::Allocate {count} if count.to_bits()!=1.0_f32.to_bits())
            {
                return Err("Test requires Count1".into());
            }
            Ok([0; 1024])
        });
        owner.queue_frame(0.0, &[], false).unwrap();
        let owner = app.world_mut().spawn(owner).id();
        let request = SourceParticleComputeRequest::new(
            Handle::default(),
            "project".into(),
            Handle::default(),
            [0; 1024],
            input,
            output.clone(),
            1,
        )
        .unwrap()
        .copy_input_prefix_from(stable, 16)
        .unwrap()
        .after_slot(90)
        .unwrap();
        (app, owner, request, output)
    }

    #[test]
    fn component_refreshes_without_checkpoint_and_retires_only_its_consumer_slot() {
        let (mut app, owner, request, output) = fixture();
        let projection = app
            .world_mut()
            .spawn(
                SourceParticleProjection::new(owner, 2, request)
                    .unwrap()
                    .with_frame_layout(
                        forge_assets::source_particle_transport::SourceParticleFrameLayout {
                            object_to_world_offset: 0,
                            world_to_object_offset: 64,
                            relative_world_to_clip_offset: 128,
                            camera_position_offset: 192,
                            evidence_sha256: forge_core::bytes_sha256(
                                b"generated projection frame test",
                            ),
                        },
                    )
                    .unwrap(),
            )
            .id();
        app.update();
        assert!(
            !app.world()
                .resource::<SourceParticleComputeQueue>()
                .contains_slot(2)
        );
        let frame = crate::source_particle_frame::SourceParticleFrame {
            source_world_from_source_object: Mat4::from_translation(Vec3::new(3., 2., 1.)),
            source_relative_world_to_clip: Mat4::IDENTITY,
            source_camera_position: Vec3::new(0., 0., -4.),
        };
        app.world_mut()
            .get_mut::<SourceParticleProjection>(projection)
            .unwrap()
            .update_frame(&frame)
            .unwrap();
        app.update();
        assert!(
            app.world()
                .resource::<SourceParticleComputeQueue>()
                .contains_slot(2)
        );
        let pending = app.world().resource::<SourceParticleComputeQueue>().0[&2][0]
            .bank
            .clone();
        let mut next = frame;
        next.source_camera_position.x = 9.0;
        app.world_mut()
            .get_mut::<SourceParticleProjection>(projection)
            .unwrap()
            .update_frame(&next)
            .unwrap();
        assert_eq!(
            *app.world().resource::<SourceParticleComputeQueue>().0[&2][0].bank,
            *pending
        );
        for request in &app.world().resource::<SourceParticleComputeQueue>().0[&90] {
            request.set_status(SourceParticleComputeStatus::Encoded);
        }
        app.world().resource::<SourceParticleComputeQueue>().0[&2][0]
            .set_status(SourceParticleComputeStatus::Encoded);
        app.update();
        assert_ne!(
            *app.world().resource::<SourceParticleComputeQueue>().0[&2][0].bank,
            *pending
        );
        let component = app
            .world()
            .get::<SourceParticleProjection>(projection)
            .unwrap();
        assert_eq!(component.encoded_frames(), 1);
        assert_eq!(component.output_buffer(), output);
        assert!(matches!(
            app.world().resource::<SourceParticleComputeQueue>().0[&2][0].status(),
            Ok(SourceParticleComputeStatus::WaitingForResources)
        ));
        app.world_mut().despawn(projection);
        app.update();
        let queue = app.world().resource::<SourceParticleComputeQueue>();
        assert!(!queue.contains_slot(2));
        assert!(queue.contains_slot(90));
    }
}
