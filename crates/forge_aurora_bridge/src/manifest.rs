use serde::{Deserialize, Serialize};

pub const MODULE_BUNDLE_SCHEMA_VERSION: &str = "module-migration-bundle@1.0.0";
pub const AREA_BUNDLE_SCHEMA_VERSION: &str = "area-migration-bundle@1.0.0";
pub const INTEGRITY_SCOPE: &str = "module inventory covers every payload outside nested area bundles; each nested area manifest is the integrity root of its own payload";

pub const MAX_MANIFEST_BYTES: u64 = 16 * 1024 * 1024;
pub const MAX_MODULE_FILES: usize = 100_000;
pub const MAX_MODULE_BYTES: u64 = 16 * 1024 * 1024 * 1024;
pub const MAX_AREAS: usize = 4_096;
pub const MAX_AREA_FILES: usize = 50_000;
pub const MAX_AREA_BYTES: u64 = 4 * 1024 * 1024 * 1024;

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ModuleManifest {
    pub schema_version: String,
    pub exporter: serde_json::Value,
    pub source: SourceRef,
    pub rights: Rights,
    pub coordinate_system: serde_json::Value,
    pub resolution_policy: String,
    pub module: PayloadRef,
    pub areas: Vec<AreaRef>,
    pub catalogs: Vec<CatalogRef>,
    pub dialogs: Vec<DialogRef>,
    pub journal: JournalRef,
    pub factions: FactionRef,
    pub scripts: ScriptsRef,
    pub rules: RulesRef,
    pub assets: Vec<AssetRef>,
    pub references: ReferencesRef,
    pub identity_map: IdentityMapRef,
    pub dependencies: Vec<DependencyRef>,
    pub counts: Counts,
    pub readiness: Readiness,
    pub files: Vec<FileRecord>,
    pub integrity_scope: String,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct SourceRef {
    pub game: String,
    pub module_resref: String,
    pub module_file_name: String,
    pub module_sha256: String,
    pub module_size_bytes: u64,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct Rights {
    pub classification: String,
    pub redistribution: String,
    #[serde(default)]
    pub license: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct PayloadRef {
    pub path: String,
    pub sha256: String,
    pub size_bytes: u64,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct AreaRef {
    pub resref: String,
    pub name: String,
    pub area_schema_version: String,
    pub path: String,
    pub sha256: String,
    pub size_bytes: u64,
    pub tile_count: usize,
    pub instance_count: usize,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct CatalogRef {
    pub kind: String,
    pub path: String,
    pub sha256: String,
    pub size_bytes: u64,
    pub entry_count: usize,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct DialogRef {
    pub resref: String,
    pub path: String,
    pub sha256: String,
    pub size_bytes: u64,
    pub node_count: usize,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct JournalRef {
    pub path: String,
    pub sha256: String,
    pub size_bytes: u64,
    pub entry_count: usize,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct FactionRef {
    pub path: String,
    pub sha256: String,
    pub size_bytes: u64,
    pub faction_count: usize,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ScriptsRef {
    pub inventory_path: String,
    pub inventory_sha256: String,
    pub inventory_size_bytes: u64,
    pub ncs_count: usize,
    pub nss_count: usize,
    pub missing_ncs_count: usize,
    pub missing_nss_count: usize,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct RulesRef {
    pub tables_path: String,
    pub tables_sha256: String,
    pub tables_size_bytes: u64,
    pub table_count: usize,
    pub tlk_path: String,
    pub tlk_sha256: String,
    pub tlk_size_bytes: u64,
    pub string_count: usize,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct AssetRef {
    pub path: String,
    pub sha256: String,
    pub size_bytes: u64,
    pub role: String,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ReferencesRef {
    pub path: String,
    pub sha256: String,
    pub size_bytes: u64,
    pub node_count: usize,
    pub edge_count: usize,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct IdentityMapRef {
    pub path: String,
    pub sha256: String,
    pub size_bytes: u64,
    pub module_namespace: String,
    pub entry_count: usize,
    pub collision_count: usize,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct DependencyRef {
    pub kind: String,
    pub name: String,
    pub sha256: Option<String>,
    pub size_bytes: Option<u64>,
    pub status: String,
}

#[derive(Debug, Clone, Default, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct Counts {
    pub areas: usize,
    pub tiles: usize,
    pub instances: usize,
    pub blueprints: usize,
    pub dialogs: usize,
    pub journal_entries: usize,
    pub factions: usize,
    pub scripts_ncs: usize,
    pub scripts_nss: usize,
    pub rules_tables: usize,
    pub tlk_strings: usize,
    pub assets: usize,
    pub references: usize,
    pub missing: usize,
    pub unsupported: usize,
    pub license_blocked: usize,
    pub diagnostics: usize,
    pub warnings: usize,
    pub errors: usize,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct Readiness {
    pub exportable: bool,
    pub complete: bool,
    pub playable: bool,
    pub blockers: Vec<String>,
    pub warnings: Vec<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct FileRecord {
    pub path: String,
    pub size_bytes: u64,
    pub sha256: String,
    pub role: String,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct AreaManifest {
    pub schema_version: String,
    pub module_sha256: String,
    pub area_resref: String,
    pub files: Vec<FileRecord>,
    #[serde(default)]
    pub counts: serde_json::Value,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ReferencesDocument {
    pub schema_version: String,
    pub nodes: Vec<ReferenceNode>,
    pub edges: Vec<ReferenceEdge>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ReferenceNode {
    pub id: String,
    pub kind: String,
    #[serde(default)]
    pub area_resref: Option<String>,
    #[serde(default)]
    pub resref: Option<String>,
    pub status: String,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ReferenceEdge {
    pub from: String,
    pub to: String,
    pub kind: String,
    #[serde(default)]
    pub source_field: Option<String>,
    pub status: String,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct IdentityDocument {
    pub schema_version: String,
    pub module_namespace: String,
    pub entries: Vec<IdentityEntry>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct IdentityEntry {
    pub semantic_id: String,
    pub kind: String,
    #[serde(default)]
    pub area_resref: Option<String>,
    #[serde(default)]
    pub source_resref: Option<String>,
    #[serde(default)]
    pub source_tag: Option<String>,
    pub discriminator: String,
    pub source_ordinal: usize,
    pub fingerprint: Option<String>,
    pub stability: String,
}
