//! Opt-in native adapter for real auxiliary and source-resolve GPU observations.
use bevy::prelude::*;
use bevy::render::view::screenshot::{Screenshot, save_to_disk};
use forge_core::bytes_sha256;
use forge_engine::tidewater_temporal as temporal;
use std::{
    path::PathBuf,
    time::{Duration, Instant},
};

#[derive(Resource)]
// These switches describe independent proof lanes; keeping them explicit makes
// the receipt state auditable instead of hiding combinations in a bit mask.
#[allow(clippy::struct_excessive_bools)]
pub struct Proof {
    source_resolve: bool,
    resolve_readback: bool,
    trace_readback: bool,
    base_readback: bool,
    output: PathBuf,
    planes: PathBuf,
    screenshot: PathBuf,
    started: Instant,
    last_progress: Instant,
    request: Option<u64>,
    captures: Vec<serde_json::Value>,
    resolve_captures: Vec<serde_json::Value>,
    resolve_planes: PathBuf,
    resolve_request: Option<u64>,
    trace_request: Option<u64>,
    trace_captures: Vec<serde_json::Value>,
    trace_planes: PathBuf,
    base_request: Option<u64>,
    base_captures: Vec<serde_json::Value>,
    base_planes: PathBuf,
    seed_pending: bool,
    seed_reset_done: bool,
    seed_error: Option<String>,
    screenshot_requested: bool,
}

// Registration order is part of this proof adapter: each optional readback
// lane is enabled only after its owning render stage has been installed.
#[allow(clippy::too_many_lines)]
pub fn install(app: &mut App) -> Result<(), String> {
    use temporal::{phase::MaterialRole, shaders::Family};
    temporal::register_material::<super::terrain_lod::LodMaterial>(
        app,
        Family::TerrainLod,
        MaterialRole::Opaque,
    )?;
    temporal::register_material::<super::PlantMaterial>(app, Family::Plant, MaterialRole::Opaque)?;
    temporal::register_material::<super::village::VillageMaterial>(
        app,
        Family::Village,
        MaterialRole::Opaque,
    )?;
    temporal::register_material::<super::water_optics::WaterMaterial>(
        app,
        Family::Ocean,
        MaterialRole::Water,
    )?;
    temporal::register_material::<forge_engine::tidewater_stall::FishStandMaterial>(
        app,
        Family::FishStand,
        MaterialRole::Opaque,
    )?;
    temporal::register_material::<super::breaker_lip::LipMaterial>(
        app,
        Family::BreakerLip,
        MaterialRole::Transparent,
    )?;
    temporal::register_material::<super::spray_sprites::SprayMaterial>(
        app,
        Family::Spray,
        MaterialRole::Transparent,
    )?;
    temporal::phase::register_standard_opaque(app)?;
    temporal::phase::register_sky_material::<super::sky::SkyMaterial>(app)?;
    let source_resolve = std::env::var("FORGE_TIDEWATER_TEMPORAL_RESOLVE").as_deref() == Ok("1");
    if source_resolve {
        if !app.is_plugin_added::<forge_engine::tidewater_underwater::TidewaterUnderwaterPlugin>() {
            // Static graphics previously installed grading only. Reuse the
            // playable camera's real medium, surface query, optics and caustics.
            // A missing pass is not a neutral contribution to the HDR chain.
            if !app
                .world()
                .contains_resource::<super::spray::water_queries::Targets>()
            {
                let output = app
                    .world()
                    .resource::<super::Probe>()
                    .screenshot
                    .with_extension("water-query.json");
                super::spray::enable_water_queries(app, output)?;
            }
            let underwater =
                super::walk::underwater_plugin(app).map_err(|error| error.to_string())?;
            app.add_plugins(underwater);
        }
        temporal::resolve::register(app)?;
    }
    let resolve_readback =
        std::env::var("FORGE_TIDEWATER_TEMPORAL_RESOLVE_READBACK").as_deref() == Ok("1");
    if resolve_readback {
        if !source_resolve {
            return Err("Resolve GPU readback requires the source resolve owner".into());
        }
        temporal::resolve_readback::register(app)?;
    }
    temporal::observation::register(app)?;
    let trace_readback =
        std::env::var("FORGE_TIDEWATER_TEMPORAL_RESOLVE_TRACE").as_deref() == Ok("1");
    if trace_readback {
        if !resolve_readback {
            return Err("Instrumented trace requires the real seven-plane source readback".into());
        }
        temporal::trace_readback::register(app)?;
    }
    let base_readback =
        std::env::var("FORGE_TIDEWATER_TEMPORAL_AUX_BASE_READBACK").as_deref() == Ok("1");
    if base_readback {
        temporal::register_base_readback(app)?;
    }
    let screenshot = app.world().resource::<super::Probe>().screenshot.clone();
    let output = screenshot.with_extension("aux.json");
    let planes = screenshot.with_extension("aux-planes");
    let resolve_planes = screenshot.with_extension("resolve-planes");
    let base_planes = screenshot.with_extension("base-planes");
    let trace_planes = screenshot.with_extension("trace-planes");
    if output.exists()
        || screenshot.exists()
        || planes.exists()
        || resolve_planes.exists()
        || base_planes.exists()
        || trace_planes.exists()
    {
        return Err("Temporal proof output already exists".into());
    }
    std::fs::create_dir(&planes).map_err(|error| error.to_string())?;
    if resolve_readback {
        std::fs::create_dir(&resolve_planes).map_err(|error| error.to_string())?;
    }
    if base_readback {
        std::fs::create_dir(&base_planes).map_err(|error| error.to_string())?;
    }
    if trace_readback {
        std::fs::create_dir(&trace_planes).map_err(|error| error.to_string())?;
    }
    app.insert_resource(Proof {
        source_resolve,
        resolve_readback,
        trace_readback,
        base_readback,
        output,
        planes,
        screenshot,
        started: Instant::now(),
        last_progress: Instant::now(),
        request: None,
        captures: Vec::new(),
        resolve_captures: Vec::new(),
        resolve_planes,
        resolve_request: None,
        trace_request: None,
        trace_captures: Vec::new(),
        trace_planes,
        base_request: None,
        base_captures: Vec::new(),
        base_planes,
        seed_pending: false,
        seed_reset_done: false,
        seed_error: None,
        screenshot_requested: false,
    })
    .add_systems(
        PostUpdate,
        select_camera
            .before(temporal::FrameSystems::Prepare)
            .before(forge_engine::tidewater_underwater::TidewaterUnderwaterUpdateSet),
    )
    .add_systems(
        PostUpdate,
        update_selected_water_query
            .after(select_camera)
            .after(bevy::transform::TransformSystems::Propagate)
            .before(forge_engine::tidewater_underwater::TidewaterUnderwaterUpdateSet),
    )
    .add_systems(
        PostUpdate,
        reset_after_acknowledgement
            .after(temporal::FrameSystems::Acknowledge)
            .before(temporal::FrameSystems::Prepare),
    )
    .add_systems(
        PostUpdate,
        arm_reset_seed
            .after(temporal::FrameSystems::Prepare)
            .before(temporal::FrameSystems::Jitter),
    )
    .add_systems(Update, advance);
    Ok(())
}

fn select_camera(world: &mut World) {
    let mut cameras = world.query_filtered::<(Entity, &Camera), (
        With<Camera3d>,
        Without<forge_engine::tidewater_refraction::CaptureCamera>,
    )>();
    let mut active = cameras.iter(world).filter(|(_, camera)| camera.is_active);
    let Some((entity, _)) = active.next() else {
        return;
    };
    if active.next().is_some() {
        return;
    }
    // The native probe's submitted scene contains terrain, plants, village,
    // stall/fish, sky and ocean. Its setup has no boat or hull draw path.
    world
        .entity_mut(entity)
        .insert(temporal::render::HullRequirement::NoHullsInScene);
    if let Err(error) = temporal::select_camera(world, Some(entity)) {
        warn!("Temporal auxiliary camera selection: {error}");
        return;
    }
    if world.resource::<Proof>().source_resolve {
        world
            .entity_mut(entity)
            .insert(forge_engine::tidewater_underwater::TidewaterUnderwaterCamera);
        let result = world
            .resource_mut::<temporal::frame::Frames>()
            .set_encoding_owner(temporal::frame::EncodingOwner::Resolve);
        if let Err(error) = result {
            warn!("Temporal resolve owner: {error}");
            return;
        }
        if let Err(error) = temporal::resolve::activate(world, Some(entity)) {
            warn!("Temporal resolve camera selection: {error}");
        }
    }
}

fn update_selected_water_query(world: &World) {
    if !world.resource::<Proof>().source_resolve {
        return;
    }
    let Some(view) = world.resource::<temporal::frame::Frames>().selected_view() else {
        return;
    };
    let Some(transform) = world.get::<GlobalTransform>(view) else {
        return;
    };
    let targets = world.resource::<super::spray::water_queries::Targets>();
    if targets
        .bootstrap_ready
        .load(std::sync::atomic::Ordering::Acquire)
    {
        let position = transform.translation();
        if let Err(error) = targets.queries.set_point(0, position.x, position.z) {
            error!("Selected temporal camera water query: {error}");
        }
    }
}

// This proof loop validates the paired readback lanes before advancing the
// receipt state, so its ordered branches stay together.
#[allow(clippy::too_many_lines)]
fn advance(world: &mut World) {
    let result = world.resource_scope(|world, mut proof: Mut<Proof>| -> Result<(), String> {
        if let Some(error) = &proof.seed_error {
            return Err(error.clone());
        }
        if proof.started.elapsed() > Duration::from_secs(180) {
            return Err("Temporal auxiliary GPU proof exceeded 180 seconds".into());
        }
        let frames = world.resource::<temporal::frame::Frames>();
        let Some(view) = frames.selected_view() else {
            return Ok(());
        };
        let Some(encoded) = world
            .resource::<temporal::render::AuxObservations>()
            .snapshot(view)
        else {
            return Ok(());
        };
        if proof.last_progress.elapsed() >= Duration::from_secs(5) {
            eprintln!(
                "Temporal auxiliary: committed={}, resolved={}, visible={}, queued={}, pending={}, unsupported={}, draws={}, errors={:?}, shader_errors={:?}",
                frames.committed_aux,
                frames.committed_resolve,
                encoded.coverage.visible,
                encoded.coverage.queued,
                encoded.coverage.pending,
                encoded.coverage.unsupported,
                encoded.mesh_draws,
                encoded.errors,
                world.resource::<temporal::Derivations>().errors(),
            );
            if proof.source_resolve {
                eprintln!(
                    "Temporal resolve: {:?}; HDR: {:?}",
                    world.resource::<temporal::resolve::ResolveObservations>().snapshot(view),
                    world.resource::<temporal::hdr::HdrObservations>().snapshot(view),
                );
            }
            proof.last_progress = Instant::now();
        }
        if proof.captures.len() == 2 {
            if proof.base_readback && proof.base_captures.len() != 2 {
                return Err("Base proof lacks its two paired six-plane snapshots".into());
            }
            if proof.resolve_readback && proof.resolve_captures.len() == 2 {
                let Some(snapshot) = completed_resolve(world, &proof)? else {
                    return Ok(());
                };
                if !snapshot.metadata.history_seeded || !snapshot.metadata.previous_depth_cleared {
                    return Err("Reset capture did not observe the actual history seed and depth clear".into());
                }
                if proof.trace_readback {
                    let Some(trace) = completed_trace(world, &proof)? else {
                        return Ok(());
                    };
                    let capture = write_trace_snapshot(&proof, "history-reset-seed", &trace, &snapshot)?;
                    proof.trace_captures.push(capture);
                    proof.trace_request = None;
                }
                let capture = write_resolve_snapshot(
                    &proof,
                    "history-reset-seed",
                    &snapshot,
                )?;
                proof.resolve_captures.push(capture);
                proof.resolve_request = None;
            }
            if proof.resolve_readback && proof.resolve_captures.len() != 3 {
                return Err("Resolve proof lacks its three seven-plane snapshots".into());
            }
            if proof.trace_readback && proof.trace_captures.len() != 3 {
                return Err("Instrumented proof lacks its three paired two-callback snapshots".into());
            }
            if !proof.screenshot_requested {
                let path = proof.screenshot.clone();
                world
                    .spawn(Screenshot::primary_window())
                    .observe(save_to_disk(path));
                proof.screenshot_requested = true;
            }
            if proof.screenshot.is_file() {
                write_receipt(world, &proof, "gpu-readbacks-captured", None)?;
                world.write_message(AppExit::Success);
            }
            return Ok(());
        }
        if proof.request.is_none() {
            let ready = if proof.source_resolve {
                frames.committed_resolve >= 40 && world.get_resource::<temporal::resolve::ResolveObservations>()
                    .and_then(|observations| observations.snapshot(view))
                    .is_some_and(|resolved| resolved.complete() && resolved.tag == encoded.tag)
            } else {
                frames.committed_aux >= 40 && encoded.auxiliary_acknowledged
            };
            if !ready || !encoded.complete() {
                return Ok(());
            }
            if proof.trace_readback && !world.resource::<temporal::trace_readback::Observation>()
                .pipeline_ready(view).map_err(|error| error.to_string())?
            {
                return Ok(());
            }
            proof.request = Some(
                temporal::observation::request(world, view).map_err(|error| error.to_string())?,
            );
            if proof.base_readback {
                proof.base_request = Some(request_base(world, view)?);
            }
            if proof.resolve_readback {
                proof.resolve_request = Some(request_resolve(world, view)?);
            }
            if proof.trace_readback {
                proof.trace_request = Some(request_trace(world, resolve_scope(world, view)?, "static")?);
            }
        }
        let status = world
            .resource::<temporal::observation::Observation>()
            .status()
            .map_err(|error| error.to_string())?;
        match status {
            temporal::observation::Status::Complete(snapshot) => {
                if Some(snapshot.request_id) != proof.request {
                    return Err("Unexpected completed GPU request".into());
                }
                let label = if proof.captures.is_empty() {
                    "static"
                } else {
                    "camera-moved"
                };
                let base = if proof.base_readback {
                    let Some(base) = completed_base(world, &proof)? else {
                        return Ok(());
                    };
                    if base.metadata.tag() != snapshot.tag
                        || base.metadata.generation != snapshot.generation
                        || base.metadata.view_packet.packet != temporal::frame::Packet::from_frame(&snapshot.frame)
                    {
                        return Err("Base and final auxiliary callbacks belong to different images".into());
                    }
                    Some(base)
                } else { None };
                if proof.resolve_readback {
                    let Some(source) = completed_resolve(world, &proof)? else {
                        return Ok(());
                    };
                    if source.metadata.tag != snapshot.tag
                        || source.metadata.auxiliary_generation != snapshot.generation
                        || source.metadata.packet != temporal::frame::Packet::from_frame(&snapshot.frame)
                    {
                        return Err("Source resolve and auxiliary callbacks belong to different images".into());
                    }
                    if proof.trace_readback {
                        let Some(trace) = completed_trace(world, &proof)? else {
                            return Ok(());
                        };
                        let capture = write_trace_snapshot(&proof, label, &trace, &source)?;
                        proof.trace_captures.push(capture);
                        proof.trace_request = None;
                    }
                    let capture = write_resolve_snapshot(&proof, label, &source)?;
                    proof.resolve_captures.push(capture);
                    proof.resolve_request = None;
                }
                if let Some(base) = base {
                    let capture = write_base_snapshot(&proof, label, &base)?;
                    proof.base_captures.push(capture);
                    proof.base_request = None;
                }
                let capture = write_snapshot(&proof, label, &snapshot)?;
                proof.captures.push(capture);
                proof.request = None;
                if proof.captures.len() == 1 {
                    // Request this very next encoded image: waiting for a later
                    // image after the camera move would lose its velocity.
                    world
                        .get_mut::<Transform>(view)
                        .ok_or("Main camera transform absent")?
                        .translation += Vec3::new(0.5, 0.0, 0.125);
                    proof.request = Some(
                        temporal::observation::request(world, view)
                            .map_err(|error| error.to_string())?,
                    );
                    if proof.base_readback {
                        proof.base_request = Some(request_base(world, view)?);
                    }
                    if proof.resolve_readback {
                        proof.resolve_request = Some(request_resolve(world, view)?);
                    }
                    if proof.trace_readback {
                        proof.trace_request = Some(request_trace(world, resolve_scope(world, view)?, "camera-moved")?);
                    }
                } else if proof.resolve_readback {
                    // Observe a real seed following an explicit history reset.
                    // Arm after preparation, before this epoch's first render;
                    // waiting until the next Update would miss the seed draw.
                    proof.seed_pending = true;
                    proof.seed_reset_done = false;
                }
            }
            temporal::observation::Status::Failed(failure) => {
                return Err(format!(
                    "GPU request {}: {}",
                    failure.request_id, failure.error
                ));
            }
            temporal::observation::Status::Idle => {
                return Err("Requested GPU observation unexpectedly idle".into());
            }
            temporal::observation::Status::Pending { .. } => {}
        }
        Ok(())
    });
    if let Err(error) = result {
        error!("{error}");
        let _ = world.resource_scope(|world, proof: Mut<Proof>| {
            write_receipt(world, &proof, "failed", Some(&error))
        });
        world.write_message(AppExit::error());
    }
}

fn request_base(world: &World, view: Entity) -> Result<u64, String> {
    let encoded = world
        .resource::<temporal::render::AuxObservations>()
        .snapshot(view)
        .ok_or("Missing base auxiliary baseline")?;
    let packet = world
        .get::<temporal::frame::ViewPacket>(view)
        .ok_or("Missing base prepared packet")?;
    if !encoded.complete() {
        return Err("Base auxiliary baseline is incomplete".into());
    }
    world
        .resource::<temporal::base_readback::Observation>()
        .request(temporal::base_readback::Scope {
            view,
            minimum_serial: encoded
                .tag
                .serial
                .checked_add(1)
                .ok_or("Base serial exhausted")?,
            history_epoch: packet.frame.history_epoch,
            expected_generation: Some(encoded.generation),
            mode: temporal::base_readback::Mode::BaseAndFinal,
        })
        .map_err(|error| error.to_string())
}

fn completed_base(
    world: &World,
    proof: &Proof,
) -> Result<Option<temporal::base_readback::Snapshot>, String> {
    let request = proof.base_request.ok_or("Base request absent")?;
    let observations = world.resource::<temporal::base_readback::Observation>();
    match observations.status().map_err(|error| error.to_string())? {
        temporal::base_readback::Status::Ready { request_id } if request_id == request => {
            let snapshot = observations
                .snapshot()
                .map_err(|error| error.to_string())?
                .ok_or("Ready base snapshot absent")?;
            if snapshot.request_id != request || !snapshot.complete() {
                return Err("Base snapshot identity or six callbacks incomplete".into());
            }
            Ok(Some(snapshot))
        }
        temporal::base_readback::Status::Pending { request_id, .. } if request_id == request => {
            Ok(None)
        }
        status => Err(format!("Unexpected base readback status: {status:?}")),
    }
}

fn write_base_snapshot(
    proof: &Proof,
    label: &str,
    snapshot: &temporal::base_readback::Snapshot,
) -> Result<serde_json::Value, String> {
    let directory = proof.base_planes.join(label);
    snapshot
        .write_new_directory(&directory)
        .map_err(|error| error.to_string())?;
    let mut planes = Vec::new();
    for plane in &snapshot.planes {
        let bytes = plane.unpadded_bytes().map_err(|error| error.to_string())?;
        planes.push(serde_json::json!({
            "role":plane.role.name(),"format":format!("{:?}",plane.format),
            "size":plane.size.to_array(),"texture_id":plane.texture_id,
            "path":directory.join(format!("{}.bin",plane.role.name())),
            "bytes":bytes.len(),"sha256":bytes_sha256(&bytes),
            "padded_path":directory.join(format!("{}.padded.bin",plane.role.name())),
            "padded_bytes":plane.bytes.len(),"padded_sha256":bytes_sha256(&plane.bytes),
            "row_bytes":plane.row_bytes,"bytes_per_row":plane.bytes_per_row,
        }));
    }
    let metadata = &snapshot.metadata;
    let packet = metadata
        .uniform_bytes()
        .map_err(|error| error.to_string())?;
    let b = &metadata.boundary;
    let f = &snapshot.final_encoding;
    Ok(serde_json::json!({
        "label":label,"request_id":snapshot.request_id,"mode":format!("{:?}",snapshot.mode),
        "complete":snapshot.complete(),
        "tag":{"view_bits":metadata.tag().view.to_bits().to_string(),"serial":metadata.tag().serial},
        "history_epoch":metadata.history_epoch(),"generation":metadata.generation,
        "input_size":metadata.input_size.to_array(),"output_size":metadata.output_size.to_array(),
        "packet":{"path":directory.join("packet336.bin"),"bytes":packet.len(),"sha256":bytes_sha256(&packet)},
        "boundary":{"opaque":b.opaque,"water":b.water,"transparent":b.transparent,"queued":b.queued,
            "base_mesh_draws":b.base_mesh_draws,"sky_draws":b.sky_draws,"hull_draws":b.hull_draws,
            "hull_contract_satisfied":b.hull_contract_satisfied},
        "final_encoding":{"complete":f.complete(),"tag":{"view_bits":f.tag.view.to_bits().to_string(),"serial":f.tag.serial},
            "generation":f.generation,"visible":f.coverage.visible,"queued":f.coverage.queued,"pending":f.coverage.pending,"unsupported":f.coverage.unsupported,
            "opaque":f.coverage.opaque,"water":f.coverage.water,"transparent":f.coverage.transparent,
            "mesh_draws":f.mesh_draws,"sky_draws":f.sky_draws,"hull_draws":f.hull_draws,
            "hull_contract_satisfied":f.hull_contract_satisfied,"errors":f.errors},
        "completion_path":directory.join("readback-complete.txt"),"planes":planes,
    }))
}

fn resolve_scope(world: &World, view: Entity) -> Result<temporal::resolve_readback::Scope, String> {
    let encoded = world
        .resource::<temporal::resolve::ResolveObservations>()
        .snapshot(view)
        .ok_or("Missing source resolve baseline")?;
    if !encoded.complete() {
        return Err("Source resolve baseline is incomplete".into());
    }
    Ok(temporal::resolve_readback::Scope {
        view,
        minimum_serial: encoded
            .tag
            .serial
            .checked_add(1)
            .ok_or("Resolve serial exhausted")?,
        history_epoch: encoded.history_epoch,
        expected_resolve_generation: Some(encoded.generation),
        expected_auxiliary_generation: Some(encoded.auxiliary_generation),
    })
}

fn request_resolve(world: &World, view: Entity) -> Result<u64, String> {
    world
        .resource::<temporal::resolve_readback::Observation>()
        .request(resolve_scope(world, view)?)
        .map_err(|error| error.to_string())
}

fn arm_reset_seed(world: &mut World) {
    world.resource_scope(|world, mut proof: Mut<Proof>| {
        if !proof.seed_pending
            || !proof.seed_reset_done
            || proof.resolve_request.is_some()
            || proof.seed_error.is_some()
        {
            return;
        }
        let result = (|| -> Result<u64, String> {
            let view = world
                .resource::<temporal::frame::Frames>()
                .selected_view()
                .ok_or("Reset seed camera absent")?;
            let packet = world
                .get::<temporal::frame::ViewPacket>(view)
                .ok_or("Reset seed prepared packet absent")?;
            if !packet.frame.needs_history_seed {
                return Err("Reset seed request missed the first prepared image".into());
            }
            let scope = temporal::resolve_readback::Scope {
                view,
                minimum_serial: packet.tag.serial,
                history_epoch: packet.frame.history_epoch,
                expected_resolve_generation: None,
                expected_auxiliary_generation: None,
            };
            if proof.trace_readback {
                proof.trace_request = Some(request_trace(world, scope, "history-reset-seed")?);
            }
            world
                .resource::<temporal::resolve_readback::Observation>()
                .request(scope)
                .map_err(|error| error.to_string())
        })();
        match result {
            Ok(request) => {
                proof.resolve_request = Some(request);
                proof.seed_pending = false;
            }
            Err(error) => proof.seed_error = Some(error),
        }
    });
}

fn reset_after_acknowledgement(world: &mut World) {
    world.resource_scope(|world, mut proof: Mut<Proof>| {
        if !proof.seed_pending || proof.seed_reset_done || proof.seed_error.is_some() {
            return;
        }
        let result = (|| -> Result<(), String> {
            let mut frames = world.resource_mut::<temporal::frame::Frames>();
            let view = frames.selected_view().ok_or("Reset seed camera absent")?;
            frames.reset(view).map_err(|error| error.to_string())
        })();
        match result {
            Ok(()) => proof.seed_reset_done = true,
            Err(error) => proof.seed_error = Some(error),
        }
    });
}

fn request_trace(
    world: &World,
    scope: temporal::resolve_readback::Scope,
    label: &str,
) -> Result<u64, String> {
    let observations = world.resource::<temporal::trace_readback::Observation>();
    if !observations
        .pipeline_ready(scope.view)
        .map_err(|error| error.to_string())?
        || observations
            .outstanding_bytes()
            .map_err(|error| error.to_string())?
            != 0
    {
        return Err("Instrumented pipeline or previous diagnostic callbacks are not ready".into());
    }
    let case_id = match label {
        "static" => 0,
        "camera-moved" => 1,
        "history-reset-seed" => 2,
        _ => return Err("Unknown instrumented case label".into()),
    };
    // Historical sensitive pixels and all three tied depth taps. Fixed sites
    // are diagnostics, not a region of interest for either whole-image audit.
    let sites = [
        (867, 66),
        (893, 20),
        (1743, 457),
        (935, 733),
        (934, 734),
        (935, 734),
        (936, 734),
        (290, 466),
    ]
    .into_iter()
    .map(|(x, y)| temporal::trace_readback::Site { x, y, case_id })
    .collect();
    observations
        .request(scope, sites)
        .map_err(|error| error.to_string())
}

fn completed_trace(
    world: &World,
    proof: &Proof,
) -> Result<Option<temporal::trace_readback::Snapshot>, String> {
    let request = proof.trace_request.ok_or("Instrumented request absent")?;
    let observations = world.resource::<temporal::trace_readback::Observation>();
    match observations.status().map_err(|error| error.to_string())? {
        temporal::trace_readback::Status::Ready { request_id } if request_id == request => {
            let snapshot = observations
                .snapshot()
                .map_err(|error| error.to_string())?
                .ok_or("Ready instrumented snapshot absent")?;
            if snapshot.request_id != request
                || !snapshot.complete()
                || observations
                    .outstanding_bytes()
                    .map_err(|error| error.to_string())?
                    != 0
            {
                return Err("Instrumented snapshot identity or two callbacks incomplete".into());
            }
            Ok(Some(snapshot))
        }
        temporal::trace_readback::Status::Pending { request_id, .. } if request_id == request => {
            Ok(None)
        }
        status => Err(format!(
            "Unexpected instrumented readback status: {status:?}"
        )),
    }
}

fn write_trace_snapshot(
    proof: &Proof,
    label: &str,
    trace: &temporal::trace_readback::Snapshot,
    original: &temporal::resolve_readback::Snapshot,
) -> Result<serde_json::Value, String> {
    let comparison = trace
        .compare_original(original)
        .map_err(|error| error.to_string())?;
    let directory = proof.trace_planes.join(label);
    trace
        .write_new_directory(&directory)
        .map_err(|error| error.to_string())?;
    let metadata = &trace.metadata;
    let (packet, params) = metadata
        .uniform_bytes()
        .map_err(|error| error.to_string())?;
    let compact = trace
        .scratch
        .unpadded_bytes()
        .map_err(|error| error.to_string())?;
    let original_texture_roles = [
        temporal::resolve_readback::Role::Beauty,
        temporal::resolve_readback::Role::InputHistory,
        temporal::resolve_readback::Role::PreviousSourceDepth,
        temporal::resolve_readback::Role::CurrentDepth,
        temporal::resolve_readback::Role::Velocity,
        temporal::resolve_readback::Role::Mask,
        temporal::resolve_readback::Role::Output,
    ]
    .map(temporal::resolve_readback::Role::name);
    Ok(serde_json::json!({
        "label":label,"request_id":trace.request_id,"complete":trace.complete(),
        "diagnostic_draw_encoded":trace.diagnostic_draw_encoded,
        "tag":{"view_bits":metadata.tag.view.to_bits().to_string(),"serial":metadata.tag.serial},
        "history_epoch":metadata.history_epoch,"resolve_generation":metadata.resolve_generation,
        "auxiliary_generation":metadata.auxiliary_generation,
        "input_size":metadata.input_size.to_array(),"output_size":metadata.output_size.to_array(),
        "history_seeded":metadata.history_seeded,"previous_depth_cleared":metadata.previous_depth_cleared,
        "frames_encoded_before":metadata.frames_encoded_before,
        "packet":{"path":directory.join("packet336.bin"),"bytes":packet.len(),"sha256":bytes_sha256(&packet)},
        "params":{"path":directory.join("params32.bin"),"bytes":params.len(),"sha256":bytes_sha256(&params)},
        "selection":{"path":directory.join("selection144.bin"),"bytes":trace.selection_bytes.len(),"sha256":bytes_sha256(&trace.selection_bytes)},
        "sites":trace.sites.iter().map(|site|serde_json::json!({"x":site.x,"y":site.y,"case_id":site.case_id})).collect::<Vec<_>>(),
        "original_texture_ids":trace.original_texture_ids,
        "original_texture_roles":original_texture_roles,
        "trace":{"path":directory.join("trace-float32.bin"),"bytes":trace.trace_bytes.len(),"sha256":bytes_sha256(&trace.trace_bytes)},
        "scratch":{"path":directory.join("scratch-rgba16f.bin"),"bytes":compact.len(),"sha256":bytes_sha256(&compact),
            "padded_path":directory.join("scratch-rgba16f.padded.bin"),"padded_bytes":trace.scratch.bytes.len(),"padded_sha256":bytes_sha256(&trace.scratch.bytes),
            "format":format!("{:?}",trace.scratch.format),"size":trace.scratch.size.to_array(),"texture_id":trace.scratch.texture_id,
            "row_bytes":trace.scratch.row_bytes,"bytes_per_row":trace.scratch.bytes_per_row},
        "comparison":{"byte_exact":comparison.byte_exact,"compared_bytes":comparison.compared_bytes,
            "mismatching_bytes":comparison.mismatching_bytes,"mismatching_pixels":comparison.mismatching_pixels,
            "scope":"Every output byte; equality does not prove original float32 instructions or intermediate identity"},
        "completion_path":directory.join("readback-complete.txt"),
    }))
}

fn completed_resolve(
    world: &World,
    proof: &Proof,
) -> Result<Option<temporal::resolve_readback::Snapshot>, String> {
    let Some(request) = proof.resolve_request else {
        return Ok(None);
    };
    let observations = world.resource::<temporal::resolve_readback::Observation>();
    match observations.status().map_err(|error| error.to_string())? {
        temporal::resolve_readback::Status::Ready { request_id } if request_id == request => {
            let snapshot = observations
                .snapshot()
                .map_err(|error| error.to_string())?
                .ok_or("Ready resolve snapshot absent")?;
            if snapshot.request_id != request || !snapshot.complete() {
                return Err("Resolve snapshot identity or seven callbacks incomplete".into());
            }
            Ok(Some(snapshot))
        }
        temporal::resolve_readback::Status::Pending { request_id, .. } if request_id == request => {
            Ok(None)
        }
        status => Err(format!("Unexpected resolve readback status: {status:?}")),
    }
}

fn write_resolve_snapshot(
    proof: &Proof,
    label: &str,
    snapshot: &temporal::resolve_readback::Snapshot,
) -> Result<serde_json::Value, String> {
    let directory = proof.resolve_planes.join(label);
    snapshot
        .write_new_directory(&directory)
        .map_err(|error| error.to_string())?;
    let mut planes = Vec::new();
    for plane in &snapshot.planes {
        let bytes = plane.unpadded_bytes().map_err(|error| error.to_string())?;
        planes.push(serde_json::json!({
            "role":plane.role.name(), "format":format!("{:?}",plane.format),
            "size":plane.size.to_array(), "texture_id":plane.texture_id,
            "path":directory.join(format!("{}.bin",plane.role.name())),
            "bytes":bytes.len(), "sha256":bytes_sha256(&bytes),
            "padded_path":directory.join(format!("{}.padded.bin",plane.role.name())),
            "padded_bytes":plane.bytes.len(), "padded_sha256":bytes_sha256(&plane.bytes),
            "row_bytes":plane.row_bytes, "bytes_per_row":plane.bytes_per_row,
        }));
    }
    let (packet, params) = snapshot
        .metadata
        .uniform_bytes()
        .map_err(|error| error.to_string())?;
    let metadata = &snapshot.metadata;
    Ok(serde_json::json!({
        "label":label,"request_id":snapshot.request_id,"paired_draw":snapshot.resolve_draw_encoded,
        "tag":{"view_bits":metadata.tag.view.to_bits().to_string(),"serial":metadata.tag.serial},
        "history_epoch":metadata.history_epoch,"resolve_generation":metadata.resolve_generation,
        "auxiliary_generation":metadata.auxiliary_generation,
        "input_size":metadata.input_size.to_array(),"output_size":metadata.output_size.to_array(),
        "history_seeded":metadata.history_seeded,"previous_depth_cleared":metadata.previous_depth_cleared,
        "frames_encoded_before":metadata.frames_encoded_before,
        "packet":{"path":directory.join("packet336.bin"),"bytes":packet.len(),"sha256":bytes_sha256(&packet)},
        "params":{"path":directory.join("params32.bin"),"bytes":params.len(),"sha256":bytes_sha256(&params)},
        "completion_path":directory.join("readback-complete.txt"),"planes":planes,
    }))
}

fn write_snapshot(
    proof: &Proof,
    label: &str,
    snapshot: &temporal::observation::Snapshot,
) -> Result<serde_json::Value, String> {
    if !snapshot.complete() {
        return Err("GPU snapshot lacks its six complete planes".into());
    }
    let mut planes = Vec::new();
    for plane in &snapshot.planes {
        let bytes = plane.unpadded_bytes().map_err(|error| error.to_string())?;
        let path = proof.planes.join(format!("{label}-{:?}.bin", plane.role));
        if path.exists() {
            return Err("GPU plane output would overwrite a previous capture".into());
        }
        std::fs::write(&path, &bytes).map_err(|error| error.to_string())?;
        planes.push(serde_json::json!({
            "role": format!("{:?}", plane.role), "format": format!("{:?}", plane.format),
            "size": plane.size.to_array(), "texture_id": plane.texture_id,
            "path": path, "bytes":bytes.len(), "sha256":bytes_sha256(&bytes),
            "row_bytes":plane.row_bytes, "bytes_per_row":plane.bytes_per_row, "padded_bytes":plane.bytes.len(),
        }));
    }
    let frame = snapshot.frame;
    Ok(serde_json::json!({
        "label":label, "request_id":snapshot.request_id,
        "tag":{"view_bits":snapshot.tag.view.to_bits().to_string(),"serial":snapshot.tag.serial},
        "generation":snapshot.generation, "input_size":snapshot.input_size.to_array(), "output_size":snapshot.output_size.to_array(),
        "frame":{
            "phase":frame.phase,"history_epoch":frame.history_epoch,"needs_history_seed":frame.needs_history_seed,
            "current_unjittered_clip_from_world":frame.unjittered_clip_from_world.to_cols_array(),
            "previous_unjittered_clip_from_world":frame.previous_unjittered_clip_from_world.to_cols_array(),
            "current_world_from_jittered_clip":frame.world_from_clip.to_cols_array(),
            "previous_world_from_jittered_clip":frame.previous_world_from_clip.to_cols_array(),
            "camera_position":frame.camera_position.to_array(),"previous_camera_position":frame.previous_camera_position.to_array(),
            "near_far":frame.near_far.to_array(),"jitter_pixels":frame.jitter_pixels.to_array(),
        },
        "planes":planes,
        "resolve_encoding":snapshot.resolve_encoding.as_ref().map(|observation| serde_json::json!({
            "tag_serial":observation.tag.serial,"history_epoch":observation.history_epoch,"generation":observation.generation,"auxiliary_generation":observation.auxiliary_generation,
            "seed_draws":observation.seed_draws,"resolve_draws":observation.resolve_draws,
            "previous_depth_copied":observation.previous_depth_copied,"output_copied":observation.output_copied,"resolve_acknowledged":observation.resolve_acknowledged,
            "frames_encoded":observation.frames_encoded,"history_input_id":observation.history_input_id,"history_output_id":observation.history_output_id,
            "previous_depth_id":observation.previous_depth_id,"error":format!("{:?}",observation.error),
        })),
    }))
}

fn write_receipt(
    world: &World,
    proof: &Proof,
    status: &str,
    error: Option<&str>,
) -> Result<(), String> {
    let observations = world
        .resource::<temporal::render::AuxObservations>()
        .snapshots();
    let observations = observations.into_iter().map(|observation| serde_json::json!({
        "tag_serial":observation.tag.serial,"generation":observation.generation,
            "coverage":{"visible":observation.coverage.visible,"registered":observation.coverage.registered,"queued":observation.coverage.queued,
            "unsupported":observation.coverage.unsupported,"pending":observation.coverage.pending,"sky_visible":observation.coverage.sky_visible,"errors":observation.coverage.errors,
            "diagnostics":observation.coverage.diagnostics,"diagnostics_omitted":observation.coverage.diagnostics_omitted},
        "mesh_draws":observation.mesh_draws,"sky_draws":observation.sky_draws,"hull_draws":observation.hull_draws,
        "hull_contract_satisfied":observation.hull_contract_satisfied,"final_depth_copied":observation.final_depth_copied,
        "previous_depth_copied":observation.previous_depth_copied,"auxiliary_acknowledged":observation.auxiliary_acknowledged,"errors":observation.errors,
        "draw_diagnostics":observation.draw_diagnostics,"draw_diagnostics_omitted":observation.draw_diagnostics_omitted,
        "diagnostic_names":observation.coverage.diagnostics.iter().chain(observation.draw_diagnostics.iter()).map(|diagnostic| {
            let entity=Entity::from_bits(diagnostic.main_entity_bits);
            serde_json::json!({"main_entity_bits":diagnostic.main_entity_bits,"name":world.get::<Name>(entity).map(Name::as_str)})
        }).collect::<Vec<_>>(),
    })).collect::<Vec<_>>();
    let frames = world.resource::<temporal::frame::Frames>();
    let receipt = serde_json::json!({
        "schema":"forge.tidewater.native-temporal-aux-gpu-proof", "status":status,"error":error,
        "elapsed_seconds":proof.started.elapsed().as_secs_f64(),"captures":proof.captures,
        "resolve_captures":proof.resolve_captures,"resolve_readback_enabled":proof.resolve_readback,
        "trace_captures":proof.trace_captures,"trace_readback_enabled":proof.trace_readback,
        "trace_scope":"Instrumented shader reproductions only, paired with seven real source planes and two private callbacks; all-pixel scratch equality is a separate gate and never proves original F32 instruction/intermediate identity",
        "base_captures":proof.base_captures,"base_readback_enabled":proof.base_readback,
        "base_scope":"Opt-in before/after late transparencies of the same auxiliary textures and encoding; six real GPU map callbacks per capture, bound packet336; no independent camera/blend/source parity verdict",
        "committed_aux":frames.committed_aux,"rejected_aux":frames.rejected_aux,"frame_error":format!("{:?}",frames.last_error),
        "source_resolve_enabled":proof.source_resolve,"committed_resolve":frames.committed_resolve,"rejected_resolve":frames.rejected_resolve,
        "resolve_encoding":frames.selected_view().and_then(|view| world.get_resource::<temporal::resolve::ResolveObservations>().and_then(|observations| observations.snapshot(view))).map(|observation| serde_json::json!({
            "tag_serial":observation.tag.serial,"history_epoch":observation.history_epoch,"generation":observation.generation,"auxiliary_generation":observation.auxiliary_generation,
            "seed_draws":observation.seed_draws,"resolve_draws":observation.resolve_draws,"previous_depth_cleared":observation.previous_depth_cleared,
            "previous_depth_copied":observation.previous_depth_copied,"output_copied":observation.output_copied,"resolve_acknowledged":observation.resolve_acknowledged,
            "frames_encoded":observation.frames_encoded,"history_input_id":observation.history_input_id,"history_output_id":observation.history_output_id,
            "previous_depth_id":observation.previous_depth_id,"error":format!("{:?}",observation.error),
        })),
        "hdr_encoding":world.get_resource::<temporal::hdr::HdrObservations>().map(|observations|format!("{:?}",observations.snapshots())),
        "derivation_errors":world.resource::<temporal::Derivations>().errors(),"observations":observations,
        "native_material_types":{
            "breaker_lip":format!("{:?}",std::any::TypeId::of::<super::breaker_lip::LipMaterial>()),
            "spray":format!("{:?}",std::any::TypeId::of::<super::spray_sprites::SprayMaterial>()),
            "standard":format!("{:?}",std::any::TypeId::of::<StandardMaterial>()),
            "ocean":format!("{:?}",std::any::TypeId::of::<super::water_optics::WaterMaterial>()),
        },
        "screenshot":proof.screenshot,"screenshot_scope":"Context image captured after GPU snapshots, different frame; no exact-frame beauty claim",
        "scope":if proof.resolve_readback {"Native scale1 six auxiliary planes plus three seven-plane source resolve GPU snapshots (static, camera-moved, explicit reset seed); no independent numeric parity, editor history, first-allocation/resize or package qualification"} else if proof.source_resolve {"Native scale1 six auxiliary planes and matching actual source-resolve/HDR command encoding; no resolve color readback/numeric parity, editor history or package qualification"} else {"Native scale1 auxiliary inputs only; no TAAU resolve, source visual parity, editor history or package qualification"},
    });
    std::fs::write(
        &proof.output,
        serde_json::to_vec_pretty(&receipt).map_err(|error| error.to_string())?,
    )
    .map_err(|error| error.to_string())
}
