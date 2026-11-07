"""Bind exact House hierarchy TRS, renderers and colliders to proven mesh buffers."""
from pathlib import Path
import datetime
import hashlib
import json
import math

W = Path('F:/OpenRPG_ImportWork')
O = W / 'house-scene-geometry-package-v667'
assert not O.exists()
def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))
def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()
def multiply(a,b):
    return [[sum(a[r][k]*b[k][c] for k in range(4)) for c in range(4)] for r in range(4)]
def basis(a):
    signs = [1,1,-1,1]
    return [[a[r][c]*signs[r]*signs[c] for c in range(4)] for r in range(4)]
def column_major(a):
    return [a[r][c] for c in range(4) for r in range(4)]
def trs(node):
    p,s,q = node['local_position'],node['local_scale'],node['local_rotation']
    x,y,z,w = [q[k] for k in 'xyzw']
    assert all(math.isfinite(v) for v in [*p.values(),*s.values(),x,y,z,w])
    assert abs(x*x+y*y+z*z+w*w-1) < 1e-5
    assert all(s[k] != 0 for k in 'xyz')
    r = [[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)], [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)], [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]]
    return [[r[i][j]*s['xyz'[j]] for j in range(3)] + [p['xyz'[i]]] for i in range(3)] + [[0,0,0,1]]

packet_path = W / 'house-accessor-buffers-v666/RESULT.v1.json'
queue_path = W / 'queue/house-on-a-hill/prefabs.json'
lit_path = W / 'house-native-lit-checkpoint-v663/RESULT.v1.json'
packet,queue,lit = [read(p) for p in [packet_path,queue_path,lit_path]]
pins = {str(p):sha(p) for p in [packet_path,queue_path,lit_path,Path(__file__).resolve()]}
for proof in [packet,lit]:
    for field in ['input_pins','output_pins']:
        for path,digest in proof.get(field,{}).items():
            assert sha(path) == digest,path
            pins[path] = digest
materials = {r['material_guid'] for r in lit['rows']}
rows = []
for case in packet['cases']:
    source = case['preserved_native_case']
    originals = {n['path']:n for n in source['nodes']}
    assert len(originals) == len(source['nodes']) and 'root' in originals
    mesh_map = {(m['identity']['guid'],m['identity']['file_id']):m for m in case['meshes']}
    local,world,target_world = {},{},{}
    visiting = set()
    def visit(path):
        if path in world:
            return
        assert path not in visiting
        visiting.add(path)
        node = originals[path]
        local[path] = trs(node)
        if node['parent']:
            assert node['parent'] in originals
            visit(node['parent'])
            world[path] = multiply(world[node['parent']],local[path])
            target_world[path] = multiply(target_world[node['parent']],basis(local[path]))
        else:
            assert path == 'root'
            world[path] = local[path]
            target_world[path] = basis(local[path])
        expected = basis(world[path])
        assert max(abs(target_world[path][r][c]-expected[r][c]) for r in range(4) for c in range(4)) <= 1e-12
        visiting.remove(path)
    nodes = []
    for path,node in originals.items():
        visit(path)
        bindings = [b for b in source['bindings'] if b['path']==path]
        entry = dict(source_node=node, target_local_matrix_column_major=column_major(basis(local[path])), target_world_matrix_column_major=column_major(target_world[path]), source_world_matrix_row_major=[v for r in world[path] for v in r], original_bindings=bindings, collider_geometry=[])
        for binding in bindings:
            for name in ['local_position','local_rotation','local_scale']:
                assert binding[name] == node[name]
            mesh = mesh_map[(binding['mesh']['guid'],binding['mesh']['file_id'])]
            g = dict(identity=mesh['identity'], attributes=mesh['attributes'], original_attributes=mesh['original_attributes'], **{k:mesh['converted_geometry'][k] for k in ['primitives','bounds_min','bounds_max','vertex_count']}, materials=binding['materials'])
            if binding['role']=='mesh_filter':
                assert 'geometry' not in entry
                assert len(g['primitives'])==len(g['materials']) and all(m['guid'] in materials for m in g['materials'])
                entry['geometry']=g
            else:
                assert binding['role']=='mesh_collider'
                entry['collider_geometry'].append(g)
        nodes.append(entry)
    prefab = Path(queue['source_root']) / queue['guid_paths'][case['prefab_guid']]
    pins[str(prefab)] = sha(prefab)
    rows.append(dict(prefab_guid=case['prefab_guid'],cards=[case['card_id']],source_prefab=str(prefab),source_prefab_sha256=sha(prefab),nodes=nodes,original_lod_groups=source['lod_groups'],unassigned_mesh_colliders=source['unassigned_mesh_colliders'],component_snapshots=source['component_snapshots'],source_trs_preserved=True,native_world_float32_capture_pending=True,runtime_renderer_and_collider_admission_pending=True))
assert len(rows)==24
assert sum('geometry' in n for r in rows for n in r['nodes'])==24
assert sum(len(n['collider_geometry']) for r in rows for n in r['nodes'])==19
assert all(sha(p)==h for p,h in pins.items())
O.mkdir()
result=dict(schema='hivemind.house-scene-geometry-package.v667',utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),prefabs=24,meshes=42,render_bindings=24,collider_bindings=19,rows=rows,input_pins=pins,glb_created=False,renderer_installed=False,full_import=False)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2,allow_nan=False),'utf-8')
print(json.dumps(dict(prefabs=24,render_bindings=24,collider_bindings=19,receipt=str(O/'RESULT.v1.json'))))
