"""Prepare three tiny Witch House pairs with exact inline historical GLBs.

No publication or removal is performed. Runtime and visual parity are not claimed.
"""
import argparse
import hashlib
import json
from pathlib import Path

import vault_storage as storage
from audit_witch_source_choices import triangle_positions
from prepare_variant_metadata_transition import load_packs, prepare_transition, _index
from prepare_variant_reference_patterns import vault_uuid
from variant_legacy_recipe import MAX_INLINE_MODEL_BYTES, build_inline_recipe

GUID = 'abc00000000006059646869590748175'
ROOT = Path(r'F:\OpenRPG_ImportWork\hivemind-run\witch-house')
SOURCES = (
    (ROOT / 'source/Assets/Hivemind/WitchHouse/HDRP(Default)/Art/Prefabs/SM_PlantKnot.prefab', 'f7bb20c501ea4c91c81088cbc0345f2fe36f8514722b70feea03ba55782e8e3c'),
    (ROOT / 'variants/7a87221d80c1a122/source/Assets/Hivemind/WitchHouse/URP/Art/Prefabs/SM_PlantKnot.prefab', 'f7bb20c501ea4c91c81088cbc0345f2fe36f8514722b70feea03ba55782e8e3c'),
    (ROOT / 'source/Assets/Hivemind/WitchHouse/HDRP(Default)/Art/Meshes/Plants/SM_PlantKnot.fbx', '1e0982a69ffeba4674066113bdc5519c225c780dd5a161f3fefb7b924eaba7b7'),
    (ROOT / 'variants/7a87221d80c1a122/source/Assets/Hivemind/WitchHouse/URP/Art/Meshes/Plants/SM_PlantKnot.fbx', '1e0982a69ffeba4674066113bdc5519c225c780dd5a161f3fefb7b924eaba7b7'),
)


def plan(vault, source):
    vault = vault.resolve(strict=True)
    src = source.read_bytes()
    inv = json.loads(src)
    registry = storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
    before = registry.read_bytes()
    if storage.sha(before) != inv['registry_sha256']:
        raise ValueError('registry changed')
    rows = [row for row in inv['pairs'] if '.witch-house.' in row['main_id'] and GUID in row['main_id']]
    if len(rows) != 3:
        raise ValueError('expected exactly three plant LOD pairs')
    for path, pin in SOURCES:
        path = storage.confined(Path(path.anchor), path)
        if hashlib.sha256(path.read_bytes()).hexdigest() != pin:
            raise ValueError('source SHA changed')
    candidates, recipes = [], {}
    for row in rows:
        if not row.get('comparison_current') or row.get('transforms_exact') != 'True':
            raise ValueError('current transforms not established')
        paths = [storage.confined(vault, Path(row[k + '_path'])) for k in ('main', 'variant')]
        raw = [storage.read_bytes(vault, path, MAX_INLINE_MODEL_BYTES) for path in paths]
        if any(storage.sha(data) != row[k + '_sha'] for data, k in zip(raw, ('main', 'variant'))):
            raise ValueError('model SHA changed')
        if triangle_positions(raw[0]) != triangle_positions(raw[1]):
            raise ValueError('oriented position triangles differ')
        candidates.append({'retained_id': row['variant_id'], 'retired_id': row['main_id'],
            'retained_uuid': vault_uuid(row['variant_id']), 'retired_uuid': vault_uuid(row['main_id']),
            'retained_path': row['variant_path'], 'retired_path': row['main_path'],
            'retained_sha256': row['variant_sha'], 'retired_sha256': row['main_sha'],
            'choice': 'Keep reviewed URP with source-frame evidence; historical HDRP preserved inline',
            'preferred_controls': ['source_authored_frames', 'verified_source', 'oriented_triangles'],
            'ready_for_removal': False})
        recipes[row['main_id']] = build_inline_recipe(raw[0], row['main_sha'])
    result = {'schema': 'openrpg.variant-consolidation-plan.v1', 'registry_sha256': inv['registry_sha256'],
              'state': 'planning', 'candidate_count': len(candidates), 'candidates': candidates}
    packs = load_packs(vault, result)
    cards = _index(packs)
    pins = {}
    for item in candidates:
        selected = []
        for side in ('retained', 'retired'):
            _, card, _ = cards[item[side + '_id']]
            if not card.get('license') or card.get('readiness', {}).get('metadata_ready') is not True or card.get('missing'):
                raise ValueError('card or license incomplete')
            cat = storage.confined(vault, Path(card['source_catalog']))
            data = cat.read_bytes()
            pin = storage.sha(data)
            if cat in pins and pins[cat] != pin:
                raise ValueError('catalog changed')
            pins[cat] = pin
            entry = next(x for x in json.loads(data)['assets'] if x['id'] == card['id'])
            if (entry.get('sha256') != card['model_sha256'] or entry.get('geometry_roundtrip_passed') is not True
                    or entry.get('material_review_pending') is not False or entry.get('visual_review') != 'agent_reviewed'):
                raise ValueError('review or roundtrip evidence missing')
            selected.append((card, entry))
        if selected[0][1].get('source_authored_frames_preserved') is not True:
            raise ValueError('retained source frame evidence missing')
        if selected[0][0]['license'] != selected[1][0]['license']:
            raise ValueError('license differs')
    prepare_transition(packs, candidates, recipes)
    if registry.read_bytes() != before or source.read_bytes() != src:
        raise ValueError('input changed')
    for path, pin in pins.items():
        if storage.sha(path.read_bytes()) != pin:
            raise ValueError('catalog changed during planning')
    return result, {'state': 'prepared_only', 'registry_sha256': inv['registry_sha256'], 'recipes': recipes}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for n in ('vault', 'source', 'plan', 'recipes'):
        p.add_argument('--' + n, type=Path, required=True)
    args = p.parse_args()
    if args.plan.exists() or args.recipes.exists():
        p.error('output exists')
    result, recipes = plan(args.vault, args.source)
    for path, value in ((args.plan, result), (args.recipes, recipes)):
        with path.open('x', encoding='utf8') as out:
            json.dump(value, out, ensure_ascii=False, indent=2)
            out.write('\n')
    print(json.dumps({'state': 'planning_only', 'candidates': result['candidate_count'], 'models_removed': 0}))
