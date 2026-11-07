"""Reconcile the actual Vault delta and native reader; do not qualify GUI/runtime."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
V = Path('F:/OpenRPG_AssetVault')
ROOT = W / 'house-cliff-lux-delivery-v743'


def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))


def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


assert not ROOT.exists()
index_path = W / 'house-cliff-lux-index-v740/RESULT.v1.json'
reader_path = W / 'house-cliff-lux-native-reader-v742/RESULT.v1.json'
review_path = W / 'house-cliff-lux-review-v739/RESULT.v1.json'
index, reader, review = read(index_path), read(reader_path), read(review_path)
assert index['cards_updated'] == reader['cards_verified'] == review['cards_verified'] == 2
assert index['images_installed'] == review['images_verified'] == 4
assert reader['status'] == 'two_current_native_source_cards_search_filter_details_images_passed'
assert reader['actual_exit_code'] == 0 and index['other_cards_unchanged'] and index['source_license_and_readiness_unchanged']
registry_path = V / 'metadata/asset-source-support-registry.v1.json'
registry = read(registry_path)
assert registry['revision'] == index['new_catalog_revision']
assert sha(V / registry['path']) == registry['sha256']
current = read(V / registry['path'])
before_registry = read(index_path.parent / 'prior-registry.json')
before = read(V / before_registry['path'])
old = {a['id']: a for a in before['assets']}
new = {a['id']: a for a in current['assets']}
ids = {a['id'] for a in index['cards']}
assert len(ids) == 2 and set(old) == set(new) and {i for i in old if old[i] != new[i]} == ids
allowed = {'names', 'descriptions', 'tags'}
for i in ids:
    assert {k: v for k, v in old[i].items() if k not in allowed and not k.startswith('preview')} == {k: v for k, v in new[i].items() if k not in allowed and not k.startswith('preview')}
delta = index['controlled_vault_delta']
assert sha(registry_path) == delta['new_registry_sha256']
assert sha(V / 'metadata/asset-import-registry.v1.json') == delta['model_registry_sha256']
pins = {**review['input_pins'], **reader['input_pins'], **index['preview_pins'], **delta['new_files']}
for p in [Path(__file__), index_path, reader_path, review_path, registry_path, V / registry['path']]:
    pins[str(p)] = sha(p)
for p, h in pins.items():
    assert sha(p) == h, p
missing_before = sum(not a.get('preview') for a in before['assets'])
missing_after = sum(not a.get('preview') for a in current['assets'])
assert missing_before - missing_after == 2
ROOT.mkdir()
result = {'status': 'two_native_source_cards_images_indexed_and_product_reader_verified',
          'finished_utc': datetime.now(timezone.utc).isoformat(), 'lot': 'L08.IMAGE.HOUSE.08',
          'cards_updated': 2, 'source_images_installed': 4, 'missing_primary_images_before': missing_before,
          'missing_primary_images_after': missing_after, 'source_cards': len(new), 'catalog_revision': registry['revision'],
          'search_pack_availability_filters_details_and_primary_image_verified': True,
          'sources_licenses_readiness_and_other_cards_unchanged': True, 'vault_delta_rehashed': True,
          'full_vault_rescan': False, 'source_support_model_requests_refused': True,
          'agent_visual_review': True, 'human_review': False, 'actual_editor_visual_verified': False,
          'asset_conversion_runtime_qualified': False, 'complete_assets_delivered_increment': 0,
          'full_import': False, 'input_pins': pins}
(ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps({k: v for k, v in result.items() if k != 'input_pins'}))
