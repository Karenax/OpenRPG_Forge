"""Recheck current SHA pins and exact-reconstruction recipes for candidate pairs.

This is a read-only Vault audit. Identical binary chunks do not establish visual
equivalence of differing materials and do not authorize publication or removal.
"""
import argparse
import csv
import hashlib
import json
import struct
from pathlib import Path

import vault_storage
from prepare_variant_metadata_transition import load_packs, prepare_transition
from prepare_variant_reference_patterns import vault_uuid
from variant_legacy_recipe import build_recipe


def material_summary(raw):
    if len(raw) < 20 or raw[:4] != b'glTF':
        raise ValueError('invalid GLB')
    length = struct.unpack_from('<I', raw, 12)[0]
    if 20 + length > len(raw):
        raise ValueError('truncated GLB JSON')
    doc = json.loads(raw[20:20 + length])
    return [{'name': m.get('name'), 'alpha_mode': m.get('alphaMode', 'OPAQUE'),
             'alpha_cutoff': m.get('alphaCutoff', 0.5), 'double_sided': m.get('doubleSided', False),
             'source_program_sha256': m.get('extensions', {}).get('OPENRPG_source_fragment', {}).get('source_program_sha256')}
            for m in doc.get('materials', [])]


def select(evidence, pairs, pin):
    rows = []
    seen = set()
    for entry in evidence:
        if entry.get('status') != 'compared' or entry.get('binary_chunk_exact') is not True:
            continue
        row = pairs[entry['id']]
        if entry['main_path'] != row['main_path'] or entry['variant_path'] != row['variant_path']:
            raise ValueError('mismatched audit paths')
        keep, retired = row['main_id'], row['variant_id']
        if keep == retired or keep in seen or retired in seen:
            raise ValueError('overlapping identities')
        seen.update((keep, retired))
        rows.append({'retained_id': keep, 'retired_id': retired,
                     'retained_uuid': vault_uuid(keep), 'retired_uuid': vault_uuid(retired),
                     'retained_path': row['main_path'], 'retired_path': row['variant_path'],
                     'retained_sha256': row['main_sha'], 'retired_sha256': row['variant_sha'],
                     'differences': entry['json_difference_paths'],
                     'differences_truncated': entry['difference_paths_truncated'],
                     'ready_for_removal': False})
    if not rows:
        raise ValueError('no eligible evidence')
    return {'schema': 'openrpg.variant-consolidation-plan.v1', 'state': 'audit_only',
            'registry_sha256': pin, 'candidate_count': len(rows), 'candidates': rows}


def audit(vault, report, output, plan_output=None, recipe_output=None):
    if (plan_output is None) != (recipe_output is None):
        raise ValueError('plan and recipe outputs must be supplied together')
    if any(p is not None and p.exists() for p in (plan_output, recipe_output)):
        raise ValueError('plan or recipe output already exists')
    vault = vault.resolve(strict=True)
    registry = vault / 'metadata/asset-import-registry.v1.json'
    before = registry.read_bytes()
    with (report / 'variantes-paires.csv').open(encoding='utf-8-sig', newline='') as stream:
        pairs = {r['id']: r for r in csv.DictReader(stream)}
    evidence = [json.loads(line) for line in (report / 'extensions-privees-ecarts-glb.jsonl').read_text(encoding='utf8').splitlines() if line]
    plan = select(evidence, pairs, hashlib.sha256(before).hexdigest())
    prepare_transition(load_packs(vault, plan), plan['candidates'])
    counts = {'verified': 0, 'refused': 0}
    recipes = {}
    with output.open('x', encoding='utf8', newline='\n') as stream:
        for i, item in enumerate(plan['candidates'], 1):
            result = {'retained_id': item['retained_id'], 'retired_id': item['retired_id']}
            try:
                a = vault_storage.confined(vault, Path(item['retained_path']))
                b = vault_storage.confined(vault, Path(item['retired_path']))
                first = vault_storage.read_bytes(vault, a, 512 * 1024 * 1024)
                second = vault_storage.read_bytes(vault, b, 512 * 1024 * 1024)
                recipe = build_recipe(first, second, item['retained_sha256'], item['retired_sha256'])
                if recipe_output is not None:
                    recipes[item['retired_id']] = recipe
                result.update(state='verified_exact_reconstruction',
                              logical_bytes=len(second), prefix_bytes=len(recipe['prefix_hex']) // 2,
                              retained_materials=material_summary(first),
                              retired_materials=material_summary(second),
                              differences=item['differences'],
                              differences_truncated=item['differences_truncated'])
                counts['verified'] += 1
            except (OSError, ValueError) as error:
                result.update(state='refused', reason=str(error))
                counts['refused'] += 1
            stream.write(json.dumps(result, ensure_ascii=False) + '\n')
            stream.flush()
            if i % 25 == 0:
                print(f"audited {i}/{len(plan['candidates'])}: {counts}", flush=True)
        if registry.read_bytes() != before:
            raise ValueError('registry changed; audit cannot be attributed to a stable snapshot')
        stream.write(json.dumps({'state': 'finished', 'registry_sha256': plan['registry_sha256'],
                                 'counts': counts}, ensure_ascii=False) + '\n')
    if recipe_output is not None and not counts['refused']:
        with plan_output.open('x', encoding='utf8', newline='\n') as stream:
            json.dump(plan, stream, ensure_ascii=False, indent=2)
            stream.write('\n')
        with recipe_output.open('x', encoding='utf8', newline='\n') as stream:
            json.dump({'state': 'prepared_only', 'registry_sha256': plan['registry_sha256'],
                       'recipes': recipes}, stream, ensure_ascii=False)
            stream.write('\n')
    return counts


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', type=Path, required=True)
    parser.add_argument('--report', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--plan-output', type=Path)
    parser.add_argument('--recipes-output', type=Path)
    args = parser.parse_args()
    print(json.dumps(audit(args.vault, args.report, args.output, args.plan_output, args.recipes_output)))
