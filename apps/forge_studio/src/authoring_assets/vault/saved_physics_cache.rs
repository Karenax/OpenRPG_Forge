//! Bounded historical packet cache. Carrier/manifest alone cannot identify buffers.
use forge_core::{AssetId, ContentHash};
use std::{
    collections::{BTreeMap, VecDeque},
    sync::Arc,
};

#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord)]
pub(super) struct Key {
    pub asset_id: AssetId,
    pub carrier: ContentHash,
    pub manifest: ContentHash,
    pub package: ContentHash,
}

pub(super) struct Cache {
    entries: BTreeMap<Key, Arc<[u8]>>,
    order: VecDeque<Key>,
    retained_bytes: usize,
    budget: usize,
}

impl Cache {
    pub(super) fn new(budget: usize) -> Self {
        Self {
            entries: BTreeMap::new(),
            order: VecDeque::new(),
            retained_bytes: 0,
            budget,
        }
    }

    pub(super) fn get(&mut self, key: Key) -> Option<Arc<[u8]>> {
        let bytes = self.entries.get(&key)?.clone();
        self.order.retain(|old| *old != key);
        self.order.push_back(key);
        Some(bytes)
    }

    /// Always verify before touching residents. Oversize packets stay uncached.
    /// The budget covers cache ownership, not strong handles held by live hosts.
    pub(super) fn insert(&mut self, key: Key, wire: Vec<u8>) -> Result<Arc<[u8]>, String> {
        forge_assets::source_physics_package::SourcePhysicsPackage::decode_pinned(
            &wire,
            key.carrier,
            key.manifest,
            key.package,
        )?;
        if let Some(existing) = self.get(key) {
            return Ok(existing);
        }
        let wire: Arc<[u8]> = wire.into();
        if wire.len() > self.budget {
            return Ok(wire);
        }
        while self.retained_bytes > self.budget - wire.len() || self.entries.len() >= 32 {
            let oldest = self.order.pop_front().ok_or("Cache physique incohérent")?;
            let removed = self
                .entries
                .remove(&oldest)
                .ok_or("Entrée physique absente")?;
            self.retained_bytes -= removed.len();
        }
        self.retained_bytes += wire.len();
        self.order.push_back(key);
        self.entries.insert(key, wire.clone());
        Ok(wire)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::bytes_sha256;

    fn fixture(label: &str) -> (Key, Vec<u8>) {
        let manifest = br#"{"owned_fixture":true}"#.to_vec();
        let carrier = bytes_sha256(b"same owned model");
        let manifest_hash = bytes_sha256(&manifest);
        let wire = forge_assets::source_physics_package::SourcePhysicsPackage {
            carrier_sha256: carrier,
            manifest_sha256: manifest_hash,
            manifest,
            candidates: BTreeMap::from([("shape.bin".into(), label.as_bytes().to_vec())]),
        }
        .encode()
        .unwrap();
        (
            Key {
                asset_id: AssetId::deterministic("owned cache fixture"),
                carrier,
                manifest: manifest_hash,
                package: bytes_sha256(&wire),
            },
            wire,
        )
    }

    #[test]
    fn same_model_and_manifest_keep_distinct_packages_and_asset_identities() {
        let (a, aw) = fixture("shape a");
        let (b, bw) = fixture("shape b");
        assert_eq!((a.carrier, a.manifest), (b.carrier, b.manifest));
        let mut cache = Cache::new(aw.len() + bw.len());
        let ah = cache.insert(a, aw.clone()).unwrap();
        let bh = cache.insert(b, bw.clone()).unwrap();
        assert_eq!(&*cache.get(a).unwrap(), aw.as_slice());
        assert_eq!(&*cache.get(b).unwrap(), bw.as_slice());
        assert!(!Arc::ptr_eq(&ah, &bh));
        assert!(
            cache
                .get(Key {
                    asset_id: AssetId::deterministic("other asset"),
                    ..a
                })
                .is_none()
        );
        assert_eq!(cache.retained_bytes, aw.len() + bw.len());
    }

    #[test]
    fn mismatched_packet_is_refused_without_evicting_a_verified_resident() {
        let (a, aw) = fixture("shape a");
        let (_, bw) = fixture("shape b");
        let mut cache = Cache::new(aw.len());
        let retained = cache.insert(a, aw).unwrap();
        assert!(cache.insert(a, bw).is_err());
        assert!(Arc::ptr_eq(&cache.get(a).unwrap(), &retained));
        assert_eq!(cache.entries.len(), 1);
    }

    #[test]
    fn bounded_eviction_preserves_live_handles_and_recent_reads() {
        let (a, aw) = fixture("shape a");
        let (b, bw) = fixture("shape b");
        let (c, cw) = fixture("shape c");
        let mut cache = Cache::new(aw.len() + bw.len());
        let alive = cache.insert(a, aw.clone()).unwrap();
        let evicted_handle = cache.insert(b, bw.clone()).unwrap();
        cache.get(a).unwrap();
        cache.insert(c, cw).unwrap();
        assert!(cache.get(b).is_none());
        assert_eq!(&*evicted_handle, bw.as_slice());
        assert!(cache.get(a).is_some() && cache.get(c).is_some());
        assert_eq!(&*alive, aw.as_slice());
        assert!(cache.retained_bytes <= cache.budget);
        let (large_key, large) = fixture(&"large".repeat(cache.budget));
        assert!(cache.insert(large_key, large).is_ok());
        assert!(cache.get(large_key).is_none());
        assert!(cache.get(a).is_some() && cache.get(c).is_some());
    }
}
