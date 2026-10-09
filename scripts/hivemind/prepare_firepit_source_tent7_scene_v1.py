"""Apply GPU-verified tent7 filter to private shaders; native biases retained."""
from pathlib import Path
import json,struct,hashlib,datetime,copy
W=Path('F:/OpenRPG_ImportWork');S=Path(__file__).parent;ROOT=W/'firepit-source-tent7-scene-case-v1'
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text('utf-8-sig'))
def main():
 assert not ROOT.exists()
 old=W/'firepit-source-shadow-projection-case-v1';proof=read(old/'RESULT.v1.json');case=read(old/'CASE.v1.json')
 verified=W/'source-shadow-tent7-reopen-v1/RESULT.v1.json';vp=read(verified);assert vp['status']=='actual_source_native_tent7_gpu_weights_uv_verified'
 for p,h in {**proof['input_pins'],**vp['input_pins']}.items():assert sha(p)==h,p
 fixture=Path(proof['private_fixture']);assert sha(fixture)==proof['private_fixture_sha256']
 original=fixture.read_bytes();n=struct.unpack_from('<I',original,12)[0];doc=json.loads(original[20:20+n]);binary=bytearray(original[28+n:]);old_binary=bytes(binary)
 geometry_before=copy.deepcopy({k:doc[k] for k in ['nodes','meshes','accessors']})
 math_path=S/'candidates/SourceShadowTent7.wgsl';sampling_path=S/'candidates/SourceShadowTent7Sampling.wgsl'
 added=math_path.read_text()+sampling_path.read_text()
 def mutate(text):
  assert text.count('shadows::fetch_directional_shadow(')==2
  return added+'\n'+text.replace('shadows::fetch_directional_shadow(','source_tent7_fetch_directional_shadow(')
 changes=[]
 for material in doc['materials']:
  slot=material['extensions']['OPENRPG_source_graph']['forward'];view=doc['bufferViews'][slot['buffer_view']];offset=view.get('byteOffset',0);data=bytes(binary[offset:offset+view['byteLength']]);assert hashlib.sha256(data).hexdigest()==slot['sha256']
  changed=mutate(data.decode()).encode();binary.extend(b'\0'*((-len(binary))%4));new_view={'buffer':0,'byteOffset':len(binary),'byteLength':len(changed)};binary.extend(changed)
  slot.update(buffer_view=len(doc['bufferViews']),sha256=hashlib.sha256(changed).hexdigest());doc['bufferViews'].append(new_view);changes.append({'material':material['name'],'old_sha256':hashlib.sha256(data).hexdigest(),'new_sha256':slot['sha256']})
 assert bytes(binary[:len(old_binary)])==old_binary;assert geometry_before=={k:doc[k] for k in geometry_before}
 doc['buffers'][0]['byteLength']=len(binary);binary.extend(b'\0'*((-len(binary))%4));encoded=json.dumps(doc,separators=(',',':')).encode();encoded+=b' '*((-len(encoded))%4)
 ROOT.mkdir();out=ROOT/'private-source-tent7-scene.glb';out.write_bytes(b'glTF'+struct.pack('<II',2,28+len(encoded)+len(binary))+struct.pack('<I',len(encoded))+b'JSON'+encoded+struct.pack('<I',len(binary))+b'BIN\0'+binary)
 receiver=Path(case['receiver_program']['forward']['path']);assert sha(receiver)==case['receiver_program']['forward']['sha256'];target=ROOT/'private-receiver-forward.wgsl';target.write_text(mutate(receiver.read_text()),'utf-8');case['receiver_program']['forward'].update(path=str(target),sha256=sha(target))
 case.update(fixture=str(out),fixture_sha256=sha(out),source_tent7_filter_applied=True,source_shadow_bias_matched=False,source_shadow_atlas_borders_matched=False)
 (ROOT/'CASE.v1.json').write_text(json.dumps(case,indent=2),'utf-8')
 files=[Path(__file__).resolve(),math_path,sampling_path,fixture,receiver,verified,old/'RESULT.v1.json',old/'CASE.v1.json',out,target]
 proof.update(status='private_source_tent7_scene_filter_prepared_native_bias_retained',private_fixture=str(out),private_fixture_sha256=sha(out),shader_changes=changes,original_binary_prefix_unchanged=len(old_binary),nodes_meshes_accessors_unchanged=True,input_pins={**proof['input_pins'],**{str(p):sha(p) for p in files}},source_bias_matched=False,full_import=False,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
 (ROOT/'RESULT.v1.json').write_text(json.dumps(proof,indent=2),'utf-8');print(json.dumps({'root':str(ROOT),'shader_changes':len(changes),'full_import':False}))
if __name__=='__main__':main()
