//! Opt-in native camera orbit and GPU allocation witness for scanned ground debris.
use bevy::{
    asset::{AssetId, UntypedAssetId},
    diagnostic::FrameCount,
    mesh::MeshTag,
    pbr::{MaterialBindGroupAllocators, PreparedMaterial},
    prelude::*,
    render::{
        Render, RenderApp, RenderSystems,
        erased_render_asset::ErasedRenderAssets,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        render_asset::RenderAssets,
        storage::{GpuShaderBuffer, ShaderBuffer},
        view::screenshot::{Screenshot, save_to_disk},
    },
};
use forge_engine::{
    ForgePlayerCamera, PlayerInputFocus, PlayerSimulationReady,
    tidewater_debris::scanned_material::ScannedMaterial,
};
use std::{
    any::TypeId,
    sync::{Arc, Mutex},
};

#[derive(Resource, Default)]
struct Proof {
    frame: u32,
}
#[derive(Resource, Clone, Default, ExtractResource)]
struct Sample {
    frame: u32,
    buffer: Option<AssetId<ShaderBuffer>>,
    material: Option<UntypedAssetId>,
    draws: usize,
    records: Arc<Mutex<Vec<serde_json::Value>>>,
}
pub(super) fn requested() -> bool {
    std::env::var("FORGE_TIDEWATER_LOGS_PROOF").as_deref() == Ok("1")
}
pub(super) fn install(app: &mut App) {
    app.insert_resource(bevy::winit::WinitSettings::continuous())
        .init_resource::<Proof>()
        .init_resource::<Sample>()
        .add_plugins(ExtractResourcePlugin::<Sample>::default())
        .add_systems(
            PostUpdate,
            drive.before(bevy::transform::TransformSystems::Propagate),
        )
        .add_systems(Last, observe);
    app.sub_app_mut(RenderApp).add_systems(
        Render,
        resident
            .after(RenderSystems::PrepareBindGroups)
            .before(RenderSystems::Render),
    );
}
fn pose(frame: u32) -> Transform {
    let view = (frame / 40) % 12;
    let target = if view < 8 {
        Vec3::new(45.5398, 2.66864, -77.41449)
    } else if view < 10 {
        Vec3::new(49.59997, 2.13143, -72.33461)
    } else {
        Vec3::new(62.11073, 2.02036, -71.56126)
    };
    #[allow(clippy::cast_precision_loss)]
    let angle = (view % 8) as f32 * std::f32::consts::FRAC_PI_4;
    let radius = if view < 8 { 5.0 } else { 2.0 };
    Transform::from_translation(target + Vec3::new(angle.cos() * radius, 1.6, angle.sin() * radius))
        .looking_at(target, Vec3::Y)
}
fn drive(
    ready: Res<PlayerSimulationReady>,
    frame: Res<FrameCount>,
    mut proof: ResMut<Proof>,
    mut focus: ResMut<PlayerInputFocus>,
    mut camera: Query<&mut Transform, With<ForgePlayerCamera>>,
) {
    if !ready.0 || frame.0 < 240 {
        return;
    }
    focus.0 = false;
    if let Ok(mut camera) = camera.single_mut() {
        *camera = pose(proof.frame);
        proof.frame += 1;
    }
}
fn resident(
    sample: Res<Sample>,
    buffers: Res<RenderAssets<GpuShaderBuffer>>,
    materials: Res<ErasedRenderAssets<PreparedMaterial>>,
    allocators: Res<MaterialBindGroupAllocators>,
) {
    if sample.frame == 0 {
        return;
    }
    let buffer = sample.buffer.and_then(|id| buffers.get(id));
    let material = sample.material.and_then(|id| materials.get(id));
    let group = material.and_then(|m| {
        allocators
            .get(&TypeId::of::<ScannedMaterial>())
            .and_then(|a| a.get(m.binding.group))
            .and_then(|s| s.bind_group())
    });
    sample
        .records
        .lock()
        .expect("logs render records")
        .push(serde_json::json!({
            "frame":sample.frame,"draws":sample.draws,
            "buffer":buffer.map(|b|format!("{:?}",b.buffer.id())),
            "bytes":buffer.map(|b|b.buffer_descriptor.size),
            "bind_group":group.map(|g|format!("{:?}",g.id())),
        }));
}
fn observe(world: &mut World) {
    let frame = world.resource::<Proof>().frame;
    if frame == 0 {
        return;
    }
    let draws = world
        .query::<(&MeshMaterial3d<ScannedMaterial>, &MeshTag)>()
        .iter(world)
        .map(|(m, t)| (m.0.id(), t.0))
        .collect::<Vec<_>>();
    let material = draws.first().map(|(id, _)| *id);
    let buffer = material
        .and_then(|id| world.resource::<Assets<ScannedMaterial>>().get(id))
        .map(|m| m.extension.instances.id());
    {
        let mut sample = world.resource_mut::<Sample>();
        sample.frame = frame;
        sample.buffer = buffer;
        sample.material = material.map(AssetId::untyped);
        sample.draws = draws.len();
    }
    let path = world.resource::<crate::Probe>().screenshot.clone();
    if frame % 40 == 30 {
        world
            .spawn(Screenshot::primary_window())
            .observe(save_to_disk(
                path.with_file_name(format!("logs-{:02}.png", frame / 40)),
            ));
    }
    if frame == 980 {
        let records = world
            .resource::<Sample>()
            .records
            .lock()
            .expect("logs records")
            .clone();
        let settled = records
            .iter()
            .filter(|r| r["frame"].as_u64().unwrap_or(0) > 80)
            .collect::<Vec<_>>();
        let stale = settled
            .windows(2)
            .filter(|pair| {
                pair[0]["buffer"] != pair[1]["buffer"]
                    && pair[0]["bind_group"] == pair[1]["bind_group"]
            })
            .count();
        let ready = settled
            .iter()
            .all(|r| !r["buffer"].is_null() && !r["bind_group"].is_null());
        let buffer_ids = settled
            .iter()
            .map(|r| r["buffer"].to_string())
            .collect::<std::collections::BTreeSet<_>>();
        let receipt = serde_json::json!({"pass":ready&&stale==0,"stale_binding_transitions":stale,
            "gpu_buffer_allocations":buffer_ids.len(),"captures":24,"samples":records,
            "camera":"12 poses repeated: 8 angles around log asset 0; two angles around assets 1 and 2",
            "scope":"Local visual and allocation regression; no whole Gate qualification","gate3_closed":false});
        std::fs::write(
            path.with_extension("logs-proof.json"),
            serde_json::to_vec_pretty(&receipt).expect("logs receipt"),
        )
        .expect("logs output");
        world.write_message(bevy::app::AppExit::Success);
    }
}
