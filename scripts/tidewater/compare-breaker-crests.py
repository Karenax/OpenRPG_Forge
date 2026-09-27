"""Compare meaningful crest slots; inactive slot payloads are intentionally stale upstream."""
from pathlib import Path
import hashlib
import json
import sys
import numpy as np

source, native, output = map(Path, sys.argv[1:4])
assert len(sys.argv) == 4 and not output.exists()
meta = json.loads((source / 'breakers.json').read_text())
assert meta['schema'] == 'forge.tidewater.breakers' and meta['source_commit'] == '1438b1abfcaee3267092b75573014f4d9b4a983c'
count = meta['stations']
probe = json.loads((native / 'probe.json').read_text())
assert probe['schema'] == 'forge.tidewater.native-crest-probe' and probe['version'] == 1
assert probe['mode'] in ['isolated_source_fft_inputs', 'shared_native_fft_frame120'] and probe['source_commit'] == meta['source_commit']
shared = probe['mode'] == 'shared_native_fft_frame120'
assert probe['source_manifest_sha256'] == hashlib.sha256((source / 'breakers.json').read_bytes()).hexdigest()
assert probe['native_stations_sha256'] == meta['files']['stations.f32.bin']
assert probe['snapshots'] == ([120] if shared else [1, 120, 540]) and probe['source_crest_results_uploaded'] is False
if shared: assert probe['source_fft_textures_uploaded'] is False
reports = []
def drawable(c):
    wave, progress = c[:, :, 2, 3], c[:, :, 0, 3]
    return (wave[:-1] > .5) & (np.abs(wave[:-1] - wave[1:]) < .5) & (progress[:-1] < 1.25) & (progress[1:] < 1.25)
def stats(a, b):
    d = np.abs(a.astype('float64') - b.astype('float64'))
    return dict(mean=float(d.mean()), maximum=float(d.max()), p99=float(np.quantile(d, .99)))
for snap in meta['snapshots']:
    if snap['frame'] not in probe['snapshots']: continue
    name = snap['prefix'] + '-crests.f32.bin'
    raw_a, raw_b = (source / name).read_bytes(), (native / name).read_bytes()
    assert hashlib.sha256(raw_a).hexdigest() == meta['files'][name]
    a = np.frombuffer(raw_a, dtype='<f4').reshape(count, 2, 3, 4)
    b = np.frombuffer(raw_b, dtype='<f4').reshape(count, 2, 3, 4)
    assert np.isfinite(a).all() and np.isfinite(b).all()
    active_a, active_b = a[:, :, 2, 3] > .5, b[:, :, 2, 3] > .5
    paired = active_a & active_b & (a[:, :, 2, 3] == b[:, :, 2, 3])
    assert paired.any(), 'No corresponding native crests'
    x, y = a[paired], b[paired]
    reports.append(dict(frame=snap['frame'], source_active=int(active_a.sum()), native_active=int(active_b.sum()),
        active_mask_mismatches=int((active_a != active_b).sum()),
        active_wave_id_mismatches=int((a[:, :, 2, 3][active_a | active_b] != b[:, :, 2, 3][active_a | active_b]).sum()),
        drawable_segment_mismatches=int((drawable(a) != drawable(b)).sum()),
        root_metres=stats(x[:, 0, :3], y[:, 0, :3]), back_metres=stats(x[:, 1, :3], y[:, 1, :3]),
        height_metres=stats(x[:, 1, 3], y[:, 1, 3]), trough_metres=stats(x[:, 2, 2], y[:, 2, 2]),
        direction=stats(x[:, 2, :2], y[:, 2, :2]), progress=stats(x[:, 0, 3], y[:, 0, 3]),
        native_sha256=hashlib.sha256(raw_b).hexdigest()))
report = dict(probe=probe, snapshots=reports, limits=[('shared native FFT and crest compute at frame120' if shared else 'native isolated crest compute; paired original FFT inputs'),
    'inactive slots ignored beyond zero id because source retains stale payloads',
    'errors reported, no automatic visual parity verdict', 'no spray or clouds'])
output.write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
