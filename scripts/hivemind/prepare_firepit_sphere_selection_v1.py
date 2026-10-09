"""Single-variable private replay: source sphere selector, existing fade retained."""
from pathlib import Path
import base64, copy, datetime, hashlib, json, struct

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'firepit-source-sphere-selection-case-v1'
OLD=W/'firepit-source-shadow-oriented-case-v1'

def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def read(p): return json.loads(Path(p).read_text('utf-8-sig'))

def main():
    assert not ROOT.exists()
    proof=read(OLD/'RESULT.v1.json');case=read(OLD/'CASE.v1.json')
    source_receipt=W/'firepit-source-all-lights-gpu-reopen-v2/RESULT.v1.json'
    source_proof=read(source_receipt)
    for p,h in {**proof['input_pins'],**source_proof['input_pins']}.items(): assert sha(p)==h,p
    state_path=W/'firepit-source-all-lights-gpu-v1/SOURCE-STATE-0.json'
    state=read(state_path)
    native=struct.unpack('<26f',base64.b64decode(state['directional_shadow_gpu_base64']))
    spheres=[list(native[4*i:4*i+4]) for i in range(4)]
    assert spheres==source_proof['directional_cascade_spheres']
    assert native[19]==4 and all(row[3]>0 for row in spheres)
    eye=[case['source_camera']['position'][k] for k in 'xyz']
    literal=lambda values: ','.join(repr(float(v)) for v in values)
    helper='\nfn private_source_sphere_cascade(position:vec4<f32>) -> u32 {\n'
    helper+=' let rws=vec3<f32>(-position.x,position.y-1024.0,position.z)-vec3<f32>('+literal(eye)+');\n'
    helper+=' let spheres=array<vec4<f32>,4>('+','.join('vec4<f32>('+literal(row)+')' for row in spheres)+');\n'
    helper+=' for(var i=0u;i<4u;i+=1u){let delta=rws-spheres[i].xyz;let rel=dot(delta,delta)/spheres[i].w;if(rel>0.0 && rel<=1.0){return i;}}\n return 4u;\n}\n'
    before='let index=shadows::get_cascade_index(light_id,view_z);'
    after='let index=private_source_sphere_cascade(position);'
    def mutate(text):
        assert text.count(before)==1
        return text.replace(before,after)+helper
    fixture=Path(proof['private_fixture']);assert sha(fixture)==proof['private_fixture_sha256']
    original=fixture.read_bytes();n=struct.unpack_from('<I',original,12)[0]
    doc=json.loads(original[20:20+n]);binary=bytearray(original[28+n:]);original_binary=bytes(binary)
    geometry=copy.deepcopy({k:doc[k] for k in ['nodes','meshes','accessors']})
    changes=[]
    for material in doc['materials']:
        slot=material['extensions']['OPENRPG_source_graph']['forward'];view=doc['bufferViews'][slot['buffer_view']]
        data=bytes(binary[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']])
        assert hashlib.sha256(data).hexdigest()==slot['sha256']
        changed=mutate(data.decode()).encode();binary.extend(b'\0'*((-len(binary))%4))
        new_view={'buffer':0,'byteOffset':len(binary),'byteLength':len(changed)};binary.extend(changed)
        slot.update(buffer_view=len(doc['bufferViews']),sha256=hashlib.sha256(changed).hexdigest());doc['bufferViews'].append(new_view)
        changes.append({'material':material['name'],'old_sha256':hashlib.sha256(data).hexdigest(),'new_sha256':slot['sha256']})
    assert bytes(binary[:len(original_binary)])==original_binary
    assert geometry=={k:doc[k] for k in geometry}
    receiver=Path(case['receiver_program']['forward']['path']);assert sha(receiver)==case['receiver_program']['forward']['sha256']
    ROOT.mkdir()
    target=ROOT/'private-receiver-forward.wgsl';target.write_text(mutate(receiver.read_text('utf-8')),'utf-8')
    case['receiver_program']['forward'].update(path=str(target),sha256=sha(target))
    doc['buffers'][0]['byteLength']=len(binary);binary.extend(b'\0'*((-len(binary))%4))
    encoded=json.dumps(doc,separators=(',',':')).encode();encoded+=b' '*((-len(encoded))%4)
    out=ROOT/'private-sphere-selection-scene.glb'
    out.write_bytes(b'glTF'+struct.pack('<II',2,28+len(encoded)+len(binary))+struct.pack('<I',len(encoded))+b'JSON'+encoded+struct.pack('<I',len(binary))+b'BIN\0'+binary)
    case.update(fixture=str(out),fixture_sha256=sha(out),private_source_sphere_selection_replay={'spheres_rws':spheres,'source_eye':eye,'target_root_offset':[0,1024,0],'existing_view_depth_overlap_fade_retained':True,'source_cascade_border_and_dither_replayed':False,'single_variable_selection_control':True})
    cp=ROOT/'CASE.v1.json';cp.write_text(json.dumps(case,indent=2),'utf-8')
    files=[Path(__file__).resolve(),source_receipt,state_path,OLD/'RESULT.v1.json',OLD/'CASE.v1.json',fixture,receiver,out,target,cp]
    pins={**proof['input_pins'],**source_proof['input_pins'],**{str(p):sha(p) for p in files}}
    result={'status':'private_measured_source_sphere_selection_control_prepared','created_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'private_fixture':str(out),'private_fixture_sha256':sha(out),'original_binary_prefix_unchanged':len(original_binary),'nodes_meshes_accessors_unchanged':True,'shader_changes':changes,'receiver_shader_changed':True,'source_cascade_borders_matched':False,'full_import':False,'complete_assets_delivered_increment':0,'input_pins':pins}
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({'root':str(ROOT),'shader_changes':len(changes),'single_variable_control':True}))

if __name__=='__main__': main()
