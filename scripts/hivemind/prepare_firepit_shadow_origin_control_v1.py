from pathlib import Path
import json,runpy,hashlib,datetime
W=Path('F:/OpenRPG_ImportWork');OLD=W/'firepit-source-shadow-raster-case-v1';O=W/'firepit-source-shadow-origin-case-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert not O.exists()
m=runpy.run_path(str(Path(__file__).with_name('prepare_firepit_source_shadow_projection_v1.py')));mul=m['mul'];fc=m['fromcols'];cols=m['cols'];inverse=m['inverse']
prior=read(OLD/'RESULT.v1.json');case=read(OLD/'CASE.v1.json')
for name,h in prior['input_pins'].items():assert sha(Path(name))==h,name
srcpath=W/'firepit-source-shadow-raster-v6/SOURCE-CASTER-SPLITS-0.json';src=read(srcpath);eye=case['source_shadow_context_replay']['source_culling_inputs']['origin']
reflection=[[-1.,0.,0.,0.],[0.,1.,0.,0.],[0.,0.,1.,0.],[0.,0.,0.,1.]];translate=[[float(r==c) for c in range(4)] for r in range(4)]
for i,k in enumerate(['x','y','z']):translate[i][3]=-eye[k]
rws=mul(translate,reflection);checks=[]
for row,old in zip(src['splits'],case['source_shadow_projection_replay']['cascades']):
 view=mul(fc(row['view']),rws);projection=fc(row['device_projection_y_flip']);clip=mul(projection,view)
 rebased=mul(fc(old['clip_from_world']),[[1.,0.,0.,0.],[0.,1.,0.,1024.],[0.,0.,1.,0.],[0.,0.,0.,1.]])
 err=max(abs(a-b) for a,b in zip(cols(clip),cols(rebased)));assert err<1e-9
 old.update(world_from_cascade=cols(inverse(view)),clip_from_cascade=cols(projection),clip_from_world=cols(clip));checks.append({'cascade':row['split_index'],'analytical_rebase_max_error':err})
case['private_origin_control']={'original_root_translation':[0,1024,0],'diagnostic_root_translation':[0,0,0],'actual_source_raster_matrices_used':True,'source_geometry_and_bias_unchanged':True,'no_pixel_fitting':True,'full_fidelity_qualified':False}
O.mkdir();(O/'CASE.v1.json').write_text(json.dumps(case,indent=2),encoding='utf-8')
files=[Path(__file__),Path(__file__).with_name('prepare_firepit_source_shadow_projection_v1.py'),OLD/'CASE.v1.json',OLD/'RESULT.v1.json',srcpath,O/'CASE.v1.json']
result={**prior,'status':'analytical_origin_precision_control_prepared','created_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'checks':checks,'private_fixture':case['fixture'],'private_fixture_sha256':case['fixture_sha256'],'geometry_and_raster_bias_unchanged':True,'source_frames_and_thresholds_unchanged':True,'full_import':False,'input_pins':{**prior['input_pins'],**{str(p):sha(p) for p in files}}}
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({'root':str(O),'checks':checks}))
