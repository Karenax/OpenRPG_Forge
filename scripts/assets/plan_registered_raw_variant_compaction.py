"""Prepare a pinned, read-only inventory of registered raw Witch House variants.

The plan does not compact, delete or publish anything. Only raw variants whose
matching retained model is already shared, whose geometry comparison is current,
and whose material controls prefer the retained main model are considered.
"""
import argparse
import hashlib
import json
from pathlib import Path

import audit_variant_material_choices as materials
import vault_storage as storage


def build(vault, source, candidates, additional_batches=False):
    vault = vault.resolve(strict=True)
    src, cand = source.read_bytes(), candidates.read_bytes()
    inv, doc = json.loads(src), json.loads(cand)
    registry_path = storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
    registry = registry_path.read_bytes()
    if (storage.sha(registry) != inv['registry_sha256']
            or doc['source_sha256'] != storage.sha(src)
            or doc['registry_sha256'] != inv['registry_sha256']
            or doc['mode'] != 'identical-mesh'):
        raise ValueError('inventory or registry pin differs')
    assets = {}
    for entry in json.loads(registry)['packs'].values():
        details_path = storage.confined(vault, vault / entry['path'] / 'details.v1.json')
        raw = details_path.read_bytes()
        if storage.sha(raw) != entry['files']['details.v1.json']:
            raise ValueError('active metadata changed')
        for asset in json.loads(raw)['assets']:
            if asset['id'] in assets:
                raise ValueError('duplicate active identity')
            assets[asset['id']] = asset
    eligible = {x['variant_id']: x for x in doc['candidates']
                if x['main']['storage'] == 'descriptor' and x['main']['declared_pin_matches']
                and x['variant']['storage'] == 'raw' and not x['variant_has_aliases']}
    if additional_batches:
        # Compaction is lossless and does not decide which visibly different
        # version should be kept. Limit this mode to three registered batches.
        eligible = {x['variant_id']: {} for x in inv['pairs']
                    if x['main_id'].split('.')[1] == 'witch-house'}
    rows, refused = [], []
    for pair in inv['pairs']:
        if pair['variant_id'] not in eligible or pair['main_id'].split('.')[1] != 'witch-house':
            continue
        try:
            if not pair['comparison_current'] or (not additional_batches and
                    (pair['mesh_payloads_exact'] != 'True' or pair['transforms_exact'] != 'True')):
                raise ValueError('historical geometry comparison is not current')
            for side in ('main', 'variant'):
                asset = assets[pair[side + '_id']]
                if asset.get('model_path') != pair[side + '_path'] or asset.get('model_sha256') != pair[side + '_sha']:
                    raise ValueError('active model pin differs')
            main, raw = (storage.confined(vault, Path(pair[side + '_path'])) for side in ('main', 'variant'))
            parts = raw.relative_to(vault).parts
            allowed = (('witch-house-urp-basic-source-frame-batch209-v1',
                        'witch-house-urp-basic-orm-srgb13-v1',
                        'witch-house-urp-basic-source-corner-batch6-v1') if additional_batches else
                       ('witch-house-urp-bounded-remaining36-v1',
                        'witch-house-urp-basic-remaining8-v1'))
            if (len(parts) < 5 or parts[:2] != ('normalized', 'hivemind')
                    or parts[2] not in allowed
                    or raw.suffix.lower() != '.glb'):
                if additional_batches:
                    continue
                raise ValueError('raw model outside explicit converted Witch House scope')
            if storage.read_descriptor(vault, raw) is not None:
                raise ValueError('raw model already compacted')
            info = raw.stat()
            if storage.windows_readonly(info):
                raise ValueError('raw model is read-only')
            if not additional_batches:
                choice = materials.choose(materials.glb_document(vault, main), materials.glb_document(vault, raw))
                if choice['keep_side'] != 'main':
                    raise ValueError('material choice does not prefer main')
            for side, path in (('main', main), ('variant', raw)):
                if storage.digest(vault, path) != pair[side + '_sha']:
                    raise ValueError('logical model SHA mismatch')
            rows.append({'path': str(raw), 'bytes': info.st_size, 'mtime_ns': info.st_mtime_ns,
                         'sha256': pair['variant_sha'], 'asset_id': pair['variant_id'],
                         'retained_id': pair['main_id'], 'retained_sha256': pair['main_sha']})
        except (OSError, ValueError, KeyError) as error:
            refused.append({'variant_id': pair['variant_id'], 'reason': str(error)})
    if registry_path.read_bytes() != registry or source.read_bytes() != src or candidates.read_bytes() != cand:
        raise ValueError('inputs changed during planning')
    return {'schema': 'openrpg.registered-raw-variant-compaction-plan.v1', 'state': 'planning_only',
            'vault': str(vault), 'registry_sha256': storage.sha(registry),
            'source_sha256': storage.sha(src), 'candidate_inventory_sha256': storage.sha(cand),
            'files': rows, 'refused': refused, 'logical_bytes': sum(x['bytes'] for x in rows),
            'additional_batches': additional_batches,
            'limitations': 'No physical gain estimated, compaction, publication or removal performed.'}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', type=Path, required=True)
    p.add_argument('--source', type=Path, required=True)
    p.add_argument('--candidates', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--additional-batches', action='store_true',
                   help='inventory three explicit converted raw batches without choosing a retained version')
    args = p.parse_args()
    if args.output.exists():
        p.error('output already exists')
    result = build(args.vault, args.source, args.candidates, args.additional_batches)
    with args.output.open('x', encoding='utf-8', newline='\n') as out:
        json.dump(result, out, ensure_ascii=False, indent=2)
        out.write('\n')
    print(json.dumps({'state': result['state'], 'files': len(result['files']),
                      'refused': len(result['refused']), 'logical_bytes': result['logical_bytes']}))
