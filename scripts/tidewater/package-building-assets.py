"""Package pinned building metadata, geometry, materials and MIT notice locally.

Usage: CATALOG FRAMES SCENE UPSTREAM NEW_OUTPUT
Runtime atmosphere/lighting images are supplied by the scene, not frozen here.
"""
from pathlib import Path
import hashlib
import json
import sys

catalog_root, frames, scene, upstream, output = map(Path, sys.argv[1:])
assert not output.exists()
catalog_bytes = (catalog_root / 'catalog.json').read_bytes()
catalog = json.loads(catalog_bytes)
fixture = json.loads((scene / 'fixture.json').read_text())
assert catalog['schema_version'] == 1 and len(catalog['assets']) == 23
assert fixture['source']['commit'] == '1438b1abfcaee3267092b75573014f4d9b4a983c'
files = {}
material_ids = set()


def include(name, raw, expected=None):
    digest = hashlib.sha256(raw).hexdigest()
    assert expected is None or digest == expected
    assert name not in files or files[name] == raw
    files[name] = raw
    return {'file': name, 'sha256': digest, 'bytes': len(raw)}


def scene_file(name):
    assert Path(name).name == name and not any(c in name for c in '\\/:')
    raw = (scene / name).read_bytes()
    assert hashlib.sha256(raw).hexdigest() == fixture['files'][name]
    return raw


include('catalog.json', catalog_bytes)
for record in catalog['assets']:
    digest = record['content_hash']
    assert len(digest) == 64 and all(c in '0123456789abcdef' for c in digest)
    descriptor = (catalog_root / (digest + '.json')).read_bytes()
    include(digest + '.json', descriptor, digest)
    building = json.loads(descriptor)['building']
    assert building['source_id'] == record['logical_name']
    assert record['source']['declared_license'] == 'MIT'
    for part in building['parts']:
        material_ids.add(part['material'])
        for payload in list(part['attributes'].values()) + [part['indices'], part['reference_position'], part['reference_normal']]:
            name = payload['file']
            assert name == payload['sha256'] + '.bin'
            raw = (frames / name).read_bytes()
            assert len(raw) == payload['bytes']
            include(name, raw, payload['sha256'])

village = json.loads(scene_file('village.json'))
materials = village['materials']
assert len(materials) == 7
assert material_ids == {f'material-{i}' for i in range(7)}
textures = village['textures']
assert len(textures) == 14
for texture in textures.values():
    include(texture['file'], scene_file(texture['file']))
shader = include('village.wgsl', scene_file('village.wgsl'))
license_bytes = (upstream / 'LICENSE').read_bytes()
assert b'MIT License' in license_bytes and b'Permission is hereby granted' in license_bytes
license_record = include('LICENSE-Tidewater.txt', license_bytes)
material_bytes = json.dumps({'schema': 'forge.tidewater.building-material-library', 'version': 1,
                            'materials': {f'material-{i}': m for i, m in enumerate(materials)},
                            'textures': textures, 'shader': shader},
                           indent=2).encode()
material_library = include('materials.json', material_bytes)
manifest = {'schema': 'forge.tidewater.building-package', 'version': 1,
            'source_commit': fixture['source']['commit'], 'license': license_record,
            'catalog_sha256': hashlib.sha256(catalog_bytes).hexdigest(),
            'material_library': material_library,
            'files': {name: {'sha256': hashlib.sha256(raw).hexdigest(), 'bytes': len(raw)}
                      for name, raw in sorted(files.items())},
            'runtime_requirements': ['Bevy village material adapter and bevy_pbr shaders',
                                     'scene environment cube, terrain/object/cloud shadow textures',
                                     'scene frame and shadow uniforms'],
            'qualification': 'dependency package only; Studio import not yet qualified'}
output.mkdir()
for name, raw in files.items():
    (output / name).write_bytes(raw)
(output / 'package.json').write_text(json.dumps(manifest, indent=2))
# Reopen the package using only its own root.
for name, expected in manifest['files'].items():
    raw = (output / name).read_bytes()
    assert len(raw) == expected['bytes'] and hashlib.sha256(raw).hexdigest() == expected['sha256']
print(json.dumps({'buildings': len(catalog['assets']), 'files': len(files),
                  'bytes': sum(len(raw) for raw in files.values())}))
