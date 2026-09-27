"""Verify independent crest snapshots and exact repeated geometry acquisition."""
from pathlib import Path
import hashlib
import json
import sys
import numpy as np

source, repeat, ocean, output = map(Path, sys.argv[1:5])
assert len(sys.argv) == 5 and not output.exists()
meta = json.loads((source / 'breakers.json').read_text())
assert meta == json.loads((repeat / 'breakers.json').read_text())
assert meta['schema'] == 'forge.tidewater.breakers' and meta['version'] == 1
assert meta['source_commit'] == '1438b1abfcaee3267092b75573014f4d9b4a983c'
assert meta['options'] == dict(spray=False, clouds=False)
assert meta['terrain_sha256'] == 'be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3'
for name, sha in meta['files'].items():
    assert Path(name).name == name and '/' not in name and '\\' not in name
    for root in [source, repeat]:
        assert hashlib.sha256((root / name).read_bytes()).hexdigest() == sha
count = meta['stations']
assert 1 < count <= 4096 and meta['spacing'] == .6 and meta['slots'] == 2 and meta['profile_vertices'] == 20
stations = np.frombuffer((source / 'stations.f32.bin').read_bytes(), dtype='<f4').reshape(count, 4)
assert np.isfinite(stations).all() and np.max(np.abs(np.linalg.norm(stations[:, 2:], axis=1) - 1)) < 1e-6
assert np.all(np.diff(stations[:, 0]) > 0)
ids = np.frombuffer((source / 'sheet-ids.f32.bin').read_bytes(), dtype='<f4').reshape(-1, 4)
indices = np.frombuffer((source / 'indices.u32.bin').read_bytes(), dtype='<u4')
assert ids.shape == ((count - 1) * 2 * 40, 4) and indices.size == (count - 1) * 2 * 19 * 6
assert indices.max() < ids.shape[0] and np.isfinite(ids).all()
assert [s['frame'] for s in meta['snapshots']] == [1, 120, 540]
snapshots = []
for snap in meta['snapshots']:
    prefix = snap['prefix']
    frame = np.frombuffer((source / (prefix + '-frame.bin')).read_bytes(), dtype='<f4')
    assert frame.size == 180 and frame[145] == np.float32(snap['frame'] / 60)
    crests = np.frombuffer((source / (prefix + '-crests.f32.bin')).read_bytes(), dtype='<f4').reshape(count, 2, 3, 4)
    assert np.isfinite(crests).all()
    wave_id, progress = crests[:, :, 2, 3], crests[:, :, 0, 3]
    active = wave_id > .5
    assert active.any() and np.all(wave_id[active] <= 1024)
    valid = (wave_id[:-1] > .5) & (np.abs(wave_id[:-1] - wave_id[1:]) < .5) & (progress[:-1] < 1.25) & (progress[1:] < 1.25)
    assert valid.any()
    for mip in range(9):
        raw = (source / f'{prefix}-displacement-m{mip}.rgba16.bin').read_bytes()
        vals = np.frombuffer(raw, dtype='<f2')
        assert vals.size == (256 >> mip) ** 2 * 4 * 4 and np.isfinite(vals).all()
        if snap['frame'] == 120:
            expected = b''.join((ocean / f'frame-120-displacement-c{layer}-m{mip}.rgba16.bin').read_bytes() for layer in range(4))
            assert raw == expected, 'Crest input differs from the original FFT oracle'
    snapshots.append(dict(frame=snap['frame'], active_slots=int(active.sum()), drawable_segments=int(valid.sum())))
report = dict(files_exact=len(meta['files']), stations=count, vertices=len(ids), indices=int(indices.size),
              snapshots=snapshots, frame120_fft_matches_previous_oracle=True,
              limits=['source acquisition and derived geometry only', 'spray and clouds disabled', 'no native crest compute or visual verdict'])
output.write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
