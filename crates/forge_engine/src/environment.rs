use crate::visual_valley::VisualValleySun;
use bevy::{
    camera::{Exposure, Hdr},
    core_pipeline::tonemapping::Tonemapping,
    light::{Atmosphere, AtmosphereEnvironmentMapLight, SunDisk, atmosphere::ScatteringMedium},
    pbr::{AtmosphereMode, AtmosphereSettings, DistanceFog, FogFalloff},
    prelude::*,
};
use forge_project::{
    DistanceFogFalloff, EnvironmentProfileDocument, SrgbColor, SunProfile, VolumetricFogCapability,
};
use serde::Serialize;
mod quality;
mod volumetric;
mod weather_sunlight;
pub use quality::{EnvironmentQualityObservation, EnvironmentQualityRequest};
pub use volumetric::projection::VolumetricPreviewRequest;
pub use weather_sunlight::WeatherSunlightObservation;

const ENVIRONMENT_EVIDENCE_SOURCE: &str = "main_world_components_not_gpu_execution";

#[derive(Clone, Debug, Resource)]
struct VisualValleyEnvironmentProfile {
    document: EnvironmentProfileDocument,
    source_hash: String,
}

#[derive(Clone, Copy, Component, Debug)]
struct VisualValleyAtmosphere;

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum EnvironmentProjectionStatus {
    ObservedApproximation,
    InvalidCameraTopology,
    InvalidSunTopology,
    IncompleteComponents,
}

#[derive(Clone, Debug, PartialEq, Serialize)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "the evidence schema records independent component-presence observations"
)]
pub struct EnvironmentSkyStats {
    pub requested: bool,
    pub atmosphere_settings: bool,
    pub hdr: bool,
    pub exposure_ev100: Option<f32>,
    pub tonemapping: Option<&'static str>,
    pub ambient_light: bool,
    pub atmosphere_ibl: bool,
    pub atmosphere_ibl_resolution: Option<[u32; 2]>,
    pub atmosphere_ibl_intensity: Option<f32>,
    pub aerial_view_max_distance_m: f32,
}

#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct EnvironmentSunStats {
    pub weather_response: WeatherSunlightObservation,
    pub illuminance_lux: Option<f32>,
    pub color_linear_rgba: Option<[f32; 4]>,
    pub forward_world: Option<[f32; 3]>,
    pub shadows_enabled: Option<bool>,
    pub sun_disk: bool,
}

#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct EnvironmentDistanceFogStats {
    pub requested: bool,
    pub component_present: bool,
    pub falloff_kind: &'static str,
    pub measurement_distance_m: f32,
    pub maximum_opacity: f32,
}

#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct EnvironmentVolumetricStats {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub effective_maximum_distance_m: Option<f32>,
    pub requested: bool,
    pub applied: bool,
    pub resolved_path: &'static str,
    /// Selected quality budget, or authored budget without a quality request; not executed steps.
    pub requested_step_count: u16,
    pub fallback: &'static str,
}

/// ECS-observed projection of the environment profile.
///
/// This intentionally does not claim that the render pipeline executed on the GPU.
#[derive(Clone, Debug, PartialEq, Resource, Serialize)]
pub struct EnvironmentProjectionStats {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub quality: Option<EnvironmentQualityObservation>,
    pub source_hash: String,
    pub status: EnvironmentProjectionStatus,
    pub evidence_source: &'static str,
    pub camera_3d_entities: usize,
    pub active_camera_3d_entities: usize,
    pub directional_light_entities: usize,
    pub visual_valley_sun_entities: usize,
    pub atmosphere_entities: usize,
    pub fog_volume_entities: usize,
    pub sky: EnvironmentSkyStats,
    pub sun: EnvironmentSunStats,
    pub distance_fog: EnvironmentDistanceFogStats,
    pub volumetric: EnvironmentVolumetricStats,
}

/// Projects one validated Visual Valley environment profile onto the existing camera and sun.
///
/// R1-021 deliberately uses the documented distance-fog approximation. Volumetric intent remains
/// visible in metrics. Experimental ambient volumetrics require an explicit preview request;
/// the default stays unchanged until native/WebGPU qualification proves the combined path safe.
pub struct VisualValleyEnvironmentPlugin {
    document: EnvironmentProfileDocument,
}

impl VisualValleyEnvironmentPlugin {
    #[must_use]
    pub const fn new(document: EnvironmentProfileDocument) -> Self {
        Self { document }
    }
}

impl Plugin for VisualValleyEnvironmentPlugin {
    fn build(&self, app: &mut App) {
        self.document
            .validate()
            .expect("VisualValleyEnvironmentPlugin requires a validated profile");
        let source_hash = self
            .document
            .content_hash()
            .expect("validated environment profile must have a canonical hash")
            .to_string();
        app.insert_resource(VisualValleyEnvironmentProfile {
            document: self.document.clone(),
            source_hash,
        })
        .add_systems(PostStartup, project_environment)
        .init_resource::<quality::probe::SkyProbeState>()
        .init_resource::<VolumetricPreviewRequest>()
        .init_resource::<WeatherSunlightObservation>()
        .add_systems(Update, quality::apply)
        .add_systems(
            PostUpdate,
            (
                weather_sunlight::project,
                quality::probe::publish,
                volumetric::projection::apply,
                observe_environment_projection,
            )
                .chain()
                .after(bevy::transform::TransformSystems::Propagate),
        );
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn project_environment(
    mut commands: Commands,
    profile: Res<VisualValleyEnvironmentProfile>,
    cameras: Query<(Entity, &Camera), With<Camera3d>>,
    directional_lights: Query<(Entity, Option<&VisualValleySun>), With<DirectionalLight>>,
    mut scattering_media: ResMut<Assets<ScatteringMedium>>,
    quality: Option<Res<EnvironmentQualityRequest>>,
) {
    let active_cameras = cameras
        .iter()
        .filter_map(|(entity, camera)| camera.is_active.then_some(entity))
        .collect::<Vec<_>>();
    let valley_suns = directional_lights
        .iter()
        .filter_map(|(entity, marker)| marker.is_some().then_some(entity))
        .collect::<Vec<_>>();

    if active_cameras.len() != 1 || directional_lights.iter().count() != 1 || valley_suns.len() != 1
    {
        return;
    }

    let document = &profile.document;
    let camera = active_cameras[0];
    let sun = valley_suns[0];
    commands.insert_resource(ClearColor(srgb_color(
        document.sky.fallback_clear_color_srgb,
        1.0,
    )));

    commands.entity(camera).insert((
        Exposure {
            ev100: document.sky.exposure_ev100,
        },
        Tonemapping::TonyMcMapface,
        AmbientLight {
            color: srgb_color(document.sky.fallback_clear_color_srgb, 1.0),
            brightness: document.sky.environment_light_intensity,
            ..default()
        },
    ));
    if document.sky.enabled {
        let settings = AtmosphereSettings {
            aerial_view_lut_max_distance: document.sky.aerial_view_max_distance_m,
            rendering_method: AtmosphereMode::LookupTexture,
            ..default()
        };
        let resolution = u32::from(document.sky.environment_resolution);
        let map = AtmosphereEnvironmentMapLight {
            size: UVec2::splat(resolution),
            // Unit gain preserves the physical brightness of the generated sky.
            intensity: 1.0,
            ..default()
        };
        if quality.is_none() {
            commands.entity(camera).insert(map.clone());
        }
        commands.entity(camera).insert((
            Hdr,
            settings.clone(),
            quality::PreparedSkyCamera(settings, map),
        ));
        let medium = scattering_media.add(
            ScatteringMedium::earth(resolution, resolution)
                .with_density_multiplier(document.sky.medium_density_multiplier),
        );
        let atmosphere = Atmosphere::earth(medium);
        commands.spawn((
            Name::new("Visual Valley atmosphere"),
            VisualValleyAtmosphere,
            quality::PreparedAtmosphere(atmosphere.clone()),
            atmosphere,
        ));
    } else {
        commands
            .entity(camera)
            .remove::<(Hdr, AtmosphereSettings, AtmosphereEnvironmentMapLight)>();
    }

    if document.distance_fog.enabled {
        commands
            .entity(camera)
            .insert(distance_fog_component(document));
    } else {
        commands.entity(camera).remove::<DistanceFog>();
    }

    commands.entity(sun).insert((
        DirectionalLight {
            color: srgb_color(document.sun.color_srgb, 1.0),
            illuminance: document.sun.illuminance_lux,
            shadow_maps_enabled: true,
            ..default()
        },
        sun_transform(&document.sun),
        SunDisk {
            angular_size: document.sun.disk_angular_diameter_degrees.to_radians(),
            intensity: document.sun.disk_intensity,
        },
    ));
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    clippy::type_complexity,
    clippy::too_many_lines,
    reason = "the observer records independent ECS evidence without hidden globals"
)]
fn observe_environment_projection(
    mut commands: Commands,
    profile: Res<VisualValleyEnvironmentProfile>,
    sunlight_observation: Res<WeatherSunlightObservation>,
    cameras: Query<
        (
            &Camera,
            Option<&Hdr>,
            Option<&AtmosphereSettings>,
            Option<&Exposure>,
            Option<&Tonemapping>,
            Option<&AmbientLight>,
            Option<&DistanceFog>,
        ),
        With<Camera3d>,
    >,
    directional_lights: Query<(
        &DirectionalLight,
        &Transform,
        Option<&VisualValleySun>,
        Option<&SunDisk>,
    )>,
    atmospheres: Query<Entity, (With<VisualValleyAtmosphere>, With<Atmosphere>)>,
    fog_volumes: Query<Option<&volumetric::projection::OwnedVolume>, With<bevy::light::FogVolume>>,
    atmosphere_lights: Query<(
        Option<&Camera>,
        &AtmosphereEnvironmentMapLight,
        Option<&quality::probe::OwnedSkyProbe>,
    )>,
    post_process_quality: Option<Res<crate::PostProcessQualityRequest>>,
    quality: Option<Res<EnvironmentQualityRequest>>,
    probe_state: Res<quality::probe::SkyProbeState>,
) {
    let camera_3d_entities = cameras.iter().count();
    let active = cameras
        .iter()
        .filter(|(camera, ..)| camera.is_active)
        .collect::<Vec<_>>();
    let active_camera_3d_entities = active.len();
    let directional_light_entities = directional_lights.iter().count();
    let valley_suns = directional_lights
        .iter()
        .filter(|(_, _, marker, _)| marker.is_some())
        .collect::<Vec<_>>();
    let visual_valley_sun_entities = valley_suns.len();
    let atmosphere_entities = atmospheres.iter().count();
    let fog_volume_entities = fog_volumes.iter().count();
    let owned_volumes = fog_volumes.iter().flatten().count();
    let camera = active.first().copied();
    let sun = valley_suns.first().copied();
    let atmosphere_light = atmosphere_lights
        .iter()
        .find(|(camera, _, owned)| camera.is_some_and(|camera| camera.is_active) || owned.is_some())
        .map(|(_, light, _)| light);

    let expected_tonemapping = crate::post_process_quality::tonemapping(
        post_process_quality
            .as_ref()
            .is_none_or(|request| request.settings().tonemapping_enabled),
    );
    let sky_enabled = profile.document.sky.enabled
        && quality
            .as_ref()
            .is_none_or(|q| q.settings().atmosphere_enabled);
    let camera_complete = camera.is_some_and(
        |(_, hdr, atmosphere, exposure, tonemapping, ambient, distance_fog)| {
            (!sky_enabled || (hdr.is_some() && atmosphere.is_some() && atmosphere_light.is_some()))
                && exposure.is_some()
                && tonemapping == Some(&expected_tonemapping)
                && ambient.is_some()
                && (!profile.document.distance_fog.enabled || distance_fog.is_some())
        },
    );
    let sun_complete = sun.is_some_and(|(_, _, _, disk)| disk.is_some());
    let status = if active_camera_3d_entities != 1 {
        EnvironmentProjectionStatus::InvalidCameraTopology
    } else if directional_light_entities != 1 || visual_valley_sun_entities != 1 {
        EnvironmentProjectionStatus::InvalidSunTopology
    } else if !camera_complete
        || !sun_complete
        || !matches!(sunlight_observation.status, "disabled" | "applied_cpu")
        || atmosphere_entities != usize::from(sky_enabled)
        || fog_volume_entities != owned_volumes
    {
        EnvironmentProjectionStatus::IncompleteComponents
    } else {
        EnvironmentProjectionStatus::ObservedApproximation
    };

    let sky = EnvironmentSkyStats {
        requested: profile.document.sky.enabled,
        atmosphere_settings: camera.is_some_and(|(_, _, value, ..)| value.is_some()),
        hdr: camera.is_some_and(|(_, value, ..)| value.is_some()),
        exposure_ev100: camera.and_then(|(_, _, _, value, ..)| value.map(|value| value.ev100)),
        tonemapping: camera
            .and_then(|(_, _, _, _, value, ..)| value.copied().map(tonemapping_label)),
        ambient_light: camera.is_some_and(|(_, _, _, _, _, value, _)| value.is_some()),
        aerial_view_max_distance_m: profile.document.sky.aerial_view_max_distance_m,
        atmosphere_ibl: atmosphere_light.is_some(),
        atmosphere_ibl_resolution: atmosphere_light.map(|light| light.size.to_array()),
        atmosphere_ibl_intensity: atmosphere_light.map(|light| light.intensity),
    };
    let mut sun_stats = sun_observation(
        sun.map(|(light, ..)| light),
        sun.map(|(_, transform, ..)| transform),
        sun.is_some_and(|(_, _, _, disk)| disk.is_some()),
    );
    sun_stats.weather_response = sunlight_observation.clone();
    let distance_fog = distance_fog_stats(
        &profile.document,
        camera.is_some_and(|(_, _, _, _, _, _, value)| value.is_some()),
    );
    let mut observation = EnvironmentProjectionStats {
        quality: None,
        source_hash: profile.source_hash.clone(),
        status,
        evidence_source: ENVIRONMENT_EVIDENCE_SOURCE,
        camera_3d_entities,
        active_camera_3d_entities,
        directional_light_entities,
        visual_valley_sun_entities,
        atmosphere_entities,
        fog_volume_entities,
        sky,
        sun: sun_stats,
        distance_fog,
        volumetric: volumetric_stats(&profile.document),
    };
    observation.quality = quality.as_ref().map(|q| {
        q.observe(
            &observation,
            profile.document.sky.enabled,
            probe_state.texture_bound,
        )
    });
    volumetric::projection::annotate(
        &mut observation,
        fog_volumes.iter().flatten().next().map(|v| v.0),
    );
    commands.insert_resource(observation);
}

fn tonemapping_label(value: Tonemapping) -> &'static str {
    match value {
        Tonemapping::TonyMcMapface => "tony_mc_mapface",
        Tonemapping::None => "none",
        _ => "other",
    }
}

fn sun_observation(
    light: Option<&DirectionalLight>,
    transform: Option<&Transform>,
    sun_disk: bool,
) -> EnvironmentSunStats {
    EnvironmentSunStats {
        weather_response: WeatherSunlightObservation::default(),
        illuminance_lux: light.map(|light| light.illuminance),
        color_linear_rgba: light.map(|light| LinearRgba::from(light.color).to_f32_array()),
        forward_world: transform.map(|transform| transform.forward().as_vec3().to_array()),
        shadows_enabled: light.map(|light| light.shadow_maps_enabled),
        sun_disk,
    }
}

fn distance_fog_stats(
    document: &EnvironmentProfileDocument,
    component_present: bool,
) -> EnvironmentDistanceFogStats {
    EnvironmentDistanceFogStats {
        requested: document.distance_fog.enabled,
        component_present,
        falloff_kind: match document.distance_fog.falloff {
            DistanceFogFalloff::Linear { .. } => "linear",
            DistanceFogFalloff::Atmospheric { .. } => "atmospheric",
        },
        measurement_distance_m: document.distance_fog.measurement_distance_m(),
        maximum_opacity: document.distance_fog.maximum_opacity,
    }
}

fn volumetric_stats(document: &EnvironmentProfileDocument) -> EnvironmentVolumetricStats {
    EnvironmentVolumetricStats {
        effective_maximum_distance_m: None,
        requested: document.volumetric_fog.enabled,
        applied: false,
        resolved_path: resolved_fog_path(document),
        requested_step_count: document.volumetric_fog.step_count,
        fallback: "distance_fog",
    }
}

fn resolved_fog_path(document: &EnvironmentProfileDocument) -> &'static str {
    match document.resolve_fog_path(VolumetricFogCapability::NotQualified) {
        forge_project::EnvironmentFogResolution::Disabled => "disabled",
        forge_project::EnvironmentFogResolution::DistanceFog => "distance_fog",
        forge_project::EnvironmentFogResolution::Volumetric { .. } => "volumetric",
        forge_project::EnvironmentFogResolution::DistanceFogFallback { .. } => {
            "distance_fog_fallback"
        }
    }
}

fn srgb_color(color: SrgbColor, alpha: f32) -> Color {
    Color::srgba(color.red, color.green, color.blue, alpha)
}

fn distance_fog_component(document: &EnvironmentProfileDocument) -> DistanceFog {
    let profile = document.distance_fog;
    let fog_color = srgb_color(profile.color_srgb, profile.maximum_opacity);
    let falloff = match profile.falloff {
        DistanceFogFalloff::Linear {
            start_distance_m,
            end_distance_m,
        } => FogFalloff::Linear {
            start: start_distance_m,
            end: end_distance_m,
        },
        DistanceFogFalloff::Atmospheric {
            visibility_distance_m,
            contrast_threshold,
        } => FogFalloff::from_visibility_contrast_color(
            visibility_distance_m,
            contrast_threshold,
            fog_color,
        ),
    };
    DistanceFog {
        color: fog_color,
        directional_light_color: srgb_color(profile.directional_light_color_srgb, 1.0),
        directional_light_exponent: profile.directional_light_exponent,
        falloff,
    }
}

fn sun_transform(profile: &SunProfile) -> Transform {
    let direction = profile.direction_toward_sun();
    let toward_sun = Vec3::new(direction.x, direction.y, direction.z);
    let light_direction = -toward_sun.normalize_or_zero();
    let up = if light_direction.dot(Vec3::Y).abs() > 0.999 {
        Vec3::Z
    } else {
        Vec3::Y
    };
    Transform::IDENTITY.looking_at(light_direction, up)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::visual_valley::VisualValleySun;
    use forge_world::embedded_visual_valley_environment_profile;

    fn profile() -> EnvironmentProfileDocument {
        embedded_visual_valley_environment_profile().unwrap()
    }

    #[test]
    fn observation_refreshes_after_runtime_changes_without_reprojecting_sources() {
        let mut app = App::new();
        app.init_resource::<Assets<ScatteringMedium>>();
        let camera = app
            .world_mut()
            .spawn((Camera3d::default(), Camera::default()))
            .id();
        let sun = app
            .world_mut()
            .spawn((
                VisualValleySun,
                DirectionalLight::default(),
                Transform::IDENTITY,
            ))
            .id();
        app.add_plugins(VisualValleyEnvironmentPlugin::new(profile()));
        app.update();
        let source_hash = app
            .world()
            .resource::<EnvironmentProjectionStats>()
            .source_hash
            .clone();
        app.world_mut().entity_mut(camera).insert(Tonemapping::None);
        app.world_mut()
            .entity_mut(camera)
            .get_mut::<AtmosphereEnvironmentMapLight>()
            .unwrap()
            .size = UVec2::splat(64);
        app.world_mut()
            .entity_mut(sun)
            .get_mut::<DirectionalLight>()
            .unwrap()
            .shadow_maps_enabled = false;
        app.update();
        let stats = app.world().resource::<EnvironmentProjectionStats>();
        assert_eq!(
            stats.status,
            EnvironmentProjectionStatus::IncompleteComponents
        );
        assert_eq!(stats.sky.tonemapping, Some("none"));
        assert_eq!(stats.sky.atmosphere_ibl_resolution, Some([64, 64]));
        assert_eq!(stats.sun.shadows_enabled, Some(false));
        assert_eq!(stats.source_hash, source_hash);
        app.world_mut()
            .entity_mut(camera)
            .insert(Tonemapping::TonyMcMapface);
        app.update();
        assert_eq!(
            app.world().resource::<EnvironmentProjectionStats>().status,
            EnvironmentProjectionStatus::ObservedApproximation
        );
        app.world_mut()
            .entity_mut(camera)
            .get_mut::<Camera>()
            .unwrap()
            .is_active = false;
        app.update();
        assert_eq!(
            app.world().resource::<EnvironmentProjectionStats>().status,
            EnvironmentProjectionStatus::InvalidCameraTopology
        );
    }

    #[test]
    fn atmospheric_fallback_preserves_authored_visibility() {
        let document = profile();
        let fog = distance_fog_component(&document);
        assert!(matches!(fog.falloff, FogFalloff::Atmospheric { .. }));
        assert!((document.distance_fog.measurement_distance_m() - 640.0).abs() < f32::EPSILON);
        assert!((LinearRgba::from(fog.color).alpha - 0.82).abs() < f32::EPSILON);
    }

    #[test]
    fn sun_projection_is_finite_unit_and_points_downward() {
        let transform = sun_transform(&profile().sun);
        let forward = transform.forward().as_vec3();
        assert!(forward.is_finite());
        assert!((forward.length() - 1.0).abs() < 1.0e-5);
        assert!(forward.y < 0.0);
    }

    #[test]
    fn plugin_observes_one_camera_one_sun_and_distance_fog_fallback() {
        let mut app = App::new();
        app.init_resource::<Assets<ScatteringMedium>>();
        app.world_mut()
            .spawn((Camera3d::default(), Camera::default()));
        app.world_mut().spawn((
            VisualValleySun,
            DirectionalLight::default(),
            Transform::IDENTITY,
        ));
        app.add_plugins(VisualValleyEnvironmentPlugin::new(profile()));
        app.update();

        let stats = app.world().resource::<EnvironmentProjectionStats>();
        assert_eq!(
            stats.status,
            EnvironmentProjectionStatus::ObservedApproximation
        );
        assert_eq!(stats.camera_3d_entities, 1);
        assert_eq!(stats.visual_valley_sun_entities, 1);
        assert_eq!(stats.atmosphere_entities, 1);
        assert!(stats.sky.hdr);
        assert!(stats.sky.atmosphere_ibl);
        assert_eq!(stats.sky.atmosphere_ibl_resolution, Some([256, 256]));
        assert_eq!(
            stats.sky.atmosphere_ibl_intensity.unwrap().to_bits(),
            1.0_f32.to_bits()
        );
        assert!(stats.distance_fog.component_present);
        assert!(!stats.volumetric.applied);
        assert_eq!(stats.volumetric.resolved_path, "distance_fog_fallback");
    }

    #[test]
    fn disabled_sky_removes_atmosphere_lighting_and_keeps_ambient_fallback() {
        let mut app = App::new();
        app.init_resource::<Assets<ScatteringMedium>>();
        let camera = app
            .world_mut()
            .spawn((
                Camera3d::default(),
                Camera::default(),
                Hdr,
                AtmosphereSettings::default(),
                AtmosphereEnvironmentMapLight::default(),
            ))
            .id();
        app.world_mut().spawn((
            VisualValleySun,
            DirectionalLight::default(),
            Transform::IDENTITY,
        ));
        let mut document = profile();
        document.sky.enabled = false;
        app.add_plugins(VisualValleyEnvironmentPlugin::new(document));
        app.update();
        let entity = app.world().entity(camera);
        assert!(!entity.contains::<AtmosphereEnvironmentMapLight>());
        assert!(!entity.contains::<AtmosphereSettings>());
        assert!(!entity.contains::<Hdr>());
        assert!(entity.contains::<AmbientLight>());
        let stats = app.world().resource::<EnvironmentProjectionStats>();
        assert_eq!(
            stats.status,
            EnvironmentProjectionStatus::ObservedApproximation
        );
        assert_eq!(stats.atmosphere_entities, 0);
        assert!(!stats.sky.atmosphere_ibl);
        assert_eq!(stats.sky.atmosphere_ibl_resolution, None);
        assert_eq!(stats.sky.atmosphere_ibl_intensity, None);
    }

    #[test]
    fn invalid_topology_is_reported_without_projecting_components() {
        let mut app = App::new();
        app.init_resource::<Assets<ScatteringMedium>>();
        app.world_mut()
            .spawn((Camera3d::default(), Camera::default()));
        app.world_mut()
            .spawn((Camera3d::default(), Camera::default()));
        app.world_mut().spawn((
            VisualValleySun,
            DirectionalLight::default(),
            Transform::IDENTITY,
        ));
        app.add_plugins(VisualValleyEnvironmentPlugin::new(profile()));
        app.update();

        let stats = app.world().resource::<EnvironmentProjectionStats>();
        assert_eq!(
            stats.status,
            EnvironmentProjectionStatus::InvalidCameraTopology
        );
        assert_eq!(stats.atmosphere_entities, 0);
    }
}
