from pathlib import Path
import json,hashlib,struct,runpy,datetime
S=Path(__file__).parent
m=runpy.run_path(str(S/'prepare_firepit_source_shadow_projection_v1.py'))
W=m['W'];read=m['read'];fc=m['fromcols'];mul=m['mul'];cols=m['cols']
O=W/'firepit-source-render-matrix-comparison-v1'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def bits(x):return struct.pack('<f',x)
assert not O.exists()
proofpath=W/'firepit-source-shadow-render-matrices-reopen-v1/RESULT.v1.json'
proof=read(proofpath)
for name,h in proof['input_pins'].items():assert sha(Path(name))==h,name
casterproofpath=W/'firepit-source-shadow-caster-view-gpu-reopen-v1/RESULT.v1.json'
cp=read(casterproofpath)
for name,h in cp['input_pins'].items():assert sha(Path(name))==h,name
casepath=W/'firepit-source-shadow-projection-case-v1/CASE.v1.json';case=read(casepath)
campath=W/'firepit-source-shadow-context-native-v1/CAMERA-STATE-0.json';cam=read(campath)
rows=cam['source_uniform_rows'];assert all(r['source_world_from_target_world']==rows[0]['source_world_from_target_world'] for r in rows)
world=fc(rows[0]['source_world_from_target_world']);eye=case['source_shadow_context_replay']['source_culling_inputs']['origin']
t=[[float(r==c) for c in range(4)] for r in range(4)]
for i,k in enumerate(['x','y','z']):t[i][3]=-eye[k]
rws=mul(t,world)
sourcepath=W/'firepit-source-shadow-raster-v6/SOURCE-CASTER-SPLITS-0.json'
source=read(sourcepath);checks=[];files=[Path(__file__),S/'prepare_firepit_source_shadow_projection_v1.py',proofpath,casterproofpath,casepath,campath,sourcepath]
for i,row in enumerate(source['splits']):
 plan=case['source_shadow_projection_replay']['cascades'][i];sd=plan['source_shadow_record']
 sampler_view=[[sd[f'rot{r}'][k] for k in ['x','y','z']]+[sd['pos'][['x','y','z'][r]]] for r in range(3)]+[[0.,0.,0.,1.]]
 view_mismatches=sum(bits(a)!=bits(b) for a,b in zip(row['view'],cols(sampler_view)))
 projection_mismatches=sum(bits(a)!=bits(b) for a,b in zip(row['device_projection_y_flip'],plan['clip_from_cascade']))
 raw=W/f'firepit-source-shadow-caster-view-gpu-native-v1/CASTER-VIEW-0-{i}.float32';gpu=struct.unpack('<112f',raw.read_bytes());files.append(raw)
 gpu_plan_mismatches=sum(bits(a)!=bits(b) for a,b in zip(gpu[:16],plan['clip_from_world']))
 target=cols(mul(fc(row['clip_from_world']),rws));err=max(abs(a-b) for a,b in zip(target,gpu[:16]))
 checks.append(dict(cascade=i,source_raster_vs_sampler_view_f32_bit_mismatches=view_mismatches,source_raster_vs_native_projection_f32_bit_mismatches=projection_mismatches,native_gpu_vs_declared_reconstruction_clip_f32_bit_mismatches=gpu_plan_mismatches,direct_product_double_vs_gpu_f32_max_absolute_error=err))
 assert view_mismatches==projection_mismatches==gpu_plan_mismatches==0
result=dict(status='source_actual_raster_matrices_equal_declared_native_reconstruction_verified',finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),source_cascades_verified=4,source_native_matrix_fields_bit_compared=192,checks=checks,coordinate_contract='Previously declared source camera relative frame: Translate(-sourceEye) * sourceWorldFromTargetWorld; no pixel alignment or fitted transform',source_gpu_uniform_readback_verified=False,native_caster_gpu_readback_verified=True,source_raster_matrices_reconstructed_without_changed_policy=True,full_shadow_fidelity_qualified=False,full_import=False,complete_assets_delivered_increment=0,next_action='Compare actual caster geometry, winding/cull state and depth bias against source; actual source render projection is not an identified mismatch.',input_pins={str(p):sha(p) for p in files})
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
