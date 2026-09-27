"""Derive reusable local frames; retain exact source buffers for reference rendering.

Usage: prepare-building-frames.py BUILDING_MATERIALS NEW_OUTPUT
The source builder uses x/z translation and yaw, with world-space y. This
preparation is not a governed-kit registration or a Studio qualification.
"""
import hashlib
import json
import math
from pathlib import Path
import sys

import numpy as np


def main():
    root, output = map(Path, sys.argv[1:])
    source_bytes = (root / 'buildings.json').read_bytes()
    source = json.loads(source_bytes)
    assert source['schema'] == 'forge.tidewater.building-materials'
    assert source['version'] == 1 and len(source['buildings']) == 23
    assert not output.exists()
    output.mkdir()

    def read(desc):
        path = (root / desc['file']).resolve()
        assert path.is_relative_to(root.resolve())
        raw = path.read_bytes()
        assert len(raw) == desc['bytes']
        assert hashlib.sha256(raw).hexdigest() == desc['sha256']
        return raw

    def write(raw):
        digest = hashlib.sha256(raw).hexdigest()
        path = output / (digest + '.bin')
        if not path.exists():
            path.write_bytes(raw)
        return {'file': path.name, 'bytes': len(raw), 'sha256': digest}

    buildings = []
    max_error = 0.0
    count = 0
    for building in source['buildings']:
        spec = building['spec']
        yaw = spec.get('yaw', 0)
        c, s = math.cos(yaw), math.sin(yaw)
        rotation = np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
        # Preserve the source construction frame. Floor anchors are separate.
        origin = np.array([spec['x'], 0, spec['z']], dtype=np.float64)
        assert np.isfinite(origin).all() and math.isfinite(yaw)
        parts = []
        bounds = []
        for part in building['parts']:
            attrs = {k: read(v) for k, v in part['attributes'].items()}
            local_attrs = {k: write(v) for k, v in attrs.items()}
            positions = np.frombuffer(attrs['pos'], '<f4').reshape(-1, 3)
            normals = np.frombuffer(attrs['nrm'], '<f4').reshape(-1, 3)
            assert len(positions) == part['vertices']
            local = ((positions.astype(np.float64) - origin) @ rotation).astype('<f4')
            local_normals = (normals.astype(np.float64) @ rotation).astype('<f4')
            restored = local.astype(np.float64) @ rotation.T + origin
            error = float(np.max(np.abs(restored - positions)))
            max_error = max(max_error, error)
            assert error < 0.0001, 'Local conversion exceeds 0.1 mm'
            assert np.isfinite(local).all() and np.isfinite(local_normals).all()
            local_attrs['pos'] = write(local.tobytes())
            local_attrs['nrm'] = write(local_normals.tobytes())
            indices = read(part['indices'])
            assert np.frombuffer(indices, '<u4').max() < len(local)
            parts.append({k: part[k] for k in ['source_material', 'material', 'target', 'vertices']} | {
                'attributes': local_attrs, 'indices': write(indices),
                'reference_position': write(attrs['pos']),
                'reference_normal': write(attrs['nrm']),
                'roundtrip_position_max_m': error,
            })
            bounds.append(local)
            count += len(local)
        points = np.concatenate(bounds)
        buildings.append({
            'source_id': building['source_id'], 'kind': building['kind'],
            'source_frame': {'translation_m': origin.tolist(),
                             'rotation_xyzw': [0, math.sin(yaw / 2), 0, math.cos(yaw / 2)],
                             'scale': [1, 1, 1]},
            'pivot_basis': 'source construction frame; y remains source world height',
            'floor_anchor_local_m': ([0, building['result']['floorY'], 0]
                                     if 'floorY' in building['result'] else None),
            'local_bounds': {'min': points.min(axis=0).tolist(), 'max': points.max(axis=0).tolist()},
            'parts': parts,
        })
    result = {'schema': 'forge.tidewater.building-frames', 'version': 1,
              'source_commit': source['source_commit'],
              'source_descriptor_sha256': hashlib.sha256(source_bytes).hexdigest(),
              'buildings': buildings, 'vertices': count,
              'roundtrip_position_max_m': max_error,
              'limits': ['local f32 transform is not bit-exact; reference attributes preserved',
                         'shader materials require native adapter',
                         'not a catalog registration or Studio placement proof']}
    (output / 'buildings.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps({'buildings': len(buildings), 'vertices': count,
                      'roundtrip_position_max_m': max_error}))


if __name__ == '__main__':
    main()
