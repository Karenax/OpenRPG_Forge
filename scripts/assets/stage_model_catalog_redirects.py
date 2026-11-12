"""Prepare remaining active references to already-derived preview catalogs."""
import argparse
import json
from pathlib import Path

import vault_storage as storage
from prepare_variant_metadata_transition import encoded


def prepare(vault, prior_plan_path, stage):
    vault = vault.resolve(strict=True)
    prior_raw = prior_plan_path.read_bytes()
    prior = json.loads(prior_raw)
    if prior.get('schema') != 'openrpg.model-preview-relink-stage.v1':
        raise ValueError('invalid earlier preview stage')
    registry_path = storage.confined(vault,vault/'metadata/asset-import-registry.v1.json')
    support_path = storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json')
    before, support = registry_path.read_bytes(), support_path.read_bytes()
    catalogs = {}
    for row in prior['catalogs']:
        old, new = storage.confined(vault,Path(row['old'])), storage.confined(vault,Path(row['new']))
        original, derived = old.read_bytes(), new.read_bytes()
        if storage.sha(original) != row['old_sha256'] or storage.sha(derived) != row['new_sha256']:
            raise ValueError('catalog SHA changed')
        a, b = json.loads(original), json.loads(derived)
        catalogs[str(old).casefold()] = (row,{x['id']:x for x in a['assets']},{x['id']:x for x in b['assets']})
    packs, changed = {}, []
    for pack, entry in sorted(json.loads(before)['packs'].items()):
        docs = {}
        for key,name in (('details','details.v1.json'),('profiles','profiles.v1.json')):
            data = storage.confined(vault,vault/entry['path']/name).read_bytes()
            if storage.sha(data) != entry['files'][name]:
                raise ValueError('active model snapshot SHA changed')
            docs[key] = json.loads(data)
        any_changed = False
        for card in docs['details']['assets']:
            key = card.get('source_catalog','').casefold()
            if key not in catalogs:
                continue
            item, original, derived = catalogs[key]
            ident = card['id']
            if ident not in original or ident not in derived or original[ident] != derived[ident]:
                raise ValueError('unrelated catalog entry differs: '+ident)
            if original[ident].get('sha256') != card.get('model_sha256'):
                raise ValueError('catalog model SHA differs: '+ident)
            if 'source_catalog_preview_origin' in card:
                raise ValueError('existing origin metadata requires migration')
            card['source_catalog_preview_origin'] = {'path':item['old'],'sha256':item['old_sha256']}
            card['source_catalog'] = item['new']
            changed.append(ident)
            any_changed = True
        if any_changed:
            packs[pack] = docs
    if not changed or len(changed) != len(set(changed)):
        raise ValueError('no redirects or duplicate identity')
    if registry_path.read_bytes() != before or support_path.read_bytes() != support or prior_plan_path.read_bytes() != prior_raw:
        raise ValueError('inputs changed')
    stage.mkdir(exist_ok=False)
    records = {}
    for pack,docs in packs.items():
        dest = stage/pack
        dest.mkdir()
        files = {}
        for key,name in (('details','details.v1.json'),('profiles','profiles.v1.json')):
            raw = encoded(docs[key])
            (dest/name).write_bytes(raw)
            files[name] = storage.sha(raw)
        files['revision'] = storage.sha(encoded({'profiles':docs['profiles'],'details':docs['details']}))
        records[pack] = files
    plan = {'schema':'openrpg.model-catalog-redirect-stage.v1','state':'prepared_only',
            'registry_sha256':storage.sha(before),'source_registry_sha256':storage.sha(support),
            'prior_preview_plan_sha256':storage.sha(prior_raw),'changed_ids':sorted(changed),
            'catalogs':prior['catalogs'],'candidate_paths':prior['candidate_paths'],'packs':records,
            'limitations':'Not published; all original preview/catalog and source consumers remain to be checked.'}
    (stage/'plan.json').write_bytes(encoded(plan))
    return plan


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for name in ('vault','prior-plan','stage'):
        p.add_argument('--'+name,type=Path,required=True)
    a = p.parse_args()
    root = Path(r'E:\OpenRPG_Forge\.build\asset-conversion').resolve(strict=True)
    stage = a.stage.resolve(strict=False)
    if not stage.is_relative_to(root) or stage.exists():p.error('new stage must be under asset conversion workspace')
    result = prepare(a.vault,a.prior_plan,stage)
    print(json.dumps({'state':result['state'],'models':len(result['changed_ids']),'packs':len(result['packs'])}))
