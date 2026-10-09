"""Exercise source pipelines privately; this does not admit production reveal or LOD."""
from pathlib import Path
import importlib.util,sys
BASE=Path(__file__).with_name('check_source_scene_actual_device_mount_v2.py')
spec=importlib.util.spec_from_file_location('actual_device_v2',BASE);previous=importlib.util.module_from_spec(spec);spec.loader.exec_module(previous)
PROBE=r'''
 // Diagnostic only: all admitted static primitive layouts are submitted.
 // This is explicitly not a source LOD decision or a product reveal policy.
 let mut source_shader_ids=std::collections::BTreeSet::new();
 for h in materials.values(){let k=app.world().resource::<Assets<SourceGraphMaterial>>().get(h).unwrap().extension.program_key;for uuid in [k.vertex,k.fragment,k.prepass_vertex,k.prepass_fragment]{source_shader_ids.insert(bevy::asset::AssetId::<Shader>::Uuid{uuid:bevy::asset::uuid::Uuid::from_u128(uuid)});}}
 let target=app.world_mut().resource_mut::<Assets<Image>>().add(Image::new_target_texture(128,128,bevy::render::render_resource::TextureFormat::Rgba8UnormSrgb,None));
 let camera=app.world_mut().spawn((Camera3d::default(),bevy::camera::RenderTarget::Image(target.into()),Msaa::Sample4,bevy::core_pipeline::prepass::DepthPrepass,bevy::core_pipeline::prepass::NormalPrepass,Transform::from_translation(offset+Vec3::new(4.,3.,5.)).looking_at(offset+Vec3::Y,Vec3::Y))).id();
 app.world_mut().entity_mut(root).insert(Visibility::Inherited);for draw in &draws{app.world_mut().entity_mut(*draw).insert(bevy::camera::visibility::NoFrustumCulling);}
 let deadline=Instant::now()+Duration::from_secs(45);let mut pipeline_rows=Vec::new();let mut frames=0;
 loop{
  app.update();frames+=1;pipeline_rows.clear();
  let cache=app.get_sub_app(RenderApp).unwrap().world().resource::<bevy::render::render_resource::PipelineCache>();
  for cached in cache.pipelines(){
   let bevy::render::render_resource::PipelineDescriptor::RenderPipelineDescriptor(d)=&cached.descriptor else{continue;};
   if !source_shader_ids.contains(&d.vertex.shader.id())&&!d.fragment.as_ref().is_some_and(|f|source_shader_ids.contains(&f.shader.id())){continue;}
   let (state,error)=match &cached.state{bevy::render::render_resource::CachedPipelineState::Ok(_)=>("ok",None),bevy::render::render_resource::CachedPipelineState::Err(e)=>("error",Some(e.to_string())),_=>("waiting",None)};
   pipeline_rows.push(serde_json::json!({"label":d.label,"vertex_shader":format!("{:?}",d.vertex.shader.id()),"fragment_shader":d.fragment.as_ref().map(|f|format!("{:?}",f.shader.id())),"vertex_defs":format!("{:?}",d.vertex.shader_defs),"fragment_defs":d.fragment.as_ref().map(|f|format!("{:?}",f.shader_defs)),"state":state,"error":error}));
  }
  if frames>=20&&!pipeline_rows.is_empty()&&pipeline_rows.iter().all(|r|r["state"]=="ok"){break;}
  if Instant::now()>=deadline{break;}
  std::thread::sleep(Duration::from_millis(15));
 }
 app.world_mut().entity_mut(root).insert(Visibility::Hidden);app.world_mut().despawn(camera);app.update();
 let passed=!pipeline_rows.is_empty()&&pipeline_rows.iter().all(|r|r["state"]=="ok");let proof=serde_json::json!({"status":if passed{"source_pipelines_created_on_actual_bevy_device"}else{"source_pipeline_probe_refused"},"frames":frames,"source_pipeline_count":pipeline_rows.len(),"source_pipelines":pipeline_rows,"all_source_pipelines_ready":passed,"diagnostic_all_static_layouts":true,"source_lod_selection_qualified":false,"production_reveal_changed":false,"draw_readback_qualified":false,"editor_visual_verified":false,"full_import":false});
 let path=std::path::Path::new(&args[2]).with_file_name("PIPELINE-RESULT.v1.json");use std::io::Write as _;let mut file=std::fs::OpenOptions::new().create_new(true).write(true).open(path)?;file.write_all(&serde_json::to_vec_pretty(&proof)?)?;
 if !passed{return Err("Source pipeline errors or missing specialization; see PIPELINE-RESULT.v1.json".into());}
'''
def main():
 text=previous.patched_code();text=text.replace("ROOT=W/'studio-source-actual-device-mount-v2'","ROOT=W/'studio-source-pipeline-probe-v1'").replace("name='openrpg-source-actual-device-mount-v2'","name='openrpg-source-pipeline-probe-v1'")
 needle='source_graph_runtime::retain(app.world_mut(),host,policy)?;app.update();';assert text.count(needle)==1;text=text.replace(needle,PROBE+'\n'+needle)
 needle='Path(__file__).resolve(),fixture,';assert text.count(needle)==1;text=text.replace(needle,'Path(__file__).resolve(),Path('+repr(str(BASE))+'),Path('+repr(str(previous.BASE))+'),fixture,')
 base=previous.base;exec(compile(text,str(previous.BASE),'exec'),base.__dict__);base.__file__=str(Path(__file__).resolve())
 base.prepare() if sys.argv[-1]=='prepare' else base.run()
if __name__=='__main__':main()
