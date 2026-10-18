//! Opt-in moving-camera regression with actual mesh/material residency and native captures.
use bevy::{
    asset::AssetId,
    diagnostic::FrameCount,
    input::{InputSystems, mouse::MouseMotion},
    prelude::*,
    render::{
        Render, RenderApp, RenderSystems,
        erased_render_asset::ErasedRenderAssets,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        mesh::RenderMesh,
        render_asset::RenderAssets,
        view::screenshot::{Screenshot, save_to_disk},
    },
};
use std::sync::{Arc, Mutex};
use {
    forge_engine::{ForgePlayerCamera, PlayerInputFocus, PlayerSimulationReady},
    forge_tidewater::tidewater_terrain::{lod::LodMaterial, scene::ResidentTerrain},
};
#[path = "context_proof.rs"]
mod context_proof;
#[derive(Resource, Clone, Default, ExtractResource)]
struct Sample {
    entity: Option<Entity>,
    mesh: Option<AssetId<Mesh>>,
    ocean_mesh: Option<AssetId<Mesh>>,
    material: Option<AssetId<LodMaterial>>,
    frame: u32,
    tiles: Vec<(Entity, AssetId<Mesh>)>,
    records: Arc<Mutex<Vec<serde_json::Value>>>,
}
#[derive(Resource, Default)]
struct Proof {
    frames: u32,
    captures: u32,
    moved: u32,
    previous: Option<Transform>,
    initial_position: Option<Vec3>,
    max_translation: f32,
    look_initial_position: Option<Vec3>,
    max_look_translation: f32,
}
pub(super) fn requested() -> bool {
    std::env::var("FORGE_TIDEWATER_TERRAIN_PROOF").as_deref() == Ok("1")
}
pub(super) fn install(app: &mut App) {
    app.insert_resource(bevy::winit::WinitSettings::continuous())
        .init_resource::<Proof>()
        .init_resource::<Sample>()
        .add_plugins(ExtractResourcePlugin::<Sample>::default())
        .add_systems(PreUpdate, drive.before(InputSystems))
        .add_systems(
            PostUpdate,
            observe.after(bevy::camera::visibility::VisibilitySystems::CheckVisibility),
        );
    context_proof::install(app);
    app.sub_app_mut(RenderApp).add_systems(
        Render,
        residency
            .after(RenderSystems::QueueMeshes)
            .before(RenderSystems::PhaseSort),
    );
}
fn drive(
    ready: Res<PlayerSimulationReady>,
    proof: Res<Proof>,
    mut motion: MessageWriter<MouseMotion>,
    mut focus: ResMut<PlayerInputFocus>,
) {
    // Exercise native mouse accumulation and the real input sampler, with no
    // keyboard movement or operating-system input injection.
    focus.0 = true;
    if !ready.0 || proof.frames < 25 || proof.frames > 245 {
        return;
    }
    motion.write(MouseMotion {
        delta: Vec2::new(
            if (proof.frames / 25).is_multiple_of(2) {
                18.0
            } else {
                -18.0
            },
            0.0,
        ),
    });
}
fn residency(
    sample: Res<Sample>,
    meshes: Res<RenderAssets<RenderMesh>>,
    materials: Res<ErasedRenderAssets<bevy::pbr::PreparedMaterial>>,
    buffers: Res<RenderAssets<bevy::render::storage::GpuShaderBuffer>>,
    instances: Res<bevy::pbr::RenderMeshInstances>,
    pipelines: Res<bevy::pbr::SpecializedMaterialPipelineCache>,
    cache: Res<bevy::render::render_resource::PipelineCache>,
) {
    if sample.frame == 0 {
        return;
    }
    let mesh_ready = sample.mesh.is_some_and(|id| meshes.get(id).is_some());
    let material_ready = sample
        .material
        .is_some_and(|id| materials.get(id.untyped()).is_some());
    let ocean_ready = sample.ocean_mesh.is_some_and(|id| meshes.get(id).is_some());
    let buffer = buffers.get(forge_tidewater::tidewater_material::scene_buffer::HANDLE.id());
    let entity = sample
        .entity
        .map(bevy::render::sync_world::MainEntity::from);
    let extracted = entity.and_then(|entity| instances.mesh_asset_id(entity));
    let pipeline_ready = entity.is_some_and(|entity| {
        pipelines.values().any(|view| {
            view.get(&entity)
                .is_some_and(|id| cache.get_render_pipeline(*id).is_some())
        })
    });
    let all_tiles_ready = sample.tiles.iter().all(|(entity, id)| {
        let entity = bevy::render::sync_world::MainEntity::from(*entity);
        meshes.get(*id).is_some()
            && instances.mesh_asset_id(entity) == Some(*id)
            && pipelines.values().any(|view| {
                view.get(&entity)
                    .is_some_and(|id| cache.get_render_pipeline(*id).is_some())
            })
    });
    sample.records.lock().expect("residency samples").push(serde_json::json!({"frame":sample.frame,"mesh":format!("{:?}",sample.mesh),"mesh_ready":mesh_ready,"ocean_ready":ocean_ready,"extracted_mesh":format!("{extracted:?}"),"mesh_matches":extracted==sample.mesh,"pipeline_ready":pipeline_ready,"material_ready":material_ready,"all_tiles_ready":all_tiles_ready,"tile_count":sample.tiles.len(),"scene_buffer":buffer.map(|b|format!("{:?}",b.buffer.id()))}));
}
fn observe(world: &mut World) {
    if !world.resource::<PlayerSimulationReady>().0 || world.resource::<FrameCount>().0 < 180 {
        return;
    }
    world.resource_scope(|world,mut proof:Mut<Proof>|{
  proof.frames+=1;
  if std::env::var("FORGE_TIDEWATER_TERRAIN_GRAPHICS").as_deref()==Ok("1") && proof.frames.is_multiple_of(20) {
   super::graphics::terrain_transition(world,((proof.frames/20)-1) as usize);
  }
  if std::env::var("FORGE_TIDEWATER_TERRAIN_CLOCK").as_deref()==Ok("1") && proof.frames.is_multiple_of(60) {
   world.resource_mut::<super::State>().preferences.minutes=[570,480,720,1080][((proof.frames/60)%4) as usize];
  }
  let terrain=world.resource::<ResidentTerrain>();let entity=terrain.entity.expect("terrain");let material=terrain.material.as_ref().map(Handle::id);
  let mesh=world.get::<Mesh3d>(entity).map(|m|m.0.id());
  let ocean_mesh=world.query_filtered::<&Mesh3d,With<crate::ocean_geometry::OceanSurface>>().single(world).ok().map(|m|m.0.id());
  let transform=*world.query_filtered::<&Transform,With<ForgePlayerCamera>>().single(world).expect("camera");
  if proof.previous.is_some_and(|p|p.rotation != transform.rotation){proof.moved+=1;}
  let initial=*proof.initial_position.get_or_insert(transform.translation);
  proof.max_translation=proof.max_translation.max(initial.distance(transform.translation));
  // The first 24 samples include the spawn's gravity settling. Measure the
  // stationary witness from the start of injected look input, keeping both metrics.
  if proof.frames>=25 {
   let initial=*proof.look_initial_position.get_or_insert(transform.translation);
   proof.max_look_translation=proof.max_look_translation.max(initial.distance(transform.translation));
  }
  proof.previous=Some(transform);
  let tiles=super::super::tile_entities(world).into_iter().filter_map(|entity| world.get::<Mesh3d>(entity).map(|mesh|(entity,mesh.0.id()))).collect();
  {let mut sample=world.resource_mut::<Sample>();sample.frame=proof.frames;sample.mesh=mesh;sample.ocean_mesh=ocean_mesh;sample.material=material;sample.entity=Some(entity);sample.tiles=tiles;}
  let path=world.resource::<crate::Probe>().screenshot.clone();
  if (30..90).contains(&proof.frames) || (90..270).contains(&proof.frames) && proof.frames.is_multiple_of(8) {
   let target=path.with_file_name(format!("terrain-{:03}.png",proof.frames));
   world.spawn(Screenshot::primary_window()).observe(save_to_disk(target));proof.captures+=1;
  }
  if proof.frames==300{
   let samples=world.resource::<Sample>().records.lock().expect("records").clone();
   let ready=samples.iter().all(|s| ["mesh_ready","ocean_ready","material_ready","mesh_matches","pipeline_ready","all_tiles_ready"].iter().all(|k|s[k].as_bool()==Some(true)));
   let receipt=serde_json::json!({"pass":proof.moved>100&&proof.max_look_translation<0.05&&ready,"moving_frames":proof.moved,"captures":proof.captures,"samples":samples,"input":"native MouseMotion, stationary yaw","camera":transform.translation.to_array(),"max_translation_m":proof.max_translation,"max_look_translation_m":proof.max_look_translation,"all_render_samples_ready":ready,"graphics":world.resource::<super::graphics::GraphicsState>().values_receipt(),"gate3_closed":false});
   std::fs::write(path.with_extension("terrain-proof.json"),serde_json::to_vec_pretty(&receipt).expect("receipt")).expect("proof output");
   world.write_message(bevy::app::AppExit::Success);
  }
 });
}
