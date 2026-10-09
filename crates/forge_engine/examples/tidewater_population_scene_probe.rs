//! Portable package + canonical population identities through real ECS projection.
use bevy::{ecs::world::CommandQueue, prelude::*, render::storage::ShaderBuffer};
use forge_assets::tidewater_plants::PlantPackage;
use forge_engine::tidewater_plants::{Params, PlantMaterial, canonical, scene};
use forge_project::{
    authoring::{AuthoringRequest, host::AuthoringHost, workspace::WorkspaceHostRequest},
    workspace::source_blobs::SourceBlobStore,
    zone_authoring::ZoneAuthoringHead,
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
    check_authored_copy(&population, &package, head, &heights, ids.len())?;
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
        "POPULATION_SCENE_PASS {} canonical identities plus one authored copy selected near its position; portable package scene; two camera selections and return stable; ground edit preserves identities and changes streams/head; source state unchanged",
        ids.len()
    );
    Ok(())
}

fn check_authored_copy(
    population: &ImportedPopulation,
    package: &PlantPackage,
    head: &ZoneAuthoringHead,
    heights: &[u8],
    original_count: usize,
) -> Result<(), Box<dyn Error>> {
    let template_layer = population.layers().first().ok_or("template layer")?;
    let template = template_layer
        .instances()
        .iter()
        .find(|entity| {
            entity.source.position_m[0].abs()
                < f64::from(population.document().extent_m.size_x_m) * 0.5 - 2.0
        })
        .ok_or("interior plant template")?;
    let catalogue = population.plant_catalogue();
    assert_eq!(catalogue.len(), population.layers().len());
    assert_eq!(
        catalogue
            .iter()
            .map(|entry| entry.source_count)
            .sum::<usize>(),
        original_count
    );
    let authored_id = forge_core::EntityId::deterministic("population-scene-authored-copy");
    let [x, y, z] = template.source.position_m;
    let catalogue_copy = population.plant_placement_at(
        &forge_project::VegetationLayerId::new(&template_layer.layer_id),
        authored_id,
        [x + 1.0, z],
        0.0,
        None,
        None,
    )?;
    let mut authored = head.clone();
    authored.corrections.imported_plant_placements = Some(vec![catalogue_copy]);
    authored.version = authored.corrections.schema_version();
    let authored_population = canonical::prepare(population, Some((&authored, heights)))?;
    assert_eq!(
        authored_population
            .identities
            .values()
            .map(Vec::len)
            .sum::<usize>(),
        original_count + 1
    );
    assert!(
        authored_population
            .identities
            .values()
            .flatten()
            .any(|id| *id == authored_id)
    );
    let authored_scene = scene::from_population(package, population, &authored, heights)?;
    let nearby = project(
        &authored_scene,
        bevy::math::DVec3::new(x + 1.0, y + 10.0, z).as_vec3(),
    )?;
    assert!(nearby.iter().any(|plant| plant.id == authored_id));
    let mut transformed = authored.clone();
    let copy = &mut transformed
        .corrections
        .imported_plant_placements
        .as_mut()
        .ok_or("authored copy absent")?[0];
    copy.yaw_degrees = Some(90.0);
    copy.scale_factor = Some(1.5);
    transformed.version = transformed.corrections.schema_version();
    assert_eq!(transformed.version, 12);
    let transformed_population = canonical::prepare(population, Some((&transformed, heights)))?;
    assert_eq!(
        transformed_population.identities,
        authored_population.identities
    );
    assert_ne!(transformed_population.streams, authored_population.streams);
    let transformed_scene = scene::from_population(package, population, &transformed, heights)?;
    let transformed_nearby = project(
        &transformed_scene,
        bevy::math::DVec3::new(x + 1.0, y + 10.0, z).as_vec3(),
    )?;
    assert!(
        transformed_nearby
            .iter()
            .any(|plant| plant.id == authored_id)
    );
    println!("AUTHORED_PLANT_APPEARANCE_PASS stable_id_changed_gpu_streams scene_resident");
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
    assert!(snapshot.visible_count() <= snapshot.identity_count());
    let drawn = snapshot
        .batches
        .iter()
        .flat_map(|batch| {
            batch
                .tags
                .iter()
                .map(|tag| batch.material.extension.ids.x + tag)
        })
        .collect::<BTreeSet<_>>();
    let hidden = (1..=u32::try_from(snapshot.identity_count()).expect("bounded population"))
        .find(|id| !drawn.contains(id))
        .expect("camera view excludes at least one imported plant");
    assert!(snapshot.resolve(hidden, snapshot.head).is_err());
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
        scene_lighting: forge_engine::tidewater_material::scene_buffer::HANDLE.clone(),
        motion: forge_engine::tidewater_plants::motion::DEFAULT_BUFFER.clone(),
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
        live_shadows: forge_engine::tidewater_object_shadow::receiver::DEFAULT_BUFFER.clone(),
        grass_heights: default(),
        grass_mask: default(),
        kind: 0,
    }
}
