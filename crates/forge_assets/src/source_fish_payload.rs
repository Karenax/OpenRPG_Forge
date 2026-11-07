//! Logical payload access for a verified source-fish profile.
//!
//! The caller must validate the package, provenance and content pins before
//! passing it to a renderer. This interface does not choose a storage format.
use crate::{AssetCatalog, AssetKind};
use forge_core::{AssetId, ContentHash};
use serde::Deserialize;
use std::collections::{BTreeMap, BTreeSet};

pub trait SourceFishPayload {
    /// Return the verified bytes for an explicitly named profile resource.
    fn file(&self, name: &str) -> Option<&[u8]>;
}

/// A caller-admitted profile supplies its inventory and its own asset identities.
/// This describes verified content without prescribing a module or storage path.
pub trait SourceFishProfile: SourceFishPayload {
    fn description(&self) -> &[u8];
    fn inventory(&self) -> (usize, usize);
    /// Return `None` for a verified source row that is not an authored asset.
    fn identify(&self, ordinal: usize, source_id: &str) -> Result<Option<AssetId>, String>;
}

/// Compare an existing catalog with the exact asset identities declared by its caller.
///
/// # Errors
/// Rejects invalid catalog metadata or any missing, extra or differently pinned asset.
pub fn check_catalog_pins(
    catalog: &AssetCatalog,
    expected: &[(AssetId, ContentHash)],
) -> Result<(), String> {
    catalog.validate_import_metadata()?;
    let mut actual: Vec<_> = catalog
        .assets
        .iter()
        .map(|asset| (asset.id, asset.content_hash))
        .collect();
    actual.sort_unstable();
    if actual != expected {
        return Err("Fish catalog identities differ from declared pins".into());
    }
    Ok(())
}

/// Bind an already admitted payload to the existing source asset catalog.
/// The caller verifies the package, catalog provenance and distribution rights.
pub struct CatalogFishProfile<'a, P: SourceFishPayload + ?Sized> {
    payload: &'a P,
    description: &'a [u8],
    inventory: (usize, usize),
    source_ids: Vec<String>,
    selected: BTreeMap<String, AssetId>,
}

impl<'a, P: SourceFishPayload + ?Sized> CatalogFishProfile<'a, P> {
    /// Admit a catalog whose selected mesh identities all belong to this profile.
    ///
    /// # Errors
    /// Rejects invalid inventory, ambiguous or absent source identities, catalog
    /// metadata, asset kinds, pins, or the named profile description.
    pub fn new(
        payload: &'a P,
        description_name: &str,
        inventory: (usize, usize),
        catalog: &AssetCatalog,
        package_hash: ContentHash,
    ) -> Result<Self, String> {
        #[derive(Deserialize)]
        struct Description {
            prototypes: Vec<serde::de::IgnoredAny>,
            instances: Vec<Instance>,
        }
        #[derive(Deserialize)]
        struct Instance {
            source_id: String,
        }
        if description_name.len() > 256
            || !description_name.bytes().all(|byte| {
                byte.is_ascii_alphanumeric() || matches!(byte, b'_' | b'-' | b'.' | b'/')
            })
            || description_name
                .split('/')
                .any(|part| matches!(part, "" | "." | ".."))
            || inventory.0 == 0
            || inventory.0 > 4096
            || inventory.1 == 0
            || inventory.1 > 131_072
        {
            return Err("Invalid source fish description or inventory".into());
        }
        let description = payload
            .file(description_name)
            .ok_or("Fish profile description absent")?;
        if description.len() > 4 * 1024 * 1024 {
            return Err("Fish profile description exceeds its byte budget".into());
        }
        let parsed: Description =
            serde_json::from_slice(description).map_err(|error| error.to_string())?;
        if (parsed.prototypes.len(), parsed.instances.len()) != inventory {
            return Err("Fish profile inventory differs".into());
        }
        let source_ids: Vec<_> = parsed
            .instances
            .into_iter()
            .map(|instance| instance.source_id)
            .collect();
        let mut unique = BTreeSet::new();
        if source_ids.iter().any(|id| {
            id.trim().is_empty()
                || id.len() > 256
                || id.chars().any(char::is_control)
                || !unique.insert(id.as_str())
        }) {
            return Err("Fish profile source identities are invalid or repeated".into());
        }
        catalog.validate_import_metadata()?;
        let mut selected = BTreeMap::new();
        for asset in &catalog.assets {
            if asset.kind != AssetKind::Mesh
                || asset.content_hash != package_hash
                || !unique.contains(asset.logical_name.as_str())
            {
                return Err("Fish catalog asset or package pin differs".into());
            }
            selected.insert(asset.logical_name.clone(), asset.id);
        }
        Ok(Self {
            payload,
            description,
            inventory,
            source_ids,
            selected,
        })
    }
}

impl<P: SourceFishPayload + ?Sized> SourceFishPayload for CatalogFishProfile<'_, P> {
    fn file(&self, name: &str) -> Option<&[u8]> {
        self.payload.file(name)
    }
}

impl<P: SourceFishPayload + ?Sized> SourceFishProfile for CatalogFishProfile<'_, P> {
    fn description(&self) -> &[u8] {
        self.description
    }
    fn inventory(&self) -> (usize, usize) {
        self.inventory
    }
    fn identify(&self, ordinal: usize, source_id: &str) -> Result<Option<AssetId>, String> {
        if self.source_ids.get(ordinal).map(String::as_str) != Some(source_id) {
            return Err("Fish profile identity or order differs".into());
        }
        Ok(self.selected.get(source_id).copied())
    }
}

/// Own an admitted payload and its existing catalog for a renderer or transport.
/// Package provenance, byte pins and rights remain the caller's responsibility.
pub struct OwnedFishProfile<P: SourceFishPayload> {
    pub package_sha256: ContentHash,
    pub payload: P,
    pub catalog: AssetCatalog,
    description: String,
    inventory: (usize, usize),
}

impl<P: SourceFishPayload> OwnedFishProfile<P> {
    /// Bind the admitted payload to its explicit catalog and profile inventory.
    ///
    /// # Errors
    /// Rejects invalid inventories, catalog entries, identities or package pins.
    pub fn new(
        package_sha256: ContentHash,
        payload: P,
        catalog: AssetCatalog,
        description: String,
        inventory: (usize, usize),
    ) -> Result<Self, String> {
        let _ =
            CatalogFishProfile::new(&payload, &description, inventory, &catalog, package_sha256)?;
        Ok(Self {
            package_sha256,
            payload,
            catalog,
            description,
            inventory,
        })
    }

    /// Obtain the same checked profile view for the common engine.
    ///
    /// # Errors
    /// Rejects a payload or catalog that no longer matches the admitted profile.
    pub fn profile(&self) -> Result<CatalogFishProfile<'_, P>, String> {
        CatalogFishProfile::new(
            &self.payload,
            &self.description,
            self.inventory,
            &self.catalog,
            self.package_sha256,
        )
    }
}

impl SourceFishPayload for BTreeMap<String, Vec<u8>> {
    fn file(&self, name: &str) -> Option<&[u8]> {
        self.get(name).map(Vec::as_slice)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{AssetRecord, AssetSource};
    use forge_core::bytes_sha256;

    fn fixture() -> (BTreeMap<String, Vec<u8>>, AssetCatalog, ContentHash) {
        let hash = bytes_sha256(b"verified profile package");
        let payload = BTreeMap::from([(
            "geometry/profile.json".into(),
            br#"{"prototypes":[{}],"instances":[{"source_id":"other/fish/0"},{"source_id":"other/fish/1"}]}"#.to_vec(),
        )]);
        let catalog = AssetCatalog {
            schema_version: 1,
            assets: vec![AssetRecord {
                id: AssetId::deterministic("other/fish/1"),
                logical_name: "other/fish/1".into(),
                kind: AssetKind::Mesh,
                source: AssetSource::External {
                    uri: "https://example.invalid/profile".into(),
                    declared_license: Some("MIT".into()),
                },
                content_hash: hash,
            }],
        };
        (payload, catalog, hash)
    }

    #[test]
    fn catalog_selects_existing_source_id_without_inventing_a_format() {
        let (payload, catalog, hash) = fixture();
        let profile =
            CatalogFishProfile::new(&payload, "geometry/profile.json", (1, 2), &catalog, hash)
                .unwrap();
        assert_eq!(profile.inventory(), (1, 2));
        assert_eq!(profile.identify(0, "other/fish/0").unwrap(), None);
        assert_eq!(
            profile.identify(1, "other/fish/1").unwrap(),
            Some(catalog.assets[0].id)
        );
        assert!(profile.identify(0, "other/fish/1").is_err());
    }

    #[test]
    fn catalog_binding_refuses_foreign_pins_names_and_inventories() {
        let (mut payload, mut catalog, hash) = fixture();
        assert!(
            CatalogFishProfile::new(&payload, "../profile.json", (1, 2), &catalog, hash).is_err()
        );
        assert!(
            CatalogFishProfile::new(&payload, "geometry/profile.json", (1, 3), &catalog, hash)
                .is_err()
        );
        assert!(
            CatalogFishProfile::new(
                &payload,
                "geometry/profile.json",
                (1, 2),
                &catalog,
                bytes_sha256(b"other")
            )
            .is_err()
        );
        catalog.assets[0].logical_name = "other/missing".into();
        assert!(
            CatalogFishProfile::new(&payload, "geometry/profile.json", (1, 2), &catalog, hash)
                .is_err()
        );
        catalog.assets[0].logical_name = "other/fish/1".into();
        payload.insert("geometry/profile.json".into(), br#"{"prototypes":[{}],"instances":[{"source_id":"other/fish/1"},{"source_id":"other/fish/1"}]}"#.to_vec());
        assert!(
            CatalogFishProfile::new(&payload, "geometry/profile.json", (1, 2), &catalog, hash)
                .is_err()
        );
    }

    #[test]
    fn owned_profile_reuses_the_catalog_contract_and_refuses_later_drift() {
        let (payload, catalog, hash) = fixture();
        let mut owned = OwnedFishProfile::new(
            hash,
            payload,
            catalog,
            "geometry/profile.json".into(),
            (1, 2),
        )
        .unwrap();
        assert_eq!(
            owned
                .profile()
                .unwrap()
                .identify(1, "other/fish/1")
                .unwrap(),
            Some(owned.catalog.assets[0].id)
        );
        owned.catalog.assets[0].content_hash = bytes_sha256(b"different package");
        assert!(owned.profile().is_err());
    }
}
