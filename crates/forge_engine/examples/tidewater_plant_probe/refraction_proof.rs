//! Explicit static capture triplet; independent of the walk/swim regressions.
use bevy::{
    app::AppExit,
    diagnostic::FrameCount,
    math::UVec4,
    prelude::*,
    render::{
        gpu_readback::{Readback, ReadbackComplete},
        view::screenshot::{Screenshot, ScreenshotCaptured, save_to_disk},
    },
};
use forge_engine::tidewater_refraction as capture;
use serde_json::{Value, json};
use std::{path::PathBuf, sync::atomic::Ordering, time::Instant};

#[derive(Resource)]
pub(super) struct Proof {
    path: PathBuf,
    started: Instant,
    phase: usize,
    frame: u32,
    baseline: u64,
    off_counter: u64,
    off_stable: u32,
    original: bool,
    signature: Option<Value>,
    captures: Vec<Value>,
    pixels: Option<Vec<u8>>,
    waiting: bool,
    raw_requested: bool,
    raw: [Option<Value>; 2],
    retries: u8,
    readback_mismatches: Vec<Value>,
}

pub(super) fn install(app: &mut App) -> Result<(), Box<dyn std::error::Error>> {
    if std::env::var("FORGE_TIDEWATER_REFRACTION_PROOF").as_deref() != Ok("1") {
        return Ok(());
    }
    let probe = app.world().resource::<super::Probe>();
    if !matches!(probe.plant_projection, super::PlantProjection::Static) {
        return Err("Refraction triplet requires Static projection; walk/navigation are separate regressions".into());
    }
    let path = probe.screenshot.clone();
    if path.with_extension("refraction.json").exists() {
        return Err("Refraction proof receipt already exists".into());
    }
    let original = app
        .world()
        .get_resource::<capture::Control>()
        .ok_or("Refraction capture not registered")?
        .enabled;
    app.insert_resource(Proof {
        path,
        started: Instant::now(),
        phase: 0,
        frame: 0,
        baseline: 0,
        off_counter: 0,
        off_stable: 0,
        original,
        signature: None,
        captures: Vec::new(),
        pixels: None,
        waiting: false,
        raw_requested: false,
        raw: [None, None],
        retries: 0,
        readback_mismatches: Vec::new(),
    })
    .insert_resource(bevy::winit::WinitSettings::continuous())
    .add_systems(Last, update);
    Ok(())
}

fn update(world: &mut World) {
    let Some(mut proof) = world.remove_resource::<Proof>() else {
        return;
    };
    let outcome = advance(world, &mut proof);
    match outcome {
        Ok(false) => {
            world.insert_resource(proof);
        }
        Ok(true) => finish(world, &proof, None),
        Err(error) => finish(world, &proof, Some(error)),
    }
}

// Each phase keeps the actual capture-control transition, GPU observations and
// readback checks together so no intermediate receipt can look complete.
#[allow(clippy::too_many_lines)]
fn advance(world: &mut World, proof: &mut Proof) -> Result<bool, String> {
    if proof.started.elapsed().as_secs_f64() > 180.0 {
        return Err("Static refraction triplet exceeded 180 s".into());
    }
    if world.resource::<super::Probe>().failed {
        return Err("Native pipeline observation failed".into());
    }
    if world.contains_resource::<super::ocean::LiveClock>() {
        return Err("Static proof refuses a live ocean clock".into());
    }
    let frame = world.resource::<FrameCount>().0;
    if frame < 120 || world.resource::<super::Probe>().ready_frames < 8 || !sources_ready(world) {
        return Ok(false);
    }
    let expected = proof.phase != 1;
    if world.resource::<capture::Control>().enabled != expected {
        return Err("Refraction control changed outside proof".into());
    }
    let observation = world.resource::<capture::Observation>();
    if observation.error.is_some() {
        return Err(format!("Refraction observation: {:?}", observation.error));
    }
    if observation.ready != expected {
        return Ok(false);
    }
    let state = snapshot(world)?;
    if state["ready"] != expected {
        return Ok(false);
    }
    let signature = &state["signature"];
    if let Some(frozen) = &proof.signature
        && signature != frozen
    {
        return Err(
            "Static camera, projection, optical inputs or observed GPU stamp changed".into(),
        );
    }
    if proof.frame == 0 {
        let baseline = state["encoded_frames"]
            .as_u64()
            .ok_or("Missing capture counter")?;
        rebase_readbacks(world, proof, frame, baseline);
    }
    if proof.waiting {
        return Ok(false);
    }
    if proof.captures.len() > proof.phase {
        if proof.captures[proof.phase]["valid"] != true {
            return Err("Screenshot failed frozen signature or pixel checks".into());
        }
        proof.phase += 1;
        if proof.phase == 3 {
            let effect = &proof.captures[1]["pixel_delta"];
            let repeat = &proof.captures[2]["pixel_delta"];
            if !effect["mean_abs_rgb"].as_f64().is_some_and(|x| x > 0.01)
                || effect["pixels_delta_gt_2"]
                    .as_u64()
                    .is_none_or(|x| x < 1000)
            {
                return Err(
                    "No measurable native refraction effect (.01 RGB / 1000 pixels >2)".into(),
                );
            }
            if repeat["max_channel_delta"] != 0
                || proof.captures[0]["sha256"] != proof.captures[2]["sha256"]
            {
                return Err("Native refraction repeat is not pixel/PNG exact".into());
            }
            for channel in ["color", "depth"] {
                if proof.captures[0]["raw"][channel]["sha256"]
                    != proof.captures[2]["raw"][channel]["sha256"]
                {
                    return Err("Fresh repeated HDR/depth bytes differ".into());
                }
            }
            return Ok(true);
        }
        world.resource_mut::<capture::Control>().enabled = proof.phase != 1;
        proof.frame = frame;
        proof.baseline = state["encoded_frames"]
            .as_u64()
            .ok_or("Missing capture counter")?;
        proof.off_counter = proof.baseline;
        proof.off_stable = 0;
        if proof.phase == 2 {
            let baseline = proof.baseline;
            rebase_readbacks(world, proof, frame, baseline);
        }
        return Ok(false);
    }
    if expected {
        queue_readback_pair(world, proof, frame);
    }
    let encoded = state["encoded_frames"]
        .as_u64()
        .ok_or("Missing capture counter")?;
    if !expected {
        proof.off_stable = if encoded == proof.off_counter {
            proof.off_stable + 1
        } else {
            0
        };
        proof.off_counter = encoded;
    }
    if frame.saturating_sub(proof.frame) <= 40
        || (expected && encoded <= proof.baseline + 40)
        || (!expected && proof.off_stable < 8)
    {
        return Ok(false);
    }
    if expected {
        if !gpu_valid(world) {
            return Ok(false);
        }
        if proof.raw.iter().any(Option::is_none) {
            return Ok(false);
        }
        if proof.raw.iter().flatten().any(|r| r["error"].is_string()) {
            return Err("HDR/depth raw readback failed".into());
        }
        if !raw_matches_gpu(world, proof) {
            proof.readback_mismatches.push(json!({"phase":(["on","off","repeat"][proof.phase]),
                "attempt":proof.retries,"main_frame":frame,"state":state,"raw":proof.raw,
                "phase_started_main_frame":proof.frame,"capture_encoding_baseline":proof.baseline}));
            if !take_retry(&mut proof.retries) {
                return Err("HDR/depth readbacks still differ after two fresh retries".into());
            }
            rebase_readbacks(world, proof, frame, encoded);
            save(proof, "running", None)?;
            return Ok(false);
        }
    }
    if proof.signature.is_none() {
        proof.signature = Some(signature.clone());
    }
    let phase = ["on", "off", "repeat"][proof.phase];
    let raw = json!({"color": proof.raw[0], "depth": proof.raw[1], "fresh": expected});
    let path = proof.path.with_extension(format!("refraction.{phase}.png"));
    let expected_signature = proof.signature.clone().ok_or("Missing frozen signature")?;
    let settling = json!({"phase_started_main_frame":proof.frame,"capture_encoding_baseline":proof.baseline,"off_stable_frames":proof.off_stable});
    proof.waiting = true;
    world
        .spawn(Screenshot::primary_window())
        .observe(screenshot_observer(
            path,
            phase,
            expected_signature,
            raw,
            settling,
        ));
    Ok(false)
}

fn rebase_readbacks(world: &mut World, proof: &mut Proof, frame: u32, baseline: u64) {
    proof.frame = frame;
    proof.baseline = baseline;
    capture::observe_while_held(world, false);
    // Start the shared hold now, including when repeat returns early this frame.
    capture::observe_while_held(world, true);
    proof.raw = [None, None];
    proof.raw_requested = false;
}

fn queue_readback_pair(world: &mut World, proof: &mut Proof, frame: u32) {
    capture::observe_while_held(world, true);
    // Match the shared observer's forty-frame hold, before waiting on its callback.
    // Both texture requests are extracted together and copied by the same renderer.
    if frame.saturating_sub(proof.frame) >= 40 && !proof.raw_requested {
        request_raw(world, proof);
        proof.raw_requested = true;
    }
}

fn raw_matches_gpu(world: &World, proof: &Proof) -> bool {
    let Some(gpu) = world.get_resource::<capture::GpuObservation>() else {
        return false;
    };
    [gpu.color.as_ref(), gpu.depth.as_ref()]
        .into_iter()
        .zip(&proof.raw)
        .all(|(sample, raw)| {
            sample.is_some_and(|sample| {
                raw.as_ref().and_then(|raw| raw["checksum64"].as_u64()) == Some(sample.checksum64)
            })
        })
}

fn take_retry(retries: &mut u8) -> bool {
    if *retries >= 2 {
        return false;
    }
    *retries += 1;
    true
}

fn checksum64(bytes: &[u8], row_bytes: usize, stride: usize) -> Option<u64> {
    if row_bytes == 0
        || stride < row_bytes
        || bytes.is_empty()
        || !bytes.len().is_multiple_of(stride)
    {
        return None;
    }
    // Same diagnostic byte algorithm as the shared observation; aligned padding
    // is excluded. File SHA256 and independent RGB/raw decoding remain required.
    Some(
        bytes
            .chunks_exact(stride)
            .fold(0xcbf2_9ce4_8422_2325, |hash, row| {
                row[..row_bytes].iter().fold(hash, |hash, byte| {
                    (hash ^ u64::from(*byte)).wrapping_mul(0x0100_0000_01b3)
                })
            }),
    )
}

fn screenshot_observer(
    path: PathBuf,
    phase: &'static str,
    expected_signature: Value,
    raw: Value,
    settling: Value,
) -> impl FnMut(On<ScreenshotCaptured>, Commands) {
    move |event: On<ScreenshotCaptured>, mut commands: Commands| {
        let image = event.image.clone();
        let dimensions = [
            image.texture_descriptor.size.width,
            image.texture_descriptor.size.height,
        ];
        save_to_disk(&path)(event);
        let path = path.clone();
        let expected_signature = expected_signature.clone();
        let raw = raw.clone();
        let settling = settling.clone();
        // Observers cannot borrow the entire World exclusively. Apply the same
        // receipt checks through a queued command with owned screenshot data.
        commands.queue(move |world: &mut World| {
            let state = snapshot(world);
            let pixels = image
                .try_into_dynamic()
                .ok()
                .map(|v| v.to_rgb8().into_raw());
            let mut delta = None;
            if let Some(rgb) = &pixels {
                let mut proof = world.resource_mut::<Proof>();
                if phase == "on" {
                    proof.pixels = Some(rgb.clone());
                } else if let Some(first) = &proof.pixels {
                    delta = pixel_delta(first, rgb);
                }
            }
            let bytes = std::fs::read(&path);
            let valid = state.as_ref().is_ok_and(|s| {
                s["signature"] == expected_signature && s["ready"] == (phase != "off")
            }) && dimensions == [1920, 1080]
                && pixels.as_ref().is_some_and(|p| p.len() == 1920 * 1080 * 3)
                && bytes.as_ref().is_ok_and(|b| !b.is_empty())
                && (phase == "on" || delta.is_some());
            let record = json!({"phase":phase,"path":path,"valid":valid,"dimensions":dimensions,
                "sha256":bytes.as_ref().ok().map(|b|forge_core::bytes_sha256(b).to_string()),
                "bytes":bytes.as_ref().ok().map(Vec::len),
                "pixel_sha256":pixels.as_ref().map(|b|forge_core::bytes_sha256(b).to_string()),
                "pixel_delta":delta,"raw":raw,"settling":settling,"state":state.as_ref().ok(),"error":state.err()});
            let mut proof = world.resource_mut::<Proof>();
            proof.captures.push(record);
            proof.waiting = false;
            let _ = save(&proof, "running", None);
        });
    }
}

fn sources_ready(world: &World) -> bool {
    world
        .get_resource::<super::ocean::Targets>()
        .is_some_and(|t| {
            t.ready.load(Ordering::Acquire) && t.readback_complete.load(Ordering::Acquire)
        })
        && world
            .get_resource::<super::shore_sim::Targets>()
            .is_none_or(|t| {
                t.ready.load(Ordering::Acquire) && t.readback_complete.load(Ordering::Acquire)
            })
        && world
            .get_resource::<super::clouds::Targets>()
            .is_none_or(|t| t.ready.load(Ordering::Acquire))
        && world
            .resource::<super::terrain_normals::Shared>()
            .ready
            .load(Ordering::Acquire)
        && world
            .get_resource::<forge_engine::tidewater_underwater_lighting::Targets>()
            .is_some_and(|maps| {
                maps.observation.lock().is_ok_and(|stamp| {
                    stamp.step > 0 && stamp.resolution == 512 && stamp.layers == 2
                })
            })
}

// The material stores this as an exact 0/1 GPU-ready flag. An epsilon here
// would accept an intermediate state and weaken the proof's binding check.
#[allow(clippy::float_cmp)]
fn exact_refraction_flag(actual: f32, expected: bool) -> bool {
    actual == f32::from(expected)
}

fn optical_frames(world: &World, enabled: bool) -> Result<Vec<Vec<[u32; 4]>>, String> {
    let targets = world.resource::<capture::Targets>();
    let materials = world.resource::<Assets<super::water_optics::WaterMaterial>>();
    materials
        .iter()
        .map(|(_, material)| {
            if !exact_refraction_flag(material.refraction_ready.x, enabled)
                || material.refraction_color != targets.color
                || material.refraction_depth != targets.depth
            {
                return Err(
                    "Native optical material is not bound to actual capture state/targets"
                        .to_owned(),
                );
            }
            Ok(material
                .frame
                .iter()
                .map(UVec4::to_array)
                .collect::<Vec<_>>())
        })
        .collect()
}

fn snapshot(world: &mut World) -> Result<Value, String> {
    let cameras = world.query_filtered::<(Entity, &Camera, &GlobalTransform), (With<Camera3d>, Without<capture::CaptureCamera>)>()
        .iter(world).filter(|(_, c, _)| c.is_active).map(|(e,c,p)| (e,c.clip_from_view().to_cols_array().map(f32::to_bits),p.to_matrix().to_cols_array().map(f32::to_bits),c.physical_viewport_size())).collect::<Vec<_>>();
    let [(camera, projection, pose, size)] = cameras.as_slice() else {
        return Err("Static proof requires one active main camera".into());
    };
    if *size != Some(UVec2::new(1920, 1080)) {
        return Err("Main viewport is not 1920x1080".into());
    }
    let derived = world
        .query_filtered::<(&Camera, &GlobalTransform), With<capture::CaptureCamera>>()
        .iter(world)
        .map(|(c, p)| {
            (
                c.clip_from_view().to_cols_array().map(f32::to_bits),
                p.to_matrix().to_cols_array().map(f32::to_bits),
                c.physical_target_size(),
            )
        })
        .collect::<Vec<_>>();
    let [(derived_projection, derived_pose, derived_size)] = derived.as_slice() else {
        return Err("Static proof requires one derived camera".into());
    };
    if derived_projection != projection
        || derived_pose != pose
        || *derived_size != Some(UVec2::new(960, 540))
    {
        return Err(
            "Actual derived camera projection/pose/target differs from main contract".into(),
        );
    }
    if world
        .get::<bevy::post_process::auto_exposure::AutoExposure>(*camera)
        .is_some()
        || world
            .get::<bevy::anti_alias::taa::TemporalAntiAliasing>(*camera)
            .is_some()
        || world
            .get::<bevy::post_process::motion_blur::MotionBlur>(*camera)
            .is_some()
        || world
            .get::<forge_engine::tidewater_film_grain::TidewaterFilmGrain>(*camera)
            .is_some_and(|g| g.intensity.x != 0.0)
    {
        return Err("Static exact triplet needs isolated profile without exposure adaptation/TAA/motion blur/grain".into());
    }
    let observation = world.resource::<capture::Observation>();
    if observation.error.is_some() {
        return Err(format!("Refraction observation: {:?}", observation.error));
    }
    let enabled = world.resource::<capture::Control>().enabled;
    if observation.size != [960, 540]
        || observation.terrain_parts != 1
        || observation.village_parts == 0
    {
        return Err("Refraction targets/opaque source parts unavailable".into());
    }
    if observation.source_camera.map(f32::to_bits) != *pose
        || observation.capture_camera.map(f32::to_bits) != *pose
    {
        return Err("Refraction source/capture camera differs from main".into());
    }
    let optical = optical_frames(world, enabled)?;
    if optical.is_empty() {
        return Err("Native optical material absent".into());
    }
    let targets = world.resource::<capture::Targets>();
    let maps = world.resource::<forge_engine::tidewater_underwater_lighting::Targets>();
    let gpu = *maps
        .observation
        .lock()
        .map_err(|_| "Light-map observation lock")?;
    if gpu.resolution != 512 || gpu.layers != 2 || gpu.step == 0 || !gpu.time.is_finite() {
        return Err("Actual GPU light-map stamp unavailable".into());
    }
    let ocean = world.resource::<super::ocean::Targets>();
    let clouds = world.get_resource::<super::clouds::Targets>();
    let signature = json!({"main_camera_bits":pose,"main_projection_bits":projection,
        "actual_capture_camera_bits":derived_pose,"capture_projection_bits":derived_projection,
        "source_camera_bits":observation.source_camera.map(f32::to_bits),"capture_camera_bits":observation.capture_camera.map(f32::to_bits),
        "size":observation.size,"terrain_parts":observation.terrain_parts,"village_parts":observation.village_parts,
        "optical_frame_bits":optical,"encoded_fft_step":ocean.completed_steps.load(Ordering::Acquire),
        "cloud_completed_step":clouds.map(|c|c.completed_steps.load(Ordering::Acquire)),
        "gpu_light_map":{"origins_bits":gpu.origins.map(f32::to_bits),"step":gpu.step,"time_bits":gpu.time.to_bits(),"resolution":gpu.resolution,"layers":gpu.layers}});
    Ok(
        json!({"signature":signature,"ready":observation.ready,"enabled":enabled,
        "main_frame":world.resource::<FrameCount>().0,"encoded_frames":targets.encoded_frames.load(Ordering::Acquire),
        "gpu":world.get_resource::<capture::GpuObservation>(),"light_map_encoded_frames":maps.encoded_frames.load(Ordering::Acquire)}),
    )
}

fn gpu_valid(world: &World) -> bool {
    world
        .get_resource::<capture::GpuObservation>()
        .is_some_and(|g| {
            let (Some(color), Some(depth)) = (&g.color, &g.depth) else {
                return false;
            };
            [color, depth].iter().all(|s| {
                s.size == [960, 540]
                    && s.pixels == 518_400
                    && s.covered_pixels > 1000
                    && s.nonfinite_pixels == 0
                    && s.min >= 0.0
                    && s.max <= 1.0
                    && s.min < s.max
                    && s.mean_rgb.iter().all(|x| x.is_finite())
            }) && color.covered_pixels == depth.covered_pixels
                && rgb_channels_are_nonflat(color.mean_rgb)
        })
}

// The raw proof's criterion is exact inequality between the channel means.
// Keep that criterion unchanged when comparing the observed f64 statistics.
#[allow(clippy::float_cmp)]
fn rgb_channels_are_nonflat(mean: [f64; 3]) -> bool {
    mean[0] != mean[1] && mean[1] != mean[2]
}

fn request_raw(world: &mut World, proof: &Proof) {
    let targets = world.resource::<capture::Targets>().clone();
    let requested_frame = world.resource::<FrameCount>().0;
    let phase_index = proof.phase;
    let attempt = proof.retries;
    let phase = ["on", "off", "repeat"][proof.phase];
    for (index, handle, name, pixel_size) in [
        (0, targets.color, "color", 8_usize),
        (1, targets.depth, "depth", 4),
    ] {
        let path = proof.path.with_extension(format!(
            "refraction.{phase}.attempt-{}.{name}.bin",
            proof.retries
        ));
        world.spawn(Readback::texture(handle)).observe(
            move |event: On<ReadbackComplete>,
                  mut proof: ResMut<Proof>,
                  frame: Res<FrameCount>,
                  targets: Res<capture::Targets>,
                  mut commands: Commands| {
                if proof.phase != phase_index || proof.retries != attempt {
                    commands.entity(event.entity).despawn();
                    return;
                }
                let row = 960 * pixel_size;
                let stride = row.div_ceil(256) * 256;
                let checksum = checksum64(&event.data, row, stride);
                let error = if event.data.len() == stride * 540 {
                    std::fs::write(&path, &event.data)
                        .err()
                        .map(|e| e.to_string())
                } else {
                    Some("Unexpected raw texture byte count".to_owned())
                };
                proof.raw[index] = Some(
                    json!({"path":path,"format":if index==0{"RGBA16Float"}else{"Depth32Float"},
                "size":[960,540],"row_bytes":row,"stride_bytes":stride,"bytes":event.data.len(),
                "sha256":forge_core::bytes_sha256(&event.data).to_string(),"error":error,
                "checksum64":checksum,"phase":phase,"attempt":attempt,
                "requested_main_frame":requested_frame,"received_main_frame":frame.0,
                "received_capture_encodings":targets.encoded_frames.load(Ordering::Acquire)}),
                );
                commands.entity(event.entity).despawn();
            },
        );
    }
}

fn pixel_delta(first: &[u8], other: &[u8]) -> Option<Value> {
    if first.len() != other.len() || first.is_empty() || !first.len().is_multiple_of(3) {
        return None;
    }
    let mut sum = 0_u64;
    let mut changed = 0_u64;
    let mut gt2 = 0_u64;
    let mut max = 0_u8;
    for (a, b) in first
        .as_chunks::<3>()
        .0
        .iter()
        .zip(other.as_chunks::<3>().0)
    {
        let d = [
            a[0].abs_diff(b[0]),
            a[1].abs_diff(b[1]),
            a[2].abs_diff(b[2]),
        ];
        sum += d.iter().map(|d| u64::from(*d)).sum::<u64>();
        changed += u64::from(d.iter().any(|d| *d > 0));
        gt2 += u64::from(d.iter().any(|d| *d > 2));
        max = max.max(*d.iter().max()?);
    }
    #[allow(clippy::cast_precision_loss)]
    let mean = sum as f64 / first.len() as f64;
    Some(
        json!({"mean_abs_rgb":mean,"changed_pixels":changed,"pixels_delta_gt_2":gt2,"max_channel_delta":max}),
    )
}

fn save(proof: &Proof, status: &str, error: Option<&str>) -> Result<(), String> {
    let bytes=serde_json::to_vec_pretty(&json!({"schema":"forge.tidewater.native-static-refraction-triplet","status":status,
        "scope":"Static native surface capture, actual HDR/depth readbacks and exact RGB triplet; no live source parity, performance or locomotion verdict",
        "signature":proof.signature,"captures":proof.captures,"error":error,"elapsed_seconds":proof.started.elapsed().as_secs_f64(),
        "original_capture_control":proof.original,"readback_retry_count":proof.retries,
        "readback_mismatches":proof.readback_mismatches})).map_err(|e|e.to_string())?;
    std::fs::write(proof.path.with_extension("refraction.json"), bytes).map_err(|e| e.to_string())
}

fn finish(world: &mut World, proof: &Proof, error: Option<String>) {
    world.resource_mut::<capture::Control>().enabled = proof.original;
    capture::observe_while_held(world, false);
    let saved = save(
        proof,
        if error.is_some() { "failed" } else { "passed" },
        error.as_deref(),
    );
    if error.is_some() || saved.is_err() {
        error!(?error, ?saved, "Native static refraction proof failed");
    }
    println!(
        "TIDEWATER_REFRACTION_PROOF {}",
        json!({"status":if error.is_none() && saved.is_ok(){"passed"}else{"failed"},
        "receipt":proof.path.with_extension("refraction.json"),"captures":proof.captures.len(),"error":error,
        "save_error":saved.as_ref().err(),"elapsed_seconds":proof.started.elapsed().as_secs_f64()})
    );
    world.write_message(if error.is_none() && saved.is_ok() {
        AppExit::Success
    } else {
        AppExit::error()
    });
}

#[cfg(test)]
mod tests {
    use super::{
        Proof, checksum64, pixel_delta, queue_readback_pair, rebase_readbacks, screenshot_observer,
        take_retry,
    };
    use bevy::{
        diagnostic::FrameCount,
        prelude::*,
        render::{gpu_readback::Readback, view::screenshot::Screenshot},
    };
    use forge_engine::tidewater_refraction as capture;
    use serde_json::json;
    use std::{
        path::PathBuf,
        sync::{Arc, atomic::AtomicU64},
        time::Instant,
    };

    #[test]
    fn checksum_ignores_padding_and_rejects_incomplete_rows() {
        assert_eq!(checksum64(b"hello", 5, 5), Some(0xa430_d846_80aa_bd0b));
        let mut first = vec![0; 512];
        first[..3].copy_from_slice(b"abc");
        first[256..259].copy_from_slice(b"def");
        let mut padded = first.clone();
        padded[3..256].fill(255);
        padded[259..].fill(255);
        assert_eq!(checksum64(&first, 3, 256), checksum64(&padded, 3, 256));
        padded[256] ^= 1;
        assert_ne!(checksum64(&first, 3, 256), checksum64(&padded, 3, 256));
        assert!(checksum64(&first[..511], 3, 256).is_none());
        assert!(checksum64(b"a", 1, 0).is_none());
        assert!(checksum64(&[], 0, 0).is_none());
    }

    #[test]
    fn stability_retries_are_bounded_to_two_without_resetting_the_counter() {
        let mut retries = 0;
        assert!(take_retry(&mut retries));
        assert!(take_retry(&mut retries));
        assert!(!take_retry(&mut retries));
        assert_eq!(retries, 2);
    }

    #[test]
    fn actual_shared_and_raw_requests_queue_together_after_forty_frames() {
        let mut world = World::new();
        world.insert_resource(FrameCount(100));
        world.insert_resource(capture::Targets {
            color: Handle::default(),
            depth: Handle::default(),
            encoded_frames: Arc::new(AtomicU64::new(0)),
        });
        world.insert_resource(capture::Observation {
            size: [960, 540],
            ready: true,
            ..default()
        });
        let mut proof = Proof {
            path: PathBuf::from("unused-refraction-headless.png"),
            started: Instant::now(),
            phase: 2,
            frame: 0,
            baseline: 0,
            off_counter: 0,
            off_stable: 0,
            original: true,
            signature: None,
            captures: Vec::new(),
            pixels: None,
            waiting: false,
            raw_requested: false,
            raw: [None, None],
            retries: 0,
            readback_mismatches: Vec::new(),
        };
        // Use the real reset and real shared observer, including repeat's early return.
        rebase_readbacks(&mut world, &mut proof, 100, 0);
        world.resource_mut::<FrameCount>().0 = 139;
        queue_readback_pair(&mut world, &mut proof, 139);
        assert_eq!(
            world
                .query_filtered::<Entity, With<Readback>>()
                .iter(&world)
                .count(),
            0
        );
        world.resource_mut::<FrameCount>().0 = 140;
        queue_readback_pair(&mut world, &mut proof, 140);
        world.flush();
        assert_eq!(
            world
                .query_filtered::<Entity, With<Readback>>()
                .iter(&world)
                .count(),
            4
        );
        assert!(proof.raw_requested);
        world.resource_mut::<FrameCount>().0 = 141;
        queue_readback_pair(&mut world, &mut proof, 141);
        assert_eq!(
            world
                .query_filtered::<Entity, With<Readback>>()
                .iter(&world)
                .count(),
            4
        );
    }

    #[test]
    fn screenshot_observer_registers_with_a_headless_world() {
        let mut world = World::new();
        let entity = world
            .spawn(Screenshot::primary_window())
            .observe(screenshot_observer(
                PathBuf::from("unused-refraction-headless.png"),
                "on",
                json!({}),
                json!({}),
                json!({}),
            ))
            .id();
        // This executes the real observer registration hook that rejected the
        // previous exclusive callback. No event, file write or GPU is needed.
        world.flush();
        assert!(world.get::<Screenshot>(entity).is_some());
    }

    #[test]
    fn pixels_distinguish_two_from_three_and_exact_repeat() {
        let first = [0, 0, 0, 0, 0, 0];
        let changed = pixel_delta(&first, &[2, 0, 0, 0, 3, 0]).unwrap();
        assert_eq!(changed["changed_pixels"], 2);
        assert_eq!(changed["pixels_delta_gt_2"], 1);
        assert_eq!(changed["max_channel_delta"], 3);
        assert_eq!(changed["mean_abs_rgb"].as_f64(), Some(5.0 / 6.0));
        assert_eq!(pixel_delta(&first, &first).unwrap()["max_channel_delta"], 0);
    }
    #[test]
    fn malformed_pixels_are_rejected() {
        assert!(pixel_delta(&[], &[]).is_none());
        assert!(pixel_delta(&[0, 0, 0], &[0, 0]).is_none());
        assert!(pixel_delta(&[0, 0], &[0, 0]).is_none());
    }
}
