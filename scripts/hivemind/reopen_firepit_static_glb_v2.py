from pathlib import Path
import json,struct,hashlib,copy,datetime
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-complete-static-glb-v2'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(raw):return hashlib.sha256(raw).hexdigest()
receipt=read(ROOT/'RESULT.v1.json');row=receipt['rows'][0];raw=Path(row['glb']).read_bytes();assert sha(raw)==row['sha256'];assert raw[:4]==b'glTF' and struct.unpack_from('<II',raw,4)==(2,len(raw))
n=struct.unpack_from('<I',raw,12)[0];assert raw[16:20]==b'JSON';doc=json.loads(raw[20:20+n]);assert raw[24+n:28+n]==b'BIN\0';binary=raw[28+n:];assert len(binary)==struct.unpack_from('<I',raw,20+n)[0]
scene=next(s for s in read(W/'rocks-firepit-scene-bindings-v1/RESULT.v1.json')['rows'] if s['asset_id']==row['asset_id'])
native=read(W/'firepit-static-material-native-reimport-v3/NATIVE-RESULT.v1.json')['cases'][0]
def verify(d,b):
 assert d['buffers'][0]['byteLength']<=len(b) and len(b)-d['buffers'][0]['byteLength']<4
 views=[]
 for v in d['bufferViews']:
  at=v.get('byteOffset',0);size=v['byteLength'];assert v['buffer']==0 and at>=0 and size>0 and at+size<=len(b);views.append(b[at:at+size])
 blobs=0
 def walk(x):
  nonlocal blobs
  if isinstance(x,dict):
   if 'buffer_view' in x and 'sha256' in x:assert sha(views[x['buffer_view']])==x['sha256'];blobs+=1
   for v in x.values():walk(v)
  elif isinstance(x,list):
   for v in x:walk(v)
 walk(d)
 assert len(d['nodes'])==len(scene['nodes'])==9 and len(d['materials'])==1
 indices={e['source_node']['path']:i for i,e in enumerate(scene['nodes'])};expected_children={i:[] for i in range(9)}
 roots=[]
 for i,e in enumerate(scene['nodes']):
  parent=e['source_node']['parent_path']
  if parent:expected_children[indices[parent]].append(i)
  else:roots.append(i)
 for i,(node,e) in enumerate(zip(d['nodes'],scene['nodes'])):
  assert node['matrix']==e['target_local_matrix_column_major'];assert node['extras']['unity_source_node']==e['source_node'];assert node.get('children',[])==expected_children[i]
 assert d['scenes'][0]['nodes']==roots and len(roots)==1
 assert d['extras']['original_lod_groups']==scene['original_lod_groups'];assert d['extras']['deferred_renderers']==scene['deferred_renderers'];assert len(scene['deferred_renderers'])==2
 mat=d['materials'][0];assert mat['extras']['native_material_observation']==native and not native['error'];assert mat['extensions']['OPENRPG_source_graph']['state']['render_queue']==native['render_queue']==2225
 streams=0;draws=0
 def accessor(index):
  a=d['accessors'][index];assert a.get('byteOffset',0)==0 and a['componentType'] in [5126,5125];width={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']];v=views[a['bufferView']];assert len(v)==a['count']*4*width;return v
 bindings={x['node_path']:x for x in scene['static_renderer_bindings']}
 for i,e in enumerate(scene['nodes']):
  path=e['source_node']['path']
  if path not in bindings:assert 'mesh' not in d['nodes'][i];continue
  binding=bindings[path];directory=Path(binding['geometry_derivative']);g=read(directory/'GEOMETRY.v1.json');mesh=d['meshes'][d['nodes'][i]['mesh']]
  original={Path(p).name:Path(p) for p in g['source_stream_pins']}
  assert len(mesh['primitives'])==g['metadata']['submeshes']
  for sub,primitive in enumerate(mesh['primitives']):
   assert primitive['mode']==4 and primitive['material']==0;assert accessor(primitive['indices'])==(directory/('submesh-'+str(sub)+'.target.u32')).read_bytes();streams+=1;draws+=1
   for attr,index in primitive['attributes'].items():
    if attr.startswith('_OPENRPG_SOURCE_'):file=original[{'NORMAL':'normals.f32','TANGENT':'tangents.f32','COLOR':'colors.f32'}.get(attr[len('_OPENRPG_SOURCE_'):],attr[len('_OPENRPG_SOURCE_'):].lower()+'.f32')]
    elif attr.startswith('TEXCOORD_'):file=directory/('uv'+attr.split('_')[-1]+'.target.f32')
    else:file=directory/({'POSITION':'positions','NORMAL':'normals','TANGENT':'tangents'}[attr]+'.target.f32')
    assert accessor(index)==file.read_bytes(),(path,attr);streams+=1
 assert draws==4
 return dict(embedded_blobs_reopened=blobs,geometry_stream_instances_reopened=streams,static_draws_reopened=draws,nodes_reopened=9)
counts=verify(doc,binary);negative=[]
def refuse(label,d,b):
 try:verify(d,b)
 except (AssertionError,KeyError,IndexError):negative.append(label);return
 raise AssertionError('Mutation wrongly accepted: '+label)
d=copy.deepcopy(doc);d['nodes'][0]['matrix'][12]+=1;refuse('node_transform',d,binary)
d=copy.deepcopy(doc);d['nodes'][0]['children']=[0];refuse('hierarchy_cycle',d,binary)
d=copy.deepcopy(doc);d['materials'][0]['extensions']['OPENRPG_source_graph']['state']['render_queue']=0;refuse('native_queue',d,binary)
d=copy.deepcopy(doc);d['extras']['deferred_renderers']=[];refuse('missing_original_particle_metadata',d,binary)
b=bytearray(binary);v=doc['bufferViews'][doc['accessors'][doc['meshes'][0]['primitives'][0]['indices']]['bufferView']];b[v.get('byteOffset',0)]^=1;refuse('target_triangle_bytes',doc,bytes(b))
b=bytearray(binary);v=doc['bufferViews'][doc['materials'][0]['extensions']['OPENRPG_source_graph']['forward']['buffer_view']];b[v.get('byteOffset',0)]^=1;refuse('forward_program_bytes',doc,bytes(b))
result=dict(status='static_firepit_hierarchy_and_embedded_payload_independently_reopened',**counts,negative_controls=negative,glb_sha256=row['sha256'],assembly_receipt_sha256=sha((ROOT/'RESULT.v1.json').read_bytes()),verifier_sha256=sha(Path(__file__).read_bytes()),gpu_rendered=False,studio_verified=False,lod_runtime_qualified=False,particle_runtime_qualified=False,full_import=False,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
(ROOT/'INDEPENDENT-REOPEN.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(result))
