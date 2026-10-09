"""Inventory physical Vault files and bound further integrity work; no content verdict."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import os

W = Path('F:/OpenRPG_ImportWork')
ROOT = Path('F:/OpenRPG_AssetVault')
OUT = W / 'vault-physical-batch-plan-v1'
BASELINE = W / 'vault-current-integrity-v2/current-file-hashes.jsonl'


def main():
    OUT.mkdir(exist_ok=True)
    baseline = {}
    with BASELINE.open(encoding='utf-8-sig') as stream:
        for line in stream:
            row = json.loads(line)
            baseline[row['path']] = row
    rows, excluded, errors = [], [], []
    stack = [ROOT]
    while stack:
        parent = stack.pop()
        try:
            with os.scandir(parent) as entries:
                for entry in entries:
                    try:
                        st = entry.stat(follow_symlinks=False)
                        rel = Path(entry.path).relative_to(ROOT).as_posix()
                        if entry.is_symlink() or getattr(st, 'st_file_attributes', 0) & 0x400:
                            excluded.append(rel)
                        elif entry.is_dir(follow_symlinks=False):
                            stack.append(Path(entry.path))
                        elif entry.is_file(follow_symlinks=False):
                            old = baseline.get(rel)
                            same = bool(old and old['bytes'] == st.st_size and old['mtime_ns'] == st.st_mtime_ns)
                            rows.append(dict(path=rel, bytes=st.st_size, mtime_ns=st.st_mtime_ns,
                                             baseline_metadata_equal=same,
                                             historical_sha256=old.get('sha256') if old else None,
                                             current_content_sha_verified=False))
                    except OSError as exc:
                        errors.append(dict(path=entry.path, error=str(exc)))
        except OSError as exc:
            errors.append(dict(path=str(parent), error=str(exc)))
    rows.sort(key=lambda r: (r['baseline_metadata_equal'], r['path']))
    inventory = OUT / 'INVENTORY.jsonl'
    with inventory.open('w', encoding='utf-8') as stream:
        for row in rows:
            stream.write(json.dumps(row, ensure_ascii=False) + '\n')
    batch_dir = OUT / 'batches'
    batch_dir.mkdir(exist_ok=True)
    batches, current, total = [], [], 0
    def flush():
        nonlocal current, total
        if not current:
            return
        path = batch_dir / f'{len(batches):05d}.json'
        path.write_text(json.dumps(current, ensure_ascii=False, indent=2), 'utf-8')
        batches.append(dict(path=str(path), files=len(current), bytes=total,
                            sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                            status='planned_not_hashed'))
        current, total = [], 0
    for row in rows:
        if current and (len(current) >= 512 or total + row['bytes'] > 2 * 1024**3):
            flush()
        current.append(row)
        total += row['bytes']
    flush()
    live = {r['path'] for r in rows}
    result = dict(status='physical_inventory_and_bounded_batches_prepared' if not errors else 'inventory_errors_require_resolution',
                  finished_utc=datetime.now(timezone.utc).isoformat(), root=str(ROOT),
                  physical_files=len(rows), physical_bytes=sum(r['bytes'] for r in rows),
                  baseline_metadata_equal=sum(r['baseline_metadata_equal'] for r in rows),
                  new_or_changed_metadata=sum(not r['baseline_metadata_equal'] for r in rows),
                  missing_historical_paths=sorted(set(baseline) - live), excluded_reparse_points=excluded,
                  errors=errors, inventory_sha256=hashlib.sha256(inventory.read_bytes()).hexdigest(),
                  batches=batches, whole_current_vault_qualified=False, full_import=False,
                  note='Metadata equality retains historical evidence only; it is not a current content SHA verification. Oversize files form single-file batches. Indexed audit coverage must be mapped before hashing remaining files.')
    (OUT / 'RESULT.v1.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
    print(json.dumps({k: result[k] for k in ('status', 'physical_files', 'physical_bytes', 'baseline_metadata_equal', 'new_or_changed_metadata')}))
    print(f'Prepared {len(batches)} durable batches; excluded links {len(excluded)}; errors {len(errors)}')


if __name__ == '__main__':
    main()
