"""Capture native SSAO visibility at the actual model/receiver fragments."""
from pathlib import Path
from datetime import datetime,timezone
import json,struct,hashlib
W=Path('F:/OpenRPG_ImportWork');OLD=W/'firepit-specular-scene-case-v2';ROOT=W/'firepit-ssao-native-case-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def diagnostic(text):
    marker='out.color=source_graph_native_light_transport(p,surface);';assert text.count(marker)==1
    return text.replace(marker,'''var ssao_visibility=1.0;
#ifdef SCREEN_SPACE_AMBIENT_OCCLUSION
 ssao_visibility=textureLoad(view_bindings::screen_space_ambient_occlusion_texture,vec2<i32>(in.position.xy),0i).r;
#endif
 out.color=vec4<f32>(vec3<f32>(ssao_visibility),0.5);''')
def main():
    assert not ROOT.exists()
    protocol=read(OLD/'RESULT.v1.json');case=read(OLD/'CASE.v1.json');fixture=Path(case['fixture'])
    assert sha(fixture)==case['fixture_sha256']
    for p,h in protocol['input_pins'].items():assert sha(p)==h,p
    raw=fixture.read_bytes();n=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+n]);binary=bytearray(raw[28+n:])
    deltas=[];stages={}
    for material in doc['materials']:
        c=material['extensions']['OPENRPG_source_graph'];before=c['forward'];key=(before['buffer_view'],before['sha256'])
        if key not in stages:
            view=doc['bufferViews'][before['buffer_view']];start=view.get('byteOffset',0);data=bytes(binary[start:start+view['byteLength']]);assert hashlib.sha256(data).hexdigest()==before['sha256']
            data=diagnostic(data.decode('utf-8')).encode()
            while len(binary)%4:binary.append(0)
            after=dict(buffer_view=len(doc['bufferViews']),sha256=hashlib.sha256(data).hexdigest())
            doc['bufferViews'].append(dict(buffer=0,byteOffset=len(binary),byteLength=len(data)));binary.extend(data);stages[key]=after
            original=next(v['original'] for v in protocol['forward_deltas'] if v['derived']==before)
            deltas.append(dict(original=original,derived=after,diagnostic_fragment_only=True))
        c['forward']=stages[key].copy()
    while len(binary)%4:binary.append(0)
    doc['buffers'][0]['byteLength']=len(binary)
    encoded=json.dumps(doc,separators=(',',':')).encode();encoded+=b' '*((-len(encoded))%4)
    output=struct.pack('<III',0x46546c67,2,28+len(encoded)+len(binary))+struct.pack('<II',len(encoded),0x4e4f534a)+encoded+struct.pack('<II',len(binary),0x004e4942)+binary
    ROOT.mkdir();derived=ROOT/'private-ssao-visibility.glb';derived.write_bytes(output)
    ground={};original_receivers=[]
    for name,item in case['receiver_program'].items():
        p=Path(item['path']);assert sha(p)==item['sha256'];original_receivers.append(p)
        text=p.read_text('utf-8');text=diagnostic(text) if name=='forward' else text
        p=ROOT/f'private-receiver-{name}.wgsl';p.write_text(text,'utf-8');ground[name]=dict(path=str(p),sha256=sha(p))
    case.update(fixture=str(derived),fixture_sha256=sha(derived),receiver_program=ground,native_ssao_visibility_diagnostic=True)
    (ROOT/'CASE.v1.json').write_text(json.dumps(case,indent=2),'utf-8')
    pins=[Path(__file__).resolve(),OLD/'RESULT.v1.json',OLD/'CASE.v1.json',fixture,*original_receivers,ROOT/'CASE.v1.json',*[Path(v['path']) for v in ground.values()]]
    result=dict(status='native_ssao_fragment_visibility_diagnostic_prepared_before_pixels',created_utc=datetime.now(timezone.utc).isoformat(),private_fixture=str(derived),private_fixture_sha256=sha(derived),forward_deltas=deltas,witness_alpha=.5,expected_views=2,minimum_visibility=0.0,maximum_visibility=1.0,source_ssao_pixels_used_as_target_input=False,native_ssao_settings_changed=False,full_import=False,editor_visual_verified=False,input_pins={str(p):sha(p) for p in pins})
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
