//! Verify the actual carried collision geometry and Rapier queries from both faces.
use bevy::{
    asset::RenderAssetUsages,
    mesh::{Indices, PrimitiveTopology},
    prelude::*,
};
use bevy_rapier3d::prelude::Collider;
use forge_engine::{
    ManualPlacementCollision, ManualPlacementCollisionGeometry, ManualPlacementCollisionPlugin,
};

fn inspect(path: &std::path::Path) -> Result<(), Box<dyn std::error::Error>> {
    let bytes = std::fs::read(path)?;
    let glb = gltf::Gltf::from_slice(&bytes)?;
    let blob = glb.blob.as_deref().ok_or("No BIN chunk")?;
    let nodes: Vec<_> = glb
        .nodes()
        .filter(|node| node.name().is_some_and(|name| name.starts_with("UTM_")))
        .collect();
    let [node] = nodes.as_slice() else {
        return Err("Expected exactly one carried collision".into());
    };
    let [primitive]: [gltf::Primitive<'_>; 1] = node
        .mesh()
        .ok_or("Missing collision mesh")?
        .primitives()
        .collect::<Vec<_>>()
        .try_into()
        .map_err(|_: Vec<_>| "Expected exactly one collision primitive")?;
    let reader = primitive.reader(|buffer| (buffer.index() == 0).then_some(blob));
    let positions: Vec<_> = reader
        .read_positions()
        .ok_or("Missing positions")?
        .collect();
    let indices = reader
        .read_indices()
        .ok_or("Missing indices")?
        .into_u32()
        .collect();
    let mut mesh = Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::MAIN_WORLD,
    );
    mesh.insert_attribute(Mesh::ATTRIBUTE_POSITION, positions);
    mesh.insert_indices(Indices::U32(indices));
    let mut app = App::new();
    app.init_resource::<Assets<Mesh>>()
        .add_plugins(ManualPlacementCollisionPlugin);
    let handle = app.world_mut().resource_mut::<Assets<Mesh>>().add(mesh);
    let entity = app
        .world_mut()
        .spawn((
            Mesh3d(handle),
            ManualPlacementCollision(forge_core::EntityId::deterministic("witch-flat-box-probe")),
            ManualPlacementCollisionGeometry::FlatBoxSurface,
        ))
        .id();
    app.update();
    let collider = app
        .world()
        .get::<Collider>(entity)
        .ok_or("Flat box did not materialize")?;
    let rotation = Mat4::from_cols_array_2d(&node.transform().matrix());
    let toward_front = rotation.transform_vector3(Vec3::NEG_Z);
    if toward_front != Vec3::Y || collider.as_trimesh().is_none() {
        return Err("Flat box normal or collision kind changed".into());
    }
    let front = collider.cast_local_ray(Vec3::Z, Vec3::NEG_Z, 2., false);
    let back = collider.cast_local_ray(Vec3::NEG_Z, Vec3::Z, 2., false);
    let miss = collider.cast_local_ray(Vec3::new(0.51, 0., 1.), Vec3::NEG_Z, 2., false);
    if (front, back, miss) != (Some(1.), Some(1.), None) {
        return Err("Flat collision does not match the measured two-sided ray profile".into());
    }
    println!(
        "{}",
        serde_json::json!({
            "path":path, "sha256":forge_core::bytes_sha256(&bytes).to_string(),
            "front":front, "back":back, "outside":miss, "world_normal":toward_front.to_array()
        })
    );
    Ok(())
}

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let paths: Vec<_> = std::env::args_os().skip(1).collect();
    if paths.len() != 2 {
        return Err("Expected the two private carriers".into());
    }
    for path in paths {
        inspect(std::path::Path::new(&path))?;
    }
    Ok(())
}
