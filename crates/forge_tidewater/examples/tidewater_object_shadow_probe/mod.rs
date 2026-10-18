//! Developmental native depth passes on Bevy's device; village casters first.
#![allow(clippy::needless_pass_by_value)]
#[path = "../tidewater_plant_probe/vegetation_scene/canonical.rs"]
pub(crate) mod canonical;
#[allow(
    dead_code,
    reason = "The shared capture module retains the standalone isolated LOD runner which only its dedicated probe invokes"
)]
pub mod isolated_lod;
#[allow(
    dead_code,
    reason = "The shared module is driven by tidewater_plant_probe; the static object-shadow example does not install its live source resource"
)]
pub mod live;
#[path = "plant_shader.rs"]
mod plant_shader;
#[path = "plants.rs"]
mod plants;
#[path = "terrain.rs"]
mod terrain;
use bevy::{
    app::{AppExit, ScheduleRunnerPlugin},
    diagnostic::FrameCount,
    math::{DMat4, DQuat, DVec3},
    prelude::*,
    render::gpu_readback::{Readback, ReadbackComplete},
    window::ExitCondition,
    winit::WinitPlugin,
};
use forge_tidewater::sun_cascades::{CascadeConfig, CascadeView, SunCascades};
use serde_json::Value;
use std::{
    error::Error,
    fs,
    io::Write,
    path::{Path, PathBuf},
    sync::atomic::Ordering,
    time::Duration,
};

pub use forge_tidewater::tidewater_material::ObjectShadowSettings as Settings;
pub use forge_tidewater::tidewater_object_shadow::graph::Target;
use forge_tidewater::tidewater_object_shadow::{OpaqueDraw as Draw, graph::Input};
#[derive(Resource)]
struct Output {
    path: PathBuf,
    requested: bool,
    completed_frame: Option<u32>,
}
fn checked_file(root: &Path, manifest: &Value, name: &str) -> Result<Vec<u8>, Box<dyn Error>> {
    if Path::new(name).file_name().and_then(|n| n.to_str()) != Some(name) {
        return Err("invalid fixture path".into());
    }
    let bytes = fs::read(root.join(name))?;
    if manifest["files"][name] != forge_core::bytes_sha256(&bytes).to_string() {
        return Err("fixture hash mismatch".into());
    }
    Ok(bytes)
}

fn village_draws(
    root: &Path,
    manifest: &Value,
    scene_files: Option<&std::collections::BTreeMap<String, Vec<u8>>>,
) -> Result<Vec<Draw>, Box<dyn Error>> {
    let read = |name: &str| -> Result<Vec<u8>, Box<dyn Error>> {
        match scene_files {
            Some(files) => files
                .get(name)
                .cloned()
                .ok_or_else(|| "Missing shared village payload".into()),
            None => checked_file(root, manifest, name),
        }
    };
    let bytes = match scene_files {
        Some(files) => files
            .get("village.json")
            .ok_or("Missing shared village")?
            .clone(),
        None => checked_file(root, manifest, "village.json")?,
    };
    let village: Value = serde_json::from_slice(&bytes)?;
    if village["schema"] != "forge.tidewater.village-probe"
        || !matches!(village["version"].as_u64(), Some(1 | 2))
    {
        return Err("invalid village".into());
    }
    let mut draws = Vec::new();
    let mut caster_names = std::collections::BTreeSet::new();
    for node in village["nodes"].as_array().ok_or("missing nodes")? {
        let name = node["name"].as_str().ok_or("missing name")?;
        // The source wood callback draws all five opaque batches in depth passes.
        if !matches!(
            name,
            "village_wood"
                | "village_hard"
                | "village_roofMetal"
                | "village_thatch"
                | "village_stone"
                | "village_sign_wood"
                | "village_sign_hard"
        ) {
            continue;
        }
        let geometry =
            &village["geometries"][node["geometry"].as_str().ok_or("missing geometry")?];
        caster_names.insert(name);
        let vertices = read(
            geometry["attributes"]["position"]
                .as_str()
                .ok_or("missing positions")?,
        )?;
        let indices = read(geometry["indices"].as_str().ok_or("missing indices")?)?;
        let model = Mat4::from_cols_array(&serde_json::from_value::<[f32; 16]>(
            node["matrix_world"].clone(),
        )?);
        draws.push(Draw::new(vertices, indices, model)?);
    }
    // Canonical version 2 may remove categories through deletion or asset replacement.
    // The immutable version 1 reference still requires all seven source batches.
    if draws.len() > 2048
        || (village["version"] == 1 && (caster_names.len() != 7 || draws.len() != 7))
    {
        return Err("missing opaque village caster batches".into());
    }
    Ok(draws)
}

#[allow(clippy::too_many_lines)] // Development fixture validation followed by headless Bevy setup.
#[allow(dead_code)]
pub fn run() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if !(2..=3).contains(&args.len())
        || (args.len() == 3 && !matches!(args[2].as_str(), "--terrain" | "--plants"))
    {
        return Err("FIXTURE NEW_DEPTH_ARRAY_BIN [--terrain|--plants] required".into());
    }
    let root = PathBuf::from(&args[0]);
    let path = PathBuf::from(&args[1]);
    if path.exists() {
        return Err("output exists".into());
    }
    let input = load(
        &root,
        args.len() == 3,
        args.get(2).is_some_and(|s| s == "--plants"),
        None,
    )?;
    let mut app = App::new();
    app.add_plugins(
        DefaultPlugins
            .set(WindowPlugin {
                primary_window: None,
                exit_condition: ExitCondition::DontExit,
                ..default()
            })
            .disable::<WinitPlugin>(),
    )
    .add_plugins(ScheduleRunnerPlugin::run_loop(Duration::from_millis(16)))
    .insert_resource(Output {
        path,
        requested: false,
        completed_frame: None,
    })
    .add_systems(Update, readback);
    install_input(&mut app, input);
    if app.run() != AppExit::Success {
        return Err("native depth probe failed".into());
    }
    Ok(())
}

#[allow(clippy::too_many_lines)]
fn load(
    root: &Path,
    with_terrain: bool,
    with_plants: bool,
    scene_files: Option<&std::collections::BTreeMap<String, Vec<u8>>>,
) -> Result<Input, Box<dyn Error>> {
    let fixture: Value = serde_json::from_slice(&fs::read(root.join("fixture.json"))?)?;
    if fixture["schema"] != "forge.tidewater.plant-probe"
        || fixture["version"] != 2
        || fixture["source"]["commit"] != "1438b1abfcaee3267092b75573014f4d9b4a983c"
    {
        return Err("source pin differs".into());
    }
    let draws = village_draws(root, &fixture, scene_files)?;
    let camera = &fixture["camera_pose"];
    let position = Vec3::from_array(serde_json::from_value::<[f32; 3]>(
        camera["position"].clone(),
    )?)
    .as_dvec3();
    let rotation = serde_json::from_value::<[f32; 4]>(camera["rotation"].clone())?.map(f64::from);
    let view = CascadeView {
        world_from_view: DMat4::from_rotation_translation(DQuat::from_array(rotation), position),
        near: 0.06,
        fov_degrees: camera["fov_degrees"].as_f64().ok_or("missing camera fov")?,
        aspect: 1920.0 / 1080.0,
    };
    let shadow: Value = serde_json::from_slice(&checked_file(root, &fixture, "shadow.json")?)?;
    let sun = DVec3::from_array(serde_json::from_value::<[f64; 3]>(
        shadow["sun_direction"].clone(),
    )?);
    let mut cascades = SunCascades::new(CascadeConfig::default())?;
    cascades.update(view, sun, true)?;
    let mut settings = Settings {
        enabled: 1.0,
        camera: position.as_vec3().extend(0.0),
        forward: (-view.world_from_view.z_axis).as_vec4(),
        ..default()
    };
    for (i, cascade) in cascades.cascades().iter().enumerate() {
        let c = cascade.expect("initialized");
        settings.matrices[i] = c.clip_from_world.as_mat4();
        settings.cascades[i] = bevy::math::DVec4::from_array(c.info).as_vec4();
        settings.blend[i] = bevy::math::DVec4::from_array(c.blend).as_vec4();
    }
    let eyes = cascades
        .cascades()
        .iter()
        .map(|v| v.expect("initialized").world_from_view.w_axis.as_vec4())
        .collect();
    let plants = if with_plants {
        Some(plants::load(
            root,
            &fixture,
            position.as_vec3().extend(0.0),
            scene_files,
        )?)
    } else {
        None
    };
    let terrain = if with_terrain || with_plants {
        Some(terrain::load(root, &fixture, view, scene_files)?)
    } else {
        None
    };
    let matrices = cascades
        .cascades()
        .iter()
        .map(|v| v.expect("initialized").clip_from_world.as_mat4())
        .collect();
    Ok(Input {
        settings,
        draws,
        matrices,
        terrain,
        plants,
        eyes,
    })
}

/// Attach the same native depth graph to an existing Bevy application.
#[allow(dead_code)]
pub fn install(
    app: &mut App,
    root: &Path,
    scene_files: &std::collections::BTreeMap<String, Vec<u8>>,
) -> Result<Target, Box<dyn Error>> {
    Ok(install_input(
        app,
        load(root, true, true, Some(scene_files))?,
    ))
}
#[allow(dead_code)]
pub fn install_disabled(app: &mut App) {
    install_input(
        app,
        Input {
            settings: Settings::default(),
            draws: Vec::new(),
            matrices: Vec::new(),
            terrain: None,
            plants: None,
            eyes: Vec::new(),
        },
    );
}

/// Additive source casters supplied by an independently verified native layer.
#[derive(Resource)]
pub struct AdditionalOpaqueDraws(pub Vec<Draw>);

fn install_input(app: &mut App, mut input: Input) -> Target {
    if let (Some(frame), Some(plants)) = (
        app.world()
            .get_resource::<forge_tidewater::scene_wind::AcquiredFrame>(),
        input.plants.as_mut(),
    ) {
        plants.initial_motion = Some(
            forge_tidewater::tidewater_object_shadow::graph::animation::Motion {
                main_camera: plants.camera,
                wind_time: frame
                    .wind_frame()
                    .expect("validated acquired wind")
                    .material_uniform(),
                gust_frame: bevy::math::DVec4::new(
                    frame.vegetation_gust_offset[0],
                    frame.vegetation_gust_offset[1],
                    f64::from(frame.global_frame % 64),
                    0.0,
                )
                .as_vec4(),
            },
        );
    }
    if let Some(extra) = app.world_mut().remove_resource::<AdditionalOpaqueDraws>()
        && input.settings.enabled > 0.5
    {
        input.draws.extend(extra.0);
    }
    forge_tidewater::tidewater_object_shadow::graph::install(app, input)
        .expect("validated scene inputs and initialized rendering")
}

fn readback(
    mut commands: Commands,
    frame: Res<FrameCount>,
    target: Res<Target>,
    mut output: ResMut<Output>,
    mut exit: MessageWriter<AppExit>,
) {
    if output
        .completed_frame
        .is_some_and(|done| frame.0 >= done + 8)
    {
        exit.write(AppExit::Success);
        return;
    }
    if frame.0 > 600 {
        exit.write(AppExit::error());
        return;
    }
    if !target.ready.load(Ordering::Acquire) || output.requested {
        return;
    }
    output.requested = true;
    commands
        .spawn(Readback::texture(target.image.clone()))
        .observe(
            |event: On<ReadbackComplete>,
             mut output: ResMut<Output>,
             frame: Res<FrameCount>,
             mut commands: Commands,
             mut exit: MessageWriter<AppExit>| {
                commands.entity(event.entity).despawn();
                let result = (|| -> std::io::Result<()> {
                    if event.data.len() != 2048 * 2048 * 3 * 4 {
                        return Err(std::io::Error::other("invalid depth readback length"));
                    }
                    fs::OpenOptions::new()
                        .write(true)
                        .create_new(true)
                        .open(&output.path)?
                        .write_all(&event.data)
                })();
                match result {
                    Ok(()) => {
                        println!(
                            "native depth array: {} bytes, sha256 {}",
                            event.data.len(),
                            forge_core::bytes_sha256(&event.data)
                        );
                        output.completed_frame = Some(frame.0);
                    }
                    Err(e) => {
                        eprintln!("{e}");
                        exit.write(AppExit::error());
                    }
                }
            },
        );
}
