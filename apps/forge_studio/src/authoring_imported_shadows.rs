//! Publish complete shadow generations, then animate their resident casters.
mod prop_observation;
use crate::{
    authoring_assets::{imported_buildings, imported_scene},
    authoring_imported_plants,
};
use bevy::{
    ecs as bevy_ecs,
    math::{DMat4, DQuat},
    prelude::*,
};
use std::sync::atomic::Ordering;
use {
    forge_engine::sampled_terrain_physics::SampledTerrainPhysicsState,
    forge_tidewater::{
        sun_cascades::CascadeView,
        tidewater_material::VillageMaterial,
        tidewater_object_shadow::{graph, live, scene, terrain, wind},
        tidewater_scene_environment::ResidentEnvironment,
        tidewater_terrain::{TerrainMaterial, lod::LodMaterial},
    },
};

#[derive(Resource)]
pub(crate) struct Receiver(pub graph::Target);
#[derive(Clone, PartialEq)]
struct Source {
    terrain: forge_core::ContentHash,
    hill: AssetId<Image>,
    library: u64,
    environment: AssetId<Image>,
    roots: Vec<Entity>,
    plants: Option<forge_core::ContentHash>,
    sun: [u32; 4],
}
#[derive(Clone, PartialEq)]
struct Key {
    source: Source,
    pose: Vec<u32>,
}
#[derive(Resource, Default)]
struct State {
    committed: Option<Key>,
    pending: Option<(Key, graph::Target)>,
    failed: Option<Key>,
    terrain: Option<(u64, terrain::authored::PreparedSource)>,
    tracker: Option<live::Tracker>,
    clock: wind::Clock,
    previous_time: Option<f64>,
    generations: u64,
    diagnostics_time: f64,
}
pub(crate) fn install(app: &mut App, live: bool) -> Result<(), String> {
    // The editor retains the neutral Receiver published by terrain rebuild.
    // No cascade transaction or vegetation motion callback is needed there.
    if !live {
        return Ok(());
    }
    let acquired = {
        let (bytes, frame) = forge_tidewater_assets::tidewater_environment::animation_reference()?;
        forge_tidewater::scene_wind::AcquiredFrame::from_json(&bytes, frame)?
    };
    let clock = wind::Clock::from_acquired(
        acquired.global_time_s,
        acquired.global_frame,
        acquired.vegetation_gust_offset,
        acquired.wind_direction,
        acquired.wind_speed,
    )
    .map_err(str::to_owned)?;
    app.insert_resource(State { clock, ..default() })
        .add_systems(
            PostUpdate,
            update
                .after(imported_scene::update)
                .after(crate::authoring_assets::fish_stand_scene::update)
                .after(authoring_imported_plants::update)
                .after(bevy::transform::TransformSystems::Propagate),
        );
    Ok(())
}
fn snapshot(world: &World, camera: Entity) -> Result<(Key, CascadeView), String> {
    let transform = world
        .get::<GlobalTransform>(camera)
        .ok_or("Caméra absente")?
        .compute_transform();
    let Some(Projection::Perspective(projection)) = world.get::<Projection>(camera) else {
        return Err("Perspective absente".into());
    };
    let environment = world.resource::<ResidentEnvironment>();
    let mut roots = Vec::new();
    let mut pose = transform
        .to_matrix()
        .to_cols_array()
        .map(f32::to_bits)
        .to_vec();
    pose.extend(
        [
            projection.fov,
            projection.aspect_ratio,
            projection.near,
            projection.far,
        ]
        .map(f32::to_bits),
    );
    for (entity, _) in opaque_parts(world)? {
        roots.push(entity);
    }
    let view = CascadeView {
        world_from_view: DMat4::from_rotation_translation(
            DQuat::from_array(transform.rotation.to_array().map(f64::from)).normalize(),
            transform.translation.as_dvec3(),
        ),
        near: f64::from(projection.near),
        fov_degrees: f64::from(projection.fov).to_degrees(),
        aspect: f64::from(projection.aspect_ratio),
    };
    Ok((
        Key {
            source: Source {
                terrain: world
                    .resource::<SampledTerrainPhysicsState>()
                    .plan
                    .document_hash(),
                hill: world
                    .resource::<forge_tidewater::tidewater_hill_shadow::Images>()
                    .output
                    .id(),
                library: imported_buildings::published_generation(),
                environment: environment.environment.id(),
                roots,
                plants: authoring_imported_plants::shadow_head(world),
                sun: environment
                    .scene_lighting
                    .sun_direction
                    .to_array()
                    .map(f32::to_bits),
            },
            pose,
        },
        view,
    ))
}
/// The frame model order must match buildings followed by the acquired Debris draws.
fn opaque_parts(world: &World) -> Result<Vec<(Entity, Mat4)>, String> {
    let mut parts = scene::opaque_parts(world)?;
    if let Some(debris) =
        world.get_resource::<forge_tidewater::tidewater_debris::scene::ResidentScene>()
    {
        parts.extend(debris.shadow_parts(world)?);
    }
    Ok(parts)
}

fn update(world: &mut World) {
    if !crate::authoring_viewport::can_project_sources() || !world.contains_resource::<Receiver>() {
        return;
    }
    let held = crate::authoring_immersion::held(world);
    forge_tidewater::tidewater_object_shadow::receiver::observe_while_held(world, held);
    forge_tidewater::tidewater_refraction::observe_while_held(world, held);
    let Some((_, camera)) = crate::authoring_imported_terrain::shadow_source(world) else {
        return;
    };
    let (key, view) = match snapshot(world, camera) {
        Ok(value) => value,
        Err(error) => {
            crate::authoring_viewport::imported_failure(&error);
            return;
        }
    };
    world.resource_scope(|world, mut state: Mut<State>| {
        if held {
            // A final ocean packet can adjust the floating camera after pause.
            // Drain its actual pose into the existing shadow generation without
            // advancing wind or accepting edits during the frozen comparison.
            let drained = forge_tidewater::tidewater_ocean::live_clock(world)
                .is_some_and(|clock| clock.snapshot().quiescent);
            let can_submit = world
                .get_resource::<graph::animation::State>()
                .is_some_and(graph::animation::State::can_submit);
            let moved = state.committed.as_ref().is_some_and(|committed| {
                committed.source == key.source && committed.pose != key.pose
            });
            if drained && can_submit && moved && state.pending.is_none() {
                if let Err(error) = animate(world, &mut state, view) {
                    crate::authoring_viewport::report(format!(
                        "Drain des ombres impossible : {error}"
                    ));
                } else {
                    state.committed = Some(key);
                }
            }
            return;
        }
        if let Some((pending, target)) = &state.pending {
            if pending.source != key.source {
                // Deleted/replaced objects and terrain must never publish an obsolete pass.
                state.pending = None;
                state.tracker = None;
            } else if !target.ready.load(Ordering::Acquire) {
                return;
            } else {
                publish(world, target);
                state.committed = Some(pending.clone());
                state.pending = None;
            }
        }
        if state
            .committed
            .as_ref()
            .is_some_and(|committed| committed.source == key.source)
        {
            if let Err(error) = animate(world, &mut state, view) {
                crate::authoring_viewport::report(format!(
                    "Ombres végétales non animées : {error}"
                ));
            }
            state.committed = Some(key);
            return;
        }
        if state.failed.as_ref() == Some(&key) {
            return;
        }
        let sun = world
            .resource::<ResidentEnvironment>()
            .scene_lighting
            .sun_direction
            .truncate()
            .as_dvec3()
            .normalize();
        match live::Tracker::new(sun)
            .and_then(|tracker| {
                prepare(world, &mut state, &key.source, view).map(|input| (tracker, input))
            })
            .and_then(|(tracker, input)| graph::stage(world, input).map(|target| (tracker, target)))
        {
            Ok((tracker, target)) => {
                state.pending = Some((key, target));
                state.tracker = Some(tracker);
                state.failed = None;
                state.generations = state.generations.saturating_add(1);
            }
            Err(error) => {
                state.failed = Some(key);
                crate::authoring_viewport::report(format!(
                    "Ombres importées non actualisées : {error}"
                ));
            }
        }
    });
    publish_diagnostics(world);
}

/// Render-pass counters are diagnostic evidence, not GPU completion fences.
#[allow(
    clippy::too_many_lines,
    reason = "The callback assembles one public observation of shadow sources and player locomotion."
)]
fn publish_diagnostics(world: &mut World) {
    let now = world.resource::<Time<Real>>().elapsed_secs_f64();
    if now - world.resource::<State>().diagnostics_time < 0.25 {
        return;
    }
    world.resource_mut::<State>().diagnostics_time = now;
    let state = world.resource::<State>();
    let Some(animation) = world.get_resource::<graph::animation::State>() else {
        return;
    };
    let statistics = animation.statistics();
    // Keep the new clock fields outside the large established diagnostic
    // macro; adding them there exceeds serde_json's expansion depth.
    let clock_diagnostics = serde_json::json!({
        "wind_pose": {
            "time": state.clock.pose().time,
            "direction": state.clock.pose().dir,
            "speed": state.clock.pose().speed,
            "offset": state.clock.pose().offset,
            "frame_index": state.clock.pose().index,
        },
        "scene_wind": world.get_resource::<forge_tidewater::scene_wind::WindFrame>()
            .map(|wind| serde_json::json!({ "time": wind.time_seconds(),
                "direction": wind.wind().direction, "speed": wind.wind().speed })),
        "virtual_time_s": world.resource::<Time<Virtual>>().elapsed_secs_f64(),
        "cloud_clock": forge_tidewater::tidewater_clouds::live::submitted_frame(world).ok()
            .map(|frame| serde_json::json!({ "submitted_step": frame.step,
                "frame_index": frame.clock.x, "time": frame.clock.y, "dt": frame.clock.z,
                "encoded_step": world.get_resource::<forge_tidewater::tidewater_clouds::Targets>()
                    .map(|targets| targets.completed_steps.load(Ordering::Acquire)),
                "scope": "submitted uniforms and encoded commands, not GPU completion or pixel parity" })),
    });
    let mut diagnostics = serde_json::json!({
        "refraction_capture": world.get_resource::<forge_tidewater::tidewater_refraction::Observation>()
            .map(|observation| serde_json::json!({ "state": observation,
                "gpu": world.get_resource::<forge_tidewater::tidewater_refraction::GpuObservation>(),
                "encoded_frames": world.get_resource::<forge_tidewater::tidewater_refraction::Targets>()
                    .map(|targets| targets.encoded_frames.load(std::sync::atomic::Ordering::Acquire)) })),
        "shadow_receiver_gpu": world.get_resource::<forge_tidewater::tidewater_object_shadow::receiver::GpuObservation>()
            .and_then(|observation| observation.0.as_ref())
            .map(|sample| serde_json::json!({ "enabled":sample.enabled, "control":sample.control,
                "count":sample.count,"map_size":sample.map_size,"frame_index":sample.frame_index,
                "camera":sample.camera,"forward":sample.forward,"matrices":sample.matrices,
                "cascades":sample.cascades })),
        "generations": state.generations,
        "opaque_caster_parts": state.committed.as_ref().map(|key| key.source.roots.len()),
        "pending": state.pending.is_some(),
        "wind_time_s": state.clock.pose().time,
        "encoded_frames": statistics.submissions,
        "cascade_layers": statistics.layers,
        "recovered_frames": statistics.recovered,
        "sequence": statistics.last_sequence,
        "layer_mask": statistics.last_mask,
        "plant_lighting_refreshes": crate::authoring_imported_plants::lighting_refresh_count(world),
        "plant_caster_selection": crate::authoring_imported_plants::shadow_selection_observation(world),
        "comparison_hold": crate::authoring_immersion::held(world),
        "camera_signature": crate::authoring_immersion::camera_signature(world),
        "ocean_clock": forge_tidewater::tidewater_ocean::live_clock(world).map(|clock| {
            let clock = clock.snapshot();
            serde_json::json!({"paused": clock.paused, "quiescent": clock.quiescent,
                "next_step": clock.next_step, "simulation_time": clock.simulation_time,
                "completed": clock.completed.map(|step| serde_json::json!({"step": step.step, "time": step.time, "dt": step.dt})),
            })
        }),
        "underwater": world.get_resource::<forge_tidewater::tidewater_underwater::UnderwaterObservation>().map(|observation| serde_json::json!({
            "immersion": format!("{:?}", observation.immersion),
            "enabled": observation.effect_enabled, "active": observation.effect_active(),
            "pipeline_ready": observation.pipeline_ready(),
            "water_height": observation.water_height_m, "camera_height": observation.camera_height_m,
            "camera_depth": observation.camera_depth_m, "sample_step": observation.sample_step,
            "sample_time": observation.sample_time_s, "encoded_frames": observation.encoded_frames,
            "enabled_frames": observation.encoded_enabled_frames, "bypassed_frames": observation.encoded_bypassed_frames,
            "shafts_strength": observation.shafts_strength, "shaft_frames": observation.encoded_shaft_frames,
        })),
        "caustics": world.get_resource::<forge_tidewater::tidewater_caustics::CausticMaps>().map(|maps| serde_json::json!({
            "encoded_frames": maps.encoded_frames.load(Ordering::Acquire),
            "encoded_fft_step": maps.encoded_fft_step.load(Ordering::Acquire),
        })),
        "underwater_light_maps": world.get_resource::<forge_tidewater::tidewater_underwater_lighting::Targets>().map(|maps| {
            let observed = maps.observation.lock().ok().map(|state| *state);
            serde_json::json!({
                "encoded_frames": maps.encoded_frames.load(Ordering::Relaxed),
                "control": world.get_resource::<forge_tidewater::tidewater_underwater_lighting::Control>()
                    .map(|control| serde_json::json!({ "enabled": control.enabled, "caustics_strength": control.caustics_strength,
                        "shadow_refraction": control.shadow_refraction })),
                "terrain_receivers": world.get_resource::<Assets<forge_tidewater::tidewater_terrain::lod::LodMaterial>>()
                    .map(|materials| materials.iter().filter(|(_, material)| material.base.underwater_map_frame == maps.bindings.frame)
                        .map(|(_, material)| serde_json::json!({ "enabled": material.base.caustic_params.water_scattering.w,
                            "strength": material.base.caustic_params.sun_and_strength.w,
                            "sun": material.base.caustic_params.sun_and_strength.truncate().to_array(),
                            "sea_and_reach": [material.base.caustic_params.tiles_m.z, material.base.caustic_params.tiles_m.w],
                            "shadow_refraction": material.base.caustic_params.water_absorption.w })).collect::<Vec<_>>()),
                "lite_receivers": world.get_resource::<Assets<forge_tidewater::tidewater_material::VillageMaterial>>()
                    .map(|materials| materials.iter().filter(|(_, material)| material.underwater_map_frame == maps.bindings.frame)
                        .map(|(_, material)| serde_json::json!({ "enabled": material.underwater_params.water_scattering.w,
                            "strength": material.underwater_params.sun_and_strength.w,
                            "sun": material.underwater_params.sun_and_strength.truncate().to_array(),
                            "sea_and_reach": [material.underwater_params.tiles_m.z, material.underwater_params.tiles_m.w],
                            "shadow_refraction": material.underwater_params.water_absorption.w })).collect::<Vec<_>>()),
                "gpu": observed.map(|state| serde_json::json!({ "origins": state.origins, "step": state.step,
                    "time": state.time, "resolution": state.resolution, "layers": state.layers })),
            })
        }),
        "water_surface_queries": world.get_resource::<forge_tidewater::tidewater_surface_queries::Targets>().map(|targets| serde_json::json!({
            "encoded_frames": targets.encoded_frames.load(Ordering::Relaxed),
            "rejected_packets": targets.rejected_packets.load(Ordering::Relaxed),
            "sample": targets.queries.latest(),
        })),
        "player_locomotion": world.get_resource::<forge_tidewater::AmphibiousPlayerState>().map(|player| serde_json::json!({
            "mode": format!("{:?}", player.motion.mode),
            "anchor": player.motion.position.to_array(),
            "velocity": player.motion.velocity.to_array(),
            "grounded": player.motion.grounded,
            "body_grounded": world.get_resource::<forge_engine::PlayerRuntimeSnapshot>().map(|snapshot| snapshot.grounded),
            "rapier_grounded": player.rapier_grounded(),
            "rapier_sliding_down_slope": player.rapier_sliding_down_slope(),
            "floating": player.motion.floating,
            "water_height": player.motion.water_h,
            "water_mean": player.motion.water_mean,
            "ground_height": player.ground_height,
            "water_query_step": player.water_query_step,
            "water_query_time": player.water_query_time,
            "water_query_position": player.water_query_position,
            "swim_entries": player.swim_entries,
            "walk_returns": player.walk_returns,
        })),
    });
    if let serde_json::Value::Object(fields) = clock_diagnostics {
        diagnostics
            .as_object_mut()
            .expect("diagnostics are a JSON object")
            .extend(fields);
    }
    diagnostics["source_prop_projection"] = prop_observation::observe(world);
    if let Some(canvas) = web_sys::window()
        .and_then(|window| window.document())
        .and_then(|document| document.get_element_by_id("forge-authoring-canvas"))
    {
        let _ = canvas.set_attribute("data-tidewater-live-shadows", &diagnostics.to_string());
    }
}
fn prepare(
    world: &World,
    state: &mut State,
    source: &Source,
    view: CascadeView,
) -> Result<graph::Input, String> {
    let (document, _) =
        crate::authoring_imported_terrain::shadow_source(world).ok_or("Terrain non prêt")?;
    if state.terrain.as_ref().is_none_or(|(generation, cached)| {
        *generation != source.library || cached.document_hash != source.terrain
    }) {
        let package = imported_buildings::terrain_package().ok_or("Paquet terrain absent")?;
        let shader = std::str::from_utf8(
            package
                .file("terrain-lod.wgsl")
                .ok_or("Shader relief absent")?,
        )
        .map_err(|e| e.to_string())?;
        let prepared = terrain::authored::PreparedSource::new(
            document,
            crate::authoring_raster::source_bytes(document)?.to_vec(),
            shader,
        )
        .map_err(|e| e.to_string())?;
        state.terrain = Some((source.library, prepared));
    }
    let terrain = state
        .terrain
        .as_ref()
        .ok_or("Cache terrain absent")?
        .1
        .at(view)
        .map_err(|e| e.to_string())?;
    let sun = world
        .resource::<ResidentEnvironment>()
        .scene_lighting
        .sun_direction
        .truncate()
        .as_dvec3()
        .normalize();
    let mut draws = scene::buildings(world)?;
    if let Some(debris) =
        world.get_resource::<forge_tidewater::tidewater_debris::scene::ResidentScene>()
    {
        draws.extend(debris.shadow_draws(world)?);
    }
    let mut input = scene::prepare(view, sun, draws, Some(terrain))?;
    if source.plants.is_some() && input.settings.enabled > 0.5 {
        input.plants = Some(
            authoring_imported_plants::shadow_input(world, input.settings.camera)
                .ok_or("Plant shadow source absent")??,
        );
        if let Some(plants) = &mut input.plants {
            let pose = state.clock.pose();
            plants.initial_motion = Some(graph::animation::Motion {
                main_camera: plants.camera,
                wind_time: Vec4::new(pose.dir[0], pose.dir[1], pose.speed, pose.time),
                gust_frame: Vec4::new(
                    pose.offset[0],
                    pose.offset[1],
                    f32::from(u8::try_from(pose.index % 64).expect("six frame bits")),
                    0.0,
                ),
            });
        }
    }
    Ok(input)
}

/// One source wind pose drives visible leaves and the resident shadow atlas.
/// Camera motion updates only the due cascade layers; content edits restage above.
fn animate(world: &mut World, state: &mut State, view: CascadeView) -> Result<(), String> {
    if !world
        .get_resource::<graph::animation::State>()
        .is_some_and(graph::animation::State::can_submit)
    {
        return Ok(());
    }
    let now = world.resource::<Time>().elapsed_secs_f64();
    // Engine.js caps the source application's frame delta at 0.1 seconds.
    // A delayed shadow submission must not accelerate leaves relative to the
    // ocean and cloud transactions by consuming the whole waiting interval.
    let dt = state
        .previous_time
        .map_or(0.0, |previous| (now - previous).min(0.1));
    let mut clock = state.clock.clone();
    let pose = if crate::authoring_immersion::held(world) {
        clock.pose()
    } else {
        // Visible leaves and their shadow deformation must use the same
        // imported-scene wind as the source materials and pier pendulums.
        let wind = world
            .resource::<forge_tidewater::scene_wind::WindFrame>()
            .wind();
        clock
            .advance(dt, wind.direction, wind.speed)
            .map_err(str::to_owned)?
    };
    let frame_index = u8::try_from(pose.index % 64).expect("six frame bits");
    let motion = graph::animation::Motion {
        main_camera: view.world_from_view.w_axis.as_vec4().truncate().extend(0.0),
        wind_time: Vec4::new(pose.dir[0], pose.dir[1], pose.speed, pose.time),
        gust_frame: Vec4::new(pose.offset[0], pose.offset[1], f32::from(frame_index), 0.0),
    };
    let has_plants = state
        .committed
        .as_ref()
        .is_some_and(|key| key.source.plants.is_some());
    if world
        .get_resource::<Receiver>()
        .is_some_and(|receiver| receiver.0.settings.enabled > 0.5)
    {
        let terrain = state
            .terrain
            .as_ref()
            .ok_or("Cache terrain absent pour les ombres animées")?
            .1
            .select(view)
            .map_err(|error| error.to_string())?;
        let plants = if has_plants {
            authoring_imported_plants::shadow_streams(world, motion.main_camera)
                .ok_or("Population d'ombres végétales absente")??
        } else {
            Vec::new()
        };
        let tracker = state
            .tracker
            .as_mut()
            .ok_or("Cascades végétales absentes")?;
        let mut frame = tracker.next(view, u32::from(frame_index))?;
        let parts = opaque_parts(world)?;
        let expected = &state
            .committed
            .as_ref()
            .ok_or("Shadow generation absent")?
            .source
            .roots;
        if parts.len() != expected.len()
            || parts
                .iter()
                .zip(expected)
                .any(|((entity, _), expected)| entity != expected)
        {
            tracker.invalidate();
            return Err("Opaque shadow caster identities changed".into());
        }
        frame.opaque_models = Some(parts.into_iter().map(|(_, model)| model).collect());
        frame.terrain = Some(terrain);
        frame.plants = plants;
        frame.validate()?;
        if let Err(error) = graph::animation::submit_frame(world, motion, frame) {
            tracker.invalidate();
            return Err(error);
        }
    }
    if has_plants {
        forge_tidewater::tidewater_plants::motion::update(
            world,
            motion.wind_time,
            motion.gust_frame,
        )?;
    }
    state.clock = clock;
    state.previous_time = Some(now);
    Ok(())
}

fn publish(world: &mut World, target: &graph::Target) {
    for (_, material) in world.resource_mut::<Assets<VillageMaterial>>().iter_mut() {
        material.object_shadow = target.image.clone();
        material.object_settings = target.settings.clone();
    }
    for (_, material) in world.resource_mut::<Assets<TerrainMaterial>>().iter_mut() {
        material.object_shadow = target.image.clone();
        material.object_settings = target.settings.clone();
    }
    for (_, material) in world.resource_mut::<Assets<LodMaterial>>().iter_mut() {
        material.base.object_shadow = target.image.clone();
        material.base.object_settings = target.settings.clone();
    }
    world.insert_resource(Receiver(target.clone()));
}
