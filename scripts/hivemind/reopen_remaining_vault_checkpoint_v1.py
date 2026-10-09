"""Independently reconcile completed per-file SHA ledgers with the physical inventory."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
P = W / 'vault-indexed-coverage-plan-v1'


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    run_path = P / 'RUN-RESULT.v1.json'
    run = read(run_path)
    count = run['completed_batches']
    out = P / 'independent-checkpoints' / f'{count:05d}.RESULT.v1.json'
    assert not out.exists(), 'Existing immutable checkpoint'
    plan_path = P / 'RESULT.v1.json'
    plan = read(plan_path)
    assert sha(plan_path) == run['plan_sha256']
    inventory_path = W / 'vault-physical-batch-plan-v1/INVENTORY.jsonl'
    assert sha(inventory_path) == plan['inventory_sha256']
    inventory = {r['path']: r for r in map(json.loads, inventory_path.read_text('utf-8-sig').splitlines())}
    audit_path = W / 'asset-library-indexed-payload-integrity-resume-v2/PAYLOAD-RESULT.v1.json'
    assert sha(audit_path) == plan['audit_receipt_sha256']
    coverage_path = P / 'COVERED.json'
    coverage = read(coverage_path)
    covered = {r['path'] for r in coverage}
    assert len(covered) == len(coverage) == plan['covered_physical_paths']
    for row in coverage:
        assert (row['bytes'], row['mtime_ns']) == (inventory[row['path']]['bytes'], inventory[row['path']]['mtime_ns'])
    scheduled = []
    pins = {str(plan_path): sha(plan_path), str(inventory_path): sha(inventory_path),
            str(audit_path): sha(audit_path), str(coverage_path): sha(coverage_path), str(Path(__file__)): sha(Path(__file__))}
    checked, total_bytes = set(), 0
    for number, batch in enumerate(plan['remaining_batches']):
        batch_path = Path(batch['path'])
        assert sha(batch_path) == batch['sha256']
        planned = read(batch_path)
        assert len(planned) == batch['files'] and sum(r['bytes'] for r in planned) == batch['bytes']
        scheduled.extend(r['path'] for r in planned)
        for row in planned:
            assert row == inventory[row['path']]
        if number >= count:
            continue
        receipt_path = P / 'checks' / f'{number:05d}.RESULT.v1.json'
        ledger_path = receipt_path.with_suffix('.jsonl')
        receipt = read(receipt_path)
        assert receipt['status'] == 'batch_current_sha_reads_completed' and not receipt['errors']
        assert receipt['plan_sha256'] == batch['sha256'] and sha(ledger_path) == receipt['ledger_sha256']
        rows = list(map(json.loads, ledger_path.read_text('utf-8-sig').splitlines()))
        assert len(rows) == receipt['checked_files'] == len(planned)
        assert [r['path'] for r in rows] == [r['path'] for r in planned]
        for row in rows:
            assert row['path'] not in checked
            checked.add(row['path'])
            before = inventory[row['path']]
            assert (row['bytes'], row['mtime_ns']) == (before['bytes'], before['mtime_ns'])
            digest = row['actual_current_sha256']
            assert len(digest) == 64 and all(c in '0123456789abcdef' for c in digest)
            total_bytes += row['bytes']
        assert sum(r['bytes'] for r in rows) == receipt['checked_bytes']
        pins[str(receipt_path)] = sha(receipt_path)
        pins[str(ledger_path)] = sha(ledger_path)
    assert len(scheduled) == len(set(scheduled)) == plan['remaining_physical_paths']
    assert covered.isdisjoint(scheduled) and covered | set(scheduled) == set(inventory)
    assert len(checked) == run['checked_files'] and total_bytes == run['checked_bytes']
    # This checkpoint verifies ledger consistency, not a second content read or
    # the final postscan stability needed to close the whole current Vault.
    result = dict(status='completed_sha_ledgers_independently_reconciled_with_exact_inventory_partition',
                  finished_utc=datetime.now(timezone.utc).isoformat(), completed_batches=count,
                  total_remaining_batches=run['batch_count'], checked_files=len(checked), checked_bytes=total_bytes,
                  indexed_and_first_batch_covered_paths=len(covered), total_inventory_paths=len(inventory),
                  exact_inventory_partition_verified=True, source_pins=pins,
                  final_current_inventory_stability_qualified=False, whole_current_vault_qualified=False,
                  actual_editor_qualified=False, runtime_qualified=False, full_import=False)
    out.parent.mkdir(exist_ok=True)
    out.write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k: result[k] for k in ('status', 'completed_batches', 'checked_files', 'checked_bytes')}))


if __name__ == '__main__':
    main()
