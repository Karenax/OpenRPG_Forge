//! Read-only private asset-library transport. The Vault remains the source of truth.
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, deny_unknown_fields)]
pub struct LibraryQuery {
    pub text: String,
    pub pack: String,
    pub family: String,
    pub asset_type: String,
    pub subtype: String,
    /// Empty, `converted_model`, or `source_support`.
    pub availability: String,
    /// Empty, `metadata_ready`, `needs_review`, or `runtime_qualified`.
    pub status: String,
    /// Empty/name, triangles, or height; all ascending, with ID as a stable tie breaker.
    pub sort: String,
    pub offset: usize,
    pub limit: usize,
    pub refresh: bool,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum LibraryRequest {
    Search {
        query: LibraryQuery,
    },
    Details {
        id: String,
        revision: String,
    },
    Thumbnail {
        id: String,
        revision: String,
    },
    Model {
        id: String,
        revision: String,
    },
    /// Persisted placements pin content independently of a search-page revision.
    ModelReference {
        asset_id: forge_core::AssetId,
        sha256: forge_core::ContentHash,
    },
}

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct LibraryCard {
    pub id: String,
    pub name: String,
    pub description: String,
    pub pack: String,
    pub family: String,
    pub asset_type: String,
    pub subtype: String,
    /// `converted_model` for a usable GLB, `source_support` for a preserved
    /// source that cannot honestly be exposed as a placement model.
    pub availability: String,
    pub tags: Vec<String>,
    pub dimensions: [f64; 3],
    pub triangles: u64,
    pub metadata_ready: bool,
    pub runtime_qualified: bool,
    pub has_thumbnail: bool,
}

#[derive(Clone, Debug, Default, Serialize, Deserialize)]
pub struct LibraryFacets {
    pub packs: Vec<String>,
    pub families: Vec<String>,
    pub types: Vec<String>,
    pub subtypes: Vec<String>,
    pub availabilities: Vec<String>,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct LibraryPage {
    /// SHA-256 of the registry; selection and image requests pin this snapshot.
    pub revision: String,
    pub vault_path: String,
    pub total_assets: usize,
    pub matched_assets: usize,
    pub offset: usize,
    pub limit: usize,
    pub facets: LibraryFacets,
    pub assets: Vec<LibraryCard>,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct LibraryDetail {
    pub card: LibraryCard,
    pub details: serde_json::Value,
    pub profile: serde_json::Value,
    pub catalog_asset: serde_json::Value,
    pub license: String,
    pub model_path: Option<String>,
    pub source_path: Option<String>,
    pub preview_path: Option<String>,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case")]
pub enum LibraryResponse {
    Page { page: LibraryPage },
    Details { detail: Box<LibraryDetail> },
    Thumbnail { bytes: Vec<u8> },
    Model { model: LibraryModel },
}

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct LibraryModel {
    pub id: String,
    pub asset_id: forge_core::AssetId,
    pub sha256: forge_core::ContentHash,
    pub bytes: Vec<u8>,
}

/// A separate domain prevents collisions with assets in the distributed kit.
#[must_use]
pub fn vault_asset_id(id: &str) -> forge_core::AssetId {
    forge_core::AssetId::deterministic(format!("asset-vault/v1:{id}"))
}
