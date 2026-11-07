"""Reopen raw Unity mesh buffers, preserving all LODs and material bindings."""
from pathlib import Path
from datetime import datetime, timezone
import base64, hashlib, json, math, struct
W=Path('F:/OpenRPG_ImportWork'); P=W/'gladiator-native-geometry-v380'; O=W/'gladiator-native-geometry-reopen-v381'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists(), 'Never overwrite proof'
run=read(P/'RUN-RESULT.v1.json'); prep=read(P/'PREPARED.v1.json'); nativepath=P/'NATIVE-RESULT.v1.json'
assert run['status']=='native_flag_geometry_extracted_review_pending',run
assert sha(nativepath)==run['native_receipt_sha256']
for p,h in prep['input_pins'].items():assert sha(Path(p))==h,p
native=read(nativepath); assert native['source_components_unchanged']
meshes=[]; ids=set()
for mesh in native['meshes']:
    key=(mesh['identity']['guid'],mesh['identity']['file_id']);assert key not in ids;ids.add(key)
    n=mesh['vertices'];assert n>0
    streams={s['index']:(s['stride'],base64.b64decode(s['bytes_base64'],validate=True)) for s in mesh['streams']}
    for stride,data in streams.values():assert stride>0 and len(data)==stride*n
    attrs={a['semantic']:a for a in mesh['attributes']};assert len(attrs)==len(mesh['attributes'])
    values={}
    for name,a in attrs.items():
        assert a['format']=='Float32',(name,a['format'])
        stride,data=streams[a['stream']];assert 0<=a['offset'] and a['offset']+4*a['dimension']<=stride
        array=[struct.unpack_from('<'+'f'*a['dimension'],data,i*stride+a['offset']) for i in range(n)]
        assert all(math.isfinite(x) for v in array for x in v);values[name]=array
    assert all(s in values for s in ['Position','Normal','Tangent','TexCoord0'])
    positions=values['Position']; normals=values['Normal']; tangents=values['Tangent']
    assert all(len(v)==3 for v in positions+normals) and all(len(v)==4 for v in tangents)
    assert all(v[3] in [-1.0,1.0] for v in tangents)
    raw=base64.b64decode(mesh['index_bytes_base64'],validate=True)
    fmt={'UInt16':'H','UInt32':'I'}[mesh['index_format']];size=struct.calcsize('<'+fmt)
    assert len(raw)%size==0;indices=[i[0] for i in struct.iter_unpack('<'+fmt,raw)]
    triangles=0
    for sub in mesh['submeshes']:
        assert sub['topology']=='Triangles' and sub['index_count']%3==0
        start=sub['index_start'];end=start+sub['index_count'];assert 0<=start<=end<=len(indices)
        assert all(0<=i+sub['base_vertex']<n for i in indices[start:end]);triangles+=sub['index_count']//3
    normal_error=max(abs(sum(x*x for x in v)-1.0) for v in normals)
    tangent_error=max(abs(sum(x*x for x in v[:3])-1.0) for v in tangents)
    dot_error=max(abs(sum(a*b for a,b in zip(normal,tangent))) for normal,tangent in zip(normals,tangents))
    meshes.append(dict(identity=mesh['identity'],vertices=n,triangles=triangles,submeshes=len(mesh['submeshes']),attributes=list(attrs),measured_bounds=dict(min=[min(v[i] for v in positions) for i in range(3)],max=[max(v[i] for v in positions) for i in range(3)]),normal_squared_length_max_error=normal_error,tangent_squared_length_max_error=tangent_error,normal_tangent_max_dot=dot_error,finite_attributes=True,indices_in_range=True))
assert len(meshes)==4 and len(native['bindings'])==4 and len(native['lod_groups'])==1
for b in native['bindings']:
    assert b['role']=='mesh_filter' and (b['mesh']['guid'],b['mesh']['file_id']) in ids
    assert len(b['materials'])==next(m['submeshes'] for m in meshes if m['identity']==b['mesh'])
    for field in ['local_position','local_scale','local_rotation']:assert all(math.isfinite(x) for x in b[field].values())
group=native['lod_groups'][0];assert len(group['lods'])==4
assert all(len(l['renderers'])==1 for l in group['lods'])
result=dict(status='four_native_lod_mesh_buffers_reopened',asset_id=prep['asset_id'],native_receipt_sha256=sha(nativepath),meshes=meshes,bindings=native['bindings'],lod_groups=native['lod_groups'],source_inputs_unchanged=True,private_importer_changes=run['private_importer_changes'],source_shader_qualified=False,ancestor_transform_and_renderer_identity_bridge_pending=True,converted=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(status=result['status'],meshes=[dict(vertices=m['vertices'],triangles=m['triangles'],submeshes=m['submeshes']) for m in meshes])))
