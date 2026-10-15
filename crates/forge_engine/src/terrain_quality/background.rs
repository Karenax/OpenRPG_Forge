//! Own the geometry snapshot across frames; only the caller mutates ECS/assets.
use super::{
    ContentHash, PreparationOutput, TerrainChunkId, TerrainChunkMesh, TerrainLodCache,
    TerrainLodTopology, TerrainQualityRequest, VisualQualityTier, prepare_from_snapshot,
};
use bevy::tasks::{AsyncComputeTaskPool, Task, futures::check_ready};
use std::collections::BTreeSet;

#[derive(PartialEq)]
struct Key {
    source: ContentHash,
    policy: String,
    tier: VisualQualityTier,
    protected: BTreeSet<TerrainChunkId>,
}

pub(super) struct Preparation {
    key: Key,
    task: Task<PreparationOutput>,
}

#[cfg(test)]
impl Preparation {
    pub(super) fn is_finished(&self) -> bool {
        self.task.is_finished()
    }
}

pub(super) fn discard_completed(cache: &mut TerrainLodCache) {
    if cache
        .pending
        .as_mut()
        .is_some_and(|p| check_ready(&mut p.task).is_some())
    {
        cache.pending = None;
    }
}

pub(super) fn prepare(
    cache: &mut TerrainLodCache,
    request: &TerrainQualityRequest,
    source: ContentHash,
    chunks: &[TerrainChunkMesh],
    topology: &[TerrainLodTopology; 3],
    protected: &BTreeSet<TerrainChunkId>,
) -> Option<PreparationOutput> {
    let key = Key {
        source,
        policy: request.source_hash.clone(),
        tier: request.tier,
        protected: protected.clone(),
    };
    if let Some(job) = &mut cache.pending {
        let output = check_ready(&mut job.task)?;
        let current = job.key == key;
        cache.pending = None;
        return current.then_some(output);
    }
    let Some(pool) = AsyncComputeTaskPool::try_get() else {
        return Some((Err("native terrain compute pool unavailable".into()), 0.0));
    };
    let chunks = chunks.to_vec();
    let topology = topology.clone();
    let protected = protected.clone();
    let previous_protected = cache.bank_chunks.clone();
    let changed_policy = !cache.matches_policy(request);
    let previous = cache
        .chunks
        .iter()
        .map(|(id, c)| (*id, c.source_hash))
        .collect();
    cache.pending = Some(Preparation {
        key,
        task: pool.spawn(async move {
            let started = bevy::platform::time::Instant::now();
            let result = prepare_from_snapshot(
                &chunks,
                &topology,
                &protected,
                &previous_protected,
                changed_policy,
                &previous,
            );
            (result, started.elapsed().as_secs_f64() * 1000.0)
        }),
    });
    None
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::tasks::TaskPool;

    fn fixture() -> (TerrainQualityRequest, ContentHash, Vec<TerrainChunkMesh>) {
        AsyncComputeTaskPool::get_or_init(TaskPool::new);
        let document = forge_world::embedded_visual_valley_terrain_document().unwrap();
        let generated = forge_world::TerrainGenerationPlan::from_document(&document)
            .unwrap()
            .generate()
            .unwrap();
        let policy = forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        (
            TerrainQualityRequest {
                source_hash: policy.content_hash().unwrap().to_string(),
                document: policy,
                tier: VisualQualityTier::High,
            },
            document.content_hash().unwrap(),
            generated.chunks[..2].to_vec(),
        )
    }

    fn finish(
        cache: &mut TerrainLodCache,
        request: &TerrainQualityRequest,
        source: ContentHash,
        chunks: &[TerrainChunkMesh],
        topology: &[TerrainLodTopology; 3],
        protected: &BTreeSet<TerrainChunkId>,
    ) -> Option<PreparationOutput> {
        let deadline = std::time::Instant::now() + std::time::Duration::from_secs(30);
        loop {
            let result = prepare(cache, request, source, chunks, topology, protected);
            if cache.pending.is_none() {
                return result;
            }
            assert!(
                std::time::Instant::now() < deadline,
                "terrain worker timed out"
            );
            std::thread::yield_now();
        }
    }

    #[test]
    fn detached_preparation_matches_serial_geometry_including_protected_banks() {
        let (request, source, chunks) = fixture();
        let topology =
            super::super::quality_topology(request.document.preset(request.tier).terrain);
        let protected = BTreeSet::from([chunks[1].id]);
        let mut cache = TerrainLodCache::default();
        let serial = prepare_from_snapshot(
            &chunks,
            &topology,
            &protected,
            &BTreeSet::new(),
            true,
            &std::collections::BTreeMap::new(),
        )
        .unwrap();
        assert!(prepare(&mut cache, &request, source, &chunks, &topology, &protected).is_none());
        assert!(cache.chunks.is_empty() && cache.tier.is_none());
        let (result, _) =
            finish(&mut cache, &request, source, &chunks, &topology, &protected).unwrap();
        assert_eq!(result.unwrap(), serial);
        assert!(cache.chunks.is_empty() && cache.tier.is_none());
    }

    #[test]
    fn completed_result_rejects_stale_source_policy_tier_and_bank_set() {
        let (mut request, source, chunks) = fixture();
        let topology =
            super::super::quality_topology(request.document.preset(request.tier).terrain);
        let protected = BTreeSet::from([chunks[1].id]);
        for changed in 0..5 {
            request.tier = VisualQualityTier::High;
            let mut key = Key {
                source,
                policy: request.source_hash.clone(),
                tier: request.tier,
                protected: protected.clone(),
            };
            match changed {
                1 => key.source = forge_core::canonical_json_sha256(&"other source").unwrap(),
                2 => key.policy.push('x'),
                3 => key.tier = VisualQualityTier::Low,
                4 => key.protected.clear(),
                _ => {}
            }
            let mut cache = TerrainLodCache {
                pending: Some(Preparation {
                    key,
                    task: AsyncComputeTaskPool::get()
                        .spawn(async { (Err("worker failure".into()), 0.0) }),
                }),
                ..Default::default()
            };
            let result = finish(&mut cache, &request, source, &chunks, &topology, &protected);
            if changed == 0 {
                assert_eq!(result.unwrap().0.err().as_deref(), Some("worker failure"));
            } else {
                assert!(result.is_none());
            }
            assert!(cache.pending.is_none() && cache.chunks.is_empty());
        }
    }
}
