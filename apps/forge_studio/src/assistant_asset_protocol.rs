//! Native library search and metadata pins; never caller-supplied paths or claims.
use forge_core::ContentHash;
use forge_project::assistant::assets::AssetReference;
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssetQuery {
    pub text: String,
    pub pack: String,
    pub offset: usize,
}
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssetSelection {
    pub revision: ContentHash,
    pub ids: Vec<String>,
    #[serde(default)]
    pub allow_placement: bool,
}
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct AssetSearchEntry {
    pub library_id: String,
    pub asset: AssetReference,
}
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct AssetSearchPage {
    pub revision: ContentHash,
    pub matched: usize,
    pub offset: usize,
    pub assets: Vec<AssetSearchEntry>,
}
