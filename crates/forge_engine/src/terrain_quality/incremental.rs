//! Budgeted terrain chunks per browser frame. Live meshes stay intact until completion.
use super::{
    ContentHash, PreparationOutput, PreparedLods, TerrainChunkId, TerrainChunkMesh,
    TerrainLodCache, TerrainLodTopology, TerrainQualityRequest, VisualQualityTier,
    prepare_from_snapshot,
};
use std::collections::{BTreeMap, BTreeSet};

#[derive(PartialEq)]
struct Key {
    source: ContentHash,
    policy: String,
    tier: VisualQualityTier,
    protected: BTreeSet<TerrainChunkId>,
}

pub(super) struct Preparation {
    key: Key,
    chunks: Vec<TerrainChunkMesh>,
    topology: [TerrainLodTopology; 3],
    previous_protected: BTreeSet<TerrainChunkId>,
    previous: BTreeMap<TerrainChunkId, ContentHash>,
    changed_policy: bool,
    prepared: Vec<PreparedLods>,
    next: usize,
    cpu_ms: f64,
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
    if let Some(job) = &mut cache.incremental {
        if job.key != key {
            cache.incremental = None;
            return None;
        }
        let mut budget = crate::preparation_budget::ChunkBudget::new();
        while let Some(chunk) = job.chunks.get(job.next) {
            let started = bevy::platform::time::Instant::now();
            let result = prepare_from_snapshot(
                std::slice::from_ref(chunk),
                &job.topology,
                &job.key.protected,
                &job.previous_protected,
                job.changed_policy,
                &job.previous,
            );
            job.cpu_ms += started.elapsed().as_secs_f64() * 1000.0;
            match result {
                Ok(prepared) => job.prepared.extend(prepared),
                Err(error) => {
                    let elapsed = job.cpu_ms;
                    cache.incremental = None;
                    return Some((Err(error), elapsed));
                }
            }
            job.next += 1;
            if budget.exhausted() {
                break;
            }
        }
        if job.next < job.chunks.len() {
            return None;
        }
        let result = std::mem::take(&mut job.prepared);
        let elapsed = job.cpu_ms;
        cache.incremental = None;
        return Some((Ok(result), elapsed));
    }
    cache.incremental = Some(Preparation {
        key,
        chunks: chunks.to_vec(),
        topology: topology.clone(),
        previous_protected: cache.bank_chunks.clone(),
        previous: cache
            .chunks
            .iter()
            .map(|(id, c)| (*id, c.source_hash))
            .collect(),
        changed_policy: !cache.matches_policy(request),
        prepared: Vec::with_capacity(chunks.len()),
        next: 0,
        cpu_ms: 0.0,
    });
    None
}

#[cfg(test)]
mod tests {
    use super::*;

    fn fixture() -> (TerrainQualityRequest, ContentHash, Vec<TerrainChunkMesh>) {
        let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
        let generated = forge_world::TerrainGenerationPlan::from_document(&terrain)
            .unwrap()
            .generate()
            .unwrap();
        let document = forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        (
            TerrainQualityRequest {
                source_hash: document.content_hash().unwrap().to_string(),
                document,
                tier: VisualQualityTier::High,
            },
            terrain.content_hash().unwrap(),
            generated.chunks.into_iter().take(16).collect(),
        )
    }

    #[test]
    fn bounded_chunks_match_all_profiles_and_protected_banks() {
        let (mut request, source, chunks) = fixture();
        let protected = BTreeSet::from([chunks[1].id]);
        for tier in [
            VisualQualityTier::Low,
            VisualQualityTier::Medium,
            VisualQualityTier::High,
            VisualQualityTier::Ultra,
        ] {
            request.tier = tier;
            let topology = super::super::quality_topology(request.document.preset(tier).terrain);
            let expected = prepare_from_snapshot(
                &chunks,
                &topology,
                &protected,
                &BTreeSet::new(),
                true,
                &BTreeMap::new(),
            )
            .unwrap();
            let mut cache = TerrainLodCache::default();
            assert!(
                prepare(&mut cache, &request, source, &chunks, &topology, &protected).is_none()
            );
            let mut previous = 0;
            for poll in 0..chunks.len() {
                let output = prepare(&mut cache, &request, source, &chunks, &topology, &protected);
                if let Some(output) = output {
                    assert_eq!(output.0.unwrap(), expected);
                    assert!(chunks.len() - previous <= 8);
                    assert!(cache.incremental.is_none());
                    assert!(cache.chunks.is_empty() && cache.tier.is_none());
                    break;
                }
                let next = cache.incremental.as_ref().unwrap().next;
                assert!((1..=8).contains(&(next - previous)));
                previous = next;
                assert!(poll + 1 < chunks.len(), "preparation never completed");
                assert!(cache.chunks.is_empty() && cache.tier.is_none());
            }
        }
    }

    #[test]
    fn malformed_later_chunk_does_not_publish_partial_geometry() {
        let (request, source, mut chunks) = fixture();
        chunks[1].positions_m.pop();
        let topology =
            super::super::quality_topology(request.document.preset(request.tier).terrain);
        let protected = BTreeSet::new();
        let mut cache = TerrainLodCache::default();
        assert!(prepare(&mut cache, &request, source, &chunks, &topology, &protected).is_none());
        let result = prepare(&mut cache, &request, source, &chunks, &topology, &protected)
            .or_else(|| prepare(&mut cache, &request, source, &chunks, &topology, &protected));
        assert!(result.unwrap().0.is_err());
        assert!(cache.incremental.is_none() && cache.chunks.is_empty() && cache.tier.is_none());
    }

    #[test]
    fn stale_source_policy_tier_or_banks_discards_partial_output() {
        let (request, source, chunks) = fixture();
        let topology =
            super::super::quality_topology(request.document.preset(request.tier).terrain);
        let protected = BTreeSet::from([chunks[1].id]);
        for change in 0..4 {
            let mut cache = TerrainLodCache::default();
            assert!(
                prepare(&mut cache, &request, source, &chunks, &topology, &protected).is_none()
            );
            assert!(
                prepare(&mut cache, &request, source, &chunks, &topology, &protected).is_none()
            );
            let job = cache.incremental.as_mut().unwrap();
            match change {
                0 => job.key.source = forge_core::canonical_json_sha256(&"stale").unwrap(),
                1 => job.key.policy.push('x'),
                2 => job.key.tier = VisualQualityTier::Low,
                _ => job.key.protected.clear(),
            }
            assert!(
                prepare(&mut cache, &request, source, &chunks, &topology, &protected).is_none()
            );
            assert!(cache.incremental.is_none() && cache.chunks.is_empty());
        }
    }
}
