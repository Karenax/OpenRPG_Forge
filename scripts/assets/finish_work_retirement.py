"""Retire E: copies incrementally as their Vault counterparts become verified.

Not a scheduler or an age purge. No Vault scan, new migration or process kill.
The worker hashes the frozen E: inventory and consumes only complete migration
receipts, with fresh reference/process checks for each retirement pass.
"""
import argparse
import json
import os
import shutil
from pathlib import Path
import time

import retire_work_duplicates as retirement
import vault_storage as storage


def dependency_state(value, expected_plan):
    if value.get('plan_sha256')!=expected_plan:raise ValueError('Migration plan changed')
    if value.get('state')=='interrupted':raise ValueError('Migration interrupted: '+value.get('error','unknown error'))
    if value.get('state')=='complete' and value.get('phase')=='final':return True
    if value.get('state')!='running':raise ValueError('Unexpected migration state')
    return False


def completed_receipts(path):
    rows=[]
    with Path(path).open('rb') as stream:
        for line in stream:
            # A concurrent writer may not have finished its last record yet.
            if not line.endswith(b'\n'):break
            row=json.loads(line)
            if row.get('state')=='complete':rows.append(row)
    return rows


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--inventory',type=Path,required=True)
    p.add_argument('--migration-report',type=Path,required=True)
    p.add_argument('--migration-journal',type=Path,required=True)
    p.add_argument('--output-dir',type=Path,required=True)
    a=p.parse_args();repository=Path(__file__).resolve().parents[2]
    root=repository.parent/'.build/asset-conversion/deterministic_import/jobs'
    out=a.output_dir;out.mkdir(parents=True,exist_ok=True)
    lock=out/'finish-worker.lock'
    with lock.open('x') as f:json.dump(dict(pid=os.getpid(),utc=storage.utc()),f)
    state=dict(state='running',phase='hashing-E',pid=os.getpid(),started_utc=storage.utc())
    def save(**values):
        state.update(values,updated_utc=storage.utc());storage.atomic_json(out/'finish-state.json',state)
    try:
        sources={str(Path(p)):storage.sha(Path(p).read_bytes()) for p in (__file__,retirement.__file__,storage.__file__)}
        initial=json.loads(a.migration_report.read_bytes());expected=initial['plan_sha256']
        dependency_state(initial,expected)
        save(migration_plan_sha256=expected,qualified_sources=sources)
        retirement.hash_inventory(a.inventory,root,out/'hashed.jsonl',lambda count,total:save(hashed=count,planned=total))
        hashes=[json.loads(s) for s in (out/'hashed.jsonl').read_text().splitlines()]
        cumulative=dict(utc=storage.utc(),free_before=shutil.disk_usage(root).free,removed=[],excluded=[])
        previous=out/'incremental-retirement.json'
        if previous.exists():cumulative=json.loads(previous.read_bytes())
        retired={x['source']['path'] for x in cumulative['removed']}
        for name in ('retrait.json','retrait-incremental-02.json'):
            prior=out/name
            if prior.exists():retired.update(x['source']['path'] for x in json.loads(prior.read_bytes()).get('removed',[]))
        hashes=[row for row in hashes if row['path'] not in retired]
        save(phase='waiting-for-verified-receipts')
        deadline=time.monotonic()+48*3600
        last_count=-1;last_sweep=0
        while True:
            migration=json.loads(a.migration_report.read_bytes())
            final=dependency_state(migration,expected)
            if time.monotonic()>deadline:raise ValueError('Migration wait exceeded 48 hours; remaining copies preserved')
            # Only query process identity when the E: status is stale.
            if not final and time.time()-a.migration_report.stat().st_mtime>300:
                commands=json.loads(retirement.active_commands())
                commands=commands if isinstance(commands,list) else [commands]
                matching=[x for x in commands if x.get('processid')==migration.get('pid')]
                if not matching or 'vault_storage.py' not in (matching[0].get('commandline') or '') or str(a.migration_report).lower() not in (matching[0].get('commandline') or ''):
                    raise ValueError('Migration status stale and expected process absent; no deletion attempted')
            count=migration.get('files',0)
            # Cadence limits metadata rereads, not how much data may be retained.
            if final or (count!=last_count and time.monotonic()-last_sweep>=300):
                if any(storage.sha(Path(p).read_bytes())!=pin for p,pin in sources.items()):
                    raise ValueError('Qualified retirement tools changed; restart explicitly')
                save(phase='planning-retirement',migration_files=count)
                receipts=completed_receipts(a.migration_journal)
                plan=retirement.make_plan(root,repository,hashes,receipts,retirement.active_commands())
                storage.atomic_json(out/('final-plan.json' if final else 'incremental-plan.json'),plan)
                save(phase='applying-retirement',planned_retirements=len(plan['files']))
                result=retirement.apply_plan(plan,Path(initial['vault']),retirement.active_commands(),out/'final-retirement.jsonl')
                cumulative['removed'].extend(result['removed']);cumulative['excluded']=result['excluded']
                retired.update(x['source']['path'] for x in result['removed'])
                hashes=[row for row in hashes if row['path'] not in retired]
                cumulative.update(free_after=result['free_after'],logical_bytes=sum(x['source']['bytes'] for x in cumulative['removed']),updated_utc=storage.utc())
                storage.atomic_json(previous,cumulative)
                save(removed=len(cumulative['removed']),logical_bytes=cumulative['logical_bytes'],free_after=cumulative['free_after'])
                last_count=count;last_sweep=time.monotonic()
            if final:break
            save(phase='waiting-for-verified-receipts',migration_files=count);time.sleep(30)
        storage.atomic_json(out/'final-retirement.json',cumulative)
        save(state='complete',phase='final',removed=len(cumulative['removed']),logical_bytes=cumulative['logical_bytes'],free_before=cumulative['free_before'],free_after=cumulative['free_after'],excluded=len(cumulative['excluded']))
    except BaseException as error:
        save(state='interrupted',error=str(error));raise
    finally:
        lock.unlink()


if __name__=='__main__':main()
