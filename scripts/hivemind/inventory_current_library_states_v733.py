"""Hash current immutable metadata and count declared states without promoting them."""
from pathlib import Path
from datetime import datetime, timezone
from collections import Counter
import json
import hashlib

V = Path('F:/OpenRPG_AssetVault')
O = Path('F:/OpenRPG_ImportWork/library-current-state-inventory-v733')

def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def main():
    assert not O.exists()
    pins = {}
    def read(path, expected=None):
        actual = sha(path)
        assert expected is None or actual == expected, path
        pins[str(path)] = actual
        return json.loads(path.read_text('utf-8-sig'))
    models_registry = read(V / 'metadata/asset-import-registry.v1.json')
    source_registry = read(V / 'metadata/asset-source-support-registry.v1.json')
    sources = read(V / source_registry['path'], source_registry['sha256'])['assets']
    models = []
    packs = []
    for pack, entry in models_registry['packs'].items():
        path = V / entry['path']
        data = read(path / 'details.v1.json', entry['files']['details.v1.json'])
        for filename, digest in entry['files'].items():
            if filename != 'details.v1.json': read(path / filename, digest)
        assert data['pack_id'] == pack
        models.extend(data['assets'])
        packs.append(dict(pack_id=pack, declared_models=len(data['assets']), revision=entry['revision']))
    ids = [a['id'] for a in models + sources]
    assert len(ids) == len(set(ids)), 'Duplicate library identities'
    def counts(rows):
        flags = Counter()
        for row in rows:
            for key, value in row.get('readiness', {}).items():
                flags[f'{key}:{str(value).lower()}'] += 1
        return dict(flags)
    missing = Counter(a['pack_id'] for a in sources if not a.get('preview'))
    result = dict(status='current_library_metadata_hashes_and_declared_states_reopened', finished_utc=datetime.now(timezone.utc).isoformat(), model_cards=len(models), source_cards=len(sources), total_unique_card_ids=len(ids), logical_object_total_known=False, model_pack_variants=len(packs), source_cards_missing_primary_preview=sum(missing.values()), missing_previews_by_pack=dict(sorted(missing.items())), model_declared_readiness=counts(models), source_declared_readiness=counts(sources), model_packs=packs, input_pins=pins, metadata_states_are_not_runtime_proofs=True, all_model_files_rehashed=False, studio_gui_verified=False, full_import=False)
    for path, digest in pins.items(): assert sha(Path(path)) == digest, path
    O.mkdir(); output = O / 'RESULT.v1.json'; output.write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','model_packs','model_declared_readiness','source_declared_readiness','missing_previews_by_pack']}))

if __name__ == '__main__': main()
