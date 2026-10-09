"""Read actual HDR GPU pixels from the production host; do not claim render parity."""
from pathlib import Path
import importlib.util,sys
BASE=Path(__file__).with_name('check_source_scene_actual_device_mount_v2.py')
spec=importlib.util.spec_from_file_location('forge_shadow_device',BASE);previous=importlib.util.module_from_spec(spec);spec.loader.exec_module(previous)
PROBE=r'''
 let folder=std::path::Path::new(&args[2]).parent().unwrap();
 let case:serde_json::Value=serde_json::from_slice(&std::fs::read(folder.join("CASE.v1.json"))?)?;
 let v3=|v:&serde_json::Value|->Vec3{Vec3::new(v["x"].as_f64().unwrap() as f32,v["y"].as_f64().unwrap() as f32,v["z"].as_f64().unwrap() as f32)};
 let source_center=v3(&case["receiver"]["bounds_reference"]);let source_size=v3(&case["receiver"]["size_reference"]);let radius=(source_size*.5).length();
 let source_eye=v3(&case["source_camera"]["position"]);let source_target=v3(&case["source_camera"]["target"]);
 let camera_eye=offset+reflection.transform_point3(source_eye);let camera_target=offset+reflection.transform_point3(source_target);
 let selected=host.attached.nodes[&(case["selected_source_node"].as_u64().unwrap() as usize)];
 host.install_shadow_routes(app.world_mut(),31)?;
 let mut selected_draws=Vec::new();
 for draw in &draws{let active=app.world().get::<ChildOf>(*draw).unwrap().parent()==selected;if active{selected_draws.push(*draw);}app.world_mut().entity_mut(*draw).insert(if active{Visibility::Inherited}else{Visibility::Hidden});}
 assert_eq!(selected_draws.len(),1);app.world_mut().entity_mut(root).insert(Visibility::Inherited);
 app.world_mut().insert_resource(AmbientLight{brightness:0.,..default()});
 let ground_mesh=app.world_mut().resource_mut::<Assets<Mesh>>().add(Plane3d::default().mesh().size(radius*10.,radius*10.));
 let ground_material=app.world_mut().resource_mut::<Assets<StandardMaterial>>().add(StandardMaterial{base_color:Color::WHITE,perceptual_roughness:1.,metallic:0.,..default()});
 let ground_pos=offset+reflection.transform_point3(Vec3::new(source_center.x,source_center.y-source_size.y*.5-.01,source_center.z));
 let ground=app.world_mut().spawn((Mesh3d(ground_mesh),MeshMaterial3d(ground_material),Transform::from_translation(ground_pos),bevy::light::NotShadowCaster)).id();
 let direction=reflection.transform_vector3(Vec3::new(.8,-.4,.6)).normalize();
 let light_value=DirectionalLight{illuminance:case["source_light"]["intensity"].as_f64().unwrap() as f32,shadow_maps_enabled:true,..default()};
 let shadow_parameters=serde_json::json!({"depth_bias":light_value.shadow_depth_bias,"normal_bias":light_value.shadow_normal_bias,"filter":"Gaussian (Bevy default)","source_shadow_settings_matched":false});
 let light=app.world_mut().spawn((light_value,Transform::from_translation(offset+Vec3::Y*5.).looking_to(direction,Vec3::Y))).id();
 let target=app.world_mut().resource_mut::<Assets<Image>>().add(Image::new_target_texture(512,512,bevy::render::render_resource::TextureFormat::Rgba16Float,None));
 let camera=app.world_mut().spawn((Camera3d::default(),bevy::camera::Hdr,Camera{clear_color:ClearColorConfig::Custom(Color::linear_rgb(.025,.035,.05)),..default()},bevy::camera::RenderTarget::Image(target.clone().into()),Msaa::Off,bevy::core_pipeline::tonemapping::Tonemapping::None,bevy::camera::Exposure{ev100:10.},Projection::Perspective(PerspectiveProjection{fov:30_f32.to_radians(),near:.01,far:(radius*12.).max(10.),aspect_ratio:1.,..default()}),Transform::from_translation(camera_eye).looking_at(camera_target,Vec3::Y),bevy::core_pipeline::prepass::DepthPrepass,bevy::core_pipeline::prepass::NormalPrepass)).id();
 let mut capture_rows=Vec::new();
 for view in 0..2 {
  for draw in &selected_draws{set_source_graph_shadow_enabled(app.world_mut(),*draw,view==0)?;}
  for _ in 0..60{app.update();std::thread::sleep(Duration::from_millis(15));}
  host.update_runtime_frame(app.world_mut(),&frame)?;
  let raw_path=folder.join(format!("gpu-{view}.rgba16f"));let meta_path=folder.join(format!("gpu-{view}.metadata.json"));assert!(!raw_path.exists()&&!meta_path.exists());
  let write_raw=raw_path.clone();let write_meta=meta_path.clone();
  app.world_mut().spawn(bevy::render::view::window::screenshot::Screenshot::image(target.clone())).observe(move|e:On<bevy::render::view::window::screenshot::ScreenshotCaptured>|{
   let descriptor=&e.image.texture_descriptor;let data=e.image.data.as_ref().expect("GPU screenshot CPU data");
   std::fs::write(&write_raw,data).unwrap();std::fs::write(&write_meta,serde_json::to_vec_pretty(&serde_json::json!({"format":format!("{:?}",descriptor.format),"width":descriptor.size.width,"height":descriptor.size.height,"bytes":data.len(),"gpu_readback":true})).unwrap()).unwrap();
  });
  let deadline=Instant::now()+Duration::from_secs(30);while !meta_path.exists(){app.update();if Instant::now()>deadline{return Err("GPU readback timed out".into());}std::thread::sleep(Duration::from_millis(15));}
  let meta:serde_json::Value=serde_json::from_slice(&std::fs::read(&meta_path)?)?;assert_eq!(meta["format"],"Rgba16Float");assert_eq!(meta["bytes"],512*512*8);
  capture_rows.push(serde_json::json!({"view":view,"shadows":view==0,"raw_path":raw_path,"metadata_path":meta_path}));
 }
 let routes:Vec<_>=selected_draws.iter().map(|e|app.world().get::<SourceGraphShadowRoute>(*e).unwrap()).collect();assert!(routes.iter().all(|r|app.world().get::<ChildOf>(r.proxy).unwrap().parent()==selected_draws[0]));
 let receipt=serde_json::json!({"status":"actual_forge_source_lod0_shadow_pair_gpu_read_back","images":capture_rows,"width":512,"height":512,"source_mesh_node":case["selected_source_node"],"source_lod0_only_diagnostic":true,"same_camera_pose_and_fov":true,"light_lux":10000,"exposure_ev100":10,"gpu_tonemapping":"None; raw exposed HDR data retained","source_tonemapping":"HDRP Neutral; comparison transfer still required","source_time_parameters_frozen":0,"shadow_parameters":shadow_parameters,"actual_editor_visual_verified":false,"full_import":false});
 std::fs::write(folder.join("GPU-PAIR.v1.json"),serde_json::to_vec_pretty(&receipt)?)?;
 app.world_mut().entity_mut(root).insert(Visibility::Hidden);app.world_mut().despawn(camera);app.world_mut().despawn(light);app.world_mut().despawn(ground);app.update();
'''
def main():
 text=previous.patched_code().replace("ROOT=W/'studio-source-actual-device-mount-v2'","ROOT=W/'firepit-forge-shadow-pair-v1'").replace("name='openrpg-source-actual-device-mount-v2'","name='openrpg-firepit-forge-shadow-pair-v1'")
 needle="ROOT.mkdir();(ROOT/'src').mkdir();";assert text.count(needle)==1
 text=text.replace(needle,"ROOT.mkdir();(ROOT/'CASE.v1.json').write_bytes((W/'firepit-shadow-comparison-case-v1/CASE.v1.json').read_bytes());(ROOT/'src').mkdir();")
 needle='source_graph_runtime::retain(app.world_mut(),host,policy)?;app.update();';assert text.count(needle)==1;text=text.replace(needle,PROBE+'\n'+needle)
 needle='Path(__file__).resolve(),fixture,';assert text.count(needle)==1
 extra=[BASE,previous.BASE,previous.base.W/'firepit-shadow-comparison-case-v1/CASE.v1.json',previous.base.W/'firepit-shadow-comparison-case-v1/RESULT.v1.json']
 text=text.replace(needle,'Path(__file__).resolve(),'+''.join('Path('+repr(str(p))+'),' for p in extra)+'fixture,')
 base=previous.base;exec(compile(text,str(previous.BASE),'exec'),base.__dict__);base.__file__=str(Path(__file__).resolve())
 base.prepare() if sys.argv[-1]=='prepare' else base.run()
if __name__=='__main__':main()
