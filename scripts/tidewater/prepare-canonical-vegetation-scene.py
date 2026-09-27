"""Replace acquired instance streams with canonical document/blobs in a scene fixture."""
from pathlib import Path
import hashlib,json,sys
source,imported,target = map(Path,sys.argv[1:])
assert not target.exists()
fixture = json.loads((source/'fixture.json').read_text())
assert fixture['source']['commit']=='1438b1abfcaee3267092b75573014f4d9b4a983c'
scene = json.loads((source/'vegetation-scene.json').read_text())
removed = {p for group in scene['groups'] for p in group['streams'].values()}
payloads = {}
for name,digest in fixture['files'].items():
    raw = (source/name).read_bytes()
    assert hashlib.sha256(raw).hexdigest()==digest
    if name not in removed: payloads[name]=raw
doc = (imported/'vegetation.v2.json').read_bytes()
document = json.loads(doc)
assert all(block['parameter_names'][-1]=='source_order' for block in document['imported_source']['blocks'])
payloads['canonical-vegetation.json']=doc
for block in document['imported_source']['blocks']:
    digest = block['instances_sha256']
    raw = (imported/(digest+'.bin')).read_bytes()
    assert hashlib.sha256(raw).hexdigest()==digest
    payloads['canonical-'+digest+'.bin']=raw
fixture['files']={name:hashlib.sha256(raw).hexdigest() for name,raw in payloads.items()}
target.mkdir()
for name,raw in payloads.items():(target/name).write_bytes(raw)
(target/'fixture.json').write_text(json.dumps(fixture,indent=2))
(target/'canonical-import.json').write_text(json.dumps({'removed_acquired_streams':sorted(removed),'canonical_document_sha256':hashlib.sha256(doc).hexdigest()},indent=2))
print(json.dumps({'removed_streams':len(removed),'canonical_blocks':len(document['imported_source']['blocks'])}))
