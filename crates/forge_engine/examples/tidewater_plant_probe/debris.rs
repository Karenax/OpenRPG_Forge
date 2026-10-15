//! Filesystem bootstrap of verified procedural Debris through the shared engine adapter.
use bevy::{
    image::{ImageAddressMode, ImageFilterMode, ImageSampler},
    mesh::VertexAttributeValues,
    prelude::*,
    render::render_resource::TextureFormat,
};
use forge_assets::tidewater_debris::{collisions::CollisionFrames, native::NativeInput};
use forge_engine::tidewater_debris::{
    PreparedDebris,
    nature::{self, NatureExtension, NatureMaterial},
    scene::{PreparedScene, ResidentMaterials},
};
use forge_project::zone_authoring::placements::ManualPlacement;
use std::{error::Error, io::Read, path::Path};

#[derive(Resource)]
pub struct Input {
    geometry: PreparedDebris,
    collisions: CollisionFrames,
    shader: String,
    pebbles: Option<super::pebbles::Input>,
}

#[derive(Resource)]
pub struct Requested;
#[derive(Resource)]
struct AcceptedPlacements(Vec<ManualPlacement>);

pub fn ready(world: &mut World) -> bool {
    if !super::pebbles::ready(world) {
        return false;
    }
    let mut placements = if world.contains_resource::<Requested>() {
        let Some(placements) = world.get_resource::<AcceptedPlacements>() else {
            return false;
        };
        if !world
            .get_resource::<forge_engine::tidewater_debris::scene::ResidentScene>()
            .is_some_and(|scene| scene.collisions_retired(world))
        {
            return false;
        }
        placements.0.clone()
    } else {
        Vec::new()
    };
    let Some(scanned) = super::scanned_debris::accepted(world) else {
        return false;
    };
    placements.extend(scanned);
    if placements.is_empty() {
        return true;
    }
    // The common checker compares against all projection roots in the world.
    // Independent producer subsets must be combined before this exact check.
    placements.sort_by_key(|placement| placement.id);
    forge_engine::manual_placements_ready(world, &placements)
}

fn bounded(path: &Path, limit: usize) -> Result<Vec<u8>, Box<dyn Error>> {
    let mut raw = Vec::new();
    std::fs::File::open(path)?
        .take(limit as u64 + 1)
        .read_to_end(&mut raw)?;
    if raw.len() > limit {
        return Err("Debris native file exceeds bound".into());
    }
    Ok(raw)
}

pub fn load() -> Result<Option<Input>, Box<dyn Error>> {
    let Some(root) = std::env::var_os("FORGE_TIDEWATER_DEBRIS_ROOT") else {
        return Ok(None);
    };
    let root = Path::new(&root).canonicalize()?;
    let manifest = bounded(&root.join("debris-native.json"), 1_048_576)?;
    let source = NativeInput::load(
        &manifest,
        forge_core::bytes_sha256(&manifest),
        |name, limit| {
            let path = root.join(name).canonicalize().map_err(|e| e.to_string())?;
            if !path.starts_with(&root) {
                return Err("Debris payload escapes native package".into());
            }
            bounded(&path, limit).map_err(|e| e.to_string())
        },
    )?;
    let shader = source.nature_program;
    // Village denotes this exact vertex/fragment ABI, not a replacement surface.
    // Derivation retains Nature sourceVertex/sourceSurface and their coverage guards.
    let adapted = nature::shader_source(&shader)?;
    forge_engine::tidewater_temporal::shaders::derive(
        forge_engine::tidewater_temporal::shaders::Family::Village,
        &adapted,
        &adapted,
    )?;
    let geometry = PreparedDebris::from_package(&source.geometry)?;
    let pebbles = super::pebbles::load(source.geometry)?;
    Ok(Some(Input {
        geometry,
        collisions: source.collisions,
        shader,
        pebbles,
    }))
}

impl Input {
    /// Original static source poses for the shared cascade graph's initial adoption.
    #[allow(
        clippy::cast_possible_truncation,
        reason = "Verified metre coordinates narrowed by the common placement contract."
    )]
    pub fn shadow_draws(
        &self,
    ) -> Result<Vec<forge_engine::tidewater_object_shadow::OpaqueDraw>, Box<dyn Error>> {
        let mut draws = Vec::new();
        for object in &self.geometry.objects {
            let model = Mat4::from_translation(Vec3::from_array(
                object.source_translation_m.map(|v| v as f32),
            ));
            for part in &object.parts {
                let Some(mesh) = &part.shadow_mesh else {
                    continue;
                };
                let Some(VertexAttributeValues::Float32x3(positions)) =
                    mesh.attribute(Mesh::ATTRIBUTE_POSITION)
                else {
                    return Err("Debris caster positions absent".into());
                };
                let vertices = positions
                    .iter()
                    .flatten()
                    .flat_map(|v| v.to_le_bytes())
                    .collect();
                let indices = mesh
                    .indices()
                    .ok_or("Debris caster indices absent")?
                    .iter()
                    .map(u32::try_from)
                    .collect::<Result<Vec<_>, _>>()?
                    .into_iter()
                    .flat_map(u32::to_le_bytes)
                    .collect();
                draws.push(forge_engine::tidewater_object_shadow::OpaqueDraw::new(
                    vertices, indices, model,
                )?);
            }
        }
        Ok(draws)
    }
}

#[derive(Resource)]
struct LiveNature {
    source: Handle<super::village::VillageMaterial>,
    target: Handle<NatureMaterial>,
}

pub fn setup(world: &mut World) {
    let Some(input) = world.remove_resource::<Input>() else {
        return;
    };
    let result = stage(world, &input);
    if let Err(error) = result {
        world.resource_mut::<super::Probe>().failed = true;
        error!(%error, "Debris native adoption failed");
    } else if let Some(pebbles) = input.pebbles {
        world.insert_resource(pebbles);
    }
}

#[allow(
    clippy::too_many_lines,
    reason = "Validate the complete procedural inventory before publishing it"
)]
fn stage(world: &mut World, input: &Input) -> Result<(), String> {
    let shared = world
        .get_resource::<super::village::SharedMaterials>()
        .ok_or("Debris requires resident Village materials")?;
    let wood = shared.0.first().ok_or("Debris Wood role absent")?.clone();
    let hard = shared.0.get(1).ok_or("Debris Hard role absent")?.clone();
    let fabric = shared.0.get(5).ok_or("Debris Fabric role absent")?.clone();
    let base = world
        .resource::<Assets<super::village::VillageMaterial>>()
        .get(&wood)
        .ok_or("Debris base material absent")?
        .clone();
    let terrain = world
        .get_resource::<forge_engine::tidewater_terrain::scene::ResidentTerrain>()
        .ok_or("Debris requires resident terrain")?;
    let terrain = world
        .resource::<Assets<super::terrain_lod::LodMaterial>>()
        .get(
            terrain
                .material
                .as_ref()
                .ok_or("Debris terrain material absent")?,
        )
        .ok_or("Debris terrain material unresolved")?;
    let height = terrain.extension.height.clone();
    let detail = terrain.base.detail.clone();
    let domain = terrain.base.settings.domain;
    let sun = terrain.base.settings.sun;
    let images = world.resource::<Assets<Image>>();
    let height_image = images
        .get(&height)
        .ok_or("Debris height image unresolved")?;
    let detail_image = images
        .get(&detail)
        .ok_or("Debris detail image unresolved")?;
    if height_image.texture_descriptor.format != TextureFormat::R32Float
        || height_image.width() != 2048
        || height_image.height() != 2048
        || detail_image.texture_descriptor.format != TextureFormat::Rgba8Unorm
        || detail_image.width() != 512
        || detail_image.height() != 512
        || detail_image.texture_descriptor.mip_level_count != 10
    {
        return Err("Debris Nature source texture contract differs".into());
    }
    let ImageSampler::Descriptor(sampler) = &detail_image.sampler else {
        return Err("Debris Nature detail sampler unspecified".into());
    };
    if sampler.address_mode_u != ImageAddressMode::Repeat
        || sampler.address_mode_v != ImageAddressMode::Repeat
        || sampler.mag_filter != ImageFilterMode::Linear
        || sampler.min_filter != ImageFilterMode::Linear
        || sampler.mipmap_filter != ImageFilterMode::Linear
        || sampler.anisotropy_clamp != 4
    {
        return Err("Debris Nature detail sampler contract differs".into());
    }
    // The terrain detail image retains the exact RGBA8Unorm mip chain/aniso-4 sampler.
    let extension = NatureExtension {
        heights: height,
        detail,
        terrain_domain: Vec4::new(domain.x, domain.y, 2048.0, 1.0),
        terrain_sun: sun,
    };
    let material = nature::prepare_material(base, extension)?;
    nature::stage_shader(world, &input.shader)?;
    let nature = world.resource_mut::<Assets<NatureMaterial>>().add(material);
    let materials = ResidentMaterials {
        wood: wood.clone(),
        hard,
        fabric,
        nature: nature.clone(),
    };
    let mut placements: Vec<_> = input
        .geometry
        .templates()
        .into_iter()
        .map(|template| ManualPlacement {
            particles: None,
            physics: None,
            id: forge_core::EntityId::deterministic(format!(
                "native-debris:{}",
                template.source_id
            )),
            name: template.kind,
            asset_id: template.asset_id,
            asset_hash: forge_assets::tidewater_debris::package_hash(),
            transform: template.transform,
            locked: false,
        })
        .collect();
    placements.sort_by_key(|p| p.id);
    PreparedScene::new(&input.geometry, &placements, &materials, world)?
        .with_collision_frames(&input.geometry, &input.collisions)?
        .stage(world);
    world.insert_resource(LiveNature {
        source: wood,
        target: nature,
    });
    world.insert_resource(AcceptedPlacements(placements.clone()));
    info!(
        objects = placements.len(),
        "Verified procedural Debris adopted into native scene"
    );
    Ok(())
}

/// Copy live shared lighting updates into the extension base; source surface defaults persist.
pub fn update(world: &mut World) {
    let Some(live) = world.get_resource::<LiveNature>() else {
        return;
    };
    let target = live.target.clone();
    let Some(mut base) = world
        .resource::<Assets<super::village::VillageMaterial>>()
        .get(&live.source)
        .cloned()
    else {
        return;
    };
    base.kind = 0;
    base.settings.color = Vec4::ONE;
    base.settings.emissive = Vec4::new(0.0, 0.0, 0.0, 1.0);
    base.settings.surface = Vec4::new(0.8, 0.0, 0.0, 1.0);
    if world
        .resource::<Assets<NatureMaterial>>()
        .get(&target)
        .is_some_and(|material| material.base != base)
        && let Some(mut material) = world
            .resource_mut::<Assets<NatureMaterial>>()
            .get_mut(&target)
    {
        material.base = base;
    }
}
