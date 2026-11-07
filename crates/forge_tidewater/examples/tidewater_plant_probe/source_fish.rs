//! Native display and village fish retain source surfaces, LODs and masked depth.
use super::Probe;
use super::fish_stand::ResidentStall;
use super::village::SharedMaterials;
use bevy::{
    camera::primitives::{Frustum, Sphere},
    diagnostic::FrameCount,
    prelude::*,
    render::storage::ShaderBuffer,
};
use forge_engine::source_fish::material::{self as fish_material, FishMaterial};
use forge_engine::source_fish::{
    resident::{self, Attachment, ResidentFishProps, SceneInstance},
    shadow,
    surface::SpecularMode,
};
use forge_engine::source_surface::SourceSurfaceMaterial;
use forge_tidewater::tidewater_fish_props::{PreparedFishProps, lod};
use forge_tidewater_assets::tidewater_fish_props;
use forge_tidewater_assets::tidewater_fish_props::FishPropPackage;
use forge_tidewater_assets::tidewater_game_fish;
use forge_tidewater_assets::tidewater_game_fish::GameFishPackage;
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
        shadow::masked_mesh(&template.lod_meshes[2])?;
    }
    let village = PreparedFishProps::from_package(&surface)?;
    if village.templates.len() != 78 {
        return Err("Native village fish inventory differs".into());
    }
    for template in &village.templates {
        if template.lod_meshes.len() != 3 {
            return Err("Native village fish require three source LODs".into());
        }
        shadow::masked_mesh(&template.lod_meshes[2])?;
    }
    fish_material::skin_buffer(&surface)?;
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
#[derive(Resource)]
struct Live {
    resident: ResidentFishProps,
    count: usize,
    meshes: Arc<Vec<forge_engine::source_shadows::casters::masked::MaskedMesh>>,
    shader: Arc<String>,
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
    fish_material::stage_shader(
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

fn lighting(world: &World) -> Result<SourceSurfaceMaterial, String> {
    let handle = world
        .get_resource::<SharedMaterials>()
        .and_then(|shared| shared.0.first())
        .ok_or("Native fish common material absent")?;
    world
        .resource::<Assets<SourceSurfaceMaterial>>()
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
        let skin_buffer = fish_material::skin_buffer(&inputs.surface)?;
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
            let value =
                fish_material::prepare_scene(template.surface, Handle::default(), &common, 0.0)?;
            let world_pose = forge_engine::domain_transform_to_bevy(template.transform);
            let pose = if parent.is_some() {
                Transform::from_matrix(Mat4::from(
                    inverse * GlobalTransform::from(world_pose).affine(),
                ))
            } else {
                world_pose
            };
            depth_meshes.push(shadow::masked_mesh(&template.lod_meshes[2])?);
            plans.push((template.source_id, template.lod_meshes, value, pose, parent));
        }
        let skin = world
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(skin_buffer);
        let clock = forge_engine::source_fish::clock::create(world)?;
        let masked_shadow_meshes = depth_meshes.len();
        let mut instances = Vec::new();
        let mut base_materials = Vec::new();
        let mut owned_meshes = Vec::new();
        for (name, meshes, mut value, pose, parent) in plans {
            value.skin = skin.clone();
            value.clock = clock.clone();
            let material = world.resource_mut::<Assets<FishMaterial>>().add(value);
            base_materials.push(material.clone());
            let meshes: Vec<_> = meshes
                .into_iter()
                .map(|mesh| world.resource_mut::<Assets<Mesh>>().add(mesh))
                .collect();
            owned_meshes.extend(meshes.iter().cloned());
            instances.push(SceneInstance {
                attachment: Attachment {
                    meshes,
                    material,
                    pose,
                },
                parent,
                placement: None,
                name: Some(name),
            });
        }
        let visual_lod_slots = instances.len() * 2;
        let count = instances.len();
        let staged = resident::stage_instances(world, &instances, &clock, lod::POLICY);
        for handle in base_materials {
            world
                .resource_mut::<Assets<FishMaterial>>()
                .remove(handle.id());
        }
        if staged.is_err() {
            for handle in owned_meshes {
                world.resource_mut::<Assets<Mesh>>().remove(handle.id());
            }
            let mut buffers = world.resource_mut::<Assets<ShaderBuffer>>();
            buffers.remove(skin.id());
            buffers.remove(clock.id());
        }
        world.insert_resource(Live {
            resident: staged?,
            count,
            meshes: Arc::new(depth_meshes),
            shader,
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
                Without<forge_engine::source_refraction::CaptureCamera>,
            )>()
            .iter(world)
            .filter(|(camera, _, _)| camera.is_active)
            .map(|(_, pose, frustum)| (pose.translation(), *frustum))
            .collect();
        if cameras.is_empty() {
            // No view consumes fish during a temporary inactive-camera gap.
            // Keep the resident data and resume with the live clock afterward.
            return Ok(());
        }
        if cameras.len() > 1 {
            return Err("Native fish main camera ambiguous".into());
        }
        let (camera, frustum) = cameras[0];
        let time = world
            .get_resource::<super::walk_wind::Receipt>()
            .filter(|receipt| receipt.submitted > 0)
            .map_or(common.settings.wind.w, |receipt| receipt.wind[3]);
        if !time.is_finite() || time < 0.0 {
            return Err("Native fish source clock invalid".into());
        }
        let frame_index = world.resource::<FrameCount>().0;
        let was_dormant = live.resident.is_dormant();
        live.resident.update_view(
            world,
            camera,
            |center, radius| {
                frustum.intersects_sphere(
                    &Sphere {
                        center: center.into(),
                        radius,
                    },
                    true,
                )
            },
            time,
            frame_index,
        )?;
        if live.resident.is_dormant() {
            if !was_dormant {
                // Retain the resident shadow inventory, and retire every caster
                // once. An inactive clock must not restart or freeze other fish.
                if let Some(mut frame) =
                    world.get_resource_mut::<forge_engine::source_shadows::casters::masked::Frame>()
                {
                    for pose in &mut frame.poses {
                        pose.enabled = false;
                    }
                }
            }
            let mut receipt = world.resource_mut::<Receipt>();
            receipt.group_active = false;
            receipt.prepared_instances = 0;
            receipt.dormant_frames = receipt.dormant_frames.saturating_add(1);
            return Ok(());
        }
        live.resident.update_scene_receivers(world, &common)?;
        let poses = live
            .resident
            .shadow_inventory(world)?
            .into_iter()
            .map(
                |(shadow, enabled)| forge_engine::source_shadows::casters::masked::gpu::Pose {
                    model: shadow.world_from_local,
                    enabled,
                },
            )
            .collect();
        if common.object_settings.enabled >= 0.5 {
            world.insert_resource(forge_engine::source_shadows::casters::masked::Frame {
                target: common.object_shadow,
                shader: live.shader.clone(),
                meshes: live.meshes.clone(),
                poses,
                time,
            });
        } else {
            world.remove_resource::<forge_engine::source_shadows::casters::masked::Frame>();
        }
        let mut receipt = world.resource_mut::<Receipt>();
        receipt.group_active = true;
        receipt.prepared_instances = live.count;
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
