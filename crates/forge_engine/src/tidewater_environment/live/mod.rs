//! Submit coherent frames to the existing atmosphere/cloud/environment schedule.
mod compute;
mod lighting;
mod samplers;
use super::{programs::Prepared, resident::Targets};
use crate::{tidewater_atmosphere, tidewater_clouds};
use bevy::{
    core_pipeline::schedule::camera_driver,
    math::DVec3,
    prelude::*,
    render::{
        RenderApp,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        renderer::RenderGraph,
    },
};
use std::sync::{
    Arc, Mutex,
    atomic::{AtomicBool, AtomicU64, Ordering},
};

#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct EnvironmentCompute;
#[derive(Resource)]
struct Registered;
struct Progress {
    active: AtomicBool,
    encoded: AtomicU64,
    published: AtomicU64,
    failure: Mutex<Option<String>>,
}
impl Default for Progress {
    fn default() -> Self {
        Self {
            active: AtomicBool::new(true),
            encoded: AtomicU64::new(0),
            published: AtomicU64::new(0),
            failure: Mutex::new(None),
        }
    }
}
#[derive(Resource, Clone, ExtractResource)]
struct Staged {
    programs: Arc<Prepared>,
    targets: Targets,
    atmosphere: tidewater_atmosphere::Targets,
    clouds: tidewater_clouds::Targets,
    progress: Arc<Progress>,
    lighting: Option<Destination>,
}
#[derive(Clone)]
struct Destination {
    handle: Handle<bevy::render::storage::ShaderBuffer>,
    lease: Arc<()>,
}
#[derive(Resource, Clone, Default, ExtractResource)]
struct Pending(Option<Arc<Frame>>);
struct Frame {
    id: u64,
    input: FrameInput,
}

/// Exactly the uniforms and cloud history used by this scene frame. `sun` is
/// the true atmosphere sun, while the 720-byte global frame contains key light.
#[derive(Clone)]
pub struct FrameInput {
    pub dt: f64,
    pub sun: [f64; 3],
    pub force: bool,
    pub global: [u8; 720],
    pub atmosphere: [Vec4; 4],
    pub clouds: [Vec4; 5],
    pub sky: [Vec4; 2],
    pub cloud_step: u32,
    pub cloud_view: usize,
}

/// Encoded outputs, not a CPU GPU-completion receipt. A scene can adopt the
/// pair together on a later frame; SH readback is a separate operation.
pub struct Publication {
    pub frame: u64,
    pub front: u8,
    pub cube: Handle<Image>,
    pub sh: Handle<bevy::render::storage::ShaderBuffer>,
}

pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Registered>() {
        return Ok(());
    }
    if app.get_sub_app(RenderApp).is_none() {
        return Err("Environment render app absent".into());
    }
    app.add_plugins((
        ExtractResourcePlugin::<Staged>::default(),
        ExtractResourcePlugin::<Pending>::default(),
    ));
    app.sub_app_mut(RenderApp).add_systems(
        RenderGraph,
        compute::compute
            .in_set(EnvironmentCompute)
            .after(tidewater_clouds::CloudCompute)
            .before(camera_driver)
            .run_if(resource_exists::<Staged>)
            .run_if(resource_exists::<Pending>),
    );
    app.insert_resource(Registered);
    Ok(())
}

/// Pipelines must be prepared using this scene's Bevy device. Existing acquired
/// outputs stay displayed; installing a generation does not publish a result.
pub fn stage(
    world: &mut World,
    programs: Prepared,
    targets: Targets,
    atmosphere: tidewater_atmosphere::Targets,
    clouds: tidewater_clouds::Targets,
) -> Result<(), String> {
    if !world.contains_resource::<Registered>()
        || atmosphere.images.len() != 3
        || clouds.images.len() != 10
        || programs.programs.len() != 22
    {
        return Err("Environment renderer or qualified inputs absent".into());
    }
    clear(world);
    world.insert_resource(Staged {
        programs: Arc::new(programs),
        targets,
        atmosphere,
        clouds,
        progress: Arc::new(Progress::default()),
        lighting: None,
    });
    world.insert_resource(Pending::default());
    Ok(())
}

/// Bind a stable 176-byte material lighting buffer. Its SH tail is copied only
/// at an environment swap; its direct-light prefix is updated on every frame.
pub fn bind_lighting(
    world: &mut World,
    handle: Handle<bevy::render::storage::ShaderBuffer>,
) -> Result<(), String> {
    if !can_submit(world) {
        return Err("Environment generation not ready for a lighting destination".into());
    }
    let lease = crate::tidewater_material::scene_buffer::ownership::claim(world, &handle)?;
    let old = world
        .resource_mut::<Staged>()
        .lighting
        .replace(Destination { handle, lease });
    if let Some(old) = old {
        crate::tidewater_material::scene_buffer::ownership::release(world, &old.handle, &old.lease);
    }
    Ok(())
}

/// Release only this generation's ownership before reseeding a different scene.
pub fn clear(world: &mut World) {
    if let Some(staged) = world.remove_resource::<Staged>() {
        staged.progress.active.store(false, Ordering::Release);
        if let Some(destination) = staged.lighting {
            crate::tidewater_material::scene_buffer::ownership::release(
                world,
                &destination.handle,
                &destination.lease,
            );
        }
    }
    world.remove_resource::<Pending>();
}

#[must_use]
pub fn can_submit(world: &World) -> bool {
    let (Some(staged), Some(pending)) = (
        world.get_resource::<Staged>(),
        world.get_resource::<Pending>(),
    ) else {
        return false;
    };
    current(world, staged)
        && staged.progress.failure.lock().is_ok_and(|f| f.is_none())
        && pending
            .0
            .as_ref()
            .is_none_or(|f| staged.progress.encoded.load(Ordering::Acquire) >= f.id)
}

/// Includes pending/failed generations, so upstream cloud frames cannot overtake them.
#[must_use]
pub fn is_staged(world: &World) -> bool {
    world.contains_resource::<Staged>()
}

/// One pending frame only. Its clouds must already be submitted; the render
/// system waits for that exact cloud step before sampling the temporal textures.
pub fn submit(world: &mut World, input: FrameInput) -> Result<u64, String> {
    if !can_submit(world) {
        return Err("Environment frame still pending or failed".into());
    }
    super::controller::Controller::default().plan(input.dt, input.sun, input.force)?;
    lighting::direct(&input.global)?;
    if input.cloud_view > 1
        || input.cloud_step == 0
        || input
            .atmosphere
            .iter()
            .chain(&input.clouds)
            .chain(&input.sky)
            .any(|v| !v.is_finite())
        || input.atmosphere[3]
            .truncate()
            .as_dvec3()
            .distance(DVec3::from_array(input.sun))
            > 0.0001
    {
        return Err("Invalid environment frame uniforms or sun".into());
    }
    let staged = world.resource::<Staged>();
    if input.atmosphere != world.resource::<tidewater_atmosphere::Targets>().params {
        return Err("Environment frame differs from current atmosphere".into());
    }
    let id = staged
        .progress
        .encoded
        .load(Ordering::Acquire)
        .checked_add(1)
        .filter(|id| *id <= u64::MAX / 2)
        .ok_or("Environment frame overflow")?;
    world.insert_resource(Pending(Some(Arc::new(Frame { id, input }))));
    Ok(id)
}

pub fn publication(world: &World) -> Option<Publication> {
    let staged = world.get_resource::<Staged>()?;
    if !current(world, staged) {
        return None;
    }
    let value = staged.progress.published.load(Ordering::Acquire);
    if value == 0 {
        return None;
    }
    let front = (value & 1) as u8;
    Some(Publication {
        frame: value >> 1,
        front,
        cube: staged.targets.displayed.clone(),
        sh: staged.targets.sh[usize::from(front)].clone(),
    })
}

pub fn failure(world: &World) -> Option<String> {
    world
        .get_resource::<Staged>()?
        .progress
        .failure
        .lock()
        .ok()?
        .clone()
}

fn current(world: &World, staged: &Staged) -> bool {
    let (Some(atmosphere), Some(clouds)) = (
        world.get_resource::<tidewater_atmosphere::Targets>(),
        world.get_resource::<tidewater_clouds::Targets>(),
    ) else {
        return false;
    };
    Arc::ptr_eq(&atmosphere.ready, &staged.atmosphere.ready)
        && atmosphere.images == staged.atmosphere.images
        && Arc::ptr_eq(&clouds.ready, &staged.clouds.ready)
        && clouds.images == staged.clouds.images
}
