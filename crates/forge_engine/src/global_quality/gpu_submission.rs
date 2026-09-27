//! Queue completion evidence, not proof that every requested effect rendered correctly.
use super::GlobalQualityStats;
use bevy::{
    camera::{NormalizedRenderTarget, RenderTarget},
    diagnostic::FrameCount,
    prelude::*,
    render::{
        ExtractSchedule, MainWorld, Render, RenderApp, RenderStartup, RenderSystems,
        camera::ExtractedCamera,
        render_resource::{CachedPipelineState, PipelineCache},
        renderer::RenderQueue,
        sync_world::MainEntity,
        view::ViewTarget,
    },
    window::PrimaryWindow,
};
use forge_world::quality_application::QualityApplicationIdentity;
use serde::Serialize;
use std::sync::{
    Arc,
    atomic::{AtomicBool, Ordering},
};

const STABLE_FRAMES: u8 = 8;
const MAX_COMPLETION_AGE: u32 = 64;
mod assets;
pub use assets::GpuAssetEvidence;
mod views;
pub use views::GpuViewEvidence;
mod effects;
pub use effects::GpuEffectEvidence;

#[derive(Clone, Debug, Default, PartialEq, Resource, Serialize)]
pub struct GpuSubmissionEvidence {
    #[serde(skip)]
    key: Option<Key>,
    pub identity: Option<QualityApplicationIdentity>,
    pub renderer_epoch: u64,
    pub observed_frame: u32,
    pub completed_frame: Option<u32>,
    pub pipelines_total: usize,
    pub pipelines_pending: usize,
    pub pipelines_failed: usize,
    pub matching_view_ready: bool,
    pub queue_work_completed: bool,
    pub assets: GpuAssetEvidence,
    pub view: GpuViewEvidence,
    pub effects: GpuEffectEvidence,
}

impl GpuSubmissionEvidence {
    pub(super) fn ready_for(&self, key: &Key, frame: u32, epoch: Option<u64>) -> Option<u64> {
        let fresh = frame
            .checked_sub(self.observed_frame)
            .is_some_and(|age| age <= 2);
        let completed = self.completed_frame.is_some_and(|f| {
            self.observed_frame
                .checked_sub(f)
                .is_some_and(|age| age <= MAX_COMPLETION_AGE)
        });
        (self.key.as_ref() == Some(key)
            && self.identity == Some(key.identity)
            && self.renderer_epoch > 0
            && epoch.is_none_or(|epoch| epoch == self.renderer_epoch)
            && fresh
            && completed
            && self.queue_work_completed
            && self.pipelines_total > 0
            && self.pipelines_pending == 0
            && self.pipelines_failed == 0
            && self.matching_view_ready
            && self.view.camera_resources_ready
            && self.assets.inventory_ready
            && self.effects.public_resources_ready)
            .then_some(self.renderer_epoch)
    }

    pub(super) fn pipeline_failure_for(
        &self,
        identity: QualityApplicationIdentity,
        frame: u32,
    ) -> bool {
        self.identity == Some(identity)
            && self.pipelines_failed > 0
            && frame
                .checked_sub(self.observed_frame)
                .is_some_and(|age| age <= 2)
    }
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub(super) struct Key {
    identity: QualityApplicationIdentity,
    camera: Entity,
    viewport: UVec2,
    origin: UVec2,
    target: NormalizedRenderTarget,
}

#[derive(Resource, Default)]
struct Probe {
    assets: assets::Requirements,
    effects: Option<effects::Requirements>,
    key: Option<Key>,
    epoch: u64,
    frame: u32,
    stable: u8,
    submitted: Option<u32>,
    completion: Arc<AtomicBool>,
    evidence: GpuSubmissionEvidence,
}

impl Probe {
    fn observe_readiness(&mut self, total: usize, pending: usize, failed: usize, view_ready: bool) {
        if !view_ready
            || self.epoch == 0
            || total == 0
            || pending != 0
            || failed != 0
            || total != self.evidence.pipelines_total
        {
            self.reset_completion();
        } else {
            self.stable = self.stable.saturating_add(1);
        }
        self.evidence.pipelines_total = total;
        self.evidence.pipelines_pending = pending;
        self.evidence.pipelines_failed = failed;
        self.evidence.matching_view_ready = view_ready;
    }
    fn reset_completion(&mut self) {
        self.completion = Arc::new(AtomicBool::new(false));
        self.submitted = None;
        self.evidence.completed_frame = None;
        self.evidence.queue_work_completed = false;
        self.stable = 0;
    }

    fn select(&mut self, key: Option<Key>) {
        if self.key != key {
            self.reset_completion();
            self.key.clone_from(&key);
            // Never relabel the previous render observation with a new plan/view identity.
            self.evidence = GpuSubmissionEvidence {
                identity: key.as_ref().map(|key| key.identity),
                key,
                renderer_epoch: self.epoch,
                ..default()
            };
        }
    }

    fn completed_recently(&self, frame: u32) -> bool {
        self.evidence.completed_frame.is_some_and(|completed| {
            frame
                .checked_sub(completed)
                .is_some_and(|age| age <= MAX_COMPLETION_AGE)
        })
    }
}

pub(super) fn install(app: &mut App) {
    app.init_resource::<GpuSubmissionEvidence>();
    if let Some(render_app) = app.get_sub_app_mut(RenderApp) {
        render_app
            .init_resource::<Probe>()
            .add_systems(RenderStartup, reset_device)
            .add_systems(ExtractSchedule, extract)
            // Cleanup is ordered after Render (render_system has submitted and presented).
            .add_systems(Render, after_submission.in_set(RenderSystems::Cleanup));
    }
}

fn reset_device(mut probe: ResMut<Probe>) {
    probe.reset_completion();
    probe.key = None;
    probe.epoch = probe.epoch.checked_add(1).unwrap_or(0);
    probe.evidence = GpuSubmissionEvidence {
        renderer_epoch: probe.epoch,
        ..default()
    };
}

fn extract(mut main: ResMut<MainWorld>, mut probe: ResMut<Probe>) {
    let frame = main.get_resource::<FrameCount>().map_or(0, |frame| frame.0);
    let identity = main
        .get_resource::<GlobalQualityStats>()
        .and_then(|stats| stats.application.as_ref())
        .filter(|application| application.projected_domains.iter().all(|yes| *yes))
        .and_then(|application| application.identity);
    let key = identity.and_then(|identity| current_key(&mut main, identity));
    probe.select(key.clone());
    let effect_requirements = effects::collect(&main, key.as_ref());
    if probe.effects != effect_requirements {
        probe.reset_completion();
        probe.effects = effect_requirements;
        probe.evidence.effects = GpuEffectEvidence::default();
    }
    let requirements = if key.is_some() {
        assets::collect(&mut main)
    } else {
        assets::Requirements::default()
    };
    if probe.assets != requirements {
        probe.reset_completion();
        probe.evidence.assets = GpuAssetEvidence::default();
        probe.assets = requirements;
    }
    let mut evidence = probe.evidence.clone();
    evidence.queue_work_completed = key.is_some()
        && probe.completed_recently(frame)
        && evidence.pipelines_pending == 0
        && evidence.pipelines_failed == 0
        && evidence.matching_view_ready;
    evidence.queue_work_completed &= evidence.assets.inventory_ready;
    evidence.queue_work_completed &= evidence.view.camera_resources_ready;
    evidence.queue_work_completed &= evidence.effects.public_resources_ready;
    main.insert_resource(evidence);
    probe.frame = frame;
}

pub(super) fn current_key(main: &mut World, identity: QualityApplicationIdentity) -> Option<Key> {
    let primary = main
        .query_filtered::<Entity, With<PrimaryWindow>>()
        .single(main)
        .ok();
    let mut cameras = main.query_filtered::<(Entity, &Camera, &RenderTarget), With<Camera3d>>();
    let mut active = cameras.iter(main).filter(|(_, camera, _)| camera.is_active);
    active.next().and_then(|(entity, camera, target)| {
        let viewport = camera.physical_viewport_size()?;
        (active.next().is_none() && viewport.min_element() > 0).then_some(Key {
            identity,
            camera: entity,
            viewport,
            origin: camera
                .viewport
                .as_ref()
                .map_or(UVec2::ZERO, |v| v.physical_position),
            target: target.normalize(primary)?,
        })
    })
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy render system parameters"
)]
fn after_submission(
    mut probe: ResMut<Probe>,
    pipelines: Res<PipelineCache>,
    queue: Res<RenderQueue>,
    views: Query<(&MainEntity, &ExtractedCamera), With<ViewTarget>>,
    assets: assets::GpuAssets,
    view_resources: views::GpuViews,
    effect_resources: effects::GpuEffects,
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
    let view_ready = probe.key.as_ref().is_some_and(|key| {
        views
            .iter()
            .filter(|(main, camera)| {
                main.id() == key.camera && camera.physical_viewport_size == Some(key.viewport)
            })
            .count()
            == 1
    });
    probe.observe_readiness(total, pending, failed, view_ready);
    let asset_evidence = assets.observe(&probe.assets);
    if !asset_evidence.inventory_ready {
        probe.reset_completion();
    }
    probe.evidence.assets = asset_evidence;
    let view_evidence = view_resources.observe(probe.key.as_ref());
    if !view_evidence.camera_resources_ready {
        probe.reset_completion();
    }
    probe.evidence.view = view_evidence;
    let effect_evidence =
        effect_resources.observe(probe.key.as_ref(), probe.effects.as_ref(), &pipelines);
    if !effect_evidence.public_resources_ready {
        probe.reset_completion();
    }
    probe.evidence.effects = effect_evidence;
    probe.evidence.observed_frame = probe.frame;
    if probe.stable < STABLE_FRAMES {
        return;
    }
    if probe.completion.load(Ordering::Acquire) {
        probe.evidence.completed_frame = probe.submitted.take();
        probe.completion = Arc::new(AtomicBool::new(false));
    }
    // One outstanding callback per current key; no polling loop, image readback or blocking wait.
    if probe.submitted.is_none() {
        let completion = Arc::clone(&probe.completion);
        probe.submitted = Some(probe.frame);
        queue.on_submitted_work_done(move || completion.store(true, Ordering::Release));
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn prepared_receipt() -> GpuSubmissionEvidence {
        GpuSubmissionEvidence {
            key: Some(key()),
            identity: Some(key().identity),
            renderer_epoch: 1,
            observed_frame: 20,
            completed_frame: Some(19),
            pipelines_total: 12,
            matching_view_ready: true,
            queue_work_completed: true,
            assets: GpuAssetEvidence {
                inventory_ready: true,
                ..default()
            },
            view: GpuViewEvidence {
                camera_resources_ready: true,
                ..default()
            },
            effects: GpuEffectEvidence {
                public_resources_ready: true,
                ..default()
            },
            ..default()
        }
    }

    #[test]
    fn readiness_requires_current_resources_identity_epoch_and_completed_work() {
        let receipt = prepared_receipt();
        assert_eq!(receipt.ready_for(&key(), 21, None), Some(1));
        assert_eq!(receipt.ready_for(&key(), 21, Some(1)), Some(1));
        assert_eq!(receipt.ready_for(&key(), 21, Some(2)), None);
        assert_eq!(receipt.ready_for(&key(), 19, None), None);
        assert_eq!(receipt.ready_for(&key(), 23, None), None);
        for change in [
            |e: &mut GpuSubmissionEvidence| e.key = None,
            |e: &mut GpuSubmissionEvidence| e.identity = None,
            |e: &mut GpuSubmissionEvidence| e.renderer_epoch = 0,
            |e: &mut GpuSubmissionEvidence| e.completed_frame = None,
            |e: &mut GpuSubmissionEvidence| e.completed_frame = Some(22),
            |e: &mut GpuSubmissionEvidence| e.queue_work_completed = false,
            |e: &mut GpuSubmissionEvidence| e.pipelines_total = 0,
            |e: &mut GpuSubmissionEvidence| e.pipelines_pending = 1,
            |e: &mut GpuSubmissionEvidence| e.pipelines_failed = 1,
            |e: &mut GpuSubmissionEvidence| e.matching_view_ready = false,
            |e: &mut GpuSubmissionEvidence| e.assets.inventory_ready = false,
            |e: &mut GpuSubmissionEvidence| e.view.camera_resources_ready = false,
            |e: &mut GpuSubmissionEvidence| e.effects.public_resources_ready = false,
        ] {
            let mut invalid = receipt.clone();
            change(&mut invalid);
            assert_eq!(invalid.ready_for(&key(), 21, None), None);
        }
        let mut key = key();
        key.identity.adapter_generation += 1;
        assert_eq!(receipt.ready_for(&key, 21, None), None);
    }

    #[test]
    fn current_key_tracks_render_target_origin_and_camera_ambiguity() {
        use bevy::camera::{RenderTargetInfo, Viewport};
        let mut world = World::new();
        let entity = world
            .spawn((
                Camera3d::default(),
                Camera {
                    computed: bevy::camera::ComputedCameraValues {
                        target_info: Some(RenderTargetInfo {
                            physical_size: UVec2::new(1920, 1080),
                            scale_factor: 1.0,
                        }),
                        ..default()
                    },
                    ..default()
                },
                RenderTarget::None {
                    size: UVec2::new(1920, 1080),
                },
            ))
            .id();
        let original = current_key(&mut world, key().identity).unwrap();
        world.get_mut::<Camera>(entity).unwrap().viewport = Some(Viewport {
            physical_position: UVec2::new(1, 0),
            physical_size: original.viewport,
            ..default()
        });
        assert_ne!(
            current_key(&mut world, key().identity),
            Some(original.clone())
        );
        world.get_mut::<Camera>(entity).unwrap().viewport = None;
        world.entity_mut(entity).insert(RenderTarget::None {
            size: UVec2::new(1280, 720),
        });
        assert_ne!(current_key(&mut world, key().identity), Some(original));
        world.spawn((Camera3d::default(), Camera::default()));
        assert!(current_key(&mut world, key().identity).is_none());
    }

    #[test]
    fn complete_render_probe_parameters_initialize_without_access_conflicts() {
        use bevy::ecs::system::{IntoSystem, System};
        let mut world = World::new();
        let mut system = IntoSystem::into_system(after_submission);
        system.initialize(&mut world);
    }

    #[test]
    fn pipeline_changes_and_missing_views_revoke_completion() {
        for (total, pending, failed, view_ready) in [
            (0, 0, 0, true),
            (12, 1, 0, true),
            (12, 0, 1, true),
            (12, 0, 0, false),
            (13, 0, 0, true),
        ] {
            let mut probe = Probe {
                epoch: 1,
                ..default()
            };
            probe.observe_readiness(12, 0, 0, true);
            for _ in 0..STABLE_FRAMES {
                probe.observe_readiness(12, 0, 0, true);
            }
            assert_eq!(probe.stable, STABLE_FRAMES);
            probe.evidence.completed_frame = Some(10);
            probe.observe_readiness(total, pending, failed, view_ready);
            assert_eq!(probe.stable, 0);
            assert!(!probe.completed_recently(11));
        }
    }

    #[test]
    fn pipeline_failure_requires_the_current_identity_and_fresh_render_observation() {
        let evidence = GpuSubmissionEvidence {
            identity: Some(key().identity),
            observed_frame: 10,
            pipelines_failed: 1,
            ..default()
        };
        assert!(evidence.pipeline_failure_for(key().identity, 11));
        assert!(!evidence.pipeline_failure_for(key().identity, 9));
        assert!(!evidence.pipeline_failure_for(key().identity, 13));
        let mut identity = key().identity;
        identity.sequence += 1;
        assert!(!evidence.pipeline_failure_for(identity, 11));
    }

    pub(super) fn key() -> Key {
        Key {
            identity: QualityApplicationIdentity {
                sequence: 1,
                plan_hash: "ab".repeat(32).parse().unwrap(),
                adapter_generation: 1,
            },
            camera: Entity::from_raw_u32(1).unwrap(),
            viewport: UVec2::new(1920, 1080),
            origin: UVec2::ZERO,
            target: NormalizedRenderTarget::None {
                width: 1920,
                height: 1080,
            },
        }
    }

    #[test]
    fn stale_completion_cannot_cross_identity_viewport_or_camera_changes() {
        for change in [
            |k: &mut Key| k.identity.sequence += 1,
            |k: &mut Key| k.viewport.x += 1,
            |k: &mut Key| k.origin.x += 1,
            |k: &mut Key| {
                k.target = NormalizedRenderTarget::None {
                    width: 1280,
                    height: 720,
                }
            },
            |k: &mut Key| k.camera = Entity::from_raw_u32(2).unwrap(),
        ] {
            let mut probe = Probe::default();
            probe.select(Some(key()));
            let old = Arc::clone(&probe.completion);
            probe.submitted = Some(12);
            probe.evidence.completed_frame = Some(10);
            probe.evidence.pipelines_failed = 1;
            probe.evidence.observed_frame = 12;
            let mut changed = key();
            change(&mut changed);
            probe.select(Some(changed));
            old.store(true, Ordering::Release);
            assert!(!probe.completion.load(Ordering::Acquire));
            assert!(probe.submitted.is_none());
            assert!(!probe.completed_recently(13));
            assert_eq!(probe.evidence.pipelines_failed, 0);
            assert_eq!(probe.evidence.observed_frame, 0);
        }
    }

    #[test]
    fn missing_expired_or_future_completion_never_counts_as_current() {
        let mut probe = Probe::default();
        assert!(!probe.completed_recently(10));
        probe.evidence.completed_frame = Some(10);
        assert!(probe.completed_recently(11));
        assert!(!probe.completed_recently(9));
        assert!(!probe.completed_recently(75));
        probe.reset_completion();
        assert!(!probe.completed_recently(11));
    }
}
