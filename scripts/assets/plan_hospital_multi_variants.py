"""Plan current Hospital multi-version groups without publishing or deleting.

Choose the existing HDRP main, or the URP card if no main remains. Historical
material and image bytes remain available through verified packed descriptors.
Recorded mesh/transform equality does not establish visual equivalence.
"""
import argparse
import csv
import hashlib
import json
import os
from pathlib import Path

import vault_storage
from prepare_variant_metadata_transition import load_packs, prepare_transition
from prepare_variant_reference_patterns import vault_uuid
from variant_legacy_recipe import build_descriptor_recipe, build_recipe
from audit_witch_source_choices import triangle_positions


def plan(vault, groups_file, comparison_file, registry_sha, excluded, allow_oriented_triangles=False,
         allow_raw_shared_tail=False):
    vault = vault.resolve(strict=True)
    registry_path = vault_storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
    before = registry_path.read_bytes()
    if hashlib.sha256(before).hexdigest() != registry_sha:
        raise ValueError('registry changed')
    active = {}
    for entry in json.loads(before)['packs'].values():
        path = vault_storage.confined(vault, vault / entry['path'] / 'details.v1.json')
        raw = path.read_bytes()
        if hashlib.sha256(raw).hexdigest() != entry['files']['details.v1.json']:
            raise ValueError('active metadata SHA changed')
        for asset in json.loads(raw)['assets']:
            if asset['id'] in active:
                raise ValueError('duplicate active identity')
            active[asset['id']] = asset
    with groups_file.open(encoding='utf-8-sig', newline='') as stream:
        groups = list(csv.DictReader(stream))
    with comparison_file.open(encoding='utf8') as stream:
        comparisons = {x['id']: x for x in map(json.loads, stream) if x}
    rows, recipes, refused = [], {}, []
    for group in groups:
        if group['base_id'] in excluded:
            continue
        if int(group['main_count']) + int(group['variant_count']) < 2:
            continue
        try:
            records = json.loads(group['records_json'])
            if any(len(x) != 5 for x in records) or len({x[0] for x in records}) != len(records):
                raise ValueError('invalid group identities')
            for ident, _, path, sha, _ in records:
                asset = active.get(ident)
                if not asset or asset.get('model_path') != path or asset.get('model_sha256') != sha:
                    raise ValueError('active metadata changed')
            main = [x for x in records if '.variant-' not in x[0]]
            if not main:
                main = [x for x in records if x[4] == 'urp']
            if len(main) != 1:
                raise ValueError('no unique main or URP card')
            keep = main[0]
            others = [x for x in records if x[0] != keep[0]]
            if any(active[x[0]].get('consolidated_variants') for x in others):
                raise ValueError('retired candidate has pre-existing aliases')
            keep_path = vault_storage.confined(vault, Path(keep[2]))
            if vault_storage.digest(vault, keep_path, 512 * 1024 * 1024) != keep[3]:
                raise ValueError('retained model SHA changed')
            group_rows, group_recipes = [], {}
            for old in others:
                comp = comparisons.get(keep[0] + '::' + old[0])
                reversed_order = False
                if comp is None:
                    comp = comparisons.get(old[0] + '::' + keep[0])
                    reversed_order = True
                expected = (old[3], keep[3]) if reversed_order else (keep[3], old[3])
                if (not comp or comp.get('status') != 'compared' or not comp.get('transforms_exact')
                        or (comp.get('main_sha'), comp.get('variant_sha')) != expected):
                    raise ValueError('historical mesh/transform comparison absent or mismatched')
                old_path = vault_storage.confined(vault, Path(old[2]))
                if not comp.get('mesh_payloads_exact'):
                    if not allow_oriented_triangles:
                        raise ValueError('mesh bytes differ')
                    if (not active[keep[0]].get('license') or active[keep[0]].get('license') != active[old[0]].get('license')):
                        raise ValueError('license differs or missing')
                    a, b = (triangle_positions(b''.join(vault_storage.chunks(vault, path))) for path in (keep_path, old_path))
                    if a != b:
                        raise ValueError('oriented position triangles differ')
                if allow_raw_shared_tail and vault_storage.read_descriptor(vault, old_path) is None:
                    retained_bytes = vault_storage.read_bytes(vault, keep_path, 512 * 1024 * 1024)
                    old_bytes = vault_storage.read_bytes(vault, old_path, 512 * 1024 * 1024)
                    recipe = build_recipe(retained_bytes, old_bytes, keep[3], old[3])
                else:
                    recipe = build_descriptor_recipe(vault, old_path, old[3])
                group_rows.append({'retained_id': keep[0], 'retired_id': old[0],
                    'retained_uuid': vault_uuid(keep[0]), 'retired_uuid': vault_uuid(old[0]),
                    'retained_path': str(keep_path), 'retired_path': str(old_path),
                    'retained_sha256': keep[3], 'retired_sha256': old[3],
                    'choice': 'existing main or URP card; historical material bytes preserved; visual parity not established',
                    'ready_for_removal': False})
                group_recipes[old[0]] = recipe
            rows.extend(group_rows)
            recipes.update(group_recipes)
        except (OSError, ValueError, KeyError) as error:
            refused.append({'base_id': group['base_id'], 'reason': str(error)})
        if (len(rows) + len(refused)) % 25 == 0:
            print(f"prepared {len(rows)} historical variants, {len(refused)} groups refused", flush=True)
    if registry_path.read_bytes() != before:
        raise ValueError('registry changed during planning')
    result = {'schema': 'openrpg.variant-consolidation-plan.v1', 'state': 'planning',
              'registry_sha256': registry_sha, 'candidate_count': len(rows), 'candidates': rows,
              'refused_groups': refused, 'excluded_base_ids': sorted(excluded)}
    if rows:
        prepare_transition(load_packs(vault, result), rows, recipes)
    return result, {'state': 'prepared_only', 'registry_sha256': registry_sha, 'recipes': recipes}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', type=Path, required=True)
    p.add_argument('--groups', type=Path, required=True)
    p.add_argument('--comparisons', type=Path, required=True)
    p.add_argument('--registry-sha', required=True)
    p.add_argument('--exclude-plan', type=Path, required=True)
    p.add_argument('--plan-output', type=Path, required=True)
    p.add_argument('--recipes-output', type=Path, required=True)
    args = p.parse_args()
    if args.plan_output.exists() or args.recipes_output.exists():
        raise ValueError('output exists')
    old = json.loads(args.exclude_plan.read_bytes())
    if old.get('registry_sha256') != args.registry_sha:
        raise ValueError('pilot registry differs')
    excluded = {x['retained_id'] for x in old['candidates']}
    result, recipes = plan(args.vault, args.groups, args.comparisons, args.registry_sha, excluded)
    for path, data in ((args.plan_output, result), (args.recipes_output, recipes)):
        with path.open('x', encoding='utf8', newline='\n') as stream:
            json.dump(data, stream, ensure_ascii=False, indent=2)
            stream.write('\n')
    print(json.dumps({'candidates': len(result['candidates']), 'refused_groups': len(result['refused_groups']),
                      'state': 'planning', 'removed': 0}))
