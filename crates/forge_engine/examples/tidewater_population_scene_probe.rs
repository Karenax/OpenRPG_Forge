//! Portable package + canonical population identities through real ECS projection.
use bevy::{ecs::world::CommandQueue, prelude::*, render::storage::ShaderBuffer};
use forge_assets::tidewater_plants::PlantPackage;
use forge_engine::tidewater_plants::{Params, PlantMaterial, canonical, scene};
use forge_project::{
    authoring::{AuthoringRequest, host::AuthoringHost, workspace::WorkspaceHostRequest},
    workspace::source_blobs::SourceBlobStore,
};
use forge_world::imported_vegetation::ImportedPopulation;
use std::{collections::BTreeSet, error::Error, fs, io::Read, path::Path};

fn main() -> Result<(), Box<dyn Error>> {
    let args = std::env::args().skip(1).collect::<Vec<_>>();
    if args.len() != 4 {
        return Err("WORKSPACE AREA PACKAGE PIN required".into());
    }
    let mut host = AuthoringHost::new(Path::new(&args[0]))?;
    host.execute_workspace(WorkspaceHostRequest::Open)?;
    let state = host.execute(AuthoringRequest::OpenManaged {
        area_id: args[1].parse()?,
    })?;
    let head = state.zone_head.as_ref().ok_or("head")?;
    let source = SourceBlobStore::open_existing(&Path::new(&args[0]).join("workspace-v1"))?;
    let population = ImportedPopulation::resolve(
        head.base.vegetation.as_ref().ok_or("vegetation")?,
        |hash, size| source.read(hash, size),
    )?;
    let raster = head.base.terrain.sampled_source.as_ref().ok_or("raster")?;
    let heights = source.read(raster.samples_sha256, raster.byte_length()?)?;
    let root = Path::new(&args[2]).canonicalize()?;
    let package = PlantPackage::load(
        &fs::read(root.join("package.json"))?,
        args[3].parse()?,
        |name, limit| {
            let path = root.join(name).canonicalize().map_err(|e| e.to_string())?;
            if path.parent() != Some(root.as_path()) {
                return Err("package path escaped".into());
            }
            let mut bytes = Vec::new();
            fs::File::open(path)
                .map_err(|e| e.to_string())?
                .take(limit as u64 + 1)
                .read_to_end(&mut bytes)
                .map_err(|e| e.to_string())?;
            Ok(bytes)
        },
    )?;
    let prepared = canonical::prepare(&population, Some((head, &heights)))?;
    let ids: BTreeSet<_> = prepared.identities.values().flatten().copied().collect();
    assert_eq!(ids.len(), population.instance_count());
    for (group, ids) in &prepared.identities {
        assert_eq!(prepared.streams[group]["matrices"].len(), ids.len() * 64);
    }
    let mut edited = head.clone();
    edited.corrections.base_elevation_m =
        Some(head.effective()?.0.height_sampling.base_elevation_m + 1.0);
    let shifted = canonical::prepare(&population, Some((&edited, &heights)))?;
    assert_eq!(prepared.identities, shifted.identities);
    assert_ne!(prepared.streams, shifted.streams);
    assert_ne!(prepared.head_hash, shifted.head_hash);
    let scene = scene::from_population(&package, &population, head, &heights)?;
    let mut counts = Vec::new();
    for camera in [
        Vec3::new(42., 14., -90.),
        Vec3::new(400., 30., 400.),
        Vec3::new(42., 14., -90.),
    ] {
        let mapped = project(&scene, camera)?;
        assert!(!mapped.is_empty());
        assert!(
            mapped
                .iter()
                .all(|p| ids.contains(&p.id) && p.head_hash == prepared.head_hash)
        );
        counts.push(mapped.into_iter().map(|p| p.id).collect::<Vec<_>>());
    }
    assert_eq!(counts[0], counts[2]);
    assert_ne!(counts[0], counts[1]);
    check_resident(scene, &counts)?;
    assert_eq!(state, host.execute(AuthoringRequest::Inspect)?);
    host.execute(AuthoringRequest::Close)?;
    println!(
        "POPULATION_SCENE_PASS {} canonical identities; portable package scene; two camera selections and return stable; ground edit preserves identities and changes streams/head; source state unchanged",
        ids.len()
    );
    Ok(())
}

fn check_resident(
    scene: scene::Vegetation,
    expected: &[Vec<forge_core::EntityId>],
) -> Result<(), String> {
    let mut world = World::new();
    world.init_resource::<Assets<Mesh>>();
    world.init_resource::<Assets<PlantMaterial>>();
    world.init_resource::<Assets<ShaderBuffer>>();
    let mut resident = scene::resident::Resident::stage(&mut world, scene, &material())?;
    let handles = resident.materials();
    let sizes = |world: &World| {
        (
            world.resource::<Assets<Mesh>>().len(),
            world.resource::<Assets<PlantMaterial>>().len(),
            world.resource::<Assets<ShaderBuffer>>().len(),
        )
    };
    let original = sizes(&world);
    let mut total_retained = 0;
    for (index, camera) in [
        Vec3::new(42., 14., -90.),
        Vec3::new(400., 30., 400.),
        Vec3::new(42., 14., -90.),
    ]
    .into_iter()
    .enumerate()
    {
        let view = scene::View {
            camera,
            visibility: Visibility::Visible,
        };
        let report = resident.select(&mut world, view)?;
        total_retained += report.retained;
        let entities = resident.entities();
        let ids = entities
            .iter()
            .map(|e| world.get::<scene::ImportedPlant>(*e).expect("identity").id)
            .collect::<Vec<_>>();
        assert_eq!(ids, expected[index]);
        check_picking(&resident, &mut world, &ids)?;
        assert_eq!(sizes(&world), original);
        assert_eq!(resident.materials(), handles);
        let repeat = resident.select(&mut world, view)?;
        assert_eq!(repeat.spawned, 0);
        assert_eq!(repeat.removed, 0);
        assert_eq!(repeat.retained, entities.len());
        assert_eq!(resident.entities(), entities);
        assert!(
            resident
                .select(
                    &mut world,
                    scene::View {
                        camera: Vec3::splat(f32::NAN),
                        visibility: Visibility::Visible
                    }
                )
                .is_err()
        );
        assert_eq!(resident.entities(), entities);
        assert_eq!(sizes(&world), original);
    }
    assert!(total_retained > 0);
    resident.retire(&mut world);
    assert_eq!(
        world.query::<&scene::ImportedPlant>().iter(&world).count(),
        0
    );
    println!(
        "RESIDENT_SELECTION_PASS legacy selections identical; assets constant {original:?}; retained across changed views={total_retained}; repeat and invalid views preserve entities; retirement complete"
    );
    Ok(())
}

fn check_picking(
    resident: &scene::resident::Resident,
    world: &mut World,
    expected: &[forge_core::EntityId],
) -> Result<(), String> {
    let snapshot = resident.picking_snapshot(world)?;
    let mapped = snapshot
        .batches
        .iter()
        .flat_map(|batch| {
            batch.tags.iter().map(|tag| {
                snapshot
                    .resolve(batch.material.extension.ids.x + tag, snapshot.head)
                    .expect("valid GPU ID")
                    .expect("plant")
            })
        })
        .collect::<Vec<_>>();
    assert_eq!(mapped, expected);
    assert_eq!(snapshot.resolve(0, snapshot.head)?, None);
    assert!(snapshot.resolve(u32::MAX, snapshot.head).is_err());
    let stale = "0000000000000000000000000000000000000000000000000000000000000000"
        .parse()
        .map_err(|_| "hash")?;
    assert!(snapshot.resolve(1, stale).is_err());
    let entity = resident.entities()[0];
    let tag = world.get::<bevy::mesh::MeshTag>(entity).expect("tag").0;
    world
        .entity_mut(entity)
        .insert(bevy::mesh::MeshTag(u32::MAX));
    assert!(resident.picking_snapshot(world).is_err());
    world.entity_mut(entity).insert(bevy::mesh::MeshTag(tag));
    assert!(resident.picking_snapshot(world).is_ok());
    println!(
        "PLANT_PICKING_TABLE_PASS draws={} batches={} stale_and_corrupt_refused",
        mapped.len(),
        snapshot.batches.len()
    );
    Ok(())
}

fn project(scene: &scene::Vegetation, camera: Vec3) -> Result<Vec<scene::ImportedPlant>, String> {
    let mut world = World::new();
    let mut queue = CommandQueue::default();
    let mut commands = Commands::new(&mut queue, &world);
    let mut meshes = Assets::<Mesh>::default();
    let mut materials = Assets::<PlantMaterial>::default();
    let mut buffers = Assets::<ShaderBuffer>::default();
    let entities = scene::spawn(
        &mut commands,
        scene,
        &material(),
        scene::View {
            camera,
            visibility: Visibility::Visible,
        },
        &mut meshes,
        &mut materials,
        &mut buffers,
    )?;
    queue.apply(&mut world);
    entities
        .into_iter()
        .map(|e| {
            world
                .get::<scene::ImportedPlant>(e)
                .copied()
                .ok_or_else(|| "Missing canonical identity on plant mesh".into())
        })
        .collect()
}

fn material() -> PlantMaterial {
    PlantMaterial {
        params: Params {
            i_pos: Vec4::ZERO,
            i_dat: Vec4::ZERO,
            wind: Vec4::ZERO,
            gust_lod: Vec4::ZERO,
            sun_night: Vec4::ZERO,
            lod_end_frame: Vec4::ZERO,
        },
        detail: default(),
        leaf: default(),
        impostor_a: default(),
        impostor_b: default(),
        instances: default(),
        models: default(),
        environment: default(),
        hill_shadow: default(),
        object_shadow: default(),
        clouds: [Vec4::ZERO; 5],
        cloud_shadow: None,
        object_settings: default(),
        grass_heights: default(),
        grass_mask: default(),
        kind: 0,
    }
}
