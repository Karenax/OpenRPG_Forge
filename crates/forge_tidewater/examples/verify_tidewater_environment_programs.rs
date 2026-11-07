//! Validate acquired environment programs on the device owned by Bevy.
//! Optional acquired-input modes execute IBL, or mips plus IBL, without visual acceptance.
#[path = "verify_tidewater_environment_programs/ibl.rs"]
mod ibl;
use bevy::{
    app::PluginsState,
    prelude::*,
    render::{
        RenderApp,
        pipelined_rendering::PipelinedRenderingPlugin,
        renderer::{RenderAdapterInfo, RenderDevice, RenderQueue},
    },
    window::ExitCondition,
    winit::WinitPlugin,
};
use forge_core::bytes_sha256;
use serde_json::{Value, json};
use std::io::Write;
use std::{
    error::Error,
    future::Future,
    path::Path,
    sync::Arc,
    task::{Context, Poll, Wake, Waker},
    time::{Duration, Instant},
};

struct WakeThread(std::thread::Thread);
impl Wake for WakeThread {
    fn wake(self: Arc<Self>) {
        self.0.unpark();
    }
    fn wake_by_ref(self: &Arc<Self>) {
        self.0.unpark();
    }
}
fn wait<T>(future: impl Future<Output = T>, device: &RenderDevice) -> Result<T, Box<dyn Error>> {
    let waker = Waker::from(Arc::new(WakeThread(std::thread::current())));
    let mut cx = Context::from_waker(&waker);
    let mut future = std::pin::pin!(future);
    let deadline = Instant::now() + Duration::from_secs(30);
    loop {
        if let Poll::Ready(value) = future.as_mut().poll(&mut cx) {
            return Ok(value);
        }
        if Instant::now() >= deadline {
            return Err("GPU validation timeout".into());
        }
        device.poll(wgpu::PollType::Poll)?;
        std::thread::park_timeout(Duration::from_millis(10));
    }
}
#[expect(
    clippy::too_many_lines,
    reason = "The probe retains source admissions, GPU execution and its final evidence receipt in execution order"
)]
fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 3
        && args.len() != 5
        && !(args.len() == 6 && ["--generate-mips", "--generate-sky"].contains(&args[5].as_str()))
    {
        return Err(
            "PACKAGE MANIFEST_SHA256 FRESH_REPORT [LIVE_ROOT LIVE_MANIFEST_SHA256 [--generate-mips|--generate-sky]] required".into(),
        );
    }
    let root = Path::new(&args[0]);
    if Path::new(&args[2]).exists() {
        return Err("report must be new".into());
    }
    let raw = std::fs::read(root.join("environment-programs.json"))?;
    if bytes_sha256(&raw).to_string() != args[1] {
        return Err("manifest hash mismatch".into());
    }
    let package: Value = serde_json::from_slice(&raw)?;
    if package["schema"] != "forge.tidewater.environment-programs"
        || package["version"] != 1
        || package["source_commit"] != "1438b1abfcaee3267092b75573014f4d9b4a983c"
    {
        return Err("package identity mismatch".into());
    }
    let files = package["files"].as_object().ok_or("missing file pins")?;
    for (name, hash) in files {
        if name.contains(['/', '\\']) || name == "." || name == ".." {
            return Err("non-flat package path".into());
        }
        if bytes_sha256(&std::fs::read(root.join(name))?).to_string()
            != hash.as_str().ok_or("missing file hash")?
        {
            return Err(format!("file hash mismatch: {name}").into());
        }
    }
    let kernels = package["kernels"].as_array().ok_or("missing programs")?;
    if kernels.len() != 22 {
        return Err("22 programs required".into());
    }
    let mut app = App::new();
    app.add_plugins(
        DefaultPlugins
            .set(WindowPlugin {
                primary_window: None,
                exit_condition: ExitCondition::DontExit,
                ..default()
            })
            .disable::<WinitPlugin>()
            .disable::<PipelinedRenderingPlugin>(),
    );
    let deadline = Instant::now() + Duration::from_secs(30);
    while app.plugins_state() != PluginsState::Ready {
        if Instant::now() >= deadline {
            return Err("Bevy initialization timeout".into());
        }
        std::thread::sleep(Duration::from_millis(10));
    }
    app.finish();
    app.cleanup();
    let render = app.get_sub_app_mut(RenderApp).ok_or("RenderApp missing")?;
    let device = render
        .world()
        .get_resource::<RenderDevice>()
        .ok_or("RenderDevice missing")?;
    let info = render
        .world()
        .get_resource::<RenderAdapterInfo>()
        .ok_or("adapter missing")?;
    let input = forge_engine::source_environment::programs::package::from_acquisition(
        &raw,
        bytes_sha256(&raw),
        |name| std::fs::read(root.join(name)).map_err(|e| e.to_string()),
    )?;
    let prepared = wait(input.prepare(device), device)??;
    let mut pipelines = Vec::new();
    let mut programs = Vec::new();
    for program in prepared.programs {
        let kernel = kernels
            .iter()
            .find(|kernel| kernel["name"].as_str() == Some(program.name.as_str()))
            .ok_or("Prepared source kernel identity differs")?;
        let shader_name = kernel["shader"].as_str().ok_or("shader missing")?;
        if serde_json::from_value::<[u32; 3]>(kernel["dispatch"].clone())? != program.dispatch {
            return Err("Prepared source dispatch differs".into());
        }
        programs.push(json!({"name":program.name,"shader_sha256":files[shader_name],"explicit_layouts_preserved":true,"status":"created-and-validated"}));
        pipelines.push((kernel.clone(), program.pipeline, program.layouts));
    }
    let execution = if args.len() >= 5 {
        let queue = render
            .world()
            .get_resource::<RenderQueue>()
            .ok_or("RenderQueue missing")?;
        Some(ibl::execute(
            Path::new(&args[3]),
            &args[4],
            device,
            queue,
            &pipelines,
            args.len() == 6,
            args.get(5).is_some_and(|arg| arg == "--generate-sky"),
        )?)
    } else {
        None
    };
    let report = json!({"status":"passed","manifest_sha256":args[1],"source_commit":package["source_commit"],"programs":programs,"same_bevy_render_app_device":true,"production_engine_programs":true,"adapter":{"name":info.name,"backend":format!("{:?}",info.backend)},"device_features":format!("{:?}",device.features()),"gpu_pipeline_creation":true,"scene_resources_supplied":false,"source_ibl_inputs_supplied":execution.is_some(),"dispatch_executed":execution.is_some(),"execution":execution,"visual_parity":false});
    let bytes = serde_json::to_vec_pretty(&report)?;
    std::fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&args[2])?
        .write_all(&bytes)?;
    println!(
        "{}",
        json!({"programs":pipelines.len(),"status":"passed","dispatch_executed":args.len()>=5})
    );
    Ok(())
}
