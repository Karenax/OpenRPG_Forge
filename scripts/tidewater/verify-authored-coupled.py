"""Compare authored native coupled states with the previous optics-only captures."""
from pathlib import Path
import hashlib
import json
import sys
import numpy as np
from PIL import Image

root, output = map(Path, sys.argv[1:])
assert not output.exists()
report = {}
for state, current, previous in [
    ('local', root/'relief-coupled-terrain-proof-001/raised', root/'relief-shore-field-proof-001/raised'),
    ('global', root/'native-global-coupled-terrain-001', root/'native-global-shore-field-001'),
    ('undo', root/'relief-coupled-terrain-proof-001/undo', root/'relief-shore-field-proof-001/undo'),
]:
    log = current.with_suffix('.log').read_text(encoding='utf8', errors='replace')
    assert 'ERROR' not in log
    assert log.count('authored coupled terrain:') == (0 if state == 'undo' else 2)
    delta = np.abs(np.array(Image.open(current.with_suffix('.png'))).astype('i2') -
                   np.array(Image.open(previous.with_suffix('.png'))).astype('i2'))
    item = {'changed_pixels':int(np.any(delta,axis=2).sum()),'max_channel_delta':int(delta.max())}
    for suffix in ['.shadow.bin','.object-depth.bin']:
        assert current.with_suffix(suffix).read_bytes() == previous.with_suffix(suffix).read_bytes()
    for suffix,dtype in [('shore.rgba16.bin','<f2'),('crests.f32.bin','<f4'),('deposit.bin','<u4')]:
        a,b = [np.fromfile(p.with_suffix('.spray')/f'frame-0120-{suffix}',dtype) for p in [current,previous]]
        assert np.isfinite(a).all() and np.isfinite(b).all()
        item[suffix] = {'changed_values':int(np.count_nonzero(a!=b)),
                       'max_delta':float(np.abs(a.astype('f8')-b.astype('f8')).max())}
    counts=[]; hashes=[]
    for p in [current,previous]:
        arrays=[np.fromfile(p.with_suffix('.spray')/f'frame-0120-{name}.bin','<f4').reshape(40960,4) for name in ['pos','vel','info']]
        assert all(np.isfinite(a).all() for a in arrays)
        rows=np.concatenate(arrays,axis=1)
        active=rows[:,9]>0
        assert not active[32768:].any()
        counts.append(int(active.sum()))
        hashes.append(hashlib.sha256(b''.join(sorted(row.tobytes() for row in rows[active]))).hexdigest())
    item['active_particles_current_previous']=counts
    item['active_particle_multiset_identical']=hashes[0]==hashes[1]
    item['active_particle_sha256']=hashes[0]
    if state=='undo':
        assert item['changed_pixels']==0 and hashes[0]==hashes[1]
        assert all(item[s]['changed_values']==0 for s in ['shore.rgba16.bin','crests.f32.bin','deposit.bin'])
    if state=='global': assert item['shore.rgba16.bin']['changed_values']>0
    report[state]=item
output.write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
