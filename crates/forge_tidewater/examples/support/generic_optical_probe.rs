//! Independent procedural scenes on the actual shared Bevy post-processing graph.
#[cfg(not(target_arch = "wasm32"))]
use bevy::render::pipelined_rendering::PipelinedRenderingPlugin;
use bevy::{
    app::PluginsState,
    camera::{RenderTarget, visibility::RenderLayers},
    core_pipeline::{prepass::DepthPrepass, tonemapping::DebandDither},
    platform::time::Instant,
    prelude::*,
    render::{
        RenderApp, RenderPlugin,
        render_resource::{CachedPipelineState, PipelineCache, TextureFormat, TextureUsages},
        settings::{Backends, RenderCreation, WgpuSettings},
        view::screenshot::{Screenshot, ScreenshotCaptured},
    },
    window::ExitCondition,
    winit::WinitPlugin,
};
use std::{
    collections::BTreeMap,
    sync::{Arc, Mutex},
    time::Duration,
};
use {
    forge_engine::lens_flare_sun::SceneSun,
    forge_tidewater::{
        ambient_occlusion::{AmbientOcclusionPlugin, AmbientOcclusionSettings},
        film_grain::{FilmGrain, FilmGrainPlugin},
        lens_flare::{LensFlare, LensFlarePlugin},
    },
};
type Error = Box<dyn std::error::Error>;
type Captures = Arc<Mutex<BTreeMap<String, Vec<u8>>>>;
const WIDTH: u32 = 256;
const HEIGHT: u32 = 160;
struct View {
    name: &'static str,
    camera: Entity,
    target: Handle<Image>,
}

fn build() -> App {
    let mut app = App::new();
    let settings = WgpuSettings {
        backends: Some(if cfg!(target_arch = "wasm32") {
            Backends::BROWSER_WEBGPU
        } else {
            Backends::DX12 | Backends::VULKAN
        }),
        ..default()
    };
    let plugins = DefaultPlugins
        .set(WindowPlugin {
            primary_window: None,
            exit_condition: ExitCondition::DontExit,
            ..default()
        })
        .set(RenderPlugin {
            render_creation: RenderCreation::Automatic(Box::new(settings)),
            ..default()
        })
        .disable::<WinitPlugin>();
    #[cfg(not(target_arch = "wasm32"))]
    let plugins = plugins.disable::<PipelinedRenderingPlugin>();
    app.add_plugins(plugins);
    app.add_plugins((FilmGrainPlugin, AmbientOcclusionPlugin, LensFlarePlugin));
    app.insert_resource(GlobalAmbientLight {
        brightness: 120.0,
        ..default()
    });
    app.insert_resource(
        SceneSun::new(
            Vec3::new(0.15, 0.25, -1.0).normalize(),
            Vec3::new(8.0, 6.0, 4.0),
        )
        .expect("constant unit sun"),
    );
    app
}

fn scene(app: &mut App, layer: usize, name: &'static str, color: Color) -> View {
    let offset = Vec3::X * if layer == 0 { 0.0 } else { 20.0 };
    let world = app.world_mut();
    let mut target = Image::new_target_texture(WIDTH, HEIGHT, TextureFormat::Rgba8UnormSrgb, None);
    target.texture_descriptor.usage |= TextureUsages::COPY_SRC;
    let target = world.resource_mut::<Assets<Image>>().add(target);
    let camera = world
        .spawn((
            Camera3d::default(),
            Camera {
                clear_color: ClearColorConfig::Custom(color),
                ..default()
            },
            RenderTarget::Image(target.clone().into()),
            RenderLayers::layer(layer),
            Msaa::Off,
            DepthPrepass,
            DebandDither::Disabled,
            Transform::from_translation(offset + Vec3::new(0.0, 2.0, 8.0))
                .looking_at(offset + Vec3::Y, Vec3::Y),
            FilmGrain::new(0.0),
            AmbientOcclusionSettings::new(0.0),
            LensFlare::new(0.0),
        ))
        .id();
    for (size, position, tint) in [
        (
            Vec3::new(10.0, 0.2, 10.0),
            Vec3::new(0.0, -0.2, 0.0),
            Color::srgb(0.28, 0.3, 0.32),
        ),
        (
            Vec3::new(1.6, 2.4, 1.2),
            Vec3::new(-0.8, 1.1, 0.0),
            Color::srgb(0.2, 0.4, 0.6),
        ),
        (
            Vec3::new(1.0, 0.8, 2.0),
            Vec3::new(1.0, 0.3, 0.4),
            Color::srgb(0.6, 0.3, 0.15),
        ),
    ] {
        let geometry: Mesh = if layer == 0 || position.y < 0.0 {
            Cuboid::from_size(size).into()
        } else {
            Sphere::new(size.min_element() * 0.7).mesh().uv(24, 16)
        };
        let mesh = world.resource_mut::<Assets<Mesh>>().add(geometry);
        let material = world
            .resource_mut::<Assets<StandardMaterial>>()
            .add(StandardMaterial {
                base_color: tint,
                perceptual_roughness: 0.8,
                ..default()
            });
        world.spawn((
            Mesh3d(mesh),
            MeshMaterial3d(material),
            Transform::from_translation(offset + position),
            RenderLayers::layer(layer),
        ));
    }
    world.spawn((
        DirectionalLight {
            illuminance: 1500.0,
            ..default()
        },
        Transform::from_rotation(Quat::from_euler(EulerRot::XYZ, -0.8, -0.4, 0.0)),
        RenderLayers::layer(layer),
    ));
    View {
        name,
        camera,
        target,
    }
}

fn pipelines(app: &App) -> Result<bool, Error> {
    let cache = app
        .get_sub_app(RenderApp)
        .ok_or("Render app absent")?
        .world()
        .resource::<PipelineCache>();
    let mut total = 0;
    let mut pending = false;
    for pipeline in cache.pipelines() {
        total += 1;
        match &pipeline.state {
            CachedPipelineState::Ok(_) => {}
            CachedPipelineState::Err(
                bevy::shader::ShaderCacheError::ShaderNotLoaded(_)
                | bevy::shader::ShaderCacheError::ShaderImportNotYetAvailable,
            ) => pending = true,
            CachedPipelineState::Err(error) => {
                return Err(format!("GPU pipeline refused: {error}").into());
            }
            _ => pending = true,
        }
    }
    Ok(total > 0 && !pending)
}

async fn settle(app: &mut App) -> Result<(), Error> {
    let deadline = Instant::now() + Duration::from_secs(45);
    let mut stable = 0;
    while stable < 20 {
        advance(app)?;
        if pipelines(app)? {
            stable += 1;
        } else {
            stable = 0;
        }
        if Instant::now() > deadline {
            return Err("GPU pipeline warmup timed out".into());
        }
        next_frame().await?;
    }
    Ok(())
}

async fn capture(
    app: &mut App,
    views: &[View],
    phase: &str,
    results: &Captures,
    output: Option<&str>,
) -> Result<(), Error> {
    let before = results.lock().map_err(|e| e.to_string())?.len();
    for view in views {
        let key = format!("{}-{phase}", view.name);
        let results = results.clone();
        let mut entity = app
            .world_mut()
            .spawn(Screenshot::image(view.target.clone()));
        #[cfg(not(target_arch = "wasm32"))]
        if let Some(output) = output {
            entity.observe(bevy::render::view::screenshot::save_to_disk(
                std::path::Path::new(output).join(format!("{key}.png")),
            ));
        }
        #[cfg(target_arch = "wasm32")]
        let _ = output;
        entity.observe(move |event: On<ScreenshotCaptured>| {
            if event.image.width() != WIDTH || event.image.height() != HEIGHT {
                return;
            }
            if let Some(bytes) = &event.image.data
                && let Ok(mut results) = results.lock()
            {
                results.insert(key.clone(), bytes.clone());
            }
        });
    }
    let deadline = Instant::now() + Duration::from_secs(30);
    while results.lock().map_err(|e| e.to_string())?.len() < before + views.len() {
        advance(app)?;
        next_frame().await?;
        if Instant::now() > deadline {
            return Err("GPU screenshot readback timed out".into());
        }
    }
    Ok(())
}

pub async fn run(output: Option<&str>) -> Result<serde_json::Value, Error> {
    let mut app = build();
    let deadline = Instant::now() + Duration::from_secs(30);
    while app.plugins_state() != PluginsState::Ready {
        if Instant::now() > deadline {
            return Err("GPU initialization timed out".into());
        }
        next_frame().await?;
    }
    app.finish();
    app.cleanup();
    advance(&mut app)?;
    let views = [
        scene(
            &mut app,
            0,
            "slate-courtyard",
            Color::srgb(0.04, 0.06, 0.09),
        ),
        scene(
            &mut app,
            1,
            "amber-courtyard",
            Color::srgb(0.12, 0.07, 0.03),
        ),
    ];
    let captures: Captures = Arc::default();
    let mut observations = Vec::new();
    for phase in ["baseline", "control", "grain", "flare", "ao"] {
        for view in &views {
            let world = app.world_mut();
            world
                .get_mut::<FilmGrain>(view.camera)
                .ok_or("Grain absent")?
                .intensity
                .x = if phase == "grain" { 0.12 } else { 0.0 };
            world
                .get_mut::<LensFlare>(view.camera)
                .ok_or("Flare absent")?
                .set_strength(if phase == "flare" { 1.5 } else { 0.0 });
            world
                .get_mut::<AmbientOcclusionSettings>(view.camera)
                .ok_or("AO absent")?
                .set_strength(if phase == "ao" { 2.0 } else { 0.0 });
        }
        settle(&mut app).await?;
        for view in &views {
            observations.push(serde_json::json!({
                "scene": view.name,
                "phase": phase,
                "flare_cpu_inputs": format!("{:?}", app.world().get::<LensFlare>(view.camera)),
            }));
        }
        capture(&mut app, &views, phase, &captures, output).await?;
    }
    let captures = captures.lock().map_err(|e| e.to_string())?;
    let (passed, rows, failure) = match compare_effects(&captures, &views) {
        Ok(rows) => (true, rows, None),
        Err(error) => (false, Vec::new(), Some(error.to_string())),
    };
    let images: BTreeMap<_, _> = captures
        .iter()
        .map(|(k, v)| (k.clone(), v.clone()))
        .collect();
    let control_equal = views.iter().all(|view| {
        captures
            .get(&format!("{}-baseline", view.name))
            .zip(captures.get(&format!("{}-control", view.name)))
            .is_some_and(|(baseline, control)| baseline == control)
    });
    Ok(
        serde_json::json!({"schema":"forge.generic-optical-gpu-proof.v1","passed":passed,"failure":failure,"observations":observations,"width":WIDTH,"height":HEIGHT,"source_assets":0,"control_frames_byte_equal":control_equal,"imported_materials":0,"comparisons":rows,"images":images,"global_engine_genericity_qualified":false,"adapter":format!("{:?}", app.get_sub_app(RenderApp).ok_or("Render app absent")?.world().resource::<bevy::render::renderer::RenderAdapterInfo>().0)}),
    )
}

#[cfg(not(target_arch = "wasm32"))]
fn next_frame() -> std::future::Ready<Result<(), Error>> {
    std::thread::sleep(Duration::from_millis(16));
    std::future::ready(Ok(()))
}
#[cfg(target_arch = "wasm32")]
async fn next_frame() -> Result<(), Error> {
    use wasm_bindgen::{JsCast, prelude::*};
    let promise = js_sys::Promise::new(&mut |resolve, reject| {
        let callback = Closure::once_into_js(move |_time: f64| {
            let _ = resolve.call0(&JsValue::NULL);
        });
        if let Some(window) = web_sys::window() {
            if let Err(error) = window.request_animation_frame(callback.unchecked_ref()) {
                let _ = reject.call1(&JsValue::NULL, &error);
            }
        } else {
            let _ = reject.call1(&JsValue::NULL, &JsValue::from_str("Browser window absent"));
        }
    });
    wasm_bindgen_futures::JsFuture::from(promise)
        .await
        .map_err(|error| format!("Browser frame failed: {error:?}"))?;
    Ok(())
}

fn compare_effects(
    captures: &BTreeMap<String, Vec<u8>>,
    views: &[View],
) -> Result<Vec<serde_json::Value>, Error> {
    let mut rows = Vec::new();
    for view in views {
        let baseline = captures
            .get(&format!("{}-baseline", view.name))
            .ok_or("Baseline readback absent")?;
        let control = captures
            .get(&format!("{}-control", view.name))
            .ok_or("Control readback absent")?;
        if baseline != control {
            return Err(format!(
                "Unstable baseline for {}: a no-effect frame changed",
                view.name
            )
            .into());
        }
        for phase in ["grain", "flare", "ao"] {
            let key = format!("{}-{phase}", view.name);
            let bytes = captures.get(&key).ok_or("Effect readback absent")?;
            if bytes.len() != WIDTH as usize * HEIGHT as usize * 4 || bytes.len() != baseline.len()
            {
                return Err("Unexpected screenshot storage".into());
            }
            let changed = bytes
                .as_chunks::<4>()
                .0
                .iter()
                .zip(baseline.as_chunks::<4>().0.iter())
                .filter(|(a, b)| a[..3] != b[..3])
                .count();
            if changed < 32 {
                return Err(format!("Effect {key} did not affect enough pixels: {changed}").into());
            }
            rows.push(serde_json::json!({"scene":view.name,"effect":phase,"changed_pixels":changed,"sha256":forge_core::bytes_sha256(bytes).to_string()}));
        }
    }
    Ok(rows)
}

fn advance(app: &mut App) -> Result<(), Error> {
    app.update();
    if let Some(reason) = app.should_exit() {
        return Err(format!("Renderer requested exit before proof completion: {reason:?}").into());
    }
    Ok(())
}
