"""Independently compare every canonical placement with its source JSON record."""
import hashlib
import json
import math
from pathlib import Path
import struct
import sys

source, first, repeat, output = map(Path, sys.argv[1:])
assert not output.exists()
document = json.loads((first / 'vegetation.v2.json').read_text())
provenance = json.loads((first / 'import.json').read_text())
assert {p.name for p in first.iterdir()} == {p.name for p in repeat.iterdir()}
for path in first.iterdir():
    assert path.read_bytes() == (repeat / path.name).read_bytes(), path.name
total = 0
counts = json.loads((source / 'manifest.json').read_text())['vegetation_counts']
offsets = {'palms':0,'bananas':0,'youngPalms':0,'ferns':counts['youngPalms'],'monsteras':0,
           'elephantEars':0,'heliconias':counts['elephantEars'],
           'strelitzias':counts['elephantEars']+counts['heliconias'],'trees':0,'shrubs':counts['trees']}
for block, origin in zip(document['imported_source']['blocks'], provenance['blocks'], strict=True):
    raw = (source / origin['source_file']).read_bytes()
    assert hashlib.sha256(raw).hexdigest() == origin['source_sha256']
    original = json.loads(raw)
    orders = {r['source_id']:i+offsets[Path(origin['source_file']).stem] for i,r in enumerate(original)}
    records = sorted(original, key=lambda item: item['source_id'])
    data = (first / (block['instances_sha256'] + '.bin')).read_bytes()
    assert hashlib.sha256(data).hexdigest() == block['instances_sha256']
    numbers = 10 + len(block['parameter_names'])
    assert numbers in (15,16)
    assert len(data) == len(records) * (32+8*numbers) and len(records) == block['count']
    for record, unpacked in zip(records, struct.iter_unpack(f'<32s{numbers}d', data), strict=True):
        key, *values = unpacked
        assert key.hex() == record['source_id']
        assert values[:3] == [record[k] for k in ('x', 'y', 'z')]
        assert values[3:7] == [0, math.sin(record['yaw']/2), 0, math.cos(record['yaw']/2)]
        assert values[7:10] == [record['s']] * 3
        assert values[10:15] == [record['la'], record['l'], record['H'], record['seed'], record.get('sy', 1)]
        if numbers == 16:
            assert block['parameter_names'][-1] == 'source_order' and values[15] == orders[record['source_id']]
        total += 1
assert total == provenance['total'] == 65383
report = {'status': 'pass', 'instances': total, 'blocks': len(document['imported_source']['blocks']),
          'repeat_files_exact': len(list(first.iterdir())),
          'source_positions_scales_parameters_exact_f64': True,
          'scope': 'data only; quaternion conversion checked; no renderer or Studio qualification'}
output.write_text(json.dumps(report, indent=2))
print(json.dumps(report))
