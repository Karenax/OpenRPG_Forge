#![deny(unsafe_code)]

//! Asset metadata and provenance validation without rendering or filesystem coupling.

use forge_core::{AssetId, CanonicalJsonError, ContentHash, canonical_json_sha256};
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, HashSet};
use thiserror::Error;

pub const ASSET_CATALOG_SCHEMA_VERSION: u32 = 1;

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum AssetKind {
    Mesh,
    Material,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ProceduralAssetRecipe {
    pub generator: String,
    pub generator_version: u32,
    pub parameters: BTreeMap<String, String>,
}

impl ProceduralAssetRecipe {
    #[must_use]
    pub fn new(generator: impl Into<String>, generator_version: u32) -> Self {
        Self {
            generator: generator.into(),
            generator_version,
            parameters: BTreeMap::new(),
        }
    }

    #[must_use]
    pub fn with_parameter(mut self, key: impl Into<String>, value: impl Into<String>) -> Self {
        self.parameters.insert(key.into(), value.into());
        self
    }
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(tag = "origin", rename_all = "snake_case", deny_unknown_fields)]
pub enum AssetSource {
    Procedural {
        recipe: ProceduralAssetRecipe,
    },
    External {
        uri: String,
        declared_license: Option<String>,
    },
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssetRecord {
    pub id: AssetId,
    pub logical_name: String,
    pub kind: AssetKind,
    pub source: AssetSource,
    pub content_hash: ContentHash,
}

impl AssetRecord {
    pub fn procedural(
        logical_name: impl Into<String>,
        kind: AssetKind,
        recipe: ProceduralAssetRecipe,
    ) -> Result<Self, AssetCatalogError> {
        let logical_name = logical_name.into();
        let id = AssetId::deterministic(logical_name.as_bytes());
        let content_hash = canonical_json_sha256(&recipe)?;
        Ok(Self {
            id,
            logical_name,
            kind,
            source: AssetSource::Procedural { recipe },
            content_hash,
        })
    }
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssetCatalog {
    pub schema_version: u32,
    pub assets: Vec<AssetRecord>,
}

impl AssetCatalog {
    /// Gate 0 catalogue: generated ground/cube meshes and colors, no file-backed asset.
    pub fn gate0_procedural() -> Result<Self, AssetCatalogError> {
        let assets = vec![
            AssetRecord::procedural(
                "proof/ground-mesh",
                AssetKind::Mesh,
                ProceduralAssetRecipe::new("forge.primitive.plane", 1)
                    .with_parameter("size_x_m", "16")
                    .with_parameter("size_z_m", "16"),
            )?,
            AssetRecord::procedural(
                "proof/cube-mesh",
                AssetKind::Mesh,
                ProceduralAssetRecipe::new("forge.primitive.cube", 1).with_parameter("edge_m", "1"),
            )?,
            AssetRecord::procedural(
                "proof/ground-material",
                AssetKind::Material,
                ProceduralAssetRecipe::new("forge.material.flat-color", 1)
                    .with_parameter("linear_rgba", "0.12,0.30,0.14,1.0"),
            )?,
            AssetRecord::procedural(
                "proof/cube-material",
                AssetKind::Material,
                ProceduralAssetRecipe::new("forge.material.flat-color", 1)
                    .with_parameter("linear_rgba", "0.16,0.42,0.80,1.0"),
            )?,
        ];
        let catalog = Self {
            schema_version: ASSET_CATALOG_SCHEMA_VERSION,
            assets,
        };
        catalog.validate_procedural_only()?;
        Ok(catalog)
    }

    /// Validates the Gate 0 boundary without reading or resolving any external path.
    pub fn validate_procedural_only(&self) -> Result<(), AssetCatalogError> {
        if self.schema_version != ASSET_CATALOG_SCHEMA_VERSION {
            return Err(AssetCatalogError::UnsupportedVersion(self.schema_version));
        }
        if self.assets.is_empty() {
            return Err(AssetCatalogError::EmptyCatalog);
        }

        let mut ids = HashSet::with_capacity(self.assets.len());
        let mut names = HashSet::with_capacity(self.assets.len());
        for asset in &self.assets {
            if !ids.insert(asset.id) {
                return Err(AssetCatalogError::DuplicateId(asset.id));
            }
            if asset.logical_name.trim().is_empty() {
                return Err(AssetCatalogError::EmptyLogicalName(asset.id));
            }
            if !names.insert(asset.logical_name.as_str()) {
                return Err(AssetCatalogError::DuplicateLogicalName(
                    asset.logical_name.clone(),
                ));
            }
            match &asset.source {
                AssetSource::Procedural { recipe } => {
                    if recipe.generator.trim().is_empty() || recipe.generator_version == 0 {
                        return Err(AssetCatalogError::InvalidProceduralRecipe(asset.id));
                    }
                    let expected = canonical_json_sha256(recipe)?;
                    if expected != asset.content_hash {
                        return Err(AssetCatalogError::HashMismatch(asset.id));
                    }
                }
                AssetSource::External { .. } => {
                    return Err(AssetCatalogError::ExternalAssetNotAllowed(asset.id));
                }
            }
        }
        Ok(())
    }
}

#[derive(Debug, Error)]
pub enum AssetCatalogError {
    #[error("unsupported asset catalog schema version {0}")]
    UnsupportedVersion(u32),
    #[error("asset catalog cannot be empty")]
    EmptyCatalog,
    #[error("duplicate asset id {0}")]
    DuplicateId(AssetId),
    #[error("asset {0} has an empty logical name")]
    EmptyLogicalName(AssetId),
    #[error("duplicate asset logical name `{0}`")]
    DuplicateLogicalName(String),
    #[error("asset {0} has an invalid procedural recipe")]
    InvalidProceduralRecipe(AssetId),
    #[error("procedural asset {0} does not match its recipe hash")]
    HashMismatch(AssetId),
    #[error("external asset {0} is forbidden in the Gate 0 procedural catalogue")]
    ExternalAssetNotAllowed(AssetId),
    #[error("cannot hash procedural recipe: {0}")]
    Hash(#[from] CanonicalJsonError),
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn gate0_catalog_is_fully_procedural_and_deterministic() {
        let first = AssetCatalog::gate0_procedural().unwrap();
        let second = AssetCatalog::gate0_procedural().unwrap();

        assert_eq!(first, second);
        assert_eq!(first.assets.len(), 4);
        assert!(first.validate_procedural_only().is_ok());
        assert!(
            first
                .assets
                .iter()
                .all(|asset| matches!(asset.source, AssetSource::Procedural { .. }))
        );
    }

    #[test]
    fn external_asset_is_rejected_without_accessing_its_uri() {
        let mut catalog = AssetCatalog::gate0_procedural().unwrap();
        catalog.assets[0].source = AssetSource::External {
            uri: "file:///outside-workspace/ground.glb".to_owned(),
            declared_license: None,
        };

        assert!(matches!(
            catalog.validate_procedural_only(),
            Err(AssetCatalogError::ExternalAssetNotAllowed(_))
        ));
    }

    #[test]
    fn changed_recipe_requires_a_new_content_hash() {
        let mut catalog = AssetCatalog::gate0_procedural().unwrap();
        let AssetSource::Procedural { recipe } = &mut catalog.assets[0].source else {
            panic!("Gate 0 ground must be procedural");
        };
        recipe
            .parameters
            .insert("size_x_m".to_owned(), "32".to_owned());

        assert!(matches!(
            catalog.validate_procedural_only(),
            Err(AssetCatalogError::HashMismatch(_))
        ));
    }
}
