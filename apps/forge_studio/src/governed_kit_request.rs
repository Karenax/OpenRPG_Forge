//! Strict opt-in parsing, independent of browser and renderer.
pub const FOREST_PREVIEW_HREF: &str =
    "?forge_scene=world-sketch-visual-valley&forge_kit=governed&forge_forest=preview";

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum KitRequest {
    None,
    Manual,
    ForestPreview,
    ForestBatches,
}

pub fn parse(search: &str) -> Result<KitRequest, String> {
    let mut kit = None;
    let mut forest = None;
    for part in search.trim_start_matches('?').split('&') {
        let (name, value) = part.split_once('=').unwrap_or((part, ""));
        let slot = match name {
            "forge_kit" => &mut kit,
            "forge_forest" => &mut forest,
            _ => continue,
        };
        if slot.replace(value).is_some() {
            return Err(format!("Paramètre {name} répété"));
        }
    }
    match (kit, forest) {
        (None, None) => Ok(KitRequest::None),
        (Some("governed"), None) => Ok(KitRequest::Manual),
        (Some("governed"), Some("preview")) => Ok(KitRequest::ForestPreview),
        (Some("governed"), Some("batches")) => Ok(KitRequest::ForestBatches),
        _ => Err("Aperçu : utiliser forge_kit=governed et, en option, forge_forest=preview ou batches (expérimental)".into()),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn preview_is_explicit_and_requires_unique_governed_kit() {
        assert_eq!(
            parse(FOREST_PREVIEW_HREF).unwrap(),
            KitRequest::ForestPreview
        );
        assert_eq!(
            crate::parse_studio_scene_selection(FOREST_PREVIEW_HREF).scene,
            crate::StudioScene::WorldSketchVisualValley
        );
        assert_eq!(parse("?scene=valley").unwrap(), KitRequest::None);
        assert_eq!(parse("?forge_kit=governed").unwrap(), KitRequest::Manual);
        assert_eq!(
            parse("?forge_kit=governed&forge_forest=batches").unwrap(),
            KitRequest::ForestBatches
        );
        assert!(parse("?forge_forest=batches").is_err());
        assert_eq!(
            parse("?forge_forest=preview&forge_kit=governed").unwrap(),
            KitRequest::ForestPreview
        );
        for query in [
            "?forge_forest=preview",
            "?forge_kit",
            "?forge_kit=governed&forge_forest=",
            "?forge_kit=governed&forge_kit=governed",
            "?forge_kit=governed&forge_forest=preview&forge_forest=preview",
            "?forge_kit=other",
            "?forge_kit=governed&forge_forest=true",
        ] {
            assert!(parse(query).is_err(), "{query}");
        }
    }
}
