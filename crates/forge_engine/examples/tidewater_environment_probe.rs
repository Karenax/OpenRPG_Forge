//! Verify the acquired scene cubemap through the shared native/WebGPU decoder.
use forge_engine::tidewater_environment::cube_rgba16f;
use std::{error::Error, fs::File, io::Read};

fn main() -> Result<(), Box<dyn Error>> {
    let path = std::env::args()
        .nth(1)
        .ok_or("ENVIRONMENT_CUBE_BIN required")?;
    let mut bytes = Vec::new();
    File::open(path)?
        .take(64 * 1024 * 1024 + 1)
        .read_to_end(&mut bytes)?;
    let image = cube_rgba16f(128, 6, &bytes)?;
    assert_eq!(image.data.as_deref(), Some(bytes.as_slice()));
    assert_eq!(image.texture_descriptor.mip_level_count, 6);
    assert_eq!(image.texture_descriptor.size.depth_or_array_layers, 6);
    assert_eq!(
        image
            .texture_view_descriptor
            .as_ref()
            .and_then(|d| d.dimension),
        Some(bevy::render::render_resource::TextureViewDimension::Cube)
    );
    assert!(cube_rgba16f(128, 6, &bytes[..bytes.len() - 1]).is_err());
    assert!(cube_rgba16f(0, 6, &bytes).is_err());
    assert!(cube_rgba16f(128, 9, &bytes).is_err());
    let mut corrupt = bytes.clone();
    corrupt[..2].copy_from_slice(&0x7c00_u16.to_le_bytes());
    assert!(cube_rgba16f(128, 6, &corrupt).is_err());
    corrupt[..2].copy_from_slice(&0x7e00_u16.to_le_bytes());
    assert!(cube_rgba16f(128, 6, &corrupt).is_err());
    println!(
        "Environment cube preserved: {} bytes, 6 faces, 6 mips; truncation, invalid dimensions, infinity and NaN rejected",
        bytes.len()
    );
    Ok(())
}
