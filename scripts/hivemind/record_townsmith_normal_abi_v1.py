from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,subprocess,sys
S=Path(__file__).parent;W=Path('F:/OpenRPG_ImportWork');ROOT=W/'townsmith-s-paint-normal-abi-v1'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 profiles=ROOT/'PROFILES.v1.json';p=read(profiles);gpu=W/'townsmith-s-paint-native-texture-gpu-control-reopen-v2/RESULT.v1.json';g=read(gpu)
 assert g['common_decoder_scalar_modes_gpu_control_qualified'] and g['normal_decode_cases_compared']==324 and g['refused_observations']==0
 for d in [p,g]:
  for name,h in d['input_pins'].items():assert sha(name)==h,name
 assert sha(p['material_program'])==p['material_program_sha256']
 rows=[dict(row,common_mode_scalar_gpu_control_verified=True) for row in p['rows']]
 result={'status':'three_explicit_native_to_common_normal_scalar_mappings_gpu_verified','finished_utc':datetime.now(timezone.utc).isoformat(),
  'rows':rows,'source_native_mode':1,'common_shader_value':2.0,'normal_gpu_cases_verified':324,
  'input_pins':{str(profiles):sha(profiles),str(gpu):sha(gpu),str(Path(__file__)):sha(__file__)},
  'actual_engine_uniform_binding_qualified':False,'filtered_sampling_qualified':False,'actual_material_render_qualified':False,'forge_editor_verified':False,'full_import':False,'complete_assets_delivered_increment':0}
 target=ROOT/'PROFILES-QUALIFICATION.v1.json';assert not target.exists();target.write_text(json.dumps(result,indent=2),encoding='utf-8')
 # Keep preceding tracker publications intact; latest receipt overrides only the current summary.
 t=(S/'record_native_texture_gpu_progress_v1.py').read_text(encoding='utf-8')
 t=t.replace('native-texture-gpu-control-reopen-v1','native-texture-gpu-control-reopen-v2')
 t=t.replace('GPU.LOAD_CONTROL','NORMALS.ABI_CONTROL')
 t=t.replace('324 decodages normaux sans ecart','324 decodages normaux sans ecart avec modes communs (source RG/AG 1 vers ABI 2)')
 t=t.replace('ABI normales, sampling filtre','binding uniforms reel, sampling filtre')
 t=t.replace('mapper explicitement les profils de normales dans ABI commun, puis ','verifier le binding effectif des uniforms de normales, puis ')
 t=t.replace('ABI commun des normales, rendu materiau','binding effectif des uniforms, rendu materiau')
 t=t.replace('GPU natif prototype','GPU natif avec modes communs').replace('GPU prototype :','GPU avec modes communs :')
 (S/'record_native_texture_gpu_progress_v2.py').write_text(t,encoding='utf-8')
 subprocess.run([sys.executable,'-B',str(S/'record_native_texture_gpu_progress_v2.py')],check=True)
 print(json.dumps({k:v for k,v in result.items() if k not in ['rows','input_pins']}))
if __name__=='__main__':main()
