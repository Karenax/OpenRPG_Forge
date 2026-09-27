//! Budgeted browser preparation chunks, with no live projection mutation.
use super::{
    PreparedVegetationProjection, VegetationQualityBindings, VegetationQualityRequest,
    VisualQualityTier,
};
use std::{
    future::Future,
    pin::Pin,
    task::{Context, Poll, Waker},
};

type Output = Result<PreparedVegetationProjection, String>;
pub(super) struct SourcePlan {
    terrain: String,
    vegetation: String,
    plan: std::sync::Arc<forge_world::VegetationGenerationPlan>,
}

fn source_plan(
    bindings: &mut VegetationQualityBindings,
    terrain: &str,
    vegetation: &str,
) -> Result<std::sync::Arc<forge_world::VegetationGenerationPlan>, String> {
    if let Some(cached) = &bindings.source_plan
        && cached.terrain == terrain
        && cached.vegetation == vegetation
    {
        return Ok(cached.plan.clone());
    }
    // At most one source plan retained. It owns immutable documents and its
    // existing canonical generation cache; quality never edits those sources.
    bindings.source_plan = None;
    let plan = std::sync::Arc::new(
        forge_world::VegetationGenerationPlan::from_documents_with_buildings(
            &bindings.terrain,
            &bindings.vegetation,
            &bindings.paths,
            &bindings.rivers,
            &bindings.buildings,
        )
        .and_then(|plan| plan.with_promotions(&bindings.promotions))
        .map_err(|e| e.to_string())?,
    );
    bindings.source_plan = Some(SourcePlan {
        terrain: terrain.to_owned(),
        vegetation: vegetation.to_owned(),
        plan: plan.clone(),
    });
    Ok(plan)
}

pub(super) struct Preparation {
    tier: VisualQualityTier,
    policy: String,
    terrain: String,
    vegetation: String,
    cpu_ms: f64,
    future: Pin<Box<dyn Future<Output = Output> + Send + Sync>>,
    mesh_stream: Option<std::sync::Arc<crate::vegetation::PreparedMeshStream>>,
}

pub(super) fn mesh_stream(
    bindings: &VegetationQualityBindings,
) -> Option<std::sync::Arc<crate::vegetation::PreparedMeshStream>> {
    bindings
        .incremental
        .as_ref()
        .and_then(|job| job.mesh_stream.clone())
}

pub(super) fn prepare(
    bindings: &mut VegetationQualityBindings,
    request: &VegetationQualityRequest,
    stream_meshes: bool,
) -> Option<(Output, f64)> {
    let source_key = bindings.vegetation_key();
    if let Some(job) = &mut bindings.incremental {
        if job.tier != request.tier || job.policy != request.hash {
            bindings.incremental = None;
            return None;
        }
        let started = bevy::platform::time::Instant::now();
        let mut budget = crate::preparation_budget::ChunkBudget::new();
        let result = loop {
            let result = job
                .future
                .as_mut()
                .poll(&mut Context::from_waker(Waker::noop()));
            if result.is_ready() || budget.exhausted() {
                break result;
            }
        };
        job.cpu_ms += started.elapsed().as_secs_f64() * 1000.0;
        let Poll::Ready(mut result) = result else {
            return None;
        };
        let current = bindings
            .terrain
            .content_hash()
            .is_ok_and(|h| h.to_string() == job.terrain)
            && source_key.as_ref().is_ok_and(|h| *h == job.vegetation);
        let cpu_ms = job.cpu_ms;
        if let (Ok(prepared), Some(stream)) = (&mut result, &job.mesh_stream) {
            prepared.meshes.extend(stream.drain());
        }
        bindings.incremental = None;
        return current.then_some((result, cpu_ms));
    }
    let hashes = bindings
        .terrain
        .content_hash()
        .map_err(|e| e.to_string())
        .and_then(|terrain| source_key.map(|vegetation| (terrain.to_string(), vegetation)));
    let (terrain_hash, vegetation_hash) = match hashes {
        Ok(hashes) => hashes,
        Err(error) => return Some((Err(error), 0.0)),
    };
    let plan = match source_plan(bindings, &terrain_hash, &vegetation_hash) {
        Ok(plan) => plan,
        Err(error) => return Some((Err(error), 0.0)),
    };
    let requested = request.clone();
    let stream = stream_meshes
        .then(|| std::sync::Arc::new(crate::vegetation::PreparedMeshStream::default()));
    let producer = stream.clone();
    bindings.incremental = Some(Preparation {
        tier: request.tier,
        policy: request.hash.clone(),
        terrain: terrain_hash,
        vegetation: vegetation_hash,
        cpu_ms: 0.0,
        mesh_stream: stream,
        future: Box::pin(async move {
            crate::vegetation::prepare_vegetation_projection_from_plan(
                &plan,
                Some(&requested),
                true,
                producer.as_deref(),
            )
            .await
            .map_err(|e| e.to_string())
        }),
    });
    None
}

#[cfg(test)]
mod tests {
    use super::*;

    fn cached(
        bindings: &mut VegetationQualityBindings,
    ) -> std::sync::Arc<forge_world::VegetationGenerationPlan> {
        let terrain = bindings.terrain.content_hash().unwrap().to_string();
        let vegetation = bindings.vegetation_key().unwrap();
        source_plan(bindings, &terrain, &vegetation).unwrap()
    }

    #[test]
    fn immutable_plan_reuses_canonical_generation_and_invalidates_both_sources() {
        let mut bindings = VegetationQualityBindings::new(
            forge_world::embedded_visual_valley_terrain_document().unwrap(),
            forge_world::embedded_visual_valley_vegetation_document().unwrap(),
        );
        let first = cached(&mut bindings);
        let canonical = first.generate().unwrap();
        let reused = cached(&mut bindings);
        assert!(std::sync::Arc::ptr_eq(&first, &reused));
        assert!(std::sync::Arc::ptr_eq(
            &canonical,
            &reused.generate().unwrap()
        ));
        bindings.vegetation.seed = bindings.vegetation.seed.wrapping_add(1);
        let edited = cached(&mut bindings);
        assert!(!std::sync::Arc::ptr_eq(&first, &edited));
        assert_ne!(
            canonical.vegetation_document_hash,
            edited.generate().unwrap().vegetation_document_hash
        );
        bindings.terrain.seed = bindings.terrain.seed.wrapping_add(1);
        let terrain_edited = cached(&mut bindings);
        assert!(!std::sync::Arc::ptr_eq(&edited, &terrain_edited));
        assert_ne!(
            edited.generate().unwrap().terrain_document_hash,
            terrain_edited.generate().unwrap().terrain_document_hash
        );
        bindings.vegetation.layers.clear();
        assert!(source_plan(&mut bindings, "edited-invalid", "edited-invalid").is_err());
        assert!(bindings.source_plan.is_none());
    }
    #[test]
    fn staged_output_matches_sync_and_rejects_superseded_work() {
        let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
        let vegetation = forge_world::embedded_visual_valley_vegetation_document().unwrap();
        let policy = forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        let mut request =
            VegetationQualityRequest::new(&terrain, policy, VisualQualityTier::Low).unwrap();
        let reference = crate::vegetation::prepare_vegetation_projection_with_quality(
            &terrain,
            &vegetation,
            Some(&request),
        )
        .unwrap();
        let mut bindings = VegetationQualityBindings::new(terrain, vegetation);
        let mut polls = 0;
        let output = loop {
            polls += 1;
            assert!(polls < 1000);
            if let Some((result, _)) = prepare(&mut bindings, &request, false) {
                break result.unwrap();
            }
            assert!(bindings.entities.is_empty() && bindings.meshes.is_empty());
        };
        assert!(polls > 2);
        assert_eq!(
            serde_json::to_value(&reference.stats).unwrap(),
            serde_json::to_value(&output.stats).unwrap()
        );
        assert_eq!(reference.entities.len(), output.entities.len());
        assert_eq!(reference.meshes.len(), output.meshes.len());
        for (left, right) in reference.meshes.iter().zip(&output.meshes) {
            assert_eq!(left.key, right.key);
            assert_eq!(
                left.value.mesh.attributes().count(),
                right.value.mesh.attributes().count()
            );
            for (attribute, values) in left.value.mesh.attributes() {
                assert_eq!(Some(values), right.value.mesh.attribute(attribute.id));
            }
            assert_eq!(
                left.value
                    .mesh
                    .indices()
                    .map(|v| v.iter().collect::<Vec<_>>()),
                right
                    .value
                    .mesh
                    .indices()
                    .map(|v| v.iter().collect::<Vec<_>>())
            );
        }
        assert!(prepare(&mut bindings, &request, false).is_none());
        request.tier = VisualQualityTier::High;
        assert!(prepare(&mut bindings, &request, false).is_none());
        assert!(bindings.incremental.is_none());
        assert!(bindings.entities.is_empty() && bindings.meshes.is_empty());
        assert!(prepare(&mut bindings, &request, false).is_none());
        let old_plan = bindings.source_plan.as_ref().unwrap().plan.clone();
        bindings.vegetation.seed = bindings.vegetation.seed.wrapping_add(1);
        for poll in 0..1000 {
            assert!(
                prepare(&mut bindings, &request, false).is_none(),
                "stale source applied"
            );
            if bindings.incremental.is_none() {
                break;
            }
            assert!(poll < 999, "stale source job did not finish");
        }
        assert!(prepare(&mut bindings, &request, false).is_none());
        assert!(!std::sync::Arc::ptr_eq(
            &old_plan,
            &bindings.source_plan.as_ref().unwrap().plan
        ));
    }
}
