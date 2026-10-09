"""Private instance transport candidate; all original buffers remain preserved."""
from pathlib import Path
from datetime import datetime,timezone
import json,struct,hashlib
from source_graph_instance_index_transport_v1 import repair_vertex_instance_transport
W=Path('F:/OpenRPG_ImportWork');OLD=W/'firepit-gpu-frame-matrix-case-v1';ROOT=W/'firepit-gpu-frame-instance-fix-case-v1'
SDK=Path('C:/Users/karen/.cargo/registry/src/index.crates.io-1949cf8c6b5b557f/bevy_pbr-0.19.1/src/render/mesh.wgsl')
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    assert 'work around a wgpu dx12 bug' in SDK.read_text('utf-8')
    prior=read(OLD/'RESULT.v1.json');case=read(OLD/'CASE.v1.json');fixture=Path(case['fixture']);assert sha(fixture)==case['fixture_sha256']
    for p,h in prior['input_pins'].items():assert sha(p)==h,p
    b=fixture.read_bytes();n=struct.unpack_from('<I',b,12)[0];d=json.loads(b[20:20+n]);binary=bytearray(b[28+n:]);original=bytes(binary);stages={};changes=[]
    for material in d['materials']:
        graph=material['extensions']['OPENRPG_source_graph']
        for stage in ['forward','prepass']:
            item=graph[stage];key=(item['buffer_view'],item['sha256'])
            if key not in stages:
                v=d['bufferViews'][item['buffer_view']];data=bytes(binary[v.get('byteOffset',0):v.get('byteOffset',0)+v['byteLength']]);assert hashlib.sha256(data).hexdigest()==item['sha256']
                patched=repair_vertex_instance_transport(data.decode()).encode()
                while len(binary)%4:binary.append(0)
                after=dict(buffer_view=len(d['bufferViews']),sha256=hashlib.sha256(patched).hexdigest());d['bufferViews'].append(dict(buffer=0,byteOffset=len(binary),byteLength=len(patched)));binary.extend(patched);stages[key]=after;changes.append(dict(stage=stage,before=item,after=after))
            graph[stage]=stages[key].copy()
    while len(binary)%4:binary.append(0)
    d['buffers'][0]['byteLength']=len(binary);d['extras']['private_explicit_instance_transport']=True
    j=json.dumps(d,separators=(',',':')).encode();j+=b' '*((-len(j))%4)
    output=struct.pack('<III',0x46546c67,2,28+len(j)+len(binary))+struct.pack('<II',len(j),0x4e4f534a)+j+struct.pack('<II',len(binary),0x004e4942)+binary
    ROOT.mkdir();p=ROOT/'private-instance-transport.glb';p.write_bytes(output);assert bytes(binary[:len(original)])==original
    files=[];receiver={}
    for stage,item in case['receiver_program'].items():
        src=Path(item['path']);assert sha(src)==item['sha256'];dest=ROOT/f'private-receiver-{stage}.wgsl';dest.write_text(repair_vertex_instance_transport(src.read_text('utf-8')),'utf-8');receiver[stage]=dict(path=str(dest),sha256=sha(dest));files.extend([src,dest])
    case.update(fixture=str(p),fixture_sha256=sha(p),receiver_program=receiver,explicit_instance_index_transport=True)
    (ROOT/'CASE.v1.json').write_text(json.dumps(case,indent=2),'utf-8')
    result={**prior,'status':'explicit_entrypoint_instance_transport_candidate_prepared','created_utc':datetime.now(timezone.utc).isoformat(),'private_fixture':str(p),'private_fixture_sha256':sha(p),'stage_changes':changes,'source_binary_prefix_unchanged':True,'source_binary_bytes':len(original),'current_model_mask':'encoded alpha differs from receiver .25 and clear 1; source matrices finite, upper f32 byte never 255','original_cull_state_unchanged':True,'input_pins':{str(p):sha(p) for p in [Path(__file__).resolve(),Path(__file__).with_name('source_graph_instance_index_transport_v1.py'),SDK,OLD/'RESULT.v1.json',OLD/'CASE.v1.json',fixture,ROOT/'CASE.v1.json',*files,*[Path(p) for p in prior['input_pins']]]}}
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:result[k] for k in ['status','source_binary_bytes','private_fixture']}))
if __name__=='__main__':main()
