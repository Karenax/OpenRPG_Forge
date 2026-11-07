//! Unit-only simulated receipts exercise revocation, never claim real renderer qualification.
use super::*;
use crate::render_inventory::tests::Fixture;
use bevy::{camera::Viewport, render::render_resource::TextureUsages};

fn observed() -> (Fixture, RenderInventoryToken) {
    let mut f = Fixture::new();
    f.request(1).unwrap();
    f.app.world_mut().insert_resource(FrameCount(30));
    let mut target = Image::default();
    target.texture_descriptor.usage |= TextureUsages::RENDER_ATTACHMENT;
    let target = f
        .app
        .world_mut()
        .resource_mut::<Assets<Image>>()
        .add(target);
    f.app.world_mut().spawn((
        Camera3d::default(),
        Camera {
            viewport: Some(Viewport {
                physical_size: UVec2::new(128, 128),
                ..default()
            }),
            ..default()
        },
        RenderTarget::Image(target.into()),
    ));
    let (key, request) = current(f.app.world()).unwrap();
    let required = request.required.clone();
    assert!(acquire(f.app.world(), f.owner, f.identity, 1).is_none());
    f.app.world_mut().insert_resource(RenderInventoryEvidence {
        key: Some(key),
        required,
        owner: Some(f.owner),
        identity: Some(f.identity),
        generation: Some(1),
        epoch: 1,
        observed: 30,
        completed: Some(28),
        ready: true,
        pipelines_total: 1,
        stable_frames: 8,
        assets: GpuAssetEvidence {
            inventory_ready: true,
            ..default()
        },
        view: GpuViewEvidence {
            camera_resources_ready: true,
            ..default()
        },
        ..default()
    });
    let token = acquire(f.app.world(), f.owner, f.identity, 1).unwrap();
    (f, token)
}

// Unit-only synthetic observations; these exercise rejection and never claim
// that a real GPU or view specialization has run.
fn observed_context(f: &mut Fixture, token: &RenderInventoryToken) -> GraphicsContextReceipt {
    let view = graphics_view(f.app.world()).unwrap();
    let prepared = PreparedGraphicsContext {
        epoch: token.epoch,
        forward: MeshPipelineKey::from_msaa_samples(view.msaa) | view.prepass,
        prepass: (!view.prepass.is_empty()).then_some(view.prepass),
        view,
    };
    f.app
        .world_mut()
        .resource_mut::<RenderInventoryEvidence>()
        .context = Some(prepared.clone());
    f.app
        .world_mut()
        .insert_resource(GraphicsContextState(Arc::new(Mutex::new(
            ContextObservation {
                prepared: Some(prepared),
                observed: 30,
                ready: true,
            },
        ))));
    token.graphics_context(f.app.world()).unwrap()
}

#[test]
fn context_requires_a_completed_inventory_and_survives_selection_requests_only() {
    let (mut f, token) = observed();
    assert!(token.graphics_context(f.app.world()).is_none());
    let receipt = observed_context(&mut f, &token);
    assert!(receipt.is_current(f.app.world()));
    f.request(2).unwrap();
    assert!(!token.is_current(f.app.world(), f.owner, f.identity, 1));
    assert!(token.graphics_context(f.app.world()).is_none());
    // This context does not certify the new request or any new publication.
    assert!(receipt.is_current(f.app.world()));
    f.app.world_mut().insert_resource(FrameCount(33));
    assert!(!receipt.is_current(f.app.world()));
}

#[test]
fn context_rejects_device_view_msaa_prepass_and_unfinished_specializations() {
    let (mut f, token) = observed();
    let receipt = observed_context(&mut f, &token);
    let camera = token.key.camera;
    let original_msaa = f
        .app
        .world()
        .get::<Msaa>(camera)
        .copied()
        .unwrap_or(Msaa::Off);
    let changed_msaa = if original_msaa == Msaa::Off {
        Msaa::Sample4
    } else {
        Msaa::Off
    };
    f.app.world_mut().entity_mut(camera).insert(changed_msaa);
    assert!(!receipt.is_current(f.app.world()));
    f.app.world_mut().entity_mut(camera).insert(original_msaa);
    f.app
        .world_mut()
        .entity_mut(camera)
        .insert(bevy::core_pipeline::prepass::DepthPrepass);
    assert!(!receipt.is_current(f.app.world()));
    f.app
        .world_mut()
        .entity_mut(camera)
        .remove::<bevy::core_pipeline::prepass::DepthPrepass>();
    assert!(receipt.is_current(f.app.world()));
    f.app
        .world_mut()
        .get_mut::<Camera>(camera)
        .unwrap()
        .viewport
        .as_mut()
        .unwrap()
        .physical_size
        .x += 1;
    assert!(!receipt.is_current(f.app.world()));
    f.app
        .world_mut()
        .get_mut::<Camera>(camera)
        .unwrap()
        .viewport
        .as_mut()
        .unwrap()
        .physical_size
        .x -= 1;
    assert!(receipt.is_current(f.app.world()));
    let state = f.app.world().resource::<GraphicsContextState>().clone();
    state.0.lock().unwrap().ready = false;
    assert!(!receipt.is_current(f.app.world()));
    state.0.lock().unwrap().ready = true;
    state.0.lock().unwrap().prepared.as_mut().unwrap().epoch += 1;
    assert!(!receipt.is_current(f.app.world()));
    *state.0.lock().unwrap() = ContextObservation::default();
    assert!(!receipt.is_current(f.app.world()));
}

#[test]
fn auxiliary_id_camera_preserves_inventory_but_two_scene_cameras_revoke_it() {
    let (mut f, token) = observed();
    let target = RenderTarget::Image(f.image.clone().into());
    let auxiliary = f
        .app
        .world_mut()
        .spawn((
            Camera3d::default(),
            Camera::default(),
            target.clone(),
            crate::scene_camera::OffscreenSceneCamera,
        ))
        .id();
    assert!(token.is_current(f.app.world(), f.owner, f.identity, 1));
    assert!(token.covers_frame(f.app.world(), 28));
    let second_scene = f
        .app
        .world_mut()
        .spawn((Camera3d::default(), Camera::default(), target))
        .id();
    assert!(!token.is_current(f.app.world(), f.owner, f.identity, 1));
    assert!(!token.covers_frame(f.app.world(), 28));
    f.app.world_mut().despawn(second_scene);
    assert!(token.is_current(f.app.world(), f.owner, f.identity, 1));
    f.app.world_mut().despawn(auxiliary);
    assert!(token.is_current(f.app.world(), f.owner, f.identity, 1));
}

#[test]
fn scoped_receipts_revoke_independently_and_cannot_reuse_primary_or_released_tokens() {
    let (mut f, primary) = observed();
    let owner = EntityId::deterministic("quality-terrain-owner");
    let meshes = [f.mesh.clone()];
    let materials = [f.material.clone()];
    crate::render_inventory::request_scoped(
        f.app.world_mut(),
        owner,
        f.identity,
        1,
        &meshes,
        &materials,
    )
    .unwrap();
    assert!(acquire_scoped(f.app.world(), owner, f.identity, 1).is_none());
    assert!(primary.is_current(f.app.world(), f.owner, f.identity, 1));
    let (key, request) = current_at(f.app.world(), Slot::Scoped, owner).unwrap();
    let mut evidence = f.app.world().resource::<RenderInventoryEvidence>().clone();
    evidence.key = Some(key);
    evidence.owner = Some(owner);
    evidence.completed = Some(27);
    evidence.required = request.required.clone();
    f.app
        .world_mut()
        .insert_resource(ScopedEvidence(BTreeMap::from([(owner, evidence)])));
    let scoped = acquire_scoped(f.app.world(), owner, f.identity, 1).unwrap();
    assert_eq!(scoped.completed_frame(f.app.world()), Some(27));
    assert_eq!(primary.completed_frame(f.app.world()), Some(28));
    assert!(scoped.covers_frame(f.app.world(), 27));
    assert!(!scoped.covers_frame(f.app.world(), 28));
    assert!(acquire(f.app.world(), owner, f.identity, 1).is_none());
    f.app
        .world_mut()
        .resource_mut::<ScopedEvidence>()
        .0
        .get_mut(&owner)
        .unwrap()
        .epoch = 2;
    assert!(!scoped.is_current(f.app.world(), owner, f.identity, 1));
    assert_eq!(scoped.completed_frame(f.app.world()), None);
    assert!(primary.is_current(f.app.world(), f.owner, f.identity, 1));
    crate::render_inventory::release_scoped(f.app.world_mut(), owner);
    crate::render_inventory::request_scoped(
        f.app.world_mut(),
        owner,
        f.identity,
        1,
        &meshes,
        &materials,
    )
    .unwrap();
    assert!(!scoped.is_current(f.app.world(), owner, f.identity, 1));
    assert!(acquire_scoped(f.app.world(), owner, f.identity, 1).is_none());
}

#[test]
fn fence_tokens_are_view_generation_epoch_and_source_scoped() {
    let (mut f, token) = observed();
    assert!(token.is_current(f.app.world(), f.owner, f.identity, 1));
    assert!(token.covers_frame(f.app.world(), 28));
    assert!(!token.covers_frame(f.app.world(), 29));
    f.app
        .world_mut()
        .resource_mut::<RenderInventoryEvidence>()
        .epoch = 2;
    assert!(!token.is_current(f.app.world(), f.owner, f.identity, 1));
    f.app
        .world_mut()
        .resource_mut::<RenderInventoryEvidence>()
        .epoch = 1;
    f.app
        .world_mut()
        .resource_mut::<Assets<Image>>()
        .remove(f.image.id());
    assert!(!token.is_current(f.app.world(), f.owner, f.identity, 1));
}

#[test]
fn token_covers_only_its_exact_live_typed_sources() {
    let (mut f, token) = observed();
    let meshes = [f.mesh.clone()];
    let materials = [f.material.clone()];
    assert!(token.covers_sources(f.app.world(), &meshes, &materials));
    let other = f
        .app
        .world_mut()
        .resource_mut::<Assets<Mesh>>()
        .add(Cuboid::default());
    assert!(!token.covers_sources(f.app.world(), &[other], &materials));
    assert!(!token.covers_sources(f.app.world(), &[], &materials));
    let another = f
        .app
        .world_mut()
        .resource_mut::<Assets<crate::render_inventory::tests::SourceMaterial>>()
        .add(crate::render_inventory::tests::SourceMaterial {
            image: f.image.clone(),
            buffer: f.buffer.clone(),
        });
    assert!(!token.covers_sources(f.app.world(), &meshes, &[another]));
    f.app
        .world_mut()
        .resource_mut::<Assets<Image>>()
        .remove(f.image.id());
    assert!(!token.covers_sources(f.app.world(), &meshes, &materials));
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "One staged candidate exercises its complete receipt and revocation lifecycle"
)]
fn tile_candidate_needs_its_exact_completed_fence_and_preserves_the_live_selection() {
    use crate::render_inventory::tests::SourceMaterial;
    use crate::source_terrain::resident_tiles::{ResidentTiles, TileAdmission};
    use forge_world::terrain_lod::CdlodNode;

    let (mut f, wrong_token) = observed();
    let root = f
        .app
        .world_mut()
        .spawn((Mesh3d(f.mesh.clone()), MeshMaterial3d(f.material.clone())))
        .id();
    let node = |x| CdlodNode {
        origin_xz: [x, 0.0],
        size_m: 8.0,
        lod: 0,
    };
    let mut tiles = ResidentTiles::default();
    tiles.select::<SourceMaterial>(f.app.world_mut(), root, &[node(0.0)], |_| {
        Mesh::from(Cuboid::default())
    });
    let old = f.app.world().get::<Mesh3d>(root).unwrap().0.clone();
    let requested = [node(0.0), node(8.0)];
    let candidate = tiles
        .prepare::<SourceMaterial>(f.app.world_mut(), root, &requested, 512, |_| {
            Mesh::from(Cuboid::default())
        })
        .unwrap();
    let mut admission = TileAdmission {
        owner: f.owner,
        identity: f.identity,
        generation: 2,
        registration_frame: 28,
    };
    assert_eq!(tiles.counts(), [1, 1]);
    assert_eq!(f.app.world().get::<Mesh3d>(root).unwrap().0, old);
    // A host may re-observe the unchanged live view while the candidate warms.
    // That must repair bindings without invalidating the candidate.
    tiles.select::<SourceMaterial>(f.app.world_mut(), root, &[node(0.0)], |_| {
        panic!("an unchanged resident patch must not be rebuilt")
    });
    assert!(
        tiles
            .publish(
                f.app.world_mut(),
                &candidate,
                &requested,
                &wrong_token,
                &admission
            )
            .is_err()
    );
    assert_eq!(tiles.counts(), [1, 1]);

    let owner = f.owner;
    let identity = f.identity;
    crate::render_inventory::request(
        f.app.world_mut(),
        owner,
        identity,
        2,
        candidate.meshes(),
        std::slice::from_ref(candidate.material()),
    )
    .unwrap();
    let (key, request) = current(f.app.world()).unwrap();
    let mut evidence = f.app.world().resource::<RenderInventoryEvidence>().clone();
    evidence.key = Some(key);
    evidence.required = request.required.clone();
    evidence.generation = Some(2);
    f.app.world_mut().insert_resource(evidence);
    let token = acquire(f.app.world(), f.owner, f.identity, 2).unwrap();
    assert!(
        tiles
            .publish(
                f.app.world_mut(),
                &candidate,
                &[node(0.0), node(16.0)],
                &token,
                &admission
            )
            .is_err()
    );
    assert_eq!(tiles.counts(), [1, 1]);
    admission.registration_frame = 29;
    assert!(
        tiles
            .publish(
                f.app.world_mut(),
                &candidate,
                &requested,
                &token,
                &admission
            )
            .is_err()
    );
    admission.registration_frame = 28;
    admission.owner = EntityId::deterministic("unrelated-owner");
    assert!(
        tiles
            .publish(
                f.app.world_mut(),
                &candidate,
                &requested,
                &token,
                &admission
            )
            .is_err()
    );
    admission.owner = owner;
    assert_eq!(tiles.counts(), [1, 1]);
    tiles
        .publish(
            f.app.world_mut(),
            &candidate,
            &requested,
            &token,
            &admission,
        )
        .unwrap();
    assert_eq!(tiles.counts(), [2, 2]);
    assert!(
        tiles
            .entities()
            .iter()
            .all(|entity| tiles.owns(f.app.world(), *entity))
    );
    assert!(
        tiles
            .publish(
                f.app.world_mut(),
                &candidate,
                &requested,
                &token,
                &admission
            )
            .is_err()
    );
    let same = tiles
        .prepare::<SourceMaterial>(f.app.world_mut(), root, &requested, 512, |_| {
            panic!("the unchanged candidate uses resident meshes")
        })
        .unwrap();
    tiles
        .publish(f.app.world_mut(), &same, &requested, &token, &admission)
        .unwrap();
    assert!(
        tiles
            .publish(f.app.world_mut(), &same, &requested, &token, &admission)
            .is_err()
    );
    // A clear and re-selection can reach the same generation number. The old
    // candidate must still be rejected even when its renderer token stays live.
    tiles.clear(f.app.world_mut());
    tiles.select::<SourceMaterial>(f.app.world_mut(), root, &[node(0.0)], |_| {
        Mesh::from(Cuboid::default())
    });
    assert!(
        tiles
            .publish(
                f.app.world_mut(),
                &candidate,
                &requested,
                &token,
                &admission
            )
            .is_err()
    );
}

#[test]
fn stale_frames_and_released_requests_cannot_reuse_a_receipt() {
    let (mut f, token) = observed();
    f.app.world_mut().insert_resource(FrameCount(33));
    assert!(!token.is_current(f.app.world(), f.owner, f.identity, 1));
    f.app.world_mut().insert_resource(FrameCount(30));
    crate::render_inventory::release(f.app.world_mut(), f.owner).unwrap();
    assert!(!token.is_current(f.app.world(), f.owner, f.identity, 1));
    f.request(1).unwrap();
    assert!(!token.is_current(f.app.world(), f.owner, f.identity, 1));
    assert!(acquire(f.app.world(), f.owner, f.identity, 1).is_none());
}

fn observe_scoped_for_staging(world: &mut World, owner: EntityId, completed: u32) {
    let (key, request) = current_at(world, Slot::Scoped, owner).unwrap();
    let (identity, generation, required) = (
        request.identity,
        request.generation,
        request.required.clone(),
    );
    let mut evidence = world.resource::<RenderInventoryEvidence>().clone();
    evidence.key = Some(key);
    evidence.required = required;
    evidence.owner = Some(owner);
    evidence.identity = Some(identity);
    evidence.generation = Some(generation);
    evidence.observed = world.resource::<FrameCount>().0;
    evidence.completed = Some(completed);
    world
        .resource_mut::<ScopedEvidence>()
        .0
        .insert(owner, evidence);
}

#[test]
fn staged_scope_keeps_the_current_receipt_and_requires_its_own_later_fences() {
    use crate::render_inventory::{StagedScopedInventory, request_scoped};
    let (mut f, _) = observed();
    f.app.init_resource::<ScopedEvidence>();
    let current = EntityId::deterministic("staged-current");
    let next = EntityId::deterministic("staged-next");
    let next_identity = ContentHash::from_bytes([9; 32]);
    let materials = [f.material.clone()];
    let old_mesh = [f.mesh.clone()];
    request_scoped(
        f.app.world_mut(),
        current,
        f.identity,
        1,
        &old_mesh,
        &materials,
    )
    .unwrap();
    observe_scoped_for_staging(f.app.world_mut(), current, 28);
    let old_token = acquire_scoped(f.app.world(), current, f.identity, 1).unwrap();
    let added = f
        .app
        .world_mut()
        .resource_mut::<Assets<Mesh>>()
        .add(Cuboid::default());
    let expanded = [f.mesh.clone(), added];
    let mut staged = StagedScopedInventory::begin(
        f.app.world_mut(),
        current,
        next,
        next_identity,
        1,
        &expanded,
        &materials,
    )
    .unwrap();
    assert!(old_token.is_current(f.app.world(), current, f.identity, 1));
    assert!(staged.try_activate(f.app.world()).unwrap().is_none());
    observe_scoped_for_staging(f.app.world_mut(), next, 29);
    assert!(staged.try_activate(f.app.world()).unwrap().is_none());
    observe_scoped_for_staging(f.app.world_mut(), next, 30);
    let active = staged.try_activate(f.app.world()).unwrap().unwrap();
    assert_eq!(active.owner, next);
    assert_eq!(active.registration_frame, 30);
    assert!(old_token.is_current(f.app.world(), current, f.identity, 1));
    // Staging at frame 30 cannot acknowledge a later scene publication.
    f.app.world_mut().insert_resource(FrameCount(31));
    assert!(!active.token.covers_frame(f.app.world(), 31));
    observe_scoped_for_staging(f.app.world_mut(), next, 31);
    assert!(active.token.covers_frame(f.app.world(), 31));
    assert!(staged.cancel(f.app.world_mut()).is_err());
    active.cancel(f.app.world_mut()).unwrap();
    assert!(current_at(f.app.world(), Slot::Scoped, next).is_none());
    assert!(old_token.is_current(f.app.world(), current, f.identity, 1));
}

#[test]
fn staged_scope_can_retire_unused_meshes_only_after_covering_live_sources() {
    use crate::render_inventory::{StagedScopedInventory, request_scoped};
    let (mut f, _) = observed();
    f.app.init_resource::<ScopedEvidence>();
    let current = EntityId::deterministic("pruned-current");
    let next = EntityId::deterministic("pruned-next");
    let old_unused = f
        .app
        .world_mut()
        .resource_mut::<Assets<Mesh>>()
        .add(Cuboid::default());
    let future = f
        .app
        .world_mut()
        .resource_mut::<Assets<Mesh>>()
        .add(Cuboid::default());
    let materials = [f.material.clone()];
    let previous = [f.mesh.clone(), old_unused.clone()];
    let replacement = [f.mesh.clone(), future.clone()];
    request_scoped(
        f.app.world_mut(),
        current,
        f.identity,
        1,
        &previous,
        &materials,
    )
    .unwrap();
    observe_scoped_for_staging(f.app.world_mut(), current, 28);
    let old_token = acquire_scoped(f.app.world(), current, f.identity, 1).unwrap();

    assert!(
        StagedScopedInventory::begin(
            f.app.world_mut(),
            current,
            next,
            f.identity,
            2,
            &replacement,
            &materials,
        )
        .is_err()
    );
    assert!(
        StagedScopedInventory::begin_with_live_sources(
            f.app.world_mut(),
            current,
            next,
            f.identity,
            2,
            std::slice::from_ref(&old_unused),
            &replacement,
            &materials,
        )
        .is_err()
    );
    assert!(
        StagedScopedInventory::begin_with_live_sources(
            f.app.world_mut(),
            current,
            next,
            f.identity,
            2,
            std::slice::from_ref(&future),
            &replacement,
            &materials,
        )
        .is_err()
    );
    let mut staged = StagedScopedInventory::begin_with_live_sources(
        f.app.world_mut(),
        current,
        next,
        f.identity,
        2,
        std::slice::from_ref(&f.mesh),
        &replacement,
        &materials,
    )
    .unwrap();
    assert!(old_token.is_current(f.app.world(), current, f.identity, 1));
    assert!(staged.try_activate(f.app.world()).unwrap().is_none());
    observe_scoped_for_staging(f.app.world_mut(), next, 30);
    let active = staged.try_activate(f.app.world()).unwrap().unwrap();
    assert!(
        active
            .token
            .covers_subset(f.app.world(), std::slice::from_ref(&f.mesh), &materials)
    );
    assert!(
        !active
            .token
            .covers_subset(f.app.world(), &[old_unused], &materials)
    );
    active.release_previous(f.app.world_mut(), current).unwrap();
    assert!(!old_token.is_current(f.app.world(), current, f.identity, 1));
    active.cancel(f.app.world_mut()).unwrap();
}

#[test]
fn cancelling_an_activated_candidate_cannot_release_its_successor() {
    use crate::render_inventory::{StagedScopedInventory, release_scoped, request_scoped};
    let (mut f, _) = observed();
    f.app.init_resource::<ScopedEvidence>();
    let current = EntityId::deterministic("serial-live");
    let next = EntityId::deterministic("serial-candidate");
    let meshes = [f.mesh.clone()];
    let materials = [f.material.clone()];
    request_scoped(
        f.app.world_mut(),
        current,
        f.identity,
        1,
        &meshes,
        &materials,
    )
    .unwrap();
    observe_scoped_for_staging(f.app.world_mut(), current, 28);
    let mut staged = StagedScopedInventory::begin(
        f.app.world_mut(),
        current,
        next,
        f.identity,
        1,
        &meshes,
        &materials,
    )
    .unwrap();
    observe_scoped_for_staging(f.app.world_mut(), next, 30);
    let active = staged.try_activate(f.app.world()).unwrap().unwrap();
    release_scoped(f.app.world_mut(), next);
    request_scoped(f.app.world_mut(), next, f.identity, 1, &meshes, &materials).unwrap();
    assert!(active.cancel(f.app.world_mut()).is_err());
    assert!(current_at(f.app.world(), Slot::Scoped, next).is_some());
    assert!(acquire_scoped(f.app.world(), current, f.identity, 1).is_some());
}

fn activated_scoped_pair() -> (
    Fixture,
    crate::render_inventory::ActivatedScopedInventory<
        crate::render_inventory::tests::SourceMaterial,
    >,
    EntityId,
    EntityId,
) {
    use crate::render_inventory::{StagedScopedInventory, request_scoped};
    let (mut f, _) = observed();
    f.app.init_resource::<ScopedEvidence>();
    let previous = EntityId::deterministic("exact-previous");
    let candidate = EntityId::deterministic("exact-candidate");
    let meshes = [f.mesh.clone()];
    let materials = [f.material.clone()];
    request_scoped(
        f.app.world_mut(),
        previous,
        f.identity,
        1,
        &meshes,
        &materials,
    )
    .unwrap();
    observe_scoped_for_staging(f.app.world_mut(), previous, 28);
    let mut staged = StagedScopedInventory::begin(
        f.app.world_mut(),
        previous,
        candidate,
        f.identity,
        1,
        &meshes,
        &materials,
    )
    .unwrap();
    observe_scoped_for_staging(f.app.world_mut(), candidate, 30);
    let active = staged.try_activate(f.app.world()).unwrap().unwrap();
    (f, active, previous, candidate)
}

#[test]
fn activated_scope_can_retire_its_exact_previous_request() {
    let (mut f, active, previous, candidate) = activated_scoped_pair();
    assert!(active.request_is_current(f.app.world()));
    assert!(active.previous_request_is_current(f.app.world(), previous));
    active
        .release_previous(f.app.world_mut(), previous)
        .unwrap();
    assert!(current_at(f.app.world(), Slot::Scoped, previous).is_none());
    assert!(acquire_scoped(f.app.world(), candidate, f.identity, 1).is_some());
    active.cancel(f.app.world_mut()).unwrap();
}

#[test]
fn activated_scope_does_not_retire_a_replaced_previous_request() {
    use crate::render_inventory::{release_scoped, request_scoped};
    let (mut f, active, previous, candidate) = activated_scoped_pair();
    let meshes = [f.mesh.clone()];
    let materials = [f.material.clone()];
    release_scoped(f.app.world_mut(), previous);
    request_scoped(
        f.app.world_mut(),
        previous,
        f.identity,
        1,
        &meshes,
        &materials,
    )
    .unwrap();
    observe_scoped_for_staging(f.app.world_mut(), previous, 30);
    assert!(acquire_scoped(f.app.world(), previous, f.identity, 1).is_some());
    assert!(!active.previous_request_is_current(f.app.world(), previous));
    assert!(
        active
            .release_previous(f.app.world_mut(), previous)
            .is_err()
    );
    assert!(acquire_scoped(f.app.world(), previous, f.identity, 1).is_some());
    assert!(acquire_scoped(f.app.world(), candidate, f.identity, 1).is_some());
    active.cancel(f.app.world_mut()).unwrap();
}

#[test]
fn activated_scope_does_not_retire_previous_after_candidate_replacement() {
    use crate::render_inventory::{release_scoped, request_scoped};
    let (mut f, active, previous, candidate) = activated_scoped_pair();
    let meshes = [f.mesh.clone()];
    let materials = [f.material.clone()];
    release_scoped(f.app.world_mut(), candidate);
    request_scoped(
        f.app.world_mut(),
        candidate,
        f.identity,
        1,
        &meshes,
        &materials,
    )
    .unwrap();
    assert!(!active.request_is_current(f.app.world()));
    assert!(
        active
            .release_previous(f.app.world_mut(), previous)
            .is_err()
    );
    assert!(acquire_scoped(f.app.world(), previous, f.identity, 1).is_some());
    assert!(current_at(f.app.world(), Slot::Scoped, candidate).is_some());
    assert!(active.cancel(f.app.world_mut()).is_err());
}

#[test]
fn activated_scope_keeps_previous_when_candidate_gpu_epoch_is_revoked() {
    let (mut f, active, previous, candidate) = activated_scoped_pair();
    f.app
        .world_mut()
        .resource_mut::<ScopedEvidence>()
        .0
        .get_mut(&candidate)
        .unwrap()
        .epoch += 1;
    assert!(active.request_is_current(f.app.world()));
    assert!(active.previous_request_is_current(f.app.world(), previous));
    assert!(
        active
            .release_previous(f.app.world_mut(), previous)
            .is_err()
    );
    assert!(acquire_scoped(f.app.world(), previous, f.identity, 1).is_some());
    active.cancel(f.app.world_mut()).unwrap();
}

#[test]
fn staged_scope_rejects_missing_sources_and_cannot_cancel_a_replaced_owner() {
    use crate::render_inventory::{StagedScopedInventory, release_scoped, request_scoped};
    let (mut f, _) = observed();
    f.app.init_resource::<ScopedEvidence>();
    let current = EntityId::deterministic("staged-current-refusal");
    let next = EntityId::deterministic("staged-next-refusal");
    let material = [f.material.clone()];
    let old_mesh = [f.mesh.clone()];
    request_scoped(
        f.app.world_mut(),
        current,
        f.identity,
        1,
        &old_mesh,
        &material,
    )
    .unwrap();
    observe_scoped_for_staging(f.app.world_mut(), current, 28);
    let foreign = f
        .app
        .world_mut()
        .resource_mut::<Assets<Mesh>>()
        .add(Cuboid::default());
    assert!(
        StagedScopedInventory::begin(
            f.app.world_mut(),
            current,
            next,
            f.identity,
            1,
            &[foreign],
            &material,
        )
        .is_err()
    );
    let mut staged = StagedScopedInventory::begin(
        f.app.world_mut(),
        current,
        next,
        f.identity,
        1,
        &old_mesh,
        &material,
    )
    .unwrap();
    release_scoped(f.app.world_mut(), next);
    request_scoped(f.app.world_mut(), next, f.identity, 1, &old_mesh, &material).unwrap();
    assert!(staged.try_activate(f.app.world()).is_err());
    assert!(staged.cancel(f.app.world_mut()).is_err());
    assert!(scoped_evidence(f.app.world(), current).is_some());
    assert!(current_at(f.app.world(), Slot::Scoped, next).is_some());
}

#[test]
fn staged_scope_refuses_a_replaced_current_inventory_without_retiring_it() {
    use crate::render_inventory::{StagedScopedInventory, release_scoped, request_scoped};
    let (mut f, _) = observed();
    f.app.init_resource::<ScopedEvidence>();
    let current = EntityId::deterministic("staged-current-replaced");
    let next = EntityId::deterministic("staged-next-cancelled");
    let meshes = [f.mesh.clone()];
    let materials = [f.material.clone()];
    request_scoped(
        f.app.world_mut(),
        current,
        f.identity,
        1,
        &meshes,
        &materials,
    )
    .unwrap();
    observe_scoped_for_staging(f.app.world_mut(), current, 28);
    let mut staged = StagedScopedInventory::begin(
        f.app.world_mut(),
        current,
        next,
        f.identity,
        1,
        &meshes,
        &materials,
    )
    .unwrap();
    release_scoped(f.app.world_mut(), current);
    request_scoped(
        f.app.world_mut(),
        current,
        f.identity,
        1,
        &meshes,
        &materials,
    )
    .unwrap();
    assert!(staged.try_activate(f.app.world()).is_err());
    staged.cancel(f.app.world_mut()).unwrap();
    assert!(current_at(f.app.world(), Slot::Scoped, current).is_some());
    assert!(current_at(f.app.world(), Slot::Scoped, next).is_none());
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "One test covers the full cache, staged receipt and later publication lifecycle"
)]
fn gpu_admitted_future_tiles_remain_hidden_until_selected_and_need_a_later_fence() {
    use crate::render_inventory::tests::SourceMaterial;
    use crate::render_inventory::{StagedScopedInventory, request_scoped};
    use crate::source_terrain::resident_tiles::{ResidentTiles, TileAdmission};
    use forge_world::terrain_lod::CdlodNode;

    let (mut f, _) = observed();
    f.app.init_resource::<ScopedEvidence>();
    let root = f
        .app
        .world_mut()
        .spawn((
            Mesh3d(f.mesh.clone()),
            MeshMaterial3d(f.material.clone()),
            Visibility::Inherited,
        ))
        .id();
    let node = |x| CdlodNode {
        origin_xz: [x, 0.0],
        size_m: 8.0,
        lod: 0,
    };
    let mut tiles = ResidentTiles::default();
    tiles.select::<SourceMaterial>(f.app.world_mut(), root, &[node(0.0)], |_| {
        Mesh::from(Cuboid::default())
    });
    let old_mesh = f.app.world().get::<Mesh3d>(root).unwrap().0.clone();
    let current = EntityId::deterministic("retained-current");
    let next = EntityId::deterministic("retained-stage");
    let identity = ContentHash::from_bytes([7; 32]);
    let materials = [f.material.clone()];
    request_scoped(
        f.app.world_mut(),
        current,
        f.identity,
        1,
        std::slice::from_ref(&old_mesh),
        &materials,
    )
    .unwrap();
    observe_scoped_for_staging(f.app.world_mut(), current, 28);
    let candidate = tiles
        .prepare::<SourceMaterial>(
            f.app.world_mut(),
            root,
            &[node(0.0), node(8.0)],
            512,
            |_| Mesh::from(Cuboid::default()),
        )
        .unwrap();
    let unrelated = f
        .app
        .world_mut()
        .resource_mut::<Assets<Mesh>>()
        .add(Cuboid::default());
    let mut staged_meshes = candidate.meshes().to_vec();
    staged_meshes.push(unrelated.clone());
    let mut staged = StagedScopedInventory::begin(
        f.app.world_mut(),
        current,
        next,
        identity,
        1,
        &staged_meshes,
        &materials,
    )
    .unwrap();
    observe_scoped_for_staging(f.app.world_mut(), next, 30);
    let active = staged.try_activate(f.app.world()).unwrap().unwrap();
    let admission = TileAdmission {
        owner: next,
        identity,
        generation: 1,
        registration_frame: 30,
    };
    assert!(
        !active
            .token
            .covers_sources(f.app.world(), candidate.meshes(), &materials)
    );
    assert!(
        active
            .token
            .covers_subset(f.app.world(), candidate.meshes(), &materials)
    );
    let before_entities = tiles.entities();
    assert_eq!(
        tiles.retain_prepared(f.app.world(), &candidate, &active.token, &admission),
        Ok(1)
    );
    assert_eq!(tiles.counts(), [1, 2]);
    assert_eq!(tiles.entities(), before_entities);
    assert_eq!(f.app.world().get::<Mesh3d>(root).unwrap().0, old_mesh);
    assert!(
        tiles
            .retain_prepared(f.app.world(), &candidate, &active.token, &admission)
            .is_err()
    );
    tiles.select::<SourceMaterial>(f.app.world_mut(), root, &[node(0.0)], |_| {
        panic!("the old visible patch must remain resident")
    });
    tiles.select::<SourceMaterial>(f.app.world_mut(), root, &[node(0.0), node(8.0)], |_| {
        panic!("the admitted future patch must be reused")
    });
    assert_eq!(tiles.counts(), [2, 2]);
    f.app.world_mut().insert_resource(FrameCount(31));
    assert!(!active.token.covers_frame(f.app.world(), 31));
    observe_scoped_for_staging(f.app.world_mut(), next, 31);
    assert!(active.token.covers_frame(f.app.world(), 31));
    assert!(
        !active
            .token
            .covers_subset::<SourceMaterial>(f.app.world(), &[unrelated], &[])
    );
}
