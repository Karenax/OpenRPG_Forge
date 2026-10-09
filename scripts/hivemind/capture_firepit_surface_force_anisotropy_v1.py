"""Private source quality policy: aniso 9 plus trilinear, original texels intact."""
from pathlib import Path
import json
from datetime import datetime,timezone

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'firepit-surface-force-anisotropy-v2'
PLAN=W/'firepit-surface-force-anisotropy-plan-v2'
BASE=Path(__file__).with_name('capture_firepit_surface_packed_hdr_control_v1.py')
source=BASE.read_text('utf-8').replace('firepit-surface-packed-hdr-control','firepit-surface-force-anisotropy')
source=source.replace('firepit-surface-force-anisotropy-v1','firepit-surface-force-anisotropy-v2').replace('firepit-surface-force-anisotropy-plan-v1','firepit-surface-force-anisotropy-plan-v2')
marker='    rust=rust.replace(old_format,\'TextureFormat::Rg11b10Ufloat,None)\')'
assert source.count(marker)==1

palette_code=r'''
 let original_sampling_rows:Vec<_>=materials.values().map(|handle|{let e=&app.world().resource::<Assets<SourceGraphMaterial>>().get(handle).unwrap().extension;(handle.clone(),[e.texture_0.clone(),e.texture_1.clone(),e.texture_2.clone(),e.texture_3.clone(),e.texture_4.clone(),e.texture_5.clone(),e.texture_6.clone()])}).collect();
 let mut private_sampling_rows=Vec::new();
 let mut shared_quality_images:std::collections::HashMap<bevy::asset::AssetId<Image>,Handle<Image>>=std::collections::HashMap::new();
 for (handle,originals) in &original_sampling_rows {
  let mut replacements=Vec::new();
  for image_handle in originals {
   let replacement=if let Some(image_handle)=image_handle {
    if let Some(cached)=shared_quality_images.get(&image_handle.id()){Some(cached.clone())}else{
     let image=app.world().resource::<Assets<Image>>().get(image_handle).unwrap();
     let mut copy=image.clone();
     let bevy::image::ImageSampler::Descriptor(sampler)=&mut copy.sampler else {return Err("Explicit source sampler required".into());};
     assert_eq!(sampler.anisotropy_clamp,1);assert_eq!(sampler.min_filter,bevy::image::ImageFilterMode::Linear);assert_eq!(sampler.mag_filter,bevy::image::ImageFilterMode::Linear);
     sampler.anisotropy_clamp=9;sampler.mipmap_filter=bevy::image::ImageFilterMode::Linear;
     assert_eq!(copy.data,image.data);assert_eq!(copy.texture_descriptor.format,image.texture_descriptor.format);assert_eq!(copy.texture_descriptor.mip_level_count,image.texture_descriptor.mip_level_count);
     private_sampling_rows.push(serde_json::json!({"original_handle":format!("{:?}",image_handle.id()),"mips":copy.texture_descriptor.mip_level_count,"format":format!("{:?}",copy.texture_descriptor.format),"original_texel_bytes_unchanged":true,"source_quality":"ForceEnable","effective_anisotropy":9,"effective_mipmap_filter":"Linear"}));
     let sampled=app.world_mut().resource_mut::<Assets<Image>>().add(copy);shared_quality_images.insert(image_handle.id(),sampled.clone());Some(sampled)
    }
   }else{None};replacements.push(replacement);
  }
  let mut assets=app.world_mut().resource_mut::<Assets<SourceGraphMaterial>>();let e=&mut assets.get_mut(handle).unwrap().extension;
  e.texture_0=replacements[0].clone();e.texture_1=replacements[1].clone();e.texture_2=replacements[2].clone();e.texture_3=replacements[3].clone();e.texture_4=replacements[4].clone();e.texture_5=replacements[5].clone();e.texture_6=replacements[6].clone();
 }
 std::fs::write(folder.join("SAMPLING-POLICY.json"),serde_json::to_vec_pretty(&private_sampling_rows)?)?;
'''
restore=r'''
 for (handle,originals) in &original_sampling_rows{let mut assets=app.world_mut().resource_mut::<Assets<SourceGraphMaterial>>();let e=&mut assets.get_mut(handle).unwrap().extension;e.texture_0=originals[0].clone();e.texture_1=originals[1].clone();e.texture_2=originals[2].clone();e.texture_3=originals[3].clone();e.texture_4=originals[4].clone();e.texture_5=originals[5].clone();e.texture_6=originals[6].clone();}
'''
inject="\n    sampler_code=r'''"+palette_code+"'''\n    sampler_restore=r'''"+restore+"'''\n    needle=' let prototype=app.world().resource::<Assets<SourceGraphMaterial>>()'\n    assert rust.count(needle)==1\n    rust=rust.replace(needle,sampler_code+needle)\n    needle=' let routes:Vec<_>=selected_draws.iter()'\n    assert rust.count(needle)==1\n    rust=rust.replace(needle,sampler_restore+needle)\n"
source=source.replace(marker,marker+inject)

if __name__=='__main__':
    import sys
    if sys.argv[-1]=='prepare':
        assert not ROOT.exists()
        quality=json.loads((W/'firepit-source-no-anisotropy-control-v1/QUALITY-STATE.json').read_text('utf-8-sig'))
        assert quality['anisotropic_filtering']=='ForceEnable'
    exec(compile(source,str(BASE),'exec'),globals())
