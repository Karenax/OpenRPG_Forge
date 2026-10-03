//! Resolve asynchronously acquired object sources against one frozen canonical head.
use forge_core::ContentHash;
use forge_project::zone_authoring::{ZoneAuthoringHead, placements::ManualPlacement};

type SourceKey = (u64, Option<ContentHash>);

pub(super) struct PlaytestPlacements {
    head: Option<Box<ZoneAuthoringHead>>,
    head_hash: ContentHash,
    key: Option<SourceKey>,
    resolved: Vec<ManualPlacement>,
}

impl PlaytestPlacements {
    pub(super) fn new(
        head: Option<ZoneAuthoringHead>,
        head_hash: ContentHash,
        snapshot: Vec<ManualPlacement>,
    ) -> Result<Self, String> {
        if let Some(head) = &head
            && head.content_hash()? != head_hash
        {
            return Err("La source des objets ne correspond pas au Playtest".into());
        }
        Ok(Self {
            head: head.map(Box::new),
            head_hash,
            key: None,
            resolved: snapshot,
        })
    }

    /// Re-resolve when verified imports or this head's population arrives. A failed
    /// resolution never returns the old list and is retried even at the same key.
    pub(super) fn resolve(
        &mut self,
        generation: u64,
        population_owner: Option<ContentHash>,
        resolver: impl FnOnce(&ZoneAuthoringHead) -> Result<Vec<ManualPlacement>, String>,
    ) -> Result<&[ManualPlacement], String> {
        let Some(head) = &self.head else {
            return Ok(&self.resolved);
        };
        let key = (
            generation,
            population_owner.filter(|owner| *owner == self.head_hash),
        );
        if self.key != Some(key) {
            let objects = resolver(head)?;
            self.resolved = objects;
            self.key = Some(key);
        }
        Ok(&self.resolved)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::{AssetId, EntityId, Transform};
    use forge_project::{authoring::new_terrain, zone_authoring::*};

    fn head() -> ZoneAuthoringHead {
        let terrain = new_terrain("playtest-sources", "test", "Zone").unwrap();
        ZoneAuthoringHead {
            schema: "forge.zone-authoring".into(),
            version: 1,
            revision: terrain.revision,
            base: ZoneBase {
                generator: GeneratorStamp {
                    name: "fixture".into(),
                    version: "1".into(),
                    inputs_hash: terrain.content_hash().unwrap(),
                },
                terrain,
                vegetation: None,
            },
            corrections: ZoneCorrections::default(),
        }
    }

    fn objects(count: usize) -> Vec<ManualPlacement> {
        let mut objects = (0..count)
            .map(|index| ManualPlacement {
                id: EntityId::deterministic(format!("late-object-{index}")),
                name: format!("Objet {index}"),
                asset_id: AssetId::deterministic(format!("late-asset-{index}")),
                asset_hash: ContentHash::from_bytes([3; 32]),
                transform: Transform::IDENTITY,
                locked: false,
            })
            .collect::<Vec<_>>();
        objects.sort_by_key(|object| object.id);
        objects
    }

    #[test]
    fn late_imports_and_population_refresh_objects_without_changing_the_head() {
        let head = head();
        let hash = head.content_hash().unwrap();
        let frozen = head.to_bytes().unwrap();
        let initial = objects(23);
        let acquired = objects(24);
        let mut source = PlaytestPlacements::new(Some(head), hash, initial.clone()).unwrap();
        assert_eq!(
            source.resolve(0, None, |_| Ok(initial.clone())).unwrap(),
            initial
        );
        assert_eq!(
            source.resolve(1, None, |_| Ok(acquired.clone())).unwrap(),
            acquired
        );
        // A ready population can change inherited poses without publishing a library.
        let mut grounded = acquired;
        grounded[0].transform.translation_m.y = 7.5;
        assert_eq!(
            source
                .resolve(1, Some(hash), |_| Ok(grounded.clone()))
                .unwrap(),
            grounded
        );
        assert_eq!(
            source
                .resolve(1, Some(hash), |_| panic!(
                    "unchanged source must reuse its resolution"
                ))
                .unwrap(),
            grounded
        );
        assert_eq!(source.head.as_ref().unwrap().to_bytes().unwrap(), frozen);
    }

    #[test]
    fn unverified_new_generation_refuses_old_objects_and_retries_the_same_generation() {
        let head = head();
        let hash = head.content_hash().unwrap();
        let initial = objects(1);
        let acquired = objects(2);
        let mut source = PlaytestPlacements::new(Some(head), hash, initial.clone()).unwrap();
        assert_eq!(
            source.resolve(0, None, |_| Ok(initial.clone())).unwrap(),
            initial
        );
        assert_eq!(
            source
                .resolve(1, None, |_| Err("Source non vérifiée".into()))
                .unwrap_err(),
            "Source non vérifiée"
        );
        assert_eq!(
            source.resolve(1, None, |_| Ok(acquired.clone())).unwrap(),
            acquired
        );
    }

    #[test]
    fn a_foreign_population_does_not_replace_the_frozen_head() {
        let frozen_head = head();
        let hash = frozen_head.content_hash().unwrap();
        let initial = objects(1);
        let mut source = PlaytestPlacements::new(Some(frozen_head), hash, initial.clone()).unwrap();
        source.resolve(0, None, |_| Ok(initial.clone())).unwrap();
        assert_eq!(
            source
                .resolve(0, Some(ContentHash::from_bytes([9; 32])), |_| panic!(
                    "foreign population must not invalidate this head"
                ))
                .unwrap(),
            initial
        );
        assert!(
            PlaytestPlacements::new(Some(head()), ContentHash::from_bytes([9; 32]), initial)
                .is_err()
        );
    }

    #[test]
    fn a_legacy_snapshot_never_inherits_new_imports() {
        let initial = objects(1);
        let mut source =
            PlaytestPlacements::new(None, ContentHash::from_bytes([8; 32]), initial.clone())
                .unwrap();
        assert_eq!(
            source
                .resolve(3, Some(ContentHash::from_bytes([9; 32])), |_| panic!(
                    "no canonical head to resolve"
                ))
                .unwrap(),
            initial
        );
    }
}
