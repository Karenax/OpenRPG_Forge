import hashlib,json,math
from pathlib import Path
from datetime import datetime,timezone
W=Path(r"F:\OpenRPG_ImportWork")
R=W/'firepit-source-shadow-raster-v6'
O=W/'firepit-source-shadow-render-matrices-reopen-v1'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 assert not O.exists(),'Immutable receipt already exists'
 prepared=read(R/'PREPARED.v1.json');run=read(R/'RUN-RESULT.v1.json')
 assert run['actual_exit_code']==0 and run['source_inputs_unchanged'] and not run['private_changed']
 pins={}
 for group in ['input_pins','private_before_pins']:
  for name,expected in prepared[group].items():
   p=Path(name);assert sha(p)==expected, name;pins[name]=expected
 assert len(prepared['dependency_paths'])==24
 pairs=[]
 for i in range(2):
  for ref in ['firepit-source-shadow-raster-v4','firepit-shadow-source-reference-v8']:
   for rel in [f'images/{i}.png',f'linear-{i}.rgba16f']:
    p=R/rel;q=W/ref/rel;assert sha(p)==sha(q),(p,q)
    pins[str(p)]=sha(p);pins[str(q)]=sha(q);pairs.append([str(p),str(q)])
 active=read(R/'SOURCE-CASTER-SPLITS-0.json');off=read(R/'SOURCE-CASTER-SPLITS-1.json')
 assert active['active_requests']==4 and len(active['splits'])==4
 assert off['active_requests']==0 and len(off['splits'])==0
 fields=['view','projection','device_projection_matrix','device_projection_y_flip','inv_view_projection','clip_from_world']
 count=0
 for n,row in enumerate(active['splits']):
  assert row['split_index']==n and row['z_clip'] is False and row['forward_offset']==0
  for field in fields:
   values=row[field];assert len(values)==16 and all(math.isfinite(x) for x in values);count+=16
  a=row['device_projection_y_flip'];b=row['view'];c=row['clip_from_world']
  expected=[sum(a[k*4+r]*b[col*4+k] for k in range(4)) for col in range(4) for r in range(4)]
  assert max(abs(x-y) for x,y in zip(c,expected))<=2e-6
 for name in ['RUN-RESULT.v1.json','PREPARED.v1.json','SOURCE-CASTER-SPLITS-0.json','SOURCE-CASTER-SPLITS-1.json','SOURCE-SHADOW-RASTER-0.json','SOURCE-SHADOW-RASTER-1.json','SOURCE-SHADOW-DEPTH-0.float32','SOURCE-SHADOW-DEPTH-1.float32']:
  p=R/name;pins[str(p)]=sha(p)
 pins[str(Path(__file__))]=sha(Path(__file__))
 result={'status':'source_actual_shadow_render_matrices_captured_and_reopened','finished_utc':datetime.now(timezone.utc).isoformat(),'source_dependencies_unchanged':24,'source_and_private_pins_verified':107,'active_source_caster_splits_verified':4,'inactive_view_splits':0,'matrix_scalars_verified_finite':count,'original_image_and_hdr_pairs_byte_identical':len(pairs),'image_pairs':pairs,'input_pins':pins,'actual_render_projection_field':'HDShadowRequest.cullingSplit.deviceProjectionYFlip','source_gpu_uniform_bits_verified':False,'native_actual_source_raster_projection_parity_verified':False,'next_action':'Transform the actual source raster matrices into the declared Forge coordinate frame and compare to the native caster GPU uniforms; do not infer geometry parity from sampler reconstruction.','full_import':False,'forge_comparison_qualified':False,'editor_visual_verified':False,'complete_assets_delivered_increment':0}
 O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding='utf-8')
 print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','image_pairs']}))
if __name__=='__main__':main()
