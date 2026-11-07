"""Classify captured coarse derivatives against captured fine quad rows/columns."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,numpy as np
w=Path('F:/OpenRPG_ImportWork');root=w/'townsmith-derivative-quad-orientation-v1';assert not root.exists()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
pins={}
def pin(p,h=None):
 value=sha(p)
 if h is not None:assert value==h,str(p)
 pins[str(p)]=value
fp=w/'townsmith-fine-uv-comparison-v28/RESULT.v1.json';fine=read(fp);dp=w/'townsmith-depth-bound-uv-comparison-v27/RESULT.v1.json';default=read(dp);sp=w/'townsmith-door-source-fine-uv-reopen-v28/RESULT.v1.json';sourceproof=read(sp)
for p in [fp,dp,sp,Path(__file__)]:pin(p)
source=w/'townsmith-door-source-fine-uv-v28';depth=source/'door-nopost-linear.depth-3.r32f';pin(depth,fine['input_pins'][str(depth)]);mask=(np.fromfile(depth,dtype='<f4').reshape(512,512)>0)[::-1];assert mask.sum()==34445
full=mask.reshape(256,2,256,2).all(axis=(1,3));quads=full.repeat(2,0).repeat(2,1);assert np.all(~quads|mask)
meta=source/'door-nopost-linear.uv-control-11.json';pin(meta,sourceproof['input_pins'][str(meta)]);m=read(meta)
arrays=[]
for idx in [10,11]:
 p=source/f'door-nopost-linear.uv-control-{idx}.rgba32f';pin(p,sourceproof['input_pins'][str(p)]);a=np.fromfile(p,dtype='<f4').reshape(m['height'],m['width'],4)[:512,:512][::-1].copy();a[:,:,2:]*=-1;arrays.append(a)
source_default,source_fine=arrays;native_default=[];native_fine=[]
for name in ['du_dx','dv_dx','du_dy','dv_dy']:
 p=w/'townsmith-depth-bound-uv-comparison-v27'/f'{name}.native.r32f';pin(p,default['input_pins'][str(p)]);native_default.append(np.fromfile(p,dtype='<f4').reshape(512,512))
 p=w/'townsmith-fine-uv-comparison-v28'/f'{name}_fine.native.r32f';pin(p,fine['input_pins'][str(p)]);native_fine.append(np.fromfile(p,dtype='<f4').reshape(512,512))
native_default=np.stack(native_default,2);native_fine=np.stack(native_fine,2);yy,xx=np.indices((512,512));rows=[]
for backend,base,detail in [('Unity source',source_default,source_fine),('Forge native',native_default,native_fine)]:
 for ch,name in enumerate(['du_dx','dv_dx','du_dy','dv_dy']):
  for choice in [0,1]:
   if ch<2:predicted=detail[(yy//2)*2+choice,xx,ch];label='native_row_'+str(choice)
   else:predicted=detail[yy,(xx//2)*2+choice,ch];label='native_column_'+str(choice)
   delta=np.abs(base[:,:,ch].astype(np.float64)-predicted.astype(np.float64))[quads];rows.append(dict(backend=backend,component=name,quad_selection_model=label,diagnostic_pixels=int(quads.sum()),mean_absolute_error=float(delta.mean()),max_absolute_error=float(delta.max()),exact_value_pixels=int((delta==0).sum())))
root.mkdir();result=dict(status='captured_derivative_quad_row_and_column_models_compared',finished_utc=datetime.now(timezone.utc).isoformat(),rows=rows,input_pins=pins,required_roi_pixels=34445,full_quad_diagnostic_pixels=int(quads.sum()),boundary_pixels_excluded_from_diagnostic_only=int(mask.sum()-quads.sum()),required_roi_unchanged=True,model_comparison_not_gpu_fix_qualification=True,full_import=False)
(root/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
