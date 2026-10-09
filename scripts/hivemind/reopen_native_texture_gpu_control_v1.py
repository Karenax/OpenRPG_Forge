from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,struct,math
W=Path('F:/OpenRPG_ImportWork');CAP=W/'townsmith-s-paint-native-texture-gpu-control-v1';ROOT=W/'townsmith-s-paint-native-texture-gpu-control-reopen-v1'
ABSOLUTE_TOLERANCE=1e-6
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def f32(v):return struct.unpack('<f',struct.pack('<f',v))[0]
def main():
 prep=read(CAP/'PREPARED.v1.json');run=read(CAP/'RUN-RESULT.v1.json');native=read(CAP/'NATIVE-RESULT.v1.json');manifest=read(CAP/'INPUT.v1.json')
 assert run['compile_exit_code']==run['gpu_exit_code']==0 and run['status']=='native_texture_gpu_control_finished_inputs_unchanged'
 assert sha(CAP/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256'];pins={}
 for entries in [prep['input_pins'],run['artifact_pins']]:
  for p,h in entries.items():assert sha(p)==h,p;pins[p]=h
 bcpath=W/'townsmith-s-paint-compressed-storage-v2/NATIVE-RESULT.v1.json';bc=read(bcpath);bclookup={r['guid']:r for r in bc['rows']}
 normalpath=W/'townsmith-s-paint-native-normal-decode-v1/NATIVE-RESULT.v1.json';normals=read(normalpath);normallookup={r['guid']:r for r in normals['rows']}
 for p in [bcpath,normalpath,CAP/'PREPARED.v1.json',CAP/'RUN-RESULT.v1.json',CAP/'NATIVE-RESULT.v1.json',Path(__file__)]:pins[str(p)]=sha(p)
 assert len(native['rows'])==len(manifest['rows'])==10
 rows=[];load_total=normal_total=0;failed=[];max_load=max_normal=0.0;unity_cases=0;unorm_cases=0
 for resource,gpu in zip(manifest['rows'],native['rows']):
  guid=resource['guid'];assert gpu['guid']==guid and gpu['authored_texture_index']==resource['authored_texture_index'] and gpu['samples']==108
  payload=Path(gpu['file']).read_bytes();assert len(payload)==108*32
  if guid in bclookup:
   sourcepath=Path(bclookup[guid]['source_samples']);reference=sourcepath.read_bytes();pins[str(sourcepath)]=sha(sourcepath);assert len(reference)==108*16;reference_kind='Unity GPU Texture2D.Load';unity_cases+=108
  else:
   assert resource['encoding']=='rgba8_unorm';blob=Path(resource['native_storage']).read_bytes();samples=[]
   for mip in resource['mips']:
    w=mip['width'];h=mip['height']
    for x,y in [(0,0),(w-1,0),(0,h-1),(w-1,h-1),(w//2,h//2),(w//4,h//4),(3*w//4,3*h//4),(w//2,0),(0,h//2)]:
     offset=mip['offset']+(y*w+x)*4;samples.extend(f32(v/255.0) for v in blob[offset:offset+4])
   reference=struct.pack('<'+str(len(samples))+'f',*samples);reference_kind='Unity native GPU mip bytes, UNORM conversion';unorm_cases+=108
  normalref=None
  if guid in normallookup:
   normalrow=normallookup[guid];assert normalrow['shader_unpack_mode']==gpu['prototype_unpack_mode']==1
   normalref=Path(normalrow['samples_file']).read_bytes();pins[normalrow['samples_file']]=sha(normalrow['samples_file']);assert len(normalref)==108*48 and gpu['normal_decode_enabled']
  else:assert not gpu['normal_decode_enabled']
  local_load=local_normal=0.0;local_failures=0
  for i in range(108):
   loaded=struct.unpack('<4f',payload[i*32:i*32+16]);expected=struct.unpack('<4f',reference[i*16:(i+1)*16]);assert all(math.isfinite(v) for v in loaded+expected)
   error=max(abs(a-b) for a,b in zip(loaded,expected));local_load=max(local_load,error);max_load=max(max_load,error);load_total+=1
   if error>ABSOLUTE_TOLERANCE:
    local_failures+=1;failed.append({'guid':guid,'sample':i,'kind':'texture_load','error':error,'source':expected,'gpu':loaded})
   if normalref is not None:
    decoded=struct.unpack('<4f',payload[i*32+16:(i+1)*32]);source=struct.unpack('<4f',normalref[i*48+16:i*48+32]);assert all(math.isfinite(v) for v in decoded+source)
    error=max(abs(a-b) for a,b in zip(decoded,source));local_normal=max(local_normal,error);max_normal=max(max_normal,error);normal_total+=1
    if error>ABSOLUTE_TOLERANCE:
     local_failures+=1;failed.append({'guid':guid,'sample':i,'kind':'normal_decode','error':error,'source':source,'gpu':decoded})
  rows.append({'guid':guid,'authored_texture_index':resource['authored_texture_index'],'load_cases':108,'load_reference':reference_kind,'max_load_error':local_load,'normal_cases':108 if normalref else 0,'max_normal_error':local_normal,'refused_observations':local_failures})
 assert (load_total,normal_total,unity_cases,unorm_cases)==(1080,324,864,216)
 result={'status':'native_ten_texture_gpu_load_and_three_normal_decode_controls_verified' if not failed else 'native_texture_gpu_load_decode_comparison_refusals_retained',
  'finished_utc':datetime.now(timezone.utc).isoformat(),'adapter':native['adapter'],'backend':native['backend'],
  'resources_uploaded':10,'mips_uploaded':120,'load_cases_compared':1080,'direct_unity_gpu_load_cases':864,'native_unorm_byte_reference_cases':216,'normal_decode_cases_compared':324,
  'absolute_tolerance':ABSOLUTE_TOLERANCE,'max_load_error':max_load,'max_normal_error':max_normal,'refused_observations':len(failed),'rows':rows,'refusals':failed,'input_pins':pins,
  'source_mip_load_controls_qualified':not failed,'prototype_normal_decode_controls_qualified':not failed,'filtered_sampling_qualified':False,'common_decoder_abi_qualified':False,
  'actual_engine_material_render_qualified':False,'forge_editor_verified':False,'web_gpu_qualified':False,'full_import':False,'complete_assets_delivered_increment':0,
  'next_action':'Install common admission after current engine tests; map normal profiles explicitly, then verify filtered sampling and full material rendering in native/Web and Studio'}
 ROOT.mkdir(exist_ok=False);(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','rows','refusals']}))
if __name__=='__main__':main()
