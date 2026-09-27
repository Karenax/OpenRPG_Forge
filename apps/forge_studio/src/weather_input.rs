//! Read-only, reload-only proof input; never a document authoring command.
use crate::{LoadedStudioScene, StudioScene};

fn requested(search: &str, scene: StudioScene) -> Result<bool, String> {
    let values: Vec<_> = search
        .trim_start_matches('?')
        .split('&')
        .filter_map(|part| {
            let (key, value) = part.split_once('=').unwrap_or((part, ""));
            (key == "forge_weather_input").then_some(value)
        })
        .collect();
    match values.as_slice() {
        [] => Ok(false),
        ["candidate"] if scene.is_visual_valley() => Ok(true),
        _ => Err("forge_weather_input exige une valeur unique candidate dans une vallée".into()),
    }
}

fn apply(scene: &mut LoadedStudioScene, bytes: &[u8]) -> Result<String, String> {
    if bytes.len() > 64 * 1024 {
        return Err("Profil météo limité à 64 KiB".into());
    }
    let LoadedStudioScene::VisualValley(valley) = scene else {
        return Err("Profil météo réservé aux vallées".into());
    };
    let json =
        std::str::from_utf8(bytes).map_err(|error| format!("Météo UTF-8 invalide : {error}"))?;
    let candidate = forge_project::WeatherProfileDocument::from_json(json)
        .map_err(|error| format!("Profil météo invalide : {error}"))?;
    candidate
        .validate_identity(
            valley.terrain.project_id,
            valley.terrain.area_id,
            valley.terrain.revision,
        )
        .map_err(|error| format!("Profil météo incompatible : {error}"))?;
    let hash = candidate
        .content_hash()
        .map_err(|error| error.to_string())?;
    // Only the already validated in-memory input changes. No file, revision or override is written.
    valley.weather = candidate;
    Ok(format!(
        "Profil météo candidat · {hash} · lecture seule, non sauvegardé"
    ))
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
pub async fn load(scene: &mut LoadedStudioScene, selected: StudioScene) -> Result<String, String> {
    let search = web_sys::window()
        .ok_or("Fenêtre absente")?
        .location()
        .search()
        .map_err(|_| "URL inaccessible")?;
    if !requested(&search, selected)? {
        return Ok("Profil météo embarqué · inchangé".into());
    }
    let bytes = crate::governed_kit::transport::fetch_weather_candidate()
        .await
        .map_err(|error| format!("Chargement météo candidat refusé : {error}"))?;
    apply(scene, &bytes)
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn request_is_opt_in_unique_and_cannot_choose_an_external_url() {
        assert!(!requested("", StudioScene::Gate0).unwrap());
        assert!(
            requested(
                "?forge_weather_input=candidate",
                StudioScene::WorldSketchVisualValley
            )
            .unwrap()
        );
        for query in [
            "?forge_weather_input",
            "?forge_weather_input=other",
            "?forge_weather_input=https://example.org",
            "?forge_weather_input=candidate&forge_weather_input=candidate",
        ] {
            assert!(requested(query, StudioScene::VisualValley).is_err());
        }
        assert!(requested("?forge_weather_input=candidate", StudioScene::Gate0).is_err());
    }
    #[test]
    fn candidate_identity_is_never_rebound_and_rejection_preserves_the_scene() {
        let mut scene = LoadedStudioScene::load(StudioScene::WorldSketchVisualValley).unwrap();
        let original = scene
            .manifest(Some(forge_project::WeatherState::Clear))
            .unwrap();
        let mut candidate = forge_world::embedded_visual_valley_weather_profile().unwrap();
        assert!(apply(&mut scene, candidate.to_pretty_json().unwrap().as_bytes()).is_err());
        assert_eq!(
            scene
                .manifest(Some(forge_project::WeatherState::Clear))
                .unwrap(),
            original
        );
        let LoadedStudioScene::VisualValley(valley) = &scene else {
            panic!("valley required");
        };
        candidate.revision = valley.terrain.revision;
        candidate.sunlight = Some(forge_project::WeatherSunlightProfile {
            rain_illuminance_multiplier: 0.65,
            transition_seconds: 4.0,
        });
        apply(&mut scene, candidate.to_pretty_json().unwrap().as_bytes()).unwrap();
        let after = scene
            .manifest(Some(forge_project::WeatherState::Clear))
            .unwrap();
        assert_ne!(after.source_hashes.weather, original.source_hashes.weather);
        assert_eq!(after.source_hashes.terrain, original.source_hashes.terrain);
        assert_eq!(
            after.source_hashes.environment,
            original.source_hashes.environment
        );
        for invalid in [b"{}".to_vec(), vec![0xff], vec![b' '; 65537]] {
            assert!(apply(&mut scene, &invalid).is_err());
            assert_eq!(
                scene
                    .manifest(Some(forge_project::WeatherState::Clear))
                    .unwrap(),
                after
            );
        }
    }
}
