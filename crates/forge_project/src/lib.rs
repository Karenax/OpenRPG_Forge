#![deny(unsafe_code)]

//! Versioned source-of-truth documents used by both native and WASM consumers.

mod environment;
mod player;
mod terrain;
mod vegetation;

pub use environment::{
    DistanceFogFalloff, DistanceFogProfile, ENVIRONMENT_PROFILE_SCHEMA,
    ENVIRONMENT_PROFILE_SCHEMA_VERSION, EnvironmentFogResolution, EnvironmentProfileDocument,
    EnvironmentProfileError, EnvironmentQualityTier, MAX_ENVIRONMENT_PROFILE_JSON_BYTES,
    SkyProfile, SrgbColor, SunProfile, VolumetricFogCapability, VolumetricFogFallback,
    VolumetricFogProfile,
};

pub use player::{
    PLAYER_CONTROLLER_PROFILE_SCHEMA, PLAYER_CONTROLLER_PROFILE_SCHEMA_VERSION,
    PlayerCameraProfile, PlayerControllerProfile, PlayerControllerProfileDocument,
    PlayerControllerProfileError,
};

pub use terrain::{
    HeightSamplingAlgorithm, HeightSamplingParameters, MAX_TERRAIN_HEIGHT_DELTAS,
    MAX_TERRAIN_MATERIAL_LAYERS, MAX_TERRAIN_SAMPLE_INTERVALS_PER_AXIS,
    MIN_TERRAIN_MATERIAL_LAYERS, TERRAIN_DOCUMENT_SCHEMA, TERRAIN_DOCUMENT_SCHEMA_VERSION,
    TERRAIN_DOCUMENT_SCHEMA_VERSION_V1, TerrainDocument, TerrainDocumentError, TerrainExtent,
    TerrainHeightDelta, TerrainMaterialLayer, TerrainMaterialLayerId, TerrainMaterialRole,
};
pub use vegetation::{
    MAX_GRASS_BLADES_PER_PATCH, MAX_VEGETATION_ATTEMPTS_PER_CELL,
    MAX_VEGETATION_CELLS_PER_CHUNK_AXIS, MAX_VEGETATION_DOCUMENT_JSON_BYTES,
    MAX_VEGETATION_EXCLUSIONS, MAX_VEGETATION_EXPANDED_GRASS_BLADES, MAX_VEGETATION_LAYERS,
    MAX_VEGETATION_PLACEMENT_ATTEMPTS, MIN_VEGETATION_CELLS_PER_CHUNK_AXIS,
    VEGETATION_CHUNK_SIZE_M, VEGETATION_DOCUMENT_SCHEMA, VEGETATION_DOCUMENT_SCHEMA_VERSION,
    VEGETATION_EXTENT_M, VegetationCollisionProfile, VegetationCollisionShape, VegetationDocument,
    VegetationDocumentError, VegetationExclusion, VegetationExclusionId, VegetationExtent,
    VegetationKind, VegetationLayer, VegetationLayerId, VegetationMaterialFilter,
    VegetationPresentationProfile,
};

use forge_core::{
    AreaId, CanonicalJsonError, ContentHash, CoordinateSystem, EntityId, ProjectId, Revision,
    Transform, TransformError, canonical_json_sha256,
};
use serde::{Deserialize, Serialize};
use std::collections::HashSet;
use thiserror::Error;

pub const SCENE_PROOF_SCHEMA: &str = "forge.scene-proof";
pub const SCENE_PROOF_SCHEMA_VERSION: u32 = 1;

/// Small, versioned scene used to prove native/WASM document parity.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SceneProofDocument {
    pub schema: String,
    pub schema_version: u32,
    pub project_id: ProjectId,
    pub area_id: AreaId,
    pub revision: Revision,
    pub coordinate_system: CoordinateSystem,
    pub name: String,
    pub entities: Vec<SceneProofEntity>,
}

impl SceneProofDocument {
    #[must_use]
    pub fn new(
        project_id: ProjectId,
        area_id: AreaId,
        revision: Revision,
        name: impl Into<String>,
        entities: Vec<SceneProofEntity>,
    ) -> Self {
        Self {
            schema: SCENE_PROOF_SCHEMA.to_owned(),
            schema_version: SCENE_PROOF_SCHEMA_VERSION,
            project_id,
            area_id,
            revision,
            coordinate_system: CoordinateSystem::METRES_Y_UP_RIGHT_HANDED,
            name: name.into(),
            entities,
        }
    }

    pub fn validate(&self) -> Result<(), SceneProofError> {
        if self.schema != SCENE_PROOF_SCHEMA {
            return Err(SceneProofError::InvalidSchema(self.schema.clone()));
        }
        if self.schema_version != SCENE_PROOF_SCHEMA_VERSION {
            return Err(SceneProofError::UnsupportedVersion(self.schema_version));
        }
        if self.coordinate_system != CoordinateSystem::METRES_Y_UP_RIGHT_HANDED {
            return Err(SceneProofError::UnsupportedCoordinateSystem);
        }
        if self.name.trim().is_empty() {
            return Err(SceneProofError::EmptySceneName);
        }
        if self.entities.is_empty() {
            return Err(SceneProofError::NoEntities);
        }

        let mut ids = HashSet::with_capacity(self.entities.len());
        let mut camera_count = 0_usize;
        let mut light_count = 0_usize;
        for entity in &self.entities {
            if !ids.insert(entity.id) {
                return Err(SceneProofError::DuplicateEntity(entity.id));
            }
            if entity.name.trim().is_empty() {
                return Err(SceneProofError::EmptyEntityName(entity.id));
            }
            entity
                .transform
                .validate()
                .map_err(|source| SceneProofError::InvalidTransform {
                    entity_id: entity.id,
                    source,
                })?;
            entity.kind.validate(entity.id)?;
            match entity.kind {
                SceneProofKind::Camera { .. } => camera_count += 1,
                SceneProofKind::DirectionalLight { .. } => light_count += 1,
                SceneProofKind::Ground { .. } | SceneProofKind::Cube { .. } => {}
            }
        }
        if camera_count != 1 {
            return Err(SceneProofError::InvalidCameraCount(camera_count));
        }
        if light_count == 0 {
            return Err(SceneProofError::MissingLight);
        }
        Ok(())
    }

    pub fn from_json(json: &str) -> Result<Self, SceneProofError> {
        let document: Self = serde_json::from_str(json)?;
        document.validate()?;
        Ok(document)
    }

    pub fn to_pretty_json(&self) -> Result<String, SceneProofError> {
        self.validate()?;
        Ok(serde_json::to_string_pretty(self)?)
    }

    pub fn content_hash(&self) -> Result<ContentHash, SceneProofError> {
        self.validate()?;
        Ok(canonical_json_sha256(self)?)
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SceneProofEntity {
    pub id: EntityId,
    pub name: String,
    pub transform: Transform,
    pub kind: SceneProofKind,
}

impl SceneProofEntity {
    #[must_use]
    pub fn new(
        id: EntityId,
        name: impl Into<String>,
        transform: Transform,
        kind: SceneProofKind,
    ) -> Self {
        Self {
            id,
            name: name.into(),
            transform,
            kind,
        }
    }
}

/// Procedural primitives required by the Gate 0 proof scene.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "type", rename_all = "snake_case", deny_unknown_fields)]
pub enum SceneProofKind {
    Ground {
        size_x_m: f32,
        size_z_m: f32,
    },
    Cube {
        edge_m: f32,
    },
    DirectionalLight {
        illuminance_lux: f32,
    },
    Camera {
        vertical_fov_degrees: f32,
        near_m: f32,
        far_m: f32,
    },
}

impl SceneProofKind {
    fn validate(&self, entity_id: EntityId) -> Result<(), SceneProofError> {
        let valid_positive = |value: f32| value.is_finite() && value > 0.0;
        match self {
            Self::Ground { size_x_m, size_z_m } => {
                if !valid_positive(*size_x_m) || !valid_positive(*size_z_m) {
                    return Err(SceneProofError::InvalidKindValue {
                        entity_id,
                        field: "ground size",
                    });
                }
            }
            Self::Cube { edge_m } => {
                if !valid_positive(*edge_m) {
                    return Err(SceneProofError::InvalidKindValue {
                        entity_id,
                        field: "cube edge",
                    });
                }
            }
            Self::DirectionalLight { illuminance_lux } => {
                if !valid_positive(*illuminance_lux) {
                    return Err(SceneProofError::InvalidKindValue {
                        entity_id,
                        field: "light illuminance",
                    });
                }
            }
            Self::Camera {
                vertical_fov_degrees,
                near_m,
                far_m,
            } => {
                if !vertical_fov_degrees.is_finite()
                    || *vertical_fov_degrees <= 1.0
                    || *vertical_fov_degrees >= 179.0
                    || !valid_positive(*near_m)
                    || !far_m.is_finite()
                    || *far_m <= *near_m
                {
                    return Err(SceneProofError::InvalidKindValue {
                        entity_id,
                        field: "camera projection",
                    });
                }
            }
        }
        Ok(())
    }
}

#[derive(Debug, Error)]
pub enum SceneProofError {
    #[error("invalid scene-proof schema `{0}`")]
    InvalidSchema(String),
    #[error("unsupported scene-proof schema version {0}")]
    UnsupportedVersion(u32),
    #[error("scene-proof coordinate system must be metres, Y-up, right-handed")]
    UnsupportedCoordinateSystem,
    #[error("scene-proof name cannot be empty")]
    EmptySceneName,
    #[error("scene-proof must contain at least one entity")]
    NoEntities,
    #[error("duplicate persistent entity id {0}")]
    DuplicateEntity(EntityId),
    #[error("entity {0} has an empty name")]
    EmptyEntityName(EntityId),
    #[error("entity {entity_id} has an invalid transform: {source}")]
    InvalidTransform {
        entity_id: EntityId,
        #[source]
        source: TransformError,
    },
    #[error("entity {entity_id} has an invalid {field}")]
    InvalidKindValue {
        entity_id: EntityId,
        field: &'static str,
    },
    #[error("scene-proof must contain exactly one camera, found {0}")]
    InvalidCameraCount(usize),
    #[error("scene-proof must contain at least one directional light")]
    MissingLight,
    #[error("invalid scene-proof JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("cannot hash scene-proof: {0}")]
    Hash(#[from] CanonicalJsonError),
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::{UnitQuaternion, Vec3};

    fn valid_document() -> SceneProofDocument {
        let project_id = ProjectId::deterministic("test-project");
        let area_id = AreaId::deterministic("test-area");
        SceneProofDocument::new(
            project_id,
            area_id,
            Revision::INITIAL,
            "Gate 0 proof",
            vec![
                SceneProofEntity::new(
                    EntityId::deterministic("ground"),
                    "Ground",
                    Transform::IDENTITY,
                    SceneProofKind::Ground {
                        size_x_m: 16.0,
                        size_z_m: 16.0,
                    },
                ),
                SceneProofEntity::new(
                    EntityId::deterministic("light"),
                    "Sun",
                    Transform::IDENTITY,
                    SceneProofKind::DirectionalLight {
                        illuminance_lux: 20_000.0,
                    },
                ),
                SceneProofEntity::new(
                    EntityId::deterministic("camera"),
                    "Camera",
                    Transform {
                        translation_m: Vec3::new(0.0, 3.0, 8.0),
                        rotation: UnitQuaternion::IDENTITY,
                        scale: Vec3::ONE,
                    },
                    SceneProofKind::Camera {
                        vertical_fov_degrees: 60.0,
                        near_m: 0.1,
                        far_m: 1000.0,
                    },
                ),
            ],
        )
    }

    #[test]
    fn versioned_json_round_trip_is_lossless() {
        let document = valid_document();
        let json = document.to_pretty_json().unwrap();
        let restored = SceneProofDocument::from_json(&json).unwrap();

        assert_eq!(restored, document);
        assert_eq!(
            restored.content_hash().unwrap(),
            document.content_hash().unwrap()
        );
        assert!(json.contains("\"schema_version\": 1"));
    }

    #[test]
    fn duplicate_persistent_ids_are_rejected() {
        let mut document = valid_document();
        document.entities[1].id = document.entities[0].id;

        assert!(matches!(
            document.validate(),
            Err(SceneProofError::DuplicateEntity(_))
        ));
    }

    #[test]
    fn unknown_schema_versions_are_rejected() {
        let mut document = valid_document();
        document.schema_version = 2;

        assert!(matches!(
            document.validate(),
            Err(SceneProofError::UnsupportedVersion(2))
        ));
    }
}
