//! Exercise exact catalogue joins and live roots with the real closed packages.
//! CPU/ECS proof only: no meshes, renderer, GPU or Studio interaction.
use bevy::prelude::*;
use forge_assets::{
    source_local_lights::{SourceLocalLightsPackage, ownership::SourceLightOwnership},
    tidewater_package::BuildingPackage,
};
use forge_core::{ContentHash, EntityId, Transform as Pose, UnitQuaternion, Vec3 as Vector};
use forge_engine::{
    ManualPlacementProjection,
    source_local_lights::scene,
    tidewater_assets::{
        local_lights::BuildingLightBindings,
        scene::{Instance, ResidentScene},
    },
};
use forge_project::zone_authoring::placements::ManualPlacement;
use serde::Deserialize;
use std::{
    collections::BTreeSet,
    error::Error,
    fs::File,
    io::{Read, Write},
    path::Path,
};

fn bounded(path: &Path, maximum: usize) -> Result<Vec<u8>, Box<dyn Error>> {
    let mut bytes = Vec::new();
    File::open(path)?
        .take(
            u64::try_from(maximum)?
                .checked_add(1)
                .ok_or("limit overflow")?,
        )
        .read_to_end(&mut bytes)?;
    if bytes.len() > maximum {
        return Err("Package member exceeds limit".into());
    }
    Ok(bytes)
}
fn read(root: &Path, name: &str, limit: usize) -> Result<Vec<u8>, String> {
    let path = root
        .join(name)
        .canonicalize()
        .map_err(|error| error.to_string())?;
    if !path.starts_with(root) {
        return Err("Package member leaves selected root".into());
    }
    bounded(&path, limit).map_err(|error| error.to_string())
}
fn require(value: bool, message: &str) -> Result<(), Box<dyn Error>> {
    if value { Ok(()) } else { Err(message.into()) }
}
#[derive(Deserialize)]
struct Descriptor {
    building: Building,
}
#[derive(Deserialize)]
struct Building {
    source_frame: Frame,
}
#[derive(Deserialize)]
struct Frame {
    translation_m: [f64; 3],
    rotation_xyzw: [f64; 4],
    scale: [f64; 3],
}
#[allow(
    clippy::cast_possible_truncation,
    reason = "The domain uses f32; input bounds are checked and the live pose error is reported."
)]
fn domain_pose(frame: &Frame) -> Result<Pose, Box<dyn Error>> {
    require(
        frame
            .translation_m
            .iter()
            .chain(&frame.rotation_xyzw)
            .chain(&frame.scale)
            .all(|value| value.is_finite() && value.abs() <= f64::from(f32::MAX)),
        "Pose exceeds domain float range",
    )?;
    let [x, y, z] = frame.translation_m.map(|value| value as f32);
    let [qx, qy, qz, qw] = frame.rotation_xyzw.map(|value| value as f32);
    let [sx, sy, sz] = frame.scale.map(|value| value as f32);
    Ok(Pose {
        translation_m: Vector::new(x, y, z),
        rotation: UnitQuaternion::new(qx, qy, qz, qw),
        scale: Vector::new(sx, sy, sz),
    })
}
fn root(world: &mut World, placement: &ManualPlacement) -> Entity {
    let entity = world
        .spawn((
            ManualPlacementProjection(placement.clone()),
            forge_engine::domain_transform_to_bevy(placement.transform),
        ))
        .id();
    world
        .resource_mut::<ResidentScene>()
        .instances
        .push(Instance {
            source: placement.clone(),
            root: entity,
            parts: Vec::new(),
        });
    entity
}

#[allow(
    clippy::too_many_lines,
    reason = "Ordered real-package lifecycle proof preserves refusal checks and their scene state in one sequence."
)]
fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    require(
        args.len() == 5,
        "Usage: verify_building_source_lights BUILDING_ROOT BUILDING_SHA LIGHT_ROOT LIGHT_SHA FRESH_REPORT",
    )?;
    let building_root = Path::new(&args[0]).canonicalize()?;
    let building_pin: ContentHash = args[1].parse()?;
    let light_root = Path::new(&args[2]).canonicalize()?;
    let light_pin: ContentHash = args[3].parse()?;
    require(!Path::new(&args[4]).exists(), "Fresh report required")?;
    let buildings = BuildingPackage::load(
        &read(&building_root, "package.json", 2 * 1024 * 1024)?,
        building_pin,
        |name, limit| read(&building_root, name, limit),
    )?;
    let lights = SourceLocalLightsPackage::load(
        &read(&light_root, "manifest.json", 2 * 1024 * 1024)?,
        light_pin,
        |name, limit| read(&light_root, name, limit),
    )?;
    let ownership = SourceLightOwnership::from_package(&lights)?;
    let bindings = BuildingLightBindings::from_packages(&buildings, &lights)?;
    let mut app = App::new();
    app.add_plugins((MinimalPlugins, TransformPlugin));
    app.insert_resource(ResidentScene::default());
    let camera = app
        .world_mut()
        .spawn((Camera3d::default(), Transform::IDENTITY))
        .id();
    let mut plans = Vec::new();
    let mut unresolved = Vec::new();
    let mut attested_empty = Vec::new();
    for asset in &buildings.catalog().assets {
        let bytes = buildings
            .file(&format!("{}.json", asset.content_hash))
            .ok_or("Building descriptor absent")?;
        let description: Descriptor = serde_json::from_slice(bytes)?;
        let frame = description.building.source_frame;
        let placement = ManualPlacement {
            id: EntityId::deterministic(format!("building-light-proof:{}", asset.id)),
            name: asset.logical_name.clone(),
            asset_id: asset.id,
            asset_hash: asset.content_hash,
            locked: false,
            transform: domain_pose(&frame)?,
        };
        if ownership
            .selection_for_source_object(&asset.logical_name)
            .is_none()
        {
            if matches!(bindings.prepare_optional(&lights, &placement, 0), Ok(None)) {
                root(app.world_mut(), &placement);
                attested_empty.push(asset.logical_name.clone());
                continue;
            }
            require(
                bindings.prepare(&lights, &placement, 0).is_err(),
                "Missing identity silently became zero emitters",
            )?;
            unresolved.push(asset.logical_name.clone());
            continue;
        }
        let prepared = bindings.prepare(&lights, &placement, 0)?;
        let entity = root(app.world_mut(), &placement);
        plans.push((placement, entity, prepared));
    }
    require(
        plans.len() == 19 && unresolved.len() + attested_empty.len() == 4,
        "Real catalogue coverage differs",
    )?;
    app.update();
    let (first, first_root, first_plan) = plans.first().ok_or("No luminous building")?;
    let carrier = app
        .world_mut()
        .spawn(Transform::from_xyz(100.0, 0.0, 0.0))
        .id();
    app.world_mut()
        .entity_mut(*first_root)
        .insert(ChildOf(carrier));
    app.update();
    let before = app.world().entities().len();
    require(
        first_plan.stage(app.world_mut()).is_err() && app.world().entities().len() == before,
        "Parented root accepted or failed non-atomically",
    )?;
    app.world_mut().entity_mut(*first_root).remove::<ChildOf>();
    let duplicate = app
        .world_mut()
        .spawn((
            ManualPlacementProjection(first.clone()),
            forge_engine::domain_transform_to_bevy(first.transform),
        ))
        .id();
    app.update();
    let before = app.world().entities().len();
    require(
        first_plan.stage(app.world_mut()).is_err() && app.world().entities().len() == before,
        "Duplicate canonical root accepted",
    )?;
    app.world_mut().despawn(duplicate);
    app.update();
    let mut residents = Vec::new();
    let mut orders = Vec::new();
    for (_, _, prepared) in &plans {
        orders.extend_from_slice(prepared.registration_orders());
        residents.push(prepared.stage(app.world_mut())?);
    }
    orders.sort_unstable();
    require(orders.len() == 32, "Building emission count differs")?;
    app.update();
    require(
        residents
            .iter()
            .all(|resident| resident.is_synchronized(app.world())),
        "Building projection not synchronized",
    )?;
    let (_, harvested) = scene::harvest(app.world_mut(), camera, &orders)?;
    let selected: BTreeSet<_> = orders.iter().copied().collect();
    let expected: Vec<_> = lights
        .descriptor()
        .document()
        .lights
        .iter()
        .filter(|light| selected.contains(&light.registration_order))
        .collect();
    let mut pose_error = 0.0_f64;
    for (actual, expected) in harvested.iter().zip(&expected) {
        require(
            actual.phase.to_bits() == expected.phase.to_bits(),
            "Captured phase changed",
        )?;
        for (actual, expected) in actual
            .position
            .to_array()
            .into_iter()
            .zip(expected.acquired_position_world_m)
        {
            pose_error = pose_error.max((actual - expected).abs());
        }
    }
    require(
        pose_error < 0.0001,
        "Live f32 placement pose exceeds 0.1 mm",
    )?;
    let mut copy = first.clone();
    copy.id = EntityId::deterministic("building-light-proof:copy");
    copy.transform.translation_m.x += 7.0;
    let copy_root = root(app.world_mut(), &copy);
    app.update();
    let copy_plan = bindings.prepare(&lights, &copy, 100)?;
    let mut copy_resident = copy_plan.stage(app.world_mut())?;
    orders.extend_from_slice(copy_plan.registration_orders());
    orders.sort_unstable();
    app.update();
    let (_, copied) = scene::harvest(app.world_mut(), camera, &orders)?;
    for (index, order) in first_plan.registration_orders().iter().enumerate() {
        let original = copied
            .get(
                orders
                    .binary_search(order)
                    .map_err(|_| "Original order absent")?,
            )
            .ok_or("Original light absent")?;
        let copied_light = copied
            .get(
                orders
                    .binary_search(&copy_plan.registration_orders()[index])
                    .map_err(|_| "Copy order absent")?,
            )
            .ok_or("Copy light absent")?;
        require(
            (copied_light.position.x - original.position.x - 7.0).abs() < 0.0001
                && copied_light.phase.to_bits() == original.phase.to_bits(),
            "Copy transform or phase differs",
        )?;
    }
    let before = app.world().entities().len();
    require(
        first_plan
            .replace(app.world_mut(), &mut copy_resident)
            .is_err()
            && app.world().entities().len() == before
            && copy_resident.is_synchronized(app.world()),
        "Replacement accepted another canonical instance or mutated its copy",
    )?;
    copy.transform.translation_m.x += 3.0;
    copy.transform.translation_m.y += 2.0;
    {
        let mut scene = app.world_mut().resource_mut::<ResidentScene>();
        scene
            .instances
            .iter_mut()
            .find(|instance| instance.source.id == copy.id)
            .ok_or("Canonical copy absent")?
            .source
            .clone_from(&copy);
    }
    app.world_mut().entity_mut(copy_root).insert((
        ManualPlacementProjection(copy.clone()),
        forge_engine::domain_transform_to_bevy(copy.transform),
    ));
    app.update();
    let before = app.world().entities().len();
    require(
        copy_plan
            .replace(app.world_mut(), &mut copy_resident)
            .is_err()
            && app.world().entities().len() == before,
        "Stale canonical edit accepted or mutated emitters",
    )?;
    let edited_plan = bindings.prepare(&lights, &copy, 100)?;
    edited_plan.replace(app.world_mut(), &mut copy_resident)?;
    app.update();
    require(
        copy_resident.is_synchronized(app.world()),
        "Edited copy not synchronized",
    )?;
    let (_, edited) = scene::harvest(app.world_mut(), camera, &orders)?;
    for order in copy_plan.registration_orders() {
        let index = orders
            .binary_search(order)
            .map_err(|_| "Copy order absent")?;
        let old = &copied[index];
        let next = &edited[index];
        require(
            (next.position - old.position - bevy::math::DVec3::new(3.0, 2.0, 0.0)).length()
                < 0.0001
                && next.phase.to_bits() == old.phase.to_bits(),
            "Canonical copy edit changed lamp phase or displacement",
        )?;
    }
    residents[0].retire(app.world_mut());
    app.world_mut()
        .resource_mut::<ResidentScene>()
        .instances
        .retain(|instance| instance.source.id != first.id);
    app.world_mut().despawn(*first_root);
    orders.retain(|order| !first_plan.registration_orders().contains(order));
    app.update();
    require(
        copy_resident.is_synchronized(app.world())
            && scene::harvest(app.world_mut(), camera, &orders).is_ok(),
        "Original removal damaged copy",
    )?;
    copy_resident.retire(app.world_mut());
    require(
        app.world().get_entity(copy_root).is_ok(),
        "Light retirement removed placement",
    )?;
    let report = serde_json::json!({"schema":"forge.building-source-light-ecs-proof","version":1,"building_package_sha256":building_pin,"light_package_sha256":light_pin,"luminous_buildings":plans.len(),"source_lights":32,"unresolved_asset_identities":unresolved,"attested_empty_building_identities":attested_empty,"pose_max_error_m":pose_error,"phase_bits_preserved":true,"parented_root_refused":true,"duplicate_root_refused":true,"copy_and_original_removal_verified":true,"canonical_edit_replacement_verified":true,"cross_instance_replacement_refused":true,"stale_canonical_edit_refused":true,"gpu_executed":false,"geometry_projected":false,"studio_projection_executed":false,"renderer_parity":false});
    File::create_new(&args[4])?.write_all(&serde_json::to_vec_pretty(&report)?)?;
    println!("{}", serde_json::to_string(&report)?);
    Ok(())
}
