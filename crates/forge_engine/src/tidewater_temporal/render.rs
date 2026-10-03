// SPDX-License-Identifier: MIT OR Apache-2.0
//! Encodes actual auxiliary draws and copies the main depth after water.
//! Observations describe CPU command encoding, never GPU completion or a TAAU resolve.
use super::{
    frame::{Acknowledgements, AuxEncoding, EncodingOwner, FrameTag, ViewPacket, commit_aux},
    phase::{AuxCoverage, AuxDiagnostic, AuxDrawCounter, AuxItem, PacketOffset, PacketUniforms},
    targets::AuxTargets,
};
use bevy::{
    prelude::*,
    render::{
        extract_component::{ExtractComponent, ExtractComponentPlugin},
        render_phase::ViewSortedRenderPhases,
        render_resource::{
            BindGroup, BindGroupLayoutDescriptor, CachedRenderPipelineId, ColorTargetState,
            ColorWrites, CompareFunction, DepthStencilState, FragmentState, LoadOp, Operations,
            PipelineCache, PrimitiveState, RenderPassColorAttachment,
            RenderPassDepthStencilAttachment, RenderPassDescriptor, RenderPipelineDescriptor,
            StoreOp, Texture, TextureAspect, TextureFormat, TextureUsages, VertexState,
        },
        renderer::{RenderContext, RenderDevice, ViewQuery},
        view::{ExtractedView, ViewDepthTexture},
    },
    shader::Shader,
};
use std::{
    collections::HashMap,
    sync::{Arc, Mutex},
};

/// This is a scene assertion supplied by its adapter, not inferred from a slug.
#[derive(Component, Clone, Copy, Debug, Default, ExtractComponent)]
pub enum HullRequirement {
    /// The source can have boat hulls; missing integration blocks full coverage.
    #[default]
    Required,
    /// The actual submitted scene has been audited and contains no boat hulls.
    NoHullsInScene,
}

/// A scene adapter must attest its complete explicit hull inventory and return
/// the dedicated distance-pass encoding shared by beauty and auxiliary water.
/// The RGBA8 water mask is never a substitute for the source R16Float hull mask.
/// Zero active draws is valid for a registered hull outside the frustum or for
/// a camera inside its expanded local bounds; it is not a no-hulls assertion.
#[derive(Resource)]
pub struct HullMaskAdapter(
    pub  for<'w, 's> fn(
        &World,
        Entity,
        &AuxTargets,
        &ViewPacket,
        &mut RenderContext<'w, 's>,
    ) -> Result<crate::tidewater_hull_mask::Encoding, String>,
);

#[derive(Resource)]
pub struct SkyShader(pub Handle<Shader>);

#[derive(Resource)]
pub struct SkyPipeline {
    pub pipeline: CachedRenderPipelineId,
    pub empty_group: BindGroup,
}

#[derive(Clone, Debug)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "These independent receipt flags are the public auxiliary contract"
)]
pub struct AuxObservation {
    pub tag: FrameTag,
    pub generation: u64,
    pub coverage: AuxCoverage,
    pub mesh_draws: u32,
    pub sky_draws: u32,
    pub hull_draws: u32,
    pub hull_contract_satisfied: bool,
    pub final_depth_copied: bool,
    pub previous_depth_copied: bool,
    pub history_seeded: bool,
    pub auxiliary_acknowledged: bool,
    pub draw_diagnostics: Vec<AuxDiagnostic>,
    pub draw_diagnostics_omitted: u32,
    pub errors: Vec<String>,
}

impl AuxObservation {
    #[must_use]
    pub fn complete(&self) -> bool {
        self.coverage.complete()
            && self.mesh_draws == self.coverage.queued
            && self.sky_draws == 1
            && self.hull_contract_satisfied
            && self.final_depth_copied
            && self.previous_depth_copied
            && self.errors.is_empty()
    }
}

/// `MainWorld` and `RenderWorld` share this observation handle for probes.
#[derive(Resource, Clone, Default)]
pub struct AuxObservations(Arc<Mutex<HashMap<Entity, AuxObservation>>>);

impl AuxObservations {
    #[must_use]
    pub fn snapshot(&self, view: Entity) -> Option<AuxObservation> {
        self.0.lock().ok()?.get(&view).cloned()
    }
    #[must_use]
    pub fn snapshots(&self) -> Vec<AuxObservation> {
        self.0.lock().map_or_else(
            |_| Vec::new(),
            |entries| entries.values().cloned().collect(),
        )
    }
    fn record(&self, observation: AuxObservation) {
        if let Ok(mut entries) = self.0.lock() {
            entries.insert(observation.tag.view, observation);
        }
    }
}

pub fn install_scene_contract(app: &mut App) {
    app.add_plugins(ExtractComponentPlugin::<HullRequirement>::default());
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy startup systems receive render resources by value"
)]
pub fn initialize_sky(
    mut commands: Commands,
    uniforms: Res<PacketUniforms>,
    shader: Res<SkyShader>,
    device: Res<RenderDevice>,
    cache: ResMut<PipelineCache>,
) {
    let empty_layout =
        BindGroupLayoutDescriptor::new("Tidewater auxiliary sky unused group 0", &[]);
    let empty_group = device.create_bind_group(
        "Tidewater auxiliary sky empty group",
        &cache.get_bind_group_layout(&empty_layout),
        &[],
    );
    let pipeline = cache.queue_render_pipeline(RenderPipelineDescriptor {
        label: Some("Tidewater auxiliary sky direction velocity".into()),
        layout: vec![empty_layout, uniforms.layout.clone()],
        vertex: VertexState {
            shader: shader.0.clone(),
            entry_point: Some("vertex".into()),
            ..default()
        },
        fragment: Some(FragmentState {
            shader: shader.0.clone(),
            entry_point: Some("fragment".into()),
            targets: vec![
                Some(ColorTargetState {
                    format: TextureFormat::Rgba16Float,
                    blend: None,
                    write_mask: ColorWrites::ALL,
                }),
                Some(ColorTargetState {
                    format: TextureFormat::Rgba8Unorm,
                    blend: Some(bevy::render::render_resource::BlendState::ALPHA_BLENDING),
                    write_mask: ColorWrites::ALL,
                }),
            ],
            ..default()
        }),
        primitive: PrimitiveState::default(),
        depth_stencil: Some(DepthStencilState {
            format: TextureFormat::Depth32Float,
            depth_write_enabled: Some(false),
            depth_compare: Some(CompareFunction::GreaterEqual),
            stencil: default(),
            bias: default(),
        }),
        ..default()
    });
    commands.insert_resource(SkyPipeline {
        pipeline,
        empty_group,
    });
}

/// Core3d schedule runs this after the complete main pass, including water and
/// late transparent draws, and before early post processing / tonemapping.
#[allow(
    clippy::too_many_arguments,
    clippy::needless_pass_by_value,
    clippy::too_many_lines,
    clippy::type_complexity,
    reason = "This ordered Bevy render pass owns the complete auxiliary contract"
)]
pub fn encode(
    world: &World,
    view: ViewQuery<(
        &ExtractedView,
        &ViewPacket,
        &AuxTargets,
        &ViewDepthTexture,
        &PacketOffset,
        &AuxCoverage,
        &AuxDrawCounter,
        Option<&HullRequirement>,
    )>,
    phases: Res<ViewSortedRenderPhases<AuxItem>>,
    uniforms: Res<PacketUniforms>,
    sky: Res<SkyPipeline>,
    cache: Res<PipelineCache>,
    hull_adapter: Option<Res<HullMaskAdapter>>,
    acknowledgements: Res<Acknowledgements>,
    encoding_owner: Option<Res<EncodingOwner>>,
    observations: Res<AuxObservations>,
    base_observation: Option<Res<super::base_readback::Observation>>,
    base_jobs: Option<Res<super::base_readback::Jobs>>,
    mut context: RenderContext,
) {
    let view_entity = view.entity();
    let (extracted, packet, targets, main_depth, offset, coverage, counter, hull_requirement) =
        view.into_inner();
    let mut observation = AuxObservation {
        tag: packet.tag,
        generation: targets.generation,
        coverage: coverage.clone(),
        mesh_draws: 0,
        sky_draws: 0,
        hull_draws: 0,
        hull_contract_satisfied: false,
        final_depth_copied: false,
        previous_depth_copied: false,
        history_seeded: false,
        auxiliary_acknowledged: false,
        draw_diagnostics: Vec::new(),
        draw_diagnostics_omitted: 0,
        errors: Vec::new(),
    };
    if targets.was_aux_encoded(packet.tag).unwrap_or(false) {
        observation
            .errors
            .push("Packet was already encoded; duplicate Core3d view render refused".into());
        observations.record(observation);
        return;
    }
    let seed = match targets.needs_seed(packet) {
        Ok(seed) => seed,
        Err(error) => {
            observation.errors.push(error.to_string());
            observations.record(observation);
            return;
        }
    };
    if let Err(error) =
        validate_depth_copy(&main_depth.texture, &targets.current_final_depth.texture)
    {
        observation.errors.push(error);
        observations.record(observation);
        return;
    }
    let (Some(phase), Some(packet_binding), Some(sky_pipeline)) = (
        phases.get(&extracted.retained_view_entity),
        uniforms.binding.as_ref(),
        cache.get_render_pipeline(sky.pipeline),
    ) else {
        observation
            .errors
            .push("Auxiliary phase, packet binding or sky pipeline is still preparing".into());
        observations.record(observation);
        return;
    };
    counter.reset();
    let mut base_ticket = None;
    // Sky writes the source direction velocity at reversed far depth 0, then
    // actual opaque geometry overwrites covered pixels and writes auxiliary depth.
    let opaque_end = phase
        .items
        .values()
        .take_while(|item| item.role == super::phase::MaterialRole::Opaque)
        .count();
    {
        let color = color_attachments(targets, true);
        let depth = depth_attachment(targets, true);
        let mut pass = context.begin_tracked_render_pass(RenderPassDescriptor {
            label: Some("Tidewater temporal sky and opaque auxiliary"),
            color_attachments: &color,
            depth_stencil_attachment: Some(depth),
            ..default()
        });
        pass.set_render_pipeline(sky_pipeline);
        pass.set_bind_group(0, &sky.empty_group, &[]);
        pass.set_bind_group(1, packet_binding, &[offset.0]);
        pass.draw(0..3, 0..1);
        observation.sky_draws = 1;
        if let Err(error) = phase.render_range(&mut pass, world, view_entity, 0..opaque_end) {
            observation
                .errors
                .push(format!("Opaque auxiliary draw: {error:?}"));
        }
    }
    match hull_requirement.copied().unwrap_or_default() {
        HullRequirement::NoHullsInScene => observation.hull_contract_satisfied = true,
        HullRequirement::Required => {
            if let Some(adapter) = hull_adapter {
                match (adapter.0)(world, view_entity, targets, packet, &mut context) {
                    Ok(encoding)
                        if hull_encoding_matches(&encoding, view_entity, packet, targets) =>
                    {
                        observation.hull_draws = encoding.encoded;
                        observation.hull_contract_satisfied = true;
                    }
                    Ok(_) => observation.errors.push(
                        "Hull distance encoding is incomplete, empty or belongs to another frame"
                            .into(),
                    ),
                    Err(error) => observation.errors.push(error),
                }
            } else {
                observation.errors.push(
                    "Source hull mask contract is required but no encoder is registered".into(),
                );
            }
        }
    }
    let water_end = phase
        .items
        .values()
        .take_while(|item| item.role != super::phase::MaterialRole::Transparent)
        .count();
    if opaque_end < water_end {
        let color = color_attachments(targets, false);
        let depth = depth_attachment(targets, false);
        let mut pass = context.begin_tracked_render_pass(RenderPassDescriptor {
            label: Some("Tidewater temporal water auxiliary"),
            color_attachments: &color,
            depth_stencil_attachment: Some(depth),
            ..default()
        });
        if let Err(error) = phase.render_range(&mut pass, world, view_entity, opaque_end..water_end)
        {
            observation
                .errors
                .push(format!("Water auxiliary draw: {error:?}"));
        }
    }
    // Pass is closed: these copies are ordered BEFORE any transparent draw.
    // Captures are optional diagnostics and do not relax auxiliary readiness.
    if let (Some(shared), Some(jobs)) = (base_observation.as_deref(), base_jobs.as_deref()) {
        let metadata = super::base_readback::Metadata {
            view_packet: packet.clone(),
            generation: targets.generation,
            input_size: targets.input_size,
            output_size: targets.output_size,
            boundary: super::base_readback::Boundary {
                opaque: coverage.opaque,
                water: coverage.water,
                transparent: coverage.transparent,
                queued: coverage.queued,
                base_mesh_draws: counter.draws(),
                sky_draws: observation.sky_draws,
                hull_draws: observation.hull_draws,
                hull_contract_satisfied: observation.hull_contract_satisfied,
            },
        };
        match super::base_readback::before_late(
            shared,
            jobs,
            &mut context,
            metadata,
            super::base_readback::Inputs {
                velocity: &targets.velocity.texture,
                mask: &targets.mask.texture,
                depth: &targets.depth_aux.texture,
            },
        ) {
            Ok(ticket) => base_ticket = ticket,
            Err(error) => warn!("Temporal pre-late readback refused: {error}"),
        }
    }
    if water_end < phase.items.len() {
        let color = color_attachments(targets, false);
        let depth = depth_attachment(targets, false);
        let mut pass = context.begin_tracked_render_pass(RenderPassDescriptor {
            label: Some("Tidewater temporal transparent auxiliary"),
            color_attachments: &color,
            depth_stencil_attachment: Some(depth),
            ..default()
        });
        if let Err(error) = phase.render_range(&mut pass, world, view_entity, water_end..) {
            observation
                .errors
                .push(format!("Late auxiliary draw: {error:?}"));
        }
    }
    observation.mesh_draws = counter.draws();
    (
        observation.draw_diagnostics,
        observation.draw_diagnostics_omitted,
    ) = counter.diagnostics();
    if observation.mesh_draws != coverage.queued {
        observation.errors.push(format!(
            "Actual DrawMesh commands {} differ from queued meshes {}",
            observation.mesh_draws, coverage.queued,
        ));
    }
    // Partial draws must not overwrite depth history owned by the last
    // acknowledged packet. Only complete auxiliary encoding advances history.
    if !coverage.complete()
        || observation.sky_draws != 1
        || !observation.hull_contract_satisfied
        || !observation.errors.is_empty()
    {
        observations.record(observation);
        return;
    }
    // Preserve N-1 before overwriting N. New allocations are deliberately seeded
    // with N; the history marker advances only after complete, real encoding.
    if !seed {
        copy_depth(
            &mut context,
            &targets.current_final_depth.texture,
            &targets.previous_depth.texture,
        );
        observation.previous_depth_copied = true;
    }
    copy_depth(
        &mut context,
        &main_depth.texture,
        &targets.current_final_depth.texture,
    );
    observation.final_depth_copied = true;
    if seed {
        copy_depth(
            &mut context,
            &targets.current_final_depth.texture,
            &targets.previous_depth.texture,
        );
        observation.previous_depth_copied = true;
        observation.history_seeded = true;
    }
    if observation.complete() {
        match targets.mark_aux_encoded(packet) {
            Ok(()) => {
                // Resolve owns sequence advancement when explicitly selected.
                // Complete auxiliary draws/copies remain observable in either mode.
                if encoding_owner.as_deref().copied().unwrap_or_default()
                    == EncodingOwner::Auxiliary
                {
                    match commit_aux(
                        &acknowledgements,
                        packet.tag,
                        AuxEncoding {
                            draw_count: observation.mesh_draws
                                + observation.sky_draws
                                + observation.hull_draws,
                            final_depth_copied: observation.final_depth_copied,
                        },
                    ) {
                        Ok(()) => observation.auxiliary_acknowledged = true,
                        Err(error) => observation.errors.push(error.to_string()),
                    }
                }
            }
            Err(error) => observation.errors.push(error.to_string()),
        }
    }
    // Pair only AFTER complete real final draws/depth copies/history mark/ack.
    // Resolve mode legitimately has auxiliary_acknowledged=false.
    if let Some(ticket) = base_ticket
        && let (Some(shared), Some(jobs)) = (base_observation.as_deref(), base_jobs.as_deref())
        && let Err(error) = super::base_readback::after_aux(
            shared,
            jobs,
            &mut context,
            ticket,
            &observation,
            super::base_readback::Inputs {
                velocity: &targets.velocity.texture,
                mask: &targets.mask.texture,
                depth: &targets.depth_aux.texture,
            },
        )
    {
        warn!("Temporal paired auxiliary readback refused: {error}");
    }
    observations.record(observation);
}

fn hull_encoding_matches(
    encoding: &crate::tidewater_hull_mask::Encoding,
    render_view: Entity,
    packet: &ViewPacket,
    targets: &AuxTargets,
) -> bool {
    encoding.render_view == render_view
        && targets.view == packet.tag.view
        && targets.input_size == packet.frame.input_size
        && encoding.matches_required_frame(packet.tag, targets.input_size)
}

fn color_attachments(
    targets: &AuxTargets,
    clear: bool,
) -> [Option<RenderPassColorAttachment<'_>>; 2] {
    [&targets.velocity.default_view, &targets.mask.default_view].map(|view| {
        Some(RenderPassColorAttachment {
            view,
            depth_slice: None,
            resolve_target: None,
            ops: Operations {
                load: if clear {
                    LoadOp::Clear(default())
                } else {
                    LoadOp::Load
                },
                store: StoreOp::Store,
            },
        })
    })
}

fn depth_attachment(targets: &AuxTargets, clear: bool) -> RenderPassDepthStencilAttachment<'_> {
    RenderPassDepthStencilAttachment {
        view: &targets.depth_aux.default_view,
        depth_ops: Some(Operations {
            load: if clear {
                LoadOp::Clear(0.0)
            } else {
                LoadOp::Load
            },
            store: StoreOp::Store,
        }),
        stencil_ops: None,
    }
}

fn validate_depth_copy(source: &Texture, destination: &Texture) -> Result<(), String> {
    if source.format() != TextureFormat::Depth32Float
        || destination.format() != TextureFormat::Depth32Float
        || source.sample_count() != 1
        || destination.sample_count() != 1
        || source.size() != destination.size()
    {
        return Err(
            "Final depth copy requires matching Depth32Float single-sample internal dimensions"
                .into(),
        );
    }
    if !source.usage().contains(TextureUsages::COPY_SRC)
        || !destination.usage().contains(TextureUsages::COPY_DST)
    {
        return Err("Main final depth must be configured COPY_SRC and its target COPY_DST".into());
    }
    if source.id() == destination.id() {
        return Err("Final depth copy aliases its source".into());
    }
    Ok(())
}

fn copy_depth(context: &mut RenderContext, source: &Texture, destination: &Texture) {
    let mut from = source.as_image_copy();
    from.aspect = TextureAspect::DepthOnly;
    let mut to = destination.as_image_copy();
    to.aspect = TextureAspect::DepthOnly;
    context
        .command_encoder()
        .copy_texture_to_texture(from, to, destination.size());
}
