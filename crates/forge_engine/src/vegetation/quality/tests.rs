use super::*;
use crate::vegetation::{
    PreparedProjectionMarker, VegetationProjectionLod, prepare_vegetation_projection_with_quality,
};

#[test]
fn authoring_sources_reproject_without_a_quality_change() {
    use crate::vegetation::*;
    let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    let mut vegetation = forge_world::editor_vegetation::prepare(&terrain, 42).unwrap();
    vegetation.cells_per_chunk_axis = 1;
    vegetation.layers.truncate(1);
    vegetation.layers[0].collision = Some(forge_project::VegetationCollisionProfile {
        shape: forge_project::VegetationCollisionShape::Cylinder,
        radius_m: 0.3,
        height_m: 5.0,
        activation_distance_m: 96.0,
    });
    let policy = VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap();
    let request = VegetationQualityRequest::new(&terrain, policy, VisualQualityTier::Low).unwrap();
    let prepared =
        prepare_vegetation_projection_with_quality(&terrain, &vegetation, Some(&request)).unwrap();
    let mut app = bevy::prelude::App::new();
    app.init_resource::<bevy::prelude::Assets<bevy::prelude::Mesh>>()
        .init_resource::<bevy::prelude::Time>()
        .init_resource::<bevy::prelude::Assets<VisualValleyVegetationMaterial>>()
        .insert_resource(VegetationPresentation(prepared.profile))
        .insert_resource(VegetationInteractionField::disabled())
        .insert_resource(prepared.stats.clone())
        .insert_resource(PreparedVegetation(Some(prepared)))
        .insert_resource(VegetationQualityBindings::new(
            terrain.clone(),
            vegetation.clone(),
        ))
        .insert_resource(request)
        .add_systems(bevy::prelude::Startup, spawn_vegetation_projection)
        .add_systems(bevy::prelude::Update, apply_quality);
    app.update();
    let old_hash = vegetation.content_hash().unwrap().to_string();
    #[cfg(feature = "physics-rapier")]
    let original_trunks = source_trunks(app.world_mut());
    vegetation.seed = 43;
    assert!(
        update_vegetation_sources(app.world_mut(), terrain.clone(), vegetation.clone()).unwrap()
    );
    assert_eq!(
        app.world()
            .resource::<VegetationProjectionStats>()
            .vegetation_document_hash,
        old_hash
    );
    // A newer accepted recipe replaces the pending request, not the live render early.
    vegetation.seed = 44;
    assert!(
        update_vegetation_sources(app.world_mut(), terrain.clone(), vegetation.clone()).unwrap()
    );
    app.update();
    let stats = app.world().resource::<VegetationProjectionStats>();
    assert_eq!(
        stats.vegetation_document_hash,
        vegetation.content_hash().unwrap().to_string()
    );
    assert_eq!(
        stats.quality.as_ref().unwrap().projected_tier,
        VisualQualityTier::Low
    );
    #[cfg(feature = "physics-rapier")]
    {
        let expected =
            prepare_vegetation_projection_with_quality(&terrain, &vegetation, None).unwrap();
        let actual = source_trunks(app.world_mut());
        assert!(!actual.is_empty());
        assert_ne!(actual, original_trunks);
        assert_eq!(actual.len(), expected.tree_colliders.len());
        for trunk in expected.tree_colliders {
            assert_eq!(actual.get(&trunk.id), Some(&trunk.transform));
        }
    }
    assert!(
        !update_vegetation_sources(app.world_mut(), terrain.clone(), vegetation.clone()).unwrap()
    );
    let before = app
        .world()
        .resource::<VegetationProjectionStats>()
        .vegetation_document_hash
        .clone();
    #[cfg(feature = "physics-rapier")]
    let retained_trunks = source_trunks(app.world_mut());
    vegetation.layers[0].acceptance_probability = f32::NAN;
    assert!(update_vegetation_sources(app.world_mut(), terrain, vegetation).is_err());
    app.update();
    assert_eq!(
        app.world()
            .resource::<VegetationProjectionStats>()
            .vegetation_document_hash,
        before
    );
    #[cfg(feature = "physics-rapier")]
    assert_eq!(source_trunks(app.world_mut()), retained_trunks);
}

#[cfg(feature = "physics-rapier")]
fn source_trunks(
    world: &mut bevy::prelude::World,
) -> std::collections::BTreeMap<forge_world::VegetationInstanceId, bevy::prelude::Transform> {
    use crate::vegetation::collision::TreeTrunkCollider;
    use bevy::prelude::Transform;
    world
        .query::<(&TreeTrunkCollider, &Transform)>()
        .iter(world)
        .map(|(trunk, transform)| (trunk.0, *transform))
        .collect()
}

#[test]
#[ignore = "explicit CPU preparation probe; no timing threshold or GPU qualification"]
fn quality_preparation_cpu_probe() {
    let source = forge_world::compile_world_sketch(
        &forge_world::embedded_visual_valley_world_sketch().unwrap(),
    )
    .unwrap();
    let mut policy = VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap();
    policy.revision = source.carved_terrain.revision;
    for round in 0..3 {
        for tier in [
            VisualQualityTier::Low,
            VisualQualityTier::Ultra,
            VisualQualityTier::High,
        ] {
            let request =
                VegetationQualityRequest::new(&source.carved_terrain, policy.clone(), tier)
                    .unwrap();
            let started = std::time::Instant::now();
            let plan = forge_world::VegetationGenerationPlan::from_documents(
                &source.carved_terrain,
                &source.vegetation,
            )
            .unwrap();
            let canonical = plan.generate().unwrap();
            let generation_ms = started.elapsed().as_secs_f64() * 1000.0;
            let started = std::time::Instant::now();
            let prepared = prepare_vegetation_projection_with_quality(
                &source.carved_terrain,
                &source.vegetation,
                Some(&request),
            )
            .unwrap();
            let preparation_ms = started.elapsed().as_secs_f64() * 1000.0;
            println!(
                "quality_cpu_probe round={round} tier={tier:?} canonical_ms={generation_ms:.3} fresh_preparation_ms={preparation_ms:.3} canonical_placements={} triangles={}",
                canonical.kind_counts.total(),
                prepared.stats.triangles.unique_mesh_asset_triangles
            );
        }
    }
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "one end-to-end ECS cycle retains its initial bindings through success and rejection"
)]
fn quality_cycle_preserves_surviving_bindings_material_and_sources() {
    use crate::vegetation::*;
    let source = forge_world::compile_world_sketch(
        &forge_world::embedded_visual_valley_world_sketch().unwrap(),
    )
    .unwrap();
    let mut policy = VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap();
    policy.revision = source.carved_terrain.revision;
    let request =
        VegetationQualityRequest::new(&source.carved_terrain, policy, VisualQualityTier::High)
            .unwrap();
    let prepared = prepare_vegetation_projection_with_quality(
        &source.carved_terrain,
        &source.vegetation,
        Some(&request),
    )
    .unwrap();
    let original_stats = prepared.stats.clone();
    let mut app = App::new();
    app.init_resource::<Assets<Mesh>>()
        .init_resource::<Time>()
        .init_resource::<Assets<VisualValleyVegetationMaterial>>()
        .insert_resource(VegetationPresentation(prepared.profile))
        .insert_resource(VegetationInteractionField::disabled())
        .insert_resource(prepared.stats.clone())
        .insert_resource(PreparedVegetation(Some(prepared)))
        .insert_resource(VegetationQualityBindings::new(
            source.carved_terrain.clone(),
            source.vegetation.clone(),
        ))
        .insert_resource(request)
        .add_systems(Startup, spawn_vegetation_projection)
        .add_systems(Update, apply_quality)
        .add_systems(
            Update,
            update_vegetation_interaction_uniform.after(apply_quality),
        );
    app.update();
    let original_tree_placements: Vec<_> = app
        .world()
        .resource::<VegetationScatterProjection>()
        .trees()
        .cloned()
        .collect();
    assert_eq!(original_tree_placements.len(), 1662);
    #[cfg(feature = "physics-rapier")]
    let original_colliders = tree_collision_bindings(app.world_mut());
    #[cfg(feature = "physics-rapier")]
    assert_eq!(original_colliders.len(), 1662);
    let meshes = app
        .world()
        .resource::<VegetationQualityBindings>()
        .meshes
        .clone();
    let entities = app
        .world()
        .resource::<VegetationQualityBindings>()
        .entities
        .clone();
    let material = app.world().resource::<VegetationMaterialHandle>().0.clone();
    let protected: Vec<_> = entities
        .iter()
        .filter(|(_, entity)| {
            app.world()
                .get::<VegetationPlacementMarker>(**entity)
                .is_some_and(|marker| marker.kind == forge_project::VegetationKind::Tree)
        })
        .collect();
    assert_eq!(protected.len(), 3324);
    let interaction = *app.world().resource::<VegetationInteractionField>();
    let original_material = app
        .world()
        .resource::<Assets<VisualValleyVegetationMaterial>>()
        .get(&material)
        .unwrap()
        .extension
        .wetness_parameters;
    for tier in [
        VisualQualityTier::Low,
        VisualQualityTier::Medium,
        VisualQualityTier::Ultra,
        VisualQualityTier::High,
    ] {
        app.world_mut()
            .resource_mut::<VegetationQualityRequest>()
            .request(tier);
        app.update();
        let current_trees: Vec<_> = app
            .world()
            .resource::<VegetationScatterProjection>()
            .trees()
            .cloned()
            .collect();
        assert_eq!(current_trees, original_tree_placements);
        for entity in app
            .world()
            .resource::<VegetationQualityBindings>()
            .entities
            .values()
        {
            if let Some(marker) = app.world().get::<VegetationPlacementMarker>(*entity)
                && marker.kind == forge_project::VegetationKind::Tree
            {
                let source = current_trees
                    .iter()
                    .find(|tree| tree.id == marker.id)
                    .unwrap();
                assert_eq!(marker.layer_id, source.layer_id);
            }
        }
        let projected_profile = app.world().resource::<VegetationPresentation>().0;
        let expected_range = VegetationLodRanges::from_profile(projected_profile).hlod;
        let actual_uniform = app
            .world()
            .resource::<Assets<VisualValleyVegetationMaterial>>()
            .get(&material)
            .unwrap()
            .extension
            .hlod_distances;
        assert_eq!(
            actual_uniform.to_array().map(f32::to_bits),
            [
                expected_range.start_margin.start,
                expected_range.start_margin.end,
                projected_profile.cull_distance_m,
                0.0
            ]
            .map(f32::to_bits)
        );
        #[cfg(feature = "physics-rapier")]
        assert_eq!(tree_collision_bindings(app.world_mut()), original_colliders);
        let stats = app.world().resource::<VegetationProjectionStats>();
        assert_eq!(stats.quality.as_ref().unwrap().projected_tier, tier);
        assert_eq!(
            stats.terrain_document_hash,
            original_stats.terrain_document_hash
        );
        assert_eq!(
            stats.vegetation_document_hash,
            original_stats.vegetation_document_hash
        );
        assert_eq!(stats.placements.accepted_by_kind.trees, 1662);
        let bindings = app.world().resource::<VegetationQualityBindings>();
        for (key, (handle, _)) in &meshes {
            assert_eq!(bindings.meshes[key].0.id(), handle.id());
        }
        // Trees survive all levels, so both of their LOD entities retain identity.
        for (key, entity) in &protected {
            assert_eq!(bindings.entities[*key], **entity);
            assert!(
                app.world()
                    .get::<VegetationPlacementMarker>(**entity)
                    .is_some()
            );
        }
        assert_eq!(
            app.world().resource::<VegetationMaterialHandle>().0.id(),
            material.id()
        );
        assert_eq!(
            *app.world().resource::<VegetationInteractionField>(),
            interaction
        );
        assert_eq!(
            app.world()
                .resource::<Assets<VisualValleyVegetationMaterial>>()
                .get(&material)
                .unwrap()
                .extension
                .wetness_parameters,
            original_material
        );
    }
    assert_eq!(
        app.world()
            .resource::<VegetationProjectionStats>()
            .projection,
        original_stats.projection
    );
    // A replacement policy must be applied even when the named tier stays High.
    let mut replacement = app
        .world()
        .resource::<VegetationQualityRequest>()
        .document
        .clone();
    replacement.presets.high.vegetation = replacement.presets.medium.vegetation;
    replacement.validate().unwrap();
    let expected_hash = replacement.content_hash().unwrap().to_string();
    let replacement_request = VegetationQualityRequest::new(
        &source.carved_terrain,
        replacement.clone(),
        VisualQualityTier::High,
    )
    .unwrap();
    let expected_profile = replacement_request.profile(source.vegetation.presentation);
    app.insert_resource(replacement_request);
    app.update();
    assert_eq!(
        app.world().resource::<VegetationPresentation>().0,
        expected_profile
    );
    assert_eq!(
        app.world()
            .resource::<VegetationProjectionStats>()
            .quality
            .as_ref()
            .unwrap()
            .policy_hash,
        expected_hash
    );
    assert_eq!(
        app.world()
            .resource::<VegetationProjectionStats>()
            .vegetation_document_hash,
        original_stats.vegetation_document_hash
    );
    #[cfg(feature = "physics-rapier")]
    assert_eq!(tree_collision_bindings(app.world_mut()), original_colliders);

    let before = app
        .world()
        .resource::<VegetationQualityBindings>()
        .entities
        .clone();
    {
        let mut request = app.world_mut().resource_mut::<VegetationQualityRequest>();
        request.document.presets.low.vegetation.near_lod_distance_m = 1.0;
        request
            .document
            .presets
            .low
            .vegetation
            .medium_lod_distance_m = 2.0;
        request.document.presets.low.vegetation.hlod_distance_m = 3.0;
        request.document.presets.low.vegetation.cull_distance_m = 4.0;
        request.request(VisualQualityTier::Low);
    }
    app.update();
    assert_eq!(
        app.world().resource::<VegetationQualityBindings>().entities,
        before
    );
    let quality = app
        .world()
        .resource::<VegetationProjectionStats>()
        .quality
        .as_ref()
        .unwrap();
    assert_eq!(quality.projected_tier, VisualQualityTier::High);
    assert_eq!(quality.requested_tier, VisualQualityTier::Low);
    assert_eq!(quality.status, "rejected_previous_cpu_projection_retained");
    #[cfg(feature = "physics-rapier")]
    assert_eq!(tree_collision_bindings(app.world_mut()), original_colliders);

    // A corrected policy at the failed tier is a new attempt, not a sticky failure.
    replacement.name.push_str(" corrected");
    let corrected =
        VegetationQualityRequest::new(&source.carved_terrain, replacement, VisualQualityTier::Low)
            .unwrap();
    let corrected_hash = corrected.hash.clone();
    app.insert_resource(corrected);
    app.update();
    let stats = app.world().resource::<VegetationProjectionStats>();
    let quality = stats.quality.as_ref().unwrap();
    assert_eq!(quality.projected_tier, VisualQualityTier::Low);
    assert_eq!(quality.policy_hash, corrected_hash);
    assert_eq!(
        stats.terrain_document_hash,
        original_stats.terrain_document_hash
    );
    assert_eq!(
        stats.vegetation_document_hash,
        original_stats.vegetation_document_hash
    );
}

#[cfg(feature = "physics-rapier")]
fn tree_collision_bindings(
    world: &mut bevy::prelude::World,
) -> std::collections::HashMap<
    bevy::prelude::Entity,
    (
        forge_world::VegetationInstanceId,
        bevy::prelude::Transform,
        f32,
        f32,
    ),
> {
    use crate::vegetation::collision::TreeTrunkCollider;
    use bevy::camera::visibility::VisibilityRange;
    use bevy::prelude::*;
    use bevy_rapier3d::prelude::*;
    world
        .query::<(
            Entity,
            &TreeTrunkCollider,
            &Transform,
            &Collider,
            &RigidBody,
            Option<&Mesh3d>,
            Option<&VisibilityRange>,
        )>()
        .iter(world)
        .map(|(entity, marker, transform, collider, body, mesh, range)| {
            assert_eq!(*body, RigidBody::Fixed);
            assert!(mesh.is_none() && range.is_none());
            let capsule = collider.as_capsule().unwrap();
            (
                entity,
                (
                    marker.0,
                    *transform,
                    capsule.radius(),
                    capsule.half_height(),
                ),
            )
        })
        .collect()
}

#[test]
fn initial_quality_projects_density_ranges_and_grass_without_rewriting_sources() {
    let world = forge_world::compile_world_sketch(
        &forge_world::embedded_visual_valley_world_sketch().unwrap(),
    )
    .unwrap();
    let mut policy = VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap();
    policy.revision = world.carved_terrain.revision;
    let terrain_hash = world.carved_terrain.content_hash().unwrap();
    let vegetation_hash = world.vegetation.content_hash().unwrap();
    for tier in [
        VisualQualityTier::Low,
        VisualQualityTier::Medium,
        VisualQualityTier::High,
        VisualQualityTier::Ultra,
    ] {
        let request =
            VegetationQualityRequest::new(&world.carved_terrain, policy.clone(), tier).unwrap();
        request.validate_vegetation(&world.vegetation).unwrap();
        let prepared = prepare_vegetation_projection_with_quality(
            &world.carved_terrain,
            &world.vegetation,
            Some(&request),
        )
        .unwrap();
        let settings = policy.preset(tier).vegetation;
        assert_eq!(
            prepared.stats.terrain_document_hash,
            terrain_hash.to_string()
        );
        assert_eq!(
            prepared.stats.vegetation_document_hash,
            vegetation_hash.to_string()
        );
        assert_eq!(
            prepared
                .stats
                .quality
                .as_ref()
                .unwrap()
                .canonical_placements,
            23353
        );
        assert_eq!(prepared.stats.placements.accepted_by_kind.trees, 1662);
        #[cfg(feature = "physics-rapier")]
        assert_eq!(prepared.tree_colliders.len(), 1662);
        assert_eq!(
            prepared.stats.projection.grass_blades,
            u64::from(prepared.stats.placements.accepted_by_kind.grass_patches)
                * u64::from(settings.grass_blades_per_patch)
        );
        assert_eq!(
            prepared.profile.wind_strength.to_bits(),
            world.vegetation.presentation.wind_strength.to_bits()
        );
        assert_eq!(
            prepared.profile.wind_speed_hz.to_bits(),
            world.vegetation.presentation.wind_speed_hz.to_bits()
        );
        assert_eq!(
            prepared.profile.interaction_radius_m.to_bits(),
            world.vegetation.presentation.interaction_radius_m.to_bits()
        );
        for entity in &prepared.entities {
            if let PreparedProjectionMarker::Placement(marker) = &entity.marker
                && marker.lod == VegetationProjectionLod::Near
                && marker.kind == forge_project::VegetationKind::Tree
            {
                assert_eq!(
                    entity.visibility_range.end_margin,
                    settings
                        .near_lod_distance_m
                        .max(settings.medium_lod_distance_m - 8.0)
                        ..settings.medium_lod_distance_m
                );
            }
        }
        println!(
            "{tier:?} placements={} entities={} grass_blades={} triangles={}",
            prepared.stats.placements.accepted_placements,
            prepared.stats.projection.total_entities,
            prepared.stats.projection.grass_blades,
            prepared.stats.triangles.unique_mesh_asset_triangles
        );
    }
    assert_eq!(world.carved_terrain.content_hash().unwrap(), terrain_hash);
    assert_eq!(world.vegetation.content_hash().unwrap(), vegetation_hash);
    policy.presets.low.vegetation.near_lod_distance_m = 1.0;
    policy.presets.low.vegetation.medium_lod_distance_m = 2.0;
    policy.presets.low.vegetation.hlod_distance_m = 3.0;
    policy.presets.low.vegetation.cull_distance_m = 4.0;
    let request =
        VegetationQualityRequest::new(&world.carved_terrain, policy, VisualQualityTier::Low)
            .unwrap();
    assert!(request.validate_vegetation(&world.vegetation).is_err());
}
