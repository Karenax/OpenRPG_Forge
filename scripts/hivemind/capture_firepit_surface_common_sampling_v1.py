"""Exercise source sampling through the common hidden Studio scene mount."""
from pathlib import Path

BASE=Path(__file__).with_name('capture_firepit_surface_packed_hdr_control_v1.py')
source=BASE.read_text('utf-8').replace('firepit-surface-packed-hdr-control','firepit-surface-common-sampling')
injection=r"""
    mount='let host=source_graph_host::mount_initial(app.world_mut(),root,'
    assert rust.count(mount)==1
    rust=rust.replace(mount,'app.world_mut().entity_mut(root).insert(source_graph_resources::SceneSampling(forge_engine::source_graph::SourceGraphSceneSampling::UnityForceEnableD3d));'+mount)
    inspect=r'''
 let mut sampling_rows=Vec::new();
 let mut seen_images=std::collections::HashSet::new();
 for handle in materials.values() {
  let e=&app.world().resource::<Assets<SourceGraphMaterial>>().get(handle).unwrap().extension;
  for handle in [&e.texture_0,&e.texture_1,&e.texture_2,&e.texture_3,&e.texture_4,&e.texture_5,&e.texture_6].into_iter().flatten() {
   if !seen_images.insert(handle.id()){continue;}
   let image=app.world().resource::<Assets<Image>>().get(handle).unwrap();
   let bevy::image::ImageSampler::Descriptor(sampler)=&image.sampler else {return Err("Explicit common sampler absent".into());};
   assert_eq!(sampler.anisotropy_clamp,9);assert_eq!(sampler.mipmap_filter,bevy::image::ImageFilterMode::Linear);
   assert_eq!(image.texture_descriptor.mip_level_count,12);
   sampling_rows.push(serde_json::json!({"handle":format!("{:?}",handle.id()),"mips":image.texture_descriptor.mip_level_count,"format":format!("{:?}",image.texture_descriptor.format),"effective_anisotropy":sampler.anisotropy_clamp,"effective_mipmap_filter":"Linear","common_mount":true}));
  }
 }
 assert_eq!(sampling_rows.len(),3);
 for (_,contract) in &glb.materials {
  let p=forge_engine::source_graph_glb::source_graph_program_from_glb(contract,&glb.views)?;
  assert!(p.with_scene_sampling(forge_engine::source_graph::SourceGraphSceneSampling::UnityForceEnableD3d,None).is_err());
  for texture in &contract.textures {assert_eq!(texture.imported_storage.as_ref().unwrap().sampler.anisotropy,1);}
 }
 std::fs::write(folder.join("SAMPLING-POLICY.json"),serde_json::to_vec_pretty(&sampling_rows)?)?;
'''
    needle=' let prototype=app.world().resource::<Assets<SourceGraphMaterial>>()'
    assert rust.count(needle)==1
    rust=rust.replace(needle,inspect+needle)
    guard=r'''
 app.world_mut().entity_mut(root).insert(source_graph_resources::SceneSampling(forge_engine::source_graph::SourceGraphSceneSampling::Authored));
 assert!(host.update_runtime_frame(app.world_mut(),&frame).is_err());
 assert_eq!(app.world().get::<Visibility>(root),Some(&Visibility::Hidden));
 app.world_mut().entity_mut(root).insert(source_graph_resources::SceneSampling(forge_engine::source_graph::SourceGraphSceneSampling::UnityForceEnableD3d));
 host.update_runtime_frame(app.world_mut(),&frame)?;
 std::fs::write(folder.join("SAMPLING-REFUSALS.json"),b"{\"missing_actual_adapter_refused\":true,\"changed_policy_hides_host\":true,\"authored_metadata_preserved\":true}")?;
'''
    needle=' let routes:Vec<_>=selected_draws.iter()'
    assert rust.count(needle)==1
    rust=rust.replace(needle,guard+needle)
"""
marker="    rust=rust.replace(old_format,'TextureFormat::Rg11b10Ufloat,None)')"
assert source.count(marker)==1
source=source.replace(marker,marker+injection)
if __name__=='__main__':
    exec(compile(source,str(BASE),'exec'),globals())
