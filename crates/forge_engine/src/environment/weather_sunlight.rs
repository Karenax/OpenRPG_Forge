//! Weather owns a lighting blend; this adapter owns only the matching authored sun's illuminance.
use super::{VisualValleyEnvironmentProfile, VisualValleySun};
use crate::weather::{VisualValleyWeatherProfile, WeatherRuntimeState};
use bevy::prelude::*;
use serde::Serialize;

#[derive(Clone, Debug, PartialEq, Resource, Serialize)]
pub struct WeatherSunlightObservation {
    pub status: &'static str,
    pub weather_source_hash: Option<String>,
    pub rain_blend: Option<f32>,
    pub illuminance_multiplier: Option<f32>,
    pub authored_illuminance_lux: Option<f32>,
    pub effective_illuminance_lux: Option<f32>,
}

impl Default for WeatherSunlightObservation {
    fn default() -> Self {
        Self {
            status: "disabled",
            weather_source_hash: None,
            rain_blend: None,
            illuminance_multiplier: None,
            authored_illuminance_lux: None,
            effective_illuminance_lux: None,
        }
    }
}

#[derive(Component)]
pub(super) struct AppliedSunlight {
    authored_illuminance_lux: f32,
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
pub(super) fn project(
    mut commands: Commands,
    environment: Option<Res<VisualValleyEnvironmentProfile>>,
    weather: Option<Res<VisualValleyWeatherProfile>>,
    state: Option<Res<WeatherRuntimeState>>,
    mut suns: Query<
        (Entity, &mut DirectionalLight, Option<&mut AppliedSunlight>),
        With<VisualValleySun>,
    >,
    mut observation: ResMut<WeatherSunlightObservation>,
) {
    let mut next = WeatherSunlightObservation::default();
    let requested = weather
        .as_ref()
        .and_then(|weather| weather.document.sunlight);
    let mut projection = None;
    if let Some(settings) = requested {
        next.weather_source_hash = weather.as_ref().map(|weather| weather.source_hash.clone());
        next.status = "source_or_state_unavailable";
        if let (Some(environment), Some(weather), Some(state)) = (&environment, &weather, &state) {
            let source = &environment.document;
            next.status = "identity_mismatch";
            if weather
                .document
                .validate_identity(source.project_id, source.area_id, source.revision)
                .is_ok()
            {
                next.status = "invalid_lighting_response";
                if let Ok(multiplier) = settings.illuminance_multiplier(state.sunlight_blend) {
                    next.status = "invalid_sun_topology";
                    if suns.iter().count() == 1 {
                        next.status = "applied_cpu";
                        next.rain_blend = Some(state.sunlight_blend);
                        next.illuminance_multiplier = Some(multiplier);
                        next.authored_illuminance_lux = Some(source.sun.illuminance_lux);
                        projection = Some((source.sun.illuminance_lux, multiplier));
                    }
                }
            }
        }
    }
    for (entity, mut light, owned) in &mut suns {
        if let Some((authored, multiplier)) = projection {
            let effective = authored * multiplier;
            if light.illuminance.to_bits() != effective.to_bits() {
                light.illuminance = effective;
            }
            next.effective_illuminance_lux = Some(light.illuminance);
            if let Some(mut owned) = owned {
                owned.authored_illuminance_lux = authored;
            } else {
                commands.entity(entity).insert(AppliedSunlight {
                    authored_illuminance_lux: authored,
                });
            }
        } else if let Some(owned) = owned {
            // Release only a light previously controlled here, including after invalidation.
            light.illuminance = owned.authored_illuminance_lux;
            commands.entity(entity).remove::<AppliedSunlight>();
        }
    }
    *observation = next;
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::weather::advance_runtime;
    use forge_project::{WeatherState, WeatherSunlightProfile};

    fn proof_app(enabled: bool) -> (App, Entity) {
        let environment = forge_world::embedded_visual_valley_environment_profile().unwrap();
        let mut weather = forge_world::embedded_visual_valley_weather_profile().unwrap();
        if enabled {
            weather.sunlight = Some(WeatherSunlightProfile {
                rain_illuminance_multiplier: 0.25,
                transition_seconds: 4.0,
            });
        }
        let state = WeatherRuntimeState::at_state(&weather, WeatherState::Rain);
        let mut app = App::new();
        app.insert_resource(VisualValleyEnvironmentProfile {
            source_hash: environment.content_hash().unwrap().to_string(),
            document: environment.clone(),
        })
        .insert_resource(VisualValleyWeatherProfile {
            source_hash: weather.content_hash().unwrap().to_string(),
            document: weather,
        })
        .insert_resource(state)
        .init_resource::<WeatherSunlightObservation>()
        .add_systems(PostUpdate, project);
        let entity = app
            .world_mut()
            .spawn((
                VisualValleySun,
                DirectionalLight {
                    illuminance: environment.sun.illuminance_lux,
                    ..default()
                },
            ))
            .id();
        (app, entity)
    }

    fn lux(app: &App, entity: Entity) -> f32 {
        app.world()
            .get::<DirectionalLight>(entity)
            .unwrap()
            .illuminance
    }

    #[test]
    fn rain_clear_restores_the_same_sun_without_accumulation_or_source_edits() {
        let (mut app, sun) = proof_app(true);
        let authored = lux(&app, sun);
        let environment_hash = app
            .world()
            .resource::<VisualValleyEnvironmentProfile>()
            .source_hash
            .clone();
        let weather = app
            .world()
            .resource::<VisualValleyWeatherProfile>()
            .document
            .clone();
        for _ in 0..4 {
            app.update();
        }
        assert_eq!(lux(&app, sun).to_bits(), (authored * 0.25).to_bits());
        {
            let mut state = app.world_mut().resource_mut::<WeatherRuntimeState>();
            state.request_state(WeatherState::Clear);
            advance_runtime(&weather, &mut state, 4.0);
            assert!(state.current_wetness() > 0.0);
        }
        app.update();
        assert_eq!(lux(&app, sun).to_bits(), authored.to_bits());
        let observation = app.world().resource::<WeatherSunlightObservation>();
        assert_eq!(observation.status, "applied_cpu");
        assert_eq!(observation.effective_illuminance_lux, Some(authored));
        assert_eq!(
            app.world()
                .resource::<VisualValleyEnvironmentProfile>()
                .document
                .content_hash()
                .unwrap()
                .to_string(),
            environment_hash
        );
        assert_eq!(
            app.world()
                .resource::<VisualValleyWeatherProfile>()
                .document,
            weather
        );
    }

    #[test]
    fn missing_weather_and_identity_mismatch_release_owned_illumination() {
        let (mut app, sun) = proof_app(true);
        let authored = lux(&app, sun);
        app.update();
        let mut weather = app
            .world_mut()
            .remove_resource::<VisualValleyWeatherProfile>()
            .unwrap();
        app.update();
        assert_eq!(lux(&app, sun).to_bits(), authored.to_bits());
        assert!(app.world().get::<AppliedSunlight>(sun).is_none());
        weather.document.area_id = forge_core::AreaId::deterministic("different-area");
        app.insert_resource(weather);
        app.update();
        assert_eq!(
            app.world().resource::<WeatherSunlightObservation>().status,
            "identity_mismatch"
        );
        assert_eq!(lux(&app, sun).to_bits(), authored.to_bits());
    }

    #[test]
    fn removing_state_or_disabling_the_option_restores_only_the_owned_sun() {
        let (mut app, sun) = proof_app(true);
        let authored = lux(&app, sun);
        app.update();
        let state = app
            .world_mut()
            .remove_resource::<WeatherRuntimeState>()
            .unwrap();
        app.update();
        assert_eq!(lux(&app, sun).to_bits(), authored.to_bits());
        assert_eq!(
            app.world().resource::<WeatherSunlightObservation>().status,
            "source_or_state_unavailable"
        );
        app.insert_resource(state);
        app.update();
        assert_eq!(lux(&app, sun).to_bits(), (authored * 0.25).to_bits());
        app.world_mut()
            .resource_mut::<VisualValleyWeatherProfile>()
            .document
            .sunlight = None;
        app.update();
        assert_eq!(lux(&app, sun).to_bits(), authored.to_bits());
        assert_eq!(
            app.world().resource::<WeatherSunlightObservation>().status,
            "disabled"
        );
        assert!(app.world().get::<AppliedSunlight>(sun).is_none());
    }

    #[test]
    fn absent_option_leaves_unowned_lights_unchanged_and_ambiguous_suns_are_refused() {
        let (mut app, sun) = proof_app(false);
        app.world_mut()
            .get_mut::<DirectionalLight>(sun)
            .unwrap()
            .illuminance = 123.0;
        app.update();
        assert_eq!(lux(&app, sun).to_bits(), 123.0_f32.to_bits());
        assert_eq!(
            app.world().resource::<WeatherSunlightObservation>().status,
            "disabled"
        );
        let (mut app, sun) = proof_app(true);
        let authored = lux(&app, sun);
        app.update();
        let extra = app
            .world_mut()
            .spawn((
                VisualValleySun,
                DirectionalLight {
                    illuminance: 987.0,
                    ..default()
                },
            ))
            .id();
        app.update();
        assert_eq!(
            app.world().resource::<WeatherSunlightObservation>().status,
            "invalid_sun_topology"
        );
        assert_eq!(lux(&app, sun).to_bits(), authored.to_bits());
        assert_eq!(lux(&app, extra).to_bits(), 987.0_f32.to_bits());
    }
}
