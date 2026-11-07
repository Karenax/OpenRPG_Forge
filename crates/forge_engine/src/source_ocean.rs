// SPDX-License-Identifier: MIT
// Adapted from Tidewater OceanFFT.js, copyright 2026 DRG Software Solutions LLC.
// Revision 1438b1abfcaee3267092b75573014f4d9b4a983c; third_party/tidewater/LICENSE.
//! Resident four-cascade FFT on Bevy's existing render device.
//! The verified source package is transport-neutral; this module owns only GPU derivatives.
#![allow(clippy::needless_pass_by_value)]

use crate::ocean_spectrum::{OceanFrame, OceanSpectrum};
use crate::source_water_schedule::OceanComputeSet;
use bevy::{
    asset::RenderAssetUsages,
    core_pipeline::schedule::camera_driver,
    prelude::*,
    render::{
        RenderApp,
        diagnostic::RecordDiagnostics,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        render_asset::RenderAssets,
        render_resource as gpu,
        renderer::{RenderContext, RenderDevice, RenderGraph, RenderGraphSystems},
        storage::{GpuShaderBuffer, ShaderBuffer},
        texture::GpuImage,
    },
};
use forge_assets::source_ocean::OceanInputSource;
use serde_json::Value;
use std::sync::{
    Arc,
    atomic::{AtomicBool, AtomicU32, Ordering},
};

pub mod kernels;
pub mod live_clock;
use kernels::NAMES;

const TOTAL: usize = 256 * 256 * 4;
/// Default live ocean cadence shared by reference and module scenes.
pub const DEFAULT_LIVE_TIME_SCALE: f64 = 1.5;

#[derive(PartialEq)]
struct Input {
    shaders: [String; 9],
    params: [u8; 224],
    bootstrap_steps: u32,
    bootstrap_time: f64,
    frames: Option<Vec<AcquiredFrame>>,
}

#[derive(PartialEq, Eq)]
struct AcquiredFrame {
    params: [u8; 224],
    global: [u8; 720],
}

/// CPU validated source inputs, without any GPU or filesystem handle.
pub struct PreparedOcean(Input);

impl PreparedOcean {
    /// Terrain retouches may retain this exact FFT and its live clock; altered
    /// source programs, parameters or acquisition frames require a new scene.
    pub(crate) fn matches_staged(&self, world: &World) -> bool {
        world
            .get_resource::<StagedInput>()
            .is_some_and(|input| self.0 == *input.0)
    }

    /// Validate initial spectrum and every explicit acquired FFT frame before staging.
    pub fn new(package: &impl OceanInputSource) -> Result<Self, String> {
        let bootstrap_frames = package.bootstrap_frames();
        let has_acquired_frames = package.has_acquired_frames();
        if !(1..=256).contains(&bootstrap_frames)
            || (!has_acquired_frames && bootstrap_frames != 120)
        {
            return Err("Unsupported ocean acquisition length".into());
        }
        let file = |name: &str| {
            let bytes = package
                .file(name)
                .ok_or_else(|| format!("Ocean input missing: {name}"))?;
            if bytes.is_empty() || bytes.len() > 64 * 1024 {
                return Err(format!("Ocean input size limit: {name}"));
            }
            Ok(bytes)
        };
        let metadata: Value =
            serde_json::from_slice(file("ocean.json")?).map_err(|error| error.to_string())?;
        if metadata["fft_size"] != 256
            || metadata["cascades"] != 4
            || (has_acquired_frames
                && metadata["bootstrap_frames"].as_u64() != Some(bootstrap_frames as u64))
        {
            return Err("Ocean source metadata differs from the supported GPU ABI".into());
        }
        let options: OceanSpectrum = metadata
            .get("simulation_options")
            .map(|value| serde_json::from_value(value.clone()))
            .transpose()
            .map_err(|error| error.to_string())?
            .unwrap_or_default();
        let frame: OceanFrame = metadata
            .get("simulation_frame")
            .map(|value| serde_json::from_value(value.clone()))
            .transpose()
            .map_err(|error| error.to_string())?
            .unwrap_or_default();
        if options.cascades != 4 {
            return Err("Ocean source requires four cascades".into());
        }
        let params = options.uniform_bytes(frame).map_err(str::to_owned)?;
        if file("params-initial.bin")? != params {
            return Err("Ocean initial uniforms differ from source".into());
        }
        let mut expected = params;
        let mut time = 0.0_f64;
        let bootstrap_steps = u32::try_from(bootstrap_frames).map_err(|error| error.to_string())?;
        let mut acquired = Vec::new();
        for step in 1..=bootstrap_steps {
            time += 1.0 / 60.0;
            #[allow(clippy::cast_possible_truncation)]
            expected[212..216].copy_from_slice(&(time as f32).to_le_bytes());
            if has_acquired_frames {
                if file(&format!("ocean-step{step:03}-params.bin"))? != expected {
                    return Err(format!("Ocean acquired uniforms differ at frame {step}"));
                }
                let global: [u8; 720] = file(&format!("ocean-step{step:03}-frame.bin"))?
                    .try_into()
                    .map_err(|_| "Ocean acquired global uniform layout mismatch")?;
                if global[584..588] != (1.0_f32 / 60.0).to_le_bytes() {
                    return Err("Ocean acquired global delta differs".into());
                }
                acquired.push(AcquiredFrame {
                    params: expected,
                    global,
                });
            }
        }
        if !has_acquired_frames {
            if file("frame-120-params.bin")? != expected {
                return Err("Ocean 120-step uniforms differ from source".into());
            }
            let global = file("frame-120-frame.bin")?;
            if global.len() != 720 || global[584..588] != (1.0_f32 / 60.0).to_le_bytes() {
                return Err("Ocean 120-step global uniform layout or delta differs".into());
            }
        }
        let shaders = NAMES
            .map(|name| {
                std::str::from_utf8(file(&format!("{name}.wgsl"))?)
                    .map(str::to_owned)
                    .map_err(|error| error.to_string())
            })
            .into_iter()
            .collect::<Result<Vec<_>, String>>()?
            .try_into()
            .map_err(|_| "Ocean kernel count mismatch".to_owned())?;
        Ok(Self(Input {
            shaders,
            params,
            bootstrap_steps,
            bootstrap_time: time,
            frames: has_acquired_frames.then_some(acquired),
        }))
    }

    /// Publish a new generation in the existing renderer. Previous generations remain
    /// referenced only by consumers holding their old handles.
    pub fn stage(self, world: &mut World) -> Result<Targets, String> {
        if !world.contains_resource::<Registered>()
            || !world.contains_resource::<Assets<Image>>()
            || !world.contains_resource::<Assets<ShaderBuffer>>()
        {
            return Err("Ocean renderer must be registered before staging".into());
        }
        let targets = create_targets(world, &self.0.params);
        let clock =
            live_clock::LiveClock::from_bootstrap(self.0.bootstrap_steps, self.0.bootstrap_time)
                .map_err(str::to_owned)?;
        clock
            .set_time_scale(DEFAULT_LIVE_TIME_SCALE)
            .map_err(str::to_owned)?;
        world.insert_resource(StagedInput(Arc::new(self.0)));
        world.insert_resource(targets.clone());
        world.insert_resource(Clock(clock));
        Ok(targets)
    }
}

#[derive(Resource, Clone, ExtractResource)]
struct StagedInput(Arc<Input>);

#[derive(Resource, Clone, ExtractResource)]
pub struct Clock(live_clock::LiveClock);

impl Clock {
    #[must_use]
    pub fn completed(&self, step: u32) -> Option<live_clock::LiveStep> {
        self.0.completed(step)
    }

    pub(crate) fn encoded_for_dependent(&self, step: u32) -> Option<live_clock::LiveStep> {
        self.0.encoded_for_dependent(step)
    }

    pub(crate) fn dependent_encoded(&self, step: u32) -> Result<(), &'static str> {
        self.0.dependent_encoded(step)
    }
}

#[derive(Resource, Clone, ExtractResource)]
pub struct Targets {
    pub completed_steps: Arc<AtomicU32>,
    pub ready: Arc<AtomicBool>,
    pub images: Vec<Handle<Image>>,
    pub sizes: [Vec4; 4],
    pub parameters: [UVec4; 14],
    buffers: Vec<Handle<ShaderBuffer>>,
}

impl Targets {
    /// Resident h0, waveData and foam buffers for acquisition readbacks.
    /// Their `COPY_SRC` usage is part of `ShaderBuffer`'s existing allocation.
    #[must_use]
    pub fn readback_buffers(&self) -> [Handle<ShaderBuffer>; 3] {
        // create_targets always allocates these six private work buffers.
        [
            self.buffers[0].clone(),
            self.buffers[1].clone(),
            self.buffers[3].clone(),
        ]
    }
}

/// Last coherent FFT simulation timestamp for visible water materials.
pub fn completed_frame(world: &World) -> Option<live_clock::LiveStep> {
    let step = world
        .get_resource::<Targets>()?
        .completed_steps
        .load(Ordering::Acquire);
    world.get_resource::<Clock>()?.0.completed(step)
}

/// Shared timeline owner for a bounded pause/resume of the resident simulation.
#[must_use]
pub fn live_clock(world: &World) -> Option<live_clock::LiveClock> {
    world.get_resource::<Clock>().map(|clock| clock.0.clone())
}

#[derive(Resource)]
struct Registered;

/// Register once after Bevy's render plugins. Inputs may arrive after startup.
pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Registered>() {
        return Ok(());
    }
    if !app.world().contains_resource::<Assets<Image>>()
        || !app.world().contains_resource::<Assets<ShaderBuffer>>()
        || app.get_sub_app(RenderApp).is_none()
    {
        return Err("Ocean renderer absent".into());
    }
    app.add_plugins((
        ExtractResourcePlugin::<StagedInput>::default(),
        ExtractResourcePlugin::<Targets>::default(),
        ExtractResourcePlugin::<Clock>::default(),
    ));
    app.add_systems(Update, offer_live_frame);
    app.sub_app_mut(RenderApp).add_systems(
        RenderGraph,
        compute
            .in_set(OceanComputeSet)
            .in_set(RenderGraphSystems::Render)
            .before(camera_driver)
            .run_if(resource_exists::<StagedInput>)
            .run_if(resource_exists::<Targets>)
            .run_if(resource_exists::<Clock>),
    );
    app.insert_resource(Registered);
    Ok(())
}

fn offer_live_frame(
    time: Res<Time<Real>>,
    targets: Option<Res<Targets>>,
    clock: Option<Res<Clock>>,
) {
    let (Some(targets), Some(clock)) = (targets, clock) else {
        return;
    };
    if targets.ready.load(Ordering::Acquire) {
        clock.0.offer(time.elapsed_secs_f64());
    }
}

fn create_targets(world: &mut World, params: &[u8; 224]) -> Targets {
    let buffers = [
        TOTAL * 4,
        TOTAL * 4,
        TOTAL * 8,
        TOTAL,
        TOTAL * 8,
        64 * 4 * 8,
    ]
    .into_iter()
    .map(|count| {
        world
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::from(vec![0.0_f32; count]))
    })
    .collect();
    let images = (0..2)
        .map(|_| {
            let mut image = Image::new_uninit(
                gpu::Extent3d {
                    width: 256,
                    height: 256,
                    depth_or_array_layers: 4,
                },
                gpu::TextureDimension::D2,
                gpu::TextureFormat::Rgba16Float,
                RenderAssetUsages::RENDER_WORLD,
            );
            image.texture_descriptor.mip_level_count = 9;
            image.texture_descriptor.usage = gpu::TextureUsages::STORAGE_BINDING
                | gpu::TextureUsages::TEXTURE_BINDING
                | gpu::TextureUsages::COPY_SRC;
            image.sampler =
                bevy::image::ImageSampler::Descriptor(bevy::image::ImageSamplerDescriptor {
                    address_mode_u: bevy::image::ImageAddressMode::Repeat,
                    address_mode_v: bevy::image::ImageAddressMode::Repeat,
                    min_filter: bevy::image::ImageFilterMode::Linear,
                    mag_filter: bevy::image::ImageFilterMode::Linear,
                    mipmap_filter: bevy::image::ImageFilterMode::Linear,
                    ..default()
                });
            world.resource_mut::<Assets<Image>>().add(image)
        })
        .collect();
    Targets {
        completed_steps: Arc::new(AtomicU32::new(0)),
        ready: Arc::new(AtomicBool::new(false)),
        images,
        sizes: std::array::from_fn(|i| {
            Vec4::new(
                f32::from_le_bytes(params[i * 16..i * 16 + 4].try_into().expect("ocean size")),
                0.0,
                0.0,
                0.0,
            )
        }),
        parameters: std::array::from_fn(|i| {
            UVec4::from_array(std::array::from_fn(|j| {
                u32::from_le_bytes(
                    params[i * 16 + j * 4..i * 16 + j * 4 + 4]
                        .try_into()
                        .expect("ocean parameter"),
                )
            }))
        }),
        buffers,
    }
}

#[derive(Default)]
struct Execution {
    generation: Option<Arc<AtomicBool>>,
    next: u32,
    time: f64,
    pipelines: Vec<gpu::ComputePipeline>,
}

// A resident shore can be staged after the ocean has already advanced. It
// first joins that completed frame, then both producers advance in lockstep.
// During bootstrap the shore can be ahead, so equality is not required.
fn shore_allows_live_step(ocean_step: u32, shore: Option<(bool, u32)>) -> bool {
    shore.is_none_or(|(ready, completed)| ready && completed >= ocean_step)
}

// A shore bootstrap can be ahead of a shorter ocean bootstrap. It only needs
// to consume a live step once the ocean reaches a step it has not encoded.
fn shore_needs_live_step(step: u32, shore_completed: Option<u32>) -> bool {
    shore_completed.is_some_and(|completed| completed < step)
}

#[allow(clippy::too_many_arguments, clippy::too_many_lines)]
fn compute(
    mut context: RenderContext,
    staged: Res<StagedInput>,
    targets: Res<Targets>,
    shore: Option<Res<crate::source_shore_sim::Targets>>,
    clock: Res<Clock>,
    device: Res<RenderDevice>,
    buffers: Res<RenderAssets<GpuShaderBuffer>>,
    images: Res<RenderAssets<GpuImage>>,
    mut execution: Local<Execution>,
) {
    if !execution
        .generation
        .as_ref()
        .is_some_and(|generation| Arc::ptr_eq(generation, &targets.ready))
    {
        *execution = Execution {
            generation: Some(targets.ready.clone()),
            ..default()
        };
    }
    let gpu: Vec<_> = targets
        .buffers
        .iter()
        .filter_map(|handle| buffers.get(handle))
        .collect();
    let textures: Vec<_> = targets
        .images
        .iter()
        .filter_map(|handle| images.get(handle))
        .collect();
    if gpu.len() != targets.buffers.len() || textures.len() != 2 {
        return;
    }
    let live = if targets.ready.load(Ordering::Acquire) {
        let ocean_step = targets.completed_steps.load(Ordering::Acquire);
        // Removing the resident shore must not strand the last encoded FFT.
        // A staged but temporarily unready shore still owns its transaction.
        if shore.is_none() && clock.0.encoded_for_dependent(ocean_step).is_some() {
            clock
                .0
                .dependent_removed(ocean_step)
                .expect("removed shore owns the encoded ocean step");
        }
        let shore_progress = shore.as_ref().map(|shore| {
            (
                shore.ready.load(Ordering::Acquire),
                shore.completed_steps.load(Ordering::Acquire),
            )
        });
        if !shore_allows_live_step(ocean_step, shore_progress) {
            return;
        }
        let Some(frame) = clock.0.pending(execution.next) else {
            return;
        };
        Some(frame)
    } else {
        None
    };
    if !targets.ready.load(Ordering::Acquire) && execution.next > staged.0.bootstrap_steps {
        return;
    }
    let diagnostics = context.diagnostic_recorder();
    let recorder = diagnostics.as_deref();
    let compute_span = recorder.time_span(context.command_encoder(), "source_ocean");
    let views = match kernels::views(&textures) {
        Ok(views) => views,
        Err(reason) => {
            compute_span.end(context.command_encoder());
            error!(%reason, "Ocean GPU views unavailable");
            return;
        }
    };
    if execution.pipelines.is_empty() {
        match kernels::pipelines(&device, &staged.0.shaders, NAMES.len()) {
            Ok(pipelines) => execution.pipelines = pipelines,
            Err(reason) => {
                compute_span.end(context.command_encoder());
                error!(%reason, "Ocean GPU kernels unavailable");
                return;
            }
        }
    }
    let step = execution.next;
    let dt = live.map_or(1.0_f32 / 60.0, |frame| frame.dt);
    let mut frame_bytes = [0_u8; 720];
    frame_bytes[584..588].copy_from_slice(&dt.to_le_bytes());
    let mut params = staged.0.params;
    if let Some(frame) = live {
        debug_assert_eq!(step, frame.step);
        execution.time = frame.time;
    } else if step > 0 {
        execution.time += 1.0 / 60.0;
    }
    #[allow(clippy::cast_possible_truncation)]
    params[212..216].copy_from_slice(&(execution.time as f32).to_le_bytes());
    if live.is_none()
        && step > 0
        && let Some(frames) = &staged.0.frames
    {
        let acquired = &frames[usize::try_from(step - 1).expect("bounded acquired FFT frame")];
        params = acquired.params;
        frame_bytes = acquired.global;
    }
    if let Err(reason) = kernels::encode(
        &device,
        &mut context,
        kernels::Step {
            pipelines: &execution.pipelines,
            buffers: &gpu,
            views: &views,
            frame: &frame_bytes,
            parameters: &params,
            index: step,
        },
    ) {
        compute_span.end(context.command_encoder());
        error!(%reason, "Ocean GPU step refused");
        return;
    }
    compute_span.end(context.command_encoder());
    targets.completed_steps.store(step, Ordering::Release);
    execution.next = step + 1;
    if step == staged.0.bootstrap_steps {
        clock
            .0
            .publish_bootstrap(live_clock::LiveStep {
                step,
                time: execution.time,
                dt: 1.0 / 60.0,
            })
            .expect("final encoded step matches validated acquired bootstrap");
        targets.ready.store(true, Ordering::Release);
    }
    if live.is_some() {
        let shore_completed = shore
            .as_ref()
            .map(|shore| shore.completed_steps.load(Ordering::Acquire));
        clock
            .0
            .fft_encoded(step, shore_needs_live_step(step, shore_completed))
            .expect("encoded ocean matches the pending live step");
    }
}

#[cfg(test)]
mod input_tests {
    use super::*;
    use std::collections::BTreeMap;

    #[test]
    fn live_ocean_waits_for_resident_shore_without_dropping_pending_frame() {
        let clock = live_clock::LiveClock::from_bootstrap(120, 2.0).unwrap();
        clock.offer(1.0);
        clock.offer(1.02);
        assert!(clock.pending(121).is_some());

        // No shore leaves the standalone ocean unrestricted. A staged shore
        // blocks until it has both finished bootstrap and caught up.
        assert!(shore_allows_live_step(120, None));
        assert!(!shore_allows_live_step(120, Some((false, 120))));
        assert!(!shore_allows_live_step(120, Some((true, 119))));
        assert!(clock.pending(121).is_some());
        assert!(shore_allows_live_step(120, Some((true, 120))));
        clock.fft_encoded(121, true).unwrap();
        clock.dependent_encoded(121).unwrap();

        clock.offer(1.04);
        assert!(clock.pending(122).is_some());
        assert!(!shore_allows_live_step(121, Some((true, 120))));
        assert!(clock.pending(122).is_some());
        assert!(shore_allows_live_step(121, Some((true, 121))));

        // A late-loaded shore joins the latest ocean frame before it resumes;
        // an acquired ocean with a shorter bootstrap may instead be behind.
        assert!(!shore_allows_live_step(300, Some((true, 120))));
        assert!(shore_allows_live_step(300, Some((true, 300))));
        assert!(shore_allows_live_step(100, Some((true, 120))));
    }

    #[test]
    fn shorter_ocean_bootstrap_catches_up_before_waiting_for_shore() {
        let clock = live_clock::LiveClock::from_bootstrap(100, 100.0 / 60.0).unwrap();
        clock.offer(0.0);
        for step in 101..=121 {
            clock.offer(f64::from(step - 100) / 60.0);
            let frame = clock.pending(step).expect("next ocean frame");
            assert!(shore_allows_live_step(step - 1, Some((true, 120))));
            let needs_shore = shore_needs_live_step(step, Some(120));
            assert_eq!(needs_shore, step == 121);
            clock.fft_encoded(step, needs_shore).unwrap();
            if needs_shore {
                assert_eq!(clock.completed(step), None);
                clock.dependent_encoded(step).unwrap();
            }
            assert_eq!(clock.completed(step), Some(frame));
        }
        assert!(!shore_needs_live_step(121, None));
    }

    struct Inputs {
        files: BTreeMap<String, Vec<u8>>,
        steps: usize,
        acquired: bool,
    }

    impl OceanInputSource for Inputs {
        fn file(&self, name: &str) -> Option<&[u8]> {
            self.files.get(name).map(Vec::as_slice)
        }
        fn bootstrap_frames(&self) -> usize {
            self.steps
        }
        fn has_acquired_frames(&self) -> bool {
            self.acquired
        }
    }

    fn valid_inputs() -> Inputs {
        let initial = OceanSpectrum::default()
            .uniform_bytes(OceanFrame::default())
            .unwrap();
        let mut step = initial;
        step[212..216].copy_from_slice(&(1.0_f32 / 60.0).to_le_bytes());
        let mut global = vec![0; 720];
        global[584..588].copy_from_slice(&(1.0_f32 / 60.0).to_le_bytes());
        let mut files = BTreeMap::from([
            (
                "ocean.json".into(),
                br#"{"fft_size":256,"cascades":4,"bootstrap_frames":1}"#.to_vec(),
            ),
            ("params-initial.bin".into(), initial.to_vec()),
            ("ocean-step001-params.bin".into(), step.to_vec()),
            ("ocean-step001-frame.bin".into(), global),
        ]);
        for name in NAMES {
            files.insert(format!("{name}.wgsl"), b"// input fixture".to_vec());
        }
        Inputs {
            files,
            steps: 1,
            acquired: true,
        }
    }

    #[test]
    fn generic_ocean_inputs_enforce_acquisition_bounds_and_metadata() {
        let mut input = valid_inputs();
        assert!(PreparedOcean::new(&input).is_ok());
        for steps in [0, 257] {
            input.steps = steps;
            assert!(PreparedOcean::new(&input).is_err());
        }
        input.steps = 1;
        input.acquired = false;
        assert!(PreparedOcean::new(&input).is_err());
        input.acquired = true;
        input.files.insert(
            "ocean.json".into(),
            br#"{"fft_size":128,"cascades":4,"bootstrap_frames":1}"#.to_vec(),
        );
        assert!(PreparedOcean::new(&input).is_err());
    }

    #[test]
    fn generic_ocean_inputs_refuse_oversized_missing_and_invalid_frame_data() {
        let mut input = valid_inputs();
        input
            .files
            .insert("row.wgsl".into(), vec![b'x'; 64 * 1024 + 1]);
        assert!(PreparedOcean::new(&input).is_err());
        input = valid_inputs();
        input.files.remove("column.wgsl");
        assert!(PreparedOcean::new(&input).is_err());
        input = valid_inputs();
        input.files.get_mut("ocean-step001-frame.bin").unwrap()[584] ^= 1;
        assert!(PreparedOcean::new(&input).is_err());
        input = valid_inputs();
        input.files.get_mut("ocean-step001-params.bin").unwrap()[0] ^= 1;
        assert!(PreparedOcean::new(&input).is_err());
    }

    #[test]
    #[allow(
        clippy::cast_possible_truncation,
        reason = "The source FFT frame stores its accumulated f64 time as f32"
    )]
    fn legacy_120_frame_inputs_keep_their_original_admission() {
        let mut input = valid_inputs();
        input.steps = 120;
        input.acquired = false;
        input.files.insert(
            "ocean.json".into(),
            br#"{"fft_size":256,"cascades":4}"#.to_vec(),
        );
        let mut final_params: [u8; 224] = input.files["params-initial.bin"]
            .as_slice()
            .try_into()
            .unwrap();
        let mut time = 0.0_f64;
        for _ in 0..120 {
            time += 1.0 / 60.0;
        }
        final_params[212..216].copy_from_slice(&(time as f32).to_le_bytes());
        input
            .files
            .insert("frame-120-params.bin".into(), final_params.to_vec());
        let mut frame = vec![0; 720];
        frame[584..588].copy_from_slice(&(1.0_f32 / 60.0).to_le_bytes());
        input.files.insert("frame-120-frame.bin".into(), frame);
        assert!(PreparedOcean::new(&input).is_ok());
        input
            .files
            .get_mut("frame-120-frame.bin")
            .unwrap()
            .truncate(719);
        assert!(PreparedOcean::new(&input).is_err());
    }
}
