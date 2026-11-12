"""Independently compare staged preview catalogs and snapshots to active data."""
import argparse
import copy
import json
from pathlib import Path

import vault_storage as storage
from prepare_variant_metadata_transition import encoded


def verify(vault, stage):
    vault, stage = vault.resolve(strict=True), stage.resolve(strict=True)
    plan_raw = (stage/'plan.json').read_bytes()
    plan = json.loads(plan_raw)
    if plan.get('schema') != 'openrpg.model-preview-relink-stage.v1' or plan.get('state') != 'prepared_only':
        raise ValueError('invalid preview stage')
    registry = storage.confined(vault, vault/'metadata/asset-import-registry.v1.json')
    support = storage.confined(vault, vault/'metadata/asset-source-support-registry.v1.json')
    before, source_before = registry.read_bytes(), support.read_bytes()
    if storage.sha(before) != plan['registry_sha256'] or storage.sha(source_before) != plan['source_registry_sha256']:
        raise ValueError('registry changed')
    current = json.loads(before)
    rows = {}
    for pair in plan['candidate_paths']:
        for ident in pair['ids']:
            if ident in rows:
                raise ValueError('identity appears twice')
            rows[ident] = pair
    if set(rows) != set(plan['changed_ids']):
        raise ValueError('candidate identity set differs')
    catalogs = {}
    catalog_changed_ids = set()
    for item in plan['catalogs']:
        old = storage.confined(vault, Path(item['old']))
        original = old.read_bytes()
        if storage.sha(original) != item['old_sha256']:
            raise ValueError('original catalog changed')
        staged = (stage/item['stage_file']).read_bytes()
        if storage.sha(staged) != item['new_sha256']:
            raise ValueError('staged catalog SHA changed')
        derived = json.loads(staged)
        expected = copy.deepcopy(json.loads(original))
        for entry in expected['assets']:
            ident = entry['id']
            if ident not in rows:
                continue
            pair = rows[ident]
            if entry.get('preview_sha256') != pair['sha256']:
                raise ValueError('preview hash changed')
            relative = Path(entry['preview'])
            if relative.is_absolute() or '..' in relative.parts:
                raise ValueError('invalid original preview path')
            if Path(pair['old']).parent != Path(pair['retained']).parent:
                raise ValueError('preview directories differ')
            entry['preview'] = (relative.parent/Path(pair['retained']).name).as_posix()
            catalog_changed_ids.add(ident)
        if expected != derived:
            raise ValueError('derived catalog changes additional data')
        if Path(item['new']).parent != old.parent or not Path(item['new']).name.endswith(item['new_sha256']+'.json'):
            raise ValueError('derived catalog destination differs')
        catalogs[str(old).casefold()] = item
    if catalog_changed_ids != set(rows):
        raise ValueError('catalog candidate coverage differs')
    changed = set()
    for pack, pins in plan['packs'].items():
        entry = current['packs'][pack]
        docs = {}
        for key, filename in (('details','details.v1.json'),('profiles','profiles.v1.json')):
            original_raw = storage.confined(vault, vault/entry['path']/filename).read_bytes()
            if storage.sha(original_raw) != entry['files'][filename]:
                raise ValueError('active pack hash differs')
            staged_raw = (stage/pack/filename).read_bytes()
            if storage.sha(staged_raw) != pins[filename]:
                raise ValueError('staged pack hash differs')
            expected = json.loads(original_raw)
            if key == 'details':
                for detail in expected['assets']:
                    if detail['id'] not in rows:
                        continue
                    rec = catalogs[detail['source_catalog'].casefold()]
                    if 'source_catalog_preview_origin' in detail:
                        raise ValueError('existing preview origin')
                    detail['source_catalog_preview_origin'] = {'path':rec['old'],'sha256':rec['old_sha256']}
                    detail['source_catalog'] = rec['new']
                    changed.add(detail['id'])
            if expected != json.loads(staged_raw):
                raise ValueError('pack changes additional data')
            docs[key] = expected
        if storage.sha(encoded({'profiles':docs['profiles'],'details':docs['details']})) != pins['revision']:
            raise ValueError('staged revision differs')
    if changed != set(rows):
        raise ValueError('pack candidate coverage differs')
    if registry.read_bytes() != before or support.read_bytes() != source_before or (stage/'plan.json').read_bytes() != plan_raw:
        raise ValueError('inputs changed during verification')
    return {'state':'stage_verified','models':len(changed),'catalogs':len(catalogs),'packs':len(plan['packs']),
            'paths':len(plan['candidate_paths']), 'plan_sha256':storage.sha(plan_raw),
            'limitations':'No publication, native reader, references outside active catalogs or deletion checked.'}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for name in ('vault','stage','output'):
        p.add_argument('--'+name, type=Path, required=True)
    a = p.parse_args()
    result = verify(a.vault, a.stage)
    with a.output.open('x', encoding='utf8', newline='\n') as f:
        json.dump(result,f,ensure_ascii=False,indent=2)
        f.write('\n')
    print(json.dumps(result))
