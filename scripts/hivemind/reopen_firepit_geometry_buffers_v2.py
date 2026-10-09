"""Reopen completed native geometry; retain refusal for malformed GPU inputs."""
from pathlib import Path
import copy
import datetime
import hashlib
import json
import math

W = Path('F:/OpenRPG_ImportWork')
SOURCE = W / 'unity-delta-firepit-particle-geometry-v1'
ROOT = W / 'delta-firepit-geometry-buffers-v2'

def read(p):
    return json.loads(p.read_text('utf-8-sig'))

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def matrix(value):
    assert set(value) == {f'e{i}{j}' for i in range(4) for j in range(4)}, 'matrix schema'
    assert all(type(v) in (int, float) and math.isfinite(v) for v in value.values()), 'matrix finite'
    return [[value[f'e{i}{j}'] for j in range(4)] for i in range(4)]

def vectors(values, fields, count):
    assert len(values) in (0, count), 'attribute cardinality'
    for value in values:
        assert set(value) == set(fields), 'vector schema'
        assert all(type(v) in (int, float) and math.isfinite(v) for v in value.values()), 'vector finite'

def validate(g, row):
    count = g['vertices']
    assert type(count) is int and count >= 0, 'vertex count'
    assert len(g['positions']) == count, 'position count'
    assert type(g['submeshes']) is int and g['submeshes'] >= 0, 'submesh count'
    for name, fields in [('positions', 'xyz'), ('normals', 'xyz'),
                         ('tangents', 'xyzw'), ('uv0', 'xyzw'), ('uv1', 'xyzw')]:
        vectors(g[name], fields, count)
    assert len(g['colors']) in (0, count), 'color count'
    for color in g['colors']:
        assert set(color) == set('rgba'), 'color schema'
        assert all(type(v) is int and 0 <= v <= 255 for v in color.values()), 'color byte'
    indices = g['indices']
    assert len(indices) % 3 == 0, 'triangle count'
    assert all(type(i) is int and 0 <= i < count for i in indices), 'index bounds'
    if row['count']:
        assert count > 0 and indices and g['submeshes'] > 0, 'missing live geometry'
        assert len(g['uv0']) == len(g['colors']) == count, 'missing live attributes'
    camera = matrix(g['camera_local_to_world'])
    inverse = matrix(g['world_to_camera'])
    matrix(g['projection'])
    matrix(g['particle_local_to_world'])
    assert g['particle_local_to_world'] == row['local_to_world'], 'particle matrix mismatch'
    # Unity camera space flips Z relative to Transform local space.
    for i in range(4):
        for j in range(4):
            actual = sum(inverse[i][k] * camera[k][j] for k in range(4))
            expected = (-1 if i == 2 else 1) if i == j else 0
            assert abs(actual - expected) <= 2e-6, 'camera inverse mismatch'
    for key in ('fov', 'aspect', 'near', 'far', 'orthographic_size'):
        assert type(g[key]) in (float, int) and math.isfinite(g[key]), 'camera finite'
    assert 0 < g['near'] < g['far'] and g['aspect'] > 0, 'camera range'

def main():
    assert not ROOT.exists(), 'retain prior evidence'
    status = read(SOURCE / 'RUN-RESULT.v1.json')
    assert status['native_geometry_verified'] and status['unity_exit_code'] == 0, 'native job not qualified'
    receipt = SOURCE / 'INDEPENDENT-REOPEN.v1.json'
    assert sha(receipt) == status['independent_receipt_sha256']
    proof = read(receipt)
    capture = SOURCE / 'GEOMETRY.v1.json'
    assert sha(capture) == proof['capture_sha256']
    pins = dict(proof['input_pins'])
    for p in (capture, receipt, SOURCE / 'RUN-RESULT.v1.json', Path(__file__).resolve()):
        pins[str(p)] = sha(p)
    for p, expected in pins.items():
        assert sha(Path(p)) == expected, 'changed pinned input: ' + p
    report = read(capture)
    observations = vertices = triangles = 0
    sample = None
    for case in report['cases']:
        for row in case['rows']:
            for g in row['geometry']:
                validate(g, row)
                observations += 1
                vertices += g['vertices']
                triangles += len(g['indices']) // 3
                if row['count'] and sample is None:
                    sample = (g, row)
    assert observations == 336 and sample
    negatives = []
    mutations = [
        ('nonfinite_camera_matrix', lambda g: g['projection'].__setitem__('e00', float('nan'))),
        ('mismatched_particle_matrix', lambda g: g['particle_local_to_world'].__setitem__('e03', g['particle_local_to_world']['e03'] + 1)),
        ('out_of_bounds_index', lambda g: g['indices'].__setitem__(0, g['vertices'])),
        ('missing_uv', lambda g: g.__setitem__('uv0', [])),
        ('nonfinite_position', lambda g: g['positions'][0].__setitem__('x', float('inf'))),
        ('nonbyte_color', lambda g: g['colors'][0].__setitem__('a', 256)),
    ]
    for name, mutate in mutations:
        altered = copy.deepcopy(sample[0])
        mutate(altered)
        try:
            validate(altered, sample[1])
        except AssertionError as error:
            negatives.append(dict(name=name, refused=True, reason=str(error)))
        else:
            raise AssertionError('corrupt input admitted: ' + name)
    for p, expected in pins.items():
        assert sha(Path(p)) == expected, 'changed input after reopen: ' + p
    ROOT.mkdir()
    result = dict(status='336_native_geometry_buffers_and_camera_matrices_reopened',
        observations_verified=observations, vertex_observations=vertices,
        triangle_observations=triangles, negative_controls=negatives, input_pins=pins,
        camera_inverse_absolute_tolerance=2e-6, particle_matrices_exact=True,
        coordinate_mapping_to_forge_qualified=False, gpu_rendered=False,
        runtime_converted=False, studio_verified=False, full_import=False,
        finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k: result[k] for k in ('status', 'observations_verified', 'vertex_observations', 'triangle_observations', 'full_import')}))

if __name__ == '__main__':
    main()
