from pathlib import Path
import datetime,hashlib,json
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-source-shadow-raster-state-gpu-native-v1';OLD=W/'firepit-source-shadow-caster-view-gpu-native-v1';ROOT=W/'firepit-source-shadow-raster-state-gpu-reopen-v1'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
if __name__=='__main__':
 assert not ROOT.exists()
 run=read(CAP/'RUN-RESULT.v1.json');assert run.get('actual_exit_code')==0,run
 pins=read(CAP/'PREPARED.v1.json')['input_pins']
 for p,h in pins.items():assert sha(p)==h,p
 source=read(W/'firepit-source-shadow-depth-clipping-source-v1/RESULT.v1.json')
 for p,h in source['input_pins'].items():assert sha(p)==h,p
 assert source['active_source_depth_clipping_enabled']==[False]*4
 meta=read(CAP/'NATIVE-SHADOW-DEPTH-0.json');rows=meta['raster_pipelines'];assert rows and all(r['source_shadow'] and r['orthographic'] and r['ready'] for r in rows)
 control=read(CAP/'KNOWN-DEPTH-PROBE-CONTROL.json');assert control['mismatches_per_layer']==[0,0,0,0]
 for v in range(2):assert sha(CAP/f'gpu-{v}.rgba16f')==sha(OLD/f'gpu-{v}.rgba16f')
 mismatch=any(not r['unclipped_depth'] for r in rows)
 files=[Path(__file__).resolve(),CAP/'PREPARED.v1.json',CAP/'RUN-RESULT.v1.json',CAP/'NATIVE-SHADOW-DEPTH-0.json',CAP/'KNOWN-DEPTH-PROBE-CONTROL.json',W/'firepit-source-shadow-depth-clipping-source-v1/RESULT.v1.json',*[root/f'gpu-{v}.rgba16f' for root in [CAP,OLD] for v in range(2)]]
 result=dict(status='native_source_directional_depth_clipping_mismatch_verified' if mismatch else 'native_source_directional_depth_clipping_state_matches',created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),source_depth_clip_enabled=False,native_pipelines=rows,source_native_depth_clipping_mismatch=mismatch,depth_clip_control_on_device=all(r['device_depth_clip_control'] for r in rows),depth_clip_control_on_adapter=all(r['adapter_depth_clip_control'] for r in rows),source_geometry_and_images_unchanged=True,native_source_pins_verified=len(pins),full_import=False,full_shadow_fidelity_qualified=False,complete_assets_delivered_increment=0,next_action='Implement an explicit orthographic source clipping policy and feature gate, then compare qualified four-layer GPU depth and original images' if mismatch else 'Inspect source caster culling and rasterized geometry without changing matching clipping',input_pins={str(p):sha(p) for p in files})
 ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
