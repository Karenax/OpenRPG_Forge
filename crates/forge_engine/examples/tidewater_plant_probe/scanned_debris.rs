//! Native acquired scans using shared terrain, lighting and common placement roots.
use bevy::{
    camera::primitives::{Frustum, Sphere},
    prelude::*,
    render::{render_resource::TextureFormat, storage::ShaderBuffer},
};
use forge_assets::tidewater_debris::scanned_input::ScannedInput;
use forge_engine::{
    ManualPlacementCollision, ManualPlacementCollisionExpected,
    ManualPlacementCollisionNotRequired, ManualPlacementProjection,
    tidewater_debris::{
        scanned_draws, scanned_images,
        scanned_material::{self, ScannedExtension, ScannedMaterial},
        scanned_meshes,
        scanned_placements::{self, SourcePlacements},
        scanned_scene::ResidentDraws,
        scanned_shadow, scanned_shadow_runtime,
    },
};
use forge_project::zone_authoring::placements::ManualPlacement;
use std::{collections::BTreeMap, error::Error, io::Read, path::Path, sync::Arc};

#[derive(Resource)]
pub struct Requested;
#[derive(Component)]
pub struct MainCamera;
#[derive(Resource)]
pub struct Input {
    meshes: scanned_meshes::PreparedMeshes,
    images: [Image; 3],
    source: SourcePlacements,
    collisions: BTreeMap<
        forge_core::AssetId,
        Vec<forge_engine::tidewater_debris::collisions::PreparedPrimitive>,
    >,
    shader: String,
    depth_shader: String,
}
#[derive(Resource)]
struct Live {
    source: SourcePlacements,
    placements: Vec<ManualPlacement>,
    roots: BTreeMap<forge_core::EntityId, Entity>,
    draws: ResidentDraws,
    base: Handle<super::village::VillageMaterial>,
    material: Handle<ScannedMaterial>,
    depth_shader: Arc<String>,
    depth_meshes: Arc<Vec<scanned_shadow_runtime::MeshBytes>>,
}

fn bounded(path: &Path, limit: usize) -> Result<Vec<u8>, String> {
    let mut bytes = Vec::new();
    std::fs::File::open(path)
        .map_err(|e| e.to_string())?
        .take(limit as u64 + 1)
        .read_to_end(&mut bytes)
        .map_err(|e| e.to_string())?;
    if bytes.len() > limit {
        return Err("Scanned native payload exceeds bound".into());
    }
    Ok(bytes)
}

pub fn load() -> Result<Option<Input>, Box<dyn Error>> {
    let Some(root) = std::env::var_os("FORGE_TIDEWATER_SCANNED_ROOT") else {
        return Ok(None);
    };
    let root = Path::new(&root).canonicalize()?;
    let manifest = bounded(&root.join("package.json"), 64 * 1024)?;
    let shader_path = std::env::var_os("FORGE_TIDEWATER_SCANNED_PROGRAM")
        .ok_or("Scanned instance program path required")?;
    let shader = bounded(Path::new(&shader_path), 128 * 1024)?;
    let acquired = ScannedInput::load(&manifest, &shader, |name, limit| {
        let path = root.join(name).canonicalize().map_err(|e| e.to_string())?;
        if !path.starts_with(&root) {
            return Err("Scanned payload escapes native package".into());
        }
        bounded(&path, limit)
    })?;
    scanned_material::shader_source(&acquired.instance_program)?;
    let source = scanned_placements::from_geometry(&acquired.geometry)?;
    let mut collisions = BTreeMap::new();
    for (asset, instance) in &source.definitions {
        let owner = acquired
            .collisions
            .owner(&instance.source_id)
            .ok_or("Scanned collision owner absent")?;
        collisions.insert(
            *asset,
            forge_engine::tidewater_debris::collisions::prepare(owner)?,
        );
    }
    Ok(Some(Input {
        meshes: scanned_meshes::prepare(&acquired.package)?,
        images: scanned_images::prepare(&acquired.package)?,
        source,
        collisions,
        shader: acquired.instance_program,
        depth_shader: scanned_shadow::shader_source(&acquired.package)?,
    }))
}

pub fn setup(world: &mut World) {
    let Some(input) = world.remove_resource::<Input>() else {
        return;
    };
    if let Err(error) = stage(world, input) {
        world.resource_mut::<super::Probe>().failed = true;
        error!(%error,"Scanned native adoption failed");
    }
}

#[allow(
    clippy::too_many_lines,
    reason = "Validate the complete scanned inventory before publishing it"
)]
fn stage(world: &mut World, input: Input) -> Result<(), String> {
    let base_handle = world
        .get_resource::<super::village::SharedMaterials>()
        .and_then(|r| r.0.first())
        .ok_or("Scans require resident Village lighting")?
        .clone();
    let base = world
        .resource::<Assets<super::village::VillageMaterial>>()
        .get(&base_handle)
        .ok_or("Scanned base material absent")?
        .clone();
    let terrain_handle = world
        .get_resource::<forge_engine::tidewater_terrain::scene::ResidentTerrain>()
        .and_then(|t| t.material.as_ref())
        .ok_or("Scans require resident terrain")?;
    let terrain = world
        .resource::<Assets<super::terrain_lod::LodMaterial>>()
        .get(terrain_handle)
        .ok_or("Scanned terrain material absent")?;
    let heights = terrain.extension.height.clone();
    let domain = terrain.base.settings.domain;
    let sun = terrain.base.settings.sun;
    let image = world
        .resource::<Assets<Image>>()
        .get(&heights)
        .ok_or("Scanned height image absent")?;
    if image.texture_descriptor.format != TextureFormat::R32Float
        || image.width() != 2048
        || image.height() != 2048
    {
        return Err("Scanned terrain image contract differs".into());
    }
    scanned_material::stage_shader(world, &input.shader)?;
    let [albedo, normal, arm] = input
        .images
        .map(|image| world.resource_mut::<Assets<Image>>().add(image));
    let buffer = world
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(ShaderBuffer::new(&[0; 16], default()));
    let material = scanned_material::prepare_material(
        base,
        ScannedExtension {
            heights,
            terrain_domain: Vec4::new(domain.x, domain.y, 2048.0, 1.0),
            terrain_sun: sun,
            albedo,
            normal,
            arm,
            instances: buffer,
        },
    )?;
    let material = world
        .resource_mut::<Assets<ScannedMaterial>>()
        .add(material);
    let depth_meshes = Arc::new(scanned_shadow_runtime::geometry(&input.meshes)?);
    let draws = ResidentDraws::stage(world, input.meshes, material.clone())?;
    let mut roots = BTreeMap::new();
    for placement in &input.source.placements {
        let root = world
            .spawn((
                Name::new(placement.name.clone()),
                forge_engine::domain_transform_to_bevy(placement.transform),
                Visibility::Inherited,
                ManualPlacementProjection(placement.clone()),
            ))
            .id();
        let primitives = input
            .collisions
            .get(&placement.asset_id)
            .ok_or("Scanned prepared colliders absent")?;
        let colliders: Vec<_> = primitives
            .iter()
            .map(|p| {
                world
                    .spawn((
                        p.transform,
                        p.collider.clone(),
                        p.source.clone(),
                        ManualPlacementCollision(placement.id),
                        ChildOf(root),
                    ))
                    .id()
            })
            .collect();
        world
            .entity_mut(root)
            .insert(ManualPlacementCollisionExpected {
                placement: placement.id,
                primitives: colliders,
            });
        if primitives.is_empty() {
            world
                .entity_mut(root)
                .insert(ManualPlacementCollisionNotRequired(placement.id));
        }
        roots.insert(placement.id, root);
    }
    let placements = input.source.placements.clone();
    world.insert_resource(Live {
        source: input.source,
        placements,
        roots,
        draws,
        base: base_handle,
        material,
        depth_meshes,
        depth_shader: Arc::new(input.depth_shader),
    });
    info!("141 scanned common roots staged; draw and Rapier readiness pending");
    Ok(())
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "Source frustum inputs narrow bounded world coordinates to Bevy's f32 camera ABI."
)]
pub fn update(world: &mut World) {
    let camera = world
        .query_filtered::<(&GlobalTransform, &Frustum), Or<(With<forge_engine::ForgePlayerCamera>, With<MainCamera>)>>()
        .single(world)
        .ok()
        .map(|(t, f)| (t.translation(), *f));
    let Some((position, frustum)) = camera else {
        return;
    };
    let Some(mut live) = world.remove_resource::<Live>() else {
        return;
    };
    let result = scanned_draws::prepare(
        &live.source,
        &live.placements,
        position.to_array().map(f64::from),
        |_, p, r| {
            frustum.intersects_sphere(
                &Sphere {
                    center: Vec3::from_array(p.map(|v| v as f32)).into(),
                    radius: r as f32,
                },
                true,
            )
        },
    )
    .and_then(|frame| {
        let poses = scanned_shadow_runtime::poses(&live.source, &live.placements, &frame)?;
        let target = world
            .get_resource::<forge_engine::tidewater_object_shadow::graph::Target>()
            .ok_or("Scanned depth graph target unavailable")?
            .image
            .clone();
        live.draws.synchronize(world, &frame, &live.roots)?;
        world.insert_resource(scanned_shadow_runtime::Frame {
            target,
            shader: live.depth_shader.clone(),
            meshes: live.depth_meshes.clone(),
            poses,
        });
        Ok(())
    });
    if let Some(mut base) = world
        .resource::<Assets<super::village::VillageMaterial>>()
        .get(&live.base)
        .cloned()
    {
        base.kind = 0;
        base.settings.color = Vec4::ONE;
        base.settings.emissive = Vec4::new(0.0, 0.0, 0.0, 1.0);
        base.settings.surface = Vec4::new(0.85, 0.0, 0.0, 1.0);
        if world
            .resource::<Assets<ScannedMaterial>>()
            .get(&live.material)
            .is_some_and(|target| target.base != base)
            && let Some(mut target) = world
                .resource_mut::<Assets<ScannedMaterial>>()
                .get_mut(&live.material)
        {
            target.base = base;
        }
    }
    world.insert_resource(live);
    if let Err(error) = result {
        world.resource_mut::<super::Probe>().failed = true;
        error!(%error,"Scanned frame adoption failed");
    }
}

pub fn accepted(world: &World) -> Option<Vec<ManualPlacement>> {
    if !world.contains_resource::<Requested>() {
        return Some(Vec::new());
    }
    world
        .get_resource::<Live>()
        .map(|live| live.placements.clone())
}
