"""Prepare shared Vault preview references across model asset roots.

Requires a qualified Studio reader supporting preview_vault before publication.
Never modifies the Vault, publishes a registry or removes an image.
"""
import argparse
import json
from pathlib import Path

import vault_storage as storage
from prepare_variant_metadata_transition import encoded
from stage_model_preview_relinks import asset_root
from stage_source_support_relinks import digest_file


def select(inventory):
    paths={};ids={}
    for group in inventory['groups']:
        pin=group['sha256']
        if not storage.valid_hex(pin,64):raise ValueError('invalid preview hash')
        rows=sorted(group['paths'],key=lambda x:x['path'].casefold())
        if len(rows)<2:continue
        kept=rows[0]['path']
        for row in rows[1:]:
            key=row['path'].casefold()
            if key in paths:raise ValueError('duplicate preview path')
            item={'old':row['path'],'retained':kept,'sha256':pin,
                  'ids':[m['id'] for m in row['models']]}
            paths[key]=item
            for ident in item['ids']:
                if ident in ids:raise ValueError('duplicate model identity')
                ids[ident]=item
    if not paths or any(x['retained'].casefold() in paths for x in paths.values()):
        raise ValueError('empty or overlapping preview mappings')
    return paths,ids


def prepare(vault,inventory_path,stage):
    vault=vault.resolve(strict=True);inventory_raw=inventory_path.read_bytes();inventory=json.loads(inventory_raw)
    reg=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json')
    support=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json')
    before,support_before=reg.read_bytes(),support.read_bytes()
    if storage.sha(before)!=inventory['registry_sha256']:raise ValueError('registry changed')
    paths,wanted=select(inventory)
    for row in paths.values():
        for name in ('old','retained'):
            file=storage.confined(vault,Path(row[name]))
            if digest_file(file)!=row['sha256']:raise ValueError('preview SHA changed')
    registry=json.loads(before);pack_docs={};catalogs={};found=set()
    # Load every active pack once; all cards using a derived catalog must move
    # together to keep the importer's original-to-derived mapping unambiguous.
    for pack,entry in sorted(registry['packs'].items()):
        docs={}
        for key,name in (('details','details.v1.json'),('profiles','profiles.v1.json')):
            raw=storage.confined(vault,vault/entry['path']/name).read_bytes()
            if storage.sha(raw)!=entry['files'][name]:raise ValueError('snapshot SHA changed')
            docs[key]=json.loads(raw)
        pack_docs[pack]=docs
        for detail in docs['details']['assets']:
            ident=detail['id']
            if ident not in wanted:continue
            cat=storage.confined(vault,Path(detail['source_catalog']))
            if cat not in catalogs:
                raw=cat.read_bytes();catalogs[cat]={'raw':raw,'doc':json.loads(raw)}
            matches=[x for x in catalogs[cat]['doc']['assets'] if x['id']==ident]
            if len(matches)!=1 or matches[0].get('sha256')!=detail.get('model_sha256'):
                raise ValueError('catalog/model mismatch: '+ident)
            item=matches[0];row=wanted[ident]
            old=asset_root(cat,detail,item)/Path(item['preview'])
            if str(old).casefold()!=row['old'].casefold() or item.get('preview_sha256')!=row['sha256']:
                raise ValueError('preview identity changed')
            if 'preview_vault' in item:raise ValueError('existing shared preview requires migration')
            item['preview_vault']=Path(row['retained']).relative_to(vault).as_posix()
            found.add(ident)
    if found!=set(wanted):raise ValueError('preview identity missing')
    derived={}
    for index,(path,record) in enumerate(sorted(catalogs.items(),key=lambda x:str(x[0]).casefold()),1):
        raw=encoded(record['doc']);sha=storage.sha(raw)
        derived[str(path).casefold()]={'old':str(path),'old_sha256':storage.sha(record['raw']),
            'new':str(path.parent/('openrpg-preview-'+sha+'.json')),'new_sha256':sha,
            'stage_file':f'catalog-{index:04d}.json','raw':raw}
    changed=[];changed_packs={}
    for pack,docs in pack_docs.items():
        for card in docs['details']['assets']:
            key=card.get('source_catalog','').casefold()
            if key not in derived:continue
            item=derived[key]
            origin=card.get('source_catalog_preview_origin')
            if origin is None:
                card['source_catalog_preview_origin']={'path':item['old'],'sha256':item['old_sha256']}
            elif not isinstance(origin,dict) or set(origin)!={'path','sha256'}:
                raise ValueError('invalid existing source catalog origin')
            card['source_catalog']=item['new']
            changed.append(card['id']);changed_packs[pack]=docs
    origins={}
    for docs in pack_docs.values():
        for card in docs['details']['assets']:
            origin=card.get('source_catalog_preview_origin')
            if isinstance(origin,dict):
                key=origin['path'].casefold();value=card['source_catalog'].casefold()
                if key in origins and origins[key]!=value:raise ValueError('ambiguous origin mapping')
                origins[key]=value
    if reg.read_bytes()!=before or support.read_bytes()!=support_before or inventory_path.read_bytes()!=inventory_raw:
        raise ValueError('inputs changed')
    for path,record in catalogs.items():
        if path.read_bytes()!=record['raw']:raise ValueError('catalog changed')
    stage.mkdir(exist_ok=False);catalog_rows=[]
    for item in derived.values():
        raw=item.pop('raw');(stage/item['stage_file']).write_bytes(raw);catalog_rows.append(item)
    packs={}
    for pack,docs in changed_packs.items():
        out=stage/pack;out.mkdir();files={}
        for key,name in (('details','details.v1.json'),('profiles','profiles.v1.json')):
            raw=encoded(docs[key]);(out/name).write_bytes(raw);files[name]=storage.sha(raw)
        files['revision']=storage.sha(encoded({'profiles':docs['profiles'],'details':docs['details']}));packs[pack]=files
    plan={'schema':'openrpg.cross-root-preview-stage.v1','state':'prepared_only',
          'registry_sha256':storage.sha(before),'source_registry_sha256':storage.sha(support_before),
          'inventory_sha256':storage.sha(inventory_raw),'candidate_paths':list(paths.values()),
          'preview_ids':sorted(wanted),'changed_ids':sorted(changed),'catalogs':catalog_rows,'packs':packs,
          'limitations':'Requires qualified shared preview reader; not published or removed.'}
    (stage/'plan.json').write_bytes(encoded(plan));return plan


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','inventory','stage'):p.add_argument('--'+n,type=Path,required=True)
    a=p.parse_args();root=Path(r'E:\OpenRPG_Forge\.build\asset-conversion').resolve(strict=True);stage=a.stage.resolve(strict=False)
    if not stage.is_relative_to(root) or stage.exists():p.error('stage must be new and inside asset conversion workspace')
    result=prepare(a.vault,a.inventory,stage)
    print(json.dumps({'state':result['state'],'paths':len(result['candidate_paths']),
          'preview_models':len(result['preview_ids']),'redirected_models':len(result['changed_ids']),
          'catalogs':len(result['catalogs']),'packs':len(result['packs'])}))
