//! Common source-profile graphics settings and scene application.
// SPDX-License-Identifier: MIT OR Apache-2.0
// Derived from the shared Forge reference adapter; source render algorithms
// retain their original notices in the engine render passes.
use bevy::{
    anti_alias::{
        contrast_adaptive_sharpening::ContrastAdaptiveSharpening, fxaa::Fxaa,
        taa::TemporalAntiAliasing,
    },
    asset::AssetEvent,
    camera::Hdr,
    core_pipeline::{
        prepass::{
            BackgroundMotionVectorsBindGroup, BackgroundMotionVectorsPipelineId, DepthPrepass,
            MotionVectorPrepass, NoBackgroundMotionVectors,
        },
        tonemapping::Tonemapping,
    },
    ecs::message::{MessageCursor, Messages},
    post_process::{
        auto_exposure::{AutoExposure, AutoExposureCompensationCurve, AutoExposurePlugin},
        bloom::Bloom,
        effect_stack::Vignette,
        motion_blur::MotionBlur,
    },
    prelude::*,
    render::{
        Render, RenderApp, RenderSystems,
        camera::{MipBias, TemporalJitter},
        view::ColorGrading,
    },
};
use forge_project::PostProcessQualitySettings;
use serde::{Deserialize, Serialize};

pub mod admission;
mod controls;
pub mod quality_policy;
pub use controls::Control;

#[derive(Clone, Copy, Debug, Deserialize, Serialize, PartialEq)]
#[serde(default)]
#[serde(deny_unknown_fields)]
pub struct Settings {
    pub auto_exposure: f32,
    pub ambient_occlusion: f32,
    pub fxaa: f32,
    pub temporal_aa: f32,
    pub exposure_ev: f32,
    pub bloom: f32,
    pub saturation: f32,
    pub contrast: f32,
    pub sharpen: f32,
    pub vignette: f32,
    pub film_grain: f32,
    pub haze: f32,
    pub bounce_light: f32,
    pub motion_blur_degrees: f32,
    pub lens_flare: f32,
    pub underwater_shafts: f32,
    pub underwater_lighting: f32,
    pub underwater_caustics: f32,
    pub underwater_shadow_refraction: f32,
    pub water_refraction_capture: f32,
}

impl Default for Settings {
    fn default() -> Self {
        Self {
            auto_exposure: 1.0,
            ambient_occlusion: 1.0,
            fxaa: 1.0,
            temporal_aa: 0.0,
            exposure_ev: 0.4,
            bloom: 0.05,
            saturation: 1.06,
            contrast: 1.04,
            sharpen: 0.2,
            vignette: 0.28,
            film_grain: 0.012,
            haze: 1.6,
            bounce_light: 1.0,
            // Keep camera movement readable; cinematic blur remains opt-in.
            motion_blur_degrees: 0.0,
            lens_flare: 1.0,
            underwater_shafts: 1.0,
            underwater_lighting: 1.0,
            underwater_caustics: 0.75,
            underwater_shadow_refraction: 1.0,
            water_refraction_capture: 1.0,
        }
    }
}

impl Settings {
    #[must_use]
    pub fn valid(self) -> bool {
        Control::ALL.into_iter().all(|control| {
            let (low, high, _) = control.bounds();
            (low..=high).contains(&control.value(&self))
        })
    }

    /// Keep the reference values while applying the module's explicit tier.
    pub fn for_quality(
        quality: PostProcessQualitySettings,
        temporal_aa: bool,
    ) -> Result<Self, String> {
        validate_quality(quality)?;
        let mut value = Self::default().gated(quality);
        value.temporal_aa = if temporal_aa { 1.0 } else { 0.0 };
        value.fxaa = if temporal_aa { 0.0 } else { 1.0 };
        Ok(value)
    }

    fn gated(mut self, quality: PostProcessQualitySettings) -> Self {
        if !quality.bloom_enabled {
            self.bloom = 0.0;
        }
        if !quality.color_grading_enabled {
            self.saturation = 1.0;
            self.contrast = 1.0;
            self.vignette = 0.0;
            self.film_grain = 0.0;
        }
        self
    }

    /// The optional source final pass uses these same values and quality gates.
    pub fn final_controls(
        self,
        quality: PostProcessQualitySettings,
    ) -> Result<Option<crate::source_final_post::Controls>, String> {
        validate_quality(quality)?;
        if !self.valid() {
            return Err("Invalid source graphics settings".into());
        }
        if !quality.tonemapping_enabled {
            return Ok(None);
        }
        Ok(Some(crate::source_final_post::Controls {
            grade: if quality.color_grading_enabled {
                Vec4::new(self.vignette, self.saturation, self.contrast, 0.02)
            } else {
                Vec4::new(0.0, 1.0, 1.0, 0.0)
            },
            effects: Vec4::new(
                if quality.color_grading_enabled {
                    self.film_grain
                } else {
                    0.0
                },
                if quality.bloom_enabled {
                    self.bloom
                } else {
                    0.0
                },
                0.55 * (self.exposure_ev - 0.4).exp2(),
                self.auto_exposure,
            ),
            timing: Vec4::new(0.0, 0.0, self.sharpen, 0.0),
            frame: UVec4::ZERO,
        }))
    }
}

fn validate_quality(quality: PostProcessQualitySettings) -> Result<(), String> {
    if !quality.tonemapping_enabled && (quality.bloom_enabled || quality.color_grading_enabled) {
        return Err("Source display grading and bloom require final tone mapping".into());
    }
    Ok(())
}

/// Exposure assets are runtime resources, not persisted project data.
#[derive(Resource, Clone)]
pub struct ExposureAssets {
    pub curve: Handle<AutoExposureCompensationCurve>,
    pub mask: Handle<Image>,
}

#[derive(Resource)]
struct ActiveBounce(f32);

#[derive(Resource)]
struct BounceSyncInstalled;

#[derive(Resource)]
struct MotionVectorCleanupInstalled;

fn install_motion_vector_cleanup(app: &mut App) {
    let Some(render) = app.get_sub_app_mut(RenderApp) else {
        return;
    };
    if !render
        .world()
        .contains_resource::<MotionVectorCleanupInstalled>()
    {
        render
            .insert_resource(MotionVectorCleanupInstalled)
            .add_systems(
                Render,
                retire_background_motion_vectors.in_set(RenderSystems::PrepareBindGroups),
            );
    }
}

// Bevy 0.19 prepares these render-only components for active motion prepasses,
// but leaves them behind when the prepass or background contribution is disabled.
// Its prepass node still consumes them, even without a motion texture attachment.
// Retire only the inactive pass's cache before rendering; active views are untouched.
fn retire_background_motion_vectors(world: &mut World) {
    let mut views = world.query_filtered::<(
        Entity,
        Has<MotionVectorPrepass>,
        Has<NoBackgroundMotionVectors>,
    ), Or<(
        With<BackgroundMotionVectorsPipelineId>,
        With<BackgroundMotionVectorsBindGroup>,
    )>>();
    let retired: Vec<_> = views
        .iter(world)
        .filter_map(|(entity, motion, disabled)| (!motion || disabled).then_some(entity))
        .collect();
    for entity in retired {
        world.entity_mut(entity).remove::<(
            BackgroundMotionVectorsPipelineId,
            BackgroundMotionVectorsBindGroup,
        )>();
    }
}

// Scene materials can arrive after the player's graphics choice. Apply the
// current value to those assets without walking every material every frame.
fn sync_bounce_materials(
    active: Option<Res<ActiveBounce>>,
    events: Option<Res<Messages<AssetEvent<crate::source_surface::SourceSurfaceMaterial>>>>,
    materials: Option<ResMut<Assets<crate::source_surface::SourceSurfaceMaterial>>>,
    mut cursor: Local<MessageCursor<AssetEvent<crate::source_surface::SourceSurfaceMaterial>>>,
) {
    let (Some(active), Some(events), Some(mut materials)) = (active, events, materials) else {
        return;
    };
    for event in cursor.read(&events) {
        let id = match event {
            AssetEvent::Added { id } | AssetEvent::Modified { id } => *id,
            _ => continue,
        };
        let needs_update = materials.get(id).is_some_and(|material| {
            material.ground_bounce.is_some()
                && material.bounce_settings.x.to_bits() != active.0.to_bits()
        });
        if needs_update && let Some(mut material) = materials.get_mut(id) {
            material.bounce_settings.x = active.0;
        }
    }
}

/// Install the reference renderer passes once and create their common exposure inputs.
pub fn install(app: &mut App) -> Result<(), String> {
    install_motion_vector_cleanup(app);
    if !app.is_plugin_added::<AutoExposurePlugin>() {
        app.add_plugins(AutoExposurePlugin);
    }
    if !app.is_plugin_added::<crate::film_grain::FilmGrainPlugin>() {
        app.add_plugins(crate::film_grain::FilmGrainPlugin);
    }
    if !app.is_plugin_added::<crate::source_air_haze::SourceAirHazePlugin>() {
        app.add_plugins(crate::source_air_haze::SourceAirHazePlugin);
    }
    if !app.is_plugin_added::<crate::ambient_occlusion::AmbientOcclusionPlugin>() {
        app.add_plugins(crate::ambient_occlusion::AmbientOcclusionPlugin);
    }
    if !app.is_plugin_added::<crate::lens_flare::LensFlarePlugin>() {
        app.add_plugins(crate::lens_flare::LensFlarePlugin);
    }
    if !app.is_plugin_added::<crate::source_surface::ImportedSunRelayPlugin>() {
        app.add_plugins(crate::source_surface::ImportedSunRelayPlugin);
    }
    if !app.world().contains_resource::<BounceSyncInstalled>() {
        app.insert_resource(BounceSyncInstalled)
            .add_systems(Update, sync_bounce_materials);
    }
    if app.world().contains_resource::<ExposureAssets>() {
        return Ok(());
    }
    let curve = crate::exposure_metering::ExposureProfile::default()
        .compensation_curve()
        .map_err(|error| error.to_string())?;
    let curve = app
        .world_mut()
        .get_resource_mut::<Assets<AutoExposureCompensationCurve>>()
        .ok_or("Exposure curve assets unavailable")?
        .add(curve);
    let mask = app
        .world_mut()
        .get_resource_mut::<Assets<Image>>()
        .ok_or("Exposure mask assets unavailable")?
        .add(crate::exposure_metering::center_weighted_metering_mask());
    app.insert_resource(ExposureAssets { curve, mask });
    Ok(())
}

/// The reference presentation and its optional source-final variant.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Pipeline {
    Reference,
    SourceFinal,
}

/// Apply the same graphics logic to an admitted scene camera.
/// The caller supplies tier, camera and scene data; no module identity is read.
#[allow(clippy::too_many_lines)]
pub fn apply(
    world: &mut World,
    camera: Entity,
    values: Settings,
    quality: PostProcessQualitySettings,
    pipeline: Pipeline,
) -> Result<(), String> {
    if !values.valid() {
        return Err("Invalid source graphics settings".into());
    }
    validate_quality(quality)?;
    let values = values.gated(quality);
    if world.get_entity(camera).is_err() {
        return Err("Graphics camera is absent".into());
    }
    let exposure = if pipeline == Pipeline::Reference
        && quality.tonemapping_enabled
        && values.auto_exposure > 0.5
    {
        Some(
            world
                .get_resource::<ExposureAssets>()
                .ok_or("Exposure assets unavailable")?
                .clone(),
        )
    } else {
        None
    };
    let final_controls = if pipeline == Pipeline::SourceFinal {
        values.final_controls(quality)?
    } else {
        None
    };
    if let Some(mut underwater) =
        world.get_resource_mut::<crate::source_underwater::SourceUnderwaterControl>()
    {
        underwater.shafts_strength = values.underwater_shafts;
    }
    if let Some(mut lighting) =
        world.get_resource_mut::<crate::source_underwater_lighting::Control>()
    {
        let next = crate::source_underwater_lighting::Control {
            enabled: values.underwater_lighting > 0.5,
            caustics_strength: values.underwater_caustics,
            shadow_refraction: values.underwater_shadow_refraction > 0.5,
        };
        if *lighting != next {
            *lighting = next;
        }
    }
    if let Some(mut refraction) =
        world.get_resource_mut::<crate::source_refraction_capture::Control>()
    {
        let enabled = values.water_refraction_capture > 0.5;
        if refraction.enabled != enabled {
            refraction.enabled = enabled;
        }
    }
    world.insert_resource(ActiveBounce(values.bounce_light));
    if let Some(mut materials) =
        world.get_resource_mut::<Assets<crate::source_surface::SourceSurfaceMaterial>>()
    {
        let changed: Vec<_> = materials
            .iter()
            .filter_map(|(id, material)| {
                (material.ground_bounce.is_some()
                    && material.bounce_settings.x.to_bits() != values.bounce_light.to_bits())
                .then_some(id)
            })
            .collect();
        for id in changed {
            if let Some(mut material) = materials.get_mut(id) {
                material.bounce_settings.x = values.bounce_light;
            }
        }
    }
    let mut view = world.entity_mut(camera);
    view.insert((
        Hdr,
        crate::source_air_haze::SourceAirHaze::default(),
        crate::atmospheric_haze::AirHazeSettings::new(values.haze),
        crate::ambient_occlusion::AmbientOcclusionSettings::new(values.ambient_occlusion),
        crate::lens_flare::LensFlare::new(values.lens_flare),
    ));
    if values.temporal_aa > 0.5 {
        view.insert(TemporalAntiAliasing::default());
        view.remove::<Fxaa>();
    } else {
        view.remove::<TemporalAntiAliasing>();
        if values.fxaa > 0.5 {
            view.insert(Fxaa::default());
        } else {
            view.remove::<Fxaa>();
        }
    }
    if values.motion_blur_degrees > 0.0 {
        view.insert((
            MotionBlur {
                shutter_angle: values.motion_blur_degrees / 360.0,
                samples: 3,
            },
            MotionVectorPrepass,
        ));
    } else {
        view.remove::<MotionBlur>();
    }
    if pipeline == Pipeline::Reference && quality.tonemapping_enabled {
        let mut grading = ColorGrading::default();
        grading.global.exposure = values.exposure_ev;
        grading.global.post_saturation = values.saturation;
        grading.midtones.contrast = values.contrast;
        view.insert((
            Tonemapping::AcesFitted,
            grading,
            ContrastAdaptiveSharpening {
                enabled: values.sharpen > 0.0,
                sharpening_strength: values.sharpen,
                denoise: false,
            },
            Vignette {
                intensity: values.vignette,
                ..default()
            },
        ));
        view.remove::<crate::source_final_post::Controls>();
        if values.bloom > 0.0 {
            view.insert(Bloom {
                intensity: values.bloom,
                ..Bloom::NATURAL
            });
        } else {
            view.remove::<Bloom>();
        }
        if values.film_grain > 0.0 {
            view.insert(crate::film_grain::FilmGrain::new(values.film_grain));
        } else {
            view.remove::<crate::film_grain::FilmGrain>();
        }
        if let Some(exposure) = exposure {
            view.insert(AutoExposure {
                filter: 0.0..=1.0,
                speed_brighten: 1.6,
                speed_darken: 1.1,
                compensation_curve: exposure.curve,
                metering_mask: exposure.mask,
                ..default()
            });
        } else {
            view.remove::<AutoExposure>();
        }
    } else {
        view.remove::<(
            AutoExposure,
            Bloom,
            Vignette,
            crate::film_grain::FilmGrain,
            ContrastAdaptiveSharpening,
        )>();
        view.insert((Tonemapping::None, Msaa::Off));
        if let Some(controls) = final_controls {
            view.insert((ColorGrading::default(), controls));
        } else {
            view.remove::<(ColorGrading, crate::source_final_post::Controls)>();
        }
    }
    if values.temporal_aa <= 0.5 {
        remove_if_not_required::<TemporalJitter>(world, camera);
        remove_if_not_required::<MipBias>(world, camera);
        if values.motion_blur_degrees <= 0.0 {
            remove_if_not_required::<DepthPrepass>(world, camera);
            remove_if_not_required::<MotionVectorPrepass>(world, camera);
        }
    }
    Ok(())
}

/// A camera can carry independent effects that require these same components.
/// Removing one effect must not remove another effect's declared prerequisites.
fn remove_if_not_required<C: Component>(world: &mut World, camera: Entity) {
    let Some(required_id) = world.component_id::<C>() else {
        return;
    };
    let required = world
        .entity(camera)
        .archetype()
        .components()
        .iter()
        .any(|id| {
            world.components().get_info(*id).is_some_and(|info| {
                info.required_components()
                    .iter_ids()
                    .any(|required| required == required_id)
            })
        });
    if !required {
        world.entity_mut(camera).remove::<C>();
    }
}

/// Check the current CPU camera configuration without claiming GPU execution.
#[must_use]
#[allow(clippy::too_many_lines)]
pub fn reference_matches(
    world: &World,
    camera: Entity,
    values: Settings,
    quality: PostProcessQualitySettings,
) -> bool {
    if !values.valid() || validate_quality(quality).is_err() {
        return false;
    }
    let values = values.gated(quality);
    let temporal = values.temporal_aa > 0.5;
    let fxaa = !temporal && values.fxaa > 0.5;
    let same = |left: f32, right: f32| left.to_bits() == right.to_bits();
    let optional_controls_match = world
        .get_resource::<crate::source_underwater::SourceUnderwaterControl>()
        .is_none_or(|control| same(control.shafts_strength, values.underwater_shafts))
        && world
            .get_resource::<crate::source_underwater_lighting::Control>()
            .is_none_or(|control| {
                control.enabled == (values.underwater_lighting > 0.5)
                    && same(control.caustics_strength, values.underwater_caustics)
                    && control.shadow_refraction == (values.underwater_shadow_refraction > 0.5)
            })
        && world
            .get_resource::<crate::source_refraction_capture::Control>()
            .is_none_or(|control| control.enabled == (values.water_refraction_capture > 0.5));
    let common = optional_controls_match
        && world.get::<Hdr>(camera).is_some()
        && world
            .get::<crate::source_air_haze::SourceAirHaze>(camera)
            .is_some()
        && world
            .get::<crate::atmospheric_haze::AirHazeSettings>(camera)
            .is_some_and(|setting| setting.enabled && same(setting.density(), values.haze))
        && world
            .get::<crate::ambient_occlusion::AmbientOcclusionSettings>(camera)
            .is_some_and(|setting| same(setting.strength(), values.ambient_occlusion))
        && world
            .get::<crate::lens_flare::LensFlare>(camera)
            .is_some_and(|setting| same(setting.strength(), values.lens_flare))
        && world.get::<TemporalAntiAliasing>(camera).is_some() == temporal
        && world
            .get::<Fxaa>(camera)
            .is_some_and(|setting| setting.enabled)
            == fxaa
        && world
            .get::<crate::source_final_post::Controls>(camera)
            .is_none();
    let motion = world.get::<MotionBlur>(camera);
    let motion_matches = if values.motion_blur_degrees > 0.0 {
        motion.is_some_and(|setting| {
            same(setting.shutter_angle, values.motion_blur_degrees / 360.0) && setting.samples == 3
        })
    } else {
        motion.is_none()
    };
    if !common || !motion_matches {
        return false;
    }
    if !quality.tonemapping_enabled {
        return world.get::<Tonemapping>(camera) == Some(&Tonemapping::None)
            && world.get::<ColorGrading>(camera).is_none()
            && world.get::<AutoExposure>(camera).is_none()
            && world.get::<Bloom>(camera).is_none()
            && world.get::<Vignette>(camera).is_none()
            && world.get::<crate::film_grain::FilmGrain>(camera).is_none()
            && world.get::<ContrastAdaptiveSharpening>(camera).is_none();
    }
    let grading = world.get::<ColorGrading>(camera);
    let bloom = world.get::<Bloom>(camera);
    let grain = world.get::<crate::film_grain::FilmGrain>(camera);
    let exposure = world.get::<AutoExposure>(camera);
    let exposure_matches = if values.auto_exposure > 0.5 {
        exposure
            .zip(world.get_resource::<ExposureAssets>())
            .is_some_and(|(actual, assets)| {
                actual.compensation_curve == assets.curve
                    && actual.metering_mask == assets.mask
                    && same(actual.speed_brighten, 1.6)
                    && same(actual.speed_darken, 1.1)
            })
    } else {
        exposure.is_none()
    };
    world.get::<Tonemapping>(camera) == Some(&Tonemapping::AcesFitted)
        && grading.is_some_and(|actual| {
            same(actual.global.exposure, values.exposure_ev)
                && same(actual.global.post_saturation, values.saturation)
                && same(actual.midtones.contrast, values.contrast)
        })
        && world
            .get::<ContrastAdaptiveSharpening>(camera)
            .is_some_and(|actual| {
                actual.enabled == (values.sharpen > 0.0)
                    && same(actual.sharpening_strength, values.sharpen)
                    && !actual.denoise
            })
        && world
            .get::<Vignette>(camera)
            .is_some_and(|actual| same(actual.intensity, values.vignette))
        && if values.bloom > 0.0 {
            bloom.is_some_and(|actual| same(actual.intensity, values.bloom))
        } else {
            bloom.is_none()
        }
        && if values.film_grain > 0.0 {
            grain.is_some_and(|actual| same(actual.intensity.x, values.film_grain))
        } else {
            grain.is_none()
        }
        && exposure_matches
}

#[cfg(test)]
mod tests {
    use super::*;

    fn quality() -> PostProcessQualitySettings {
        PostProcessQualitySettings {
            bloom_enabled: true,
            color_grading_enabled: true,
            tonemapping_enabled: true,
        }
    }

    #[derive(Component)]
    #[require(DepthPrepass, MotionVectorPrepass, TemporalJitter, MipBias)]
    struct OtherCameraEffect;

    #[test]
    fn disabled_motion_prepass_retires_render_cache_and_allows_reactivation() {
        use bevy::render::render_resource::CachedRenderPipelineId;

        let mut app = App::new();
        app.insert_sub_app(RenderApp, bevy::app::SubApp::new());
        install_motion_vector_cleanup(&mut app);
        install_motion_vector_cleanup(&mut app);
        let render = app.sub_app_mut(RenderApp);
        let camera = render
            .world_mut()
            .spawn((
                MotionVectorPrepass,
                BackgroundMotionVectorsPipelineId(CachedRenderPipelineId::new(7)),
            ))
            .id();
        render.world_mut().run_schedule(Render);
        assert_eq!(
            render
                .world()
                .get::<BackgroundMotionVectorsPipelineId>(camera)
                .unwrap()
                .0
                .id(),
            7
        );
        render
            .world_mut()
            .entity_mut(camera)
            .remove::<MotionVectorPrepass>();
        render.world_mut().run_schedule(Render);
        assert!(
            render
                .world()
                .get::<BackgroundMotionVectorsPipelineId>(camera)
                .is_none()
        );
        render.world_mut().entity_mut(camera).insert((
            MotionVectorPrepass,
            BackgroundMotionVectorsPipelineId(CachedRenderPipelineId::new(8)),
        ));
        render.world_mut().run_schedule(Render);
        assert_eq!(
            render
                .world()
                .get::<BackgroundMotionVectorsPipelineId>(camera)
                .unwrap()
                .0
                .id(),
            8
        );
        render
            .world_mut()
            .entity_mut(camera)
            .insert(NoBackgroundMotionVectors);
        render.world_mut().run_schedule(Render);
        assert!(
            render
                .world()
                .get::<BackgroundMotionVectorsPipelineId>(camera)
                .is_none()
        );
        assert!(render.world().get::<MotionVectorPrepass>(camera).is_some());
    }

    #[test]
    fn late_source_material_uses_the_active_bounce_setting() {
        use crate::source_surface::SourceSurfaceMaterial;

        let mut app = App::new();
        app.init_resource::<Assets<SourceSurfaceMaterial>>()
            .add_message::<AssetEvent<SourceSurfaceMaterial>>()
            .insert_resource(ActiveBounce(0.35))
            .add_systems(Update, sync_bounce_materials);
        let mut material = crate::source_surface::tests::lighting();
        material.ground_bounce = Some(Handle::default());
        let handle = app
            .world_mut()
            .resource_mut::<Assets<SourceSurfaceMaterial>>()
            .add(material);
        app.world_mut()
            .resource_mut::<Messages<AssetEvent<SourceSurfaceMaterial>>>()
            .write(AssetEvent::Added { id: handle.id() });
        app.update();
        assert_eq!(
            app.world()
                .resource::<Assets<SourceSurfaceMaterial>>()
                .get(&handle)
                .unwrap()
                .bounce_settings
                .x
                .to_bits(),
            0.35_f32.to_bits()
        );
    }

    #[test]
    fn switching_graphics_profiles_keeps_other_effects_camera_requirements() {
        let mut world = World::new();
        world.insert_resource(ExposureAssets {
            curve: Handle::default(),
            mask: Handle::default(),
        });
        let camera = world.spawn_empty().id();
        let temporal = Settings {
            temporal_aa: 1.0,
            fxaa: 0.0,
            ..default()
        };
        apply(&mut world, camera, temporal, quality(), Pipeline::Reference).unwrap();
        world.entity_mut(camera).insert(OtherCameraEffect);
        let standard = Settings::default();
        apply(&mut world, camera, standard, quality(), Pipeline::Reference).unwrap();
        assert!(world.get::<DepthPrepass>(camera).is_some());
        assert!(world.get::<MotionVectorPrepass>(camera).is_some());
        assert!(world.get::<TemporalJitter>(camera).is_some());
        assert!(world.get::<MipBias>(camera).is_some());
        world.entity_mut(camera).remove::<OtherCameraEffect>();
        apply(&mut world, camera, standard, quality(), Pipeline::Reference).unwrap();
        assert!(world.get::<DepthPrepass>(camera).is_none());
        assert!(world.get::<MotionVectorPrepass>(camera).is_none());
        assert!(world.get::<TemporalJitter>(camera).is_none());
        assert!(world.get::<MipBias>(camera).is_none());

        let contact = world.spawn(bevy::pbr::ContactShadows::default()).id();
        apply(
            &mut world,
            contact,
            standard,
            quality(),
            Pipeline::Reference,
        )
        .unwrap();
        assert!(world.get::<DepthPrepass>(contact).is_some());
    }

    #[test]
    fn shared_reference_settings_apply_and_detect_removed_or_changed_effects() {
        let mut world = World::new();
        world.insert_resource(ExposureAssets {
            curve: Handle::default(),
            mask: Handle::default(),
        });
        world.insert_resource(crate::source_refraction_capture::Control { enabled: false });
        let camera = world.spawn_empty().id();
        let values = Settings::default();
        apply(&mut world, camera, values, quality(), Pipeline::Reference).unwrap();
        assert!(reference_matches(&world, camera, values, quality()));
        assert!(
            world
                .resource::<crate::source_refraction_capture::Control>()
                .enabled
        );
        world
            .get_mut::<crate::atmospheric_haze::AirHazeSettings>(camera)
            .unwrap()
            .enabled = false;
        assert!(!reference_matches(&world, camera, values, quality()));
        apply(&mut world, camera, values, quality(), Pipeline::Reference).unwrap();
        world
            .get_mut::<crate::ambient_occlusion::AmbientOcclusionSettings>(camera)
            .unwrap()
            .set_strength(0.0);
        assert!(!reference_matches(&world, camera, values, quality()));
        apply(&mut world, camera, values, quality(), Pipeline::Reference).unwrap();
        world
            .entity_mut(camera)
            .remove::<crate::lens_flare::LensFlare>();
        assert!(!reference_matches(&world, camera, values, quality()));
        let reduced = PostProcessQualitySettings {
            bloom_enabled: false,
            color_grading_enabled: false,
            tonemapping_enabled: true,
        };
        apply(&mut world, camera, values, reduced, Pipeline::Reference).unwrap();
        assert!(reference_matches(&world, camera, values, reduced));
        assert!(world.get::<Bloom>(camera).is_none());
        assert!(world.get::<crate::film_grain::FilmGrain>(camera).is_none());
    }

    #[test]
    fn source_final_defaults_preserve_the_reference_bits_and_invalid_inputs_do_not_mutate() {
        let values = Settings::default();
        let actual = values.final_controls(quality()).unwrap().unwrap();
        let expected = crate::source_final_post::Controls {
            grade: Vec4::new(0.28, 1.06, 1.04, 0.02),
            effects: Vec4::new(0.012, 0.05, 0.55, 1.0),
            timing: Vec4::new(0.0, 0.0, 0.2, 0.0),
            frame: UVec4::ZERO,
        };
        assert_eq!(actual.settings_bits(), expected.settings_bits());
        let mut world = World::new();
        world.insert_resource(crate::source_refraction_capture::Control { enabled: false });
        let camera = world.spawn_empty().id();
        let invalid = Settings {
            haze: f32::NAN,
            ..values
        };
        assert!(apply(&mut world, camera, invalid, quality(), Pipeline::Reference).is_err());
        assert!(world.get::<Hdr>(camera).is_none());
        assert!(
            !world
                .resource::<crate::source_refraction_capture::Control>()
                .enabled
        );
        assert!(apply(&mut world, camera, values, quality(), Pipeline::Reference).is_err());
        assert!(world.get::<Hdr>(camera).is_none());
        apply(&mut world, camera, values, quality(), Pipeline::SourceFinal).unwrap();
        assert_eq!(
            world
                .get::<crate::source_final_post::Controls>(camera)
                .unwrap()
                .settings_bits(),
            expected.settings_bits()
        );
    }
}
