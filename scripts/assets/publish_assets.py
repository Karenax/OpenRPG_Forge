"""Publish with the canonical importer, then compact profitable, immutable models.

No shared skill is modified. A busy storage writer defers work into one small
SQLite queue; the running migration or the next publication drains that queue.
"""
import argparse
import contextlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile

import vault_storage as storage

def check_consolidated_identities(vault, stage):
    """Prevent a normal import from undoing a published consolidation.

    Only the current registry and immutable stage are consulted. The canonical
    importer rechecks the same registry SHA while holding its publication lock.
    """
    vault, stage = Path(vault), Path(stage)
    plan = storage.decode_json(storage.confined(vault, stage/'plan.json').read_bytes())
    staged_raw = storage.confined(vault, stage/'details.v1.json').read_bytes()
    if storage.sha(staged_raw) != plan['files']['details.v1.json']:
        raise ValueError('Publication stage details SHA differs')
    stage_doc = storage.decode_json(staged_raw)
    if stage_doc.get('pack_id') != plan['pack_id']:
        raise ValueError('Publication stage pack differs')
    index = vault/'metadata/asset-import-registry.v1.json'
    if not index.exists():
        before = b''
        registry = {'packs': {}}
    else:
        before = storage.confined(vault, index).read_bytes()
        registry = storage.decode_json(before)
    if storage.sha(before) != plan['expected_registry_sha256']:
        # Replaying an already applied stage is safe only when the active
        # immutable snapshot is exactly that stage. This lets the canonical
        # importer return "unchanged" and drain deferred storage work, while
        # a later revision or altered metadata still fails closed.
        current = registry['packs'].get(plan['pack_id'])
        if not current or current.get('revision') != plan.get('revision') or current.get('files') != plan['files']:
            raise ValueError('Registry changed; prepare a new plan')
        for name, pin in plan['files'].items():
            staged = storage.confined(vault, stage/name).read_bytes()
            active = storage.confined(vault, vault/current['path']/name).read_bytes()
            if storage.sha(staged) != pin or storage.sha(active) != pin:
                raise ValueError('Already applied snapshot differs from stage')
        if storage.confined(vault,index).read_bytes() != before:
            raise ValueError('Registry changed during replay verification')
        return {'state':'already_applied','revision':plan['revision']}
    current, resulting = {}, {}
    def add(target, card):
        ident = card['id']
        if ident in target:
            raise ValueError('Duplicate active asset identity')
        target[ident] = card
    for pack, entry in registry['packs'].items():
        data = storage.confined(vault, vault/entry['path']/'details.v1.json').read_bytes()
        if storage.sha(data) != entry['files']['details.v1.json']:
            raise ValueError('Active details SHA differs')
        for card in storage.decode_json(data)['assets']:
            add(current, card)
            if pack != plan['pack_id']:
                add(resulting, card)
    for card in stage_doc['assets']:
        add(resulting, card)
    for ident, card in current.items():
        aliases = card.get('consolidated_variants', [])
        if aliases and (ident not in resulting or resulting[ident].get('consolidated_variants') != aliases):
            raise ValueError('Publication would discard consolidated historical references: ' + ident)
    seen = set()
    for card in resulting.values():
        aliases = card.get('consolidated_variants', [])
        if not isinstance(aliases, list):
            raise ValueError('Invalid consolidated reference list')
        for alias in aliases:
            ident = alias.get('retired_id')
            if not isinstance(ident, str) or ident in seen or ident in resulting:
                raise ValueError('Publication would recreate or duplicate a consolidated identity: ' + str(ident))
            seen.add(ident)
    return {'active_assets': len(resulting), 'historical_ids': len(seen)}

def publication_models(stage_plan, details):
    """Queue only assets measured in this publication, not retained merge entries."""
    assets=details['assets']
    merge=stage_plan.get('merge')
    if merge is None:
        return [(row['model_path'],row['model_sha256']) for row in assets]
    measured=merge.get('measured_asset_ids')
    if not isinstance(measured,list) or not measured or len(measured)!=len(set(measured)):
        raise ValueError('Invalid measured asset IDs in publication stage')
    selected=set(measured)
    matches=[row for row in assets if row['id'] in selected]
    if len(matches)!=len(selected) or {row['id'] for row in matches}!=selected:
        raise ValueError('Publication stage asset IDs do not match published details')
    return [(row['model_path'],row['model_sha256']) for row in matches]

def publish(vault,stage,importer,verifier,work_root):
    vault=Path(vault).absolute();storage.confined(vault,vault)
    stage=Path(stage)
    if not stage.is_absolute():stage=vault/stage
    env=dict(os.environ,OPENRPG_ASSET_VAULT=str(vault),OPENRPG_FORGE_ROOT=str(Path(__file__).resolve().parents[3]))
    command=[sys.executable,str(importer/'asset_import.py'),'--vault',str(vault),'apply','--stage',str(stage)]
    with contextlib.ExitStack() as handles:
        for name in ('plan.json', 'details.v1.json', 'profiles.v1.json'):
            handles.enter_context(storage.locked_source(storage.confined(vault, stage/name), share_delete=False))
        check_consolidated_identities(vault, stage)
        result=subprocess.run(command,capture_output=True,text=True,encoding='utf-8',env=env)
    applied=json.loads(result.stdout)
    if result.returncode:raise ValueError('Canonical publication refused: '+json.dumps(applied,ensure_ascii=False))
    try:
        result=optimize_published(vault,stage,verifier,work_root,applied)
    except (ValueError,OSError,KeyError) as error:
        result=dict(publication=applied,storage='deferred',reason=str(error))
    # Publishing and compacting do not prove that E: staging copies were retired.
    # Keep this separate in the machine-readable result so callers cannot close
    # a conversion solely from publication=applied/storage=processed.
    result.update(work_retirement='required',closure='open',storage_policy='docs/ASSET_STORAGE_POLICY.md')
    return result

def optimize_published(vault,stage,verifier,work_root,applied):
    receipt=vault/'.forge-storage/deployed-readers.v1.json'
    if not receipt.exists():
        return dict(publication=applied,storage='deferred',reason='compatible storage readers not deployed')
    storage.confined(vault,receipt)
    readiness=storage.decode_json(receipt.read_bytes())
    storage.verify_deployed_readers(readiness,vault,verifier)
    # Resolve the current, pinned pack after publication; preserve all IDs and rights.
    stage_plan=storage.decode_json(storage.confined(vault,Path(stage)/'plan.json').read_bytes())
    registry=storage.decode_json(storage.confined(vault,vault/'metadata/asset-import-registry.v1.json').read_bytes())
    entry=registry['packs'][stage_plan['pack_id']]
    details_path=storage.confined(vault,vault/entry['path']/'details.v1.json')
    raw=details_path.read_bytes()
    if storage.sha(raw)!=entry['files']['details.v1.json']:raise ValueError('Published details pin changed')
    details=storage.decode_json(raw)
    models=publication_models(stage_plan,details)
    queued=storage.queue_models(vault,models)
    if (vault/'.forge-storage/WRITER.lock').exists():
        return dict(publication=applied,storage='queued',models_queued=queued)
    # The caller holds no importer lock here, so conversion publication remains independent.
    with tempfile.TemporaryDirectory(prefix='vault-publication-',dir=work_root) as work:
        work=Path(work);plan=work/'plan.json';report=work/'result.json'
        storage.atomic_json(plan,dict(schema=1,root=str(vault),logical_bytes=0,files=[],utc=storage.utc()))
        run=subprocess.run([sys.executable,str(Path(storage.__file__)),'apply','--plan',str(plan),'--verify-cli',str(verifier),'--readers-receipt',str(receipt),'--report',str(report)],capture_output=True,text=True,encoding='utf-8')
        if run.returncode:
            # Keep functional F: transaction records and queue entries for recovery.
            return dict(publication=applied,storage='deferred',reason=run.stderr[-2000:],models_queued=queued)
        summary=json.loads(run.stdout)
        return dict(publication=applied,storage='processed',compacted_models=summary['files'],pending_models=summary.get('pending_publications',0),skipped_models=len(json.loads(report.read_bytes()).get('skipped',[])))

def main():
    repository=Path(__file__).resolve().parents[2]
    workspace=repository.parent
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault',type=Path,default=Path(os.environ.get('OPENRPG_ASSET_VAULT','F:/OpenRPG_AssetVault')))
    p.add_argument('--stage',type=Path,required=True)
    p.add_argument('--importer-scripts',type=Path,default=Path(os.environ.get('OPENRPG_IMPORTER_SCRIPTS',str(Path.home()/'.codex/skills/openrpg-import-assets/scripts'))))
    p.add_argument('--verify-cli',type=Path,default=workspace/'.build/cargo-target/release/examples/vault_storage.exe')
    p.add_argument('--work-root',type=Path,default=workspace/'.build/tmp')
    args=p.parse_args()
    print(json.dumps(publish(args.vault,args.stage,args.importer_scripts,args.verify_cli,args.work_root),ensure_ascii=False))

if __name__=='__main__':main()
