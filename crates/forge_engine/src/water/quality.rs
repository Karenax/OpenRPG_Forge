use super::{
    PreparedWaterCamera, VisualValleyWaterMaterial, WaterMaterialProjection, WaterSurfaceMaterial,
};
use bevy::{anti_alias::taa::TemporalAntiAliasing, pbr::ScreenSpaceReflections, prelude::*};
use forge_world::quality_application::{QualityApplicationGuard, QualityApplicationIdentity};
use serde::Serialize;

#[cfg(test)]
mod tests;
use forge_project::{
    QualityWaterReflection, QualityWaterRefraction, TerrainDocument, VisualQualityTier,
    VisualValleyQualityProfileDocument, VisualValleyQualityProfileError, WaterQualitySettings,
};

#[derive(Resource)]
pub struct WaterQualityRequest {
    document: VisualValleyQualityProfileDocument,
    hash: String,
    tier: VisualQualityTier,
    surface_tint_preview: Option<bool>,
    reflections_disabled_preview: bool,
    screen_reflections_disabled_preview: bool,
    screen_refraction_disabled_preview: bool,
    effective: Option<(
        QualityApplicationIdentity,
        forge_project::effective_quality::EffectiveWaterSettings,
    )>,
}

impl WaterQualityRequest {
    #[cfg(any(feature = "physics-rapier", test))]
    pub(crate) fn clear_effective(&mut self) {
        self.effective = None;
    }

    pub(crate) fn effective_identity(&self) -> Option<QualityApplicationIdentity> {
        self.effective.as_ref().map(|(identity, _)| *identity)
    }
    pub fn new(
        terrain: &TerrainDocument,
        document: VisualValleyQualityProfileDocument,
        tier: VisualQualityTier,
    ) -> Result<Self, VisualValleyQualityProfileError> {
        document.validate_identity(terrain.project_id, terrain.area_id, terrain.revision)?;
        let hash = document.content_hash()?.to_string();
        Ok(Self {
            document,
            hash,
            tier,
            surface_tint_preview: None,
            reflections_disabled_preview: false,
            screen_reflections_disabled_preview: false,
            screen_refraction_disabled_preview: false,
            effective: None,
        })
    }

    pub fn request(&mut self, tier: VisualQualityTier) {
        self.tier = tier;
        self.effective = None;
    }

    /// Transient installation. The coordinator owns fresh snapshots and later invalidation.
    /// Manual previews remain higher priority; receipt is not a projection/GPU acknowledgment.
    pub fn request_effective(
        &mut self,
        guard: &QualityApplicationGuard,
        now: std::time::Duration,
        current: QualityApplicationIdentity,
        sources: forge_project::effective_quality::QualitySourceBindings,
    ) -> bool {
        self.effective = None;
        let Some(plan) = guard
            .installation_plan(now, current, sources)
            .filter(|plan| {
                plan.requested_policy_hash.to_string() == self.hash
                    && plan.requested_tier == self.tier
            })
        else {
            return false;
        };
        self.effective = Some((current, plan.effective.water.clone()));
        true
    }

    fn renderer_settings(&self) -> forge_project::effective_quality::EffectiveWaterSettings {
        let requested = self.settings();
        let mut settings = self.effective.as_ref().map_or_else(
            || forge_project::effective_quality::EffectiveWaterSettings {
                reflection: requested.reflection,
                reflection_steps: requested.reflection_steps,
                reflection_max_distance_m: Some(requested.reflection_max_distance_m),
                refraction: requested.refraction,
                refraction_steps: requested.refraction_steps,
                refraction_max_depth_m: requested.refraction_max_depth_m,
            },
            |(_, settings)| settings.clone(),
        );
        if self.surface_tint_preview.is_some() {
            settings.refraction = requested.refraction;
            settings.refraction_steps = requested.refraction_steps;
            settings.refraction_max_depth_m = requested.refraction_max_depth_m;
        }
        if self.reflections_disabled_preview || self.screen_reflections_disabled_preview {
            settings.reflection = requested.reflection;
            settings.reflection_steps = requested.reflection_steps;
            settings.reflection_max_distance_m = Some(requested.reflection_max_distance_m);
        }
        if self.screen_refraction_disabled_preview
            && settings.refraction == QualityWaterRefraction::ScreenSpace
        {
            settings.refraction = QualityWaterRefraction::SurfaceTint;
            settings.refraction_steps = 0;
            settings.refraction_max_depth_m = 0.0;
        }
        settings
    }

    fn settings(&self) -> WaterQualitySettings {
        let mut settings = self.document.preset(self.tier).water;
        if let Some(enabled) = self.surface_tint_preview {
            settings.refraction = if enabled {
                QualityWaterRefraction::SurfaceTint
            } else {
                QualityWaterRefraction::Disabled
            };
            settings.refraction_steps = 0;
            settings.refraction_max_depth_m = 0.0;
        }
        if self.screen_reflections_disabled_preview
            && settings.reflection == QualityWaterReflection::ScreenSpace
        {
            settings.reflection = QualityWaterReflection::Environment;
            settings.reflection_steps = 0;
            settings.reflection_max_distance_m = 0.0;
        }
        if self.reflections_disabled_preview {
            settings.reflection = QualityWaterReflection::Disabled;
            settings.reflection_steps = 0;
            settings.reflection_max_distance_m = 0.0;
        }
        settings
    }

    /// Transient renderer preview; None follows the selected preset without altering its document.
    pub const fn preview_surface_tint(&mut self, enabled: Option<bool>) {
        self.surface_tint_preview = enabled;
    }

    /// Temporarily suppress reflections; false resumes the selected authored quality preset.
    pub const fn preview_reflections_disabled(&mut self, disabled: bool) {
        self.reflections_disabled_preview = disabled;
    }

    /// Isolate SSR without suppressing environmental/direct reflection or changing sources.
    pub const fn preview_screen_reflections_disabled(&mut self, disabled: bool) {
        self.screen_reflections_disabled_preview = disabled;
    }

    /// Suppress only the refracted ray march. Keep the transmission fallback,
    /// reflection controls and authored policy; false follows the effective preset.
    pub const fn preview_screen_refraction_disabled(&mut self, disabled: bool) {
        self.screen_refraction_disabled_preview = disabled;
    }
}

/// Per-field evidence: unsupported optics must never be acknowledged as a complete quality tier.
#[derive(Clone, Debug, PartialEq, Serialize)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "independent evidence for camera SSR, tint, reflectance and complete optics, not exclusive states"
)]
pub struct WaterQualityObservation {
    pub surface_tint_preview: Option<bool>,
    pub reflections_disabled_preview: bool,
    pub screen_reflections_disabled_preview: bool,
    pub policy_hash: String,
    pub requested_tier: VisualQualityTier,
    pub requested: WaterQualitySettings,
    pub renderer_requested: forge_project::effective_quality::EffectiveWaterSettings,
    /// Receipt only; manual overrides and observed components must be checked separately.
    pub effective_request: Option<QualityApplicationIdentity>,
    pub ssr_budget_projected: bool,
    pub ssr_linear_steps: Option<u32>,
    pub reflection_path_status: &'static str,
    pub reflection_distance_status: &'static str,
    pub refraction_status: &'static str,
    pub surface_tint_strength: Option<f32>,
    pub surface_tint_projected: bool,
    pub material_reflectance: Option<f32>,
    pub material_reflectance_projected: bool,
    /// Main-world image availability only; not render-world upload or visible reflections.
    pub environment_textures_available: bool,
    /// Active camera IBL intensity, not the authored per-material reflection gain.
    pub environment_light_intensity: Option<f32>,
    pub full_preset_applied: bool,
    pub status: &'static str,
}

/// Material-uniform evidence for the opt-in forward trace, separate from camera SSR.
#[derive(Clone, Debug, PartialEq, Serialize)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "Independent observed optics prerequisites, not alternative states"
)]
pub struct ForwardReflectionObservation {
    pub manual_optics_override: bool,
    pub environment_textures_available: bool,
    pub environment_light_intensity: Option<f32>,
    pub full_target_viewport: bool,
    pub surface_tint_projected: bool,
    pub policy_hash: Option<String>,
    pub requested_tier: Option<VisualQualityTier>,
    pub effective_request: Option<QualityApplicationIdentity>,
    pub screen_refraction_disabled_preview: bool,
    pub requested: Option<forge_project::effective_quality::EffectiveWaterSettings>,
    pub linear_steps: Option<f32>,
    pub max_distance_m: Option<f32>,
    pub refraction_linear_steps: Option<f32>,
    pub refraction_max_path_m: Option<f32>,
    pub material_reflectance: Option<f32>,
    pub projection_matches: bool,
    pub status: &'static str,
}

impl ForwardReflectionObservation {
    pub(super) fn observe_view(
        &mut self,
        camera: Option<&Camera>,
        map: Option<&bevy::light::EnvironmentMapLight>,
        images: Option<&Assets<Image>>,
    ) {
        self.environment_textures_available = environment_images_available(map, images);
        self.environment_light_intensity = map.map(|map| map.intensity);
        self.full_target_viewport = camera.is_some_and(|camera| {
            camera.physical_target_size().is_some_and(|size| {
                size.min_element() > 0
                    && camera.physical_viewport_size() == Some(size)
                    && camera
                        .viewport
                        .as_ref()
                        .is_none_or(|v| v.physical_position == UVec2::ZERO)
            })
        });
    }
    #[cfg(any(feature = "physics-rapier", test))]
    pub(crate) fn matches_application(&self, guard: &QualityApplicationGuard) -> bool {
        self.effective_request == Some(guard.identity())
            && self.matches_policy(
                Some(&guard.plan().requested_policy_hash.to_string()),
                guard.plan().requested_tier,
            )
            && self.requested.as_ref() == Some(&guard.plan().effective.water)
            && self.budgets_match()
    }

    /// Exact renderer budgets, independent of an application receipt. This can
    /// establish the bounded adapter contract before installing its first plan.
    #[cfg(any(feature = "physics-rapier", test))]
    pub(crate) fn budgets_match(&self) -> bool {
        self.projection_matches
            && self.requested.as_ref().is_some_and(|settings| {
                // The forward path cannot trace an unbounded/unspecified distance.
                // A matching clamped uniform is not proof of the requested budget.
                (settings.reflection != QualityWaterReflection::ScreenSpace
                    || (settings.reflection_steps >= 2
                        && self.linear_steps == Some(f32::from(settings.reflection_steps))
                        && settings.reflection_max_distance_m.is_some_and(|distance| {
                            distance.is_finite()
                                && distance > 0.0
                                && self.max_distance_m == Some(distance)
                        })))
                    && (settings.refraction != QualityWaterRefraction::ScreenSpace
                        || (settings.refraction_steps >= 2
                            && self.refraction_linear_steps
                                == Some(f32::from(settings.refraction_steps))
                            && settings.refraction_max_depth_m.is_finite()
                            && settings.refraction_max_depth_m > 0.0
                            && self.refraction_max_path_m == Some(settings.refraction_max_depth_m)))
            })
    }

    /// CPU projection only. This does not establish optical support or GPU readiness.
    #[cfg(any(feature = "physics-rapier", test))]
    pub(crate) fn matches_policy(&self, policy: Option<&str>, tier: VisualQualityTier) -> bool {
        policy.is_some_and(|hash| !hash.is_empty() && self.policy_hash.as_deref() == Some(hash))
            && self.requested_tier == Some(tier)
            && self.requested.is_some()
            && self.projection_matches
    }
}

fn forward_refraction_projection(
    request: Option<&WaterQualityRequest>,
    unique_active_camera: bool,
) -> Vec4 {
    let Some(request) = request.filter(|_| unique_active_camera) else {
        return Vec4::ZERO;
    };
    let settings = request.renderer_settings();
    if settings.refraction != QualityWaterRefraction::ScreenSpace
        || settings.refraction_steps < 2
        || !settings.refraction_max_depth_m.is_finite()
        || settings.refraction_max_depth_m <= 0.0
    {
        return Vec4::ZERO;
    }
    Vec4::new(
        f32::from(settings.refraction_steps.min(64)),
        settings.refraction_max_depth_m,
        0.2,
        1.0,
    )
}

pub(super) fn forward_reflection_projection(
    request: Option<&WaterQualityRequest>,
    unique_active_camera: bool,
    authored_reflectance: f32,
) -> (Vec4, f32) {
    if !unique_active_camera {
        return (Vec4::ZERO, authored_reflectance);
    }
    let Some(request) = request else {
        return (Vec4::new(32.0, 64.0, 0.2, 1.0), authored_reflectance);
    };
    let settings = request.renderer_settings();
    let reflectance = effective_reflectance(request, authored_reflectance);
    let distance = settings.reflection_max_distance_m.unwrap_or(0.0);
    if settings.reflection != QualityWaterReflection::ScreenSpace
        || settings.reflection_steps < 2
        || !distance.is_finite()
        || distance <= 0.0
    {
        return (Vec4::ZERO, reflectance);
    }
    (
        Vec4::new(
            f32::from(settings.reflection_steps.min(64)),
            distance.min(128.0),
            0.2,
            1.0,
        ),
        reflectance,
    )
}

pub(super) fn observe_forward_reflection(
    request: Option<&WaterQualityRequest>,
    unique_active_camera: bool,
    authored_reflectance: f32,
    authored_tint: f32,
    material: Option<&VisualValleyWaterMaterial>,
) -> ForwardReflectionObservation {
    let (expected, reflectance) =
        forward_reflection_projection(request, unique_active_camera, authored_reflectance);
    ForwardReflectionObservation {
        manual_optics_override: request.is_some_and(|r| {
            r.surface_tint_preview.is_some()
                || r.reflections_disabled_preview
                || r.screen_reflections_disabled_preview
                || r.screen_refraction_disabled_preview
        }),
        environment_textures_available: false,
        environment_light_intensity: None,
        full_target_viewport: false,
        surface_tint_projected: request.is_some_and(|r| {
            material.is_some_and(|m| {
                m.extension.shallow_color_surface_tint.w.to_bits()
                    == effective_tint(r, authored_tint).to_bits()
            })
        }),
        policy_hash: request.map(|r| r.hash.clone()),
        requested_tier: request.map(|r| r.tier),
        effective_request: request.and_then(WaterQualityRequest::effective_identity),
        screen_refraction_disabled_preview: request
            .is_some_and(|request| request.screen_refraction_disabled_preview),
        requested: request.map(WaterQualityRequest::renderer_settings),
        linear_steps: material.map(|m| m.extension.reflection_preview.x),
        max_distance_m: material.map(|m| m.extension.reflection_preview.y),
        refraction_linear_steps: material.map(|m| m.extension.refraction_preview.x),
        refraction_max_path_m: material.map(|m| m.extension.refraction_preview.y),
        material_reflectance: material.map(|m| m.extension.optical_parameters.w),
        projection_matches: unique_active_camera
            && material.is_some_and(|m| {
                m.extension.reflection_preview == expected
                    && m.extension.refraction_preview
                        == forward_refraction_projection(request, unique_active_camera)
                    && m.extension.optical_parameters.w.to_bits() == reflectance.to_bits()
            }),
        status: "experimental_forward_uniforms_only_not_gpu_or_complete_quality",
    }
}

pub(super) fn expected_extension(
    projection: &super::VisualValleyWaterExtension,
    runtime: &super::WaterRuntimeState,
    request: Option<&WaterQualityRequest>,
    unique_active_camera: bool,
    optical_preview: bool,
) -> super::VisualValleyWaterExtension {
    let mut expected = super::projected_water_extension(projection, runtime);
    if projection.reflection_preview.w > 0.0 {
        let (trace, reflectance) = forward_reflection_projection(
            request,
            unique_active_camera,
            projection.optical_parameters.w,
        );
        expected.reflection_preview = trace;
        expected.refraction_preview = forward_refraction_projection(request, unique_active_camera);
        expected.optical_parameters.w = reflectance;
    }
    if (!optical_preview || projection.reflection_preview.w > 0.0)
        && unique_active_camera
        && let Some(request) = request
    {
        expected.shallow_color_surface_tint.w =
            effective_tint(request, expected.shallow_color_surface_tint.w);
        expected.optical_parameters.w =
            effective_reflectance(request, expected.optical_parameters.w);
    }
    expected
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::type_complexity,
    clippy::too_many_arguments,
    reason = "explicit Bevy camera query preserves shared TAA ownership"
)]
pub(super) fn apply_water_quality(
    preview: Option<Res<super::WaterDepthOpticsPreview>>,
    mut commands: Commands,
    request: Option<Res<WaterQualityRequest>>,
    prepared: Res<PreparedWaterCamera>,
    projection: Res<WaterMaterialProjection>,
    material: Option<Res<WaterSurfaceMaterial>>,
    mut materials: ResMut<Assets<VisualValleyWaterMaterial>>,
    mut announced: Local<Option<VisualQualityTier>>,
    cameras: Query<
        (
            Entity,
            &Camera,
            Option<&ScreenSpaceReflections>,
            Option<&TemporalAntiAliasing>,
        ),
        With<Camera3d>,
    >,
) {
    // The candidate is deliberately not a qualified authored quality path.
    // Do not re-enable deferred SSR or silently change the comparison material.
    if preview.is_some() {
        if projection.0.reflection_preview.w > 0.0 {
            let unique = cameras
                .iter()
                .filter(|(_, camera, _, _)| camera.is_active)
                .count()
                == 1;
            let (trace, reflectance) = forward_reflection_projection(
                request.as_deref(),
                unique,
                projection.0.optical_parameters.w,
            );
            if let Some(mut material) = material
                .as_deref()
                .and_then(|handle| materials.get_mut(&handle.0))
            {
                material.extension.reflection_preview = trace;
                material.extension.refraction_preview =
                    forward_refraction_projection(request.as_deref(), unique);
                material.extension.optical_parameters.w = reflectance;
                if let Some(request) = request.as_deref() {
                    material.extension.shallow_color_surface_tint.w =
                        effective_tint(request, projection.0.shallow_color_surface_tint.w);
                }
            }
        }
        return;
    }
    let Some(request) = request else {
        return;
    };
    let mut active = cameras.iter().filter(|(_, camera, _, _)| camera.is_active);
    let Some((entity, _, current, taa)) = active.next() else {
        return;
    };
    if active.next().is_some() {
        return;
    }
    if let Some(mut material) = material
        .as_deref()
        .and_then(|handle| materials.get_mut(&handle.0))
    {
        material.extension.shallow_color_surface_tint.w =
            effective_tint(&request, projection.0.shallow_color_surface_tint.w);
        material.extension.optical_parameters.w =
            effective_reflectance(&request, projection.0.optical_parameters.w);
    }
    if *announced != Some(request.tier) {
        warn!(target: "forge::water", schema = "forge.runtime-event", schema_version = 1_u64,
            event_kind = "water_quality_partial", tier = ?request.tier, policy_hash = %request.hash,
            reflection = ?request.renderer_settings().reflection, refraction = ?request.renderer_settings().refraction,
            "supported SSR budgets and surface tint controls are applied; reflection distance, screen-space refraction and complete environment reflection qualification remain unavailable");
        *announced = Some(request.tier);
    }
    match request.renderer_settings().reflection {
        QualityWaterReflection::ScreenSpace => {
            // Preserve the authored thickness and Bevy traversal parameters; change only the budget.
            let Some(mut desired) = prepared.ssr.clone() else {
                return;
            };
            desired.linear_steps = u32::from(request.renderer_settings().reflection_steps);
            if current.is_none_or(|current| current.linear_steps != desired.linear_steps) {
                commands.entity(entity).insert(desired);
            }
            if taa.is_none() {
                commands
                    .entity(entity)
                    .insert(TemporalAntiAliasing::default());
            }
        }
        QualityWaterReflection::Environment | QualityWaterReflection::Disabled => {
            if current.is_some() {
                commands.entity(entity).remove::<ScreenSpaceReflections>();
            }
            // Keep TAA and prepasses: they may be shared with other rendering effects.
        }
    }
}

pub(super) fn observe(
    request: &WaterQualityRequest,
    prepared: &PreparedWaterCamera,
    camera: Option<(&Camera, Option<&ScreenSpaceReflections>)>,
    authored: &super::VisualValleyWaterExtension,
    material: Option<&VisualValleyWaterMaterial>,
    environment_textures_available: bool,
    environment_light_intensity: Option<f32>,
) -> WaterQualityObservation {
    let tint = material.map(|material| {
        (
            authored.shallow_color_surface_tint.w,
            material.extension.shallow_color_surface_tint.w,
        )
    });
    let reflectance = material.map(|material| {
        (
            authored.optical_parameters.w,
            material.extension.optical_parameters.w,
        )
    });
    let settings = request.renderer_settings();
    let actual = camera.and_then(|(_, ssr)| ssr);
    let ssr_budget_projected = camera.is_some_and(|(camera, _)| camera.is_active)
        && match settings.reflection {
            QualityWaterReflection::Environment | QualityWaterReflection::Disabled => {
                actual.is_none()
            }
            QualityWaterReflection::ScreenSpace => {
                prepared.ssr.is_some()
                    && actual
                        .is_some_and(|ssr| ssr.linear_steps == u32::from(settings.reflection_steps))
            }
        };
    WaterQualityObservation {
        surface_tint_preview: request.surface_tint_preview,
        reflections_disabled_preview: request.reflections_disabled_preview,
        screen_reflections_disabled_preview: request.screen_reflections_disabled_preview,
        policy_hash: request.hash.clone(),
        requested_tier: request.tier,
        requested: request.settings(),
        renderer_requested: settings.clone(),
        effective_request: request.effective.as_ref().map(|(identity, _)| *identity),
        ssr_budget_projected,
        ssr_linear_steps: actual.map(|ssr| ssr.linear_steps),
        reflection_path_status: match settings.reflection {
            QualityWaterReflection::ScreenSpace => "screen_space_component_budget_only",
            QualityWaterReflection::Environment => {
                "ssr_disabled_environment_reflections_not_qualified"
            }
            QualityWaterReflection::Disabled => {
                "ssr_disabled_zero_reflectance_cpu_observation_required"
            }
        },
        reflection_distance_status: if settings.reflection == QualityWaterReflection::ScreenSpace {
            "unsupported_by_bevy_ssr_component"
        } else {
            "no_ray_march_distance_requested"
        },
        refraction_status: match settings.refraction {
            QualityWaterRefraction::ScreenSpace => {
                "requested_screen_space_unavailable_authored_fallback_retained"
            }
            QualityWaterRefraction::SurfaceTint => {
                "surface_tint_requested_cpu_material_observation_required"
            }
            QualityWaterRefraction::Disabled => {
                "surface_tint_disable_requested_cpu_material_observation_required"
            }
        },
        surface_tint_strength: tint.map(|(_, actual)| actual),
        surface_tint_projected: camera.is_some_and(|(camera, _)| camera.is_active)
            && tint.is_some_and(|(authored, actual)| {
                actual.to_bits() == effective_tint(request, authored).to_bits()
            }),
        material_reflectance: reflectance.map(|(_, actual)| actual),
        environment_textures_available,
        environment_light_intensity,
        material_reflectance_projected: camera.is_some_and(|(camera, _)| camera.is_active)
            && reflectance.is_some_and(|(authored, actual)| {
                actual.to_bits() == effective_reflectance(request, authored).to_bits()
            }),
        full_preset_applied: false,
        status: if ssr_budget_projected {
            "partial_cpu_ssr_budget_not_complete_optics_or_gpu_execution"
        } else {
            "incomplete_cpu_ssr_budget"
        },
    }
}

pub(super) fn environment_images_available(
    map: Option<&bevy::light::EnvironmentMapLight>,
    images: Option<&Assets<Image>>,
) -> bool {
    let (Some(map), Some(images)) = (map, images) else {
        return false;
    };
    [&map.diffuse_map, &map.specular_map]
        .into_iter()
        .all(|handle| {
            images.get(handle).is_some_and(|image| {
                let descriptor = &image.texture_descriptor;
                let size = descriptor.size;
                size.width > 0
                    && size.width == size.height
                    && size.depth_or_array_layers == 6
                    && descriptor.sample_count == 1
                    && descriptor.dimension == bevy::render::render_resource::TextureDimension::D2
                    && descriptor
                        .usage
                        .contains(bevy::render::render_resource::TextureUsages::TEXTURE_BINDING)
            })
        })
}

pub(super) fn effective_tint(request: &WaterQualityRequest, authored: f32) -> f32 {
    match request.renderer_settings().refraction {
        QualityWaterRefraction::Disabled => 0.0,
        QualityWaterRefraction::SurfaceTint | QualityWaterRefraction::ScreenSpace => authored,
    }
}

pub(super) fn effective_reflectance(request: &WaterQualityRequest, authored: f32) -> f32 {
    match request.renderer_settings().reflection {
        // The water shader forces metallic=0 and has no clearcoat. Bevy's dielectric F0 and
        // specular occlusion are then zero, including grazing-angle environment reflection.
        // This is a CPU projection contract; GPU qualification is reported separately.
        QualityWaterReflection::Disabled => 0.0,
        QualityWaterReflection::Environment | QualityWaterReflection::ScreenSpace => authored,
    }
}
