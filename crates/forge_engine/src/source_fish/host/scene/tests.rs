use super::*;
use crate::source_fish::{material::SurfaceParameters, prepare::FishPropTemplate};
use forge_core::{EntityId, Transform as ForgeTransform, bytes_sha256};

pub(in crate::source_fish::host) fn profile(
    asset: AssetId,
    hash: forge_core::ContentHash,
    shader: &str,
) -> PreparedProfile {
    PreparedProfile {
        package_sha256: hash,
        templates: vec![FishPropTemplate {
            source_id: format!("independent/{asset}"),
            prototype: "fish".into(),
            asset_id: asset,
            transform: ForgeTransform::IDENTITY,
            surface: SurfaceParameters {
                pattern_and_seed: 0.25,
                source_length_m: 1.0,
                flags: [0.0; 4],
            },
            lod_meshes: (0..3)
                .map(|_| {
                    Mesh::new(
                        bevy::render::render_resource::PrimitiveTopology::TriangleList,
                        bevy::asset::RenderAssetUsages::default(),
                    )
                })
                .collect(),
        }],
        shader: shader.into(),
        shadow_shader: Arc::new("shared shadow".into()),
        skin: ShaderBuffer::from(vec![Vec4::ONE; 168]),
    }
}

pub(in crate::source_fish::host) fn placement(
    asset: AssetId,
    hash: forge_core::ContentHash,
    name: &str,
) -> ManualPlacement {
    ManualPlacement {
        id: EntityId::deterministic(name),
        name: name.into(),
        asset_id: asset,
        asset_hash: hash,
        transform: ForgeTransform::IDENTITY,
        locked: false,
        particles: None,
        physics: None,
        local_lights: None,
    }
}

pub(in crate::source_fish::host) fn policy() -> Policy {
    Policy::new(40.0, 36.0, 10.0, 12.0, 0.8).unwrap()
}

#[test]
fn independent_profiles_stage_together_and_release_only_their_own_resources() {
    let mut world = World::new();
    world.init_resource::<Assets<Shader>>();
    world.init_resource::<Assets<ShaderBuffer>>();
    world.init_resource::<Assets<Mesh>>();
    world.init_resource::<Assets<material::FishMaterial>>();
    let lighting = crate::source_surface::tests::lighting();
    let external_skin = world
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(ShaderBuffer::from(vec![Vec4::ZERO]));
    let external = world.resource_mut::<Assets<material::FishMaterial>>().add(
        material::prepare_scene(
            SurfaceParameters {
                pattern_and_seed: 0.25,
                source_length_m: 1.0,
                flags: [0.0; 4],
            },
            external_skin.clone(),
            &lighting,
            0.0,
        )
        .unwrap(),
    );
    let (a, b) = (AssetId::deterministic("a"), AssetId::deterministic("b"));
    let (ha, hb) = (bytes_sha256(b"a"), bytes_sha256(b"b"));
    let mut placements = vec![placement(a, ha, "a"), placement(b, hb, "b")];
    placements.sort_by_key(|item| item.id);
    let mut scene = PreparedScene::new(
        vec![profile(a, ha, "same shader"), profile(b, hb, "same shader")],
        placements.clone(),
        policy(),
    )
    .unwrap()
    .stage(&mut world, &lighting, 0.0)
    .unwrap();
    assert_eq!(scene.resident().authored_roots().len(), 2);
    assert_eq!(scene.shadow_shader().as_str(), "shared shadow");
    assert_eq!(world.resource::<Assets<Mesh>>().len(), 6);
    let roots = scene.resident().authored_roots().to_vec();
    let mut moved = placements.clone();
    moved[0].transform.translation_m.x = 5.0;
    let mut invalid = moved.clone();
    invalid[1].asset_hash = bytes_sha256(b"altered");
    assert!(
        scene
            .resident_mut()
            .move_authored(&mut world, &invalid)
            .is_err()
    );
    assert_eq!(scene.resident().authored_roots(), roots);
    let mut with_effect = moved.clone();
    with_effect[0].particles = Some(Box::new(
        forge_project::zone_authoring::placements::ParticlePackageReference {
            schema_version: 1,
            carrier_sha256: with_effect[0].asset_hash,
            manifest_sha256: bytes_sha256(b"effect manifest"),
            package_sha256: bytes_sha256(b"effect package"),
        },
    ));
    assert!(
        scene
            .resident_mut()
            .move_authored(&mut world, &with_effect)
            .is_err()
    );
    assert_eq!(scene.resident().authored_roots(), roots);
    scene
        .resident_mut()
        .move_authored(&mut world, &moved)
        .unwrap();
    assert_eq!(scene.resident().authored_roots()[0].0, roots[0].0);
    assert_eq!(
        scene.resident().authored_roots()[0].2,
        moved
            .iter()
            .find(|item| item.id == roots[0].1)
            .unwrap()
            .transform
    );
    assert_eq!(world.resource::<Assets<Mesh>>().len(), 6);
    scene.clear(&mut world);
    assert!(world.resource::<Assets<Mesh>>().is_empty());
    let materials = world.resource::<Assets<material::FishMaterial>>();
    assert_eq!(materials.len(), 1);
    assert!(materials.contains(external.id()));
    assert!(
        world
            .resource::<Assets<ShaderBuffer>>()
            .contains(external_skin.id())
    );
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "Exercise independent draw and shadow ranges, group sleep and wake"
)]
fn different_profile_policies_share_a_group_without_losing_their_ranges() {
    let mut app = App::new();
    app.add_plugins((MinimalPlugins, bevy::transform::TransformPlugin));
    app.world_mut().init_resource::<Assets<Shader>>();
    app.world_mut().init_resource::<Assets<ShaderBuffer>>();
    app.world_mut().init_resource::<Assets<Mesh>>();
    app.world_mut()
        .init_resource::<Assets<material::FishMaterial>>();
    let lighting = crate::source_surface::tests::lighting();
    let (near, far) = (
        AssetId::deterministic("near"),
        AssetId::deterministic("far"),
    );
    let (near_hash, far_hash) = (bytes_sha256(b"near"), bytes_sha256(b"far"));
    let near_placement = placement(near, near_hash, "near placement");
    let far_placement = placement(far, far_hash, "far placement");
    let mut placements = vec![near_placement.clone(), far_placement.clone()];
    placements.sort_by_key(|item| item.id);
    let near_policy = Policy::new(40.0, 36.0, 35.0, 12.0, 0.8).unwrap();
    let far_policy = Policy::new(100.0, 90.0, 5.0, 12.0, 0.8).unwrap();
    let mut scene = PreparedScene::with_profile_policies(
        vec![
            (profile(near, near_hash, "same"), near_policy),
            (profile(far, far_hash, "same"), far_policy),
        ],
        placements,
    )
    .unwrap()
    .stage(app.world_mut(), &lighting, 0.0)
    .unwrap();
    app.update();
    let root = |scene: &Scene, id| {
        scene
            .resident()
            .authored_roots()
            .iter()
            .find(|(_, placed, _)| *placed == id)
            .unwrap()
            .0
    };
    let near_root = root(&scene, near_placement.id);
    let far_root = root(&scene, far_placement.id);

    // Only the near profile casts offscreen at 30 m; the wide draw range from
    // the other profile must not hide that distinct shadow range.
    scene
        .resident_mut()
        .update_view(app.world_mut(), Vec3::X * 30.0, |_, _| false, 1.0, 1)
        .unwrap();
    assert!(!scene.resident().is_dormant());
    let inventory = scene.resident().shadow_inventory(app.world()).unwrap();
    assert!(
        inventory
            .iter()
            .find(|(item, _)| item.root == near_root)
            .unwrap()
            .1
    );
    assert!(
        !inventory
            .iter()
            .find(|(item, _)| item.root == far_root)
            .unwrap()
            .1
    );

    scene
        .resident_mut()
        .update_view(app.world_mut(), Vec3::X * 50.0, |_, _| true, 2.0, 2)
        .unwrap();
    for part in scene.resident().authored_parts(near_placement.id).unwrap() {
        assert_eq!(
            app.world().get::<Visibility>(part),
            Some(&Visibility::Hidden)
        );
    }
    let far_parts = scene.resident().authored_parts(far_placement.id).unwrap();
    assert!(
        far_parts
            .iter()
            .any(|part| app.world().get::<Visibility>(*part) == Some(&Visibility::Inherited))
    );
    scene
        .resident_mut()
        .update_view(app.world_mut(), Vec3::X * 150.0, |_, _| true, 3.0, 3)
        .unwrap();
    assert!(scene.resident().is_dormant());
    scene
        .resident_mut()
        .update_view(app.world_mut(), Vec3::X * 30.0, |_, _| false, 4.0, 4)
        .unwrap();
    assert!(!scene.resident().is_dormant());
    assert!(
        scene
            .resident()
            .shadow_inventory(app.world())
            .unwrap()
            .iter()
            .find(|(item, _)| item.root == near_root)
            .unwrap()
            .1
    );
    scene.clear(app.world_mut());
    assert!(app.world().resource::<Assets<Mesh>>().is_empty());
    assert!(
        app.world()
            .resource::<Assets<material::FishMaterial>>()
            .is_empty()
    );
}

#[test]
fn incompatible_profiles_and_wrong_pins_are_refused_before_publication() {
    let (a, b) = (AssetId::deterministic("a"), AssetId::deterministic("b"));
    let (ha, hb) = (bytes_sha256(b"a"), bytes_sha256(b"b"));
    let mut placements = vec![placement(a, ha, "a"), placement(b, hb, "b")];
    placements.sort_by_key(|item| item.id);
    let mut with_physics = placements.clone();
    with_physics[0].physics = Some(Box::new(
        forge_project::zone_authoring::placements::PhysicsPackageReference {
            schema_version: 1,
            carrier_sha256: with_physics[0].asset_hash,
            manifest_sha256: bytes_sha256(b"physics manifest"),
            package_sha256: bytes_sha256(b"physics package"),
        },
    ));
    assert!(
        PreparedScene::new(
            vec![profile(a, ha, "same"), profile(b, hb, "same")],
            with_physics,
            policy(),
        )
        .is_err()
    );
    assert!(
        PreparedScene::new(
            vec![profile(a, ha, "one"), profile(b, hb, "two")],
            placements.clone(),
            policy(),
        )
        .is_err()
    );
    placements[0].asset_hash = bytes_sha256(b"wrong pin");
    assert!(
        PreparedScene::new(
            vec![profile(a, ha, "same"), profile(b, hb, "same")],
            placements,
            policy(),
        )
        .is_err()
    );
}
