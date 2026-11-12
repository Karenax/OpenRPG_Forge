"""Read and hash a bounded candidate batch, retaining resumable evidence on E:.

This does not make candidates equivalent, publish metadata, or remove files.
Both models' logical SHA-256 values are rechecked before a recipe is recorded.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path

import vault_storage
from variant_legacy_recipe import build_descriptor_recipe, MAX_CONSOLIDATION_MODEL_BYTES


def audit(vault, source, candidates, output, pack, limit, offset=0, choices=None):
    vault = vault.resolve(strict=True)
    src_raw, candidates_raw = source.read_bytes(), candidates.read_bytes()
    inventory = json.loads(src_raw)
    candidate_doc = json.loads(candidates_raw)
    if candidate_doc['source_sha256'] != hashlib.sha256(src_raw).hexdigest():
        raise ValueError('candidate inventory and source differ')
    registry = vault_storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
    before = registry.read_bytes()
    if hashlib.sha256(before).hexdigest() != inventory['registry_sha256']:
        raise ValueError('registry changed; refresh source inventory')
    eligible = {x['variant_id']: x for x in candidate_doc['candidates']
                if x['main']['storage'] in ('raw', 'descriptor')
                and x['variant']['storage'] in ('raw', 'descriptor')
                and (x['main']['storage'] == 'raw' or x['main']['declared_pin_matches'])
                and (x['variant']['storage'] == 'raw' or x['variant']['declared_pin_matches'])
                and x['main']['logical_bytes'] <= MAX_CONSOLIDATION_MODEL_BYTES
                and x['variant']['logical_bytes'] <= MAX_CONSOLIDATION_MODEL_BYTES}
    choice_raw, selection = None, None
    if choices is not None:
        choice_raw = choices.read_bytes()
        doc = json.loads(choice_raw)
        if (doc.get('schema') != 'openrpg.material-choice-audit.v1' or doc.get('state') != 'audit_only'
                or doc.get('source_sha256') != hashlib.sha256(src_raw).hexdigest()
                or doc.get('candidate_inventory_sha256') != hashlib.sha256(candidates_raw).hexdigest()
                or doc.get('registry_sha256') != inventory['registry_sha256'] or doc.get('pack') != pack):
            raise ValueError('material choices are stale or mismatched')
        selected = [x for x in doc['results'] if x.get('state') == 'candidate_only']
        selection = {x['variant_id']: x for x in selected}
        if len(selection) != len(selected):
            raise ValueError('duplicate material choice')
    if offset < 0 or limit < 1:
        raise ValueError('invalid batch offset or limit')
    rows = [x for x in inventory['pairs'] if x['variant_id'] in eligible
            and x['main_id'].split('.')[1] == pack
            and (selection is not None or not eligible[x['variant_id']]['variant_has_aliases'])
            and (selection is None or x['variant_id'] in selection)
            and eligible[x['variant_id']]['variant' if selection is None
                         or selection[x['variant_id']].get('keep_side') == 'main'
                         else 'main']['storage'] == 'descriptor'][offset:offset + limit]
    if not rows:
        raise ValueError('no eligible candidates selected')
    header = {'schema': 'openrpg.descriptor-candidate-batch.v1', 'state': 'started',
              'registry_sha256': inventory['registry_sha256'],
              'source_sha256': hashlib.sha256(src_raw).hexdigest(),
              'candidate_inventory_sha256': hashlib.sha256(candidates_raw).hexdigest(),
              'pack': pack, 'offset': offset, 'planned': len(rows),
              'material_choices_sha256': hashlib.sha256(choice_raw).hexdigest() if choice_raw else None}
    with output.open('x', encoding='utf8', newline='\n') as log:
        def write(record):
            log.write(json.dumps(record, ensure_ascii=False, separators=(',', ':')) + '\n')
            log.flush()
            os.fsync(log.fileno())
        write(header)
        for index, row in enumerate(rows, 1):
            if registry.read_bytes() != before:
                raise ValueError('registry changed while auditing; partial journal retained')
            selected = selection[row['variant_id']] if selection is not None else {'keep_side': 'main'}
            side = selected.get('keep_side')
            if side not in ('main', 'variant') or selected.get('main_id', row['main_id']) != row['main_id']:
                raise ValueError('invalid material choice')
            old_side = 'variant' if side == 'main' else 'main'
            if (eligible[row['variant_id']][old_side]['storage'] != 'descriptor'
                    or not eligible[row['variant_id']][old_side]['declared_pin_matches']):
                raise ValueError('retired model must be a pinned descriptor')
            if eligible[row['variant_id']][old_side + '_has_aliases']:
                raise ValueError('retired card has pre-existing aliases')
            keep = vault_storage.confined(vault, Path(row[side + '_path']))
            old = vault_storage.confined(vault, Path(row[old_side + '_path']))
            if keep == old:
                raise ValueError('same path cannot be classified as distinct models')
            if vault_storage.digest(vault, keep, MAX_CONSOLIDATION_MODEL_BYTES) != row[side + '_sha']:
                raise ValueError('retained logical model SHA changed')
            recipe = build_descriptor_recipe(vault, old, row[old_side + '_sha'])
            write({'state': 'verified_model_bytes', 'main_id': row['main_id'],
                   'variant_id': row['variant_id'], 'main_sha256': row['main_sha'],
                   'variant_sha256': row['variant_sha'], 'retained_side': side,
                   'preferred_controls': selected.get('preferred_controls', []), 'recipe': recipe})
            print(f'verified {index}/{len(rows)} in {pack}', flush=True)
        if (registry.read_bytes() != before or source.read_bytes() != src_raw or candidates.read_bytes() != candidates_raw
                or (choices is not None and choices.read_bytes() != choice_raw)):
            raise ValueError('inventory changed; partial journal retained')
        write({'state': 'complete', 'verified': len(rows), 'models_removed': 0})
    return len(rows)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', type=Path, required=True)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--candidates', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--pack', required=True)
    parser.add_argument('--limit', type=int, required=True)
    parser.add_argument('--offset', type=int, default=0)
    parser.add_argument('--choices', type=Path)
    args = parser.parse_args()
    if not 1 <= args.limit <= 1000:
        parser.error('limit must be between 1 and 1000')
    print(json.dumps({'state': 'complete', 'verified': audit(args.vault, args.source,
          args.candidates, args.output, args.pack, args.limit, args.offset, args.choices), 'models_removed': 0}))
