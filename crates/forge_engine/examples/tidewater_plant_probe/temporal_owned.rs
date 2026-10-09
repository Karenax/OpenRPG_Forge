//! Explicit pipelined Render-owned encoding probe. No texture readback oracle.
use bevy::{
    core_pipeline::schedule::{Core3d, Core3dSystems},
    prelude::*,
    render::{
        Render, RenderApp, RenderSystems,
        camera::{ExtractedCamera, TemporalJitter},
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        renderer::ViewQuery,
        view::{
            ExtractedView,
            screenshot::{Screenshot, ScreenshotCaptured, save_to_disk},
        },
    },
};
use forge_engine::{tidewater_refraction as refraction, tidewater_temporal as temporal};
use std::{
    collections::VecDeque,
    path::PathBuf,
    sync::{Arc, Mutex, atomic::Ordering},
    time::{Duration, Instant},
};

const MAIL_LIMIT: usize = 80;
const SAMPLE_LIMIT: usize = 4096;
const LABELS: [&str; 7] = [
    "static",
    "camera-moved",
    "explicit-reset",
    "odd-resize",
    "inactive-gap",
    "gap-recovered",
    "finished",
];

#[derive(Resource, Clone, Default, ExtractResource)]
struct Intent {
    case: usize,
    expects_stall_contact: bool,
}
#[derive(Clone)]
struct Sample {
    case: usize,
    valid: bool,
    phase: u8,
    epoch: u64,
    resets: u64,
    recovery_resets: u64,
    generation: u64,
    auxiliary_generation: u64,
    size: UVec2,
    camera: Vec3,
    json: serde_json::Value,
}
#[derive(Default)]
struct Mail {
    records: VecDeque<Sample>,
    omitted: u64,
    render_frames: u64,
    inactive_frames: u64,
    latest_owner: Option<String>,
    render_thread: Option<std::thread::ThreadId>,
    gap_diagnostics: VecDeque<serde_json::Value>,
}
#[derive(Resource, Clone, Default)]
struct Bridge(Arc<Mutex<Mail>>);
#[derive(Resource, Default)]
struct Seen {
    last_success: Option<(
        temporal::frame::FrameTag,
        forge_engine::tidewater_temporal_frame::Frame,
        u64,
        u64,
    )>,
    capture_counter: u64,
}

#[derive(Resource)]
pub struct Proof {
    started: Instant,
    last_progress: Instant,
    selected: Option<Entity>,
    output: PathBuf,
    screenshot: PathBuf,
    case: usize,
    counts: [u32; 7],
    invalid_counts: [u32; 7],
    samples: VecDeque<serde_json::Value>,
    omitted: u64,
    phases: u32,
    last: Option<Sample>,
    before: Option<Sample>,
    inactive_before: u64,
    screenshot_done: Option<Instant>,
    finished: bool,
    error: Option<String>,
}

impl Proof {
    fn remember_current(&mut self) {
        self.before.clone_from(&self.last);
    }
}

// This adapter wires the render-owned proof stages in a deliberate order;
// splitting the registration would make the stage dependency less reviewable.
#[allow(clippy::too_many_lines)]
pub fn install(app: &mut App) -> Result<(), String> {
    use temporal::{phase::MaterialRole, shaders::Family};
    if std::env::var("FORGE_TIDEWATER_TEMPORAL_RESOLVE").as_deref() != Ok("1") {
        return Err("Render-owned proof requires FORGE_TIDEWATER_TEMPORAL_RESOLVE=1".into());
    }
    if !app.is_plugin_added::<bevy::render::pipelined_rendering::PipelinedRenderingPlugin>() {
        return Err("Render-owned proof requires the real PipelinedRenderingPlugin enabled".into());
    }
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
    // Nature has the same acquired ABI; its actual installed source shader is derived.
    temporal::register_material::<forge_engine::tidewater_debris::nature::NatureMaterial>(
        app,
        Family::Village,
        MaterialRole::Opaque,
    )?;
    temporal::register_material::<forge_engine::tidewater_debris::pebble_material::PebbleMaterial>(
        app,
        Family::Pebble,
        MaterialRole::Opaque,
    )?;
    temporal::register_material::<forge_engine::tidewater_debris::scanned_material::ScannedMaterial>(
        app,
        Family::Scanned,
        MaterialRole::Opaque,
    )?;
    temporal::register_material::<forge_engine::tidewater_fish_props::material::FishMaterial>(
        app,
        Family::Fish,
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
    if app.world().resource::<super::Probe>().fish_stand.is_some() {
        temporal::register_material::<forge_engine::tidewater_stall::full_material::FullMaterial>(
            app,
            Family::FullStall,
            MaterialRole::Opaque,
        )?;
    }
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
    if !app.is_plugin_added::<forge_engine::tidewater_underwater::TidewaterUnderwaterPlugin>() {
        let underwater = super::walk::underwater_plugin(app).map_err(|e| e.to_string())?;
        app.add_plugins(underwater);
    }
    temporal::render_owned::register(app)?;
    let screenshot = app.world().resource::<super::Probe>().screenshot.clone();
    let output = screenshot.with_extension("owned.json");
    if screenshot.exists() || output.exists() {
        return Err("Render-owned proof refuses existing outputs".into());
    }
    let bridge = Bridge::default();
    let expects_stall_contact = app.world().resource::<super::Probe>().fish_stand.is_some();
    app.insert_resource(bridge.clone())
        .insert_resource(Intent {
            case: 0,
            expects_stall_contact,
        })
        .add_plugins(ExtractResourcePlugin::<Intent>::default())
        .insert_resource(Proof {
            started: Instant::now(),
            last_progress: Instant::now(),
            selected: None,
            output,
            screenshot,
            case: 0,
            counts: [0; 7],
            invalid_counts: [0; 7],
            samples: VecDeque::new(),
            omitted: 0,
            phases: 0,
            last: None,
            before: None,
            inactive_before: 0,
            screenshot_done: None,
            finished: false,
            error: None,
        })
        .add_systems(
            PostUpdate,
            select_camera
                .before(temporal::FrameSystems::Prepare)
                .before(forge_engine::tidewater_underwater::TidewaterUnderwaterUpdateSet),
        )
        .add_systems(
            PostUpdate,
            water_query
                .after(select_camera)
                .after(bevy::transform::TransformSystems::Propagate)
                .before(forge_engine::tidewater_underwater::TidewaterUnderwaterUpdateSet),
        )
        .add_systems(Update, advance);
    app.sub_app_mut(RenderApp)
        .insert_resource(bridge)
        .init_resource::<Seen>()
        .add_systems(
            Core3d,
            observe
                .after(temporal::hdr::SourceHdrSystems::Resolve)
                .before(Core3dSystems::EarlyPostProcess),
        )
        .add_systems(
            Render,
            heartbeat
                .after(RenderSystems::Render)
                .before(RenderSystems::Cleanup),
        );
    Ok(())
}

fn select_camera(world: &mut World) {
    if world.resource::<Proof>().selected.is_some() {
        return;
    }
    let mut query = world
        .query_filtered::<(Entity, &Camera), (With<Camera3d>, Without<refraction::CaptureCamera>)>(
        );
    let active: Vec<_> = query
        .iter(world)
        .filter(|(_, c)| c.is_active)
        .map(|(e, _)| e)
        .collect();
    if active.is_empty() {
        return;
    }
    if active.len() != 1 {
        world.resource_mut::<Proof>().error = Some("Multiple active main cameras".into());
        return;
    }
    let entity = active[0];
    // The submitted native scene was audited: it contains no boat/hull renderer.
    world.entity_mut(entity).insert((
        temporal::render::HullRequirement::NoHullsInScene,
        forge_engine::tidewater_underwater::TidewaterUnderwaterCamera,
    ));
    match temporal::render_owned::select(world, Some(entity)) {
        Ok(()) => world.resource_mut::<Proof>().selected = Some(entity),
        Err(e) => world.resource_mut::<Proof>().error = Some(e),
    }
}

fn water_query(world: &World) {
    let Some(view) = world.resource::<Proof>().selected else {
        return;
    };
    let Some(transform) = world.get::<GlobalTransform>(view) else {
        return;
    };
    let targets = world.resource::<super::spray::water_queries::Targets>();
    if targets.bootstrap_ready.load(Ordering::Acquire) {
        let p = transform.translation();
        if let Err(e) = targets.queries.set_point(0, p.x, p.z) {
            error!("Render-owned water query: {e}");
        }
    }
}

// CPU f32 camera recomputation allowance, per coefficient. Not a pixel tolerance.
fn matrix_matches(a: Mat4, b: Mat4) -> bool {
    a.to_cols_array()
        .into_iter()
        .zip(b.to_cols_array())
        .all(|(a, b)| {
            a.is_finite()
                && b.is_finite()
                && (a - b).abs() <= 8.0 * f32::EPSILON * a.abs().max(b.abs()).max(1.0)
        })
}

// The render callback compares all owned stages in one receipt; its ordered
// parameters are the Bevy system contract and its body keeps those checks
// together to avoid partial observations.
#[allow(
    clippy::too_many_arguments,
    clippy::too_many_lines,
    clippy::type_complexity
)]
fn observe(
    view: ViewQuery<(
        &temporal::frame::ViewPacket,
        &ExtractedView,
        &ExtractedCamera,
        &TemporalJitter,
        &temporal::targets::AuxTargets,
    )>,
    owner: Res<temporal::render_owner::Owner>,
    owner_report: Res<temporal::render_owned::Observation>,
    resolve: Res<temporal::resolve::ResolveObservations>,
    auxiliary: Res<temporal::render::AuxObservations>,
    hdr: Res<temporal::hdr::HdrObservations>,
    captures: Query<
        (
            &refraction::CaptureSource,
            &ExtractedView,
            &ExtractedCamera,
            Option<&TemporalJitter>,
        ),
        With<refraction::CaptureCamera>,
    >,
    capture_targets: Option<Res<refraction::Targets>>,
    stall_contact: Option<Res<forge_engine::tidewater_stall::contact_gpu::Observation>>,
    intent: Res<Intent>,
    bridge: Res<Bridge>,
    mut seen: ResMut<Seen>,
) {
    // The final PNG waits on separate frames; retain the bounded proof records.
    if intent.case >= 6 {
        return;
    }
    let (packet, extracted, camera, jitter, targets) = view.into_inner();
    let state = owner.snapshot();
    let resolved = resolve.snapshot(packet.tag.view);
    let aux = auxiliary.snapshot(packet.tag.view);
    let medium = hdr.snapshot(packet.tag.view);
    let report = owner_report.snapshot();
    let contact = stall_contact
        .as_ref()
        .and_then(|observation| observation.0.lock().ok().map(|report| report.clone()));
    let mut checks = Vec::<String>::new();
    let mut check = |valid: bool, reason: &str| {
        if !valid {
            checks.push(reason.into());
        }
    };
    let size = packet.frame.input_size;
    check(
        camera.physical_viewport_size == Some(size) && camera.physical_target_size == Some(size),
        "extracted-size",
    );
    check(
        extracted.viewport == UVec4::new(0, 0, size.x, size.y),
        "extracted-viewport",
    );
    check(
        extracted.clip_from_view == packet.frame.clip_from_view
            && extracted.world_from_view.to_matrix().inverse() == packet.frame.view_from_world,
        "extracted-unjittered-pose-projection",
    );
    check(
        jitter.offset == packet.frame.jitter_pixels,
        "actual-render-jitter",
    );
    let mut raster = extracted.clip_from_view;
    jitter.jitter_projection(&mut raster, size.as_vec2());
    let raster_clip = raster * extracted.world_from_view.to_matrix().inverse();
    check(
        matrix_matches(raster_clip, packet.frame.clip_from_world),
        "actual-render-raster-projection",
    );
    check(
        targets.view == packet.tag.view && targets.history_epoch == packet.frame.history_epoch,
        "auxiliary-target-identity",
    );
    check(
        aux.as_ref().is_some_and(|a| {
            a.complete() && a.tag == packet.tag && a.generation == targets.generation
        }),
        "auxiliary-same-tag-complete",
    );
    check(
        medium
            .as_ref()
            .is_some_and(|h| h.complete() && h.tag == packet.tag),
        "hdr-same-tag-complete",
    );
    check(
        resolved.as_ref().is_some_and(|r| {
            r.complete()
                && r.tag == packet.tag
                && r.history_epoch == packet.frame.history_epoch
                && r.auxiliary_generation == targets.generation
                && r.bound_packet.as_ref() == Some(&packet.packet)
        }),
        "resolve-same-tag-bound-packet-complete",
    );
    let (resets, committed, generation) = state.as_ref().map_or((0, 0, 0), |s| {
        (
            s.resets,
            s.committed,
            s.last_commit.map_or(0, |c| c.resolve_generation),
        )
    });
    check(
        state.as_ref().is_ok_and(|s| {
            s.last_frame.as_ref() == Some(&packet.frame)
                && s.rejected_commits == 0
                && s.last_error.is_none()
                && s.last_commit.is_some_and(|c| {
                    c.tag == packet.tag
                        && c.history_epoch == packet.frame.history_epoch
                        && c.resolve_generation == generation
                        && c.auxiliary_generation == targets.generation
                })
        }),
        "owner-real-commit-frame",
    );
    check(
        resolved
            .as_ref()
            .is_some_and(|r| r.generation == generation && generation != 0),
        "owner-resolve-generation",
    );
    check(
        seen.last_success.as_ref().is_none_or(|(previous, ..)| {
            previous.view == packet.tag.view && previous.serial < packet.tag.serial
        }),
        "owner-real-serial-advances",
    );
    let previous_check = if packet.packet.phase_epoch_seed.w & 2 != 0 {
        check(
            packet.frame.needs_history_seed
                && packet.frame.phase == 0
                && packet.frame.previous_unjittered_clip_from_world
                    == packet.frame.unjittered_clip_from_world
                && packet.frame.previous_world_from_clip == packet.frame.world_from_clip
                && packet.frame.previous_camera_position == packet.frame.camera_position,
            "new-instance-camera-object-seed",
        );
        "current-camera-seed"
    } else if let Some((_, previous, previous_resets, previous_committed)) = &seen.last_success {
        check(
            *previous_resets == resets
                && packet.frame.previous_unjittered_clip_from_world
                    == previous.unjittered_clip_from_world
                && packet.frame.previous_world_from_clip == previous.world_from_clip
                && packet.frame.previous_camera_position == previous.camera_position,
            "previous-camera-last-real-resolve",
        );
        check(
            committed == previous_committed + 1 && packet.frame.phase == (previous.phase + 1) % 32,
            "phase-advances-on-real-commit",
        );
        if previous.input_size != size {
            check(
                packet.frame.history_epoch == previous.history_epoch + 1
                    && packet.frame.needs_history_seed
                    && packet.packet.phase_epoch_seed.w == 1
                    && resolved.as_ref().is_some_and(|r| r.seed_draws == 1),
                "resize-seeds-preserving-phase-and-camera",
            );
        }
        "last-successful-resolve"
    } else {
        check(false, "previous-camera-unobserved");
        "unobserved"
    };
    // Camera history advances on a genuine temporal commit even when optics is
    // still bootstrapping. A missing refraction join must not make it stale.
    let history_commit_valid = checks.is_empty();
    if intent.expects_stall_contact
        && !contact.as_ref().is_some_and(|contact| {
            contact.tag == Some(packet.tag)
                && contact.source_owned
                && contact.error.is_none()
                && if packet.frame.needs_history_seed {
                    contact.seed && !contact.history_matched
                } else {
                    !contact.seed && contact.history_matched
                }
        })
    {
        checks.push("stall-contact-same-camera-tag-matched-history-or-explicit-seed".into());
    }
    let matching: Vec<_> = captures
        .iter()
        .filter(|(s, ..)| s.view == packet.tag.view)
        .collect();
    let counter = capture_targets
        .as_ref()
        .map_or(0, |t| t.encoded_frames.load(Ordering::Acquire));
    let mut capture_json = serde_json::Value::Null;
    let mut capture_ok = matching.len() == 1 && counter > seen.capture_counter;
    if let Some((_, v, c, j)) = matching.first() {
        let s = c.physical_viewport_size.unwrap_or(UVec2::ZERO);
        let expected = jitter.offset * s.as_vec2() / size.as_vec2();
        capture_ok &= s.min_element() > 0
            && v.clip_from_view == extracted.clip_from_view
            && v.world_from_view == extracted.world_from_view
            && j.is_some_and(|j| j.offset == expected);
        capture_json = serde_json::json!({"size":s.to_array(),"jitter":j.map(|j|j.offset.to_array()),
            "expected_jitter":expected.to_array(),"unjittered_projection":v.clip_from_view.to_cols_array(),
            "world_from_view":v.world_from_view.to_matrix().to_cols_array(),"encoded_depth_counter":counter});
    }
    if !capture_ok {
        checks.push("refraction-extracted-pose-projection-jitter-fresh-depth-encoding".into());
    }
    seen.capture_counter = counter;
    let valid = checks.is_empty();
    let previous_phase = seen.last_success.as_ref().map(|(_, f, ..)| f.phase);
    let previous_committed = seen.last_success.as_ref().map(|(.., c)| *c);
    if history_commit_valid {
        seen.last_success = Some((packet.tag, packet.frame, resets, committed));
    }
    let sample = Sample {
        case: intent.case,
        valid,
        phase: packet.frame.phase,
        epoch: packet.frame.history_epoch,
        resets,
        recovery_resets: report.as_ref().map_or(0, |r| r.recovery_resets),
        generation,
        auxiliary_generation: targets.generation,
        size,
        camera: packet.frame.camera_position,
        json: serde_json::json!({"case":LABELS[intent.case.min(6)],"valid":valid,"failed_checks":checks,
            "tag":{"view_bits":packet.tag.view.to_bits().to_string(),"serial":packet.tag.serial},
            "phase":packet.frame.phase,"history_epoch":packet.frame.history_epoch,"flags":packet.packet.phase_epoch_seed.w,
            "input_size":size.to_array(),"jitter":jitter.offset.to_array(),"camera":packet.frame.camera_position.to_array(),
            "previous_camera":packet.frame.previous_camera_position.to_array(),"previous_camera_check":previous_check,
            "previous_phase":previous_phase,"previous_owner_committed":previous_committed,
            "current_unjittered_vp":packet.frame.unjittered_clip_from_world.to_cols_array(),
            "previous_unjittered_vp":packet.frame.previous_unjittered_clip_from_world.to_cols_array(),
            "actual_recomputed_raster_vp":raster_clip.to_cols_array(),"owner_resets":resets,"owner_committed":committed,
            "owner":format!("{state:?}"),"owner_report":report.as_ref().map(|r|format!("{:?}",r.error)),
            "recovery_resets":report.as_ref().map_or(0,|r|r.recovery_resets),
            "resolve":resolved.as_ref().map(|r|serde_json::json!({"complete":r.complete(),"generation":r.generation,
                "auxiliary_generation":r.auxiliary_generation,"seed_draws":r.seed_draws,"resolve_draws":r.resolve_draws,
                "depth_cleared":r.previous_depth_cleared,"depth_copied":r.previous_depth_copied,"output_copied":r.output_copied,
                "frames_encoded":r.frames_encoded,"error":format!("{:?}",r.error)})),
            "auxiliary":aux.as_ref().map(|a|format!("{a:?}")),"hdr":medium.as_ref().map(|h|format!("{h:?}")),
            "refraction_join":capture_json,"refraction_join_valid":capture_ok,
            "stall_contact_required":intent.expects_stall_contact,
            "stall_contact":contact.as_ref().map(|contact|serde_json::json!({
                "tag":contact.tag.map(|tag|serde_json::json!({"view_bits":tag.view.to_bits().to_string(),"serial":tag.serial})),
                "source_owned":contact.source_owned,"history_matched":contact.history_matched,"seed":contact.seed,"error":contact.error}))}),
    };
    if let Ok(mut mail) = bridge.0.lock() {
        if mail.records.len() == MAIL_LIMIT {
            mail.records.pop_front();
            mail.omitted += 1;
        }
        mail.records.push_back(sample);
    }
}

// Exclusive: the thread is the actual Render sub-app driver, not a task-pool
// worker that could also execute a system with pipelining disabled.
fn heartbeat(world: &mut World) {
    let bridge = world.resource::<Bridge>().clone();
    let case = world.resource::<Intent>().case;
    let mut packets = world.query::<&temporal::frame::ViewPacket>();
    let missing = packets.iter(world).next().is_none();
    let snapshot = world.resource::<temporal::render_owner::Owner>().snapshot();
    let state = format!("{snapshot:?}");
    let report = world
        .resource::<temporal::render_owned::Observation>()
        .snapshot();
    if let Ok(mut mail) = bridge.0.lock() {
        mail.render_frames += 1;
        mail.render_thread = Some(std::thread::current().id());
        if case == 4 && missing {
            mail.inactive_frames += 1;
            if mail.gap_diagnostics.len() < 2 {
                let frame = mail.render_frames;
                let owner_fields = snapshot.as_ref().ok().map(|s| {
                    serde_json::json!({
                    "committed":s.committed,"prepared":s.prepared,"last_serial":s.last_serial,
                    "resets":s.resets,"status":format!("{:?}",s.status),
                    "pending_frame_present":s.pending_frame.is_some(),
                    "last_frame_phase":s.last_frame.as_ref().map(|f|f.phase),
                    "last_commit":s.last_commit.map(|c|serde_json::json!({
                        "serial":c.tag.serial,"view_bits":c.tag.view.to_bits().to_string(),
                        "epoch":c.history_epoch,"generation":c.resolve_generation,
                        "auxiliary_generation":c.auxiliary_generation}))})
                });
                mail.gap_diagnostics
                    .push_back(serde_json::json!({"actual_render_frame":frame,
                    "selected_packet_absent":true,"owner":state,"owner_fields":owner_fields,
                    "render_owned_error":report.as_ref().and_then(|r|r.error.clone()),
                    "recovery_resets":report.as_ref().map_or(0,|r|r.recovery_resets)}));
            }
        }
        mail.latest_owner = Some(format!(
            "{state}; runtime_error={:?}",
            report.as_ref().map(|r| &r.error)
        ));
    }
}

// The state machine keeps each externally observable proof transition together
// so a receipt cannot be emitted between a transition and its validation.
#[allow(clippy::too_many_lines)]
fn advance(world: &mut World) {
    let result = world.resource_scope(|world, mut proof: Mut<Proof>| -> Result<bool, String> {
        if proof.finished {
            return Ok(false);
        }
        if let Some(e) = &proof.error {
            return Err(e.clone());
        }
        if proof.started.elapsed() > Duration::from_secs(180) {
            return Err("Render-owned runtime deadline180s".into());
        }
        let bridge = world.resource::<Bridge>().clone();
        let old_case = proof.case;
        let (records, inactive, latest, gaps) = {
            let mut mail = bridge
                .0
                .lock()
                .map_err(|_| "Render proof bridge poisoned")?;
            proof.omitted += mail.omitted;
            mail.omitted = 0;
            (
                mail.records.drain(..).collect::<Vec<_>>(),
                mail.inactive_frames,
                mail.latest_owner.clone(),
                mail.gap_diagnostics.clone(),
            )
        };
        if proof.omitted != 0 {
            return Err("Render proof mailbox overflow lost receipts".into());
        }
        for sample in records {
            if proof.samples.len() == SAMPLE_LIMIT {
                return Err(
                    "Render proof sample limit reached; refusing incomplete evidence".into(),
                );
            }
            proof.samples.push_back(sample.json.clone());
            if !sample.valid {
                proof.invalid_counts[sample.case.min(6)] += 1;
            }
            if !sample.valid || sample.case != proof.case {
                continue;
            }
            let current_case = proof.case;
            proof.counts[current_case] += 1;
            if proof.case == 0 {
                proof.phases |= 1u32 << sample.phase;
            }
            proof.last = Some(sample);
        }
        if proof.last_progress.elapsed() > Duration::from_secs(10) {
            eprintln!(
                "Render-owned proof case={} counts={:?}, latest={latest:?}",
                LABELS[proof.case], proof.counts
            );
            proof.last_progress = Instant::now();
        }
        let Some(view) = proof.selected else {
            return Ok(false);
        };
        match proof.case {
            0 if proof.counts[0] >= 34 && proof.phases == u32::MAX => {
                proof.remember_current();
                world
                    .get_mut::<Transform>(view)
                    .ok_or("Selected camera missing transform")?
                    .translation
                    .x += 0.75;
                proof.case = 1;
            }
            1 if proof.counts[1] >= 3 => {
                let before = proof.before.as_ref().ok_or("Static baseline missing")?;
                let current = proof.last.as_ref().ok_or("Moved camera receipt missing")?;
                if current.camera == before.camera {
                    return Err("Main camera movement did not reach real Render view".into());
                }
                proof.remember_current();
                temporal::render_owned::reset(world, view)?;
                proof.case = 2;
            }
            2 if proof.counts[2] >= 3 => {
                let before = proof.before.as_ref().ok_or("Reset baseline missing")?;
                let current = proof.last.as_ref().ok_or("Reset receipt missing")?;
                if current.resets <= before.resets {
                    return Err("Explicit reset did not reach Render owner".into());
                }
                if !proof.samples.iter().any(|s| {
                    s["case"] == "explicit-reset"
                        && s["valid"] == true
                        && s["flags"].as_u64().is_some_and(|f| f & 2 != 0)
                        && s["resolve"]["seed_draws"] == 1
                }) {
                    return Err("Explicit reset seed was not observed".into());
                }
                proof.remember_current();
                let mut windows =
                    world.query_filtered::<&mut Window, With<bevy::window::PrimaryWindow>>();
                let mut window = windows.single_mut(world).map_err(|e| e.to_string())?;
                window.resolution.set_physical_resolution(1919, 1079);
                proof.case = 3;
            }
            3 if proof.counts[3] >= 3 => {
                let before = proof.before.as_ref().ok_or("Resize baseline missing")?;
                let current = proof.last.as_ref().ok_or("Resize receipt missing")?;
                if current.size != UVec2::new(1919, 1079) {
                    return Ok(false);
                }
                if current.generation <= before.generation
                    || current.auxiliary_generation <= before.auxiliary_generation
                    || current.epoch <= before.epoch
                {
                    return Err("Odd resize did not change actual generations/epoch".into());
                }
                if !proof.samples.iter().any(|s| {
                    s["case"] == "odd-resize"
                        && s["valid"] == true
                        && s["input_size"] == serde_json::json!([1919, 1079])
                        && s["flags"] == 1
                        && s["resolve"]["seed_draws"] == 1
                }) {
                    return Err("Resize-only seed without model flag2 was not observed".into());
                }
                proof.remember_current();
                proof.inactive_before = inactive;
                world
                    .get_mut::<Camera>(view)
                    .ok_or("Selected camera missing")?
                    .is_active = false;
                proof.case = 4;
            }
            4 if inactive >= proof.inactive_before + 2 => {
                if gaps.len() != 2
                    || gaps[0]["actual_render_frame"] == gaps[1]["actual_render_frame"]
                    || ["committed", "prepared", "last_serial"].iter().any(|key| {
                        gaps[0]["owner_fields"][*key].is_null()
                            || gaps[0]["owner_fields"][*key] != gaps[1]["owner_fields"][*key]
                    })
                    || gaps
                        .iter()
                        .any(|g| g["owner_fields"]["pending_frame_present"] != false)
                {
                    return Err(
                        "Inactive Render receipts advanced ownership or were incomplete".into(),
                    );
                }
                world
                    .get_mut::<Camera>(view)
                    .ok_or("Selected camera missing")?
                    .is_active = true;
                proof.case = 5;
            }
            5 if proof.counts[5] >= 3 => {
                let before = proof.before.as_ref().ok_or("Gap baseline missing")?;
                let current = proof.last.as_ref().ok_or("Gap recovery receipt missing")?;
                if current.recovery_resets <= before.recovery_resets
                    || current.resets <= before.resets
                    || current.generation <= before.generation
                    || current.auxiliary_generation <= before.auxiliary_generation
                {
                    return Err(
                        "Actual inactive Render gap did not retire and reset history".into(),
                    );
                }
                if !proof.samples.iter().any(|s| {
                    s["case"] == "gap-recovered"
                        && s["valid"] == true
                        && s["flags"].as_u64().is_some_and(|f| f & 2 != 0)
                        && s["resolve"]["seed_draws"] == 1
                }) {
                    return Err("Recovery seed was not observed".into());
                }
                proof.case = 6;
                world
                    .spawn(Screenshot::primary_window())
                    .observe(save_to_disk(proof.screenshot.clone()))
                    .observe(|_: On<ScreenshotCaptured>, mut proof: ResMut<Proof>| {
                        proof.screenshot_done = Some(Instant::now());
                    });
            }
            6 if proof
                .screenshot_done
                .is_some_and(|t| t.elapsed() >= Duration::from_secs(2)) =>
            {
                if !proof.screenshot.exists() {
                    return Err("Context screenshot callback received but PNG absent".into());
                }
                let mail = bridge
                    .0
                    .lock()
                    .map_err(|_| "Render proof bridge poisoned")?;
                if mail
                    .render_thread
                    .is_none_or(|thread| thread == std::thread::current().id())
                {
                    return Err(
                        "Actual Render sub-app driver thread is not separate from Main".into(),
                    );
                }
                return Ok(true);
            }
            _ => {}
        }
        if proof.case != old_case {
            write_progress(&proof, old_case, inactive, &gaps)?;
        }
        world.resource_mut::<Intent>().case = proof.case;
        Ok(false)
    });
    match result {
        Ok(true) => finish(world, None),
        Err(e) => finish(world, Some(e)),
        _ => {}
    }
}

// Each completed stage has a fresh immutable receipt before the next stage.
// A later panic leaves these diagnostics; none is a final PASS verdict.
fn write_progress(
    proof: &Proof,
    completed_case: usize,
    inactive: u64,
    gaps: &VecDeque<serde_json::Value>,
) -> Result<(), String> {
    let output = proof
        .output
        .with_extension(format!("stage-{completed_case}.json"));
    let receipt = serde_json::json!({"schema":"forge.tidewater.render-owned-progress264",
        "status":"incomplete-progress-only","completed_stage":LABELS[completed_case],
        "next_stage":LABELS[proof.case],"elapsed_seconds":proof.started.elapsed().as_secs_f64(),
        "successful_case_counts":proof.counts,"invalid_case_counts":proof.invalid_counts,
        "omitted_samples":proof.omitted,"samples":proof.samples,
        "actual_inactive_render_frames":inactive,"inactive_render_receipts":gaps});
    write_new_json(&output, &receipt)
}

fn write_new_json(output: &std::path::Path, receipt: &serde_json::Value) -> Result<(), String> {
    use std::io::Write;
    let bytes = serde_json::to_vec_pretty(receipt).map_err(|e| e.to_string())?;
    let mut file = std::fs::OpenOptions::new()
        .create_new(true)
        .write(true)
        .open(output)
        .map_err(|e| e.to_string())?;
    file.write_all(&bytes).map_err(|e| e.to_string())?;
    file.sync_all().map_err(|e| e.to_string())
}

fn finish(world: &mut World, error: Option<String>) {
    let (output, receipt) = {
        let mut proof = world.resource_mut::<Proof>();
        if proof.finished {
            return;
        }
        proof.finished = true;
        let status = if error.is_none() {
            "passed-render-encoding-only"
        } else {
            "failed-render-encoding"
        };
        (
            proof.output.clone(),
            serde_json::json!({"schema":"forge.tidewater.render-owned-pipelined-probe264",
            "status":status,"error":error,"elapsed_seconds":proof.started.elapsed().as_secs_f64(),
            "pipelined_plugin_installed":true,"source_resolve_enabled":true,"case":LABELS[proof.case],
            "successful_case_counts":proof.counts,"phases_mask":proof.phases,"samples":proof.samples,
            "invalid_case_counts":proof.invalid_counts,
            "omitted_samples":proof.omitted,"screenshot":proof.screenshot,"screenshot_received":proof.screenshot_done.is_some(),
            "scope":"Actual Render schedule command-encoding joins and immutable Owner/camera/target receipts under real pipelined rendering; no texture readbacks, GPU completion, numeric/source/visual parity, object-motion oracle, Studio or package qualification",
            "projection_check":"Actual ExtractedView plus real TemporalJitter recomputed with Bevy jitter_projection; 8*f32::EPSILON per coefficient scaled by max(abs(a),abs(b),1), not GPU uniform readback",
            "screenshot_scope":"Final context PNG after cases, separate frame; not a paired source output or oracle"}),
        )
    };
    let bridge = world.resource::<Bridge>().clone();
    let mut receipt = receipt;
    if let Ok(mail) = bridge.0.lock() {
        receipt["render_frames"] = mail.render_frames.into();
        receipt["actual_inactive_render_frames"] = mail.inactive_frames.into();
        receipt["latest_owner"] = serde_json::json!(mail.latest_owner);
        receipt["inactive_render_receipts"] = serde_json::json!(mail.gap_diagnostics);
        receipt["main_driver_thread"] =
            serde_json::json!(format!("{:?}", std::thread::current().id()));
        receipt["render_driver_thread"] =
            serde_json::json!(mail.render_thread.map(|t| format!("{t:?}")));
    }
    receipt["derivation_errors"] =
        serde_json::json!(world.resource::<temporal::Derivations>().errors());
    let write = write_new_json(&output, &receipt);
    let failed = error.is_some() || write.is_err();
    if failed {
        eprintln!("Render-owned proof failure={error:?}, receipt={write:?}");
        world.resource_mut::<super::Probe>().failed = true;
    }
    world.write_message(if failed {
        AppExit::error()
    } else {
        AppExit::Success
    });
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn camera_matrix_check_rejects_nonfinite_and_real_pose_changes() {
        assert!(matrix_matches(Mat4::IDENTITY, Mat4::IDENTITY));
        assert!(!matrix_matches(
            Mat4::IDENTITY,
            Mat4::from_translation(Vec3::X)
        ));
        assert!(!matrix_matches(
            Mat4::IDENTITY,
            Mat4::from_cols_array(&[f32::NAN; 16])
        ));
    }
}
