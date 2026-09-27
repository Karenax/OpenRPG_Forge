//! Disposable Bevy assets prepared from the verified, portable building package.
use crate::tidewater_mesh::VillageMeshBytes;
use bevy::{
    asset::RenderAssetUsages,
    image::{ImageAddressMode, ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    prelude::*,
    render::render_resource::{Extent3d, TextureDimension, TextureFormat},
};
use forge_assets::tidewater_package::BuildingPackage;
use forge_core::{AssetId, ContentHash};
use serde::Deserialize;
use std::collections::BTreeMap;

pub mod materials;
pub mod placements;
pub mod scene;

/// Local coordinates for new placements; source coordinates for exact reference
/// rendering with an authored delta, avoiding local f32 round-trip differences.
#[derive(Clone, Copy)]
pub enum GeometrySpace {
    Local,
    SourceReference,
}

pub struct BuildingPart {
    pub mesh: Mesh,
    pub material: String,
}

/// Shared geometry for placement roots; cloning a part only clones its handle.
#[derive(Clone)]
pub struct ResidentPart {
    pub mesh: Handle<Mesh>,
    pub material: String,
}

/// A package-scoped cache with exact revision lookup. Roots retain their own
/// canonical placement identity and transform; geometry stays shared.
#[derive(Resource)]
pub struct ResidentMeshes {
    parts: BTreeMap<(AssetId, ContentHash), Vec<ResidentPart>>,
}

/// Validated CPU geometry, ready for adoption by an existing renderer.
/// Preparing a package never allocates handles or mutates the current scene.
pub struct PreparedMeshes {
    parts: BTreeMap<(AssetId, ContentHash), Vec<BuildingPart>>,
}

impl PreparedMeshes {
    pub fn load(package: &BuildingPackage) -> Result<Self, String> {
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
        Ok(Self { parts: prepared })
    }

    /// Adoption is infallible: every part and material reference was validated.
    pub fn stage(self, assets: &mut Assets<Mesh>) -> ResidentMeshes {
        let parts = self
            .parts
            .into_iter()
            .map(|(key, parts)| {
                let resident = parts
                    .into_iter()
                    .map(|part| ResidentPart {
                        mesh: assets.add(part.mesh),
                        material: part.material,
                    })
                    .collect();
                (key, resident)
            })
            .collect();
        ResidentMeshes { parts }
    }
}

impl ResidentMeshes {
    /// Validate all geometry before adding anything to the caller's asset store.
    pub fn load(package: &BuildingPackage, assets: &mut Assets<Mesh>) -> Result<Self, String> {
        Ok(PreparedMeshes::load(package)?.stage(assets))
    }

    pub fn resolve(&self, id: AssetId, hash: ContentHash) -> Result<&[ResidentPart], String> {
        self.parts
            .get(&(id, hash))
            .map(Vec::as_slice)
            .ok_or_else(|| "Building geometry revision is not resident".into())
    }
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
    package: &BuildingPackage,
    id: AssetId,
    hash: ContentHash,
    space: GeometrySpace,
) -> Result<Vec<BuildingPart>, String> {
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
            let mesh = VillageMeshBytes {
                positions,
                normals,
                uv: attr("uv")?,
                tint: attr("tint")?,
                data: attr("data")?,
                indices: read(&part.indices)?,
            }
            .into_mesh()?;
            Ok(BuildingPart {
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

    pub fn load(package: &BuildingPackage) -> Result<Self, String> {
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
