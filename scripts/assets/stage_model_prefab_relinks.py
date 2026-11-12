"""Prepare immutable model and nested catalog revisions for identical prefabs."""
import argparse
import copy
import csv
import json
from pathlib import Path

import vault_storage as storage
from prepare_variant_metadata_transition import encoded
from stage_source_support_relinks import digest_file, norm


def replace_prefabs(value, mappings, hits):
    """Change only SHA-backed prefab path fields, leaving other metadata intact."""
    if isinstance(value, list):
        for item in value:
            replace_prefabs(item, mappings, hits)
    elif isinstance(value, dict):
        for key, item in list(value.items()):
            if not isinstance(item, str):
                replace_prefabs(item, mappings, hits)
                continue
            if key == 'path' and 'guid' in value and 'sha256' in value:
                hash_key = 'sha256'
            elif key in ('source_prefab_file', 'source_prefab_meta_file'):
                hash_key = key.replace('_file', '_sha256')
            else:
                continue
            relative = item.replace('\\', '/').casefold()
            if relative.startswith('f:/openrpg_assetvault/'):
                relative = relative[len('f:/openrpg_assetvault/'):]
            if ':' in relative or relative.startswith('/'):
                continue
            relative = norm(relative)
            if relative not in mappings:
                continue
            row = mappings[relative]
            if value.get(hash_key) != row['sha256']:
                raise ValueError('prefab declared SHA mismatch: '+item)
            value[key] = str(row['absolute']) if Path(item).is_absolute() else row['retained']
            hits[relative] = hits.get(relative, 0) + 1


def prepare(vault, candidates, audit_file, stage):
    vault = vault.resolve(strict=True)
    audit_raw = audit_file.read_bytes()
    audit = json.loads(audit_raw)
    if audit['state'] != 'active_model_reference_audit' or audit['unresolved_catalogs']:
        raise ValueError('incomplete reference audit')
    registry_path = storage.confined(vault, vault/'metadata/asset-import-registry.v1.json')
    source_path = storage.confined(vault, vault/'metadata/asset-source-support-registry.v1.json')
    registry_raw, source_raw = registry_path.read_bytes(), source_path.read_bytes()
    if storage.sha(registry_raw) != audit['registry_sha256']:
        raise ValueError('stale model registry audit')
    selected = {norm(Path(x['path']).relative_to(vault).as_posix()): x
                for x in audit['archives'] if x['references']}
    mappings = {}
    with candidates.open(encoding='utf8', newline='') as stream:
        for row in csv.DictReader(stream):
            old = norm(row['candidate'])
            if old not in selected:
                continue
            kept = json.loads(row['retained_hdrp'])
            if len(kept) != 1 or old in mappings:
                raise ValueError('ambiguous mapping: '+old)
            original = storage.confined(vault, vault/row['candidate'])
            retained = storage.confined(vault, vault/kept[0])
            if digest_file(original) != row['sha256'] or digest_file(retained) != row['sha256']:
                raise ValueError('prefab contents changed: '+old)
            mappings[old] = {'old':row['candidate'], 'retained':kept[0],
                             'sha256':row['sha256'], 'absolute':retained}
    if set(mappings) != set(selected):
        raise ValueError('candidate coverage incomplete')
    registry = json.loads(registry_raw)
    docs = {p for row in selected.values() for p in row['documents']}
    hits = {}
    catalogs = {}
    for name in sorted(docs):
        old = storage.confined(vault, Path(name))
        if 'asset-import-revisions' in old.parts:
            continue
        before = old.read_bytes()
        data = json.loads(before)
        updated = copy.deepcopy(data)
        replace_prefabs(updated, mappings, hits)
        after = encoded(updated)
        pin = storage.sha(after)
        catalogs[str(old).casefold()] = {'old':str(old), 'old_sha256':storage.sha(before),
            'new':str(old.parent/('openrpg-prefab-'+pin+'.json')), 'new_sha256':pin, 'bytes':after}
    packs, changed_ids = {}, set()
    for pack, entry in sorted(registry['packs'].items()):
        loaded = {}
        for key, name in (('details','details.v1.json'), ('profiles','profiles.v1.json')):
            raw = storage.confined(vault,vault/entry['path']/name).read_bytes()
            if storage.sha(raw) != entry['files'][name]:
                raise ValueError('snapshot SHA mismatch: '+pack)
            loaded[key] = json.loads(raw)
        before_details = copy.deepcopy(loaded['details'])
        for card in loaded['details']['assets']:
            old_card = copy.deepcopy(card)
            replace_prefabs(card, mappings, hits)
            old_catalog = card.get('source_catalog', '')
            if old_catalog.casefold() in catalogs:
                record = catalogs[old_catalog.casefold()]
                card['source_catalog'] = record['new']
                if 'source_catalog_prefab_origin' in card:
                    raise ValueError('existing prefab origin requires migration')
                card['source_catalog_prefab_origin'] = {'path':record['old'], 'sha256':record['old_sha256']}
            if card != old_card:
                changed_ids.add(card['id'])
        if loaded['details'] != before_details:
            packs[pack] = loaded
    if not changed_ids or set(hits) != set(mappings):
        raise ValueError('not all prefab references migrated')
    if registry_path.read_bytes() != registry_raw or source_path.read_bytes() != source_raw:
        raise ValueError('registry changed')
    if audit_file.read_bytes() != audit_raw:
        raise ValueError('audit changed')
    stage.mkdir(exist_ok=False)
    catalog_records = []
    for index, record in enumerate(catalogs.values()):
        record = dict(record)
        content = record.pop('bytes')
        record['stage_file'] = f'catalog-{index:04}.json'
        (stage/record['stage_file']).write_bytes(content)
        catalog_records.append(record)
    pack_records = {}
    for pack, data in packs.items():
        dest = stage/pack
        dest.mkdir()
        files = {}
        for key, name in (('details','details.v1.json'), ('profiles','profiles.v1.json')):
            raw = encoded(data[key]); (dest/name).write_bytes(raw); files[name] = storage.sha(raw)
        files['revision'] = storage.sha(encoded(data))
        pack_records[pack] = files
    plan = {'schema':'openrpg.model-prefab-relink-stage.v1', 'state':'prepared_only',
            'registry_sha256':storage.sha(registry_raw), 'source_registry_sha256':storage.sha(source_raw),
            'audit_sha256':storage.sha(audit_raw), 'changed_ids':sorted(changed_ids),
            'paths':[{k:v for k,v in row.items() if k != 'absolute'} for row in mappings.values()],
            'reference_counts':hits, 'catalogs':catalog_records, 'packs':pack_records}
    (stage/'plan.json').write_bytes(encoded(plan))
    return plan


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('vault','candidates','audit','stage'):
        parser.add_argument('--'+name, type=Path, required=True)
    args = parser.parse_args()
    root = Path('E:/OpenRPG_Forge/.build/asset-conversion').resolve(strict=True)
    if args.stage.resolve(strict=False).parent != root:
        parser.error('stage must be a direct work-root child')
    result = prepare(args.vault,args.candidates,args.audit,args.stage)
    print(json.dumps({'state':result['state'],'paths':len(result['paths']),
                      'models':len(result['changed_ids']),'catalogs':len(result['catalogs'])}))
