use super::*;
use bevy::{
    camera::{RenderTarget, RenderTargetInfo},
    diagnostic::FrameCount,
};
use forge_world::cell_streaming::{CellDetail, CellKey, CellStreamingPolicy, LoadedCell};

fn policy() -> CellStreamingPolicy {
    CellStreamingPolicy {
        origin_xz_m: [0.0, 0.0],
        cell_size_m: 16.0,
        dimensions: [8, 8],
        detail_radius: 0,
        far_radius: 2,
        max_resident_cells: 25,
        full_cell_bytes: 100_000,
        hlod_cell_bytes: 100_000,
        max_resident_bytes: 2_500_000,
        max_staging_bytes: 5_000_000,
    }
}

fn hash() -> ContentHash {
    "ab".repeat(32).parse().unwrap()
}

fn source_fixture() -> (App, Vec<Entity>) {
    let mut app = App::new();
    app.add_plugins(MinimalPlugins)
        .init_resource::<Assets<Mesh>>()
        .init_resource::<Assets<StandardMaterial>>()
        .add_plugins(StaticHlodPlugin::default());
    app.world_mut().insert_resource(FrameCount(20));
    app.world_mut().spawn((
        Camera3d::default(),
        Camera {
            computed: bevy::camera::ComputedCameraValues {
                target_info: Some(RenderTargetInfo {
                    physical_size: UVec2::new(640, 480),
                    scale_factor: 1.0,
                }),
                ..default()
            },
            ..default()
        },
        RenderTarget::None {
            size: UVec2::new(640, 480),
        },
    ));
    let mesh = app
        .world_mut()
        .resource_mut::<Assets<Mesh>>()
        .add(Cuboid::new(1.0, 1.0, 1.0));
    let material = app
        .world_mut()
        .resource_mut::<Assets<StandardMaterial>>()
        .add(StandardMaterial::default());
    let entities = (0_i16..2)
        .map(|index| {
            app.world_mut()
                .spawn((
                    StaticHlodSource {
                        object: EntityId::deterministic(format!("static-{index}")),
                        source: hash(),
                    },
                    Mesh3d(mesh.clone()),
                    MeshMaterial3d(material.clone()),
                    Transform::from_xyz(72.0 + f32::from(index) * 2.0, 1.0, 72.0),
                    Visibility::Inherited,
                ))
                .id()
        })
        .collect();
    (app, entities)
}

#[test]
fn gpu_acknowledgement_uses_the_scene_camera_with_offscreen_passes() {
    let (mut app, _) = source_fixture();
    let world = app.world_mut();
    let auxiliary = world
        .spawn((
            Camera3d::default(),
            crate::scene_camera::OffscreenSceneCamera,
        ))
        .id();
    gpu::acknowledge(world, 1);
    assert!(gpu::ready(world, 1));

    world
        .entity_mut(auxiliary)
        .remove::<crate::scene_camera::OffscreenSceneCamera>();
    assert!(!gpu::ready(world, 1));
}

fn synthetic_request(world: &mut World) -> Request {
    world.resource_scope(|world, mut state: Mut<State>| {
        // Collect admission exactly as production, then supply the explicit
        // terrain partition to the pure selector; no renderer/terrain claim.
        assert!(collect::request(world, &mut state).unwrap().is_none());
        let routes = world.resource::<Routes>().0.clone();
        let mut sources: Vec<_> = world
            .query::<(Entity, &StaticHlodSource, &Mesh3d)>()
            .iter(world)
            .map(|(entity, identity, mesh)| {
                collect::inspect(world, &state, &routes, entity, *identity, mesh.id()).unwrap()
            })
            .collect();
        sources.sort_by_key(|source| (source.identity.object, source.entity));
        for source in &mut sources {
            source.node = Some(TerrainHlodKey {
                x: 4,
                z: 4,
                level: 1,
            });
        }
        let policy = policy();
        let cells = BTreeMap::new();
        let selection = crate::cell_streaming::SampledRenderSelection {
            source: hash(),
            policy: &policy,
            cells: &cells,
            distant: None,
        };
        let groups = selection::groups(&selection, &sources, &state.cache);
        Request {
            enabled: true,
            terrain: hash(),
            sources,
            groups,
        }
    })
}

fn prepare_and_admit(world: &mut World, request: Request) {
    world.resource_scope(|world, mut state: Mut<State>| {
        state.generation += 1;
        let inputs = request
            .groups
            .iter()
            .map(|group| InputGroup {
                group: group.clone(),
                parts: group
                    .members
                    .iter()
                    .map(|&index| {
                        let source = &request.sources[index];
                        let mut pose = source.pose;
                        pose.translation -= group.origin;
                        (Arc::clone(&state.cache[&source.mesh].mesh), pose)
                    })
                    .collect(),
            })
            .collect();
        let prepared = lifecycle::prepare(inputs).unwrap();
        state.request = Some(request);
        lifecycle::admit(world, &mut state, prepared);
    });
}

#[test]
fn selection_reuses_terrain_owners_and_keeps_a_whole_object_touching_full_detail() {
    let policy = policy();
    let key = CellKey { x: 3, z: 3 };
    let cells = BTreeMap::from([(
        key,
        LoadedCell {
            key,
            detail: CellDetail::Full,
            source: hash(),
            bytes: 1,
        },
    )]);
    let details = BTreeMap::from([(key, CellDetail::Full)]);
    let distant = forge_world::terrain_hlod::TerrainHlodLayout::new(&policy, &details).unwrap();
    let selection = crate::cell_streaming::SampledRenderSelection {
        source: hash(),
        policy: &policy,
        cells: &cells,
        distant: Some(&distant),
    };
    assert!(
        selection::node(
            &selection,
            Vec3::new(63.0, 0.0, 63.0),
            Vec3::new(95.0, 3.0, 95.0)
        )
        .is_none()
    );
    let node = selection::node(
        &selection,
        Vec3::new(72.0, 0.0, 72.0),
        Vec3::new(74.0, 3.0, 74.0),
    )
    .unwrap();
    assert!(distant.tiles().contains_key(&node));
    assert!(
        selection::node(
            &selection,
            Vec3::new(-1.0, 0.0, 72.0),
            Vec3::new(74.0, 3.0, 74.0)
        )
        .is_none()
    );
}

#[test]
fn cpu_preparation_keeps_originals_visible_and_cannot_acknowledge_gpu_upload() {
    let (mut app, sources) = source_fixture();
    let request = synthetic_request(app.world_mut());
    assert_eq!(request.groups.len(), 1);
    prepare_and_admit(app.world_mut(), request);
    let state = app.world().resource::<State>();
    assert!(!state.active);
    assert_eq!(state.live.len(), 1);
    assert!(state.hidden.is_empty());
    assert!(lifecycle::intact(app.world(), state));
    assert!(!gpu::ready(app.world(), state.generation));
    for entity in sources {
        assert_eq!(
            app.world().get::<Visibility>(entity),
            Some(&Visibility::Inherited)
        );
        assert!(source_is_covered(app.world(), entity));
    }
    assert_eq!(
        app.world().get::<Visibility>(state.live[0].entity),
        Some(&Visibility::Hidden)
    );
}

#[test]
fn acknowledgement_replaces_all_parts_together_and_retirement_restores_them_and_keeps_meshes() {
    let (mut app, sources) = source_fixture();
    let request = synthetic_request(app.world_mut());
    let source_mesh = request.sources[0].mesh;
    prepare_and_admit(app.world_mut(), request);
    let generation = app.world().resource::<State>().generation;
    gpu::acknowledge(app.world_mut(), generation);
    assert!(gpu::ready(app.world(), generation));
    app.world_mut()
        .resource_scope(|world, mut state: Mut<State>| lifecycle::activate(world, &mut state));
    let derived = app.world().resource::<State>().live[0].mesh.id();
    for &entity in &sources {
        assert_eq!(
            app.world().get::<Visibility>(entity),
            Some(&Visibility::Hidden)
        );
        assert!(source_is_covered(app.world(), entity));
    }
    app.world_mut()
        .resource_scope(|world, mut state: Mut<State>| lifecycle::retire(world, &mut state));
    for entity in sources {
        assert_eq!(
            app.world().get::<Visibility>(entity),
            Some(&Visibility::Inherited)
        );
        assert!(app.world().get::<StaticHlodSource>(entity).is_some());
        assert!(app.world().get::<Mesh3d>(entity).is_some());
    }
    let assets = app.world().resource::<Assets<Mesh>>();
    assert!(assets.get(source_mesh).is_some());
    assert!(assets.get(derived).is_none());
}

#[test]
fn stale_generation_view_and_missing_resources_revoke_coverage() {
    let (mut app, sources) = source_fixture();
    let request = synthetic_request(app.world_mut());
    prepare_and_admit(app.world_mut(), request);
    let generation = app.world().resource::<State>().generation;
    gpu::acknowledge(app.world_mut(), generation - 1);
    assert!(!gpu::ready(app.world(), generation));
    gpu::acknowledge(app.world_mut(), generation);
    app.world_mut()
        .resource_scope(|world, mut state: Mut<State>| lifecycle::activate(world, &mut state));
    assert!(source_is_covered(app.world(), sources[0]));
    app.world_mut().resource_mut::<FrameCount>().0 += 3;
    assert!(!source_is_covered(app.world(), sources[0]));
    gpu::acknowledge(app.world_mut(), generation);
    let mesh = app.world().resource::<State>().live[0].mesh.id();
    app.world_mut().resource_mut::<Assets<Mesh>>().remove(mesh);
    assert!(!source_is_covered(app.world(), sources[0]));
    app.world_mut()
        .resource_scope(|world, mut state: Mut<State>| lifecycle::retire(world, &mut state));
    assert!(source_is_covered(app.world(), sources[0]));
}

#[test]
fn changed_pinned_mesh_is_rejected_until_a_new_source_handle_is_admitted() {
    let (mut app, sources) = source_fixture();
    synthetic_request(app.world_mut());
    let mesh = app.world().get::<Mesh3d>(sources[0]).unwrap().id();
    app.world_mut()
        .resource_mut::<Assets<Mesh>>()
        .get_mut(mesh)
        .unwrap()
        .insert_attribute(Mesh::ATTRIBUTE_POSITION, vec![[0.0, 0.0, 0.0]; 24]);
    app.world_mut()
        .resource_scope(|world, mut state: Mut<State>| {
            state.modified_meshes.insert(mesh);
            for _ in 0..2 {
                let error = collect::request(world, &mut state).unwrap_err();
                assert!(error.contains("pinned mesh changed"));
            }
        });
}

fn wait_for(app: &mut App, predicate: impl Fn(&World) -> bool) {
    let deadline = std::time::Instant::now() + std::time::Duration::from_secs(10);
    while std::time::Instant::now() < deadline {
        app.update();
        if predicate(app.world()) {
            return;
        }
        std::thread::sleep(std::time::Duration::from_millis(1));
    }
    panic!(
        "static HLOD timed out: {:?}",
        app.world().resource::<StaticHlodStats>()
    );
}

fn streamed_fixture() -> (App, Vec<Entity>) {
    let (mut app, sources) = source_fixture();
    let terrain = crate::cell_streaming::tests::terrain_fixture();
    let policy = crate::cell_streaming::sampled_policy(terrain.raster().source()).unwrap();
    app.add_plugins(
        crate::cell_streaming::SampledCellStreamingPlugin::new(
            terrain,
            policy,
            EntityId::deterministic("static-hlod-streaming"),
            StandardMaterial::default(),
            [-64.0; 2],
        )
        .unwrap()
        .with_terrain_hlod(),
    );
    (app, sources)
}

#[test]
fn actual_streaming_drives_aggregate_return_to_full_detail_and_stale_acknowledgement_refusal() {
    let (mut app, sources) = streamed_fixture();
    let original_mesh = app.world().get::<Mesh3d>(sources[0]).unwrap().0.clone();
    let collider = bevy_rapier3d::prelude::Collider::cuboid(0.5, 0.5, 0.5);
    app.world_mut()
        .entity_mut(sources[0])
        .insert(collider.clone());
    wait_for(&mut app, |world| !world.resource::<State>().live.is_empty());
    let generation = app.world().resource::<State>().generation;
    assert!(
        app.world()
            .resource::<State>()
            .request
            .as_ref()
            .unwrap()
            .sources
            .iter()
            .all(|source| source.node.is_some())
    );
    assert!(
        sources
            .iter()
            .all(|&entity| app.world().get::<Visibility>(entity) == Some(&Visibility::Inherited))
    );
    gpu::acknowledge(app.world_mut(), generation);
    app.update();
    assert!(app.world().resource::<State>().active);
    let proxy = app.world().resource::<State>().live[0].mesh.id();
    app.world_mut()
        .resource_mut::<crate::cell_streaming::CellStreamingObserver>()
        .position_xz_m = [72.0; 2];
    wait_for(&mut app, |world| {
        world
            .resource::<State>()
            .request
            .as_ref()
            .is_some_and(|request| request.groups.is_empty())
    });
    assert!(app.world().resource::<State>().live.is_empty());
    assert!(app.world().resource::<Assets<Mesh>>().get(proxy).is_none());
    assert!(
        sources
            .iter()
            .all(|&entity| app.world().get::<Visibility>(entity) == Some(&Visibility::Inherited))
    );
    assert_eq!(
        app.world().get::<Mesh3d>(sources[0]).unwrap().0,
        original_mesh
    );
    assert!(std::ptr::eq(
        collider.raw.as_ref(),
        app.world()
            .get::<bevy_rapier3d::prelude::Collider>(sources[0])
            .unwrap()
            .raw
            .as_ref()
    ));
    app.world_mut()
        .resource_mut::<crate::cell_streaming::CellStreamingObserver>()
        .position_xz_m = [-64.0; 2];
    wait_for(&mut app, |world| !world.resource::<State>().live.is_empty());
    assert!(app.world().resource::<State>().generation > generation);
    gpu::acknowledge(app.world_mut(), generation);
    app.update();
    assert!(!app.world().resource::<State>().active);
    assert!(
        sources
            .iter()
            .all(|&entity| source_is_covered(app.world(), entity))
    );
}

#[test]
fn a_blended_primitive_keeps_its_whole_object_in_detail() {
    let (mut app, sources) = streamed_fixture();
    let world = app.world_mut();
    let object = *world.get::<StaticHlodSource>(sources[1]).unwrap();
    let mesh = world.get::<Mesh3d>(sources[1]).unwrap().clone();
    let pose = *world.get::<Transform>(sources[1]).unwrap();
    let glass = world
        .resource_mut::<Assets<StandardMaterial>>()
        .add(StandardMaterial {
            alpha_mode: AlphaMode::Blend,
            ..default()
        });
    let window = world
        .spawn((
            object,
            mesh,
            MeshMaterial3d(glass),
            pose,
            Visibility::Inherited,
        ))
        .id();
    wait_for(&mut app, |world| {
        world.resource::<State>().request.is_some()
    });
    let world = app.world();
    let request = world.resource::<State>().request.as_ref().unwrap();
    assert_eq!(request.sources.len(), 3);
    for source in &request.sources {
        assert_eq!(source.node.is_some(), source.entity == sources[0]);
    }
    assert!(source_is_covered(world, sources[1]));
    assert!(source_is_covered(world, window));
    assert!(world.resource::<StaticHlodStats>().last_error.is_none());
}

#[test]
fn retained_materials_keep_blended_originals_covered_without_claiming_a_proxy() {
    let (mut app, sources) = streamed_fixture();
    app.world_mut().resource_mut::<Routes>().0 =
        vec![StaticHlodMaterial::retained_detail::<StandardMaterial>()];
    let material = app
        .world_mut()
        .resource_mut::<Assets<StandardMaterial>>()
        .add(StandardMaterial {
            alpha_mode: AlphaMode::Blend,
            ..default()
        });
    app.world_mut()
        .entity_mut(sources[1])
        .insert(MeshMaterial3d(material.clone()));
    let original = sources
        .iter()
        .map(|&entity| app.world().get::<Mesh3d>(entity).unwrap().0.clone())
        .collect::<Vec<_>>();
    wait_for(&mut app, |world| {
        world
            .resource::<State>()
            .request
            .as_ref()
            .is_some_and(|request| {
                request.sources.len() == 2
                    && request.sources.iter().all(|source| source.node.is_none())
            })
    });
    assert!(app.world().resource::<State>().live.is_empty());
    assert!(!gpu_upload_ready(app.world()));
    for (&entity, mesh) in sources.iter().zip(original) {
        assert_eq!(&app.world().get::<Mesh3d>(entity).unwrap().0, &mesh);
        assert_eq!(
            app.world().get::<Visibility>(entity),
            Some(&Visibility::Inherited)
        );
        assert!(source_is_covered(app.world(), entity));
    }
    app.world_mut()
        .resource_mut::<Assets<StandardMaterial>>()
        .remove(material.id());
    app.update();
    assert!(
        app.world()
            .resource::<StaticHlodStats>()
            .last_error
            .is_some()
    );
    assert!(!source_is_covered(app.world(), sources[1]));
    // Ordinary modules can render originals without installing the hierarchy;
    // that path still requires a resident material and never invents a proxy.
    app.world_mut().remove_resource::<Routes>();
    assert!(source_is_covered(app.world(), sources[0]));
    assert!(!source_is_covered(app.world(), sources[1]));
}

#[test]
fn a_whole_object_keeps_detail_when_its_other_part_touches_the_near_cell() {
    let (mut app, sources) = streamed_fixture();
    let identity = *app.world().get::<StaticHlodSource>(sources[0]).unwrap();
    app.world_mut()
        .entity_mut(sources[1])
        .insert((identity, Transform::from_xyz(-64.0, 1.0, -64.0)));
    wait_for(&mut app, |world| {
        world.resource::<State>().request.is_some()
    });
    let request = app.world().resource::<State>().request.as_ref().unwrap();
    assert_eq!(request.sources.len(), 2);
    assert!(request.sources.iter().all(|source| source.node.is_none()));
    assert!(request.groups.is_empty());
    assert!(selection_is_ready(app.world()));
    assert!(app.world().resource::<State>().live.is_empty());
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "Exercises proxy admission, animation, independent retention and release in one scene"
)]
fn pendulum_keeps_its_object_detailed_without_rebuilding_other_static_objects() {
    use crate::pendulum_motion::{
        LanternMotion, Wind,
        host::{Binding, Motion, Resident},
    };
    use forge_core::{AssetId, Transform as DomainTransform};
    use forge_project::zone_authoring::placements::ManualPlacement;

    let (mut app, sources) = streamed_fixture();
    let identity = *app.world().get::<StaticHlodSource>(sources[0]).unwrap();
    let placement = ManualPlacement {
        id: identity.object,
        name: "Moving test object".into(),
        asset_id: AssetId::deterministic("moving-test-asset"),
        asset_hash: identity.source,
        transform: DomainTransform::IDENTITY,
        locked: false,
        physics: None,
        particles: None,
        local_lights: None,
    };
    let root = app
        .world_mut()
        .spawn((
            crate::ManualPlacementProjection(placement.clone()),
            Transform::IDENTITY,
            Visibility::Inherited,
        ))
        .id();
    let pivot = Vec3::new(72.0, 1.0, 72.0);
    let hinge = app
        .world_mut()
        .spawn((
            ChildOf(root),
            Transform::from_translation(pivot),
            Visibility::Inherited,
        ))
        .id();
    app.world_mut()
        .entity_mut(sources[0])
        .insert((ChildOf(hinge), Transform::IDENTITY));
    let mesh = app.world().get::<Mesh3d>(sources[0]).unwrap().0.clone();
    let material = app
        .world()
        .get::<MeshMaterial3d<StandardMaterial>>(sources[0])
        .unwrap()
        .0
        .clone();
    let sibling = app
        .world_mut()
        .spawn((
            identity,
            Mesh3d(mesh),
            MeshMaterial3d(material),
            ChildOf(root),
            Transform::from_xyz(74.0, 1.0, 72.0),
            Visibility::Inherited,
        ))
        .id();

    wait_for(&mut app, |world| !world.resource::<State>().live.is_empty());
    let old_generation = app.world().resource::<State>().generation;
    gpu::acknowledge(app.world_mut(), old_generation);
    app.update();
    assert!(app.world().resource::<State>().active);
    assert_eq!(
        app.world().get::<Visibility>(sources[0]),
        Some(&Visibility::Hidden)
    );

    let mut host = Resident::bind(
        app.world_mut(),
        &placement,
        root,
        vec![Binding {
            key: 0,
            entity: hinge,
            pivot,
            rest_rotation: Quat::IDENTITY,
            rest_scale: Vec3::ONE,
            motion: Motion::Suspended(LanternMotion {
                t: 0.0,
                ph: 0.7,
                x: 0.0,
                z: 0.0,
                vx: 0.0,
                vz: 0.0,
            }),
        }],
    )
    .unwrap();
    // The old proxy is retired synchronously, before the first animated pose.
    assert!(!app.world().resource::<State>().active);
    for entity in [sources[0], sibling, sources[1]] {
        assert_eq!(
            app.world().get::<Visibility>(entity),
            Some(&Visibility::Inherited)
        );
    }
    wait_for(&mut app, |world| {
        world
            .resource::<State>()
            .request
            .as_ref()
            .is_some_and(|request| {
                request
                    .sources
                    .iter()
                    .filter(|source| source.identity.object == identity.object)
                    .count()
                    == 2
                    && request
                        .sources
                        .iter()
                        .filter(|source| source.identity.object == identity.object)
                        .all(|source| source.retained_motion && source.node.is_none())
                    && request
                        .sources
                        .iter()
                        .any(|source| source.entity == sources[1] && source.node.is_some())
            })
            && !world.resource::<State>().live.is_empty()
    });
    let generation = app.world().resource::<State>().generation;
    gpu::acknowledge(app.world_mut(), generation);
    app.update();
    assert!(app.world().resource::<State>().active);
    let wind = Wind {
        speed: 7.0,
        direction: [1.0, 0.0],
    };
    for _ in 0..8 {
        host.advance(app.world_mut(), 1.0 / 60.0, wind).unwrap();
        // A real render device republishes fresh evidence; this headless fixture
        // must do the same to distinguish motion churn from evidence expiry.
        gpu::acknowledge(app.world_mut(), generation);
        app.update();
        assert_eq!(app.world().resource::<State>().generation, generation);
        assert!(app.world().resource::<State>().active);
        assert_eq!(
            app.world().get::<Visibility>(sources[0]),
            Some(&Visibility::Inherited)
        );
        assert_eq!(
            app.world().get::<Visibility>(sibling),
            Some(&Visibility::Inherited)
        );
        assert!(source_is_covered(app.world(), sources[0]));
    }
    assert_ne!(
        app.world().get::<Transform>(hinge).unwrap().rotation,
        Quat::IDENTITY
    );

    retain_moving_object(app.world_mut(), identity.object, "other-animation").unwrap();
    host.release(app.world_mut());
    assert!(moving_object_retained(app.world(), identity.object));
    release_moving_object(app.world_mut(), identity.object, "other-animation");
    wait_for(&mut app, |world| {
        world
            .resource::<State>()
            .request
            .as_ref()
            .is_some_and(|request| {
                request
                    .sources
                    .iter()
                    .filter(|source| source.identity.object == identity.object)
                    .all(|source| !source.retained_motion && source.node.is_some())
            })
    });
}

#[test]
fn a_preparation_finishing_after_the_terrain_partition_changed_is_discarded() {
    let (mut app, sources) = streamed_fixture();
    wait_for(&mut app, |world| {
        world.resource::<State>().pending.is_some()
    });
    // The terrain update uses the same pool. A blocking receive here deadlocks
    // a one-worker executor before that update can invalidate this job.
    let release = std::sync::Arc::new(std::sync::Mutex::new((false, None::<std::task::Waker>)));
    let held = release.clone();
    let held_deadline = std::time::Instant::now() + std::time::Duration::from_secs(5);
    let generation = app.world().resource::<State>().generation;
    app.world_mut().resource_scope(|_, mut state: Mut<State>| {
        let Job {
            request,
            generation,
            input_bytes,
            task,
        } = state.pending.take().unwrap();
        state.pending = Some(Job {
            request,
            generation,
            input_bytes,
            task: bevy::tasks::AsyncComputeTaskPool::get().spawn(async move {
                std::future::poll_fn(|context| {
                    let mut gate = held.lock().unwrap();
                    if gate.0 {
                        std::task::Poll::Ready(())
                    } else {
                        gate.1 = Some(context.waker().clone());
                        std::task::Poll::Pending
                    }
                })
                .await;
                task.await
            }),
        });
    });
    app.world_mut()
        .resource_mut::<crate::cell_streaming::CellStreamingObserver>()
        .position_xz_m = [72.0; 2];
    wait_for(&mut app, |world| {
        assert!(
            std::time::Instant::now() < held_deadline,
            "Held HLOD preparation exceeded five seconds"
        );
        world
            .resource::<State>()
            .request
            .as_ref()
            .is_some_and(|request| request.groups.is_empty())
    });
    assert!(app.world().resource::<State>().generation > generation);
    assert!(app.world().resource::<State>().pending.is_some());
    assert!(app.world().resource::<State>().live.is_empty());
    assert!(
        sources
            .iter()
            .all(|&entity| source_is_covered(app.world(), entity))
    );
    assert!(!selection_is_ready(app.world()));
    let wake = {
        let mut gate = release.lock().unwrap();
        gate.0 = true;
        gate.1.take()
    };
    if let Some(wake) = wake {
        wake.wake();
    }
    wait_for(&mut app, |world| {
        world.resource::<State>().pending.is_none()
    });
    assert_eq!(app.world().resource::<StaticHlodStats>().discarded_jobs, 1);
    assert!(app.world().resource::<State>().live.is_empty());
    assert!(selection_is_ready(app.world()));
}

#[test]
fn touching_the_minimum_boundary_of_a_detailed_cell_also_keeps_detail() {
    let policy = policy();
    let key = CellKey { x: 3, z: 3 };
    let cells = BTreeMap::from([(
        key,
        LoadedCell {
            key,
            detail: CellDetail::Full,
            source: hash(),
            bytes: 1,
        },
    )]);
    let distant = forge_world::terrain_hlod::TerrainHlodLayout::new(
        &policy,
        &BTreeMap::from([(key, CellDetail::Full)]),
    )
    .unwrap();
    let selection = crate::cell_streaming::SampledRenderSelection {
        source: hash(),
        policy: &policy,
        cells: &cells,
        distant: Some(&distant),
    };
    assert!(
        selection::node(
            &selection,
            Vec3::new(64.0, 0.0, 48.0),
            Vec3::new(72.0, 3.0, 56.0)
        )
        .is_none()
    );
    assert!(
        selection::node(
            &selection,
            Vec3::new(64.01, 0.0, 48.0),
            Vec3::new(72.0, 3.0, 56.0)
        )
        .is_some()
    );
}

#[test]
fn the_runtime_primitive_budget_is_checked_before_mesh_acquisition() {
    let (mut app, sources) = source_fixture();
    let mesh = app.world().get::<Mesh3d>(sources[0]).unwrap().0.clone();
    for _ in 0..MAX_PRIMITIVES - 1 {
        app.world_mut().spawn((
            StaticHlodSource {
                object: EntityId::deterministic("over-budget"),
                source: hash(),
            },
            Mesh3d(mesh.clone()),
        ));
    }
    app.world_mut()
        .resource_scope(|world, mut state: Mut<State>| {
            let error = collect::request(world, &mut state).unwrap_err();
            assert!(error.contains("primitive budget"));
            assert!(state.cache.is_empty());
            assert!(state.live.is_empty());
        });
}

#[test]
fn changing_one_source_pose_or_a_proxy_shadow_flag_revokes_the_complete_transaction() {
    let (mut app, sources) = source_fixture();
    let request = synthetic_request(app.world_mut());
    prepare_and_admit(app.world_mut(), request);
    let generation = app.world().resource::<State>().generation;
    gpu::acknowledge(app.world_mut(), generation);
    app.world_mut()
        .resource_scope(|world, mut state: Mut<State>| lifecycle::activate(world, &mut state));
    app.world_mut()
        .get_mut::<Transform>(sources[0])
        .unwrap()
        .translation
        .x += 1.0;
    assert!(!source_is_covered(app.world(), sources[1]));
    app.world_mut()
        .get_mut::<Transform>(sources[0])
        .unwrap()
        .translation
        .x -= 1.0;
    assert!(source_is_covered(app.world(), sources[1]));
    let proxy = app.world().resource::<State>().live[0].entity;
    app.world_mut()
        .entity_mut(proxy)
        .insert(bevy::light::NotShadowCaster);
    assert!(!source_is_covered(app.world(), sources[1]));
}

#[test]
fn disabling_and_resuming_restores_detail_and_requires_a_new_gpu_acknowledgement() {
    let (mut app, sources) = streamed_fixture();
    wait_for(&mut app, |world| !world.resource::<State>().live.is_empty());
    let generation = app.world().resource::<State>().generation;
    gpu::acknowledge(app.world_mut(), generation);
    app.update();
    assert!(app.world().resource::<State>().active);
    let mesh = app.world().resource::<State>().live[0].mesh.id();
    app.world_mut().resource_mut::<StaticHlodSettings>().enabled = false;
    app.update();
    assert!(app.world().resource::<State>().live.is_empty());
    assert!(app.world().resource::<Assets<Mesh>>().get(mesh).is_none());
    assert!(!app.world().resource::<StaticHlodStats>().enabled);
    assert!(
        sources
            .iter()
            .all(|&entity| source_is_covered(app.world(), entity))
    );
    assert!(selection_is_ready(app.world()));
    app.world_mut().resource_mut::<StaticHlodSettings>().enabled = true;
    wait_for(&mut app, |world| !world.resource::<State>().live.is_empty());
    assert!(app.world().resource::<State>().generation > generation);
    assert!(!app.world().resource::<State>().active);
    gpu::acknowledge(app.world_mut(), generation);
    app.update();
    assert!(!app.world().resource::<State>().active);
    assert!(
        sources
            .iter()
            .all(|&entity| source_is_covered(app.world(), entity))
    );
}
