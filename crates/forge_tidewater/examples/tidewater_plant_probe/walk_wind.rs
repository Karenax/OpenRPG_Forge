//! A common source wind pose for visible vegetation and its resident depth casters.
use bevy::prelude::*;
use std::sync::atomic::Ordering;
use {
    forge_engine::{ForgePlayerCamera, PlayerSimulationReady},
    forge_tidewater::tidewater_object_shadow::{
        graph::{self, animation},
        wind,
    },
};

#[derive(Resource, Default)]
struct Live {
    clock: wind::Clock,
    previous_time: Option<f64>,
    held: Option<animation::Motion>,
}

/// Used only by the bounded smoke test to separate motion from temporal dithering.
#[derive(Resource, Default)]
pub(super) struct HeldPose(pub Option<animation::Motion>);

/// Freezes visible wind and cascade history for a fixed-pose qualification capture.
#[derive(Resource, Default)]
pub(super) struct QualificationHold(pub bool);

/// A bounded GPU test steps shadow views individually while holding the wind.
#[derive(Resource, Default, Clone, Copy)]
pub(super) struct CascadeControl {
    pub view: Option<forge_tidewater::sun_cascades::CascadeView>,
    pub motion: Option<animation::Motion>,
    pub advance: bool,
}

#[derive(Resource, Default, serde::Serialize)]
pub(super) struct Receipt {
    pub submitted: u32,
    pub sequence: u32,
    pub wind: [f32; 4],
    pub gust_frame: [f32; 4],
    pub camera: [f32; 3],
    pub materials: usize,
    pub shadow_update: u64,
    pub shadow_layers: Vec<usize>,
    pub shadow_matrices: [[f32; 16]; 3],
    pub shadow_eyes: [[f32; 4]; 3],
    pub terrain_nodes: u32,
    pub plant_counts: Vec<u32>,
}

pub(super) fn install(app: &mut App) {
    app.init_resource::<Live>()
        .init_resource::<HeldPose>()
        .init_resource::<QualificationHold>()
        .init_resource::<CascadeControl>()
        .init_resource::<Receipt>()
        .add_systems(
            PostUpdate,
            update.after(bevy::transform::TransformSystems::Propagate),
        );
}

#[allow(clippy::too_many_lines)] // One exclusive commit keeps visible wind, streams and cascade metadata together.
pub(super) fn update(world: &mut World) {
    if world.resource::<QualificationHold>().0 {
        return;
    }
    if !world.resource::<PlayerSimulationReady>().0
        || !world
            .resource::<graph::Target>()
            .ready
            .load(Ordering::Acquire)
    {
        return;
    }
    if !world.resource::<animation::State>().can_submit() {
        return;
    }
    let control = *world.resource::<CascadeControl>();
    if control.view.is_some() && !control.advance {
        return;
    }
    let mut cameras =
        world.query_filtered::<(&GlobalTransform, &Projection), With<ForgePlayerCamera>>();
    let Ok((camera, Projection::Perspective(projection))) = cameras.single(world) else {
        return;
    };
    let position = camera.translation();
    let view = match control.view.map_or_else(
        || {
            forge_tidewater::sun_cascades::CascadeView::from_camera_matrix(
                camera.to_matrix(),
                projection.near,
                projection.fov,
                projection.aspect_ratio,
            )
        },
        Ok,
    ) {
        Ok(view) => view,
        Err(error) => {
            error!(%error, matrix = ?camera.to_matrix(), near = projection.near,
                fov = projection.fov, aspect = projection.aspect_ratio, "invalid player shadow camera");
            world.write_message(bevy::app::AppExit::error());
            return;
        }
    };
    let controlled_motion = control.motion;
    let now = world.resource::<Time>().elapsed_secs_f64();
    let mut live = world.resource_mut::<Live>();
    let dt = live.previous_time.map_or(0.0, |previous| now - previous);
    let pose = match live.clock.advance(dt, [1.0, 0.0], 6.0) {
        Ok(pose) => pose,
        Err(error) => {
            error!(%error, "live wind clock failed");
            world.write_message(bevy::app::AppExit::error());
            return;
        }
    };
    live.previous_time = Some(now);
    // These shaders use only the low six frame bits (Bayer/PCSS). Keeping those
    // bits avoids losing the pattern to f32 rounding after long play sessions.
    let frame = u8::try_from(pose.index % 64).expect("six frame bits");
    let held = world.resource::<HeldPose>().0;
    if controlled_motion.is_none() && held.is_some() && held == world.resource::<Live>().held {
        return;
    }
    let mut motion = controlled_motion.or(held).unwrap_or(animation::Motion {
        main_camera: position.extend(0.0),
        wind_time: Vec4::new(pose.dir[0], pose.dir[1], pose.speed, pose.time),
        gust_frame: Vec4::new(pose.offset[0], pose.offset[1], f32::from(frame), 0.0),
    });
    if controlled_motion.is_some() {
        motion.main_camera = view.world_from_view.w_axis.as_vec4().truncate().extend(0.0);
    }
    #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
    // Validated low six frame bits, also held by the GPU smoke.
    let frame_index = motion.gust_frame.z as u32;
    let started = super::walk::perf::start(world);
    let frame = match world
        .resource_mut::<crate::object_shadow::live::Sources>()
        .frame(
            view,
            frame_index,
            controlled_motion.is_none() && held.is_some(),
        ) {
        Ok(frame) => frame,
        Err(error) => {
            error!(%error, "live cascade selection failed");
            world.write_message(bevy::app::AppExit::error());
            return;
        }
    };
    super::walk::perf::end(world, started, "shadow_selection");
    world.resource_mut::<CascadeControl>().advance = false;
    let metadata = (
        frame.update_index,
        frame.layers.clone(),
        std::array::from_fn(|i| frame.settings.matrices[i].to_cols_array()),
        frame.eyes.map(|v| v.to_array()),
        frame.terrain.as_ref().map_or(0, |v| v.count),
        frame.plants.iter().map(|p| p.count).collect(),
    );
    let sequence = match animation::submit_frame(world, motion, frame) {
        Ok(sequence) => sequence,
        Err(error) => {
            error!(%error, "live vegetation shadow update failed");
            world.write_message(bevy::app::AppExit::error());
            return;
        }
    };
    world.resource_mut::<Live>().held = held;
    if let Err(error) = forge_tidewater::tidewater_plants::motion::update(
        world,
        motion.wind_time,
        motion.gust_frame,
    ) {
        error!(%error, "live visible wind update failed");
        world.write_message(bevy::app::AppExit::error());
        return;
    }
    let count = world.resource::<Assets<super::PlantMaterial>>().len();
    let mut receipt = world.resource_mut::<Receipt>();
    receipt.submitted += 1;
    receipt.sequence = sequence;
    receipt.wind = motion.wind_time.to_array();
    receipt.gust_frame = motion.gust_frame.to_array();
    receipt.camera = motion.main_camera.truncate().to_array();
    receipt.materials = count;
    receipt.shadow_update = metadata.0;
    receipt.shadow_layers = metadata.1;
    receipt.shadow_matrices = metadata.2;
    receipt.shadow_eyes = metadata.3;
    receipt.terrain_nodes = metadata.4;
    receipt.plant_counts = metadata.5;
}
