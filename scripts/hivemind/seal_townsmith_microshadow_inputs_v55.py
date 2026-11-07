from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
import numpy as np
W=Path('F:/OpenRPG_ImportWork');D=W/'townsmith-door-source-microshadow-inputs-v55';O=W/'townsmith-source-microshadow-inputs-reopen-v55'
assert not O.exists()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
run=read(D/'RUN-RESULT.v1.json');assert run['actual_exit_code']==0 and run['source_inputs_unchanged'] and run['neutral_reference_byte_equal']
assert sha(D/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256']
prep=read(D/'PREPARED.v1.json');pins=dict(prep['input_pins']);pins.update(prep['private_before_pins'])
for p,h in pins.items():assert sha(Path(p))==h,p
for p in [D/'RUN-RESULT.v1.json',D/'NATIVE-RESULT.v1.json',D/'PREPARED.v1.json',Path(__file__)]:pins[str(p)]=sha(p)
rows=[]
for name in ['door-neutral','door-nopost-linear','door-noaniso-nopost-linear']:
 depth=D/(name+'.depth-3.r32f');mask=np.fromfile(depth,dtype='<f4').reshape(512,512)>0;assert int(mask.sum())==34445;pins[str(depth)]=sha(depth)
 for mode in ['sun','gbuffer0','gbuffer2']:
  paths=[D/(name+f'.actual-{mode}-{i}.rgba32f') for i in range(4)];assert all(p.exists() for p in paths);assert sha(paths[-1])==sha(paths[-2])
  for p in paths:pins[str(p)]=sha(p)
  a=np.fromfile(paths[-1],dtype='<f4').reshape(-1,4);assert np.isfinite(a).all()
  row=dict(case=name,resource=mode,repeated_full_buffers_byte_equal=True,captures=4)
  if mode=='sun':
   assert a.shape==(4,4);assert np.array_equal(a[0],np.array([15000,15000,15000,1],dtype=np.float32));assert np.array_equal(a[2,:2],[1,1]) and a[2,3]==-1
   assert np.isclose(np.linalg.norm(a[1,:3]),1,atol=1e-6)
   row.update(color_and_light_dimmer=a[0].tolist(),forward_and_min_roughness=a[1].tolist(),diffuse_specular_angular_shadow=a[2].tolist(),actual_gpu_shadow_tint=a[3,:3].tolist(),actual_gpu_micro_shadow_opacity=float(a[3,3]))
  else:
   assert a.shape==(512*512,4);a=a.reshape(512,512,4)
   assert np.array_equal(a[:,:,0]>0,mask)
   row.update(coverage_equals_actual_depth=True,covered_pixels=34445,rgba_min=a[mask].min(axis=0).tolist(),rgba_max=a[mask].max(axis=0).tolist())
  rows.append(row)
 for p in [D/(name+'.inpass-normal-3.rgba32f'),W/'townsmith-door-source-inpass-normal-v31'/(name+'.inpass-normal-3.rgba32f')]:pins[str(p)]=sha(p)
 assert sha(D/(name+'.inpass-normal-3.rgba32f'))==sha(W/'townsmith-door-source-inpass-normal-v31'/(name+'.inpass-normal-3.rgba32f'))
assert sha(D/'door-nopost-linear.rgba16f')==sha(W/'townsmith-door-source-inpass-normal-v31/door-nopost-linear.rgba16f')
prior_path=W/'townsmith-source-deferred-inputs-reopen-v49/RESULT.v1.json';prior=read(prior_path);pins[str(prior_path)]=sha(prior_path)
for name in ['door-neutral','door-nopost-linear','door-noaniso-nopost-linear']:
    for mode in ['sun','gbuffer0','gbuffer2']:
        old=W/'townsmith-door-source-deferred-inputs-v49'/(name+f'.actual-{mode}-3.rgba32f')
        assert sha(old)==prior['input_pins'][str(old)];pins[str(old)]=sha(old)
        before=np.fromfile(old,dtype='<f4').reshape(-1,4);after=np.fromfile(D/(name+f'.actual-{mode}-3.rgba32f'),dtype='<f4').reshape(-1,4)
        assert np.array_equal(before,after[:len(before)]),'PRIOR_INPUT_CHANGED'
    context=D/(name+'.context.json');pins[str(context)]=sha(context)
    assert read(context)['actual_lit_shader_mode']=='Deferred'

source=W/'townsmith-door-source-material-values-v41';metadata=read(source/'door-nopost-linear.graph-probe-9.json');shape=(metadata['height'],metadata['width'],4)
base_path=source/'door-nopost-linear.graph-probe-9.rgba32f';scalar_path=source/'door-nopost-linear.graph-probe-10.rgba32f'
base=np.fromfile(base_path,dtype='<f4').reshape(shape)[:512,:512,:3];scalar=np.fromfile(scalar_path,dtype='<f4').reshape(shape)[:512,:512,:1]
x=np.maximum(base*(1-scalar),0);encoded=np.where(x<=.0031308,12.92*x,1.055*x**(1/2.4)-.055);c=np.round(np.clip(encoded,0,1)*255)/255
analytic=np.where(c<=.04045,c/12.92,((c+.055)/1.055)**2.4)
actual=np.fromfile(D/'door-nopost-linear.actual-gbuffer0-3.rgba32f',dtype='<f4').reshape(512,512,4)[:,:,:3]
mask=np.fromfile(D/'door-nopost-linear.depth-3.r32f',dtype='<f4').reshape(512,512)>0;delta=np.abs(actual-analytic)[mask]
for p in [base_path,scalar_path,source/'door-nopost-linear.graph-probe-9.json',D/'door-nopost-linear.rgba16f']:pins[str(p)]=sha(p)
result=dict(status='actual_inpass_microshadow_uniform_and_deferred_inputs_reopened',finished_utc=datetime.now(timezone.utc).isoformat(),rows=rows,input_pins=pins,rehashed_pins=len(pins),source_original_reference_and_normals_unchanged=True,captures=36,prior_v49_sun_normal_and_gbuffer_bytes_unchanged=True,micro_shadow_opacity_gpu_observations=12,analytic_source_diffuse_storage_mean_error=float(delta.mean()),analytic_source_diffuse_storage_max_error=float(delta.max()),analytic_storage_is_diagnostic_only=True,source_pixels_applied_to_native=False,thresholds_unchanged=True,full_import=False,forge_editor_verified=False,complete_assets_delivered_increment=0)
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','rows']}))
