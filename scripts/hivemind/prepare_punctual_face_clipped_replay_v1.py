"""Diagnostic clipping of existing source triangles; no delivered mesh or pixel replay."""
from pathlib import Path
import hashlib,json,shutil
import numpy as np
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-punctual-d16-caster-face-clipped-v1';BASE=W/'firepit-punctual-d16-caster-shader-mapped-v1';CLIPS=W/'firepit-punctual-native-caster-clip-coordinates-v1'
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def clip(poly,axis,sign):
 if not poly:return []
 out=[];prev=poly[-1];pd=np.float32(prev[3]+np.float32(sign)*prev[axis]);pin=pd>=0
 for current in poly:
  cd=np.float32(current[3]+np.float32(sign)*current[axis]);inside=cd>=0
  if pin!=inside:
   t=np.float32(pd/np.float32(pd-cd));out.append(np.float32(prev+np.float32((current-prev)*t)))
  if inside:out.append(current.copy())
  prev=current;pd=cd;pin=inside
 return out

def main():
 assert not ROOT.exists();ROOT.mkdir();(ROOT/'src').mkdir()
 indices=np.fromfile(BASE/'indices.uint32',dtype='<u4').reshape(-1,3);assert len(indices)==2819
 stats=[];inputs={}
 for face in range(6):
  source=CLIPS/f'clip-face-{face}.float32';points=np.fromfile(source,dtype='<f4').reshape(3381,4);inputs[str(source)]=sha(source)
  result=[];retained=0
  for tri in indices:
   poly=[points[i].copy() for i in tri]
   for axis,sign in [(0,1),(0,-1),(1,1),(1,-1)]:poly=clip(poly,axis,sign)
   if len(poly)>=3:
    retained+=1
    for i in range(1,len(poly)-1):result.extend([poly[0],poly[i],poly[i+1]])
  vertices=np.asarray(result,dtype='<f4').reshape(-1,4);assert np.isfinite(vertices).all();vertices.tofile(ROOT/f'face-{face}.vertices.float32')
  stats.append({'face':face,'original_triangles':2819,'retained_original_triangles':retained,'output_vertices':len(vertices),'output_triangles':len(vertices)//3})
  shutil.copy2(BASE/f'face-{face}.uniform',ROOT/f'face-{face}.uniform')
 text=(BASE/'src/main.rs').read_text(encoding='utf-8')
 old='let vertices=device.create_buffer_init(&util::BufferInitDescriptor{label:Some("original-source-lod0-positions"),contents:&std::fs::read("vertices.float32")?,usage:BufferUsages::VERTEX});'
 new='''let mut face_vertices=Vec::new();let mut face_vertex_counts=Vec::new();
 for i in 0..6 {let bytes=std::fs::read(format!("face-{i}.vertices.float32"))?;assert_eq!(bytes.len()%48,0);face_vertex_counts.push((bytes.len()/16) as u32);face_vertices.push(device.create_buffer_init(&util::BufferInitDescriptor{label:Some("existing-source-triangles-clipped-to-face"),contents:&bytes,usage:BufferUsages::VERTEX}));}'''
 assert text.count(old)==1;text=text.replace(old,new)
 text=text.replace('let indices=device.create_buffer_init(&util::BufferInitDescriptor{label:Some("original-source-lod0-indices"),contents:&std::fs::read("indices.uint32")?,usage:BufferUsages::INDEX});','')
 text=text.replace('@vertex fn vertex(@location(0) position:vec3<f32>)','@vertex fn vertex(@location(0) position:vec4<f32>)')
 text=text.replace('return source_punctual_atlas_position(face.clip_from_source_rws*vec4(position-face.camera.xyz,1.),face.rect,face.extent.xy);','return source_punctual_atlas_position(position,face.rect,face.extent.xy);')
 text=text.replace('array_stride:12','array_stride:16').replace('format:VertexFormat::Float32x3','format:VertexFormat::Float32x4')
 old='pass.set_vertex_buffer(0,vertices.slice(..));pass.set_index_buffer(indices.slice(..),IndexFormat::Uint32);pass.draw_indexed(0..8457,0,0..1);'
 assert text.count(old)==1;text=text.replace(old,'pass.set_vertex_buffer(0,face_vertices[index].slice(..));pass.draw(0..face_vertex_counts[index],0..1);')
 text=text.replace('shader_mapped_original_lod0_d16_depth_replay_captured_comparison_pending','preclipped_source_face_geometry_depth_captured_comparison_pending')
 (ROOT/'src/main.rs').write_text(text,encoding='utf-8');(ROOT/'Cargo.toml').write_text((BASE/'Cargo.toml').read_text().replace('firepit-punctual-d16-caster-shader-mapped-v1','firepit-punctual-d16-caster-face-clipped-v1'),encoding='utf-8')
 for p in [BASE/'indices.uint32',BASE/'src/main.rs',Path(__file__),ROOT/'src/main.rs',ROOT/'Cargo.toml',*ROOT.glob('face-*')]:inputs[str(p)]=sha(p)
 (ROOT/'CASE.v1.json').write_text(json.dumps({'status':'source_triangles_xy_frustum_clipped_diagnostic_prepared','input_pins':inputs,'face_statistics':stats,'input_clips_produced_by_forge_gpu':True,'source_unity_clips_used_as_render_input':False,'source_pixels_used_as_render_input':False,'float_arithmetic':'f32','near_far_clipping':'original hardware Z/W unchanged','predeclared_max_depth_error':2/65535,'full_import':False,'editor_verified':False},indent=2),encoding='utf-8')
 print(json.dumps({'root':str(ROOT),'face_statistics':stats}))
if __name__=='__main__':main()
