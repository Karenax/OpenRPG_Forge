//! Exact explicit binding layout parser shared with the acquired GPU probe.
use serde_json::Value;
use std::error::Error;
fn dimension(value: &Value) -> Result<wgpu::TextureViewDimension, Box<dyn Error>> {
    Ok(match value.as_str().ok_or("missing dimension")? {
        "2d" => wgpu::TextureViewDimension::D2,
        "2d-array" => wgpu::TextureViewDimension::D2Array,
        "cube" => wgpu::TextureViewDimension::Cube,
        _ => return Err("unqualified texture dimension".into()),
    })
}
pub(super) fn entry(value: &Value) -> Result<wgpu::BindGroupLayoutEntry, Box<dyn Error>> {
    let layout = &value["layout"];
    if layout["visibility"] != 4 {
        return Err("compute visibility required".into());
    }
    let ty = if let Some(buffer) = layout.get("buffer") {
        wgpu::BindingType::Buffer {
            ty: match buffer["type"].as_str() {
                Some("uniform") => wgpu::BufferBindingType::Uniform,
                Some("storage") => wgpu::BufferBindingType::Storage { read_only: false },
                _ => return Err("unqualified buffer type".into()),
            },
            has_dynamic_offset: false,
            min_binding_size: None,
        }
    } else if let Some(sampler) = layout.get("sampler") {
        wgpu::BindingType::Sampler(match sampler["type"].as_str() {
            Some("filtering") => wgpu::SamplerBindingType::Filtering,
            Some("non-filtering") => wgpu::SamplerBindingType::NonFiltering,
            Some("comparison") => wgpu::SamplerBindingType::Comparison,
            _ => return Err("unqualified sampler type".into()),
        })
    } else if let Some(texture) = layout.get("texture") {
        if texture["sampleType"] != "float" || texture["multisampled"] != false {
            return Err("unqualified sampled texture".into());
        }
        wgpu::BindingType::Texture {
            sample_type: wgpu::TextureSampleType::Float { filterable: true },
            view_dimension: dimension(&texture["viewDimension"])?,
            multisampled: false,
        }
    } else if let Some(texture) = layout.get("storageTexture") {
        if texture["access"] != "write-only" || texture["format"] != "rgba16float" {
            return Err("unqualified storage texture".into());
        }
        wgpu::BindingType::StorageTexture {
            access: wgpu::StorageTextureAccess::WriteOnly,
            format: wgpu::TextureFormat::Rgba16Float,
            view_dimension: dimension(&texture["viewDimension"])?,
        }
    } else {
        return Err("unknown binding kind".into());
    };
    Ok(wgpu::BindGroupLayoutEntry {
        binding: u32::try_from(value["binding"].as_u64().ok_or("missing binding")?)?,
        visibility: wgpu::ShaderStages::COMPUTE,
        ty,
        count: None,
    })
}
