use super::*;
use crate::source_fish::host::scene::tests::{placement, policy, profile};
use bevy::render::storage::ShaderBuffer;
use forge_core::{AssetId, bytes_sha256};

#[test]
fn live_edits_reuse_meshes_reject_stale_heads_and_retire_their_scene() {
    let mut world = World::new();
    world.init_resource::<Assets<Shader>>();
    world.init_resource::<Assets<ShaderBuffer>>();
    world.init_resource::<Assets<Mesh>>();
    world.init_resource::<Assets<Image>>();
    world.init_resource::<Assets<FishMaterial>>();
    world.init_resource::<Status>();
    let asset = AssetId::deterministic("independent/live-fish");
    let pin = bytes_sha256(b"independent profile");
    let initial = vec![placement(asset, pin, "independent/live-placement")];
    let resident = PreparedScene::new(
        vec![profile(asset, pin, "verified shader")],
        initial.clone(),
        policy(),
    )
    .unwrap()
    .stage(&mut world, &crate::source_surface::tests::lighting(), 0.0)
    .unwrap();
    let root = resident.resident().authored_roots()[0].0;
    let parts = resident.resident().authored_parts(initial[0].id).unwrap();
    let water = scene::absent_water(&mut world).unwrap();
    world.insert_resource(State {
        scene: Some(resident),
        placements: initial.clone(),
        water_mode: WaterMode::Absent,
        water,
        shadow: MaskedFishCache::default(),
    });
    assert!(parts.iter().all(|part| owns(&world, *part)));
    assert!(!owns(&world, root));
    let original_mesh = world.get::<Mesh3d>(parts[0]).unwrap().clone();
    world.entity_mut(parts[0]).insert(Mesh3d(Handle::default()));
    assert!(!owns(&world, parts[0]));
    world.entity_mut(parts[0]).insert(original_mesh);
    assert!(owns(&world, parts[0]));
    let mut moved = initial.clone();
    moved[0].transform.translation_m.x = 4.0;
    replace(&mut world, &initial, &moved, None).unwrap();
    assert_eq!(world.resource::<Assets<Mesh>>().len(), 3);
    assert_eq!(
        world
            .get::<crate::ManualPlacementProjection>(root)
            .unwrap()
            .0,
        moved[0]
    );
    assert!(replace(&mut world, &initial, &[], None).is_err());
    assert!(world.get_entity(root).is_ok());
    replace(&mut world, &moved, &[], None).unwrap();
    assert!(world.get_entity(root).is_err());
    assert!(parts.iter().all(|part| !owns(&world, *part)));
    assert!(world.resource::<Assets<Mesh>>().is_empty());
    assert!(world.resource::<Assets<FishMaterial>>().is_empty());
}
