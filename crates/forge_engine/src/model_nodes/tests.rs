use super::*;
use crate::pendulum_motion::{
    LanternMotion, Wind,
    host::{
        Motion, Resident,
        nodes::{NodeMotion, bindings},
    },
};
use bevy::{
    app::TaskPoolPlugin,
    asset::{
        AssetApp, AssetPlugin, AssetServer, LoadState,
        io::{
            AssetSourceBuilder, AssetSourceId,
            memory::{Dir, MemoryAssetReader},
        },
    },
    camera::visibility::{InheritedVisibility, ViewVisibility},
    gltf::{GltfAssetLabel, GltfPlugin},
    mesh::MeshPlugin,
    transform::TransformPlugin,
    world_serialization::{
        WorldAsset, WorldAssetRoot, WorldInstance, WorldInstanceSpawner, WorldSerializationPlugin,
    },
};
use forge_core::{AssetId, EntityId};
use forge_project::zone_authoring::placements::ManualPlacement;
use std::{path::Path, time::Duration};

fn fixture_glb() -> Vec<u8> {
    let mut json = br#"{"asset":{"version":"2.0"},"nodes":[{"name":"Repeated","children":[1]},{"name":"Repeated","translation":[1,2,3],"scale":[2,-0.5,1.25]},{"name":"Repeated"}],"scene":0,"scenes":[{"nodes":[0,2]}]}"#.to_vec();
    json.resize(json.len().next_multiple_of(4), b' ');
    let total = u32::try_from(20 + json.len()).unwrap();
    let mut bytes = b"glTF".to_vec();
    bytes.extend(2_u32.to_le_bytes());
    bytes.extend(total.to_le_bytes());
    bytes.extend(u32::try_from(json.len()).unwrap().to_le_bytes());
    bytes.extend(b"JSON");
    bytes.extend(json);
    bytes
}

fn fixture_app(before_gltf: bool) -> App {
    let dir = Dir::default();
    dir.insert_asset(Path::new("node-identity.glb"), fixture_glb());
    dir.insert_asset(Path::new("ordinary.glb"), fixture_glb());
    let reader = MemoryAssetReader { root: dir };
    let preview_reader = reader.clone();
    let mut app = App::new();
    app.register_asset_source(
        AssetSourceId::Default,
        AssetSourceBuilder::new(move || Box::new(reader.clone())),
    )
    .register_asset_source(
        "authoring-preview",
        AssetSourceBuilder::new(move || Box::new(preview_reader.clone())),
    )
    .add_plugins((
        TaskPoolPlugin::default(),
        AssetPlugin::default(),
        WorldSerializationPlugin,
        MeshPlugin,
        TransformPlugin,
    ));
    if before_gltf {
        app.add_plugins(ModelNodeIdentityPlugin);
    }
    app.add_plugins(GltfPlugin::default());
    if !before_gltf {
        app.add_plugins(ModelNodeIdentityPlugin);
    }
    app.register_type::<Name>()
        .register_type::<Transform>()
        .register_type::<GlobalTransform>()
        .register_type::<Visibility>()
        .register_type::<InheritedVisibility>()
        .register_type::<ViewVisibility>()
        .register_type::<ChildOf>()
        .register_type::<Children>();
    app.finish();
    app.cleanup();
    app
}

fn run_until(app: &mut App, mut ready: impl FnMut(&mut World) -> bool) {
    let deadline = std::time::Instant::now() + Duration::from_secs(10);
    while !ready(app.world_mut()) {
        assert!(
            std::time::Instant::now() < deadline,
            "scene did not become ready"
        );
        app.update();
        std::thread::yield_now();
    }
}

fn placement(name: &str) -> ManualPlacement {
    ManualPlacement {
        id: EntityId::deterministic(name),
        name: name.into(),
        asset_id: AssetId::deterministic("shared-model"),
        asset_hash: forge_core::bytes_sha256(&fixture_glb()),
        physics: None,
        particles: None,
        local_lights: None,
        transform: forge_core::Transform::IDENTITY,
        locked: false,
    }
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "One scenario checks both loader orders and independent scene instances."
)]
fn ordinary_glb_nodes_keep_identity_and_rest_frame_in_each_scene_instance() {
    for before_gltf in [true, false] {
        let mut app = fixture_app(before_gltf);
        let (tracked_path, ordinary_path) = if before_gltf {
            ("node-identity.glb", "ordinary.glb")
        } else {
            ("authoring-preview://node-identity.glb", "node-identity.glb")
        };
        let tracking = app.world().resource::<ModelNodeIdentityTracking>();
        assert!(
            tracking
                .track_before_load(tracked_path.into(), &[])
                .is_err()
        );
        assert!(
            tracking
                .track_before_load(tracked_path.into(), &[1, 1])
                .is_err()
        );
        assert!(
            tracking
                .track_before_load(tracked_path.into(), &[1])
                .unwrap()
        );
        assert!(
            !tracking
                .track_before_load(tracked_path.into(), &[1])
                .unwrap()
        );
        let server = app.world().resource::<AssetServer>().clone();
        let scene: Handle<WorldAsset> =
            server.load(GltfAssetLabel::Scene(0).from_asset(tracked_path));
        let ordinary: Handle<WorldAsset> =
            server.load(GltfAssetLabel::Scene(0).from_asset(ordinary_path));
        run_until(&mut app, |_| {
            [scene.id(), ordinary.id()]
                .into_iter()
                .all(|id| match server.get_load_state(id) {
                    Some(LoadState::Loaded) => true,
                    Some(LoadState::Failed(error)) => panic!("GLB load failed: {error}"),
                    _ => false,
                })
        });
        let first = placement("model-instance-one");
        let second = placement("model-instance-two");
        let root_a = app
            .world_mut()
            .spawn((
                WorldAssetRoot(scene.clone()),
                crate::ManualPlacementProjection(first.clone()),
                crate::domain_transform_to_bevy(first.transform),
            ))
            .id();
        let root_b = app
            .world_mut()
            .spawn((
                WorldAssetRoot(scene.clone()),
                crate::ManualPlacementProjection(second.clone()),
                crate::domain_transform_to_bevy(second.transform),
            ))
            .id();
        let root_ordinary = app.world_mut().spawn(WorldAssetRoot(ordinary)).id();
        run_until(&mut app, |world| {
            let spawner = world.resource::<WorldInstanceSpawner>();
            [root_a, root_b, root_ordinary].into_iter().all(|root| {
                world
                    .get::<WorldInstance>(root)
                    .is_some_and(|instance| spawner.instance_is_ready(**instance))
            })
        });
        let motion = [NodeMotion {
            node_index: 1,
            motion: Motion::Suspended(LanternMotion {
                t: 0.5,
                ph: 0.2,
                x: 0.0,
                z: 0.0,
                vx: 0.0,
                vz: 0.0,
            }),
        }];
        let a = bindings(app.world(), root_a, &motion).unwrap();
        let b = bindings(app.world(), root_b, &motion).unwrap();
        assert!(bindings(app.world(), root_ordinary, &motion).is_err());
        let tracked_count = {
            let world = app.world_mut();
            let mut query = world.query::<&ModelNodeIdentity>();
            query.iter(world).count()
        };
        assert_eq!(tracked_count, 2);
        assert_ne!(a[0].entity, b[0].entity);
        assert_eq!(a[0].rest_scale, Vec3::new(2.0, -0.5, 1.25));
        assert_eq!(a[0].rest_scale, b[0].rest_scale);
        assert_eq!(
            app.world().get::<Name>(a[0].entity).unwrap().as_str(),
            "Repeated"
        );
        let original_rest = app
            .world()
            .get::<ModelNodeIdentity>(a[0].entity)
            .unwrap()
            .rest();
        let untouched_b = *app.world().get::<Transform>(b[0].entity).unwrap();
        let entity_b = b[0].entity;
        let mut first_host = Resident::bind(app.world_mut(), &first, root_a, a).unwrap();
        let mut second_host = Resident::bind(app.world_mut(), &second, root_b, b).unwrap();
        let wind = Wind {
            speed: 7.0,
            direction: [0.6, 0.8],
        };
        first_host.advance(app.world_mut(), 0.05, wind).unwrap();
        let rebound = bindings(app.world(), root_a, &motion).unwrap();
        assert_eq!(rebound[0].rest_rotation, original_rest.rotation);
        assert_eq!(rebound[0].rest_scale, original_rest.scale);
        assert_eq!(
            app.world()
                .get::<Transform>(rebound[0].entity)
                .unwrap()
                .scale,
            original_rest.scale
        );
        assert_eq!(
            *app.world().get::<Transform>(entity_b).unwrap(),
            untouched_b
        );
        first_host
            .rebind(app.world_mut(), &first, root_a, rebound)
            .unwrap();
        second_host.advance(app.world_mut(), 0.05, wind).unwrap();
        let a = bindings(app.world(), root_a, &motion).unwrap();
        let b = bindings(app.world(), root_b, &motion).unwrap();
        assert_eq!(
            app.world().get::<Transform>(a[0].entity).unwrap().rotation,
            app.world().get::<Transform>(b[0].entity).unwrap().rotation
        );
        first_host.release(app.world_mut());
        second_host.release(app.world_mut());
    }
}
