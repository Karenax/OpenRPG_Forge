"""Compare the first native flag's exact identities/LOD slots against preserved YAML."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,math
W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-native-binding-comparison-v391'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def compare_vector(a,b):
    assert set(a)==set(b)
    error=max(abs(a[k]-b[k]) for k in a)
    assert all(math.isfinite(a[k]) and math.isfinite(b[k]) and abs(a[k]-b[k])<=1e-5+1e-6*abs(b[k]) for k in a),(a,b)
    return error
assert not O.exists()
bp=W/'gladiator-source-binding-bridge-v390/RESULT.v1.json';bridge=read(bp)
gp=W/'gladiator-native-geometry-reopen-v381/RESULT.v1.json';geometry=read(gp)
np=W/'gladiator-native-geometry-v380/NATIVE-RESULT.v1.json';native=read(np)
assert geometry['status']=='four_native_lod_mesh_buffers_reopened' and sha(np)==geometry['native_receipt_sha256']
pins={str(p):sha(p) for p in [bp,gp,np,Path(__file__)]}
for p,h in bridge['input_pins'].items():assert sha(p)==h;pins[p]=h
source=next(r for r in bridge['rows'] if r['prefab_guid']==native['prefab_guid'])
source_nodes={n['path']:n for n in source['nodes']};native_bindings={b['path']:b for b in native['bindings']}
assert len(native_bindings)==len(native['bindings'])==source['renderer_count']==4
source_renderers={n['renderer']['source_renderer_file_id']:n for n in source['nodes'] if n['renderer']}
rows=[]
for path,b in native_bindings.items():
    n=source_nodes[path];r=n['renderer'];assert r and b['role']=='mesh_filter'
    assert (b['mesh']['guid'],b['mesh']['file_id'])==(r['mesh_guid'],r['mesh_file_id'])
    assert len(b['materials'])==len(r['material_slots'])
    for native_material,source_material in zip(b['materials'],r['material_slots']):
        assert native_material['type']=='UnityEngine.Material'
        assert (native_material['guid'],native_material['file_id'])==(source_material['guid'],str(source_material['fileID']))
    errors={field:compare_vector(b[field],n[field]) for field in ['local_position','local_rotation','local_scale']}
    rows.append(dict(path=path,source_renderer_file_id=r['source_renderer_file_id'],mesh=b['mesh'],material_slots=b['materials'],local_transform_max_errors=errors))
assert len(native['lod_groups'])==len(source['lod_groups'])==1
ng=native['lod_groups'][0];sg=source['lod_groups'][0]
assert ng['path']=='root' and len(ng['lods'])==len(sg['levels'])==4
compare_vector(ng['reference_point'],sg['reference_point']);assert abs(ng['size']-sg['size'])<=1e-5+1e-6*abs(sg['size'])
lods=[]
for level,(nl,sl) in enumerate(zip(ng['lods'],sg['levels'])):
    assert sl['explicit_empty_renderer_slots']==0
    assert len(nl['renderers'])==len(sl['renderers'])==1
    assert abs(nl['screen_relative_height']-sl['screen_relative_height'])<=1e-6 and abs(nl['fade_width']-sl['fade_width'])<=1e-6
    for nr,sr in zip(nl['renderers'],sl['renderers']):
        assert nr['type']=='UnityEngine.MeshRenderer'
        assert (nr['guid'],nr['file_id'])==(source['prefab_guid'],sr['source_renderer_file_id'])
        node=source_renderers[nr['file_id']];assert node['path'] in native_bindings
    lods.append(dict(level=level,source_renderer_file_id=sl['renderers'][0]['source_renderer_file_id'],mesh=native_bindings[node['path']]['mesh'],screen_relative_height=nl['screen_relative_height']))
for p,h in pins.items():assert sha(p)==h,p
result=dict(status='four_native_flag_lods_exact_mesh_renderer_and_material_identities_matched',source_prefab=source['source_path'],source_prefab_sha256=source['source_sha256'],bindings=rows,lods=lods,input_pins=pins,source_root_transform=source_nodes['root']['source_local_matrix_row_major'],native_root_transform_direct_capture_pending=True,material_gpu_qualified=False,converted=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(dict(status=result['status'],bindings=len(rows),lods=len(lods))))
