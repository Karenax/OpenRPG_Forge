"""Seal the six admitted image pairs and retain two source parity refusals."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
V = Path('F:/OpenRPG_AssetVault')
O = W / 'pirate-source-thumbnail-batch-0113-0120-delivery-reopen-v179'
pins = {}

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def read(path):
    pins[str(path)] = sha(path)
    return json.loads(Path(path).read_text('utf-8-sig'))

def main():
    assert not O.exists()
    ip = W / 'pirate-source-thumbnail-batch-0113-0120-index-v177/RESULT.v1.json'
    rp = W / 'pirate-source-thumbnail-batch-0113-0120-native-reader-v178/RESULT.v1.json'
    vp = W / 'pirate-source-thumbnail-batch-0113-0120-review-v176/RESULT.v1.json'
    index = read(ip); reader = read(rp); review = read(vp)
    assert index['cards_updated'] == reader['cards_verified'] == review['cards_verified'] == 6
    assert index['images_installed'] == review['images_verified'] == 12
    assert review['cards_refused'] == len(review['refusals']) == 2
    assert reader['actual_exit_code'] == 0 and reader['status'] == 'reviewed_current_native_source_cards_search_filter_details_images_passed'
    assert index['other_cards_unchanged'] and index['source_license_and_readiness_unchanged']
    pointer = read(V / 'metadata/asset-source-support-registry.v1.json')
    assert pointer['revision'] == index['new_catalog_revision']
    path = V / pointer['path']; assert sha(path) == pointer['sha256']
    catalog = read(path)
    before_pointer = read(ip.parent / 'prior-registry.json')
    prior_path = V / before_pointer['path']; assert sha(prior_path) == before_pointer['sha256']
    prior = read(prior_path)
    old = {a['id']:a for a in prior['assets']}; new = {a['id']:a for a in catalog['assets']}
    ids = {c['id'] for c in index['cards']}
    assert len(ids) == 6 and set(old) == set(new)
    assert {i for i in old if old[i] != new[i]} == ids
    for i in ids:
        trim = lambda a: {k:v for k,v in a.items() if k not in {'names','descriptions','tags'} and not k.startswith('preview')}
        assert trim(old[i]) == trim(new[i])
    assert not ids & {r['id'] for r in review['refusals']}
    delta = index['controlled_vault_delta']
    assert sha(V / 'metadata/asset-import-registry.v1.json') == delta['model_registry_sha256']
    assert sha(V / 'metadata/asset-source-support-registry.v1.json') == delta['new_registry_sha256']
    for proof in [review,reader,read(ip.parent / 'PREPARED.v1.json')]:
        for p,h in proof['input_pins'].items():
            assert sha(p) == h, p
            pins[p] = h
    pins.update(index['preview_pins']);pins.update(delta['new_files'])
    pins[str(Path(__file__).resolve())] = sha(Path(__file__).resolve())
    for p,h in pins.items(): assert sha(p) == h, p
    missing_before = sum(not a.get('preview') for a in prior['assets'])
    missing_after = sum(not a.get('preview') for a in catalog['assets'])
    assert missing_before - missing_after == 6
    O.mkdir()
    result = dict(status='six_native_source_cards_images_indexed_and_product_reader_verified', finished_utc=datetime.now(timezone.utc).isoformat(), lot='L08.IMAGE.PIRATE.07', cards_updated=6, source_images_installed=12, cards_refused=2, source_parity_refusals=review['refusals'], missing_primary_images_before=missing_before, missing_primary_images_after=missing_after, source_cards=len(new), catalog_revision=pointer['revision'], search_pack_availability_filters_details_and_primary_image_verified=True, sources_licenses_readiness_and_other_cards_unchanged=True, vault_delta_rehashed=True, full_vault_rescan=False, agent_visual_review=True, human_review=False, actual_editor_visual_verified=False, complete_assets_delivered_increment=0, full_import=False, input_pins=pins)
    (O / 'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','source_parity_refusals']}))

if __name__ == '__main__': main()
