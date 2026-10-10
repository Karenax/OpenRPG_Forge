//! Native, read-only implementation of the private Asset Vault library.
//!
//! The registry is the authority for the imported metadata.  Nothing in this
//! module accepts a client-supplied filesystem path, and the Vault is never
//! written to.
use crate::asset_library_protocol::{
    LibraryCard, LibraryDetail, LibraryFacets, LibraryModel, LibraryPage, LibraryQuery,
    LibraryRequest, LibraryResponse, vault_asset_id,
};
use serde_json::Value;
mod history;
use sha2::{Digest, Sha256};
use std::{
    collections::{BTreeSet, HashMap},
    fs,
    io::Read,
    path::{Component, Path, PathBuf},
    sync::Arc,
};

const MAX_JSON: u64 = 64 * 1024 * 1024;
const MAX_TOTAL_JSON: u64 = 256 * 1024 * 1024;
const MAX_PACKS: usize = 1024;
const MAX_ASSETS: usize = 100_000;
const MAX_ID: usize = 512;
const MAX_QUERY: usize = 256;
const MAX_THUMBNAIL: u64 = 512 * 1024;
const MAX_MODEL: u64 = crate::asset_library_model_wire::MAX_MODEL_BYTES as u64;
const MAX_SOURCE_SUPPORT: u64 = 128 * 1024 * 1024;
const DEFAULT_LIMIT: usize = 24;
const MAX_LIMIT: usize = 48;
const SOURCE_SUPPORT_REGISTRY: &str = "metadata/asset-source-support-registry.v1.json";

#[derive(Clone, Debug)]
struct StoredAsset {
    card: LibraryCard,
    details: Arc<Value>,
    profile: Value,
    catalog_asset: Arc<Value>,
    license: String,
    model_path: Option<String>,
    source_path: Option<String>,
    preview: Option<(PathBuf, String)>,
    catalog_root: Option<PathBuf>,
    preview_reference: Option<String>,
}

#[derive(Clone, Debug)]
struct SourceSupportCatalog {
    registry_raw: Vec<u8>,
    catalog_raw: Vec<u8>,
}

struct CatalogIndex {
    assets: Vec<Arc<Value>>,
    by_id: HashMap<String, usize>,
    root: PathBuf,
}

#[derive(Default)]
struct BuildCache {
    catalogs: HashMap<PathBuf, CatalogIndex>,
    canonical_paths: HashMap<(PathBuf, String), PathBuf>,
    #[cfg(test)]
    stats: BuildCacheStats,
}

#[cfg(test)]
#[derive(Clone, Debug, Default)]
struct BuildCacheStats {
    canonical_hits: usize,
    canonical_misses: usize,
    profile_lookups: usize,
    catalog_lookups: usize,
    catalog_cache_hits: usize,
    catalog_cache_misses: usize,
}

#[derive(Clone, Debug)]
struct Snapshot {
    revision: String,
    registry_raw: Vec<u8>,
    source_support_registry_raw: Option<Vec<u8>>,
    assets: Vec<StoredAsset>,
    by_id: HashMap<String, usize>,
    #[cfg(test)]
    build_cache_stats: BuildCacheStats,
}

/// Read-only, bounded view over an Asset Vault rooted at `root`.
#[derive(Debug)]
pub struct Library {
    root: PathBuf,
    snapshot: Option<Snapshot>,
    #[cfg(test)]
    physical_access_checks: std::cell::Cell<usize>,
    #[cfg(test)]
    after_build_hook: Option<fn(&Path)>,
    #[cfg(test)]
    before_reply_hook: Option<fn(&Path)>,
}

impl BuildCache {
    fn safe_absolute(&mut self, library: &Library, value: &str) -> Result<PathBuf, String> {
        let path = Path::new(value);
        if !path.is_absolute() {
            return Err("chemin absolu attendu".to_owned());
        }
        let key = (library.root.clone(), value.to_owned());
        if let Some(cached) = self.canonical_paths.get(&key).cloned() {
            inside(&library.root, &cached)?;
            ensure_path_exists(&cached)?;
            #[cfg(test)]
            {
                self.stats.canonical_hits += 1;
            }
            return Ok(cached);
        }
        let canonical = library.safe_absolute(value)?;
        self.canonical_paths.insert(key, canonical.clone());
        #[cfg(test)]
        {
            self.stats.canonical_misses += 1;
        }
        Ok(canonical)
    }

    fn catalog_model_path(
        &mut self,
        library: &Library,
        base: &Path,
        value: &str,
    ) -> Result<Option<PathBuf>, String> {
        validate_relative(value)?;
        let key = (base.to_path_buf(), value.to_owned());
        if let Some(cached) = self.canonical_paths.get(&key).cloned() {
            inside(base, &cached)?;
            inside(&library.root, &cached)?;
            if ensure_path_exists(&cached).is_err() {
                self.canonical_paths.remove(&key);
                return Ok(None);
            }
            #[cfg(test)]
            {
                self.stats.canonical_hits += 1;
            }
            return Ok(Some(cached));
        }
        let joined = base.join(value);
        match joined.canonicalize() {
            Ok(canonical) => {
                inside(base, &canonical)?;
                inside(&library.root, &canonical)?;
                self.canonical_paths.insert(key, canonical.clone());
                #[cfg(test)]
                {
                    self.stats.canonical_misses += 1;
                }
                Ok(Some(canonical))
            }
            Err(_) => Ok(None),
        }
    }
}

impl Library {
    pub fn new(root: &Path) -> Result<Self, String> {
        let root = root
            .canonicalize()
            .map_err(|error| format!("impossible de résoudre le Vault: {error}"))?;
        if !root.is_dir() {
            return Err("la racine du Vault doit être un répertoire".to_owned());
        }
        Ok(Self {
            root,
            snapshot: None,
            #[cfg(test)]
            physical_access_checks: std::cell::Cell::new(0),
            #[cfg(test)]
            after_build_hook: None,
            #[cfg(test)]
            before_reply_hook: None,
        })
    }

    #[expect(
        clippy::too_many_lines,
        reason = "Request routing retains validation and errors beside each read-only library operation"
    )]
    pub fn execute(&mut self, request: LibraryRequest) -> Result<LibraryResponse, String> {
        // An unreadable registry must invalidate the metadata snapshot even on
        // an ordinary request. Restoring identical registry bytes after an
        // error must not permit reuse of stale details or profiles.
        let registry = match self.read_registry() {
            Ok(registry) => registry,
            Err(error) => {
                self.snapshot = None;
                return Err(error);
            }
        };
        let source_support_registry = match self.read_source_support_registry() {
            Ok(registry) => registry,
            Err(error) => {
                self.snapshot = None;
                return Err(error);
            }
        };
        let refresh = matches!(&request, LibraryRequest::Search { query } if query.refresh);
        if refresh
            || self.snapshot.as_ref().is_none_or(|snapshot| {
                snapshot.registry_raw != registry
                    || snapshot.source_support_registry_raw != source_support_registry
            })
        {
            // A failed refresh must not leave an old snapshot available as an
            // implicit fallback on the next ordinary request.
            self.snapshot = None;
            let source_support =
                self.read_source_support_catalog(source_support_registry.as_deref())?;
            let revision = snapshot_revision(&registry, source_support.as_ref());
            let candidate = self.build_snapshot(
                &registry,
                source_support
                    .as_ref()
                    .map(|support| support.catalog_raw.as_slice()),
                source_support_registry.as_deref(),
                revision,
            )?;
            #[cfg(test)]
            if let Some(hook) = self.after_build_hook.take() {
                hook(&self.root);
            }
            self.verify_registry_pins(&registry, source_support_registry.as_deref())?;
            self.snapshot = Some(candidate);
        }
        let response = {
            let snapshot = self.snapshot.as_ref().expect("snapshot was populated");
            match request {
                LibraryRequest::Search { query } => LibraryResponse::Page {
                    page: self.search(snapshot, &query)?,
                },
                LibraryRequest::Details { id, revision } => {
                    let asset = Self::pinned_asset(snapshot, &id, &revision)?;
                    let access = self.resolve_access(asset, true)?;
                    LibraryResponse::Details {
                        detail: Box::new(LibraryDetail {
                            card: asset.card.clone(),
                            details: asset.details.as_ref().clone(),
                            profile: asset.profile.clone(),
                            catalog_asset: asset.catalog_asset.as_ref().clone(),
                            license: asset.license.clone(),
                            model_path: access.model.map(|path| path.display().to_string()),
                            source_path: access.source.map(|path| path.display().to_string()),
                            preview_path: access
                                .preview
                                .map(|(path, _)| path.display().to_string()),
                        }),
                    }
                }
                LibraryRequest::Thumbnail { id, revision } => {
                    let asset = Self::pinned_asset(snapshot, &id, &revision)?;
                    let access = self.resolve_access(asset, false)?;
                    let (path, expected) = access
                        .preview
                        .ok_or_else(|| "vignette absente".to_owned())?;
                    let bytes = read_limited(&path, MAX_THUMBNAIL)?;
                    if !is_png(&bytes) || !same_hash(&bytes, &expected) {
                        return Err("vignette PNG invalide ou empreinte incohérente".to_owned());
                    }
                    LibraryResponse::Thumbnail { bytes }
                }
                LibraryRequest::Model { id, revision } => {
                    let asset = Self::pinned_asset(snapshot, &id, &revision)?;
                    LibraryResponse::Model {
                        model: self.read_model(asset)?,
                    }
                }
                LibraryRequest::ModelReference { asset_id, sha256 } => {
                    let mut matches = snapshot
                        .assets
                        .iter()
                        .filter(|asset| vault_asset_id(&asset.card.id) == asset_id);
                    let asset = matches.next().ok_or("référence du Vault inconnue")?;
                    if matches.next().is_some() {
                        return Err("référence du Vault ambiguë".to_owned());
                    }
                    if asset.card.availability != "converted_model" {
                        return Err("source conservée non plaçable".to_owned());
                    }
                    // Check the saved pin before reading any payload.
                    if forge_core::ContentHash::parse(required(&asset.details, "model_sha256")?)
                        .map_err(|e| e.to_string())?
                        == sha256
                    {
                        LibraryResponse::Model {
                            model: self.read_model(asset)?,
                        }
                    } else {
                        LibraryResponse::Model {
                            model: self.read_historical_model(snapshot, asset, sha256)?,
                        }
                    }
                }
            }
        };
        #[cfg(test)]
        if let Some(hook) = self.before_reply_hook.take() {
            hook(&self.root);
        }
        let snapshot = self.snapshot.as_ref().expect("snapshot was populated");
        if let Err(error) = self.verify_registry_pins(
            &snapshot.registry_raw,
            snapshot.source_support_registry_raw.as_deref(),
        ) {
            self.snapshot = None;
            return Err(error);
        }
        Ok(response)
    }

    fn read_model(&self, asset: &StoredAsset) -> Result<LibraryModel, String> {
        if asset.card.availability != "converted_model" {
            return Err("source conservée non plaçable".to_owned());
        }
        let path = self
            .resolve_access(asset, false)?
            .model
            .ok_or("modèle absent")?;
        let bytes = read_model_limited(&path, MAX_MODEL)?;
        let expected = required(&asset.details, "model_sha256")?;
        if !same_hash(&bytes, expected) {
            return Err("empreinte de modèle incohérente".to_owned());
        }
        // Envelope admission only; the existing governed GLB validator must
        // still run before GPU loading. This does not qualify geometry/rendering.
        if bytes.len() < 20
            || &bytes[..4] != b"glTF"
            || u32::from_le_bytes(bytes[4..8].try_into().unwrap()) != 2
            || u32::from_le_bytes(bytes[8..12].try_into().unwrap()) as usize != bytes.len()
        {
            return Err("enveloppe GLB invalide ou tronquée".to_owned());
        }
        Ok(LibraryModel {
            id: asset.card.id.clone(),
            asset_id: vault_asset_id(&asset.card.id),
            sha256: forge_core::ContentHash::from_bytes(Sha256::digest(&bytes).into()),
            bytes,
        })
    }

    fn verify_registry_pins(&self, expected: &[u8], support: Option<&[u8]>) -> Result<(), String> {
        if self.read_registry()? != expected
            || self.read_source_support_registry()?.as_deref() != support
        {
            return Err("registre modifié pendant la lecture ; relancez la recherche".to_owned());
        }
        Ok(())
    }

    /// Explicit read-only audit of indexed model/source-support/preview payloads.
    /// This is deliberately distinct from Search, and never grants qualification.
    /// Purchased archive payloads are read only when `include_archives` is explicit.
    pub fn audit_indexed_integrity(&mut self, include_archives: bool) -> Result<Value, String> {
        // An explicit audit builds fresh metadata and never reuses a prior
        // snapshot. Any initial or final registry failure therefore leaves no
        // stale snapshot available for a subsequent interactive request.
        self.snapshot = None;
        let registry = self.read_registry()?;
        let support_registry = self.read_source_support_registry()?;
        let support = self.read_source_support_catalog(support_registry.as_deref())?;
        let revision = snapshot_revision(&registry, support.as_ref());
        let candidate = self.build_snapshot(
            &registry,
            support.as_ref().map(|s| s.catalog_raw.as_slice()),
            support_registry.as_deref(),
            revision.clone(),
        )?;
        self.verify_registry_pins(&registry, support_registry.as_deref())?;
        let mut rows = Vec::with_capacity(candidate.assets.len());
        let mut archives = HashMap::<PathBuf, (String, bool)>::new();
        let mut errors = 0_usize;
        for asset in &candidate.assets {
            let result = (|| -> Result<Value, String> {
                let access = self.resolve_access(asset, true)?;
                let mut preview_checked = false;
                if let Some((path, expected)) = access.preview {
                    let bytes = read_limited(&path, MAX_THUMBNAIL)?;
                    if !is_png(&bytes) || !same_hash(&bytes, &expected) {
                        return Err("vignette PNG ou SHA incohérente".to_owned());
                    }
                    preview_checked = true;
                }
                if include_archives && asset.card.availability == "source_support" {
                    let path = self.safe_relative(required(&asset.details, "source_archive")?)?;
                    let expected = required(&asset.details, "source_archive_sha256")?;
                    if let Some((prior, valid)) = archives.get(&path) {
                        if prior != expected {
                            return Err("SHA divergents pour la même archive".to_owned());
                        }
                        if !valid {
                            return Err("SHA d'archive incohérent".to_owned());
                        }
                    } else {
                        let size = fs::metadata(&path)
                            .map_err(|e| format!("archive inaccessible: {e}"))?
                            .len();
                        let valid = same_file_hash(&path, expected, size)?;
                        archives.insert(path, (expected.to_owned(), valid));
                        if !valid {
                            return Err("SHA d'archive incohérent".to_owned());
                        }
                    }
                }
                Ok(
                    serde_json::json!({"id":asset.card.id,"availability":asset.card.availability,"status":"pass","model_full_sha_checked":access.model.is_some(),"source_and_meta_full_sha_checked":access.source.is_some(),"preview_png_sha_checked":preview_checked}),
                )
            })();
            match result {
                Ok(row) => rows.push(row),
                Err(error) => {
                    errors += 1;
                    rows.push(
                        serde_json::json!({"id":asset.card.id,"status":"refused","error":error}),
                    );
                }
            }
        }
        self.verify_registry_pins(&registry, support_registry.as_deref())?;
        Ok(serde_json::json!({
            "schema":"openrpg.indexed-library-integrity-audit.v1", "revision":revision,
            "registry_sha256":sha256(&registry),"source_support_registry_sha256":support_registry.as_ref().map(|r|sha256(r)),
            "indexed_count":candidate.assets.len(),"passed_count":candidate.assets.len()-errors,"refused_count":errors,
            "archives_requested":include_archives,"unique_archive_payloads_hashed":archives.len(),
            "scope":"Every indexed GLB or source-support source/meta and declared preview. Optional purchased archives referenced by source-support entries only. Raw FBX dependency closures, Unity parity, shader semantics and rendering are not requalified.",
            "runtime_qualified":false,"human_review":false,"placement_qualified":false,"unity_parity":false,"full_import":false,
            "rows":rows
        }))
    }

    fn read_registry(&self) -> Result<Vec<u8>, String> {
        let path = self.safe_relative("metadata/asset-import-registry.v1.json")?;
        read_limited(&path, MAX_JSON)
    }

    fn read_source_support_registry(&self) -> Result<Option<Vec<u8>>, String> {
        let registry_path = self.root.join(SOURCE_SUPPORT_REGISTRY);
        if !registry_path.exists() {
            return Ok(None);
        }
        let registry_path = self.safe_relative(SOURCE_SUPPORT_REGISTRY)?;
        read_limited(&registry_path, MAX_JSON).map(Some)
    }

    fn read_source_support_catalog(
        &self,
        registry_raw: Option<&[u8]>,
    ) -> Result<Option<SourceSupportCatalog>, String> {
        let Some(registry_raw) = registry_raw else {
            return Ok(None);
        };
        let registry: Value = parse_json(registry_raw, "registre des sources conservées")?;
        if registry.get("schema_version").and_then(Value::as_u64) != Some(1) {
            return Err("schéma du registre des sources conservées non pris en charge".to_owned());
        }
        let expected = required(&registry, "sha256")?;
        let catalog_path = self.safe_relative(required(&registry, "path")?)?;
        let catalog_raw = read_limited(&catalog_path, MAX_JSON)?;
        if !same_hash(&catalog_raw, expected) {
            return Err("empreinte du catalogue des sources conservées incohérente".to_owned());
        }
        if registry.get("revision").and_then(Value::as_str) != Some(expected) {
            return Err("révision du catalogue des sources conservées incohérente".to_owned());
        }
        Ok(Some(SourceSupportCatalog {
            registry_raw: registry_raw.to_vec(),
            catalog_raw,
        }))
    }

    fn build_snapshot(
        &self,
        registry_raw: &[u8],
        source_support_raw: Option<&[u8]>,
        source_support_registry_raw: Option<&[u8]>,
        revision: String,
    ) -> Result<Snapshot, String> {
        let registry: Value = parse_json(registry_raw, "registre")?;
        if registry.get("schema_version").and_then(Value::as_u64) != Some(1) {
            return Err("schéma de registre non pris en charge".to_owned());
        }
        let packs = registry
            .get("packs")
            .and_then(Value::as_object)
            .ok_or_else(|| "packs absent du registre".to_owned())?;
        if packs.len() > MAX_PACKS {
            return Err("trop de packs dans le registre".to_owned());
        }
        let mut assets = Vec::new();
        let mut total_json = registry_raw.len() as u64;
        let mut cache = BuildCache::default();
        for (pack_id, entry) in packs {
            let path = entry
                .get("path")
                .and_then(Value::as_str)
                .ok_or_else(|| "chemin de pack absent".to_owned())?;
            let pack_dir = self.safe_relative(path)?;
            let files = entry
                .get("files")
                .and_then(Value::as_object)
                .ok_or_else(|| "empreintes de pack absentes".to_owned())?;
            let details_raw =
                self.checked_pack_file(&pack_dir, files, "details.v1.json", &mut total_json)?;
            let profiles_raw =
                self.checked_pack_file(&pack_dir, files, "profiles.v1.json", &mut total_json)?;
            let details: Value = parse_json(&details_raw, "details")?;
            let profiles: Value = parse_json(&profiles_raw, "profiles")?;
            if details.get("schema_version").and_then(Value::as_u64) != Some(1)
                || profiles.get("schema_version").and_then(Value::as_u64) != Some(1)
                || details.get("pack_id").and_then(Value::as_str) != Some(pack_id)
                || profiles
                    .get("pack_id")
                    .is_some_and(|value| value.as_str() != Some(pack_id))
            {
                return Err(format!("métadonnées incohérentes pour {pack_id}"));
            }
            let profile_list = profiles
                .get("profiles")
                .and_then(Value::as_array)
                .ok_or_else(|| "profiles absent".to_owned())?;
            let entries = details
                .get("assets")
                .and_then(Value::as_array)
                .ok_or_else(|| "assets absent".to_owned())?;
            validate_pack_metadata(pack_id, entries, profile_list)?;
            if assets.len().saturating_add(entries.len()) > MAX_ASSETS {
                return Err("limite d’assets dépassée".to_owned());
            }
            let profile_index = profile_index(profile_list)?;
            for item in entries {
                let id = item
                    .get("id")
                    .and_then(Value::as_str)
                    .unwrap_or("<identifiant absent>");
                assets.push(
                    self.make_asset(
                        pack_id,
                        item,
                        profile_list,
                        &profile_index,
                        &mut total_json,
                        &mut cache,
                    )
                    .map_err(|error| format!("{pack_id}/{id}: {error}"))?,
                );
            }
        }
        if let Some(raw) = source_support_raw {
            self.append_source_support_assets(raw, &mut total_json, &mut assets, &mut cache)?;
        }
        let mut by_id = HashMap::with_capacity(assets.len());
        for (index, asset) in assets.iter().enumerate() {
            if asset.card.id.len() > MAX_ID || by_id.insert(asset.card.id.clone(), index).is_some()
            {
                return Err("identifiant d'asset invalide ou dupliqué".to_owned());
            }
        }
        Ok(Snapshot {
            revision,
            registry_raw: registry_raw.to_vec(),
            source_support_registry_raw: source_support_registry_raw.map(<[u8]>::to_vec),
            assets,
            by_id,
            #[cfg(test)]
            build_cache_stats: cache.stats,
        })
    }

    fn append_source_support_assets(
        &self,
        raw: &[u8],
        total_json: &mut u64,
        assets: &mut Vec<StoredAsset>,
        cache: &mut BuildCache,
    ) -> Result<(), String> {
        *total_json = total_json.saturating_add(raw.len() as u64);
        if *total_json > MAX_TOTAL_JSON {
            return Err("limite JSON totale dépassée".to_owned());
        }
        let support: Value = parse_json(raw, "catalogue des sources conservées")?;
        if support.get("schema_version").and_then(Value::as_u64) != Some(1) {
            return Err("schéma du catalogue des sources conservées non pris en charge".to_owned());
        }
        let entries = support
            .get("assets")
            .and_then(Value::as_array)
            .ok_or_else(|| "assets absent du catalogue des sources conservées".to_owned())?;
        if assets.len().saturating_add(entries.len()) > MAX_ASSETS {
            return Err("limite d'assets dépassée".to_owned());
        }
        for item in entries {
            let support_pack = required(item, "pack_id")?;
            let id = item
                .get("id")
                .and_then(Value::as_str)
                .unwrap_or("<identifiant absent>");
            assets.push(
                self.make_source_support_asset(item, cache)
                    .map_err(|error| format!("{support_pack}/{id}: {error}"))?,
            );
        }
        Ok(())
    }

    fn checked_pack_file(
        &self,
        pack: &Path,
        hashes: &serde_json::Map<String, Value>,
        name: &str,
        total: &mut u64,
    ) -> Result<Vec<u8>, String> {
        let expected = hashes
            .get(name)
            .and_then(Value::as_str)
            .ok_or_else(|| format!("empreinte {name} absente"))?;
        let path = self.safe_from(pack, name)?;
        let raw = read_limited(&path, MAX_JSON)?;
        *total = total.saturating_add(raw.len() as u64);
        if *total > MAX_TOTAL_JSON || !same_hash(&raw, expected) {
            return Err(format!("limite ou empreinte invalide pour {name}"));
        }
        Ok(raw)
    }

    #[expect(
        clippy::too_many_lines,
        reason = "Asset metadata and provenance are validated before a complete immutable stored asset is returned"
    )]
    fn make_asset(
        &self,
        pack: &str,
        item: &Value,
        profiles: &[Value],
        profile_index: &HashMap<&str, usize>,
        total: &mut u64,
        cache: &mut BuildCache,
    ) -> Result<StoredAsset, String> {
        let id = required(item, "id")?;
        if !id.starts_with(&format!("{pack}.")) {
            return Err(format!("asset hors pack: {id}"));
        }
        #[cfg(test)]
        {
            cache.stats.profile_lookups += 1;
        }
        let profile = profile_index
            .get(id)
            .and_then(|index| profiles.get(*index))
            .cloned()
            .ok_or_else(|| format!("profil absent pour {id}"))?;
        let catalog_reference = required(item, "source_catalog")?;
        let catalog_key = (self.root.clone(), catalog_reference.to_owned());
        let already_loaded = cache
            .canonical_paths
            .get(&catalog_key)
            .filter(|path| cache.catalogs.contains_key(*path))
            .cloned();
        let catalog_path = if let Some(path) = already_loaded {
            inside(&self.root, &path)?;
            #[cfg(test)]
            {
                cache.stats.canonical_hits += 1;
            }
            path
        } else {
            cache.safe_absolute(self, catalog_reference)?
        };
        if cache.catalogs.contains_key(&catalog_path) {
            #[cfg(test)]
            {
                cache.stats.catalog_cache_hits += 1;
            }
        } else {
            let catalog_raw = read_limited(&catalog_path, MAX_JSON)?;
            *total = total.saturating_add(catalog_raw.len() as u64);
            if *total > MAX_TOTAL_JSON {
                return Err("limite JSON totale dépassée".to_owned());
            }
            let mut catalog = parse_json(&catalog_raw, "catalogue")?;
            validate_catalog(pack, &catalog)?;
            let root = catalog_path
                .parent()
                .ok_or_else(|| "catalogue sans répertoire".to_owned())?
                .to_path_buf();
            let by_id = catalog_index(&catalog)?;
            // Keep one immutable copy of each catalog row across the build
            // cache and the snapshot. Full JSON is cloned only for the single
            // requested detail response, rather than for the whole library.
            let catalog_assets = std::mem::take(
                catalog
                    .get_mut("assets")
                    .and_then(Value::as_array_mut)
                    .expect("validated catalog assets"),
            )
            .into_iter()
            .map(Arc::new)
            .collect();
            cache.catalogs.insert(
                catalog_path.clone(),
                CatalogIndex {
                    assets: catalog_assets,
                    by_id,
                    root,
                },
            );
            #[cfg(test)]
            {
                cache.stats.catalog_cache_misses += 1;
            }
        }
        #[cfg(test)]
        {
            cache.stats.catalog_lookups += 1;
        }
        let catalog = cache.catalogs.get(&catalog_path).expect("catalogue inséré");
        let catalog_index = catalog
            .by_id
            .get(id)
            .copied()
            .ok_or_else(|| format!("asset {id} absent du catalogue"))?;
        let (catalog_root, catalog_item) = (
            catalog.root.clone(),
            catalog
                .assets
                .get(catalog_index)
                .cloned()
                .ok_or_else(|| format!("asset {id} absent du catalogue"))?,
        );
        let model_reference = required(&catalog_item, "model")?.to_owned();
        validate_relative(&model_reference)?;
        let declared_model = self.lexical_absolute(required(item, "model_path")?)?;
        let asset_root =
            self.lexical_asset_root(&catalog_root, &model_reference, &declared_model)?;
        if !valid_hash(required(item, "model_sha256")?)
            || !same_value(item, "model_sha256", &catalog_item, "sha256")
            || profile.get("model_content_sha256") != item.get("model_sha256")
        {
            return Err(format!("modèle incohérent pour {id}"));
        }
        let preview = match (
            catalog_item.get("preview").and_then(Value::as_str),
            catalog_item.get("preview_sha256").and_then(Value::as_str),
        ) {
            (Some(path), Some(hash)) => {
                if !valid_hash(hash) {
                    return Err(format!("empreinte de vignette invalide pour {id}"));
                }
                Some((self.lexical_from(&asset_root, path)?, hash.to_owned()))
            }
            (None, None) => None,
            _ => return Err(format!("vignette incohérente pour {id}")),
        };
        let preview_reference = catalog_item
            .get("preview")
            .and_then(Value::as_str)
            .map(str::to_owned);
        Ok(StoredAsset {
            card: library_card(pack, id, item, preview.is_some(), "converted_model"),
            details: Arc::new(item.clone()),
            profile,
            catalog_asset: catalog_item,
            license: item
                .get("license")
                .and_then(Value::as_str)
                .unwrap_or_default()
                .to_owned(),
            model_path: Some(declared_model.display().to_string()),
            source_path: None,
            preview_reference,
            catalog_root: Some(catalog_root),
            preview,
        })
    }

    fn make_source_support_asset(
        &self,
        item: &Value,
        _cache: &mut BuildCache,
    ) -> Result<StoredAsset, String> {
        let id = required(item, "id")?;
        let pack = required(item, "pack_id")?;
        if !id.starts_with(&format!("{pack}."))
            || item.get("availability").and_then(Value::as_str) != Some("source_support")
        {
            return Err(format!("source conservée incohérente: {id}"));
        }
        let source = self.lexical_from(&self.root, required(item, "source_file")?)?;
        let expected = required(item, "source_sha256")?;
        if !valid_hash(expected) {
            return Err(format!("source conservée absente ou invalide: {id}"));
        }
        self.lexical_from(&self.root, required(item, "source_meta_file")?)?;
        let expected_meta = required(item, "source_meta_sha256")?;
        if !valid_hash(expected_meta) {
            return Err(format!(
                "métadonnées Unity de source absentes ou invalides: {id}"
            ));
        }
        self.lexical_from(&self.root, required(item, "source_archive")?)?;
        if item
            .get("source_archive_sha256")
            .and_then(Value::as_str)
            .is_none_or(|hash| !valid_hash(hash))
        {
            return Err(format!("archive source absente ou invalide: {id}"));
        }
        let preview = match (
            item.get("preview").and_then(Value::as_str),
            item.get("preview_sha256").and_then(Value::as_str),
        ) {
            (Some(path), Some(hash)) => {
                if !valid_hash(hash) {
                    return Err(format!("empreinte de vignette invalide pour {id}"));
                }
                Some((self.lexical_from(&self.root, path)?, hash.to_owned()))
            }
            (None, None) => None,
            _ => return Err(format!("vignette incohérente pour {id}")),
        };
        let profile = serde_json::json!({
            "schema_version": 1,
            "external_id": id,
            "readiness": "source-support",
            "review_status": "source-preserved-not-placeable",
            "support_kind": item.get("support_kind").cloned().unwrap_or(Value::Null),
            "reason": item.get("reason").cloned().unwrap_or(Value::Null),
            "source_asset_path": item.get("source_asset_path").cloned().unwrap_or(Value::Null)
        });
        let shared_details = Arc::new(item.clone());
        Ok(StoredAsset {
            card: library_card(pack, id, item, preview.is_some(), "source_support"),
            details: Arc::clone(&shared_details),
            profile,
            catalog_asset: shared_details,
            license: item
                .get("license")
                .and_then(Value::as_str)
                .unwrap_or_default()
                .to_owned(),
            model_path: None,
            source_path: Some(source.display().to_string()),
            preview_reference: item
                .get("preview")
                .and_then(Value::as_str)
                .map(str::to_owned),
            catalog_root: None,
            preview,
        })
    }

    fn verify_source_support_files(&self, item: &Value) -> Result<(), String> {
        let source = self.safe_relative(required(item, "source_file")?)?;
        if !same_file_hash(
            &source,
            required(item, "source_sha256")?,
            MAX_SOURCE_SUPPORT,
        )? {
            return Err("empreinte de source conservée incohérente".to_owned());
        }
        let meta = self.safe_relative(required(item, "source_meta_file")?)?;
        if !same_file_hash(&meta, required(item, "source_meta_sha256")?, MAX_JSON)? {
            return Err("empreinte des métadonnées Unity incohérente".to_owned());
        }
        Ok(())
    }

    fn search(&self, snapshot: &Snapshot, query: &LibraryQuery) -> Result<LibraryPage, String> {
        if query.text.len() > MAX_QUERY {
            return Err("requête trop longue".to_owned());
        }
        for value in [
            &query.pack,
            &query.family,
            &query.asset_type,
            &query.subtype,
            &query.availability,
            &query.status,
            &query.sort,
        ] {
            if value.len() > MAX_QUERY {
                return Err("filtre trop long".to_owned());
            }
        }
        if !matches!(
            query.status.as_str(),
            "" | "metadata_ready" | "needs_review" | "runtime_qualified"
        ) || !matches!(
            query.availability.as_str(),
            "" | "converted_model" | "source_support"
        ) || !matches!(query.sort.as_str(), "" | "name" | "triangles" | "height")
        {
            return Err("filtre ou tri non pris en charge".to_owned());
        }
        let mut matched: Vec<&StoredAsset> = snapshot
            .assets
            .iter()
            .filter(|asset| matches_query(asset, query))
            .collect();
        matched.sort_by(|a, b| match query.sort.as_str() {
            "triangles" => a
                .card
                .triangles
                .cmp(&b.card.triangles)
                .then_with(|| a.card.id.cmp(&b.card.id)),
            "height" => a.card.dimensions[1]
                .total_cmp(&b.card.dimensions[1])
                .then_with(|| a.card.id.cmp(&b.card.id)),
            _ => normalized(&a.card.name)
                .cmp(&normalized(&b.card.name))
                .then_with(|| a.card.id.cmp(&b.card.id)),
        });
        let limit = if query.limit == 0 {
            DEFAULT_LIMIT
        } else {
            query.limit.min(MAX_LIMIT)
        };
        let global = snapshot.assets.iter().collect::<Vec<_>>();
        let facets = facets(&global);
        let assets = matched
            .iter()
            .skip(query.offset)
            .take(limit)
            .map(|asset| {
                self.resolve_access(asset, false)?;
                Ok(asset.card.clone())
            })
            .collect::<Result<Vec<_>, String>>()?;
        Ok(LibraryPage {
            revision: snapshot.revision.clone(),
            vault_path: self.root.display().to_string(),
            total_assets: snapshot.assets.len(),
            matched_assets: matched.len(),
            offset: query.offset,
            limit,
            facets,
            assets,
        })
    }

    fn pinned_asset<'a>(
        snapshot: &'a Snapshot,
        id: &str,
        revision: &str,
    ) -> Result<&'a StoredAsset, String> {
        if id.len() > MAX_ID || revision != snapshot.revision {
            return Err("révision périmée ou identifiant invalide".to_owned());
        }
        snapshot
            .by_id
            .get(id)
            .and_then(|index| snapshot.assets.get(*index))
            .ok_or_else(|| "asset inconnu".to_owned())
    }

    fn lexical_from(&self, base: &Path, value: &str) -> Result<PathBuf, String> {
        validate_relative(value)?;
        inside(&self.root, base)?;
        let path = base.join(value);
        inside(base, &path)?;
        inside(&self.root, &path)?;
        Ok(path)
    }

    fn lexical_absolute(&self, value: &str) -> Result<PathBuf, String> {
        let path = Path::new(value);
        if !path.is_absolute()
            || path
                .components()
                .any(|part| matches!(part, Component::ParentDir))
        {
            return Err("chemin absolu confiné attendu".to_owned());
        }
        let base: Vec<_> = self.root.components().collect();
        let parts: Vec<_> = path
            .components()
            .filter(|part| !matches!(part, Component::CurDir))
            .collect();
        if parts.len() <= base.len()
            || !base
                .iter()
                .zip(&parts)
                .all(|(a, b)| reference_component_equal(*a, *b))
        {
            return Err("chemin hors racine autorisée".to_owned());
        }
        let mut confined = self.root.clone();
        for part in &parts[base.len()..] {
            match part {
                Component::Normal(name) => confined.push(name),
                _ => return Err("composant de chemin individuel refusé".to_owned()),
            }
        }
        Ok(confined)
    }

    fn lexical_asset_root(
        &self,
        catalog_root: &Path,
        reference: &str,
        model: &Path,
    ) -> Result<PathBuf, String> {
        let relative = Path::new(reference);
        validate_relative(reference)?;
        if model.ends_with(relative) {
            let mut root = model.to_path_buf();
            for _ in relative.components() {
                if !root.pop() {
                    return Err("racine d'asset introuvable".to_owned());
                }
            }
            inside(&self.root, &root)?;
            Ok(root)
        } else {
            // Actual aliases need a real filesystem resolution on access.
            Ok(catalog_root.to_path_buf())
        }
    }

    fn resolve_access(&self, asset: &StoredAsset, full_hash: bool) -> Result<AccessPaths, String> {
        #[cfg(test)]
        self.physical_access_checks
            .set(self.physical_access_checks.get().saturating_add(1));
        let resolve = || -> Result<AccessPaths, String> {
            let (model, source, base) = if asset.card.availability == "converted_model" {
                let declared = self.safe_absolute(
                    asset
                        .model_path
                        .as_deref()
                        .ok_or_else(|| "référence de modèle absente".to_owned())?,
                )?;
                let metadata =
                    fs::metadata(&declared).map_err(|error| format!("lecture refusée: {error}"))?;
                if !metadata.is_file() {
                    return Err("modèle absent ou non régulier".to_owned());
                }
                let catalog_root = asset
                    .catalog_root
                    .as_deref()
                    .ok_or_else(|| "racine de catalogue absente".to_owned())?;
                let mut paths = BuildCache::default();
                let base = self.catalog_asset_root(
                    catalog_root,
                    required(&asset.catalog_asset, "model")?,
                    &declared,
                    &mut paths,
                )?;
                if full_hash
                    && !same_file_hash(
                        &declared,
                        required(&asset.details, "model_sha256")?,
                        metadata.len(),
                    )?
                {
                    return Err("empreinte de modèle incohérente".to_owned());
                }
                (Some(declared), None, base)
            } else {
                let source = self.safe_absolute(
                    asset
                        .source_path
                        .as_deref()
                        .ok_or_else(|| "référence de source absente".to_owned())?,
                )?;
                let meta = self.safe_relative(required(&asset.details, "source_meta_file")?)?;
                if !regular_file_within_limit(&source, MAX_SOURCE_SUPPORT)?
                    || !regular_file_within_limit(&meta, MAX_JSON)?
                {
                    return Err("source conservée ou métadonnées absentes, non régulières ou trop volumineuses".to_owned());
                }
                let archive = self.safe_relative(required(&asset.details, "source_archive")?)?;
                if !archive.is_file() {
                    return Err("archive source absente ou non régulière".to_owned());
                }
                // Historical source/.meta integrity guards remain fresh for every
                // selected support, including Search and Thumbnail. Only model
                // payload hashing is deferred until Details or the global audit.
                self.verify_source_support_files(&asset.details)?;
                (None, Some(source), self.root.clone())
            };
            let preview = match (&asset.preview_reference, &asset.preview) {
                (Some(reference), Some((_, expected))) => {
                    let path = self.safe_from(&base, reference)?;
                    if !regular_file_within_limit(&path, MAX_THUMBNAIL)? {
                        return Err(
                            "vignette absente, non régulière ou trop volumineuse".to_owned()
                        );
                    }
                    Some((path, expected.clone()))
                }
                (None, None) => None,
                _ => return Err("référence de vignette incohérente".to_owned()),
            };
            Ok(AccessPaths {
                model,
                source,
                preview,
            })
        };
        resolve().map_err(|error| format!("{}/{}: {error}", asset.card.pack, asset.card.id))
    }

    fn safe_relative(&self, value: &str) -> Result<PathBuf, String> {
        self.safe_from(&self.root, value)
    }
    fn safe_absolute(&self, value: &str) -> Result<PathBuf, String> {
        let path = Path::new(value);
        if !path.is_absolute() {
            return Err("chemin absolu attendu".to_owned());
        }
        let path = path
            .canonicalize()
            .map_err(|error| format!("chemin introuvable: {error}"))?;
        inside(&self.root, &path)?;
        Ok(path)
    }
    fn catalog_asset_root(
        &self,
        catalog_root: &Path,
        model_reference: &str,
        declared_model: &Path,
        cache: &mut BuildCache,
    ) -> Result<PathBuf, String> {
        let relative = Path::new(model_reference);
        validate_relative(model_reference)?;

        if let Some(direct) = cache.catalog_model_path(self, catalog_root, model_reference)?
            && direct == declared_model
        {
            return Ok(catalog_root.to_path_buf());
        }

        if !declared_model.ends_with(relative) {
            return Err("le modèle déclaré ne correspond pas au catalogue".to_owned());
        }
        let mut asset_root = declared_model.to_path_buf();
        for _ in relative.components() {
            if !asset_root.pop() {
                return Err("racine d'asset introuvable".to_owned());
            }
        }
        inside(&self.root, &asset_root)?;
        if !asset_root.is_dir() {
            return Err("racine d'asset introuvable".to_owned());
        }
        Ok(asset_root)
    }
    fn safe_from(&self, base: &Path, value: &str) -> Result<PathBuf, String> {
        validate_relative(value)?;
        let relative = Path::new(value);
        let path = base
            .join(relative)
            .canonicalize()
            .map_err(|error| format!("chemin introuvable: {error}"))?;
        inside(base, &path)?;
        inside(&self.root, &path)?;
        Ok(path)
    }
}

struct AccessPaths {
    model: Option<PathBuf>,
    source: Option<PathBuf>,
    preview: Option<(PathBuf, String)>,
}

fn reference_component_equal(left: Component<'_>, right: Component<'_>) -> bool {
    if left == right {
        return true;
    }
    #[cfg(windows)]
    {
        use std::path::Prefix;
        match (left, right) {
            (Component::Prefix(a), Component::Prefix(b)) => match (a.kind(), b.kind()) {
                (
                    Prefix::Disk(x) | Prefix::VerbatimDisk(x),
                    Prefix::Disk(y) | Prefix::VerbatimDisk(y),
                ) => x.eq_ignore_ascii_case(&y),
                (
                    Prefix::UNC(a, b) | Prefix::VerbatimUNC(a, b),
                    Prefix::UNC(c, d) | Prefix::VerbatimUNC(c, d),
                ) => a == c && b == d,
                _ => false,
            },
            (Component::Normal(a), Component::Normal(b)) => a
                .to_str()
                .zip(b.to_str())
                .is_some_and(|(a, b)| a.eq_ignore_ascii_case(b)),
            _ => false,
        }
    }
    #[cfg(not(windows))]
    {
        false
    }
}

fn library_card(
    pack: &str,
    id: &str,
    item: &Value,
    has_thumbnail: bool,
    availability: &str,
) -> LibraryCard {
    let readiness = item.get("readiness").and_then(Value::as_object);
    let dimensions = item
        .pointer("/measurement/dimensions")
        .and_then(Value::as_array)
        .filter(|d| d.len() == 3)
        .map_or([0.0; 3], |d| [number(&d[0]), number(&d[1]), number(&d[2])]);
    LibraryCard {
        id: id.to_owned(),
        name: localized(item.get("names")),
        description: localized(item.get("descriptions")),
        pack: pack.to_owned(),
        family: taxonomy(item, "family"),
        asset_type: taxonomy(item, "type"),
        subtype: taxonomy(item, "subtype"),
        availability: availability.to_owned(),
        tags: strings(item.get("tags")),
        dimensions,
        triangles: item
            .pointer("/measurement/triangles")
            .and_then(Value::as_u64)
            .unwrap_or(0),
        metadata_ready: readiness
            .and_then(|r| r.get("metadata_ready"))
            .and_then(Value::as_bool)
            .unwrap_or(false),
        runtime_qualified: readiness
            .and_then(|r| r.get("runtime_qualified"))
            .and_then(Value::as_bool)
            .unwrap_or(false),
        has_thumbnail,
    }
}
fn taxonomy(item: &Value, field: &str) -> String {
    item.pointer(&format!("/taxonomy/{field}"))
        .and_then(Value::as_str)
        .unwrap_or_default()
        .to_owned()
}
fn matches_query(asset: &StoredAsset, query: &LibraryQuery) -> bool {
    let eq = |left: &str, right: &str| right.is_empty() || normalized(left) == normalized(right);
    if !eq(&asset.card.pack, &query.pack)
        || !eq(&asset.card.family, &query.family)
        || !eq(&asset.card.asset_type, &query.asset_type)
        || !eq(&asset.card.subtype, &query.subtype)
        || !eq(&asset.card.availability, &query.availability)
    {
        return false;
    }
    let status_matches = match query.status.as_str() {
        "" => true,
        "metadata_ready" => asset.card.metadata_ready,
        "needs_review" => !asset.card.metadata_ready || !asset.card.runtime_qualified,
        "runtime_qualified" => asset.card.runtime_qualified,
        _ => false,
    };
    if !status_matches {
        return false;
    }
    let haystack = normalized(&format!(
        "{} {} {} {} {} {} {} {} {}",
        asset.card.id,
        asset.card.name,
        asset.card.description,
        asset.card.pack,
        asset.card.family,
        asset.card.asset_type,
        asset.card.subtype,
        asset.card.availability,
        asset.card.tags.join(" ")
    ));
    normalized(&query.text)
        .split_whitespace()
        .all(|term| haystack.contains(term))
}

fn facets(items: &[&StoredAsset]) -> LibraryFacets {
    let collect = |field: fn(&LibraryCard) -> &String| -> Vec<String> {
        items
            .iter()
            .map(|asset| field(&asset.card).clone())
            .filter(|s| !s.is_empty())
            .collect::<BTreeSet<_>>()
            .into_iter()
            .collect()
    };
    LibraryFacets {
        packs: collect(|c| &c.pack),
        families: collect(|c| &c.family),
        types: collect(|c| &c.asset_type),
        subtypes: collect(|c| &c.subtype),
        availabilities: collect(|c| &c.availability),
    }
}

fn profile_index(profiles: &[Value]) -> Result<HashMap<&str, usize>, String> {
    let mut index = HashMap::with_capacity(profiles.len());
    for (position, profile) in profiles.iter().enumerate() {
        let id = required(profile, "external_id")?;
        if index.insert(id, position).is_some() {
            return Err("identifiant de profil dupliqué".to_owned());
        }
    }
    Ok(index)
}

fn catalog_index(catalog: &Value) -> Result<HashMap<String, usize>, String> {
    let assets = catalog
        .get("assets")
        .and_then(Value::as_array)
        .ok_or_else(|| "assets absent du catalogue".to_owned())?;
    let mut index = HashMap::with_capacity(assets.len());
    for (position, asset) in assets.iter().enumerate() {
        let id = required(asset, "id")?;
        if index.insert(id.to_owned(), position).is_some() {
            return Err("identifiant de catalogue dupliqué".to_owned());
        }
    }
    Ok(index)
}

fn validate_pack_metadata(pack: &str, details: &[Value], profiles: &[Value]) -> Result<(), String> {
    let detail_ids = details
        .iter()
        .map(|asset| required(asset, "id"))
        .collect::<Result<BTreeSet<_>, _>>()?;
    if detail_ids.len() != details.len()
        || detail_ids
            .iter()
            .any(|id| !id.starts_with(&format!("{pack}.")))
    {
        return Err(format!("IDs détails invalides pour {pack}"));
    }
    let profile_ids = profiles
        .iter()
        .map(|profile| required(profile, "external_id"))
        .collect::<Result<BTreeSet<_>, _>>()?;
    if profile_ids.len() != profiles.len() || profile_ids != detail_ids {
        return Err(format!("ensemble de profils incohérent pour {pack}"));
    }
    Ok(())
}
fn validate_catalog(pack: &str, catalog: &Value) -> Result<(), String> {
    if catalog.get("id").and_then(Value::as_str) != Some(pack) {
        return Err(format!("catalogue de pack incohérent pour {pack}"));
    }
    let assets = catalog
        .get("assets")
        .and_then(Value::as_array)
        .ok_or_else(|| "assets absent du catalogue".to_owned())?;
    let ids = assets
        .iter()
        .map(|asset| required(asset, "id"))
        .collect::<Result<BTreeSet<_>, _>>()?;
    if ids.len() != assets.len() || ids.iter().any(|id| !id.starts_with(&format!("{pack}."))) {
        return Err(format!("IDs catalogue invalides pour {pack}"));
    }
    Ok(())
}
fn required<'a>(value: &'a Value, key: &str) -> Result<&'a str, String> {
    value
        .get(key)
        .and_then(Value::as_str)
        .filter(|s| !s.is_empty())
        .ok_or_else(|| format!("{key} absent"))
}
fn localized(value: Option<&Value>) -> String {
    value
        .and_then(|v| v.get("fr").or_else(|| v.get("en")))
        .and_then(Value::as_str)
        .unwrap_or_default()
        .to_owned()
}
fn strings(value: Option<&Value>) -> Vec<String> {
    value
        .and_then(Value::as_array)
        .map(|a| {
            a.iter()
                .filter_map(Value::as_str)
                .map(str::to_owned)
                .collect()
        })
        .unwrap_or_default()
}
fn number(value: &Value) -> f64 {
    value.as_f64().unwrap_or(0.0)
}
fn same_value(left: &Value, left_key: &str, right: &Value, right_key: &str) -> bool {
    left.get(left_key).and_then(Value::as_str) == right.get(right_key).and_then(Value::as_str)
}
fn parse_json(raw: &[u8], label: &str) -> Result<Value, String> {
    serde_json::from_slice(raw).map_err(|error| format!("JSON {label} invalide: {error}"))
}

/// Reserve packet headroom before reading a bounded model. Metadata size is
/// checked first; shrink/growth during reading remains a refusal.
fn read_model_limited(path: &Path, limit: u64) -> Result<Vec<u8>, String> {
    let file = fs::File::open(path).map_err(|error| format!("lecture refusée: {error}"))?;
    let metadata = file
        .metadata()
        .map_err(|error| format!("lecture refusée: {error}"))?;
    if !metadata.is_file() || metadata.len() > limit {
        return Err("fichier absent, non régulier ou trop volumineux".to_owned());
    }
    let body_len = usize::try_from(metadata.len()).map_err(|_| "taille de modèle invalide")?;
    let capacity = body_len
        .checked_add(crate::asset_library_model_wire::MODEL_HEADER_HEADROOM)
        .ok_or("taille de modèle invalide")?;
    let mut bytes = Vec::new();
    bytes
        .try_reserve_exact(capacity)
        .map_err(|error| format!("allocation de modèle refusée: {error}"))?;
    file.take(metadata.len().saturating_add(1))
        .read_to_end(&mut bytes)
        .map_err(|error| format!("lecture impossible: {error}"))?;
    if bytes.len() != body_len {
        return Err("modèle modifié pendant la lecture".to_owned());
    }
    Ok(bytes)
}

fn read_limited(path: &Path, limit: u64) -> Result<Vec<u8>, String> {
    let file = fs::File::open(path).map_err(|error| format!("lecture refusée: {error}"))?;
    if !file
        .metadata()
        .map_err(|error| format!("lecture refusée: {error}"))?
        .is_file()
    {
        return Err("fichier absent, non régulier ou trop volumineux".to_owned());
    }
    let mut bytes = Vec::new();
    file.take(limit.saturating_add(1))
        .read_to_end(&mut bytes)
        .map_err(|error| format!("lecture impossible: {error}"))?;
    if bytes.len() as u64 > limit {
        return Err("fichier absent, non régulier ou trop volumineux".to_owned());
    }
    Ok(bytes)
}
fn sha256(bytes: &[u8]) -> String {
    hex_digest(Sha256::digest(bytes))
}
fn hex_digest(bytes: impl AsRef<[u8]>) -> String {
    use std::fmt::Write;
    bytes
        .as_ref()
        .iter()
        .fold(String::with_capacity(64), |mut hex, byte| {
            let _ = write!(hex, "{byte:02x}");
            hex
        })
}
fn snapshot_revision(registry: &[u8], support: Option<&SourceSupportCatalog>) -> String {
    let mut digest = Sha256::new();
    for bytes in std::iter::once(registry).chain(
        support
            .into_iter()
            .flat_map(|value| [&value.registry_raw[..], &value.catalog_raw[..]]),
    ) {
        digest.update((bytes.len() as u64).to_le_bytes());
        digest.update(bytes);
    }
    hex_digest(digest.finalize())
}
fn valid_hash(expected: &str) -> bool {
    expected.len() == 64 && expected.bytes().all(|byte| byte.is_ascii_hexdigit())
}
fn same_hash(bytes: &[u8], expected: &str) -> bool {
    valid_hash(expected) && sha256(bytes).eq_ignore_ascii_case(expected)
}
fn regular_file_within_limit(path: &Path, limit: u64) -> Result<bool, String> {
    let metadata = fs::metadata(path).map_err(|error| format!("lecture refusée: {error}"))?;
    Ok(metadata.is_file() && metadata.len() <= limit)
}
fn same_file_hash(path: &Path, expected: &str, limit: u64) -> Result<bool, String> {
    if !valid_hash(expected) {
        return Ok(false);
    }
    let mut file = fs::File::open(path).map_err(|error| format!("lecture refusée: {error}"))?;
    let metadata = file
        .metadata()
        .map_err(|error| format!("lecture refusée: {error}"))?;
    if !metadata.is_file() || metadata.len() > limit {
        return Err("fichier absent, non régulier ou trop volumineux".to_owned());
    }
    let mut digest = Sha256::new();
    let mut buffer = vec![0_u8; 64 * 1024].into_boxed_slice();
    let mut total_read = 0_u64;
    loop {
        let read = file
            .read(&mut buffer)
            .map_err(|error| format!("lecture impossible: {error}"))?;
        if read == 0 {
            break;
        }
        total_read = total_read.saturating_add(read as u64);
        if total_read > limit {
            return Err("fichier agrandi pendant la lecture".to_owned());
        }
        digest.update(&buffer[..read]);
    }
    Ok(hex_digest(digest.finalize()).eq_ignore_ascii_case(expected))
}
fn is_png(bytes: &[u8]) -> bool {
    bytes.starts_with(b"\x89PNG\r\n\x1a\n")
}
fn inside(base: &Path, value: &Path) -> Result<(), String> {
    if value.starts_with(base) {
        Ok(())
    } else {
        Err("chemin hors racine autorisée".to_owned())
    }
}
fn validate_relative(value: &str) -> Result<(), String> {
    let relative = Path::new(value);
    if relative.as_os_str().is_empty()
        || relative.is_absolute()
        || relative.components().any(|component| {
            matches!(
                component,
                Component::ParentDir | Component::RootDir | Component::Prefix(_)
            )
        })
    {
        return Err("sortie de répertoire refusée".to_owned());
    }
    Ok(())
}
fn ensure_path_exists(path: &Path) -> Result<(), String> {
    fs::metadata(path)
        .map(|_| ())
        .map_err(|error| format!("chemin introuvable: {error}"))
}
fn normalized(value: &str) -> String {
    value.chars().map(fold).collect::<String>().to_lowercase()
}
fn fold(c: char) -> char {
    match c {
        'à' | 'á' | 'â' | 'ä' | 'ã' | 'å' | 'À' | 'Á' | 'Â' | 'Ä' | 'Ã' | 'Å' => 'a',
        'ç' | 'Ç' => 'c',
        'è' | 'é' | 'ê' | 'ë' | 'È' | 'É' | 'Ê' | 'Ë' => 'e',
        'ì' | 'í' | 'î' | 'ï' | 'Ì' | 'Í' | 'Î' | 'Ï' => 'i',
        'ñ' | 'Ñ' => 'n',
        'ò' | 'ó' | 'ô' | 'ö' | 'õ' | 'Ò' | 'Ó' | 'Ô' | 'Ö' | 'Õ' => 'o',
        'ù' | 'ú' | 'û' | 'ü' | 'Ù' | 'Ú' | 'Û' | 'Ü' => 'u',
        'ý' | 'ÿ' | 'Ý' => 'y',
        other => other,
    }
}

#[cfg(test)]
mod tests {
    type FixtureMutation = (&'static str, fn(&std::path::Path));
    use super::*;
    use std::time::{SystemTime, UNIX_EPOCH};

    #[test]
    fn vault_model_transport_refuses_preserved_sources_and_invalid_envelopes() {
        let (root, _) = fixture();
        let support_id = add_source_support(&root);
        let mut library = Library::new(&root).unwrap();
        let LibraryResponse::Page { page } = library
            .execute(LibraryRequest::Search {
                query: LibraryQuery::default(),
            })
            .unwrap()
        else {
            panic!()
        };
        assert!(
            library
                .execute(LibraryRequest::Model {
                    id: support_id.clone(),
                    revision: page.revision.clone(),
                })
                .unwrap_err()
                .contains("non plaçable")
        );
        assert!(
            library
                .execute(LibraryRequest::ModelReference {
                    asset_id: vault_asset_id(&support_id),
                    sha256: forge_core::ContentHash::from_bytes([0; 32]),
                })
                .unwrap_err()
                .contains("non plaçable")
        );
        // This fixture has matching metadata hashes but deliberately contains
        // only three bytes. Metadata integrity is not model admission.
        assert!(
            library
                .execute(LibraryRequest::Model {
                    id: "demo.arbre-eclaire".into(),
                    revision: page.revision,
                })
                .unwrap_err()
                .contains("GLB")
        );
        fs::remove_dir_all(root).unwrap();
    }

    #[test]
    fn vault_model_transport_refuses_oversized_payload_before_reading() {
        let (root, revision) = fixture();
        let file = fs::OpenOptions::new()
            .write(true)
            .open(root.join("normalized/demo/models/a.glb"))
            .unwrap();
        file.set_len(MAX_MODEL + 1).unwrap();
        drop(file);
        let mut library = Library::new(&root).unwrap();
        assert!(
            library
                .execute(LibraryRequest::Model {
                    id: "demo.arbre-eclaire".into(),
                    revision,
                })
                .is_err()
        );
        fs::remove_dir_all(root).unwrap();
    }

    #[test]
    fn vault_model_transport_pins_selection_and_persisted_content() {
        let (root, _) = fixture();
        let catalog_path = root.join("normalized/demo/catalog.v1.json");
        let pack = root.join("metadata/rev/demo/one");
        let mut details: Value =
            serde_json::from_slice(&fs::read(pack.join("details.v1.json")).unwrap()).unwrap();
        let mut profiles: Value =
            serde_json::from_slice(&fs::read(pack.join("profiles.v1.json")).unwrap()).unwrap();
        let mut catalog: Value = serde_json::from_slice(&fs::read(&catalog_path).unwrap()).unwrap();
        // Minimal GLB envelope, deliberately not a geometry/runtime qualification.
        let json = br#"{"asset":{"version":"2.0"}}  "#;
        let mut bytes = b"glTF".to_vec();
        bytes.extend_from_slice(&2_u32.to_le_bytes());
        bytes.extend_from_slice(&u32::try_from(20 + json.len()).unwrap().to_le_bytes());
        bytes.extend_from_slice(&u32::try_from(json.len()).unwrap().to_le_bytes());
        bytes.extend_from_slice(b"JSON");
        bytes.extend_from_slice(json);
        let hash = sha256(&bytes);
        details["assets"][0]["model_sha256"] = hash.clone().into();
        profiles["profiles"][0]["model_content_sha256"] = hash.clone().into();
        catalog["assets"][0]["sha256"] = hash.clone().into();
        fs::write(root.join("normalized/demo/models/a.glb"), &bytes).unwrap();
        write_pack_fixture(&root, "demo", &details, &profiles, &catalog, &catalog_path);
        let mut library = Library::new(&root).unwrap();
        let LibraryResponse::Page { page } = library
            .execute(LibraryRequest::Search {
                query: LibraryQuery::default(),
            })
            .unwrap()
        else {
            panic!()
        };
        let id = page.assets[0].id.clone();
        let LibraryResponse::Model { model } = library
            .execute(LibraryRequest::Model {
                id: id.clone(),
                revision: page.revision.clone(),
            })
            .unwrap()
        else {
            panic!()
        };
        assert_eq!(model.bytes, bytes);
        assert_eq!(model.asset_id, vault_asset_id(&id));
        assert!(matches!(
            library
                .execute(LibraryRequest::ModelReference {
                    asset_id: model.asset_id,
                    sha256: model.sha256
                })
                .unwrap(),
            LibraryResponse::Model { .. }
        ));
        assert!(
            library
                .execute(LibraryRequest::Model {
                    id,
                    revision: "stale".into()
                })
                .is_err()
        );
        assert!(
            library
                .execute(LibraryRequest::ModelReference {
                    asset_id: model.asset_id,
                    sha256: forge_core::ContentHash::from_bytes([0; 32])
                })
                .is_err()
        );
        assert!(
            library
                .execute(LibraryRequest::ModelReference {
                    asset_id: vault_asset_id("unknown"),
                    sha256: model.sha256
                })
                .is_err()
        );
        let last = bytes.len() - 1;
        bytes[last] ^= 1;
        fs::write(root.join("normalized/demo/models/a.glb"), bytes).unwrap();
        assert!(
            library
                .execute(LibraryRequest::ModelReference {
                    asset_id: model.asset_id,
                    sha256: model.sha256
                })
                .is_err()
        );
        fs::remove_dir_all(root).unwrap();
    }

    pub(super) fn fixture() -> (PathBuf, String) {
        let root = std::env::temp_dir().join(format!(
            "forge-library-{}",
            SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        let pack = root.join("metadata/rev/demo/one");
        let catalog = root.join("normalized/demo");
        fs::create_dir_all(catalog.join("previews")).unwrap();
        fs::create_dir_all(catalog.join("models")).unwrap();
        fs::create_dir_all(&pack).unwrap();
        let png = b"\x89PNG\r\n\x1a\nfixture";
        fs::write(catalog.join("previews/a.png"), png).unwrap();
        fs::write(catalog.join("models/a.glb"), b"glb").unwrap();
        let id = "demo.arbre-eclaire";
        let detail = serde_json::json!({"schema_version":1,"pack_id":"demo","assets":[{"id":id,"names":{"fr":"Arbre éclairé"},"descriptions":{"fr":"Grand chêne magique"},"tags":["forêt","vert"],"taxonomy":{"family":"nature","type":"tree","subtype":"oak"},"license":"CC0","model_path":catalog.join("models/a.glb").display().to_string(),"model_sha256":sha256(b"glb"),"source_catalog":catalog.join("catalog.v1.json").display().to_string(),"measurement":{"dimensions":[1.0,4.0,2.0],"triangles":12},"readiness":{"metadata_ready":true,"runtime_qualified":false}}]});
        let profile = serde_json::json!({"schema_version":1,"profiles":[{"external_id":id,"model_content_sha256":sha256(b"glb")}]});
        let cat = serde_json::json!({"id":"demo","assets":[{"id":id,"model":"models/a.glb","sha256":sha256(b"glb"),"preview":"previews/a.png","preview_sha256":sha256(png)}]});
        fs::write(
            catalog.join("catalog.v1.json"),
            serde_json::to_vec(&cat).unwrap(),
        )
        .unwrap();
        let d = serde_json::to_vec(&detail).unwrap();
        let p = serde_json::to_vec(&profile).unwrap();
        fs::write(pack.join("details.v1.json"), &d).unwrap();
        fs::write(pack.join("profiles.v1.json"), &p).unwrap();
        let reg = serde_json::json!({"schema_version":1,"packs":{"demo":{"path":"metadata/rev/demo/one","files":{"details.v1.json":sha256(&d),"profiles.v1.json":sha256(&p)}}}});
        let raw = serde_json::to_vec(&reg).unwrap();
        fs::create_dir_all(root.join("metadata")).unwrap();
        fs::write(root.join("metadata/asset-import-registry.v1.json"), &raw).unwrap();
        (root, snapshot_revision(&raw, None))
    }

    #[test]
    fn snapshot_reuses_indexes_and_canonical_paths_for_all_matching_assets() {
        let (root, _) = fixture();
        let pack = root.join("metadata/rev/demo/one");
        let catalog_root = root.join("normalized/demo");
        let details_path = pack.join("details.v1.json");
        let profiles_path = pack.join("profiles.v1.json");
        let catalog_path = catalog_root.join("catalog.v1.json");

        let mut details: Value = serde_json::from_slice(&fs::read(&details_path).unwrap()).unwrap();
        let mut second_details = details["assets"][0].clone();
        second_details["id"] = Value::String("demo.arbre-second".into());
        second_details["names"]["fr"] = Value::String("Second arbre".into());
        details["assets"]
            .as_array_mut()
            .unwrap()
            .push(second_details);
        let details_raw = serde_json::to_vec(&details).unwrap();
        fs::write(&details_path, &details_raw).unwrap();

        let mut profiles: Value =
            serde_json::from_slice(&fs::read(&profiles_path).unwrap()).unwrap();
        let mut second_profile = profiles["profiles"][0].clone();
        second_profile["external_id"] = Value::String("demo.arbre-second".into());
        profiles["profiles"]
            .as_array_mut()
            .unwrap()
            .push(second_profile);
        let profiles_raw = serde_json::to_vec(&profiles).unwrap();
        fs::write(&profiles_path, &profiles_raw).unwrap();

        let mut catalog: Value = serde_json::from_slice(&fs::read(&catalog_path).unwrap()).unwrap();
        let mut second_catalog_asset = catalog["assets"][0].clone();
        second_catalog_asset["id"] = Value::String("demo.arbre-second".into());
        catalog["assets"]
            .as_array_mut()
            .unwrap()
            .push(second_catalog_asset);
        fs::write(&catalog_path, serde_json::to_vec(&catalog).unwrap()).unwrap();

        let registry_path = root.join("metadata/asset-import-registry.v1.json");
        let mut registry: Value =
            serde_json::from_slice(&fs::read(&registry_path).unwrap()).unwrap();
        registry["packs"]["demo"]["files"]["details.v1.json"] = Value::String(sha256(&details_raw));
        registry["packs"]["demo"]["files"]["profiles.v1.json"] =
            Value::String(sha256(&profiles_raw));
        fs::write(&registry_path, serde_json::to_vec(&registry).unwrap()).unwrap();

        let mut library = Library::new(&root).unwrap();
        let LibraryResponse::Page { page } = library
            .execute(LibraryRequest::Search {
                query: LibraryQuery::default(),
            })
            .unwrap()
        else {
            panic!("la recherche doit retourner une page")
        };
        assert_eq!(page.total_assets, 2);
        assert_eq!(
            page.assets
                .iter()
                .map(|asset| asset.id.as_str())
                .collect::<BTreeSet<_>>(),
            BTreeSet::from(["demo.arbre-eclaire", "demo.arbre-second"])
        );

        let stats = &library.snapshot.as_ref().unwrap().build_cache_stats;
        assert_eq!(stats.profile_lookups, 2);
        assert_eq!(stats.catalog_lookups, 2);
        assert_eq!(stats.catalog_cache_misses, 1);
        assert_eq!(stats.catalog_cache_hits, 1);
        assert!(stats.canonical_misses > 0);
        assert!(stats.canonical_hits > 0);
        eprintln!(
            "cache proof: profiles={} catalogs={} catalog_hits={} catalog_misses={} canonical_hits={} canonical_misses={}",
            stats.profile_lookups,
            stats.catalog_lookups,
            stats.catalog_cache_hits,
            stats.catalog_cache_misses,
            stats.canonical_hits,
            stats.canonical_misses
        );
        let _ = fs::remove_dir_all(root);
    }

    fn add_source_support(root: &Path) -> String {
        add_source_support_with_pack(root, "demo")
    }

    fn add_source_support_with_pack(root: &Path, pack: &str) -> String {
        let id = format!("{pack}.source-helper");
        let source_rel = "originals/source-support/helper.fbx";
        let meta_rel = "originals/source-support/helper.fbx.meta";
        let archive_rel = "originals/demo.unitypackage";
        let source = b"source-fbx";
        let meta = b"fileFormatVersion: 2";
        fs::create_dir_all(root.join("originals/source-support")).unwrap();
        fs::write(root.join(source_rel), source).unwrap();
        fs::write(root.join(meta_rel), meta).unwrap();
        fs::write(root.join(archive_rel), b"archive").unwrap();
        let catalog = serde_json::json!({
            "schema":"openrpg.asset-source-support",
            "schema_version":1,
            "private_only":true,
            "assets":[{
                "id":id,
                "pack_id":pack,
                "availability":"source_support",
                "names":{"fr":"Source sans modèle"},
                "descriptions":{"fr":"Nœud Unity conservé sans géométrie visible"},
                "taxonomy":{"family":"source-support","type":"helper","subtype":"empty-fbx-node"},
                "tags":["source-support","not-placeable"],
                "license":"CC0",
                "source_file":source_rel,
                "source_sha256":sha256(source),
                "source_meta_file":meta_rel,
                "source_meta_sha256":sha256(meta),
                "source_archive":archive_rel,
                "source_archive_sha256":sha256(b"archive"),
                "source_asset_path":"Assets/helper.fbx",
                "support_kind":"source_support_no_visible_geometry",
                "reason":"no visible mesh",
                "measurement":{"dimensions":null,"triangles":0},
                "readiness":{"source_preserved":true,"metadata_ready":true,"runtime_qualified":false}
            }]
        });
        let raw = serde_json::to_vec(&catalog).unwrap();
        let hash = sha256(&raw);
        let relative =
            format!("metadata/asset-source-support-revisions/{hash}/source-support.v1.json");
        let catalog_path = root.join(&relative);
        fs::create_dir_all(catalog_path.parent().unwrap()).unwrap();
        fs::write(&catalog_path, raw).unwrap();
        let pointer = serde_json::json!({
            "schema_version":1,
            "path":relative,
            "revision":hash,
            "sha256":hash
        });
        fs::write(
            root.join(SOURCE_SUPPORT_REGISTRY),
            serde_json::to_vec(&pointer).unwrap(),
        )
        .unwrap();
        id
    }

    #[test]
    fn source_support_is_searchable_and_cannot_masquerade_as_a_model() {
        let (root, _) = fixture();
        let id = add_source_support(&root);
        let mut library = Library::new(&root).unwrap();
        let LibraryResponse::Page { page } = library
            .execute(LibraryRequest::Search {
                query: LibraryQuery {
                    availability: "source_support".into(),
                    ..LibraryQuery::default()
                },
            })
            .unwrap()
        else {
            panic!("la recherche doit retourner une page")
        };
        assert_eq!(page.total_assets, 2);
        assert_eq!(page.matched_assets, 1);
        assert_eq!(page.assets[0].id, id);
        assert_eq!(page.assets[0].availability, "source_support");
        match library
            .execute(LibraryRequest::Details {
                id,
                revision: page.revision,
            })
            .unwrap()
        {
            LibraryResponse::Details { detail } => {
                assert!(detail.model_path.is_none());
                assert!(detail.source_path.is_some());
                assert_eq!(detail.profile["readiness"], "source-support");
            }
            _ => panic!("la source conservée doit retourner ses détails"),
        }
        let _ = fs::remove_dir_all(root);
    }

    #[test]
    fn source_support_details_refuse_a_changed_source_after_fast_search() {
        let (root, _) = fixture();
        let id = add_source_support(&root);
        let mut library = Library::new(&root).unwrap();
        let LibraryResponse::Page { page } = library
            .execute(LibraryRequest::Search {
                query: LibraryQuery {
                    availability: "source_support".into(),
                    ..LibraryQuery::default()
                },
            })
            .unwrap()
        else {
            panic!("la recherche doit retourner une page")
        };
        assert_eq!(page.matched_assets, 1);
        fs::write(
            root.join("originals/source-support/helper.fbx"),
            b"changed-fbx",
        )
        .unwrap();
        assert!(
            library
                .execute(LibraryRequest::Details {
                    id,
                    revision: page.revision,
                })
                .is_err()
        );
        let _ = fs::remove_dir_all(root);
    }

    #[test]
    fn source_only_pack_is_searchable_without_a_converted_model() {
        let (root, _) = fixture();
        let id = add_source_support_with_pack(&root, "source-only");
        let mut library = Library::new(&root).unwrap();
        let LibraryResponse::Page { page } = library
            .execute(LibraryRequest::Search {
                query: LibraryQuery {
                    pack: "source-only".into(),
                    availability: "source_support".into(),
                    ..LibraryQuery::default()
                },
            })
            .unwrap()
        else {
            panic!("la recherche doit retourner une page")
        };
        assert_eq!(page.matched_assets, 1);
        assert_eq!(page.assets[0].id, id);
        assert_eq!(page.assets[0].availability, "source_support");
        match library
            .execute(LibraryRequest::Details {
                id,
                revision: page.revision,
            })
            .unwrap()
        {
            LibraryResponse::Details { detail } => assert!(detail.model_path.is_none()),
            _ => panic!("la source conservée doit retourner ses détails"),
        }
        let _ = fs::remove_dir_all(root);
    }

    #[test]
    fn source_support_pointer_change_refreshes_an_open_library() {
        let (root, _) = fixture();
        let mut library = Library::new(&root).unwrap();
        let LibraryResponse::Page { page: before } = library
            .execute(LibraryRequest::Search {
                query: LibraryQuery::default(),
            })
            .unwrap()
        else {
            panic!("la recherche doit retourner une page")
        };
        assert_eq!(before.total_assets, 1);
        let id = add_source_support(&root);
        let LibraryResponse::Page { page: after } = library
            .execute(LibraryRequest::Search {
                query: LibraryQuery::default(),
            })
            .unwrap()
        else {
            panic!("la recherche doit retourner une page")
        };
        assert_eq!(after.total_assets, 2);
        assert_ne!(after.revision, before.revision);
        assert!(
            library
                .execute(LibraryRequest::Details {
                    id: id.clone(),
                    revision: before.revision,
                })
                .is_err()
        );
        assert!(matches!(
            library
                .execute(LibraryRequest::Details {
                    id,
                    revision: after.revision,
                })
                .unwrap(),
            LibraryResponse::Details { .. }
        ));
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn search_is_accent_insensitive_and_filters_metadata() {
        let (root, _) = fixture();
        let mut l = Library::new(&root).unwrap();
        let r = l
            .execute(LibraryRequest::Search {
                query: LibraryQuery {
                    text: "arbre chene".into(),
                    status: "metadata_ready".into(),
                    limit: 1,
                    ..LibraryQuery::default()
                },
            })
            .unwrap();
        match r {
            LibraryResponse::Page { page } => assert_eq!(page.assets[0].id, "demo.arbre-eclaire"),
            _ => panic!(),
        }
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn stale_and_invalid_thumbnail_are_refused() {
        let (root, rev) = fixture();
        let mut l = Library::new(&root).unwrap();
        assert!(
            l.execute(LibraryRequest::Thumbnail {
                id: "demo.arbre-eclaire".into(),
                revision: "stale".into()
            })
            .is_err()
        );
        fs::write(root.join("normalized/demo/previews/a.png"), b"not png").unwrap();
        assert!(
            l.execute(LibraryRequest::Thumbnail {
                id: "demo.arbre-eclaire".into(),
                revision: rev
            })
            .is_err()
        );
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn details_returns_exact_metadata() {
        let (root, rev) = fixture();
        let mut l = Library::new(&root).unwrap();
        match l
            .execute(LibraryRequest::Details {
                id: "demo.arbre-eclaire".into(),
                revision: rev,
            })
            .unwrap()
        {
            LibraryResponse::Details { detail } => {
                assert_eq!(detail.license, "CC0");
                assert_eq!(detail.profile["external_id"], "demo.arbre-eclaire");
            }
            _ => panic!(),
        }
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn registry_path_escape_is_refused() {
        let (root, _) = fixture();
        let raw = fs::read(root.join("metadata/asset-import-registry.v1.json")).unwrap();
        let mut value: Value = serde_json::from_slice(&raw).unwrap();
        value["packs"]["demo"]["path"] = Value::String("../outside".into());
        fs::write(
            root.join("metadata/asset-import-registry.v1.json"),
            serde_json::to_vec(&value).unwrap(),
        )
        .unwrap();
        let mut l = Library::new(&root).unwrap();
        assert!(
            l.execute(LibraryRequest::Search {
                query: LibraryQuery::default()
            })
            .is_err()
        );
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn incorrect_metadata_hash_is_refused() {
        let (root, _) = fixture();
        fs::write(root.join("metadata/rev/demo/one/details.v1.json"), b"{}").unwrap();
        let mut l = Library::new(&root).unwrap();
        assert!(
            l.execute(LibraryRequest::Search {
                query: LibraryQuery::default()
            })
            .is_err()
        );
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn thumbnail_symlink_escape_is_refused_when_supported() {
        let (root, revision) = fixture();
        let mut l = Library::new(&root).unwrap();
        let _ = l
            .execute(LibraryRequest::Search {
                query: LibraryQuery::default(),
            })
            .unwrap();
        let preview = root.join("normalized/demo/previews/a.png");
        let outside = std::env::temp_dir().join("forge-library-outside.png");
        fs::write(&outside, b"\x89PNG\r\n\x1a\noutside").unwrap();
        fs::remove_file(&preview).unwrap();
        if std::os::windows::fs::symlink_file(&outside, &preview).is_ok() {
            assert!(
                l.execute(LibraryRequest::Thumbnail {
                    id: "demo.arbre-eclaire".into(),
                    revision
                })
                .is_err()
            );
        }
        let _ = fs::remove_file(outside);
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn pagination_is_bounded_and_stable() {
        let (root, _) = fixture();
        let mut l = Library::new(&root).unwrap();
        let _ = l
            .execute(LibraryRequest::Search {
                query: LibraryQuery::default(),
            })
            .unwrap();
        let mut snapshot = l.snapshot.clone().unwrap();
        let mut another = snapshot.assets[0].clone();
        another.card.id = "demo.zeta".into();
        another.card.name = "Zèbre".into();
        snapshot.by_id.insert(another.card.id.clone(), 1);
        snapshot.assets.push(another);
        l.snapshot = Some(snapshot);
        match l
            .execute(LibraryRequest::Search {
                query: LibraryQuery {
                    limit: 1,
                    offset: 1,
                    ..LibraryQuery::default()
                },
            })
            .unwrap()
        {
            LibraryResponse::Page { page } => {
                assert_eq!(page.matched_assets, 2);
                assert_eq!(page.assets[0].id, "demo.zeta");
            }
            _ => panic!(),
        }
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    #[ignore = "Requires OPENRPG_ASSET_VAULT pointing to the real private asset library"]
    fn live_source_support_catalog() {
        let root = std::env::var("OPENRPG_ASSET_VAULT")
            .expect("OPENRPG_ASSET_VAULT doit désigner le Vault réel pour ce test ignoré");
        let mut library = Library::new(Path::new(&root)).unwrap();
        let mut offset = 0;
        let mut matched_assets = None;
        let mut total_assets = None;
        let mut ids = BTreeSet::new();
        loop {
            let LibraryResponse::Page { page } = library
                .execute(LibraryRequest::Search {
                    query: LibraryQuery {
                        availability: "source_support".into(),
                        offset,
                        limit: MAX_LIMIT,
                        ..LibraryQuery::default()
                    },
                })
                .unwrap()
            else {
                panic!("la recherche doit retourner une page")
            };
            assert!(page.matched_assets > 0);
            assert!(page.total_assets >= page.matched_assets);
            if let Some(expected) = matched_assets {
                assert_eq!(page.matched_assets, expected);
            } else {
                matched_assets = Some(page.matched_assets);
            }
            if let Some(expected) = total_assets {
                assert_eq!(page.total_assets, expected);
            } else {
                total_assets = Some(page.total_assets);
            }
            let page_len = page.assets.len();
            for card in page.assets {
                assert!(ids.insert(card.id.clone()), "ID dupliqué: {}", card.id);
                assert_eq!(card.availability, "source_support");
                match library
                    .execute(LibraryRequest::Details {
                        id: card.id,
                        revision: page.revision.clone(),
                    })
                    .unwrap()
                {
                    LibraryResponse::Details { detail } => {
                        assert!(detail.model_path.is_none());
                        assert!(detail.source_path.is_some());
                    }
                    _ => panic!("la source conservée doit retourner ses détails"),
                }
            }
            offset += page_len;
            if offset >= page.matched_assets {
                break;
            }
            assert!(
                page_len > 0,
                "la pagination source-support ne doit pas stagner"
            );
        }
        let matched_assets = matched_assets.unwrap();
        assert_eq!(ids.len(), matched_assets);
        eprintln!(
            "Asset Vault source support checked: {} records, {} total library entries",
            matched_assets,
            total_assets.unwrap()
        );
    }

    #[test]
    #[ignore = "Requires OPENRPG_ASSET_VAULT pointing to the real private asset library"]
    #[allow(
        clippy::float_cmp,
        reason = "Search and detail responses must preserve identical stored dimensions"
    )]
    fn live_vault_library() {
        let root = std::env::var("OPENRPG_ASSET_VAULT")
            .expect("OPENRPG_ASSET_VAULT doit désigner le Vault réel pour ce test ignoré");
        let mut l = Library::new(Path::new(&root)).unwrap();
        let mut offset = 0;
        let mut total = None;
        let mut ids = BTreeSet::new();
        let mut packs = BTreeSet::new();
        loop {
            let LibraryResponse::Page { page } = l
                .execute(LibraryRequest::Search {
                    query: LibraryQuery {
                        offset,
                        limit: MAX_LIMIT,
                        ..LibraryQuery::default()
                    },
                })
                .unwrap()
            else {
                panic!("la recherche doit retourner une page")
            };
            assert_eq!(page.matched_assets, page.total_assets);
            if let Some(expected_total) = total {
                assert_eq!(page.total_assets, expected_total);
            } else {
                total = Some(page.total_assets);
            }
            for card in &page.assets {
                assert!(ids.insert(card.id.clone()), "ID dupliqué: {}", card.id);
                assert!(page.facets.packs.contains(&card.pack));
                packs.insert(card.pack.clone());
                match l
                    .execute(LibraryRequest::Details {
                        id: card.id.clone(),
                        revision: page.revision.clone(),
                    })
                    .unwrap()
                {
                    LibraryResponse::Details { detail } => {
                        assert_eq!(detail.card.id, card.id);
                        assert_eq!(detail.card.name, card.name);
                        assert_eq!(detail.card.description, card.description);
                        assert_eq!(detail.card.pack, card.pack);
                        assert_eq!(detail.card.family, card.family);
                        assert_eq!(detail.card.asset_type, card.asset_type);
                        assert_eq!(detail.card.subtype, card.subtype);
                        assert_eq!(detail.card.availability, card.availability);
                        assert_eq!(detail.card.tags, card.tags);
                        assert_eq!(detail.card.dimensions, card.dimensions);
                        assert_eq!(detail.card.triangles, card.triangles);
                        assert_eq!(detail.card.metadata_ready, card.metadata_ready);
                        assert_eq!(detail.card.runtime_qualified, card.runtime_qualified);
                        assert_eq!(detail.card.has_thumbnail, card.has_thumbnail);
                        if card.availability == "source_support" {
                            assert!(detail.model_path.is_none());
                            assert!(detail.source_path.is_some());
                        } else {
                            assert!(detail.model_path.is_some());
                        }
                    }
                    _ => panic!("un asset doit retourner ses détails"),
                }
                if card.has_thumbnail {
                    match l
                        .execute(LibraryRequest::Thumbnail {
                            id: card.id.clone(),
                            revision: page.revision.clone(),
                        })
                        .unwrap()
                    {
                        LibraryResponse::Thumbnail { bytes } => assert!(is_png(&bytes)),
                        _ => panic!("un asset avec vignette doit retourner un PNG"),
                    }
                }
            }
            offset = offset.saturating_add(page.assets.len());
            if offset >= page.total_assets {
                assert_eq!(offset, page.total_assets);
                assert_eq!(packs.len(), page.facets.packs.len());
                break;
            }
            assert_eq!(
                page.assets.len(),
                page.limit,
                "page intermédiaire incomplète"
            );
        }
        assert_eq!(ids.len(), total.expect("au moins une page"));
        eprintln!(
            "Asset Vault library checked: {} assets, {} packs",
            ids.len(),
            packs.len()
        );
    }

    fn write_pack_fixture(
        root: &Path,
        pack: &str,
        details: &Value,
        profiles: &Value,
        catalog: &Value,
        catalog_path: &Path,
    ) {
        let pack_dir = root.join(format!("metadata/rev/{pack}/one"));
        fs::create_dir_all(&pack_dir).unwrap();
        fs::create_dir_all(catalog_path.parent().unwrap()).unwrap();
        fs::write(catalog_path, serde_json::to_vec(catalog).unwrap()).unwrap();
        let d = serde_json::to_vec(details).unwrap();
        let p = serde_json::to_vec(profiles).unwrap();
        fs::write(pack_dir.join("details.v1.json"), &d).unwrap();
        fs::write(pack_dir.join("profiles.v1.json"), &p).unwrap();
        let reg_path = root.join("metadata/asset-import-registry.v1.json");
        let mut registry: Value = serde_json::from_slice(&fs::read(&reg_path).unwrap()).unwrap();
        registry["packs"][pack] = serde_json::json!({"path":format!("metadata/rev/{pack}/one"),"files":{"details.v1.json":sha256(&d),"profiles.v1.json":sha256(&p)}});
        fs::write(reg_path, serde_json::to_vec(&registry).unwrap()).unwrap();
    }
    fn add_second_model(root: &Path, materialize: bool) -> String {
        let pack_dir = root.join("metadata/rev/demo/one");
        let cat_path = root.join("normalized/demo/catalog.v1.json");
        let mut details: Value =
            serde_json::from_slice(&fs::read(pack_dir.join("details.v1.json")).unwrap()).unwrap();
        let mut profiles: Value =
            serde_json::from_slice(&fs::read(pack_dir.join("profiles.v1.json")).unwrap()).unwrap();
        let mut catalog: Value = serde_json::from_slice(&fs::read(&cat_path).unwrap()).unwrap();
        let id = "demo.second-model";
        let mut detail = details["assets"][0].clone();
        detail["id"] = Value::String(id.into());
        detail["names"]["fr"] = Value::String("ZZ deuxième modèle".into());
        detail["model_path"] = Value::String(
            root.join("normalized/demo/models/b.glb")
                .display()
                .to_string(),
        );
        detail["readiness"]["model_conversion_qualified"] = Value::Bool(false);
        let mut profile = profiles["profiles"][0].clone();
        profile["external_id"] = Value::String(id.into());
        let mut cat = catalog["assets"][0].clone();
        cat["id"] = Value::String(id.into());
        cat["model"] = Value::String("models/b.glb".into());
        details["assets"].as_array_mut().unwrap().push(detail);
        profiles["profiles"].as_array_mut().unwrap().push(profile);
        catalog["assets"].as_array_mut().unwrap().push(cat);
        if materialize {
            fs::write(root.join("normalized/demo/models/b.glb"), b"glb").unwrap();
        }
        write_pack_fixture(root, "demo", &details, &profiles, &catalog, &cat_path);
        id.into()
    }
    fn search_page(library: &mut Library, query: LibraryQuery) -> LibraryPage {
        match library.execute(LibraryRequest::Search { query }).unwrap() {
            LibraryResponse::Page { page } => page,
            _ => panic!("page attendue"),
        }
    }
    fn bump_registry(root: &Path) {
        let path = root.join("metadata/asset-import-registry.v1.json");
        let mut raw = fs::read(&path).unwrap();
        raw.push(b' ');
        fs::write(path, raw).unwrap();
    }
    #[test]
    fn missing_model_outside_page_does_not_block_index_or_valid_page() {
        let (root, _) = fixture();
        let missing = add_second_model(&root, false);
        let mut library = Library::new(&root).unwrap();
        let page = search_page(
            &mut library,
            LibraryQuery {
                limit: 1,
                ..LibraryQuery::default()
            },
        );
        assert_eq!(page.total_assets, 2);
        assert_eq!(page.matched_assets, 2);
        assert_eq!(page.assets[0].id, "demo.arbre-eclaire");
        assert_eq!(library.physical_access_checks.get(), 1);
        let error = library
            .execute(LibraryRequest::Search {
                query: LibraryQuery {
                    limit: 1,
                    offset: 1,
                    ..LibraryQuery::default()
                },
            })
            .unwrap_err();
        assert!(error.contains(&missing));
        let audit = library.audit_indexed_integrity(false).unwrap();
        assert_eq!(audit["refused_count"], 1);
        assert_eq!(audit["indexed_count"], 2);
        assert_eq!(audit["full_import"], false);
        assert_eq!(audit["human_review"], false);
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn displayed_model_deleted_after_search_is_refused_on_every_access() {
        let (root, _) = fixture();
        let mut library = Library::new(&root).unwrap();
        let page = search_page(&mut library, LibraryQuery::default());
        fs::remove_file(root.join("normalized/demo/models/a.glb")).unwrap();
        assert!(
            library
                .execute(LibraryRequest::Details {
                    id: page.assets[0].id.clone(),
                    revision: page.revision.clone()
                })
                .is_err()
        );
        assert!(
            library
                .execute(LibraryRequest::Thumbnail {
                    id: page.assets[0].id.clone(),
                    revision: page.revision
                })
                .is_err()
        );
        assert!(
            library
                .execute(LibraryRequest::Search {
                    query: LibraryQuery::default()
                })
                .is_err()
        );
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn same_length_model_tamper_is_refused_before_details_exposes_path() {
        let (root, _) = fixture();
        let mut library = Library::new(&root).unwrap();
        let page = search_page(&mut library, LibraryQuery::default());
        fs::write(root.join("normalized/demo/models/a.glb"), b"bad").unwrap();
        let error = library
            .execute(LibraryRequest::Details {
                id: page.assets[0].id.clone(),
                revision: page.revision,
            })
            .unwrap_err();
        assert!(error.contains("empreinte de modèle incohérente"));
        let audit = library.audit_indexed_integrity(false).unwrap();
        assert_eq!(audit["refused_count"], 1);
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn directory_cannot_masquerade_as_a_model() {
        let (root, _) = fixture();
        let path = root.join("normalized/demo/models/a.glb");
        fs::remove_file(&path).unwrap();
        fs::create_dir(&path).unwrap();
        let mut library = Library::new(&root).unwrap();
        assert!(
            library
                .execute(LibraryRequest::Search {
                    query: LibraryQuery::default()
                })
                .is_err()
        );
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn metadata_build_performs_zero_individual_payload_access_checks() {
        let (root, _) = fixture();
        add_second_model(&root, false);
        let library = Library::new(&root).unwrap();
        let raw = library.read_registry().unwrap();
        let snapshot = library
            .build_snapshot(&raw, None, None, snapshot_revision(&raw, None))
            .unwrap();
        assert_eq!(snapshot.assets.len(), 2);
        assert_eq!(library.physical_access_checks.get(), 0);
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn model_reference_traversal_is_refused_during_metadata_build() {
        let (root, _) = fixture();
        let path = root.join("normalized/demo/catalog.v1.json");
        let mut cat: Value = serde_json::from_slice(&fs::read(&path).unwrap()).unwrap();
        cat["assets"][0]["model"] = Value::String("../outside.glb".into());
        fs::write(path, serde_json::to_vec(&cat).unwrap()).unwrap();
        let mut library = Library::new(&root).unwrap();
        assert!(
            library
                .execute(LibraryRequest::Search {
                    query: LibraryQuery::default()
                })
                .is_err()
        );
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn lexical_outside_model_is_refused_even_when_not_on_page() {
        let (root, _) = fixture();
        add_second_model(&root, false);
        let path = root.join("metadata/rev/demo/one/details.v1.json");
        let mut details: Value = serde_json::from_slice(&fs::read(&path).unwrap()).unwrap();
        details["assets"][1]["model_path"] = Value::String(
            std::env::temp_dir()
                .join("outside.glb")
                .display()
                .to_string(),
        );
        let p: Value =
            serde_json::from_slice(&fs::read(path.with_file_name("profiles.v1.json")).unwrap())
                .unwrap();
        let cat_path = root.join("normalized/demo/catalog.v1.json");
        let cat: Value = serde_json::from_slice(&fs::read(&cat_path).unwrap()).unwrap();
        write_pack_fixture(&root, "demo", &details, &p, &cat, &cat_path);
        let mut library = Library::new(&root).unwrap();
        assert!(
            library
                .execute(LibraryRequest::Search {
                    query: LibraryQuery {
                        limit: 1,
                        ..LibraryQuery::default()
                    }
                })
                .is_err()
        );
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn registry_change_after_build_refuses_publication() {
        let (root, _) = fixture();
        let mut library = Library::new(&root).unwrap();
        library.after_build_hook = Some(bump_registry);
        assert!(
            library
                .execute(LibraryRequest::Search {
                    query: LibraryQuery::default()
                })
                .is_err()
        );
        assert!(library.snapshot.is_none());
        search_page(&mut library, LibraryQuery::default());
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn registry_change_after_access_refuses_the_response() {
        let (root, _) = fixture();
        let mut library = Library::new(&root).unwrap();
        library.before_reply_hook = Some(bump_registry);
        assert!(
            library
                .execute(LibraryRequest::Search {
                    query: LibraryQuery::default()
                })
                .is_err()
        );
        search_page(&mut library, LibraryQuery::default());
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn alternative_asset_root_keeps_preview_binding_and_full_hash() {
        let (root, _) = fixture();
        let alt = root.join("normalized/alternative");
        fs::create_dir_all(alt.join("models")).unwrap();
        fs::create_dir_all(alt.join("previews")).unwrap();
        fs::write(alt.join("models/a.glb"), b"glb").unwrap();
        fs::copy(
            root.join("normalized/demo/previews/a.png"),
            alt.join("previews/a.png"),
        )
        .unwrap();
        let dir = root.join("metadata/rev/demo/one");
        let mut details: Value =
            serde_json::from_slice(&fs::read(dir.join("details.v1.json")).unwrap()).unwrap();
        details["assets"][0]["model_path"] =
            Value::String(alt.join("models/a.glb").display().to_string());
        let profiles: Value =
            serde_json::from_slice(&fs::read(dir.join("profiles.v1.json")).unwrap()).unwrap();
        let cat_path = root.join("normalized/demo/catalog.v1.json");
        let catalog: Value = serde_json::from_slice(&fs::read(&cat_path).unwrap()).unwrap();
        write_pack_fixture(&root, "demo", &details, &profiles, &catalog, &cat_path);
        let mut library = Library::new(&root).unwrap();
        let page = search_page(&mut library, LibraryQuery::default());
        match library
            .execute(LibraryRequest::Details {
                id: page.assets[0].id.clone(),
                revision: page.revision,
            })
            .unwrap()
        {
            LibraryResponse::Details { detail } => {
                assert_eq!(
                    detail.model_path.unwrap(),
                    alt.join("models/a.glb")
                        .canonicalize()
                        .unwrap()
                        .display()
                        .to_string()
                );
                assert_eq!(
                    detail.preview_path.unwrap(),
                    alt.join("previews/a.png")
                        .canonicalize()
                        .unwrap()
                        .display()
                        .to_string()
                );
            }
            _ => panic!(),
        }
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn metadata_only_source_support_missing_outside_model_query_is_still_indexed() {
        let (root, _) = fixture();
        let support_id = add_source_support(&root);
        fs::remove_file(root.join("originals/source-support/helper.fbx")).unwrap();
        let mut library = Library::new(&root).unwrap();
        let page = search_page(
            &mut library,
            LibraryQuery {
                availability: "converted_model".into(),
                ..LibraryQuery::default()
            },
        );
        assert_eq!(page.total_assets, 2);
        assert_eq!(page.matched_assets, 1);
        assert!(
            library
                .execute(LibraryRequest::Details {
                    id: support_id,
                    revision: page.revision
                })
                .is_err()
        );
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn invalid_model_sha_declaration_is_refused_before_any_file_access() {
        let (root, _) = fixture();
        let dir = root.join("metadata/rev/demo/one");
        let mut details: Value =
            serde_json::from_slice(&fs::read(dir.join("details.v1.json")).unwrap()).unwrap();
        let mut profiles: Value =
            serde_json::from_slice(&fs::read(dir.join("profiles.v1.json")).unwrap()).unwrap();
        let cat_path = root.join("normalized/demo/catalog.v1.json");
        let mut catalog: Value = serde_json::from_slice(&fs::read(&cat_path).unwrap()).unwrap();
        details["assets"][0]["model_sha256"] = Value::String("not-sha".into());
        profiles["profiles"][0]["model_content_sha256"] = Value::String("not-sha".into());
        catalog["assets"][0]["sha256"] = Value::String("not-sha".into());
        write_pack_fixture(&root, "demo", &details, &profiles, &catalog, &cat_path);
        let mut library = Library::new(&root).unwrap();
        assert!(
            library
                .execute(LibraryRequest::Search {
                    query: LibraryQuery::default()
                })
                .is_err()
        );
        assert_eq!(library.physical_access_checks.get(), 0);
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn audit_reports_full_index_payload_scope_without_qualifying_import() {
        let (root, _) = fixture();
        add_second_model(&root, true);
        let mut library = Library::new(&root).unwrap();
        let report = library.audit_indexed_integrity(false).unwrap();
        assert_eq!(report["indexed_count"], 2);
        assert_eq!(report["passed_count"], 2);
        assert_eq!(report["refused_count"], 0);
        assert_eq!(report["archives_requested"], false);
        for row in report["rows"].as_array().unwrap() {
            assert_eq!(row["model_full_sha_checked"], true);
            assert_eq!(row["preview_png_sha_checked"], true);
        }
        for key in [
            "full_import",
            "human_review",
            "runtime_qualified",
            "placement_qualified",
            "unity_parity",
        ] {
            assert_eq!(report[key], false);
        }
        let _ = fs::remove_dir_all(root);
    }

    #[test]
    fn failed_metadata_refresh_cannot_fall_back_to_an_old_snapshot() {
        let (root, _) = fixture();
        let mut library = Library::new(&root).unwrap();
        search_page(&mut library, LibraryQuery::default());
        fs::write(root.join("metadata/rev/demo/one/details.v1.json"), b"{}").unwrap();
        assert!(
            library
                .execute(LibraryRequest::Search {
                    query: LibraryQuery {
                        refresh: true,
                        ..LibraryQuery::default()
                    }
                })
                .is_err()
        );
        assert!(library.snapshot.is_none());
        assert!(
            library
                .execute(LibraryRequest::Search {
                    query: LibraryQuery::default()
                })
                .is_err()
        );
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn registry_failure_invalidation_initial_import_ordinary_and_refresh() {
        for refresh in [false, true] {
            let (root, _) = fixture();
            let mut library = Library::new(&root).unwrap();
            search_page(&mut library, LibraryQuery::default());
            let registry = root.join("metadata/asset-import-registry.v1.json");
            let pinned_bytes = fs::read(&registry).unwrap();
            fs::remove_file(&registry).unwrap();
            fs::create_dir(&registry).unwrap();
            assert!(
                library
                    .execute(LibraryRequest::Search {
                        query: LibraryQuery {
                            refresh,
                            ..LibraryQuery::default()
                        }
                    })
                    .is_err()
            );
            assert!(library.snapshot.is_none());
            fs::write(root.join("metadata/rev/demo/one/details.v1.json"), b"{}").unwrap();
            fs::remove_dir(&registry).unwrap();
            fs::write(&registry, &pinned_bytes).unwrap();
            assert_eq!(fs::read(&registry).unwrap(), pinned_bytes);
            assert!(
                library
                    .execute(LibraryRequest::Search {
                        query: LibraryQuery::default()
                    })
                    .is_err()
            );
            assert!(library.snapshot.is_none());
            let _ = fs::remove_dir_all(root);
        }
    }

    #[test]
    fn registry_failure_invalidation_initial_support_ordinary_and_refresh() {
        for refresh in [false, true] {
            let (root, _) = fixture();
            add_source_support(&root);
            let mut library = Library::new(&root).unwrap();
            search_page(&mut library, LibraryQuery::default());
            let registry = root.join(SOURCE_SUPPORT_REGISTRY);
            let pinned_bytes = fs::read(&registry).unwrap();
            fs::remove_file(&registry).unwrap();
            fs::create_dir(&registry).unwrap();
            assert!(
                library
                    .execute(LibraryRequest::Search {
                        query: LibraryQuery {
                            refresh,
                            ..LibraryQuery::default()
                        }
                    })
                    .is_err()
            );
            assert!(library.snapshot.is_none());
            fs::write(root.join("metadata/rev/demo/one/details.v1.json"), b"{}").unwrap();
            fs::remove_dir(&registry).unwrap();
            fs::write(&registry, &pinned_bytes).unwrap();
            assert_eq!(fs::read(&registry).unwrap(), pinned_bytes);
            assert!(
                library
                    .execute(LibraryRequest::Search {
                        query: LibraryQuery::default()
                    })
                    .is_err()
            );
            assert!(library.snapshot.is_none());
            let _ = fs::remove_dir_all(root);
        }
    }

    #[test]
    fn registry_failure_invalidation_final_import_and_support_read_and_divergence() {
        fn unreadable_import(root: &Path) {
            let path = root.join("metadata/asset-import-registry.v1.json");
            fs::remove_file(&path).unwrap();
            fs::create_dir(&path).unwrap();
        }
        fn changed_import(root: &Path) {
            fs::write(root.join("metadata/asset-import-registry.v1.json"), b"{}").unwrap();
        }
        fn unreadable_support(root: &Path) {
            let path = root.join(SOURCE_SUPPORT_REGISTRY);
            fs::remove_file(&path).unwrap();
            fs::create_dir(&path).unwrap();
        }
        fn changed_support(root: &Path) {
            fs::write(root.join(SOURCE_SUPPORT_REGISTRY), b"{}").unwrap();
        }
        let cases: [FixtureMutation; 4] = [
            ("metadata/asset-import-registry.v1.json", unreadable_import),
            ("metadata/asset-import-registry.v1.json", changed_import),
            (SOURCE_SUPPORT_REGISTRY, unreadable_support),
            (SOURCE_SUPPORT_REGISTRY, changed_support),
        ];
        for (relative, hook) in cases {
            let (root, _) = fixture();
            add_source_support(&root);
            let mut library = Library::new(&root).unwrap();
            search_page(&mut library, LibraryQuery::default());
            let registry = root.join(relative);
            let pinned_bytes = fs::read(&registry).unwrap();
            library.before_reply_hook = Some(hook);
            assert!(
                library
                    .execute(LibraryRequest::Search {
                        query: LibraryQuery::default()
                    })
                    .is_err()
            );
            assert!(library.snapshot.is_none());
            fs::write(root.join("metadata/rev/demo/one/details.v1.json"), b"{}").unwrap();
            if registry.is_dir() {
                fs::remove_dir(&registry).unwrap();
            }
            fs::write(&registry, &pinned_bytes).unwrap();
            assert_eq!(fs::read(&registry).unwrap(), pinned_bytes);
            assert!(
                library
                    .execute(LibraryRequest::Search {
                        query: LibraryQuery::default()
                    })
                    .is_err()
            );
            assert!(library.snapshot.is_none());
            let _ = fs::remove_dir_all(root);
        }
    }

    #[test]
    fn registry_failure_invalidation_explicit_audit_leaves_no_reusable_snapshot() {
        for relative in [
            "metadata/asset-import-registry.v1.json",
            SOURCE_SUPPORT_REGISTRY,
        ] {
            let (root, _) = fixture();
            add_source_support(&root);
            let mut library = Library::new(&root).unwrap();
            search_page(&mut library, LibraryQuery::default());
            let registry = root.join(relative);
            let pinned_bytes = fs::read(&registry).unwrap();
            fs::remove_file(&registry).unwrap();
            fs::create_dir(&registry).unwrap();
            assert!(library.audit_indexed_integrity(false).is_err());
            assert!(library.snapshot.is_none());
            fs::write(root.join("metadata/rev/demo/one/details.v1.json"), b"{}").unwrap();
            fs::remove_dir(&registry).unwrap();
            fs::write(&registry, &pinned_bytes).unwrap();
            assert_eq!(fs::read(&registry).unwrap(), pinned_bytes);
            assert!(
                library
                    .execute(LibraryRequest::Search {
                        query: LibraryQuery::default()
                    })
                    .is_err()
            );
            assert!(library.snapshot.is_none());
            let _ = fs::remove_dir_all(root);
        }
    }

    #[test]
    fn multi_pack_search_preserves_exact_licence_metadata_and_flags() {
        let (root, _) = fixture();
        let source = root.join("metadata/rev/demo/one");
        let mut details: Value =
            serde_json::from_slice(&fs::read(source.join("details.v1.json")).unwrap()).unwrap();
        let mut profiles: Value =
            serde_json::from_slice(&fs::read(source.join("profiles.v1.json")).unwrap()).unwrap();
        let mut catalog: Value = serde_json::from_slice(
            &fs::read(root.join("normalized/demo/catalog.v1.json")).unwrap(),
        )
        .unwrap();
        let cat_path = root.join("normalized/other/catalog.v1.json");
        let model = root.join("normalized/other/models/a.glb");
        let png = root.join("normalized/other/previews/a.png");
        fs::create_dir_all(model.parent().unwrap()).unwrap();
        fs::create_dir_all(png.parent().unwrap()).unwrap();
        fs::write(&model, b"glb").unwrap();
        fs::copy(root.join("normalized/demo/previews/a.png"), &png).unwrap();
        details["pack_id"] = Value::String("other".into());
        details["assets"][0]["id"] = Value::String("other.object".into());
        details["assets"][0]["names"]["fr"] = Value::String("ZZ objet autre".into());
        details["assets"][0]["license"] = Value::String("LicenseRef-Test-Private".into());
        details["assets"][0]["model_path"] = Value::String(model.display().to_string());
        details["assets"][0]["source_catalog"] = Value::String(cat_path.display().to_string());
        details["assets"][0]["readiness"] = serde_json::json!({"metadata_ready":false,"runtime_qualified":false,"human_review":false,"model_conversion_qualified":false,"full_import":false});
        profiles["profiles"][0]["external_id"] = Value::String("other.object".into());
        catalog["id"] = Value::String("other".into());
        catalog["assets"][0]["id"] = Value::String("other.object".into());
        let expected_details = details["assets"][0].clone();
        let expected_profile = profiles["profiles"][0].clone();
        let expected_catalog = catalog["assets"][0].clone();
        write_pack_fixture(&root, "other", &details, &profiles, &catalog, &cat_path);
        let mut library = Library::new(&root).unwrap();
        let page = search_page(
            &mut library,
            LibraryQuery {
                pack: "other".into(),
                status: "needs_review".into(),
                ..LibraryQuery::default()
            },
        );
        assert_eq!(page.total_assets, 2);
        assert_eq!(page.matched_assets, 1);
        assert_eq!(page.facets.packs, vec!["demo", "other"]);
        assert!(!page.assets[0].runtime_qualified);
        assert!(!page.assets[0].metadata_ready);
        match library
            .execute(LibraryRequest::Details {
                id: "other.object".into(),
                revision: page.revision,
            })
            .unwrap()
        {
            LibraryResponse::Details { detail } => {
                assert_eq!(detail.license, "LicenseRef-Test-Private");
                assert_eq!(detail.details, expected_details);
                assert_eq!(detail.profile, expected_profile);
                assert_eq!(detail.catalog_asset, expected_catalog);
            }
            _ => panic!(),
        }
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn duplicate_profile_and_model_hash_mismatch_are_refused_without_payload_reads() {
        for duplicate in [true, false] {
            let (root, _) = fixture();
            let dir = root.join("metadata/rev/demo/one");
            let details: Value =
                serde_json::from_slice(&fs::read(dir.join("details.v1.json")).unwrap()).unwrap();
            let mut profiles: Value =
                serde_json::from_slice(&fs::read(dir.join("profiles.v1.json")).unwrap()).unwrap();
            let cat_path = root.join("normalized/demo/catalog.v1.json");
            let cat: Value = serde_json::from_slice(&fs::read(&cat_path).unwrap()).unwrap();
            if duplicate {
                let extra = profiles["profiles"][0].clone();
                profiles["profiles"].as_array_mut().unwrap().push(extra);
            } else {
                profiles["profiles"][0]["model_content_sha256"] =
                    Value::String(sha256(b"different"));
            }
            write_pack_fixture(&root, "demo", &details, &profiles, &cat, &cat_path);
            let mut library = Library::new(&root).unwrap();
            assert!(
                library
                    .execute(LibraryRequest::Search {
                        query: LibraryQuery::default()
                    })
                    .is_err()
            );
            assert_eq!(library.physical_access_checks.get(), 0);
            let _ = fs::remove_dir_all(root);
        }
    }
    #[test]
    fn explicit_audit_hashes_a_shared_archive_once_and_preserves_support_scope() {
        let (root, _) = fixture();
        add_source_support(&root);
        let reg_path = root.join(SOURCE_SUPPORT_REGISTRY);
        let mut reg: Value = serde_json::from_slice(&fs::read(&reg_path).unwrap()).unwrap();
        let old_path = root.join(reg["path"].as_str().unwrap());
        let mut support: Value = serde_json::from_slice(&fs::read(old_path).unwrap()).unwrap();
        let mut second = support["assets"][0].clone();
        second["id"] = Value::String("demo.source-helper-second".into());
        support["assets"].as_array_mut().unwrap().push(second);
        let raw = serde_json::to_vec(&support).unwrap();
        let hash = sha256(&raw);
        let rel = format!("metadata/asset-source-support-revisions/{hash}/source-support.v1.json");
        let target = root.join(&rel);
        fs::create_dir_all(target.parent().unwrap()).unwrap();
        fs::write(target, raw).unwrap();
        reg["path"] = Value::String(rel);
        reg["sha256"] = Value::String(hash.clone());
        reg["revision"] = Value::String(hash);
        fs::write(reg_path, serde_json::to_vec(&reg).unwrap()).unwrap();
        let mut library = Library::new(&root).unwrap();
        let report = library.audit_indexed_integrity(true).unwrap();
        assert_eq!(report["indexed_count"], 3);
        assert_eq!(report["refused_count"], 0);
        assert_eq!(report["unique_archive_payloads_hashed"], 1);
        let source_row = report["rows"]
            .as_array()
            .unwrap()
            .iter()
            .find(|row| row["availability"] == "source_support")
            .unwrap();
        assert_eq!(source_row["model_full_sha_checked"], false);
        assert_eq!(source_row["source_and_meta_full_sha_checked"], true);
        assert_eq!(report["full_import"], false);
        fs::write(root.join("originals/demo.unitypackage"), b"damaged").unwrap();
        let changed = library.audit_indexed_integrity(true).unwrap();
        assert_eq!(changed["refused_count"], 2);
        assert_eq!(changed["unique_archive_payloads_hashed"], 1);
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn bounded_pagination_checks_only_returned_entries_without_model_payload_hash() {
        let (root, _) = fixture();
        add_second_model(&root, true);
        let mut library = Library::new(&root).unwrap();
        let page = search_page(
            &mut library,
            LibraryQuery {
                limit: 1,
                offset: 1,
                ..LibraryQuery::default()
            },
        );
        assert_eq!(page.assets.len(), 1);
        assert_eq!(library.physical_access_checks.get(), 1);
        // Search does not claim payload integrity; Details must discover a same-
        // length tamper before exposing that model path.
        fs::write(root.join("normalized/demo/models/b.glb"), b"bad").unwrap();
        let warm = search_page(
            &mut library,
            LibraryQuery {
                limit: 1,
                offset: 1,
                ..LibraryQuery::default()
            },
        );
        assert_eq!(warm.assets[0].id, page.assets[0].id);
        assert!(!warm.assets[0].runtime_qualified);
        assert!(
            library
                .execute(LibraryRequest::Details {
                    id: warm.assets[0].id.clone(),
                    revision: warm.revision
                })
                .is_err()
        );
        assert_eq!(library.physical_access_checks.get(), 3);
        let _ = fs::remove_dir_all(root);
    }
    #[test]
    fn selected_support_source_and_meta_sha_are_fresh_for_every_access() {
        for (relative, payload, expected_error) in [
            (
                "originals/source-support/helper.fbx",
                b"tamper-fbx".as_slice(),
                "empreinte de source conservee incoherente",
            ),
            (
                "originals/source-support/helper.fbx.meta",
                b"fileFormatVersion: 3".as_slice(),
                "empreinte des metadonnees Unity incoherente",
            ),
        ] {
            let (root, _) = fixture();
            let id = add_source_support(&root);
            let mut library = Library::new(&root).unwrap();
            let query = LibraryQuery {
                availability: "source_support".into(),
                ..LibraryQuery::default()
            };
            let page = search_page(&mut library, query.clone());
            assert_eq!(page.assets.len(), 1);
            assert_eq!(
                fs::metadata(root.join(relative)).unwrap().len(),
                payload.len() as u64
            );
            fs::write(root.join(relative), payload).unwrap();
            // A missing/corrupted support off the chosen model page remains an
            // indexed fiche; selection must refuse it with its identity.
            let models = search_page(
                &mut library,
                LibraryQuery {
                    availability: "converted_model".into(),
                    ..LibraryQuery::default()
                },
            );
            assert_eq!(models.total_assets, 2);
            assert_eq!(models.assets.len(), 1);
            for request in [
                LibraryRequest::Search {
                    query: query.clone(),
                },
                LibraryRequest::Thumbnail {
                    id: id.clone(),
                    revision: page.revision.clone(),
                },
                LibraryRequest::Details {
                    id: id.clone(),
                    revision: page.revision.clone(),
                },
            ] {
                let error = library.execute(request).unwrap_err();
                assert!(error.contains(&id), "{error}");
                let normalized = error.replace('é', "e");
                assert!(normalized.contains(expected_error), "{error}");
            }
            let _ = fs::remove_dir_all(root);
        }
    }
}

#[cfg(test)]
mod model_headroom_tests {
    use super::*;
    #[test]
    fn model_reader_reserves_packet_headroom_and_refuses_oversized_or_non_regular_files() {
        let root =
            std::env::temp_dir().join(format!("forge-model-headroom-{}", std::process::id()));
        fs::create_dir_all(&root).unwrap();
        let path = root.join("body.bin");
        fs::write(&path, b"original-body").unwrap();
        let body = read_model_limited(&path, 13).unwrap();
        assert_eq!(body, b"original-body");
        assert!(
            body.capacity() >= body.len() + crate::asset_library_model_wire::MODEL_HEADER_HEADROOM
        );
        let pointer = body.as_ptr();
        let id = "headroom-model".to_owned();
        let model = LibraryModel {
            asset_id: vault_asset_id(&id),
            sha256: forge_core::bytes_sha256(&body),
            id,
            bytes: body,
        };
        let packet = crate::asset_library_model_wire::encode(model).unwrap();
        assert_eq!(packet.as_ptr(), pointer);
        assert!(read_model_limited(&path, 12).is_err());
        assert!(read_model_limited(&root, 13).is_err());
        fs::remove_file(&path).unwrap();
        fs::remove_dir(&root).unwrap();
    }
}
