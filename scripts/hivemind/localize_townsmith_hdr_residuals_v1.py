from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,numpy as np
from PIL import Image,ImageDraw
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'townsmith-hdr-residual-localization-v1';assert not ROOT.exists()
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
proof=W/'townsmith-hdr-buffer-comparison-v56/RESULT.v1.json';d=json.loads(proof.read_text(encoding='utf-8'));pins={str(proof):sha(proof),str(Path(__file__)):sha(Path(__file__))}
paths=[W/'townsmith-hdr-buffer-comparison-v56/FORGE-DECODED.rgb32f',W/'townsmith-door-source-microshadow-inputs-v55/door-nopost-linear.rgba16f',W/'townsmith-door-source-material-values-v41/door-nopost-linear.depth-3.r32f']
for p in paths:assert sha(p)==d['input_pins'][str(p)];pins[str(p)]=sha(p)
native=np.fromfile(paths[0],dtype='<f4').reshape(512,512,3);source=np.fromfile(paths[1],dtype='<f2').reshape(512,512,4)[::-1,:,:3].astype('float32');mask=(np.fromfile(paths[2],dtype='<f4').reshape(512,512)>0)[::-1];assert mask.sum()==34445
inside=mask.copy()
for dy,dx in [(1,0),(-1,0),(0,1),(0,-1)]:inside &= np.roll(mask,(dy,dx),(0,1))
edge=mask&~inside;delta=np.abs(source-native);rows=[]
for name,region in [('full_required_roi',mask),('outline_diagnostic_only',edge),('interior_diagnostic_only',inside)]:
 q=delta[region];rows.append(dict(region=name,pixels=int(region.sum()),mean_rgb_error=float(q.mean()),max_rgb_error=float(q.max()),pixels_over_001=int((q.max(1)>.001).sum())))
ROOT.mkdir();sheet=Image.new('RGB',(1536,550),(30,30,30));draw=ImageDraw.Draw(sheet)
for i,(label,value) in enumerate([('Unity source',source),('Forge GPU R11/G11/B10',native)]):
 rgb=np.maximum(value,0);rgb=rgb/(1+rgb);rgb=np.where(rgb<=.0031308,rgb*12.92,1.055*rgb**(1/2.4)-.055);rgb[~mask]=0;sheet.paste(Image.fromarray(np.round(np.clip(rgb,0,1)*255).astype('uint8')),(i*512,30));draw.text((i*512+10,8),label,fill='white')
heat=np.zeros((512,512,3),dtype='uint8');heat[:,:,0]=np.round(np.clip(delta.max(2)/.02,0,1)*255).astype('uint8');heat[:,:,1]=np.where(delta.max(2)>.001,0,40).astype('uint8');heat[~mask]=0;sheet.paste(Image.fromarray(heat),(1024,30));draw.text((1034,8),'Residual RGB (display scale .02)',fill='white');image=ROOT/'comparison.png';sheet.save(image);pins[str(image)]=sha(image)
r=dict(status='unmodified_hdr_residuals_localized',finished_utc=datetime.now(timezone.utc).isoformat(),rows=rows,input_pins=pins,outline_partition_diagnostic_only=True,required_roi_unchanged=True,display_mapping_only=True,source_pixels_applied_to_native=False,full_material_fidelity_qualified=False,full_import=False)
(ROOT/'RESULT.v1.json').write_text(json.dumps(r,indent=2)+'\n',encoding='utf-8');print(json.dumps({k:v for k,v in r.items() if k!='input_pins'}))
