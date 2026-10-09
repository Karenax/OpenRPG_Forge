"""Override only a private borrowed texture with same-size mip ID palette."""
from pathlib import Path

BASE=Path(__file__).with_name('capture_firepit_surface_camera_native_v1.py')
source=BASE.read_text('utf-8').replace('firepit-surface-camera-native-v1','firepit-mip-palette-native-v1')
source=source.replace("CASE=W/'firepit-surface-native-case-v1'","CASE=W/'firepit-mip-palette-native-case-v1'")
marker='    rust=rust.replace(marker,observe+marker)'
inject=r'''
    palette_code=r"""
 let original_texture_rows:Vec<_>=materials.values().map(|handle|(handle.clone(),app.world().resource::<Assets<SourceGraphMaterial>>().get(handle).unwrap().extension.texture_0.clone())).collect();
 let original_handle=original_texture_rows[0].1.clone().ok_or("Actual source texture absent")?;
 let mut palette=app.world().resource::<Assets<Image>>().get(&original_handle).unwrap().clone();
 assert_eq!(palette.texture_descriptor.size.width,2048);assert_eq!(palette.texture_descriptor.size.height,2048);assert_eq!(palette.texture_descriptor.mip_level_count,12);
 palette.texture_descriptor.format=bevy::render::render_resource::TextureFormat::Rgba8Unorm;
 palette.texture_view_descriptor=None;
 let mut palette_bytes=Vec::new();for mip in 0..12u32{let edge=(2048u32>>mip).max(1);let value=((mip as f64)*255.0/16.0).round() as u8;for _ in 0..edge*edge{palette_bytes.extend_from_slice(&[value,value,value,255]);}}
 palette.data=Some(palette_bytes);
 let palette_handle=app.world_mut().resource_mut::<Assets<Image>>().add(palette);
 for handle in materials.values(){app.world_mut().resource_mut::<Assets<SourceGraphMaterial>>().get_mut(handle).unwrap().extension.texture_0=Some(palette_handle.clone());}
 """
    needle=' let prototype=app.world().resource::<Assets<SourceGraphMaterial>>()'
    assert rust.count(needle)==1
    rust=rust.replace(needle,palette_code+needle)
    needle=' let routes:Vec<_>=selected_draws.iter()'
    assert rust.count(needle)==1
    restore=' for (handle,texture) in &original_texture_rows {app.world_mut().resource_mut::<Assets<SourceGraphMaterial>>().get_mut(handle).unwrap().extension.texture_0=texture.clone();}\n'
    rust=rust.replace(needle,restore+needle)
'''
assert source.count(marker)==1
source=source.replace(marker,marker+inject)
exec(compile(source,str(BASE),'exec'),globals())
