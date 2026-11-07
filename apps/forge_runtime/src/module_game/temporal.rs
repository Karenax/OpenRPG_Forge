//! Explicit diagnostics for module materials and the shared temporal path.
//! Geometry encodings and dry-scene receipts do not prove GPU completion,
//! complete hull coverage, visual equivalence or performance.
use bevy::prelude::*;
use forge_engine::source_temporal as temporal;
use std::{
    io::Write,
    path::{Path, PathBuf},
    time::Instant,
};

const OPTION: &str = "FORGE_SOURCE_TEMPORAL_AUX_AUDIT";
const DRY_OPTION: &str = "FORGE_SOURCE_TEMPORAL_DRY_AUDIT";

pub(super) fn enabled() -> bool {
    std::env::var_os(OPTION).is_some()
}

fn prepare(save: &Path, module: &Path) -> Result<Option<PathBuf>, String> {
    let Some(value) = std::env::var_os(OPTION) else {
        return Ok(None);
    };
    let path = PathBuf::from(value);
    if !path.is_absolute() {
        return Err("Temporal audit output must be absolute".into());
    }
    // Reuse the module runtime's confinement, no-reparse and fresh-output guards.
    super::output::prepare(save, Some(&path), module)?;
    Ok(Some(path))
}

#[derive(Resource)]
struct Audit {
    output: PathBuf,
    started: Instant,
    camera: Option<Entity>,
    previous_serial: Option<u64>,
    geometry_encodings: u32,
    samples: Vec<serde_json::Value>,
    written: bool,
}

pub(super) fn install(
    app: &mut App,
    save: &Path,
    module: &Path,
    dry: Option<DryInput>,
) -> Result<(), String> {
    use temporal::{phase::MaterialRole, shaders::Family};
    if let Some(dry) = dry {
        return install_dry(app, dry);
    }
    let Some(output) = prepare(save, module)? else {
        return Ok(());
    };
    temporal::register_material::<forge_engine::source_terrain::lod::LodMaterial>(
        app,
        Family::TerrainLod,
        MaterialRole::Opaque,
    )?;
    temporal::register_material::<forge_engine::source_vegetation::world::WorldPlantMaterial>(
        app,
        Family::WorldPlant,
        MaterialRole::Opaque,
    )?;
    temporal::register_material::<forge_engine::source_surface::SourceSurfaceMaterial>(
        app,
        Family::Village,
        MaterialRole::Opaque,
    )?;
    temporal::phase::register_standard_opaque(app)?;
    temporal::phase::register_source_model_opaque(app)?;
    temporal::phase::register_sky_material::<forge_engine::source_sky::SkyMaterial>(app)?;
    app.insert_resource(Audit {
        output,
        started: Instant::now(),
        camera: None,
        previous_serial: None,
        geometry_encodings: 0,
        samples: Vec::new(),
        written: false,
    })
    .add_systems(Last, observe);
    Ok(())
}

/// Immutable identity of the admitted module inputs. The session does not
/// retain or modify the authoring document during the rendered game.
pub(super) struct DryInput {
    output: PathBuf,
    scene: forge_core::ContentHash,
    revision: u64,
    sources: temporal::hdr::WaterSources,
}

pub(super) fn prepare_dry(
    loaded: &forge_delivery::module_runtime::LoadedModuleZone,
    save: &Path,
    environment: bool,
    source_graphs_present: bool,
) -> Result<Option<DryInput>, String> {
    let Some(value) = std::env::var_os(DRY_OPTION) else {
        return Ok(None);
    };
    if enabled() {
        return Err("Select only one temporal diagnostic".into());
    }
    let output = PathBuf::from(value);
    if !output.is_absolute() {
        return Err("Dry temporal audit output must be absolute".into());
    }
    loaded
        .manifest
        .validate()
        .map_err(|error| error.to_string())?;
    let head = loaded.zone.head();
    head.validate()?;
    let sources = dry_water_sources(
        head.base.terrain.area_id,
        &loaded.manifest.ocean_inputs,
        head.corrections
            .rivers
            .as_ref()
            .is_some_and(|rivers| !rivers.is_empty()),
        source_graphs_present,
    );
    let scene = forge_core::canonical_json_sha256(&(head.content_hash()?, &loaded.manifest))
        .map_err(|error| error.to_string())?;
    let revision = head.revision.get();
    validate_dry_inputs(environment, scene, revision, sources)?;
    super::output::prepare(save, Some(&output), &loaded.package_root)?;
    Ok(Some(DryInput {
        output,
        scene,
        revision,
        sources,
    }))
}

fn dry_water_sources(
    area: forge_core::AreaId,
    oceans: &[forge_project::module_package::ModuleOceanBinding],
    rivers: bool,
    source_graphs_present: bool,
) -> temporal::hdr::WaterSources {
    temporal::hdr::WaterSources {
        ocean: oceans.iter().any(|input| input.area_id == area),
        rivers,
        // Source graph effects are not part of this bounded water census.
        other_or_pending: source_graphs_present,
        complete: true,
    }
}

fn validate_dry_inputs(
    environment: bool,
    scene: forge_core::ContentHash,
    revision: u64,
    sources: temporal::hdr::WaterSources,
) -> Result<(), String> {
    if !environment {
        return Err("Dry temporal audit requires the admitted source environment".into());
    }
    // Check the inputs now as well as when the actual camera is selected.
    temporal::hdr::DryScene::validate(Entity::PLACEHOLDER, scene, revision, sources)
        .map(|_| ())
        .map_err(str::to_owned)
}

#[derive(Resource)]
struct DryAudit {
    input: DryInput,
    started: Instant,
    camera: Option<Entity>,
    previous_serial: Option<u64>,
    dry_scene_receipts: u32,
    resolve_encodings: u32,
    samples: Vec<serde_json::Value>,
    written: bool,
}

fn install_dry(app: &mut App, input: DryInput) -> Result<(), String> {
    use temporal::{phase::MaterialRole, shaders::Family};
    temporal::register_material::<forge_engine::source_terrain::lod::LodMaterial>(
        app,
        Family::TerrainLod,
        MaterialRole::Opaque,
    )?;
    temporal::register_material::<forge_engine::source_vegetation::world::WorldPlantMaterial>(
        app,
        Family::WorldPlant,
        MaterialRole::Opaque,
    )?;
    temporal::register_material::<forge_engine::source_surface::SourceSurfaceMaterial>(
        app,
        Family::Village,
        MaterialRole::Opaque,
    )?;
    if app
        .world()
        .contains_resource::<Assets<forge_engine::source_fish::material::FishMaterial>>()
    {
        temporal::register_material::<forge_engine::source_fish::material::FishMaterial>(
            app,
            Family::Fish,
            MaterialRole::Opaque,
        )?;
    }
    temporal::phase::register_standard_opaque(app)?;
    temporal::phase::register_source_model_opaque(app)?;
    temporal::phase::register_sky_material::<forge_engine::source_sky::SkyMaterial>(app)?;
    temporal::render_owned::register(app)?;
    app.insert_resource(DryAudit {
        input,
        started: Instant::now(),
        camera: None,
        previous_serial: None,
        dry_scene_receipts: 0,
        resolve_encodings: 0,
        samples: Vec::new(),
        written: false,
    })
    .add_systems(Last, observe_dry);
    Ok(())
}

/// Recheck live producers before every extraction; a missing producer alone
/// never proves a dry scene. New module water producers must join this census.
fn reject_live_water(world: &mut World) -> Result<(), String> {
    if world.contains_resource::<forge_engine::source_water_host::Status>()
        || world.contains_resource::<forge_engine::source_surface_queries::Targets>()
        || world.contains_resource::<forge_engine::source_ocean::Targets>()
        || world.contains_resource::<forge_engine::source_ocean_geometry::ResidentGeometry>()
        || world.contains_resource::<forge_engine::source_shore_sim::Targets>()
        || world.contains_resource::<forge_engine::source_water_optics::ResidentOptics>()
        || world.contains_resource::<forge_engine::source_breaker_resident::Targets>()
        || world.contains_resource::<forge_engine::source_spray_feedback::LiveFeedback>()
        || world.contains_resource::<forge_engine::source_underwater::SourceUnderwaterControl>()
        || world
            .query_filtered::<Entity, With<forge_engine::source_underwater::SourceUnderwaterCamera>>()
            .iter(world)
            .next()
            .is_some()
        || world
            .query::<&MeshMaterial3d<forge_engine::source_water_optics::OpticalMaterial>>()
            .iter(world)
            .next()
            .is_some()
    {
        return Err("Dry temporal scene has an active or pending water producer".into());
    }
    Ok(())
}

fn observe_dry(world: &mut World) {
    world.resource_scope(|world, mut audit: Mut<DryAudit>| {
        if audit.written {
            return;
        }
        if let Err(error) = step_dry(world, &mut audit) {
            error!(%error, "Dry temporal diagnostic failed");
            if let Err(write_error) = finish_dry(world, &mut audit, Some(&error)) {
                error!(%write_error, "Dry temporal diagnostic could not be saved");
                audit.written = true;
                world.write_message(AppExit::error());
            }
        }
    });
}

#[allow(
    clippy::too_many_lines,
    reason = "The diagnostic checks admission, selection and per-frame receipts together."
)]
fn step_dry(world: &mut World, audit: &mut DryAudit) -> Result<(), String> {
    reject_live_water(world)?;
    if audit.camera.is_none() {
        let ready = world
            .get_resource::<super::graphics::State>()
            .is_some_and(|state| state.ready);
        if ready {
            let mut query = world.query_filtered::<(Entity, &Camera), (
                With<Camera3d>,
                Without<forge_engine::scene_camera::OffscreenSceneCamera>,
            )>();
            let cameras = query
                .iter(world)
                .filter(|(_, camera)| camera.is_active)
                .map(|(entity, _)| entity)
                .collect::<Vec<_>>();
            match cameras.as_slice() {
                [camera] => {
                    let camera = *camera;
                    if world
                        .get::<bevy::anti_alias::taa::TemporalAntiAliasing>(camera)
                        .is_some()
                    {
                        return Err("Dry temporal audit requires a profile without Bevy TAA".into());
                    }
                    if world.get::<bevy::camera::Hdr>(camera).is_some()
                        && world.get::<Msaa>(camera) == Some(&Msaa::Off)
                    {
                        let dry = temporal::hdr::DryScene::validate(
                            camera,
                            audit.input.scene,
                            audit.input.revision,
                            audit.input.sources,
                        )
                        .map_err(str::to_owned)?;
                        world.entity_mut(camera).insert(dry);
                        if let Err(error) = temporal::render_owned::select(world, Some(camera)) {
                            world.entity_mut(camera).remove::<temporal::hdr::DryScene>();
                            return Err(error);
                        }
                        audit.camera = Some(camera);
                    }
                }
                [] => {}
                _ => return Err("Dry temporal audit requires one active 3D camera".into()),
            }
        }
    }
    if let Some(camera) = audit.camera {
        let expected = temporal::hdr::DryScene::validate(
            camera,
            audit.input.scene,
            audit.input.revision,
            audit.input.sources,
        )
        .map_err(str::to_owned)?;
        if world.get::<temporal::hdr::DryScene>(camera) != Some(&expected)
            || world
                .get::<Camera>(camera)
                .is_none_or(|camera| !camera.is_active)
            || world
                .get::<bevy::anti_alias::taa::TemporalAntiAliasing>(camera)
                .is_some()
        {
            return Err("Dry temporal camera or its scene assertion changed".into());
        }
        let hdr = world
            .get_resource::<temporal::hdr::HdrObservations>()
            .and_then(|bus| bus.snapshot(camera));
        if let Some(hdr) = hdr
            && audit.previous_serial != Some(hdr.tag.serial)
        {
            audit.previous_serial = Some(hdr.tag.serial);
            let dry_scene_confirmed = hdr.dry_scene == Some(expected)
                && hdr.underwater_absent
                && hdr.underwater_draws == 0
                && hdr.shaft_draws == 0
                && hdr.errors.is_empty();
            let resolve = world
                .get_resource::<temporal::resolve::ResolveObservations>()
                .and_then(|bus| bus.snapshot(camera))
                .filter(|observation| observation.tag == hdr.tag);
            let auxiliary = world
                .get_resource::<temporal::render::AuxObservations>()
                .and_then(|bus| bus.snapshot_receipt(camera));
            let auxiliary = auxiliary_report(hdr.tag, auxiliary.as_ref());
            let resolved = resolve
                .as_ref()
                .is_some_and(temporal::resolve::ResolveObservation::complete);
            audit.dry_scene_receipts += u32::from(dry_scene_confirmed);
            audit.resolve_encodings += u32::from(resolved);
            if audit.samples.len() == 64 {
                audit.samples.remove(0);
            }
            audit.samples.push(serde_json::json!({
                "serial": hdr.tag.serial,
                "dry_scene_confirmed": dry_scene_confirmed,
                "underwater_draws": hdr.underwater_draws,
                "shaft_draws": hdr.shaft_draws,
                "hdr_complete": hdr.complete(),
                "hdr_errors": hdr.errors,
                "auxiliary": auxiliary,
                "resolve_complete": resolved,
                "resolve_auxiliary_generation": resolve.as_ref().map(|value| value.auxiliary_generation),
                "resolve_error": resolve.and_then(|value| value.error).map(|error| error.to_string()),
            }));
        }
    }
    if audit.dry_scene_receipts >= 8 {
        finish_dry(world, audit, None)?;
    } else if audit.started.elapsed().as_secs() >= 60 {
        finish_dry(world, audit, Some("Dry-scene receipt did not become ready"))?;
    }
    Ok(())
}

fn auxiliary_report(
    tag: forge_engine::scene_camera::FrameTag,
    receipt: Option<&temporal::render::AuxReceipt>,
) -> serde_json::Value {
    use temporal::render::HullObservation;
    let Some(receipt) = receipt.filter(|receipt| receipt.observation.tag == tag) else {
        return serde_json::json!({"state": "waiting", "reason": "same_frame_auxiliary_unavailable"});
    };
    let value = &receipt.observation;
    let hull = match receipt.hull {
        None => serde_json::json!({"state": "not_reported"}),
        Some(HullObservation::Waiting) => serde_json::json!({"state": "waiting"}),
        Some(HullObservation::NoHullsAttested) => serde_json::json!({"state": "absence_attested"}),
        Some(HullObservation::MaskEncoded {
            registered,
            active,
            draws,
        }) => serde_json::json!({
            "state": "mask_encoded", "registered": registered, "active": active, "encoded_draws": draws,
        }),
    };
    serde_json::json!({
        "state": "observed",
        "view": value.tag.view.to_bits(),
        "serial": value.tag.serial,
        "generation": value.generation,
        "complete": value.complete(),
        "hull": hull,
        "hull_draws": value.hull_draws,
        "hull_contract_satisfied": value.hull_contract_satisfied,
        "coverage": {
            "complete": value.coverage.complete(),
            "visible": value.coverage.visible,
            "queued": value.coverage.queued,
            "unsupported": value.coverage.unsupported,
            "pending": value.coverage.pending,
            "water": value.coverage.water,
            "errors": value.coverage.errors,
        },
        "errors": value.errors,
    })
}

fn finish_dry(world: &mut World, audit: &mut DryAudit, error: Option<&str>) -> Result<(), String> {
    if let Some(camera) = audit.camera.take() {
        temporal::render_owned::select(world, None)?;
        if let Ok(mut entity) = world.get_entity_mut(camera) {
            entity.remove::<temporal::hdr::DryScene>();
        }
    }
    let report = serde_json::json!({
        "schema": "forge.module.temporal-dry-scene-audit.v1",
        "scene": audit.input.scene.to_string(),
        "revision": audit.input.revision,
        "dry_scene_receipts": audit.dry_scene_receipts,
        "resolve_encodings": audit.resolve_encodings,
        "error": error,
        "samples": audit.samples,
        "gpu_readback_proven": false,
        "gpu_completion_proven": false,
        "complete_source_chain_qualified": false,
        "performance_qualified": false,
        "gate_closed": false,
    });
    let mut file = std::fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&audit.input.output)
        .map_err(|error| error.to_string())?;
    file.write_all(&serde_json::to_vec_pretty(&report).map_err(|error| error.to_string())?)
        .and_then(|()| file.sync_all())
        .map_err(|error| error.to_string())?;
    audit.written = true;
    world.write_message(if error.is_none() {
        AppExit::Success
    } else {
        AppExit::error()
    });
    Ok(())
}

fn observe(world: &mut World) {
    world.resource_scope(|world, mut audit: Mut<Audit>| {
        if audit.written {
            return;
        }
        let result = step(world, &mut audit);
        if let Err(error) = result {
            error!(%error, "Module temporal auxiliary audit failed");
            world.write_message(AppExit::error());
            audit.written = true;
        }
    });
}

fn step(world: &mut World, audit: &mut Audit) -> Result<(), String> {
    if audit.camera.is_none() {
        let mut query = world.query_filtered::<(Entity, &Camera), (
            With<Camera3d>,
            Without<forge_engine::scene_camera::OffscreenSceneCamera>,
        )>();
        let cameras = query
            .iter(world)
            .filter(|(_, c)| c.is_active)
            .map(|(e, _)| e)
            .collect::<Vec<_>>();
        match cameras.as_slice() {
            [camera] => {
                let camera = *camera;
                if world
                    .get::<bevy::anti_alias::taa::TemporalAntiAliasing>(camera)
                    .is_some()
                {
                    return Err(
                        "Auxiliary audit requires an initial profile without Bevy TAA".into(),
                    );
                }
                if world.get::<bevy::camera::Hdr>(camera).is_none()
                    || world.get::<Msaa>(camera) != Some(&Msaa::Off)
                {
                    return Ok(());
                }
                temporal::select_camera(world, Some(camera))?;
                audit.camera = Some(camera);
            }
            [] => return Ok(()),
            _ => return Err("Auxiliary audit requires one active 3D camera".into()),
        }
    }
    let camera = audit.camera.ok_or("Missing audit camera")?;
    let observation = world
        .get_resource::<temporal::render::AuxObservations>()
        .and_then(|bus| bus.snapshot(camera));
    if let Some(value) = observation
        && audit.previous_serial != Some(value.tag.serial)
    {
        audit.previous_serial = Some(value.tag.serial);
        // Keep the required-hull refusal intact. This counter is only the
        // actual geometry encoding subtotal, never a full auxiliary verdict.
        let graphics_ready = world
            .get_resource::<super::graphics::State>()
            .is_some_and(|state| state.ready);
        let geometry_ready = graphics_ready
            && value.coverage.complete()
            && value.mesh_draws == value.coverage.queued
            && value.sky_draws == 1
            && value
                .errors
                .iter()
                .all(|e| e == "Source hull mask contract is required but no encoder is registered");
        if geometry_ready {
            audit.geometry_encodings += 1;
        }
        if audit.samples.len() == 64 {
            audit.samples.remove(0);
        }
        audit.samples.push(serde_json::json!({
            "serial": value.tag.serial, "view": value.tag.view.to_bits(), "generation": value.generation,
            "coverage_complete": value.coverage.complete(), "visible": value.coverage.visible,
            "queued": value.coverage.queued, "unsupported": value.coverage.unsupported,
            "pending": value.coverage.pending, "opaque": value.coverage.opaque,
            "water": value.coverage.water, "transparent": value.coverage.transparent,
            "mesh_draws": value.mesh_draws, "sky_draws": value.sky_draws,
            "geometry_encoding_ready": geometry_ready, "hull_contract_satisfied": value.hull_contract_satisfied,
            "graphics_ready": graphics_ready,
            "complete_auxiliary": value.complete(), "history_seeded": value.history_seeded,
            "auxiliary_acknowledged": value.auxiliary_acknowledged,
            "errors": value.errors, "coverage_errors": value.coverage.errors,
            "coverage_diagnostics": value.coverage.diagnostics, "draw_diagnostics": value.draw_diagnostics,
        }));
    }
    if audit.geometry_encodings >= 8 || audit.started.elapsed().as_secs() >= 60 {
        finish(world, audit, camera)?;
    }
    Ok(())
}

fn finish(world: &mut World, audit: &mut Audit, camera: Entity) -> Result<(), String> {
    let derivation_errors = world
        .get_resource::<temporal::Derivations>()
        .map_or_else(Vec::new, |d| {
            d.errors().into_iter().map(str::to_owned).collect()
        });
    let frames = world.resource::<temporal::frame::Frames>();
    let report = serde_json::json!({
        "schema": "forge.module.temporal-auxiliary-audit.v1", "nonpipelined_diagnostic": true,
        "geometry_encodings": audit.geometry_encodings, "geometry_encoding_ready": audit.geometry_encodings >= 8,
        "camera": camera.to_bits(), "derivation_errors": derivation_errors,
        "source_model_material_type_id": format!("{:?}", std::any::TypeId::of::<forge_engine::source_lighting::surfaces::ModelMaterial>()),
        "pending_mesh_entities": pending_mesh_entities(world, camera),
        "committed_auxiliary": frames.committed_aux, "committed_resolve": frames.committed_resolve,
        "samples": audit.samples, "gpu_readback_proven": false, "complete_source_chain_qualified": false,
        "performance_qualified": false, "gate_closed": false,
    });
    let mut file = std::fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&audit.output)
        .map_err(|e| e.to_string())?;
    file.write_all(&serde_json::to_vec_pretty(&report).map_err(|e| e.to_string())?)
        .and_then(|()| file.sync_all())
        .map_err(|e| e.to_string())?;
    audit.written = true;
    info!(encodings=audit.geometry_encodings, output=%audit.output.display(), "Module temporal auxiliary audit saved");
    world.write_message(if audit.geometry_encodings >= 8 {
        AppExit::Success
    } else {
        AppExit::error()
    });
    Ok(())
}

fn pending_mesh_entities(world: &World, camera: Entity) -> Vec<serde_json::Value> {
    let Some(observation) = world
        .get_resource::<temporal::render::AuxObservations>()
        .and_then(|bus| bus.snapshot(camera))
    else {
        return Vec::new();
    };
    observation.coverage.diagnostics.iter().filter(|d| d.stage == "queue.mesh-instance").take(64).map(|d| {
        let entity = Entity::from_bits(d.main_entity_bits);
        let mesh = world.get::<Mesh3d>(entity);
        let asset = mesh.and_then(|m| world.get_resource::<Assets<Mesh>>().and_then(|assets| assets.get(&m.0)));
        let parent = world.get::<ChildOf>(entity).map(ChildOf::parent);
        serde_json::json!({
            "entity": d.main_entity_bits, "exists": world.get_entity(entity).is_ok(),
            "name": world.get::<Name>(entity).map(ToString::to_string),
            "parent": parent.map(Entity::to_bits), "parent_name": parent.and_then(|e| world.get::<Name>(e)).map(ToString::to_string),
            "visibility": format!("{:?}", world.get::<Visibility>(entity)),
            "inherited_visible": world.get::<InheritedVisibility>(entity).map(|v| v.get()),
            "view_visible": world.get::<ViewVisibility>(entity).map(|v| v.get()),
            "render_layers": format!("{:?}", world.get::<bevy::camera::visibility::RenderLayers>(entity)),
            "render_entity": world.get::<bevy::render::sync_world::RenderEntity>(entity).map(|e| e.id().to_bits()),
            "mesh": mesh.map(|m| format!("{:?}", m.0.id())), "mesh_loaded": asset.is_some(),
            "mesh_usage": asset.map(|m| format!("{:?}", m.asset_usage)),
            "position": world.get::<GlobalTransform>(entity).map(|t| t.translation().to_array()),
            "physics_projection": forge_engine::source_terrain::host::owns_physics_projection(world, entity),
            "inactive_source_tile": forge_engine::source_terrain::host::inactive_tile(world, entity),
        })
    }).collect()
}

#[cfg(test)]
mod dry_tests {
    use super::*;

    fn auxiliary_receipt(
        tag: forge_engine::scene_camera::FrameTag,
        hull: Option<temporal::render::HullObservation>,
    ) -> temporal::render::AuxReceipt {
        temporal::render::AuxReceipt {
            observation: temporal::render::AuxObservation {
                tag,
                generation: 7,
                coverage: temporal::phase::AuxCoverage {
                    visible: 1,
                    registered: 1,
                    queued: 1,
                    opaque: 1,
                    ..default()
                },
                mesh_draws: 1,
                sky_draws: 1,
                hull_draws: 0,
                hull_contract_satisfied: true,
                final_depth_copied: false,
                previous_depth_copied: false,
                history_seeded: false,
                auxiliary_acknowledged: false,
                draw_diagnostics: Vec::new(),
                draw_diagnostics_omitted: 0,
                errors: vec!["existing observation".into()],
            },
            hull,
        }
    }

    #[test]
    fn auxiliary_report_joins_only_the_same_frame_and_never_infers_absence() {
        use temporal::render::HullObservation;
        let view = Entity::from_raw_u32(12).unwrap();
        let tag = forge_engine::scene_camera::FrameTag { view, serial: 1 };
        let mut receipt = auxiliary_receipt(tag, None);
        let old = auxiliary_report(tag, Some(&receipt));
        assert_eq!(old["hull"]["state"], "not_reported");
        assert_eq!(old["hull_contract_satisfied"], true);
        assert_eq!(old["hull_draws"], 0);
        assert_eq!(old["errors"][0], "existing observation");
        assert_eq!(old["generation"], 7);

        receipt.hull = Some(HullObservation::Waiting);
        assert_eq!(
            auxiliary_report(tag, Some(&receipt))["hull"]["state"],
            "waiting"
        );
        receipt.hull = Some(HullObservation::NoHullsAttested);
        assert_eq!(
            auxiliary_report(tag, Some(&receipt))["hull"]["state"],
            "absence_attested"
        );
        receipt.hull = Some(HullObservation::MaskEncoded {
            registered: 2,
            active: 0,
            draws: 0,
        });
        let outside = auxiliary_report(tag, Some(&receipt));
        assert_eq!(outside["hull"]["state"], "mask_encoded");
        assert_eq!(outside["hull"]["registered"], 2);
        assert_eq!(outside["hull"]["encoded_draws"], 0);
        receipt.hull = Some(HullObservation::MaskEncoded {
            registered: 2,
            active: 1,
            draws: 1,
        });
        assert_eq!(
            auxiliary_report(tag, Some(&receipt))["hull"]["encoded_draws"],
            1
        );

        let later = forge_engine::scene_camera::FrameTag { serial: 2, ..tag };
        assert_eq!(auxiliary_report(later, Some(&receipt))["state"], "waiting");
        let other = forge_engine::scene_camera::FrameTag {
            view: Entity::from_raw_u32(13).unwrap(),
            ..tag
        };
        assert_eq!(auxiliary_report(other, Some(&receipt))["state"], "waiting");
        assert_eq!(auxiliary_report(tag, None)["state"], "waiting");
    }

    #[test]
    fn scene_inventory_checks_the_selected_area_and_all_declared_water() {
        use forge_project::module_package::{ModuleFilePin, ModuleOceanBinding, ModuleOceanFiles};
        let selected = forge_core::AreaId::deterministic("dry-selected");
        let other = forge_core::AreaId::deterministic("dry-other");
        let binding = ModuleOceanBinding {
            area_id: other,
            package: ModuleOceanFiles {
                manifest: ModuleFilePin {
                    path: "ocean.json".into(),
                    sha256: forge_core::ContentHash::from_bytes([7; 32]),
                },
                resources: vec![],
            },
            surface: None,
            shore_sim: None,
        };
        let sources = dry_water_sources(selected, std::slice::from_ref(&binding), false, false);
        assert!(!sources.ocean);
        assert!(dry_water_sources(other, &[binding], false, false).ocean);
        assert!(dry_water_sources(selected, &[], true, false).rivers);
        assert!(dry_water_sources(selected, &[], false, true).other_or_pending);
    }

    #[test]
    fn dry_input_refuses_missing_environment_and_water_sources() {
        let scene = forge_core::ContentHash::from_bytes([7; 32]);
        let dry = temporal::hdr::WaterSources {
            ocean: false,
            rivers: false,
            other_or_pending: false,
            complete: true,
        };
        assert!(validate_dry_inputs(true, scene, 1, dry).is_ok());
        assert!(validate_dry_inputs(false, scene, 1, dry).is_err());
        for rejected in [
            temporal::hdr::WaterSources { ocean: true, ..dry },
            temporal::hdr::WaterSources {
                rivers: true,
                ..dry
            },
            temporal::hdr::WaterSources {
                other_or_pending: true,
                ..dry
            },
            temporal::hdr::WaterSources {
                complete: false,
                ..dry
            },
        ] {
            assert!(validate_dry_inputs(true, scene, 1, rejected).is_err());
        }
    }

    #[test]
    fn live_water_invalidates_the_dry_assertion() {
        let mut world = World::new();
        assert!(reject_live_water(&mut world).is_ok());
        world.insert_resource(forge_engine::source_underwater::SourceUnderwaterControl::default());
        assert!(reject_live_water(&mut world).is_err());
        world.remove_resource::<forge_engine::source_underwater::SourceUnderwaterControl>();
        let camera = world
            .spawn(forge_engine::source_underwater::SourceUnderwaterCamera)
            .id();
        assert!(reject_live_water(&mut world).is_err());
        world.despawn(camera);
        assert!(reject_live_water(&mut world).is_ok());
    }
}
