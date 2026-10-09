"""Pin the 71 no-PNG source cards into six explicit conversion/image batches."""
import collections
import datetime
import hashlib
import json
from pathlib import Path

WORK = Path('F:/OpenRPG_ImportWork')
VAULT = Path('F:/OpenRPG_AssetVault')
ROOT = WORK / 'library-no-png-source-batches-v1'
MISSING = WORK / 'library-source-preview-coverage-v1/MISSING_PREVIEWS.v1.json'
INVENTORY = WORK / 'remaining-source-preview-inventory-v1/RECORDS.v1.json'
REGISTRY = VAULT / 'metadata/asset-source-support-registry.v1.json'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def within(relative):
    path = VAULT / relative
    assert path.resolve().is_relative_to(VAULT.resolve()), 'Source escapes Vault'
    assert path.is_file(), 'Source absent: ' + relative
    return path


def main():
    assert not ROOT.exists(), 'Prior batch preparation preserved'
    registry = read(REGISTRY)
    catalog_path = within(registry['path'])
    pins = {str(p): sha(p) for p in [MISSING, INVENTORY, REGISTRY, catalog_path, Path(__file__).resolve()]}
    assert pins[str(catalog_path)] == registry['sha256'] == registry['revision']
    missing = read(MISSING)
    assert missing['catalog_sha256'] == registry['sha256']
    previous = read(INVENTORY)['rows']
    by_id = {r['asset_id']: r for r in previous}
    assert len(by_id) == len(previous)
    catalog = read(catalog_path)['assets']
    cards = {r['id']: r for r in catalog}
    assert len(cards) == len(catalog)
    groups = collections.defaultdict(list)
    for row in missing['rows']:
        candidate = by_id[row['id']]
        if candidate['preview_found']:
            continue
        card = cards[row['id']]
        assert card['id'] == candidate['asset_id'] and card['pack_id'] == row['pack_id'] == candidate['pack_id']
        assert card['source_file'] == row['source_file'] and card['source_asset_path'] == row['source_asset_path']
        assert card['availability'] == 'source_support' and card['private_only']
        for field, digest in [('source_file', 'source_sha256'), ('source_meta_file', 'source_meta_sha256')]:
            path = within(card[field])
            actual = sha(path)
            assert actual == card[digest], 'Source SHA mismatch: ' + card['id']
            if str(path) in pins:
                assert pins[str(path)] == actual
            pins[str(path)] = actual
        groups[row['pack_id']].append(dict(
            id=card['id'], source_asset_path=card['source_asset_path'], source_guid=card['source_guid'],
            source_file=card['source_file'], source_sha256=card['source_sha256'],
            source_meta_file=card['source_meta_file'], source_meta_sha256=card['source_meta_sha256'],
            license=card['license'], source_archive=card['source_archive'],
            source_archive_sha256=card['source_archive_sha256'], original_lod_level=card.get('original_lod_level'),
            material_bindings=card.get('material_bindings'), unported_components=card.get('unported_components'),
            phase='source_pinned_geometry_material_and_image_work_pending', images_generated=False))
    assert len(groups) == 6 and sum(map(len, groups.values())) == 71
    assert all(sha(p) == h for p, h in pins.items()), 'Input changed during batch preparation'
    ROOT.mkdir()
    batches = []
    for index, (pack, rows) in enumerate(sorted(groups.items()), 1):
        name = f'L08.NO_PNG.{index:02d}.v1.json'
        batch = dict(id=f'L08.NO_PNG.{index:02d}', pack_id=pack, expected_cards=len(rows),
                     distinct_source_prefabs=len({r['source_file'] for r in rows}), rows=rows,
                     images_generated=0, assets_delivered=0, studio_verified=False, full_import=False)
        path = ROOT / name
        path.write_text(json.dumps(batch, indent=2, ensure_ascii=False), 'utf-8')
        batches.append(dict(id=batch['id'], pack_id=pack, expected_cards=len(rows),
                            distinct_source_prefabs=batch['distinct_source_prefabs'], receipt=str(path), sha256=sha(path)))
    result = dict(status='71_no_png_source_cards_pinned_into_six_batches',
                  input_pins=pins, batches=batches, source_cards=71,
                  distinct_source_prefabs=len({r['source_file'] for rows in groups.values() for r in rows}),
                  limitation='Source catalog has no admitted geometry GLB reference for these cards; this preparation does not prove absence of intermediates elsewhere.',
                  next_steps=['Resolve and verify complete prefab geometry and LOD bindings',
                              'Qualify applicable source materials and deformation',
                              'Render and inspect asset-specific images', 'Publish a private revision and verify it in actual Studio'],
                  vault_modified=False, images_generated=0, assets_delivered=0, full_import=False,
                  recorded_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, ensure_ascii=False), 'utf-8')
    print(json.dumps({k: result[k] for k in ['status', 'source_cards', 'distinct_source_prefabs', 'images_generated', 'assets_delivered']}))


if __name__ == '__main__':
    main()
