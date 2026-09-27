//! Explicit preview policy, never an authored preset replacement.
use forge_project::VisualValleyQualityProfileDocument;

pub const HREF: &str = "?forge_scene=world-sketch-visual-valley&forge_kit=governed&forge_forest=batches&forge_shadows=balanced-preview";
pub const LABEL: &str = "Candidat ombres High : 2048 / 3 cascades · portée et contact conservés · non qualifié · sans sauvegarde";

pub fn requested(search: &str, scene: crate::StudioScene) -> Result<bool, String> {
    let values: Vec<_> = search
        .trim_start_matches('?')
        .split('&')
        .filter_map(|part| {
            let (key, value) = part.split_once('=').unwrap_or((part, ""));
            (key == "forge_shadows").then_some(value)
        })
        .collect();
    match values.as_slice() {
        [] => Ok(false),
        ["balanced-preview"] if scene.is_visual_valley() => Ok(true),
        _ => Err("Ombres : balanced-preview unique et réservé aux vallées".into()),
    }
}

pub fn policy(
    source: &VisualValleyQualityProfileDocument,
    enabled: bool,
) -> Result<VisualValleyQualityProfileDocument, String> {
    source.validate().map_err(|error| error.to_string())?;
    let mut candidate = source.clone();
    if enabled {
        candidate.presets.high.shadows.map_resolution = 2048;
        candidate.presets.high.shadows.cascade_count = 3;
    }
    candidate.validate().map_err(|error| error.to_string())?;
    Ok(candidate)
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
pub fn from_location(scene: crate::StudioScene) -> Result<bool, String> {
    let search = web_sys::window()
        .ok_or("Fenêtre du Studio absente")?
        .location()
        .search()
        .map_err(|_| "URL inaccessible")?;
    requested(&search, scene)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn request_is_explicit_unique_and_valley_only() {
        let scene = crate::StudioScene::WorldSketchVisualValley;
        assert!(!requested("", scene).unwrap());
        assert!(requested(HREF, scene).unwrap());
        assert_eq!(crate::parse_studio_scene_selection(HREF).scene, scene);
        assert!(requested(HREF, crate::StudioScene::Gate0).is_err());
        for query in [
            "?forge_shadows",
            "?forge_shadows=high",
            "?forge_shadows=balanced-preview&forge_shadows=balanced-preview",
        ] {
            assert!(requested(query, scene).is_err());
        }
        assert!(LABEL.contains("non qualifié"));
    }

    #[test]
    fn candidate_changes_only_two_high_shadow_fields_and_preserves_source() {
        let source = VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        let original_hash = source.content_hash().unwrap();
        assert_eq!(policy(&source, false).unwrap(), source);
        let mut balanced = policy(&source, true).unwrap();
        assert_eq!(balanced.presets.high.shadows.map_resolution, 2048);
        assert_eq!(balanced.presets.high.shadows.cascade_count, 3);
        assert_ne!(balanced.content_hash().unwrap(), original_hash);
        balanced.presets.high.shadows = source.presets.high.shadows;
        assert_eq!(balanced, source);
        assert_eq!(source.content_hash().unwrap(), original_hash);
    }
}
