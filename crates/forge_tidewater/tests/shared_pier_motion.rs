//! Replay the pinned pier inputs through the same host used by independent scenes.
#![cfg(feature = "physics-rapier")]

use bevy::prelude::*;
use forge_core::EntityId;
use forge_engine::{
    pendulum_motion::{
        LanternMotion, SignMotion, Wind,
        host::collection::{Collection, Desired},
    },
    source_surface::SourceSurfaceMaterial,
};
use forge_project::zone_authoring::placements::ManualPlacement;
use forge_tidewater::{
    tidewater_pier::{PierLanternPivot, PierSwingPivot, PreparedPier, ResidentPier},
    tidewater_pier_animation::ResidentPierMotion,
};
use forge_tidewater_assets::{tidewater_pier, tidewater_pier_motion::PierMotionPackage};
use std::{collections::BTreeMap, path::PathBuf};

fn root(name: &str) -> PathBuf {
    let path = PathBuf::from(std::env::var_os(name).expect("qualification path"));
    assert!(path.is_absolute());
    path
}

fn files(root: &std::path::Path, name: &str, maximum: usize) -> Result<Vec<u8>, String> {
    let bytes = std::fs::read(root.join(name)).map_err(|error| error.to_string())?;
    if bytes.len() > maximum {
        return Err("Pinned fixture exceeds its declared size".into());
    }
    Ok(bytes)
}

fn materials(
    value: &serde_json::Value,
    result: &mut BTreeMap<String, Handle<SourceSurfaceMaterial>>,
) {
    match value {
        serde_json::Value::Object(object) => {
            for (key, value) in object {
                if key == "material"
                    && let Some(name) = value.as_str()
                {
                    result.insert(name.into(), Handle::default());
                }
                materials(value, result);
            }
        }
        serde_json::Value::Array(array) => {
            for value in array {
                materials(value, result);
            }
        }
        _ => {}
    }
}

fn pivots(world: &mut World, root: Entity) -> (Vec<Entity>, Entity) {
    let mut lanterns: Vec<_> = world
        .query::<(Entity, &PierLanternPivot, &ChildOf)>()
        .iter(world)
        .filter(|(_, _, parent)| parent.parent() == root)
        .map(|(entity, ordinal, _)| (ordinal.0, entity))
        .collect();
    lanterns.sort_by_key(|(ordinal, _)| *ordinal);
    assert!(
        lanterns
            .iter()
            .enumerate()
            .all(|(index, (ordinal, _))| index == *ordinal)
    );
    let signs: Vec<_> = world
        .query::<(Entity, &PierSwingPivot, &ChildOf)>()
        .iter(world)
        .filter(|(_, _, parent)| parent.parent() == root)
        .map(|(entity, _, _)| entity)
        .collect();
    assert_eq!(signs.len(), 1);
    (
        lanterns.into_iter().map(|(_, entity)| entity).collect(),
        signs[0],
    )
}

#[test]
#[ignore = "requires the existing, hash-pinned pier and motion packages"]
#[allow(
    clippy::too_many_lines,
    reason = "One complete acquired reference and rebuild replay"
)]
fn acquired_pier_uses_shared_host_and_preserves_motion_after_rebuild() {
    let geometry_root = root("FORGE_GATE7_PIER_ROOT");
    let motion_root = root("FORGE_GATE7_PIER_MOTION_ROOT");
    let geometry = tidewater_pier::PierPackage::load(
        &std::fs::read(geometry_root.join("package.json")).unwrap(),
        tidewater_pier::package_hash(),
        |name, maximum| files(&geometry_root, name, maximum),
    )
    .unwrap();
    let motion = PierMotionPackage::load(
        &std::fs::read(motion_root.join("manifest.json")).unwrap(),
        &geometry,
        |name, maximum| files(&motion_root, name, maximum),
    )
    .unwrap();
    let description: serde_json::Value = serde_json::from_slice(geometry.description()).unwrap();
    let mut material_handles = BTreeMap::new();
    materials(&description, &mut material_handles);
    assert!(!material_handles.is_empty());
    let mut placement = ManualPlacement {
        id: EntityId::deterministic("shared-pier-motion-qualification"),
        name: "Pier".into(),
        asset_id: tidewater_pier::asset_id(),
        asset_hash: tidewater_pier::package_hash(),
        transform: forge_core::Transform::IDENTITY,
        locked: false,
        physics: None,
        particles: None,
        local_lights: None,
    };
    let mut world = World::new();
    world.init_resource::<Assets<Mesh>>();
    let prepared = PreparedPier::from_package(&geometry).unwrap();
    let resident = prepared
        .stage_authored(&mut world, &material_handles, &[placement.clone()])
        .unwrap();
    let mut root = resident.authored_roots()[0].0;
    world.insert_resource(resident);
    let mut host = Collection::<ResidentPierMotion>::default();
    let (mut lanterns, mut sign) = pivots(&mut world, root);
    assert_eq!(lanterns.len(), motion.lanterns().len());
    let mut expected: Vec<_> = motion
        .lanterns()
        .iter()
        .map(|source| LanternMotion {
            t: source.t,
            ph: source.ph,
            x: source.x,
            z: source.z,
            vx: source.vx,
            vz: source.vz,
        })
        .collect();
    let source = motion.sign();
    let mut expected_sign = SignMotion {
        t: source.t,
        a: source.a,
        av: source.av,
        b: source.b,
        bv: source.bv,
    };
    let wind = Wind {
        speed: 7.0,
        direction: [0.6, 0.8],
    };
    let original = world.get::<Transform>(lanterns[0]).unwrap().rotation;
    world.get_mut::<PierLanternPivot>(lanterns[0]).unwrap().0 = 1;
    assert!(
        host.update(
            &mut world,
            &motion,
            &Desired::new(std::slice::from_ref(&placement)).unwrap(),
            1.0 / 60.0,
            wind,
        )
        .is_err()
    );
    assert!(host.is_empty());
    assert_eq!(
        world.get::<Transform>(lanterns[0]).unwrap().rotation,
        original
    );
    assert!(!forge_engine::static_hlod::moving_object_retained(
        &world,
        placement.id
    ));
    world.get_mut::<PierLanternPivot>(lanterns[0]).unwrap().0 = 0;
    world.entity_mut(sign).remove::<PierSwingPivot>();
    assert!(
        host.update(
            &mut world,
            &motion,
            &Desired::new(std::slice::from_ref(&placement)).unwrap(),
            1.0 / 60.0,
            wind,
        )
        .is_err()
    );
    assert!(host.is_empty());
    world.entity_mut(sign).insert(PierSwingPivot);
    for frame in 0..240 {
        if frame == 120 {
            let _ = world.remove_resource::<ResidentPier>();
            let _ = world.despawn(root);
            placement.transform =
                forge_core::Transform::from_translation(forge_core::Vec3::new(10.0, 0.0, -3.0));
            let prepared = PreparedPier::from_package(&geometry).unwrap();
            let resident = prepared
                .stage_authored(&mut world, &material_handles, &[placement.clone()])
                .unwrap();
            root = resident.authored_roots()[0].0;
            world.insert_resource(resident);
            (lanterns, sign) = pivots(&mut world, root);
        }
        let desired = Desired::new(std::slice::from_ref(&placement)).unwrap();
        host.update(&mut world, &motion, &desired, 1.0 / 60.0, wind)
            .unwrap();
        assert!(forge_engine::static_hlod::moving_object_retained(
            &world,
            placement.id
        ));
        for (entity, state) in lanterns.iter().zip(&mut expected) {
            state.advance(1.0 / 60.0, wind).unwrap();
            assert_eq!(
                world.get::<Transform>(*entity).unwrap().rotation,
                state.rotation().as_quat()
            );
        }
        expected_sign.advance(1.0 / 60.0, wind).unwrap();
        assert_eq!(
            world.get::<Transform>(sign).unwrap().rotation,
            expected_sign.rotation().as_quat()
        );
    }
    assert_eq!(host.len(), 1);
    host.update(&mut world, &motion, &Desired::new(&[]).unwrap(), 0.0, wind)
        .unwrap();
    assert!(host.is_empty());
    assert!(!forge_engine::static_hlod::moving_object_retained(
        &world,
        placement.id
    ));
    assert!(world.get_entity(root).is_ok());
    println!(
        "240 frames; {} lanterns and one sign; authored rebuild preserved",
        lanterns.len()
    );
}
