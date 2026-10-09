from pathlib import Path
import json,hashlib,struct,datetime
W=Path('F:/OpenRPG_ImportWork');R=W/'firepit-source-shadow-mesh-matrix-native-v1';O=W/'firepit-source-shadow-mesh-matrix-reopen-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert not O.exists();run=read(R/'RUN-RESULT.v1.json');assert run['actual_exit_code']==0
prep=read(R/'PREPARED.v1.json');pins=prep['input_pins'].copy()
for name,h in pins.items():assert sha(Path(name))==h,name
control=read(R/'KNOWN-DEPTH-PROBE-CONTROL.json');assert control['samples']==1048576 and control['mismatches_per_layer']==[0,0,0,0]
meta=read(R/'NATIVE-SHADOW-DEPTH-0.json');assert all(row['ready'] and row['constant']==0 and row['slope_scale']==0 and row['clamp']==0 for row in meta['raster_pipelines'])
camera=read(R/'CAMERA-STATE-0.json');assert len(camera['draw_transforms'])==1;expected=camera['draw_transforms'][0]['world_from_local'];assert len(expected)==16
values=struct.unpack('<1048576f',(R/'NATIVE-SHADOW-DEPTH-0.float32').read_bytes());checks=[];samples=0
for layer in range(4):
 groups=[set() for _ in range(16)];counts=[0]*16
 for y in range(512):
  for x in range(512):
   v=values[layer*262144+y*512+x]
   if v!=0:
    k=x%4+4*(y%4);groups[k].add(v*4096-2048);counts[k]+=1;samples+=1
 if layer<2:assert sum(counts)==0;continue
 assert all(n>0 for n in counts)
 assert all(group=={expected[k]} for k,group in enumerate(groups))
 checks.append({'layer':layer,'samples_per_matrix_component':counts,'decoded_values':[next(iter(v)) for v in groups],'expected_mesh_world_matrix':expected,'numeric_mismatches':0})
files=[Path(__file__),R/'RUN-RESULT.v1.json',R/'PREPARED.v1.json',R/'CAMERA-STATE-0.json',R/'KNOWN-DEPTH-PROBE-CONTROL.json',R/'KNOWN-DEPTH-PROBE-CONTROL.float32',R/'NATIVE-SHADOW-DEPTH-0.json',R/'NATIVE-SHADOW-DEPTH-0.float32']
for file in files:pins[str(file)]=sha(file)
result=dict(status='actual_shadow_mesh_uniform_matrix_gpu_verified',finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),gpu_matrix_components_verified=32,depth_pixel_observations_verified=samples,checks=checks,depth_probe_positive_control_exact_samples=1048576,comparison='Exact decoded numeric equality; encoding does not preserve the sign of zero',actual_shadow_fragment_mesh_uniform_index_verified=True,diagnostic_depth_and_bias_changed=True,original_geometry_binary_prefix_unchanged=True,original_source_inputs_preserved=True,actual_vertex_clip_output_gpu_verified=False,source_gpu_vertex_output_verified=False,full_shadow_fidelity_qualified=False,editor_visual_verified=False,full_import=False,complete_assets_delivered_increment=0,next_action='Inspect actual vertex clip positions/precision and rasterized topology; shadow mesh uniform identity and transform are now ruled out as a mismatch.',input_pins=pins)
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','checks']}))
