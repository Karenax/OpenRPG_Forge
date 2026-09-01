use crate::visual_valley::VisualValleySun;
use bevy::{
    camera::{Exposure, Hdr},
    core_pipeline::tonemapping::Tonemapping,
    light::{Atmosphere, AtmosphereEnvironmentMapLight, SunDisk, atmosphere::ScatteringMedium},
    pbr::{AtmosphereMode, AtmosphereSettings, DistanceFog, FogFalloff},
    prelude::*,
};
use forge_project::{
    DistanceFogFalloff, EnvironmentProfileDocument, SrgbColor, SunProfile, VisualQualityPreset,
    VolumetricFogCapability,
};
use serde::Serialize;

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
    pub atmosphere_environment_map_light: bool,
    pub environment_map_intensity: Option<f32>,
    pub environment_map_resolution: Option<[u32; 2]>,
    pub aerial_view_max_distance_m: f32,
}

#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct EnvironmentSunStats {
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
    pub requested: bool,
    pub applied: bool,
    pub resolved_path: &'static str,
    pub requested_step_count: u16,
    pub fallback: &'static str,
}

/// ECS-observed projection of the environment profile.
///
/// This intentionally does not claim that the render pipeline executed on the GPU.
#[derive(Clone, Debug, PartialEq, Resource, Serialize)]
pub struct EnvironmentProjectionStats {
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
/// visible in metrics, but no volumetric component is installed until a native/WebGPU qualification
/// proves that the combined atmosphere path is safe.
pub struct VisualValleyEnvironmentPlugin {
    document: EnvironmentProfileDocument,
    quality: Option<VisualQualityPreset>,
}

impl VisualValleyEnvironmentPlugin {
    #[must_use]
    pub const fn new(document: EnvironmentProfileDocument) -> Self {
        Self {
            document,
            quality: None,
        }
    }

    /// Applies startup-only atmosphere budgets from a validated quality preset.
    #[must_use]
    pub fn with_quality_preset(mut self, preset: VisualQualityPreset) -> Self {
        self.quality = Some(preset);
        self
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
        let mut projection_document = self.document.clone();
        if let Some(quality) = &self.quality {
            projection_document.sky.enabled &= quality.environment.atmosphere_enabled;
            projection_document.sky.environment_resolution =
                quality.environment.environment_map_resolution;
        }
        app.insert_resource(VisualValleyEnvironmentProfile {
            document: projection_document,
            source_hash,
        })
        .add_systems(
            PostStartup,
            (project_environment, observe_environment_projection).chain(),
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
        commands.entity(camera).insert((
            Hdr,
            AtmosphereEnvironmentMapLight {
                intensity: 1.0,
                size: UVec2::splat(u32::from(document.sky.environment_resolution)),
                ..default()
            },
            AtmosphereSettings {
                aerial_view_lut_max_distance: document.sky.aerial_view_max_distance_m,
                rendering_method: AtmosphereMode::LookupTexture,
                ..default()
            },
        ));
        let resolution = u32::from(document.sky.environment_resolution);
        let medium = scattering_media.add(
            ScatteringMedium::earth(resolution, resolution)
                .with_density_multiplier(document.sky.medium_density_multiplier),
        );
        commands.spawn((
            Name::new("Visual Valley atmosphere"),
            VisualValleyAtmosphere,
            Atmosphere::earth(medium),
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
    clippy::too_many_lines,
    clippy::type_complexity,
    reason = "the observer records independent ECS evidence without hidden globals"
)]
fn observe_environment_projection(
    mut commands: Commands,
    profile: Res<VisualValleyEnvironmentProfile>,
    cameras: Query<
        (
            &Camera,
            Option<&Hdr>,
            Option<&AtmosphereSettings>,
            Option<&Exposure>,
            Option<&Tonemapping>,
            Option<&AmbientLight>,
            Option<&AtmosphereEnvironmentMapLight>,
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
    atmospheres: Query<Entity, With<VisualValleyAtmosphere>>,
    fog_volumes: Query<Entity, With<bevy::light::FogVolume>>,
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
    let camera = active.first().copied();
    let sun = valley_suns.first().copied();

    let camera_complete = camera.is_some_and(
        |(_, hdr, atmosphere, exposure, tonemapping, ambient, environment_map, distance_fog)| {
            (!profile.document.sky.enabled
                || (hdr.is_some() && atmosphere.is_some() && environment_map.is_some()))
                && exposure.is_some()
                && matches!(tonemapping, Some(Tonemapping::TonyMcMapface))
                && ambient.is_some()
                && (!profile.document.distance_fog.enabled || distance_fog.is_some())
        },
    );
    let sun_complete = sun.is_some_and(|(_, _, _, disk)| disk.is_some());
    let expected_atmospheres = usize::from(profile.document.sky.enabled);
    let status = if active_camera_3d_entities != 1 {
        EnvironmentProjectionStatus::InvalidCameraTopology
    } else if directional_light_entities != 1 || visual_valley_sun_entities != 1 {
        EnvironmentProjectionStatus::InvalidSunTopology
    } else if !camera_complete
        || !sun_complete
        || atmosphere_entities != expected_atmospheres
        || fog_volume_entities != 0
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
        tonemapping: camera.and_then(|(_, _, _, _, value, ..)| {
            matches!(value, Some(Tonemapping::TonyMcMapface)).then_some("tony_mc_mapface")
        }),
        ambient_light: camera.is_some_and(|(_, _, _, _, _, value, ..)| value.is_some()),
        atmosphere_environment_map_light: camera
            .is_some_and(|(_, _, _, _, _, _, value, _)| value.is_some()),
        environment_map_intensity: camera
            .and_then(|(_, _, _, _, _, _, value, _)| value.map(|value| value.intensity)),
        environment_map_resolution: camera
            .and_then(|(_, _, _, _, _, _, value, _)| value.map(|value| value.size.to_array())),
        aerial_view_max_distance_m: profile.document.sky.aerial_view_max_distance_m,
    };
    let sun_stats = EnvironmentSunStats {
        illuminance_lux: sun.map(|(light, ..)| light.illuminance),
        color_linear_rgba: sun.map(|(light, ..)| LinearRgba::from(light.color).to_f32_array()),
        forward_world: sun.map(|(_, transform, ..)| transform.forward().as_vec3().to_array()),
        shadows_enabled: sun.map(|(light, ..)| light.shadow_maps_enabled),
        sun_disk: sun.is_some_and(|(_, _, _, disk)| disk.is_some()),
    };
    let distance_fog = EnvironmentDistanceFogStats {
        requested: profile.document.distance_fog.enabled,
        component_present: camera.is_some_and(|(_, _, _, _, _, _, _, value)| value.is_some()),
        falloff_kind: match profile.document.distance_fog.falloff {
            DistanceFogFalloff::Linear { .. } => "linear",
            DistanceFogFalloff::Atmospheric { .. } => "atmospheric",
        },
        measurement_distance_m: profile.document.distance_fog.measurement_distance_m(),
        maximum_opacity: profile.document.distance_fog.maximum_opacity,
    };
    let resolved_path = match profile
        .document
        .resolve_fog_path(VolumetricFogCapability::NotQualified)
    {
        forge_project::EnvironmentFogResolution::Disabled => "disabled",
        forge_project::EnvironmentFogResolution::DistanceFog => "distance_fog",
        forge_project::EnvironmentFogResolution::Volumetric { .. } => "volumetric",
        forge_project::EnvironmentFogResolution::DistanceFogFallback { .. } => {
            "distance_fog_fallback"
        }
    };
    commands.insert_resource(EnvironmentProjectionStats {
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
        volumetric: EnvironmentVolumetricStats {
            requested: profile.document.volumetric_fog.enabled,
            applied: false,
            resolved_path,
            requested_step_count: profile.document.volumetric_fog.step_count,
            fallback: "distance_fog",
        },
    });
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
        assert!(stats.sky.atmosphere_environment_map_light);
        assert_eq!(stats.sky.environment_map_intensity, Some(1.0));
        assert_eq!(stats.sky.environment_map_resolution, Some([256, 256]));
        assert!(stats.distance_fog.component_present);
        assert!(!stats.volumetric.applied);
        assert_eq!(stats.volumetric.resolved_path, "distance_fog_fallback");
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
