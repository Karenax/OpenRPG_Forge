"""Retire archived HDRP extractions after a checked URP catalogue publication."""
import argparse
import contextlib
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

import vault_storage as storage
from exclusive_source_delete import ExclusiveSource
from publish_source_support_relinks import importer_module
from publish_variant_transition import exclusive_storage_lock
from retire_relinked_source_copies import active_references
from verify_published_source_aliases import verify as verify_reader


def run(vault, stage, proof_paths, reader_proof_path, binary, studio, importer, journal, remove,
        skip_unity_tree=False, retained_sources=()):
    vault = vault.resolve(strict=True)
    stage = stage.resolve(strict=True)
    plan_raw = (stage/'plan.json').read_bytes()
    plan = json.loads(plan_raw)
    reader_raw = reader_proof_path.read_bytes()
    reader = json.loads(reader_raw)
    if (plan.get('schema') != 'openrpg.archived-effect-variant-stage.v1'
            or reader.get('state') != 'passed' or reader.get('stage_plan_sha256') != storage.sha(plan_raw)
            or reader.get('pairs') != len(plan['pairs'])
            or reader.get('studio_binary_sha256') != storage.sha(studio.read_bytes())
            or reader.get('native_test_sha256') != storage.sha(binary.read_bytes())):
        raise ValueError('published reader proof differs')
    proof_raw = [p.read_bytes() for p in proof_paths]
    if [storage.sha(x) for x in proof_raw] != plan['archive_proof_sha256']:
        raise ValueError('archive proof hashes changed')
    proofs = [json.loads(x) for x in proof_raw]
    entries = {}
    for proof in proofs:
        if proof.get('state') != 'passed' or proof['count'] != len(proof['assets']):
            raise ValueError('archive proof incomplete')
        for row in proof['assets']:
            if row['id'] in entries:
                raise ValueError('duplicate archive proof identity')
            entries[row['id']] = (row, proof['archive'], proof['archive_sha256'])
    if {p['hdrp_id'] for p in plan['pairs']} != set(entries):
        raise ValueError('archive proof coverage differs')
    staged_raw = (stage/'source-support.v1.json').read_bytes()
    if storage.sha(staged_raw) != plan['new_catalog_sha256']:
        raise ValueError('staged catalogue changed')
    staged = {c['id']:c for c in json.loads(staged_raw)['assets']}
    targets = {}
    shared = set()
    excluded = set()
    retained_sources = {x.replace('\\','/').casefold() for x in retained_sources}
    found_retained = set()
    for pair in plan['pairs']:
        row, archive, archive_sha = entries[pair['hdrp_id']]
        source_path = row['source_file'].replace('\\','/').casefold()
        if source_path in retained_sources:
            found_retained.add(source_path)
            continue
        if '/source/' in source_path and '/assets/' in '/'+source_path.split('/source/',1)[1]:
            if not skip_unity_tree:
                raise ValueError('Unity asset tree requires a qualified consumer migration')
            excluded.add(source_path)
            continue
        retained = staged[pair['urp_id']]
        for key, pin, kind in (('source_file','source_sha256','asset'),
                               ('source_meta_file','source_meta_sha256','asset.meta')):
            rel = row[key].replace('\\','/')
            if retained.get(key, '').replace('\\','/').casefold() == rel.casefold():
                if retained.get(pin) != row[pin]:
                    raise ValueError('shared retained payload has conflicting hash')
                shared.add(rel)
                continue
            allowed = ('originals/unity-purchased/hivemind/effect-source-support-v1/',
                       'originals/unity-purchased/hivemind/source-support-remaining-v1/files/',
                       'originals/unity-purchased/hivemind/source-support-townsmith-v1/')
            if not rel.startswith(allowed):
                raise ValueError('outside independently stored Unity source scope')
            if not storage.valid_hex(row[pin], 64):
                raise ValueError('invalid source hash')
            candidate = {'old':rel,'sha256':row[pin], 'guid':row['guid'],
                         'archive':archive,'archive_sha256':archive_sha,'archive_member':kind,
                         'source_asset_path':row['source_asset_path']}
            if rel in targets and targets[rel] != candidate:
                raise ValueError('shared source has conflicting recovery data')
            targets[rel] = candidate
    if not targets:
        raise ValueError('no eligible source files to retire')
    if found_retained != retained_sources:
        raise ValueError('explicit retained source not found in archive proofs')
    if journal.exists():
        raise ValueError('journal already exists')
    before = shutil.disk_usage(vault).free
    with importer.shared_import_lock(vault), exclusive_storage_lock(vault), contextlib.ExitStack() as stack:
        source_raw, model_raw = active_references(vault, {x.casefold() for x in targets})
        if (storage.sha(source_raw) != reader['source_registry_sha256']
                or storage.sha(model_raw) != reader['asset_registry_sha256']
                or json.loads(source_raw)['sha256'] != plan['new_catalog_sha256']):
            raise ValueError('active registry differs from published proof')
        queue = Path('F:/OpenRPG_ImportWork/queue')
        if not queue.is_dir():
            raise ValueError('active import queue missing')
        with tempfile.TemporaryDirectory(dir='E:/OpenRPG_Forge/.build/asset-conversion') as temp:
            patterns = Path(temp)/'patterns.txt'
            patterns.write_text(''.join(rel+'\n'+rel.replace('/','\\')+'\n' for rel in targets),encoding='utf8')
            scan = subprocess.run(['rg','-l','-i','-F','-f',str(patterns),str(queue),'-g','*.json','-g','*.py'],
                                  capture_output=True,text=True,encoding='utf8',errors='replace')
        if scan.returncode not in (0,1) or scan.stdout:
            raise ValueError('import queue references selected sources or could not be checked')
        archive_handles = {}
        for row in targets.values():
            archive = storage.confined(vault, vault/row['archive'])
            if archive not in archive_handles:
                stream = stack.enter_context(storage.locked_source(archive, share_delete=False))
                if hashlib.file_digest(stream,'sha256').hexdigest() != row['archive_sha256']:
                    raise ValueError('archive changed')
                archive_handles[archive] = stream
        handles = {}
        total = 0
        for rel, row in targets.items():
            path = storage.confined(vault, vault/rel)
            if storage.windows_readonly(path.stat()):
                raise ValueError('read-only source requires inspection')
            handle = stack.enter_context(ExclusiveSource(path))
            if handle.sha256() != row['sha256']:
                raise ValueError('extracted source changed: '+rel)
            row['bytes'] = path.stat().st_size
            total += row['bytes']
            handles[rel] = handle
        # Prove the published Studio reader works while all old paths are locked.
        locked_proof = verify_reader(vault,stage,binary,studio)
        if locked_proof['source_registry_sha256'] != reader['source_registry_sha256']:
            raise ValueError('locked reader test differs')
        if ((vault/'metadata/asset-source-support-registry.v1.json').read_bytes() != source_raw
                or (vault/'metadata/asset-import-registry.v1.json').read_bytes() != model_raw
                or (stage/'plan.json').read_bytes() != plan_raw
                or reader_proof_path.read_bytes() != reader_raw
                or any(p.read_bytes() != raw for p,raw in zip(proof_paths,proof_raw))):
            raise ValueError('retirement inputs changed')
        result = {'files':len(targets),'shared_retained':len(shared),'unity_tree_sources_excluded':len(excluded),
                  'referenced_sources_retained':len(found_retained),
                  'logical_bytes':total,'rows':list(targets.values()),
                  'source_registry_sha256':storage.sha(source_raw),'reader_locked':'passed'}
        if not remove:
            return dict(result,state='dry_run_passed')
        txdir = storage.confined(vault, vault/'metadata/asset-source-support-transactions')
        recovery = txdir/('archived-effects-recovery-'+storage.sha(plan_raw)+'.json')
        if recovery.exists():
            raise ValueError('recovery already exists')
        with recovery.open('xb') as f:
            f.write((json.dumps(result,ensure_ascii=False,indent=2)+'\n').encode('utf8'))
            f.flush();os.fsync(f.fileno())
        with journal.open('x',encoding='utf8') as log:
            def record(value):
                log.write(json.dumps(value,ensure_ascii=False)+'\n');log.flush();os.fsync(log.fileno())
            record({'state':'prepared','recovery':str(recovery),'free_before':before})
            for rel, row in targets.items():
                record({'state':'verified',**row})
                handles[rel].mark_delete()
                record({'state':'marked_delete','old':rel})
    if any((vault/rel).exists() for rel in targets):
        raise ValueError('old source remains after handle close')
    return dict(result,state='removed_verified',recovery=str(recovery),
                free_delta_observed=shutil.disk_usage(vault).free-before)


if __name__ == '__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','stage','reader-proof','binary','studio','journal','output'):
        p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--archive-proof',type=Path,action='append',required=True)
    p.add_argument('--importer-sha256',required=True)
    p.add_argument('--remove',action='store_true')
    p.add_argument('--skip-unity-tree',action='store_true')
    p.add_argument('--retain-source',action='append',default=[])
    a=p.parse_args()
    if a.output.exists():p.error('output already exists')
    result=run(a.vault,a.stage,a.archive_proof,a.reader_proof,a.binary,a.studio,
               importer_module(a.importer_sha256),a.journal,a.remove,a.skip_unity_tree,a.retain_source)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps({k:v for k,v in result.items() if k!='rows'}))
