"""Reopen each current committed pointer back to its initial import, read only."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

V = Path('F:/OpenRPG_AssetVault')
W = Path('F:/OpenRPG_ImportWork')
OUT = W / 'vault-registry-evolution-review-v3'


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def previous_pointer(event, pack, current, transaction):
    assert event['schema_version'] == 1 and event['state'] == 'committed'
    if event['operation'] == 'apply':
        assert event['pack_id'] == pack and event['next'] == current
        return event.get('previous')
    assert event['operation'] == 'apply-atomic-pack-pair', 'Unsupported history operation'
    assert event['transaction'] == transaction
    before, after = event['previous_registry'], event['next_registry']
    assert before['schema_version'] == after['schema_version'] == 1
    assert set(before) == set(after) and set(before['packs']) == set(after['packs'])
    assert all(before[k] == after[k] for k in before if k != 'packs')
    changed = {key for key in before['packs'] if before['packs'][key] != after['packs'][key]}
    assert len(changed) == 2 and pack in changed, 'Atomic pair must change exactly two existing packs'
    assert all(after['packs'][key]['transaction'] == transaction for key in changed)
    assert after['packs'][pack] == current
    for field in ('audit_sha256', 'prepared_manifest_sha256'):
        digest = event[field]
        assert isinstance(digest, str) and len(digest) == 64 and all(c in '0123456789abcdef' for c in digest)
    return before['packs'][pack]


def transition_self_test():
    import copy
    txn = 'pair-txn'
    prior = {'schema_version': 1, 'packs': {'a': {'transaction': 'old-a'}, 'b': {'transaction': 'old-b'}, 'c': {'transaction': 'old-c'}}}
    after = copy.deepcopy(prior)
    for key in ('a', 'b'):
        after['packs'][key] = {'transaction': txn}
    event = {'schema_version': 1, 'state': 'committed', 'operation': 'apply-atomic-pack-pair', 'transaction': txn,
             'previous_registry': prior, 'next_registry': after, 'audit_sha256': 'a' * 64, 'prepared_manifest_sha256': 'b' * 64}
    assert previous_pointer(event, 'a', after['packs']['a'], txn) == prior['packs']['a']
    assert previous_pointer({'schema_version': 1, 'state': 'committed', 'operation': 'apply', 'pack_id': 'a',
                            'next': after['packs']['a'], 'previous': prior['packs']['a']}, 'a', after['packs']['a'], txn) == prior['packs']['a']
    bad_events = []
    bad = copy.deepcopy(event); bad['next_registry']['packs']['c'] = {'transaction': txn}; bad_events.append(bad)
    bad = copy.deepcopy(event); bad['next_registry']['packs']['b']['transaction'] = 'wrong'; bad_events.append(bad)
    bad = copy.deepcopy(event); bad['previous_registry']['schema_version'] = 2; bad_events.append(bad)
    bad = copy.deepcopy(event); bad['audit_sha256'] = 'not-a-sha'; bad_events.append(bad)
    bad = copy.deepcopy(event); bad['transaction'] = 'wrong'; bad_events.append(bad)
    for bad in bad_events:
        try:
            previous_pointer(bad, 'a', after['packs']['a'], txn)
        except AssertionError:
            continue
        raise AssertionError('Corrupted atomic history accepted')
    return {'positive_transitions': 2, 'negative_controls_refused': len(bad_events)}


def main():
    tests = transition_self_test()
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
                previous = previous_pointer(event, pack, current, transaction)
                chain.append(dict(transaction=transaction, path=str(path), sha256=digest))
                current = previous
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
                  history_schema_controls=tests,
                  whole_current_vault_qualified=False, vault_mutated=False, full_import=False)
    (OUT / 'RESULT.v1.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
    print(json.dumps({k: result[k] for k in ('status', 'current_heads_verified', 'total_current_heads', 'histories_read', 'errors')}))
    if errors:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
