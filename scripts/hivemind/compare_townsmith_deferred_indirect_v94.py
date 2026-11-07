"""Compare unchanged source references and real candidate GPU readbacks."""
from pathlib import Path
from datetime import datetime, timezone
import ast, hashlib, json
import numpy as np
from PIL import Image, ImageDraw
W=Path('F:/OpenRPG_ImportWork'); R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
O=W/'townsmith-deferred-indirect-comparison-v94'; assert not O.exists()
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
P={}
def pin(p, expected=None):
 h=sha(p); assert expected is None or h==expected,str(p); P[str(p)]=h
base=R/'scripts/hivemind/compare_townsmith_common_srgb_components_v93.py'
tree=ast.parse(base.read_text()); node=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='packed_pixels')
scope={'np':np};exec(compile(ast.Module(body=[node],type_ignores=[]),str(base),'exec'),scope)
decode=scope['packed_pixels'];pin(base);pin(Path(__file__))
previous=W/'townsmith-common-srgb-components-comparison-v93/RESULT.v1.json'; prior=read(previous);pin(previous)
depth=W/'townsmith-door-source-material-values-v41/door-nopost-linear.depth-3.r32f';pin(depth,prior['input_pins'][str(depth)])
roi=(np.fromfile(depth,dtype='<f4').reshape(512,512)>0)[::-1];assert roi.sum()==34445
carrier=W/'townsmith-deferred-indirect-carrier-v94/RESULT.v1.json'; cp=read(carrier);pin(carrier)
for p,h in cp['input_pins'].items():pin(Path(p),h)
rows=[];comparisons=[]
for layer,name,source_name in [(0,'full','door-nopost-linear.rgba16f'),(2,'indirect','door-debug-IndirectDiffuseLighting-nopost-linear.rgba16f')]:
 source=W/'townsmith-door-source-direct-controls-v18'/source_name;pin(source,prior['input_pins'][str(source)])
 src=np.fromfile(source,dtype='<f2').reshape(512,512,4)[::-1,:,:3].astype('float32');assert np.isfinite(src).all()
 values=[]
 for directory in [f'townsmith-common-native-srgb-layer-{layer}-v91',f'townsmith-deferred-indirect-layer-{layer}-v94']:
  d=W/directory;run=read(d/'RUN-RESULT.v1.json');native=read(d/'NATIVE-RESULT.v1.json');prep=read(d/'PREPARED.v1.json');inp=read(d/'INPUT.v1.json')
  assert run['actual_exit_code']==0 and run['gpu_draw_executed'] and native['consecutive_equal_readbacks']>=3 and inp['diagnostic_layer']==layer
  pin(d/'NATIVE-RESULT.v1.json',run['native_receipt_sha256']);pin(d/'RUN-RESULT.v1.json');pin(d/'PREPARED.v1.json')
  for p,h in prep['input_pins'].items():pin(Path(p),h)
  pin(d/'FORGE-PACKED.rg11b10',run['forge_packed_image_sha256']);values.append(decode(d/'FORGE-PACKED.rg11b10'))
 for label,target in zip(['previous_common_srgb','deferred_indirect_candidate'],values):
  delta=np.abs(src-target)[roi];rows.append(dict(channel=name,control=label,mean_rgb_error=float(delta.mean()),max_rgb_error=float(delta.max()),pixels_over_001=int((delta.max(axis=1)>.001).sum()),exact_channels_fraction=float((delta==0).mean())))
 comparisons.append((name,src,*values))
O.mkdir();panel=Image.new('RGB',(1536,2*544),'#202020');draw=ImageDraw.Draw(panel)
for row,(name,*values) in enumerate(comparisons):
 for col,(label,value) in enumerate(zip(['source','previous','candidate'],values)):
  x=np.maximum(value,0);rgb=np.where(x<=.0031308,12.92*x,1.055*x**(1/2.4)-.055)
  rgb=np.round(np.clip(rgb,0,1)*255).astype('uint8');rgb[~roi]=45
  draw.text((col*512+6,row*544+8),name+' / '+label,fill='white');panel.paste(Image.fromarray(rgb),(col*512,row*544+28))
panel.save(O/'fixed-roi-comparison.png');pin(O/'fixed-roi-comparison.png')
full=[r for r in rows if r['channel']=='full']; improvement=100*(full[0]['mean_rgb_error']-full[1]['mean_rgb_error'])/full[0]['mean_rgb_error']
result=dict(status='deferred_indirect_source_order_candidate_compared_fidelity_open',lot='L03.TOWNSMITH.MATERIAUX.DIFFUS.INDIRECT',finished_utc=datetime.now(timezone.utc).isoformat(),rows=rows,full_mean_error_improvement_percent=improvement,source_fixed_roi_pixels=34445,input_pins=P,diagnostic_threshold_unchanged=.001,pixel_fit_or_threshold_relaxation=False,source_pixels_applied_to_native=False,source_branch_activation_independently_captured=False,installed_in_product=False,editor_verified=False,full_material_fidelity_qualified=False,full_import=False,complete_assets_delivered_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
