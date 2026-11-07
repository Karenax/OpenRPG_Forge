"""Copy the actual native camera depth texture to a GPU readback buffer."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
R = S.parents[1]
example = R / 'crates/forge_engine/examples/verify_source_glb_native_depth_draw.rs'
queue_path = S / 'queue_source_glb_native_depth_draw_v5.py'
assert not example.exists() and not queue_path.exists()
text = (R / 'crates/forge_engine/examples/verify_source_glb_diffuse_material_draw.rs').read_text(encoding='utf-8')
text = text.replace('Camera3d::default(),', 'Camera3d {depth_texture_usages:(TextureUsages::RENDER_ATTACHMENT|TextureUsages::COPY_SRC).into(),..default()},')
anchor = '    let deadline = Instant::now() + Duration::from_secs(90);'
assert text.count(anchor) == 1
text = text.replace(anchor, '''    let depth_pixels:Arc<Mutex<Option<Vec<u8>>>>=Arc::new(Mutex::new(None));
    app.get_sub_app_mut(RenderApp).unwrap().insert_resource(NativeDepthCopy(depth_pixels.clone()))
        .add_systems(bevy::render::Render,copy_actual_native_depth.after(bevy::render::RenderSystems::Render).before(bevy::render::RenderSystems::Cleanup));
    let mut stable_depth=0;let mut previous_depth:Option<Vec<u8>>=None;
''' + anchor)
anchor = '        if frames > 30 && pending == 0 {'
assert text.count(anchor) == 1
text = text.replace(anchor, '''        if frames>30 && pending==0 {
            if let Some(bytes)=depth_pixels.lock().unwrap().take(){
                assert_eq!(bytes.len(),512*512*4);
                let populated=bytes.chunks_exact(4).filter(|chunk|f32::from_le_bytes(chunk.try_into().unwrap())>0.0).count();
                if populated>100 {if previous_depth.as_ref()==Some(&bytes){stable_depth+=1;}else{stable_depth=0;}previous_depth=Some(bytes);}
            }
        }
''' + anchor)
text = text.replace('if stable >= 2 {', 'if stable >= 2 && stable_depth >= 2 {')
anchor = '    let result = serde_json::json!({"status":"actual_source_glb_lod0_camera_draw_and_stable_gpu_readback",'
assert text.count(anchor) == 1
text = text.replace(anchor, '''    let actual_depth=previous_depth.ok_or("Native depth absent")?;
    std::fs::write(root.join("FORGE-DEPTH.r32f"),&actual_depth)?;
    let depth_sha=forge_core::bytes_sha256(&actual_depth);
    let result = serde_json::json!({"native_camera_depth_buffer_captured":true,"native_depth_format":"Depth32Float","native_depth_sha256":depth_sha,"native_depth_consecutive_equal_readbacks":stable_depth+1,"status":"actual_source_glb_lod0_camera_draw_and_stable_gpu_readback",''')
text += '''
#[derive(Resource)]
struct NativeDepthCopy(Arc<Mutex<Option<Vec<u8>>>>);
fn copy_actual_native_depth(
    views:Query<&bevy::render::view::ViewDepthTexture>,
    device:Res<RenderDevice>,queue:Res<bevy::render::renderer::RenderQueue>,capture:Res<NativeDepthCopy>,
){
    let Ok(depth)=views.single() else{return;};
    if depth.texture.width()!=512||depth.texture.height()!=512{return;}
    assert_eq!(depth.texture.format(),TextureFormat::Depth32Float);
    let buffer=device.create_buffer(&BufferDescriptor{label:Some("source-independent-native-depth-readback"),size:512*512*4,usage:BufferUsages::COPY_DST|BufferUsages::MAP_READ,mapped_at_creation:false});
    let mut encoder=device.create_command_encoder(&CommandEncoderDescriptor{label:Some("actual-native-depth-copy")});
    encoder.copy_texture_to_buffer(TexelCopyTextureInfo{texture:&depth.texture,mip_level:0,origin:Origin3d::ZERO,aspect:TextureAspect::DepthOnly},
        TexelCopyBufferInfo{buffer:&buffer,layout:TexelCopyBufferLayout{offset:0,bytes_per_row:Some(512*4),rows_per_image:Some(512)}},Extent3d{width:512,height:512,depth_or_array_layers:1});
    queue.submit([encoder.finish()]);
    let callback_buffer=buffer.clone();let received=capture.0.clone();
    buffer.slice(..).map_async(MapMode::Read,move |result|{
        if result.is_ok(){let bytes=callback_buffer.slice(..).get_mapped_range().to_vec();callback_buffer.unmap();*received.lock().unwrap()=Some(bytes);}
    });
}
'''
example.write_text(text, encoding='utf-8')
queue = (S / 'queue_source_glb_diffuse_material_draw_v3.py').read_text(encoding='utf-8')
queue = queue.replace('townsmith-paint-door-native-diffuse-material-draw-v3', 'townsmith-paint-door-native-depth-draw-v5')
queue = queue.replace('verify_source_glb_diffuse_material_draw', 'verify_source_glb_native_depth_draw')
compile(queue, str(queue_path), 'exec')
queue_path.write_text(queue, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(queue_path)], check=True)
