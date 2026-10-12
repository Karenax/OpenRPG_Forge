//! Read-only private asset-library transport. The Vault remains the source of truth.
use forge_core::ContentHash;
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

/// A current library revision authorizes the metadata-owned package path.
/// Client requests contain only identity and content pins, never paths.
#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct LibraryPhysicsRequest {
    pub schema_version: u32,
    pub id: String,
    pub revision: String,
    pub carrier_sha256: ContentHash,
    pub manifest_sha256: ContentHash,
}

/// A saved placement pins its package independently of the current search page.
#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct LibrarySavedPhysicsRequest {
    pub schema_version: u32,
    pub asset_id: forge_core::AssetId,
    pub carrier_sha256: ContentHash,
    pub manifest_sha256: ContentHash,
    pub package_sha256: ContentHash,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(untagged)]
pub enum PhysicsReadRequest {
    Current(LibraryPhysicsRequest),
    Saved(LibrarySavedPhysicsRequest),
}

/// Installed modules keep their published UUIDs; they are not Vault identities.
#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum ModuleModelRequest {
    ModuleModelReference {
        schema_version: u32,
        asset_id: forge_core::AssetId,
        sha256: forge_core::ContentHash,
    },
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(untagged)]
pub enum ModelReadRequest {
    Vault(LibraryRequest),
    Module(ModuleModelRequest),
}

#[derive(Clone, Debug)]
pub struct ModuleModel {
    pub asset_id: forge_core::AssetId,
    pub sha256: forge_core::ContentHash,
    pub catalog_sha256: forge_core::ContentHash,
    /// Optional authority from the host-pinned catalog, never inferred from a filename.
    pub collision: Option<ModuleCollisionPolicy>,
    pub bytes: Vec<u8>,
}

/// Explicit collision interpretation of the verified module's static Scene0.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ModuleCollisionPolicy {
    StaticTriangleMeshes,
}

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
impl ModuleCollisionPolicy {
    /// A primitive must belong to this placement's scene, even under an explicit policy.
    pub(crate) fn selects_primitive<T: Copy + Eq>(
        self,
        mut entity: T,
        root: T,
        is_mesh: bool,
        mut parent: impl FnMut(T) -> Option<T>,
    ) -> bool {
        match self {
            Self::StaticTriangleMeshes => {
                if !is_mesh || entity == root {
                    return false;
                }
                for _ in 0..128 {
                    let Some(next) = parent(entity) else {
                        return false;
                    };
                    if next == root {
                        return true;
                    }
                    entity = next;
                }
                false
            }
        }
    }
}

/// A missing entry is a distinct successful wire result, allowing a Vault lookup.
/// A corrupt catalogue, stale pin or unavailable file never produces this result.
#[derive(Clone, Debug)]
pub enum ModuleModelReply {
    Model(ModuleModel),
    Missing {
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

#[cfg(test)]
mod module_collision_tests {
    use super::ModuleCollisionPolicy;

    #[test]
    fn static_primitives_must_be_meshes_owned_by_the_expected_placement() {
        let policy = ModuleCollisionPolicy::StaticTriangleMeshes;
        let parent = |entity| match entity {
            3 => Some(2),
            2 => Some(1),
            5 => Some(4),
            _ => None,
        };
        assert!(policy.selects_primitive(3, 1, true, parent));
        assert!(policy.selects_primitive(2, 1, true, parent));
        assert!(!policy.selects_primitive(3, 4, true, parent));
        assert!(!policy.selects_primitive(5, 1, true, parent));
        assert!(!policy.selects_primitive(3, 1, false, parent));
        assert!(!policy.selects_primitive(1, 1, true, parent));
        assert!(!policy.selects_primitive(6, 1, true, parent));
    }

    #[test]
    fn cycles_and_excessive_parent_depth_never_admit_a_primitive() {
        let policy = ModuleCollisionPolicy::StaticTriangleMeshes;
        assert!(!policy.selects_primitive(3, 1, true, |_| Some(3)));
        assert!(!policy.selects_primitive(130_u16, 1, true, |entity| entity.checked_sub(1)));
        assert!(policy.selects_primitive(129_u16, 1, true, |entity| entity.checked_sub(1)));
    }

    #[test]
    fn collision_policy_has_one_explicit_spelling_and_no_default() {
        let policy = ModuleCollisionPolicy::StaticTriangleMeshes;
        assert_eq!(
            serde_json::to_value(policy).unwrap(),
            "static_triangle_meshes"
        );
        for value in ["null", "\"convex\"", "\"STATIC_TRIANGLE_MESHES\"", "{}"] {
            assert!(serde_json::from_str::<ModuleCollisionPolicy>(value).is_err());
        }
    }
}
