#![deny(unsafe_code)]

//! Deterministic world recipes. Rendering remains an adapter responsibility.

mod environment;
mod player;
mod terrain;
mod terrain_brush;
mod terrain_generation;
mod vegetation;
mod water;
mod weather;

pub use terrain_brush::{
    TerrainBrush, TerrainBrushCommand, TerrainBrushError, TerrainBrushOperation,
    TerrainBrushResult, preview_terrain_brush, preview_terrain_stroke,
};

pub use environment::{
    VISUAL_VALLEY_ENVIRONMENT_JSON, VisualValleyEnvironmentRecipe,
    embedded_visual_valley_environment_profile,
};

pub use player::{
    VISUAL_VALLEY_PLAYER_CONTROLLER_JSON, VisualValleyPlayerControllerRecipe,
    embedded_visual_valley_player_controller_profile,
};
pub use terrain::{
    VISUAL_VALLEY_SEED, VISUAL_VALLEY_SIZE_M, VISUAL_VALLEY_TERRAIN_JSON,
    VisualValleyTerrainRecipe, embedded_visual_valley_terrain_document,
};
pub use terrain_generation::{
    GeneratedTerrain, TERRAIN_CHUNK_COUNT, TERRAIN_CHUNK_INTERVALS,
    TERRAIN_CHUNK_VERTICES_PER_AXIS, TERRAIN_CHUNKS_PER_AXIS, TERRAIN_EXTENT_M,
    TERRAIN_GLOBAL_INTERVALS_PER_AXIS, TERRAIN_GLOBAL_VERTICES_PER_AXIS, TERRAIN_HALF_EXTENT_M,
    TERRAIN_SAMPLE_SPACING_M, TERRAIN_TRIANGLE_COUNT, TERRAIN_VERTEX_COUNT,
    TerrainChunkColliderData, TerrainChunkCoordinate, TerrainChunkId, TerrainChunkMesh,
    TerrainColliderTriangleOrder, TerrainColliderVertexOrder, TerrainDirtyChunk, TerrainDirtyPlan,
    TerrainGenerationError, TerrainGenerationPlan, TerrainHeightSampleCoordinate,
    TerrainMaterialWeights, TerrainPartialUpdate, TerrainPartialUpdateCommit,
    TerrainPartialUpdateError, TerrainPartialUpdateRequest, TerrainSampleRegion,
    TerrainSurfaceSample, sample_height,
};
pub use vegetation::{
    GeneratedVegetation, VISUAL_VALLEY_VEGETATION_CELLS_PER_CHUNK_AXIS,
    VISUAL_VALLEY_VEGETATION_JSON, VISUAL_VALLEY_VEGETATION_SEED, VegetationChunk,
    VegetationChunkId, VegetationGenerationError, VegetationGenerationPlan, VegetationInstance,
    VegetationInstanceId, VegetationKindCounts, VisualValleyVegetationRecipe,
    embedded_visual_valley_vegetation_document,
};
pub use water::{
    RiverCenterlineSample, VISUAL_VALLEY_WATER_JSON, VISUAL_VALLEY_WATER_SEED,
    VisualValleyWaterRecipe, embedded_visual_valley_water_document, sample_river_centerline,
};
pub use weather::{
    VISUAL_VALLEY_WEATHER_JSON, VISUAL_VALLEY_WEATHER_SEED, VisualValleyWeatherRecipe,
    embedded_visual_valley_weather_profile,
};

use forge_core::{AreaId, ProjectId, Revision, Transform, UnitQuaternion, Vec3};
use forge_project::{SceneProofDocument, SceneProofEntity, SceneProofError, SceneProofKind};
use serde::{Deserialize, Serialize};
use thiserror::Error;

pub const PROOF_RECIPE_VERSION: u32 = 1;
pub const GATE0_PROOF_JSON: &str = include_str!("../../../examples/gate0/proof.scene.json");

/// Loads the exact versioned JSON embedded by both native and WASM Gate 0 targets.
pub fn embedded_gate0_proof_document() -> Result<SceneProofDocument, ProofSceneRecipeError> {
    Ok(SceneProofDocument::from_json(GATE0_PROOF_JSON)?)
}

/// Procedural Gate 0 recipe containing no imported asset reference.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ProofSceneRecipe {
    pub recipe_version: u32,
    pub seed: u64,
    pub scene_name: String,
    pub ground_size_m: [f32; 2],
    pub cube_edge_m: f32,
    pub light_illuminance_lux: f32,
    pub camera_vertical_fov_degrees: f32,
}

impl Default for ProofSceneRecipe {
    fn default() -> Self {
        Self {
            recipe_version: PROOF_RECIPE_VERSION,
            seed: 0xF0_12_6E,
            scene_name: "Gate 0 procedural proof".to_owned(),
            ground_size_m: [16.0, 16.0],
            cube_edge_m: 1.0,
            light_illuminance_lux: 30_000.0,
            camera_vertical_fov_degrees: 60.0,
        }
    }
}

impl ProofSceneRecipe {
    pub fn validate(&self) -> Result<(), ProofSceneRecipeError> {
        if self.recipe_version != PROOF_RECIPE_VERSION {
            return Err(ProofSceneRecipeError::UnsupportedVersion(
                self.recipe_version,
            ));
        }
        if self.scene_name.trim().is_empty() {
            return Err(ProofSceneRecipeError::EmptyName);
        }
        if !positive(self.ground_size_m[0]) || !positive(self.ground_size_m[1]) {
            return Err(ProofSceneRecipeError::InvalidGroundSize);
        }
        if !positive(self.cube_edge_m) {
            return Err(ProofSceneRecipeError::InvalidCubeSize);
        }
        if !positive(self.light_illuminance_lux) {
            return Err(ProofSceneRecipeError::InvalidIlluminance);
        }
        if !self.camera_vertical_fov_degrees.is_finite()
            || self.camera_vertical_fov_degrees <= 1.0
            || self.camera_vertical_fov_degrees >= 179.0
        {
            return Err(ProofSceneRecipeError::InvalidFieldOfView);
        }
        Ok(())
    }

    /// Materializes a stable document. Same IDs, revision and recipe yield the same hash.
    pub fn build(
        &self,
        project_id: ProjectId,
        area_id: AreaId,
        revision: Revision,
    ) -> Result<SceneProofDocument, ProofSceneRecipeError> {
        self.validate()?;
        let scope = format!("{project_id}/{area_id}/{:016x}", self.seed);
        let entity_id = |name: &str| forge_core::EntityId::deterministic(format!("{scope}/{name}"));
        let half_cube = self.cube_edge_m * 0.5;

        let entities = vec![
            SceneProofEntity::new(
                entity_id("ground"),
                "Procedural ground",
                Transform::IDENTITY,
                SceneProofKind::Ground {
                    size_x_m: self.ground_size_m[0],
                    size_z_m: self.ground_size_m[1],
                },
            ),
            SceneProofEntity::new(
                entity_id("cube"),
                "Procedural cube",
                Transform::from_translation(Vec3::new(0.0, half_cube, 0.0)),
                SceneProofKind::Cube {
                    edge_m: self.cube_edge_m,
                },
            ),
            SceneProofEntity::new(
                entity_id("sun"),
                "Proof sun",
                Transform {
                    translation_m: Vec3::new(0.0, 8.0, 0.0),
                    rotation: UnitQuaternion::new(-0.382_683_43, 0.0, 0.0, 0.923_879_5),
                    scale: Vec3::ONE,
                },
                SceneProofKind::DirectionalLight {
                    illuminance_lux: self.light_illuminance_lux,
                },
            ),
            SceneProofEntity::new(
                entity_id("camera"),
                "Proof camera",
                Transform {
                    translation_m: Vec3::new(0.0, 2.0, 8.0),
                    rotation: UnitQuaternion::new(-0.092_54, 0.0, 0.0, 0.995_708),
                    scale: Vec3::ONE,
                },
                SceneProofKind::Camera {
                    vertical_fov_degrees: self.camera_vertical_fov_degrees,
                    near_m: 0.1,
                    far_m: 1_000.0,
                },
            ),
        ];

        let document = SceneProofDocument::new(
            project_id,
            area_id,
            revision,
            self.scene_name.clone(),
            entities,
        );
        document.validate()?;
        Ok(document)
    }
}

fn positive(value: f32) -> bool {
    value.is_finite() && value > 0.0
}

#[derive(Debug, Error)]
pub enum ProofSceneRecipeError {
    #[error("unsupported proof recipe version {0}")]
    UnsupportedVersion(u32),
    #[error("proof scene name cannot be empty")]
    EmptyName,
    #[error("ground dimensions must be finite and strictly positive")]
    InvalidGroundSize,
    #[error("cube edge must be finite and strictly positive")]
    InvalidCubeSize,
    #[error("light illuminance must be finite and strictly positive")]
    InvalidIlluminance,
    #[error("camera vertical field of view must be between 1 and 179 degrees")]
    InvalidFieldOfView,
    #[error("generated scene-proof is invalid: {0}")]
    InvalidDocument(#[from] SceneProofError),
}

#[cfg(test)]
mod tests {
    use super::*;

    fn identities() -> (ProjectId, AreaId) {
        (
            ProjectId::deterministic("gate-0-project"),
            AreaId::deterministic("gate-0-area"),
        )
    }

    #[test]
    fn default_recipe_builds_ground_cube_light_and_camera() {
        let (project_id, area_id) = identities();
        let document = ProofSceneRecipe::default()
            .build(project_id, area_id, Revision::INITIAL)
            .unwrap();

        assert_eq!(document.entities.len(), 4);
        assert!(matches!(
            document.entities[0].kind,
            SceneProofKind::Ground { .. }
        ));
        assert!(matches!(
            document.entities[1].kind,
            SceneProofKind::Cube { .. }
        ));
        assert!(matches!(
            document.entities[2].kind,
            SceneProofKind::DirectionalLight { .. }
        ));
        assert!(matches!(
            document.entities[3].kind,
            SceneProofKind::Camera { .. }
        ));
        assert!(document.validate().is_ok());
    }

    #[test]
    fn same_recipe_is_byte_and_hash_deterministic() {
        let (project_id, area_id) = identities();
        let recipe = ProofSceneRecipe::default();
        let first = recipe
            .build(project_id, area_id, Revision::INITIAL)
            .unwrap();
        let second = recipe
            .build(project_id, area_id, Revision::INITIAL)
            .unwrap();

        assert_eq!(first, second);
        assert_eq!(
            first.content_hash().unwrap(),
            second.content_hash().unwrap()
        );
        assert_eq!(
            first.to_pretty_json().unwrap(),
            second.to_pretty_json().unwrap()
        );
        println!("{}", first.content_hash().unwrap());
    }

    #[test]
    fn seed_changes_generated_entity_ids() {
        let (project_id, area_id) = identities();
        let first = ProofSceneRecipe::default()
            .build(project_id, area_id, Revision::INITIAL)
            .unwrap();
        let second = ProofSceneRecipe {
            seed: ProofSceneRecipe::default().seed + 1,
            ..ProofSceneRecipe::default()
        }
        .build(project_id, area_id, Revision::INITIAL)
        .unwrap();

        assert_ne!(first.entities[0].id, second.entities[0].id);
        assert_ne!(
            first.content_hash().unwrap(),
            second.content_hash().unwrap()
        );
    }

    #[test]
    fn embedded_json_matches_the_default_recipe_exactly() {
        let (project_id, area_id) = identities();
        let generated = ProofSceneRecipe::default()
            .build(project_id, area_id, Revision::INITIAL)
            .unwrap();
        let embedded = embedded_gate0_proof_document().unwrap();
        assert_eq!(embedded, generated);
        assert_eq!(
            embedded.content_hash().unwrap().to_string(),
            "0ea53d539067f399e7e76678532144955f35e5a15e95d11becf3a174843c67df"
        );
    }
}
