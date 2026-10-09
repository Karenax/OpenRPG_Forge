from pathlib import Path
import datetime,hashlib,json
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-source-shadow-raster-native-v7';OLD=W/'firepit-source-shadow-raster-native-v4';ROOT=W/'firepit-source-shadow-raster-replay-reopen-v2'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def check(report):
 for p,h in report['input_pins'].items():assert sha(p)==h,p
if __name__=='__main__':
 assert not ROOT.exists()
 run=read(CAP/'RUN-RESULT.v1.json');assert run.get('actual_exit_code')==0,run
 prep=read(CAP/'PREPARED.v1.json');check(prep)
 assert (run['nodes'],run['primitives'],run['material_instances'])==(9,4,4)
 for n in ['CASE.v1.json','gpu-0.rgba16f','gpu-1.rgba16f']:assert sha(CAP/n)==sha(OLD/n),n
 depth=read(W/'firepit-source-shadow-depth-comparison-v6/RESULT.v1.json');check(depth)
 comparison=read(W/'firepit-source-shadow-raster-comparison-v7/RESULT.v1.json');check(comparison)
 metadata=[read(CAP/f'NATIVE-SHADOW-DEPTH-{v}.json') for v in range(2)]
 assert all(m['copied_inside_render_schedule_after_shadow_pass'] and m['stage_submissions']>0 and m['bytes']==4194304 for m in metadata)
 assert metadata[0]['raster_pipelines'] and not metadata[1]['raster_pipelines']
 web=read(W/'studio-current-source-wasm-check-v23/RUN-RESULT.v1.json');assert web['actual_exit_code']==0 and web['changed_source_files']==[];check(web)
 files=[Path(__file__).resolve(),CAP/'PREPARED.v1.json',CAP/'RUN-RESULT.v1.json',W/'firepit-source-shadow-depth-comparison-v6/RESULT.v1.json',W/'firepit-source-shadow-raster-comparison-v7/RESULT.v1.json',W/'studio-current-source-wasm-check-v23/RUN-RESULT.v1.json',*[CAP/f'NATIVE-SHADOW-DEPTH-{v}.json' for v in range(2)],*[root/n for root in [CAP,OLD] for n in ['CASE.v1.json','gpu-0.rgba16f','gpu-1.rgba16f']]]
 result=dict(status='active_raster_descriptor_and_in_pass_gpu_depth_reopened_fidelity_open',created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),native_input_pins_verified=len(prep['input_pins']),web_input_pins_verified=len(web['input_pins']),rendered_images_identical_to_external_depth_copy=2,depth_comparison=depth['status'],shadow_mask_iou=comparison['shadow_mask_iou'],gpu_depth_samples_verified=depth['depth_samples_verified'],source_geometry_unchanged=True,caster_view_uniform_gpu_verified=False,full_import=False,full_shadow_fidelity_qualified=False,editor_visual_verified=False,complete_assets_delivered_increment=0,input_pins={str(p):sha(p) for p in files})
 ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
