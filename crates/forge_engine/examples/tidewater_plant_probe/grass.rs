//! Original grass data and geometry, adapted to the shared resident renderer.
use super::{PlantMaterial, Probe, ProbeMode, canopy::Record, vegetation_scene};
use bevy::{
    asset::RenderAssetUsages,
    camera::visibility::NoFrustumCulling,
    light::NotShadowCaster,
    mesh::MeshTag,
    prelude::*,
    render::{
        render_resource::{Extent3d, TextureDimension, TextureFormat},
        storage::ShaderBuffer,
    },
};
use serde::Deserialize;
use std::{collections::BTreeMap, error::Error};

#[derive(Deserialize)]
struct Description {
    schema: String,
    version: u32,
    cells_per_side: usize,
    flags: String,
    min_y: String,
    max_y: String,
    mask: Mask,
    levels: Vec<Level>,
}
#[derive(Deserialize)]
struct Mask {
    width: u32,
    height: u32,
    file: String,
}
#[derive(Deserialize)]
struct Level {
    #[serde(flatten)]
    geometry: vegetation_scene::LevelDescription,
    oracle_cells: Vec<[f32; 4]>,
}
pub(super) struct Grass {
    description: Description,
    grid: forge_engine::tidewater_plants::grass::Grid,
    mask: Vec<u8>,
    meshes: Vec<Mesh>,
}
pub(super) fn load(files: &BTreeMap<String, Vec<u8>>) -> Result<Option<Grass>, Box<dyn Error>> {
    let Some(raw) = files.get("grass.json") else {
        return Ok(None);
    };
    let d: Description = serde_json::from_slice(raw)?;
    if d.schema != "forge.tidewater.grass-probe"
        || d.version != 1
        || d.cells_per_side != 256
        || d.levels.len() != 3
        || d.mask.width != 1024
        || d.mask.height != 1024
    {
        return Err("unsupported grass data".into());
    }
    let get = |name: &str| files.get(name).ok_or("missing grass payload");
    let flags = get(&d.flags)?.clone();
    let min_y = super::read_vectors::<1>(get(&d.min_y)?)?;
    let max_y = super::read_vectors::<1>(get(&d.max_y)?)?;
    let mask = get(&d.mask.file)?.clone();
    if flags.len() != 65536
        || min_y.len() != 65536
        || max_y.len() != 65536
        || mask.len() != 1024 * 1024 * 4
        || flags.iter().any(|&v| v > 1)
    {
        return Err("grass payload dimensions".into());
    }
    let meshes = d
        .levels
        .iter()
        .map(|l| vegetation_scene::mesh(&l.geometry, files))
        .collect::<Result<Vec<_>, _>>()?;
    Ok(Some(Grass {
        description: d,
        grid: forge_engine::tidewater_plants::grass::Grid::new(
            flags,
            min_y.into_iter().map(|v| v[0]).collect(),
            max_y.into_iter().map(|v| v[0]).collect(),
        )?,
        mask,
        meshes,
    }))
}

pub(super) fn textures(
    probe: &Probe,
    images: &mut Assets<Image>,
) -> (Handle<Image>, Handle<Image>) {
    let (size, heights, mask_size, mask) =
        probe
            .grass
            .as_ref()
            .map_or((1, vec![0; 4], 1, vec![0; 4]), |g| {
                (
                    2048,
                    probe
                        .lod_heights
                        .as_ref()
                        .expect("grass validated terrain")
                        .clone(),
                    1024,
                    g.mask.clone(),
                )
            });
    let make = |size, bytes, format| {
        Image::new(
            Extent3d {
                width: size,
                height: size,
                depth_or_array_layers: 1,
            },
            TextureDimension::D2,
            bytes,
            format,
            RenderAssetUsages::RENDER_WORLD,
        )
    };
    (
        images.add(make(size, heights, TextureFormat::R32Float)),
        images.add(make(mask_size, mask, TextureFormat::Rgba8Unorm)),
    )
}

fn select(g: &Grass, view: forge_world::cdlod::CdlodView) -> [Vec<[f32; 4]>; 3] {
    g.grid.select(view).expect("validated grass view")
}

#[allow(clippy::too_many_arguments, clippy::cast_possible_truncation)]
pub(super) fn spawn(
    commands: &mut Commands,
    probe: &Probe,
    base: &PlantMaterial,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<PlantMaterial>,
    buffers: &mut Assets<ShaderBuffer>,
) {
    let Some(grass) = &probe.grass else {
        return;
    };
    let transform = super::camera_transform(probe, Vec3::ZERO);
    let Projection::Perspective(projection) = super::camera_projection(probe) else {
        unreachable!()
    };
    let selected = select(
        grass,
        super::terrain_lod::source_view(&transform, &projection),
    );
    if matches!(
        probe.plant_projection,
        super::PlantProjection::Walk
            | super::PlantProjection::WalkSmoke
            | super::PlantProjection::WalkLongSmoke
    ) {
        for (level, cells) in selected.iter().enumerate() {
            assert_eq!(*cells, grass.description.levels[level].oracle_cells);
        }
        let base = base.clone();
        let readback = matches!(
            probe.plant_projection,
            super::PlantProjection::WalkSmoke | super::PlantProjection::WalkLongSmoke
        );
        commands.queue(move |world: &mut World| {
            let grass = world
                .resource_mut::<Probe>()
                .grass
                .take()
                .expect("validated grass");
            let meshes: [Mesh; 3] = grass.meshes.try_into().expect("three grass levels");
            let resident =
                forge_engine::tidewater_plants::grass::Resident::stage(world, &base, meshes)
                    .expect("grass assets");
            if readback {
                let mut buffers = world.resource_mut::<Assets<ShaderBuffer>>();
                for handle in resident.buffers() {
                    // Configure readback before the first render extraction.
                    buffers
                        .get_mut(&handle)
                        .expect("grass buffer")
                        .buffer_description
                        .usage |= wgpu::BufferUsages::COPY_SRC;
                }
            }
            world.insert_resource(super::walk_grass::Live::new(grass.grid, resident));
        });
        return;
    }
    for (level, cells) in selected.into_iter().enumerate() {
        assert_eq!(
            cells, grass.description.levels[level].oracle_cells,
            "native grass cells differ from original oracle"
        );
        info!(
            level,
            cells = cells.len(),
            "Native grass selection equals source"
        );
        if cells.is_empty() {
            continue;
        }
        let mut mat = base.clone();
        mat.kind = 3;
        mat.instances = buffers.add(ShaderBuffer::from(
            cells
                .iter()
                .map(|v| Record {
                    i_pos: Vec4::from_array(*v),
                    i_dat: Vec4::ZERO,
                })
                .collect::<Vec<_>>(),
        ));
        let material = materials.add(mat);
        let mesh = meshes.add(grass.meshes[level].clone());
        for index in 0..cells.len() {
            commands.spawn((
                Mesh3d(mesh.clone()),
                MeshMaterial3d(material.clone()),
                MeshTag(index as u32),
                Transform::IDENTITY,
                NoFrustumCulling,
                NotShadowCaster,
                if matches!(
                    probe.mode,
                    ProbeMode::Surface | ProbeMode::SceneLighting | ProbeMode::GrassCoverage
                ) {
                    Visibility::Visible
                } else {
                    Visibility::Hidden
                },
            ));
        }
    }
}
