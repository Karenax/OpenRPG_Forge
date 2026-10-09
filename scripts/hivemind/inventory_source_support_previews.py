"""Inventory declared source-card previews; never qualify actual image content."""
import collections
import datetime
import hashlib
import json
from pathlib import Path

VAULT = Path('F:/OpenRPG_AssetVault')
ROOT = Path('F:/OpenRPG_ImportWork/library-source-preview-coverage-v1')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    assert not ROOT.exists(), 'Preserve existing evidence'
    registry_path = VAULT / 'metadata/asset-source-support-registry.v1.json'
    registry_raw = registry_path.read_bytes()
    registry = json.loads(registry_raw)
    relative = Path(registry['path'])
    assert not relative.is_absolute() and '..' not in relative.parts
    catalog_path = VAULT / relative
    raw = catalog_path.read_bytes()
    assert sha(raw) == registry['sha256'] == registry['revision']
    assets = json.loads(raw)['assets']
    assert len({a['id'] for a in assets}) == len(assets)
    image_groups = collections.defaultdict(list)
    packs = {}
    for asset in assets:
        pack = packs.setdefault(asset['pack_id'], {'cards': 0, 'declared_preview': 0,
                                                  'declared_diagnostic_preview': 0,
                                                  'no_declared_preview': 0})
        pack['cards'] += 1
        preview, digest = asset.get('preview'), asset.get('preview_sha256')
        if preview:
            assert isinstance(digest, str) and len(digest) == 64
            assert all(c in '0123456789abcdef' for c in digest)
            pack['declared_preview'] += 1
            if asset.get('diagnostic_preview') is True:
                pack['declared_diagnostic_preview'] += 1
            image_groups[digest].append({'id': asset['id'], 'pack_id': asset['pack_id'],
                                        'preview': preview,
                                        'diagnostic_preview': asset.get('diagnostic_preview')})
        else:
            assert not digest
            pack['no_declared_preview'] += 1
    shared = {digest: rows for digest, rows in image_groups.items() if len(rows) > 1}
    assert registry_path.read_bytes() == registry_raw
    assert catalog_path.read_bytes() == raw
    report = {'schema': 'openrpg.source-preview-coverage.v1',
              'status': 'declared_source_preview_coverage_inventoried',
              'finished_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
              'registry_sha256': sha(registry_raw), 'catalog_sha256': sha(raw),
              'catalog': str(catalog_path), 'helper_sha256': sha(Path(__file__).read_bytes()),
              'source_cards': len(assets), 'packs': packs,
              'declared_preview_cards': sum(p['declared_preview'] for p in packs.values()),
              'declared_diagnostic_preview_cards': sum(p['declared_diagnostic_preview'] for p in packs.values()),
              'unique_declared_image_hashes': len(image_groups),
              'shared_declared_image_hashes': len(shared),
              'cards_with_shared_declared_image_hash': sum(map(len, shared.values())),
              'shared_image_groups': shared,
              'actual_image_bytes_reopened': False, 'generic_image_content_classified': False,
              'actual_editor_images_verified': False, 'full_import': False}
    ROOT.mkdir()
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in report.items() if k not in ('packs', 'shared_image_groups')}))


if __name__ == '__main__':
    main()
