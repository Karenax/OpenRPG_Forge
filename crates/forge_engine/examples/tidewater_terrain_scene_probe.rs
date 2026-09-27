//! CPU assembly and replacement of imported terrain; no visual/GPU claims.
use bevy::{prelude::*, window::ExitCondition, winit::WinitPlugin};
use forge_assets::tidewater_terrain::TerrainPackage;
use forge_engine::tidewater_terrain::{
    TerrainSettings,
    lod::ViewGeometry,
    scene::{self, Bindings, PreparedTerrain, ResidentTerrain},
};
use forge_project::{TerrainDocument, TerrainHeightDelta};
use forge_world::{cdlod::CdlodView, sampled_terrain::SampledTerrainPlan};
use std::{error::Error, fs, io::Read, path::PathBuf};

fn bindings(world: &mut World) -> Bindings {
    // CPU-owned placeholders intentionally do not qualify rendering or lighting.
    let image = world.resource_mut::<Assets<Image>>().add(Image::default());
    Bindings {
        environment: image.clone(),
        hill_shadow: image.clone(),
        object_shadow: image.clone(),
        object_settings: default(),
        clouds: [Vec4::ZERO; 5],
        cloud_shadow: None,
        shore_state: image.clone(),
        shore_lace: image,
        shore_params: [UVec4::ZERO; 3],
        settings: TerrainSettings {
            wind: Vec4::new(1.0, 0.0, 6.0, 2.0),
            sun: Vec4::Y,
            domain: Vec4::new(-1024.0, 2048.0, 2048.0, 0.58),
            flags: Vec4::ZERO,
        },
    }
}
fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 4 {
        return Err("TERRAIN_JSON HEIGHTS_BIN PACKAGE_ROOT MANIFEST_SHA256 required".into());
    }
    let mut document = TerrainDocument::from_json(&fs::read_to_string(&args[0])?)?;
    let heights = fs::read(&args[1])?;
    let root = PathBuf::from(&args[2]).canonicalize()?;
    let manifest = fs::read(root.join("package.json"))?;
    let package = TerrainPackage::load(&manifest, args[3].parse()?, |name, limit| {
        let path = root.join(name).canonicalize().map_err(|e| e.to_string())?;
        if path.parent() != Some(root.as_path()) {
            return Err("Payload escapes package".into());
        }
        let mut data = Vec::new();
        fs::File::open(path)
            .map_err(|e| e.to_string())?
            .take(limit as u64 + 1)
            .read_to_end(&mut data)
            .map_err(|e| e.to_string())?;
        Ok(data)
    })?;
    let view = CdlodView {
        position: [42.0, 14.0, -90.0],
        planes: [
            [1.0, 0.0, 0.0, 1024.0],
            [-1.0, 0.0, 0.0, 1024.0],
            [0.0, 1.0, 0.0, 2048.0],
            [0.0, -1.0, 0.0, 2048.0],
            [0.0, 0.0, 1.0, 1024.0],
            [0.0, 0.0, -1.0, 1024.0],
        ],
    };
    let plan = SampledTerrainPlan::from_document(&document, &heights)?;
    camera_roundtrip(&plan, view)?;
    let mut app = App::new();
    assert!(scene::register(&mut app).is_err());
    app.add_plugins(
        DefaultPlugins
            .set(WindowPlugin {
                primary_window: None,
                exit_condition: ExitCondition::DontExit,
                ..default()
            })
            .disable::<WinitPlugin>(),
    );
    scene::register(&mut app)?;
    scene::register(&mut app)?;
    let lighting = bindings(app.world_mut());
    let first = PreparedTerrain::new(&plan, &heights, &package, view)?
        .stage(app.world_mut(), lighting.clone())?;
    assert_eq!(first.document_hash, plan.document_hash());
    let old = first.entity.ok_or("No terrain entity")?;
    assert!(app.world().get_entity(old).is_ok());
    let count = app.world().resource::<Assets<Image>>().len();
    let mut invalid = lighting.clone();
    invalid.environment = bevy::asset::uuid_handle!("39509219-f30f-43c6-ac64-93c53c0183f1");
    assert!(
        PreparedTerrain::new(&plan, &heights, &package, view)?
            .stage(app.world_mut(), invalid)
            .is_err()
    );
    assert_eq!(app.world().resource::<Assets<Image>>().len(), count);
    assert_eq!(app.world().resource::<ResidentTerrain>().entity, Some(old));
    document.height_deltas = vec![TerrainHeightDelta::new(1024, 1024, 12.0)];
    let changed = SampledTerrainPlan::from_document(&document, &heights)?;
    let second = PreparedTerrain::new(&changed, &heights, &package, view)?
        .stage(app.world_mut(), lighting)?;
    assert_ne!(first.document_hash, second.document_hash);
    assert_ne!(first.entity, second.entity);
    assert!(app.world().get_entity(old).is_err());
    assert!(
        app.world()
            .get_entity(second.entity.ok_or("No replacement entity")?)
            .is_ok()
    );
    assert!(
        !second
            .normals
            .ready
            .load(std::sync::atomic::Ordering::Acquire)
    );
    println!(
        "TERRAIN_ASSEMBLY_PASS cached camera leave/return, replacement entity, document hash, atomic rejection, pending edited normals"
    );
    Ok(())
}

fn camera_roundtrip(plan: &SampledTerrainPlan, view: CdlodView) -> Result<(), Box<dyn Error>> {
    let geometry = ViewGeometry::new(plan)?;
    assert_eq!(geometry.document_hash, plan.document_hash());
    let (initial_mesh, initial_nodes) = geometry.mesh(view)?.ok_or("Initial view is empty")?;
    let away = CdlodView {
        position: [100_000.0, 0.0, 0.0],
        ..view
    };
    assert!(
        geometry.mesh(away)?.is_none(),
        "distant view should not retain terrain nodes"
    );
    let (returned_mesh, returned_nodes) = geometry.mesh(view)?.ok_or("Returning view is empty")?;
    assert_eq!(initial_nodes, returned_nodes);
    assert_eq!(
        initial_mesh.count_vertices(),
        returned_mesh.count_vertices()
    );
    assert!(
        initial_mesh
            .indices()
            .expect("indexed mesh")
            .iter()
            .eq(returned_mesh.indices().expect("indexed mesh").iter())
    );
    Ok(())
}
