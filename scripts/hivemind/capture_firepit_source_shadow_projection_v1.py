"""Replay reconstructed source projections before native shadow frusta/extract."""
from pathlib import Path
BASE=Path(__file__).with_name('capture_firepit_surface_camera_native_v1.py')
source=BASE.read_text('utf-8').replace('firepit-surface-camera-native-v1','firepit-source-shadow-projection-native-v1')
source=source.replace("OLD=W/'firepit-surface-native-v1'","OLD=W/'firepit-source-shadow-context-native-v1'")
source=source.replace("CASE=W/'firepit-surface-native-case-v1'","CASE=W/'firepit-source-shadow-projection-case-v1'")
source=source.replace('runner.ROOT=ROOT','runner.ROOT=ROOT;runner.MODULES=[*runner.MODULES,"lights"]')
marker='    rust=rust.replace(marker,observe+marker)';assert source.count(marker)==1
injection=r'''
    replay=r"""
 let projections=&case["source_shadow_projection_replay"];
 assert_eq!(projections["schema_version"].as_u64(),Some(1));
 let matrix=|v:&serde_json::Value|->Mat4 {let values=v.as_array().unwrap();assert_eq!(values.len(),16);Mat4::from_cols_array(&std::array::from_fn(|i|values[i].as_f64().unwrap() as f32))};
 let planned_cascades=projections["cascades"].as_array().unwrap().iter().map(|row|bevy::light::Cascade{world_from_cascade:matrix(&row["world_from_cascade"]),clip_from_cascade:matrix(&row["clip_from_cascade"]),clip_from_world:matrix(&row["clip_from_world"]),texel_size:row["texel_size"].as_f64().unwrap() as f32}).collect::<Vec<_>>();
 assert_eq!(planned_cascades.len(),4);assert!(planned_cascades.iter().all(|c|c.world_from_cascade.is_finite()&&c.clip_from_cascade.is_finite()&&c.clip_from_world.is_finite()&&c.texel_size>0.));
 app.add_systems(PostUpdate,(move |mut lights:Query<&mut bevy::light::Cascades>|{if let Ok(mut target)=lights.get_mut(light){target.cascades.insert(camera,planned_cascades.clone());}}).after(bevy::light::SimulationLightSystems::UpdateDirectionalLightCascades).before(bevy::light::SimulationLightSystems::UpdateLightFrusta));
 std::fs::write(folder.join("SOURCE-SHADOW-PROJECTIONS-APPLIED.json"),serde_json::to_vec_pretty(projections)?)?;
"""
    needle=' let mut capture_rows=Vec::new();'
    assert rust.count(needle)==1
    rust=rust.replace(needle,replay+needle)
'''
source=source.replace(marker,marker+injection)
if __name__=='__main__':exec(compile(source,str(BASE),'exec'),globals())
