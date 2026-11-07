"""Reconcile current physical files with the pinned completed storage audit.

Unchanged stat records reuse historical SHA evidence; only new/changed files are
read now. This is a delta inventory, not a fresh whole-Vault SHA qualification.
"""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import os

W = Path('F:/OpenRPG_ImportWork')
V = Path('F:/OpenRPG_AssetVault').resolve()
O = W / 'vault-current-delta-audit-v895'

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def save(name, value):
    (O / name).write_text(json.dumps(value, indent=2)+'\n', encoding='utf-8')

def main():
    O.mkdir(exist_ok=False)
    save('JOB.v1.json', dict(pid=os.getpid(), script=str(Path(__file__)), utc=datetime.now(timezone.utc).isoformat()))
    reopened = read(W / 'historical-vault-transfer-proof-reopen-v763/RESULT.v1.json')
    prior_path = W / 'vault-current-final-review-v4/RESULT.v1.json'
    anchor = next(row for row in reopened['observations'] if Path(row['path']) == prior_path)
    assert sha(prior_path) == anchor['expected_sha256']
    prior = read(prior_path)
    assert prior['whole_current_vault_qualified']
    pins = prior['source_pins']
    used = {}
    def pinned(path):
        path = Path(path)
        expected = pins[str(path)]
        assert sha(path) == expected, path
        used[str(path)] = expected
        return path
    inventory_path = pinned(W / 'vault-physical-batch-plan-v1/INVENTORY.jsonl')
    inventory = {r['path']: r for r in map(json.loads, inventory_path.read_text('utf-8-sig').splitlines())}
    covered = read(pinned(W / 'vault-indexed-coverage-plan-v1/COVERED.json'))
    hashes = {row['path']: row['expected_sha256'] for row in covered}
    plan = read(pinned(W / 'vault-indexed-coverage-plan-v1/RESULT.v1.json'))
    for index, batch in enumerate(plan['remaining_batches']):
        base = W / 'vault-indexed-coverage-plan-v1' / ('checks' if index < 197 else 'checks-v2')
        receipt_path = base / f'{index:05d}.RESULT.v1.json'
        receipt = read(pinned(receipt_path))
        ledger = pinned(receipt_path.with_suffix('.jsonl'))
        assert sha(ledger) == receipt['ledger_sha256']
        rows = list(map(json.loads, ledger.read_text('utf-8-sig').splitlines()))
        assert len(rows) == receipt['checked_files'] == batch['files']
        for row in rows:
            assert row['path'] not in hashes
            hashes[row['path']] = row['actual_current_sha256']
    assert set(hashes) == set(inventory) and len(inventory) == prior['physical_files']
    live, excluded, stack = {}, [], [V]
    while stack:
        directory = stack.pop()
        with os.scandir(directory) as entries:
            for entry in entries:
                stat = entry.stat(follow_symlinks=False)
                relative = Path(entry.path).relative_to(V).as_posix()
                if entry.is_symlink() or getattr(stat, 'st_file_attributes', 0) & 0x400:
                    excluded.append(relative)
                elif entry.is_dir(follow_symlinks=False):
                    stack.append(Path(entry.path))
                elif entry.is_file(follow_symlinks=False):
                    live[relative] = (stat.st_size, stat.st_mtime_ns)
    missing = sorted(inventory.keys() - live.keys())
    added = sorted(live.keys() - inventory.keys())
    changed = sorted(p for p in live.keys() & inventory.keys()
                     if live[p] != (inventory[p]['bytes'], inventory[p]['mtime_ns']))
    save('PLAN.v1.json', dict(missing=missing, added=added, changed=changed, excluded=excluded))
    observations, unstable = [], []
    with (O / 'DELTA.v1.jsonl').open('x', encoding='utf-8') as stream:
        for index, relative in enumerate(added + changed):
            path = V / relative
            digest = sha(path)
            after = path.stat()
            stable = live[relative] == (after.st_size, after.st_mtime_ns)
            if not stable:
                unstable.append(relative)
            row = dict(path=relative, kind='changed' if relative in hashes else 'added',
                       bytes=after.st_size, mtime_ns=after.st_mtime_ns, sha256=digest,
                       historical_sha256=hashes.get(relative), stable_during_read=stable)
            stream.write(json.dumps(row)+'\n')
            stream.flush()
            observations.append(row)
            if index % 100 == 0:
                save('PROGRESS.v1.json', dict(checked=index+1, total=len(added)+len(changed)))
    changed_proofs = [p for p, expected in used.items() if sha(p) != expected]
    result = dict(status='current_vault_delta_inventory_completed', utc=datetime.now(timezone.utc).isoformat(),
                  baseline_utc=prior['finished_utc'], baseline_files=len(inventory), current_files=len(live),
                  current_bytes=sum(size for size, _ in live.values()), missing=missing,
                  added_files=len(added), changed_stat_files=len(changed), excluded_reparse_points=excluded,
                  unstable_delta_files=unstable, changed_baseline_proofs=changed_proofs,
                  unchanged_stat_files_reusing_historical_sha=len(live.keys() & inventory.keys())-len(changed),
                  current_delta_sha_reads=len(observations),
                  changed_content_files=sum(row['kind']=='changed' and row['historical_sha256']!=row['sha256'] for row in observations),
                  delta_ledger_sha256=sha(O / 'DELTA.v1.jsonl'),
                  proof_pins=used, current_entire_vault_rehashed=False,
                  current_entire_vault_qualified=False, full_import_increment=0)
    save('RESULT.v1.json', result)
    print(json.dumps({k: result[k] for k in ('status','current_files','added_files','changed_stat_files','current_delta_sha_reads')}))

if __name__ == '__main__':
    main()
