"""Remove legacy building/material payloads so package rendering cannot fall back."""
from pathlib import Path
import hashlib
import json
import os
import sys

root, output = map(Path, sys.argv[1:])
assert not output.exists()
fixture = json.loads((root / 'fixture.json').read_text())
scene = json.loads((root / 'village.json').read_text())
assert scene['version'] == 2
building_geometry = {n['geometry'] for n in scene['nodes'] if n.get('building_source_id')}
residual_geometry = {n['geometry'] for n in scene['nodes'] if not n.get('building_source_id')}


def payloads(keys):
    result = set()
    for key in keys:
        geometry = scene['geometries'][key]
        result.update(geometry['attributes'].values())
        result.add(geometry['indices'])
    return result


remove = payloads(building_geometry) - payloads(residual_geometry)
remove.update(t['file'] for t in scene['textures'].values())
remove.add('village.wgsl')
remove.update(n for n in fixture['files'] if n.startswith('building-'))
output.mkdir()
removed = []
for name, digest in list(fixture['files'].items()):
    assert Path(name).name == name and not any(c in name for c in '\\/:')
    source = (root / name).resolve()
    assert source.is_relative_to(root.resolve())
    assert hashlib.sha256(source.read_bytes()).hexdigest() == digest
    if name in remove:
        removed.append(name)
        del fixture['files'][name]
    else:
        os.link(source, output / name)
fixture['requires_building_package'] = True
(output / 'fixture.json').write_text(json.dumps(fixture, indent=2))
(output / 'package-only-proof.json').write_text(json.dumps({
    'removed_files': sorted(removed), 'building_nodes': sum(bool(n.get('building_source_id')) for n in scene['nodes']),
    'residual_nodes': sum(not n.get('building_source_id') for n in scene['nodes']),
    'note': 'node membership remains as scene placement metadata; building geometry and material payloads require package'
}, indent=2))
print(json.dumps({'removed_files': len(removed), 'retained_files': len(fixture['files'])}))
