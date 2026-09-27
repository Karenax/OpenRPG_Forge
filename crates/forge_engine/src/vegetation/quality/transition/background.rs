//! Native-only preparation. No ECS or asset storage is accessed by the worker.
use super::{
    PreparedVegetationProjection, VegetationQualityBindings, VegetationQualityRequest,
    VisualQualityTier, prepare_vegetation_projection_with_promotions,
};
use bevy::tasks::{AsyncComputeTaskPool, Task, futures::check_ready};

type Output = (Result<PreparedVegetationProjection, String>, f64);

pub(super) struct Preparation {
    tier: VisualQualityTier,
    policy: String,
    terrain: String,
    vegetation: String,
    task: Task<Output>,
}

pub(super) fn discard_completed(bindings: &mut VegetationQualityBindings) {
    if bindings
        .pending
        .as_mut()
        .is_some_and(|job| check_ready(&mut job.task).is_some())
    {
        bindings.pending = None;
    }
}

pub(super) fn prepare(
    bindings: &mut VegetationQualityBindings,
    request: &VegetationQualityRequest,
) -> Option<Output> {
    let source_key = bindings.vegetation_key();
    if let Some(job) = &mut bindings.pending {
        let result = check_ready(&mut job.task)?;
        let current = job.tier == request.tier
            && job.policy == request.hash
            && bindings
                .terrain
                .content_hash()
                .is_ok_and(|h| h.to_string() == job.terrain)
            && source_key.as_ref().is_ok_and(|h| *h == job.vegetation);
        bindings.pending = None;
        // Never apply a completed stale job; start only the latest request next frame.
        return current.then_some(result);
    }
    let keys = bindings
        .terrain
        .content_hash()
        .map(|h| h.to_string())
        .map_err(|e| e.to_string())
        .and_then(|terrain| source_key.map(|h| (terrain, h)));
    let (terrain_hash, vegetation_hash) = match keys {
        Ok(keys) => keys,
        Err(error) => return Some((Err(error), 0.0)),
    };
    let terrain = bindings.terrain.clone();
    let vegetation = bindings.vegetation.clone();
    let paths = bindings.paths.clone();
    let rivers = bindings.rivers.clone();
    let buildings = bindings.buildings.clone();
    let promotions = bindings.promotions.clone();
    let requested = request.clone();
    let Some(pool) = AsyncComputeTaskPool::try_get() else {
        return Some((
            Err("native vegetation compute pool unavailable".into()),
            0.0,
        ));
    };
    bindings.pending = Some(Preparation {
        tier: request.tier,
        policy: request.hash.clone(),
        terrain: terrain_hash,
        vegetation: vegetation_hash,
        task: pool.spawn(async move {
            let started = bevy::platform::time::Instant::now();
            let result = prepare_vegetation_projection_with_promotions(
                &terrain,
                &vegetation,
                &paths,
                &rivers,
                &buildings,
                &promotions,
                Some(&requested),
            )
            .map_err(|error| error.to_string());
            (result, started.elapsed().as_secs_f64() * 1000.0)
        }),
    });
    None
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::tasks::TaskPool;

    fn fixture() -> (VegetationQualityBindings, VegetationQualityRequest) {
        AsyncComputeTaskPool::get_or_init(TaskPool::new);
        let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
        let vegetation = forge_world::embedded_visual_valley_vegetation_document().unwrap();
        let policy = forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        let request =
            VegetationQualityRequest::new(&terrain, policy, VisualQualityTier::Low).unwrap();
        (VegetationQualityBindings::new(terrain, vegetation), request)
    }

    fn settle(
        bindings: &mut VegetationQualityBindings,
        request: &VegetationQualityRequest,
    ) -> Option<Output> {
        let deadline = std::time::Instant::now() + std::time::Duration::from_secs(30);
        loop {
            let result = prepare(bindings, request);
            if bindings.pending.is_none() {
                return result;
            }
            assert!(
                std::time::Instant::now() < deadline,
                "worker did not complete"
            );
            std::thread::yield_now();
        }
    }

    #[test]
    fn native_worker_prepares_without_changing_live_bindings() {
        let (mut bindings, request) = fixture();
        assert!(prepare(&mut bindings, &request).is_none());
        assert!(bindings.pending.is_some());
        assert!(bindings.entities.is_empty() && bindings.meshes.is_empty());
        let (result, _) = settle(&mut bindings, &request).unwrap();
        let projection = result.expect("valid fixture");
        assert!(!projection.meshes.is_empty());
        assert_eq!(
            projection.stats.vegetation_document_hash,
            bindings.vegetation.content_hash().unwrap().to_string()
        );
        assert!(bindings.entities.is_empty() && bindings.meshes.is_empty());
    }

    #[test]
    fn completed_jobs_reject_changed_tier_policy_or_sources_and_deliver_errors() {
        for changed in 0..6 {
            let (mut bindings, mut request) = fixture();
            bindings.pending = Some(Preparation {
                tier: request.tier,
                policy: request.hash.clone(),
                terrain: bindings.terrain.content_hash().unwrap().to_string(),
                vegetation: bindings.vegetation.content_hash().unwrap().to_string(),
                task: AsyncComputeTaskPool::get()
                    .spawn(async { (Err("worker failure".into()), 0.0) }),
            });
            match changed {
                1 => request.tier = VisualQualityTier::High,
                2 => request.hash.push('x'),
                3 => bindings.terrain.revision = bindings.terrain.revision.next().unwrap(),
                4 => bindings.vegetation.revision = bindings.vegetation.revision.next().unwrap(),
                5 => bindings.path_source_key = Some("changed-path-identity".into()),
                _ => {}
            }
            let result = settle(&mut bindings, &request);
            if changed == 0 {
                assert_eq!(result.unwrap().0.err().as_deref(), Some("worker failure"));
            } else {
                assert!(result.is_none());
            }
            assert!(bindings.pending.is_none());
        }
    }

    #[test]
    fn requests_do_not_spawn_over_pending_work_and_return_to_current_discards_it() {
        let (mut bindings, mut request) = fixture();
        let (release, wait) = std::sync::mpsc::channel();
        bindings.pending = Some(Preparation {
            tier: request.tier,
            policy: request.hash.clone(),
            terrain: bindings.terrain.content_hash().unwrap().to_string(),
            vegetation: bindings.vegetation.content_hash().unwrap().to_string(),
            task: AsyncComputeTaskPool::get().spawn(async move {
                wait.recv().unwrap();
                (Err("discarded work".into()), 0.0)
            }),
        });
        for tier in [
            VisualQualityTier::Medium,
            VisualQualityTier::Ultra,
            VisualQualityTier::High,
        ] {
            request.tier = tier;
            assert!(prepare(&mut bindings, &request).is_none());
            assert_eq!(
                bindings.pending.as_ref().unwrap().tier,
                VisualQualityTier::Low
            );
        }
        discard_completed(&mut bindings);
        assert!(bindings.pending.is_some());
        release.send(()).unwrap();
        let deadline = std::time::Instant::now() + std::time::Duration::from_secs(30);
        while bindings.pending.is_some() {
            discard_completed(&mut bindings);
            assert!(std::time::Instant::now() < deadline);
            std::thread::yield_now();
        }
        assert!(bindings.entities.is_empty() && bindings.meshes.is_empty());
    }
}
