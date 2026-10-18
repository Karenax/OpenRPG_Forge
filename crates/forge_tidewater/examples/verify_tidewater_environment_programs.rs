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
fn dimension(value: &Value) -> Result<wgpu::TextureViewDimension, Box<dyn Error>> {
    Ok(match value.as_str().ok_or("missing dimension")? {
        "2d" => wgpu::TextureViewDimension::D2,
        "2d-array" => wgpu::TextureViewDimension::D2Array,
        "cube" => wgpu::TextureViewDimension::Cube,
        _ => return Err("unqualified texture dimension".into()),
    })
}
fn entry(value: &Value) -> Result<wgpu::BindGroupLayoutEntry, Box<dyn Error>> {
    let layout = &value["layout"];
    if layout["visibility"] != 4 {
        return Err("compute visibility required".into());
    }
    let ty = if let Some(buffer) = layout.get("buffer") {
        wgpu::BindingType::Buffer {
            ty: match buffer["type"].as_str() {
                Some("uniform") => wgpu::BufferBindingType::Uniform,
                Some("storage") => wgpu::BufferBindingType::Storage { read_only: false },
                _ => return Err("unqualified buffer type".into()),
            },
            has_dynamic_offset: false,
            min_binding_size: None,
        }
    } else if let Some(sampler) = layout.get("sampler") {
        wgpu::BindingType::Sampler(match sampler["type"].as_str() {
            Some("filtering") => wgpu::SamplerBindingType::Filtering,
            Some("non-filtering") => wgpu::SamplerBindingType::NonFiltering,
            Some("comparison") => wgpu::SamplerBindingType::Comparison,
            _ => return Err("unqualified sampler type".into()),
        })
    } else if let Some(texture) = layout.get("texture") {
        if texture["sampleType"] != "float" || texture["multisampled"] != false {
            return Err("unqualified sampled texture".into());
        }
        wgpu::BindingType::Texture {
            sample_type: wgpu::TextureSampleType::Float { filterable: true },
            view_dimension: dimension(&texture["viewDimension"])?,
            multisampled: false,
        }
    } else if let Some(texture) = layout.get("storageTexture") {
        if texture["access"] != "write-only" || texture["format"] != "rgba16float" {
            return Err("unqualified storage texture".into());
        }
        wgpu::BindingType::StorageTexture {
            access: wgpu::StorageTextureAccess::WriteOnly,
            format: wgpu::TextureFormat::Rgba16Float,
            view_dimension: dimension(&texture["viewDimension"])?,
        }
    } else {
        return Err("unknown binding kind".into());
    };
    Ok(wgpu::BindGroupLayoutEntry {
        binding: u32::try_from(value["binding"].as_u64().ok_or("missing binding")?)?,
        visibility: wgpu::ShaderStages::COMPUTE,
        ty,
        count: None,
    })
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
    let raw_device = device.wgpu_device();
    let mut pipelines = Vec::new();
    let mut programs = Vec::new();
    for kernel in kernels {
        let name = kernel["name"].as_str().ok_or("program name missing")?;
        let shader_name = kernel["shader"].as_str().ok_or("shader missing")?;
        if !files.contains_key(shader_name) {
            return Err("shader not pinned".into());
        }
        let code = std::fs::read_to_string(root.join(shader_name))?;
        let groups = kernel["groups"].as_array().ok_or("groups missing")?;
        if groups.len() != 2 {
            return Err("two source groups required".into());
        }
        let entries: Vec<Vec<_>> = groups
            .iter()
            .map(|group| {
                group
                    .as_array()
                    .ok_or_else(|| "invalid group".into())
                    .and_then(|group| group.iter().map(entry).collect())
            })
            .collect::<Result<_, Box<dyn Error>>>()?;
        let scope = raw_device.push_error_scope(wgpu::ErrorFilter::Validation);
        let shader = device.create_and_validate_shader_module(wgpu::ShaderModuleDescriptor {
            label: Some(name),
            source: wgpu::ShaderSource::Wgsl(code.into()),
        });
        let layouts: Vec<_> = entries
            .iter()
            .map(|entries| {
                raw_device.create_bind_group_layout(&wgpu::BindGroupLayoutDescriptor {
                    label: Some(name),
                    entries,
                })
            })
            .collect();
        let refs: Vec<_> = layouts.iter().map(Some).collect();
        let layout = raw_device.create_pipeline_layout(&wgpu::PipelineLayoutDescriptor {
            label: Some(name),
            bind_group_layouts: &refs,
            immediate_size: 0,
        });
        let pipeline = device.create_compute_pipeline(&wgpu::ComputePipelineDescriptor {
            label: Some(name),
            layout: Some(&layout),
            module: &shader,
            entry_point: Some("main"),
            compilation_options: default(),
            cache: None,
        });
        if let Some(error) = wait(scope.pop(), device)? {
            return Err(format!("{name}: {error}").into());
        }
        programs.push(json!({"name":name,"shader_sha256":files[shader_name],"explicit_layouts_preserved":true,"status":"created-and-validated"}));
        pipelines.push((kernel.clone(), pipeline, layouts));
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
    let report = json!({"status":"passed","manifest_sha256":args[1],"source_commit":package["source_commit"],"programs":programs,"same_bevy_render_app_device":true,"adapter":{"name":info.name,"backend":format!("{:?}",info.backend)},"device_features":format!("{:?}",device.features()),"gpu_pipeline_creation":true,"scene_resources_supplied":false,"source_ibl_inputs_supplied":execution.is_some(),"dispatch_executed":execution.is_some(),"execution":execution,"visual_parity":false});
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
