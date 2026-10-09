"""Replay pinned source bounds/resolution; retain native filter/bias limitations."""
from pathlib import Path
BASE=Path(__file__).with_name('capture_firepit_surface_camera_native_v1.py')
source=BASE.read_text('utf-8').replace('firepit-surface-camera-native-v1','firepit-source-shadow-context-native-v1')
source=source.replace("OLD=W/'firepit-surface-native-v1'","OLD=W/'firepit-shadow-cascades-native-v4'")
source=source.replace("CASE=W/'firepit-surface-native-case-v1'","CASE=W/'firepit-source-shadow-context-case-v1'")
source=source.replace('runner.ROOT=ROOT','runner.ROOT=ROOT;runner.MODULES=[*runner.MODULES,"lights"]')
marker='    rust=rust.replace(marker,observe+marker)';assert source.count(marker)==1
injection=r'''
    replay=r"""
 let context=&case["source_shadow_context_replay"];
 assert_eq!(context["schema_version"].as_u64(),Some(1));
 let bounds=context["cascade_bounds"].as_array().unwrap().iter().map(|v|v.as_f64().unwrap() as f32).collect::<Vec<_>>();
 let config=bevy::light::CascadeShadowConfig{bounds,minimum_distance:context["minimum_distance"].as_f64().unwrap() as f32,overlap_proportion:context["native_overlap_proportion"].as_f64().unwrap() as f32};
 assert!(config.bounds.windows(2).all(|v|v[0]<v[1]));
 app.world_mut().entity_mut(light).insert(config);
 app.world_mut().insert_resource(bevy::light::DirectionalLightShadowMap{size:context["directional_map_size"].as_u64().unwrap() as usize});
 std::fs::write(folder.join("SOURCE-SHADOW-CONTEXT-APPLIED.json"),serde_json::to_vec_pretty(context)?)?;
"""
    needle=' let target=app.world_mut().resource_mut::<Assets<Image>>()'
    assert rust.count(needle)==1
    rust=rust.replace(needle,replay+needle)
    rust=rust.replace('"shadow_settings_changed":false','"shadow_settings_changed":true')
'''
source=source.replace(marker,marker+injection)
if __name__=='__main__':exec(compile(source,str(BASE),'exec'),globals())
