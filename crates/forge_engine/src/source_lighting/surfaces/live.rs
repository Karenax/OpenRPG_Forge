//! One source-surface lifecycle, with optional vegetation casters and shared visible motion.
use super::scene;
use crate::{source_environment::host::EnvironmentHostSet, source_lighting::frame::Frame};
use bevy::{prelude::*, render::storage::ShaderBuffer};

#[derive(SystemSet, Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct SurfaceHostSet;

pub struct PreparedHost {
    pub scene: scene::Prepared,
    pub wind_speed: f32,
    pub wind_frequency: f32,
}

#[derive(Resource)]
struct Pending(Option<PreparedHost>);

#[derive(Resource)]
struct State {
    scene: scene::Resident,
    frame: Frame,
    handle: Handle<ShaderBuffer>,
    clock: crate::source_wind::vegetation::Clock,
    direction: bevy::math::DVec2,
    speed: f32,
    frequency: f32,
    frame_ready: bool,
}

/// Snapshot from the sole scene producer, consumed by visible vegetation too.
pub struct Snapshot {
    pub frame: Frame,
    /// Source-material time in seconds, independent of vegetation waveform frequency.
    pub material_wind: Vec4,
    pub handle: Handle<ShaderBuffer>,
    pub generation: u64,
    pub frame_ready: bool,
    /// Exact heightfield inputs used for the current hill-shadow generation.
    pub terrain_relief: forge_core::ContentHash,
}

#[must_use]
pub fn installed(world: &World) -> bool {
    world.contains_resource::<Pending>() || world.contains_resource::<State>()
}

#[cfg(feature = "physics-rapier")]
pub(crate) fn validate_plant_replacement(
    world: &World,
    head: forge_core::ContentHash,
    ids: &std::collections::BTreeSet<forge_core::EntityId>,
) -> Result<(), String> {
    world
        .get_resource::<State>()
        .ok_or("Source surface host absent")?
        .scene
        .validate_plant_replacement(head, ids)
}

#[cfg(feature = "physics-rapier")]
pub(crate) fn replace_plant_instances(
    world: &mut World,
    head: forge_core::ContentHash,
    ids: std::collections::BTreeSet<forge_core::EntityId>,
) -> Result<(), String> {
    world
        .get_resource_mut::<State>()
        .ok_or("Source surface host absent")?
        .scene
        .replace_plant_instances(head, ids)
}

#[derive(Resource, Default)]
pub struct Error(pub Option<String>);

impl PreparedHost {
    pub fn install(self, app: &mut App) -> Result<(), String> {
        if app.world().contains_resource::<Pending>() || app.world().contains_resource::<State>() {
            return Err("Source surfaces require their exclusive scene host".into());
        }
        // Reuse the same validation as the published frame, including the frequency bounds.
        let direction = bevy::math::DVec2::new(0.82, 0.57).normalize();
        crate::source_lighting::frame::motion::advance(
            &crate::source_wind::vegetation::Clock::from_acquired(
                0.0,
                0,
                [0.0; 2],
                direction.to_array(),
                f64::from(self.wind_speed),
            )?,
            0.0,
            direction.to_array(),
            f64::from(self.wind_speed),
            f64::from(self.wind_frequency),
        )?;
        super::host::register(app)?;
        crate::source_shadows::casters::graph::register(app)?;
        if !app.is_plugin_added::<crate::source_shadows::heightfield::HillShadowPlugin>() {
            app.add_plugins(crate::source_shadows::heightfield::HillShadowPlugin);
        }
        app.insert_resource(Pending(Some(self)))
            .init_resource::<Error>()
            .add_systems(
                PostStartup,
                start.in_set(SurfaceHostSet).after(EnvironmentHostSet),
            )
            .add_systems(
                PostUpdate,
                update
                    .in_set(SurfaceHostSet)
                    .after(EnvironmentHostSet)
                    .after(bevy::transform::TransformSystems::Propagate),
            );
        Ok(())
    }
}

#[must_use]
pub fn snapshot(world: &World) -> Option<Snapshot> {
    world.get_resource::<State>().map(|state| Snapshot {
        frame: state.frame,
        material_wind: state.clock.pose().material_uniform(),
        handle: state.handle.clone(),
        generation: state.scene.generation,
        frame_ready: state.frame_ready,
        terrain_relief: state.scene.terrain_relief,
    })
}

/// Adopt CPU-prepared caster/height inputs after their authored raster is admitted.
/// Keep the scene's frame handle and motion clock; consumers observe a new generation.
pub fn replace_scene(world: &mut World, scene: scene::Prepared) -> Result<(), String> {
    let (camera, _) = crate::scene_camera::primary(world)?;
    let state = world
        .get_resource::<State>()
        .ok_or("Source surface scene absent")?;
    let generation = state
        .scene
        .generation
        .checked_add(1)
        .ok_or("Surface generation overflow")?;
    let motion = state.frame.shadow_motion();
    let mut next = scene.stage(world, camera, motion)?;
    next.generation = generation;
    let mut state = world.resource_mut::<State>();
    state.scene = next;
    state.frame_ready = false;
    Ok(())
}

/// A GPU-ready observation still requires the consumer's real inventory token.
#[must_use]
pub fn observe(
    world: &World,
    token: Option<&crate::render_inventory::RenderInventoryToken>,
) -> Option<scene::Status> {
    world.get_resource::<State>().map(|state| {
        let mut status = state
            .scene
            .observe(world, token, state.frame.shadow_motion());
        status.ready &= state.frame_ready && world.resource::<Error>().0.is_none();
        status
    })
}

/// Bind a vegetation subscriber to exactly the same resident targets and frame.
#[cfg(feature = "physics-rapier")]
pub(crate) fn bind_plant(
    world: &World,
    material: &mut crate::source_vegetation::world::WorldPlantMaterial,
) -> Result<(), String> {
    let state = world
        .get_resource::<State>()
        .ok_or("Source surface scene absent")?;
    material.frame = state.handle.clone();
    material.bind_source_scene(
        world,
        world.resource::<crate::source_environment::scene::ResidentEnvironment>(),
        &state.scene.hill,
        &state.scene.target,
    )
}

fn start(world: &mut World) {
    let Some(input) = world.resource_mut::<Pending>().0.take() else {
        return;
    };
    let result = (|| {
        let (camera_entity, camera) = crate::scene_camera::primary(world)?;
        super::stage_shaders(world)?;
        let direction = bevy::math::DVec2::new(0.82, 0.57).normalize();
        let (current_direction, current_speed) = scene_wind(world, direction, input.wind_speed);
        let clock = crate::source_wind::vegetation::Clock::from_acquired(
            0.0,
            0,
            [0.0; 2],
            current_direction.to_array(),
            f64::from(current_speed),
        )?;
        let frame = Frame::new(
            camera,
            current_direction
                .as_vec2()
                .extend(current_speed)
                .extend(0.0),
            Vec4::ZERO,
        )?
        .with_clouds(
            world
                .resource::<crate::source_environment::scene::ResidentEnvironment>()
                .cloud_view,
        )?;
        let handle = frame.stage(world)?;
        let scene = input
            .scene
            .stage(world, camera_entity, frame.shadow_motion())?;
        world.insert_resource(State {
            scene,
            frame,
            handle,
            clock,
            direction,
            speed: input.wind_speed,
            frequency: input.wind_frequency,
            frame_ready: false,
        });
        Ok::<_, String>(())
    })();
    if let Err(error) = result {
        fail(world, error);
    }
}

fn update(world: &mut World) {
    if !world.contains_resource::<State>() || world.resource::<Error>().0.is_some() {
        return;
    }
    let result = world.resource_scope(|world, mut state: Mut<State>| -> Result<(), String> {
        let (camera, position) = crate::scene_camera::primary(world)?;
        let motion = state.frame.shadow_motion();
        let ready = state.scene.refresh(world, camera, motion)?;
        super::host::sync(world, &state.handle)?;
        state.frame_ready = false;
        if !ready {
            return Ok(());
        }
        let (direction, speed) = scene_wind(world, state.direction, state.speed);
        let (clock, wind, gust) = crate::source_lighting::frame::motion::advance(
            &state.clock,
            world.resource::<Time<Real>>().delta_secs_f64(),
            direction.to_array(),
            f64::from(speed),
            f64::from(state.frequency),
        )?;
        let handle = state.handle.clone();
        state.frame.update(world, &handle, position, wind, gust)?;
        let frame = state.frame;
        let phase = u32::from(u8::try_from(clock.pose().index % 64).map_err(|e| e.to_string())?);
        state.scene.animate(world, camera, &frame, phase)?;
        state.clock = clock;
        state.frame_ready = true;
        Ok(())
    });
    if let Err(error) = result {
        fail(world, error);
    }
}

fn scene_wind(
    world: &World,
    fallback_direction: bevy::math::DVec2,
    fallback_speed: f32,
) -> (bevy::math::DVec2, f32) {
    world
        .get_resource::<crate::source_wind::SceneWind>()
        .map_or((fallback_direction, fallback_speed), |scene| {
            (
                bevy::math::DVec2::from_array(scene.wind().direction),
                scene.speed_mps(),
            )
        })
}

fn fail(world: &mut World, error: String) {
    error!(%error, "Source surface scene failed");
    world.resource_mut::<Error>().0 = Some(error);
    #[cfg(feature = "physics-rapier")]
    if let Some(mut ready) = world.get_resource_mut::<crate::PlayerSimulationReady>() {
        ready.0 = false;
    }
    world.write_message(bevy::app::AppExit::error());
}

#[cfg(test)]
mod wind_tests {
    use super::*;

    #[test]
    fn explicit_scene_wind_overrides_and_restores_legacy_surface_wind() {
        let mut world = World::new();
        let fallback = bevy::math::DVec2::new(0.82, 0.57).normalize();
        assert_eq!(scene_wind(&world, fallback, 4.0), (fallback, 4.0));
        crate::source_wind::SceneWind::publish(
            &mut world,
            Some(forge_project::zone_authoring::wind::SceneWindSettings {
                schema_version: 1,
                heading_degrees: 90.0,
                speed_mps: 7.0,
            }),
        )
        .unwrap();
        let (direction, speed) = scene_wind(&world, fallback, 4.0);
        assert!((direction.x - 1.0).abs() < 1e-12);
        assert!(direction.y.abs() < 1e-12);
        assert_eq!(speed.to_bits(), 7.0_f32.to_bits());
        crate::source_wind::SceneWind::publish(&mut world, None).unwrap();
        assert_eq!(scene_wind(&world, fallback, 4.0), (fallback, 4.0));
    }
}
