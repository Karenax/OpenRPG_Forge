//! Contact metadata for a real render camera without source temporal ownership.
//! This never inserts a `ViewPacket`, advances Halton or claims a temporal resolve.
use super::contact_frame::CameraFrame;
use crate::tidewater_ocean_depth::DepthCopy;
use bevy::{
    prelude::*,
    render::{
        Render, RenderApp, RenderSystems,
        camera::{ExtractedCamera, TemporalJitter},
        sync_world::MainEntity,
        view::ExtractedView,
    },
};
use std::sync::{Arc, Mutex};

#[derive(Component)]
pub(crate) struct ContactView(pub CameraFrame);

/// Separate provenance from a source-resolve depth receipt, including on mode cuts.
#[derive(Resource, Clone, Default)]
pub(crate) struct History(pub Arc<Mutex<Option<DepthCopy>>>);

#[derive(Resource, Default)]
struct Clock {
    serial: u64,
    epoch: u64,
}

#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub(crate) struct Prepare;

pub(crate) fn register(app: &mut App) {
    app.sub_app_mut(RenderApp)
        .init_resource::<History>()
        .init_resource::<Clock>()
        .add_systems(
            Render,
            prepare
                .in_set(Prepare)
                .after(RenderSystems::PrepareAssets)
                .before(RenderSystems::Queue),
        );
}

#[allow(clippy::type_complexity, clippy::needless_pass_by_value)]
#[allow(
    clippy::float_cmp,
    reason = "Depth reconstruction requires exact perspective projection row constants and rejects other conventions."
)]
fn prepare(
    mut commands: Commands,
    mut clock: ResMut<Clock>,
    history: Res<History>,
    old: Query<Entity, With<ContactView>>,
    views: Query<
        (
            Entity,
            &MainEntity,
            &ExtractedView,
            &ExtractedCamera,
            Option<&TemporalJitter>,
            Option<&crate::tidewater_temporal::frame::ViewPacket>,
        ),
        (
            With<Camera3d>,
            Without<crate::tidewater_refraction::CaptureCamera>,
        ),
    >,
) {
    for entity in &old {
        commands.entity(entity).remove::<ContactView>();
    }
    let Some(serial) = clock.serial.checked_add(1) else {
        return;
    };
    clock.serial = serial;
    let mut candidates = views.iter();
    let Some((entity, main, view, camera, jitter, source)) = candidates.next() else {
        return;
    };
    // A shared scene depth target can only describe one main camera.
    if candidates.next().is_some() || source.is_some() {
        return;
    }
    let Some(size) = camera.physical_target_size else {
        return;
    };
    if camera.physical_viewport_size != Some(size)
        || view.viewport != UVec4::new(0, 0, size.x, size.y)
        || size.min_element() == 0
    {
        return;
    }
    let projection = view.clip_from_view;
    // Contacts reconstruct perspective reverse-Z depth. Reject orthographic/custom
    // projections rather than treating them as the source camera convention.
    if projection.z_axis.w != -1.0 || projection.w_axis.w != 0.0 {
        return;
    }
    let near = projection.w_axis.z / (1.0 + projection.z_axis.z);
    let world = view.world_from_view.to_matrix();
    let inverse = world.inverse();
    let unjittered = projection * inverse;
    // Bevy's unjittered uniform is projection * view even when raster has an
    // override. A divergent override would not match source depth reconstruction.
    if view
        .clip_from_world
        .is_some_and(|override_matrix| override_matrix != unjittered)
    {
        return;
    }
    let raster = if let Some(jitter) = jitter {
        let mut raster = projection;
        jitter.jitter_projection(&mut raster, size.as_vec2());
        raster * inverse
    } else {
        unjittered
    };
    if !near.is_finite()
        || near <= 0.0
        || !world.is_finite()
        || !inverse.is_finite()
        || !unjittered.is_finite()
        || !raster.is_finite()
    {
        return;
    }
    let previous = history.0.lock().ok().and_then(|previous| previous.clone());
    let matching = previous.as_ref().filter(|previous| {
        previous.tag.view == main.id()
            && previous.tag.serial.checked_add(1) == Some(serial)
            && previous.size == size
            && previous.history_epoch == clock.epoch
    });
    if matching.is_none() {
        let Some(epoch) = clock.epoch.checked_add(1) else {
            return;
        };
        clock.epoch = epoch;
    }
    commands.entity(entity).insert(ContactView(CameraFrame {
        tag: crate::tidewater_temporal::frame::FrameTag {
            view: main.id(),
            serial,
        },
        input_size: size,
        history_epoch: clock.epoch,
        needs_history_seed: matching.is_none(),
        view_from_world: inverse,
        clip_from_world: raster,
        unjittered_clip_from_world: unjittered,
        previous_unjittered_clip_from_world: matching
            .map_or(unjittered, |previous| previous.unjittered_clip_from_world),
        camera_position: world.w_axis.truncate(),
        near,
    }));
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{
        ecs::schedule::ScheduleLabel,
        render::{render_resource::TextureFormat, view::RetainedViewEntity},
    };

    fn fixture(world: &mut World, main: Entity) -> Entity {
        world
            .spawn((
                Camera3d::default(),
                MainEntity::from(main),
                ExtractedView {
                    retained_view_entity: RetainedViewEntity {
                        main_entity: main.into(),
                        auxiliary_entity: Entity::PLACEHOLDER.into(),
                        subview_index: 0,
                    },
                    clip_from_view: Mat4::perspective_infinite_reverse_rh(1.0, 1.0, 0.06),
                    world_from_view: GlobalTransform::IDENTITY,
                    clip_from_world: None,
                    target_format: TextureFormat::Rgba16Float,
                    viewport: UVec4::new(0, 0, 64, 64),
                    color_grading: default(),
                    invert_culling: false,
                },
                ExtractedCamera {
                    target: None,
                    physical_viewport_size: Some(UVec2::splat(64)),
                    physical_target_size: Some(UVec2::splat(64)),
                    viewport: None,
                    schedule: bevy::core_pipeline::schedule::Core3d.intern(),
                    order: 0,
                    output_mode: default(),
                    msaa_writeback: bevy::camera::MsaaWriteback::Off,
                    clear_color: default(),
                    sorted_camera_index_for_target: 0,
                    exposure: 1.0,
                    hdr: true,
                    compositing_space: None,
                },
            ))
            .id()
    }

    fn encode_receipt(world: &World, entity: Entity) {
        let view = &world.get::<ContactView>(entity).unwrap().0;
        *world.resource::<History>().0.lock().unwrap() = Some(DepthCopy {
            image: Handle::default(),
            tag: view.tag,
            size: view.input_size,
            history_epoch: view.history_epoch,
            unjittered_clip_from_world: view.unjittered_clip_from_world,
        });
    }

    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Exact copied fixture values and GPU wire fields must remain identical; an epsilon would weaken this regression."
    )]
    fn render_metadata_seeds_on_copy_gap_and_rejects_ambiguous_or_capture_views() {
        let mut world = World::new();
        world.init_resource::<Clock>();
        world.init_resource::<History>();
        let main = Entity::from_bits(7);
        let entity = fixture(&mut world, main);
        let mut schedule = Schedule::new(Render);
        schedule.add_systems(prepare);
        schedule.run(&mut world);
        let first = world.get::<ContactView>(entity).unwrap().0.clone();
        assert!(first.needs_history_seed);
        assert_eq!(first.near, 0.06);
        encode_receipt(&world, entity);
        schedule.run(&mut world);
        let second = world.get::<ContactView>(entity).unwrap().0.clone();
        assert!(!second.needs_history_seed);
        assert_eq!(second.tag.serial, first.tag.serial + 1);
        // Skip the second frame's depth encoding: an older receipt is not usable.
        schedule.run(&mut world);
        let resumed = world.get::<ContactView>(entity).unwrap().0.clone();
        assert!(resumed.needs_history_seed);
        assert!(resumed.history_epoch > second.history_epoch);
        encode_receipt(&world, entity);
        let other = fixture(&mut world, Entity::from_bits(8));
        schedule.run(&mut world);
        assert!(world.get::<ContactView>(entity).is_none());
        assert!(world.get::<ContactView>(other).is_none());
        world.despawn(other);
        schedule.run(&mut world);
        assert!(
            world
                .get::<ContactView>(entity)
                .unwrap()
                .0
                .needs_history_seed
        );
        world
            .entity_mut(entity)
            .insert(crate::tidewater_refraction::CaptureCamera);
        schedule.run(&mut world);
        assert!(world.get::<ContactView>(entity).is_none());
        assert!(
            !world
                .entity(entity)
                .contains::<crate::tidewater_temporal::frame::ViewPacket>()
        );
    }
}
