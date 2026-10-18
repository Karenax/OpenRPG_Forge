//! Native display and village fish retain source surfaces, LODs and masked depth.
use super::{
    Probe,
    fish_stand::ResidentStall,
    village::{SharedMaterials, VillageMaterial},
};
use bevy::{
    camera::primitives::{Frustum, Sphere},
    diagnostic::FrameCount,
    prelude::*,
    render::storage::ShaderBuffer,
};
use forge_tidewater::tidewater_fish_props::{
    PreparedFishProps, lod,
    material::{self, FishMaterial},
    shadow,
    surface::SpecularMode,
};
use forge_tidewater_assets::{
    tidewater_fish_props::{self, FishPropPackage},
    tidewater_game_fish::{self, GameFishPackage},
};
use std::{fs, path::Path, sync::Arc};

pub struct Inputs {
    game: GameFishPackage,
    surface: FishPropPackage,
    source_root: Transform,
}

#[derive(serde::Deserialize)]
struct Description {
    stand: Stand,
    stand_y: f32,
}
#[derive(serde::Deserialize)]
struct Stand {
    x: f32,
    z: f32,
    yaw: f32,
}

/// Both sidecars must be selected together; no source clone is needed at runtime.
pub fn load() -> Result<Option<Arc<Inputs>>, String> {
    let game = std::env::var_os("FORGE_TIDEWATER_GAME_FISH_ROOT");
    let surface = std::env::var_os("FORGE_TIDEWATER_FISH_PROP_ROOT");
    let (game, surface) = match (game, surface) {
        (None, None) => return Ok(None),
        (Some(game), Some(surface)) => (game, surface),
        _ => return Err("Native source fish require both verified sidecar roots".into()),
    };
    let game_root = Path::new(&game)
        .canonicalize()
        .map_err(|error| error.to_string())?;
    let surface_root = Path::new(&surface)
        .canonicalize()
        .map_err(|error| error.to_string())?;
    let read = |root: &Path, name: &str| -> Result<Vec<u8>, String> {
        let path = root
            .join(name)
            .canonicalize()
            .map_err(|error| error.to_string())?;
        if !path.starts_with(root) {
            return Err("Fish sidecar payload escapes its package".into());
        }
        fs::read(path).map_err(|error| error.to_string())
    };
    let game = GameFishPackage::load(
        &read(&game_root, "package.json")?,
        tidewater_game_fish::package_hash(),
        |name, _| read(&game_root, name),
    )?;
    let surface = FishPropPackage::load(
        &read(&surface_root, "package.json")?,
        tidewater_fish_props::package_hash(),
        |name, _| read(&surface_root, name),
    )?;
    let description: Description =
        serde_json::from_slice(game.description()).map_err(|error| error.to_string())?;
    if [
        description.stand.x,
        description.stand.z,
        description.stand.yaw,
        description.stand_y,
    ]
    .iter()
    .any(|value| !value.is_finite())
    {
        return Err("Invalid captured fish stand pose".into());
    }
    let prepared = PreparedFishProps::from_game_package(&game)?;
    if prepared.templates.len() != 5 {
        return Err("Native displayed fish inventory differs".into());
    }
    for template in &prepared.templates {
        if template.lod_meshes.len() != 3 {
            return Err("Native fish require three source LODs".into());
        }
        shadow::MaskedMesh::from_mesh(&template.lod_meshes[2])?;
    }
    let village = PreparedFishProps::from_package(&surface)?;
    if village.templates.len() != 78 {
        return Err("Native village fish inventory differs".into());
    }
    for template in &village.templates {
        if template.lod_meshes.len() != 3 {
            return Err("Native village fish require three source LODs".into());
        }
        shadow::MaskedMesh::from_mesh(&template.lod_meshes[2])?;
    }
    material::skin_buffer(&surface)?;
    shadow::shader_source(&surface)?;
    Ok(Some(Arc::new(Inputs {
        game,
        surface,
        source_root: Transform::from_xyz(
            description.stand.x,
            description.stand_y,
            description.stand.z,
        )
        .with_rotation(Quat::from_rotation_y(description.stand.yaw)),
    })))
}

#[derive(Resource)]
struct Source(Arc<Inputs>);

/// `MainWorld` preparation evidence, separate from GPU completion and visual review.
#[derive(Resource, serde::Serialize, Default)]
pub(super) struct Receipt {
    scope: &'static str,
    game_package_sha256: String,
    surface_package_sha256: String,
    staged: bool,
    source_ids: Vec<String>,
    village_source_ids: Vec<String>,
    visual_lod_slots: usize,
    masked_shadow_meshes: usize,
    prepared_frames: u64,
    shadow_frames_published: u64,
    source_time: f32,
    scene_frame: u32,
    group_active: bool,
    prepared_instances: usize,
    dormant_frames: u64,
}

impl Receipt {
    pub(super) fn valid(&self) -> bool {
        self.staged
            && identities_match(&self.source_ids, "game-fish", 18..23)
            && identities_match(&self.village_source_ids, "fish-prop", 0..78)
            && self.visual_lod_slots == 166
            && self.masked_shadow_meshes == 83
            && self.prepared_frames > 0
            && self.source_time.is_finite()
            && self.source_time >= 0.0
    }
}

fn identities_match(ids: &[String], kind: &str, ordinals: std::ops::Range<usize>) -> bool {
    ids.len() == ordinals.len()
        && ids.iter().zip(ordinals).all(|(id, ordinal)| {
            id == &format!("tidewater:1438b1abfcaee3267092b75573014f4d9b4a983c:{kind}:{ordinal}")
        })
}
#[derive(Component)]
struct FishRoot;

struct Instance {
    root: Entity,
    parts: [Entity; 2],
    materials: [Handle<FishMaterial>; 2],
}
#[derive(Resource)]
struct Live {
    instances: Vec<Instance>,
    meshes: Arc<Vec<shadow::MaskedMesh>>,
    shader: Arc<String>,
    clock: Handle<ShaderBuffer>,
    bounds: Option<Sphere>,
    dormant: bool,
}

pub fn install(app: &mut App, lighting_source: &str) -> Result<(), String> {
    let inputs = app
        .world()
        .resource::<Probe>()
        .fish_stand_content
        .as_ref()
        .and_then(|content| content.source.clone());
    let Some(inputs) = inputs else {
        return Ok(());
    };
    app.add_plugins(bevy::pbr::MaterialPlugin::<FishMaterial>::default());
    material::stage_shader(
        app.world_mut(),
        &inputs.surface,
        lighting_source,
        SpecularMode::ThreeReference,
    )?;
    app.insert_resource(Source(inputs))
        .insert_resource(Receipt {
            scope: "MainWorld source material/LOD preparation and masked shadow input publication; GPU completion and visual parity not qualified",
            game_package_sha256: tidewater_game_fish::package_hash().to_string(),
            surface_package_sha256: tidewater_fish_props::package_hash().to_string(),
            ..default()
        })
        .add_systems(PostStartup, stage)
        .add_systems(PostUpdate, update
            .after(bevy::transform::TransformSystems::Propagate)
            .after(super::walk_wind::update)
            .before(bevy::asset::AssetEventSystems)
            .before(bevy::camera::visibility::VisibilitySystems::VisibilityPropagate));
    Ok(())
}

fn lighting(world: &World) -> Result<VillageMaterial, String> {
    let handle = world
        .get_resource::<SharedMaterials>()
        .and_then(|shared| shared.0.first())
        .ok_or("Native fish common material absent")?;
    world
        .resource::<Assets<VillageMaterial>>()
        .get(handle)
        .cloned()
        .ok_or_else(|| "Native fish common material unresolved".to_owned())
}

#[allow(
    clippy::too_many_lines,
    reason = "Validate source meshes and receivers before publishing the fish generation"
)]
fn stage(world: &mut World) {
    let result = (|| -> Result<(), String> {
        let inputs = world.resource::<Source>().0.clone();
        let parent = world
            .get_resource::<ResidentStall>()
            .ok_or("Native fish stand absent")?
            .entity;
        let common = lighting(world)?;
        let prepared = PreparedFishProps::from_game_package(&inputs.game)?;
        let village = PreparedFishProps::from_package(&inputs.surface)?;
        let source_ids = prepared
            .templates
            .iter()
            .map(|fish| fish.source_id.clone())
            .collect();
        let village_source_ids = village
            .templates
            .iter()
            .map(|fish| fish.source_id.clone())
            .collect();
        let inverse = GlobalTransform::from(inputs.source_root).affine().inverse();
        let skin_buffer = material::skin_buffer(&inputs.surface)?;
        let shader = Arc::new(shadow::shader_source(&inputs.surface)?);
        // Validate every material and shadow mesh before publishing any fish entity.
        let mut plans = Vec::new();
        let mut depth_meshes = Vec::new();
        let templates = prepared
            .templates
            .into_iter()
            .map(|fish| (fish, Some(parent)))
            .chain(village.templates.into_iter().map(|fish| (fish, None)));
        for (template, parent) in templates {
            let value = material::prepare_scene(template.surface, Handle::default(), &common, 0.0)?;
            let world_pose = forge_engine::domain_transform_to_bevy(template.transform);
            let pose = if parent.is_some() {
                Transform::from_matrix(Mat4::from(
                    inverse * GlobalTransform::from(world_pose).affine(),
                ))
            } else {
                world_pose
            };
            depth_meshes.push(shadow::MaskedMesh::from_mesh(&template.lod_meshes[2])?);
            plans.push((template.source_id, template.lod_meshes, value, pose, parent));
        }
        let skin = world
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(skin_buffer);
        let clock = forge_tidewater::tidewater_fish_props::clock::create(world)?;
        let masked_shadow_meshes = depth_meshes.len();
        let mut instances = Vec::new();
        for (name, meshes, mut value, pose, parent) in plans {
            value.skin = skin.clone();
            value.clock = clock.clone();
            let root = world
                .spawn((FishRoot, Name::new(name), pose, Visibility::Inherited))
                .id();
            if let Some(parent) = parent {
                world.entity_mut(root).insert(ChildOf(parent));
            }
            let handles: [Handle<FishMaterial>; 2] = std::array::from_fn(|_| {
                world
                    .resource_mut::<Assets<FishMaterial>>()
                    .add(value.clone())
            });
            let mut parts = Vec::new();
            for (index, mesh) in meshes.into_iter().take(2).enumerate() {
                let mesh = world.resource_mut::<Assets<Mesh>>().add(mesh);
                parts.push(
                    world
                        .spawn((
                            Mesh3d(mesh),
                            MeshMaterial3d(handles[index].clone()),
                            Transform::IDENTITY,
                            Visibility::Hidden,
                            ChildOf(root),
                        ))
                        .id(),
                );
            }
            instances.push(Instance {
                root,
                parts: [parts[0], parts[1]],
                materials: handles,
            });
        }
        let visual_lod_slots = instances.len() * 2;
        world.insert_resource(Live {
            instances,
            meshes: Arc::new(depth_meshes),
            shader,
            clock,
            bounds: None,
            dormant: false,
        });
        let mut receipt = world.resource_mut::<Receipt>();
        receipt.staged = true;
        receipt.source_ids = source_ids;
        receipt.village_source_ids = village_source_ids;
        receipt.visual_lod_slots = visual_lod_slots;
        receipt.masked_shadow_meshes = masked_shadow_meshes;
        Ok(())
    })();
    if let Err(error) = result {
        fail(world, &error);
    }
}

#[allow(
    clippy::too_many_lines,
    reason = "Commit fish visibility, scene receivers, clock and shadow poses together"
)]
fn update(world: &mut World) {
    if !world.contains_resource::<Live>() {
        return;
    }
    let result = world.resource_scope(|world, mut live: Mut<Live>| -> Result<(), String> {
        let common = lighting(world)?;
        let cameras: Vec<_> = world
            .query_filtered::<(&Camera, &GlobalTransform, &Frustum), (
                With<Camera3d>,
                Without<forge_tidewater::tidewater_refraction::CaptureCamera>,
            )>()
            .iter(world)
            .filter(|(camera, _, _)| camera.is_active)
            .map(|(_, pose, frustum)| (pose.translation(), *frustum))
            .collect();
        if cameras.len() != 1 {
            return Err("Native fish main camera absent or ambiguous".into());
        }
        let (camera, frustum) = cameras[0];
        if live.bounds.is_none()
            || world
                .query_filtered::<Entity, (With<FishRoot>, Changed<GlobalTransform>)>()
                .iter(world)
                .next()
                .is_some()
        {
            let mut min = Vec3::splat(f32::INFINITY);
            let mut max = Vec3::splat(f32::NEG_INFINITY);
            for instance in &live.instances {
                let global = world
                    .get::<GlobalTransform>(instance.root)
                    .ok_or("Native fish pose absent")?;
                let (scale, _, center) = global.to_scale_rotation_translation();
                let radius = scale.abs().max_element() * 0.8;
                min = min.min(center - Vec3::splat(radius));
                max = max.max(center + Vec3::splat(radius));
            }
            let center = (min + max) * 0.5;
            live.bounds = Some(Sphere {
                center: center.into(),
                radius: center.distance(max),
            });
        }
        let bounds = live.bounds.as_ref().ok_or("Native fish bounds absent")?;
        let contributes = lod::group_may_contribute(
            camera.distance(bounds.center.into()),
            bounds.radius,
            frustum.intersects_sphere(bounds, true),
        );
        if !contributes {
            if !live.dormant {
                for instance in &live.instances {
                    for &part in &instance.parts {
                        if world.get::<Visibility>(part) != Some(&Visibility::Hidden) {
                            world.entity_mut(part).insert(Visibility::Hidden);
                        }
                    }
                }
                // Retain the resident shadow inventory, and retire every caster
                // once. An inactive clock must not restart or freeze other fish.
                if let Some(mut frame) = world.get_resource_mut::<shadow::Frame>() {
                    for pose in &mut frame.poses {
                        pose.enabled = false;
                    }
                }
                live.dormant = true;
            }
            let mut receipt = world.resource_mut::<Receipt>();
            receipt.group_active = false;
            receipt.prepared_instances = 0;
            receipt.dormant_frames = receipt.dormant_frames.saturating_add(1);
            return Ok(());
        }
        live.dormant = false;

        let time = world
            .get_resource::<super::walk_wind::Receipt>()
            .filter(|receipt| receipt.submitted > 0)
            .map_or(common.settings.wind.w, |receipt| receipt.wind[3]);
        if !time.is_finite() || time < 0.0 {
            return Err("Native fish source clock invalid".into());
        }
        let frame_index = world.resource::<FrameCount>().0;
        let mut plans = Vec::new();
        let mut poses = Vec::new();
        for instance in &live.instances {
            let global = world
                .get::<GlobalTransform>(instance.root)
                .ok_or("Native fish pose absent")?;
            let (scale, _, center) = global.to_scale_rotation_translation();
            let length = scale.abs().max_element();
            let selection = lod::select(
                (center.as_dvec3() - camera.as_dvec3()).length(),
                f64::from(length),
                frustum.intersects_sphere(
                    &Sphere {
                        center: center.into(),
                        radius: length * 0.8,
                    },
                    true,
                ),
            )?;
            poses.push(shadow::gpu::Pose {
                model: global.to_matrix(),
                enabled: selection.shadow,
            });
            for index in 0..2 {
                let draw = selection.draws.iter().find(|draw| draw.lod == index);
                if draw.is_none() {
                    plans.push((
                        instance.parts[index],
                        instance.materials[index].clone(),
                        None,
                        false,
                    ));
                    continue;
                }
                let mut value = world
                    .resource::<Assets<FishMaterial>>()
                    .get(&instance.materials[index])
                    .cloned()
                    .ok_or("Native fish LOD material removed")?;
                value.update_scene_receivers(&common)?;
                if let Some(draw) = draw {
                    value.settings.lod =
                        Vec4::new(draw.fade, f32::from(u8::from(draw.outgoing)), 0.0, 0.0);
                }
                plans.push((
                    instance.parts[index],
                    instance.materials[index].clone(),
                    Some(value),
                    draw.is_some(),
                ));
            }
        }
        for (entity, handle, value, visible) in plans {
            let visibility = if visible {
                Visibility::Inherited
            } else {
                Visibility::Hidden
            };
            if world.get::<Visibility>(entity) != Some(&visibility) {
                world.entity_mut(entity).insert(visibility);
            }
            let Some(value) = value else {
                continue;
            };
            let mut materials = world.resource_mut::<Assets<FishMaterial>>();
            if materials.get(&handle) != Some(&value) {
                *materials
                    .get_mut(&handle)
                    .ok_or("Native fish LOD material removed")? = value;
            }
        }
        forge_tidewater::tidewater_fish_props::clock::update(
            world,
            &live.clock,
            time,
            frame_index,
        )?;
        if common.object_settings.enabled >= 0.5 {
            world.insert_resource(shadow::Frame {
                target: common.object_shadow,
                shader: live.shader.clone(),
                meshes: live.meshes.clone(),
                poses,
                time,
            });
        } else {
            world.remove_resource::<shadow::Frame>();
        }
        let mut receipt = world.resource_mut::<Receipt>();
        receipt.group_active = true;
        receipt.prepared_instances = live.instances.len();
        receipt.prepared_frames = receipt.prepared_frames.saturating_add(1);
        if common.object_settings.enabled >= 0.5 {
            receipt.shadow_frames_published = receipt.shadow_frames_published.saturating_add(1);
        }
        receipt.source_time = time;
        receipt.scene_frame = frame_index;
        Ok(())
    });
    if let Err(error) = result {
        fail(world, &error);
    }
}

pub(super) fn activity(world: &World) -> Option<(bool, usize, u64)> {
    world.get_resource::<Receipt>().map(|receipt| {
        (
            receipt.group_active,
            receipt.prepared_instances,
            receipt.dormant_frames,
        )
    })
}

fn fail(world: &mut World, error: &str) {
    eprintln!("Native source fish: {error}");
    world.resource_mut::<Probe>().failed = true;
    world.write_message(AppExit::error());
}
