//! Reload-only water comparison, never a source mutation or quality promotion.
pub const PREVIEW_HREF: &str = "?forge_scene=world-sketch-visual-valley&forge_kit=governed&forge_forest=batches&forge_water=depth-preview";
pub const MEDIUM_HREF: &str = "?forge_scene=world-sketch-visual-valley&forge_kit=governed&forge_forest=batches&forge_water=clear-medium-preview";
pub const REFLECTION_HREF: &str = "?forge_scene=world-sketch-visual-valley&forge_kit=governed&forge_forest=batches&forge_water=clear-reflections-preview";

pub fn refraction_status(steps: Option<f32>, path: Option<f32>, matched: bool) -> String {
    let mode = match steps {
        Some(value) if value > 0.0 => format!(
            "rayon · {value:.0} pas / {} m",
            path.map_or_else(|| "?".into(), |value| format!("{value:.2}"))
        ),
        Some(_) => "sans rayon · transmission approchée conservée".into(),
        None => "matériau non observé".into(),
    };
    format!(
        "Réfraction expérimentale · {mode} · {} · observation CPU, pas qualification GPU",
        if matched {
            "réglage projeté"
        } else {
            "application non confirmée"
        }
    )
}

pub fn forward_status(
    steps: Option<f32>,
    distance: Option<f32>,
    reflectance: Option<f32>,
    matched: bool,
) -> String {
    format!(
        "Reflets transmissifs expérimentaux · {} pas / {} m · réflectance {} · {} · observation CPU, pas qualification GPU",
        steps.map_or_else(|| "?".into(), |value| format!("{value:.0}")),
        distance.map_or_else(|| "?".into(), |value| format!("{value:.0}")),
        reflectance.map_or_else(|| "?".into(), |value| format!("{value:.3}")),
        if matched {
            "réglage projeté"
        } else {
            "application non confirmée"
        }
    )
}

pub fn reflection_requested(search: &str, scene: crate::StudioScene) -> Result<bool, String> {
    requested(search, scene)?;
    Ok(search
        .trim_start_matches('?')
        .split('&')
        .any(|part| part == "forge_water=clear-reflections-preview"))
}

pub fn unprofiled_status(optical_preview: bool) -> &'static str {
    if optical_preview {
        "Milieu optique épinglé · teinte historique non pilotable · rendu non qualifié"
    } else {
        "Matériau d’eau non observé"
    }
}

pub fn requested(search: &str, scene: crate::StudioScene) -> Result<bool, String> {
    let values: Vec<_> = search
        .trim_start_matches('?')
        .split('&')
        .filter_map(|part| {
            let (key, value) = part.split_once('=').unwrap_or((part, ""));
            (key == "forge_water").then_some(value)
        })
        .collect();
    match values.as_slice() {
        [] => Ok(false),
        ["depth-preview" | "clear-medium-preview" | "clear-reflections-preview"] if scene.is_visual_valley() => Ok(true),
        _ => Err("forge_water exige depth-preview, clear-medium-preview ou clear-reflections-preview, une seule fois et dans une vallée".into()),
    }
}

fn apply(
    search: &str,
    selected: crate::StudioScene,
    scene: &mut crate::LoadedStudioScene,
) -> Result<(), String> {
    requested(search, selected)?;
    if !search.trim_start_matches('?').split('&').any(|part| {
        matches!(
            part,
            "forge_water=clear-medium-preview" | "forge_water=clear-reflections-preview"
        )
    }) {
        return Ok(());
    }
    let crate::LoadedStudioScene::VisualValley(valley) = scene else {
        return Err("Milieu optique réservé aux vallées".into());
    };
    let medium: forge_project::WaterMediumProfile = serde_json::from_str(include_str!(
        "../../../examples/water/clear-medium.preview.json"
    ))
    .map_err(|error| format!("Profil optique invalide : {error}"))?;
    medium.validate().map_err(|error| error.to_string())?;
    // Apply before publishing the manifest: its water hash must identify the actual input.
    // No terrain, revision, palette, file or saved authoring document is rewritten.
    valley.water.medium = Some(medium);
    Ok(())
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
pub fn load(
    scene: &mut crate::LoadedStudioScene,
    selected: crate::StudioScene,
) -> Result<(), String> {
    let search = web_sys::window()
        .ok_or("Fenêtre absente")?
        .location()
        .search()
        .map_err(|_| "URL inaccessible")?;
    apply(&search, selected, scene)
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
pub fn install(app: &mut bevy::prelude::App, scene: crate::StudioScene) -> Result<(), String> {
    let window = web_sys::window().ok_or("Fenêtre du Studio absente")?;
    let search = window
        .location()
        .search()
        .map_err(|error| format!("{error:?}"))?;
    let enabled = requested(&search, scene)?;
    let reflections = reflection_requested(&search, scene)?;
    if enabled {
        app.insert_resource(forge_engine::WaterDepthOpticsPreview);
    }
    if reflections {
        app.insert_resource(forge_engine::WaterReflectionOpticsPreview);
    }
    if let Some(status) = window
        .document()
        .and_then(|doc| doc.get_element_by_id("studio-water-optics-status"))
    {
        status.set_text_content(Some(if reflections {
            "Candidat eau claire et reflets du décor demandé · budgets de réflexion pilotables · profils globaux non qualifiés · sans sauvegarde"
        } else if search.split('&').any(|part| part.trim_start_matches('?') == "forge_water=clear-medium-preview") {
            "Candidat eau claire RGB demandé · absorption/diffusion expérimentales · profils et SSR non qualifiés · sans sauvegarde"
        } else if enabled {
            "Candidat eau demandé · transmission/profondeur expérimentales · reflets d’environnement seuls, SSR et profils non qualifiés · sans sauvegarde"
        } else {
            "Eau par défaut · candidat optique non demandé"
        }));
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn experimental_candidate_is_not_reported_as_missing_or_qualified() {
        assert!(unprofiled_status(true).contains("épinglé"));
        assert!(unprofiled_status(true).contains("non qualifié"));
        assert!(unprofiled_status(false).contains("non observé"));
    }

    #[test]
    fn water_candidate_is_explicit_unique_and_valley_only() {
        let valley = crate::StudioScene::WorldSketchVisualValley;
        assert!(!requested("", valley).unwrap());
        assert!(requested(PREVIEW_HREF, valley).unwrap());
        assert!(requested(MEDIUM_HREF, valley).unwrap());
        assert!(reflection_requested(REFLECTION_HREF, valley).unwrap());
        assert!(
            reflection_requested(crate::cloud_preview::INTEGRATED_PREVIEW_HREF, valley).unwrap()
        );
        assert!(!reflection_requested(MEDIUM_HREF, valley).unwrap());
        assert!(reflection_requested(REFLECTION_HREF, crate::StudioScene::Gate0).is_err());
        assert_eq!(
            crate::parse_studio_scene_selection(PREVIEW_HREF).scene,
            valley
        );
        assert!(requested(PREVIEW_HREF, crate::StudioScene::Gate0).is_err());
        for search in [
            "?forge_water",
            "?forge_water=true",
            "?forge_water=depth-preview&forge_water=depth-preview",
            "?forge_water=clear-reflections-preview&forge_water=depth-preview",
        ] {
            assert!(requested(search, valley).is_err(), "{search}");
        }
    }

    #[test]
    fn medium_candidate_changes_only_loaded_water_and_manifest_hash() {
        let selected = crate::StudioScene::WorldSketchVisualValley;
        let mut scene = crate::LoadedStudioScene::load(selected).unwrap();
        let original = scene
            .manifest(Some(forge_project::WeatherState::Clear))
            .unwrap();
        apply(PREVIEW_HREF, selected, &mut scene).unwrap();
        assert_eq!(
            scene
                .manifest(Some(forge_project::WeatherState::Clear))
                .unwrap(),
            original
        );
        apply(MEDIUM_HREF, selected, &mut scene).unwrap();
        let medium_manifest = scene
            .manifest(Some(forge_project::WeatherState::Clear))
            .unwrap();
        apply(REFLECTION_HREF, selected, &mut scene).unwrap();
        apply(
            crate::cloud_preview::INTEGRATED_PREVIEW_HREF,
            selected,
            &mut scene,
        )
        .unwrap();
        assert_eq!(
            scene
                .manifest(Some(forge_project::WeatherState::Clear))
                .unwrap(),
            medium_manifest
        );
        assert_ne!(
            scene
                .manifest(Some(forge_project::WeatherState::Clear))
                .unwrap(),
            original
        );
        let crate::LoadedStudioScene::VisualValley(valley) = &mut scene else {
            panic!("missing valley")
        };
        assert_eq!(
            valley
                .water
                .medium
                .unwrap()
                .scattering_per_m
                .map(f32::to_bits),
            [0.005_f32.to_bits(); 3]
        );
        valley.water.medium = None;
        assert_eq!(
            scene
                .manifest(Some(forge_project::WeatherState::Clear))
                .unwrap(),
            original
        );
        assert!(
            apply(
                "?forge_water=clear-medium-preview&forge_water=depth-preview",
                selected,
                &mut scene
            )
            .is_err()
        );
        assert_eq!(
            scene
                .manifest(Some(forge_project::WeatherState::Clear))
                .unwrap(),
            original
        );
    }

    #[test]
    fn forward_status_distinguishes_projection_from_qualification() {
        let status = forward_status(Some(48.0), Some(96.0), Some(0.36), true);
        assert!(status.contains("48 pas / 96 m"));
        assert!(status.contains("pas qualification GPU"));
        assert!(forward_status(None, None, None, false).contains("application non confirmée"));
    }

    #[test]
    fn refraction_labels_distinguish_ray_fallback_and_missing_observation() {
        assert!(refraction_status(Some(24.0), Some(3.2), true).contains("24 pas / 3.20 m"));
        let fallback = refraction_status(Some(0.0), Some(0.0), true);
        assert!(fallback.contains("transmission approchée conservée"));
        assert!(fallback.contains("pas qualification GPU"));
        assert!(refraction_status(None, None, false).contains("application non confirmée"));
    }
}
