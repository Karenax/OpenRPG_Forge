use crate::error::{BridgeError, Result, display_path};
use crate::manifest::{AreaRef, IdentityDocument, ModuleManifest, ReferencesDocument};
use crate::validate::{ValidatedBundle, validate_bundle};
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;
use std::fs;
use std::path::Path;

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ImportCounts {
    pub areas: usize,
    pub tiles: usize,
    pub instances: usize,
    pub blueprints: usize,
    pub dialogs: usize,
    pub journal_entries: usize,
    pub factions: usize,
    pub scripts_ncs: usize,
    pub scripts_nss: usize,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ImportReport {
    pub importable: bool,
    pub complete: bool,
    pub playable: bool,
    pub counts: ImportCounts,
    pub warnings: Vec<String>,
    pub errors: Vec<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct ImportedModule {
    pub root: std::path::PathBuf,
    pub manifest: ModuleManifest,
    pub module: ImportedModuleDocument,
    pub areas: Vec<ImportedArea>,
    pub blueprints: Vec<ImportedBlueprint>,
    pub dialogs: Vec<ImportedDialog>,
    pub journal: ImportedJournal,
    pub factions: ImportedFactions,
    pub scripts: ImportedScripts,
    pub rules: serde_json::Value,
    pub references: ReferencesDocument,
    pub identity: IdentityDocument,
    pub diagnostics: Vec<serde_json::Value>,
    pub report: ImportReport,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct ImportedModuleDocument {
    pub schema_version: String,
    pub identity: ModuleIdentity,
    pub entry_area_resref: String,
    pub start_positions: Vec<serde_json::Value>,
    pub areas: Vec<serde_json::Value>,
    pub properties: serde_json::Value,
    pub event_scripts: BTreeMap<String, String>,
    pub global_variables: Vec<serde_json::Value>,
    pub localization: serde_json::Value,
    pub unmapped_fields: Vec<serde_json::Value>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ModuleIdentity {
    pub resref: String,
    pub name: String,
    pub description: String,
    pub tag: String,
    pub version: String,
    pub game: String,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ImportedArea {
    pub manifest: AreaRef,
    pub manifest_sha256: String,
    pub name: String,
    pub width: u32,
    pub height: u32,
    pub tile_count: usize,
    pub instance_count: usize,
    pub objects: Vec<ImportedObject>,
    pub area_document: serde_json::Value,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ImportedObject {
    pub id: String,
    pub category: String,
    pub tag: Option<String>,
    pub template_resref: Option<String>,
    pub classification: String,
    pub status: String,
    pub model_assets: Vec<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ImportedBlueprint {
    pub blueprint_id: String,
    pub kind: String,
    pub resref: String,
    pub tag: Option<String>,
    pub name: Option<String>,
    pub status: String,
    pub reference_count: usize,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ImportedDialog {
    pub resref: String,
    pub nodes: usize,
    pub links: usize,
    pub diagnostics: usize,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ImportedJournal {
    pub categories: Vec<serde_json::Value>,
    pub entry_count: usize,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ImportedFactions {
    pub factions: Vec<serde_json::Value>,
    pub reputations: Vec<serde_json::Value>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ImportedScripts {
    pub scripts: Vec<serde_json::Value>,
    pub ncs_count: usize,
    pub nss_count: usize,
}

pub fn load_bundle(bundle: &Path) -> Result<ImportedModule> {
    let validated = validate_bundle(bundle)?;
    import_validated(&validated)
}

#[allow(clippy::too_many_lines)]
fn import_validated(validated: &ValidatedBundle) -> Result<ImportedModule> {
    let root = validated.root.as_path();
    let manifest = validated.manifest.clone();
    let module: ImportedModuleDocument = read_json(root, &manifest.module.path)?;
    if module.schema_version != "module-document@1" {
        return Err(BridgeError::UnsupportedVersion(module.schema_version));
    }
    if module.identity.game != "nwn1-ee" {
        return Err(BridgeError::invalid(format!(
            "unsupported module game {}",
            module.identity.game
        )));
    }

    let mut areas = Vec::new();
    for (reference, area_manifest) in &validated.areas {
        let area_root = root
            .join(&reference.path)
            .parent()
            .ok_or_else(|| BridgeError::invalid("area manifest has no parent"))?
            .to_path_buf();
        let document: AreaDocument = read_json(&area_root, "area.json")?;
        if document.schema_version != "area-migration-bundle@1.0.0" {
            return Err(BridgeError::UnsupportedVersion(document.schema_version));
        }
        if document.resref != reference.resref {
            return Err(BridgeError::invalid(format!(
                "area resref mismatch {} != {}",
                document.resref, reference.resref
            )));
        }
        if document.instances.len() != reference.instance_count
            || document.tiles.len() != reference.tile_count
        {
            return Err(BridgeError::invalid(format!(
                "area counts mismatch {}",
                reference.resref
            )));
        }
        let objects = document
            .instances
            .iter()
            .map(|instance| ImportedObject {
                id: instance.id.clone(),
                category: instance.source.category.clone(),
                tag: instance.source.tag.clone(),
                template_resref: instance.source.template_resref.clone(),
                classification: instance.classification.clone(),
                status: instance.status.clone(),
                model_assets: instance.model_assets.clone(),
            })
            .collect::<Vec<_>>();
        areas.push(ImportedArea {
            manifest: reference.clone(),
            manifest_sha256: sha256_bytes(&fs::read(root.join(&reference.path)).map_err(
                |error| BridgeError::io(display_path(&root.join(&reference.path)), error),
            )?),
            name: document.name.clone(),
            width: document.dimensions[0],
            height: document.dimensions[1],
            tile_count: document.tiles.len(),
            instance_count: document.instances.len(),
            objects,
            area_document: serde_json::to_value(&document)
                .map_err(|error| BridgeError::invalid(error.to_string()))?,
        });
        let _ = area_manifest;
    }
    areas.sort_by(|left, right| left.manifest.resref.cmp(&right.manifest.resref));

    let mut blueprints = Vec::new();
    for catalog in &manifest.catalogs {
        if catalog.kind == "blueprints" {
            let file: BlueprintFile = read_json(root, &catalog.path)?;
            for blueprint in file.blueprints {
                blueprints.push(ImportedBlueprint {
                    blueprint_id: blueprint.id,
                    kind: blueprint.kind,
                    resref: blueprint.resref,
                    tag: blueprint.tag,
                    name: blueprint.name,
                    status: blueprint.status,
                    reference_count: blueprint.references.len(),
                });
            }
        }
    }
    blueprints.sort_by(|left, right| left.blueprint_id.cmp(&right.blueprint_id));

    let mut dialogs = Vec::new();
    for dialog in &manifest.dialogs {
        let file: DialogFile = read_json(root, &dialog.path)?;
        dialogs.push(ImportedDialog {
            resref: dialog.resref.clone(),
            nodes: file.nodes.len(),
            links: file.links.len(),
            diagnostics: file.diagnostics.len(),
        });
    }
    dialogs.sort_by(|left, right| left.resref.cmp(&right.resref));

    let journal_file: JournalFile = read_json(root, &manifest.journal.path)?;
    let journal = ImportedJournal {
        entry_count: journal_file
            .categories
            .iter()
            .map(|category| category.entries.len())
            .sum(),
        categories: journal_file
            .categories
            .into_iter()
            .map(|category| serde_json::to_value(category).unwrap_or_default())
            .collect(),
    };

    let faction_file: FactionFile = read_json(root, &manifest.factions.path)?;
    let factions = ImportedFactions {
        factions: faction_file
            .factions
            .into_iter()
            .map(|faction| serde_json::to_value(faction).unwrap_or_default())
            .collect(),
        reputations: faction_file
            .reputations
            .into_iter()
            .map(|reputation| serde_json::to_value(reputation).unwrap_or_default())
            .collect(),
    };

    let scripts_file: ScriptsFile = read_json(root, &manifest.scripts.inventory_path)?;
    let scripts = ImportedScripts {
        ncs_count: scripts_file
            .scripts
            .iter()
            .filter(|script| script.ncs.is_some())
            .count(),
        nss_count: scripts_file
            .scripts
            .iter()
            .filter(|script| script.nss.is_some())
            .count(),
        scripts: scripts_file
            .scripts
            .into_iter()
            .map(|script| serde_json::to_value(script).unwrap_or_default())
            .collect(),
    };

    let rules = serde_json::json!({
        "tables": read_json_value(root, &manifest.rules.tables_path)?,
        "tlk": read_json_value(root, &manifest.rules.tlk_path)?,
    });
    let references: ReferencesDocument = read_json(root, &manifest.references.path)?;
    let identity: IdentityDocument = read_json(root, &manifest.identity_map.path)?;
    let diagnostics = read_diagnostics(root)?;

    let counts = ImportCounts {
        areas: areas.len(),
        tiles: areas.iter().map(|area| area.tile_count).sum(),
        instances: areas.iter().map(|area| area.instance_count).sum(),
        blueprints: blueprints.len(),
        dialogs: dialogs.len(),
        journal_entries: journal.entry_count,
        factions: factions.factions.len(),
        scripts_ncs: scripts.ncs_count,
        scripts_nss: scripts.nss_count,
    };
    verify_import_counts(&manifest, &counts)?;
    let report = ImportReport {
        importable: true,
        complete: manifest.readiness.complete,
        playable: false,
        counts: counts.clone(),
        warnings: manifest.readiness.warnings.clone(),
        errors: Vec::new(),
    };
    Ok(ImportedModule {
        root: root.to_path_buf(),
        manifest,
        module,
        areas,
        blueprints,
        dialogs,
        journal,
        factions,
        scripts,
        rules,
        references,
        identity,
        diagnostics,
        report,
    })
}

fn verify_import_counts(manifest: &ModuleManifest, counts: &ImportCounts) -> Result<()> {
    if counts.areas != manifest.counts.areas
        || counts.tiles != manifest.counts.tiles
        || counts.instances != manifest.counts.instances
        || counts.blueprints != manifest.counts.blueprints
        || counts.dialogs != manifest.counts.dialogs
        || counts.journal_entries != manifest.counts.journal_entries
        || counts.factions != manifest.counts.factions
        || counts.scripts_ncs != manifest.counts.scripts_ncs
        || counts.scripts_nss != manifest.counts.scripts_nss
    {
        return Err(BridgeError::invalid(
            "imported counts do not match the manifest",
        ));
    }
    Ok(())
}

fn read_json<T: serde::de::DeserializeOwned>(root: &Path, relative: &str) -> Result<T> {
    let path = root.join(relative);
    let bytes = read_bounded(&path)?;
    serde_json::from_slice(&bytes).map_err(|error| BridgeError::json(display_path(&path), error))
}

fn read_json_value(root: &Path, relative: &str) -> Result<serde_json::Value> {
    read_json(root, relative)
}

fn read_bounded(path: &Path) -> Result<Vec<u8>> {
    let metadata =
        fs::symlink_metadata(path).map_err(|error| BridgeError::io(display_path(path), error))?;
    if metadata.file_type().is_symlink() {
        return Err(BridgeError::UnsafePath(display_path(path)));
    }
    if metadata.len() > crate::manifest::MAX_MANIFEST_BYTES {
        return Err(BridgeError::SizeLimit(display_path(path)));
    }
    fs::read(path).map_err(|error| BridgeError::io(display_path(path), error))
}

fn read_diagnostics(root: &Path) -> Result<Vec<serde_json::Value>> {
    let path = root.join("diagnostics.jsonl");
    let bytes = read_bounded(&path)?;
    let mut diagnostics = Vec::new();
    for (index, line) in bytes.split(|byte| *byte == b'\n').enumerate() {
        if line.is_empty() {
            continue;
        }
        diagnostics.push(serde_json::from_slice(line).map_err(|error| {
            BridgeError::invalid(format!(
                "diagnostics.jsonl line {} invalid: {error}",
                index + 1
            ))
        })?);
    }
    Ok(diagnostics)
}

fn sha256_bytes(bytes: &[u8]) -> String {
    use sha2::{Digest, Sha256};
    let mut digest = String::with_capacity(64);
    for byte in Sha256::digest(bytes) {
        use std::fmt::Write as _;
        let _ = write!(digest, "{byte:02x}");
    }
    digest
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct AreaDocument {
    schema_version: String,
    resref: String,
    name: String,
    dimensions: [u32; 2],
    tiles: Vec<serde_json::Value>,
    instances: Vec<AreaInstance>,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct AreaInstance {
    id: String,
    source: AreaInstanceSource,
    classification: String,
    status: String,
    #[serde(default)]
    model_assets: Vec<String>,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct AreaInstanceSource {
    category: String,
    #[serde(default)]
    tag: Option<String>,
    #[serde(default)]
    template_resref: Option<String>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct BlueprintFile {
    blueprints: Vec<Blueprint>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct Blueprint {
    #[serde(rename = "blueprintId")]
    id: String,
    kind: String,
    resref: String,
    #[serde(default)]
    tag: Option<String>,
    #[serde(default)]
    name: Option<String>,
    status: String,
    #[serde(default)]
    references: Vec<serde_json::Value>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct DialogFile {
    nodes: Vec<serde_json::Value>,
    links: Vec<serde_json::Value>,
    diagnostics: Vec<serde_json::Value>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct JournalFile {
    categories: Vec<JournalCategory>,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct JournalCategory {
    entries: Vec<serde_json::Value>,
    #[serde(flatten)]
    other: serde_json::Map<String, serde_json::Value>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct FactionFile {
    #[serde(default)]
    factions: Vec<serde_json::Value>,
    #[serde(default)]
    reputations: Vec<serde_json::Value>,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct ScriptsFile {
    scripts: Vec<Script>,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct Script {
    resref: String,
    status: String,
    #[serde(default)]
    ncs: Option<serde_json::Value>,
    #[serde(default)]
    nss: Option<serde_json::Value>,
    #[serde(flatten)]
    other: serde_json::Map<String, serde_json::Value>,
}
