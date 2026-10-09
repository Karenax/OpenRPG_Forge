"""Preserve Rust ranges and wait for the actual source pipelines before readback."""
from pathlib import Path
BASE_V2=Path(__file__).with_name('capture_firepit_forge_shadow_pair_v2.py')
source=BASE_V2.read_text('utf-8').replace('firepit-forge-shadow-pair-v2','firepit-forge-shadow-pair-v3')
before='(?<!['+chr(92)*2+'w])';after=before.replace('w]','w.]');assert source.count(before)==1;source=source.replace(before,after)
needle='exec(compile(source,str(BASE_GENERATOR),\'exec\'),globals())'
assert source.count(needle)==1
CHECK=r'''
 let mut shader_ids=std::collections::BTreeSet::new();for h in materials.values(){let k=app.world().resource::<Assets<SourceGraphMaterial>>().get(h).unwrap().extension.program_key;for id in [k.vertex,k.fragment,k.prepass_vertex,k.prepass_fragment]{shader_ids.insert(bevy::asset::AssetId::<Shader>::Uuid{uuid:bevy::asset::uuid::Uuid::from_u128(id)});}}
 let deadline=Instant::now()+Duration::from_secs(45);let mut observed_source_pipelines=Vec::new();
 loop{
  app.update();observed_source_pipelines.clear();let cache=app.get_sub_app(RenderApp).unwrap().world().resource::<bevy::render::render_resource::PipelineCache>();
  for p in cache.pipelines(){let bevy::render::render_resource::PipelineDescriptor::RenderPipelineDescriptor(d)=&p.descriptor else{continue;};if !shader_ids.contains(&d.vertex.shader.id())&&!d.fragment.as_ref().is_some_and(|f|shader_ids.contains(&f.shader.id())){continue;}
   let (state,error)=match &p.state{bevy::render::render_resource::CachedPipelineState::Ok(_)=>("ready",None),bevy::render::render_resource::CachedPipelineState::Err(e)=>("error",Some(e.to_string())),_=>("waiting",None)};
   observed_source_pipelines.push(serde_json::json!({"state":state,"error":error,"vertex_defs":format!("{:?}",d.vertex.shader_defs),"vertex_shader":format!("{:?}",d.vertex.shader.id())}));
  }
  if observed_source_pipelines.len()>=3&&observed_source_pipelines.iter().all(|p|p["state"]=="ready"){break;}
  if Instant::now()>deadline{return Err("Source forward/prepass/shadow pipelines not ready before readback".into());}std::thread::sleep(Duration::from_millis(15));
 }
'''
injection="source=source.replace('for view in 0..2 {',"+repr(CHECK+"\n for view in 0..2 {")+")\nsource=source.replace("+repr('"actual_editor_visual_verified":false,')+","+repr('"source_pipelines":observed_source_pipelines,"actual_editor_visual_verified":false,')+")\n"
# Patch the literal Rust probe in the underlying generator before its main runs.
source=source.replace(needle,injection+needle)
exec(compile(source,str(BASE_V2),'exec'),globals())
