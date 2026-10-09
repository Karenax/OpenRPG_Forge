"""Reopen source pixels; measure shadow response outside the projected caster bounds."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,math,struct
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-shadow-source-reference-v1';ROOT=W/'firepit-shadow-source-measurement-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def vec(d):return [d[k] for k in ['x','y','z']]
def sub(a,b):return [x-y for x,y in zip(a,b)]
def dot(a,b):return sum(x*y for x,y in zip(a,b))
def cross(a,b):return [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
def norm(a):length=math.sqrt(dot(a,a));assert length>0;return [x/length for x in a]
def main():
 assert not ROOT.exists();run=read(CAP/'RUN-RESULT.v1.json');prep=read(CAP/'PREPARED.v1.json');n=read(CAP/'NATIVE-RESULT.v1.json')
 assert run['actual_exit_code']==0 and run['source_inputs_unchanged'] and n['source_object_unchanged'] and n['preview_scene_isolated'] and sha(CAP/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256']
 for p,h in prep['input_pins'].items():assert sha(p)==h,p
 on,off=n['images'];assert on['source_shadows'] and not off['source_shadows']
 for key in ['camera_position','target','light_euler','source_bounds_center','source_bounds_size','width','height']:assert on[key]==off[key],key
 for row in [on,off]:assert sha(row['path'])==row['sha256'] and sha(row['raw_path'])==row['raw_sha256']
 eye=vec(on['camera_position']);forward=norm(sub(vec(on['target']),eye));right=norm(cross([0,1,0],forward));up=cross(forward,right);center=vec(on['source_bounds_center']);size=vec(on['source_bounds_size']);points=[]
 for x in [-.5,.5]:
  for y in [-.5,.5]:
   for z in [-.5,.5]:
    p=sub([center[0]+size[0]*x,center[1]+size[1]*y,center[2]+size[2]*z],eye);depth=dot(p,forward);assert depth>0
    points.append([(dot(p,right)/depth/math.tan(math.radians(15))*.5+.5)*512,(dot(p,up)/depth/math.tan(math.radians(15))*.5+.5)*512])
 box=[math.floor(min(p[0] for p in points))-2,math.floor(min(p[1] for p in points))-2,math.ceil(max(p[0] for p in points))+2,math.ceil(max(p[1] for p in points))+2]
 a=Path(on['raw_path']).read_bytes();b=Path(off['raw_path']).read_bytes();assert len(a)==len(b)==512*512*8
 changed=0;outside_changed=0;outside_darker=0;outside_brighter=0;maximum=0.;total=0.;mask=bytearray(512*512)
 for i,(aa,bb) in enumerate(zip(struct.iter_unpack('<4e',a),struct.iter_unpack('<4e',b))):
  assert all(math.isfinite(v) for v in aa+bb)
  diffs=[bb[j]-aa[j] for j in range(3)];delta=max(abs(v) for v in diffs);maximum=max(maximum,delta);total+=sum(abs(v) for v in diffs)
  if delta>1e-4:
   changed+=1;x=i%512;y=i//512
   if not(box[0]<=x<=box[2] and box[1]<=y<=box[3]):
    outside_changed+=1;mask[i]=255
    if sum(diffs)>1e-4:outside_darker+=1
    elif sum(diffs)<-1e-4:outside_brighter+=1
 qualified=outside_changed>0 and outside_darker/outside_changed>.95
 ROOT.mkdir();(ROOT/'outside-caster-response-mask.pgm').write_bytes(b'P5\n512 512\n255\n'+mask)
 result=dict(status='source_shadow_response_outside_caster_bounds_verified' if qualified else 'source_pair_captured_but_shadow_response_refused',finished_utc=datetime.now(timezone.utc).isoformat(),sub_lot='L01.OMBRES.02.SOURCE',images_verified=2,raw_pixels_verified=512*512*2,changed_rgb_pixels=changed,outside_caster_changed_pixels=outside_changed,outside_caster_darker_with_shadows=outside_darker,outside_caster_brighter_with_shadows=outside_brighter,maximum_linear_channel_delta=maximum,mean_absolute_linear_channel_delta=total/(512*512*3),projected_source_bounds_box=box,source_shadow_response_verified=qualified,source_shader_passes_compiled=len(n['compiled_passes']),source_dependencies_verified=len(prep['dependency_paths']),source_files_rehashed=len(prep['input_pins']),private_source_importer_changes=run['private_changed'],input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),CAP/'PREPARED.v1.json',CAP/'RUN-RESULT.v1.json',CAP/'NATIVE-RESULT.v1.json',*[Path(r[k]) for r in [on,off] for k in ['path','raw_path']]]},presentation_fixed_ev=n['presentation_fixed_ev'],presentation_tonemapping=n['presentation_tonemapping'],original_project_version_qualified=False,forced_lod0_diagnostic_only=True,forge_image_compared=False,shadow_fidelity_qualified=False,editor_visual_verified=False,full_import=False,next_action='Capture Forge avec mêmes caméra, géométrie source, paramètres lumineux et présentation ; comparer ombre et pixels' if qualified else 'Diagnostiquer la capture source avant toute comparaison Forge')
 (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2,ensure_ascii=False),'utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'},ensure_ascii=False))
if __name__=='__main__':main()
