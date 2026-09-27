"""Copy a validated fixed-scene fixture to a named upstream reference camera."""
import hashlib
import json
import shutil
import sys
from pathlib import Path

source, metadata_path, target = (Path(v).resolve() for v in sys.argv[1:4])
if target.exists() or target.is_relative_to(source):
    raise SystemExit('new output outside input required')
fixture = json.loads((source / 'fixture.json').read_text())
metadata_bytes = metadata_path.read_bytes()
metadata = json.loads(metadata_bytes)
assert metadata['status'] == 'ready_reference_only'
assert metadata['source_commit'] == fixture['source']['commit']
assert metadata['output'] == [1920,1080]
assert metadata['settings']['timeOfDay'] == 16.2, 'native night environment is not implemented'
for name, expected in fixture['files'].items():
    assert Path(name).name == name
    assert hashlib.sha256((source/name).read_bytes()).hexdigest() == expected
target.mkdir(parents=True)
for name in fixture['files']: shutil.copyfile(source/name,target/name)
c = metadata['camera']
fixture['camera_pose'] = {'position':[c['position'][k] for k in ('x','y','z')],
    'rotation':[c['quaternion'][k] for k in ('_x','_y','_z','_w')], 'fov_degrees':c['fov']}
fixture['reference_view'] = metadata['view']
fixture['reference_metadata_sha256'] = hashlib.sha256(metadata_bytes).hexdigest()
fixture['preparation'][Path(__file__).name] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
(target/'fixture.json').write_text(json.dumps(fixture,indent=2),encoding='utf-8')
print(json.dumps({'output':str(target),'view':metadata['view']}))
