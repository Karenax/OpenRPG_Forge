//! Native adoption of the acquired camera-following Pebble field, on the shared renderer.
use bevy::{
    camera::primitives::{Aabb, Frustum},
    image::{ImageAddressMode, ImageFilterMode, ImageSampler},
    prelude::*,
    render::{render_resource::TextureFormat, storage::ShaderBuffer},
};
use forge_tidewater::tidewater_debris::{
    pebble_cells::{Camera, CellMap, Selector},
    pebble_material::{self, PebbleExtension, PebbleMaterial},
    pebble_mesh,
    pebble_scene::ResidentField,
    pebbles,
};
use forge_tidewater_assets::tidewater_debris::{DebrisPackage, pebble_program::Program};
use std::{error::Error, io::Read, path::Path};

#[derive(Resource)]
pub struct Input {
    package: DebrisPackage,
    program: Program,
}

#[derive(Resource)]
pub struct Requested;

#[derive(Resource)]
struct Live {
    cells: CellMap,
    selector: Selector,
    field: ResidentField,
    source: Handle<super::village::VillageMaterial>,
    material: Handle<PebbleMaterial>,
    terrain_revision: forge_core::ContentHash,
    published: bool,
}

fn bounded(path: &Path, limit: usize) -> Result<Vec<u8>, String> {
    let mut raw = Vec::new();
    std::fs::File::open(path)
        .map_err(|error| error.to_string())?
        .take(u64::try_from(limit).map_err(|error| error.to_string())? + 1)
        .read_to_end(&mut raw)
        .map_err(|error| error.to_string())?;
    if raw.len() > limit {
        return Err("Pebble program payload exceeds bound".into());
    }
    Ok(raw)
}

pub fn requested() -> bool {
    std::env::var_os("FORGE_TIDEWATER_PEBBLE_PROGRAM_ROOT").is_some()
}

/// Consume the geometry package already verified by the Debris bootstrap.
pub fn load(package: DebrisPackage) -> Result<Option<Input>, Box<dyn Error>> {
    let Some(root) = std::env::var_os("FORGE_TIDEWATER_PEBBLE_PROGRAM_ROOT") else {
        return Ok(None);
    };
    let root = Path::new(&root).canonicalize()?;
    let descriptor = bounded(&root.join("program.json"), 65_536)?;
    let program = Program::load(
        &descriptor,
        forge_tidewater_assets::tidewater_debris::pebble_program::program_hash(),
        |name, limit| {
            let path = root
                .join(name)
                .canonicalize()
                .map_err(|error| error.to_string())?;
            if !path.starts_with(&root) {
                return Err("Pebble program escapes acquired package".into());
            }
            bounded(&path, limit)
        },
    )?;
    let adapted = pebble_material::shader_source(program.source())?;
    forge_tidewater::tidewater_temporal::shaders::derive(
        forge_tidewater::tidewater_temporal::shaders::Family::Pebble,
        &adapted,
        &adapted,
    )?;
    Ok(Some(Input { package, program }))
}

pub fn ready(world: &World) -> bool {
    !world.contains_resource::<Requested>()
        || world
            .get_resource::<Live>()
            .is_some_and(|live| live.published)
}

pub fn setup(world: &mut World) {
    let Some(input) = world.remove_resource::<Input>() else {
        return;
    };
    if let Err(error) = stage(world, input) {
        world.resource_mut::<super::Probe>().failed = true;
        error!(%error, "Native Pebble adoption failed");
    }
}

fn stage(world: &mut World, input: Input) -> Result<(), String> {
    let source = world
        .get_resource::<super::village::SharedMaterials>()
        .and_then(|shared| shared.0.first())
        .ok_or("Pebbles require Village lighting")?
        .clone();
    let base = world
        .resource::<Assets<super::village::VillageMaterial>>()
        .get(&source)
        .ok_or("Pebble base material absent")?
        .clone();
    let terrain_handle = world
        .get_resource::<forge_tidewater::tidewater_terrain::scene::ResidentTerrain>()
        .and_then(|terrain| terrain.material.as_ref())
        .ok_or("Pebbles require resident terrain")?;
    let terrain = world
        .resource::<Assets<super::terrain_lod::LodMaterial>>()
        .get(terrain_handle)
        .ok_or("Pebble terrain material absent")?;
    let heights = terrain.extension.height.clone();
    let detail = terrain.base.detail.clone();
    let domain = terrain.base.settings.domain;
    let sun = terrain.base.settings.sun;
    let images = world.resource::<Assets<Image>>();
    let height_image = images.get(&heights).ok_or("Pebble height texture absent")?;
    let detail_image = images.get(&detail).ok_or("Pebble detail texture absent")?;
    if height_image.texture_descriptor.format != TextureFormat::R32Float
        || height_image.width() != 2048
        || height_image.height() != 2048
        || detail_image.texture_descriptor.format != TextureFormat::Rgba8Unorm
        || detail_image.width() != 512
        || detail_image.height() != 512
        || detail_image.texture_descriptor.mip_level_count != 10
    {
        return Err("Pebble acquired terrain texture contract differs".into());
    }
    let ImageSampler::Descriptor(sampler) = &detail_image.sampler else {
        return Err("Pebble acquired detail sampler unspecified".into());
    };
    if sampler.address_mode_u != ImageAddressMode::Repeat
        || sampler.address_mode_v != ImageAddressMode::Repeat
        || sampler.min_filter != ImageFilterMode::Linear
        || sampler.mag_filter != ImageFilterMode::Linear
        || sampler.mipmap_filter != ImageFilterMode::Linear
        || sampler.anisotropy_clamp != 4
    {
        return Err("Pebble acquired detail sampler differs".into());
    }
    let prepared = pebbles::prepare(&input.package, f64::from(domain.x))?;
    let resources = pebble_mesh::prepare(&prepared.patch)?;
    let terrain_revision = world
        .resource::<super::Probe>()
        .terrain
        .as_ref()
        .ok_or("Pebble CPU terrain unavailable")?
        .document_hash();
    pebble_material::stage_shader(world, input.program.source())?;
    let density = world.resource_mut::<Assets<Image>>().add(resources.density);
    let cells = world
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(ShaderBuffer::new(&[0; 16], default()));
    let material = pebble_material::prepare_material(
        base,
        PebbleExtension {
            heights,
            detail,
            terrain_domain: Vec4::new(domain.x, domain.y, 2048.0, 1.0),
            terrain_sun: sun,
            density,
            cells,
        },
    )?;
    let material = world.resource_mut::<Assets<PebbleMaterial>>().add(material);
    let field = ResidentField::stage(world, resources.mesh, material.clone())?;
    world.insert_resource(Live {
        cells: prepared.cells,
        selector: Selector::default(),
        field,
        source,
        material,
        terrain_revision,
        published: false,
    });
    info!("Native Pebble resources staged; source camera publication pending");
    Ok(())
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "Validated source cell bounds narrow to the existing Bevy float32 frustum ABI."
)]
pub fn update(world: &mut World) {
    let camera = world
        .query_filtered::<(&GlobalTransform, &Frustum, &Projection), Or<(
            With<forge_engine::ForgePlayerCamera>,
            With<super::scanned_debris::MainCamera>,
        )>>()
        .single(world)
        .ok()
        .map(|(transform, frustum, projection)| {
            (
                transform.translation(),
                transform.back().as_vec3(),
                *frustum,
                projection.get_clip_from_view(),
            )
        });
    let Some((position, back, frustum, projection)) = camera else {
        return;
    };
    let Some(mut live) = world.remove_resource::<Live>() else {
        return;
    };
    let result = (|| {
        let terrain = world
            .resource::<super::Probe>()
            .terrain
            .as_ref()
            .ok_or("Pebble CPU terrain unavailable")?;
        if terrain.document_hash() != live.terrain_revision {
            return Err("Pebble cell bounds require rebuilding after terrain revision".into());
        }
        let height = terrain
            .height_at(f64::from(position.x), f64::from(position.z))
            .map_err(|error| error.to_string())?;
        if live.selector.update(
            &live.cells,
            Camera {
                position: position.to_array().map(f64::from),
                world_back: back.to_array().map(f64::from),
                projection_scale: [
                    f64::from(projection.x_axis.x),
                    f64::from(projection.y_axis.y),
                ],
                terrain_height: height,
            },
            |min, max| {
                let min = Vec3::from_array(min.map(|value| value as f32));
                let max = Vec3::from_array(max.map(|value| value as f32));
                frustum.intersects_obb_identity(&Aabb::from_min_max(min, max))
            },
        )? {
            live.field.synchronize(world, &live.selector)?;
        }
        let base = world
            .resource::<Assets<super::village::VillageMaterial>>()
            .get(&live.source)
            .ok_or("Pebble source lighting retired")?
            .clone();
        let materials = &mut world.resource_mut::<Assets<PebbleMaterial>>();
        let mut material = materials
            .get_mut(&live.material)
            .ok_or("Pebble material retired")?;
        material.base = pebble_material::prepare_material(base, material.extension.clone())?.base;
        live.published = true;
        Ok::<(), String>(())
    })();
    if let Err(error) = result {
        live.published = false;
        world.resource_mut::<super::Probe>().failed = true;
        error!(%error, "Native Pebble camera publication failed");
    }
    world.insert_resource(live);
}
