//! Upload acknowledgement for the explicit proxy inventory, using the shared
//! public renderer-resource checks. This is not proof of pixels or GPU timing.
use super::State;
use crate::gpu_readiness::{
    assets::{GpuAssetEvidence, GpuAssets, Requirements},
    views::{GpuViewEvidence, GpuViews},
};
use bevy::{
    camera::{NormalizedRenderTarget, RenderTarget},
    diagnostic::FrameCount,
    prelude::*,
    render::{
        ExtractSchedule, MainWorld, Render, RenderApp, RenderStartup, RenderSystems,
        render_resource::{CachedPipelineState, PipelineCache},
        renderer::RenderQueue,
    },
    window::PrimaryWindow,
};
use std::sync::{
    Arc,
    atomic::{AtomicBool, Ordering},
};

#[derive(Clone, Debug, PartialEq, Eq)]
struct Key {
    generation: u64,
    camera: Entity,
    viewport: UVec2,
    origin: UVec2,
    target: NormalizedRenderTarget,
}

#[derive(Resource, Clone, Debug, Default, serde::Serialize)]
pub struct StaticHlodGpuEvidence {
    #[serde(skip)]
    key: Option<Key>,
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
}
type Evidence = StaticHlodGpuEvidence;

#[derive(Resource, Default)]
struct Probe {
    key: Option<Key>,
    assets: Requirements,
    epoch: u64,
    frame: u32,
    stable: u8,
    pipeline_count: usize,
    submitted: Option<u32>,
    completion: Arc<AtomicBool>,
    evidence: Evidence,
}

pub(super) fn install(app: &mut App) {
    app.init_resource::<Evidence>();
    if let Some(render_app) = app.get_sub_app_mut(RenderApp) {
        render_app
            .init_resource::<Probe>()
            .add_systems(RenderStartup, reset_device)
            .add_systems(ExtractSchedule, extract)
            .add_systems(Render, after_submission.in_set(RenderSystems::Cleanup));
    }
}

fn reset_device(mut probe: ResMut<Probe>) {
    probe.epoch = probe.epoch.checked_add(1).unwrap_or(0);
    probe.key = None;
    probe.reset();
}

impl Probe {
    fn publish_identity(&mut self) {
        self.evidence.key.clone_from(&self.key);
        self.evidence.generation = self.key.as_ref().map(|key| key.generation);
        self.evidence.epoch = self.epoch;
        self.evidence.observed = self.frame;
    }

    fn reset(&mut self) {
        self.completion = Arc::new(AtomicBool::new(false));
        self.submitted = None;
        self.stable = 0;
        self.evidence = Evidence::default();
    }
}

pub(super) fn ready(world: &World, generation: u64) -> bool {
    let Some(evidence) = world.get_resource::<Evidence>() else {
        return false;
    };
    let frame = world
        .get_resource::<FrameCount>()
        .map_or(0, |frame| frame.0);
    evidence.ready
        && evidence.epoch > 0
        && evidence.assets.inventory_ready
        && evidence.view.camera_resources_ready
        && evidence.pipelines_total > 0
        && evidence.pipelines_pending == 0
        && evidence.pipelines_failed == 0
        && evidence.stable_frames >= 8
        && evidence.key == current_key(world, generation)
        && evidence.key.is_some()
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

fn current_key(world: &World, generation: u64) -> Option<Key> {
    let primary = world
        .try_query_filtered::<Entity, With<PrimaryWindow>>()
        .and_then(|mut query| query.single(world).ok());
    let mut cameras =
        world.try_query_filtered::<(Entity, &Camera, &RenderTarget), With<Camera3d>>()?;
    // Match the shared scene view: refraction and other offscreen passes do
    // not make the player's view ambiguous. Check the marker directly so the
    // immutable query also works in scenes where it was never registered.
    let mut active = cameras.iter(world).filter(|(entity, camera, _)| {
        camera.is_active
            && world
                .get::<crate::scene_camera::OffscreenSceneCamera>(*entity)
                .is_none()
    });
    let (entity, camera, target) = active.next()?;
    let viewport = camera.physical_viewport_size()?;
    (active.next().is_none() && viewport.min_element() > 0).then_some(Key {
        generation,
        camera: entity,
        viewport,
        origin: camera
            .viewport
            .as_ref()
            .map_or(UVec2::ZERO, |view| view.physical_position),
        target: target.normalize(primary)?,
    })
}

fn extract(mut main: ResMut<MainWorld>, mut probe: ResMut<Probe>) {
    let state = main.get_resource::<State>();
    let key = state
        .filter(|state| !state.live.is_empty())
        .and_then(|state| current_key(&main, state.generation));
    let requirements = if let Some(state) = state.filter(|_| key.is_some()) {
        let request = state.request.as_ref().expect("owned live batch request");
        Requirements::subset(
            state.live.iter().map(|live| live.mesh.id()),
            state.live.iter().map(|live| live.group.material),
            request
                .sources
                .iter()
                .filter(|source| source.node.is_some())
                .flat_map(|source| source.images.iter().copied()),
        )
    } else {
        Requirements::default()
    };
    if probe.key != key || probe.assets != requirements {
        probe.reset();
        probe.key = key;
        probe.assets = requirements;
    }
    main.insert_resource(probe.evidence.clone());
    probe.frame = main.get_resource::<FrameCount>().map_or(0, |frame| frame.0);
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy render system parameters"
)]
fn after_submission(
    mut probe: ResMut<Probe>,
    pipelines: Res<PipelineCache>,
    queue: Res<RenderQueue>,
    assets: GpuAssets,
    views: GpuViews,
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
    let inventory = assets.observe(&probe.assets);
    if probe.epoch == 0
        || total == 0
        || pending != 0
        || failed != 0
        || total != probe.pipeline_count
        || !view.camera_resources_ready
        || !inventory.inventory_ready
    {
        probe.reset();
    } else {
        probe.stable = probe.stable.saturating_add(1);
    }
    probe.pipeline_count = total;
    probe.publish_identity();
    probe.evidence.pipelines_total = total;
    probe.evidence.pipelines_pending = pending;
    probe.evidence.pipelines_failed = failed;
    probe.evidence.stable_frames = probe.stable;
    probe.evidence.assets = inventory;
    probe.evidence.view = view;
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

#[cfg(test)]
pub(super) fn acknowledge(world: &mut World, generation: u64) {
    let frame = world.resource::<FrameCount>().0;
    world.insert_resource(Evidence {
        key: current_key(world, generation),
        epoch: 1,
        observed: frame,
        completed: Some(frame),
        ready: true,
        generation: Some(generation),
        pipelines_total: 1,
        stable_frames: 8,
        assets: GpuAssetEvidence {
            inventory_ready: true,
            ..default()
        },
        view: GpuViewEvidence {
            camera_resources_ready: true,
            ..default()
        },
        ..default()
    });
}
