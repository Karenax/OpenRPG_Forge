"""Capture eight new House cards; leave previous captures/refusals and Vault untouched."""
from pathlib import Path
from datetime import datetime, timezone
import json
import os
import subprocess
import sys

import queue_house_thumbnail_batch_v1 as capture

S = Path(__file__).parent
W = capture.W
V = capture.V
ROOT = W / 'house-source-thumbnail-batch-0025-0032-v1'
capture.ROOT = ROOT


def prepare():
    capture.no_unity()
    assert not ROOT.exists()
    registry_path = V / 'metadata/asset-source-support-registry.v1.json'
    registry = capture.read(registry_path)
    catalog_path = V / registry['path']
    assert capture.sha(catalog_path) == registry['sha256']
    catalog = capture.read(catalog_path)
    previous = capture.read(W / 'house-source-thumbnail-batch-0001-0008-v1/PREPARED.v1.json')
    excluded_ids = {row['id'] for row in previous['cases']}
    selected = sorted([a for a in catalog['assets'] if a.get('pack_id') == 'hivemind.house-on-a-hill'
                       and not a.get('preview') and '/HDRP/' in a.get('source_asset_path', '')
                       and a.get('source_asset_path', '').endswith('.prefab') and a['id'] not in excluded_ids], key=lambda a: a['id'])[:8]
    assert len(selected) == 8
    private_helper = capture.PROJECT / 'Assets/Editor' / capture.HELPER.name
    assert capture.HELPER.read_bytes() == private_helper.read_bytes()
    source_manifest_path = W / 'hivemind-run/house-on-a-hill/source.v1.json'
    source_manifest = capture.read(source_manifest_path)
    assert source_manifest['archive_sha256'] == 'd476a7102427cb5ae7bc8ab8c828c4dbc9815ba94650d9b3b829205ca5f02958'
    pins = {str(p): capture.sha(p) for p in (Path(__file__), S / 'queue_house_thumbnail_batch_v1.py', capture.HELPER, private_helper, source_manifest_path, catalog_path)}
    source_root = source_manifest_path.parent
    originals_checked = 0
    for row in source_manifest['files']:
        path = source_root / row['stored_path']
        assert path.resolve().is_relative_to(source_root.resolve())
        assert capture.sha(path) == row['sha256'], path
        pins[str(path)] = row['sha256']
        originals_checked += 1
    ROOT.mkdir()
    cases = []
    for index, asset in enumerate(selected):
        assert asset['source_archive_sha256'] == source_manifest['archive_sha256']
        for field, digest in [('source_file', 'source_sha256'), ('source_meta_file', 'source_meta_sha256')]:
            path = V / asset[field]
            assert path.resolve().is_relative_to(V.resolve())
            assert capture.sha(path) == asset[digest]
            pins[str(path)] = asset[digest]
        folder = ROOT / f'case-{index:02d}'
        folder.mkdir()
        cases.append({'id': asset['id'], 'prefab_path': asset['source_asset_path'], 'prefab_guid': asset['source_guid'], 'output': str(folder / 'NATIVE-RESULT.v1.json')})
    request = ROOT / 'REQUEST.v1.json'
    capture.write(request, {'cases': cases, 'output': str(ROOT / 'NATIVE-RESULT.v1.json')})
    pins[str(request)] = capture.sha(request)
    capture.write(ROOT / 'PREPARED.v1.json', {'sub_batch': 'L08.IMAGE.HOUSE.05', 'expected_cards': 8, 'expected_images': 16,
                  'cases': cases, 'input_pins': pins, 'catalog_revision': registry['revision'], 'original_files_checked': originals_checked,
                  'original_version_parity': False, 'presentation_only': True, 'source_assets_executed': False, 'full_import': False})
    capture.write(ROOT / 'LOT.v1.json', {'lot': 'L08.IMAGE.HOUSE.05', 'ids': [a['id'] for a in selected],
                  'excluded_previous_ids': sorted(excluded_ids), 'phases': ['capture originale native', 'SHA et PNG', 'revue de chaque image', 'indexation transactionnelle', 'recherche et lecture native'],
                  'images_expected': 16, 'work_root': str(ROOT), 'vault': str(V), 'vault_modified': False})
    capture.save({'status': 'house_eight_card_native_thumbnail_batch_prepared', 'full_import': False, 'images_installed_in_vault': False})
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT, stdout=log, stderr=log, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    capture.write(ROOT / 'JOB.v1.json', {'pid': child.pid, 'created_utc': datetime.now(timezone.utc).isoformat()})
    print(json.dumps({'pid': child.pid, 'cards': 8, 'original_files_checked': originals_checked, 'output': str(ROOT)}))


if __name__ == '__main__':
    prepare() if len(sys.argv) == 1 else capture.run()
