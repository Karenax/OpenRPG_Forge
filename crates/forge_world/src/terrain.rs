use forge_core::{AreaId, ProjectId, Revision};
use forge_project::{
    HeightSamplingAlgorithm, HeightSamplingParameters, TerrainDocument, TerrainDocumentError,
    TerrainExtent, TerrainMaterialLayer,
};

pub const VISUAL_VALLEY_SIZE_M: f32 = 512.0;
pub const VISUAL_VALLEY_SEED: u64 = 0x5641_4c4c_4559;
pub const VISUAL_VALLEY_TERRAIN_JSON: &str =
    include_str!("../../../examples/visual_valley/terrain.v1.json");

/// Original, asset-free recipe for the R1-001 Visual Valley terrain document.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct VisualValleyTerrainRecipe {
    pub seed: u64,
}

impl Default for VisualValleyTerrainRecipe {
    fn default() -> Self {
        Self {
            seed: VISUAL_VALLEY_SEED,
        }
    }
}

impl VisualValleyTerrainRecipe {
    pub fn build(self) -> Result<TerrainDocument, TerrainDocumentError> {
        let document = TerrainDocument::new(
            ProjectId::deterministic("visual-valley-project"),
            AreaId::deterministic("visual-valley-area"),
            Revision::INITIAL,
            "Visual Valley terrain",
            self.seed,
            TerrainExtent::new(VISUAL_VALLEY_SIZE_M, VISUAL_VALLEY_SIZE_M),
            HeightSamplingParameters {
                algorithm: HeightSamplingAlgorithm::FractalValueNoiseV1,
                sample_spacing_m: 2.0,
                base_elevation_m: 8.0,
                amplitude_m: 48.0,
                base_frequency_cycles_per_m: 1.0 / 256.0,
                octaves: 5,
                lacunarity: 2.0,
                persistence: 0.5,
            },
            vec![
                TerrainMaterialLayer::new("meadow_grass", "Meadow grass"),
                TerrainMaterialLayer::new("valley_earth", "Valley earth"),
                TerrainMaterialLayer::new("ridge_rock", "Ridge rock"),
            ],
        );
        document.validate()?;
        Ok(document)
    }
}

/// Loads and validates the exact R1-001 fixture embedded in Forge.
pub fn embedded_visual_valley_terrain_document() -> Result<TerrainDocument, TerrainDocumentError> {
    TerrainDocument::from_json(VISUAL_VALLEY_TERRAIN_JSON)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn embedded_fixture_matches_the_recipe_and_uses_512_m_extent() {
        let generated = VisualValleyTerrainRecipe::default().build().unwrap();
        let embedded = embedded_visual_valley_terrain_document().unwrap();

        assert_eq!(embedded, generated);
        assert_eq!(
            embedded.extent_m.size_x_m.to_bits(),
            VISUAL_VALLEY_SIZE_M.to_bits()
        );
        assert_eq!(
            embedded.extent_m.size_z_m.to_bits(),
            VISUAL_VALLEY_SIZE_M.to_bits()
        );
        assert_eq!(embedded.material_layers.len(), 3);
        assert_eq!(
            embedded.content_hash().unwrap(),
            generated.content_hash().unwrap()
        );
        assert_eq!(
            embedded.content_hash().unwrap().to_string(),
            "9f16780a1de1e9dd4b65931624a8b45281713829434292bada320a578420b2ef"
        );
    }

    #[test]
    fn recipe_is_deterministic_and_seed_changes_the_hash() {
        let first = VisualValleyTerrainRecipe::default().build().unwrap();
        let replay = VisualValleyTerrainRecipe::default().build().unwrap();
        let changed = VisualValleyTerrainRecipe {
            seed: VISUAL_VALLEY_SEED + 1,
        }
        .build()
        .unwrap();

        assert_eq!(first, replay);
        assert_eq!(
            first.content_hash().unwrap(),
            replay.content_hash().unwrap()
        );
        assert_ne!(
            first.content_hash().unwrap(),
            changed.content_hash().unwrap()
        );
    }

    #[test]
    fn fixture_contains_no_asset_or_upstream_identity_reference() {
        let lowercase = VISUAL_VALLEY_TERRAIN_JSON.to_ascii_lowercase();
        for forbidden in [
            "asset",
            "cadle",
            "the vale",
            "whisperwood",
            "three.js",
            "bevy",
        ] {
            assert!(!lowercase.contains(forbidden), "found `{forbidden}`");
        }
    }
}
