//! One pending replacement set; live bindings remain untouched until GPU completion.
use super::{
    PreparedVegetationProjection, VegetationQualityBindings, VegetationQualityRequest,
    VisualQualityTier,
};
use crate::vegetation::{PreparedMeshAsset, VegetationMeshKey, VegetationProjectionStats};
use bevy::render::{
    ExtractSchedule, MainWorld, Render, RenderApp, RenderStartup, RenderSystems,
    mesh::{RenderMesh, allocator::MeshAllocator},
    render_asset::{RenderAsset, RenderAssets},
    renderer::RenderQueue,
};
use bevy::{camera::primitives::Aabb, prelude::*};
use std::{
    collections::{HashMap, VecDeque},
    sync::{
        Arc,
        atomic::{AtomicU8, Ordering},
    },
};

const BYTES_PER_FRAME: usize = 12 * 1024 * 1024;
const MESHES_PER_FRAME: usize = 16;

fn fits_upload_batch(bytes: usize, count: usize, next_bytes: usize) -> bool {
    count < MESHES_PER_FRAME && (count == 0 || bytes.saturating_add(next_bytes) <= BYTES_PER_FRAME)
}
const PENDING: u8 = 0;
const SUBMITTED: u8 = 1;
const COMPLETE: u8 = 2;
const INVALIDATED: u8 = 3;

struct Ticket {
    meshes: Vec<AssetId<Mesh>>,
    state: AtomicU8,
}

struct Stage {
    request: VegetationQualityRequest,
    prepared: Option<PreparedVegetationProjection>,
    remaining: VecDeque<PreparedMeshAsset>,
    uploaded: HashMap<VegetationMeshKey, (Handle<Mesh>, Aabb)>,
    ticket: Option<Arc<Ticket>>,
    #[cfg(any(target_arch = "wasm32", test))]
    stream: Option<Arc<crate::vegetation::PreparedMeshStream>>,
}

#[derive(Resource, Default)]
pub(in crate::vegetation) struct MeshStaging(Option<Stage>);

pub(super) fn is_pending(world: &World) -> bool {
    world
        .get_resource::<MeshStaging>()
        .is_some_and(|staging| staging.0.is_some())
}

#[derive(Resource, Default)]
struct UploadProbe(Option<Arc<Ticket>>);

pub(in crate::vegetation) fn install(app: &mut App) {
    // Native headless callers remain synchronous; real native and Web renderers share this path.
    if let Some(render) = app.get_sub_app_mut(RenderApp) {
        render
            .init_resource::<UploadProbe>()
            .add_systems(ExtractSchedule, extract)
            .add_systems(RenderStartup, reset_device)
            .add_systems(Render, after_submission.in_set(RenderSystems::Cleanup));
        app.init_resource::<MeshStaging>();
    }
}

pub(super) fn start(
    world: &mut World,
    mut prepared: PreparedVegetationProjection,
    request: VegetationQualityRequest,
) {
    let requested_tier = request.tier;
    if let Some(stage) = world.resource_mut::<MeshStaging>().0.as_mut()
        && stage.prepared.is_none()
        && stage.request.tier == request.tier
        && stage.request.hash == request.hash
    {
        stage.remaining.extend(std::mem::take(&mut prepared.meshes));
        stage.prepared = Some(prepared);
        return;
    }
    cancel(world);
    let remaining = std::mem::take(&mut prepared.meshes).into();
    world.resource_mut::<MeshStaging>().0 = Some(Stage {
        request,
        prepared: Some(prepared),
        remaining,
        uploaded: HashMap::new(),
        ticket: None,
        #[cfg(any(target_arch = "wasm32", test))]
        stream: None,
    });
    mark_waiting(world, requested_tier);
}

#[cfg(any(target_arch = "wasm32", test))]
pub(super) fn receive_chunks(
    world: &mut World,
    request: &VegetationQualityRequest,
    stream: Option<Arc<crate::vegetation::PreparedMeshStream>>,
) {
    if !world.contains_resource::<MeshStaging>() {
        return;
    }
    let Some(stream) = stream else {
        // A rejected or source-invalidated preparation must not leave partial uploads alive.
        cancel(world);
        return;
    };
    let matches = world
        .resource::<MeshStaging>()
        .0
        .as_ref()
        .is_some_and(|stage| {
            stage
                .stream
                .as_ref()
                .is_some_and(|old| Arc::ptr_eq(old, &stream))
        });
    if !matches {
        cancel(world);
        world.resource_mut::<MeshStaging>().0 = Some(Stage {
            request: request.clone(),
            prepared: None,
            remaining: VecDeque::new(),
            uploaded: HashMap::new(),
            ticket: None,
            stream: Some(stream.clone()),
        });
    }
    world
        .resource_mut::<MeshStaging>()
        .0
        .as_mut()
        .expect("stream stage installed")
        .remaining
        .extend(stream.drain());
}

fn mark_waiting(world: &mut World, tier: VisualQualityTier) {
    if let Some(quality) = &mut world.resource_mut::<VegetationProjectionStats>().quality {
        quality.requested_tier = tier;
        quality.status = "uploading_staged_previous_projection_retained";
    }
}

pub(super) fn cancel(world: &mut World) {
    let stage = world
        .get_resource_mut::<MeshStaging>()
        .and_then(|mut staging| staging.0.take());
    if let Some(stage) = stage {
        if let Some(ticket) = stage.ticket {
            ticket.state.store(INVALIDATED, Ordering::Release);
        }
        let mut assets = world.resource_mut::<Assets<Mesh>>();
        for (handle, _) in stage.uploaded.into_values() {
            assets.remove(handle.id());
        }
        // Dropped handles also release render-world allocations via AssetEvent::Unused.
    }
}

pub(super) fn discard_superseded(world: &mut World, request: &VegetationQualityRequest) {
    if world
        .get_resource::<MeshStaging>()
        .and_then(|s| s.0.as_ref())
        .is_some_and(|s| {
            s.request.tier != request.tier
                || s.request.hash != request.hash
                || s.ticket
                    .as_ref()
                    .is_some_and(|t| t.state.load(Ordering::Acquire) == INVALIDATED)
        })
    {
        cancel(world);
    }
}

pub(super) fn advance(
    world: &mut World,
    request: &VegetationQualityRequest,
    initial: VisualQualityTier,
) -> bool {
    let Some(mut stage) = world
        .get_resource_mut::<MeshStaging>()
        .and_then(|mut s| s.0.take())
    else {
        return false;
    };
    if stage
        .ticket
        .as_ref()
        .is_some_and(|t| t.state.load(Ordering::Acquire) == COMPLETE)
    {
        let prepared = stage
            .prepared
            .take()
            .expect("ticket requires completed preparation");
        let bindings = world.resource::<VegetationQualityBindings>();
        let current = bindings
            .terrain
            .content_hash()
            .is_ok_and(|h| h.to_string() == prepared.stats.terrain_document_hash)
            && bindings
                .vegetation_key()
                .is_ok_and(|h| h == prepared.stats.vegetation_document_hash);
        if !current {
            world.resource_mut::<MeshStaging>().0 = Some(stage);
            cancel(world);
            return true;
        }
        let mut retired = Vec::new();
        {
            let mut bindings = world.resource_mut::<VegetationQualityBindings>();
            for (key, value) in stage.uploaded {
                if let Some((old, _)) = bindings.meshes.insert(key, value) {
                    retired.push(old);
                }
            }
            bindings.record_attempt(request);
        }
        // Only presentation handles change. Reconcile retains logical keys/entities and sources.
        // The caller measures the whole staging/commit call: do not count reconcile twice.
        super::reconcile_inner(world, prepared, initial);
        let mut assets = world.resource_mut::<Assets<Mesh>>();
        for handle in retired {
            assets.remove(handle.id());
        }
        return true;
    }
    let mut bytes = 0;
    let mut count = 0;
    while let Some(next) = stage.remaining.front() {
        let next_bytes = RenderMesh::byte_len(&next.value.mesh).unwrap_or(0);
        if !fits_upload_batch(bytes, count, next_bytes) {
            break;
        }
        let value = stage
            .remaining
            .pop_front()
            .expect("nonempty pending meshes");
        bytes = bytes.saturating_add(next_bytes);
        let handle = world.resource_mut::<Assets<Mesh>>().add(value.value.mesh);
        stage.uploaded.insert(value.key, (handle, value.value.aabb));
        count += 1;
    }
    if stage.prepared.is_some() && stage.remaining.is_empty() && stage.ticket.is_none() {
        stage.ticket = Some(Arc::new(Ticket {
            meshes: stage
                .uploaded
                .values()
                .map(|(handle, _)| handle.id())
                .collect(),
            state: AtomicU8::new(PENDING),
        }));
    }
    let preparation_complete = stage.prepared.is_some();
    world.resource_mut::<MeshStaging>().0 = Some(stage);
    mark_waiting(world, request.tier);
    preparation_complete
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy owns system parameter wrappers"
)]
fn extract(main: Res<MainWorld>, mut probe: ResMut<UploadProbe>) {
    probe.0 = main
        .get_resource::<MeshStaging>()
        .and_then(|s| s.0.as_ref())
        .and_then(|s| s.ticket.clone());
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy owns system parameter wrappers"
)]
fn reset_device(probe: Res<UploadProbe>) {
    if let Some(ticket) = &probe.0 {
        ticket.state.store(INVALIDATED, Ordering::Release);
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy owns system parameter wrappers"
)]
fn after_submission(
    probe: Res<UploadProbe>,
    meshes: Res<RenderAssets<RenderMesh>>,
    allocator: Res<MeshAllocator>,
    queue: Res<RenderQueue>,
) {
    let Some(ticket) = &probe.0 else {
        return;
    };
    if ticket.meshes.iter().all(|id| {
        meshes.get(*id).is_some_and(|mesh| {
            allocator.mesh_vertex_slice(id).is_some()
                && (!mesh.indexed() || allocator.mesh_index_slice(id).is_some())
        })
    }) && ticket
        .state
        .compare_exchange(PENDING, SUBMITTED, Ordering::AcqRel, Ordering::Acquire)
        .is_ok()
    {
        let ticket = ticket.clone();
        queue.on_submitted_work_done(move || {
            // An obsolete callback cannot revive a cancelled/device-invalidated ticket.
            let _ = ticket.state.compare_exchange(
                SUBMITTED,
                COMPLETE,
                Ordering::AcqRel,
                Ordering::Acquire,
            );
        });
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::vegetation::*;

    #[test]
    fn streamed_preparation_uploads_early_but_only_publishes_complete_identical_geometry() {
        let (mut app, mut request) = fixture();
        request.tier = VisualQualityTier::High;
        let old = app
            .world()
            .resource::<VegetationQualityBindings>()
            .meshes
            .clone();
        let reference = {
            let bindings = app.world().resource::<VegetationQualityBindings>();
            prepare_vegetation_projection_with_quality(
                &bindings.terrain,
                &bindings.vegetation,
                Some(&request),
            )
            .unwrap()
        };
        let mut early_upload = false;
        let mut completed_ticket = None;
        for _ in 0..1000 {
            let result = super::super::incremental::prepare(
                &mut app.world_mut().resource_mut::<VegetationQualityBindings>(),
                &request,
                true,
            );
            if let Some((result, _)) = result {
                start(app.world_mut(), result.unwrap(), request.clone());
            } else {
                let stream = super::super::incremental::mesh_stream(
                    app.world().resource::<VegetationQualityBindings>(),
                );
                receive_chunks(app.world_mut(), &request, stream);
            }
            advance(app.world_mut(), &request, VisualQualityTier::Low);
            let stage = app.world().resource::<MeshStaging>().0.as_ref().unwrap();
            early_upload |= stage.prepared.is_none() && !stage.uploaded.is_empty();
            assert_eq!(
                app.world().resource::<VegetationQualityBindings>().meshes,
                old
            );
            if let Some(ticket) = &stage.ticket {
                assert_eq!(
                    serde_json::to_value(&stage.prepared.as_ref().unwrap().stats).unwrap(),
                    serde_json::to_value(&reference.stats).unwrap()
                );
                completed_ticket = Some(ticket.clone());
                break;
            }
            // Once preparation finishes, upload the remaining batch without starting another job.
            if stage.prepared.is_some() {
                completed_ticket = Some(upload_all(&mut app, &request));
                break;
            }
        }
        assert!(early_upload);
        let ticket = completed_ticket.expect("finite streamed preparation");
        ticket.state.store(COMPLETE, Ordering::Release);
        advance(app.world_mut(), &request, VisualQualityTier::Low);
        let bindings = app.world().resource::<VegetationQualityBindings>();
        for expected in reference.meshes {
            let actual = app
                .world()
                .resource::<Assets<Mesh>>()
                .get(bindings.meshes[&expected.key].0.id())
                .unwrap();
            for (attribute, values) in expected.value.mesh.attributes() {
                assert_eq!(Some(values), actual.attribute(attribute.id));
            }
            assert_eq!(
                expected
                    .value
                    .mesh
                    .indices()
                    .map(|v| v.iter().collect::<Vec<_>>()),
                actual.indices().map(|v| v.iter().collect::<Vec<_>>())
            );
        }
    }

    #[test]
    fn abandoned_stream_releases_partial_uploads_and_same_policy_restart_has_new_identity() {
        let (mut app, request) = fixture();
        let count = app.world().resource::<Assets<Mesh>>().len();
        let stream = Arc::new(PreparedMeshStream::default());
        begin(&mut app, &request);
        let mut meshes = app
            .world_mut()
            .resource_mut::<MeshStaging>()
            .0
            .take()
            .unwrap()
            .remaining
            .into_iter()
            .collect();
        stream.append(&mut meshes);
        receive_chunks(app.world_mut(), &request, Some(stream));
        assert!(!advance(app.world_mut(), &request, VisualQualityTier::Low));
        assert!(app.world().resource::<Assets<Mesh>>().len() > count);
        receive_chunks(
            app.world_mut(),
            &request,
            Some(Arc::new(PreparedMeshStream::default())),
        );
        assert_eq!(app.world().resource::<Assets<Mesh>>().len(), count);
        receive_chunks(app.world_mut(), &request, None);
        assert!(app.world().resource::<MeshStaging>().0.is_none());
    }

    #[test]
    fn upload_batch_packs_small_meshes_without_crossing_the_byte_budget() {
        assert!(fits_upload_batch(0, 0, BYTES_PER_FRAME + 1));
        assert!(!fits_upload_batch(BYTES_PER_FRAME + 1, 1, 1));
        assert!(fits_upload_batch(BYTES_PER_FRAME - 1, 4, 1));
        assert!(!fits_upload_batch(BYTES_PER_FRAME - 1, 4, 2));
        assert!(fits_upload_batch(0, MESHES_PER_FRAME - 1, 0));
        assert!(!fits_upload_batch(0, MESHES_PER_FRAME, 0));
        assert!(!fits_upload_batch(usize::MAX, 1, 1));
    }

    fn fixture() -> (App, VegetationQualityRequest) {
        let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
        let vegetation = forge_world::embedded_visual_valley_vegetation_document().unwrap();
        let policy = forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        let request =
            VegetationQualityRequest::new(&terrain, policy, VisualQualityTier::Low).unwrap();
        let prepared =
            prepare_vegetation_projection_with_quality(&terrain, &vegetation, Some(&request))
                .unwrap();
        let mut app = App::new();
        app.init_resource::<Assets<Mesh>>()
            .init_resource::<Assets<VisualValleyVegetationMaterial>>()
            .init_resource::<MeshStaging>()
            .insert_resource(VegetationPresentation(prepared.profile))
            .insert_resource(VegetationInteractionField::disabled())
            .insert_resource(prepared.stats.clone())
            .insert_resource(PreparedVegetation(Some(prepared)))
            .insert_resource(VegetationQualityBindings::new(terrain, vegetation))
            .add_systems(Startup, spawn_vegetation_projection);
        app.update();
        (app, request)
    }

    fn begin(app: &mut App, request: &VegetationQualityRequest) {
        let bindings = app.world().resource::<VegetationQualityBindings>();
        let prepared = crate::vegetation::prepare_vegetation_projection_with_paths(
            &bindings.terrain,
            &bindings.vegetation,
            &bindings.paths,
            Some(request),
        )
        .unwrap();
        start(app.world_mut(), prepared, request.clone());
    }

    #[test]
    fn path_projection_identity_is_accepted_at_upload_commit() {
        let (mut app, request) = fixture();
        let key = {
            let mut bindings = app.world_mut().resource_mut::<VegetationQualityBindings>();
            bindings.paths = vec![forge_project::zone_authoring::paths::AuthoredPath {
                id: forge_core::EntityId::deterministic("staged-path"),
                name: "Path".into(),
                width_m: 8.0,
                shoulder_m: 1.0,
                profile: None,
                locked: false,
                points: vec![
                    forge_project::zone_authoring::paths::PathPoint {
                        x_m: -100.0,
                        z_m: 0.0,
                    },
                    forge_project::zone_authoring::paths::PathPoint {
                        x_m: 100.0,
                        z_m: 0.0,
                    },
                ],
            }];
            let key = forge_world::path_vegetation::prepare(&bindings.vegetation, &bindings.paths)
                .unwrap()
                .source_hash()
                .to_string();
            bindings.path_source_key = Some(key.clone());
            key
        };
        begin(&mut app, &request);
        let ticket = upload_all(&mut app, &request);
        ticket.state.store(COMPLETE, Ordering::Release);
        assert!(advance(app.world_mut(), &request, VisualQualityTier::Low));
        assert_eq!(
            app.world()
                .resource::<VegetationProjectionStats>()
                .vegetation_document_hash,
            key
        );
        assert!(app.world().resource::<MeshStaging>().0.is_none());
    }

    fn upload_all(app: &mut App, request: &VegetationQualityRequest) -> Arc<Ticket> {
        for _ in 0..1000 {
            assert!(advance(app.world_mut(), request, VisualQualityTier::Low));
            let stage = app.world().resource::<MeshStaging>().0.as_ref().unwrap();
            if let Some(ticket) = &stage.ticket {
                return ticket.clone();
            }
        }
        panic!("staging did not finish its bounded upload steps");
    }

    #[test]
    fn staged_meshes_keep_old_projection_until_completion_and_retire_old_assets() {
        let (mut app, mut request) = fixture();
        let old = app
            .world()
            .resource::<VegetationQualityBindings>()
            .meshes
            .clone();
        let entities = app
            .world()
            .resource::<VegetationQualityBindings>()
            .entities
            .clone();
        request.tier = VisualQualityTier::High;
        begin(&mut app, &request);
        assert!(advance(app.world_mut(), &request, VisualQualityTier::Low));
        assert!(
            app.world()
                .resource::<MeshStaging>()
                .0
                .as_ref()
                .unwrap()
                .uploaded
                .len()
                <= MESHES_PER_FRAME
        );
        assert_eq!(
            app.world().resource::<VegetationQualityBindings>().meshes,
            old
        );
        let ticket = upload_all(&mut app, &request);
        // CPU assets and elapsed frames are not a renderer-completion receipt.
        for _ in 0..3 {
            assert!(advance(app.world_mut(), &request, VisualQualityTier::Low));
        }
        assert_eq!(
            app.world().resource::<VegetationQualityBindings>().meshes,
            old
        );
        assert_eq!(
            app.world()
                .resource::<VegetationProjectionStats>()
                .quality
                .as_ref()
                .unwrap()
                .projected_tier,
            VisualQualityTier::Low
        );
        assert!(
            old.values()
                .all(|(h, _)| app.world().resource::<Assets<Mesh>>().contains(h.id()))
        );
        // Synthetic completion only: no real renderer is installed in this test.
        ticket.state.store(COMPLETE, Ordering::Release);
        assert!(advance(app.world_mut(), &request, VisualQualityTier::Low));
        assert!(app.world().resource::<MeshStaging>().0.is_none());
        let bindings = app.world().resource::<VegetationQualityBindings>();
        for (key, entity) in entities {
            assert_eq!(bindings.entities.get(&key), Some(&entity));
        }
        assert_eq!(
            app.world()
                .resource::<VegetationProjectionStats>()
                .quality
                .as_ref()
                .unwrap()
                .projected_tier,
            VisualQualityTier::High
        );
        for (key, (handle, _)) in old {
            assert_ne!(bindings.meshes[&key].0, handle);
            assert!(!app.world().resource::<Assets<Mesh>>().contains(handle.id()));
        }
    }

    #[test]
    fn superseded_and_source_changed_stages_release_unpublished_assets() {
        let (mut app, mut request) = fixture();
        let original_count = app.world().resource::<Assets<Mesh>>().len();
        request.tier = VisualQualityTier::High;
        begin(&mut app, &request);
        let ticket = upload_all(&mut app, &request);
        request.tier = VisualQualityTier::Medium;
        discard_superseded(app.world_mut(), &request);
        assert!(app.world().resource::<MeshStaging>().0.is_none());
        assert_eq!(app.world().resource::<Assets<Mesh>>().len(), original_count);
        assert_eq!(ticket.state.load(Ordering::Acquire), INVALIDATED);
        assert!(
            ticket
                .state
                .compare_exchange(SUBMITTED, COMPLETE, Ordering::AcqRel, Ordering::Acquire)
                .is_err()
        );
        begin(&mut app, &request);
        let ticket = upload_all(&mut app, &request);
        app.world_mut()
            .resource_mut::<VegetationQualityBindings>()
            .vegetation
            .seed += 1;
        ticket.state.store(COMPLETE, Ordering::Release);
        assert!(advance(app.world_mut(), &request, VisualQualityTier::Low));
        assert!(app.world().resource::<MeshStaging>().0.is_none());
        assert_eq!(app.world().resource::<Assets<Mesh>>().len(), original_count);
        assert_eq!(
            app.world()
                .resource::<VegetationProjectionStats>()
                .quality
                .as_ref()
                .unwrap()
                .projected_tier,
            VisualQualityTier::Low
        );
    }

    #[test]
    fn policy_replacement_and_device_invalidation_do_not_reuse_completion() {
        let (mut app, mut request) = fixture();
        let original_count = app.world().resource::<Assets<Mesh>>().len();
        request.tier = VisualQualityTier::High;
        begin(&mut app, &request);
        let old = upload_all(&mut app, &request);
        request.hash = "synthetic changed policy identity".into();
        discard_superseded(app.world_mut(), &request);
        assert_eq!(old.state.load(Ordering::Acquire), INVALIDATED);
        assert_eq!(app.world().resource::<Assets<Mesh>>().len(), original_count);
        // New request, new ticket. A cancelled ticket completing cannot acknowledge it.
        begin(&mut app, &request);
        let new = upload_all(&mut app, &request);
        assert!(!Arc::ptr_eq(&old, &new));
        assert_eq!(new.state.load(Ordering::Acquire), PENDING);
        app.world_mut()
            .insert_resource(UploadProbe(Some(new.clone())));
        app.add_systems(Update, reset_device);
        app.update();
        discard_superseded(app.world_mut(), &request);
        assert_eq!(new.state.load(Ordering::Acquire), INVALIDATED);
        assert!(app.world().resource::<MeshStaging>().0.is_none());
        assert_eq!(app.world().resource::<Assets<Mesh>>().len(), original_count);
    }
}
