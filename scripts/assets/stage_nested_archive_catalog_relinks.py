"""Prepare immutable nested-variant catalog redirects for identical archives."""
import argparse
import copy
import json
from pathlib import Path
import vault_storage as storage
from prepare_variant_metadata_transition import encoded
from stage_source_support_relinks import digest_file


def replace_archive_paths(value, replacements, hits):
    if isinstance(value,list):
        for item in value:replace_archive_paths(item,replacements,hits)
    elif isinstance(value,dict):
        for key,item in list(value.items()):
            if key in ('source_archive','archive_path_canonical_f') and isinstance(item,str):
                normalized=item.replace('\\','/').casefold()
                if normalized in replacements:
                    mapping=replacements[normalized]
                    declared=value.get('source_archive_sha256',value.get('archive_sha256'))
                    if declared!=mapping['sha256']:raise ValueError('archive metadata SHA differs')
                    size=value.get('source_archive_bytes')
                    if size is not None and size!=mapping['bytes']:raise ValueError('archive metadata size differs')
                    value[key]=mapping['new_absolute'] if Path(item).is_absolute() else mapping['new_relative']
                    hits[mapping['sha256']]=hits.get(mapping['sha256'],0)+1
            elif isinstance(item,(dict,list)):replace_archive_paths(item,replacements,hits)


def replace_catalog_paths(value, catalogs, changed):
    if isinstance(value,list):
        for item in value:replace_catalog_paths(item,catalogs,changed)
    elif isinstance(value,dict):
        for key,item in list(value.items()):
            if key=='source_catalog' and isinstance(item,str) and item.casefold() in catalogs:
                if 'source_catalog_archive_origin' in value:raise ValueError('archive catalog origin already present')
                target=catalogs[item.casefold()]
                value['source_catalog_archive_origin']={'path':target['old'],'sha256':target['old_sha256']}
                value[key]=target['new'];changed.append(item)
            elif isinstance(item,(dict,list)):replace_catalog_paths(item,catalogs,changed)


def build(vault,audit_path,recovery_path):
    vault=vault.resolve(strict=True);ar=audit_path.read_bytes();rr=recovery_path.read_bytes();audit=json.loads(ar);recovery=json.loads(rr)
    if audit['state']!='active_model_reference_audit' or audit['unresolved_catalogs']:
        raise ValueError('incomplete nested catalog audit')
    registry=vault/'metadata/asset-import-registry.v1.json';support=vault/'metadata/asset-source-support-registry.v1.json'
    reg_raw=registry.read_bytes();support_raw=support.read_bytes()
    if storage.sha(reg_raw)!=audit['registry_sha256']:raise ValueError('model registry changed')
    replacement={};catalog_paths=set();archive_rows=[]
    for row in recovery['rows']:
        old=storage.confined(vault,vault/row['old']);new=storage.confined(vault,vault/row['retained'])
        if digest_file(old)!=row['sha256'] or digest_file(new)!=row['sha256'] or old.stat().st_size!=new.stat().st_size:
            raise ValueError('archive pair changed')
        found=[x for x in audit['archives'] if Path(x['path'])==old]
        if len(found)!=1 or not found[0]['references']:raise ValueError('missing nested archive references')
        mapping={'sha256':row['sha256'],'bytes':new.stat().st_size,'new_absolute':str(new),'new_relative':new.relative_to(vault).as_posix()}
        replacement[str(old).replace('\\','/').casefold()]=mapping
        replacement[old.relative_to(vault).as_posix().casefold()]=mapping
        catalog_paths.update(found[0]['documents']);archive_rows.append({'old':str(old),'retained':str(new),**mapping})
    catalogs={};hits={}
    for name in sorted(catalog_paths,key=str.casefold):
        old=storage.confined(vault,Path(name));old_raw=old.read_bytes();doc=json.loads(old_raw)
        updated=copy.deepcopy(doc);before=dict(hits);replace_archive_paths(updated,replacement,hits)
        if before==hits:raise ValueError('catalog has no replaceable archive field')
        new_raw=encoded(updated);pin=storage.sha(new_raw);new=old.parent/('openrpg-archive-'+pin+'.json')
        catalogs[str(old).casefold()]={'old':str(old),'old_sha256':storage.sha(old_raw),'new':str(new),'new_sha256':pin,'raw':new_raw}
    packs={};changed=[];changed_ids=set()
    for pack,entry in sorted(json.loads(reg_raw)['packs'].items()):
        docs={}
        for key,name in (('details','details.v1.json'),('profiles','profiles.v1.json')):
            path=storage.confined(vault,vault/entry['path']/name);raw=path.read_bytes()
            if storage.sha(raw)!=entry['files'][name]:raise ValueError('snapshot SHA changed')
            docs[key]=json.loads(raw)
        replaced=[]
        for card in docs['details']['assets']:
            card_replaced=[];replace_catalog_paths(card,catalogs,card_replaced)
            if card_replaced:
                changed_ids.add(card['id']);replaced.extend(card_replaced)
        if replaced:packs[pack]=docs;changed.extend({'pack':pack,'catalog':x} for x in replaced)
    if not changed or set(x['catalog'].casefold() for x in changed)!=set(catalogs):
        raise ValueError('catalogs not fully reachable from active metadata')
    if registry.read_bytes()!=reg_raw or support.read_bytes()!=support_raw or audit_path.read_bytes()!=ar or recovery_path.read_bytes()!=rr:
        raise ValueError('inputs changed')
    for record in catalogs.values():
        if storage.sha(Path(record['old']).read_bytes())!=record['old_sha256']:raise ValueError('catalog changed')
    return {'registry_sha256':storage.sha(reg_raw),'source_registry_sha256':storage.sha(support_raw),
            'audit_sha256':storage.sha(ar),'recovery_sha256':storage.sha(rr),'archive_rows':archive_rows,
            'catalogs':catalogs,'packs':packs,'changed':changed,'changed_ids':sorted(changed_ids),'archive_references':hits}


def prepare(vault,audit_path,recovery_path,stage):
    if stage.exists():raise ValueError('stage exists')
    result=build(vault,audit_path,recovery_path);stage.mkdir()
    catalogs=[]
    for i,record in enumerate(result['catalogs'].values()):
        record=dict(record);raw=record.pop('raw');record['stage_file']=f'catalog-{i:04}.json';(stage/record['stage_file']).write_bytes(raw);catalogs.append(record)
    packs={}
    for pack,docs in result['packs'].items():
        dest=stage/pack;dest.mkdir();files={}
        for key,name in (('details','details.v1.json'),('profiles','profiles.v1.json')):
            raw=encoded(docs[key]);(dest/name).write_bytes(raw);files[name]=storage.sha(raw)
        files['revision']=storage.sha(encoded(docs));packs[pack]=files
    plan={k:v for k,v in result.items() if k not in ('catalogs','packs')};plan.update(schema='openrpg.nested-archive-relink-stage.v1',state='prepared_only',catalogs=catalogs,packs=packs)
    (stage/'plan.json').write_bytes(encoded(plan));return plan


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','audit','recovery','stage'):p.add_argument('--'+name,type=Path,required=True)
    a=p.parse_args();work=Path(r'E:\OpenRPG_Forge\.build\asset-conversion').resolve(strict=True)
    if a.stage.resolve(strict=False).parent!=work:p.error('stage must be a direct work-root child')
    result=prepare(a.vault,a.audit,a.recovery,a.stage)
    print(json.dumps({'state':result['state'],'catalogs':len(result['catalogs']),'packs':len(result['packs']),
                      'changed_pointers':len(result['changed']),'archive_references':result['archive_references']}))
