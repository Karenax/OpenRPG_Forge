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

fn floor_with_remote_lower_surface(profile: AgentProfile, key: TileKey, minimum: f32) -> TileInput {
    let mut input = floor(profile, key);
    for v in &mut input.vertices {
        v[1] = 6.0;
    }
    let corner = input.vertices[0];
    input.vertices.extend([
        [corner[0], minimum, corner[2]],
        [corner[0], minimum + 0.2, corner[2]],
        [corner[0], minimum, corner[2] + 0.2],
    ]);
    input.triangles.push([4, 5, 6]);
    input.source_hash = source_hash(&input).unwrap();
    input
}
#[test]
fn remote_lower_geometry_does_not_disconnect_adjacent_walkable_floors() {
    let profile = AgentProfile::default();
    let inputs = [
        floor_with_remote_lower_surface(profile, TileKey { x: 0, z: 0 }, 2.993_657_7),
        floor_with_remote_lower_surface(profile, TileKey { x: 1, z: 0 }, 4.599_487_4),
    ];
    let mut options = ArchipelagoOptions::<ForgeCoordinates>::from_agent_radius(profile.radius_m);
    options.point_sample_distance = PointSampleDistance3d {
        horizontal_distance: 0.35,
        distance_above: 0.5,
        distance_below: 0.5,
        vertical_preference_ratio: 2.0,
        animation_link_max_vertical_distance: 0.4,
    };
    let mut world = Archipelago::new(options);
    for input in inputs {
        let tile = generate_tile(input).unwrap();
        world.add_island(Island::new(
            landmass::Transform::default(),
            Arc::new(tile.validate().unwrap()),
        ));
    }
    world.update(1.0 / 60.0);
    let start = world
        .sample_point(
            point([2.0, 6.01, 10.0]),
            &world.archipelago_options.point_sample_distance,
        )
        .unwrap();
    let end = world
        .sample_point(
            point([60.0, 6.01, 10.0]),
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
        .expect("Remote lower geometry must not disconnect unchanged adjacent walkable floors");
    assert!(!path.is_empty());
}
