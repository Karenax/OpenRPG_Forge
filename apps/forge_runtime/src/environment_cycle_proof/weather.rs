//! Native transition observer; requests use the same runtime state API as Studio.
use super::{Cycle, Domain};
use bevy::prelude::*;
use forge_engine::{
    EnvironmentProjectionStats, WeatherProjectionStats, WeatherProjectionStatus,
    WeatherRuntimeState,
};
use forge_project::WeatherState;

#[derive(Default, Resource)]
pub(super) struct Observation {
    pub source_hash: String,
    pub matching: bool,
    intermediate: [bool; 2],
    restored_while_wet: bool,
    baseline_lux: Option<f32>,
}

pub fn weather_domain(
    enabled: bool,
    document: &crate::RuntimeDocument,
) -> Result<Option<Domain>, String> {
    if !enabled {
        return Ok(None);
    }
    let crate::RuntimeDocument::VisualValley {
        weather: Some(weather),
        ..
    } = document
    else {
        return Err("weather cycle requires a weather document".into());
    };
    let Some(settings) = weather.sunlight else {
        return Err("weather cycle requires optional sunlight settings".into());
    };
    if weather.initial_state != WeatherState::Clear || settings.rain_illuminance_multiplier >= 1.0 {
        return Err(
            "weather cycle requires initial clear and a rain sunlight multiplier below one".into(),
        );
    }
    Ok(Some(Domain::Weather(settings)))
}

pub(super) fn install(app: &mut App) {
    app.init_resource::<Observation>();
    app.add_systems(Last, observe.before(super::advance));
}

#[allow(clippy::needless_pass_by_value)]
fn observe(
    cycle: Res<Cycle>,
    mut proof: ResMut<Observation>,
    state: Option<ResMut<WeatherRuntimeState>>,
    weather: Option<Res<WeatherProjectionStats>>,
    environment: Option<Res<EnvironmentProjectionStats>>,
) {
    if cycle.terminal {
        return;
    }
    proof.matching = false;
    let Domain::Weather(settings) = cycle.domain else {
        return;
    };
    let (Some(mut state), Some(weather), Some(environment)) = (state, weather, environment) else {
        return;
    };
    let target = if cycle.stage == 1 {
        WeatherState::Rain
    } else {
        WeatherState::Clear
    };
    state.request_state(target);
    // Include both immutable sources in the shared harness's source-change guard.
    proof.source_hash = format!("{}:{}", weather.source_hash, environment.source_hash);
    let response = &environment.sun.weather_response;
    let Some(blend) = response.rain_blend else {
        return;
    };
    if response.status != "applied_cpu"
        || response.weather_source_hash.as_ref() != Some(&weather.source_hash)
        || weather.status != WeatherProjectionStatus::ObservedCpuProjection
        || weather.target_state != target
        || !weather.wetness.terrain_uniforms_match
        || !weather.wetness.vegetation_uniforms_match
    {
        return;
    }
    let (Some(authored), Some(lux)) = (
        response.authored_illuminance_lux,
        environment.sun.illuminance_lux,
    ) else {
        return;
    };
    if let Some(baseline) = proof.baseline_lux {
        if baseline.to_bits() != authored.to_bits() {
            return;
        }
    } else {
        proof.baseline_lux = Some(authored);
    }
    let Ok(expected) = settings.illuminance_multiplier(blend) else {
        return;
    };
    if response.illuminance_multiplier.map(f32::to_bits) != Some(expected.to_bits())
        || lux.to_bits() != (authored * expected).to_bits()
    {
        return;
    }
    if cycle.stage > 0 && blend > 0.0 && blend < 1.0 && !proof.intermediate[cycle.stage - 1] {
        proof.intermediate[cycle.stage - 1] = true;
        info!(target: "forge::proof", event_kind = "weather_cycle_intermediate", stage = cycle.stage,
            blend, lux, "progressive sunlight transition observed");
    }
    if cycle.stage == 2
        && blend.to_bits() == 0.0_f32.to_bits()
        && weather.wetness.current > weather.wetness.target
        && !proof.restored_while_wet
    {
        proof.restored_while_wet = true;
        info!(target: "forge::proof", event_kind = "weather_cycle_sun_restored_while_wet",
            lux, wetness = weather.wetness.current,
            "authored sunlight restored before drying completes");
    }
    let endpoint: f32 = if target == WeatherState::Rain {
        1.0
    } else {
        0.0
    };
    proof.matching = blend.to_bits() == endpoint.to_bits()
        && weather.wetness.current.to_bits() == weather.wetness.target.to_bits()
        && weather.rain.visible == (target == WeatherState::Rain)
        && (cycle.stage != 1 || proof.intermediate[0])
        && (cycle.stage != 2
            || (proof.intermediate.iter().all(|seen| *seen) && proof.restored_while_wet));
    if proof.matching && cycle.stable == 0 {
        info!(target: "forge::proof", event_kind = "weather_cycle_endpoint", stage = cycle.stage,
            lux, wetness = weather.wetness.current,
            source_hash = %proof.source_hash, "weather endpoint observed; waiting for capture");
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn cycle_requires_explicit_candidate_and_excludes_conflicting_automation() {
        let base = [
            "--world-sketch-visual-valley",
            "--weather-input",
            "weather.json",
            "--weather-cycle-proof",
            "fresh",
        ];
        assert!(crate::Options::parse(base.into_iter().map(str::to_owned)).is_ok());
        for extra in [
            vec!["--weather", "rain"],
            vec!["--exit-after-frames", "10"],
            vec!["--quality", "high", "--adaptive-quality"],
            vec!["--player-camera"],
            vec!["--weather-cycle-proof", "duplicate"],
            vec!["--quality", "high", "--quality-cycle-proof", "other"],
        ] {
            assert!(
                crate::Options::parse(base.iter().copied().chain(extra).map(str::to_owned))
                    .is_err()
            );
        }
        assert!(
            crate::Options::parse(
                ["--visual-valley", "--weather-cycle-proof", "fresh"]
                    .into_iter()
                    .map(str::to_owned)
            )
            .is_err()
        );
    }

    #[test]
    fn cycle_requires_clear_opt_in_and_has_three_stages() {
        let mut document = crate::Options {
            visual_valley: true,
            ..default()
        }
        .load_document()
        .unwrap();
        assert!(weather_domain(true, &document).is_err());
        assert!(weather_domain(false, &document).unwrap().is_none());
        let crate::RuntimeDocument::VisualValley {
            weather: Some(profile),
            ..
        } = &mut document
        else {
            panic!("expected weather");
        };
        profile.sunlight = Some(forge_project::WeatherSunlightProfile {
            rain_illuminance_multiplier: 0.65,
            transition_seconds: 4.0,
        });
        assert_eq!(
            weather_domain(true, &document).unwrap().unwrap().stages(),
            3
        );
        let crate::RuntimeDocument::VisualValley {
            weather: Some(profile),
            ..
        } = &mut document
        else {
            panic!("expected weather");
        };
        profile.initial_state = WeatherState::Rain;
        assert!(weather_domain(true, &document).is_err());
    }
}
