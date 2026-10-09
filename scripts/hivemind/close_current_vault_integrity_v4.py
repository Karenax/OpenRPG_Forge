"""Close storage only after all batch proofs and a complete stable physical postscan."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import os

W = Path('F:/OpenRPG_ImportWork')
V = Path('F:/OpenRPG_AssetVault')
P = W / 'vault-indexed-coverage-plan-v1'
OUT = W / 'vault-current-final-review-v4'


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    run = read(P / 'RUN-RESULT.v2.json')
    assert run['status'] == 'all_uncovered_physical_sha_batches_completed', 'Owned SHA controller must finish first'
    assert run['completed_batches'] == run['batch_count']
    assert not OUT.exists(), 'Preserve final receipt'
    plan_path = P / 'RESULT.v1.json'
    plan = read(plan_path)
    assert sha(plan_path) == run['plan_sha256']
    inventory_path = W / 'vault-physical-batch-plan-v1/INVENTORY.jsonl'
    assert sha(inventory_path) == plan['inventory_sha256']
    inventory = {r['path']: r for r in map(json.loads, inventory_path.read_text('utf-8-sig').splitlines())}
    pins = {str(plan_path): sha(plan_path), str(inventory_path): sha(inventory_path), str(Path(__file__)): sha(Path(__file__))}
    audit_path = W / 'asset-library-indexed-payload-integrity-resume-v2/PAYLOAD-RESULT.v1.json'
    assert sha(audit_path) == plan['audit_receipt_sha256']
    pins[str(audit_path)] = sha(audit_path)
    audit = read(audit_path)
    assert audit['passed_count'] == 26735 and audit['refused_count'] == 0
    coverage_path = P / 'COVERED.json'
    covered = read(coverage_path)
    # Reconstruct every indexed path/hash from the pinned current metadata,
    # rather than trusting a coverage file that has no hash in the original plan.
    import map_indexed_vault_coverage_v1 as remap
    remap.OUT = W / 'vault-indexed-coverage-independent-v3'
    assert not remap.OUT.exists(), 'Inspect existing independent mapping before a new final review'
    remap.main()
    independent_coverage = remap.OUT / 'COVERED.json'
    assert read(independent_coverage) == covered
    pins[str(independent_coverage)] = sha(independent_coverage)
    pins[str(remap.OUT / 'RESULT.v1.json')] = sha(remap.OUT / 'RESULT.v1.json')
    pins[str(Path(remap.__file__))] = sha(Path(remap.__file__))
    hashes = {}
    for row in covered:
        assert row['path'] not in hashes
        assert (row['bytes'], row['mtime_ns']) == (inventory[row['path']]['bytes'], inventory[row['path']]['mtime_ns'])
        hashes[row['path']] = row['expected_sha256']
    assert len(hashes) == plan['covered_physical_paths']
    pins[str(coverage_path)] = sha(coverage_path)
    checked_files, checked_bytes = 0, 0
    for number, batch in enumerate(plan['remaining_batches']):
        batch_path = Path(batch['path'])
        assert sha(batch_path) == batch['sha256']
        pins[str(batch_path)] = batch['sha256']
        planned = read(batch_path)
        receipt_path = P / ('checks' if number < 197 else 'checks-v2') / f'{number:05d}.RESULT.v1.json'
        ledger_path = receipt_path.with_suffix('.jsonl')
        receipt = read(receipt_path)
        assert receipt['status'] == 'batch_current_sha_reads_completed' and not receipt['errors']
        assert receipt['plan_sha256'] == batch['sha256'] and sha(ledger_path) == receipt['ledger_sha256']
        rows = list(map(json.loads, ledger_path.read_text('utf-8-sig').splitlines()))
        assert [r['path'] for r in rows] == [r['path'] for r in planned]
        assert len(rows) == receipt['checked_files'] == batch['files']
        for row in rows:
            assert row['path'] not in hashes
            before = inventory[row['path']]
            assert (row['bytes'], row['mtime_ns']) == (before['bytes'], before['mtime_ns'])
            hashes[row['path']] = row['actual_current_sha256']
            checked_files += 1
            checked_bytes += row['bytes']
        assert sum(r['bytes'] for r in rows) == receipt['checked_bytes']
        pins[str(receipt_path)], pins[str(ledger_path)] = sha(receipt_path), sha(ledger_path)
    assert set(hashes) == set(inventory)
    assert checked_files == run['checked_files'] == plan['remaining_physical_paths']
    assert checked_bytes == run['checked_bytes'] == plan['remaining_bytes']
    history_path = W / 'vault-registry-evolution-review-v3/RESULT.v1.json'
    pins[str(history_path)] = sha(history_path)
    history = read(history_path)
    assert not history['errors'] and history['current_heads_verified'] == history['total_current_heads'] == 43
    for pin_set in (plan['registry_pins'], plan['source_metadata_pins'], history['source_pins']):
        for path, expected in pin_set.items():
            assert sha(Path(path)) == expected
            pins[path] = expected
    ledger = W / 'file-hashes.jsonl'
    assert sha(ledger) == '104a12417311274862b5355c1913fd33f3200580951dab2e6bde3203493c8236'
    pins[str(ledger)] = sha(ledger)
    original = list(map(json.loads, ledger.read_text('utf-8-sig').splitlines()))
    assert len(original) == 39725 and len({r['path'] for r in original}) == 39725
    cache_path = W / 'vault-yaml-cache-regeneration-review-v1/RESULT.v1.json'
    pins[str(cache_path)] = sha(cache_path)
    cache = {r['path']: r for r in read(cache_path)['rows']}
    unchanged, evolutions, errors = 0, [], []
    for old in original:
        path = old['path']
        if path not in hashes:
            errors.append(dict(path=path, reason='Missing transferred historical file'))
        elif hashes[path] == old['sha256']:
            unchanged += 1
        elif path in cache and hashes[path] == cache[path]['regenerated_sha256'] and hashes[cache[path]['source']] == cache[path]['source_sha256']:
            evolutions.append(dict(path=path, classification='verified_cache_regeneration_from_unchanged_source'))
        elif path == 'metadata/asset-import-registry.v1.json' and hashes[path] == audit['registry_sha256']:
            evolutions.append(dict(path=path, classification='current_committed_import_chains_and_exact_initial_pointer_reconstruction'))
        else:
            errors.append(dict(path=path, reason='Unclassified historical content change', old_sha256=old['sha256'], current_sha256=hashes[path]))
    # Enumerate every physical path again; never follow junctions or aliases.
    scan_root = Path('\\\\?\\' + str(V.resolve()))
    live, excluded, stack = {}, [], [scan_root]
    while stack:
        parent = stack.pop()
        with os.scandir(parent) as entries:
            for entry in entries:
                stat = entry.stat(follow_symlinks=False)
                relative = Path(entry.path).relative_to(scan_root).as_posix()
                if entry.is_symlink() or getattr(stat, 'st_file_attributes', 0) & 0x400:
                    excluded.append(relative)
                elif entry.is_dir(follow_symlinks=False):
                    stack.append(Path(entry.path))
                elif entry.is_file(follow_symlinks=False):
                    live[relative] = (stat.st_size, stat.st_mtime_ns)
    missing, added = sorted(set(inventory) - set(live)), sorted(set(live) - set(inventory))
    changed = [key for key in live.keys() & inventory.keys()
               if live[key] != (inventory[key]['bytes'], inventory[key]['mtime_ns'])]
    for path, expected in plan['registry_pins'].items():
        assert sha(Path(path)) == expected
    # All receipts, batch plans, ledgers and source metadata must still match
    # the exact bytes used above after the complete physical postscan.
    changed_evidence = [path for path, expected in pins.items() if sha(Path(path)) != expected]
    assert not changed_evidence, ('Evidence changed during final review', changed_evidence)
    qualified = not (errors or missing or added or changed or excluded)
    OUT.mkdir()
    result = dict(status='whole_current_vault_file_integrity_and_historical_transfer_reconciled' if qualified else 'final_storage_refusals_retained',
                  finished_utc=datetime.now(timezone.utc).isoformat(), physical_files=len(live),
                  physical_bytes=sum(size for size, _ in live.values()), historical_files_accounted=len(original),
                  historical_content_unchanged=unchanged, classified_evolutions=evolutions, errors=errors,
                  missing_since_inventory=missing, added_since_inventory=added, changed_since_inventory=changed,
                  excluded_reparse_points=excluded, source_pins=pins,
                  reused_completed_indexed_audit_and_first_batch_paths=len(covered), actual_remaining_sha_reads=checked_files,
                  final_complete_postscan_metadata_stable=not (missing or added or changed or excluded),
                  whole_current_vault_qualified=qualified, conversion_fidelity_qualified=False,
                  actual_editor_global_qualified=False, runtime_qualified=False, full_import=False)
    (OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k: result[k] for k in ('status', 'physical_files', 'physical_bytes', 'historical_content_unchanged', 'errors')}))
    if not qualified:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
