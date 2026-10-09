use bevy::{app::PluginsState,prelude::*,render::{RenderApp,RenderPlugin,pipelined_rendering::PipelinedRenderingPlugin,renderer::{RenderDevice,RenderAdapterInfo},settings::{RenderCreation,WgpuSettings,Backends},render_resource::*},window::ExitCondition,winit::WinitPlugin};
use forge_engine::source_graph::SourceGraphMaterial;
use std::{time::{Instant,Duration},path::Path};
fn counts(entries:&[BindGroupLayoutEntry],stage:ShaderStages)->serde_json::Value {
 let textures=entries.iter().filter(|e|e.visibility.contains(stage)&&matches!(e.ty,BindingType::Texture{..})).count();
 let samplers=entries.iter().filter(|e|e.visibility.contains(stage)&&matches!(e.ty,BindingType::Sampler(_))).count();
 serde_json::json!({"sampled_textures":textures,"samplers":samplers})
}
fn main()->Result<(),Box<dyn std::error::Error>> {
 let args:Vec<_>=std::env::args().collect();let root=Path::new(&args[1]);
 let mut app=App::new();
 app.add_plugins(DefaultPlugins.set(WindowPlugin{primary_window:None,exit_condition:ExitCondition::DontExit,..default()}).set(RenderPlugin{render_creation:RenderCreation::Automatic(Box::new(WgpuSettings{backends:Some(Backends::DX12),force_fallback_adapter:false,..default()})),..default()}).disable::<WinitPlugin>().disable::<PipelinedRenderingPlugin>());
 let deadline=Instant::now()+Duration::from_secs(30);
 while app.plugins_state()!=PluginsState::Ready {if Instant::now()>deadline{return Err("GPU initialization timeout".into());}std::thread::sleep(Duration::from_millis(10));}
 app.finish();app.cleanup();
 let rw=app.get_sub_app(RenderApp).unwrap().world();let device=rw.resource::<RenderDevice>();let info=rw.resource::<RenderAdapterInfo>();
 let entries=SourceGraphMaterial::bind_group_layout_entries(device,true);
 let limits=device.wgpu_device().limits();
 let mut proposed=entries.clone();
 let texture=entries.iter().find(|e|e.binding==101).ok_or("Original texture entry absent")?.clone();
 let sampler=entries.iter().find(|e|e.binding==102).ok_or("Original sampler entry absent")?.clone();
 for (t,s) in [(115,116),(117,118),(125,126)] {
  assert!(!proposed.iter().any(|e|e.binding==t||e.binding==s));
  let mut tex=texture.clone();tex.binding=t;proposed.push(tex);
  let mut samp=sampler.clone();samp.binding=s;proposed.push(samp);
 }
 proposed.sort_by_key(|e|e.binding);
 let mut result=serde_json::json!({
  "status":"actual_common_layout_entries_inspected_creation_pending",
  "adapter":info.name,"backend":format!("{:?}",info.backend),
  "device_max_sampled_textures_per_stage":limits.max_sampled_textures_per_shader_stage,
  "device_max_samplers_per_stage":limits.max_samplers_per_shader_stage,
  "current_fragment":counts(&entries,ShaderStages::FRAGMENT),"proposed_fragment":counts(&proposed,ShaderStages::FRAGMENT),
  "current_vertex":counts(&entries,ShaderStages::VERTEX),"proposed_vertex":counts(&proposed,ShaderStages::VERTEX),
  "current_binding_ids":entries.iter().map(|e|e.binding).collect::<Vec<_>>(),
  "proposed_binding_ids":proposed.iter().map(|e|e.binding).collect::<Vec<_>>(),
  "baseline_layout_created":false,"proposed_layout_created":false,
  "actual_render_pipeline_created":false,"web_gpu_verified":false,
  "asset_render_qualified":false,"full_import":false
 });
 let path=root.join("NATIVE-RESULT.v1.json");
 std::fs::write(&path,serde_json::to_vec_pretty(&result)?)?;
 let _baseline=device.wgpu_device().create_bind_group_layout(&BindGroupLayoutDescriptor{label:Some("actual-existing-source-graph-layout"),entries:&entries});
 result["baseline_layout_created"]=true.into();std::fs::write(&path,serde_json::to_vec_pretty(&result)?)?;
 let _proposed=device.wgpu_device().create_bind_group_layout(&BindGroupLayoutDescriptor{label:Some("proposed-ten-source-textures-layout"),entries:&proposed});
 result["proposed_layout_created"]=true.into();result["status"]="actual_common_and_proposed_ten_texture_bind_group_layouts_created".into();
 std::fs::write(path,serde_json::to_vec_pretty(&result)?)?;Ok(())
}
