//! Native compilation proof on the `RenderDevice` created by this Bevy App.
//! Requires a real shader package with explicit v2 state. No scene resources,
//! substitute textures, render submission, drawing or visual verdict.
use bevy::{
    app::PluginsState,
    prelude::*,
    render::{
        RenderApp,
        pipelined_rendering::PipelinedRenderingPlugin,
        renderer::{RenderAdapter, RenderAdapterInfo, RenderDevice},
    },
    window::ExitCondition,
    winit::WinitPlugin,
};
use forge_assets::source_mesh_shaders::SourceMeshShaderPackage;
use forge_core::{ContentHash, bytes_sha256};
use forge_engine::source_mesh_pipeline::{self, SourcePipelineError};
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
        if let Poll::Ready(result) = future.as_mut().poll(&mut cx) {
            return Ok(result);
        }
        if Instant::now() >= deadline {
            return Err("GPU validation future exceeded 30 seconds".into());
        }
        device.poll(wgpu::PollType::Poll)?;
        std::thread::park_timeout(Duration::from_millis(10));
    }
}

fn load(
    root: &Path,
    manifest: &[u8],
    hash: ContentHash,
) -> Result<SourceMeshShaderPackage, Box<dyn Error>> {
    Ok(SourceMeshShaderPackage::load(manifest, hash, |name, _| {
        std::fs::read(root.join(name)).map_err(|e| e.to_string())
    })?)
}

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 3 && args.len() != 5 {
        return Err(
            "Usage: verify_source_mesh_pipelines PACKAGE_ROOT MANIFEST_SHA256 FRESH_REPORT_JSON [POLICY_RECEIPT_JSON POLICY_RECEIPT_SHA256]"
                .into(),
        );
    }
    let root = Path::new(&args[0]);
    let output = Path::new(&args[2]);
    if output.exists() {
        return Err("proof report exists; use a fresh path".into());
    }
    let manifest = std::fs::read(root.join("manifest.json"))?;
    let package = load(root, &manifest, args[1].parse()?)?;
    if package.manifest().version != 2 {
        return Err("explicit pipeline-state v2 package required".into());
    }
    let policy_receipt = policy_receipt(&args, &package)?;
    let mut app = renderer_app()?;
    let render = app
        .get_sub_app_mut(RenderApp)
        .ok_or("Bevy RenderApp missing")?;
    let device = render
        .world()
        .get_resource::<RenderDevice>()
        .ok_or("Bevy RenderDevice missing")?;
    let adapter = render
        .world()
        .get_resource::<RenderAdapter>()
        .ok_or("Bevy RenderAdapter missing")?;
    let adapter_info = render
        .world()
        .get_resource::<RenderAdapterInfo>()
        .ok_or("Bevy adapter info missing")?;
    let started = Instant::now();
    let mut prepared = Vec::new();
    let mut programs = Vec::new();
    for program in &package.manifest().programs {
        let pipeline = wait(
            source_mesh_pipeline::prepare(device, adapter, &package, program),
            device,
        )??;
        if pipeline.late != program.pass.late {
            return Err("prepared variant late identity differs".into());
        }
        programs.push(serde_json::json!({"material":pipeline.material,"pass":pipeline.pass,"late":pipeline.late,
            "code_sha256":pipeline.code_sha256,"pipeline_id":format!("{:?}",pipeline.pipeline.id()),
            "declared_requirements":program.declared_requirements(),"status":"created-and-validated"}));
        prepared.push(pipeline);
    }
    let negative_checks = verify_rejections(root, &manifest, &package, device, adapter)?;
    let limits = device.limits();
    let report = serde_json::json!({"status":"passed", "package_sha256":package.hash(),
        "source_commit":package.manifest().source_commit,"programs":programs,"negative_checks":negative_checks,
        "source_policy_receipt":policy_receipt,"application_variant_inventory_qualified":false,
        "adapter":{"name":adapter_info.name,"backend":format!("{:?}",adapter_info.backend),
            "vendor":adapter_info.vendor,"device":adapter_info.device},
        "device_limits":{"sampled_textures_per_stage":limits.max_sampled_textures_per_shader_stage,
            "samplers_per_stage":limits.max_samplers_per_shader_stage,"uniform_buffers_per_stage":limits.max_uniform_buffers_per_shader_stage,
            "storage_buffers_per_stage":limits.max_storage_buffers_per_shader_stage,"draw_alignment":limits.min_uniform_buffer_offset_alignment},
        "same_bevy_render_app_device":true,"cpu_prepare_ms":started.elapsed().as_secs_f64()*1000.0,
        "gpu_pipeline_creation":true,"scene_resources_supplied":false,"draw_executed":false,"appearance_qualified":false});
    std::fs::write(output, serde_json::to_vec_pretty(&report)?)?;
    println!(
        "{}",
        serde_json::json!({"status":"passed","programs":prepared.len(),"negative_checks":negative_checks,
        "gpu_pipeline_creation":true,"application_variant_inventory_qualified":false,"draw_executed":false,"appearance_qualified":false})
    );
    Ok(())
}

fn policy_receipt(
    args: &[String],
    package: &SourceMeshShaderPackage,
) -> Result<serde_json::Value, Box<dyn Error>> {
    if args.len() == 3 {
        return Ok(serde_json::Value::Null);
    }
    let bytes = std::fs::read(&args[3])?;
    let expected: ContentHash = args[4].parse()?;
    if bytes_sha256(&bytes) != expected {
        return Err("source policy receipt hash differs".into());
    }
    let receipt: serde_json::Value = serde_json::from_slice(&bytes)?;
    if receipt["manifest_sha256"] != serde_json::to_value(package.hash())?
        || receipt["source_commit"] != package.manifest().source_commit
        || !receipt["application_variant_inventory_complete"].is_boolean()
        || receipt["policy_origin"].as_str().is_none_or(str::is_empty)
    {
        return Err("source policy receipt identity or scope differs".into());
    }
    Ok(serde_json::json!({"sha256":expected,"data":receipt}))
}

fn renderer_app() -> Result<App, Box<dyn Error>> {
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
            return Err("Bevy renderer initialization exceeded 30 seconds".into());
        }
        std::thread::sleep(Duration::from_millis(10));
    }
    app.finish();
    app.cleanup();
    Ok(app)
}

fn verify_rejections(
    root: &Path,
    manifest: &[u8],
    package: &SourceMeshShaderPackage,
    device: &RenderDevice,
    adapter: &RenderAdapter,
) -> Result<usize, Box<dyn Error>> {
    let (index, program) = package
        .manifest()
        .programs
        .iter()
        .enumerate()
        .find(|(_, p)| p.pass.kind == forge_assets::source_mesh_shaders::SourcePassKind::Main)
        .ok_or("negative proof requires a main pass")?;
    let material = package
        .manifest()
        .materials
        .iter()
        .find(|m| m.name == program.material)
        .ok_or("negative proof material missing")?;
    let mut limits = device.limits();
    limits.max_sampled_textures_per_shader_stage = 0;
    if !matches!(
        source_mesh_pipeline::admit(&limits, program, material),
        Err(SourcePipelineError::Limit { .. })
    ) {
        return Err("missing texture budget was accepted".into());
    }
    limits = device.limits();
    limits.max_uniform_buffers_per_shader_stage = 0;
    if !matches!(
        source_mesh_pipeline::admit(&limits, program, material),
        Err(SourcePipelineError::Limit { .. })
    ) {
        return Err("missing uniform budget was accepted".into());
    }
    let invalid_code = b"@vertex fn vs() -> @builtin(position) vec4f { return 42; }";
    let hash = bytes_sha256(invalid_code);
    let mut bad: serde_json::Value = serde_json::from_slice(manifest)?;
    let files = bad["files"]
        .as_object_mut()
        .ok_or("fixture file map missing")?;
    files.insert(
        "invalid.wgsl".into(),
        serde_json::json!({"bytes":invalid_code.len(),"sha256":hash}),
    );
    bad["programs"][index]["code"] =
        serde_json::json!({"file":"invalid.wgsl","bytes":invalid_code.len(),"sha256":hash});
    let bytes = serde_json::to_vec(&bad)?;
    let bad_package = SourceMeshShaderPackage::load(&bytes, bytes_sha256(&bytes), |name, _| {
        if name == "invalid.wgsl" {
            Ok(invalid_code.to_vec())
        } else {
            std::fs::read(root.join(name)).map_err(|e| e.to_string())
        }
    })?;
    let failed = wait(
        source_mesh_pipeline::prepare(
            device,
            adapter,
            &bad_package,
            &bad_package.manifest().programs[index],
        ),
        device,
    )?;
    if !matches!(
        failed,
        Err(SourcePipelineError::Validation {
            phase: "shader",
            ..
        })
    ) {
        return Err("invalid WGSL was not rejected by the GPU shader validation scope".into());
    }
    let mut bad: serde_json::Value = serde_json::from_slice(manifest)?;
    let binding = program
        .group0
        .iter()
        .position(|b| b.name == "frame")
        .ok_or("fixture frame binding missing")?;
    bad["programs"][index]["group0"][binding]["layout"]["visibility"] = 2.into();
    let bytes = serde_json::to_vec(&bad)?;
    let bad_package = load(root, &bytes, bytes_sha256(&bytes))?;
    let failed = wait(
        source_mesh_pipeline::prepare(
            device,
            adapter,
            &bad_package,
            &bad_package.manifest().programs[index],
        ),
        device,
    )?;
    if !matches!(
        failed,
        Err(SourcePipelineError::Validation {
            phase: "layout/pipeline",
            ..
        })
    ) {
        return Err(
            "incompatible visibility was not rejected by the GPU pipeline validation scope".into(),
        );
    }
    Ok(4)
}
