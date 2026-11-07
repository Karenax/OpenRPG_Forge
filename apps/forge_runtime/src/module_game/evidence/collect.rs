use super::super::evidence_contract::{
    ExpectedManual, ObservedManual, ObservedSceneWind, ObservedSourceParticles,
    ObservedSourcePhysics, ObservedVisual, ProjectionObservation, verify,
};
use super::{Context, FrameTiming, MAX_FRAME_TIMINGS};
use bevy::{
    app::AppExit,
    asset::AssetServer,
    prelude::*,
    world_serialization::{WorldAssetRoot, WorldInstance, WorldInstanceSpawner},
};
use bevy_rapier3d::prelude::Collider;
use forge_engine::{
    DomainEntityId, ManualPlacementCollisionExpected, ManualPlacementCollisionNotRequired,
    ManualPlacementCollisionStatus, ManualPlacementPart, ManualPlacementProjection,
    gameplay::{
        GameplayBody, GameplayRuntime,
        visuals::{GameplayVisualStatus, GovernedGameplayVisual},
    },
};

pub(super) fn observe(world: &mut World) {
    let context = world.resource::<Context>().clone();
    let frame = world.resource::<bevy::diagnostic::FrameCount>().0;
    let capture_issued_frame = forge_engine::issued_capture_frame(world);
    let (renderer, cached) = if let Ok(shared) = context.bridge.0.lock() {
        (shared.renderer.clone(), shared.functional_identity)
    } else {
        world.write_message(AppExit::error());
        return;
    };
    let mut observation = ProjectionObservation {
        wind: scene_wind(world),
        local_lights: super::super::lights::observe(world),
        environment: super::super::environment::observe(world),
        graphics: super::super::graphics::observe(world),
        imported: super::super::imported::observe(world),
        species: super::super::species::observe(world),
        pid: std::process::id(),
        frame,
        module_id: None,
        area_id: None,
        source_hash: None,
        functional_hash: None,
        gameplay_ready: false,
        tick: 0,
        simulation_error: None,
        visual_error: None,
        visual_expected: 0,
        visual_spawned: 0,
        visual_ready: 0,
        manual_projection_ready: false,
        visuals: vec![],
        manual: vec![],
        building_source_hash: None,
        building_count: 0,
        building_parts: 0,
        registered_building_parts: 0,
        building_projection_ready: false,
        streamed_terrain: None,
        procedural_streaming: None,
        static_hlod: static_hlod(world),
        renderer,
    };
    observation.streamed_terrain = streamed_terrain(world);
    observation.procedural_streaming = procedural_streaming(world);
    if let Some(buildings) = forge_engine::runtime_buildings::observe(world) {
        observation.building_source_hash = Some(buildings.source_hash);
        observation.building_count = buildings.count;
        observation.building_parts = buildings.parts;
        observation.registered_building_parts = buildings.registered_parts;
        observation.building_projection_ready = buildings.ready;
    }
    observe_gameplay(world, &mut observation, cached, &context.manual);
    let asset_failure = world
        .get_resource::<super::super::manual::ModuleAssetFailure>()
        .and_then(super::super::manual::ModuleAssetFailure::reason)
        .map(str::to_owned);
    let fatal = failed(&observation) || asset_failure.is_some();
    let bounded_end = context.last_frame.is_some_and(|last| frame >= last);
    let ready = !fatal && verify(&context.expected, &observation).is_ok();
    let timing = context
        .measure_frames
        .then(|| frame_timing(world, &observation, ready))
        .flatten();
    if let Ok(mut shared) = context.bridge.0.lock() {
        if ready {
            shared.first_projection_ready_frame.get_or_insert(frame);
            shared.last_projection_ready_frame = Some(frame);
        }
        if let Some(mut timing) = timing {
            if shared.frame_timings.len() < MAX_FRAME_TIMINGS {
                shared.metadata.projection_boundary(ready);
                (timing.graphics, timing.imported) = shared.metadata.observe(
                    frame,
                    observation.graphics.as_ref(),
                    observation.imported.as_ref(),
                );
                shared.frame_timings.push(timing);
            } else {
                shared.frame_timings_truncated = true;
            }
        }
        if context.capture_frame.is_some() && capture_issued_frame == Some(frame) {
            shared.capture_snapshot = Some(observation.clone());
        }
        shared.functional_identity = observation.source_hash.zip(observation.functional_hash);
        shared.final_snapshot = Some(observation);
        shared.asset_failure = asset_failure;
    } else {
        world.write_message(AppExit::error());
        return;
    }
    if let Some(mut scenario) =
        world.get_resource_mut::<forge_engine::native_gameplay::NativeScenarioProjection>()
    {
        scenario.frame = frame;
        scenario.ready = ready;
    }
    if fatal || (bounded_end && !ready) {
        world.write_message(AppExit::error());
    }
}

fn scene_wind(world: &World) -> Option<ObservedSceneWind> {
    let wind = world
        .get_resource::<forge_engine::source_wind::SceneWind>()?
        .wind();
    let surface = forge_engine::source_lighting::surfaces::live::snapshot(world)
        .map(|snapshot| snapshot.material_wind.to_array());
    let cloud = world
        .get_resource::<forge_engine::source_environment::scene::ResidentEnvironment>()
        .map(|environment| &environment.cloud_continuation);
    Some(ObservedSceneWind {
        direction_xz: wind.direction,
        speed_mps: wind.speed,
        surface,
        cloud_direction_xz: cloud.map(|cloud| cloud.input.wind_direction),
        cloud_speed_mps: cloud.and_then(|cloud| cloud.input.wind_speed_mps),
        cloud_step_xz: cloud.map(|cloud| [cloud.last.frame[12][0], cloud.last.frame[12][1]]),
    })
}

fn failed(observation: &ProjectionObservation) -> bool {
    observation
        .environment
        .as_ref()
        .is_some_and(|value| value.error.is_some())
        || observation
            .local_lights
            .as_ref()
            .is_some_and(|value| value.error.is_some())
        || observation
            .graphics
            .as_ref()
            .is_some_and(|value| value.error.is_some())
        || observation
            .imported
            .as_ref()
            .is_some_and(|value| value.error.is_some())
        || observation
            .species
            .as_ref()
            .is_some_and(|value| value.status.error.is_some())
        || observation
            .procedural_streaming
            .as_ref()
            .is_some_and(|value| value.error.is_some())
        || observation.simulation_error.is_some()
        || observation.visual_error.is_some()
        || observation.renderer.failed > 0
}

fn frame_timing(
    world: &World,
    observed: &ProjectionObservation,
    ready: bool,
) -> Option<FrameTiming> {
    let stats = observed.static_hlod.as_ref();
    let frame_ms = world.get_resource::<Time<Real>>()?.delta_secs_f64() * 1000.0;
    if !frame_ms.is_finite() || frame_ms <= 0.0 {
        return None;
    }
    let player = world.get_resource::<forge_engine::PlayerRuntimeSnapshot>()?;
    Some(FrameTiming {
        graphics: None,
        imported: None,
        frame: observed.frame,
        tick: observed.tick,
        frame_ms,
        player_position: player.player_position.to_array(),
        camera: [
            player.camera_yaw_radians,
            player.camera_pitch_radians,
            player.camera_boom_distance_m,
            player.camera_vertical_fov_radians,
        ],
        selected_triangles: stats.map(|value| value.selected_triangles),
        rendered_batches: stats.map(|value| value.rendered_batches),
        generation: stats.map(|value| value.generation),
        pending: stats.map(|value| value.pending),
        projection_ready: ready,
        simulation_ready: world
            .get_resource::<forge_engine::PlayerSimulationReady>()
            .map(|status| status.0),
    })
}

fn streamed_terrain(
    world: &World,
) -> Option<super::super::evidence_contract::ObservedStreamedTerrain> {
    if let (Some(identity), Some(cells), Some(physics), Some(hlod)) = (
        world.get_resource::<forge_engine::StreamedTerrainIdentity>(),
        world.get_resource::<forge_engine::cell_streaming::CellStreamingStats>(),
        world.get_resource::<forge_engine::cell_streaming::PlayerCellStreamingStatus>(),
        world.get_resource::<forge_engine::cell_streaming::TerrainHlodStats>(),
    ) {
        Some(super::super::evidence_contract::ObservedStreamedTerrain {
            identity: super::super::evidence_contract::ExpectedStreamedTerrain {
                document_hash: identity.document_hash,
                raster_hash: identity.raster_hash,
                raster_bytes: identity.raster_bytes,
                maximum_cells: identity.policy.max_resident_cells,
                maximum_cpu_mesh_bytes: identity.policy.max_resident_bytes,
                dimensions: identity.policy.dimensions,
                maximum_staging_cpu_mesh_bytes: identity.policy.max_staging_bytes,
            },
            resident_cells: cells.resident_cells,
            detailed_cells: cells.detailed_cells,
            coarse_cells: cells.coarse_cells,
            cpu_mesh_bytes: cells.cpu_mesh_bytes,
            resident_triangles: cells.resident_triangles,
            committed_transitions: cells.committed_transitions,
            maximum_cells: identity.policy.max_resident_cells,
            maximum_cpu_mesh_bytes: identity.policy.max_resident_bytes,
            pending: cells.pending,
            physics_ready: physics.ready,
            error: cells.last_error.clone().or_else(|| physics.error.clone()),
            source_synchronized: forge_engine::cell_streaming::source_is_synchronized(
                world,
                identity.document_hash,
            ),
            hlod: super::super::evidence_contract::ObservedTerrainHlod {
                tiles: hlod.tiles,
                aggregated_tiles: hlod.aggregated_tiles,
                covered_base_cells: hlod.covered_base_cells,
                maximum_level: hlod.maximum_level,
                cpu_mesh_bytes: hlod.cpu_mesh_bytes,
                resident_triangles: hlod.resident_triangles,
                peak_staging_mesh_bytes: hlod.peak_staging_mesh_bytes,
            },
        })
    } else {
        None
    }
}

fn procedural_streaming(
    world: &World,
) -> Option<super::super::evidence_contract::ObservedProceduralTerrain> {
    use super::super::evidence_contract::{
        ExpectedProceduralTerrain, ObservedProceduralTerrain, ObservedTerrainHlod,
    };
    let identity = world.get_resource::<super::super::streamed::ProceduralTerrainIdentity>()?;
    let selection_matches =
        forge_engine::cell_streaming::render_selection(world).is_some_and(|selection| {
            selection.source == identity.document_hash && selection.policy == &identity.policy
        });
    let cells = world.get_resource::<forge_engine::cell_streaming::CellStreamingStats>()?;
    let physics =
        world.get_resource::<forge_engine::cell_streaming::PlayerCellStreamingStatus>()?;
    let hlod = world.get_resource::<forge_engine::cell_streaming::TerrainHlodStats>()?;
    Some(ObservedProceduralTerrain {
        identity: ExpectedProceduralTerrain {
            document_hash: identity.document_hash,
            maximum_cells: identity.policy.max_resident_cells,
            maximum_cpu_mesh_bytes: identity.policy.max_resident_bytes,
            dimensions: identity.policy.dimensions,
            maximum_staging_cpu_mesh_bytes: identity.policy.max_staging_bytes,
        },
        resident_cells: cells.resident_cells,
        detailed_cells: cells.detailed_cells,
        coarse_cells: cells.coarse_cells,
        cpu_mesh_bytes: cells.cpu_mesh_bytes,
        resident_triangles: cells.resident_triangles,
        committed_transitions: cells.committed_transitions,
        pending: cells.pending,
        physics_ready: physics.ready,
        error: cells.last_error.clone().or_else(|| physics.error.clone()),
        source_synchronized: selection_matches
            && forge_engine::cell_streaming::source_is_synchronized(world, identity.document_hash),
        hlod: ObservedTerrainHlod {
            tiles: hlod.tiles,
            aggregated_tiles: hlod.aggregated_tiles,
            covered_base_cells: hlod.covered_base_cells,
            maximum_level: hlod.maximum_level,
            cpu_mesh_bytes: hlod.cpu_mesh_bytes,
            resident_triangles: hlod.resident_triangles,
            peak_staging_mesh_bytes: hlod.peak_staging_mesh_bytes,
        },
    })
}

fn static_hlod(world: &mut World) -> Option<super::super::evidence_contract::ObservedStaticHlod> {
    let stats = world
        .get_resource::<forge_engine::static_hlod::StaticHlodStats>()?
        .clone();
    let covered = forge_engine::static_hlod::covered_sources(world).len();
    Some(super::super::evidence_contract::ObservedStaticHlod {
        enabled: stats.enabled,
        generation: stats.generation,
        source_objects: stats.source_objects,
        source_primitives: stats.source_primitives,
        covered_primitives: covered,
        source_triangles: stats.source_triangles,
        selected_triangles: stats.selected_triangles,
        desired_batches: stats.desired_batches,
        rendered_batches: stats.rendered_batches,
        replaced_primitives: stats.replaced_primitives,
        retained_source_mesh_bytes: stats.retained_source_mesh_bytes,
        source_snapshot_bytes: stats.source_snapshot_bytes,
        derived_mesh_bytes: stats.derived_mesh_bytes,
        peak_tracked_buffer_bound_bytes: stats.peak_tracked_buffer_bound_bytes,
        maximum_position_error_m: stats.maximum_position_error_m,
        maximum_attribute_deltas: stats.maximum_attribute_deltas,
        minimum_face_alignment: stats.minimum_face_alignment,
        committed_transitions: stats.committed_transitions,
        discarded_jobs: stats.discarded_jobs,
        pending: stats.pending,
        error: stats.last_error,
        gpu_upload_ready: forge_engine::static_hlod::gpu_upload_ready(world),
    })
}

fn observe_gameplay(
    world: &mut World,
    observation: &mut ProjectionObservation,
    cached: Option<(forge_core::ContentHash, forge_core::ContentHash)>,
    expected_manual: &[forge_project::zone_authoring::placements::ManualPlacement],
) {
    if let Some(runtime) = world.get_resource::<GameplayRuntime>() {
        let document = runtime.session().document();
        let source = runtime.session().source_hash();
        observation.module_id = Some(document.module_id);
        observation.area_id = Some(document.area_id);
        observation.source_hash = Some(source);
        // GameSession exposes the authored document immutably for this native run.
        observation.functional_hash = cached
            .filter(|(hash, _)| *hash == source)
            .map(|(_, hash)| hash)
            .or_else(|| document.functional_hash().ok());
        observation.gameplay_ready = runtime.is_ready();
        observation.tick = runtime.session().state().tick;
        observation.simulation_error = runtime.error().map(str::to_owned);
    }
    if let Some(status) = world.get_resource::<GameplayVisualStatus>() {
        observation.visual_expected = status.expected;
        observation.visual_spawned = status.spawned;
        observation.visual_ready = status.ready;
        observation.visual_error.clone_from(&status.error);
    }
    observation.visuals = visuals(world);
    observation.manual_projection_ready =
        forge_engine::manual_placements_ready(world, expected_manual);
    if let Some(status) = world.get_resource::<forge_engine::source_fish::host::live::Status>() {
        observation.manual_projection_ready &= status.synchronized && status.error.is_none();
        if observation.visual_error.is_none() {
            observation.visual_error.clone_from(&status.error);
        }
    }
    observation.manual = manual(world);
    if observation.visual_error.is_none() {
        observation.visual_error = observation.manual.iter().find_map(|row| {
            row.source_physics
                .as_ref()
                .and_then(|physics| physics.error.as_ref())
                .map(|reason| format!("Source physics observation refused: {reason}"))
        });
    }
    if observation.visual_error.is_none() {
        observation.visual_error = world
            .get_resource::<forge_engine::source_surface::package::host::Status>()
            .and_then(|status| status.error.clone());
    }
    if observation.visual_error.is_none() {
        observation.visual_error = world
            .query::<&ManualPlacementCollisionStatus>()
            .iter(world)
            .find_map(|status| {
                if let ManualPlacementCollisionStatus::Failed(reason) = status {
                    Some(format!(
                        "Native manual collision projection failed: {reason}"
                    ))
                } else {
                    None
                }
            });
    }
}

fn visuals(world: &mut World) -> Vec<ObservedVisual> {
    let mut query = world.query::<(
        &GovernedGameplayVisual,
        &Transform,
        &ChildOf,
        &WorldAssetRoot,
        Option<&WorldInstance>,
    )>();
    let server = world.resource::<AssetServer>();
    let spawner = world.resource::<WorldInstanceSpawner>();
    query
        .iter(world)
        .map(|(visual, transform, parent, root, instance)| {
            let scene_ready = instance
                .is_some_and(|instance| spawner.instance_is_ready(**instance))
                && server.is_loaded_with_dependencies(root.0.id());
            let mesh_primitives = instance.map_or(0, |instance| {
                spawner
                    .iter_instance_entities(**instance)
                    .filter(|entity| world.get::<Mesh3d>(*entity).is_some())
                    .count()
            });
            ObservedVisual {
                entity_id: visual.target,
                asset: visual.pin,
                transform: domain_transform(*transform),
                parent_identity: world.get::<DomainEntityId>(parent.parent()).map(|id| id.0),
                parent_is_gameplay_body: world.get::<GameplayBody>(parent.parent()).is_some(),
                parent_has_collider: world.get::<Collider>(parent.parent()).is_some(),
                scene_ready,
                mesh_primitives,
            }
        })
        .collect()
}

fn manual(world: &mut World) -> Vec<ObservedManual> {
    world
        .query::<(
            Entity,
            &ManualPlacementProjection,
            &Transform,
            Option<&ManualPlacementCollisionExpected>,
            Option<&ManualPlacementCollisionStatus>,
            Option<&ManualPlacementCollisionNotRequired>,
            Option<&Children>,
        )>()
        .iter(world)
        .map(
            |(root, placement, transform, expected, status, marker, children)| {
                let non_colliding = placement.0.particles.is_none()
                    && marker.is_some_and(|marker| marker.0 == placement.0.id);
                // The host's owned-part lookup is indexed; avoid rebuilding a
                // map of all fish roots on every gameplay observation frame.
                let fish = non_colliding
                    && children.is_some_and(|children| {
                        children.iter().any(|child| {
                            world
                                .get::<ManualPlacementPart>(child)
                                .is_some_and(|part| part.0 == placement.0.id)
                                && forge_engine::source_fish::host::live::owns(world, child)
                        })
                    });
                let mesh_primitives =
                    if placement.0.physics.is_some() || placement.0.particles.is_some() {
                        world
                        .get_resource::<forge_engine::source_graph_runtime::SourceSceneResidents>()
                        .and_then(|residents| {
                            residents.primitives_for_carrier(root, placement.0.asset_hash)
                        })
                        .map_or(0, |primitives| {
                            primitives
                                .iter()
                                .filter(|&&entity| {
                                    world.get::<Mesh3d>(entity).is_some()
                                        && world
                                            .get::<ManualPlacementPart>(entity)
                                            .is_some_and(|part| part.0 == placement.0.id)
                                })
                                .count()
                        })
                    } else if non_colliding {
                        children.map_or(0, |children| {
                            children
                                .iter()
                                .filter(|child| {
                                    world.get::<Mesh3d>(*child).is_some()
                                        && world
                                            .get::<ManualPlacementPart>(*child)
                                            .is_some_and(|part| part.0 == placement.0.id)
                                        && (!fish
                                            || forge_engine::source_fish::host::live::owns(
                                                world, *child,
                                            ))
                                })
                                .count()
                        })
                    } else {
                        expected.map_or(0, |expected| expected.primitives.len())
                    };
                ObservedManual {
                    identity: ExpectedManual {
                        entity_id: placement.0.id,
                        asset: forge_project::module_gameplay_visuals::VisualAssetPin {
                            asset_id: placement.0.asset_id,
                            sha256: placement.0.asset_hash,
                        },
                        transform: domain_transform(*transform),
                        non_colliding,
                        fish,
                        source_physics: placement.0.physics.clone(),
                        source_particles: placement.0.particles.clone(),
                    },
                    mesh_primitives,
                    registered_primitives: match status {
                        Some(ManualPlacementCollisionStatus::Registered { primitives }) => {
                            *primitives
                        }
                        Some(ManualPlacementCollisionStatus::Waiting { registered, .. }) => {
                            *registered
                        }
                        _ => 0,
                    },
                    source_physics: placement
                        .0
                        .physics
                        .as_ref()
                        .map(|_| source_physics(world, root)),
                    source_particles: placement
                        .0
                        .particles
                        .as_ref()
                        .map(|_| source_particles(world, root, placement.0.asset_hash)),
                }
            },
        )
        .collect()
}

fn source_physics(world: &World, root: Entity) -> ObservedSourcePhysics {
    match forge_engine::source_graph_placement::observe_manual_physics(world, root) {
        Ok(Some(observed)) => ObservedSourcePhysics {
            active_solids: observed.active_solids,
            registered_solids: observed.registered_solids,
            active_sensors: observed.active_sensors,
            registered_sensors: observed.registered_sensors,
            disabled: observed.disabled,
            disabled_synchronized: observed.disabled_synchronized,
            unassigned: observed.unassigned,
            unassigned_clear: observed.unassigned_clear,
            pending: !observed.is_ready(),
            error: None,
        },
        Ok(None) => ObservedSourcePhysics {
            pending: true,
            ..Default::default()
        },
        Err(reason) => ObservedSourcePhysics {
            error: Some(reason),
            ..Default::default()
        },
    }
}

fn source_particles(
    world: &World,
    root: Entity,
    carrier: forge_core::ContentHash,
) -> ObservedSourceParticles {
    let result = world
        .get_resource::<forge_engine::source_graph_runtime::SourceSceneResidents>()
        .ok_or_else(|| "Particle scene host absent".to_owned())
        .and_then(|residents| residents.observe_particles(world, root, carrier));
    match result {
        Ok(Some(value)) => ObservedSourceParticles {
            pending: !value.current(),
            owners: value.owners,
            initialized_owners: value.initialized_owners,
            current_owners: value.current_owners,
            producer_steps: value.producer_steps,
            projection_frames: value.projection_frames,
            primitives: value.primitives,
            error: None,
        },
        Ok(None) => ObservedSourceParticles {
            pending: true,
            ..Default::default()
        },
        Err(error) => ObservedSourceParticles {
            error: Some(error),
            ..Default::default()
        },
    }
}

fn domain_transform(transform: Transform) -> forge_core::Transform {
    forge_core::Transform {
        translation_m: forge_core::Vec3::new(
            transform.translation.x,
            transform.translation.y,
            transform.translation.z,
        ),
        rotation: forge_core::UnitQuaternion::new(
            transform.rotation.x,
            transform.rotation.y,
            transform.rotation.z,
            transform.rotation.w,
        ),
        scale: forge_core::Vec3::new(transform.scale.x, transform.scale.y, transform.scale.z),
    }
}

#[cfg(test)]
mod frame_timing_tests {
    use super::*;

    #[test]
    fn records_full_terrain_frame_without_fabricating_static_hlod() {
        let mut world = World::new();
        let mut clock = Time::<Real>::default();
        clock.advance_by(std::time::Duration::from_millis(16));
        world.insert_resource(clock);
        world.insert_resource(forge_engine::PlayerRuntimeSnapshot::default());
        let observation: ProjectionObservation = serde_json::from_value(serde_json::json!({
            "pid": 1, "frame": 3, "module_id": null, "area_id": null,
            "source_hash": null, "functional_hash": null, "gameplay_ready": true,
            "tick": 2, "simulation_error": null, "visual_error": null,
            "visual_expected": 0, "visual_spawned": 0, "visual_ready": 0,
            "manual_projection_ready": true, "visuals": [], "manual": [],
            "renderer": {"observed": false, "frame": 0, "pipelines": 0, "pending": 0, "failed": 0}
        }))
        .unwrap();
        let row = frame_timing(&world, &observation, true).unwrap();
        assert_eq!(row.frame, 3);
        assert_eq!(row.tick, 2);
        assert!((row.frame_ms - 16.0).abs() < 1.0e-9);
        assert!(row.projection_ready);
        assert_eq!(row.selected_triangles, None);
        assert_eq!(row.rendered_batches, None);
        assert_eq!(row.generation, None);
        assert_eq!(row.pending, None);
    }
}
