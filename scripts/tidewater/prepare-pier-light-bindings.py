"""Prepare exact pier subframe joins from independently pinned source captures.

This is derived import metadata. It neither installs lights nor changes placements.
"""
import argparse
import hashlib
import json
from pathlib import Path

COMMIT = '1438b1abfcaee3267092b75573014f4d9b4a983c'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def package(root, manifest_name):
    raw = (root / manifest_name).read_bytes()
    document = json.loads(raw)
    assert document['source_commit'] == COMMIT
    for name, entry in document['files'].items():
        path = (root / name).resolve()
        assert path.is_relative_to(root.resolve())
        data = path.read_bytes()
        assert len(data) == entry['bytes'] and sha(data) == entry['sha256'], name
    return document, sha(raw)


def referenced(root, manifest, reference):
    assert reference['file'] in manifest['files']
    assert manifest['files'][reference['file']]['sha256'] == reference['sha256']
    raw = (root / reference['file']).read_bytes()
    assert len(raw) == reference['bytes'] and sha(raw) == reference['sha256']
    return json.loads(raw)


def close(a, b):
    assert len(a) == len(b)
    assert max(abs(x - y) for x, y in zip(a, b)) < 1e-12, (a, b)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--lights', type=Path, required=True)
    parser.add_argument('--pier', type=Path, required=True)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    assert not args.output.exists(), 'Fresh output required'
    lm, light_pin = package(args.lights, 'manifest.json')
    pm, pier_pin = package(args.pier, 'package.json')
    lights = referenced(args.lights, lm, lm['descriptor'])
    acquisition = referenced(args.lights, lm, lm['acquisition'])
    pier = referenced(args.pier, pm, pm['pier'])['pier']
    assert lights['source_commit'] == COMMIT
    assert pier['source_id'] == f'tidewater:{COMMIT}:pier:0'
    assert len(pier['lanterns']) == 11
    assert pier['source_frame']['rotation_xyzw'] == [0, 0, 0, 1]
    assert pier['source_frame']['scale'] == [1, 1, 1]
    # The source constructor explicitly creates lanterns from pierInfo.hung.
    # Preserve that ordinal identity; proximity is never used to choose an owner.
    sources = {entry['path']: entry for entry in lights['source_inputs']}
    for name in ['src/world/Village.js', 'src/world/Pier.js']:
        raw = (args.source / name).read_bytes()
        assert len(raw) == sources[name]['bytes'] and sha(raw) == sources[name]['sha256']
    village = (args.source / 'src/world/Village.js').read_text(encoding='utf-8')
    assert 'const hung = this.pierInfo.hung || [];' in village
    assert 'for ( const h of hung )' in village
    assert 'this.lanterns.push( { obj: g, pivot: h.pivot, rest: h.rest, live: h.live,' in village
    frames = {frame['id']: frame for frame in lights['frames']}
    emitters = {light['id']: light for light in lights['lights']}
    selected = [row for row in acquisition['editable_ownership']['resolved']
                if row['source_binding'].get('producer_function') == '_buildLanterns']
    assert len(selected) == 11
    by_ordinal = {row['source_binding']['ordinal']: row for row in selected}
    assert set(by_ordinal) == set(range(11))
    rows = []
    root_translation = pier['source_frame']['translation_m']
    for ordinal, lantern in enumerate(pier['lanterns']):
        owner = by_ordinal[ordinal]
        binding = owner['source_binding']
        assert binding['collection'] == 'lanterns' and binding['anchor'] == 'obj'
        assert binding['evidence'] == 'live-vector-object-identity'
        frame = frames[owner['owner_frame']]
        assert frame['id'] == f'village-lantern-{ordinal}'
        assert frame['source_object'] == f'Village.lanterns[{ordinal}].obj'
        assert frame['parent'] == 'world'
        pivot_world = [a + b for a, b in zip(root_translation, lantern['pivot'])]
        expected_matrix = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, *pivot_world, 1]
        close(frame['world_from_local'], expected_matrix)
        light = emitters[owner['light_id']]
        assert light['owner_frame'] == frame['id'] and light['direction_local'] is None
        local_center = [a - b for a, b in zip(lantern['rest'], lantern['pivot'])]
        close(light['position_local_m'], local_center)
        close(light['acquired_position_world_m'],
              [a + b for a, b in zip(root_translation, lantern['rest'])])
        rows.append({'lantern_ordinal': ordinal, 'light_id': light['id'],
                     'registration_order': light['registration_order'],
                     'owner_frame': frame['id'], 'pivot_in_asset_m': lantern['pivot'],
                     'rest_center_in_asset_m': lantern['rest'],
                     'source_binding': binding})
    assert len({row['light_id'] for row in rows}) == 11
    assert len({row['registration_order'] for row in rows}) == 11
    receipt = {'schema': 'forge.tidewater.pier-light-bindings', 'version': 1,
               'source_commit': COMMIT, 'source_object_id': pier['source_id'],
               'pier_package_sha256': pier_pin, 'light_package_sha256': light_pin,
               'light_descriptor_sha256': lm['descriptor']['sha256'],
               'pier_descriptor_sha256': pm['pier']['sha256'],
               'source_frame': pier['source_frame'], 'lantern_bindings': rows,
               'script_sha256': sha(Path(__file__).read_bytes()),
               'source_ordinal_join_verified': True, 'anchor_roundtrip_verified': True,
               'phases_preserved_by_reference': True,
               'studio_installed': False, 'live_swing_binding_verified': False,
               'rendered_pixels_verified': False, 'visual_parity': False}
    args.output.mkdir(parents=True)
    output = args.output / 'pier-light-bindings.json'
    output.write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'bindings': len(rows), 'receipt': str(output), 'sha256': sha(output.read_bytes())}))


if __name__ == '__main__':
    main()
