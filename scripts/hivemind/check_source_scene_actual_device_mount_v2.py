"""Repair the boxed SDK setting and additionally verify source texture residency."""
from pathlib import Path
import importlib.util,json,sys
BASE=Path(__file__).with_name('check_source_scene_actual_device_mount_v1.py')
spec=importlib.util.spec_from_file_location('device_mount_base',BASE);base=importlib.util.module_from_spec(spec);spec.loader.exec_module(base)
ROOT=base.W/'studio-source-actual-device-mount-v2'
RESIDENCY=r'''
 let mut expected_images=BTreeMap::new();
 for h in materials.values(){let material=app.world().resource::<Assets<SourceGraphMaterial>>().get(h).unwrap();let e=&material.extension;
  for image in [e.texture_0.as_ref(),e.texture_1.as_ref(),e.texture_2.as_ref(),e.texture_3.as_ref(),e.texture_4.as_ref(),e.texture_5.as_ref(),e.texture_6.as_ref()].into_iter().flatten(){
   let cpu=app.world().resource::<Assets<Image>>().get(image).unwrap();assert!(cpu.data.as_ref().is_some_and(|data|!data.is_empty()));expected_images.insert(image.id(),cpu.texture_descriptor.clone());
  }
 }
 assert!(!expected_images.is_empty());
 for _ in 0..60 {app.update();let resident=app.get_sub_app(RenderApp).unwrap().world().resource::<bevy::render::render_asset::RenderAssets<bevy::render::texture::GpuImage>>();if expected_images.keys().all(|id|resident.get(*id).is_some()){break;}std::thread::sleep(Duration::from_millis(10));}
 let gpu_images=app.get_sub_app(RenderApp).unwrap().world().resource::<bevy::render::render_asset::RenderAssets<bevy::render::texture::GpuImage>>();
 let mut gpu_image_rows=Vec::new();for (id,expected) in expected_images {let actual=gpu_images.get(id).ok_or("Captured source texture did not become GPU resident")?;assert!(actual.had_data);assert_eq!(actual.texture_descriptor,expected);gpu_image_rows.push(serde_json::json!({"image_asset_id":format!("{id:?}"),"format":format!("{:?}",actual.texture_descriptor.format),"width":actual.texture_descriptor.size.width,"height":actual.texture_descriptor.size.height,"mips":actual.texture_descriptor.mip_level_count,"had_source_data":actual.had_data}));}
'''
def patched_code():
    text=BASE.read_text('utf-8');assert text.count("ROOT=W/'studio-source-actual-device-mount-v1'")==1
    text=text.replace("ROOT=W/'studio-source-actual-device-mount-v1'","ROOT=W/'studio-source-actual-device-mount-v2'")
    text=text.replace("name='openrpg-source-actual-device-mount'","name='openrpg-source-actual-device-mount-v2'")
    assert text.count('RenderCreation::Automatic(WgpuSettings{')==1 and text.count('force_fallback_adapter:false,..default()}),..default()}')==1
    text=text.replace('RenderCreation::Automatic(WgpuSettings{','RenderCreation::Automatic(Box::new(WgpuSettings{').replace('force_fallback_adapter:false,..default()}),..default()}','force_fallback_adapter:false,..default()})),..default()}')
    needle='source_graph_runtime::retain(app.world_mut(),host,policy)?;app.update();';assert text.count(needle)==1
    text=text.replace(needle,RESIDENCY+'\n'+needle)
    needle='"new_shader_assets":after.2-before.2,';assert text.count(needle)==1
    text=text.replace(needle,needle+'"gpu_images_resident":gpu_image_rows.len(),"gpu_image_rows":gpu_image_rows,')
    return text
def main():
    # Apply only these explicit harness changes; product modules stay exact.
    exec(compile(patched_code(),str(BASE),'exec'),base.__dict__)
    base.__file__=str(Path(__file__).resolve())
    if sys.argv[-1]=='prepare':
        assert base.read(base.W/'studio-source-actual-device-mount-v1/RUN-RESULT.v1.json')['actual_exit_code']==101
        base.prepare()
    else:base.run()
if __name__=='__main__':main()
