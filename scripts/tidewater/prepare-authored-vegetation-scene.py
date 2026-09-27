"""Attach a verified-at-runtime zone archive to a canonical vegetation scene fixture."""
import hashlib,json,sys
from pathlib import Path
source,archive,target=map(Path,sys.argv[1:])
assert not target.exists()
fixture=json.loads((source/'fixture.json').read_text())
assert 'canonical-vegetation.json' in fixture['files']
target.mkdir()
for name,digest in fixture['files'].items():
    assert Path(name).name==name
    raw=(source/name).read_bytes()
    assert hashlib.sha256(raw).hexdigest()==digest
    (target/name).write_bytes(raw)
raw=archive.read_bytes()
assert len(raw)<4*1024*1024
(target/'canonical-zone.json').write_bytes(raw)
fixture['files']['canonical-zone.json']=hashlib.sha256(raw).hexdigest()
(target/'fixture.json').write_text(json.dumps(fixture,indent=2))
