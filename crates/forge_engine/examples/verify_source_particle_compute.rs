//! Explicit private fixtures; no purchased shader/data is compiled into Forge.
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
    SOURCE_PARTICLE_CURVE_BANK_BYTES, SourceParticleComputePlugin, SourceParticleComputeQueue,
    SourceParticleComputeRequest, SourceParticleComputeStatus,
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
struct FilePin {
    file: String,
    sha256: ContentHash,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Fixture {
    name: String,
    shader: FilePin,
    atlas: FilePin,
    bank: FilePin,
    inputs: FilePin,
    expected: FilePin,
    width: u32,
    height: u32,
    entry_point: String,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Manifest {
    schema_version: u32,
    fixtures: Vec<Fixture>,
}

struct LoadedFixture {
    fixture: Fixture,
    shader: String,
    atlas: Vec<u8>,
    bank: [u8; SOURCE_PARTICLE_CURVE_BANK_BYTES],
    inputs: Vec<u8>,
    expected: Vec<u8>,
}

fn read(root: &Path, pin: &FilePin) -> Result<Vec<u8>, String> {
    let path = Path::new(&pin.file);
    if path.components().count() != 1
        || !matches!(path.components().next(), Some(Component::Normal(_)))
    {
        return Err("Fixture path must be a single relative filename".into());
    }
    let path = root.join(path);
    if std::fs::metadata(&path).map_err(|e| e.to_string())?.len() > 64 * 1024 * 1024 {
        return Err("Fixture file exceeds the private proof budget".into());
    }
    let bytes = std::fs::read(path).map_err(|e| e.to_string())?;
    if bytes_sha256(&bytes) != pin.sha256 {
        return Err(format!("Fixture SHA mismatch: {}", pin.file));
    }
    Ok(bytes)
}

fn load(manifest_path: &Path, expected_hash: ContentHash) -> Result<Vec<LoadedFixture>, String> {
    let bytes = std::fs::read(manifest_path).map_err(|e| e.to_string())?;
    if bytes_sha256(&bytes) != expected_hash {
        return Err("Fixture manifest SHA mismatch".into());
    }
    let manifest: Manifest = serde_json::from_slice(&bytes).map_err(|e| e.to_string())?;
    if manifest.schema_version != 1 || manifest.fixtures.is_empty() || manifest.fixtures.len() > 16
    {
        return Err("Unsupported fixture manifest".into());
    }
    let root = manifest_path
        .parent()
        .ok_or("Fixture manifest parent missing")?;
    manifest
        .fixtures
        .into_iter()
        .map(|fixture| {
            let shader =
                String::from_utf8(read(root, &fixture.shader)?).map_err(|e| e.to_string())?;
            let atlas = read(root, &fixture.atlas)?;
            let bank = read(root, &fixture.bank)?
                .try_into()
                .map_err(|_| "Fixture curve bank size differs from ABI")?;
            let inputs = read(root, &fixture.inputs)?;
            let expected = read(root, &fixture.expected)?;
            let pixels = u64::from(fixture.width) * u64::from(fixture.height) * 8;
            if pixels == 0
                || pixels != atlas.len() as u64
                || inputs.is_empty()
                || inputs.len() % 8 != 0
                || expected.len() != inputs.len() * 2
            {
                return Err("Fixture cardinalities/texture dimensions invalid".into());
            }
            Ok(LoadedFixture {
                fixture,
                shader,
                atlas,
                bank,
                inputs,
                expected,
            })
        })
        .collect()
}

#[derive(Component)]
struct ProofReadback {
    name: String,
    expected: Arc<[u8]>,
    result: ProofResult,
    output: PathBuf,
}

type ProofResult = Arc<Mutex<Option<Result<(), String>>>>;

#[derive(Resource)]
struct Proof {
    requests: Vec<SourceParticleComputeRequest>,
    results: Vec<ProofResult>,
    deadline: Instant,
    draining_frames: u8,
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy observer parameters")]
fn receive(event: On<ReadbackComplete>, queries: Query<&ProofReadback>) {
    let Ok(proof) = queries.get(event.entity) else {
        return;
    };
    // Initial uploads/readbacks may precede the dispatch. The sentinel cannot
    // occur in the proof outputs; completion is judged from GPU bytes only.
    if event.data.iter().all(|byte| *byte == 255) {
        return;
    }
    let Ok(mut result) = proof.result.lock() else {
        return;
    };
    if result.is_some() {
        return;
    }
    // Keep the actual GPU bytes for diagnosis even when native parity fails.
    if let Err(error) = std::fs::write(&proof.output, &event.data) {
        *result = Some(Err(format!("Readback evidence write failed: {error}")));
        return;
    }
    let mismatch = event
        .data
        .iter()
        .zip(proof.expected.iter())
        .position(|(a, b)| a != b);
    *result = Some(
        if event.data.len() != proof.expected.len() || mismatch.is_some() {
            Err(format!(
                "{} GPU bytes differ, first byte {:?}",
                proof.name, mismatch
            ))
        } else {
            println!(
                "PASS {} {} GPU words exact",
                proof.name,
                event.data.len() / 4
            );
            Ok(())
        },
    );
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn finish(
    mut proof: ResMut<Proof>,
    reads: Query<Entity, With<ProofReadback>>,
    mut commands: Commands,
    mut exit: MessageWriter<AppExit>,
) {
    let failed = proof
        .requests
        .iter()
        .find_map(|r| match r.status() {
            Ok(SourceParticleComputeStatus::Failed(error)) => Some(error),
            Err(error) => Some(error.into()),
            _ => None,
        })
        .or_else(|| {
            proof.results.iter().find_map(|r| match r.lock() {
                Ok(result) => result.as_ref().and_then(|r| r.as_ref().err()).cloned(),
                Err(_) => Some("Readback status poisoned".into()),
            })
        });
    if let Some(error) = failed {
        let shader_failed = proof.requests.iter().any(|request| {
            !matches!(
                request.status(),
                Ok(SourceParticleComputeStatus::WaitingForResources
                    | SourceParticleComputeStatus::Compiling
                    | SourceParticleComputeStatus::Encoded)
            )
        });
        if !shader_failed
            && Instant::now() <= proof.deadline
            && proof
                .results
                .iter()
                .any(|slot| slot.lock().is_ok_and(|result| result.is_none()))
        {
            return;
        }
        if proof.draining_frames == 0 {
            eprintln!("FAIL {error}");
            for entity in &reads {
                commands.entity(entity).remove::<Readback>();
            }
        }
        proof.draining_frames += 1;
        if proof.draining_frames >= 4 {
            exit.write(AppExit::error());
        }
    } else if proof
        .results
        .iter()
        .all(|r| r.lock().is_ok_and(|r| matches!(*r, Some(Ok(())))))
    {
        if proof.draining_frames == 0 {
            for entity in &reads {
                commands.entity(entity).remove::<Readback>();
            }
        }
        proof.draining_frames += 1;
        if proof.draining_frames >= 4 {
            exit.write(AppExit::Success);
        }
    } else if Instant::now() > proof.deadline {
        eprintln!("FAIL source particle GPU timeout");
        exit.write(AppExit::error());
    }
}

fn arguments() -> Result<(Vec<LoadedFixture>, PathBuf), String> {
    let mut args = std::env::args().skip(1);
    let path = args
        .next()
        .ok_or("Expected private fixture manifest path")?;
    let hash = args
        .next()
        .ok_or("Expected private manifest SHA256")?
        .parse::<ContentHash>()
        .map_err(|e| e.to_string())?;
    let loaded = load(Path::new(&path), hash)?;
    let evidence_directory = PathBuf::from(
        args.next()
            .ok_or("Expected new output evidence directory")?,
    );
    std::fs::create_dir(&evidence_directory)
        .map_err(|e| format!("Cannot create fresh evidence directory: {e}"))?;
    Ok((loaded, evidence_directory))
}

fn run() -> Result<AppExit, String> {
    let (loaded, evidence_directory) = arguments()?;
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
        ScheduleRunnerPlugin::run_loop(Duration::from_millis(16)),
        SourceParticleComputePlugin,
    ));
    let mut requests = Vec::new();
    let mut results = Vec::new();
    for (slot, loaded) in loaded.into_iter().enumerate() {
        let workgroups = u32::try_from((loaded.inputs.len() / 8).div_ceil(64))
            .map_err(|_| "Proof dispatch too large")?;
        let shader = register_source_particle_compute_program(
            &mut app.world_mut().resource_mut::<Assets<Shader>>(),
            &loaded.shader,
            loaded.fixture.shader.sha256,
            &loaded.fixture.entry_point,
        )?;
        let mut image = Image::new(
            Extent3d {
                width: loaded.fixture.width,
                height: loaded.fixture.height,
                depth_or_array_layers: 1,
            },
            TextureDimension::D2,
            loaded.atlas,
            TextureFormat::Rgba16Float,
            RenderAssetUsages::default(),
        );
        image.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor::linear());
        let curve = app.world_mut().resource_mut::<Assets<Image>>().add(image);
        let input = app
            .world_mut()
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::new(
                &loaded.inputs,
                RenderAssetUsages::default(),
            ));
        let output = app
            .world_mut()
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::new(
                &vec![255; loaded.expected.len()],
                RenderAssetUsages::default(),
            ));
        let request = SourceParticleComputeRequest::new(
            shader,
            loaded.fixture.entry_point,
            curve,
            loaded.bank,
            input,
            output.clone(),
            workgroups,
        )
        .map_err(str::to_owned)?;
        let result = Arc::new(Mutex::new(None));
        app.world_mut()
            .spawn((
                Readback::buffer(output),
                ProofReadback {
                    name: loaded.fixture.name,
                    expected: loaded.expected.into(),
                    result: result.clone(),
                    output: evidence_directory.join(format!("readback-{slot}.u32le.bin")),
                },
            ))
            .observe(receive);
        app.world_mut()
            .resource_mut::<SourceParticleComputeQueue>()
            .insert(slot as u64, request.clone());
        requests.push(request);
        results.push(result);
    }
    app.insert_resource(Proof {
        requests,
        results,
        deadline: Instant::now() + Duration::from_secs(60),
        draining_frames: 0,
    })
    .add_systems(Update, finish);
    Ok(app.run())
}

fn main() -> AppExit {
    match run() {
        Ok(exit) => exit,
        Err(error) => {
            eprintln!("FAIL {error}");
            AppExit::error()
        }
    }
}
