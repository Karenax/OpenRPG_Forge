"""Reopen each current committed pointer back to its initial import, read only."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

V = Path('F:/OpenRPG_AssetVault')
W = Path('F:/OpenRPG_ImportWork')
OUT = W / 'vault-registry-evolution-review-v2'


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def main():
    assert not OUT.exists(), 'Preserve earlier evidence'
    registry_path = V / 'metadata/asset-import-registry.v1.json'
    audit = read(W / 'asset-library-indexed-payload-integrity-resume-v2/PAYLOAD-RESULT.v1.json')
    registry_pin = sha(registry_path)
    assert registry_pin == audit['registry_sha256']
    ledger = W / 'file-hashes.jsonl'
    assert sha(ledger) == '104a12417311274862b5355c1913fd33f3200580951dab2e6bde3203493c8236'
    historical = next(row for row in map(json.loads, ledger.read_text('utf-8-sig').splitlines())
                      if row['path'] == 'metadata/asset-import-registry.v1.json')
    history, matches, pins = {}, [], {str(registry_path): registry_pin}
    for path in sorted((V / 'metadata/asset-import-history').glob('*.json')):
        event, digest = read(path), sha(path)
        history[path.stem] = (path, event, digest)
        pins[str(path)] = digest
        for field in ('previous', 'next'):
            pointer = event.get(field)
            if pointer:
                encoded = (json.dumps({'packs': {event['pack_id']: pointer}, 'schema_version': 1},
                                     ensure_ascii=False, sort_keys=True, indent=2, allow_nan=False) + '\n').encode('utf-8')
                if len(encoded) == historical['bytes'] and hashlib.sha256(encoded).hexdigest() == historical['sha256']:
                    matches.append(dict(path=str(path), field=field, sha256=digest))
    assert matches, 'Transferred original pointer cannot be reconstructed exactly'
    rows, errors = [], []
    for pack, pointer in read(registry_path)['packs'].items():
        try:
            root = V / pointer['path']
            assert root.resolve().is_relative_to((V / 'metadata/asset-import-revisions').resolve())
            assert root.name == pointer['revision']
            for relative, expected in pointer['files'].items():
                path = root / relative
                assert path.resolve().is_relative_to(root.resolve())
                assert sha(path) == expected
                pins[str(path)] = expected
            current, seen, chain = pointer, set(), []
            while current:
                transaction = current['transaction']
                assert transaction not in seen
                seen.add(transaction)
                path, event, digest = history[transaction]
                assert event['pack_id'] == pack and event['next'] == current
                assert event['state'] == 'committed' and event['operation'] == 'apply'
                chain.append(dict(transaction=transaction, path=str(path), sha256=digest))
                current = event.get('previous')
            rows.append(dict(pack_id=pack, current_pointer=pointer, chain=chain, complete_to_initial_import=True))
        except (AssertionError, KeyError, OSError) as exc:
            errors.append(dict(pack_id=pack, reason=repr(exc)))
    for path, expected in pins.items():
        assert sha(Path(path)) == expected, 'History changed during review'
    OUT.mkdir()
    result = dict(status='current_committed_registry_heads_and_complete_import_chains_verified' if not errors else 'registry_history_refusals_retained',
                  finished_utc=datetime.now(timezone.utc).isoformat(), current_heads_verified=len(rows),
                  total_current_heads=len(read(registry_path)['packs']), histories_read=len(history), rows=rows,
                  historical_exact_reconstruction_matches=matches, errors=errors, source_pins=pins,
                  global_interleaved_transaction_order_reconstructed=False, retouch_content_semantics_qualified=False,
                  whole_current_vault_qualified=False, vault_mutated=False, full_import=False)
    (OUT / 'RESULT.v1.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
    print(json.dumps({k: result[k] for k in ('status', 'current_heads_verified', 'total_current_heads', 'histories_read', 'errors')}))
    if errors:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
