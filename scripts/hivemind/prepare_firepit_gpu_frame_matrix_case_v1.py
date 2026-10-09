"""Read 96 actual shader matrix scalars via lossless four-byte fragment encoding."""
from pathlib import Path
from datetime import datetime,timezone
import json,struct,hashlib
W=Path('F:/OpenRPG_ImportWork');OLD=W/'firepit-surface-native-case-v1';ROOT=W/'firepit-gpu-frame-matrix-case-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    prior=read(OLD/'RESULT.v1.json');case=read(OLD/'CASE.v1.json');fixture=Path(case['fixture']);assert sha(fixture)==case['fixture_sha256']
    for p,h in prior['input_pins'].items():assert sha(p)==h,p
    b=fixture.read_bytes();n=struct.unpack_from('<I',b,12)[0];d=json.loads(b[20:20+n]);binary=bytearray(b[28+n:]);original=bytes(binary)
    shader=d['materials'][0]['extensions']['OPENRPG_source_graph']['forward'];v=d['bufferViews'][shader['buffer_view']];text=bytes(binary[v.get('byteOffset',0):v.get('byteOffset',0)+v['byteLength']]).decode()
    start=text.index('let surface_mode=');end=text.index('out.color=vec4<f32>(surface_value,0.5);',start)+len('out.color=vec4<f32>(surface_value,0.5);')
    probe='''let frame_scalar=(u32(in.position.x)+512u*u32(in.position.y))%96u;
 let frame_group=frame_scalar/16u;let frame_component=frame_scalar%16u;
 var frame_matrix=view.clip_from_world;
 switch frame_group {
  case 1u:{frame_matrix=view.world_from_view;}
  case 2u:{frame_matrix=mesh_functions::get_world_from_local(in.instance_index);}
  case 3u:{frame_matrix=source_graph.source_object_from_target_object;}
  case 4u:{frame_matrix=source_graph.target_object_from_source_object;}
  case 5u:{frame_matrix=source_graph.source_world_from_target_world;}
  default:{}
 }
 let frame_bits=bitcast<u32>(frame_matrix[frame_component/4u][frame_component%4u]);
 let frame_bytes=vec4<u32>(frame_bits&255u,(frame_bits>>8u)&255u,(frame_bits>>16u)&255u,(frame_bits>>24u)&255u);
 out.color=vec4<f32>(frame_bytes)/255.0;'''
    derived=(text[:start]+probe+text[end:]).encode()
    while len(binary)%4:binary.append(0)
    replacement=dict(buffer_view=len(d['bufferViews']),sha256=hashlib.sha256(derived).hexdigest());d['bufferViews'].append(dict(buffer=0,byteOffset=len(binary),byteLength=len(derived)));binary.extend(derived)
    for m in d['materials']:
        x=m['extensions']['OPENRPG_source_graph'];assert x['forward']==shader;x['forward']=replacement.copy()
    while len(binary)%4:binary.append(0)
    d['buffers'][0]['byteLength']=len(binary);d['extras']['private_gpu_frame_readback']=True
    j=json.dumps(d,separators=(',',':')).encode();j+=b' '*((-len(j))%4)
    out=struct.pack('<III',0x46546c67,2,28+len(j)+len(binary))+struct.pack('<II',len(j),0x4e4f534a)+j+struct.pack('<II',len(binary),0x004e4942)+binary
    ROOT.mkdir();p=ROOT/'private-gpu-frame-matrix.glb';p.write_bytes(out);assert bytes(binary[:len(original)])==original
    case.update(fixture=str(p),fixture_sha256=sha(p),actual_gpu_frame_matrix_diagnostic=True)
    (ROOT/'CASE.v1.json').write_text(json.dumps(case,indent=2),'utf-8')
    result={**prior,'status':'gpu_frame_six_matrices_96_scalars_protocol_prepared','created_utc':datetime.now(timezone.utc).isoformat(),'private_fixture':str(p),'private_fixture_sha256':sha(p),'matrix_labels':['clip_from_world','world_from_view','world_from_local','source_object_from_target_object','target_object_from_source_object','source_world_from_target_world'],'scalar_cases':96,'byte_encoding':'bitcast f32 to u32, little endian bytes /255 in RGBA; round each channel *255 on readback','model_mask_reference':str(W/'firepit-surface-native-v1'),'source_binary_prefix_unchanged':True,'source_binary_bytes':len(original),'original_cull_state_unchanged':True,'minimum_samples_per_scalar':20,'private_control_not_delivered':True,'input_pins':{str(p):sha(p) for p in [Path(__file__).resolve(),OLD/'RESULT.v1.json',OLD/'CASE.v1.json',fixture,ROOT/'CASE.v1.json',*[Path(p) for p in prior['input_pins']]]}}
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:result[k] for k in ['status','scalar_cases','source_binary_bytes']}))
if __name__=='__main__':main()
