"""Measure actual half-float differences without fitting or accepting a tolerance."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
import numpy as np
W=Path('F:/OpenRPG_ImportWork');CAP=W/'pirate-material-migration-render-v191';O=W/'pirate-wall-repeat-diagnostic-v195'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
run=read(CAP/'RUN-RESULT.v1.json');assert run['unity_exit_code']==0
rows=[];pins={str(CAP/'RUN-RESULT.v1.json'):sha(CAP/'RUN-RESULT.v1.json'),str(Path(__file__)):sha(Path(__file__))}
for row in run['comparisons']:
    if 'rockstonewall' not in row['prefab_id']:continue
    arrays=[]
    for p in row['raw_paths']:
        assert sha(p)==run['output_pins'][p];pins[p]=sha(p)
        a=np.frombuffer(Path(p).read_bytes(),dtype='<f2').reshape(512,512,4)
        assert np.isfinite(a).all();arrays.append(a)
    metrics=[]
    for name,j,k in [('migration_vs_current',0,1),('repeat_vs_current',0,2),('migration_vs_repeat',1,2)]:
        a,b=arrays[j][:,:,:3],arrays[k][:,:,:3];delta=np.abs(a.astype(np.float32)-b.astype(np.float32))
        different=a!=b
        metrics.append(dict(comparison=name,different_rgb_channels=int(different.sum()),
            different_rgb_pixels=int(different.any(axis=2).sum()),max_abs_linear_rgb=float(delta.max()),
            mean_abs_linear_rgb=float(delta.mean()),exact_rgb_channels_fraction=float((~different).mean())))
    rows.append(dict(view=row['view'],metrics=metrics))
assert len(rows)==2
result=dict(status='wall_migration_and_native_repeat_differences_measured_no_tolerance_admitted',rows=rows,
    input_pins=pins,source_equivalence_qualified=False,images_admitted=0,full_import=False,
    next_action='Identify the native renderer instability in view 0; do not relax the exact comparison based on this diagnostic alone.',
    finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(dict(status=result['status'],rows=rows)))
