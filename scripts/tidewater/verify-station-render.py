"""Verify effective station counts, resized GPU buffers and empty-shore behavior."""
from pathlib import Path
import hashlib,json,sys
import numpy as np
root,output=map(Path,sys.argv[1:])
assert not output.exists()
report={}
for state,stem in [('raised','relief-stations-proof-001/raised'),('global','native-global-stations-001'),('shallow','native-shallow-stations-001')]:
    base=root/stem
    oracle=json.loads((root/f'stations-{state}-oracle-001.json').read_text())
    log=base.with_suffix('.log').read_text(encoding='utf8',errors='replace')
    assert 'ERROR' not in log
    assert f"authored breaker stations: count={oracle['count']}, sha256={oracle['sha256']}" in log
    spray=base.with_suffix('.spray')
    meta=json.loads((spray/'probe.json').read_text())
    assert meta['native_stations_sha256']==oracle['sha256']
    crests=np.fromfile(spray/'frame-0120-crests.f32.bin','<f4')
    assert len(crests)==max(1,oracle['count'])*24 and np.isfinite(crests).all()
    info=np.fromfile(spray/'frame-0120-info.bin','<f4').reshape(40960,4)
    active=int((info[:,1]>0).sum())
    for name in ['pos','vel','info']:
        assert np.isfinite(np.fromfile(spray/f'frame-0120-{name}.bin','<f4')).all()
    assert np.isfinite(np.fromfile(spray/'frame-0120-shore.rgba16.bin','<f2')).all()
    if oracle['count']==0:
        assert active==0 and not crests.any()
        assert not np.fromfile(spray/'frame-0120-deposit.bin','<u4').any()
        assert not np.fromfile(spray/'history-head.u32.bin','<u4').any()
    else: assert active>0
    report[state]={'stations':oracle['count'],'source_coordinates_exact':True,'crest_buffer_bytes':crests.nbytes,
                   'active_particles':active,'image_sha256':hashlib.sha256(base.with_suffix('.png').read_bytes()).hexdigest()}
proof=json.loads((root/'relief-stations-proof-001/report.json').read_text())
assert proof['undo']['changed_pixels']==0
report['binary_sha256']=proof['binary_sha256']
report['undo_pixel_exact']=True
output.write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
