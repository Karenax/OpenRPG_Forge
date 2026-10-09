//! Opt-in copies of real HDR inputs at final submission 124, never renderer inputs.
use bevy::{
    prelude::*,
    render::{
        RenderApp,
        gpu_readback::{Readback, ReadbackComplete},
        render_asset::RenderAssets,
        render_resource::{
            Buffer, BufferUsages, TexelCopyBufferInfo, TexelCopyBufferLayout, Texture,
        },
        renderer::{RenderContext, RenderQueue, ViewQuery},
        storage::{GpuShaderBuffer, ShaderBuffer},
    },
};
use std::{
    path::PathBuf,
    sync::{
        Arc, Mutex,
        atomic::{AtomicBool, Ordering},
    },
};

const HEADER: usize = 512;
const PLANES: [(&str, u32, u32); 4] = [
    ("scene-hdr", 1920, 1080),
    ("resolved", 1920, 1080),
    ("bloom-meter", 120, 68),
    ("bloom", 960, 540),
];
fn stride(width: u32) -> u32 {
    (width * 8).div_ceil(256) * 256
}
fn length() -> usize {
    HEADER
        + PLANES
            .iter()
            .map(|(_, w, h)| (stride(*w) * h) as usize)
            .sum::<usize>()
}
#[derive(Resource, Clone)]
pub(super) struct Capture {
    buffer: Handle<ShaderBuffer>,
    folder: PathBuf,
    encoded: Arc<AtomicBool>,
    armed: Arc<AtomicBool>,
    saved: Arc<AtomicBool>,
    scene_tag: Arc<Mutex<Option<(forge_engine::tidewater_temporal::frame::FrameTag, Entity)>>>,
}
pub(super) fn install(app: &mut App) {
    let Some(folder) = std::env::var_os("FORGE_TIDEWATER_FINAL_HDR_CAPTURE") else {
        return;
    };
    let folder = PathBuf::from(folder);
    assert!(!folder.exists(), "HDR capture output already exists");
    std::fs::create_dir_all(&folder).expect("HDR capture directory");
    let mut buffer = ShaderBuffer::from(vec![0_u32; length() / 4]);
    buffer.buffer_description.usage |= BufferUsages::COPY_SRC | BufferUsages::COPY_DST;
    let capture = Capture {
        buffer: app
            .world_mut()
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(buffer),
        folder,
        encoded: Arc::new(AtomicBool::new(false)),
        armed: Arc::new(AtomicBool::new(false)),
        saved: Arc::new(AtomicBool::new(false)),
        scene_tag: Arc::new(Mutex::new(None)),
    };
    app.insert_resource(capture.clone())
        .add_systems(Update, arm);
    app.get_sub_app_mut(RenderApp)
        .expect("render app")
        .insert_resource(capture);
}
fn arm(mut commands: Commands, capture: Res<Capture>) {
    if capture.encoded.load(Ordering::Acquire) && !capture.armed.swap(true, Ordering::AcqRel) {
        commands
            .spawn(Readback::buffer(capture.buffer.clone()))
            .observe(receive);
    }
}
pub(super) fn before_hdr(
    view: ViewQuery<
        (
            &bevy::render::view::ViewTarget,
            &super::History,
            &forge_engine::tidewater_temporal::frame::ViewPacket,
        ),
        With<forge_engine::tidewater_temporal::resolve::ResolveEnabled>,
    >,
    capture: Option<Res<Capture>>,
    buffers: Res<RenderAssets<GpuShaderBuffer>>,
    queue: Res<RenderQueue>,
    mut context: RenderContext,
) {
    let Some(capture) = capture else { return };
    let render_view = view.entity();
    let (target, history, packet) = view.into_inner();
    if history.submitted != 123 || capture.encoded.load(Ordering::Acquire) {
        return;
    }
    let buffer = buffers
        .get(&capture.buffer)
        .expect("Scene HDR capture buffer");
    let texture = target.main_texture();
    assert_eq!((texture.width(), texture.height()), (1920, 1080));
    assert_eq!(
        texture.format(),
        bevy::render::render_resource::TextureFormat::Rgba16Float
    );
    assert!(
        texture
            .usage()
            .contains(bevy::render::render_resource::TextureUsages::COPY_SRC)
    );
    let mut encoded_packet = bevy::render::render_resource::encase::UniformBuffer::new(Vec::new());
    encoded_packet
        .write(&packet.packet)
        .expect("View packet ABI");
    let bytes = encoded_packet.into_inner();
    assert_eq!(bytes.len(), 336);
    // Explicit diagnostic upload of the selected packet, not a copy from its bound GPU buffer.
    queue.write_buffer(&buffer.buffer, 128, &bytes);
    queue.write_buffer(&buffer.buffer, 64, &packet.tag.serial.to_le_bytes());
    queue.write_buffer(&buffer.buffer, 72, &packet.tag.view.to_bits().to_le_bytes());
    queue.write_buffer(&buffer.buffer, 80, &render_view.to_bits().to_le_bytes());
    context.command_encoder().copy_texture_to_buffer(
        texture.as_image_copy(),
        TexelCopyBufferInfo {
            buffer: &buffer.buffer,
            layout: TexelCopyBufferLayout {
                offset: HEADER as u64,
                bytes_per_row: Some(stride(1920)),
                rows_per_image: Some(1080),
            },
        },
        bevy::render::render_resource::Extent3d {
            width: 1920,
            height: 1080,
            depth_or_array_layers: 1,
        },
    );
    *capture.scene_tag.lock().expect("Scene capture tag") = Some((packet.tag, render_view));
}
pub(super) fn encode(
    capture: Option<&Capture>,
    buffers: &RenderAssets<GpuShaderBuffer>,
    context: &mut RenderContext,
    submitted: u32,
    uniform: &Buffer,
    textures: [&Texture; 3],
    identity: Option<(forge_engine::tidewater_temporal::frame::FrameTag, Entity)>,
) {
    let Some(capture) = capture else { return };
    if submitted != 124 || capture.encoded.load(Ordering::Acquire) {
        return;
    }
    let Some(buffer) = buffers.get(&capture.buffer) else {
        panic!("HDR capture buffer unavailable at image124")
    };
    assert_eq!(
        *capture.scene_tag.lock().expect("Scene capture tag"),
        identity,
        "Scene and final HDR must use the same main camera tag and render view"
    );
    assert!(
        identity.is_some(),
        "Final HDR capture needs the selected temporal camera"
    );
    for ((_, width, height), texture) in PLANES[1..].iter().zip(textures) {
        assert_eq!((texture.width(), texture.height()), (*width, *height));
        assert_eq!(
            texture.format(),
            bevy::render::render_resource::TextureFormat::Rgba16Float
        );
        assert!(
            texture
                .usage()
                .contains(bevy::render::render_resource::TextureUsages::COPY_SRC)
        );
    }
    context
        .command_encoder()
        .copy_buffer_to_buffer(uniform, 0, &buffer.buffer, 0, 64);
    let mut offset = HEADER as u64 + u64::from(stride(1920) * 1080);
    for ((_, width, height), texture) in PLANES[1..].iter().zip(textures) {
        context.command_encoder().copy_texture_to_buffer(
            texture.as_image_copy(),
            TexelCopyBufferInfo {
                buffer: &buffer.buffer,
                layout: TexelCopyBufferLayout {
                    offset,
                    bytes_per_row: Some(stride(*width)),
                    rows_per_image: Some(*height),
                },
            },
            bevy::render::render_resource::Extent3d {
                width: *width,
                height: *height,
                depth_or_array_layers: 1,
            },
        );
        offset += u64::from(stride(*width) * height);
    }
    // The callbacks copy this frozen buffer in a later submission; it is never overwritten.
    capture.encoded.store(true, Ordering::Release);
}
fn receive(
    event: On<ReadbackComplete>,
    capture: Res<Capture>,
    mut exit: MessageWriter<bevy::app::AppExit>,
) {
    if capture.saved.load(Ordering::Acquire) {
        return;
    }
    let result = (|| -> Result<(), String> {
        if event.data.len() != length() {
            return Err("HDR capture length".into());
        }
        let ordinal = u32::from_le_bytes(event.data[48..52].try_into().map_err(|_| "HDR marker")?);
        if ordinal != 124 {
            return Err("HDR capture lacks actual submission124 uniform".into());
        }
        std::fs::write(capture.folder.join("controls64.bin"), &event.data[..64])
            .map_err(|e| e.to_string())?;
        std::fs::write(capture.folder.join("packet336.bin"), &event.data[128..464])
            .map_err(|e| e.to_string())?;
        let tag_serial =
            u64::from_le_bytes(event.data[64..72].try_into().map_err(|_| "Tag serial")?);
        let view_bits = u64::from_le_bytes(event.data[72..80].try_into().map_err(|_| "Tag view")?);
        let render_view_bits =
            u64::from_le_bytes(event.data[80..88].try_into().map_err(|_| "Render view")?);
        if tag_serial == 0 {
            return Err("Scene HDR tag absent".into());
        }
        let mut offset = HEADER;
        let mut planes = Vec::new();
        for (role, width, height) in PLANES {
            let row_bytes = (width * 8) as usize;
            let padded = stride(width) as usize;
            let mut bytes = Vec::with_capacity(row_bytes * height as usize);
            for row in 0..height as usize {
                bytes.extend_from_slice(
                    &event.data[offset + row * padded..offset + row * padded + row_bytes],
                );
            }
            let name = format!("{role}.rgba16f.bin");
            std::fs::write(capture.folder.join(&name), &bytes).map_err(|e| e.to_string())?;
            planes.push(serde_json::json!({"role":role,"width":width,"height":height,"format":"rgba16float",
                "path":name,"byte_length":bytes.len(),"sha256":forge_core::bytes_sha256(&bytes).to_string()}));
            offset += padded * height as usize;
        }
        let receipt = serde_json::json!({"schema":"forge.tidewater.native-final-hdr-capture","version":3,
            "submitted":ordinal,"actual_gpu_readback":true,"planes":planes,
            "controls_sha256":forge_core::bytes_sha256(&event.data[..64]).to_string(),
            "tag_serial":tag_serial,"view_bits":view_bits.to_string(),
            "main_view_bits":view_bits.to_string(),"render_view_bits":render_view_bits.to_string(),
            "scene_and_final_tag_equal":true,
            "scene_and_final_render_view_equal":true,
            "view_identity_provenance":"Selected ViewPacket main-world tag and ViewQuery render-world entity diagnostic upload",
            "packet_sha256":forge_core::bytes_sha256(&event.data[128..464]).to_string(),
            "packet_provenance":"Selected ViewPacket diagnostic upload, not bound-buffer copy",
            "source_frame_history_paired":false,"visual_parity":false});
        std::fs::write(
            capture.folder.join("planes.json"),
            serde_json::to_vec_pretty(&receipt).map_err(|e| e.to_string())?,
        )
        .map_err(|e| e.to_string())?;
        capture.saved.store(true, Ordering::Release);
        Ok(())
    })();
    if let Err(error) = result {
        error!(%error,"Native final HDR capture failed");
        exit.write(bevy::app::AppExit::error());
    }
}
