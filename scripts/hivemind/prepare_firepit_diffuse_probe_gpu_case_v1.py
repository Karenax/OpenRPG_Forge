"""Private six-direction SH witness; not a replacement converted material."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,struct
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-diffuse-probe-gpu-case-v1'
ENGINE=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_engine/src')
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    case_path=W/'firepit-shadow-comparison-case-v1/CASE.v1.json';case=read(case_path)
    fixture=Path(case['fixture']);assert sha(fixture)==case['fixture_sha256']
    environment=W/'firepit-shadow-source-reference-v11/SOURCE-STATE-0.json'
    audit=W/'firepit-shadow-effective-environment-v2/RESULT.v1.json';proof=read(audit)
    for p,h in proof['input_pins'].items():assert sha(p)==h,p
    state=read(environment);assert state['source_history_channel']=='CustomUserHistory0'
    raw=fixture.read_bytes();length=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+length]);bin_data=bytearray(raw[28+length:])
    assert struct.unpack_from('<I',raw,24+length)[0]==0x004e4942
    helper=ENGINE/'shaders/source_diffuse_probe.wgsl';helper_text=helper.read_text('utf-8')
    tail='out.color=source_graph_native_light_transport(p,surface);'
    witness='let witness_index=(u32(in.position.x)+u32(in.position.y))%6u;let witness_normals=array<vec3<f32>,6>(vec3<f32>(1.0,0.0,0.0),vec3<f32>(-1.0,0.0,0.0),vec3<f32>(0.0,1.0,0.0),vec3<f32>(0.0,-1.0,0.0),vec3<f32>(0.0,0.0,1.0),vec3<f32>(0.0,0.0,-1.0));out.color=vec4<f32>(source_preconvolved_diffuse_irradiance(witness_normals[witness_index]),0.5);'
    replacements={};rows=[]
    for material in doc['materials']:
        contract=material['extensions']['OPENRPG_source_graph'];original=contract['forward']
        key=(original['buffer_view'],original['sha256'])
        if key not in replacements:
            view=doc['bufferViews'][original['buffer_view']];offset=view.get('byteOffset',0);data=bytes(bin_data[offset:offset+view['byteLength']])
            assert hashlib.sha256(data).hexdigest()==original['sha256']
            text=data.decode('utf-8');assert text.count(tail)==1
            diagnostic=(text.replace(tail,witness)+'\n'+helper_text).encode()
            while len(bin_data)%4:bin_data.append(0)
            new_view=len(doc['bufferViews']);doc['bufferViews'].append(dict(buffer=0,byteOffset=len(bin_data),byteLength=len(diagnostic)));bin_data.extend(diagnostic)
            replacements[key]=dict(buffer_view=new_view,sha256=hashlib.sha256(diagnostic).hexdigest())
            rows.append(dict(original=original.copy(),diagnostic=replacements[key],diagnostic_fragment_only=True))
        contract['forward']=replacements[key].copy()
    while len(bin_data)%4:bin_data.append(0)
    doc['buffers'][0]['byteLength']=len(bin_data)
    doc.setdefault('extras',{})['private_diffuse_probe_witness']=dict(not_deliverable=True,original_fixture_sha256=case['fixture_sha256'])
    encoded=json.dumps(doc,separators=(',',':')).encode();encoded+=b' '*((-len(encoded))%4)
    output=struct.pack('<III',0x46546c67,2,28+len(encoded)+len(bin_data))+struct.pack('<II',len(encoded),0x4e4f534a)+encoded+struct.pack('<II',len(bin_data),0x004e4942)+bin_data
    ROOT.mkdir();derived=ROOT/'private-six-direction-witness.glb';derived.write_bytes(output)
    case.update(fixture=str(derived),fixture_sha256=sha(derived),diffuse_probe_coefficients=state['hdrp_diffuse_probe_buffer'],diffuse_probe_source=str(environment),diffuse_probe_source_sha256=sha(environment),diagnostic_only=True)
    (ROOT/'CASE.v1.json').write_text(json.dumps(case,indent=2),'utf-8')
    result=dict(status='six_direction_gpu_witness_prepared_before_pixels',created_utc=datetime.now(timezone.utc).isoformat(),private_fixture=str(derived),private_fixture_sha256=sha(derived),forward_stage_deltas=rows,expected_directions=6,
        expected_target_to_source_basis=[-1,1,1],witness_alpha=0.5,relative_channel_error_limit=2**-10,absolute_channel_error_floor=0.0001,minimum_samples_per_direction=100,
        mathematical_irradiance_only=True,brdf_qualified=False,exposure_qualified=False,full_import=False,editor_visual_verified=False,
        input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),helper,case_path,fixture,environment,audit,ROOT/'CASE.v1.json']})
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
