"""Private single-variable shader control of native point world-depth offset."""
from pathlib import Path
import copy,datetime,hashlib,json,struct
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-point-world-bias-control-case-v1'
OLD=W/'firepit-source-receiver-coordinates-case-v3'
SDK=Path('C:/Users/karen/.cargo/registry/src/index.crates.io-1949cf8c6b5b557f/bevy_pbr-0.19.1/src/render/shadows.wgsl')
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def main():
    assert not ROOT.exists()
    proof=read(OLD/'RESULT.v1.json');case=read(OLD/'CASE.v1.json')
    observed=W/'firepit-point-shadow-diagnostics-reopen-v1/RESULT.v1.json';verified=read(observed)
    for p,h in {**proof['input_pins'],**verified['input_pins']}.items():assert sha(p)==h,p
    native=SDK.read_text('utf-8');helper=native.split('fn fetch_point_shadow(',1)[1].split('fn fetch_spot_shadow(',1)[0]
    helper='\nfn private_point_shadow_without_world_depth_offset('+helper
    old='let depth_offset = (*light).shadow_depth_bias * normalize(surface_to_light.xyz);'
    assert helper.count(old)==1
    helper=helper.replace(old,'let depth_offset = vec3<f32>(0.0);')
    helper=helper.replace('frag_ls * flip_z','frag_ls * vec3<f32>(1.0,1.0,-1.0)')
    helper=helper.replace('return sample_shadow_cubemap_pcss(','return source_shadow_sampling::sample_shadow_cubemap_pcss(').replace('return sample_shadow_cubemap(','return source_shadow_sampling::sample_shadow_cubemap(')
    def mutate(text):
        assert text.count('shadows::fetch_point_shadow(')==2 and 'shadow_sampling as source_shadow_sampling' in text
        return text.replace('shadows::fetch_point_shadow(','private_point_shadow_without_world_depth_offset(')+helper
    fixture=Path(proof['private_fixture']);assert sha(fixture)==proof['private_fixture_sha256']
    original=fixture.read_bytes();n=struct.unpack_from('<I',original,12)[0];doc=json.loads(original[20:20+n]);binary=bytearray(original[28+n:]);original_binary=bytes(binary)
    geometry=copy.deepcopy({k:doc[k] for k in ['nodes','meshes','accessors']});changes=[]
    for m in doc['materials']:
        slot=m['extensions']['OPENRPG_source_graph']['forward'];v=doc['bufferViews'][slot['buffer_view']];data=bytes(binary[v.get('byteOffset',0):v.get('byteOffset',0)+v['byteLength']]);assert hashlib.sha256(data).hexdigest()==slot['sha256']
        changed=mutate(data.decode()).encode();binary.extend(b'\0'*((-len(binary))%4));nv={'buffer':0,'byteOffset':len(binary),'byteLength':len(changed)};binary.extend(changed)
        slot.update(buffer_view=len(doc['bufferViews']),sha256=hashlib.sha256(changed).hexdigest());doc['bufferViews'].append(nv);changes.append({'material':m['name'],'old_sha256':hashlib.sha256(data).hexdigest(),'new_sha256':slot['sha256']})
    assert bytes(binary[:len(original_binary)])==original_binary and geometry=={k:doc[k] for k in geometry}
    receiver=Path(case['receiver_program']['forward']['path']);assert sha(receiver)==case['receiver_program']['forward']['sha256']
    ROOT.mkdir();target=ROOT/'private-receiver-forward.wgsl';target.write_text(mutate(receiver.read_text('utf-8')),'utf-8');case['receiver_program']['forward'].update(path=str(target),sha256=sha(target))
    doc['buffers'][0]['byteLength']=len(binary);binary.extend(b'\0'*((-len(binary))%4));encoded=json.dumps(doc,separators=(',',':')).encode();encoded+=b' '*((-len(encoded))%4)
    out=ROOT/'private-point-world-bias-control.glb';out.write_bytes(b'glTF'+struct.pack('<II',2,28+len(encoded)+len(binary))+struct.pack('<I',len(encoded))+b'JSON'+encoded+struct.pack('<I',len(binary))+b'BIN\0'+binary)
    case.update(fixture=str(out),fixture_sha256=sha(out),private_point_world_depth_offset_control={'source_sdk_world_offset_absent':True,'native_world_offset_m':verified['native_observations'][0]['world_depth_offset_m'],'shader_world_offset_m':0.0,'normal_bias_projection_filter_and_point_gpu_record_preserved':True,'full_import':False})
    cp=ROOT/'CASE.v1.json';cp.write_text(json.dumps(case,indent=2),'utf-8')
    files=[Path(__file__),SDK,observed,OLD/'RESULT.v1.json',OLD/'CASE.v1.json',fixture,receiver,out,target,cp]
    result={'status':'private_point_world_depth_offset_single_variable_control_prepared','created_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'private_fixture':str(out),'private_fixture_sha256':sha(out),'original_binary_prefix_unchanged':len(original_binary),'nodes_meshes_accessors_unchanged':True,'shader_changes':changes,'source_punctual_filter_matched':False,'full_import':False,'complete_assets_delivered_increment':0,'input_pins':{**proof['input_pins'],**verified['input_pins'],**{str(p):sha(p) for p in files}}}
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({'root':str(ROOT),'single_variable_control':True}))
if __name__=='__main__':main()
