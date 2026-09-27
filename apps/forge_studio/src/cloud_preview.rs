//! Explicit read-only sky candidate; not a persisted environment or quality preset.
pub const PREVIEW_HREF: &str = "?forge_scene=world-sketch-visual-valley&forge_clouds=preview";
/// Integrated preview, separate from the isolated sky comparison above.
pub const INTEGRATED_PREVIEW_HREF: &str = "?forge_scene=world-sketch-visual-valley&forge_kit=governed&forge_forest=batches&forge_water=clear-reflections-preview&forge_clouds=preview";

fn settings(
    search: &str,
    valley: bool,
) -> Result<Option<forge_project::CloudLayerSettings>, String> {
    let mut requested = None;
    for part in search.trim_start_matches('?').split('&') {
        let (name, value) = part.split_once('=').unwrap_or((part, ""));
        if name == "forge_clouds" && requested.replace(value).is_some() {
            return Err("Paramètre forge_clouds répété".into());
        }
    }
    match requested {
        None => Ok(None),
        Some("preview") if valley => {
            let settings: forge_project::CloudLayerSettings = serde_json::from_str(include_str!(
                "../../../examples/visual_valley/cloud-layer-preview.json"
            ))
            .map_err(|error| error.to_string())?;
            settings.validate().map_err(|error| error.to_string())?;
            Ok(Some(settings))
        }
        _ => Err("Le ciel expérimental requiert une vallée et forge_clouds=preview".into()),
    }
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
pub fn install(app: &mut bevy::prelude::App, valley: bool) -> Result<(), String> {
    let search = web_sys::window()
        .ok_or("Fenêtre absente")?
        .location()
        .search()
        .map_err(|error| format!("{error:?}"))?;
    if let Some(settings) = settings(&search, valley)? {
        app.add_plugins(
            forge_engine::CloudLayerPreviewPlugin::new(settings)
                .map_err(|error| error.to_string())?,
        )
        .add_systems(bevy::prelude::Last, publish);
    }
    Ok(())
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameter")]
fn publish(observation: bevy::prelude::Res<forge_engine::CloudPreviewObservation>) {
    let Some(document) = web_sys::window().and_then(|window| window.document()) else {
        return;
    };
    if let Some(element) = document.get_element_by_id("studio-cloud-preview-status") {
        element.set_text_content(Some(&format!(
            "Ciel expérimental · {} · rendu final non qualifié",
            observation.status
        )));
    }
    if let Some(element) = document.get_element_by_id("studio-cloud-preview-ledger") {
        match serde_json::to_string(&*observation) {
            Ok(json) => element.set_text_content(Some(&json)),
            Err(error) => {
                element.set_text_content(None);
                bevy::log::error!("Cloud diagnostic serialization failed: {error}");
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn integrated_candidate_keeps_governed_forest_and_the_same_cloud_settings() {
        assert_eq!(
            crate::parse_studio_scene_selection(INTEGRATED_PREVIEW_HREF).scene,
            crate::StudioScene::WorldSketchVisualValley
        );
        assert_eq!(
            crate::governed_kit_request::parse(INTEGRATED_PREVIEW_HREF).unwrap(),
            crate::governed_kit_request::KitRequest::ForestBatches
        );
        assert_eq!(
            settings(INTEGRATED_PREVIEW_HREF, true).unwrap(),
            settings(PREVIEW_HREF, true).unwrap()
        );
    }

    #[test]
    fn preview_is_opt_in_bounded_and_rejects_ambiguous_requests() {
        assert!(settings("", true).unwrap().is_none());
        assert!(settings("?unrelated=preview", false).unwrap().is_none());
        let candidate = settings(PREVIEW_HREF, true).unwrap().unwrap();
        candidate.validate().unwrap();
        assert_eq!(candidate.seed, 1337);
        assert!(settings(PREVIEW_HREF, false).is_err());
        for search in [
            "?forge_clouds",
            "?forge_clouds=true",
            "?forge_clouds=preview&forge_clouds=preview",
        ] {
            assert!(settings(search, true).is_err());
        }
        assert_eq!(
            crate::parse_studio_scene_selection(PREVIEW_HREF).scene,
            crate::StudioScene::WorldSketchVisualValley
        );
    }
}
