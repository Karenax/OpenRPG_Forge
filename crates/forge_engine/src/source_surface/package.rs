//! Disposable Bevy assets prepared from the verified, portable building package.
use crate::source_surface::geometry::{MeshPart, PreparedMeshes};
use crate::source_surface::mesh::SourceMeshBytes;
use bevy::{
    asset::RenderAssetUsages,
    image::{ImageAddressMode, ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    prelude::*,
    render::render_resource::{Extent3d, TextureDimension, TextureFormat},
};
use forge_assets::source_surface_package::SurfacePackage;
use forge_core::{AssetId, ContentHash};
use serde::Deserialize;
use std::collections::BTreeMap;
#[cfg(feature = "physics-rapier")]
pub mod host;
pub mod material;

/// Local coordinates for new placements; source coordinates for exact reference
/// rendering with an authored delta, avoiding local f32 round-trip differences.
#[derive(Clone, Copy)]
pub enum GeometrySpace {
    Local,
    SourceReference,
}

/// Return the verified program explicitly declared by this surface package.
///
/// # Errors
/// Rejects a missing declaration, missing payload or non-UTF-8 program.
pub fn shader_source(package: &SurfacePackage) -> Result<&str, String> {
    let path = package.materials()["shader"]["file"]
        .as_str()
        .ok_or("Source object shader path absent")?;
    let bytes = package.file(path).ok_or("Source object shader absent")?;
    std::str::from_utf8(bytes).map_err(|error| error.to_string())
}

pub fn prepare_meshes(package: &SurfacePackage) -> Result<PreparedMeshes, String> {
    let mut prepared = BTreeMap::new();
    for record in &package.catalog().assets {
        let parts = meshes(
            package,
            record.id,
            record.content_hash,
            GeometrySpace::Local,
        )?;
        for part in &parts {
            if package.materials()["materials"]
                .get(&part.material)
                .is_none()
            {
                return Err("Unknown building part material".into());
            }
        }
        prepared.insert((record.id, record.content_hash), parts);
    }
    Ok(PreparedMeshes::from_parts(prepared))
}

#[derive(Deserialize)]
struct Descriptor {
    building: Building,
}
#[derive(Deserialize)]
struct Building {
    parts: Vec<Part>,
}
#[derive(Deserialize)]
struct Part {
    material: String,
    attributes: BTreeMap<String, Payload>,
    indices: Payload,
    reference_position: Payload,
    reference_normal: Payload,
}
#[derive(Deserialize)]
struct Payload {
    file: String,
}

/// No asset-path fallback: identity and content revision must resolve together.
pub fn meshes(
    package: &SurfacePackage,
    id: AssetId,
    hash: ContentHash,
    space: GeometrySpace,
) -> Result<Vec<MeshPart>, String> {
    package.catalog().resolve_import_pin(id, hash)?;
    let descriptor: Descriptor = serde_json::from_slice(
        package
            .file(&format!("{hash}.json"))
            .ok_or("Missing building descriptor")?,
    )
    .map_err(|e| e.to_string())?;
    if descriptor.building.parts.is_empty() || descriptor.building.parts.len() > 32 {
        return Err("Invalid building part count".into());
    }
    descriptor
        .building
        .parts
        .iter()
        .map(|part| {
            let read = |p: &Payload| package.file(&p.file).ok_or("Absent building payload");
            let attr = |name: &str| {
                part.attributes
                    .get(name)
                    .ok_or("Missing building attribute")
                    .and_then(read)
            };
            let (positions, normals) = match space {
                GeometrySpace::Local => (attr("pos")?, attr("nrm")?),
                GeometrySpace::SourceReference => (
                    read(&part.reference_position)?,
                    read(&part.reference_normal)?,
                ),
            };
            let mesh = SourceMeshBytes {
                positions,
                normals,
                uv: attr("uv")?,
                tint: attr("tint")?,
                data: attr("data")?,
                indices: read(&part.indices)?,
            }
            .into_mesh()?;
            Ok(MeshPart {
                mesh,
                material: part.material.clone(),
            })
        })
        .collect()
}

#[derive(Clone, Deserialize)]
pub struct MaterialParameters {
    pub kind: u8,
    pub textures: Vec<String>,
    pub color: [f32; 4],
    pub emissive: [f32; 4],
    pub surface: [f32; 4],
}
#[derive(Deserialize)]
struct Texture {
    file: String,
    width: u32,
    height: u32,
    mips: u32,
}
#[derive(Deserialize)]
struct Library {
    materials: BTreeMap<String, MaterialParameters>,
    textures: BTreeMap<String, Texture>,
}

/// Allocate this once per package; building instances reuse the returned handles.
pub struct PreparedLibrary {
    pub materials: BTreeMap<String, MaterialParameters>,
    pub textures: BTreeMap<String, Image>,
}
/// Shared texture handles and parameters; scene lighting is supplied separately.
#[derive(Resource)]
pub struct ResidentLibrary {
    pub materials: BTreeMap<String, MaterialParameters>,
    pub textures: BTreeMap<String, Handle<Image>>,
}

impl PreparedLibrary {
    pub fn stage(self, assets: &mut Assets<Image>) -> ResidentLibrary {
        ResidentLibrary {
            materials: self.materials,
            textures: self
                .textures
                .into_iter()
                .map(|(name, image)| (name, assets.add(image)))
                .collect(),
        }
    }

    pub fn load(package: &SurfacePackage) -> Result<Self, String> {
        let library: Library =
            serde_json::from_value(package.materials().clone()).map_err(|e| e.to_string())?;
        let mut textures = BTreeMap::new();
        for (name, meta) in library.textures {
            let bytes = package.file(&meta.file).ok_or("Missing texture")?;
            textures.insert(name, texture(&meta, bytes)?);
        }
        for (name, m) in &library.materials {
            if m.kind > 6
                || *name != format!("material-{}", m.kind)
                || m.textures.is_empty()
                || m.textures.len() > 4
                || m.textures.iter().any(|n| !textures.contains_key(n))
                || m.color
                    .iter()
                    .chain(&m.emissive)
                    .chain(&m.surface)
                    .any(|v| !v.is_finite())
            {
                return Err("Invalid building material parameters".into());
            }
        }
        Ok(Self {
            materials: library.materials,
            textures,
        })
    }
}

fn texture(meta: &Texture, bytes: &[u8]) -> Result<Image, String> {
    if !meta.width.is_power_of_two()
        || !meta.height.is_power_of_two()
        || meta.width > 2048
        || meta.height > 2048
        || meta.mips != meta.width.max(meta.height).ilog2() + 1
    {
        return Err("Invalid building texture dimensions".into());
    }
    let length: usize = (0..meta.mips)
        .map(|l| ((meta.width >> l).max(1) * (meta.height >> l).max(1) * 4) as usize)
        .sum();
    if bytes.len() != length {
        return Err("Invalid building mip chain".into());
    }
    let mut image = Image::new(
        Extent3d {
            width: meta.width,
            height: meta.height,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        bytes[..(meta.width * meta.height * 4) as usize].to_vec(),
        TextureFormat::Rgba8Unorm,
        RenderAssetUsages::default(),
    );
    image.data = Some(bytes.to_vec());
    image.texture_descriptor.mip_level_count = meta.mips;
    image.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
        address_mode_u: ImageAddressMode::Repeat,
        address_mode_v: ImageAddressMode::Repeat,
        mag_filter: ImageFilterMode::Linear,
        min_filter: ImageFilterMode::Linear,
        mipmap_filter: ImageFilterMode::Linear,
        anisotropy_clamp: 8,
        ..default()
    });
    Ok(image)
}
