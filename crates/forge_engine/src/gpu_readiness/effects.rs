//! Public render-effect prerequisites. Private render-node bind groups are not observed here.
#[cfg(feature = "physics-rapier")]
use crate::{
    EnvironmentProjectionStats, WaterProjectionStats, WeatherProjectionStats,
    global_quality::{GlobalQualityStats, gpu_submission::Key},
};
#[cfg(feature = "physics-rapier")]
use bevy::pbr::AtmosphereSettings;
use bevy::{
    anti_alias::fxaa::{CameraFxaaPipeline, Fxaa},
    anti_alias::taa::{
        TemporalAntiAliasHistoryTextures, TemporalAntiAliasPipelineId, TemporalAntiAliasing,
    },
    core_pipeline::{
        prepass::ViewPrepassTextures,
        tonemapping::{Tonemapping, TonemappingLuts, ViewTonemappingPipeline},
    },
    ecs::{query::QueryData, system::SystemParam},
    pbr::{
        Bluenoise, ContactShadowsBuffer, ExtractedAtmosphere, FogMeta, GpuAtmosphereSettings,
        GpuScatteringMedium, LightEntity, LightMeta, ScreenSpaceReflectionsBuffer,
        ScreenSpaceReflectionsPipelineId, ShadowView, ViewContactShadowsUniformOffset,
        ViewFogUniformOffset, ViewLightEntities, ViewLightsUniformOffset,
        ViewScreenSpaceReflectionsUniformOffset, ViewShadowBindings, ViewTransmissionTexture,
        resources::{
            AtmosphereTextures, AtmosphereTransforms, AtmosphereTransformsOffset, GpuAtmosphere,
        },
    },
    post_process::bloom::{BloomBindGroups, BloomTexture},
    prelude::*,
    render::{
        camera::ExtractedCamera,
        extract_component::{ComponentUniforms, DynamicUniformIndex},
        render_asset::RenderAssets,
        render_resource::PipelineCache,
        sync_world::MainEntity,
        texture::GpuImage,
        view::ViewTarget,
    },
};
use serde::Serialize;
use std::collections::HashSet;

#[derive(Clone, Debug, PartialEq, Eq)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "Independent requested render effects"
)]
pub struct Requirements {
    shadows: Option<(u32, u8)>,
    contact_shadows: bool,
    fog: bool,
    atmosphere: Option<[UVec3; 4]>,
    ssr: bool,
    transmission: bool,
    bloom: bool,
    tonemapping: bool,
    temporal_aa: bool,
    source_final: bool,
    fxaa: bool,
}

impl Requirements {
    /// Display effects actually requested by a module host; no absent world domains.
    #[must_use]
    pub fn display(
        shadows: forge_project::ShadowQualitySettings,
        post: forge_project::PostProcessQualitySettings,
    ) -> Self {
        Self {
            shadows: shadows
                .enabled
                .then_some((u32::from(shadows.map_resolution), shadows.cascade_count)),
            contact_shadows: shadows.contact_shadows_enabled,
            fog: false,
            atmosphere: None,
            ssr: false,
            transmission: false,
            bloom: post.bloom_enabled,
            tonemapping: post.tonemapping_enabled,
            temporal_aa: shadows.contact_shadows_enabled,
            source_final: false,
            fxaa: false,
        }
    }

    /// Replace the requested Bevy post effects with their actual common owner.
    /// Its encoding receipt and the selected FXAA pipeline remain mandatory.
    #[must_use]
    pub fn with_source_final(mut self, enabled: bool, fxaa: bool) -> Self {
        self.source_final = enabled;
        self.fxaa = fxaa;
        if enabled {
            self.bloom = false;
            self.tonemapping = false;
        }
        self
    }

    /// Require the antialiasing pipelines selected by the effective settings.
    #[must_use]
    pub fn with_antialias(mut self, temporal_aa: bool, fxaa: bool) -> Self {
        self.temporal_aa = temporal_aa;
        self.fxaa = fxaa && !temporal_aa;
        self
    }
}

#[cfg(feature = "physics-rapier")]
pub(crate) fn collect(world: &World, key: Option<&Key>) -> Option<Requirements> {
    let key = key?;
    let candidate = world
        .get_resource::<GlobalQualityStats>()?
        .effective
        .as_ref()?;
    if candidate.plan_hash != Some(key.identity.plan_hash) {
        return None;
    }
    let plan = &candidate.plan.as_ref()?.effective;
    let environment = world.get_resource::<EnvironmentProjectionStats>()?;
    let weather = world.get_resource::<WeatherProjectionStats>()?;
    let transmission = world
        .get_resource::<WaterProjectionStats>()?
        .optical_depth_preview;
    let atmosphere = if plan.environment.atmosphere_enabled && environment.sky.requested {
        let settings = world.get::<AtmosphereSettings>(key.camera)?;
        Some([
            settings.transmittance_lut_size.extend(1),
            settings.multiscattering_lut_size.extend(1),
            settings.sky_view_lut_size.extend(1),
            settings.aerial_view_lut_size,
        ])
    } else {
        None
    };
    Some(Requirements {
        shadows: plan.shadows.enabled.then_some((
            u32::from(plan.shadows.map_resolution),
            plan.shadows.cascade_count,
        )),
        contact_shadows: plan.shadows.contact_shadows_enabled,
        fog: environment.distance_fog.requested,
        atmosphere,
        ssr: !transmission
            && plan.water.reflection == forge_project::QualityWaterReflection::ScreenSpace,
        transmission,
        bloom: plan.post_process.bloom_enabled && weather.post_processing.bloom_requested,
        tonemapping: plan.post_process.tonemapping_enabled,
        temporal_aa: false,
        source_final: false,
        fxaa: false,
    })
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum EffectResource {
    LightUniforms,
    ShadowAtlasAndCascades,
    ContactShadowUniforms,
    TemporalHistoryAndPrepass,
    DistanceFogUniforms,
    AtmosphereLutTextures,
    AtmosphereUniformsAndMedium,
    ScreenReflectionPipeline,
    ScreenReflectionUniforms,
    ScreenReflectionPrepassAndNoise,
    TransmissionTextureAndDepth,
    BloomTexturesAndBindings,
    TonemappingPipelineAndLut,
    SourceFinalEncoding,
    FxaaPipeline,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
enum ToneInput {
    Analytic,
    Agx,
    Tony,
    Blender,
}

fn tone_input(mode: Tonemapping) -> Option<ToneInput> {
    match mode {
        Tonemapping::None => None,
        Tonemapping::AgX => Some(ToneInput::Agx),
        Tonemapping::TonyMcMapface => Some(ToneInput::Tony),
        Tonemapping::BlenderFilmic => Some(ToneInput::Blender),
        Tonemapping::Reinhard
        | Tonemapping::ReinhardLuminance
        | Tonemapping::AcesFitted
        | Tonemapping::SomewhatBoringDisplayTransform
        | Tonemapping::KhronosPbrNeutral => Some(ToneInput::Analytic),
    }
}

#[derive(Clone, Debug, Default, PartialEq, Serialize)]
pub struct GpuEffectEvidence {
    pub required_groups: usize,
    pub prepared_groups: usize,
    pub missing: Vec<EffectResource>,
    pub public_resources_ready: bool,
}

impl GpuEffectEvidence {
    fn check(&mut self, resource: EffectResource, ready: bool) {
        self.required_groups += 1;
        if ready {
            self.prepared_groups += 1;
        } else {
            self.missing.push(resource);
        }
        self.public_resources_ready = self.required_groups > 0 && self.missing.is_empty();
    }
}

#[derive(QueryData)]
struct EffectView {
    main: &'static MainEntity,
    camera: &'static ExtractedCamera,
    light_offset: Option<&'static ViewLightsUniformOffset>,
    shadows: Option<&'static ViewShadowBindings>,
    cascades: Option<&'static ViewLightEntities>,
    contact: Option<&'static ViewContactShadowsUniformOffset>,
    fog: Option<&'static ViewFogUniformOffset>,
    atmosphere: Option<&'static ExtractedAtmosphere>,
    atmosphere_textures: Option<&'static AtmosphereTextures>,
    atmosphere_offset: Option<&'static DynamicUniformIndex<GpuAtmosphere>>,
    atmosphere_settings_offset: Option<&'static DynamicUniformIndex<GpuAtmosphereSettings>>,
    atmosphere_transform_offset: Option<&'static AtmosphereTransformsOffset>,
    ssr: Option<&'static ScreenSpaceReflectionsPipelineId>,
    ssr_offset: Option<&'static ViewScreenSpaceReflectionsUniformOffset>,
    prepass: Option<&'static ViewPrepassTextures>,
    transmission: Option<&'static ViewTransmissionTexture>,
    bloom_texture: Option<&'static BloomTexture>,
    bloom_bindings: Option<&'static BloomBindGroups>,
    tonemapping: Option<&'static ViewTonemappingPipeline>,
    tonemapping_mode: Option<&'static Tonemapping>,
    temporal_aa: Option<&'static TemporalAntiAliasing>,
    temporal_history: Option<&'static TemporalAntiAliasHistoryTextures>,
    temporal_pipeline: Option<&'static TemporalAntiAliasPipelineId>,
    msaa: Option<&'static Msaa>,
    source_final: Option<&'static crate::source_final_post::Controls>,
    fxaa: Option<&'static Fxaa>,
    fxaa_pipeline: Option<&'static CameraFxaaPipeline>,
}

#[derive(SystemParam)]
pub(crate) struct GpuEffects<'w, 's> {
    source_final: Option<Res<'w, crate::source_final_post::observation::Observations>>,
    source_frame: Option<Res<'w, crate::source_final_post::observation::Frame>>,
    views: Query<'w, 's, EffectView, With<ViewTarget>>,
    shadows: Query<'w, 's, &'static LightEntity, With<ShadowView>>,
    lights: Option<Res<'w, LightMeta>>,
    fog: Option<Res<'w, FogMeta>>,
    contact: Option<Res<'w, ContactShadowsBuffer>>,
    atmosphere_uniforms: Option<Res<'w, ComponentUniforms<GpuAtmosphere>>>,
    atmosphere_settings: Option<Res<'w, ComponentUniforms<GpuAtmosphereSettings>>>,
    transforms: Option<Res<'w, AtmosphereTransforms>>,
    media: Option<Res<'w, RenderAssets<GpuScatteringMedium>>>,
    ssr: Option<Res<'w, ScreenSpaceReflectionsBuffer>>,
    images: Option<Res<'w, RenderAssets<GpuImage>>>,
    noise: Option<Res<'w, Bluenoise>>,
    tonemapping: Option<Res<'w, TonemappingLuts>>,
}

impl GpuEffects<'_, '_> {
    #[allow(
        clippy::too_many_lines,
        reason = "One ordered inventory of independent render prerequisites"
    )]
    #[cfg(feature = "physics-rapier")]
    pub(crate) fn observe(
        &self,
        key: Option<&Key>,
        required: Option<&Requirements>,
        pipelines: &PipelineCache,
    ) -> GpuEffectEvidence {
        let (Some(key), Some(required)) = (key, required) else {
            return GpuEffectEvidence::default();
        };
        self.observe_camera(
            (key.camera, key.viewport, key.origin, &key.target),
            required,
            pipelines,
        )
    }

    #[allow(
        clippy::too_many_lines,
        reason = "One ordered inventory of independent render prerequisites"
    )]
    pub(crate) fn observe_camera(
        &self,
        camera: (Entity, UVec2, UVec2, &bevy::camera::NormalizedRenderTarget),
        required: &Requirements,
        pipelines: &PipelineCache,
    ) -> GpuEffectEvidence {
        let mut evidence = GpuEffectEvidence::default();
        let (entity, viewport, origin, target) = camera;
        let mut views = self.views.iter().filter(|v| {
            v.main.id() == entity
                && v.camera.target.as_ref() == Some(target)
                && v.camera.physical_viewport_size == Some(viewport)
                && v.camera
                    .viewport
                    .as_ref()
                    .map_or(UVec2::ZERO, |v| v.physical_position)
                    == origin
        });
        let Some(view) = views.next() else {
            return evidence;
        };
        if views.next().is_some() {
            return evidence;
        }
        evidence.check(
            EffectResource::LightUniforms,
            view.light_offset.is_some()
                && self
                    .lights
                    .as_ref()
                    .is_some_and(|r| r.view_gpu_lights.binding().is_some()),
        );
        if let Some((resolution, count)) = required.shadows {
            let atlas = view.shadows.is_some_and(|s| {
                let size = s.directional_light_depth_texture.size();
                size.width == resolution
                    && size.height == resolution
                    && size.depth_or_array_layers >= u32::from(count)
            });
            let cascades = view.cascades.is_some_and(|c| {
                let indices = c
                    .lights
                    .iter()
                    .filter_map(|id| match self.shadows.get(*id).ok()? {
                        LightEntity::Directional {
                            light_entity,
                            cascade_index,
                        } => Some((*light_entity, *cascade_index)),
                        _ => None,
                    })
                    .collect::<Vec<_>>();
                valid_cascades(&indices, count)
            });
            evidence.check(EffectResource::ShadowAtlasAndCascades, atlas && cascades);
        }
        if required.contact_shadows {
            evidence.check(
                EffectResource::ContactShadowUniforms,
                view.contact.is_some()
                    && self
                        .contact
                        .as_ref()
                        .is_some_and(|r| r.0.binding().is_some()),
            );
        }
        if required.temporal_aa {
            // Bevy keeps history textures and the pipeline id's internals private.
            // Their real prepared components, the single-sample depth/motion
            // textures and the enclosing stable pipeline fence are observable.
            let prepass = view.prepass.is_some_and(|p| {
                p.depth
                    .as_ref()
                    .zip(p.motion_vectors.as_ref())
                    .is_some_and(|(depth, motion)| {
                        depth.texture.texture.sample_count() == 1
                            && motion.texture.texture.sample_count() == 1
                            && view.camera.physical_target_size
                                == Some(UVec2::new(p.size.width, p.size.height))
                    })
            });
            evidence.check(
                EffectResource::TemporalHistoryAndPrepass,
                view.temporal_aa.is_some()
                    && view.temporal_history.is_some()
                    && view.temporal_pipeline.is_some()
                    && view.msaa == Some(&Msaa::Off)
                    && prepass,
            );
        }
        if required.fog {
            evidence.check(
                EffectResource::DistanceFogUniforms,
                view.fog.is_some()
                    && self
                        .fog
                        .as_ref()
                        .is_some_and(|r| r.gpu_fogs.binding().is_some()),
            );
        }
        if let Some(sizes) = required.atmosphere {
            evidence.check(
                EffectResource::AtmosphereLutTextures,
                view.atmosphere_textures.is_some_and(|t| {
                    [
                        &t.transmittance_lut,
                        &t.multiscattering_lut,
                        &t.sky_view_lut,
                        &t.aerial_view_lut,
                    ]
                    .iter()
                    .zip(sizes)
                    .all(|(texture, expected)| {
                        let size = texture.texture.size();
                        expected.min_element() > 0
                            && UVec3::new(size.width, size.height, size.depth_or_array_layers)
                                == expected
                    })
                }),
            );
            evidence.check(
                EffectResource::AtmosphereUniformsAndMedium,
                view.atmosphere_offset.is_some()
                    && view.atmosphere_settings_offset.is_some()
                    && view.atmosphere_transform_offset.is_some()
                    && self
                        .atmosphere_uniforms
                        .as_ref()
                        .is_some_and(|r| r.binding().is_some())
                    && self
                        .atmosphere_settings
                        .as_ref()
                        .is_some_and(|r| r.binding().is_some())
                    && self
                        .transforms
                        .as_ref()
                        .is_some_and(|r| r.uniforms().binding().is_some())
                    && view.atmosphere.is_some_and(|a| {
                        self.media
                            .as_ref()
                            .is_some_and(|m| m.get(a.medium).is_some())
                    }),
            );
        }
        if required.transmission {
            // This texture covers the full render target, not just the camera viewport.
            // Presence does not prove that the expected pixels were copied or sampled.
            evidence.check(
                EffectResource::TransmissionTextureAndDepth,
                transmission_view_matches(
                    view.camera.physical_target_size,
                    view.camera.physical_viewport_size,
                    view.camera
                        .viewport
                        .as_ref()
                        .map_or(UVec2::ZERO, |v| v.physical_position),
                ) && view.prepass.is_some_and(|p| p.depth.is_some())
                    && view.transmission.is_some_and(|t| {
                        transmission_extent_matches(
                            t.texture.size(),
                            view.camera.physical_target_size,
                        ) && t.texture.sample_count() == 1
                    }),
            );
        }
        if required.ssr {
            evidence.check(
                EffectResource::ScreenReflectionPipeline,
                view.ssr
                    .is_some_and(|id| pipelines.get_render_pipeline(id.0).is_some()),
            );
            evidence.check(
                EffectResource::ScreenReflectionUniforms,
                view.ssr_offset.is_some()
                    && self.ssr.as_ref().is_some_and(|r| r.binding().is_some()),
            );
            evidence.check(
                EffectResource::ScreenReflectionPrepassAndNoise,
                view.prepass
                    .is_some_and(|p| p.depth.is_some() && p.deferred.is_some())
                    && self
                        .noise
                        .as_ref()
                        .is_some_and(|n| self.image_prepared(&n.texture)),
            );
        }
        if required.bloom {
            evidence.check(
                EffectResource::BloomTexturesAndBindings,
                view.bloom_texture.is_some() && view.bloom_bindings.is_some(),
            );
        }
        if required.tonemapping {
            let input_ready = match view.tonemapping_mode.and_then(|mode| tone_input(*mode)) {
                Some(ToneInput::Analytic) => true,
                Some(ToneInput::Agx) => self
                    .tonemapping
                    .as_ref()
                    .is_some_and(|luts| self.image_prepared(&luts.agx)),
                Some(ToneInput::Tony) => self
                    .tonemapping
                    .as_ref()
                    .is_some_and(|luts| self.image_prepared(&luts.tony_mc_mapface)),
                Some(ToneInput::Blender) => self
                    .tonemapping
                    .as_ref()
                    .is_some_and(|luts| self.image_prepared(&luts.blender_filmic)),
                None => false,
            };
            evidence.check(
                EffectResource::TonemappingPipelineAndLut,
                view.tonemapping.is_some() && input_ready,
            );
        }
        if required.source_final {
            let prepared = self
                .source_final
                .as_ref()
                .zip(self.source_frame.as_ref())
                .zip(view.source_final)
                .and_then(|((observations, frame), controls)| {
                    let submission = observations.snapshot(entity)?;
                    Some(
                        submission.matches(controls, viewport, frame.0)
                            && view.msaa == Some(&Msaa::Off),
                    )
                })
                .unwrap_or(false);
            evidence.check(EffectResource::SourceFinalEncoding, prepared);
        }
        if required.fxaa {
            evidence.check(
                EffectResource::FxaaPipeline,
                view.fxaa.is_some_and(|fxaa| fxaa.enabled)
                    && view.msaa == Some(&Msaa::Off)
                    && view
                        .fxaa_pipeline
                        .is_some_and(|p| pipelines.get_render_pipeline(p.pipeline_id).is_some()),
            );
        }
        evidence
    }

    fn image_prepared(&self, handle: &Handle<Image>) -> bool {
        self.images
            .as_ref()
            .is_some_and(|images| images.get(handle).is_some())
    }
}

// The forward ray adapter explicitly declines subviewports in WGSL. Texture
// allocation alone must not acknowledge effects that the shader will skip.
fn transmission_view_matches(
    target: Option<UVec2>,
    viewport: Option<UVec2>,
    origin: UVec2,
) -> bool {
    origin == UVec2::ZERO
        && target.is_some_and(|size| size.min_element() > 0 && viewport == Some(size))
}

fn transmission_extent_matches(
    size: bevy::render::render_resource::Extent3d,
    target: Option<UVec2>,
) -> bool {
    target.is_some_and(|target| {
        target.x > 0
            && target.y > 0
            && size.width == target.x
            && size.height == target.y
            && size.depth_or_array_layers == 1
    })
}

fn valid_cascades(indices: &[(Entity, usize)], count: u8) -> bool {
    let Some((light, _)) = indices.first() else {
        return false;
    };
    count > 0
        && indices.len() == usize::from(count)
        && indices
            .iter()
            .all(|(owner, index)| owner == light && *index < usize::from(count))
        && indices
            .iter()
            .map(|(_, index)| *index)
            .collect::<HashSet<_>>()
            .len()
            == usize::from(count)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn forward_acknowledgment_rejects_subviewports_skipped_by_the_shader() {
        let size = Some(UVec2::new(1920, 1080));
        assert!(transmission_view_matches(size, size, UVec2::ZERO));
        assert!(!transmission_view_matches(size, size, UVec2::ONE));
        assert!(!transmission_view_matches(
            size,
            Some(UVec2::new(960, 540)),
            UVec2::ZERO
        ));
        assert!(!transmission_view_matches(None, size, UVec2::ZERO));
        assert!(!transmission_view_matches(size, None, UVec2::ZERO));
        assert!(!transmission_view_matches(
            Some(UVec2::ZERO),
            Some(UVec2::ZERO),
            UVec2::ZERO
        ));
    }

    #[test]
    fn transmission_requires_the_full_target_extent_not_a_viewport_or_stale_texture() {
        use bevy::render::render_resource::Extent3d;
        let size = Extent3d {
            width: 1920,
            height: 1080,
            depth_or_array_layers: 1,
        };
        assert!(transmission_extent_matches(
            size,
            Some(UVec2::new(1920, 1080))
        ));
        assert!(!transmission_extent_matches(size, None));
        assert!(!transmission_extent_matches(size, Some(UVec2::ZERO)));
        assert!(!transmission_extent_matches(
            size,
            Some(UVec2::new(960, 540))
        ));
        assert!(!transmission_extent_matches(
            Extent3d {
                depth_or_array_layers: 2,
                ..size
            },
            Some(UVec2::new(1920, 1080))
        ));
    }

    #[test]
    fn empty_inventory_is_not_ready_and_missing_groups_stay_visible() {
        let mut evidence = GpuEffectEvidence::default();
        assert!(!evidence.public_resources_ready);
        evidence.check(EffectResource::LightUniforms, true);
        assert!(evidence.public_resources_ready);
        evidence.check(EffectResource::AtmosphereLutTextures, false);
        evidence.check(EffectResource::ScreenReflectionPipeline, true);
        assert!(!evidence.public_resources_ready);
        assert_eq!(evidence.required_groups, 3);
        assert_eq!(evidence.prepared_groups, 2);
        assert_eq!(evidence.missing, [EffectResource::AtmosphereLutTextures]);
    }

    #[test]
    fn shadow_cascades_require_every_index_from_one_light_without_duplicates() {
        let light = Entity::from_raw_u32(1).unwrap();
        let other = Entity::from_raw_u32(2).unwrap();
        assert!(valid_cascades(&[(light, 1), (light, 0)], 2));
        assert!(!valid_cascades(&[], 0));
        for indices in [
            vec![(light, 0)],
            vec![(light, 0), (light, 0)],
            vec![(light, 0), (light, 2)],
            vec![(light, 0), (other, 1)],
        ] {
            assert!(!valid_cascades(&indices, 2));
        }
    }

    #[test]
    #[cfg(feature = "physics-rapier")]
    fn missing_source_context_does_not_produce_an_empty_successful_requirement() {
        let world = World::new();
        assert!(collect(&world, None).is_none());
        assert!(
            collect(
                &world,
                Some(&crate::global_quality::gpu_submission::tests::key())
            )
            .is_none()
        );
    }

    #[test]
    fn tone_mapping_admission_uses_the_active_algorithm_and_its_actual_input() {
        assert_eq!(tone_input(Tonemapping::None), None);
        assert_eq!(
            tone_input(Tonemapping::AcesFitted),
            Some(ToneInput::Analytic)
        );
        assert_eq!(tone_input(Tonemapping::Reinhard), Some(ToneInput::Analytic));
        assert_eq!(tone_input(Tonemapping::AgX), Some(ToneInput::Agx));
        assert_eq!(
            tone_input(Tonemapping::TonyMcMapface),
            Some(ToneInput::Tony)
        );
        assert_eq!(
            tone_input(Tonemapping::BlenderFilmic),
            Some(ToneInput::Blender)
        );
    }
}
