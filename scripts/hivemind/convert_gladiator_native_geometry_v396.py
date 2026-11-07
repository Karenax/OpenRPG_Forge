"""Convert real Unity geometry buffers with reversible basis and winding checks."""
from pathlib import Path
from datetime import datetime,timezone
import base64,json,hashlib,math,struct
W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-converted-geometry-v396'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not O.exists()
 receipt=W/'gladiator-native-binding-comparison-v391/RESULT.v1.json';source=W/'gladiator-native-geometry-v380/NATIVE-RESULT.v1.json';hierarchy=W/'gladiator-source-binding-bridge-v390/RESULT.v1.json'
 proof=read(receipt);assert proof['status']=='four_native_flag_lods_exact_mesh_renderer_and_material_identities_matched' and sha(source)==proof['input_pins'][str(source)];native=read(source);source_case=next(r for r in read(hierarchy)['rows'] if r['prefab_guid']==native['prefab_guid']);nodes={n['path']:n for n in source_case['nodes']};native_bindings={b['path']:b for b in native['bindings']}
 for path,b in native_bindings.items():nodes[path]['materials']=b['materials']
 reopen_path=W/'gladiator-native-geometry-reopen-v381/RESULT.v1.json';reopened=read(reopen_path);assert reopened['status']=='four_native_lod_mesh_buffers_reopened' and reopened['native_receipt_sha256']==sha(source)
 bindings=native['bindings'];assert len(native['meshes'])==4
 O.mkdir();outputs={};converted=[]
 for mesh in native['meshes']:
  identity=mesh['identity'];folder=O/identity['file_id'];folder.mkdir();streams={s['index']:base64.b64decode(s['bytes_base64'],validate=True) for s in mesh['streams']};strides={s['index']:s['stride'] for s in mesh['streams']};target={k:bytearray(v) for k,v in streams.items()};flips=[];bounds=[]
  for attr in mesh['attributes']:
   assert attr['format']=='Float32' and attr['semantic'] in ['Position','Normal','Tangent','TexCoord0','TexCoord1'],attr
   expected={'Position':3,'Normal':3,'Tangent':4,'TexCoord0':2,'TexCoord1':2}[attr['semantic']];assert attr['dimension']==expected
   s=attr['stream'];offset=attr['offset'];stride=strides[s];assert offset+expected*4<=stride
   for i in range(mesh['vertices']):
    values=struct.unpack_from('<'+'f'*expected,streams[s],i*stride+offset);assert all(math.isfinite(v) for v in values)
    if attr['semantic'] in ['Position','Normal','Tangent']:
     offsets=[i*stride+offset+2*4]
     if attr['semantic']=='Tangent':offsets.append(i*stride+offset+3*4)
     for p in offsets:target[s][p+3]^=0x80;flips.append((s,p))
    if attr['semantic']=='Position':bounds.append((values[0],values[1],-values[2]))
  assert len(bounds)==mesh['vertices']
  for s,data in target.items():
   p=folder/f'vertex-stream-{s}.bin';p.write_bytes(data);reopened=bytearray(p.read_bytes())
   for si,offset in flips:
    if si==s:reopened[offset+3]^=0x80
   assert bytes(reopened)==streams[s],'Reversible attribute proof failed';outputs[str(p)]=sha(p)
  rawindex=base64.b64decode(mesh['index_bytes_base64'],validate=True);fmt={'UInt16':'H','UInt32':'I'}[mesh['index_format']];stride=struct.calcsize('<'+fmt);allindices=struct.unpack('<'+fmt*(len(rawindex)//stride),rawindex);primitives=[]
  for i,sub in enumerate(mesh['submeshes']):
   assert sub['topology']=='Triangles' and sub['index_count']%3==0
   original=allindices[sub['index_start']:sub['index_start']+sub['index_count']];assert len(original)==sub['index_count'];indices=[v+sub['base_vertex'] for v in original];assert all(0<=v<mesh['vertices'] for v in indices)
   for j in range(0,len(indices),3):indices[j+1],indices[j+2]=indices[j+2],indices[j+1]
   p=folder/f'primitive-{i}.indices-u32.bin';p.write_bytes(struct.pack('<'+'I'*len(indices),*indices));back=list(struct.unpack('<'+'I'*len(indices),p.read_bytes()))
   for j in range(0,len(back),3):back[j+1],back[j+2]=back[j+2],back[j+1]
   assert [v-sub['base_vertex'] for v in back]==list(original)
   outputs[str(p)]=sha(p);primitives.append(dict(material_slot=i,index_path=str(p),indices=len(indices),triangles=len(indices)//3,source_submesh=sub))
  used=[b for b in bindings if b['mesh']['guid']==identity['guid'] and b['mesh']['file_id']==identity['file_id']]
  assert len(used)==1
  binding=used[0];node=nodes[binding['path']]
  if binding['role']=='mesh_filter':assert len(node['materials'])==len(primitives)
  converted.append(dict(identity=identity,binding=binding,vertex_count=mesh['vertices'],attributes=mesh['attributes'],streams=[dict(index=s,stride=strides[s],path=str(folder/f'vertex-stream-{s}.bin')) for s in streams],primitives=primitives,bounds_min=[min(v[i] for v in bounds) for i in range(3)],bounds_max=[max(v[i] for v in bounds) for i in range(3)],source_node=node,materials_preserved=node['materials']))
 result=dict(status='four_native_meshes_basis_winding_and_attributes_converted_reversibly',finished_utc=datetime.now(timezone.utc).isoformat(),basis=dict(unity_to_target_diagonal=[1,1,-1],normal_z_negated=True,tangent_z_and_handedness_negated=True,winding_reversed=True,uv_channels_unchanged=True),meshes=converted,lod_groups=native['lod_groups'],all_vertex_bytes_reopened_and_reverse_compared=True,all_indices_reopened_and_reverse_compared=True,materials_converted=False,particle_runtime_converted=False,renderer_admitted=False,full_import=False,input_pins={str(p):sha(p) for p in [receipt,source,hierarchy,reopen_path,Path(__file__),Path(__file__).with_name('convert_torch_native_geometry_v152.py')]},output_pins=outputs)
 (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2,allow_nan=False),'utf-8')
 print(json.dumps(dict(meshes=len(converted),vertex_count=sum(m['vertex_count'] for m in converted),triangles=sum(p['triangles'] for m in converted for p in m['primitives']),files=len(outputs),renderer_admitted=False)))
if __name__=='__main__':main()
