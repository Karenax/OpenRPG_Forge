//! Per-frame ownership for scene-bound particle draws.
//!
//! This metadata is transient renderer state, never an authored asset or a
//! GPU completion receipt. The ordinary source material path remains separate.

use crate::source_graph::SourceGraphMaterial;
use bevy::{
    asset::{AssetId, UntypedAssetId},
    camera::MainPassResolutionOverride,
    core_pipeline::{
        core_3d::{AlphaMask3d, Opaque3d, Transparent3d},
        deferred::{AlphaMask3dDeferred, Opaque3dDeferred},
        prepass::{AlphaMask3dPrepass, Opaque3dPrepass},
    },
    ecs::{
        query::ROQueryItem,
        system::{
            SystemParamItem,
            lifetimeless::{Read, SRes},
        },
    },
    material::{MaterialProperties, labels::DrawFunctionLabel},
    pbr::{
        DeferredAlphaMaskDrawFunction, DeferredOpaqueDrawFunction, DrawDepthOnlyPrepass,
        DrawMaterial, DrawPrepass, MainPassAlphaMaskDrawFunction, MainPassOpaqueDrawFunction,
        MainPassTransmissiveDrawFunction, MainPassTransparentDrawFunction, PreparedMaterial,
        PrepassAlphaMaskDrawFunction, PrepassOpaqueDepthOnlyDrawFunction,
        PrepassOpaqueDrawFunction, RenderMaterialInstances, RenderMeshInstances, Shadow,
        ShadowView, ShadowsDepthOnlyDrawFunction, ShadowsDrawFunction, Transmissive3d,
        check_entities_needing_specialization,
    },
    prelude::*,
    render::{
        Render, RenderApp, RenderStartup, RenderSystems,
        batching::NoAutomaticBatching,
        camera::{ExtractedCamera, TemporalJitter},
        erased_render_asset::{ErasedRenderAssets, ExtractedAssets, prepare_erased_assets},
        render_phase::{
            AddRenderCommand, DrawFunctionId, DrawFunctions, PhaseItem, RenderCommand,
            RenderCommandResult, TrackedRenderPass,
        },
        storage::ShaderBuffer,
        sync_world::MainEntity,
        view::ExtractedView,
    },
};
use std::{collections::HashMap, sync::Arc};

#[derive(Clone, Debug)]
pub(crate) struct ParticleFrameOwner {
    pub(crate) slot: u64,
    pub(crate) identity: Arc<()>,
    pub(crate) output: AssetId<ShaderBuffer>,
}

impl ParticleFrameOwner {
    pub(crate) fn matches(&self, other: &Self) -> bool {
        self.slot == other.slot
            && self.output == other.output
            && Arc::ptr_eq(&self.identity, &other.identity)
    }
}

#[derive(Clone)]
struct Frame {
    owner: ParticleFrameOwner,
    view: Entity,
    world_from_view: Mat4,
    clip_from_view: Mat4,
}

/// Entries are cleared before every render-graph execution and published only
/// after the corresponding compute commands have been encoded in that graph.
#[derive(Resource, Default)]
pub(crate) struct CurrentFrames(HashMap<u64, Frame>);

impl CurrentFrames {
    pub(super) fn clear(&mut self) {
        self.0.clear();
    }

    pub(super) fn record(
        &mut self,
        owner: ParticleFrameOwner,
        view: Entity,
        world_from_view: Mat4,
        clip_from_view: Mat4,
    ) {
        self.0.insert(
            owner.slot,
            Frame {
                owner,
                view,
                world_from_view,
                clip_from_view,
            },
        );
    }
}

#[derive(Clone)]
enum MaterialFrame {
    Ordinary,
    Particle(ParticleFrameOwner),
    Invalid,
}

/// Mirrors each exact extracted material version, including shadow clones.
/// It is updated before Bevy consumes the extracted versions for preparation.
#[derive(Resource, Default)]
pub(crate) struct MaterialFrames(HashMap<UntypedAssetId, MaterialFrame>);

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy render system parameters"
)]
fn capture_material_frames(
    extracted: Option<Res<ExtractedAssets<MeshMaterial3d<SourceGraphMaterial>>>>,
    mut frames: ResMut<MaterialFrames>,
) {
    let Some(extracted) = extracted else { return };
    for id in &extracted.removed {
        frames.0.remove(&id.untyped());
    }
    for (id, material) in &extracted.extracted {
        let owner = match material.extension.particle_frame_owner() {
            None => MaterialFrame::Ordinary,
            Some(owner) if material.extension.particle_frame_binding_valid() => {
                MaterialFrame::Particle(owner.clone())
            }
            Some(_) => MaterialFrame::Invalid,
        };
        frames.0.insert(id.untyped(), owner);
    }
}

type GuardedMain = (CheckParticleFrame, DrawMaterial);
type GuardedPrepass = (CheckParticleFrame, DrawPrepass);
type GuardedDepthOnly = (CheckParticleFrame, DrawDepthOnlyPrepass);

#[derive(Resource)]
struct DrawRoutes {
    opaque: DrawFunctionId,
    alpha: DrawFunctionId,
    transmissive: DrawFunctionId,
    transparent: DrawFunctionId,
    prepass_opaque: DrawFunctionId,
    prepass_alpha: DrawFunctionId,
    prepass_depth: DrawFunctionId,
    deferred_opaque: DrawFunctionId,
    deferred_alpha: DrawFunctionId,
    shadow: DrawFunctionId,
    shadow_depth: DrawFunctionId,
}

fn draw_id<P: PhaseItem, C: 'static>(world: &World) -> DrawFunctionId {
    world.resource::<DrawFunctions<P>>().read().id::<C>()
}

fn initialize_routes(world: &mut World) {
    let routes = DrawRoutes {
        opaque: draw_id::<Opaque3d, GuardedMain>(world),
        alpha: draw_id::<AlphaMask3d, GuardedMain>(world),
        transmissive: draw_id::<Transmissive3d, GuardedMain>(world),
        transparent: draw_id::<Transparent3d, GuardedMain>(world),
        prepass_opaque: draw_id::<Opaque3dPrepass, GuardedPrepass>(world),
        prepass_alpha: draw_id::<AlphaMask3dPrepass, GuardedPrepass>(world),
        prepass_depth: draw_id::<Opaque3dPrepass, GuardedDepthOnly>(world),
        deferred_opaque: draw_id::<Opaque3dDeferred, GuardedPrepass>(world),
        deferred_alpha: draw_id::<AlphaMask3dDeferred, GuardedPrepass>(world),
        shadow: draw_id::<Shadow, GuardedPrepass>(world),
        shadow_depth: draw_id::<Shadow, GuardedDepthOnly>(world),
    };
    world.insert_resource(routes);
}

fn set_route(
    properties: &mut MaterialProperties,
    label: &impl DrawFunctionLabel,
    id: DrawFunctionId,
) {
    let label = label.intern();
    if let Some((_, route)) = properties
        .draw_functions
        .iter_mut()
        .find(|(candidate, _)| *candidate == label)
    {
        *route = id;
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy render system parameters"
)]
fn route_particle_materials(
    mut materials: Option<ResMut<ErasedRenderAssets<PreparedMaterial>>>,
    frames: Res<MaterialFrames>,
    routes: Res<DrawRoutes>,
) {
    let Some(materials) = materials.as_mut() else {
        return;
    };
    let mut refused = Vec::new();
    for (id, material) in materials.iter_mut() {
        if !matches!(
            frames.0.get(&id),
            Some(MaterialFrame::Particle(_) | MaterialFrame::Invalid)
        ) {
            continue;
        }
        if material
            .properties
            .get_draw_function(MainPassOpaqueDrawFunction)
            == Some(routes.opaque)
        {
            continue;
        }
        let Some(properties) = Arc::get_mut(&mut material.properties) else {
            // An unguarded shared prepared material cannot be published. A
            // subsequent asset extraction can prepare it again.
            refused.push(id);
            continue;
        };
        set_route(properties, &MainPassOpaqueDrawFunction, routes.opaque);
        set_route(properties, &MainPassAlphaMaskDrawFunction, routes.alpha);
        set_route(
            properties,
            &MainPassTransmissiveDrawFunction,
            routes.transmissive,
        );
        set_route(
            properties,
            &MainPassTransparentDrawFunction,
            routes.transparent,
        );
        set_route(
            properties,
            &PrepassOpaqueDrawFunction,
            routes.prepass_opaque,
        );
        set_route(
            properties,
            &PrepassAlphaMaskDrawFunction,
            routes.prepass_alpha,
        );
        set_route(
            properties,
            &PrepassOpaqueDepthOnlyDrawFunction,
            routes.prepass_depth,
        );
        set_route(
            properties,
            &DeferredOpaqueDrawFunction,
            routes.deferred_opaque,
        );
        set_route(
            properties,
            &DeferredAlphaMaskDrawFunction,
            routes.deferred_alpha,
        );
        set_route(properties, &ShadowsDrawFunction, routes.shadow);
        set_route(
            properties,
            &ShadowsDepthOnlyDrawFunction,
            routes.shadow_depth,
        );
    }
    for id in refused {
        materials.remove(id);
        warn!("Source particle material was shared before its guarded draw route was installed");
    }
}

pub(super) fn install(app: &mut App) {
    app.add_systems(
        PostUpdate,
        isolate_particle_draws
            .after(super::projection::SourceParticleProjectionUpdate)
            .before(check_entities_needing_specialization::<SourceGraphMaterial>),
    );
    let Some(render) = app.get_sub_app_mut(RenderApp) else {
        return;
    };
    render
        .init_resource::<MaterialFrames>()
        .init_resource::<DrawFunctions<Opaque3d>>()
        .init_resource::<DrawFunctions<AlphaMask3d>>()
        .init_resource::<DrawFunctions<Transmissive3d>>()
        .init_resource::<DrawFunctions<Transparent3d>>()
        .init_resource::<DrawFunctions<Opaque3dPrepass>>()
        .init_resource::<DrawFunctions<AlphaMask3dPrepass>>()
        .init_resource::<DrawFunctions<Opaque3dDeferred>>()
        .init_resource::<DrawFunctions<AlphaMask3dDeferred>>()
        .init_resource::<DrawFunctions<Shadow>>()
        .add_render_command::<Opaque3d, GuardedMain>()
        .add_render_command::<AlphaMask3d, GuardedMain>()
        .add_render_command::<Transmissive3d, GuardedMain>()
        .add_render_command::<Transparent3d, GuardedMain>()
        .add_render_command::<Opaque3dPrepass, GuardedPrepass>()
        .add_render_command::<Opaque3dPrepass, GuardedDepthOnly>()
        .add_render_command::<AlphaMask3dPrepass, GuardedPrepass>()
        .add_render_command::<Opaque3dDeferred, GuardedPrepass>()
        .add_render_command::<AlphaMask3dDeferred, GuardedPrepass>()
        .add_render_command::<Shadow, GuardedPrepass>()
        .add_render_command::<Shadow, GuardedDepthOnly>()
        .add_systems(RenderStartup, initialize_routes)
        .add_systems(
            Render,
            (
                capture_material_frames
                    .in_set(RenderSystems::PrepareAssets)
                    .before(prepare_erased_assets::<MeshMaterial3d<SourceGraphMaterial>>),
                route_particle_materials
                    .in_set(RenderSystems::PrepareAssets)
                    .after(prepare_erased_assets::<MeshMaterial3d<SourceGraphMaterial>>),
            ),
        );
}

/// Attached only when this system itself adds `NoAutomaticBatching`.
#[derive(Component)]
struct OwnedNoBatching;

#[allow(
    clippy::needless_pass_by_value,
    clippy::type_complexity,
    reason = "Bevy scene query names independent batching ownership"
)]
fn isolate_particle_draws(
    mut commands: Commands,
    materials: Res<Assets<SourceGraphMaterial>>,
    mut draws: Query<(
        Entity,
        &MeshMaterial3d<SourceGraphMaterial>,
        &mut Mesh3d,
        Has<NoAutomaticBatching>,
        Has<OwnedNoBatching>,
    )>,
) {
    for (entity, material, mut mesh, no_batching, owned) in &mut draws {
        let Some(material) = materials.get(&material.0) else {
            continue;
        };
        let particle = material.extension.particle_frame_owner().is_some();
        if particle && !no_batching {
            commands
                .entity(entity)
                .insert((NoAutomaticBatching, OwnedNoBatching));
            mesh.set_changed();
        } else if !particle && owned {
            commands
                .entity(entity)
                .remove::<(NoAutomaticBatching, OwnedNoBatching)>();
            mesh.set_changed();
        }
    }
}

pub(crate) struct CheckParticleFrame;

fn admitted_frame<'a>(
    owner: &ParticleFrameOwner,
    current: Option<&'a CurrentFrames>,
    batchable: bool,
) -> Option<&'a Frame> {
    if batchable {
        return None;
    }
    current?
        .0
        .get(&owner.slot)
        .filter(|frame| frame.owner.matches(owner))
}

#[derive(Clone, Copy, Default)]
struct ViewAccess<'a> {
    main: Option<Entity>,
    capture: Option<Entity>,
    view: Option<&'a ExtractedView>,
    camera: Option<&'a ExtractedCamera>,
    jitter: Option<&'a TemporalJitter>,
    resolution: Option<&'a MainPassResolutionOverride>,
    shadow: bool,
}

fn raster_matches_frame(frame: &Frame, access: &ViewAccess<'_>) -> bool {
    access
        .view
        .zip(access.camera)
        .is_some_and(|(view, camera)| {
            view.world_from_view
                .to_matrix()
                .abs_diff_eq(frame.world_from_view, 1.0e-5)
                && super::raster_view_clip(view, camera, access.jitter, access.resolution)
                    .is_ok_and(|clip| {
                        clip.is_some_and(|(clip, _)| clip.abs_diff_eq(frame.clip_from_view, 1.0e-4))
                    })
        })
}

fn view_matches_frame(frame: &Frame, access: ViewAccess<'_>) -> bool {
    if access.shadow {
        // The current source output contains main-camera clip coordinates.
        // Every light view needs its own qualified projection before drawing.
        false
    } else if let Some(capture) = access.capture {
        capture == frame.view && raster_matches_frame(frame, &access)
    } else {
        access.main == Some(frame.view) && raster_matches_frame(frame, &access)
    }
}

impl<P: PhaseItem> RenderCommand<P> for CheckParticleFrame {
    type Param = (
        SRes<RenderMaterialInstances>,
        SRes<RenderMeshInstances>,
        Option<SRes<MaterialFrames>>,
        Option<SRes<CurrentFrames>>,
    );
    type ViewQuery = (
        Option<Read<MainEntity>>,
        Option<Read<crate::source_refraction::CaptureSource>>,
        Option<Read<ExtractedView>>,
        Option<Read<ExtractedCamera>>,
        Option<Read<TemporalJitter>>,
        Option<Read<MainPassResolutionOverride>>,
        Has<ShadowView>,
    );
    type ItemQuery = ();

    fn render<'w>(
        item: &P,
        (main, capture, view, camera, jitter, resolution, shadow): ROQueryItem<
            'w,
            '_,
            Self::ViewQuery,
        >,
        _: Option<()>,
        (materials, meshes, material_frames, current): SystemParamItem<'w, '_, Self::Param>,
        _: &mut TrackedRenderPass<'w>,
    ) -> RenderCommandResult {
        let Some(material) = materials.into_inner().instances.get(&item.main_entity()) else {
            return RenderCommandResult::Skip;
        };
        if material.asset_id.type_id() != std::any::TypeId::of::<SourceGraphMaterial>() {
            return RenderCommandResult::Success;
        }
        let Some(material_frames) = material_frames else {
            // The optional particle plugin is absent; preserve the ordinary path.
            return RenderCommandResult::Success;
        };
        let Some(material_frame) = material_frames.into_inner().0.get(&material.asset_id) else {
            return RenderCommandResult::Skip;
        };
        let owner = match material_frame {
            MaterialFrame::Ordinary => return RenderCommandResult::Success,
            MaterialFrame::Invalid => return RenderCommandResult::Skip,
            MaterialFrame::Particle(owner) => owner,
        };
        let batchable = meshes
            .into_inner()
            .render_mesh_queue_data(item.main_entity())
            .is_none_or(|mesh| mesh.should_batch());
        let Some(frame) = admitted_frame(owner, current.map(Res::into_inner), batchable) else {
            return RenderCommandResult::Skip;
        };
        if view_matches_frame(
            frame,
            ViewAccess {
                main: main.map(MainEntity::id),
                capture: capture.map(|source| source.view),
                view,
                camera,
                jitter,
                resolution,
                shadow,
            },
        ) {
            RenderCommandResult::Success
        } else {
            RenderCommandResult::Skip
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::ecs::system::RunSystemOnce;

    fn owner(output: AssetId<ShaderBuffer>) -> ParticleFrameOwner {
        ParticleFrameOwner {
            slot: 3,
            identity: Arc::new(()),
            output,
        }
    }

    #[test]
    fn draw_requires_the_same_owner_output_and_current_render_frame() {
        let (input, mut view, camera) = super::super::raster_tests::fixture();
        let camera_entity = input.camera.view;
        let mut buffers = Assets::<ShaderBuffer>::default();
        let output = buffers.add(ShaderBuffer::new(&[0_u8; 16], default()));
        let other_output = buffers.add(ShaderBuffer::new(&[0_u8; 16], default()));
        let owner = owner(output.id());
        let mut frames = CurrentFrames::default();
        assert!(admitted_frame(&owner, Some(&frames), false).is_none());
        frames.record(
            owner.clone(),
            camera_entity,
            input.camera.world_from_view,
            input.camera.clip_from_view,
        );
        let frame = admitted_frame(&owner, Some(&frames), false).unwrap();
        assert!(view_matches_frame(
            frame,
            ViewAccess {
                main: Some(camera_entity),
                view: Some(&view),
                camera: Some(&camera),
                ..default()
            }
        ));
        assert!(!view_matches_frame(
            frame,
            ViewAccess {
                main: Some(Entity::PLACEHOLDER),
                view: Some(&view),
                camera: Some(&camera),
                ..default()
            }
        ));
        view.world_from_view = GlobalTransform::from_translation(Vec3::X);
        assert!(!view_matches_frame(
            frame,
            ViewAccess {
                main: Some(camera_entity),
                view: Some(&view),
                camera: Some(&camera),
                ..default()
            }
        ));
        view.world_from_view = GlobalTransform::from(input.camera.world_from_view);
        let changed_jitter = TemporalJitter {
            offset: Vec2::new(0.25, -0.375),
        };
        assert!(!view_matches_frame(
            frame,
            ViewAccess {
                main: Some(camera_entity),
                view: Some(&view),
                camera: Some(&camera),
                jitter: Some(&changed_jitter),
                ..default()
            }
        ));
        assert!(admitted_frame(&owner, Some(&frames), true).is_none());
        assert!(
            admitted_frame(
                &ParticleFrameOwner {
                    output: other_output.id(),
                    ..owner.clone()
                },
                Some(&frames),
                false
            )
            .is_none()
        );
        assert!(
            admitted_frame(
                &ParticleFrameOwner {
                    identity: Arc::new(()),
                    ..owner.clone()
                },
                Some(&frames),
                false
            )
            .is_none()
        );
        frames.clear();
        assert!(admitted_frame(&owner, Some(&frames), false).is_none());
    }

    #[test]
    fn captures_require_the_same_view_and_raster_and_shadow_views_wait() {
        use bevy::render::view::RetainedViewEntity;
        let (input, mut view, camera) = super::super::raster_tests::fixture();
        let mut buffers = Assets::<ShaderBuffer>::default();
        let output = buffers.add(ShaderBuffer::new(&[0_u8; 16], default()));
        let owner = owner(output.id());
        let jitter = TemporalJitter {
            offset: Vec2::new(0.25, -0.375),
        };
        let (clip, _) = super::super::raster_view_clip(&view, &camera, Some(&jitter), None)
            .unwrap()
            .unwrap();
        let frame = Frame {
            owner,
            view: input.camera.view,
            world_from_view: input.camera.world_from_view,
            clip_from_view: clip,
        };
        assert!(view_matches_frame(
            &frame,
            ViewAccess {
                capture: Some(input.camera.view),
                view: Some(&view),
                camera: Some(&camera),
                jitter: Some(&jitter),
                ..default()
            }
        ));
        assert!(!view_matches_frame(
            &frame,
            ViewAccess {
                capture: Some(input.camera.view),
                view: Some(&view),
                camera: Some(&camera),
                ..default()
            }
        ));
        assert!(!view_matches_frame(
            &frame,
            ViewAccess {
                capture: Some(Entity::PLACEHOLDER),
                view: Some(&view),
                camera: Some(&camera),
                jitter: Some(&jitter),
                ..default()
            }
        ));
        view.retained_view_entity = RetainedViewEntity::new(
            Entity::PLACEHOLDER.into(),
            Some(input.camera.view.into()),
            0,
        );
        assert!(!view_matches_frame(
            &frame,
            ViewAccess {
                shadow: true,
                view: Some(&view),
                ..default()
            }
        ));
        view.retained_view_entity = RetainedViewEntity::new(Entity::PLACEHOLDER.into(), None, 0);
        assert!(!view_matches_frame(
            &frame,
            ViewAccess {
                shadow: true,
                view: Some(&view),
                ..default()
            }
        ));
    }

    #[test]
    fn source_particle_isolation_preserves_preexisting_batching_policy() {
        use crate::source_graph::SourceGraphStateBufferBinding;
        let (mut world, _, material) = crate::source_particle_host::tests::setup(true);
        let buffer = world
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::new(&[0_u8; 16], default()));
        let binding = SourceGraphStateBufferBinding::new(buffer.clone(), world.resource()).unwrap();
        world
            .resource_mut::<Assets<SourceGraphMaterial>>()
            .get_mut(&material)
            .unwrap()
            .extension
            .set_particle_frame_buffer(&binding, owner(buffer.id()));
        let owned = world
            .spawn((Mesh3d(Handle::default()), MeshMaterial3d(material.clone())))
            .id();
        let preexisting = world
            .spawn((
                Mesh3d(Handle::default()),
                MeshMaterial3d(material.clone()),
                NoAutomaticBatching,
            ))
            .id();
        world.run_system_once(isolate_particle_draws).unwrap();
        assert!(world.get::<OwnedNoBatching>(owned).is_some());
        assert!(world.get::<NoAutomaticBatching>(owned).is_some());
        assert!(world.get::<OwnedNoBatching>(preexisting).is_none());
        world
            .resource_mut::<Assets<SourceGraphMaterial>>()
            .get_mut(&material)
            .unwrap()
            .extension
            .clear_source_state_buffer();
        world.run_system_once(isolate_particle_draws).unwrap();
        assert!(world.get::<NoAutomaticBatching>(owned).is_none());
        assert!(world.get::<NoAutomaticBatching>(preexisting).is_some());
    }

    #[test]
    fn shadow_material_retains_the_particle_owner_in_its_extracted_version() {
        use crate::source_graph::{
            SourceGraphStateBufferBinding, source_graph_shadow_only_material,
        };
        let (mut world, _, material) = crate::source_particle_host::tests::setup(true);
        let buffer = world
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::new(&[0_u8; 16], default()));
        let expected = owner(buffer.id());
        let binding = SourceGraphStateBufferBinding::new(buffer, world.resource()).unwrap();
        world
            .resource_mut::<Assets<SourceGraphMaterial>>()
            .get_mut(&material)
            .unwrap()
            .extension
            .set_particle_frame_buffer(&binding, expected.clone());
        let shadow = source_graph_shadow_only_material(
            world
                .resource::<Assets<SourceGraphMaterial>>()
                .get(&material)
                .unwrap(),
        )
        .unwrap();
        assert!(shadow.extension.particle_frame_binding_valid());
        assert!(
            shadow
                .extension
                .particle_frame_owner()
                .unwrap()
                .matches(&expected)
        );
        let mut extracted = ExtractedAssets::<MeshMaterial3d<SourceGraphMaterial>>::default();
        extracted.extracted.push((material.id(), shadow));
        world.insert_resource(extracted);
        world.init_resource::<MaterialFrames>();
        world.run_system_once(capture_material_frames).unwrap();
        assert!(
            matches!(world.resource::<MaterialFrames>().0.get(&material.id().untyped()),
            Some(MaterialFrame::Particle(actual)) if actual.matches(&expected))
        );
    }
}
