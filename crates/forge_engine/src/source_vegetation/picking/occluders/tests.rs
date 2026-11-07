use super::*;
use bevy::{asset::RenderAssetUsages, mesh::Indices, render::render_resource::PrimitiveTopology};

#[test]
fn lit_models_keep_their_real_mesh_pose_and_source_bindings_in_the_depth_pass() {
    use crate::material_composition::ComposedMaterial;
    use crate::source_lighting::surfaces::{ModelMaterial, Receiver};
    let mut world = World::new();
    world.init_resource::<Assets<Mesh>>();
    world.init_resource::<Assets<ModelMaterial>>();
    world.init_resource::<Assets<ModelOccluder>>();
    let mesh = world.resource_mut::<Assets<Mesh>>().add(triangle(false));
    let source = ComposedMaterial {
        base: StandardMaterial::default(),
        extension: Receiver {
            scene: Handle::default(),
            shadows: Handle::default(),
            frame: Handle::default(),
            local_lights: Handle::default(),
            environment: Handle::default(),
            hill: Handle::default(),
            objects: Handle::default(),
            object_settings: default(),
            clouds: [Vec4::ZERO; 5],
            cloud_shadow: Handle::default(),
            domain: Vec4::ONE,
            terrain: false,
            vegetation: false,
        },
    };
    let mut transparent = source.clone();
    transparent.base.alpha_mode = AlphaMode::Blend;
    let transparent = world
        .resource_mut::<Assets<ModelMaterial>>()
        .add(transparent);
    let source = world.resource_mut::<Assets<ModelMaterial>>().add(source);
    let pose = Transform::from_xyz(4.0, 5.0, 6.0);
    let original = world
        .spawn((Mesh3d(mesh.clone()), MeshMaterial3d(source.clone()), pose))
        .id();
    world.spawn((Mesh3d(mesh.clone()), MeshMaterial3d(transparent)));
    let mut draws = model_occluders(&world).unwrap();
    assert_eq!(draws.len(), 1);
    let draw = draws.pop().unwrap();
    assert_eq!(draw.mesh, mesh);
    assert_eq!(draw.transform, pose);
    assert_eq!(draw.source, source.id());
    assert_eq!(draw.material.base.extension.domain, Vec4::ONE);
    let material = world
        .resource_mut::<Assets<ModelOccluder>>()
        .add(draw.material.clone());
    let copied = draw.stage(&mut world, material, 30);
    assert_eq!(*world.get::<Transform>(copied).unwrap(), pose);
    assert_eq!(world.get::<Mesh3d>(copied).unwrap().0, mesh);
    assert_eq!(
        world
            .get::<MeshMaterial3d<ModelMaterial>>(original)
            .unwrap()
            .0,
        source
    );
    assert_eq!(world.resource::<Assets<Mesh>>().len(), 1);
}

fn triangle(skinned: bool) -> Mesh {
    let mut mesh = Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    );
    mesh.insert_attribute(
        Mesh::ATTRIBUTE_POSITION,
        vec![[0.0, 0.0, 0.0], [1.0, 0.0, 0.0], [0.0, 1.0, 0.0]],
    );
    mesh.insert_indices(Indices::U32(vec![0, 1, 2]));
    if skinned {
        mesh.insert_attribute(
            Mesh::ATTRIBUTE_JOINT_INDEX,
            bevy::mesh::VertexAttributeValues::Uint16x4(vec![[0u16; 4]; 3]),
        );
        mesh.insert_attribute(Mesh::ATTRIBUTE_JOINT_WEIGHT, vec![[1.0, 0.0, 0.0, 0.0]; 3]);
    }
    mesh
}

fn draw(mesh: Handle<Mesh>, skin: Option<SkinnedMesh>) -> Draw<StandardOccluder, ()> {
    Draw {
        mesh,
        material: StandardOccluder {
            base: StandardMaterial::default(),
            extension: StandardDepth::default(),
        },
        source: (),
        transform: Transform::from_xyz(4.0, 5.0, 6.0),
        global_transform: GlobalTransform::from_translation(Vec3::new(4.0, 5.0, 6.0)),
        parent: None,
        skin,
    }
}

#[test]
fn skinned_occluder_keeps_source_mesh_and_real_joint_references() {
    let mut world = World::new();
    world.init_resource::<Assets<Mesh>>();
    world.init_resource::<Assets<StandardOccluder>>();
    world.init_resource::<Assets<SkinnedMeshInverseBindposes>>();
    let joint_pose = GlobalTransform::from_translation(Vec3::new(3.0, 2.0, 1.0));
    let joint = world.spawn(joint_pose).id();
    let source_skin = SkinnedMesh {
        inverse_bindposes: world
            .resource_mut::<Assets<SkinnedMeshInverseBindposes>>()
            .add(SkinnedMeshInverseBindposes::from(vec![Mat4::IDENTITY])),
        joints: vec![joint],
    };
    let source = world.spawn(source_skin.clone()).id();
    let source_mesh = triangle(true);
    let captured = skin_for_draw(&world, source, &source_mesh).unwrap();
    let mesh = world.resource_mut::<Assets<Mesh>>().add(source_mesh);
    let draw = draw(mesh.clone(), captured);
    let material = world
        .resource_mut::<Assets<StandardOccluder>>()
        .add(draw.material.clone());
    let entity = draw.stage(&mut world, material, 30);

    assert_eq!(world.get::<Mesh3d>(entity).unwrap().0, mesh);
    assert_eq!(
        *world.get::<GlobalTransform>(entity).unwrap(),
        GlobalTransform::from_translation(Vec3::new(4.0, 5.0, 6.0)),
    );
    let skin = world.get::<SkinnedMesh>(entity).unwrap();
    assert_eq!(skin.inverse_bindposes, source_skin.inverse_bindposes);
    assert_eq!(skin.joints, source_skin.joints);
    assert_eq!(
        world.get::<SkinnedMesh>(source).unwrap().joints,
        vec![joint]
    );
    assert_eq!(*world.get::<GlobalTransform>(joint).unwrap(), joint_pose);
    assert_eq!(world.resource::<Assets<Mesh>>().len(), 1);
    let original = world.resource::<Assets<Mesh>>().get(&mesh).unwrap();
    assert!(original.contains_attribute(Mesh::ATTRIBUTE_JOINT_INDEX));
    assert!(original.contains_attribute(Mesh::ATTRIBUTE_JOINT_WEIGHT));
    assert_eq!(original.indices().unwrap(), &Indices::U32(vec![0, 1, 2]));
}

#[test]
fn ordinary_occluder_does_not_acquire_a_skin() {
    let mut world = World::new();
    let source = world.spawn_empty().id();
    assert!(
        skin_for_draw(&world, source, &triangle(false))
            .unwrap()
            .is_none()
    );
    let entity = draw(Handle::default(), None).stage(&mut world, Handle::default(), 30);
    assert!(world.get::<SkinnedMesh>(entity).is_none());
}

#[test]
fn clone_preserves_parent_local_and_affine_world_pose() {
    let mut world = World::new();
    let parent_transform = Transform::from_xyz(7.0, 8.0, 9.0)
        .with_rotation(Quat::from_rotation_y(0.4))
        .with_scale(Vec3::new(2.0, 3.0, 4.0));
    let parent_global = GlobalTransform::from(parent_transform);
    let parent = world.spawn((parent_transform, parent_global)).id();
    let local = Transform::from_xyz(0.2, 0.3, 0.4).with_rotation(Quat::from_rotation_x(0.7));
    let global = parent_global.mul_transform(local);
    let source = world.spawn((local, global, ChildOf(parent))).id();
    let mut draw = draw(Handle::default(), None);
    draw.transform = local;
    draw.global_transform = global;
    draw.parent = Some(parent);
    let clone = draw.stage(&mut world, Handle::default(), 30);

    assert_eq!(
        world.get::<Transform>(clone),
        world.get::<Transform>(source)
    );
    assert_eq!(world.get::<ChildOf>(clone), world.get::<ChildOf>(source));
    assert_eq!(
        world.get::<GlobalTransform>(clone),
        world.get::<GlobalTransform>(source)
    );
    let recomposed = world
        .get::<GlobalTransform>(parent)
        .unwrap()
        .mul_transform(*world.get::<Transform>(clone).unwrap());
    assert_eq!(recomposed, global);
    world.despawn(clone);
    assert!(world.get_entity(source).is_ok());
    assert!(world.get_entity(parent).is_ok());
}

#[test]
fn inconsistent_skin_is_rejected_before_staging_entities() {
    let mut world = World::new();
    let unskinned = world.spawn_empty().id();
    let skinned = world.spawn(SkinnedMesh::default()).id();
    let count = world.entities().len();
    assert!(skin_for_draw(&world, unskinned, &triangle(true)).is_err());
    assert!(skin_for_draw(&world, skinned, &triangle(false)).is_err());
    assert_eq!(world.entities().len(), count);
}

#[test]
fn missing_inverse_bind_or_joint_pose_is_rejected_before_staging() {
    let mut world = World::new();
    let joint = world.spawn_empty().id();
    let source = world
        .spawn(SkinnedMesh {
            inverse_bindposes: Handle::default(),
            joints: vec![joint],
        })
        .id();
    let mesh = triangle(true);
    assert!(skin_for_draw(&world, source, &mesh).is_err());
    world.init_resource::<Assets<SkinnedMeshInverseBindposes>>();
    let poses = world
        .resource_mut::<Assets<SkinnedMeshInverseBindposes>>()
        .add(SkinnedMeshInverseBindposes::from(vec![Mat4::IDENTITY]));
    world
        .get_mut::<SkinnedMesh>(source)
        .unwrap()
        .inverse_bindposes = poses;
    assert!(skin_for_draw(&world, source, &mesh).is_err());
    world.entity_mut(joint).insert(GlobalTransform::IDENTITY);
    assert!(skin_for_draw(&world, source, &mesh).is_ok());
}

#[test]
fn common_snapshot_accepts_caller_material_families_without_map_types() {
    type Terrain = ExtendedMaterial<StandardMaterial, TerrainDepth>;
    type Surface = ExtendedMaterial<StandardMaterial, VillageDepth>;
    let mut world = World::new();
    world.init_resource::<Assets<Mesh>>();
    world.init_resource::<Assets<Terrain>>();
    world.init_resource::<Assets<Surface>>();
    world.init_resource::<Assets<StandardMaterial>>();
    world.init_resource::<Assets<TerrainOccluder<Terrain>>>();
    world.init_resource::<Assets<VillageOccluder<Surface>>>();
    world.init_resource::<Assets<StandardOccluder>>();
    let mesh = world.resource_mut::<Assets<Mesh>>().add(triangle(false));
    let terrain = world.resource_mut::<Assets<Terrain>>().add(Terrain {
        base: StandardMaterial::default(),
        extension: TerrainDepth::default(),
    });
    let surface = world.resource_mut::<Assets<Surface>>().add(Surface {
        base: StandardMaterial::default(),
        extension: VillageDepth::default(),
    });
    let transparent = world.resource_mut::<Assets<Surface>>().add(Surface {
        base: StandardMaterial {
            alpha_mode: AlphaMode::Blend,
            ..default()
        },
        extension: VillageDepth::default(),
    });
    let standard = world
        .resource_mut::<Assets<StandardMaterial>>()
        .add(StandardMaterial::default());
    let terrain_entity = world
        .spawn((Mesh3d(mesh.clone()), MeshMaterial3d(terrain)))
        .id();
    world.spawn((Mesh3d(mesh.clone()), MeshMaterial3d(surface)));
    world.spawn((Mesh3d(mesh.clone()), MeshMaterial3d(transparent)));
    world.spawn((Mesh3d(mesh), MeshMaterial3d(standard)));
    let source_entities = world.entities().len();
    let prepared = Prepared::<Terrain, Surface>::from_world(&world).unwrap();
    assert_eq!(prepared.counts(), (1, 1));
    assert_eq!(prepared.standard_count(), 1);
    assert_eq!(prepared.transparent_skipped, 1);
    let entities = prepared.stage(&mut world, 30);
    assert_eq!(entities.len(), 3);
    assert!(
        world
            .get::<MeshMaterial3d<Terrain>>(terrain_entity)
            .is_some()
    );
    for entity in entities {
        assert!(world.get::<NotShadowCaster>(entity).is_some());
        assert_eq!(
            world.get::<RenderLayers>(entity),
            Some(&RenderLayers::layer(30))
        );
        world.despawn(entity);
    }
    assert_eq!(world.entities().len(), source_entities);
}
