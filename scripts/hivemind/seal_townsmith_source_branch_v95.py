"""Seal actual GPU flags without promoting an inferred branch to a draw trace."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib,json,struct
W=Path('F:/OpenRPG_ImportWork');D=W/'townsmith-door-source-branch-v95'
O=W/'townsmith-source-branch-reopen-v95';assert not O.exists()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
run=read(D/'RUN-RESULT.v1.json');assert run['actual_exit_code']==0 and run['reference_images_byte_equal'] and run['source_inputs_unchanged']
prep=read(D/'PREPARED.v1.json');pins={**prep['input_pins'],**prep['private_before_pins']}
for p,h in pins.items():assert sha(Path(p))==h,p
native=read(D/'NATIVE-RESULT.v1.json');assert sha(D/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256']
rows=[]
for case in read(D/'REQUEST.v1.json')['cases']:
 files=sorted(D.glob(case['id']+'.branch-*.rgba32f'),key=lambda p:int(p.stem.split('.branch-')[-1]))
 assert len(files)>=3
 values=[]
 for p in files:
  assert p.stat().st_size==16;v=struct.unpack('<4f',p.read_bytes())
  assert v[0] in [0,1] and v[1] in [0,1,2,3] and v[2] in [0,1,2] and v[3]==1
  values.append(v);pins[str(p)]=sha(p)
 assert values[-1]==values[-2]==values[-3]
 enable,mode,variant,marker=values[-1]
 rows.append(dict(id=case['id'],captures=len(files),stable_final_readbacks=3,enable_probe_volumes=int(enable),indirect_diffuse_mode=int(mode),compiled_probe_volume_sh_band=int(variant),gpu_draw_marker=marker,ambient_fallback_conditions_verified=(enable==0 and mode==0 and variant>0)))
prior=W/'townsmith-door-source-camera-brdf-v10'
for name in ['door-neutral.rgba16f','door-nopost-linear.rgba16f']:
 assert sha(D/name)==sha(prior/name);pins[str(D/name)]=sha(D/name);pins[str(prior/name)]=sha(prior/name)
ao=W/'townsmith-gbuffer3-ao-tag-control-v76/RESULT.v1.json';a=read(ao);assert a['ao_tag_pixels']==34445;pins[str(ao)]=sha(ao)
for p in [Path(__file__),D/'RUN-RESULT.v1.json',D/'NATIVE-RESULT.v1.json',D/'PREPARED.v1.json']:pins[str(p)]=sha(p)
O.mkdir();result=dict(status='source_gi_flags_gpu_captured_original_camera_reference_unchanged',lot='L03.TOWNSMITH.MATERIAUX.DIFFUS.INDIRECT.BRANCHE_SOURCE',finished_utc=datetime.now(timezone.utc).isoformat(),actual_unity_exit_code=0,rows=rows,source_sdk_modified=False,source_and_private_asset_pins_rehashed=True,reference_images_byte_equal=True,source_ao_tag_pixels=34445,ambient_probe_fallback_inferred_from_gpu_flags_and_pinned_sdk=all(r['ambient_fallback_conditions_verified'] for r in rows),branch_execution_trace_captured=False,probe_values_are_actual_gpu_readbacks=True,full_material_fidelity_qualified=False,installed_material_in_product=False,editor_visual_verified=False,full_import=False,new_fully_qualified_assets=0,input_pins=pins)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
