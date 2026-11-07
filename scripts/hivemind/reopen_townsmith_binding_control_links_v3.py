"""Reopen existing source controls and link exact retained draw-binding cases."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib
import numpy as np
W=Path('F:/OpenRPG_ImportWork');OUT=W/'townsmith-draw-binding-control-links-v3';assert not OUT.exists()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
plan_path=W/'townsmith-draw-binding-lot-plan-v2/RESULT.v1.json';plan=read(plan_path)
context_path=W/'townsmith-hdrp-slot-contexts-reopen-v1/RESULT.v1.json';context=read(context_path)
control_path=W/'townsmith-explicit-slot-controls-reopen-v1/RESULT.v1.json';controls=read(control_path)
assert context['cases_reopened']==8 and controls['cases_reopened']==16
pins={str(p):sha(p) for p in [Path(__file__),plan_path,context_path,control_path]}
linked=[]
for case in plan['cases']:
 key=case['source_mesh_identity']['guid']+':'+case['source_mesh_identity']['file_id']
 materials=[m['guid']+':'+m['file_id'] for m in case['original_material_slots']]
 matches=[r for r in context['rows'] if r['mesh_identity']==key and r['material_identities']==materials]
 assert len(matches)==1,(key,materials);reference=matches[0]
 assert reference['submeshes']==case['source_submesh_count'] and reference['material_slots']==len(materials)
 raw=Path(reference['raw_image']);png=Path(reference['image']);assert sha(raw)==reference['raw_sha256'] and sha(png)==reference['image_sha256'];pins[str(raw)]=sha(raw);pins[str(png)]=sha(png)
 source=np.fromfile(raw,dtype='<f2').reshape(512,512,4);assert np.isfinite(source).all()
 observations=[]
 for mode in ['submesh_clamp','slot_repeat_last']:
  matches=[r for r in controls['comparisons'] if r['id']==reference['id']+'-'+mode];assert len(matches)==1;row=matches[0]
  image=Path(row['actual_image']);capture=image.with_suffix('.rgba16f')
  for f in [image,capture]:assert sha(f)==controls['input_pins'][str(f)];pins[str(f)]=sha(f)
  target=np.fromfile(capture,dtype='<f2').reshape(512,512,4);assert np.isfinite(target).all()
  delta=np.abs(source[:,:,:3].astype('float32')-target[:,:,:3].astype('float32'));changed=int(np.any(delta!=0,axis=2).sum())
  assert changed==row['rgb_changed_pixels'];assert float(delta.max())==row['rgb_max_absolute_error'];assert (raw.read_bytes()==capture.read_bytes())==row['reference_bytes_identical']
  observations.append(dict(mode=mode,groups=row['explicit_groups'],mappings=row['mappings'],changed_pixels=changed,max_rgb_error=float(delta.max()),reference_bytes_identical=row['reference_bytes_identical']))
 linked.append(dict(case_id=case['case_id'],source_control_id=reference['id'],mesh_identity=key,ordered_material_identities=materials,occurrences=len(case['occurrences']),existing_controls_reopened=observations,draw_event_count_qualified=False,native_mapping_qualified=False))
assert len(linked)==8 and sum(r['occurrences'] for r in linked)==37
OUT.mkdir();r=dict(status='eight_exact_binding_cases_linked_to_sixteen_reopened_source_controls',finished_utc=datetime.now(timezone.utc).isoformat(),cases=linked,input_pins=pins,rehashed_pins=len(pins)-1,source_gpu_not_rerun=True,observed_image_equivalence_is_not_draw_event_proof=True,material_render_qualified=False,studio_verified=False,full_import=False,complete_assets_delivered_increment=0)
(OUT/'RESULT.v1.json').write_text(json.dumps(r,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in r.items() if k not in ['input_pins','cases']}))
