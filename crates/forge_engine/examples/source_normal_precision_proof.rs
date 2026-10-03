//! Verify real source PNG16 through the product decoder against pinned RGB48LE.
use forge_engine::source_fragment::normal_image_from_png;
use serde::Deserialize;

#[derive(Deserialize)]
struct Proof {
    images: Vec<ImageProof>,
    bindings: Vec<Binding>,
}
#[derive(Deserialize)]
struct ImageProof {
    source_image_sha256: String,
    dimensions: [usize; 2],
    decoded_path: String,
    decoded_sha256: String,
}
#[derive(Deserialize)]
struct Binding {
    source_image: String,
    source_sha256: String,
}
#[allow(
    clippy::float_cmp,
    reason = "This precision proof requires exact quantized source values and alpha, rather than an approximate rendering comparison"
)]
fn main() -> Result<(), Box<dyn std::error::Error>> {
    let path = std::env::args()
        .nth(1)
        .ok_or("PNG16 source proof JSON path required")?;
    let proof: Proof = serde_json::from_slice(&std::fs::read(path)?)?;
    if let Some(glb) = std::env::args().nth(2) {
        let original = std::fs::read(glb)?;
        let adapted = forge_engine::source_fragment::prepare_source_glb(&original)?;
        let parsed = bevy::gltf::gltf::Gltf::from_slice(&adapted)?;
        if parsed.materials().count() == 0 {
            return Err("source GLB proof has no material".into());
        }
        println!(
            "{}",
            serde_json::json!({"source_glb_full_validation":true,"original_bytes_modified":false,"source_extension_required":true,"materials":parsed.materials().count()})
        );
    }
    if proof.images.is_empty() {
        return Err("no PNG16 source image selected".into());
    }
    let mut pixels = 0_usize;
    for row in &proof.images {
        let binding = proof
            .bindings
            .iter()
            .find(|b| b.source_sha256 == row.source_image_sha256)
            .ok_or("PNG16 source binding missing")?;
        let original = std::fs::read(&binding.source_image)?;
        let image = normal_image_from_png(&original, &row.source_image_sha256)?;
        if image.texture_descriptor.format
            != bevy::render::render_resource::TextureFormat::Rgba32Float
        {
            return Err("product decoder did not produce portable normal storage".into());
        }
        let raw = std::fs::read(&row.decoded_path)?;
        if forge_core::bytes_sha256(&raw).to_string() != row.decoded_sha256 {
            return Err("independent RGB48LE pin changed".into());
        }
        let data = image.data.as_ref().ok_or("product decoded data missing")?;
        let count = row.dimensions[0] * row.dimensions[1];
        if data.len() != count * 16 || raw.len() != count * 6 {
            return Err("normal decoded extent changed".into());
        }
        for (rgba, rgb) in data.as_chunks::<16>().0.iter().zip(raw.as_chunks::<6>().0) {
            for channel in 0..3 {
                let source = u16::from_le_bytes([rgb[channel * 2], rgb[channel * 2 + 1]]);
                let target = f32::from_le_bytes(rgba[channel * 4..channel * 4 + 4].try_into()?);
                if target != f32::from(source) / 65535.0
                    || (target * 65535.0).round() != f32::from(source)
                {
                    return Err("normal integer source precision lost".into());
                }
            }
            if f32::from_le_bytes(rgba[12..16].try_into()?) != 1.0 {
                return Err("RGB16 source alpha should be one".into());
            }
        }
        pixels += count;
        println!(
            "{}",
            serde_json::json!({"source_sha256":row.source_image_sha256,"exact_integer_pixels":count,"texture_format":"Rgba32Float"})
        );
    }
    println!(
        "{}",
        serde_json::json!({"images_verified":proof.images.len(),"pixels_verified":pixels,"components_verified":pixels*3,"8bit_intermediate":false,"runtime_qualified":false})
    );
    Ok(())
}
