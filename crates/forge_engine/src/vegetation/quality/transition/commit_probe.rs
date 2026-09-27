//! Explicit diagnostic: separates CPU preparation from the live ECS/asset commit.
use super::*;
use crate::vegetation::*;

// Diagnostic payload comparison only; not a production mesh equivalence policy.
fn same_vertex_payload(left: &Mesh, right: &Mesh) -> bool {
    left.primitive_topology() == right.primitive_topology()
        && left.attributes().count() == right.attributes().count()
        && left
            .attributes()
            .all(|(attribute, values)| right.attribute(attribute.id) == Some(values))
        && match (left.indices(), right.indices()) {
            (None, None) => true,
            (Some(left), Some(right)) => left.iter().eq(right.iter()),
            _ => false,
        }
}

#[test]
#[ignore = "explicit CPU commit probe; not browser, GPU or frame-time qualification"]
fn quality_commit_cpu_probe() {
    let source = forge_world::compile_world_sketch(
        &forge_world::embedded_visual_valley_world_sketch().unwrap(),
    )
    .unwrap();
    let mut policy = forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap();
    policy.revision = source.carved_terrain.revision;
    let mut request =
        VegetationQualityRequest::new(&source.carved_terrain, policy, VisualQualityTier::High)
            .unwrap();
    let prepared = prepare_vegetation_projection_with_quality(
        &source.carved_terrain,
        &source.vegetation,
        Some(&request),
    )
    .unwrap();
    let mut app = App::new();
    app.init_resource::<Assets<Mesh>>()
        .init_resource::<Time>()
        .init_resource::<Assets<VisualValleyVegetationMaterial>>()
        .insert_resource(VegetationPresentation(prepared.profile))
        .insert_resource(VegetationInteractionField::disabled())
        .insert_resource(prepared.stats.clone())
        .insert_resource(PreparedVegetation(Some(prepared)))
        .insert_resource(VegetationQualityBindings::new(
            source.carved_terrain.clone(),
            source.vegetation.clone(),
        ))
        .insert_resource(request.clone())
        .add_systems(Startup, spawn_vegetation_projection);
    app.update();
    for tier in [
        VisualQualityTier::Low,
        VisualQualityTier::Medium,
        VisualQualityTier::Ultra,
        VisualQualityTier::High,
    ] {
        request.request(tier);
        let started = std::time::Instant::now();
        let prepared = prepare_vegetation_projection_with_quality(
            &source.carved_terrain,
            &source.vegetation,
            Some(&request),
        )
        .unwrap();
        let preparation_ms = started.elapsed().as_secs_f64() * 1000.0;
        let mesh_count = prepared.meshes.len();
        let entity_count = prepared.entities.len();
        let bindings = app.world().resource::<VegetationQualityBindings>();
        let meshes = app.world().resource::<Assets<Mesh>>();
        let mut identical_payloads = 0;
        let mut identical_vertices = 0;
        let total_vertices: usize = prepared
            .meshes
            .iter()
            .map(|m| m.value.mesh.count_vertices())
            .sum();
        for candidate in &prepared.meshes {
            if let Some((handle, _)) = bindings.meshes.get(&candidate.key)
                && let Some(previous) = meshes.get(handle.id())
                && same_vertex_payload(previous, &candidate.value.mesh)
            {
                identical_payloads += 1;
                identical_vertices += candidate.value.mesh.count_vertices();
            }
        }
        let started = std::time::Instant::now();
        reconcile(app.world_mut(), prepared, VisualQualityTier::High);
        let commit_ms = started.elapsed().as_secs_f64() * 1000.0;
        let stats = app.world().resource::<VegetationProjectionStats>();
        assert_eq!(stats.quality.as_ref().unwrap().projected_tier, tier);
        assert_eq!(
            stats.terrain_document_hash,
            source.carved_terrain.content_hash().unwrap().to_string()
        );
        assert_eq!(
            stats.vegetation_document_hash,
            source.vegetation.content_hash().unwrap().to_string()
        );
        println!(
            "quality_commit_probe tier={tier:?} preparation_ms={preparation_ms:.3} commit_ms={commit_ms:.3} meshes={mesh_count} entities={entity_count} identical_payloads={identical_payloads} identical_vertices={identical_vertices} total_vertices={total_vertices} scope=native_headless_cpu_no_gpu"
        );
    }
}
