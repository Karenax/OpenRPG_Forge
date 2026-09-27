//! Original village meshes and materials in the native development scene.
use super::{LeafTexture, Probe, ProbeMode};
use bevy::{
    camera::visibility::NoFrustumCulling,
    image::{ImageAddressMode, ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    light::NotShadowCaster,
    prelude::*,
};
use serde::Deserialize;
use std::{collections::BTreeMap, error::Error};

use forge_engine::tidewater_assets::{
    MaterialParameters as MaterialData, materials as building_materials,
};
pub use forge_engine::tidewater_material::VillageMaterial;
use forge_engine::tidewater_mesh::VillageMeshBytes;
#[derive(Deserialize)]
struct Description {
    schema: String,
    version: u32,
    nodes: Vec<Node>,
    geometries: BTreeMap<String, Geometry>,
    materials: Vec<MaterialData>,
    textures: BTreeMap<String, TextureData>,
    #[serde(default)]
    partition_sha256: Option<forge_core::ContentHash>,
}
#[derive(Deserialize)]
struct Node {
    geometry: String,
    material: usize,
    matrix_world: [f32; 16],
    visible: bool,
}
#[derive(Deserialize)]
struct Geometry {
    attributes: BTreeMap<String, String>,
    indices: String,
}
#[derive(Deserialize)]
struct TextureData {
    file: String,
    width: u32,
    height: u32,
    mips: u32,
}
pub struct Village {
    lighting: forge_engine::tidewater_material::SceneLighting,
    description: Description,
    meshes: BTreeMap<String, Mesh>,
    textures: BTreeMap<String, Image>,
}

#[expect(
    clippy::too_many_lines,
    reason = "bounded development fixture validation before any GPU upload"
)]
pub fn load(files: &BTreeMap<String, Vec<u8>>) -> Result<Option<Village>, Box<dyn Error>> {
    let Some(encoded) = files.get("village.json") else {
        return Ok(None);
    };
    let d: Description = serde_json::from_slice(encoded)?;
    if d.schema != "forge.tidewater.village-probe"
        || !matches!(d.version, 1 | 2)
        || (d.version == 1 && d.nodes.len() != 31)
        || (d.version == 2
            && (d.partition_sha256.is_none() || d.nodes.is_empty() || d.nodes.len() > 2048))
        || d.materials.len() != 7
        || d.geometries.len() > if d.version == 1 { 31 } else { 2048 }
        || d.textures.len() != 14
        || !files.contains_key("village.wgsl")
    {
        return Err("invalid village fixture".into());
    }
    let get = |name: &str| files.get(name).ok_or("missing village payload");
    let mut textures = BTreeMap::new();
    for (name, t) in &d.textures {
        if !t.width.is_power_of_two()
            || !t.height.is_power_of_two()
            || t.width > 2048
            || t.height > 2048
            || t.mips != t.width.max(t.height).ilog2() + 1
        {
            return Err("invalid village texture dimensions".into());
        }
        let expected: usize = (0..t.mips)
            .map(|l| ((t.width >> l).max(1) * (t.height >> l).max(1) * 4) as usize)
            .sum();
        let bytes = get(&t.file)?;
        if bytes.len() != expected {
            return Err("invalid village mip length".into());
        }
        let mut image = super::mipped_image(
            &LeafTexture {
                width: t.width,
                height: t.height,
                mips: t.mips,
            },
            bytes,
        );
        image.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
            address_mode_u: ImageAddressMode::Repeat,
            address_mode_v: ImageAddressMode::Repeat,
            mag_filter: ImageFilterMode::Linear,
            min_filter: ImageFilterMode::Linear,
            mipmap_filter: ImageFilterMode::Linear,
            anisotropy_clamp: 8,
            ..default()
        });
        textures.insert(name.clone(), image);
    }
    for (i, m) in d.materials.iter().enumerate() {
        if usize::from(m.kind) != i
            || m.textures.is_empty()
            || m.textures.len() > 4
            || m.textures.iter().any(|n| !textures.contains_key(n))
            || m.color
                .iter()
                .chain(&m.emissive)
                .chain(&m.surface)
                .any(|v| !v.is_finite())
        {
            return Err("invalid village material".into());
        }
    }
    let mut meshes = BTreeMap::new();
    for (name, g) in &d.geometries {
        let attr = |n: &str| {
            g.attributes
                .get(n)
                .ok_or("missing village attribute")
                .and_then(|p| get(p))
        };
        meshes.insert(
            name.clone(),
            VillageMeshBytes {
                positions: attr("position")?,
                normals: attr("normal")?,
                uv: attr("uv")?,
                tint: attr("tint")?,
                data: attr("vdata")?,
                indices: get(&g.indices)?,
            }
            .into_mesh()?,
        );
    }

    for n in &d.nodes {
        if !meshes.contains_key(&n.geometry)
            || n.material >= d.materials.len()
            || n.matrix_world.iter().any(|v| !v.is_finite())
            || Mat4::from_cols_array(&n.matrix_world).determinant().abs() < 1e-8
        {
            return Err("invalid village node".into());
        }
    }
    Ok(Some(Village {
        lighting: load_lighting(files)?,
        description: d,
        meshes,
        textures,
    }))
}

#[allow(clippy::needless_pass_by_value, clippy::too_many_arguments)] // Bevy asset and lighting resources.
pub fn setup(
    mut commands: Commands,
    probe: Res<Probe>,
    mut meshes: ResMut<Assets<Mesh>>,
    mut images: ResMut<Assets<Image>>,
    mut materials: ResMut<Assets<VillageMaterial>>,
    environment: Res<super::ProbeEnvironment>,
    hill: Res<super::hill_shadow::Images>,
    object: Res<super::object_shadow::Target>,
) {
    let Some(v) = &probe.village else {
        return;
    };
    let texture_handles: BTreeMap<_, _> = v
        .textures
        .iter()
        .map(|(k, v)| (k.clone(), images.add(v.clone())))
        .collect();
    let mesh_handles: BTreeMap<_, _> = v
        .meshes
        .iter()
        .map(|(k, v)| (k, meshes.add(v.clone())))
        .collect();
    let lighting = building_materials::LightingBindings {
        scene_lighting: v.lighting.clone(),
        environment: environment.0.clone(),
        hill_shadow: hill.output.clone(),
        object_shadow: object.image.clone(),
        object_settings: object.settings.clone(),
        clouds: [Vec4::ZERO; 5],
        cloud_shadow: None,
        wind: Vec4::new(1.0, 0.0, 6.0, 2.0),
        flags: if probe.mode == ProbeMode::VillageAlbedo {
            Vec4::new(0.0, 0.0, 1.0, 0.0)
        } else {
            Vec4::ZERO
        },
    };
    let prepared = v
        .description
        .materials
        .iter()
        .map(|m| building_materials::prepare(m, &texture_handles, &lighting))
        .collect::<Result<Vec<_>, _>>()
        .expect("village material dependencies validated before scene setup");
    let handles: Vec<_> = prepared.into_iter().map(|m| materials.add(m)).collect();
    for n in &v.description.nodes {
        commands.spawn((
            Mesh3d(mesh_handles[&n.geometry].clone()),
            MeshMaterial3d(handles[n.material].clone()),
            Transform::from_matrix(Mat4::from_cols_array(&n.matrix_world)),
            NoFrustumCulling,
            NotShadowCaster,
            if n.visible
                && matches!(
                    probe.mode,
                    ProbeMode::Surface | ProbeMode::VillageAlbedo | ProbeMode::SceneLighting
                )
            {
                Visibility::Visible
            } else {
                Visibility::Hidden
            },
        ));
    }
    info!(
        nodes = v.description.nodes.len(),
        "Source village instantiated"
    );
}

fn load_lighting(
    files: &BTreeMap<String, Vec<u8>>,
) -> Result<forge_engine::tidewater_material::SceneLighting, Box<dyn Error>> {
    #[derive(Deserialize)]
    struct SourceLighting {
        sun_direction: [f32; 3],
        sun_color: [f32; 3],
        environment_sh: [[f32; 3]; 9],
    }
    // Historical fixed fixtures use their pinned acquisition; new fixtures can
    // supply verified scene parameters without rebuilding the material shader.
    let bytes = files.get("village-lighting.json").map_or(
        include_bytes!("../../../../scripts/tidewater/reference-village-lighting.json").as_slice(),
        Vec::as_slice,
    );
    let source: SourceLighting = serde_json::from_slice(bytes)?;
    Ok(forge_engine::tidewater_material::SceneLighting::new(
        source.sun_direction,
        source.sun_color,
        source.environment_sh,
    )?)
}
