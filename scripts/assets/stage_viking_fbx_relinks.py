"""Prepare immutable nested Viking catalog redirects for identical FBX sources."""
import argparse
import copy
import json
import os
from pathlib import Path

import vault_storage as storage
from prepare_variant_metadata_transition import encoded
from stage_source_support_relinks import digest_file


def replace_source_paths(value, document, vault, mapping, hits):
    if isinstance(value,list):
        for child in value:replace_source_paths(child,document,vault,mapping,hits)
    elif isinstance(value,dict):
        path=value.get('path')
        if isinstance(path,str):
            candidates=set()
            raw=Path(path)
            if raw.is_absolute():candidates.add(os.path.normpath(str(raw)).replace('\\','/').casefold())
            else:
                for base in (vault,*[x for x in document.parents if x.is_relative_to(vault)]):
                    candidates.add(os.path.normpath(str(base/raw)).replace('\\','/').casefold())
            found={key for key in candidates if key in mapping}
            if len(found)>1:raise ValueError('ambiguous source path')
            if found:
                old=next(iter(found));row=mapping[old]
                if value.get('sha256')!=row['sha256'] or value.get('source_kind')!='asset':
                    raise ValueError('source metadata or SHA differs')
                value['path']=row['retained']
                hits[old]=hits.get(old,0)+1
        for child in value.values():
            if isinstance(child,(dict,list)):replace_source_paths(child,document,vault,mapping,hits)


def replace_catalog_paths(value, catalogs, changed, vault=None, mapping=None, source_hits=None):
    if isinstance(value,list):
        for child in value:replace_catalog_paths(child,catalogs,changed,vault,mapping,source_hits)
    elif isinstance(value,dict):
        path=value.get('source_catalog')
        if isinstance(path,str) and path.casefold() in catalogs:
            if 'source_catalog_prefab_origin' in value:raise ValueError('catalog origin already exists')
            if vault is not None and mapping is not None:
                # Paths in this metadata record are relative to its original
                # source catalog, not to the active metadata snapshot.
                replace_source_paths(value.get('source_fbx',[]),Path(path),vault,mapping,source_hits)
            entry=catalogs[path.casefold()]
            value['source_catalog_prefab_origin']={'path':entry['old'],'sha256':entry['old_sha256']}
            value['source_catalog']=entry['new'];changed.append(path)
        for child in list(value.values()):
            if isinstance(child,(dict,list)):replace_catalog_paths(child,catalogs,changed,vault,mapping,source_hits)


def prepare(vault, candidates_path, audit_path, stage):
    vault=vault.resolve(strict=True);candidate_raw=candidates_path.read_bytes();audit_raw=audit_path.read_bytes()
    candidates=json.loads(candidate_raw);audit=json.loads(audit_raw)
    if candidates.get('state')!='planning_only' or audit.get('state')!='active_model_reference_audit' or audit['unresolved_catalogs']:
        raise ValueError('candidate or reference audit incomplete')
    registry=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json')
    support=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json')
    registry_raw,support_raw=registry.read_bytes(),support.read_bytes()
    if storage.sha(registry_raw)!=audit['registry_sha256'] or storage.sha(registry_raw)!=candidates['model_registry_sha256']:
        raise ValueError('model registry changed')
    if storage.sha(support_raw)!=candidates['source_registry_sha256']:
        raise ValueError('source registry changed')
    audit_rows={str(Path(x['path'])).replace('\\','/').casefold():x for x in audit['archives']}
    mapping={};catalog_paths=set()
    for row in candidates['rows']:
        if row.get('state')!='verified_bytes':raise ValueError('unverified FBX candidate')
        old=storage.confined(vault,Path(row['old']));new=storage.confined(vault,Path(row['retained']))
        if digest_file(old)!=row['sha256'] or digest_file(new)!=row['sha256']:
            raise ValueError('FBX pair differs')
        key=str(old).replace('\\','/').casefold()
        if key in mapping or key not in audit_rows or not audit_rows[key]['references']:
            raise ValueError('candidate reference coverage differs')
        mapping[key]={'old':str(old),'retained':str(new),'sha256':row['sha256'],'bytes':row['bytes']}
        # Active metadata snapshots are revised separately below; only source
        # catalogs become derived catalogs beside their originals.
        catalog_paths.update(name for name in audit_rows[key]['documents']
                             if 'asset-import-revisions' not in Path(name).parts)
    if len(mapping)!=len(audit_rows):raise ValueError('audit scope differs')
    catalogs={};hits={}
    for name in sorted(catalog_paths,key=str.casefold):
        path=storage.confined(vault,Path(name));before=path.read_bytes();doc=json.loads(before);updated=copy.deepcopy(doc)
        previous=sum(hits.values());replace_source_paths(updated,path,vault,mapping,hits)
        if sum(hits.values())==previous:raise ValueError('catalog has no replaceable FBX')
        raw=encoded(updated);pin=storage.sha(raw);new=path.parent/('openrpg-prefab-'+pin+'.json')
        catalogs[str(path).casefold()]={'old':str(path),'old_sha256':storage.sha(before),
                                      'new':str(new),'new_sha256':pin,'raw':raw}
    if set(hits)!=set(mapping):raise ValueError('not all FBX references changed')
    packs={};changes=[];changed_ids=set();metadata_source_hits={}
    for pack,entry in sorted(json.loads(registry_raw)['packs'].items()):
        docs={}
        for key,name in (('details','details.v1.json'),('profiles','profiles.v1.json')):
            path=storage.confined(vault,vault/entry['path']/name);raw=path.read_bytes()
            if storage.sha(raw)!=entry['files'][name]:raise ValueError('snapshot changed')
            docs[key]=json.loads(raw)
        for card in docs['details']['assets']:
            found=[];replace_catalog_paths(card,catalogs,found,vault,mapping,metadata_source_hits)
            if found:changed_ids.add(card['id']);changes.extend({'pack':pack,'catalog':x} for x in found)
        if any(x['pack']==pack for x in changes):packs[pack]=docs
    if not changes or {x['catalog'].casefold() for x in changes}!=set(catalogs):
        raise ValueError('catalog pointers not fully covered')
    if (set(metadata_source_hits)!=set(mapping)
            or sum(hits.values())+sum(metadata_source_hits.values()) != sum(x['references'] for x in audit['archives'])):
        raise ValueError('active source reference counts not fully migrated')
    if registry.read_bytes()!=registry_raw or support.read_bytes()!=support_raw or candidates_path.read_bytes()!=candidate_raw or audit_path.read_bytes()!=audit_raw:
        raise ValueError('inputs changed')
    if stage.exists():raise ValueError('stage already exists')
    stage.mkdir();catalog_records=[]
    for i,record in enumerate(catalogs.values()):
        record=dict(record);raw=record.pop('raw');record['stage_file']=f'catalog-{i:04}.json'
        (stage/record['stage_file']).write_bytes(raw);catalog_records.append(record)
    pack_records={}
    for pack,docs in packs.items():
        dest=stage/pack;dest.mkdir();files={}
        for key,name in (('details','details.v1.json'),('profiles','profiles.v1.json')):
            raw=encoded(docs[key]);(dest/name).write_bytes(raw);files[name]=storage.sha(raw)
        files['revision']=storage.sha(encoded(docs));pack_records[pack]=files
    result={'schema':'openrpg.viking-fbx-relink-stage.v1','state':'prepared_only',
            'registry_sha256':storage.sha(registry_raw),'source_registry_sha256':storage.sha(support_raw),
            'candidates_sha256':storage.sha(candidate_raw),'audit_sha256':storage.sha(audit_raw),
            'paths':list(mapping.values()),'reference_counts':hits,'catalogs':catalog_records,
            'metadata_source_reference_counts':metadata_source_hits,
            'packs':pack_records,'changed':changes,'changed_ids':sorted(changed_ids),
            'limitations':'Not published; package manifests, external consumers and source retirement require separate qualification.'}
    (stage/'plan.json').write_bytes(encoded(result));return result


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','candidates','audit','stage'):p.add_argument('--'+name,type=Path,required=True)
    a=p.parse_args();root=Path('E:/OpenRPG_Forge/.build/asset-conversion').resolve(strict=True)
    if a.stage.resolve(strict=False).parent!=root:p.error('stage must be a direct work-root child')
    result=prepare(a.vault,a.candidates,a.audit,a.stage)
    print(json.dumps({'state':result['state'],'files':len(result['paths']),'catalogs':len(result['catalogs']),
                      'changed_pointers':len(result['changed']),'cards':len(result['changed_ids'])}))
