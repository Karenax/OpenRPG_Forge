use super::*;
use forge_world::TerrainGenerationPlan;

#[test]
fn worker_preparation_matches_serial_payloads_for_every_tier() {
    let compiled = forge_world::compile_world_sketch(
        &forge_world::embedded_visual_valley_world_sketch().unwrap(),
    )
    .unwrap();
    let generated = TerrainGenerationPlan::from_document(&compiled.carved_terrain)
        .unwrap()
        .generate()
        .unwrap();
    let chunks = [0, 7, 24, 37].map(|index| generated.chunks[index].clone());
    let fingerprints = chunks
        .iter()
        .map(|chunk| (canonical_json_sha256(chunk).unwrap(), true))
        .collect::<Vec<_>>();
    let protected = BTreeSet::from([chunks[1].id]);
    let policy = VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap();
    for tier in [
        VisualQualityTier::Low,
        VisualQualityTier::Medium,
        VisualQualityTier::High,
        VisualQualityTier::Ultra,
    ] {
        let settings = policy.preset(tier).terrain;
        let topology = [1, settings.lod1_sample_stride, settings.lod2_sample_stride]
            .map(|stride| TerrainLodTopology::new(stride).unwrap());
        let serial = chunks
            .iter()
            .zip(&fingerprints)
            .map(|(chunk, (hash, _))| {
                (
                    *hash,
                    Some(
                        prepare_lod_payloads(chunk, &topology, protected.contains(&chunk.id))
                            .unwrap(),
                    ),
                )
            })
            .collect::<Vec<_>>();
        for _ in 0..2 {
            let parallel =
                prepare_changed_lods(&chunks, &fingerprints, &topology, &protected).unwrap();
            assert_eq!(parallel, serial);
            // Include signed-zero bit identity of geometry attributes, not approximate floats.
            for ((_, actual), (_, expected)) in parallel.iter().zip(&serial) {
                for ((mesh, morph), (reference, reference_morph)) in actual
                    .as_ref()
                    .unwrap()
                    .iter()
                    .zip(expected.as_ref().unwrap())
                {
                    for (value, baseline) in std::iter::once((mesh, reference))
                        .chain(morph.iter().zip(reference_morph.iter()))
                    {
                        let bits = |mesh: &TerrainLodMesh| {
                            mesh.positions_m
                                .iter()
                                .chain(&mesh.normals)
                                .flatten()
                                .chain(mesh.uvs.iter().flatten())
                                .map(|value| value.to_bits())
                                .collect::<Vec<_>>()
                        };
                        assert_eq!(bits(value), bits(baseline));
                    }
                }
            }
        }
    }
}

#[test]
fn worker_preparation_skips_unchanged_chunks_and_rejects_invalid_batches() {
    let document = forge_world::embedded_visual_valley_terrain_document().unwrap();
    let generated = TerrainGenerationPlan::from_document(&document)
        .unwrap()
        .generate()
        .unwrap();
    let mut chunks = vec![generated.chunks[0].clone(), generated.chunks[1].clone()];
    let topology = [1, 2, 4].map(|stride| TerrainLodTopology::new(stride).unwrap());
    let fingerprints = chunks
        .iter()
        .map(|chunk| (canonical_json_sha256(chunk).unwrap(), false))
        .collect::<Vec<_>>();
    assert!(
        prepare_changed_lods(&chunks, &fingerprints, &topology, &BTreeSet::new())
            .unwrap()
            .iter()
            .all(|(_, value)| value.is_none())
    );
    assert!(
        prepare_changed_lods(&chunks, &fingerprints[..1], &topology, &BTreeSet::new()).is_err()
    );
    chunks[1].positions_m.pop();
    let changed = fingerprints
        .into_iter()
        .map(|(hash, _)| (hash, true))
        .collect::<Vec<_>>();
    assert!(prepare_changed_lods(&chunks, &changed, &topology, &BTreeSet::new()).is_err());
}

#[test]
fn protected_payload_reuse_invalidates_on_source_or_protection_change() {
    let document = forge_world::embedded_visual_valley_terrain_document().unwrap();
    let generated = TerrainGenerationPlan::from_document(&document)
        .unwrap()
        .generate()
        .unwrap();
    let mut chunks = vec![generated.chunks[0].clone()];
    let id = chunks[0].id;
    let previous = BTreeMap::from([(id, canonical_json_sha256(&chunks[0]).unwrap())]);
    let protected = BTreeSet::from([id]);
    let topology = [1, 4, 8].map(|stride| TerrainLodTopology::new(stride).unwrap());
    let retained =
        prepare_from_snapshot(&chunks, &topology, &protected, &protected, true, &previous).unwrap();
    assert!(retained[0].1.is_none());
    let removed = prepare_from_snapshot(
        &chunks,
        &topology,
        &BTreeSet::new(),
        &protected,
        false,
        &previous,
    )
    .unwrap();
    assert!(removed[0].1.as_ref().unwrap()[0].1.is_some());
    let arrived = prepare_from_snapshot(
        &chunks,
        &topology,
        &protected,
        &BTreeSet::new(),
        false,
        &previous,
    )
    .unwrap();
    assert!(
        arrived[0]
            .1
            .as_ref()
            .unwrap()
            .iter()
            .all(|payload| payload.1.is_none())
    );
    chunks[0].positions_m[0][1] += 0.25;
    let edited =
        prepare_from_snapshot(&chunks, &topology, &protected, &protected, false, &previous)
            .unwrap();
    assert_ne!(edited[0].0, previous[&id]);
    assert!(edited[0].1.is_some());
    let first = prepare_from_snapshot(
        &chunks,
        &topology,
        &protected,
        &protected,
        true,
        &BTreeMap::new(),
    )
    .unwrap();
    assert!(first[0].1.is_some());
}

fn bank_triangle(points: Vec<[f32; 3]>) -> Mesh {
    Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::MAIN_WORLD,
    )
    .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, points)
    .with_inserted_indices(Indices::U32(vec![0, 1, 2]))
}

#[test]
fn bank_mesh_edits_cross_boundaries_and_recover_after_invalid_geometry() {
    let mut app = app();
    app.update();
    let original = derived_bindings(&app);
    let (x, z, edge, source_hash) = {
        let source = app.world().resource::<TerrainRuntimeState>();
        let chunk = &source.cache().chunks[0];
        (
            chunk.minimum_xz_m[0],
            chunk.minimum_xz_m[1],
            chunk.maximum_xz_m[0],
            source.cache().source_document_hash,
        )
    };
    let handle = app
        .world_mut()
        .resource_mut::<Assets<Mesh>>()
        .add(bank_triangle(vec![
            [x + 1.0, 0.0, z + 1.0],
            [x + 2.0, 0.0, z + 1.0],
            [x + 1.0, 0.0, z + 2.0],
        ]));
    app.world_mut().spawn((
        crate::water::VisualValleyRiverBankSurface,
        Mesh3d(handle.clone()),
        Transform::IDENTITY,
    ));
    app.update();
    assert_eq!(
        app.world()
            .resource::<TerrainLodProjectionStats>()
            .bank_constrained_chunks,
        1
    );
    app.world_mut()
        .resource_mut::<Assets<Mesh>>()
        .get_mut(&handle)
        .unwrap()
        .insert_attribute(
            Mesh::ATTRIBUTE_POSITION,
            vec![
                [edge - 1.0, 0.0, z + 1.0],
                [edge + 1.0, 0.0, z + 1.0],
                [edge, 0.0, z + 2.0],
            ],
        );
    app.update();
    assert_eq!(
        app.world()
            .resource::<TerrainLodProjectionStats>()
            .bank_constrained_chunks,
        2
    );
    app.world_mut()
        .resource_mut::<Assets<Mesh>>()
        .get_mut(&handle)
        .unwrap()
        .insert_indices(Indices::U32(vec![0, 1, 99]));
    app.update();
    assert_eq!(
        app.world().resource::<TerrainLodProjectionStats>().status,
        "bank_constraints_failed"
    );
    assert_eq!(derived_bindings(&app), original);
    app.world_mut()
        .resource_mut::<Assets<Mesh>>()
        .get_mut(&handle)
        .unwrap()
        .insert_indices(Indices::U32(vec![0, 1, 2]));
    app.update();
    let stats = app.world().resource::<TerrainLodProjectionStats>();
    assert_eq!(stats.projected_tier, Some(VisualQualityTier::High));
    assert_eq!(stats.bank_constrained_chunks, 2);
    let constraints = stats.bank_constraints.as_ref().unwrap();
    assert_eq!(constraints.terrain_hash, source_hash);
    assert_eq!(constraints.full_resolution_chunks.len(), 2);
    assert!(
        constraints
            .full_resolution_chunks
            .windows(2)
            .all(|pair| pair[0] < pair[1])
    );
    assert_eq!(stats.last_rebuilt_chunks, 0);
    assert_eq!(derived_bindings(&app), original);
    assert_eq!(
        app.world()
            .resource::<TerrainRuntimeState>()
            .cache()
            .source_document_hash,
        source_hash
    );
}

#[test]
fn bank_constraints_follow_geometry_not_valley_coordinates() {
    let app = app();
    let terrain = app.world().resource::<TerrainRuntimeState>();
    let chunks = &terrain.cache().chunks;
    let first = &chunks[0];
    let [x, z] = first.minimum_xz_m;
    let mut meshes = Assets::<Mesh>::default();
    let handle = meshes.add(bank_triangle(vec![
        [1.0, 0.0, 1.0],
        [2.0, 0.0, 1.0],
        [1.0, 0.0, 2.0],
    ]));
    let bindings = [(handle.id(), Transform::from_xyz(x, 0.0, z))];
    assert_eq!(
        bank_constraints::affected_chunks(chunks, &bindings, &meshes).unwrap(),
        BTreeSet::from([first.id])
    );
    assert!(
        bank_constraints::affected_chunks(chunks, &[], &meshes)
            .unwrap()
            .is_empty()
    );
    meshes
        .get_mut(&handle)
        .unwrap()
        .insert_indices(Indices::U32(vec![0, 1, 99]));
    assert_eq!(
        bank_constraints::affected_chunks(chunks, &bindings, &meshes),
        Err("bank index out of bounds")
    );
    meshes
        .get_mut(&handle)
        .unwrap()
        .insert_indices(Indices::U32(vec![0, 1, 2]));
    let invalid = [(handle.id(), Transform::from_xyz(f32::NAN, 0.0, 0.0))];
    assert_eq!(
        bank_constraints::affected_chunks(chunks, &invalid, &meshes),
        Err("non-finite bank position")
    );
}

#[test]
fn bank_arrival_and_removal_restore_lods_without_changing_handles() {
    let mut app = app();
    app.update();
    let original = derived_bindings(&app);
    let (id, x, z) = {
        let terrain = app.world().resource::<TerrainRuntimeState>();
        let chunk = &terrain.cache().chunks[0];
        (chunk.id, chunk.minimum_xz_m[0], chunk.minimum_xz_m[1])
    };
    let handle = app
        .world_mut()
        .resource_mut::<Assets<Mesh>>()
        .add(bank_triangle(vec![
            [x + 1.0, 0.0, z + 1.0],
            [x + 2.0, 0.0, z + 1.0],
            [x + 1.0, 0.0, z + 2.0],
        ]));
    let bank = app
        .world_mut()
        .spawn((
            crate::water::VisualValleyRiverBankSurface,
            Mesh3d(handle),
            Transform::IDENTITY,
        ))
        .id();
    app.update();
    let stats = app.world().resource::<TerrainLodProjectionStats>();
    assert_eq!(stats.bank_constrained_chunks, 1);
    assert_eq!(stats.last_rebuilt_chunks, 1);
    assert_eq!(derived_bindings(&app), original);
    for variant in &app.world().resource::<TerrainLodCache>().chunks[&id].variants {
        let mesh = app
            .world()
            .resource::<Assets<Mesh>>()
            .get(&variant.mesh)
            .unwrap();
        assert_eq!(mesh.count_vertices(), 33 * 33);
        assert!(mesh.morph_targets().is_none());
    }
    for tier in [
        VisualQualityTier::Low,
        VisualQualityTier::Ultra,
        VisualQualityTier::Medium,
    ] {
        app.world_mut()
            .resource_mut::<TerrainQualityRequest>()
            .request(tier);
        app.update();
        let world = app.world();
        let stats = world.resource::<TerrainLodProjectionStats>();
        assert_eq!(stats.projected_tier, Some(tier));
        assert_eq!(stats.last_rebuilt_chunks, stats.canonical_chunks - 1);
        let settings = world
            .resource::<TerrainQualityRequest>()
            .document
            .preset(tier)
            .terrain;
        let ranges = ranges(settings);
        for (level, variant) in world.resource::<TerrainLodCache>().chunks[&id]
            .variants
            .iter()
            .enumerate()
        {
            let mesh = world.resource::<Assets<Mesh>>().get(&variant.mesh).unwrap();
            assert_eq!(mesh.count_vertices(), 33 * 33);
            assert!(mesh.morph_targets().is_none());
            assert!(world.get::<VisibilityRange>(variant.entity).unwrap() == &ranges[level]);
        }
        assert_eq!(derived_bindings(&app), original);
    }
    app.world_mut().despawn(bank);
    app.update();
    assert_eq!(
        app.world()
            .resource::<TerrainLodProjectionStats>()
            .bank_constrained_chunks,
        0
    );
    assert_eq!(
        app.world()
            .resource::<TerrainLodProjectionStats>()
            .last_rebuilt_chunks,
        1
    );
    assert_eq!(derived_bindings(&app), original);
}

#[test]
fn morph_weight_follows_camera_and_restores_without_rebuilding_meshes() {
    let mut app = App::new();
    app.add_plugins(MinimalPlugins)
        .add_systems(Update, update_terrain_morphs);
    let camera = app
        .world_mut()
        .spawn((
            Camera3d::default(),
            Camera::default(),
            GlobalTransform::IDENTITY,
        ))
        .id();
    let mesh = app
        .world_mut()
        .spawn((
            Transform::IDENTITY,
            TerrainMorphBand {
                start: 10.0,
                end: 20.0,
            },
            MeshMorphWeights::Value { weights: vec![0.0] },
        ))
        .id();
    for (distance, expected) in [
        (0.0, 0.0_f32),
        (15.0, 0.5),
        (25.0, 1.0),
        (15.0, 0.5),
        (0.0, 0.0),
    ] {
        app.world_mut()
            .entity_mut(camera)
            .insert(GlobalTransform::from_translation(Vec3::new(
                distance, 0.0, 0.0,
            )));
        app.update();
        let MeshMorphWeights::Value { weights } =
            app.world().get::<MeshMorphWeights>(mesh).unwrap()
        else {
            panic!("owned weights");
        };
        assert_eq!(weights[0].to_bits(), expected.to_bits());
    }
}

#[cfg(not(target_arch = "wasm32"))]
#[test]
fn background_bank_change_keeps_geometry_without_qualifying_stale_projection() {
    let mut app = app();
    app.update();
    let original = derived_bindings(&app);
    let tier = app
        .world()
        .resource::<TerrainLodProjectionStats>()
        .projected_tier;
    assert!(tier.is_some());
    let (source_hash, x, z) = {
        let terrain = app.world().resource::<TerrainRuntimeState>();
        let chunk = &terrain.cache().chunks[0];
        (
            terrain.cache().source_document_hash,
            chunk.minimum_xz_m[0],
            chunk.minimum_xz_m[1],
        )
    };
    app.world_mut().resource_mut::<TerrainLodCache>().background = true;
    let handle = app
        .world_mut()
        .resource_mut::<Assets<Mesh>>()
        .add(bank_triangle(vec![
            [x + 1.0, 0.0, z + 1.0],
            [x + 2.0, 0.0, z + 1.0],
            [x + 1.0, 0.0, z + 2.0],
        ]));
    app.world_mut().spawn((
        crate::water::VisualValleyRiverBankSurface,
        Mesh3d(handle),
        Transform::IDENTITY,
    ));
    app.update();
    let stats = app.world().resource::<TerrainLodProjectionStats>();
    assert_eq!(stats.projected_tier, None);
    assert_eq!(
        stats.status,
        "preparing_background_previous_projection_retained"
    );
    assert_eq!(derived_bindings(&app), original);
    let deadline = std::time::Instant::now() + std::time::Duration::from_secs(30);
    // Synchronize on the real worker before publishing its result. Repeated full
    // app updates would recalculate the same bank constraints and fingerprints
    // while competing with other terrain tests for the global compute pools.
    while !app
        .world()
        .resource::<TerrainLodCache>()
        .pending
        .as_ref()
        .expect("background preparation started")
        .is_finished()
    {
        assert!(
            std::time::Instant::now() < deadline,
            "terrain background deadline"
        );
        std::thread::yield_now();
    }
    app.update();
    let stats = app.world().resource::<TerrainLodProjectionStats>();
    assert_eq!(stats.projected_tier, tier);
    assert_eq!(stats.bank_constrained_chunks, 1);
    assert_eq!(derived_bindings(&app), original);
    assert_eq!(
        app.world()
            .resource::<TerrainRuntimeState>()
            .cache()
            .source_document_hash,
        source_hash
    );
}

fn app() -> App {
    let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    let generated = TerrainGenerationPlan::from_document(&terrain)
        .unwrap()
        .generate()
        .unwrap();
    let policy = VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap();
    let mut app = App::new();
    app.add_plugins(MinimalPlugins)
        .init_resource::<Assets<Mesh>>();
    app.add_plugins(
        VisualValleyTerrainQualityPlugin::new(&terrain, policy, VisualQualityTier::High).unwrap(),
    );
    for chunk in &generated.chunks {
        let (mesh, _) = crate::build_bevy_terrain_mesh(chunk).unwrap();
        let handle = app.world_mut().resource_mut::<Assets<Mesh>>().add(mesh);
        app.world_mut().spawn((
            VisualValleyTerrainChunk {
                id: chunk.id,
                coordinate: chunk.coordinate,
                lod_level: 0,
            },
            Mesh3d(handle),
            MeshMaterial3d::<VisualValleyTerrainMaterial>(Handle::default()),
            Transform::IDENTITY,
        ));
    }
    app.insert_resource(TerrainRuntimeState::new(terrain, generated));
    app
}

fn derived_bindings(app: &App) -> Vec<(Entity, bevy::asset::AssetId<Mesh>)> {
    app.world()
        .resource::<TerrainLodCache>()
        .chunks
        .values()
        .flat_map(|chunk| {
            chunk
                .variants
                .iter()
                .map(|variant| (variant.entity, variant.mesh.id()))
        })
        .collect()
}

#[test]
#[cfg(not(target_arch = "wasm32"))]
fn replacement_policy_at_same_tier_rebuilds_without_changing_sources_or_handles() {
    for detached in [false, true] {
        let mut app = app();
        app.update();
        let bindings = derived_bindings(&app);
        let source =
            canonical_json_sha256(app.world().resource::<TerrainRuntimeState>().cache()).unwrap();
        app.world_mut().resource_mut::<TerrainLodCache>().background = detached;
        let (hash, settings) = {
            let mut request = app.world_mut().resource_mut::<TerrainQualityRequest>();
            request.document.presets.high.terrain = request.document.presets.medium.terrain;
            request.document.validate().unwrap();
            request.source_hash = request.document.content_hash().unwrap().to_string();
            (
                request.source_hash.clone(),
                request.document.presets.high.terrain,
            )
        };
        app.update();
        if detached {
            assert_eq!(
                app.world()
                    .resource::<TerrainLodProjectionStats>()
                    .projected_tier,
                None
            );
            assert_eq!(derived_bindings(&app), bindings);
            let deadline = std::time::Instant::now() + std::time::Duration::from_secs(30);
            while app
                .world()
                .resource::<TerrainLodProjectionStats>()
                .projected_tier
                .is_none()
            {
                assert!(
                    std::time::Instant::now() < deadline,
                    "policy replacement timed out"
                );
                std::thread::yield_now();
                app.update();
            }
        }
        let stats = app.world().resource::<TerrainLodProjectionStats>();
        assert_eq!(stats.policy_hash, hash);
        assert_eq!(stats.projected_tier, Some(VisualQualityTier::High));
        assert_eq!(stats.last_rebuilt_chunks, stats.canonical_chunks);
        assert_eq!(derived_bindings(&app), bindings);
        for (entity, _) in bindings.iter().step_by(3) {
            let band = app.world().get::<TerrainMorphBand>(*entity).unwrap();
            assert_eq!(band.start.to_bits(), settings.lod0_distance_m.to_bits());
            assert_eq!(band.end.to_bits(), settings.lod1_distance_m.to_bits());
        }
        assert_eq!(
            canonical_json_sha256(app.world().resource::<TerrainRuntimeState>().cache()).unwrap(),
            source
        );
        app.update();
        assert_eq!(derived_bindings(&app), bindings);
    }
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "single quality-cycle fixture checks source, handles, bounds and morph payloads together"
)]
fn quality_cycle_preserves_canonical_meshes_source_and_derived_handles() {
    let mut app = app();
    let canonical = app
        .world_mut()
        .query::<(Entity, &VisualValleyTerrainChunk, &Mesh3d)>()
        .iter(app.world())
        .map(|(entity, _, mesh)| (entity, mesh.0.clone()))
        .collect::<Vec<_>>();
    let source_hash =
        canonical_json_sha256(app.world().resource::<TerrainRuntimeState>().cache()).unwrap();
    app.update();
    let derived = derived_bindings(&app);
    for tier in [
        VisualQualityTier::High,
        VisualQualityTier::Low,
        VisualQualityTier::Ultra,
        VisualQualityTier::Medium,
        VisualQualityTier::High,
    ] {
        app.world_mut()
            .resource_mut::<TerrainQualityRequest>()
            .request(tier);
        app.update();
        let world = app.world();
        let stats = world.resource::<TerrainLodProjectionStats>();
        assert_eq!(stats.projected_tier, Some(tier));
        let settings = world
            .resource::<TerrainQualityRequest>()
            .document
            .preset(tier)
            .terrain;
        let topologies = [1, settings.lod1_sample_stride, settings.lod2_sample_stride]
            .map(|stride| TerrainLodTopology::new(stride).unwrap());
        // Exact deduplication depends on each chunk's attributes, not only its grid stride.
        let assets = world.resource::<Assets<Mesh>>();
        assert_eq!(
            stats.derived_vertices,
            derived
                .iter()
                .map(|(_, id)| assets.get(*id).unwrap().count_vertices())
                .sum::<usize>()
        );
        assert_eq!(
            stats.derived_triangles,
            derived
                .iter()
                .map(|(_, id)| assets.get(*id).unwrap().indices().unwrap().len() / 3)
                .sum::<usize>()
        );
        assert_eq!(derived_bindings(&app), derived);
        assert_eq!(world.resource::<Assets<Mesh>>().len(), 256);
        assert_eq!(
            canonical_json_sha256(world.resource::<TerrainRuntimeState>().cache()).unwrap(),
            source_hash
        );
        for (entity, handle) in &canonical {
            assert_eq!(world.get::<Mesh3d>(*entity).unwrap().0.id(), handle.id());
            let mesh = world.resource::<Assets<Mesh>>().get(handle).unwrap();
            let chunk = &world
                .resource::<TerrainRuntimeState>()
                .cache()
                .chunks
                .iter()
                .find(|chunk| {
                    chunk.id == world.get::<VisualValleyTerrainChunk>(*entity).unwrap().id
                })
                .unwrap();
            assert_eq!(
                mesh.attribute(Mesh::ATTRIBUTE_POSITION)
                    .unwrap()
                    .as_float3()
                    .unwrap(),
                chunk.positions_m
            );
            assert_eq!(mesh.indices().unwrap().len(), 6144);
            let cache = &world.resource::<TerrainLodCache>().chunks[&chunk.id];
            assert_eq!(world.get::<Visibility>(*entity), Some(&Visibility::Hidden));
            let bounds = world.get::<Aabb>(cache.variants[0].entity).unwrap();
            let transform = world.get::<Transform>(cache.variants[0].entity).unwrap();
            let rendered_full = world
                .resource::<Assets<Mesh>>()
                .get(&cache.variants[0].mesh)
                .unwrap()
                .attribute(Mesh::ATTRIBUTE_POSITION)
                .unwrap()
                .as_float3()
                .unwrap();
            let endpoint = topologies[0]
                .project_transition(chunk, &topologies[1])
                .unwrap()
                .0;
            assert_eq!(rendered_full.len(), endpoint.positions_m.len());
            for (local, source) in rendered_full.iter().zip(&endpoint.positions_m) {
                let projected = transform.transform_point(Vec3::from_array(*local));
                assert!(projected.abs_diff_eq(Vec3::from_array(*source), 0.0001));
            }
            for (level, variant) in cache.variants.iter().enumerate() {
                let mesh = world.resource::<Assets<Mesh>>().get(&variant.mesh).unwrap();
                assert_eq!(mesh.has_morph_targets(), level < 2);
                if level < 2 {
                    assert_eq!(
                        mesh.get_morph_targets().unwrap().len(),
                        mesh.count_vertices()
                    );
                }
                let derived_bounds = world.get::<Aabb>(variant.entity).unwrap();
                assert_eq!(bounds.center, derived_bounds.center);
                assert_eq!(bounds.half_extents, derived_bounds.half_extents);
                assert_eq!(world.get::<Transform>(variant.entity).unwrap(), transform);
            }
        }
    }
}

#[test]
fn synthetic_source_refresh_rebuilds_only_the_changed_chunk() {
    let mut app = app();
    app.update();
    let bindings = derived_bindings(&app);
    let mut document = app
        .world()
        .resource::<TerrainRuntimeState>()
        .document()
        .migrate_to_latest()
        .unwrap();
    document.revision = forge_core::Revision::new(2);
    document
        .height_deltas
        .push(forge_project::TerrainHeightDelta::new(16, 16, 1.0));
    let generated = TerrainGenerationPlan::from_document(&document)
        .unwrap()
        .generate()
        .unwrap();
    let hash = generated.source_document_hash.to_string();
    // Synthetic cache invalidation only: the native probe separately exercises the transaction.
    app.insert_resource(TerrainRuntimeState::new(document, generated));
    app.update();
    let stats = app.world().resource::<TerrainLodProjectionStats>();
    assert_eq!(stats.last_rebuilt_chunks, 1);
    assert_eq!(stats.terrain_hash, hash);
    assert_eq!(derived_bindings(&app), bindings);
}

#[test]
fn missing_derived_binding_rejects_the_entire_quality_change() {
    let mut app = app();
    app.update();
    let bindings = derived_bindings(&app);
    let previous_counts = bindings
        .iter()
        .map(|(_, mesh)| {
            app.world()
                .resource::<Assets<Mesh>>()
                .get(*mesh)
                .unwrap()
                .count_vertices()
        })
        .collect::<Vec<_>>();
    let previous_vertices = app
        .world()
        .resource::<TerrainLodProjectionStats>()
        .derived_vertices;
    app.world_mut()
        .entity_mut(bindings.last().unwrap().0)
        .remove::<Mesh3d>();
    app.world_mut()
        .resource_mut::<TerrainQualityRequest>()
        .request(VisualQualityTier::Low);
    app.update();
    let stats = app.world().resource::<TerrainLodProjectionStats>();
    assert_eq!(stats.status, "projection_failed");
    assert_eq!(stats.projected_tier, None);
    assert_eq!(stats.derived_vertices, previous_vertices);
    assert_eq!(
        app.world().resource::<TerrainLodCache>().tier,
        Some(VisualQualityTier::High)
    );
    for (entity, mesh) in &bindings[..bindings.len() - 1] {
        assert_eq!(app.world().get::<Mesh3d>(*entity).unwrap().0.id(), *mesh);
    }
    for (index, (_, mesh)) in bindings.iter().enumerate() {
        let mesh = app.world().resource::<Assets<Mesh>>().get(*mesh).unwrap();
        assert_eq!(mesh.count_vertices(), previous_counts[index]);
    }
}

#[test]
fn valley_horizon_stays_opaque_from_author_cameras_and_area_bounds() {
    let sketch = forge_world::embedded_visual_valley_world_sketch().unwrap();
    let compiled = forge_world::compile_world_sketch(&sketch).unwrap();
    let generated = TerrainGenerationPlan::from_document(&compiled.carved_terrain)
        .unwrap()
        .generate()
        .unwrap();
    let policy = VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap();
    let mut minimum = Vec3::splat(f32::INFINITY);
    let mut maximum = Vec3::splat(f32::NEG_INFINITY);
    let mut centers = Vec::new();
    for chunk in &generated.chunks {
        let mut low = Vec3::splat(f32::INFINITY);
        let mut high = Vec3::splat(f32::NEG_INFINITY);
        for position in &chunk.positions_m {
            let position = Vec3::from_array(*position);
            low = low.min(position);
            high = high.max(position);
        }
        minimum = minimum.min(low);
        maximum = maximum.max(high);
        centers.push((low + high) * 0.5);
    }
    let mut cameras = sketch
        .validation_cameras
        .iter()
        .map(|camera| Vec3::from_array(camera.position_m))
        .collect::<Vec<_>>();
    // Bounding-box corners conservatively cover all ground-level positions in the finite area.
    for x in [minimum.x, maximum.x] {
        for z in [minimum.z, maximum.z] {
            for y in [minimum.y, maximum.y] {
                cameras.push(Vec3::new(x, y + 2.0, z));
            }
        }
    }
    for tier in [
        VisualQualityTier::Low,
        VisualQualityTier::Medium,
        VisualQualityTier::High,
        VisualQualityTier::Ultra,
    ] {
        let fade_start = ranges(policy.preset(tier).terrain)[2].end_margin.start;
        for camera in &cameras {
            for center in &centers {
                assert!(
                    camera.distance(*center) < fade_start,
                    "{tier:?}: terrain fades at {fade_start}m, camera {camera:?}, chunk {center:?}"
                );
            }
        }
    }
}

#[test]
fn ranges_are_complementary_and_invalid_policy_is_rejected() {
    let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    let mut policy = VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap();
    for tier in [
        VisualQualityTier::Low,
        VisualQualityTier::Medium,
        VisualQualityTier::High,
        VisualQualityTier::Ultra,
    ] {
        let settings = policy.preset(tier).terrain;
        let bands = ranges(settings);
        assert_eq!(bands[0].end_margin, bands[1].start_margin);
        assert_eq!(bands[1].end_margin, bands[2].start_margin);
        assert_eq!(
            bands[2].end_margin.end.to_bits(),
            settings.cull_distance_m.to_bits()
        );
        assert!(bands.iter().all(|range| !range.use_aabb));
    }
    policy.presets.high.terrain.lod1_sample_stride = 3;
    assert!(
        VisualValleyTerrainQualityPlugin::new(&terrain, policy, VisualQualityTier::High).is_err()
    );
}
