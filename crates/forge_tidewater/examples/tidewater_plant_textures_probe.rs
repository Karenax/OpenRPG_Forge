//! Exact portable texture bytes/samplers and immutable generations; no GPU receipt.
use bevy::{
    image::{ImageAddressMode, ImageFilterMode, ImageSampler},
    prelude::*,
    render::render_resource::TextureFormat,
};
use forge_tidewater::tidewater_plants::textures::{self, Layout, PreparedTextures};
use forge_tidewater_assets::tidewater_plants::PlantPackage;
use std::{error::Error, fs, io::Read, path::PathBuf};
fn main() -> Result<(), Box<dyn Error>> {
    let args = std::env::args().skip(1).collect::<Vec<_>>();
    if args.len() != 2 {
        return Err("PACKAGE_ROOT MANIFEST_SHA256 required".into());
    }
    let root = PathBuf::from(&args[0]).canonicalize()?;
    let package = PlantPackage::load(
        &fs::read(root.join("package.json"))?,
        args[1].parse()?,
        |name, limit| {
            let path = root.join(name).canonicalize().map_err(|e| e.to_string())?;
            if path.parent() != Some(root.as_path()) {
                return Err("Package path escaped".into());
            }
            let mut bytes = Vec::new();
            fs::File::open(path)
                .map_err(|e| e.to_string())?
                .take(limit as u64 + 1)
                .read_to_end(&mut bytes)
                .map_err(|e| e.to_string())?;
            Ok(bytes)
        },
    )?;
    let mut images = Assets::<Image>::default();
    let first = PreparedTextures::from_package(&package)?.stage(&mut images);
    for (handle, name, width, height, mips) in [
        (&first.detail, "detail.bin", 512, 512, 1),
        (&first.leaf, "leaf.bin", 1024, 1024, 11),
        (&first.impostors[0], "impostor-a.bin", 3840, 768, 12),
        (&first.impostors[1], "impostor-b.bin", 3840, 768, 12),
        (&first.grass_mask, "grass-mask.bin", 1024, 1024, 1),
    ] {
        let image = images.get(handle).ok_or("image")?;
        assert_eq!(image.data.as_deref(), package.file(name));
        assert_eq!(image.texture_descriptor.format, TextureFormat::Rgba8Unorm);
        assert_eq!(
            (
                image.width(),
                image.height(),
                image.texture_descriptor.mip_level_count
            ),
            (width, height, mips)
        );
    }
    let ImageSampler::Descriptor(leaf) = &images.get(&first.leaf).ok_or("leaf")?.sampler else {
        return Err("leaf sampler".into());
    };
    assert_eq!(leaf.anisotropy_clamp, 8);
    assert_eq!(leaf.address_mode_u, ImageAddressMode::Repeat);
    assert_eq!(leaf.mipmap_filter, ImageFilterMode::Linear);
    let ImageSampler::Descriptor(atlas) = &images.get(&first.impostors[0]).ok_or("atlas")?.sampler
    else {
        return Err("atlas sampler".into());
    };
    assert_eq!(atlas.address_mode_u, ImageAddressMode::ClampToEdge);
    assert_eq!(atlas.mipmap_filter, ImageFilterMode::Linear);
    let second = PreparedTextures::from_package(&package)?.stage(&mut images);
    assert_ne!(first.leaf, second.leaf);
    assert_eq!(
        images.get(&first.leaf).ok_or("retained")?.data,
        images.get(&second.leaf).ok_or("new")?.data
    );
    assert_eq!(images.len(), 10);
    invalid_layouts();
    assert_eq!(images.len(), 10);
    println!(
        "PLANT_TEXTURES_PASS five source images, exact bytes/mips/linear format/samplers; isolated replacement handles; invalid layouts and truncated buffers rejected"
    );
    Ok(())
}

fn invalid_layouts() {
    assert!(textures::detail(&[0; 4]).is_err());
    assert!(textures::mask(&[0; 4]).is_err());
    for layout in [
        Layout {
            width: 0,
            height: 1,
            mips: 1,
        },
        Layout {
            width: 4097,
            height: 1,
            mips: 1,
        },
        Layout {
            width: 1,
            height: 1,
            mips: 2,
        },
        Layout {
            width: 1,
            height: 1,
            mips: 0,
        },
    ] {
        assert!(textures::mipped(layout, &[0; 4]).is_err());
    }
    assert!(
        textures::mipped(
            Layout {
                width: 2,
                height: 2,
                mips: 2
            },
            &[0; 16]
        )
        .is_err()
    );
}
