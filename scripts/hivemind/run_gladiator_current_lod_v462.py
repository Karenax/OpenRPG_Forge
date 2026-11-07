"""Repeat the native replay using actual Bevy viewport camera components."""
from pathlib import Path
parent=Path(__file__).with_name('run_gladiator_current_lod_v460.py')
code=parent.read_text('utf-8').replace('gladiator-current-lod-v460','gladiator-current-lod-v462')
injection='''
rust=rust.replace('let camera=source_graph_lod::LodCamera{position:basis.transform_point3(vec3(&sample["camera_position"])),vertical_fov_radians:(sample["field_of_view"].as_f64().unwrap() as f32).to_radians(),lod_bias:1.};', '''+repr('''let view=world.spawn((Camera3d::default(),Camera::default(),Projection::Perspective(PerspectiveProjection{fov:(sample["field_of_view"].as_f64().unwrap() as f32).to_radians(),..Default::default()}),GlobalTransform::from_translation(basis.transform_point3(vec3(&sample["camera_position"]))))).id();
  let camera=source_graph_lod::camera(&world,view,1.).unwrap();
  world.get_mut::<Camera>(view).unwrap().is_active=false;assert!(source_graph_lod::camera(&world,view,1.).is_err());world.get_mut::<Camera>(view).unwrap().is_active=true;
  world.entity_mut(view).insert(Projection::Orthographic(OrthographicProjection::default_3d()));assert!(source_graph_lod::camera(&world,view,1.).is_err());world.despawn(view);''')+''')
'''
assert code.count('prefix = ')==1
code=code.replace('prefix = ',injection+'\nprefix = ')
code=code.replace('scene_module,lod_module,metric_path,assembly_path','scene_module,lod_module,R/"apps/forge_studio/src/authoring_assets/source_graph_runtime.rs",metric_path,assembly_path')
exec(compile(code,str(parent)+'#actual-viewport-v462','exec'),globals())
