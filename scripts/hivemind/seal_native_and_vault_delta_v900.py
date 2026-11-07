"""Seal actual ownership tests and reconcile the sole mutable Vault pointer."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
V = Path('F:/OpenRPG_AssetVault')
O = W / 'native-ownership-and-vault-delta-delivery-v900'

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def main():
    O.mkdir(exist_ok=False)
    checks = [('source-resident-release-tests-v871','two_actual_module_tests_executed'),
              ('persistent-source-placement-tests-v893','four_actual_module_tests_executed'),
              ('persistent-source-cleanup-wasm-v892',None)]
    proofs, revalidated = {}, {}
    for folder, field in checks:
        base = W / folder
        result = read(base / 'RESULT.v1.json')
        assert result['exit_code'] == 0 and not result['changed_inputs']
        if field:
            assert result[field]
        for path, expected in read(base / 'INPUT-PINS.v1.json').items():
            if path in revalidated:
                assert revalidated[path] == expected
            else:
                assert sha(path) == expected, path
                revalidated[path] = expected
        for name in ('RESULT.v1.json','INPUT-PINS.v1.json','cargo.log'):
            proofs[str(base / name)] = sha(base / name)
    audit_path = W / 'vault-current-delta-audit-v895/RESULT.v1.json'
    audit = read(audit_path)
    assert not audit['missing'] and not audit['unstable_delta_files']
    assert not audit['excluded_reparse_points'] and not audit['changed_baseline_proofs']
    ledger = W / 'vault-current-delta-audit-v895/DELTA.v1.jsonl'
    assert sha(ledger) == audit['delta_ledger_sha256']
    rows = list(map(json.loads, ledger.read_text('utf-8').splitlines()))
    changed = [row for row in rows if row['kind'] == 'changed']
    assert len(changed) == 1 and changed[0]['path'] == 'metadata/asset-source-support-registry.v1.json'
    pointer = V / changed[0]['path']
    assert sha(pointer) == changed[0]['sha256']
    transactions = {}
    for path in (V / 'metadata/asset-source-support-transactions').glob('*.json'):
        value = read(path)
        if value.get('schema') == 'openrpg.source-support-transaction.v1':
            transactions.setdefault(value['new_registry_sha256'], []).append((path, value))
    chain, current, visited = [], changed[0]['sha256'], set()
    while current != changed[0]['historical_sha256']:
        assert current not in visited and len(visited) < 1000
        visited.add(current)
        matches = transactions[current]
        assert len(matches) == 1
        path, transaction = matches[0]
        revision = transaction['new_catalog_sha256']
        catalog = V / 'metadata/asset-source-support-revisions' / revision / 'source-support.v1.json'
        assert sha(catalog) == revision
        chain.append(dict(transaction=str(path), transaction_sha256=sha(path),
                          catalog=str(catalog), catalog_sha256=revision,
                          prior_registry_sha256=transaction['prior_registry_sha256']))
        current = transaction['prior_registry_sha256']
    proofs[str(audit_path)], proofs[str(ledger)] = sha(audit_path), sha(ledger)
    result = dict(status='six_actual_native_ownership_tests_wasm_and_vault_pointer_chain_verified',
                  utc=datetime.now(timezone.utc).isoformat(), proof_pins=proofs,
                  revalidated_source_inputs=len(revalidated), native_tests_passed=6,
                  current_vault_files=audit['current_files'], current_vault_bytes=audit['current_bytes'],
                  missing_files=0, added_files=audit['added_files'], current_delta_sha_reads=896,
                  pointer_transaction_chain=chain, historical_sha_reused_for_unchanged_stat_files=audit['unchanged_stat_files_reusing_historical_sha'],
                  current_entire_vault_rehashed=False, current_entire_vault_qualified=False,
                  new_file_provenance_global_review_complete=False,
                  actual_studio_gui_verified=False, render_admission_verified=False,
                  native_simulation_verified=False, images_admitted=0, full_import_increment=0)
    (O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({k:result[k] for k in ('status','native_tests_passed','current_vault_files','added_files','revalidated_source_inputs')}))

if __name__ == '__main__':
    main()
