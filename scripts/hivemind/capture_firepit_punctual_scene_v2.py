"""Exercise common source light mount on the original full scene geometry."""
from pathlib import Path
BASE=Path(__file__).with_name('capture_firepit_surface_camera_native_v1.py')
source=BASE.read_text('utf-8').replace('firepit-surface-camera-native-v1','firepit-punctual-scene-v2')
source=source.replace("OLD=W/'firepit-surface-native-v1'","OLD=W/'firepit-source-ao-scene-v1'")
source=source.replace("CASE=W/'firepit-surface-native-case-v1'","CASE=W/'firepit-punctual-scene-case-v1'")
source=source.replace('runner.ROOT=ROOT','runner.ROOT=ROOT;runner.MODULES=[*runner.MODULES,"lights"]')
marker='    rust=rust.replace(marker,observe+marker)'
assert source.count(marker)==1
injection=r"""
    rust='#[allow(dead_code)] #[path="E:/OpenRPG_Forge/OpenRPG_Forge-Rust/apps/forge_studio/src/authoring_assets/source_graph_lights.rs"] mod source_graph_lights;\n'+rust
    mount='let host=source_graph_host::mount_initial(app.world_mut(),root,'
    assert rust.count(mount)==1
    rust=rust.replace(mount,'app.world_mut().entity_mut(root).insert((source_graph_resources::SceneSampling(forge_engine::source_graph::SourceGraphSceneSampling::UnityForceEnableD3d),source_graph_lights::NativeSourceLightTransport));'+mount)
    check=r'''
 let point_node=case["source_punctual_node"].as_u64().unwrap() as usize;
 let point_entity=nodes[&point_node];
 let point=app.world().get::<PointLight>(point_entity).ok_or("Common source point light absent")?;
 let declared=&case["source_punctual_light"];
 assert!((point.intensity-declared["intensity"].as_f64().unwrap() as f32*(4.*std::f32::consts::PI)).abs()<0.001);
 assert_eq!(point.range,declared["range"].as_f64().unwrap() as f32);assert!(point.shadow_maps_enabled);
 let source=app.world().get::<source_graph_lights::OriginalSourceLight>(point_entity).unwrap();
 assert_eq!(&source.definition,declared);assert!(!source.renderer_parity);assert!(source.source.is_some());
 let count=app.world_mut().query::<&PointLight>().iter(app.world()).count();assert_eq!(count,1);
 let missing_policy=source_graph_lights::prepare(glb.document(),&nodes.keys().copied().collect(),false);assert!(missing_policy.is_err());
 std::fs::write(folder.join("POINT-LIGHT-TRANSPORT.json"),serde_json::to_vec_pretty(&serde_json::json!({"common_mount":true,"point_lights_cpu":count,"node":point_node,"photometry_verified":true,"source_metadata_preserved":true,"shadow_maps_enabled":true,"native_shadow_filter_qualified":false,"missing_transport_policy_refused":true}))?)?;
'''
    needle=' let ground=app.world_mut().spawn('
    assert rust.count(needle)==1
    rust=rust.replace(needle,check+needle)
    observer=r'''
 let point_world=app.world().get::<GlobalTransform>(point_entity).unwrap().translation();
 let expected_point=offset+reflection.transform_point3(Vec3::new(0.,0.33000001311302185,0.));
 assert!(point_world.abs_diff_eq(expected_point,0.0001));
 std::fs::write(folder.join(format!("POINT-LIGHT-FRAME-{view}.json")),serde_json::to_vec_pretty(&serde_json::json!({"view":view,"world":point_world.to_array(),"expected_world":expected_point.to_array(),"common_shadow_route_layer":app.world().get::<bevy::camera::visibility::RenderLayers>(point_entity).map(|v|format!("{:?}",v)),"actual_point_component_present":true,"gpu_punctual_count_readback":false}))?)?;
'''
    needle=' let camera_world=app.world().get::<GlobalTransform>(camera)'
    assert rust.count(needle)==1
    rust=rust.replace(needle,observer+needle)
    needle='app.world_mut().despawn(root);source_graph_runtime::update(app.world_mut());'
    assert rust.count(needle)==1
    rust=rust.replace(needle,needle+'assert!(app.world().get_entity(point_entity).is_err());')
"""
source=source.replace(marker,marker+injection)
if __name__=='__main__':exec(compile(source,str(BASE),'exec'),globals())
