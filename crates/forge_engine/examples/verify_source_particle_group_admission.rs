//! Generated control data checks whole-group GPU admission on the shared renderer.
//! No purchased asset, shader or native oracle is embedded in this diagnostic.
#[path = "verify_source_particle_group_admission/dependencies.rs"]
mod dependencies;
#[path = "verify_source_particle_group_admission/owners.rs"]
mod owners;
#[path = "verify_source_particle_group_admission/publication.rs"]
mod publication;
use bevy::{
    app::PluginsState,
    asset::RenderAssetUsages,
    image::{ImageSampler, ImageSamplerDescriptor},
    prelude::*,
    render::{
        RenderApp,
        gpu_readback::{Readback, ReadbackComplete},
        pipelined_rendering::PipelinedRenderingPlugin,
        render_resource::{Extent3d, TextureDimension, TextureFormat},
        renderer::RenderAdapter,
        storage::ShaderBuffer,
    },
    window::{ExitCondition, WindowPlugin},
    winit::WinitPlugin,
};
use forge_core::bytes_sha256;
use forge_engine::source_particle_compute::{
    SourceParticleComputePlugin, SourceParticleComputeQueue, SourceParticleComputeRequest,
    SourceParticleComputeStatus, register_source_particle_compute_program,
};
use std::{
    path::Path,
    sync::{Arc, Mutex},
    time::{Duration, Instant},
};

const SHADER: &str = r"
@group(0) @binding(0) var curve: texture_2d<f32>;
@group(0) @binding(1) var curve_sampler: sampler;
struct Bank { words: array<vec4<u32>,64>, }
@group(0) @binding(2) var<uniform> bank: Bank;
@group(0) @binding(3) var<storage,read> input_words: array<u32>;
@group(0) @binding(4) var<storage,read_write> output_words: array<u32>;
@compute @workgroup_size(64) fn advance(@builtin(global_invocation_id) id:vec3<u32>) {
 if id.x >= arrayLength(&input_words) { return; }
 output_words[id.x] = input_words[id.x] * 10u + bank.words[63].x;
}
";

fn image() -> Image {
    let mut image = Image::new(
        Extent3d {
            width: 1,
            height: 1,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        vec![0; 8],
        TextureFormat::Rgba16Float,
        RenderAssetUsages::default(),
    );
    image.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor::linear());
    image
}

fn update(app: &mut App, deadline: Instant) -> Result<(), String> {
    if Instant::now() > deadline {
        return Err("Group admission probe timeout".into());
    }
    app.update();
    std::thread::sleep(Duration::from_millis(4));
    Ok(())
}

fn readback(
    app: &mut App,
    buffer: Handle<ShaderBuffer>,
    deadline: Instant,
) -> Result<Vec<u8>, String> {
    let slot = Arc::new(Mutex::new(None));
    let sink = slot.clone();
    let entity = app
        .world_mut()
        .spawn(Readback::buffer(buffer))
        .observe(move |event: On<ReadbackComplete>| {
            if let Ok(mut value) = sink.lock() {
                *value = Some(event.data.clone());
            }
        })
        .id();
    let mut previous = None;
    let mut stable = 0;
    for _ in 0.. {
        update(app, deadline)?;
        if let Some(data) = slot.lock().map_err(|_| "Readback mutex poisoned")?.take() {
            if previous.as_ref() == Some(&data) {
                stable += 1;
            } else {
                stable = 0;
            }
            previous = Some(data);
            if stable >= 3 {
                break;
            }
        }
    }
    app.world_mut().entity_mut(entity).remove::<Readback>();
    previous.ok_or_else(|| "GPU readback absent".into())
}

fn requests(
    shader: &Handle<Shader>,
    curve: &Handle<Image>,
    pair: &[Handle<ShaderBuffer>; 2],
    missing_curve: Option<&Handle<Image>>,
    failed_shader: Option<&Handle<Shader>>,
) -> Result<Vec<SourceParticleComputeRequest>, String> {
    (0_u32..3)
        .map(|phase| {
            let mut bank = [0; 1024];
            bank[1008..1012].copy_from_slice(&(phase + 1).to_le_bytes());
            let index = usize::try_from(phase % 2).map_err(|_| "Phase conversion")?;
            SourceParticleComputeRequest::new(
                if phase == 2 {
                    failed_shader.unwrap_or(shader).clone()
                } else {
                    shader.clone()
                },
                "advance".into(),
                if phase == 2 {
                    missing_curve.unwrap_or(curve).clone()
                } else {
                    curve.clone()
                },
                bank,
                pair[index].clone(),
                pair[1 - index].clone(),
                1,
            )
            .map_err(str::to_owned)
        })
        .collect()
}

fn preserve(
    app: &mut App,
    pair: &[Handle<ShaderBuffer>; 2],
    initial: &[Vec<u8>; 2],
    deadline: Instant,
    output: &Path,
    label: &str,
) -> Result<(), String> {
    for index in 0..2 {
        let data = readback(app, pair[index].clone(), deadline)?;
        std::fs::write(output.join(format!("{label}-buffer-{index}.bin")), &data)
            .map_err(|e| e.to_string())?;
        if data != initial[index] {
            return Err(format!("Partial GPU mutation in {label}, buffer {index}"));
        }
    }
    Ok(())
}

#[allow(
    clippy::too_many_lines,
    reason = "Ordered native admission, failure, recovery and readback evidence"
)]
fn run(output: &Path) -> Result<(), String> {
    std::fs::create_dir(output).map_err(|e| e.to_string())?;
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
    )
    .add_plugins(SourceParticleComputePlugin);
    let deadline = Instant::now() + Duration::from_secs(120);
    while app.plugins_state() != PluginsState::Ready {
        if Instant::now() > deadline {
            return Err("Renderer initialization timeout".into());
        }
        std::thread::sleep(Duration::from_millis(8));
    }
    app.finish();
    app.cleanup();
    app.update();
    let adapter = app
        .get_sub_app(RenderApp)
        .ok_or("Renderer absent")?
        .world()
        .resource::<RenderAdapter>()
        .get_info();
    let initial: [Vec<u8>; 2] = std::array::from_fn(|buffer| {
        (0_u32..32)
            .flat_map(|word| (word + if buffer == 0 { 10 } else { 200 }).to_le_bytes())
            .collect()
    });
    let pair = std::array::from_fn(|index| {
        app.world_mut()
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::new(
                &initial[index],
                RenderAssetUsages::default(),
            ))
    });
    let curve = app.world_mut().resource_mut::<Assets<Image>>().add(image());
    let shader = register_source_particle_compute_program(
        &mut app.world_mut().resource_mut::<Assets<Shader>>(),
        SHADER,
        bytes_sha256(SHADER.as_bytes()),
        "advance",
    )?;
    preserve(&mut app, &pair, &initial, deadline, output, "initial")?;

    let shader_count = app.world().resource::<Assets<Shader>>().len();
    let absent_entry = register_source_particle_compute_program(
        &mut app.world_mut().resource_mut::<Assets<Shader>>(),
        SHADER,
        bytes_sha256(SHADER.as_bytes()),
        "entry_absent",
    )
    .err()
    .ok_or("Absent compute entry was admitted")?;
    if !absent_entry.contains("entry_absent")
        || app.world().resource::<Assets<Shader>>().len() != shader_count
    {
        return Err("Absent entry did not preserve shader assets".into());
    }
    for _ in 0..8 {
        update(&mut app, deadline)?;
    }
    preserve(&mut app, &pair, &initial, deadline, output, "refused-entry")?;

    let broken = app
        .world_mut()
        .resource_mut::<Assets<Shader>>()
        .add(Shader::from_wgsl(
            "invalid generated WGSL",
            "generated malformed third-phase shader",
        ));
    let failed = requests(&shader, &curve, &pair, None, Some(&broken))?;
    app.world_mut()
        .resource_mut::<SourceParticleComputeQueue>()
        .insert_ordered(7, failed.clone())
        .map_err(str::to_owned)?;
    loop {
        update(&mut app, deadline)?;
        if failed
            .iter()
            .all(|r| matches!(r.status(), Ok(SourceParticleComputeStatus::Failed(_))))
        {
            break;
        }
    }
    let failure = failed
        .iter()
        .map(SourceParticleComputeRequest::status)
        .collect::<Result<Vec<_>, _>>()
        .map_err(str::to_owned)?;
    preserve(
        &mut app,
        &pair,
        &initial,
        deadline,
        output,
        "failed-third-shader",
    )?;
    app.world_mut()
        .resource_mut::<SourceParticleComputeQueue>()
        .remove(7);

    let absent = app.world().resource::<Assets<Image>>().reserve_handle();
    let waiting = requests(&shader, &curve, &pair, Some(&absent), None)?;
    app.world_mut()
        .resource_mut::<SourceParticleComputeQueue>()
        .insert_ordered(7, waiting.clone())
        .map_err(str::to_owned)?;
    for _ in 0..16 {
        update(&mut app, deadline)?;
    }
    if waiting.iter().any(|r| {
        matches!(
            r.status(),
            Ok(SourceParticleComputeStatus::Encoded | SourceParticleComputeStatus::Failed(_))
        )
    }) {
        return Err("Missing third-phase resource did not hold the entire group".into());
    }
    preserve(
        &mut app,
        &pair,
        &initial,
        deadline,
        output,
        "missing-third-curve",
    )?;
    app.world_mut()
        .resource_mut::<Assets<Image>>()
        .insert(absent.id(), image())
        .map_err(|e| e.to_string())?;
    loop {
        update(&mut app, deadline)?;
        if waiting
            .iter()
            .all(|r| matches!(r.status(), Ok(SourceParticleComputeStatus::Encoded)))
        {
            break;
        }
        if let Some(error) = waiting.iter().find_map(|r| match r.status() {
            Ok(SourceParticleComputeStatus::Failed(error)) => Some(error),
            _ => None,
        }) {
            return Err(error);
        }
    }
    let expected: Vec<u8> = (10_u32..42)
        .flat_map(|word| (word * 1000 + 123).to_le_bytes())
        .collect();
    let actual = readback(&mut app, pair[1].clone(), deadline)?;
    std::fs::write(output.join("recovered-final.bin"), &actual).map_err(|e| e.to_string())?;
    if actual != expected {
        return Err("Recovered group ordering differs on GPU".into());
    }
    for _ in 0..8 {
        update(&mut app, deadline)?;
    }
    if readback(&mut app, pair[1].clone(), deadline)? != expected {
        return Err("Retained group encoded more than once".into());
    }
    app.world_mut()
        .resource_mut::<SourceParticleComputeQueue>()
        .remove(7);
    let independent_owners = owners::verify(&mut app, deadline, output)?;
    let stable_publication = publication::verify(&mut app, &shader, &curve, deadline, output)?;
    let dependency_admission = dependencies::verify(&mut app, &shader, &curve, deadline, output)?;
    let report = serde_json::json!({"status":"GPU_ORDERED_GROUP_FAILURE_WAIT_RECOVERY_AND_ONCE_ONLY_PASS",
        "adapter":adapter.name,"backend":format!("{:?}",adapter.backend),
        "buffers":2,"bytes_per_buffer":128,"third_shader_failure":format!("{failure:?}"),
        "both_buffers_preserved_when_third_shader_fails":true,
        "absent_entry_refused_before_shader_registration":absent_entry,
        "shader_assets_unchanged_after_entry_refusal":true,
        "both_buffers_preserved_after_entry_refusal":true,
        "both_buffers_preserved_when_third_curve_missing":true,
        "same_requests_recover_after_curve_admission":true,"ordered_gpu_words_exact":32,
        "retained_group_not_reencoded":true,"final_sha256":bytes_sha256(&actual).to_string(),
        "independent_owners":independent_owners,
        "stable_publication":stable_publication,
        "dependency_admission":dependency_admission,
        "purchased_sources_used":false,"full_effect_qualified":false});
    std::fs::write(
        output.join("RESULT.v1.json"),
        serde_json::to_vec_pretty(&report).map_err(|e| e.to_string())?,
    )
    .map_err(|e| e.to_string())?;
    println!(
        "PASS GPU group failure/wait preserve both buffers; recovery executes all phases once"
    );
    Ok(())
}

fn main() {
    let result = std::env::args()
        .nth(1)
        .ok_or_else(|| "New evidence output directory required".to_owned())
        .and_then(|path| run(Path::new(&path)));
    if let Err(error) = result {
        eprintln!("FAIL {error}");
        std::process::exit(1);
    }
}
