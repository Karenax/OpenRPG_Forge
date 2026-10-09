"""Rehash one planned physical Vault batch and keep per-file durable evidence."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import os
import sys

ROOT = Path('F:/OpenRPG_AssetVault')
OUT = Path('F:/OpenRPG_ImportWork/vault-physical-batch-plan-v1')


def main():
    global OUT
    if len(sys.argv) > 2:
        assert sys.argv[2] == 'remaining', 'Unknown batch scope'
        OUT = OUT.parent / 'vault-indexed-coverage-plan-v1'
    number = int(sys.argv[1])
    assert number >= 0
    plan = OUT / ('remaining-batches' if len(sys.argv) > 2 else 'batches') / f'{number:05d}.json'
    result_path = OUT / 'checks-v2' / f'{number:05d}.RESULT.v1.json'
    physical_root = Path('\\\\?\\' + str(ROOT.resolve()))
    if result_path.exists():
        raise RuntimeError('Existing immutable batch receipt; review before any rerun')
    rows = json.loads(plan.read_text('utf-8-sig'))
    checked, errors = [], []
    result_path.parent.mkdir(exist_ok=True)
    ledger = result_path.with_suffix('.jsonl')
    with ledger.open('x', encoding='utf-8') as stream:
        for row in rows:
            path = physical_root / row['path']
            try:
                if path.resolve().is_relative_to(physical_root.resolve()) is False:
                    raise ValueError('Path escapes physical Vault')
                for ancestor in (path, *path.parents):
                    if ancestor == physical_root.parent:
                        break
                    st = ancestor.stat(follow_symlinks=False)
                    if ancestor.is_symlink() or getattr(st, 'st_file_attributes', 0) & 0x400:
                        raise ValueError('Reparse point in planned physical path')
                before = path.stat()
                if before.st_size != row['bytes'] or before.st_mtime_ns != row['mtime_ns']:
                    raise ValueError('Changed since inventory')
                digest = hashlib.sha256()
                with path.open('rb') as source:
                    while block := source.read(4 * 1024**2):
                        digest.update(block)
                after = path.stat()
                if (before.st_size, before.st_mtime_ns) != (after.st_size, after.st_mtime_ns):
                    raise ValueError('Changed during SHA read')
                actual = digest.hexdigest()
                checked.append(dict(path=row['path'], bytes=after.st_size, mtime_ns=after.st_mtime_ns,
                                    actual_current_sha256=actual,
                                    historical_sha_equal=actual == row['historical_sha256'] if row['historical_sha256'] else None))
                stream.write(json.dumps(checked[-1], ensure_ascii=False) + '\n')
                stream.flush()
            except (OSError, ValueError) as exc:
                errors.append(dict(path=row['path'], reason=str(exc)))
    result = dict(status='batch_current_sha_reads_completed' if not errors else 'batch_refusals_retained',
                  finished_utc=datetime.now(timezone.utc).isoformat(), batch=number,
                  planned_files=len(rows), checked_files=len(checked),
                  checked_bytes=sum(r['bytes'] for r in checked), errors=errors,
                  plan_sha256=hashlib.sha256(plan.read_bytes()).hexdigest(),
                  ledger_sha256=hashlib.sha256(ledger.read_bytes()).hexdigest(),
                  windows_extended_paths_used=True, helper_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                  whole_current_vault_qualified=False, full_import=False)
    result_path.write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
