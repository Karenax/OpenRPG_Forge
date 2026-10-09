"""Replay reconstructed source projections before native shadow frusta/extract."""
from pathlib import Path
BASE=Path(__file__).with_name('capture_firepit_surface_camera_native_v1.py')
source=BASE.read_text('utf-8').replace('firepit-surface-camera-native-v1','firepit-source-shadow-oriented-native-v1')
source=source.replace("OLD=W/'firepit-surface-native-v1'","OLD=W/'firepit-source-shadow-context-native-v1'")
source=source.replace("CASE=W/'firepit-surface-native-case-v1'","CASE=W/'firepit-source-shadow-oriented-case-v1'")
source=source.replace('runner.ROOT=ROOT','runner.ROOT=ROOT;runner.MODULES=[*runner.MODULES,"lights"]')
marker='    rust=rust.replace(marker,observe+marker)';assert source.count(marker)==1
injection=r'''
    replay=r"""
 let projections=&case["source_shadow_projection_replay"];
 assert_eq!(projections["schema_version"].as_u64(),Some(1));
 let matrix=|v:&serde_json::Value|->Mat4 {let values=v.as_array().unwrap();assert_eq!(values.len(),16);Mat4::from_cols_array(&std::array::from_fn(|i|values[i].as_f64().unwrap() as f32))};
 let planned_cascades=projections["cascades"].as_array().unwrap().iter().map(|row|bevy::light::cascade::Cascade{world_from_cascade:matrix(&row["world_from_cascade"]),clip_from_cascade:matrix(&row["clip_from_cascade"]),clip_from_world:matrix(&row["clip_from_world"]),texel_size:row["texel_size"].as_f64().unwrap() as f32}).collect::<Vec<_>>();
 assert_eq!(planned_cascades.len(),4);assert!(planned_cascades.iter().all(|c|c.world_from_cascade.is_finite()&&c.clip_from_cascade.is_finite()&&c.clip_from_world.is_finite()&&c.texel_size>0.));
 app.add_systems(PostUpdate,(move |mut lights:Query<&mut bevy::light::Cascades>|{if let Ok(mut target)=lights.get_mut(light){target.cascades.insert(camera,planned_cascades.clone());}}).after(bevy::light::SimulationLightSystems::UpdateDirectionalLightCascades).before(bevy::light::SimulationLightSystems::UpdateLightFrusta));
 std::fs::write(folder.join("SOURCE-SHADOW-PROJECTIONS-APPLIED.json"),serde_json::to_vec_pretty(projections)?)?;
"""
    needle=' let mut capture_rows=Vec::new();'
    assert rust.count(needle)==1
    rust=rust.replace(needle,replay+needle)
    raster=r"""
 let raster=&case["source_shadow_raster_replay"];assert_eq!(raster["schema_version"].as_u64(),Some(1));
 let raster_policy=SourceGraphShadowRasterState::resolved(raster["constant"].as_i64().unwrap() as i32,raster["slope_scale"].as_f64().unwrap() as f32,raster["clamp"].as_f64().unwrap() as f32)?;
 let face=match raster["front_face"].as_str(){Some("cw")=>bevy::render::render_resource::FrontFace::Cw,Some("ccw")=>bevy::render::render_resource::FrontFace::Ccw,_=>return Err("Explicit source shadow orientation missing".into())};let raster_policy=raster_policy.with_front_face(face);
 let mut raster_originals=0;for h in materials.values(){app.world_mut().resource_mut::<Assets<SourceGraphMaterial>>().get_mut(h).unwrap().extension.program_key.orthographic_shadow_raster=Some(raster_policy);raster_originals+=1;}assert_eq!(raster_originals,4);
 let mut raster_materials=0;for (_,material) in app.world_mut().resource_mut::<Assets<SourceGraphMaterial>>().iter_mut(){if material.extension.program_key.shadow_only{material.extension.program_key.orthographic_shadow_raster=Some(raster_policy);raster_materials+=1;}}
 assert_eq!(raster_materials,4);std::fs::write(folder.join("SOURCE-RASTER-POLICY-APPLIED.json"),serde_json::to_vec_pretty(&serde_json::json!({"shadow_materials":raster_materials,"policy":raster,"native_depth_gpu_readback_verified":false,"full_import":false}))?)?;
"""
    assert rust.count(needle)==1;rust=rust.replace(needle,raster+needle)
    stage_path=Path(__file__).parent/'candidates/SourceShadowDepthStageV3.rs'
    stage=stage_path.read_text('utf-8');rust=stage+'\n'+rust
    assert rust.count(needle)==1;rust=rust.replace(needle,' source_depth_stage::install(&mut app,&folder.join("depth-probe.wgsl"));\n'+needle)
    depth_probe=(Path(__file__).parent/'candidates/SourceShadowDepthReadbackV6.rs').read_text('utf-8')
    depth_marker=' let raw_path=folder.join(format!("gpu-{view}.rgba16f"));';assert rust.count(depth_marker)==1
    rust=rust.replace(depth_marker,depth_probe+depth_marker)
    caster_probe=(Path(__file__).parent/'candidates/SourceShadowCasterViewReadbackV1.rs').read_text('utf-8')
    rust=rust.replace(depth_marker,caster_probe+depth_marker)

    failure='if Instant::now()>deadline{return Err("Source forward/prepass/shadow pipelines not ready before readback".into());}'
    diagnostic='std::fs::write(folder.join("SOURCE-PIPELINE-REFUSAL.json"),serde_json::to_vec_pretty(&observed_source_pipelines)?)?;'
    assert rust.count(failure)==1
    rust=rust.replace(failure,'if Instant::now()>deadline{'+diagnostic+'return Err("Source forward/prepass/shadow pipelines not ready before readback".into());}')

'''
source=source.replace(marker,marker+injection)
copy_marker="    files=[Path(__file__).resolve(),BASE,fixture,"
assert source.count(copy_marker)==1
source=source.replace(copy_marker,"    (ROOT/'caster-view-probe.wgsl').write_bytes((Path(__file__).parent/'candidates/SourceShadowCasterViewProbeV1.wgsl').read_bytes())\n    (ROOT/'depth-probe.wgsl').write_bytes((Path(__file__).parent/'candidates/SourceShadowDepthProbeHostLayersV2.wgsl').read_bytes())\n"+copy_marker)

source=source.replace(copy_marker,"    files=[Path(__file__).parent/'candidates/SourceShadowCasterViewProbeV1.wgsl',Path(__file__).parent/'candidates/SourceShadowCasterViewReadbackV1.rs',Path(__file__).parent/'candidates/SourceShadowDepthKnownControlV1.rs',Path(__file__).parent/'candidates/SourceShadowDepthStageV3.rs',Path(__file__).parent/'candidates/SourceShadowDepthReadbackV6.rs',Path(__file__).parent/'candidates/SourceShadowDepthProbeHostLayersV2.wgsl',Path(__file__).resolve(),BASE,fixture,")
if __name__=='__main__':exec(compile(source,str(BASE),'exec'),globals())

