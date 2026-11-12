"""Validate a prepared source relink stage and its selected payloads, read-only.

Archives are checked for existence and declared size, not rehashed. This does
not qualify Unity dependency resolution, external references or Studio.
"""
import argparse
import json
from pathlib import Path

import vault_storage as storage
from stage_source_support_relinks import digest_file, norm, replace


def validate(vault, stage):
    vault = vault.resolve(strict=True)
    stage = stage.resolve(strict=True)
    plan = json.loads((stage/'plan.json').read_bytes())
    if plan.get('schema') != 'openrpg.source-support-relinks-stage.v1' or plan.get('state') != 'prepared_only':
        raise ValueError('invalid prepared stage')
    pointer = storage.confined(vault, vault/'metadata/asset-source-support-registry.v1.json')
    assets_pointer = storage.confined(vault, vault/'metadata/asset-import-registry.v1.json')
    pbytes, abytes = pointer.read_bytes(), assets_pointer.read_bytes()
    if storage.sha(pbytes) != plan['source_registry_sha256'] or storage.sha(abytes) != plan['asset_registry_sha256']:
        raise ValueError('registry changed')
    source = storage.confined(vault, vault/json.loads(pbytes)['path']).read_bytes()
    staged_raw = (stage/'source-support.v1.json').read_bytes()
    if storage.sha(source) != plan['prior_catalog_sha256'] or storage.sha(staged_raw) != plan['staged_catalog_sha256']:
        raise ValueError('catalog SHA mismatch')
    mapping = {norm(p['old']): p for p in plan['paths']}
    if len(mapping) != len(plan['paths']):
        raise ValueError('duplicate mapping')
    for item in mapping.values():
        for rel in (item['old'], item['retained']):
            norm(rel)
            if digest_file(storage.confined(vault, vault/rel)) != item['sha256']:
                raise ValueError('payload SHA differs: '+rel)
    expected, counts, ids = replace(json.loads(source), mapping, plan.get('include_aliases') is True)
    staged = json.loads(staged_raw)
    if expected != staged or ids != plan['changed_ids'] or any(counts[norm(p['old'])] != p['references'] for p in plan['paths']):
        raise ValueError('staged content differs from exact relink')
    selected = set(ids)
    cache = {}
    payloads = {'source_file': 'source_sha256', 'source_meta_file': 'source_meta_sha256',
                'source_prefab_file': 'source_prefab_sha256', 'source_prefab_meta_file': 'source_prefab_meta_sha256',
                'preview': 'preview_sha256'}
    archived = set()
    optional_unchecked = 0
    for card in staged['assets']:
        if card['id'] not in selected:
            continue
        for field, hash_field in payloads.items():
            rel, pin = card.get(field), card.get(hash_field)
            if rel is None and pin is None:
                continue
            if rel is None and field in ('source_prefab_file', 'source_prefab_meta_file'):
                optional_unchecked += 1
                continue
            if not isinstance(rel, str) or not storage.valid_hex(pin, 64):
                raise ValueError('incomplete payload metadata: '+card['id']+'/'+field)
            norm(rel)
            path = storage.confined(vault, vault/rel)
            if path not in cache:
                cache[path] = digest_file(path)
            if cache[path] != pin:
                raise ValueError('payload SHA differs: '+card['id']+'/'+field)
        evidence = card.get('evidence')
        if isinstance(evidence, dict) and evidence.get('diagnostic_preview_reference') is not None:
            rel, pin = evidence['diagnostic_preview_reference'], evidence.get('diagnostic_preview_reference_sha256')
            if not storage.valid_hex(pin, 64):
                raise ValueError('diagnostic preview SHA missing: '+card['id'])
            norm(rel)
            path = storage.confined(vault, vault/rel)
            if path not in cache:
                cache[path] = digest_file(path)
            if cache[path] != pin:
                raise ValueError('diagnostic preview SHA differs: '+card['id'])
        archive = card.get('source_archive')
        norm(archive)
        path = storage.confined(vault, vault/archive)
        if not path.is_file() or (card.get('source_archive_bytes') is not None and path.stat().st_size != card['source_archive_bytes']):
            raise ValueError('source archive missing or size differs')
        archived.add(str(path))
    if pointer.read_bytes() != pbytes or assets_pointer.read_bytes() != abytes:
        raise ValueError('registry changed during validation')
    return {'state': 'payloads_verified', 'catalog_sha256': storage.sha(staged_raw),
            'changed_cards': len(selected), 'unique_payloads_hashed': len(cache),
            'optional_prefab_hashes_without_paths': optional_unchecked,
            'archives_checked_for_size_only': len(archived), 'paths': len(mapping),
            'limitations': 'Archives not rehashed; Unity dependencies, external references and Studio not validated.'}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', type=Path, required=True)
    p.add_argument('--stage', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    a = p.parse_args()
    result = validate(a.vault, a.stage)
    with a.output.open('x', encoding='utf8', newline='\n') as f:
        json.dump(result, f, ensure_ascii=False, indent=2)
        f.write('\n')
    print(json.dumps(result))
