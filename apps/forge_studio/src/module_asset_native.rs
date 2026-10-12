//! Read-only, host-authorized installed-module models. `WebView` supplies no paths.
use crate::asset_library_model_wire::{MAX_MODEL_BYTES, MODEL_HEADER_HEADROOM};
use crate::asset_library_protocol::{ModuleCollisionPolicy, ModuleModel};
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
    /// Absent means existing UCX/UTM admission. Null or unknown policies are refused.
    #[serde(default, deserialize_with = "declared_collision")]
    collision: Option<ModuleCollisionPolicy>,
}

fn declared_collision<'de, D: serde::Deserializer<'de>>(
    deserializer: D,
) -> Result<Option<ModuleCollisionPolicy>, D::Error> {
    ModuleCollisionPolicy::deserialize(deserializer).map(Some)
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
        if entry.collision == Some(ModuleCollisionPolicy::StaticTriangleMeshes) {
            validate_static_scene0(&bytes).map_err(reject)?;
        }
        // Detect catalog replacement during acquisition, before publishing bytes.
        self.read_catalog()?;
        Ok(ModuleModel {
            asset_id,
            sha256,
            catalog_sha256: self.catalog_sha256,
            collision: entry.collision,
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

/// This policy covers only static, embedded triangle geometry in Scene0.
fn validate_static_scene0(bytes: &[u8]) -> Result<(), &'static str> {
    validate_glb_header(bytes)?;
    let length =
        u32::from_le_bytes(bytes[12..16].try_into().map_err(|_| "JSON GLB absent")?) as usize;
    if length > 4 * 1024 * 1024 {
        return Err("Budget JSON Scene0 statique dépassé");
    }
    let doc: serde_json::Value = serde_json::from_slice(&bytes[20..20 + length])
        .map_err(|_| "JSON GLB statique invalide")?;
    for name in ["animations", "skins"] {
        if doc
            .get(name)
            .is_some_and(|value| value.as_array().is_none_or(|a| !a.is_empty()))
        {
            return Err("Le rôle statique refuse animations et skins");
        }
    }
    if doc
        .get("buffers")
        .and_then(serde_json::Value::as_array)
        .is_some_and(|buffers| {
            buffers.iter().any(|buffer| {
                buffer
                    .get("uri")
                    .is_some_and(|uri| uri.as_str().is_none_or(|value| !value.starts_with("data:")))
            })
        })
    {
        return Err("Géométrie statique externe non épinglée");
    }
    static_scene_primitives(&doc).map(|_| ())
}

fn static_scene_primitives(doc: &serde_json::Value) -> Result<usize, &'static str> {
    let nodes = doc["nodes"].as_array().ok_or("Nœuds Scene0 absents")?;
    let meshes = doc["meshes"].as_array().ok_or("Meshes Scene0 absents")?;
    let roots = doc["scenes"][0]["nodes"]
        .as_array()
        .ok_or("Scene0 absente")?;
    let mut pending = roots.iter().collect::<Vec<_>>();
    let mut visited = BTreeSet::new();
    let mut count = 0;
    while let Some(index) = pending.pop() {
        let index = index
            .as_u64()
            .and_then(|value| usize::try_from(value).ok())
            .ok_or("Index Scene0 invalide")?;
        if visited.len() >= 65_536 || !visited.insert(index) {
            return Err("Hiérarchie Scene0 répétée ou budget dépassé");
        }
        let node = nodes.get(index).ok_or("Nœud Scene0 hors limites")?;
        if node.get("skin").is_some() || node.get("weights").is_some() {
            return Err("Déformation Scene0 incompatible avec le rôle statique");
        }
        if let Some(index) = node.get("mesh") {
            let mesh = index
                .as_u64()
                .and_then(|value| usize::try_from(value).ok())
                .and_then(|index| meshes.get(index))
                .ok_or("Mesh Scene0 hors limites")?;
            count += static_mesh_primitives(mesh)?;
        }
        if let Some(children) = node.get("children") {
            pending.extend(children.as_array().ok_or("Enfants Scene0 invalides")?);
        }
    }
    if count == 0 {
        return Err("Scene0 sans géométrie triangulaire statique");
    }
    Ok(count)
}

fn static_mesh_primitives(mesh: &serde_json::Value) -> Result<usize, &'static str> {
    if mesh.get("weights").is_some() {
        return Err("Morph de mesh incompatible avec le rôle statique");
    }
    let primitives = mesh["primitives"]
        .as_array()
        .ok_or("Primitives Scene0 absentes")?;
    for primitive in primitives {
        if primitive.get("targets").is_some()
            || primitive
                .get("mode")
                .is_some_and(|mode| mode.as_u64() != Some(4))
            || primitive["attributes"]["POSITION"].as_u64().is_none()
        {
            return Err("Primitive Scene0 non statique ou non triangulaire");
        }
    }
    Ok(primitives.len())
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::{Value, json};
    use std::sync::atomic::{AtomicU64, Ordering};

    fn static_scene() -> Value {
        json!({
            "asset": {"version": "2.0"},
            "scenes": [{"nodes": [0]}],
            "nodes": [{"children": [1]}, {"mesh": 0}],
            "meshes": [{"primitives": [{"attributes": {"POSITION": 0}}]}]
        })
    }

    fn glb(doc: &Value) -> Vec<u8> {
        let mut json = serde_json::to_vec(doc).unwrap();
        while !json.len().is_multiple_of(4) {
            json.push(b' ');
        }
        let mut bytes = b"glTF".to_vec();
        bytes.extend(2_u32.to_le_bytes());
        bytes.extend(u32::try_from(20 + json.len()).unwrap().to_le_bytes());
        bytes.extend(u32::try_from(json.len()).unwrap().to_le_bytes());
        bytes.extend(b"JSON");
        bytes.extend(json);
        bytes
    }

    fn entry() -> Value {
        json!({
            "id": AssetId::deterministic("generic-module/model"),
            "hash": forge_core::bytes_sha256(&glb(&static_scene())),
            "file": "model.glb",
            "license": "fixture-license"
        })
    }

    #[test]
    fn catalogue_collision_is_explicit_optional_and_strict() {
        let absent: Entry = serde_json::from_value(entry()).unwrap();
        assert_eq!(absent.collision, None);
        let mut declared = entry();
        declared["collision"] = json!("static_triangle_meshes");
        let declared: Entry = serde_json::from_value(declared).unwrap();
        assert_eq!(
            declared.collision,
            Some(ModuleCollisionPolicy::StaticTriangleMeshes)
        );
        for policy in [
            Value::Null,
            json!("convex"),
            json!("STATIC_TRIANGLE_MESHES"),
            json!({}),
        ] {
            let mut invalid = entry();
            invalid["collision"] = policy;
            assert!(serde_json::from_value::<Entry>(invalid).is_err());
        }
        let mut invalid = entry();
        invalid["collision_policy"] = json!("static_triangle_meshes");
        assert!(serde_json::from_value::<Entry>(invalid).is_err());
    }

    #[test]
    fn static_scene0_walks_owned_nodes_and_counts_all_triangle_primitives() {
        let mut doc = static_scene();
        doc["meshes"][0]["primitives"]
            .as_array_mut()
            .unwrap()
            .push(json!({
                "mode": 4, "attributes": {"POSITION": 1}
            }));
        assert_eq!(static_scene_primitives(&doc), Ok(2));
        assert!(validate_static_scene0(&glb(&doc)).is_ok());
        doc["scenes"][0]["nodes"] = json!([]);
        doc["scenes"]
            .as_array_mut()
            .unwrap()
            .push(json!({"nodes": [0]}));
        assert!(validate_static_scene0(&glb(&doc)).is_err());
    }

    #[test]
    fn static_role_rejects_skin_morph_animation_and_non_triangles() {
        for (pointer, value) in [
            ("/nodes/1", json!({"mesh": 0, "skin": 0})),
            ("/nodes/1", json!({"mesh": 0, "weights": []})),
            (
                "/meshes/0",
                json!({"weights": [], "primitives": [{"attributes": {"POSITION": 0}}]}),
            ),
            (
                "/meshes/0/primitives/0",
                json!({"targets": [], "attributes": {"POSITION": 0}}),
            ),
            (
                "/meshes/0/primitives/0",
                json!({"mode": 1, "attributes": {"POSITION": 0}}),
            ),
            ("/meshes/0/primitives/0", json!({"attributes": {}})),
        ] {
            let mut doc = static_scene();
            *doc.pointer_mut(pointer).unwrap() = value;
            assert!(validate_static_scene0(&glb(&doc)).is_err(), "{pointer}");
        }
        for field in ["animations", "skins"] {
            let mut doc = static_scene();
            doc[field] = json!([{}]);
            assert!(validate_static_scene0(&glb(&doc)).is_err(), "{field}");
        }
    }

    #[test]
    fn static_role_refuses_external_geometry_and_invalid_hierarchies() {
        let mut doc = static_scene();
        doc["buffers"] = json!([{"uri": "external.bin"}]);
        assert!(validate_static_scene0(&glb(&doc)).is_err());
        for roots in [json!([99]), json!([0, 0]), json!(["0"])] {
            let mut doc = static_scene();
            doc["scenes"][0]["nodes"] = roots;
            assert!(validate_static_scene0(&glb(&doc)).is_err());
        }
        let mut doc = static_scene();
        doc["nodes"][1]["children"] = json!([0]);
        assert!(validate_static_scene0(&glb(&doc)).is_err());
        let mut doc = static_scene();
        doc["nodes"][1]["mesh"] = json!(99);
        assert!(validate_static_scene0(&glb(&doc)).is_err());
    }

    struct CatalogFixture {
        root: PathBuf,
        catalog: Vec<u8>,
        bytes: Vec<u8>,
        id: AssetId,
    }

    impl CatalogFixture {
        fn new(collision: Option<&str>) -> Self {
            static NEXT: AtomicU64 = AtomicU64::new(0);
            let nonce = std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos();
            let root = std::env::temp_dir().join(format!(
                "forge-module-admission-{}-{nonce}-{}",
                std::process::id(),
                NEXT.fetch_add(1, Ordering::Relaxed)
            ));
            std::fs::create_dir(&root).unwrap();
            let bytes = glb(&static_scene());
            let mut model = entry();
            if let Some(collision) = collision {
                model["collision"] = json!(collision);
            }
            let id = serde_json::from_value(model["id"].clone()).unwrap();
            let catalog =
                serde_json::to_vec(&json!({"schema_version": 1, "assets": [model]})).unwrap();
            std::fs::write(root.join("model.glb"), &bytes).unwrap();
            std::fs::write(root.join("catalog.json"), &catalog).unwrap();
            Self {
                root,
                catalog,
                bytes,
                id,
            }
        }

        fn resolver(&self) -> Result<ModuleAssetResolver, ModuleModelError> {
            ModuleAssetResolver::new(
                &self.root,
                "catalog.json",
                forge_core::bytes_sha256(&self.catalog),
            )
        }
    }

    impl Drop for CatalogFixture {
        fn drop(&mut self) {
            std::fs::remove_dir_all(&self.root).unwrap();
        }
    }

    #[test]
    fn admitted_policy_is_bound_to_catalog_and_unchanged_asset_bytes() {
        let fixture = CatalogFixture::new(Some("static_triangle_meshes"));
        let resolver = fixture.resolver().unwrap();
        let sha256 = forge_core::bytes_sha256(&fixture.bytes);
        let model = resolver.resolve(fixture.id, sha256).unwrap();
        assert_eq!(
            model.collision,
            Some(ModuleCollisionPolicy::StaticTriangleMeshes)
        );
        assert_eq!(
            model.catalog_sha256,
            forge_core::bytes_sha256(&fixture.catalog)
        );
        assert_eq!(model.sha256, sha256);
        assert_eq!(model.bytes, fixture.bytes);
        assert!(matches!(
            resolver.resolve(fixture.id, ContentHash::from_bytes([0; 32])),
            Err(ModuleModelError::Rejected(_))
        ));
        std::fs::write(fixture.root.join("model.glb"), b"replaced model").unwrap();
        assert!(matches!(
            resolver.resolve(fixture.id, sha256),
            Err(ModuleModelError::Rejected(_))
        ));
    }

    #[test]
    fn absent_role_keeps_legacy_admission_and_only_exact_missing_allows_fallback() {
        let fixture = CatalogFixture::new(None);
        let resolver = fixture.resolver().unwrap();
        let sha256 = forge_core::bytes_sha256(&fixture.bytes);
        assert_eq!(
            resolver.resolve(fixture.id, sha256).unwrap().collision,
            None
        );
        let missing = AssetId::deterministic("unlisted-module/model");
        assert_eq!(
            resolver.resolve(missing, sha256).unwrap_err(),
            ModuleModelError::Missing(missing)
        );
        std::fs::write(fixture.root.join("catalog.json"), b"replaced catalogue").unwrap();
        assert!(matches!(
            resolver.resolve(missing, sha256),
            Err(ModuleModelError::Rejected(_))
        ));
        assert!(
            CatalogFixture::new(Some("unknown_policy"))
                .resolver()
                .is_err()
        );
    }
}
