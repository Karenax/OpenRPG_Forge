from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,re
S=Path(__file__).parent;R=S.parents[1];W=Path('F:/OpenRPG_ImportWork');ROOT=W/'townsmith-s-paint-normal-abi-v1'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not ROOT.exists()
 source_proof=W/'townsmith-s-paint-native-normal-decode-reopen-v1/RESULT.v1.json';normal=read(source_proof)
 for p,h in normal['input_pins'].items():assert sha(p)==h,p
 common=R/'crates/forge_assets/src/source_graph_bundle.rs';text=common.read_text(encoding='utf-8')
 assert 'Self::RgOrAg => 2.0' in text
 tests=W/'native-texture-identity-contract-check-v2/tests.stdout.log';assert 'normal_decode_wire_modes_keep_the_source_shader_abi ... ok' in tests.read_text(encoding='utf-8')
 program=W/'townsmith-s-paint-wgsl-v1/typed-program-v1/material-program.wgsl';old=program.read_text(encoding='utf-8')
 prefix,body=old.split('// Source graph material arithmetic candidate.',1);assert prefix.count('fn source_unpack_normal_sample(')==1
 prefix=prefix.replace('fn source_unpack_normal_sample(','fn source_unpack_normal_sample_native(')
 wrapper='''// Input modes use the common source-graph ABI, not Unity/prototype numbering.
// RG-or-AG is common mode 2 and native mode 1. Unqualified modes refuse.
fn source_unpack_normal_sample(packed:vec4<f32>,common_mode:u32)->vec4<f32> {
    if common_mode==0u {return source_unpack_normal_sample_native(packed,0u);}
    if common_mode==2u {return source_unpack_normal_sample_native(packed,1u);}
    return vec4<f32>(bitcast<f32>(0x7fc00000u));
}

'''
 mapped=prefix+wrapper+'// Source graph material arithmetic candidate.'+body
 assert body.count('source_unpack_normal_sample(')==3
 rows=[]
 for row in sorted(normal['rows'],key=lambda r:r['normal_profile_index']):
  assert row['native_unpack_mode']==1
  rows.append({'guid':row['guid'],'authored_texture_index':row['authored_texture_index'],'normal_profile_index':row['normal_profile_index'],
    'native_unpack_mode':1,'common_wire_mode':'rg_or_ag','common_shader_value':2.0,'gpu_profile_equivalence_verified':False})
 assert [(r['authored_texture_index'],r['normal_profile_index']) for r in rows]==[(6,0),(7,1),(8,2)]
 ROOT.mkdir();target=ROOT/'material-program-common-normal-abi.wgsl';target.write_text(mapped,encoding='utf-8')
 d={'status':'explicit_three_profile_native_to_common_normal_abi_prepared','prepared_utc':datetime.now(timezone.utc).isoformat(),'rows':rows,
    'normal_decode_modes':['rg_or_ag','rg_or_ag','rg_or_ag',None,None,None,None,None],
    'normal_decode_shader_values':[2.0,2.0,2.0,-1.0,-1.0,-1.0,-1.0,-1.0],
    'material_program':str(target),'material_program_sha256':sha(target),
    'input_pins':{str(p):sha(p) for p in [source_proof,common,program,tests,Path(__file__)]},
    'common_engine_installation_verified':False,'gpu_profile_equivalence_verified':False,'actual_material_render_qualified':False,'full_import':False,'complete_assets_delivered_increment':0}
 (ROOT/'PROFILES.v1.json').write_text(json.dumps(d,indent=2),encoding='utf-8')
 # Derive a GPU control using common wire value 2 with the explicit bridge.
 code=(S/'candidates/SourceNativeTextureLoadV1.rs').read_text(encoding='utf-8').replace('if normal{1u32}else{0}','if normal{2u32}else{0}').replace('prototype_unpack_mode','common_unpack_mode')
 assert 'if normal{2u32}else{0}' in code;(S/'candidates/SourceNativeTextureLoadV2.rs').write_text(code,encoding='utf-8')
 queue=(S/'queue_native_texture_gpu_control_v1.py').read_text(encoding='utf-8').replace('native-texture-gpu-control-v1','native-texture-gpu-control-v2').replace('SourceNativeTextureLoadV1.rs','SourceNativeTextureLoadV2.rs')
 queue=queue.replace("program=W/'townsmith-s-paint-wgsl-v1/typed-program-v1/material-program.wgsl'","program=W/'townsmith-s-paint-normal-abi-v1/material-program-common-normal-abi.wgsl'")
 queue=queue.replace("for p in [code,target,shader,program,inputpath,Path(__file__)]:", "for p in [code,target,shader,program,inputpath,Path(__file__),W/'townsmith-s-paint-normal-abi-v1/PROFILES.v1.json']:")
 (S/'queue_native_texture_gpu_control_v2.py').write_text(queue,encoding='utf-8')
 compare=(S/'reopen_native_texture_gpu_control_v1.py').read_text(encoding='utf-8').replace('native-texture-gpu-control-v1','native-texture-gpu-control-v2').replace('native-texture-gpu-control-reopen-v1','native-texture-gpu-control-reopen-v2')
 compare=compare.replace("normalrow['shader_unpack_mode']==gpu['prototype_unpack_mode']==1","normalrow['shader_unpack_mode']==1 and gpu['common_unpack_mode']==2")
 compare=compare.replace('prototype_normal_decode_controls_qualified','common_wire_normal_decode_controls_qualified')
 compare=compare.replace("'common_decoder_abi_qualified':False", "'common_decoder_scalar_modes_gpu_control_qualified':not failed,'actual_engine_uniform_binding_qualified':False")
 (S/'reopen_native_texture_gpu_control_v2.py').write_text(compare,encoding='utf-8')
 print(json.dumps({'status':d['status'],'profiles':3,'native_mode':1,'common_wire_value':2,'program':str(target)}))
if __name__=='__main__':main()
