//! Exact saved-content lookup through registry-pinned immutable pack revisions.
use super::{
    BTreeSet, BuildCache, Library, LibraryModel, MAX_ASSETS, Snapshot, StoredAsset, Value,
    parse_json, profile_index, required, valid_hash, validate_pack_metadata, validate_relative,
};

impl Library {
    pub(super) fn read_historical_model(
        &self,
        snapshot: &Snapshot,
        current: &StoredAsset,
        expected: forge_core::ContentHash,
    ) -> Result<LibraryModel, String> {
        let registry: Value = parse_json(&snapshot.registry_raw, "registre")?;
        let entry = &registry["packs"][&current.card.pack];
        let Some(history) = entry.get("history") else {
            return Err("empreinte sauvegardée différente du Vault".to_owned());
        };
        let history = history.as_array().ok_or("historique de pack invalide")?;
        if history.len() > 256 {
            return Err("trop de révisions historiques".to_owned());
        }
        // Validate every pointer before lookup; clients never supply a disk path.
        let mut paths = BTreeSet::new();
        for pointer in history {
            let path = required(pointer, "path")?;
            validate_relative(path)?;
            if !paths.insert(path) || !valid_hash(required(pointer, "revision")?) {
                return Err("pointeur historique invalide ou dupliqué".to_owned());
            }
            let files = pointer["files"]
                .as_object()
                .ok_or("empreintes historiques absentes")?;
            for name in ["details.v1.json", "profiles.v1.json"] {
                if !files
                    .get(name)
                    .and_then(Value::as_str)
                    .is_some_and(valid_hash)
                {
                    return Err("empreinte de snapshot historique invalide".to_owned());
                }
            }
        }
        let mut total = snapshot.registry_raw.len() as u64;
        let mut cache = BuildCache::default();
        for pointer in history {
            let root = self.safe_relative(required(pointer, "path")?)?;
            let files = pointer["files"]
                .as_object()
                .ok_or("empreintes historiques absentes")?;
            let details = parse_json(
                &self.checked_pack_file(&root, files, "details.v1.json", &mut total)?,
                "details historiques",
            )?;
            let profiles = parse_json(
                &self.checked_pack_file(&root, files, "profiles.v1.json", &mut total)?,
                "profiles historiques",
            )?;
            if details["schema_version"].as_u64() != Some(1)
                || profiles["schema_version"].as_u64() != Some(1)
                || details["pack_id"].as_str() != Some(current.card.pack.as_str())
                || profiles
                    .get("pack_id")
                    .is_some_and(|p| p.as_str() != Some(current.card.pack.as_str()))
            {
                return Err("schéma de snapshot historique incohérent".to_owned());
            }
            let assets = details["assets"]
                .as_array()
                .ok_or("assets historiques absents")?;
            let profiles = profiles["profiles"]
                .as_array()
                .ok_or("profiles historiques absents")?;
            if assets.len() > MAX_ASSETS || profiles.len() > MAX_ASSETS {
                return Err("trop d'assets historiques".to_owned());
            }
            validate_pack_metadata(&current.card.pack, assets, profiles)?;
            if let Some(item) = assets.iter().find(|item| {
                item["id"].as_str() == Some(current.card.id.as_str())
                    && item["model_sha256"]
                        .as_str()
                        .and_then(|hash| forge_core::ContentHash::parse(hash).ok())
                        == Some(expected)
            }) {
                let index = profile_index(profiles)?;
                let asset = self.make_asset(
                    &current.card.pack,
                    item,
                    profiles,
                    &index,
                    &mut total,
                    &mut cache,
                )?;
                // The normal reader checks containment, licence and exact payload SHA.
                return self.read_model(&asset);
            }
        }
        Err("version sauvegardée absente de l'historique du Vault".to_owned())
    }
}

#[cfg(test)]
mod tests {
    use super::super::{LibraryQuery, LibraryRequest, LibraryResponse, sha256, vault_asset_id};
    use super::*;
    use sha2::{Digest, Sha256};
    use std::{
        fs,
        path::{Path, PathBuf},
    };

    fn version(root: &Path, tag: &str) -> (Value, Vec<u8>) {
        let template = root.join("metadata/rev/demo/one");
        let mut details: Value =
            serde_json::from_slice(&fs::read(template.join("details.v1.json")).unwrap()).unwrap();
        let mut profiles: Value =
            serde_json::from_slice(&fs::read(template.join("profiles.v1.json")).unwrap()).unwrap();
        let catalog = root.join(format!("normalized/demo/{tag}"));
        fs::create_dir_all(catalog.join("models")).unwrap();
        let json = format!("{{\"asset\":{{\"version\":\"2.0\",\"generator\":\"{tag}\"}}}}");
        let mut bytes = b"glTF".to_vec();
        bytes.extend(2_u32.to_le_bytes());
        bytes.extend(u32::try_from(20 + json.len()).unwrap().to_le_bytes());
        bytes.extend(u32::try_from(json.len()).unwrap().to_le_bytes());
        bytes.extend(b"JSON");
        bytes.extend(json.as_bytes());
        let hash = sha256(&bytes);
        fs::write(catalog.join("models/a.glb"), &bytes).unwrap();
        details["assets"][0]["model_sha256"] = hash.clone().into();
        details["assets"][0]["model_path"] =
            catalog.join("models/a.glb").display().to_string().into();
        details["assets"][0]["source_catalog"] =
            catalog.join("catalog.v1.json").display().to_string().into();
        profiles["profiles"][0]["model_content_sha256"] = hash.clone().into();
        fs::write(catalog.join("catalog.v1.json"), serde_json::to_vec(&serde_json::json!({"id":"demo","assets":[{"id":"demo.arbre-eclaire","model":"models/a.glb","sha256":hash}]})).unwrap()).unwrap();
        let path = format!("metadata/rev/demo/{tag}");
        fs::create_dir_all(root.join(&path)).unwrap();
        let d = serde_json::to_vec(&details).unwrap();
        let p = serde_json::to_vec(&profiles).unwrap();
        fs::write(root.join(&path).join("details.v1.json"), &d).unwrap();
        fs::write(root.join(&path).join("profiles.v1.json"), &p).unwrap();
        (
            serde_json::json!({"path":path,"revision":sha256(tag.as_bytes()),"files":{"details.v1.json":sha256(&d),"profiles.v1.json":sha256(&p)}}),
            bytes,
        )
    }

    fn fixture() -> (PathBuf, Vec<u8>, Vec<u8>) {
        let (root, _) = super::super::tests::fixture();
        let (old, old_bytes) = version(&root, "old");
        let (mut current, current_bytes) = version(&root, "current");
        current["history"] = serde_json::json!([old]);
        fs::write(
            root.join("metadata/asset-import-registry.v1.json"),
            serde_json::to_vec(&serde_json::json!({"schema_version":1,"packs":{"demo":current}}))
                .unwrap(),
        )
        .unwrap();
        (root, old_bytes, current_bytes)
    }

    fn request(bytes: &[u8]) -> LibraryRequest {
        LibraryRequest::ModelReference {
            asset_id: vault_asset_id("demo.arbre-eclaire"),
            sha256: forge_core::ContentHash::from_bytes(Sha256::digest(bytes).into()),
        }
    }

    #[test]
    fn saved_version_returns_old_bytes_and_search_keeps_only_current_card() {
        let (root, old, current) = fixture();
        let mut library = Library::new(&root).unwrap();
        for bytes in [&old, &current] {
            let LibraryResponse::Model { model } = library.execute(request(bytes)).unwrap() else {
                panic!()
            };
            assert_eq!(&model.bytes, bytes);
        }
        let LibraryResponse::Page { page } = library
            .execute(LibraryRequest::Search {
                query: LibraryQuery::default(),
            })
            .unwrap()
        else {
            panic!()
        };
        assert_eq!(page.total_assets, 1);
        assert!(library.execute(request(b"unknown version")).is_err());
        fs::remove_dir_all(root).unwrap();
    }

    #[test]
    fn corrupted_historical_snapshot_and_payload_are_refused() {
        let (root, old, _) = fixture();
        let mut library = Library::new(&root).unwrap();
        fs::write(root.join("normalized/demo/old/models/a.glb"), b"changed").unwrap();
        assert!(
            library
                .execute(request(&old))
                .unwrap_err()
                .contains("empreinte")
        );
        fs::write(root.join("metadata/rev/demo/old/details.v1.json"), b"{}").unwrap();
        assert!(library.execute(request(&old)).is_err());
        fs::remove_dir_all(root).unwrap();
    }

    #[test]
    fn historical_escape_and_unbounded_history_are_refused() {
        let (root, old, _) = fixture();
        let path = root.join("metadata/asset-import-registry.v1.json");
        let original: Value = serde_json::from_slice(&fs::read(&path).unwrap()).unwrap();
        let mut registry = original.clone();
        registry["packs"]["demo"]["history"][0]["path"] = "../outside".into();
        fs::write(&path, serde_json::to_vec(&registry).unwrap()).unwrap();
        assert!(Library::new(&root).unwrap().execute(request(&old)).is_err());
        let mut registry = original;
        let pointer = registry["packs"]["demo"]["history"][0].clone();
        registry["packs"]["demo"]["history"] = Value::Array(vec![pointer; 257]);
        fs::write(&path, serde_json::to_vec(&registry).unwrap()).unwrap();
        assert!(
            Library::new(&root)
                .unwrap()
                .execute(request(&old))
                .unwrap_err()
                .contains("trop de révisions")
        );
        fs::remove_dir_all(root).unwrap();
    }
}
