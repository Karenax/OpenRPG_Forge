//! One-shot reference sky inspection on the existing render device.
#![allow(clippy::needless_pass_by_value)]
use super::{Settings, SkyMaterial};
use crate::tidewater_scene_environment::ResidentEnvironment;
use bevy::{
    camera::{
        RenderTarget,
        visibility::{NoFrustumCulling, RenderLayers},
    },
    diagnostic::FrameCount,
    light::NotShadowCaster,
    prelude::*,
    render::{
        gpu_readback::{Readback, ReadbackComplete},
        render_resource::{TextureFormat, TextureUsages},
    },
};
use std::sync::atomic::Ordering;

pub const WIDTH: u32 = 1920;
pub const HEIGHT: u32 = 1080;
const LAYER: usize = 31;
/// Excluded from editor navigation and inspection-light systems.
#[derive(Component)]
#[require(forge_engine::scene_camera::OffscreenSceneCamera)]
pub struct PreviewCamera;
/// Scene tools ignore the temporary offscreen reference view.
/// The scene tools operate on the visible camera, never an offscreen capture.
pub type SceneCameraFilter = (
    With<Camera3d>,
    Without<PreviewCamera>,
    Without<crate::tidewater_refraction::CaptureCamera>,
);
#[derive(Resource)]
pub struct Snapshot(pub Result<Vec<u8>, String>);
#[derive(Resource)]
struct Active {
    camera: Entity,
    sky: Entity,
    reader: Entity,
    started: u32,
    previous: Vec<u8>,
    stable: u8,
}

pub fn install(app: &mut App) {
    app.add_systems(Update, watchdog);
    if !app.is_plugin_added::<MaterialPlugin<SkyMaterial>>() {
        app.add_plugins(MaterialPlugin::<SkyMaterial>::default());
    }
}

/// Request one fixed-view sky image. No authored camera, terrain or light is changed.
pub fn request(world: &mut World) -> Result<(), String> {
    if world.contains_resource::<Active>() {
        return Err("Sky preview already running".into());
    }
    let resident = world
        .get_resource::<ResidentEnvironment>()
        .ok_or("Environment is not loaded")?;
    if !resident.clouds.ready.load(Ordering::Acquire) {
        return Err("Environment is still computing".into());
    }
    let atmosphere = &resident.atmosphere;
    let sun = atmosphere.params[3].truncate();
    let moon = Vec3::new(-sun.x, sun.y.abs() * 0.8 + 0.25, -sun.z).normalize();
    let material = SkyMaterial {
        atmosphere: atmosphere.params,
        settings: Settings {
            moon_disk: moon.extend(1.0),
            stars_time_night: Vec4::new(0.0, 2.0, 0.0, 0.0),
        },
        transmittance: atmosphere.images[0].clone(),
        sky: atmosphere.images[2].clone(),
        clouds: resident.cloud_view,
        panorama: Some(resident.clouds.images[9].clone()),
        cloud_view: Some(resident.clouds.images[7].clone()),
    };
    if !world.contains_resource::<Assets<SkyMaterial>>() {
        return Err("Sky preview plugin is absent".into());
    }
    let material = world.resource_mut::<Assets<SkyMaterial>>().add(material);
    let mesh = world
        .resource_mut::<Assets<Mesh>>()
        .add(Rectangle::new(2.0, 2.0));
    let mut image = Image::new_target_texture(WIDTH, HEIGHT, TextureFormat::Rgba8UnormSrgb, None);
    image.texture_descriptor.usage |= TextureUsages::COPY_SRC;
    let target = world.resource_mut::<Assets<Image>>().add(image);
    let sky = world
        .spawn((
            Mesh3d(mesh),
            MeshMaterial3d(material),
            Transform::default(),
            NoFrustumCulling,
            NotShadowCaster,
            RenderLayers::layer(LAYER),
        ))
        .id();
    let camera = world
        .spawn((
            PreviewCamera,
            Camera3d::default(),
            Camera {
                order: -20,
                ..default()
            },
            RenderTarget::Image(target.clone().into()),
            Msaa::Off,
            Transform::from_xyz(42.0, 14.0, -90.0).with_rotation(Quat::from_xyzw(
                0.0,
                0.995_004_2,
                0.099_833_414,
                0.0,
            )),
            Projection::Perspective(PerspectiveProjection {
                fov: 62.0_f32.to_radians(),
                near: 0.06,
                far: 60_000.0,
                ..default()
            }),
            RenderLayers::layer(LAYER),
        ))
        .id();
    let reader = world.spawn(Readback::texture(target)).observe(receive).id();
    let started = world.resource::<FrameCount>().0;
    world.remove_resource::<Snapshot>();
    world.insert_resource(Active {
        camera,
        sky,
        reader,
        started,
        previous: Vec::new(),
        stable: 0,
    });
    Ok(())
}

fn receive(
    event: On<ReadbackComplete>,
    state: Option<ResMut<Active>>,
    frame: Res<FrameCount>,
    mut commands: Commands,
) {
    let Some(mut state) = state else {
        return;
    };
    if event.entity != state.reader {
        return;
    }
    let elapsed = frame.0.saturating_sub(state.started);
    if elapsed < 60 {
        return;
    }
    let result = if event.data.len() != (WIDTH * HEIGHT * 4) as usize {
        Some(Err("Sky preview readback size differs".into()))
    } else if elapsed > 300 {
        Some(Err("Sky preview did not stabilize".into()))
    } else {
        let nonuniform = event
            .data
            .as_chunks::<4>()
            .0
            .iter()
            .any(|pixel| pixel != &event.data[..4]);
        state.stable = if nonuniform && state.previous == event.data {
            state.stable + 1
        } else {
            0
        };
        state.previous.clone_from(&event.data);
        (state.stable >= 3).then(|| Ok(std::mem::take(&mut state.previous)))
    };
    if let Some(result) = result {
        commands.entity(state.camera).despawn();
        commands.entity(state.sky).despawn();
        commands.entity(state.reader).despawn();
        commands.remove_resource::<Active>();
        commands.insert_resource(Snapshot(result));
    }
}

fn watchdog(state: Option<Res<Active>>, frame: Res<FrameCount>, mut commands: Commands) {
    let Some(state) = state else {
        return;
    };
    if frame.0.saturating_sub(state.started) > 600 {
        commands.entity(state.camera).despawn();
        commands.entity(state.sky).despawn();
        commands.entity(state.reader).despawn();
        commands.remove_resource::<Active>();
        commands.insert_resource(Snapshot(Err("Sky preview timed out".into())));
    }
}

#[cfg(test)]
mod camera_filter_tests {
    use super::*;

    #[test]
    fn scene_tools_keep_the_visible_camera_when_offscreen_cameras_exist() {
        let mut world = World::new();
        let visible = world.spawn(Camera3d::default()).id();
        world.spawn((Camera3d::default(), PreviewCamera));
        world.spawn((
            Camera3d::default(),
            crate::tidewater_refraction::CaptureCamera,
        ));
        let mut cameras = world.query_filtered::<Entity, SceneCameraFilter>();
        assert_eq!(cameras.single(&world).unwrap(), visible);
    }
}
