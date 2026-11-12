"""Independently reconstruct and compare every staged nested catalog relink."""
import argparse
import json
from pathlib import Path
import vault_storage as storage
from prepare_variant_metadata_transition import encoded
from stage_nested_archive_catalog_relinks import build


def verify(vault,audit,recovery,stage):
    plan_raw=(stage/'plan.json').read_bytes();plan=json.loads(plan_raw)
    if plan.get('schema')!='openrpg.nested-archive-relink-stage.v1' or plan.get('state')!='prepared_only':
        raise ValueError('invalid stage')
    expected=build(vault,audit,recovery)
    for field in ('registry_sha256','source_registry_sha256','audit_sha256','recovery_sha256','archive_rows','changed','changed_ids','archive_references'):
        if plan[field]!=expected[field]:raise ValueError('plan differs: '+field)
    staged={x['old'].casefold():x for x in plan['catalogs']}
    if set(staged)!=set(expected['catalogs']):raise ValueError('catalog coverage differs')
    for key,record in expected['catalogs'].items():
        item=staged[key]
        for name in ('old','old_sha256','new','new_sha256'):
            if item[name]!=record[name]:raise ValueError('catalog metadata differs')
        if (stage/item['stage_file']).read_bytes()!=record['raw']:raise ValueError('staged catalog differs')
    if set(plan['packs'])!=set(expected['packs']):raise ValueError('pack coverage differs')
    for pack,docs in expected['packs'].items():
        pins=plan['packs'][pack]
        for key,name in (('details','details.v1.json'),('profiles','profiles.v1.json')):
            raw=encoded(docs[key])
            if (stage/pack/name).read_bytes()!=raw or pins[name]!=storage.sha(raw):raise ValueError('snapshot differs')
        if pins['revision']!=storage.sha(encoded(docs)):raise ValueError('revision differs')
    if (stage/'plan.json').read_bytes()!=plan_raw:raise ValueError('plan changed')
    return {'state':'stage_verified','plan_sha256':storage.sha(plan_raw),'registry_sha256':plan['registry_sha256'],
            'source_registry_sha256':plan['source_registry_sha256'],'catalogs':len(staged),'packs':len(plan['packs']),
            'changed_pointers':len(plan['changed']),'archive_references':plan['archive_references'],
            'limitations':'Exact staged metadata and archive hashes only; no publication or runtime qualification.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','audit','recovery','stage','output'):p.add_argument('--'+name,type=Path,required=True)
    a=p.parse_args();r=verify(a.vault,a.audit,a.recovery,a.stage)
    with a.output.open('x',encoding='utf8') as f:json.dump(r,f,indent=2);f.write('\n')
    print(json.dumps(r))
