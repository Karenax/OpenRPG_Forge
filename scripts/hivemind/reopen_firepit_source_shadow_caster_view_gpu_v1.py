from pathlib import Path
import datetime,hashlib,json,math,struct
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-source-shadow-caster-view-gpu-native-v1';OLD=W/'firepit-source-shadow-raster-native-v7';ROOT=W/'firepit-source-shadow-caster-view-gpu-reopen-v1'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def bits(v):return struct.pack('<f',v)
if __name__=='__main__':
 assert not ROOT.exists()
 run=read(CAP/'RUN-RESULT.v1.json');assert run.get('actual_exit_code')==0,run
 prep=read(CAP/'PREPARED.v1.json')
 for p,h in prep['input_pins'].items():assert sha(p)==h,p
 case=read(CAP/'CASE.v1.json');plan=case['source_shadow_projection_replay']['cascades'];assert len(plan)==4
 assert sha(CAP/'CASE.v1.json')==sha(OLD/'CASE.v1.json')
 files=[Path(__file__).resolve(),CAP/'PREPARED.v1.json',CAP/'RUN-RESULT.v1.json',CAP/'CASE.v1.json',OLD/'CASE.v1.json'];checks=[];total=compared=0
 for view in range(2):
  report=read(CAP/f'CASTER-VIEW-GPU-{view}.json');assert report['source_uniform_buffer_bound'] and report['gpu_scalars_read']==448 and report['cpu_gpu_scalars_compared']==192
  rows=report['orthographic_cascades'];assert [r['cascade'] for r in rows]==[0,1,2,3]
  for row in rows:
   index=row['cascade'];raw=CAP/f'CASTER-VIEW-{view}-{index}.float32';values=[v[0] for v in struct.iter_unpack('<f',raw.read_bytes())];assert len(values)==112 and all(math.isfinite(v) for v in values);assert all(bits(a)==bits(b) for a,b in zip(values,row['gpu_values']))
   assert row['gpu_cpu_matrix_bit_mismatches']==0;total+=len(values);compared+=48
   for offset,key in [(0,'clip_from_world'),(48,'world_from_cascade'),(80,'clip_from_cascade')]:
    expected=plan[index][key];actual=values[offset:offset+16];mismatches=sum(bits(a)!=bits(b) for a,b in zip(actual,expected));checks.append(dict(view=view,cascade=index,field=key,bit_mismatches=mismatches,maximum_absolute_error=max(abs(a-b) for a,b in zip(actual,expected))))
   files.append(raw)
  for name in [f'gpu-{view}.rgba16f',f'CASTER-VIEW-GPU-{view}.json']:
   if name.startswith('gpu-'):assert sha(CAP/name)==sha(OLD/name),'Uniform probe changed rendered image'
   files.append(CAP/name)
  files.append(OLD/f'gpu-{view}.rgba16f')
 control=read(CAP/'KNOWN-DEPTH-PROBE-CONTROL.json');assert control['mismatches_per_layer']==[0,0,0,0];files.append(CAP/'KNOWN-DEPTH-PROBE-CONTROL.json')
 matched=all(c['bit_mismatches']==0 for c in checks)
 result=dict(status='caster_view_uniform_gpu_source_projection_bits_verified' if matched else 'caster_view_uniform_gpu_source_projection_mismatch_measured',created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),gpu_scalars_read=total,cpu_gpu_scalars_compared=compared,source_projection_gpu_scalars_compared=384,checks=checks,images_unchanged=2,depth_probe_positive_control_passed=True,native_source_pins_verified=len(prep['input_pins']),source_geometry_unchanged=True,full_import=False,full_shadow_fidelity_qualified=False,complete_assets_delivered_increment=0,next_action='Inspect rasterized caster geometry, cull and coverage against source' if matched else 'Repair the actual caster view uniform transport against the pinned source projection before changing receiver/filter',input_pins={str(p):sha(p) for p in files})
 ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','checks']}))
