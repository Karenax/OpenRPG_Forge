use forge_navigation::{
    AgentProfile, ForgeCoordinates, TileInput, TileKey, generate_tile, point, source_hash,
};
use landmass::{
    Archipelago, ArchipelagoOptions, FromAgentRadius, Island, PermittedAnimationLinks,
    PointSampleDistance3d,
};
use std::{collections::HashMap, sync::Arc};

fn floor(profile: AgentProfile, key: TileKey) -> TileInput {
    let (min, max) = key.bounds(profile);
    let pad = profile.border_m() + 1.0;
    let mut input = TileInput {
        key,
        profile,
        revision: 7,
        token: 17,
        source_hash: String::new(),
        vertices: vec![
            [min[0] - pad, 0.0, min[1] - pad],
            [min[0] - pad, 0.0, max[1] + pad],
            [max[0] + pad, 0.0, max[1] + pad],
            [max[0] + pad, 0.0, min[1] - pad],
        ],
        triangles: vec![[0, 1, 2], [0, 2, 3]],
        areas: vec![],
        policy_source_hash: None,
        curved_obstacles: vec![],
    };
    input.source_hash = source_hash(&input).unwrap();
    input
}

#[test]
fn adjacent_raw_tiles_validate_connect_and_preserve_generation_identity() {
    let profile = AgentProfile::default();
    let a = generate_tile(floor(profile, TileKey { x: 0, z: 0 })).unwrap();
    let b = generate_tile(floor(profile, TileKey { x: 1, z: 0 })).unwrap();
    assert!((a.profile.height_m - 1.90).abs() < f32::EPSILON);
    assert_eq!(a.revision, 7);
    assert_eq!(a.token, 17);
    let replay = generate_tile(floor(profile, TileKey { x: 0, z: 0 })).unwrap();
    assert_eq!(
        serde_json::to_vec(&a).unwrap(),
        serde_json::to_vec(&replay).unwrap()
    );
    let mut options = ArchipelagoOptions::<ForgeCoordinates>::from_agent_radius(profile.radius_m);
    options.point_sample_distance = PointSampleDistance3d {
        horizontal_distance: 0.35,
        distance_above: 0.5,
        distance_below: 0.5,
        vertical_preference_ratio: 2.0,
        animation_link_max_vertical_distance: 0.4,
    };
    let mut world = Archipelago::new(options);
    world.add_island(Island::new(
        landmass::Transform::default(),
        Arc::new(a.validate().unwrap()),
    ));
    world.add_island(Island::new(
        landmass::Transform::default(),
        Arc::new(b.validate().unwrap()),
    ));
    world.update(1.0 / 60.0);
    let start = world
        .sample_point(
            point([2.0, 0.01, 10.0]),
            &world.archipelago_options.point_sample_distance,
        )
        .unwrap();
    let end = world
        .sample_point(
            point([60.0, 0.01, 10.0]),
            &world.archipelago_options.point_sample_distance,
        )
        .unwrap();
    let path = world
        .find_path(
            &start,
            &end,
            &HashMap::default(),
            PermittedAnimationLinks::default(),
        )
        .unwrap();
    assert!(!path.is_empty());
}

#[test]
fn forged_geometry_hash_and_nonfinite_profile_fail_before_generation() {
    let mut input = floor(AgentProfile::default(), TileKey { x: 0, z: 0 });
    input.vertices[0][1] += 0.1;
    assert!(generate_tile(input).unwrap_err().contains("hash mismatch"));
    let profile = AgentProfile {
        height_m: f32::NAN,
        ..AgentProfile::default()
    };
    assert!(profile.validate().is_err());
}

#[test]
fn canonical_policy_and_prepared_curved_identity_are_hashed_and_validated() {
    use forge_navigation::{CONTOUR_ERROR_CELLS, CurvedObstacleProjection};
    let profile = AgentProfile::default();
    let mut input = floor(profile, TileKey { x: 0, z: 0 });
    let empty_hash = input.source_hash.clone();
    input.policy_source_hash = Some("ab".repeat(32));
    input.source_hash = source_hash(&input).unwrap();
    assert_ne!(input.source_hash, empty_hash);
    input.curved_obstacles.push(CurvedObstacleProjection {
        algorithm: "rapier-support-planes-v1".into(),
        kind: "ball".into(),
        parameters: vec![0.5],
        position: [10.0, 1.0, 10.0],
        rotation: [0.0, 0.0, 0.0, 1.0],
        planes: 32,
        clearance_m: profile.radius_m
            + profile.radius_m * 0.5 * (std::f32::consts::FRAC_1_SQRT_2 + CONTOUR_ERROR_CELLS)
            + 0.01
            + 0.02,
        footprint_excess_bound_m: 0.1,
    });
    let before = source_hash(&input).unwrap();
    input.curved_obstacles[0].position[0] += 0.5;
    assert_ne!(source_hash(&input).unwrap(), before);
    input.source_hash = source_hash(&input).unwrap();
    let encoded = serde_json::to_vec(&input).unwrap();
    let decoded: TileInput = serde_json::from_slice(&encoded).unwrap();
    assert_eq!(
        source_hash(&decoded).unwrap(),
        input.source_hash,
        "worker float roundtrip must preserve portable identity"
    );
    input.curved_obstacles[0].validate(profile).unwrap();
    input.curved_obstacles[0].planes = 0;
    assert!(input.curved_obstacles[0].validate(profile).is_err());
    input.source_hash = source_hash(&input).unwrap();
    assert!(generate_tile(input).is_err());
    let mut invalid = floor(profile, TileKey { x: 0, z: 0 });
    invalid.policy_source_hash = Some("unattested".into());
    invalid.source_hash = source_hash(&invalid).unwrap();
    assert!(generate_tile(invalid).is_err());
}

#[test]
fn cancelled_generation_never_publishes_a_tile() {
    let input = floor(AgentProfile::default(), TileKey { x: 0, z: 0 });
    assert!(
        forge_navigation::generate_tile_cancellable(input, || true)
            .unwrap_err()
            .contains("cancelled")
    );
}

#[test]
fn raw_invalid_height_triangle_is_rejected_without_winding_repair() {
    let mut tile = generate_tile(floor(AgentProfile::default(), TileKey { x: 0, z: 0 })).unwrap();
    assert!(!tile.detail.triangles.is_empty());
    tile.detail.triangles[0].swap(1, 2);
    assert!(tile.validate().is_err());
}

#[test]
fn malformed_output_counts_and_nonfinite_coordinates_reject_before_raw_conversion() {
    let original = generate_tile(floor(AgentProfile::default(), TileKey { x: 0, z: 0 })).unwrap();
    let mut zero = original.clone();
    zero.polygon.max_vertices_per_polygon = 0;
    assert!(zero.validate().is_err());
    let mut truncated = original.clone();
    truncated.polygon.polygons.pop();
    assert!(truncated.validate().is_err());
    let mut nan = original.clone();
    nan.detail.vertices[0].x = f32::NAN;
    assert!(nan.validate().is_err());
    let mut range = original;
    range.detail.meshes[0].base_vertex_index = u32::MAX;
    assert!(range.validate().is_err());
}

#[test]
fn canonical_areas_reject_concavity_self_intersection_and_invalid_height_atomically() {
    use forge_navigation::AreaVolume;
    let valid = AreaVolume {
        vertices_xz: vec![[0.0, 0.0], [4.0, 0.0], [4.0, 4.0], [0.0, 4.0]],
        min_y: 0.0,
        max_y: 1.0,
        area: 0,
    };
    assert!(valid.validate().is_ok());
    let mut concave = valid.clone();
    concave.vertices_xz.insert(2, [1.0, 1.0]);
    assert!(concave.validate().is_err());
    let mut crossing = valid.clone();
    crossing.vertices_xz.swap(1, 2);
    assert!(crossing.validate().is_err());
    let mut height = valid;
    height.min_y = f32::NAN;
    assert!(height.validate().is_err());
}
