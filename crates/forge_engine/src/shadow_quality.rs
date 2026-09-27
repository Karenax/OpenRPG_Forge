use crate::visual_valley::{VisualValleyShadowStats, VisualValleySun};
use bevy::{
    light::{CascadeShadowConfig, CascadeShadowConfigBuilder, DirectionalLightShadowMap},
    pbr::ContactShadows,
    prelude::*,
};
use forge_project::{
    ShadowQualitySettings, TerrainDocument, VisualQualityTier, VisualValleyQualityProfileDocument,
    VisualValleyQualityProfileError,
};
use serde::Serialize;

/// Renderer-only request. This never changes the quality document or acknowledges a global tier.
#[derive(Resource)]
pub struct ShadowQualityRequest {
    document: VisualValleyQualityProfileDocument,
    source_hash: String,
    tier: VisualQualityTier,
}

impl ShadowQualityRequest {
    pub const fn request(&mut self, tier: VisualQualityTier) {
        self.tier = tier;
    }
}

#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct ShadowQualityObservation {
    pub source_hash: String,
    pub requested_tier: VisualQualityTier,
    pub projected_tier: Option<VisualQualityTier>,
    pub requested: ShadowQualitySettings,
    pub map_resolution: Option<usize>,
    pub light_contact_shadows: Option<bool>,
    pub camera_contact_shadows: Option<bool>,
    pub contact_linear_steps: Option<u32>,
    pub status: &'static str,
}

#[derive(Clone, Copy, Debug, Eq, Hash, PartialEq, SystemSet)]
pub(crate) struct ShadowQualitySet;

pub struct VisualValleyShadowQualityPlugin {
    document: VisualValleyQualityProfileDocument,
    source_hash: String,
    tier: VisualQualityTier,
}

impl VisualValleyShadowQualityPlugin {
    pub fn new(
        terrain: &TerrainDocument,
        document: VisualValleyQualityProfileDocument,
        tier: VisualQualityTier,
    ) -> Result<Self, VisualValleyQualityProfileError> {
        document.validate_identity(terrain.project_id, terrain.area_id, terrain.revision)?;
        let source_hash = document.content_hash()?.to_string();
        Ok(Self {
            document,
            source_hash,
            tier,
        })
    }
}

impl Plugin for VisualValleyShadowQualityPlugin {
    fn build(&self, app: &mut App) {
        app.insert_resource(ShadowQualityRequest {
            document: self.document.clone(),
            source_hash: self.source_hash.clone(),
            tier: self.tier,
        })
        .init_resource::<DirectionalLightShadowMap>()
        .init_resource::<VisualValleyShadowStats>()
        // Install budgets before Bevy validates the atlas and rebuilds cascades in PostUpdate.
        .add_systems(Update, apply_shadow_quality)
        .add_systems(
            PostUpdate,
            observe_shadow_quality
                .in_set(ShadowQualitySet)
                .after(bevy::light::SimulationLightSystems::UpdateDirectionalLightCascades),
        );
    }
}

fn cascade_config(settings: ShadowQualitySettings) -> CascadeShadowConfig {
    if !settings.enabled {
        return CascadeShadowConfig {
            bounds: Vec::new(),
            overlap_proportion: 0.0,
            minimum_distance: 0.0,
        };
    }
    let mut configured = CascadeShadowConfigBuilder {
        num_cascades: usize::from(settings.cascade_count),
        minimum_distance: 0.0,
        maximum_distance: settings.maximum_distance_m,
        first_cascade_far_bound: (settings.maximum_distance_m * 0.5).min(24.0),
        ..default()
    }
    .build();
    // Geometric spacing may overshoot by a few ULPs; keep the authored outer budget exact.
    if let Some(last) = configured.bounds.last_mut() {
        *last = settings.maximum_distance_m;
    }
    configured
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::type_complexity,
    reason = "Bevy system parameters"
)]
fn apply_shadow_quality(
    mut commands: Commands,
    request: Res<ShadowQualityRequest>,
    mut map: ResMut<DirectionalLightShadowMap>,
    mut lights: Query<(
        &mut DirectionalLight,
        Option<&mut CascadeShadowConfig>,
        Option<&VisualValleySun>,
    )>,
    cameras: Query<(Entity, &Camera, Option<&ContactShadows>), With<Camera3d>>,
) {
    // The shadow atlas is global. Never resize it when another directional light owns it too.
    let Ok((mut light, Some(mut cascades), Some(_))) = lights.single_mut() else {
        return;
    };
    let mut active = cameras.iter().filter(|(_, camera, _)| camera.is_active);
    let Some((camera, _, contact)) = active.next() else {
        return;
    };
    if active.next().is_some() {
        return;
    }
    let settings = request.document.preset(request.tier).shadows;
    if light.shadow_maps_enabled != settings.enabled {
        light.shadow_maps_enabled = settings.enabled;
    }
    if light.contact_shadows_enabled != settings.contact_shadows_enabled {
        light.contact_shadows_enabled = settings.contact_shadows_enabled;
    }
    let configured = cascade_config(settings);
    if cascades.bounds != configured.bounds
        || cascades.minimum_distance.to_bits() != configured.minimum_distance.to_bits()
        || cascades.overlap_proportion.to_bits() != configured.overlap_proportion.to_bits()
    {
        *cascades = configured;
    }
    // Disabled shadows keep the valid atlas allocation; zero-sized GPU textures are invalid.
    if settings.enabled && map.size != usize::from(settings.map_resolution) {
        map.size = usize::from(settings.map_resolution);
    }
    if settings.contact_shadows_enabled {
        if contact.is_none() {
            commands.entity(camera).insert(ContactShadows::default());
        }
    } else if contact.is_some() {
        // Keep the depth prepass: other effects may require it independently.
        commands.entity(camera).remove::<ContactShadows>();
    }
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::type_complexity,
    reason = "Bevy evidence observer"
)]
fn observe_shadow_quality(
    request: Res<ShadowQualityRequest>,
    map: Res<DirectionalLightShadowMap>,
    lights: Query<(
        &DirectionalLight,
        Option<&CascadeShadowConfig>,
        Option<&VisualValleySun>,
    )>,
    cameras: Query<(&Camera, Option<&ContactShadows>), With<Camera3d>>,
    mut stats: ResMut<VisualValleyShadowStats>,
) {
    let settings = request.document.preset(request.tier).shadows;
    let mut observed = ShadowQualityObservation {
        source_hash: request.source_hash.clone(),
        requested_tier: request.tier,
        projected_tier: None,
        requested: settings,
        map_resolution: Some(map.size),
        light_contact_shadows: None,
        camera_contact_shadows: None,
        contact_linear_steps: None,
        status: "invalid_light_or_camera_topology",
    };
    let mut active = cameras.iter().filter(|(camera, _)| camera.is_active);
    if let (Ok((light, Some(cascades), Some(_))), Some((_, contact)), None) =
        (lights.single(), active.next(), active.next())
    {
        observed.light_contact_shadows = Some(light.contact_shadows_enabled);
        observed.camera_contact_shadows = Some(contact.is_some());
        observed.contact_linear_steps = contact.map(|settings| settings.linear_steps);
        let matches = light.shadow_maps_enabled == settings.enabled
            && light.contact_shadows_enabled == settings.contact_shadows_enabled
            && contact.is_some() == settings.contact_shadows_enabled
            && (!settings.enabled || map.size == usize::from(settings.map_resolution))
            && cascades.bounds == cascade_config(settings).bounds;
        observed.status = if matches {
            "observed_cpu_projection_not_gpu_execution"
        } else {
            "incomplete_projection"
        };
        observed.projected_tier = matches.then_some(request.tier);
    }
    stats.quality = Some(observed);
}

#[cfg(test)]
mod tests {
    use super::*;

    fn policy() -> VisualValleyQualityProfileDocument {
        VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap()
    }

    fn app(document: VisualValleyQualityProfileDocument) -> (App, Entity, Entity) {
        let mut app = App::new();
        app.add_plugins(MinimalPlugins);
        let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
        app.add_plugins(
            VisualValleyShadowQualityPlugin::new(&terrain, document, VisualQualityTier::High)
                .unwrap(),
        );
        let sun = app
            .world_mut()
            .spawn((
                VisualValleySun,
                DirectionalLight {
                    illuminance: 12345.0,
                    ..default()
                },
                CascadeShadowConfig::default(),
                Transform::from_xyz(1.0, 2.0, 3.0),
            ))
            .id();
        let camera = app
            .world_mut()
            .spawn((Camera3d::default(), Camera::default()))
            .id();
        (app, sun, camera)
    }

    #[test]
    fn shadow_cycle_projects_every_budget_and_preserves_sun_and_source() {
        let document = policy();
        let hash = document.content_hash().unwrap().to_string();
        let (mut app, sun, camera) = app(document.clone());
        let mut high_bounds = None;
        for tier in [
            VisualQualityTier::High,
            VisualQualityTier::Low,
            VisualQualityTier::Ultra,
            VisualQualityTier::Medium,
            VisualQualityTier::High,
        ] {
            app.world_mut()
                .resource_mut::<ShadowQualityRequest>()
                .request(tier);
            app.update();
            let settings = document.preset(tier).shadows;
            let world = app.world();
            let light = world.get::<DirectionalLight>(sun).unwrap();
            assert_eq!(light.illuminance.to_bits(), 12345.0_f32.to_bits());
            assert_eq!(
                world.get::<Transform>(sun).unwrap().translation,
                Vec3::new(1.0, 2.0, 3.0)
            );
            assert_eq!(
                light.contact_shadows_enabled,
                settings.contact_shadows_enabled
            );
            assert_eq!(
                world.get::<ContactShadows>(camera).is_some(),
                settings.contact_shadows_enabled
            );
            let cascades = world.get::<CascadeShadowConfig>(sun).unwrap();
            assert_eq!(cascades.bounds.len(), usize::from(settings.cascade_count));
            assert_eq!(
                cascades.bounds.last().unwrap().to_bits(),
                settings.maximum_distance_m.to_bits()
            );
            assert_eq!(
                world.resource::<DirectionalLightShadowMap>().size,
                usize::from(settings.map_resolution)
            );
            let observed = world
                .resource::<VisualValleyShadowStats>()
                .quality
                .as_ref()
                .unwrap();
            assert_eq!(observed.projected_tier, Some(tier));
            assert_eq!(observed.source_hash, hash);
            if tier == VisualQualityTier::High {
                if let Some(previous) = &high_bounds {
                    assert_eq!(&cascades.bounds, previous);
                }
                high_bounds = Some(cascades.bounds.clone());
            }
        }
        assert_eq!(
            app.world().resource::<ShadowQualityRequest>().document,
            document
        );
    }

    #[test]
    fn ambiguous_topology_does_not_resize_global_atlas_or_claim_application() {
        let (mut app, _, _) = app(policy());
        let extra = app.world_mut().spawn(DirectionalLight::default()).id();
        app.update();
        assert_eq!(
            app.world().resource::<DirectionalLightShadowMap>().size,
            2048
        );
        assert_eq!(
            app.world()
                .resource::<VisualValleyShadowStats>()
                .quality
                .as_ref()
                .unwrap()
                .projected_tier,
            None
        );
        app.world_mut().despawn(extra);
        let extra = app
            .world_mut()
            .spawn((Camera3d::default(), Camera::default()))
            .id();
        app.update();
        assert_eq!(
            app.world().resource::<DirectionalLightShadowMap>().size,
            2048
        );
        assert_eq!(
            app.world()
                .resource::<VisualValleyShadowStats>()
                .quality
                .as_ref()
                .unwrap()
                .projected_tier,
            None
        );
        app.world_mut().despawn(extra);
        app.update();
        assert_eq!(
            app.world()
                .resource::<VisualValleyShadowStats>()
                .quality
                .as_ref()
                .unwrap()
                .projected_tier,
            Some(VisualQualityTier::High)
        );
    }

    #[test]
    fn disabled_shadows_keep_a_valid_atlas_and_restore_the_high_budget() {
        let mut document = policy();
        document.presets.low.shadows = ShadowQualitySettings {
            enabled: false,
            cascade_count: 0,
            map_resolution: 0,
            maximum_distance_m: 0.0,
            contact_shadows_enabled: false,
        };
        let (mut app, sun, camera) = app(document);
        app.update();
        app.world_mut()
            .resource_mut::<ShadowQualityRequest>()
            .request(VisualQualityTier::Low);
        app.update();
        assert!(
            !app.world()
                .get::<DirectionalLight>(sun)
                .unwrap()
                .shadow_maps_enabled
        );
        assert!(
            app.world()
                .get::<CascadeShadowConfig>(sun)
                .unwrap()
                .bounds
                .is_empty()
        );
        assert!(app.world().get::<ContactShadows>(camera).is_none());
        assert!(
            app.world()
                .get::<bevy::core_pipeline::prepass::DepthPrepass>(camera)
                .is_some()
        );
        assert_eq!(
            app.world().resource::<DirectionalLightShadowMap>().size,
            4096
        );
        assert_eq!(
            app.world()
                .resource::<VisualValleyShadowStats>()
                .quality
                .as_ref()
                .unwrap()
                .projected_tier,
            Some(VisualQualityTier::Low)
        );
        app.world_mut()
            .resource_mut::<ShadowQualityRequest>()
            .request(VisualQualityTier::High);
        app.update();
        assert!(
            app.world()
                .get::<DirectionalLight>(sun)
                .unwrap()
                .shadow_maps_enabled
        );
        assert!(app.world().get::<ContactShadows>(camera).is_some());
    }

    #[test]
    fn invalid_or_foreign_policy_is_rejected_before_plugin_installation() {
        let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
        let mut document = policy();
        document.presets.high.shadows.map_resolution = 3;
        assert!(
            VisualValleyShadowQualityPlugin::new(&terrain, document, VisualQualityTier::High)
                .is_err()
        );
        let mut document = policy();
        document.revision = forge_core::Revision::new(2);
        assert!(
            VisualValleyShadowQualityPlugin::new(&terrain, document, VisualQualityTier::High)
                .is_err()
        );
    }
}
