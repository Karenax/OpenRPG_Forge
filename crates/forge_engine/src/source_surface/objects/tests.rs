use super::*;
use crate::source_surface::{
    geometry::{MeshPart, PreparedMeshes},
    tests::lighting,
};
use bevy::{asset::RenderAssetUsages, mesh::Indices, render::render_resource::PrimitiveTopology};
use bevy_rapier3d::plugin::context::{DefaultRapierContext, RapierContextColliders};
use forge_core::{AssetId, EntityId};

type Materials = BTreeMap<String, Handle<SourceSurfaceMaterial>>;

fn triangle() -> Mesh {
    Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    )
    .with_inserted_attribute(
        Mesh::ATTRIBUTE_POSITION,
        vec![[0.0, 0.0, 0.0], [2.0, 0.0, 0.0], [0.0, 0.0, 2.0]],
    )
    .with_inserted_indices(Indices::U32(vec![0, 1, 2]))
}

fn fixture() -> (App, ManualPlacement, Materials) {
    let mut app = App::new();
    app.add_plugins((
        MinimalPlugins,
        TransformPlugin,
        crate::ManualPlacementCollisionPlugin,
    ));
    crate::sampled_terrain_physics::register(&mut app);
    app.init_resource::<Assets<Mesh>>()
        .init_resource::<Assets<SourceSurfaceMaterial>>();
    let source = ManualPlacement {
        id: EntityId::deterministic("independent object"),
        name: "Module object".into(),
        asset_id: AssetId::deterministic("independent geometry"),
        asset_hash: forge_core::bytes_sha256(b"independent revision"),
        transform: forge_core::Transform::IDENTITY,
        locked: false,
        particles: None,
        physics: None,
        local_lights: None,
    };
    let geometry = PreparedMeshes::from_parts(BTreeMap::from([(
        (source.asset_id, source.asset_hash),
        vec![MeshPart {
            mesh: triangle(),
            material: "opaque".into(),
        }],
    )]))
    .stage(&mut app.world_mut().resource_mut::<Assets<Mesh>>());
    app.insert_resource(geometry);
    let material = app
        .world_mut()
        .resource_mut::<Assets<SourceSurfaceMaterial>>()
        .add(lighting());
    (app, source, BTreeMap::from([("opaque".into(), material)]))
}

fn prepare(
    app: &App,
    sources: &[ManualPlacement],
    materials: &Materials,
) -> Result<PreparedScene, String> {
    PreparedScene::new(
        sources,
        app.world().resource::<ResidentMeshes>(),
        materials,
        app.world().resource::<Assets<Mesh>>(),
        app.world().resource::<Assets<SourceSurfaceMaterial>>(),
    )
}

fn ready(app: &App) -> bool {
    app.world()
        .resource::<ResidentScene>()
        .is_synchronized(app.world())
}

fn synchronize(app: &mut App, expected: usize) {
    app.update();
    app.world_mut().run_schedule(FixedUpdate);
    assert!(ready(app));
    let mut query = app
        .world_mut()
        .query_filtered::<&RapierContextColliders, With<DefaultRapierContext>>();
    assert_eq!(query.single(app.world()).unwrap().colliders.len(), expected);
}

#[test]
fn independent_scene_replaces_duplicates_and_retires_backend_collisions_with_shared_geometry() {
    let (mut app, original, materials) = fixture();
    prepare(&app, std::slice::from_ref(&original), &materials)
        .unwrap()
        .stage(app.world_mut());
    assert!(!ready(&app));
    synchronize(&mut app, 1);
    let first = app.world().resource::<ResidentScene>().instances[0].root;
    let geometry = app
        .world()
        .resource::<ResidentMeshes>()
        .resolve(original.asset_id, original.asset_hash)
        .unwrap()[0]
        .mesh
        .clone();
    let mut changed = original.clone();
    changed.transform.translation_m.x = 10.0;
    changed.transform.rotation = forge_core::UnitQuaternion {
        x: 0.0,
        y: 0.6,
        z: 0.0,
        w: 0.8,
    };
    changed.transform.scale = forge_core::Vec3 {
        x: 2.0,
        y: 1.0,
        z: 0.5,
    };
    let mut duplicate = changed.clone();
    duplicate.id = EntityId::deterministic("second independent object");
    duplicate.transform.translation_m.z = 8.0;
    let mut sources = vec![changed, duplicate];
    sources.sort_by_key(|p| p.id);
    prepare(&app, &sources, &materials)
        .unwrap()
        .stage(app.world_mut());
    assert!(app.world().get_entity(first).is_err());
    assert!(!ready(&app));
    synchronize(&mut app, 2);
    for instance in &app.world().resource::<ResidentScene>().instances {
        assert_eq!(
            app.world().get::<Mesh3d>(instance.parts[0]).unwrap().0,
            geometry
        );
    }
    let root = app.world().resource::<ResidentScene>().instances[0].root;
    let parent = app.world_mut().spawn(Transform::IDENTITY).id();
    app.world_mut().entity_mut(root).insert(ChildOf(parent));
    assert!(!ready(&app));
    app.world_mut().entity_mut(root).remove::<ChildOf>();
    synchronize(&mut app, 2);
    PreparedScene::default().stage(app.world_mut());
    assert!(!ready(&app));
    synchronize(&mut app, 0);
    assert_eq!(app.world().resource::<Assets<Mesh>>().len(), 1);
    prepare(&app, &[original], &materials)
        .unwrap()
        .stage(app.world_mut());
    synchronize(&mut app, 1);
    assert_eq!(app.world().resource::<Assets<Mesh>>().len(), 1);
}

#[test]
fn failed_revisions_materials_and_surface_preparation_leave_current_scene_untouched() {
    let (mut app, original, materials) = fixture();
    prepare(&app, std::slice::from_ref(&original), &materials)
        .unwrap()
        .stage(app.world_mut());
    synchronize(&mut app, 1);
    let root = app.world().resource::<ResidentScene>().instances[0].root;
    let mut stale = original.clone();
    stale.asset_hash = forge_core::bytes_sha256(b"different revision");
    assert!(prepare(&app, &[stale], &materials).is_err());
    assert!(prepare(&app, &[original.clone(), original.clone()], &materials).is_err());
    assert!(prepare(&app, std::slice::from_ref(&original), &BTreeMap::new()).is_err());
    let mesh = app
        .world()
        .resource::<ResidentMeshes>()
        .resolve(original.asset_id, original.asset_hash)
        .unwrap()[0]
        .mesh
        .clone();
    app.world_mut()
        .resource_mut::<Assets<Mesh>>()
        .get_mut(&mesh)
        .unwrap()
        .insert_attribute(Mesh::ATTRIBUTE_POSITION, vec![[f32::NAN, 0.0, 0.0]; 3]);
    assert!(prepare(&app, &[original], &materials).is_err());
    assert_eq!(
        app.world().resource::<ResidentScene>().instances[0].root,
        root
    );
    assert!(app.world().get_entity(root).is_ok());
    assert_eq!(app.world().resource::<Assets<Mesh>>().len(), 1);
}
