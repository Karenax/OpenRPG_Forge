//! Common GPU kernel preparation and dispatch for source-ocean consumers.
// SPDX-License-Identifier: MIT
// Adapted from Tidewater OceanFFT.js, copyright 2026 DRG Software Solutions LLC.
// Revision 1438b1ab; see third_party/tidewater/LICENSE.

use bevy::{
    prelude::*,
    render::{
        render_resource as gpu,
        renderer::{RenderContext, RenderDevice},
        storage::GpuShaderBuffer,
        texture::GpuImage,
    },
};

/// Source kernel order, also used by the verified input loaders.
pub const NAMES: [&str; 9] = [
    "initSpectrum",
    "conjugate",
    "copyH0",
    "row",
    "column",
    "mipA-0",
    "mipA-1",
    "mipB-0",
    "mipB-1",
];

/// Create the mip views once per render invocation, not once per simulated step.
pub fn views(textures: &[&GpuImage]) -> Result<Vec<Vec<gpu::TextureView>>, String> {
    if !matches!(textures.len(), 0 | 2) {
        return Err("Ocean requires zero or two GPU textures".into());
    }
    Ok(textures
        .iter()
        .map(|texture| {
            (0..9)
                .map(|mip| {
                    texture.texture.create_view(&gpu::TextureViewDescriptor {
                        dimension: Some(gpu::TextureViewDimension::D2Array),
                        base_mip_level: mip,
                        mip_level_count: Some(1),
                        ..default()
                    })
                })
                .collect()
        })
        .collect())
}

/// Prepare the original kernels in source order; initialization-only probes need three.
pub fn pipelines(
    device: &RenderDevice,
    shaders: &[String],
    count: usize,
) -> Result<Vec<gpu::ComputePipeline>, String> {
    if !matches!(count, 3 | 9) || shaders.len() < count {
        return Err("Ocean kernel inputs are incomplete".into());
    }
    Ok(shaders[..count]
        .iter()
        .enumerate()
        .map(|(index, source)| {
            let shader = device.create_and_validate_shader_module(gpu::ShaderModuleDescriptor {
                label: Some(NAMES[index]),
                source: gpu::ShaderSource::Wgsl(source.as_str().into()),
            });
            device.create_compute_pipeline(&gpu::RawComputePipelineDescriptor {
                label: Some(NAMES[index]),
                layout: None,
                module: &shader,
                entry_point: Some("main"),
                compilation_options: default(),
                cache: None,
            })
        })
        .collect())
}

/// Inputs for one already-admitted simulation step.
pub struct Step<'a> {
    pub pipelines: &'a [gpu::ComputePipeline],
    pub buffers: &'a [&'a GpuShaderBuffer],
    pub views: &'a [Vec<gpu::TextureView>],
    pub frame: &'a [u8; 720],
    pub parameters: &'a [u8; 224],
    pub index: u32,
}

/// Encode the reference bind groups, pass order and dispatch dimensions unchanged.
#[allow(clippy::too_many_lines)]
pub fn encode(
    device: &RenderDevice,
    context: &mut RenderContext,
    step: Step<'_>,
) -> Result<(), String> {
    let count = if step.index == 0 { 3 } else { 9 };
    let complete_views = step.views.len() == 2 && step.views.iter().all(|mips| mips.len() == 9);
    let initialization_without_views = step.index == 0 && step.views.is_empty();
    if step.pipelines.len() < count
        || step.buffers.len() != 6
        || !(complete_views || initialization_without_views)
    {
        return Err("Ocean GPU step inputs are incomplete".into());
    }
    let frame_uniform = device.create_buffer_with_data(&gpu::BufferInitDescriptor {
        label: Some("source ocean frame dt"),
        contents: step.frame,
        usage: gpu::BufferUsages::UNIFORM,
    });
    let uniform = device.create_buffer_with_data(&gpu::BufferInitDescriptor {
        label: Some("source ocean step"),
        contents: step.parameters,
        usage: gpu::BufferUsages::UNIFORM,
    });
    let range = if step.index == 0 { 0..3 } else { 3..9 };
    for index in range {
        let pipeline = &step.pipelines[index];
        let frame_entries = if index == 4 {
            vec![gpu::BindGroupEntry {
                binding: 0,
                resource: frame_uniform.as_entire_binding(),
            }]
        } else {
            vec![]
        };
        let group0 = device.create_bind_group(
            NAMES[index],
            &pipeline.get_bind_group_layout(0).into(),
            &frame_entries,
        );
        let buffer_binding = |i: usize| step.buffers[i].buffer.as_entire_binding();
        let texture_binding = |texture: usize, mip: usize| {
            gpu::BindingResource::TextureView(&step.views[texture][mip])
        };
        let resources = match index {
            0 => vec![
                uniform.as_entire_binding(),
                buffer_binding(0),
                buffer_binding(1),
            ],
            1 => vec![buffer_binding(0), buffer_binding(2)],
            2 => vec![buffer_binding(0), buffer_binding(2), buffer_binding(3)],
            3 => vec![
                uniform.as_entire_binding(),
                buffer_binding(0),
                buffer_binding(1),
                buffer_binding(2),
            ],
            4 => vec![
                uniform.as_entire_binding(),
                buffer_binding(2),
                buffer_binding(3),
                buffer_binding(4),
                texture_binding(0, 0),
                texture_binding(1, 0),
            ],
            5 | 6 => {
                let texture = index - 5;
                vec![
                    buffer_binding(4),
                    buffer_binding(5),
                    texture_binding(texture, 1),
                    texture_binding(texture, 2),
                    texture_binding(texture, 3),
                    texture_binding(texture, 4),
                    texture_binding(texture, 5),
                ]
            }
            _ => {
                let texture = index - 7;
                vec![
                    buffer_binding(5),
                    texture_binding(texture, 6),
                    texture_binding(texture, 7),
                    texture_binding(texture, 8),
                ]
            }
        };
        let entries: Vec<_> = resources
            .into_iter()
            .enumerate()
            .map(|(binding, resource)| gpu::BindGroupEntry {
                binding: u32::try_from(binding).expect("small ocean binding index"),
                resource,
            })
            .collect();
        let group1 = device.create_bind_group(
            NAMES[index],
            &pipeline.get_bind_group_layout(1).into(),
            &entries,
        );
        let mut pass = context
            .command_encoder()
            .begin_compute_pass(&gpu::ComputePassDescriptor {
                label: Some(NAMES[index]),
                ..default()
            });
        pass.set_pipeline(pipeline);
        pass.set_bind_group(0, &*group0, &[]);
        pass.set_bind_group(1, &*group1, &[]);
        let (x, y, z) = match index {
            0..=2 => (16, 16, 4),
            3 | 4 => (256, 4, 1),
            5 | 6 => (8, 8, 4),
            _ => (1, 1, 4),
        };
        pass.dispatch_workgroups(x, y, z);
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn initialization_without_output_textures_needs_no_mip_views() {
        assert!(views(&[]).unwrap().is_empty());
    }
}
