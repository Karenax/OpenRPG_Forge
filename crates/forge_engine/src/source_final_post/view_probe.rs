//! Opt-in GPU readback and explicit surface counterfactuals, never a renderer fix.
#[path = "view_probe/plant.rs"]
mod plant;
use bevy::{
    core_pipeline::{Core3dSystems, schedule::Core3d},
    prelude::*,
    render::{
        Render, RenderApp, RenderSystems,
        gpu_readback::{Readback, ReadbackComplete},
        render_asset::RenderAssets,
        render_resource::{
            Buffer, BufferUsages, Extent3d, Origin3d, ShaderType, TexelCopyBufferInfo,
            TexelCopyBufferLayout, Texture, TextureAspect, TextureFormat,
        },
        renderer::{RenderContext, ViewQuery},
        storage::{GpuShaderBuffer, ShaderBuffer},
        sync_world::MainEntity,
        view::{ViewDepthTexture, ViewTarget, ViewUniform, ViewUniformOffset, ViewUniforms},
    },
};
use std::{
    path::PathBuf,
    sync::{
        Arc, Mutex,
        atomic::{AtomicBool, Ordering},
    },
};

const HEADER: usize = 4096;
const VIEW_OFFSET: usize = 256;
const PLANES: [(&str, u32, u32, u32); 4] = [
    ("before-post", 128, 160, 384),
    ("input", 128, 160, 384),
    ("output", 128, 160, 384),
    ("bloom", 64, 80, 192),
];
const DEPTH_OFFSET: usize = HEADER + (384 * 384 * 3 + 192 * 192) * 8;
// Depth copies must cover the whole subresource. This opt-in fixture uses a
// fixed 1280x800 target; crop the requested area only after GPU readback.
const DEPTH_SIZE: [u32; 2] = [1280, 800];
const LENGTH: usize = DEPTH_OFFSET + 1280 * 800 * 4;

#[derive(Clone, Debug, PartialEq, Eq, serde::Serialize)]
struct FrameKey {
    frame: u32,
    render_view: u64,
    main_view: u64,
    target_size: [u32; 2],
}
#[derive(Clone, Copy, serde::Serialize)]
#[serde(rename_all = "lowercase")]
enum TerrainMode {
    Coverage,
    Albedo,
}
#[derive(Default, serde::Serialize)]
struct Metadata {
    before: Option<FrameKey>,
    encoded: Option<FrameKey>,
    view_uniform_offset: Option<u32>,
    view_uniform_bytes: usize,
    grass_coverage_materials: Option<usize>,
    terrain_probe_mode: Option<TerrainMode>,
    terrain_probe_materials: Option<usize>,
    plant_probe_mode: Option<String>,
    plant_shader_original_sha256: Option<String>,
    plant_shader_diagnostic_sha256: Option<String>,
}

#[derive(Resource, Clone)]
pub(super) struct Probe {
    packet: Handle<ShaderBuffer>,
    folder: PathBuf,
    encoded: Arc<AtomicBool>,
    armed: Arc<AtomicBool>,
    saved: Arc<AtomicBool>,
    metadata: Arc<Mutex<Metadata>>,
}

pub(super) fn install(app: &mut App) {
    let Some(folder) = std::env::var_os("FORGE_SOURCE_FINAL_VIEW_PROBE") else {
        return;
    };
    let terrain_mode = match std::env::var_os("FORGE_SOURCE_FINAL_TERRAIN_PROBE") {
        None => None,
        Some(value) if value == "coverage" => Some(TerrainMode::Coverage),
        Some(value) if value == "albedo" => Some(TerrainMode::Albedo),
        Some(_) => {
            error!("Terrain diagnostic mode must be coverage or albedo");
            app.add_systems(Startup, |mut exit: MessageWriter<bevy::app::AppExit>| {
                exit.write(bevy::app::AppExit::error());
            });
            return;
        }
    };
    let folder = PathBuf::from(folder);
    std::fs::create_dir(&folder).expect("Fresh final view probe folder with existing parent");
    let mut buffer = ShaderBuffer::from(vec![0_u32; LENGTH / 4]);
    buffer.buffer_description.usage |= BufferUsages::COPY_SRC | BufferUsages::COPY_DST;
    let grass_coverage =
        std::env::var_os("FORGE_SOURCE_FINAL_GRASS_COVERAGE").is_some_and(|value| value == "1");
    let probe = Probe {
        packet: app
            .world_mut()
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(buffer),
        folder,
        encoded: Arc::new(AtomicBool::new(false)),
        armed: Arc::new(AtomicBool::new(false)),
        saved: Arc::new(AtomicBool::new(false)),
        metadata: Arc::new(Mutex::new(Metadata {
            grass_coverage_materials: grass_coverage.then_some(0),
            terrain_probe_mode: terrain_mode,
            terrain_probe_materials: terrain_mode.map(|_| 0),
            ..default()
        })),
    };
    if grass_coverage {
        app.add_systems(Last, mark_grass_coverage);
    }
    if terrain_mode.is_some() {
        app.add_systems(Last, mark_terrain_probe);
    }
    plant::install(app, &probe);
    app.insert_resource(probe.clone()).add_systems(Update, arm);
    app.get_sub_app_mut(RenderApp)
        .expect("View probe render app")
        .insert_resource(probe)
        .add_systems(
            Render,
            prepare_view_copy
                .in_set(RenderSystems::PrepareResources)
                .before(bevy::render::view::prepare_view_uniforms),
        )
        .add_systems(
            Core3d,
            before_post
                .after(Core3dSystems::MainPass)
                .before(Core3dSystems::EarlyPostProcess)
                .before(Core3dSystems::PostProcess)
                .before(crate::source_temporal::hdr::SourceHdrSystems::Gtao),
        );
}

// Preserve geometry, resources and authored data; use the two existing source
// terrain diagnostic switches rather than introducing another shader.
fn mark_terrain_probe(
    materials: Option<ResMut<Assets<crate::source_terrain::lod::LodMaterial>>>,
    probe: Res<Probe>,
) {
    let Some(mut materials) = materials else {
        return;
    };
    let mode = probe
        .metadata
        .lock()
        .expect("View probe metadata")
        .terrain_probe_mode;
    let Some(mode) = mode else { return };
    let flags = match mode {
        TerrainMode::Coverage => [0.0_f32, 1.0_f32],
        TerrainMode::Albedo => [1.0_f32, 0.0_f32],
    };
    let selected: Vec<_> = materials.iter().map(|(id, _)| id).collect();
    for &id in &selected {
        let current = materials
            .get(id)
            .expect("Resident terrain material")
            .base
            .settings
            .flags;
        if [current.x.to_bits(), current.z.to_bits()] != flags.map(f32::to_bits) {
            let mut material = materials.get_mut(id).expect("Resident terrain material");
            material.base.settings.flags.x = flags[0];
            material.base.settings.flags.z = flags[1];
        }
    }
    probe
        .metadata
        .lock()
        .expect("View probe metadata")
        .terrain_probe_materials = Some(selected.len());
}

// Reuse the source's existing coverage output, after its surface/alpha code.
// This is an explicit counterfactual for attribution, never a renderer fix.
fn mark_grass_coverage(
    materials: Option<ResMut<Assets<crate::source_vegetation::world::WorldPlantMaterial>>>,
    probe: Res<Probe>,
) {
    let Some(mut materials) = materials else {
        return;
    };
    let selected: Vec<_> = materials
        .iter()
        .filter(|(_, material)| material.kind == 3 && material.source_lighting)
        .map(|(id, material)| {
            (
                id,
                material.params.lod_end_frame.w.to_bits() != 1.0_f32.to_bits(),
            )
        })
        .collect();
    for &(id, changed) in &selected {
        if changed {
            materials
                .get_mut(id)
                .expect("Selected resident grass material")
                .params
                .lod_end_frame
                .w = 1.0;
        }
    }
    probe
        .metadata
        .lock()
        .expect("View probe metadata")
        .grass_coverage_materials = Some(selected.len());
}

fn prepare_view_copy(mut uniforms: ResMut<ViewUniforms>) {
    if uniforms
        .uniforms
        .buffer()
        .is_none_or(|b| !b.usage().contains(BufferUsages::COPY_SRC))
    {
        uniforms.uniforms.add_usages(BufferUsages::COPY_SRC);
    }
}

fn before_post(
    view: ViewQuery<(&ViewTarget, &super::History, &MainEntity), With<super::Controls>>,
    probe: Res<Probe>,
    buffers: Res<RenderAssets<GpuShaderBuffer>>,
    frame: Res<super::observation::Frame>,
    mut context: RenderContext,
) {
    let entity = view.entity();
    let (target, history, main) = view.into_inner();
    if history.submitted != 123 || probe.encoded.load(Ordering::Acquire) {
        return;
    }
    let Some(buffer) = buffers.get(&probe.packet) else {
        return;
    };
    copy_plane(
        &mut context,
        &buffer.buffer,
        target.main_texture(),
        HEADER as u64,
        [128, 160],
        [384, 384],
    );
    probe.metadata.lock().expect("View probe metadata").before = Some(FrameKey {
        frame: frame.0,
        render_view: entity.to_bits(),
        main_view: main.id().to_bits(),
        target_size: [
            target.main_texture().width(),
            target.main_texture().height(),
        ],
    });
}

fn copy_plane(
    context: &mut RenderContext,
    destination: &Buffer,
    texture: &Texture,
    offset: u64,
    origin: [u32; 2],
    size: [u32; 2],
) {
    let depth = texture.format() == TextureFormat::Depth32Float;
    assert!(depth || texture.format() == TextureFormat::Rgba16Float);
    assert!(texture.width() >= origin[0] + size[0] && texture.height() >= origin[1] + size[1]);
    if depth {
        assert_eq!(origin, [0, 0]);
        assert_eq!(size, [texture.width(), texture.height()]);
    }
    assert_eq!(texture.sample_count(), 1);
    assert!(
        texture
            .usage()
            .contains(bevy::render::render_resource::TextureUsages::COPY_SRC)
    );
    let mut source = texture.as_image_copy();
    source.origin = Origin3d {
        x: origin[0],
        y: origin[1],
        z: 0,
    };
    if depth {
        source.aspect = TextureAspect::DepthOnly;
    }
    context.command_encoder().copy_texture_to_buffer(
        source,
        TexelCopyBufferInfo {
            buffer: destination,
            layout: TexelCopyBufferLayout {
                offset,
                bytes_per_row: Some(size[0] * if depth { 4 } else { 8 }),
                rows_per_image: Some(size[1]),
            },
        },
        Extent3d {
            width: size[0],
            height: size[1],
            depth_or_array_layers: 1,
        },
    );
}

fn arm(mut commands: Commands, probe: Res<Probe>) {
    if probe.encoded.load(Ordering::Acquire) && !probe.armed.swap(true, Ordering::AcqRel) {
        commands
            .spawn(Readback::buffer(probe.packet.clone()))
            .observe(receive);
    }
}

#[allow(clippy::too_many_arguments)]
pub(super) fn encode(
    probe: Option<&Probe>,
    buffers: &RenderAssets<GpuShaderBuffer>,
    context: &mut RenderContext,
    submitted: u32,
    uniform: &Buffer,
    exposure: &Buffer,
    textures: [&Texture; 3],
    view: (
        &ViewDepthTexture,
        Option<&ViewUniformOffset>,
        Option<&ViewUniforms>,
    ),
    identity: (Entity, Entity, u32),
) {
    let Some(probe) = probe else { return };
    if submitted != 124 || probe.encoded.load(Ordering::Acquire) {
        return;
    }
    let buffer = buffers
        .get(&probe.packet)
        .expect("View probe GPU buffer at submission124");
    let key = FrameKey {
        frame: identity.2,
        render_view: identity.0.to_bits(),
        main_view: identity.1.to_bits(),
        target_size: [textures[0].width(), textures[0].height()],
    };
    let offset = view.1.expect("Selected view uniform offset");
    let uniforms = view
        .2
        .and_then(|v| v.uniforms.buffer())
        .expect("Selected GPU view uniforms");
    let length = usize::try_from(ViewUniform::min_size().get()).expect("View uniform length");
    assert!(length <= HEADER - VIEW_OFFSET);
    assert!(uniforms.usage().contains(BufferUsages::COPY_SRC));
    {
        let mut meta = probe.metadata.lock().expect("View probe metadata");
        assert_eq!(
            meta.before,
            Some(key.clone()),
            "Before-post and final copies must belong to the same rendered view/frame"
        );
        meta.encoded = Some(key);
        meta.view_uniform_offset = Some(offset.offset);
        meta.view_uniform_bytes = length;
    }
    for ((_, x, y, side), texture) in PLANES[1..].iter().zip(textures) {
        assert!(
            texture.width() >= x + side && texture.height() >= y + side,
            "View probe crop outside target"
        );
        assert_eq!(texture.format(), TextureFormat::Rgba16Float);
        assert_eq!(texture.sample_count(), 1);
        assert!(
            texture
                .usage()
                .contains(bevy::render::render_resource::TextureUsages::COPY_SRC)
        );
    }
    let encoder = context.command_encoder();
    encoder.copy_buffer_to_buffer(uniform, 0, &buffer.buffer, 0, 64);
    encoder.copy_buffer_to_buffer(exposure, 0, &buffer.buffer, 64, 16);
    encoder.copy_buffer_to_buffer(
        uniforms,
        u64::from(offset.offset),
        &buffer.buffer,
        VIEW_OFFSET as u64,
        length as u64,
    );
    let mut plane_offset = (HEADER + 384 * 384 * 8) as u64;
    for ((_, x, y, side), texture) in PLANES[1..].iter().zip(textures) {
        copy_plane(
            context,
            &buffer.buffer,
            texture,
            plane_offset,
            [*x, *y],
            [*side, *side],
        );
        plane_offset += u64::from(side * side * 8);
    }
    assert_eq!(view.0.texture.format(), TextureFormat::Depth32Float);
    copy_plane(
        context,
        &buffer.buffer,
        &view.0.texture,
        DEPTH_OFFSET as u64,
        [0, 0],
        DEPTH_SIZE,
    );
    probe.encoded.store(true, Ordering::Release);
}

fn receive(
    event: On<ReadbackComplete>,
    probe: Res<Probe>,
    mut exit: MessageWriter<bevy::app::AppExit>,
    mut commands: Commands,
) {
    if probe.saved.swap(true, Ordering::AcqRel) {
        return;
    }
    commands.entity(event.entity).despawn();
    let result = (|| -> Result<(), String> {
        if event.data.len() != LENGTH {
            return Err("View probe length mismatch".into());
        }
        let ordinal = u32::from_le_bytes(
            event.data[48..52]
                .try_into()
                .map_err(|_| "View probe marker")?,
        );
        if ordinal != 124 {
            return Err("View probe lacks submission124 controls".into());
        }
        std::fs::write(probe.folder.join("controls64.bin"), &event.data[..64])
            .map_err(|e| e.to_string())?;
        std::fs::write(probe.folder.join("exposure16.bin"), &event.data[64..80])
            .map_err(|e| e.to_string())?;
        let meta = probe.metadata.lock().map_err(|_| "View probe metadata")?;
        if meta.before != meta.encoded || meta.encoded.is_none() {
            return Err("View probe frame mismatch".into());
        }
        let view_data = &event.data[VIEW_OFFSET..VIEW_OFFSET + meta.view_uniform_bytes];
        std::fs::write(probe.folder.join("view-uniforms.bin"), view_data)
            .map_err(|e| e.to_string())?;
        let mut offset = HEADER;
        let mut planes = Vec::new();
        for (role, x, y, side) in PLANES {
            let length = (side * side * 8) as usize;
            let data = &event.data[offset..offset + length];
            let path = format!("{role}.rgba16f.bin");
            std::fs::write(probe.folder.join(&path), data).map_err(|e| e.to_string())?;
            planes.push(
                serde_json::json!({"role":role,"origin":[x,y],"size":[side,side],"path":path,
                "bytes":length,"sha256":forge_core::bytes_sha256(data).to_string()}),
            );
            offset += length;
        }
        let depth_data: Vec<u8> = event.data[DEPTH_OFFSET..]
            .as_chunks::<{ 1280 * 4 }>()
            .0
            .iter()
            .skip(160)
            .take(384)
            .flat_map(|row| row[128 * 4..512 * 4].iter().copied())
            .collect();
        std::fs::write(probe.folder.join("depth32.bin"), &depth_data).map_err(|e| e.to_string())?;
        let report = serde_json::json!({"schema":"forge.source.final-view-probe.v2","submitted":ordinal,
            "actual_gpu_readback":true,"format":"rgba16float","planes":planes,
            "depth":{"origin":[128,160],"size":[384,384],"gpu_copy_size":DEPTH_SIZE,"crop_after_readback":true,"format":"depth32float","path":"depth32.bin","bytes":depth_data.len(),"sha256":forge_core::bytes_sha256(&depth_data).to_string()},
            "view_uniform":{"path":"view-uniforms.bin","bytes":view_data.len(),"sha256":forge_core::bytes_sha256(view_data).to_string(),"provenance":"GPU copy of selected ViewUniforms buffer at selected ViewUniformOffset"},
            "view_frame_metadata":&*meta,
            "scope":"Before-post and final crops share the selected rendered view/frame; depth and bound view uniform copied on GPU; controls/exposure before metering; no screenshot pairing claim",
            "performance_qualified":false,"visual_qualified":false});
        std::fs::write(
            probe.folder.join("planes.json"),
            serde_json::to_vec_pretty(&report).map_err(|e| e.to_string())?,
        )
        .map_err(|e| e.to_string())
    })();
    if let Err(error) = result {
        error!(%error,"Final view probe failed");
        exit.write(bevy::app::AppExit::error());
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::source_final_post::{Controls, prepare_depth};
    use bevy::render::render_resource::TextureUsages;

    #[test]
    fn opt_in_probe_prepares_copyable_depth_without_touching_other_cameras() {
        let mut app = App::new();
        // This marker fixture never submits work or claims GPU preparation.
        app.insert_resource(Probe {
            packet: Handle::default(),
            folder: PathBuf::new(),
            encoded: Arc::new(AtomicBool::new(false)),
            armed: Arc::new(AtomicBool::new(false)),
            saved: Arc::new(AtomicBool::new(false)),
            metadata: Arc::new(Mutex::new(Metadata::default())),
        })
        .add_systems(Update, prepare_depth);
        let controls = Controls::for_quality(forge_project::PostProcessQualitySettings {
            tonemapping_enabled: true,
            bloom_enabled: true,
            color_grading_enabled: true,
        })
        .unwrap()
        .unwrap();
        let camera = || Camera3d {
            depth_texture_usages: TextureUsages::RENDER_ATTACHMENT.into(),
            ..default()
        };
        let selected = app.world_mut().spawn((camera(), controls)).id();
        let other = app.world_mut().spawn(camera()).id();
        app.update();
        let usages = TextureUsages::from(
            app.world()
                .get::<Camera3d>(selected)
                .unwrap()
                .depth_texture_usages,
        );
        assert!(usages.contains(
            TextureUsages::RENDER_ATTACHMENT
                | TextureUsages::TEXTURE_BINDING
                | TextureUsages::COPY_SRC
        ));
        assert_eq!(
            TextureUsages::from(
                app.world()
                    .get::<Camera3d>(other)
                    .unwrap()
                    .depth_texture_usages
            ),
            TextureUsages::RENDER_ATTACHMENT
        );
    }
}
