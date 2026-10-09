"""Independently audit source FishProps models, GPU records and repeatability."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path

import numpy as np

COMMIT = '1438b1abfcaee3267092b75573014f4d9b4a983c'
SCENE = 'e2f4799bb6f425afb4b9b800fb547528a037da15ad95f8c9d2182889c1e805f1'
KINDS = {'trunk': 1, 'head': 1, 'whole': 28, 'ice': 2, 'leaf': 6, 'lobster': 2, 'split': 38}


def digest(data):
    return hashlib.sha256(data).hexdigest()


def verify(source, first, second):
    assert first.resolve() != second.resolve(), 'Two distinct acquisitions required'
    names = {p.name for p in first.iterdir() if p.is_file()}
    assert len(names) == 227 and names == {p.name for p in second.iterdir() if p.is_file()}
    assert all((first/name).read_bytes() == (second/name).read_bytes() for name in names)
    raw = (first/'fish-props.json').read_bytes()
    data = json.loads(raw)
    assert data['schema'] == 'forge.tidewater.fish-prop-source' and data['version'] == 1
    assert data['source_commit'] == COMMIT and data['node_version'] == '22.22.3'
    assert data['source_scene_digest'] == SCENE
    assert data['unchanged_source_scene'] and data['unchanged_fish_geometry_and_records']
    assert data['draw_range_m'] == 90 and data['shadow_range_m'] == 22
    assert len(data['instances']) == 78 and len(data['prototypes']) == 18
    assert dict(Counter(row['source']['kind'] for row in data['instances'])) == KINDS
    referenced = {'fish-props.json'}

    def payload(reference):
        name = reference['file']
        assert Path(name).name == name and name not in ('', '.', '..') and ':' not in name
        content = (first/name).read_bytes()
        assert len(content) == reference['bytes'] and digest(content) == reference['sha256']
        referenced.add(name)
        return content

    for relative, reference in data['provenance'].items():
        path = (source/relative).resolve()
        assert path.is_relative_to(source.resolve())
        assert payload(reference) == path.read_bytes()
    assert payload(data['provenance']['LICENSE']).startswith(b'MIT License')
    keys = [row['key'] for row in data['prototypes']]
    assert len(set(keys)) == 18
    keys_from_instances = list(dict.fromkeys(row['prototype'] for row in data['instances']))
    assert keys == keys_from_instances
    streams = {name: [] for name in ['position', 'normal', 'aData', 'aKind', 'indices']}
    triangle_counts = [0, 0, 0]
    for ordinal, prototype in enumerate(data['prototypes']):
        assert len(prototype['lods']) == 3
        prior = None
        for lod, row in enumerate(prototype['lods']):
            assert row['lod'] == lod and row['shadow'] == (lod == 2) and row['shadow_only'] == (lod == 2)
            assert set(row['attributes']) == {'position', 'normal', 'aData'}
            buffers = {}
            for name, width in [('position', 3), ('normal', 3), ('aData', 4)]:
                reference = row['attributes'][name]
                assert reference['component_type'] == 'Float32Array' and reference['item_size'] == width
                content = payload(reference)
                values = np.frombuffer(content, dtype='<f4')
                assert values.size == row['vertices'] * width and np.isfinite(values).all()
                buffers[name] = content
                streams[name].append(content)
            assert row['indices']['component_type'] == 'Uint16Array'
            content = payload(row['indices'])
            indices = np.frombuffer(content, dtype='<u2')
            assert indices.size == row['triangles'] * 3 and indices.size > 0 and indices.max() < row['vertices']
            buffers['indices'] = content
            streams['indices'].append(indices.astype('<u4').tobytes())
            streams['aKind'].append(np.full(row['vertices'], ordinal*3+lod, dtype='<f4').tobytes())
            triangle_counts[lod] += row['triangles']
            if lod == 2:
                assert buffers == prior, 'Shadow proxy must retain the exact lower-detail source geometry'
            prior = buffers
    merged = b''.join(b''.join(streams[name]) for name in ['position', 'normal', 'aData', 'aKind', 'indices'])
    assert digest(merged) == data['source_fish_geometry_sha256']
    stream = payload(data['instance_stream'])
    assert digest(stream) == data['source_instance_sha256']
    records = np.frombuffer(stream, dtype='<f4').reshape(78, 16)
    assert np.isfinite(records).all()
    for ordinal, row in enumerate(data['instances']):
        assert row['source_id'] == f'tidewater:{COMMIT}:fish-prop:{ordinal}'
        assert row['kind0'] == keys.index(row['prototype']) * 3
        item = row['source']
        assert row['prototype'] == item['kind'] + ':' + (item['species'] or '')
        assert item['L'] > 0 and item['seed'] >= 0
        assert len(item['q']) == 4 and abs(sum(v*v for v in item['q']) - 1) < 1e-9
        expected = np.asarray([item['x'], item['y'], item['z'], item['L'], *item['q'],
                               item['pattern'] + (item['seed'] % 1) * 0.9,
                               item['curl'], item['sag'], item['jaw'], *item['flags']], dtype='<f4')
        assert np.array_equal(expected, records[ordinal])
        assert np.array_equal(np.asarray(row['record_f32'], dtype='<f4'), records[ordinal])
    assert referenced == names
    return {'status': 'passed', 'source_commit': COMMIT, 'source_scene_digest': SCENE,
            'instances': 78, 'prototypes': 18, 'files_identical': len(names),
            'lod_triangles': triangle_counts, 'descriptor_sha256': digest(raw),
            'exact_source_gpu_records': True, 'exact_source_merged_geometry': True,
            'provenance_matches_source': True, 'rust_material_ported': False,
            'studio_executed': False, 'visual_parity': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['source', 'first', 'second', 'report']:
        parser.add_argument(name, type=Path)
    args = parser.parse_args()
    assert not args.report.exists()
    result = verify(args.source, args.first, args.second)
    args.report.write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result))


if __name__ == '__main__':
    main()
