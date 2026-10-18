//! Verify terrain package preparation in CPU-owned Bevy assets, without GPU claims.
use forge_core::ContentHash;
use forge_tidewater_assets::tidewater_terrain::TerrainPackage;
use std::{error::Error, fs, io::Read, path::PathBuf};

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 2 {
        return Err("PACKAGE_ROOT EXPECTED_MANIFEST_SHA256 required".into());
    }
    let root = PathBuf::from(&args[0]).canonicalize()?;
    let expected: ContentHash = args[1].parse()?;
    let mut manifest = Vec::new();
    fs::File::open(root.join("package.json"))?
        .take(16 * 1024 + 1)
        .read_to_end(&mut manifest)?;
    let reader = |name: &str, limit: usize| -> Result<Vec<u8>, String> {
        let path = root.join(name).canonicalize().map_err(|e| e.to_string())?;
        if path.parent() != Some(root.as_path()) {
            return Err("Payload escapes package".into());
        }
        let mut bytes = Vec::new();
        fs::File::open(path)
            .map_err(|e| e.to_string())?
            .take(limit as u64 + 1)
            .read_to_end(&mut bytes)
            .map_err(|e| e.to_string())?;
        Ok(bytes)
    };
    let package = TerrainPackage::load(&manifest, expected, reader)?;
    let mut images = bevy::prelude::Assets::<bevy::prelude::Image>::default();
    let prepared =
        forge_tidewater::tidewater_terrain::textures::PreparedSurface::from_package(&package)?;
    assert_eq!(images.len(), 0);
    let resident = prepared.stage(&mut images);
    assert_eq!(images.len(), 3);
    for (handle, name, side, mips, repeat, anisotropy) in [
        (&resident.splat, "terrain-splat.bin", 2048, 12, false, 1),
        (&resident.detail, "terrain-detail.bin", 512, 10, true, 4),
        (
            &resident.linear_detail,
            "terrain-detail.bin",
            512,
            10,
            true,
            1,
        ),
    ] {
        let image = images.get(handle).ok_or("Missing staged image")?;
        assert_eq!(image.data.as_deref(), package.file(name));
        assert_eq!(image.texture_descriptor.size.width, side);
        assert_eq!(image.texture_descriptor.size.height, side);
        assert_eq!(image.texture_descriptor.mip_level_count, mips);
        assert_eq!(
            image.texture_descriptor.format,
            bevy::render::render_resource::TextureFormat::Rgba8Unorm
        );
        let bevy::image::ImageSampler::Descriptor(sampler) = &image.sampler else {
            return Err("Explicit terrain sampler missing".into());
        };
        assert_eq!(sampler.anisotropy_clamp, anisotropy);
        let mode = if repeat {
            bevy::image::ImageAddressMode::Repeat
        } else {
            bevy::image::ImageAddressMode::ClampToEdge
        };
        assert_eq!(sampler.address_mode_u, mode);
        assert_eq!(sampler.address_mode_v, mode);
        assert_eq!(sampler.mipmap_filter, bevy::image::ImageFilterMode::Linear);
    }
    let normal = package
        .file("terrain-normal.bin")
        .ok_or("Missing normals")?;
    let image = forge_tidewater::tidewater_terrain::textures::normal_image(normal)?;
    assert_eq!(image.data.as_deref(), Some(normal));
    assert_eq!(image.texture_descriptor.mip_level_count, 12);
    assert!(
        image
            .texture_descriptor
            .usage
            .contains(bevy::render::render_resource::TextureUsages::RENDER_ATTACHMENT)
    );
    assert!(
        forge_tidewater::tidewater_terrain::textures::normal_image(&normal[..normal.len() - 1])
            .is_err()
    );
    assert!(
        forge_tidewater::tidewater_terrain::textures::PreparedSurface::new(normal, &[0; 4])
            .is_err()
    );
    assert!(
        forge_tidewater::tidewater_terrain::textures::PreparedSurface::new(&[], &[0; 4]).is_err()
    );
    assert_eq!(images.len(), 3);
    println!(
        "PASS: package textures staged without byte changes; mip formats and samplers preserved; malformed inputs rejected before staging"
    );
    Ok(())
}
