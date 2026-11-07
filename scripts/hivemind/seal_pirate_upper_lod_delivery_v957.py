"""Seal image delivery against the current registry and actual product reader."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json

W = Path('F:/OpenRPG_ImportWork')
V = Path('F:/OpenRPG_AssetVault')
O = W/'pirate-upper-lod-images-delivery-v957'
pins = {}

def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

def read(p):
    pins[str(p)] = sha(p)
    return json.loads(Path(p).read_text('utf-8-sig'))

assert not O.exists()
index = read(W/'pirate-upper-lod-images-index-v955/RESULT.v1.json')
reader = read(W/'pirate-upper-lod-images-native-reader-v956/RESULT.v1.json')
native = read(W/'pirate-upper-lod-images-native-reader-v956/NATIVE-RESULT.v1.json')
review = read(W/'pirate-upper-lod-images-reviewed-v954/RESULT.v1.json')
run = read(W/'pirate-upper-lod-images-v951/RUN-RESULT.v1.json')
assert index['cards_updated'] == reader['cards_verified'] == native['cards_verified'] == review['cards_verified'] == 22
assert reader['actual_exit_code'] == 0 and reader['reader_logic_unchanged']
assert index['images_installed'] == review['images_verified'] == 44
assert run['unity_exit_code'] == 0 and run['input_changes'] == [] and len(run['refusals']) == 2
registry = read(V/'metadata/asset-source-support-registry.v1.json')
assert registry['revision'] == index['new_catalog_revision']
catalog_path = V/registry['path']
assert sha(catalog_path) == registry['sha256']
catalog = read(catalog_path)['assets']
assert len(catalog) == 12517 and sum(not c.get('preview') for c in catalog) == 493
current = {c['id']:c for c in catalog}
before = read(W/'pirate-upper-lod-images-index-v955/prior-entries.json')
for old in before:
    card = current[old['id']]
    allowed = {'names', 'descriptions', 'tags'} | {key for key in card if key.startswith('preview')}
    assert {k:v for k,v in old.items() if k not in allowed} == {k:v for k,v in card.items() if k not in allowed}
    for field, digest in [('source_file', 'source_sha256'), ('source_meta_file', 'source_meta_sha256')]:
        p = V/card[field]
        assert sha(p) == card[digest]
        pins[str(p)] = card[digest]
for p, digest in index['preview_pins'].items():
    assert sha(p) == digest
    pins[p] = digest
for p, digest in index['controlled_vault_delta']['new_files'].items():
    assert sha(p) == digest
    pins[p] = digest
model_registry = V/'metadata/asset-import-registry.v1.json'
assert sha(model_registry) == index['controlled_vault_delta']['model_registry_sha256']
pins[str(model_registry)] = sha(model_registry)
for refusal in run['refusals']:
    assert not current[refusal['id']].get('preview')
pins[str(Path(__file__).resolve())] = sha(Path(__file__))
assert all(sha(p) == h for p,h in pins.items())
O.mkdir()
result = dict(status='22_upper_lod_source_previews_delivered_and_product_reader_verified',
              finished_utc=datetime.now(timezone.utc).isoformat(), cards_delivered=22, images_delivered=44,
              missing_primary_images_before=515, missing_primary_images_after=493,
              refused_cards=run['refusals'], current_native_reader_verified=True,
              source_license_readiness_unchanged=True, model_registry_unchanged=True,
              current_catalog_revision=registry['revision'], controlled_vault_delta_verified=True,
              full_vault_rescan=False, input_pins=pins, editor_visual_verified=False,
              full_import_increment=0, full_import=False)
(O/'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in {'input_pins','refused_cards'}}))
