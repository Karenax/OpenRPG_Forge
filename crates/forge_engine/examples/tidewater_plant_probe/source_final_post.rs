//! Candidate source bloom, final grading and next-frame exposure on Bevy's device.
// SPDX-License-Identifier: MIT
// Source algorithms: Tidewater 1438b1ab, copyright 2026 DRG Software Solutions LLC.
// See third_party/tidewater/LICENSE. No captured exposure or image is injected.
#![allow(clippy::needless_pass_by_value)]

use bevy::{
    asset::{load_internal_asset, uuid_handle},
    core_pipeline::{Core3dSystems, FullscreenShader, schedule::Core3d},
    prelude::*,
    render::{
        Render, RenderApp, RenderStartup, RenderSystems,
        extract_component::{ExtractComponent, ExtractComponentPlugin},
        render_asset::RenderAssets,
        render_resource::{
            BindGroup, BindGroupEntries, BindGroupLayoutDescriptor, BindGroupLayoutEntries, Buffer,
            BufferDescriptor, BufferUsages, CachedComputePipelineId, ColorTargetState, ColorWrites,
            ComputePassDescriptor, ComputePipelineDescriptor, Extent3d, FilterMode, FragmentState,
            MipmapFilterMode, Operations, PipelineCache, RenderPassColorAttachment,
            RenderPassDescriptor, RenderPipeline, RenderPipelineDescriptor, Sampler,
            SamplerBindingType, SamplerDescriptor, ShaderStages, ShaderType,
            SpecializedRenderPipeline, SpecializedRenderPipelines, Texture, TextureDescriptor,
            TextureDimension, TextureFormat, TextureSampleType, TextureUsages, TextureView,
            TextureViewDescriptor,
            binding_types::{
                sampler, storage_buffer, storage_buffer_read_only, texture_2d, texture_depth_2d,
                uniform_buffer,
            },
        },
        renderer::{RenderContext, RenderDevice, RenderQueue, ViewQuery},
        storage::GpuShaderBuffer,
        view::{ViewDepthTexture, ViewTarget},
    },
    shader::Shader,
};
use forge_engine::tidewater_temporal::{
    frame::ViewPacket,
    resolve::{ResolveEnabled, ResolveObservations},
};

#[path = "source_final_post/capture.rs"]
mod capture;
#[path = "source_final_post/clock.rs"]
mod clock;
#[path = "source_final_post/trace.rs"]
mod trace;

const FIRST: Handle<Shader> = uuid_handle!("1123ba1a-87e2-4836-920c-6c68f3aa2c01");
const DOWN: Handle<Shader> = uuid_handle!("1123ba1a-87e2-4836-920c-6c68f3aa2c02");
const UP: Handle<Shader> = uuid_handle!("1123ba1a-87e2-4836-920c-6c68f3aa2c03");
const FINAL: Handle<Shader> = uuid_handle!("1123ba1a-87e2-4836-920c-6c68f3aa2c04");
const METER: Handle<Shader> = uuid_handle!("1123ba1a-87e2-4836-920c-6c68f3aa2c05");

pub(super) struct PluginSourceFinal;
impl Plugin for PluginSourceFinal {
    fn build(&self, app: &mut App) {
        load_internal_asset!(
            app,
            FIRST,
            "source_final_post/down_first.wgsl",
            Shader::from_wgsl
        );
        load_internal_asset!(app, DOWN, "source_final_post/down.wgsl", Shader::from_wgsl);
        load_internal_asset!(app, UP, "source_final_post/up.wgsl", Shader::from_wgsl);
        load_internal_asset!(
            app,
            FINAL,
            "source_final_post/final.wgsl",
            Shader::from_wgsl
        );
        load_internal_asset!(
            app,
            METER,
            "source_final_post/meter.wgsl",
            Shader::from_wgsl
        );
        app.add_plugins(ExtractComponentPlugin::<Controls>::default())
            .add_systems(PostUpdate, update_clock);
        trace::install(app);
        capture::install(app);
        clock::install(app).expect("Invalid opt-in source final comparison clock");
        let Some(render_app) = app.get_sub_app_mut(RenderApp) else {
            return;
        };
        render_app
            .init_resource::<forge_engine::tidewater_temporal::consumer::ConsumerGate>()
            .init_resource::<SpecializedRenderPipelines<Pipeline>>()
            .add_systems(RenderStartup, init_pipeline)
            .add_systems(
                Core3d,
                preflight
                    .after(Core3dSystems::MainPass)
                    .before(forge_engine::tidewater_temporal::hdr::SourceHdrSystems::Gtao),
            )
            .add_systems(
                Core3d,
                capture::before_hdr
                    .after(Core3dSystems::MainPass)
                    .before(forge_engine::tidewater_temporal::hdr::SourceHdrSystems::Gtao),
            )
            .add_systems(
                Render,
                prepare
                    .in_set(RenderSystems::PrepareResources)
                    .after(bevy::render::view::prepare_view_targets),
            )
            .add_systems(
                Core3d,
                render
                    .after(Core3dSystems::PostProcess)
                    .after(forge_engine::tidewater_lens_flare::TidewaterLensFlarePass)
                    .after(forge_engine::tidewater_air_haze::TidewaterAirHazePass)
                    .before(bevy::ui_render::render_pass::ui_pass)
                    .before(bevy::core_pipeline::upscaling::upscaling),
            );
    }
}

/// Four aligned vectors, identical Rust/WGSL layout. Runtime history belongs to each view.
#[derive(Component, Clone, Copy, ExtractComponent, ShaderType)]
pub(super) struct Controls {
    pub grade: Vec4,   // vignette, saturation, contrast, warmth
    pub effects: Vec4, // grain, bloom, source linear exposure, AE enabled
    pub timing: Vec4,  // actual dt, night fraction, source RCAS strength, reserved
    pub frame: UVec4,  // assigned from actual submitted final passes
}
fn update_clock(time: Res<Time>, mut cameras: Query<&mut Controls>) {
    for mut camera in &mut cameras {
        camera.timing.x = time.delta_secs();
    }
}

struct Target {
    texture: Texture,
    view: TextureView,
}
#[derive(Component)]
struct History {
    size: UVec2,
    down: Vec<Target>,
    up: Vec<Target>,
    exposure: Buffer,
    uniform: Buffer,
    submitted: u32,
}
fn prepare(
    mut commands: Commands,
    device: Res<RenderDevice>,
    queue: Res<RenderQueue>,
    views: Query<(Entity, &ViewTarget, Option<&History>), With<Controls>>,
    removed: Query<Entity, (With<History>, Without<Controls>)>,
) {
    for entity in &removed {
        commands.entity(entity).remove::<History>();
    }
    for (entity, target, old) in &views {
        let size = UVec2::new(
            target.main_texture().width(),
            target.main_texture().height(),
        );
        if old.is_some_and(|old| old.size == size) {
            continue;
        }
        let make = |level: u32| {
            // Positive JS Math.round, including odd dimensions; never a zero-size target.
            let divisor = 1 << (level + 1);
            let texture = device.create_texture(&TextureDescriptor {
                label: Some("source final bloom"),
                size: Extent3d {
                    width: ((size.x + divisor / 2) / divisor).max(1),
                    height: ((size.y + divisor / 2) / divisor).max(1),
                    depth_or_array_layers: 1,
                },
                mip_level_count: 1,
                sample_count: 1,
                dimension: TextureDimension::D2,
                format: TextureFormat::Rgba16Float,
                usage: TextureUsages::RENDER_ATTACHMENT
                    | TextureUsages::TEXTURE_BINDING
                    | if std::env::var_os("FORGE_TIDEWATER_FINAL_HDR_CAPTURE").is_some() {
                        TextureUsages::COPY_SRC
                    } else {
                        TextureUsages::empty()
                    },
                view_formats: &[],
            });
            let view = texture.create_view(&TextureViewDescriptor::default());
            Target { texture, view }
        };
        let exposure = device.create_buffer(&BufferDescriptor {
            label: Some("source final per-view exposure"),
            size: 16,
            usage: BufferUsages::STORAGE | BufferUsages::COPY_DST | BufferUsages::COPY_SRC,
            mapped_at_creation: false,
        });
        let mut initial = [0_u8; 16];
        initial[..4].copy_from_slice(&1.0_f32.to_le_bytes());
        queue.write_buffer(&exposure, 0, &initial);
        let uniform = device.create_buffer(&BufferDescriptor {
            label: Some("source final controls"),
            size: 64,
            usage: BufferUsages::UNIFORM | BufferUsages::COPY_DST | BufferUsages::COPY_SRC,
            mapped_at_creation: false,
        });
        commands.entity(entity).insert(History {
            size,
            down: (0..5).map(make).collect(),
            up: (0..4).map(make).collect(),
            exposure,
            uniform,
            submitted: 0,
        });
    }
}

#[derive(Resource)]
struct Pipeline {
    down: BindGroupLayoutDescriptor,
    up: BindGroupLayoutDescriptor,
    final_layout: BindGroupLayoutDescriptor,
    meter: BindGroupLayoutDescriptor,
    meter_id: CachedComputePipelineId,
    fullscreen: FullscreenShader,
    sampler: Sampler,
}
#[derive(Clone, Copy, PartialEq, Eq, Hash)]
enum Key {
    First,
    Down,
    Up,
    Final(TextureFormat),
}
fn init_pipeline(
    mut commands: Commands,
    fullscreen: Res<FullscreenShader>,
    cache: Res<PipelineCache>,
    device: Res<RenderDevice>,
) {
    let texture = || texture_2d(TextureSampleType::Float { filterable: true });
    let filtering = || sampler(SamplerBindingType::Filtering);
    let down = BindGroupLayoutDescriptor::new(
        "source bloom down",
        &BindGroupLayoutEntries::sequential(ShaderStages::FRAGMENT, (texture(), filtering())),
    );
    let up = BindGroupLayoutDescriptor::new(
        "source bloom up",
        &BindGroupLayoutEntries::sequential(
            ShaderStages::FRAGMENT,
            (texture(), filtering(), texture()),
        ),
    );
    let final_layout = BindGroupLayoutDescriptor::new(
        "source final grade",
        &BindGroupLayoutEntries::sequential(
            ShaderStages::FRAGMENT,
            (
                texture(),
                texture(),
                filtering(),
                uniform_buffer::<Controls>(false),
                storage_buffer_read_only::<Vec4>(false),
                texture_depth_2d(),
            ),
        ),
    );
    let meter = BindGroupLayoutDescriptor::new(
        "source exposure meter",
        &BindGroupLayoutEntries::sequential(
            ShaderStages::COMPUTE,
            (
                texture(),
                uniform_buffer::<Controls>(false),
                storage_buffer::<Vec4>(false),
            ),
        ),
    );
    let meter_id = cache.queue_compute_pipeline(ComputePipelineDescriptor {
        label: Some("source exposure next frame".into()),
        layout: vec![meter.clone()],
        shader: METER,
        entry_point: Some("main".into()),
        ..default()
    });
    commands.insert_resource(Pipeline {
        down,
        up,
        final_layout,
        meter,
        meter_id,
        fullscreen: fullscreen.clone(),
        sampler: device.create_sampler(&SamplerDescriptor {
            label: Some("source linear clamp"),
            mag_filter: FilterMode::Linear,
            min_filter: FilterMode::Linear,
            mipmap_filter: MipmapFilterMode::Nearest,
            ..default()
        }),
    });
}
impl SpecializedRenderPipeline for Pipeline {
    type Key = Key;
    fn specialize(&self, key: Key) -> RenderPipelineDescriptor {
        let (layout, shader, format) = match key {
            Key::First => (&self.down, FIRST, TextureFormat::Rgba16Float),
            Key::Down => (&self.down, DOWN, TextureFormat::Rgba16Float),
            Key::Up => (&self.up, UP, TextureFormat::Rgba16Float),
            Key::Final(format) => (&self.final_layout, FINAL, format),
        };
        RenderPipelineDescriptor {
            label: Some("source final chain".into()),
            layout: vec![layout.clone()],
            vertex: self.fullscreen.to_vertex_state(),
            fragment: Some(FragmentState {
                shader,
                entry_point: Some("fragment".into()),
                targets: vec![Some(ColorTargetState {
                    format,
                    blend: None,
                    write_mask: ColorWrites::ALL,
                })],
                ..default()
            }),
            ..default()
        }
    }
}
fn draw(
    context: &mut RenderContext,
    pipeline: &RenderPipeline,
    group: &BindGroup,
    destination: &TextureView,
) {
    let mut pass = context
        .command_encoder()
        .begin_render_pass(&RenderPassDescriptor {
            label: Some("source bloom or final"),
            color_attachments: &[Some(RenderPassColorAttachment {
                view: destination,
                depth_slice: None,
                resolve_target: None,
                ops: Operations::default(),
            })],
            depth_stencil_attachment: None,
            timestamp_writes: None,
            occlusion_query_set: None,
            multiview_mask: None,
        });
    pass.set_pipeline(pipeline);
    pass.set_bind_group(0, group, &[]);
    pass.draw(0..3, 0..1);
}

/// Queue and check every downstream pipeline before allowing a resolve commit.
/// The consumer gate is same-tag and fail-closed even when `History` is absent.
#[allow(
    clippy::type_complexity,
    reason = "Bevy view query includes optional per-view history"
)]
fn preflight(
    view: ViewQuery<(&ViewTarget, &ViewPacket, Option<&History>), With<ResolveEnabled>>,
    pipeline: Res<Pipeline>,
    cache: Res<PipelineCache>,
    mut specialized: ResMut<SpecializedRenderPipelines<Pipeline>>,
    gate: Res<forge_engine::tidewater_temporal::consumer::ConsumerGate>,
) {
    let (target, packet, history) = view.into_inner();
    let ids = [
        Key::First,
        Key::Down,
        Key::Up,
        Key::Final(target.main_texture_format()),
    ]
    .map(|key| specialized.specialize(&cache, &pipeline, key));
    let ready = history.is_some_and(|history| {
        history.size == packet.frame.output_size && history.down.len() == 5 && history.up.len() == 4
    }) && ids
        .into_iter()
        .all(|id| cache.get_render_pipeline(id).is_some())
        && cache.get_compute_pipeline(pipeline.meter_id).is_some();
    let _ = gate.publish(packet.tag, ready);
}

#[allow(
    clippy::too_many_arguments,
    clippy::too_many_lines,
    clippy::type_complexity
)]
fn render(
    view: ViewQuery<(
        &ViewTarget,
        &ViewDepthTexture,
        &Controls,
        &mut History,
        Option<&ResolveEnabled>,
        Option<&ViewPacket>,
    )>,
    pipeline: Res<Pipeline>,
    cache: Res<PipelineCache>,
    queue: Res<RenderQueue>,
    mut specialized: ResMut<SpecializedRenderPipelines<Pipeline>>,
    trace: Option<Res<trace::Trace>>,
    buffers: Res<RenderAssets<GpuShaderBuffer>>,
    resolve_observations: Option<Res<ResolveObservations>>,
    reference_clock: Option<Res<clock::ReferenceClock>>,
    capture: Option<Res<capture::Capture>>,
    mut context: RenderContext,
) {
    let entity = view.entity();
    let (target, depth, controls, mut history, resolve_enabled, packet) = view.into_inner();
    if resolve_enabled.is_some() {
        let Some(packet) = packet else { return };
        let Some(observation) = resolve_observations
            .as_deref()
            .and_then(|observations| observations.snapshot(packet.tag.view))
        else {
            return;
        };
        // A previous image's acknowledgement cannot authorize this exposure step.
        // Refuse before ping-pong, uniform writes or advancement of final history.
        if observation.tag != packet.tag
            || observation.history_epoch != packet.frame.history_epoch
            || !observation.complete()
        {
            return;
        }
    }
    let keys = [
        Key::First,
        Key::Down,
        Key::Up,
        Key::Final(target.main_texture_format()),
    ];
    let ids = keys.map(|key| specialized.specialize(&cache, &pipeline, key));
    let [Some(first), Some(down), Some(up), Some(final_pipeline)] =
        ids.map(|id| cache.get_render_pipeline(id))
    else {
        return;
    };
    let Some(meter) = cache.get_compute_pipeline(pipeline.meter_id) else {
        return;
    };
    // No ping-pong or history advance until every pipeline is ready.
    history.submitted = history.submitted.wrapping_add(1);
    let mut controls = *controls;
    if let Some(clock) = reference_clock {
        let [dt, night] = clock.step(history.submitted);
        controls.timing.x = dt;
        controls.timing.y = night;
    }
    let mut bytes = Vec::with_capacity(64);
    for value in controls
        .grade
        .to_array()
        .into_iter()
        .chain(controls.effects.to_array())
        .chain(controls.timing.to_array())
    {
        bytes.extend_from_slice(&value.to_le_bytes());
    }
    for value in [history.submitted, 0, 0, 0] {
        bytes.extend_from_slice(&value.to_le_bytes());
    }
    queue.write_buffer(&history.uniform, 0, &bytes);
    let captured_source = capture.as_ref().map(|_| target.main_texture().clone());
    let post = target.post_process_write();
    let device = context.render_device();
    let down_layout = cache.get_bind_group_layout(&pipeline.down);
    let groups: Vec<_> = (0..5)
        .map(|i| {
            device.create_bind_group(
                "source bloom down",
                &down_layout,
                &BindGroupEntries::sequential((
                    if i == 0 {
                        post.source
                    } else {
                        &history.down[i - 1].view
                    },
                    &pipeline.sampler,
                )),
            )
        })
        .collect();
    let up_layout = cache.get_bind_group_layout(&pipeline.up);
    let ups: Vec<_> = (0..4)
        .map(|i| {
            device.create_bind_group(
                "source bloom up",
                &up_layout,
                &BindGroupEntries::sequential((
                    if i == 3 {
                        &history.down[4].view
                    } else {
                        &history.up[i + 1].view
                    },
                    &pipeline.sampler,
                    &history.down[i].view,
                )),
            )
        })
        .collect();
    let final_group = device.create_bind_group(
        "source final",
        &cache.get_bind_group_layout(&pipeline.final_layout),
        &BindGroupEntries::sequential((
            post.source,
            &history.up[0].view,
            &pipeline.sampler,
            history.uniform.as_entire_binding(),
            history.exposure.as_entire_binding(),
            depth.view(),
        )),
    );
    let meter_group = device.create_bind_group(
        "source exposure",
        &cache.get_bind_group_layout(&pipeline.meter),
        &BindGroupEntries::sequential((
            &history.down[3].view,
            history.uniform.as_entire_binding(),
            history.exposure.as_entire_binding(),
        )),
    );
    for (i, group) in groups.iter().enumerate() {
        draw(
            &mut context,
            if i == 0 { first } else { down },
            group,
            &history.down[i].view,
        );
    }
    for i in (0..4).rev() {
        draw(&mut context, up, &ups[i], &history.up[i].view);
    }
    if let Some(source) = captured_source.as_ref() {
        capture::encode(
            capture.as_deref(),
            &buffers,
            &mut context,
            history.submitted,
            &history.uniform,
            [source, &history.down[3].texture, &history.up[0].texture],
            packet.map(|packet| (packet.tag, entity)),
        );
    }
    let trace_packet = trace::packet(trace.as_deref(), &buffers, entity)
        .filter(|_| (1..=trace::CAPACITY).contains(&(history.submitted as usize)));
    let trace_offset = u64::from(history.submitted.saturating_sub(1)) * trace::STRIDE as u64;
    if let Some(packet) = trace_packet {
        context.command_encoder().copy_buffer_to_buffer(
            &history.uniform,
            0,
            &packet.buffer,
            trace_offset,
            64,
        );
        context.command_encoder().copy_buffer_to_buffer(
            &history.exposure,
            0,
            &packet.buffer,
            trace_offset + 64,
            4,
        );
    }
    draw(&mut context, final_pipeline, &final_group, post.destination);
    // Exposure storage is read above, then updated below for the following frame.
    let mut pass = context
        .command_encoder()
        .begin_compute_pass(&ComputePassDescriptor {
            label: Some("source exposure for next image"),
            timestamp_writes: None,
        });
    pass.set_pipeline(meter);
    pass.set_bind_group(0, &meter_group, &[]);
    pass.dispatch_workgroups(1, 1, 1);
    drop(pass);
    if let Some(packet) = trace_packet {
        context.command_encoder().copy_buffer_to_buffer(
            &history.exposure,
            0,
            &packet.buffer,
            trace_offset + 68,
            4,
        );
        context.command_encoder().copy_buffer_to_buffer(
            &history.uniform,
            48,
            &packet.buffer,
            trace_offset + 72,
            4,
        );
    }
    if history.submitted == 1 || history.submitted == 124 {
        info!(
            "Source final native: submitted={} size={:?} dt={} night={} linear_exposure={} bloom={} ae={}",
            history.submitted,
            history.size,
            controls.timing.x,
            controls.timing.y,
            controls.effects.z,
            controls.effects.y,
            controls.effects.w
        );
    }
}
