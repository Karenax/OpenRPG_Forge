//! Source mip layout and sampling, independent of files and scene placement.
use bevy::{
    asset::RenderAssetUsages,
    image::{ImageAddressMode, ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    prelude::*,
    render::render_resource::{Extent3d, TextureDimension, TextureFormat},
};
use forge_tidewater_assets::tidewater_plants::PlantPackage;
#[derive(Clone, Copy, serde::Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Layout {
    pub width: u32,
    pub height: u32,
    pub mips: u32,
}
#[derive(serde::Deserialize)]
struct Description {
    leaf: Layout,
    impostor: Layout,
}
#[derive(serde::Deserialize)]
struct GrassDescription {
    mask: MaskDescription,
}
#[derive(serde::Deserialize)]
struct MaskDescription {
    file: String,
}
pub struct PreparedTextures {
    detail: Image,
    leaf: Image,
    impostors: [Image; 2],
    grass_mask: Image,
}
#[derive(Resource, Clone)]
pub struct ResidentTextures {
    pub detail: Handle<Image>,
    pub leaf: Handle<Image>,
    pub impostors: [Handle<Image>; 2],
    pub grass_mask: Handle<Image>,
}
impl PreparedTextures {
    pub fn from_package(package: &PlantPackage) -> Result<Self, String> {
        let get = |name: &str| {
            package
                .file(name)
                .ok_or_else(|| format!("Missing plant resource: {name}"))
        };
        let desc: Description =
            serde_json::from_slice(get("plant-textures.json")?).map_err(|e| e.to_string())?;
        let grass: GrassDescription =
            serde_json::from_slice(get("grass.json")?).map_err(|e| e.to_string())?;
        Ok(Self {
            detail: detail(get("detail.bin")?)?,
            leaf: leaf(desc.leaf, get("leaf.bin")?)?,
            impostors: [
                mipped(desc.impostor, get("impostor-a.bin")?)?,
                mipped(desc.impostor, get("impostor-b.bin")?)?,
            ],
            grass_mask: mask(get(&grass.mask.file)?)?,
        })
    }
    pub fn stage(self, images: &mut Assets<Image>) -> ResidentTextures {
        ResidentTextures {
            detail: images.add(self.detail),
            leaf: images.add(self.leaf),
            impostors: self.impostors.map(|i| images.add(i)),
            grass_mask: images.add(self.grass_mask),
        }
    }
}
pub fn detail(bytes: &[u8]) -> Result<Image, String> {
    let mut image = mipped(
        Layout {
            width: 512,
            height: 512,
            mips: 1,
        },
        bytes,
    )?;
    image.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
        address_mode_u: ImageAddressMode::Repeat,
        address_mode_v: ImageAddressMode::Repeat,
        mag_filter: ImageFilterMode::Linear,
        min_filter: ImageFilterMode::Linear,
        ..default()
    });
    Ok(image)
}
pub fn leaf(layout: Layout, bytes: &[u8]) -> Result<Image, String> {
    let mut image = mipped(layout, bytes)?;
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
pub fn mask(bytes: &[u8]) -> Result<Image, String> {
    if bytes.len() != 1024 * 1024 * 4 {
        return Err("Invalid plant grass mask".into());
    }
    Ok(Image::new(
        Extent3d {
            width: 1024,
            height: 1024,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        bytes.to_vec(),
        TextureFormat::Rgba8Unorm,
        RenderAssetUsages::RENDER_WORLD,
    ))
}
pub fn mipped(layout: Layout, bytes: &[u8]) -> Result<Image, String> {
    if !(1..=4096).contains(&layout.width)
        || !(1..=4096).contains(&layout.height)
        || layout.mips == 0
        || layout.mips > layout.width.max(layout.height).ilog2() + 1
    {
        return Err("Invalid plant texture layout".into());
    }
    let expected: u64 = (0..layout.mips)
        .map(|level| {
            u64::from((layout.width >> level).max(1))
                * u64::from((layout.height >> level).max(1))
                * 4
        })
        .sum();
    if bytes.len() as u64 != expected {
        return Err("Invalid plant mip payload size".into());
    }
    let base = usize::try_from(u64::from(layout.width) * u64::from(layout.height) * 4)
        .map_err(|e| e.to_string())?;
    let mut image = Image::new(
        Extent3d {
            width: layout.width,
            height: layout.height,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        bytes[..base].to_vec(),
        TextureFormat::Rgba8Unorm,
        RenderAssetUsages::default(),
    );
    image.data = Some(bytes.to_vec());
    image.texture_descriptor.mip_level_count = layout.mips;
    image.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
        address_mode_u: ImageAddressMode::ClampToEdge,
        address_mode_v: ImageAddressMode::ClampToEdge,
        mag_filter: ImageFilterMode::Linear,
        min_filter: ImageFilterMode::Linear,
        mipmap_filter: ImageFilterMode::Linear,
        ..default()
    });
    Ok(image)
}
