//! One real renderer lifecycle checks replacement picking through the shared pass.
use super::Snapshot;
use crate::source_vegetation::picking::{self, governed, render};
use crate::vegetation::{VisualValleyVegetationExtension, VisualValleyVegetationMaterial};
use bevy::{
    asset::RenderAssetUsages,
    camera::{primitives::Aabb, visibility::VisibilityRange},
    prelude::*,
    render::{
        gpu_readback::{Readback, ReadbackComplete},
        render_resource::{Extent3d, TextureDimension, TextureFormat},
        storage::ShaderBuffer,
    },
};
use forge_core::{ContentHash, EntityId};
use std::{
    collections::BTreeSet,
    sync::{Arc, Mutex},
    time::{Duration, Instant},
};

#[derive(Debug)]
struct Coverage {
    back: usize,
    front: usize,
    x_sum: usize,
    hash: ContentHash,
}

fn advance(app: &mut App, count: usize) {
    for _ in 0..count {
        app.update();
        std::thread::sleep(Duration::from_millis(1));
    }
}

fn coverage(app: &mut App, target: Handle<Image>) -> Coverage {
    let result = Arc::new(Mutex::new(None));
    let reply = Arc::clone(&result);
    let entity = app
        .world_mut()
        .spawn(Readback::texture(target))
        .observe(move |event: On<ReadbackComplete>| {
            let bytes = &event.data[..128 * 128 * 4];
            let mut value = Coverage {
                back: 0,
                front: 0,
                x_sum: 0,
                hash: forge_core::bytes_sha256(bytes),
            };
            for (index, pixel) in bytes.as_chunks::<4>().0.iter().enumerate() {
                let id = picking::decode(*pixel).unwrap_or(0);
                match id {
                    0 => {}
                    1 => value.back += 1,
                    2 => {
                        value.front += 1;
                        value.x_sum += index % 128;
                    }
                    _ => panic!("Noncanonical picking pixel: {pixel:?}"),
                }
            }
            *reply.lock().unwrap() = Some(value);
        })
        .id();
    let started = Instant::now();
    loop {
        advance(app, 1);
        if let Some(value) = result.lock().unwrap().take() {
            app.world_mut().despawn(entity);
            return value;
        }
        assert!(
            started.elapsed().as_secs() < 30,
            "Picking readback timed out"
        );
    }
}

#[test]
#[ignore = "Requires the native Bevy renderer and a hardware GPU"]
#[allow(
    clippy::too_many_lines,
    reason = "Single renderer validates the actual ID shader, masks, wind, identity freshness and retirement"
)]
fn replacement_ids_preserve_alpha_wind_and_nearest_canonical_identity() {
    let mut app = App::new();
    app.add_plugins(
        DefaultPlugins
            .set(WindowPlugin {
                primary_window: None,
                exit_condition: bevy::window::ExitCondition::DontExit,
                ..default()
            })
            .disable::<bevy::winit::WinitPlugin>()
            .disable::<bevy::render::pipelined_rendering::PipelinedRenderingPlugin>(),
    );
    crate::vegetation::install_temporal_material(&mut app);
    render::install(&mut app);
    governed::install(&mut app).unwrap();
    app.world_mut()
        .resource_mut::<Assets<ShaderBuffer>>()
        .insert(
            crate::temporal_lod::render::BUFFER.id(),
            ShaderBuffer::from(vec![[0_u32; 4]; 2]),
        )
        .unwrap();
    let mesh = app
        .world_mut()
        .resource_mut::<Assets<Mesh>>()
        .add(Cuboid::new(1.0, 1.0, 0.05));
    let texture = app
        .world_mut()
        .resource_mut::<Assets<Image>>()
        .add(Image::new(
            Extent3d {
                width: 2,
                height: 2,
                depth_or_array_layers: 1,
            },
            TextureDimension::D2,
            vec![
                255, 255, 255, 0, 255, 255, 255, 255, 255, 255, 255, 0, 255, 255, 255, 255,
            ],
            TextureFormat::Rgba8UnormSrgb,
            RenderAssetUsages::default(),
        ));
    let mut material = VisualValleyVegetationMaterial {
        base: StandardMaterial {
            base_color_texture: Some(texture),
            ..default()
        },
        extension: VisualValleyVegetationExtension::default()
            .with_governed_prototype_height(1.0)
            .unwrap(),
    };
    let front_material = app
        .world_mut()
        .resource_mut::<Assets<VisualValleyVegetationMaterial>>()
        .add(material.clone());
    material.base.base_color_texture = None;
    let back_material = app
        .world_mut()
        .resource_mut::<Assets<VisualValleyVegetationMaterial>>()
        .add(material);
    let head = forge_core::bytes_sha256(b"picking-canonical-head");
    let back = EntityId::deterministic("picking-back");
    let front = EntityId::deterministic("picking-front");
    let mut snapshot: Snapshot<picking::PickingMaterial> = Snapshot {
        head,
        batches: vec![],
        models: vec![],
        identities: vec![back, front],
        visible_ids: BTreeSet::new(),
    };
    let mut draws = Vec::new();
    for (id, handle, scale, depth) in [
        (back, back_material, 1.4, -0.1),
        (front, front_material, 1.0, 0.1),
    ] {
        draws.push(governed::Draw {
            entity: app.world_mut().spawn_empty().id(),
            id,
            pick_id: 0,
            mesh: mesh.clone(),
            material_id: handle.id(),
            material: Arc::new(
                app.world()
                    .resource::<Assets<VisualValleyVegetationMaterial>>()
                    .get(&handle)
                    .unwrap()
                    .clone(),
            ),
            tag: crate::vegetation::mesh_tag_for(1.4, 1.0).0,
            transform: Transform::from_xyz(0.0, 0.5, depth).with_scale(Vec3::new(scale, 1.2, 1.0)),
            bounds: Aabb::from_min_max(Vec3::splat(-1.0), Vec3::splat(1.0)),
            range: VisibilityRange {
                start_margin: 0.0..0.0,
                end_margin: 50.0..50.0,
                use_aabb: false,
            },
        });
    }
    snapshot.include_models(head, draws).unwrap();
    assert_eq!(snapshot.resolve(2, head), Ok(Some(front)));
    assert!(
        snapshot
            .resolve(2, forge_core::bytes_sha256(b"stale"))
            .is_err()
    );
    let pass = render::Pass::spawn(
        app.world_mut(),
        snapshot,
        render::View {
            width: 128,
            height: 128,
            transform: Transform::from_xyz(0.0, 0.5, 3.0).looking_at(Vec3::Y * 0.5, Vec3::Y),
            projection: Projection::Perspective(PerspectiveProjection::default()),
        },
    )
    .unwrap();
    app.finish();
    app.cleanup();
    let adapter = app
        .sub_app(bevy::render::RenderApp)
        .world()
        .resource::<bevy::render::renderer::RenderAdapterInfo>();
    assert_ne!(adapter.device_type, wgpu::DeviceType::Cpu);
    eprintln!(
        "replacement picking GPU: {}; {:?}; {:?}",
        adapter.name, adapter.backend, adapter.device_type
    );
    let started = Instant::now();
    let opaque = loop {
        let value = coverage(&mut app, pass.target.clone());
        if value.front > 100 && value.back > 50 {
            break value;
        }
        assert!(
            started.elapsed().as_secs() < 30,
            "Picking pipeline did not render: {value:?}"
        );
    };
    let front_handle = {
        let mut materials = app
            .world_mut()
            .resource_mut::<Assets<VisualValleyVegetationMaterial>>();
        let (id, material) = materials
            .iter_mut()
            .find(|(_, m)| m.base.base_color_texture.is_some())
            .unwrap();
        material.base.alpha_mode = AlphaMode::Mask(0.5);
        id
    };
    advance(&mut app, 8);
    let masked = coverage(&mut app, pass.target.clone());
    assert!(
        masked.front > opaque.front / 5 && masked.front < opaque.front * 4 / 5,
        "{opaque:?} {masked:?}"
    );
    assert!(
        masked.back > opaque.back,
        "Alpha holes must reveal the back canonical identity"
    );
    app.world_mut()
        .resource_mut::<Assets<VisualValleyVegetationMaterial>>()
        .get_mut(front_handle)
        .unwrap()
        .extension
        .wind_parameters
        .x = 0.8;
    advance(&mut app, 8);
    let wind = coverage(&mut app, pass.target.clone());
    assert_ne!(wind.hash, masked.hash);
    assert!(wind.front > 50);
    assert_ne!(wind.x_sum / wind.front, masked.x_sum / masked.front);
    eprintln!("replacement picking coverage: opaque={opaque:?}; mask={masked:?}; wind={wind:?}");
    let camera = pass.camera;
    pass.retire(app.world_mut());
    assert!(app.world().get_entity(camera).is_err());
    advance(&mut app, 2);
}
