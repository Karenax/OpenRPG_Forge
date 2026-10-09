"""Render actual original geometry with measured diffuse on object and receiver."""
from pathlib import Path
BASE=Path(__file__).with_name('capture_firepit_combined_diffuse_gpu_v1.py')
source=BASE.read_text('utf-8')
# Insert transforms into the deepest existing host generator, after its bindings.
old_ground=''' let ground_material=app.world_mut().resource_mut::<Assets<StandardMaterial>>().add(StandardMaterial{base_color:Color::WHITE,perceptual_roughness:1.,metallic:0.,..default()});
 let ground_pos=offset+reflection.transform_point3(Vec3::new(source_center.x,source_center.y-source_size.y*0.5-0.01,source_center.z));'''
new_ground=r'''
 let ground_pos=offset+reflection.transform_point3(Vec3::new(source_center.x,source_center.y-source_size.y*0.5-0.01,source_center.z));
 let prototype=app.world().resource::<Assets<SourceGraphMaterial>>().get(materials.values().next().unwrap()).unwrap().clone();
 let source_uniform=frame.uniform(host_basis(&glb)?,Mat4::from_translation(ground_pos),prototype.extension.source.normal_decode_modes)?;
 let images=vec![prototype.extension.texture_0.clone().unwrap(),prototype.extension.texture_1.clone().unwrap(),prototype.extension.texture_2.clone().unwrap()];
 let mut ground_shaders=Vec::new();
 for stage in ["forward","prepass"]{
  let item=&case["receiver_program"][stage];let text=std::fs::read_to_string(item["path"].as_str().unwrap())?;
  ground_shaders.push(SourceGraphShaderArtifact{source:text,expected_sha256:item["sha256"].as_str().unwrap().parse().unwrap()});
 }
 let k=prototype.extension.program_key;
 let program=SourceGraphMaterialProgram{prepass_cull:k.prepass_cull,shaders:ground_shaders,forward_index:0,prepass_index:1,
  required_texture_count:3,color_write_mask:k.color_write_mask,forward_state:k.forward_state,
  original_transparent:false,original_alpha_test:false,original_alpha_to_mask:false,requires_original_vertex_color:false};
 let mut ground_value=app.world_mut().resource_scope(|world,mut registry:Mut<SourceGraphShaderRegistry>|{
  register_source_graph_material(&mut world.resource_mut::<Assets<Shader>>(),&mut registry,program,source_uniform,images)
 })?;
 ground_value.extension.diffuse_probe=diffuse_probe.clone();ground_value.extension.set_preintegrated_brdf(&binding);
 ground_value.extension.validate_scene_dependencies(app.world().resource::<Assets<Image>>())?;
 let ground_material=app.world_mut().resource_mut::<Assets<SourceGraphMaterial>>().add(ground_value);
'''
old_mesh=' let ground_mesh=app.world_mut().resource_mut::<Assets<Mesh>>().add(Plane3d::default().mesh().size(radius*10.,radius*10.));'
new_mesh=' let mut receiver_mesh=Plane3d::default().mesh().size(radius*10.,radius*10.).build();receiver_mesh.generate_tangents()?;let ground_mesh=app.world_mut().resource_mut::<Assets<Mesh>>().add(receiver_mesh);'
commands='source=source.replace('+repr(old_ground)+','+repr(new_ground)+')\n'
commands+='source=source.replace('+repr(old_mesh)+','+repr(new_mesh)+')\n'
commands+='source=source.replace('+repr('Msaa::Off,bevy::core_pipeline::tonemapping')+','+repr('Msaa::Off,bevy::pbr::ScreenSpaceAmbientOcclusion::default(),bevy::core_pipeline::tonemapping')+')\n'
commands+='source=source.replace('+repr('let mut shader_ids=std::collections::BTreeSet::new();for h in materials.values(){')+','+repr('let mut shader_ids=std::collections::BTreeSet::new();for h in materials.values().chain(std::iter::once(&ground_material)){')+')\n'
commands+='source=source.replace('+repr('if observed_source_pipelines.len()>=3&&observed_source_pipelines.iter().all(|p|p["state"]=="ready"){break;}')+','+repr('if observed_source_pipelines.len()>=5&&observed_source_pipelines.iter().all(|p|p["state"]=="ready"){break;}')+')\n'
check=r'''
 let ssao_pipeline={let rw=app.get_sub_app_mut(RenderApp).unwrap().world_mut();let mut q=rw.query::<&bevy::pbr::SsaoPipelineId>();q.iter(rw).next().map(|p|p.0).ok_or("Native SSAO pipeline absent")?};
 assert!(app.get_sub_app(RenderApp).unwrap().world().resource::<bevy::render::render_resource::PipelineCache>().get_compute_pipeline(ssao_pipeline).is_some(),"Native SSAO pipeline not ready");
'''
commands+='source=source.replace('+repr('let raw_path=folder.join(format!("gpu-{view}.rgba16f"));')+','+repr(check+'\n let raw_path=folder.join(format!("gpu-{view}.rgba16f"));')+')\n'
commands+='source=source.replace('+repr('"brdf_lookup_cases_expected":48,')+','+repr('"measured_diffuse_applied_on_object_and_receiver":true,"native_ssao_compute_ready":true,"source_ssao_algorithm_matched":false,')+')\n'
# The wrapper's source variable contains the BRDF host generator at this point.
needle='exec(compile(source,str(BASE),\'exec\'),globals())'
assert source.count(needle)==1
insertion="marker='needle = "+repr('"needle=\'source=source.replace(needle,injection+needle)\'"')+"'\n"
# Use the full known generator line, no execution of purchased code.
insertion="marker="+repr('needle = "needle=\'source=source.replace(needle,injection+needle)\'"')+"\nassert source.count(marker)==1\nsource=source.replace(marker,'commands += '+repr("+repr(commands)+")+'\\n'+marker)\n"
insertion+="source=source.replace('firepit-combined-diffuse-gpu','firepit-diffuse-scene').replace('private-combined-diffuse-witness.glb','private-source-diffuse-scene.glb')\n"
source=source.replace(needle,insertion+needle)
exec(compile(source,str(BASE),'exec'),globals())
