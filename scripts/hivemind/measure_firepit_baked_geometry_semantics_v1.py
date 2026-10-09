"""Measure native BakeMesh semantics without guessing a runtime mapping."""
from pathlib import Path
import datetime
import hashlib
import json
import math

W = Path('F:/OpenRPG_ImportWork')
SOURCE = W / 'unity-delta-firepit-particle-geometry-v1'
ROOT = W / 'delta-firepit-baked-geometry-semantics-v1'

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def read(p):
    return json.loads(p.read_text('utf-8-sig'))

def main():
    assert not ROOT.exists()
    upstream = W / 'delta-firepit-geometry-buffers-v2/RESULT.v1.json'
    proof = read(upstream)
    assert proof['observations_verified'] == 336
    pins = dict(proof['input_pins'])
    for p in (upstream, Path(__file__).resolve()):
        pins[str(p)] = sha(p)
    for p, expected in pins.items():
        assert sha(Path(p)) == expected, 'changed input: ' + p
    report = read(SOURCE / 'GEOMETRY.v1.json')
    results = []
    color_differences = []
    for case in report['cases']:
        for row in case['rows']:
            matrix = row['local_to_world']
            scales = [math.sqrt(sum(matrix[f'e{i}{j}'] ** 2 for i in range(3))) for j in range(3)]
            for g in row['geometry']:
                assert g['vertices'] == row['count'] * 4, 'quad count differs'
                assert len(g['indices']) == row['count'] * 6, 'triangle count differs'
                center_error = side_error = 0.0
                differences = 0
                for index, particle in enumerate(row['particles']):
                    quad = g['positions'][index * 4:index * 4 + 4]
                    center = [sum(p[k] for p in quad) / 4 for k in 'xyz']
                    center_error = max(center_error, *(abs(center[i] - particle['position'][k]) for i, k in enumerate('xyz')))
                    sides = [math.dist([quad[a][k] for k in 'xyz'], [quad[b][k] for k in 'xyz']) for a, b in [(0, 1), (1, 2)]]
                    expected = [particle['current_size']['y'] * scales[1], particle['current_size']['x'] * scales[0]]
                    side_error = max(side_error, *(abs(a - b) for a, b in zip(sides, expected)))
                    colors = g['colors'][index * 4:index * 4 + 4]
                    assert all(c == colors[0] for c in colors), 'color differs within native quad'
                    if any(abs(colors[0][k] / 255 - particle['current_color'][k]) > 1 / 255 for k in 'rgba'):
                        differences += 1
                        if len(color_differences) < 4:
                            color_differences.append(dict(node_path=case['node_path'], seed=row['seed'],
                                requested_time=row['requested_time'], fixture=g['fixture'], particle_index=index,
                                get_current_color=particle['current_color'], baked_color32=colors[0]))
                results.append(dict(node_path=case['node_path'], seed=row['seed'],
                    requested_time=row['requested_time'], repetition=row['repetition'], fixture=g['fixture'],
                    particles=row['count'], maximum_center_vs_particle_position_error=center_error,
                    maximum_side_vs_current_size_times_transform_scale_error=side_error,
                    current_color_vs_baked_color_differences=differences))
    for p, expected in pins.items():
        assert sha(Path(p)) == expected, 'changed input after measurement: ' + p
    ROOT.mkdir()
    result = dict(status='native_billboard_center_size_and_color_semantics_measured',
        observations=results, observations_measured=len(results), input_pins=pins,
        maximum_center_error=max(r['maximum_center_vs_particle_position_error'] for r in results),
        maximum_side_error=max(r['maximum_side_vs_current_size_times_transform_scale_error'] for r in results),
        color_difference_observations=sum(r['current_color_vs_baked_color_differences'] for r in results),
        color_difference_examples=color_differences, color_difference_cause_qualified=False,
        bake_coordinates_are_not_assumed_to_be_final_world_positions=True,
        coordinate_mapping_to_forge_qualified=False, gpu_rendered=False, full_import=False,
        finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k: result[k] for k in ['status', 'observations_measured', 'maximum_center_error',
        'maximum_side_error', 'color_difference_observations', 'full_import']}))

if __name__ == '__main__':
    main()
