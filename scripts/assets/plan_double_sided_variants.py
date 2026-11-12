"""Choose double-sided versions of exact-binary pairs; keep legacy bytes reconstructible.

The choice adds backface visibility and is not a claim of visual equivalence.
No Vault files are modified by this planner.
"""
import collections
import copy
import csv
import hashlib
import json
import re
import struct
from pathlib import Path

import vault_storage
from audit_variant_exact_candidates import diffs
from plan_metadata_only_variants import metadata_only, verify_documents
from prepare_variant_metadata_transition import load_packs, prepare_transition
from prepare_variant_reference_patterns import vault_uuid
from variant_legacy_recipe import build_recipe

ROOT = Path(r'F:\OpenRPG_AssetVault')
BASE = Path(r'E:\OpenRPG_Forge\.build\asset-conversion')
REPORTS = Path(r'E:\OpenRPG_Forge\Preuves-et-analyses\rapports\audit-variantes-hdrp-urp-20261110')
AUDITS = ('townsmith-double-sided-audit-146-20261111.json',
          'audit-blocs-villa-forge-provenance-v2-20261111.json',
          'audit-blocs-modular-cave-provenance-v2-20261111.json',
          'audit-blocs-witch-house-20261111.json')


def choose(a, b, expected):
    if list(diffs(a, b)) != expected:
        raise ValueError('current JSON differs from audit')
    pattern = re.compile(r'\$/materials/(\d+)/doubleSided(?: \(missing\))?')
    positions = {int(m.group(1)) for p in expected if (m := pattern.fullmatch(p))}
    if not positions:
        raise ValueError('no double-sided difference')
    directions = set()
    adapted = copy.deepcopy(b)
    for i in positions:
        left, right = a['materials'][i].get('doubleSided', False), b['materials'][i].get('doubleSided', False)
        if type(left) is not bool or type(right) is not bool or left == right:
            raise ValueError('double-sided values not opposite')
        directions.add('main' if left else 'variant')
        if 'doubleSided' in a['materials'][i]:
            adapted['materials'][i]['doubleSided'] = a['materials'][i]['doubleSided']
        else:
            adapted['materials'][i].pop('doubleSided', None)
    if len(directions) != 1:
        raise ValueError('neither version has all double-sided materials')
    rest = [p for p in expected if not pattern.fullmatch(p)]
    if rest:
        if not metadata_only({'binary_chunk_exact': True, 'truncated': False, 'differences': rest}):
            raise ValueError('other rendering or unknown metadata differs')
        verify_documents(a, adapted, rest)
    elif a != adapted:
        raise ValueError('unexplained JSON difference')
    return directions.pop()


def document(raw):
    if raw[:4] != b'glTF' or len(raw) < 28:
        raise ValueError('invalid GLB')
    size = struct.unpack_from('<I', raw, 12)[0]
    return vault_storage.decode_json(raw[20:20 + size])


def main():
    registry = ROOT / 'metadata/asset-import-registry.v1.json'
    before = registry.read_bytes()
    active = {}
    for entry in json.loads(before)['packs'].values():
        raw = (ROOT / entry['path'] / 'details.v1.json').read_bytes()
        if hashlib.sha256(raw).hexdigest() != entry['files']['details.v1.json']:
            raise ValueError('active metadata SHA mismatch')
        for asset in json.loads(raw)['assets']:
            active[asset['id']] = asset
    with (REPORTS / 'variantes-paires.csv').open(encoding='utf-8-sig', newline='') as f:
        pairs = {x['id']: x for x in csv.DictReader(f)}
    selected = {}
    for filename in AUDITS:
        for item in json.loads((REPORTS / filename).read_bytes())['results']:
            if (item.get('binary_chunk_exact') is True and item.get('truncated') is False
                    and any('/doubleSided' in p for p in item.get('differences', []))):
                selected[item['id']] = item
    rows, recipes, refused = [], {}, []
    for i, (identity, audit) in enumerate(sorted(selected.items()), 1):
        pair = pairs[identity]
        try:
            docs, raws = [], []
            for side in ('main', 'variant'):
                asset = active.get(pair[side + '_id'])
                if not asset or asset.get('model_path') != pair[side + '_path'] or asset.get('model_sha256') != pair[side + '_sha']:
                    raise ValueError('asset inactive or changed')
                raw = vault_storage.read_bytes(ROOT, vault_storage.confined(ROOT, Path(asset['model_path'])), 512 * 1024 * 1024)
                if vault_storage.sha(raw) != pair[side + '_sha']:
                    raise ValueError('logical SHA changed')
                raws.append(raw); docs.append(document(raw))
            direction = choose(docs[0], docs[1], audit['differences'])
            keep, retired = (('main', 'variant') if direction == 'main' else ('variant', 'main'))
            ia, ib = (0, 1) if direction == 'main' else (1, 0)
            if active[pair[retired + '_id']].get('consolidated_variants'):
                raise ValueError('retired card has historical aliases')
            recipe = build_recipe(raws[ia], raws[ib], pair[keep + '_sha'], pair[retired + '_sha'])
            row = {'retained_id': pair[keep + '_id'], 'retired_id': pair[retired + '_id'],
                   'retained_uuid': vault_uuid(pair[keep + '_id']), 'retired_uuid': vault_uuid(pair[retired + '_id']),
                   'retained_sha256': pair[keep + '_sha'], 'retired_sha256': pair[retired + '_sha'],
                   'retained_path': pair[keep + '_path'], 'retired_path': pair[retired + '_path'],
                   'choice': 'double-sided version; historical rendering reconstructed by legacy reference',
                   'ready_for_removal': False}
            rows.append(row); recipes[row['retired_id']] = recipe
        except (ValueError, KeyError, OSError) as exc:
            refused.append({'id': identity, 'reason': str(exc)})
        if i % 25 == 0:
            print(f'checked {i}/{len(selected)} pairs; {len(rows)} choices', flush=True)
    if registry.read_bytes() != before:
        raise ValueError('registry changed')
    plan = {'schema': 'openrpg.variant-consolidation-plan.v1', 'state': 'planning',
            'registry_sha256': hashlib.sha256(before).hexdigest(), 'candidate_count': len(rows), 'candidates': rows}
    if rows:
        prepare_transition(load_packs(ROOT, plan), rows, recipes)
    for name, data in (('plan-doubleface-20261111.json', plan),
                       ('recettes-doubleface-20261111.json', {'state': 'prepared_only', 'registry_sha256': plan['registry_sha256'], 'recipes': recipes}),
                       ('refus-doubleface-20261111.json', {'refused': refused})):
        with (BASE / name).open('x', encoding='utf8') as f:
            json.dump(data, f, ensure_ascii=False, indent=2); f.write('\n')
    print(json.dumps({'choices': len(rows), 'refused': len(refused),
                      'reasons': dict(collections.Counter(x['reason'] for x in refused))}))


if __name__ == '__main__':
    main()
