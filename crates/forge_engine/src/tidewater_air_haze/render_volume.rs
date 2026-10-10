//! Encode the complete resident volumetric chain before publishing HDR haze.
use super::{TidewaterAirHaze, clock::Clock, lighting, moon, passes, scene, targets};
use crate::{
    tidewater_atmosphere, tidewater_clouds, tidewater_object_shadow,
    tidewater_temporal::{
        frame::ViewPacket,
        hdr::{HdrObservations, HdrRole},
        resolve::ResolveEnabled,
    },
    tidewater_underwater, tidewater_underwater_lighting,
};
use bevy::{
    ecs::system::SystemParam,
    prelude::*,
    render::{
        extract_component::{ComponentUniforms, DynamicUniformIndex},
        render_asset::RenderAssets,
        render_resource::{
            AddressMode, BindGroup, BindGroupEntries, FilterMode, Operations, PipelineCache,
            RenderPassColorAttachment, RenderPassDescriptor, RenderPipeline, Sampler,
            SamplerDescriptor, TextureFormat, TextureView, UniformBuffer,
        },
        renderer::{RenderContext, RenderDevice, RenderQueue, ViewQuery},
        storage::GpuShaderBuffer,
        texture::GpuImage,
        view::{ViewDepthTexture, ViewTarget},
    },
};

#[derive(Resource)]
pub(super) struct Samplers {
    clamp: Sampler,
    repeat: Sampler,
}

pub(super) fn init(mut commands: Commands, device: Res<RenderDevice>) {
    let sampler = |repeat| {
        device.create_sampler(&SamplerDescriptor {
            address_mode_u: repeat,
            address_mode_v: repeat,
            mag_filter: FilterMode::Linear,
            min_filter: FilterMode::Linear,
            ..default()
        })
    };
    commands.insert_resource(Samplers {
        clamp: sampler(AddressMode::ClampToEdge),
        repeat: sampler(AddressMode::Repeat),
    });
}

#[derive(SystemParam)]
pub(super) struct Inputs<'w> {
    programs: Res<'w, passes::Pipelines>,
    cache: Res<'w, PipelineCache>,
    samplers: Res<'w, Samplers>,
    queue: Res<'w, RenderQueue>,
    uniforms: Res<'w, ComponentUniforms<TidewaterAirHaze>>,
    images: Res<'w, RenderAssets<GpuImage>>,
    buffers: Res<'w, RenderAssets<GpuShaderBuffer>>,
    scene: Res<'w, crate::atmospheric_haze::scene::Inputs>,
    moon: Res<'w, moon::Inputs>,
    object: Option<Res<'w, tidewater_object_shadow::graph::Target>>,
    clouds: Option<Res<'w, tidewater_clouds::Targets>>,
    sampling: Option<Res<'w, tidewater_clouds::sampling::Sampling>>,
    optics: Option<Res<'w, tidewater_underwater_lighting::Targets>>,
    surface: Option<Res<'w, tidewater_underwater_lighting::Surface>>,
    lighting: Option<Res<'w, lighting::Targets>>,
    atmosphere: Option<Res<'w, tidewater_atmosphere::Targets>>,
    hdr: Res<'w, HdrObservations>,
}

type HazeView = (
    &'static ViewTarget,
    &'static ViewDepthTexture,
    &'static TidewaterAirHaze,
    &'static DynamicUniformIndex<TidewaterAirHaze>,
    &'static ViewPacket,
    &'static targets::Targets,
    &'static tidewater_underwater::medium::Target,
    &'static Clock,
    &'static crate::atmospheric_haze::AirHazeSettings,
);

fn draw(
    context: &mut RenderContext,
    label: &'static str,
    target: &TextureView,
    program: &RenderPipeline,
    group: &BindGroup,
    offset: u32,
) {
    let mut pass = context
        .command_encoder()
        .begin_render_pass(&RenderPassDescriptor {
            label: Some(label),
            color_attachments: &[Some(RenderPassColorAttachment {
                view: target,
                depth_slice: None,
                resolve_target: None,
                ops: Operations::default(),
            })],
            depth_stencil_attachment: None,
            timestamp_writes: None,
            occlusion_query_set: None,
            multiview_mask: None,
        });
    pass.set_pipeline(program);
    pass.set_bind_group(0, group, &[offset]);
    pass.draw(0..3, 0..1);
}

#[expect(
    clippy::too_many_lines,
    reason = "The render pass keeps attachment, bind group, draw and diagnostic ordering visible in one operation"
)]
pub(super) fn render(
    view: ViewQuery<HazeView, With<ResolveEnabled>>,
    inputs: Inputs,
    mut context: RenderContext,
    mut reported: Local<bool>,
) {
    let (target, depth, haze, index, frame, targets, medium, clock, settings) = view.into_inner();
    if !inputs.hdr.ready_for(frame.tag, HdrRole::Haze)
        || target.main_texture_format() != TextureFormat::Rgba16Float
    {
        return;
    }
    if haze.control.y <= 0.0 {
        let _ = inputs.hdr.record_neutral_haze(frame.tag);
        return;
    }
    let extent = target.main_texture().size();
    let size = UVec2::new(extent.width, extent.height);
    if !medium.encoded_for(frame.tag, size)
        || targets::Dimensions::from_output(
            frame.packet.sizes.zw(),
            settings.resolution_scale(),
            context.render_device().limits().max_texture_dimension_2d,
        ) != Some(targets.dimensions)
        || !inputs.programs.programs_ready(&inputs.cache)
    {
        return;
    }
    let (
        Some(selected),
        Some(moon),
        Some(object),
        Some(clouds),
        Some(sampling),
        Some(optics),
        Some(surface),
        Some(lighting),
        Some(atmosphere),
    ) = (
        inputs.scene.current.as_ref(),
        inputs.moon.current.as_ref(),
        inputs.object.as_deref(),
        inputs.clouds.as_deref(),
        inputs.sampling.as_deref().and_then(|s| s.current.as_ref()),
        inputs.optics.as_deref(),
        inputs.surface.as_deref(),
        inputs.lighting.as_deref(),
        inputs.atmosphere.as_deref(),
    )
    else {
        return;
    };
    let Some(current) = scene::current(object, clouds, optics, surface, lighting) else {
        return;
    };
    if !selected.encoded_for(&current)
        || !sampling.encoded_for(clouds)
        || !moon.encoded_for(atmosphere)
    {
        return;
    }
    let selected = &selected.resources;
    let (
        Some(object_depth),
        Some(hill),
        Some(cloud_shadow),
        Some(history),
        Some(panorama),
        Some(sky),
        Some(live),
        Some(light),
        Some(camera),
    ) = (
        inputs.images.get(&selected.object_depth),
        inputs.images.get(&selected.hill_shadow),
        inputs.images.get(&selected.cloud_shadow),
        inputs.images.get(&sampling.history),
        inputs.images.get(&sampling.panorama),
        inputs.images.get(&moon.sky),
        inputs.buffers.get(&selected.live_shadow),
        inputs.buffers.get(&selected.lighting),
        inputs.uniforms.uniforms().binding(),
    )
    else {
        return;
    };
    let (Some(march_program), Some(mask_program), Some(radial_program), Some(composite_program)) = (
        inputs.cache.get_render_pipeline(inputs.programs.march),
        inputs.cache.get_render_pipeline(inputs.programs.mask),
        inputs.cache.get_render_pipeline(inputs.programs.radial),
        inputs.cache.get_render_pipeline(inputs.programs.composite),
    ) else {
        return;
    };
    let device = context.render_device();
    let uniforms = passes::Uniforms::upload(
        passes::Volume {
            control: Vec4::new(
                settings.density(),
                settings.shafts(),
                if settings.enabled { 1.0 } else { 0.0 },
                f32::from(clock.0),
            ),
            terrain_domain: selected.terrain_domain,
            cloud_rectangle: selected.cloud_rectangle,
            // Screen position/fade are evaluated from the resident lighting on GPU.
            screen: Vec4::new(0.0, 0.0, if settings.god_rays { 1.0 } else { 0.0 }, 0.0),
        },
        device,
        &inputs.queue,
    );
    let mut cloud_uniform = UniformBuffer::from(passes::CloudView {
        right: sampling.camera.right,
        up: sampling.camera.up,
        forward: sampling.camera.forward,
        tangent_size: sampling.camera.tangent_size,
        validity: Vec4::X,
    });
    cloud_uniform.write_buffer(device, &inputs.queue);
    let mut moon_uniform = UniformBuffer::from(passes::Moon {
        direction_night: moon.direction_night,
    });
    moon_uniform.write_buffer(device, &inputs.queue);
    let (Some(volume), Some(cloud), Some(moon_binding)) = (
        uniforms.march.binding(),
        cloud_uniform.binding(),
        moon_uniform.binding(),
    ) else {
        return;
    };
    let march = device.create_bind_group(
        "tidewater haze march bindings",
        &inputs
            .cache
            .get_bind_group_layout(&inputs.programs.march_layout),
        &BindGroupEntries::sequential((
            depth.view(),
            camera.clone(),
            volume.clone(),
            &object_depth.texture_view,
            &hill.texture_view,
            &cloud_shadow.texture_view,
            live.buffer.as_entire_binding(),
            light.buffer.as_entire_binding(),
        )),
    );
    let mask = device.create_bind_group(
        "tidewater haze mask bindings",
        &inputs
            .cache
            .get_bind_group_layout(&inputs.programs.mask_layout),
        &BindGroupEntries::sequential((
            depth.view(),
            camera.clone(),
            volume.clone(),
            light.buffer.as_entire_binding(),
            cloud,
            &history.texture_view,
            &panorama.texture_view,
            &inputs.samplers.clamp,
            &inputs.samplers.repeat,
        )),
    );
    let mut radial = Vec::with_capacity(3);
    for (step, uniform) in uniforms.radial.iter().enumerate() {
        let Some(binding) = uniform.binding() else {
            return;
        };
        radial.push(
            device.create_bind_group(
                "tidewater haze radial bindings",
                &inputs
                    .cache
                    .get_bind_group_layout(&inputs.programs.radial_layout),
                &BindGroupEntries::sequential((
                    &targets.screen[step].default_view,
                    &inputs.samplers.clamp,
                    binding,
                    camera.clone(),
                    light.buffer.as_entire_binding(),
                )),
            ),
        );
    }
    // Preflight is complete before touching the scene's ping-pong state.
    let post = target.post_process_write();
    let composite = device.create_bind_group(
        "tidewater haze full composite bindings",
        &inputs
            .cache
            .get_bind_group_layout(&inputs.programs.composite_layout),
        &BindGroupEntries::sequential((
            post.source,
            depth.view(),
            &sky.texture_view,
            &inputs.samplers.clamp,
            camera,
            volume,
            light.buffer.as_entire_binding(),
            &targets.march.default_view,
            &targets.screen[3].default_view,
            &medium.texture.default_view,
            moon_binding,
        )),
    );
    draw(
        &mut context,
        "tidewater volumetric haze march",
        &targets.march.default_view,
        march_program,
        &march,
        index.index(),
    );
    draw(
        &mut context,
        "tidewater haze sun mask",
        &targets.screen[0].default_view,
        mask_program,
        &mask,
        index.index(),
    );
    for (step, group) in radial.iter().enumerate() {
        draw(
            &mut context,
            "tidewater haze radial blur",
            &targets.screen[step + 1].default_view,
            radial_program,
            group,
            index.index(),
        );
    }
    draw(
        &mut context,
        "tidewater full volumetric haze composite",
        post.destination,
        composite_program,
        &composite,
        index.index(),
    );
    let _ = inputs.hdr.record(frame.tag, HdrRole::Haze, 1);
    if !*reported {
        info!(
            "Tidewater full volumetric haze encoded: march + mask + 3 radial + composite; source_haze_frame={} cloud_step={}; gpu_completion_unproven=true",
            clock.0, sampling.encoded_step
        );
        *reported = true;
    }
}
