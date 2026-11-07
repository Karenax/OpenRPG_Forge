//! Continuous resident projection; the caller admits programs, prefixes, mesh
//! suffix and material layout. No vertex/particle data returns from CPU here.
use super::{
    SourceParticleComputeQueue, SourceParticleComputeRequest, SourceParticleComputeStatus,
    SourceParticleSceneOwner, SourceParticleScenePlugin,
};
use crate::source_graph::{SourceGraphMaterial, SourceGraphStateBufferBinding};
use bevy::{
    ecs::{
        change_detection::Tick,
        system::{SystemParamItem, SystemState},
    },
    prelude::*,
    render::{
        Extract, ExtractSchedule, MainWorld, RenderApp,
        extract_resource::{ExtractResource, ExtractResourcePlugin, extract_resource},
        storage::ShaderBuffer,
    },
};
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
    scene_bound: bool,
    scene_witness: Option<Arc<crate::source_particle_host::ParticleSceneWitness>>,
}

/// Main-world camera snapshot used for every hosted projection in this frame.
#[derive(Resource, Clone)]
pub(crate) struct SourceParticleCameraSnapshot {
    pub(crate) view: Entity,
    pub(crate) world_from_view: Mat4,
    pub(crate) clip_from_view: Mat4,
    pub(crate) bevy_taa: bool,
    pub(crate) witness: Option<Arc<crate::source_particle_host::ParticleViewWitness>>,
    pub(crate) clock: Option<crate::source_graph_runtime::SourceClockWitness>,
    pub(crate) creation_tick: Option<Tick>,
}

#[derive(Clone)]
pub(super) struct RasterInput {
    pub(super) owner: Arc<()>,
    pub(super) camera: SourceParticleCameraSnapshot,
    pub(super) camera_tick: Option<Tick>,
    pub(super) relative_offset: usize,
    pub(super) bank: Arc<[u8; 1024]>,
    pub(super) witness: Option<Arc<crate::source_particle_host::ParticleSceneWitness>>,
}

/// Current extracted source banks are independent of any delayed GPU request.
#[derive(Resource, Clone, Default, ExtractResource)]
pub(super) struct RasterInputs(pub(super) BTreeMap<u64, RasterInput>);

/// Rebuilt from the main world for each extraction; no old admission survives
/// an unavailable source resource. Slots of other compute clients are absent.
#[derive(Resource, Default)]
pub(super) struct SceneValidation {
    pub(super) producers: BTreeMap<u64, Arc<()>>,
    pub(super) projections: BTreeMap<u64, Arc<()>>,
}

impl SceneValidation {
    pub(super) fn producer_allowed(&self, slot: u64, identity: &Arc<()>) -> bool {
        self.producers
            .get(&slot)
            .is_some_and(|current| Arc::ptr_eq(current, identity))
    }

    pub(super) fn projection_allowed(&self, slot: u64, identity: &Arc<()>) -> bool {
        self.projections
            .get(&slot)
            .is_some_and(|current| Arc::ptr_eq(current, identity))
    }
}

impl SourceParticleProjection {
    pub(crate) fn scene_frame_current(&self, world: &World, root: Entity) -> bool {
        self.frame_ready
            && self.scene_bound
            && self
                .scene_witness
                .as_ref()
                .is_some_and(|witness| witness.root == root && witness.is_fresh(world))
    }

    pub(crate) fn instance_identity(&self) -> Arc<()> {
        self.identity.clone()
    }
    pub(crate) const fn compute_slot(&self) -> u64 {
        self.slot
    }
    pub(crate) fn queue_identity(&self) -> Option<super::RequestIdentity> {
        self.pending.as_ref().map(|request| request.status.clone())
    }
    fn remove_queued_request(&self, queue: &mut SourceParticleComputeQueue) {
        if let Some(pending) = &self.pending {
            queue.remove_exact(
                self.slot,
                &self.identity,
                std::slice::from_ref(&pending.status),
            );
        }
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
            scene_bound: false,
            scene_witness: None,
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
            self.scene_bound = false;
            self.scene_witness = None;
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
        self.scene_bound = false;
        self.scene_witness = None;
    }
    pub(crate) fn apply_scene_bank(&mut self, bank: Arc<[u8; 1024]>) {
        self.apply_prepared_bank(bank);
        self.scene_bound = true;
    }
    pub(crate) fn seal_scene_frame(
        &mut self,
        witness: crate::source_particle_host::ParticleSceneWitness,
    ) {
        self.scene_witness = Some(Arc::new(witness));
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
        if !queue.free_or_owned(self.slot, &self.identity) || !owns_queued_request(queue, self) {
            return Err("Source particle projection slot was replaced".into());
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
        request.queue_owner = Some(self.identity.clone());
        request.raster_owner = self.scene_bound.then(|| self.identity.clone());
        queue.insert(self.slot, request.clone());
        self.pending = Some(request);
        Ok(())
    }
}

struct Claim {
    slot: u64,
    identity: Arc<()>,
    queue_identity: Option<super::RequestIdentity>,
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
            .init_resource::<RasterInputs>()
            .add_plugins(ExtractResourcePlugin::<RasterInputs>::default())
            .init_resource::<Assets<ShaderBuffer>>()
            .init_resource::<Assets<SourceGraphMaterial>>()
            .add_systems(
                PostUpdate,
                advance_projections
                    .in_set(SourceParticleProjectionUpdate)
                    .after(bevy::transform::TransformSystems::Propagate)
                    .after(bevy::camera::CameraUpdateSystems),
            );
        if let Some(render) = app.get_sub_app_mut(RenderApp) {
            render.init_resource::<SceneValidation>().add_systems(
                ExtractSchedule,
                (
                    refresh_scene_requests_after_render
                        .before(extract_resource::<SourceParticleComputeQueue, ()>)
                        .before(extract_resource::<RasterInputs, ()>),
                    remove_missing_extracted_resources
                        .after(extract_resource::<SourceParticleComputeQueue, ()>)
                        .after(extract_resource::<RasterInputs, ()>),
                ),
            );
        }
        super::draw::install(app);
    }
}

type SceneRefreshParams = (
    ResMut<'static, SourceParticleComputeQueue>,
    Res<'static, super::scene::SceneOwners>,
    Res<'static, Projections>,
    ResMut<'static, RasterInputs>,
    Query<'static, 'static, (Entity, &'static mut SourceParticleSceneOwner)>,
    Query<'static, 'static, (Entity, &'static mut SourceParticleProjection)>,
);

/// With pipelined rendering, the normal Main update can finish before the
/// preceding render has encoded its request. Extraction starts only after that
/// render completes, so refresh the same owners here before copying the queue.
/// This does not create an asset, replay a dispatch or change a legacy owner.
#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy extract system parameters"
)]
fn refresh_scene_requests_after_render(
    mut main: ResMut<MainWorld>,
    mut validation: ResMut<SceneValidation>,
    mut state: Local<Option<SystemState<SceneRefreshParams>>>,
) {
    validation.producers.clear();
    validation.projections.clear();
    let state = state.get_or_insert_with(|| SystemState::new(&mut main));
    match state.get_mut(&mut main) {
        Ok(params) => refresh_scene_requests(params),
        Err(error) => {
            warn!("Source particle scene refresh unavailable: {error}");
            invalidate_scene_frames(&mut main);
            return;
        }
    }
    state.apply(&mut main);
    validate_scene_inputs(&mut main, &mut validation);
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy extract system parameters"
)]
fn remove_missing_extracted_resources(
    mut commands: Commands,
    queue: Extract<Option<Res<SourceParticleComputeQueue>>>,
    raster: Extract<Option<Res<RasterInputs>>>,
) {
    if queue.is_none() {
        commands.remove_resource::<SourceParticleComputeQueue>();
    }
    if raster.is_none() {
        commands.remove_resource::<RasterInputs>();
    }
}

fn invalidate_scene_frames(world: &mut World) {
    for mut projection in world
        .query::<&mut SourceParticleProjection>()
        .iter_mut(world)
    {
        if projection.scene_bound {
            projection.invalidate_declared_frame();
        }
    }
}

fn validate_scene_inputs(world: &mut World, validation: &mut SceneValidation) {
    let camera = crate::source_graph_runtime::current_particle_camera(world).ok();
    validate_scene_inputs_with_camera(world, validation, camera);
}

#[allow(
    clippy::too_many_lines,
    reason = "Validate and revoke the same extraction snapshot without touching foreign slots"
)]
fn validate_scene_inputs_with_camera(
    world: &mut World,
    validation: &mut SceneValidation,
    camera: Option<(Entity, Mat4, Mat4, bool)>,
) {
    let inputs: Vec<_> = world
        .get_resource::<RasterInputs>()
        .map(|inputs| {
            inputs
                .0
                .iter()
                .map(|(slot, input)| (*slot, input.clone()))
                .collect()
        })
        .unwrap_or_default();
    let ownership = world
        .get_resource::<crate::source_graph_runtime::SourceSceneResidents>()
        .map(crate::source_graph_runtime::SourceSceneResidents::particle_index)
        .unwrap_or_default();
    let mut invalid = Vec::new();
    let mut duplicate_producers = std::collections::BTreeSet::new();
    let mut producer_projections = BTreeMap::<u64, Vec<u64>>::new();
    for (slot, input) in inputs {
        let Some(witness) = &input.witness else {
            continue;
        };
        if !Arc::ptr_eq(&input.owner, &witness.projection_identity) {
            continue;
        }
        let camera_valid = camera.as_ref().is_some_and(|(view, pose, clip, taa)| {
            *view == input.camera.view
                && *taa == input.camera.bevy_taa
                && pose.abs_diff_eq(input.camera.world_from_view, 1.0e-5)
                && clip.abs_diff_eq(input.camera.clip_from_view, 1.0e-5)
                && input
                    .camera
                    .witness
                    .as_ref()
                    .is_some_and(|witness| witness.is_fresh(world))
                && input
                    .camera
                    .clock
                    .is_some_and(|clock| clock.is_fresh(world))
                && world
                    .get_resource_ref::<SourceParticleCameraSnapshot>()
                    .is_some_and(|current| {
                        input.camera_tick == input.camera.creation_tick
                            && input.camera_tick == Some(current.last_changed())
                            && current
                                .witness
                                .as_ref()
                                .zip(input.camera.witness.as_ref())
                                .is_some_and(|(left, right)| Arc::ptr_eq(left, right))
                    })
                && crate::physics::current_global(world, *view)
                    .is_some_and(|current| current.to_matrix().abs_diff_eq(*pose, 1.0e-5))
        });
        let valid = camera_valid
            && world
                .get_resource::<crate::source_graph_runtime::SourceSceneResidents>()
                .is_some_and(|residents| residents.owns_particle(witness, &ownership))
            && world
                .get::<crate::source_graph_scene_attachment::SourceSceneAttached>(witness.root)
                .is_some()
            && world.get::<crate::source_graph_runtime::SourceFrameStatus>(witness.root)
                == Some(&crate::source_graph_runtime::SourceFrameStatus::Updated)
            && world
                .get::<SourceParticleSceneOwner>(witness.owner)
                .is_some_and(|owner| {
                    owner.slot() == witness.producer_slot
                        && Arc::ptr_eq(&owner.instance_identity(), &witness.owner_identity)
                        && owner.error().is_none()
                        && world
                            .get_resource::<super::scene::SceneOwners>()
                            .is_some_and(|claims| claims.owns(witness.owner, owner))
                        && world
                            .get_resource::<SourceParticleComputeQueue>()
                            .is_some_and(|queue| owner.owns_current_queue(queue))
                })
            && world
                .get::<SourceParticleProjection>(witness.projection)
                .is_some_and(|projection| {
                    projection.slot == slot
                        && projection.frame_ready
                        && projection.scene_bound
                        && Arc::ptr_eq(&projection.identity, &witness.projection_identity)
                        && projection
                            .scene_witness
                            .as_ref()
                            .is_some_and(|current| Arc::ptr_eq(current, witness))
                        && world.get_resource::<Projections>().is_some_and(|claims| {
                            claims.claims.get(&witness.projection).is_some_and(|claim| {
                                claim.slot == slot
                                    && Arc::ptr_eq(&claim.identity, &projection.identity)
                            })
                        })
                        && world
                            .get_resource::<SourceParticleComputeQueue>()
                            .is_some_and(|queue| {
                                owns_queued_request(queue, projection)
                                    && queue.free_or_owned(slot, &projection.identity)
                            })
                })
            && witness.is_fresh(world);
        if valid {
            producer_projections
                .entry(witness.producer_slot)
                .or_default()
                .push(slot);
            if validation
                .producers
                .insert(witness.producer_slot, witness.owner_identity.clone())
                .is_some()
            {
                duplicate_producers.insert(witness.producer_slot);
            }
            validation
                .projections
                .insert(slot, witness.projection_identity.clone());
        } else {
            invalid.push((
                slot,
                witness.projection,
                witness.projection_identity.clone(),
            ));
        }
    }
    for slot in duplicate_producers {
        validation.producers.remove(&slot);
        if let Some(projections) = producer_projections.get(&slot) {
            for projection in projections {
                validation.projections.remove(projection);
            }
        }
    }
    for (slot, entity, identity) in invalid {
        if let Some(mut projection) = world.get_mut::<SourceParticleProjection>(entity)
            && Arc::ptr_eq(&projection.identity, &identity)
        {
            projection.invalidate_declared_frame();
        }
        if let Some(mut raster) = world.get_resource_mut::<RasterInputs>() {
            revoke_owned_input(&mut raster, slot, &identity);
        }
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy SystemState parameter tuple"
)]
fn refresh_scene_requests(
    (mut queue, scene_claims, projection_claims, mut raster, owners, mut projections):
        SystemParamItem<'_, '_, SceneRefreshParams>,
) {
    let mut owner_slots = BTreeMap::<u64, usize>::new();
    for (_, owner) in &owners {
        *owner_slots.entry(owner.slot()).or_default() += 1;
    }
    let mut projection_slots = BTreeMap::<u64, usize>::new();
    for (_, projection) in &projections {
        *projection_slots.entry(projection.slot).or_default() += 1;
    }
    revoke_stale_inputs(
        &projection_claims,
        &mut raster,
        &owners,
        &projections,
        &scene_claims,
        &owner_slots,
        &projection_slots,
    );
    revoke_unowned_queue_inputs(&queue, &projection_claims, &mut raster, &projections);
    let eligible: Vec<_> = projections
        .iter()
        .filter(|(entity, projection)| {
            projection.frame_ready
                && projection.scene_bound
                && projection.error.is_none()
                && projection_slots.get(&projection.slot) == Some(&1)
                && !owner_slots.contains_key(&projection.slot)
                && owns_queued_request(&queue, projection)
                && projection_claims.claims.get(entity).is_some_and(|claim| {
                    claim.slot == projection.slot
                        && Arc::ptr_eq(&claim.identity, &projection.identity)
                })
                && raster
                    .0
                    .get(&projection.slot)
                    .is_some_and(|input| Arc::ptr_eq(&input.owner, &projection.identity))
        })
        .map(|(entity, projection)| (entity, projection.owner))
        .collect();
    let mut failed = Vec::new();
    for (entity, owner_id) in eligible {
        let Ok((_, mut projection)) = projections.get_mut(entity) else {
            continue;
        };
        if let Ok((owner_entity, owner)) = owners.get(owner_id)
            && owner_slots.get(&owner.slot()) == Some(&1)
            && scene_claims.owns(owner_entity, owner)
            && let Err(error) = projection.advance(owner, &mut queue)
        {
            projection.error = Some(error);
            failed.push((projection.slot, projection.identity.clone()));
        }
    }
    for (slot, identity) in failed {
        revoke_owned_input(&mut raster, slot, &identity);
    }
}

fn owns_queued_request(
    queue: &SourceParticleComputeQueue,
    projection: &SourceParticleProjection,
) -> bool {
    match (&projection.pending, queue.0.get(&projection.slot)) {
        (Some(pending), Some(requests)) if requests.len() == 1 => {
            Arc::ptr_eq(&pending.status, &requests[0].status)
        }
        (None, None) => true,
        _ => false,
    }
}

fn revoke_unowned_queue_inputs(
    queue: &SourceParticleComputeQueue,
    claims: &Projections,
    raster: &mut RasterInputs,
    projections: &Query<(Entity, &mut SourceParticleProjection)>,
) {
    for (entity, claim) in &claims.claims {
        if let Ok((_, projection)) = projections.get(*entity)
            && claim.slot == projection.slot
            && Arc::ptr_eq(&claim.identity, &projection.identity)
            && !owns_queued_request(queue, projection)
        {
            revoke_owned_input(raster, claim.slot, &claim.identity);
        }
    }
}

fn revoke_owned_input(raster: &mut RasterInputs, slot: u64, identity: &Arc<()>) {
    if raster
        .0
        .get(&slot)
        .is_some_and(|input| Arc::ptr_eq(&input.owner, identity))
    {
        raster.0.remove(&slot);
    }
}

/// Revoke only a camera input that still bears the previously checked owner;
/// no queue slot or other owner's input is changed during this extraction.
fn revoke_stale_inputs(
    projection_claims: &Projections,
    raster: &mut RasterInputs,
    owners: &Query<(Entity, &mut SourceParticleSceneOwner)>,
    projections: &Query<(Entity, &mut SourceParticleProjection)>,
    scene_claims: &super::scene::SceneOwners,
    owner_slots: &BTreeMap<u64, usize>,
    projection_slots: &BTreeMap<u64, usize>,
) {
    let revoked: Vec<_> = projection_claims
        .claims
        .iter()
        .filter_map(|(entity, claim)| {
            let input = raster.0.get(&claim.slot)?;
            if !Arc::ptr_eq(&input.owner, &claim.identity) {
                return None;
            }
            let valid = projections.get(*entity).is_ok_and(|(_, projection)| {
                claim.slot == projection.slot
                    && Arc::ptr_eq(&claim.identity, &projection.identity)
                    && projection.frame_ready
                    && projection.scene_bound
                    && projection.error.is_none()
                    && projection_slots.get(&projection.slot) == Some(&1)
                    && !owner_slots.contains_key(&projection.slot)
                    && owners
                        .get(projection.owner)
                        .is_ok_and(|(owner_entity, owner)| {
                            owner_slots.get(&owner.slot()) == Some(&1)
                                && scene_claims.owns(owner_entity, owner)
                                && owner.error().is_none()
                        })
            });
            (!valid).then(|| (claim.slot, claim.identity.clone()))
        })
        .collect();
    for (slot, identity) in revoked {
        revoke_owned_input(raster, slot, &identity);
    }
}

fn record_raster_input(
    projection: &SourceParticleProjection,
    camera: Option<&Res<SourceParticleCameraSnapshot>>,
    inputs: &mut RasterInputs,
) {
    if projection.frame_ready
        && projection.scene_bound
        && let (Some(camera), Some(layout)) = (camera, &projection.frame_layout)
    {
        inputs.0.insert(
            projection.slot,
            RasterInput {
                owner: projection.identity.clone(),
                camera: (**camera).clone(),
                camera_tick: Some(camera.last_changed()),
                relative_offset: layout.relative_world_to_clip_offset,
                bank: projection.prototype.bank.clone(),
                witness: projection.scene_witness.clone(),
            },
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
    frame_inputs: (
        Option<Res<SourceParticleCameraSnapshot>>,
        ResMut<RasterInputs>,
    ),
) {
    let (camera, mut raster_inputs) = frame_inputs;
    raster_inputs.0.clear();
    let current: BTreeMap<_, _> = projections
        .iter()
        .map(|(entity, projection)| {
            (
                entity,
                Claim {
                    slot: projection.slot,
                    identity: projection.identity.clone(),
                    queue_identity: projection.queue_identity(),
                },
            )
        })
        .collect();
    for (entity, claim) in &residents.claims {
        if current.get(entity).is_none_or(|next| {
            next.slot != claim.slot || !Arc::ptr_eq(&next.identity, &claim.identity)
        }) && let Some(status) = &claim.queue_identity
        {
            queue.remove_exact(claim.slot, &claim.identity, std::slice::from_ref(status));
        }
    }
    let mut counts = BTreeMap::<u64, usize>::new();
    for claim in current.values() {
        *counts.entry(claim.slot).or_default() += 1;
    }
    let source_slots: std::collections::BTreeSet<_> =
        owners.iter().map(SourceParticleSceneOwner::slot).collect();
    residents.claims = current;
    for (entity, mut projection) in &mut projections {
        if counts[&projection.slot] != 1 || source_slots.contains(&projection.slot) {
            projection.error =
                Some("Source projection has a duplicate or producer-owned slot".into());
        }
        if projection.error.is_some() {
            projection.remove_queued_request(&mut queue);
            continue;
        }
        let result = owners
            .get(projection.owner)
            .map_err(|_| "Source projection owner removed".to_owned())
            .and_then(|owner| projection.advance(owner, &mut queue));
        if let Err(error) = result {
            projection.error = Some(error);
            projection.remove_queued_request(&mut queue);
            continue;
        }
        if let Some(claim) = residents.claims.get_mut(&entity) {
            claim.queue_identity = projection.queue_identity();
        }
        record_raster_input(&projection, camera.as_ref(), &mut raster_inputs);
        if projection.frame_ready
            && projection.encoded_frames > 0
            && let Some(material) = &projection.material
        {
            let output = projection.output_buffer();
            let binding = SourceGraphStateBufferBinding::new(output.clone(), &buffers);
            match binding {
                Ok(binding) => {
                    if let Some(existing) = materials.get(material) {
                        let owner =
                            projection
                                .scene_bound
                                .then(|| super::draw::ParticleFrameOwner {
                                    slot: projection.slot,
                                    identity: projection.identity.clone(),
                                    output: output.id(),
                                });
                        let same_owner = match (existing.extension.particle_frame_owner(), &owner) {
                            (None, None) => true,
                            (Some(old), Some(next)) => old.matches(next),
                            _ => false,
                        };
                        if !existing.extension.is_source_state_buffer(&output) || !same_owner {
                            let extension = &mut materials.get_mut(material).unwrap().extension;
                            if let Some(owner) = owner {
                                extension.set_particle_frame_buffer(&binding, owner);
                            } else {
                                extension.set_source_state_buffer(&binding);
                            }
                        }
                    } else {
                        projection.error = Some("Source projection material asset absent".into());
                        projection.remove_queued_request(&mut queue);
                    }
                }
                Err(error) => {
                    projection.error = Some(error.into());
                    projection.remove_queued_request(&mut queue);
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

    #[test]
    fn scene_validation_is_rebuilt_and_compares_owner_identity() {
        let identity = Arc::new(());
        let foreign = Arc::new(());
        let mut validation = SceneValidation::default();
        assert!(!validation.producer_allowed(7, &identity));
        assert!(!validation.projection_allowed(1, &identity));
        validation.producers.insert(7, identity.clone());
        validation.projections.insert(1, identity.clone());
        assert!(validation.producer_allowed(7, &identity));
        assert!(validation.projection_allowed(1, &identity));
        assert!(!validation.producer_allowed(7, &foreign));
        assert!(!validation.projection_allowed(1, &foreign));
        validation.producers.clear();
        validation.projections.clear();
        assert!(!validation.producer_allowed(7, &identity));
        assert!(!validation.projection_allowed(1, &identity));
    }

    #[test]
    fn missing_main_resource_revokes_only_its_extracted_render_copy() {
        use bevy::ecs::system::RunSystemOnce;

        let mut buffers = Assets::<ShaderBuffer>::default();
        let input = buffers.add(ShaderBuffer::new(&[0_u8; 16], default()));
        let output = buffers.add(ShaderBuffer::new(&[0_u8; 16], default()));
        let request = SourceParticleComputeRequest::new(
            Handle::default(),
            "advance".into(),
            Handle::default(),
            [0; 1024],
            input,
            output,
            1,
        )
        .unwrap();
        let status = request.status.clone();
        let mut queue = SourceParticleComputeQueue::default();
        queue.insert(42, request);
        let mut render = World::new();
        render.insert_resource(MainWorld::default());
        render
            .resource_mut::<MainWorld>()
            .insert_resource(queue.clone());
        render.insert_resource(queue);
        render.insert_resource(RasterInputs::default());

        render
            .run_system_once(remove_missing_extracted_resources)
            .unwrap();
        assert!(!render.contains_resource::<RasterInputs>());
        assert!(Arc::ptr_eq(
            &render.resource::<SourceParticleComputeQueue>().0[&42][0].status,
            &status
        ));

        render
            .resource_mut::<MainWorld>()
            .remove_resource::<SourceParticleComputeQueue>();
        render
            .resource_mut::<MainWorld>()
            .insert_resource(RasterInputs::default());
        render.insert_resource(RasterInputs::default());
        render
            .run_system_once(remove_missing_extracted_resources)
            .unwrap();
        assert!(!render.contains_resource::<SourceParticleComputeQueue>());
        assert!(render.contains_resource::<RasterInputs>());
    }

    #[cfg(feature = "physics-rapier")]
    fn retain_particle_fixture(
        world: &mut World,
        node: Entity,
        instance: crate::source_particle_host::HostedSourceParticle,
    ) -> Entity {
        use crate::source_graph_runtime::{self, RuntimePolicy, SourceFrameStatus};

        let root = world
            .spawn((
                crate::source_graph_scene_attachment::SourceSceneAttached,
                SourceFrameStatus::Updated,
                Visibility::Inherited,
            ))
            .id();
        world.entity_mut(node).insert(ChildOf(root));
        let host = crate::source_graph_host::SourceSceneHost {
            root,
            attached: crate::source_graph_scene_attachment::AttachedScene {
                nodes: BTreeMap::from([(0, node)]),
                primitives: Vec::new(),
            },
            registered: crate::source_graph_resources::RegisteredScene {
                owned_image_ids: std::collections::BTreeSet::new(),
                sampling: crate::source_graph::SourceGraphSceneSampling::default(),
                bindings: crate::source_graph_scene_attachment::SceneBindings::default(),
                original_passes: BTreeMap::new(),
            },
            original_state: None,
            object_basis: None,
            source_document: serde_json::json!({}),
            carrier_sha256: forge_core::bytes_sha256(b"fixture"),
            particle_instances: vec![instance],
            collider_projection: None,
            collider_scene_projection: None,
        };
        source_graph_runtime::retain(
            world,
            host,
            RuntimePolicy {
                source_world_from_target_world: Mat4::IDENTITY,
                source_absolute_world_from_target_world: Mat4::IDENTITY,
                global_mip_bias: 0.0,
                source_float_min: f32::MIN_POSITIVE,
            },
        )
        .unwrap();
        root
    }

    fn prepared_with_frame_layout() -> crate::source_particle_transport::PreparedSourceParticleOwner
    {
        let (mut declaration, resources) = crate::source_particle_transport::tests::fixture();
        declaration.projection.frame_layout = Some(
            forge_assets::source_particle_transport::SourceParticleFrameLayout {
                object_to_world_offset: 0,
                world_to_object_offset: 64,
                relative_world_to_clip_offset: 128,
                camera_position_offset: 192,
                evidence_sha256: forge_core::bytes_sha256(b"scene extraction fixture layout"),
            },
        );
        crate::source_particle_transport::PreparedSourceParticleOwner::prepare(
            &declaration,
            &resources,
        )
        .unwrap()
    }

    fn sealed_scene_fixture_with_reinsert(
        reinsert: bool,
    ) -> (World, Entity, Entity, Entity, Entity) {
        use crate::source_particle_host::{
            HostedSourceParticle, ParticleSceneWitness, ParticleViewWitness,
        };
        use bevy::ecs::system::RunSystemOnce;

        let (mut world, node, material) = crate::source_particle_host::tests::setup(true);
        let instance = HostedSourceParticle::install(
            &mut world,
            prepared_with_frame_layout(),
            node,
            material,
            7,
            1,
        )
        .unwrap();
        let projection = world
            .query::<(Entity, &SourceParticleProjection)>()
            .single(&world)
            .unwrap()
            .0;
        let root = retain_particle_fixture(&mut world, node, instance);
        world.entity_mut(root).insert(Transform::IDENTITY);
        world.entity_mut(node).insert(Transform::IDENTITY);
        let camera = world.spawn((Camera3d::default(), Transform::IDENTITY)).id();
        let view_witness = ParticleViewWitness::capture(&world, camera, Mat4::IDENTITY).unwrap();
        let owner_identity = world
            .get::<SourceParticleSceneOwner>(node)
            .unwrap()
            .instance_identity();
        let projection_identity = world
            .get::<SourceParticleProjection>(projection)
            .unwrap()
            .instance_identity();
        let witness = ParticleSceneWitness::capture(
            &world,
            root,
            node,
            projection,
            7,
            owner_identity,
            projection_identity,
            &BTreeMap::from([(node, 0)]),
        )
        .unwrap();
        let mut component = world
            .get_mut::<SourceParticleProjection>(projection)
            .unwrap();
        component.apply_scene_bank(Arc::new([0; 1024]));
        component.seal_scene_frame(witness);
        world.insert_resource(crate::source_graph_runtime::SourceClock::default());
        let clock = crate::source_graph_runtime::SourceClockWitness::capture(&world);
        let creation_tick = world.read_change_tick();
        world.insert_resource(SourceParticleCameraSnapshot {
            view: camera,
            world_from_view: Mat4::IDENTITY,
            clip_from_view: Mat4::IDENTITY,
            bevy_taa: false,
            witness: Some(Arc::new(view_witness)),
            clock,
            creation_tick: Some(creation_tick),
        });
        // Mirror the shared runtime's single frame boundary before publication.
        world.increment_change_tick();
        if reinsert {
            let replacement = world.resource::<SourceParticleCameraSnapshot>().clone();
            world.insert_resource(replacement);
        }
        world.init_resource::<Projections>();
        world.init_resource::<RasterInputs>();
        world.init_resource::<super::super::scene::SceneOwners>();
        world
            .run_system_once(super::super::scene::advance_scene_owners)
            .unwrap();
        world.run_system_once(advance_projections).unwrap();
        (world, root, node, projection, camera)
    }

    fn sealed_scene_fixture() -> (World, Entity, Entity, Entity, Entity) {
        sealed_scene_fixture_with_reinsert(false)
    }

    #[test]
    fn late_reparent_revokes_only_the_current_scene_and_needs_a_new_frame() {
        let (mut world, root, node, projection, camera) = sealed_scene_fixture();
        let mut validation = SceneValidation::default();
        let current = Some((camera, Mat4::IDENTITY, Mat4::IDENTITY, false));
        validate_scene_inputs_with_camera(&mut world, &mut validation, current);
        let owner = world
            .get::<SourceParticleSceneOwner>(node)
            .unwrap()
            .instance_identity();
        let projection_identity = world
            .get::<SourceParticleProjection>(projection)
            .unwrap()
            .instance_identity();
        assert!(validation.producer_allowed(7, &owner));
        assert!(validation.projection_allowed(1, &projection_identity));

        let foreign = world.spawn(Transform::IDENTITY).id();
        world.increment_change_tick();
        world.entity_mut(node).insert(ChildOf(foreign));
        let mut refused = SceneValidation::default();
        validate_scene_inputs_with_camera(&mut world, &mut refused, current);
        assert!(!refused.producer_allowed(7, &owner));
        assert!(!refused.projection_allowed(1, &projection_identity));
        assert!(!world.resource::<RasterInputs>().0.contains_key(&1));
        world.entity_mut(node).insert(ChildOf(root));
        assert!(
            !world
                .get::<SourceParticleProjection>(projection)
                .unwrap()
                .frame_ready
        );
    }

    #[test]
    fn missing_scene_resource_does_not_readmit_old_input_or_remove_foreign_queue() {
        let (mut world, _root, _node, projection, camera) = sealed_scene_fixture();
        let mut foreign = world.resource::<SourceParticleComputeQueue>().0[&1][0].clone();
        foreign.status = Arc::new(Mutex::new(SourceParticleComputeStatus::WaitingForResources));
        foreign.raster_owner = None;
        foreign.scene_owner = None;
        let foreign_status = foreign.status.clone();
        world
            .resource_mut::<SourceParticleComputeQueue>()
            .insert(42, foreign);
        let residents = world
            .remove_resource::<crate::source_graph_runtime::SourceSceneResidents>()
            .unwrap();
        let current = Some((camera, Mat4::IDENTITY, Mat4::IDENTITY, false));
        let mut validation = SceneValidation::default();
        validate_scene_inputs_with_camera(&mut world, &mut validation, current);
        assert!(validation.producers.is_empty());
        assert!(validation.projections.is_empty());
        assert!(
            !world
                .get::<SourceParticleProjection>(projection)
                .unwrap()
                .frame_ready
        );
        assert!(Arc::ptr_eq(
            &world.resource::<SourceParticleComputeQueue>().0[&42][0].status,
            &foreign_status
        ));
        world.insert_resource(residents);
        validate_scene_inputs_with_camera(&mut world, &mut validation, current);
        assert!(validation.projections.is_empty());
        assert!(!world.resource::<RasterInputs>().0.contains_key(&1));
    }

    fn assert_scene_frame_revoked(world: &mut World, projection: Entity, camera: Entity) {
        let mut validation = SceneValidation::default();
        validate_scene_inputs_with_camera(
            world,
            &mut validation,
            Some((camera, Mat4::IDENTITY, Mat4::IDENTITY, false)),
        );
        assert!(validation.producers.is_empty());
        assert!(validation.projections.is_empty());
        assert!(
            !world
                .get::<SourceParticleProjection>(projection)
                .unwrap()
                .frame_ready
        );
        assert!(!world.resource::<RasterInputs>().0.contains_key(&1));
    }

    #[test]
    fn missing_camera_snapshot_cannot_be_restored_without_a_new_frame() {
        let (mut world, _, _, projection, camera) = sealed_scene_fixture();
        let snapshot = world
            .remove_resource::<SourceParticleCameraSnapshot>()
            .unwrap();
        assert_scene_frame_revoked(&mut world, projection, camera);
        world.insert_resource(snapshot);
        assert_scene_frame_revoked(&mut world, projection, camera);
    }

    #[test]
    fn equal_but_replaced_camera_snapshot_is_not_the_published_frame() {
        let (mut world, _, _, projection, camera) = sealed_scene_fixture();
        let replacement = world.resource::<SourceParticleCameraSnapshot>().clone();
        world.insert_resource(replacement);
        assert_scene_frame_revoked(&mut world, projection, camera);
    }

    #[test]
    fn cloned_snapshot_reinserted_before_publication_fails_its_creation_tick() {
        let (mut world, _, _, projection, camera) = sealed_scene_fixture_with_reinsert(true);
        assert_scene_frame_revoked(&mut world, projection, camera);
    }

    #[test]
    fn missing_source_clock_cannot_be_restored_without_a_new_frame() {
        let (mut world, _, _, projection, camera) = sealed_scene_fixture();
        let clock = world
            .remove_resource::<crate::source_graph_runtime::SourceClock>()
            .unwrap();
        assert_scene_frame_revoked(&mut world, projection, camera);
        world.insert_resource(clock);
        assert_scene_frame_revoked(&mut world, projection, camera);
    }

    #[test]
    fn replacing_clock_with_equal_values_requires_a_new_frame() {
        let (mut world, _, _, projection, camera) = sealed_scene_fixture();
        let clock = *world.resource::<crate::source_graph_runtime::SourceClock>();
        world.increment_change_tick();
        world.insert_resource(clock);
        assert_scene_frame_revoked(&mut world, projection, camera);
    }

    #[test]
    fn replaced_claim_registries_do_not_admit_an_old_scene_frame() {
        let (mut world, _, _, projection, camera) = sealed_scene_fixture();
        world.insert_resource(super::super::scene::SceneOwners::default());
        assert_scene_frame_revoked(&mut world, projection, camera);
        let (mut world, _, _, projection, camera) = sealed_scene_fixture();
        world.insert_resource(Projections::default());
        assert_scene_frame_revoked(&mut world, projection, camera);
    }

    #[test]
    fn foreign_projection_request_survives_next_cycle_and_claim_removal() {
        let (mut app, owner, request, _) = fixture();
        let projection = app
            .world_mut()
            .spawn(SourceParticleProjection::new(owner, 2, request).unwrap())
            .id();
        app.update();
        let previous = app.world().resource::<SourceParticleComputeQueue>().0[&2][0].clone();
        previous.set_status(SourceParticleComputeStatus::Encoded);
        let mut foreign = previous;
        foreign.status = Arc::new(Mutex::new(SourceParticleComputeStatus::WaitingForResources));
        let status = foreign.status.clone();
        app.world_mut()
            .resource_mut::<SourceParticleComputeQueue>()
            .insert(2, foreign);
        app.update();
        assert!(Arc::ptr_eq(
            &app.world().resource::<SourceParticleComputeQueue>().0[&2][0].status,
            &status
        ));
        assert!(
            app.world()
                .get::<SourceParticleProjection>(projection)
                .unwrap()
                .error()
                .is_some()
        );
        app.world_mut().despawn(projection);
        app.update();
        assert!(Arc::ptr_eq(
            &app.world().resource::<SourceParticleComputeQueue>().0[&2][0].status,
            &status
        ));
    }

    #[test]
    fn cancel_and_release_preserve_a_replaced_projection_request() {
        use bevy::ecs::system::RunSystemOnce;
        let (mut world, node, material) = crate::source_particle_host::tests::setup(true);
        let mut instance = crate::source_particle_host::HostedSourceParticle::install(
            &mut world,
            prepared_with_frame_layout(),
            node,
            material,
            7,
            1,
        )
        .unwrap();
        let projection = world
            .query::<(Entity, &SourceParticleProjection)>()
            .single(&world)
            .unwrap()
            .0;
        world
            .get_mut::<SourceParticleProjection>(projection)
            .unwrap()
            .apply_scene_bank(Arc::new([0; 1024]));
        world.init_resource::<Projections>();
        world.init_resource::<RasterInputs>();
        world.run_system_once(advance_projections).unwrap();
        let mut foreign = world.resource::<SourceParticleComputeQueue>().0[&1][0].clone();
        foreign.status = Arc::new(Mutex::new(SourceParticleComputeStatus::WaitingForResources));
        let status = foreign.status.clone();
        world
            .resource_mut::<SourceParticleComputeQueue>()
            .insert(1, foreign);
        instance.cancel_current_frame(&mut world);
        assert!(Arc::ptr_eq(
            &world.resource::<SourceParticleComputeQueue>().0[&1][0].status,
            &status
        ));
        instance.release(&mut world);
        world.run_system_once(advance_projections).unwrap();
        assert!(Arc::ptr_eq(
            &world.resource::<SourceParticleComputeQueue>().0[&1][0].status,
            &status
        ));
    }

    #[cfg(feature = "physics-rapier")]
    #[test]
    fn projection_failure_refuses_its_host_and_hides_the_shared_scene() {
        use crate::source_graph_runtime::{self, SourceClock, SourceFrameStatus};
        use crate::source_particle_host::{HostedSourceParticle, tests::prepared};
        use bevy::ecs::system::RunSystemOnce;

        let (mut world, node, material) = crate::source_particle_host::tests::setup(true);
        let instance =
            HostedSourceParticle::install(&mut world, prepared(), node, material, 7, 1).unwrap();
        let projection = world
            .query::<(Entity, &SourceParticleProjection)>()
            .iter(&world)
            .map(|(entity, _)| entity)
            .next()
            .unwrap();
        assert_eq!(
            world
                .get::<SourceParticleProjection>(projection)
                .unwrap()
                .encoded_frames(),
            0
        );
        assert!(instance.validate_frame(&world, 0.0, &[]).is_ok());
        // Trigger the real projection system's producer-slot conflict without a GPU.
        world
            .get_mut::<SourceParticleProjection>(projection)
            .unwrap()
            .slot = 7;
        world.init_resource::<Projections>();
        world.init_resource::<RasterInputs>();
        world.run_system_once(advance_projections).unwrap();
        assert!(
            world
                .get::<SourceParticleProjection>(projection)
                .unwrap()
                .error()
                .is_some_and(|error| error.contains("producer-owned slot"))
        );
        let before = world
            .get::<SourceParticleSceneOwner>(node)
            .unwrap()
            .queued_frames();
        assert!(
            instance
                .queue_frame(&mut world, 0.0, &[], false)
                .unwrap_err()
                .contains("producer-owned slot")
        );
        assert_eq!(
            world
                .get::<SourceParticleSceneOwner>(node)
                .unwrap()
                .queued_frames(),
            before
        );

        let root = retain_particle_fixture(&mut world, node, instance);
        world.insert_resource(SourceClock {
            sequence: 1,
            ..default()
        });
        source_graph_runtime::advance_particle_inputs(&mut world);
        assert_eq!(world.get::<Visibility>(root), Some(&Visibility::Hidden));
        assert!(matches!(world.get::<SourceFrameStatus>(root),
            Some(SourceFrameStatus::Refused(reason)) if reason.contains("producer-owned slot")));
    }

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
    fn hosted_raster_input_tracks_current_bank_while_request_waits() {
        let (mut app, owner, request, _) = fixture();
        let camera = app.world_mut().spawn_empty().id();
        app.world_mut()
            .insert_resource(SourceParticleCameraSnapshot {
                view: camera,
                world_from_view: Mat4::IDENTITY,
                clip_from_view: Mat4::IDENTITY,
                bevy_taa: false,
                witness: None,
                clock: None,
                creation_tick: None,
            });
        let mut component = SourceParticleProjection::new(owner, 2, request)
            .unwrap()
            .with_frame_layout(
                forge_assets::source_particle_transport::SourceParticleFrameLayout {
                    object_to_world_offset: 0,
                    world_to_object_offset: 64,
                    relative_world_to_clip_offset: 128,
                    camera_position_offset: 192,
                    evidence_sha256: forge_core::bytes_sha256(b"hosted raster input"),
                },
            )
            .unwrap();
        component.apply_scene_bank(Arc::new([1; 1024]));
        let entity = app.world_mut().spawn(component).id();
        app.update();
        let pending = app.world().resource::<SourceParticleComputeQueue>().0[&2][0].clone();
        assert!(pending.raster_owner.is_some());
        assert_eq!(app.world().resource::<RasterInputs>().0[&2].bank[0], 1);
        app.world_mut()
            .get_mut::<SourceParticleProjection>(entity)
            .unwrap()
            .apply_scene_bank(Arc::new([2; 1024]));
        app.world_mut()
            .resource_mut::<SourceParticleCameraSnapshot>()
            .world_from_view = Mat4::from_translation(Vec3::X);
        app.update();
        let current = &app.world().resource::<RasterInputs>().0[&2];
        assert_eq!(current.bank[0], 2);
        assert_eq!(
            current.camera.world_from_view,
            Mat4::from_translation(Vec3::X)
        );
        assert!(Arc::ptr_eq(
            pending.raster_owner.as_ref().unwrap(),
            &current.owner
        ));
        assert!(Arc::ptr_eq(
            &pending.status,
            &app.world().resource::<SourceParticleComputeQueue>().0[&2][0].status
        ));
        assert_eq!(pending.bank[0], 1);
    }

    fn refresh_after_sync(world: &mut World) {
        let mut state = SystemState::<SceneRefreshParams>::new(world);
        refresh_scene_requests(
            state
                .get_mut(world)
                .expect("Scene refresh resources are present"),
        );
        state.apply(world);
    }

    #[test]
    fn scene_request_refreshes_after_render_without_replaying_the_previous_request() {
        let (mut app, owner, request, _) = fixture();
        let camera = app.world_mut().spawn_empty().id();
        app.world_mut()
            .insert_resource(SourceParticleCameraSnapshot {
                view: camera,
                world_from_view: Mat4::IDENTITY,
                clip_from_view: Mat4::IDENTITY,
                bevy_taa: false,
                witness: None,
                clock: None,
                creation_tick: None,
            });
        let mut component = SourceParticleProjection::new(owner, 2, request)
            .unwrap()
            .with_frame_layout(
                forge_assets::source_particle_transport::SourceParticleFrameLayout {
                    object_to_world_offset: 0,
                    world_to_object_offset: 64,
                    relative_world_to_clip_offset: 128,
                    camera_position_offset: 192,
                    evidence_sha256: forge_core::bytes_sha256(b"late scene refresh"),
                },
            )
            .unwrap();
        component.apply_scene_bank(Arc::new([1; 1024]));
        let projection = app.world_mut().spawn(component).id();
        app.update();
        app.world_mut()
            .get_mut::<SourceParticleSceneOwner>(owner)
            .unwrap()
            .queue_frame(0.125, &[], false)
            .unwrap();
        let previous = app.world().resource::<SourceParticleComputeQueue>().0[&2][0].clone();
        refresh_after_sync(app.world_mut());
        assert!(Arc::ptr_eq(
            &previous.status,
            &app.world().resource::<SourceParticleComputeQueue>().0[&2][0].status
        ));
        assert_eq!(
            app.world()
                .get::<SourceParticleSceneOwner>(owner)
                .unwrap()
                .submitted_frames(),
            1
        );
        for request in &app.world().resource::<SourceParticleComputeQueue>().0[&90] {
            request.set_status(SourceParticleComputeStatus::Encoded);
        }
        previous.set_status(SourceParticleComputeStatus::Encoded);
        let current_input = app
            .world_mut()
            .resource_mut::<RasterInputs>()
            .0
            .remove(&2)
            .unwrap();
        refresh_after_sync(app.world_mut());
        assert!(Arc::ptr_eq(
            &previous.status,
            &app.world().resource::<SourceParticleComputeQueue>().0[&2][0].status
        ));
        app.world_mut()
            .resource_mut::<RasterInputs>()
            .0
            .insert(2, current_input);
        refresh_after_sync(app.world_mut());
        let next = &app.world().resource::<SourceParticleComputeQueue>().0[&2][0];
        assert!(!Arc::ptr_eq(&previous.status, &next.status));
        assert_eq!(
            previous.status().unwrap(),
            SourceParticleComputeStatus::Encoded
        );
        assert_eq!(
            next.status().unwrap(),
            SourceParticleComputeStatus::WaitingForResources
        );
        assert_eq!(
            app.world()
                .get::<SourceParticleProjection>(projection)
                .unwrap()
                .encoded_frames(),
            1
        );
        let current = app.world().get::<SourceParticleSceneOwner>(owner).unwrap();
        assert_eq!(
            (current.submitted_frames(), current.completed_frames()),
            (1, 0)
        );
        app.update();
        let current = app.world().get::<SourceParticleSceneOwner>(owner).unwrap();
        assert_eq!(
            (current.submitted_frames(), current.completed_frames()),
            (2, 1)
        );
    }

    #[test]
    fn late_scene_refresh_does_not_change_the_legacy_projection_path() {
        let (mut app, owner, request, _) = fixture();
        app.world_mut()
            .spawn(SourceParticleProjection::new(owner, 2, request).unwrap());
        app.update();
        for requests in app
            .world()
            .resource::<SourceParticleComputeQueue>()
            .0
            .values()
        {
            for request in requests {
                request.set_status(SourceParticleComputeStatus::Encoded);
            }
        }
        let previous = app.world().resource::<SourceParticleComputeQueue>().0[&2][0].clone();
        refresh_after_sync(app.world_mut());
        assert!(Arc::ptr_eq(
            &previous.status,
            &app.world().resource::<SourceParticleComputeQueue>().0[&2][0].status
        ));
        assert_eq!(
            app.world()
                .get::<SourceParticleSceneOwner>(owner)
                .unwrap()
                .completed_frames(),
            0
        );
    }

    #[test]
    fn late_duplicate_revokes_only_owned_raster_input_and_keeps_foreign_queue() {
        let (mut app, owner, request, _) = fixture();
        let camera = app.world_mut().spawn_empty().id();
        app.world_mut()
            .insert_resource(SourceParticleCameraSnapshot {
                view: camera,
                world_from_view: Mat4::IDENTITY,
                clip_from_view: Mat4::IDENTITY,
                bevy_taa: false,
                witness: None,
                clock: None,
                creation_tick: None,
            });
        let mut component = SourceParticleProjection::new(owner, 2, request)
            .unwrap()
            .with_frame_layout(
                forge_assets::source_particle_transport::SourceParticleFrameLayout {
                    object_to_world_offset: 0,
                    world_to_object_offset: 64,
                    relative_world_to_clip_offset: 128,
                    camera_position_offset: 192,
                    evidence_sha256: forge_core::bytes_sha256(b"late duplicate ownership"),
                },
            )
            .unwrap();
        component.apply_scene_bank(Arc::new([1; 1024]));
        app.world_mut().spawn(component);
        app.update();
        let previous = app.world().resource::<SourceParticleComputeQueue>().0[&2][0].clone();
        let input = app.world().resource::<RasterInputs>().0[&2].clone();
        previous.set_status(SourceParticleComputeStatus::Encoded);
        let mut foreign = previous.clone();
        foreign.status = Arc::new(Mutex::new(SourceParticleComputeStatus::WaitingForResources));
        foreign.raster_owner = None;
        let foreign_status = foreign.status.clone();
        app.world_mut()
            .resource_mut::<SourceParticleComputeQueue>()
            .insert(2, foreign);
        refresh_after_sync(app.world_mut());
        assert!(!app.world().resource::<RasterInputs>().0.contains_key(&2));
        assert!(Arc::ptr_eq(
            &foreign_status,
            &app.world().resource::<SourceParticleComputeQueue>().0[&2][0].status
        ));
        app.world_mut()
            .resource_mut::<SourceParticleComputeQueue>()
            .insert(2, previous.clone());
        app.world_mut()
            .resource_mut::<RasterInputs>()
            .0
            .insert(2, input.clone());
        let mut duplicate = previous.clone();
        duplicate.status = Arc::new(Mutex::new(SourceParticleComputeStatus::WaitingForResources));
        app.world_mut()
            .spawn(SourceParticleProjection::new(owner, 2, duplicate).unwrap());
        refresh_after_sync(app.world_mut());
        assert!(!app.world().resource::<RasterInputs>().0.contains_key(&2));
        assert!(Arc::ptr_eq(
            &previous.status,
            &app.world().resource::<SourceParticleComputeQueue>().0[&2][0].status
        ));
        assert!(
            app.world()
                .resource::<SourceParticleComputeQueue>()
                .contains_slot(90)
        );

        let mut foreign = previous;
        foreign.status = Arc::new(Mutex::new(SourceParticleComputeStatus::WaitingForResources));
        foreign.raster_owner = None;
        let foreign_status = foreign.status.clone();
        app.world_mut()
            .resource_mut::<SourceParticleComputeQueue>()
            .insert(2, foreign);
        app.world_mut()
            .resource_mut::<RasterInputs>()
            .0
            .insert(2, input.clone());
        refresh_after_sync(app.world_mut());
        assert!(!app.world().resource::<RasterInputs>().0.contains_key(&2));
        assert!(Arc::ptr_eq(
            &foreign_status,
            &app.world().resource::<SourceParticleComputeQueue>().0[&2][0].status
        ));
        let mut foreign_input = input;
        foreign_input.owner = Arc::new(());
        let foreign_identity = foreign_input.owner.clone();
        app.world_mut()
            .resource_mut::<RasterInputs>()
            .0
            .insert(2, foreign_input);
        refresh_after_sync(app.world_mut());
        assert!(Arc::ptr_eq(
            &foreign_identity,
            &app.world().resource::<RasterInputs>().0[&2].owner
        ));
        assert!(Arc::ptr_eq(
            &foreign_status,
            &app.world().resource::<SourceParticleComputeQueue>().0[&2][0].status
        ));
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
