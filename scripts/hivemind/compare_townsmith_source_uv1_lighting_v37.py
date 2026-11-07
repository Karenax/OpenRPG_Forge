"""Compare whole lit material after UV1 correction without inferring components."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import numpy as np
from PIL import Image

W=Path('F:/OpenRPG_ImportWork')
DRAW=W/'townsmith-native-source-uv1-lighting-v37'
OUT=W/'townsmith-source-uv1-lighting-comparison-v37'
assert not OUT.exists()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def pixels(p):
 assert p.stat().st_size==512*512*8
 a=np.fromfile(p,dtype='<f2').reshape(512,512,4).astype(np.float32)
 assert np.isfinite(a).all()
 return a
run=read(DRAW/'RUN-RESULT.v1.json')
assert run['actual_exit_code']==0 and run['gpu_draw_executed']
assert sha(DRAW/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256']
assert sha(DRAW/'FORGE-LINEAR.rgba16f')==run['forge_image_sha256']
assert read(DRAW/'NATIVE-RESULT.v1.json')['consecutive_equal_readbacks']>=3
pins=dict(read(DRAW/'PREPARED.v1.json')['input_pins'])
carrier=W/'townsmith-source-uv1-lighting-carrier-v37/RESULT.v1.json'
pins.update(read(carrier)['input_pins'])
source_proof=W/'townsmith-door-source-direct-controls-reopen-v18/RESULT.v1.json'
pins.update(read(source_proof)['input_pins'])
source_path=W/'townsmith-door-source-direct-controls-v18/door-nopost-linear.rgba16f'
coverage_path=W/'townsmith-paint-door-native-diffuse-material-draw-v3/FORGE-LINEAR.rgba16f'
depth_proof=W/'townsmith-door-depth-coverage-reopen-v22/RESULT.v1.json'
assert read(depth_proof)['source_depth_mask_vs_native_fragment_coverage_byte_equal']
baseline_path=W/'townsmith-paint-door-native-directional-gbuffer-draw-v2/FORGE-LINEAR.rgba16f'
for p in [Path(__file__),carrier,source_proof,source_path,coverage_path,depth_proof,baseline_path,DRAW/'RUN-RESULT.v1.json',DRAW/'NATIVE-RESULT.v1.json',DRAW/'FORGE-LINEAR.rgba16f']:
 pins[str(p)]=sha(p)
for p,h in pins.items():assert sha(p)==h,p
source=np.flipud(pixels(source_path))
native=pixels(DRAW/'FORGE-LINEAR.rgba16f')
baseline=pixels(baseline_path)
coverage=pixels(coverage_path)[:,:,3]!=0
assert int(coverage.sum())==34445
assert np.array_equal(native[:,:,3]!=0,coverage)
rows=[]
for name,value in [('before_uv1_correction',baseline),('after_uv1_correction',native)]:
 error=np.abs(source[:,:,:3]-value[:,:,:3])[coverage]
 rows.append(dict(control=name,mean_rgb_error=float(error.mean()),max_rgb_error=float(error.max()),pixels_over_001=int((error.max(axis=1)>.001).sum())))
OUT.mkdir()
panel=Image.new('RGB',(1536,512),'black')
for i,value in enumerate([source,baseline,native]):
 rgb=np.maximum(value[:,:,:3],0);rgb=rgb/(1+rgb)
 rgb=np.where(rgb<=.0031308,rgb*12.92,1.055*rgb**(1/2.4)-.055)
 panel.paste(Image.fromarray(np.round(np.clip(rgb,0,1)*255).astype('uint8')),(i*512,0))
panel.save(OUT/'source-before-after.png')
pins[str(OUT/'source-before-after.png')]=sha(OUT/'source-before-after.png')
now=datetime.now(timezone.utc).isoformat()
result=dict(status='source_uv1_full_lit_material_compared_fidelity_open',finished_utc=now,lot='L03.TOWNSMITH.MATERIAUX.UV1.ECLAIRAGE',input_pins=pins,rehashed_pins=len(pins)-1,rows=rows,fixed_covered_pixels=int(coverage.sum()),native_gpu_draw_executed=True,native_fragment_coverage_unchanged=True,diagnostic_bypasses_lighting=False,comparison_is_whole_lit_material=True,component_subtraction_not_used=True,threshold=.001,pixel_fit_or_threshold_relaxation=False,source_render_parity_qualified=False,full_material_fidelity_qualified=False,forge_editor_verified=False,full_import=False,complete_assets_delivered_increment=0,next_action='Recompare graph blend outputs with authored UV1, inspect remaining pixel errors; source AO, bloom and background remain open before prefab and Studio qualification.')
(OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
T=W/'SUIVI_LOTS_HIVEMIND'
summary=f"Matériau éclairé comparé : erreur RGB moyenne {rows[0]['mean_rgb_error']:.6f} -> {rows[1]['mean_rgb_error']:.6f}, couverture identique sur 34 445 pixels. Fidélité complète ouverte."
for name in ['SUIVI_LISIBLE.md','AVANCEMENT_ACTUEL.md','PLAN_LOTS_A_SUIVRE.md','JOURNAL.md']:
 with (T/name).open('a',encoding='utf-8') as f:f.write(f'\n- {now} — **L03.TOWNSMITH.MATERIAUX.UV1.ECLAIRAGE comparé**. {summary} [Preuve](../{OUT.name}/RESULT.v1.json). Aucun nouvel asset complet qualifié.\n')
for name in ['SUIVI_SIMPLE.v1.json','PLAN_LOTS_A_SUIVRE.v1.json']:
 p=T/name;j=read(p);j.update(updated_utc=now,last_technical_batch=result['lot'],last_technical_proof=str(OUT/'RESULT.v1.json'),current_work_state=summary,state=result['status'],next_action=result['next_action'],full_import=False);p.write_text(json.dumps(j,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
