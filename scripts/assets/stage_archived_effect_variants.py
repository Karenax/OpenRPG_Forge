"""Prepare one selected URP source per effect, with HDRP provenance in its archive.

This is a stage only. It never publishes, deletes, or asserts effect equivalence.
"""
import argparse
import copy
import json
from pathlib import Path

import vault_storage as storage
from stage_source_support_relinks import digest_file, norm


def consolidate(catalog, pairs, archived, old_catalog_sha):
    result = copy.deepcopy(catalog)
    cards = {c['id']: c for c in result['assets']}
    if len(cards) != len(result['assets']) or not storage.valid_hex(old_catalog_sha, 64):
        raise ValueError('invalid source catalogue')
    retired = set()
    for pair in pairs:
        old, keep = pair['hdrp_id'], pair['urp_id']
        if old in retired or keep in retired or old == keep or old not in cards or keep not in cards:
            raise ValueError('overlapping or missing identities')
        source, target = cards[old], cards[keep]
        proof = archived.get(old)
        if not proof or source.get('consolidated_sources') or target.get('consolidated_sources'):
            raise ValueError('missing archive proof or existing alias')
        if any(source.get(k) != proof.get(k) for k in ('id','source_file','source_sha256',
                                                        'source_meta_file','source_meta_sha256','source_asset_path')):
            raise ValueError('archive proof differs from source card')
        if (source.get('source_guid') != target.get('source_guid') or source.get('source_guid') not in (None, proof.get('guid'))
                or source.get('source_meta_sha256') != target.get('source_meta_sha256')
                or source.get('source_archive_sha256') != proof.get('archive_sha256')
                or source.get('source_archive') != proof.get('archive')):
            raise ValueError('source identity or archive differs')
        if source.get('source_direct_archive_is_nested') not in (False, None):
            raise ValueError('HDRP source is not in the direct archive')
        if not source.get('source_file', '').lower().endswith(('.vfx', '.prefab', '.unity')):
            raise ValueError('only checked Unity source assets are eligible')
        if '/urp/' not in target.get('source_asset_path','').replace('\\','/').casefold():
            raise ValueError('retained source is not URP')
        # Keep the old identity and descriptive data, but its usable source is
        # the selected URP source. The original complete record is recoverable
        # from the immutable prior revision; its bytes are in the Unity archive.
        alias_detail = copy.deepcopy(source)
        for key in list(alias_detail):
            if key.startswith('source_') or key == 'nested_archive_provenance':
                del alias_detail[key]
        for key, value in target.items():
            if key.startswith('source_') or key == 'nested_archive_provenance':
                alias_detail[key] = copy.deepcopy(value)
        alias = {'retired_id':old, 'original_detail':alias_detail,
                 'archived_original': {'prior_catalog_sha256':old_catalog_sha,
                    'archive':proof['archive'], 'archive_sha256':proof['archive_sha256'],
                    'unity_guid':proof['guid'], 'unity_asset_path':proof['source_asset_path'],
                    'source_sha256':proof['source_sha256'], 'meta_sha256':proof['source_meta_sha256']},
                 'selected_source_pipeline':'URP'}
        if source.get('preview_sha256') != target.get('preview_sha256'):
            alias['preserve_distinct_preview'] = True
        target['consolidated_sources'] = [alias]
        retired.add(old)
    result['assets'] = [c for c in result['assets'] if c['id'] not in retired]
    return result


def consolidate_existing(catalog, pairs, archived, old_catalog_sha):
    """Retarget only selected existing aliases while preserving the other records."""
    result = copy.deepcopy(catalog)
    owners = {c['id']:c for c in result['assets']}
    seen = set()
    for pair in pairs:
        old, keep = pair['hdrp_id'], pair['urp_id']
        if old in seen or keep not in owners:
            raise ValueError('duplicate or absent alias owner')
        owner = owners[keep]
        aliases = owner.get('consolidated_sources', [])
        matching = [i for i, a in enumerate(aliases) if a.get('retired_id') == old]
        if len(matching) != 1:
            raise ValueError('existing alias not unique')
        index = matching[0]
        original_alias = aliases[index]
        if original_alias.get('source_variation', {}).get('kind') not in ('lod_thresholds_only','lod_collider_only','pipeline_light_only'):
            raise ValueError('existing alias is not a qualified source variation')
        if set(original_alias) - {'retired_id','original_detail','source_variation','preserve_distinct_preview'}:
            raise ValueError('unknown alias data cannot be discarded')
        standalone = copy.deepcopy(owner)
        standalone.pop('consolidated_sources', None)
        temporary = {'assets':[copy.deepcopy(original_alias['original_detail']), standalone]}
        converted = consolidate(temporary, [pair], archived, old_catalog_sha)
        aliases[index] = converted['assets'][0]['consolidated_sources'][0]
        seen.add(old)
    return result


def prepare(vault, inventory_path, proof_paths, existing_aliases=False):
    vault = vault.resolve(strict=True)
    source_pointer = storage.confined(vault, vault/'metadata/asset-source-support-registry.v1.json')
    model_pointer = storage.confined(vault, vault/'metadata/asset-import-registry.v1.json')
    source_before, model_before = source_pointer.read_bytes(), model_pointer.read_bytes()
    pointer = json.loads(source_before)
    catalog_raw = storage.confined(vault, vault/pointer['path']).read_bytes()
    catalog_sha = storage.sha(catalog_raw)
    if catalog_sha != pointer['sha256']:
        raise ValueError('catalogue hash differs')
    inv_raw = inventory_path.read_bytes()
    inv = json.loads(inv_raw)
    if inv['source_registry_sha256'] != storage.sha(source_before):
        raise ValueError('inventory is stale')
    archived = {}
    proof_hashes = []
    for path in proof_paths:
        raw = path.read_bytes()
        doc = json.loads(raw)
        if doc.get('state') != 'passed' or doc['source_registry_sha256'] != storage.sha(source_before):
            raise ValueError('archive proof is stale')
        if doc['count'] != len(doc['assets']):
            raise ValueError('archive coverage differs')
        proof_hashes.append(storage.sha(raw))
        for entry in doc['assets']:
            row = {**entry, 'archive':doc['archive'], 'archive_sha256':doc['archive_sha256']}
            if row['id'] in archived:
                raise ValueError('duplicate archive proof')
            archived[row['id']] = row
    pairs = [p for p in inv['pairs'] if p['hdrp_id'] in archived]
    if len(pairs) != len(archived):
        raise ValueError('archive proof coverage differs from inventory')
    current = json.loads(catalog_raw)
    staged = (consolidate_existing(current, pairs, archived, catalog_sha) if existing_aliases
              else consolidate(current, pairs, archived, catalog_sha))
    cards = {x['id']:x for x in current['assets']}
    if existing_aliases:
        for owner in current['assets']:
            for alias in owner.get('consolidated_sources', []):
                ident = alias['retired_id']
                if ident in cards:raise ValueError('alias conflicts with active card')
                cards[ident] = alias['original_detail']
    payloads = {}
    for pair in pairs:
        for ident in (pair['hdrp_id'], pair['urp_id']):
            card = cards[ident]
            for key, hashkey in (('source_file','source_sha256'),('source_meta_file','source_meta_sha256')):
                rel, pin = card[key], card[hashkey]
                norm(rel)
                if rel not in payloads:
                    payloads[rel] = digest_file(storage.confined(vault, vault/rel))
                if payloads[rel] != pin:
                    raise ValueError('source payload SHA mismatch')
    if source_pointer.read_bytes()!=source_before or model_pointer.read_bytes()!=model_before or inventory_path.read_bytes()!=inv_raw:
        raise ValueError('stage inputs changed')
    raw = (json.dumps(staged, sort_keys=True, ensure_ascii=False, indent=2)+'\n').encode('utf8')
    plan = {'schema':'openrpg.archived-effect-variant-stage.v1','state':'prepared_only',
            'source_registry_sha256':storage.sha(source_before), 'asset_registry_sha256':storage.sha(model_before),
            'old_catalog_sha256':catalog_sha, 'new_catalog_sha256':storage.sha(raw),
            'archive_proof_sha256':proof_hashes, 'pairs':pairs, 'payloads_verified':len(payloads),
            'limitations':'No publication or deletion. URP effect conversion and runtime not qualified.'}
    if existing_aliases:
        plan['existing_alias_migration'] = True
    return raw, plan


if __name__ == '__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','inventory','stage'):
        p.add_argument('--'+name, type=Path, required=True)
    p.add_argument('--proof', type=Path, required=True, action='append')
    p.add_argument('--existing-aliases', action='store_true')
    a=p.parse_args()
    raw, plan=prepare(a.vault,a.inventory,a.proof,a.existing_aliases)
    a.stage.mkdir(parents=True, exist_ok=False)
    (a.stage/'source-support.v1.json').write_bytes(raw)
    (a.stage/'plan.json').write_text(json.dumps(plan,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    print(json.dumps({'pairs':len(plan['pairs']),'payloads_verified':plan['payloads_verified']}))
