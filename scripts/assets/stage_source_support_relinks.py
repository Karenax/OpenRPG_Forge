"""Prepare, without publishing, exact-byte relinks in a source-support catalog.

The stage is not authorization to delete files. Historical revisions, external
consumers, Unity dependency resolution and Studio must be qualified separately.
"""
import argparse
import copy
import csv
import hashlib
import json
from pathlib import Path

import vault_storage as storage

FIELDS = {'source_file': 'source_sha256', 'source_prefab_file': 'source_prefab_sha256',
          'source_meta_file': 'source_meta_sha256', 'source_prefab_meta_file': 'source_prefab_meta_sha256',
          'preview': 'preview_sha256', 'source_archive': 'source_archive_sha256'}


def norm(path):
    if not isinstance(path, str) or not path or '\\' in path:
        raise ValueError('invalid relative source path')
    p = Path(path)
    if p.is_absolute() or '..' in p.parts:
        raise ValueError('source path escapes Vault')
    return path.casefold()


def replace(catalog, mapping, include_aliases=False):
    result = copy.deepcopy(catalog)
    counts = {key: 0 for key in mapping}
    changed = set()
    ids = set()
    for owner in result['assets']:
        ident = owner['id']
        if ident in ids:
            raise ValueError('duplicate source-support ID')
        ids.add(ident)
        cards=[owner]
        if include_aliases:
            aliases=owner.get('consolidated_sources',[])
            if not isinstance(aliases,list):raise ValueError('invalid source aliases')
            for alias in aliases:
                detail=alias.get('original_detail')
                if not isinstance(detail,dict) or detail.get('id')!=alias.get('retired_id'):
                    raise ValueError('invalid source alias detail')
                cards.append(detail)
        for card in cards:
            for field, hash_field in FIELDS.items():
                value = card.get(field)
                if value is None:
                    continue
                key = norm(value)
                if key not in mapping:
                    continue
                item = mapping[key]
                if card.get(hash_field) != item['sha256']:
                    raise ValueError('source field SHA disagrees with mapping: ' + ident)
                card[field] = item['retained']
                counts[key] += 1
                changed.add(ident)
            mesh_file = card.get('source_mesh_file')
            if mesh_file is not None:
                key = norm(mesh_file)
                if key in mapping:
                    card['source_mesh_file'] = mapping[key]['retained']
                    counts[key] += 1
                    changed.add(ident)
            mesh_files = card.get('source_mesh_files')
            if mesh_files is not None:
                if not isinstance(mesh_files, list):
                    raise ValueError('invalid source mesh dependencies: ' + ident)
                for index, value in enumerate(mesh_files):
                    key = norm(value)
                    if key in mapping:
                        mesh_files[index] = mapping[key]['retained']
                        counts[key] += 1
                        changed.add(ident)
            evidence = card.get('evidence')
            if isinstance(evidence, dict) and evidence.get('diagnostic_preview_reference') is not None:
                key = norm(evidence['diagnostic_preview_reference'])
                if key in mapping:
                    item = mapping[key]
                    if evidence.get('diagnostic_preview_reference_sha256') != item['sha256']:
                        raise ValueError('diagnostic preview SHA disagrees with mapping: ' + ident)
                    evidence['diagnostic_preview_reference'] = item['retained']
                    counts[key] += 1
                    changed.add(ident)
    if any(count == 0 for count in counts.values()):
        raise ValueError('candidate has no supported catalog reference')
    return result, counts, sorted(changed)


def digest_file(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda: f.read(4 * 1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def prepare(vault, candidates_file, include_aliases=False):
    vault = vault.resolve(strict=True)
    pointer = storage.confined(vault, vault/'metadata/asset-source-support-registry.v1.json')
    asset_pointer = storage.confined(vault, vault/'metadata/asset-import-registry.v1.json')
    before, asset_before = pointer.read_bytes(), asset_pointer.read_bytes()
    record = json.loads(before)
    raw = storage.confined(vault, vault/record['path']).read_bytes()
    if storage.sha(raw) != record['sha256']:
        raise ValueError('source catalog SHA mismatch')
    data = candidates_file.read_bytes()
    rows = list(csv.DictReader(data.decode('utf-8-sig').splitlines()))
    mapping = {}
    cache = {}
    for row in rows:
        old = row['candidate']
        key = norm(old)
        targets = json.loads(row['retained_hdrp'])
        expected = row['sha256']
        if key in mapping or not targets or not storage.valid_hex(expected, 64):
            raise ValueError('duplicate or invalid candidate')
        retained = sorted(targets, key=str.casefold)[0]
        if norm(retained) == key:
            raise ValueError('candidate and retained path are the same')
        for rel in (old, retained):
            norm(rel)
            path = storage.confined(vault, vault/rel)
            if path not in cache:
                cache[path] = digest_file(path)
            if cache[path] != expected:
                raise ValueError('source SHA mismatch: ' + rel)
        mapping[key] = {'old': old, 'retained': retained, 'sha256': expected}
    old_keys = set(mapping)
    if any(norm(item['retained']) in old_keys for item in mapping.values()):
        raise ValueError('retained path is another candidate')
    result, counts, changed = replace(json.loads(raw), mapping, include_aliases)
    if pointer.read_bytes() != before or asset_pointer.read_bytes() != asset_before:
        raise ValueError('registry changed during preparation')
    result_raw = (json.dumps(result, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode('utf8')
    plan = {'state': 'prepared_only', 'schema': 'openrpg.source-support-relinks-stage.v1',
            'source_registry_sha256': storage.sha(before), 'asset_registry_sha256': storage.sha(asset_before),
            'prior_catalog_sha256': storage.sha(raw), 'candidate_list_sha256': storage.sha(data),
            'staged_catalog_sha256': storage.sha(result_raw), 'changed_ids': changed,
            'paths': [dict(item, references=counts[key]) for key, item in sorted(mapping.items())],
            'limitations': 'No publication or deletion; verify all references, historical consumers and Studio before use.'}
    if include_aliases:plan['include_aliases']=True
    return result_raw, plan


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', type=Path, required=True)
    p.add_argument('--candidates', type=Path, required=True)
    p.add_argument('--stage', type=Path, required=True)
    p.add_argument('--include-aliases', action='store_true')
    args = p.parse_args()
    root = Path(r'E:\OpenRPG_Forge\.build\asset-conversion').resolve(strict=True)
    stage = args.stage.resolve(strict=False)
    if not stage.is_relative_to(root) or stage.exists():
        p.error('new stage must be under the asset-conversion workspace')
    catalog, plan = prepare(args.vault, args.candidates, args.include_aliases)
    stage.mkdir()
    (stage/'source-support.v1.json').write_bytes(catalog)
    (stage/'plan.json').write_text(json.dumps(plan, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    print(json.dumps({'state': plan['state'], 'paths': len(plan['paths']), 'changed_cards': len(plan['changed_ids']),
                      'references': sum(x['references'] for x in plan['paths'])}))
