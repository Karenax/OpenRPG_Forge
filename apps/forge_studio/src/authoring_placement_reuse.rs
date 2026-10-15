//! Reuse decisions for derived roots, never canonical mutations.
use forge_project::zone_authoring::placements::ManualPlacement;

/// Actual reconciliation choice for one derived placement root.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub(super) enum ReconciliationDecision {
    Retain,
    Rebuild,
    Remove,
}

/// Authoring collections are validated and sorted by ID before projection.
pub(super) fn retained<'a>(
    source: &ManualPlacement,
    desired: &'a [ManualPlacement],
) -> Option<&'a ManualPlacement> {
    let index = desired.binary_search_by_key(&source.id, |p| p.id).ok()?;
    let target = &desired[index];
    (source.asset_id == target.asset_id
        && source.asset_hash == target.asset_hash
        && source.physics == target.physics
        && source.particles == target.particles)
        .then_some(target)
}

/// A transform/name/lock edit keeps its root. A changed asset pin rebuilds that
/// root, while a missing ID removes it. The adapter records this exact branch
/// while applying it; it does not infer categories from a later snapshot.
pub(super) fn reconciliation_decision(
    source: &ManualPlacement,
    desired: &[ManualPlacement],
) -> ReconciliationDecision {
    if retained(source, desired).is_some() {
        ReconciliationDecision::Retain
    } else if desired
        .binary_search_by_key(&source.id, |placement| placement.id)
        .is_ok()
    {
        ReconciliationDecision::Rebuild
    } else {
        ReconciliationDecision::Remove
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::{AssetId, ContentHash, EntityId, Transform};

    #[test]
    fn same_carrier_rebuilds_for_changed_physics_but_retains_authoring_edits() {
        use forge_project::zone_authoring::placements::PhysicsPackageReference;
        let source = ManualPlacement {
            particles: None,
            id: EntityId::deterministic("physical-reuse-object"),
            name: "Collision conservée".into(),
            asset_id: AssetId::deterministic("physical-reuse-asset"),
            asset_hash: ContentHash::from_bytes([3; 32]),
            physics: Some(Box::new(PhysicsPackageReference {
                schema_version: 1,
                carrier_sha256: ContentHash::from_bytes([3; 32]),
                manifest_sha256: ContentHash::from_bytes([4; 32]),
                package_sha256: ContentHash::from_bytes([5; 32]),
            })),
            transform: Transform::IDENTITY,
            locked: false,
        };
        let mut edited = source.clone();
        edited.name = "Retouche".into();
        edited.transform.translation_m.x = 7.0;
        edited.locked = true;
        assert_eq!(
            reconciliation_decision(&source, std::slice::from_ref(&edited)),
            ReconciliationDecision::Retain
        );
        for manifest_changed in [false, true] {
            let mut changed = edited.clone();
            let physics = changed.physics.as_mut().unwrap();
            if manifest_changed {
                physics.manifest_sha256 = ContentHash::from_bytes([6; 32]);
            } else {
                physics.package_sha256 = ContentHash::from_bytes([6; 32]);
            }
            assert_eq!(changed.asset_hash, source.asset_hash);
            assert_eq!(
                reconciliation_decision(&source, std::slice::from_ref(&changed)),
                ReconciliationDecision::Rebuild
            );
        }
        edited.physics = None;
        assert_eq!(
            reconciliation_decision(&source, std::slice::from_ref(&edited)),
            ReconciliationDecision::Rebuild
        );
        assert_eq!(
            reconciliation_decision(&source, &[]),
            ReconciliationDecision::Remove
        );
    }

    #[test]
    #[ignore = "Local CPU lookup comparison; not a renderer benchmark"]
    fn compare_linear_and_indexed_lookup() {
        use std::{collections::BTreeMap, hint::black_box, time::Instant};
        let ids = (0..4096)
            .map(|i| EntityId::deterministic(format!("lookup-{i}")))
            .collect::<Vec<_>>();
        let start = Instant::now();
        let linear = ids
            .iter()
            .map(|id| {
                black_box(&ids)
                    .iter()
                    .position(|candidate| candidate == id)
                    .unwrap()
            })
            .sum::<usize>();
        let linear_time = start.elapsed();
        let start = Instant::now();
        let index = ids
            .iter()
            .copied()
            .enumerate()
            .map(|(i, id)| (id, i))
            .collect::<BTreeMap<_, _>>();
        let indexed = ids
            .iter()
            .map(|id| *black_box(&index).get(id).unwrap())
            .sum::<usize>();
        let indexed_time = start.elapsed();
        assert_eq!(linear, indexed);
        println!(
            "4096 lookups: linear={linear_time:?}; indexed_including_build={indexed_time:?}; same_indices=true"
        );
    }

    #[test]
    fn reuse_preserves_identity_but_never_substitutes_asset_pins() {
        let source = ManualPlacement {
            particles: None,
            physics: None,
            id: EntityId::deterministic("reuse-object"),
            name: "Objet".into(),
            asset_id: AssetId::deterministic("reuse-asset"),
            asset_hash: ContentHash::from_bytes([3; 32]),
            transform: Transform::IDENTITY,
            locked: false,
        };
        let original = source.clone();
        let mut desired = vec![source.clone()];
        assert_eq!(retained(&source, &desired), Some(&source));
        assert_eq!(
            reconciliation_decision(&source, &desired),
            ReconciliationDecision::Retain
        );
        desired[0].transform.translation_m.x = 7.0;
        desired[0].name = "Retouché".into();
        desired[0].locked = true;
        assert_eq!(retained(&source, &desired), Some(&desired[0]));
        // Undo and redo use the same root while exchanging its derived values.
        assert_eq!(
            retained(&desired[0], std::slice::from_ref(&source)),
            Some(&source)
        );
        desired[0].asset_hash = ContentHash::from_bytes([4; 32]);
        assert!(retained(&source, &desired).is_none());
        assert_eq!(
            reconciliation_decision(&source, &desired),
            ReconciliationDecision::Rebuild
        );
        desired[0] = source.clone();
        desired[0].asset_id = AssetId::deterministic("other-asset");
        assert!(retained(&source, &desired).is_none());
        desired[0] = source.clone();
        desired[0].id = EntityId::deterministic("other-object");
        assert!(retained(&source, &desired).is_none());
        desired.push(source.clone());
        desired.sort_by_key(|p| p.id);
        assert_eq!(retained(&source, &desired), Some(&source));
        assert_eq!(
            reconciliation_decision(&source, &[]),
            ReconciliationDecision::Remove
        );
        assert_eq!(source, original);
    }
}
