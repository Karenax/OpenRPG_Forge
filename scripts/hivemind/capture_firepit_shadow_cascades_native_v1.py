"""Observe native shadow cascades without changing the pinned scene draw."""
from pathlib import Path
BASE=Path(__file__).with_name('capture_firepit_punctual_scene_v2.py')
source=BASE.read_text('utf-8').replace('firepit-punctual-scene-v2','firepit-shadow-cascades-native-v1')
needle=' let point_world='
assert source.count(needle)==1
extra=r"""
 let cascade_config=app.world().get::<bevy::light::CascadeShadowConfig>(light).ok_or("Directional cascade config absent")?;
 let cascades=app.world().get::<bevy::light::Cascades>(light).ok_or("Directional cascade state absent")?.cascades.get(&camera).ok_or("Camera cascade state absent")?;
 assert_eq!(cascades.len(),cascade_config.bounds.len());
 let object_center=offset+reflection.transform_point3(source_center);
 let view_z=app.world().get::<GlobalTransform>(camera).unwrap().to_matrix().inverse().transform_point3(object_center).z;
 let selected_cascade=cascade_config.bounds.iter().position(|bound|-view_z < *bound).ok_or("Model outside native shadow cascades")?;
 let rows:Vec<_>=cascades.iter().enumerate().map(|(i,c)|serde_json::json!({"index":i,"far_bound":cascade_config.bounds[i],"world_texel_size":c.texel_size,"world_from_cascade":c.world_from_cascade.to_cols_array(),"clip_from_cascade":c.clip_from_cascade.to_cols_array(),"clip_from_world":c.clip_from_world.to_cols_array()})).collect();
 let directional=app.world().get::<DirectionalLight>(light).unwrap();
 let record=serde_json::json!({"view":view,"camera_view_z_at_model_center":view_z,"model_center_world":object_center.to_array(),"selected_cascade_cpu":selected_cascade,"cascade_count":cascades.len(),"cascades":rows,"minimum_distance":cascade_config.minimum_distance,"overlap_proportion":cascade_config.overlap_proportion,"directional_shadow_map_size":app.world().resource::<bevy::light::DirectionalLightShadowMap>().size,"point_shadow_map_size":app.world().resource::<bevy::light::PointLightShadowMap>().size,"directional_depth_bias":directional.shadow_depth_bias,"directional_normal_bias":directional.shadow_normal_bias,"gpu_uniform_readback":false,"shadow_settings_changed":false});
 std::fs::write(folder.join(format!("NATIVE-SHADOW-CASCADES-{view}.json")),serde_json::to_vec_pretty(&record)?)?;
"""
source=source.replace(needle,extra+needle)
exec(compile(source,str(BASE),'exec'),globals())
