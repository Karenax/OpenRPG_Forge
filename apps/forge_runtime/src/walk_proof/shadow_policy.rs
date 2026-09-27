use forge_project::{TerrainDocument, VisualValleyQualityProfileDocument};
use std::path::Path;

pub fn load(
    terrain: &TerrainDocument,
    path: Option<&Path>,
) -> Result<VisualValleyQualityProfileDocument, String> {
    let Some(path) = path else {
        return crate::area_quality_policy(terrain);
    };
    let json = std::fs::read_to_string(path)
        .map_err(|error| format!("shadow profile {}: {error}", path.display()))?;
    parse(terrain, &json)
}

fn parse(
    terrain: &TerrainDocument,
    json: &str,
) -> Result<VisualValleyQualityProfileDocument, String> {
    let policy = VisualValleyQualityProfileDocument::from_json(json)
        .map_err(|error| format!("shadow profile: {error}"))?;
    policy
        .validate_identity(terrain.project_id, terrain.area_id, terrain.revision)
        .map_err(|error| format!("shadow profile identity: {error}"))?;
    Ok(policy)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn profile_input_is_validated_without_rebinding_identity_or_revision() {
        let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
        let original = load(&terrain, None).unwrap();
        let json = serde_json::to_string(&original).unwrap();
        assert_eq!(parse(&terrain, &json).unwrap(), original);
        let mut wrong = original.clone();
        wrong.revision = wrong.revision.next().unwrap();
        assert!(parse(&terrain, &serde_json::to_string(&wrong).unwrap()).is_err());
        let mut invalid = original;
        invalid.presets.high.shadows.map_resolution = 0;
        assert!(parse(&terrain, &serde_json::to_string(&invalid).unwrap()).is_err());
        assert!(parse(&terrain, "{}").is_err());
    }
}
