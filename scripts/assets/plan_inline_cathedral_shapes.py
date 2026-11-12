"""Prepare three Cathedral shapes with collider-bearing versions retained."""
import argparse
import json
import struct
from pathlib import Path
import vault_storage as storage
from audit_witch_source_choices import triangle_positions
from prepare_variant_metadata_transition import load_packs, prepare_transition, _index
from prepare_variant_reference_patterns import vault_uuid
from variant_legacy_recipe import MAX_INLINE_MODEL_BYTES, build_inline_recipe

HDRP = Path(r'F:\OpenRPG_ImportWork\hivemind-run\modular-gothic-cathedral\source')
GUIDS = {'abc00000000016807694666305441489', 'abc00000000012983052241087814100',
         'abc00000000001497478416529335407'}


def doc(raw):
    return json.loads(raw[20:20 + struct.unpack_from('<I', raw, 12)[0]])


def plan(vault, source):
    vault = vault.resolve(strict=True)
    src = source.read_bytes(); inv = json.loads(src)
    registry = storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
    before = registry.read_bytes()
    if storage.sha(before) != inv['registry_sha256']:
        raise ValueError('registry changed')
    rows = [row for row in inv['pairs'] if '.modular-gothic-cathedral.' in row['main_id']]
    if len(rows) != 3 or {next((g for g in GUIDS if g in row['main_id']), None) for row in rows} != GUIDS:
        raise ValueError('expected the three specific shapes')
    candidates, recipes = [], {}
    for row in rows:
        if not row.get('comparison_current'):
            raise ValueError('comparison stale')
        raw = [storage.read_bytes(vault, storage.confined(vault, Path(row[k+'_path'])), MAX_INLINE_MODEL_BYTES)
               for k in ('main', 'variant')]
        if any(storage.sha(data) != row[k+'_sha'] for data, k in zip(raw, ('main', 'variant'))):
            raise ValueError('model SHA changed')
        if triangle_positions(raw[0]) != triangle_positions(raw[1]):
            raise ValueError('oriented triangles differ')
        main, variant = (doc(data) for data in raw)
        transforms = lambda d: [{k: n[k] for k in ('matrix', 'rotation', 'scale', 'translation') if k in n}
                                for n in d.get('nodes', []) if 'mesh' in n]
        if not transforms(main) or transforms(main) != transforms(variant):
            raise ValueError('mesh transforms differ')
        colliders = [n.get('extras', {}).get('unity_box_collider') for n in variant.get('nodes', [])]
        if len([x for x in colliders if isinstance(x, dict) and x.get('source_declared') is True]) != 1:
            raise ValueError('retained Unity collider evidence missing')
        candidates.append({'retained_id': row['variant_id'], 'retired_id': row['main_id'],
            'retained_uuid': vault_uuid(row['variant_id']), 'retired_uuid': vault_uuid(row['main_id']),
            'retained_path': row['variant_path'], 'retired_path': row['main_path'],
            'retained_sha256': row['variant_sha'], 'retired_sha256': row['main_sha'],
            'choice': 'Keep URP with declared collider; preserve reviewed HDRP inline',
            'preferred_controls': ['source_collider', 'verified_sources', 'oriented_triangles'],
            'ready_for_removal': False})
        recipes[row['main_id']] = build_inline_recipe(raw[0], row['main_sha'])
    result = {'schema': 'openrpg.variant-consolidation-plan.v1', 'registry_sha256': inv['registry_sha256'],
              'state': 'planning', 'candidate_count': 3, 'candidates': candidates}
    packs = load_packs(vault, result); cards = _index(packs); catalogs = {}
    for item in candidates:
        _, retained, _ = cards[item['retained_id']]; _, retired, _ = cards[item['retired_id']]
        if (not retained.get('license') or retained['license'] != retired.get('license')
                or any(card.get('missing') or card.get('readiness', {}).get('metadata_ready') is not True
                       for card in (retained, retired))):
            raise ValueError('license or metadata incomplete')
        old_prefab, old_fbx = retired['source_prefab'], retired['source_fbx'][0]
        if old_prefab['guid'] not in GUIDS:
            raise ValueError('source GUID differs')
        for key, evidence in (('source_prefab', old_prefab), ('source_fbx', old_fbx)):
            primary = storage.confined(Path(HDRP.anchor), HDRP / evidence['path'])
            alternate = storage.confined(vault, Path(retained[key]))
            if (storage.sha(primary.read_bytes()) != evidence['sha256']
                    or storage.sha(alternate.read_bytes()) != evidence['sha256']):
                raise ValueError('source SHA differs')
        for card in (retained, retired):
            path = storage.confined(vault, Path(card['source_catalog'])); raw = path.read_bytes(); pin = storage.sha(raw)
            if path in catalogs and catalogs[path] != pin:
                raise ValueError('catalog changed')
            catalogs[path] = pin
            entry = next(x for x in json.loads(raw)['assets'] if x['id'] == card['id'])
            if entry.get('sha256') != card['model_sha256']:
                raise ValueError('catalog model pin differs')
    prepare_transition(packs, candidates, recipes)
    if registry.read_bytes() != before or source.read_bytes() != src or any(storage.sha(p.read_bytes()) != h for p,h in catalogs.items()):
        raise ValueError('input changed')
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
            json.dump(value, out, ensure_ascii=False, indent=2); out.write('\n')
    print(json.dumps({'state': 'planning_only', 'candidates': 3, 'models_removed': 0}))
