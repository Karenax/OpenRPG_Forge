//! Real portable buildings, canonical placements and Rapier replacement. No GPU claims.
use bevy::{ecs::system::SystemState, prelude::*};
use bevy_rapier3d::{
    plugin::context::{DefaultRapierContext, RapierContextColliders},
    prelude::{QueryFilter, ReadRapierContext},
};
use forge_assets::tidewater_package::BuildingPackage;
use forge_engine::tidewater_assets::{
    PreparedLibrary, PreparedMeshes, ResidentMeshes, materials,
    scene::{PreparedScene, ResidentScene},
};
use forge_engine::tidewater_material::{SceneLighting, VillageMaterial};
use forge_project::zone_authoring::{placements::ManualPlacement, session::ZoneEditSession};
use std::{collections::BTreeMap, error::Error, fs, io::Read, path::Path};

#[path = "tidewater_building_scene_probe/shadows.rs"]
mod shadows;

type MaterialHandles = BTreeMap<String, Handle<VillageMaterial>>;

fn package(root: &Path, pin: forge_core::ContentHash) -> Result<BuildingPackage, Box<dyn Error>> {
    let root = root.canonicalize()?;
    Ok(BuildingPackage::load(
        &fs::read(root.join("package.json"))?,
        pin,
        |name, limit| {
            let path = root.join(name).canonicalize().map_err(|e| e.to_string())?;
            if path.parent() != Some(root.as_path()) {
                return Err("Escaping package".into());
            }
            let mut bytes = Vec::new();
            fs::File::open(path)
                .map_err(|e| e.to_string())?
                .take(limit as u64 + 1)
                .read_to_end(&mut bytes)
                .map_err(|e| e.to_string())?;
            Ok(bytes)
        },
    )?)
}
fn prepare(
    app: &App,
    sources: &[ManualPlacement],
    materials: &MaterialHandles,
) -> Result<PreparedScene, String> {
    PreparedScene::new(
        sources,
        app.world().resource::<ResidentMeshes>(),
        materials,
        app.world().resource::<Assets<Mesh>>(),
        app.world().resource::<Assets<VillageMaterial>>(),
    )
}
fn ready(app: &App) -> bool {
    app.world()
        .resource::<ResidentScene>()
        .is_synchronized(app.world())
}
fn sync(app: &mut App, expected: usize) {
    app.update();
    app.world_mut().run_schedule(FixedUpdate);
    assert!(
        ready(app),
        "current building poses/shapes must be synchronized"
    );
    let mut query = app
        .world_mut()
        .query_filtered::<&RapierContextColliders, With<DefaultRapierContext>>();
    assert_eq!(
        query.single(app.world()).expect("context").colliders.len(),
        expected,
        "stale or missing collisions"
    );
}
fn ray(app: &mut App) {
    use bevy::mesh::VertexAttributeValues;
    let part = app.world().resource::<ResidentScene>().instances[0].parts[0];
    let world = app.world();
    let mesh = world
        .resource::<Assets<Mesh>>()
        .get(&world.get::<Mesh3d>(part).expect("mesh").0)
        .expect("resident mesh");
    let VertexAttributeValues::Float32x3(vertices) =
        mesh.attribute(Mesh::ATTRIBUTE_POSITION).expect("positions")
    else {
        panic!("positions format")
    };
    let indices = mesh.indices().expect("indices").iter().collect::<Vec<_>>();
    let pose = world.get::<GlobalTransform>(part).expect("global").affine();
    let (center, normal) = indices
        .as_chunks::<3>()
        .0
        .iter()
        .find_map(|t| {
            let points = t
                .iter()
                .map(|&i| pose.transform_point3(Vec3::from_array(vertices[i])))
                .collect::<Vec<_>>();
            let normal = (points[1] - points[0]).cross(points[2] - points[0]);
            (normal.length() > 0.01).then(|| {
                (
                    (points[0] + points[1] + points[2]) / 3.0,
                    normal.normalize(),
                )
            })
        })
        .expect("nondegenerate triangle");
    let mut state = SystemState::<ReadRapierContext>::new(app.world_mut());
    let contexts = state.get(app.world()).expect("parameters");
    let context = contexts.single().expect("context");
    let predicate = |entity| entity == part;
    let (entity, distance) = context
        .cast_ray(
            center + normal,
            -normal,
            2.0,
            true,
            QueryFilter::default().predicate(&predicate),
        )
        .expect("transformed building triangle hit");
    assert_eq!(entity, part);
    assert!(distance <= 1.01);
}
fn setup(package: &BuildingPackage) -> Result<(App, MaterialHandles), Box<dyn Error>> {
    let mut app = App::new();
    app.add_plugins((
        MinimalPlugins,
        TransformPlugin,
        forge_engine::ManualPlacementCollisionPlugin,
    ));
    forge_engine::sampled_terrain_physics::register(&mut app);
    app.init_resource::<Assets<Mesh>>()
        .init_resource::<Assets<Image>>()
        .init_resource::<Assets<VillageMaterial>>();
    let meshes =
        PreparedMeshes::load(package)?.stage(&mut app.world_mut().resource_mut::<Assets<Mesh>>());
    app.insert_resource(meshes);
    let library =
        PreparedLibrary::load(package)?.stage(&mut app.world_mut().resource_mut::<Assets<Image>>());
    // Material assets validate identity only; these placeholders are not a rendered scene.
    let lighting = materials::LightingBindings {
        scene_lighting: SceneLighting::new([0.0, 1.0, 0.0], [1.0; 3], [[0.0; 3]; 9])?,
        environment: default(),
        hill_shadow: default(),
        object_shadow: default(),
        object_settings: default(),
        clouds: [Vec4::ZERO; 5],
        cloud_shadow: None,
        wind: Vec4::ZERO,
        flags: Vec4::ZERO,
    };
    let mut handles = BTreeMap::new();
    for (name, params) in &library.materials {
        let material = materials::prepare(params, &library.textures, &lighting)?;
        handles.insert(
            name.clone(),
            app.world_mut()
                .resource_mut::<Assets<VillageMaterial>>()
                .add(material),
        );
    }
    Ok((app, handles))
}
fn main() -> Result<(), Box<dyn Error>> {
    let args = std::env::args().skip(1).collect::<Vec<_>>();
    if !(3..=4).contains(&args.len()) {
        return Err(
            "PACKAGE_ROOT PIN CANONICAL_ZONE [NEW_SHADOW_OUTPUT_DIRECTORY] required".into(),
        );
    }
    let package = package(Path::new(&args[0]), args[1].parse()?)?;
    let zone = ZoneEditSession::from_archive_bytes(&fs::read(&args[2])?)?;
    let original = zone
        .head()
        .corrections
        .placements
        .clone()
        .ok_or("placements")?;
    assert_eq!(original.len(), 23);
    let (mut app, materials) = setup(&package)?;
    prepare(&app, &original, &materials)?.stage(app.world_mut());
    assert!(!ready(&app));
    sync(&mut app, 111);
    ray(&mut app);
    let roots = app
        .world()
        .resource::<ResidentScene>()
        .instances
        .iter()
        .map(|i| i.root)
        .collect::<Vec<_>>();
    let mut invalid = original.clone();
    invalid[0].asset_hash = forge_core::bytes_sha256(b"stale");
    assert!(prepare(&app, &invalid, &materials).is_err());
    assert!(
        roots
            .iter()
            .all(|&root| app.world().get_entity(root).is_ok())
    );
    assert!(ready(&app));
    let mut changed = original.clone();
    changed[0].transform.translation_m.x += 12.0;
    let q = Quat::from_rotation_y(0.7);
    changed[0].transform.rotation = forge_core::UnitQuaternion {
        x: q.x,
        y: q.y,
        z: q.z,
        w: q.w,
    };
    changed[0].transform.scale = forge_core::Vec3 {
        x: 1.25,
        y: 0.9,
        z: 1.1,
    };
    prepare(&app, &changed, &materials)?.stage(app.world_mut());
    assert!(!ready(&app));
    assert!(
        roots
            .iter()
            .all(|&root| app.world().get_entity(root).is_err())
    );
    sync(&mut app, 111);
    ray(&mut app);
    let extra = app.world().resource::<ResidentScene>().instances[0]
        .parts
        .len();
    let mut duplicate = changed[0].clone();
    duplicate.id = forge_core::EntityId::deterministic("building scene duplicate");
    duplicate.transform.translation_m.z += 30.0;
    changed.push(duplicate);
    changed.sort_by_key(|p| p.id);
    prepare(&app, &changed, &materials)?.stage(app.world_mut());
    sync(&mut app, 111 + extra);
    ray(&mut app);
    PreparedScene::default().stage(app.world_mut());
    assert!(
        !ready(&app),
        "removed backend colliders still need synchronization"
    );
    sync(&mut app, 0);
    prepare(&app, &original, &materials)?.stage(app.world_mut());
    sync(&mut app, 111);
    ray(&mut app);
    assert_eq!(
        app.world().resource::<Assets<Mesh>>().len(),
        111,
        "instances must reuse geometry"
    );
    println!(
        "BUILDING_SCENE_PASS 23 buildings 111 colliders; stale pin rejection, translate/rotate/scale, duplicate, delete all, restore, triangle rays and shared meshes"
    );
    if let Some(output) = args.get(3) {
        shadows::run(app, &materials, output.into())?;
    }
    Ok(())
}
