from pathlib import Path
import array,datetime,hashlib,json,math,sys
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-source-shadow-raster-zero-control-native-v1';OLD=W/'firepit-source-shadow-raster-native-v6';ROOT=W/'firepit-source-shadow-raster-zero-control-reopen-v1'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
if __name__=='__main__':
 assert not ROOT.exists()
 run=read(CAP/'RUN-RESULT.v1.json');assert run.get('actual_exit_code')==0,run
 pins=read(CAP/'PREPARED.v1.json')['input_pins']
 for p,h in pins.items():assert sha(p)==h,p
 oldcase=read(OLD/'CASE.v1.json');case=read(CAP/'CASE.v1.json');oldcase.pop('source_shadow_raster_replay');case.pop('source_shadow_raster_replay');assert case==oldcase
 meta=read(CAP/'NATIVE-SHADOW-DEPTH-0.json');assert meta['copied_inside_render_schedule_after_shadow_pass'] and meta['stage_submissions']>0
 assert meta['raster_pipelines']
 for row in meta['raster_pipelines']:assert (row['constant'],row['slope_scale'],row['clamp'])==(0,0,0)
 values=[]
 for root in [OLD,CAP]:
  a=array.array('f');a.frombytes((root/'NATIVE-SHADOW-DEPTH-0.float32').read_bytes())
  if sys.byteorder!='little':a.byteswap()
  assert len(a)==1048576 and all(math.isfinite(v) and 0<=v<=1 for v in a);values.append(a)
 counts=[[sum(v!=0 for v in a[layer*262144:(layer+1)*262144]) for layer in range(4)] for a in values]
 result=dict(status='zero_raster_bias_causal_control_measured',created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),source_scene_and_shaders_identical=True,candidate_nonzero_per_layer=counts[0],zero_bias_nonzero_per_layer=counts[1],zero_bias_recovers_directional_depth=any(counts[1]) and not any(counts[0]),depth_samples_verified=2097152,full_import=False,full_shadow_fidelity_qualified=False,complete_assets_delivered_increment=0,next_action='Inspect effective caster GPU projections and clip coordinates if zero bias does not recover depth; otherwise inspect raster-bias units and source backend translation')
 files=[Path(__file__).resolve(),CAP/'PREPARED.v1.json',CAP/'RUN-RESULT.v1.json',CAP/'NATIVE-SHADOW-DEPTH-0.json',*[r/n for r in [OLD,CAP] for n in ['CASE.v1.json','NATIVE-SHADOW-DEPTH-0.float32']]];result['input_pins']={str(p):sha(p) for p in files}
 ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
