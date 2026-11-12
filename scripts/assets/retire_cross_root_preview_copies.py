"""Retire relinked identical model previews after exclusive reader checks."""
import argparse
import contextlib
import hashlib
import json
import os
from pathlib import Path
import shutil

import vault_storage as storage
from exclusive_source_delete import ExclusiveSource
from publish_model_preview_relinks import load_importer
from publish_variant_transition import exclusive_storage_lock
from stage_model_preview_relinks import asset_root


def normalize(value):
    return str(value).replace('\\','/').casefold().removeprefix('//?/')


def strings(value):
    if isinstance(value,str):yield normalize(value)
    elif isinstance(value,list):
        for x in value:yield from strings(x)
    elif isinstance(value,dict):
        for x in value.values():yield from strings(x)


def active_audit(vault, plan, registry_raw, support_raw):
    targets={normalize(x['old']) for x in plan['candidate_paths']}
    relative={normalize(Path(x['old']).relative_to(vault)) for x in plan['candidate_paths']}
    support_pointer=json.loads(support_raw)
    source=storage.confined(vault,vault/support_pointer['path']).read_bytes()
    if storage.sha(source)!=support_pointer['sha256']:raise ValueError('source catalog SHA changed')
    if any(x in targets or x in relative for x in strings(json.loads(source))):
        raise ValueError('active source catalog references an old preview')
    old_catalogs={normalize(x['old']) for x in plan['catalogs']}
    registry=json.loads(registry_raw);cache={};cards=0
    for pack,entry in sorted(registry['packs'].items()):
        for name in ('details.v1.json','profiles.v1.json'):
            raw=storage.confined(vault,vault/entry['path']/name).read_bytes()
            if storage.sha(raw)!=entry['files'][name]:raise ValueError('active snapshot SHA changed')
            doc=json.loads(raw)
            if name!='details.v1.json':continue
            for card in doc['assets']:
                cat=storage.confined(vault,Path(card['source_catalog']))
                if normalize(cat) in old_catalogs:raise ValueError('old promotion catalog remains active')
                if cat not in cache:
                    data=cat.read_bytes();items=json.loads(data)['assets'];by_id={x['id']:x for x in items}
                    if len(by_id)!=len(items):raise ValueError('duplicate catalog ID')
                    cache[cat]=(storage.sha(data),by_id,items)
                item=cache[cat][1].get(card['id'])
                if not item or item.get('sha256')!=card.get('model_sha256'):
                    raise ValueError('active catalog/model mismatch')
                if item.get('preview'):
                    path=(storage.confined(vault,vault/Path(item['preview_vault'])) if item.get('preview_vault')
                          else asset_root(cat,card,item)/Path(item['preview']))
                    if normalize(path) in targets:raise ValueError('active model references an old preview')
                cards+=1
    for cat,(sha,_,items) in cache.items():
        if storage.sha(cat.read_bytes())!=sha:raise ValueError('active catalog changed')
        # A catalog remains a functional import input even for rows not active
        # today. Refuse direct relative references to a retiring image.
        for item in items:
            preview=item.get('preview')
            if isinstance(preview,str) and not item.get('preview_vault') and normalize(cat.parent/Path(preview)) in targets:
                raise ValueError('active catalog retains an old preview reference')
    return cards,len(cache)


def run(vault,plan_path,redirect_stage,proof_path,binary,importer,importer_sha,journal,remove):
    vault=vault.resolve(strict=True);plan_raw=plan_path.read_bytes();plan=json.loads(plan_raw)
    redirect_raw=(redirect_stage/'plan.json').read_bytes();redirect=json.loads(redirect_raw)
    proof_raw=proof_path.read_bytes();proof=json.loads(proof_raw)
    if (plan.get('schema')!='openrpg.cross-root-preview-stage.v1'
            or redirect_raw!=plan_raw
            or redirect.get('candidate_paths')!=plan['candidate_paths']
            or proof.get('state')!='passed' or proof.get('published') is not True
            or proof.get('stage_plan_sha256')!=storage.sha(plan_raw)
            or proof.get('old_paths_locked')!=len(plan['candidate_paths'])
            or proof.get('models')!=len({ident for row in plan['candidate_paths'] for ident in row['ids']})
            or proof.get('native_test_sha256')!=storage.sha(binary.read_bytes())
            or storage.sha(Path(importer.__file__).read_bytes())!=importer_sha):
        raise ValueError('preview proof or importer differs')
    if journal.exists():raise ValueError('journal already exists')
    for row in plan['candidate_paths']:
        old,keep=Path(row['old']),Path(row['retained'])
        if (not old.is_absolute() or not keep.is_absolute() or not old.is_relative_to(vault)
                or not keep.is_relative_to(vault) or old==keep
                or old.suffix.casefold()!='.png' or keep.suffix.casefold()!='.png'
                or old.relative_to(vault).parts[:2] not in (('normalized','hivemind'),('candidates','hivemind'))
                or not storage.valid_hex(row['sha256'],64)):
            raise ValueError('preview candidate outside permitted scope')
    old_paths={normalize(row['old']) for row in plan['candidate_paths']}
    if len(old_paths)!=len(plan['candidate_paths']) or any(normalize(row['retained']) in old_paths for row in plan['candidate_paths']):
        raise ValueError('duplicate or overlapping preview paths')
    free_before=shutil.disk_usage(vault).free
    with importer.locked(vault),exclusive_storage_lock(vault),contextlib.ExitStack() as stack:
        registry=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json')
        support=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json')
        rb,sb=registry.read_bytes(),support.read_bytes()
        if storage.sha(rb)!=proof['registry_sha256'] or storage.sha(sb)!=proof['source_registry_sha256']:
            raise ValueError('active registry changed')
        current=json.loads(rb)
        for pack,pin in redirect['packs'].items():
            if current['packs'][pack]['revision']!=pin['revision']:raise ValueError('catalog redirect no longer active')
        cards,catalogs=active_audit(vault,plan,rb,sb)
        for catalog in plan['catalogs']:
            old=storage.confined(vault,Path(catalog['old']));new=storage.confined(vault,Path(catalog['new']))
            stack.enter_context(storage.locked_source(old,share_delete=False))
            stack.enter_context(storage.locked_source(new,share_delete=False))
            try:
                redirected=importer.redirected_preview_catalog(vault,old,storage.sha(rb))
            except ValueError as error:
                if 'Stale derived preview catalog' not in str(error):raise
                origins=set()
                for entry in current['packs'].values():
                    details=storage.confined(vault,vault/entry['path']/'details.v1.json').read_bytes()
                    if storage.sha(details)!=entry['files']['details.v1.json']:raise ValueError('details changed')
                    for card in json.loads(details)['assets']:
                        if normalize(card['source_catalog'])==normalize(new):
                            origin=card.get('source_catalog_preview_origin')
                            if not origin:raise ValueError('missing original catalog mapping')
                            origins.add(Path(origin['path']))
                if not origins or any(importer.redirected_preview_catalog(vault,p,storage.sha(rb))!=new for p in origins):
                    raise ValueError('importer cannot redirect original catalog')
            else:
                if redirected!=new:raise ValueError('importer cannot redirect original catalog')
        old_handles={};kept={}
        for row in plan['candidate_paths']:
            old=storage.confined(vault,Path(row['old']));keep=storage.confined(vault,Path(row['retained']))
            if storage.windows_readonly(old.stat()):raise ValueError('read-only preview requires inspection')
            old_handles[old]=stack.enter_context(ExclusiveSource(old))
            if keep not in kept:kept[keep]=stack.enter_context(storage.locked_source(keep,share_delete=False))
        for row in plan['candidate_paths']:
            old,keep=Path(row['old']),Path(row['retained'])
            if old_handles[old].sha256()!=row['sha256']:raise ValueError('old preview SHA changed')
            kept[keep].seek(0)
            if hashlib.file_digest(kept[keep],'sha256').hexdigest()!=row['sha256']:
                raise ValueError('retained preview SHA changed')
        if (registry.read_bytes()!=rb or support.read_bytes()!=sb or plan_path.read_bytes()!=plan_raw
                or (redirect_stage/'plan.json').read_bytes()!=redirect_raw or proof_path.read_bytes()!=proof_raw):
            raise ValueError('inputs changed before retirement')
        if not remove:return {'state':'dry_run_passed','paths':len(old_handles),'active_models':cards,'active_catalogs':catalogs,'removed':0}
        recovery={'schema':'openrpg.model-preview-recovery.v1','source_plan_sha256':storage.sha(plan_raw),
                  'paths':[{'old':Path(x['old']).relative_to(vault).as_posix(),
                            'retained':Path(x['retained']).relative_to(vault).as_posix(),'sha256':x['sha256']} for x in plan['candidate_paths']]}
        recovery_raw=(json.dumps(recovery,ensure_ascii=False,sort_keys=True,indent=2)+'\n').encode('utf8')
        home=storage.confined(vault,vault/'metadata/asset-source-support-transactions')
        recovery_path=home/('model-preview-recovery-'+storage.sha(recovery_raw)+'.json')
        if recovery_path.exists():
            if storage.confined(vault,recovery_path).read_bytes()!=recovery_raw:raise ValueError('recovery mapping conflicts')
        else:
            with recovery_path.open('xb') as out:out.write(recovery_raw);out.flush();os.fsync(out.fileno())
            if recovery_path.read_bytes()!=recovery_raw:raise ValueError('recovery mapping readback failed')
        with journal.open('x',encoding='utf8',newline='\n') as log:
            def record(item):
                log.write(json.dumps(item,ensure_ascii=False)+'\n');log.flush();os.fsync(log.fileno())
            record({'state':'prepared','count':len(old_handles),'recovery':str(recovery_path),'free_before':free_before})
            for row in plan['candidate_paths']:
                record({'state':'verified','old':row['old'],'retained':row['retained'],'sha256':row['sha256']})
                old_handles[Path(row['old'])].mark_delete()
                record({'state':'marked_delete','old':row['old']})
            record({'state':'marked_pending_handle_close','count':len(old_handles)})
    free_after=shutil.disk_usage(vault).free
    with journal.open('a',encoding='utf8') as f:
        f.write(json.dumps({'state':'handles_closed','free_after':free_after})+'\n');f.flush();os.fsync(f.fileno())
    return {'state':'removed_pending_verification','removed':len(plan['candidate_paths']),
            'recovery':str(recovery_path),'free_before':free_before,'free_after':free_after,
            'free_delta_observed':free_after-free_before}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','plan','redirect-stage','proof','binary','journal','output'):
        p.add_argument('--'+n,type=Path,required=True)
    p.add_argument('--importer-sha256',required=True);p.add_argument('--remove',action='store_true')
    a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=run(a.vault,a.plan,a.redirect_stage,a.proof,a.binary,load_importer(a.importer_sha256),a.importer_sha256,a.journal,a.remove)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps(result))
