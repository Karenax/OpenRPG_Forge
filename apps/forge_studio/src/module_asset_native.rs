//! Read-only, host-authorized installed-module models. `WebView` supplies no paths.
use crate::asset_library_model_wire::{MAX_MODEL_BYTES, MODEL_HEADER_HEADROOM};
use crate::asset_library_protocol::ModuleModel;
use forge_core::{AssetId, ContentHash};
use serde::Deserialize;
use std::collections::BTreeSet;
use std::fmt;
use std::fs::File;
use std::io::Read;
use std::path::{Path, PathBuf};

const MAX_CATALOG_BYTES: usize = 1024 * 1024;

#[derive(Debug, PartialEq, Eq)]
pub enum ModuleModelError {
    Missing(AssetId),
    Rejected(String),
}

impl fmt::Display for ModuleModelError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Missing(id) => write!(formatter, "Modèle {id} absent du catalogue du module"),
            Self::Rejected(reason) => write!(formatter, "Catalogue du module refusé : {reason}"),
        }
    }
}

impl std::error::Error for ModuleModelError {}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Catalog {
    schema_version: u32,
    assets: Vec<Entry>,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Entry {
    id: AssetId,
    hash: ContentHash,
    file: String,
    license: String,
    // Optional native shadow auxiliaries are catalog metadata, never model paths
    // accepted from IPC. The ordinary Studio GLB loader derives its own geometry.
    #[serde(default)]
    shadow_positions: Option<String>,
    #[serde(default)]
    shadow_indices: Option<String>,
}

pub struct ModuleAssetResolver {
    root: PathBuf,
    catalog: String,
    catalog_sha256: ContentHash,
}

impl ModuleAssetResolver {
    pub fn new(
        root: &Path,
        catalog: &str,
        catalog_sha256: ContentHash,
    ) -> Result<Self, ModuleModelError> {
        safe_name(catalog).map_err(ModuleModelError::Rejected)?;
        if !Path::new(catalog)
            .extension()
            .is_some_and(|ext| ext.eq_ignore_ascii_case("json"))
        {
            return Err(ModuleModelError::Rejected(
                "Le catalogue doit être un fichier JSON".into(),
            ));
        }
        let root = root.canonicalize().map_err(reject)?;
        if !root.is_dir() {
            return Err(ModuleModelError::Rejected(
                "Racine de modèles indisponible".into(),
            ));
        }
        let resolver = Self {
            root,
            catalog: catalog.into(),
            catalog_sha256,
        };
        resolver.read_catalog()?;
        Ok(resolver)
    }

    /// Completely absent configuration allows ordinary Vault-only installations.
    /// Partial configuration is a refusal, not a reason to search another store.
    pub fn from_environment() -> Result<Option<Self>, ModuleModelError> {
        let root = std::env::var_os("FORGE_MODULE_ASSET_ROOT");
        let catalog = std::env::var_os("FORGE_MODULE_ASSET_CATALOG");
        let pin = std::env::var_os("FORGE_MODULE_ASSET_CATALOG_SHA256");
        match (root, catalog, pin) {
            (None, None, None) => Ok(None),
            (Some(root), Some(catalog), Some(pin)) => {
                let catalog = catalog
                    .to_str()
                    .ok_or_else(|| reject("Nom de catalogue illisible"))?;
                let pin = pin
                    .to_str()
                    .ok_or_else(|| reject("Empreinte du catalogue illisible"))?;
                let hash = ContentHash::parse(pin).map_err(reject)?;
                Self::new(Path::new(&root), catalog, hash).map(Some)
            }
            _ => Err(reject(
                "Configuration FORGE_MODULE_ASSET_ROOT/CATALOG/CATALOG_SHA256 incomplète",
            )),
        }
    }

    pub fn resolve(
        &self,
        asset_id: AssetId,
        sha256: ContentHash,
    ) -> Result<ModuleModel, ModuleModelError> {
        // Reverify the trusted snapshot even for Missing. A replaced/corrupt
        // catalogue must not silently route the placement to the Vault.
        let catalog = self.read_catalog()?;
        let entry = catalog
            .assets
            .iter()
            .find(|entry| entry.id == asset_id)
            .ok_or(ModuleModelError::Missing(asset_id))?;
        if entry.hash != sha256 {
            return Err(reject(format!(
                "Empreinte sauvegardée différente du catalogue pour {asset_id}"
            )));
        }
        let path = self.safe_path(&entry.file)?;
        let bytes = read_limited(&path, MAX_MODEL_BYTES, MODEL_HEADER_HEADROOM)?;
        if forge_core::bytes_sha256(&bytes) != sha256 {
            return Err(reject(format!(
                "SHA-256 différent du catalogue pour {asset_id}"
            )));
        }
        validate_glb_header(&bytes).map_err(reject)?;
        // Detect catalog replacement during acquisition, before publishing bytes.
        self.read_catalog()?;
        Ok(ModuleModel {
            asset_id,
            sha256,
            catalog_sha256: self.catalog_sha256,
            bytes,
        })
    }

    fn safe_path(&self, name: &str) -> Result<PathBuf, ModuleModelError> {
        safe_name(name).map_err(ModuleModelError::Rejected)?;
        let path = self.root.join(name).canonicalize().map_err(reject)?;
        if !path.starts_with(&self.root) {
            return Err(reject(
                "Un fichier du catalogue sort de la racine autorisée",
            ));
        }
        Ok(path)
    }

    fn read_catalog(&self) -> Result<Catalog, ModuleModelError> {
        let path = self.safe_path(&self.catalog)?;
        let bytes = read_limited(&path, MAX_CATALOG_BYTES, 0)?;
        if forge_core::bytes_sha256(&bytes) != self.catalog_sha256 {
            return Err(reject("SHA-256 du catalogue différent du pin installé"));
        }
        let catalog: Catalog = serde_json::from_slice(&bytes).map_err(reject)?;
        if catalog.schema_version != 1 || catalog.assets.len() > 4096 {
            return Err(reject("Version ou budget du catalogue invalide"));
        }
        let mut ids = BTreeSet::new();
        for entry in &catalog.assets {
            if entry.id.as_uuid().is_nil()
                || !ids.insert(entry.id)
                || entry.license.trim().is_empty()
            {
                return Err(reject("Identité dupliquée/vide ou licence absente"));
            }
            safe_name(&entry.file).map_err(reject)?;
            if !Path::new(&entry.file)
                .extension()
                .is_some_and(|ext| ext.eq_ignore_ascii_case("glb"))
            {
                return Err(reject("Un modèle du catalogue doit être un GLB"));
            }
            for shadow in [&entry.shadow_positions, &entry.shadow_indices]
                .into_iter()
                .flatten()
            {
                safe_name(shadow).map_err(reject)?;
            }
        }
        Ok(catalog)
    }
}

fn reject(reason: impl fmt::Display) -> ModuleModelError {
    ModuleModelError::Rejected(reason.to_string())
}

fn safe_name(name: &str) -> Result<(), String> {
    if name.is_empty()
        || name.len() > 255
        || name.starts_with('.')
        || name.contains(['/', '\\', ':'])
        || name.chars().any(char::is_control)
    {
        return Err("Nom de fichier relatif simple attendu".into());
    }
    Ok(())
}

fn read_limited(path: &Path, limit: usize, headroom: usize) -> Result<Vec<u8>, ModuleModelError> {
    let file = File::open(path).map_err(reject)?;
    let metadata = file.metadata().map_err(reject)?;
    if !metadata.is_file() || metadata.len() > limit as u64 {
        return Err(reject("Fichier irrégulier ou budget dépassé"));
    }
    let length = usize::try_from(metadata.len()).map_err(reject)?;
    let capacity = length
        .checked_add(headroom)
        .ok_or_else(|| reject("Taille invalide"))?;
    let mut bytes = Vec::new();
    bytes.try_reserve_exact(capacity).map_err(reject)?;
    file.take(metadata.len() + 1)
        .read_to_end(&mut bytes)
        .map_err(reject)?;
    if bytes.len() != length {
        return Err(reject("Fichier modifié pendant la lecture"));
    }
    Ok(bytes)
}

fn validate_glb_header(bytes: &[u8]) -> Result<(), &'static str> {
    let header = bytes.get(..20).ok_or("GLB tronqué")?;
    if &header[..4] != b"glTF"
        || &header[4..8] != 2_u32.to_le_bytes().as_slice()
        || &header[8..12]
            != u32::try_from(bytes.len())
                .map_err(|_| "GLB trop grand")?
                .to_le_bytes()
                .as_slice()
        || &header[16..20] != b"JSON"
    {
        return Err("En-tête GLB autonome invalide");
    }
    let length = u32::from_le_bytes(
        header[12..16]
            .try_into()
            .map_err(|_| "En-tête GLB tronqué")?,
    ) as usize;
    if length == 0 || !length.is_multiple_of(4) || length > bytes.len() - 20 {
        return Err("Chunk JSON GLB tronqué ou invalide");
    }
    Ok(())
}
