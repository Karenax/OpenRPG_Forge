use super::*;
use forge_assets::source_geometry::{AssetLocalFrame, ResolvedWaterExclusion, SourceProxyTopology};
use forge_core::bytes_sha256;

fn frame() -> AssetLocalFrame {
    AssetLocalFrame {
        translation_m: [0.0; 3],
        rotation_xyzw: [0.0, 0.0, 0.0, 1.0],
        scale: [1.0; 3],
    }
}

fn profile(name: &str) -> profile::Profile {
    let positions = vec![
        [0.0, 0.0, 0.0],
        [1.0, 0.0, 0.0],
        [0.0, 1.0, 0.0],
        [0.0, 0.0, 1.0],
    ];
    let indices = vec![0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3];
    let fingerprint = Geometry::content_hash(&positions, &indices);
    profile::Profile {
        source_id: format!("{name}/boat"),
        canonical_translation_m: [3.0, 0.0, 7.0],
        canonical_heading_radians: 0.0,
        nodes: vec![profile::Node {
            id: "root".into(),
            parent: None,
            transform: frame(),
            visible: true,
        }],
        parts: vec![profile::Part {
            id: "part".into(),
            node: "root".into(),
            material: "wood".into(),
            attributes: BTreeMap::from([
                (
                    "position".into(),
                    vec![0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0, 0.0],
                ),
                ("normal".into(), [0.0, 0.0, 1.0].repeat(3)),
                ("uv".into(), vec![0.0; 6]),
                ("color".into(), vec![1.0; 9]),
                ("aux".into(), vec![0.0; 12]),
            ]),
            indices: vec![0, 1, 2],
            cast_shadow: true,
            receive_shadow: true,
            render_order: 0,
        }],
        colliders: vec![profile::CollisionVolume {
            id: "deck".into(),
            tag: "deck".into(),
            center_m: [0.0; 3],
            half_extents_m: [1.0, 0.1, 1.0],
            walkable: true,
            solid: false,
        }],
        water_exclusion: ResolvedWaterExclusion {
            id: "hull".into(),
            parent_node: "root".into(),
            local_frame: frame(),
            positions,
            indices,
            fingerprint,
            topology: SourceProxyTopology {
                kind: "closed".into(),
                boundary_edges: 0,
                welded_vertices: 4,
                edges: 6,
                nonmanifold_edges: 0,
                inconsistent_edges: 0,
                degenerate_triangles: 0,
                repairs_applied: false,
            },
            source_boundary_edges: None,
        },
        triangles: 1,
    }
}

#[test]
fn independent_profiles_share_projection_and_keep_separate_ownership() {
    let mut world = World::new();
    world.init_resource::<Assets<Mesh>>();
    world.init_resource::<Assets<StandardMaterial>>();
    let wood = world
        .resource_mut::<Assets<StandardMaterial>>()
        .add(StandardMaterial::default());
    let materials = BTreeMap::from([("wood".into(), wood.clone())]);
    let first_id = AssetId::deterministic("independent/a");
    let second_id = AssetId::deterministic("independent/b");
    let first = PreparedBoat::from_profile(profile("module-a"), first_id, bytes_sha256(b"a"))
        .unwrap()
        .stage(&mut world, &materials)
        .unwrap();
    let second = PreparedBoat::from_profile(profile("module-b"), second_id, bytes_sha256(b"b"))
        .unwrap()
        .stage(&mut world, &materials)
        .unwrap();
    assert_eq!(first.census(&world).unwrap().triangles, 1);
    assert_eq!(second.census(&world).unwrap().water_exclusions, 1);
    assert_ne!(first_id, second_id);
    first.retire(&mut world);
    assert_eq!(second.census(&world).unwrap().collision_volumes, 1);
    assert!(
        world
            .resource::<Assets<StandardMaterial>>()
            .contains(wood.id())
    );
    let mut collisions = world.query::<(&BoatCollisionVolume, Option<&Sensor>)>();
    let live = collisions.iter(&world).collect::<Vec<_>>();
    assert_eq!(live.len(), 1);
    assert!(live[0].0.walkable && !live[0].0.solid && live[0].1.is_none());
    second.retire(&mut world);
}

#[test]
fn invalid_profile_data_is_refused_before_scene_allocation() {
    let asset = AssetId::deterministic("independent/boat");
    let revision = bytes_sha256(b"revision");
    let mut cyclic = profile("cycle");
    cyclic.nodes.push(profile::Node {
        id: "loop".into(),
        parent: Some("loop".into()),
        transform: frame(),
        visible: true,
    });
    assert!(PreparedBoat::from_profile(cyclic, asset, revision).is_err());
    let mut missing_vertex = profile("vertex");
    missing_vertex.parts[0].indices[0] = 4;
    assert!(PreparedBoat::from_profile(missing_vertex, asset, revision).is_err());
    let mut invalid_hull = profile("hull");
    invalid_hull.water_exclusion.fingerprint = bytes_sha256(b"different");
    assert!(PreparedBoat::from_profile(invalid_hull, asset, revision).is_err());
}
