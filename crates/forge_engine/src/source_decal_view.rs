//! Per-camera source decal attachments, rendered between depth and lighting.
use crate::{
    source_decal_prepare::{prepare_source_decal_draws, prepare_source_decals},
    source_decal_raster::{Raster, Sampling, TARGET_FORMAT, Targets},
    source_decal_receiver::SourceDecalReceiverPlugin,
};
use bevy::{
    camera::MainPassResolutionOverride,
    core_pipeline::{
        prepass::{DepthPrepass, ViewPrepassTextures},
        schedule::{Core3d, Core3dSystems},
    },
    prelude::*,
    render::{
        RenderApp,
        camera::TemporalJitter,
        extract_component::{ExtractComponent, ExtractComponentPlugin},
        render_asset::RenderAssets,
        render_resource::TextureUsages,
        renderer::{RenderContext, RenderDevice, RenderQueue, ViewQuery, WgpuWrapper},
        texture::GpuImage,
        view::{ExtractedView, ViewDepthTexture},
    },
};
use std::sync::{Arc, Mutex};

#[derive(Clone)]
pub struct SourceDecalRaster(Arc<WgpuWrapper<Raster>>);

impl SourceDecalRaster {
    pub fn preview_frame(&self) -> Result<(Vec3, Vec3, f32), String> {
        self.0.preview_frame()
    }

    /// A GLB with a decal declaration may not fall back to a glTF mesh.
    pub async fn prepare(
        device: &RenderDevice,
        queue: &RenderQueue,
        bytes: &[u8],
    ) -> Result<Self, String> {
        let glb = forge_assets::source_decal_glb::read_source_decal_glb(bytes)?
            .ok_or("Déclaration de toile source absente")?;
        let [material] = prepare_source_decals(&glb)?
            .try_into()
            .map_err(|_| "Un seul matériau est attendu pour une toile")?;
        let [draw] = prepare_source_decal_draws(&glb)?
            .try_into()
            .map_err(|_| "Un seul maillage est attendu pour une toile")?;
        let raster = Raster::prepare(
            device,
            queue,
            &material,
            draw,
            Sampling::SceneLinear,
            wgpu::FrontFace::Ccw,
        )
        .await?;
        Ok(Self(Arc::new(WgpuWrapper::new(raster))))
    }
}

#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub enum SourceDecalViewStatus {
    #[default]
    Pending,
    Refused(String),
    /// Command encoding only. A visible result requires a readback or UI proof.
    Encoded(usize),
}

#[derive(Component, Clone, ExtractComponent)]
#[require(DepthPrepass)]
pub struct SourceDecalView {
    targets: [Handle<Image>; 2],
    draws: Vec<(SourceDecalRaster, Mat4)>,
    status: Arc<Mutex<SourceDecalViewStatus>>,
}

impl SourceDecalView {
    /// One pair per camera. Bind the same handles to its opaque receivers.
    pub fn new(images: &mut Assets<Image>, size: UVec2) -> Result<Self, String> {
        if size.min_element() == 0 || size.max_element() > 4096 {
            return Err("Dimensions de vue incompatibles avec les toiles".into());
        }
        let mut target = |pixel: [u8; 4]| {
            let mut image = Image::new_target_texture(size.x, size.y, TARGET_FORMAT, None);
            image.texture_descriptor.usage |= TextureUsages::COPY_SRC | TextureUsages::COPY_DST;
            image.data = Some(pixel.repeat((size.x * size.y) as usize));
            images.add(image)
        };
        Ok(Self {
            targets: [target([0, 0, 0, 255]), target([128, 128, 128, 255])],
            draws: Vec::new(),
            status: Arc::default(),
        })
    }

    pub fn targets(&self) -> [Handle<Image>; 2] {
        self.targets.clone()
    }

    /// Opt in a host-owned receiver rendered exclusively by this camera.
    /// No unrelated or shared material should be changed by the caller.
    pub fn bind_graph_receiver(
        &self,
        material: &mut crate::source_graph::SourceGraphMaterial,
    ) -> bool {
        material.extension.bind_decal_view(self.targets())
    }

    pub fn set_draws(&mut self, draws: Vec<(SourceDecalRaster, Mat4)>) -> Result<(), String> {
        if draws.len() > 256 {
            return Err("Trop de toiles actives sur cette vue".into());
        }
        if self.draws.len() != draws.len()
            || self
                .draws
                .iter()
                .zip(&draws)
                .any(|((a, from), (b, to))| !Arc::ptr_eq(&a.0, &b.0) || from != to)
        {
            self.draws = draws;
            self.status = Arc::default();
        }
        Ok(())
    }

    pub fn status(&self) -> SourceDecalViewStatus {
        self.status
            .lock()
            .map(|status| status.clone())
            .unwrap_or_else(|_| {
                SourceDecalViewStatus::Refused("État du rendu des toiles indisponible".into())
            })
    }
}

pub struct SourceDecalViewPlugin;

impl Plugin for SourceDecalViewPlugin {
    fn build(&self, app: &mut App) {
        app.add_plugins((
            SourceDecalReceiverPlugin,
            ExtractComponentPlugin::<SourceDecalView>::default(),
        ));
        if let Some(render) = app.get_sub_app_mut(RenderApp) {
            render.add_systems(
                Core3d,
                encode
                    .after(Core3dSystems::Prepass)
                    .before(Core3dSystems::MainPass),
            );
        }
    }
}

fn encode(
    view: ViewQuery<(
        Option<&SourceDecalView>,
        &ExtractedView,
        Option<&ViewDepthTexture>,
        Option<&ViewPrepassTextures>,
        Option<&TemporalJitter>,
        Has<MainPassResolutionOverride>,
    )>,
    device: Res<RenderDevice>,
    images: Res<RenderAssets<GpuImage>>,
    mut context: RenderContext,
) {
    let (Some(binding), view, depth, prepass, jitter, resolution_override) = view.into_inner()
    else {
        return;
    };
    let status = (|| {
        let [Some(color), Some(normal)] = binding.targets.each_ref().map(|id| images.get(id))
        else {
            return Ok(SourceDecalViewStatus::Pending);
        };
        let targets = Targets::from_images(color, normal)?;
        let size = color.texture.size();
        if resolution_override || view.viewport.to_array() != [0, 0, size.width, size.height] {
            targets.clear(context.command_encoder());
            return Err("Le rendu des toiles exige une vue à résolution entière".into());
        }
        let Some(depth) = depth.filter(|_| prepass.is_some_and(|p| p.depth.is_some())) else {
            targets.clear(context.command_encoder());
            return Err("Prépass de profondeur manquant pour les toiles".into());
        };
        let view_from_world = view.world_from_view.to_matrix().inverse();
        let clip = if let Some(jitter) = jitter {
            let mut projection = view.clip_from_view;
            jitter.jitter_projection(&mut projection, view.viewport.as_vec4().zw());
            projection * view_from_world
        } else {
            view.clip_from_world
                .unwrap_or(view.clip_from_view * view_from_world)
        };
        let result = binding
            .draws
            .iter()
            .map(|(raster, world)| raster.0.project(&device, *world, clip))
            .collect::<Result<Vec<_>, _>>()
            .and_then(|draws| targets.encode(context.command_encoder(), depth, &draws));
        if let Err(error) = result {
            targets.clear(context.command_encoder());
            return Err(error);
        }
        Ok(SourceDecalViewStatus::Encoded(binding.draws.len()))
    })()
    .unwrap_or_else(SourceDecalViewStatus::Refused);
    if let Ok(mut last) = binding.status.lock() {
        *last = status;
    }
}
