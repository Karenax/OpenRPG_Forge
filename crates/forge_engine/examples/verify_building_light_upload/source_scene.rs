//! Real closed building/light packages projected onto canonical live roots.
//! Used by the headless upload proof; it deliberately does not draw geometry.
use super::Proof;
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
use std::{error::Error, fs::File, io::Read, path::Path};

fn read(root: &Path, name: &str, limit: usize) -> Result<Vec<u8>, String> {
    let path = root.join(name).canonicalize().map_err(|e| e.to_string())?;
    if !path.starts_with(root) {
        return Err("Package member leaves selected root".into());
    }
    let maximum = u64::try_from(limit)
        .map_err(|e| e.to_string())?
        .checked_add(1)
        .ok_or("Package limit overflow")?;
    let mut bytes = Vec::new();
    File::open(path)
        .map_err(|e| e.to_string())?
        .take(maximum)
        .read_to_end(&mut bytes)
        .map_err(|e| e.to_string())?;
    if bytes.len() > limit {
        return Err("Package member exceeds limit".into());
    }
    Ok(bytes)
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
    reason = "Validated source poses are bounded to the f32 domain before projection."
)]
fn pose(frame: &Frame) -> Result<Pose, Box<dyn Error>> {
    if !frame
        .translation_m
        .iter()
        .chain(&frame.rotation_xyzw)
        .chain(&frame.scale)
        .all(|v| v.is_finite() && v.abs() <= f64::from(f32::MAX))
    {
        return Err("Building pose exceeds domain float range".into());
    }
    let [x, y, z] = frame.translation_m.map(|v| v as f32);
    let [qx, qy, qz, qw] = frame.rotation_xyzw.map(|v| v as f32);
    let [sx, sy, sz] = frame.scale.map(|v| v as f32);
    Ok(Pose {
        translation_m: Vector::new(x, y, z),
        rotation: UnitQuaternion::new(qx, qy, qz, qw),
        scale: Vector::new(sx, sy, sz),
    })
}

fn stage_root(app: &mut App, placement: ManualPlacement) {
    let root = app
        .world_mut()
        .spawn((
            ManualPlacementProjection(placement.clone()),
            forge_engine::domain_transform_to_bevy(placement.transform),
        ))
        .id();
    app.world_mut()
        .resource_mut::<ResidentScene>()
        .instances
        .push(Instance {
            source: placement,
            root,
            parts: Vec::new(),
        });
}

pub(super) fn install(app: &mut App, args: &[String]) -> Result<serde_json::Value, Box<dyn Error>> {
    let building_root = Path::new(&args[0]).canonicalize()?;
    let building_pin: ContentHash = args[1].parse()?;
    let light_root = Path::new(&args[2]).canonicalize()?;
    let light_pin: ContentHash = args[3].parse()?;
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
    app.insert_resource(ResidentScene::default());
    let mut plans = Vec::new();
    let mut unresolved = Vec::new();
    let mut attested_empty = Vec::new();
    for asset in &buildings.catalog().assets {
        let descriptor: Descriptor = serde_json::from_slice(
            buildings
                .file(&format!("{}.json", asset.content_hash))
                .ok_or("Building descriptor absent")?,
        )?;
        let placement = ManualPlacement {
            physics: None,
            id: EntityId::deterministic(format!("building-upload-proof:{}", asset.id)),
            name: asset.logical_name.clone(),
            asset_id: asset.id,
            asset_hash: asset.content_hash,
            locked: false,
            transform: pose(&descriptor.building.source_frame)?,
        };
        if ownership
            .selection_for_source_object(&asset.logical_name)
            .is_none()
        {
            if matches!(bindings.prepare_optional(&lights, &placement, 0), Ok(None)) {
                stage_root(app, placement);
                attested_empty.push(asset.logical_name.clone());
                continue;
            }
            if bindings.prepare(&lights, &placement, 0).is_ok() {
                return Err("Missing source identity was accepted".into());
            }
            unresolved.push(asset.logical_name.clone());
            continue;
        }
        let plan = bindings.prepare(&lights, &placement, 0)?;
        stage_root(app, placement);
        plans.push(plan);
    }
    if plans.len() != 19 || unresolved.len() + attested_empty.len() != 4 {
        return Err("Pinned building coverage differs".into());
    }
    app.update(); // Propagate the canonical roots before staging their light children.
    let mut residents = Vec::new();
    let mut orders = Vec::new();
    for plan in &plans {
        orders.extend_from_slice(plan.registration_orders());
        residents.push(plan.stage(app.world_mut())?);
    }
    orders.sort_unstable();
    if orders.len() != 32 || orders.windows(2).any(|p| p[0] == p[1]) {
        return Err("Pinned lamp inventory differs".into());
    }
    app.world_mut()
        .resource_mut::<Proof>()
        .orders
        .clone_from(&orders);
    app.update(); // Propagate the live source children before harvesting.
    if !residents.iter().all(|r| r.is_synchronized(app.world())) {
        return Err("Building lamps not synchronized".into());
    }
    let camera = app.world().resource::<Proof>().camera;
    let (_, harvested) = scene::harvest(app.world_mut(), camera, &orders)?;
    let first = harvested.first().ok_or("No source lamps")?;
    let position = first.position.as_vec3();
    if !position.is_finite() {
        return Err("Source lamp camera pose exceeds f32".into());
    }
    *app.world_mut()
        .get_mut::<Transform>(camera)
        .ok_or("Camera transform absent")? = Transform::from_translation(position);
    Ok(
        serde_json::json!({"building_package_sha256":building_pin,"light_package_sha256":light_pin,
        "source_commit":buildings.source_commit(),"canonical_building_roots":plans.len()+attested_empty.len(),
        "luminous_building_roots":plans.len(),"attested_empty_buildings":attested_empty,
        "source_lamps":orders.len(),"unresolved_buildings":unresolved,"geometry_draw_executed":false}),
    )
}
