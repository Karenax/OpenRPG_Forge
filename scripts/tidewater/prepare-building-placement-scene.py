"""Add pinned building descriptors to a split scene; placement authority stays in the zone."""
from pathlib import Path
import hashlib
import json
import os
import sys

scene, catalog_root, output = map(Path, sys.argv[1:])
assert not output.exists()
fixture = json.loads((scene / 'fixture.json').read_text())
catalog_raw = (catalog_root / 'catalog.json').read_bytes()
catalog = json.loads(catalog_raw)
assert catalog['schema_version'] == 1 and len(catalog['assets']) == 23
payloads = {'building-catalog.json': catalog_raw}
for record in catalog['assets']:
    digest = record['content_hash']
    assert len(digest) == 64 and all(c in '0123456789abcdef' for c in digest)
    raw = (catalog_root / (digest + '.json')).read_bytes()
    assert hashlib.sha256(raw).hexdigest() == digest
    payloads['building-' + digest + '.json'] = raw
output.mkdir()
for name, digest in fixture['files'].items():
    assert Path(name).name == name and not any(c in name for c in '\\/:')
    source = (scene / name).resolve()
    assert source.is_relative_to(scene.resolve())
    assert hashlib.sha256(source.read_bytes()).hexdigest() == digest
    os.link(source, output / name)
for name, raw in payloads.items():
    assert name not in fixture['files']
    (output / name).write_bytes(raw)
    fixture['files'][name] = hashlib.sha256(raw).hexdigest()
(output / 'fixture.json').write_text(json.dumps(fixture, indent=2))
print(json.dumps({'added_files': len(payloads), 'scene': str(output)}))
