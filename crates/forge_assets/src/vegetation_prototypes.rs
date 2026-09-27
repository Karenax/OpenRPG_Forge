//! Engine-neutral presentation bindings. Scatter poses remain in the vegetation document.
use crate::{GovernedAssetKitBuildReport, GovernedAssetKitError};
use forge_core::{CanonicalJsonError, ContentHash, EntityId, canonical_json_sha256};
use serde::{Deserialize, Serialize};
use std::collections::HashSet;
use thiserror::Error;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationPrototypeCatalog {
    pub schema: String,
    pub schema_version: u32,
    pub vegetation_sha256: ContentHash,
    pub asset_kit_id: String,
    pub asset_kit_revision: u32,
    pub bindings: Vec<VegetationPrototypeBinding>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationPrototypeBinding {
    pub layer_id: String,
    pub asset_id: String,
    pub glb_sha256: ContentHash,
    /// Root pivot in GLB right-handed, Y-up metres, before placement transforms.
    pub source_pivot_m: [f32; 3],
    pub source_height_m: f32,
    pub target_height_m: f32,
}

/// Explicit selection from a source catalog, not a persisted world or asset copy.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct VegetationPrototypeSelection {
    pub source_layer_id: String,
    pub asset_id: String,
    pub target_layer_id: String,
}

impl VegetationPrototypeBinding {
    /// Must be used only after catalog validation, before the unchanged scatter pose.
    #[must_use]
    pub fn uniform_scale(&self) -> f32 {
        self.target_height_m / self.source_height_m
    }
}

impl VegetationPrototypeCatalog {
    /// Resolve a saved reference to this exact catalog, including its normalization metadata.
    /// Hashing the source vegetation alone would not detect an altered pivot or target height.
    pub fn prepare_pinned_selection(
        &self,
        expected_catalog: ContentHash,
        target: ContentHash,
        tree_layer_ids: &[&str],
        selections: &[VegetationPrototypeSelection],
        kit: &GovernedAssetKitBuildReport,
    ) -> Result<Self, VegetationPrototypeError> {
        if self.bindings.len() > 64 {
            return Err(VegetationPrototypeError::LayerCoverage);
        }
        let actual = canonical_json_sha256(self)
            .map_err(|error| VegetationPrototypeError::CatalogHash(error.to_string()))?;
        if actual != expected_catalog {
            return Err(VegetationPrototypeError::CatalogHash(
                "saved source catalog differs from supplied metadata".into(),
            ));
        }
        self.prepare_selection(
            self.vegetation_sha256,
            target,
            tree_layer_ids,
            selections,
            kit,
        )
    }

    /// Prepare a complete catalog for authored tree layers without changing either source.
    /// The caller supplies pinned source/target document hashes and authoritative target IDs.
    /// This validates metadata; actual GLB bytes still require the normal asset resolver.
    pub fn prepare_selection(
        &self,
        expected_source: ContentHash,
        target: ContentHash,
        tree_layer_ids: &[&str],
        selections: &[VegetationPrototypeSelection],
        kit: &GovernedAssetKitBuildReport,
    ) -> Result<Self, VegetationPrototypeError> {
        if selections.is_empty() || selections.len() > 64 || self.bindings.len() > 64 {
            return Err(VegetationPrototypeError::LayerCoverage);
        }
        let source_layers: std::collections::BTreeSet<_> = self
            .bindings
            .iter()
            .map(|binding| binding.layer_id.as_str())
            .collect();
        self.validate_against(
            expected_source,
            &source_layers.into_iter().collect::<Vec<_>>(),
            kit,
        )?;
        let mut bindings = Vec::with_capacity(selections.len());
        for selection in selections {
            let mut binding = self
                .bindings
                .iter()
                .find(|binding| {
                    binding.layer_id == selection.source_layer_id
                        && binding.asset_id == selection.asset_id
                })
                .ok_or_else(|| VegetationPrototypeError::Selection {
                    layer_id: selection.source_layer_id.clone(),
                    asset_id: selection.asset_id.clone(),
                })?
                .clone();
            binding.layer_id.clone_from(&selection.target_layer_id);
            bindings.push(binding);
        }
        bindings.sort_by(|a, b| (&a.layer_id, &a.asset_id).cmp(&(&b.layer_id, &b.asset_id)));
        let proposal = Self {
            schema: self.schema.clone(),
            schema_version: 2,
            vegetation_sha256: target,
            asset_kit_id: self.asset_kit_id.clone(),
            asset_kit_revision: self.asset_kit_revision,
            bindings,
        };
        proposal.validate_against(target, tree_layer_ids, kit)?;
        Ok(proposal)
    }

    /// Presentation-only selection on a previously validated catalog. Equal-weight rendezvous
    /// hashing is independent of list order, LOD and quality; changing asset bytes or pivot does
    /// not reassign a tree. Adding an asset can only move trees to that new asset.
    /// Missing layers are explicit; no procedural fallback is fabricated here.
    pub fn select_binding(
        &self,
        layer_id: &str,
        placement: EntityId,
    ) -> Result<Option<&VegetationPrototypeBinding>, CanonicalJsonError> {
        let mut selected: Option<(&VegetationPrototypeBinding, ContentHash)> = None;
        for binding in self
            .bindings
            .iter()
            .filter(|binding| binding.layer_id == layer_id)
        {
            let score = canonical_json_sha256(&(
                "forge.vegetation-prototype-choice.v2",
                layer_id,
                placement,
                &binding.asset_id,
            ))?;
            if selected.is_none_or(|(previous, previous_score)| {
                (score.as_bytes(), &binding.asset_id)
                    > (previous_score.as_bytes(), &previous.asset_id)
            }) {
                selected = Some((binding, score));
            }
        }
        Ok(selected.map(|(binding, _)| binding))
    }

    /// Parse the closed schema; call `validate_against` with authoritative inputs next.
    pub fn from_json(json: &str) -> Result<Self, serde_json::Error> {
        serde_json::from_str(json)
    }
    /// Validate against authoritative inputs, not a cached list of runtime entities.
    /// The caller supplies only the IDs of tree layers from the pinned document.
    /// Missing bindings are refused: opting into a catalog cannot silently fall back.
    pub fn validate_against(
        &self,
        vegetation_sha256: ContentHash,
        tree_layer_ids: &[&str],
        kit: &GovernedAssetKitBuildReport,
    ) -> Result<(), VegetationPrototypeError> {
        kit.validate()?;
        if self.schema != "forge.vegetation-prototype-catalog"
            || !matches!(self.schema_version, 1 | 2)
        {
            return Err(VegetationPrototypeError::Schema);
        }
        if self.vegetation_sha256 != vegetation_sha256 {
            return Err(VegetationPrototypeError::StaleVegetation);
        }
        if self.asset_kit_id != kit.asset_kit_id
            || self.asset_kit_revision != kit.asset_kit_revision
        {
            return Err(VegetationPrototypeError::StaleKit);
        }
        let expected: HashSet<_> = tree_layer_ids.iter().copied().collect();
        if self.bindings.is_empty()
            || self.bindings.len() > 64
            || expected.len() != tree_layer_ids.len()
        {
            return Err(VegetationPrototypeError::LayerCoverage);
        }
        let mut observed = HashSet::new();
        let mut variants = HashSet::new();
        for binding in &self.bindings {
            if !expected.contains(binding.layer_id.as_str())
                || (self.schema_version == 1 && observed.contains(binding.layer_id.as_str()))
                || !variants.insert((binding.layer_id.as_str(), binding.asset_id.as_str()))
            {
                return Err(VegetationPrototypeError::LayerCoverage);
            }
            observed.insert(binding.layer_id.as_str());
            let asset = kit
                .assets
                .iter()
                .find(|asset| asset.id == binding.asset_id)
                .ok_or_else(|| VegetationPrototypeError::Asset(binding.asset_id.clone()))?;
            if asset.kind != "tree" || asset.glb.sha256 != binding.glb_sha256 {
                return Err(VegetationPrototypeError::Asset(binding.asset_id.clone()));
            }
            let dimensions = [binding.source_height_m, binding.target_height_m];
            if dimensions
                .iter()
                .any(|value| !value.is_finite() || !(0.01..=1000.0).contains(value))
                || binding
                    .source_pivot_m
                    .iter()
                    .any(|value| !value.is_finite() || value.abs() > 1000.0)
                || !(0.01..=100.0).contains(&binding.uniform_scale())
            {
                return Err(VegetationPrototypeError::Dimensions(
                    binding.layer_id.clone(),
                ));
            }
        }
        if observed != expected {
            return Err(VegetationPrototypeError::LayerCoverage);
        }
        Ok(())
    }
}

#[derive(Debug, Error)]
pub enum VegetationPrototypeError {
    #[error("prototype catalog hash: {0}")]
    CatalogHash(String),
    #[error("source catalog has no prototype {asset_id} on layer {layer_id}")]
    Selection { layer_id: String, asset_id: String },
    #[error("unsupported vegetation prototype catalog schema")]
    Schema,
    #[error("vegetation prototype catalog references a stale vegetation document")]
    StaleVegetation,
    #[error("vegetation prototype catalog references a stale asset kit")]
    StaleKit,
    #[error(
        "prototype bindings must cover each tree layer; v1 requires one per layer, v2 unique layer/asset pairs (maximum 64 bindings)"
    )]
    LayerCoverage,
    #[error("prototype asset {0} is missing, not a tree, or has a different GLB hash")]
    Asset(String),
    #[error("invalid prototype normalization for layer {0}")]
    Dimensions(String),
    #[error(transparent)]
    Kit(#[from] GovernedAssetKitError),
}

#[cfg(test)]
mod tests {
    use super::*;

    fn fixture() -> (VegetationPrototypeCatalog, GovernedAssetKitBuildReport) {
        let kit: GovernedAssetKitBuildReport = serde_json::from_value(serde_json::json!({
            "schema":"forge.blender-asset-kit-build", "schema_version":1,
            "asset_kit_id":"test-kit", "asset_kit_revision":1,
            "generator":{"input_manifest_sha256":"11".repeat(32)},
            "provenance":{"source_kind":"procedural_original",
                "license":"LicenseRef-OpenRPG-Forge-Project-Generated",
                "cadle_content_used":false,"commercial_game_content_used":false},
            "qualification":{"technical_export_validated":true},
            "assets":[{"id":"test-tree","kind":"tree",
                "glb":{"path":"assets/tree.glb","sha256":"22".repeat(32)}}]
        }))
        .unwrap();
        let catalog = VegetationPrototypeCatalog {
            schema: "forge.vegetation-prototype-catalog".into(),
            schema_version: 1,
            vegetation_sha256: ContentHash::from_bytes([3; 32]),
            asset_kit_id: kit.asset_kit_id.clone(),
            asset_kit_revision: 1,
            bindings: vec![VegetationPrototypeBinding {
                layer_id: "canopy".into(),
                asset_id: "test-tree".into(),
                glb_sha256: kit.assets[0].glb.sha256,
                source_pivot_m: [0.0; 3],
                source_height_m: 15.0,
                target_height_m: 5.0,
            }],
        };
        (catalog, kit)
    }

    #[test]
    fn pinned_selection_rejects_changed_metadata_even_with_same_vegetation_and_glb() {
        let (catalog, kit) = fixture();
        let pinned = canonical_json_sha256(&catalog).unwrap();
        let selection = [VegetationPrototypeSelection {
            source_layer_id: "canopy".into(),
            asset_id: "test-tree".into(),
            target_layer_id: "authored-trees".into(),
        }];
        let target = ContentHash::from_bytes([9; 32]);
        let resolved = catalog
            .prepare_pinned_selection(pinned, target, &["authored-trees"], &selection, &kit)
            .unwrap();
        assert_eq!(resolved.vegetation_sha256, target);
        let mut altered = catalog.clone();
        altered.bindings[0].target_height_m = 10.0;
        assert!(
            altered
                .prepare_pinned_selection(pinned, target, &["authored-trees"], &selection, &kit)
                .is_err()
        );
        altered = catalog.clone();
        altered.bindings[0].source_pivot_m[0] = 1.0;
        assert!(
            altered
                .prepare_pinned_selection(pinned, target, &["authored-trees"], &selection, &kit)
                .is_err()
        );
        let reopened =
            VegetationPrototypeCatalog::from_json(&serde_json::to_string_pretty(&catalog).unwrap())
                .unwrap();
        assert_eq!(
            reopened
                .prepare_pinned_selection(pinned, target, &["authored-trees"], &selection, &kit)
                .unwrap(),
            resolved
        );
    }

    #[test]
    fn authored_selection_is_explicit_deterministic_and_preserves_normalization() {
        let (catalog, kit) = fixture();
        let original = catalog.clone();
        let target = ContentHash::from_bytes([7; 32]);
        let mut selections = ["trees_b", "trees_a"].map(|layer| VegetationPrototypeSelection {
            source_layer_id: "canopy".into(),
            asset_id: "test-tree".into(),
            target_layer_id: layer.into(),
        });
        let first = catalog
            .prepare_selection(
                catalog.vegetation_sha256,
                target,
                &["trees_a", "trees_b"],
                &selections,
                &kit,
            )
            .unwrap();
        selections.reverse();
        assert_eq!(
            first,
            catalog
                .prepare_selection(
                    catalog.vegetation_sha256,
                    target,
                    &["trees_b", "trees_a"],
                    &selections,
                    &kit
                )
                .unwrap()
        );
        assert_eq!(catalog, original);
        assert_eq!(first.schema_version, 2);
        assert_eq!(first.vegetation_sha256, target);
        for binding in &first.bindings {
            let mut restored = binding.clone();
            restored.layer_id = "canopy".into();
            assert_eq!(restored, catalog.bindings[0]);
            assert_eq!(
                first
                    .select_binding(&binding.layer_id, EntityId::deterministic("tree"))
                    .unwrap(),
                Some(binding)
            );
        }
        let reopened =
            VegetationPrototypeCatalog::from_json(&serde_json::to_string(&first).unwrap()).unwrap();
        reopened
            .validate_against(target, &["trees_a", "trees_b"], &kit)
            .unwrap();
        assert_eq!(first, reopened);
    }

    #[test]
    fn authored_selection_rejects_stale_missing_duplicate_and_uncovered_inputs() {
        let (catalog, kit) = fixture();
        let hash = catalog.vegetation_sha256;
        let target = ContentHash::from_bytes([7; 32]);
        let selected = VegetationPrototypeSelection {
            source_layer_id: "canopy".into(),
            asset_id: "test-tree".into(),
            target_layer_id: "trees".into(),
        };
        let prepare = |source_hash, layers: &[&str], choices: &[VegetationPrototypeSelection]| {
            catalog.prepare_selection(source_hash, target, layers, choices, &kit)
        };
        assert!(matches!(
            prepare(target, &["trees"], std::slice::from_ref(&selected)),
            Err(VegetationPrototypeError::StaleVegetation)
        ));
        assert!(prepare(hash, &["trees"], &[]).is_err());
        assert!(prepare(hash, &["trees", "missing"], std::slice::from_ref(&selected)).is_err());
        assert!(prepare(hash, &["wrong"], std::slice::from_ref(&selected)).is_err());
        assert!(prepare(hash, &["trees"], &[selected.clone(), selected.clone()]).is_err());
        assert!(prepare(hash, &["trees"], &vec![selected.clone(); 65]).is_err());
        let mut missing = selected.clone();
        missing.source_layer_id = "unknown".into();
        assert!(matches!(
            prepare(hash, &["trees"], &[missing]),
            Err(VegetationPrototypeError::Selection { .. })
        ));
        let mut stale_kit = kit.clone();
        stale_kit.asset_kit_revision += 1;
        assert!(matches!(
            catalog.prepare_selection(
                hash,
                target,
                &["trees"],
                std::slice::from_ref(&selected),
                &stale_kit
            ),
            Err(VegetationPrototypeError::StaleKit)
        ));
        let mut altered = catalog.clone();
        altered.bindings[0].glb_sha256 = target;
        assert!(matches!(
            altered.prepare_selection(hash, target, &["trees"], &[selected], &kit),
            Err(VegetationPrototypeError::Asset(_))
        ));
    }

    #[test]
    fn variants_are_versioned_and_keep_existing_single_binding_choices() {
        let (mut catalog, mut kit) = fixture();
        let tree = EntityId::deterministic("tree-17");
        assert_eq!(
            catalog
                .select_binding("canopy", tree)
                .unwrap()
                .unwrap()
                .asset_id,
            "test-tree"
        );
        assert!(catalog.select_binding("missing", tree).unwrap().is_none());
        let mut alternative = catalog.bindings[0].clone();
        alternative.asset_id = "other-tree".into();
        let mut asset = kit.assets[0].clone();
        asset.id = alternative.asset_id.clone();
        asset.glb.path = "assets/other-tree.glb".into();
        kit.assets.push(asset);
        catalog.bindings.push(alternative);
        assert!(
            catalog
                .validate_against(catalog.vegetation_sha256, &["canopy"], &kit)
                .is_err()
        );
        catalog.schema_version = 2;
        catalog
            .validate_against(catalog.vegetation_sha256, &["canopy"], &kit)
            .unwrap();
        let restored =
            VegetationPrototypeCatalog::from_json(&serde_json::to_string(&catalog).unwrap())
                .unwrap();
        assert_eq!(catalog, restored);
        catalog.bindings.push(catalog.bindings[0].clone());
        assert!(
            catalog
                .validate_against(catalog.vegetation_sha256, &["canopy"], &kit)
                .is_err()
        );
    }

    #[test]
    fn choice_survives_order_reimport_and_only_moves_to_added_variant() {
        let (mut catalog, _) = fixture();
        catalog.schema_version = 2;
        let mut second = catalog.bindings[0].clone();
        second.asset_id = "second".into();
        catalog.bindings.push(second);
        let choose = |catalog: &VegetationPrototypeCatalog, index| {
            catalog
                .select_binding("canopy", EntityId::deterministic(format!("tree-{index}")))
                .unwrap()
                .unwrap()
                .asset_id
                .clone()
        };
        let before: Vec<_> = (0..256).map(|index| choose(&catalog, index)).collect();
        assert!(before.iter().any(|id| id == "second"));
        assert!(before.iter().any(|id| id == "test-tree"));
        catalog.bindings.reverse();
        catalog.bindings[0].source_pivot_m[1] = 0.25;
        catalog.bindings[0].glb_sha256 = ContentHash::from_bytes([9; 32]);
        assert_eq!(
            before,
            (0..256)
                .map(|index| choose(&catalog, index))
                .collect::<Vec<_>>()
        );
        let mut third = catalog.bindings[0].clone();
        third.asset_id = "third".into();
        catalog.bindings.push(third);
        let after: Vec<_> = (0..256).map(|index| choose(&catalog, index)).collect();
        assert!(after.iter().any(|id| id == "third"));
        for (old, new) in before.iter().zip(&after) {
            assert!(old == new || new == "third");
        }
        catalog
            .bindings
            .retain(|binding| binding.asset_id != "third");
        assert_eq!(
            before,
            (0..256)
                .map(|index| choose(&catalog, index))
                .collect::<Vec<_>>()
        );
    }

    #[test]
    fn roundtrip_and_normalization_without_placement_storage() {
        let (catalog, kit) = fixture();
        catalog
            .validate_against(catalog.vegetation_sha256, &["canopy"], &kit)
            .unwrap();
        let json = serde_json::to_string(&catalog).unwrap();
        assert_eq!(
            catalog,
            serde_json::from_str::<VegetationPrototypeCatalog>(&json).unwrap()
        );
        assert_eq!(
            catalog.bindings[0].uniform_scale().to_bits(),
            (1.0_f32 / 3.0).to_bits()
        );
        let mut value = serde_json::to_value(&catalog).unwrap();
        value["placements"] = serde_json::json!([]);
        assert!(serde_json::from_value::<VegetationPrototypeCatalog>(value).is_err());
    }

    #[test]
    fn rejects_stale_sources_wrong_assets_and_incomplete_layers() {
        let (catalog, kit) = fixture();
        assert!(
            catalog
                .validate_against(ContentHash::from_bytes([4; 32]), &["canopy"], &kit)
                .is_err()
        );
        for mutate in [
            |c: &mut VegetationPrototypeCatalog| c.asset_kit_revision += 1,
            |c: &mut VegetationPrototypeCatalog| {
                c.bindings[0].glb_sha256 = ContentHash::from_bytes([0; 32]);
            },
            |c: &mut VegetationPrototypeCatalog| c.bindings[0].asset_id = "missing".into(),
            |c: &mut VegetationPrototypeCatalog| c.bindings.push(c.bindings[0].clone()),
            |c: &mut VegetationPrototypeCatalog| c.bindings.clear(),
            |c: &mut VegetationPrototypeCatalog| c.schema_version = 3,
        ] {
            let mut changed = catalog.clone();
            mutate(&mut changed);
            assert!(
                changed
                    .validate_against(catalog.vegetation_sha256, &["canopy"], &kit)
                    .is_err()
            );
        }
        assert!(
            catalog
                .validate_against(catalog.vegetation_sha256, &["canopy", "other"], &kit)
                .is_err()
        );
        let mut rock = kit.clone();
        rock.assets[0].kind = "rock".into();
        assert!(
            catalog
                .validate_against(catalog.vegetation_sha256, &["canopy"], &rock)
                .is_err()
        );
    }

    #[test]
    fn rejects_invalid_normalization_and_unsafe_provenance() {
        let (catalog, kit) = fixture();
        for height in [0.0, -1.0, f32::NAN, f32::INFINITY, 1001.0] {
            let mut changed = catalog.clone();
            changed.bindings[0].source_height_m = height;
            assert!(
                changed
                    .validate_against(catalog.vegetation_sha256, &["canopy"], &kit)
                    .is_err()
            );
        }
        let mut changed = catalog.clone();
        changed.bindings[0].source_pivot_m[0] = f32::NAN;
        assert!(
            changed
                .validate_against(catalog.vegetation_sha256, &["canopy"], &kit)
                .is_err()
        );
        let mut unsafe_kit = kit;
        unsafe_kit.provenance.cadle_content_used = true;
        assert!(
            catalog
                .validate_against(catalog.vegetation_sha256, &["canopy"], &unsafe_kit)
                .is_err()
        );
    }
}
