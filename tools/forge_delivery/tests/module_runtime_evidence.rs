#[path = "../../../apps/forge_runtime/src/module_game/evidence_contract.rs"]
mod evidence;
use evidence::*;
use forge_core::{AreaId, AssetId, EntityId, ModuleId, Transform, bytes_sha256};
use forge_project::module_gameplay_visuals::{GameplayVisualBinding, VisualAssetPin};

fn fixture() -> (ExpectedProjection, ProjectionObservation) {
    let module_id = ModuleId::deterministic("evidence/module");
    let area_id = AreaId::deterministic("evidence/area");
    let source_hash = bytes_sha256(b"canonical zone");
    let functional_hash = bytes_sha256(b"unchanged gameplay");
    let id = EntityId::deterministic("evidence/keeper");
    let asset = VisualAssetPin {
        asset_id: AssetId::deterministic("evidence/blue"),
        sha256: bytes_sha256(b"blue model"),
    };
    let manual = ExpectedManual {
        entity_id: EntityId::deterministic("evidence/cairn"),
        asset,
        transform: Transform::IDENTITY,
        non_colliding: false,
        fish: false,
        source_physics: None,
        source_particles: None,
    };
    let expected = ExpectedProjection {
        local_lights: None,
        environment: None,
        graphics: None,
        imported: None,
        species: None,
        streamed_terrain: None,
        procedural_streaming: None,
        module_id,
        area_id,
        source_hash,
        functional_hash,
        visuals: vec![GameplayVisualBinding {
            area_id,
            entity_id: id,
            asset,
            local_transform: Transform::IDENTITY,
        }],
        manual: vec![manual.clone()],
        building_count: 0,
    };
    let observed = ProjectionObservation {
        wind: None,
        local_lights: None,
        environment: None,
        graphics: None,
        imported: None,
        species: None,
        streamed_terrain: None,
        procedural_streaming: None,
        static_hlod: None,
        pid: 42,
        frame: 300,
        module_id: Some(module_id),
        area_id: Some(area_id),
        source_hash: Some(source_hash),
        functional_hash: Some(functional_hash),
        gameplay_ready: true,
        tick: 120,
        simulation_error: None,
        visual_error: None,
        visual_expected: 1,
        visual_spawned: 1,
        visual_ready: 1,
        manual_projection_ready: true,
        visuals: vec![ObservedVisual {
            entity_id: id,
            asset,
            transform: Transform::IDENTITY,
            parent_identity: Some(id),
            parent_is_gameplay_body: true,
            parent_has_collider: true,
            scene_ready: true,
            mesh_primitives: 2,
        }],
        manual: vec![ObservedManual {
            identity: manual,
            mesh_primitives: 2,
            registered_primitives: 2,
            source_physics: None,
            source_particles: None,
        }],
        renderer: RendererObservation {
            observed: true,
            frame: 299,
            pipelines: 24,
            pending: 0,
            failed: 0,
        },
        building_source_hash: None,
        building_count: 0,
        building_parts: 0,
        registered_building_parts: 0,
        building_projection_ready: false,
    };
    (expected, observed)
}

// Strict copies of the pre-procedural field names. Values are deliberately
// opaque: this fixture tests unknown-field compatibility, not domain parsing.
#[derive(serde::Deserialize)]
#[serde(deny_unknown_fields)]
#[allow(dead_code, reason = "The fixture exercises the legacy wire shape")]
struct StrictExpectedV1 {
    local_lights: Option<serde_json::Value>,
    environment: Option<serde_json::Value>,
    graphics: Option<serde_json::Value>,
    imported: Option<serde_json::Value>,
    species: Option<serde_json::Value>,
    module_id: serde_json::Value,
    area_id: serde_json::Value,
    source_hash: serde_json::Value,
    functional_hash: serde_json::Value,
    visuals: serde_json::Value,
    manual: serde_json::Value,
    building_count: usize,
    streamed_terrain: Option<serde_json::Value>,
}

#[derive(serde::Deserialize)]
#[serde(deny_unknown_fields)]
#[allow(dead_code, reason = "The fixture exercises the legacy wire shape")]
struct StrictObservedV1 {
    wind: Option<serde_json::Value>,
    local_lights: Option<serde_json::Value>,
    environment: Option<serde_json::Value>,
    graphics: Option<serde_json::Value>,
    imported: Option<serde_json::Value>,
    species: Option<serde_json::Value>,
    pid: u32,
    frame: u32,
    module_id: Option<serde_json::Value>,
    area_id: Option<serde_json::Value>,
    source_hash: Option<serde_json::Value>,
    functional_hash: Option<serde_json::Value>,
    gameplay_ready: bool,
    tick: u64,
    simulation_error: Option<String>,
    visual_error: Option<String>,
    visual_expected: usize,
    visual_spawned: usize,
    visual_ready: usize,
    manual_projection_ready: bool,
    visuals: serde_json::Value,
    manual: serde_json::Value,
    building_source_hash: Option<serde_json::Value>,
    building_count: usize,
    building_parts: usize,
    registered_building_parts: usize,
    building_projection_ready: bool,
    streamed_terrain: Option<serde_json::Value>,
    static_hlod: Option<serde_json::Value>,
    renderer: serde_json::Value,
}

#[derive(serde::Deserialize)]
#[serde(deny_unknown_fields)]
#[allow(
    dead_code,
    reason = "The fixture exercises the legacy nested placement shape"
)]
struct StrictExpectedManualV1 {
    entity_id: serde_json::Value,
    asset: serde_json::Value,
    transform: serde_json::Value,
    non_colliding: Option<bool>,
    fish: Option<bool>,
}

#[derive(serde::Deserialize)]
#[serde(deny_unknown_fields)]
#[allow(
    dead_code,
    reason = "The fixture exercises the legacy nested observation shape"
)]
struct StrictObservedManualV1 {
    identity: StrictExpectedManualV1,
    mesh_primitives: usize,
    registered_primitives: usize,
}

fn procedural_fixture() -> (ExpectedProceduralTerrain, ObservedProceduralTerrain) {
    let identity = ExpectedProceduralTerrain {
        document_hash: bytes_sha256(b"procedural source"),
        maximum_cells: 4,
        maximum_cpu_mesh_bytes: 4096,
        dimensions: [8, 8],
        maximum_staging_cpu_mesh_bytes: 8192,
    };
    let observed = ObservedProceduralTerrain {
        identity: identity.clone(),
        resident_cells: 0,
        detailed_cells: 0,
        coarse_cells: 0,
        cpu_mesh_bytes: 0,
        resident_triangles: 0,
        committed_transitions: 0,
        pending: true,
        physics_ready: false,
        error: None,
        source_synchronized: false,
        hlod: ObservedTerrainHlod {
            tiles: 0,
            aggregated_tiles: 0,
            covered_base_cells: 0,
            maximum_level: 0,
            cpu_mesh_bytes: 0,
            resident_triangles: 0,
            peak_staging_mesh_bytes: 0,
        },
    };
    (identity, observed)
}

#[test]
fn legacy_evidence_roundtrips_without_procedural_fields() {
    let (expected, observed) = fixture();
    let expected_wire = serde_json::to_value(&expected).unwrap();
    let observed_wire = serde_json::to_value(&observed).unwrap();
    assert!(expected_wire.get("procedural_streaming").is_none());
    assert!(observed_wire.get("procedural_streaming").is_none());
    assert_eq!(report_version(&expected, Some(&observed), None), 1);
    serde_json::from_value::<StrictExpectedV1>(expected_wire.clone()).unwrap();
    serde_json::from_value::<StrictObservedV1>(observed_wire.clone()).unwrap();
    serde_json::from_value::<StrictExpectedManualV1>(expected_wire["manual"][0].clone()).unwrap();
    serde_json::from_value::<StrictObservedManualV1>(observed_wire["manual"][0].clone()).unwrap();
    let reopened_expected: ExpectedProjection = serde_json::from_value(expected_wire).unwrap();
    let reopened_observed: ProjectionObservation = serde_json::from_value(observed_wire).unwrap();
    assert_eq!(reopened_expected, expected);
    assert_eq!(reopened_observed, observed);
}

#[test]
fn imported_models_use_v4_without_relabelling_source_parts_even_on_failure() {
    let (mut expected, mut observed) = fixture();
    let hash = bytes_sha256(b"imported-model-head");
    expected.imported = Some(HostIdentity {
        source_lighting: false,
        grass: None,
        projection: hash,
        head: hash,
        terrain: hash,
        instances: 12,
        colliders: 12,
    });
    expected.species = Some(SpeciesIdentity {
        population_source_hash: hash,
        catalog_sha256: hash,
    });
    assert_eq!(report_version(&expected, None, None), 4);
    assert!(verify(&expected, &observed).is_err());
    expected.imported = None;
    expected.species = None;
    let models = HostStatus {
        replacement_primitives: 100,
        ..Default::default()
    };
    let wire = serde_json::to_value(&models).unwrap();
    assert_eq!(wire["replacement_primitives"], 100);
    assert!(
        serde_json::to_value(HostStatus::default())
            .unwrap()
            .get("replacement_primitives")
            .is_none()
    );
    observed.imported = Some(models);
    assert_eq!(report_version(&expected, Some(&observed), None), 4);
    assert_eq!(report_version(&expected, None, Some(&observed)), 4);
    assert!(verify(&expected, &observed).is_err());
}

#[test]
fn procedural_extension_uses_v2_for_expected_final_or_capture_even_on_failure() {
    let (mut expected, mut observed) = fixture();
    let (identity, procedural) = procedural_fixture();
    expected.procedural_streaming = Some(identity);
    assert_eq!(report_version(&expected, None, None), 2);
    assert!(
        serde_json::from_value::<StrictExpectedV1>(serde_json::to_value(&expected).unwrap())
            .is_err()
    );
    assert!(verify(&expected, &observed).is_err());

    expected.procedural_streaming = None;
    observed.procedural_streaming = Some(procedural);
    assert_eq!(report_version(&expected, Some(&observed), None), 2);
    assert_eq!(report_version(&expected, None, Some(&observed)), 2);
    assert!(
        serde_json::from_value::<StrictObservedV1>(serde_json::to_value(&observed).unwrap())
            .is_err()
    );
    assert!(verify(&expected, &observed).is_err());
}

#[test]
fn legacy_strict_shapes_refuse_even_a_null_procedural_extension() {
    let (expected, observed) = fixture();
    let mut expected_wire = serde_json::to_value(expected).unwrap();
    let mut observed_wire = serde_json::to_value(observed).unwrap();
    expected_wire["procedural_streaming"] = serde_json::Value::Null;
    observed_wire["procedural_streaming"] = serde_json::Value::Null;
    assert!(serde_json::from_value::<StrictExpectedV1>(expected_wire).is_err());
    assert!(serde_json::from_value::<StrictObservedV1>(observed_wire).is_err());
}

fn source_physics_fixture() -> (ExpectedProjection, ProjectionObservation) {
    use forge_project::zone_authoring::placements::PhysicsPackageReference;

    let (mut expected, mut observed) = fixture();
    expected.manual[0].source_physics = Some(Box::new(PhysicsPackageReference {
        schema_version: 1,
        carrier_sha256: expected.manual[0].asset.sha256,
        manifest_sha256: bytes_sha256(b"physics manifest"),
        package_sha256: bytes_sha256(b"physics package"),
    }));
    observed.manual[0].identity = expected.manual[0].clone();
    // Four rendered meshes do not imply four colliders: the source supplies
    // two solids and one sensor independently from the visual geometry.
    observed.manual[0].mesh_primitives = 4;
    observed.manual[0].registered_primitives = 0;
    observed.manual[0].source_physics = Some(ObservedSourcePhysics {
        active_solids: 2,
        registered_solids: 2,
        active_sensors: 1,
        registered_sensors: 1,
        disabled: 1,
        disabled_synchronized: 1,
        unassigned: 2,
        unassigned_clear: 2,
        pending: false,
        error: None,
    });
    (expected, observed)
}

#[test]
fn source_physics_uses_v2_and_independent_graphical_and_backend_counts() {
    let (expected, observed) = source_physics_fixture();
    assert_eq!(report_version(&expected, Some(&observed), None), 2);
    assert_eq!(report_version(&expected, None, None), 2);
    assert_eq!(report_version(&fixture().0, Some(&observed), None), 2);
    assert_eq!(report_version(&fixture().0, None, Some(&observed)), 2);
    verify(&expected, &observed).unwrap();

    let expected_wire = serde_json::to_value(&expected).unwrap();
    let observed_wire = serde_json::to_value(&observed).unwrap();
    assert!(
        serde_json::from_value::<StrictExpectedManualV1>(expected_wire["manual"][0].clone())
            .is_err()
    );
    assert!(
        serde_json::from_value::<StrictObservedManualV1>(observed_wire["manual"][0].clone())
            .is_err()
    );
    assert_eq!(
        serde_json::from_value::<ExpectedProjection>(expected_wire).unwrap(),
        expected
    );
    assert_eq!(
        serde_json::from_value::<ProjectionObservation>(observed_wire).unwrap(),
        observed
    );
}

#[test]
fn source_physics_rejects_pending_refused_or_incomplete_backend_states() {
    let (expected, observed) = source_physics_fixture();
    for fault in 0..9 {
        let mut bad = observed.clone();
        let physics = bad.manual[0].source_physics.as_mut().unwrap();
        match fault {
            0 => physics.pending = true,
            1 => physics.error = Some("source collider refused".into()),
            2 => physics.registered_solids -= 1,
            3 => physics.registered_sensors -= 1,
            4 => physics.disabled_synchronized -= 1,
            5 => physics.unassigned_clear -= 1,
            6 => {
                physics.active_solids = 0;
                physics.registered_solids = 0;
                physics.active_sensors = 0;
                physics.registered_sensors = 0;
            }
            7 => bad.manual[0].registered_primitives = 1,
            _ => bad.manual[0].mesh_primitives = 0,
        }
        assert!(verify(&expected, &bad).is_err(), "fault {fault}");
    }
    let mut missing = observed.clone();
    missing.manual[0].source_physics = None;
    assert!(verify(&expected, &missing).is_err());
    let mut unexpected = fixture().1;
    unexpected.manual[0].source_physics = observed.manual[0].source_physics.clone();
    assert!(verify(&fixture().0, &unexpected).is_err());
}

#[test]
fn source_physics_requires_the_saved_companion_and_carrier_pin() {
    let (expected, observed) = source_physics_fixture();
    let mut bad = observed.clone();
    bad.manual[0]
        .identity
        .source_physics
        .as_mut()
        .unwrap()
        .package_sha256 = bytes_sha256(b"other package");
    assert!(verify(&expected, &bad).is_err());
    let mut invalid_expected = expected.clone();
    invalid_expected.manual[0]
        .source_physics
        .as_mut()
        .unwrap()
        .carrier_sha256 = bytes_sha256(b"other carrier");
    bad.manual[0].identity = invalid_expected.manual[0].clone();
    assert!(verify(&invalid_expected, &bad).is_err());
    invalid_expected = expected.clone();
    invalid_expected.manual[0]
        .source_physics
        .as_mut()
        .unwrap()
        .schema_version = 2;
    bad.manual[0].identity = invalid_expected.manual[0].clone();
    assert!(verify(&invalid_expected, &bad).is_err());
}

#[test]
fn legacy_manual_readers_refuse_a_null_physics_extension() {
    let (expected, observed) = fixture();
    let mut expected_wire = serde_json::to_value(&expected.manual[0]).unwrap();
    let mut observed_wire = serde_json::to_value(&observed.manual[0]).unwrap();
    expected_wire["source_physics"] = serde_json::Value::Null;
    observed_wire["source_physics"] = serde_json::Value::Null;
    assert!(serde_json::from_value::<StrictExpectedManualV1>(expected_wire).is_err());
    assert!(serde_json::from_value::<StrictObservedManualV1>(observed_wire).is_err());
}

fn admit_fish_graphics(expected: &mut ExpectedProjection, observed: &mut ProjectionObservation) {
    use evidence::graphics::{GraphicsObservation, GraphicsScope};
    use forge_project::VisualQualityTier;
    let identity = GraphicsIdentity {
        head: expected.source_hash,
        policy: bytes_sha256(b"graphics policy"),
        water: false,
    };
    let tier = VisualQualityTier::High;
    let token =
        forge_core::canonical_json_sha256(&(identity.head, identity.policy, tier, 1_u64)).unwrap();
    let scope = |name: &str| GraphicsScope {
        name: name.into(),
        owner: Some(EntityId::deterministic(format!("module-graphics/{name}"))),
        identity: Some(token),
        generation: Some(1),
        epoch: 1,
        observed: 299,
        completed: Some(299),
        required_frame: None,
        publication: None,
        token_current: true,
        meshes: [2, 2],
        materials: [1, 1],
        images: [1, 1],
        buffers: [1, 1],
        pipelines: [1, 0, 0],
        effect_groups: [1, 1],
        effects_ready: true,
        camera_ready: true,
    };
    observed.graphics = Some(GraphicsObservation {
        identity: identity.clone(),
        requested: tier,
        applied: Some(tier),
        persisted: Some(tier),
        request_sequence: 1,
        generation: 1,
        application_frame: 296,
        cpu_ready: true,
        ready: true,
        error: None,
        preference_error: None,
        scopes: vec![scope("terrain"), scope("fish")],
        plant_completed_frame: None,
        source_surfaces: None,
        source_terrain: None,
    });
    expected.graphics = Some(identity);
}

#[test]
fn animated_non_colliding_placements_require_their_verified_marker_and_meshes() {
    let (mut expected, mut observed) = fixture();
    expected.manual[0].non_colliding = true;
    observed.manual[0].identity.non_colliding = true;
    observed.manual[0].registered_primitives = 0;
    verify(&expected, &observed).unwrap();

    let mut bad = observed.clone();
    bad.manual[0].identity.non_colliding = false;
    assert!(verify(&expected, &bad).is_err());
    let mut bad = observed.clone();
    bad.manual[0].mesh_primitives = 0;
    assert!(verify(&expected, &bad).is_err());
    let mut bad = observed.clone();
    bad.manual[0].registered_primitives = 1;
    assert!(verify(&expected, &bad).is_err());
    expected.manual[0].non_colliding = false;
    assert!(verify(&expected, &observed).is_err());
}

#[test]
fn admitted_fish_require_their_own_complete_gpu_scope() {
    let (mut expected, mut observed) = fixture();
    expected.manual[0].non_colliding = true;
    expected.manual[0].fish = true;
    observed.manual[0].identity = expected.manual[0].clone();
    observed.manual[0].registered_primitives = 0;
    assert!(verify(&expected, &observed).is_err());
    admit_fish_graphics(&mut expected, &mut observed);
    verify(&expected, &observed).unwrap();
    let mut missing = observed.clone();
    missing.graphics.as_mut().unwrap().scopes.pop();
    assert!(verify(&expected, &missing).is_err());
    let mut stale = observed.clone();
    stale.graphics.as_mut().unwrap().scopes[1].completed = None;
    assert!(verify(&expected, &stale).is_err());
    let mut foreign = observed.clone();
    foreign.graphics.as_mut().unwrap().scopes[1].owner = Some(EntityId::deterministic("foreign"));
    assert!(verify(&expected, &foreign).is_err());
    let mut forged = observed;
    forged.manual[0].identity.fish = false;
    assert!(verify(&expected, &forged).is_err());
}

#[test]
fn installed_species_require_exact_population_catalog_gpu_and_collisions() {
    let (mut expected, mut observed) = fixture();
    let identity = SpeciesIdentity {
        population_source_hash: bytes_sha256(b"authored vegetation with clearances"),
        catalog_sha256: bytes_sha256(b"resolved installed catalog"),
    };
    expected.species = Some(identity.clone());
    assert!(verify(&expected, &observed).is_err());
    let ready = ObservedSpecies {
        empty: false,
        identity: identity.clone(),
        projected_population_hash: Some(identity.population_source_hash.to_string()),
        projected_catalog_sha256: Some(identity.catalog_sha256),
        status: ObservedSpeciesStatus {
            ready: true,
            cpu_ready: true,
            collisions_ready: true,
            error: None,
        },
        gpu_generation: Some(1),
        meshes_expected: 6,
        meshes_prepared: 6,
        materials_expected: 4,
        materials_bound: 4,
        images_expected: 2,
        images_prepared: 2,
    };
    observed.species = Some(ready.clone());
    verify(&expected, &observed).unwrap();
    let encoded = serde_json::to_vec(&observed).unwrap();
    let decoded = serde_json::from_slice(&encoded).unwrap();
    verify(&expected, &decoded).unwrap();
    for field in 0..5 {
        let mut changed = ready.clone();
        match field {
            0 => {
                changed.projected_population_hash =
                    Some(bytes_sha256(b"other clearances").to_string())
            }
            1 => changed.projected_catalog_sha256 = Some(bytes_sha256(b"other species")),
            2 => changed.status.collisions_ready = false,
            3 => changed.images_prepared = 1,
            _ => changed.gpu_generation = None,
        }
        observed.species = Some(changed);
        assert!(verify(&expected, &observed).is_err());
    }
    let mut empty = ready.clone();
    empty.empty = true;
    empty.gpu_generation = None;
    empty.meshes_expected = 0;
    empty.meshes_prepared = 0;
    empty.materials_expected = 0;
    empty.materials_bound = 0;
    empty.images_expected = 0;
    empty.images_prepared = 0;
    assert!(empty.ready());
    observed.species = Some(empty.clone());
    // A procedural forest cannot use the imported-empty exception.
    assert!(verify(&expected, &observed).is_err());
    empty.gpu_generation = Some(1);
    assert!(!empty.ready());
    empty.gpu_generation = None;
    empty.meshes_prepared = 1;
    assert!(!empty.ready());
    observed.species = Some(ready);
    expected.species = None;
    assert!(verify(&expected, &observed).is_err());
}

#[test]
fn canonical_rooms_require_their_source_every_part_and_static_registration() {
    let (mut expected, mut observed) = fixture();
    expected.building_count = 1;
    assert!(verify(&expected, &observed).is_err());
    observed.building_source_hash = Some(expected.source_hash);
    observed.building_count = 1;
    observed.building_parts = 8;
    observed.registered_building_parts = 8;
    observed.building_projection_ready = true;
    verify(&expected, &observed).unwrap();
    for fault in 0..5 {
        let mut bad = observed.clone();
        match fault {
            0 => bad.building_source_hash = Some(bytes_sha256(b"other room pose")),
            1 => bad.building_count = 2,
            2 => bad.building_parts = 7,
            3 => bad.registered_building_parts = 7,
            _ => bad.building_projection_ready = false,
        }
        assert!(verify(&expected, &bad).is_err(), "fault {fault}");
    }
}

#[test]
fn exact_live_projection_includes_identity_pins_physics_and_ready_renderer() {
    let (expected, observed) = fixture();
    verify(&expected, &observed).unwrap();
    let wire = serde_json::to_vec(&observed).unwrap();
    let reopened = serde_json::from_slice(&wire).unwrap();
    verify(&expected, &reopened).unwrap();
}

#[test]
fn process_success_or_counters_without_actual_instances_never_prove_readiness() {
    let (expected, mut observed) = fixture();
    observed.visuals.clear();
    observed.manual.clear();
    assert!(verify(&expected, &observed).is_err());
    observed.gameplay_ready = false;
    observed.tick = 0;
    assert!(verify(&expected, &observed).is_err());
}

#[test]
fn wrong_source_variant_transform_parent_collider_or_renderer_is_refused() {
    let (expected, observed) = fixture();
    for fault in 0..15 {
        let mut bad = observed.clone();
        match fault {
            0 => bad.source_hash = Some(bytes_sha256(b"wrong source")),
            1 => bad.functional_hash = Some(bytes_sha256(b"changed gameplay")),
            2 => bad.visuals[0].asset.sha256 = bytes_sha256(b"other variant"),
            3 => bad.visuals[0].transform.translation_m.y = 1.0,
            4 => bad.visuals[0].parent_identity = Some(EntityId::deterministic("wrong parent")),
            5 => bad.visuals[0].parent_has_collider = false,
            6 => bad.visuals[0].scene_ready = false,
            7 => bad.visuals[0].mesh_primitives = 0,
            8 => bad.manual[0].registered_primitives = 1,
            9 => bad.renderer.observed = false,
            10 => bad.renderer.pending = 1,
            11 => bad.renderer.failed = 1,
            12 => bad.renderer.frame = 200,
            13 => bad.manual_projection_ready = false,
            _ => bad.simulation_error = Some("physical projection failed".into()),
        }
        assert!(verify(&expected, &bad).is_err(), "fault {fault}");
    }
}

#[test]
fn duplicate_or_extra_visual_and_manual_identities_do_not_satisfy_counts() {
    let (expected, observed) = fixture();
    let mut bad = observed.clone();
    bad.visuals.push(bad.visuals[0].clone());
    assert!(verify(&expected, &bad).is_err());
    let mut bad = observed.clone();
    bad.manual.push(bad.manual[0].clone());
    assert!(verify(&expected, &bad).is_err());
    let mut bad = observed;
    bad.visual_ready = 2;
    assert!(verify(&expected, &bad).is_err());
}

#[test]
fn a_streamed_module_needs_its_exact_raster_residency_and_physics_observations() {
    let (mut expected, mut observed) = fixture();
    let identity = ExpectedStreamedTerrain {
        document_hash: bytes_sha256(b"terrain document"),
        raster_hash: bytes_sha256(b"immutable raster"),
        raster_bytes: 4_194_304,
        maximum_cells: 121,
        maximum_cpu_mesh_bytes: 12_100_000,
        dimensions: [32; 2],
        maximum_staging_cpu_mesh_bytes: 24_200_000,
    };
    expected.streamed_terrain = Some(identity.clone());
    assert!(verify(&expected, &observed).is_err());
    observed.streamed_terrain = Some(ObservedStreamedTerrain {
        identity,
        resident_cells: 81,
        detailed_cells: 9,
        coarse_cells: 72,
        cpu_mesh_bytes: 859_032,
        resident_triangles: 20_000,
        committed_transitions: 1,
        maximum_cells: 121,
        maximum_cpu_mesh_bytes: 12_100_000,
        pending: false,
        physics_ready: true,
        error: None,
        source_synchronized: true,
        hlod: ObservedTerrainHlod {
            tiles: 89,
            aggregated_tiles: 68,
            covered_base_cells: 943,
            maximum_level: 3,
            cpu_mesh_bytes: 783_080,
            resident_triangles: 10_000,
            peak_staging_mesh_bytes: 4_153_232,
        },
    });
    observed.static_hlod = Some(ObservedStaticHlod {
        enabled: true,
        generation: 1,
        source_objects: 1,
        source_primitives: 2,
        covered_primitives: 2,
        source_triangles: 2048,
        selected_triangles: 392,
        desired_batches: 1,
        rendered_batches: 1,
        replaced_primitives: 2,
        retained_source_mesh_bytes: 50_000,
        source_snapshot_bytes: 50_000,
        derived_mesh_bytes: 10_000,
        peak_tracked_buffer_bound_bytes: 200_000,
        maximum_position_error_m: 0.05,
        maximum_attribute_deltas: Default::default(),
        minimum_face_alignment: Some(1.0),
        committed_transitions: 1,
        discarded_jobs: 0,
        pending: false,
        error: None,
        gpu_upload_ready: true,
    });
    verify(&expected, &observed).unwrap();
    let mut animated = expected.manual[0].clone();
    animated.entity_id = EntityId::deterministic("evidence/animated-fish");
    animated.non_colliding = true;
    expected.manual.push(animated.clone());
    observed.manual.push(ObservedManual {
        identity: animated,
        mesh_primitives: 2,
        registered_primitives: 0,
        source_physics: None,
        source_particles: None,
    });
    // Animated geometry is observed, but is not counted as static HLOD input.
    verify(&expected, &observed).unwrap();
    expected.manual.last_mut().unwrap().fish = true;
    observed.manual.last_mut().unwrap().identity.fish = true;
    assert!(verify(&expected, &observed).is_err());
    admit_fish_graphics(&mut expected, &mut observed);
    verify(&expected, &observed).unwrap();
    expected.manual.pop();
    observed.manual.pop();
    expected.graphics = None;
    observed.graphics = None;
    for fault in 0..10 {
        let mut bad = observed.clone();
        let row = bad.streamed_terrain.as_mut().unwrap();
        match fault {
            0 => row.identity.raster_hash = bytes_sha256(b"wrong raster"),
            1 => row.identity.document_hash = bytes_sha256(b"wrong paint"),
            2 => {
                row.resident_cells = 122;
                row.coarse_cells = 113;
            }
            3 => row.cpu_mesh_bytes = 12_100_001,
            4 => row.pending = true,
            5 => row.physics_ready = false,
            6 => row.detailed_cells = 0,
            7 => row.error = Some("missing terrain collider".into()),
            8 => row.maximum_cpu_mesh_bytes = u64::MAX,
            _ => row.coarse_cells = usize::MAX,
        }
        assert!(verify(&expected, &bad).is_err(), "fault {fault}");
    }
    for fault in 0..18 {
        let mut bad = observed.clone();
        if fault == 0 {
            bad.static_hlod = None;
        } else {
            let row = bad.static_hlod.as_mut().unwrap();
            match fault {
                1 => row.gpu_upload_ready = false,
                2 => row.covered_primitives = 0,
                3 => row.rendered_batches = 0,
                4 => row.pending = true,
                5 => row.error = Some("upload missing".into()),
                6 => row.source_objects += 1,
                7 => row.replaced_primitives = 0,
                8 => row.selected_triangles = row.source_triangles + 1,
                9 => row.source_snapshot_bytes = 64 * 1024 * 1024 + 1,
                10 => row.derived_mesh_bytes = 32 * 1024 * 1024 + 1,
                11 => row.maximum_position_error_m = 1.01,
                12 => row.minimum_face_alignment = Some(0.49),
                13 => row.minimum_face_alignment = None,
                14 => {
                    row.maximum_attribute_deltas
                        .insert("Vertex_Uv".into(), 0.02);
                }
                15 => row.maximum_position_error_m = -0.01,
                16 => row.source_primitives += 1,
                _ => row.enabled = false,
            }
        }
        assert!(verify(&expected, &bad).is_err(), "static fault {fault}");
    }
    let row = observed.static_hlod.as_mut().unwrap();
    row.enabled = false;
    row.selected_triangles = row.source_triangles;
    row.desired_batches = 0;
    row.rendered_batches = 0;
    row.replaced_primitives = 0;
    row.derived_mesh_bytes = 0;
    row.minimum_face_alignment = None;
    row.gpu_upload_ready = false;
    verify(&expected, &observed).unwrap();
    observed.static_hlod.as_mut().unwrap().selected_triangles -= 1;
    assert!(verify(&expected, &observed).is_err());
}
