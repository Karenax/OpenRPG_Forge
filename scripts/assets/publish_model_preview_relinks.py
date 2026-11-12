"""Publish verified model-preview catalog transitions atomically, without deletion."""
import argparse
import contextlib
import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import sys
import uuid

import vault_storage as storage
from publish_variant_transition import ensure_vault_directory, exclusive_storage_lock
from verify_model_preview_stage import verify
from verify_model_catalog_redirects import verify as verify_redirects
from verify_cross_root_preview_stage import verify as verify_cross_root
from verify_nested_archive_catalog_relinks import verify as verify_nested_archive
from verify_model_prefab_relinks import verify as verify_prefab
from verify_viking_fbx_relinks import verify as verify_viking

IMPORTER = Path(r'E:\CodexData\skills\openrpg-import-assets\scripts\asset_import.py')


def load_importer(expected):
    if storage.sha(IMPORTER.read_bytes()) != expected:
        raise ValueError('canonical importer changed')
    sys.path.insert(0, str(IMPORTER.parent))
    spec = importlib.util.spec_from_file_location('model_preview_canonical_import', IMPORTER)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def publish(vault, stage, proof_path, native_test, importer, importer_sha, studio=None, audit=None, recovery=None, candidates=None, rebuilt=None):
    vault, stage = vault.resolve(strict=True), stage.resolve(strict=True)
    plan_raw, proof_raw = (stage/'plan.json').read_bytes(), proof_path.read_bytes()
    plan, proof = json.loads(plan_raw), json.loads(proof_raw)
    cross_root = plan.get('schema') == 'openrpg.cross-root-preview-stage.v1'
    nested_archive = plan.get('schema') == 'openrpg.nested-archive-relink-stage.v1'
    prefab = plan.get('schema') == 'openrpg.model-prefab-relink-stage.v1'
    viking = plan.get('schema') == 'openrpg.viking-fbx-relink-stage.v1'
    if nested_archive and (audit is None or recovery is None):
        raise ValueError('nested archive audit and recovery required')
    if prefab and (audit is None or candidates is None or studio is None
                   or proof.get('studio_binary_sha256') != storage.sha(studio.read_bytes())):
        raise ValueError('prefab audit, candidates and qualified Studio required')
    if viking and (audit is None or candidates is None or rebuilt is None or studio is None
                   or proof.get('studio_binary_sha256') != storage.sha(studio.read_bytes())):
        raise ValueError('Viking audit, candidates, rebuild path and qualified Studio required')
    if cross_root and (studio is None or proof.get('studio_binary_sha256') != storage.sha(studio.read_bytes())
            or proof.get('old_paths_locked') != len(plan['candidate_paths'])):
        raise ValueError('qualified installed Studio and exclusive preview test required')
    if (proof.get('state') != 'passed' or proof.get('plan_sha256' if nested_archive or prefab or viking else 'stage_plan_sha256') != storage.sha(plan_raw)
            or proof.get('native_test_sha256') != storage.sha(native_test.read_bytes())
            or proof.get('registry_sha256') != plan['registry_sha256']
            or proof.get('source_registry_sha256') != plan['source_registry_sha256']
            or proof.get('cards' if nested_archive or prefab or viking else 'models') != len(plan['preview_ids'] if cross_root else plan['changed_ids'])
            or storage.sha(Path(importer.__file__).read_bytes()) != importer_sha):
        raise ValueError('reader proof or importer differs')
    with importer.locked(vault), exclusive_storage_lock(vault), contextlib.ExitStack() as handles:
        schema = plan.get('schema')
        if schema == 'openrpg.model-preview-relink-stage.v1':
            verify(vault, stage)
        elif schema == 'openrpg.model-catalog-redirect-stage.v1':
            verify_redirects(vault, stage)
        elif cross_root:
            verify_cross_root(vault, stage)
        elif nested_archive:
            verify_nested_archive(vault, audit, recovery, stage)
        elif prefab:
            verify_prefab(vault, candidates, audit, stage)
        elif viking:
            verify_viking(vault, candidates, audit, stage, rebuilt)
        else:
            raise ValueError('unsupported transition schema')
        for item in plan['catalogs']:
            path = storage.confined(vault, Path(item['new']))
            if storage.sha(path.read_bytes()) != item['new_sha256']:
                raise ValueError('materialized catalog changed')
            handles.enter_context(storage.locked_source(path, share_delete=False))
        held = {}
        for pair in plan['archive_rows' if nested_archive else 'paths' if prefab or viking else 'candidate_paths']:
            for side in ('old','retained'):
                path = storage.confined(vault, (vault/Path(pair[side])) if prefab or viking else Path(pair[side]))
                if path not in held:
                    held[path] = handles.enter_context(storage.locked_source(path, share_delete=False))
                stream = held[path]
                stream.seek(0)
                if hashlib.file_digest(stream,'sha256').hexdigest() != pair['sha256']:
                    raise ValueError('preview SHA changed')
        registry_path = storage.confined(vault, vault/'metadata/asset-import-registry.v1.json')
        support_path = storage.confined(vault, vault/'metadata/asset-source-support-registry.v1.json')
        registry_raw, support_raw = registry_path.read_bytes(), support_path.read_bytes()
        if storage.sha(registry_raw) != plan['registry_sha256'] or storage.sha(support_raw) != plan['source_registry_sha256']:
            raise ValueError('registry changed')
        current = json.loads(registry_raw)
        next_registry = copy.deepcopy(current)
        events = []
        revisions = ensure_vault_directory(vault, ['metadata','asset-import-revisions'])
        for pack, pins in sorted(plan['packs'].items()):
            importer.safe_id(pack)
            parent = ensure_vault_directory(vault, ['metadata','asset-import-revisions',pack])
            target = parent/pins['revision']
            if not target.exists():
                target.mkdir()
            storage.confined(vault,target)
            files = {}
            for filename in ('details.v1.json','profiles.v1.json'):
                raw = (stage/pack/filename).read_bytes()
                if storage.sha(raw) != pins[filename]:
                    raise ValueError('stage file changed')
                dest = target/filename
                if dest.exists():
                    if storage.sha(storage.confined(vault,dest).read_bytes()) != pins[filename]:
                        raise ValueError('existing snapshot conflicts')
                else:
                    with dest.open('xb') as stream:
                        stream.write(raw)
                        stream.flush()
                        os.fsync(stream.fileno())
                files[filename] = pins[filename]
            tx = uuid.uuid4().hex
            previous = current['packs'][pack]
            entry = {'revision':pins['revision'],'path':target.relative_to(vault).as_posix(),
                     'files':files,'transaction':tx}
            entry['history'] = importer.retained_history(vault, previous, entry)
            next_registry['packs'][pack] = entry
            events.append({'schema_version':1,'operation':'apply','pack_id':pack,
                           'previous':previous,'next':entry,'state':'prepared'})
        if (registry_path.read_bytes() != registry_raw or support_path.read_bytes() != support_raw
                or (stage/'plan.json').read_bytes() != plan_raw or proof_path.read_bytes() != proof_raw
                or storage.sha(Path(importer.__file__).read_bytes()) != importer_sha
                or ((cross_root or prefab or viking) and storage.sha(studio.read_bytes()) != proof['studio_binary_sha256'])):
            raise ValueError('inputs changed before publication')
        group_id = uuid.uuid4().hex
        group_home = ensure_vault_directory(vault,['metadata','asset-consolidation-history'])
        event_home = ensure_vault_directory(vault,['metadata','asset-import-history'])
        group_path = group_home/(group_id+'.json')
        group = {'schema':'openrpg.asset-consolidation-transaction.v1','state':'prepared',
                 'previous_registry_sha256':plan['registry_sha256'],'events':events,
                 'purpose':'viking-fbx-relinks' if viking else 'model-prefab-relinks' if prefab else 'nested-archive-catalog-relinks' if nested_archive else 'identical-model-preview-relinks',
                 'reader_proof_sha256':storage.sha(proof_raw)}
        importer.atomic(group_path, group)
        for event in events:
            importer.atomic(event_home/(event['next']['transaction']+'.json'),event)
        if registry_path.read_bytes() != registry_raw or support_path.read_bytes() != support_raw:
            raise ValueError('registry changed before atomic swap')
        importer.atomic(registry_path,next_registry)
        group['state'] = 'committed'
        importer.atomic(group_path,group)
        for event in events:
            event['state'] = 'committed'
            importer.atomic(event_home/(event['next']['transaction']+'.json'),event)
        return {'state':'committed','group':group_id,'packs':len(events),
                'models':len(plan['changed_ids']),'files_removed':0}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for name in ('vault','stage','proof','native-test','output'):
        p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--importer-sha256',required=True)
    p.add_argument('--studio',type=Path)
    p.add_argument('--audit',type=Path)
    p.add_argument('--recovery',type=Path)
    p.add_argument('--candidates',type=Path)
    p.add_argument('--rebuilt',type=Path)
    a = p.parse_args()
    if a.output.exists():p.error('output exists')
    result = publish(a.vault,a.stage,a.proof,a.native_test,load_importer(a.importer_sha256),a.importer_sha256,a.studio,a.audit,a.recovery,a.candidates,a.rebuilt)
    with a.output.open('x',encoding='utf8') as f:
        json.dump(result,f,indent=2)
        f.write('\n')
    print(json.dumps(result))
