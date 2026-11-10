//! Exercise the shared placement, Rapier registration and Studio draw check on private,
//! SHA-pinned static carriers. No UI, map edits, or Vault publication.
#[path = "../../../apps/forge_studio/src/authoring_assets/source_graph_draw_probe.rs"]
#[allow(
    dead_code,
    unused_imports,
    reason = "Shares the exact Studio probe with the headless example"
)]
mod draw;

use bevy::{
    app::PluginsState,
    prelude::*,
    render::{
        RenderApp, RenderPlugin,
        pipelined_rendering::PipelinedRenderingPlugin,
        render_resource::{CachedPipelineState, PipelineCache, PipelineDescriptor},
        settings::{Backends, RenderCreation, WgpuFeatures, WgpuSettings},
    },
    window::ExitCondition,
    winit::WinitPlugin,
};
use bevy_rapier3d::prelude::{NoUserData, RapierPhysicsPlugin};
use forge_engine::{
    ManualPlacementCollisionPlugin, ManualPlacementCollisionStatus, ManualPlacementProjection,
    source_graph::{SourceGraphMaterial, SourceGraphPlugin},
    source_graph_placement::{ManualAdmission, Status, admit_manual, submit},
    source_graph_runtime::{
        SourceClock, SourceFrameStatus, SourceSceneResidents, SourceSceneRuntimePlugin,
    },
};
use forge_project::zone_authoring::placements::ManualPlacement;
use serde::Deserialize;
use std::{
    collections::BTreeSet,
    path::PathBuf,
    sync::Arc,
    time::{Duration, Instant},
};

fn pipeline_diagnostics(app: &App, parts: &[Entity]) -> String {
    use bevy::asset::uuid::Uuid;
    let world = app.world();
    let materials = world.resource::<Assets<SourceGraphMaterial>>();
    let shaders: BTreeSet<_> = parts
        .iter()
        .filter_map(|entity| world.get::<MeshMaterial3d<SourceGraphMaterial>>(*entity))
        .filter_map(|material| materials.get(&material.0))
        .flat_map(|material| {
            let k = material.extension.program_key;
            [k.vertex, k.fragment, k.prepass_vertex, k.prepass_fragment]
                .into_iter()
                .map(|id| AssetId::<Shader>::Uuid {
                    uuid: Uuid::from_u128(id),
                })
        })
        .collect();
    let Some(render) = app.get_sub_app(RenderApp) else {
        return "render app absent".into();
    };
    let cache = render.world().resource::<PipelineCache>();
    let programs: Vec<_> = cache
        .pipelines()
        .filter_map(|pipeline| {
            let PipelineDescriptor::RenderPipelineDescriptor(desc) = &pipeline.descriptor else {
                return None;
            };
            if !shaders.contains(&desc.vertex.shader.id())
                && !desc
                    .fragment
                    .as_ref()
                    .is_some_and(|f| shaders.contains(&f.shader.id()))
            {
                return None;
            }
            let state = match &pipeline.state {
                CachedPipelineState::Ok(_) => "ready".to_owned(),
                CachedPipelineState::Err(err) => format!("error: {err}"),
                _ => "pending".to_owned(),
            };
            Some(format!("{:?}: {state}", desc.label))
        })
        .collect();
    format!("source pipelines={programs:?}")
}

#[derive(Deserialize)]
struct Manifest {
    models: Vec<Model>,
}

#[derive(Deserialize)]
struct Model {
    id: String,
    path: PathBuf,
    sha256: String,
}

fn check(
    app: &mut App,
    model: &Model,
    receiver_probe: bool,
    decals: &[Model],
) -> Result<usize, String> {
    let bytes = std::fs::read(&model.path).map_err(|error| error.to_string())?;
    let digest = forge_core::bytes_sha256(&bytes);
    if digest.to_string() != model.sha256 {
        return Err("Carrier SHA-256 differs".into());
    }
    let placement = ManualPlacement {
        id: forge_core::EntityId::deterministic(format!("source-static-probe/v1:{}", model.id)),
        name: model.id.clone(),
        asset_id: forge_assets::governed_import::vault_asset_id(&model.id),
        asset_hash: digest,
        transform: forge_core::Transform::from_translation(forge_core::Vec3::new(7., 0.25, -3.)),
        physics: None,
        particles: None,
        local_lights: None,
        locked: false,
    };
    let pose = forge_engine::placement_transform(&placement);
    let root = app
        .world_mut()
        .spawn((
            pose,
            GlobalTransform::from(pose),
            Visibility::Hidden,
            ManualPlacementProjection(placement),
        ))
        .id();
    submit(
        &mut app.world_mut().commands(),
        root,
        Arc::from(bytes),
        None,
        None,
        31,
    );
    app.world_mut().flush();
    let result = (|| {
        let until = Instant::now() + Duration::from_secs(45);
        let (mut probe, parts, registered) = loop {
            app.update();
            if let Some(Status::Refused(error)) = app.world().get::<Status>(root) {
                return Err(error.clone());
            }
            if let Some(SourceFrameStatus::Refused(error)) =
                app.world().get::<SourceFrameStatus>(root)
            {
                return Err(error.clone());
            }
            if Instant::now() >= until {
                return Err(format!(
                    "Source placement not ready: {:?}, collision {:?}",
                    app.world().get::<Status>(root),
                    app.world().get::<ManualPlacementCollisionStatus>(root)
                ));
            }
            if !matches!(
                admit_manual(app.world_mut(), root)?,
                ManualAdmission::Attached
            ) {
                continue;
            }
            let registered = match app.world().get::<ManualPlacementCollisionStatus>(root) {
                Some(ManualPlacementCollisionStatus::Registered { primitives })
                    if *primitives > 0 =>
                {
                    *primitives
                }
                Some(ManualPlacementCollisionStatus::Failed(error)) => return Err((*error).into()),
                _ => continue,
            };
            let parts = app
                .world()
                .resource::<SourceSceneResidents>()
                .static_primitives_for_carrier(root, digest)
                .ok_or("No static primitives for pinned carrier")?
                .to_vec();
            if let Some(probe) =
                draw::Probe::start(app.world_mut(), root, &parts, draw::PLACEMENT_LAYER)?
            {
                break (probe, parts, registered);
            }
        };
        let result = loop {
            app.update();
            if let Some(SourceFrameStatus::Refused(error)) =
                app.world().get::<SourceFrameStatus>(root)
            {
                break Err(error.clone());
            }
            match probe.poll(app.world_mut()) {
                draw::Outcome::Drawn => {
                    break if receiver_probe {
                        verify_receiver(app, &probe, &parts)
                            .and_then(|()| {
                                if decals.is_empty() {
                                    Ok(())
                                } else {
                                    verify_placed_decals(app, &probe, &parts, decals)
                                }
                            })
                            .map(|()| registered)
                    } else {
                        Ok(registered)
                    };
                }
                draw::Outcome::Empty => {
                    break Err(format!(
                        "No stable offscreen draw at either angle: {}; {}",
                        probe.diagnostics(app.world()),
                        pipeline_diagnostics(app, &parts)
                    ));
                }
                draw::Outcome::Pending if Instant::now() >= until => {
                    break Err("Offscreen readback timeout".into());
                }
                draw::Outcome::Pending => {}
            }
        };
        probe.finish(app.world_mut());
        result
    })();
    app.world_mut().despawn(root);
    app.update();
    result
}

fn verify_receiver(app: &mut App, probe: &draw::Probe, parts: &[Entity]) -> Result<(), String> {
    use forge_engine::source_decal_view::SourceDecalView;
    let (baseline, width) = probe
        .stable_pixels(app.world())
        .ok_or("Missing original receiver readback")?;
    let baseline = baseline.to_vec();
    let world = app.world_mut();
    let handles: BTreeSet<_> = parts
        .iter()
        .filter_map(|&part| world.get::<MeshMaterial3d<SourceGraphMaterial>>(part))
        .map(|m| m.0.id())
        .collect();
    if handles.is_empty() {
        return Err("No source graph receivers".into());
    }
    let view = SourceDecalView::new(
        &mut world.resource_mut::<Assets<Image>>(),
        UVec2::splat(width),
    )?;
    let [color, normal] = view.targets();
    // Known non-neutral attachments, isolated from scene projection. The
    // source_decal_view_probe separately exercises the actual Witch House draw.
    {
        let mut images = world.resource_mut::<Assets<Image>>();
        images.get_mut(&color).unwrap().data =
            Some([36, 0, 0, 92].repeat((width * width) as usize));
        images.get_mut(&normal).unwrap().data =
            Some([153, 142, 164, 110].repeat((width * width) as usize));
    }
    {
        let mut materials = world.resource_mut::<Assets<SourceGraphMaterial>>();
        if !handles.iter().copied().all(|id| {
            materials
                .get_mut(id)
                .is_some_and(|mut m| view.bind_graph_receiver(&mut m))
        }) {
            return Err("Witch House graph receiver bindings refused".into());
        }
    }
    let wait = |app: &mut App, active: bool| {
        let deadline = Instant::now() + Duration::from_secs(35);
        while Instant::now() < deadline {
            app.update();
            let Some((pixels, _)) = probe.stable_pixels(app.world()) else {
                continue;
            };
            if !active && pixels == baseline {
                return Ok(());
            }
            if active && pixels != baseline {
                let changed = pixels
                    .chunks_exact(4)
                    .zip(baseline.chunks_exact(4))
                    .filter(|(new, old)| new != old)
                    .count();
                let coverage_matches = pixels
                    .chunks_exact(4)
                    .zip(baseline.chunks_exact(4))
                    .all(|(new, old)| new[3] == old[3] && (old[3] != 0 || new == old));
                if changed > 0 && coverage_matches {
                    eprintln!(
                        "Graph receiver composited {changed} pixels; original alpha preserved"
                    );
                    return Ok(());
                }
            }
        }
        Err(format!(
            "Graph receiver readback timed out, active={active}; {}",
            pipeline_diagnostics(app, parts)
        ))
    };
    let changed = wait(app, true);
    {
        let mut materials = app
            .world_mut()
            .resource_mut::<Assets<SourceGraphMaterial>>();
        for id in handles {
            materials.get_mut(id).unwrap().extension.unbind_decal_view();
        }
    }
    changed?;
    wait(app, false)
}

fn verify_placed_decals(
    app: &mut App,
    probe: &draw::Probe,
    parts: &[Entity],
    decals: &[Model],
) -> Result<(), String> {
    use bevy::render::renderer::{RenderDevice, RenderQueue};
    use forge_engine::source_decal_view::{
        SourceDecalRaster, SourceDecalView, SourceDecalViewStatus,
    };
    let (baseline, width) = probe
        .stable_pixels(app.world())
        .ok_or("No neutral receiver pixels")?;
    let baseline = baseline.to_vec();
    let world = app.world_mut();
    let camera = world
        .query_filtered::<Entity, With<forge_engine::scene_camera::OffscreenSceneCamera>>()
        .single(world)
        .map_err(|e| e.to_string())?;
    let view = SourceDecalView::new(
        &mut world.resource_mut::<Assets<Image>>(),
        UVec2::splat(width),
    )?;
    let materials: BTreeSet<_> = parts
        .iter()
        .filter_map(|&part| world.get::<MeshMaterial3d<SourceGraphMaterial>>(part))
        .map(|m| m.0.id())
        .collect();
    let mut assets = world.resource_mut::<Assets<SourceGraphMaterial>>();
    for &id in &materials {
        if !assets
            .get_mut(id)
            .is_some_and(|mut m| view.bind_graph_receiver(&mut m))
        {
            return Err("Refused graph receiver for placed decal".into());
        }
    }
    world.entity_mut(camera).insert(view);
    let (pivot, eye) = probe.framing();
    let direction = (eye - pivot).normalize();
    let wait = |app: &mut App, count: usize| {
        let start = app.world().resource::<bevy::diagnostic::FrameCount>().0;
        let deadline = Instant::now() + Duration::from_secs(20);
        while Instant::now() < deadline {
            app.update();
            let status = app.world().get::<SourceDecalView>(camera).unwrap().status();
            if let SourceDecalViewStatus::Refused(reason) = status {
                return Err(reason);
            }
            let age = app
                .world()
                .resource::<bevy::diagnostic::FrameCount>()
                .0
                .wrapping_sub(start);
            if age < 12 || status != SourceDecalViewStatus::Encoded(count) {
                continue;
            }
            let Some((pixels, _)) = probe.stable_pixels(app.world()) else {
                continue;
            };
            if count == 0 && pixels == baseline {
                return Ok(0);
            }
            if count != 0 && pixels != baseline {
                let changes = pixels.chunks_exact(4).zip(baseline.chunks_exact(4));
                if changes
                    .clone()
                    .all(|(new, old)| new[3] == old[3] && (old[3] != 0 || new == old))
                {
                    return Ok(changes.filter(|(new, old)| new != old).count());
                }
            }
        }
        Err(format!(
            "No stable placed-decal readback for {count} draws; {}",
            pipeline_diagnostics(app, parts)
        ))
    };
    wait(app, 0)?;
    for decal in decals {
        let bytes = std::fs::read(&decal.path).map_err(|err| err.to_string())?;
        if forge_core::bytes_sha256(&bytes).to_string() != decal.sha256 {
            return Err("Decal SHA-256 differs".into());
        }
        let raster = bevy::tasks::block_on(SourceDecalRaster::prepare(
            app.world().resource::<RenderDevice>(),
            app.world().resource::<RenderQueue>(),
            &bytes,
        ))?;
        let (center, normal, radius) = raster.preview_frame()?;
        let rotation = Quat::from_rotation_arc(normal, direction);
        let scale = (eye - pivot).length() * 0.2 / radius;
        let position = pivot.lerp(eye, 0.38) - rotation * (center * scale);
        let pose = Mat4::from_scale_rotation_translation(Vec3::splat(scale), rotation, position);
        app.world_mut()
            .get_mut::<SourceDecalView>(camera)
            .unwrap()
            .set_draws(vec![(raster, pose)])?;
        let changed = wait(app, 1)?;
        println!(
            "{}",
            serde_json::json!({"decal":decal.id,"sha256":decal.sha256,"changed_receiver_pixels":changed})
        );
        app.world_mut()
            .get_mut::<SourceDecalView>(camera)
            .unwrap()
            .set_draws(Vec::new())?;
        wait(app, 0)?;
    }
    let mut assets = app
        .world_mut()
        .resource_mut::<Assets<SourceGraphMaterial>>();
    for id in materials {
        assets.get_mut(id).unwrap().extension.unbind_decal_view();
    }
    Ok(())
}

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let path = std::env::args_os()
        .nth(1)
        .ok_or("Expected pinned manifest path")?;
    let manifest: Manifest = serde_json::from_slice(&std::fs::read(path)?)?;
    let receiver_probe = std::env::args_os()
        .nth(2)
        .is_some_and(|arg| arg == "--decal-receiver");
    let decals: Vec<Model> = if let Some(path) = std::env::args_os().nth(3) {
        let input: Manifest = serde_json::from_slice(&std::fs::read(path)?)?;
        if !receiver_probe
            || manifest.models.is_empty()
            || input.models.is_empty()
            || input.models.len() % manifest.models.len() != 0
        {
            return Err("Decal fixtures must divide evenly among the pinned receivers".into());
        }
        input.models
    } else {
        Vec::new()
    };
    let per_receiver = decals.len() / manifest.models.len().max(1);
    let mut app = App::new();
    app.add_plugins(
        DefaultPlugins
            .set(WindowPlugin {
                primary_window: None,
                exit_condition: ExitCondition::DontExit,
                ..default()
            })
            .set(RenderPlugin {
                render_creation: RenderCreation::Automatic(Box::new(WgpuSettings {
                    backends: Some(Backends::DX12),
                    features: WgpuFeatures::TEXTURE_COMPRESSION_BC,
                    ..default()
                })),
                ..default()
            })
            .disable::<WinitPlugin>()
            .disable::<PipelinedRenderingPlugin>(),
    )
    .add_plugins((
        SourceGraphPlugin,
        SourceSceneRuntimePlugin {
            capture_bevy_clock: false,
            ..default()
        },
        RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule(),
        ManualPlacementCollisionPlugin,
    ));
    if !decals.is_empty() {
        app.add_plugins(forge_engine::source_decal_view::SourceDecalViewPlugin);
    } else if receiver_probe {
        app.add_plugins(forge_engine::source_decal_receiver::SourceDecalReceiverPlugin);
    }
    let until = Instant::now() + Duration::from_secs(30);
    while app.plugins_state() != PluginsState::Ready {
        if Instant::now() >= until {
            return Err("Render device initialization timeout".into());
        }
        std::thread::sleep(Duration::from_millis(10));
    }
    app.finish();
    app.cleanup();
    app.world_mut().insert_resource(SourceClock::default());
    let mut failures = 0;
    for (index, model) in manifest.models.into_iter().enumerate() {
        let result = check(
            &mut app,
            &model,
            receiver_probe,
            &decals[index * per_receiver..(index + 1) * per_receiver],
        );
        failures += usize::from(result.is_err());
        println!(
            "{}",
            serde_json::json!({
                "asset_id": model.id, "sha256": model.sha256,
                "drawn": result.is_ok(), "collision_registered": result.is_ok(), "result": result,
                "decal_receiver_probe": receiver_probe,
            })
        );
    }
    if failures > 0 {
        return Err(format!("{failures} source carrier(s) refused").into());
    }
    Ok(())
}
