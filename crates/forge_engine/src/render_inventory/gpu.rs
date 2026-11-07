use super::{Request, ScopedRequests};
use crate::gpu_readiness::{
    assets::{GpuAssetEvidence, GpuAssets},
    effects::{GpuEffectEvidence, GpuEffects},
    views::{GpuViewEvidence, GpuViews},
};
use bevy::{
    camera::{NormalizedRenderTarget, RenderTarget},
    diagnostic::FrameCount,
    ecs::system::SystemParam,
    pbr::{MeshPipelineKey, ViewKeyCache, ViewKeyPrepassCache},
    prelude::*,
    render::{
        ExtractSchedule, MainWorld, Render, RenderApp, RenderStartup, RenderSystems,
        render_resource::{CachedPipelineState, PipelineCache},
        renderer::RenderQueue,
    },
    window::PrimaryWindow,
};
use forge_core::{ContentHash, EntityId};
use std::collections::BTreeMap;
use std::sync::{
    Arc, Mutex,
    atomic::{AtomicBool, Ordering},
};
#[cfg(test)]
mod tests;

#[derive(Clone, Debug, PartialEq, Eq)]
struct GraphicsView {
    camera: Entity,
    viewport: UVec2,
    origin: UVec2,
    target: NormalizedRenderTarget,
    msaa: u32,
    prepass: MeshPipelineKey,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub(crate) struct PreparedGraphicsContext {
    epoch: u64,
    view: GraphicsView,
    forward: MeshPipelineKey,
    prepass: Option<MeshPipelineKey>,
}

impl PreparedGraphicsContext {
    fn new(
        view: GraphicsView,
        epoch: u64,
        forward: MeshPipelineKey,
        prepass: Option<MeshPipelineKey>,
    ) -> Option<Self> {
        let flags = MeshPipelineKey::DEPTH_PREPASS
            | MeshPipelineKey::NORMAL_PREPASS
            | MeshPipelineKey::MOTION_VECTOR_PREPASS
            | MeshPipelineKey::DEFERRED_PREPASS;
        if epoch == 0
            || forward.msaa_samples() != view.msaa
            || forward & flags != view.prepass
            || (!view.prepass.is_empty() && prepass.is_none())
        {
            return None;
        }
        Some(Self {
            epoch,
            view,
            forward,
            prepass,
        })
    }
}

/// A renderer context observed while an actual inventory token was current.
/// This is not an inventory token or completion of a later scene publication.
#[derive(Clone, Debug)]
pub struct GraphicsContextReceipt(PreparedGraphicsContext);

#[derive(Default)]
struct ContextObservation {
    prepared: Option<PreparedGraphicsContext>,
    observed: u32,
    ready: bool,
}

#[derive(Resource, Clone, Default)]
struct GraphicsContextState(Arc<Mutex<ContextObservation>>);

#[derive(Resource, Default)]
struct GraphicsContextInput {
    view: Option<GraphicsView>,
    frame: u32,
}

impl GraphicsContextReceipt {
    /// Revalidate the live device/view and its actual prepared specialization.
    /// Selection ownership and source coverage remain the caller's responsibility.
    #[must_use]
    pub fn is_current(&self, world: &World) -> bool {
        let Some(view) = graphics_view(world) else {
            return false;
        };
        let Some(state) = world.get_resource::<GraphicsContextState>() else {
            return false;
        };
        let Ok(observed) = state.0.lock() else {
            return false;
        };
        let frame = world
            .get_resource::<FrameCount>()
            .map_or(0, |frame| frame.0);
        observed.ready
            && self.0.epoch > 0
            && self.0.view == view
            && observed.prepared.as_ref() == Some(&self.0)
            && frame
                .checked_sub(observed.observed)
                .is_some_and(|age| age <= 2)
    }

    pub(crate) fn matches_prepared(&self, prepared: &PreparedGraphicsContext) -> bool {
        &self.0 == prepared
    }
}

#[derive(SystemParam)]
struct PreparedViews<'w, 's> {
    views: Query<
        'w,
        's,
        (
            &'static bevy::render::sync_world::MainEntity,
            &'static bevy::render::view::ExtractedView,
        ),
        With<bevy::render::camera::ExtractedCamera>,
    >,
    forward: Option<Res<'w, ViewKeyCache>>,
    prepass: Option<Res<'w, ViewKeyPrepassCache>>,
}

impl PreparedViews<'_, '_> {
    fn observe(&self, view: &GraphicsView, epoch: u64) -> Option<PreparedGraphicsContext> {
        let mut matching = self
            .views
            .iter()
            .filter(|(main, _)| main.id() == view.camera);
        let (_, extracted) = matching.next()?;
        if matching.next().is_some() || epoch == 0 {
            return None;
        }
        let forward = self
            .forward
            .as_ref()?
            .get(&extracted.retained_view_entity)
            .copied()?;
        let prepass = self
            .prepass
            .as_ref()
            .and_then(|keys| keys.get(&extracted.retained_view_entity).copied());
        PreparedGraphicsContext::new(view.clone(), epoch, forward, prepass)
    }
}

/// Used by the existing forward prewarmer; preparation alone grants no admission.
pub(crate) fn prepared_graphics_context(
    world: &World,
    camera: Entity,
    forward: MeshPipelineKey,
    prepass: Option<MeshPipelineKey>,
) -> Option<PreparedGraphicsContext> {
    let view = world.get_resource::<GraphicsContextInput>()?.view.clone()?;
    if view.camera != camera {
        return None;
    }
    let epoch = world.get_resource::<Probe>()?.epoch;
    PreparedGraphicsContext::new(view, epoch, forward, prepass)
}

#[derive(Clone, Debug, PartialEq, Eq)]
struct Key {
    owner: EntityId,
    identity: ContentHash,
    generation: u64,
    serial: u64,
    effects: Option<super::RenderEffectRequirements>,
    camera: Entity,
    viewport: UVec2,
    origin: UVec2,
    target: NormalizedRenderTarget,
    msaa: u32,
    prepass: MeshPipelineKey,
}

#[derive(Resource, Clone, Debug, Default, serde::Serialize)]
pub struct RenderInventoryEvidence {
    #[serde(skip)]
    key: Option<Key>,
    #[serde(skip)]
    required: crate::gpu_readiness::assets::Requirements,
    #[serde(skip)]
    context: Option<PreparedGraphicsContext>,
    pub owner: Option<EntityId>,
    pub identity: Option<ContentHash>,
    pub generation: Option<u64>,
    pub epoch: u64,
    pub observed: u32,
    pub completed: Option<u32>,
    pub ready: bool,
    pub pipelines_total: usize,
    pub pipelines_pending: usize,
    pub pipelines_failed: usize,
    pub stable_frames: u8,
    pub assets: GpuAssetEvidence,
    pub view: GpuViewEvidence,
    pub effects: GpuEffectEvidence,
}

/// Opaque observation of an actual renderer fence. Every publication must revalidate it.
pub struct RenderInventoryToken {
    slot: Slot,
    key: Key,
    epoch: u64,
    required: crate::gpu_readiness::assets::Requirements,
}
impl RenderInventoryToken {
    /// Capture context only from a real, still-current completed inventory.
    #[must_use]
    pub fn graphics_context(&self, world: &World) -> Option<GraphicsContextReceipt> {
        if !self.is_current(
            world,
            self.key.owner,
            self.key.identity,
            self.key.generation,
        ) {
            return None;
        }
        // The context must belong to the inventory's completed submission,
        // rather than to a newer preparation that has no fence of its own yet.
        let prepared = evidence_at(world, self.slot, self.key.owner)?
            .context
            .clone()?;
        let receipt = GraphicsContextReceipt(prepared);
        (receipt.0.epoch == self.epoch && receipt.is_current(world)).then_some(receipt)
    }
    /// A staging consumer must bind a current receipt to its exact typed
    /// sources, rather than treating an unrelated completed fence as admission.
    #[must_use]
    pub fn covers_sources<M: bevy::pbr::Material>(
        &self,
        world: &World,
        meshes: &[Handle<Mesh>],
        materials: &[Handle<M>],
    ) -> bool {
        self.is_current(
            world,
            self.key.owner,
            self.key.identity,
            self.key.generation,
        ) && crate::gpu_readiness::assets::Requirements::custom(world, meshes, materials)
            .is_ok_and(|required| required == self.required)
    }

    /// Check a typed subset of this still-current renderer inventory. The
    /// receipt covers the full registered inventory; this never invents a
    /// completion for the subset or for a later scene publication.
    #[must_use]
    pub fn covers_subset<M: bevy::pbr::Material>(
        &self,
        world: &World,
        meshes: &[Handle<Mesh>],
        materials: &[Handle<M>],
    ) -> bool {
        self.is_current(
            world,
            self.key.owner,
            self.key.identity,
            self.key.generation,
        ) && current_at(world, self.slot, self.key.owner)
            .is_some_and(|(_, request)| request.material_type == std::any::TypeId::of::<M>())
            && crate::gpu_readiness::assets::Requirements::custom(world, meshes, materials)
                .is_ok_and(|required| self.required.contains(&required))
    }

    /// Completion of this still-current typed inventory, including scoped consumers.
    #[must_use]
    pub fn completed_frame(&self, world: &World) -> Option<u32> {
        self.is_current(
            world,
            self.key.owner,
            self.key.identity,
            self.key.generation,
        )
        .then(|| evidence_at(world, self.slot, self.key.owner).and_then(|e| e.completed))
        .flatten()
    }

    #[must_use]
    pub fn covers_frame(&self, world: &World, frame: u32) -> bool {
        self.completed_frame(world)
            .is_some_and(|completed| completed.checked_sub(frame).is_some())
    }
    #[must_use]
    pub fn is_current(
        &self,
        world: &World,
        owner: EntityId,
        identity: ContentHash,
        generation: u64,
    ) -> bool {
        self.key.owner == owner
            && self.key.identity == identity
            && self.key.generation == generation
            && current_at(world, self.slot, self.key.owner).is_some_and(|(key, request)| {
                ready(world, &key, request, self.slot)
                    && key == self.key
                    && request.required == self.required
                    && evidence_at(world, self.slot, self.key.owner)
                        .is_some_and(|evidence| evidence.epoch == self.epoch)
            })
    }
}

/// Returns no token from CPU handles, an absent renderer, a stale view or a retired request.
#[must_use]
pub fn acquire(
    world: &World,
    owner: EntityId,
    identity: ContentHash,
    generation: u64,
) -> Option<RenderInventoryToken> {
    acquire_at(world, owner, identity, generation, Slot::Primary)
}

#[derive(Clone, Copy)]
enum Slot {
    Primary,
    Scoped,
}

#[derive(Resource, Default)]
struct ScopedEvidence(BTreeMap<EntityId, RenderInventoryEvidence>);

#[must_use]
pub fn scoped_evidence(world: &World, owner: EntityId) -> Option<&RenderInventoryEvidence> {
    world.get_resource::<ScopedRequests>()?.0.get(&owner)?;
    world.get_resource::<ScopedEvidence>()?.0.get(&owner)
}

fn evidence_at(world: &World, slot: Slot, owner: EntityId) -> Option<&RenderInventoryEvidence> {
    match slot {
        Slot::Primary => world.get_resource::<RenderInventoryEvidence>(),
        Slot::Scoped => scoped_evidence(world, owner),
    }
}

#[must_use]
pub fn acquire_scoped(
    world: &World,
    owner: EntityId,
    identity: ContentHash,
    generation: u64,
) -> Option<RenderInventoryToken> {
    acquire_at(world, owner, identity, generation, Slot::Scoped)
}

fn acquire_at(
    world: &World,
    owner: EntityId,
    identity: ContentHash,
    generation: u64,
    slot: Slot,
) -> Option<RenderInventoryToken> {
    let (key, request) = current_at(world, slot, owner)?;
    (key.owner == owner
        && key.identity == identity
        && key.generation == generation
        && ready(world, &key, request, slot))
    .then(|| RenderInventoryToken {
        slot,
        key,
        epoch: evidence_at(world, slot, owner).map_or(0, |evidence| evidence.epoch),
        required: request.required.clone(),
    })
}

fn ready(world: &World, key: &Key, request: &Request, slot: Slot) -> bool {
    let Some(evidence) = evidence_at(world, slot, key.owner) else {
        return false;
    };
    let frame = world
        .get_resource::<FrameCount>()
        .map_or(0, |frame| frame.0);
    evidence.ready
        && evidence.epoch > 0
        && evidence.key.as_ref() == Some(key)
        && evidence.required == request.required
        && evidence.assets.inventory_ready
        && evidence.view.camera_resources_ready
        && (request.effects.is_none() || evidence.effects.public_resources_ready)
        && evidence.pipelines_total > 0
        && evidence.pipelines_pending == 0
        && evidence.pipelines_failed == 0
        && evidence.stable_frames >= 8
        && frame
            .checked_sub(evidence.observed)
            .is_some_and(|age| age <= 2)
        && evidence.completed.is_some_and(|completed| {
            evidence
                .observed
                .checked_sub(completed)
                .is_some_and(|age| age <= 64)
        })
}

#[derive(Resource, Default)]
struct Probe {
    request: Option<Request>,
    key: Option<Key>,
    epoch: u64,
    frame: u32,
    stable: u8,
    pipeline_count: usize,
    submitted: Option<u32>,
    completion: Arc<AtomicBool>,
    evidence: RenderInventoryEvidence,
    context: Option<PreparedGraphicsContext>,
}

#[derive(Resource, Default)]
struct ScopedProbes {
    epoch: u64,
    probes: BTreeMap<EntityId, Probe>,
}

impl Probe {
    fn reset(&mut self) {
        self.stable = 0;
        self.submitted = None;
        self.completion = Arc::new(AtomicBool::new(false));
        self.evidence = RenderInventoryEvidence::default();
    }
    fn publish(&mut self) {
        self.evidence.key.clone_from(&self.key);
        self.evidence.epoch = self.epoch;
        self.evidence.observed = self.frame;
        self.evidence.context.clone_from(&self.context);
        if let Some(key) = &self.key {
            self.evidence.owner = Some(key.owner);
            self.evidence.identity = Some(key.identity);
            self.evidence.generation = Some(key.generation);
        }
        if let Some(request) = &self.request {
            self.evidence.required.clone_from(&request.required);
        }
    }
}

pub struct RenderInventoryPlugin;
impl Plugin for RenderInventoryPlugin {
    fn build(&self, app: &mut App) {
        app.init_resource::<RenderInventoryEvidence>()
            .init_resource::<ScopedEvidence>();
        if let Some(render) = app.get_sub_app_mut(RenderApp) {
            render
                .init_resource::<Probe>()
                .init_resource::<ScopedProbes>()
                .init_resource::<GraphicsContextState>()
                .init_resource::<GraphicsContextInput>()
                .add_systems(RenderStartup, reset_device)
                .add_systems(ExtractSchedule, extract)
                .add_systems(Render, after_submission.in_set(RenderSystems::Cleanup));
        }
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy owns the resource wrapper passed to this render startup system"
)]
fn reset_device(
    mut probe: ResMut<Probe>,
    mut scoped: ResMut<ScopedProbes>,
    context: Res<GraphicsContextState>,
) {
    probe.epoch = probe.epoch.checked_add(1).unwrap_or(0);
    probe.key = None;
    probe.request = None;
    probe.reset();
    probe.context = None;
    scoped.epoch = probe.epoch;
    scoped.probes.clear();
    if let Ok(mut observation) = context.0.lock() {
        *observation = ContextObservation::default();
    }
}

fn current(world: &World) -> Option<(Key, &Request)> {
    let request = world.get_resource::<Request>()?;
    current_request(world, request)
}

fn current_at(world: &World, slot: Slot, owner: EntityId) -> Option<(Key, &Request)> {
    match slot {
        Slot::Primary => current(world),
        Slot::Scoped => current_request(
            world,
            world.get_resource::<ScopedRequests>()?.0.get(&owner)?,
        ),
    }
}

fn current_request<'a>(world: &World, request: &'a Request) -> Option<(Key, &'a Request)> {
    if !(request.sources_exist)(&request.required, world) {
        return None;
    }
    let view = graphics_view(world)?;
    Some((
        Key {
            owner: request.owner,
            identity: request.identity,
            generation: request.generation,
            serial: request.serial,
            effects: request.effects.clone(),
            camera: view.camera,
            viewport: view.viewport,
            origin: view.origin,
            target: view.target,
            msaa: view.msaa,
            prepass: view.prepass,
        },
        request,
    ))
}

fn graphics_view(world: &World) -> Option<GraphicsView> {
    use bevy::core_pipeline::prepass::{
        DeferredPrepass, DepthPrepass, MotionVectorPrepass, NormalPrepass,
    };
    let primary = world
        .try_query_filtered::<Entity, With<PrimaryWindow>>()
        .and_then(|mut query| query.single(world).ok());
    let mut query =
        world.try_query_filtered::<(Entity, &Camera, &RenderTarget), With<Camera3d>>()?;
    // Inspect the marker directly: an immutable query with Without<T> cannot
    // be created before that optional component has ever been registered.
    let mut cameras = query.iter(world).filter(|(entity, camera, _)| {
        camera.is_active
            && world
                .get::<crate::scene_camera::OffscreenSceneCamera>(*entity)
                .is_none()
    });
    let (entity, camera, target) = cameras.next()?;
    let viewport = camera.physical_viewport_size()?;
    if cameras.next().is_some() || viewport.min_element() == 0 {
        return None;
    }
    let mut prepass = MeshPipelineKey::empty();
    if world.get::<DepthPrepass>(entity).is_some() {
        prepass |= MeshPipelineKey::DEPTH_PREPASS;
    }
    if world.get::<NormalPrepass>(entity).is_some() {
        prepass |= MeshPipelineKey::NORMAL_PREPASS;
    }
    if world.get::<MotionVectorPrepass>(entity).is_some() {
        prepass |= MeshPipelineKey::MOTION_VECTOR_PREPASS;
    }
    if world.get::<DeferredPrepass>(entity).is_some() {
        prepass |= MeshPipelineKey::DEFERRED_PREPASS;
    }
    Some(GraphicsView {
        camera: entity,
        viewport,
        origin: camera
            .viewport
            .as_ref()
            .map_or(UVec2::ZERO, |v| v.physical_position),
        target: target.normalize(primary)?,
        msaa: world.get::<Msaa>(entity).map_or(1, Msaa::samples),
        prepass,
    })
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy owns the resource wrapper shared with the main world during extraction"
)]
fn extract(
    mut main: ResMut<MainWorld>,
    mut probe: ResMut<Probe>,
    mut scoped: ResMut<ScopedProbes>,
    context: Res<GraphicsContextState>,
    mut context_input: ResMut<GraphicsContextInput>,
) {
    context_input.view = graphics_view(&main);
    context_input.frame = main.get_resource::<FrameCount>().map_or(0, |frame| frame.0);
    main.insert_resource((*context).clone());
    let candidate = current(&main).map(|(key, request)| (key, request.clone()));
    let key = candidate.as_ref().map(|(key, _)| key.clone());
    let changed = probe.key != key
        || candidate.as_ref().map(|(_, request)| &request.required)
            != probe.request.as_ref().map(|request| &request.required);
    if changed {
        probe.reset();
    }
    probe.key = key;
    probe.request = candidate.map(|(_, request)| request);
    main.insert_resource(probe.evidence.clone());
    probe.frame = main.get_resource::<FrameCount>().map_or(0, |frame| frame.0);
    let requests = main
        .get_resource::<ScopedRequests>()
        .map(|requests| requests.0.clone())
        .unwrap_or_default();
    scoped
        .probes
        .retain(|owner, _| requests.contains_key(owner));
    let epoch = scoped.epoch;
    for (owner, request) in &requests {
        let next = current_request(&main, request).map(|(key, _)| key);
        let probe = scoped
            .probes
            .entry(*owner)
            .or_insert_with(|| Probe { epoch, ..default() });
        if probe.key != next
            || probe.request.as_ref().map(|request| &request.required) != Some(&request.required)
        {
            probe.reset();
        }
        probe.key = next;
        probe.request = probe.key.as_ref().map(|_| request.clone());
        probe.frame = main.get_resource::<FrameCount>().map_or(0, |frame| frame.0);
    }
    main.insert_resource(ScopedEvidence(
        scoped
            .probes
            .iter()
            .map(|(owner, probe)| (*owner, probe.evidence.clone()))
            .collect(),
    ));
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    reason = "One Bevy render system observes the same submission's inventory, view, effects and prepared context"
)]
fn after_submission(
    mut probe: ResMut<Probe>,
    mut scoped: ResMut<ScopedProbes>,
    pipelines: Res<PipelineCache>,
    queue: Res<RenderQueue>,
    assets: GpuAssets,
    views: GpuViews,
    effects: GpuEffects,
    prepared_views: PreparedViews,
    context_input: Res<GraphicsContextInput>,
    context: Res<GraphicsContextState>,
) {
    let prepared = context_input
        .view
        .as_ref()
        .and_then(|view| prepared_views.observe(view, probe.epoch));
    let view_ready = context_input.view.as_ref().is_some_and(|view| {
        views
            .observe_camera(view.camera, view.viewport, view.origin, &view.target)
            .camera_resources_ready
    });
    let mut count = 0;
    let mut pipelines_ready = true;
    for pipeline in pipelines.pipelines() {
        count += 1;
        pipelines_ready &= matches!(pipeline.state, CachedPipelineState::Ok(_));
    }
    if let Ok(mut observed) = context.0.lock() {
        *observed = ContextObservation {
            ready: prepared.is_some() && view_ready && count > 0 && pipelines_ready,
            prepared: prepared.clone(),
            observed: context_input.frame,
        };
    }
    if probe.context != prepared {
        probe.reset();
        probe.context.clone_from(&prepared);
    }
    observe_probe(&mut probe, &pipelines, &queue, &assets, &views, &effects);
    for probe in scoped.probes.values_mut() {
        if probe.context != prepared {
            probe.reset();
            probe.context.clone_from(&prepared);
        }
        observe_probe(probe, &pipelines, &queue, &assets, &views, &effects);
    }
}

fn observe_probe(
    probe: &mut Probe,
    pipelines: &PipelineCache,
    queue: &RenderQueue,
    assets: &GpuAssets<'_>,
    views: &GpuViews<'_, '_>,
    effects: &GpuEffects<'_, '_>,
) {
    let mut total = 0;
    let mut pending = 0;
    let mut failed = 0;
    for pipeline in pipelines.pipelines() {
        total += 1;
        match pipeline.state {
            CachedPipelineState::Ok(_) => {}
            CachedPipelineState::Queued | CachedPipelineState::Creating(_) => pending += 1,
            CachedPipelineState::Err(_) => failed += 1,
        }
    }
    let view = probe
        .key
        .as_ref()
        .map_or_else(GpuViewEvidence::default, |key| {
            views.observe_camera(key.camera, key.viewport, key.origin, &key.target)
        });
    let inventory = probe
        .request
        .as_ref()
        .map_or_else(GpuAssetEvidence::default, |request| {
            assets.observe(&request.required)
        });
    let effect_evidence =
        probe
            .key
            .as_ref()
            .zip(probe.request.as_ref())
            .and_then(|(key, request)| {
                request.effects.as_ref().map(|required| {
                    effects.observe_camera(
                        (key.camera, key.viewport, key.origin, &key.target),
                        required,
                        pipelines,
                    )
                })
            });
    if probe.key.is_none()
        || probe.epoch == 0
        || total == 0
        || pending != 0
        || failed != 0
        || total != probe.pipeline_count
        || !view.camera_resources_ready
        || !inventory.inventory_ready
        || effect_evidence
            .as_ref()
            .is_some_and(|evidence| !evidence.public_resources_ready)
    {
        probe.reset();
    } else {
        probe.stable = probe.stable.saturating_add(1);
    }
    probe.pipeline_count = total;
    probe.publish();
    probe.evidence.pipelines_total = total;
    probe.evidence.pipelines_pending = pending;
    probe.evidence.pipelines_failed = failed;
    probe.evidence.stable_frames = probe.stable;
    probe.evidence.assets = inventory;
    probe.evidence.view = view;
    probe.evidence.effects = effect_evidence.unwrap_or_default();
    if probe.stable < 8 {
        return;
    }
    if probe.completion.load(Ordering::Acquire) {
        probe.evidence.completed = probe.submitted.take();
        probe.completion = Arc::new(AtomicBool::new(false));
    }
    probe.evidence.ready = probe
        .evidence
        .completed
        .is_some_and(|frame| probe.frame.checked_sub(frame).is_some_and(|age| age <= 64));
    if probe.submitted.is_none() {
        probe.submitted = Some(probe.frame);
        let completion = Arc::clone(&probe.completion);
        queue.on_submitted_work_done(move || completion.store(true, Ordering::Release));
    }
}
