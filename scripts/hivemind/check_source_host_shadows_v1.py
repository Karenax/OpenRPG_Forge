"""Exercise production host shadow installation and same-frame refusal on real GPU."""
from pathlib import Path
import importlib.util,sys
BASE=Path(__file__).with_name('check_source_scene_actual_device_mount_v2.py')
spec=importlib.util.spec_from_file_location('host_shadow_base',BASE)
previous=importlib.util.module_from_spec(spec);spec.loader.exec_module(previous)
SETUP=r'''
 let allocation_count=app.world().resource::<Assets<SourceGraphMaterial>>().len();
 let conflict=app.world_mut().spawn((Camera3d::default(),bevy::camera::visibility::RenderLayers::layer(31))).id();
 assert!(host.install_shadow_routes(app.world_mut(),31).is_err());
 assert_eq!(app.world().resource::<Assets<SourceGraphMaterial>>().len(),allocation_count);
 assert!(draws.iter().all(|e|app.world().get::<SourceGraphShadowRoute>(*e).is_none()));
 app.world_mut().despawn(conflict);
 let last=*draws.last().unwrap();let saved=app.world().get::<source_graph_state::OriginalRendererState>(last).unwrap().shadow_casting_mode;
 app.world_mut().get_mut::<source_graph_state::OriginalRendererState>(last).unwrap().shadow_casting_mode=2;
 assert!(host.install_shadow_routes(app.world_mut(),31).is_err());
 assert!(draws.iter().all(|e|app.world().get::<SourceGraphShadowRoute>(*e).is_none()));
 app.world_mut().get_mut::<source_graph_state::OriginalRendererState>(last).unwrap().shadow_casting_mode=saved;
 let shadow_count=host.install_shadow_routes(app.world_mut(),31)?;
 let routes:Vec<_>=draws.iter().filter_map(|e|app.world().get::<SourceGraphShadowRoute>(*e).map(|r|(*e,r.clone()))).collect();
 assert_eq!(shadow_count,4);assert_eq!(routes.len(),4);
 let after_routes=app.world().resource::<Assets<SourceGraphMaterial>>().len();
 assert_eq!(host.install_shadow_routes(app.world_mut(),31)?,4);
 assert_eq!(app.world().resource::<Assets<SourceGraphMaterial>>().len(),after_routes);
 assert!(routes.iter().all(|(e,r)|app.world().get::<SourceGraphShadowRoute>(*e).unwrap().proxy==r.proxy));
'''
CHECK=r'''
 for (_,route) in &routes{let shadow=app.world().resource::<Assets<SourceGraphMaterial>>().get(&route.shadow_material).unwrap();assert!(shadow.extension.program_key.shadow_only);assert_eq!(shadow.extension.source.source_time_parameters.x,6.);assert_eq!(shadow.extension.source.source_previous_time_parameters.x,5.);}
 let (owner,route)=&routes[0];
 app.world_mut().entity_mut(*owner).insert(SourceGraphShadowRouteStatus::Refused("Controlled refusal".into()));
 app.world_mut().insert_resource(source_graph_runtime::SourceClock{current_seconds:7.,previous_seconds:6.});source_graph_runtime::update(app.world_mut());
 assert!(matches!(app.world().get::<source_graph_runtime::SourceFrameStatus>(root),Some(source_graph_runtime::SourceFrameStatus::Refused(_))));
 for h in materials.values().chain(routes.iter().map(|(_,r)|&r.shadow_material)){assert_eq!(app.world().resource::<Assets<SourceGraphMaterial>>().get(h).unwrap().extension.source.source_time_parameters.x,6.);}
 app.world_mut().entity_mut(*owner).insert(SourceGraphShadowRouteStatus::Ready);
 app.world_mut().entity_mut(*owner).remove::<SourceGraphShadowRoute>();source_graph_runtime::update(app.world_mut());
 assert!(matches!(app.world().get::<source_graph_runtime::SourceFrameStatus>(root),Some(source_graph_runtime::SourceFrameStatus::Refused(_))));
 for h in materials.values().chain(routes.iter().map(|(_,r)|&r.shadow_material)){assert_eq!(app.world().resource::<Assets<SourceGraphMaterial>>().get(h).unwrap().extension.source.source_time_parameters.x,6.);}
 app.world_mut().entity_mut(*owner).insert(route.clone());
'''
def main():
 text=previous.patched_code().replace("ROOT=W/'studio-source-actual-device-mount-v2'","ROOT=W/'studio-source-host-shadows-v1'").replace("name='openrpg-source-actual-device-mount-v2'","name='openrpg-source-host-shadows-v1'")
 needle='source_graph_runtime::retain(app.world_mut(),host,policy)?;app.update();';assert text.count(needle)==1;text=text.replace(needle,SETUP+'\n'+needle)
 needle='app.world_mut().insert_resource(source_graph_runtime::SourceClock{current_seconds:f32::NAN';assert text.count(needle)==1;text=text.replace(needle,CHECK+'\n'+needle)
 needle='"root_revealed":false,';assert text.count(needle)==1;text=text.replace(needle,'"host_shadow_routes":shadow_count,"same_frame_shadow_time_verified":true,"camera_conflict_no_allocation":true,"partial_install_rollback_verified":true,"repeated_install_preserves_ids":true,"refused_and_missing_routes_no_partial_frame":true,'+needle)
 needle='Path(__file__).resolve(),fixture,';assert text.count(needle)==1
 text=text.replace(needle,'Path(__file__).resolve(),Path('+repr(str(BASE))+'),Path('+repr(str(previous.BASE))+'),A/"source_graph_preview.rs",fixture,')
 base=previous.base;exec(compile(text,str(previous.BASE),'exec'),base.__dict__);base.__file__=str(Path(__file__).resolve())
 base.prepare() if sys.argv[-1]=='prepare' else base.run()
if __name__=='__main__':main()
