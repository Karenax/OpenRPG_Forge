//! Read-only search over an already governed kit; no import or placement authority.
use crate::{GovernedAssetKitAsset, GovernedAssetKitBuildReport, GovernedAssetKitError};
use forge_core::{AssetId, ContentHash};

/// Resolution checks the governed manifest, not the existence of the GLB bytes.
#[derive(Clone, Debug, PartialEq, Eq, thiserror::Error)]
pub enum AssetResolutionError {
    #[error("Unknown kit asset: {0}")]
    UnknownName(String),
    #[error("Asset {0} is not available in this governed kit")]
    Missing(AssetId),
    #[error(
        "Asset {id} differs from its pinned content: expected {expected}, available {available}"
    )]
    ContentChanged {
        id: AssetId,
        expected: ContentHash,
        available: ContentHash,
    },
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct AssetBrowser {
    report: GovernedAssetKitBuildReport,
    terms: Vec<String>,
}

impl AssetBrowser {
    pub fn new(mut report: GovernedAssetKitBuildReport) -> Result<Self, GovernedAssetKitError> {
        report.validate()?;
        report.assets.sort_by(|a, b| a.id.cmp(&b.id));
        let terms = report
            .assets
            .iter()
            .map(|asset| {
                normalize(&format!(
                    "{} {} {}",
                    asset.id,
                    asset.kind,
                    category_label(&asset.kind)
                ))
            })
            .collect();
        Ok(Self { report, terms })
    }

    #[must_use]
    pub const fn report(&self) -> &GovernedAssetKitBuildReport {
        &self.report
    }

    /// Stable logical identity plus an explicit content pin. Paths, list order and
    /// kit revision are not identities; changing content never updates a saved pin.
    pub fn reference(&self, name: &str) -> Result<(AssetId, ContentHash), AssetResolutionError> {
        let index = self
            .report
            .assets
            .binary_search_by(|asset| asset.id.as_str().cmp(name))
            .map_err(|_| AssetResolutionError::UnknownName(name.into()))?;
        let asset = &self.report.assets[index];
        Ok((self.asset_id(&asset.id), asset.glb.sha256))
    }

    pub fn resolve(
        &self,
        id: AssetId,
        expected: ContentHash,
    ) -> Result<&GovernedAssetKitAsset, AssetResolutionError> {
        let asset = self
            .report
            .assets
            .iter()
            .find(|asset| self.asset_id(&asset.id) == id)
            .ok_or(AssetResolutionError::Missing(id))?;
        if asset.glb.sha256 != expected {
            return Err(AssetResolutionError::ContentChanged {
                id,
                expected,
                available: asset.glb.sha256,
            });
        }
        Ok(asset)
    }

    fn asset_id(&self, name: &str) -> AssetId {
        // Length-prefixed UTF-8 components: no ambiguous delimiter concatenation.
        let kit = &self.report.asset_kit_id;
        AssetId::deterministic(format!(
            "governed-kit/v1:{}:{kit}{}:{name}",
            kit.len(),
            name.len()
        ))
    }

    pub fn search(
        &self,
        query: &str,
        kind: Option<&str>,
    ) -> Result<Vec<&GovernedAssetKitAsset>, String> {
        if query.len() > 256 {
            return Err("Recherche limitée à 256 octets".into());
        }
        let normalized = normalize(query);
        let words: Vec<_> = normalized.split_whitespace().collect();
        Ok(self
            .report
            .assets
            .iter()
            .zip(&self.terms)
            .filter(|(asset, terms)| {
                kind.is_none_or(|kind| asset.kind == kind)
                    && words.iter().all(|word| terms.contains(word))
            })
            .map(|(asset, _)| asset)
            .collect())
    }
}

#[must_use]
pub fn category_label(kind: &str) -> &str {
    match kind {
        "tree" => "Arbres végétation",
        "rock" => "Rochers",
        "architecture" => "Architecture",
        other => other,
    }
}

fn normalize(text: &str) -> String {
    text.to_lowercase()
        .chars()
        .map(|ch| match ch {
            'é' | 'è' | 'ê' | 'ë' => 'e',
            'à' | 'â' | 'ä' => 'a',
            'î' | 'ï' => 'i',
            'ô' | 'ö' => 'o',
            'ù' | 'û' | 'ü' => 'u',
            'ç' => 'c',
            '-' | '_' => ' ',
            other => other,
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{
        GovernedAssetKitGenerator, GovernedAssetKitGlb, GovernedAssetKitProvenance,
        GovernedAssetKitQualification,
    };

    fn report() -> GovernedAssetKitBuildReport {
        let hash = ContentHash::from_bytes([1; 32]);
        GovernedAssetKitBuildReport {
            schema: "forge.blender-asset-kit-build".into(),
            schema_version: 1,
            asset_kit_id: "browser-test".into(),
            asset_kit_revision: 1,
            generator: GovernedAssetKitGenerator {
                input_manifest_sha256: hash,
            },
            provenance: GovernedAssetKitProvenance {
                source_kind: "procedural_original".into(),
                license: "LicenseRef-OpenRPG-Forge-Project-Generated".into(),
                cadle_content_used: false,
                commercial_game_content_used: false,
            },
            qualification: GovernedAssetKitQualification {
                technical_export_validated: true,
            },
            authored_source: None,
            assets: [
                ("pine-tall", "tree"),
                ("bridge-small", "architecture"),
                ("rock-flat", "rock"),
            ]
            .into_iter()
            .map(|(id, kind)| crate::GovernedAssetKitAsset {
                id: id.into(),
                kind: kind.into(),
                glb: GovernedAssetKitGlb {
                    path: format!("{id}.glb"),
                    sha256: hash,
                },
            })
            .collect(),
        }
    }

    #[test]
    fn search_is_sorted_accent_insensitive_and_does_not_mutate_sources() {
        let input = report();
        let before = input.clone();
        let browser = AssetBrowser::new(input.clone()).unwrap();
        assert_eq!(input, before);
        assert_eq!(browser.search("", None).unwrap()[0].id, "bridge-small");
        assert_eq!(
            browser.search("VEGETATION tall", None).unwrap()[0].id,
            "pine-tall"
        );
        assert_eq!(browser.search("végétation", Some("tree")).unwrap().len(), 1);
        assert_eq!(browser.search("PINE_TALL", None).unwrap().len(), 1);
        assert!(browser.search("pine", Some("rock")).unwrap().is_empty());
        assert!(browser.search(&"x".repeat(257), None).is_err());
    }

    #[test]
    fn browser_cannot_bypass_provenance_or_file_path_rules() {
        let mut input = report();
        input.provenance.cadle_content_used = true;
        assert!(AssetBrowser::new(input).is_err());
        let mut input = report();
        input.provenance.license = "CC0-1.0".into();
        assert!(AssetBrowser::new(input).is_err());
        let mut input = report();
        input.assets[0].glb.path = "../escape.glb".into();
        assert!(AssetBrowser::new(input).is_err());
    }

    #[test]
    fn saved_reference_survives_reorder_revision_and_path_changes() {
        let original = AssetBrowser::new(report()).unwrap();
        let (id, hash) = original.reference("pine-tall").unwrap();
        let mut updated = report();
        updated.asset_kit_revision += 1;
        updated.assets.reverse();
        updated
            .assets
            .iter_mut()
            .find(|a| a.id == "pine-tall")
            .unwrap()
            .glb
            .path = "relocated/pine.glb".into();
        let reopened = AssetBrowser::new(updated).unwrap();
        assert_eq!(reopened.reference("pine-tall").unwrap(), (id, hash));
        assert_eq!(
            reopened.resolve(id, hash).unwrap().glb.path,
            "relocated/pine.glb"
        );
        let serialized = serde_json::to_vec(&(id, hash)).unwrap();
        assert_eq!(
            serde_json::from_slice::<(AssetId, ContentHash)>(&serialized).unwrap(),
            (id, hash)
        );
    }

    #[test]
    fn content_change_is_not_a_silent_reimport_and_other_kits_cannot_substitute() {
        let original = AssetBrowser::new(report()).unwrap();
        let (id, hash) = original.reference("pine-tall").unwrap();
        let mut changed = report();
        changed.assets[0].glb.sha256 = ContentHash::from_bytes([2; 32]);
        let changed = AssetBrowser::new(changed).unwrap();
        assert_eq!(changed.reference("pine-tall").unwrap().0, id);
        assert!(matches!(
            changed.resolve(id, hash),
            Err(AssetResolutionError::ContentChanged { .. })
        ));
        let mut other = report();
        other.asset_kit_id = "another-kit".into();
        let other = AssetBrowser::new(other).unwrap();
        assert_ne!(other.reference("pine-tall").unwrap().0, id);
        assert_eq!(
            other.resolve(id, hash),
            Err(AssetResolutionError::Missing(id))
        );
        assert!(matches!(
            original.reference("missing"),
            Err(AssetResolutionError::UnknownName(_))
        ));
        let mut removed = report();
        removed.assets.remove(0);
        assert_eq!(
            AssetBrowser::new(removed).unwrap().resolve(id, hash),
            Err(AssetResolutionError::Missing(id))
        );
    }
}
