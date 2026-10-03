//! Capture jitter for Bevy TAA, sampled after its actual Render preparation.
//! `SourceResolve` owns a different earlier path; this helper never touches it.
use super::{CaptureCamera, CaptureSource};
use bevy::{
    anti_alias::taa::TemporalAntiAliasing,
    prelude::*,
    render::{
        Render, RenderApp, RenderSystems,
        camera::{ExtractedCamera, TemporalJitter},
        extract_component::{ExtractComponent, ExtractComponentPlugin},
        sync_world::MainEntity,
        view::ExtractedView,
    },
};
use std::{
    collections::HashMap,
    sync::{Arc, Mutex},
};

/// Request identity used to reject observations from a previous mode/camera lifetime.
/// This is telemetry; it never controls Bevy's temporal phase or history.
#[derive(Component, Clone, Copy, Debug, ExtractComponent)]
pub struct Request(pub u64);

#[derive(Clone, Debug)]
pub struct CaptureSync {
    pub source: Entity,
    pub capture: Option<Entity>,
    pub source_size: UVec2,
    pub capture_size: UVec2,
    pub jitter: Vec2,
    pub capture_jitter: Option<Vec2>,
    pub request: Option<u64>,
    pub error: Option<String>,
}

/// Current Render observations only, never a GPU fence or a sequence owner.
#[derive(Resource, Clone, Default)]
pub struct Observation(Arc<Mutex<Vec<CaptureSync>>>);
impl Observation {
    #[must_use]
    pub fn snapshot(&self, source: Entity) -> Option<CaptureSync> {
        self.0
            .lock()
            .ok()?
            .iter()
            .find(|value| value.source == source)
            .cloned()
    }
}

pub(super) fn install(app: &mut App) {
    let observation = Observation::default();
    app.insert_resource(observation.clone())
        .add_plugins(ExtractComponentPlugin::<Request>::default());
    app.sub_app_mut(RenderApp)
        .insert_resource(observation)
        .add_systems(
            Render,
            synchronize
                .after(RenderSystems::PrepareViews)
                .before(RenderSystems::PrepareResources),
        );
}

fn scaled_jitter(source: UVec2, capture: UVec2, jitter: Vec2) -> Result<Vec2, String> {
    if source.min_element() == 0 || capture.min_element() == 0 || !jitter.is_finite() {
        return Err("TAA refraction viewport/jitter unavailable".into());
    }
    Ok(jitter * capture.as_vec2() / source.as_vec2())
}

#[allow(
    clippy::type_complexity,
    clippy::needless_pass_by_value,
    reason = "Bevy supplies the typed view queries and resource by value as system parameters."
)]
fn synchronize(
    sources: Query<
        (
            &MainEntity,
            &ExtractedCamera,
            &ExtractedView,
            &TemporalJitter,
            Option<&Request>,
        ),
        (With<TemporalAntiAliasing>, Without<CaptureCamera>),
    >,
    mut captures: Query<
        (
            Entity,
            &CaptureSource,
            &ExtractedCamera,
            &ExtractedView,
            Option<&mut TemporalJitter>,
        ),
        With<CaptureCamera>,
    >,
    observation: Res<Observation>,
) {
    let mut unique_sources = HashMap::new();
    for (entity, camera, view, jitter, request) in &sources {
        let candidate = (camera, view, jitter.offset, request.map(|value| value.0));
        unique_sources
            .entry(entity.id())
            .and_modify(|entry: &mut (_, usize)| entry.1 += 1)
            .or_insert((candidate, 1));
    }
    let mut reports = Vec::new();
    let mut updates = Vec::new();
    for (&source, &((camera, view, jitter, request), count)) in &unique_sources {
        let source_size = camera.physical_viewport_size.unwrap_or(UVec2::ZERO);
        if count != 1 {
            reports.push(CaptureSync {
                source,
                capture: None,
                source_size,
                capture_size: UVec2::ZERO,
                jitter,
                capture_jitter: None,
                request,
                error: Some("TAA refraction source view is not unique".into()),
            });
            continue;
        }
        let matches: Vec<_> = captures
            .iter()
            .filter(|(_, association, ..)| association.view == source)
            .map(|(entity, _, capture, extracted, marker)| {
                let size = capture.physical_viewport_size.unwrap_or(UVec2::ZERO);
                let result = scaled_jitter(source_size, size, jitter).and_then(|offset| {
                    if extracted.clip_from_view != view.clip_from_view
                        || extracted.world_from_view != view.world_from_view
                    {
                        return Err("TAA refraction source pose/projection mismatch".into());
                    }
                    // The marker must already have selected TEMPORAL_JITTER in
                    // PrepareAssets. Inserting it here would be too late.
                    if marker.is_none() {
                        return Err(
                            "TAA refraction jitter marker absent before specialization".into()
                        );
                    }
                    Ok(offset)
                });
                (entity, size, result)
            })
            .collect();
        let mut report = CaptureSync {
            source,
            capture: None,
            source_size,
            capture_size: UVec2::ZERO,
            jitter,
            capture_jitter: None,
            request,
            error: Some("TAA refraction capture not ready or not unique".into()),
        };
        if let [(entity, size, result)] = matches.as_slice() {
            report.capture = Some(*entity);
            report.capture_size = *size;
            report.error = result.as_ref().err().cloned();
            if let Ok(offset) = result {
                report.capture_jitter = Some(*offset);
                updates.push((*entity, *offset));
            }
        }
        reports.push(report);
    }
    // Collect and validate associations before applying valid updates. Never overwrite
    // capture jitter on a source that has no actual Bevy TAA component.
    for (entity, offset) in updates {
        if let Ok((_, _, _, _, Some(mut jitter))) = captures.get_mut(entity) {
            jitter.offset = offset;
        }
    }
    if let Ok(mut status) = observation.0.lock() {
        *status = reports;
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{
        ecs::{schedule::ScheduleLabel, system::RunSystemOnce},
        render::{render_resource::TextureFormat, view::RetainedViewEntity},
    };

    fn extracted_camera(size: UVec2) -> ExtractedCamera {
        ExtractedCamera {
            target: None,
            physical_viewport_size: Some(size),
            physical_target_size: Some(size),
            viewport: None,
            schedule: Render.intern(),
            order: 0,
            output_mode: bevy::camera::CameraOutputMode::default(),
            msaa_writeback: bevy::camera::MsaaWriteback::default(),
            clear_color: bevy::camera::ClearColorConfig::default(),
            sorted_camera_index_for_target: 0,
            exposure: 0.0,
            hdr: false,
            compositing_space: None,
        }
    }

    fn extracted_view(
        main_entity: Entity,
        size: UVec2,
        clip_from_view: Mat4,
        world_from_view: GlobalTransform,
    ) -> ExtractedView {
        ExtractedView {
            retained_view_entity: RetainedViewEntity::new(main_entity.into(), None, 0),
            clip_from_view,
            world_from_view,
            clip_from_world: None,
            target_format: TextureFormat::Rgba16Float,
            viewport: UVec4::new(0, 0, size.x, size.y),
            color_grading: default(),
            invert_culling: false,
        }
    }

    fn spawn_source(
        world: &mut World,
        size: UVec2,
        clip_from_view: Mat4,
        world_from_view: GlobalTransform,
        jitter: Vec2,
        request: Option<u64>,
        with_taa: bool,
    ) -> Entity {
        let source = world.spawn_empty().id();
        let main_entity: MainEntity = source.into();
        world.entity_mut(source).insert((
            main_entity,
            extracted_camera(size),
            extracted_view(source, size, clip_from_view, world_from_view),
            TemporalJitter { offset: jitter },
        ));
        if with_taa {
            world
                .entity_mut(source)
                .insert(TemporalAntiAliasing::default());
        }
        if let Some(request) = request {
            world.entity_mut(source).insert(Request(request));
        }
        source
    }

    fn spawn_capture(
        world: &mut World,
        source: Entity,
        size: UVec2,
        clip_from_view: Mat4,
        world_from_view: GlobalTransform,
        jitter: Option<Vec2>,
    ) -> Entity {
        let capture = world.spawn_empty().id();
        world.entity_mut(capture).insert((
            CaptureCamera,
            CaptureSource { view: source },
            extracted_camera(size),
            extracted_view(capture, size, clip_from_view, world_from_view),
        ));
        if let Some(jitter) = jitter {
            world
                .entity_mut(capture)
                .insert(TemporalJitter { offset: jitter });
        }
        capture
    }

    fn run_synchronize(world: &mut World) {
        world
            .run_system_once(synchronize)
            .expect("TAA mirror system should run on a render-world fixture");
    }

    #[test]
    fn odd_capture_uses_actual_viewport_ratio_and_rejects_unavailable_inputs() {
        let offset = Vec2::new(0.25, -0.125);
        let actual = scaled_jitter(UVec2::new(1919, 1079), UVec2::new(960, 540), offset).unwrap();
        assert_eq!(
            actual,
            offset * Vec2::new(960.0, 540.0) / Vec2::new(1919.0, 1079.0)
        );
        assert_ne!(actual, offset * 0.5);
        assert!(scaled_jitter(UVec2::ZERO, UVec2::ONE, offset).is_err());
        assert!(scaled_jitter(UVec2::ONE, UVec2::ONE, Vec2::NAN).is_err());
    }

    #[test]
    fn ecs_sync_mirrors_actual_taa_jitter_and_request_at_odd_viewport_ratio() {
        let source_size = UVec2::new(1919, 1079);
        let capture_size = UVec2::new(960, 540);
        let source_jitter = Vec2::new(0.25, -0.125);
        let expected = scaled_jitter(source_size, capture_size, source_jitter).unwrap();
        let pose = GlobalTransform::from_xyz(1.0, 2.0, 3.0);
        let clip = Mat4::from_rotation_z(0.2);
        let mut world = World::new();
        let observation = Observation::default();
        world.insert_resource(observation.clone());
        let source = spawn_source(
            &mut world,
            source_size,
            clip,
            pose,
            source_jitter,
            Some(276),
            true,
        );
        let capture = spawn_capture(
            &mut world,
            source,
            capture_size,
            clip,
            pose,
            Some(Vec2::splat(9.0)),
        );

        run_synchronize(&mut world);

        assert_eq!(
            world.get::<TemporalJitter>(capture).unwrap().offset,
            expected
        );
        let report = observation.snapshot(source).expect("source observation");
        assert_eq!(report.capture, Some(capture));
        assert_eq!(report.source_size, source_size);
        assert_eq!(report.capture_size, capture_size);
        assert_eq!(report.jitter, source_jitter);
        assert_eq!(report.capture_jitter, Some(expected));
        assert_eq!(report.request, Some(276));
        assert_eq!(report.error, None);
    }

    #[test]
    fn ecs_sync_refuses_capture_for_different_camera_association() {
        let size = UVec2::new(1920, 1080);
        let pose = GlobalTransform::IDENTITY;
        let clip = Mat4::IDENTITY;
        let mut world = World::new();
        let observation = Observation::default();
        world.insert_resource(observation.clone());
        let source = spawn_source(
            &mut world,
            size,
            clip,
            pose,
            Vec2::new(0.2, -0.3),
            Some(1),
            true,
        );
        let other_camera = world.spawn_empty().id();
        let capture = spawn_capture(
            &mut world,
            other_camera,
            UVec2::new(960, 540),
            clip,
            pose,
            Some(Vec2::new(0.7, 0.8)),
        );
        let original = world.get::<TemporalJitter>(capture).unwrap().offset;

        run_synchronize(&mut world);

        assert_eq!(
            world.get::<TemporalJitter>(capture).unwrap().offset,
            original
        );
        let report = observation
            .snapshot(source)
            .expect("source refusal observation");
        assert_eq!(report.capture, None);
        assert_eq!(report.capture_size, UVec2::ZERO);
        assert_eq!(report.capture_jitter, None);
        assert_eq!(
            report.error.as_deref(),
            Some("TAA refraction capture not ready or not unique")
        );
    }

    #[test]
    fn ecs_sync_refuses_duplicate_captures_without_touching_either_jitter() {
        let size = UVec2::new(1920, 1080);
        let pose = GlobalTransform::IDENTITY;
        let clip = Mat4::IDENTITY;
        let mut world = World::new();
        let observation = Observation::default();
        world.insert_resource(observation.clone());
        let source = spawn_source(
            &mut world,
            size,
            clip,
            pose,
            Vec2::new(0.2, -0.3),
            Some(2),
            true,
        );
        let first = spawn_capture(
            &mut world,
            source,
            UVec2::new(960, 540),
            clip,
            pose,
            Some(Vec2::new(0.7, 0.8)),
        );
        let second = spawn_capture(
            &mut world,
            source,
            UVec2::new(960, 540),
            clip,
            pose,
            Some(Vec2::new(-0.4, 0.6)),
        );
        let first_original = world.get::<TemporalJitter>(first).unwrap().offset;
        let second_original = world.get::<TemporalJitter>(second).unwrap().offset;

        run_synchronize(&mut world);

        assert_eq!(
            world.get::<TemporalJitter>(first).unwrap().offset,
            first_original
        );
        assert_eq!(
            world.get::<TemporalJitter>(second).unwrap().offset,
            second_original
        );
        let report = observation
            .snapshot(source)
            .expect("duplicate refusal observation");
        assert_eq!(report.capture, None);
        assert_eq!(report.capture_jitter, None);
        assert_eq!(
            report.error.as_deref(),
            Some("TAA refraction capture not ready or not unique")
        );
    }

    #[test]
    fn ecs_sync_refuses_pose_mismatch_without_touching_capture_jitter() {
        let size = UVec2::new(1920, 1080);
        let clip = Mat4::IDENTITY;
        let source_pose = GlobalTransform::from_xyz(1.0, 2.0, 3.0);
        let capture_pose = GlobalTransform::from_xyz(1.0, 2.0, 4.0);
        let mut world = World::new();
        let observation = Observation::default();
        world.insert_resource(observation.clone());
        let source = spawn_source(
            &mut world,
            size,
            clip,
            source_pose,
            Vec2::new(0.2, -0.3),
            Some(3),
            true,
        );
        let capture = spawn_capture(
            &mut world,
            source,
            UVec2::new(960, 540),
            clip,
            capture_pose,
            Some(Vec2::new(0.7, 0.8)),
        );
        let original = world.get::<TemporalJitter>(capture).unwrap().offset;

        run_synchronize(&mut world);

        assert_eq!(
            world.get::<TemporalJitter>(capture).unwrap().offset,
            original
        );
        let report = observation
            .snapshot(source)
            .expect("pose refusal observation");
        assert_eq!(report.capture, Some(capture));
        assert_eq!(report.capture_jitter, None);
        assert_eq!(
            report.error.as_deref(),
            Some("TAA refraction source pose/projection mismatch")
        );
    }

    #[test]
    fn ecs_sync_refuses_capture_without_prepared_jitter_marker() {
        let size = UVec2::new(1920, 1080);
        let pose = GlobalTransform::IDENTITY;
        let clip = Mat4::IDENTITY;
        let mut world = World::new();
        let observation = Observation::default();
        world.insert_resource(observation.clone());
        let source = spawn_source(
            &mut world,
            size,
            clip,
            pose,
            Vec2::new(0.2, -0.3),
            Some(4),
            true,
        );
        let capture = spawn_capture(&mut world, source, UVec2::new(960, 540), clip, pose, None);

        run_synchronize(&mut world);

        assert!(world.get::<TemporalJitter>(capture).is_none());
        let report = observation
            .snapshot(source)
            .expect("marker refusal observation");
        assert_eq!(report.capture, Some(capture));
        assert_eq!(report.capture_jitter, None);
        assert_eq!(
            report.error.as_deref(),
            Some("TAA refraction jitter marker absent before specialization")
        );
    }

    #[test]
    fn source_resolve_without_actual_taa_stays_untouched_and_clears_stale_observation() {
        let size = UVec2::new(1920, 1080);
        let pose = GlobalTransform::IDENTITY;
        let clip = Mat4::IDENTITY;
        let mut world = World::new();
        let observation = Observation::default();
        world.insert_resource(observation.clone());
        let source = spawn_source(
            &mut world,
            size,
            clip,
            pose,
            Vec2::new(0.2, -0.3),
            Some(5),
            true,
        );
        let capture = spawn_capture(
            &mut world,
            source,
            UVec2::new(960, 540),
            clip,
            pose,
            Some(Vec2::new(0.7, 0.8)),
        );

        run_synchronize(&mut world);
        assert!(observation.snapshot(source).is_some());

        world.entity_mut(source).remove::<TemporalAntiAliasing>();
        let untouched = Vec2::new(-0.9, 0.95);
        world
            .entity_mut(capture)
            .insert(TemporalJitter { offset: untouched });

        run_synchronize(&mut world);

        assert!(observation.snapshot(source).is_none());
        assert_eq!(
            world.get::<TemporalJitter>(capture).unwrap().offset,
            untouched
        );
    }

    #[test]
    fn ecs_sync_refuses_duplicate_render_views_for_same_main_entity_but_keeps_independent_source() {
        let size = UVec2::new(1920, 1080);
        let capture_size = UVec2::new(960, 540);
        let pose = GlobalTransform::IDENTITY;
        let clip = Mat4::IDENTITY;
        let duplicate_jitter = Vec2::new(0.2, -0.3);
        let independent_jitter = Vec2::new(-0.15, 0.25);
        let mut world = World::new();
        let observation = Observation::default();
        world.insert_resource(observation.clone());

        let duplicate_source = spawn_source(
            &mut world,
            size,
            clip,
            pose,
            duplicate_jitter,
            Some(276),
            true,
        );
        let duplicate_view = world.spawn_empty().id();
        let duplicate_main_entity: MainEntity = duplicate_source.into();
        world.entity_mut(duplicate_view).insert((
            duplicate_main_entity,
            extracted_camera(size),
            extracted_view(duplicate_source, size, clip, pose),
            TemporalJitter {
                offset: duplicate_jitter,
            },
        ));
        world
            .entity_mut(duplicate_view)
            .insert(TemporalAntiAliasing::default());
        world.entity_mut(duplicate_view).insert(Request(276));
        let duplicate_capture = spawn_capture(
            &mut world,
            duplicate_source,
            capture_size,
            clip,
            pose,
            Some(Vec2::new(0.7, 0.8)),
        );
        let duplicate_original = world
            .get::<TemporalJitter>(duplicate_capture)
            .unwrap()
            .offset;

        let independent_source = spawn_source(
            &mut world,
            size,
            clip,
            pose,
            independent_jitter,
            Some(276),
            true,
        );
        let independent_capture = spawn_capture(
            &mut world,
            independent_source,
            capture_size,
            clip,
            pose,
            Some(Vec2::splat(9.0)),
        );
        let independent_expected = scaled_jitter(size, capture_size, independent_jitter).unwrap();

        run_synchronize(&mut world);

        assert_eq!(
            world
                .get::<TemporalJitter>(duplicate_capture)
                .unwrap()
                .offset,
            duplicate_original
        );
        let duplicate_report = observation
            .snapshot(duplicate_source)
            .expect("duplicate source refusal observation");
        assert_eq!(duplicate_report.capture, None);
        assert_eq!(duplicate_report.capture_jitter, None);
        assert_eq!(duplicate_report.request, Some(276));
        assert_eq!(
            duplicate_report.error.as_deref(),
            Some("TAA refraction source view is not unique")
        );

        assert_eq!(
            world
                .get::<TemporalJitter>(independent_capture)
                .unwrap()
                .offset,
            independent_expected
        );
        let independent_report = observation
            .snapshot(independent_source)
            .expect("independent source observation");
        assert_eq!(independent_report.capture, Some(independent_capture));
        assert_eq!(
            independent_report.capture_jitter,
            Some(independent_expected)
        );
        assert_eq!(independent_report.request, Some(276));
        assert!(independent_report.error.is_none());
    }
}
