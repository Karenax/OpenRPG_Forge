//! Derived viewport projection for the verified Tidewater `FishStand` composite.
//! The canonical placement remains a `ManualPlacement`; resident GPU resources
//! are retained by the viewport and are never rebuilt by transform edits.
use super::{imported_buildings, placements};
use bevy::{
    asset::RenderAssetUsages,
    ecs as bevy_ecs,
    gltf::GltfAssetLabel,
    mesh::Indices,
    prelude::*,
    render::render_resource::PrimitiveTopology,
    world_serialization::{WorldAsset, WorldAssetRoot, WorldInstanceReady},
};
use forge_engine::{
    ManualPlacementPart, ManualPlacementProjection,
    tidewater_scene_environment::ResidentEnvironment,
    tidewater_stall::{FishStandMaterial, FishStandResident},
};
use forge_project::zone_authoring::placements::ManualPlacement;
use std::{cell::RefCell, collections::BTreeMap};

thread_local! {
    static READY: RefCell<Result<Vec<ManualPlacement>, String>> = const { RefCell::new(Ok(Vec::new())) };
    static FOCUS: std::cell::Cell<Option<forge_core::EntityId>> = const { std::cell::Cell::new(None) };
}

#[derive(Resource, Default)]
struct State {
    resident: Option<FishStandResident>,
    material: Option<Handle<FishStandMaterial>>,
    lighting_key: Option<AssetId<Image>>,
    joe_path: Option<String>,
    joe: Option<Handle<WorldAsset>>,
    fish: Vec<(Handle<Mesh>, Handle<StandardMaterial>)>,
    objects: BTreeMap<forge_core::EntityId, Object>,
}

struct Object {
    source: ManualPlacement,
    root: Entity,
}

#[derive(Component)]
struct JoePart(Option<forge_core::EntityId>);
#[derive(Component)]
struct JoeReady;

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy observer triggers are delivered by value to the observer callback"
)]
fn tag_joe(
    ready: On<WorldInstanceReady>,
    mut commands: Commands,
    roots: Query<&JoePart>,
    children: Query<&Children>,
) {
    let Ok(part) = roots.get(ready.entity) else {
        return;
    };
    commands.entity(ready.entity).insert(JoeReady);
    if let Some(id) = part.0 {
        commands
            .entity(ready.entity)
            .insert(ManualPlacementPart(id));
        for child in children.iter_descendants(ready.entity) {
            commands.entity(child).insert(ManualPlacementPart(id));
        }
    }
}

fn fish_mesh(
    fish: &forge_assets::tidewater_fish_stand::FishStandFish,
    payloads: &std::collections::BTreeMap<String, Vec<u8>>,
) -> Result<Mesh, String> {
    let floats = |key: &str, width: usize| -> Result<Vec<Vec<f32>>, String> {
        let bytes = payloads.get(key).ok_or("Fish payload absent")?;
        if bytes.len() != fish.vertices * width * 4 {
            return Err("Fish payload shape invalid".into());
        }
        Ok(bytes
            .chunks_exact(width * 4)
            .map(|row| {
                row.as_chunks::<4>()
                    .0
                    .iter()
                    .map(|v| f32::from_le_bytes([v[0], v[1], v[2], v[3]]))
                    .collect()
            })
            .collect())
    };
    let position = floats(&fish.position_key, 3)?;
    let normal = floats(&fish.normal_key, 3)?;
    let indices = payloads
        .get(&fish.indices_key)
        .ok_or("Fish indices absent")?;
    if indices.len() != fish.triangles * 3 * 4 {
        return Err("Fish indices shape invalid".into());
    }
    let indices = indices
        .as_chunks::<4>()
        .0
        .iter()
        .map(|v| u32::from_le_bytes([v[0], v[1], v[2], v[3]]))
        .collect::<Vec<_>>();
    if position.len() != normal.len() || indices.iter().any(|i| *i as usize >= position.len()) {
        return Err("Fish geometry bounds invalid".into());
    }
    Ok(Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    )
    .with_inserted_attribute(
        Mesh::ATTRIBUTE_POSITION,
        position
            .into_iter()
            .map(|v| [v[0], v[1], v[2]])
            .collect::<Vec<_>>(),
    )
    .with_inserted_attribute(
        Mesh::ATTRIBUTE_NORMAL,
        normal
            .into_iter()
            .map(|v| [v[0], v[1], v[2]])
            .collect::<Vec<_>>(),
    )
    .with_inserted_indices(Indices::U32(indices)))
}

fn fish_pose(pose: &str) -> Quat {
    let basis = match pose {
        "side" => Mat3::from_cols(Vec3::Y, Vec3::Z, Vec3::X),
        "sideFlip" => Mat3::from_cols(-Vec3::Y, -Vec3::Z, Vec3::X),
        _ => Mat3::IDENTITY,
    };
    Quat::from_mat3(&basis)
}

fn desired() -> Vec<ManualPlacement> {
    placements::desired_fish_stands()
}

pub(super) fn focus(id: forge_core::EntityId) -> bool {
    let selected = desired().iter().any(|placement| placement.id == id);
    if selected {
        FOCUS.with(|slot| slot.set(Some(id)));
    }
    selected
}

pub(super) fn ready() -> Result<bool, String> {
    let desired = desired();
    READY.with(|slot| match &*slot.borrow() {
        Err(error) => Err(error.clone()),
        Ok(actual) => Ok(*actual == desired),
    })
}

pub(super) fn install(app: &mut App) -> Result<(), String> {
    app.add_plugins(MaterialPlugin::<FishStandMaterial>::default())
        .init_resource::<State>()
        .add_systems(
            PostUpdate,
            update.after(crate::authoring_assets::imported_scene::update),
        );
    forge_engine::tidewater_stall::install_shader(app)
}

fn clear(world: &mut World, state: &mut State) {
    for object in std::mem::take(&mut state.objects).into_values() {
        world.despawn(object.root);
    }
}

fn purge(world: &mut World, state: &mut State) {
    clear(world, state);
    if let Some(path) = state.joe_path.take() {
        super::preview::remove(&path);
    }
    state.joe = None;
    state.fish.clear();
    if let Some(handle) = state.material.take() {
        world
            .resource_mut::<Assets<FishStandMaterial>>()
            .remove(handle.id());
    }
    if let Some(resident) = state.resident.take() {
        world
            .resource_mut::<Assets<Mesh>>()
            .remove(resident.mesh.id());
        let mut images = world.resource_mut::<Assets<Image>>();
        for texture in resident.textures.into_values() {
            images.remove(texture.id());
        }
    }
    state.lighting_key = None;
}

fn stage(world: &mut World, state: &mut State) -> Result<(), String> {
    if imported_buildings::fish_stand_reference().is_none() {
        purge(world, state);
        let _ = READY.with(|slot| slot.replace(Ok(Vec::new())));
        return Ok(());
    }
    if desired().is_empty() {
        clear(world, state);
        let _ = READY.with(|slot| slot.replace(Ok(Vec::new())));
        return Ok(());
    }
    if let Some(prepared) = imported_buildings::take_fish_stand() {
        purge(world, state);
        state.resident = Some(
            world.resource_scope(|world, mut meshes: Mut<Assets<Mesh>>| {
                world.resource_scope(|_, mut images: Mut<Assets<Image>>| {
                    prepared.stage(&mut meshes, &mut images)
                })
            }),
        );
        state.material = None;
        state.lighting_key = None;
        clear(world, state);
    }
    let Some(resident) = state.resident.as_ref() else {
        return if desired().is_empty() {
            Ok(())
        } else {
            Err("Le bundle FishStand 011 vérifié doit être chargé avant sa projection".into())
        };
    };
    let Some(environment) = world.get_resource::<ResidentEnvironment>() else {
        return Ok(());
    };
    let mesh = resident.mesh.clone();
    let lighting_key = environment.environment.id();
    if state.material.is_none() || state.lighting_key != Some(lighting_key) {
        let material =
            FishStandMaterial::from_resident(resident, environment.scene_lighting.clone())?;
        if let Some(handle) = state.material.as_ref() {
            let mut materials = world.resource_mut::<Assets<FishStandMaterial>>();
            let mut existing = materials
                .get_mut(handle)
                .ok_or("FishStand material unavailable")?;
            *existing = material;
        } else {
            state.material = Some(
                world
                    .resource_mut::<Assets<FishStandMaterial>>()
                    .add(material),
            );
        }
        state.lighting_key = Some(lighting_key);
    }
    let handle = state
        .material
        .clone()
        .ok_or("FishStand material unavailable")?;
    synchronize(world, state, &mesh, &handle)
}

fn synchronize(
    world: &mut World,
    state: &mut State,
    mesh: &Handle<Mesh>,
    material: &Handle<FishStandMaterial>,
) -> Result<(), String> {
    let authored = desired();
    let desired = authored.clone();
    let reference = imported_buildings::fish_stand_reference().ok_or("FishStand non vérifié")?;
    let package = imported_buildings::fish_stand_package().ok_or("FishStand non vérifié")?;
    if desired.iter().any(|placement| {
        placement.asset_id != reference.asset_id || placement.asset_hash != reference.bundle_hash
    }) {
        return Err("Le placement FishStand ne correspond pas au bundle vérifié".into());
    }
    prepare_fish_assets(world, state, &reference, &package)?;
    state.objects.retain(|id, object| {
        let keep = desired.iter().any(|placement| placement.id == *id);
        if !keep {
            world.despawn(object.root);
        }
        keep
    });
    for source in desired.iter().cloned() {
        if let Some(object) = state.objects.get_mut(&source.id) {
            if object.source != source {
                world
                    .entity_mut(object.root)
                    .insert(forge_engine::domain_transform_to_bevy(source.transform));
                world.entity_mut(object.root).insert((
                    placements::gizmo::PlacementRoot(source.id, source.transform),
                    ManualPlacementProjection(source.clone()),
                    Name::new(format!("Placement {} · {}", source.id, source.name)),
                ));
                object.source = source;
            }
            continue;
        }
        let root = spawn_fish_stand(world, state, &source, mesh, material, &package)?;
        state.objects.insert(source.id, Object { source, root });
    }
    let joe_ready = world
        .query_filtered::<Entity, With<JoeReady>>()
        .iter(world)
        .count()
        >= desired.len();
    let colliders_ready = desired.iter().all(|placement| {
        state.objects.get(&placement.id).is_some_and(|object| {
            matches!(
                world.get::<forge_engine::ManualPlacementCollisionStatus>(object.root),
                Some(forge_engine::ManualPlacementCollisionStatus::Registered { primitives: 5 })
            )
        })
    });
    let projected = joe_ready && colliders_ready;
    let _ = READY.with(|slot| slot.replace(Ok(if projected { authored } else { Vec::new() })));
    Ok(())
}

fn prepare_fish_assets(
    world: &mut World,
    state: &mut State,
    reference: &forge_assets::tidewater_fish_stand::FishStandReference,
    package: &forge_assets::tidewater_fish_stand::FishStandPackage,
) -> Result<(), String> {
    if state.joe.is_none() {
        let path = format!("fish-stand-{}-joe.glb", reference.bundle_hash);
        super::preview::insert(&path, package.joe_glb().to_vec());
        state.joe = Some(
            world
                .resource::<AssetServer>()
                .load(GltfAssetLabel::Scene(0).from_asset(format!("authoring-preview://{path}"))),
        );
        state.joe_path = Some(path);
    }
    if state.joe.as_ref().is_some_and(|handle| {
        world
            .resource::<AssetServer>()
            .load_state(handle.id())
            .is_failed()
    }) {
        return Err("Chargement du GLB Joe échoué".into());
    }
    if state.fish.is_empty() {
        for fish in package.fish() {
            let mesh = world
                .resource_mut::<Assets<Mesh>>()
                .add(fish_mesh(fish, package.fish_payloads())?);
            let material = world
                .resource_mut::<Assets<StandardMaterial>>()
                .add(StandardMaterial {
                    base_color: Color::srgb(0.56, 0.58, 0.55),
                    metallic: 0.15,
                    perceptual_roughness: 0.32,
                    ..default()
                });
            state.fish.push((mesh, material));
        }
    }
    Ok(())
}

fn spawn_fish_stand(
    world: &mut World,
    state: &State,
    source: &ManualPlacement,
    mesh: &Handle<Mesh>,
    material: &Handle<FishStandMaterial>,
    package: &forge_assets::tidewater_fish_stand::FishStandPackage,
) -> Result<Entity, String> {
    let joe = state.joe.clone().ok_or("Joe GLB unavailable")?;
    let fish_handles = state
        .fish
        .get(..package.fish().len())
        .ok_or("Fish cache unavailable")?
        .to_vec();
    let fish_layers = package
        .fish()
        .iter()
        .enumerate()
        .map(|(index, _)| u16::try_from(index).map_err(|_| "Fish index too large".to_owned()))
        .collect::<Result<Vec<_>, _>>()?;

    let root = world
        .spawn((
            Name::new(format!("Placement {} · {}", source.id, source.name)),
            forge_engine::domain_transform_to_bevy(source.transform),
            Visibility::Inherited,
        ))
        .id();
    world.entity_mut(root).insert((
        placements::gizmo::PlacementRoot(source.id, source.transform),
        ManualPlacementProjection(source.clone()),
    ));
    let mut stall = world.spawn((
        Mesh3d(mesh.clone()),
        MeshMaterial3d(material.clone()),
        Transform::IDENTITY,
        Visibility::Inherited,
        ChildOf(root),
    ));
    stall.insert(ManualPlacementPart(source.id));
    let primitives = forge_engine::fish_stand_colliders()
        .into_iter()
        .map(|(transform, collider)| {
            world
                .spawn((
                    transform,
                    collider,
                    forge_engine::ManualPlacementCollision(source.id),
                    ChildOf(root),
                ))
                .id()
        })
        .collect();
    world
        .entity_mut(root)
        .insert(forge_engine::ManualPlacementCollisionExpected {
            placement: source.id,
            primitives,
        });
    let _ = world
        .spawn((
            WorldAssetRoot(joe),
            Transform::from_xyz(0.2, 0.06, 0.12),
            Visibility::Inherited,
            JoePart(Some(source.id)),
            ChildOf(root),
        ))
        .observe(tag_joe)
        .id();
    for ((fish, (mesh, material)), layer) in
        package.fish().iter().zip(fish_handles).zip(fish_layers)
    {
        let pose = Quat::from_rotation_y(fish.local_yaw_radians) * fish_pose(&fish.pose);
        let mut entity = world.spawn((
            Mesh3d(mesh),
            MeshMaterial3d(material),
            Transform::from_xyz(
                fish.local_x,
                1.27 + fish.rest_height_meters + 0.006 * f32::from(layer),
                fish.local_z,
            )
            .with_rotation(pose)
            .with_scale(Vec3::splat(fish.length_meters)),
            Visibility::Inherited,
            ChildOf(root),
        ));
        entity.insert(ManualPlacementPart(source.id));
    }
    Ok(root)
}

pub(crate) fn update(world: &mut World) {
    if !crate::authoring_viewport::can_project_sources() {
        let _ = READY.with(|slot| slot.replace(Ok(Vec::new())));
        return;
    }
    world.resource_scope(|world, mut state: Mut<State>| {
        if let Err(error) = stage(world, &mut state) {
            clear(world, &mut state);
            let _ = READY.with(|slot| slot.replace(Err(error.clone())));
            placements::report(format!("FishStand non projeté : {error}"));
        }
    });
    let Some(id) = FOCUS.with(std::cell::Cell::take) else {
        return;
    };
    let root = world
        .get_resource::<State>()
        .and_then(|state| state.objects.get(&id))
        .map(|object| object.root);
    if let Some(root) = root {
        let transform = world.get::<GlobalTransform>(root).copied();
        if let Some(transform) = transform {
            crate::authoring_camera::focus_object(transform.translation(), 12.0);
        }
    }
}
