"""Prepare four source-graph pairs without discarding either logical model.

The established main card is retained as a deliberate choice; its materials
and the historical variant are not claimed to be visually equivalent.
"""
import hashlib
import json
from pathlib import Path

import vault_storage
from prepare_variant_metadata_transition import load_packs, prepare_transition
from prepare_variant_reference_patterns import vault_uuid
from variant_legacy_recipe import build_descriptor_recipe

ROOT = Path(r'F:\OpenRPG_AssetVault')
BASE = Path(r'E:\OpenRPG_Forge\.build\asset-conversion')
PACKS = ('pirate-island', 'mountain-temple', 'house-on-a-hill', 'mountain-ancient-mountain')


def main():
    index = ROOT / 'metadata/asset-import-registry.v1.json'
    before = index.read_bytes()
    active = {}
    for entry in json.loads(before)['packs'].values():
        path = vault_storage.confined(ROOT, ROOT / entry['path'] / 'details.v1.json')
        raw = path.read_bytes()
        if hashlib.sha256(raw).hexdigest() != entry['files']['details.v1.json']:
            raise ValueError('metadata SHA mismatch')
        for asset in json.loads(raw)['assets']:
            active[asset['id']] = asset
    inventory = json.loads((BASE / 'remaining-variant-pairs-v2-20261111.json').read_bytes())
    available = [x for x in inventory['pairs'] if x['comparison_current']
                 and x['mesh_payloads_exact'] == 'True' and x['images_exact'] == 'True'
                 and 'OPENRPG_source_graph' in x['main_private_extensions']
                 and 'OPENRPG_source_graph' in x['variant_private_extensions']]
    rows, recipes = [], {}
    for pack in PACKS:
        candidates = [x for x in available if x['main_id'].split('.')[1] == pack]
        for candidate in candidates:
            keep, old = active.get(candidate['main_id']), active.get(candidate['variant_id'])
            if not keep or not old or keep.get('consolidated_variants') or old.get('consolidated_variants'):
                continue
            if (keep.get('model_sha256') != candidate['main_sha'] or old.get('model_sha256') != candidate['variant_sha']
                    or keep.get('model_path') != candidate['main_path'] or old.get('model_path') != candidate['variant_path']):
                continue
            keep_path = vault_storage.confined(ROOT, Path(keep['model_path']))
            old_path = vault_storage.confined(ROOT, Path(old['model_path']))
            keep_desc = vault_storage.read_descriptor(ROOT, keep_path)
            old_desc = vault_storage.read_descriptor(ROOT, old_path)
            if (keep_desc is None or old_desc is None or keep_desc['bytes'] > 128 * 1024 * 1024
                    or old_desc['bytes'] > 128 * 1024 * 1024):
                continue
            if vault_storage.digest(ROOT, keep_path, 512 * 1024 * 1024) != candidate['main_sha']:
                raise ValueError('retained model SHA changed')
            recipe = build_descriptor_recipe(ROOT, old_path, candidate['variant_sha'])
            row = {'retained_id': candidate['main_id'], 'retired_id': candidate['variant_id'],
                   'retained_uuid': vault_uuid(candidate['main_id']), 'retired_uuid': vault_uuid(candidate['variant_id']),
                   'retained_sha256': candidate['main_sha'], 'retired_sha256': candidate['variant_sha'],
                   'retained_path': str(keep_path), 'retired_path': str(old_path),
                   'choice': 'existing main card; variant material graph retained in embedded descriptor',
                   'ready_for_removal': False}
            rows.append(row); recipes[row['retired_id']] = recipe
            break
        else:
            raise ValueError('no suitable pilot candidate for ' + pack)
    plan = {'schema': 'openrpg.variant-consolidation-plan.v1', 'state': 'planning',
            'registry_sha256': hashlib.sha256(before).hexdigest(), 'candidate_count': len(rows), 'candidates': rows}
    prepare_transition(load_packs(ROOT, plan), rows, recipes)
    if index.read_bytes() != before:
        raise ValueError('registry changed')
    for name, value in (('plan-graph-descriptor-pilot-4-20261111.json', plan),
                        ('recettes-graph-descriptor-pilot-4-20261111.json',
                         {'state': 'prepared_only', 'registry_sha256': plan['registry_sha256'], 'recipes': recipes})):
        with (BASE / name).open('x', encoding='utf8') as f:
            json.dump(value, f, ensure_ascii=False, indent=2); f.write('\n')
    print(json.dumps({'candidates': len(rows), 'packs': list(PACKS), 'retired_models_preserved_in_packs': True}))


if __name__ == '__main__':
    main()
