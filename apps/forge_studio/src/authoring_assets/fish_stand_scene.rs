//! Derived viewport projection for the verified Tidewater `FishStand` composite.
//! The canonical placement remains a `ManualPlacement`; resident GPU resources
//! are retained by the viewport and are never rebuilt by transform edits.
use super::{imported_buildings, placements};
use bevy::{
    ecs as bevy_ecs,
    gltf::GltfAssetLabel,
    prelude::*,
    world_serialization::{WorldAsset, WorldAssetRoot, WorldInstanceReady},
};
use forge_project::zone_authoring::placements::ManualPlacement;
use std::{cell::RefCell, collections::BTreeMap};
use {
    forge_engine::{ManualPlacementPart, ManualPlacementProjection},
    forge_tidewater::{
        tidewater_fish_props::material::{self as fish_material, FishMaterial},
        tidewater_scene_environment::ResidentEnvironment,
        tidewater_stall::{FishStandResident, full_material::FullMaterial},
    },
};

mod full;
mod lod;

thread_local! {
    static READY: RefCell<Result<Vec<ManualPlacement>, String>> = const { RefCell::new(Ok(Vec::new())) };
    static FOCUS: std::cell::Cell<Option<forge_core::EntityId>> = const { std::cell::Cell::new(None) };
}

#[derive(Resource, Default)]
struct State {
    resident: Option<FishStandResident>,
    material: Option<Handle<FullMaterial>>,
    lighting_key: Option<AssetId<Image>>,
    joe_path: Option<String>,
    joe: Option<Handle<WorldAsset>>,
    fish: Vec<lod::Asset>,
    skin: Option<Handle<bevy::render::storage::ShaderBuffer>>,
    objects: BTreeMap<forge_core::EntityId, Object>,
    shadow_shader: Option<std::sync::Arc<String>>,
    full_shader_generation: Option<u64>,
}

struct Object {
    source: ManualPlacement,
    root: Entity,
    fish_materials: Vec<Handle<FishMaterial>>,
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
    super::fish_shadow_scene::install(app);
    app.add_plugins((
        MaterialPlugin::<forge_tidewater::tidewater_stall::FishStandMaterial>::default(),
        MaterialPlugin::<FullMaterial>::default(),
    ))
    .init_resource::<State>()
    .add_systems(
        PostUpdate,
        update
            .after(crate::authoring_assets::imported_scene::update)
            .after(bevy::transform::TransformSystems::Propagate),
    );
    forge_tidewater::tidewater_stall::install_shader(app)?;
    full::install(app)
}

fn clear(world: &mut World, state: &mut State) {
    lod::clear_shadow(world);
    for object in std::mem::take(&mut state.objects).into_values() {
        world.despawn(object.root);
        lod::retire(world, object.fish_materials);
    }
}

fn purge(world: &mut World, state: &mut State) {
    world.remove_resource::<forge_tidewater::tidewater_stall::contact_gpu::Inputs>();
    state.shadow_shader = None;
    clear(world, state);
    if let Some(path) = state.joe_path.take() {
        super::preview::remove(&path);
    }
    state.joe = None;
    for asset in std::mem::take(&mut state.fish) {
        for mesh in asset.meshes {
            world.resource_mut::<Assets<Mesh>>().remove(mesh.id());
        }
        world
            .resource_mut::<Assets<FishMaterial>>()
            .remove(asset.material.id());
    }
    if let Some(skin) = state.skin.take() {
        world
            .resource_mut::<Assets<bevy::render::storage::ShaderBuffer>>()
            .remove(skin.id());
    }
    if let Some(handle) = state.material.take() {
        world
            .resource_mut::<Assets<FullMaterial>>()
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
    state.full_shader_generation = None;
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
        let material = full::prepare(world, resident, &mut state.full_shader_generation)?;
        if let Some(handle) = state.material.as_ref() {
            let mut materials = world.resource_mut::<Assets<FullMaterial>>();
            let mut existing = materials
                .get_mut(handle)
                .ok_or("FishStand material unavailable")?;
            *existing = material;
        } else {
            state.material = Some(world.resource_mut::<Assets<FullMaterial>>().add(material));
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
    material: &Handle<FullMaterial>,
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
            lod::retire(world, object.fish_materials.clone());
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
        let (root, fish_materials) =
            spawn_fish_stand(world, state, &source, mesh, material, &package)?;
        state.objects.insert(
            source.id,
            Object {
                source,
                root,
                fish_materials,
            },
        );
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
    reference: &forge_tidewater_assets::tidewater_fish_stand::FishStandReference,
    package: &forge_tidewater_assets::tidewater_fish_stand::FishStandPackage,
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
        let game = imported_buildings::game_fish_package()
            .ok_or("Paquet GameFish requis pour les matériaux de l’étal")?;
        let source: GameFishDescription =
            serde_json::from_slice(game.description()).map_err(|error| error.to_string())?;
        let source_root = Transform::from_xyz(source.stand.x, source.stand_y, source.stand.z)
            .with_rotation(Quat::from_rotation_y(source.stand.yaw));
        let inverse_root = GlobalTransform::from(source_root).affine().inverse();
        let surface =
            imported_buildings::fish_props_package().ok_or("Matériau FishProps source absent")?;
        state.shadow_shader = Some(std::sync::Arc::new(
            forge_tidewater::tidewater_fish_props::shadow::shader_source(&surface)?,
        ));
        let building = imported_buildings::building_package().ok_or("Éclairage source absent")?;
        let lighting_source = std::str::from_utf8(
            building
                .file("village.wgsl")
                .ok_or("Shader partagé absent")?,
        )
        .map_err(|error| error.to_string())?;
        fish_material::stage_shader(
            world,
            &surface,
            lighting_source,
            forge_tidewater::tidewater_fish_props::surface::SpecularMode::ThreeReference,
        )?;
        let lighting =
            super::imported_scene::lighting(world).ok_or("Éclairage de l’étal indisponible")?;
        let skin_buffer = fish_material::skin_buffer(&surface)?;
        let mut prepared = Vec::new();
        let time = world.resource::<Time>().elapsed_secs();
        let templates =
            forge_tidewater::tidewater_fish_props::PreparedFishProps::from_game_package(&game)?
                .templates;
        if templates.len() != package.fish().len() {
            return Err("Inventaire d'étal différent".into());
        }
        for template in templates {
            let mut material =
                fish_material::prepare(template.surface, Handle::default(), &lighting, time)?;
            material.shared_scene_lighting = true;
            let local_pose = Transform::from_matrix(Mat4::from(
                inverse_root
                    * GlobalTransform::from(forge_engine::domain_transform_to_bevy(
                        template.transform,
                    ))
                    .affine(),
            ));
            prepared.push((template.lod_meshes, material, local_pose));
        }
        let skin = world
            .resource_mut::<Assets<bevy::render::storage::ShaderBuffer>>()
            .add(skin_buffer);
        state.skin = Some(skin.clone());
        for (meshes, mut material, local_pose) in prepared {
            material.skin = skin.clone();
            let meshes = meshes
                .into_iter()
                .map(|mesh| world.resource_mut::<Assets<Mesh>>().add(mesh))
                .collect();
            let material = world.resource_mut::<Assets<FishMaterial>>().add(material);
            state.fish.push(lod::Asset {
                meshes,
                material,
                pose: local_pose,
            });
        }
    }
    update_fish_materials(world, state)
}

#[derive(serde::Deserialize)]
struct GameFishDescription {
    stand: GameFishStand,
    stand_y: f32,
}
#[derive(serde::Deserialize)]
struct GameFishStand {
    x: f32,
    z: f32,
    yaw: f32,
}

fn spawn_fish_stand(
    world: &mut World,
    state: &State,
    source: &ManualPlacement,
    mesh: &Handle<Mesh>,
    material: &Handle<FullMaterial>,
    package: &forge_tidewater_assets::tidewater_fish_stand::FishStandPackage,
) -> Result<(Entity, Vec<Handle<FishMaterial>>), String> {
    let joe = state.joe.clone().ok_or("Joe GLB unavailable")?;
    let fish_handles = state
        .fish
        .get(..package.fish().len())
        .ok_or("Fish cache unavailable")?
        .to_vec();

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
        forge_tidewater::tidewater_object_shadow::scene::OpaqueCaster,
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
    let handles = match lod::spawn(world, source.id, root, &fish_handles) {
        Ok(handles) => handles,
        Err(error) => {
            world.despawn(root);
            return Err(error);
        }
    };
    Ok((root, handles))
}

pub(crate) fn update(world: &mut World) {
    if !crate::authoring_viewport::can_project_sources() {
        lod::clear_shadow(world);
        let _ = READY.with(|slot| slot.replace(Ok(Vec::new())));
        return;
    }
    world.resource_scope(|world, mut state: Mut<State>| {
        if let Err(error) = stage(world, &mut state)
            .and_then(|()| lod::update(world))
            .and_then(|()| lod::publish_shadow(world, state.shadow_shader.clone()))
        {
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

fn update_fish_materials(world: &mut World, state: &State) -> Result<(), String> {
    let editing = world.contains_resource::<crate::authoring_render::FastEditing>();
    let time = if editing {
        0.0
    } else {
        world.resource::<Time>().elapsed_secs()
    };
    let frame = if editing {
        0
    } else {
        world.resource::<bevy::diagnostic::FrameCount>().0
    };
    let mut materials = world.resource_mut::<Assets<FishMaterial>>();
    for asset in &state.fish {
        let handle = &asset.material;
        let material = materials
            .get(handle)
            .ok_or("Matériau d’étal indisponible")?;
        if material.settings.info.w.to_bits() != time.to_bits()
            || material.settings.frame_index != frame
        {
            let mut material = materials
                .get_mut(handle)
                .ok_or("Matériau d’étal indisponible")?;
            material.settings.info.w = time;
            material.settings.frame_index = frame;
        }
    }
    Ok(())
}
