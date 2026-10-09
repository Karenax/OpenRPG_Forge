"""Retry only six unsupported vector-node kernels; preserve every prior success/refusal."""
import datetime,hashlib,json,subprocess,sys
from pathlib import Path
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'delta-vector-node-converter-v3'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(p,d):
 with p.open('x',encoding='utf-8') as f:json.dump(d,f,ensure_ascii=False,indent=2)
def main():
 assert not ROOT.exists()
 topology=W/'nested-prefab-delta-material-active-ir-v1/RESULT.v1.json'
 prior=W/'nested-prefab-delta-material-wgsl-v1/RESULT.v1.json'
 emitter=W/'dungeon-original-material-wgsl-v1/adapted-node-emitter.py.txt'
 generator=Path(__file__).with_name('generate_nested_material_wgsl.py')
 native_dir=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.shadergraph@8d7376ec43ee/Editor/Data'
 normalize=native_dir/'Nodes/Math/Advanced/NormalizeNode.cs';transform=native_dir/'Nodes/Math/Vector/TransformNode.cs';spaces=native_dir/'Util/SpaceTransformUtil.cs'
 pins={str(p):sha(p) for p in [topology,prior,emitter,generator,normalize,transform,spaces,Path(__file__)]}
 assert 'Out = normalize(In);' in normalize.read_text()
 assert 'GetCameraRelativePositionWS(' in spaces.read_text() and 'TransformWorldToObject(' in spaces.read_text()
 old=read(prior);batch=read(topology)
 wanted={r['material_guid'] for r in old['failures'] if r['error']=='Unsupported source opcode NormalizeNode'}
 rows=[r for r in batch['rows'] if r['material_guid'] in wanted]
 assert len(rows)==len(wanted)==6
 for row in rows:
  assert sha(row['output'])==row['output_sha256']
  ir=read(row['output']);nodes=[n for n in ir['nodes'] if n['opcode']=='TransformNode']
  assert len(nodes)==1
  t=nodes[0]['source_parameters'];assert t['m_SGVersion']==1 and t['m_ConversionType']==0 and t['m_Conversion']=={'from':4,'to':0}
 for p,expected in batch['source_pins'].items():assert sha(p)==expected
 ROOT.mkdir()
 subset=dict(batch);subset.update(materials=6,compiled_materials=6,rows=rows)
 save(ROOT/'TOPOLOGY-SUBSET.v3.json',subset)
 text=emitter.read_text('utf-8-sig')
 marker=" elif op=='VertexColorNode':"
 assert text.count(marker)==1
 addition=""" elif op=='TransformNode':
  assert p['m_SGVersion']==1 and p['m_ConversionType']==0 and p['m_Conversion']=={'from':4,'to':0},'Unqualified source transform variant'
  input_requirements.extend([{'field':'source_camera_relative_offset','source_semantics':'camera position when Unity camera-relative rendering is enabled, zero only when disabled','host_qualified':False},{'field':'source_world_to_object','source_semantics':'Unity world-to-object matrix in the same camera-relative world space','host_qualified':False}])
  expr='vec4<f32>((i.source_world_to_object*vec4<f32>(('+get('In')+').xyz-i.source_camera_relative_offset,1.0)).xyz,0.0)';dim=3
"""
 text=text.replace(marker,addition+marker,1)
 marker="  elif op=='AbsoluteNode':"
 assert text.count(marker)==1
 text=text.replace(marker,"""  elif op=='NormalizeNode':
   component=('x','xy','xyz','xyzw')[dim-1];n='normalize(('+get('In')+').'+component+')';expr='vec4<f32>('+n+(','+','.join('0.0' for _ in range(4-dim)) if dim<4 else '')+')'
"""+marker,1)
 marker="fields=[('view_direction_world',3)"
 assert text.count(marker)==1
 text=text.replace(marker,"fields=[('source_world_to_object',16),('source_camera_relative_offset',3),('view_direction_world',3)",1)
 marker="typename=lambda n:'f32' if n==1 else f'vec{n}<f32>'"
 assert text.count(marker)==1
 text=text.replace(marker,"typename=lambda n:'mat4x4<f32>' if n==16 else ('f32' if n==1 else f'vec{n}<f32>')",1)
 old_uv=" if op=='UVNode':assert p['m_OutputChannel']==0;expr='i.uv0';dim=4"
 new_uv=""" if op=='UVNode':
  channel=p['m_OutputChannel'];assert channel in (0,1),'Source UV channel requires a qualified adapter';field='uv'+str(channel);input_requirements.append({'field':field,'source_uv_channel':channel,'host_qualified':False});expr='i.'+field;dim=4"""
 assert text.count(old_uv)==1;text=text.replace(old_uv,new_uv,1)
 assert text.count("('uv0',4)")==1;text=text.replace("('uv0',4)","('uv0',4),('uv1',4)",1)
 adapted_emitter=ROOT/'adapted-node-emitter.v3.py.txt';adapted_emitter.write_text(text,encoding='utf-8')
 compile(text,str(adapted_emitter),'exec')
 original=generator.read_text('utf-8-sig');adapted=original
 replacements=[('ROOT = W / "nested-prefab-material-wgsl-v1"','ROOT = W / "delta-vector-node-wgsl-v3"'),('EMITTER = W / "dungeon-original-material-wgsl-v1/adapted-node-emitter.py.txt"','EMITTER = W / "delta-vector-node-converter-v3/adapted-node-emitter.v3.py.txt"'),('nested-prefab-material-active-ir-v1/RESULT.v1.json','delta-vector-node-converter-v3/TOPOLOGY-SUBSET.v3.json'),('batch["compiled_materials"] != 16','batch["compiled_materials"] != 6'),('"total": 16','"total": 6'),('"requested_materials": 16','"requested_materials": 6')]
 for before,after in replacements:
  assert adapted.count(before)==1,before;adapted=adapted.replace(before,after)
 script=ROOT/'generate_selected_kernels.v3.py';script.write_text(adapted,encoding='utf-8');compile(adapted,str(script),'exec')
 with (ROOT/'generator.log').open('xb') as log:code=subprocess.run([sys.executable,'-B',str(script)],stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW).returncode
 output=W/'delta-vector-node-wgsl-v3/RESULT.v1.json';result=read(output) if output.exists() else {}
 for p,expected in pins.items():assert sha(p)==expected
 save(ROOT/'RESULT.v3.json',dict(status='six_vector_node_kernels_validated' if code==0 and result.get('naga_validated_materials')==6 else 'retained_conversion_refusals',observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),actual_exit_code=code,requested_materials=6,naga_validated_materials=result.get('naga_validated_materials',0),failures=result.get('failures',[]),result=str(output),result_sha256=sha(output) if output.exists() else None,input_pins=pins,original_sources_unchanged=True,source_transform_variant={'version':1,'from':4,'to':0,'type':0},new_host_input_fields=['source_world_to_object','source_camera_relative_offset'],zero_vector_behavior_not_gpu_qualified=True,host_bindings_qualified=False,gpu_render_parity_qualified=False,full_import=False))
 print(json.dumps(dict(exit_code=code,validated=result.get('naga_validated_materials',0),failures=result.get('failures',[]),receipt=str(ROOT/'RESULT.v3.json'))))
if __name__=='__main__':main()
