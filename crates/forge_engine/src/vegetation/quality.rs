use bevy::prelude::Resource;
use forge_project::{
    TerrainDocument, VegetationPresentationProfile, VisualQualityTier,
    VisualValleyQualityProfileDocument, VisualValleyQualityProfileError,
};
use serde::Serialize;
#[cfg(test)]
mod path_tests;
#[cfg(test)]
mod tests;

mod transition;
#[cfg(feature = "physics-rapier")]
pub(super) use transition::restore_procedural;
pub(super) use transition::staging::install as install_mesh_staging;
pub(super) use transition::{VegetationQualityBindings, apply_quality};
pub use transition::{
    update_vegetation_sources, update_vegetation_sources_with_buildings,
    update_vegetation_sources_with_paths, update_vegetation_sources_with_promotions,
    update_vegetation_sources_with_waterways,
};

/// Validated policy and requested presentation tier; sources remain immutable.
#[derive(Clone, Resource)]
pub struct VegetationQualityRequest {
    pub(super) document: VisualValleyQualityProfileDocument,
    hash: String,
    pub(super) tier: VisualQualityTier,
}

impl VegetationQualityRequest {
    pub fn request(&mut self, tier: VisualQualityTier) {
        self.tier = tier;
    }
    pub fn validate_vegetation(
        &self,
        source: &forge_project::VegetationDocument,
    ) -> Result<(), forge_project::VegetationDocumentError> {
        let mut derived = source.clone();
        derived.presentation = self.profile(source.presentation);
        derived.validate()
    }
    pub fn new(
        terrain: &TerrainDocument,
        document: VisualValleyQualityProfileDocument,
        tier: VisualQualityTier,
    ) -> Result<Self, VisualValleyQualityProfileError> {
        document.validate_identity(terrain.project_id, terrain.area_id, terrain.revision)?;
        let hash = document.content_hash()?.to_string();
        Ok(Self {
            document,
            hash,
            tier,
        })
    }

    pub(super) fn profile(
        &self,
        source: VegetationPresentationProfile,
    ) -> VegetationPresentationProfile {
        let settings = self.document.preset(self.tier).vegetation;
        VegetationPresentationProfile {
            near_lod_distance_m: settings.near_lod_distance_m,
            medium_lod_distance_m: settings.medium_lod_distance_m,
            hlod_distance_m: settings.hlod_distance_m,
            cull_distance_m: settings.cull_distance_m,
            grass_blades_per_patch: settings.grass_blades_per_patch,
            ..source
        }
    }

    pub(super) fn observation(&self, canonical_placements: u32) -> VegetationQualityObservation {
        VegetationQualityObservation {
            policy_hash: self.hash.clone(),
            initial_tier: self.tier,
            requested_tier: self.tier,
            projected_tier: self.tier,
            canonical_placements,
            status: "prepared_initial_cpu_projection_not_gpu_or_dynamic_preset",
        }
    }
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
pub struct VegetationQualityObservation {
    pub policy_hash: String,
    pub initial_tier: VisualQualityTier,
    pub requested_tier: VisualQualityTier,
    pub projected_tier: VisualQualityTier,
    pub canonical_placements: u32,
    pub status: &'static str,
}
