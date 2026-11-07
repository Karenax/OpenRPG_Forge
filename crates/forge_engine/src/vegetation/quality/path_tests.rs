use super::*;
use crate::vegetation::*;
use forge_project::zone_authoring::paths::{AuthoredPath, PathPoint};

fn room() -> forge_project::zone_authoring::buildings::AuthoredBuilding {
    forge_project::zone_authoring::buildings::AuthoredBuilding {
        id: forge_core::EntityId::deterministic("projection-room"),
        name: "Room".into(),
        generator_version: 1,
        origin_m: forge_core::Vec3::ZERO,
        yaw_degrees: 45.0,
        locked: false,
        door_offset_override_m: None,
        dimensions: forge_project::building::RoomDimensions {
            width_m: 128.0,
            depth_m: 128.0,
            clear_height_m: 3.0,
            wall_m: 0.25,
            slab_m: 0.2,
            door_width_m: 1.4,
            door_height_m: 2.2,
            door_offset_x_m: 0.0,
        },
    }
}

fn initial_head() -> forge_project::zone_authoring::ZoneAuthoringHead {
    use forge_project::zone_authoring::{
        GeneratorStamp, ZoneAuthoringHead, ZoneBase, ZoneCorrections,
    };
    let (_, terrain, vegetation) = fixture();
    let mut head = ZoneAuthoringHead {
        schema: "forge.zone-authoring".into(),
        version: 1,
        revision: terrain.revision,
        base: ZoneBase {
            generator: GeneratorStamp {
                name: "initial-vegetation-fixture".into(),
                version: "1".into(),
                inputs_hash: terrain.content_hash().unwrap(),
            },
            terrain,
            vegetation: Some(vegetation),
        },
        corrections: ZoneCorrections::default(),
    };
    head.corrections.paths = Some(paths());
    head.corrections.rivers = Some(rivers(&head.base.terrain));
    head.corrections.buildings = Some(vec![room()]);
    head.version = head.corrections.schema_version();
    head.validate().unwrap();
    head
}

fn head_plan(head: &forge_project::zone_authoring::ZoneAuthoringHead) -> VegetationGenerationPlan {
    let (terrain, source) = head.effective().unwrap();
    let source = source.unwrap();
    VegetationGenerationPlan::from_documents_with_buildings(
        &terrain,
        &source,
        head.corrections.paths.as_deref().unwrap(),
        head.corrections.rivers.as_deref().unwrap(),
        head.corrections.buildings.as_deref().unwrap(),
    )
    .unwrap()
    .with_promotions(head.corrections.promotions.as_deref().unwrap_or_default())
    .unwrap()
}

fn promote_first_tree(
    head: &mut forge_project::zone_authoring::ZoneAuthoringHead,
) -> forge_world::VegetationInstanceId {
    use forge_project::zone_authoring::promotion::PromotedInstance;
    let plan = head_plan(head);
    let canonical = plan.generate().unwrap();
    let tree = canonical
        .chunks
        .iter()
        .flat_map(|chunk| &chunk.instances)
        .next()
        .unwrap();
    let promoted_id = tree.id;
    head.corrections.promotions = Some(vec![PromotedInstance {
        layer_id: tree.layer_id.clone(),
        instance_id: tree.id.as_entity_id(),
        generation_hash: canonical.content_hash().unwrap(),
        placement_id: forge_core::EntityId::deterministic("initial-promoted-placement"),
    }]);
    head.version = head.corrections.schema_version();
    promoted_id
}

#[test]
fn initial_head_preserves_clearances_and_promotions_across_all_quality_tiers() {
    let mut head = initial_head();
    let promoted_id = promote_first_tree(&mut head);
    let (terrain, source) = head.effective().unwrap();
    let source = source.unwrap();
    let before = head.content_hash().unwrap();
    let plugin = VisualValleyVegetationPlugin::new(terrain.clone(), source.clone())
        .with_authoring_head(&head)
        .unwrap();
    let plan = head_plan(&head);
    let expected_hash = plan.source_hash().to_string();
    assert_ne!(expected_hash, source.content_hash().unwrap().to_string());
    let (old, _, _) = fixture();
    let request = old.world().resource::<VegetationQualityRequest>().clone();
    let mut app = App::new();
    app.init_resource::<Assets<Mesh>>()
        .init_resource::<Time>()
        .init_resource::<Assets<VisualValleyVegetationMaterial>>()
        // This test observes the CPU projection, without installing a renderer.
        .init_resource::<crate::temporal_lod::render::Groups>()
        .insert_resource(request);
    plugin.build(&mut app);
    app.world_mut().run_schedule(Startup);
    for tier in [
        VisualQualityTier::Low,
        VisualQualityTier::Medium,
        VisualQualityTier::High,
        VisualQualityTier::Ultra,
    ] {
        app.world_mut()
            .resource_mut::<VegetationQualityRequest>()
            .request(tier);
        apply_quality(app.world_mut());
        let expected = bevy::tasks::block_on(prepare_vegetation_projection_from_plan(
            &plan,
            Some(app.world().resource::<VegetationQualityRequest>()),
            false,
            None,
        ))
        .unwrap();
        let stats = app.world().resource::<VegetationProjectionStats>();
        assert_eq!(stats.vegetation_document_hash, expected_hash);
        assert_eq!(stats.placements, expected.stats.placements);
        assert_eq!(stats.quality.as_ref().unwrap().projected_tier, tier);
        let scatter = app.world().resource::<VegetationScatterProjection>();
        assert_eq!(
            scatter.generated.content_hash().unwrap(),
            expected.scatter.generated.content_hash().unwrap(),
        );
        assert!(
            scatter
                .generated
                .chunks
                .iter()
                .flat_map(|chunk| &chunk.instances)
                .all(|instance| instance.id != promoted_id)
        );
    }
    assert!(
        !update_vegetation_sources_with_promotions(
            app.world_mut(),
            terrain,
            source,
            head.corrections.paths.clone().unwrap(),
            head.corrections.rivers.clone().unwrap(),
            head.corrections.buildings.clone().unwrap(),
            head.corrections.promotions.clone().unwrap(),
        )
        .unwrap()
    );
    assert_eq!(head.content_hash().unwrap(), before);
}

#[test]
fn initial_head_rejects_different_documents_and_unresolved_promotions() {
    use forge_project::zone_authoring::promotion::PromotedInstance;
    let mut head = initial_head();
    let (terrain, source) = head.effective().unwrap();
    let source = source.unwrap();
    let mut different = source.clone();
    different.seed = different.seed.wrapping_add(1);
    assert!(
        VisualValleyVegetationPlugin::new(terrain.clone(), different)
            .with_authoring_head(&head)
            .is_err()
    );
    head.corrections.promotions = Some(vec![PromotedInstance {
        layer_id: source.layers[0].id.clone(),
        instance_id: forge_core::EntityId::deterministic("absent-initial-origin"),
        generation_hash: source.content_hash().unwrap(),
        placement_id: forge_core::EntityId::deterministic("absent-initial-placement"),
    }]);
    head.version = head.corrections.schema_version();
    head.validate().unwrap();
    assert!(
        VisualValleyVegetationPlugin::new(terrain, source)
            .with_authoring_head(&head)
            .is_err()
    );
}

#[test]
fn building_sources_project_and_removal_restores_population() {
    let (mut app, terrain, source) = fixture();
    let original = app
        .world()
        .resource::<VegetationProjectionStats>()
        .vegetation_document_hash
        .clone();
    let buildings = vec![room()];
    let expected = prepare_vegetation_projection_with_buildings(
        &terrain,
        &source,
        &[],
        &[],
        &buildings,
        Some(app.world().resource::<VegetationQualityRequest>()),
    )
    .unwrap();
    assert!(
        update_vegetation_sources_with_buildings(
            app.world_mut(),
            terrain.clone(),
            source.clone(),
            vec![],
            vec![],
            buildings.clone()
        )
        .unwrap()
    );
    app.update();
    let stats = app.world().resource::<VegetationProjectionStats>();
    assert_eq!(
        stats.vegetation_document_hash,
        expected.stats.vegetation_document_hash
    );
    assert_eq!(stats.placements, expected.stats.placements);
    assert_ne!(stats.vegetation_document_hash, original);
    assert!(
        !update_vegetation_sources_with_buildings(
            app.world_mut(),
            terrain.clone(),
            source.clone(),
            vec![],
            vec![],
            buildings.clone()
        )
        .unwrap()
    );
    let mut invalid = buildings;
    invalid[0].yaw_degrees = f32::NAN;
    assert!(
        update_vegetation_sources_with_buildings(
            app.world_mut(),
            terrain.clone(),
            source.clone(),
            vec![],
            vec![],
            invalid
        )
        .is_err()
    );
    app.update();
    assert_eq!(
        app.world()
            .resource::<VegetationProjectionStats>()
            .vegetation_document_hash,
        expected.stats.vegetation_document_hash
    );
    assert!(
        update_vegetation_sources_with_buildings(
            app.world_mut(),
            terrain,
            source,
            vec![],
            vec![],
            vec![]
        )
        .unwrap()
    );
    app.update();
    assert_eq!(
        app.world()
            .resource::<VegetationProjectionStats>()
            .vegetation_document_hash,
        original
    );
}

#[test]
fn room_metadata_preserves_vegetation_and_pending_quality_change() {
    let (mut app, terrain, source) = fixture();
    let mut building = room();
    let submit = |app: &mut App, building| {
        update_vegetation_sources_with_buildings(
            app.world_mut(),
            terrain.clone(),
            source.clone(),
            vec![],
            vec![],
            vec![building],
        )
    };
    assert!(submit(&mut app, building.clone()).unwrap());
    app.update();
    let key = app
        .world()
        .resource::<VegetationProjectionStats>()
        .vegetation_document_hash
        .clone();
    app.world_mut()
        .resource_mut::<VegetationQualityRequest>()
        .request(VisualQualityTier::Medium);
    building.name = "Renamed room".into();
    building.locked = true;
    building.dimensions.door_width_m = 1.8;
    building.door_offset_override_m = Some(1.5);
    assert!(!submit(&mut app, building.clone()).unwrap());
    app.update();
    let stats = app.world().resource::<VegetationProjectionStats>();
    assert_eq!(stats.vegetation_document_hash, key);
    assert_eq!(
        stats.quality.as_ref().unwrap().projected_tier,
        VisualQualityTier::Medium
    );
    let mut invalid = building.clone();
    invalid.dimensions.door_width_m = -1.0;
    assert!(submit(&mut app, invalid).is_err());
    assert!(!submit(&mut app, building.clone()).unwrap());
    building.origin_m.x = 20.0;
    assert!(submit(&mut app, building).unwrap());
    app.update();
    assert_ne!(
        app.world()
            .resource::<VegetationProjectionStats>()
            .vegetation_document_hash,
        key
    );
}

#[test]
fn revision_only_room_edit_keeps_live_entities_and_acknowledges_new_sources() {
    let (mut app, mut terrain, mut source) = fixture();
    let mut building = room();
    update_vegetation_sources_with_buildings(
        app.world_mut(),
        terrain.clone(),
        source.clone(),
        vec![],
        vec![],
        vec![building.clone()],
    )
    .unwrap();
    app.update();
    let entities = |app: &mut App| {
        let mut query = app.world_mut().query_filtered::<Entity, With<Mesh3d>>();
        let mut ids = query.iter(app.world()).collect::<Vec<_>>();
        ids.sort();
        ids
    };
    let before = entities(&mut app);
    assert!(!before.is_empty());
    terrain.revision = terrain.revision.next().unwrap();
    source.revision = terrain.revision;
    building.dimensions.door_width_m = 1.8;
    let expected = forge_world::VegetationGenerationPlan::from_documents_with_buildings(
        &terrain,
        &source,
        &[],
        &[],
        &[building.clone()],
    )
    .unwrap()
    .source_hash()
    .to_string();
    assert!(
        update_vegetation_sources_with_buildings(
            app.world_mut(),
            terrain.clone(),
            source.clone(),
            vec![],
            vec![],
            vec![building.clone()]
        )
        .unwrap()
    );
    app.update();
    assert_eq!(entities(&mut app), before);
    let stats = app.world().resource::<VegetationProjectionStats>();
    assert_eq!(
        stats.terrain_document_hash,
        terrain.content_hash().unwrap().to_string()
    );
    assert_eq!(stats.vegetation_document_hash, expected);
    assert_eq!(
        stats.quality.as_ref().unwrap().status,
        "reused_settled_geometry_new_source_receipt"
    );
    terrain.revision = terrain.revision.next().unwrap();
    source.revision = terrain.revision;
    building.origin_m.x = 20.0;
    update_vegetation_sources_with_buildings(
        app.world_mut(),
        terrain,
        source,
        vec![],
        vec![],
        vec![building],
    )
    .unwrap();
    app.update();
    assert_ne!(
        app.world()
            .resource::<VegetationProjectionStats>()
            .vegetation_document_hash,
        expected
    );
}

fn fixture() -> (
    App,
    forge_project::TerrainDocument,
    forge_project::VegetationDocument,
) {
    let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    let mut vegetation = forge_world::editor_vegetation::prepare(&terrain, 42).unwrap();
    vegetation.cells_per_chunk_axis = 1;
    vegetation.layers.truncate(1);
    vegetation.exclusions = (0..32)
        .map(|i| forge_project::VegetationExclusion::Circle {
            id: forge_project::VegetationExclusionId::new(format!("manual_{i}")),
            center_x_m: 150.0,
            center_z_m: 150.0,
            radius_m: 1.0,
        })
        .collect();
    let policy = VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap();
    let request = VegetationQualityRequest::new(&terrain, policy, VisualQualityTier::Low).unwrap();
    let prepared =
        prepare_vegetation_projection_with_quality(&terrain, &vegetation, Some(&request)).unwrap();
    let mut app = App::new();
    app.init_resource::<Assets<Mesh>>()
        .init_resource::<Time>()
        .init_resource::<Assets<VisualValleyVegetationMaterial>>()
        .insert_resource(VegetationPresentation(prepared.profile))
        .insert_resource(VegetationInteractionField::disabled())
        .insert_resource(prepared.stats.clone())
        .insert_resource(PreparedVegetation(Some(prepared)))
        .insert_resource(VegetationQualityBindings::new(
            terrain.clone(),
            vegetation.clone(),
        ))
        .insert_resource(request)
        .add_systems(Startup, spawn_vegetation_projection)
        .add_systems(Update, apply_quality);
    app.update();
    (app, terrain, vegetation)
}

fn paths() -> Vec<AuthoredPath> {
    let mut paths = (0..64)
        .map(|i| AuthoredPath {
            id: forge_core::EntityId::deterministic(format!("render-path-{i}")),
            name: "Path".into(),
            width_m: 32.0,
            shoulder_m: 1.0,
            profile: None,
            locked: false,
            points: vec![
                PathPoint {
                    x_m: -190.0,
                    z_m: 0.0,
                },
                PathPoint {
                    x_m: 190.0,
                    z_m: 0.0,
                },
            ],
        })
        .collect::<Vec<_>>();
    paths.sort_by_key(|path| path.id);
    paths
}

fn rivers(
    terrain: &forge_project::TerrainDocument,
) -> Vec<forge_project::zone_authoring::rivers::AuthoredRiver> {
    use forge_project::zone_authoring::rivers::AuthoredRiver;
    let mut water = forge_world::embedded_visual_valley_water_document().unwrap();
    water.project_id = terrain.project_id;
    water.area_id = terrain.area_id;
    water.river.algorithm = forge_project::RiverPathAlgorithm::PiecewiseLinearV1;
    water.river.elevation_policy = forge_project::RiverElevationPolicy::AuthoredSurfaceV1;
    water.river.control_points = [-190.0, -60.0, 60.0, 190.0]
        .map(|x| forge_project::RiverControlPoint {
            center_m: forge_core::Vec3::new(x, 0.0, 100.0),
            surface_width_m: 12.0,
            bed_depth_m: 8.0,
        })
        .to_vec();
    if let forge_project::WaterRefractionProfile::ScreenSpace { max_depth_m, .. } =
        &mut water.optics.refraction
    {
        *max_depth_m = 8.0;
    }
    vec![AuthoredRiver {
        id: forge_core::EntityId::deterministic("renderer-river"),
        water,
        maximum_cut_m: 8.0,
        locked: false,
    }]
}

#[test]
fn rivers_survive_quality_transitions_and_legacy_updates_remove_them() {
    let (mut app, terrain, source) = fixture();
    let rivers = rivers(&terrain);
    let paths = paths();
    let plan = forge_world::VegetationGenerationPlan::from_documents_with_waterways(
        &terrain, &source, &paths, &rivers,
    )
    .unwrap();
    let key = plan.source_hash().to_string();
    assert!(
        update_vegetation_sources_with_waterways(
            app.world_mut(),
            terrain.clone(),
            source.clone(),
            paths.clone(),
            rivers.clone()
        )
        .unwrap()
    );
    for tier in [
        VisualQualityTier::Low,
        VisualQualityTier::Medium,
        VisualQualityTier::High,
        VisualQualityTier::Ultra,
    ] {
        app.world_mut()
            .resource_mut::<VegetationQualityRequest>()
            .request(tier);
        app.update();
        let expected = prepare_vegetation_projection_with_waterways(
            &terrain,
            &source,
            &paths,
            &rivers,
            Some(app.world().resource::<VegetationQualityRequest>()),
        )
        .unwrap();
        let stats = app.world().resource::<VegetationProjectionStats>();
        assert_eq!(stats.vegetation_document_hash, key);
        assert_eq!(stats.placements, expected.stats.placements);
        assert_eq!(stats.quality.as_ref().unwrap().projected_tier, tier);
    }
    assert!(
        !update_vegetation_sources_with_waterways(
            app.world_mut(),
            terrain.clone(),
            source.clone(),
            paths.clone(),
            rivers.clone()
        )
        .unwrap()
    );
    let mut invalid = rivers;
    invalid[0].water.river.control_points[0].center_m.x = f32::NAN;
    assert!(
        update_vegetation_sources_with_waterways(
            app.world_mut(),
            terrain.clone(),
            source.clone(),
            paths.clone(),
            invalid
        )
        .is_err()
    );
    app.update();
    assert_eq!(
        app.world()
            .resource::<VegetationProjectionStats>()
            .vegetation_document_hash,
        key
    );
    assert!(
        update_vegetation_sources_with_paths(
            app.world_mut(),
            terrain.clone(),
            source.clone(),
            paths.clone()
        )
        .unwrap()
    );
    app.update();
    assert_eq!(
        app.world()
            .resource::<VegetationProjectionStats>()
            .vegetation_document_hash,
        forge_world::path_vegetation::prepare(&source, &paths)
            .unwrap()
            .source_hash()
            .to_string()
    );
}

#[test]
fn authoring_paths_survive_quality_changes_and_removal_restores_source_identity() {
    let (mut app, terrain, source) = fixture();
    let original = source.content_hash().unwrap().to_string();
    let mut paths = paths();
    let key = forge_world::path_vegetation::prepare(&source, &paths)
        .unwrap()
        .source_hash()
        .to_string();
    assert!(
        update_vegetation_sources_with_paths(
            app.world_mut(),
            terrain.clone(),
            source.clone(),
            paths.clone()
        )
        .unwrap()
    );
    assert_eq!(
        app.world()
            .resource::<VegetationProjectionStats>()
            .vegetation_document_hash,
        original
    );
    for tier in [
        VisualQualityTier::Low,
        VisualQualityTier::Medium,
        VisualQualityTier::High,
        VisualQualityTier::Ultra,
    ] {
        app.world_mut()
            .resource_mut::<VegetationQualityRequest>()
            .request(tier);
        app.update();
        let stats = app.world().resource::<VegetationProjectionStats>();
        assert_eq!(stats.vegetation_document_hash, key);
        assert_eq!(stats.quality.as_ref().unwrap().projected_tier, tier);
        let expected = prepare_vegetation_projection_with_paths(
            &terrain,
            &source,
            &paths,
            Some(app.world().resource::<VegetationQualityRequest>()),
        )
        .unwrap();
        assert_eq!(stats.placements, expected.stats.placements);
    }
    assert!(
        !update_vegetation_sources_with_paths(
            app.world_mut(),
            terrain.clone(),
            source.clone(),
            paths.clone()
        )
        .unwrap()
    );
    paths[0].points[0].x_m = f32::NAN;
    assert!(
        update_vegetation_sources_with_paths(
            app.world_mut(),
            terrain.clone(),
            source.clone(),
            paths
        )
        .is_err()
    );
    app.update();
    assert_eq!(
        app.world()
            .resource::<VegetationProjectionStats>()
            .vegetation_document_hash,
        key
    );
    assert!(update_vegetation_sources(app.world_mut(), terrain, source.clone()).unwrap());
    app.update();
    assert_eq!(
        app.world()
            .resource::<VegetationProjectionStats>()
            .vegetation_document_hash,
        original
    );
    assert_eq!(source.content_hash().unwrap().to_string(), original);
}
