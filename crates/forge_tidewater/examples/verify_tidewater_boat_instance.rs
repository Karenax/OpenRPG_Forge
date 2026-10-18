//! Actual-package CPU/Rapier proof. Test materials are never rendered and do
//! not substitute for the outstanding source-material/visual qualification.
use bevy::{
    math::{DMat4, DQuat, DVec3},
    mesh::VertexAttributeValues,
    prelude::*,
};
use bevy_rapier3d::plugin::context::RapierContextColliders;
use bevy_rapier3d::prelude::{Collider, NoUserData, RapierPhysicsPlugin, Sensor};
use forge_core::{AssetId, EntityId};
use forge_project::zone_authoring::placements::ManualPlacement;
use forge_tidewater_assets::tidewater_boat::{self as boat, BoatPackage};
use std::{
    collections::BTreeMap,
    path::{Path, PathBuf},
};
use {
    forge_engine::ManualPlacementCollisionExpected,
    forge_tidewater::{
        tidewater_boat::{
            BoatCollisionVolume, BoatPart, BoatProjectionOwner, BoatSourceNode, BoatWaterExclusion,
            PreparedBoat, ResidentBoat,
        },
        tidewater_hull_mask::HullVolume,
        tidewater_mesh::{DATA, TINT},
    },
};

fn check_source_records(
    package: &BoatPackage,
    world: &mut World,
    resident: &ResidentBoat<StandardMaterial>,
) -> Result<f64, String> {
    let root = resident.authored_roots()[0].0;
    let nodes: BTreeMap<_, _> = world
        .query::<(Entity, &BoatSourceNode, &BoatProjectionOwner)>()
        .iter(world)
        .filter(|(_, _, owner)| owner.root == root)
        .map(|(entity, node, _)| (node.0.clone(), entity))
        .collect();
    if nodes.len() != package.description().nodes.len() {
        return Err("source node IDs differ".into());
    }
    let mut maximum_error = 0.0_f64;
    for source in &package.description().nodes {
        let entity = *nodes.get(&source.id).ok_or("source node identity")?;
        let parent = source.parent.as_ref().map_or(Ok(root), |id| {
            nodes.get(id).copied().ok_or("source parent identity")
        })?;
        if world
            .get::<ChildOf>(entity)
            .is_none_or(|p| p.parent() != parent)
        {
            return Err("source parent changed".into());
        }
        let actual = world
            .get::<Transform>(entity)
            .ok_or("node transform")?
            .to_matrix()
            .to_cols_array()
            .map(f64::from);
        for (a, expected) in actual.iter().zip(source.matrix_local) {
            maximum_error = maximum_error.max((a - expected).abs());
        }
    }
    if maximum_error > 2.0e-6 {
        return Err("source node matrix error exceeds f32 tolerance".into());
    }
    check_source_colliders(package, world, &nodes)?;
    for source in &package.description().parts {
        let entity = nodes[&source.node];
        let marker = world.get::<BoatPart>(entity).ok_or("source part node")?;
        if (
            &marker.id,
            &marker.material,
            marker.cast_shadow,
            marker.receive_shadow,
            marker.render_order,
        ) != (
            &source.id,
            &source.material,
            source.cast_shadow,
            source.receive_shadow,
            source.render_order,
        ) {
            return Err("source part render policy changed".into());
        }
    }
    let hull_entity = resident.water_exclusions()[0];
    let exclusion = package.water_exclusion()?;
    let hull = world
        .get::<HullVolume>(hull_entity)
        .ok_or("source exclusion")?;
    if world
        .get::<ChildOf>(hull_entity)
        .is_none_or(|p| p.parent() != nodes[&exclusion.parent_node])
        || hull.0.fingerprint() != exclusion.fingerprint
        || Some(hull.0.boundary_edges()) != exclusion.source_boundary_edges
    {
        return Err("source water exclusion changed".into());
    }
    Ok(maximum_error)
}
fn check_source_colliders(
    package: &BoatPackage,
    world: &mut World,
    nodes: &BTreeMap<String, Entity>,
) -> Result<(), String> {
    let actual: BTreeMap<_, _> = world
        .query::<(Entity, &BoatCollisionVolume)>()
        .iter(world)
        .map(|(e, c)| (c.id.clone(), e))
        .collect();
    let source_root = package
        .description()
        .nodes
        .iter()
        .find(|n| n.parent.is_none())
        .ok_or("source root")?;
    if actual.len() != package.description().colliders.len() {
        return Err("source collider IDs differ".into());
    }
    for source in &package.description().colliders {
        let entity = *actual.get(&source.id).ok_or("source collision ID")?;
        let marker = world
            .get::<BoatCollisionVolume>(entity)
            .ok_or("source collision marker")?;
        if (&marker.tag, marker.walkable, marker.solid)
            != (&source.tag, source.walkable, source.solid)
            || world
                .get::<ChildOf>(entity)
                .is_none_or(|p| p.parent() != nodes[&source_root.id])
        {
            return Err("source collider semantics or parent changed".into());
        }
        let actual_center = world
            .get::<Transform>(entity)
            .ok_or("source collider pose")?
            .translation
            .to_array()
            .map(f64::from);
        let actual_half = world
            .get::<Collider>(entity)
            .and_then(Collider::as_cuboid)
            .ok_or("source collider shape")?
            .half_extents()
            .to_array()
            .map(f64::from);
        if actual_center
            .iter()
            .zip(source.center_m)
            .chain(actual_half.iter().zip(source.half_extents_m))
            .any(|(a, b)| (a - b).abs() > 2.0e-6)
        {
            return Err("source collider bounds changed".into());
        }
    }
    Ok(())
}

fn load(root: &std::path::Path) -> Result<BoatPackage, String> {
    let manifest = std::fs::read(root.join("manifest.json")).map_err(|e| e.to_string())?;
    BoatPackage::load(&manifest, boat::package_hash(), |name, _| {
        std::fs::read(root.join(name)).map_err(|e| e.to_string())
    })
}
fn check_source_hull_pose(
    package: &BoatPackage,
    world: &World,
    resident: &ResidentBoat<StandardMaterial>,
) -> Result<f64, String> {
    let exclusion = package.water_exclusion()?;
    let root = resident.authored_roots()[0].0;
    let hull = resident.water_exclusions()[0];
    let marker = world
        .get::<BoatWaterExclusion>(hull)
        .ok_or("exclusion marker")?;
    let owner = world
        .get::<forge_tidewater::tidewater_boat::BoatProjectionOwner>(root)
        .ok_or("source placement owner")?;
    if marker.owner != root
        || marker.asset_id != owner.asset_id
        || marker.revision != package.receipt_hash()
        || marker.placement != owner.placement
        || marker.source_node != exclusion.parent_node
        || marker.source_volume != exclusion.id
    {
        return Err("water exclusion source identity differs".into());
    }
    let frame = exclusion.local_frame;
    let local = DMat4::from_scale_rotation_translation(
        DVec3::from_array(frame.scale),
        DQuat::from_array(frame.rotation_xyzw),
        DVec3::from_array(frame.translation_m),
    );
    let actual_local = world
        .get::<Transform>(hull)
        .ok_or("hull local pose")?
        .to_matrix()
        .to_cols_array()
        .map(f64::from);
    let mut error = matrix_error(&actual_local, &local.to_cols_array());
    let mut chain = DMat4::IDENTITY;
    let mut parent = Some(exclusion.parent_node.as_str());
    while let Some(id) = parent {
        let source = package
            .description()
            .nodes
            .iter()
            .find(|n| n.id == id)
            .ok_or("source hull ancestor")?;
        chain = DMat4::from_cols_array(&source.matrix_local) * chain;
        parent = source.parent.as_deref();
    }
    let root_matrix = world
        .get::<Transform>(root)
        .ok_or("placement pose")?
        .to_matrix()
        .to_cols_array()
        .map(f64::from);
    let expected = DMat4::from_cols_array(&root_matrix) * chain * local;
    let actual_global = world
        .get::<GlobalTransform>(hull)
        .ok_or("propagated source hull")?
        .to_matrix()
        .to_cols_array()
        .map(f64::from);
    error = error.max(matrix_error(&actual_global, &expected.to_cols_array()));
    if error > 2.0e-5 {
        return Err("source hull local or propagated matrix differs".into());
    }
    Ok(error)
}
fn matrix_error(actual: &[f64; 16], expected: &[f64; 16]) -> f64 {
    actual
        .iter()
        .zip(expected)
        .map(|(a, b)| (a - b).abs())
        .fold(0.0, f64::max)
}
fn tick(app: &mut App) {
    app.update();
    app.world_mut().run_schedule(FixedUpdate);
}
fn placement(package: &BoatPackage) -> Result<ManualPlacement, String> {
    let prepared = PreparedBoat::from_package(package, boat::asset_id())?;
    Ok(ManualPlacement {
        particles: None,
        physics: None,
        id: EntityId::deterministic("boat-actual-instance-proof"),
        name: "Bateau éditable".into(),
        asset_id: boat::asset_id(),
        asset_hash: package.receipt_hash(),
        transform: prepared.canonical_transform(),
        locked: false,
    })
}
fn node(world: &mut World, name: &str) -> Entity {
    world
        .query::<(Entity, &BoatSourceNode)>()
        .iter(world)
        .find(|(_, n)| n.0 == name)
        .expect("source node")
        .0
}
fn check_bits(
    package: &BoatPackage,
    world: &World,
    resident: &ResidentBoat<StandardMaterial>,
) -> Result<usize, String> {
    let meshes = world.resource::<Assets<Mesh>>();
    for entity in resident.render_parts() {
        let source = world.get::<BoatPart>(entity).ok_or("part marker")?;
        let resolved = package.resolve_part(&source.id)?;
        let mesh = meshes
            .get(&world.get::<Mesh3d>(entity).ok_or("mesh handle")?.0)
            .ok_or("mesh")?;
        for (name, attribute) in [
            ("position", Mesh::ATTRIBUTE_POSITION),
            ("normal", Mesh::ATTRIBUTE_NORMAL),
            ("uv", Mesh::ATTRIBUTE_UV_0),
            ("color", TINT),
            ("aux", DATA),
        ] {
            let values: Vec<_> = match mesh.attribute(attribute).ok_or("attribute")? {
                VertexAttributeValues::Float32x2(values) => {
                    values.iter().flatten().copied().collect()
                }
                VertexAttributeValues::Float32x3(values) => {
                    values.iter().flatten().copied().collect()
                }
                VertexAttributeValues::Float32x4(values) => {
                    values.iter().flatten().copied().collect()
                }
                _ => return Err("unexpected attribute format".into()),
            };
            if values.len() != resolved.attributes[name].len()
                || values
                    .iter()
                    .zip(&resolved.attributes[name])
                    .any(|(a, b)| a.to_bits() != b.to_bits())
            {
                return Err(format!("source bits changed: {} {name}", source.id));
            }
        }
        let Some(bevy::mesh::Indices::U32(indices)) = mesh.indices() else {
            return Err("source indices are no longer U32".into());
        };
        if indices != &resolved.indices {
            return Err("source indices changed".into());
        }
    }
    Ok(resident.render_parts().len())
}
fn negative_staging(
    package: &BoatPackage,
    world: &mut World,
    materials: &BTreeMap<String, Handle<StandardMaterial>>,
    source: &ManualPlacement,
) -> Result<usize, String> {
    let mut wrong_asset = source.clone();
    wrong_asset.asset_id = AssetId::deterministic("wrong-boat-revision");
    let mut wrong_revision = source.clone();
    wrong_revision.asset_hash = forge_core::bytes_sha256(b"wrong");
    let mut scaled = source.clone();
    scaled.transform.scale = forge_core::Vec3::new(2.0, 2.0, 2.0);
    let cases = [
        vec![],
        vec![wrong_asset],
        vec![wrong_revision],
        vec![scaled],
        vec![source.clone(), source.clone()],
    ];
    let entities = world.entities().len();
    let meshes = world.resource::<Assets<Mesh>>().len();
    for case in &cases {
        if PreparedBoat::from_package(package, boat::asset_id())?
            .stage_authored(world, materials, case)
            .is_ok()
        {
            return Err("invalid placement was staged".into());
        }
        if world.entities().len() != entities || world.resource::<Assets<Mesh>>().len() != meshes {
            return Err("failed staging mutated the current scene".into());
        }
    }
    let mut missing = materials.clone();
    missing.remove("boat-material-hull");
    if PreparedBoat::from_package(package, boat::asset_id())?
        .stage_authored(world, &missing, std::slice::from_ref(source))
        .is_ok()
    {
        return Err("missing material was staged".into());
    }
    if world.entities().len() != entities || world.resource::<Assets<Mesh>>().len() != meshes {
        return Err("missing material allocated scene assets".into());
    }
    Ok(cases.len() + 1)
}
fn check_mutations(
    world: &mut World,
    resident: &ResidentBoat<StandardMaterial>,
) -> Result<usize, String> {
    let (root, _, _) = resident.authored_roots()[0];
    let mut rejected = 0;
    let original = *world.get::<Transform>(root).ok_or("root pose")?;
    world
        .get_mut::<Transform>(root)
        .ok_or("root pose")?
        .translation
        .x += 1.0;
    if resident.census(world).is_ok() {
        return Err("stale root accepted".into());
    }
    rejected += 1;
    world.entity_mut(root).insert(original);
    let hull = resident.water_exclusions()[0];
    let original_parent = world.get::<ChildOf>(hull).ok_or("hull parent")?.parent();
    let foreign = world.spawn(Transform::IDENTITY).id();
    world.entity_mut(hull).insert(ChildOf(foreign));
    if resident.census(world).is_ok() {
        return Err("foreign hull parent accepted".into());
    }
    rejected += 1;
    world.entity_mut(hull).insert(ChildOf(original_parent));
    let identity = world
        .get::<BoatWaterExclusion>(hull)
        .ok_or("hull identity")?
        .clone();
    world
        .get_mut::<BoatWaterExclusion>(hull)
        .ok_or("hull identity")?
        .owner = foreign;
    if resident.census(world).is_ok() {
        return Err("foreign hull owner accepted".into());
    }
    rejected += 1;
    world.entity_mut(hull).insert(identity);
    let wheel = node(world, "boat-wheel");
    let parent = world.get::<ChildOf>(wheel).ok_or("wheel parent")?.parent();
    world.entity_mut(wheel).insert(ChildOf(original_parent));
    if resident.census(world).is_ok() {
        return Err("flattened wheel pivot accepted".into());
    }
    rejected += 1;
    world.entity_mut(wheel).insert(ChildOf(parent));
    let owner = world
        .get::<BoatProjectionOwner>(wheel)
        .ok_or("wheel owner")?
        .clone();
    world
        .get_mut::<BoatProjectionOwner>(wheel)
        .ok_or("wheel owner")?
        .root = foreign;
    if resident.census(world).is_ok() {
        return Err("foreign piece owner accepted".into());
    }
    rejected += 1;
    world.entity_mut(wheel).insert(owner);
    let hull_geometry = world.get::<HullVolume>(hull).ok_or("hull")?.clone();
    let extra_hull = world
        .spawn((hull_geometry, Transform::IDENTITY, ChildOf(root)))
        .id();
    if resident.census(world).is_ok() {
        return Err("untracked extra water hull accepted".into());
    }
    rejected += 1;
    world.despawn(extra_hull);
    check_mesh_mutation(world, resident, wheel)?;
    rejected += 1;
    world.despawn(foreign);
    resident.census(world)?;
    Ok(rejected)
}
fn check_mesh_mutation(
    world: &mut World,
    resident: &ResidentBoat<StandardMaterial>,
    wheel: Entity,
) -> Result<(), String> {
    let handle = world.get::<Mesh3d>(wheel).ok_or("wheel mesh")?.0.clone();
    let backup = world
        .resource::<Assets<Mesh>>()
        .get(&handle)
        .ok_or("wheel mesh")?
        .clone();
    {
        let mut meshes = world.resource_mut::<Assets<Mesh>>();
        let mut mesh = meshes.get_mut(&handle).ok_or("wheel mesh")?;
        let Some(VertexAttributeValues::Float32x4(values)) = mesh.attribute_mut(DATA) else {
            return Err("wheel aux".into());
        };
        values[0][3] += 0.125;
    }
    if resident.census(world).is_ok() {
        return Err("mutated shader data accepted".into());
    }
    world
        .resource_mut::<Assets<Mesh>>()
        .insert(handle.id(), backup)
        .map_err(|e| e.to_string())?;
    Ok(())
}

fn check_owner_and_role_mutations(
    world: &mut World,
    resident: &ResidentBoat<StandardMaterial>,
) -> Result<usize, String> {
    let root = resident.authored_roots()[0].0;
    let root_owner = world
        .get::<BoatProjectionOwner>(root)
        .ok_or("root owner")?
        .clone();
    world
        .get_mut::<BoatProjectionOwner>(root)
        .ok_or("root owner")?
        .revision = forge_core::bytes_sha256(b"wrong-owner-revision");
    if resident.census(world).is_ok() {
        return Err("wrong root revision accepted".into());
    }
    world.entity_mut(root).insert(root_owner);
    let collider = world
        .get::<ManualPlacementCollisionExpected>(root)
        .ok_or("source colliders")?
        .primitives[0];
    let collider_owner = world
        .get::<BoatProjectionOwner>(collider)
        .ok_or("collider owner")?
        .clone();
    world
        .get_mut::<BoatProjectionOwner>(collider)
        .ok_or("collider owner")?
        .placement = None;
    if resident.census(world).is_ok() {
        return Err("foreign collider placement accepted".into());
    }
    world.entity_mut(collider).insert(collider_owner);
    world.entity_mut(collider).insert(Sensor);
    if resident.census(world).is_ok() {
        return Err("source support changed to a sensor".into());
    }
    world.entity_mut(collider).remove::<Sensor>();
    let hull = resident.water_exclusions()[0];
    let geometry = world.get::<HullVolume>(hull).ok_or("source hull")?.clone();
    world.entity_mut(root).insert(geometry);
    if resident.census(world).is_ok() {
        return Err("extra hull on an existing root accepted".into());
    }
    world.entity_mut(root).remove::<HullVolume>();
    let exclusion = world
        .get::<BoatWaterExclusion>(hull)
        .ok_or("source exclusion identity")?
        .clone();
    let wheel = node(world, "boat-wheel");
    world.entity_mut(wheel).insert(exclusion);
    if resident.census(world).is_ok() {
        return Err("extra exclusion marker on a render node accepted".into());
    }
    world.entity_mut(wheel).remove::<BoatWaterExclusion>();
    resident.census(world)?;
    Ok(5)
}
fn run(root: &Path) -> Result<serde_json::Value, String> {
    let package = load(root)?;
    let source = placement(&package)?;
    let mut app = App::new();
    app.add_plugins((
        MinimalPlugins,
        TransformPlugin,
        RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule(),
    ));
    app.insert_resource(Assets::<Mesh>::default());
    app.insert_resource(Assets::<StandardMaterial>::default());
    let materials: BTreeMap<_, _> = package
        .description()
        .parts
        .iter()
        .map(|p| p.material.clone())
        .collect::<std::collections::BTreeSet<_>>()
        .into_iter()
        .map(|id| {
            (
                id,
                app.world_mut()
                    .resource_mut::<Assets<StandardMaterial>>()
                    .add(StandardMaterial::default()),
            )
        })
        .collect();
    let negative_staging = negative_staging(&package, app.world_mut(), &materials, &source)?;
    let resident = PreparedBoat::from_package(&package, source.asset_id)?.stage_authored(
        app.world_mut(),
        &materials,
        std::slice::from_ref(&source),
    )?;
    let census = resident.census(app.world())?;
    let root_entity = resident.authored_roots()[0].0;
    if app
        .world()
        .get::<ManualPlacementCollisionExpected>(root_entity)
        .is_none_or(|e| e.primitives.len() != 23)
    {
        return Err("authored collision inventory differs".into());
    }
    if (
        census.instances,
        census.source_nodes,
        census.render_parts,
        census.collision_volumes,
        census.water_exclusions,
        census.triangles,
    ) != (1, 14, 12, 23, 1, 46_845)
    {
        return Err("source census differs".into());
    }
    let exact_parts = check_bits(&package, app.world(), &resident)?;
    if resident.is_synchronized(app.world()) {
        return Err("unregistered physics reported ready".into());
    }
    tick(&mut app);
    if !resident.is_synchronized(app.world()) {
        return Err("source physics did not synchronize".into());
    }
    let source_matrix_error = check_source_records(&package, app.world_mut(), &resident)?;
    let source_hull_error = check_source_hull_pose(&package, app.world(), &resident)?;
    let support: Vec<_> = app
        .world_mut()
        .query::<(Entity, &BoatCollisionVolume)>()
        .iter(app.world())
        .filter(|(_, c)| c.walkable && !c.solid)
        .map(|(e, _)| e)
        .collect();
    if support.len() != 1 || app.world().get::<Sensor>(support[0]).is_some() {
        return Err("walkable deck became a sensor".into());
    }
    let mut negative_live = check_mutations(app.world_mut(), &resident)?;
    negative_live += check_owner_and_role_mutations(app.world_mut(), &resident)?;
    app.world_mut()
        .entity_mut(support[0])
        .insert(forge_engine::ManualPlacementCollisionFailed(
            "proof-failure",
        ));
    if resident.is_synchronized(app.world()) {
        return Err("explicit collision failure was reported ready".into());
    }
    negative_live += 1;
    app.world_mut()
        .entity_mut(support[0])
        .remove::<forge_engine::ManualPlacementCollisionFailed>();
    tick(&mut app);
    if !resident.is_synchronized(app.world()) {
        return Err("restored source did not synchronize".into());
    }
    check_replacement(&package, &source, &mut app, resident, &materials)?;
    Ok(
        serde_json::json!({"schema":"forge.tidewater.boat-instance-proof","version":1,"package_root":root,"package_sha256":package.receipt_hash().to_string(),"census":{"instances":census.instances,"nodes":census.source_nodes,"parts":census.render_parts,"colliders":census.collision_volumes,"water_exclusions":census.water_exclusions,"triangles":census.triangles},"bit_identical_parts":exact_parts,"maximum_local_matrix_error":source_matrix_error,"maximum_source_hull_matrix_error":source_hull_error,"source_collision_records_verified":23,"negative_staging_cases":negative_staging,"negative_live_cases":negative_live,"walkable_deck_is_sensor":false,"source_hull_boundary_edges":8,"physics_registration_and_replacement":true,"placement_serialization_round_trip":true,"unrelated_content_preserved":true,"owned_meshes_after_retirement":0,"backend_colliders_after_retirement":0,"appearance_qualified":false,"gpu_executed":false,"studio_qualified":false}),
    )
}
fn check_replacement(
    package: &BoatPackage,
    source: &ManualPlacement,
    app: &mut App,
    resident: ResidentBoat<StandardMaterial>,
    materials: &BTreeMap<String, Handle<StandardMaterial>>,
) -> Result<(), String> {
    let witness = RetirementWitness::capture(app, &resident)?;
    let mut moved = source.clone();
    moved.transform.translation_m.x += 20.0;
    let rotation = Quat::from_rotation_y(0.37);
    moved.transform.rotation =
        forge_core::UnitQuaternion::new(rotation.x, rotation.y, rotation.z, rotation.w);
    let roundtripped = serde_json::from_slice::<ManualPlacement>(
        &serde_json::to_vec(&moved).map_err(|e| e.to_string())?,
    )
    .map_err(|e| e.to_string())?;
    if roundtripped != moved {
        return Err("placement serialization changed values".into());
    }
    let replacement = PreparedBoat::from_package(package, moved.asset_id)?.stage_authored(
        app.world_mut(),
        materials,
        std::slice::from_ref(&moved),
    )?;
    // New residency is constructed before retiring the current one.
    replacement.census(app.world())?;
    app.world_mut()
        .entity_mut(witness.owned_wheel)
        .insert(ChildOf(witness.unrelated));
    resident.retire(app.world_mut());
    witness.verify(app.world())?;
    tick(app);
    if !replacement.is_synchronized(app.world()) {
        return Err("replacement physics did not synchronize".into());
    }
    check_source_hull_pose(package, app.world(), &replacement)?;
    let moved_hull = replacement.water_exclusions()[0];
    let moved_hull_global = app
        .world()
        .get::<GlobalTransform>(moved_hull)
        .ok_or("moved propagated hull")?
        .to_matrix();
    if moved_hull_global == witness.old_hull_global {
        return Err("water exclusion did not follow authored placement".into());
    }
    let moved_root = replacement.authored_roots()[0].0;
    if app.world().get::<Transform>(moved_root)
        != Some(&forge_engine::domain_transform_to_bevy(moved.transform))
    {
        return Err("placement round trip changed pose".into());
    }
    check_bits(package, app.world(), &replacement)?;
    replacement.retire(app.world_mut());
    // The foreign consumer owns the adopted handle and explicitly releases it.
    app.world_mut().despawn(witness.foreign_child);
    app.world_mut()
        .resource_mut::<Assets<Mesh>>()
        .remove(witness.adopted_mesh.id());
    tick(app);
    if app
        .world_mut()
        .query::<&RapierContextColliders>()
        .iter(app.world())
        .any(|c| !c.colliders.is_empty() || !c.entity2collider().is_empty())
    {
        return Err("retired boat colliders remain in Rapier".into());
    }
    if !app.world().resource::<Assets<Mesh>>().is_empty() {
        return Err("owned mesh allocations leaked".into());
    }
    if app.world().resource::<Assets<StandardMaterial>>().len() != 7 {
        return Err("shared materials were retired".into());
    }
    Ok(())
}
struct RetirementWitness {
    owned_entities: Vec<Entity>,
    original_root: Entity,
    hull: Entity,
    old_hull_global: Mat4,
    geometry: std::sync::Arc<forge_tidewater::tidewater_hull_mask::Geometry>,
    unrelated: Entity,
    owned_wheel: Entity,
    adopted_mesh: Handle<Mesh>,
    foreign_child: Entity,
}
impl RetirementWitness {
    fn capture(app: &mut App, resident: &ResidentBoat<StandardMaterial>) -> Result<Self, String> {
        let original_root = resident.authored_roots()[0].0;
        let owned_entities = app
            .world_mut()
            .query::<(Entity, &BoatProjectionOwner)>()
            .iter(app.world())
            .filter(|(_, o)| o.root == original_root)
            .map(|(e, _)| e)
            .collect();
        let hull = resident.water_exclusions()[0];
        let old_hull_global = app
            .world()
            .get::<GlobalTransform>(hull)
            .ok_or("propagated hull")?
            .to_matrix();
        let geometry = app
            .world()
            .get::<HullVolume>(hull)
            .ok_or("hull geometry")?
            .0
            .clone();
        if geometry.boundary_edges() != 8 || geometry.verified_source().is_none() {
            return Err("source hull was capped or lost its receipt".into());
        }
        let unrelated = app
            .world_mut()
            .spawn((
                HullVolume(geometry.clone()),
                Transform::from_xyz(-100.0, 0.0, 0.0),
            ))
            .id();
        let owned_wheel = node(app.world_mut(), "boat-wheel");
        let adopted_mesh = app
            .world()
            .get::<Mesh3d>(owned_wheel)
            .ok_or("owned wheel")?
            .0
            .clone();
        let foreign_child = app
            .world_mut()
            .spawn((
                Mesh3d(adopted_mesh.clone()),
                Transform::IDENTITY,
                ChildOf(original_root),
            ))
            .id();
        Ok(Self {
            owned_entities,
            original_root,
            hull,
            old_hull_global,
            geometry,
            unrelated,
            owned_wheel,
            adopted_mesh,
            foreign_child,
        })
    }
    fn verify(&self, world: &World) -> Result<(), String> {
        if self
            .owned_entities
            .iter()
            .any(|e| world.get_entity(*e).is_ok())
        {
            return Err("retirement left an owned source entity".into());
        }
        if world.get_entity(self.original_root).is_ok()
            || world.get_entity(self.hull).is_ok()
            || world.get_entity(self.owned_wheel).is_ok()
        {
            return Err("old boat or hull survived retirement".into());
        }
        if world.get_entity(self.unrelated).is_err()
            || world.get_entity(self.foreign_child).is_err()
            || world.get::<ChildOf>(self.foreign_child).is_some()
        {
            return Err("retirement removed unrelated scene content".into());
        }
        if world.get::<HullVolume>(self.unrelated).is_none_or(|h| {
            !std::sync::Arc::ptr_eq(&h.0, &self.geometry)
                || h.0.boundary_edges() != 8
                || h.0.verified_source().is_none()
        }) || !world
            .resource::<Assets<Mesh>>()
            .contains(self.adopted_mesh.id())
        {
            return Err("retirement changed borrowed geometry".into());
        }
        Ok(())
    }
}
fn main() -> std::process::ExitCode {
    let Some(root) = std::env::args_os().nth(1) else {
        eprintln!("Usage: verify_tidewater_boat_instance PACKAGE_ROOT");
        return std::process::ExitCode::FAILURE;
    };
    match run(&PathBuf::from(root)) {
        Ok(result) => {
            println!(
                "{}",
                serde_json::to_string_pretty(&result).expect("proof JSON")
            );
            std::process::ExitCode::SUCCESS
        }
        Err(error) => {
            eprintln!("{error}");
            std::process::ExitCode::FAILURE
        }
    }
}
