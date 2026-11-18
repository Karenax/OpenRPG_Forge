use super::{Pending, Progress, Staged, samplers};
use crate::source_environment::{
    controller::Controller,
    programs::{cycle::Resources, encode::FrameBindings},
};
use bevy::{
    prelude::*,
    render::{
        render_asset::RenderAssets,
        render_resource::BufferId,
        renderer::{RenderContext, RenderDevice},
        storage::GpuShaderBuffer,
        texture::GpuImage,
    },
};
use std::sync::{Arc, atomic::Ordering};

#[derive(Default)]
pub(super) struct Execution {
    generation: Option<Arc<Progress>>,
    controller: Controller,
    samplers: Vec<wgpu::Sampler>,
    direct_light: Option<(BufferId, Arc<()>, [u8; 32])>,
}

#[allow(
    clippy::too_many_lines,
    reason = "Resolve the coherent frame and all resident resources before accepting commands"
)]
#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy render system parameters are consumed by the scheduler"
)]
pub(super) fn compute(
    mut context: RenderContext,
    mut execution: Local<Execution>,
    staged: Res<Staged>,
    pending: Res<Pending>,
    device: Res<RenderDevice>,
    images: Res<RenderAssets<GpuImage>>,
    buffers: Res<RenderAssets<GpuShaderBuffer>>,
) {
    let Some(frame) = pending.0.as_ref() else {
        return;
    };
    if !staged.progress.active.load(Ordering::Acquire) {
        return;
    }
    if staged.progress.encoded.load(Ordering::Acquire) >= frame.id
        || staged.progress.failure.lock().is_ok_and(|f| f.is_some())
    {
        return;
    }
    if !staged.atmosphere.ready.load(Ordering::Acquire)
        || !staged.clouds.ready.load(Ordering::Acquire)
    {
        return;
    }
    let cloud_step = staged.clouds.completed_steps.load(Ordering::Acquire);
    if cloud_step < frame.input.cloud_step {
        return;
    }
    let fail = |error: String| {
        if let Ok(mut f) = staged.progress.failure.lock() {
            *f = Some(error);
        }
    };
    if cloud_step != frame.input.cloud_step {
        fail("Environment cloud frame was superseded".into());
        return;
    }
    let input = &frame.input;
    let Some(source) = images.get(&staged.targets.source) else {
        return;
    };
    let Some(displayed) = images.get(&staged.targets.displayed) else {
        return;
    };
    let (Some(a), Some(b), Some(sh_a), Some(sh_b)) = (
        images.get(&staged.targets.filtered[0]),
        images.get(&staged.targets.filtered[1]),
        buffers.get(&staged.targets.sh[0]),
        buffers.get(&staged.targets.sh[1]),
    ) else {
        return;
    };
    if *displayed.texture == *source.texture
        || *displayed.texture == *a.texture
        || *displayed.texture == *b.texture
        || displayed.texture.size()
            != (wgpu::Extent3d {
                width: 128,
                height: 128,
                depth_or_array_layers: 6,
            })
        || displayed.texture.format() != wgpu::TextureFormat::Rgba16Float
        || displayed.texture.mip_level_count() != 6
        || displayed.texture.sample_count() != 1
        || !displayed
            .texture
            .usage()
            .contains(wgpu::TextureUsages::COPY_DST | wgpu::TextureUsages::TEXTURE_BINDING)
        || !a.texture.usage().contains(wgpu::TextureUsages::COPY_SRC)
        || !b.texture.usage().contains(wgpu::TextureUsages::COPY_SRC)
    {
        fail("Displayed environment cube ABI differs or aliases a working target".into());
        return;
    }
    let handles = [
        &staged.atmosphere.images[0],
        &staged.atmosphere.images[2],
        &staged.clouds.images[2],
        &staged.clouds.images[9],
        &staged.clouds.images[if input.cloud_view == 0 { 5 } else { 7 }],
    ];
    let views: Vec<_> = handles.into_iter().filter_map(|h| images.get(h)).collect();
    if views.len() != 5 {
        return;
    }
    let destination = if let Some(destination) = &staged.lighting {
        let Some(buffer) = buffers.get(&destination.handle) else {
            return;
        };
        if buffer.buffer.size() != 176
            || !buffer.buffer.usage().contains(wgpu::BufferUsages::COPY_DST)
        {
            fail("Scene lighting destination GPU ABI differs".into());
            return;
        }
        Some((buffer, &destination.lease))
    } else {
        None
    };
    if !execution
        .generation
        .as_ref()
        .is_some_and(|g| Arc::ptr_eq(g, &staged.progress))
    {
        *execution = Execution {
            generation: Some(staged.progress.clone()),
            controller: Controller::default(),
            samplers: samplers::create(device.wgpu_device()),
            direct_light: None,
        };
    }
    let plan = match execution.controller.plan(input.dt, input.sun, input.force) {
        Ok(p) => p,
        Err(e) => {
            fail(e);
            return;
        }
    };
    let refresh = !plan.steps().is_empty();
    if !refresh
        && (*a.texture == *b.texture
            || *sh_a.buffer == *sh_b.buffer
            || *source.texture == *a.texture
            || *source.texture == *b.texture)
    {
        fail("Environment front/back resources alias".into());
        return;
    }
    let mut direct_update = None;
    if let Some((destination, lease)) = destination {
        let direct = match super::lighting::direct(&input.global) {
            Ok(d) => d,
            Err(e) => {
                fail(e);
                return;
            }
        };
        let id = destination.buffer.id();
        // Rebinding or replacing the buffer requires the current light again.
        if execution
            .direct_light
            .as_ref()
            .is_none_or(|(previous_id, previous_lease, previous)| {
                *previous_id != id || !Arc::ptr_eq(previous_lease, lease) || *previous != direct
            })
        {
            direct_update = Some((destination, id, lease, direct));
        }
    }
    // Idle plans still advance the refresh timer and acknowledge the cloud frame.
    let mut encoder = (refresh || direct_update.is_some()).then(|| {
        device.create_command_encoder(&wgpu::CommandEncoderDescriptor {
            label: Some("environment refresh"),
        })
    });
    let uniform = |label: &'static str, contents: &[u8]| {
        device.create_buffer_with_data(&wgpu::util::BufferInitDescriptor {
            label: Some(label),
            contents,
            usage: wgpu::BufferUsages::UNIFORM,
        })
    };
    let pack = |values: &[Vec4]| {
        values
            .iter()
            .flat_map(Vec4::to_array)
            .flat_map(f32::to_le_bytes)
            .collect::<Vec<_>>()
    };
    let front = if refresh {
        let global = uniform("environment frame", &input.global);
        let atmosphere = uniform("environment atmosphere", &pack(&input.atmosphere));
        let clouds = uniform("environment clouds", &pack(&input.clouds));
        let sky = uniform("environment sky", &pack(&input.sky));
        let result = staged.programs.encode_cycle(
            &execution.controller,
            &plan,
            device.wgpu_device(),
            encoder.as_mut().expect("refresh encoder"),
            FrameBindings {
                frame: &global,
                samplers: &execution.samplers,
            },
            Resources {
                atmosphere: &atmosphere,
                textures: [
                    &views[0].texture_view,
                    &views[1].texture_view,
                    &views[2].texture_view,
                    &views[3].texture_view,
                    &views[4].texture_view,
                ],
                clouds: &clouds,
                sky: &sky,
                source: &source.texture,
                filtered: [&a.texture, &b.texture],
                sh: [&sh_a.buffer, &sh_b.buffer],
            },
        );
        match result {
            Ok(p) => p,
            Err(e) => {
                fail(e);
                return;
            }
        }
    } else {
        None
    };
    if let Some((buffer, _, _, direct)) = direct_update {
        let prefix = device.create_buffer_with_data(&wgpu::util::BufferInitDescriptor {
            label: Some("current scene key light"),
            contents: &direct,
            usage: wgpu::BufferUsages::COPY_SRC,
        });
        encoder
            .as_mut()
            .expect("direct light encoder")
            .copy_buffer_to_buffer(&prefix, 0, &buffer.buffer, 0, 32);
    }
    if let Some(front) = front {
        let encoder = encoder.as_mut().expect("publication encoder");
        let completed = if front == 0 { a } else { b };
        for level in 0..6 {
            encoder.copy_texture_to_texture(
                wgpu::TexelCopyTextureInfo {
                    texture: &completed.texture,
                    mip_level: level,
                    origin: wgpu::Origin3d::ZERO,
                    aspect: wgpu::TextureAspect::All,
                },
                wgpu::TexelCopyTextureInfo {
                    texture: &displayed.texture,
                    mip_level: level,
                    origin: wgpu::Origin3d::ZERO,
                    aspect: wgpu::TextureAspect::All,
                },
                wgpu::Extent3d {
                    width: 128 >> level,
                    height: 128 >> level,
                    depth_or_array_layers: 6,
                },
            );
        }
        if let Some((destination, _)) = destination {
            let source = if front == 0 { sh_a } else { sh_b };
            encoder.copy_buffer_to_buffer(&source.buffer, 0, &destination.buffer, 32, 144);
        }
    }
    if !staged.progress.active.load(Ordering::Acquire) {
        return;
    }
    if let Some(encoder) = encoder {
        // Queue only successful commands after the atmosphere and cloud work.
        context.add_command_buffer(encoder.finish());
    }
    if let Err(e) = execution.controller.commit(plan) {
        fail(e);
        return;
    }
    if let Some((_, id, lease, direct)) = direct_update {
        execution.direct_light = Some((id, lease.clone(), direct));
    }
    if let Some(front) = front {
        staged
            .progress
            .published
            .store((frame.id << 1) | u64::from(front), Ordering::Release);
    }
    staged.progress.encoded.store(frame.id, Ordering::Release);
}
