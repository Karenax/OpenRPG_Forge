//! GPU inventory and queue completion for the current governed forest.
//! This does not prove pixels, HLOD aggregation, or frame-time performance.
use super::{PreviewState, quality_ready};
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
pub struct VegetationPrototypeGpuEvidence {
    #[serde(skip)]
    key: Option<Key>,
    #[serde(skip)]
    requirements: Requirements,
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
type Evidence = VegetationPrototypeGpuEvidence;

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

/// Opt-in proof barrier for hosts that require the governed species projection.
pub struct VegetationPrototypeGpuReadinessPlugin;

impl Plugin for VegetationPrototypeGpuReadinessPlugin {
    fn build(&self, app: &mut App) {
        install(app);
        app.init_resource::<VegetationPrototypeRuntimeStatus>()
            .add_systems(
                PostUpdate,
                observe_runtime
                    .after(bevy::transform::TransformSystems::Propagate)
                    .before(crate::capture_readiness::capture_at_requested_frame),
            );
    }
}

#[derive(Clone, Debug, Default, PartialEq, Eq, Resource, serde::Serialize, serde::Deserialize)]
pub struct VegetationPrototypeRuntimeStatus {
    pub ready: bool,
    pub cpu_ready: bool,
    pub collisions_ready: bool,
    pub error: Option<String>,
}

fn observe_runtime(world: &mut World) {
    let status = runtime_status(world);
    world.insert_resource(status);
}

pub fn runtime_status(world: &World) -> VegetationPrototypeRuntimeStatus {
    let error = match world.get_resource::<crate::VegetationPrototypeLoadStatus>() {
        Some(crate::VegetationPrototypeLoadStatus::Failed(error)) => Some(error.clone()),
        _ => world
            .get_resource::<super::super::VegetationProjectionStats>()
            .and_then(|stats| stats.prototype_preview_error.clone()),
    };
    let cpu_ready = quality_ready(world);
    let collisions_ready = super::super::collision::governed_forest_collisions_ready(world);
    VegetationPrototypeRuntimeStatus {
        ready: error.is_none() && cpu_ready && collisions_ready && governed_forest_gpu_ready(world),
        cpu_ready,
        collisions_ready,
        error,
    }
}

fn install(app: &mut App) {
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
        self.evidence.requirements.clone_from(&self.assets);
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

pub fn governed_forest_gpu_ready(world: &World) -> bool {
    let Some((key, required)) = current_snapshot(world) else {
        return false;
    };
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
        && evidence.key.as_ref() == Some(&key)
        && evidence.requirements == required
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

fn current_key(world: &World) -> Option<Key> {
    if !world.contains_resource::<super::super::VegetationPrototypeForestPreview>()
        || !quality_ready(world)
    {
        return None;
    }
    let generation = world.get_resource::<PreviewState>()?.generation;
    if generation == 0 {
        return None;
    }
    let primary = world
        .try_query_filtered::<Entity, With<PrimaryWindow>>()
        .and_then(|mut query| query.single(world).ok());
    let mut cameras =
        world.try_query_filtered::<(Entity, &Camera, &RenderTarget), With<Camera3d>>()?;
    let mut active = cameras
        .iter(world)
        .filter(|(_, camera, _)| camera.is_active);
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

fn current_snapshot(world: &World) -> Option<(Key, Requirements)> {
    let key = current_key(world)?;
    let state = world.get_resource::<PreviewState>()?;
    let required = Requirements::vegetation(world, state.entities.values().copied())?;
    Some((key, required))
}

fn extract(mut main: ResMut<MainWorld>, mut probe: ResMut<Probe>) {
    let (key, requirements) = current_snapshot(&main)
        .map_or((None, Requirements::default()), |(key, required)| {
            (Some(key), required)
        });
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
mod tests {
    use super::*;

    fn fixture() -> App {
        let mut app = super::super::tests::preview_app();
        super::super::project(app.world_mut());
        app.insert_resource(FrameCount(20));
        app.world_mut().spawn((
            Camera3d::default(),
            Camera {
                computed: bevy::camera::ComputedCameraValues {
                    target_info: Some(bevy::camera::RenderTargetInfo {
                        physical_size: UVec2::new(640, 480),
                        scale_factor: 1.0,
                    }),
                    ..default()
                },
                ..default()
            },
            RenderTarget::None {
                size: UVec2::new(640, 480),
            },
        ));
        app
    }

    // Synthetic evidence tests only the refusal contract; it is never a GPU qualification.
    fn acknowledge(world: &mut World) {
        let (key, requirements) = current_snapshot(world).unwrap();
        let generation = key.generation;
        let frame = world.resource::<FrameCount>().0;
        world.insert_resource(Evidence {
            key: Some(key),
            requirements,
            generation: Some(generation),
            epoch: 1,
            observed: frame,
            completed: Some(frame),
            ready: true,
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

    #[test]
    fn gpu_receipt_requires_current_projection_camera_and_fresh_completion() {
        let mut app = fixture();
        assert!(!governed_forest_gpu_ready(app.world()));
        acknowledge(app.world_mut());
        assert!(governed_forest_gpu_ready(app.world()));
        app.world_mut().resource_mut::<FrameCount>().0 += 3;
        assert!(!governed_forest_gpu_ready(app.world()));
        acknowledge(app.world_mut());
        let generation = app.world().resource::<PreviewState>().generation;
        app.world_mut()
            .resource_mut::<super::super::super::VegetationPresentation>()
            .0
            .wind_strength *= 0.5;
        assert!(!governed_forest_gpu_ready(app.world()));
        super::super::project(app.world_mut());
        assert!(app.world().resource::<PreviewState>().generation > generation);
        assert!(!governed_forest_gpu_ready(app.world()));
        acknowledge(app.world_mut());
        assert!(governed_forest_gpu_ready(app.world()));
        let camera = app
            .world_mut()
            .query_filtered::<Entity, With<Camera3d>>()
            .single(app.world())
            .unwrap();
        app.world_mut().get_mut::<Camera>(camera).unwrap().is_active = false;
        assert!(!governed_forest_gpu_ready(app.world()));
    }

    #[test]
    fn source_mesh_or_owned_entity_removal_revokes_a_prior_gpu_receipt() {
        let mut app = fixture();
        acknowledge(app.world_mut());
        let entity = *app
            .world()
            .resource::<PreviewState>()
            .entities
            .values()
            .next()
            .unwrap();
        let handle = app.world().get::<Mesh3d>(entity).unwrap().0.clone();
        let mesh = app
            .world_mut()
            .resource_mut::<Assets<Mesh>>()
            .remove(handle.id())
            .unwrap();
        assert!(!governed_forest_gpu_ready(app.world()));
        app.world_mut()
            .resource_mut::<Assets<Mesh>>()
            .insert(handle.id(), mesh)
            .unwrap();
        assert!(governed_forest_gpu_ready(app.world()));
        app.world_mut().despawn(entity);
        assert!(!governed_forest_gpu_ready(app.world()));
    }
}
