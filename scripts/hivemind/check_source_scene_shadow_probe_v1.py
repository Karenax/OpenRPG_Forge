"""Exercise explicit source shadow proxies and record actual specialized states."""
from pathlib import Path
import importlib.util,sys
BASE=Path(__file__).with_name('check_source_scene_pipeline_probe_v1.py')
spec=importlib.util.spec_from_file_location('pipeline_probe_base',BASE);previous=importlib.util.module_from_spec(spec);spec.loader.exec_module(previous)
def main():
 probe=previous.PROBE
 needle='let deadline=Instant::now()+Duration::from_secs(45);';assert probe.count(needle)==1
 setup=r'''
 let light=app.world_mut().spawn((DirectionalLight{shadow_maps_enabled:true,illuminance:8000.,..default()},Transform::from_translation(offset+Vec3::new(3.,5.,4.)).looking_at(offset,Vec3::Y))).id();
 let mut shadow_routes=Vec::new();for draw in &draws{
  let original=app.world().get::<source_graph_state::OriginalRendererState>(*draw).unwrap();
  if original.shadow_casting_mode==1{let proxy=install_source_graph_shadow_route(app.world_mut(),*draw,31)?;shadow_routes.push((*draw,proxy));}
 }
 assert!(!shadow_routes.is_empty());
 '''
 probe=probe.replace(needle,setup+'\n'+needle)
 needle='"state":state,"error":error';assert probe.count(needle)==1
 probe=probe.replace(needle,needle+',"cull_mode":format!("{:?}",d.primitive.cull_mode),"front_face":format!("{:?}",d.primitive.front_face),"depth_stencil":format!("{:?}",d.depth_stencil),"multisample":format!("{:?}",d.multisample),"color_targets":d.fragment.as_ref().map(|f|format!("{:?}",f.targets))')
 needle='frames>=20&&!pipeline_rows.is_empty()&&pipeline_rows.iter().all(|r|r["state"]=="ok")';assert probe.count(needle)==1
 probe=probe.replace(needle,'frames>=30&&pipeline_rows.iter().any(|r|r["vertex_defs"].as_str().unwrap().contains("SOURCE_GRAPH_SHADOW_PASS"))&&pipeline_rows.iter().all(|r|r["state"]=="ok")')
 needle='let passed=!pipeline_rows.is_empty()&&pipeline_rows.iter().all(|r|r["state"]=="ok");';assert probe.count(needle)==1
 probe=probe.replace(needle,'let shadow_ready=pipeline_rows.iter().any(|r|r["vertex_defs"].as_str().unwrap().contains("SOURCE_GRAPH_SHADOW_PASS"));let passed=shadow_ready&&!pipeline_rows.is_empty()&&pipeline_rows.iter().all(|r|r["state"]=="ok");')
 needle='app.world_mut().entity_mut(root).insert(Visibility::Hidden);';assert probe.count(needle)==1
 probe=probe.replace(needle,r'''
 for (draw,proxy) in &shadow_routes{assert_eq!(app.world().get::<SourceGraphShadowRouteStatus>(*draw),Some(&SourceGraphShadowRouteStatus::Ready));assert_eq!(app.world().get::<ChildOf>(*proxy).unwrap().parent(),*draw);}
 app.world_mut().despawn(light);
 '''+needle)
 needle='"source_pipeline_count":pipeline_rows.len(),';assert probe.count(needle)==1
 probe=probe.replace(needle,needle+'"explicit_shadow_routes":shadow_routes.len(),"source_shadow_pipeline_ready":shadow_ready,')
 text=previous.previous.patched_code();text=text.replace("ROOT=W/'studio-source-actual-device-mount-v2'","ROOT=W/'studio-source-shadow-probe-v1'").replace("name='openrpg-source-actual-device-mount-v2'","name='openrpg-source-shadow-probe-v1'")
 needle='source_graph_runtime::retain(app.world_mut(),host,policy)?;app.update();';assert text.count(needle)==1;text=text.replace(needle,probe+'\n'+needle)
 needle='Path(__file__).resolve(),fixture,';assert text.count(needle)==1
 files=[BASE,previous.BASE,previous.previous.BASE]
 text=text.replace(needle,'Path(__file__).resolve(),'+''.join('Path('+repr(str(p))+'),' for p in files)+'fixture,')
 base=previous.previous.base;exec(compile(text,str(previous.previous.BASE),'exec'),base.__dict__);base.__file__=str(Path(__file__).resolve())
 base.prepare() if sys.argv[-1]=='prepare' else base.run()
if __name__=='__main__':main()
