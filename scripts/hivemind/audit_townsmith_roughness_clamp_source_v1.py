"""Measure whether the source/direct clamp discrepancy affects this material."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,numpy as np
W=Path('F:/OpenRPG_ImportWork');D=W/'townsmith-door-source-inpass-normal-v31';ROOT=W/'townsmith-roughness-clamp-source-audit-v1'
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not ROOT.exists()
proof_path=W/'townsmith-door-source-inpass-normal-reopen-v31/RESULT.v1.json';proof=json.loads(proof_path.read_text(encoding='utf-8'))
sun_path=W/'townsmith-source-deferred-inputs-reopen-v49/RESULT.v1.json';sun=json.loads(sun_path.read_text(encoding='utf-8'))
sdk=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime'
lit=sdk/'Material/Lit/Lit.hlsl';surface=sdk/'Lighting/SurfaceShading.hlsl';carrier=W/'townsmith-lighting-components-carrier-v52/SM_Door-Single-B.lighting-components.candidate.glb'
assert 'ConvertAnisotropyToRoughness(bsdfData.perceptualRoughness' in lit.read_text(encoding='utf-8')
assert 'ClampRoughness(preLightData, bsdfData, light.minRoughness);' in surface.read_text(encoding='utf-8')
pins={str(p):sha(p) for p in [Path(__file__),proof_path,sun_path,lit,surface,carrier]};rows=[]
for case in ['door-neutral','door-nopost-linear','door-noaniso-nopost-linear']:
 paths=[D/(case+suffix) for suffix in ['.inpass-normal-2.rgba32f','.inpass-normal-3.rgba32f','.inpass-normal-3.json','.depth-3.r32f']]
 for p in paths:assert proof['input_pins'][str(p)]==sha(p);pins[str(p)]=sha(p)
 assert paths[0].read_bytes()==paths[1].read_bytes()
 metadata=json.loads(paths[2].read_text(encoding='utf-8'));assert metadata['width']==640 and metadata['height']==512 and metadata['viewportWidth']==metadata['viewportHeight']==512
 mask=np.fromfile(paths[3],dtype='<f4').reshape(512,512)>0;assert int(mask.sum())==34445
 r=np.fromfile(paths[1],dtype='<f4').reshape(512,640,4)[:,:512][mask,3];assert np.isfinite(r).all()
 min_sun=next(row['forward_and_min_roughness'][3] for row in sun['rows'] if row['case']==case and row['resource']=='sun')
 rows.append(dict(case=case,covered_pixels=34445,min_perceptual_roughness=float(r.min()),max_perceptual_roughness=float(r.max()),old_clamp_affected_pixels=int((r*r<1/1024).sum()),source_sun_clamp_affected_pixels=int((r*r<min_sun).sum()),actual_source_sun_min_roughness=min_sun))
assert all(row['old_clamp_affected_pixels']==row['source_sun_clamp_affected_pixels']==0 for row in rows)
ROOT.mkdir();result=dict(status='roughness_clamp_discrepancy_not_applicable_to_measured_door',finished_utc=datetime.now(timezone.utc).isoformat(),rows=rows,input_pins=pins,direct_brdf_clamp_order_general_parity_qualified=False,unnecessary_gpu_rerender_avoided=True,full_material_fidelity_qualified=False,full_import=False,complete_assets_delivered_increment=0)
(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
