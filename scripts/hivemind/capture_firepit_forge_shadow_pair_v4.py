"""Recheck author-disabled frames and unsafe disabled-route refusals on actual GPU."""
from pathlib import Path
BASE_V3=Path(__file__).with_name('capture_firepit_forge_shadow_pair_v3.py')
source=BASE_V3.read_text('utf-8').replace('firepit-forge-shadow-pair-v3','firepit-forge-shadow-pair-v4')
NEGATIVE=r'''
 let owner=selected_draws[0];let proxy=app.world().get::<SourceGraphShadowRoute>(owner).unwrap().proxy;
 assert_eq!(app.world().get::<SourceGraphShadowRouteStatus>(owner),Some(&SourceGraphShadowRouteStatus::Disabled));
 let bad_frame=policy.frame(source_graph_runtime::SourceClock{current_seconds:1.,previous_seconds:0.})?;
 app.world_mut().entity_mut(proxy).insert(ChildOf(root));assert!(host.update_runtime_frame(app.world_mut(),&bad_frame).is_err());
 for h in materials.values(){assert_eq!(app.world().resource::<Assets<SourceGraphMaterial>>().get(h).unwrap().extension.source.source_time_parameters.x,0.);}
 app.world_mut().entity_mut(proxy).insert(ChildOf(owner));
 app.world_mut().entity_mut(owner).insert(SourceGraphShadowRouteStatus::Refused("Controlled invalid disabled route".into()));assert!(host.update_runtime_frame(app.world_mut(),&bad_frame).is_err());
 for h in materials.values(){assert_eq!(app.world().resource::<Assets<SourceGraphMaterial>>().get(h).unwrap().extension.source.source_time_parameters.x,0.);}
 app.world_mut().entity_mut(owner).insert(SourceGraphShadowRouteStatus::Disabled);
'''
commands="source=source.replace('host.update_runtime_frame(app.world_mut(),&frame)?;', 'host.update_runtime_frame(app.world_mut(),&frame)?;for draw in &selected_draws{assert_eq!(app.world().get::<SourceGraphShadowRouteStatus>(*draw),Some(&if view==0{SourceGraphShadowRouteStatus::Ready}else{SourceGraphShadowRouteStatus::Disabled}));}')\n"
commands+="source=source.replace('let receipt=serde_json::json!(',"+repr(NEGATIVE+'\n let receipt=serde_json::json!(')+")\n"
commands+="source=source.replace("+repr('"actual_editor_visual_verified":false,')+","+repr('"author_disabled_shadow_frame_allowed":true,"unsafe_disabled_shadow_parent_and_status_refused_atomically":true,"actual_editor_visual_verified":false,')+")\n"
needle='source=source.replace(needle,injection+needle)';assert source.count(needle)==1
source=source.replace(needle,'injection+='+repr(commands)+'\n'+needle)
exec(compile(source,str(BASE_V3),'exec'),globals())
