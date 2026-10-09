from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,struct,math
W=Path('F:/OpenRPG_ImportWork');SRC=W/'townsmith-s-paint-native-filtered-reference-v1';GPU=W/'townsmith-s-paint-native-filtered-gpu-control-v2';ROOT=W/'townsmith-s-paint-native-filtered-gpu-reopen-v2'
ABSOLUTE_TOLERANCE=1e-6
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 pins={};natives=[]
 for root,exit_keys in [(SRC,['actual_exit_code']),(GPU,['compile_exit_code','gpu_exit_code'])]:
  run=read(root/'RUN-RESULT.v1.json');prep=read(root/'PREPARED.v1.json');native=read(root/'NATIVE-RESULT.v1.json')
  assert all(run[k]==0 for k in exit_keys)
  assert sha(root/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256']
  for entries in [prep['input_pins'],prep.get('isolated_copy_pins',{}),run['artifact_pins']]:
   for p,h in entries.items():assert sha(p)==h,p;pins[p]=h
  for p in [root/'PREPARED.v1.json',root/'RUN-RESULT.v1.json',root/'NATIVE-RESULT.v1.json']:pins[str(p)]=sha(p)
  natives.append(native)
 source,gpu=natives;assert source['status']=='native_filtered_texture_capture_complete' and gpu['status']=='native_filtered_resource_gpu_control_complete'
 lookup={r['guid']:r for r in source['rows']};assert len(lookup)==len(gpu['rows'])==10
 rows=[];refusals=[];loads=normals=0;max_load=max_normal=0.0
 for r in gpu['rows']:
  native=lookup[r['guid']];assert native['settings_unchanged'] and native['settings_before']==native['settings_after'] and not native['error']
  assert native['authored_texture_index']==r['authored_texture_index'] and native['sample_count']==r['samples']==108
  sourcebytes=Path(native['samples_file']).read_bytes();gpubytes=Path(r['file']).read_bytes();assert len(sourcebytes)==108*48 and len(gpubytes)==108*32
  decode=native['normal_profile_index']>=0;assert decode==r['normal_decode_enabled']
  if decode:assert native['shader_unpack_mode']==1 and r['common_unpack_mode']==2
  local_load=local_normal=0.0;local_refusals=0
  for i in range(108):
   for name,sourceoffset,gpuoffset in [('filtered_rgba',0,0)]+([('filtered_normal',16,16)] if decode else []):
    expected=struct.unpack('<4f',sourcebytes[i*48+sourceoffset:i*48+sourceoffset+16]);actual=struct.unpack('<4f',gpubytes[i*32+gpuoffset:i*32+gpuoffset+16]);assert all(math.isfinite(v) for v in expected+actual)
    error=max(abs(a-b) for a,b in zip(expected,actual))
    if name=='filtered_rgba':loads+=1;local_load=max(local_load,error);max_load=max(max_load,error)
    else:normals+=1;local_normal=max(local_normal,error);max_normal=max(max_normal,error)
    if error>ABSOLUTE_TOLERANCE:local_refusals+=1;refusals.append({'guid':r['guid'],'sample':i,'kind':name,'error':error,'source':expected,'gpu':actual})
  rows.append({'guid':r['guid'],'authored_texture_index':r['authored_texture_index'],'filtered_cases':108,'normal_cases':108 if decode else 0,'max_filtered_error':local_load,'max_normal_error':local_normal,'refused_observations':local_refusals})
 assert (loads,normals)==(1080,324);pins[str(Path(__file__))]=sha(__file__)
 result={'status':'ten_native_filtered_texture_controls_verified' if not refusals else 'native_filtered_texture_comparison_refusals_retained','finished_utc':datetime.now(timezone.utc).isoformat(),
  'resources_compared':10,'filtered_rgba_cases_compared':loads,'filtered_normal_cases_compared':normals,'absolute_tolerance':ABSOLUTE_TOLERANCE,
  'max_filtered_error':max_load,'max_normal_error':max_normal,'refused_observations':len(refusals),'rows':rows,'refusals':refusals,'input_pins':pins,
  'scope':'Native-observed adjacent mip interpolation with bilinear repeat sampling at nine UVs including wrapped/border coordinates, twelve explicit LODs per texture; anisotropy one, measured bias zero',
  'explicit_lod_filtered_controls_qualified':not refusals,'nominal_filter_metadata_changed':False,'full_sampler_policy_qualified':False,'implicit_derivative_sampling_qualified':False,'actual_engine_uniform_binding_qualified':False,'actual_material_render_qualified':False,'forge_editor_verified':False,'web_gpu_qualified':False,'full_import':False,'complete_assets_delivered_increment':0}
 ROOT.mkdir(exist_ok=False);(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ['rows','refusals','input_pins']}))
if __name__=='__main__':main()
