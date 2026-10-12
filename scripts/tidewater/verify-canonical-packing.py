"""Compare canonical Rust plant packing to the original exported GPU streams, by source key."""
import hashlib
import json
from pathlib import Path
import sys
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from render_verification import reconcile_source_ids

source, native, report = map(Path, sys.argv[1:])
assert not report.exists()
manifest = json.loads((source / 'manifest.json').read_text())
assert manifest['source']['commit'] == '1438b1abfcaee3267092b75573014f4d9b4a983c'
files = {f['path']: f for f in manifest['files']}
def read(name):
    raw = (source / name).read_bytes()
    assert hashlib.sha256(raw).hexdigest() == files[name]['sha256'], name
    return raw

runtime = json.loads(read('vegetation/runtime.json'))
layout = {'palms':['palms'], 'bananas':['bananas'], 'understory':['youngPalms','ferns'],
          'monsteras':['monsteras'], 'broadleaf':['elephantEars','heliconias','strelitzias'], 'canopy':['trees','shrubs']}
expected = {}
expected_streams = {}
for group in runtime['types']:
    records = [r for species in layout[group['name']] for r in json.loads(read(f'vegetation/{species}.json'))]
    arrays = [np.frombuffer(read(group['streams'][key]['path']),dtype='<f4').reshape(len(records),width)
              for key,width in [('matrices',16),('iPos',4),('iDat',4),('qr2',1)]]
    packed = np.concatenate(arrays,axis=1)
    expected_streams[group['name']] = packed.tobytes()
    for record, values in zip(records, packed, strict=True):
        assert record['source_id'] not in expected
        expected[record['source_id']] = (group['name'],values)

description = json.loads((native / 'packing.json').read_text())
dtype = np.dtype([('key','u1',(32,)),('values','<f4',(25,))])
seen = set()
different_words = np.zeros(25,dtype=np.int64)
maximum_error = np.zeros(25)
different_records = 0
for layer in description['layers']:
    raw = (native / layer['file']).read_bytes()
    assert hashlib.sha256(raw).hexdigest() == layer['sha256']
    rows = np.frombuffer(raw,dtype=dtype)
    assert len(rows) == layer['count']
    for row in rows:
        key = row['key'].tobytes().hex()
        assert key not in seen
        seen.add(key)
        group, oracle = expected[key]
        assert group == layer['group']
        actual = row['values']
        differences = actual.view('<u4') != oracle.view('<u4')
        different_records += bool(np.any(differences))
        different_words += differences
        maximum_error = np.maximum(maximum_error,np.abs(actual.astype(float)-oracle.astype(float)))
coverage = reconcile_source_ids(expected, seen)
assert len(seen) == 65383
ordered_exact = False
if description.get('groups'):
    assert {g['name'] for g in description['groups']} == set(expected_streams)
    for group in description['groups']:
        raw = (native / group['file']).read_bytes()
        assert hashlib.sha256(raw).hexdigest() == group['sha256']
        assert raw == expected_streams[group['name']], group['name']
    ordered_exact = True
result = {'instances':len(seen),'different_records':different_records,'different_words_by_component':different_words.tolist(),
          'source_coverage':coverage,
          'max_abs_error_by_component':maximum_error.tolist(),'bit_exact':not bool(np.any(different_words)),
          'ordered_group_streams_exact':ordered_exact,
          'layout':'matrix16, iPos4, iDat4, qr2','scope':'packing values and optional ordered groups; draw calls and pixels not qualified'}
report.write_text(json.dumps(result,indent=2))
print(json.dumps(result))
if not result['bit_exact']:
    raise SystemExit(1)
