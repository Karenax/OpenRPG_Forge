//! Bounded metadata eligible for sharing. Catalogue authority is native-only.
use forge_core::{AssetId, ContentHash, canonical_json_bytes};
use serde::{Deserialize, Serialize};
use std::collections::BTreeSet;

pub const MAX_SHARED_ASSETS: usize = 16;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssetReference {
    pub asset_id: AssetId,
    pub model_hash: Option<ContentHash>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub physics: Option<Box<crate::zone_authoring::placements::PhysicsPackageReference>>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub particles: Option<Box<crate::zone_authoring::placements::ParticlePackageReference>>,
    pub name: String,
    pub pack: String,
    pub family: String,
    pub asset_type: String,
    pub license: String,
    pub dimensions_m: [f64; 3],
    pub triangles: u64,
    pub metadata_ready: bool,
    pub runtime_qualified: bool,
    /// True only after the native library verified the pinned model bytes.
    pub model_verified: bool,
}

impl AssetReference {
    #[must_use]
    pub const fn model_available(&self) -> bool {
        self.model_hash.is_some()
    }
}

#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct CatalogContext {
    pub revision: ContentHash,
    pub assets: Vec<AssetReference>,
    #[serde(skip_serializing_if = "std::ops::Not::not")]
    pub allow_placement: bool,
}

/// This authority cannot be deserialized from UI or model data.
#[derive(Clone, Debug)]
pub struct AssetCatalog(CatalogContext);
impl AssetCatalog {
    pub fn new(revision: ContentHash, assets: Vec<AssetReference>) -> Result<Self, String> {
        if assets.is_empty()
            || assets.len() > MAX_SHARED_ASSETS
            || assets
                .iter()
                .map(|asset| asset.asset_id)
                .collect::<BTreeSet<_>>()
                .len()
                != assets.len()
        {
            return Err("Choose 1..16 distinct asset references".into());
        }
        for asset in &assets {
            if asset.physics.as_ref().is_some_and(|pin| {
                pin.schema_version != 1 || Some(pin.carrier_sha256) != asset.model_hash
            }) || asset.particles.as_ref().is_some_and(|pin| {
                pin.schema_version != 1 || Some(pin.carrier_sha256) != asset.model_hash
            }) {
                return Err("Asset package does not match the verified carrier".into());
            }
            if asset.name.trim().is_empty()
                || asset.name.len() > 512
                || asset.pack.len() > 256
                || asset.family.len() > 256
                || asset.asset_type.len() > 128
                || asset.license.len() > 4096
                || asset
                    .dimensions_m
                    .iter()
                    .any(|value| !value.is_finite() || *value < 0.0)
                || (asset.model_verified
                    && (!asset.model_available()
                        || !asset.metadata_ready
                        || asset.license.trim().is_empty()))
            {
                return Err("Invalid asset metadata or model verification claim".into());
            }
        }
        let context = CatalogContext {
            revision,
            assets,
            allow_placement: false,
        };
        if canonical_json_bytes(&context)
            .map_err(|error| error.to_string())?
            .len()
            > 32 * 1024
        {
            return Err("Shared asset metadata exceeds 32 KiB".into());
        }
        Ok(Self(context))
    }
    #[must_use]
    pub const fn context(&self) -> &CatalogContext {
        &self.0
    }

    /// Called only after native verification and an explicit creation permission.
    pub fn authorize_placement(mut self) -> Result<Self, String> {
        if !self.0.assets.iter().any(|asset| asset.model_verified) {
            return Err("No verified model is available for placement".into());
        }
        self.0.allow_placement = true;
        Ok(self)
    }
}
