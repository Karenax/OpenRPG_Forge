//! Exclusive playable host for explicitly admitted environment inputs.
//! Encoded cloud/HDR work and a completed renderer fence are distinct observations.
use super::{
    live, programs,
    scene::{PreparedEnvironment, ResidentEnvironment},
};
use crate::{cloud_controller, render_inventory, source_atmosphere, source_clouds, source_sky};
use bevy::{
    diagnostic::FrameCount,
    prelude::*,
    render::renderer::RenderDevice,
    tasks::{AsyncComputeTaskPool, Task, futures::check_ready},
};
use forge_core::{ContentHash, EntityId};
use std::sync::atomic::Ordering;

pub struct PreparedHost {
    pub solar_clock: Option<forge_project::module_package::ModuleSolarClock>,
    pub input: PreparedEnvironment,
    pub programs: Option<programs::Input>,
    pub identity: ContentHash,
    pub owner: EntityId,
}

/// Consumers stage only after the admitted environment has published its targets.
#[derive(SystemSet, Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct EnvironmentHostSet;

#[derive(Resource, Clone, Debug, Default, PartialEq, Eq, serde::Serialize, serde::Deserialize)]
pub struct Status {
    pub solar_start_minutes: Option<u16>,
    pub solar_hours_bits: Option<u64>,
    pub solar_night_bits: Option<u32>,
    pub solar_ready: bool,
    pub identity: Option<ContentHash>,
    pub ready: bool,
    pub bootstrap_steps: u32,
    pub cloud_steps: u32,
    pub cloud_submissions: u32,
    pub live_submissions: u32,
    pub programs_expected: bool,
    pub programs_prepared: usize,
    pub hdr_publication: Option<u64>,
    pub publication_frame: Option<u32>,
    pub gpu_observed: u32,
    pub gpu_completed: Option<u32>,
    pub gpu_images: [usize; 2],
    pub gpu_materials: [usize; 2],
    pub error: Option<String>,
}

#[derive(Resource)]
struct Pending(Option<PreparedHost>);
#[derive(Resource)]
struct State {
    solar_clock: Option<source_sky::publication::Clock>,
    owner: EntityId,
    identity: ContentHash,
    entity: Entity,
    mesh: Handle<Mesh>,
    materials: [Handle<source_sky::SkyMaterial>; 2],
    programs: Option<programs::Input>,
    task: Option<Task<Result<programs::Prepared, String>>>,
    previous_time: Option<f64>,
    bootstrap_frame: Option<u32>,
    hdr_frame: Option<u32>,
}

/// Identify the sky owned by this admitted environment, without admitting an
/// arbitrary entity that happens to carry the same material type.
pub fn owns_sky(world: &World, entity: Entity) -> bool {
    world
        .get_resource::<State>()
        .is_some_and(|state| state.entity == entity)
}

/// A profile change requires a completed sky resource observation at its own
/// application frame, in addition to the environment's initial publication.
pub fn covers_frame(world: &World, frame: u32) -> bool {
    world
        .get_resource::<Status>()
        .is_some_and(|status| status.ready)
        && world.get_resource::<State>().is_some_and(|state| {
            render_inventory::acquire_scoped(world, state.owner, state.identity, 1)
                .is_some_and(|token| token.covers_frame(world, frame))
        })
}

impl PreparedHost {
    pub fn install(self, app: &mut App) -> Result<(), String> {
        if let Some(clock) = self.solar_clock {
            clock.validate().map_err(|e| e.to_string())?;
            if self.programs.is_none() {
                return Err("Solar clock requires the live environment graph".into());
            }
        }
        if self.owner.as_uuid().is_nil()
            || app.world().contains_resource::<Pending>()
            || app.world().contains_resource::<State>()
            || app.world().contains_resource::<ResidentEnvironment>()
        {
            return Err("Environment requires its exclusive real scene owner".into());
        }
        source_atmosphere::register(app)?;
        source_clouds::register(app)?;
        live::register(app)?;
        if !app.is_plugin_added::<MaterialPlugin<source_sky::SkyMaterial>>() {
            app.add_plugins(MaterialPlugin::<source_sky::SkyMaterial>::default());
        }
        if !app.is_plugin_added::<render_inventory::RenderInventoryPlugin>() {
            app.add_plugins(render_inventory::RenderInventoryPlugin);
        }
        app.insert_resource(Status {
            identity: Some(self.identity),
            programs_expected: self.programs.is_some(),
            ..default()
        })
        .insert_resource(Pending(Some(self)))
        .add_systems(PostStartup, start.in_set(EnvironmentHostSet))
        .add_systems(
            PostUpdate,
            update
                .in_set(EnvironmentHostSet)
                .after(bevy::transform::TransformSystems::Propagate),
        );
        Ok(())
    }
}

fn start(world: &mut World) {
    let Some(prepared) = world.resource_mut::<Pending>().0.take() else {
        return;
    };
    let result = stage(world, prepared);
    if let Err(error) = result {
        fail(world, error);
    }
}

fn stage(world: &mut World, prepared: PreparedHost) -> Result<(), String> {
    let bootstrap_steps = u32::try_from(
        prepared
            .input
            .clouds
            .shadow
            .as_ref()
            .ok_or("Environment cloud schedule absent")?
            .frames
            .len(),
    )
    .map_err(|_| "Environment bootstrap frame count overflow")?;
    let resident = prepared.input.stage(world)?;
    let solar_clock = prepare_solar(prepared.solar_clock, &resident)?;
    world.resource_mut::<Status>().solar_start_minutes =
        prepared.solar_clock.map(|c| c.start_minutes);
    let sun = resident.atmosphere.params[3].truncate();
    let moon = Vec3::new(-sun.x, sun.y.abs() * 0.8 + 0.25, -sun.z).normalize();
    let base = source_sky::SkyMaterial {
        atmosphere: resident.atmosphere.params,
        settings: source_sky::Settings {
            moon_disk: moon.extend(1.0),
            stars_time_night: Vec4::new(0.0, 2.0, 0.0, 0.0),
        },
        transmittance: resident.atmosphere.images[0].clone(),
        sky: resident.atmosphere.images[2].clone(),
        clouds: resident.cloud_view,
        panorama: Some(resident.clouds.images[9].clone()),
        cloud_view: None,
    };
    // Both real temporal views are declared to the stable resource inventory.
    // Switching the drawn material cannot turn a new binding into an old GPU proof.
    let materials = std::array::from_fn(|index| {
        let mut material = base.clone();
        material.cloud_view = Some(resident.clouds.images[5 + index * 2].clone());
        world
            .resource_mut::<Assets<source_sky::SkyMaterial>>()
            .add(material)
    });
    let mesh = world
        .resource_mut::<Assets<Mesh>>()
        .add(Rectangle::new(2.0, 2.0));
    let initial_view = resident.cloud_continuation.last.view_index;
    if initial_view > 1 {
        return Err("Invalid environment bootstrap cloud view".into());
    }
    let entity = world
        .spawn((
            Mesh3d(mesh.clone()),
            MeshMaterial3d(materials[initial_view].clone()),
            Transform::default(),
            bevy::camera::visibility::NoFrustumCulling,
            bevy::light::NotShadowCaster,
        ))
        .id();
    render_inventory::request_scoped(
        world,
        prepared.owner,
        prepared.identity,
        1,
        std::slice::from_ref(&mesh),
        &materials,
    )?;
    world.resource_mut::<Status>().bootstrap_steps = bootstrap_steps;
    world.insert_resource(resident);
    world.insert_resource(State {
        solar_clock,
        owner: prepared.owner,
        identity: prepared.identity,
        entity,
        mesh,
        materials,
        programs: prepared.programs,
        task: None,
        previous_time: None,
        bootstrap_frame: None,
        hdr_frame: None,
    });
    Ok(())
}

fn update(world: &mut World) {
    if !world.contains_resource::<State>() || world.resource::<Status>().error.is_some() {
        return;
    }
    let result = world.resource_scope(|world, mut state: Mut<State>| advance(world, &mut state));
    if let Err(error) = result {
        fail(world, error);
    }
}

fn advance(world: &mut World, state: &mut State) -> Result<(), String> {
    if world
        .get::<bevy::camera::visibility::NoFrustumCulling>(state.entity)
        .is_none()
        || world
            .get::<Visibility>(state.entity)
            .is_none_or(|v| *v == Visibility::Hidden)
        || world.get::<ChildOf>(state.entity).is_some()
        || world
            .get::<Mesh3d>(state.entity)
            .is_none_or(|mesh| mesh.0 != state.mesh)
        || world.get::<Transform>(state.entity) != Some(&Transform::IDENTITY)
        || world
            .get::<MeshMaterial3d<source_sky::SkyMaterial>>(state.entity)
            .is_none_or(|material| !state.materials.contains(&material.0))
    {
        return Err("Owned environment sky projection changed".into());
    }
    prepare_programs(world, state)?;
    if let Some(error) = live::failure(world) {
        return Err(error);
    }
    let cloud_ready = observe(world, state);
    if !cloud_ready
        || !source_clouds::live::can_submit(world)
        || (world.resource::<Status>().programs_expected && !live::can_submit(world))
    {
        return Ok(());
    }
    let camera = camera(world)?;
    let now = world.resource::<Time>().elapsed_secs_f64();
    let delta = state
        .previous_time
        .map_or(1.0 / 60.0, |previous| (now - previous).clamp(0.0, 0.1));
    let mut input = world
        .resource::<ResidentEnvironment>()
        .cloud_continuation
        .input;
    world
        .resource::<ResidentEnvironment>()
        .cloud_continuation
        .apply_scene_wind(world, &mut input);
    input.camera = camera;
    input.dt = delta;
    let solar = advance_solar(world, state, delta, camera.position[1])?;
    if state.solar_clock.is_some() && solar.is_none() {
        world.resource_mut::<Status>().ready = false;
        return Ok(());
    }
    if let Some(frame) = solar {
        input.light_direction = frame.key.to_array();
    }
    let mut controller = world
        .resource::<ResidentEnvironment>()
        .cloud_continuation
        .controller
        .clone();
    let output = controller.update(input).map_err(str::to_owned)?;
    let mut params = world.resource::<source_atmosphere::Targets>().params;
    if solar.is_none() {
        params[1].w = source_atmosphere::observer::radius(camera.position[1])?;
        let values = std::array::from_fn(|i| params[i / 4][i % 4]);
        source_atmosphere::update_parameters(world, values, false, false)?;
    }
    let step = if let Some(frame) = solar {
        source_clouds::live::submit_solar(world, &output, frame)?
    } else {
        source_clouds::live::submit_observer(world, &output)?
    };
    publish_frame(
        world,
        state,
        &output,
        params,
        delta,
        step,
        solar.map_or(input.light_direction, |frame| frame.sun.to_array()),
    )?;
    let mut resident = world.resource_mut::<ResidentEnvironment>();
    resident.atmosphere.params = params;
    resident.cloud_view = output.public.map(Vec4::from_array);
    resident.cloud_continuation.controller = controller;
    resident.cloud_continuation.input = input;
    resident.cloud_continuation.last = output;
    state.previous_time = Some(now);
    let count = world
        .resource::<Status>()
        .live_submissions
        .checked_add(1)
        .ok_or("Environment submission count exhausted")?;
    world.resource_mut::<Status>().live_submissions = count;
    Ok(())
}

fn publish_frame(
    world: &mut World,
    state: &State,
    output: &cloud_controller::Output,
    params: [Vec4; 4],
    delta: f64,
    step: u32,
    true_sun: [f64; 3],
) -> Result<(), String> {
    let time = output.frame[10][1];
    for material in &state.materials {
        let mut assets = world.resource_mut::<Assets<source_sky::SkyMaterial>>();
        let mut sky = assets
            .get_mut(material)
            .ok_or("Owned sky material disappeared")?;
        sky.atmosphere = params;
        sky.clouds = output.public.map(Vec4::from_array);
        sky.settings.stars_time_night.y = time;
    }
    world
        .entity_mut(state.entity)
        .insert(MeshMaterial3d(state.materials[output.view_index].clone()));
    if live::is_staged(world) {
        let submitted = source_clouds::live::submitted_frame(world)?;
        let sky = world
            .resource::<Assets<source_sky::SkyMaterial>>()
            .get(&state.materials[output.view_index])
            .ok_or("Owned sky material disappeared")?
            .settings
            .clone();
        live::submit(
            world,
            live::FrameInput {
                dt: delta,
                sun: true_sun,
                force: false,
                global: submitted.global,
                atmosphere: params,
                clouds: submitted.clouds,
                sky: [sky.moon_disk, sky.stars_time_night],
                cloud_step: step,
                cloud_view: submitted.view,
            },
        )?;
    }
    Ok(())
}

fn prepare_solar(
    settings: Option<forge_project::module_package::ModuleSolarClock>,
    environment: &ResidentEnvironment,
) -> Result<Option<source_sky::publication::Clock>, String> {
    settings
        .map(|settings| {
            settings.validate().map_err(|e| e.to_string())?;
            let initial = source_sky::solar::Settings {
                hours: f64::from(settings.captured_minutes) / 60.0,
                azimuth_degrees: f64::from(settings.azimuth_degrees),
                speed: 0.0,
            };
            let target = source_sky::solar::Settings {
                hours: f64::from(settings.start_minutes) / 60.0,
                speed: settings
                    .day_length_seconds
                    .map_or(0.0, |s| 24.0 / f64::from(s)),
                ..initial
            };
            source_sky::publication::Clock::from_acquired(
                initial,
                target,
                environment.atmosphere.params[3].truncate(),
            )
        })
        .transpose()
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "Validated solar night is in zero to one"
)]
fn advance_solar(
    world: &mut World,
    state: &mut State,
    delta: f64,
    camera_y: f64,
) -> Result<Option<source_sky::solar::Frame>, String> {
    let Some(clock) = &mut state.solar_clock else {
        return Ok(None);
    };
    source_sky::publication::validate_camera_height(camera_y)?;
    let step = clock.advance(delta)?;
    if step.changed {
        source_atmosphere::controller::cancel(world);
    }
    let publication =
        world.resource_scope(|world, mut environment: Mut<ResidentEnvironment>| {
            let publication = source_sky::publication::advance(
                world,
                &mut environment,
                delta,
                camera_y,
                step.frame,
            )?;
            if let Some(publication) = &publication {
                if world.contains_resource::<crate::source_water_optics::ResidentOptics>() {
                    crate::source_water_optics::update_lighting(
                        world,
                        &publication.packed,
                        publication.params,
                    )?;
                }
                publication.publish_direct(&mut environment);
            }
            Ok::<_, String>(publication)
        })?;
    if publication.is_some() {
        clock.acknowledge_readback();
    }
    if let Some(publication) = &publication {
        let scene = world
            .resource::<ResidentEnvironment>()
            .scene_lighting
            .clone();
        source_sky::publication::sync_directional_lights(world, &scene, publication.frame);
    }
    let mut status = world.resource_mut::<Status>();
    status.solar_hours_bits = Some(step.frame.hours.to_bits());
    status.solar_night_bits = Some((step.frame.night as f32).to_bits());
    status.solar_ready = clock.ready() && publication.is_some();
    Ok(publication.map(|p| p.frame))
}

fn observe(world: &mut World, state: &mut State) -> bool {
    let frame = world.resource::<FrameCount>().0;
    let clouds = world.resource::<source_clouds::Targets>();
    let cloud_ready = clouds.ready.load(Ordering::Acquire);
    let steps = clouds.completed_steps.load(Ordering::Acquire);
    let submissions = clouds.submissions.load(Ordering::Acquire);
    if cloud_ready {
        state.bootstrap_frame.get_or_insert(frame);
    }
    let publication = live::publication(world).map(|p| p.frame);
    if publication.is_some() {
        state.hdr_frame.get_or_insert(frame);
    }
    let fence_frame = if world.resource::<Status>().programs_expected {
        state.hdr_frame
    } else {
        state.bootstrap_frame
    };
    let ready = (state.solar_clock.is_none() || world.resource::<Status>().solar_ready)
        && world.resource::<Status>().live_submissions > 0
        && steps > world.resource::<Status>().bootstrap_steps
        && cloud_ready
        && fence_frame.is_some_and(|frame| {
            render_inventory::acquire_scoped(world, state.owner, state.identity, 1)
                .is_some_and(|token| token.covers_frame(world, frame))
        });
    let evidence = render_inventory::scoped_evidence(world, state.owner).cloned();
    let mut status = world.resource_mut::<Status>();
    status.ready = ready;
    status.cloud_steps = steps;
    status.cloud_submissions = submissions;
    status.hdr_publication = publication;
    status.publication_frame = fence_frame;
    if let Some(evidence) = evidence {
        status.gpu_observed = evidence.observed;
        status.gpu_completed = evidence.completed;
        status.gpu_images = [
            evidence.assets.images_expected,
            evidence.assets.images_prepared,
        ];
        status.gpu_materials = [
            evidence.assets.materials_expected,
            evidence.assets.materials_bound,
        ];
    }
    cloud_ready
}

fn prepare_programs(world: &mut World, state: &mut State) -> Result<(), String> {
    if let Some(device) = world.get_resource::<RenderDevice>().cloned()
        && let Some(input) = state.programs.take()
    {
        let pool = AsyncComputeTaskPool::try_get().ok_or("Environment compute pool unavailable")?;
        state.task = Some(pool.spawn(async move { input.prepare(&device).await }));
    }
    let Some(result) = state.task.as_mut().and_then(check_ready) else {
        return Ok(());
    };
    state.task = None;
    let prepared = result?;
    let count = prepared.programs.len();
    let resident = world.resource::<ResidentEnvironment>();
    let (targets, atmosphere, clouds) = (
        resident.environment_targets.clone(),
        resident.atmosphere.clone(),
        resident.clouds.clone(),
    );
    live::stage(world, prepared, targets, atmosphere, clouds)?;
    live::bind_lighting(world, crate::source_lighting::scene_buffer::HANDLE)?;
    world.resource_mut::<Status>().programs_prepared = count;
    Ok(())
}

fn camera(world: &mut World) -> Result<cloud_controller::Camera, String> {
    let mut cameras = world.query_filtered::<(&Camera, &GlobalTransform, &Projection), (
        With<Camera3d>,
        Without<crate::scene_camera::OffscreenSceneCamera>,
    )>();
    let mut active = cameras
        .iter(world)
        .filter(|(camera, _, _)| camera.is_active);
    let Some((_, pose, Projection::Perspective(projection))) = active.next() else {
        return Err("Environment requires one perspective scene camera".into());
    };
    if active.next().is_some() {
        return Err("Environment refuses multiple active scene cameras".into());
    }
    let pose = pose.compute_transform();
    Ok(cloud_controller::Camera {
        position: pose.translation.to_array().map(f64::from),
        right: pose.right().to_array().map(f64::from),
        up: pose.up().to_array().map(f64::from),
        forward: pose.forward().to_array().map(f64::from),
        fov_degrees: f64::from(projection.fov).to_degrees(),
        aspect: f64::from(projection.aspect_ratio),
        zoom: 1.0,
    })
}

fn fail(world: &mut World, error: String) {
    error!(%error, "Common environment host failed");
    let mut status = world.resource_mut::<Status>();
    status.ready = false;
    status.error = Some(error);
    world.write_message(AppExit::error());
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn camera_selection_ignores_offscreen_views_but_refuses_multiple_interactive_views() {
        let mut world = World::new();
        world.spawn((
            Camera3d::default(),
            GlobalTransform::from_translation(Vec3::new(1.0, 2.0, 3.0)),
        ));
        world.spawn((
            Camera3d::default(),
            GlobalTransform::from_translation(Vec3::NAN),
            crate::scene_camera::OffscreenSceneCamera,
        ));
        assert_eq!(
            camera(&mut world).unwrap().position.map(f64::to_bits),
            [1.0_f64, 2.0, 3.0].map(f64::to_bits)
        );
        world.spawn(Camera3d::default());
        assert!(camera(&mut world).is_err());
    }
    #[test]
    fn missing_owned_projection_refuses_before_any_gpu_state_is_considered_ready() {
        let mut world = World::new();
        let entity = world.spawn_empty().id();
        let mut state = State {
            solar_clock: None,
            owner: EntityId::deterministic("environment-test"),
            identity: forge_core::bytes_sha256(b"env"),
            entity,
            mesh: Handle::default(),
            materials: [Handle::default(), Handle::default()],
            programs: None,
            task: None,
            previous_time: None,
            bootstrap_frame: None,
            hdr_frame: None,
        };
        assert!(
            advance(&mut world, &mut state)
                .unwrap_err()
                .contains("projection changed")
        );
        assert!(state.previous_time.is_none());
    }
}
