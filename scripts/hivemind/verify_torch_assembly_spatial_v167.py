"""Convert LOD anchors and verify composed transforms and real vertex bounds."""
from pathlib import Path
from datetime import datetime, timezone
import copy
import hashlib
import json
import math
import struct
import verify_medieval_torch_hierarchy_v139 as hierarchy
from assemble_torch_source_hierarchy_v166 import flip, same

W = Path('F:/OpenRPG_ImportWork')
O = W / 'medieval-torch-spatial-assembly-v167'
pins = {}

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def read(path):
    pins[str(path)] = sha(path)
    return json.loads(Path(path).read_text('utf-8-sig'))

def main():
    assert not O.exists()
    proof = read(W / 'medieval-torch-source-assembly-v166/RESULT.v1.json')
    for path, digest in proof['output_pins'].items():
        assert sha(path) == digest
    source = read(W / 'medieval-torch-source-assembly-v166/ASSEMBLY.v1.json')
    document = copy.deepcopy(source)
    world = {}
    errors = []
    bounds = []
    for node in document['nodes']:
        local = hierarchy.matrix(dict(m_LocalPosition=node['position'], m_LocalRotation=node['rotation'], m_LocalScale=node['scale']))
        parent = world[node['parent']] if node['parent'] else [[float(i == j) for j in range(4)] for i in range(4)]
        composed = hierarchy.multiply(parent, local)
        expected = node['world_matrix']
        error = max(abs(composed[i][j] - expected[f'e{i}{j}']) for i in range(4) for j in range(4))
        assert all(abs(composed[i][j] - expected[f'e{i}{j}']) <= 1e-7 + 4*2**-23*abs(expected[f'e{i}{j}']) for i in range(4) for j in range(4)), node['path']
        world[node['path']] = composed
        errors.append(dict(path=node['path'], max_composition_error=error))
        for role in ['render_mesh', 'collider_mesh']:
            mesh = node[role]
            if mesh is None: continue
            position = next(a for a in mesh['attributes'] if a['semantic'] == 'Position')
            stream = next(s for s in mesh['streams'] if s['index'] == position['stream'])
            path = Path(stream['path']); raw = path.read_bytes(); pins[str(path)] = sha(path)
            assert pins[str(path)] == read(W / 'medieval-torch-converted-geometry-v152/RESULT.v1.json')['output_pins'][str(path)]
            points = []
            for index in range(mesh['vertex_count']):
                v = list(struct.unpack_from('<fff', raw, index*stream['stride'] + position['offset'])) + [1.]
                points.append([sum(expected[f'e{i}{j}']*v[j] for j in range(4)) for i in range(3)])
            assert all(math.isfinite(v) for point in points for v in point)
            row = dict(path=node['path'], role=role, vertices_checked=len(points), world_min=[min(p[i] for p in points) for i in range(3)], world_max=[max(p[i] for p in points) for i in range(3)])
            node[role+'_world_bounds'] = row
            bounds.append(row)
    document['source_lod_groups'] = copy.deepcopy(source['lod_groups'])
    for group in document['lod_groups']:
        group['reference_point']['z'] = flip(group['reference_point']['z'])
        group['runtime_lod_selection_verified'] = False
    def verify_lod(value):
        assert len(value['lod_groups']) == len(source['lod_groups']) == 1
        for target, original in zip(value['lod_groups'], source['lod_groups']):
            assert same(target['reference_point']['z'], flip(original['reference_point']['z']))
            for axis in 'xy': assert same(target['reference_point'][axis], original['reference_point'][axis])
            assert same(target['size'], original['size']) and target['lods'] == original['lods']
    verify_lod(document)
    bad = copy.deepcopy(document);bad['lod_groups'][0]['reference_point']['z'] += .01
    try: verify_lod(bad)
    except AssertionError: pass
    else: raise AssertionError('Changed LOD anchor admitted')
    for path in [Path(__file__).resolve(), Path(hierarchy.__file__).resolve()]: pins[str(path)] = sha(path)
    for path, digest in pins.items(): assert sha(path) == digest, path
    O.mkdir(); output = O / 'ASSEMBLY.v1.json'
    output.write_text(json.dumps(document, indent=2, allow_nan=False), 'utf-8')
    verify_lod(json.loads(output.read_text('utf-8')))
    result = dict(status='converted_hierarchy_composition_lod_anchor_and_world_bounds_verified', finished_utc=datetime.now(timezone.utc).isoformat(), nodes=len(errors), composed_transform_checks=errors, mesh_bounds=bounds, vertices_checked=sum(b['vertices_checked'] for b in bounds), lod_groups_converted=1, altered_lod_anchor_refused=True, input_pins=pins, output_pins={str(output):sha(output)}, collider_physics_verified=False, runtime_lod_selection_verified=False, renderer_admitted=False, full_import=False)
    (O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps(dict(nodes=len(errors), mesh_bounds=len(bounds), vertices_checked=result['vertices_checked'], lod_groups_converted=1, full_import=False)))

if __name__ == '__main__': main()
