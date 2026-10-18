#[path = "../../forge_engine/src/source_graph_srgb_curve.rs"]
mod source_srgb_curve;
#[path = "../../forge_engine/src/source_graph_srgb_measure.rs"]
mod source_srgb_measure;
// Generic native sRGB transfer probes. No purchased assets or Unity data loaded.
use bevy::{
    app::PluginsState,
    prelude::*,
    render::{
        RenderApp, RenderPlugin,
        pipelined_rendering::PipelinedRenderingPlugin,
        renderer::{RenderAdapterInfo, RenderDevice, RenderQueue},
        settings::{Backends, RenderCreation, WgpuSettings},
    },
    window::ExitCondition,
    winit::WinitPlugin,
};
use std::{
    path::Path,
    sync::{Arc, Mutex},
    time::{Duration, Instant},
};
fn main() -> Result<(), Box<dyn std::error::Error>> {
    let args: Vec<_> = std::env::args().collect();
    let root = Path::new(&args[1]);
    let mut app = App::new();
    app.add_plugins(
        DefaultPlugins
            .set(WindowPlugin {
                primary_window: None,
                exit_condition: ExitCondition::DontExit,
                ..default()
            })
            .set(RenderPlugin {
                render_creation: RenderCreation::Automatic(Box::new(WgpuSettings {
                    backends: Some(Backends::DX12),
                    ..default()
                })),
                ..default()
            })
            .disable::<WinitPlugin>()
            .disable::<PipelinedRenderingPlugin>(),
    );
    let deadline = Instant::now() + Duration::from_secs(30);
    while app.plugins_state() != PluginsState::Ready {
        if Instant::now() > deadline {
            return Err("GPU initialization timed out".into());
        }
        std::thread::sleep(Duration::from_millis(10));
    }
    app.finish();
    app.cleanup();
    app.update();
    let rw = app.sub_app(RenderApp).world();
    let device = rw.resource::<RenderDevice>().clone();
    let queue = rw.resource::<RenderQueue>().clone();
    let info = rw.resource::<RenderAdapterInfo>();
    let adapter = info.name.clone();
    let backend = format!("{:?}", info.backend);
    let device_info = serde_json::json!({"vendor":info.vendor,"device":info.device,"driver":info.driver.clone(),"driver_info":info.driver_info.clone()});
    let errors: Arc<Mutex<Vec<String>>> = Arc::new(Mutex::new(Vec::new()));
    let sink = errors.clone();
    device
        .wgpu_device()
        .on_uncaptured_error(Arc::new(move |e| sink.lock().unwrap().push(e.to_string())));

    let started = Instant::now();
    let curve = source_srgb_measure::measure(&device, &queue, info)?;
    let key = source_srgb_measure::device_key(info);
    let bytes = curve.r32f_bytes_for_device(&key)?;
    std::fs::write(root.join("PRODUCER-CURVE.r32f"), &bytes)?;
    let mut wrong = key.clone();
    wrong.device ^= 1;
    assert!(curve.r32f_bytes_for_device(&wrong).is_err());
    assert!(errors.lock().unwrap().is_empty(), "GPU validation errors");
    let result = serde_json::json!({"status":"native_srgb_producer_measured_without_file_inputs","adapter":adapter,"backend":backend,"device_info":device_info,"curve_bytes":bytes.len(),"wrong_device_rejected":true,"gpu_errors":errors.lock().unwrap().clone(),"elapsed_seconds":started.elapsed().as_secs_f64(),"purchased_asset_inputs":0,"unity_pixel_inputs":0,"material_integration":false,"full_import":false});
    std::fs::write(
        root.join("NATIVE-RESULT.v1.json"),
        serde_json::to_vec_pretty(&result)?,
    )?;
    println!("{result}");
    Ok(())
}
