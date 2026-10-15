//! GPU-resident playback against explicit external native reference checkpoints.
use bevy::{
    app::{AppExit, ScheduleRunnerPlugin},
    asset::RenderAssetUsages,
    image::{ImageSampler, ImageSamplerDescriptor},
    prelude::*,
    render::{
        gpu_readback::{Readback, ReadbackComplete},
        render_resource::{Extent3d, TextureDimension, TextureFormat},
        storage::ShaderBuffer,
    },
    window::{ExitCondition, WindowPlugin},
    winit::WinitPlugin,
};
use forge_core::{ContentHash, bytes_sha256};
use forge_engine::source_particle_compute::{
    SourceParticleComputePlugin, SourceParticleComputeQueue, SourceParticleComputeSequence,
    register_source_particle_compute_program,
};
use serde::Deserialize;
use std::{
    path::{Component, Path, PathBuf},
    sync::{Arc, Mutex},
    time::{Duration, Instant},
};

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Pin {
    file: String,
    sha256: ContentHash,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Step {
    delta_time: f32,
    mode: u32,
    label: String,
    expected: Option<Pin>,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Manifest {
    schema_version: u32,
    shader: Pin,
    atlas: Pin,
    bank: Pin,
    initial: Pin,
    record_words: usize,
    particle_count: usize,
    width: u32,
    height: u32,
    entry_point: String,
    steps: Vec<Step>,
}
fn read(root: &Path, pin: &Pin) -> Result<Vec<u8>, String> {
    let p = Path::new(&pin.file);
    if p.components().count() != 1 || !matches!(p.components().next(), Some(Component::Normal(_))) {
        return Err("Expected flat private fixture path".into());
    }
    let bytes = std::fs::read(root.join(p)).map_err(|e| e.to_string())?;
    if bytes.len() > 64 * 1024 * 1024 || bytes_sha256(&bytes) != pin.sha256 {
        return Err(format!(
            "Private fixture size or SHA mismatch: {}",
            pin.file
        ));
    }
    Ok(bytes)
}
struct Loaded {
    manifest: Manifest,
    shader: String,
    atlas: Vec<u8>,
    bank: [u8; 1024],
    initial: Vec<u8>,
    expected: Vec<Option<Vec<u8>>>,
    output: PathBuf,
}
fn arguments() -> Result<Loaded, String> {
    let mut args = std::env::args().skip(1);
    let path = PathBuf::from(args.next().ok_or("Expected private timeline manifest")?);
    let hash = args
        .next()
        .ok_or("Expected manifest SHA")?
        .parse::<ContentHash>()
        .map_err(|e| e.to_string())?;
    let output = PathBuf::from(args.next().ok_or("Expected fresh evidence directory")?);
    let bytes = std::fs::read(&path).map_err(|e| e.to_string())?;
    if bytes_sha256(&bytes) != hash {
        return Err("Timeline manifest SHA mismatch".into());
    }
    let manifest: Manifest = serde_json::from_slice(&bytes).map_err(|e| e.to_string())?;
    if manifest.schema_version != 1
        || manifest.record_words != 40
        || manifest.particle_count != 2
        || !(1..=4096).contains(&manifest.width)
        || !(1..=4096).contains(&manifest.height)
        || manifest.steps.is_empty()
        || manifest.steps.len() > 4096
    {
        return Err("Unsupported private resident timeline fixture".into());
    }
    let root = path.parent().ok_or("Timeline parent missing")?;
    let shader = String::from_utf8(read(root, &manifest.shader)?).map_err(|e| e.to_string())?;
    let atlas = read(root, &manifest.atlas)?;
    let bank = read(root, &manifest.bank)?
        .try_into()
        .map_err(|_| "Timeline curve bank ABI mismatch")?;
    let initial = read(root, &manifest.initial)?;
    if initial.len() != manifest.particle_count * manifest.record_words * 4
        || atlas.len() as u64 != u64::from(manifest.width) * u64::from(manifest.height) * 8
    {
        return Err("Timeline resource cardinalities invalid".into());
    }
    let mut expected = Vec::new();
    for step in &manifest.steps {
        if !step.delta_time.is_finite() || step.delta_time < 0.0 || step.mode > 1 {
            return Err("Invalid source step".into());
        }
        let reference = step.expected.as_ref().map(|p| read(root, p)).transpose()?;
        if reference.as_ref().is_some_and(|bytes| bytes.len() != 104) {
            return Err("Native checkpoint field cardinality invalid".into());
        }
        expected.push(reference);
    }
    if expected.iter().all(Option::is_none) {
        return Err("Timeline has no native reference checkpoint".into());
    }
    std::fs::create_dir(&output).map_err(|e| e.to_string())?;
    Ok(Loaded {
        manifest,
        shader,
        atlas,
        bank,
        initial,
        expected,
        output,
    })
}

type ResultSlot = Arc<Mutex<Option<Result<(), String>>>>;
#[derive(Component)]
struct Checkpoint {
    step: u32,
    expected: Vec<u8>,
    properties: Vec<u8>,
    result: ResultSlot,
    path: PathBuf,
}
#[derive(Resource)]
struct Playback {
    sequence: SourceParticleComputeSequence,
    steps: Vec<Step>,
    expected: Vec<Option<Vec<u8>>>,
    bank: [u8; 1024],
    initial: Vec<u8>,
    output: PathBuf,
    checkpoint: Option<ResultSlot>,
    checkpoint_handled: usize,
    checkpoints_passed: usize,
    deadline: Instant,
    drain: u8,
}
fn u32_at(bytes: &[u8], word: usize) -> Option<u32> {
    bytes
        .get(word * 4..word * 4 + 4)
        .and_then(|b| b.try_into().ok())
        .map(u32::from_le_bytes)
}
#[allow(clippy::needless_pass_by_value, reason = "Bevy observer parameters")]
fn receive(event: On<ReadbackComplete>, queries: Query<&Checkpoint>, mut commands: Commands) {
    let Ok(check) = queries.get(event.entity) else {
        return;
    };
    if u32_at(&event.data, 14) != Some(check.step) || u32_at(&event.data, 54) != Some(check.step) {
        return;
    }
    let Ok(mut result) = check.result.lock() else {
        return;
    };
    if result.is_some() {
        return;
    }
    let outcome = (|| {
        if event.data.len() != 320 {
            return Err("Resident state readback length invalid".into());
        }
        for particle in 0..2 {
            let base = particle * 160;
            let expected = particle * 52;
            if event.data[base..base + 52] != check.expected[expected..expected + 52] {
                return Err(format!(
                    "Native state differs at step{} particle{}",
                    check.step, particle
                ));
            }
            if event.data[base + 64..base + 160] != check.properties[base + 64..base + 160] {
                return Err("GPU source properties changed while advancing state".into());
            }
            if u32_at(&event.data, particle * 40 + 15) != Some(0x5253_5441) {
                return Err("Resident source stamp invalid".into());
            }
        }
        std::fs::write(&check.path, &event.data).map_err(|e| e.to_string())?;
        println!("PASS step{} 26 native field values exact", check.step);
        Ok(())
    })();
    *result = Some(outcome);
    commands.entity(event.entity).remove::<Readback>();
}
fn advance(
    playback: &mut Playback,
    queue: &mut SourceParticleComputeQueue,
    commands: &mut Commands,
) -> Result<bool, String> {
    if !playback.sequence.poll()? {
        return Ok(false);
    }
    if let Some(slot) = &playback.checkpoint {
        let result = slot
            .lock()
            .map_err(|_| "Timeline readback status poisoned")?
            .clone();
        match result {
            None => return Ok(false),
            Some(Err(e)) => return Err(e),
            Some(Ok(())) => {
                playback.checkpoints_passed += 1;
            }
        }
        playback.checkpoint = None;
    }
    let done = usize::try_from(playback.sequence.encoded_steps())
        .map_err(|_| "Timeline counter overflow")?;
    if done > 0 && playback.checkpoint_handled < done {
        playback.checkpoint_handled = done;
        if let Some(expected) = &playback.expected[done - 1] {
            let slot = Arc::new(Mutex::new(None));
            commands
                .spawn((
                    Readback::buffer(playback.sequence.current_buffer()),
                    Checkpoint {
                        step: u32::try_from(done).map_err(|_| "Timeline stamp overflow")?,
                        expected: expected.clone(),
                        properties: playback.initial.clone(),
                        result: slot.clone(),
                        path: playback.output.join(format!("step-{done}.u32le.bin")),
                    },
                ))
                .observe(receive);
            playback.checkpoint = Some(slot);
            return Ok(false);
        }
    }
    if done == playback.steps.len() {
        return Ok(true);
    }
    let step = &playback.steps[done];
    let mut bank = playback.bank;
    let stamp =
        u16::try_from(done + 1).map_err(|_| "Timeline stamp exceeds exact f32 integer range")?;
    bank[1008..1012].copy_from_slice(&step.delta_time.to_le_bytes());
    bank[1012..1016].copy_from_slice(
        &f32::from(u16::try_from(step.mode).map_err(|_| "Invalid source mode")?).to_le_bytes(),
    );
    bank[1016..1020].copy_from_slice(&f32::from(stamp).to_le_bytes());
    if !playback.sequence.submit_next(queue, bank)? {
        return Err("Timeline advance lost ready state".into());
    }
    if done == 0 {
        println!(
            "START {} source steps; first {}",
            playback.steps.len(),
            step.label
        );
    }
    Ok(false)
}
#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn update(
    mut playback: ResMut<Playback>,
    mut queue: ResMut<SourceParticleComputeQueue>,
    mut commands: Commands,
    mut exit: MessageWriter<AppExit>,
) {
    if Instant::now() > playback.deadline {
        eprintln!("FAIL resident source timeline timeout");
        exit.write(AppExit::error());
        return;
    }
    match advance(&mut playback, &mut queue, &mut commands) {
        Err(e) => {
            eprintln!("FAIL {e}");
            exit.write(AppExit::error());
        }
        Ok(true) => {
            playback.drain += 1;
            if playback.drain >= 4 {
                println!(
                    "PASS {} ordered resident dispatches, {} native checkpoints; no CPU state uploads",
                    playback.steps.len(),
                    playback.checkpoints_passed
                );
                exit.write(AppExit::Success);
            }
        }
        Ok(false) => {}
    }
}
fn run() -> Result<AppExit, String> {
    let loaded = arguments()?;
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
    .add_plugins((
        ScheduleRunnerPlugin::run_loop(Duration::from_millis(8)),
        SourceParticleComputePlugin,
    ));
    let shader = register_source_particle_compute_program(
        &mut app.world_mut().resource_mut::<Assets<Shader>>(),
        &loaded.shader,
        loaded.manifest.shader.sha256,
        &loaded.manifest.entry_point,
    )?;
    let mut image = Image::new(
        Extent3d {
            width: loaded.manifest.width,
            height: loaded.manifest.height,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        loaded.atlas,
        TextureFormat::Rgba16Float,
        RenderAssetUsages::default(),
    );
    image.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor::linear());
    let curve = app.world_mut().resource_mut::<Assets<Image>>().add(image);
    let buffers = [
        app.world_mut()
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::new(
                &loaded.initial,
                RenderAssetUsages::default(),
            )),
        app.world_mut()
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::new(
                &loaded.initial,
                RenderAssetUsages::default(),
            )),
    ];
    let sequence = SourceParticleComputeSequence::new(
        0,
        shader,
        loaded.manifest.entry_point,
        curve,
        buffers,
        1,
    )
    .map_err(str::to_owned)?;
    app.insert_resource(Playback {
        sequence,
        steps: loaded.manifest.steps,
        expected: loaded.expected,
        bank: loaded.bank,
        initial: loaded.initial,
        output: loaded.output,
        checkpoint: None,
        checkpoint_handled: 0,
        checkpoints_passed: 0,
        deadline: Instant::now() + Duration::from_secs(120),
        drain: 0,
    })
    .add_systems(Update, update);
    Ok(app.run())
}
fn main() -> AppExit {
    match run() {
        Ok(exit) => exit,
        Err(e) => {
            eprintln!("FAIL {e}");
            AppExit::error()
        }
    }
}
