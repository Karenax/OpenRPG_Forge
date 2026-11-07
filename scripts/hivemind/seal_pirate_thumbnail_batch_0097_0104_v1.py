"""Reconcile the actual Vault delta and native reader; do not qualify GUI/runtime."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
V = Path('F:/OpenRPG_AssetVault')
ROOT = W / 'pirate-source-thumbnail-batch-0097-0104-delivery-reopen-v1'


def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))


def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


assert not ROOT.exists()
index_path = W / 'pirate-source-thumbnail-batch-0097-0104-index-v1/RESULT.v1.json'
reader_path = W / 'pirate-source-thumbnail-batch-0097-0104-native-reader-v1/RESULT.v1.json'
review_path = W / 'pirate-source-thumbnail-batch-0097-0104-review-v1/RESULT.v1.json'
index, reader, review = read(index_path), read(reader_path), read(review_path)
assert index['cards_updated'] == reader['cards_verified'] == review['cards_verified'] == 7
assert index['images_installed'] == review['images_verified'] == 14
assert reader['status'] == 'reviewed_current_native_source_cards_search_filter_details_images_passed'
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
assert len(ids) == 7 and set(old) == set(new) and {i for i in old if old[i] != new[i]} == ids
allowed = {'names', 'descriptions', 'tags'}
for i in ids:
    assert {k: v for k, v in old[i].items() if k not in allowed and not k.startswith('preview')} == {k: v for k, v in new[i].items() if k not in allowed and not k.startswith('preview')}
delta = index['controlled_vault_delta']
assert sha(registry_path) == delta['new_registry_sha256']
assert sha(V / 'metadata/asset-import-registry.v1.json') == delta['model_registry_sha256']
index_prepared = read(index_path.parent / 'PREPARED.v1.json')
resolved_executed_inputs = {}
for original, expected in index_prepared['input_pins'].items():
    actual = Path(original)
    if sha(actual) != expected:
        assert actual.name == 'apply_pirate_thumbnail_batch_0097_0104_v1.py'
        actual = W / 'pirate-source-thumbnail-batch-0097-0104-console-count-fix-v1' / actual.name
        assert sha(actual) == expected
        resolved_executed_inputs[original] = str(actual)
    assert sha(actual) == expected

pins = {**review['input_pins'], **reader['input_pins'], **index['preview_pins'], **delta['new_files']}
for snapshot in resolved_executed_inputs.values(): pins[snapshot] = sha(Path(snapshot))
for extra in [index_path.parent / 'PREPARED.v1.json', W / 'pirate-source-thumbnail-batch-0097-0104-receipt-count-repair-v1/RESULT.v1.json', W / 'pirate-source-thumbnail-batch-0097-0104-console-count-fix-v1/RESULT.v1.json']: pins[str(extra)] = sha(extra)
for p in [Path(__file__), index_path, reader_path, review_path, registry_path, V / registry['path']]:
    pins[str(p)] = sha(p)
for p, h in pins.items():
    assert sha(p) == h, p
missing_before = sum(not a.get('preview') for a in before['assets'])
missing_after = sum(not a.get('preview') for a in current['assets'])
assert missing_before - missing_after == 7
ROOT.mkdir()
result = {'status': 'seven_native_source_cards_images_indexed_and_product_reader_verified',
          'finished_utc': datetime.now(timezone.utc).isoformat(), 'lot': 'L08.IMAGE.PIRATE.04',
          'cards_updated': 18, 'source_images_installed': 36, 'cards_refused': 6, 'private_sdk_migrations_classified': True, 'source_version_parity_qualified': False, 'missing_primary_images_before': missing_before,
          'missing_primary_images_after': missing_after, 'source_cards': len(new), 'catalog_revision': registry['revision'],
          'search_pack_availability_filters_details_and_primary_image_verified': True,
          'sources_licenses_readiness_and_other_cards_unchanged': True, 'vault_delta_rehashed': True,
          'full_vault_rescan': False, 'source_support_model_requests_refused': True,
          'agent_visual_review': True, 'human_review': False, 'actual_editor_visual_verified': False,
          'asset_conversion_runtime_qualified': False, 'complete_assets_delivered_increment': 0,
          'full_import': False, 'input_pins': pins}
(ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps({k: v for k, v in result.items() if k != 'input_pins'}))
