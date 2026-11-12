"""Prepare reversible consolidation for exact-byte duplicate weapon cards."""
import hashlib
import json
from pathlib import Path

import vault_storage
from prepare_variant_metadata_transition import load_packs, prepare_transition
from prepare_variant_reference_patterns import vault_uuid
from variant_legacy_recipe import build_recipe

ROOT = Path(r'F:\OpenRPG_AssetVault')
BASE = Path(r'E:\OpenRPG_Forge\.build\asset-conversion')


def main():
    registry = ROOT / 'metadata/asset-import-registry.v1.json'
    before = registry.read_bytes()
    groups = json.loads((BASE / 'active-exact-model-groups-20261111.json').read_bytes())
    if groups['registry_sha256'] != hashlib.sha256(before).hexdigest() or len(groups['groups']) != 17:
        raise ValueError('exact-model inventory stale or unexpected')
    active = {}
    for pack, entry in json.loads(before)['packs'].items():
        path = vault_storage.confined(ROOT, ROOT / entry['path'] / 'details.v1.json')
        raw = path.read_bytes()
        if hashlib.sha256(raw).hexdigest() != entry['files']['details.v1.json']:
            raise ValueError('metadata SHA mismatch')
        for asset in json.loads(raw)['assets']:
            active[asset['id']] = asset
    rows, recipes, previous_aliases = [], {}, 0
    for group in groups['groups']:
        cards = group['cards']
        if len(cards) != 2 or any(not c['id'].startswith('hivemind.dark-fantasy-weapons.') for c in cards):
            raise ValueError('unexpected group')
        keep = next((c for c in cards if '.prefab-' in c['id']), None)
        if keep is None:
            raise ValueError('canonical prefab missing')
        retired = next(c for c in cards if c is not keep)
        a, b = active[keep['id']], active[retired['id']]
        if a['model_sha256'] != b['model_sha256'] or a['model_sha256'] != group['sha256']:
            raise ValueError('active model SHA mismatch')
        if b.get('consolidated_variants'):
            raise ValueError('retired card has historical variants')
        previous_aliases += len(a.get('consolidated_variants', []))
        raw_a = vault_storage.read_bytes(ROOT, vault_storage.confined(ROOT, Path(a['model_path'])), 512 * 1024 * 1024)
        raw_b = vault_storage.read_bytes(ROOT, vault_storage.confined(ROOT, Path(b['model_path'])), 512 * 1024 * 1024)
        if raw_a != raw_b:
            raise ValueError('models are not byte identical')
        recipes[retired['id']] = build_recipe(raw_a, raw_b, group['sha256'], group['sha256'])
        rows.append({'retained_id': keep['id'], 'retired_id': retired['id'],
                     'retained_uuid': vault_uuid(keep['id']), 'retired_uuid': vault_uuid(retired['id']),
                     'retained_sha256': group['sha256'], 'retired_sha256': group['sha256'],
                     'retained_path': a['model_path'], 'retired_path': b['model_path'], 'ready_for_removal': False})
    plan = {'schema': 'openrpg.variant-consolidation-plan.v1', 'state': 'planning',
            'registry_sha256': groups['registry_sha256'], 'candidate_count': len(rows), 'candidates': rows}
    prepare_transition(load_packs(ROOT, plan), rows, recipes)
    if registry.read_bytes() != before:
        raise ValueError('registry changed')
    for name, value in (('plan-exact-armes-17-20261111.json', plan),
                        ('recettes-exact-armes-17-20261111.json',
                         {'state': 'prepared_only', 'registry_sha256': plan['registry_sha256'], 'recipes': recipes})):
        with (BASE / name).open('x', encoding='utf8') as f:
            json.dump(value, f, ensure_ascii=False, indent=2); f.write('\n')
    print(json.dumps({'candidates': len(rows), 'existing_aliases_preserved': previous_aliases}))


if __name__ == '__main__':
    main()
