//! Read actual imported GPU storage through the existing renderer device/queue.
use bevy::{
    prelude::*,
    render::{
        RenderApp,
        render_asset::RenderAssets,
        renderer::{RenderDevice, RenderQueue},
        texture::GpuImage,
    },
};
use forge_core::bytes_sha256;
use std::{
    path::Path,
    sync::mpsc,
    time::{Duration, Instant},
};

pub(super) fn read(
    app: &App,
    handles: &[Handle<Image>],
    metadata: &[super::Texture],
    output: &Path,
) -> Result<Vec<serde_json::Value>, String> {
    let world = app
        .get_sub_app(RenderApp)
        .ok_or("Renderer missing")?
        .world();
    let device = world.resource::<RenderDevice>();
    let queue = world.resource::<RenderQueue>();
    let images = world.resource::<RenderAssets<GpuImage>>();
    let mut rows = Vec::new();
    for (slot, (handle, source)) in handles.iter().zip(metadata).enumerate() {
        let gpu = images.get(handle).ok_or("GPU texture missing")?;
        if !gpu
            .texture_descriptor
            .usage
            .contains(wgpu::TextureUsages::COPY_SRC)
        {
            return Err("GPU texture does not admit storage readback".into());
        }
        let format = gpu.texture_descriptor.format;
        let block_bytes = format
            .block_copy_size(None)
            .ok_or("Unsupported source texture aspect")?;
        let (block_width, block_height) = format.block_dimensions();
        for (level, mip) in source.mips.iter().enumerate() {
            let block_rows = mip.height.div_ceil(block_height);
            let row_bytes = mip.width.div_ceil(block_width) * block_bytes;
            let row_stride = row_bytes.next_multiple_of(wgpu::COPY_BYTES_PER_ROW_ALIGNMENT);
            let data = read_mip(
                device,
                queue,
                gpu,
                u32::try_from(level).map_err(|_| "Mip index overflow")?,
                mip.width.div_ceil(block_width) * block_width,
                block_rows * block_height,
                row_bytes,
                row_stride,
                block_rows,
            )?;
            let filename = format!("texture-{slot}-mip-{level}.GPU-native.bin");
            std::fs::write(output.join(&filename), &data).map_err(|e| e.to_string())?;
            let actual = bytes_sha256(&data);
            let exact = actual == mip.sha256 && data.len() as u64 == mip.bytes;
            rows.push(serde_json::json!({"slot":slot,"mip":level,"bytes":data.len(),"file":filename,"sha256":actual.to_string(),"native_sha256":mip.sha256.to_string(),"GPU_storage_native_bytes_exact":exact}));
        }
    }
    Ok(rows)
}

#[allow(
    clippy::too_many_arguments,
    reason = "Explicit block-aligned source mip and padded copy layout"
)]
fn read_mip(
    device: &RenderDevice,
    queue: &RenderQueue,
    image: &GpuImage,
    mip: u32,
    width: u32,
    height: u32,
    row_bytes: u32,
    row_stride: u32,
    rows: u32,
) -> Result<Vec<u8>, String> {
    let size = u64::from(row_stride) * u64::from(rows);
    if size == 0 || size > 64 * 1024 * 1024 {
        return Err("Mip readback byte budget exceeded".into());
    }
    let gpu = device.wgpu_device();
    let buffer = gpu.create_buffer(&wgpu::BufferDescriptor {
        label: Some("source-native-mip-readback"),
        size,
        usage: wgpu::BufferUsages::COPY_DST | wgpu::BufferUsages::MAP_READ,
        mapped_at_creation: false,
    });
    let mut encoder = gpu.create_command_encoder(&wgpu::CommandEncoderDescriptor::default());
    encoder.copy_texture_to_buffer(
        wgpu::TexelCopyTextureInfo {
            texture: &image.texture,
            mip_level: mip,
            origin: wgpu::Origin3d::ZERO,
            aspect: wgpu::TextureAspect::All,
        },
        wgpu::TexelCopyBufferInfo {
            buffer: &buffer,
            layout: wgpu::TexelCopyBufferLayout {
                offset: 0,
                bytes_per_row: Some(row_stride),
                rows_per_image: Some(rows),
            },
        },
        wgpu::Extent3d {
            width,
            height,
            depth_or_array_layers: 1,
        },
    );
    queue.submit([encoder.finish()]);
    let (tx, rx) = mpsc::sync_channel(1);
    buffer.slice(..).map_async(wgpu::MapMode::Read, move |r| {
        let _ = tx.send(r);
    });
    let deadline = Instant::now() + Duration::from_secs(15);
    loop {
        gpu.poll(wgpu::PollType::Poll).map_err(|e| e.to_string())?;
        match rx.try_recv() {
            Ok(result) => {
                result.map_err(|e| e.to_string())?;
                break;
            }
            Err(mpsc::TryRecvError::Disconnected) => {
                return Err("Mip map result channel lost".into());
            }
            Err(mpsc::TryRecvError::Empty) if Instant::now() > deadline => {
                return Err("Mip map readback timeout".into());
            }
            Err(mpsc::TryRecvError::Empty) => std::thread::sleep(Duration::from_millis(2)),
        }
    }
    let view = buffer.slice(..).get_mapped_range();
    let mut data = Vec::new();
    for row in 0..rows {
        let start = usize::try_from(u64::from(row) * u64::from(row_stride))
            .map_err(|_| "Row offset overflow")?;
        let end = start + usize::try_from(row_bytes).map_err(|_| "Row length overflow")?;
        data.extend_from_slice(view.get(start..end).ok_or("Padded mip row truncated")?);
    }
    drop(view);
    buffer.unmap();
    Ok(data)
}
